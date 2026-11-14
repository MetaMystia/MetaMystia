// 总览：统计卡片、Gateway 趋势、实例列表及登记 / 退役 / 删除。
import {
  h, icon, iconButton, api, enc, openModal, field, textInput, actionForm, compareVersion,
} from './core.js';
import { lineChart, series } from './chart.js';

export function mountOverview(root, app) {
  const state = { instances: null, statuses: new Map(), gateway: null, history: null, failed: false, filter: '' };
  const stats = h('div', { class: 'stats' });
  const charts = h('div', { class: 'charts two' });
  const count = h('span', { class: 'count' });
  const body = h('div');
  const search = h('input', {
    type: 'search', placeholder: '筛选 ID、版本、端口',
    oninput: event => { state.filter = event.target.value; renderTable(); },
  });
  root.replaceChildren(h('div', { class: 'page' },
    stats,
    h('section', { class: 'panel' },
      h('div', { class: 'panel-head' },
        h('span', { class: 'panel-title' }, '实例', count),
        h('span', { class: 'spacer' }),
        h('label', { class: 'search' }, icon('search'), search),
        h('button', { type: 'button', class: 'btn primary', onclick: () => openRegister(state, refresh) }, icon('plus'), '登记实例')),
      body),
    charts));

  async function refresh() {
    try {
      const [gateway, instances, history] = await Promise.all([
        api('GET', 'gateway').catch(() => api('GET', 'status')),
        api('GET', 'instances'),
        api('GET', 'history').catch(() => null),
      ]);
      const statuses = new Map(await Promise.all(instances.map(async record => {
        try { return [record.id, { status: await api('GET', 'instances/' + enc(record.id) + '/status') }]; }
        catch (error) { return [record.id, { error: error.status }]; }
      })));
      Object.assign(state, { gateway, instances, statuses, history, failed: false });
      app.online(true);
    } catch (error) {
      if (error.status === 401) return;
      state.failed = true;
      app.online(false);
    }
    render();
  }

  function render() {
    renderStats();
    renderTable();
    renderCharts();
  }

  function renderStats() {
    const records = state.instances ?? [];
    const known = state.instances != null;
    let players = 0, reachable = 0;
    for (const record of records) {
      const entry = state.statuses.get(record.id);
      if (entry?.status) { players += entry.status.players; reachable++; }
    }
    const failing = records.length - reachable;
    const rejected = state.gateway?.totals ? Object.values(state.gateway.totals.rejected).reduce((a, b) => a + b, 0) : null;
    const stat = (label, value, note, cls, href) => h(href ? 'a' : 'div', { class: 'stat' + (href ? ' link' : ''), href },
      h('div', { class: 'stat-label' }, label),
      h('div', { class: 'stat-value' + (cls ? ' ' + cls : '') }, value),
      h('div', { class: 'stat-note' }, note));
    stats.replaceChildren(
      stat('转发连接', state.gateway?.connections ?? '—',
        state.gateway?.totals ? '累计转发 ' + state.gateway.totals.forwarded + ' · 拒绝 ' + rejected : '经 Gateway 的 TCP 连接', null, '#/gateway'),
      stat('在线玩家', known ? players : '—', '可访问实例的人数合计'),
      stat('接客实例', known ? records.filter(r => r.state === 'active').length : '—', known ? '共登记 ' + records.length + ' 个' : ''),
      stat('不可访问', known ? failing : '—', '状态查询失败的实例', known && failing > 0 ? 'err' : null),
    );
  }

  function renderCharts() {
    if (!state.history) { charts.replaceChildren(); return; }
    charts.replaceChildren(
      lineChart({ title: 'Gateway 当前连接', points: series(state.history, 'connections'), current: state.gateway?.connections }),
      lineChart({ title: '每分钟转发连接', points: series(state.history, 'forwarded') }),
    );
  }

  function renderTable() {
    const filter = state.filter.trim().toLowerCase();
    const all = sorted(state.instances ?? []);
    const records = all.filter(r => matches(r, filter));
    count.textContent = state.instances == null ? '' : filter ? records.length + ' / ' + all.length : String(all.length);
    if (state.instances == null) {
      body.replaceChildren(h('div', { class: 'empty' }, state.failed ? '无法访问 Gateway' : '加载中…'));
      return;
    }
    if (all.length === 0) {
      body.replaceChildren(h('div', { class: 'empty' },
        h('p', null, '没有登记的实例。新实例通常由发布流程自动登记。'),
        h('button', { type: 'button', class: 'btn', onclick: () => openRegister(state, refresh) }, icon('plus'), '手动登记')));
      return;
    }
    if (records.length === 0) { body.replaceChildren(h('div', { class: 'empty' }, '没有匹配的实例')); return; }
    const rows = records.map(record => {
      const r = record.registration;
      const entry = state.statuses.get(record.id);
      const status = entry?.status;
      const target = record.targetModVersion ?? status?.targetModVersion;
      const open = () => { location.hash = '#/instance/' + enc(record.id); };
      return h('tr', { 'data-id': record.id, class: 'clickable', onclick: event => { if (!event.target.closest('button')) open(); } },
        cell('ID', 'wide', h('a', { class: 'id-cell', href: '#/instance/' + enc(record.id) }, record.id), h('span', { class: 'sub' }, r.adminUrl)),
        cell('版本', null, h('span', { class: 'mono' }, r.modVersion), h('span', { class: 'sub' }, r.gameVersion)),
        cell('路由', null, routePill(record), target ? h('span', { class: 'sub' }, '→ ' + target) : null),
        cell('实例', null, instancePill(entry)),
        cell('玩家', 'num', status ? status.players : '—'),
        cell('后端', null, h('span', { class: 'mono' }, r.host + ':' + r.port)),
        h('td', { class: 'actions' }, h('span', { class: 'row-actions' },
          h('button', { type: 'button', class: 'btn small ghost', onclick: () => openDrain(record, state, refresh) },
            icon('sunset'), record.state === 'active' ? '退役' : '重发退役'),
          iconButton('trash', '删除登记', () => openDelete(record, state, refresh), 'danger'),
          iconButton('chev', '详情', open))));
    });
    body.replaceChildren(h('table', { class: 'table' },
      h('thead', null, h('tr', null,
        h('th', null, 'ID'), h('th', null, '模组 / 游戏版本'), h('th', null, '路由'), h('th', null, '实例'),
        h('th', { class: 'num' }, '玩家'), h('th', null, '后端'), h('th', null, ''))),
      h('tbody', null, rows)));
  }

  render();
  return { refresh, dispose() {} };
}

// ---------- 列表辅助 ----------

export function cell(label, cls, ...children) {
  return h('td', { class: cls }, h('span', { class: 'cell-label' }, label), children);
}

function sorted(records) {
  const rank = r => (r.state === 'active' ? 0 : 1);
  return [...records].sort((a, b) =>
    compareVersion(a.registration.gameVersion, b.registration.gameVersion) || rank(a) - rank(b) ||
    compareVersion(b.registration.modVersion, a.registration.modVersion) || a.id.localeCompare(b.id));
}

function matches(record, filter) {
  if (!filter) return true;
  const r = record.registration;
  return [record.id, r.gameVersion, r.modVersion, r.host + ':' + r.port, r.adminUrl, record.targetModVersion ?? '']
    .some(text => text.toLowerCase().includes(filter));
}

export function routePill(record) {
  return record.state === 'active' ? h('span', { class: 'pill ok' }, '接客') : h('span', { class: 'pill warn' }, '退役中');
}

export function instancePill(entry) {
  if (!entry) return h('span', { class: 'pill idle' }, '查询中');
  if (entry.error != null) {
    const text = { 0: '网络错误', 400: '身份不符', 404: '登记已删除', 502: '不可访问' }[entry.error] ?? 'HTTP ' + entry.error;
    return h('span', { class: 'pill bad', title: entry.error === 400 ? '实例返回的 ID、版本或端口与登记不一致' : null }, text);
  }
  return entry.status.state === 'running' ? h('span', { class: 'pill ok' }, '运行中')
    : h('span', { class: 'pill warn' }, entry.status.state === 'draining' ? '退役中' : entry.status.state);
}

// ---------- 对话框 ----------

function nextPort(first, last, used) {
  for (let port = first; port <= last; port++) if (!used.has(port)) return port;
  return '';
}

function openRegister(state, done) {
  const records = state.instances ?? [];
  const tcp = new Set(records.map(r => r.registration.port));
  const admin = new Set(records.map(r => Number(new URL(r.registration.adminUrl).port)));
  const game = sorted(records).find(r => r.state === 'active')?.registration.gameVersion ?? '';
  const adminPort = nextPort(42000, 42099, admin);
  openModal('登记实例',
    h('p', null, 'Gateway 会先查询实例的 ', h('code', null, '/admin/status'), '，ID、版本和端口一致且处于运行中才会登记并开始接客。新实例通常由发布流程自动登记，这里用于手动补登。'),
    actionForm([
      field('实例 ID', textInput('id', '', { pattern: '[A-Za-z0-9][A-Za-z0-9._\\-]{0,127}', placeholder: '与实例 MM_INSTANCE_ID 一致' })),
      h('div', { class: 'field-row' },
        field('游戏版本', textInput('gameVersion', game, { maxlength: 64 })),
        field('模组版本', textInput('modVersion', '', { maxlength: 64, placeholder: '0.31.0' }))),
      h('div', { class: 'field-row' },
        field('TCP 主机', textInput('host', '127.0.0.1')),
        field('TCP 端口', textInput('port', nextPort(41000, 41099, tcp), { type: 'number', min: 1, max: 65535 }))),
      field('管理地址', textInput('adminUrl', adminPort ? 'http://127.0.0.1:' + adminPort : '', { type: 'url' }), '主机与管理地址只允许回环 IP'),
    ], '登记', 'primary', async data => {
      const id = String(data.get('id')).trim();
      await api('PUT', 'instances/' + enc(id), {
        gameVersion: String(data.get('gameVersion')).trim(), modVersion: String(data.get('modVersion')).trim(),
        host: String(data.get('host')).trim(), port: Number(data.get('port')), adminUrl: String(data.get('adminUrl')).trim(),
      });
      return '已登记 ' + id;
    }, {
      400: '参数无效，或实例返回的 ID、版本、端口与填写不一致',
      409: 'ID 已被占用、地址或版本与现有登记冲突，或实例不在运行',
      502: '无法访问实例管理地址',
    }, done));
}

/** 通知 ID 只用于去重：同一 ID 玩家只会收到一次提示。按目标版本生成，重试时保持不变。 */
export function noticeIdFor(target) { return 'retire-to-' + target; }

export function openDrain(record, state, done) {
  const r = record.registration;
  const successor = sorted(state.instances ?? []).find(other => other.id !== record.id && other.state === 'active' &&
    other.registration.gameVersion === r.gameVersion && compareVersion(other.registration.modVersion, r.modVersion) > 0);
  const status = state.statuses?.get(record.id)?.status;
  const retry = record.state !== 'active';
  const targetInput = textInput('targetModVersion', record.targetModVersion ?? status?.targetModVersion ?? successor?.registration.modVersion ?? '', { maxlength: 64 });
  const noticeInput = textInput('noticeId', '', { maxlength: 128, required: false, placeholder: '' });
  const syncNotice = () => { noticeInput.placeholder = noticeIdFor(targetInput.value.trim() || '目标版本'); };
  targetInput.addEventListener('input', syncNotice);
  syncNotice();
  openModal(retry ? '重发退役请求 ' + record.id : '退役 ' + record.id,
    h('p', null, retry
      ? '路由已是退役中。用相同内容重发是安全的，玩家不会重复收到提示。'
      : ['Gateway 先把路由标记为退役中并保存，再通知实例。新玩家连接 ', h('code', null, r.modVersion), ' 时会被提示升级到目标版本；已有玩家收到通知，全部离开后实例自行退出。']),
    !retry && status?.players ? h('div', { class: 'notice' }, '当前有 ' + status.players + ' 名玩家在线，不会被强制下线。') : null,
    !retry && !successor ? h('div', { class: 'notice bad' }, '没有同游戏版本的更新接客实例，请确认目标版本已上线。') : null,
    actionForm([
      field('目标模组版本', targetInput, '提示玩家升级到的版本'),
      h('details', { class: 'advanced', open: Boolean(status?.noticeId && status.noticeId !== noticeIdFor(targetInput.value.trim())) },
        h('summary', null, '高级'),
        field('通知 ID', noticeInput, '留空自动生成。只用于去重：同一 ID 玩家只会收到一次提示，一般不用填。')),
    ], retry ? '重发' : '退役', retry ? 'primary' : 'danger', async data => {
      const target = String(data.get('targetModVersion')).trim();
      const notice = String(data.get('noticeId') ?? '').trim() || (retry && status?.noticeId) || noticeIdFor(target);
      await api('POST', 'instances/' + enc(record.id) + '/drain', { noticeId: notice, targetModVersion: target });
      return record.id + ' 已收到退役请求';
    }, {
      400: '参数无效', 404: '登记不存在',
      502: '路由已标记为退役中，但转发给实例失败；可用相同内容重试',
    }, done));
}

export function openDelete(record, state, done) {
  const status = state.statuses?.get(record.id)?.status;
  openModal('删除登记 ' + record.id,
    h('p', null, '只删除 Gateway 中的路由记录，不关闭已有连接，也不停止实例进程。'),
    record.state === 'active' ? h('div', { class: 'notice bad' }, '该实例仍在接客，删除后 ' + record.registration.modVersion + ' 的新连接会被拒绝。') : null,
    status && status.players > 0 ? h('div', { class: 'notice' }, '实例仍有 ' + status.players + ' 名玩家在线。') : null,
    actionForm([], '删除', 'danger', async () => {
      await api('DELETE', 'instances/' + enc(record.id));
      return '已删除 ' + record.id;
    }, { 404: '登记不存在' }, done));
}


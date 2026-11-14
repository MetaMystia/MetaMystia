// 实例详情：概览、玩家、房间、日志、操作。实例接口经 Gateway 的 /api/ 通用转发。
import {
  h, icon, iconButton, api, enc, openModal, field, textInput, actionForm, toast, failureText,
  fmtBytes, fmtDuration, since, fmtTime, fmtDateTime, named, dlcText, STAGE_NAMES, MAP_NAMES, LEVEL_NAMES,
} from './core.js';
import { lineChart, series } from './chart.js';
import { cell, routePill, instancePill, openDrain, openDelete } from './overview.js';

const TABS = [['overview', '概览'], ['players', '玩家'], ['rooms', '房间'], ['logs', '日志'], ['actions', '操作']];
const MAX_LOGS = 2000;

export function mountInstance(root, app, id, tab) {
  const base = 'instances/' + enc(id);
  const state = {
    record: null, status: null, statusError: null, details: null, detailsError: null, history: null, historyAt: 0,
    logs: [], logNext: null, logLevels: new Set(['info', 'warning', 'error', 'chat']), logFilter: '', follow: true,
    playerFilter: '', loaded: false,
  };
  const header = h('div', { class: 'detail-head' });
  const tabs = h('nav', { class: 'tabs' });
  const content = h('div', { class: 'detail-body' });
  root.replaceChildren(h('div', { class: 'page' }, header, h('section', { class: 'panel' }, tabs, content)));

  let current = TABS.some(t => t[0] === tab) ? tab : 'overview';
  let logView = null;
  let view = null; // 当前标签页：{ update() }，刷新时只更新数据

  async function refresh() {
    const loadHistory = Date.now() - state.historyAt > 55_000;
    const [instances, status, details, history, logs] = await Promise.allSettled([
      api('GET', 'instances'),
      api('GET', base + '/status'),
      api('GET', base + '/api/details'),
      loadHistory ? api('GET', base + '/api/history') : Promise.resolve(state.history),
      api('GET', base + '/api/logs' + (state.logNext != null ? '?after=' + state.logNext : '?limit=500')),
    ]);
    if ([instances, status, details].some(r => r.status === 'rejected' && r.reason.status === 401)) return;
    if (instances.status === 'rejected') { app.online(false); renderAll(); return; }
    app.online(true);
    state.instances = instances.value;
    state.record = instances.value.find(r => r.id === id) ?? null;
    state.status = status.status === 'fulfilled' ? status.value : null;
    state.statusError = status.status === 'rejected' ? status.reason.status : null;
    state.details = details.status === 'fulfilled' ? details.value : null;
    state.detailsError = details.status === 'rejected' ? details.reason.status : null;
    if (history.status === 'fulfilled') { state.history = history.value; if (loadHistory) state.historyAt = Date.now(); }
    if (logs.status === 'fulfilled' && logs.value) {
      const page = logs.value;
      // 序号回退说明实例重启或换了进程，重新开始。
      if (state.logNext != null && page.next < state.logNext) { state.logs = []; state.logNext = null; }
      else {
        state.logs.push(...page.entries);
        if (state.logs.length > MAX_LOGS) state.logs.splice(0, state.logs.length - MAX_LOGS);
        state.logNext = page.next;
        logView?.append(page.entries);
      }
    }
    state.loaded = true;
    renderAll();
  }

  function renderAll() {
    renderHeader();
    renderTabs();
    if (view?.update && Boolean(state.details) === view.withDetails) view.update();
    else renderContent();
  }

  function renderHeader() {
    const record = state.record;
    const entry = state.status ? { status: state.status } : state.statusError != null ? { error: state.statusError } : null;
    const r = record?.registration;
    header.replaceChildren(
      h('a', { class: 'back', href: '#/' }, icon('left'), '实例'),
      h('div', { class: 'detail-title' },
        h('h1', { class: 'mono' }, id),
        record ? routePill(record) : null,
        state.loaded ? instancePill(entry) : null,
        h('span', { class: 'spacer' }),
        h('span', { class: 'title-actions' }, record ? h('button', { type: 'button', class: 'btn small', onclick: () => openDrain(record, { instances: state.instances ?? [record], statuses: new Map([[id, entry]]) }, refresh) },
          icon('sunset'), record.state === 'active' ? '退役' : '重发退役') : null,
        record ? iconButton('trash', '删除登记', () => openDelete(record, { statuses: new Map([[id, entry]]) }, () => { location.hash = '#/'; }), 'danger') : null)),
      h('div', { class: 'detail-meta' },
        r ? [meta('模组', r.modVersion), meta('游戏', r.gameVersion), meta('后端', r.host + ':' + r.port), meta('管理', r.adminUrl)] : null,
        state.loaded && !record ? h('span', { class: 'login-error' }, '该实例已不在 Gateway 登记中') : null));
  }

  function renderTabs() {
    const players = state.details?.players.length, rooms = state.details?.rooms.length;
    tabs.replaceChildren(...TABS.map(([key, label]) => h('a', {
      class: 'tab' + (key === current ? ' active' : ''), href: '#/instance/' + enc(id) + '/' + key,
    }, label, key === 'players' && players != null ? h('span', { class: 'count' }, players) : null,
      key === 'rooms' && rooms != null ? h('span', { class: 'count' }, rooms) : null)));
  }

  function renderContent() {
    view = null;
    if (!state.loaded) { content.replaceChildren(h('div', { class: 'empty' }, '加载中…')); return; }
    if (!state.details && current !== 'actions') {
      content.replaceChildren(h('div', { class: 'empty' }, detailsErrorText(state.detailsError)));
      return;
    }
    logView = null;
    view = ({ overview: renderOverview, players: renderPlayers, rooms: renderRooms, logs: renderLogs, actions: renderActions })[current]() ?? null;
    if (view) view.withDetails = Boolean(state.details);
  }

  // ---------- 概览 ----------

  function renderOverview() {
    const d = state.details, p = d.process;
    const cpu = series(state.history, 'cpu');
    const stat = (label, value, note) => h('div', { class: 'stat' },
      h('div', { class: 'stat-label' }, label), h('div', { class: 'stat-value' }, value), h('div', { class: 'stat-note' }, note));
    content.replaceChildren(h('div', { class: 'detail-section' },
      h('div', { class: 'stats' },
        stat('在线玩家', d.players.length + ' / ' + d.maxPlayers, '房间 ' + d.rooms.length + ' 个'),
        stat('运行时长', fmtDuration(p.uptimeSeconds), '启动于 ' + fmtDateTime(p.startedAt)),
        stat('内存', fmtBytes(p.workingSetBytes), '托管堆 ' + fmtBytes(p.managedBytes)),
        stat('CPU', cpu.length ? cpu[cpu.length - 1][1] + '%' : '—', '线程 ' + p.threads + ' · GC ' + p.gcCollections.join('/'))),
      h('div', { class: 'info-grid' },
        infoCard('实例', [
          ['状态', d.state === 'running' ? '运行中' : '退役中'],
          ['协议版本', d.protocol],
          ['游戏端口', d.port],
          ['运行时', p.runtime + ' · ' + p.processors + ' 核'],
        ]),
        infoCard('配置', [
          ['人数上限', d.maxPlayers],
          ['敏感词', d.settings.chatFilterWords ? d.settings.chatFilterWords + ' 个词条' : '未启用'],
          ['聊天记录', d.settings.logChat ? '开启' : '关闭'],
          ['欢迎公告', d.settings.welcomeMessages + ' 条'],
        ]),
        d.retirement ? infoCard('退役', [
          ['目标版本', d.retirement.targetModVersion],
          ['通知 ID', d.retirement.noticeId],
        ]) : null),
      h('div', { class: 'charts three' },
        lineChart({ title: '在线玩家', points: series(state.history, 'players'), current: d.players.length }),
        lineChart({ title: 'CPU', points: cpu, format: v => Math.round(v) + '%' }),
        lineChart({ title: '内存', points: series(state.history, 'memoryMb'), format: v => Math.round(v) + ' MB', current: p.workingSetBytes / 1048576 }))));
    return { update: renderOverview };
  }

  // ---------- 玩家 ----------

  function renderPlayers() {
    const tableHost = h('div');
    const summary = h('span', { class: 'muted' });
    const search = h('input', { type: 'search', placeholder: '昵称、UID、房间号', value: state.playerFilter,
      oninput: event => { state.playerFilter = event.target.value; update(); } });
    function update() {
      const d = state.details;
      const filter = state.playerFilter.trim().toLowerCase();
      const players = d.players.filter(p => !filter || p.name.toLowerCase().includes(filter) || String(p.uid) === filter || (p.room ?? '').toLowerCase() === filter);
      summary.textContent = d.players.length + ' / ' + d.maxPlayers + ' 人';
      tableHost.replaceChildren(players.length === 0
        ? h('div', { class: 'empty' }, d.players.length ? '没有匹配的玩家' : '当前没有在线玩家')
        : h('table', { class: 'table' },
          h('thead', null, h('tr', null, ['玩家', '位置', '阶段', '地图', 'DLC / 资源包', '在线时长', ''].map(t => h('th', null, t)))),
          h('tbody', null, players.map(p => h('tr', null,
            cell('玩家', 'wide', h('b', null, p.name), h('span', { class: 'sub' }, 'UID ' + p.uid)),
            cell('位置', null, p.room ? h('a', { class: 'mono', href: '#/instance/' + enc(id) + '/rooms', title: '查看房间' }, '房间 ' + p.room) : h('span', { class: 'muted' }, '大厅')),
            cell('阶段', null, named(STAGE_NAMES, p.stage)),
            cell('地图', null, p.map === 'Unknown' ? h('span', { class: 'muted' }, '—') : named(MAP_NAMES, p.map)),
            cell('DLC / 资源包', null, dlcText(p.dlc), p.packIds.length ? h('span', { class: 'sub', title: p.packIds.join('\n') }, p.packIds.length + ' 个资源包') : null),
            cell('在线时长', null, since(p.connectedAt)),
            h('td', { class: 'actions' }, h('span', { class: 'row-actions' },
              p.room ? h('button', { type: 'button', class: 'btn small ghost', onclick: () => confirmPlayer(p, 'leave') }, icon('door'), '移出房间') : null,
              h('button', { type: 'button', class: 'btn small ghost danger-text', onclick: () => confirmPlayer(p, 'kick') }, icon('boot'), '踢出'))))))));
    }
    content.replaceChildren(h('div', { class: 'detail-section flush' },
      h('div', { class: 'toolbar' }, h('label', { class: 'search' }, icon('search'), search), h('span', { class: 'spacer' }), summary),
      tableHost));
    update();
    return { update };
  }

  function confirmPlayer(player, action) {
    const kick = action === 'kick';
    openModal((kick ? '踢出 ' : '移出房间 ') + player.name,
      h('p', null, kick
        ? ['把 ', h('b', null, player.name), '（UID ', player.uid, '）断开连接。玩家可以重新连接。']
        : ['把 ', h('b', null, player.name), ' 移出房间 ', h('code', null, player.room), '，玩家回到大厅，连接保留。']),
      actionForm([], kick ? '踢出' : '移出', 'danger', async () => {
        await api('POST', base + '/api/players/' + player.uid + '/' + action);
        return (kick ? '已踢出 ' : '已移出房间：') + player.name;
      }, null, refresh));
  }

  // ---------- 房间 ----------

  function renderRooms() {
    const d = state.details;
    if (d.rooms.length === 0) { content.replaceChildren(h('div', { class: 'empty' }, '当前没有房间')); return { update: renderRooms }; }
    content.replaceChildren(h('div', { class: 'detail-section' }, h('div', { class: 'room-grid' }, d.rooms.map(room => {
      const members = d.players.filter(p => p.room === room.id);
      return h('div', { class: 'room' },
        h('div', { class: 'room-head' },
          h('span', { class: 'room-id mono' }, room.id),
          h('span', { class: 'pill ' + (room.joinable ? 'ok' : 'idle') }, room.joinable ? '开放加入' : '关闭加入'),
          h('span', { class: 'spacer' }),
          h('span', { class: 'mono muted' }, room.count + ' / ' + room.maxPlayers)),
        h('ul', { class: 'room-members' }, members.map(p => h('li', null,
          h('span', null, p.name, p.uid === room.host ? h('span', { class: 'host-tag' }, '房主') : null),
          h('span', { class: 'muted' }, named(STAGE_NAMES, p.stage) + (p.map !== 'Unknown' ? ' · ' + named(MAP_NAMES, p.map) : ''))))));
    }))));
    return { update: renderRooms };
  }

  // ---------- 日志 ----------

  function renderLogs() {
    const list = h('div', { class: 'log-list scroll' });
    const levelChips = Object.entries(LEVEL_NAMES).map(([level, label]) => h('button', {
      type: 'button', class: 'level-chip ' + level + (state.logLevels.has(level) ? ' on' : ''),
      onclick: event => {
        state.logLevels.has(level) ? state.logLevels.delete(level) : state.logLevels.add(level);
        event.currentTarget.classList.toggle('on');
        redraw();
      },
    }, label));
    const search = h('input', { type: 'search', placeholder: '搜索日志', value: state.logFilter,
      oninput: event => { state.logFilter = event.target.value; redraw(); } });
    const followBtn = h('button', { type: 'button', class: 'btn small' + (state.follow ? ' on' : ''), onclick: () => {
      state.follow = !state.follow; followBtn.classList.toggle('on', state.follow); if (state.follow) list.scrollTop = list.scrollHeight;
    } }, icon('down'), '跟随');
    const visible = e => state.logLevels.has(e.level) && (!state.logFilter || e.message.toLowerCase().includes(state.logFilter.toLowerCase()));
    const line = e => h('div', { class: 'log-line ' + e.level },
      h('span', { class: 'log-time mono', title: fmtDateTime(e.time) }, fmtTime(e.time)),
      h('span', { class: 'log-level' }, LEVEL_NAMES[e.level] ?? e.level),
      h('span', { class: 'log-msg' }, e.message));
    function redraw() {
      const entries = state.logs.filter(visible);
      list.replaceChildren(...(entries.length ? entries.map(line) : [h('div', { class: 'empty' }, state.logs.length ? '没有匹配的日志' : '暂无日志')]));
      if (state.follow) list.scrollTop = list.scrollHeight;
    }
    list.addEventListener('scroll', () => {
      const atBottom = list.scrollHeight - list.scrollTop - list.clientHeight < 24;
      if (state.follow !== atBottom) { state.follow = atBottom; followBtn.classList.toggle('on', atBottom); }
    });
    content.replaceChildren(h('div', { class: 'detail-section flush logs' },
      h('div', { class: 'toolbar' }, h('div', { class: 'level-chips' }, levelChips), h('label', { class: 'search' }, icon('search'), search),
        h('span', { class: 'spacer' }), h('span', { class: 'muted' }, '内存中保留最近 ' + MAX_LOGS + ' 条'), followBtn),
      list));
    redraw();
    logView = {
      append(entries) {
        if (list.querySelector('.empty')) { redraw(); return; }
        const shown = entries.filter(visible);
        list.append(...shown.map(line));
        while (list.childElementCount > MAX_LOGS) list.firstElementChild.remove();
        if (state.follow) list.scrollTop = list.scrollHeight;
      },
    };
    return { update() {} };
  }

  // ---------- 操作 ----------

  function renderActions() {
    const d = state.details;
    const unsupported = !d && state.detailsError === 501;
    const message = h('textarea', { class: 'input textarea', name: 'message', maxlength: 1024, rows: 3, required: true, placeholder: '发给所有在线玩家的系统消息' });
    const limit = textInput('value', d?.maxPlayers ?? '', { type: 'number', min: 1, max: 256 });
    const run = (button, task, success) => async event => {
      event.preventDefault();
      button.disabled = true;
      try { await task(); toast(success(), 'ok'); refresh(); }
      catch (failure) { if (failure.status !== 401) toast(failureText(failure), 'err'); }
      finally { button.disabled = false; }
    };
    const sayBtn = h('button', { type: 'submit', class: 'btn primary' }, icon('megaphone'), '发送广播');
    const limitBtn = h('button', { type: 'submit', class: 'btn' }, '保存');
    const reloadBtn = h('button', { type: 'button', class: 'btn' }, icon('reload'), '重新加载');
    reloadBtn.addEventListener('click', run(reloadBtn, () => api('POST', base + '/api/reload'), () => '配置已重新加载'));
    content.replaceChildren(h('div', { class: 'detail-section' },
      unsupported ? h('div', { class: 'notice' }, '该实例版本较旧，不支持以下操作。') : null,
      h('div', { class: 'action-grid' },
        h('form', { class: 'action-card', onsubmit: run(sayBtn, () => api('POST', base + '/api/say', { message: message.value.trim() }), () => { message.value = ''; return '广播已发送'; }) },
          h('h3', null, '服务器广播'),
          h('p', { class: 'muted' }, '以服务器身份发送聊天消息，同样经过敏感词过滤。'),
          message, h('div', { class: 'form-actions' }, sayBtn)),
        h('form', { class: 'action-card', onsubmit: run(limitBtn, () => api('PUT', base + '/api/max-players', { value: Number(limit.value) }), () => '人数上限已改为 ' + limit.value) },
          h('h3', null, '人数上限'),
          h('p', { class: 'muted' }, '1–256。已在线的玩家不受影响，只限制新连接。'),
          field('最多玩家数', limit), h('div', { class: 'form-actions' }, limitBtn)),
        h('div', { class: 'action-card' },
          h('h3', null, '重新加载配置'),
          h('p', { class: 'muted' }, '重新读取 ', h('code', null, 'server.json'), ' 与 ', h('code', null, 'chat-filter.json'), '（欢迎公告、敏感词、聊天记录开关）。文件有误时保持原配置。'),
          h('div', { class: 'form-actions' }, reloadBtn)))));
    return {
      update() {
        if (state.details && document.activeElement !== limit && !limit.value) limit.value = state.details.maxPlayers;
      },
    };
  }

  renderAll();
  return {
    refresh,
    setTab(next) {
      if (next === current) return;
      current = TABS.some(t => t[0] === next) ? next : 'overview';
      renderTabs();
      renderContent();
    },
    dispose() {},
  };
}

function meta(label, value) { return h('span', { class: 'meta' }, h('span', { class: 'muted' }, label), ' ', h('span', { class: 'mono' }, value)); }

function infoCard(title, rows) {
  return h('div', { class: 'info-card' }, h('h3', null, title),
    h('dl', null, rows.map(([k, v]) => [h('dt', null, k), h('dd', null, v ?? '—')])));
}

function detailsErrorText(status) {
  return {
    501: '该实例版本较旧，只能查看基本状态。实例更新到新版本后可查看玩家、房间和日志。',
    502: '无法访问实例。', 404: '该实例已不在 Gateway 登记中。', 400: '实例返回的身份与登记不一致。', 0: '无法连接 Gateway。',
  }[status] ?? '无法读取实例详情' + (status ? '（HTTP ' + status + '）' : '') + '。';
}

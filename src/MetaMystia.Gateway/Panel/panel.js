'use strict';

// MetaMystia Gateway 管理面板：只调用同源 /admin/ 接口，令牌仅存于浏览器。
(() => {
  const TOKEN_KEY = 'mm-gateway-token';
  const THEME_KEY = 'mm-gateway-theme';
  const REFRESH_MS = 5000;

  // ---------- 存储 ----------

  function storage(kind) {
    try { return kind === 'local' ? window.localStorage : window.sessionStorage; } catch { return null; }
  }
  function read(kind, key) { try { return storage(kind)?.getItem(key) ?? null; } catch { return null; } }
  function write(kind, key, value) {
    try { value == null ? storage(kind)?.removeItem(key) : storage(kind)?.setItem(key, value); } catch { /* 隐私模式 */ }
  }

  // ---------- DOM ----------

  function h(tag, props, ...children) {
    const el = document.createElement(tag);
    for (const [key, value] of Object.entries(props || {})) {
      if (value == null || value === false) continue;
      if (key === 'class') el.className = value;
      else if (key.startsWith('on')) el.addEventListener(key.slice(2), value);
      else if (key in el && typeof value !== 'string') el[key] = value;
      else el.setAttribute(key, value === true ? '' : value);
    }
    append(el, children);
    return el;
  }
  function append(el, children) {
    for (const child of children.flat(Infinity)) {
      if (child == null || child === false) continue;
      el.append(child instanceof Node ? child : document.createTextNode(String(child)));
    }
  }

  const SVG = 'http://www.w3.org/2000/svg';
  const ICONS = {
    refresh: ['M20 11a8 8 0 0 0-14.6-4.5M4 4v3.5h3.5M4 13a8 8 0 0 0 14.6 4.5M20 20v-3.5h-3.5'],
    plus: ['M12 5v14M5 12h14'],
    trash: ['M4 7h16M9 7V4h6v3M6.5 7l1 13h9l1-13'],
    x: ['m6 6 12 12M18 6 6 18'],
    search: ['m20 20-4.2-4.2', 'M11 4.5a6.5 6.5 0 1 0 0 13 6.5 6.5 0 0 0 0-13Z'],
    contrast: ['M12 3.5a8.5 8.5 0 1 0 0 17 8.5 8.5 0 0 0 0-17Z', 'M12 3.5v17a8.5 8.5 0 0 0 0-17Z'],
    logout: ['M14 4h5v16h-5M10 8l-4 4 4 4M6 12h10'],
    sunset: ['M4 18h16M7 14a5 5 0 0 1 10 0M12 4v4M5 8l2 2M19 8l-2 2'],
  };
  function icon(name, extra) {
    const svg = document.createElementNS(SVG, 'svg');
    svg.setAttribute('class', 'i' + (extra ? ' ' + extra : ''));
    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('aria-hidden', 'true');
    ICONS[name].forEach((d, index) => {
      const path = document.createElementNS(SVG, 'path');
      path.setAttribute('d', d);
      if (name === 'contrast' && index === 1) path.setAttribute('fill', 'currentColor');
      svg.append(path);
    });
    return svg;
  }
  function mark(cls) {
    const img = h('img', { class: cls, src: 'favicon.svg', alt: '' });
    return img;
  }
  function iconButton(name, title, onclick, cls) {
    return h('button', { type: 'button', class: 'icon-btn' + (cls ? ' ' + cls : ''), title, 'aria-label': title, onclick }, icon(name));
  }

  // ---------- 主题 ----------

  function applyTheme(theme) {
    if (theme === 'light') document.documentElement.dataset.theme = 'light';
    else delete document.documentElement.dataset.theme;
  }
  function toggleTheme() {
    const next = document.documentElement.dataset.theme === 'light' ? 'dark' : 'light';
    write('local', THEME_KEY, next);
    applyTheme(next);
  }
  applyTheme(read('local', THEME_KEY));

  // ---------- 接口 ----------

  class ApiError extends Error {
    constructor(status) { super('HTTP ' + status); this.status = status; }
  }

  let token = read('session', TOKEN_KEY) ?? read('local', TOKEN_KEY);

  async function api(method, path, body) {
    let response;
    try {
      response = await fetch(path, {
        method, cache: 'no-store', credentials: 'omit',
        headers: body === undefined ? { Authorization: 'Bearer ' + token } : { Authorization: 'Bearer ' + token, 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
      });
    } catch {
      throw new ApiError(0);
    }
    if (response.status === 401) { signOut(true); throw new ApiError(401); }
    if (!response.ok) throw new ApiError(response.status);
    return response.status === 200 ? response.json() : null;
  }

  const id = value => encodeURIComponent(value);

  // ---------- 状态 ----------

  const state = {
    online: 'connecting', // connecting | ok | offline
    connections: null,
    instances: [],
    statuses: new Map(), // id -> { status } | { error }
    updated: null,
    loading: false,
    filter: '',
  };

  async function refresh() {
    if (!token || state.loading) return;
    state.loading = true;
    renderBusy();
    try {
      const [gateway, instances] = await Promise.all([api('GET', 'status'), api('GET', 'instances')]);
      const statuses = new Map(await Promise.all(instances.map(async record => {
        try { return [record.id, { status: await api('GET', 'instances/' + id(record.id) + '/status') }]; }
        catch (error) { return [record.id, { error: error.status }]; }
      })));
      Object.assign(state, { online: 'ok', connections: gateway.connections, instances, statuses, updated: Date.now() });
    } catch (error) {
      if (error.status === 401) return;
      state.online = 'offline';
    } finally {
      state.loading = false;
      if (token) render();
    }
  }

  let timer = 0;
  function schedule() {
    clearInterval(timer);
    timer = setInterval(() => { if (!document.hidden) refresh(); else renderClock(); }, REFRESH_MS);
  }
  document.addEventListener('visibilitychange', () => { if (!document.hidden && token) refresh(); });

  // ---------- 登录 ----------

  function signOut(rejected) {
    write('session', TOKEN_KEY, null);
    write('local', TOKEN_KEY, null);
    token = null;
    clearInterval(timer);
    closeModal();
    showLogin(rejected ? 'token 无效或已更换' : null);
  }

  function showLogin(error) {
    const root = document.getElementById('root');
    root.replaceChildren();
    const input = h('input', { class: 'mono', type: 'password', placeholder: 'token', spellcheck: false, autocomplete: 'off' });
    const remember = h('input', { type: 'checkbox' });
    const message = h('p', { class: 'login-error', hidden: !error }, error || '');
    const submit = h('button', { class: 'btn primary', type: 'submit' }, '连接');
    const form = h('form', {
      class: 'login-card', autocomplete: 'off',
      onsubmit: async event => {
        event.preventDefault();
        const value = input.value.trim();
        if (!value) return;
        submit.disabled = true;
        token = value;
        try {
          await api('GET', 'status');
          write(remember.checked ? 'local' : 'session', TOKEN_KEY, value);
          start();
        } catch (failure) {
          if (failure.status === 401) return; // signOut 已重新渲染登录页
          token = null;
          message.hidden = false;
          message.textContent = failure.status === 0 ? '无法连接 Gateway' : 'Gateway 返回 ' + failure.status;
          submit.disabled = false;
        }
      },
    },
      mark('login-mark'),
      h('h1', null, 'MetaMystia Gateway'),
      h('p', { class: 'muted' }, '管理令牌见服务器 ', h('code', null, 'gateway.env'), ' 中的 ', h('code', null, 'MM_GATEWAY_TOKEN'), '。'),
      input,
      h('label', { class: 'check' }, remember, '记住'),
      message,
      submit,
    );
    root.append(h('div', { class: 'overlay' }, form));
    input.focus();
  }

  // ---------- 主界面 ----------

  const ui = {};

  function start() {
    const root = document.getElementById('root');
    ui.status = h('div', { class: 'status' });
    ui.refreshBtn = iconButton('refresh', '刷新', () => refresh());
    ui.stats = h('div', { class: 'stats' });
    ui.count = h('span', { class: 'count' });
    ui.body = h('div');
    const search = h('input', {
      type: 'search', placeholder: '筛选 ID、版本、端口', value: state.filter,
      oninput: event => { state.filter = event.target.value; renderInstances(); },
    });
    root.replaceChildren(h('div', { class: 'app' },
      h('header', { class: 'topbar' },
        h('div', { class: 'brand' }, mark('brand-mark'), h('span', { class: 'brand-name' }, 'MetaMystia Gateway'), h('span', { class: 'brand-sub' }, '实例管理')),
        ui.status,
        h('div', { class: 'top-actions' },
          ui.refreshBtn,
          iconButton('contrast', '切换主题', toggleTheme),
          iconButton('logout', '退出登录', () => signOut(false)),
        ),
      ),
      h('main', { class: 'main scroll' },
        h('div', { class: 'page' },
          ui.stats,
          h('section', { class: 'panel' },
            h('div', { class: 'panel-head' },
              h('span', { class: 'panel-title' }, '实例', ui.count),
              h('span', { class: 'spacer' }),
              h('label', { class: 'search' }, icon('search'), search),
              h('button', { type: 'button', class: 'btn primary', onclick: () => openRegister() }, icon('plus'), '登记实例'),
            ),
            ui.body,
          ),
        ),
      ),
      h('div', { class: 'toasts', id: 'toasts' }),
    ));
    render();
    refresh();
    schedule();
  }

  function render() {
    if (!ui.status) return;
    renderStatus();
    renderStats();
    renderInstances();
    renderBusy();
  }

  function ago() {
    if (!state.updated) return '';
    const seconds = Math.max(0, Math.round((Date.now() - state.updated) / 1000));
    return seconds < 5 ? '刚刚更新' : seconds + ' 秒前更新';
  }

  function renderStatus() {
    const online = { connecting: ['', '连接中'], ok: ['ok', '已连接'], offline: ['bad', '无法访问 Gateway'] }[state.online];
    ui.status.replaceChildren(
      h('span', { class: 'chip ' + online[0], title: 'Gateway 管理接口' }, h('i', { class: 'dot' }), online[1]),
      h('span', { class: 'chip', id: 'st-refresh', title: '每 5 秒自动刷新' }, ago() || '—'),
    );
  }
  function renderClock() {
    const el = document.getElementById('st-refresh');
    if (el) el.textContent = ago() || '—';
  }
  function renderBusy() {
    if (!ui.refreshBtn) return;
    ui.refreshBtn.disabled = state.loading;
    ui.refreshBtn.firstChild.classList.toggle('spinning', state.loading);
  }

  function renderStats() {
    const records = state.instances;
    const active = records.filter(r => r.state === 'active').length;
    let players = 0, reachable = 0;
    for (const record of records) {
      const entry = state.statuses.get(record.id);
      if (entry?.status) { players += entry.status.players; reachable++; }
    }
    const failing = records.length - reachable;
    const stat = (label, value, note, cls) => h('div', { class: 'stat' },
      h('div', { class: 'stat-label' }, label),
      h('div', { class: 'stat-value' + (cls ? ' ' + cls : '') }, value),
      h('div', { class: 'stat-note' }, note));
    const known = state.updated != null;
    ui.stats.replaceChildren(
      stat('转发连接', state.connections ?? '—', '经 Gateway 的 TCP 连接'),
      stat('在线玩家', known ? players : '—', '可访问实例的人数合计'),
      stat('接客实例', known ? active : '—', known ? '共登记 ' + records.length + ' 个' : ''),
      stat('不可访问', known ? failing : '—', '状态查询失败的实例', known && failing > 0 ? 'err' : null),
    );
  }

  function compareVersion(a, b) { return a.localeCompare(b, undefined, { numeric: true }); }

  function sorted() {
    const rank = r => (r.state === 'active' ? 0 : 1);
    return [...state.instances].sort((a, b) =>
      compareVersion(a.registration.gameVersion, b.registration.gameVersion) ||
      rank(a) - rank(b) ||
      compareVersion(b.registration.modVersion, a.registration.modVersion) ||
      a.id.localeCompare(b.id));
  }

  function matches(record, filter) {
    if (!filter) return true;
    const r = record.registration;
    return [record.id, r.gameVersion, r.modVersion, r.host + ':' + r.port, r.adminUrl, record.targetModVersion ?? '']
      .some(text => text.toLowerCase().includes(filter));
  }

  function routePill(record) {
    return record.state === 'active'
      ? h('span', { class: 'pill ok' }, '接客')
      : h('span', { class: 'pill warn' }, '退役中');
  }

  function instancePill(entry) {
    if (!entry) return h('span', { class: 'pill idle' }, '查询中');
    if (entry.error != null) {
      const text = { 0: '网络错误', 400: '身份不符', 404: '登记已删除', 502: '不可访问' }[entry.error] ?? 'HTTP ' + entry.error;
      return h('span', { class: 'pill bad', title: entry.error === 400 ? '实例返回的 ID、版本或端口与登记不一致' : null }, text);
    }
    return entry.status.state === 'running'
      ? h('span', { class: 'pill ok' }, '运行中')
      : h('span', { class: 'pill warn' }, entry.status.state === 'draining' ? '退役中' : entry.status.state);
  }

  function cell(label, cls, ...children) {
    return h('td', { class: cls }, h('span', { class: 'cell-label' }, label), children);
  }

  function renderInstances() {
    const filter = state.filter.trim().toLowerCase();
    const records = sorted().filter(r => matches(r, filter));
    ui.count.textContent = state.updated == null ? '' : filter ? records.length + ' / ' + state.instances.length : String(state.instances.length);
    if (state.updated == null) {
      ui.body.replaceChildren(h('div', { class: 'empty' }, state.online === 'offline' ? '无法访问 Gateway' : '加载中…'));
      return;
    }
    if (state.instances.length === 0) {
      ui.body.replaceChildren(h('div', { class: 'empty' },
        h('p', null, '没有登记的实例。新实例通常由发布流程自动登记。'),
        h('button', { type: 'button', class: 'btn', onclick: () => openRegister() }, icon('plus'), '手动登记')));
      return;
    }
    if (records.length === 0) {
      ui.body.replaceChildren(h('div', { class: 'empty' }, '没有匹配的实例'));
      return;
    }
    const rows = records.map(record => {
      const r = record.registration;
      const entry = state.statuses.get(record.id);
      const status = entry?.status;
      const notice = status?.noticeId ? '通知 ' + status.noticeId : null;
      const target = record.targetModVersion ?? status?.targetModVersion;
      return h('tr', { 'data-id': record.id },
        cell('ID', 'wide', h('span', { class: 'id-cell' }, record.id), h('span', { class: 'sub' }, r.adminUrl)),
        cell('版本', null, h('span', { class: 'mono' }, r.modVersion), h('span', { class: 'sub' }, r.gameVersion)),
        cell('路由', null, routePill(record), target ? h('span', { class: 'sub' }, '→ ' + target) : null),
        cell('实例', null, instancePill(entry), notice ? h('span', { class: 'sub' }, notice) : null),
        cell('玩家', 'num', status ? status.players : '—'),
        cell('后端', null, h('span', { class: 'mono' }, r.host + ':' + r.port)),
        h('td', { class: 'actions' }, h('span', { class: 'row-actions' },
          record.state === 'active'
            ? h('button', { type: 'button', class: 'btn small ghost', onclick: () => openDrain(record) }, icon('sunset'), '退役')
            : h('button', { type: 'button', class: 'btn small ghost', title: '使用相同正文重试退役请求', onclick: () => openDrain(record) }, icon('sunset'), '重发退役'),
          iconButton('trash', '删除登记', () => openDelete(record), 'danger'),
        )),
      );
    });
    ui.body.replaceChildren(h('table', { class: 'table' },
      h('thead', null, h('tr', null,
        h('th', null, 'ID'), h('th', null, '模组 / 游戏版本'), h('th', null, '路由'), h('th', null, '实例'),
        h('th', { class: 'num' }, '玩家'), h('th', null, '后端'), h('th', null, ''))),
      h('tbody', null, rows)));
  }

  // ---------- 提示与对话框 ----------

  function toast(text, kind) {
    const host = document.getElementById('toasts');
    if (!host) return;
    const el = h('div', { class: 'toast ' + (kind || '') }, text);
    host.append(el);
    setTimeout(() => el.remove(), kind === 'err' ? 6000 : 3000);
  }

  let modalEl = null;
  function closeModal() { modalEl?.remove(); modalEl = null; }
  function openModal(title, ...body) {
    closeModal();
    modalEl = h('div', {
      class: 'overlay',
      onmousedown: event => { if (event.target === modalEl) closeModal(); },
    }, h('div', { class: 'modal', role: 'dialog', 'aria-label': title },
      h('div', { class: 'modal-head' }, title, iconButton('x', '关闭', closeModal)),
      h('div', { class: 'modal-body scroll' }, body)));
    document.body.append(modalEl);
    modalEl.querySelector('input:not([type=hidden]), select, button.btn')?.focus();
  }
  document.addEventListener('keydown', event => { if (event.key === 'Escape' && modalEl) closeModal(); });

  function field(label, input, hint) {
    return h('label', { class: 'field' }, label, input, hint ? h('span', { class: 'hint' }, hint) : null);
  }
  function textInput(name, value, props) {
    return h('input', { class: 'input mono', name, value: value ?? '', spellcheck: false, autocomplete: 'off', required: true, ...props });
  }

  // 提交表单：成功后关闭对话框并刷新；失败显示在表单内。
  function actionForm(children, submitLabel, submitClass, run, messages) {
    const error = h('p', { class: 'form-error', hidden: true });
    const submit = h('button', { type: 'submit', class: 'btn ' + submitClass }, submitLabel);
    const form = h('form', {
      class: 'action-form',
      onsubmit: async event => {
        event.preventDefault();
        submit.disabled = true;
        error.hidden = true;
        try {
          const done = await run(new FormData(form));
          closeModal();
          toast(done, 'ok');
          refresh();
        } catch (failure) {
          if (failure.status === 401) return;
          error.textContent = messages[failure.status] ?? (failure.status === 0 ? '无法连接 Gateway' : failure.message || 'HTTP ' + failure.status);
          error.hidden = false;
          submit.disabled = false;
        }
      },
    }, children, error, h('div', { class: 'form-actions' },
      h('button', { type: 'button', class: 'btn ghost', onclick: closeModal }, '取消'), submit));
    return form;
  }

  function nextPort(first, last, used) {
    for (let port = first; port <= last; port++) if (!used.has(port)) return port;
    return '';
  }

  function openRegister() {
    const records = state.instances;
    const tcp = new Set(records.map(r => r.registration.port));
    const admin = new Set(records.map(r => Number(new URL(r.registration.adminUrl).port)));
    const game = sorted().find(r => r.state === 'active')?.registration.gameVersion ?? '';
    const port = nextPort(41000, 41099, tcp);
    const adminPort = nextPort(42000, 42099, admin);
    openModal('登记实例',
      h('p', null, 'Gateway 会先查询实例的 ', h('code', null, '/admin/status'), '，ID、版本和端口一致且处于运行中才会登记并开始接客。'),
      actionForm([
        field('实例 ID', textInput('id', '', { pattern: '[A-Za-z0-9][A-Za-z0-9._\\-]{0,127}', placeholder: '与实例 MM_INSTANCE_ID 一致' })),
        h('div', { class: 'field-row' },
          field('游戏版本', textInput('gameVersion', game, { maxlength: 64 })),
          field('模组版本', textInput('modVersion', '', { maxlength: 64, placeholder: '0.30.1' }))),
        h('div', { class: 'field-row' },
          field('TCP 主机', textInput('host', '127.0.0.1')),
          field('TCP 端口', textInput('port', port, { type: 'number', min: 1, max: 65535 }))),
        field('管理地址', textInput('adminUrl', adminPort ? 'http://127.0.0.1:' + adminPort : '', { type: 'url' }), '主机与管理地址只允许回环 IP'),
      ], '登记', 'primary', async data => {
        const instance = String(data.get('id')).trim();
        await api('PUT', 'instances/' + id(instance), {
          gameVersion: String(data.get('gameVersion')).trim(),
          modVersion: String(data.get('modVersion')).trim(),
          host: String(data.get('host')).trim(),
          port: Number(data.get('port')),
          adminUrl: String(data.get('adminUrl')).trim(),
        });
        return '已登记 ' + instance;
      }, {
        400: '参数无效，或实例返回的 ID、版本、端口与填写不一致',
        409: 'ID 已被占用、地址或版本与现有登记冲突，或实例不在运行',
        502: '无法访问实例管理地址',
      }));
  }

  function openDrain(record) {
    const r = record.registration;
    // 只把同游戏版本、模组版本更高的接客实例当作接替者。
    const successor = sorted().find(other => other.id !== record.id && other.state === 'active' &&
      other.registration.gameVersion === r.gameVersion && compareVersion(other.registration.modVersion, r.modVersion) > 0);
    const status = state.statuses.get(record.id)?.status;
    const retry = record.state !== 'active';
    openModal(retry ? '重发退役请求' : '退役 ' + record.id,
      h('p', null, retry
        ? '路由已是退役中。使用相同正文可安全重试转发给实例的退役请求。'
        : ['Gateway 先把路由标记为退役中并保存，再通知实例。新玩家连接 ', h('code', null, r.modVersion), ' 时会被提示升级到目标版本；已有玩家收到通知，全部离开后实例自行退出。']),
      !retry && status?.players ? h('div', { class: 'notice' }, '当前有 ' + status.players + ' 名玩家在线，不会被强制下线。') : null,
      !retry && !successor ? h('div', { class: 'notice bad' }, '没有同游戏版本的更新接客实例，请确认目标版本已上线。') : null,
      actionForm([
        h('div', { class: 'field-row' },
          field('目标模组版本', textInput('targetModVersion', record.targetModVersion ?? status?.targetModVersion ?? successor?.registration.modVersion ?? '', { maxlength: 64 })),
          field('通知 ID', textInput('noticeId', status?.noticeId ?? successor?.id ?? '', { maxlength: 128 }), '发布流程使用新实例 ID')),
      ], retry ? '重发' : '退役', retry ? 'primary' : 'danger', async data => {
        await api('POST', 'instances/' + id(record.id) + '/drain', {
          noticeId: String(data.get('noticeId')).trim(),
          targetModVersion: String(data.get('targetModVersion')).trim(),
        });
        return record.id + ' 已收到退役请求';
      }, {
        400: '参数无效',
        404: '登记不存在',
        502: '路由已标记为退役中，但转发给实例失败；可用相同内容重试',
      }));
  }

  function openDelete(record) {
    const status = state.statuses.get(record.id)?.status;
    openModal('删除登记 ' + record.id,
      h('p', null, '只删除 Gateway 中的路由记录，不关闭已有连接，也不停止实例进程。登记删除后同一 ID 可重新登记。'),
      record.state === 'active' ? h('div', { class: 'notice bad' }, '该实例仍在接客，删除后 ' + record.registration.modVersion + ' 的新连接会被拒绝。') : null,
      status && status.players > 0 ? h('div', { class: 'notice' }, '实例仍有 ' + status.players + ' 名玩家在线。') : null,
      actionForm([], '删除', 'danger', async () => {
        await api('DELETE', 'instances/' + id(record.id));
        return '已删除 ' + record.id;
      }, { 404: '登记不存在' }));
  }

  // ---------- 启动 ----------

  if (token) start(); else showLogin(null);
})();

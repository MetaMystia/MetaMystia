// 入口：登录、顶栏、路由（#/、#/instance/<id>/<tab>、#/gateway）与定时刷新。
import { h, iconButton, api, session, read, write, closeModal, mark } from './core.js';
import { mountOverview } from './overview.js';
import { mountInstance } from './instance.js';
import { mountGateway } from './gateway.js';

const TOKEN_KEY = 'mm-gateway-token';
const THEME_KEY = 'mm-gateway-theme';
const REFRESH_MS = 5000;

function applyTheme(theme) {
  if (theme === 'light') document.documentElement.dataset.theme = 'light';
  else delete document.documentElement.dataset.theme;
}
applyTheme(read('local', THEME_KEY));

// ---------- 登录 ----------

function signOut(rejected) {
  write('session', TOKEN_KEY, null);
  write('local', TOKEN_KEY, null);
  session.token = null;
  stop();
  closeModal();
  showLogin(rejected ? 'token 无效或已更换' : null);
}
session.onUnauthorized = () => signOut(true);

function showLogin(error) {
  const input = h('input', { class: 'mono', type: 'password', placeholder: 'token', spellcheck: false, autocomplete: 'off' });
  const remember = h('input', { type: 'checkbox' });
  const message = h('p', { class: 'login-error', hidden: !error }, error || '');
  const submit = h('button', { class: 'btn primary', type: 'submit' }, '连接');
  document.getElementById('root').replaceChildren(h('div', { class: 'overlay' }, h('form', {
    class: 'login-card', autocomplete: 'off',
    onsubmit: async event => {
      event.preventDefault();
      const value = input.value.trim();
      if (!value) return;
      submit.disabled = true;
      session.token = value;
      try {
        await api('GET', 'status');
        write(remember.checked ? 'local' : 'session', TOKEN_KEY, value);
        start();
      } catch (failure) {
        if (failure.status === 401) return;
        session.token = null;
        message.hidden = false;
        message.textContent = failure.status === 0 ? '无法连接 Gateway' : 'Gateway 返回 ' + failure.status;
        submit.disabled = false;
      }
    },
  },
    mark('login-mark'),
    h('h1', null, 'MetaMystia Gateway'),
    h('p', { class: 'muted' }, '管理令牌见服务器 ', h('code', null, 'gateway.env'), ' 中的 ', h('code', null, 'MM_GATEWAY_TOKEN'), '。'),
    input, h('label', { class: 'check' }, remember, '记住'), message, submit)));
  input.focus();
}

// ---------- 外壳 ----------

const shell = {};
let view = null, route = '', timer = 0, busy = false, again = false, updated = 0;

const app = {
  online(ok) {
    if (ok) updated = Date.now();
    shell.online?.replaceChildren(h('i', { class: 'dot' }), ok ? '已连接' : '无法访问 Gateway');
    if (shell.online) shell.online.className = 'chip ' + (ok ? 'ok' : 'bad');
  },
};

function start() {
  shell.online = h('span', { class: 'chip', title: 'Gateway 管理接口' }, h('i', { class: 'dot' }), '连接中');
  shell.clock = h('span', { class: 'chip', id: 'st-refresh', title: '每 5 秒自动刷新' }, '—');
  shell.refresh = iconButton('refresh', '刷新', () => tick());
  shell.nav = h('nav', { class: 'top-nav' });
  shell.main = h('main', { class: 'main scroll' });
  document.getElementById('root').replaceChildren(h('div', { class: 'app' },
    h('header', { class: 'topbar' },
      h('a', { class: 'brand', href: '#/' }, mark('brand-mark'), h('span', { class: 'brand-name' }, 'MetaMystia Gateway')),
      shell.nav,
      h('div', { class: 'status' }, shell.online, shell.clock),
      h('div', { class: 'top-actions' },
        shell.refresh,
        iconButton('contrast', '切换主题', () => {
          const next = document.documentElement.dataset.theme === 'light' ? 'dark' : 'light';
          write('local', THEME_KEY, next);
          applyTheme(next);
        }),
        iconButton('logout', '退出登录', () => signOut(false)))),
    shell.main));
  route = '';
  navigate();
  clearInterval(timer);
  timer = setInterval(() => { if (!document.hidden) tick(); else clock(); }, REFRESH_MS);
}

function stop() { clearInterval(timer); view?.dispose(); view = null; route = ''; }

function clock() {
  if (!shell.clock) return;
  const seconds = updated ? Math.round((Date.now() - updated) / 1000) : null;
  shell.clock.textContent = seconds == null ? '—' : seconds < 5 ? '刚刚更新' : seconds + ' 秒前更新';
}

async function tick() {
  if (!view || !session.token) return;
  if (busy) { again = true; return; }
  busy = true;
  shell.refresh.disabled = true;
  shell.refresh.firstChild.classList.add('spinning');
  try { await view.refresh(); }
  finally {
    busy = false;
    shell.refresh.disabled = false;
    shell.refresh.firstChild.classList.remove('spinning');
    clock();
  }
  if (again) { again = false; tick(); }
}

function navigate() {
  if (!session.token || !shell.main) return;
  const parts = location.hash.replace(/^#\/?/, '').split('/').map(decodeURIComponent);
  const page = parts[0] === 'instance' && parts[1] ? 'instance' : parts[0] === 'gateway' ? 'gateway' : 'overview';
  shell.nav.replaceChildren(
    h('a', { class: 'top-tab' + (page !== 'gateway' ? ' active' : ''), href: '#/' }, '实例'),
    h('a', { class: 'top-tab' + (page === 'gateway' ? ' active' : ''), href: '#/gateway' }, '连接'));
  const key = page === 'instance' ? 'instance/' + parts[1] : page;
  if (key === route && page === 'instance') { view.setTab(parts[2] || 'overview'); return; }
  if (key === route) return;
  view?.dispose();
  route = key;
  closeModal();
  shell.main.scrollTop = 0;
  view = page === 'instance' ? mountInstance(shell.main, app, parts[1], parts[2])
    : page === 'gateway' ? mountGateway(shell.main, app) : mountOverview(shell.main, app);
  document.title = (page === 'instance' ? parts[1] + ' · ' : page === 'gateway' ? '连接 · ' : '') + 'MetaMystia Gateway';
  tick();
}
window.addEventListener('hashchange', navigate);
document.addEventListener('visibilitychange', () => { if (!document.hidden) tick(); });

if (session.token) start(); else showLogin(null);

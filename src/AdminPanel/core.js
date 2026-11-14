// 公共工具：存储、DOM、接口、提示、对话框、格式化与中文名称。

export const API = '/admin/';

// ---------- 存储 ----------

function storage(kind) {
  try { return kind === 'local' ? window.localStorage : window.sessionStorage; } catch { return null; }
}
export function read(kind, key) { try { return storage(kind)?.getItem(key) ?? null; } catch { return null; } }
export function write(kind, key, value) {
  try { value == null ? storage(kind)?.removeItem(key) : storage(kind)?.setItem(key, value); } catch { /* 隐私模式 */ }
}

// ---------- DOM ----------

export function h(tag, props, ...children) {
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
export function append(el, children) {
  for (const child of [children].flat(Infinity)) {
    if (child == null || child === false) continue;
    el.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return el;
}

const SVG = 'http://www.w3.org/2000/svg';
export function svg(tag, attrs, ...children) {
  const el = document.createElementNS(SVG, tag);
  for (const [key, value] of Object.entries(attrs || {})) if (value != null) el.setAttribute(key, value);
  for (const child of children.flat(Infinity)) if (child) el.append(child);
  return el;
}

const ICONS = {
  refresh: ['M20 11a8 8 0 0 0-14.6-4.5M4 4v3.5h3.5M4 13a8 8 0 0 0 14.6 4.5M20 20v-3.5h-3.5'],
  plus: ['M12 5v14M5 12h14'],
  trash: ['M4 7h16M9 7V4h6v3M6.5 7l1 13h9l1-13'],
  x: ['m6 6 12 12M18 6 6 18'],
  search: ['m20 20-4.2-4.2', 'M11 4.5a6.5 6.5 0 1 0 0 13 6.5 6.5 0 0 0 0-13Z'],
  contrast: ['M12 3.5a8.5 8.5 0 1 0 0 17 8.5 8.5 0 0 0 0-17Z', '*M12 3.5v17a8.5 8.5 0 0 0 0-17Z'],
  logout: ['M14 4h5v16h-5M10 8l-4 4 4 4M6 12h10'],
  sunset: ['M4 18h16M7 14a5 5 0 0 1 10 0M12 4v4M5 8l2 2M19 8l-2 2'],
  left: ['m15 5-7 7 7 7'],
  chev: ['m9 6 6 6-6 6'],
  user: ['M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8ZM4.5 20a7.5 7.5 0 0 1 15 0'],
  door: ['M6 20V4h12v16M4 20h16M14 12h.01'],
  boot: ['M14 4h5v16h-5M10 8l-4 4 4 4M6 12h10'],
  megaphone: ['M4 10v4h3l7 4V6l-7 4H4ZM17 9a4 4 0 0 1 0 6'],
  sliders: ['M4 7h10M18 7h2M4 17h4M12 17h8M16 5v4M10 15v4'],
  reload: ['M3.5 12a8.5 8.5 0 1 0 2.6-6.1M3.5 4v4.5H8'],
  pause: ['M8 5v14M16 5v14'],
  play: ['*M7 4.5v15l12-7.5-12-7.5Z'],
  down: ['M12 4v14M6 12l6 6 6-6'],
};
export function icon(name, extra) {
  const el = svg('svg', { class: 'i' + (extra ? ' ' + extra : ''), viewBox: '0 0 24 24', 'aria-hidden': 'true' });
  for (const d of ICONS[name]) {
    const filled = d.startsWith('*');
    el.append(svg('path', { d: filled ? d.slice(1) : d, fill: filled ? 'currentColor' : null, stroke: filled ? 'none' : null }));
  }
  return el;
}
export function iconButton(name, title, onclick, cls) {
  return h('button', { type: 'button', class: 'icon-btn' + (cls ? ' ' + cls : ''), title, 'aria-label': title, onclick }, icon(name));
}
export function mark(cls) { return h('img', { class: cls, src: 'favicon.svg', alt: '' }); }

// ---------- 接口 ----------

export class ApiError extends Error {
  constructor(status, body) { super('HTTP ' + status); this.status = status; this.body = body; }
}

export const session = {
  token: read('session', 'mm-gateway-token') ?? read('local', 'mm-gateway-token'),
  onUnauthorized: () => {},
};

export async function api(method, path, body) {
  let response;
  try {
    response = await fetch(API + path, {
      method, cache: 'no-store', credentials: 'omit',
      headers: body === undefined ? { Authorization: 'Bearer ' + session.token } : { Authorization: 'Bearer ' + session.token, 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(0);
  }
  if (response.status === 401) { session.onUnauthorized(); throw new ApiError(401); }
  const text = await response.text();
  let data = null;
  if (text) { try { data = JSON.parse(text); } catch { data = null; } }
  if (!response.ok) throw new ApiError(response.status, data);
  return data;
}

export const enc = encodeURIComponent;

// ---------- 提示与对话框 ----------

export function toast(text, kind) {
  let host = document.getElementById('toasts');
  if (!host) { host = h('div', { class: 'toasts', id: 'toasts' }); document.body.append(host); }
  const el = h('div', { class: 'toast ' + (kind || '') }, text);
  host.append(el);
  setTimeout(() => el.remove(), kind === 'err' ? 6000 : 3000);
}

let modalEl = null;
export function closeModal() { modalEl?.remove(); modalEl = null; }
export function openModal(title, ...body) {
  closeModal();
  modalEl = h('div', { class: 'overlay', onmousedown: event => { if (event.target === modalEl) closeModal(); } },
    h('div', { class: 'modal', role: 'dialog', 'aria-label': title },
      h('div', { class: 'modal-head' }, title, iconButton('x', '关闭', closeModal)),
      h('div', { class: 'modal-body scroll' }, body)));
  document.body.append(modalEl);
  modalEl.querySelector('input:not([type=hidden]):not([type=checkbox]), select, textarea, button.btn')?.focus();
}
document.addEventListener('keydown', event => { if (event.key === 'Escape' && modalEl) closeModal(); });

export function field(label, input, hint) {
  return h('label', { class: 'field' }, label, input, hint ? h('span', { class: 'hint' }, hint) : null);
}
export function textInput(name, value, props) {
  return h('input', { class: 'input mono', name, value: value ?? '', spellcheck: false, autocomplete: 'off', required: true, ...props });
}

/** 对话框里的表单：成功后关闭并提示，失败把原因显示在表单里。 */
export function actionForm(children, submitLabel, submitClass, run, messages, done) {
  const error = h('p', { class: 'form-error', hidden: true });
  const submit = h('button', { type: 'submit', class: 'btn ' + submitClass }, submitLabel);
  const form = h('form', {
    class: 'action-form',
    onsubmit: async event => {
      event.preventDefault();
      submit.disabled = true;
      error.hidden = true;
      try {
        const message = await run(new FormData(form));
        closeModal();
        toast(message, 'ok');
        done?.();
      } catch (failure) {
        if (failure.status === 401) return;
        error.textContent = failureText(failure, messages);
        error.hidden = false;
        submit.disabled = false;
      }
    },
  }, children, error, h('div', { class: 'form-actions' },
    h('button', { type: 'button', class: 'btn ghost', onclick: closeModal }, '取消'), submit));
  return form;
}

const COMMON_ERRORS = {
  0: '无法连接 Gateway',
  413: '内容过长',
  501: '该实例版本较旧，不支持此功能',
  502: '无法访问实例',
  503: '实例正在关闭',
};
export function failureText(failure, messages) {
  const code = failure.body?.error;
  if (code && ERROR_NAMES[code]) return ERROR_NAMES[code] + (failure.body.message ? '：' + failure.body.message : '');
  return messages?.[failure.status] ?? COMMON_ERRORS[failure.status] ?? failure.message ?? '操作失败';
}

// ---------- 格式化 ----------

export function fmtBytes(value) {
  if (value == null) return '—';
  const units = ['B', 'KB', 'MB', 'GB'];
  let n = value, i = 0;
  while (n >= 1024 && i < units.length - 1) { n /= 1024; i++; }
  return (i === 0 ? n : n.toFixed(n >= 100 ? 0 : 1)) + ' ' + units[i];
}
export function fmtDuration(seconds) {
  if (seconds == null || !isFinite(seconds)) return '—';
  seconds = Math.max(0, Math.floor(seconds));
  const d = Math.floor(seconds / 86400), hr = Math.floor(seconds % 86400 / 3600), m = Math.floor(seconds % 3600 / 60), s = seconds % 60;
  if (d) return d + ' 天 ' + hr + ' 小时';
  if (hr) return hr + ' 小时 ' + m + ' 分';
  if (m) return m + ' 分 ' + s + ' 秒';
  return s + ' 秒';
}
export function since(time) { return time ? fmtDuration((Date.now() - new Date(time).getTime()) / 1000) : '—'; }
const pad = n => String(n).padStart(2, '0');
export function fmtTime(time) {
  const d = new Date(time);
  return pad(d.getHours()) + ':' + pad(d.getMinutes()) + ':' + pad(d.getSeconds());
}
export function fmtDateTime(time) {
  const d = new Date(time);
  return (d.getMonth() + 1) + '-' + pad(d.getDate()) + ' ' + fmtTime(d);
}
export function compareVersion(a, b) { return String(a).localeCompare(String(b), undefined, { numeric: true }); }

// ---------- 中文名称 ----------

export const STAGE_NAMES = {
  Loading: '加载中', MainMenu: '主菜单', Day: '白天', DayEnd: '白天结束', Preparation: '营业准备',
  Work: '营业中', Result: '结算', Unavailable: '不可用',
};
export const MAP_NAMES = {
  Unknown: '未知地图', Home: '夜雀小屋', Basement: '地下室', BeastForest: '妖怪兽道', HumanVillage: '人间之里',
  HakureiShrine: '博丽神社', ScarletMansion: '红魔馆', BambooForest: '迷途竹林', PartyStage: '舞台', Hakugyokurou: '白玉楼',
  DLC1_MagicForest: '魔法森林', DLC1_YoukaiMountain: '妖怪之山', DLC2_FormerHell: '旧地狱', DLC2_EarthSpiritsPalace: '地灵殿',
  DLC3_MyourenTemple: '命莲寺', DLC3_DivineSpiritMausoleum: '神灵庙', DLC3_HakureiFestival: '博丽大祭',
  DLC4_GardenOfTheSun: '太阳花田', DLC4_ShiningNeedleCastle: '辉针城', DLC4_ScarletMansionBasement: '红魔馆地下室',
  DLC5_Makai: '魔界', DLC5_LunarCapital: '月之都',
};
export const ERROR_NAMES = {
  VersionUnavailable: '没有该版本的实例', ServerRetiring: '实例退役中', InvalidHello: '握手无效', ConnectFailed: '连接实例失败',
  PlayerMissing: '玩家不在线', NotInRoom: '玩家不在房间中', InvalidLimit: '人数上限须为 1–256', InvalidChat: '内容为空、过长或含过滤词',
  ChatFiltered: '内容含过滤词', ServerStopped: '实例正在关闭', ConfigInvalid: '配置文件无效',
};
export const LEVEL_NAMES = { info: '信息', warning: '警告', error: '错误', chat: '聊天' };
export function named(map, key) { return map[key] ?? key; }
export function dlcText(flags) {
  if (!flags || flags === 'None') return '—';
  if (flags === 'All') return '全部 DLC';
  return flags.split(', ').map(f => ({ Core: '本体', DlcMusic: '音乐' }[f] ?? f.replace('Dlc', 'DLC'))).join(' · ');
}

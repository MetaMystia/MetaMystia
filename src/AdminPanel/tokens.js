// 只读令牌：签发、列表与吊销。
import {
  h, icon, iconButton, api, enc, openModal, closeModal, field, textInput, actionForm, confirmRisk, fmtDateTime, toast,
} from './core.js';
import { cell } from './overview.js';

export function mountTokens(root, app) {
  const state = { tokens: null, failed: false, unsupported: false };
  const count = h('span', { class: 'count' });
  const body = h('div');
  root.replaceChildren(h('div', { class: 'page' },
    h('section', { class: 'panel' },
      h('div', { class: 'panel-head' },
        h('span', { class: 'panel-title' }, '只读令牌', count),
        h('span', { class: 'spacer' }),
        h('button', { type: 'button', class: 'btn primary', onclick: () => openCreate(refresh) }, icon('plus'), '签发令牌')),
      h('p', { class: 'panel-note muted' }, '只读令牌只能访问 ', h('code', null, '/api/v1/'), ' 下的状态与事件接口，不能登录面板。'),
      body)));

  async function refresh() {
    try {
      Object.assign(state, { tokens: await api('GET', 'tokens'), failed: false, unsupported: false });
      app.online(true);
    } catch (error) {
      if (error.status === 401) return;
      if (error.status === 404) state.unsupported = true;
      else { state.failed = true; app.online(false); }
    }
    render();
  }

  function render() {
    if (state.unsupported) {
      body.replaceChildren(h('div', { class: 'notice' }, '当前 Gateway 版本较旧，不支持只读令牌。重新部署 Gateway 后可用。'));
      return;
    }
    if (!state.tokens) { body.replaceChildren(h('div', { class: 'empty' }, state.failed ? '无法访问 Gateway' : '加载中…')); return; }
    count.textContent = String(state.tokens.length);
    if (state.tokens.length === 0) { body.replaceChildren(h('div', { class: 'empty' }, '还没有签发令牌')); return; }
    body.replaceChildren(h('table', { class: 'table' },
      h('thead', null, h('tr', null, ['名称', 'ID', '创建时间', '最近使用', ''].map(t => h('th', null, t)))),
      h('tbody', null, state.tokens.map(token => h('tr', null,
        cell('名称', 'wide', token.name),
        cell('ID', 'mono', token.id),
        cell('创建时间', null, fmtDateTime(token.createdAt)),
        cell('最近使用', null, token.lastUsedAt ? fmtDateTime(token.lastUsedAt) : h('span', { class: 'muted', title: 'Gateway 重启后清空' }, '—')),
        h('td', { class: 'actions' }, iconButton('trash', '吊销', () => openRevoke(token, refresh), 'danger')))))));
  }

  render();
  return { refresh, dispose() {} };
}

function openCreate(done) {
  let created = null;
  openModal('签发只读令牌',
    h('p', null, '明文只在签发后显示一次，Gateway 只保存其哈希。'),
    actionForm([field('名称', textInput('name', '', { maxlength: 64, placeholder: '用途，例如 qq-bot' }))], '签发', 'primary', async data => {
      created = await api('POST', 'tokens', { name: String(data.get('name')).trim() });
      return '已签发 ' + created.name;
    }, { 400: '名称无效', 409: '令牌数量已达上限' }, () => { done(); showToken(created); }));
}

function showToken(created) {
  const input = h('input', { class: 'input mono', readonly: true, value: created.token, onfocus: event => event.target.select() });
  openModal('令牌 ' + created.name,
    h('div', { class: 'notice' }, '关闭后无法再次查看，请立即保存。'),
    field('令牌', input),
    h('div', { class: 'form-actions' },
      h('button', {
        type: 'button', class: 'btn',
        onclick: async () => {
          try { await navigator.clipboard.writeText(created.token); toast('已复制', 'ok'); } catch { input.select(); }
        },
      }, '复制'),
      h('button', { type: 'button', class: 'btn primary', onclick: closeModal }, '完成')));
}

function openRevoke(token, done) {
  openModal('吊销 ' + token.name,
    h('p', null, '吊销后使用该令牌的请求立即返回 401。'),
    actionForm([confirmRisk('我确认吊销 ' + token.name + '（' + token.id + '）。')], '吊销', 'danger', async () => {
      await api('DELETE', 'tokens/' + enc(token.id));
      return '已吊销 ' + token.name;
    }, { 404: '令牌不存在' }, done));
}

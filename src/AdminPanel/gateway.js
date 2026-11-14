// Gateway：当前状态、拒绝原因、连接记录（含客户端 IP）与 Gateway 日志。
import {
  h, icon, api, fmtBytes, fmtDuration, fmtTime, fmtDateTime, since, named, ERROR_NAMES, LEVEL_NAMES,
} from './core.js';
import { lineChart, series } from './chart.js';
import { cell } from './overview.js';

const STATES = { forwarding: ['ok', '转发中'], closed: ['idle', '已结束'], rejected: ['bad', '已拒绝'] };

export function mountGateway(root, app) {
  const state = { info: null, connections: null, history: null, logs: [], logNext: null, filter: '', show: 'all', unsupported: false };
  const stats = h('div', { class: 'stats' });
  const reasons = h('div', { class: 'reason-list' });
  const charts = h('div', { class: 'charts three' });
  const count = h('span', { class: 'count' });
  const tableHost = h('div');
  const logList = h('div', { class: 'log-list short scroll' });
  const search = h('input', { type: 'search', placeholder: 'IP、模组版本、实例', oninput: event => { state.filter = event.target.value; renderTable(); } });
  const show = h('select', { class: 'select', onchange: event => { state.show = event.target.value; renderTable(); } },
    h('option', { value: 'all' }, '全部'), h('option', { value: 'forwarding' }, '转发中'), h('option', { value: 'rejected' }, '已拒绝'));
  root.replaceChildren(h('div', { class: 'page' },
    stats,
    charts,
    h('section', { class: 'panel' },
      h('div', { class: 'panel-head' },
        h('span', { class: 'panel-title' }, '连接记录', count),
        h('span', { class: 'spacer' }), show, h('label', { class: 'search' }, icon('search'), search)),
      h('p', { class: 'panel-note muted' }, '记录发送了有效握手的连接，保留最近 2000 条，只存在 Gateway 内存中，重启后清空。'),
      tableHost),
    h('div', { class: 'two-col' },
      h('section', { class: 'panel' }, h('div', { class: 'panel-head' }, h('span', { class: 'panel-title' }, '拒绝原因'),
        h('span', { class: 'spacer' }), h('span', { class: 'muted' }, '自启动以来')), reasons),
      h('section', { class: 'panel' }, h('div', { class: 'panel-head' }, h('span', { class: 'panel-title' }, 'Gateway 日志')), logList))));

  async function refresh() {
    try {
      const [info, connections, history, logs] = await Promise.all([
        api('GET', 'gateway'), api('GET', 'connections?limit=2000'), api('GET', 'history'),
        api('GET', 'logs' + (state.logNext != null ? '?after=' + state.logNext : '?limit=300')),
      ]);
      if (state.logNext != null && logs.next < state.logNext) { state.logs = []; state.logNext = null; }
      else { state.logs.push(...logs.entries); state.logs.splice(0, Math.max(0, state.logs.length - 300)); state.logNext = logs.next; }
      Object.assign(state, { info, connections, history, unsupported: false });
      app.online(true);
    } catch (error) {
      if (error.status === 401) return;
      if (error.status === 404) state.unsupported = true;
      else app.online(false);
    }
    render();
  }

  function render() {
    if (state.unsupported) {
      stats.replaceChildren(h('div', { class: 'notice' }, '当前 Gateway 版本较旧，没有连接记录与统计。重新部署 Gateway 后可用。'));
      return;
    }
    const info = state.info;
    if (!info) { stats.replaceChildren(h('div', { class: 'empty' }, '加载中…')); return; }
    const t = info.totals, p = info.process;
    const rejected = Object.values(t.rejected).reduce((a, b) => a + b, 0);
    const stat = (label, value, note) => h('div', { class: 'stat' },
      h('div', { class: 'stat-label' }, label), h('div', { class: 'stat-value' }, value), h('div', { class: 'stat-note' }, note));
    stats.replaceChildren(
      stat('当前连接', info.connections, '累计接入 ' + t.accepted),
      stat('累计转发', t.forwarded, '拒绝 ' + rejected + ' · 无效 ' + t.invalid + (t.overflow ? ' · 超限 ' + t.overflow : '')),
      stat('运行时长', fmtDuration(p.uptimeSeconds), '启动于 ' + fmtDateTime(p.startedAt)),
      stat('内存', fmtBytes(p.workingSetBytes), '线程 ' + p.threads + ' · ' + p.runtime));
    charts.replaceChildren(
      lineChart({ title: '当前连接', points: series(state.history, 'connections'), current: info.connections }),
      lineChart({ title: '每分钟转发', points: series(state.history, 'forwarded') }),
      lineChart({ title: '每分钟拒绝', points: series(state.history, 'rejected') }));
    const entries = Object.entries(t.rejected).sort((a, b) => b[1] - a[1]);
    reasons.replaceChildren(...(entries.length || t.invalid ? [
      ...entries.map(([reason, n]) => h('div', { class: 'reason' }, h('span', null, named(ERROR_NAMES, reason)), h('span', { class: 'mono' }, n))),
      h('div', { class: 'reason muted', title: '未发送有效握手，例如端口扫描或网络中断；不逐条记录' }, h('span', null, '无效或未完成握手'), h('span', { class: 'mono' }, t.invalid)),
    ] : [h('div', { class: 'empty' }, '没有被拒绝的连接')]));
    renderTable();
    renderLogs();
  }

  function renderTable() {
    if (!state.connections) return;
    const filter = state.filter.trim().toLowerCase();
    const rows = state.connections.filter(c => (state.show === 'all' || c.state === state.show) &&
      (!filter || [c.ip, c.modVersion, c.gameVersion, c.instanceId ?? ''].some(v => v.toLowerCase().includes(filter))));
    count.textContent = rows.length === state.connections.length ? String(rows.length) : rows.length + ' / ' + state.connections.length;
    if (rows.length === 0) {
      tableHost.replaceChildren(h('div', { class: 'empty' }, state.connections.length ? '没有匹配的连接' : '还没有连接记录'));
      return;
    }
    tableHost.replaceChildren(h('table', { class: 'table' },
      h('thead', null, h('tr', null, ['时间', '客户端', '模组 / 游戏版本', '结果', '实例', '时长', '流量 ↑ / ↓'].map(t => h('th', null, t)))),
      h('tbody', null, rows.slice(0, 500).map(c => {
        const [cls, label] = STATES[c.state] ?? ['idle', c.state];
        const duration = c.endedAt ? fmtDuration((new Date(c.endedAt) - new Date(c.startedAt)) / 1000) : since(c.startedAt);
        return h('tr', null,
          cell('时间', null, h('span', { class: 'mono', title: fmtDateTime(c.startedAt) }, fmtTime(c.startedAt))),
          cell('客户端', 'wide', h('span', { class: 'mono ip' }, c.ip), h('span', { class: 'sub' }, '端口 ' + c.port)),
          cell('版本', null, h('span', { class: 'mono' }, c.modVersion), h('span', { class: 'sub' }, c.gameVersion)),
          cell('结果', null, h('span', { class: 'pill ' + cls }, label), c.reason ? h('span', { class: 'sub' }, named(ERROR_NAMES, c.reason)) : null),
          cell('实例', null, c.instanceId ? h('a', { class: 'mono', href: '#/instance/' + encodeURIComponent(c.instanceId) }, c.instanceId) : h('span', { class: 'muted' }, '—')),
          cell('时长', null, c.state === 'rejected' ? '—' : duration),
          cell('流量', 'mono', fmtBytes(c.bytesIn) + ' / ' + fmtBytes(c.bytesOut)));
      }))),
      ...(rows.length > 500 ? [h('div', { class: 'empty' }, '只显示最近 500 条，可用筛选缩小范围')] : []));
  }

  function renderLogs() {
    const atBottom = logList.scrollHeight - logList.scrollTop - logList.clientHeight < 24;
    logList.replaceChildren(...(state.logs.length ? state.logs.map(e => h('div', { class: 'log-line ' + e.level },
      h('span', { class: 'log-time mono', title: fmtDateTime(e.time) }, fmtTime(e.time)),
      h('span', { class: 'log-level' }, LEVEL_NAMES[e.level] ?? e.level),
      h('span', { class: 'log-msg' }, e.message))) : [h('div', { class: 'empty' }, '暂无日志')]));
    if (atBottom) logList.scrollTop = logList.scrollHeight;
  }

  render();
  return { refresh, dispose() {} };
}

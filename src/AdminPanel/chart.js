// 单序列折线图：标题即序列名，无图例；悬停显示十字线与数值。
import { h, svg, fmtTime } from './core.js';

const W = 600, H = 140;

function niceMax(value) {
  if (value <= 0) return 1;
  const power = 10 ** Math.floor(Math.log10(value));
  for (const step of [1, 2, 2.5, 5, 10]) if (step * power >= value) return step * power;
  return 10 * power;
}

/**
 * points: [[unix 毫秒, 值], ...]；format(value) 生成数值文本。
 */
export function lineChart({ title, points, format = v => String(v), note, current }) {
  const latest = current ?? (points.length ? points[points.length - 1][1] : null);
  const head = h('div', { class: 'chart-head' },
    h('span', { class: 'chart-title' }, title),
    h('span', { class: 'chart-value' }, latest == null ? '—' : format(latest)));
  const card = h('div', { class: 'chart' }, head);
  if (points.length < 2) {
    card.append(h('div', { class: 'chart-empty' }, note ?? '采样不足，每分钟记录一次'));
    return card;
  }
  const t0 = points[0][0], t1 = points[points.length - 1][0];
  const max = niceMax(Math.max(...points.map(p => p[1])));
  const x = t => (t - t0) / (t1 - t0 || 1) * W;
  const y = v => H - v / max * H;
  const line = points.map((p, i) => (i ? 'L' : 'M') + x(p[0]).toFixed(1) + ' ' + y(p[1]).toFixed(1)).join('');
  const area = line + `L${W} ${H}L0 ${H}Z`;
  const cross = svg('line', { class: 'chart-cross', x1: 0, x2: 0, y1: 0, y2: H, 'vector-effect': 'non-scaling-stroke', visibility: 'hidden' });
  const plot = svg('svg', { class: 'chart-svg', viewBox: `0 0 ${W} ${H}`, preserveAspectRatio: 'none', 'aria-hidden': 'true' },
    [0.5, 1].map(f => svg('line', { class: 'chart-grid', x1: 0, x2: W, y1: H - f * H, y2: H - f * H, 'vector-effect': 'non-scaling-stroke' })),
    svg('path', { class: 'chart-area', d: area }),
    svg('path', { class: 'chart-line', d: line, 'vector-effect': 'non-scaling-stroke' }),
    cross);
  const dot = h('i', { class: 'chart-dot', hidden: true });
  const tip = h('div', { class: 'chart-tip', hidden: true });
  const body = h('div', { class: 'chart-body' },
    h('span', { class: 'chart-y top' }, format(max)), h('span', { class: 'chart-y mid' }, format(max / 2)), h('span', { class: 'chart-y zero' }, format(0)),
    plot, dot, tip);
  const hide = () => { cross.setAttribute('visibility', 'hidden'); dot.hidden = tip.hidden = true; };
  body.addEventListener('pointerleave', hide);
  body.addEventListener('pointermove', event => {
    const rect = plot.getBoundingClientRect();
    const ratio = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
    const target = t0 + ratio * (t1 - t0);
    let best = points[0];
    for (const p of points) if (Math.abs(p[0] - target) < Math.abs(best[0] - target)) best = p;
    const px = x(best[0]) / W, py = y(best[1]) / H;
    cross.setAttribute('x1', x(best[0])); cross.setAttribute('x2', x(best[0])); cross.setAttribute('visibility', 'visible');
    dot.hidden = tip.hidden = false;
    dot.style.left = px * 100 + '%'; dot.style.top = py * 100 + '%';
    tip.replaceChildren(h('span', { class: 'muted' }, fmtTime(best[0])), ' ', h('b', null, format(best[1])));
    tip.style.left = Math.min(Math.max(px * 100, 12), 88) + '%';
  });
  card.append(body, h('div', { class: 'chart-x' }, h('span', null, fmtTime(t0)), h('span', null, fmtTime(t1))));
  return card;
}

/** 把历史快照 { names, points } 中的某一列取出来。 */
export function series(history, name) {
  const index = history?.names?.indexOf(name) ?? -1;
  if (index < 0) return [];
  return history.points.map(p => [p[0], p[index + 1]]);
}

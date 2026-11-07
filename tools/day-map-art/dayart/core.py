"""画布、坐标、噪声、遮罩与色阶量化。

约定：世界坐标 x 向右、y 向上，单位为格；像素坐标原点在左上角。
所有颜色数组为 float32，范围 0..1；遮罩为 bool 数组。
"""
import colorsys
import math

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi
from scipy.spatial import cKDTree

PPU = 48


# ---------------------------------------------------------------- 颜色

def rgb(h):
    """'#rrggbb' 或 (r,g,b) → float32[3]。"""
    if isinstance(h, str):
        h = h.lstrip('#')
        return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32) / 255
    return np.array(h[:3], np.float32) / (255 if max(h[:3]) > 1 else 1)


def ramp(*colors):
    """色阶：从暗到亮的颜色数组 (n,3)。"""
    return np.stack([rgb(c) for c in colors])


def shift(c, dh=0.0, ds=0.0, dv=0.0):
    """在 HSV 空间偏移色相/饱和度/明度，用于做色相偏移的明暗。"""
    r, g, b = [float(v) for v in rgb(c)]
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    r, g, b = colorsys.hsv_to_rgb((h + dh) % 1, min(1, max(0, s + ds)), min(1, max(0, v + dv)))
    return np.array([r, g, b], np.float32)


class Frame:
    """世界矩形 [x0,x1]×[y0,y1] 到像素画布的映射。"""

    def __init__(self, x0, y0, x1, y1, ppu=PPU):
        self.x0, self.y0, self.x1, self.y1, self.ppu = x0, y0, x1, y1, ppu
        self.W = int(round((x1 - x0) * ppu))
        self.H = int(round((y1 - y0) * ppu))

    @property
    def shape(self):
        return (self.H, self.W)

    def px(self, x, y):
        return ((x - self.x0) * self.ppu, (self.y1 - y) * self.ppu)

    def world(self, px, py):
        return (self.x0 + px / self.ppu, self.y1 - py / self.ppu)

    def grid(self):
        """每个像素中心的世界坐标 (X, Y)。"""
        xs = self.x0 + (np.arange(self.W, dtype=np.float32) + 0.5) / self.ppu
        ys = self.y1 - (np.arange(self.H, dtype=np.float32) + 0.5) / self.ppu
        return np.meshgrid(xs, ys)

    def blank(self, channels=4):
        return np.zeros((self.H, self.W, channels), np.float32)


# ---------------------------------------------------------------- 噪声

def value_noise(shape, cell, seed, octaves=4, persistence=0.5, lacunarity=2.0, normalize=True):
    """分形值噪声。cell 为最低频网格的像素尺寸。"""
    rng = np.random.default_rng(seed)
    H, W = shape
    out = np.zeros(shape, np.float32)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        c = max(2.0, cell / lacunarity ** o)
        gh, gw = int(math.ceil(H / c)) + 4, int(math.ceil(W / c)) + 4
        g = rng.random((gh, gw)).astype(np.float32)
        z = ndi.zoom(g, c, order=3, mode='nearest', grid_mode=True)
        oy, ox = (rng.integers(0, max(1, int(c)), 2) + int(c))
        out += amp * z[oy:oy + H, ox:ox + W]
        total += amp
        amp *= persistence
    out /= total
    if normalize:
        lo, hi = np.percentile(out, [1, 99])
        out = np.clip((out - lo) / max(1e-6, hi - lo), 0, 1)
    return out


def poly_mask(frame, pts, closed=True, width=0):
    """世界坐标多边形（或折线 width>0）光栅化，无抗锯齿。"""
    im = Image.new('L', (frame.W, frame.H), 0)
    d = ImageDraw.Draw(im)
    p = [frame.px(x, y) for x, y in pts]
    if closed:
        d.polygon(p, fill=255)
    else:
        d.line(p, fill=255, width=max(1, int(round(width * frame.ppu))), joint='curve')
        r = width * frame.ppu / 2
        for (px, py) in (p[0], p[-1]):
            d.ellipse([px - r, py - r, px + r, py + r], fill=255)
    return np.asarray(im) > 0


def catmull(points, per=12, closed=False):
    """Catmull-Rom 平滑折线。"""
    P = list(points)
    if closed:
        P = P[-1:] + P + P[:2]
    else:
        P = [P[0]] + P + [P[-1]]
    out = []
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = (np.array(P[j], float) for j in (i - 1, i, i + 1, i + 2))
        for t in np.linspace(0, 1, per, endpoint=False):
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)))
    if not closed:
        out.append(tuple(P[-2]))
    return out


def sdf(mask):
    """有符号距离（像素）：外部为正，内部为负。"""
    return ndi.distance_transform_edt(~mask) - ndi.distance_transform_edt(mask)


def roughen(mask, amp, cell, seed, octaves=3):
    """用噪声扰动边界，得到自然轮廓。amp、cell 为像素。"""
    n = value_noise(mask.shape, cell, seed, octaves) * 2 - 1
    return sdf(mask) + n * amp < 0


def clean(mask, it=1):
    """开闭运算去掉单像素毛刺，保持像素画轮廓干净。"""
    m = ndi.binary_opening(mask, iterations=it)
    return ndi.binary_closing(m, iterations=it)


def shifted(mask, dx, dy):
    """遮罩整体平移（像素，dy 向下为正），越界补 False。"""
    out = np.zeros_like(mask)
    H, W = mask.shape
    xs, xd = (slice(0, W - dx), slice(dx, W)) if dx >= 0 else (slice(-dx, W), slice(0, W + dx))
    ys, yd = (slice(0, H - dy), slice(dy, H)) if dy >= 0 else (slice(-dy, H), slice(0, H + dy))
    out[yd, xd] = mask[ys, xs]
    return out


# ---------------------------------------------------------------- 色阶量化

def quantize(value, thresholds):
    """连续值 → 色阶索引。"""
    return np.digitize(value, thresholds).astype(np.int16)


def mode_filter(idx, size=3, classes=None, mask=None):
    """众数滤波：把量化后的碎点合并成平涂色块。"""
    classes = classes if classes is not None else np.unique(idx)
    best = np.full(idx.shape, -1.0, np.float32)
    out = idx.copy()
    for c in classes:
        score = ndi.uniform_filter((idx == c).astype(np.float32), size)
        better = score > best
        out[better] = c
        best[better] = score[better]
    if mask is not None:
        out = np.where(mask, out, idx)
    return out


def despeckle(idx, passes=1):
    """孤立像素并入周围多数色。"""
    for _ in range(passes):
        H, W = idx.shape
        p = np.pad(idx, 1, mode='edge')
        n = np.stack([p[0:H, 1:W + 1], p[2:H + 2, 1:W + 1], p[1:H + 1, 0:W], p[1:H + 1, 2:W + 2]])
        lonely = np.all(n != idx[None], axis=0)
        # 取四邻域里出现次数最多的值
        vals = np.sort(n, axis=0)
        major = np.where(vals[1] == vals[2], vals[1], np.where(vals[0] == vals[1], vals[0], vals[2]))
        idx = np.where(lonely, major, idx)
    return idx


def colorize(idx, palette):
    pal = np.asarray(palette, np.float32)
    return pal[np.clip(idx, 0, len(pal) - 1)]


# ---------------------------------------------------------------- 合成

def blend(dst, rgb_img, mask=None, alpha=1.0):
    """把 RGB 或 RGBA 图按遮罩写入 RGBA 画布。"""
    if rgb_img.ndim == 1:
        rgb_img = np.broadcast_to(rgb_img, dst.shape[:2] + (3,))
    a = np.ones(dst.shape[:2], np.float32) * alpha
    if rgb_img.shape[-1] == 4:
        a = a * rgb_img[..., 3]
        rgb_img = rgb_img[..., :3]
    if mask is not None:
        a = a * mask
    a3 = a[..., None]
    dst[..., :3] = dst[..., :3] * (1 - a3) + rgb_img * a3
    dst[..., 3] = dst[..., 3] * (1 - a) + a


def paste(dst, spr, x, y, alpha=1.0):
    """把 RGBA 小图贴到画布像素位置 (x,y)=左上角，自动裁剪。"""
    h, w = spr.shape[:2]
    H, W = dst.shape[:2]
    x, y = int(round(x)), int(round(y))
    x0, y0, x1, y1 = max(0, x), max(0, y), min(W, x + w), min(H, y + h)
    if x0 >= x1 or y0 >= y1:
        return
    s = spr[y0 - y:y1 - y, x0 - x:x1 - x]
    blend(dst[y0:y1, x0:x1], s, None, alpha)


def scatter(mask, spacing, seed, jitter=0.5):
    """在遮罩内取近似泊松分布的点（像素坐标），spacing 为最小间距。"""
    rng = np.random.default_rng(seed)
    H, W = mask.shape
    step = spacing
    ys, xs = np.mgrid[0:H:step, 0:W:step]
    pts = np.stack([xs.ravel(), ys.ravel()], 1).astype(np.float32)
    pts += (rng.random(pts.shape) - 0.5) * 2 * jitter * step + step / 2
    pts = pts[(pts[:, 0] >= 0) & (pts[:, 0] < W) & (pts[:, 1] >= 0) & (pts[:, 1] < H)]
    keep = mask[pts[:, 1].astype(int), pts[:, 0].astype(int)]
    pts = pts[keep]
    rng.shuffle(pts)
    if len(pts) == 0:
        return pts
    tree = cKDTree(pts)
    taken = np.zeros(len(pts), bool)
    alive = np.ones(len(pts), bool)
    for i in range(len(pts)):
        if not alive[i]:
            continue
        taken[i] = True
        for j in tree.query_ball_point(pts[i], spacing * 0.85):
            if j != i:
                alive[j] = False
    return pts[taken]


def to_image(arr):
    return Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8))

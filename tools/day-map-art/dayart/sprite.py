"""道具精灵：像素坐标下的绘图原语 + 自动描边/阴影。

Sprite 的 pivot 是“脚点”像素坐标（相对左上角），导出时换算成归一化锚点，
放进地图时世界坐标就是脚点位置，用于按 Y 排序。
"""
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage as ndi

from .core import rgb


def C(c, a=1.0):
    """颜色 → RGBA float32。"""
    v = rgb(c) if isinstance(c, str) or len(c) == 3 else np.asarray(c[:3], np.float32)
    return np.array([v[0], v[1], v[2], a], np.float32)


class Sprite:
    def __init__(self, w, h, pivot=None):
        self.a = np.zeros((h, w, 4), np.float32)
        self.pivot = pivot if pivot is not None else (w / 2, h)

    @property
    def w(self):
        return self.a.shape[1]

    @property
    def h(self):
        return self.a.shape[0]

    # ------------------------------------------------------------ 原语（像素坐标，左上原点）
    def put(self, mask, color, alpha=1.0):
        c = C(color)
        a = mask.astype(np.float32) * alpha * c[3]
        self.a[..., :3] = self.a[..., :3] * (1 - a[..., None]) + c[:3] * a[..., None]
        self.a[..., 3] = np.maximum(self.a[..., 3], a)
        return mask

    def mask(self):
        return np.zeros((self.h, self.w), bool)

    def rect(self, x, y, w, h, color, alpha=1.0):
        m = self.mask()
        m[max(0, int(y)):max(0, int(y + h)), max(0, int(x)):max(0, int(x + w))] = True
        return self.put(m, color, alpha)

    def poly(self, pts, color, alpha=1.0):
        im = Image.new('L', (self.w, self.h), 0)
        ImageDraw.Draw(im).polygon([tuple(p) for p in pts], fill=255)
        return self.put(np.asarray(im) > 0, color, alpha)

    def line(self, pts, color, width=1, alpha=1.0):
        im = Image.new('L', (self.w, self.h), 0)
        ImageDraw.Draw(im).line([tuple(p) for p in pts], fill=255, width=width)
        return self.put(np.asarray(im) > 0, color, alpha)

    def ellipse(self, cx, cy, rx, ry, color, alpha=1.0):
        yy, xx = np.mgrid[0:self.h, 0:self.w].astype(np.float32) + 0.5
        m = ((xx - cx) / max(rx, 0.5)) ** 2 + ((yy - cy) / max(ry, 0.5)) ** 2 <= 1
        return self.put(m, color, alpha)

    def px(self, x, y, color, alpha=1.0):
        x, y = int(x), int(y)
        if 0 <= x < self.w and 0 <= y < self.h:
            c = C(color)
            a = alpha * c[3]
            self.a[y, x, :3] = self.a[y, x, :3] * (1 - a) + c[:3] * a
            self.a[y, x, 3] = max(self.a[y, x, 3], a)

    def text(self, s, x, y, color, font=None, size=12, vertical=False):
        """在精灵上写字（单色、无抗锯齿）。font 为 TTF/OTF 路径。"""
        if font is None:
            return
        f = ImageFont.truetype(font, size)
        im = Image.new('L', (self.w, self.h), 0)
        d = ImageDraw.Draw(im)
        d.fontmode = '1'
        if vertical:
            yy = y
            for ch in s:
                d.text((x, yy), ch, font=f, fill=255)
                yy += size
        else:
            d.text((x, y), s, font=f, fill=255)
        return self.put(np.asarray(im) > 127, color)

    # ------------------------------------------------------------ 加工
    def alpha_mask(self, t=0.5):
        return self.a[..., 3] > t

    def outline(self, color, inner=True, sides='all', t=1):
        """自动描边。inner=True 在形状内侧描（不改变尺寸）；sides='bottom' 只描下半边缘。"""
        m = self.alpha_mask()
        if inner:
            e = m & ~ndi.binary_erosion(m, iterations=t)
        else:
            e = ndi.binary_dilation(m, iterations=t) & ~m
        if sides == 'bottom':
            below = np.zeros_like(m)
            below[:-1] = m[1:]
            e &= ~below | ~m
        return self.put(e, color)

    def flip(self):
        s = Sprite(self.w, self.h, (self.w - self.pivot[0], self.pivot[1]))
        s.a = self.a[:, ::-1].copy()
        return s

    def paste(self, other, x, y):
        """把另一个精灵按其左上角贴到 (x,y)。"""
        h, w = other.a.shape[:2]
        x, y = int(x), int(y)
        x0, y0, x1, y1 = max(0, x), max(0, y), min(self.w, x + w), min(self.h, y + h)
        if x0 >= x1 or y0 >= y1:
            return
        src = other.a[y0 - y:y1 - y, x0 - x:x1 - x]
        dst = self.a[y0:y1, x0:x1]
        a = src[..., 3:4]
        dst[..., :3] = dst[..., :3] * (1 - a) + src[..., :3] * a
        dst[..., 3] = dst[..., 3] * (1 - src[..., 3]) + src[..., 3]

    def trim(self, pad=1):
        """裁掉透明边，pivot 同步平移。"""
        ys, xs = np.nonzero(self.a[..., 3] > 0.01)
        if len(ys) == 0:
            return self
        x0, y0 = max(0, xs.min() - pad), max(0, ys.min() - pad)
        x1, y1 = min(self.w, xs.max() + 1 + pad), min(self.h, ys.max() + 1 + pad)
        s = Sprite(x1 - x0, y1 - y0, (self.pivot[0] - x0, self.pivot[1] - y0))
        s.a = self.a[y0:y1, x0:x1].copy()
        return s

    def image(self):
        return Image.fromarray((np.clip(self.a, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGBA')


def ascii_sprite(rows, colors, pivot=None):
    """用字符画定义小精灵；'.' 为透明。"""
    h, w = len(rows), max(len(r) for r in rows)
    s = Sprite(w, h, pivot)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != '.' and ch != ' ':
                s.px(x, y, colors[ch])
    return s

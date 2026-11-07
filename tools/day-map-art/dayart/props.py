"""通用道具：岩石、积石、树、柳、灯笼、花草等。尺寸单位为像素（48 像素 = 1 格）。

画法统一：光从左上来；物体分“顶面 + 朝镜头的前侧面”两块明暗；1 像素深色描边，
底部描边更重；颜色用色相偏移（暗部偏冷、亮部偏暖）。
"""
import math

import numpy as np
from scipy import ndimage as ndi

from .core import rgb, sdf, shift, value_noise
from .sprite import Sprite, ascii_sprite


def boulder(w, h, seed, pal=('#c9bfbf', '#ab9f9f', '#8d8083', '#6a5f66'), outline='#4a3c44', front=0.38,
            moss=0.0, moss_pal=('#7d8b4c', '#94a35a'), facet=True):
    """3/4 视角的石块：由 2~3 个椭圆拼出的不规则轮廓，浅色顶面 + 深色前侧面 + 左上高光 + 深色描边。

    pal = (高光, 顶面, 前侧面, 暗部)。front 为前侧面占高度比例。"""
    rng = np.random.default_rng(seed)
    s = Sprite(w + 4, h + 4, ((w + 4) / 2, h + 2))
    H, W = h + 4, w + 4
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32) + 0.5
    body = np.zeros((H, W), bool)
    lobes = [(W / 2, H / 2, w / 2, h / 2)]
    for _ in range(int(rng.integers(1, 3))):
        lw, lh = w * rng.uniform(0.3, 0.5), h * rng.uniform(0.45, 0.7)
        lx = W / 2 + rng.choice([-1, 1]) * (w / 2 - lw * 0.8)
        ly = H / 2 + rng.uniform(-0.1, 0.25) * h
        lobes.append((lx, ly, lw, lh))
    for i, (cx, cy, rx, ry) in enumerate(lobes):
        sc = 0.86 if i == 0 else 1.0
        body |= ((xx - cx) / (rx * sc)) ** 2 + ((yy - cy) / (ry * sc)) ** 2 <= 1
    n = value_noise((H, W), max(4, w / 4), seed, 2, normalize=False) - 0.5
    body = (sdf(body) + n * 3.0) < 0
    body = ndi.binary_opening(body, iterations=1)
    body[:, :1] = body[:, -1:] = False
    body[:1] = body[-1:] = False
    k = max(2, int(h * front))
    below = np.zeros_like(body)
    below[:-k] = body[k:]
    side = body & ~below
    top = body & ~side
    hi, topc, sidec, dark = pal
    s.put(top, topc)
    s.put(side, sidec)
    cx, cy = W / 2, H / 2
    hl = top & (((xx - cx * 0.78) / (w * 0.3)) ** 2 + ((yy - cy * 0.62) / (h * 0.26)) ** 2 < 1)
    if facet:
        hl |= top & (value_noise((H, W), max(3, w / 5), seed + 1, 2) > 0.62) & (xx < cx + w * 0.1)
    s.put(ndi.binary_opening(hl), hi)
    s.put(side & (xx > cx + w * 0.1), dark)
    s.put(side & (value_noise((H, W), max(3, w / 6), seed + 4, 2) > 0.7), dark, 0.7)
    if moss > 0:
        mm = top & (value_noise((H, W), max(3, w / 3), seed + 2, 2) < moss) & (yy < cy + h * 0.1)
        s.put(mm, moss_pal[0])
        s.put(mm & (value_noise((H, W), 3, seed + 3, 1) > 0.6), moss_pal[1])
    e = body & ~ndi.binary_erosion(body)
    s.put(e, outline)
    bottom = body & ~np.roll(body, -1, 0)
    s.put(ndi.binary_dilation(bottom, structure=np.array([[0, 0, 0], [1, 1, 1], [0, 0, 0]])) & body, outline)
    return s


def stone_stack(seed, n=None, base_w=None, pal=None):
    """赛之河原的积石：自下而上逐渐变小的扁平卵石。"""
    rng = np.random.default_rng(seed)
    n = n or int(rng.integers(3, 8))
    base_w = base_w or int(rng.integers(22, 32))
    stones = []
    w = base_w
    for i in range(n):
        h = max(6, int(w * rng.uniform(0.45, 0.62)))
        stones.append((w, h, rng.uniform(-1.6, 1.6)))
        w = max(6, int(w * rng.uniform(0.68, 0.86)))
    total = sum(h - 2 for _, h, _ in stones) + 6
    W = base_w + 10
    s = Sprite(W, total + 2, (W / 2, total))
    y = total
    xoff = 0.0
    palettes = pal or [('#e2ddd2', '#c4bfb3', '#9a958a', '#75716a'), ('#d8dbdc', '#b9bdc0', '#91959b', '#6c7077'),
                       ('#e0d6cb', '#c2b7ab', '#988d82', '#73695f'), ('#d6dacd', '#b7bdaf', '#8f9688', '#6b7266')]
    for i, (w, h, dx) in enumerate(stones):
        xoff += dx
        b = boulder(w, h, seed * 7 + i, palettes[int(rng.integers(len(palettes)))], outline='#2f2c33',
                    front=0.42, facet=False)
        s.paste(b, W / 2 - b.w / 2 + xoff, y - b.h + 1)
        y -= h - 3
    return s.trim()


def higanbana_head(variant=0, pal=('#ff6b5e', '#e0283c', '#a8182c', '#6e1020'), stamen='#ff8a7a'):
    """彼岸花花头（约 11×9 像素）：向外反卷的花瓣 + 更长的花蕊。"""
    L, R, D, K, S = 'L', 'R', 'D', 'K', 'S'
    designs = [
        ["S...S.S...S",
         ".S..S.S..S.",
         "..SRLRLRS..",
         ".SRRDKDRRS.",
         "S.LRKKKRL.S",
         "..RDRKRDR..",
         ".R..RDR..R.",
         "....D.D...."],
        [".S...S...S.",
         "..S..S..S..",
         "S..RLRLR..S",
         ".SRRDKDRRS.",
         "..LRKKKRL..",
         ".RRDRKRDRR.",
         "R...DRD...R",
         "....D.D...."],
        ["..S..S..S..",
         "S..SLRLS..S",
         ".SRRRDRRRS.",
         "..LRDKDRL..",
         ".RRDKKKDRR.",
         "R..RDRDR..R",
         "....D.D...."],
    ]
    rows = designs[variant % len(designs)]
    cols = {'L': pal[0], 'R': pal[1], 'D': pal[2], 'K': pal[3], 'S': stamen}
    return ascii_sprite(rows, cols)


def higanbana_clump(seed, n=None, stem='#4f7a37', stem_dark='#3a5c2a', scale=1.0):
    """一丛彼岸花：只有花茎没有叶（花叶不相见）。"""
    rng = np.random.default_rng(seed)
    n = n or int(rng.integers(2, 6))
    W, H = int(36 * scale) + 12, int(30 * scale) + 12
    s = Sprite(W, H, (W / 2, H - 2))
    heads = []
    for i in range(n):
        x = W / 2 + rng.normal(0, 5 * scale)
        top = H - 2 - rng.uniform(10, 22) * scale
        heads.append((x, top, int(rng.integers(3))))
    heads.sort(key=lambda t: t[1])
    for x, top, v in heads:
        bend = rng.uniform(-2, 2)
        pts = [(x + bend * (1 - t) ** 2, top + (H - 2 - top) * t) for t in np.linspace(0, 1, 8)]
        s.line([(px, py) for px, py in pts], stem, 1)
        s.px(pts[-1][0] + 1, pts[-1][1] - 1, stem_dark)
        hd = higanbana_head(v)
        if rng.random() < 0.5:
            hd = hd.flip()
        s.paste(hd, x - hd.w / 2, top - hd.h + 3)
    return s.trim()


def pinwheel(color='#d8333c', color2='#f1e6d0', stick='#8a6a48', height=30):
    """纸风车（鬼形兽赛之河原的红绿风车）：四片主色风叶，叶尖一点浅色。"""
    s = Sprite(19, height + 13, (9, height + 12))
    s.line([(9, 12), (9, height + 12)], stick, 1)
    s.px(10, height + 12, '#5a4532')
    blades = ["...AAA.....",
              "...AAAB....",
              "....AABB...",
              "BB..AAB..AA",
              "BBBA.X.AAAA",
              "AAAA.X.ABBB",
              "AA..BAA..BB",
              "...BBAA....",
              "....BAAA...",
              ".....AAA..."]
    dark = shift(color, 0.0, 0.05, -0.25)
    hd = ascii_sprite(blades, {'A': color, 'B': dark, 'X': '#ffe9a8'})
    s.paste(hd, 4, 2)
    s.px(4, 6, color2)
    s.px(14, 8, color2)
    s.outline('#2e2220')
    return s


def sotoba(seed, height=None, wood=('#d9cbb0', '#bba98a', '#8f7d63'), ink='#3a3230'):
    """卒塔婆：顶端刻五轮缺口的细长木板，正面写墨字。"""
    rng = np.random.default_rng(seed)
    h = height or int(rng.integers(40, 58))
    w = 7
    s = Sprite(w + 2, h + 2, ((w + 2) / 2, h + 1))
    light, mid, dark = wood
    s.rect(1, 6, w, h - 6, mid)
    s.rect(1, 6, 2, h - 6, light)
    s.rect(w - 1, 6, 2, h - 6, dark)
    # 五轮塔顶：尖 + 两处缺口
    s.poly([(1, 6), (1 + w / 2, 0), (1 + w, 6)], light)
    for yk in (8, 13):
        s.a[yk, 1, 3] = 0
        s.a[yk, w, 3] = 0
    for k in range(int(rng.integers(5, 9))):  # 墨字：竖排短划
        yk = 16 + k * 4
        if yk < h - 3:
            s.rect(3, yk, 2 if rng.random() < 0.6 else 3, 2, ink, 0.85)
    s.outline('#4c3e30')
    if rng.random() < 0.5:  # 风化后微斜
        a = s.a
        out = np.zeros_like(a)
        tilt = rng.choice([-1, 1])
        for y in range(a.shape[0]):
            dx = int(round(tilt * (a.shape[0] - y) / a.shape[0] * 1.5))
            out[y] = np.roll(a[y], dx, axis=0)
        s.a = out
    return s


def canopy(w, h, seed, pal, outline, blob=(10, 18), density=1.0, dots=True):
    """原版树冠画法：多个云团叠成冠，每团下缘有深色弧线，左上亮、右下暗，外轮廓 1 像素深线。

    pal = (暗, 中暗, 中, 亮, 高光)。返回 Sprite（不含树干）。"""
    rng = np.random.default_rng(seed)
    s = Sprite(w, h, (w / 2, h))
    cx, cy, rx, ry = w / 2, h / 2, w / 2 - 3, h / 2 - 3
    blobs = []
    for _ in range(int(w * h / (blob[1] ** 2) * 1.8 * density) + 4):
        a = rng.uniform(0, 2 * math.pi)
        r = math.sqrt(rng.random())
        bx, by = cx + math.cos(a) * r * (rx - blob[1] * 0.6), cy + math.sin(a) * r * (ry - blob[1] * 0.6)
        blobs.append((bx, by, rng.uniform(*blob)))
    blobs.sort(key=lambda b: b[1])  # 上面的先画，下面的压住上面的
    dark, mid_dark, mid, light, hi = (rgb(c) for c in pal)
    union = np.zeros((h, w), bool)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32) + 0.5
    for bx, by, br in blobs:
        bry = br * 0.9
        q = ((xx - bx) / br) ** 2 + ((yy - by) / bry) ** 2
        m = q <= 1
        # 团内明暗：左上亮
        lit = -(xx - bx) / br * 0.5 - (yy - by) / bry * 0.85
        # 越靠下的团整体越暗（冠的体积感）
        depth = (by - (cy - ry)) / (2 * ry)
        lit = lit - depth * 0.55 + 0.25
        col = np.where(lit[..., None] > 0.62, hi, np.where(lit[..., None] > 0.2, light,
                       np.where(lit[..., None] > -0.25, mid, mid_dark)))
        s.a[..., :3] = np.where(m[..., None], col, s.a[..., :3])
        s.a[..., 3] = np.where(m, 1.0, s.a[..., 3])
        # 下缘弧线
        arc = m & (q > 0.78) & (yy > by + bry * 0.25)
        s.put(arc, outline, 0.85)
        union |= m
    # 冠底部暗影 + 斑点纹理
    bottom = union & (yy > cy + ry * 0.35)
    if dots:
        chk = ((xx.astype(int) % 2 == 0) & (yy.astype(int) % 2 == 0)) & (value_noise((h, w), 10, seed + 5, 2) > 0.6)
        s.put(bottom & chk, dark, 0.8)
    e = union & ~ndi.binary_erosion(union)
    s.put(e, outline)
    s.a[..., 3] = np.where(union, 1.0, 0.0)
    return s


def tree(seed, w=120, h=104, trunk_h=34, pal=None, outline='#12302a', bark=('#8a4b34', '#6b3527', '#4a2219')):
    """普通阔叶树：云团树冠 + 红褐树干。脚点在树干底部。"""
    pal = pal or ('#164a43', '#1f6a52', '#2e8650', '#5aa040', '#86bd4e')
    cv = canopy(w, h, seed, pal, outline)
    H = h + trunk_h - 10
    s = Sprite(w, H, (w / 2, H - 2))
    tw = max(8, w // 9)
    x0 = w / 2 - tw / 2
    light, mid, dark = bark
    s.rect(x0, h - 24, tw, trunk_h + 12, mid)
    s.rect(x0, h - 24, 2, trunk_h + 12, light)
    s.rect(x0 + tw - 3, h - 24, 3, trunk_h + 12, dark)
    for k in range(3):  # 树根
        rx = x0 + [-3, tw // 2 - 1, tw + 1][k]
        s.poly([(rx, H - 2), (x0 + tw / 2, H - 9), (rx + 4, H - 2)], dark)
    s.line([(x0 + 3, h - 10), (x0 + 3, H - 6)], dark)
    s.paste(cv, 0, 0)
    s.outline(outline, sides='all')
    return s


def willow(seed, w=110, h=120, pal=('#2c5a4a', '#3d7656', '#5c9156', '#86ad5c', '#a9c46a'), outline='#183a30',
           bark=('#7a6a58', '#5c4e41', '#3d332b')):
    """垂柳：粗短树干 + 顶部小冠 + 大量下垂枝条。柳下鬼影是日本怪谈的常见意象。"""
    rng = np.random.default_rng(seed)
    s = Sprite(w, h, (w / 2, h - 2))
    light, mid, dark = bark
    tx = w / 2
    s.poly([(tx - 6, h - 2), (tx - 4, h - 46), (tx + 5, h - 50), (tx + 7, h - 2)], mid)
    s.line([(tx - 4, h - 4), (tx - 3, h - 44)], light, 2)
    s.line([(tx + 5, h - 4), (tx + 5, h - 46)], dark, 2)
    for bx, by in ((tx - 4, h - 44), (tx + 4, h - 48)):  # 主枝
        s.line([(bx, by), (bx + rng.uniform(-22, -10) if bx < tx else bx + rng.uniform(10, 22), by - rng.uniform(16, 26))], mid, 3)
    crown = canopy(int(w * 0.8), int(h * 0.42), seed, pal, outline, blob=(8, 13), dots=False)
    s.paste(crown, w * 0.1, 2)
    cols = [rgb(c) for c in pal]
    # 下垂枝条：从冠的下缘垂下，末端微微外弯
    for i in range(int(w * 0.9)):
        x = rng.uniform(w * 0.08, w * 0.92)
        y0 = rng.uniform(h * 0.18, h * 0.42)
        L = rng.uniform(h * 0.25, h * 0.62) * (1 - abs(x - w / 2) / w)
        k = int(np.clip(rng.normal(2.2, 1.0), 0, 4))
        curl = (x - w / 2) / w * 6
        for t in range(int(L)):
            px_ = x + curl * (t / L) ** 2
            py_ = y0 + t
            if t % 3 != 2 or rng.random() < 0.7:
                s.px(px_, py_, cols[k])
        s.px(x + curl, y0 + L, outline)
    s.outline(outline)
    return s


def stone_lantern(lit=False, pal=('#c3bfb4', '#a19d93', '#7f7b73', '#5d5a55'), outline='#3e3b39', moss=True, seed=0):
    """石灯笼（春日型简化）：基座、竿、中台、火袋、笠、宝珠。约 30×74 像素。"""
    hi, l, m, d = pal
    s = Sprite(34, 76, (17, 74))
    # 基座
    s.poly([(6, 74), (8, 68), (26, 68), (28, 74)], m)
    s.rect(8, 68, 18, 2, l)
    # 竿
    s.rect(13, 46, 8, 22, l)
    s.rect(19, 46, 2, 22, d)
    s.rect(13, 46, 2, 22, hi)
    # 中台
    s.poly([(7, 46), (10, 40), (24, 40), (27, 46)], m)
    s.rect(10, 40, 14, 2, l)
    # 火袋
    s.rect(10, 26, 14, 14, l)
    s.rect(21, 26, 3, 14, d)
    s.rect(14, 29, 6, 7, '#ffd27a' if lit else '#2c2a2e')
    if lit:
        s.rect(15, 30, 4, 5, '#fff2c8')
    # 笠（反翘屋顶）
    s.poly([(2, 26), (8, 18), (26, 18), (32, 26), (28, 25), (6, 25)], m)
    s.poly([(8, 18), (12, 14), (22, 14), (26, 18)], l)
    s.line([(2, 26), (6, 24)], hi)
    s.rect(4, 25, 26, 1, d)
    # 宝珠
    s.ellipse(17, 10, 4, 4, l)
    s.px(16, 8, hi)
    s.poly([(15, 5), (17, 1), (19, 5)], l)
    if moss:
        rng = np.random.default_rng(seed)
        for _ in range(10):
            x, y = int(rng.integers(6, 28)), int(rng.choice([19, 20, 41, 42, 69]))
            s.px(x, y, '#738049')
    s.outline(outline)
    return s


def paper_lantern(color='#f2e2c0', band='#b83a30', glow=True, w=13, h=16, text=None):
    """提灯：椭圆纸面 + 横向竹骨 + 上下黑口。"""
    s = Sprite(w + 2, h + 6, ((w + 2) / 2, 0))
    s.rect(w / 2 - 2, 0, 6, 3, '#2a2420')
    s.ellipse((w + 2) / 2, 3 + h / 2, w / 2, h / 2, color)
    for k in range(3, h, 3):
        s.line([(3, 3 + k), (w - 1, 3 + k)], shift(color, 0, 0.12, -0.12), 1, 0.6)
    s.rect(w / 2 - 2, h + 2, 6, 3, '#2a2420')
    if band:
        s.rect(2, 3 + h // 2 - 2, w - 2, 4, band, 0.9)
    s.outline('#3b2a22')
    return s


def grass_tuft(seed, pal=('#4f6236', '#66793f', '#83954c'), h=9):
    rng = np.random.default_rng(seed)
    s = Sprite(11, h + 1, (5, h))
    for i in range(int(rng.integers(3, 6))):
        x = 5 + rng.normal(0, 1.6)
        top = rng.uniform(2, h - 3)
        lean = rng.uniform(-2.5, 2.5)
        s.line([(x + lean, top), (x, h)], pal[int(rng.integers(len(pal)))], 1)
    return s


def bush(seed, w=40, h=28, pal=('#1c473e', '#2a5f4c', '#3f7a52', '#5f9450', '#7fab55'), outline='#11302a'):
    """矮灌木：小云团树冠，脚点在底部中央。"""
    cv = canopy(w, h, seed, pal, outline, blob=(6, 10), dots=False)
    cv.pivot = (w / 2, h - 2)
    return cv


def conifer(seed, w=70, h=150, pal=('#0f2a28', '#163a34', '#1f4c40', '#2d604c', '#3f7653'), outline='#0a1c1b',
            bark=('#6e4a36', '#54382a', '#3a271e')):
    """杉/扁柏：层层下垂的尖塔形树冠。"""
    rng = np.random.default_rng(seed)
    s = Sprite(w, h, (w / 2, h - 2))
    cols = [rgb(c) for c in pal]
    s.rect(w / 2 - 4, h - 26, 8, 24, bark[1])
    s.rect(w / 2 - 4, h - 26, 2, 24, bark[0])
    tiers = int(rng.integers(6, 9))
    for i in range(tiers):
        t = i / (tiers - 1)
        cy = 8 + t * (h - 40)
        half = 6 + t * (w / 2 - 8)
        th = (h - 40) / tiers * 1.6
        pts = [(w / 2, cy - th * 0.6), (w / 2 + half, cy + th * 0.55), (w / 2 + half * 0.4, cy + th * 0.42),
               (w / 2, cy + th * 0.6), (w / 2 - half * 0.4, cy + th * 0.42), (w / 2 - half, cy + th * 0.55)]
        s.poly(pts, cols[1])
        s.poly([(w / 2, cy - th * 0.6), (w / 2 - half, cy + th * 0.55), (w / 2 - half * 0.4, cy + th * 0.42), (w / 2, cy + th * 0.1)], cols[3])
        s.poly([(w / 2, cy - th * 0.6), (w / 2 + half, cy + th * 0.55), (w / 2 + half * 0.5, cy + th * 0.45), (w / 2 + 2, cy + th * 0.05)], cols[0])
        s.line([(w / 2 - half * 0.7, cy + th * 0.42), (w / 2 - half * 0.2, cy + th * 0.1)], cols[4], 1)
    s.outline(outline)
    return s


def dead_tree(seed, w=110, h=140, bark=('#7a7068', '#5a524c', '#3c3632'), outline='#221e1c'):
    """枯树：只剩枝干。"""
    rng = np.random.default_rng(seed)
    s = Sprite(w, h, (w / 2, h - 2))
    segs = []

    def br(x, y, a, L, wd, d):
        x2, y2 = x + math.cos(a) * L, y - math.sin(a) * L
        segs.append((x, y, x2, y2, wd))
        if d and wd > 1.2:
            for da in (rng.uniform(0.3, 0.8), -rng.uniform(0.3, 0.8)):
                br(x2, y2, a + da, L * rng.uniform(0.6, 0.78), wd * 0.65, d - 1)

    br(w / 2, h - 2, math.pi / 2 + rng.normal(0, 0.08), h * 0.32, 9, 4)
    for x1, y1, x2, y2, wd in sorted(segs, key=lambda t: -t[4]):
        s.line([(x1, y1), (x2, y2)], bark[1], max(1, int(wd)))
        s.line([(x1 - wd * 0.25, y1), (x2 - wd * 0.25, y2)], bark[0], max(1, int(wd * 0.35)))
    s.outline(outline)
    return s

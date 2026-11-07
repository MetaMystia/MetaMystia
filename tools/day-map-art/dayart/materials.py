"""地表材质：平涂色块 + 少量笔触，模仿原版的厚涂像素风。

每个函数在给定遮罩内绘制并返回 RGBA 图层（遮罩外透明），或直接写入传入的画布。
"""
import numpy as np
from scipy import ndimage as ndi

from .core import blend, clean, colorize, despeckle, mode_filter, quantize, rgb, scatter, shifted, value_noise


def mottled(mask, palette, seed, cell=48, weights=None, mode=7, octaves=3, bias=None):
    """斑驳地面：低频噪声量化成 len(palette) 级色块，再用众数滤波合并成平涂块。

    weights 为各级占比（累计阈值由它决定），bias 为额外的连续偏移场（如光照、湿度）。"""
    n = value_noise(mask.shape, cell, seed, octaves)
    if bias is not None:
        n = n + bias
    k = len(palette)
    w = np.asarray(weights if weights is not None else [1] * k, float)
    th = np.percentile(n[mask], np.cumsum(w)[:-1] / w.sum() * 100) if mask.any() else np.linspace(0, 1, k + 1)[1:-1]
    idx = quantize(n, th)
    if mode:
        idx = mode_filter(idx, mode, range(k))
    idx = despeckle(idx, 2)
    out = np.zeros(mask.shape + (4,), np.float32)
    out[..., :3] = colorize(idx, palette)
    out[..., 3] = mask
    return out, idx


def rim(layer, mask, color, width=2, seed=0, broken=0.0, side='all'):
    """沿遮罩内侧描一圈边（原版土坡/沙岸的深色边缘）。side='bottom' 只描朝下的边。"""
    e = mask & ~ndi.binary_erosion(mask, iterations=width)
    if side == 'bottom':
        e &= ~shifted(mask, 0, -width - 1)
    if broken > 0:
        n = value_noise(mask.shape, 10, seed, 2)
        e &= n > broken
    blend(layer, rgb(color), e)
    return e


def water(mask, dist_px, base, seed, shallow=None, spot=None, ripple=None, glint=None, spot_spacing=70):
    """平静水面（原版画法）：大面积平涂底色 + 稀疏低对比圆斑 + 近岸浅水带 + “~”形细波纹。

    base 为底色；shallow=(颜色, 宽度像素) 画近岸浅水带；spot 为圆斑颜色。"""
    H, W = mask.shape
    rng = np.random.default_rng(seed)
    out = np.zeros((H, W, 4), np.float32)
    out[..., :3] = rgb(base)
    out[..., 3] = mask
    if spot is not None:
        c = rgb(spot)
        for x, y in scatter(mask & (dist_px > 18), spot_spacing, seed + 3, jitter=0.5):
            r = rng.uniform(7, 19)
            rx, ry = r * rng.uniform(1.0, 1.25), r * rng.uniform(0.8, 1.0)
            x0, x1, y0, y1 = int(x - rx) - 1, int(x + rx) + 2, int(y - ry) - 1, int(y + ry) + 2
            if x0 < 0 or y0 < 0 or x1 > W or y1 > H:
                continue
            yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32) + 0.5
            q = ((xx - x) / rx) ** 2 + ((yy - y) / ry) ** 2
            m = (q <= 1) & mask[y0:y1, x0:x1]
            out[y0:y1, x0:x1, :3][m] = c
    if shallow is not None:
        col, width = shallow
        n = value_noise((H, W), 22, seed + 5, 2)
        band = mask & (dist_px < width * (0.55 + 0.9 * n))
        band = clean(band, 1)
        out[band, :3] = rgb(col)
    if ripple is not None:
        c = rgb(ripple)
        for x, y in scatter(mask & (dist_px > 6), 46, seed + 7, jitter=0.5).astype(int):
            L = int(rng.integers(3, 9))
            # “~”：一段横线，两端各有一个像素上翘/下沉
            pts = [(x + k, y) for k in range(L)]
            pts.append((x - 1, y + 1))
            if rng.random() < 0.6:
                pts.append((x + L, y - 1))
            if rng.random() < 0.35:  # 成对出现
                dx = int(rng.integers(L + 2, L + 6))
                pts += [(x + dx + k, y + 1) for k in range(max(2, L - 3))]
            for px, py in pts:
                if 0 <= px < W and 0 <= py < H and mask[py, px]:
                    out[py, px, :3] = c
    if glint is not None:
        c = rgb(glint)
        for x, y in scatter(mask & (dist_px > 24), 140, seed + 9).astype(int):
            for px, py in ((x, y), (x + 1, y)):
                if 0 <= px < W and mask[py, px]:
                    out[py, px, :3] = c
    return out


def pebbles(mask, seed, sizes=(5, 12), spacing=13, palette=None, density=None, wet=None, moss=None,
            gap_color='#4d4f5c', outline='#3a3a48', contrast=0.7, fill_gap=True, squash=(0.62, 0.78)):
    """卵石滩：自上而下画一层层压扁的椭圆卵石，每颗三色明暗 + 下侧描边。

    density 0..1 场控制疏密（低处露出底色）；wet 为 0..1 场，使卵石变暗变饱和；moss 为布尔场，顶部长苔。"""
    H, W = mask.shape
    rng = np.random.default_rng(seed)
    out = np.zeros((H, W, 4), np.float32)
    out[..., :3] = rgb(gap_color)
    out[..., 3] = mask if fill_gap else 0
    palette = palette or [('#8e9097', '#b2b3b8', '#6a6c77'), ('#998f86', '#bdb3a7', '#72675f'),
                          ('#848d90', '#aab4b5', '#5e696f'), ('#a3a19c', '#c9c6be', '#7a7872'),
                          ('#7d8079', '#a1a59b', '#5b5f5a')]
    pts = scatter(mask, spacing, seed, jitter=0.45)
    pts = pts[np.argsort(pts[:, 1])]  # 自上而下，下面的压住上面的
    oc = rgb(outline)
    for x, y in pts:
        xi, yi = int(x), int(y)
        d = 1.0 if density is None else float(density[yi, xi])
        if rng.random() > d:
            continue
        r = rng.uniform(*sizes) * (0.75 + 0.5 * d)
        rx, ry = r, r * rng.uniform(*squash)
        ang = rng.uniform(-0.35, 0.35)
        x0, x1 = int(x - rx - 2), int(x + rx + 3)
        y0, y1 = int(y - ry - 2), int(y + ry + 3)
        if x0 < 0 or y0 < 0 or x1 > W or y1 > H:
            continue
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32) + 0.5
        dx, dy = xx - x, yy - y
        u = dx * np.cos(ang) + dy * np.sin(ang)
        v = -dx * np.sin(ang) + dy * np.cos(ang)
        q = (u / rx) ** 2 + (v / ry) ** 2
        body = q <= 1
        if body.sum() < 4:
            continue
        base, light, dark = (rgb(c) for c in palette[int(rng.integers(len(palette)))])
        light, dark = base + (light - base) * contrast, base + (dark - base) * contrast
        w = 0.0 if wet is None else float(wet[yi, xi])
        if w > 0:
            base, light, dark = (c * (1 - 0.28 * w) + np.array([0.02, 0.05, 0.08]) * w for c in (base, light, dark))
        # 光从左上：上左偏亮，下右偏暗
        lit = (-u / rx) * 0.45 + (-v / ry) * 0.75 - q * 0.35
        col = np.where(lit[..., None] > 0.42, light, np.where(lit[..., None] < -0.32, dark, base))
        # 下侧 1 像素描边
        er = ndi.binary_erosion(body)
        edge = body & ~er
        low = edge & (v > -ry * 0.25)
        col = np.where(low[..., None], oc, col)
        if moss is not None and moss[yi, xi] and r > 6:
            cap = body & (v < -ry * 0.2) & ~low
            col = np.where(cap[..., None], rgb('#6d7c45') if rng.random() < 0.6 else rgb('#86955a'), col)
        if r > 8 and rng.random() < 0.6:  # 小高光点
            hx, hy = int(x - rx * 0.35), int(y - ry * 0.4)
            if 0 <= hy - y0 < body.shape[0] and 0 <= hx - x0 < body.shape[1] and body[hy - y0, hx - x0]:
                col[hy - y0, hx - x0] = np.minimum(1, light + 0.12)
        sub = out[y0:y1, x0:x1]
        m = body & mask[y0:y1, x0:x1]
        sub[..., :3] = np.where(m[..., None], col, sub[..., :3])
        sub[..., 3] = np.where(m, 1.0, sub[..., 3])
        # 卵石下方一像素的接触阴影
        sh = np.zeros_like(body)
        sh[1:] = body[:-1]
        sh &= ~body & mask[y0:y1, x0:x1]
        sub[..., :3] = np.where(sh[..., None], sub[..., :3] * 0.78, sub[..., :3])
    return out


def dab_ground(mask, base, dabs_pal, seed, cover, radius=(1.6, 3.4), spacing=4.2, edge_rim=None, solid=0.62):
    """原版地面画法：土色底 + 苔草斑块。cover>solid 处铺实心色块，斑块边缘与内部再撒小圆点，
    形成“实心块 + 点状边缘”的效果。dabs_pal 从暗到亮。"""
    H, W = mask.shape
    rng = np.random.default_rng(seed)
    out = np.zeros((H, W, 4), np.float32)
    out[..., :3] = rgb(base) if isinstance(base, str) or np.ndim(base) == 1 else base
    out[..., 3] = mask
    pal = [rgb(c) for c in dabs_pal]
    patch = clean(mask & (cover > solid), 2)
    tone = value_noise((H, W), 40, seed + 1, 2)
    inner = quantize(tone, [0.45, 0.72])
    out[patch, :3] = np.stack([pal[1], pal[2], pal[3] if len(pal) > 3 else pal[2]])[inner[patch]]
    # 斑块外围：越靠近斑块越密的点
    d_out = ndi.distance_transform_edt(~patch)
    for region, sp, lo, hi in ((mask & ~patch & (d_out < 14), spacing, 0, 2), (patch, spacing * 2.2, 2, len(pal) - 1),
                               (mask & ~patch & (cover > 0.25), spacing * 3.2, 0, 1)):
        for x, y in scatter(region, sp, int(rng.integers(1 << 30)), jitter=0.6):
            xi, yi = int(x), int(y)
            if region is not patch and rng.random() < d_out[yi, xi] / 16:
                continue
            r = rng.uniform(*radius)
            k = int(rng.integers(lo, hi + 1))
            x0, x1, y0, y1 = int(x - r), int(x + r) + 2, int(y - r), int(y + r) + 2
            if x0 < 0 or y0 < 0 or x1 > W or y1 > H:
                continue
            yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32) + 0.5
            m = ((xx - x) ** 2 + (yy - y) ** 2 <= r * r) & mask[y0:y1, x0:x1]
            out[y0:y1, x0:x1, :3][m] = pal[k]
    if edge_rim is not None:
        e = mask & ~ndi.binary_erosion(mask, iterations=2)
        out[e, :3] = rgb(edge_rim)
    return out


def boulder_wall(face, seed, palette, outline='#2f2b3a', row_h=(12, 40), width=(16, 72), moss=None, moss_pal=('#55663c', '#6f8048'), cracks=True):
    """崖面由一排排圆角石块堆成（参考原版博丽神社的岩壁）。

    face 为立面遮罩；palette 为 (暗, 中, 亮, 高光)。每块石头左上受光，块间留深色缝。"""
    H, W = face.shape
    rng = np.random.default_rng(seed)
    out = np.zeros((H, W, 4), np.float32)
    out[..., :3] = rgb(outline)
    out[..., 3] = face
    ys, xs = np.nonzero(face)
    if len(ys) == 0:
        return out
    dark, mid, light, hi = (rgb(c) for c in palette)
    oc = rgb(outline)
    y = ys.min()
    while y < ys.max():
        h = int(rng.integers(*row_h))
        x = xs.min() - int(rng.integers(0, 20))
        while x < xs.max():
            w = int(rng.integers(*width))
            hh = int(h * rng.uniform(0.7, 1.5))
            bx0, by0 = x + int(rng.integers(-3, 4)), y + int(rng.integers(-6, 5))
            bw, bh = w + int(rng.integers(0, 8)), hh + int(rng.integers(0, 6))
            x0, y0, x1, y1 = max(0, bx0), max(0, by0), min(W, bx0 + bw), min(H, by0 + bh)
            if x1 - x0 > 4 and y1 - y0 > 4:
                yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32) + 0.5
                cx, cy, rx, ry = bx0 + bw / 2, by0 + bh / 2, bw / 2, bh / 2
                # 圆角矩形：超椭圆
                q = np.abs((xx - cx) / rx) ** 3.2 + np.abs((yy - cy) / ry) ** 3.2
                body = (q <= 1) & face[y0:y1, x0:x1]
                if body.sum() > 10:
                    u, v = (xx - cx) / rx, (yy - cy) / ry
                    lit = -u * 0.35 - v * 0.8 - q * 0.25 + rng.normal(0, 0.08)
                    col = np.where(lit[..., None] > 0.45, hi, np.where(lit[..., None] > 0.05, light,
                                   np.where(lit[..., None] > -0.45, mid, dark)))
                    edge = body & ~ndi.binary_erosion(body)
                    col = np.where((edge & (v > 0.1))[..., None], oc, col)
                    if cracks and bw > 30 and rng.random() < 0.5:  # 竖向裂纹
                        cxk = int(rng.integers(int(bw * 0.3), int(bw * 0.7)))
                        for yk in range(int(bh * 0.15), int(bh * rng.uniform(0.5, 0.9))):
                            xk = cxk + int(round(np.sin(yk * 0.35) * 1.2))
                            if 0 <= yk + by0 - y0 < body.shape[0] and 0 <= xk + bx0 - x0 < body.shape[1] and body[yk + by0 - y0, xk + bx0 - x0]:
                                col[yk + by0 - y0, xk + bx0 - x0] = dark * 0.8
                    if moss is not None:
                        mm = body & (v < -0.45) & moss[y0:y1, x0:x1]
                        col = np.where(mm[..., None], rgb(moss_pal[int(rng.integers(2))]), col)
                    sub = out[y0:y1, x0:x1]
                    sub[..., :3] = np.where(body[..., None], col, sub[..., :3])
            x += w - int(rng.integers(2, 6))
        y += h - int(rng.integers(2, 5))
    return out


def fog_bands(shape, Y, seed, start, end, levels=(0.1, 0.2, 0.32, 0.46, 0.6), color=('#e6eff2', '#cfdbe4'),
              stretch=8, cell=34):
    """横向雾带：噪声沿 x 拉长 stretch 倍，按纵向密度分级成几档透明度，边缘是平滑长弧。

    start/end 为雾开始与最浓处的世界 y；返回 RGBA。"""
    H, W = shape
    n = value_noise((H, W // stretch + 2), cell, seed, 4)
    n = np.repeat(n, stretch, axis=1)[:, :W]
    n = ndi.uniform_filter(n, (1, stretch * 2))
    dens = np.clip((Y - start) / max(1e-6, end - start), 0, 1)
    f = n * 0.55 + dens * 0.9 - 0.42
    k = len(levels)
    th = np.linspace(0.08, 0.72, k)
    lv = quantize(f, th)
    lv = mode_filter(lv, 9, range(k + 1))
    alpha = np.concatenate([[0.0], np.asarray(levels, np.float32)])[lv]
    out = np.zeros((H, W, 4), np.float32)
    hi = rgb(color[0])
    lo = rgb(color[1])
    out[..., :3] = np.where((lv >= k - 1)[..., None], hi, lo)
    out[..., 3] = alpha
    return out


def drift_mask(mask, seed, cell=60, cover=0.45):
    """在遮罩内取成片的花丛/草丛区域（低频噪声阈值）。"""
    n = value_noise(mask.shape, cell, seed, 3)
    return clean(mask & (n > 1 - cover), 2)

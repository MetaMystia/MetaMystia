"""生图结果的后处理：分块细化后的拼接、对齐、配色校正，以及物件图抠图。

约定同 core：图像为 float32 RGB，0..1；遮罩为 bool。
"""
import numpy as np
from PIL import Image
from scipy import ndimage as ndi


def load_rgb(path, size=None):
    im = Image.open(path).convert('RGB')
    if size and im.size != tuple(size):
        down = im.size[0] > size[0]
        im = im.resize(tuple(size), Image.Resampling.LANCZOS if not down else Image.Resampling.LANCZOS)
    return np.asarray(im).astype(np.float32) / 255


def save_rgb(arr, path):
    Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8)).save(path)


# ---------------------------------------------------------------- 分块

def windows(W, H, win_w, win_h, cols, rows):
    """均匀铺满画布的重叠窗口 (x0, y0, x1, y1)。"""
    xs = np.linspace(0, W - win_w, cols).round().astype(int)
    ys = np.linspace(0, H - win_h, rows).round().astype(int)
    return [(int(x), int(y), int(x + win_w), int(y + win_h)) for y in ys for x in xs]


def gray(a):
    return a[..., 0] * 0.3 + a[..., 1] * 0.59 + a[..., 2] * 0.11


def register(ref, img, search=8, gain=0.02):
    """整数平移配准：返回 (dx, dy)，使 img 平移后与 ref 最吻合。用梯度做相关，避免颜色差影响。
    相关度比不平移高出不到 gain 时返回 (0, 0)：水面、林冠这类纹理弱的区域容易配错。"""
    def feat(a):
        g = ndi.gaussian_filter(gray(a), 1.2)
        f = np.hypot(ndi.sobel(g, 0), ndi.sobel(g, 1))
        return (f - f.mean()) / (f.std() + 1e-6)
    fr, fi = feat(ref), feat(img)
    H, W = fr.shape
    m = search
    core = fr[m:H - m, m:W - m]

    def score(dx, dy):
        return float((core * fi[m - dy:H - m - dy, m - dx:W - m - dx]).mean())
    base = score(0, 0)
    best, arg = base, (0, 0)
    for dy in range(-m, m + 1):
        for dx in range(-m, m + 1):
            sc = score(dx, dy)
            if sc > best:
                best, arg = sc, (dx, dy)
    return arg if best - base > gain else (0, 0)


def shift_img(img, dx, dy):
    """img 内容平移 (dx, dy)：out[y, x] = img[y - dy, x - dx]；空出的边复制最近的边缘像素。"""
    H, W = img.shape[:2]
    p = np.pad(img, ((abs(dy), abs(dy)), (abs(dx), abs(dx)), (0, 0)), mode='edge')
    y0, x0 = abs(dy) - dy, abs(dx) - dx
    return p[y0:y0 + H, x0:x0 + W]


def color_match(img, ref, block=32, sigma=24, limit=0.3):
    """低频配色校正：保留 img 的细节，低频颜色对齐 ref。
    差值按块取中位数再平滑，并限幅，避免两图内容不同（多了块石头）的地方被抹出色斑。"""
    d = ref - img
    H, W = d.shape[:2]
    gh, gw = -(-H // block), -(-W // block)
    pad = np.pad(d, ((0, gh * block - H), (0, gw * block - W), (0, 0)), mode='edge')
    med = np.median(pad.reshape(gh, block, gw, block, 3), axis=(1, 3))
    up = ndi.zoom(med, (block, block, 1), order=1)[:H, :W]
    up = np.clip(ndi.gaussian_filter(up, (sigma, sigma, 0)), -limit, limit)
    return np.clip(img + up, 0, 1)


# ---------------------------------------------------------------- 最小代价接缝

def _seam_vertical(cost):
    """cost (H, w)：从上到下的最小代价路径，返回每行的列号。"""
    H, w = cost.shape
    acc = cost.copy()
    back = np.zeros((H, w), np.int8)
    for y in range(1, H):
        prev = acc[y - 1]
        l = np.r_[np.inf, prev[:-1]]
        r = np.r_[prev[1:], np.inf]
        stack = np.stack([l, prev, r])
        k = stack.argmin(0)
        acc[y] += stack[k, np.arange(w)]
        back[y] = k - 1
    path = np.zeros(H, int)
    path[-1] = int(acc[-1].argmin())
    for y in range(H - 1, 0, -1):
        path[y - 1] = path[y] + back[y, path[y]]
    return path


def quilt(canvas, filled, patch, box, feather=2):
    """把 patch 放进 canvas 的 box；与已填区域重叠处沿最小差异路径切开，再羽化几像素。"""
    x0, y0, x1, y1 = box
    cur = canvas[y0:y1, x0:x1]
    have = filled[y0:y1, x0:x1]
    H, W = have.shape
    take = np.ones((H, W), bool)
    if have.any():
        diff = ndi.gaussian_filter(np.abs(patch - cur).sum(-1), 1.0)
        cols = np.nonzero(have.all(0))[0]
        rows = np.nonzero(have.all(1))[0]
        # 左侧重叠带：竖向接缝，接缝左边保留原画
        left = cols[cols < W // 2]
        if len(left):
            w = left.max() + 1
            p = _seam_vertical(diff[:, :w])
            take &= np.arange(W)[None, :] >= p[:, None]
        top = rows[rows < H // 2]
        if len(top):
            h = top.max() + 1
            p = _seam_vertical(diff[:h, :].T)
            take &= np.arange(H)[:, None] >= p[None, :]
        take |= ~have
    a = take.astype(np.float32)
    if feather:
        a = ndi.gaussian_filter(a, feather)
        a = np.where(~have, 1.0, a)
    canvas[y0:y1, x0:x1] = cur * (1 - a[..., None]) + patch * a[..., None]
    filled[y0:y1, x0:x1] = True


# ---------------------------------------------------------------- 物件图抠图

def key_out(img, key, t_bg=0.22, t_mix=0.45, grow=2, min_area=400):
    """纯色底物件图 → (前景遮罩, 连通块切片列表)。

    与底色距离 < t_bg 为底；t_bg..t_mix 之间且贴着底的像素视为混色边，一并去掉。
    """
    k = np.asarray(key, np.float32)
    d = np.sqrt(((img - k) ** 2).sum(-1))
    bg = d < t_bg
    mix = (d < t_mix) & ndi.binary_dilation(bg, iterations=grow)
    fg = ~(bg | mix)
    fg = ndi.binary_opening(fg, iterations=1)
    lab, n = ndi.label(ndi.binary_dilation(fg, iterations=5))
    out = []
    for i, s in enumerate(ndi.find_objects(lab)):
        m = (lab[s] == i + 1) & fg[s]
        if m.sum() >= min_area:
            out.append((s, m))
    return fg, out


def despill(rgb, mask, key, band=4):
    """去掉边缘一圈的底色溢色：绿底压低多出的绿，品红底压低多出的红蓝。"""
    edge = mask & ~ndi.binary_erosion(mask, iterations=band)
    out = rgb.copy()
    r, g, b = out[..., 0], out[..., 1], out[..., 2]
    k = np.asarray(key, np.float32)
    if k[1] > k[0] and k[1] > k[2]:
        s = np.maximum(g - np.maximum(r, b), 0)
        out[..., 1] = np.where(edge, g - s, g)
    else:
        s = np.maximum(np.minimum(r, b) - g, 0)
        out[..., 0] = np.where(edge, r - s, r)
        out[..., 2] = np.where(edge, b - s, b)
    return out


def spill_pixels(rgb, mask, key, band=4, t=0.12):
    """带底色色相的像素：边缘一圈里稍有偏色即算，内部要偏得更明显（底色从枝叶缝里透出来的点）。"""
    edge = mask & ~ndi.binary_erosion(mask, iterations=band)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    k = np.asarray(key, np.float32)
    if k[1] > k[0] and k[1] > k[2]:
        s = g - np.maximum(r, b)
    else:
        s = np.minimum(r, b) - g
    return (edge & (s > t)) | (mask & (s > 2 * t))


def sprite_from(img, sl, mask, height_px=None, width_px=None, colors=None, key=None, kill_spill=True):
    """切出一个物件，按目标高度（或宽度）缩放，返回 RGBA uint8 数组。预乘缩放避免底色渗边。"""
    rgb = img[sl]
    if key is not None:
        if kill_spill:
            mask = mask & ~spill_pixels(rgb, mask, key)
        rgb = despill(rgb, mask, key)
    a = mask.astype(np.float32)
    h, w = a.shape
    if height_px:
        s = height_px / h
    elif width_px:
        s = width_px / w
    else:
        s = 1.0
    W, H = max(1, round(w * s)), max(1, round(h * s))
    pm = np.dstack([rgb * a[..., None], a])
    im = Image.fromarray((pm * 255 + 0.5).astype(np.uint8), 'RGBA').resize((W, H), Image.Resampling.BOX)
    q = np.asarray(im).astype(np.float32) / 255
    al = q[..., 3]
    col = np.where(al[..., None] > 0, q[..., :3] / np.maximum(al[..., None], 1e-6), 0)
    if colors:
        pim = Image.fromarray((np.clip(col, 0, 1) * 255 + 0.5).astype(np.uint8))
        col = np.asarray(pim.quantize(colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert('RGB')).astype(np.float32) / 255
    alpha = (al > 0.5).astype(np.float32)
    return (np.dstack([np.clip(col, 0, 1), alpha]) * 255 + 0.5).astype(np.uint8)

"""导出：整图切片、去重、打图集，生成 ResourceEx dayMaps（formatVersion 1）配置与 ZIP。"""
import hashlib
import io
import json
import math
import wave
import zipfile

import numpy as np
from PIL import Image

from .core import PPU

ATLAS = 2048


class Atlas:
    """货架式打包到 2048 宽的图集页。切片 rect 在 finalize 时按页的实际高度换算成左下原点。"""

    def __init__(self, prefix, size=ATLAS):
        self.prefix, self.size = prefix, size
        self.pages = []      # PIL images
        self.used = []       # 每页已用高度
        self.cursor = None   # (page, x, y, shelf_h)

    def add(self, im):
        w, h = im.size
        if w > self.size or h > self.size:
            raise ValueError(f'sprite too large for atlas: {w}x{h}')
        if self.cursor is None:
            self._new_page()
        page, x, y, sh = self.cursor
        if x + w > self.size:
            x, y, sh = 0, y + sh, 0
        if y + h > self.size:
            self._new_page()
            page, x, y, sh = self.cursor
        self.pages[page].paste(im, (x, y))
        self.used[page] = max(self.used[page], y + h)
        self.cursor = (page, x + w, y, max(sh, h))
        return (page, x, y, w, h)

    def _new_page(self):
        self.pages.append(Image.new('RGBA', (self.size, self.size), (0, 0, 0, 0)))
        self.used.append(0)
        self.cursor = (len(self.pages) - 1, 0, 0, 0)

    def finalize(self, tiles, folder):
        """裁掉每页底部空白，把 tiles 中的 handle 换成 image 路径与左下原点 rect。返回 {路径: 图片}。"""
        heights = [max(4, (u + 3) // 4 * 4) for u in self.used]
        for t in tiles:
            if 'handle' in t:
                page, x, y, w, h = t.pop('handle')
                t['image'] = f'{folder}/{self.prefix}_{page}.png'
                t['rect'] = [x, heights[page] - y - h, w, h]
        return {f'{folder}/{self.prefix}_{i}.png': im.crop((0, 0, self.size, heights[i]))
                for i, im in enumerate(self.pages)}


def slice_layer(img, frame, chunk, atlas, key_prefix, skip_empty=True):
    """把整张图层图片按 chunk 格切片（chunk*48 像素），相同内容只存一份。返回 (tiles, cells)。"""
    arr = np.asarray(img)
    size = chunk * frame.ppu
    tiles, cells, seen = [], [], {}
    rows, cols = arr.shape[0] // size, arr.shape[1] // size
    for j in range(rows):
        for i in range(cols):
            block = arr[j * size:(j + 1) * size, i * size:(i + 1) * size]
            if skip_empty and block[..., 3].max() == 0:
                continue
            h = hashlib.blake2b(block.tobytes(), digest_size=16).hexdigest()
            if h not in seen:
                handle = atlas.add(Image.fromarray(block, 'RGBA'))
                key = f'{key_prefix}{len(seen)}'
                seen[h] = key
                tiles.append(dict(key=key, handle=handle, pivot=[0, 0], pixelsPerUnit=frame.ppu))
            x = int(round(frame.x0)) + i * chunk
            y = int(round(frame.y1)) - (j + 1) * chunk
            cells.append(dict(x=x, y=y, tile=seen[h]))
    return tiles, cells


def sprite_tile(spr, atlas, key):
    """物件精灵 → 切片定义（pivot 换算成左下原点的归一化坐标）。"""
    im = spr.image()
    handle = atlas.add(im)
    px, py = spr.pivot
    pivot = [round(min(1, max(0, px / im.width)), 5), round(min(1, max(0, (im.height - py) / im.height)), 5)]
    return dict(key=key, handle=handle, pivot=pivot, pixelsPerUnit=PPU)


def camera_bounds(x0, y0, x1, y1, half_w=40 / 3, half_h=7.5, offset_y=0.5, margin=1 / 32):
    """原生相机中心范围：按 16:9、正交半高 7.5、相机 Y 偏移 +0.5 计算（见 docs/resourceex-day-map-research.md）。"""
    return [round(x0 + half_w + margin, 5), round(y0 + half_h - offset_y + margin, 5),
            round(x1 - half_w - margin, 5), round(y1 - half_h - offset_y - margin, 5)]


def synth_bgm(seconds=48, rate=22050, seed=7):
    """占位环境乐：低音铺底 + 五声音阶拨弦（Karplus-Strong）+ 轻微水声。返回 WAV 字节。"""
    rng = np.random.default_rng(seed)
    n = seconds * rate
    t = np.arange(n) / rate
    out = np.zeros(n, np.float64)
    # 铺底：D2 与 A2，缓慢起伏；首尾相接无缝（周期整除）
    for f, a in ((73.42, 0.10), (110.0, 0.06), (146.83, 0.035)):
        out += a * np.sin(2 * math.pi * f * t) * (0.75 + 0.25 * np.sin(2 * math.pi * t / seconds * 3))
    # 水声：低通噪声，振幅缓慢变化
    noise = rng.normal(0, 1, n)
    k = 60
    noise = np.convolve(noise, np.ones(k) / k, mode='same')
    out += 0.05 * noise * (0.6 + 0.4 * np.sin(2 * math.pi * t / seconds * 5 + 1))
    # 拨弦：D 小调五声 D F G A C
    scale = [293.66, 349.23, 392.0, 440.0, 523.25, 587.33]
    pos = 1.0
    while pos < seconds - 4:
        f = scale[int(rng.integers(len(scale)))]
        L = int(rate / f)
        buf = rng.uniform(-1, 1, L)
        dur = int(rate * 3.2)
        y = np.zeros(dur)
        for i in range(dur):
            y[i] = buf[i % L]
            buf[i % L] = 0.996 * 0.5 * (buf[i % L] + buf[(i + 1) % L])
        s = int(pos * rate)
        out[s:s + dur] += 0.22 * y[:max(0, min(dur, n - s))]
        pos += rng.uniform(1.6, 4.2)
    # 首尾 0.5 秒交叉淡化，保证循环无爆音
    fade = int(rate * 0.5)
    w = np.linspace(0, 1, fade)
    out[:fade] = out[:fade] * w + out[-fade:] * (1 - w)
    out = out[:n - fade]
    out = out / (np.abs(out).max() + 1e-9) * 0.42
    pcm = (out * 32767).astype('<i2')
    buf = io.BytesIO()
    with wave.open(buf, 'wb') as wav:
        wav.setparams((1, 2, rate, 0, 'NONE', 'not compressed'))
        wav.writeframes(pcm.tobytes())
    return buf.getvalue()


def write_zip(path, config, files):
    """files: {包内路径: PIL.Image | bytes}。"""
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as z:
        z.writestr('ResourceEx.json', json.dumps(config, ensure_ascii=False, indent=1))
        for name, data in files.items():
            if isinstance(data, Image.Image):
                b = io.BytesIO()
                data.save(b, 'PNG', optimize=True)
                data = b.getvalue()
            z.writestr(name, data)

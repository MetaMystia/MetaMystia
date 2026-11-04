"""导出：图层大图切块、物件模板去重，生成 ResourceEx dayMaps（formatVersion 3）配置与 ZIP。"""
import hashlib
import io
import json
import math
import re
import wave
import zipfile

import numpy as np
from PIL import Image

from .core import PPU

CHUNK = 1024
FORMAT_VERSION = 3


def chunk_image(img, folder):
    """RGBA 图片 → 切块图片配置与块文件 {路径: 图片}；全透明的块不输出。"""
    w, h = img.size
    files, chunks = {}, []
    for r in range(math.ceil(h / CHUNK)):
        for c in range(math.ceil(w / CHUNK)):
            tile = img.crop((c * CHUNK, r * CHUNK, min(w, (c + 1) * CHUNK), min(h, (r + 1) * CHUNK)))
            if tile.getchannel('A').getbbox() is not None:
                files[f'{folder}/{c}_{r}.png'] = tile
                chunks.append([c, r])
    return dict(width=w, height=h, dir=folder, chunks=chunks), files


def canvas_layer(name, img, left, bottom, folder, sorting_layer='Background', sorting_order=0, color=None):
    """整张图层 → 画布层配置与块文件。

    img 为 RGBA；(left, bottom) 是图片左下角的世界像素坐标（世界坐标 × PPU）。
    透明边缘先裁掉。返回 (layer, files)，图层全空时 layer 为 None。
    """
    bbox = img.getchannel('A').getbbox()
    if bbox is None:
        return None, {}
    x0, y0, x1, y1 = bbox
    image, files = chunk_image(img.crop(bbox), folder)
    layer = dict(name=name, sortingLayer=sorting_layer, sortingOrder=sorting_order, x=left + x0, y=bottom + img.height - y1, image=image)
    if color is not None:
        layer['color'] = color
    return layer, files


def safe_key(name):
    return re.sub(r'[^0-9A-Za-z_-]+', '_', name).strip('_') or 'prefab'


class Prefabs:
    """物件模板：图片、锚点、排序与碰撞都相同的物件共用一份。"""

    def __init__(self, folder):
        self.folder = folder
        self.items, self.files, self._by_signature = [], {}, {}

    def add(self, name, img, pivot, sorting_layer='Character', sorting_order=0, sort_by_y=True, colliders=(), ppu=PPU):
        """img 为 RGBA，pivot 为左下原点的归一化锚点；透明边缘先裁掉。返回模板 key。"""
        bbox = img.getchannel('A').getbbox() or (0, 0, 1, 1)
        x0, y0, x1, y1 = bbox
        px, py = pivot[0] * img.width - x0, pivot[1] * img.height - (img.height - y1)
        img = img.crop(bbox)
        pivot = [round(px / img.width, 6), round(py / img.height, 6)]
        settings = json.dumps([img.size, pivot, sorting_layer, sorting_order, sort_by_y, list(colliders), ppu], sort_keys=True)
        signature = hashlib.blake2b(img.tobytes() + settings.encode(), digest_size=16).hexdigest()
        if signature in self._by_signature:
            return self._by_signature[signature]
        base = key = safe_key(name)
        n = 1
        while any(p['key'] == key for p in self.items):
            n += 1
            key = f'{base}_{n}'
        image, files = chunk_image(img, f'{self.folder}/{key}')
        self.files.update(files)
        self.items.append(dict(key=key, image=image, pivot=pivot, pixelsPerUnit=ppu, sortingLayer=sorting_layer,
                               sortingOrder=sorting_order, sortByY=sort_by_y, colliders=list(colliders)))
        self._by_signature[signature] = key
        return key


def sprite_pivot(spr):
    """dayart.sprite.Sprite 的锚点（左上原点像素）→ 左下原点的归一化锚点。"""
    px, py = spr.pivot
    return [px / spr.w, (spr.h - py) / spr.h]


def rect_polygon(x0, y0, x1, y1):
    return [[x0, y0], [x1, y0], [x1, y1], [x0, y1]]


def camera_bounds(x0, y0, x1, y1, half_w=40 / 3, half_h=7.5, offset_y=0.5, margin=1 / 32):
    """原生相机中心范围多边形：按 16:9、正交半高 7.5、相机 Y 偏移 +0.5 计算（见 docs/resourceex-day-map-research.md）。"""
    return rect_polygon(round(x0 + half_w + margin, 5), round(y0 + half_h - offset_y + margin, 5),
                        round(x1 - half_w - margin, 5), round(y1 - half_h - offset_y - margin, 5))


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

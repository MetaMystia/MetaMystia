"""物件图（纯色底）切成独立精灵。

  python -m maps.sanzu.sprites cut   <sheet.png> <key:#rrggbb> <outdir>   # 切出全部连通块，出带编号的总览
  python -m maps.sanzu.sprites build <spec.json> <sheetdir> <outdir>     # 按配置命名、缩放到游戏尺寸

spec.json: {"<sheet>": {"key": "#00ff00", "items": {"<编号>": ["名字", 高度像素] 或 ["名字", 高度像素, "w"]}}}
第三项为 "w" 时按宽度缩放。编号来自 cut 的总览图。
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

from dayart import paintover as po
from dayart.core import rgb


def components(sheet, key):
    img = po.load_rgb(sheet)
    _, comps = po.key_out(img, rgb(key))
    comps.sort(key=lambda c: (c[0][0].start // 120, c[0][1].start))
    return img, comps


def cut(sheet, key, out):
    os.makedirs(out, exist_ok=True)
    img, comps = components(sheet, key)
    ov = Image.fromarray((img * 255).astype(np.uint8)).convert('RGB')
    d = ImageDraw.Draw(ov)
    for i, (s, m) in enumerate(comps):
        d.rectangle([s[1].start, s[0].start, s[1].stop, s[0].stop], outline=(255, 0, 0), width=3)
        d.rectangle([s[1].start, s[0].start, s[1].start + 34, s[0].start + 22], fill=(0, 0, 0))
        d.text((s[1].start + 4, s[0].start + 4), str(i), fill=(255, 255, 255))
    ov.save(os.path.join(out, os.path.basename(sheet).replace('.png', '_idx.png')))
    print(len(comps), 'components')


def build(spec_path, sheetdir, out):
    os.makedirs(out, exist_ok=True)
    spec = json.load(open(spec_path))
    made = []
    for sheet, cfg in spec.items():
        img, comps = components(os.path.join(sheetdir, sheet + '.png'), cfg['key'])
        for idx, item in cfg['items'].items():
            name, size = item[0], item[1]
            by_w = len(item) > 2 and item[2] == 'w'
            s, m = comps[int(idx)]
            arr = po.sprite_from(img, s, m, height_px=None if by_w else size, width_px=size if by_w else None, key=rgb(cfg['key']), kill_spill=name not in cfg.get('keep_hue', []))
            Image.fromarray(arr, 'RGBA').save(os.path.join(out, name + '.png'))
            made.append((name, arr.shape[1], arr.shape[0]))
    # 总览：游戏尺寸 2 倍放大
    pad, x, y, rowh, W = 8, 8, 8, 0, 1400
    pos = []
    ims = [Image.open(os.path.join(out, n + '.png')) for n, _, _ in made]
    for im in ims:
        w, h = im.size[0] * 2, im.size[1] * 2
        if x + w + pad > W:
            x, y, rowh = 8, y + rowh + pad + 14, 0
        pos.append((x, y))
        x += w + pad
        rowh = max(rowh, h)
    sheet = Image.new('RGBA', (W, y + rowh + 24), (150, 145, 130, 255))
    d = ImageDraw.Draw(sheet)
    for im, (n, _, _), p in zip(ims, made, pos):
        sheet.alpha_composite(im.resize((im.size[0] * 2, im.size[1] * 2), Image.Resampling.NEAREST), (p[0], p[1] + 12))
        d.text(p, n, fill=(20, 20, 20))
    sheet.save(os.path.join(out, '_overview.png'))
    print(len(made), 'sprites')


if __name__ == '__main__':
    if sys.argv[1] == 'cut':
        cut(sys.argv[2], sys.argv[3], sys.argv[4])
    else:
        build(sys.argv[2], sys.argv[3], sys.argv[4])

"""贴图预览：把目录中的 PNG 排在暖色（店内）与冷色两种底色上，便于检查透明边缘与对比度。

Usage: python scripts/contact_sheet.py <texture_dir> <out.png> [name ...]
"""
import glob
import os
import sys

from PIL import Image, ImageDraw


def main(tex_dir, out, names):
    files = sorted(glob.glob(os.path.join(tex_dir, '*.png')))
    if names:
        files = [f for f in files if os.path.splitext(os.path.basename(f))[0] in names]
    cell = 220
    sheet = Image.new('RGBA', (cell * len(files), cell * 2 + 16), (0, 0, 0, 255))
    d = ImageDraw.Draw(sheet)
    for i, f in enumerate(files):
        im = Image.open(f).convert('RGBA')
        im.thumbnail((cell - 12, cell - 12))
        for row, bg in enumerate(((58, 42, 36), (30, 40, 70))):
            box = Image.new('RGBA', (cell, cell), bg + (255,))
            box.alpha_composite(im, ((cell - im.width) // 2, (cell - im.height) // 2))
            sheet.paste(box, (i * cell, row * cell))
        d.text((i * cell + 4, cell * 2 + 2), os.path.basename(f), fill=(255, 255, 255, 255))
    sheet.save(out)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], sys.argv[3:])

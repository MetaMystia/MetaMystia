"""样张：把一组精灵排在中性底色上并放大，用于逐个审阅道具。"""
from PIL import Image, ImageDraw


def contact_sheet(items, path, bg=(118, 112, 98), scale=2, cols=8, pad=12):
    """items: [(名称, Sprite)]。每格按最大尺寸排布，底部画脚点十字。"""
    cw = max(s.w for _, s in items) + pad * 2
    ch = max(s.h for _, s in items) + pad * 2 + 10
    rows = (len(items) + cols - 1) // cols
    sheet = Image.new('RGBA', (cw * cols, ch * rows), bg + (255,))
    d = ImageDraw.Draw(sheet)
    for i, (name, s) in enumerate(items):
        cx, cy = (i % cols) * cw, (i // cols) * ch
        x = cx + (cw - s.w) // 2
        y = cy + ch - 12 - s.h
        sheet.alpha_composite(s.image(), (int(x), int(y)))
        fx, fy = int(x + s.pivot[0]), int(y + s.pivot[1])
        d.line([(fx - 3, fy), (fx + 3, fy)], fill=(255, 0, 255, 255))
        d.text((cx + 3, cy + ch - 11), name[:18], fill=(20, 20, 20, 255))
    sheet = sheet.resize((sheet.width * scale, sheet.height * scale), Image.NEAREST)
    sheet.save(path)

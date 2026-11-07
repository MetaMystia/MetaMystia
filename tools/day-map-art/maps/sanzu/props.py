"""三途川专属道具。尺寸单位为像素（48 像素 = 1 格）。"""
import math
import sys

import numpy as np

sys.path.insert(0, __file__.rsplit('/maps/', 1)[0])
from dayart.core import shift
from dayart.props import canopy, paper_lantern
from dayart.sprite import Sprite, ascii_sprite

WOOD = ('#b08e66', '#8d6c4c', '#674d37', '#45332a')         # 亮、中、暗、描边
GREY_WOOD = ('#a39a8c', '#81786b', '#5e574e', '#3e3832')
STONE = ('#c6c2b7', '#a6a298', '#848077', '#5f5c56', '#3f3c39')  # 高光、亮、中、暗、描边
RED = ('#e0453f', '#c0303a', '#8c1d29')
THATCH = ('#c4ab72', '#a68c55', '#7e683c', '#55452a')


def _planks_h(s, x, y, w, h, pal, gap=6, seed=0, grain=True):
    """横铺木板：每块板一条暗缝，偶尔木纹。"""
    rng = np.random.default_rng(seed)
    light, mid, dark, out = pal
    s.rect(x, y, w, h, mid)
    for yy in range(int(y), int(y + h), gap):
        s.rect(x, yy, w, 1, light)
        s.rect(x, yy + gap - 1, w, 1, dark)
        if grain:
            for _ in range(int(w / 16)):
                gx = x + rng.uniform(2, w - 8)
                s.rect(gx, yy + int(rng.integers(2, max(3, gap - 2))), int(rng.integers(3, 9)), 1, dark, 0.5)


def _planks_v(s, x, y, w, h, pal, gap=6, seed=0):
    rng = np.random.default_rng(seed)
    light, mid, dark, out = pal
    s.rect(x, y, w, h, mid)
    for xx in range(int(x), int(x + w), gap):
        s.rect(xx, y, 1, h, light)
        s.rect(xx + gap - 1, y, 1, h, dark)
        for _ in range(int(h / 18)):
            s.rect(xx + int(rng.integers(2, max(3, gap - 2))), y + rng.uniform(2, h - 8), 1, int(rng.integers(3, 8)), dark, 0.5)


def _post(s, x, y, w, h, pal):
    light, mid, dark, out = pal
    s.rect(x, y, w, h, mid)
    s.rect(x, y, 1, h, light)
    s.rect(x + w - 2, y, 2, h, dark)


def jizo(seed=0, bib=RED, hat=True):
    """地藏：圆头、合掌、红色围兜，可戴红色毛线帽。约 22×34 像素。"""
    rng = np.random.default_rng(seed)
    hi, l, m, d, o = STONE
    s = Sprite(24, 36, (12, 35))
    # 莲座
    s.poly([(3, 35), (5, 30), (19, 30), (21, 35)], m)
    s.rect(5, 30, 14, 1, l)
    # 身体（袈裟）
    s.poly([(5, 30), (6, 16), (18, 16), (19, 30)], l)
    s.rect(15, 17, 4, 13, m)
    s.line([(8, 22), (12, 27), (16, 22)], m)
    # 头
    s.ellipse(12, 11, 6, 6, l)
    s.ellipse(10, 9, 3, 3, hi)
    s.px(10, 12, d)
    s.px(14, 12, d)
    s.rect(11, 14, 3, 1, m)
    # 合掌
    s.rect(11, 21, 3, 4, hi)
    # 红围兜
    s.poly([(6, 16), (12, 24), (18, 16), (17, 15), (7, 15)], bib[1])
    s.line([(7, 15), (17, 15)], bib[0])
    s.line([(6, 16), (12, 24), (18, 16)], bib[2])
    if hat:
        s.poly([(6, 8), (8, 4), (16, 4), (18, 8)], bib[1])
        s.rect(6, 7, 13, 2, bib[2])
        s.rect(11, 2, 3, 2, bib[0])
    s.outline(o)
    if rng.random() < 0.5:  # 青苔
        for _ in range(3):
            s.px(int(rng.integers(6, 18)), int(rng.integers(28, 33)), '#6f7d45')
    return s


def jizo_row(seed=0):
    """六地藏：长条石台上并排六尊，前面摆小石子与风车。约 172×52 像素。"""
    hi, l, m, d, o = STONE
    W, H = 176, 58
    s = Sprite(W, H, (W / 2, H - 2))
    # 石台：顶面 + 前侧面
    s.rect(4, 34, W - 8, 8, l)
    s.rect(4, 42, W - 8, 12, m)
    s.rect(4, 52, W - 8, 2, d)
    for k in range(6):
        s.rect(4 + k * 28 + 27, 42, 1, 10, d)
    s.rect(4, 34, W - 8, 1, hi)
    for k in range(6):
        j = jizo(seed + k, hat=(k % 2 == 0))
        s.paste(j, 10 + k * 27, 6)
    rng = np.random.default_rng(seed)
    for k in range(14):  # 供奉的小石子
        x = int(rng.integers(8, W - 10))
        s.ellipse(x, 38, 2, 1.5, '#9a968e')
        s.px(x - 1, 37, '#c3bfb6')
    s.outline(o)
    return s


def boat(seed=0):
    """小町的渡船：是非曲直厅统一配发，朴素无帆，船尾一支橹。约 250×86 像素（含船身在水中的部分）。"""
    W, H = 252, 92
    s = Sprite(W, H, (W / 2, H - 14))
    light, mid, dark, out = GREY_WOOD
    # 船身外形（俯视略带侧面）：上缘为船舷，下半为船侧板
    top = [(14, 34), (40, 22), (210, 22), (238, 30), (246, 40), (236, 50), (208, 58), (40, 58), (14, 48)]
    s.poly(top, mid)
    # 船内底板
    inner = [(30, 33), (48, 28), (204, 28), (228, 35), (232, 42), (222, 48), (204, 52), (48, 52), (30, 46)]
    s.poly(inner, '#6a6157')
    for x in range(52, 204, 12):
        s.rect(x, 29, 1, 23, '#574f47')
    # 横梁
    for x in (90, 150, 196):
        s.rect(x, 26, 6, 30, light)
        s.rect(x + 5, 26, 1, 30, dark)
    # 船侧板（朝镜头）
    side = [(14, 48), (40, 58), (208, 58), (236, 50), (240, 56), (210, 70), (44, 70), (18, 58)]
    s.poly(side, dark)
    s.line([(18, 58), (44, 70), (210, 70), (240, 56)], out, 2)
    s.line([(40, 60), (208, 60)], mid)
    s.line([(14, 34), (40, 22), (210, 22), (238, 30)], light)
    # 橹（船尾，伸向水面）
    s.line([(20, 30), (2, 8)], '#7a5a3c', 3)
    s.line([(20, 30), (2, 8)], '#a07a52', 1)
    # 船头挂一盏小提灯（为了雾）
    s.line([(232, 32), (238, 6)], '#6b5a3a', 2)
    s.line([(238, 6), (246, 6)], '#6b5a3a', 1)
    lan = paper_lantern('#f5e6c6', '#c0303a', w=9, h=11)
    s.paste(lan, 241, 6)
    # 缆绳
    s.line([(236, 40), (250, 44)], '#c8b58a', 1)
    # 水线下的船身（半透明）
    s.poly([(44, 70), (210, 70), (200, 78), (52, 78)], '#21495a', 0.55)
    s.outline(out, sides='all')
    return s


def stone_sign(font=None):
    """渡口告示牌：木框立牌，写“三途の渡 / 渡賃六文”。"""
    light, mid, dark, out = WOOD
    s = Sprite(64, 64, (32, 62))
    _post(s, 8, 24, 5, 38, WOOD)
    _post(s, 51, 24, 5, 38, WOOD)
    s.rect(3, 6, 58, 34, '#e4d6b4')
    s.rect(3, 6, 58, 3, mid)
    s.rect(3, 37, 58, 3, mid)
    s.rect(3, 6, 2, 34, dark)
    s.rect(59, 6, 2, 34, dark)
    s.poly([(0, 7), (32, 0), (64, 7), (60, 8), (32, 3), (4, 8)], dark)
    if font:
        s.text('三途の渡', 8, 10, '#2e2622', font, 12)
        s.text('渡賃六文', 8, 24, '#a32a2a', font, 12)
    else:
        for k in range(4):
            s.rect(10 + k * 12, 12, 8, 8, '#3b302a', 0.9)
            s.rect(10 + k * 12, 26, 8, 8, '#a32a2a', 0.9)
    s.outline(out)
    return s


def bell_frame():
    """渡钟：木架吊一口小梵钟，敲响唤来船头。"""
    s = Sprite(52, 82, (26, 80))
    _post(s, 6, 14, 6, 66, WOOD)
    _post(s, 40, 14, 6, 66, WOOD)
    s.poly([(0, 14), (26, 4), (52, 14), (48, 16), (26, 8), (4, 16)], WOOD[2])
    s.rect(2, 14, 48, 4, WOOD[1])
    s.rect(2, 14, 48, 1, WOOD[0])
    # 钟
    s.line([(26, 18), (26, 24)], '#3a3a3a', 2)
    s.poly([(18, 50), (19, 30), (22, 25), (30, 25), (33, 30), (34, 50)], '#6e7a6a')
    s.rect(17, 48, 18, 4, '#56604f')
    s.rect(20, 28, 3, 18, '#93a08b')
    for yy in (33, 38, 43):
        s.rect(19, yy, 14, 1, '#4f5848')
    # 撞木
    s.line([(44, 36), (50, 36)], '#8a6a48', 3)
    s.outline(WOOD[3])
    return s


def shelter(seed=0):
    """候船小屋：茅草顶、两根柱、长凳。约 210×150 像素，脚点在前柱连线中点。"""
    W, H = 214, 156
    s = Sprite(W, H, (W / 2, H - 4))
    rng = np.random.default_rng(seed)
    # 后墙（竹篱）
    _planks_v(s, 20, 54, W - 40, 62, GREY_WOOD, gap=5, seed=seed)
    s.rect(20, 54, W - 40, 3, GREY_WOOD[2])
    # 地面木台
    s.poly([(10, 132), (24, 112), (W - 24, 112), (W - 10, 132)], WOOD[1])
    _planks_h(s, 24, 112, W - 48, 14, WOOD, gap=4, seed=seed + 1)
    s.rect(10, 132, W - 20, 10, WOOD[2])
    s.rect(10, 141, W - 20, 2, WOOD[3])
    # 长凳
    s.rect(46, 96, W - 92, 7, WOOD[0])
    s.rect(46, 103, W - 92, 4, WOOD[2])
    for x in (54, W - 60):
        s.rect(x, 107, 5, 10, WOOD[2])
    # 柱
    for x in (22, W - 30):
        _post(s, x, 40, 8, 102, WOOD)
    # 茅草屋顶
    roof = [(0, 58), (30, 12), (W - 30, 12), (W, 58), (W - 6, 64), (6, 64)]
    s.poly(roof, THATCH[1])
    for k in range(70):  # 草茎
        x = rng.uniform(8, W - 8)
        y = rng.uniform(16, 58)
        L = rng.uniform(5, 12)
        s.line([(x, y), (x + rng.uniform(-1, 1), y + L)], THATCH[int(rng.integers(0, 3))], 1)
    s.poly([(30, 12), (40, 4), (W - 40, 4), (W - 30, 12)], THATCH[2])
    s.rect(30, 3, W - 60, 3, GREY_WOOD[2])
    s.line([(0, 58), (W, 58)], THATCH[3], 2)
    s.rect(4, 60, W - 8, 4, THATCH[2])
    s.outline(WOOD[3])
    # 挂一块小木牌
    s.rect(W / 2 - 14, 66, 28, 12, '#d9c9a5')
    s.rect(W / 2 - 14, 66, 28, 12, WOOD[3], 0.0)
    s.line([(W / 2 - 10, 62), (W / 2 - 10, 66)], '#5a4632')
    s.line([(W / 2 + 10, 62), (W / 2 + 10, 66)], '#5a4632')
    for k in range(3):
        s.rect(W / 2 - 9 + k * 7, 70, 4, 4, '#3b302a', 0.85)
    return s


def stall(kind, seed=0, font=None):
    """中有之道的屋台：木框、布幌、前台货品。kind: candy/goldfish/sotoba/fortune。约 170×150 像素。"""
    W, H = 172, 152
    s = Sprite(W, H, (W / 2, H - 4))
    rng = np.random.default_rng(seed)
    colors = {'candy': ('#5a6fb0', '#3d4f8c'), 'goldfish': ('#c0303a', '#8c1d29'),
              'sotoba': ('#6b5a7a', '#4c3f58'), 'fortune': ('#3f6f5a', '#2c5040')}
    cloth, cloth_d = colors[kind]
    # 后板
    _planks_v(s, 14, 40, W - 28, 60, GREY_WOOD, gap=6, seed=seed)
    # 柱
    for x in (10, W - 18):
        _post(s, x, 22, 8, 118, WOOD)
    # 屋顶（木板）
    s.poly([(0, 30), (12, 8), (W - 12, 8), (W, 30), (W - 4, 34), (4, 34)], WOOD[2])
    for x in range(14, W - 12, 10):
        s.line([(x, 9), (x - 4 if x < W / 2 else x + 4, 31)], WOOD[3], 1)
    s.rect(4, 30, W - 8, 4, WOOD[1])
    s.rect(12, 6, W - 24, 3, WOOD[1])
    # 暖帘（分成几片）
    for k in range(5):
        x0 = 20 + k * 27
        s.rect(x0, 34, 25, 24, cloth)
        s.rect(x0 + 21, 34, 4, 24, cloth_d)
        s.rect(x0, 56, 25, 2, cloth_d)
    if font:
        names = {'candy': '人魂飴', 'goldfish': '金魚掬', 'sotoba': '卒塔婆', 'fortune': '死後占'}
        for k, ch in enumerate(names[kind]):
            s.text(ch, 20 + (k + 1) * 27 + 6, 40, '#f3ead8', font, 12)
    # 前台
    s.rect(6, 100, W - 12, 12, WOOD[0])
    s.rect(6, 112, W - 12, 26, WOOD[1])
    s.rect(6, 136, W - 12, 3, WOOD[3])
    for x in range(10, W - 10, 14):
        s.rect(x, 113, 1, 23, WOOD[2])
    # 货品
    if kind == 'candy':   # 人魂糖：青白色的火焰形糖果插在稻草靶上
        s.rect(26, 84, 24, 18, THATCH[1])
        s.rect(26, 84, 24, 3, THATCH[0])
        for k in range(9):
            x, y = 28 + (k % 3) * 8, 70 + (k // 3) * 5
            s.line([(x + 2, y + 10), (x + 2, y + 16)], '#d8c8a0')
            s.ellipse(x + 2, y + 7, 3, 4, '#bfe8ff')
            s.px(x + 1, y + 5, '#ffffff')
            s.poly([(x, y + 6), (x + 2, y), (x + 4, y + 6)], '#9fd8ff')
        for k in range(6):
            s.ellipse(80 + k * 12, 96, 5, 4, ['#f2c9d8', '#bfe8ff', '#fff3c4'][k % 3])
    elif kind == 'goldfish':  # 捞死灵金鱼：浅水盆里游着半透明的灵金鱼
        s.ellipse(W / 2, 96, 52, 12, '#2d5c74')
        s.ellipse(W / 2, 95, 48, 9, '#4a8fae')
        for k in range(7):
            fx, fy = W / 2 - 36 + k * 12 + rng.uniform(-3, 3), 94 + rng.uniform(-4, 4)
            s.ellipse(fx, fy, 3, 2, '#e8f4ff', 0.85)
            s.px(fx + 3, fy, '#ff8f7a')
        for k in range(4):
            s.ellipse(22 + k * 9, 92, 4, 4, '#f5f0e0')
            s.ellipse(22 + k * 9, 92, 2.5, 2.5, '#d8e6ee')
    elif kind == 'sotoba':  # 卒塔婆与遗书代写
        for k in range(7):
            x = 22 + k * 7
            s.rect(x, 62, 5, 38, '#ddcfb2')
            s.poly([(x, 62), (x + 2.5, 58), (x + 5, 62)], '#ddcfb2')
            s.rect(x + 1, 70, 2, 20, '#3b302a', 0.6)
        s.rect(96, 92, 50, 8, '#efe6cf')
        s.line([(100, 95), (140, 95)], '#3b302a', 1, 0.6)
        s.rect(130, 86, 3, 8, '#2a2420')
    elif kind == 'fortune':  # 死后占卜：水晶球与签筒
        s.ellipse(W / 2, 90, 11, 11, '#a8c8e8')
        s.ellipse(W / 2 - 3, 86, 4, 4, '#e8f4ff')
        s.rect(W / 2 - 9, 100, 18, 3, '#6b4a8a')
        s.rect(40, 80, 10, 20, '#8a5a3a')
        for k in range(4):
            s.rect(41 + k * 2, 74 + (k % 2) * 2, 1, 8, '#e8dcc0')
    # 檐下挂两盏提灯
    for x in (20, W - 34):
        s.paste(paper_lantern(w=11, h=14), x, 34)
    s.outline(WOOD[3])
    return s


def gate_posts():
    """冠木门的两根立柱（单独精灵，带碰撞）。返回 (左柱, 右柱)。"""
    out = []
    for _ in range(2):
        s = Sprite(16, 150, (8, 148))
        _post(s, 3, 6, 10, 142, GREY_WOOD)
        s.rect(1, 140, 14, 8, GREY_WOOD[2])
        s.outline(GREY_WOOD[3])
        out.append(s)
    return out


def gate_beam(span_px, font=None):
    """冠木门的横梁与匾额（放在 Overlay 层）。"""
    W = span_px + 40
    s = Sprite(W, 44, (W / 2, 44))
    light, mid, dark, out = GREY_WOOD
    s.rect(0, 6, W, 10, mid)
    s.rect(0, 6, W, 2, light)
    s.rect(0, 14, W, 2, dark)
    s.rect(14, 24, W - 28, 6, mid)
    s.rect(14, 29, W - 28, 1, dark)
    s.rect(W / 2 - 34, 14, 68, 26, '#d9c9a5')
    s.rect(W / 2 - 34, 14, 68, 3, dark)
    s.rect(W / 2 - 34, 37, 68, 3, dark)
    if font:
        s.text('賽の河原', W / 2 - 24, 21, '#2e2622', font, 12)
    else:
        for k in range(4):
            s.rect(W / 2 - 26 + k * 14, 21, 9, 11, '#3b302a', 0.9)
    s.outline(out)
    return s


def kimono_tree(seed=3):
    """衣领树：枝干虬曲的老树，枝头挂着亡者被剥下的衣物（三途川民俗原型：夺衣婆与悬衣翁）。"""
    rng = np.random.default_rng(seed)
    W, H = 300, 300
    s = Sprite(W, H, (W / 2, H - 6))
    bark = ('#7d6b5e', '#5d4e45', '#3f342e', '#251e1b')
    # 递归枝干
    segs = []

    def branch(x, y, ang, length, width, depth):
        x2, y2 = x + math.cos(ang) * length, y - math.sin(ang) * length
        segs.append((x, y, x2, y2, width))
        if depth == 0 or width < 2:
            return
        for da in (rng.uniform(0.25, 0.7), -rng.uniform(0.25, 0.7)):
            if rng.random() < 0.88:
                branch(x2, y2, ang + da + rng.normal(0, 0.12), length * rng.uniform(0.62, 0.8), width * 0.68, depth - 1)

    branch(W / 2, H - 8, math.pi / 2 + rng.normal(0, 0.05), 70, 22, 5)
    segs.sort(key=lambda t: -t[4])
    # 稀疏的暗色叶团（先画在枝后）
    leaves = canopy(W - 20, int(H * 0.62), seed, ('#1b2a2c', '#243838', '#2f4743', '#3f5a50', '#52705e'), '#101c1d',
                    blob=(8, 14), density=0.32, dots=False)
    s.paste(leaves, 10, 6)
    for x1, y1, x2, y2, w in segs:
        s.line([(x1, y1), (x2, y2)], bark[1], max(1, int(w)))
        s.line([(x1 - w * 0.25, y1), (x2 - w * 0.25, y2)], bark[0], max(1, int(w * 0.3)))
        s.line([(x1 + w * 0.3, y1), (x2 + w * 0.3, y2)], bark[2], max(1, int(w * 0.25)))
    # 根部张开
    for dx in (-26, -12, 14, 28):
        s.poly([(W / 2 + dx, H - 6), (W / 2 + dx * 0.3, H - 30), (W / 2 + dx * 0.3 + 8, H - 30), (W / 2 + dx + 10, H - 6)], bark[1])
    s.outline(bark[3])
    # 挂在枝头的衣物：白色经帷子与褪色和服，袖子横穿在枝上
    clothes = [('#ece8dc', '#c9c3b3', '#b5ae9c'), ('#e7e3d6', '#bdb6a5', '#a59e8c'), ('#b9606a', '#8e4650', '#d8b54a'),
               ('#5f7299', '#46577a', '#c9c3b3'), ('#ece8dc', '#c9c3b3', '#8e4650'), ('#a89668', '#857549', '#5f7299')]
    tips = [((x1 + x2) / 2, (y1 + y2) / 2) for x1, y1, x2, y2, w in segs if 3 < w < 10 and y2 < H * 0.66]
    rng.shuffle(tips)
    placed = []
    for x, y in tips:
        if len(placed) >= 7:
            break
        if any(abs(x - px_) < 46 and abs(y - py_) < 46 for px_, py_ in placed):
            continue
        placed.append((x, y))
        c1, c2, obi = clothes[len(placed) % len(clothes)]
        sw, bw = int(rng.integers(11, 14)), int(rng.integers(16, 20))   # 袖宽、身宽
        sh, bh = int(rng.integers(18, 24)), int(rng.integers(34, 44))   # 袖长（振袖下垂）、身长
        x0 = int(x - sw - bw / 2)
        y0 = int(y)
        s.rect(x0, y0, sw, sh, c1)                       # 左袖
        s.rect(x0 + sw + bw, y0, sw, sh, c1)             # 右袖
        s.rect(x0 + sw, y0, bw, bh, c1)                  # 身
        s.rect(x0 + sw + bw - 3, y0 + 2, 3, bh - 2, c2)  # 右侧暗面
        s.rect(x0 + sw * 2 + bw - 2, y0 + 1, 2, sh - 1, c2)
        s.rect(x0, y0 + sh - 2, sw, 2, c2)
        s.line([(x0 + sw + 2, y0), (x0 + sw + bw / 2, y0 + 9), (x0 + sw + bw - 2, y0)], c2, 1)  # 交领
        s.rect(x0 + sw, y0 + int(bh * 0.42), bw, 3, obi)                                  # 腰带
        s.line([(x0 - 3, y0), (x0 + sw * 2 + bw + 3, y0)], '#4a3c30', 1)                   # 穿过两袖的枝
    s.outline(bark[3])
    return s


def purple_sakura(seed=5):
    """紫之樱：无缘冢的妖怪樱（花映冢、求闻史纪）。"""
    pal = ('#4e3672', '#6d4c98', '#8f6bbd', '#b08fd6', '#d3bdee')
    cv = canopy(190, 140, seed, pal, '#2e1f45', blob=(9, 16))
    W, H = 190, 196
    s = Sprite(W, H, (W / 2, H - 4))
    bark = ('#6b5a5e', '#4d3f44', '#33292e')
    s.poly([(W / 2 - 9, H - 4), (W / 2 - 6, 120), (W / 2 + 6, 120), (W / 2 + 10, H - 4)], bark[1])
    s.line([(W / 2 - 6, H - 6), (W / 2 - 4, 122)], bark[0], 2)
    s.line([(W / 2 - 4, 130), (W / 2 - 40, 92)], bark[1], 5)
    s.line([(W / 2 + 4, 126), (W / 2 + 44, 96)], bark[1], 5)
    s.paste(cv, 0, 0)
    rng = np.random.default_rng(seed)
    for _ in range(40):  # 树冠下缘的散落花瓣
        x, y = rng.uniform(20, W - 20), rng.uniform(110, 150)
        s.px(x, y, pal[3])
    s.outline('#2e1f45')
    return s


def signpost(font=None):
    """无缘冢方向的路标。"""
    s = Sprite(56, 64, (14, 62))
    _post(s, 11, 10, 6, 52, GREY_WOOD)
    s.poly([(4, 12), (46, 12), (54, 19), (46, 26), (4, 26)], '#cfc2a2')
    s.line([(4, 12), (46, 12), (54, 19), (46, 26), (4, 26)], GREY_WOOD[3])
    if font:
        s.text('無縁塚', 7, 13, '#2e2622', font, 12)
    else:
        for k in range(3):
            s.rect(9 + k * 11, 15, 7, 8, '#3b302a', 0.85)
    s.outline(GREY_WOOD[3])
    return s


def skeleton(seed=9):
    """半埋在卵石里的蛇颈龙骨架（鬼形兽：三途川有已灭绝的巨型鱼与蛇颈龙）。约 420×170 像素。"""
    rng = np.random.default_rng(seed)
    W, H = 430, 176
    s = Sprite(W, H, (W / 2, H - 30))
    bone, bone_l, bone_d, out = '#d8d0bc', '#efe9da', '#a89f8a', '#5a5246'
    # 脊柱：从右侧尾部弯向左侧长颈与头骨
    spine = [(410, 120), (370, 112), (320, 104), (270, 100), (220, 104), (175, 112), (140, 106),
             (112, 88), (88, 66), (64, 50), (40, 44)]
    pts = []
    for (x0, y0), (x1, y1) in zip(spine[:-1], spine[1:]):
        for t in np.linspace(0, 1, 6, endpoint=False):
            pts.append((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t))
    for k, (x, y) in enumerate(pts):
        r = 5.5 if 4 < k < 34 else 4
        s.ellipse(x, y, r, r * 0.8, bone)
        s.px(x - 1, y - 2, bone_l)
        s.px(x + 2, y + 2, bone_d)
    # 肋骨：躯干段向下弯曲的弧（下半埋在卵石里）
    for k in range(9):
        x = 190 + k * 22
        y = 104 - math.sin(k / 8 * math.pi) * 6
        span = 46 - abs(k - 4) * 4
        arc = [(x + math.sin(t) * 10 - 6, y + t / (math.pi / 2) * span) for t in np.linspace(0, math.pi / 2, 10)]
        s.line(arc, bone_d, 4)
        s.line([(px - 1, py) for px, py in arc], bone, 2)
    # 头骨
    s.poly([(10, 40), (26, 30), (46, 32), (54, 44), (44, 54), (20, 52)], bone)
    s.ellipse(32, 40, 5, 4, '#3b352e')
    s.line([(12, 48), (46, 50)], bone_d, 1)
    for k in range(6):
        s.px(16 + k * 5, 50, bone_l)
    # 鳍状肢骨
    for bx, by, dirx in ((170, 116, -1), (300, 112, 1)):
        for j in range(4):
            s.line([(bx, by), (bx + dirx * (18 + j * 6), by + 22 + j * 4)], bone_d, 2)
    # 埋入卵石：下缘用卵石色块遮住
    for _ in range(60):
        x, y = rng.uniform(150, 420), rng.uniform(136, 170)
        s.ellipse(x, y, rng.uniform(5, 10), rng.uniform(3, 6), ['#8e9097', '#a3a19c', '#848d90'][int(rng.integers(3))])
    s.outline(out)
    return s


def coelacanth(w=60):
    """腔棘鱼（已灭绝的古代鱼）：深蓝带白斑，肉质叶状鳍。头朝下挂在晾架上。"""
    h = int(w * 0.42)
    s = Sprite(h + 10, w + 8, ((h + 10) / 2, 0))
    body, belly, spot, out = '#3b4f7a', '#55688f', '#c9d4e6', '#1e2840'
    cx = (h + 10) / 2
    s.ellipse(cx, w * 0.45, h / 2, w * 0.42, body)
    s.ellipse(cx - h * 0.15, w * 0.45, h / 4, w * 0.36, belly)
    # 叶状鳍
    for fy, side in ((0.35, -1), (0.55, 1), (0.7, -1)):
        s.ellipse(cx + side * h * 0.55, w * fy, 4, 7, body)
    # 三叉尾
    s.poly([(cx - 8, w * 0.85), (cx, w * 0.8), (cx + 8, w * 0.85), (cx + 4, w + 4), (cx, w * 0.92), (cx - 4, w + 4)], body)
    rng = np.random.default_rng(w)
    for _ in range(int(w / 5)):
        s.px(cx + rng.uniform(-h * 0.35, h * 0.35), rng.uniform(w * 0.15, w * 0.75), spot)
    s.ellipse(cx + 2, w * 0.1, 2, 2, '#e8e4c8')
    s.px(cx + 2, w * 0.1, '#111111')
    s.outline(out)
    return s


def fish_rack(seed=0, n=3):
    """晾鱼架：两根立柱一根横杆，挂着腔棘鱼。"""
    W, H = 150, 118
    s = Sprite(W, H, (W / 2, H - 4))
    _post(s, 8, 14, 6, H - 18, GREY_WOOD)
    _post(s, W - 14, 14, 6, H - 18, GREY_WOOD)
    s.rect(4, 14, W - 8, 5, GREY_WOOD[1])
    s.rect(4, 14, W - 8, 1, GREY_WOOD[0])
    rng = np.random.default_rng(seed)
    for k in range(n):
        x = 30 + k * (W - 60) / max(1, n - 1)
        f = coelacanth(int(rng.integers(50, 66)))
        s.line([(x, 18), (x, 24)], '#c8b58a')
        s.paste(f, x - f.w / 2, 23)
    s.outline(GREY_WOOD[3])
    return s


def urumi_stall(seed=0, font=None):
    """牛崎润美的鱼摊：牛纹（黄黑白）布篷、台秤（改变重量的能力）、冰鱼箱。约 220×160 像素。"""
    W, H = 224, 164
    s = Sprite(W, H, (W / 2, H - 4))
    rng = np.random.default_rng(seed)
    yel, blk, wht, red = '#e8c24a', '#2a2a2e', '#eeeee6', '#c83a3a'
    for x in (10, W - 18):
        _post(s, x, 30, 8, 124, GREY_WOOD)
    # 篷布：白底黑斑（奶牛纹），黄色荷叶边带红点
    s.poly([(0, 40), (14, 12), (W - 14, 12), (W, 40)], wht)
    for _ in range(9):
        x, y = rng.uniform(24, W - 24), rng.uniform(18, 34)
        s.ellipse(x, y, rng.uniform(6, 13), rng.uniform(3, 6), blk)
    s.rect(0, 40, W, 10, yel)
    for x in range(6, W, 14):
        s.ellipse(x, 50, 6, 4, yel)
        s.px(x, 51, red)
    s.line([(0, 40), (W, 40)], '#a8862a', 1)
    # 台面与鱼
    s.rect(8, 104, W - 16, 12, '#a0c4d0')
    s.rect(8, 104, W - 16, 2, '#d8eef4')
    s.rect(8, 116, W - 16, 30, WOOD[1])
    s.rect(8, 144, W - 16, 3, WOOD[3])
    for k in range(4):
        x = 30 + k * 40
        s.ellipse(x, 100, 16, 6, ['#3b4f7a', '#6a7f8c', '#8a6f5a', '#4f6a5a'][k])
        s.px(x - 12, 99, '#e8e4c8')
    # 台秤（吊秤）
    s.line([(W - 50, 50), (W - 50, 66)], '#5a5246', 1)
    s.rect(W - 64, 66, 28, 3, '#8a7a5a')
    s.line([(W - 62, 69), (W - 62, 78)], '#5a5246', 1)
    s.line([(W - 38, 69), (W - 38, 78)], '#5a5246', 1)
    s.ellipse(W - 62, 80, 6, 2, '#b8a87a')
    s.ellipse(W - 38, 80, 6, 2, '#b8a87a')
    # 招牌
    s.rect(W / 2 - 34, 56, 68, 22, '#efe6cf')
    s.rect(W / 2 - 34, 56, 68, 2, blk)
    s.rect(W / 2 - 34, 76, 68, 2, blk)
    if font:
        s.text('三途川鮮魚', W / 2 - 30, 61, '#2a2a2e', font, 12)
    else:
        for k in range(4):
            s.rect(W / 2 - 24 + k * 13, 61, 9, 11, '#2a2a2e', 0.85)
    s.outline('#3a3026')
    return s


def barrel(seed=0):
    s = Sprite(28, 34, (14, 32))
    light, mid, dark, out = WOOD
    s.ellipse(14, 22, 12, 10, mid)
    s.rect(2, 8, 24, 14, mid)
    s.ellipse(14, 8, 12, 5, light)
    s.ellipse(14, 8, 9, 3, dark)
    for y in (12, 24):
        s.rect(2, y, 24, 2, '#4a4a4a')
    s.rect(20, 9, 4, 20, dark)
    s.outline(out)
    return s


def crate(seed=0, w=34, h=30):
    s = Sprite(w, h, (w / 2, h - 1))
    light, mid, dark, out = WOOD
    s.rect(1, 1, w - 2, 9, light)
    s.rect(1, 10, w - 2, h - 11, mid)
    s.line([(2, 11), (w - 3, h - 2)], dark, 2)
    s.rect(1, 10, w - 2, 1, dark)
    s.outline(out)
    return s


def net_poles():
    s = Sprite(90, 104, (45, 102))
    for x in (8, 78):
        _post(s, x, 6, 5, 96, GREY_WOOD)
    s.line([(10, 10), (80, 14)], '#6a5a44', 1)
    for k in range(10):  # 渔网的菱形网眼
        x = 12 + k * 7
        s.line([(x, 12), (x + 7, 60 - k % 2 * 6)], '#8d8670', 1)
        s.line([(x + 7, 12), (x, 60 - k % 2 * 6)], '#8d8670', 1)
    s.outline(GREY_WOOD[3])
    return s


def hitodama(size=1.0, color=('#e8f6ff', '#a9dcff', '#6fb4e8')):
    """人魂：青白色的灵火，带拖尾。"""
    rows = ["....AA....",
            "...ABBA...",
            "..ABBBBA..",
            "..ABCCBA..",
            "..ABCCBA..",
            "...ABBA...",
            "....AB....",
            ".....A....",
            "....A.....",
            "...A......"]
    return ascii_sprite(rows, {'A': color[2], 'B': color[1], 'C': color[0]}, pivot=(5, 10))


def floating_lantern(seed=0):
    """灯笼流：漂在水面的方形纸灯。"""
    s = Sprite(16, 16, (8, 14))
    s.rect(2, 10, 12, 4, WOOD[2])
    s.rect(3, 3, 10, 8, '#f3e3c3')
    s.rect(4, 4, 8, 6, '#ffd98a')
    s.rect(3, 3, 10, 1, '#c84a3a')
    s.outline('#5a3a28')
    return s


def contest_board(font=None):
    """积石大赛的告示板：竖牌 + 红布。"""
    s = Sprite(40, 70, (20, 68))
    _post(s, 17, 30, 6, 38, GREY_WOOD)
    s.rect(4, 4, 32, 34, '#e7dcc0')
    s.rect(4, 4, 32, 4, '#c0303a')
    if font:
        s.text('積石', 8, 11, '#2e2622', font, 12)
        s.text('大会', 8, 24, '#2e2622', font, 12)
    else:
        for k in range(4):
            s.rect(10 + (k % 2) * 11, 12 + (k // 2) * 12, 8, 9, '#3b302a', 0.85)
    s.outline(GREY_WOOD[3])
    return s


def pennant_line(length_px, seed=0):
    """两旗杆间的三角彩旗绳（Overlay）。"""
    W, H = int(length_px) + 4, 26
    s = Sprite(W, H, (0, 0))
    rng = np.random.default_rng(seed)
    pts = [(x, 4 + math.sin(x / W * math.pi) * 10) for x in range(0, W, 4)]
    s.line(pts, '#5a4a3a', 1)
    cols = ['#d8333c', '#3f9a54', '#f2e6c8', '#4f6fb0', '#e8c24a']
    for k, x in enumerate(range(6, W - 6, 13)):
        y = 4 + math.sin(x / W * math.pi) * 10
        s.poly([(x, y), (x + 9, y), (x + 4.5, y + 10)], cols[k % len(cols)])
    s.outline('#3a3026')
    return s


def pole(h=110, color=GREY_WOOD):
    s = Sprite(10, h + 2, (5, h))
    _post(s, 2, 2, 5, h - 2, color)
    s.rect(1, 0, 8, 4, color[2])
    s.outline(color[3])
    return s


def bridge_rail(length_px, rise_px, posts=6):
    """拱桥的栏杆（前侧，Overlay/按 Y 排序），沿桥拱抬升。"""
    W, H = int(length_px) + 8, int(rise_px) + 40
    s = Sprite(W, H, (W / 2, H - 2))
    red, red_d, red_l = '#b8433a', '#7e2a26', '#d8665a'

    def arch(x):
        t = (x - 4) / max(1, length_px)
        return H - 26 - math.sin(np.clip(t, 0, 1) * math.pi) * rise_px

    top = [(x, arch(x)) for x in range(4, W - 3, 2)]
    s.line(top, red, 4)
    s.line([(x, y - 1) for x, y in top], red_l, 1)
    s.line([(x, y + 12) for x, y in top], red_d, 3)
    for k in range(posts):
        x = 4 + k * (length_px - 2) / (posts - 1)
        y = arch(x)
        s.rect(x - 3, y - 6, 6, 30, red)
        s.rect(x - 3, y - 6, 2, 30, red_l)
        s.rect(x - 4, y - 9, 8, 4, '#3a3030')
    s.outline('#3a2420')
    return s


def wrecked_boat(seed=0):
    """搁浅漏水的旧渡船：船底破洞、缺板，旁边丢着一把柄杓。
    （夜雀食堂中村纱水蜜的台词：“那个倒霉死神发现自己的船漏水了吗？”）"""
    rng = np.random.default_rng(seed)
    W, H = 200, 84
    s = Sprite(W, H, (W / 2, H - 10))
    light, mid, dark, out = GREY_WOOD
    hull = [(10, 40), (30, 26), (166, 22), (190, 30), (194, 44), (176, 56), (34, 62), (12, 54)]
    s.poly(hull, dark)
    inner = [(28, 38), (40, 31), (160, 28), (180, 36), (176, 46), (160, 50), (40, 54), (26, 48)]
    s.poly(inner, '#5a5249')
    for x in range(44, 160, 11):
        if rng.random() < 0.8:
            s.rect(x, 31, 9, 20, mid)
            s.rect(x, 31, 2, 20, light)
    # 破洞与积水
    s.ellipse(110, 44, 13, 6, '#2b2a2c')
    s.ellipse(112, 45, 9, 3, '#56818a')
    s.rect(108, 43, 6, 1, '#a9cfd2')
    # 断裂的船舷
    s.poly([(150, 22), (166, 22), (176, 14), (172, 12)], mid)
    s.line([(30, 26), (166, 22)], light, 2)
    s.line([(12, 54), (34, 62), (176, 56), (194, 44)], out, 2)
    # 柄杓
    s.line([(182, 70), (198, 60)], '#8a6a48', 2)
    s.ellipse(178, 72, 6, 4, '#7a5a3a')
    s.ellipse(178, 71, 4, 2, '#3a2a20')
    # 埋入卵石的边缘
    for _ in range(18):
        x, y = rng.uniform(14, 190), rng.uniform(56, 66)
        s.ellipse(x, y, rng.uniform(4, 8), rng.uniform(2, 4), ['#8e9097', '#a3a19c', '#848d90'][int(rng.integers(3))])
    s.outline(out)
    return s


def stele(font=None):
    """“三途川”石碑：自然石，正面阴刻。"""
    hi, l, m, d, o = STONE
    s = Sprite(44, 86, (22, 84))
    s.poly([(4, 84), (2, 70), (6, 20), (14, 6), (30, 3), (38, 14), (42, 40), (40, 84)], l)
    s.poly([(30, 3), (38, 14), (42, 40), (40, 84), (32, 84), (34, 30)], m)
    s.poly([(14, 6), (30, 3), (24, 10), (12, 14)], hi)
    s.rect(2, 80, 40, 4, d)
    if font:
        s.text('三', 16, 18, d, font, 12)
        s.text('途', 16, 32, d, font, 12)
        s.text('川', 16, 46, d, font, 12)
    else:
        for k in range(3):
            s.rect(17, 19 + k * 14, 9, 9, d, 0.9)
    for k in range(10):
        s.px(6 + (k * 7) % 30, 70 + (k * 3) % 12, '#6f7d45')
    s.outline(o)
    return s


def lantern_pole():
    """栈桥端的挂灯竹竿：雾中给渡船指路。"""
    s = Sprite(40, 150, (8, 148))
    s.rect(6, 10, 4, 138, '#7a6a48')
    s.rect(6, 10, 1, 138, '#a08c62')
    for y in range(20, 148, 22):
        s.rect(5, y, 6, 2, '#5e5036')
    s.line([(8, 12), (30, 8)], '#7a6a48', 2)
    s.line([(28, 9), (28, 18)], '#3a3026', 1)
    s.paste(paper_lantern('#f5e6c6', '#c0303a', w=12, h=15), 22, 18)
    s.outline('#3a2f22')
    return s


def nobori(text, color='#c0303a', font=None):
    """幟旗：竖长布旗，白字。"""
    s = Sprite(26, 132, (5, 130))
    _post(s, 3, 4, 4, 126, GREY_WOOD)
    s.rect(7, 8, 16, 76, color)
    s.rect(7, 8, 16, 3, shift(color, 0, 0, -0.2))
    s.rect(21, 11, 2, 73, shift(color, 0, 0.05, -0.18))
    s.line([(7, 8), (23, 8)], '#3a3026', 1)
    for k in range(4):                                  # 下摆的流苏
        s.px(9 + k * 4, 84, shift(color, 0, 0, -0.2))
    if font:
        for k, ch in enumerate(text):
            s.text(ch, 9, 16 + k * 14, '#f6efe0', font, 12)
    s.outline('#3a2420')
    return s


def bench_parasol(font=None):
    """缘台与野点伞：红毛毡长凳配一把大红伞。"""
    W, H = 150, 150
    s = Sprite(W, H, (W / 2, H - 4))
    # 长凳
    s.rect(30, 112, 90, 10, '#c83a36')
    s.rect(30, 112, 90, 2, '#e0605a')
    s.rect(30, 122, 90, 6, '#8a6a48')
    for x in (34, 112):
        s.rect(x, 128, 5, 16, '#6a4e36')
    s.rect(60, 104, 14, 8, '#e8dcc0')                   # 茶托与团子
    s.ellipse(67, 103, 5, 3, '#f2ead8')
    for k in range(3):
        s.ellipse(86 + k * 5, 108, 2.5, 2.5, ['#f2b8c6', '#f5f0e0', '#9cc87a'][k])
    # 伞柄与伞面（3/4 视角的扁椭圆，伞骨放射）
    s.rect(W / 2 - 2, 30, 4, 92, '#5a4632')
    s.ellipse(W / 2, 32, 70, 26, '#b32a2e')
    s.ellipse(W / 2, 28, 66, 20, '#d0383c')
    for k in range(12):
        a = k / 12 * 2 * math.pi
        s.line([(W / 2, 28), (W / 2 + math.cos(a) * 64, 28 + math.sin(a) * 19)], '#a02428', 1)
    s.ellipse(W / 2, 27, 6, 3, '#e8c24a')
    s.outline('#3a1c1c')
    return s

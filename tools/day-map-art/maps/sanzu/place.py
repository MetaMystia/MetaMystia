"""三途川（生图版）的物件摆放与地形测量值。

坐标为世界坐标（格），物件以脚点定位。地形数值从定稿地面图上按网格量出，
地面图重画后需要重新量（见 docs/map-creation/art-pipeline.md）。

物件项：dict(spr, x, y, ...)
  collide=(w, h)  脚点上方的阻挡矩形（宽、深，格）；None 表示可穿过
  flip / scale / alpha
  layer='Overlay', order=n  不参与 Y 排序的顶层物件
  shadow=w        投影椭圆宽（格）；0 表示不画
  glow=(dx, dy, r, color, a)  在脚点偏移处加光晕（格）
"""
import math

# ---------------------------------------------------------------- 地形测量（定稿地面图）
PIER_STEM = (-2.3, 2.95, -0.05, 9.6)
PIER_HEAD = (-4.0, 9.45, 1.5, 11.9)
JETTY = (26.2, 6.3, 27.3, 9.3)            # 向陆地多延伸一点，与岸边相接

BRIDGE_X = (15.54, 20.67)
BRIDGE_MID = 18.1


def bridge_center(x):
    """拱桥可走带中线的 y：两端 1.47，桥顶 2.56。"""
    u = (x - BRIDGE_MID) / ((BRIDGE_X[1] - BRIDGE_X[0]) / 2)
    return 1.47 + 1.09 * (1 - u * u)


BRIDGE_HALF = 0.72                       # 可走带半宽
BRIDGE_SLOPES = {15: 0.75, 16: 0.5, 17: 0.2, 18: -0.15, 19: -0.45, 20: -0.75}   # 按拱形逐列

WALL = (-36.0, -11.0, -22.04, -8.24)     # 台地南侧石墙
STAIRS = [(-28.75, -8.24), (-26.65, -8.24), (-21.95, -11.0), (-24.45, -11.0)]   # 石阶四角（左上、右上、右下、左下）
STAIRS_SLOPE = -0.6
TERRACE = [(-36.6, -8.24), (-22.3, -8.24), (-22.7, -2.9), (-23.1, 0.0), (-23.75, 5.0), (-25.0, 8.0),
           (-26.2, 9.6), (-27.5, 10.8), (-36.6, 10.8)]                          # 台地顶面
# 河原上被踩出来的小路（积石避开）
TRAILS = [[(-1.2, 2.9), (-1.0, -6.4)],
          [(0.25, 0.67), (7.75, 1.29), (15.67, 2.13)],
          [(-1.4, -6.8), (-12.0, -7.9), (-23.5, -9.3)]]

# 地面图修补：(区域 x0, y0, x1, y1), (取样偏移 dx, dy)。用旁边干净的区域盖掉去物件时留下的残影
PLATE_PATCHES = [((20.2, 15.0, 22.5, 19.3), (-3.2, 0.0))]

# ---------------------------------------------------------------- 物件
P = []


def add(spr, x, y, **kw):
    P.append(dict(spr=spr, x=x, y=y, **kw))


# 渡口
add('Boat', 4.4, 9.62, collide=None, shadow=0)
add('LanternPole', -3.55, 11.55, collide=(0.3, 0.2), glow=(-0.45, 2.15, 1.6, '#ffd89a', 0.34))
add('NoticeBoard', -4.9, 2.4, collide=(1.6, 0.35))
add('LanternA', -3.1, 2.55, collide=(0.6, 0.35), glow=(0, 0.9, 1.4, '#ffcf80', 0.32))
add('LanternB', 0.75, 2.55, collide=(0.6, 0.35), glow=(0, 0.85, 1.4, '#ffcf80', 0.32))
add('BellFrame', 3.4, 2.45, collide=(1.4, 0.3))
add('Shelter', 8.0, 0.2, collide=(3.9, 1.3))
add('BoulderFlat', 9.8, 4.75, collide=(2.0, 0.7))
add('Wreck', 12.4, 3.3, collide=(3.0, 0.8))
add('BoulderRound', 7.04, 4.36, collide=(1.0, 0.5), scale=0.8)

# 赛之河原：积石、风车、卒塔婆（避开小路与地标）
STACKS = [(-12.66, 3.44), (-13.5, 1.72), (-21.1, -3.48), (-9.81, 1.04), (-21.08, 2.24), (-9.8, -3.24), (-8.45, 0.16),
          (-9.92, -0.49), (-12.2, 0.61), (-6.95, -0.61), (-21.21, 0.4), (-17.13, -1.86), (-16.71, -0.08), (-9.08, 2.44),
          (-7.57, -2.67), (-14.75, 3.35), (-11.48, 4.28), (-7.53, 2.54), (-19.56, 0.46), (-19.98, -1.5), (-7.41, 4.14),
          (-11.84, -3.85), (-17.53, 1.02), (-9.54, 4.48), (-15.97, 1.83), (-12.75, -1.1), (-9.99, -1.9), (-13.47, 4.51),
          (-14.32, -1.2), (-16.94, 4.29), (-18.71, -4.04), (-13.98, -3.17), (-8.05, -4.06), (-20.56, 4.46), (-17.1, 2.63),
          (-18.54, -1.25), (-14.54, 0.51), (-4.16, -2.67), (11.29, -4.56), (15.63, -5.52), (5.79, -4.81), (-4.94, 0.11)]
PINWHEELS = [(-12.55, 0.73), (-11.38, -3.6), (-18.18, -4.0), (-16.0, 3.97), (-9.22, -3.16), (-16.39, -2.4), (-20.58, -1.81),
             (-18.26, -0.66), (-9.29, 1.63), (-7.95, -1.16), (-6.73, 0.84), (-20.88, 1.51), (-12.27, 3.35)]
SOTOBA = [(-15.57, 2.58), (-11.43, -1.79), (-10.45, 1.86), (-14.32, -0.94), (-19.28, -0.99), (-7.01, 3.5)]
KEEP_OUT = [(-12.6, 3.3, 2.6), (-20.3, -7.3, 2.4), (-4.6, -6.6, 1.2), (-4.9, 2.4, 1.3)]   # 积石大会看板、六地藏、石碑、告示牌


def _seg_dist(p, a, b):
    ax, ay = a
    bx, by = b
    px, py = p
    dx, dy = bx - ax, by - ay
    t = max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - ax - t * dx, py - ay - t * dy)


def clear_of_trails(x, y, margin):
    for tr in TRAILS:
        for a, b in zip(tr, tr[1:]):
            if _seg_dist((x, y), a, b) < margin:
                return False
    return all(math.hypot(x - kx, y - ky) >= kr for kx, ky, kr in KEEP_OUT)


CAIRNS = ['Cairn2', 'Cairn3', 'Cairn4', 'Cairn5', 'Cairn6']
for i, (x, y) in enumerate(STACKS):
    if clear_of_trails(x, y, 0.95):
        add(CAIRNS[(i * 7 + 3) % 5], x, y, collide=(0.5, 0.25), flip=i % 3 == 0, shadow=0.55)
PINS = ['PinwheelRed', 'PinwheelRW', 'PinwheelYellow', 'PinwheelBlue']
for i, (x, y) in enumerate(PINWHEELS):
    if clear_of_trails(x, y, 0.7):
        add(PINS[i % 4], x, y, collide=None, flip=i % 2 == 1, shadow=0.25)
SOTOBAS = ['SotobaA', 'SotobaB', 'SotobaC']
for i, (x, y) in enumerate(SOTOBA):
    if clear_of_trails(x, y, 0.7):
        add(SOTOBAS[i % 3], x, y, collide=None, shadow=0.3)

add('ContestBoard', -12.6, 3.15, collide=(4.0, 0.25))
add('JizoRow', -20.3, -7.3, collide=(3.1, 0.55))
for i, x in enumerate((-21.6, -20.8, -20.0, -19.2)):
    add(SOTOBAS[i % 3], x, -6.55, collide=None, shadow=0)
add('Stele', -4.6, -6.45, collide=(1.1, 0.45))
add('Jizo', -5.6, -6.35, collide=(0.6, 0.3))
add('BoulderTall', 7.72, -5.06, collide=(1.2, 0.5), scale=0.85)
add('BoulderRound', 11.62, -5.21, collide=(1.1, 0.5))

# 台地：衣领树、紫花树、无缘塚
add('KimonoTree', -30.0, 1.8, collide=(1.6, 0.7))
add('PurpleTree', -32.6, -6.4, collide=(1.0, 0.5))
add('Stele', -34.6, -7.6, collide=(0.7, 0.35), scale=0.62)
add('BoulderRound', -24.6, 1.6, collide=(1.0, 0.45), scale=0.85)
add('BoulderTall', -25.4, 5.6, collide=(1.0, 0.45), scale=0.8)
add('BoulderFlat', -24.4, -3.6, collide=(1.6, 0.5), scale=0.8)
add('BoulderRound', -33.8, 4.4, collide=(1.0, 0.45), scale=0.9)
add('Jizo', -35.2, -7.7, collide=(0.6, 0.3))

# 中有之道：冠木门、摊位、灯笼、幡、柳
add('Gate', -0.35, -7.25, collide=None, shadow=0)
add('StallCandy', -6.8, -19.6, collide=(3.5, 1.2))
add('StallGoldfish', -6.6, -13.8, collide=(3.5, 1.2))
add('StallSotoba', 6.0, -18.4, collide=(3.5, 1.2))
add('StallFortune', 5.6, -12.6, collide=(3.5, 1.2))
add('ParasolBench', -8.7, -16.7, collide=(1.8, 0.5))
for i, (x, y) in enumerate([(-3.4, -10.6), (3.0, -10.4), (-3.6, -16.8), (3.0, -16.6), (-3.4, -22.2), (3.1, -22.0)]):
    add('LanternA' if i % 2 == 0 else 'LanternB', x, y, collide=(0.6, 0.35), glow=(0, 0.88, 1.3, '#ffcf80', 0.3))
for spr, x, y in [('NoboriIndigo', -4.3, -15.3), ('NoboriWhite', 3.6, -14.6), ('NoboriRed', -4.1, -20.8), ('NoboriPurple', 3.6, -20.0)]:
    add(spr, x, y, collide=(0.3, 0.2))
add('WillowBig', -9.4, -9.2, collide=(0.8, 0.45))
add('WillowSmall', 8.6, -8.6, collide=(0.7, 0.4))
add('WillowSmall', -9.3, -21.6, collide=(0.7, 0.4), flip=True)
add('WillowBig', 8.0, -21.4, collide=(0.8, 0.45), flip=True)
add('Crates', -8.9, -18.8, collide=(1.2, 0.5))
add('Bucket', 8.1, -17.6, collide=(0.5, 0.3))
add('Bucket', -8.4, -13.0, collide=(0.5, 0.3))
add('Crates', 7.9, -12.0, collide=(1.2, 0.5), flip=True)

# 东岸鱼市
add('UrumiStall', 27.6, 1.6, collide=(4.0, 1.2))
add('FishRackA', 23.8, 4.0, collide=(3.0, 0.3))
add('FishRackB', 31.6, 3.2, collide=(3.0, 0.3))
add('Crates', 24.9, 0.2, collide=(1.2, 0.5))
add('Bucket', 25.9, -0.3, collide=(0.5, 0.3))
add('Crates', 30.6, 0.4, collide=(1.2, 0.5), flip=True)
add('Bucket', 31.4, -0.3, collide=(0.5, 0.3))
add('ConiferSmall', 34.6, -2.2, collide=(0.8, 0.5))

# 水面：流灯、人魂（顶层，不挡人）
for i, (x, y) in enumerate([(2.8, 6.4), (5.6, 7.7), (-5.6, 6.6), (-8.4, 8.0), (8.2, 9.2), (-1.0, 13.6), (11.0, 7.6)]):
    add(['FloatLanternA', 'FloatLanternB', 'FloatLanternC'][i % 3], x, y, collide=None, shadow=0,
        layer='BelowCharacter', order=0, glow=(0, 0.45, 1.0, '#ffc070', 0.3))
for i, (x, y) in enumerate([(-10.0, 9.2), (-3.6, 7.4), (6.4, 8.2), (12.6, 11.4), (-18.6, 3.2), (-6.0, 1.0), (21.2, 9.6), (-27.0, 12.8)]):
    add(['HitodamaA', 'HitodamaB', 'HitodamaC'][i % 3], x, y + 0.9, collide=None, shadow=0, alpha=0.85,
        layer='Overlay', order=30, flip=i % 2 == 1, glow=(0, 0.4, 1.1, '#bfe8ff', 0.22))

# 写字：精灵像素坐标（中心）、字、颜色、竖排、放大倍数
TEXT = {
    'NoticeBoard': [((50, 39), '三途の渡', '#3a2a1c', False, 1), ((50, 54), '渡賃六文', '#8c2a22', False, 1)],
    'ContestBoard': [((110, 89), '積石大会', '#3a2a1c', False, 2)],
    'Stele': [((31, 40), '三途川', '#4e4c4a', True, 1)],
    'Gate': [((137, 42), '賽の河原', '#3a2a1c', False, 1)],
    'NoboriRed': [((27, 50), '人魂飴', '#f4e8d8', True, 1)],
    'NoboriPurple': [((27, 44), '供養', '#f4e8d8', True, 1)],
    'NoboriIndigo': [((27, 50), '金魚掬', '#f4e8d8', True, 1)],
    'NoboriWhite': [((28, 50), '死後占', '#3a2a1c', True, 1)],
    'StallCandy': [((70, 51), '人', '#f4e8d8', False, 1), ((96, 51), '魂', '#f4e8d8', False, 1), ((122, 51), '飴', '#f4e8d8', False, 1)],
    'StallGoldfish': [((70, 51), '金', '#f4e8d8', False, 1), ((96, 51), '魚', '#f4e8d8', False, 1), ((122, 51), '掬', '#f4e8d8', False, 1)],
    'StallSotoba': [((70, 51), '卒', '#f4e8d8', False, 1), ((96, 51), '塔', '#f4e8d8', False, 1), ((122, 51), '婆', '#f4e8d8', False, 1)],
    'StallFortune': [((70, 51), '死', '#f4e8d8', False, 1), ((96, 51), '後', '#f4e8d8', False, 1), ((122, 51), '占', '#f4e8d8', False, 1)],
}
# 小石碑（无缘塚）单独写字：按缩放后的尺寸
TEXT_SMALL_STELE = [((19, 25), '無縁塚', '#4e4c4a', True, 1)]

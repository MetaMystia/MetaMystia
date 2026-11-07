"""材质试验场：24×16 格，检查水面、岸线、卵石滩、泥土、崖面的质感。"""
import sys

import numpy as np
from scipy import ndimage as ndi

sys.path.insert(0, __file__.rsplit('/maps/', 1)[0])
from dayart.core import (Frame, blend, catmull, clean, poly_mask, ramp, rgb, roughen, shifted, to_image,
                         value_noise)
from dayart.materials import boulder_wall, dab_ground, mottled, pebbles, rim, water

F = Frame(-12, -8, 12, 8)
X, Y = F.grid()
canvas = F.blank()

shore = catmull([(-13, 2.2), (-8, 1.4), (-4, 2.6), (0, 1.8), (4, 2.9), (8, 2.0), (13, 2.4)], 16)
land = poly_mask(F, shore + [(13, -9), (-13, -9)])
land = clean(roughen(land, 5, 30, 1), 1)
water_m = ~land
dist_w = ndi.distance_transform_edt(water_m)
dist_l = ndi.distance_transform_edt(land)

blend(canvas, water(water_m, dist_w, '#2c7086', 3, shallow=('#3a8784', 18), spot='#2a6a82',
                    ripple='#79b8b9', glint='#d6efeb', spot_spacing=110))
# 岸下阴影：紧贴岸线的 2 像素深水
blend(canvas, rgb('#1f5468'), water_m & (dist_w <= 2))

# 卵石滩：底色是灰褐砂砾，卵石密铺；近水更湿更暗
gravel, _ = mottled(land, ramp('#777064', '#857d70', '#91897b'), 5, cell=36, weights=[3, 5, 2])
blend(canvas, gravel)
beach = land & (Y > -4.2 + value_noise(F.shape, 60, 31, 2) * 1.6)
beach = clean(beach, 2)
dens = np.clip(value_noise(F.shape, 120, 11, 2) * 0.7 + 0.45, 0, 1)
wet = np.clip(1 - dist_l / 30, 0, 1)
moss = value_noise(F.shape, 90, 12, 2) > 0.78
big = pebbles(beach, 13, sizes=(9, 16), spacing=26, density=dens * 0.8, wet=wet, moss=moss,
              gap_color='#6d675d', outline='#45434d', contrast=0.75, fill_gap=False)
mid = pebbles(beach, 15, sizes=(5, 9), spacing=10, density=dens, wet=wet, moss=moss,
              gap_color='#6d675d', outline='#4a4852', contrast=0.6, fill_gap=False)
small = pebbles(beach, 17, sizes=(2, 4), spacing=5, density=dens * 0.9, wet=wet,
                gap_color='#6d675d', outline='#55525a', contrast=0.5, fill_gap=False)
blend(canvas, rgb('#6d675d'), beach)
blend(canvas, small)
blend(canvas, mid)
blend(canvas, big)
# 水边一排更大的湿卵石（原版岸线的卵石带）
edge = land & (dist_l < 9)
blend(canvas, pebbles(edge, 14, sizes=(5, 9), spacing=8, wet=np.ones(F.shape) * 0.9,
                      gap_color='#55524c', outline='#33333d'), edge)

# 下方泥土+苔草点
soil = land & ~beach
cover = np.clip(value_noise(F.shape, 70, 41, 3) * 1.4 - 0.25, 0, 1)
blend(canvas, dab_ground(soil, '#7b6a4b', ['#4f5f34', '#5f7039', '#738443', '#8a9a4d'], 42, cover))
rim(canvas, beach, '#5c574e', 1, 3, side='bottom')

# 右侧台地：顶面在上，立面向下挤出 2 格
top = poly_mask(F, catmull([(4.5, -3.5), (5.2, 0.4), (8, 1.2), (12.8, 1.0), (12.8, -3.0), (9, -3.6)], 10, closed=True))
top = clean(roughen(top, 4, 24, 4), 1)
hpx = 96
ext = top.copy()
for k in range(1, hpx + 1):
    ext |= shifted(top, 0, k)
face = ext & ~top
mossf = value_noise(F.shape, 40, 51, 2) > 0.45
blend(canvas, boulder_wall(face, 9, ('#4a4558', '#5f5970', '#7a7489', '#9b95a8'), moss=mossf))
# 立面底部接触阴影
foot = face & ~shifted(face, 0, -1)
blend(canvas, rgb('#2b2733'), ndi.binary_dilation(foot, iterations=1) & ~top)
tcov = np.clip(value_noise(F.shape, 60, 61, 3) * 1.2 + 0.1, 0, 1)
blend(canvas, dab_ground(top, '#6e6448', ['#56693a', '#6a7d40', '#7f914a', '#98a856'], 62, tcov, edge_rim='#a9b56a'))
rim(canvas, top, '#c2c88a', 1, 7, side='bottom')

to_image(canvas).save(sys.argv[1] if len(sys.argv) > 1 else 'test_materials.png')
print('saved', F.W, F.H)

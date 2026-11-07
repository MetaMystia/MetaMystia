"""三途川整图绘制：地面层、物件、前景层（树冠/雾/光），以及可走区域、坡度与出生点。

绘制顺序即图层顺序：远岸 → 河面 → 溪流 → 陆地底色 → 卵石 → 道路与草地 → 台地 → 木构（栈桥、拱桥、鱼栏）
→ 物件投影 → 物件（单独精灵，按 Y 排序）→ 前景树冠 → 雾 → 光。
"""
import math
import sys

import numpy as np
from scipy import ndimage as ndi

HERE = __file__.rsplit('/', 1)[0]
sys.path.insert(0, HERE.rsplit('/maps/', 1)[0])
sys.path.insert(0, HERE)
import layout as L
import props as S
from dayart import props as P
from dayart.core import (Frame, blend, catmull, clean, mode_filter, paste, poly_mask, quantize, ramp, rgb, roughen,
                         scatter, shifted, value_noise)
from dayart.materials import boulder_wall, dab_ground, drift_mask, fog_bands, mottled, pebbles, rim, water
from dayart.sprite import Sprite


class Obj:
    """单独摆放的物件。collide 为相对脚点的矩形 (dx, dy, w, h)（世界格，中心偏移）。"""

    def __init__(self, name, spr, x, y, sort=True, layer='Character', order=0, collide=(), shadow=1.0, alpha=1.0):
        self.name, self.spr, self.x, self.y = name, spr, x, y
        self.sort, self.layer, self.order = sort, layer, order
        self.collide, self.shadow, self.alpha = list(collide), shadow, alpha


class SanzuMap:
    def __init__(self, font=None, seed=7):
        self.F = Frame(L.X0, L.Y0, L.X1, L.Y1)
        self.X, self.Y = self.F.grid()
        self.font, self.seed = font, seed
        self.ground = self.F.blank()
        self.canopy = self.F.blank()
        self.fog = self.F.blank()
        self.glow = self.F.blank()
        self.objects = []
        self.walk_extra = []    # 额外可走的像素遮罩
        self.rng = np.random.default_rng(seed)

    # ------------------------------------------------------------ 工具
    def px(self, x, y):
        return self.F.px(x, y)

    def poly(self, pts, smooth=0, closed=True, width=0):
        if smooth:
            pts = catmull(pts, smooth, closed=closed)
        return poly_mask(self.F, pts, closed=closed, width=width)

    def rect(self, x0, y0, x1, y1):
        return (self.X >= x0) & (self.X < x1) & (self.Y >= y0) & (self.Y < y1)

    def stamp(self, layer, spr, x, y, alpha=1.0):
        """按脚点把精灵直接画进图层（烘焙）。"""
        px, py = self.px(x, y)
        paste(layer, spr.a, px - spr.pivot[0], py - spr.pivot[1], alpha)

    def add(self, *a, **k):
        o = Obj(*a, **k)
        self.objects.append(o)
        return o

    # ------------------------------------------------------------ 区域
    def masks(self):
        X, Y = self.X, self.Y
        s = self.seed
        river_pts = [(-36.6, 10.2), (-30.5, 10.2), (-27.8, 9.2), (-25.8, 7.2), (-24.6, 6.9)] + L.SHORE + L.SHORE_EAST
        river = self.poly(river_pts + [(36.6, 8.6)] + [(36.6, 24.6), (-36.6, 24.6)])
        river = clean(roughen(river, 5, 34, s + 1), 1)
        creek_c = catmull(L.CREEK, 10)
        creek = poly_mask(self.F, creek_c, closed=False, width=L.CREEK_W)
        creek |= poly_mask(self.F, creek_c[-12:], closed=False, width=L.CREEK_W + 1.0)  # 河口放宽
        creek = clean(roughen(creek, 4, 22, s + 2), 1)
        far = self.poly(L.FAR_SHORE + [(36.6, 24.6), (-36.6, 24.6)], smooth=8)
        far = clean(roughen(far, 6, 40, s + 3), 1)
        water = (river | creek) & ~far
        self.river, self.creek, self.far, self.water = river & ~far, creek & ~far, far, water
        self.land = ~water & ~far
        self.dist_land = ndi.distance_transform_edt(water)        # 水中离岸距离
        self.dist_water = ndi.distance_transform_edt(self.land)   # 陆上离水距离

        top = self.poly(L.PLATEAU)
        self.top = top
        hpx = int(L.PLATEAU_FACE * self.F.ppu)
        ext = top.copy()
        for k in range(1, hpx + 1):
            ext |= shifted(top, 0, k)
        self.face = ext & ~top
        g, t = L.STAIRS
        hw = L.STAIRS_W / 2
        self.stair_poly = [(g[0] - hw, g[1]), (g[0] + hw, g[1]), (t[0] + hw, t[1]), (t[0] - hw, t[1])]
        self.stairs = self.poly(self.stair_poly)

        inland = self.poly(L.INLAND + [(36.6, 24.6), (-36.6, 24.6)], smooth=6)
        inland = clean(roughen(inland, 14, 70, s + 4), 2)
        self.beach = self.land & inland & ~top & ~self.face
        self.south = self.land & ~inland & ~top & ~self.face
        so = self.poly(L.SOUTH_OPEN, smooth=6, closed=True)
        self.south_open = clean(roughen(so, 10, 60, s + 5), 2) & self.south
        self.thicket = self.south & ~self.south_open
        road = poly_mask(self.F, catmull(L.ROAD, 8), closed=False, width=L.ROAD_W)
        self.road = clean(roughen(road, 6, 40, s + 6), 1) & self.land & ~top
        trail = np.zeros_like(top)
        for p in L.PATHS:
            trail |= poly_mask(self.F, catmull(p, 8), closed=False, width=1.5)
        self.trail = clean(roughen(trail, 5, 26, s + 7), 1) & self.beach
        self.east_cliff = self.poly(L.EAST_CLIFF, smooth=6)
        self.east_cliff = clean(roughen(self.east_cliff, 8, 50, s + 8), 1) & self.land
        self.market = self.beach & (X > 20.0) & ~self.east_cliff

    # ------------------------------------------------------------ 远岸（彼岸）
    def paint_far(self):
        """彼岸：无边的花原，长年被柔和的暖光包围（求闻史纪；鬼形兽三面为暖色原野与成丛彼岸花）。
        远景只用几道横向花带表现，越远越小越淡，再由雾层罩住。"""
        F, X, Y = self.F, self.X, self.Y
        far = self.far
        dist = ndi.distance_transform_edt(far)          # 离岸线越远 = 越远景
        base = np.where((dist < 30)[..., None], rgb('#5a4445'), np.where((dist < 70)[..., None], rgb('#6a5150'), rgb('#7a6060')))
        blend(self.ground, base, far)
        rows = [(4, 26, 9, ('#7e2c37', '#a8394a', '#c85a5e')), (28, 58, 7, ('#8c4048', '#a85258', '#c27470')),
                (60, 130, 5, ('#9a6064', '#b07a78', '#c89890'))]
        for d0, d1, sp, pal in rows:
            band = far & (dist >= d0) & (dist < d1)
            for x, y in scatter(band, sp, int(d0 * 7 + 1)).astype(int):
                r = max(1, sp // 3)
                c = rgb(pal[int(self.rng.integers(3))])
                self.ground[y - r // 2:y + r // 2 + 1, x - r:x + r + 1, :3] = c
                if d0 < 10 and self.rng.random() < 0.5:   # 近处花带露出花茎
                    self.ground[y + 1:y + 4, x, :3] = rgb('#4f5a38')
        # 岸线：一线浅沙 + 暗色水线
        blend(self.ground, rgb('#a9978c'), far & ~shifted(far, 0, -5))
        blend(self.ground, rgb('#6e5f5c'), far & ~shifted(far, 0, -2))

    # ------------------------------------------------------------ 三途川
    def paint_river(self):
        F, X, Y = self.F, self.X, self.Y
        w = water(self.river, self.dist_land, '#2c7086', 11, shallow=('#3b8986', 16), spot='#296a81',
                  ripple='#7cbabb', glint='#dff4ef', spot_spacing=120)
        blend(self.ground, w)
        # 岸下暗线
        blend(self.ground, rgb('#1f5468'), self.river & (self.dist_land <= 2) & ~self.far)
        # 古代鱼的影子（鬼形兽：河中黑影窜过）
        shadow_col = '#1d4f63'
        for (x, y, kind, ang) in [(-12.5, 10.6, 'fish', 0.2), (5.6, 16.4, 'fish', -0.3), (-24.5, 15.6, 'fish', 0.1),
                                  (19.6, 13.4, 'plesio', 2.9), (-4.0, 13.8, 'school', 0), (27.0, 16.0, 'school', 0),
                                  (12.0, 17.8, 'fish', 0.4)]:
            self.fish_shadow(x, y, kind, ang, shadow_col)
        # 河中长苔的尖石
        for (x, y, sc) in sorted(L.WATER_ROCKS, key=lambda r: -r[1]):
            self.water_rock(x, y, sc)
        # 越往北越被雾气浸染：分段向雾色靠拢（保留细节）
        n = value_noise((F.H, F.W // 8 + 2), 18, 41, 3)
        n = np.repeat(n, 8, axis=1)[:, :F.W]
        t = (Y - 10.0) / 12.0 + (n - 0.5) * 0.18
        band = quantize(t, [0.0, 0.25, 0.5, 0.75])
        band = mode_filter(band, 9, range(5))
        fogc = rgb('#a6c4cd')
        for k, a in ((1, 0.1), (2, 0.2), (3, 0.32), (4, 0.44)):
            m = self.river & (band == k)
            self.ground[m, :3] = self.ground[m, :3] * (1 - a) + fogc * a
        # 彼岸花在水里的倒影：近对岸的水面上几道暗红短波纹
        refl = self.river & (self.dist_land < 80) & (Y > 18.5)
        for x, y in scatter(refl, 18, 44).astype(int):
            L_ = int(self.rng.integers(4, 12))
            self.ground[y, x:x + L_, :3] = self.ground[y, x:x + L_, :3] * 0.55 + rgb('#9a5a5e') * 0.45

    def fish_shadow(self, x, y, kind, ang, col):
        cx, cy = self.px(x, y)
        H, W = self.F.shape
        s = Sprite(320, 200, (160, 100))
        if kind == 'fish':      # 腔棘鱼
            s.ellipse(160, 100, 56, 20, col)
            s.poly([(100, 100), (82, 84), (84, 116)], col)
            s.poly([(84, 100), (66, 88), (70, 100), (66, 112)], col)
            for fx, fy in ((140, 82), (140, 118), (176, 84), (176, 116)):
                s.ellipse(fx, fy, 9, 5, col)
        elif kind == 'plesio':  # 蛇颈龙
            s.ellipse(170, 110, 46, 24, col)
            for fx, fy in ((140, 88), (140, 132), (196, 90), (196, 130)):
                s.ellipse(fx, fy, 16, 6, col)
            s.line([(124, 110), (100, 100), (80, 86), (62, 76)], col, 9)
            s.ellipse(56, 74, 9, 6, col)
            s.poly([(214, 110), (250, 104), (250, 116)], col)
        else:                   # 小鱼群
            rng = np.random.default_rng(abs(int(x * 10 + y * 1000)))
            for _ in range(16):
                fx, fy = rng.normal(160, 26), rng.normal(100, 12)
                s.ellipse(fx, fy, 5, 2, col)
        if ang:
            from PIL import Image
            im = s.image().rotate(math.degrees(ang), resample=Image.NEAREST, expand=False)
            s.a = np.asarray(im).astype(np.float32) / 255
        m = s.a[..., 3] > 0.5
        sub = np.zeros_like(s.a)
        sub[m, :3] = rgb(col)
        sub[m, 3] = 0.5
        x0, y0 = int(cx - 160), int(cy - 100)
        # 只画在河面上
        h, w = sub.shape[:2]
        ys, xs = slice(max(0, y0), min(H, y0 + h)), slice(max(0, x0), min(W, x0 + w))
        part = sub[ys.start - y0:ys.stop - y0, xs.start - x0:xs.stop - x0].copy()
        part[..., 3] *= self.river[ys, xs]
        blend(self.ground[ys, xs], part)

    def water_rock(self, x, y, sc):
        """长苔的尖石：几块尖锥岩 + 苔顶 + 水线白沫 + 倒影。"""
        rng = np.random.default_rng(abs(int(x * 100 + y * 7000)))
        sc = sc * 1.9
        W, H = int(150 * sc) + 60, int(150 * sc) + 40
        s = Sprite(W, H, (W / 2, H - 22))
        light, mid, dark, deep, out = '#7d778d', '#5d5770', '#463f58', '#332d43', '#1d1928'
        moss, moss_l = '#4f6e3c', '#78994f'
        spires = []
        for k in range(int(rng.integers(3, 6))):
            bx = W / 2 + rng.normal(0, 26 * sc)
            bw = rng.uniform(26, 52) * sc
            bh = rng.uniform(56, 128) * sc * (1.0 if k == 0 else rng.uniform(0.45, 0.8))
            spires.append((bx, bw, bh))
        spires.sort(key=lambda t: -t[2])
        base_y = H - 22
        for bx, bw, bh in spires:
            apex = (bx + rng.uniform(-bw * 0.35, bw * 0.25), base_y - bh)
            left, right = (bx - bw / 2, base_y), (bx + bw / 2, base_y)
            # 轮廓：中段外鼓、局部凹进的锯齿边
            jag_l = [(left[0] + (apex[0] - left[0]) * t - math.sin(t * math.pi) * bw * 0.12 + rng.uniform(-4, 4) * sc,
                      left[1] + (apex[1] - left[1]) * t) for t in np.linspace(0, 1, 9)]
            jag_r = [(apex[0] + (right[0] - apex[0]) * t + math.sin(t * math.pi) * bw * 0.12 + rng.uniform(-4, 4) * sc,
                      apex[1] + (right[1] - apex[1]) * t) for t in np.linspace(0, 1, 9)]
            ridge_x = apex[0] + bw * 0.08
            s.poly(jag_l + jag_r, mid)
            s.poly([apex] + jag_r + [(ridge_x, base_y)], dark)                 # 右侧背光面
            s.poly([jag_l[0], jag_l[2], (ridge_x - bw * 0.15, base_y - bh * 0.3), (ridge_x - bw * 0.2, base_y)], light)
            for k in range(3):                                                 # 横向岩纹
                yy = base_y - bh * rng.uniform(0.15, 0.7)
                s.line([(bx - bw * 0.3, yy), (bx + bw * 0.3, yy + rng.uniform(-3, 3))], deep, 1, 0.6)
            # 苔顶：尖端以下一段覆盖苔藓，边缘下垂
            mt = np.zeros((H, W), bool)
            for yy in range(int(apex[1]), int(apex[1] + bh * rng.uniform(0.32, 0.5))):
                t = (yy - apex[1]) / bh
                half = bw / 2 * t * 1.05
                xl, xr = int(apex[0] - half), int(apex[0] + half)
                mt[yy, max(0, xl):max(0, xr)] = True
            for _ in range(int(bw / 4)):                                       # 苔藓垂下
                dx = rng.uniform(-bw * 0.3, bw * 0.3)
                yy0 = int(apex[1] + bh * 0.3)
                mt[yy0:yy0 + int(rng.integers(3, 10)), int(apex[0] + dx)] = True
            body = s.alpha_mask()
            s.put(mt & body, moss)
            s.put(mt & body & (value_noise((H, W), 4, abs(int(x * 13 + y * 977)), 1) > 0.55), moss_l)
        body = s.alpha_mask()
        wet = body & (np.arange(H)[:, None] > base_y - 7 * sc)              # 水线附近湿暗
        s.put(wet, deep)
        s.outline(out)
        # 倒影（压扁、变暗、断续）
        bx0, by0 = self.px(x, y)
        refl = s.a[:base_y][::-1]
        refl = refl[::2]
        rh = refl.shape[0]
        R = np.zeros_like(refl)
        m = refl[..., 3] > 0.5
        rows = (np.arange(rh) % 3) != 2
        m &= rows[:, None]
        R[m, :3] = rgb('#1f4e60')
        R[m, 3] = 0.55
        paste(self.ground, R, bx0 - s.pivot[0], by0, 1.0)
        self.stamp(self.ground, s, x, y)
        # 水线白沫
        foam = Sprite(W, 16, (W / 2, 8))
        for k in range(int(26 * sc) + 8):
            fx = W / 2 + rng.normal(0, 30 * sc)
            fy = 8 + rng.normal(0, 2)
            foam.rect(fx, fy, int(rng.integers(2, 6)), 1, '#cfeae6')
        self.stamp(self.ground, foam, x, y - 0.04)

    # ------------------------------------------------------------ 溪流
    def paint_creek(self):
        c = self.creek & ~self.river
        d = ndi.distance_transform_edt(self.creek)
        w = water(self.creek, d, '#3b8a8c', 51, shallow=('#4f9c94', 10), spot='#33797d', ripple='#a6d8d0',
                  glint='#e8fbf6', spot_spacing=60)
        blend(self.ground, w, c)
        # 水底卵石若隐若现
        p = pebbles(c & (d > 6), 52, sizes=(3, 7), spacing=11, density=np.full(self.F.shape, 0.5),
                    palette=[('#5d8f8a', '#79a8a1', '#467671'), ('#6b8f86', '#88aba0', '#557a72')],
                    outline='#3a6a68', contrast=0.5, fill_gap=False)
        blend(self.ground, p, None, 0.55)
        blend(self.ground, rgb('#24606a'), c & (d <= 2))

    # ------------------------------------------------------------ 陆地
    def paint_land(self):
        F, X, Y = self.F, self.X, self.Y
        land = self.land
        # 南侧泥土 + 苔草；灌丛地面更暗
        cover = np.clip(value_noise(F.shape, 80, 61, 3) * 1.3 - 0.1, 0, 1)
        soil = self.south | self.top
        g = dab_ground(soil, '#625440', ['#465532', '#556537', '#66763e', '#7a8a46'], 62, cover)
        blend(self.ground, g)
        th = self.thicket | (self.east_cliff & self.land)
        blend(self.ground, dab_ground(th, '#3a3f2c', ['#283829', '#2f4430', '#3a5236', '#4b6440'], 63,
                                      np.clip(cover + 0.4, 0, 1)), th)
        # 河原砾石底
        gravel, _ = mottled(self.beach, ramp('#757065', '#837c70', '#8f877a'), 64, cell=40, weights=[3, 5, 2])
        blend(self.ground, gravel)
        # 卵石：西侧赛之河原最密，小路与内陆边缘稀疏，近水更湿
        dens = np.clip(value_noise(F.shape, 140, 65, 2) * 0.6 + 0.5, 0, 1)
        dens = np.where(X < -6, np.minimum(1, dens + 0.15), dens)
        d_in = ndi.distance_transform_edt(self.beach)
        dens = dens * np.clip(d_in / 40, 0.25, 1)
        dens = np.where(self.trail, dens * 0.18, dens)
        sandy = (value_noise(F.shape, 110, 77, 2) > 0.72) & (X > -6)   # 东侧零星砂地
        dens = np.where(sandy, dens * 0.35, dens)
        wet = np.clip(1 - self.dist_water / 34, 0, 1)
        moss = (value_noise(F.shape, 100, 66, 2) > 0.74) & (self.dist_water > 40)
        beach = self.beach
        blend(self.ground, pebbles(beach, 67, sizes=(2, 4), spacing=5, density=dens * 0.9, wet=wet,
                                   outline='#55525a', contrast=0.5, fill_gap=False))
        blend(self.ground, pebbles(beach, 68, sizes=(5, 9), spacing=10, density=dens, wet=wet, moss=moss,
                                   outline='#4a4852', contrast=0.6, fill_gap=False))
        blend(self.ground, pebbles(beach, 69, sizes=(9, 16), spacing=26, density=dens * 0.75, wet=wet, moss=moss,
                                   outline='#45434d', contrast=0.75, fill_gap=False))
        edge = beach & (self.dist_water < 9)
        blend(self.ground, pebbles(edge, 70, sizes=(5, 9), spacing=8, wet=np.ones(F.shape) * 0.9,
                                   gap_color='#55524c', outline='#33333d'), edge)
        # 小路：砂地 + 零星小石
        sand, _ = mottled(self.trail, ramp('#8d8472', '#998f7c', '#a59a86'), 71, cell=30, weights=[2, 5, 3])
        blend(self.ground, sand, self.trail & (value_noise(F.shape, 12, 72, 2) > 0.25))
        # 近水的小水洼：映着雾色的天光
        for k, (px_, py_) in enumerate(scatter(beach & (self.dist_water > 22) & (self.dist_water < 110) & ~self.trail, 260, 78)):
            if self.rng.random() < 0.4:
                continue
            rx, ry = self.rng.uniform(8, 22), self.rng.uniform(4, 8)
            pud = Sprite(int(rx * 2 + 4), int(ry * 2 + 4), (rx + 2, ry + 2))
            pud.ellipse(rx + 2, ry + 2, rx, ry, '#4f7f8a')
            pud.ellipse(rx + 1, ry + 1, rx - 2, ry - 2, '#6f9ea6')
            pud.rect(rx * 0.6, ry * 0.6 + 1, rx * 0.8, 1, '#c3e0e2')
            pud.outline('#3e4a52', sides='bottom')
            paste(self.ground, pud.a, px_ - pud.pivot[0], py_ - pud.pivot[1])
        # 河原与泥土交界：一圈深色边
        rim(self.ground, beach, '#5d584e', 1, 73, side='bottom')
        # 道路（中有之道）：夯实的浅色土
        road = self.road & ~beach
        rd, _ = mottled(road, ramp('#8f7a5c', '#9d8767', '#ab9574', '#b9a382'), 74, cell=34, weights=[1, 3, 4, 2])
        blend(self.ground, rd)
        rim(self.ground, road, '#6f5d45', 2, 75)
        # 车辙：两条断续的深色细线沿路延伸；路面碎石与草屑
        for off in (-0.9, 0.8):
            rut = poly_mask(F, catmull([(x + off, y) for x, y in L.ROAD], 8), closed=False, width=0.12)
            rut &= road & (value_noise(F.shape, 16, 79 + int(off * 10), 2) > 0.35)
            blend(self.ground, rgb('#806a4f'), rut)
        blend(self.ground, pebbles(road, 80, sizes=(2, 4), spacing=9, density=np.full(F.shape, 0.6),
                                   palette=[('#9c8f7c', '#b5a892', '#7a6e5e'), ('#8f8574', '#aaa08c', '#706658')],
                                   outline='#6a5c4a', contrast=0.6, fill_gap=False))
        edge_dust = road & (ndi.distance_transform_edt(road) < 10)
        blend(self.ground, rgb('#8a7558'), edge_dust & (value_noise(F.shape, 8, 89, 2) > 0.45))
        # 冠木门前后一段铺石（踏石）
        for k, (x, y) in enumerate(catmull([(0.4, -12.8), (0.3, -10.6), (0.0, -8.6), (-0.4, -6.8)], 3)):
            for dx in (-0.55, 0.55):
                w, h = self.rng.uniform(0.8, 1.05), self.rng.uniform(0.5, 0.62)
                st = P.boulder(int(w * F.ppu), int(h * F.ppu), 1200 + k * 3 + int(dx > 0), front=0.22, facet=False,
                               pal=('#c9c4b6', '#b1ac9e', '#8e897d', '#706b61'), outline='#4f4a42')
                self.stamp(self.ground, st, x + dx + self.rng.uniform(-0.1, 0.1), y)

    def paint_flowers(self):
        """彼岸花成片开放：花丛中心密集到看不见花茎，边缘稀疏、露出花茎。只有茎没有叶。"""
        F = self.F
        d_th = ndi.distance_transform_edt(~self.thicket)
        d_road = ndi.distance_transform_edt(~self.road)
        zone = (self.south_open & (d_th < 70)) | (self.south_open & (d_road < 26) & (d_road > 3))
        zone |= self.top & ~self.plateau_path
        zone |= self.beach & (ndi.distance_transform_edt(self.beach) < 30) & (self.Y < -4)
        zone &= ~self.road
        drifts = drift_mask(zone, 81, cell=56, cover=0.42)
        core = ndi.binary_erosion(drifts, iterations=6)
        # 花丛底色：深绿茎秆 + 暗红
        blend(self.ground, rgb('#3e4f2c'), drifts & (value_noise(F.shape, 6, 84, 1) > 0.35))
        blend(self.ground, rgb('#5e1a24'), core & (value_noise(F.shape, 5, 85, 1) > 0.5))
        heads = [P.higanbana_head(v) for v in range(3)]
        heads += [h.flip() for h in heads]
        pts = scatter(core, 8, 86)
        pts = np.concatenate([pts, scatter(drifts & ~core, 15, 87)])
        pts = pts[np.argsort(pts[:, 1])]
        for x, y in pts:
            h = heads[int(self.rng.integers(len(heads)))]
            if not core[int(y), int(x)]:  # 边缘的花露出花茎
                stem = int(self.rng.integers(5, 9))
                for k in range(stem):
                    if 0 <= int(y) - k < F.H:
                        self.ground[int(y) - k, int(x), :3] = rgb('#4f7a37')
                y -= stem
            paste(self.ground, h.a, x - h.w / 2, y - h.h + 2)
        # 零散单丛
        for x, y in scatter(zone & ~drifts, 60, 88):
            spr = P.higanbana_clump(int(self.rng.integers(1 << 20)), n=int(self.rng.integers(2, 4)), scale=0.8)
            paste(self.ground, spr.a, x - spr.pivot[0], y - spr.pivot[1])
        # 草丛
        for x, y in scatter(self.south_open & ~self.road & ~drifts, 22, 83).astype(int):
            spr = P.grass_tuft(int(self.rng.integers(1 << 20)))
            paste(self.ground, spr.a, x - spr.pivot[0], y - spr.pivot[1])

    # ------------------------------------------------------------ 台地
    def paint_plateau(self):
        F, X, Y = self.F, self.X, self.Y
        top, face = self.top, self.face
        mossf = value_noise(F.shape, 40, 91, 2) > 0.45
        # 东缘侧面：顶面向右挤出一条窄立面（背光，更暗），让台地与河原之间有高差
        side = top.copy()
        for k in range(1, 15):
            side |= shifted(top, k, 0)
        side &= ~top & ~face & self.land & (Y > -9.2)
        self.side = side
        blend(self.ground, boulder_wall(side, 97, ('#3c3748', '#4c4659', '#605a70', '#77718a'), row_h=(10, 22),
                                        width=(8, 16), moss=mossf))
        wall = boulder_wall(face, 92, ('#4b4659', '#605a71', '#7b758a', '#9c96a9'), moss=mossf)
        blend(self.ground, wall)
        foot = face & ~shifted(face, 0, -1)
        blend(self.ground, rgb('#2a2632'), ndi.binary_dilation(foot, iterations=2) & ~top & ~face)
        tcov = np.clip(value_noise(F.shape, 46, 93, 3) * 1.25 + 0.05, 0, 1)
        blend(self.ground, dab_ground(top, '#5f5a42', ['#4a5c34', '#5a6d3a', '#6c7f42', '#829349'], 94, tcov,
                                      spacing=3.6, solid=0.66))
        # 顶面东缘的亮边（台地边缘受光）
        east_lip = top & ~shifted(top, -3, 0)
        blend(self.ground, rgb('#a6ad72'), east_lip)
        # 台地南缘亮边
        lip = top & ~shifted(top, 0, -3)
        blend(self.ground, rgb('#b9c07e'), lip & (Y < -8.5))
        # 台地上的土路：石阶顶 → 衣领树 → 紫樱
        path = poly_mask(F, catmull([(-28.6, -8.6), (-29.4, -5.0), (-29.8, -1.0), (-30.0, 0.8)], 8), closed=False, width=1.3)
        path |= poly_mask(F, catmull([(-29.2, -5.2), (-31.4, -6.4), (-33.6, -7.6)], 8), closed=False, width=1.1)
        path = clean(roughen(path, 4, 20, 95), 1) & top
        pth, _ = mottled(path, ramp('#8d7b5e', '#9b8868', '#a99673'), 96, cell=24)
        blend(self.ground, pth)
        self.plateau_path = path
        # 岬角：台地临水的一圈岩石（崖岸朝北看不到立面，用岩石轮廓表现高差）
        wet_edge = top & ndi.binary_dilation(self.water, iterations=10)
        for k, (px_, py_) in enumerate(scatter(wet_edge, 30, 98)):
            x, y = F.world(px_, py_)
            w = int(self.rng.integers(30, 58))
            h = int(w * self.rng.uniform(0.6, 0.85))
            spr = P.boulder(w, h, 1500 + k, pal=('#a9a3b6', '#8b8599', '#6b6579', '#4e485c'), outline='#2c2736',
                            moss=0.45 if self.rng.random() < 0.6 else 0)
            self.stamp(self.ground, spr, x, y + 0.1)
        # 石阶
        self.paint_stairs()

    def paint_stairs(self):
        F = self.F
        band = self.stairs
        (gx, gy), (tx, ty) = L.STAIRS
        steps = 10
        stone_top, stone_riser, stone_dark, out = rgb('#b3ad9f'), rgb('#7d776c'), rgb('#5b564e'), rgb('#3a3632')
        blend(self.ground, stone_riser, band)
        for k in range(steps):
            t0, t1 = k / steps, (k + 1) / steps
            yb, yt = gy + (ty - gy) * t0, gy + (ty - gy) * t1
            xb = gx + (tx - gx) * t0
            xt = gx + (tx - gx) * t1
            # 每级台阶：上方为踏面，下方 5 像素为踢面
            step = poly_mask(F, [(xb - L.STAIRS_W / 2, yb), (xb + L.STAIRS_W / 2, yb), (xt + L.STAIRS_W / 2, yt),
                                 (xt - L.STAIRS_W / 2, yt)]) & band
            riser = step & ~shifted(step, 0, 6)
            tread = step & ~riser
            blend(self.ground, stone_top, tread)
            blend(self.ground, stone_riser, riser)
            blend(self.ground, rgb('#cbc5b6'), tread & ~shifted(tread, 0, 1))      # 踏面前缘高光
            blend(self.ground, stone_dark, riser & ~shifted(riser, 0, -1))
        # 两侧：右侧挡墙暗面、左侧描边
        right = band & ~shifted(band, -5, 0)
        blend(self.ground, stone_dark, right)
        blend(self.ground, out, band & ~shifted(band, 1, 0))
        blend(self.ground, out, band & ~shifted(band, -1, 0))

    # ------------------------------------------------------------ 木构：栈桥、拱桥、鱼栏
    def planks(self, mask, direction, pal, seed, gap=6):
        """在遮罩内铺木板。direction='h' 横缝、'v' 竖缝。pal=(亮, 中, 暗, 描边)。"""
        F = self.F
        rng = np.random.default_rng(seed)
        light, mid, dark, out = (rgb(c) for c in pal)
        blend(self.ground, mid, mask)
        yy, xx = np.mgrid[0:F.H, 0:F.W]
        if direction == 'h':
            seam = (yy % gap == gap - 1)
            hi = (yy % gap == 0)
        else:
            seam = (xx % gap == gap - 1)
            hi = (xx % gap == 0)
        blend(self.ground, dark, mask & seam)
        blend(self.ground, light, mask & hi, 0.7)
        # 木板端头接缝与木纹
        n = value_noise(F.shape, 9, seed, 2)
        blend(self.ground, dark, mask & (n > 0.88) & ~seam, 0.3)
        e = mask & ~ndi.binary_erosion(mask)
        blend(self.ground, out, e)

    def paint_pier(self):
        F, X, Y = self.F, self.X, self.Y
        x0, y0, x1, y1 = L.PIER
        hx0, hy0, hx1, hy1 = L.PIER_HEAD
        deck = self.rect(x0, y0, x1, y1) | self.rect(hx0, hy0, hx1, hy1)
        wood = ('#a48462', '#86684b', '#5f4936', '#3d2e24')
        # 水面上的阴影（光从左上，影落右下）
        sh = shifted(deck, 10, 8) & ~deck & self.water
        blend(self.ground, rgb('#1d4b5d'), sh, 0.75)
        # 桩：沿两侧每 1.4 格一根，露出水面
        for yy in np.arange(y0 + 1.0, y1, 1.4):
            for xx in (x0 - 0.08, x1 - 0.06):
                self.post_in_water(xx, yy)
        for xx in np.arange(hx0 + 0.1, hx1, 1.45):
            self.post_in_water(xx, hy0 - 0.02)
        # 端部平台朝南的侧面
        front = self.rect(hx0, hy0 - 0.2, hx1, hy0) & ~self.rect(x0, hy0 - 0.2, x1, hy0)
        blend(self.ground, rgb(wood[2]), front)
        blend(self.ground, rgb(wood[3]), front & ~shifted(front, 0, -1))
        self.planks(deck, 'h', wood, 101)
        # 两侧压边木
        for xa in (x0, x1 - 0.12):
            blend(self.ground, rgb(wood[2]), self.rect(xa, y0, xa + 0.12, y1) & deck)
        self.deck = deck | front

    def post_in_water(self, x, y):
        s = Sprite(12, 26, (6, 22))
        s.rect(3, 2, 6, 20, '#6e543e')
        s.rect(3, 2, 2, 20, '#8f7154')
        s.rect(7, 2, 2, 20, '#4b3a2c')
        s.rect(2, 0, 8, 3, '#5a4534')
        s.outline('#33261e')
        ring = Sprite(20, 6, (10, 3))
        ring.rect(1, 2, 5, 1, '#bfe2dc')
        ring.rect(12, 3, 6, 1, '#bfe2dc')
        self.stamp(self.ground, ring, x, y - 0.05)
        self.stamp(self.ground, s, x, y)

    def bridge_profile(self, x):
        """拱桥桥面的抬升（格）：左 2 格每格 +0.5，中间 1 格平，右 2 格每格 -0.5。"""
        x0 = L.BRIDGE[0]
        t = x - x0
        if t < 2:
            return 0.5 * max(0.0, t)
        if t < 3:
            return 1.0
        return max(0.0, 1.0 - 0.5 * (t - 3))

    def paint_bridge(self):
        F, X, Y = self.F, self.X, self.Y
        x0, y0, x1, y1 = L.BRIDGE
        # 美术用平滑拱形；行走用 bridge_profile 的分段坡度（两者最大相差约 0.1 格）
        t = np.clip((X - x0) / (x1 - x0), 0, 1)
        rise = L.BRIDGE_RISE * np.sin(t * math.pi)
        band = (X >= x0) & (X < x1) & (Y >= y0 + rise) & (Y < y1 + rise)
        # 桥身南侧立面：朱红拱梁，梁下是拱形桥洞的暗影
        under = (X >= x0) & (X < x1) & (Y >= y0 + rise - 0.5) & (Y < y0 + rise)
        beam = under & (Y >= y0 + rise - 0.22)
        blend(self.ground, rgb('#9a4036'), beam)
        blend(self.ground, rgb('#c25a4c'), beam & ~shifted(beam, 0, 2))
        hole_top = y0 + rise - 0.22
        hole = (X >= x0 + 0.5) & (X < x1 - 0.5) & (Y < hole_top) & (Y >= y0 - 0.55 + rise * 0.0) & self.creek
        blend(self.ground, rgb('#1a3f47'), hole, 0.8)
        side = under & ~beam & ~hole
        blend(self.ground, rgb('#6e2c26'), side)
        sh = self.creek & (X >= x0) & (X < x1) & (Y < y0 - 0.55) & (Y > y0 - 1.5)
        blend(self.ground, rgb('#1e4f58'), sh, 0.5)
        # 桥墩
        for xx in (x0 + 0.9, x1 - 0.9):
            self.post_in_water(xx, y0 - 0.5)
        wood = ('#b6926a', '#94724f', '#6a503a', '#3f2e24')
        self.planks(band, 'v', wood, 111, gap=7)
        # 拱的明暗：上坡段迎光略亮，下坡段背光略暗，拱顶一线高光
        up = band & (X < x0 + 2.0)
        down = band & (X >= x0 + 3.0)
        crest = band & (X >= x0 + 2.2) & (X < x0 + 2.8)
        self.ground[up, :3] = np.minimum(1, self.ground[up, :3] * 1.08 + 0.02)
        self.ground[down, :3] *= 0.84
        blend(self.ground, rgb('#d8b78c'), crest & ((np.arange(F.W)[None, :] % 7) == 1), 0.6)
        # 北侧栏杆（在人物后方，直接画进地面层）
        rail = S.bridge_rail((x1 - x0) * F.ppu, L.BRIDGE_RISE * F.ppu)
        self.stamp(self.ground, rail, (x0 + x1) / 2, y1 + 0.05)
        self.bridge_band = band | under
        # 南侧栏杆作为物件（脚点取桥头南缘，保证站在桥上的角色在其后）
        self.add('BridgeRailSouth', S.bridge_rail((x1 - x0) * F.ppu, L.BRIDGE_RISE * F.ppu), (x0 + x1) / 2, y0 - 0.06,
                 shadow=0)

    def paint_pen(self):
        F, X, Y = self.F, self.X, self.Y
        x0, y0, x1, y1 = L.FISH_PEN
        inside = self.rect(x0, y0, x1, y1) & self.water
        self.ground[inside, :3] *= 0.86
        for (x, y) in ((25.6, 10.6), (28.8, 9.6)):
            self.fish_shadow(x, y, 'fish', 0.15, '#1a4558')
        for xx in np.arange(x0, x1 + 0.01, 0.55):
            for yy in (y0, y1):
                self.post_in_water(xx, yy)
        for yy in np.arange(y0 + 0.55, y1, 0.55):
            for xx in (x0, x1):
                self.post_in_water(xx, yy)
        # 栈道
        jx0, jy0, jx1, jy1 = L.MARKET_JETTY
        jetty = self.rect(jx0, jy0, jx1, jy1)
        sh = shifted(jetty, 8, 6) & ~jetty & self.water
        blend(self.ground, rgb('#1d4b5d'), sh, 0.7)
        self.planks(jetty, 'h', ('#9a8a74', '#7a6b58', '#57493c', '#3a3028'), 121, gap=5)
        self.jetty = jetty

    # ------------------------------------------------------------ 物件
    def place_objects(self):
        F, f = self.F, self.font
        rng = self.rng
        A = self.add
        # 渡口
        A('KomachiBoat', S.boat(), *L.BOAT, collide=[(0, 0.3, 5.0, 1.0)], shadow=0)
        for i, (x, y) in enumerate(L.MOORING):
            A(f'Mooring{i}', S.pole(30), x, y, collide=[(0, 0, 0.2, 0.15)], shadow=0.6)
        A('FerrySign', S.stone_sign(f), *L.SIGN, collide=[(0, 0.1, 1.2, 0.3)])
        A('PierLanternPole', S.lantern_pole(), L.PIER_HEAD[0] + 0.35, L.PIER_HEAD[3] - 0.25, collide=[(0, 0.05, 0.25, 0.2)])
        for i, (x, y) in enumerate(L.STONE_LANTERNS):
            A(f'PierLantern{i}', P.stone_lantern(True, seed=i), x, y, collide=[(0, 0.15, 0.6, 0.35)])
        A('FerryBell', S.bell_frame(), *L.BELL, collide=[(-0.36, 0.1, 0.2, 0.2), (0.36, 0.1, 0.2, 0.2)])
        A('Shelter', S.shelter(), *L.SHELTER, collide=[(0, 0.95, 4.3, 1.7)])
        A('KomachiRock', P.boulder(96, 46, 5, pal=('#c7c3cc', '#a8a3b0', '#86808f', '#655f70'), outline='#3e3846', moss=0.3),
          *L.KOMACHI_ROCK, collide=[(0, 0.35, 1.8, 0.6)])
        # 赛之河原：积石、卒塔婆、风车、大石
        x0, y0, x1, y1 = L.TOWER_FIELD
        field = self.rect(x0, y0, x1, y1) & self.beach & ~self.trail
        cx0, cy0, cx1, cy1 = L.CONTEST[0], L.CONTEST[1] - 2.6, L.CONTEST[2], L.CONTEST[3] + 0.2
        for k, (px_, py_) in enumerate(scatter(field, int(1.55 * F.ppu), 131)):
            x, y = F.world(px_, py_)
            big = rng.random() < 0.35
            spr = P.stone_stack(1000 + k, n=int(rng.integers(4, 8)) if big else None, base_w=int(rng.integers(20, 28)) if big else None)
            A(f'Stack{k}', spr, x, y, collide=[(0, 0.05, 0.4, 0.2)] if big else [])
        for k, (px_, py_) in enumerate(scatter(field, int(2.6 * F.ppu), 132)):
            x, y = F.world(px_, py_)
            x += 0.5
            A(f'Pinwheel{k}', P.pinwheel(['#d8333c', '#3f9a54'][k % 2], '#f1e6d0' if k % 3 else '#e8c24a'), x, y, shadow=0.4)
        for k, (px_, py_) in enumerate(scatter(field, int(3.4 * F.ppu), 133)):
            x, y = F.world(px_, py_)
            A(f'Sotoba{k}', P.sotoba(200 + k), x - 0.4, y + 0.3, collide=[(0, 0, 0.18, 0.12)], shadow=0.5)
        # 积石大赛
        (bx0, by0, bx1, by1) = L.CONTEST
        A('ContestPoleL', S.pole(110), bx0, by0, collide=[(0, 0, 0.2, 0.15)])
        A('ContestPoleR', S.pole(110), bx1, by1, collide=[(0, 0, 0.2, 0.15)])
        A('ContestBoard', S.contest_board(f), bx0 - 0.9, by0 - 0.3, collide=[(0, 0, 0.3, 0.15)])
        span = math.hypot(bx1 - bx0, by1 - by0) * F.ppu
        pen = S.pennant_line(span)
        pen.pivot = (0, 0)
        A('ContestPennants', pen, bx0 + 0.02, by0 + 110 / F.ppu - 0.08, sort=False, layer='Overlay', order=5, shadow=0)
        # 六地藏
        A('JizoRow', S.jizo_row(3), *L.JIZO_ROW, collide=[(0, 0.35, 3.5, 0.6)])
        for k in range(5):
            A(f'JizoSotoba{k}', P.sotoba(300 + k), L.JIZO_ROW[0] - 1.6 + k * 0.8, L.JIZO_ROW[1] + 0.9, shadow=0.3)
        # 河原上的大石
        keep_clear = ndi.binary_dilation(self.trail | self.stairs, iterations=40) | self.rect(-26.0, -13.0, -19.0, -9.0)
        for (cx, cy, hw, hh) in ((L.SKELETON[0], L.SKELETON[1] + 0.8, 5.4, 2.0), (L.SHELTER[0], L.SHELTER[1] + 1.2, 2.8, 1.8),
                                 (L.WRECK[0], L.WRECK[1] + 0.4, 2.6, 1.2), (L.KOMACHI_ROCK[0], L.KOMACHI_ROCK[1], 1.6, 1.0)):
            keep_clear |= self.rect(cx - hw, cy - hh, cx + hw, cy + hh)
        self.keep_clear = keep_clear
        for k, (px_, py_) in enumerate(scatter(self.beach & ~keep_clear & ~field & (self.dist_water > 30) & ~self.market,
                                               int(3.4 * F.ppu), 134)):
            x, y = F.world(px_, py_)
            if rng.random() < 0.45:
                continue
            w = int(rng.integers(26, 70))
            h = int(w * rng.uniform(0.55, 0.75))
            spr = P.boulder(w, h, 400 + k, pal=('#c4c0c6', '#a5a0a8', '#837e88', '#625d69'), outline='#3d3844',
                            moss=0.35 if rng.random() < 0.4 else 0)
            A(f'Rock{k}', spr, x, y, collide=[(0, h / F.ppu * 0.3, w / F.ppu * 0.85, h / F.ppu * 0.5)])
        # 半埋的蛇颈龙骨架、搁浅的漏船、石碑
        A('PlesioSkeleton', S.skeleton(), *L.SKELETON, collide=[(1.2, 0.6, 5.2, 0.9), (-2.6, 1.6, 1.6, 0.8)])
        A('WreckedBoat', S.wrecked_boat(), *L.WRECK, collide=[(0, 0.5, 3.9, 0.9)])
        A('SanzuStele', S.stele(f), *L.STELE, collide=[(0, 0.1, 0.8, 0.3)])
        # 河原东半也有零星积石
        for k, (px_, py_) in enumerate(scatter(self.beach & ~self.trail & ~self.keep_clear & (self.X > -5) & (self.X < 16)
                                               & (self.dist_water > 60) & (self.Y < 2), int(2.4 * F.ppu), 135)):
            x, y = F.world(px_, py_)
            A(f'StackE{k}', P.stone_stack(2000 + k), x, y, collide=[])
        # 中有之道：摊位、石灯笼、柳、冠木门
        for i, (kind, x, y) in enumerate(L.STALLS):
            A(f'Stall_{kind}', S.stall(kind, 40 + i, f), x, y, collide=[(0, 0.65, 3.4, 1.3)])
        for i, (x, y) in enumerate([(-3.4, -10.6), (3.4, -10.4), (-3.8, -16.8), (3.4, -16.6), (-3.6, -22.4), (3.0, -22.2)]):
            A(f'RoadLantern{i}', P.stone_lantern(True, seed=10 + i), x, y, collide=[(0, 0.15, 0.6, 0.35)])
        for i, (x, y) in enumerate(L.WILLOWS):
            A(f'Willow{i}', P.willow(20 + i), x, y, collide=[(0, 0.1, 0.5, 0.3)])
        gx, gy, span = L.GATE
        lpost, rpost = S.gate_posts()
        A('GatePostL', lpost, gx - span / 2, gy, collide=[(0, 0, 0.3, 0.2)])
        A('GatePostR', rpost, gx + span / 2, gy, collide=[(0, 0, 0.3, 0.2)])
        beam = S.gate_beam(int(span * F.ppu), f)
        A('GateBeam', beam, gx, gy + 2.16, sort=False, layer='Overlay', order=6, shadow=0)
        for k, (x, y, t, c) in enumerate([(-4.4, -16.4, '人魂飴', '#4a5fa0'), (3.9, -15.4, '供養', '#6b4a7a'),
                                          (-4.2, -22.6, '中有', '#c0303a'), (3.7, -21.8, '金魚', '#c0303a')]):
            A(f'Nobori{k}', S.nobori(t, c, f), x, y, collide=[(0, 0, 0.2, 0.15)], shadow=0.4)
        A('BenchParasol', S.bench_parasol(f), -8.6, -16.6, collide=[(0, 0.35, 1.9, 0.45), (0, 0.9, 0.2, 0.2)])
        for k, (x, y) in enumerate([(-8.6, -19.0), (-8.9, -18.4), (8.0, -17.6), (-8.3, -13.0), (7.7, -12.1)]):
            spr = S.barrel(k) if k % 2 == 0 else S.crate(k)
            A(f'StallGoods{k}', spr, x, y, collide=[(0, 0.15, 0.6, 0.3)])
        # 台地：衣领树、紫樱、路标
        A('KimonoTree', S.kimono_tree(), *L.KIMONO_TREE, collide=[(0, 0.2, 1.3, 0.6)])
        A('PurpleSakura', S.purple_sakura(), *L.PURPLE_SAKURA, collide=[(0, 0.15, 0.6, 0.35)])
        A('MuenSign', S.signpost(f), *L.MUEN_SIGN, collide=[(0, 0.05, 0.25, 0.15)])
        for k, (x, y) in enumerate([(-24.6, 1.6), (-25.4, 5.6), (-24.2, -3.6), (-33.8, 4.4), (-35.0, -2.0)]):
            spr = P.boulder(int(rng.integers(40, 70)), int(rng.integers(26, 40)), 500 + k, moss=0.4,
                            pal=('#b9b4c0', '#9a95a3', '#7a7584', '#5c5766'), outline='#36313f')
            A(f'KnollRock{k}', spr, x, y, collide=[(0, 0.25, 1.1, 0.5)])
        # 鱼市
        A('UrumiStall', S.urumi_stall(0, f), *L.MARKET_STALL, collide=[(0, 0.75, 4.4, 1.4)])
        for i, (x, y) in enumerate(L.FISH_RACKS):
            A(f'FishRack{i}', S.fish_rack(i), x, y, collide=[(-1.3, 0.05, 0.2, 0.2), (1.3, 0.05, 0.2, 0.2)])
        for k, (x, y) in enumerate([(24.8, 0.2), (25.4, -0.4), (30.6, 0.6), (31.2, 0.0), (30.0, -0.6)]):
            spr = S.barrel(k) if k % 2 else S.crate(k)
            A(f'MarketGoods{k}', spr, x, y, collide=[(0, 0.15, 0.6, 0.3)])
        A('NetPoles', S.net_poles(), *L.NET_POLES, collide=[(-0.75, 0.05, 0.2, 0.15), (0.7, 0.05, 0.2, 0.15)])
        # 人魂（漂在河面与河原上空，前景层）
        for k, (x, y) in enumerate([(-10.0, 9.2), (-3.6, 7.4), (6.4, 8.2), (12.6, 11.4), (-18.6, 3.2), (-6.0, 1.0),
                                    (21.2, 9.6), (-27.0, 12.8)]):
            A(f'Hitodama{k}', S.hitodama(), x, y, sort=False, layer='Overlay', order=30, shadow=0, alpha=0.9)
        # 灯笼流（漂在水上，烘焙进地面）
        hx, hy = L.PIER_HEAD[0] + 0.35, L.PIER_HEAD[3] - 0.25
        self.glow_spot(hx + 0.35, hy + 2.7, 1.2, '#ffd08a', 0.3)
        for k, (x, y) in enumerate([(2.8, 5.0), (5.6, 6.3), (8.0, 7.7), (3.8, 7.6), (10.2, 8.6), (6.6, 9.4)]):
            fl = S.floating_lantern(k)
            self.stamp(self.ground, fl, x, y)
            self.glow_spot(x, y + 0.2, 0.9, '#ffcf7a', 0.28)

    def bake_shadows(self):
        """物件投影烘焙进地面：脚下压扁的椭圆，带一级半透明外圈。"""
        F = self.F
        for o in self.objects:
            if not o.sort or o.shadow <= 0:
                continue
            m = o.spr.alpha_mask()
            ys, xs = np.nonzero(m)
            if len(xs) == 0:
                continue
            width = (xs.max() - xs.min() + 1) * 0.92
            rx, ry = width / 2, max(3, width * 0.16)
            cx, cy = self.px(o.x + 0.08, o.y - 0.04)
            x0, x1, y0, y1 = int(cx - rx - 2), int(cx + rx + 3), int(cy - ry - 2), int(cy + ry + 3)
            x0, y0 = max(0, x0), max(0, y0)
            x1, y1 = min(F.W, x1), min(F.H, y1)
            if x0 >= x1 or y0 >= y1:
                continue
            yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32) + 0.5
            q = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2
            # 两级硬边阴影：外圈浅、内圈深，略偏冷
            k = np.where(q <= 0.5, 0.68, np.where(q <= 1.0, 0.82, 1.0))
            k = 1 - (1 - k) * o.shadow
            sub = self.ground[y0:y1, x0:x1]
            sub[..., :3] = sub[..., :3] * k[..., None] + (1 - k[..., None]) * np.array([0.03, 0.04, 0.09], np.float32)

    # ------------------------------------------------------------ 前景：树冠
    def paint_canopy(self):
        """边界林：阔叶、杉、柳、枯树混种，树冠互相重叠成整片，压暗地图边缘。"""
        F = self.F
        rng = self.rng
        broad = [('#112925', '#173731', '#20463b', '#2c5843', '#3c6c4a'),
                 ('#13292b', '#1a3a3a', '#234a44', '#305a4c', '#406b52'),
                 ('#1a2b24', '#22392d', '#2d4935', '#3c5b3e', '#4e6d45')]
        zone = self.thicket | (self.east_cliff & self.land) | (self.top & (self.X < -34.6))
        zone |= self.rect(-36.6, -24.6, -23.0, -13.6) & self.land
        zone &= ~self.rect(-26.6, -14.4, -20.0, -11.0)            # 石阶脚下留空
        self.canopy_zone = zone
        # 树下的暗影：林地整体压暗
        dz = ndi.distance_transform_edt(~zone)
        shade = (dz < 18) & self.land & ~zone
        self.ground[zone, :3] *= 0.72
        self.ground[shade, :3] *= 0.86
        pts = scatter(zone, int(1.7 * F.ppu), 141)
        pts = pts[np.argsort(pts[:, 1])]
        for k, (px_, py_) in enumerate(pts):
            x, y = F.world(px_, py_)
            sz = rng.uniform(0.9, 1.3)
            r = rng.random()
            if r < 0.22:
                spr = P.conifer(900 + k, w=int(74 * sz), h=int(156 * sz))
            elif r < 0.32:
                spr = P.willow(600 + k, w=int(100 * sz), h=int(116 * sz),
                               pal=('#22403a', '#2e5246', '#3e644f', '#557856', '#6a8a5c'), outline='#142a26')
            elif r < 0.36:
                spr = P.dead_tree(950 + k, w=int(110 * sz), h=int(140 * sz))
            else:
                spr = P.tree(700 + k, w=int(132 * sz), h=int(112 * sz), trunk_h=int(30 * sz),
                             pal=broad[k % len(broad)], outline='#0b1d1b')
            self.stamp(self.canopy, spr, x, y)
        # 林缘矮灌木，软化边界
        edge = (dz > 0) & (dz < 26) & self.land & ~self.road & ~self.beach
        for k, (px_, py_) in enumerate(scatter(edge, 38, 142)):
            x, y = F.world(px_, py_)
            spr = P.bush(800 + k, w=int(rng.integers(34, 52)), h=int(rng.integers(24, 34)),
                         pal=('#173a33', '#22493d', '#305b45', '#436e4c', '#58804f'), outline='#0e2622')
            self.stamp(self.ground, spr, x, y)
        for a, b in L.LANTERN_LINES:
            self.lantern_line(a, b)

    def lantern_line(self, a, b, height=2.9):
        """两处屋檐之间的提灯串（前景层）。a、b 为两端脚点的世界坐标，height 为离地高度（格）。"""
        F = self.F
        (xa, ya), (xb, yb) = a, b
        pa, pb = self.px(xa, ya + height), self.px(xb, yb + height)
        x0, y0 = min(pa[0], pb[0]) - 12, min(pa[1], pb[1]) - 8
        W, H = int(abs(pb[0] - pa[0]) + 24), int(abs(pb[1] - pa[1]) + 44)
        s = Sprite(W, H, (0, 0))
        sag = 14
        pts = []
        for t in np.linspace(0, 1, 40):
            pts.append((pa[0] + (pb[0] - pa[0]) * t - x0, pa[1] + (pb[1] - pa[1]) * t + math.sin(t * math.pi) * sag - y0))
        s.line(pts, '#3a3026', 1)
        for k, t in enumerate(np.linspace(0.1, 0.9, 7)):
            lx = pa[0] + (pb[0] - pa[0]) * t - x0
            ly = pa[1] + (pb[1] - pa[1]) * t + math.sin(t * math.pi) * sag - y0
            lan = P.paper_lantern('#f3e2c0' if k % 2 else '#e8574a', '#b83a30' if k % 2 else '#f3e2c0', w=11, h=13)
            s.paste(lan, lx - lan.w / 2, ly)
            wx, wy = F.world(lx + x0, ly + y0 + 9)
            self.glow_spot(wx, wy, 0.8, '#ffcf8a', 0.22)
        paste(self.canopy, s.a, x0, y0)

    # ------------------------------------------------------------ 雾与光
    def paint_fog(self):
        """雾：①整体雾霭，越往北越浓，营造纵深；②一团团横向雾堤，底平顶鼓，顶缘提亮；
        ③岸边一缕低雾；④雾里闪烁的微光（求闻史纪）。可走区域上空保持通透。"""
        F, X, Y = self.F, self.X, self.Y
        rng = np.random.default_rng(155)
        alpha = np.zeros(F.shape, np.float32)
        col = np.zeros(F.shape + (3,), np.float32)
        col[:] = rgb('#cfdce5')
        # ① 雾霭：分级的纵向渐变
        t = np.clip((Y - 8.5) / 14.5, 0, 1) + (value_noise(F.shape, 70, 156, 2) - 0.5) * 0.12
        haze = np.array([0, 0.08, 0.16, 0.26, 0.38, 0.5], np.float32)[quantize(t, [0.1, 0.3, 0.5, 0.7, 0.88])]
        alpha = np.maximum(alpha, haze * ~(self.land & (Y < 7)))
        # ② 雾堤
        for yb, dens in ((12.4, 0.25), (15.0, 0.35), (17.4, 0.45), (19.6, 0.55), (21.4, 0.7), (23.0, 0.8)):
            bank = np.zeros(F.shape, bool)
            xs = -40.0
            while xs < 38:
                xs += rng.uniform(5.0, 11.0)
                if rng.random() > dens:
                    continue
                y_row = yb + rng.uniform(-0.8, 0.8)
                for _ in range(int(rng.integers(2, 4))):
                    cx = xs + rng.uniform(-2.0, 2.0)
                    rx, ry = rng.uniform(2.6, 6.0), rng.uniform(0.22, 0.48)
                    cy = y_row + rng.uniform(-0.15, 0.25)
                    px_, py_ = self.px(cx, cy)
                    R, Ry = rx * F.ppu, ry * F.ppu
                    x0, x1 = int(max(0, px_ - R)), int(min(F.W, px_ + R))
                    y0, y1 = int(max(0, py_ - Ry)), int(min(F.H, py_ + Ry))
                    if x0 >= x1 or y0 >= y1:
                        continue
                    yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32) + 0.5
                    bank[y0:y1, x0:x1] |= ((xx - px_) / R) ** 2 + ((yy - py_) / Ry) ** 2 <= 1
            bank = clean(bank, 2)
            core = ndi.binary_erosion(bank, iterations=7)
            a = np.where(core, 0.42, 0.24) * bank * (0.7 + 0.3 * dens)
            alpha = np.maximum(alpha, a)
            top_edge = bank & ~shifted(bank, 0, 2)
            col[bank] = rgb('#e6eef2')
            col[top_edge] = rgb('#f6fafb')
            alpha = np.where(top_edge, np.maximum(alpha, 0.36), alpha)
        # ③ 岸边低雾
        near = self.land & (self.dist_water < 60) & (Y > -6)
        mist = fog_bands(F.shape, Y + 30, 152, start=-30, end=60, levels=(0.08, 0.12), stretch=12, cell=20)
        alpha = np.maximum(alpha, mist[..., 3] * near)
        # 可走区域与栈桥上空保持通透
        clear = (self.land & (Y < 7)) | ndi.binary_dilation(self.deck, iterations=30)
        alpha = np.where(clear, np.minimum(alpha, 0.14), alpha)
        self.fog[..., :3] = col
        self.fog[..., 3] = alpha
        # ④ 微光
        for x, y in scatter(alpha > 0.25, 120, 154).astype(int):
            c = rgb('#fff6d6') if self.rng.random() < 0.7 else rgb('#d6f0ff')
            for dx, dy, a in ((0, 0, 1.0), (1, 0, 0.6), (-1, 0, 0.6), (0, 1, 0.6), (0, -1, 0.6)):
                xx, yy = x + dx, y + dy
                if 0 <= xx < F.W and 0 <= yy < F.H:
                    self.fog[yy, xx, :3] = c
                    self.fog[yy, xx, 3] = max(self.fog[yy, xx, 3], a)
        # 斜射的天光：从左上穿过雾落在河原上，三级透明度的长条
        for (x, y, w_, l_) in ((-18.0, 12.0, 2.6, 22), (-4.0, 13.0, 1.8, 20), (9.0, 12.5, 3.0, 24), (24.0, 13.5, 2.2, 20)):
            self.light_beam(x, y, w_, l_)
        # 彼岸的暖光
        for x in np.arange(-33, 36, 7.5):
            self.glow_spot(x + self.rng.uniform(-2, 2), 22.6 + self.rng.uniform(-0.4, 0.6), 3.0, '#ffc08e', 0.2)

    def light_beam(self, x, y, width, length, color='#fff3d6', a=0.1, angle=-58):
        """一道斜向光束（世界坐标起点在上方），沿光束方向两端渐隐。"""
        F, X, Y = self.F, self.X, self.Y
        ang = math.radians(angle)
        dx, dy = math.cos(ang), math.sin(ang)          # 指向右下
        u = (X - x) * dx + (Y - y) * dy                  # 沿光束
        v = -(X - x) * dy + (Y - y) * dx                 # 横向
        inside = (u > 0) & (u < length) & (np.abs(v) < width / 2)
        fade = np.minimum(u / (length * 0.25), (length - u) / (length * 0.35))
        lv = np.where(np.abs(v) < width * 0.22, 1.0, 0.55) * np.clip(fade, 0, 1)
        lv = np.where(lv > 0.66, a, np.where(lv > 0.3, a * 0.6, np.where(lv > 0, a * 0.3, 0))) * inside
        c = rgb(color)
        upd = lv > self.glow[..., 3]
        self.glow[upd, :3] = c
        self.glow[..., 3] = np.maximum(self.glow[..., 3], lv)

    def glow_spot(self, x, y, r, color, a):
        """分级的圆形光晕（3 级透明度，保持像素画的硬边）。"""
        F = self.F
        cx, cy = self.px(x, y)
        R = r * F.ppu
        x0, x1, y0, y1 = int(max(0, cx - R)), int(min(F.W, cx + R)), int(max(0, cy - R * 0.7)), int(min(F.H, cy + R * 0.7))
        if x0 >= x1 or y0 >= y1:
            return
        yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32) + 0.5
        q = np.sqrt(((xx - cx) / R) ** 2 + ((yy - cy) / (R * 0.7)) ** 2)
        lv = np.where(q < 0.35, a, np.where(q < 0.65, a * 0.6, np.where(q < 1.0, a * 0.3, 0)))
        sub = self.glow[y0:y1, x0:x1]
        c = rgb(color)
        upd = lv > sub[..., 3]
        sub[upd, :3] = c
        sub[..., 3] = np.maximum(sub[..., 3], lv)

    # ------------------------------------------------------------ 可走区域与坡度
    def walkable(self):
        F, X, Y = self.F, self.X, self.Y
        near_water = ndi.binary_dilation(self.water, iterations=int(0.38 * F.ppu))
        walk = (self.beach | self.south_open) & ~near_water & ~self.face
        top = ndi.binary_erosion(self.top, iterations=int(0.55 * F.ppu))
        walk |= top
        stairs_ext = self.poly([(x, y) for x, y in self.stair_poly])
        walk |= stairs_ext
        # 石阶顶端与台地相接处补一块
        tx, ty = L.STAIRS[1]
        walk |= self.rect(tx - L.STAIRS_W / 2, ty - 0.2, tx + L.STAIRS_W / 2, ty + 0.9)
        walk |= ndi.binary_erosion(self.deck, iterations=10)
        walk |= self.bridge_walk()
        walk |= ndi.binary_erosion(self.jetty, iterations=8)
        walk &= ~self.east_cliff
        walk &= self.rect(L.X0 + 0.8, L.Y0 + 0.2, L.X1 - 0.8, L.Y1 - 0.8)
        self.walk = walk

    def bridge_walk(self):
        X, Y = self.X, self.Y
        x0, y0, x1, y1 = L.BRIDGE
        rise = np.vectorize(self.bridge_profile)(X)
        return (X >= x0 - 0.3) & (X < x1 + 0.3) & (Y >= y0 + rise + 0.32) & (Y < y1 + rise - 0.25)

    def slope_cells(self):
        cells = []
        F = self.F
        x0, y0, x1, y1 = L.BRIDGE
        band = self.bridge_walk()
        for cx in range(int(x0), int(x1)):
            s = 0.5 if cx < x0 + 2 else (0.0 if cx < x0 + 3 else -0.5)
            if s == 0:
                continue
            for cy in range(int(y0) - 1, int(y1 + L.BRIDGE_RISE) + 1):
                px0, py0 = self.px(cx, cy + 1)
                sub = band[int(py0):int(py0) + F.ppu, int(px0):int(px0) + F.ppu]
                if sub.size and sub.mean() > 0.05:
                    cells.append(dict(x=cx, y=cy, slope=s))
        # 石阶：石阶带覆盖该格面积 ≥ 30% 的格子
        F = self.F
        for cx in range(-31, -20):
            for cy in range(-13, -7):
                px0, py0 = self.px(cx, cy + 1)
                sub = self.stairs[int(py0):int(py0) + F.ppu, int(px0):int(px0) + F.ppu]
                if sub.size and sub.mean() >= 0.3:
                    cells.append(dict(x=cx, y=cy, slope=-0.5))
        return cells

    # ------------------------------------------------------------ 总流程
    def run(self):
        self.masks()
        self.paint_far()
        self.paint_river()
        self.paint_creek()
        self.paint_land()
        self.paint_plateau()
        self.paint_flowers()
        self.paint_pier()
        self.paint_bridge()
        self.paint_pen()
        self.place_objects()
        self.bake_shadows()
        self.paint_canopy()
        self.paint_fog()
        self.walkable()
        return self

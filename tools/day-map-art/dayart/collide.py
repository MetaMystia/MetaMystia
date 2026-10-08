"""可走区域 → 轴对齐碰撞矩形，以及出生点、可达性、坡度格检查。

当前 Mod 只支持矩形碰撞（docs/resourceex-day-maps.md），这里把不可走区域按网格合并成矩形。
网格越细，斜边台阶越小，矩形越多。
"""
import numpy as np
from scipy import ndimage as ndi


class WalkGrid:
    """世界范围上的布尔网格，res 为每格多少个网格单元（4 → 0.25 格）。True = 可走。"""

    def __init__(self, x0, y0, x1, y1, res=4):
        self.x0, self.y0, self.x1, self.y1, self.res = x0, y0, x1, y1, res
        self.W, self.H = int((x1 - x0) * res), int((y1 - y0) * res)
        self.walk = np.zeros((self.H, self.W), bool)   # 行 0 在最上方（y1）

    def from_pixels(self, mask_px, ppu, thresh=0.5):
        """由像素级遮罩下采样（覆盖率 ≥ thresh 视为真）。"""
        k = ppu // self.res
        H, W = mask_px.shape
        m = mask_px[:self.H * k, :self.W * k].reshape(self.H, k, self.W, k).mean(axis=(1, 3))
        return m >= thresh

    def cell_of(self, x, y):
        return int((self.y1 - y) * self.res), int((x - self.x0) * self.res)

    def rect_block(self, cx, cy, w, h):
        """把世界坐标矩形（中心、宽高）设为不可走。"""
        r0, c0 = self.cell_of(cx - w / 2, cy + h / 2)
        r1, c1 = self.cell_of(cx + w / 2, cy - h / 2)
        self.walk[max(0, r0):max(0, r1), max(0, c0):max(0, c1)] = False

    def rectangles(self, only_near=2.0):
        """不可走网格合并成矩形（贪心：先横向成条，再纵向合并同宽条）。

        only_near：只保留距可走区域 only_near 格以内的阻挡（更远的玩家到不了）。"""
        block = ~self.walk
        if only_near:
            d = ndi.distance_transform_edt(block)
            block &= d <= only_near * self.res
        H, W = block.shape
        runs = {}  # (c0,c1) -> 当前延伸中的矩形 [r0, r1]
        rects = []
        for r in range(H + 1):
            row = block[r] if r < H else np.zeros(W, bool)
            cur = set()
            c = 0
            while c < W:
                if row[c]:
                    c0 = c
                    while c < W and row[c]:
                        c += 1
                    cur.add((c0, c))
                else:
                    c += 1
            for key in list(runs):
                if key not in cur:
                    r0 = runs.pop(key)
                    rects.append((r0, r, key[0], key[1]))
            for key in cur:
                if key not in runs:
                    runs[key] = r
        out = []
        for r0, r1, c0, c1 in rects:
            x0 = self.x0 + c0 / self.res
            x1 = self.x0 + c1 / self.res
            ytop = self.y1 - r0 / self.res
            ybot = self.y1 - r1 / self.res
            out.append(dict(x=round((x0 + x1) / 2, 4), y=round((ytop + ybot) / 2, 4),
                            width=round(x1 - x0, 4), height=round(ytop - ybot, 4)))
        return out

    def check(self, spawns, radius=0.3):
        """返回问题列表：出生点在阻挡内或离阻挡太近、出生点之间不可达、孤立的可走区域。"""
        issues = []
        er = max(1, int(round(radius * self.res)))
        free = ndi.binary_erosion(self.walk, iterations=er)
        labels, n = ndi.label(free)
        seen = set()
        for name, x, y in spawns:
            r, c = self.cell_of(x, y)
            if not (0 <= r < self.H and 0 <= c < self.W) or not free[r, c]:
                issues.append(f'出生点 {name} ({x},{y}) 处于阻挡内或距阻挡不足 {radius} 格')
                continue
            seen.add(labels[r, c])
        if len(seen) > 1:
            issues.append(f'出生点分属 {len(seen)} 个互不连通的区域')
        sizes = ndi.sum(np.ones_like(labels), labels, range(1, n + 1))
        for i, sz in enumerate(sizes, 1):
            if i not in seen and sz > self.res * self.res * 2:
                rr, cc = np.argwhere(labels == i)[0]
                issues.append(f'孤立可走区域约 {sz / self.res ** 2:.1f} 格，起点 ({self.x0 + cc / self.res:.1f},{self.y1 - rr / self.res:.1f})')
        return issues

    def check_slopes(self, cells):
        """坡度格必须落在可走区域内（至少部分）。"""
        bad = []
        for c in cells:
            r0, c0 = self.cell_of(c['x'], c['y'] + 1)
            sub = self.walk[max(0, r0):r0 + self.res, max(0, c0):c0 + self.res]
            if sub.size == 0 or not sub.any():
                bad.append((c['x'], c['y']))
        return bad

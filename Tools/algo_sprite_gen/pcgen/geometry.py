"""计算几何族（Geometry）—— 算法绘图八大族之一。

提供：Voronoi 归属场与边界线、Delaunay 辅助、Poisson Disk 自然散点采样、
以及基于 Voronoi 的部件划分（弹匣阵列 / 花岗岩颗粒 / 领地分格）。
"""

import numpy as np


def voronoi_field(x, y, points, second=False):
    """Voronoi 归属场。

    points: [(px, py), ...] 站点列表。
    返回 (nearest_index, f1, f2)，均为 (H, W) 数组；f1/f2 是到最近/次近站点的距离。
    second=True 时额外返回 f2（用于提取元胞边界线）。
    """
    h, w = x.shape
    best = np.full((h, w), 1e9, dtype=np.float32)
    second_best = np.full((h, w), 1e9, dtype=np.float32)
    idx = np.zeros((h, w), dtype=np.int32)

    for i, (px, py) in enumerate(points):
        d = np.hypot(x - px, y - py)
        closer = d < best
        second_best = np.where(closer, best, np.minimum(second_best, d))
        idx = np.where(closer, i, idx)
        best = np.where(closer, d, best)

    if second:
        return idx, best, second_best
    return idx, best


def voronoi_edges(x, y, points, width=0.9):
    """Voronoi 元胞边界线掩码（0-1），宽度为像素。

    这是龟裂、石墙、拼缝、铸件颗粒的标准做法。
    """
    _, f1, f2 = voronoi_field(x, y, points, second=True)
    return (np.abs(f2 - f1) < width).astype(np.float32)


def poisson_disk(w, h, radius, seed=0, k=24):
    """Poisson Disk 采样：任意两点间距 >= radius，得到自然（非随机聚簇）的散点分布。

    用于指示灯、铆钉、散落零件、测点阵的自然排布。返回 [(x, y), ...]。
    """
    rng = np.random.default_rng(seed)
    cell = radius / np.sqrt(2.0)
    gw = int(np.ceil(w / cell))
    gh = int(np.ceil(h / cell))
    grid = -np.ones((gh, gw), dtype=np.int64)

    def _grid_ok(px, py):
        gx = int(px / cell)
        gy = int(py / cell)
        for oy in range(max(0, gy - 2), min(gh, gy + 3)):
            for ox in range(max(0, gx - 2), min(gw, gx + 3)):
                j = grid[oy, ox]
                if j >= 0:
                    qx, qy = samples[j]
                    if (qx - px) ** 2 + (qy - py) ** 2 < radius * radius:
                        return False
        return True

    samples = [(float(rng.random() * w), float(rng.random() * h))]
    grid[int(samples[0][1] / cell), int(samples[0][0] / cell)] = 0
    active = [0]

    while active:
        ai = rng.integers(len(active))
        i = active[ai]
        px, py = samples[i]
        found = False
        for _ in range(k):
            ang = rng.random() * 2.0 * np.pi
            rad = radius * (1.0 + rng.random())
            qx = px + np.cos(ang) * rad
            qy = py + np.sin(ang) * rad
            if not (0 <= qx < w and 0 <= qy < h):
                continue
            if _grid_ok(qx, qy):
                samples.append((float(qx), float(qy)))
                grid[int(qy / cell), int(qx / cell)] = len(samples) - 1
                active.append(len(samples) - 1)
                found = True
                break
        if not found:
            active.pop(ai)
    return samples


def scatter_dots(x, y, points, radius=1.4, softness=0.8):
    """把散点集光栅化成 (H, W) 的强度场，用于指示灯 / 铆钉 / 测点。"""
    field = np.zeros_like(x, dtype=np.float32)
    for px, py in points:
        d = np.hypot(x - px, y - py)
        field = np.maximum(field, np.clip(1.0 - (d - radius) / max(softness, 1e-3), 0.0, 1.0))
    return field


def grid_rects(x, y, x0, y0, cols, rows, cw, ch, gap, jitter=0.0, seed=0):
    """规则阵列 -> 掩码场。用于弹匣/零件盒/散热片等重复结构（模板拼装族）。

    返回 (mask, (H, W) 的单元编号场)；编号场可为每格赋予不同扰动。
    """
    rng = np.random.default_rng(seed)
    mask = np.zeros_like(x, dtype=np.float32)
    cellid = np.zeros_like(x, dtype=np.int32)
    n = 0
    for r in range(rows):
        for c in range(cols):
            jx = (rng.random() - 0.5) * jitter if jitter else 0.0
            jy = (rng.random() - 0.5) * jitter if jitter else 0.0
            cx = x0 + c * (cw + gap) + cw / 2.0 + jx
            cy = y0 + r * (ch + gap) + ch / 2.0 + jy
            inside = ((np.abs(x - cx) <= cw / 2.0) & (np.abs(y - cy) <= ch / 2.0))
            mask = np.maximum(mask, inside.astype(np.float32))
            cellid = np.where(inside, n, cellid)
            n += 1
    return mask, cellid


def distance_to_polyline(x, y, pts):
    """点到折线的最短距离场。用于导轨、管路、切割槽的中心线距离。"""
    best = np.full_like(x, 1e9, dtype=np.float32)
    for i in range(len(pts) - 1):
        ax, ay = pts[i]
        bx, by = pts[i + 1]
        pax, pay = x - ax, y - ay
        bax, bay = bx - ax, by - ay
        d = bax * bax + bay * bay
        t = np.clip((pax * bax + pay * bay) / (d + 1e-9), 0.0, 1.0)
        best = np.minimum(best, np.hypot(pax - bax * t, pay - bay * t))
    return best

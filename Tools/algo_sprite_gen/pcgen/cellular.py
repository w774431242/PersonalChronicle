"""元胞族（Cellular）—— 算法绘图八大族之一。

提供：通用元胞自动机步进、洞穴/凹坑生成、反应扩散（Gray-Scott）斑纹场。
元胞自动机在贴图里的典型用途：铸件凹坑、锈蚀蔓延、化学槽液面花纹。
"""

import numpy as np


def random_grid(w, h, density=0.45, seed=0):
    rng = np.random.default_rng(seed)
    return (rng.random((h, w)) < density).astype(np.uint8)


def _neighbor_count(g):
    """8 邻域计数（环形边界）。"""
    acc = np.zeros_like(g, dtype=np.int32)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dx == 0 and dy == 0:
                continue
            acc += np.roll(np.roll(g, dy, axis=0), dx, axis=1)
    return acc


def step(g, birth=(5, 6, 7, 8), survive=(4, 5, 6, 7, 8)):
    """B/S 规则元胞步进。返回新的 uint8 网格。"""
    n = _neighbor_count(g)
    born = np.isin(n, birth) & (g == 0)
    alive = np.isin(n, survive) & (g == 1)
    return (born | alive).astype(np.uint8)


def smooth_caves(w, h, density=0.47, steps=4, seed=0,
                 birth=(5, 6, 7, 8), survive=(4, 5, 6, 7, 8)):
    """洞穴式平滑自动机：反复步进得到连通的凹凸结构。返回 0/1 网格。"""
    g = random_grid(w, h, density, seed)
    for _ in range(steps):
        g = step(g, birth, survive)
    return g


def pits(x, y, cell=9.0, steps=3, density=0.42, seed=0, smooth=1.2):
    """铸件凹坑 / 锈蚀斑：在元胞网格上做平滑插值，得到连续起伏场（0-1）。"""
    gx = int(np.ceil(x.max() / cell)) + 1
    gy = int(np.ceil(y.max() / cell)) + 1
    g = smooth_caves(gx, gy, density, steps, seed)

    u = np.clip(x / cell, 0, gx - 1.001)
    v = np.clip(y / cell, 0, gy - 1.001)
    x0 = u.astype(np.int32)
    y0 = v.astype(np.int32)
    x1 = np.minimum(x0 + 1, gx - 1)
    y1 = np.minimum(y0 + 1, gy - 1)
    fx = u - x0
    fy = v - y0
    fx = fx * fx * (3 - 2 * fx)
    fy = fy * fy * (3 - 2 * fy)

    a = g[y0, x0].astype(np.float32)
    b = g[y0, x1].astype(np.float32)
    c = g[y1, x0].astype(np.float32)
    d = g[y1, x1].astype(np.float32)
    val = (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy
    return np.clip(0.5 + (val - 0.5) * (1.0 + smooth), 0.0, 1.0)


def reaction_diffusion(w, h, steps=180, seed=0, f=0.0367, k=0.0649,
                       da=1.0, db=0.5, dt=1.0):
    """Gray-Scott 反应扩散：两种虚拟化学物质扩散并反应。

    输出 0-1 场，呈斑纹/珊瑚/脑纹形态。用于电镀液面、涂料流痕。
    """
    rng = np.random.default_rng(seed)
    a = np.ones((h, w), dtype=np.float32)
    b = np.zeros((h, w), dtype=np.float32)
    r = 4
    cy, cx = h // 2, w // 2
    a[cy - r:cy + r, cx - r:cx + r] = 0.5
    b[cy - r:cy + r, cx - r:cx + r] = 0.25
    b += (rng.random((h, w)) < 0.02).astype(np.float32) * 0.2

    def lap(z):
        return (np.roll(z, 1, 0) + np.roll(z, -1, 0) +
                np.roll(z, 1, 1) + np.roll(z, -1, 1) - 4.0 * z)

    for _ in range(steps):
        ab2 = a * b * b
        a = a + (da * lap(a) - ab2 + f * (1.0 - a)) * dt
        b = b + (db * lap(b) + ab2 - (k + f) * b) * dt
        np.clip(a, 0.0, 1.0, out=a)
        np.clip(b, 0.0, 1.0, out=b)

    out = b - b.min()
    return out / (out.max() + 1e-9)

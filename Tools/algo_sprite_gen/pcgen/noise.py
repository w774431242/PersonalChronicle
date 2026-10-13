"""噪声族（Noise）—— 算法绘图八大族之一。

提供：值噪声哈希、Perlin 梯度噪声、Worley 元胞噪声、fBm 多倍频叠加、
Turbulence 湍流、各向异性噪声（木纹/拉丝金属）。

所有函数为纯函数：给定相同 seed 与参数，永远输出同一结果。
坐标单位是像素，频率参数 base_freq 表示"每像素的周期数"。
"""

import numpy as np

# 8 个梯度方向：4 个对角 + 4 个轴向（Perlin 经典配置）
_GRAD = np.array(
    [[1, 1], [-1, 1], [1, -1], [-1, -1], [1, 0], [-1, 0], [0, 1], [0, -1]],
    dtype=np.float32,
)


def _hash2(ix, iy, seed=0):
    """整数坐标哈希 -> [0, 2^31) 的伪随机整数，确定性且无表依赖。"""
    h = (ix.astype(np.int64) * 374761393 + iy.astype(np.int64) * 668265263
         + np.int64(seed) * 1274126177)
    h = h & 0x7FFFFFFF
    h = (h ^ (h >> 13)) * 1274126177
    h = h & 0x7FFFFFFF
    h = h ^ (h >> 16)
    return h & 0x7FFFFFFF


def _hash01(ix, iy, seed=0):
    return _hash2(ix, iy, seed).astype(np.float32) / float(0x7FFFFFFF)


def _fade(t):
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0)


def perlin(x, y, seed=0):
    """Perlin 梯度噪声，输出约在 [-1, 1]。"""
    x0 = np.floor(x)
    y0 = np.floor(y)
    fx = (x - x0).astype(np.float32)
    fy = (y - y0).astype(np.float32)
    ix0 = x0.astype(np.int64)
    iy0 = y0.astype(np.int64)

    u = _fade(fx)
    v = _fade(fy)

    def _dot(ix, iy, dx, dy):
        g = _GRAD[_hash2(ix, iy, seed) & 7]
        return g[:, :, 0] * dx + g[:, :, 1] * dy

    n00 = _dot(ix0, iy0, fx, fy)
    n10 = _dot(ix0 + 1, iy0, fx - 1.0, fy)
    n01 = _dot(ix0, iy0 + 1, fx, fy - 1.0)
    n11 = _dot(ix0 + 1, iy0 + 1, fx - 1.0, fy - 1.0)

    nx0 = n00 * (1.0 - u) + n10 * u
    nx1 = n01 * (1.0 - u) + n11 * u
    return (nx0 * (1.0 - v) + nx1 * v) * 1.4


def value_noise(x, y, seed=0):
    """值噪声：直接对格点随机值做双三次插值，比 Perlin 更"块"。"""
    x0 = np.floor(x)
    y0 = np.floor(y)
    fx = (x - x0).astype(np.float32)
    fy = (y - y0).astype(np.float32)
    ix0 = x0.astype(np.int64)
    iy0 = y0.astype(np.int64)
    u = _fade(fx)
    v = _fade(fy)

    a = _hash01(ix0, iy0, seed)
    b = _hash01(ix0 + 1, iy0, seed)
    c = _hash01(ix0, iy0 + 1, seed)
    d = _hash01(ix0 + 1, iy0 + 1, seed)
    return (a * (1 - u) + b * u) * (1 - v) + (c * (1 - u) + d * u) * v


def fbm(x, y, octaves=4, base_freq=0.05, lacunarity=2.0, gain=0.5, seed=0):
    """分形布朗运动：多倍频噪声叠加。频率每层翻倍、振幅减半 —— 程序化纹理的地基。

    返回归一化到 [0, 1] 的数组。
    """
    total = np.zeros_like(x, dtype=np.float32)
    amp = 1.0
    freq = base_freq
    norm = 0.0
    for o in range(octaves):
        total += amp * perlin(x * freq, y * freq, seed + o * 131)
        norm += amp
        amp *= gain
        freq *= lacunarity
    return (total / (norm + 1e-9)) * 0.5 + 0.5


def turbulence(x, y, octaves=4, base_freq=0.05, gain=0.5, seed=0):
    """湍流：各层取绝对值，产生"褶皱、卷曲"的锐利结构，云与火焰的经典配方。"""
    total = np.zeros_like(x, dtype=np.float32)
    amp = 1.0
    freq = base_freq
    norm = 0.0
    for o in range(octaves):
        total += amp * np.abs(perlin(x * freq, y * freq, seed + o * 97))
        norm += amp
        amp *= gain
        freq *= 2.0
    return np.clip(total / (norm + 1e-9), 0.0, 1.0)


def ridged(x, y, octaves=4, base_freq=0.05, seed=0):
    """山脊噪声：1-|noise| 再平方，出尖锐山脊线，用于金属折痕与石纹。"""
    total = np.zeros_like(x, dtype=np.float32)
    amp = 1.0
    freq = base_freq
    norm = 0.0
    for o in range(octaves):
        n = 1.0 - np.abs(perlin(x * freq, y * freq, seed + o * 211))
        total += amp * n * n
        norm += amp
        amp *= 0.5
        freq *= 2.0
    return np.clip(total / (norm + 1e-9), 0.0, 1.0)


def worley(x, y, cell=12.0, seed=0, jitter=1.0):
    """Worley（元胞）噪声：返回到最近特征点的归一化距离 F1。

    距离小 = 靠近种子点（亮），距离大 = 远离（暗），出蜂窝/龟裂/铸件颗粒感。
    """
    gx = np.floor(x / cell)
    gy = np.floor(y / cell)
    igx = gx.astype(np.int64)
    igy = gy.astype(np.int64)
    best = np.full_like(x, 1e9, dtype=np.float32)

    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            cx = igx + ox
            cy = igy + oy
            h1 = _hash01(cx, cy, seed)
            h2 = _hash01(cx, cy, seed + 7717)
            px = (cx.astype(np.float32) + h1 * jitter) * cell
            py = (cy.astype(np.float32) + h2 * jitter) * cell
            best = np.minimum(best, np.hypot(x - px, y - py))
    return np.clip(best / cell, 0.0, 1.0)


def worley_f2f1(x, y, cell=12.0, seed=0):
    """Worley 的 F2-F1 差分：靠近元胞边界处趋近 0，得到锐利的龟裂纹。"""
    gx = np.floor(x / cell).astype(np.int64)
    gy = np.floor(y / cell).astype(np.int64)
    f1 = np.full_like(x, 1e9, dtype=np.float32)
    f2 = np.full_like(x, 1e9, dtype=np.float32)

    for oy in (-1, 0, 1):
        for ox in (-1, 0, 1):
            cx = gx + ox
            cy = gy + oy
            h1 = _hash01(cx, cy, seed)
            h2 = _hash01(cx, cy, seed + 7717)
            px = (cx.astype(np.float32) + h1) * cell
            py = (cy.astype(np.float32) + h2) * cell
            d = np.hypot(x - px, y - py)
            newf1 = np.minimum(f1, d)
            f2 = np.minimum(f2, np.maximum(f1, d))
            f1 = newf1
    return np.clip((f2 - f1) / cell, 0.0, 1.0)


def anisotropic_wood(x, y, scale=18.0, warp=0.35, ring=True, seed=0):
    """各向异性木纹：沿一个方向拉伸的噪声，再叠加年轮条纹。

    x 方向变化慢、y 方向变化快 => 竖向条纹（树干纹理）。
    """
    w = fbm(x * 0.35, y * 0.35, octaves=3, base_freq=0.05, seed=seed) - 0.5
    yy = y + w * scale * 2.0 * warp
    base = fbm(x * 0.06, yy * 0.9, octaves=4, base_freq=0.05, seed=seed + 31)
    if ring:
        rings = 0.5 + 0.5 * np.sin(yy * (2.0 * np.pi / max(scale * 0.45, 2.0)) + base * 5.0)
        return np.clip(base * 0.55 + rings * 0.45, 0.0, 1.0)
    return base


def anisotropic_brushed(x, y, angle_deg=0.0, freq=0.9, seed=0):
    """各向异性拉丝金属：沿指定角度的高频细纹，叠低频噪声做明暗不均。"""
    a = np.deg2rad(angle_deg)
    u = x * np.cos(a) + y * np.sin(a)
    v = -x * np.sin(a) + y * np.cos(a)
    fine = fbm(u * 0.08, v * freq, octaves=2, base_freq=0.05, seed=seed)
    broad = fbm(u * 0.02, v * 0.02, octaves=3, base_freq=0.05, seed=seed + 53)
    return np.clip(0.55 + (fine - 0.5) * 0.55 + (broad - 0.5) * 0.5, 0.0, 1.0)

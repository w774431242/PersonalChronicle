"""分形族（Fractal）—— 算法绘图八大族之一。

提供：1D/2D 中点位移（山脉、膛线起伏）、L-system 文法生长（树、支架）、
DLA 扩散限制凝聚（锈斑、枝晶）、同心靶环、以及 Mandelbrot 逃逸时间场。
"""

import numpy as np


def midpoint_displacement_1d(n, roughness=0.55, seed=0, base=0.0, span=1.0):
    """一维中点位移：一条线段反复在中间点加随机偏移。

    返回长度 2^k + 1 的 float32 数组，值域约 [base-span/2, base+span/2]。
    用于枪管膛线起伏、台面轮廓、边缘磨损曲线。
    """
    rng = np.random.default_rng(seed)
    size = 1
    while size + 1 < n:
        size *= 2
    size += 1
    arr = np.zeros(size, dtype=np.float32)
    arr[0] = (rng.random() - 0.5) * span
    arr[-1] = (rng.random() - 0.5) * span
    step = size - 1
    amp = span
    while step > 1:
        half = step // 2
        idx = np.arange(half, size, step)
        arr[idx] = (arr[idx - half] + arr[idx + half]) * 0.5 + \
            (rng.random(len(idx)) - 0.5) * amp
        amp *= roughness
        step = half
    return arr[:n] + base


def midpoint_displacement_2d(w, h, roughness=0.6, seed=0):
    """Diamond-Square 二维中点位移，生成高度图（0-1）。山脉/地形底图。"""
    rng = np.random.default_rng(seed)
    n = 1
    while n < max(w, h):
        n *= 2
    n += 1
    g = np.zeros((n, n), dtype=np.float32)
    g[0, 0] = rng.random()
    g[0, -1] = rng.random()
    g[-1, 0] = rng.random()
    g[-1, -1] = rng.random()

    step = n - 1
    amp = 1.0
    while step > 1:
        half = step // 2
        # Diamond
        for y in range(half, n, step):
            for x in range(half, n, step):
                g[y, x] = (g[y - half, x - half] + g[y - half, x + half] +
                           g[y + half, x - half] + g[y + half, x + half]) * 0.25 + \
                    (rng.random() - 0.5) * amp
        # Square
        for y in range(0, n, half):
            for x in range((y + half) % step, n, step):
                acc, cnt = 0.0, 0
                for dy, dx in ((-half, 0), (half, 0), (0, -half), (0, half)):
                    yy, xx = y + dy, x + dx
                    if 0 <= yy < n and 0 <= xx < n:
                        acc += g[yy, xx]
                        cnt += 1
                if cnt:
                    g[y, x] = acc / cnt + (rng.random() - 0.5) * amp
        amp *= roughness
        step = half

    out = g[:h, :w]
    lo, hi = out.min(), out.max()
    return (out - lo) / (hi - lo + 1e-9)


def lsystem(axiom, rules, iterations, angle_deg, step_len, start=(0.0, 0.0),
            start_angle=-90.0, jitter=0.0, seed=0):
    """L-system 文法生长，返回线段列表 [((x0,y0),(x1,y1),depth), ...]。

    角度单位度，图像坐标系 y 向下；start_angle=-90 表示向上生长。
    含随机扰动 jitter 时结果随 seed 变化，可产出每台设备不同的植株形态。
    """
    rng = np.random.default_rng(seed)
    s = axiom
    for _ in range(iterations):
        s = "".join(rules.get(ch, ch) for ch in s)

    segs = []
    stack = []
    x, y = start
    ang = start_angle
    for ch in s:
        if ch == "F":
            a = np.deg2rad(ang + (rng.random() - 0.5) * jitter)
            nx = x + np.cos(a) * step_len
            ny = y + np.sin(a) * step_len
            segs.append(((x, y), (nx, ny), len(stack)))
            x, y = nx, ny
        elif ch == "+":
            ang += angle_deg
        elif ch == "-":
            ang -= angle_deg
        elif ch == "[":
            stack.append((x, y, ang))
        elif ch == "]":
            if stack:
                x, y, ang = stack.pop()
    return segs


def dla(n_points=900, w=64, h=64, seed=0, stickiness=1.0, start_radius=1.2):
    """扩散限制凝聚：粒子随机游走，碰到已有簇即黏附。

    返回 (H, W) 浮点掩码（0-1），呈枝晶/珊瑚/锈蚀蔓延形态。
    """
    rng = np.random.default_rng(seed)
    grid = np.zeros((h, w), dtype=np.float32)
    cx, cy = w / 2.0, h / 2.0
    grid[int(cy), int(cx)] = 1.0
    occupied = [(int(cx), int(cy))]
    max_r = start_radius

    for _ in range(n_points):
        ang = rng.random() * 2 * np.pi
        r = max_r + 1.0
        px = cx + np.cos(ang) * r
        py = cy + np.sin(ang) * r
        for _step in range(4000):
            if rng.random() < stickiness:
                px += rng.choice([-1.0, 0.0, 1.0])
                py += rng.choice([-1.0, 0.0, 1.0])
            ix, iy = int(round(px)), int(round(py))
            if not (0 <= ix < w and 0 <= iy < h):
                break
            hit = False
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    jx, jy = ix + dx, iy + dy
                    if 0 <= jx < w and 0 <= jy < h and grid[jy, jx] > 0:
                        hit = True
                        break
                if hit:
                    break
            if hit:
                grid[iy, ix] = 1.0
                occupied.append((ix, iy))
                d = np.hypot(ix - cx, iy - cy)
                max_r = max(max_r, d)
                break
    return grid


def concentric_rings(x, y, cx, cy, period=6.0, phase=0.0):
    """同心环场：靶板/表盘刻度/镜头镀膜。返回 [0, 1] 的环带强度。"""
    d = np.hypot(x - cx, y - cy)
    return 0.5 + 0.5 * np.sin((d / period) * 2.0 * np.pi + phase)


def mandelbrot_escape(x, y, cx=-0.5, cy=0.0, scale=0.005, max_iter=24):
    """Mandelbrot 逃逸时间场（0-1）：用于"铸造流痕"类的边缘纹理。"""
    zr = (x - x.shape[1] / 2.0) * scale + cx
    zi = (y - y.shape[0] / 2.0) * scale + cy
    cr, ci = zr.copy(), zi.copy()
    out = np.zeros_like(zr, dtype=np.float32)
    alive = np.ones_like(zr, dtype=bool)
    for i in range(max_iter):
        zr2 = zr * zr - zi * zi + cr
        zi2 = 2.0 * zr * zi + ci
        # 逃逸后数值会迅速发散，及时夹住避免溢出警告
        zr = np.clip(zr2, -1e4, 1e4)
        zi = np.clip(zi2, -1e4, 1e4)
        mag = zr * zr + zi * zi
        escaped = mag > 4.0
        out[escaped & alive] = i / max_iter
        alive &= ~escaped
        if not alive.any():
            break
    out[alive] = 1.0
    return out

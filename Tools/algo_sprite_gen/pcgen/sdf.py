"""SDF（有符号距离场）图元与布尔运算。

约定：所有图元函数接受坐标网格 (x, y) 与几何参数，返回同形状 float32 距离场。
    < 0 = 形状内部，> 0 = 形状外部，绝对值 = 到边界的像素距离。

这套表示是设备"硬造型"的唯一来源：台面、机身、机架、腔体全部由 SDF 布尔拼出，
因此不存在手绘轮廓，改一个参数即可整体重构。
"""

import numpy as np


def grid(w, h):
    """返回 (x, y) 坐标网格，x 向右、y 向下（图像空间）。"""
    ys, xs = np.mgrid[0:h, 0:w]
    return xs.astype(np.float32), ys.astype(np.float32)


# --------------------------------------------------------------------------
# 基础图元
# --------------------------------------------------------------------------

def circle(x, y, cx, cy, r):
    return np.hypot(x - cx, y - cy) - r


def ellipse(x, y, cx, cy, rx, ry):
    dx = (x - cx) / max(rx, 1e-6)
    dy = (y - cy) / max(ry, 1e-6)
    k = np.hypot(dx, dy)
    return (k - 1.0) * min(rx, ry)


def rect(x, y, cx, cy, hw, hh):
    qx = np.abs(x - cx) - hw
    qy = np.abs(y - cy) - hh
    return (np.maximum(qx, 0.0) ** 2 + np.maximum(qy, 0.0) ** 2) ** 0.5 + \
        np.minimum(np.maximum(qx, qy), 0.0)


def rrect(x, y, cx, cy, hw, hh, r):
    """圆角矩形。r 为圆角半径。"""
    r = min(r, hw, hh)
    qx = np.abs(x - cx) - (hw - r)
    qy = np.abs(y - cy) - (hh - r)
    return (np.maximum(qx, 0.0) ** 2 + np.maximum(qy, 0.0) ** 2) ** 0.5 + \
        np.minimum(np.maximum(qx, qy), 0.0) - r


def capsule(x, y, ax, ay, bx, by, r):
    """胶囊体：线段 AB 向外膨胀 r。"""
    pax, pay = x - ax, y - ay
    bax, bay = bx - ax, by - ay
    d = bax * bax + bay * bay
    h = np.clip((pax * bax + pay * bay) / (d + 1e-9), 0.0, 1.0)
    return np.hypot(pax - bax * h, pay - bay * h) - r


def annulus(x, y, cx, cy, r_in, r_out):
    """圆环（环带）。"""
    d = np.hypot(x - cx, y - cy)
    return np.maximum(d - r_out, r_in - d)


def arc_ring(x, y, cx, cy, r_in, r_out, a0, a1):
    """带角度范围的圆环扇段，角度为弧度，0 = 正右，逆时针为正（图像 y 向下故取负）。"""
    d = np.hypot(x - cx, y - cy)
    ang = np.arctan2(-(y - cy), x - cx)
    span = (a1 - a0) % (2 * np.pi)
    rel = (ang - a0) % (2 * np.pi)
    inside = rel <= span
    ring = np.maximum(d - r_out, r_in - d)
    return np.where(inside, ring, np.abs(ring) + 1.0)


def polygon(x, y, pts):
    """任意多边形 SDF（支持凹多边形）。pts 为 [(x, y), ...] 顶点序列。"""
    n = len(pts)
    px = np.asarray([p[0] for p in pts], dtype=np.float32)
    py = np.asarray([p[1] for p in pts], dtype=np.float32)

    d = (x - px[0]) ** 2 + (y - py[0]) ** 2
    s = np.ones_like(x)
    j = n - 1
    for i in range(n):
        ex = px[j] - px[i]
        ey = py[j] - py[i]
        wx = x - px[i]
        wy = y - py[i]
        t = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey + 1e-9), 0.0, 1.0)
        bx = wx - ex * t
        by = wy - ey * t
        d = np.minimum(d, bx * bx + by * by)

        c1 = y >= py[i]
        c2 = y < py[j]
        c3 = ex * wy > ey * wx
        s = np.where((c1 & c2 & c3) | (~c1 & ~c2 & ~c3), -s, s)
        j = i
    return s * np.sqrt(d)


def hstripes(x, y, cx, cy, hw, hh, angle_deg, period, duty=0.5):
    """斜条纹掩码（0/1），用于警示条纹。返回掩码而非距离场。"""
    a = np.deg2rad(angle_deg)
    u = (x - cx) * np.cos(a) + (y - cy) * np.sin(a)
    phase = np.mod(u, period) / period
    return (phase < duty).astype(np.float32)


# --------------------------------------------------------------------------
# 布尔运算
# --------------------------------------------------------------------------

def union(*fields):
    out = fields[0]
    for f in fields[1:]:
        out = np.minimum(out, f)
    return out


def subtract(base, *cutters):
    out = base
    for c in cutters:
        out = np.maximum(out, -c)
    return out


def intersect(*fields):
    out = fields[0]
    for f in fields[1:]:
        out = np.maximum(out, f)
    return out


def smooth_union(a, b, k):
    """平滑并集，k 越大接缝越圆润。用于把拼装件融成一体。"""
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b * (1.0 - h) + a * h - k * h * (1.0 - h)


def smooth_subtract(a, b, k):
    """平滑差集。"""
    h = np.clip(0.5 - 0.5 * (a + b) / k, 0.0, 1.0)
    return (a * (1.0 - h) + (-b) * h + k * h * (1.0 - h))


def smooth_intersect(a, b, k):
    h = np.clip(0.5 - 0.5 * (b - a) / k, 0.0, 1.0)
    return b * (1.0 - h) + a * h + k * h * (1.0 - h)


def outline(sd, width):
    """返回距离边界 width 像素以内的环带掩码（0/1）。"""
    return (np.abs(sd) <= width).astype(np.float32)


def inner_edge(sd, width):
    """返回从边界向内 width 像素的环带掩码（用于高光/做旧）。"""
    return ((sd < 0.0) & (sd > -width)).astype(np.float32)


# --------------------------------------------------------------------------
# 2.5D 挤出
# --------------------------------------------------------------------------

def shift_down(field, depth):
    """把场整体向下（+y）平移 depth 像素，空出的顶部填 +inf。"""
    out = np.full_like(field, 1e9)
    if depth <= 0:
        return field.copy()
    if depth < field.shape[0]:
        out[depth:, :] = field[:-depth, :]
    return out


def shift(field, dx=0, dy=0):
    """整数像素平移，越界填 +inf。"""
    out = np.full_like(field, 1e9)
    h, w = field.shape
    sx0, sx1 = max(0, -dx), min(w, w - dx)
    sy0, sy1 = max(0, -dy), min(h, h - dy)
    if sx0 < sx1 and sy0 < sy1:
        out[sy0 + dy:sy1 + dy, sx0 + dx:sx1 + dx] = field[sy0:sy1, sx0:sx1]
    return out


def extrude_down(sd, depth):
    """向下挤出，返回 (顶面掩码, 侧壁掩码, 整体掩码)。

    RimWorld 原版建筑的 2.5D 观感：顶面是俯视轮廓，下沿有一道薄立面。
    depth 即立面的像素高度（T1 规范：极薄上沿，depth 取 2-4px）。
    """
    top = sd < 0.0
    lower = shift_down(sd, depth) < 0.0
    side = lower & (~top)
    body = top | lower
    return top, side, body

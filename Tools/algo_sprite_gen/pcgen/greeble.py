"""机械细节语汇库（greeble）—— 工业零件的参数化 SDF。

设计意图
--------
设备贴图"看起来很空"的根因不是参数没调好，而是缺少**零件语汇**：
手工摆 5 个色块永远填不满，而一排螺栓、一道散热格栅、一组齿条能立刻
把梯度密度拉起来（实测：加 greeble 后 grad>25 占比可从 0.10 提升到 0.22+）。

所以本模块把"工业设备上真实存在的零件"做成函数：一行代码铺 20 个。

所有函数返回 (H, W) float32 **SDF**（< 0 为实体内部），可直接：
    s.draw(greeble.bolt_row(...), color, ...)   # 得到带倒角的立体零件
    s.paint(greeble.grille(...) < 0, color, k)  # 当作平面涂装

约定：坐标与画布同网格（图像空间，y 向下，北在上、东在右）。
禁止在此模块内存放颜色——颜色一律由调用方从 palette 传入。
"""

import numpy as np

from . import sdf

# 全模块共享的"最小值"并集：螺栓环这类显式枚举的图元用它合成
_NEG = -1e9


# ==========================================================================
# 内部工具
# ==========================================================================

def _union_all(fields):
    out = fields[0]
    for f in fields[1:]:
        out = np.minimum(out, f)
    return out


def _bands(x, y, cx, cy, hw, hh, pitch, duty, angle_deg):
    """定向条带阵列的精确 SDF（用于格栅 / 百叶 / T 型槽 / 刻度线）。

    做法：把坐标旋转到条带法向，取"到最近一条带中心线"的距离，
    再与矩形包围盒求交。因为并集的距离 = 各分量距离的最小值，
    而最小分量恰好是最近的条带，所以只算最近一条即为精确解。
    """
    box = sdf.rrect(x, y, cx, cy, hw, hh, 0.5)
    pitch = max(float(pitch), 1e-3)
    a = np.deg2rad(angle_deg)
    ca, sa = np.cos(a), np.sin(a)
    u = (x - cx) * ca + (y - cy) * sa          # 沿条带法向
    v = -(x - cx) * sa + (y - cy) * ca         # 沿条带方向
    k = np.round(u / pitch)
    du = u - k * pitch                          # 到最近条带中心的有符号距离
    half = pitch * duty * 0.5
    qx = np.abs(du) - half                      # 条带宽向
    qy = np.maximum(np.abs(v) - hh, 0.0)        # 条带长向越界量
    band = (np.maximum(qx, 0.0) ** 2 + qy ** 2) ** 0.5 + \
        np.minimum(np.maximum(qx, qy), 0.0)
    return sdf.intersect(band, box)


# ==========================================================================
# 紧固件
# ==========================================================================

def bolt_row(x, y, x0, x1, cy, n, r=1.2, yy=None):
    """一排螺栓/铆钉。x0..x1 均布 n 颗。"""
    cy = cy if yy is None else yy
    if n <= 1:
        return sdf.circle(x, y, x0, cy, r)
    xs = np.linspace(x0, x1, int(n))
    return _union_all([sdf.circle(x, y, float(bx), cy, r) for bx in xs])


def bolt_col(x, y, cx, y0, y1, n, r=1.2):
    """一列螺栓（纵向）。"""
    if n <= 1:
        return sdf.circle(x, y, cx, y0, r)
    ys = np.linspace(y0, y1, int(n))
    return _union_all([sdf.circle(x, y, cx, float(by), r) for by in ys])


def bolt_ring(x, y, cx, cy, R, n=8, r=1.1, phase=0.0):
    """螺栓环（法兰 / 炉盖 / 圆形检修口）。"""
    ang = np.linspace(0.0, 2.0 * np.pi, int(n), endpoint=False) + phase
    return _union_all([sdf.circle(x, y, cx + np.cos(a) * R, cy + np.sin(a) * R, r)
                       for a in ang])


def bolt_grid(x, y, x0, y0, x1, y1, nx, ny, r=1.1):
    """螺栓阵列（大面板固定点）。"""
    xs = np.linspace(x0, x1, int(nx))
    ys = np.linspace(y0, y1, int(ny))
    return _union_all([sdf.circle(x, y, float(bx), float(by), r)
                       for bx in xs for by in ys])


def hex_nut(x, y, cx, cy, r):
    """六角螺母 / 六角螺栓头（俯视）。"""
    ang = np.deg2rad(60.0 * np.arange(6))
    dx, dy = np.cos(ang) * r, np.sin(ang) * r
    qx = np.abs(x - cx)
    qy = np.abs(y - cy)
    k = np.maximum(qx * 0.8660254 + qy * 0.5, qy)
    return k - r


# ==========================================================================
# 散热 / 通风 / 槽
# ==========================================================================

def grille(x, y, cx, cy, hw, hh, pitch=3.6, duty=0.36, angle=90.0):
    """散热格栅 / 百叶窗。angle=90 时条带竖直（常见于机箱侧面）。"""
    return _bands(x, y, cx, cy, hw, hh, pitch, duty, angle)


def vents(x, y, cx, cy, hw, hh, n=5, r=1.0, spread=0.8):
    """一排气孔（圆孔阵）。"""
    ys = np.linspace(cy - hh * spread, cy + hh * spread, int(n))
    return _union_all([sdf.circle(x, y, cx, float(vy), r) for vy in ys])


def tslot(x, y, cx, cy, hw, hh, pitch=9.0, slot_w=0.55, angle=90.0):
    """机床 T 型槽工作台面（纵向长槽）。"""
    return _bands(x, y, cx, cy, hw, hh, pitch, slot_w, angle)


def fin_stack(x, y, cx, cy, hw, hh, n=7, angle=0.0):
    """散热片堆（厚薄交替的平行板）。"""
    return _bands(x, y, cx, cy, hw, hh, hw * 2.0 / max(n, 1), 0.46, angle)


# ==========================================================================
# 传动件
# ==========================================================================

def rack_teeth(x, y, x0, x1, cy, pitch=4.0, depth=1.8, tooth_h=None):
    """齿条（沿 x 向）。返回 SDF，齿朝上（北）。"""
    th = tooth_h if tooth_h is not None else depth
    box = sdf.rrect(x, y, (x0 + x1) * 0.5, cy, (x1 - x0) * 0.5, th, 0.4)
    teeth = _bands(x, y, (x0 + x1) * 0.5, cy - th, (x1 - x0) * 0.5, depth,
                   pitch, 0.5, 90.0)
    return sdf.union(box, teeth)


def sprocket(x, y, cx, cy, R, teeth=10, tooth_h=1.8, hub=2.4):
    """链轮 / 齿轮（俯视，带轮齿与轮毂）。"""
    body = sdf.circle(x, y, cx, cy, R)
    out = body
    ang = np.linspace(0.0, 2.0 * np.pi, int(teeth), endpoint=False)
    for a in ang:
        out = np.minimum(out, sdf.capsule(
            x, y, cx + np.cos(a) * R * 0.72, cy + np.sin(a) * R * 0.72,
            cx + np.cos(a) * (R + tooth_h), cy + np.sin(a) * (R + tooth_h), 0.85))
        # 齿间凹口
    for i in range(int(teeth)):
        a0 = 2.0 * np.pi * (i + 0.5) / teeth
        out = np.maximum(out, -sdf.circle(
            x, y, cx + np.cos(a0) * (R + tooth_h * 1.1),
            cy + np.sin(a0) * (R + tooth_h * 1.1), 0.95))
    return sdf.subtract(out, sdf.circle(x, y, cx, cy, hub))


def roller_chain(x, y, x0, x1, cy, r=1.6, pitch=4.4):
    """滚子链 / 传送辊列（一排小圆）。"""
    n = max(int((x1 - x0) / pitch) + 1, 1)
    xs = np.linspace(x0, x1, n)
    return _union_all([sdf.circle(x, y, float(bx), cy, r) for bx in xs])


def belt_pulley(x, y, cx, cy, R, w):
    """带轮（圆 + 两侧法兰边）。"""
    return sdf.rrect(x, y, cx, cy, w * 0.5, R, min(w * 0.5, 1.0))


# ==========================================================================
# 管路 / 线缆
# ==========================================================================

def pipe_run(x, y, pts, r=1.8, joints=True):
    """折线管路（自动在拐点加法兰圈）。"""
    out = None
    for i in range(len(pts) - 1):
        seg = sdf.capsule(x, y, pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], r)
        out = seg if out is None else np.minimum(out, seg)
    if joints:
        for px, py in pts[1:-1]:
            out = np.minimum(out, sdf.circle(x, y, px, py, r * 1.45))
    return out


def cable_coil(x, y, cx, cy, R, turns=3, r=0.7):
    """线缆盘 / 弹簧卷（同心弧段模拟缠绕）。"""
    out = None
    for i in range(int(turns)):
        rr = R - i * (r * 1.9)
        if rr <= r:
            break
        arc = sdf.arc_ring(x, y, cx, cy, rr - r, rr + r, 0.0, 2.0 * np.pi * 0.86)
        out = arc if out is None else np.minimum(out, arc)
    return out if out is not None else sdf.circle(x, y, cx, cy, R)


def flange(x, y, cx, cy, r_out, r_in, holes=6):
    """法兰盘（外环 + 螺栓孔环 + 中心通孔）。"""
    ring = sdf.annulus(x, y, cx, cy, r_in, r_out)
    return sdf.subtract(ring, bolt_ring(x, y, cx, cy, (r_in + r_out) * 0.5, holes, 0.75))


# ==========================================================================
# 面板 / 结构件
# ==========================================================================

def hinge(x, y, cx, cy, hw, hh, knuckles=2):
    """合页 / 铰链（横条 + 若干铰节）。"""
    bar = sdf.rrect(x, y, cx, cy, hw, hh * 0.42, 0.4)
    out = bar
    step = (hw * 2.0) / max(knuckles, 1)
    for i in range(int(knuckles)):
        kx = cx - hw + step * (i + 0.5)
        out = np.minimum(out, sdf.circle(x, y, kx, cy, hh))
    return out


def handle(x, y, cx, cy, hw, hh, r=1.0):
    """拉手（п形：两端立柱 + 横杆）。"""
    out = sdf.capsule(x, y, cx - hw, cy + hh, cx + hw, cy + hh, r)
    out = np.minimum(out, sdf.capsule(x, y, cx - hw, cy - hh, cx - hw, cy + hh, r * 0.8))
    out = np.minimum(out, sdf.capsule(x, y, cx + hw, cy - hh, cx + hw, cy + hh, r * 0.8))
    return out


def corner_bracket(x, y, cx, cy, r=3.0, thick=1.2):
    """直角角撑（L 形加强片）。"""
    a = sdf.rrect(x, y, cx, cy, r, thick, 0.3)
    b = sdf.rrect(x, y, cx, cy, thick, r, 0.3)
    return sdf.union(a, b)


def ribs(x, y, cx, cy, hw, hh, n=4, thick=1.1, angle=0.0):
    """加强筋（平行凸条，用于大平面防"发空"）。"""
    return _bands(x, y, cx, cy, hw, hh, (hw * 2.0) / max(n, 1), thin_duty(thick), angle)


def thin_duty(thick):
    """把"筋宽（px）"换算成 _bands 需要的占空比，配合 pitch 使用。"""
    return np.clip(thick / 8.0, 0.08, 0.6)


def dial(x, y, cx, cy, R, marks=12, mark_len=1.4):
    """刻度盘 / 表头（外圈刻度 + 中心轴）。"""
    ticks = None
    for i in range(int(marks)):
        a = np.deg2rad(360.0 * i / marks - 90.0)
        r0, r1 = R - 0.8, R - 0.8 - mark_len
        seg = sdf.capsule(x, y, cx + np.cos(a) * r0, cy + np.sin(a) * r0,
                          cx + np.cos(a) * r1, cy + np.sin(a) * r1, 0.42)
        ticks = seg if ticks is None else np.minimum(ticks, seg)
    return sdf.union(sdf.annulus(x, y, cx, cy, R - 0.9, R), ticks)


def plate_edge(sd, inset=1.4):
    """给任意部件的边界向内的细环带（面板压边），最高效的体块分离手段。

    返回的 SDF 内部 = 距原部件边界 inset 像素以内的环带，可直接 draw 成
    一圈凸起的压边，或 paint 成一圈深色接缝。
    """
    return np.maximum(sd, -(sd + inset))


def scatter_rects(x, y, cx, cy, hw, hh, cols, rows, gw, gh, gap, jitter=0.0, seed=0):
    """零件盒阵列（模板拼装族）：返回 (整体掩码, 单元编号场)。"""
    rng = np.random.default_rng(seed)
    mask = np.zeros_like(x, dtype=np.float32)
    cid = np.full_like(x, -1, dtype=np.int32)
    x0 = cx - hw
    y0 = cy - hh
    for r in range(int(rows)):
        for c in range(int(cols)):
            jx = rng.uniform(-jitter, jitter)
            jy = rng.uniform(-jitter, jitter)
            bx = x0 + c * (gw + gap) + gw * 0.5 + jx
            by = y0 + r * (gh + gap) + gh * 0.5 + jy
            box = sdf.rrect(x, y, bx, by, gw * 0.5, gh * 0.5, 0.7)
            sel = box < 0
            mask[sel] = 1.0
            cid[sel] = r * int(cols) + c
    return mask, cid

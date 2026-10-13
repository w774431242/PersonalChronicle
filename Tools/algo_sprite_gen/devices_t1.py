"""T1 十二台设备的算法配方。

v2 设计要点（针对 v1「细节不满意」的重做）
------------------------------------------
v1 的根因不是参数，是**结构**：12 台共用同一个"圆角框 + 右沿缺口"外壳，
只换内芯。实测 12 张的 opaque 占比完全相同（0.414），玩家在游戏里分不清谁是谁；
同时细节靠手工摆几个色块，梯度密度只有基准件的 20-45%。

v2 把设计拆成两层：

    ┌ 家族语汇层（12 台共享，保证是一家人）
    │   台面分缝 ×2 + 亮线倒角 / 智能屏统一形制 / 警示条统一样式
    │   铆钉行 / 边角做旧规则 / 光照参数 / 黄铜旧件色 / 指示灯列
    └ 轮廓层（每台独立成形，保证一眼可辨）
        炉体圆 + 方台 / 砧座 + 立架 / 左炉右槽双箱 / 长床身 + 主轴箱
        门型龙门 / 满宽床身 + 端塔 / 三连槽 + 排风罩 / 台面 + 集尘塔
        L 形料架 / 大平板 + 工具墙 / 靶板架 / 仪表墙

每台的形状算法与材质算法对应该工序，见文件末 DEVICES 注册表。

坐标约定：画布 128x128，图像空间 y 向下，北在上、东在右；
east 为基准向，操作缺口开在右沿（东），对齐 pawn 交互格。
"""

import numpy as np

from pcgen import greeble, palette, render, sdf
from pcgen.fieldkit import FieldKit

W = H = 128
LEFT, RIGHT = 10.0, 118.0
TOP, BOT = 22.0, 108.0

L_BASE, L_DECK, L_LOW = render.LAYER_BASE, render.LAYER_DECK, render.LAYER_LOW
L_MID, L_HIGH, L_TOP = render.LAYER_MID, render.LAYER_HIGH, render.LAYER_TOP

ACCENT = {
    "energy": (palette.ENERGY_BLUE, palette.ENERGY_BLUE2),
    "bio": (palette.BIO_GREEN, palette.BIO_GREEN2),
    "motion": (palette.MOTION_ORANGE, palette.MOTION_ORANGE2),
}


# ==========================================================================
# 上下文
# ==========================================================================

class Ctx:
    """一次绘制所需的全部句柄，避免每个函数都传四个参数。"""

    __slots__ = ("s", "fx", "ac", "acd", "x", "y", "hull", "seed")

    def __init__(self, sprite, fieldkit, accent):
        self.s = sprite
        self.fx = fieldkit
        self.x = sprite.x
        self.y = sprite.y
        self.seed = sprite.seed
        self.ac, self.acd = accent
        self.hull = None


# ==========================================================================
# 家族语汇层：12 台共享的零件与规则
# ==========================================================================

def notch_sd(x, y, edge=RIGHT, cy=82.0, hh=13.0, depth=15.0, r=3.0):
    """东沿操作缺口：pawn 站位凹口。每台的尺寸与纵向位置不同，不作为轮廓标识。

    右边界严格止于 edge。若让矩形越过机体外沿，缺口地板会一直画到画布角落，
    在贴图右侧留下一块不该有的黑方块（v2 首版实测踩到）。
    """
    return sdf.rrect(x, y, edge - depth * 0.5, cy, depth * 0.5, hh, r)


def footwell(c, notch):
    """缺口地板：凹口内下沉的操作地面（最低层，先画）。"""
    s, fx = c.s, c.fx
    s.draw(notch, palette.DARK, material=fx.fbm(0.30, 2, so=3),
           depth=0, top_shade=0.74, side_shade=0.36,
           edge_hi=0.16, edge_hi_w=1.1, ao=0.34)


def smart_screen(c, cx, cy, hw=12.0, hh=6.5, lines=3, power="right"):
    """智能屏（T1 等级识别标志）。形制全级统一：深灰底 + 功能色细线 + 电源点。"""
    s, x, y = c.s, c.x, c.y
    s.draw(sdf.rrect(x, y, cx, cy, hw, hh, 1.6), palette.SCREEN_BG,
           material=c.fx.fbm(0.45, 2, so=77),
           depth=L_MID, top_shade=1.0, side_shade=0.62,
           edge_hi=0.30, edge_hi_w=1.1, ao=0.30)
    for i in range(int(lines)):
        ln = sdf.rrect(x, y, cx - hw * 0.16, cy - hh * 0.52 + i * (hh * 0.62),
                       hw * (0.74 - i * 0.20), 0.55, 0.25)
        s.paint((ln < 0).astype(np.float32), c.ac, 0.70)
    px = cx + hw * 0.62 if power == "right" else cx - hw * 0.62
    dot = sdf.circle(x, y, px, cy + hh * 0.52, 1.35)
    s.draw(dot, c.ac, depth=L_MID, top_shade=1.1, side_shade=0.6, edge_hi=0.4)
    s.emissive_add((dot < 0).astype(np.float32), c.ac, 0.5)


def lamp_row(c, x0, cy, n=3, r=1.5, lit=2, sep=6.0, angle=None):
    """状态指示灯列（深色底座 + 发光灯珠），T1 为 2~3 个。"""
    s, x, y = c.s, c.x, c.y
    for i in range(int(n)):
        lx = x0 + i * sep
        base = sdf.circle(x, y, lx, cy, r + 0.9)
        s.draw(base, palette.DARK, depth=L_LOW, top_shade=0.9,
               side_shade=0.55, edge_hi=0.22)
        on = i < int(lit)
        col = c.ac if on else palette.DARK2
        bead = sdf.circle(x, y, lx, cy, r)
        s.draw(bead, col, depth=L_LOW, top_shade=1.06, side_shade=0.6, edge_hi=0.34)
        if on:
            s.emissive_add((bead < 0).astype(np.float32), c.ac, 0.55)


def hazard_band(c, shaper, cx, cy, hw, hh, angle=45.0, period=6.0, duty=0.5):
    """警示条纹（全级通用辅助色，不计入主功能色）。

    shaper 为裁切用的 SDF（一般传 hull），保证条纹只落在机体上。
    """
    s, x, y = c.s, c.x, c.y
    band = sdf.intersect(sdf.rrect(x, y, cx, cy, hw, hh, 1.2), shaper)
    m = (band < 0).astype(np.float32) * sdf.hstripes(
        x, y, cx, cy, hw, hh, angle, period, duty)
    s.paint(m, palette.HAZARD2, 0.94)
    s.paint(m * sdf.hstripes(x, y, cx, cy, hw, hh, angle, period, duty * 0.4),
            palette.HAZARD, 0.5)


def deck_seams(c, deck, pitch=26.0, angle=90.0, width=0.9):
    """台面分缝：深色槽 + 紧邻的亮线。

    这条"暗缝贴亮线"是本项目细节密度指标的主力（np.gradient 用中心差分，
    只有紧邻的高对比配对才能产生足够梯度），宽窄和间距都别随手改。
    """
    s = c.s
    line = render.panel_seams(deck, pitch=pitch, angle=angle, width=width)
    s.paint(line, palette.DARK, 0.95)
    s.paint(render.panel_seams(deck, pitch=pitch, angle=angle,
                               width=width * 0.7, offset=width + 0.9),
            palette.LIGHT2, 0.62)


def panel_grid(c, hull, pitch=19.0, k=0.72, skip=0.0):
    """机体面板网：纵横细缝，铺满整个底壳。工业设备的"骨架感"来源。"""
    s = c.s
    for ang in (90.0, 0.0):
        line = render.panel_seams(hull, pitch=pitch, angle=ang, width=0.55)
        s.paint(line, palette.DARK, k)
        s.paint(render.panel_seams(hull, pitch=pitch, angle=ang,
                                   width=0.45, offset=1.6), palette.LIGHT, 0.30)


def bolt_scatter(c, sd_region, *, so=0, pitch=13.0, r=1.05, k=0.60):
    """在区域上散落螺栓/铆钉（机器上不可能没有的零件）。"""
    s, fx = c.s, c.fx
    inside = (sd_region < -1.0).astype(np.float32)
    pts = [(px, py) for px, py in fx.poisson(radius=pitch, so=so)
           if 4 < px < W - 4 and 4 < py < H - 4]
    dots = fx.dots(pts, radius=r, softness=0.45) * inside
    s.paint(dots, palette.DARK, k)
    s.emboss((dots - 0.5) * r * 2.6, hi=0.50, dark=0.42, width=0.95)


def greeble_scatter(c, sd_region, *, so=0, pitch=12.0, density=0.60, depth=L_LOW):
    """自动撒"机器上该有的小件"：检修盖、格栅、螺栓组、黄铜支架、舱口。

    为什么需要
    ----------
    基准件的表面不是"一块大板 + 一条缝"，而是**几十个小件密集排布**。
    人工摆零件永远摆不够（v2 每台只有 5-8 件，基准件目测 20-30 件），
    这一步把"摆零件"变成一次自动填充，是细节密度的最后一根支柱。

    形状全部来自 greeble 库，参数由该设备的 seed 决定，所以可复现。
    """
    s, fx = c.s, c.fx
    x, y = c.x, c.y
    pts = [(px, py) for px, py in fx.poisson(radius=pitch, so=so)
           if 7 < px < W - 7 and 7 < py < H - 7]
    rng = np.random.default_rng(c.seed + so * 131 + 7)
    for i, (px, py) in enumerate(pts):
        if rng.random() > density:
            continue
        kind = int(rng.choice(5, p=(0.22, 0.17, 0.17, 0.30, 0.14)))
        w = float(rng.uniform(7.0, 12.0))
        h = float(rng.uniform(5.5, 9.0))
        if kind == 0:                                  # 检修盖板
            d = sdf.intersect(sdf.rrect(x, y, px, py, w * 0.5, h * 0.5, 1.2), sd_region)
            s.draw(d, palette.BODY_HI, material=fx.brushed(0.0, 1.1, so=so + i),
                   depth=depth, top_shade=1.16, side_shade=0.56,
                   edge_hi=0.46, ao=0.30, surface=0.0)
            s.emboss(greeble.bolt_grid(x, y, px - w * 0.34, py - h * 0.34,
                                       px + w * 0.34, py + h * 0.34, 2, 2, 0.95),
                     hi=0.48, dark=0.42, width=0.9)
        elif kind == 1:                                # 散热格栅
            d = sdf.intersect(greeble.grille(x, y, px, py, w * 0.52, h * 0.42,
                                             2.7, 0.34, 90.0), sd_region)
            s.paint((d < 0).astype(np.float32), palette.DARK, 0.88)
            s.emboss(d, hi=0.48, dark=0.44, width=0.9)
        elif kind == 2:                                # 螺栓组
            d = sdf.intersect(greeble.bolt_grid(x, y, px - w * 0.32, py - h * 0.30,
                                                px + w * 0.32, py + h * 0.30, 2, 2, 1.0),
                              sd_region)
            s.paint(sdf.inner_edge(d, 0.9), palette.DARK, 0.8)
            s.emboss(d, hi=0.50, dark=0.42, width=0.95)
        elif kind == 3:                                # 黄铜支架（暖色来源）
            d = sdf.intersect(greeble.corner_bracket(x, y, px, py, w * 0.5, 1.5),
                              sd_region)
            s.draw(d, palette.RUST2, depth=depth, top_shade=1.14,
                   side_shade=0.60, edge_hi=0.44)
        else:                                          # 圆形舱口
            r = w * 0.42
            d = sdf.intersect(sdf.circle(x, y, px, py, r), sd_region)
            s.draw(d, palette.DARK, material=fx.pits(cell=4.0, steps=2, so=so + i),
                   depth=depth, top_shade=0.96, side_shade=0.52,
                   edge_hi=0.34, ao=0.34)
            s.emboss(greeble.bolt_ring(x, y, px, py, r * 0.62, 5, 0.9, phase=0.6),
                     hi=0.48, dark=0.40, width=0.9)


def edge_bolt_row(c, sd_region, y_line, n=9, r=1.25, x0=LEFT + 6, x1=RIGHT - 6):
    """铆钉行（家族统一件）：深色凹坑 + 亮顶。"""
    s = c.s
    row = sdf.intersect(greeble.bolt_row(c.x, c.y, x0, x1, y_line, n, r=r), sd_region)
    s.paint(sdf.inner_edge(row, 0.9), palette.DARK, 0.85)
    s.paint((row < 0).astype(np.float32), palette.LIGHT, 0.55)
    s.emboss(row, hi=0.44, dark=0.40, width=0.9)


def pipe_port(c, cx, cy, hw=5.0, hh=4.0, dark=True):
    """管线接口 1 处（车间体系叙事：它接在产线上）。"""
    s, x, y = c.s, c.x, c.y
    body = sdf.rrect(x, y, cx, cy, hw, hh, 1.4)
    s.draw(body, palette.DARK if dark else palette.RUST2, depth=L_MID,
           top_shade=0.95, side_shade=0.62, edge_hi=0.30)
    inner = sdf.rrect(x, y, cx, cy, hw * 0.55, hh * 0.55, 0.8)
    s.paint((inner < 0).astype(np.float32), c.ac, 0.6)
    s.emboss(greeble.bolt_ring(x, y, cx, cy, hw + 1.6, 4, 0.9, phase=0.78),
             hi=0.44, dark=0.36, width=0.9)


def copper_pipe(c, pts, r=1.7):
    """黄铜管路（第二机体色：让画面不至于全灰，这是基准件的实际配比）。"""
    s = c.s
    d = greeble.pipe_run(c.x, c.y, pts, r=r)
    s.draw(d, palette.RUST2, material=c.fx.brushed(90.0, 1.4, so=301),
           depth=L_MID, top_shade=1.10, side_shade=0.60,
           edge_hi=0.42, edge_hi_w=1.0, mat_lo=0.80, mat_hi=1.30)
    s.emboss(d, hi=0.30, dark=0.40, width=0.85)
    return d


def bright_metal(c, sd_region, *, depth=L_MID, so=0, mat=None, shade=1.16):
    """亮金属件（导轨 / 切削面 / 新加工面）——画面亮部的主要来源。

    基色用 LIGHT2（#707070），配合顶面系数与材质上限可越过 112 的亮部阈值。
    """
    s = c.s
    s.draw(sd_region, palette.LIGHT2, material=mat if mat is not None
           else c.fx.brushed(0.0, 0.9, so=so),
           depth=depth, top_shade=shade, side_shade=0.60,
           edge_hi=0.52, edge_hi_w=1.0, mat_lo=0.80, mat_hi=1.24, ao=0.30)


# ==========================================================================
# 公共框架：底壳 + 台面（每台传入自己的轮廓）
# ==========================================================================

def frame(c, hull, deck, *, deck_mat=None, deck_color=None, rust=0.9,
          deck_shade=1.09, seams=(26.0, 90.0), patch_cell=8.5, patch_so=0):
    s, fx = c.s, c.fx
    # ---- 明度档位：底壳压暗（这是"体块感"的来源）----
    # 诊断（_diag_gutter.py 三档面积实测）：
    #     基准件 PC_AutoFarm_M   深暗 0.46 / 主灰 0.34 / 亮灰 0.21
    #     生成件 v3 首版          深暗 0.30-0.52 / 主灰 **0.29-0.59** / 亮灰 0.10-0.26
    # 即中间调吃掉了一切——这是"发灰、发闷、像塑料"的根因，不是纹理不够。
    # 所以底壳的 top_shade 从 1.04 降到 0.74：底盘在俯视图里被上方机构遮挡，
    # 物理上就该落在深灰档；腾出来的面积正好补给亮灰。
    # 板缝用 DARK(#101010) 与亮线 SEAM_HI 两侧拉开，对比依然够大。
    s.draw(hull, palette.BODY_MID, material=fx.fbm(0.055, 3, so=11),
           depth=L_BASE, top_shade=0.74, side_shade=0.42,
           edge_hi=0.52, edge_hi_w=1.0, mat_lo=0.70, mat_hi=1.26,
           rust=rust, rust_field=fx.pits(cell=7.0, steps=3, so=21), rust_w=5.0,
           ao=0.42, surface=1.0, surface_so=patch_so)
    # 面板网 + 散落螺栓：机体骨架感
    panel_grid(c, hull, pitch=19.0, k=0.78)
    bolt_scatter(c, hull, so=patch_so + 11, pitch=patch_cell * 1.8, r=1.0)
    # ---- 台面：主工作面，升到亮灰档 ----
    # ⚠ deck_shade 此前是死参数（签名里有、函数体写死 1.06），一并修正。
    dm = deck_mat if deck_mat is not None else fx.brushed(angle=0.0, freq=0.5, so=37)
    s.draw(deck, deck_color if deck_color else palette.LIGHT, material=dm,
           depth=L_DECK, top_shade=deck_shade, side_shade=0.52,
           edge_hi=0.60, edge_hi_w=1.0, mat_lo=0.82, mat_hi=1.34, ao=0.40,
           surface=0.85, surface_so=patch_so + 40)
    deck_seams(c, deck, pitch=seams[0], angle=seams[1])
    # 小件自动撒布：铺在台面与机身留白处，之后被各台机构部分遮挡，
    # 形成"层层叠压"的机器观感（基准件的密度就是靠这个量级的零件数堆出来的）
    greeble_scatter(c, hull, so=patch_so + 200, pitch=patch_cell * 1.30,
                    density=0.72)
    c.hull = hull
    return hull, deck


def back_rail(c, hull, cx, cy, hw, hh, *, mat=None, angle=0.0, so=61,
              color=None, depth=L_HIGH, shade=1.14):
    """后立板 / 工具墙：比台面高一档，是全级通用件。"""
    s, fx, x, y = c.s, c.fx, c.x, c.y
    r = sdf.intersect(sdf.rrect(x, y, cx, cy, hw, hh, 2.0), hull)
    s.draw(r, color if color else palette.BODY_MID,
           material=mat if mat is not None else fx.brushed(angle, 0.55, so=so),
           depth=depth, top_shade=shade, side_shade=0.56,
           edge_hi=0.34, edge_hi_w=1.2, mat_lo=0.80, mat_hi=1.22, ao=0.30)
    return r


# ==========================================================================
# 01 冶炼 —— 圆炉体 + 方台面（SDF 平滑并集 / fBm + 湍流热辉光）
# ==========================================================================

def dev_smelter(c):
    s, fx, x, y = c.s, c.fx, c.x, c.y
    ntc = notch_sd(x, y, edge=RIGHT, cy=84.0, hh=12.0, depth=14.0)
    body = sdf.smooth_union(sdf.circle(x, y, 50.0, 64.0, 40.0),
                            sdf.rrect(x, y, 84.0, 70.0, 33.0, 37.0, 5.0), 5.0)
    hull = sdf.subtract(body, ntc)
    deck = sdf.subtract(sdf.rrect(x, y, 82.0, 68.0, 28.0, 31.0, 4.0), ntc)

    footwell(c, ntc)
    frame(c, hull, deck)

    # 炉体盖（比台面高一档的圆盘 + 螺栓环）
    lid = sdf.circle(x, y, 48.0, 64.0, 35.0)
    s.draw(lid, palette.BODY_MID, material=fx.pits(cell=6.5, steps=3, so=3),
           depth=L_LOW, top_shade=1.10, side_shade=0.58,
           edge_hi=0.38, edge_hi_w=1.3, ao=0.34)
    s.emboss(greeble.bolt_ring(x, y, 48.0, 64.0, 30.5, 12, 1.15), hi=0.34, dark=0.26)
    s.emboss(greeble.grille(x, y, 48.0, 92.0, 26.0, 3.2, 3.4, 0.34, 0.0),
             hi=0.30, dark=0.28, width=0.9)

    # 坩埚 + 铜质感应线圈
    cru = sdf.circle(x, y, 48.0, 64.0, 19.0)
    s.draw(cru, palette.DARK, material=fx.pits(cell=5.0, steps=3, so=7),
           depth=L_MID, top_shade=0.94, side_shade=0.50, edge_hi=0.28)
    s.draw(sdf.annulus(x, y, 48.0, 64.0, 19.0, 22.5), palette.RUST2,
           material=fx.brushed(0.0, 1.3, so=9),
           depth=L_HIGH, top_shade=1.12, side_shade=0.60,
           edge_hi=0.44, edge_hi_w=1.2, mat_lo=0.82, mat_hi=1.34)
    for k in range(6):
        a = np.deg2rad(k * 60.0)
        s.emboss(sdf.capsule(x, y, 48.0 + np.cos(a) * 20.0, 64.0 + np.sin(a) * 20.0,
                             48.0 + np.cos(a) * 22.5, 64.0 + np.sin(a) * 22.5, 0.8),
                 hi=0.34, dark=0.30, width=0.8)

    # 内腔热辉光：湍流场色映射（不越色板）
    inner = sdf.circle(x, y, 48.0, 64.0, 15.5)
    glow = fx.turbulence(freq=0.10, oct=4, so=5)
    s.paint(np.clip((glow - 0.34) * 2.0, 0, 1) * (inner < 0), palette.MOTION_ORANGE2, 0.94)
    s.paint(np.clip((glow - 0.58) * 3.0, 0, 1) * (inner < 0), palette.HAZARD, 0.95)
    s.paint(np.clip((glow - 0.78) * 4.0, 0, 1) * (inner < 0), palette.LIGHT2, 0.60)

    # 钢蓝能量观察窗 + 出料槽
    win = sdf.intersect(sdf.rrect(x, y, 92.0, 52.0, 12.5, 5.0, 1.4), deck)
    s.draw(win, palette.ENERGY_BLUE, material=fx.fbm(0.12, 3, so=13),
           depth=L_LOW, top_shade=1.08, side_shade=0.58, edge_hi=0.38, emissive=0.16)
    s.emboss(greeble.vents(x, y, 92.0, 52.0, 12.5, 5.0, 4, 0.6), hi=0.30, dark=0.26, width=0.8)

    chute = sdf.intersect(sdf.rrect(x, y, 74.0, 100.0, 15.0, 3.2, 1.2), deck)
    s.draw(chute, palette.DARK, depth=L_LOW, top_shade=0.84, side_shade=0.48, edge_hi=0.24)

    copper_pipe(c, [(82.0, 36.0), (100.0, 36.0), (100.0, 50.0)], r=1.6)
    pipe_port(c, 18.0, 46.0)
    smart_screen(c, 96.0, 82.0, hw=11.0, hh=6.0, lines=3)
    lamp_row(c, 76.0, 104.0, 3, 1.4, lit=2, sep=5.4)
    hazard_band(c, hull, 84.0, 90.0, 26.0, 3.0)
    edge_bolt_row(c, hull, 30.0, 8, 1.1, x0=30.0, x1=96.0)


# ==========================================================================
# 02 锻造 —— 砧座 + 后立架 + 锤臂（SDF 平滑并集 / 各向异性拉丝）
# ==========================================================================

def dev_forge(c):
    s, fx, x, y = c.s, c.fx, c.x, c.y
    ntc = notch_sd(x, y, edge=RIGHT, cy=86.0, hh=11.0, depth=13.0)
    frame_pts = (
        (22.0, 30.0), (106.0, 30.0), (112.0, 40.0), (112.0, 100.0),
        (104.0, 106.0), (22.0, 106.0), (16.0, 98.0), (16.0, 38.0),
    )
    hull = sdf.subtract(sdf.polygon(x, y, frame_pts), ntc)
    deck = sdf.subtract(sdf.rrect(x, y, 64.0, 70.0, 40.0, 30.0, 4.0), ntc)

    footwell(c, ntc)
    frame(c, hull, deck, seams=(24.0, 0.0))

    # 后立架（北侧工具墙）
    rail = back_rail(c, hull, 64.0, 42.0, 44.0, 11.0)
    s.emboss(greeble.grille(x, y, 64.0, 42.0, 40.0, 6.5, 3.6, 0.30, 90.0),
             hi=0.34, dark=0.30, width=0.9)
    for i in range(4):                       # 工具轨：悬挂的锤头/钳
        hx = 30.0 + i * 9.0
        s.draw(sdf.intersect(sdf.rrect(x, y, hx, 50.0, 1.6, 4.2, 0.6), rail),
               palette.RUST2, depth=L_TOP, top_shade=1.06, side_shade=0.6, edge_hi=0.32)

    # 砧座（Worley 铸件颗粒）+ 顶面凹槽
    anvil = sdf.intersect(sdf.rrect(x, y, 62.0, 84.0, 17.0, 11.0, 2.5), deck)
    s.draw(anvil, palette.BODY_HI, material=fx.worley(cell=5.0, so=2),
           depth=L_HIGH, top_shade=1.20, side_shade=0.60,
           edge_hi=0.44, edge_hi_w=1.3, mat_lo=0.80, mat_hi=1.30)
    s.paint((sdf.intersect(sdf.rrect(x, y, 62.0, 84.0, 13.5, 1.0, 0.5), anvil) < 0)
            .astype(np.float32), palette.DARK, 0.55)
    s.emboss(greeble.bolt_grid(x, y, 50.0, 78.0, 74.0, 90.0, 2, 2, 1.1), hi=0.30, dark=0.26)

    # 锤臂 + 锤头（橙色 = 运动机构语义）
    s.draw(sdf.capsule(x, y, 62.0, 44.0, 62.0, 62.0, 4.2), palette.MOTION_ORANGE2,
           material=fx.brushed(90.0, 1.0, so=19),
           depth=L_TOP, top_shade=1.14, side_shade=0.58, edge_hi=0.44, mat_lo=0.82, mat_hi=1.30)
    s.paint((sdf.capsule(x, y, 62.0, 44.0, 62.0, 62.0, 1.6) < 0).astype(np.float32),
            palette.MOTION_ORANGE, 0.55)
    s.draw(sdf.rrect(x, y, 62.0, 60.0, 8.5, 4.2, 1.5), palette.MOTION_ORANGE,
           depth=L_TOP, top_shade=1.16, side_shade=0.62, edge_hi=0.46)
    s.emboss(greeble.bolt_ring(x, y, 62.0, 44.0, 3.2, 6, 0.85), hi=0.32, dark=0.28)

    # 脚踏杆 + 液压管
    s.draw(sdf.capsule(x, y, 34.0, 98.0, 48.0, 98.0, 2.2), palette.MOTION_ORANGE2,
           depth=L_LOW, top_shade=1.06, side_shade=0.6, edge_hi=0.30)
    copper_pipe(c, [(22.0, 84.0), (22.0, 62.0), (36.0, 54.0)], r=1.5)
    pipe_port(c, 100.0, 48.0, hw=4.5, hh=5.5)
    smart_screen(c, 100.0, 88.0, hw=11.0, hh=6.0, lines=3)
    lamp_row(c, 26.0, 104.0, 3, 1.4, lit=2, sep=5.4)
    hazard_band(c, hull, 64.0, 104.0, 34.0, 2.8)
    edge_bolt_row(c, rail, 46.0, 10, 1.05, x0=28.0, x1=100.0)


# ==========================================================================
# 03 热处理 —— 左马弗炉 + 右淬火槽（SDF 圆角腔体 / 元胞自动机凹坑）
# ==========================================================================

def dev_heattreat(c):
    s, fx, x, y = c.s, c.fx, c.x, c.y
    ntc = notch_sd(x, y, edge=RIGHT, cy=86.0, hh=11.0, depth=13.0)
    left_box = sdf.rrect(x, y, 36.0, 64.0, 27.0, 41.0, 4.0)
    right_box = sdf.rrect(x, y, 92.0, 76.0, 26.0, 30.0, 4.0)
    hull = sdf.subtract(sdf.union(left_box, right_box), ntc)
    deck = sdf.subtract(sdf.union(sdf.rrect(x, y, 36.0, 64.0, 24.0, 37.0, 3.0),
                                  sdf.rrect(x, y, 92.0, 76.0, 23.0, 26.0, 3.0)), ntc)

    footwell(c, ntc)
    frame(c, hull, deck, seams=(30.0, 0.0))

    # 左：马弗炉体（元胞凹坑做铸铁面）
    s.draw(sdf.rrect(x, y, 36.0, 60.0, 22.0, 17.0, 3.0), palette.BODY_MID,
           material=fx.pits(cell=5.5, steps=3, so=13),
           depth=L_HIGH, top_shade=1.14, side_shade=0.58,
           edge_hi=0.40, edge_hi_w=1.3, mat_lo=0.78, mat_hi=1.26)
    s.emboss(greeble.bolt_ring(x, y, 36.0, 60.0, 18.5, 10, 1.05), hi=0.34, dark=0.26)
    s.emboss(greeble.grille(x, y, 36.0, 88.0, 19.0, 5.0, 3.2, 0.32, 0.0),
             hi=0.32, dark=0.30, width=0.9)

    # 炉门 + 内腔热辉光（山脊噪声做炉膛冷隔）
    door = sdf.rrect(x, y, 36.0, 60.0, 7.5, 8.5, 1.6)
    s.draw(door, palette.DARK, depth=L_MID, top_shade=0.92, side_shade=0.52, edge_hi=0.30)
    heat = fx.ridged(freq=0.16, oct=3, so=17)
    s.paint(np.clip((heat - 0.42) * 2.2, 0, 1) * (door < 0), palette.MOTION_ORANGE2, 0.88)
    s.paint(np.clip((heat - 0.66) * 3.0, 0, 1) * (door < 0), palette.HAZARD, 0.84)
    s.emboss(greeble.bolt_ring(x, y, 36.0, 60.0, 9.8, 8, 0.9), hi=0.32, dark=0.28)

    # 右：淬火槽（凹下）+ 钢蓝冷却液
    s.draw(sdf.rrect(x, y, 92.0, 78.0, 20.0, 18.0, 3.0), palette.DARK,
           material=fx.pits(cell=6.0, steps=2, so=23),
           depth=L_LOW, top_shade=0.90, side_shade=0.50, edge_hi=0.30)
    s.draw(sdf.rrect(x, y, 92.0, 78.0, 17.0, 15.0, 2.4), palette.ENERGY_BLUE,
           material=fx.turbulence(freq=0.14, oct=3, so=29),
           depth=L_LOW, top_shade=1.02, side_shade=0.6, edge_hi=0.36, emissive=0.12)
    s.emboss(greeble.bolt_row(x, y, 80.0, 104.0, 96.0, 5, 0.95), hi=0.28, dark=0.24)

    # 中间夹钳架（黄铜）
    for i in range(3):
        s.draw(sdf.capsule(x, y, 62.0, 54.0 + i * 8.0, 72.0, 58.0 + i * 8.0, 1.5),
               palette.RUST2, depth=L_MID, top_shade=1.10, side_shade=0.62, edge_hi=0.34)
    # 淬火槽沿口导轨 + 炉底座轨（亮金属：本台亮部来源）
    bright_metal(c, sdf.intersect(sdf.rrect(x, y, 92.0, 97.0, 20.0, 3.2, 1.0), deck),
                 depth=L_MID, so=33, shade=1.20)
    bright_metal(c, sdf.intersect(sdf.rrect(x, y, 36.0, 97.0, 20.0, 3.0, 1.0), deck),
                 depth=L_MID, so=35, shade=1.18)
    copper_pipe(c, [(66.0, 30.0), (66.0, 44.0)], r=1.6)
    # 热电偶探头 + 淬火槽黄铜阀门手轮：热处理的标配件，也是本台暖色来源
    # （描边收尾会吃掉边缘彩色像素，实测 warm_ratio 从 0.102 掉到 0.088）。
    for px_, py_ in ((50.0, 40.0), (74.0, 40.0)):
        s.draw(sdf.circle(x, y, px_, py_, 2.2), palette.RUST2,
               depth=L_MID, top_shade=1.16, side_shade=0.62, edge_hi=0.46)
        s.draw(sdf.capsule(x, y, px_, py_ + 2.0, px_, py_ + 5.4, 0.7), palette.RUST2,
               depth=L_MID, top_shade=1.10, side_shade=0.60, edge_hi=0.38)
    s.draw(sdf.annulus(x, y, 92.0, 88.0, 2.6, 4.2), palette.RUST2,
           depth=L_MID, top_shade=1.14, side_shade=0.62, edge_hi=0.44)
    s.emboss(greeble.dial(x, y, 92.0, 88.0, 4.2, 8, 1.2), hi=0.30, dark=0.26, width=0.8)
    pipe_port(c, 16.0, 44.0, hw=4.5, hh=5.5)
    smart_screen(c, 96.0, 100.0, hw=10.5, hh=5.5, lines=3)
    lamp_row(c, 24.0, 100.0, 3, 1.35, lit=2, sep=5.0)
    hazard_band(c, hull, 92.0, 100.0, 22.0, 2.6)
    edge_bolt_row(c, hull, 30.0, 9, 1.05, x0=22.0, x1=106.0)


# ==========================================================================
# 04 粗加工 —— 长床身车床 + 主轴箱（Voronoi 分格 / Worley F2-F1 龟裂）
# ==========================================================================

def dev_roughmach(c):
    s, fx, x, y = c.s, c.fx, c.x, c.y
    ntc = notch_sd(x, y, edge=RIGHT, cy=88.0, hh=10.0, depth=12.0)
    bed = sdf.rrect(x, y, 68.0, 80.0, 56.0, 24.0, 3.0)
    head = sdf.rrect(x, y, 26.0, 70.0, 18.0, 40.0, 4.0)
    hull = sdf.subtract(sdf.union(bed, head), ntc)
    deck = sdf.subtract(sdf.rrect(x, y, 74.0, 78.0, 44.0, 19.0, 3.0), ntc)

    footwell(c, ntc)

    # 台面材质：Voronoi 分格 + 龟裂（机床导轨刮研花纹）
    pts = [(px, py) for px, py in fx.poisson(radius=7.0, so=4)
           if 22 < px < 118 and 58 < py < 100]
    crack = fx.worley_crack(cell=8.5, so=41)
    scrap = np.clip(fx.vor_edges(pts, 0.9) * 0.5 + crack * 0.5, 0, 1)
    frame(c, hull, deck, deck_mat=scrap, seams=(22.0, 0.0))
    s.emboss(fx.pits(cell=9.0, steps=2, so=43), hi=0.20, dark=0.20, width=1.0)

    # 主轴箱 + 橙色旋转卡盘
    s.draw(sdf.rrect(x, y, 28.0, 68.0, 15.0, 30.0, 3.0), palette.BODY_HI,
           material=fx.worley(cell=5.0, so=47),
           depth=L_HIGH, top_shade=1.16, side_shade=0.58, edge_hi=0.42, mat_lo=0.80, mat_hi=1.28)
    s.emboss(greeble.grille(x, y, 28.0, 92.0, 12.0, 5.0, 3.2, 0.32, 0.0), hi=0.32, dark=0.30)

    s.draw(sdf.circle(x, y, 42.0, 84.0, 9.0), palette.MOTION_ORANGE2,
           material=fx.rings(42.0, 84.0, 2.6, 3),
           depth=L_HIGH, top_shade=1.14, side_shade=0.6, edge_hi=0.44)
    for k in range(3):
        a = np.deg2rad(k * 120.0 + 30.0)
        s.draw(sdf.capsule(x, y, 42.0 + np.cos(a) * 2.6, 84.0 + np.sin(a) * 2.6,
                           42.0 + np.cos(a) * 7.0, 84.0 + np.sin(a) * 7.0, 1.5),
               palette.MOTION_ORANGE, depth=L_HIGH, top_shade=1.12, side_shade=0.62, edge_hi=0.34)
    s.emboss(greeble.bolt_ring(x, y, 42.0, 84.0, 6.4, 6, 0.8), hi=0.30, dark=0.26)

    # 导轨（亮金属）+ 刀架滑板 + 手轮
    bright_metal(c, sdf.intersect(sdf.rrect(x, y, 82.0, 74.0, 40.0, 3.0, 0.8), deck),
                 depth=L_MID, so=53, shade=1.22)
    bright_metal(c, sdf.intersect(sdf.rrect(x, y, 82.0, 62.0, 40.0, 2.6, 0.8), deck),
                 depth=L_MID, so=55, shade=1.20)
    s.emboss(greeble.tslot(x, y, 82.0, 74.0, 34.0, 2.0, 12.0, 0.5, 0.0),
             hi=0.34, dark=0.30, width=0.8)
    s.draw(sdf.intersect(sdf.rrect(x, y, 62.0, 66.0, 9.0, 5.5, 1.2), deck),
           palette.BODY_HI, material=fx.brushed(0.0, 0.8, so=53),
           depth=L_MID, top_shade=1.20, side_shade=0.6, edge_hi=0.42)
    for i in range(3):                       # 进给手轮
        hx = 54.0 + i * 16.0
        s.draw(sdf.intersect(sdf.circle(x, y, hx, 98.0, 4.6), deck),
               palette.BODY_MID, material=fx.rings(hx, 98.0, 1.8, 61 + i),
               depth=L_MID, top_shade=1.14, side_shade=0.6, edge_hi=0.38)
        s.emboss(greeble.bolt_ring(x, y, hx, 98.0, 3.0, 5, 0.7), hi=0.30, dark=0.26)

    # 切屑盘（黄铜屑）
    s.draw(sdf.intersect(sdf.rrect(x, y, 82.0, 100.0, 34.0, 4.5, 1.5), deck),
           palette.DARK, material=fx.pits(cell=4.0, steps=2, so=59),
           depth=L_LOW, top_shade=0.94, side_shade=0.52, edge_hi=0.26)
    s.paint(np.clip((fx.dla(cells=40, n=260, so=63) - 0.55) * 3.0, 0, 1) *
            (sdf.intersect(sdf.rrect(x, y, 82.0, 100.0, 33.0, 3.8, 1.5), deck) < 0),
            palette.RUST2, 0.45)

    copper_pipe(c, [(10.0, 46.0), (10.0, 62.0), (20.0, 66.0)], r=1.5)
    # 床身再补一层细件（本台以大面积床身为主，单靠全局撒布密度不够）
    greeble_scatter(c, deck, so=71, pitch=9.0, density=0.55)
    smart_screen(c, 96.0, 62.0, hw=11.0, hh=5.5, lines=2)
    lamp_row(c, 22.0, 104.0, 3, 1.35, lit=2, sep=5.0)
    hazard_band(c, hull, 74.0, 102.0, 40.0, 2.8)
    edge_bolt_row(c, hull, 52.0, 12, 1.05, x0=24.0, x1=112.0)


# ==========================================================================
# 05 精密加工 —— 门型龙门 + 台面（SDF 布尔 / Worley 花岗岩斑点）
# ==========================================================================

def dev_precmach(c):
    s, fx, x, y = c.s, c.fx, c.x, c.y
    ntc = notch_sd(x, y, edge=RIGHT, cy=90.0, hh=10.0, depth=11.0)
    col_l = sdf.rrect(x, y, 24.0, 74.0, 11.0, 42.0, 3.0)
    col_r = sdf.rrect(x, y, 104.0, 74.0, 11.0, 42.0, 3.0)
    base = sdf.rrect(x, y, 64.0, 94.0, 52.0, 17.0, 3.0)
    beam = sdf.rrect(x, y, 64.0, 32.0, 52.0, 9.0, 2.0)
    hull = sdf.subtract(sdf.union(col_l, col_r, base, beam), ntc)
    deck = sdf.subtract(sdf.rrect(x, y, 64.0, 84.0, 38.0, 20.0, 3.0), ntc)

    footwell(c, ntc)
    frame(c, hull, deck, seams=(24.0, 90.0))

    # 花岗岩底座（Worley 双尺度斑点）
    granite = np.clip(fx.worley(cell=3.2, so=67) * 0.55 +
                      fx.worley(cell=7.5, so=71) * 0.45, 0, 1)
    s.draw(sdf.rrect(x, y, 64.0, 90.0, 44.0, 12.0, 2.5), palette.BODY_HI,
           material=granite, depth=L_LOW, top_shade=1.14, side_shade=0.56,
           edge_hi=0.38, mat_lo=0.74, mat_hi=1.36)
    s.emboss(greeble.tslot(x, y, 64.0, 90.0, 44.0, 12.0, 12.0, 0.42, 0.0),
             hi=0.30, dark=0.28, width=0.9)

    # 左右立柱（亮金属 + 格栅）
    for cx, so in ((24.0, 73), (104.0, 79)):
        bright_metal(c, sdf.intersect(sdf.rrect(x, y, cx, 74.0, 8.0, 36.0, 2.0), hull),
                     depth=L_HIGH, so=so, shade=1.18)
        s.emboss(greeble.vents(x, y, cx, 74.0, 8.0, 36.0, 6, 0.9, 0.72),
                 hi=0.30, dark=0.28, width=0.9)

    # 顶横梁 + 橙色主轴
    s.draw(sdf.rrect(x, y, 64.0, 32.0, 52.0, 6.5, 2.0), palette.BODY_MID,
           material=fx.brushed(0.0, 0.7, so=83),
           depth=L_TOP, top_shade=1.14, side_shade=0.58, edge_hi=0.44)
    s.emboss(greeble.rack_teeth(x, y, 20.0, 108.0, 42.0, 3.2, 2.2), hi=0.32, dark=0.28)
    s.draw(sdf.rrect(x, y, 64.0, 46.0, 7.0, 8.0, 2.0), palette.BODY_HI,
           material=fx.brushed(90.0, 0.8, so=73),
           depth=L_TOP, top_shade=1.16, side_shade=0.58, edge_hi=0.44)
    s.draw(sdf.circle(x, y, 64.0, 58.0, 6.2), palette.MOTION_ORANGE2,
           material=fx.rings(64.0, 58.0, 2.0, 89),
           depth=L_TOP, top_shade=1.16, side_shade=0.62, edge_hi=0.48)
    s.paint((sdf.circle(x, y, 64.0, 58.0, 2.4) < 0).astype(np.float32),
            palette.MOTION_ORANGE, 0.88)

    # 数显屏 + 精调手轮
    smart_screen(c, 96.0, 62.0, hw=10.5, hh=5.5, lines=2)
    s.draw(sdf.circle(x, y, 96.0, 78.0, 4.4), palette.BODY_MID,
           material=fx.rings(96.0, 78.0, 1.7, 97),
           depth=L_MID, top_shade=1.10, side_shade=0.6, edge_hi=0.36)
    copper_pipe(c, [(38.0, 44.0), (38.0, 56.0), (48.0, 62.0)], r=1.4)
    # 冷却液铜管回路（第二段）+ 黄铜管夹：精密加工中心必然有冷却管路，
    # 同时也是本台暖色像素的来源（验收口径 warm_ratio >= 0.09）。
    copper_pipe(c, [(88.0, 100.0), (88.0, 88.0), (78.0, 82.0), (66.0, 82.0)], r=1.4)
    for bx, by in ((88.0, 94.0), (78.0, 82.0), (66.0, 82.0)):
        s.draw(sdf.circle(x, y, bx, by, 2.0), palette.RUST2,
               depth=L_MID, top_shade=1.16, side_shade=0.62, edge_hi=0.44)
    pipe_port(c, 40.0, 106.0, hw=5.0, hh=3.6)
    lamp_row(c, 34.0, 84.0, 3, 1.35, lit=2, sep=5.0)
    hazard_band(c, hull, 64.0, 106.0, 44.0, 2.6)
    edge_bolt_row(c, beam, 38.0, 11, 1.05, x0=18.0, x1=110.0)


# ==========================================================================
# 06 枪管 —— 满宽长床身 + 端塔（一维中点位移 / 膛线起伏）
# ==========================================================================

def dev_barrel(c):
    s, fx, x, y = c.s, c.fx, c.x, c.y
    ntc = notch_sd(x, y, edge=RIGHT, cy=90.0, hh=9.0, depth=10.0)
    prof = fx.midline(n=W, roughness=0.5, so=3, span=2.4)
    wobble = np.tile(prof[None, :], (H, 1)) * 0.5 + 0.5
    lathe = np.clip(fx.brushed(0.0, 0.6, so=97) * 0.6 + wobble * 0.4, 0, 1)

    bed = sdf.rrect(x, y, 64.0, 76.0, 56.0, 22.0, 3.0)
    head = sdf.rrect(x, y, 24.0, 64.0, 15.0, 34.0, 3.0)
    tail = sdf.rrect(x, y, 106.0, 66.0, 12.0, 28.0, 3.0)
    hull = sdf.subtract(sdf.union(bed, head, tail), ntc)
    deck = sdf.subtract(sdf.rrect(x, y, 70.0, 76.0, 40.0, 17.0, 3.0), ntc)

    footwell(c, ntc)
    # 本台有大面积亮金属枪管坯，台面压暗一档，避免整机明度冲过上限
    frame(c, hull, deck, deck_mat=lathe, deck_color=palette.BODY_HI, seams=(20.0, 0.0))

    # 枪管坯（长胶囊 + 中点位移起伏）——亮金属，画面亮部主来源
    bright_metal(c, sdf.intersect(
        sdf.capsule(x, y, 34.0, 66.0, 96.0, 66.0, 5.0), deck),
        depth=L_HIGH, so=99, mat=wobble, shade=1.10)
    s.emboss(greeble.roller_chain(x, y, 40.0, 92.0, 62.0, 2.0, 4.4), hi=0.26, dark=0.24)

    # 中心架 3 座（黄铜 V 形托）
    for cx in (48.0, 65.0, 82.0):
        s.draw(sdf.intersect(sdf.rrect(x, y, cx, 66.0, 2.6, 7.5, 1.0), deck),
               palette.RUST2, material=fx.worley(cell=4.0, so=101),
               depth=L_HIGH, top_shade=1.12, side_shade=0.6, edge_hi=0.40)
        s.emboss(greeble.bolt_col(x, y, cx, 70.0, 84.0, 3, 0.85), hi=0.30, dark=0.26)

    # 深孔钻主轴（橙色运动机构）+ 尾座
    s.draw(sdf.rrect(x, y, 26.0, 64.0, 9.0, 20.0, 2.5), palette.MOTION_ORANGE2,
           material=fx.brushed(90.0, 0.9, so=103),
           depth=L_HIGH, top_shade=1.14, side_shade=0.6, edge_hi=0.44)
    s.emboss(greeble.sprocket(x, y, 26.0, 50.0, 5.0, 10, 1.4, 2.0), hi=0.34, dark=0.30)
    bright_metal(c, sdf.intersect(sdf.rrect(x, y, 106.0, 66.0, 8.0, 18.0, 2.0), hull),
                 depth=L_HIGH, so=107, shade=1.18)
    s.emboss(greeble.bolt_col(x, y, 106.0, 54.0, 80.0, 4, 1.0), hi=0.30, dark=0.26)

    # 排屑槽 + 工具架
    s.draw(sdf.intersect(sdf.rrect(x, y, 70.0, 96.0, 40.0, 3.2, 1.4), deck),
           palette.DARK, material=fx.pits(cell=5.0, steps=2, so=109),
           depth=L_LOW, top_shade=0.94, side_shade=0.52, edge_hi=0.26)
    for i in range(5):
        s.draw(sdf.intersect(sdf.rrect(x, y, 52.0 + i * 8.0, 46.0, 2.0, 4.4, 0.7), deck),
               palette.RUST2, depth=L_MID, top_shade=1.10, side_shade=0.62, edge_hi=0.34)

    copper_pipe(c, [(16.0, 40.0), (16.0, 58.0)], r=1.6)
    greeble_scatter(c, deck, so=61, pitch=8.5, density=0.62)
    smart_screen(c, 100.0, 96.0, hw=10.0, hh=5.0, lines=2)
    lamp_row(c, 30.0, 104.0, 3, 1.35, lit=2, sep=5.0)
    hazard_band(c, hull, 70.0, 102.0, 42.0, 2.6)
    edge_bolt_row(c, hull, 100.0, 12, 1.05, x0=20.0, x1=110.0)


# ==========================================================================
# 07 表面处理 —— 三连槽 + 排风罩（SDF 槽体 / Gray-Scott 反应扩散）
# ==========================================================================

def dev_surface(c):
    s, fx, x, y = c.s, c.fx, c.x, c.y
    ntc = notch_sd(x, y, edge=RIGHT, cy=90.0, hh=10.0, depth=12.0)
    bench = sdf.rrect(x, y, 64.0, 74.0, 54.0, 36.0, 4.0)
    hood = sdf.rrect(x, y, 62.0, 28.0, 44.0, 8.0, 2.0)
    hull = sdf.subtract(sdf.union(bench, hood), ntc)
    deck = sdf.subtract(sdf.rrect(x, y, 64.0, 76.0, 48.0, 28.0, 3.0), ntc)

    footwell(c, ntc)
    frame(c, hull, deck, seams=(30.0, 90.0))

    # 三个浅槽：镀槽 / 漂洗 / 干燥（横排）
    rd = fx.reaction(cells=72, steps=220, so=109, f=0.0367, k=0.0649)
    tanks = [(32.0, 68.0, palette.BIO_GREEN2, rd, palette.BIO_GREEN),
             (64.0, 68.0, palette.ENERGY_BLUE, fx.turbulence(0.14, 3, so=113), None),
             (96.0, 68.0, palette.DARK2, fx.pits(cell=4.5, steps=2, so=115), None)]
    for cx, cy, col, mat, hi_col in tanks:
        s.draw(sdf.rrect(x, y, cx, cy, 13.0, 15.0, 2.5), palette.DARK,
               material=fx.pits(cell=6.0, steps=2, so=117),
               depth=L_LOW, top_shade=0.90, side_shade=0.50, edge_hi=0.30)
        s.draw(sdf.rrect(x, y, cx, cy, 10.5, 12.5, 1.8), col, material=mat,
               depth=L_LOW, top_shade=1.04, side_shade=0.6, edge_hi=0.36,
               # mat_lo/mat_hi 收窄到 0.86-1.14：Gray-Scott 场当"液体"用，
               # 不能让它做出高对比斑块。默认的 0.62-1.34 会把镀槽读成一块
               # 长满苔藓的海绵（5 倍放大目检确认），而不是"槽里的药水"。
               mat_lo=0.92, mat_hi=1.07,
               emissive=0.10 if cx != 96.0 else 0.05)
        if hi_col:
            # 0.72 → 0.22：只留液面翻涌的暗示，不出实心亮斑
            s.paint(np.clip((mat - 0.62) * 3.0, 0, 1) *
                    (sdf.rrect(x, y, cx, cy, 10.5, 12.5, 1.8) < 0), hi_col, 0.22)
        s.emboss(greeble.bolt_row(x, y, cx - 11.0, cx + 11.0, cy + 17.5, 4, 0.95),
                 hi=0.28, dark=0.24)

    # 排风罩 + 风道（黄铜）
    s.draw(sdf.rrect(x, y, 62.0, 28.0, 44.0, 6.5, 2.0), palette.BODY_MID,
           material=fx.brushed(0.0, 0.8, so=127),
           depth=L_HIGH, top_shade=1.12, side_shade=0.58, edge_hi=0.42)
    s.emboss(greeble.grille(x, y, 62.0, 28.0, 40.0, 4.0, 3.4, 0.32, 90.0),
             hi=0.30, dark=0.28, width=0.9)
    copper_pipe(c, [(18.0, 36.0), (18.0, 62.0), (30.0, 68.0)], r=1.6)
    copper_pipe(c, [(108.0, 36.0), (108.0, 62.0)], r=1.5)

    # 阳极挂架（黄铜钩排）
    for i in range(5):
        s.draw(sdf.intersect(sdf.rrect(x, y, 26.0 + i * 8.0, 46.0, 1.5, 4.5, 0.6), deck),
               palette.RUST2, depth=L_MID, top_shade=1.10, side_shade=0.62, edge_hi=0.32)

    pipe_port(c, 16.0, 96.0, hw=5.0, hh=4.0)
    smart_screen(c, 100.0, 100.0, hw=10.5, hh=5.0, lines=3)
    lamp_row(c, 30.0, 102.0, 3, 1.35, lit=2, sep=5.0)
    hazard_band(c, hull, 64.0, 108.0, 46.0, 2.6)
    edge_bolt_row(c, hull, 44.0, 10, 1.05, x0=22.0, x1=106.0)


# ==========================================================================
# 08 木托 / 护木 —— 台面 + CNC 龙门 + 集尘塔（SDF 龙门差集 / 木纹年轮）
# ==========================================================================

def dev_furniture(c):
    s, fx, x, y = c.s, c.fx, c.x, c.y
    wood = fx.wood(scale=15.0, so=137)
    ntc = notch_sd(x, y, edge=RIGHT, cy=90.0, hh=10.0, depth=12.0)
    bench = sdf.rrect(x, y, 78.0, 76.0, 40.0, 32.0, 4.0)
    tower = sdf.rrect(x, y, 24.0, 62.0, 15.0, 42.0, 4.0)
    gantry = sdf.rrect(x, y, 74.0, 44.0, 34.0, 5.0, 2.0)
    hull = sdf.subtract(sdf.union(bench, tower, gantry), ntc)
    deck = sdf.subtract(sdf.rrect(x, y, 78.0, 78.0, 35.0, 25.0, 3.0), ntc)

    footwell(c, ntc)
    frame(c, hull, deck, seams=(28.0, 0.0))

    # 集尘塔（高，格栅 + 螺栓环）
    s.draw(sdf.rrect(x, y, 24.0, 62.0, 12.0, 38.0, 3.0), palette.BODY_MID,
           material=fx.pits(cell=5.0, steps=3, so=139),
           depth=L_TOP, top_shade=1.12, side_shade=0.56,
           edge_hi=0.40, edge_hi_w=1.3, mat_lo=0.78, mat_hi=1.24)
    s.emboss(greeble.grille(x, y, 24.0, 62.0, 9.5, 30.0, 3.6, 0.30, 90.0),
             hi=0.32, dark=0.30, width=0.9)
    s.emboss(greeble.bolt_ring(x, y, 24.0, 38.0, 11.5, 8, 1.0), hi=0.32, dark=0.26)

    # 集尘管：塔 -> 龙门（折线管路）
    copper_pipe(c, [(30.0, 46.0), (44.0, 46.0), (44.0, 40.0), (74.0, 40.0)], r=1.8)

    # 木料坯（浅木色，面积 < 20%，符合本级备注 2）
    stock = sdf.intersect(sdf.rrect(x, y, 78.0, 84.0, 30.0, 12.0, 2.5), deck)
    s.draw(stock, (0x6B, 0x4E, 0x2E), material=wood,
           depth=L_MID, top_shade=1.08, side_shade=0.6,
           edge_hi=0.40, edge_hi_w=1.2, mat_lo=0.72, mat_hi=1.32)
    cut = sdf.intersect(sdf.rrect(x, y, 78.0, 84.0, 22.0, 6.5, 9.0), deck)
    s.paint((cut < 0).astype(np.float32) * (stock < 0), palette.DARK, 0.38)

    # CNC 龙门横梁 + 橙色主轴
    s.draw(sdf.rrect(x, y, 74.0, 44.0, 34.0, 4.5, 1.5), palette.BODY_HI,
           material=fx.brushed(0.0, 0.8, so=139),
           depth=L_TOP, top_shade=1.14, side_shade=0.58, edge_hi=0.44)
    s.emboss(greeble.rack_teeth(x, y, 44.0, 104.0, 39.5, 3.0, 2.0), hi=0.32, dark=0.28)
    s.draw(sdf.rrect(x, y, 74.0, 56.0, 5.5, 7.0, 1.5), palette.MOTION_ORANGE2,
           material=fx.brushed(90.0, 1.0, so=149),
           depth=L_TOP, top_shade=1.14, side_shade=0.62, edge_hi=0.46)
    s.paint((sdf.circle(x, y, 74.0, 60.0, 2.6) < 0).astype(np.float32),
            palette.MOTION_ORANGE, 0.9)

    # 刀具库条（黄铜）
    for i in range(5):
        s.draw(sdf.intersect(sdf.rrect(x, y, 104.0 - i * 7.0, 66.0, 2.2, 3.6, 0.8), deck),
               palette.RUST2, depth=L_MID, top_shade=1.10, side_shade=0.62, edge_hi=0.34)
    bright_metal(c, sdf.intersect(sdf.rrect(x, y, 78.0, 68.0, 30.0, 2.0, 0.8), deck),
                 depth=L_MID, so=151, shade=1.20)

    pipe_port(c, 96.0, 106.0, hw=5.0, hh=3.6)
    smart_screen(c, 108.0, 96.0, hw=9.5, hh=5.0, lines=2)
    lamp_row(c, 56.0, 100.0, 3, 1.35, lit=2, sep=5.0)
    hazard_band(c, hull, 78.0, 106.0, 38.0, 2.6)
    edge_bolt_row(c, gantry, 50.0, 9, 1.0, x0=44.0, x1=104.0)


# ==========================================================================
# 09 供弹 —— L 形（台面 + 右后料架）（模板阵列 / DLA 枝晶 + 调色板交换）
# ==========================================================================

def dev_magazine(c):
    s, fx, x, y = c.s, c.fx, c.x, c.y
    ntc = notch_sd(x, y, edge=RIGHT, cy=96.0, hh=9.0, depth=12.0)
    bench = sdf.rrect(x, y, 60.0, 82.0, 50.0, 26.0, 4.0)
    rack = sdf.rrect(x, y, 100.0, 48.0, 18.0, 34.0, 3.0)
    hull = sdf.subtract(sdf.union(bench, rack), ntc)
    deck = sdf.subtract(sdf.rrect(x, y, 62.0, 82.0, 44.0, 21.0, 3.0), ntc)

    footwell(c, ntc)
    frame(c, hull, deck, seams=(26.0, 90.0))

    # 右后料架：零件盒 2x3 阵列（模板拼装 + 调色板交换）
    s.draw(sdf.rrect(x, y, 100.0, 48.0, 15.0, 31.0, 3.0), palette.DARK,
           material=fx.pits(cell=5.5, steps=2, so=155),
           depth=L_HIGH, top_shade=0.96, side_shade=0.54, edge_hi=0.34)
    mask, cid = greeble.scatter_rects(x, y, 100.0, 48.0, 20.0, 36.0, 2, 3, 11.0, 9.0, 2.5, 0.6, 163)
    cols = [palette.RUST2, palette.MOTION_ORANGE2, palette.BODY_MID,
            palette.HAZARD2, palette.LIGHT, palette.BODY_HI]
    for i in range(6):
        m = ((mask > 0) & (cid == i)).astype(np.float32)
        s.paint(m, cols[i], 0.78)
        s.emboss(sdf.rrect(x, y, 100.0, 48.0, 20.0, 36.0, 2.0), hi=0.0, dark=0.16, width=1.0)
    s.emboss(greeble.scatter_rects(x, y, 100.0, 48.0, 20.0, 36.0, 2, 3,
                                   11.0, 9.0, 2.5, 0.0, 163)[0] - 0.5,
             hi=0.26, dark=0.24, width=0.8)

    # 弹簧卷绕主轴（橙色）
    s.draw(sdf.intersect(sdf.circle(x, y, 44.0, 76.0, 11.0), deck),
           palette.MOTION_ORANGE2, material=fx.rings(44.0, 76.0, 2.4, 151),
           depth=L_HIGH, top_shade=1.14, side_shade=0.62,
           edge_hi=0.46, mat_lo=0.84, mat_hi=1.30)
    s.paint((sdf.circle(x, y, 44.0, 76.0, 4.0) < 0).astype(np.float32),
            palette.MOTION_ORANGE, 0.92)
    s.emboss(greeble.bolt_ring(x, y, 44.0, 76.0, 8.4, 6, 0.85), hi=0.32, dark=0.28)

    # 弹匣体夹具 + 供弹条导轨
    s.draw(sdf.intersect(sdf.rrect(x, y, 44.0, 96.0, 10.0, 5.0, 1.2), deck),
           palette.BODY_HI, material=fx.worley(cell=4.0, so=157),
           depth=L_MID, top_shade=1.16, side_shade=0.6, edge_hi=0.42)
    s.draw(sdf.intersect(sdf.rrect(x, y, 82.0, 70.0, 22.0, 2.4, 0.9), deck),
           palette.BODY_MID, depth=L_MID, top_shade=1.10, side_shade=0.6, edge_hi=0.34)
    s.emboss(greeble.roller_chain(x, y, 62.0, 106.0, 70.0, 1.5, 4.0), hi=0.26, dark=0.24)

    # 供弹条（黄铜一排）
    for i in range(5):
        s.draw(sdf.intersect(sdf.rrect(x, y, 64.0 + i * 5.4, 70.0, 1.1, 3.4, 0.4), deck),
               palette.HAZARD2, depth=L_MID, top_shade=1.08, side_shade=0.6, edge_hi=0.30)

    # DLA 枝晶锈蚀（局限在边角 9px 环带内）
    dla = fx.dla(cells=48, n=420, so=167)
    s.paint(np.clip((dla - 0.52) * 2.8, 0, 1) * sdf.inner_edge(hull, 9.0), palette.RUST, 0.5)

    copper_pipe(c, [(14.0, 62.0), (14.0, 74.0), (26.0, 78.0)], r=1.5)
    pipe_port(c, 24.0, 100.0, hw=5.0, hh=3.8)
    smart_screen(c, 84.0, 92.0, hw=11.0, hh=5.5, lines=3)
    lamp_row(c, 32.0, 58.0, 3, 1.35, lit=2, sep=5.0)
    hazard_band(c, hull, 56.0, 108.0, 34.0, 2.6)
    edge_bolt_row(c, hull, 60.0, 12, 1.05, x0=18.0, x1=94.0)


# ==========================================================================
# 10 总装 —— 大平板 + 工具墙（胶囊 CSG 拼装 / Worley 皮革细胞）
# ==========================================================================

def dev_assembly(c):
    s, fx, x, y = c.s, c.fx, c.x, c.y
    ntc = notch_sd(x, y, edge=RIGHT, cy=94.0, hh=10.0, depth=12.0)
    bench = sdf.rrect(x, y, 64.0, 78.0, 54.0, 30.0, 4.0)
    wall = sdf.rrect(x, y, 62.0, 30.0, 40.0, 12.0, 2.5)
    hull = sdf.subtract(sdf.union(bench, wall), ntc)
    deck = sdf.subtract(sdf.rrect(x, y, 64.0, 80.0, 48.0, 24.0, 3.0), ntc)

    footwell(c, ntc)
    frame(c, hull, deck, seams=(30.0, 0.0))

    # 工具墙（高，格栅 + 挂钩 + 挂具）
    s.draw(sdf.rrect(x, y, 62.0, 30.0, 38.0, 9.5, 2.0), palette.BODY_MID,
           material=fx.fbm(0.25, 3, so=197),
           depth=L_TOP, top_shade=1.12, side_shade=0.56, edge_hi=0.42)
    s.emboss(greeble.grille(x, y, 62.0, 30.0, 36.0, 6.0, 3.6, 0.30, 90.0),
             hi=0.30, dark=0.28, width=0.9)
    for i in range(6):
        hx = 32.0 + i * 12.0
        s.draw(sdf.intersect(sdf.rrect(x, y, hx, 36.0, 1.5, 4.2, 0.6), hull),
               palette.LIGHT, depth=L_TOP, top_shade=1.18, side_shade=0.6, edge_hi=0.38)

    # 皮革软垫台面（Worley 细胞）
    leather = np.clip(fx.worley(cell=4.5, so=173) * 0.7 + fx.fbm(0.22, 3, so=179) * 0.3, 0, 1)
    s.draw(sdf.intersect(sdf.rrect(x, y, 64.0, 88.0, 40.0, 13.0, 3.0), deck),
           palette.DARK2, material=leather,
           depth=L_LOW, top_shade=1.10, side_shade=0.56, edge_hi=0.34, mat_lo=0.76, mat_hi=1.28)

    # 台钳 2 个（软垫钳口 + 丝杠）
    for i, vx in enumerate((44.0, 64.0)):
        s.draw(sdf.intersect(sdf.rrect(x, y, vx, 74.0, 6.0, 9.0, 1.6), deck),
               palette.BODY_HI, material=fx.brushed(90.0, 0.9, so=181 + i),
               depth=L_HIGH, top_shade=1.18, side_shade=0.6,
               edge_hi=0.44, mat_lo=0.82, mat_hi=1.30)
        s.emboss(greeble.handle(x, y, vx, 84.0, 4.0, 2.0, 0.8), hi=0.32, dark=0.28)
    bright_metal(c, sdf.intersect(sdf.capsule(x, y, 52.0, 86.0, 84.0, 86.0, 2.0), deck),
                 depth=L_HIGH, so=185, shade=1.20)

    # 智能扭矩起子臂（橙色）
    s.draw(sdf.capsule(x, y, 100.0, 52.0, 84.0, 68.0, 3.6), palette.MOTION_ORANGE2,
           material=fx.brushed(45.0, 1.0, so=191),
           depth=L_TOP, top_shade=1.12, side_shade=0.58, edge_hi=0.44)
    s.draw(sdf.circle(x, y, 84.0, 68.0, 3.2), palette.MOTION_ORANGE,
           depth=L_TOP, top_shade=1.16, side_shade=0.62, edge_hi=0.46)

    # 零件摆放盘（黄铜小件散落）
    tray = sdf.intersect(sdf.rrect(x, y, 104.0, 98.0, 14.0, 7.0, 1.6), deck)
    s.draw(tray, palette.DARK, material=fx.pits(cell=4.5, steps=2, so=193),
           depth=L_LOW, top_shade=0.96, side_shade=0.52, edge_hi=0.28)
    # 盘里铺一排黄铜配件——总装台的语义就是"手边一堆五金件"，
    # 同时它是本台暖色像素的主要来源（验收口径 warm_ratio >= 0.09）。
    # ⚠ tray 是 SDF（内部为负），不能直接当掩码相乘——盘外正值会被 paint 的
    # clip(mask,0,1) 变成 1，等于把整张画布刷成黄铜。必须先二值化。
    tray_m = (tray < 0.0).astype(np.float32)
    tray_fit = greeble.bolt_grid(x, y, 94.0, 94.0, 114.0, 102.0, 3, 2, 1.6) * tray_m
    s.paint(tray_fit, palette.RUST2, strength=0.94)
    s.emboss(tray_fit, hi=0.34, dark=0.30, width=0.9)
    # 台钳导轨亮面（本台亮部来源）
    bright_metal(c, sdf.intersect(sdf.rrect(x, y, 64.0, 62.0, 44.0, 2.0, 0.9), deck),
                 depth=L_MID, so=199, shade=1.20)

    copper_pipe(c, [(16.0, 44.0), (16.0, 66.0), (30.0, 72.0)], r=1.6)
    pipe_port(c, 16.0, 100.0, hw=5.0, hh=4.0)
    smart_screen(c, 100.0, 62.0, hw=11.0, hh=5.5, lines=2)
    lamp_row(c, 44.0, 104.0, 3, 1.35, lit=2, sep=5.0)
    hazard_band(c, hull, 64.0, 108.0, 48.0, 2.6)
    edge_bolt_row(c, wall, 24.0, 9, 1.0, x0=30.0, x1=94.0)


# ==========================================================================
# 11 调试校准 —— 台面 + 后立靶板架（同心环 SDF / Mandelbrot 逃逸场）
# ==========================================================================

def dev_calibration(c):
    s, fx, x, y = c.s, c.fx, c.x, c.y
    ntc = notch_sd(x, y, edge=RIGHT, cy=96.0, hh=9.0, depth=11.0)
    bench = sdf.rrect(x, y, 56.0, 80.0, 48.0, 28.0, 4.0)
    tower = sdf.rrect(x, y, 102.0, 42.0, 16.0, 32.0, 3.0)
    rail = sdf.rrect(x, y, 76.0, 46.0, 32.0, 4.0, 1.5)
    hull = sdf.subtract(sdf.union(bench, tower, rail), ntc)
    deck = sdf.subtract(sdf.rrect(x, y, 56.0, 84.0, 42.0, 22.0, 3.0), ntc)

    footwell(c, ntc)
    frame(c, hull, deck, seams=(26.0, 90.0))

    # 靶板架（Mandelbrot 逃逸场做底板纹理）+ 同心环靶
    s.draw(sdf.rrect(x, y, 102.0, 42.0, 13.0, 28.0, 2.5), palette.DARK,
           material=fx.escape(scale=0.022, so=199),
           depth=L_TOP, top_shade=0.94, side_shade=0.52,
           edge_hi=0.34, mat_lo=0.80, mat_hi=1.16)
    tgt = sdf.circle(x, y, 102.0, 40.0, 10.5)
    s.draw(tgt, palette.DARK, depth=L_TOP, top_shade=0.92, side_shade=0.52, edge_hi=0.30)
    rings = fx.rings(102.0, 40.0, 3.6, 211)
    band = np.clip(np.abs(rings - 0.5) * 4.0, 0, 1)
    s.paint((1.0 - band) * (tgt < 0), palette.ENERGY_BLUE, 0.72)
    s.paint(np.clip((1.0 - band) - 0.55, 0, 1) * 3.0 * (tgt < 0), palette.LIGHT, 0.45)
    s.emboss(greeble.bolt_ring(x, y, 102.0, 40.0, 12.0, 8, 0.95), hi=0.32, dark=0.26)

    # 导轨（亮金属）+ 发射器 + 激光线
    bright_metal(c, sdf.intersect(sdf.rrect(x, y, 76.0, 46.0, 32.0, 2.2, 1.0), hull),
                 depth=L_HIGH, so=215, shade=1.20)
    s.emboss(greeble.tslot(x, y, 76.0, 46.0, 32.0, 2.2, 11.0, 0.45, 0.0),
             hi=0.32, dark=0.28, width=0.8)
    s.draw(sdf.intersect(sdf.rrect(x, y, 40.0, 62.0, 8.0, 6.0, 1.6), deck),
           palette.ENERGY_BLUE, material=fx.brushed(0.0, 1.0, so=227),
           depth=L_HIGH, top_shade=1.14, side_shade=0.6, edge_hi=0.44, emissive=0.14)
    s.paint((sdf.capsule(x, y, 47.0, 62.0, 92.0, 44.0, 0.7) < 0).astype(np.float32),
            (0x9A, 0xC8, 0xF0), 0.85)

    # 机匣夹钳
    s.draw(sdf.intersect(sdf.rrect(x, y, 44.0, 84.0, 17.0, 7.5, 1.6), deck),
           palette.BODY_HI, material=fx.worley(cell=4.5, so=223),
           depth=L_MID, top_shade=1.16, side_shade=0.6, edge_hi=0.42)
    for i in range(3):
        s.paint((sdf.intersect(sdf.rrect(x, y, 32.0 + i * 12.0, 84.0, 1.6, 5.5, 0.5),
                               deck) < 0).astype(np.float32), palette.DARK, 0.58)

    # 风速表盘（Mandelbrot 底纹 + 8 向刻度 + 黄铜指针）
    s.draw(sdf.intersect(sdf.circle(x, y, 80.0, 70.0, 6.5), deck),
           palette.SCREEN_BG, material=fx.escape(scale=0.03, so=229),
           depth=L_MID, top_shade=1.0, side_shade=0.6, edge_hi=0.38,
           mat_lo=0.78, mat_hi=1.16)
    s.emboss(greeble.dial(x, y, 80.0, 70.0, 6.5, 12, 1.5), hi=0.32, dark=0.28, width=0.8)
    s.paint((sdf.capsule(x, y, 80.0, 70.0, 83.5, 66.5, 0.6) < 0).astype(np.float32),
            palette.HAZARD, 0.92)

    copper_pipe(c, [(16.0, 60.0), (16.0, 78.0), (28.0, 84.0)], r=1.5)
    # 黄铜导轨支座（本台为能源蓝语义，需靠黄铜件补足暖色配比）
    copper_pipe(c, [(52.0, 100.0), (52.0, 92.0), (62.0, 88.0)], r=1.4)
    for i in range(3):
        s.draw(sdf.intersect(sdf.rrect(x, y, 62.0 + i * 8.0, 104.0, 2.6, 3.0, 0.8), deck),
               palette.RUST2, depth=L_MID, top_shade=1.12, side_shade=0.62, edge_hi=0.34)
    pipe_port(c, 30.0, 106.0, hw=5.0, hh=3.6)
    smart_screen(c, 76.0, 98.0, hw=10.0, hh=5.0, lines=2)
    lamp_row(c, 60.0, 100.0, 3, 1.35, lit=2, sep=5.0)
    hazard_band(c, hull, 52.0, 108.0, 34.0, 2.6)
    edge_bolt_row(c, hull, 56.0, 11, 1.05, x0=16.0, x1=96.0)


# ==========================================================================
# 12 质检计量 —— 台面 + 后立仪表墙（SDF 表盘 / Poisson Disk 测点阵）
# ==========================================================================

def dev_inspection(c):
    s, fx, x, y = c.s, c.fx, c.x, c.y
    ntc = notch_sd(x, y, edge=RIGHT, cy=98.0, hh=9.0, depth=11.0)
    bench = sdf.rrect(x, y, 64.0, 84.0, 54.0, 26.0, 4.0)
    wall = sdf.rrect(x, y, 62.0, 34.0, 38.0, 14.0, 2.5)
    hull = sdf.subtract(sdf.union(bench, wall), ntc)
    deck = sdf.subtract(sdf.rrect(x, y, 64.0, 84.0, 48.0, 21.0, 3.0), ntc)

    footwell(c, ntc)
    frame(c, hull, deck, seams=(28.0, 0.0))

    # 后立仪表墙（钢蓝背光面板 + 表盘）
    s.draw(sdf.rrect(x, y, 62.0, 34.0, 36.0, 11.5, 2.0), palette.BODY_MID,
           material=fx.brushed(0.0, 0.7, so=241),
           depth=L_TOP, top_shade=1.12, side_shade=0.56, edge_hi=0.42)
    panel = sdf.rrect(x, y, 46.0, 32.0, 18.0, 7.5, 1.4)
    s.draw(panel, palette.ENERGY_BLUE, material=fx.fbm(0.12, 3, so=251),
           depth=L_TOP, top_shade=1.10, side_shade=0.6, edge_hi=0.40, emissive=0.16)
    for i in range(2):
        s.paint((sdf.rrect(x, y, 46.0, 28.5 + i * 7.0, 15.0, 1.1, 0.5) < 0)
                .astype(np.float32), palette.ENERGY_BLUE2, 0.6)
    for i, (dx, dy) in enumerate(((84.0, 32.0), (102.0, 32.0))):
        # 黄铜表圈：老式量具的标配，也是本台暖色像素的主要来源
        # （描边收尾会吃掉边缘彩色像素，实测 warm_ratio 0.101 -> 0.082）。
        s.draw(sdf.annulus(x, y, dx, dy, 6.0, 7.4), palette.RUST2,
               depth=L_TOP, top_shade=1.14, side_shade=0.62, edge_hi=0.46)
        s.draw(sdf.circle(x, y, dx, dy, 6.0), palette.SCREEN_BG,
               material=fx.rings(dx, dy, 1.8, 241 + i),
               depth=L_TOP, top_shade=1.0, side_shade=0.6, edge_hi=0.38,
               mat_lo=0.82, mat_hi=1.14)
        s.emboss(greeble.dial(x, y, dx, dy, 6.0, 12, 1.6), hi=0.32, dark=0.28, width=0.8)
        s.paint((sdf.capsule(x, y, dx, dy, dx + 3.4, dy - 2.0, 0.55) < 0)
                .astype(np.float32), palette.HAZARD, 0.9)

    # 平板（surface plate）：亮金属 + Poisson Disk 测点阵
    # shade 1.20→1.10：本台亮部占比实测 0.225，撞到验收上限 0.22。
    # 降一档后亮面落在 96-112 区间（仍在"亮灰"档），而不是堆在 >112 的顶端。
    plate = sdf.intersect(sdf.rrect(x, y, 60.0, 84.0, 34.0, 15.0, 2.5), deck)
    bright_metal(c, plate, depth=L_MID, so=233,
                 mat=fx.worley(cell=3.4, so=233), shade=1.10)
    pts = [(px, py) for px, py in fx.poisson(radius=10.0, so=239)
           if 30 < px < 90 and 72 < py < 97]
    s.paint(fx.dots(pts, radius=1.15, softness=0.5) * (plate < 0), palette.DARK, 0.55)
    s.emboss(greeble.bolt_row(x, y, 30.0, 90.0, 68.0, 7, 1.0), hi=0.28, dark=0.24)

    # 千分表 + 磁力座
    s.draw(sdf.intersect(sdf.rrect(x, y, 106.0, 74.0, 7.0, 10.0, 1.6), deck),
           palette.BODY_HI, material=fx.worley(cell=4.5, so=257),
           depth=L_HIGH, top_shade=1.16, side_shade=0.6, edge_hi=0.42)
    s.draw(sdf.circle(x, y, 106.0, 68.0, 4.6), palette.SCREEN_BG,
           material=fx.rings(106.0, 68.0, 1.6, 259),
           depth=L_HIGH, top_shade=1.02, side_shade=0.6, edge_hi=0.40, mat_lo=0.84, mat_hi=1.16)
    s.emboss(greeble.dial(x, y, 106.0, 68.0, 4.6, 10, 1.3), hi=0.32, dark=0.28, width=0.8)
    s.paint((sdf.capsule(x, y, 106.0, 68.0, 108.0, 65.6, 0.5) < 0).astype(np.float32),
            palette.HAZARD, 0.9)

    # 放大镜臂 + 通止规架（黄铜）
    s.draw(sdf.capsule(x, y, 90.0, 100.0, 82.0, 106.0, 1.7), palette.BODY_MID,
           depth=L_HIGH, top_shade=1.10, side_shade=0.58, edge_hi=0.34)
    s.draw(sdf.annulus(x, y, 80.0, 107.0, 3.0, 4.8), palette.LIGHT,
           depth=L_MID, top_shade=1.18, side_shade=0.6, edge_hi=0.42)
    for i in range(4):
        s.draw(sdf.intersect(sdf.rrect(x, y, 20.0 + i * 6.0, 104.0, 1.8, 5.0, 0.7), deck),
               palette.RUST2, depth=L_MID, top_shade=1.10, side_shade=0.62, edge_hi=0.34)

    copper_pipe(c, [(16.0, 62.0), (16.0, 84.0), (26.0, 90.0)], r=1.5)
    pipe_port(c, 100.0, 104.0, hw=5.0, hh=4.0)
    smart_screen(c, 30.0, 66.0, hw=10.0, hh=5.5, lines=2)
    lamp_row(c, 46.0, 66.0, 3, 1.35, lit=2, sep=5.0)
    hazard_band(c, hull, 64.0, 108.0, 48.0, 2.6)
    edge_bolt_row(c, wall, 20.0, 9, 1.0, x0=30.0, x1=94.0)


# ==========================================================================
# 注册表：12 台设备的算法分配（对齐 01_T1_智能化生产设备.md 的 12 道工序）
# ==========================================================================

DEVICES = [
    dict(name="PC_Smelter_T1", cn="01 冶炼", accent="energy", fn=dev_smelter,
         shape_algo="SDF 平滑并集 (圆炉体+方台)", mat_algo="fBm + 湍流热辉光", seed=1101),
    dict(name="PC_Forge_T1", cn="02 锻造", accent="motion", fn=dev_forge,
         shape_algo="SDF 多边形轮廓 + 平滑并集", mat_algo="各向异性拉丝金属", seed=1202),
    dict(name="PC_HeatTreat_T1", cn="03 热处理", accent="energy", fn=dev_heattreat,
         shape_algo="SDF 圆角腔体（双箱）", mat_algo="元胞自动机凹坑", seed=1303),
    dict(name="PC_RoughMach_T1", cn="04 粗加工", accent="motion", fn=dev_roughmach,
         shape_algo="Voronoi 分格床身", mat_algo="Worley F2-F1 龟裂", seed=1404),
    dict(name="PC_PrecMach_T1", cn="05 精密机加工", accent="motion", fn=dev_precmach,
         shape_algo="门型龙门（SDF 布尔）", mat_algo="Worley 花岗岩斑点", seed=1505),
    dict(name="PC_Barrel_T1", cn="06 枪管", accent="motion", fn=dev_barrel,
         shape_algo="满宽长床身（一维中点位移）", mat_algo="中点位移膛线起伏", seed=1606),
    dict(name="PC_Surface_T1", cn="07 表面处理", accent="bio", fn=dev_surface,
         shape_algo="SDF 三连槽体", mat_algo="Gray-Scott 反应扩散", seed=1707),
    dict(name="PC_Furniture_T1", cn="08 木托护木", accent="motion", fn=dev_furniture,
         shape_algo="SDF 龙门 + 集尘塔", mat_algo="各向异性木纹 + 年轮", seed=1808),
    dict(name="PC_Magazine_T1", cn="09 供弹", accent="motion", fn=dev_magazine,
         shape_algo="L 形（模板阵列拼装）", mat_algo="DLA 枝晶 + 调色板交换", seed=1909),
    dict(name="PC_Assembly_T1", cn="10 总装", accent="motion", fn=dev_assembly,
         shape_algo="胶囊 / CSG 拼装", mat_algo="Worley 皮革细胞", seed=2010),
    dict(name="PC_Calibration_T1", cn="11 调试校准", accent="energy", fn=dev_calibration,
         shape_algo="同心环 SDF + 靶板架", mat_algo="Mandelbrot 逃逸场", seed=2111),
    dict(name="PC_Inspection_T1", cn="12 质检计量", accent="energy", fn=dev_inspection,
         shape_algo="SDF 表盘 + 环（仪表墙）", mat_algo="Poisson Disk 测点阵", seed=2212),
]


def build(spec, w=W, h=H, proj_k=None):
    """按配方生成一张 Sprite。

    proj_k 由 render.PROJ_K 决定（3/4 斜视投影）。传 1.0 可退回纯俯视正交，
    用于 A/B 对比。
    """
    s = render.blank_sprite(
        w, h, seed=spec["seed"],
        proj_k=render.PROJ_K if proj_k is None else proj_k)
    fx = FieldKit(s.x, s.y, seed=spec["seed"])
    accent = ACCENT[spec["accent"]]
    ctx = Ctx(s, fx, accent)
    spec["fn"](ctx)
    return s

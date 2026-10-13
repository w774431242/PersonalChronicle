"""渲染管线：把 SDF 形状 + 材质场 + 光照 + 做旧合成为最终贴图。

对齐《自动化设备_美术风格规范_v1.0》§4 视角与光影：

    - 2D 俯视正交：顶面即俯视轮廓，南侧有一道薄立面（depth 3-8px），不做 3D 拉伸盒
    - 北朝上、光影恒定：顶面最亮，立面中调，开口/槽内最暗，无体积光、无渐变天光
    - 平涂着色（flat shading）：明度靠区域常量 + 边缘倒角，不靠渐变
    - 无地面、无接触投影（RimWorld 会叠加地图光照，自带投影会双重变黑）

⚠ v3 修正的一处**重大错误**：旧版此处写着"不用外描边轮廓，靠明度差区分体块"，
这是错的。官方美术规范（RimWorld Wiki / Modding Tutorials / Textures）原文：

    "RimWorld's art style dictates that interactive objects such as items and
     buildings should have a thick black outline, generally equal to
     2px of thickness per 64px of tile resolution."

即 128px 贴图需要约 4px 粗黑描边（官方用词 "dictates"，是硬性风格要求）。
实测风格基准件 PC_AutoFarm_M 的外缘第 1px 环带中位明度 = 11（几乎纯黑），
第 2px 才回到 40；而 v2 生成件外缘第 1px 是 22-32，**直接就是机体色，没有描边**。
缺描边正是"部件糊成一团、读不出体块"的直接原因之一。见 `Sprite.outline()`。

v2 关键改动（细节密度翻倍的来源）
--------------------------------
1. **方向性倒角光照**：由 SDF 梯度算法线，朝南/东的边缘提亮、朝北/西的边缘压暗。
   替代 v1 的"整圈均匀亮边 + 一圈黑立面"。这一步同时解决三件事：体块分离、
   细节梯度（grad 指标）、以及"一片糊黑"。
2. **明度三档制**：顶面（亮）/ 立面（中）/ 开口（暗）。v1 把立面压到 0.22-0.4，
   导致每个部件都拖一圈黑裙，整机发闷。现按 0.52-0.68 给立面，靠倒角而不是
   靠压黑来分离体块。
3. **挤出深度收敛到 3-8px**：v1 的 4-13px 在 128px 画布上过重。

颜色全部在 0-255 浮点空间运算，导出时转 uint8。
"""

import numpy as np
from PIL import Image

from . import noise, sdf


# 层高等级 -> 向南挤出的像素深度（部件越高，可见立面越高）。
LAYER_PX = {
    0: 3,    # 底盘 / 底壳
    1: 5,    # 台面
    2: 6,    # 贴台面的低矮件
    3: 8,    # 一般机构
    4: 9,    # 立板 / 突出机构
    5: 11,   # 最高件（龙门、机械臂、仪表墙）
}
LAYER_BASE, LAYER_DECK, LAYER_LOW = 0, 1, 2
LAYER_MID, LAYER_HIGH, LAYER_TOP = 3, 4, 5

# 主光源方向（2D 俯视下的"体块法线 → 明度"映射）：正上方偏前 = 偏南，略偏东。
LIGHT_DIR = (0.40, 0.92)

# 倒角配色。这三个常量决定"细节密度"与"对比跨度"两个指标：
# np.gradient 用中心差分，1px 的变化会被折半，所以只有把深色接缝和亮色倒角
# **做成紧邻的一对**，梯度才能越过 25 的阈值。
#
# v3 实测修正（_diag_gutter.py 量化）：
#   - 暗沟占比 基准件 0.0315 / 生成件 0.005-0.013  → 差 2.4-6.0 倍
#   - 亮边占比 基准件 0.0620 / 生成件 0.016-0.039  → 差 1.6-4.0 倍
#   - 对比跨度 p90-p10 基准件 **125** / 生成件 63-102 → 全员"中间调压缩"
# 根因不是纹理密度不够，而是：(a) 暗缝太细太浅；(b) 亮边不够亮——
# 主体基色 BODY_MID=0x30 经 top_shade×mat_hi 后天花板只有 ~72，
# 而基准件的 p90 是 135。所以 BEVEL_HI 必须能把边缘推到 112 以上。
BEVEL_HI = (116.0, 120.0, 132.0)   # 受光倒角 = 金属镜面级高光
SEAM_COL = (8.0, 8.0, 10.0)        # 紧邻暗缝，比 DARK 还深一档
SEAM_HI = (74.0, 76.0, 84.0)       # 表面板缝的亮侧（比倒角弱一档，避免糊亮）

# ---------------------------------------------------------------------------
# 视角：3/4 斜视（"俯视 2.5D"）的投影系数
# ---------------------------------------------------------------------------
# 为什么不是纯俯视正交
# --------------------
# 项目规范 §4 字面写"2D 俯视正交"，但项目**自有的全部既有建筑贴图都是 3/4 斜视**：
#   PC_Conveyor_Mid/Start_east  —— 看得到台面 + 南侧立面 + 四角立柱
#   PC_FarmFrame                —— 四根立柱的内侧可见
#   PC_AutoFarm_M（风格基准件） —— 完整的 3/4 视图，128x128，与本级贴图同尺寸
# 纯俯视丢掉整个立面，信息量天生比斜视少约 40%——这正是"细节堆不上去"的
# 结构性根因，靠加纹理密度是补不回来的。
#
# 实现方式（关键）
# ----------------
# **在坐标网格上做投影，而不是在场或像素缓冲上做。** 即在 Sprite 里把采样用的
# y 轴按 k 压缩：输出第 y 行去采样世界坐标 cy + (y - cy) / k。
# 这样设备配方里的每一个 SDF（包括 s.paint(mask) / s.emboss(sd) 用的那些掩码）
# 都自动落在同一投影下，12 台配方一行都不用改。
#
# k = 1.0 保持原正交行为（可用于 A/B 对比）；0.72-0.78 是 3/4 斜视的常见取值。
PROJ_K = 0.88
PROJ_LAYER_BOOST = 1.85   # 斜视下立面更可见，挤出深度同步放大


def smooth_inner_edge(sd, width):
    """由边界向内的平滑衰减环带（0-1），用于高光 / 做旧 / 磨损。"""
    t = np.clip(1.0 - (-sd) / max(width, 1e-3), 0.0, 1.0)
    return (t * t * (sd < 0.0)).astype(np.float32)


def smooth_outer_edge(sd, width):
    """由边界向外的平滑衰减环带（0-1），用于柔和外溢（光晕 / 液面反光）。"""
    t = np.clip(1.0 - sd / max(width, 1e-3), 0.0, 1.0)
    return (t * t * (sd >= 0.0)).astype(np.float32)


def normals(sd):
    """由 SDF 梯度求外法线（nx, ny）。SDF 越平滑，法线越准。"""
    gy, gx = np.gradient(sd.astype(np.float32))
    n = np.hypot(gx, gy) + 1e-6
    return gx / n, gy / n


def facing(sd, light=LIGHT_DIR):
    """外法线与主光的点积（-1..1）。> 0 朝南/东（受光），< 0 朝北/西（背光）。"""
    nx, ny = normals(sd)
    return nx * light[0] + ny * light[1]


def panel_seams(sd, pitch=15.0, angle=90.0, width=0.9, offset=0.0):
    """在部件内部按间距切出分缝线（返回掩码）。最高效的"机械感"来源。"""
    a = np.deg2rad(angle)
    ca, sa = np.cos(a), np.sin(a)
    u = np.arange(sd.shape[1], dtype=np.float32)[None, :] * ca + \
        np.arange(sd.shape[0], dtype=np.float32)[:, None] * sa
    phase = np.mod(u - offset, max(pitch, 1e-3))
    line = (np.minimum(phase, pitch - phase) <= width).astype(np.float32)
    return line * (sd < -1.2).astype(np.float32)


class Sprite:
    """一张设备贴图的绘制缓冲（0-255 浮点 RGB + 0-1 alpha）。"""

    def __init__(self, w=128, h=128, seed=0, proj_k=1.0, proj_cy=None):
        self.w = w
        self.h = h
        self.seed = int(seed)
        self.rgb = np.zeros((h, w, 3), dtype=np.float32)
        self.alpha = np.zeros((h, w), dtype=np.float32)
        # ---- 3/4 斜视投影：压缩采样网格的 y 轴 ----
        # 输出第 y 行采样世界坐标 cy + (y-cy)/k。k<1 即"顶面被压扁"，
        # 相当于相机从南侧上方俯看。所有 SDF 都由这套 (x, y) 构造，
        # 因此整台设备（含 paint/emboss 掩码）自动落在同一投影下。
        self.proj_k = float(proj_k)
        ys, xs = np.mgrid[0:h, 0:w]
        cy = (h * 0.5) if proj_cy is None else float(proj_cy)
        self.proj_cy = cy
        self.x = xs.astype(np.float32)
        if abs(self.proj_k - 1.0) < 1e-4:
            self.y = ys.astype(np.float32)
            self.layer_boost = 1.0
        else:
            self.y = (cy + (ys.astype(np.float32) - cy) / self.proj_k)
            self.layer_boost = PROJ_LAYER_BOOST
        self._fields = {}
        self._part_i = 0

    # ------------------------------------------------------------------
    # 表面场缓存（所有部件共用，同一 seed 只算一次）
    # ------------------------------------------------------------------

    def _plate_seams(self, so):
        """非周期的钣金拼板缝（0/1 掩码）。

        ⚠ 这是 v3 最重要的一次返工。第二版用**等距轴对齐网格**铺满整个表面，
        量化指标确实达标了（暗沟占比 0.031 ≈ 基准件的 0.0315），但 6 倍放大后
        读感完全错：等距网格 = **瓷砖墙 / 铁皮网**，1 倍下更糊成"雪花噪点"。

        根因是**周期性**。基准件 PC_AutoFarm_M 的暗沟来自**零件轮廓**——
        车轮与车身、水箱与框架之间真实的装配缝；那是结构性的、非周期的。
        而等距网格是纹理性的、周期性的，人的视觉系统第一眼就把它识别成"图案"
        而不是"结构"。

        因此这里改成：缝的位置随机、每条缝只覆盖**随机的一段长度**、
        横竖缝交织但不闭合。这正是真实钣金机箱的样子——大小不等的板拼焊，
        焊缝断断续续，永远不出现重复的格。
        """
        key = ("plates", int(so))
        f = self._fields.get(key)
        if f is not None:
            return f
        h, w = self.h, self.w
        rnd = np.random.default_rng(self.seed * 104729 + int(so))
        seam = np.zeros((h, w), np.float32)
        # 竖缝：随机起点，反向随机步长，每条只覆盖一段 y
        x = float(rnd.uniform(4.0, 15.0))
        while x < w - 2.0:
            xi = int(x)
            y0 = float(rnd.uniform(-6.0, h * 0.55))
            y1 = y0 + float(rnd.uniform(h * 0.45, h * 1.30))
            i0, i1 = int(max(0.0, y0)), int(min(float(h), y1))
            if i1 > i0:
                seam[i0:i1, xi] = 1.0
            x += float(rnd.uniform(7.5, 19.0))
        # 横缝：同法
        y = float(rnd.uniform(4.0, 15.0))
        while y < h - 2.0:
            yi = int(y)
            x0 = float(rnd.uniform(-6.0, w * 0.55))
            x1 = x0 + float(rnd.uniform(w * 0.45, w * 1.30))
            j0, j1 = int(max(0.0, x0)), int(min(float(w), x1))
            if j1 > j0:
                seam[yi, j0:j1] = 1.0
            y += float(rnd.uniform(7.5, 19.0))
        self._fields[key] = seam
        return seam

    def _patch(self, cell, so):
        """Worley 拼板场，返回 (F1 归一化, 板缝归一化, 板缝像素单位)。

        `worley` 与 `worley_f2f1` 共享同一组特征点，所以"板面"与"板缝"天然对齐。
        像素单位那份用于 emboss —— 归一化场每像素只变化约 1/cell，
        直接拿去算倒角会被摊成 7-8px 宽的软边（细节梯度全被抹平）。
        """
        key = ("patch", round(float(cell), 2), int(so))
        f = self._fields.get(key)
        if f is None:
            f1 = noise.worley(self.x, self.y, cell=cell, seed=self.seed + so)
            cr = noise.worley_f2f1(self.x, self.y, cell=cell, seed=self.seed + so)
            f = (f1, cr, cr * float(cell))
            self._fields[key] = f
        return f

    def _micro(self, so):
        """逐像素的高频颗粒场（金属不是均匀色块）。"""
        key = ("micro", int(so))
        f = self._fields.get(key)
        if f is None:
            f = noise.value_noise(self.x * 0.85, self.y * 0.85, seed=self.seed + so)
            self._fields[key] = f
        return f

    def _grid_seam(self, pitch, width, ox, oy, dash=None):
        """轴对齐的矩形拼板缝（返回 0/1 掩码），可选断续。

        ⚠ 金属板是**矩形**的。v2 首版用 Worley 元胞做表面拼板，结果是每台设备
        都像"碎石砌的墙"——Voronoi 是无规则多边形的语言，那是石头不是钢板。

        但纯均匀网格又会读成"铁皮网/瓦楞板"（第二版实测）。真实钣金机箱的样子是：
        板距不等、板缝断续、大小两层叠在一起。所以这里支持 dash 断线与双层调用。
        """
        key = ("gseam", round(pitch, 2), round(width, 2), round(ox, 2),
               round(oy, 2), dash)
        f = self._fields.get(key)
        if f is None:
            px = np.mod(self.x - ox, pitch)
            py = np.mod(self.y - oy, pitch)
            lx = np.minimum(px, pitch - px) <= width
            ly = np.minimum(py, pitch - py) <= width
            if dash is not None:
                dp, dd = dash
                lx = lx & (np.mod(self.y + ox * 1.7, dp) < dp * dd)
                ly = ly & (np.mod(self.x + oy * 1.3, dp) < dp * dd)
            f = (lx | ly).astype(np.float32)
            self._fields[key] = f
        return f

    # ------------------------------------------------------------------
    # 底层合成
    # ------------------------------------------------------------------

    def _composite(self, rgb_new, cov):
        a = np.clip(cov, 0.0, 1.0)
        a3 = a[:, :, None]
        self.rgb = self.rgb * (1.0 - a3) + rgb_new * a3
        self.alpha = np.clip(self.alpha + a * (1.0 - self.alpha), 0.0, 1.0)

    # ------------------------------------------------------------------
    # 主绘制：一个带挤出立面与倒角的实体部件
    # ------------------------------------------------------------------

    def draw(self, sd, color, material=None, depth=3,
             top_shade=1.0, side_shade=0.60,
             edge_hi=0.46, edge_hi_w=1.0, seam=1.0,
             ao=0.0, ao_w=1.6,
             mat_lo=0.62, mat_hi=1.34,
             rust=0.0, rust_field=None, rust_w=3.5,
             emissive=0.0, coverage=1.0,
             surface=0.70, surface_so=0,
             light=LIGHT_DIR, bevel=True):
        """绘制一个部件。

        sd         : SDF 距离场，< 0 为实体内部
        color      : 基色 (r, g, b)，0-255
        material   : (H, W) 的 0-1 材质强度场，None 则纯平涂
        depth      : 层高等级 0-5（见 LAYER_PX），不是像素值
        top_shade  : 顶面明度系数（亮档，一般 1.02-1.20）
        side_shade : 南侧立面明度系数（中档，一般 0.52-0.68）
        edge_hi    : 受光倒角强度
        edge_hi_w  : 倒角宽度（像素，T1 取 1.0-1.6）
        ao         : 背光倒角的压暗强度
        rust       : 锈蚀强度，需配合 rust_field（0-1 噪声场）
        emissive   : 自发光程度（屏幕/指示灯），>0 时向亮色抬升
        bevel      : 是否启用方向性倒角（关闭则退回 v1 的均匀亮边）
        """
        depth = LAYER_PX[int(np.clip(depth, 0, 5))]
        depth = int(round(depth * self.layer_boost))   # 斜视下立面更可见
        base = np.asarray(color, dtype=np.float32)

        if material is not None:
            m = np.clip(material, 0.0, 1.0)[:, :, None]
            colmap = base[None, None, :] * (mat_lo + (mat_hi - mat_lo) * m)
        else:
            colmap = np.broadcast_to(base, (self.h, self.w, 3)).astype(np.float32).copy()

        if emissive > 0.0:
            colmap = colmap + (255.0 - colmap) * emissive

        # ---- 2.5D 挤出：顶面 + 南侧立面 ----
        sd_low = sdf.shift_down(sd, depth)
        top_a = np.clip(0.5 - sd, 0.0, 1.0)
        side_a = np.clip(np.clip(0.5 - sd_low, 0.0, 1.0) - top_a, 0.0, 1.0)
        body_a = np.maximum(top_a, side_a)
        top_bin = (sd < 0.0).astype(np.float32)

        wsum = top_a + side_a + 1e-9
        shade = (top_a * top_shade + side_a * side_shade) / wsum
        colmap = colmap * shade[:, :, None]

        # ---- 表面拼板 + 微观颗粒（每个部件自动获得，无需逐个手工加）----
        # 非周期拼板缝（见 _plate_seams 的说明）+ 弱拉丝 + 板面明度差。
        # 缝的密度是靠"随机长度、随机间距"控制的，不靠缩短间距——
        # 间距一旦缩短到 6px 以内就会退回"瓷砖/雪花"的读感。
        if surface > 0.0:
            plate = self._plate_seams(surface_so)
            k_seam = 0.78 * surface
            colmap = colmap * (1.0 - (plate * k_seam)[:, :, None]) + \
                np.array(SEAM_COL, np.float32)[None, None, :] * (plate * k_seam)[:, :, None]
            # 亮线贴在缝的北/西侧 1px：与暗缝紧邻成对，中心差分才吃得到
            lit = np.maximum(np.roll(plate, -1, 0), np.roll(plate, -1, 1))
            lit = np.clip(lit - plate, 0.0, 1.0)
            colmap = colmap + (lit * 0.44 * surface)[:, :, None] * \
                np.array(SEAM_HI, np.float32)
            # 板面明度差（不同批次钢板）+ 各向异性拉丝（金属材质的高频语汇）
            key = ("streak", int(surface_so))
            st = self._fields.get(key)
            if st is None:
                st = noise.value_noise(self.x * 3.6, self.y * 0.30,
                                       seed=self.seed + surface_so + 611)
                self._fields[key] = st
            colmap = colmap * (1.0 + (0.52 - self._patch(9.0, surface_so)[0]) *
                               0.10 * surface)[:, :, None]
            colmap = colmap * (1.0 + (st - 0.5) * 0.085 * surface)[:, :, None]
            # 微观颗粒（很弱，只为破掉纯色块）
            colmap = colmap * (1.0 + (self._micro(surface_so) - 0.5) *
                               0.16 * surface)[:, :, None]

        # ---- 方向性倒角：受光侧提亮 + 1px 接缝压暗 ----
        if bevel:
            fc = facing(sd, light)
            lit = np.clip(fc, 0.0, 1.0)
            dark = np.clip(-fc, 0.0, 1.0)
            # A 受光倒角（亮）：朝南/东的边缘
            if edge_hi > 0.0:
                ring = smooth_inner_edge(sd, edge_hi_w) * top_bin
                colmap = colmap + (ring * lit * edge_hi)[:, :, None] * \
                    np.array(BEVEL_HI, np.float32)
            # B 部件边界的接缝（暗）：越背光越重，与亮边紧邻形成高梯度。
            # v3 加宽 0.95→1.35px、底值 0.38→0.50：基准件最醒目的风格特征就是
            # **每个部件都有一圈明确的暗轮廓**（砧座、车轮、水箱都靠它从机身上
            # "切"出来）。只靠 1px 的浅缝，部件会糊成一整块灰。
            if seam > 0.0:
                s1 = smooth_inner_edge(sd, 1.35) * top_bin
                k = (s1 * (0.50 + 0.46 * dark) * seam)[:, :, None]
                colmap = colmap * (1.0 - k) + \
                    np.array(SEAM_COL, np.float32)[None, None, :] * k
            # C 背光侧的柔和压暗（体块感，不发黑）
            if ao > 0.0:
                wide = smooth_inner_edge(sd, ao_w) * top_bin
                colmap = colmap * (1.0 - (wide * dark * ao * 0.5)[:, :, None])
        else:
            if edge_hi > 0.0:
                eh = smooth_inner_edge(sd, edge_hi_w) * top_bin
                colmap = colmap + eh[:, :, None] * edge_hi * \
                    np.array([46.0, 48.0, 54.0], np.float32)
            if ao > 0.0:
                inner = smooth_inner_edge(sd, 2.2)
                colmap = colmap * (1.0 - inner[:, :, None] * ao * top_a[:, :, None])

        # ---- 做旧：锈蚀只出现在边角 ----
        if rust > 0.0:
            rf = rust_field if rust_field is not None else np.ones_like(sd, dtype=np.float32)
            zone = smooth_inner_edge(sd, rust_w)
            mask = np.clip((rf - 0.42) * 3.2, 0.0, 1.0) * zone * rust
            rust_col = np.array([0x54, 0x30, 0x00], np.float32)
            rust_hi = np.array([0x84, 0x48, 0x0C], np.float32)
            tt = np.clip((rf - 0.70) * 4.0, 0.0, 1.0)[:, :, None]
            target = rust_col[None, None, :] * (1 - tt) + rust_hi[None, None, :] * tt
            mask3 = mask[:, :, None]
            colmap = colmap * (1 - mask3) + target * mask3

        self._composite(colmap, body_a * coverage)
        return self

    # ------------------------------------------------------------------
    # 叠加层：不改 alpha，只在已有区域内着色
    # ------------------------------------------------------------------

    def paint(self, mask, color, strength=1.0, respect_alpha=True):
        """在已有像素上叠加颜色（用于屏幕内容、警示条纹、指示灯）。"""
        m = np.clip(mask * strength, 0.0, 1.0)
        if respect_alpha:
            m = m * np.clip(self.alpha, 0.0, 1.0)
        m3 = m[:, :, None]
        col = np.asarray(color, dtype=np.float32)[None, None, :]
        self.rgb = self.rgb * (1.0 - m3) + col * m3
        return self

    def emboss(self, sd, hi=0.46, dark=0.44, width=0.95, light=LIGHT_DIR):
        """给"只涂了色、没走 draw"的平面图案补方向性浮雕。

        对 greeble 这类以小零件为主的元素，比逐个 draw 便宜得多，
        又能拿到同样的方向性明暗（细节梯度主要靠它）。
        亮边与暗缝必须紧邻——这是梯度指标的来源，别把它们拉开。
        """
        fc = facing(sd, light)
        lit = np.clip(fc, 0.0, 1.0)
        dk = np.clip(-fc, 0.0, 1.0)
        a = np.clip(self.alpha, 0.0, 1.0)
        ring = smooth_inner_edge(sd, width) * (sd < 0.0)
        self.rgb = self.rgb + (ring * lit * hi * a)[:, :, None] * \
            np.array(BEVEL_HI, np.float32)
        s1 = smooth_inner_edge(sd, 0.85) * (sd < 0.0)
        k = (s1 * (0.34 + 0.50 * dk) * a)[:, :, None]
        self.rgb = self.rgb * (1.0 - k) + np.array(SEAM_COL, np.float32)[None, None, :] * k
        return self

    def tint(self, mask, color, strength=0.5):
        """按比例把已有颜色推向目标色（比 paint 更柔和）。"""
        return self.paint(mask, color, strength)

    def modulate(self, mask, k):
        """在指定区域整体缩放明度，k 可为标量或 (H, W) 数组。"""
        m = np.clip(mask, 0.0, 1.0)
        k = np.asarray(k, dtype=np.float32)
        factor = 1.0 + (k - 1.0) * m
        self.rgb = np.clip(self.rgb * factor[:, :, None], 0.0, 255.0)
        return self

    def emissive_add(self, mask, color, strength=1.0):
        """加色发光（指示灯核心、屏幕高亮），受已有 alpha 约束。"""
        m = np.clip(mask * strength, 0.0, 1.0) * np.clip(self.alpha, 0.0, 1.0)
        col = np.asarray(color, dtype=np.float32)[None, None, :]
        self.rgb = np.clip(self.rgb + col * m[:, :, None], 0.0, 255.0)
        return self

    # ------------------------------------------------------------------
    # 收尾
    # ------------------------------------------------------------------

    def flatten_luminance(self, pivot=0.1725, strength=0.5, gamma=1.0):
        """明度校准：把整体往规范要求的暗调压。

        规范 §4：画面 70% 像素明度 < #505050（80/255=0.314）。
        基准件 PC_AutoFarm_M 实测中位亮度 44/255 = 0.1725，本函数以它为锚。
        gamma < 1 抬中间调、> 1 压中间调。
        """
        op = self.alpha > 0.5
        if not op.any():
            return self
        lum = 0.299 * self.rgb[:, :, 0] + 0.587 * self.rgb[:, :, 1] + 0.114 * self.rgb[:, :, 2]
        cur = float(np.median(lum[op])) / 255.0
        if cur <= 1e-6:
            return self
        target_k = 1.0 + (pivot / cur - 1.0) * strength
        self.rgb = np.clip(self.rgb * target_k, 0.0, 255.0)
        if abs(gamma - 1.0) > 1e-3:
            self.rgb = 255.0 * np.power(np.clip(self.rgb / 255.0, 0, 1), gamma)
        return self

    def contrast_stretch(self, pivot_frac=0.1725, amount=0.60, hi_clip=1.0):
        """锚定中位数的对比拉伸（打开"对比跨度"，不动中位明度）。

        诊断依据（_diag_gutter.py 实测 p10/p90）：
            基准件 PC_AutoFarm_M   p10=10  p90=135  跨度 **125**
            生成件                 p10=9-26 p90=89-121 跨度 63-102
        即生成件的问题是**中间调压缩**——亮部上不去。若直接整体提亮，中位明度
        会冲出规范区间（32-58）；所以这里用一条以中位数为不动点的二次曲线：

            y = x · (1 + amount · (x - pivot))

        低于 pivot 的像素被轻微压暗，高于 pivot 的被抬起，pivot 本身不变。
        amount=0.60 时：中位不变，x=0.5 → +20%，x≈0.85 → +53%。
        """
        op = self.alpha > 0.5
        if not op.any():
            return self
        lum = (0.299 * self.rgb[:, :, 0] + 0.587 * self.rgb[:, :, 1] +
               0.114 * self.rgb[:, :, 2])
        med = float(np.median(lum[op])) / 255.0
        pv = float(np.clip(med if med > 1e-3 else pivot_frac, 0.02, 0.60))
        x = np.clip(self.rgb / 255.0, 0.0, 1.0)
        lx = 0.299 * x[:, :, 0] + 0.587 * x[:, :, 1] + 0.114 * x[:, :, 2]
        k = 1.0 + amount * (lx - pv)
        self.rgb = np.clip(x * k[:, :, None] * 255.0, 0.0, 255.0 * hi_clip)
        return self

    def outline(self, px=2, color=(10.0, 10.0, 12.0)):
        """RimWorld 交互物件必需的粗黑描边（画在轮廓内侧，不越出 alpha 范围）。

        依据（官方美术规范，非本项目的自定偏好）：
            RimWorld Wiki / Modding Tutorials / Textures —
            "interactive objects such as items and buildings should have a thick
             black outline, generally equal to 2px of thickness per 64px of tile
             resolution."

        128px 贴图对应 ~4px。但项目自有的风格基准件 PC_AutoFarm_M 实测只用了
        1-2px（外缘第 1px 环带明度 11，第 2px 已回到 40），故取 2px：
        既满足官方的"粗黑描边"要求，也对齐本项目基准件。

        ⚠ 必须在 flatten / contrast 等明度操作**之后**执行，否则描边会被
        一起缩放，不再是最深的黑。
        """
        a = self.alpha > 0.5
        inner = a.copy()
        for _ in range(int(px)):
            e = inner.copy()
            e[1:, :] &= inner[:-1, :]
            e[:-1, :] &= inner[1:, :]
            e[:, 1:] &= inner[:, :-1]
            e[:, :-1] &= inner[:, 1:]
            inner = e
        ring = a & ~inner
        self.rgb[ring] = np.asarray(color, np.float32)
        return self

    def pixel_snap(self, hi=0.62, lo=0.38):
        """把 alpha 推向 0 / 1，消掉 SDF 抗锯齿留下的半透明拖尾。

        RimWorld 用点采样渲染，留下 1px 的 30% 透明边缘会在游戏里显脏。
        阈值放宽避免斜边出现硬锯齿：只清掉真正孤立的低 alpha 残留。
        """
        a = self.alpha
        a = np.where(a >= hi, 1.0, np.where(a <= lo, 0.0, a))
        self.alpha = a
        return self

    def stats(self):
        """返回验收用统计（与基准件同口径）。"""
        op = self.alpha > 0.5
        n = int(op.sum())
        if n == 0:
            return {"opaque": 0}
        lum = (0.299 * self.rgb[:, :, 0] + 0.587 * self.rgb[:, :, 1] +
               0.114 * self.rgb[:, :, 2])
        L = lum[op]
        ys, xs = np.nonzero(op)
        gy, gx = np.gradient(lum)
        gm = np.hypot(gx, gy)
        r, g, b = self.rgb[:, :, 0][op], self.rgb[:, :, 1][op], self.rgb[:, :, 2][op]
        mx = np.maximum(np.maximum(r, g), b)
        mn = np.minimum(np.minimum(r, g), b)
        sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1.0), 0.0)
        colorful = sat > 0.30
        warm = colorful & (r >= g) & (g > b)
        return {
            "opaque": n,
            "opaque_ratio": round(n / (self.w * self.h), 3),
            "median_lum": round(float(np.median(L)), 1),
            "mean_lum": round(float(L.mean()), 1),
            "p70_under_0x50": round(float((L < 0x50).mean()), 3),
            "bright_ratio": round(float((L > 112).mean()), 3),
            "detail_ratio": round(float((gm > 25).mean()), 3),
            "detail_ratio_12": round(float((gm > 12).mean()), 3),
            "warm_ratio": round(float(warm.mean()), 3),
            "color_ratio": round(float(colorful.mean()), 3),
            "semi_alpha_px": int(((self.alpha > 0.02) & (self.alpha < 0.98)).sum()),
            "stray_alpha_px": int(((self.alpha > 0.02) & (self.alpha < 0.35)).sum()),
            "bbox": (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1),
        }

    def to_image(self):
        """输出 RGBA 图像；透明区 RGB 强制归零，避免黑晕白边。"""
        rgb = np.clip(self.rgb, 0.0, 255.0).astype(np.uint8)
        a = np.clip(self.alpha * 255.0, 0.0, 255.0).astype(np.uint8)
        out = np.dstack([rgb, a])
        out[a == 0] = 0
        return Image.fromarray(out, "RGBA")

    def save(self, path):
        self.to_image().save(path, "PNG", optimize=True)
        return path


def clean_alpha(img, threshold=8):
    """把 alpha 低于阈值的像素彻底清零（导出前的最后一道打扫）。"""
    arr = np.array(img)
    arr[arr[:, :, 3] < threshold] = 0
    return Image.fromarray(arr, "RGBA")


def blank_sprite(w=128, h=128, seed=0, proj_k=PROJ_K):
    """新建绘制缓冲。proj_k<1 启用 3/4 斜视投影（见 PROJ_K 的说明）。"""
    return Sprite(w, h, seed, proj_k=proj_k)

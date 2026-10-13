"""FieldKit —— 算法场的统一入口。

把八大族的算法包装成"给设备配方直接取用"的材质场（0-1 的 (H, W) 数组），
内部带缓存，同一参数只算一次。

这样设备配方只需声明"用哪个算法、什么参数"，不需要关心算法实现。
"""

import numpy as np

from . import noise, fractal, cellular, geometry


def _resize(field, w, h):
    """最近邻上采样（低分辨率算法场 -> 画布分辨率）。"""
    fh, fw = field.shape
    yi = np.clip((np.arange(h) * fh / h).astype(np.int32), 0, fh - 1)
    xi = np.clip((np.arange(w) * fw / w).astype(np.int32), 0, fw - 1)
    return field[yi][:, xi].astype(np.float32)


class FieldKit:
    """一台设备的算法场工厂。seed 决定该设备全部纹理的形态。"""

    def __init__(self, x, y, seed=0):
        self.x = np.asarray(x, dtype=np.float32)
        self.y = np.asarray(y, dtype=np.float32)
        self.h, self.w = self.x.shape
        self.seed = int(seed)
        self._cache = {}

    def _get(self, key, build):
        if key not in self._cache:
            self._cache[key] = build()
        return self._cache[key]

    # ==================================================================
    # 噪声族
    # ==================================================================

    def fbm(self, freq=0.05, oct=4, so=0, gain=0.5):
        """分形布朗运动：自然表面质感（金属磨砂、土、石）。"""
        return self._get(("fbm", freq, oct, so, gain),
                         lambda: noise.fbm(self.x, self.y, octaves=oct,
                                           base_freq=freq, gain=gain,
                                           seed=self.seed + so))

    def turbulence(self, freq=0.05, oct=4, so=0):
        """湍流：云、火焰、热辉光的锐利褶皱。"""
        return self._get(("turb", freq, oct, so),
                         lambda: noise.turbulence(self.x, self.y, octaves=oct,
                                                  base_freq=freq, seed=self.seed + so))

    def ridged(self, freq=0.06, oct=4, so=0):
        """山脊噪声：金属折痕、铸件冷隔、石材裂纹。"""
        return self._get(("ridge", freq, oct, so),
                         lambda: noise.ridged(self.x, self.y, octaves=oct,
                                              base_freq=freq, seed=self.seed + so))

    def worley(self, cell=10.0, so=0):
        """Worley F1：铸件颗粒、喷砂面。"""
        return self._get(("wor", cell, so),
                         lambda: noise.worley(self.x, self.y, cell=cell,
                                              seed=self.seed + so))

    def worley_crack(self, cell=12.0, so=0):
        """Worley F2-F1：龟裂 / 拼缝 / 边界线。"""
        return self._get(("worc", cell, so),
                         lambda: noise.worley_f2f1(self.x, self.y, cell=cell,
                                                   seed=self.seed + so))

    def brushed(self, angle=0.0, freq=1.0, so=0):
        """各向异性拉丝金属（台面主材质）。"""
        return self._get(("brush", angle, freq, so),
                         lambda: noise.anisotropic_brushed(
                             self.x, self.y, angle_deg=angle, freq=freq,
                             seed=self.seed + so))

    def wood(self, scale=16.0, so=0):
        """各向异性木纹（含年轮）。"""
        return self._get(("wood", scale, so),
                         lambda: noise.anisotropic_wood(
                             self.x, self.y, scale=scale, seed=self.seed + so))

    # ==================================================================
    # 分形族
    # ==================================================================

    def midline(self, n=None, roughness=0.55, so=0, span=1.0):
        """一维中点位移序列，用于长条构件的轮廓起伏（膛线 / 导轨）。"""
        n = n or self.w
        return self._get(("mid1", n, roughness, so, span, ),
                         lambda: fractal.midpoint_displacement_1d(
                             n, roughness=roughness, seed=self.seed + so, span=span))

    def terrain(self, cells=64, roughness=0.6, so=0):
        """Diamond-Square 二维地形高度场。"""
        return self._get(("mid2", cells, roughness, so),
                         lambda: _resize(fractal.midpoint_displacement_2d(
                             cells, cells, roughness=roughness, seed=self.seed + so),
                             self.w, self.h))

    def dla(self, cells=56, n=520, so=0):
        """DLA 枝晶场：锈蚀蔓延、爬电痕迹、裂纹网。"""
        return self._get(("dla", cells, n, so),
                         lambda: _resize(fractal.dla(n_points=n, w=cells, h=cells,
                                                     seed=self.seed + so),
                                         self.w, self.h))

    def rings(self, cx, cy, period=6.0, so=0):
        """同心环场：靶板 / 表盘刻度。"""
        return self._get(("ring", cx, cy, period, so),
                         lambda: fractal.concentric_rings(
                             self.x, self.y, cx, cy, period=period,
                             phase=(self.seed + so) % 7))

    def escape(self, scale=0.004, so=0):
        """Mandelbrot 逃逸时间场：铸造流痕、热处理色带。"""
        return self._get(("esc", scale, so),
                         lambda: fractal.mandelbrot_escape(
                             self.x, self.y, scale=scale, max_iter=20))

    # ==================================================================
    # 元胞族
    # ==================================================================

    def pits(self, cell=8.0, steps=3, so=0, smooth=1.0):
        """铸件凹坑 / 锈斑（元胞自动机平滑插值）。"""
        return self._get(("pit", cell, steps, so, smooth),
                         lambda: cellular.pits(self.x, self.y, cell=cell,
                                               steps=steps, seed=self.seed + so,
                                               smooth=smooth))

    def reaction(self, cells=64, steps=200, so=0, f=0.0367, k=0.0649):
        """Gray-Scott 反应扩散：液面斑纹、涂料流痕。"""
        return self._get(("rd", cells, steps, so, f, k),
                         lambda: _resize(cellular.reaction_diffusion(
                             cells, cells, steps=steps, seed=self.seed + so,
                             f=f, k=k), self.w, self.h))

    # ==================================================================
    # 计算几何族
    # ==================================================================

    def vor_edges(self, pts, width=0.9, so=0):
        """Voronoi 元胞边界（龟裂 / 分格）。"""
        key = ("vore", tuple((round(px, 1), round(py, 1)) for px, py in pts), width)
        return self._get(key, lambda: geometry.voronoi_edges(self.x, self.y, pts, width))

    def vor_cells(self, pts):
        """Voronoi 归属编号场。"""
        key = ("vorc", tuple((round(px, 1), round(py, 1)) for px, py in pts))
        return self._get(key, lambda: geometry.voronoi_field(self.x, self.y, pts)[0])

    def poisson(self, radius=9.0, so=0, inset=0):
        """Poisson Disk 自然散点，返回画布坐标（指示灯位 / 测点位 / 零件散落）。"""
        def _build():
            pts = geometry.poisson_disk(self.w - inset * 2, self.h - inset * 2,
                                        radius, seed=self.seed + so)
            return [(px + inset, py + inset) for px, py in pts]
        return self._get(("poi", radius, so, inset), _build)

    def dots(self, points, radius=1.5, softness=0.7):
        """把散点光栅化成强度场。"""
        return geometry.scatter_dots(self.x, self.y, points, radius, softness)

    def grid_rects(self, x0, y0, cols, rows, cw, ch, gap, jitter=0.0, so=0):
        """规则阵列（模板拼装族）：弹匣、零件盒、散热片。"""
        key = ("gr", x0, y0, cols, rows, cw, ch, gap, jitter, so)
        return self._get(key, lambda: geometry.grid_rects(
            self.x, self.y, x0, y0, cols, rows, cw, ch, gap,
            jitter=jitter, seed=self.seed + so))

    def polyline_dist(self, pts):
        """到折线的最短距离场（导轨 / 管路 / 切割槽）。"""
        key = ("pld", tuple(pts))
        return self._get(key, lambda: geometry.distance_to_polyline(self.x, self.y, pts))

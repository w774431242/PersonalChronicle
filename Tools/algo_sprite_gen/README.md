# algo_sprite_gen · 程序化设备贴图生成器

零 AI 的设备贴图生产线：不使用任何扩散模型 / 神经网络，
**全部像素由 SDF 几何、噪声、分形、元胞自动机、计算几何等确定性算法算出**。
同一 seed 永远得到同一张图，改一个参数即可整体重构。

对齐文档：

- `docs/设计文档/功能模块/资产规范/00-风格与提示词规范/自动化设备_美术风格规范_v1.0.md`（色板 / 视角 / 密度 / 技术红线）
- `docs/设计文档/功能模块/资产规范/01-智能化生产设备_T1T3/资产图集提示词/00_公共风格基底与使用说明.md`
- `docs/设计文档/功能模块/资产规范/01-智能化生产设备_T1T3/资产图集提示词/01_T1_智能化生产设备.md`（12 道工序条目）

## 运行

```bash
cd Tools/algo_sprite_gen

# 全部 12 台 + 拼版预览 + 基准件对比
python gen_t1_single.py

# 只生成第 N 台（调参迭代用）
python gen_t1_single.py --only 3

# 调整拼版放大倍数
python gen_t1_single.py --scale 3
```

依赖：`numpy`、`Pillow`（本机 venv 已装）。
一次完整生成约 1.5 秒。

## 产物

| 路径 | 说明 |
|---|---|
| `out/PC_<Function>_T1_east.png` | 12 张 128×128 RGBA 单向贴图（east 为基准向） |
| `out/_sheet_t1_east.png` | 12 台拼版预览（含算法标注） |
| `out/_vs_benchmark.png` | 与风格基准件 `PC_AutoFarm_M` 同缩放对比 |
| `out/_report.json` | 技术红线自检结果（逐条 pass/fail） |

## 算法分配（12 台覆盖不同算法族）

| # | 设备 | 形状算法 | 材质算法 |
|---|---|---|---|
| 01 | PC_Smelter_T1 冶炼 | SDF 布尔（坩埚 / 线圈） | fBm + 湍流热辉光 |
| 02 | PC_Forge_T1 锻造 | SDF 平滑并集 | 各向异性拉丝金属 |
| 03 | PC_HeatTreat_T1 热处理 | SDF 圆角腔体 | 元胞自动机凹坑 |
| 04 | PC_RoughMach_T1 粗加工 | Voronoi 分格 | Worley F2-F1 龟裂 |
| 05 | PC_PrecMach_T1 精密机加工 | SDF 布尔（立柱 / 主轴） | Worley 花岗岩斑点 |
| 06 | PC_Barrel_T1 枪管 | 一维中点位移 | 中点位移膛线起伏 |
| 07 | PC_Surface_T1 表面处理 | SDF 槽体 | Gray-Scott 反应扩散 |
| 08 | PC_Furniture_T1 木托护木 | SDF 龙门 + 差集 | 各向异性木纹 + 年轮 |
| 09 | PC_Magazine_T1 供弹 | 模板阵列拼装 | DLA 枝晶 + 调色板交换 |
| 10 | PC_Assembly_T1 总装 | 胶囊 / CSG 拼装 | Worley 皮革细胞 |
| 11 | PC_Calibration_T1 调试校准 | 同心环 SDF | Mandelbrot 逃逸场 |
| 12 | PC_Inspection_T1 质检计量 | SDF 表盘 + 环 | Poisson Disk 测点阵 |

## 目录职责（单一职责，依赖单向）

```
pcgen/
  palette.py    色板常量（唯一真源 = 美术风格规范 §2），不做计算
  sdf.py        有符号距离场图元、布尔运算、2.5D 挤出
  noise.py      噪声族：Perlin / 值噪声 / Worley / fBm / 湍流 / 山脊 / 各向异性
  fractal.py    分形族：1D/2D 中点位移 / L-system / DLA / 同心环 / Mandelbrot
  cellular.py   元胞族：B/S 规则自动机 / 洞穴 / 铸件凹坑 / Gray-Scott 反应扩散
  geometry.py   计算几何：Voronoi / Poisson Disk / 阵列 / 折线距离场
  fieldkit.py   算法场统一入口与缓存（设备配方只跟它打交道）
  render.py     渲染管线：图层合成 / 光照 / 做旧 / 明度校准 / 导出
devices_t1.py   12 台设备的算法配方（形状 + 材质 + 主功能色）
gen_t1_single.py  入口：批量生成 + 技术红线自检 + 拼版预览
```

依赖方向：`devices_t1` → `fieldkit`/`render` → `noise`/`fractal`/`cellular`/`geometry`/`sdf` → `palette`。
反向依赖不存在，算法层不感知设备语义。

## 关键参数（调参入口）

| 想改什么 | 改哪里 |
|---|---|
| **视角（正交 ↔ 3/4 斜视）** | `pcgen/render.py` 的 `PROJ_K`（0.88 = 3/4 斜视，1.0 = 纯俯视正交） |
| 立面（南侧墙）可见高度 | `pcgen/render.py` 的 `LAYER_PX` + `PROJ_LAYER_BOOST` |
| 部件高低层次 | `pcgen/render.py` 的 `LAYER_PX`（层高等级 → 挤出像素） |
| **黑描边粗细** | `gen_t1_single.py` 的 `s.outline(px=2)` |
| **对比跨度（亮部强度）** | `gen_t1_single.py` 的 `s.contrast_stretch(amount=0.72)` |
| 暗缝/亮边的浓度与色值 | `pcgen/render.py` 的 `BEVEL_HI` / `SEAM_COL` / `SEAM_HI` |
| 钣金拼板缝的疏密 | `pcgen/render.py` 的 `_plate_seams()` 里的随机步长区间 |
| 整体明度 | `gen_t1_single.py` 的 `flatten_luminance(pivot=44/255)` |
| 底盘外形 | `devices_t1.py` 的各 `dev_*` 里的 SDF 参数 |
| 主功能色 | `devices_t1.py` 的 `ACCENT` 字典 |
| 单台设备形态 | 对应 `dev_*` 函数的 SDF 参数与算法 seed |

## v3 关键修正（"细节不满"的根因与解法）

v2 把细节不足归因为"纹理密度不够"，于是不断加密表面网格——**方向错了**。
用 `_diag_gutter.py` / `_diag_outline.py` 对基准件与生成件做同口径量化后，
定位到 6 个真正的原因：

| # | 根因 | 量化证据（基准件 vs v2 生成件） | 解法 |
|---|---|---|---|
| 1 | **完全没有黑描边** | 外缘第 1px 环带明度：基准件 **11**，生成件 22–32 | 补 `Sprite.outline(px=2)`。官方 Mod Textures 规范原文：interactive objects "should have a thick black outline, generally equal to 2px of thickness per 64px" |
| 2 | **中间调压缩** | 明度三档（深暗/主灰/亮灰）：基准件 0.46/0.34/0.21，生成件 0.30–0.52/**0.29–0.59**/0.10–0.26 | 给整机建明度层级：底壳压到深灰档、台面升到亮灰档 |
| 3 | **亮部上不去** | p90：基准件 **135**，生成件 89–121 | `Sprite.contrast_stretch()`——以**中位数为不动点**的二次曲线，抬亮部而不动中位明度（否则会冲出规范区间 32–58） |
| 4 | **周期网格 = 瓷砖** | 细节占比达标（0.031 ≈ 0.0315）但读感全错 | 改成 `_plate_seams()`：随机位置 + 随机长度 + 横竖交织不闭合的**非周期**钣金拼板缝 |
| 5 | **亮暗不紧邻** | 紧邻配对：基准件 0.0073，生成件 0.0002–0.005 | 暗缝与亮线间距压到 1px 内（`np.gradient` 中心差分才吃得下） |
| 6 | **纯俯视正交丢立面** | 项目**自有**的 PC_Conveyor_*、PC_FarmFrame、PC_AutoFarm_M **全部是 3/4 斜视**，且基准件同为 128×128 | `PROJ_K` 投影：**在坐标网格上压缩 y 轴**，配方零改动、所有 SDF 自动一致投影 |

第 6 条的实现方式值得记一笔：投影若做在 SDF 或像素缓冲上，`s.paint(mask)` 用的
那些掩码就会错位；做在**采样坐标网格**（`Sprite.y`）上，则设备配方里的每一个
SDF 都落在同一投影下，12 台配方一行都不用改。

⚠ 规范 §4 字面写"2D 俯视正交"，与项目既有资产的实际视角冲突。当前取
`PROJ_K = 0.88`（轻度斜视）作为折中；`1.0` 可随时退回正交做 A/B 对比。

## 技术红线自检项（对齐美术风格规范 §7）

- `png32_rgba` — PNG-32 RGBA 8bit
- `not_palette` — 非索引色
- `size_1to1` — 128×128，设计分辨率 1:1
- `no_halo` — 透明区 alpha=0 且 RGB 干净，无黑晕白边
- `alpha_clean` — 无孤立脏边像素（0.02 < alpha < 0.35）
- `size_ok_kb` — 单文件 < 80KB（128px 类）
- `has_transparent` — 存在透明区域

当前状态：**12/12 全部通过**（含 7 条技术红线 + 6 项观感指标）。

## 诊断脚本

| 脚本 | 用途 |
|---|---|
| `_diag_gutter.py` | 暗沟 / 亮边 / 紧邻配对 / 明度三档 / 对比跨度（同口径对比基准件） |
| `_diag_outline.py` | 外缘逐环明度，验证黑描边是否达标 |
| `_inspect.py N... [倍数]` | 5–6 倍最近邻放大拼版，用于目检（`python _inspect.py 01 07 6`） |


## 部署

产物落到游戏贴图目录（单向只出 `_east`，其余方向待补）：

```
Textures/Things/Building/WeaponWorkshop_T1/PC_<Function>_T1_east.png
```

未自动写入 —— 先目测确认再复制，避免污染正式资产目录。

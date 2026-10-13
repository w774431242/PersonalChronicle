# 制造系统中心 · RUIF 大改造方案（2026-09-12）

> **目标**：把制造系统中心（及总控中心外壳）中**可以替换的 UI 组件**全部换成 RUIF 的组件/令牌，使 PC 界面获得 RUIF 的**工业风组件族**与**统一令牌**。
> **方法**：LAND-001 先量后写 —— 已对 `RUIF/Source`（25 个 Atoms 文件 + Core）实测公开签名，映射表见 §3。
> **状态**：**待依赖形态裁决**（§2），裁决后按 §4 分批落码（每批编译 + 单测 + 截图验证）。
> ⚠️ **路径基准（2026-09-12 起）**：本文所有 `RUIF/…` 路径均指**独立仓库根** `E:\SteamLibrary\steamapps\common\RimWorld\11\RUIF`（已移出本仓库，与 `PersonalChronicle` 平级）。已落地的实际接入形态见 MEMORY 的「RUIF 接入（PC 侧，软依赖）」节。

---

## 1. 结论摘要（探测结果）

| 事实 | 值 | 来源 |
|---|---|---|
| PC 当前引用 RUIF | **0 处**（csproj 无 `Reference`、源码无 `using RUIF`、`About.xml` 无 `modDependencies`） | grep 实测 |
| RUIF 程序集 | `Mods/RUIF/Assemblies/RUIF.dll`（87 KB，2026-09-12 15:46） | 磁盘实测 |
| RUIF 命名空间 | **`RUIF`**（单一命名空间；`Core` / `Layout` / `Components` 为目录分区） | `namespace RUIF` 实测 |
| 主题系统形态 | **`RUIF.Tokens`（静态色令牌）**，非"多主题 Palette" | `Core/Tokens.cs:11` |
| 组件族 | `Components/` 下 **25 个 Atoms 文件**（含 **`Industrial`** 工业风组件集） | 目录实测 |

> ⚠️ 与既有记忆的差异：记忆中"RUIF 有 14 套主题 + `DrawDecor` + `ThemeAssets`"对应的是**较新版本**；**当前 workspace 的 `RUIF/Source` 是 `Tokens` 形态**。因此**映射以本方案 §3 实测签名为准**，不以记忆为准。

## 2. 依赖形态（**必须先裁决**）

| 选项 | 做法 | 玩家成本 | 回滚 |
|---|---|---|---|
| **A 硬依赖** | `csproj` 加 `Reference` + `About.xml` 加 `modDependencies` | **必须同时装 RUIF**（否则 PC 不加载） | 删两处即回滚 |
| **B 软依赖**（推荐） | 反射探测 + `[MethodImpl(NoInlining)]` 隔离调用；RUIF 缺失时回落 PC 现有实现 | 无（可选增强） | 天然可回滚 |
| **C 不引入** | 只手写工业风（零依赖） | 无 | — |

**推荐 B**：理由——本改造的收益是"皮肤/组件升级"，**不应以"玩家必须多装一个 Mod"为代价**；且 B 天然满足 GATE-COMP（缺失可运行、仅存在时条件适配）。

> **注意 A 的隐藏代价**：`modDependencies` 会让 RUIF 缺失时 PC **直接不加载**，属不可逆的玩家侧门槛；若选 A，须同时把 RUIF 加进 Workshop 依赖声明与 README 前置说明。

## 3. 可替换组件映射表（实测签名）

### 3.1 令牌层（**最高性价比：一次替换，全局生效**）

| PC（`Archive/UI/UITheme.cs`） | RUIF（`Core/Tokens.cs`） | 备注 |
|---|---|---|
| `UITheme.Window` | `Tokens.Window` | 同名同义 |
| `UITheme.Panel` / `PanelRaised` | `Tokens.Panel` / `PanelRaised` | ✓ |
| `UITheme.Card` / `CardHover` | `Tokens.Card` / `CardHover` | ✓ |
| `UITheme.Border` / `BorderSoft` / `BorderHair` | 同名 | ✓ |
| `UITheme.Text` / `Muted` / `Dim` | 同名 | ✓ |
| `UITheme.Accent` / `Info` / `Alive` / `Dead` / `Warn` | 同名 | ✓ |
| `UITheme.Threat` 等 PC 特有 | — | 保留（或映射到 `Tokens.Warn`） |

> **实现方式（关键）**：`UITheme` 的字段是 **`static` 可变**（v1.1.4 起），因此可**保留字段名不变**、把赋值来源改为 `Tokens.*` ⇒ **全部调用点零改动**即可"换成 RUIF 令牌"。

### 3.2 绘制原语层（`UIComponents` 转发）

| PC（`UIComponents`） | RUIF |
|---|---|
| `Panel(rect)` / `Card(rect, tone)` | `Draw.Box` + `Draw.Border`（组合，与现实现等价但走 RUIF） |
| `Border(rect, color)` | **`Draw.Border(rect, color)`**（直接替换） |
| `Rule(rect, color)` | `Draw.LineH` / `Draw.LineV` |
| `Label(rect, text, font, color, anchor)` | **`Draw.Label(...)`**（直接替换） |
| `WithAlpha(c, a)` | **`Draw.WithAlpha(c, a)`**（直接替换） |

### 3.3 组件层（逐个替换 · **收益最大**）

| PC 现有 | **RUIF 组件** | 用在制造中心的哪 |
|---|---|---|
| 自研仪表（`ManufacturingAnalysisPanels` 的率卡） | **`Industrial.Gauge(rect, v, lo, hi, label)`** + `Industrial.GaugeNeedleDeg` / `ZoneColor` / `IndustrialFmt.GaugeZone` | ① 总控 KPI（OEE 四率 + 5 仪表） |
| 自研 LED / 状态点 | **`Industrial.LedMatrix(rect, statuses, cols)`** + `Industrial.LedColor` + `IndustrialFmt.LedCell` | ③ 运营（在岗殖民者热力）、⑤ 设备健康 |
| 自研时序条（`DrawTimeline` 40 格） | **`StatusFmt.TrailAlpha`** + **`IndustrialFmt.MergeRunLengths`** + `IndustrialFmt.SegmentsTotal` | 设备弹窗 ① 运行 · 状态时序 |
| 自研闪烁（`Flash`/`flashring`） | **`PanelAtoms.FlashOverlay(rect, t, color, thickness)`** + `PanelFmt.FlashAlpha` | 画布设备卡/车间 chip 的闪烁定位 |
| 自研"标签 + 值 + 条"行（原料条/健康行/契约行） | **`PanelAtoms.StatRow(rect, label, value, bar01, barColor)`** | 左右轨道原料库、⑤ 设备健康、⑥ 契约页 |
| 自研工人圆点 | **`PanelAtoms.MiniProfileChip(rect, initial, name, mood01, health01)`** | ③ 人员页 / ④ 运营在岗殖民者 |
| 自研分区标题 | **`PanelAtoms.SectionBar(rect, title, count, expanded)`** | 各 tab 的面板标题（可折叠） |
| 自研状态灯/忙碌指示 | **`StatusAtoms.PulseDot`** / `Glyph` / `Spinner` | 设备卡状态灯、加载中 |
| 自研瓶颈带（`BottleneckRibbon`） | **`IndustrialFmt.BottleneckIndex(stageTimes)`** + `Shares(values, outBuf)` | ③ 分析 · 工序瓶颈带 |
| 自研图表（趋势/直方图） | **`Charts`**（361 行组件族） | ③ 分析 · 吞吐趋势、品质直方图 |
| 自研表格（Runtime Things / Def 注册表） | **`TableAtoms`** | ⑦ 数据层契约两面板 |
| 自研进度条 | **`ProgressAtoms`** | 画布设备卡进度条、KPI 条 |
| 自研页签（`ftabs` / 弹窗 `dtab`） | **`NavAtoms` / `NavAtomsB`** | 底栏 7 页签、弹窗 7 页签 |
| 自研空态 / 缺失态 | **`FeedbackAtoms` / `FeedbackAtomsB`** | 空画布空态、各面板"未接入" |
| 自研文本/数值排版 | **`TextAtoms`** / `VisualAtoms` / `Shapes` | 全局 |
| 自研输入（下拉/开关/滑块） | **`InputAtoms` / `InputExAtoms` / `InputPlusAtoms`** | 编排页签（坐标网格开关、密度开关） |
| 自研数据展示 | **`DataAtoms` / `DataExAtoms`** | ⑥ 数据桥 / ⑦ 数据层 |

> **`Industrial` 正是用户要的"传统制造工业风格"组件集**（仪表盘 / LED 矩阵 / 工业配色），应作为本次视觉方向的主抓手。

## 4. 分批（依赖形态裁决后执行）

| 批 | 内容 | 影响面 | 风险 |
|---|---|---|---|
| **P1** ✅ | **依赖接入 + 令牌层**（2026-09-12 完成，build **`0912n`**） | 全局配色 | 低（一次换全局） |
| **P2** | `UIComponents` 的 `Border/Rule/Label/WithAlpha` 转发到 `Draw.*` | 全局绘制原语 | 低 |
| **P3** | **画布**：设备卡状态灯 → `StatusAtoms`；闪烁 → `PanelAtoms.FlashOverlay`；进度条 → `ProgressAtoms` | ② 主画布 | 中 |
| **P4** | **① 总控 KPI** → `Industrial.Gauge` + `IndustrialFmt.GaugeZone`（替换自绘率卡/仪表） | ① | 中（几何需重测 LAND-001） |
| **P5** | **弹窗**：时序条 → `IndustrialFmt.MergeRunLengths` + `StatusAtoms.TrailAlpha`；行 → `PanelAtoms.StatRow`；页签 → `NavAtoms` | 设备弹窗 7 页签 | 中 |
| **P6** | **③ 分析**：瓶颈 → `IndustrialFmt.BottleneckIndex`/`Shares`；趋势/直方图 → `Charts` | ③ | 中高（图表几何需重测） |
| **P7** | **⑦ 数据层** → `TableAtoms`；**底栏/弹窗页签** → `NavAtoms`；空态 → `FeedbackAtoms` | ⑦ / 外壳 | 中 |
| **P8** | 编排页签输入控件 → `InputAtoms*` | 编排 | 低 |

**每批验收**：编译 0 错 0 警 + 单测 301 + 发布区哈希一致 + 该模块截图复核（LAND-014）。

### P1 实施记录（✅ 已完成 · build `0912n`）

**决策**：用户批准 **B 软依赖** + 保留 PC 三套主题作回落 + 范围先只做制造中心。

| 改动 | 文件 | 要点 |
|---|---|---|
| 程序集引用 | `Source/PersonalChronicle.csproj` | 新增 `<Reference Include="RUIF" Condition="Exists('$(RimWorldPath)\Mods\RUIF\Assemblies\RUIF.dll')">` + **`<Private>false</Private>`**（⇒ 不随 PC 发布）；条件式引用保证无 RUIF 的编译机也能构建 |
| 软依赖桥 | `Source/PersonalChronicle/Archive/UI/RuifBridge.cs`（新） | ① `Probe()` **反射扫 `AppDomain` 找 `RUIF.Api` 类型**（全程不触碰 RUIF 类型）；② `Available` 延迟探测一次并缓存；③ **唯一触碰 `RUIF.Tokens` 的 `ReadTokens` 标 `[MethodImpl(NoInlining)]`**；④ 跨边界只传 **值类型 `TokenSet`**（16 个 `Color`） |
| 令牌层 | `Archive/UI/UITheme.cs::Apply` | `Assign(p)` 之后：`if (RuifBridge.TryGetTokens(out var ruif)) { 覆盖 16 个同名令牌 + AccentSoft }` ⇒ **字段名零改动、调用点零改动**即完成全局换皮；`ArchiveUiStyle.RefreshFromTheme()` 仍在末尾统一同步 |

**为什么"未装 RUIF 也不会崩"**：`ReadTokens`（唯一引用 RUIF 类型的成员）只会在 `Available == true` 时被调用 ⇒ 未装 RUIF 时**永不进入 JIT**，不会出现"JIT 期 `TypeLoadException`"（这正是 RUIF 官方 `Core/Api.cs` 警告的坑）。

**验证**：编译 **0 错 0 警** · 单测 **301 通过** · `Assemblies/` 内**只有 `PersonalChronicle.dll`**（`Private=false` 生效，RUIF.dll 未被复制）· 发布区 dll `8A78B1BA5274` / pdb `6C41E872A3B7` 已同步。

**未验证（需实机）**：装了 RUIF 时 PC 界面的令牌是否如期变为 RUIF 配色（需进游戏看任意 PC 界面）。

### P4 实施记录（✅ 已完成 · build `0912o`）

| 项 | 内容 |
|---|---|
| RUIF 签名（实测 `Components/Industrial.cs:126`） | `Gauge(Rect r, float v, float lo, float hi, string label = null, float warn01 = 0.6f, float dead01 = 0.85f)`；内部 = 上半环每 4° 分区弧（`ZoneColor(0/1/2)` = `Tokens.Alive/Warn/Dead`）+ `Draw.Rotate` 作用域 + 指针（`GaugeNeedleDeg(v01)=180+180·v01`）+ 中心数值 `v01*100%` + 左上标签 |
| 桥接 | `RuifBridge.TryDrawGauge(rect, v01, label)`（**NoInlining** + `Available` 守卫 + `try/catch`） |
| 接入点 | `MasterControlTabWindow.AppShell.DrawAppKpiCard`：`if (kpi.Share01 >= 0f && RuifBridge.TryDrawGauge(card, kpi.Share01, kpi.Label)) { Tooltip; return; }` |
| **受控偏离** | 换表盘后该卡**不再显示 `Note`（口径附注）与右侧进度条** ⇒ 需登记进 LAND 偏离清单 |
| 回落 | 无占比的卡（如「监管带宽 —」）保持原率卡形态；RUIF 未装 ⇒ 自动回落原实现 |

---

## 4.1 剩余批次落码包（**下一轮可直接照做，无需再勘察**）

> 本节把 P3 / P5–P8 所需签名与接入点全部固化（2026-09-12 实测），避免重复勘察。

### P3 · 画布组件

| 目标 | RUIF 签名（实测） | 接入点（PC） |
|---|---|---|
| 设备卡状态灯 | `StatusAtoms.PulseDot(Rect, Color, float t, float minAlpha = 0.45f)`；`StatusKind { Ok, Info, Warn, Error, Paused }` + `StatusAtoms.Glyph(Rect, StatusKind)`；`StatusFmt.DotRect(Rect)` | `WorkshopLayoutCanvas.DrawDeviceCard` 里 `Widgets.DrawBoxSolid(lamp 5×5)`；状态键→`StatusKind` 映射：`run→Ok` / `idle→Paused` / `fault→Error` / `off→Info` |
| 设备卡进度条 | `ProgressAtoms.FillWipe(Rect, float progress01, Color? color = null, …)`；`ProgressAtoms.CapacityBar(Rect, float used, float capacity, Color? fill = null)` | 同文件设备卡底部进度条（现为 `DrawBoxSolid` 手工条） |
| 画布闪烁 | `PanelAtoms.FlashOverlay(Rect, float t, Color? color = null, float thickness = 2f)`；`PanelFmt.FlashAlpha(float t, float minAlpha = 0.45f)` | `WorkshopLayoutCanvas.Flash/IsFlashing` 的闪环（现为外扩 `Border`） |
| 缩放条倍率 | `ProgressAtoms.ZoomIndicator(Rect, float zoom, float resetValue = 1f)` | `AppShell.DrawCmdBarZoom` 的 `NN%` 文本 |

### P5 · 设备弹窗

| 目标 | RUIF 签名 | 接入点 |
|---|---|---|
| 时序条 | `StatusFmt.TrailAlpha(int i, int head, int segments)`；`IndustrialFmt.MergeRunLengths(int[] states, int[] outBuf)`；`IndustrialFmt.SegmentsTotal(float[] durations)` | `FloorDeviceDetailWindow.DrawTimeline`（现已按固定 40 格右对齐） |
| 行（标签+值+条） | `PanelAtoms.StatRow(Rect, string label, string value, float? bar01 = null, Color? barColor = null)` | `FloorDeviceDetailWindow.DrawRows` |
| 标签行分区 | `PanelAtoms.RowSplit(Rect row, float labelRatio, float gap, out Rect label, out Rect value)` | 同上（列宽分配） |
| 页签 | **`NavAtoms.TabBar(Rect, string[] tabs, ref int sel, ref float indicatorX)`** | `FloorDeviceDetailWindow.DrawTabBar`（需新增 `float tabIndicatorX` 窗口字段） |

### P6 · 分析带

| 目标 | RUIF 签名 | 接入点 |
|---|---|---|
| 瓶颈判定 | `IndustrialFmt.BottleneckIndex(float[] stageTimes)`；`IndustrialFmt.Shares(float[] values, float[] outBuf)` | `BuiltinManufacturingProvider.Analysis.cs`（现为 `recipe.WorkAmountTotal` 占比自算） |
| 仪表 | `Industrial.Gauge(...)` / `IndustrialFmt.GaugeNorm/GaugeZone` | ③ 分析「产能利用率」等天然仪表位 |
| 图表 | `Components/Charts.cs`（361 行，含 DonutChart 等） | ③ 吞吐趋势 / 品质直方图 |

### P7 · 数据层与外壳

| 目标 | RUIF 签名 | 接入点 |
|---|---|---|
| 表格 | `TableAtoms.HeaderCell(Rect, string label, SortState)`；`TableAtoms.ValueCell(Rect, float value, float reference)`；`TableAtoms.ColumnSplit(Rect, float[] shares, float gap, float[] widths)`；`TableAtoms.RowDensity{Compact,Normal,Cozy}` + `RowHeight(RowDensity)` | `ManufacturingDataLayerPanels`（Def 注册表 / Runtime Things） |
| 底栏页签 | `NavAtoms.TabBar(...)` | `MasterControlTabWindow.FloorTabs.cs::DrawFloorTabBar` |
| 面包屑 | `NavAtoms.Breadcrumb(Rect, string[] path, Action<int> onJump)` | `AppShell.DrawAppShellHeader` 的「注册表 / …」行 |
| 空态 | `FeedbackAtoms` / `FeedbackAtomsB`（未勘具体签名） | 各面板「未接入」/空画布空态 |

### P8 · 编排输入

| 目标 | RUIF 签名 | 接入点 |
|---|---|---|
| 开关/输入 | `InputAtoms` / `InputExAtoms` / `InputPlusAtoms`（未勘具体签名） | 编排页签（坐标网格 / 叠加标注开关）、`#lbar` 开关 |

> **每批落码前只需**：读一次"接入点"方法体（确认原文）→ 用 `RuifBridge.Try*`（NoInlining + 守卫 + try/catch）包装 → 编译 0/0 → 单测 301 → 截图复核。
> **包装范式（统一）**：所有 RUIF 调用一律经 `RuifBridge` 的 `Try*` 方法（返回 `bool`，false 即回落），**不得**在业务代码里直接 `using RUIF` 或直接调用 RUIF 类型。

## 5. 风险与 Gate

| 项 | 说明 |
|---|---|
| **GATE-ARCH** | 硬依赖时 PC → RUIF 为**单向新增依赖**（PC 是消费方）；软依赖时须 `[MethodImpl(NoInlining)]` 隔离（ECall 铁律同法） |
| **GATE-COMP** | **选 B 才完整满足**（RUIF 缺失时 PC 正常）；选 A 需在 About/README 明示前置 |
| **GATE-PERF** | RUIF Atoms 多为每帧即时模式 ⇒ 替换后需**复核热路径**（画布每帧绘制、KPI 卡） |
| **LAND-001** | P4/P6 替换仪表/图表后**几何基准会变**，必须重测并更新落地基准表（不可沿用旧值） |
| **回滚** | 每批独立提交；P1 是全局开关（令牌来源），回滚即"令牌改回 PC 原值" |

## 6. 待裁决（阻塞项）

1. **依赖形态选 A / B / C？**（§2；推荐 **B 软依赖**）
2. 是否**保留 PC 现有 3 套主题**（Epic/Gothic/Steampunk）作为 RUIF 不可用时的回落？（建议保留 ⇒ B 的自然结果）
3. 改造**范围边界**：只做制造中心，还是**含总控中心外壳与档案馆**？（建议先只做制造中心 + 其外壳，档案馆下一轮）

---

*本方案为 GOV-001「先架构后编码」的前置件；裁决后按 §4 分批落码并逐批出 Gate 记录。*

---

## 9. 执行进度（滚动更新）

| 批 | 状态 | build | 说明 |
|---|---|---|---|
| P1 令牌层 | ✅ 完成 | `0912n` | `RuifBridge` 反射探测 + `UITheme` 16 令牌换 RUIF；dll `8A78B1BA5274` |
| P4 KPI 仪表盘 | ✅ 完成 | `0912o` | `Industrial.Gauge` 指针表盘（受控偏离：该卡不再显示 Note/进度条）；dll `B099C4C1791B` |
| **P4′ 仪表盘（二次回滚 → 自绘替代）** | ❌→✅ | `0912w` | build `0912v` 实机取证：`Industrial.Gauge` 的分区弧走 `Draw.Rotate` → `GUIUtility.RotateAroundPivot`，其 **pivot 按「旋转前的 GUI 空间」解释** ⇒ 在本工程窗口的平移/裁剪作用域下整体错位，45×N 个 8×3 色块绕**窗口左上**旋转、**甩满画布**（`GUI.BeginClip` 包裹亦压不住 —— 裁剪与绘制坐标系不一致，已实机证伪）。**删除 `RuifBridge.TryDrawGauge`**，改为 `AppShell.DrawKpiDial` **PC 自绘等价表盘**：几何同式（上半环 4°/格共 45 格 · 指针角 `180+180·v01` · 中心读数 · 左上标签），**零 GUI 旋转**（色块 cos/sin 直接定位），配色取 `UITheme` 令牌（装了 RUIF 即 RUIF 配色）；dll `C1C55F8E6F23` |
| **P4″ 表盘接回（改「修 RUIF」+ 版本闸门）** | ✅ | `0912x` | 按用户指示把修法从"绕开 RUIF"改为 **"修 RUIF"**：**RUIF v1.0.2** 把 `Draw.Rotate` 从 `GUIUtility.RotateAroundPivot` 改为**局部空间后乘矩阵**（`M' = M·T(p)·R·T(-p)`，显式展开四列、绕开 `Quaternion.Euler` 的 ECall 路径）⇒ 陷阱**在源头消除**（`Industrial.Gauge` / `Charts` / `StatusAtoms.Caret` 一并受益）。PC 侧 `RuifBridge.TryDrawGauge` **恢复**，但加**版本闸门 `GaugeFixedVersion = "1.0.2"`**（读 `RUIF.Api.VersionString` 常量，不触发类型初始化）⇒ 未装 / 旧版 RUIF 自动回落 `AppShell.DrawKpiDial`；RUIF 侧同步：单测 **174/174**、门禁 **9 道 PASS**、发布区 dll MD5 `FE3279069EE23F70D999F10DA3E8A939`；PC dll `07139A184DE6` |
| P3 画布状态灯 + 闪烁 | ✅ 完成 | `0912p` | `StatusAtoms.PulseDot` + `PanelAtoms.FlashOverlay`；**并补回范式改造中丢失的闪烁绘制**；dll `A36DF63BE445` |
| P5 时序条 | ✅ 完成 | `0912q` | `StatusFmt.TrailAlpha` 尾部渐亮（`DrawTimeline`）；dll `A619024B7AEF` |
| **P5 弹窗行** | ❌ **否决** | — | 复核 `DrawRows`：RUIF `PanelAtoms.StatRow` **不含"数据来源徽标（API/LEDGER/SIM）"** ⇒ 替换会**丢失 LAND-009 要求的口径标注**。**保持本工程实现**，可选做法是"StatRow 画标签+值、徽标另画"，但那已非替换而是叠加，收益不抵复杂度。 |
| P5 页签 / P7 底栏页签 | ⏸ 待做 | — | `NavAtoms.TabBar(Rect, string[] tabs, ref int sel, ref float indicatorX)` —— ⚠ **`string[]` 每帧构造会造成托管分配**（PERF-001）⇒ 必须先做**标题数组缓存**（语言变化时重建），再做替换 |
| P6 分析（瓶颈/仪表/图表） | ⏸ 待做 | — | 签名已固化（§4.1）；`Charts` 具体签名未勘 |
| P7 表格（`TableAtoms`）/ 空态（`FeedbackAtoms`） | ⏸ 待做 | — | 签名已固化；`FeedbackAtoms` 未勘 |
| P8 编排输入（`InputAtoms*`） | ⏸ 待做 | — | 签名未勘 |

**已交付四批的共同约束**：全部经 `RuifBridge.Try*`（NoInlining + `Available` 守卫 + `try/catch`）；RUIF 未装时**逐项回落**本工程实现；编译 0/0 + 单测 301 + 发布区哈希一致（LAND-014）。

---

## 10. ⚠️ 坐标系陷阱（**已两次踩坑 · RUIF v1.0.2 从源头修复**）

**历史缺陷**（v1.0.1 及更早）：`RUIF/Source/Core/Draw.cs::RotateScope` → `GUIUtility.RotateAroundPivot(angle, pivot)`。
该 Unity API 的 **pivot 按「旋转前的 GUI 空间」解释**（实现上先归零矩阵、再合成 `T(p)·R·T(-p)·M`）——
**不是** rect 局部坐标。消费方窗口绘制处在**平移/裁剪作用域**中 ⇒ 任何"按 rect 局部坐标传 pivot"
的 RUIF 组件都会整体错位；`GUI.BeginClip(rect)` + 局部坐标的包裹**实测无效**
（裁剪与绘制的坐标系并不一致）。build `0912v` 实机证伪：`Industrial.Gauge` 的 45×N 个 8×3 色块
绕**窗口左上**旋转、甩满画布。

**根因修复**（**RUIF v1.0.2**）：`RotateScope` 改为构造**局部空间**旋转矩阵并**后乘**当前矩阵
（`M' = M · T(p) · R · T(-p)`）—— 语义即"先把绘制点绕**局部 pivot** 旋转，再做外层变换"，
与调用点（`Draw.Box` / `Draw.Label` 的落点）严格同空间；**调用点接口与坐标约定不变** ⇒ 对既有正确用法零迁移。
并抽 `internal Draw.LocalRotation(angle, pivot)` + 3 条回归测试（含"外层平移下到 pivot 的距离不变"断言）。

| RUIF 组件 | v1.0.1 及更早 | v1.0.2（现版本） | PC 侧处置 |
|---|---|---|---|
| `Industrial.Gauge` | **错位**（45 块/卡，整屏甩弧） | **已修** | 经版本闸门 `≥1.0.2` **恢复调用**（`RuifBridge.TryDrawGauge`）；否则回落 `AppShell.DrawKpiDial` |
| `Charts`（`DonutChart` 等，`Charts.cs:281`） | **错位** | **已修** | P6 接入前**无需再自绘** |
| `StatusAtoms.Caret`（`StatusAtoms.cs:126`） | 错位（单字形，位移量小） | **已修** | 可按需使用 |
| `StatusAtoms.PulseDot` / `Spinner(Rect,t)` / `PanelAtoms.FlashOverlay` / `StatusFmt.*` / `IndustrialFmt.*` | 不受影响 | 不受影响 | 已用（P3 / P5） |

> **落码纪律（保留）**：凡 `RuifBridge` 新增桥接，先确认目标组件**是否触碰 `GUI.matrix`**。
> 触碰者的**正确姿势是修框架**（本工程标准做法：先根因、后闸门）；只有"框架不可控（真第三方）"时，
> 才走"自绘等价物 + 令牌取色"。
>
> **版本闸门纪律（本批建立）**：**玩家机器上可能装着旧版 RUIF** ⇒ 凡"缺陷在框架侧已修"的组件，
> 消费侧**必须**带 `VersionAtLeast(<修复版>)` 闸门（范式见 `RuifBridge.GaugeFixedVersion = "1.0.2"`）。

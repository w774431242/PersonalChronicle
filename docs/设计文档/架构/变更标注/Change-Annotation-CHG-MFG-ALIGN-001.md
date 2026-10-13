---
change_id: CHG-MFG-ALIGN-001
title: 制造系统中心编排画布 · 对齐吸附工具（M1 纯函数 + M2 UI 接线）
date: 2026-10-03
baseline: HEAD=21fd0a9
  - path: Source/PersonalChronicle/Domain/WorkshopCanvasAlignment.cs
    type: added
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Align.cs
    type: added
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Primitives.cs
    type: added
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Placement.cs
    type: added
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Interaction.cs
    type: added
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.cs
    type: modified
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.AppShell.cs
    type: modified
  - path: Source/PersonalChronicle/Archive/UI/UITheme.cs
    type: modified
  - path: Languages/English/Keyed/Archive.xml
    type: modified
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: modified
  - path: Tests/WorkshopCanvasAlignmentTests.cs
    type: added
  - path: docs/设计文档/架构/Change-Annotation-CHG-MFG-ALIGN-001.md
    type: added
  - path: docs/设计文档/架构/GR-CHG-MFG-ALIGN-001.md
    type: added
  - path: docs/设计文档/架构/EXC-2026-011.md
    type: modified
---

# Change Annotation — CHG-MFG-ALIGN-001

> 制造系统中心 · 编排画布**对齐吸附**工具（M1 纯函数 + M2 UI 接线）
> 日期：2026-10-03 ｜ 分支基线：`HEAD=21fd0a9`（领先 `origin/main` 1 commit，工作区另有约 477 个既有未提交文件）
> 交付形态：**一个可交付单元**（M1+M2 合并；M1 单独不足以体现用户价值）

---

## 0. 变更清单速览

| # | 文件 | 动作 | 类型 | Layer |
|---|---|---|---|---|
| F1 | `Source/PersonalChronicle/Domain/WorkshopCanvasAlignment.cs` | 🆕 333 行 | 对齐吸附纯函数 | Domain |
| F2 | `Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Align.cs` | 🆕 304 行 | 吸附接线 + 参考线 overlay | UI |
| F3 | `Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.cs` | ✏️ 1159 行（原 1538） | CanvasState +3 字段 / Create / overlay 调用 / 传 palette | UI |
| F4 | `Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Interaction.cs` | 🆕（切片新增）+ 改 | 设备卡拖拽走统一落点 + MouseUp 落档 | UI |
| F5 | `Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Primitives.cs` | 🆕 113 行 | 纯物理切片 | UI |
| F6 | `Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Placement.cs` | 🆕 69 行 | 纯物理切片 | UI |
| F7 | `Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Interaction.cs` | 🆕 290→304 行 | 纯物理切片 + M2 改 | UI || F8 | `Source/PersonalChronicle/MasterControl/MasterControlTabWindow.AppShell.cs` | ✏️ | `#lbar` 加开关 + `DrawLbarToggle` 加可选 tooltip | UI |
| F9 | `Source/PersonalChronicle/Archive/UI/UITheme.cs` | ✏️ +10 行 | `AlignGuide` 主题令牌 | UI |
| F10 | `Languages/English/Keyed/Archive.xml` | ✏️ +4 键 | 本地化 | 资源 |
| F11 | `Languages/ChineseSimplified/Keyed/Archive.xml` | ✏️ +4 键 | 本地化 | 资源 |
| F12 | `Tests/WorkshopCanvasAlignmentTests.cs` | 🆕 435 行 | 27 条离线单测 | Test |

> F4/F7 是同一文件（切片时新建，M2 在其上追加改动），Gate Record 中合并计一条。

**零改动确认**：`ChronicleGameComponent.DeviceLayout.cs` · `ManufacturingLayoutService.cs` · `Defs/` · `Patches/` · `Compatibility/` · `Textures/` · `About.xml` · `WorkshopFloorPlan.*`

---

## 1. 【变更 A】对齐吸附能力本体（M1 + M2）

### F1 · `Domain/WorkshopCanvasAlignment.cs` 🆕

| 符号 | 行 | 说明 |
|---|---|---|
| `enum CanvasAlignKind` | `:6` | 6 值：None / NeighborMin / NeighborCenter / NeighborMax / Grid / Origin |
| `readonly struct CanvasAlignRect` | `:33` | 设计单位 AABB，**只用 float 分量**（禁 `Vector2`/`Rect`） |
| `CanvasAlignRect.FromCenter` | `:49` | 由「中心 + 半尺寸」构造（设备卡语义 = 存中心点） |
| `struct CanvasAlignAxis` | `:69` | 单轴结果：`Snapped/Delta/Line/Kind/SourceIndex` |
| `struct CanvasAlignResult` | `:100` | X/Y 两轴独立求解结果 |
| `CanvasAlignment` | `:135` | 静态纯函数类 |
| `SnapScreenPx = 8f` | `:138` | 目标屏幕距离（缩放无关手感常量） |
| `MinSnapDesign = 1f` | `:141` | 阈值下限 |
| `MaxSnapDesign = 30f` | `:147` | 阈值上限 = 设备卡高一半 |
| `ScreenThresholdToDesign` | `:174` | 屏幕 px → 设计单位，zoom 感知 + 双向钳制 |
| `Resolve` | `:203` | 矩形对齐（X/Y 分解为两个一维问题） |
| `ResolveAxis` | `:230` | 单轴求解 + 阈值门 + 确定性仲裁 |

**算法语义**：每邻居每轴 3 条候选线（min/center/max），移动物用**同名线**匹配 ⇒ O(N)，不做 O(N²) 候选线配对。仲裁序：阈值门 → 距离更近 → 来源序号小 → 坐标更小。**全序无并列 ⇒ 同一输入永远同一输出 ⇒ 不需要迟滞带。**

**约束（可 grep 二值判定）**
- C1 禁 `using UnityEngine/Verse/RimWorld`（文件级纯净，对齐 `WorkshopDeviceFit` 范式）
- C2 代码内禁 `Vector2`/`Rect`/`Color` 标识符
- C3 UI 侧禁 `abs(...) OP threshold` 形态
- C4 阈值常量只在 Domain 声明；UI 侧 `ScreenThresholdToDesign` 与 `CanvasAlignment.Resolve` **各仅 1 处调用**

**Save 影响**：无。

### F2 · `Manufacturing/UI/WorkshopLayoutCanvas.Align.cs` 🆕

| 符号 | 行 | 说明 |
|---|---|---|
| `struct CanvasAlignGuide` | `:31` | 一条参考线（`Vertical` / `LineDesign` / `Kind`） |
| `AlignGuideThicknessCenter/Edge` | `:55-56` | 线宽具名常量（GOV-006，禁散落魔法数） |
| `BuildAlignNeighbors` | `:72` | 邻居投影：独立设备层 + 车间在册卡；**排除式去重** |
| `ResolveAndMoveDevice` | `:134` | **唯一**落点入口（Drag/Up 共用），返回最终设计坐标 |
| `DrawAlignGuides` | `:199` | 参考线 overlay（clip 内 `DrawPolyline`） |
| `HasAlignTarget` | `:250` | 纯查询，供提示文案如实区分 |
| `AlignSnapLabel` | `:291` | 开关文案 |
| `AlignSnapTip` | `:297` | 提示（随有无目标切换） |

**关键正确性点**
1. **排除式去重**：车间内卡与独立设备层可能同指一台（MouseDown 会并入 `DetachedList`）⇒ 按 `stableId` 排除，**与调用顺序无关**；与渲染循环 `IndexOfDetached(stableId) >= 0 → continue` 同源。
2. **不缓存**：跨帧缓存需同时盯 6 条失效路径（`ClearSession`/`EnsureSessionScope`/`UndoLast`/`ClearAll`/`SetDetached`/`RemoveDetached`），漏一条即幽灵吸附线。每帧重算 = 无跨帧状态 = 无失效面。
3. **零分配**：`AlignBuf`/`AlignGuides` 预建，每帧只 `Clear()+Add(struct)`。
4. **overlay 插入点在拖拽处理之后**（`DrawCanvasBody` 交互块之后）⇒ 参考线与卡**同帧**一致，不滞后 1 帧。
5. **残留清除判据用拖拽态 `deviceCardDragId`**（非单看 `Event.type`）：`Repaint` 帧也会重绘整棵 GUI，只在 `MouseDrag` 帧写会在 `Repaint` 帧画出冻结残线。
6. **裁剪安全**：走既有 `DrawPolyline`；⛔ 禁两点版 `Widgets.DrawLine`（`Matrix4x4.TRS`+GL 不感知 `GUI.matrix`）。

**Save 影响**：无（新增的 3 个 `CanvasState` 字段无 Scribe 路径）。

### F3 · `Manufacturing/UI/WorkshopLayoutCanvas.cs` ✏️

| 位置 | 改动 |
|---|---|
| `CanvasState`（`:108-131`） | +3 字段：`SnapEnabled` / `AlignBuf` / `AlignGuides`，附 **struct 别名铁律**注释 |
| `Create()`（`:133-...`） | +3 行：默认 `SnapEnabled = true`、两个缓冲预建空表 |
| `DrawCanvasBody`（交互块后） | +1 行 `DrawAlignGuides(rect, center, k, ref state);` |
| `DrawCanvasBody`（拖拽调用） | 传 `palette`（吸附需要枚举邻居） |

**struct 别名铁律**：`CanvasState` 是 struct 且同时存在按值传参与 `in` 传参；`List<T>` 字段在值拷贝后**共享同一实例** ⇒ 只允许原地 `Clear()+Add()`，⛔ 禁 `state.AlignBuf = new List<…>()`（值拷贝上赋值会静默丢失写入）。

**Save 影响**：无。

### F8 · `MasterControlTabWindow.AppShell.cs` ✏️

- `DrawLbarToggle` 增加可选 `string tip = null` 形参（既有 2 个调用点不受影响）→ `TooltipHandler.TipRegion`。
- `#lbar` 在「叠加标注」之后追加「对齐吸附」开关。
- **整条 `#lbar` 只在 `floorEditMode` 为 true 时绘制**（`AppShell.cs:448-451`）⇒ 查看态天然不出现该开关，无需额外门禁。
- 提示文案经 `HasAlignTarget` 如实区分「有目标 / 无可对齐对象」⇒ **不静默失效**。

**Save 影响**：无（`SnapEnabled` 是窗口实例态，无 Scribe）。

### F9 · `Archive/UI/UITheme.cs` ✏️

- 新增 `internal static Color AlignGuide => WithAlpha(Accent, 0.85f);`
- **刻意做成派生属性而非 `Apply` 里赋值的字段**：完全由 `Accent` 决定 ⇒ 任何主题切换（含 RUIF 晚加载补应用、内嵌主题切换）自动同步，不可能「切了主题参考线还是旧颜色」；同时把 `0.85` 收在 Theme 令牌里，调用方不再散落 `new Color(...)`。

**Save 影响**：无。

### F10/F11 · `Languages/{English,ChineseSimplified}/Keyed/Archive.xml` ✏️ +4 键

| Key | English | 简体中文 |
|---|---|---|
| `PersonalChronicle.UI.Layout.ToggleSnap` | `Align snap` | `对齐吸附` |
| `PersonalChronicle.UI.Layout.SnapTip` | `Hold Shift while dragging a card to align it to the edges and centers of nearby cards. The guide marks the exact line you snapped to.` | `拖动设备卡时按住 Shift，即可对齐到附近卡片的边线与中心线；参考线会标出对齐到的确切位置。` |
| `PersonalChronicle.UI.Layout.SnapTipNoTarget` | `No other card on the canvas yet - there is nothing to align to.` | `画布上还没有其他卡片，无可对齐对象。` |
| `PersonalChronicle.UI.Layout.SnapGuideAligned` | `Aligned` | `已对齐` |

- 中英键集合**一一对应**（实测 EN 46 / ZH 46，`Compare-Object` 零差异）。
- 4 键均**被代码引用**（`SnapGuideAligned` 用于 MouseUp 命中吸附时把 `LastAction` 显示为「已对齐」，让玩家不必猜吸附是否生效）⇒ 无孤儿键。
- 均**不含占位符** ⇒ 无 `LOC-004` 占位符一致性风险。
- 逻辑判定源是 `bool SnapEnabled` 与查询结果，⛔ 不按翻译文本做判断（GOV-009）。

### F12 · `Tests/WorkshopCanvasAlignmentTests.cs` 🆕 27 条

阈值换算（3）· 同名线匹配（4）· 阈值门（3）· 优先级仲裁（4）· 自身排除（2）· 网格与原点（5）· 退化输入（6）

---

## 2. 【变更 B】设备卡 MouseUp 落档补齐（既有缺口修复，**独立条目**）

> 这条修的是**本次变更之前就存在**的缺陷，不是吸附的一部分。单独成条以便回溯。

### 缺陷

`MoveDetached` 只改内存（注释自陈「拖拽过程每帧都写存档是热路径脏写」），真正写档要等关窗时的 `MirrorDetachedToSave`（`MasterControlTabWindow.cs:142 PersistBeforeClose`）或 `UndoLast`。
⇒ **拖完不关窗就 Alt-Tab / 崩溃 ⇒ 位移丢失。**

对称性佐证：车间块侧**不缺**（`MirrorLayoutToSave` 每帧镜像，`AppShell.cs:1430`/`:1556`）⇒ 缺口只在设备卡侧。

### 为什么必须并入本次

吸附把交互从「随手一拖」变成「反复微调对齐」⇒ 丢位移概率从偶发升为常态。位置精度是吸附的核心价值，位置不落档 = **功能价值归零**。这是本次变更**放大**了既有缺陷，不是引入缺陷。

### 改动（F4/F7 · `WorkshopLayoutCanvas.Interaction.cs`）

| 位置 | 改动 |
|---|---|
| `HandleDeviceCardDrag` 签名 | +`List<PaletteEntry> palette`（吸附需枚举邻居） |
| MouseDown 分支 | +`state.AlignGuides.Clear()`（拖拽刚开始，清掉上一趟残线） |
| MouseDrag 分支 | 改走 `ResolveAndMoveDevice(...)` |
| MouseUp 分支 | 改走 `ResolveAndMoveDevice(...)`；返回值 `finalDesign` 交 `SaveDetached(id, finalDesign)`；`LastAction` 按是否命中显示「已对齐 / 已移动」 |

**为什么 MouseUp 也要重解吸附**（关键正确性）：原 MouseUp 用 `DesignAt(mousePosition) + grab` 绝对换算，MouseDrag 走 delta 累加 ⇒ 两者浮点上可能不同。若只在 Drag 吸附，**松手瞬间位置会「弹回」未吸附值** —— 玩家看到的是吸附位、存进档的是未吸附位。共用 `ResolveAndMoveDevice` ⇒ **落档值 ≡ 视觉值**。

**Save 影响**：**行为变更，schema 无变更。** 字段集（`stableId`/`x`/`y`）、语义（原点居中 / +X 右 / +Y 下 / 允许负 / 不取整）、`Scribe_Values.Look(ref X, "x", 0f)` 全部不变；`LayoutSchemaVersion` 保持 `1`；**无需 Migration**。
- 写入频率：1 次 / 拖拽结束（**非每帧**）⇒ 不违反 PERF-001
- 幂等：`SetDeviceLayoutEntry` 自带 `|Δ| < 0.01 → return` 守卫（`ChronicleGameComponent.DeviceLayout.cs:78`）⇒ 重复调用零脏写
- 已知量化边界：守卫阈值 0.01 设计单位 ⇒ 视觉值与存档值允许 <0.01 偏差被吞。属可接受边界，如实登记。

---

## 2.5 【变更 A·M3】车间块（Zone）Shift 对齐吸附

> M3 承接 M2。ARCH 复判预判为「🟠 中高」，风险点是**车间「不可见 200×120 锚点框 vs 设备卡并集 AABB」双源漂移**。

### 核心：把几何收成**单一真源**

| 新符号 | 文件 | 说明 |
|---|---|---|
| `IsZoneDeviceAlignable(stableId, excludeId)` | `Align.cs` | **单一排除真源**：空 id / 自身 / `IndexOfDetached >= 0`（已转独立卡）三条规则。**四个**调用点共用：① 渲染循环 ② `FindBlockAt` ③ 设备卡邻居投影 ④ 车间块 AABB 并集 |
| `TryGetZoneDeviceBounds(state, palette, zoneIndex, out bounds)` | `Align.cs` | 车间**在册设备卡 AABB 并集**（设计单位）。返回 `false` = 空车间 / 车间区不存在 / 下标越界 |
| `CanvasAlignRect.Union(other)` | `WorkshopCanvasAlignment.cs` | 并集运算（Domain 纯函数）。**返回新实例**以保住 `readonly struct` 语义 |
| `CanvasAlignRect.TryUnionAll(rects, out bounds)` | 同上 | 批量并集；空/null 返回 `false` |
| `CanvasAlignRect.HalfWidth / HalfHeight` | 同上 | 吸附求解需要的「中心 + 半尺寸」形态 |

### 关键正确性点

1. **⛔ 否决 200×120 锚点框做吸附目标**。锚点框**不绘制任何视觉**（画布不出现 zone 容器），而 2026-10-02 审计 P1-2 已把命中区从锚点框改成设备卡并集（锚点框会在空白处产生隐形热区、点一下就误拖车间）。若吸附按锚点框、命中按设备卡 ⇒ **「吸到了看不见、点得到但吸不上」**。本轮让两者共用 `TryGetZoneDeviceBounds`，从根上消除。
2. **`FindBlockAt` 只加 early-out，不换测试**：先用并集 AABB 转屏幕矩形做快速排除，**仍保留逐卡精确测试**。并集含卡间空隙，替换会重新引入 P1-2。
3. **空车间不产生候选线**：`TryGetZoneDeviceBounds` 返回 `false` ⇒ 既不命中、也不进邻居集。吸附到画布上不可见的目标 = 幽灵吸附。
4. **邻居用并集而非逐张卡**：拖车间块的语义是「整块对齐」，玩家参照的是块的外轮廓；逐张卡会让块内部卡边缘成为候选线，出现「块对齐到块内部某张卡」这种视觉上说不通的结果。
5. **Δ 可直接加到锚点**：设备卡并集随锚点**刚性平移**（`DeviceGridPos` 是锚点的固定偏移），故 `Resolve` 返回的平移量加到锚点即整块平移。单测 `Resolve_ZoneBlockDeltaAppliedToAnchor_MovesWholeBlock` 锁定该不变性。
6. **与设备卡侧的一处刻意差异**：`HandleDragAndDrop` 的 MouseUp **不再移动**（只清 `DragIndex`），因此不存在设备卡侧「松手弹回未吸附值」的问题，**无需**在 MouseUp 重解。
7. **落盘无缺口**：车间块侧由宿主每帧 `MirrorLayoutToSave()` 镜像（`AppShell.cs:1442` 与 `:1568`，紧随 `DrawCanvasBody`）⇒ 与设备卡侧不同，**不需要** M2 那种 MouseUp 补落档。已核实。
8. **参考线显隐门禁放宽**：`DrawAlignGuides` 的「拖拽在位」判据由「仅设备卡」改为「设备卡 **或** `state.DragIndex >= 0`」；两者都结束才清残留。

### 交互反馈

- 车间块 MouseUp 命中吸附时，`LastAction` 同样显示「已对齐」，否则显示「已移动」。
- `SnapTip` / `SnapTipNoTarget` 文案扩到覆盖车间块（**中英同步改**，非新增键）。

**Save 影响**：无（复用 `state.PlacedPos`，既有 `mfgLayout` 镜像链路未变）。

**单测 +11**（454 总数）：并集扩张/包含/顺序无关/批量与增量一致/空集合返回 false/半宽半高 + 车间块对齐/已对齐零漂移/刚体平移不变性/空车间无候选。

---

## 3. 【变更 C】ARC-013 物理切片（GATE-ARCH 处置，**独立条目**）

### 触发

`EXC-2026-011` 批准时主文件 1055 行，review trigger 为「主文件 >1400 行」。开工前实测 **1538 行**（超触发线 138 行，**超出 9.9%**），且超 `ARC-013` 渲染密集上限 1200 达 338 行（+28%）⇒ GATE-ARCH 判 **BLOCKED**。
（首次勘察报告的「1410 行」为**误测**，以实测 1538 为准。）

### 处置

用户确认走**全量切片 A+B+C+D**（Gate 建议的 A+B+C 只降到 1411，仍超 1200，且新 EXC 一出生就自带触发）。

| 新文件 | 内容 | 行数 |
|---|---|---|
| `WorkshopLayoutCanvas.Primitives.cs` | `LampColorOf` / `DrawGrid` / `Scale` / `OriginScreenPos` / `Measure` / `Translate` / `Translate1` / `Translate2` | 113 |
| `WorkshopLayoutCanvas.Placement.cs` | `IsPlaced` / `Place` / `Unplace` / `PlaceDevice` | 69 |
| `WorkshopLayoutCanvas.Interaction.cs` | 车间块拖拽 / `FindBlockAt` / 设备卡拖拽 / `MoveDetached` / `Snapshot` / `EstablishBaseline` / `CanUndo` / `UndoLast` / `ClearAll` | 290 |

**主文件 1538 → 1159 行**（含变更 A 的 +25 行）⇒ **低于 ARC-013 渲染密集上限 1200**。

### 3.1 纯搬移证据（GATE-ARCH M-3 固化）

> ⚠ 固化原因：执行脚本放在 `.codebuddy/tmp/`（`.gitignore:38` 忽略该目录）⇒ **不在版本控制内、下一轮无法重跑**。
> 故把断言清单与行位移剖面在此登记，使 Gate 证据随归档长期有效。

**证据 E-1 · 行位移剖面（用切片**前**的行号当锚点逐点对账）**

| 锚点（切片前 → 切片后） | 位移 |
|---|---|
| `:17` 类头注 → `:17` | +0 |
| `:72` `ShowGrid` → `:72` | +0 |
| `DrawCanvasBody :268` → `:278` | +10 |
| 独立设备层 `:408` → `:418` | +10 |
| 设备卡拖拽调用 `:425` → `:435` | +10 |
| 车间块拖拽调用 `:428` → `:438` | +10 |
| `FlashIndex` `:433` → `:445` | +12 |
| 空态提示 `:440` → `:452` | +12 |
| `DesignAt :471` → `:483` | +12 |
| `DeviceCardWidth :1036` → `:1048` | +12 |
| `BuildSavePreview :1089` → `:1101` | +12 |

断点**只有两个**，且恰好等于申报编辑：
- **+10** = `CanvasState` +7 行（3 字段 + 4 行铁律注释） + `Create()` +3 行
- **+12** = 再加 overlay 插入的 +2 行（`DrawAlignGuides` 调用 + 空行）

⇒ **切片区段内零插入、零删除、零重排。**

**证据 E-2 · 成员唯一性**

6 个 `WorkshopLayoutCanvas*` 文件合计 **71 条成员声明，重复 0 组**（脚本口径）。GATE-ARCH 用不同正则独立重扫（含补读主文件尾段 `:954-1159` 消除工具截断盲区）得 69 条、**同样 0 重复**；差异来自多行声明/属性的计数口径，**不构成矛盾**。

**证据 E-3 · 契约面抽样**

Primitives 8 / Placement 4 / Interaction 12 / Align 11 个成员**全部 `private|internal static`**；方案按行号点名的成员（`ShowGrid`、`DeviceCardWidth/Height`、`MoveDetached`、`FindBlockAt`、`Snapshot`、`UndoLast`、`ClearAll`…）**全部在位**；存档链路成员（`SetDetached` / `SaveDetached` / `LoadDetachedFromSave` / `MirrorDetachedToSave`）**仍全部留在主文件**；翻译键由 `Primitives.cs` 原样承接。⇒ **零契约改动可判。**

**证据 E-4 · 19 条边界断言（切片脚本执行时逐行核对字面量，全部通过）**

| 断言行 | 期望内容（`Trim()` 后） |
|---|---|
| 132 | （空行） |
| 133 | `internal static Color LampColorOf(string stateKey)` |
| 144 | `}` |
| 145 | （空行） |
| 1144 | `}` |
| 1145 | （空行） |
| 1146 | `// ---- 车间块拖拽 ----` |
| 1412 | `}` |
| 1413 | （空行） |
| 1432 | `}` |
| 1433 | （空行） |
| 1479 | `}` |
| 1480 | （空行） |
| 1496 | `}` |
| 1497 | （空行） |
| 1536 | `}` |
| 1537 | `}` |
| 1538 | `}` |
| （合计 19 条，覆盖 6 个搬移区间的首尾） | |

**证据 E-5 · 编译 0 错 0 警**

丢失成员会在调用点报 `CS0103`；重复成员会报 `CS0111`。编译通过本身即排除「丢成员」与「重成员」两类搬移事故。

**证据 E-6 · 测试工程纳入形态**

`Tests/PersonalChronicle.Tests.csproj` 为 SDK-style 且**无显式 `Compile Include`** ⇒ 默认 glob 自动纳入新测试文件（不存在「新文件没登记导致 27 条根本没跑」的风险）。

### 3.2 证据强度声明（如实登记，不得当作已闭合）

- **U-8（未闭合）**：`Source/PersonalChronicle/Manufacturing/UI/` **整个目录未被 git 跟踪** ⇒ **无法用 `git diff` 证明被搬移方法体内的语句逐字未变**。E-1 能证明「成员没丢没重、锚点区间零净变化」，E-3 能证明「签名没变」，但「同一方法体内被等价改写」正是纯搬移验证的经典盲区。GATE-ARCH 判为 **等级 B（次级/条件性）**，风险 Low（残留面已被 E-1/E-2/E-3/E-5 三重压缩到「单方法体内的等价改写」；此类改动会在下一次任何一次真实 diff 中立即暴露）。
- **O-1（已判可接受）**：3 个既有静态字段（`deviceCardDragId` / `deviceCardGrabOffset` / `UndoDetached`）随切片落在 `Interaction.cs`（约 `:110/:113/:205`），与 `08 册:125`「partial 字段｜字段集中留主文件」**字面不符**。判定可接受：三者仅被交互逻辑使用、同文件可读性更佳、**零契约/零生命周期变化**，且仍受 `EXC-2026-011` 同一会话生命周期约束。此为**切片结果，非新引入**。
- **建议（不阻断）**：`git add -A Source/PersonalChronicle/Manufacturing/UI Tests` 提交一次纳入基线，否则下一个 Change 会重演同样的取证困境。

**Save 影响**：无。

---

## 4. 未测项（如实登记，不得当作已验证）

| ID | 未测内容 | 原因 | 处置 |
|---|---|---|---|
| **U-1** | 游戏内实机：`Event.current.shift` 在 `MouseDrag` 的可靠性（IMGUI 合成事件） | 沙箱无法起游戏进程 | 实机验证；失真则回落为 MouseDown 锁存（需 ARCH/UI 复核） |
| **U-2** | 参考线 overlay 的裁剪正确性 / 无残线 / 视觉权重 | 同上 | 截图比对 |
| **U-3** | 窄视口下 `#lbar` 是否因新增开关而挤掉右侧 Save 预览 | 同上 | 已有 `bar.xMax - LbarPadX` 溢出保护 + `x + hintWidth < bar.xMax - LbarPadX - 260f` 预算判断兜底，但实际像素宽度未测 |
| **U-4** | 车间内卡**首次拖出**时不产生幽灵吸附线 | 同上 | 排除式去重已实现且单测覆盖 `skipIndex`，但「并入 `DetachedList` 同帧」的真实时序需实机 |
| **U-5** | 存读档往返：读档 → 画布 → Shift 拖拽吸附 → 存 → 重开位置正确 | 同上 | 落档链路本身未改（仅改写入时机），但端到端需实机 |
| **U-6** | 低于 50% 缩放时吸附降级的实际观感 | 同上 | 已由 `MaxSnapDesign` 封顶 + 单测锁定行为；观感需实机 |
| **U-7** | PERF 实测指标（每帧邻居投影耗时） | 无游戏进程 | 见 Gate Record 的定量分析 |
| **U-8** | 切片**方法体内语句**的逐字等价性（`git diff` 比对） | `Manufacturing/UI/` 整个目录未被 git 跟踪，无基线 | 等级 B 替代证据（E-1 行位移剖面 / E-2 成员唯一性 / E-3 契约面 / E-5 编译）；残留面已压缩到「单方法体内的等价改写」 |
| **U-9** | `MasterControlTabWindow.AppShell.cs` 精确行数 | GATE-ARCH 工具 1000 命中上限截断 | 已按「≥1332 行」下界登记为存量债 R-10，未据此做任何判定 |

## 5. 已知不在本期范围

- **R-2** 缩放**不锚定光标**（`HandleWheelZoom` 不以光标为锚点）会放大吸附体验问题 —— 改它会变更**所有**落点/命中/ghost 行为，回归面远超本需求。登记为独立 Change 候选。
- **R-5** 既有热路径问题（`Measure` 用 `Text.CalcSize`、`Translate1/2` 每次 `string.Format`、`PaletteIndexOf` 每帧 O(N)、`BuildSavePreview` 每帧重建）—— **不在本期修复**；吸附只做到**不加剧**（零分配）。
- **R-9** ~~`EXC-2026-011.md:5` 规则 ID 误引（静态可变状态应为 **ARC-008**，原文写 ARC-011）~~ → **本轮已更正**（GATE-ARCH 裁定 M-2 强制）：`ARC-011（静态可变状态）` → `ARC-008`；册号「02 册」→「08 册」；同时 `Review trigger` 的「>1400 行」标注为**已消解**并把阈值改为 >1200。
- **R-10** `MasterControlTabWindow.AppShell.cs` 体量 **≥1332 行**（实测下界）超 ARC-013 渲染密集上限 1200，且不在 `EXC-2026-011` Scope 内 ⇒ **未登记的存量违规**。本 Change 对其增量约 8–12 行且**未新增方法**，按存量债处理不阻断。已登记进 `EXC-2026-011.md` 的「关联存量债」节，建议后续独立 Change 做 partial 切片（同目录 `.Roster.cs` / `.Detail.cs` / `.Population.cs` 均 ≥1000 行）。
- **M3** ~~方案文档 §2.3 表中的 `AlignDragActive`~~ → 已在 A3 修正中删除该字段，方案文档 `.codebuddy/tmp/chg-mfg-align-001.md` 因被 gitignore 未同步（其内容已由本标注 §0/§1 取代）。
- **M4/M5** 方案文档的行数约束与枚举值数偏差已在本标注更正（标注为**唯一权威**，`docs/设计文档/架构/` 受版本控制）。
- **M3/M4/M5** 车间块吸附 / 框选多选批量对齐 / 网格吸附与强度档位 —— 已分期登记。

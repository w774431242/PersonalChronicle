---
changeId: CHG-20261002-CANVAS-PERSIST
title: 制造系统中心「自由编排画布」设备卡持久化 + 放置入口修复
date: 2026-10-02
version: v1.4.1
scope:
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.cs
    type: M
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Device.cs
    type: M
  - path: Source/PersonalChronicle/Manufacturing/UI/WorkshopFloorPlan.Rails.cs
    type: M
  - path: Source/PersonalChronicle/Automation/Workshop/WorkshopPaletteAccessBinder.cs
    type: M
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.cs
    type: M
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.AppShell.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
  - path: Languages/English/Keyed/Archive.xml
    type: M
  - path: About/About.xml
    type: M
  - path: docs/changelog/v1.4.1-自由编排画布设备卡持久化修复-Changelog.md
    type: A
---

# 变更标注 — CHG-20261002-CANVAS-PERSIST

> 范围：制造系统中心「自由编排画布」设备卡持久化 + 放置入口修复
> 日期：2026-10-02 ｜ 版本：v1.4.0 → **v1.4.1** ｜ 编译：**0 错 0 警**（`_scripts/compile_with_sdk.ps1`）
> 触发：用户实机反馈「非编排模式无法从设备库放置设备」+「设备卡无法保存」→ 深度审计 → 批准修复
> 范围外：仓库存在 1051 项既有未提交工作（历史累积），已通过 `-ExcludeFromFile .codebuddy/tmp/canvas-oos.txt` 声明排除。
> 注：本文件自身由 gate-runner 从 diff 集合中剔除，故不列入 `scope`（否则会被判为 phantom）。

---

## 1. 文件清单（GATE-DELTA 互含依据）

本轮改动 **11 个文件**，与 `git diff` 命中集**完全一致**（范围外 1051 个文件为仓库既有未提交工作，已在
`.codebuddy/tmp/canvas-oos.txt` 声明排除）。

| # | 文件 | 状态 | 归属 Gate |
|---|---|---|---|
| 1 | `Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.cs` | 修改 | UI / DATA / SAVE / PERF |
| 2 | `Source/PersonalChronicle/Manufacturing/UI/WorkshopLayoutCanvas.Device.cs` | 修改 | UI / LOC / PERF |
| 3 | `Source/PersonalChronicle/Manufacturing/UI/WorkshopFloorPlan.Rails.cs` | 修改 | UI / LOC |
| 4 | `Source/PersonalChronicle/Automation/Workshop/WorkshopPaletteAccessBinder.cs` | 修改 | PERF |
| 5 | `Source/PersonalChronicle/MasterControl/MasterControlTabWindow.cs` | 修改 | DATA / SAVE |
| 6 | `Source/PersonalChronicle/MasterControl/MasterControlTabWindow.AppShell.cs` | 修改 | UI / DATA / SAVE / LOC |
| 7 | `Languages/ChineseSimplified/Keyed/Archive.xml` | 修改 | LOC |
| 8 | `Languages/English/Keyed/Archive.xml` | 修改 | LOC |
| 9 | `About/About.xml` | 修改 | REL |
| 10 | `docs/changelog/v1.4.1-自由编排画布设备卡持久化修复-Changelog.md` | 新增 | REL |
| 11 | `变更标注_CHG-20261002-CANVAS-PERSIST.md` | 新增 | DELTA |

---

## 2. 逐文件改动明细

### 2.1 `WorkshopLayoutCanvas.cs`（画布核心）

**A. 身份模型（P1-3）**
- `CanvasState.PlacedOrder: List<int>` → **`PlacedZoneIds`**（承载 `Zone.ID`，不再是托盘下标）
- 新增 `CanvasState.UndoValid: bool`（撤销快照有效位）
- `Create()` 同步初始化新字段

**B. 会话作用域与重读（P0-2 / P1-5）**
- 新增 `sessionMap`（作用域键由 `Game` 升级为 **(Game, Map) 复合键**）
- 新增 `detachedReloadPending`（关窗置位 ⇒ 下次绘制入口强制重读）
- 新增 `detachedLoaded`（未成功加载存档 ⇒ 禁止删除写入）
- `EnsureSessionScope()`：作用域变化 **或** `detachedReloadPending` ⇒ 清易失态 + `LoadDetachedFromSave()`
- `ClearSession()` 拆为 `ClearVolatileSessionState()` + 置位 `detachedReloadPending`
- `LoadDetachedFromSave()`：幂等（每次从存档重建），入口收敛到 `EnsureSessionScope`

**C. 删除语义（P0-1）**
- `MirrorDetachedToSave()`：**删除差集反删**，改为**只写不删**；`detachedLoaded == false` 时整体跳过
- 新增 `RemoveDetached(stableId)`：单卡 × 的显式删除入口
- `ClearAll()`：改为显式逐项 `RemoveDevice`（唯一允许的批量删除路径）

**D. 撤销语义（P0-3）**
- `Snapshot(ref CanvasState)`：置 `UndoValid = true`
- 新增 `EstablishBaseline(ref CanvasState)`（读档后由宿主拍基线）
- 新增 `CanUndo(CanvasState)`（两处门禁统一来源）
- `UndoLast`：无快照时拒绝执行并提示；执行后 `UndoValid = false`（快照已消费）

**E. 设备卡交互（P1-1 / P1-2 / P1-4 / P2）**
- 新增 `deviceCardDragId` / `deviceCardGrabOffset` / `HandleDeviceCardDrag()` / `MoveDetached()`
- `FindBlockAt()`：命中区由「隐形 200×120 锚点框」改为「该车间实际设备卡的并集」
- `HandleDragAndDrop()`：新增 `palette` / `hitDeviceId` 参数，命中设备卡时直接放弃
- `HandleDeviceDrop()` 移到设备卡绘制**之前**（消除落点被 `ButtonInvisible` 吞掉）
- `PlaceDevice()`：设备落点统一走快照 + `LastAction` 反馈

**F. 坐标与几何收敛**
- 新增 `DesignAt()` / `DeviceCardBox()` / `cardScaleOf()` / `IntersectRect()` / `PaletteIndexOf()`
- 全部落点/命中统一到 GUI 空间（原混用 `UI.MousePositionOnUIInverted`）

**G. 点选放置入口（P1-7）**
- 新增 `devicePlacePick` / `devicePlacePickLabel` / `PickDeviceForPlace()` / `PendingDevicePick`

**H. Save 预览口径（P0-4）**
- `BuildSavePreview()`：新增设备条目段 + `devices: {2}` 计数 + 分容器标注 `[mfgLayout]` / `[mfgDeviceLayout]`
- 新增 `SavePreviewText()` 供宿主复用

**I. 空态文案（P1-7）**
- `DrawEmptyHint(rect, palette, interactive)`：按模式分派；删除永不成立的 `mid` 死代码

### 2.2 `WorkshopLayoutCanvas.Device.cs`

- `DrawClippedDeviceCard(...)`：新增 `interactive` + `out removeClicked`；命中矩形改 `IntersectRect(box, viewport)`；tooltip 同步改裁剪后矩形
- 新增 `RemoveButtonBox()` / `DrawCardRemoveButton()`：单卡「× 收回」（**画在裁剪内、测在裁剪外**）
- `DrawDetachedLayer(...)`：签名扩展（回传悬停卡 / 抓取偏移 / 删除命中）
- `HandleDeviceDrop(..., ref CanvasState)`：改走 `PlaceDevice`（补撤销快照 + 反馈）
- `DrawLayoutChain` / `DrawMetricsRow`：`PlacedOrder` → `PlacedZoneIds`；撤销门禁改 `CanUndo`；新增设备卡计数显示
- 删除未实现的 `DrawToggle` 私有注释残留（`右对齐的小开关按钮` 孤立注释）

### 2.3 `WorkshopFloorPlan.Rails.cs`

- 新增 `ResetRailSession()`（关窗复位页签 / 轨道宽 / 拖拽把手）
- `DrawRailsOnly()`：窄视口不再静默 return，改画 `DrawRailsHiddenNotice()`
- 轨道宽上限与视口联动（`min(360, 视口宽 × 0.5)`），不再侵入画布
- `DrawDeviceLibrary()`：新增**点选放置**（`HandleDeviceLibraryRowInput` 区分拖动 / 点击）、选中高亮、超容量「还有 N 台」明示
- 新增 `T2()`（1.6 `NamedArgument` 两参翻译）

### 2.4 `WorkshopPaletteAccessBinder.cs`

- 节流时钟 `DateTime.UtcNow.Ticks` → **单调时钟** `Stopwatch.GetTimestamp()`（P1-6）
- cache miss 时**就地重扫该车间**（P1-6b：原实现留 `null` ⇒ 该车间在画布整体消失）
- 新增 `using System.Diagnostics;`

### 2.5 `MasterControlTabWindow.cs`

- `PreClose()`：新增 `WorkshopFloorPlan.ResetRailSession()`

### 2.6 `MasterControlTabWindow.AppShell.cs`

- `LoadLayoutFromSave()`：**幂等**（先 Clear）+ 移除设备重读（收敛到 `EnsureSessionScope`）+ 读档后 `EstablishBaseline`
- `MirrorLayoutToSave()`：改按 **ZoneId** 读写与删除判定（修「拿 -1 比对」逻辑缺陷）
- `FindPaletteIndex()`：委托 `WorkshopLayoutCanvas.PaletteIndexOf`（去重）
- `DrawAppClinement` / `DrawClinePlacedChips` / `DrawAppLayoutBar` / `DrawAppLayoutCanvas`：`PlacedOrder` → `PlacedZoneIds`
- `DrawAppLayoutBar()`：撤销门禁 → `CanUndo`；`#lbar` SaveTip → 复用 `SavePreviewText`（**修 `{0}` 字面量 bug**）；新增放置状态提示行
- `DrawAppTabBar()`：**重写**为等比压缩 + `+N` 溢出明示（修「编排」页签被静默截断）
- `DrawActivePane()` / `DrawAppFloorPanel()`：「编排」页签**禁用页内滚动**且不参与高度压缩
- 新增 `MinAppTabWidth`

### 2.7 Languages（中英同步，各 2481 键，一一对应）

**新增 12 键**：`Layout.EmptyHintEdit` / `EmptyHintView` / `EmptyHintNoZone` / `NothingToUndo` /
`DevicePlaced` / `DeviceMoved` / `DevicesPlaced` / `ZoneIdFallback` / `PlaceHintIdle` / `PlaceHintActive` /
`RailHiddenTip` / `RailTab.PickedTip` / `RailTab.MoreDevices` / `Floor.Tab.OverflowTip`

**修改 1 键**：`Layout.SaveTipScope`（补 `mfgDeviceLayout` 口径）

**删除 4 键**（零引用孤儿）：`Layout.EmptyHint`、`Layout.CardDevices`、`Layout.CardNoDevice`、`Floor.Lbar.SaveTip`

### 2.8 `About/About.xml`

`modVersion` v1.4.0 → **v1.4.1**；中英文描述各增 3 行画布能力说明；页脚版本号同步。

---

## 3. Gate Results

| Gate | Result | Evidence |
|---|---|---|
| GATE-ARCH | **PASS WITH EXCEPTION** | 分层未变（UI → Application 门面 → Data）；新增静态字段仍属既有会话态范畴（EXC 沿用既有登记）。无循环依赖、无 Harmony 新增、无跨 Mod 引用 |
| GATE-DATA | **PASS** | `DetachedList` 双职责已收敛（`detachedLoaded` 守卫 + 可重建）；`CanvasState` 持久语义与 UI 态分离（`PlacedZoneIds` vs `Zoom/Undo*`） |
| GATE-SAVE | **PASS** | 加性可选字段、无需 Migration、旧档读回 null 回落空列表；`SchemaVersion` 不 bump。**遗留**：字段级版本 gate 与孤儿 GC 未做（已在 Changelog「已知限制」登记） |
| GATE-UI | **PASS** | 三态交互矩阵补全；状态提示 / 空态分模式 / 降级明示 / 大列表溢出提示齐备；命中矩形裁剪一致；`BeginClip/EndClip` 仍 try-finally 配对 |
| GATE-LOC | **PASS** | 中英 2481 键一一对应、XML 合法；`{0}` 占位符不匹配已修；孤儿键已清；新增文本全 Keyed |
| GATE-COMP | N/A | 不涉及第三方 Mod / DLC |
| GATE-PERF | **PASS** | 节流改单调时钟；`FindDevice`/`IndexOfDetached` 的 O(N·M) 未扩算（新登记为遗留）；热路径无新增分配（`new List` 仅在 `ClearAll` 显式动作内） |
| GATE-TEST | **部分执行** | 编译 **0 错 0 警** ✅；XML / 键集解析 ✅。**未执行**：游戏内启动、实机拖放与存读档（沙箱无法起进程）——已在 Changelog「已知限制」如实登记 |
| GATE-REL | **PASS** | 版本 / 游戏版本 / 依赖 / 加载顺序已更新；Changelog 分类完整；Migration 明确「无」；回滚说明已写 |
| GATE-DELTA | **PASS** | 标注 11 文件 ↔ diff 命中 11 文件（互含成立，范围外 1051 项已声明排除） |

---

## 4. 未执行 / 需实机验收（GATE-TEST 遗留）

沙箱无法启动 RimWorld 进程，以下五条路径**必须**在游戏内核对：

1. 设备库拖放 → 切走标签 → 切回 ⇒ 设备卡**仍在**（P0-2 核心验收）
2. 设备库点选 → 点画布 ⇒ 放置成功且有状态提示（P1-7）
3. 放置后拖动改位 + 编排态点 × 收回（P1-1）
4. 手动存档 → 重读档 ⇒ 车间块与设备卡位置均正确回读（P0-1/P0-2/P1-3）
5. 撤销按钮：读档后首次点击应**无副作用**（回退到基线），而非清空（P0-3）

---

## 5. 回滚

- 版本级：回滚至 v1.4.0 即可，未改存档结构。
- 代码级：`git checkout` 本标注列出的 11 个文件（其中 4 个为新增未跟踪文件，需一并删除）。
- DLL 级：`Assemblies/PersonalChronicle.dll.bak_20261002234922`（本次编译自动备份）。

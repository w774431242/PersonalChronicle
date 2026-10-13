# BUG-2026-0001 — 编排画布：设备卡拖不动 + 查看态仍可修改布局

- **Status:** FIXED（代码完成，**待实机验证**）
- **Severity:** S2 Major（核心交互不可用 + 只读契约被破坏） · **Priority:** P1
- **Component:** Manufacturing/UI · `WorkshopLayoutCanvas` 画布事件流
- **Reported:** 2026-10-03（用户实机；原文「无法拖动，且在查看模式中还能拖动编排，这是大问题」）
- **Found during:** CHG-MFG-ALIGN-001（M3 交付后用户实机验收）

## 1. Symptom（事实）

- **S-1** 设备卡拖不动，按下卡片无任何反应
- **S-2** 查看模式下仍能修改编排布局（查看态应为只读观测面）

## 2. Reproduction

环境：RimWorld 1.6 + PersonalChronicle，`Mods\PersonalChronicleVer0.1`（hash 已校验为本仓构建产物）。

- **S-1**：切到「编排」（截图中 `#lbar` 存在 ⇒ 确为编辑态）⇒ 按住任意设备卡横拖 ⇒ 卡片不动
- **S-2**：切到「查看」⇒ 从设备库拖设备到画布 / 点选设备行后点画布 ⇒ 设备被放置
- **复现状态**：`REPRODUCING`（用户游戏内复现；agent 侧无游戏进程，代码层证据链已闭合）

## 3. Evidence

| ID | 事实 | 位置 |
|---|---|---|
| E-001 | 设备卡体点击用 `Widgets.ButtonInvisible(hitBox)`，该控件按下时消费 `MouseDown` | `Device.cs:414` |
| E-002 | 卡片绘制+命中循环在**交互处理之前**：`:396 DrawClippedDeviceCard` → 之后才到 `:439 HandleDeviceCardDrag` | `WorkshopLayoutCanvas.cs:396` vs `:439` |
| E-003 | 拖拽起点判定要求 `current.type == EventType.MouseDown`；事件已被 `Use()` 则该分支永不成立 | `Interaction.cs:129` |
| E-004 | 若释放 MouseDown，`HandleDragAndDrop` 在**悬停着卡片**时仍会起拖（`FindBlockAt` 只排除被悬停的**那一张**卡，同块其余卡仍可命中） | `Interaction.cs` + `FindBlockAt` |
| E-005 | `HandleDeviceDrop` 调用点**不在** `if (interactive)` 内（该门禁在 `:434` 之后） | `WorkshopLayoutCanvas.cs:328` |
| E-006 | 「点选设备行 → 点画布」落点路径同样**不在** `interactive` 内，注释自称「两态都可用」 | `:474-483` / `:837` |
| E-007 | 同方法内契约自相矛盾：`:281`「false = 查看态（只读）」、`:459`「仅编排态：查看态是只读观测面」 | `WorkshopLayoutCanvas.cs` |
| E-008 | `HandleDeviceDrop` 依赖静态 `deviceDrag`；查看态跳过而不清理 ⇒ 编辑态拖拽中途切查看态会把 `deviceDrag` 卡在 Active | `Device.cs:44/62` |
## 4. Hypotheses（逐一处置，R7 保留排除记录）

| ID | 假设 | 调查方法 | 结果 | 状态 |
|---|---|---|---|---|
| H-1 | `interactive` 映射反了 | 查 `AppShell.cs:488 DrawAppLayoutCanvas(canvasArea, floorEditMode)` 与 `:1431` 文档 | 映射**正确**；截图内 `#lbar`（仅编辑态绘制，`AppShell.cs:448`）存在 ⇒ 编辑态 ⇒ `interactive == true` | **ELIMINATED** |
| H-2 | `hoverDeviceId` 取不到 | 读 `:387-394` | 命中只用 `Mouse.IsOver(hitBox)`，**不消费事件**，逻辑正确 | **ELIMINATED** |
| H-3 | M2/M3 改动破坏了拖拽 | 逐条比对改动范围 | M2 只改 `HandleDeviceCardDrag` 签名与 MouseDown/Drag/Up **分支体**；M3 只改 `HandleDragAndDrop` 分支体 + `FindBlockAt`。**均未触及** `Device.cs:409/414` 与 `:396`→`:439` 的事件顺序 | **ELIMINATED** |
| **H-4** | 卡片 `ButtonInvisible` 在绘制循环内消费 `MouseDown`，拖拽处理在其后 ⇒ 拖拽起点永不成立 | E-001+E-002+E-003 链式推导；与代码内 2026-10-02 审计注释互证 | **CONFIRMED** | **CONFIRMED** |
| **H-5** | 落点路径缺 `interactive` 门禁 | E-005+E-006+E-007 | **CONFIRMED** | **CONFIRMED** |

## 5. Root Cause（五层分解）

### S-1 设备卡拖不动

- **Symptom**：按卡片无反应
- **Immediate**：`if (current.type == EventType.MouseDown …)` 恒不成立
- **Technical**：`Widgets.ButtonInvisible` 按下时 `Event.Use()`；卡片绘制+命中循环排在交互处理**之前**，事件到达拖拽处理时已 Used
- **Root**：**卡片「点击」与「拖拽」争用同一个 IMGUI 按下事件，实现上靠「事件被抢走」被动达成互斥，而非显式判定。** 2026-10-02 审计已识别同一缺陷族的 `MouseUp` 变体并把 `HandleDeviceDrop` 前置（代码内注释明确记录），但 **`MouseDown` 变体未修**。`DrawCanvasBody:436` 注释「命中设备卡就拖设备，没命中才考虑整块车间」这一意图当时**仅靠事件被抢走被动成立**，从未被显式编码
- **Process**：修了一半（MouseUp 路径）而未系统检查 MouseDown 对称路径

> 附带效应：卡片总抢到 MouseDown ⇒ 车间块只能在**卡片之间的空隙**起拖 ⇒ 用户观感是「整车间也拖不动」。

### S-2 查看态仍可修改布局

- **Symptom**：查看模式下从设备库拖放/点选仍能放置设备
- **Immediate**：`PlaceDevice` 在 `interactive == false` 时仍执行
- **Technical**：`HandleDeviceDrop`（E-005）与 `devicePlacePick` 落点分支（E-006）都在 `if (interactive)` 之外
- **Root**：**「查看态只读」只被局部执行** —— 门禁加在拖拽与托盘落点上，漏了设备库落点两条入口；且 `:474` 把「两态都可用」写成设计意图，与 `:281`/`:459` 直接矛盾。2026-10-02 审计 P1-7 为解决「拖放不可发现」补了降级入口，但**没把降级入口纳入只读门禁**

## 6. 诚实声明

- **git 无法提供基线**：`Source/PersonalChronicle/Manufacturing/UI/` 未被 git 跟踪（见 CHG-MFG-ALIGN-001 的 U-8），故「pre-existing」**不能用 diff 证明**。依据：① 本轮改动范围逐条枚举且不涉及事件顺序与 `Device.cs:409/414`；② 代码内 2026-10-02 审计注释独立描述了同缺陷族的 MouseUp 变体。置信度**高**，非 VCS 级铁证。
- **R3 状态**：代码已改，但**未执行原始复现验证** ⇒ 只能称 FIXED，**不得称 VERIFIED/CLOSED**。
- 用户报告的「无法拖动」是否**完全**由 H-4 解释（是否叠加其它因素）——`UNVERIFIED`，需实机确认。
## 7. Fix Record

| # | 动作 | 落点 | 状态 |
|---|---|---|---|
| F-1 | 编排态**不消费**卡体按下（`interactive == true` 时不调 `ButtonInvisible(hitBox)`） | `Device.cs` | DONE |
| F-2 | `HandleDragAndDrop` **显式**拒绝在悬停设备卡时起拖（把 `:436` 注释意图真正编码） | `Interaction.cs` | DONE |
| F-3 | MouseUp 且指针移动 ≤ 4px 判为「点击」⇒ 上报 `clickedDeviceStableId` 开详情 | `Interaction.cs` + `CanvasState.CardPressPos` | DONE |
| F-4 | `HandleDeviceDrop` 纳入 `interactive` 门禁；查看态改调 `CancelDeviceDrag()` 清静态态 | `WorkshopLayoutCanvas.cs` | DONE |
| F-5 | `devicePlacePick` 落点纳入门禁；查看态清陈旧点选 | `WorkshopLayoutCanvas.cs` | DONE |
| F-6 | 订正 `:474` 与 `:837` 的「两态都可用」注释 | `WorkshopLayoutCanvas.cs` | DONE |

**明确未改**：缩放锚定、卡位数学、持久化 schema、吸附算法（与本 Bug 无关）。

**新引入具名常量**：`CardClickSlopSq = 16f`（4px²），与 `WorkshopFloorPlan.Rails.RailClickSlopPx = 4f` 同值同义，避免「同一次点击在设备库判为选中、在画布判为拖拽」的手感分裂。

**顺带修正的自造回归**：初版修复因注释过长把主文件推到 **1203 行**（超 ARC-013 渲染密集上限 1200，即本 Change 刚认证的阈值）⇒ 压缩注释至 **1193 行**。

## 8. Verification

| ID | 项 | 结果 |
|---|---|---|
| V-1 | 编译 0 错 0 警（`--no-incremental` 强制全量） | **PASS**（0 warnings / 0 errors） |
| V-2 | 画布域单测（`WorkshopCanvasAlignmentTests`）38 条 | **PASS**（38/38） |
| V-3 | 约束脚本 `verify-align.ps1` | **PASS**（0 FAIL，含主文件 1193 < 1200） |
| V-4 | 全量单测 | **1 失败，但非本变更**：`DayPlusNight_UnionWouldStarveSleep_MustNeverBeUsedForOneCrew` 属 `Tests\WorkshopTimetableHoursTests.cs`（排班工时域），该文件修改时间 11:54:19 早于本轮最早编辑且非本会话所改 ⇒ 判定为**并行会话在制品**，未触碰 |
| V-5 | **实机**：编排态拖设备卡 | **NOT RUN**（需用户） |
| V-6 | **实机**：编排态拖车间块（空隙可拖 / 按卡片不误拖整块） | **NOT RUN**（需用户） |
| V-7 | **实机**：编排态点卡仍能开详情 | **NOT RUN**（需用户） |
| V-8 | **实机**：查看态从设备库拖放/点选**不再**放置 | **NOT RUN**（需用户） |
| V-9 | **实机**：编辑态拖拽中途切查看态，`deviceDrag` 不卡死 | **NOT RUN**（需用户） |
| V-10 | 回归：编排页签小画布放置仍可用 | **NOT RUN**（需用户） |

## 9. Regression

- 相邻功能：设备库拖放（F-4 收进编排态 ⇒ **查看态行为有意改变**，属修复目标而非退化）；编排页签小画布（`WorkshopLayoutCanvas.Draw` 恒 `interactive: true`，不受本次门禁影响）
- 共享代码路径：`DrawClippedDeviceCard` 的 `interactive` 语义由「是否画 ×」扩展为「是否消费卡体按下」，两处同源一致
- 边界：卡片过小（`RemoveButtonBox` 返回零宽）时 `×` 不存在，按下仍走拖拽/点击判定
- 旧存档兼容：未触碰 schema

## 10. Timeline

| 时间 | 事件 |
|---|---|
| 2026-10-03 | CHG-MFG-ALIGN-001 M3 交付后，用户实机验收报告 S-1 / S-2 |
| 2026-10-03 | 建档；H-1/H-2/H-3 排除，H-4/H-5 确认；产出 Fix Plan F-1~F-6 |
| 2026-10-03 | F-1~F-6 实施；编译 0/0；画布域 38/38；约束 0 FAIL；主文件回归 1193 行 |
| 待办 | 用户实机验证 V-5~V-10 ⇒ 通过后方可 CLOSED |
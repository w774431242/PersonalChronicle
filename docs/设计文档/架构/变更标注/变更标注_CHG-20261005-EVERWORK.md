---
change_id: CHG-20261005-EVERWORK
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopDutyCoordinator.cs
    type: M
    symbols: WorkshopDutyCoordinator.SyncVanillaTimetables, WorkshopDutyCoordinator.ApplyWorkHoursToCrew
    lines: 110-118, 296-320
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkEnable.cs
    type: M
    symbols: MapComponent_WorkshopManager.EnsureWorkSettingsInitialized
    lines: 279-316
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.Refresh
    lines: 322-329
---

# 变更标注 · CHG-20261005-EVERWORK

## 背景（玩家诉求，2026-10-05）

> 「还是这个【接管原版工作】问题……点击『接管原版工作』**就能实现**。**审计开关后的逻辑区别**。」

即：玩家实测「点一次接管原版工作 → 两班倒人员立刻正常上下班」，要求审计**开关之后的逻辑差异**。

## 审计（逐条列出点击后的全部副作用）

点「接管原版工作」（`WorkshopManageView.Takeover.cs:46-61`）只做两件事：

1. `manager.TakeOverVanillaWork(zone, out reason)`（`WorkTakeover.cs:147-252`）；
2. `Refresh()`（仅重建快照）。

`TakeOverVanillaWork` 内部全部动作：

| 动作 | 影响面 |
|---|---|
| `settings.EnableAndInitializeIfNotAlreadyInitialized()`（`:220`） | ★ **初始化 workSettings** |
| `ledger.RecordTakeover(...)`（`:234`） | 只写台账 |
| `settings.SetPriority(type, 0)`（`:236`） | 取消勾选车间类工作 |

⇒ 唯一能改变"能不能干活"的是 ★。

**对比 PC 另外两条会初始化的路径（覆盖不全）**：

| 路径 | 触发 | 覆盖范围 |
|---|---|---|
| `SyncVanillaWorkSettings` → `EnableWorkTypes`（`WorkEnable.cs:304`） | 仅「排班/名册变更」事件 | 全体，但需事件发生 |
| `SyncWorkStationForZone`（`WorkEnable.cs:431-475`，经 `SyncWorkStationIfDirty`） | 设备/工单**置脏**时 | ⚠ 只处理 `dutyPlan` 里**当前在班**的人（`:453` 的 `ContainsKey` 过滤） |
| **接管按钮** | 玩家点击 | **无条件 · 全体成员** |

## 根因（原版闸门 + 覆盖缺口）

原版 `JobGiver_Work.GetPriority` 的**第一道**闸门：

```csharp
if (pawn.workSettings == null || !pawn.workSettings.EverWork) return 0f;
```

（该闸门逐字记在本工程 `Capture/Patch_JobGiver_Work_GetPriority.cs:43` 的注释里。）

⇒ `EverWork == false` 的成员**即使作息表写着「工作」也拿不到工作 Job** ⇒ 表现为「**当班却不动**」。
⇒ 而 PC 的自动路径在原场景（读档后不改任何配置、设备数也没变）**都不会跑到**那两条覆盖不全的路径
⇒ 直到玩家手动点一次「接管原版工作」（唯一对全体无条件初始化）才恢复正常。

**这解释了"两班倒"的假象**：不是班次没写（作息表可能已经正确），而是**人根本没被原版派活**。

## 修复（三处，单一实现 + 全覆盖）

1. `MapComponent_WorkshopManager.WorkEnable.cs` 新增 **`EnsureWorkSettingsInitialized(zone)`**：
   对 `AllWorkshopMemberPawns(zone)`（人员名单 ∪ 班组编制，**与接管/隐藏/作息表同一份成员口径**）
   逐个 `workSettings?.EnableAndInitializeIfNotAlreadyInitialized()`。**只初始化、不写档位**（幂等）。
2. `WorkshopDutyCoordinator.SyncVanillaTimetables` **入口处**调用它（**在任何策略分支之前**）：
   - NaturalRest 策略会提前 return，但人仍要能工作 ⇒ 不能挂在"是否写表"的下游；
   - `multiShiftCrewsOnly` 会跳过单班次班组 ⇒ 不能挂在写入循环里；
   - 本方法是所有作息表同步路径（开面板 / 改配置 / 跨天重放）的**汇聚点** ⇒ 一处覆盖全部入口。
3. `WorkshopManageView.Refresh()` 亦调用一次：**打开面板即自愈**（玩家读档后开一次面板就恢复）。

## 纪律合规

- **不在 tick、不在绘制路径**：三处调用点均为事件驱动（开面板 / 配置变更 / 跨天重放）；
- **不写档位**：`EnableAndInitializeIfNotAlreadyInitialized()` 对已初始化者是空操作，
  不产生任何 `SetPriority` 语义的改写 ⇒ 不触碰 EXC-2026-013 / EXC-2026-015 的授权边界；
- **成员口径唯一**：复用 `AllWorkshopMemberPawns`，不新增第二套"谁在车间"的判定（GOV-005）。

## 存档影响：**无**

不新增/修改任何持久字段，不改 Scribe 键，无 Migration。`workSettings` 是原版运行时数据。

## 验证

1. 重启后**不做任何配置改动**，直接开一次车间面板 ⇒ 两班倒人员应在到点时正常去车间上班
   （不再需要"点一次接管"来触发）；
2. 原版【工作】面板里那些成员的工作列应正常显示（初始化后可见档位）；
3. 若仍未恢复：检查原版【工作】面板左上角是否处于「手动优先级」模式
   （简单模式下引擎不读档位，是另一条独立链，见设置页「开启手动优先级」）。

## 残留风险

1. **本修复只保证"能拿到 Job"，不改变排班语义**：若某人的作息表确实没按他的班次写入
   （例如「固定班」策略下一个班组被指派了多个班次，只会绑定其中 1 个——
   见 CHG-20261005 诊断），那仍需按策略语义修正配置（拆班组 / 改用当日轮值）；
2. **未做 tick 兜底**（刻意）：纪律禁止在 tick 写工作设置；因此若玩家从不打开面板、
   也不触发任何事件，初始化仍不会发生 —— 但任何一次开面板/配置变更/跨天重放都会补上，
   实际使用中覆盖充分；
3. `EnableAndInitializeIfNotAlreadyInitialized()` 会把原版默认档位写入（不是 PC 的档位），
   对**从未配置过工作**的殖民者，这等同于"恢复原版默认"，属预期行为。

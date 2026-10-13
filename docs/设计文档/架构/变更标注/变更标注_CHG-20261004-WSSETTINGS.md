# 变更标注 · CHG-20261004-WSSETTINGS

**Change ID**：CHG-20261004-WSSETTINGS（车间策略植入 Mod 设置 + 在班自动回写）
**日期**：2026-10-04
**宿主**：RimWorld 1.6.4871（rev590）
**关联**：CHG-20261004-SHIFTGATE（离班清 0）、CHG-20261004-IDLE（摸鱼）、EXC-2026-013（值勤不提升）、EXC-2026-016（接管红线）

---

## 一、取证结论（三条，其中两条**推翻了玩家与我的假设**）

### 1. 「接管原版工作」按钮**当前恒为空操作**

* `WorkTakeover.DeliveredByWorkshopPost` 只认 `implementedWorkTypeDefNames`（**刻意不回退** `deliversOwnService`）。
* `Defs/WorkshopPosts.xml` 全部岗位的 `<implementedWorkTypeDefNames/>` **为空**
  ⇒ `IsTakeoverAllowed` 恒 false ⇒ `candidates.Count == 0` ⇒ `outReason = "no_takeoverable_work"`
  ⇒ UI 即玩家截图的「已处理 0 项 · 无可接管的工作」。
* ⇒ **把它做成 Mod 设置开关**本身**解决不了任何事**（开关也会是空转）。要让它可用，
  必须先给 `implementedWorkTypeDefNames` 填数据，或改闸门判据 —— 而
  `EXC-2026-016:81` **明确拒绝**了"全局开关式接管"（理由：作用域过大、粒度不可控），
  可做的前提是先修订该裁决。**待玩家决策，本批未做。**

### 2. 「每天要手动点一次」的真凶：**不对称的写链**

* `ApplyOffShiftWorkTypes`（`OffShift.cs`）在 `MapComponentTick` 里**每游戏小时**把
  车间类工作类型写 0（离班）。
* 而"在班回写"此前**只挂在管理面板**（`SyncVanillaWorkSettings` 的 4 个调用点：
  面板 Refresh / OnRosterChanged / 面板按钮 / SyncWorkStationIfDirty）——
  **没有任何 tick 触发面**。
* ⇒ 班次一切换，档位停在 0（连值勤 Job 都排不出来），**直到玩家开一次面板**。
  ⇒ 这就是玩家感受的"每天/每班都要手动点一下"。**不是设置被重置**
  （接管台账 `Scribe_Collections.Look(ref workTakeover, …)` 持久化、无 SchemaVersion 门控；
  `PlaySettings.useWorkPriorities` 是原版持久字段，PC 全库只有置 true、无置 false）。

### 3. 废弃 Mod 设置：**已经清干净**（无需动作）

`takeoverWorkTab` 与 4 个 `workTabShow*` 的**字段与 Scribe 已整体删除**
（CHG-20261004-WORKTAB-ORIGINAL 的产物），全库唯一残留是 `ChronicleSettings.cs:35-47`
的**注释**（准确的删除清单，非僵尸项）。**不存在"字段还在但没人读"的项。**

---

## 二、实现

### A. 在班自动回写（核心，解决"每天手点"）

`MapComponent_WorkshopManager.WorkEnable.cs` 新增 `RestoreOnShiftWorkTypes()`，
由 `MapComponentTick` 紧跟 `ApplyOffShiftWorkTypes()` 之后调用（两者共用
`GenLocalDate.HourOfDay` 变化节拍，语义上是一对）。

* 只对**当前小时应到岗**的成员（`EnsureDutyPlan` 的 `dutyPlan`）执行；
* **只补仍为 0 的类型** ⇒ 幂等，且**不覆盖玩家在面板里手调的档位**
  （不能照搬 `EnableWorkTypes` 的"0/2/3/4 统一收敛到最高档"，那会每小时覆盖手调值）；
* 作用范围 = `CollectWorkSyncTypes(zone)`（车间类型映射）∪ 值勤类型；
* 简单模式下补 `DefaultPriority`（与 `EnableWorkTypes` 同口径）。

### B. 四个 Mod 设置（全部**默认保持既有行为**）

| 字段 | 键 | 默认 | 语义 |
|---|---|---|---|
| `AutoRestoreOnShift` | `autoRestoreOnShift` | **true** | 在班自动回写（A 的实现，关闭 = 旧的手点行为） |
| `IdleSplitEnabled` | `idleSplitEnabled` | true | 冗余员工摸鱼切分；关闭 ⇒ 全员写 Work 时段 |
| `AutoDetectWorkshopType` | `autoDetectWorkshopType` | true | 自动判定车间类型；关闭 ⇒ 必须手动指定 |
| `ReplayTimetablesOnDayChange` | `replayTimetablesOnDayChange` | true | 跨天重放作息表；关闭 ⇒ 不每天覆写在编成员手排 |

设置页新增「车间策略」分组（`DrawSettings` 里 `EnableOffShiftCancel` 之后），
中英 Keyed 各 6 键（含 `WorkshopSection` 分组标题与 `AutoRestoreOnShiftDesc` 说明）。

## files

```yaml
files:
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
    symbols: ChronicleSettings.AutoRestoreOnShift, ChronicleSettings.IdleSplitEnabled, ChronicleSettings.AutoDetectWorkshopType, ChronicleSettings.ReplayTimetablesOnDayChange, ChronicleSettings.ExposeData, ChronicleSettings.DoSettingsWindowContents
    lines: 60-83, 145-149, 190-208
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkEnable.cs
    type: A
    symbols: MapComponent_WorkshopManager.RestoreOnShiftWorkTypes, MapComponent_WorkshopManager.lastRestoreHour
    lines: 384-477
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.cs
    type: M
    symbols: MapComponent_WorkshopManager.MapComponentTick
    lines: 261-266
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.Schedule.cs
    type: M
    symbols: MapComponent_WorkshopManager.ReplayTimetablesOnDayChange
    lines: 101-108
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.Refresh
    lines: 295-302
  - path: Source/PersonalChronicle/Automation/WorkshopDutyCoordinator.cs
    type: M
    symbols: WorkshopDutyCoordinator.ApplyWorkHoursToCrew
    lines: 227-233
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.Settings.WorkshopSection, PersonalChronicle.Settings.AutoRestoreOnShift, PersonalChronicle.Settings.AutoRestoreOnShiftDesc, PersonalChronicle.Settings.IdleSplitEnabled, PersonalChronicle.Settings.AutoDetectWorkshopType, PersonalChronicle.Settings.ReplayTimetablesOnDayChange
    lines: 3692-3698
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.Settings.WorkshopSection, PersonalChronicle.Settings.AutoRestoreOnShift, PersonalChronicle.Settings.AutoRestoreOnShiftDesc, PersonalChronicle.Settings.IdleSplitEnabled, PersonalChronicle.Settings.AutoDetectWorkshopType, PersonalChronicle.Settings.ReplayTimetablesOnDayChange
    lines: 同中文
  # ---- 续修（2026-10-04 同日晚）：车间作息策略全局默认 ----
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
    symbols: ChronicleSettings.WorkshopDefaultRestPolicy
    lines: 84-93, 153-155, 214-243
  - path: Source/PersonalChronicle/Automation/WorkshopDutyCoordinator.cs
    type: M
    symbols: WorkshopDutyCoordinator.PolicyOf
    lines: 54-71
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.Settings.WorkshopRestPolicyDesc
    lines: 3699
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.Settings.WorkshopRestPolicyDesc
    lines: 同中文
```

## 三、续修：车间作息策略全局默认（`WorkshopDefaultRestPolicy`）

**动机**（玩家 2026-10-04：「按你推荐进行修复，但不能偏移预期」）：车间面板里"作息策略"
是**逐车间**字段（`Zone_Workshop.RestPolicyDefName`），新车间只能吃 Def 缺省；
而三档各有明确取舍（`NaturalRest` = 完全不写作息表 / `RotateDaily` = 按天翻转睡眠 /
`FixedPerShift` = 稳定），玩家需要一个**一次配置**的入口。

**实现**：`PolicyOf` 的降级链插入一层 ——

```
zone.RestPolicyDefName  →  [新] Mod 设置 WorkshopDefaultRestPolicy  →  Def 缺省  →  硬兜底
```

* 设置页在「车间策略」分组末尾加**三选一**（复用车间面板既有的
  `PersonalChronicle.Automation.RestPolicy.*.Label` 键 ⇒ 两处文案与口径**永不漂移**）；
  选 `RotateDaily` 时同屏显示既有的 `RestPolicy.Rotate.Warning`（披露要求）。
* **零偏移保证**：字段默认值与 Scribe 默认值均为 `PC_WorkshopRestPolicy_Fixed`
  = `WorkshopRestPolicyDef.DefaultPolicyDefName` ⇒ 未改动设置时行为与改动前**逐字一致**。
* Def 名解析失败（设置里存了一个已不存在的 Def）⇒ **继续降级**，不抛异常。

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | **已验证** | 0 错 0 警 |
| XML 解析 | **已验证** | 中英 Keyed 各 1 文件通过 |
| 部署 | **已验证** | DLL 22:20:19 · Defs 31 · Languages 37 · Assemblies 1 |
| 实机行为 | **未验证** | 需重启后确认：班次切换不再需要开面板 |

## 未做（需玩家决策）

1. **接管开关**：见「取证结论 1」—— 现状空转，且改成全局开关需先修订 `EXC-2026-016:81`
   （该记录拒绝"全局开关式接管"）。若要推进，建议顺序：先给岗位的
   `implementedWorkTypeDefNames` 填**真实已实现**的项（当前车间无生产 Job，故暂无可填），
   或明确改为"接管 = 只禁用车间映射的那些类型"。
2. **车间作息策略全局默认**（`defaultRestPolicyDefName`）：`PolicyOf` 的三级降级链已预留
   "全局缺省"一级，可让新车间自动继承；涉及单选 UI 与 Def 名，列入下一批。
3. **一键全解除**：**不建议**做成设置（破坏性出口应保持显式手点，UI-004）。
4. **值勤档位**：**不做**（EXC-2026-013 明文禁止提权）。

## 已知代价

* 在班自动回写使"档位"从"事件驱动"变为"每小时对账"（60 tick 轮 + 小时门控）。
  因只补 0、且 `SetPriority` 内部幂等，实际写入次数极少；无日志（热路径禁日志）。
* `AutoDetectWorkshopType` 关闭后，未指定类型的车间**不会**再自动判定
  （逐车间 `autoTypeSuppressed` 仍独立生效）。

---
change_id: CHG-20261004-MEALSCHEDULE
version: v1.1.5
date: 2026-10-04
author: AI
scope: [Source/PersonalChronicle, Defs, Languages]
files:
  - path: Source/PersonalChronicle/Domain/Meal/MealSchedule.cs
    type: A
    symbols: MealWindow, MealSchedule.ActivePriority, MealSchedule.InMealWindow, MealSchedule.Normalize
    lines: 1-133
  - path: Source/PersonalChronicle/Domain/Meal/MealScheduleDefaults.cs
    type: A
    symbols: MealScheduleDefaults.ClampPriority, MealScheduleDefaults.ClampHour, MealScheduleDefaults.HungerTrigger, MealScheduleDefaults.MaxFoodSearchDistance, MealScheduleDefaults.FoodReserveMaxPawns
    lines: 1-103
  - path: Source/PersonalChronicle/Application/MealScheduleService.cs
    type: A
    symbols: MealScheduleService.Current, MealScheduleService.ActivePriority, MealScheduleService.SetEnabled, MealScheduleService.SetPriority, MealScheduleService.SetMealHours, MealScheduleService.WindowOf
    lines: 1-137
  - path: Source/PersonalChronicle/Automation/MealFoodSearch.cs
    type: A
    symbols: MealFoodSearch.FindFood, MealFoodSearch.IsEdibleFor, MealFoodSearch.CanActuallyEat
    lines: 1-159
  - path: Source/PersonalChronicle/Automation/WorkGiver_Meal.cs
    type: A
    symbols: WorkGiver_Meal.ShouldSkip, WorkGiver_Meal.NonScanJob
    lines: 1-204
  - path: Source/PersonalChronicle/Automation/MapComponent_MealSchedule.cs
    type: A
    symbols: MapComponent_MealSchedule.MapComponentTick, MapComponent_MealSchedule.ApplyPriorityTo, MapComponent_MealSchedule.ResolveMealWorkType
    lines: 1-118
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
    symbols: ChronicleSettings.MealScheduleEnabled, ChronicleSettings.MealPriority, ChronicleSettings.MealBreakfastStartHour, ChronicleSettings.MealLunchStartHour, ChronicleSettings.MealDinnerStartHour, ChronicleSettings.DrawMealScheduleSection, ChronicleSettings.DrawMealWindowRow, ChronicleSettings.ExposeData, ChronicleSettings.DoSettingsWindowContents
    lines: 3-4, 59-75, 110-119, 173-237
  - path: Source/PersonalChronicle/Automation/AutoAssignService.cs
    type: M
    symbols: AutoAssignService.ManagedWorkTypeDefNames
    lines: 78-82
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.WorkDemote.cs
    type: M
    symbols: MapComponent_WorkshopManager.DemotableWorkTypes, MapComponent_WorkshopManager.CollectKeptWorkTypes
    lines: 114-146, 371-376
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.Aliases.cs
    type: M
    symbols: ChronicleGameComponent.ResolveCurrentWorkType
    lines: 411-417
  - path: Defs/Automation_WorkType.xml
    type: M
    symbols: PC_MealBreak
    lines: 100-140
  - path: Defs/Automation_Jobs.xml
    type: M
    symbols: PC_MealGiver
    lines: 53-76
  - path: Languages/ChineseSimplified/DefInjected/WorkTypeDef/AutomationWork.xml
    type: M
    symbols: PC_MealBreak.labelShort, PC_MealBreak.pawnLabel, PC_MealBreak.gerundLabel, PC_MealBreak.description, PC_MealBreak.verb
    lines: 33-37
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.Settings.MealSection, PersonalChronicle.Settings.MealEnabled, PersonalChronicle.Settings.MealDisabledHint, PersonalChronicle.Settings.MealPriorityUrgent, PersonalChronicle.Settings.MealPriorityNormal, PersonalChronicle.Settings.MealBreakfast, PersonalChronicle.Settings.MealLunch, PersonalChronicle.Settings.MealDinner, PersonalChronicle.Settings.MealStartHour, PersonalChronicle.Settings.MealEndHour, PersonalChronicle.Settings.MealWindowHint
    lines: 3688-3698
  - path: Defs/Automation_TimeAssignment.xml
    type: A
    symbols: PC_Meal
    lines: 1-40
  - path: Source/PersonalChronicle/Data/MealPriorityLedger.cs
    type: A
    symbols: MealPriorityLedger.ShouldWrite, MealPriorityLedger.RecordWrite, MealPriorityLedger.IsPlayerOwned, MealPriorityLedger.Prune, MealPriorityLedger.ExposeData
    lines: 1-186
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.MealPriority.cs
    type: A
    symbols: ChronicleGameComponent.MealPriority, ChronicleGameComponent.ExposeMealPriority, ChronicleGameComponent.MealPriorityLedger
    lines: 1-48
  - path: Source/PersonalChronicle/Data/ChronicleGameComponent.cs
    type: M
    symbols: ChronicleGameComponent.ExposeData
    lines: 223-228
  - path: Source/PersonalChronicle/Capture/Patch_TimeAssignmentSelector.cs
    type: A
    symbols: Patch_TimeAssignmentSelector.Postfix, Patch_TimeAssignmentSelector.MealButtonRect, Patch_TimeAssignmentSelector.DrawAssignmentButton, Patch_TimeAssignmentSelector.ResolveMealAssignment
    lines: 1-137
  - path: Source/PersonalChronicle/Capture/Patch_PawnColumnWorker_Timetable.cs
    type: A
    symbols: Patch_PawnColumnWorker_Timetable.Postfix
    lines: 1-60
  - path: Languages/ChineseSimplified/DefInjected/TimeAssignmentDef/PCMeals.xml
    type: A
    symbols: PC_Meal.label
    lines: 1-9
  # ---- CHG-20261004-MEALASSIGN（2026-10-04 追加）：三餐时段纳入车间排班写入 ----
  # 玩家诉求：「对于自动分配管制，给三餐时间管制进行配置」。
  # 修的是交互缺陷：车间排班是整体重写作息表，原先只写 Work/Sleep/PC_Idle
  # ⇒ 手涂的「用餐」格子会被排班变更/设备增减/跨天重放冲掉。
  - path: Source/PersonalChronicle/Automation/WorkshopDutyCoordinator.cs
    type: M
    symbols: WorkshopDutyCoordinator.ApplyWorkHoursToCrew, WorkshopDutyCoordinator.IsMealHour
    lines: 253, 284, 312-336
  - path: Source/PersonalChronicle/Automation/MapComponent_MealSchedule.cs
    type: M
    symbols: MapComponent_MealSchedule.ResolveMealTimeAssignment
    lines: 217
  - path: docs/设计文档/架构/EXC-2026-022.md
    type: A
    symbols: EXC-2026-022
    lines: 1-92
---

# 变更标注 · CHG-20261004-MEALSCHEDULE

**三餐作息**：到点让殖民地成员去吃饭。Build **1.6.4871 rev590** · 编译 **0 错 0 警** · 4 个 XML 解析通过 · 已部署至 `Mods\PersonalChronicleVer0.1`。

## 产品口径（2026-10-04 玩家决策）

| 产品口径 | 决策 | 落地方式 |
|---|---|---|
| 界面归属 | 在工作优先级表中**可见可排班**（`PC_MealBreak` 不写 `<visible>` ⇒ 默认 true ⇒ 隐式生成 `WorkPriority_PC_MealBreak` 列） | 原版 `PawnColumnDefGenerator` 自动生成列 |
| 生效语义 | 玩家可手动排班；另有 PC 三餐作息规则按餐段自动生效 | 隐藏工作类型 + 餐段边界自动写档（与手动共用同一格位） |
| 原版兜底 | 保留原版饥饿驱动，**不替换不关闭** | 不在餐段时本功能恒不介入 |

## 关键事实（全部 Mono.Cecil 静态核实，Build 1.6.4871）

| 事实 | 证据 |
|---|---|
| **工作面板里 work type = 列**，不是行 | `PawnTable.cachedPawns` + `PawnColumnDef.workType`；`PawnColumnWorker_WorkPriority.DoCell` 读 `this.def.workType` |
| **工作类型列由 `PawnColumnDefGenerator.ImpliedPawnColumnDefs` 隐式生成** | 遍历 `WorkTypeDefsUtility.WorkTypeDefsInPriorityOrder.Where(d => d.visible).Reverse()`，把 `new PawnColumnDef{defName="WorkPriority_"+d.defName, workType=d, workerClass=PawnColumnWorker_WorkPriority}` 插入 Work 表 `CopyPasteWorkPriorities` 列之后 |
| ⇒ **`visible` 就是显示判据**，默认 `true` | 过滤谓词 `<ImpliedPawnColumnDefs>b__0_2` IL = `ldfld visible; ret`；`WorkTypeDef..ctor` IL = `ldc.i4.1; stfld visible` |
| ⇒ 隐藏工作类型的正确做法是写 `<visible>false</visible>` | 本文件 3 个工时分档标签即如此 |
| 新 WorkType **自动进工作链** | `Pawn_WorkSettings.CacheWorkGiversInOrder` 遍历 `DefDatabase<WorkTypeDef>.AllDefsListForReading`，取 `GetPriority(w) > 0` |
| 排序键 | `naturalPriority + (4 - 档位) × 100000`（比较器 `b__32_0`）；档位每差 1 = 100000，原版 `naturalPriority` 全域 0~400 ⇒ **档位是唯一有效旋钮** |
| `SetPriority` 硬约束 | 对 `WorkTypeIsDisabled(w)` 的类型写非 0 档 ⇒ **Log.Error 并 return**（故所有写入点都有该守卫） |
| 原版吃饭 Job | `JobGiver_EatRandom.TryGiveJob` = `GenClosest.ClosestThingReachable` + `JobMaker.MakeJob(JobDefOf.Ingest, food)` |
| 原版食物判据（逐条照抄） | `category == ThingCategory.Item` → `IngestibleNow` → `RaceProps.CanEverEat` → `ReservationUtility.CanReserve(...,1,-1,null,0)`；距离 `10f` |
| 1.6 **不存在** | `Verse.WorkGiverDef`（真身 `RimWorld.WorkGiverDef`）、`JobDriver_Eat`、`IsIngestible()`、`GetBestFoodSourceToEatFor`、`PrioritySwitcher` |
| 1.6 MapComponent 入口 | `MapComponentTick()`（**不是** `Tick()`） |

## 实现链路（零 Harmony，全部原版扩展点）

```
MapComponent_MealSchedule.MapComponentTick   ← 每 tick 仅 O(1) 差分（当前小时档位 vs 上次写入）
  └─ 跨边界时: Pawn_WorkSettings.SetPriority(PC_MealBreak, 3|4 或 0)
       └─ Pawn_WorkSettings.CacheWorkGiversInOrder（自动重算，原版）
            └─ RimWorld.JobGiver_Work.TryIssueJobPackage（原版工作链）
                 └─ WorkGiver_Meal.NonScanJob
                      └─ MealFoodSearch.FindFood（判据与原版逐条一致）
                           └─ JobMaker.MakeJob(JobDefOf.Ingest, food)
                                └─ 原版 JobDriver_Ingest + Toils_Ingest（取食/找座/进食/释放预约）
```

## 跨模块隔离（防止三套系统互相拆台）

| 既有通道 | 位置 | 处置 | 不处置的后果 |
|---|---|---|---|
| 自动分配 | `AutoAssignService.ManagedWorkTypeDefNames` | 加入受管集合 | 自动分配勾成 1 档 ⇒ 一有空就吃饭 |
| 车间降档 | `MapComponent_WorkshopManager.WorkDemote`（`DemotableWorkTypes` 全量遍历） | 跳过三餐类型 | 压到 4 档 = 恒垫底 = **三餐静默失效** |
| 车间接管 | `WorkshopTakeover.IsTakeoverAllowed` | **无需改**：闸门②要求「车间岗位能交付该工作类型」，三餐类型无人交付 ⇒ 天然拒绝 |
| 工时/职业事件 | `ChronicleGameComponent.ResolveCurrentWorkType` | 返回 null | 档案馆出现"用餐"工时行，且每 2500 tick 派生一条 `WorkCompleted`（每小时"完成一次工作"） |

## 档位口径（为何只给 3/4 两档）

排序键 = `naturalPriority + (4 − 档位) × 100000`。档 3 ⇒ 压过全部原版工作、让位紧急事务；
档 4 ⇒ 最高。**档 2/1 会让进食恒垫底（等同关闭）**，对玩家没有可观测差异 ⇒ 不暴露，
`MealScheduleDefaults.ClampPriority` 硬 clamp 到 `{3,4}`（GOV-006：禁魔法数字）。

饥饿门槛 `HungerTrigger = 0.9f`（**不是**原版的 0.25）：原版阈值回答"会不会饿死"，
本功能回答"到饭点了没"；真正饥饿时仍由原版兜底，两条路径语义不冲突。

## 时钟语义

- 半开区间 `[start, end)`；`start == end` = **该餐关闭**（绝不解释为"全天"）
- `start > end` = 跨天（支持 22→6 的夜餐）
- 小时制，与车间班次同口径（`GenLocalDate.HourOfDay`）

## 已知限制（如实声明）

1. **不打断在跑的 Job**：工作链只在当前 Job 结束后取新 Job ⇒ 长任务中的吃饭会延迟。这是原版语义；
   实现"强制打断"需另开需求 + EXC。
2. **新加入/读档的殖民者**：靠 `MapComponentTick` 的边界写入 + 幂等差分覆盖；
   餐段中途加入者要等下一次边界（最迟 1 餐）才被写入。
3. **实机未验证**：本轮为静态取证 + 0 错 0 警编译 + XML 解析；沙箱无法起游戏进程，
   需玩家按下方验收步骤确认 `[MEAL]` 链路。
4. `MapComponent_MealSchedule` 有实例缓存 `cachedMealWorkType` 与一次性告警标志（静态），
   属**启动期一次性解析缓存**，非运行期可变状态（不构成 MATRIX-008 静态可变状态）。

## 验收步骤（玩家实机）

1. 重启 RimWorld（程序集不热重载）。
2. Mod 设置 → 勾选「启用三餐作息」，默认 7-9 / 12-14 / 18-20。
3. 打开工作标签：**确认面板里没有出现任何新行**（本功能不占行）。
4. 把游戏时间调到早餐前 1 小时，等到点后观察：空闲殖民者走向食物并坐下进食。
5. `Player.log` 过滤 `[PersonalChronicle]`：Def 解析正常时**不应**出现
   `Meal work type not found` 告警。
6. 存读档后重复第 4 步，确认档位仍生效（旧档无新键 ⇒ 取默认值）。
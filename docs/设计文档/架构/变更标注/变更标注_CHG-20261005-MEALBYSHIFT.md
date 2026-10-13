---
change_id: CHG-20261005-MEALBYSHIFT
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopDutyCoordinator.cs
    type: M
    symbols: WorkshopDutyCoordinator.AddMealHoursOfShift, WorkshopDutyCoordinator.MarkMealWindow, WorkshopDutyCoordinator.MarkWindow, WorkshopDutyCoordinator.BuildCrewMealHours, WorkshopDutyCoordinator.ApplyWorkHoursToCrew
    lines: 150-180, 270-300, 390-480
  - path: Source/PersonalChronicle/Automation/MapComponent_MealSchedule.cs
    type: M
    symbols: MapComponent_MealSchedule (doc), MapComponent_MealSchedule.lastRuleHour, MapComponent_MealSchedule.MapComponentTick, MapComponent_MealSchedule.Notify_ColonistsChanged, MapComponent_MealSchedule.MapComponentUpdate, MapComponent_MealSchedule.DesiredPriorityOf
    lines: 1-38, 40-90, 170-200
  - path: Source/PersonalChronicle/Automation/WorkshopShift.cs
    type: M
    symbols: WorkshopShift (Meal* 字段区注释), WorkshopShift.HasShiftMeal
    lines: 41-60
  - path: Source/PersonalChronicle/Automation/Dialog_WorkshopShiftEditor.cs
    type: M
    symbols: Dialog_WorkshopShiftEditor.DrawShiftMealRow
    lines: 338-360
  - path: Source/PersonalChronicle/Automation/AutoAssignService.cs
    type: M
    symbols: AutoAssignService (注释口径)
    lines: 76-82
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
    symbols: ChronicleSettings (Meal* 字段区), ChronicleSettings.ExposeData, ChronicleSettings.DrawMealScheduleSection
    lines: 96-115, 150-160, 275-345
  - path: Source/PersonalChronicle/Settings/SettingsCatalog.cs
    type: M
    symbols: SettingId (Meal* 枚举), SettingsCatalog.MealRows
    lines: 120-130, 214-232
  - path: Source/PersonalChronicle/Settings/SettingsDrawer.cs
    type: M
    symbols: SettingsDrawer.GetToggle, SettingsDrawer.SetToggle, SettingsDrawer.SegmentedIndexOf, SettingsDrawer.SetSegmentedIndex, SettingsDrawer.MealIndexOf(删)
    lines: 370-380, 448-475, 520-545
  - path: Source/PersonalChronicle/Application/MealScheduleService.cs
    type: D
    symbols: MealScheduleService (整文件删除)
    lines: 1-123
  - path: Source/PersonalChronicle/Domain/Meal/MealSchedule.cs
    type: M
    symbols: MealSchedule (类删除), MealWindow (保留)
    lines: 1-70
  - path: Source/PersonalChronicle/Domain/Meal/MealScheduleDefaults.cs
    type: M
    symbols: MealScheduleDefaults.DutyMealPriority(新增), MealScheduleDefaults.DefaultEnabled/DefaultPriority/三餐默认窗口(删)
    lines: 75-115
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: ShiftMealUnset(新增)
    lines: 536-540
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: ShiftMealUnset(新增)
    lines: 528-532
  - path: Languages/ChineseSimplified/Keyed/SettingsUi.xml
    type: M
    symbols: Title.MealMovedToShift(新增), Use.MealMovedToShift(新增)
    lines: 93-97
  - path: Languages/English/Keyed/SettingsUi.xml
    type: M
    symbols: Title.MealMovedToShift(新增), Use.MealMovedToShift(新增)
    lines: 81-85
---

# 变更标注 · CHG-20261005-MEALBYSHIFT

## 背景（玩家 2026-10-05，附管制表截图）

> 「白班可以，**夜班的三餐设置存在问题**。要求一：**删除全局三餐 mod 设置**。
> 统一口径以**班次设置中的三餐时间**执行。」

## 一、夜班三餐的根因（已定位）

`WorkshopDutyCoordinator.BuildCrewMealHours` 原先把该班组**全部指派班次**的餐点**取并集**：

```csharp
for (int i = 0; i < crew.AssignedShifts.Count; i++)
    AddMealHoursOfShift(crew.AssignedShifts[i], hours);   // ← 白班与夜班的餐点被一起并进来
AddMealHoursOfShift(effective, hours);
```

倒班班组（同时指派白班 + 夜班）的 **夜班人员因此也被涂上白班餐点（7/12/17）**；
再叠加上一批（CHG-20261005-MEALPRIO）把判定顺序改成"用餐优先于睡眠"
⇒ **夜班的人白天睡觉时被叫起来吃饭** —— 与玩家截图/描述完全吻合。

## 二、修法一：餐点 = 各自班次的工作时段 ∩ 该班次配置的餐段

```csharp
private static void AddMealHoursOfShift(WorkshopShift shift, bool[] hours)
{
    if (shift == null || hours == null || !shift.HasShiftMeal) return;   // 没配 = 该班次无餐点
    bool[] shiftWork = new bool[24];
    WorkshopDutyRules.MarkWorkHours(shift.StartHour, shift.EndHour, shiftWork);  // 与写表同一判定
    MarkMealWindow(hours, shiftWork, shift.MealBreakfastStartHour, shift.MealBreakfastEndHour);
    ...（午/晚同）
}
```

**语义收益**（一句话）：白班的 7–8 点**不可能**出现在夜班（18–6）的表上；
若把夜班餐点误配到白天，它**不生效**（而不是去污染别人的作息）——
配置错误与生效范围不再互相掩盖。

> 说明：上一批的"用餐优先于睡眠"**保留**（防御性，不再必要但无害）：
> 现在能落到餐点集合里的小时必然已在工作时段内 ⇒ 两种顺序等价。

## 三、修法二：删除全局三餐设置（玩家要求一）

**删除清单**（合计 8 个持久字段 + 1 个服务层 + 1 个域类 + 5 行设置 + 1 条回落分支）：

| 项 | 内容 |
|---|---|
| 设置字段 | `ChronicleSettings` 的 `MealScheduleEnabled` / `MealPriority` / 三餐起止 ×3（共 8 个）**及其 Scribe 键** |
| 上一批的迁移块 | `ChronicleSettings.ExposeData` 里的"旧默认值指纹式迁移"（服务对象已不存在） |
| 设置页（旧版） | `ChronicleSettings.DrawMealScheduleSection` 的开关/档位/三餐滑条（3 组控件）+ `DrawMealWindowRow`（整方法删除） |
| 设置页（新版 RUIF） | `SettingsCatalog` 的 5 行目录项 + `SettingsDrawer` 的 4 处分支（开关读写 / 段控读写 / `DualIntStep` 控件 / `MealIndexOf`） |
| 服务层 | `Application/MealScheduleService.cs`（**整文件删除**，123 行） |
| 域类 | `Domain/Meal/MealSchedule.cs` 的 `MealSchedule` 类（全局快照 / `Normalize` / `ActivePriority` / `InMealWindow`）；**保留** `MealWindow`（纯几何值对象） |
| 默认值 | `MealScheduleDefaults` 的 `DefaultEnabled` / `DefaultPriority` / 三餐默认窗口常量 |
| 回落分支 | `AddMealHoursOfShift` 的"未配班中餐 ⇒ 用全局窗口" |

**菜单位置保留**：设置页「作息与用餐」页签**保留**，但只剩一行**说明**
（"三餐已移到班次里配置"）——避免玩家打开这页只看到空白、以为功能被删掉（GATE-UI 状态完备性）。

## 四、修法三：档位判定改为只看管制表

`MapComponent_MealSchedule.DesiredPriorityOf` 原先有**两套来源**（管制表时段 / 按当前小时查全局餐段），
现在收敛为一套：

```csharp
return tracker != null && tracker.CurrentAssignment == mealAssignment
    ? MealScheduleDefaults.DutyMealPriority   // 新增常量 = PriorityNormal
    : 0;
```

- 档位固定为 `DutyMealPriority`（= 正常档："压过全部原版普通工作、让位给紧急类"）
  —— 原先是设置页二选一，该设置已删 ⇒ 收敛为固定值，避免"配置项没了但语义不明"；
- `MapComponentTick` 的每 tick 差分改为**每小时兜底重算**（不再有"规则档位"这个量）：
  正常路径仍由 `Patch_PawnColumnWorker_Timetable` 唤醒，
  小时级兜底用于"第三方 Mod/脚本改作息表不走该补丁"的情形；
- 顺带消除一个旧边界：两套来源互相打架（表里是用餐但格不是 PC 涂的时，第二套来源仍会打开档位）。

**链条收窄为一条**：<b>班次配置 → 管制表（∩ 班次工作时段）→ 档位</b>。

## 存档影响：无 Migration，不需要 bump `schemaVersion`

- 删除的 8 个键在老档里仍存在，读档时**无对应字段 ⇒ 被 Scribe 直接忽略**
  （RimWorld 按键查找，多余的键无副作用）；
- `WorkshopShift` 的 6 个班中餐字段**未改动**（存储形状不变）；
- 新增的常量不入档。

## 删除项登记（GATE-DELTA）

| 删除内容 | 替代 / 理由 |
|---|---|
| `MealScheduleService`（整文件） | 唯一职责是读写全局三餐设置 ⇒ 设置不存在则服务无对象 |
| `MealSchedule`（类） | 全局三餐快照 ⇒ 同上；`MealWindow` 值对象**保留**（班次餐点仍需要它） |
| `MealScheduleDefaults.DefaultEnabled/DefaultPriority/三餐窗口` | 由 `DutyMealPriority` 单常量取代 |
| `ChronicleSettings.Meal*` 8 字段 + Scribe 键 | 无替代（功能整体移除）；旧档多余键被忽略 |
| `DrawMealWindowRow` / `MealIndexOf` / `DualIntStep` 控件分支 / 5 行设置目录 | 控件服务对象已不存在 |

**保留的孤儿语言键**：`PersonalChronicle.Settings.MealEnabled` / `MealDisabledHint` /
`MealPriorityUrgent` / `MealPriorityNormal` / `MealStartHour` / `MealEndHour` /
`MealWindowHint` / `Automation.Tab.ShiftMealFollowGlobal` 等仍留在语言文件里
—— 键查找语义下**无任何运行时影响**（缺 Key 会显示 key 名，多余 Key 不会），
本批不动它们以缩小改动面；后续清理批次可一并移除。

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`_scripts/compile_with_sdk.ps1`） |
| XML | 已验证 | Automation（中/英）+ SettingsUi（中/英）均可解析 |
| 删除确认 | 已验证 | `Application/MealScheduleService.cs` 不存在；全仓无 `MealScheduleService` 引用（编译 0 错为证） |
| 部署 | 已验证 | Defs 31 / Languages 39 / Assemblies 1 同步 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-MEALBYSHIFT.md` |
| 实机 | 未验证 | 见下 |

### 实机验证要点（重启后）

1. **设置页**：「作息与用餐」页签只剩一行说明（"三餐已移到班次里配置"），
   不再有全局开关/档位/三餐时间滑条；
2. **班次编辑器**：班中餐未设置时按钮显示「**未设置**」（不再是"跟随全局"）；
   点开时段弹窗，两端相同 ⇒ 提示「起 == 止 = 关闭该餐」；
3. **夜班（核心）**：给夜班配好餐点（必须落在**夜班工作时段内**，如 23:00–24:00）⇒ 打开【管制】表：
   夜班行应出现「用餐」格，**且白天不出现**（白班餐点不再污染）；
4. **白班回归**：白班餐点仍按原样出现；
5. **跨天/改班组**：餐点格保持，不被重建冲掉；
6. `Player.log` 无新的 `PersonalChronicle` 异常。

## 残留风险

1. **行为变更（重要）**：原先"没配班中餐的班次自动沿用全局三餐"**不再存在** ⇒
   老档里**未配班中餐的班次现在没有餐点**（不再自动吃饭）。这与玩家要求一致（单一口径），
   但需要玩家在班次里逐条配一次；设置页说明行已写明"在哪里配"；
2. **餐点必须落在班次工作时段内**：配到工作时段外的餐点会被静默丢弃（不生效）。
   当前**没有 UI 提示**告知玩家"这段餐点不会生效"——建议后续在班次编辑器里对越界餐点标灰/加提示；
3. **孤儿语言键**（见上）未清理；
4. `SettingKind.DualIntStep` 枚举值保留但已无使用者（删它会牵动枚举与校验两处，收益为零）。

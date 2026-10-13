---
change_id: CHG-20261005-MEALHOUR-FIX
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopDutyCoordinator.cs
    type: M
    symbols: WorkshopDutyCoordinator.AddMealHoursOfShift, WorkshopDutyCoordinator.MarkMealHour, WorkshopDutyCoordinator.BuildCrewMealHours
    lines: 400-480
  - path: Source/PersonalChronicle/Automation/WorkshopShift.cs
    type: M
    symbols: WorkshopShift (Meal* 字段语义注释)
    lines: 41-50
  - path: Source/PersonalChronicle/Automation/Dialog_WorkshopShiftEditor.cs
    type: M
    symbols: Dialog_WorkshopShiftEditor (班中餐段注释)
    lines: 169-180
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: ShiftMealHint, ShiftMealClearAll (文案)
    lines: 313-316
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: ShiftMealHint, ShiftMealClearAll (文案)
    lines: 316-319
---

# 变更标注 · CHG-20261005-MEALHOUR-FIX

## 背景（玩家 2026-10-05，附两张截图）

> 「我清理了管制，发现**自动对齐后早餐没有对齐**。清楚残留【**全部跟随全局**】。」

截图证据：
- 班次表里两条班次：`08:00–20:00`（白班）与 `20:00–08:00`（**跨午夜**夜班）；
- 夜班的**班中早餐 = 19:00**、午餐 = 00:00、晚餐 = 06:00；
- 班次编辑器底部仍是「未设置的餐点仍用设置页『三餐作息』的时间」+「**全部跟随全局**」按钮。

## 一、早餐没对齐的根因（上一批我引入的过度约束）

`CHG-20261005-MEALBYSHIFT` 给餐点加了"**必须落在该班次工作时段内**"的过滤：

```csharp
bool[] shiftWork = new bool[24];
WorkshopDutyRules.MarkWorkHours(shift.StartHour, shift.EndHour, shiftWork);   // ← 20:00–08:00
MarkMealHour(hours, shiftWork, shift.MealBreakfastHour);                      // ← 19 点：shiftWork[19] == false
```

⇒ 夜班是 `20:00–08:00`，而玩家配的早餐是 **19:00** —— **19 点不在班次内 ⇒ 被静默丢弃**。
表现正是玩家看到的现象：**清理管制后自动重涂，其余两餐（00:00 / 06:00 在班次内）正常，早餐缺失**。

> 我上一批加这条过滤的**本意**是防倒班班组的餐点串台（当时玩家报"夜班三餐有问题"）。
> 但它同时把"跨午夜班次 + 上班前吃饭"这种**完全合法的配置**一起丢掉了 —— 属过度约束。

## 二、修法：不做时段过滤（餐点是玩家显式意图）

```csharp
private static void MarkMealHour(bool[] hours, int hour)
{
    if (hours == null || hour < 0 || hour >= hours.Length) return;   // -1 = 该餐关闭
    hours[hour] = true;
}
```

**立场说明（写在代码注释里）**：串台问题的真正来源是"班组把多个班次的餐点**取并集**"
（`BuildCrewMealHours`）—— 那是"一个班组多个班次"的语义问题，**应当在并集那里解决，
而不是靠丢弃玩家配置来掩盖**。

越界餐点之所以能正常落地：`ApplyWorkHoursToCrew` 的判定顺序里「用餐」**优先于「睡眠」**
（该优先级是 CHG-20261005-MEALPRIO 引入的，**现在成为必需** —— 否则"上班前吃饭"无处落脚）。

## 三、清除"跟随全局"残留（玩家要求）

| 项 | 旧 | 新 |
|---|---|---|
| `ShiftMealHint`（提示行） | "未设置的餐点沿用设置页「三餐作息」的时间" ← **全局设置已删除，这是假话** | "三餐各是一个时刻（如 07:00）；配好即写入成员的管制表" |
| `ShiftMealClearAll`（按钮） | "全部跟随全局" | "**全部清空**"（功能不变：`ClearShiftMeal()` 把三餐设为 -1） |
| `ShiftMealFollowGlobal` | "跟随全局（{0}）" | **已无代码引用** ⇒ 成为孤儿键（键查找语义下零运行时影响，保留无害） |
| 代码注释（3 处） | "未设置 ⇒ 跟随全局三餐" / "∩ 工作时段" 等 | 同步为"餐点唯一来源 = 班次配置、不做时段过滤" |

## 四、已知边界（如实登记，未修）

**一个班组同时指派多个班次（倒班）时，餐点取并集** ⇒ 该班组成员会在**全部**班次的餐点格上
都被涂「用餐」（例如白班 7/12/17 与夜班 19/00/06 同时生效）。

- 这是"班组 → 班次"**多对多**带来的语义限制；
- 正确解法是"**逐人按其当日实际班次取餐点**"，需要"人 → 班次"的映射，
  而当前数据模型只有"人 → 班组"；
- **单班次班组不受影响**（玩家当前的配置正是单班次班组 ⇒ 修完即正确）。

## 存档影响：无。本批不涉及任何 Scribe 键（纯判定与文案）

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警（`_scripts/compile_with_sdk.ps1`） |
| XML | 已验证 | Automation 中/英均可解析 |
| 部署 | 已验证 | Defs 31 / Languages 39 / Assemblies 1 同步 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-MEALHOUR-FIX.md` |
| 实机 | 未验证 | 见下 |

### 实机验证要点（重启后）

1. **核心**：夜班（20:00–08:00）餐点保持 19:00 / 00:00 / 06:00 ⇒
   清理管制（或在管制表里把这三格擦掉）⇒ 等自动重涂 ⇒
   **19:00 那一格现在应被涂成「用餐」**（此前缺失）；
2. 白班（08:00–20:00）餐点不受影响（其餐点本就在班次内）；
3. 班次编辑器底部：提示改为"三餐各是一个时刻…"，按钮显示「**全部清空**」；
   点它 ⇒ 三餐都回到「未设置」；
4. `Player.log` 无新的 `PersonalChronicle` 异常。

## 残留风险

1. **多班次班组的并集边界**（见第四节）—— 需"人 → 班次"映射才能根治，属另一批；
2. **越界餐点仍会被涂到"非工作时段"**（这是本批的**预期行为**：玩家配了就生效），
   但若误配（例如把白班餐点配到凌晨 3 点）不会被系统拦下 —— 属"尊重显式配置"的代价；
3. `ShiftMealFollowGlobal` 为孤儿键（无害，未清）。

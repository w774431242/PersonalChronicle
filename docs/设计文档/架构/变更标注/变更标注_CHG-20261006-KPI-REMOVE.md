---
changeId: CHG-20261006-KPI-REMOVE
title: 移除制造中心 ① 总控 KPI 九卡（UI + 数据采集 + 顶栏摘抄 + 翻译键）
date: 2026-10-06
version: v1.5.1（未发版，仅工作区）
scope:
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinManufacturingProvider.cs
    type: M
  - path: Source/PersonalChronicle/Application/ReadModels/SystemCenterReadModelProvider.cs
    type: M
  - path: Source/PersonalChronicle/Application/ReadModels/ManufacturingChromeSnapshot.cs
    type: M
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.AppShell.cs
    type: M
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Chrome.cs
    type: M
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Dev.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
  - path: Languages/English/Keyed/Archive.xml
    type: M
---

# 变更标注 — CHG-20261006-KPI-REMOVE

> 范围：按用户指令**移除制造中心 ① 总控 KPI 九卡**（设备可用率 / 在产设备率 / 产出效率 / 车间人时 /
> 排班覆盖率 / 在岗人数 / 良品率 / 加成效力 / 平均心情）—— UI、数据采集、顶栏摘抄、翻译键**全链删除**，
> 不留死代码与孤儿键。
> 日期：2026-10-06 ｜ 触发：用户实机截图 + 指令「kpi 也要移除。确保干净。」
> 编译：`_scripts/compile_with_sdk.ps1`（工程权威入口，裸 csc）→ **0 错 0 警**。
> 部署：`_scripts/deploy-to-game.ps1` → `RimWorld\Mods\殖民地综合管理系统`（192 文件；DLL 时间戳见部署日志）。
> 范围外：仓库既有未提交工作 + 同日两轮变更（RAIL-LAYOUT / FTABS-REMOVE），
> 通过 `-ExcludeFromFile .codebuddy/tmp/kpi-oos.txt` 声明排除。
> 注：本标注文件自身由 gate-runner 从 diff 集合中剔除，故不列入 `scope`。

---

## 1. 删除面（三个消费面 + 数据面，全链）

| # | 位置 | 删除内容 |
|---|---|---|
| ① 数据面 | `BuiltinManufacturingProvider.cs` | 九卡采集整段：`AppendKpisCore`（设备台账 / 产出 ledger / 车间运行摘要 → 9 卡）、`CollectEquipment`、`AverageColonistMood`、`BonusUplift`、`MoodNote`、`AddRateCard`、`AddTextCard`、`ToneForRate`、`KpiKeys`、`KpiRowLayout`、`RateGood/Fair/WarnThreshold`、`HoursPerDay`；`AppendKpis` 改为**空实现**；`KpiSectionTitleKey` 返回空串、`KpiRowSizes` 返回 `null`（接口成员按契约保留） |
| ② ② 面板内容区 | `MasterControlTabWindow.AppShell.cs` | `AppKpiBandHeight`、`AppKpiRowCount`、`DrawAppKpiBand`、`DrawAppKpiCard`、`DrawKpiDial`（PC 自绘表盘回落）、`DialZoneTone`（表盘分区色）+ 常量 `AppKpiCardHeight` / `AppKpiGap` / `KpiDialLabelBand` / `KpiDialTickSteps`；`ActivePaneHeight` 与 `DrawActivePane` 的 `TabCockpit` 分支改为"恒空"（保留 case 以维持 tab 语义） |
| ③ 顶栏读数带 `#kpiline` | `Chrome.cs` + `ManufacturingChromeSnapshot.cs` + `SystemCenterReadModelProvider.cs` | `Chrome.DrawKpiLine` / `KpiLineToneColor` + cmdbar 里的绘制段；`ManufacturingChromeSnapshot.KpiLine` 字段与 `KpiLineItem` 结构（含 `using System.Collections.Generic`）；`SystemCenterReadModelProvider.FillKpiLine` / `KpiLineTone` / `ParsePercent`（三者仅服务该带，已核全库无其它调用） |
| ④ Dev 假数据 | `MasterControlTabWindow.Dev.cs` | `FillKpiValues(...)` **调用点与方法体**（Dev 链路本就不可达，但留着会与"无 KPI"矛盾）。⚠ 首版只删了调用点 ⇒ 被 GATE-UI 守门判 P2「死方法残留」，**已补删方法体**；教训：删"演示填充"必须调用点与方法体同删（编译不会为未用私有方法报警） |
| ⑤ 翻译键 | zh + en `Archive.xml` | 制造中心 KPI **30 个键整块删除**：`Section.ManufacturingKpi` + `Kpi.Availability/Utilization/OutputEfficiency/ManHours/RosterCoverage/OnDuty/QualityYield/BonusUplift/WorkerMood` 及其 `Note` / 空态 / `Unit.Hours` / `WorkerMoodNote.{High,Steady,Low}` / `Quality.NoRecord` / `NoEquipment` / `NoColonist` |

## 2. 保留面（有意不删 —— 它们不是"制造中心 KPI"的专属设施）

| 保留项 | 理由 |
|---|---|
| `Detail.DrawKpiGrid` / `KpiBlockHeight` / `DrawKpiCard` / `EnsureKpiRowPlan` / `InvalidateKpiRowPlan` / `KpiCardHeight` / `KpiGroupSize` / `KpiColumnsFor` | **通用 KPI 机制**：经济中心（`MasterControlTabWindow.Economy.cs`）仍用它画"总财富 / 白银 / 库存市值"三格；人员中心亦经 `KpiRowSizes` 声明四卡 |
| `SystemCenterKpiView` / `SystemCenterKpiFactory` / `SystemCenterTone` / `KpiSectionTitleKey`（接口成员） | 共享契约与工厂（人员中心 `Count`/`Pending`、Dev `FormatPercent` 仍在用） |
| `PersonalChronicle.UI.MasterControl.Kpi.Archive.*`（4 键 + `NoService`） | 人员中心的档案规模 KPI，与本指令无关 |
| `RuifBridge.TryDrawGauge` / `GaugeFixedVersion` | 框架适配层 API（跨模块契约）；当前零调用，留待恢复或其它中心复用 —— 已在 `AppShell` 注释中登记 |
| `AppShell.ToneColor` / `MeasureWidth` / `DrawDetailPanelFallback` / `PaneMaxRatio` / `PaneMaxAbsolute` | 均为通用工具，仍被其它分支使用（已核） |
| `BuiltinManufacturingProvider.CollectProducedStats` / `CollectProducedQuality` / `StatsWindowDays` / `T` / `T2(int,int)` | **跨 partial 共用**：前者服务 `.Analysis.cs` 的良品率统计；后两者被 CommandBar / Chrome / DataLayer / Bridge / StockQueue / Events / Operations 调用（本轮一度误删 `T2`，编译立即报出 → 已恢复并加注"不得再删"） |

## 3. 连带效果（有意，勿当缺陷修）

1. **② 面板**：内容区高度恒为 0（`paneHeight > 1f` 门控跳过绘制）⇒ 平面图可用高度增加；底栏 tab 栏此前已移除。
2. **顶栏**：`#kpiline` 整条消失（cmdbar 变为"时间 → 电网 → build 标记"）。
3. **注册表卡片**：`BuildCard` 从 `AppendKpis` 首项取 `HeadKpiLabel/Value/Tone` ⇒ 制造中心卡片右下角的"首项 KPI 摘要"变为**留空**（卡片既有逻辑"无数据则整段留空，不画假数值" ⇒ 不崩、不造假）。
4. **6 个页签仍无入口**（上一轮 `CHG-20261006-FTABS-REMOVE` 已登记 P2）：本轮把 `TabCockpit` 的内容也清空后，内容区整体为空 —— 去留（恢复 / 正式下线）仍待用户裁决。

## 4. Gate 结果

| Gate | Result | Evidence |
|---|---|---|
| GATE-UI | **PASS WITH EXCEPTION** | `pc-ui-loc-guard` 只读审查：三个消费链断净（16 个已删符号在 Source/Tests/_scripts **零代码引用**，仅注释命中）；`TabCockpit` 恒空 → `paneHeight > 1f` 门控跳过绘制，**不占高、无空白带**；`selectedFloorTab` 无用户可点入口。例外（已修）= 守门发现的 P2「`FillKpiValues` 死方法残留」：首版只删调用点、漏删方法体 ⇒ 本轮已补删（**编译不会为未用私有方法报警**，此类必须调用点与方法体同删） |
| GATE-LOC | **PASS** | zh/en **双侧对称删除 30 键**（键名逐一比对）；保留键 `Kpi.Archive.*`（5）/`MasterControl.NoValue`/`Section.Kpi` 均在且在用；被删键在 Source **零引用**；本轮 0 新增可见文本 |
| GATE-DATA | 见 `pc-data-steward` 审查 | `AppendKpis` 空实现 + 空 `KpiSectionTitleKey` / `null KpiRowSizes` 符合 `ISystemCenterDataProvider` 契约（"null/空 = 不分组"，`EnsureKpiRowPlan`/`KpiGroupSize` 对空值安全）；无 Save 面变化 |
| GATE-TEST | **部分** | `_scripts/compile_with_sdk.ps1`（工程权威入口）**0 错 0 警**；已部署到 `Mods\殖民地综合管理系统`（DLL 2026-10-06 21:07）；游戏内实测未执行（沙箱无游戏进程） |
| GATE-DELTA | **PASS** | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261006-KPI-REMOVE.md -ExcludeFromFile .codebuddy/tmp/kpi-oos.txt -Blocking` → 「标注覆盖 8 个改动文件，无遗漏/超范围/残留」 |

**守门登记的后续观察项（非本轮缺陷）**：既有孤儿键 `MasterControl.Kpi.Silver / ColonyWealth`（Source 零引用，双侧保留，非本轮产物）→ 建议并入后续孤儿键审计；`RuifBridge.TryDrawGauge` 现零调用（框架适配层 API，保留）；6 个页签无入口 + ② 内容区整体为空（延续 `CHG-20261006-FTABS-REMOVE` 的 P2，待用户裁决）。

## 5. 待实机验收

1. 制造中心 ② 面板**不再出现**九张 KPI 卡，平面图相应变高且无溢出/重叠。
2. 顶栏 `#kpiline` 整条消失（cmdbar 只余时间 / 电网 / build）。
3. 制造中心注册表卡片右下角**留空**（不出现 `—` 或假数值）。
4. **经济中心**（总财富 / 白银 / 库存市值三格）与**人员中心**（档案四卡）的 KPI **照常显示** —— 这是"通用机制未被误删"的关键回归点。
5. DevMode 调试填充开关不再试图填 KPI（无异常日志）。

## 6. 回滚

`git checkout -- <§1 的 8 个文件>`（翻译键一并回滚）；或从 `_backups/release-deploy/deploy_<上一批次>` 取回 DLL。
无 Save / Migration / 版本号影响。

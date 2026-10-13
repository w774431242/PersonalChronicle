---
change_id: CHG-20260919-ECON-UI-008
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Application/**, Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.cs, Source/PersonalChronicle/Economy/UI/EconomyInventoryChainPanel.cs, Languages/**, docs/**]
files:
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinLogisticsProvider.cs
    type: M
    symbols: BuiltinLogisticsProvider.AppendKpis / AppendKpisCore / AppendPendingKpis / KpiItemWealth / KpiStorageBuildings
    lines: 41-115
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinMedicalProvider.cs
    type: M
    symbols: BuiltinMedicalProvider.AppendKpis / AppendKpisCore / AppendPendingKpis / KpiDowned / KpiMedicineIndustrial / KpiMedicineHerbal
    lines: 40-125
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinManufacturingProvider.cs
    type: M
    symbols: BuiltinManufacturingProvider.KpiKeys / AppendKpis / AppendKpisCore
    lines: 81-175
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FlowGraph.cs
    type: M
    symbols: BuiltinEconomyProvider.BuildFlowGraph / BuildChainProjection / DownstreamTop
    lines: 185-195,340-425
  - path: Source/PersonalChronicle/Automation/WorkshopFloorViewProvider.cs
    type: M
    symbols: WorkshopFloorViewProvider.BuildWorkshopView（catch 段）
    lines: 91-115
  - path: Source/PersonalChronicle/Application/ReadModels/ArchiveUiDataProvider.cs
    type: M
    symbols: ArchiveUiDataProvider.BuildHome / BuildOverview
    lines: 25-135
  - path: Source/PersonalChronicle/Application/ReadModels/ArchiveUiDataProvider.Event.cs
    type: M
    symbols: ArchiveUiDataProvider.BuildEvent / BuildTimelineEvents
    lines: 19-95
  - path: Source/PersonalChronicle/Application/ReadModels/EconomyFlowGraphSnapshot.cs
    type: M
    symbols: EconomyFlowGraphSnapshot.ChainNodeIndices / UpstreamEdgeIndices / DownstreamEdgeIndices
    lines: 59-95
  - path: Source/PersonalChronicle/Economy/UI/EconomyInventoryChainPanel.cs
    type: M
    symbols: EconomyInventoryChainPanel.DrawMain / DrawChainColumn（旧 ChainNodes / Mark / CollectEdges / DownstreamTop 已删）
    lines: 30-40,84-135,225-265,290-305
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.UI.Floor.ReadFailed
    lines: 3018
  - path: Languages/English/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.UI.Floor.ReadFailed
    lines: 3014
  - path: docs/changelog/v1.4.x_2026-09-19_P0降级链与投影下沉.md
    type: A
    symbols: CHG-20260919-ECON-UI-008
    lines: 1-130
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-008.md
    type: A
    symbols: CHG-20260919-ECON-UI-008
    lines: 1-150
---

# 变更标注 · CHG-20260919-ECON-UI-008（P0 降级链 + 链路投影下沉）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-ECON-UI-008`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-ECON-UI-008
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent；承接同日审计报告 §5 的 P0/P1 清单）
- Scope(摘要): 三组 P0 降级链缺口 + `EconomyInventoryChainPanel` 派生逻辑下沉到 Provider

### 一句话动机
承接 `审计-前后端数据对齐-2026-09-19.md` §5：把"**一处异常 = 整页空白 / GUI 卡死 / 半截假视图**"
这三类降级缺口补上，并把 `Economy/UI` 目录**唯一**的派生违规下沉到 Provider。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `Builtin/BuiltinLogisticsProvider.cs` | M | `AppendKpis`（拆 wrapper/core） · `AppendPendingKpis`（新） · 两个键常量 | 41-115 | 补 try/catch + **只清自己追加的那段** + 异常转 Pending；顺带修「`wealthWatcher` 为 null 画 0 银」→ Pending | GOV-011 / UI-001 | ARCH / TEST |
| `Builtin/BuiltinMedicalProvider.cs` | M | 同上（3 卡） | 40-125 | 同上；顺带修「`resourceCounter` 为 null 画 0」→ 两张药品卡 Pending（卧床人数保持真实读数） | GOV-011 / UI-001 / LAND-009 | ARCH / TEST |
| `Builtin/BuiltinManufacturingProvider.cs` | M | `KpiKeys`（新） · `AppendKpis` / `AppendKpisCore` | 81-175 | 同上（9 卡）；`AppendKpisCore` 必须是**实例**方法（`CollectProducedQuality` 非静态） | GOV-011 / UI-001 | ARCH / TEST |
| `Builtin/BuiltinEconomyProvider.FlowGraph.cs` | M | `BuildChainProjection`（新） · `DownstreamTop`（新常量） · `BuildFlowGraph`（+1 调用） | 185-195,340-425 | 链路投影三件（链节点集/上游边/下游边已排序截断）在**构建期**产出；`Weight01` 就地归一化（本批前置修复） | GOV-010 / PERF-001 | PERF / DATA |
| `Automation/WorkshopFloorViewProvider.cs` | M | `BuildWorkshopView` 的 catch 段 | 91-115 | catch 里**清空半截数据**（Bays/Devices/Workers/RawItems/Routes/Depot + 计数）并给诚实的失败 Hint；**有意不置 `NoMap`**（否则整块视图无声消失） | UI-001 / GOV-013 | UI / TEST |
| `ReadModels/ArchiveUiDataProvider.cs` | M | `BuildHome` / `BuildOverview` | 25-135 | 补 try/catch（类注释早已承诺失败隔离但从未实现）；降级返回**干净的新空快照** | GOV-011 / ARC-011 | ARCH / TEST |
| `ReadModels/ArchiveUiDataProvider.Event.cs` | M | `BuildEvent` / `BuildTimelineEvents` | 19-95 | 同上；`BuildTimelineEvents` 降级返回**空数组**（非 null —— 调用方按接口迭代） | GOV-011 / ARC-011 | ARCH / TEST |
| `ReadModels/EconomyFlowGraphSnapshot.cs` | M | `ChainNodeIndices` / `UpstreamEdgeIndices` / `DownstreamEdgeIndices`（新三字段） | 59-95 | 投影产物入快照（存**`Edges` 下标**，与 Edges 同批生成 ⇒ 结构上不可能下标错位） | DATA-001 / GOV-010 | DATA |
| `Economy/UI/EconomyInventoryChainPanel.cs` | M | `DrawMain` / `DrawChainColumn`（形参改下标）；`ChainNodes` / `Mark` / `CollectEdges` / `DownstreamTop` **删除** | 30-40,84-135,225-265,290-305 | UI 只按下标取边 ⇒ 绘制期**零分配**；保留一次边界守卫（铁律 3，越界异常会让 GUIClip 失衡 ⇒ GUI 卡死） | GOV-010 / PERF-001 | UI / PERF |
| `Languages/ChineseSimplified/Keyed/Archive.xml` | M | `PersonalChronicle.UI.Floor.ReadFailed` | 3018 | 新增失败文案（中英同步，占位符无） | GOV-010 / LOC-001 | LOC |
| `Languages/English/Keyed/Archive.xml` | M | 同上 | 3014 | 同上 | GOV-010 / LOC-001 | LOC |
| `docs/changelog/v1.4.x_2026-09-19_P0降级链与投影下沉.md` | A | `CHG-20260919-ECON-UI-008` | 1-130 | 更新日志：四组改动 + 4 项残余风险 + **实机验证建议表** | REL-001 / REL-004 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-008.md` | A | `CHG-20260919-ECON-UI-008` | 1-150 | 本文件自身（GATE-DELTA 要求标注覆盖**全部**实际改动文件，含标注自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步至发布区
> （MD5 `6D6F5561AE03325F1C215E868DFD1D7F`，两区一致）。

### 关键 diff 片段

```diff
// BuiltinLogisticsProvider.cs  （P0-A：只清自己追加的那段，异常转「未接入」）
+ int start = buffer.Count;      // ⚠ buffer 是全中心共用收集器，不能整表清空
  try
+ {
      AppendKpisCore(buffer, map);
+ }
+ catch (System.Exception ex)
+ {
+     // 组合层大 catch 会把整页置 IsEmpty ⇒ 一张卡出错就整页空白（本工程明令禁止）
+     ChronicleLog.Error(ChronicleLog.Category.Ui, "logistics kpis failed: " + ex);
+     if (buffer.Count > start) buffer.RemoveRange(start, buffer.Count - start);
+     AppendPendingKpis(buffer);
+ }
```

```diff
// WorkshopFloorViewProvider.cs  （P0-B：半截快照必须清掉；有意不置 NoMap）
  catch (Exception ex)
  {
      ChronicleLog.Error(ChronicleLog.Category.Ui, "Workshop floor snapshot failed: " + ex);
-     snapshot.Hint = T("PersonalChronicle.UI.Floor.NoMap");
+     snapshot.Bays.Clear(); snapshot.Devices.Clear(); snapshot.Workers.Clear();
+     snapshot.RawItems.Clear(); snapshot.Routes.Clear(); snapshot.Depot = new FloorDepotView();
+     snapshot.WorkshopCount = 0; snapshot.DeviceCount = 0; snapshot.Caption = string.Empty;
+     snapshot.IsEmpty = true;
+     snapshot.Hint = T("PersonalChronicle.UI.Floor.ReadFailed");
  }
```

```diff
// EconomyInventoryChainPanel.cs  （P1：投影下沉；UI 只按下标取，绘制期零分配）
- List<int> chains = ChainNodes(snapshot);                  // 每帧遍历 Edges + new List
+ List<int> chains = snapshot.ChainNodeIndices;
- List<EconomyFlowEdge> upEdges = new List<EconomyFlowEdge>();
- List<EconomyFlowEdge> dnEdges = new List<EconomyFlowEdge>();
- CollectEdges(snapshot, upEdges, dnEdges);                 // 分类 + 排序 + 截断 + 委托分配
- DrawChainColumn(upCol, snapshot.UpstreamLabel, upEdges, false, snapshot);
+ DrawChainColumn(upCol, snapshot.UpstreamLabel, snapshot.UpstreamEdgeIndices, false, snapshot);
  ...
- DrawChainColumn(dnCol, snapshot.DownstreamLabel, dnEdges, true, snapshot);
+ DrawChainColumn(dnCol, snapshot.DownstreamLabel, snapshot.DownstreamEdgeIndices, true, snapshot);
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 003~007 批次遗留文件由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致（`CHG-20260919-ECON-UI-008`）
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留（下游 `ChainNodes`/`Mark`/`CollectEdges`/`DownstreamTop` **已整体删除**，非留空）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录
| 事实 | 求证方式 | 结论 |
|---|---|---|
| 组合层的大 catch 会整页置空 | 直读 `SystemCenterReadModelProvider.cs:230-233`（调用点在整体 try 内）+ `:404-408`（唯一 catch 置 `IsEmpty=true`） | ✅ 证实 |
| 三个 `AppendKpis` 无保护 | 直读三家源码 | ✅ 证实 |
| 车间俯视的 UI 门控是 `!NoMap` | 检索 `MasterControlTabWindow.Detail.cs` 得 `:149 / :303 / :381` 三处 | ✅ 证实（决定"不置 NoMap"的处置） |
| `AppendKpisCore` 必须是实例方法 | 编译期发现 `CS0120`（`CollectProducedQuality` 非静态） | ✅ 已改 |
| `AllBuildingsColonistOfClass<T>()` 返回 `IEnumerable<T>` 不是 `List<T>` | 编译期发现 `CS0266` | ✅ 已改回 `foreach` |

### 回滚定位
1. **只退 P1**：删快照三字段 + 恢复面板三个方法（**不建议** —— 会重新引入绘制期分配与排序）。
2. **只退 P0-A/B/C**：删各 catch 段。**强烈不建议** —— 那会恢复"一处异常整页空白/GUI 卡死"。
3. **运行时止损**：无。本批只改异常路径与构建期投影，正常路径行为不变。

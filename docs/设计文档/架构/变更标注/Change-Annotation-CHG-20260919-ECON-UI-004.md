---
change_id: CHG-20260919-ECON-UI-004
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Economy/UI/**, Source/PersonalChronicle/Application/**, Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Economy.cs, docs/**]
files:
  - path: Source/PersonalChronicle/Economy/UI/EconomyMarketPanel.cs
    type: M
    symbols: EconomyMarketPanel.MaxDrawRows / EconomyMarketPanel.CollapsedMark / EconomyMarketPanel.ExpandedMark / EconomyMarketPanel.MembersIndent / EconomyMarketPanel.visibleRowParent / EconomyMarketPanel.visibleRowChild / EconomyMarketPanel.visibleRowCount / EconomyMarketPanel.visibleParentRows / EconomyMarketPanel.Draw / EconomyMarketPanel.DrawTable / EconomyMarketPanel.DrawVisibleTable / EconomyMarketPanel.BuildVisibleRows / EconomyMarketPanel.BuildTableCells / EconomyMarketPanel.ComposeCommodityCell / EconomyMarketPanel.HasMembers / EconomyMarketPanel.HandleRowClicks / EconomyMarketPanel.DrawRowHover / EconomyMarketPanel.DrawFallback / EconomyMarketPanel.DrawGateHint / EconomyMarketPanel.EnsureBuffers
    lines: 88-175,230-560,640-700
  - path: Source/PersonalChronicle/Application/ReadModels/EconomyMarketPanelSnapshot.cs
    type: M
    symbols: EconomyMarketRow.MemberNames
    lines: 110-135
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs
    type: M
    symbols: BuiltinEconomyProvider.AppendMembers / BuiltinEconomyProvider.JoinMemberNames
    lines: 220-300
  - path: Source/PersonalChronicle/MasterControl/MasterControlTabWindow.Economy.cs
    type: M
    symbols: MasterControlTabWindow.economyMarketExpanded / MasterControlTabWindow.DrawEconomyGridPage
    lines: 1-4,55-75,135-142
  - path: docs/changelog/v1.4.x_2026-09-19_市场面板点击展开.md
    type: A
    symbols: CHG-20260919-ECON-UI-004
    lines: 1-110
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-004.md
    type: A
    symbols: CHG-20260919-ECON-UI-004
    lines: 1-140
---

# 变更标注 · CHG-20260919-ECON-UI-004（市场面板点击展开）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-ECON-UI-004`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-ECON-UI-004
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent，先加载「RimWorld组件绘制」规范 → 读调用点确认状态归属 → 实现）
- Scope(摘要): 市场面板品种列新增**点击展开/收起成员物品**交互（含可见行映射、命中检测、状态归属）

### 一句话动机
用户实测反馈「品种列的组件，点击如基础原料展开木材、石材等等」。展开同时消除了
上一轮暴露的两个残余问题：列宽临界导致物品名被硬切、以及 **RUIF 渲染路径不回传行矩形
⇒ 全量清单此前没有任何可见途径**。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `Economy/UI/EconomyMarketPanel.cs` | M | `MaxDrawRows`(48→256) · `CollapsedMark`/`ExpandedMark`/`MembersIndent` · `visibleRowParent`/`visibleRowChild`/`visibleRowCount`/`visibleParentRows` · `Draw`(+`expandedRows` 形参) · `DrawTable` · `DrawVisibleTable`(新) · `BuildVisibleRows`(新) · `BuildTableCells`(新，从 `DrawTableBody` 提取) · `ComposeCommodityCell`(新) · `HasMembers`(新) · `HandleRowClicks`(新) · `DrawRowHover`(新) · `DrawFallback`(改走映射) · `DrawGateHint`(判据改品种行数) · `EnsureBuffers`(+映射数组) | 88-175,230-560,640-700 | 引入**可见行映射**（父行/子行）；命中检测落在**内容坐标系**；展开时隐去摘要；子行降调；越界降级不抛异常；`+N` 闸门判据修正 | UI-001 / LAND-001 / LAND-012 / PERF-001 / GOV-010 | UI / PERF |
| `Application/ReadModels/EconomyMarketPanelSnapshot.cs` | M | `EconomyMarketRow.MemberNames` | 110-135 | 新增**全量成员物品名列表**（已本地化、有序），供展开逐行渲染；与 `MemberText`/`MemberTipText` 同源同序 | DATA-001 / GOV-010 | DATA / UI |
| `Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs` | M | `AppendMembers` · `JoinMemberNames`（取代 `JoinThingNames`） | 220-300 | **一次取名、三处共用**：先填 `MemberNames`，再由它派生悬停全量与折叠摘要（分头各取会出现"摘要里有、展开里没有"的不一致） | GOV-010 / PERF-001 | DATA / LOC |
| `MasterControl/MasterControlTabWindow.Economy.cs` | M | `economyMarketExpanded`（新字段） · `DrawEconomyGridPage`（调用点） | 1-4,55-75,135-142 | 展开态**归调用方**（窗口实例字段，与既有 `economyMarketScroll` 同规）；新增 `using System.Collections.Generic;` | ARC-008 / GATE-UI | UI / ARCH |
| `docs/changelog/v1.4.x_2026-09-19_市场面板点击展开.md` | A | `CHG-20260919-ECON-UI-004` | 1-110 | 更新日志：设计要点 8 条 + **8 项降级/风险诚实登记**（含 a11y 缺口、RUIF 带色列无法按行关闭） | REL-001 / REL-004 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-004.md` | A | `CHG-20260919-ECON-UI-004` | 1-140 | 本文件自身（GATE-DELTA 要求标注覆盖**全部**实际改动文件，含标注自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步至发布区
> （MD5 `2F254CBD743C95E779CA3B8A5E746162`，两区一致），但门禁改动集不含二进制，只能在标注正文说明。

### 关键 diff 片段

```diff
// MasterControlTabWindow.Economy.cs  （状态归调用方 —— 绘制规范「第一原则」第 3 条）
+ /// <summary>市场面板已展开的品种行下标（会话内表现态，不入存档）。
+ /// 与 economyMarketScroll 完全同规：放窗口实例字段而不是面板静态字段
+ /// （GATE-UI：UI State 不持持久状态；ARC-008：避免静态可变状态）。</summary>
+ private readonly HashSet<int> economyMarketExpanded = new HashSet<int>();
- EconomyMarketPanel.Draw(market, detail.EconomyMarket, ref economyMarketScroll);
+ EconomyMarketPanel.Draw(market, detail.EconomyMarket, ref economyMarketScroll, economyMarketExpanded);
```

```diff
// EconomyMarketPanel.cs  （命中检测必须在**内容坐标系**里做 —— 否则滚动后整体偏移）
+ HandleRowClicks(content, snapshot, count, rowH, expandedRows);   // content = BeginScrollView 提供的坐标系
+ BuildVisibleRows(snapshot, expandedRows);                        // 点击可能改变展开态 ⇒ 同帧重建
+ ... 绘制 ...
+ DrawRowHover(content, snapshot, rows, rowH, expandedRows);       // 高亮画在内容之后（半透明叠加）
```

```diff
// EconomyMarketPanel.cs  （可见行映射：绘制行 ≠ 品种行）
+ visibleRowParent[visibleRowCount] = i;  visibleRowChild[visibleRowCount] = -1;  // 父行
+ for (int m = 0; m < row.MemberNames.Count && visibleRowCount < MaxDrawRows; m++)
+ { visibleRowParent[visibleRowCount] = i; visibleRowChild[visibleRowCount] = m; visibleRowCount++; }  // 子行
```

```diff
// EconomyMarketPanel.cs  （闸门判据修正：可见行数含子行，减品种总数会得负数 ⇒ 提示永久消失）
- int hidden = snapshot.Rows.Count - count;
+ int hidden = snapshot.Rows.Count - visibleParentRows;
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit；标注自身在写入后入列）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 范围外历史未提交工作由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致（`CHG-20260919-ECON-UI-004`）
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留（`DrawTableBody` 已被 `DrawVisibleTable` + `BuildTableCells` 取代，旧方法已删除而非留空）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 交互四问自检（绘制规范「第一原则」）
| # | 问题 | 本次答案 |
|---|---|---|
| 1 | 谁触发？ | `Widgets.ButtonInvisible`，命中区 = 品种列单元格，仅父行、仅有成员者 |
| 2 | 触发后返回什么？ | `Draw` 无返回值（面板级）；改的是调用方持有的 `expandedRows` |
| 3 | 状态存在哪？ | **调用方窗口实例字段**（不落盘、不进快照、不做面板静态字段） |
| 4 | 没有鼠标怎么用？ | ⚠ **缺口** —— 五面板均无键盘路径，加导航需跨面板焦点环设计，**登记不假装解决** |

### 回滚定位
1. **退回无交互**：调用点传 `null` 即可（面板对 `expandedRows == null` 全程守卫）——一行改动。
2. **退回纯表格**：删 `HandleRowClicks` 的调用 + 恢复 `DrawTableBody` 索引形态。
3. **运行时止损**：`MaxDrawRows` 改回 48（展开后超出部分被闸门挡住，功能降级但不崩）。

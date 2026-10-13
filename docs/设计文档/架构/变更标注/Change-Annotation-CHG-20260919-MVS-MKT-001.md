---
change_id: CHG-20260919-MVS-MKT-001
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Application/**, Source/PersonalChronicle/Economy/UI/**, docs/**]
files:
  - path: Source/PersonalChronicle/Application/ReadModels/EconomyMarketPanelSnapshot.cs
    type: M
    symbols: EconomyMarketRow.MemberCount（原 MemberText 语义变更）
    lines: 80-110
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs
    type: M
    symbols: AppendMembers / MemberSampleCount（删）
    lines: 36-45, 340-370
  - path: Source/PersonalChronicle/Economy/UI/EconomyMarketPanel.cs
    type: M
    symbols: ComposeCommodityCell
    lines: 495-525
  - path: docs/changelog/v1.4.x_2026-09-19_市场面板分类行瘦身.md
    type: A
    symbols: CHG-20260919-MVS-MKT-001
    lines: 1-95
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-MKT-001.md
    type: A
    symbols: CHG-20260919-MVS-MKT-001
    lines: 1-85
---

# 变更标注 · CHG-20260919-MVS-MKT-001（市场面板分类行瘦身）

**Change ID：** `CHG-20260919-MVS-MKT-001` · **Version：** v1.4.x · **Date：** 2026-09-19
**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物

## Change Annotation
- 用户：「优化市场面板，**一级分类标题含有二级品种名字**导致文本过多」
- Scope(摘要): 分类行不再拼成员名摘要，只保留成员数；成员名移到展开行与悬停（补偿不变）

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|
| `Application/ReadModels/EconomyMarketPanelSnapshot.cs` | M | `MemberText` → `MemberCount` | **字段名与类型一起改**（语义变了名字必须跟着走）；文档写明"信息移位≠信息减少" | DATA-001 / UI-001 | DATA |
| `.../BuiltinEconomyProvider.MarketPanel.cs` | M | `AppendMembers` / 删 `MemberSampleCount` | 删"摘要拼接"与**采样模式**（采样只服务摘要 ⇒ 失去用途即死代码） | PERF-001 / GOV-004 | DATA / PERF |
| `Economy/UI/EconomyMarketPanel.cs` | M | `ComposeCommodityCell` | 收起态**只拼计数**（`·12`）；`0` 时不拼（不显示 `·0`） | UI-001 / LAND-009 | UI |
| `docs/changelog/v1.4.x_2026-09-19_市场面板分类行瘦身.md` | A | `CHG-...-MKT-001` | 更新日志（含"调参调不动往往是层级问题"的教训） | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-MKT-001.md` | A | `CHG-...-MKT-001` | 本文件自身（自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**：`Assemblies/PersonalChronicle.dll` 已重编并同步
> （MD5 `569A75DE303F4A3909D8673D02284123`，两区一致）。

### 关键内容片段

```diff
// EconomyMarketPanel.ComposeCommodityCell
- if (!expanded && !string.IsNullOrEmpty(row.MemberText))
-     text = text + "·" + row.MemberText;                 // 「基础原料·钢铁/布/零部件 +15」
+ if (!expanded && row.MemberCount > 0)
+     text = text + "·" + row.MemberCount.ToString(InvariantCulture);   // 「基础原料·12」
```

```diff
// BuiltinEconomyProvider.MarketPanel.AppendMembers —— 删采样
- int take = Math.Min(row.Members.Count, MemberSampleCount);
- row.MemberText = take >= row.Members.Count ? full : JoinMemberNames(row.Members, take) + " +" + (…);
+ row.MemberCount = row.Members.Count;
+ row.MemberTipText = JoinMemberNames(row.Members, row.Members.Count);
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件（脚本 MINE 与标注 `files:` **已逐条人工对齐**）
- [x] 清单范围 ⊆ Change Summary.Scope
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] **无死代码**：`MemberSampleCount` 与采样模式已删
- [x] 无临时文件残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录
| 事实 | 求证方式 | 结论 |
|---|---|---|
| 分类行文本由谁拼 | 读 `EconomyMarketPanel.ComposeCommodityCell` | ⚠ 收起态把 `row.MemberText`（名字摘要）拼进分类行 ⇒ 用户所述现象的直接成因 |
| 成员信息还有哪些出口 | 读 `DrawRow` 的悬停挂载 + `HasMembers` 的展开符 | ✅ 展开逐行（`Members`）+ 悬停全量（`MemberTipText`）**都存在** ⇒ 去掉可见摘要**不构成静默丢弃** |
| 采样逻辑是否还有别的消费方 | 检索 `MemberSampleCount` | 仅 `AppendMembers` 一处 ⇒ 可整块删除 |

### 设计判断：**保留「数」而不是全删**
分类行若只剩名字，玩家无从判断"这个品种是一条还是二十条"；
而**展开符只说明"可展开"，不说明"有多少"**。故保留 2~3 字符的计数：
信号不丢、宽度几乎不占、且不与分类名争宽。

### 📌 提炼的教训（写进 Changelog）
该处曾有一轮**实测定标**（`MemberSampleCount` 3 → 2）。但调参治不了本：
**问题不是"取几个名字"，而是"名字根本不该出现在一级标题里"**。
⇒ 当一段文案反复调参仍被实测证伪时，应质疑**它该不该出现在这个位置**，而不是继续调参。

### 回滚定位
1. **只回文案**：`ComposeCommodityCell` 改回拼 `MemberText` 形态的字符串（需同时恢复 Provider 的采样拼接与字段）。
2. **运行时止损**：无 —— 纯渲染文案，不触存档、不改游戏状态、不动任何数值。

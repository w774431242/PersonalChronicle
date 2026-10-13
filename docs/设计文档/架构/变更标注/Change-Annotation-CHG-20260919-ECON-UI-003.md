---
change_id: CHG-20260919-ECON-UI-003
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/MVS/Data/**, Source/PersonalChronicle/Application/**, Source/PersonalChronicle/Economy/UI/**, Tests/EconomyMarketFormatTests.cs, Languages/**, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Data/CommodityMemberPicker.cs
    type: A
    symbols: CommodityMemberPicker.PickTop / CommodityMemberPicker.MemberCountOf / CommodityMemberPicker.MarketValueBaseOf
    lines: 1-130
  - path: Source/PersonalChronicle/MVS/Data/MVSCommodityRegistry.cs
    type: M
    symbols: MVSCommodityRegistry.byCommodity / MVSCommodityRegistry.Rebuild / MVSCommodityRegistry.BuildReverse / MVSCommodityRegistry.ThingsOf / MVSCommodityRegistry.EmptyThings
    lines: 44-62,118-152,262-300
  - path: Source/PersonalChronicle/Application/ReadModels/EconomyMarketPanelSnapshot.cs
    type: M
    symbols: EconomyMarketRow.MemberText / EconomyMarketRow.MemberTipText
    lines: 82-108
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs
    type: M
    symbols: BuiltinEconomyProvider.MemberSampleCount / BuiltinEconomyProvider.AppendMembers / BuiltinEconomyProvider.JoinThingNames / BuiltinEconomyProvider.BuildMarketPanel
    lines: 32-56,96-104,216-300
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FlowGraph.cs
    type: M
    symbols: BuiltinEconomyProvider.BuildFlowGraph / BuiltinEconomyProvider.AppendSideSections(删除)
    lines: 47-58,300-325
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FactionDetail.cs
    type: M
    symbols: BuiltinEconomyProvider.BuildFactionDetail
    lines: 24-32,62-72
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FactionStatus.cs
    type: M
    symbols: BuiltinEconomyProvider.AppendSelectorChrome
    lines: 200-222
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs
    type: M
    symbols: BuiltinEconomyProvider.BuildSociety
    lines: 24-40,40-52,60-100
  - path: Source/PersonalChronicle/Economy/UI/EconomyMarketPanel.cs
    type: M
    symbols: EconomyMarketPanel.Shares / EconomyMarketPanel.DrawTableBody / EconomyMarketPanel.DrawFallback
    lines: 40-80,255-275,352-385
  - path: Source/PersonalChronicle/Economy/UI/EconomyInventoryChainPanel.cs
    type: M
    symbols: EconomyInventoryChainPanel.Draw
    lines: 60-85
  - path: Source/PersonalChronicle/Economy/UI/EconomyFactionSelectorPanel.cs
    type: M
    symbols: EconomyFactionSelectorPanel.Draw
    lines: 60-78
  - path: Tests/EconomyMarketFormatTests.cs
    type: M
    symbols: EconomyMarketColumnShareTests / LandedShares_HaveFiveColumns_AndDifferFromPreviewByDesign / CommodityColumn_IsWideEnoughForMemberSamples_ButNotGreedy / RuifBridge_NormalizesShares_ToUnitSum_PreservingRatio / SharesRatio_IsPreservedByNormalization_NoDisplayCompression
    lines: 196-330
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: MVS.Economy.Market.Sub / MVS.Economy.Society.Tier.Note / MVS.Economy.Flow.DownstreamLabel / MVS.Economy.FactionList.Group.Tradeable / MVS.Economy.FactionList.Group.Blocked
    lines: 117,195,275,298,299
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: MVS.Economy.Market.Sub / MVS.Economy.Society.Tier.Note / MVS.Economy.Flow.DownstreamLabel / MVS.Economy.FactionList.Group.Tradeable / MVS.Economy.FactionList.Group.Blocked
    lines: 110,189,269,292,293
  - path: docs/changelog/v1.4.x_2026-09-19_经济中心瘦身与品种列.md
    type: A
    symbols: CHG-20260919-ECON-UI-003
    lines: 1-160
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-003.md
    type: A
    symbols: CHG-20260919-ECON-UI-003
    lines: 1-200
---

# 变更标注 · CHG-20260919-ECON-UI-003（经济中心：品种列 + 瘦身）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-ECON-UI-003`（贯穿 Summary → Annotation → Gate → Changelog）
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-ECON-UI-003
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent，只读盘点 subagent + 主 Agent 实现）
- Scope(摘要): 经济中心五面板 —— ① 市场面板品种列改「分类 · 物品样本」② 裁掉六块设计说明 + 四处尾注 ③ 修两处布局死区

### 一句话动机
用户实测截图反馈两条：①「市场面板品种显示的是游戏分类而不是真实物品品种，建议分类-品种」
②「额外描述内容过多，保持经济系统中心的干净」。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 / DefName / Key | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `Source/PersonalChronicle/MVS/Data/CommodityMemberPicker.cs` | A | `CommodityMemberPicker.PickTop / MemberCountOf / MarketValueBaseOf` | 1-130 | 新建「品种→代表物品」选取器：按 `statBases` 的 `MarketValue` 基值降序（同值 `defName` 序数序）。⚠ `ThingDef.BaseMarketValue` 在 1.6 **已不存在**（Cecil 零命中）⇒ 唯一静态价格源。**不用静态 scratch**（避免 Def 访问层的可变静态状态） | ARC-001 / GOV-003 / PERF-001 | ARCH / DATA |
| `Source/PersonalChronicle/MVS/Data/MVSCommodityRegistry.cs` | M | `byCommodity` / `Rebuild` / `BuildReverse` / `ThingsOf` / `EmptyThings` | 44-62,118-152,262-300 | 新增反查索引 + 查询面。**反查由已剪枝的正向索引反向展开**（两表派生自同一真相 ⇒ 不可能静默不同集） | GOV-005 / PERF-001 | DATA |
| `Source/PersonalChronicle/Application/ReadModels/EconomyMarketPanelSnapshot.cs` | M | `EconomyMarketRow.MemberText` / `MemberTipText` | 82-108 | 加两个**已本地化**字段（摘要 / 悬停全量，同源同序）。仅加字段，**未改签名** | DATA-001 / GOV-010 | DATA / UI |
| `Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs` | M | `MemberSampleCount` / `AppendMembers` / `JoinThingNames` | 32-56,96-104,216-300 | ① `Footnote` 有意置空 ② 新增物品样本拼装（构建期一次算好；摘要=前 3+剩余计数，**不是截断**） | GOV-010 / PERF-001 / LAND-009 | UI / LOC |
| `Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FlowGraph.cs` | M | `BuildFlowGraph` / `AppendSideSections`（**删除**） | 47-58,300-325 | `ChainNote` 置空 + **整块删除**右侧四段设计说明填充方法（236px 固定宽）；留「为何删 + 如何恢复」登记注释 | GOV-013 / ANNO-001 | UI / REL |
| `Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FactionDetail.cs` | M | `BuildFactionDetail` | 24-32,62-72 | `FactorBadge`（"设计假设"）与 `EventNote`（66 字口径行）不再填充 | GOV-013 | UI / LOC |
| `Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.FactionStatus.cs` | M | `AppendSelectorChrome` | 200-222 | 删「可贸易判据」说明块（标题 + 4 行）+ 图例第三条（"悬停行看判定理由"）；保留两条颜色图例 | GOV-013 | UI / LOC |
| `Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs` | M | `BuildSociety` | 24-40,40-52,60-100 | `TraitsBadge` / `RestoreNote`（125 字）/ `PopulationNote`（×2）不再填充 | GOV-013 | UI / LOC |
| `Source/PersonalChronicle/Economy/UI/EconomyMarketPanel.cs` | M | `Shares` / `DrawTableBody` / `DrawFallback` | 40-80,255-275,352-385 | `Shares` **有意偏离预览** `{3.4,.9,.9,.9,.9}`（保住"指标列等宽"+"品种:指标 ∈[3.5,4.5]"两条不变式）；品种列拼 `MemberText`；D2 路径加 `Verse.TooltipHandler` 悬停 | LAND-001 / LAND-012 / GOV-003 | UI |
| `Source/PersonalChronicle/Economy/UI/EconomyInventoryChainPanel.cs` | M | `Draw` | 60-85 | **修布局死区**：`SideSections` 为空时侧栏宽归零、不画分隔竖线（原实现无条件扣 236px 并画线 ⇒ 裁掉说明后会留空白竖条） | LAND-002 / UI-001 | UI |
| `Source/PersonalChronicle/Economy/UI/EconomyFactionSelectorPanel.cs` | M | `Draw` | 60-78 | **修布局死区**：`listBottom` 的 gap 由恒定 `gap*2` 改为**按实际底部件数**扣（原实现在两者都空时空出 12px） | LAND-002 | UI |
| `Tests/EconomyMarketFormatTests.cs` | M | `EconomyMarketColumnShareTests` 及 4 个测试 | 196-330 | 列宽测试**基准由"对齐预览"改为"保住三条不变式"** —— 断言"等于另一个硬编码值"是同义反复，不构成回归护栏 | AI-009 / TEST-001 | TEST |
| `Languages/ChineseSimplified/Keyed/MVS.xml` | M | 5 个键的值（去尾注） | 117,195,275,298,299 | `Market.Sub` / `Society.Tier.Note` / `Flow.DownstreamLabel` / `FactionList.Group.{Tradeable,Blocked}` | LOC-001 / GOV-010 | LOC |
| `Languages/English/Keyed/MVS.xml` | M | 同上 5 键（**中英对称**） | 110,189,269,292,293 | 同上（英文侧） | LOC-001 / GOV-010 | LOC |
| `docs/changelog/v1.4.x_2026-09-19_经济中心瘦身与品种列.md` | A | `CHG-20260919-ECON-UI-003` | 1-160 | 更新日志：完整裁剪清单（6 块 + 4 处尾注）+ 6 项残余风险诚实登记 | REL-001 / REL-004 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-003.md` | A | `CHG-20260919-ECON-UI-003` | 1-200 | 本文件自身（GATE-DELTA 要求标注覆盖**全部**实际改动文件，含标注自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步至发布区
> （MD5 `1F33D87ED59D9A9D1EDF807EE8A0149B`，两区一致），但因门禁改动集不含二进制，只能在标注正文说明。

### 关键 diff 片段

```diff
// Source/PersonalChronicle/MVS/Data/MVSCommodityRegistry.cs  （反查由正向索引派生，杜绝两表不同集）
+ PruneEmpties(thingIndex);
+ byCommodity = BuildReverse(thingIndex);   // ← 在剪枝之后展开，保证与 AffectsDef 严格同集
```

```diff
// Source/PersonalChronicle/Economy/UI/EconomyMarketPanel.cs  （品种列 = 分类 · 物品样本）
- line[0] = string.IsNullOrEmpty(row.LevelText) ? row.Name : row.Name + row.LevelText;
+ line[0] = string.IsNullOrEmpty(row.LevelText) ? row.Name : row.Name + row.LevelText;
+ if (!string.IsNullOrEmpty(row.MemberText)) { line[0] = line[0] + " · " + row.MemberText; }
```

```diff
// Source/PersonalChronicle/Economy/UI/EconomyInventoryChainPanel.cs  （侧栏空 ⇒ 宽归零，不留空壳）
+ bool hasSide = snapshot.SideSections != null && snapshot.SideSections.Count > 0;
+ float sideWidth = hasSide ? Mathf.Min(SideWidth, body.width * 0.28f) : 0f;
+ Rect main = new Rect(body.x, body.y,
+     Mathf.Max(40f, body.width - (hasSide ? sideWidth + WrapGap : 0f)), body.height);
- Rect side = new Rect(body.xMax - sideWidth, body.y, sideWidth, body.height);
- DrawSide(side, snapshot);
+ if (hasSide) { DrawSide(new Rect(body.xMax - sideWidth, body.y, sideWidth, body.height), snapshot); }
```

```diff
// Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs
+ List<ThingDef> all = CommodityMemberPicker.PickTop(commodity, int.MaxValue);  // 只排一次序
+ row.MemberTipText = full;                                                    // 全量 → 悬停
+ row.MemberText = all.Count > take ? JoinThingNames(all, take) + " +" + (all.Count - take) : full;
```

### 实测复查修复（同日第二轮，**文件清单不变**）
用户交第二轮实机截图后追加的修复，**未新增/删除文件**（仍为 16 条），故上表文件清单继续有效：

| 文件 | 追加改动 |
|---|---|
| `Languages/{ChineseSimplified,English}/Keyed/MVS.xml` | `Mvs.Economy.Market.Col.Price` 值：`价格 · 涨幅` → `价格`（英 `Price · change` → `Price`）—— 6 字符表头在 0.9 份额列里放不下，实测被挤断 |
| `BuiltinEconomyProvider.MarketPanel.cs` | `MemberSampleCount` **3 → 2**（实测多行物品名被硬切且 `+N` 丢失 = 静默截断） |
| `EconomyMarketPanel.cs` | 分隔符 ` · ` → `·`（换回约 1 个汉字宽） |

**同轮更正**：上一轮"汇总数字与核算值有差"的判断系**我读图错误**，第二轮截图逐项吻合
（2,849 / 3,656 / −807.2 / 1.28 ▲+28% vs 核算 2848.90 / 3656.10 / −807.20 / 1.2833）⇒ 汇总口径正确。

**登记未修（待裁定）**：① RUIF 路径下未读因子的 `—` 带绿（`BandColor` 中档色，语义误导，跨仓库改动）；
② 数据级别码 `①②③` 疑似在 RUIF 路径渲染为非圈码（**未定论**，需实机确认一眼）。

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏 —— 生成脚本自检 `annotation declared = 16`，其中 15 条在生成时已在 git status、第 16 条为本文件自身（写入后即入列）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 范围外 905 条历史未提交工作由 `-ExcludeFromFile .codebuddy/tmp/econ-ui-003-exclude.txt` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致（`CHG-20260919-ECON-UI-003`）
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 有意偏离预览的登记（LAND-001 例外面）
本次**两处**有意不再 1:1 对齐预览，均由**用户实测反馈直接裁定**：
1. **市场面板 `Shares`**：`{2.385,1,1,1,1.001}` → `{3.4,0.9,0.9,0.9,0.9}`（品种列需容纳物品样本）。
   已在数组注释里写死「本数组自此是**用户裁定的设计值**，不要拿预览 124:52 来纠正它」。
2. **六块设计说明 + 四处尾注被移除**：预览里存在的说明块，按用户「保持干净」裁定移除。
   Keyed 键**全部保留**（不删资产），每个裁剪点在代码里留「为何裁 + 如何恢复」注释。

### 回滚定位
1. **恢复某一块说明**：在对应 Provider 里把被置空的赋值/方法加回（每处都有注释指明键名）。
   `FlowGraph` 的 `AppendSideSections` 结构可从 Git 历史取回。
2. **恢复列宽**：把 `EconomyMarketPanel.Shares` 改回 `{2.385f, 1f, 1f, 1f, 1.001f}`，
   并同步回滚 `Tests/EconomyMarketFormatTests.cs` 的 4 个列宽测试。
3. **恢复品种列纯分类名**：删 `EconomyMarketPanel` 里那段 `MemberText` 拼接（一行）。

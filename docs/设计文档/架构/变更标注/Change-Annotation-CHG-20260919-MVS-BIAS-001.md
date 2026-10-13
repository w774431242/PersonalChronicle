---
change_id: CHG-20260919-MVS-BIAS-001
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/MVS/**, Source/PersonalChronicle/Application/SystemCenters/**, Tests/**, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Domain/TradeBias.cs
    type: A
    symbols: TradeBiasData / TradeBias.ForFaction / Clamp
    lines: 1-140
  - path: Source/PersonalChronicle/MVS/Data/FactionScaleReader.cs
    type: M
    symbols: Tuning（抽出）/ TuningBiasMin / TuningBiasMax
    lines: 78-115
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs
    type: M
    symbols: BuildMarketPanel（bias 计算 / supplyView / demandView / balance / 汇总）
    lines: 98-185
  - path: Tests/TradeBiasTests.cs
    type: A
    symbols: TradeBiasTests（8 例）
    lines: 1-160
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/SDD_派系画像人数因子接入.md
    type: A
    symbols: SDD（Draft v0.4）
    lines: 1-280
  - path: docs/changelog/v1.4.x_2026-09-19_派系供需偏置.md
    type: A
    symbols: CHG-20260919-MVS-BIAS-001
    lines: 1-140
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/裁决包_T3与P0.md
    type: A
    symbols: 决策 T3 / 决策 P0
    lines: 1-130
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-BIAS-001.md
    type: A
    symbols: CHG-20260919-MVS-BIAS-001
    lines: 1-140
---

# 变更标注 · CHG-20260919-MVS-BIAS-001（派系偏好偏置接入供需列）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-MVS-BIAS-001`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-MVS-BIAS-001
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent；落地对象是**用户此前已拍板**的上游设计 §10-2）
- Scope(摘要): 供应/需求两列改为该派系视角（画像乘子），净值与汇总同步改用偏置后值

### 一句话动机
用户指示「继续」⇒ 落地 §10-2。**同时纠正我上一轮的判断错误**：
偏置作用于**供需**（§10 原文："该派系视角下的相对供需强度"），不是价格 ——
而供需基数本就是 ③ 且已带 ③ 标记 ⇒ **不污染任何真实读数**。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `MVS/Domain/TradeBias.cs` | A | `TradeBiasData` · `ForFaction` | 1-140 | 纯函数：供应＝`ProductionFactor`、需求＝`TradeImportFactor`，按 Def 区间收口；含**四系数去向的逐条论证** | GOV-006 / GOV-004 | ARCH / DATA |
| `MVS/Data/FactionScaleReader.cs` | M | `Tuning` · `TuningBiasMin/Max` | 78-115 | 抽出 `Tuning()` 消除三处重复取 Def；暴露偏置区间（**来自 Def，非硬编码**） | GOV-006 / DATA-007 | DATA |
| `.../BuiltinEconomyProvider.MarketPanel.cs` | M | `BuildMarketPanel` | 98-185 | 循环外算一次 bias；`supplyView`/`demandView`；**净值用偏置后两值相减**；汇总累加偏置后值 | GOV-003 / DATA-001 / LAND | DATA / UI |
| `Tests/TradeBiasTests.cs` | A | 8 例 | 1-160 | 含**头号断言**「非法输入 ⟹ 乘子恒 1.0（不是 0）」+ 两个真实画像的**方向自检**（部落净进口 / 帝国净出口） | GOV-011 | TEST |
| `docs/设计文档/.../SDD_派系画像人数因子接入.md` | A | Draft v0.4 | 1-280 | `REQ-MVS-003` 标记已实现；登记**v0.3 判断错误的纠正**与四系数去向 | SDD-002 / SDD-009 | SDD |
| `docs/changelog/v1.4.x_2026-09-19_派系供需偏置.md` | A | `CHG-20260919-MVS-BIAS-001` | 1-140 | 更新日志：判断纠错 / 四系数去向表 / 两条"必须用偏置后值"的算术 / 方向自检 | REL-001 | REL |
| `docs/设计文档/功能模块/经济体系/市场经济框架/裁决包_T3与P0.md` | A | 决策 T3 / 决策 P0 | 1-130 | **决策支持文档（同批产出，非代码）**：把两个悬置决策做成可一句话拍板的形式，含本轮实测影响面数据（原版 `TraderKindDef` = 20 个 / `Tradeable.GetPriceFor` 是 `public virtual` 且被 5 处消费）与明确推荐。**不含实现授权** | REQ-002 / GOV-012 | REQ / REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-BIAS-001.md` | A | `CHG-20260919-MVS-BIAS-001` | 1-140 | 本文件自身（标注须覆盖全部改动文件，含自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 本批无 Keyed / 无 Def / 无 XML 改动** —— 复用既有 `MVS_FactionScaleTuning.tradeBiasMin/Max`
> （T2 批次已随 Def 一并登记该字段），且**有意不加新文案**（见下"呈现决策"）。
> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步
> （MD5 `DAB3027A174E200909CD566E184CAAA2`，两区一致）。

### 关键 diff 片段

```diff
// BuiltinEconomyProvider.MarketPanel.cs —— 只乘供需，不碰价格因子
+ float supplyView = commodity.supplyBaseline * bias.SupplyFactor;
+ float demandView = commodity.demandBaseline * bias.DemandFactor;
- row.SupplyText = FormatRate(commodity.supplyBaseline);
- row.DemandText = FormatRate(commodity.demandBaseline);
- float balance = commodity.supplyBaseline - commodity.demandBaseline;
+ row.SupplyText = FormatRate(supplyView);
+ row.DemandText = FormatRate(demandView);
+ float balance = supplyView - demandView;   // ⚠ 必须用偏置后两值，否则同屏自相矛盾
  ...
- totalSupply += commodity.supplyBaseline;
- totalDemand += commodity.demandBaseline;
+ totalSupply += supplyView;                 // ⚠ 否则汇总与各行对不上账
+ totalDemand += demandView;
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— T1a/SCALE-001 及更早批次由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无调试代码残留 / 死代码
- [x] 无临时文件残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| 偏置的作用对象到底是供需还是价格 | **重读** `方案/市场经济框架_派系建模扩展设计方案.md` §10-2 原文 | ✅ 「该派系视角下的**相对供需强度**」⇒ **供需**（上一轮我记成价格，属**记忆误差**，已纠正） |
| 供需基数是否本来就是 ③ | 读 `MVS_Commodities.xml` 全部条目 + `MVSCommodityDef` 字段注释 | ✅ 18 条全带 `dataLevel`，且 `supplyBaseline` 注释写明"符号参数…全部待游戏内校准" |
| 行内是否已带 ③ 标记 | 读 `MarketPanel` 的 `row.LevelText = EconomyMarketFormat.LevelMark(commodity.dataLevel)` | ✅ 已带 ⇒ 偏置**无需**新增标注 |
| 偏置区间来自何处 | 读 `MvsFactionScaleTuningDef`（T2 批次已登记 `tradeBiasMin/Max`） | ✅ 0.5 / 2.0，Def 可配 ⇒ 不硬编码 |

### ⚠ 有意为之的两项（非遗漏）
1. **不加"含偏置"字样到 UI**：供需列本就带 ③ 标记，且用户此前明确要求面板保持干净
   （`MVS.Economy.Market.Footnote` 已按其反馈清空）。透明度由 ③ 标记 + 派系选中上下文承担。
   ⇒ 若要更显式说明，`Footnote` 键仍在语言包里，**一行可恢复**。
2. **`LuxuryPreference` / `ShockSensitivity` 未参与**：前者需"品种是否奢侈"轴（数据层不存在，
   手工声明 = 编造）；后者是**响应速度非强度**（乘进供需属量纲错误）。理由已写进代码注释。

### 回滚定位
1. **只退偏置**：`MarketPanel` 把 `bias.SupplyFactor` / `bias.DemandFactor` 两处改回字面 `1f` ⇒ 回到 T1a 状态（一行级）。
2. **整体退 T1b**：删 `TradeBias.cs` 与 `TradeBiasTests.cs`，并移除 `FactionScaleReader.TuningBiasMin/Max`
   （`Tuning()` 抽出的重构可保留，它本身消除重复）。
3. **运行时止损**：无 —— 纯展示层数值投影，不触存档、不改任何游戏状态。

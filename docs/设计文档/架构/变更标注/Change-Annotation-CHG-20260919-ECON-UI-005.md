---
change_id: CHG-20260919-ECON-UI-005
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/Economy/UI/**, Source/PersonalChronicle/Application/**, Source/PersonalChronicle/MVS/Data/CommodityMemberPicker.cs, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Data/CommodityMemberPicker.cs
    type: M
    symbols: CommodityMemberPicker（类注释）
    lines: 15-40
  - path: Source/PersonalChronicle/Application/ReadModels/EconomyMarketPanelSnapshot.cs
    type: M
    symbols: EconomyMarketRow.Members / EconomyMarketMember
    lines: 100-145,165-200
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs
    type: M
    symbols: BuiltinEconomyProvider.BuildMarketPanel / BuiltinEconomyProvider.AppendMembers / BuiltinEconomyProvider.JoinMemberNames / BuiltinEconomyProvider.FormatMemberValue / BuiltinEconomyProvider.MemberValueCompactThreshold
    lines: 90-160,240-330
  - path: Source/PersonalChronicle/Economy/UI/EconomyMarketPanel.cs
    type: M
    symbols: EconomyMarketPanel.NoDataMark / EconomyMarketPanel.BuildTableCells / EconomyMarketPanel.BuildVisibleRows / EconomyMarketPanel.HasMembers
    lines: 130-155,420-510
  - path: docs/changelog/v1.4.x_2026-09-19_展开行数据与平台事实补正.md
    type: A
    symbols: CHG-20260919-ECON-UI-005
    lines: 1-120
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-005.md
    type: A
    symbols: CHG-20260919-ECON-UI-005
    lines: 1-130
---

# 变更标注 · CHG-20260919-ECON-UI-005（展开行补数据 + 平台事实补正）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-ECON-UI-005`（承接 004 的用户实测反馈）
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-ECON-UI-005
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent，先做 Cecil 取证解决与既存注释的矛盾 → 再实现 → 编译/单测/Gate）
- Scope(摘要): 市场面板展开行补**物品单价**；物品级不存在的三列改画 `—`；补正一条错误的平台注释

### 一句话动机
用户实测截图反馈「**没有细分数据**」：004 的展开功能生效了（逐行列出黄金/玻璃钢/铀…），
但四个指标列**全空**，等于展开出一堆光名字。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `MVS/Data/CommodityMemberPicker.cs` | M | 类注释 | 15-40 | **仅注释**：把「`BaseMarketValue` 已不存在」补正为「它是**兼容垫片属性**」，附访问器 IL 证据；并写明 `0` 的语义是「未声明」须映射成 `—` | GOV-013 / DATA-001 | DATA |
| `Application/ReadModels/EconomyMarketPanelSnapshot.cs` | M | `EconomyMarketRow.Members`（取代 `MemberNames`） · `EconomyMarketMember`（新 DTO） | 100-145,165-200 | 契约由 `List<string>` 换成对象列表（**避免平行数组**反模式）；新 DTO 承载 `Name` + `ValueText`，并在类注释说明**为何只有单价一列** | DATA-001 / GOV-010 | DATA / UI |
| `Application/SystemCenters/Builtin/BuiltinEconomyProvider.MarketPanel.cs` | M | `BuildMarketPanel`（调用位置后移） · `AppendMembers` · `JoinMemberNames` · `FormatMemberValue`（新） · `MemberValueCompactThreshold`（新） | 90-160,240-330 | `AppendMembers` **移到价格块之后**（否则拿到 `Factor` 默认值 0）；填单价 = `MarketValueBaseOf × row.Factor`，经 `GenText.ToStringMoney` 按量级分档格式化 | GOV-010 / PERF-001 / LAND-001 | DATA / PERF |
| `Economy/UI/EconomyMarketPanel.cs` | M | `NoDataMark`（新常量） · `BuildTableCells` · `BuildVisibleRows` · `HasMembers` | 130-155,420-510 | 子行填单价；三列改画 `—`（留空会被读成"渲染坏了"）；带色真值沿用父品种因子，文本**同时**交给 `bandTexts` 与 `line[4]`（两条渲染路径都要覆盖） | UI-001 / LAND-001 / GOV-010 | UI |
| `docs/changelog/v1.4.x_2026-09-19_展开行数据与平台事实补正.md` | A | `CHG-20260919-ECON-UI-005` | 1-120 | 更新日志：数据层真相（物品级供需不存在）+ IL 证据 + 4 项残余风险 | REL-001 / REL-004 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-ECON-UI-005.md` | A | `CHG-20260919-ECON-UI-005` | 1-130 | 本文件自身（GATE-DELTA 要求标注覆盖**全部**实际改动文件，含标注自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步至发布区
> （MD5 `799A6B84E97A89FA55F1869FFAD0E7CF`，两区一致）。

### 关键 diff 片段

```diff
// CommodityMemberPicker.cs  （平台事实补正 —— 旧结论是"检索方法用错导致的误判"）
- /// 在 1.6 **已不存在**（Mono.Cecil fieldsearch BaseMarketValue 零命中）⇒
+ /// **不是字段，而是一个兼容垫片属性**。fieldsearch 确实零命中（旧结论据此写成"已不存在"，
+ /// 属**检索方法用错导致的误判**），但读访问器的 IL 即可看清真相：
+ ///   get_BaseMarketValue()  =>  this.GetStatValueAbstract(StatDefOf.MarketValue, null)
+ ///   set_BaseMarketValue(v) =>  this.SetStatBaseValue(StatDefOf.MarketValue, v)
+ /// ⇒ 它与本方法读的是同一份数据（statBases 里的 MarketValue）—— 旧结论的**落点正确**，只是理由写错了。
```

```diff
// MarketPanel.cs  （调用位置后移到价格块之后 —— 放前面会拿到 Factor 的类型默认值 0）
+ // ⚠ 若按直觉放在价格块之前，拿到的是 `Factor` 的**类型默认值 0** ——
+ // 每个物品单价都会变成 0，而且**编译期完全看不出来**（float 无未初始化告警）。
+ AppendMembers(row, commodity);
  snapshot.Rows.Add(row);
```

```diff
// EconomyMarketPanel.cs  （子行：三列如实画 —，单价列填真实数据）
- line[0] = MembersIndent + row.MemberNames[childIndex];
- for (int c = 1; c < ColumnCount; c++) { line[c] = string.Empty; }
- bandValueBuffer[i] = BandReference;
- bandTextBuffer[i] = " ";
+ EconomyMarketMember member = row.Members[childIndex];
+ line[0] = MembersIndent + member.Name;
+ line[1] = NoDataMark; line[2] = NoDataMark; line[3] = NoDataMark;
+ line[4] = member.ValueText;
+ bandValueBuffer[i] = bandValue;          // 沿用父品种因子 ⇒ 同品种所有行同色
+ bandTextBuffer[i] = member.ValueText;    // RUIF 带色格不读 line[4]，必须同时给 bandTexts
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 004 批次遗留文件由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致（`CHG-20260919-ECON-UI-005`）
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（本次的两条，均为"先证后用"）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| `ThingDef.BaseMarketValue` 在 1.6 是否存在 | Mono.Cecil `type Verse.ThingDef full` + `il get_/set_` | **存在，但是属性**（兼容垫片：读写 `statBases/MarketValue`）；XML 元素 `<BaseMarketValue>` 全库 0 命中 |
| `GenText.ToStringMoney` 的重载形态 | Mono.Cecil `type Verse.GenText full` | `[public static] String ToStringMoney(Single f, String format)` —— **无单参重载**，必须传 format |
| `MoneyFormat` Keyed 的实际形态 | 检索游戏 `Data/Core/Languages/English/Keyed/Misc.xml` | `${0}` ⇒ 货币符号由游戏语言包决定，**不可硬编码** |

### 回滚定位
1. **退回 004 形态**：子行 `line[1..4] = string.Empty`（三列留空、无单价）——一处编辑。
2. **只退单价、保留 `—`**：`FormatMemberValue` 恒返回 `Dash`。
3. **退回平行数组**：不建议 —— 那会重新引入越界风险（绘制路径越界 ⇒ 整局 GUI 卡死）。

---
change_id: CHG-20260919-MVS-DEHOMO-003
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/MVS/**, Source/PersonalChronicle/Application/SystemCenters/**, Source/PersonalChronicle/Economy/UI/**, Languages/**, Tests/**, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Domain/FactionComposition.cs
    type: A
    symbols: FactionCompositionEntry / FactionComposition.TopShares / IsUsable
    lines: 1-165
  - path: Source/PersonalChronicle/MVS/Data/FactionCompositionReader.cs
    type: A
    symbols: FactionCompositionReader.TryRead
    lines: 1-95
  - path: Source/PersonalChronicle/MVS/Application/Contracts/IMarketFactorProvider.cs
    type: M
    symbols: IFactionProfileProvider.TryGetComposition
    lines: 41-90
  - path: Source/PersonalChronicle/MVS/Application/Adapters/FactionProfileAdapter.cs
    type: M
    symbols: TryGetComposition（+ using System.Collections.Generic）
    lines: 1-100
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs
    type: M
    symbols: 成员构成块（替换模板）/ KindLabelOf / ShareText / 移除 SocietyTierOf
    lines: 80-125, 250-300
  - path: Source/PersonalChronicle/Economy/UI/EconomySocietyPanel.cs
    type: M
    symbols: JoinMeta / 段头判空 / DrawTierRow meta 分支
    lines: 160-180, 295-390
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: Society.Tier.Title / Note（改）+ Tier.Percent / Tier.Other（增）
    lines: 196-212
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: Society.Tier.Title / Note（改）+ Tier.Percent / Tier.Other（增）
    lines: 188-204
  - path: Tests/FactionCompositionTests.cs
    type: A
    symbols: FactionCompositionTests（10 例）
    lines: 1-230
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/SDD_派系画像去同质化.md
    type: A
    symbols: REQ-MVS-007 追踪矩阵 / Change Log v0.3
    lines: 1-240
  - path: docs/changelog/v1.4.x_2026-09-19_成员构成反推.md
    type: A
    symbols: CHG-20260919-MVS-DEHOMO-003
    lines: 1-130
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-003.md
    type: A
    symbols: CHG-20260919-MVS-DEHOMO-003
    lines: 1-120
---

# 变更标注 · CHG-20260919-MVS-DEHOMO-003（社会结构：模板 → 真实反推）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-MVS-DEHOMO-003`
**Version：** v1.4.x（About 仍为 v1.4.0）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-MVS-DEHOMO-003
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent；用户实测反馈「其他派系的社会结构占比都是技术、生产者，完全不符合派系官方背景反推设定」）
- Scope(摘要): 社会结构段改为 `pawnGroupMakers` 两级权重的真实反推 + 口径标签修正 + 两处 UI 缺陷修复

### 一句话动机
面板画的「社会结构占比」是 **Keyed 里写死的模板**（所有非皇室派系一字不差、占比写死、`Count` 是破折号占位符）
⇒ 换成该派系 **`pawnGroupMakers` 两级权重**算出的真实成员构成。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `MVS/Domain/FactionComposition.cs` | A | `TopShares` 等 | 1-165 | **纯计算**：同键累加 → 归一 → 全序降序（平手按 defName）→ 截断 topN → 余项聚合。不可用 ⇒ **空列表，不兜底** | GOV-004 / DATA-007 | ARCH / TEST |
| `MVS/Data/FactionCompositionReader.cs` | A | `TryRead` | 1-95 | 读 Verse 字段 → 展平成 (键, 权重)；两级权重相乘；逐项守卫（null / NaN / 非正） | DATA-007 / PERF-001 | DATA |
| `MVS/Application/Contracts/IMarketFactorProvider.cs` | M | `TryGetComposition` | 41-90 | 接口**加方法**（不改既有方法语义）；文档写明"会失败、无兜底"与口径警告 | ARC-006 / ISP | ARCH |
| `MVS/Application/Adapters/FactionProfileAdapter.cs` | M | `TryGetComposition` | 1-100 | 薄实现 + `using System.Collections.Generic`；注释说明"两条独立降级链，不合并" | ARC-006 | ARCH |
| `.../BuiltinEconomyProvider.Society.cs` | M | 成员构成块 | 80-125 / 250-300 | **替换**硬编码模板循环；移除因此变成死代码的 `SocietyTierOf`；+`KindLabelOf` / `ShareText` | GOV-006 / UI-001 | UI / GOV |
| `Economy/UI/EconomySocietyPanel.cs` | M | `JoinMeta` 等 | 160-180 / 295-390 | ① `meta` 拼接**跳过空段**（修孤立分隔符）② 段头**随内容判空**（修假空态）③ 无 meta 不留空栏 | UI-001 / LAND-009 | UI |
| `Languages/*/Keyed/MVS.xml` | M | `Society.Tier.*` | ~196 / ~188 | Title「社会结构占比」→「**成员构成**」；Note →「**按出兵权重 · 合计 100%**」；+`Percent` / `Other` | GOV-010 / LOC-001 | LOC |
| `Tests/FactionCompositionTests.cs` | A | 10 例 | 1-230 | 算术 / 累加 / **决定性（平手全序）** / 截断聚合 / 降级**不兜底** / 两级权重 | GOV-011 | TEST |
| `docs/changelog/v1.4.x_2026-09-19_成员构成反推.md` | A | `CHG-...-003` | 1-130 | 更新日志：根因 / 新来源 / **口径修正** / 顺带修的 UI 缺陷 / **未反推项登记** | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-003.md` | A | `CHG-...-003` | 1-120 | 本文件自身（标注须覆盖全部改动文件，含自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步
> （MD5 `9BD66244511F48E5376810C3CC72B19B`，两区一致）。

### 关键内容片段

```diff
// BuiltinEconomyProvider.Society.cs —— 模板 → 真反推
- int tierCount = isRoyal ? 3 : 2;
- for (int i = 1; i <= tierCount; i++)
- {
-     string tier = pk + ".Tier" + i;
-     snapshot.Tiers.Add(SocietyTierOf(i, tier + ".Name", tier + ".Desc", tier + ".Percent", tier + ".Count"));
- }
+ List<FactionCompositionEntry> composition;
+ if (MarketContracts.Factions != null
+     && MarketContracts.Factions.TryGetComposition(selectedFactionDefName, out composition) && composition != null)
+ {
+     for (int i = 0; i < composition.Count; i++) { /* Name = 兵种名 / 「其余 N 项」；PercentText = 真占比 */ }
+ }
```

```diff
// EconomySocietyPanel.cs —— 修孤立分隔符
- string meta = tier.PercentText + " · " + tier.CountText;
+ string meta = JoinMeta(tier.PercentText, tier.CountText);   // 空段跳过
+ bool hasMeta = !string.IsNullOrEmpty(meta);
+ float metaWidth = hasMeta ? ... : 0f;
+ float metaX = hasMeta ? row.xMax - 9f - metaWidth : row.xMax - 9f;
- UIComponents.Label(... meta ...);
+ if (hasMeta) { UIComponents.Label(... meta ...); }
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 前序批次由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] **无死代码**：`SocietyTierOf` 随模板一起移除（不留注释掉的旧签名）
- [x] 无临时文件残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| `PawnGroupMaker` 权重字段 | Mono.Cecil `type RimWorld.PawnGroupMaker full` | ✅ `commonality` : `float`、`options` : `List<PawnGenOption>` |
| `PawnGenOption` 兵种与权重 | `type RimWorld.PawnGenOption full` | ✅ `kind` : `PawnKindDef`、`selectionWeight` : `float` |
| `PawnKindDef` 命名空间 | **编译报错纠正** | ⚠ 在 **`Verse`**（不是 `RimWorld`）—— 与 `PawnGroupMaker` 不同命名空间 |
| 旧模板的重复性 | 检索 Keyed | ✅ `Generic.Tier1/2` 对**所有非皇室派系**一字不差；`Count` 为 `—` 占位符 |

### ⚠ 实现期发现的两个既有 UI 缺陷（本批一并修）
1. `DrawTierRow` 的 `PercentText + " · " + CountText` 在只有一侧有值时画**孤立分隔符**，两侧皆空时更会凭空留一个 `" · "`。（原先两条模板恰好都填满 ⇒ 从未暴露。）
2. 段头**无条件绘制** ⇒ 该段不可用时会留下「有标题、无内容」的**假空态**。

### ⚠ 未做的事（如实登记）
- **人口阶级分层占比**：原版**无任何社会分层字段** ⇒ 该段整体改为「成员构成」，**不再声称是阶级分层**。
- **每兵种人数**：总人口不可知 ⇒「估算人数 × 占比」= 估算的估算（两层 ③）⇒ **只给占比**，人数列留空。
- 旧 `Faction.Royal/Generic.Tier*` 模板键**保留在语言包**（不删除可在需要时恢复）。

### 回滚定位
1. **只退文案**：改回 `Tier.Title` / `Tier.Note` 两键 ⇒ 标签回旧，但内容仍是真反推（会变成"标签与值不符"⇒ 不建议）。
2. **整体退本批**：Provider 恢复模板循环 + UI 恢复 `meta` 拼接 ⇒ 回到本批之前。
3. **运行时止损**：无 —— 本批是**纯 Def 只读 + 快照投射 + 一像素级布局修复**，不触存档、不改游戏状态。

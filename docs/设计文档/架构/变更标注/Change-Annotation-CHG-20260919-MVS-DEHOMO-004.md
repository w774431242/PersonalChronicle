---
change_id: CHG-20260919-MVS-DEHOMO-004
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [Source/PersonalChronicle/MVS/**, Source/PersonalChronicle/Application/SystemCenters/**, Languages/**, Tests/**, docs/**]
files:
  - path: Source/PersonalChronicle/MVS/Domain/FactionComposition.cs
    type: M
    symbols: CostWeightFactor / CostFractionX / CostFractionY / DefaultTopN
    lines: 40-175
  - path: Source/PersonalChronicle/MVS/Data/FactionCompositionReader.cs
    type: M
    symbols: TryRead（两遍扫描 + 成本因子）
    lines: 45-105
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.Society.cs
    type: M
    symbols: AppendDerivedChips（减法）/ GovernanceChipOf
    lines: 200-300
  - path: Source/PersonalChronicle/Application/SystemCenters/Builtin/BuiltinEconomyProvider.cs
    type: M
    symbols: TS2
    lines: 180-205
  - path: Languages/ChineseSimplified/Keyed/MVS.xml
    type: M
    symbols: Society.Trait.GovernanceBoth（+1）；NoTitles / ApparelLimited / ApparelOpen 转为保留
    lines: 116-136
  - path: Languages/English/Keyed/MVS.xml
    type: M
    symbols: Society.Trait.GovernanceBoth（+1）；同上三键转为保留
    lines: 108-128
  - path: Tests/FactionCompositionTests.cs
    type: M
    symbols: CostWeightFactor_MirrorsVanillaCurve / Reader_AppliesCostWeightFactor_CheapKindIsPenalised / KindWithCost
    lines: 180-230
  - path: docs/changelog/v1.4.x_2026-09-19_成员构成修正与面板精简.md
    type: A
    symbols: CHG-20260919-MVS-DEHOMO-004
    lines: 1-135
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-004.md
    type: A
    symbols: CHG-20260919-MVS-DEHOMO-004
    lines: 1-125
---

# 变更标注 · CHG-20260919-MVS-DEHOMO-004（成员构成原版对齐 + 面板精简）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-MVS-DEHOMO-004`
**Version：** v1.4.x（About 仍为 v1.4.0）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-MVS-DEHOMO-004
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent；用户要求一「社会背景面板保持干净化」、要求二「成员结构是否符合游戏原版数据？」）
- Scope(摘要): 补齐原版成本权重曲线（对齐原版公式）+ 面板 chips/构成行的三处减法

### 一句话动机
用户质询「是否符合原版数据」⇒ Cecil 取证发现**上一版漏了原版的成本权重曲线乘子**（廉价兵种原版只给 1% 权重，我们给了满权重）
⇒ 补上并逐点钉死；同时按「干净化」要求砍掉零信息/低信息项。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `MVS/Domain/FactionComposition.cs` | M | `CostWeightFactor` 等 | 40-175 | **镜像原版曲线**（3 点 + 端点钳制），附 **Cecil 依据与复核命令**；`DefaultTopN` 6→**4** | GOV-004 / DATA-007 | ARCH / TEST |
| `MVS/Data/FactionCompositionReader.cs` | M | `TryRead` | 45-105 | **两遍扫描**（先取该组最高成本）+ 三因子权重（`commonality × selectionWeight × CostWeightFactor`） | DATA-007 / PERF-001 | DATA |
| `.../BuiltinEconomyProvider.Society.cs` | M | `AppendDerivedChips` / `GovernanceChipOf` | 200-300 | **减法**：砍「无爵位」与「服装材料」、治理两枚合并为一枚 | UI-001 / GOV-010 | UI / LOC |
| `.../BuiltinEconomyProvider.cs` | M | `TS2` | 180-205 | 新增 2 字符串占位符的包装（**不走 `Translate(a,b)`**：1.6 传 string 会落到已废弃重载） | GOV-010 | LOC |
| `Languages/*/Keyed/MVS.xml` | M | `Society.Trait.*` | ~116 / ~108 | +1 键（`GovernanceBoth`）；3 键**转为保留**并注明原因 | GOV-010 / LOC-001 | LOC |
| `Tests/FactionCompositionTests.cs` | M | +2 例 | 180-230 | ① **曲线逐点钉死**（含端点钳制、线性中点、NaN 中性）② 读取器**确实应用了**成本因子 | GOV-011 | TEST |
| `docs/changelog/v1.4.x_2026-09-19_成员构成修正与面板精简.md` | A | `CHG-...-004` | 1-135 | 更新日志：**取证链路 + 结论「上一版不符合」+ 仍不等的部分 + 减法判据** | REL-001 | REL |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-004.md` | A | `CHG-...-004` | 1-125 | 本文件自身（标注覆盖全部改动文件，含自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 二进制产物不进 `files:`**（GATE-DELTA 禁令①）：`Assemblies/PersonalChronicle.dll` 已重编并同步
> （MD5 `86F128BDA01E3D04E2E63A0C1ECDAACA`，两区一致）。

### 关键内容片段

```diff
// FactionCompositionReader.cs —— 补上漏掉的第三个因子
+ float highestCost = ...;   // 该集团全部选项的最高成本（两遍扫描）
+ float costFactor = highestCost > 0f
+     ? FactionComposition.CostWeightFactor(option.Cost / highestCost) : 1f;
- weights.Add(group.commonality * w);
+ weights.Add(group.commonality * w * costFactor);
```

```diff
// FactionComposition.cs —— 镜像原版曲线（private static，运行时读不到）
+ private static readonly float[] CostFractionX = { 0.2f, 0.3f, 0.5f };
+ private static readonly float[] CostFractionY = { 0.01f, 0.30f, 1f };
+ public static float CostWeightFactor(float costFraction) { /* 端点钳制 + 分段线性 */ }
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件（**上次栽在这里**：脚本清单与标注 `files:` 是两份，已逐条人工对齐）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无死代码 / 无调试残留 / 无临时文件
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用 —— 本次是**质询驱动**的取证）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| 集团权重 = `commonality` | `il RimWorld.PawnGroupMakerUtility RandomPawnGroupMaker full` + `il ".../<>c" b__4_1 full` | ✅ 权重 lambda 的 IL **只有一条** `ldfld PawnGroupMaker::commonality` |
| 兵种权重 = `selectionWeight × 成本曲线` | `il ... ChoosePawnGenOptionsByPoints full` + `il ".../<>c__DisplayClass9_0" b__0 full` | ⚠ **不是**裸 `selectionWeight` —— 还有 `PawnWeightFactorByMostExpensivePawnCostFractionCurve.Evaluate(Cost/highestCost)` |
| 曲线三点与端点语义 | `il RimWorld.PawnGroupMakerUtility cctor full` + `il Verse.SimpleCurve Evaluate full` | ✅ (0.2,0.01) (0.3,0.30) (0.5,1)；区间外**钳制**（非外推） |
| `PawnGenOption.Cost` 的来源 | `il RimWorld.PawnGenOption Cost full` | ✅ `get_Cost` = `ldfld kind.combatPower`（直读） |
| 是否反复抽取 | `ChoosePawnGenOptionsByPoints` 的 `Add` + `Cost` + 循环结构 | ✅ 是（每抽一个扣其 Cost，直到点数耗尽） |
| `SimpleCurve` 的命名空间 | **`NOT FOUND: RimWorld.SimpleCurve`** | ⚠ 在 **`Verse`**（与 `PawnKindDef` 同类坑，本日第二次踩） |

### ⚠ 仍然不等同原版的部分（如实登记，不含糊）
- **点数预算过滤**（`PawnGenOptionValid`：`Cost ≤ 剩余点数` / 已选约束）—— 需"本次出兵点数"这一运行时输入，属**实际出兵规模**口径。
- **反复抽取的循环尾部效应** —— 同上。
- **`highestCost` 取法** —— 原版取"当前有效候选"最高值（随点数变），本段取"该集团全部选项"最高值作**稳定近似**。
- **跨 `groupKind` 汇总** —— 原版按 `CanGenerateFrom(parms.groupKind)` 只取一种场景；本段汇总所有场景 ⇒ 语义是"**总体**成员构成"。

### 回滚定位
1. **只回面板**：Provider 恢复治理两枚 + 服装材料 chip（键都在）⇒ 回到 9 枚。
2. **只回模型**：`FactionCompositionReader` 去掉 `costFactor` 因子 ⇒ 回到"纯权重"（**会再次偏离原版**，不建议）。
3. **运行时止损**：无 —— 纯 Def 只读 + 快照投射，不触存档、不改游戏状态。

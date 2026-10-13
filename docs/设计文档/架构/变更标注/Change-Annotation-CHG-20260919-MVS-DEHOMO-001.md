---
change_id: CHG-20260919-MVS-DEHOMO-001
version: v1.4.x
date: 2026-09-19
author: AI（主 Agent）
scope: [docs/设计文档/功能模块/经济体系/市场经济框架/**, docs/设计文档/架构/**]
files:
  - path: docs/设计文档/功能模块/经济体系/市场经济框架/SDD_派系画像去同质化.md
    type: A
    symbols: SDD（Draft v0.1）
    lines: 1-230
  - path: docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-001.md
    type: A
    symbols: CHG-20260919-MVS-DEHOMO-001
    lines: 1-100
---

# 变更标注 · CHG-20260919-MVS-DEHOMO-001（派系画像去同质化修复方案）

**规则：** ANNO-001 / GOV-013 / GATE-DELTA 前置产物
**Change ID：** `CHG-20260919-MVS-DEHOMO-001`
**Version：** v1.4.x（About 仍为 v1.4.0，本线未发版）
**Date：** 2026-09-19

## Change Annotation
- Change ID: CHG-20260919-MVS-DEHOMO-001
- Version: v1.4.x
- Date: 2026-09-19
- Author/AI: AI（主 Agent）
- Scope(摘要): 输出「派系画像去同质化」修复方案（SDD Draft），**不含实现**

### 一句话动机
用户反馈「派系画像太少，导致社会背景同质化」。诊断发现**根因不是"画像少"，而是
上游 §4 设计的 9 类反推输入只落地了 1 个（`techLevel`）** ⇒ 画像空间仅 7 档、面板仅 2 模板 ⇒
同 techLevel 派系完全同质。

### 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 行范围 | diff 摘要 | 关联规则 ID | 关联 Gate |
|---|---|---|---|---|---|---|
| `.../市场经济框架/SDD_派系画像去同质化.md` | A | SDD（Draft v0.1） | 1-230 | **诊断**（含量化：原版 39 派系 / 可建模 ≈4 / 画像 7 档 / 模板 2 套；并**额外发现 `FactionDetail` 面板的 6 因子 + 2 事件组与画像完全无关**）→ **三层修复**（① 输入扩展：把 §4 未落地的 8 类真实 Def 字段接进画像；② 契约扩展：子结构 + 向后兼容；③ 面板数据驱动）→ **3 个待裁决点** + 4 项风险 + 追踪矩阵 | SDD-002 / GOV-004 / DATA-007 | SDD / ARCH / DATA |
| `docs/设计文档/架构/Change-Annotation-CHG-20260919-MVS-DEHOMO-001.md` | A | `CHG-20260919-MVS-DEHOMO-001` | 1-100 | 本文件自身（标注须覆盖全部改动文件，含自己 ⇒ 自指一条） | ANNO-001 / GOV-013 | REL / DELTA |

> **⚠ 本批零代码改动**：无 `.cs` / 无 `.xml`（Def/Keyed）/ 无 dll 变更 ⇒
> **无编译、无单测、无需重启游戏、无玩法与存档影响**。
> **⚠ 二进制产物**：无。

### 关键内容片段

```text
诊断（逐行核对 TryGetProfile 确认的输入清单）
  def == null                                    —— 存在性检查
  IsEconomicallyModeled(def)                     —— 6 字段门禁（只产出布尔，不产出维度）
  def.GetModExtension<FactionEconomyExtension>() —— 可选覆盖（默认无）
  def.techLevel                                  —— ★ 唯一真正参与画像的输入
  ⇒ 画像输入维度 = 1；画像空间 = 7 档（Animal..Archotech）

修复（三层）
  层1 输入扩展：pawnGroupMakers / apparelStuffFilter / royalTitleTags /
                leaderTitle+fixedLeaderKinds / 四条 *TraderKinds（数量）/ settlementGenerationWeight /
                backstoryFilters / 掠夺性  —— 全部真实 Def 字段（Cecil 已逐个复核）
  层2 契约扩展：引入子结构（Composition/Materials/Governance/Trade/Density），既有 4 系数改为"合成结果"
  层3 面板数据驱动：层数/因子可变
```

### 一致性自检（GATE-DELTA 前置）
- [x] 清单覆盖 git 实际改动的全部文件，无遗漏（生成脚本自检 declared = hit）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动 —— 前序批次由 `-ExcludeFromFile` 声明
- [x] 每个改动文件都标注了类型与涉及符号
- [x] Change ID 与 Gate Record、Change Summary、Changelog 互链一致
- [x] 无调试代码残留 / 死代码（**本批零代码**）
- [x] 无临时文件残留
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile ... -Blocking` 自动校验

### 支点事实求证记录（先证后用）
| 事实 | 求证方式 | 结论 |
|---|---|---|
| §4 声明的输入字段是否真实存在 | Mono.Cecil `type RimWorld.FactionDef full` | ✅ **全部存在**：`humanlikeFaction` / `pawnGroupMakers` / `leaderTitle`(+`Female`) / `fixedLeaderKinds` / `royalTitleTags` / `settlementGenerationWeight` / `techLevel` / `backstoryFilters` / `apparelStuffFilter` |
| 画像实际用了几个输入 | 逐行读 `FactionProfileAdapter.TryGetProfile` | ✅ **只有 `techLevel`**（其余三项不产出维度）⇒ 根因坐实 |
| 原版派系规模与可建模数 | 全库解析 `<FactionDef>` 块（39 个） | ✅ ≈39 个；带 traderKinds 的仅 ≈4 个 |
| 面板模板数 | 读 `IsRoyalFaction`（判据＝`royalTitleTags` 非空）+ Provider 的 `pk` 选键 | ✅ **2 套**（Royal / Generic） |
| 同质化是否波及第三个面板 | 读 `BuiltinEconomyProvider.FactionDetail.cs` | ⚠ **是** —— 6 因子 + 2 事件组**全为硬编码**，与画像完全无关（**本批额外发现**） |

### 回滚定位
1. **整体退回**：删除本 SDD ⇒ 回到无方案状态（无运行时影响）。
2. **运行时止损**：**不适用** —— 本批不触任何运行路径。

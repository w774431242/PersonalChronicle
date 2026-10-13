---
change_id: CHG-20261009-ECON-M9-RELIABILITY
state: ImplementedAndDeployedByThisBatch
scope: M9 续 —— 接通 ReliabilityPreference 的消费（把工资可信度台账接进岗位取舍）
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Learning/ReliabilityChoiceRules.cs
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyShadowPlanning.cs
  - path: Tests/ReliabilityChoiceTests.cs
---

# M9 可靠性取舍 变更定位

**触发**：PM「继续」；上一批（`CHG-20261009-ECON-M9-STYLE`）交付了慢速学习规则，本报接通台账 → 决策的回路。

## 缺口

上一批审计发现：`LifePolicy.ReliabilityPreference` **仅定义、零消费方**。即台账（`WageCredibility`）记录了每个机构的欠薪/延迟，**但没有任何决策读它** —— 与 M5 应急储备同型的"记了但没人用"。

模型原文（`02-长期生活规划_代码设计方案` §3）：

> `| ReliabilityPreference | 对到账延误、排队、失败证据的敏感度 | 在不确定工资/服务与稳定方案之间作**有限**取舍 |`

## 改动

| 文件 | 改动 |
| --- | --- |
| `Domain/…/Learning/ReliabilityChoiceRules.cs`（新增）| `Avoid(pref, isLowConfidence)` + `ShouldSwitchToAlternative(pref, currentLowConfidence, alternativeAvailable)` |
| `Domain/…/Planning/ShadowPlan.cs` | `ShadowPlanningInput` 加可选 `LowConfidenceOpportunities`（id 集合，**Domain 不解析任何格式**）；选岗处改为"**只避一次**"并在不得不签时改用原因码 `FundedOpportunityUnderKnownRisk` |
| `Economy/…/EconomyShadowPlanning.cs` | 遍历候选，按 id 首段（`AnchorKey`）查 `QueryWageCredibility`，把 `IsLowConfidence` 的候选收成集合喂入 |
| 语言键（中英各 +1）| `Reason.FundedOpportunityUnderKnownRisk` |
| `Tests/ReliabilityChoiceTests.cs`（新增）| 4 用例 |

## 三条边界（各有用例，缺一即变形）

1. **证据不足不构成避开理由** —— 只有 `WageCredibility.IsLowConfidence`（样本量达标**且**记录差）才算。**不得把"没有证据"当成"不可靠"**。
2. **取舍是有限的** —— 只改变"在多个可行候选之间选哪个"，**不改变"是否存在可行候选"**；无替代时**仍签约**，仅以原因码标明已知风险。以"求稳"之名放弃收入不是设计的"有限取舍"。
3. **中性偏好不改变选择** —— `0.5` 是显式中性默认（同 §3 原文），故 `Avoid` 用 `> NeutralPreference` 严格大于。

## 分层处理

岗位 id 的构造格式（`AnchorKey/zoneId/shiftLoadId`）由 Economy 层的 `EconomyInstitutionWorkSource` 定义，因此在 **Economy 层**解析属**同层**操作；Domain 只收到一份 `id 集合`，**不解析任何格式**。这样既接通了回路，又不让 Domain 依赖外部字符串约定（GOV-009 同源纪律）。

## 自查（沿用上批审计方法）

| 符号 | 生产消费点 | 判定 |
| --- | --- | --- |
| `ReliabilityChoiceRules` | `ShadowPlan.cs` 选岗处 | ✅ 有 |
| `LowConfidenceOpportunities` | 生产：`EconomyShadowPlanning`（位置传参，故按名检索不到）；消费：`ShadowPlan` ×6 | ✅ 闭环 |
| `IsLowConfidence` | 生产：`WageCredibility`；消费：`EconomyShadowPlanning` | ✅ 闭环 |
| `FundedOpportunityUnderKnownRisk` | `ShadowPlan` 返回 → presenter `T("Reason." + …)` → 语言键 | ✅ 有 |

**验证基线**：编译 **0 错 0 警**；测试 **1095 通过 / 0 失败 / 1 跳过**（`M9-RELIABILITY-V1.trx`，+4）；verify **PASS**（110 双语键）。已部署。

## 遗留

| 项 | 说明 |
| --- | --- |
| **退避** | `Backoff`/`RetryBackoff`/`FailureStreak` 仍全零引用；需先定"失败"的判定口径 |
| **中断清理** | 未实装 |
| **`learnedOffset` 持久化** | 规则已交付，账目落盘与摘要投递源未接通 |
| **文件归位** | `StyleLearning.cs` 现位于 `Planning/`，而 `Learning/` 域目录已存在，宜移入以保持一致 |

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

---
change_id: CHG-20261009-ECON-M9-LEDGER
state: ImplementedAndDeployedByThisBatch
scope: M9 修复批 —— StyleLearning 归位 + 建持账方 StyleLedger + 打开 LifePolicy 学习输入面（消除零消费方规则）
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Learning/StyleLearning.cs
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Learning/StyleLedger.cs
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/LifePolicy.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyShadowPlanning.cs
  - path: Tests/StyleLedgerTests.cs
---

# M9 学习账本接线 变更定位

**触发**：PM「修复」。修的是**上一批登记的两项**：一致性待办（文件归位）与一处**自查不合格项**。

## 修的是什么（先承认问题）

上一批交付的 `StyleLearningRules` 只有**测试**作为消费方 —— 即"规则写好了但没人调用"。**这正是本会话反复在修的那类缺陷，由我自己制造。** 上一批我把它披露为"只交付规则"，但披露不等于合格：一个没有生产消费方的规则与死代码在行为上无差别。

## 改动

| # | 改动 |
| --- | --- |
| ① | `StyleLearning.cs` **归位** `Planning/` → `Learning/`（与 `ExperienceLedger`/`ExperienceSummary`/`WageCredibility` 同域），旧文件已删除 |
| ② | 新增 `Learning/StyleLedger.cs` —— `StyleLearningRules` 的**持账方**：每维 offset、**按维度计**的当日已用额度、**按 id 全局去重**的已处理摘要 |
| ③ | `LifePolicy` 打开**学习输入面**：八维统一 `p = clamp01(base + learnedOffset)`；无账本时保持显式中性默认（既有行为不变）|
| ④ | **接线**：组件持 `styleLedgers`（每 Pawn 一本）→ `RecordSettlementOutcome`（真实结算结果）投递摘要 → `EconomyShadowPlanning` 把 `Offsets` 喂进 `LifePolicy` |

## 几个关键判定

- **摘要唯一性是账本级、跨维度的**：同一条摘要换维度投递也被拒（`ReplaySummary`）。去重是"同一段经历只算一次"，不是"每维各算一次"。
- **被拒的摘要不消耗其唯一性** —— 否则该证据永久失效：次日额度恢复或维度重新启用后再也无法生效。**只有 `Applied` 才写去重表。**
- **额度按维度计，不全局共享** —— 一维用满不该冻结其它维（有专测钉住）。
- **一次事故不永久改变风格**：方向由该维定义直接决定（`ReliabilityPreference` = "对到账延误、排队、失败证据的敏感度"），按时足额 ⇒ 下调、失败/延误 ⇒ 上调；幅度由每日上限（0.03）与融合系数（0.10）共同限幅。
- **八维是上界**：`MaximumDimensions = 8`，账本不得无限增长。

## 自查（沿用本会话已稳定的方法）

| 符号 | 生产消费点 | 判定 |
| --- | --- | --- |
| `StyleLearningRules.Plan` / `ApplyOffset` | `StyleLedger` | ✅ 由"仅测试"变为有生产消费 |
| `StyleLearningRules.Effective` | `LifePolicy` ×3 | ✅ |
| `StyleLedger` | `EconomyEmployment` ×7 + `EconomyShadowPlanning` | ✅ |
| `LedgerFor` | 定义 + 结算投递 + 计划读取 | ✅ |
| 旧路径 | `Test-Path` = False | ✅ 已清 |

**端到端用例**：`TheLedgerActuallyFeedsThePolicyDimensions` —— 账本写入 ⇒ `LifePolicy.ReliabilityPreference > .5`；无账本 ⇒ 恰为 `.5`。这是"接线真的通了"的唯一证据，而非只看符号引用数。

**验证基线**：编译 **0 错 0 警**；测试 **1102 通过 / 0 失败 / 1 跳过**（`M9-LEDGER-V2.trx`，+7）；verify **PASS**（110 双语键）。已部署。

## 过程中的一次编译错误

首次编译失败 `error CS0103: StyleLearningRules`（3 处）—— 归位后 `Learning` 与 `Planning` 是**同级**命名空间，裸类型名无法解析。已限定为 `Learning.StyleLearningRules.Effective`。**这是归位的必要代价，非回归**。

## 遗留（未做，如实登记）

| 项 | 说明 |
| --- | --- |
| **`StyleLedger` 落盘** | 现为**会话内持有**。丢失的代价只是"人格随经历的微调"回退到中性基线，不涉及任何金额或权利（与 `WageCredibility` 同级定位）|
| **`BeginDay` 的调用点** | 需在"新的一天到来"时清额度；**时间回退/读档不得调用**（设计明令不得重获额度）|
| **退避** | `Backoff`/`RetryBackoff`/`FailureStreak` 仍零引用；需先定"失败"的判定口径 |
| **中断清理** | 未实装 |

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

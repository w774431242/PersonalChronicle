---
change_id: CHG-20261009-ECON-M9-FULL
state: ImplementedAndDeployedByThisBatch
scope: M9 收口批 —— §4 七类失败分类与处置、§6 有界历史（64/16）与裁剪、§5 中断阶段定格
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Learning/FailureClassification.cs
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Learning/RecoveryRules.cs
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Learning/RetryBackoffRules.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
  - path: Tests/FailureClassificationTests.cs
  - path: Tests/RecoveryRulesTests.cs
---

# M9 收口批 变更定位

**触发**：PM「做M9，至少超额完成」。

## 权威规格（照抄，未自行设计）

`docs/…/游戏代码设计/09-经验反馈与失败恢复_代码设计方案.md`：

- **§4 七类失败**（每类处置不同）：`ProtectedAction/OutOfScope`（让路，不计服务失败）、`StaleVersion/ReservationLost`（释放本票据、有界重比较）、`MoneyShortfall/StockShortage/NoStaff`（等事件、慢速兜底）、`Qualification/DependencyCycle`（寻找合法替代，没有就**结构性受阻**）、`DeliveryMismatch/TransactionUnknown`（交对账，**不重放付款**）、`ActualDefault/RepeatedCancellation`（更新可靠性、保留已履约应付）、`GetterError/Unsupported`（**不处罚供应方**）。
- **§5 中断协议**第 5 条："**不能一个中断同时记『未买到、买到未吃、服务失败』三份相同失败**"。
- **§6 有界历史**：每 Pawn 最多 **64** 个最近动作结果、最多 **16** 个活跃重试；超出按**终态/年龄**裁剪；"**未清对账义务和账本去重信息不随经验裁剪**"。

## 改动

| # | 内容 |
| --- | --- |
| A | `FailureClassification.cs`（新增）：`FailureClass` 七类 + `FailureDisposition`（退避/服务失败/结构性/交对账/等事件）+ `Classify` / `DispositionOf` / `IsBackoff` / `IsServiceFailure` |
| B | `RecoveryRules.cs`（新增）：`InterruptionRules`（§5 阶段定格 + 是否记失败）、`HistoryEntry` + `BoundedHistoryRules`（§6 裁剪）|
| C | **并入并删除** `RetryBackoffRules.IsBackoffFailure`（上批我自拟的白名单）—— 判据统一到权威七类表，调用方改用 `FailureClassRules.IsBackoff` |
| D | 退避账接入 §6 上限：`TrimRetryLedger` 按"终态（冷却已过）/ 年龄"裁剪到 16 条 |

## 关键判定

- **未知原因一律归 `GetterError`**（既不退避也不记服务失败）—— 与 §4 "部分未知/适配问题……不处罚供应方"同向：**宁可少记失败，不要错记失败**。
- **只有 `ActualDefault` 计入服务失败**：证据不足、让路、缺货都不该污染供应方可靠性（对齐 §6 验收"供应方未知 ⇒ 不记失败或满信誉"、"征召/紧急保护 ⇒ 不计供应方失败"）。
- **结构性受阻与"等事件"分开**：制度闭锁换时机无用（`Structural`）；缺钱缺货只是等相关事件（`AwaitsEvent`），不该被当成永久受阻。
- **`StaleVersion` 不进服务失败**：版本过期是并发协调问题，不是供应方违约。
- **凭据不可裁**：`BoundedHistoryRules.Trim` 跳过一切持有未清对账义务的条目，**宁可超限也不丢凭据**（§6 "未清对账义务……不随经验裁剪"；§2 "经验不是账本"）。
- **已结算的中断不算失败**：钱物两清时行为被打断不是任何一方的违约。

## ⚠ 自查：一项尚无生产消费方（如实标注）

沿用本会话方法逐符号查生产消费点：

| 符号 | 生产消费点 | 判定 |
| --- | --- | --- |
| `FailureClassRules` | `EconomyEmployment`（退避进门）| ✅ |
| `BoundedHistoryRules` | `EconomyEmployment.TrimRetryLedger`（上限 + 裁剪）| ✅ |
| `TrimRetryLedger` | 定义 + 调用 | ✅ |
| **`InterruptionRules`** | **仅测试** | ❌ **无生产消费方** |

`InterruptionRules` 的消费方是 **M4 的中断/租约证据面**（§5 第 2 步"M4 结束本行动租约并请求释放仅属于本票据的临时占用"），该证据面尚未接通。**故本规则集当前与死代码在行为上无差别** —— 这是本批唯一的自查不合格项，已登记入 `ECON-M9-LEARNING-005`，不作通过处理。其余两项均有真实生产消费点。

**验证基线**：编译 **0 错 0 警**；测试 **1124 通过 / 0 失败 / 1 跳过**（`M9-FULL-V1.trx`，+14）；verify **PASS**（110 双语键）。已部署。

## M9 现状

| 环节 | 状态 |
| --- | --- |
| §2 `ExperienceBucket`（留证据）| ✅ |
| §3 有界慢速学习（规则 + 账本 + 接线）| ✅ |
| §3 可靠性取舍（据证据修正预期）| ✅ |
| §4 失败分类与退避 | ✅（分类表 + 退避曲线 + 上限裁剪）|
| §5 中断阶段定格 | ⚠ 规则已备，**消费方（M4 中断证据面）未接通** |
| §6 有界历史（64/16）| ✅（退避账已限 16；64 的动作结果面待 M4 输出）|
| §3 Beta(1,1) 可信度与 alpha=0.20 有界平滑 | ❌ 未实装（现有 `WageCredibility` 用固定基点分档，与 §3 给出的 `mean=(1+onTime)/(2+knownOutcomes)` 不同源）|
| §6 落盘 | ❌ 会话内持有（Save schema 变更，建议独立批次）|

**未做**：无语言键（纯 Domain）；无 Harmony；无 Save schema 变化。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

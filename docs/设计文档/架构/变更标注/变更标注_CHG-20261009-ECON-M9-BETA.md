---
change_id: CHG-20261009-ECON-M9-BETA
state: ImplementedAndDeployedByThisBatch
scope: M9 续 —— §3 显式先验 Beta(1,1) 可信度估计与 alpha=0.20 有界平滑
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Learning/CredibilityEstimate.cs
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Learning/ExperienceSummary.cs
  - path: Tests/CredibilityEstimateTests.cs
  - path: Tests/EconomyProductionTests.cs
---

# M9 Beta(1,1) 对齐 变更定位

**触发**：PM 追问"为什么无法实装？如何避免？继续M9"。

## 一、对"为什么无法实装"的更正（这是本批最重要的一条）

上批我把三件事写成"未实装"，措辞让人以为它们**做不到**。逐项重启取证后，真实原因如下：

| 上批说法 | 取证结论 | 真实原因 |
| --- | --- | --- |
| `InterruptionRules` 消费方（M4 中断/租约证据面）**不存在** | ❌ **推断错误**。`Interrupt` 20 处引用、`Lease` 25 处；`MapComponent_WorkshopManager.Runtime` 已有 `Interrupt` / `JobCondition` / `EndCurrentJob` | **我没查就断言** |
| §3 Beta 公式"**输入不齐**，属独立批次" | ❌ **错误**。`ExperienceSummary.Successes/Samples` **正是** `onTime/knownOutcomes`，`MaximumSamples=64` 与 §6 一致 | **我把"要改既有口径"当成推迟理由** |
| §6 落盘需 Save schema 变更 | ✅ 属实 | 但**也不是做不到** —— 需要迁移与验证，属工作量而非阻塞 |

**结论：三项里真正"做不到"的是 0 项。** 其中一项是我的**错误推断**，两项是我的**分批选择**。把"我推迟了"写成"无法实装"是表述失实，此后改为三态分开写：**阻塞**（外部依赖确实不存在，须附取证）／**未做**（选择推迟及其代价）／**做不到**（技术不可能）。

## 二、本批实装（确实可做的那部分）

| 文件 | 改动 |
| --- | --- |
| `Learning/CredibilityEstimate.cs`（新增）| `CredibilityEstimate`（`mean=(1+onTime)/(2+knownOutcomes)`、`PriorWeight`、`BasisPoints`）+ `DurationSmoothing`（`alpha=0.20`、`IsLowEvidence`）|
| `Learning/ExperienceSummary.cs` | `ReliabilityBasisPoints` 改由 Beta(1,1) 导出（并暴露 `Credibility` / `PriorWeight`）|
| `Tests/CredibilityEstimateTests.cs`（新增）| 8 用例 |
| `Tests/EconomyProductionTests.cs` | 迁移一条旧口径断言（见下）|

## 三、关键判定

- **显式先验的意义是"无证据 ≠ 极不可靠"**：样本为 0 时读数恰为 **0.5**，而不是裸成功率的 **0**。后者会把"还没证据"显示成"极不可靠"，正是 §6 验收明令禁止的（"供应方未知/截止未知 ⇒ 显示证据不足，不记失败或满信誉"）。
- **极端样本永不触 0 或 1**：再糟的记录不判死刑、再好的不封顶（有专测跑到 ±50 样本）。
- **脏数据落回中性而非极端判断**：`onTime > knownOutcomes` 等矛盾输入一律归零样本。
- **`PriorWeight` 让"先验占比"可显示**，落实 §3 "显示先验/样本数"，样本越多先验越淡。
- **`DurationSmoothing` 只有显式投递入口、没有按时间自动衰减的通道** —— 对齐 §3 "遗忘使用有限窗口/权重，**不能反复 Tick 衰减造成同一事件被重复学习**"。
- **`IsLowEvidence` 的门槛由调用方给**：§3 只说"样本少显示 LowEvidence"，未给数值，**不自行发明一个数冒充权威参数**。
- **`WageCredibility` 的 `HasClaim`（样本量门槛）仍是"不构成主张"的唯一判据** —— 与读数是否中性是两回事，职责不混。

## 四、一条既有断言的迁移（预期行为变更，非回归）

`EconomyProductionTests.ExperienceStaysBoundedAndKeepsReliabilityInterpretable` 原文断言 `empty.ReliabilityBasisPoints == 0`（"no samples means no reliability claim"）。

这是**旧口径的产物**：旧口径用 `basisPoints=0` 表达"无主张"。新口径下**意图保留、判据迁移**：
- 读数落中性：`ReliabilityBasisPoints == 5000`
- "不构成主张"改由 `WageCredibility.From(empty).HasClaim == false` 钉住

**这是口径变更的必然结果，不是回归。**

**验证基线**：编译 **0 错 0 警**；测试 **1132 通过 / 0 失败 / 1 跳过**（`M9-BETA-V2.trx`，+8）；verify **PASS**（110 双语键）。已部署。

## 五、M9 剩余（按三态如实分类）

| 项 | 态 | 说明 |
| --- | --- | --- |
| `InterruptionRules` 消费方 | **未做** | 证据面**存在**（`MapComponent_WorkshopManager.Runtime` 的中断/JobCondition/EndCurrentJob 与 `ShadowTransactionKernel`/`WorkTakeover` 的 Lease），需选定接入点后接通；**非阻塞** |
| §6 落盘（退避账 + StyleLedger + 64 条动作结果）| **未做** | Save schema 变更 + 迁移验证；**非阻塞** |
| `RecoveryCase` 与生命周期恢复 | **未做** | 依赖 §5 中断协议接通 |

**阻塞项：0。**

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

---
change_id: CHG-20261009-ECON-M9-STYLE
state: ImplementedAndDeployedByThisBatch
scope: M9「经验反馈与失败恢复」实装 —— 有界慢速学习通道（learnedOffset）
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/StyleLearning.cs
  - path: Tests/StyleLearningTests.cs
---

# M9 有界慢速学习 变更定位

**触发**：PM「先修复再实装下一个模块」；修复批次（`CHG-20261009-ECON-FIX-M5POLICY`）完成后转入 **M9**。

## 差距分析（v0.9 §2:27 `| 9 | Pawn | 经验反馈与失败恢复 | 根据欠薪、排队、效果修正预期；中断清理、退避、重评，避免来回切换与反复失败 |`）

| M9 要求 | 现状 |
| --- | --- |
| 根据欠薪/排队/效果**留证据** | ✅ `WageCredibility` 台账（有界计数与延迟，12 处引用含 4 测试 + 概览弹窗上屏）|
| **据证据修正预期** | ❌ **`learnedOffset` 全代码库零引用** —— 本批实装 |
| **`ReliabilityPreference` 的消费** | ❌ 仅定义（`LifePolicy.cs`），**零消费方** —— 未修（需台账接入 Policy 输入面）|
| 退避 | ❌ `Backoff`/`RetryBackoff`/`FailureStreak` 全零引用 —— 未实装 |
| 中断清理 / 重评 | ⚠ 部分（`MaintainContract` 属 M6；重评走计划发布节流）|

## 权威规格（本批严格照抄，未自行设计参数）

`docs/…/游戏代码设计/02-长期生活规划_代码设计方案.md` §3「慢速更新与即时情境分开」：

> 稳定向量可写为 `p = clamp01(base + learnedOffset)`。只有 M9 的可靠、有界、多次观察摘要更新 learnedOffset；
> 原型**每日最大绝对变化 0.03**，单个摘要**融合系数 0.10**，均可配置。用**未分配的每日变化额度限幅**，
> **同一摘要 ID 只处理一次**；**时间回退/读档不能重获同一日额度**。**未经校准的学习维度可先禁用**。

## 改动

| 文件 | 改动 |
| --- | --- |
| `Domain/…/Planning/StyleLearning.cs`（新增）| `StyleLearningCode`（6 码）+ `StyleLearningRules.Plan(...)`（判决 + 幅度）+ `ApplyOffset` + `Effective` |
| `Tests/StyleLearningTests.cs`（新增）| 11 用例 |

**关键设计决定**：
- **不持有状态**：已消费的额度与已处理的摘要由调用方持久化，本类只判决"这次能不能学、最多学多少"。规则的"只进不退的账"落在存档里，规则本身保持可离线证伪。
- **超限即拒，不缩水**：当日额度耗尽返回 `DailyBudgetExhausted` 且变化量为 0。禁止"缩水成刚好用完额度"再写回 —— 那会让同一份证据被反复平滑（正是设计警告的"周期重评不断平滑同一份旧证据产生额外人格漂移"）。
- **钳制作用在合成结果上**而非偏移上：偏移可超出 0–1 表示强偏好，但人格向量永**不越界**（设计："所有维度取 0–1"）。
- **非有限证据**（NaN/±∞）返回 `NoEvidence`，不参与学习。

**参数大小关系已被钉住**：原型下 `MaximumDailyChange(0.03) < SummaryFusion(0.10)` ⇒ **满强度证据也只能走当日上限**。有专测固定这个关系，避免后续有人调大融合系数后误以为"一次就能学满"。

**未做**：无语言键（纯 Domain，无 UI 消费者）；无 Harmony；无 Save schema 变化（本类无状态，账目由调用方持有）；`ReliabilityPreference` 的消费未接通。

**验证基线**：编译 **0 错 0 警**；测试 **1091 通过 / 0 失败 / 1 跳过**（`M9-STYLE-V2.trx`）；verify **PASS**（110 双语键）。已部署。

## 过程中的一次自纠

首版两条测试断言写错（期望 `SummaryFusion`，实际被 `MaximumDailyChange` 先约束）—— **实现是对的，断言是错的**。已改为"观察差取 0.1 使融合系数成为约束"，并补一条把两个参数的大小关系显式钉住。

另：`Measure_Content_vs_Viewport` 的 `NotExecuted` 是既有的 1 个 skip（需引擎），**不是失败** —— 已核对上一批 trx 同样如此，未归因于本批。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

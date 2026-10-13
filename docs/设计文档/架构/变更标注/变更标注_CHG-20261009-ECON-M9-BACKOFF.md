---
change_id: CHG-20261009-ECON-M9-BACKOFF
state: ImplementedAndDeployedByThisBatch
scope: M9 续 —— 有界退避（照抄《09-经验反馈与失败恢复》§4 公式）并接入购买尝试
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Learning/RetryBackoffRules.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
  - path: Tests/RetryBackoffTests.cs
---

# M9 有界退避 变更定位

**触发**：PM「继续M9」。

## 权威规格（照抄，未自行设计参数）

`docs/…/游戏代码设计/09-经验反馈与失败恢复_代码设计方案.md` §4 原文：

> 原型退避为 `min(6000,150×2^min(failureCount−1,6))` Tick，失败数至少 1，增加稳定小抖动避免全体同相位。**第 5 次同指纹失败后标 PersistentBlocked 并改成事件驱动 + 每 6000 Tick 兜底**；有真实相关变化可提前复核。
> **条件指纹由有意义的报价/库存/预算/资格/地图修订组成，不含当前 Tick**。原版**紧急保护不受经济冷却限制**；**变化只解除相关原因，不把所有重试历史清零**。

数据契约（合订本 M9 专章）：`RetryState | Goal/ActionClass/Reason、条件指纹、连续失败数、NextRetryTick、上限、等待事件、是否结构性受阻`。

## 改动

| 文件 | 改动 |
| --- | --- |
| `Domain/…/Learning/RetryBackoffRules.cs`（新增）| `BackoffTicks` / `JitterTicks` / `NextRetryTick` / `MayRetry` / `Fingerprint` / `IsBackoffFailure` + `RetryPhase` |
| `Economy/…/EconomyEmployment.cs` | 退避账（`retryFailures` / `retryNextTick`）+ `MayAttempt` / `RecordAttemptFailure` / `ConsecutiveFailures`；接入 `OnPlanPublished` 的购买尝试 |
| `Tests/RetryBackoffTests.cs`（新增）| 8 用例 |

## 几个关键判定

- **"稳定"抖动用确定性哈希（FNV-1a）而非随机数** —— 文档要求散开相位，但同一条件指纹必须**恒得同一相位**，否则读档后相位跳变、退避不可复现。有专测钉住。
- **指纹结构性不含 Tick**：`Fingerprint(quoteRevision, stockRevision, budgetMinor, qualification, mapRevision)` —— 入参里根本没有 Tick 这个位置。**这是防呆，不靠调用方自觉**：含 Tick 会让每次重算都像"条件变了"，退避形同不存在。
- **只有供应与资金类失败进账**（白名单式，未知原因一律不计）：依据《09》§6 "无货无钱长期失败 ⇒ 有界退避" 与 "供应方未知/截止未知 ⇒ 显示证据不足，**不记失败**"。反过来把"证据不足"记成失败，会让玩家在没有报价时看到莫名其妙的冷却。
- **`PersistentBlocked` 用平坦 6000 兜底**（不再抖动、不再倍增），对应文档"改成事件驱动 + 每 6000 Tick 兜底"。
- **变化只放行一次，不清零失败数** —— `MayRetry(..., relevantChange: true)` 放行本次尝试，失败数**保持不变**（账本"只进不退"），落实文档的"不把所有重试历史清零"。

**未做**：无语言键（退避账无 UI 消费者）；无 Save schema 变化（账本为会话内持有）；`RetryState` 的完整字段（条件指纹持久化 / 等待事件 / 结构性受阻标记）未落盘。

**验证基线**：编译 **0 错 0 警**；测试 **1110 通过 / 0 失败 / 1 跳过**（`M9-BACKOFF-V1.trx`，+8）；verify **PASS**（110 双语键）。已部署。

## 自查（沿用本会话方法）

| 符号 | 生产消费点 | 判定 |
| --- | --- | --- |
| `RetryBackoffRules` | `EconomyEmployment.cs` ×4 | ✅ |
| `MayAttempt` / `RecordAttemptFailure` | 定义 + 调用（`OnPlanPublished`）| ✅ |
| `IsBackoffFailure` | `RetryBackoffRules` 定义 + `EconomyEmployment` 调用 | ✅ |

## M9 现状

| 环节 | 状态 |
| --- | --- |
| 留证据（`ExperienceBucket`/台账）| ✅ |
| 有界慢速学习（规则 + 账本 + 接线）| ✅ |
| 据证据修正预期（可靠性取舍）| ✅ |
| **退避**（`RetryState`）| ✅ **本批**（会话内账本）|
| 中断清理 / 生命周期恢复（`RecoveryCase`）| ❌ |
| `ObservedOutcome` 全量与 `RecoveryCase` | ❌ |
| 退避账 / 学习账的**落盘** | ❌ 会话内持有 |

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

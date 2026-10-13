---
record_id: VERIFY-20261009-ECON-M9-RC
type: 只读核实记录（非变更标注，本批零代码改动）
state: ReadOnlyVerified
scope: M9 最后一项「RecoveryCase 案例库与生命周期恢复」是否应实装
related: CHG-20261009-ECON-M9-S5, CHG-20261009-ECON-M9-S6, ECON-M9-LEARNING-005
files:
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/实施批次状态.json
---

# 核实记录：`RecoveryCase` 属重复建模，不实装

**触发**：PM「按你建议进行」。本文件遵 `doc-standard`（不新建独立主题，挂在既有变更标注目录下作只读留痕）。

## 结论

**`RecoveryCase` 不应实装** —— 其 §2 契约的六字段职责**已由既有面承担**；新造该类型会形成**第二事实源**，违反 §5 与 §2。

## 判据（§2 六字段 → 既有承载）

| §2 字段 | 既有承载 | 证据 |
|---|---|---|
| CaseId | `SettlementIntent.CommandId` | `Domain/PawnEconomy/Finance/SettlementContracts.cs:16` |
| 关联事务/阶段 | `SettlementEntry.Intent + Phase` | 同上 `:62-73` |
| 事实与预期差异 | `Reason` 原因码 / `DeficitMinor` / 旧账问题码 | `SettlementContracts.cs:95`、`Application/PawnEconomy/Finance/LegacyReconciliation.cs:24-31` |
| 唯一业务负责者 | **单写者结构**（非存储字段）| `Economy/PawnEconomy/EconomyInterruption.cs:16-37` |
| 恢复步骤状态 | **不另存** —— 设计原文"按**持久阶段**幂等执行"（阶段即状态）| `09-经验反馈与失败恢复_代码设计方案.md:62`；幂等由 `Tests/PawnSettlementPersistenceTests.cs:100-133` 钉住 |
| 结果/待治理原因 | `ReconciliationRequired` 终态 + 审计 + 治理入口 | `SettlementContracts.cs:96-97`、`PawnSettlementSaveHost.cs:114` |

**两条硬约束也已有落点**：
- 「不能一键修复凭空补钱或删债」—— 治理入口只花真钱、有界（`PawnSettlementSaveHost.cs:221-225`）
- 「只隔离受影响能力/资源」—— 隔离＝锁钱计入 Held（`FundedSettlementLedger.cs:24-25`）+ 未清对账时拒绝新 Prepare（`:38`）

## 顺带修正：总控登记的一项「零消费方」是假警报

总控曾登记 `FailureDisposition` 的 `RouteToReconciliation` / `CountsAsServiceFailure` / `Structural` / `AwaitsEvent` 在 Source 内无生产消费点。**逐项复核后需要拆开说**：

| 字段 | 复核结论 | 证据 |
| --- | --- | --- |
| `Backoff` | ✅ 直接消费 | `Economy/PawnEconomy/EconomyEmployment.cs`（`FailureClassRules.IsBackoff`）|
| `RouteToReconciliation` | ✅ **经 Resolve 间接消费** | `RecoveryRules.cs:68-70` 读取它构造 `InterruptionVerdict`；而 `InterruptionVerdict.Resolve` 由 `EconomyInterruption.cs` 生产消费 |
| `CountsAsServiceFailure` | ✅ **经 Resolve 间接消费** | 同上 `:68-70` |
| **`Structural`** | ⚠ **确认无任何消费者**（仅定义 + 测试）| 全 Source 内除 `FailureClassification.cs` 自身与测试外无引用 |
| **`AwaitsEvent`** | ⚠ **确认无任何消费者**（仅定义 + 测试）| 同上 |

**教训（与本书 2026-10-09 三次同类误判同源）**：判定"零消费方"**必须追间接链**（结构体字段 → 构造的中间类型 → 生产消费点），只看字段名在 Source 内的直接出现次数会产生假警报。此前 `SettlementUnavailable`、`LifePolicy` 六维两次误判同因。

## 本批性质

**零代码改动、零交付物** ⇒ 全部 Gate 判 `N/A`（附理由：无对象可判，非跳过）；未跑 GATE-DELTA（无本批标注对象）；无需 EXC（职责由既有面履行，**不构成偏离**）。

## 仍在册的未做项（本批未动）

1. `retryFailures` 载入后未按**指纹变化**主动提前解除退避（`Fingerprint` 已落盘，但 `MayAttempt` 仍以 `false,false` 调用 `MayRetry`）
2. 车间复核结果接原版派工门
3. 跨版本真实存档往返测试（需实机，`NOT EXECUTED`）
4. **新增登记**：`Structural` / `AwaitsEvent` 两个处置字段无消费者（小而明确，可做：把「结构性受阻」与「等事件」接进退避与复核的门）

---

AI 署名：本核实记录由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

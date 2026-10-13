---
change_id: CHG-20261010-ECON-R05-CORE
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
scope: 补齐审计 R05 的核心缺口 —— 岗位空位竞争裁决（ECON-R05 验收第 2 条）与工资预留（验收第 3 条）；新增 data 层"按岗位统计活跃合同"入口
rule_ids: AI-001, AI-003, AI-005, AI-011, GOV-003, GOV-005, GOV-012, ARC-001, DATA-001, SAVE-001, SAVE-003, LOC-001, TEST-001
files:
  - path: Source/PersonalChronicle/Data/PawnEconomy/EmploymentSaveData.cs
    type: M
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Employment/EmploymentContracts.cs
    type: M
  - path: Source/PersonalChronicle/Application/PawnEconomy/Employment/EmploymentService.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
    type: M
  - path: Tests/EconomyEmploymentTests.cs
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# R05 核心（岗位竞争裁决 + 工资预留）

**ChangeID**：`CHG-20261010-ECON-R05-CORE`｜**日期**：2026-10-10｜**依据**：`docs/任务派发/CodeBuddyCN/经济AI实装审计与修复方向_2026-10-10.md`
**当前程序集**：`Assemblies/PersonalChronicle.dll` SHA-256 `A62AEA688872BAB1DA7995B9DA590297E6DB34E339F2BFC38DA25D1F34435434`
**部署**：**未部署**

---

## 一、岗位空位竞争裁决（验收第 2 条：两 Pawn 争一个空位，只成功一个）

**复现（修复前）**：机会源（`EconomyInstitutionWorkSource.ReadOpportunities`）只读班次缺员；`Sign` 只查合同 ID 幂等与账本容量 ⇒ **同一 `PositionKey` 可被无限份合同占用**，机构对同一岗位重复承诺付款；两名 Pawn 都能签成功。

**修改边界**：
- **data 层新入口** `EmploymentSaveData.CountActiveByPosition(positionKey)` —— 按 `PositionKey` 统计**仍活跃**（`ContractState.Active`）的合同数，`ContractRow.PositionKey` 字段本已存在但此前无按岗位查询入口；
- **`EmploymentService.Sign` 写入前裁决**：`CountActiveByPosition(PositionKey) >= request.MaxJobs ⇒ EmploymentCode.SlotTaken`，位置与账本容量检查同一提交序 ⇒ **失败即零写入**（`a refused signing must leave no record` 由测试钉住）；
- 新结果码 `EmploymentCode.SlotTaken`（与"这份合同做满件数"的 `JobCapReached` 语义严格区分）。

## 二、工资预留（验收第 3 条：余额只够一人时不签两份保证支付合同）

**复现（修复前）**：签约完全不看机构资金 ⇒ 国库只够付一份工资时仍可签出多份"保证支付"合同。

**修改边界**：
- **data 层新读数** `EmploymentSaveData.SumOutstandingCommitments()` —— 所有活跃合同的剩余承诺额之和（`WagePerJobMinor × (MaxJobs − CompletedJobs)`，**饱和累加**：近 `long` 上限即返回上限，预留判据只用于比较，溢出比高估更危险）；`EmploymentService.OutstandingCommitments` 转发；
- **`TrySignForOpportunity` 在 `Sign` 之前检查**：`Treasury.AvailableMinor >= 既承诺 + 本合同承诺`，否则 `reason = "InsufficientBacking"` 并拒绝；新结果码 `EmploymentCode.InsufficientBacking`；
- **判据与支付同源**：用 `AvailableMinor`（`FundedSettlementLedger` 扣款检查用的正是它）—— 预留口径若比支付宽会签出注定付不出的合同，比支付紧则过度限制；
- 位于 `Sign` 之前 ⇒ 与竞争裁决同在写入之前，任一失败即零副作用。

## 三、既有测试的预期分歧（已修正，非放宽）

`AFullContractLedgerRefusesNewSigningsInsteadOfHalfCommitting` 原本用**同一 `PositionKey`** 连签 1024 份测"账本容量"⇒ 新裁决在第 2 份就正确挡住。该用例改**每份合同独立岗位**（`position:i`）—— 它测的本就是**账本容量**而非岗位容量，语义未放宽。

**新增两个用例**（`Tests/EconomyEmploymentTests.cs`）：
- `OnePositionAcceptsExactlyItsSlotsAndRefusesTheNextSigning`：占满即拒（`SlotTaken`）· 被拒签约**不留记录** · 不消耗账本容量 · 活跃合同的剩余承诺全额在预留内；
- `ClosingAContractFreesItsSlotAndItsReservedCommitment`：合同结束**同时释放岗位与预留**（否则空位与资金被永久锁死）。

## 四、验证与限制

| 项 | 值 |
|---|---|
| 编译 | **0 错 0 警** |
| 全量测试 | **1194 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/R05-core-V2.trx`；本批 +2 用例） |
| 新增 Save 字段 | **零**（复用既有 `ContractRow`，无 schema 变更、无迁移） |
| 实机 | **NOT EXECUTED** |

**新结果码当前不可见**（GOV-010 依此豁免翻译键义务）：`TrySignForOpportunity` 的 `reason` **唯一消费方**（`OnPlanPublished`）显式丢弃它（`out string _`）；将来若 UI 接入，须补 `SlotTaken` / `InsufficientBacking` 的中英键。

**仍未完成（如实登记）**：
1. **岗位租约**（班次占用登记/释放）—— 当前占用依据是"活跃合同数"，未落到班次实体上；R08 的"释放租约"因此仍无对象可释放；
2. **旧体系隔离**（新字符串合同 ↔ 旧 `EconomicContractId` long 合同的映射或隔离）；
3. **原子提交的完整语义** —— 现实为"顺序检查 + 写入前统一裁决"，非单事务；
4. 预留判据**未计入**其它 Pawn 尚未产生证据的未来工资（那些在支付时仍各自过国库余额检查，属"层层设防"而非"一次性总预留"）。

---
AI 署名：本变更标注与实现由 AI 助手完成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-10。

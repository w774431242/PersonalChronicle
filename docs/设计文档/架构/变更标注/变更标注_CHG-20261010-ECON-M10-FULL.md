---
change_id: CHG-20261010-ECON-M10-FULL
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
scope: M10 再生产完整闭环 —— 授权受理即绑定真实工单、完成事件按工单核销交付量、收敛式改三桶、Save v2 迁移、UI 显示已交付
rule_ids: AI-001, AI-003, AI-005, AI-011, GOV-002, GOV-003, GOV-005, GOV-009, GOV-010, GOV-012, ARC-001, ARC-003, DATA-001, DATA-003, SAVE-001, SAVE-003, UI-003, LOC-001, PERF-002, TEST-001
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Production/ProductionAuthorisation.cs
    type: M
  - path: Source/PersonalChronicle/Data/PawnEconomy/ProductionAuthorisationSaveData.cs
    type: M
  - path: Source/PersonalChronicle/Application/PawnEconomy/Production/WorkshopExecutorPorts.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Production/EconomyWorkshopExecutor.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyProductionAuthorisation.cs
    type: M
  - path: Source/PersonalChronicle/Automation/WorkshopBillActions.cs
    type: M
  - path: Source/PersonalChronicle/Capture/Patch_BillProduction.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Authorisation.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  - path: Tests/ProductionAuthorisationTests.cs
    type: M
  - path: docs/设计文档/架构/EXC-2026-030.md
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# M10 再生产完整闭环

**ChangeID**：`CHG-20261010-ECON-M10-FULL`｜**日期**：2026-10-10
**依据**：`docs/任务派发/CodeBuddyCN/经济AI实装审计与修复方向_2026-10-10.md`（M10 具体方向 + 第 3 批要求"以真实完成事件反馈需求和库存"）
**当前程序集**：`Assemblies/PersonalChronicle.dll` SHA-256 `71D9B9E4626DD98069E4F6E4604088D784ADF2C84D5EFDC7652A18AEEB18BEB4`
**部署**：**未部署**｜**Save 变更**：**有**（`ProductionAuthorisationSaveData` v1 → v2，**纯增量**）

---

## 一、修复的复现（修复前）

审计原文：*"`TrySubmitRestockProposal` 只调用 `ledger.TryAccept`；唯一外部提交消费者是授权 UI……可以保留玩家控制公有机构的授权步骤，但授权必须绑定既有合法 bill/工坊执行，跟踪真实产品交付并消耗授权量。不要因授权已有记账就宣称订单上涨已驱动生产完成。"*

逐项核实（修复前）：

| 缺口 | 证据 |
|---|---|
| 受理**只记账**，不建任何工单 | `EconomyWorkshopExecutor.TrySubmitRestockProposal` ⇒ `ledger.TryAccept`；类注释明写 "no write path into bills or schedules" |
| 授权行**无工单键、无已交付量** | `RestockProposalRow` 仅 `ProposalId` / `Receipt` / `AcceptedTick` 三字段 |
| 完成事件**不关联授权** | `ReportProductionDelivery` 不存在；`OnProduction` 首行即被 `LegacyFeesAllowed == false` 挡住（且该行为被测试锁定） |
| 收敛**两桶**，交付量无处记账 | `EffectiveNotStarted = max(0, 授权 − 在制)` |
| UI **无交付读数** | 撤销菜单只显示类别 + 数量；无 "delivered" 键 |

## 二、完整闭环（七件一次交付）

### 1. 授权行模型扩展（Domain / Application）
- `RestockProposalRow` 新增 `TargetBillKey`（工单稳定键）与 `DeliveredAmount`（已交付量），并提供 `WithDelivery`（行不可变，回执一经写入永不改写）；
- 新增 `RestockProposalLedger.Has` / `RecordDelivery` / `DeliveredAmount` / `NetOutstandingAmount`；
- `TryAccept` 新增带 `targetBillKey` 的重载（旧签名保留为未绑定转发，既有测试无需改）；
- **`ProposalId` 格式不变** ⇒ 存档键兼容、`TryParseProposalId` 校验不变。

### 2. 受理即建真实工单（唯一执行器）
- `Automation.WorkshopBillActions.TryAddBillOn(bench, recipe, count, out message)`：按目标建单并**返回新 bill**（UI 版依赖 `Find.CurrentMap` 且不返回 bill，故新增而非改旧）；仍只走原版 `BillStack.AddBill` ⇒ **无第二个 runner**；
- `EconomyWorkshopExecutor.TryCreateBill` / `FindBenchFor`：在提案地图上找**属于存活工坊**（`WorkshopZoneUtility.WorkshopAt != null`）的工作台 + 产出目标品类的配方（有界扫描，复用读数上限），**建单失败即拒绝受理**（`ProductionNoSuitableBench` / 工单栈满原样上报）—— 绝不记下没有载体的账；
- **幂等**：账内已有该 proposal ⇒ 走既有路径回原回执，**绝不重复建单**。

### 3. 完成事件 → 授权核销（真实交付）
- `GameComponent_PawnEconomy.ReportProductionDelivery(Bill_Production, now)`：按工单稳定键回匹配授权行，并按**配方中属于该品类**的真实产出量累加已交付量（多产物配方只计目标品类）；
- `Capture.Patch_BillProduction.Postfix` 挂接（与工资证据并列、互不依赖），异常降级为一次 `Log.Warning`，不影响原版制造；
- **刻意不经过 `LegacyFeesAllowed`** —— 授权闭环是新体系能力，与遗留费用开关无关（故不触既有 `B241_ProductionHookCannotPayLegacyContractOrCreateReceipt` 的断言）；
- 无匹配授权 ⇒ 零成本返回（绝大多数完成事件如此）。

### 4. 工单稳定键（GOV-009）
```
<建筑 defName>:<thingIDNumber>#<Bill.GetUniqueLoadID()>
```
用 `Bill.GetUniqueLoadID()` 而非自造序号：它正是**工资证据去重键**所用的标识（`EconomyEmployment`）并随档持久化 ⇒ 跨读档稳定。持有对象引用被明确禁止（跨层/跨档不安全）。

### 5. 收敛式改三桶（同一量只扣一次）
```
未开工 = max(0, 授权量 − 已交付量 − 观察在制量)
```
`ProductionAuthorisationRules.EffectiveNotStarted(authorised, delivered, wip)` —— 已交付的既不算在制、也不算未开工。

### 6. Save v2 迁移（纯增量）
- `SchemaVersion` 1 → 2；Row 新增 Scribe 键 `"bill"` / `"delivered"`；
- **v1 旧档的这两项读作 `null` / `0`** —— 即"未绑定、未交付"，是**显式语义**而非猜测；PostLoadInit 把负交付量夹回 0（手改存档防护）；
- 单一边界 `MaximumRows = 16` 不变；导出/恢复桥同步新字段（**否则读档后已交付量会"复活"**）。

### 7. UI 与本地化
- 撤销菜单在授权量后显示"已交付 N"（仅当 > 0，避免噪声）；
- 新增键 `PC_PEA.Policy.Delivered`（中"已交付" / 英"Delivered"），双语各 215 键一致、UTF-8 无 BOM；
- 概览页的 `authorised, not started` 读数因收敛式改三桶而**自动**反映净额。

## 三、验证

| 项 | 值 |
|---|---|
| 编译 | **0 错 0 警** |
| 全量测试 | **1198 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/M10-full.trx`；本批 +3 用例） |
| 新增/改写用例 | ① `EffectiveNotStartedNeverCountsTheSameQuantityTwice`（改写为三桶，含"交付与在制同时扣减不重叠"）；② `ADeliveredAuthorisationIsConsumedAndUnrelatedProductionsNeverTouchIt`（闭环：绑定工单核销 → 净额递减；无关工单/异品类**零核销**；超交付不为负）；③ `AnUnboundRowIsNeverConsumedAndAV1SaveLoadsAsUndelivered`（v1 旧档 null/0 语义 + 未绑定行永不核销） |
| 反证用例未冲突 | `ProposalLedgerIsBoundedDeduplicatingAndNeverExecutes`（账本层仍不执行 ✓）与 `B241_...`（工资钩子行为未变 ✓）**均无需修改** —— 新能力在 executor 与授权路径上，未污染既有语义 |
| Save | v1→v2 纯增量；旧档 null/0 |
| 实机 | **NOT EXECUTED** |

## 四、EXC-2026-030 处理（**不退场，收窄**）
工单侧键关联已落地，但 **M7 订单域 / 出口域仍未实装** ⇒ "计划键 ↔ 工单键"未满足，EXC **保持 Approved**，并把偏离面收窄为"授权与需求/出口之间仍是品类级"，回滚面收窄为"停用 `Ledger.RecordDelivery` 即退回原形态"。

## 五、仍未完成（如实登记，勿据本文声称 M10 已全线闭环）
1. **`ConfirmedUndeliveredDemand` / `AuthorisedUndeliveredExports` 仍恒 Unknown** ⇒ 补货评估顶层仍可能返回 Unknown；**授权当前仍由玩家手动发起**（未由补货缺口自动驱动）——若要"缺口驱动授权"，需一并裁定这两项的替代口径；
2. **到店 / 交割 / 进食动作链**（M5）与 **M8 交付核验**未做；
3. **无离线测试面**：`TryCreateBill` / `FindBenchFor` / `ReportProductionDelivery` 需真实地图、工坊与工单 ⇒ 上述三条修复的端到端行为（UI 提交 → 真实建单 → 完成迭代 → 授权量减少 → 读档保留）须**游戏内**验证（GameVerified 未达）。

---
AI 署名：本变更标注与实现由 AI 助手完成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-10。

---
change_id: CHG-20261010-ECON-M8-FULL
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
scope: M8 照护完整闭环 —— 唯一活跃授权选择器（未过期/有额度/执行者相符）、证据核验不再"只要存在同类型授权"、证据定键单一生产者、Grant 按钮与授权行改活跃可见性
rule_ids: AI-001, AI-003, AI-005, AI-011, GOV-003, GOV-005, GOV-009, GOV-010, GOV-012, ARC-001, ARC-003, DATA-001, UI-003, LOC-001, TEST-001
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Care/CareContracts.cs
    type: M
  - path: Source/PersonalChronicle/Application/PawnEconomy/Care/CareService.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyCare.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Care.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  - path: Tests/CareAuthorisationSelectionTests.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEnrollment.cs
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# M8 照护完整闭环

**ChangeID**：`CHG-20261010-ECON-M8-FULL`｜**日期**：2026-10-10
**依据**：`docs/任务派发/CodeBuddyCN/经济AI实装审计与修复方向_2026-10-10.md`（M8 具体方向 + 第 3 批）
**当前程序集**：`Assemblies/PersonalChronicle.dll` SHA-256 `F62F6DD011203A41E36844CAE40343D6BE26903D742ABE350FFB2770E91F1084`
**部署**：**未部署**｜**Save 变更**：**无**

---

## 一、修复的复现（修复前，逐条核实）

审计原文：*"`AcceptCareAct` 只要有同类型授权便记录证据，没有核对指定 ExecutorKey 与到期；付款再读取第一份授权。统一选择活跃授权，并检查真实执行者与授权关系，避免错误执行者、旧授权占据扣费对象。"*

| 缺口 | 修复前证据 |
|---|---|
| 证据核验**只看类型** | `AcceptCareAct`：`for (...) if (grants[i].Kind == kind) { authorized = true; break; }` —— **不看到期、不看额度、不看执行者** |
| 扣费对象取**第一份** | `ReadGrant`：`ReadForBeneficiary` 枚举次序第一份（`Dictionary.Values`，非时间序），**不过滤到期/额度/执行者** |
| **无执行者核对** | `CareRules.CanCharge` 六条判据无一涉及 `ExecutorKey`；`CanExecute` 只在**签发**时用，且 `ExecutorKey` 唯一生产签发点写 `null` |
| 定键式**两份副本** | `CareService.AcceptCareAct` 内联拼接 与 `EconomyCare.EvidenceIdOf` 各一份 —— 漂移会让去重/重放同时失效 |
| UI **过期授权死锁** | `HasCareGrant` 只比 `Kind` ⇒ 过期授权永久挡住 Grant 按钮：扣不到钱也签不出新的 |

## 二、完整闭环（六件一次交付）

### 1. 唯一活跃授权选择器（`CareService.SelectChargeable`）
三重过滤，**证据核验与扣费对象都必须经此选择**：① 未过期（`IsExpired(now)`）② 仍有剩余额度（`Max − Charged > 0`）③ 若授权已指定执行者则必须与**真实执行者**精确一致。
与既有 `ReadActiveGrant` 的分工被明确写入注释：后者只过滤到期、供**签发防重**（"有未过期同类型即不许重签"），本方法供**消费侧**（"这笔该记在谁头上"）—— **两者语义不同，不可合并**。

### 2. 授权内执行者核验门（`CareRules.CanExecuteUnder`）
- 授权**已指定**执行者 ⇒ 真实执行者必须精确等于它（错误执行者 → `ExecutorUnavailable`）；
- 授权**未指定**（空，当前 UI 唯一生产形态）⇒ 视为"不限执行者"，但仍要求不是受益者本人（与既有 `CanExecute` 同一纪律）。
`ExecutorUnavailable` 复用既有码，未新增枚举成员（避免扩大影响面）。

### 3. 证据核验不再"只要存在同类型授权"
`AcceptCareAct` 改为：`SelectChargeable(...) == null ⇒ AuthorizationUnknown`。现在**过期授权、额度用尽的授权、错误执行者、异服务类型**都无法再为一条证据背书。

### 4. 证据定键单一生产者
定键式 `care-evidence:{kind}:{beneficiary}|{doctor}|{tick}` 由 `CareService.EvidenceIdOf`（静态）**独占生产**；`EconomyCare.EvidenceIdOf` 改为转调（保留 Pawn 参数重载以免动调用点）。

### 5. 扣费对象改走活跃选择器
`TrySettleCareAct` 的 `Care.ReadGrant(...)` → `Care.SelectChargeable(kind, beneficiary, now, doctorKey)`（选择失败沿用既有 `PayerNotAuthorized`，不扩大改动面）。
（上一批已修的"仅 `Finalized` 才销额度"保持不变。）

### 6. UI 活跃可见性 + 本地化
- `HasCareGrant` 改为**活跃判据**（未过期 + 有额度），解开"过期授权永久挡住重新签发"的死锁；
- 授权行在**指定了执行者时**显示 `执行者: <key>`（未指定不占版面）；
- 新增键 `PC_PEA.Care.Executor`（中"执行者" / 英"Executor"），双语各 216 键一致、UTF-8 无 BOM。

## 三、验证

| 项 | 值 |
|---|---|
| 编译（本批文件） | **无错误** —— 全量编译当前仅有 **1 个错误，位于 `EconomyCommands.cs`，属并行写入者的半成品**（`ReverifyHint` 未定义），与本批无关 |
| 本批新增测试 | **3 用例，全部通过**：`OnlyALiveGrantWithBudgetAndTheMatchingExecutorCanBeChosenForCharging`（三重过滤含"受益人不得自任执行者"与"他人不得借用授权"）· `AnUnnamedExecutorGrantStaysOpenToAnyNonBeneficiary`（未指定 ≠ 谁都不可用）· `CareEvidenceNeedsALiveGrantAndUsesTheSingleKeyProducer`（无授权/错执行者/异类型/过期四路拒绝 + 重放 + 定键同源） |
| 全量测试 | **1199 通过 / 2 失败**（`M8-verify.trx`）—— 两个失败为 `B241_SwitchOffWithOldRightsOrRealFundsRemainsDraining` 与 `B242_CompleteOfflineCutoverScribeKeepsFundsRightsAndGameAcceptanceGate`（`Expected Draining, But was: Off`），**属并行写入者的制度冻结改动**，与照护链无交集，本批**未越界修改** |
| Save | 零 schema 变更 |
| 实机 | **NOT EXECUTED** |

## 四、顺带修复（并行写入者的编译阻塞）
`EconomyEnrollment.cs`（并行写入者的 FA-01 新文件）缺 `using RimWorld;`，导致 `Pawn.IsQuestLodger()` 无法解析、**全工程编译失败**。已补该 using（一行，与同仓其它两处同款用法一致）。该文件**仍在被并行改动**，本批只做此最小修复。

## 五、仍未完成（如实登记）
1. **喂养 / 学习 / 照护的执行接线未做** —— 当前唯一真实交付证据源仍是 `TendUtility.DoTend`（治疗）；母乳喂养 / 瓶喂 / 授课 / 学习等原版过程尚未接证据源（探针停在 `ProcessExistsCaptureNotWired`）。这类接线需**新增 Harmony patch**，会触发 Architecture Review，属独立工程；
2. **执行者指派入口未做** —— Grant 仍以 `ExecutorKey = null`（"不限执行者"）签发；"指定某位家庭成员为执行者"的选择界面留待后续（门逻辑已就绪，只缺 UI 生产方）；
3. **无离线测试面**：真实 `TendUtility.DoTend` → 证据 → 扣费的端到端链（含错误执行者的真实 Pawn 场景）须游戏内验证（GameVerified 未达）。

---
AI 署名：本变更标注与实现由 AI 助手完成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-10。

---
change_id: CHG-20261009-ECON-M8-CARE
state: ImplementedAndDeployedByThisBatch
scope: M8「家庭与照护协作」实装 —— 签发侧防重（防止儿童需求重复）+ 四角色执行者门
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Care/CareContracts.cs
  - path: Source/PersonalChronicle/Application/PawnEconomy/Care/CareService.cs
  - path: Tests/CareCoordinationTests.cs
---

# M8 家庭与照护协作 变更定位

**触发**：PM 批准「逐个模块实装」；M3（4 批）、M5、M4、M6、M7 已做满 ⇒ 转入 **M8**。

> ⚠ **更正**：上一批报告里我把 M8 写成"自给与安全网"，这是错的。权威定义（`当前方案复评与AI模块摘要_v0.9.md:26`）为
> `| 8 | Pawn | 家庭与照护协作 | 区分受益者、责任人、执行者和付款方，安排喂养/照护/学习，防止儿童需求重复 |`

## 差距分析

| M8 要求 | 现状 |
| --- | --- |
| 区分**受益者 / 责任人 / 执行者 / 付款方** | ✅ `CareAuthorization` 四身份齐备（`BeneficiaryKey` / `GuardianKey` / `ExecutorKey` / `PayerKey`），且关系**不授权金钱** —— 付款是独立授权（自带上限、有效期、revision）|
| 责任人门 | ✅ `CareRules.CanCreate`：未成年人须有具名监护人；关系本身不携带权限 |
| 付款方门 | ✅ `CareRules.CanCharge`：显式付款授权、未过期、不超上限；`ApplyCharge` 溢出即 CapExceeded |
| 服务能力门 | ✅ `ServiceReadiness` 七道门 + `ServiceCapabilityRules`（缺一即留在原版公开路径，不得报价）|
| **防止儿童需求重复** | ❌ **签发侧无防重** —— 本批实装 |
| **执行者门** | ❌ `CareCode.ExecutorUnavailable` 在设计里存在，但**全代码库零产生者** —— 本批实装 |
| 安排喂养/照护/学习 | ⚠ `ServiceKind` 含 `Childcare`/`Education`，但**安排链路未接通**（需真实证据源）|
| 家庭参与度 | ❌ `LifePolicy.FamilyParticipation => .5f` 仍是**常量占位** |

## 本批修掉的真缺陷

`CareService.ReadGrant(kind, beneficiary)` 的注释写明它取"同一受益人同一服务类型的第一份授权"，**扣费侧（`EconomyCare.cs:62`）正是靠它定位扣费对象**。但签发侧 `CareService.Create` **完全不查重**，于是：

> 同一儿童的同一项照护可以签发**多份**授权，每份各带独立上限。扣费只认其中一份 ⇒ 其余额度**永远不会被使用**（幽灵授权）；而一旦第一份过期，扣费对象会**静默换成另一份** —— 这正是"同一份照护被重复记账"的入口。

## 改动

| 文件 | 改动 |
| --- | --- |
| `Domain/…/Care/CareContracts.cs` | `CareCode` 追加 `AlreadyAuthorized`（**追加在末尾**：既有值序号不得变动）；新增纯函数 `CareRules.CanIssue(existing, beneficiaryKey, kind, now)` 与 `CareRules.CanExecute(beneficiaryKey, executorKey)` |
| `Application/…/Care/CareService.cs` | 新增 `ReadActiveGrant(kind, beneficiary, now)`（只认未过期的，供防重使用；`ReadGrant` 保持原语义供扣费侧沿用）；`Create` 接入两道门 |
| `Tests/CareCoordinationTests.cs`（新增）| 9 用例 |

**规则细节（都是可离线证伪的）**：
- 同受益人同服务类型的**活跃**授权唯一 ⇒ 第二份返回 `AlreadyAuthorized`
- **到期授权不占名额**（否则儿童换服务期后永远签不出新授权）；边界与 `IsExpired` 一致（`now == ExpiresTick` 仍算有效）
- **不同服务类型互不占位**（照护与学习是两项需求）、**不同受益人互不影响**
- 提额/续期走**显式撤销 + 重新签发**（`Revoke` ⇒ `Create`），让每次变更留下撤销痕迹，而不是靠多签一份悄悄叠加额度
- 执行者门：**受益者不得兼任执行者**（自己在四角色里占两席，协作就退化成自给并重复记账）；**未指派执行者不算不可用**（"没指定"与"不可用"是两件事，混为一谈会拒掉所有只登记付款方的授权）

**未做**：无语言键（`CareCode` 无 UI 文本消费者，避免死键）；无 Harmony；无 Save schema 变化（`CareCode` 不入存档）；未改 `ReadGrant` 语义。

**验证基线**：编译 **0 错 0 警**；测试 **1077 通过 / 0 失败 / 1 跳过**（`M8-CARE-V1.trx`，较上批 +9；既有 `EconomyCareTests` 全绿 ⇒ 新门未破坏既有路径）；verify **PASS**（110 双语键）。已部署。

## M8 剩余（未做，如实登记）

| 缺口 | 前置条件 |
| --- | --- |
| **安排喂养/照护/学习** | 需真实证据源驱动（谁在何时执行了照护/教学），当前 `AcceptCareAct` 只记录证据**不驱动安排** |
| **服务供给机制** | 七道门中 `DeliveryEvidence`/`Settlement`/`InterruptionRecovery`/`UiExplanation` 尚无证据源 ⇒ 能力**恒停在 PublicVanilla**，不会报价 |
| **家庭参与度** | `LifePolicy.FamilyParticipation => .5f` 需改为按真实家庭关系/责任计算（涉及 Policy 输入扩展）|
| **`SettlementUnavailable` 码** | 仍无产生者（结算通道接入后才会有）|

**未做独立 Gate**（沿用自评结论：无 Harmony、无新跨库面、无静态状态、Save 未变）。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

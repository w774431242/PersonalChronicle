---
change_id: CHG-20261009-ECON-M7-PURCHASE
state: ImplementedAndDeployedByThisBatch
scope: M7「消费与服务」实装 —— 购买方案可行性接通真实报价（修复"永远不可行"）+ 原因码三态精确化
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs
  - path: Tests/PlanOptionFeasibilityTests.cs
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyPlanning.xml
  - path: Languages/English/Keyed/PawnEconomyPlanning.xml
---

# M7 购买方案可行性 变更定位

**触发**：PM 批准「逐个模块实装」；M3（4 批）、M5、M4、M6 已做满 ⇒ 转入 **M7 消费与服务**。

## 差距分析（v0.9 §2 M7：`预约、购买、交付、使用及部分服务结算；区分已付费与生活已满足`）

| M7 要求 | 现状 |
| --- | --- |
| **购买** | ✅ `TryPurchaseFoodAtCounter`（已公布价格 / 机构自有库存 / 买家自有现金 / 真人站在柜台；失败归还货物并取消已备支付）|
| **交付** | ✅ `TryHandOver` + `ReturnToStock`（不让渡就不收费）|
| 报价来源 | ✅ `EconomyFoodCostSource`（真实机构报价 + 随身食物作为"已覆盖"，绝不拿 MarketValue 顶替）|
| **预约** | ❌ 未实装 |
| **部分服务结算** | ❌ `InstitutionPolicySaveData.ServicePricePerUnitMinor` 字段存在但**零消费方**；无服务供给机制 |
| **区分已付费与生活已满足** | ⚠ 仅作为运行时守卫存在（`food.CurLevelPercentage >= FoodNativeYield ⇒ NativeFoodFirst`），**未作为状态上报** |

## 本批修掉的真缺陷

`ShadowPlan.BuildCore` 原先写死：

```csharp
options = BuildPlanOptions(nativeSelfHelp, permittedForWork, fundedOffer, /*pricedConsumption*/ false, /*service*/ false);
```

⇒ **即使玩家有现金、机构也已公布报价，"购买"这一类方案永远显示为不可行**。M4 刚建好的四类候选方案里有两类（购买/服务）**不可能被选中**，且原因码一律回落到与**收入**有关的 `CostEstimateUnavailable`（"不能伪造收入金额"）—— 语义错位，会把玩家引向错误的排查方向。

## 改动

| 文件 | 改动 |
| --- | --- |
| `Domain/…/Planning/ShadowPlan.cs` | 新增纯函数 `ShadowPlanBuilder.PricedConsumptionFeasible(costs, costsComplete, availableCash, out blockCode)`：可行 ⟺ 存在**已定价的必要消费**且现金扣除已有覆盖后足以支付。`BuildCore` 改用它替代硬编码 `false` |
| 同上 | `BuildPlanOptions` 增加 `purchaseReason` 参数（三态精确码）；Service 原因码由借用的 `OpportunityNotPublished`（岗位市场）改为专属 `ServiceNotImplemented` |
| 语言键（中英各 +3）| `NoPricedConsumption` / `ConsumptionUnaffordable` / `ServiceNotImplemented` |
| `Tests/PlanOptionFeasibilityTests.cs`（新增）| 11 用例 |

**与 `FindInstitutionallyBlocked` 的一致性**：两者是同一份成本切片的**正反两面** —— 那边找"必要但付不起"、这边找"必要且付得起"，共用同一遍历与同一覆盖算法（`total - Σcoverage <= availableCash`）。已有专门用例钉住覆盖扣除（12000 报价含 8000 随身食物 ⇒ 现金 4000 够、3999 不够），否则会出现"预算说付不起、方案说买得起"的自相矛盾。

**三态不可合并**：`CostEstimateUnavailable`（成本未读全，不猜）/ `NoPricedConsumption`（无任何报价）/ `ConsumptionUnaffordable`（有报价但付不起）。合并后玩家分不清"这里没有报价"与"我有报价但买不起"。

**未做**：`WorkContract` 类字段未动；无 Harmony；无 Save 变化；未新增 UI 消费者以外的契约。

**验证基线**：编译 **0 错 0 警**；测试 **1068 通过 / 0 失败 / 1 跳过**（`M7-PURCHASE-V1.trx`，较上批 +11）；verify **PASS**（110 双语键，本批新键落在 `PawnEconomyPlanning.xml`，不在需求页键域内）。已部署。

## M7 剩余（未做，如实登记）

| 缺口 | 前置条件 |
| --- | --- |
| **预约** | 需先定义"预约什么"（服务时段 / 柜台档期），当前无该概念 |
| **部分服务结算** | 需先实装服务供给机制；`ServicePricePerUnitMinor` 至今零消费方，故本批 Service 方案仍如实不可行并给专属码 |
| **区分已付费与生活已满足** | 需把该区分做成**可上报的需求状态**（读模型字段），而非仅运行时守卫；涉及 View 契约扩展 |
| **M7 的"使用"环节** | 食用本身由原版 `Ingestion` 承担，本系统不接管，只按证据结算 |

**未做独立 Gate**（沿用自评结论：无 Harmony、无新跨库面、无静态状态、Save 未变）。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

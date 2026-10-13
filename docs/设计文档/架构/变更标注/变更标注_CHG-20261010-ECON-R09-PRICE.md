---
change_id: CHG-20261010-ECON-R09-PRICE
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
decision: 食品价格粒度裁定 —— 精确营养价 + 总额四舍五入 + 下限 1 最小货币单位（用户已批准"按你推荐继续"）
scope: R09 价格粒度收口：预算 / 报价 / 实际扣款三处统一到唯一计价式
rule_ids: AI-001, AI-005, AI-011, GOV-003, GOV-006, GOV-012, ARC-001, DATA-001, PERF-002, TEST-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyFoodCostSource.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyConsumption.cs
    type: M
  - path: Tests/EconomyPurchaseTests.cs
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# R09 价格粒度裁定（唯一计价式）

**ChangeID**：`CHG-20261010-ECON-R09-PRICE`｜**日期**：2026-10-10
**裁定**：**精确营养价 + 总额四舍五入到最小货币单位 + 下限 1**（报告要求"先裁定"；用户已批准本 AI 的推荐方案）
**当前程序集**：`Assemblies/PersonalChronicle.dll` SHA-256 `9D61B62184DA1F873352FD85DE66FD32167BCC9E0A80B59332CF1FA6EFBCAAFE`｜**部署**：**未部署**

---

## 一、裁定内容（行为变化，玩家可见）

| 缺口（营养） | 单价（每营养） | 旧：整单位 `ceil` | 新：精确 + 取整 |
|---|---|---|---|
| 0.9 | 100 | 100 | **90** |
| 0.05 | 100 | 100 | **5** |
| 0.004 | 100 | 100 | **1**（下限） |
| 1.5 | 101 | 202 | **152** |

**语义**：小额缺口不再与大餐同价；下限 1 保证"比半个最小单位还小的缺口"仍可交易（否则取整成 0 会使该笔永远不可买）。

## 二、修改边界：唯一计价式

新增 `EconomyFoodCostSource.NutritionPrice(nutrition, pricePerNutritionMinor)`（`internal static`，**规则单一来源**）：

```
nutrition <= 0 或单价 <= 0            → 0（零量零价）
nutrition × 单价 ≥ long.MaxValue      → long.MaxValue（调用方按"报价溢出"拒绝）
否则                                  → max(1, round(nutrition × 单价, AwayFromZero))
```

**三处消费者全部改为调用本式**（此前各自取整，"看到的价"与"扣的钱"会分叉）：
1. **预算**（`EconomyFoodCostSource.ReadCosts`）：总成本由 `ceil(needed) × 单价` 改 `NutritionPrice(needed, 单价)`；随身覆盖由 `floor(carried) × 单价` 改 `NutritionPrice(min(needed, carried), 单价)`；
2. **实际扣款**（`EconomyConsumption`）：由 `checked(ceil(nutrition) × 单价)` 改 `NutritionPrice(...)`，保留原有 `PriceOverflow` / `PriceUnavailable` 两个拒绝码（`long.MaxValue` ⇒ 溢出；`<= 0` ⇒ 不可用）；
3. **报价展示**：走的是同一 `policy.FoodPricePerNutritionMinor` 参数（每营养价），本裁定不改参数语义，只统一"从营养到金额"的换算。

## 三、验证

| 项 | 值 |
|---|---|
| 编译 | **0 错 0 警** |
| 全量测试 | **1196 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/R09-price.trx`；本批 +1 用例） |
| 新增用例 | `FoodPriceUsesExactNutritionWithAFloorOfOneMinorUnit` —— 钉住四个边界（0.9→90 不向上取整；0.05→5；0.004→1 下限；1.5×101→152 总额 AwayFromZero；0 营养→0 价） |
| Save | 零 schema 变更 |
| 实机 | **NOT EXECUTED** |

**既有测试未断言旧取整语义** —— 旧口径此前**无测试覆盖**，本批是新覆盖而非改写既有期望。

**无离线测试面**：`FirstFood` / `CarriedNutrition` 的真实物品筛选（需库存与 Pawn）；`ReadCosts` 的端到端预算行（需 `PerceptionSubject` + Pawn）。

## 四、剩余（R09 与第 3 批）

- **R09 未做**：到店 / 交割 / 后续原版进食动作链（可中断的自主到店任务 + 实际缺口改善确认，属 M5 生活执行大工程）；派系 / 意识形态禁忌筛选（`FirstFood` 的"允许食品"完整判据）。
- **第 3 批其余**：M8（授权核验执行者与到期、喂养/照护/学习执行接线）、M10（授权绑定真实 bill/工坊交付并消耗授权量，收据不得冒充完成）。

---
AI 署名：本变更标注与实现由 AI 助手完成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-10。

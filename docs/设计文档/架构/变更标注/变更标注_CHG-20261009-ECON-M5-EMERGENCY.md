---
change_id: CHG-20261009-ECON-M5-EMERGENCY
state: ImplementedAndDeployedByThisBatch
scope: M5「生活预算与储蓄」实装 —— v0.5 §40 应急储备（补储提前触发，仍以储备目标为上界）
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/LifePolicy.cs
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Budget/LifeBudget.cs
  - path: Tests/PawnLifeBudgetTests.cs
---

# M5 应急储备 变更定位

**触发**：PM 批准「逐个模块实装」，前一个模块 M3 已做满 ⇒ 转入 **M5 生活预算与储蓄**。

**先做的差距分析**（对照 v0.9 §2 的 M5 用途 + v0.5 §40/§53 + v0.7 §6）：

| M5 要求 | 现状 |
| --- | --- |
| 分配**不重复**预算 | ✅ 已实现（切片去重 / 覆盖去重 / 硬预留背书 `UnbackedReservation`）|
| 预测必要费用与**有限储备** | ✅ 已实现（`RequirementCostSlice` + `ReserveTargetMinor` + `LowWaterMinor`）|
| **提前**触发收入目标 | ✅ 已实现（`cash < low` 即补储，不等余额归零）|
| **避免无止境赚钱** | ✅ 已实现（`cash >= target` ⇒ `NoIncomeNeeded`）|
| 三分配（即时/储蓄/可选）| ✅ 已实现 |
| 账户/报价缺失降级 | ✅ 已实现（`WaitingAccount` / `WaitingQuote`）|
| **应急储备**（v0.5 §40）| ❌ **缺 —— 本批实装** |

⇒ **M5 内核实际已相当完整**（`budgetKernelVerified: true` 名副其实）。本批只补真实缺口。

## 改动

| 文件 | 改动 |
| --- | --- |
| `LifePolicy.cs` | 新增 `EmergencyReserveBasisPoints = 800`（储备目标的 8%）—— 下水位之上的**应急缓冲** |
| `LifeBudget.cs` | 新增只读字段 `EmergencyReserveMinor`；补储**触发线**从 `cash < low` 改为 `cash < min(target, low + emergency)` —— 更**早**寻找岗位，但以储备目标为上界，**不会因此无尽补储** |
| `PawnLifeBudgetTests.cs` | **预期变更迁移**：`target=1200 / low=840` 时触发线移到 `936`。原用例 `(840,false,false)`、`(900,false,false)` 断言"此现金水平不补储"，现按设计应为补储 ⇒ 改为 `(900,false,true)`，并**新增边界用例** `(935,false,true)` / `(936,false,false)` 钉住新触发线 |

**验证基线**：编译 **0 错 0 警**；测试 **1044 通过 / 0 失败 / 1 跳过**（`M5-EMERG-V3.trx`）；verify **PASS**（110 双语键）。已部署。

## M5 剩余缺口（如实登记，均跨出 M5 边界）

1. **`contractWageConsumptionWired: false`** —— 合同-工资-消费全链未接，属 M6（就业）↔ M7（消费）之间。
2. **v0.5 §53 的拟议 `LifePolicy` 字段仍缺**：食品溢价上限（需价格比较 ⇒ M7）、风险与通勤接受范围（需接入层路程 ⇒ 同 M3 §6 端口）、住房/舒适目标（需住房事实 ⇒ M8/M10）。`ReservePreference` / `TimeValue` / `FamilyParticipation` / `CareerOrientation` / `ReliabilityPreference` / `SocialLeisure` 仍是 `.5f` **常量占位**。
3. **"允许政策配置"（v0.5 §47/§53）** —— 需要玩家 UI + Save schema，属独立工作单元。
4. **§8 中断/清理/恢复、§10 时间与服务容量共同仲裁** —— 依赖未实装模块。

**未做独立 Gate**（沿用自评结论：无 Harmony、无新跨库面、无静态状态、Save 未变）。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

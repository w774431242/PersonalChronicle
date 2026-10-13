---
change_id: CHG-20261009-ECON-AUDIT-5MOD
state: AuditCompletedWithPartialFixes
scope: 本轮实装的五个模块（M3/M4/M5/M6/M7/M8）自审 —— 符号消费方、产生者、测试覆盖、文档一致性
files:
  - path: Tests/PolicyThresholdInvariantTests.cs
---

# 五模块自审 变更定位

**触发**：PM 要求"审计前面5个模块"。

## 审计方法

不复述结论，只查**可证伪的事实**：本轮新增的每个符号是否有**生产环境消费点**（而非仅测试引用）、每个新枚举值是否有**产生者**、每个新语言键是否有**代码引用**、声明要交付的能力是否有**测试覆盖**。

这一方法直接来自本轮反复出现的缺陷形态：**枚举值存在但零产生者**（`CareCode.ExecutorUnavailable`）、**方法存在但零调用者**（`CareService.ReadGrant` 曾被误判）、**注释写着"如实不可行"看着像设计**（`BuildPlanOptions(..., false, false)`）。审计就是查我自己有没有制造成同型缺陷。

## 结论（逐项）

| 模块 | 本轮新增符号 | 生产消费点 | 判定 |
| --- | --- | --- | --- |
| M6 | `ContractExitRules` | `EconomyEmployment.MaintainContract`（且被 `OnPlanPublished` 调用）| ✅ 闭环 |
| M6 | `MaintainContract` | `EconomyEmployment.cs`（定义+调用）| ✅ |
| M7 | `PricedConsumptionFeasible` | `ShadowPlanBuilder.BuildCore` | ✅ 闭环 |
| M8 | `CareRules.CanIssue` / `CanExecute` | `CareService.Create`（L71 / L75）| ✅ 闭环 |
| M8 | `ReadActiveGrant` | `CareService.Create`（L71）| ✅ |
| M8 | `AlreadyAuthorized` | `CanIssue` 产生 + `Create` 消费 | ✅ |
| M8 | `ExecutorUnavailable` | `CanExecute` 产生（本批补上）| ✅ 由零产生者变为有产生者 |
| 全部 | 本轮新增 6 个语言键 | `NoPricedConsumption`×3 / `ConsumptionUnaffordable`×2 / `ServiceNotImplemented`×1 / `Column.LongTarget`×1 / `Cycle.Section.LongTerm`×1 / `Cycle.Error`×1 | ✅ 无死键 |

**结论：本轮未引入新的零消费方符号。**

## 审计发现的缺陷

### ① M5「应急储备」算出但零消费方（**本系统自己引入**，未修）

`LifeBudget.EmergencyReserveMinor`（`LifeBudget.cs:56` 暴露、`:112` 计算）的全部引用点只有定义与赋值两处：

- presenter **不上屏**
- 任何逻辑（补储判定 / 下水位比较 / 预算状态）**不读取**
- **无测试**

即：上一批声明的"补应急储备"实际只产出了一个**纯装饰字段**。这正是本轮一直在修的那类缺陷 —— 由本系统自己制造。**处置**：登记为 `ECON-AUDIT-5MOD-004`，未在本批修复（接入预算卡或并入补储判定都属语义决定，需单独裁决）。

### ② M3 双阈值 / M5 储备阶梯 **零单测**（**已修**）

`PlanningPolicy.FoodEnter/RestEnter/JoyEnter` 与 `EmergencyReserveBasisPoints/LowWaterBasisPoints` 此前只有生产与展示消费、**零测试覆盖**。这些常量的语义是"越往下越紧急"，顺序一旦颠倒，实机上只表现为"钱花得怪"，极难归因。

新增 `Tests/PolicyThresholdInvariantTests.cs`（4 用例）钉住：
- 进入阈值必须**高于**原生自给线（否则"还没开始规划就已经该自给"，规划永不触发）
- 三个进入阈值都是 (0,1] 内的比例
- **应急储备 < 下水位 < 全额**（顺序颠倒会让补储提前被小钱满足，真到应急时没有缓冲）
- 自给线一律低于进入阈值（两条线不得交叉）

### ③ `LifePolicy` 仍有 **6 个 `.5f` 常量占位**（未修）

`ReservePreference` / `TimeValue` / `FamilyParticipation` / `CareerOrientation` / `ReliabilityPreference` / `SocialLeisure`（`LifePolicy.cs:45-50`）。模型要求它们由**特质与关系**推导（`LifePolicy(traits)` 目前只推导 `LifeStandard` 与 `LaborInclination`）。**处置**：登记，未修（需扩展 Policy 输入面）。

### ④ 更正：`CareCode.SettlementUnavailable` **不是**零产生者

上一批报告称它"全代码库零引用"。**该结论有误** —— 它在 `EconomyCare.cs:73` 与 `:76` 有两个真实产生点（付款准备失败 / 结算失败回滚）。误判原因：一次检索输出被截断到前 20 条，而该匹配位于截断之外，我未复核就下了结论。

### ⑤ 方法论限制（如实登记）

审计脚本本身有**假阴性**：`ConsumptionUnaffordable` 的产生点被报为 0，实际是三元表达式赋值（`blockCode = sawUnaffordable ? "ConsumptionUnaffordable" : ...`），未被 `blockCode = "X"` 模式匹配到。**结论：字符串模式审计只能用于筛查，任何"零引用"判定都必须用精确模式（含前缀限定）复核。**

## 验证基线

编译 **0 错 0 警**；测试 **1081 通过 / 0 失败 / 1 跳过**（`AUDIT-5MOD-V1.trx`，较上批 +4）；verify **PASS**（110 双语键）。已部署。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

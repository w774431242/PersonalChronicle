---
change_id: CHG-20261009-ECON-M4-OPTIONS
state: ImplementedAndDeployedByThisBatch
scope: M4「方案比较与行动调度」实装 —— 四类候选方案（免费自救/劳动/购买/服务）的可行性与不可行原因
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.cs
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyPlanning.xml
  - path: Languages/English/Keyed/PawnEconomyPlanning.xml
  - path: Tests/ShadowGoalRankingTests.cs
---

# M4 方案候选集 变更定位

**触发**：PM 批准「逐个模块实装」；M3（4 批）、M5（1 批）已做满 ⇒ 转入 **M4 方案比较与行动调度**。

**差距分析**（对照 v0.9 §2 M4 用途"比较免费自救、购买、工作、服务及协作，检查钱/时间/能力并执行原版合法 Job"）：

| 要求 | 现状 |
| --- | --- |
| 检查钱 / 时间 / 能力 / 可达 / 时效 | ✅ 既有（`WorkOpportunity.Eligible`）|
| 执行原版合法 Job | ⚠ 影子模式不执行（设计如此）|
| **比较四类方案** | ❌ **缺 —— 只有岗位一种候选，玩家看不到"为什么没选其它"** |

## 改动

| 文件 | 改动 |
| --- | --- |
| `ShadowPlan.cs` | 新增 `PlanOptionKind`（NativeSelfHelp / SeekWork / Purchase / Service）与 `PlanOption`（可行性 + **原因码**）；`ShadowPlan.Options` 只读字段；纯函数 **`BuildPlanOptions`**（顺序固定为免费自救 → 劳动 → 购买 → 服务，对应 v0.7 §6 第 5 条"免费方案确实同等时允许选免费"）|
| `ShadowPlanPresenter.cs` | 决策页仲裁区新增「可选方案」行：`免费自救 ✓ · 劳动 ✗（无可行岗位）· 购买 ✗（缺生活费用估计）· 服务 ✗（真实岗位市场尚未发布）` —— **不可行必须显示原因**，否则玩家只能看到一个决策结果 |
| 语言文件 | 新增 6 键（中英对称）：`Options` / `Option.NativeSelfHelp|SeekWork|Purchase|Service` / `Reason.SelfHelpNotNeeded` |
| `Tests/ShadowGoalRankingTests.cs` | 新增 4 用例：四类必在全、**不可行必带原因码**、资格不允许与没岗位是两种原因、可行时不带原因码 |

**购买与服务当前如实为不可行**：价格来源（M7）与服务供给尚未接入 —— 不假装可选。

## 一处由测试拦下的 UX 缺陷

首版 `NativeSelfHelp` 不可行时原因码留空 ⇒ 界面会出现 **`免费自救 ✗（）`** 这样的空括号。
测试 `InfeasibleOptionsAlwaysCarryAReasonCode` 拦下后补了语义正确的键 `Reason.SelfHelpNotNeeded`（"尚未跌破原版自给线"）——
**"不可行"不等于"失败"**，但仍必须解释。

**验证基线**：编译 **0 错 0 警**；测试 **1048 通过 / 0 失败 / 1 跳过**（`M4-OPT-V3.trx`）；verify **PASS**（110 双语键）。已部署。

## M4 剩余缺口（如实登记）

1. **购买方案无数据源**：需价格/报价（属 M7），当前 `hasPricedConsumption` 恒为 `false`。
2. **服务方案无数据源**：需服务供给与队列（属 M10/M11）。
3. **协作方案（v0.9 §2 提到的"协作"）未建模**：属 M8（家庭与照护协作）。
4. **"执行原版合法 Job"** 在影子模式下**有意不做**（`safeDecision` 只输出决策）。

**未做独立 Gate**（沿用自评结论：无 Harmony、无新跨库面、无静态状态、Save 未变）。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

---
change_id: CHG-20261009-ECON-M6-EXIT
state: ImplementedAndDeployedByThisBatch
scope: M6「就业与劳动合同」实装 —— 合同合理退出（到期/计件上限/制度关闭/岗位不可用）与工作类型释放
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Employment/ContractExitRules.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
  - path: Tests/EmploymentContractExitTests.cs
---

# M6 合同合理退出 变更定位

**触发**：PM 批准「逐个模块实装」；M3（4 批）、M5、M4 已做满 ⇒ 转入 **M6 就业与劳动合同**。

## 差距分析（对照 v0.9 §2 M6 六个动词 + `10-全工作关闭后自主求职_缺口核查` 的运行链）

| M6 要求 | 现状 |
| --- | --- |
| 比较岗位 / 资格 / 报酬 | ✅ `WorkOpportunity.Eligible`（能力/执行/资金/可达/时效/工资/到账全门）|
| 自主申请 | ✅ `TrySignForOpportunity`（政策/时窗/区域/班次/工作类型逐项校验）|
| 履约 | ✅ 证据链 `WorkEvidence` + `WorkContract.CanAcceptEvidence` 三重门 |
| 结算 | ✅ `TrySettlePayable`（command id 由 obligation 派生，幂等）|
| 所有权接管 | ✅ `WorkOwnershipRules` + `ApplyWorkTypes`（只在合同 Active 之后接管）|
| **合理退出** | ❌ **无周期复查** —— 本次实装 |

**关键发现（真缺陷）**：`OnPlanPublished` 原先在**制度门之后**才取 pawn，而释放工作类型的代码只存在于**换岗路径**（`TrySignForOpportunity` 里 `Terminate + ReleaseWorkTypes`）。因此：

> 制度一旦关闭（或岗位消失、合同到期），**没有任何代码路径会释放已占用的工作类型** —— 工作类型会被永久占着，玩家看到的是"工作被占用了但没人干活"。EMP-07 明确要求"离岗、取消合同、关闭制度 ⇒ 释放岗位并按所有权恢复工作设置"。

## 改动

| 文件 | 改动 |
| --- | --- |
| `Domain/PawnEconomy/Employment/ContractExitRules.cs`（新增）| `ContractExitCode`（None / Expired / JobCapReached / InstitutionClosed / PositionUnavailable）+ 纯函数 `ShouldExit` + `IsNaturalEnd`。**顺序即优先级**：自然结束优先于供给侧原因（前者不需玩家解释）。**幂等**：已非 Active 恒返回 None |
| `Economy/PawnEconomy/EconomyEmployment.cs` | 新增 `MaintainContract(pawn, now)`：复查 → `Employment.Terminate` → `ReleaseWorkTypes`（释放仍走 `CanRestore` 门，玩家改写值保留）。`OnPlanPublished` 改为**先复查退出、再过制度门**，并补注释说明为何顺序不能反 |
| `Tests/EmploymentContractExitTests.cs`（新增）| 9 用例：正常运行不结束 / **到期优先于其它原因** / **到期边界与 `CanAcceptEvidence` 一致**（now == ExpiresTick 仍可记工）/ 计件上限 / 制度关闭 / 岗位不可用 / **已结束不二次退出** / null 安全 / 自然结束与供给失败的区分 |

**边界一致性**：`ShouldExit` 用 `now > ExpiresTick`，与 `WorkContract.CanAcceptEvidence` 的 `ContractExpired` 判定**同一不等式** —— 两处不同会让"最后一天能不能记工"出现分叉，已有专门用例钉住。

**零新键、零契约破坏、零 Save 影响**：退出原因码目前**不外露**（无 UI 消费者），故不新增语言键（避免死键）；`WorkContract` 字段未动。

**验证基线**：编译 **0 错 0 警**；测试 **1057 通过 / 0 失败 / 1 跳过**（`M6-EXIT-V1.trx`，较上批 +9）；verify **PASS**（110 双语键）。已部署。

## `ECON-AUTOEMPLOY-001` 的进度（仍 Open）

本批解决的是该缺口运行链**末端**的一环。整条链（核查文档 line 29）现状：

```
需求缺口和储备缺口 → 收入目标 → 有资金/可达/可执行岗位 → 能力与技能筛选
→ 有效合同 → 岗位所需工作优先级接管 → 原版 WorkGiver 实际劳动
→ 经核验的工资支付 → 消费与储蓄 → 重新评估
```

| 环 | 状态 |
| --- | --- |
| 收入目标 → 岗位 → 筛选 → 合同 → 接管 → 支付 | ✅ 已有（B3/B4）|
| **合同结束 ⇒ 释放工作类型** | ✅ **本批** |
| EMP-01「全部工作优先级 0 时自主求职并签约」 | ⚠ 代码路径存在（`OnPlanPublished` → `TrySignForOpportunity`），但**实机未验**，且受 `NewCommitmentsAllowed()` 制度门限制（需 B2.4.3 验收单元）|
| 能力禁用 / 玩家改写的**逐类型**退出 | ❌ 需 `EconomyWorkOwnership` 暴露逐类型状态后 feed 到 `ShouldExit` |
| 退出原因**可见**（EMP-03）| ❌ 需 UI 接通（签约失败原因目前也被丢弃在 `out string _`）|
| EMP-05 多人争抢最后岗位 | ❌ 跨 Pawn 争用规则未独立建模 |

**未做独立 Gate**（沿用自评结论：无 Harmony、无新跨库面、无静态状态、Save 未变）。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

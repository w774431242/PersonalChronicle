---
change_id: CHG-20261010-ECON-LINKFIX-B
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: Implemented
scope: 用户「批准下一步」—— 链路审计 B 组：Vanilla 合同补"仍可工作"退出判据 + 证据键安全裁剪；P1-2 按裁决**保留柜台维度**（不改）；P1-1 缩件列入下一批
rule_ids: AI-001, AI-003, AI-007, DATA-003, SAVE-003, PERF-001, TEST-001, DELTA-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs
    type: M
  - path: Source/PersonalChronicle/Data/PawnEconomy/EmploymentSaveData.cs
    type: M
  - path: Source/PersonalChronicle/Application/PawnEconomy/Employment/EmploymentService.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs
    type: M
---

# 链路审计 B 组 · CHG-20261010-ECON-LINKFIX-B

**ChangeID**：`CHG-20261010-ECON-LINKFIX-B`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**用户决策**：「P1-2 保留柜台维度，移除统一聚合页面。批准下一步」

---

## 一、本轮 2 项实施

### 1. Vanilla 合同补「仍可工作」退出判据（审计 A 席 P1-②）
`EvaluateVanillaContractExit` 增加与**车间路径对称**的判据：持约者被禁用该工种 / 优先级归零 / 伤残致不可用时，`positionAvailable = false` ⇒ 合同退出。

- 复用**同一实现** `CanStillWork(pawn, contract)`（车间路径既有，纯只读）；
- Pawn 离图/不可寻时不在本判据内（无实例可判），仍由 `TrimExpiredContracts` 到期兜底 —— 与既有"离图 Pawn 由不依赖实例的清理覆盖"设计一致。
- **修掉的问题**：此前"活跃合同 + 无人可做"可滞留最长一个合同期（60000 tick ≈ 1 游戏日），期间锁住待办与他人的签约名额。

### 2. 证据键**安全裁剪**（审计 B 席 P1-③：`evidenceKeys` 只增不减 ⇒ 触顶 4096 后**全链永久停付**）
- 新增 `EmploymentSaveData.TrimSettledEvidenceKeys()`：仅删除"**对应合同已非活跃 且 其应付账已付清**"的键；只要合同仍活跃或义务未付清，**一律保留**（从严）；
- 服务层 `EmploymentService.TrimSettledEvidenceKeys()` **仅在接近上限时**执行（`CanRecordEvidence` 探测），避免无谓遍历；
- 接入 `GameComponentTick` 的 **250 tick 周期维护**（不放在计酬热路径）。

**安全性论证（为什么"已结清"可裁，而载入时不可裁）**：证据键含**完成 tick**（`DedupeKey` 的 Sequence）—— 历史 tick 的上报**不会重放**（同一件工作的同一 tick 只被记录一次，不同 tick 产生**新键**）。因此对已结清且合同终态的键，删除不会让任何历史事件被重新计酬。既有注释（`EmploymentSaveData.cs:67`）担心的"丢键 ⇒ 重放双付"针对的是**载入**场景，与本裁剪不冲突（本裁剪不删任何"未结清/活跃"键）。

## 二、按裁决**不做**的项

| 项 | 裁决 | 处置 |
|---|---|---|
| **P1-2 多柜台重复承诺** | 用户**保留柜台维度** | ✅ **不改**：`VanillaJobsCommitted` 维持 `(workType, anchorKey)` 口径 —— 每个劳务站各自配额；"可派发"数字按站计算（站间不共享工作池）。原审计建议的"图级口径"**不采纳**，已登记 |

## 三、列入下一批（本轮未做，如实登记）

| 项 | 内容 | 前置 |
|---|---|---|
| **P1-1 取消后幽灵承诺** | 合同"无待办且无可兑现"时**缩件**（把 `MaxJobs` 降到实际剩余） | 需破例"合同条款不随时间变"的领域不变量 ⇒ 需领域层配合 + 幂等/回放测试，属独立批次 |

## 四、立项（用户新指令）：**移除统一聚合页面**

**范围评估**：`Dialog_PawnEconomy`（10 页签、8 个 partial、约 130KB 代码）是唯一的跨机构窗口；"移除"不等于删除功能，**各页必须先有去处**：

| 聚合页 | 建议去处（我的推荐） |
|---|---|
| 机构列表 / 政策 | **工坊管理处**（政策编辑：车间区/时段/工资）+ 各柜台窗口自带本柜台政策入口 |
| 账户（缴存/提取/公共资金） | **工资结算处** |
| 台账 / 报告 | **工资结算处**（资金视角）与**体检中心**（报告视角）各取所需 |
| 需求观察 / 照护 | **总控面板**（`MasterControlTabWindow.Economy`，本就是跨机构视角） |
| 总览 | **总控面板** |

**工作量**：中偏大（约 6–10 个文件：5 个柜台窗口承接对应页 + 删除聚合窗口 + 全部深链改造 + 键审计）。**本轮未开工** —— 请确认上表分配（尤其"需求/照护/总览"是否归总控面板），我再按批次实施。

## 五、验证

- 编译 **0 错 0 警**（`BuildStamp 2026-10-10 14:32 UTC`）；`dotnet test` **1286 通过 / 0 失败 / 1 跳过**；部署成功。
- **NOT RUN**（实机）：① 签约后把该殖民者的工种禁用 ⇒ 合同应在下次复查（≤250 tick）退出并释放工作类型；② 长局中证据键接近 4096 时不再永久停付（需长局数据，建议后续用诊断读数观察）。

## 六、回滚

`git checkout -- <4 路径>`；`TrimSettledEvidenceKeys` 只删已结清键，回滚不影响存档可加载性。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（4/4）
- [x] 清单范围 ⊆ Scope
- [x] P1-2 裁决与"聚合页移除"立项均已登记
- [x] 无调试代码 / TODO 残留
- [x] 已用 `gate-runner.ps1` 自动校验

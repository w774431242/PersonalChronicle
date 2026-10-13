---
change_id: CHG-20261010-ECON-LINKFIX-A
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: Implemented
scope: 用户「按你推荐修复」—— 链路审计 A 组（低风险高价值 5 项），含把来源判定升级为三态以修正上一批的缺陷
rule_ids: AI-001, AI-003, UI-001, UI-006, LOC-001, GOV-010, TEST-001, DELTA-001
files:
  - path: Source/PersonalChronicle/Capture/Patch_DesignationSource.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
---

# 链路审计 A 组修复 · CHG-20261010-ECON-LINKFIX-A

**ChangeID**：`CHG-20261010-ECON-LINKFIX-A`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**用户决策**：「按你推荐修复」（= 先做 A 组：低风险、高价值）。

---

## 一、5 项改动

### A-1 工作台「链路阻断读数」（审计 A 席 P1-①）
审计结论：链路**接线全部为真，但默认状态不可达**（4 个串联前置），玩家此前只能看到"毫无动静"。
新增 `VanillaChainBlockers` + `AnyFoodPricePublished`（**纯只读**），在劳务站合同区末尾依**闸门顺序**呈现：

> `链路待打通: 未发布计件工资 → 无待办 → 国库为空 → 无已定价消费`
> 或 `链路已通：待办、工资、国库与求职动机齐备。`

四项判定：① 政策未发布工资 ② 待办为 0 ③ 国库读数为空/未就绪 ④ 无任何机构发布食品价（**求职动机来源**）。

### A-2 KPI「未付工资」改读真实欠薪（审计 P2-①）
旧实现读**旧体系** `EconomyReceipt`（恒 0），而真实欠薪在 obligations ⇒「欠着钱却显示 0」。
⇒ 改为本柜台合同对应的 **obligations 未付额**（`AmountMinor − PaidMinor` 之和），与统计行同源。

### A-3 合同行标注「签约价」
政策页/工件行显示的是**当前价**，合同行是**签约快照价**（不追溯，有测试锁定）⇒ 合同行的 `Wage` 标签改为 `Workspace.ContractWage`（**签约价** / signed wage），消除"显示 5.00 按 3.00 付"的预期差。

### A-4 黑名单触顶补日志（审计 P3-②）
玩家集合触顶原本有日志（保守方向：少算待办）；黑名单触顶**方向相反**（漏排除 ⇒ 误付）⇒ 补 `Log.WarningOnce`。

### A-5 来源判定升级为**三态**（修正 `CHG-20261010-ECON-JOB-AUDITFIX` 的缺陷）
审计实测：旧实现是**二值分派**（非玩家 ⇒ 一律自动），而**玩家也会走非设计器路径** —— 例如 `Plant.GetGizmos` 的「全砍枯萎」（栈内无 Designator 帧）⇒ 被误归自动 ⇒ **那些割除既不计待办也不计酬**（与我上一批写的注释矛盾）。

新判据（`Classify()`）：
| 态 | 判据 | 行为 |
|---|---|---|
| **Player** | 栈内存在 `Designator` 的**实例**方法帧 | 进白名单（计待办） |
| **Automatic** | 栈内存在 `CompAutoCut` ∨ **`JobDriver`** ∨ `Designator` 的**静态**帧 | 进黑名单（计酬排除） |
| **Unknown** | 其余（含 gizmo 命令） | **都不进**：不计待办，**计酬照付** |

区分「玩家 gizmo」与「自动维护」的关键：**前者不在 JobDriver 链内，后者在**（枯萎漫延 / 树桩补标 / 矿脉续标都发生在作业链内）。

## 二、验证

- 编译 **0 错 0 警**；`dotnet test` **1286 通过 / 0 失败 / 1 跳过**；部署成功。
- **NOT RUN**（实机）：重启后核对 —— ① 劳务站合同区末尾出现"链路待打通: …"并按实际状态变化；② 政策未设工资时第一项即为它；③ KPI 未付工资在欠薪时显示真实欠款；④ 合同行标签为"签约价"。

## 三、未做（A 组之外，待你决定是否继续 B 组）

| 项 | 内容 | 备注 |
|---|---|---|
| P1-1 | 取消后"幽灵承诺"（承诺 > 待办）的终结/缩件策略 | 需 AI-007 论证（涉及合同语义） |
| P1-2 | `VanillaJobsCommitted` 改为**图级口径**（去掉锚维度） | 会改变"每站独立"语义，需裁决 |
| P1-3 | `evidenceKeys` **安全裁剪**（4096 永不裁剪 ⇒ 长局永久停付） | 须证明裁剪后重放不会续付 |
| — | Vanilla 合同补"仍可工作"退出判据（与车间路径不对称） | 函数已存在，只读幂等 |
| — | 死代码清理（持约切换分支不可达 / `CurrentWorkPriority` 只写 / `QueryOwnedWorkTypes` 无调用者） | 维护噪音，零功能影响 |

## 四、回滚

`git checkout -- <5 路径>`；零持久化面变更（三态只影响"是否记键"，集合结构不变）。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（5/5）
- [x] 清单范围 ⊆ Scope
- [x] Change ID 与审计发现逐条可互链
- [x] 无调试代码 / TODO 残留
- [x] 已用 `gate-runner.ps1` 自动校验

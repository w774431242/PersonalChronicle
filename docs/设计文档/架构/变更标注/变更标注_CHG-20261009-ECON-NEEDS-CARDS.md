---
change_id: CHG-20261009-ECON-NEEDS-CARDS
state: ImplementedAndDeployedByThisBatch
scope: 「个人需求」页三页签全部重画为卡片化；并把「模型有数据但未上屏」的 5 类内容补上只读页
files:
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Measure.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Demand.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Goal.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Decision.cs
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyPlanning.xml
  - path: Languages/English/Keyed/PawnEconomyNeeds.xml
  - path: Languages/English/Keyed/PawnEconomyPlanning.xml
  - path: _scripts/verify-pawn-needs-ui.ps1
---

# 需求页卡片化 + 模型内容补齐 变更定位

**触发**：PM 实机反馈 2026-10-09 ——「其他的文本等等 UI 组件太简陋，其次是否符合 AI 模型内容？」

**根因诊断（已取证）**：
1. **表现层**：目标页签只有 3 行数据（约 85% 空白）；决策页签是 11 行纯 `标签: 值` 文本，零视觉层级
2. **模型覆盖**：逐项比对设计文档后确认 **5 类内容「数据已有但没上屏」**（优先级/活跃性、进入阈值、缺口三口径、预算三分配、候选岗位），另有 2 类「模型有而代码无」（未来缺口预测、消费服务 L1–L4）

**PM 裁决（三项，不可再议）**：① 做第一层（表现层）+ 第二层（把已有数据全部上屏）；未来缺口与消费服务**单独立项** ② **四循环作为决策页主结构** ③ **保持三页签**，目标页签扩容

**部署事实**：本批**已部署**。⚠ 发布目录同期存在并行会话写入活动。

**验证基线**：编译 **0 错 0 警**；全量测试 **1030 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/NeedsCycle-V16.trx`）；`verify-pawn-needs-ui.ps1` **PASS**（**110** 双语键 / 40 用例；本批新增 20 条断言）。

## 改动定位

| 层 | 文件 | 改动 |
| --- | --- | --- |
| Domain | `ShadowPlan.cs` | 新增只读字段 `Opportunities`（候选岗位，上限 `MaximumCandidates=16`）+ 构造器可选参数；**纯内存态，不触 GATE-SAVE**（持久化载体是 `ShadowPlanLatch`） |
| 表现层契约 | `NeedDashboardPresenter.cs` | 新增 4 个结构化类型 `NeedGoalLine` / `NeedBudgetView` / `NeedCycleLine` / `NeedCycleTone`；`NeedDashboardView` 新增 4 个分区（`Goals` / `Budget` / `Opportunities` / `Cycles`，全部 additive 可选）；`NeedTableLine` 新增 `Priority` / `Active`；`Build` 新增可选 `goals` 参数并按 **NeedId 结构化键**回填优先序与活跃性（禁止文本匹配） |
| 分区产出 | `ShadowPlanPresenter.cs` | 新增 4 个入口：`BuildGoals`（双阈值 + 三口径缺口）/ `BuildBudget`（结构化 + 三分配 + 派生比例）/ `BuildOpportunities` / `BuildCycles`（四循环映射）。**策略阈值只在此处转文本**，表现层不读 `PlanningPolicy` |
| 装配 | `EconomyNeedDashboard.cs` | 单人路径喂全部 6 个分区（longTerm / goals / budget / opportunities / cycles / planning）；名册路径不变 |
| 表现层 | `NeedsCyclePanel*.cs` | **按页签拆为 5 个 partial 文件**（编译后仍是同一类型 ⇒ verify 的 `GetType`/IL 扫描无需改动）：壳 255 / Measure 284 / Demand 96 / Goal 75 / Decision 101 行。需求页行内加**优先级芯片 + 活跃呼吸点**；目标页改**卡片式**（每条需求一张卡：芯片 + 呼吸点 + 满足度条 + 「进入阈值 → 目标值」+ 缺口三口径）；决策页改**四循环主结构** + 影子仲裁 + 预算卡（现金行带进度条、含三分配）+ 候选岗位 + 观测明细 |
| 语言键 | 4 个 XML | `PawnEconomyNeeds.xml` +12、`PawnEconomyPlanning.xml` +13（中英对称） |
| 验证 | `verify-pawn-needs-ui.ps1` | +20 断言：5 个分区存在性、5 个 presenter 入口唯一性、6 个分区被表现层消费、4 个新 RUIF 原子被使用且在被引用程序集内确实存在、`StatRow` 5 参重载存在 |

## 新引用的 RUIF 成员面（成员级扩展，**无新增程序集引用**）

`PanelAtoms.StatRow(Rect,string,string,float?,Color?)`（带进度条重载）、`DataAtoms.TraitChips`、`StatusAtoms.PulseDot`、`StatusFmt.DotRect`、`VisualAtoms.Swatch`。
这 5 项此前未被本项目引用 ⇒ 按 `ADR-AMD-2` 属**成员级**跨库引用扩展（同一已引用程序集内的成员，非新增程序集引用）。

工程**无集中 EXC 登记表**（已实查：仅有变更标注、Gate 记录与 DELTA 排除清单），故**以本标注为唯一登记载体**：

```
EXC-2026-10-09-NEEDS-CARDS-RUIF-MEMBERS
事实：新引 5 个 RUIF 成员（PanelAtoms.StatRow 5 参重载 / DataAtoms.TraitChips /
      StatusAtoms.PulseDot / StatusFmt.DotRect / VisualAtoms.Swatch），均在已引用程序集内。
风险：RUIF 升级若改签名会在**编译期**失败（不是运行时崩溃）；本页无反射、无动态查找。
缓解：verify 已做双向断言 —— 表现层确实用到 + 被引用程序集内确实存在。
回退：一次 git revert（本页改动自包含，无持久化字段、无 Harmony、无跨模块耦合）。
有效期：至 RUIF 主版本升级时复核；届时若断言失败即为提醒。
```

## 模型对照（本批结果）

| 模型要素 | 本批前 | 本批后 |
| --- | --- | --- |
| 需求优先级 P0/P1/P2 + 活跃性 | 只在决策页挤成一行文字 | 需求行芯片 + 呼吸点；目标卡芯片 + 呼吸点；四循环明细列出 P 序链 |
| 进入阈值 vs 完成阈值 | 完全未呈现 | 目标卡「进入 X% → 目标 Y%」 |
| 缺口三口径 | 只显示绝对缺口 | 目标卡「相对 / 绝对 / 原始」三口径齐全，不可用时给事实状态 |
| 预算三分配 | **零消费方** | 预算卡内呈现 |
| 候选岗位可行性 | 只显示选中 ID | 候选岗位区逐条列出（工资/能力/执行/资金/可达） |
| 四个业务循环 | 完全未呈现 | **决策页主结构**（消费与服务如实标注"未实装"）|
| 未来缺口预测 | 无 | **仍未做**（单独立项）|
| 消费与服务 L1–L4 | 无 | **仍未做**（单独立项）|

## 未做（如实登记）

1. **实机未验收**：已部署待重启确认。重点看：目标卡 6 张是否撑满且不溢出、四循环行明细在长文本下的折行、预算卡现金行进度条与 `StatRow` 既有排版的配合。
2. **独立 Gate 未执行**：三个 Gate 子 agent 的派发均被环境中断（`code=10003`），本批的 ARCH/COMP/DATA 评审为**主 Agent 自评**（结论已写入对话记录），按 GOV-012 **不得记为独立 PASS**。
3. **`StatusKind` 未引入契约层**：`NeedCycleTone` 为自研枚举，避免第三方枚举泄漏进读模型；表现层做唯一一处 `Tone → Color` 映射。
4. **四循环映射是呈现口径**，未新增 Domain 字段；若后续 M7 落地，"消费与服务"需改为真实状态（届时同步 `BuildCycles` 与四循环键）。
5. **`NeedDatabase` 的 `Priority` 语义**：影子 `Priority` 里 0 同时表示"安全级"与"未纳入"，本批用 `Active` 区分（安全级必 Active）；若后续引入显式"未纳入"标记需同步。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。裁决与放行由 PM 负责。

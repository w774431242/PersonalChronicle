---
change_id: CHG-20261010-ECON-LABOR-UI
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: Implemented
scope: 用户需求「继续UI工程，专注劳务站的」—— 劳务站工作台语义修正与噪音削减
rule_ids: AI-001, GOV-009, GOV-010, UI-006, LOC-001, LAND-010, TEST-001, DELTA-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
---

# 劳务站工作台 UI 完善 · CHG-20261010-ECON-LABOR-UI

**ChangeID**：`CHG-20261010-ECON-LABOR-UI`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**用户需求（原话）**：「继续UI工程，专注劳务站的」（前置：割除识别与多合同派发已实机生效，截图确认「64 割除 / 已派发 / 可派发 / 每份 32 / 每份 −/+」正常）

---

## 一、本轮 4 项改动

### 1. 语义泄漏修正：劳务站不再显示「车间区」
`PolicySummary(policy, kind)` 原先对**所有**机构类型统一输出 `车间区: #N`（`EconomyWorkspaceQueries.cs:152-168`）。但车间区只有**工坊管理处**有语义 —— 劳务站 / 商店 / 工资结算处 / 体检中心都不绑车间区。玩家在劳务站看到"车间区: #1"会去找一个并不存在的绑定。
⇒ 改为仅在 `InstitutionKind.WorkshopOffice` 时输出 Zone 段。

### 2. 工资措辞按机构语义统一
劳务站是**按件付酬**（每完成一件给一笔），但摘要行沿用通用的 `Policy.Wage`（"岗位工资 / Wage per job"），而同一窗口的工种行写的是"每次真实生产完成工资 5.00" —— 同一概念两种措辞。
⇒ 新增键 `PC_PEA.Policy.PieceworkWage`（**计件工资** / **Piecework wage**），劳务站用它，其余柜台沿用 `Policy.Wage`（语义与显示都不变）。

### 3. 噪音削减：移除每柜台都追加的「旧动词」说明行
`rows.Add(Hint(T("Workspace.Hint.OldVerbsClosed")))` 会在**每个**柜台明细尾部追加一段：「旧动词（撤下 / 解约 / 上架 / 指派医生 / 预约体检 / 补发欠薪）的写入路径已关闭，因此本页只展示事实…」。这是**开发者向**的解释（为何没有那些按钮），对玩家是噪音；F4 的诚实性由"行动区只列真正可用的动作"直接体现。
⇒ 整行移除；中英两个键**一并删除**（LOC-001：不留孤儿键）。

### 4. 劳务站提示精简 + 建立"按钮 ↔ 数值"联系
`Workspace.VanillaHint` 原为约 4 行长文（含"Phase 2 扩展建造 / 拆除 / 收获 / 狩猎"等开发信息）。现精简为两行用户向表述，并**显式指向**上方按钮：

> 识别玩家下达的砍伐 / 割除 / 采矿动作，按完成件数从国库计酬（名称取自原版工作定义，沿用它俩的第三方内容同样适用）。**用上方「每份」按钮调整每份合同的件数**：多名求职者会各签一份并行开工。

这样玩家不必自己把"每份 −/+ 按钮"与下方工种行的"每份 32"联系起来。

## 二、Gate 结果

| Gate | 结果 | 要点 |
|---|---|---|
| **UI** | **PASS** | 语义修正方向一致（"只显示对该柜台成立的事实"）；无新增布局与几何变更（`WorkspaceView` 结构未动，LAND-010 零特判约束保持）；文案缩短不引入截断风险（行族自走省略 + tooltip） |
| **LOC** | **PASS** | 新增 1 键（`Policy.PieceworkWage`）+ 删除 1 键（`Workspace.Hint.OldVerbsClosed`）；**中英双份同步**、无孤儿键、无占位符；`PawnEconomy.xml` 两份键集合一致 |
| **TEST** | **PASS** | 编译 0 错 0 警；`dotnet test` **1286 通过 / 0 失败 / 1 跳过** |
| **DATA/SAVE** | **N/A** | 零持久化面变更（纯读模型文案与展示条件） |
| **DELTA** | **PASS** | 覆盖 3 个改动文件，无遗漏/超范围/残留 |

## 三、未做（下一批候选，均需你裁决）

| # | 候选项 | 价值 | 代价 |
|---|---|---|---|
| A | 行动区右侧**只读指示**「每份 32 件」 | 点 −/+ 时即时可见，不必读下方行 | `WorkspaceView` 加一个只读字段 + UI 画一处文本（结构变更） |
| B | 劳务站**完成统计**（今日完成 N 件 / 已付 M / 平均单价） | 玩家能看到"钱花在哪、干得怎样" | 需新增读模型字段（从 `EconomyReceipt`/obligation 聚合），有 Save 无关但读路径新增 |
| C | 待办行**进度条**（已派发 / 待办比例） | 一眼看出派发进度 | 组件绘制（ProgressAtoms）接入 |
| D | 承接中合同列**进度**（`CompletedJobs/MaxJobs`） | 一眼看出谁在做多少 | 行族加进度元素（与 C 同一 helper） |
| E | 「每份」改为**单击循环预设**或弹出选择器 | 少点几次 | 交互语义变更（当前 ±1 档位已够用） |

## 四、验证证据

- 编译：`compile_with_sdk.ps1`（`BuildStamp 2026-10-10 12:25 UTC`，0 错 0 警）
- 测试：`dotnet test` → 1286 通过 / 0 失败 / 1 跳过
- 部署：`deploy-to-game.ps1` 成功（Defs 37 / Languages 71 / Assemblies 1）
- **NOT RUN**：实机视觉核对（沙箱无游戏进程）—— 需重启 RimWorld 后打开劳务站确认：头部摘要不再出现"车间区"、显示"计件工资"；明细尾部不再有"旧动词…"一行；提示为两行且提到「每份」按钮。

## 五、回滚

`git checkout -- <3 paths>`；无 Save 侧残留（零持久化面变更）。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（3/3）
- [x] 清单范围 ⊆ Scope
- [x] 每个文件标注类型与摘要
- [x] Change ID 与 Gate 结论可互链
- [x] 无调试代码 / TODO 残留
- [x] 已用 `gate-runner.ps1` 自动校验

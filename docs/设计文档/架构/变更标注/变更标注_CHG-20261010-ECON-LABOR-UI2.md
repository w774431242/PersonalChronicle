---
change_id: CHG-20261010-ECON-LABOR-UI2
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: Implemented
scope: 用户「批准」（= 按推荐 B→C+D→A）—— 劳务站工作台补齐进度可视化、完成统计与参数即时可见
rule_ids: AI-001, UI-001, UI-002, UI-006, LAND-008, LAND-010, PERF-002, LOC-001, TEST-001, DELTA-001
files:
  - path: Source/PersonalChronicle/Application/PawnEconomy/EconomyView.cs
    type: M
  - path: Source/PersonalChronicle/Application/PawnEconomy/WorkspaceView.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
---

# 劳务站工作台 · 进度可视化与完成统计 · CHG-20261010-ECON-LABOR-UI2

**ChangeID**：`CHG-20261010-ECON-LABOR-UI2`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**用户决策**：「批准」= 按推荐顺序 **B → C+D → A** 实施（前置 `CHG-20261010-ECON-LABOR-UI` 已交付语义修正与噪音削减）。

---

## 一、本轮 4 项

### B · 完成统计行（`EconomyVanillaWork.VanillaWorkStats`）
合同区末尾新增一行：`已完成 N · 已付 M`。
- **已完成件数** = 本图该柜台活跃 vanilla 合同的 Σ`CompletedJobs`（与合同行**同源**）；
- **已付工资** = **这些合同 id 对应的**应付账 `PaidMinor` 之和 —— 按 `ContractId` 归属过滤，因此**不会把车间岗位的工资算进劳务站**；
- 只读、有界（遍历本图自由殖民者 + 其 obligations），只在 60 tick 快照刷新期执行（非每帧）。

### C · 待办行进度 = 派发覆盖率（`已派发 / 待办`）
### D · 合同行进度 = 完成度（`已完成 / 总件数`）
两者都通过**行族既有的自定义行回调**绘制（`EconomyRowList.Draw(..., rowHeight, customDraw, button)`），**共用行族 `EconomyRowList.cs` 零改动** —— 因此聚合窗口（同一行族）零回归。
- DTO 侧：`EconomyRow` 新增**可选**字段 `float Progress`（默认 `-1f` = 无进度 ⇒ 走普通卡片路径）；
- 绘制侧：`RUIF.ProgressAtoms.CapacityBar`（含超载虚线段语义的容量条原子），轨道高 6px，行高 46f；
- 边界：待办 = 0 时**不给**假进度（`-1f`）；构造期夹取上限（>1 会被画成"超载红段"，语义不符）。

### A · 行动区只读参数说明
`每份 − / 每份 +` 按钮右侧显示当前值（`每份 32`），点 ± 后**同一行**立即反映，不必往下读工种行的"每份"字段。
- `WorkspaceView.ActionNote`（组装后赋值，避免改动构造签名 ⇒ 既有调用点零改动）；
- 组装在应用层（`EconomyWorkspaceQueries`），窗口内**零机构类型特判**（LAND-010 保持）。

## 二、Gate 结果（`pc-ui-loc-guard` 独立只读审查）

| Gate | 结果 | 要点 |
|---|---|---|
| **UI** | **PASS WITH EXCEPTION** → **P2 已修复** | 独立审查确认：测量/绘制用**同一**高度回调（不会重叠/截断）；绘制路径**零** `Measure`、零集合分配；`VanillaWorkStats` 只在 60 tick 快照期执行；`Land-010` 零特判保持；边界（-1 / 待办 0 / 越界）全部正确 |
| **LOC** | **PASS** | 新增 2 键中英双份各恰 1 次、无重复无孤儿、无占位符；`VanillaPerContract` 复用恰当（未为同一概念造新键） |

**P2（已修）**：`CapacityBar` 未显式传色 ⇒ RUIF 默认取色 `>85% ⇒ Warn`，会把 **100% 完成画成警示橙**（与语义相反）。本工程成文纪律要求所有调用点显式传色（`RuifBridge.cs:350-351`，全仓其余 8 处均显式传）。⇒ 改为显式 `RUIF.Tokens.Alive`。
**P3-c（已顺手修）**：`EconomyRow` 构造期夹取 `Progress` 上限，绘制层可无条件信任 `Progress ∈ {-1} ∪ [0,1]`。

## 三、已知观察（登记不修，来自 Gate）

1. **P3-a**：`46f` / `16f` / `6f` 为字面量，未派生自 `RUIF.Measure.RowHeight` 也未收口进 `EconomyWindowLayout`（该文件自述"纯几何…供绘制与离线断言共用同一组数字"）。建议后续迁移。
2. **P3-b**：进度行未走 `EconomyUiKit.CardFrame`（普通行与机构卡片走卡片框），视觉上与其他数据行不同族 —— **本轮有意**（进度条本身已提供视觉分隔，且避免每行再描边）；若需完全统一可在下一批接入卡片框 + 悬停高亮。
3. **口径透明性**：`已完成 / 已付` 只统计**活跃**合同（合同做满/到期退出后数字回落）。这是"当前在册合同的进度"口径；若要"全量台账"口径需改为遍历历史（另立项）。
4. **窄窗**：极窄时行动区说明可能整体省略（`x < rect.xMax` 分支），信息仍在明细行"每份"字段中，不构成丢失。

## 四、验证证据

- 编译：`compile_with_sdk.ps1`（`BuildStamp 2026-10-10 12:38 UTC`，0 错 0 警）
- 测试：`dotnet test` → **1286 通过 / 0 失败 / 1 跳过**
- 部署：`deploy-to-game.ps1` 成功
- **NOT RUN**：实机视觉核对（沙箱无游戏进程）—— 需重启 RimWorld 后确认：待办行/合同行出现**绿色**进度条（100% 不得为橙）、合同区末尾出现 `已完成 N · 已付 M`、行动区右侧出现 `每份 32`。

## 五、回滚

`git checkout -- <7 paths>`；无 Save 侧残留（零持久化面变更）。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（7/7）
- [x] 清单范围 ⊆ Scope
- [x] 每个文件标注类型与摘要
- [x] Change ID 与 Gate 结论可互链
- [x] 无调试代码 / TODO 残留
- [x] 已用 `gate-runner.ps1` 自动校验

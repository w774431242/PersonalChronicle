---
change_id: CHG-20261010-ECON-LABOR-UI3
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: Implemented
scope: 用户「继续」—— 消化 CHG-20261010-ECON-LABOR-UI2 的 GATE-UI P3 观察：进度行与数据行视觉同族 + 几何常量收敛
rule_ids: AI-001, UI-001, UI-006, LAND-008, LAND-010, PERF-002, TEST-001, DELTA-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs
    type: M
---

# 进度行视觉同族化 · CHG-20261010-ECON-LABOR-UI3

**ChangeID**：`CHG-20261010-ECON-LABOR-UI3`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**用户决策**：「继续」（承接 `CHG-20261010-ECON-LABOR-UI2` 的 Gate 转交项 P3-a / P3-b）。

---

## 一、本轮 2 项

### 1. 进度行与数据行**视觉同族**（消化 P3-b）
上一批的进度行是自绘的（无卡片框、无悬停高亮、只有文本级 tooltip），与同列表的数据行不同族。现改为与数据行**完全同源**：

| 元素 | 数据行（`EconomyRowList.cs:186-200`） | 本轮进度行 |
|---|---|---|
| 卡片边距 | `CardSideInset` | **同一常量** `EconomyRowList.CardSideInset` |
| 卡片框 | `EconomyUiKit.CardFrame(card, hovered, null)` | **同一原子**（含悬停提升底 + 描边） |
| 悬停全文 | 卡级 `TipRegion(card, row.Text)` | **卡级**（原为文本级，已统一） |
| 文本 | `GameFont.Small` + `Tokens.Text` | 同 |

进度条本身仍走 `RUIF.ProgressAtoms.CapacityBar(..., RUIF.Tokens.Alive)`（显式传色，见 UI2 的 P2 修复）。

### 2. 几何常量收敛（消化 P3-a，本文件内）
`46f / 16f / 3f / 6f` 四个字面量收敛为具名常量并加注释：
`ProgressRowHeight` / `ProgressTextHeight` / `ProgressBarTopGap` / `ProgressBarThickness`。

**未跨文件迁移到 `EconomyWindowLayout`**：该文件自述契约是"纯几何、零 RimWorld 依赖、供绘制与离线断言共用同一组数字"；迁移需同步其断言集，留待与其余柜台几何一并收口（如实登记，不假装已完成）。

## 二、验证证据

- 编译：`compile_with_sdk.ps1`（`BuildStamp 2026-10-10 12:48 UTC`，0 错 0 警）
  - 过程中修正一处引用错误：`CardSideInset` 的宿主是 `EconomyRowList`（`EconomyRowList.cs:52`）而非 `EconomyWindowLayout` —— 编译期即拦截，未流出。
- 测试：`dotnet test` → **1286 通过 / 0 失败 / 1 跳过**
- 部署：`deploy-to-game.ps1` 成功
- **NOT RUN**：实机视觉核对（沙箱无游戏进程）—— 需重启 RimWorld 后确认：待办行 / 合同行**出现卡片描边**，鼠标悬停时**底与描边提升**并与普通行一致，悬停任意位置显示整行全文。

## 三、未做（登记）

1. **跨文件几何收口**（见 §一.2）。
2. **统计口径**（UI2 已登记）：`已完成 / 已付` 只统计活跃合同；全量台账口径另立项。
3. **`ActionNote` 快照纯度**：`WorkspaceView.ActionNote` 为快照类唯一可写字段（构造后赋值）—— Gate 建议后续并入构造参数；本轮未动（改动会波及既有调用点，收益仅纯度）。

## 四、回滚

`git checkout -- Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs`；无 Save 侧残留（本轮零持久化面变更）。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（1/1）
- [x] 清单范围 ⊆ Scope
- [x] 每个文件标注类型与摘要
- [x] Change ID 与 Gate 结论可互链
- [x] 无调试代码 / TODO 残留
- [x] 已用 `gate-runner.ps1` 自动校验

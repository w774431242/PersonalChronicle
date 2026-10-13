---
change_id: CHG-20261004-WORKTAB-SHIFT
version: v1.1.5
date: 2026-10-03
author: AI
scope: [Source/PersonalChronicle/Archive/UI, Source/PersonalChronicle/Automation, Languages]
files:
  - path: Source/PersonalChronicle/Archive/UI/UIComponents.Sidebar.cs
    type: M
    symbols: UIComponents.ThemedCheckbox / UIComponents.CheckboxBox
    lines: 78-110
  - path: Source/PersonalChronicle/Automation/Dialog_WorkshopCrewEditor.cs
    type: M
    symbols: Dialog_WorkshopCrewEditor.DrawShiftRow
    lines: 217-263
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: Key:PersonalChronicle.Automation.Tab.CrewShiftAssignTip
    lines: 136-137
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: Key:PersonalChronicle.Automation.Tab.CrewShiftAssignTip
    lines: 136-137
---

# 变更标注 · CHG-20261004-WORKTAB-SHIFT（单元 B）

车间「分配岗位」按**班次**逐项**勾选 / 取消**。Build **1.6.4871 rev590** · 编译 **0 错 0 警**（EXIT=0）。

## 1. Scope（4 文件）

| # | 文件 | 类型 | 行 | 内容 |
|---|---|---|---|---|
| 1 | `Archive/UI/UIComponents.Sidebar.cs` | M | +78-110 | 新增 `CheckboxBox=24f` + `ThemedCheckbox(Rect,bool)`；零尺寸早退、勾选图居中 |
| 2 | `Automation/Dialog_WorkshopCrewEditor.cs` | M | +217-263 | `DrawShiftRow`：行尾 68px「负责/移除」按钮 → **行首 24px 恒定勾选框**；勾选框先注册、整行后注册（OR 合并）；延迟求值悬停提示 |
| 3 | `Languages/ChineseSimplified/Keyed/Automation.xml` | M | +136-137 | 新增 `CrewShiftAssignTip` |
| 4 | `Languages/English/Keyed/Automation.xml` | M | +136-137 | 同上，键集对等 |

## 2. 规则 ID

`GOV-010` `GOV-011` `UI-005` `UI-006` `UI-007` `LOC-001` `PERF-001` `AI-014`

## 3. Architecture

落点 `Archive/UI/`（表现层原子）+ `Automation/`（车间表现层），**无新增跨层边**。
**零 Harmony、零 Def、零存档字段、零 SchemaVersion 变更** ⇒ 不需要 EXC。
业务写入路径未变：`WorkshopCrew.AssignShift` / `UnassignShift`（双份同步 `AssignedShifts` + `AssignedShiftIds`）。

## 4. 关键设计决策

| 决策 | 理由 |
|---|---|
| 按钮 → **勾选框**（而非按钮换文案） | 按钮文案随状态翻转（负责/移除）读起来像**动作**；勾选框的勾恒定在控件上、只改变勾本身 ⇒ 状态与动作不再混读。正是用户「按班次取消、勾选」的诉求 |
| **不自绘**勾选图，走 `Widgets.Checkbox` | IL 实证它 `void` + `ref bool` 回写，**自带 `Checkbox_TurnedOn/Off` 音效**；自绘会丢反馈。8 参数显式传全，不依赖 C# 默认值（元数据里默认值不可见，猜错即行为漂移） |
| 勾选框**先**注册、整行**后**注册，OR 合并 | 点勾选框不会落到行按钮；点行任意位置都是「翻转一次」，**不会双触发**。同既有先例 `WorkshopManageView.Shifts.cs:188-197` |
| 整行可点 | 24px 命中区偏小；整行可点满足「点哪都是勾」，误操作面不增加 |
| 加 `TipRegion` 延迟求值 | 取消勾选的后果（移出候选池、**不影响在岗成员**）不适合塞进标题；`TipSignal(() => …)` 避免每帧拼串（UI-006） |
| 勾选态影响文字亮度（Text / Muted） | 状态一眼可辨，不依赖读勾 |

## 5. Compatibility / Performance / Migration

无 Harmony、无第三方、无 DLC 分支 ⇒ COMP **N/A**。
零新增每帧分配；每行命中区净 +1（行高 30、窗口固定 560×640、可见 6 行）。
**无迁移**：存档零变更；已指派班次读档后仍为勾选态（`AssignedShiftIds` 逐条重建）。

## 6. Gate Results

| Gate | Result | Evidence |
|---|---|---|
| ARCH | **待判** | 分层/依赖已自证；最终判定须由 `pc-arch-gatekeeper` 出（总控 `OWNED_GATES: NONE`） |
| DATA | PASS | 零 Def 新增 |
| SAVE | **N/A** | 零存档字段 / 零 Scribe key / 零 SchemaVersion |
| UI | PASS（静态） | 命中顺序、零尺寸早退、提示串延迟求值；**实机布局未执行** |
| LOC | PASS | 新增 1 键中英对等；`CrewAssignShift`/`CrewUnassignShift` **保留未删**（FloatMenu 仍在用） |
| COMP | **N/A** | 零 Harmony、零第三方 |
| PERF | **未执行** | 无实机采样 |
| TEST | 部分 PASS | 0 错 0 警已达成；**Vanilla 启动 / 实机交互 / 存读档未执行**（沙箱无游戏进程，无伪造 PASS） |
| REL | **未执行** | 版本号 / Changelog 待定稿 |
| DELTA | 见 §8 | 由 `gate-runner.ps1` 机械校验 |

## 7. Remaining Risks

1. **P2** 行尾按钮移除后，不习惯点行首的玩家需要一次提示 → 已用 `TipRegion` 覆盖，且整行可点。
2. **P2** `CrewAssignShift`/`CrewUnassignShift` 保留：`WorkshopManageView.Schedule.cs` 的 `OpenCrewShiftMenu`（FloatMenu 逐项 `✓ ` 切换）仍使用，删除会造成 Keyed 缺失。
3. **P3** 卡片侧「次要 mini chip 行」属 Phase 2，本次未做（D-3 不推翻「一卡一班次」裁决）。
4. **P1** 本单元只改班组编辑器一处入口；排班页 chip 的 FloatMenu 入口仍是「点一次翻转一次」，**尚未**改成勾选框（Phase 2）。

## 8. 附

- 上一单元 `CHG-20261004-WORKTAB-HIDE`（工作面板隐藏名单，8 文件）已单独通过 GATE-DELTA，
  其文件在本单元的 `-ExcludeFromFile` 中按「已交付单元」排除，不重复计入本 Scope。

---
change_id: CHG-20261004-POSTOVERRIDE
version: v1.1.5
date: 2026-10-04
author: AI
scope: [Source/PersonalChronicle, Languages]
files:
  - path: Source/PersonalChronicle/Automation/Zone_Workshop.cs
    type: M
    symbols: Zone_Workshop.GetPostOverride, Zone_Workshop.SetPostOverride, Zone_Workshop.PrunePostOverrides, Zone_Workshop.ExposeData
    lines: 141-224, 528-535
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Posts.cs
    type: M
    symbols: WorkshopManageView.PostLabelOfPawn, WorkshopManageView.IsPostOverridden, WorkshopManageView.OpenPostOverrideMenu
    lines: 174-237
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.cs
    type: M
    symbols: WorkshopManageView.DrawMemberRow
    lines: 951-965
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: Key:PersonalChronicle.Automation.Tab.SetPost, Key:PersonalChronicle.Automation.Tab.PostFollowAuto
    lines: 110-112
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: Key:PersonalChronicle.Automation.Tab.SetPost, Key:PersonalChronicle.Automation.Tab.PostFollowAuto
    lines: 110-112
---

# 变更标注 · CHG-20261004-POSTOVERRIDE

人员页手动岗位覆盖。Build **1.6.4871 rev590** · 编译 **0 错 0 警**。

## 问题

玩家报告「人员 tab 无法对殖民者进行岗位手动分配」。核实结论：**岗位在现有 UI 中 100% 自动派生，三处全为只读**，
不是 bug 而是**功能缺失**。

| 位置 | 改前证据 | 可交互 |
|---|---|---|
| 人员 tab 岗位列 | `WorkshopManageView.cs:955-961` 纯 `UIComponents.Label` | ❌ 整行仅 X（`:969`）可点 |
| 岗位 tab | `WorkshopManageView.Posts.cs:311-315` 纯 `Label` + `DrawBoxSolid` | ❌ 无按钮/菜单 |
| 排班页 | 设的是班次 `shift.TaskType`（决定该班覆盖哪些**原版工作类型**） | 非「给某人定岗」 |

数据链单向：`WorkshopPostAssigner`（自动）→ `Candidate.CurrentPostDefName` → `pawnPostDefNames`
（Refresh 期重建，`Posts.cs:70-83`）→ `PostLabelOfPawn()` → 只读 Label。
`Zone_Workshop.Members`（`Zone_Workshop.cs:132/138`）只存「谁在车间」，不存岗位。

## 产品口径（2026-10-04 玩家决策）

> **先由系统自动分配，然后允许玩家手动分配**（覆盖式，非取代）。
> 粒度 = **人级固定**（每人一岗，跨所有班次生效）。

## 变更内容

1. **存档**：`Zone_Workshop.postOverrides`（`Dictionary<string,string>`，pawn `ThingID` →岗位 defName），
   `Scribe_Collections.Look(..., "pcPostOverrides", LookMode.Value)`（`:528-535`）。
   加性可选字段，旧档读回 null ⇒ 空字典 ⇒ **全员跟随自动，行为与改前完全一致**，无需迁移、不 bump SchemaVersion。
2. **写入口**：`GetPostOverride` / `SetPostOverride(pawn, null)`（清除 = 跟随自动）（`:141-183`）。
3. **读优先级**：`PostLabelOfPawn()` 先查覆盖、回落自动（`Posts.cs:174-196`）；
   覆盖项的岗位 Def 失效时**静默回落自动**，不让死defName 把人卡成「未分配」。
4. **UI**：人员页岗位列由只读 Label 改为 `UIComponents.ThemedButton`（`WorkshopManageView.cs:951-965`），
   点击弹岗位菜单（`OpenPostOverrideMenu`，`Posts.cs:204-237`），**首项恒为「跟随自动分配」**。
   手动指定的项在标签后追加 `*` 标记。

### 控件选型与一次返工（玩家实机截图指出）

初版用了原版 `Widgets.ButtonText` —— 实机截图显示它是**琥珀色默认皮肤**，与本工程深色令牌主题
明显不一致（同一表格里「添加人员」「刷新」「每行 X」全是深色面板 + 软边框）。
经核对，工程既有同类选择器（班次编辑器的岗位选择器，`Dialog_WorkshopShiftEditor.cs:144`）
用的是 `UIComponents.ThemedButton`，而每行的移除键（`WorkshopManageView.cs:969`）也是它
⇒ 已改为 `ThemedButton`，与既有范式一致。

> ⛔ 原版 `Widgets.ButtonText` 属组件绘制规范的**「同功能两处实现」反模式**：
> 工程已有主题化按钮时不得再引原版皮肤，否则同一界面出现两套按钮语言。
> 记此一条，后续本工程新增交互控件一律先查 `UIComponents` 有无对应封装。

> **RUIF 关系**：RUIF 是本工程**硬依赖**（`About.xml` modDependencies + csproj 无条件 `Reference`），
> 且 `UITheme` 的令牌在 RUIF 可用时由 `RuifBridge` 同步为 RUIF 令牌（`RuifBridge.cs:91`
> `ReapplyForRuif`，启动期补应用一次）⇒ 走 `UITheme` 即等于走 RUIF 视觉语言，
> 无需为「用了 RUIF」而直接调 `RUIF.Api`（那会触发桥接层的类型位置铁律）。

## 关键设计取舍

- **只存玩家显式指定过的项**：缺项 = 跟随自动 ⇒ 未指定者行为完全不变，不破坏现有平衡（选项「覆盖式」而非「取代」）。
- **刻意不耦合工作类型**：工作类型由班次 `shift.TaskType` 经 `WorkTypeDefNamesOf` 决定。
  玩家口径是「只分配岗位，工作类型自动」，故手动改岗**不得**连带改工作类型格位。
- **ThingID 字符串键而非 Pawn 引用**：跨档稳定，无需处理已卸载 pawn 的引用失效。
- 菜单首项为「跟随自动」⇒ 支持「整体自动 + 个别手动」，且可整批还原。

## 读档自愈（`PrunePostOverrides`，`:185-224`）

丢弃三类失效项：① 键或值为空；② 岗位 Def 已不可解析（第三方移除/改名）；
③ 人已离开车间名单或已死亡（否则存档单调膨胀）。读档路径每档至多一次，与既有
`Attendance.Prune` / `Performance.Prune` 同档位。

## Gate Results

| Gate | Result | Evidence |
|---|---|---|
| ARCH | **待判** | 新增 1 个存档字段 + 2 个公开方法 + 1 个 UI 入口，**无新增层/ 无跨层依赖边**（UI → Automation 既有方向）。**最终判定须由 `pc-arch-gatekeeper` 出**，本单元不自判 |
| DATA | PASS（静态） | 零 Def 新增；`WorkshopPostCatalog.AllPosts()` 为既有只读来源 |
| SAVE | PASS（静态） | 键名 `pcPostOverrides`；Value 模式无引用失效；加性可选 ⇒ 旧档行为不变；`PostLoadInit` 自愈幂等（每项独立判定，可重复执行） |
| UI | PASS（返工后） | 控件**经玩家实机截图指出后返工**：初版 `Widgets.ButtonText`（琥珀原版皮肤）与主题不一致，已换 `UIComponents.ThemedButton`，与既有同类选择器 `Dialog_WorkshopShiftEditor.cs:144` 及每行移除键 `:969` 同范式；菜单沿用既有 `FloatMenu` 范式（对照 `OpenAddMemberMenu`）。**未实机复验**（玩家需重启后确认观感） |
| LOC | PASS | 中英各 2 键对等，无占位符；走既有 `KeyPrefix = PersonalChronicle.Automation.Tab.` |
| COMP | **N/A** | 零 Harmony、零第三方Mod 接入 |
| PERF | PASS | 覆盖项仅在 `PostLabelOfPawn`（绘制路径）做一次字典查找 O(1)；`PrunePostOverrides` 仅读档执行 |
| TEST | 部分 PASS | 0 错 0 警；**实机未执行**（沙箱无游戏进程），菜单交互与 `*` 标记**未经游戏内验证** |
| REL | **未执行** | 版本 / Changelog 待定稿 |
| DELTA | 见 §7 | 由 `gate-runner.ps1` 机械校验 |

## Remaining Risks

1. **P1** 岗位 tab（`Posts.cs:311-315`）**仍只读**——它显示的是「该岗位由谁承担」的统计。
   玩家手动指定某人岗位后，该岗位 tab 的承担者列表**不会**随之改变（两处数据源不同：
   岗位 tab 读分配器结果，人员页读覆盖值）。**这是本单元已知的口径不一致，未修复**。
   若玩家需要两处一致，需另立单元把岗位 tab 也接上覆盖。
2. **P1** 岗位覆盖**不影响实际派工**：工作类型仍由班次决定。若玩家期望「手动定岗 ⇒ 按该岗位干活」，
   当前实现不满足（但这与「工作类型由系统自动分配」的口径一致）。
3. **P2** 一人一岗、跨班次生效 ⇒ 同一人在白班/夜班**不能**有不同岗位（用户已确认接受）。
4. **P2** 实机未验证：FloatMenu 弹出、`*` 标记、存读档后覆盖保持，均需玩家确认。

## 附：与同会话其他单元的关系

- `CHG-20261004-SHIFTGATE`（同会话前序）：离班停工 + 0/3 互跳根除。本单元只加岗位显示层覆盖，
  不触碰 `EnableWorkTypes` / `ShouldSkipAutoEnableForShift` 班次门控，**两者无耦合**。

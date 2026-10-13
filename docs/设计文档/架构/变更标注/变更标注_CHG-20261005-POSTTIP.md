---
change_id: CHG-20261005-POSTTIP
version: v1.1.5
date: 2026-10-05
author: AI
scope: [Source/PersonalChronicle/Automation, Languages]
files:
  - path: Source/PersonalChronicle/Automation/WorkshopManageView.Posts.cs
    type: M
    symbols: WorkshopManageView.DrawPostsTable (岗位标签悬停)
    lines: 393-404
  - path: Languages/ChineseSimplified/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Tab.PostDeliversTip, PostNotDeliversTip, Takeover_no_takeoverable_work, TimeAllDayTip
    lines: 251-255, 446-452, 460-462
  - path: Languages/English/Keyed/Automation.xml
    type: M
    symbols: PersonalChronicle.Automation.Tab.PostDeliversTip, PostNotDeliversTip, Takeover_no_takeoverable_work, TimeAllDayTip
    lines: 251-255, 442-448, 456
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    symbols: PersonalChronicle.UI.Career.LegacyTip
    lines: 1286
---

# 变更标注 · CHG-20261005-POSTTIP

## Change Summary

玩家实机反馈（2026-10-05，车间体系已可正常运行）：

> 「我先点击接管原版工作，提示无可接管的工作，但它显示规划中-未接管。」

**不是功能矛盾，是同一根因的两种措辞 + 解释只在特定条件下显示**：

| 位置 | 文案 | 真实含义 |
|---|---|---|
| 岗位页（每个岗位右侧） | 「规划中·未接管」 | 该岗位**不参与接管**（`implementedWorkTypeDefNames` 为空 ⇒ 车间尚未声明"可独立交付"的工作类型） |
| 设置页（接管按钮下方） | 「无可接管的工作：…」 | **同一件事的另一面**：没有任何岗位声明可独立交付 ⇒ 接管无从下手 |

原实现里那句解释（"只表示该岗位暂不获准接管，与员工能否干活无关"）**只在"未指定车间类型"时**
才显示（`PostsNeedWorkshopType2`）；玩家已选「烹饪坊」⇒ 该引导不显示 ⇒ 只剩两处看似冲突的标签。

## 修法

1. **岗位标签补悬停说明**（`WorkshopManageView.Posts.cs:393-404`）：
   `PostDeliversTip` / `PostNotDeliversTip` —— 讲清"参与/不参与接管"的因果，
   并明确"不影响员工干活（岗位表只做派活建议）"。
2. **重写设置页提示** `Takeover_no_takeoverable_work`：补上"所有岗位都还是「规划中·未接管」"
   与"这与员工能否干活无关"，与岗位页**同措辞互相引用**（两处不再各说一套）。
3. **顺手修 3 处玩家可见文本里的 Markdown 星号**（同类缺陷，非本轮引入）：
   `TimeAllDayTip`（中/英）、`Archive.xml` 的资历来源行 —— 游戏不渲染 `**`，会原样显示。

## 兼容性

- **存档影响：无**（纯 UI 文案 + 一处 Tooltip）。
- **行为影响：无**（`row.Delivers` 判定逻辑未动）。
- 旧档、第三方 Mod：无影响（未新增 Def / Harmony）。

## 验证

| 项目 | 状态 | 结果 |
|---|---|---|
| 编译 | 已验证 | 0 错 0 警 |
| XML | 已验证 | 中英 Automation / 中文 Archive 解析通过 |
| 星号复检 | 已验证 | 玩家可见文本中 `**` **零残留**（注释内的保留，不显示） |
| 部署 | 已验证 | Languages 39 / Assemblies 1 同步 |
| GATE-DELTA | 见下 | `gate-runner.ps1 -Base HEAD -Annotation 变更标注_CHG-20261005-POSTTIP.md -ExcludeFromFile .codebuddy/tmp/posttip-out-of-scope.txt -Blocking` |

## 实机验证要点

1. 车间管理 →「岗位」页：把鼠标停在右侧「规划中·未接管」上 ⇒ 应出现解释
   （"不参与接管 / 不影响员工干活 / 与设置页的『无可接管』是同一件事"）。
2. 同页「设置」页 → 点「接管原版工作」⇒ 提示应已含"所有岗位都还是「规划中·未接管」…"。
3. 【管制】表或班次编辑器里，全天班提示、档案资历行**不应再出现 `**`**。

## Remaining

- 「规划中·未接管」的**标签本身**暂未改名（改它会牵动 `PostsNeedWorkshopType2` 等既有文案的
  交叉引用）。若你认为"规划中"读起来仍别扭，可改成「未实现 Job · 不参与接管」之类，
  我按同一处一次性替换（一次编译）。

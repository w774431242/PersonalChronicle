---
change_id: CHG-20261009-ECON-NEEDS-CYCLE-UI
state: ImplementedAndDeployedByThisBatch
scope: 「个人需求」页右栏需求循环面板的 UI 层重构（表格 → RUIF 卡片化可视化布局）+ present 层数值字段 additive + 本地化/验证脚本迁移
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml
  - path: Languages/English/Keyed/PawnEconomyNeeds.xml
  - path: _scripts/verify-pawn-needs-ui.ps1
---

# 「个人需求」右栏 UI 重构 变更定位

本批是 `CHG-20261008-ECON-NEEDS-CYCLE` 的**表现层第二版**：数据面零改动，只重做右栏绘制。

**触发**：PM 实机截图反馈「UI 布局混乱、太低级」+「没有基于 RUIF 框架模组」。事实核查：第一版确实调用了 RUIF，但只用了最朴素的 `Draw.Label` + `DataAtoms.StatTable`（纯文本表格），未动用 RUIF 的**卡片/分区/可视化**层 —— 因此 PM 的体感成立。

**部署事实**：本批**已部署**（`deploy-to-game.ps1`，233 文件同步，备份 `_backups/release-deploy/deploy_20261009_175208_941`，目标 DLL 时间戳 `2026-10-09 17:51:48`）。⚠ 部署目标目录同一时刻有并行会话写入（部署前发布区 DLL 为 `17:50:53`）⇒ 若并行会话后续再次部署，发布区将不再是本批构建。

**验证基线**：编译 0 错 0 警（`BuildStamp -> 2026-10-09 09:51 UTC`）；全量测试 **1020 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/NeedsCycle-V3.trx`）；`verify-pawn-needs-ui.ps1` **PASS**（85 双语键 / 38 用例）。

| 文件 | 定位 | 说明 |
| --- | --- | --- |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs | 全文件重写绘制层 | 需求改「卡片流」：`Shapes.InsetPanel` 容器 + `Shapes.CornerFrame` 角框（armLen 10 / thickness 1.5）+ 名称与满足度双色对齐行 + `ProgressAtoms.CapacityBar` 可视化条 + `PanelAtoms.StatRow` 缺口行；分区标题改 `PanelAtoms.SectionBar`；「未纳入规划」需求合并为一行 `Tokens.Dim` 提示；折叠按钮旁画 `StatusAtoms.Caret` 表达展开态 |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs | `NeedTableLine` 加 `Ratio01` / `Planned`（additive，可选 ctor 参数）；`Number` 格式 `0.####` → `0.##` | 结构化数值随行携带，消费者不得从文本反解（GOV-009）；修 `14.9648` 类噪声位 |
| Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml | +3 键（共 85） | `Cycle.PlannedCount` / `Cycle.Unplanned` / `Label.Gap` |
| Languages/English/Keyed/PawnEconomyNeeds.xml | +3 键（共 85） | 同上 |
| _scripts/verify-pawn-needs-ui.ps1 | 组件断言更新 | 断言集改为卡片化原子组（`SectionBar`/`CapacityBar`/`InsetPanel`/`CornerFrame`/`Caret` 等）；RUIF 类型存在性表加 `ProgressAtoms`/`Shapes`/`StatusAtoms` |

## 组件选型与契约约束（已读 RUIF 源码逐条核实）

| 组件 | 契约（源码核实） | 本批用法 |
| --- | --- | --- |
| `PanelAtoms.SectionBar(rect, title, count, expanded)` | `Draw.Box(PanelRaised)` + `▾/▸`(Accent) + title(Text) + **count 画在 `rect.xMax - 46f` 的 40px 带内** | count 只放短计数 `{0} / {1}`（原打算写"3 / 6 已纳入规划"，会溢出该 40px 带 —— 已改） |
| `PanelAtoms.StatRow(rect, label, value, bar01, barColor)` | label 55%(Muted) / value 45%(Text)；不传 bar01 时纯文本 | 用于缺口行与影子计划行 |
| `ProgressAtoms.CapacityBar(rect, used, capacity, fill)` | 底 `BorderSoft` + 填充 `fill ?? (r>.85 ? Warn : Alive)`；`r=used/capacity`；`used>capacity` 画 `Dead` 溢出段 | `used=Ratio01, capacity=1f`；**语义借用**（以满足度充当占用率），见"已知 P3" |
| `StatusAtoms.Caret(rect, bool expanded, color)` | `expanded ? ▾ : ▸`（旋转 0°/90°） | 折叠态指示 |

**新增引用的 RUIF 成员**（本项目首次引用，供 COMP 复核）：`PanelAtoms.SectionBar`、`StatusAtoms.Caret`、`Tokens.Dead`、`Tokens.SectionTitleHeight`、`Tokens.CardPadX/CardPadY/ProgressbarH/CardGap`。
**判断依据**：`About/About.xml` 明写依赖「RUI 界面框架（**RUIF 1.3.0 及以上**）」，本机安装 `v1.3.0`；上述成员在 RUIF 1.3.0 源码中均为 `public static` 正式面（非 `*Cand*` 候选面）⇒ 判为合规、不需版本门控（`RuifBridge` 的守卫场景是"RUIF 未装/版本漂移"，硬依赖下不触发）。
**已有先例的组件**（零新增面）：`ProgressAtoms.CapacityBar`、`Shapes.InsetPanel`、`Shapes.CornerFrame`、`PanelAtoms.StatRow`、`Draw.Label`、`Layout.BeginScroll/EndScroll`、`ButtonAtoms.PrimaryButton`、`FeedbackAtoms.EmptyState`。

## PM 原始问题的闭环对照

| # | 原始问题 | 处置 |
| --- | --- | --- |
| 1 | 列头拥挤、「缺口（百分点）」折行 | 取消表格与列头，改卡片流 |
| 2 | 数字 `14.9648` | `Number` 格式改 `0.##` |
| 3 | 「未配置」两列重复 | 改为一条「未纳入规划：美观 / 舒适 / 户外」（`Tokens.Dim`），已规划者才出卡 |
| 4 | 纯文本无层次 | 卡片容器 + 角框 + 满足度条 + 分区栏 + 三档配色（`Alive/Warn/Dead`） |
| 5 | 文本被裁切 | 全部单元格经 `NeedDashboardPanel.Flow` 显式折行 + `Wrapped` 实测高度；卡片高度与绘制推进逐段对齐 |
| 6 | 副标题不可见 | 移除副标题，改由 `SectionBar` 承载标题与短计数 |
| 7 | 「未用 RUIF」 | 见上表组件选型 |

## 未做与已知问题（如实登记）

1. **实机渲染未验**：本批**已部署**，但**未重启游戏验证**（沙箱无游戏进程）—— 卡片间距、角框观感、条色分级、窄屏（右栏 200~260px）表现均未经肉眼确认。
2. **独立 Gate 未完成**：`pc-ui-loc-guard` 对本批的独立复核**两次调用均被工具侧中断**（`No result found` / `aborted`），故本批**没有独立 GATE-UI/LOC 结论**；上表"契约约束"由主 Agent 读 RUIF 源码自查得出，**不构成 Gate 通过**（GOV-012：不得伪造 PASS）。建议下轮优先补做。
3. **已知 P3**：
   - `CapacityBar` 被复用于"满足度"属语义借用，代码内**未加说明注释**（本批未加，避免为注释重建 DLL）；
   - `CornerFrame` 用 `armLen 10 / thickness 1.5`（项目先例为默认 `14 / 2`），属观感调参，待实机确认；
   - `RatioColor` 的三档阈值为**呈现阈值**（非游戏机制阈值），常量已具名并注明。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。需求裁决与实机验收由 PM 负责。

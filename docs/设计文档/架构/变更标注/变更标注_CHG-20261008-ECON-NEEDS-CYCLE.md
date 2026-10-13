---
change_id: CHG-20261008-ECON-NEEDS-CYCLE
state: ImplementedNotDeployedByThisBatch
scope: Pawn 检视窗「个人需求」页改造为需求循环视图（左列原版需求条 1:1 + 右栏 RUIF 需求循环）+ 单人路径目标通道修复 + 本地化/测试/验证脚本迁移
files:
  - path: Source/PersonalChronicle/Application/PawnEconomy/Perception/NeedDashboard.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPanel.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/ITab_Pawn_PersonalNeeds.cs
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml
  - path: Languages/English/Keyed/PawnEconomyNeeds.xml
  - path: Tests/PawnNeedDashboardTests.cs
  - path: Tests/TestResults/NeedsCycle-V2.trx
  - path: _scripts/verify-pawn-needs-ui.ps1
  - path: docs/changelog/v1.4.x_2026-10-08_经济AI个人需求需求循环.md
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/B1.3_实装验证.json
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/模块化实装指导手册/07-需求驾驶舱与政策反馈.md
---

# 「个人需求」页需求循环改造 变更定位

本批把 Pawn 检视窗「个人需求」页从**开发者向只读事实表**改造为**玩家向需求循环视图**，并修复单人路径缺失规划目标通道的口径断链。

**PM 裁决（本批不可再议的前提）**：① 保留原版「需求」Tab 不动，只重构我方页面；② 右栏承载经济需求驱动 AI 的**需求循环**；③ **不要**心情条与心情影响项区；④ 宽跟原版 `GetSize(pawn).x`（580），高 `min(640, 自适应)`。

**部署事实**：本批**未执行部署**；发布区 `Mods\殖民地综合管理系统\Assemblies\PersonalChronicle.dll` 仍是更早构建，**实机验收未执行**（`gameVerified=false`）。

**并发说明**：本批期间并行会话在 `Tests/` 与 `Source/PersonalChronicle/Economy/` 的其他文件持续交付（全量用例由 962 增至 1011，其中本批 +2、其余 +47 属并行会话），本批只动上表 14 个文件，未回退任何并行改动。

**验证基线**：编译 0 错 0 警（`compile_with_sdk.ps1`，`BuildStamp -> 2026-10-08 15:38 UTC`）；全量测试 **1011 通过 / 0 失败 / 1 跳过**（`NeedsCycle-V2.trx`，跳过项为既有的离线取证用例 `TempR8Measure.Measure_Content_vs_Viewport`，与本批无关）；`verify-pawn-needs-ui.ps1` **PASS**（82 双语键 / 38 用例）；编译产物与测试产物 DLL 哈希一致。

**门禁**：ARCH = PASS（条件性，四项硬触发全 No）；COMP = PASS WITH EXCEPTION（条件式，例外为"无实机组合测试"与"RUIF 1.1.2 成员级核对未执行"）；UI = **BLOCKED → 已修 → PASS**（P1：滚动内容坐标系混入绝对坐标，见下）；LOC = PASS；PERF = PASS WITH EXCEPTION（首败退避已补）；TEST = PASS WITH EXCEPTION（无实机验收，Tier B）。

| 文件 | 行号与定位 | SHA-256 |
| --- | --- | --- |
| Source/PersonalChronicle/Application/PawnEconomy/Perception/NeedDashboard.cs | `BuildSingle` 补 `targets` / `targetProvider` 两个可选形参并贯通内部 `Project(...)`（additive，零破坏） | F784EC2EB9B4F4D66762FAAC1DD41DB8CA8F912C02FA8F8F41589F7AC6A80609 |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPresenter.cs | `NeedDashboardView` 新增 additive `Planning` 分区属性与 ctor 可选参数（结构化分区，替代按显示文本匹配） | 68E324B58AE8506ADDD301E8E5418BA91F25AE6A8B4150A76292C6D3402801D2 |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPanel.cs | `NeedPanelState` 加 `Cycle` / `CycleDetailsExpanded` 会话态与复位；`Wrapped` / `Flow` 放宽 `internal static`；`DrawContent` 的 `x = body.x` → `x = 0f`（滚动内容坐标系归零） | B63CA35B89E24903381B7B716DA3C6F190D7666815D35621CE984CC39497163F |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs | `QueryNeedDashboardSingle` 接通 `targetProvider: id => QueryShadowTargets(id, map)` 并把影子仲裁行放进 `Planning` 分区 | E1858D681C2166946A84B84598E73BDF79CBC5EACBF6B87EF15A912DE52BF0D4 |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs | **新增**：`NeedsCycleMeasure`（测量缓存）+ `NeedsCyclePanel`（需求循环面板，RUIF 既有原子，零取数/零静态态/零新跨库面） | 91A53C024ABBFB83C5CFFAF1A66705684D3CFC0BB7DE18346F83281CB7FE2960 |
| Source/PersonalChronicle/Economy/PawnEconomy/ITab_Pawn_PersonalNeeds.cs | 重写：左列 `NeedsCardUtility.DoNeeds`（1:1）+ 右栏 `NeedsCyclePanel.Draw`；`ResolveTotalWidth` 含 `GetSize` 降级分支；`needs` 空值自查；`nextRetryTick` 首败退避 | 396415763CA7B1ECAE50E31BEAA86020EA1E1E20325FFC9823B85962601FB4C1 |
| Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml | 新增 4 键 `PC_PEA.Needs.Cycle.Title/Subtitle/Section.Planning/Details`（共 82 键） | 100DF6F237237AE96E1A4975622BCB598D8D161D4F4E530AE775EE6E84C7C775 |
| Languages/English/Keyed/PawnEconomyNeeds.xml | 同上（英文对等，共 82 键） | 8BF1185E2CCBA122DACD909B98D28C3D17D5D283A4B1FEA44C5B20090F814DD6 |
| Tests/PawnNeedDashboardTests.cs | `Single` helper 扩为可选 `targets`/`provider`；新增 2 用例（目标策略贯通、provider 只解析一次且结果落进视图） | 8A09B92120A11897BA0794E769C31702644F14578C2DDAF80A55603D0779B172 |
| Tests/TestResults/NeedsCycle-V2.trx | 本批全量测试结果（1011 通过 / 0 失败 / 1 跳过） | F84B600A259525F0E87BDD3DE77A840802DDE55CCCEB225A6FF97BF6FD12A077 |
| _scripts/verify-pawn-needs-ui.ps1 | 断言迁移：新面板入扫描集与指纹清单；新增「复用原版需求列」「不得触碰心情/想法链」「尺寸取自原版度量」「新面板 RUIF-only 且宿主无关」；RUIF 类型表加 `Draw`/`Tokens`；`limits` 更新为"目标已接通、仍为影子只读" | BEC78AFB4E43E3B9A0B322D9C0770AAE199A90DF2B66CFD766E63AE1D97BF6D6 |
| docs/changelog/v1.4.x_2026-10-08_经济AI个人需求需求循环.md | 新增 changelog（变更意图 / 两处断链 / 改动清单 / 验证 / 门禁 / 未做） | 0FC2C6CC326A1189B8381109FDAC156394E218A6BB7106D489A1F6EBCC308FE5 |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/B1.3_实装验证.json | 由 `verify-pawn-needs-ui.ps1` 重生成（新指纹 CHG id、调用集 `cycleCalls`、82 键、limits 更新） | 8294697BE4A0CFC66E550CEA91C8AD94BDFA06ADEC71E45DB30AE070EDD18CCF |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/模块化实装指导手册/07-需求驾驶舱与政策反馈.md | §1.1 更新两宿主形态（单人页改为需求循环）、内容层分工、口径对齐说明 | 8109DA5028976C8BC3BC9F1E87412A2CF740B860DCD99CF1FE382E7C3AC7C96D |

## 关键决策与偏离登记

1. **扩展点选型（AI-003）**：采用「复用原版 public UI（`NeedsCardUtility.DoNeeds` / `GetSize`、`Need.DrawOnGUI`）+ 既有 ITab 注入机制 + RUIF 既有原子」。被跳过并记录理由的方案：XML PatchOperation 加页签（无法覆盖全部种族、页签已存在）、DefModExtension（无配置载荷）、反射复用 private 面（有 public 面可用时禁反射）、Harmony 接管原版 Tab（PM 明确否决，风险最高档）、**RUIF 自绘需求条**（需复刻阈值刻线与加减档行为，与"1:1 跟随原版"目标冲突）。
2. **零 Harmony、零反射、零新增可变静态**：ARCH 四项硬触发全 No；新增状态全为 ITab 实例态（`nextRetryTick` / `NeedPanelState.Cycle` / `CycleDetailsExpanded`），随换 Pawn 复位。
3. **无 EXC 登记**：本批不偏离规范（ARCH Q7 逐条判定"全部不需要"）。
4. **受控修复（P1，来自 GATE-UI 首轮判定）**：`NeedsCyclePanel` 的滚动内容坐标系原先混入窗口绝对坐标（`body.x` = 241px），实机将导致右栏内容整体右移并被裁切；已改为相对坐标 `0f`，并按同源隐患把 `NeedDashboardPanel.DrawContent` 一并归零（其宿主 `Dialog_PawnEconomy.Needs.cs:36` 传入 x=0，属隐患归零而非行为变更）。修复后 GATE-UI 复验 = PASS。
5. **测试牙齿增强（P3，来自 GATE-TEST）**：`SingleViewResolvesTargetsThroughTheProviderExactlyOnce` 的 provider 改为返回非空字典并断言 `Target` 含 `%`，防止"调了 provider 却丢弃结果"的实现蒙混通过。

## 未做与边界

- **未部署、未实机验收**：实机渲染（左右分栏像素对齐、中英字形）、折叠交互、窄屏降级分辨率、与载具框架/VEF 同场组合，全部**未执行**（Tier B：有编译 + 全量单测 + 静态调用链分析，无游戏进程 profiler）。
- 右栏的窄屏降级分支（右栏 < 200px 时只画原版需求列）逻辑存在但**未经实机分辨率验证**。
- 折叠区「观测明细」保留一条 `Mood` 事实行（`PC_PEA.Needs.Mood`，文案自述"结果，非消费目标"）；GATE-UI 复验判定**不违背**裁决③（裁决客体是原版右侧的心情条与心情影响项区，本页未调用 `DoNeedsMoodAndThoughts`）。若 PM 意图为"全页零心情字样"，属二次裁决，将波及名册页，建议后续统一确认。
- 已知 P3（不阻塞）：折叠按钮无展开态视觉指示；右栏 200~260px 时 4 列折行观感一般。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-08。需求裁决、并发会话放行与实机验收由 PM 负责。

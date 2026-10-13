---
change_id: CHG-20261009-ECON-NEEDS-SINGLE-COLUMN
state: ImplementedAndDeployedByThisBatch
scope: 「个人需求」页移除原版需求列 → 整页单栏 RUIF 重排版（概览卡 + 双区需求行 + 列头）+ ITab 解除全部原版 UI 依赖
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/ITab_Pawn_PersonalNeeds.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml
  - path: Languages/English/Keyed/PawnEconomyNeeds.xml
  - path: _scripts/verify-pawn-needs-ui.ps1
  - path: Tests/TestResults/NeedsCycle-V4.trx
---

# 「个人需求」页单栏重排版 变更定位

**触发**：PM 实机裁决 2026-10-09 —— 「移除这部分原版。并重新进行排版布局」（截图框选左侧原版需求条）。该裁决**取代** `CHG-20261008-ECON-NEEDS-CYCLE` 中"保留原版需求列作为骨架 + 宽跟原版 `GetSize().x`"的设定。

**部署事实**：本批**已部署**（`deploy-to-game.ps1`，目标 DLL 时间戳 `2026-10-09 17:58:52`）。⚠ 发布目录同期有并行会话写入活动（本批前一次部署时间为 `17:51:48`），若并行会话后续再次部署，发布区将不再是本批构建。

**验证基线**：编译 0 错 0 警（`BuildStamp -> 2026-10-09 09:58 UTC`）；全量测试 **1020 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/NeedsCycle-V4.trx`）；`verify-pawn-needs-ui.ps1` **PASS**（89 双语键 / 38 用例）。

| 文件 | 定位 | 说明 |
| --- | --- | --- |
| Source/PersonalChronicle/Economy/PawnEconomy/ITab_Pawn_PersonalNeeds.cs | 移除原版 UI 调用与尺寸继承 | 删除 `NeedsCardUtility.DoNeeds` / `GetSize` / `ResolveTotalWidth`；宽改本页自有基准 580；内容改 `NeedsCyclePanel.Draw(body, …)` 整页占满；新增面板最小可用宽降级（<260 → `EmptyState`） |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.cs | 重写为单栏布局 | 新增概览卡（`InsetPanel` + `CornerFrame` + 1.5× 条，显示"生活保障"= 已规划需求平均满足度）；需求改**双区行布局**（列头 + 数据行共享列宽常量`NameWidth/PercentWidth/GapWidth`，永不漂移）；新增「仅观察（未纳入规划）」区，使原版列移除后美观/舒适/室外/药物等需求仍可见；`SectionBar` 承载两区标题 |
| Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml | +4 键（共 89） | `Label.Satisfaction` / `Cycle.Overview` / `Cycle.ObserveOnly` / `Cycle.Summary` |
| Languages/English/Keyed/PawnEconomyNeeds.xml | +4 键（共 89） | 同上 |
| _scripts/verify-pawn-needs-ui.ps1 | 断言**反转** | 原「必须复用原版需求列」三条断言 → 改为「**不得**调用任何原版需求 UI 成员（`NeedsCardUtility::` / `PawnNeedsUIUtility::`）」与「不得触碰原版需求页签」；组件断言维持卡片化原子组 |
| Tests/TestResults/NeedsCycle-V4.trx | 本批全量测试结果 | — |

## 架构影响：原版 UI 依赖归零

本批之后，`ITab_Pawn_PersonalNeeds` 与 `NeedsCyclePanel` **不再引用任何原版 UI 成员**，原版只作为数据真值来源（经只读门面）。连带效果：

- `CHG-20261008-ECON-NEEDS-CYCLE` 中 COMP 的 10 条硬约束（原版需求面调用时机、共享缓存污染、重入禁令、禁反射探测、几何纪律等）**对本页全部不再适用**；
- 原版 UI 改版（签名裁剪/行为变化）不再影响本页渲染，只影响数据面；
- `GATE-COMP` 的"原版 public 面复用证据表"对本页作废（保留在历史标注中）。

## 未做与已知问题（如实登记）

1. **实机渲染未验**：本批已部署但**未重启游戏确认**（沙箱无游戏进程）。待确认项：概览卡高度与条比例、行间距密度、列头与数据行对齐、窄屏（<520）表现。
2. **独立 Gate 已完成（2026-10-09 补做）**：`pc-ui-loc-guard`（GATE-UI/LOC，PASS WITH EXCEPTION）与 `pc-compat-auditor`（GATE-COMP，PASS WITH EXCEPTION）已出结论，见 `.codebuddy/audit/gate-records/GR-CHG-20261009-ECON-NEEDS-SINGLE-COLUMN.md`。该轮发现 **1×P1 + 1×P2 + 3×P3**（英文满足度列过窄、窄屏列重叠、长名行高、缺空态、2 个死键）**已全部修复并重验**（编译 0 错 0 警 / 全量 1028 通过 / verify PASS / DLL 哈希 `CE854951…`）。
   ⚠ 附一条工具教训：`compile_with_sdk.ps1` 的失败**不反映在管道后的 `$LASTEXITCODE`**，本批曾因此误判一次编译失败为通过；编译判定必须扫输出文本的 `error CS|warning CS`。
3. **本批未处理**：`Cycle.Unplanned` 键（第二版"未纳入规划：xxx"整行）已由本批的「仅观察」区取代，成为**有意保留的孤儿键**（项目 P4 惯例）；`CapacityBar` 的语义借用（以满足度充当占用率）**仍未加说明注释**。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。需求裁决与实机验收由 PM 负责。

---
change_id: CHG-20261008-ECON-NEEDS-TAB
state: ImplementedNotDeployedByThisBatch
scope: Pawn 检视窗「个人需求」页签 + 需求内容层抽取 + net48 重载修复 + 编译工具链 Xml 引用
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPanel.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/ITab_Pawn_PersonalNeeds.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Needs.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardTextFlow.cs
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs
  - path: Source/PersonalChronicle/Application/PawnEconomy/Perception/NeedDashboard.cs
  - path: Source/PersonalChronicle/Archive/ChronicleInspectTabInjector.cs
  - path: Source/PersonalChronicle/ChronicleMod.cs
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml
  - path: Languages/English/Keyed/PawnEconomyNeeds.xml
  - path: Tests/PawnNeedDashboardTests.cs
  - path: _scripts/verify-pawn-needs-ui.ps1
  - path: _scripts/compile_with_sdk.ps1
  - path: _scripts/compile.rsp
  - path: docs/changelog/v1.4.x_2026-10-08_经济AI个人需求Pawn页签.md
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/模块化实装指导手册/07-需求驾驶舱与政策反馈.md
  - path: docs/设计文档/架构/工程约定/开发手册.md
  - path: docs/开发参考/项目整理与功能清单.md
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/B1.3_实装验证.json
  - path: Tests/TestResults/NeedsTab-Full.trx
---

# Pawn「个人需求」页签 变更定位

本批把经济「需求观察」抽为**宿主无关内容层**（`NeedDashboardPanel`，名册/单人双模式），新增 Pawn 检视窗页签「个人需求」，并把页签注入改为**组合根传参**（`InjectAll(类型集合)`，消除 Archive↔Economy 循环）。

**部署事实（勿据本批声明推断发布区状态）**：本批**自身未执行部署**；但 2026-10-08 20:25:46 并行会话 `CHG-ECON-B3-SHADOW` 的部署脚本已把包含本变更的程序集推入发布区 `Mods\殖民地综合管理系统\Assemblies\`（备份 `economy-b3-deploy_20261008_202546_849` 保存了被覆盖的 B2.4.2 构建 1709056B@18:14）。⇒ 发布区现状 = B2.4.2 + 本变更 + 影子计划；**实机验收仍未执行**（gameVerified=false）。

并发说明：`EconomyNeedDashboard.cs` 与 `NeedDashboard.cs` 同时含并行会话的「影子计划」改动（`targetProvider` 通道）。本批只做**最小缝合**（`Project` 补参数并贯通 `Build`/`BuildSingle`），未回退对方实现；对方独占的 Shadow 相关文件不在本批范围。

验证基线：编译 0 错 0 警（`compile_with_sdk.ps1`）；Cecil 核实 `ITab_Pawn_PersonalNeeds` 存在于程序集且基类为 `RimWorld.ITab`；全量测试 962 通过 / 0 失败 / 1 跳过（`NeedsTab-Full.trx`）；`verify-pawn-needs-ui.ps1` PASS（78 双语键 / 36 用例）；`gate-runner.ps1 -Base HEAD -Blocking` = **PASS（标注覆盖 20 个改动文件，无遗漏/超范围/残留）**。

| 文件 | 行号与定位 | SHA-256 |
| --- | --- | --- |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardPanel.cs | 新增：`NeedPanelMode` / `NeedPanelState`（会话态）/ `NeedDashboardPanel.Draw`（双模式）/ `Measure`（测量缓存） | D20B7028707C6BCF91CB16B282AD9DC88C576D96A647ACF4448779432EA08F9E |
| Source/PersonalChronicle/Economy/PawnEconomy/ITab_Pawn_PersonalNeeds.cs | 新增：页签壳（labelKey=PC_PEA.Needs.TabTitle）/ `IsVisible` / `FillTab`（60 tick 门控） | F7668E5B1A9902D806809ABB8CD177E1E095B4FC04060BE80D607AD2F959E749 |
| Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Needs.cs | 重写为宿主适配（193→39 行）：保留 `DrawNeeds`/`InvalidateNeeds`/`Flow`，委托 `NeedDashboardPanel.Draw` | 25131DC8335BCB785AC0BADA4D8C39EE9BD760D81D371DD94DDDCDD0C2F36666 |
| Source/PersonalChronicle/Economy/PawnEconomy/NeedDashboardTextFlow.cs | `Split(char[],StringSplitOptions)` 与 `TrimEnd((char[])null)` 显式化 + 兼容性说明（net48 MissingMethod 修复） | C4543905C25242854E00A0E22F5D77F4E4940B124DA6EB51A6B88E4FAC6FB8C8 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs | 新增 `QueryNeedDashboardSingle`（单人只读门面）；同文件含并发会话 `targetProvider` 接线 | 7F78BD49DA579A213F6E489C75FAB25034BC1CD0D95637ACAE646EB63C8289CF |
| Source/PersonalChronicle/Application/PawnEconomy/Perception/NeedDashboard.cs | 新增 `BuildSingle` + 抽出 `Project`（含并发会话 `targetProvider` 参数贯通） | A382B431F57979BB18E0E532A131BFCE0DFC4BCC6CC38306CEB96BBCB92A0B30 |
| Source/PersonalChronicle/Archive/ChronicleInspectTabInjector.cs | `InjectAll(IReadOnlyList<Type>)` 泛化；插入位注释按实机逆序渲染实证更正 | 125775ADC22C992D8C55866113282937F9E6F9FC497C614F8EB45404C9F000D0 |
| Source/PersonalChronicle/ChronicleMod.cs | 组合根传入 [档案, 职业档案, 个人需求]，含顺序敏感说明 | D688B145C215A3814868C2B655EDDF5738F509D50058B2B3C252075D9CA1B738 |
| Languages/ChineseSimplified/Keyed/PawnEconomyNeeds.xml | 新增 `PC_PEA.Needs.TabTitle` = 个人需求 | EF04153E14FDA5A7E3AF37FF01EC22681D679938C399305AA39CA40726DC950A |
| Languages/English/Keyed/PawnEconomyNeeds.xml | 新增 `PC_PEA.Needs.TabTitle` = Personal Needs | 5B7378350B9F630C63F69139D1E107E31383D56732CD0A1E1DD10FE73BA9EB50 |
| Tests/PawnNeedDashboardTests.cs | 新增 4 个 `BuildSingle` 用例：读放大=1 / 与名册路径对拍 / epoch≤0 零请求 / 跨图去重统计 | AB4F3F2683FDEBD9DB7C2829E917E40115B52D333512464A677B873A3F6B2506 |
| _scripts/verify-pawn-needs-ui.ps1 | 断言迁移：RUIF 原子与 Wrap 改扫面板、窗口侧加「委托面板」、新增 ITab/组合根/raw key/面板宿主无关断言；报告字段补 windowCalls/tabCalls | 1A4A31BE6A621BE769F16D3295A8903D45E9789E09EF331BF603AC1861748089 |
| _scripts/compile_with_sdk.ps1 | 补 `System.Xml.dll` / `System.Xml.Linq.dll` 引用（缺引用时既有 Data 文件报成片 CS0246/CS1069） | 7F0CFB9999F8AE79B7A54C29E475C5A081C3DC2EDC55C3DB954E6D9916573C64 |
| docs/changelog/v1.4.x_2026-10-08_经济AI个人需求Pawn页签.md | 新增 changelog（含 AI 署名与部署事实更正） | 176B06A391FE28437A3D46EF8C56A4BD6953A12A253BEC7D8239B7680187BAEA |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/模块化实装指导手册/07-需求驾驶舱与政策反馈.md | 状态行更新 + 新增 §1.1「已落地的宿主与扩展点」 | ED26CB81F8214E1769E8D492D9474B57D9E2450E30E984E063C079F7FD6DD6B1 |
| docs/设计文档/架构/工程约定/开发手册.md | ITab 注入范式更新（组合根传参 + 顺序语义 + 破环理由） | BB01F42946F49C105BAE14DD2292C566B1482C64B6007F3E58B82123BD871BCC |
| docs/开发参考/项目整理与功能清单.md | 检视页签 2 → 3（档案 / 职业档案 / 个人需求） | C739BB435D9FC0374421040248E087877B13284D2925E7C3D9208BC037AE6F8D |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/B1.3_实装验证.json | 由 `verify-pawn-needs-ui.ps1` 重生成（新指纹、CHG id 与调用集字段） | 667EAFEBB8AB7986C18805652FF074F9EB8EF2BD6531EA81E0D845E94EEFFC76 |
| Tests/TestResults/NeedsTab-Full.trx | 本批全量测试结果（962 通过 / 0 失败 / 1 跳过） | FFB1508DF79E499F952A583AB03A50D574F695867F0E2B95AF13601EF1837DFD |

## 未做与边界

- 本批未自行部署；发布区已由并行会话 `CHG-ECON-B3-SHADOW` 的部署覆盖（20:25:46），内含本变更与影子计划代码 —— 回退前请先确认该会话的交付意图。
- 未做实机截图/字形验收；页签视觉顺序（档案｜职业档案｜个人需求｜健康）由 IL 与实机逆序渲染规则推导，待人工确认。
- 单人视图未并入「影子计划」明细（经济窗口名册路径已并入），口径对齐待影子计划定稿。
- 独立复核（pc-perf-tester）：1–5 项 PASS；第 6 项指出「未部署」声明与发布区事实不符，已在上文更正。

---

AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-08。需求裁决、并发会话放行与实机验收由 PM 负责。

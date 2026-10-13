---
change_id: CHG-20261008-ECON-DELIVERY
state: CodeVerified
scope: B2.4.2
release_gate: blocked_pending_game_acceptance_and_global_gate
---

# B2.4.2 变更定位

仅列本批文件；工作区既有其他变更未重置、未合并进本批。全局 gate-runner 不存在，未发布。编译后/测试证据见 B2.4.2_实装验证.json。

| 文件 | 定位 | 作用 |
| --- | --- | --- |
| Source/PersonalChronicle/Domain/PawnEconomy/Finance/TreasuryDelivery.cs | 5: internal enum TreasuryDeliveryPhase { Waiting, Carrying, Completed, Cancelled, Faulted }<br>6: internal sealed class TreasuryDelivery | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Source/PersonalChronicle/Application/PawnEconomy/Finance/LegacyCutoverPlan.cs | 9: internal enum LegacyDisposition { PreserveHistory, CancelOrderReleaseReservation, EndContractPreserveWage, RetainOwnership }<br>10: internal sealed class LegacyCutoverItem<br>21: internal sealed class LegacyCutoverCash | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Source/PersonalChronicle/Data/PawnEconomy/TreasuryOperationsSaveData.cs | 14: internal sealed class TreasuryOperationsSaveData : IExposable | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyTreasuryOperations.cs | 17: public static class TreasuryJobDefOf<br>22: public sealed partial class GameComponent_PawnEconomy<br>292: public sealed class JobDriver_TreasuryDelivery : JobDriver | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyTreasuryPresenter.cs | 9: public sealed partial class GameComponent_PawnEconomy | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Source/PersonalChronicle/Economy/PawnEconomy/PawnSettlementSaveHost.cs | 14: internal sealed class PawnSettlementSaveHost : IExposable | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs | 14: public sealed partial class GameComponent_PawnEconomy : GameComponent | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionMode.cs | 12: public sealed partial class GameComponent_PawnEconomy<br>163: internal enum InstitutionReverifyCode { Cleared, NotFaulted, StillFaulted, Unavailable } | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs | 17: public sealed partial class GameComponent_PawnEconomy | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs | 26: public sealed partial class Window_InstitutionWorkspace : Window | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Defs/PawnEconomy_Treasury.xml | 1: <?xml version="1.0" encoding="utf-8"?><br>2: <Defs><br>3: <JobDef> | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Languages/ChineseSimplified/Keyed/TreasuryOperations.xml | 1: <?xml version="1.0" encoding="utf-8"?><br>2: <LanguageData><br>3: <PC_PEA.Treasury.Title>公有国库准备与旧权益承接</PC_PEA.Treasury.Title> | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Languages/English/Keyed/TreasuryOperations.xml | 1: <?xml version="1.0" encoding="utf-8"?><br>2: <LanguageData><br>3: <PC_PEA.Treasury.Title>Public treasury preparation and legacy settlement</PC_PEA.Treasury.Title> | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Languages/ChineseSimplified/Keyed/EconomyMode.xml | 1: <?xml version="1.0" encoding="utf-8"?><br>2: <LanguageData><br>3: <PersonalChronicle.Settings.Tab.Economy>经济</PersonalChronicle.Settings.Tab.Economy> | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Languages/English/Keyed/EconomyMode.xml | 1: <?xml version="1.0" encoding="utf-8"?><br>2: <LanguageData><br>3: <PersonalChronicle.Settings.Tab.Economy>Economy</PersonalChronicle.Settings.Tab.Economy> | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Languages/ChineseSimplified/DefInjected/JobDef/PawnEconomy_Treasury.xml | 1: <?xml version="1.0" encoding="utf-8"?><br>2: <LanguageData><PC_PEA_TreasuryDelivery.reportString>在公有结算处办理银币交付。</PC_PEA_TreasuryDelivery.reportString></LanguageData> | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Languages/English/DefInjected/JobDef/PawnEconomy_Treasury.xml | 1: <?xml version="1.0" encoding="utf-8"?><br>2: <LanguageData><PC_PEA_TreasuryDelivery.reportString>Delivering silver at a public settlement counter.</PC_PEA_TreasuryDelivery.reportString></LanguageData> | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Tests/PawnTreasuryOperationsTests.cs | 18: public sealed partial class PawnSilverReserveTests | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| Tests/PawnInstitutionModeTests.cs | 17: public sealed partial class PawnSilverReserveTests<br>254: public sealed class PawnInstitutionModeRulesTests | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| _scripts/verify-pawn-treasury-operations.ps1 | 1: #requires -Version 7.0<br>2: param([string]$GameRoot = 'E:\SteamLibrary\steamapps\common\RimWorld')<br>3: $ErrorActionPreference = 'Stop' | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/08-B2.4.2_真实交付与权益承接_实装记录.md | 5: ## 1. Intake / Architecture Review<br>17: ## 2. 验证计划<br>21: ## 3. 实装结果 | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/实施批次状态.json | 1: {<br>2: "changeId": "CHG-20261008-ECON-LANDING",<br>3: "lastChangeId": "CHG-20261009-ECON-A6-ZONEWAGE", | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/00-实装落地计划_v1.0.md | 7: ## 1. 目标、范围与交付纪律<br>15: ## 2. 当前代码基线及迁移差异<br>34: ## 3. 架构决策与数据边界 | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/模块化实装指导手册/00-总册与实施路线.md | 9: ## 1. 本手册回答什么<br>18: ## 2. 阅读与交付索引<br>57: ## 3. 当前核验基线 | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| docs/设计文档/功能模块/经济体系/经济需求驱动AI/游戏代码设计/11-资金资源权利与制度协调_代码设计方案.md | 5: ## 1. 输入输出与职责<br>11: ## 2. 共同数据与不变量<br>27: ## 3. 银币缴存、提取与外贸 | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |
| docs/changelog/v1.4.x_2026-10-08_经济AI真实交付与权益承接.md | 1: # 经济 AI：真实交付与权益承接<br>3: Change ID：CHG-20261008-ECON-DELIVERY，2026-10-08。B2.4.2 CodeVerified；未部署、未实机验收。<br>5: - 公有结算处接入真实银币缴存/提取。RUIF 账户页提交准备指令，殖民者在安全空闲窗口自主搬运；不强制打断原版行为。提取先锁可用国库，实际双端数量/所属证明后才记账。 | 本批实现/保存/接线或验证；具体内容见对应符号与实装记录 |

生成文件另含本变更标注、B2.4.2_实装验证.json、B2.4.2_UI复验.json、B242 TRX 及工作区编译 DLL/BuildStamp；历史 B2.3/B2.4.1 报告未重写。

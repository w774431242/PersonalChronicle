---
change-id: CHG-20261011-LABOR-VISUAL
date: 2026-10-12
author: CodeBuddy / DeepSeek-V4.1-Flash
type: 实装修复（CodeVerified / TestVerified / DeployedFilesSynced；GameVerified NOT RUN）
scope: CB-ECON-003 地图劳务链实装：办理意图→到站复验签约→实体合同纸(代次/补发)→真实产物捕获与交验→统一付款资格门；含存档节点、Def、双语、贴图与自动化验收
base: HEAD
task-id: CB-ECON-003
files:
  # ── Domain（纯类型与规则；零 Verse/Unity/RUIF）──
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Employment/LaborFlow.cs
    type: A
    symbols: SettlementDeliveryMode / LaborFlowStage / FlowHold / DeliveryCapability / WageEligibility / LaborWorkflow / SubmissionRecord / ProductBatch / LaborFlowRules
    note: 付款模式与流程阶段正交建模；资格在应付创建时冻结；批次数量守恒值对象；PathTo 逐阶补记路径；存储解析 fail-closed
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Employment/EmploymentContracts.cs
    type: M
    symbols: WorkContract.DeliveryMode/DocumentGeneration/DocumentInstanceId / WithDocument / OccupiedBy(deliveryMode) / WageObligation.Eligibility/SubmissionId/EligibleTick / Submitted / Blocked / EmploymentCode.SubmissionRequired/EligibilityBlocked/AlreadySubmitted/DocumentConflict
    note: 三重建点全传递模式与代次；未知模式允许构造但阻断金融动作（CanCommitNewWork）
  # ── Data（版本化载体；旧档缺键=自然默认）──
  - path: Source/PersonalChronicle/Data/PawnEconomy/LaborFlowSaveData.cs
    type: A
    symbols: pawnEconomicLaborFlow / schemaVersion / PayloadIntact / WorkflowRow / SubmissionRow / BatchRow / WorkflowIds / RemoveIntentWorkflow
    note: 独立版本化节点；未知版本保留载荷并阻断全部写入；未归档流程/未核销批次优先保留；提交行作为幂等回执刻意不裁
  - path: Source/PersonalChronicle/Data/PawnEconomy/EmploymentSaveData.cs
    type: M
    symbols: ContractRow.DeliveryMode/DocumentGeneration/DocumentInstanceId / ObligationRow.Eligibility/SubmissionId/EligibleTick / ParseDeliveryMode / ParseEligibility / SumUnpaidEarnedMinor / SumOutstandingCommitments / TrimObligationsOnLoad / TrimContractsOnLoad / HasUnpaidObligation / ReadObligationsForContract
    note: 承诺口径并入"已完成未付负债"；载入超限裁剪改为优先保留未付负债（修旧实现整段截断吞欠薪）
  # ── Application（命令与结果；唯一写入点）──
  - path: Source/PersonalChronicle/Application/PawnEconomy/Employment/LaborFlowService.cs
    type: A
    symbols: LaborFlowCode / LaborFlowResult / BeginVisit / AttachContract / MoveTo / HoldAt / Resume / EnsureDocument / RegisterBatch / AcceptSubmission / PaymentGrant / WriteOffBatch / WriteBatch / Authoritative
    note: 阶段推进/代次/批次/提交的唯一写入点；Authoritative 以存档行为权威（防陈旧实例静默回退阶段）；确定性 ID（禁 Guid）
  - path: Source/PersonalChronicle/Application/PawnEconomy/Employment/EmploymentService.cs
    type: M
    symbols: SignRequest.DeliveryMode / Sign(mode 条款比对) / TryOccupy(deliveryMode) / GrantSubmission / BlockCredential / ApplyDocument / UnpaidEarnedFor / ReadObligationsForContract / AcceptEvidence(资格冻结)
    note: 资格在应付创建时按冻结模式写入（旧档缺键=Immediate 保持既有付款权利）
  # ── Economy（原版对象适配；Job/Thing/holder）──
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborFlow.cs
    type: A
    symbols: TryBeginLaborIntent / TryRevokeLaborIntent / TrimLaborIntents / PlanLaborVisit / PlanContractReissue / PlanContractSubmission / LaborArrivedAtOffice / LaborSignCommit / LaborIssueDocument / LaborSubmit / SubmitOne / AdvanceStage / NotePaymentConfirmed / ArchiveSettledFlows / MayPayObligation / ExposeLaborFlow
    note: 意图不再远程签约；到站复验复用唯一原子签约实现；实体纸签发/补发；交验三支（实物/入库凭据/无产品）；付款门唯一判据
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborOutputs.cs
    type: A
    symbols: GeneratedOutput / NoteGeneratedThing / RegisterEvidenceOutputs / HasAllProductsFor / CountCarried / TrimGeneratedOutputs
    note: 真实产物实例两段式捕获（落地记录→证据成立时绑定）；交付前置按 Def 核对实际件数
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Thing_WorkContractDocument.cs
    type: A
    symbols: Thing_WorkContractDocument / ContractId / DocumentInstanceId / Generation / AuthorizedHolderKey / IsStructurallyValid / IsValidFor / Authorizes / GetInspectString / GetGizmos
    note: 只存身份（条款/进度/金额一律查权威快照）；纸≠债权；旧代次纸明确标注作废且无领取权利
  - path: Source/PersonalChronicle/Economy/PawnEconomy/LaborVisualText.cs
    type: A
    symbols: StageLabel / HoldLabel / ReasonLabel / NextActionLabel / CapabilityLabel / ModeLabel / EligibilityLabel
    note: 稳定 ID→玩家文本的唯一映射点（整键回落）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorld.cs
    type: M
    symbols: Building_EconomyInstitution.delivery / DeliveryContainer / DeliveryCapacity / AcceptDelivery / DeliveryRoom / JobDriver_LaborVisit / JobDriver_LaborSubmit
    note: 独立交付接收 holder（隔离于衣物库存，DeSpawn 同落地）；两个办理 JobDriver（洽谈→签约→签领 / 核验→提交）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs
    type: M
    symbols: Plan(新账本分支) / ExposeLaborFlow 挂点 / TrimLaborIntents / TrimGeneratedOutputs / ArchiveSettledFlows
    note: 新 planner 先于既有四类交付且各自自门；周期维护槽接入
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
    type: M
    symbols: OnPlanPublished(意图) / TrySettleAfterEvidence / TrySettlePayable(资格门) / RetryOutstandingPayables(资格门) / RecordSettlementOutcome(信用时点)
    note: 三个完成入口统一出口；付款门唯一判据；补付只对已交验核验者（旧模式链逐字不变）；欠薪延迟从可付款起算
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkOrders.cs
    type: M
    symbols: ReportVanillaOrderCompletion(TrySettleAfterEvidence)
    note: 第三个完成入口同规则（漏此即为订单完成提前付款漏口）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs
    type: M
    symbols: TryDraftVanillaWorkContract(DeliveryMode) / TrySignVanillaWork(DeliveryMode) / TryOccupyPostedContract(原子写模式) / ContractDetailText(阶段/模式/下一步)
    note: 起草与签约均冻结 PhysicalSubmission（修复 GATE-ARCH P0-1：新模式从未写入导致实机不可达）；承接与模式同一项写入；合同表展开显示真实阶段
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedSpeech.cs
    type: M
    symbols: PendingLaborSpeech / PostLaborSpeech(入队) / FlushLaborSpeech / TickNeedSpeech / ResetNeedSpeech
    note: 业务只入队、渲染上下文才翻译与发泡（表现与业务彻底解耦；修测试宿主 ECall 与无语言环境翻译泄漏）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs
    type: M
    symbols: BuildModel(laborWorkflow 采样)
    note: 个人账户/决策页接入劳动履约快照（只读）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.AccountModel.cs
    type: M
    symbols: BuildModel(workflow) / AppendLaborRows / RowLabel
    note: 劳动履约行=真实阶段+合同短号+付款模式+阻断原因+下一步（走既有 Details 渲染通道，零新增绘制代码）
  # ── Capture（真实产物实例捕获）──
  - path: Source/PersonalChronicle/Capture/Patch_GenPlaceOutput.cs
    type: A
    symbols: Patch_GenPlaceOutput / TargetMethod / TargetMethodExists / Prefix / WrapPlacedAction
    note: 挂原版物品落地生成点（只读 Postfix）；档案齐（Target/Type/Reason/Alternative/Compat/Rollback）；已登记 EXC-2026-001 #26
  # ── Def / 语言 / 资产 ──
  - path: Defs/PawnEconomy_WorkContract.xml
    type: A
    symbols: DefName:PC_PEA_WorkContractDocument / JobDef:PC_PEA_VisitLaborOffice / JobDef:PC_PEA_SubmitWorkContract
    note: 合同纸 stackLimit=1、tradeability=None、MarketValue=0（不作工资代币）；两个办理 JobDef
  - path: Languages/ChineseSimplified/DefInjected/JobDef/PawnEconomy_LaborFlow.xml
    type: A
    symbols: Key:PC_PEA_VisitLaborOffice.reportString / PC_PEA_SubmitWorkContract.reportString
    note: 中文 JobDef 报告串
  - path: Languages/English/DefInjected/JobDef/PawnEconomy_LaborFlow.xml
    type: A
    symbols: Key:PC_PEA_VisitLaborOffice.reportString / PC_PEA_SubmitWorkContract.reportString
    note: 英文 JobDef 报告串
  - path: Languages/ChineseSimplified/DefInjected/ThingDef/PawnEconomy_WorkContract.xml
    type: A
    symbols: Key:PC_PEA_WorkContractDocument.label / .description
    note: 中文 ThingDef 标签与描述
  - path: Languages/English/DefInjected/ThingDef/PawnEconomy_WorkContract.xml
    type: A
    symbols: Key:PC_PEA_WorkContractDocument.label / .description
    note: 英文 ThingDef 标签与描述
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
    symbols: Key:PC_PEA.LaborVisual.*（Stage×13 / Hold×4 / Reason×21 / Next×9 / Capability×4 / Mode×3 / Eligibility×5 / Doc×21 / Speech×5 / Row×4）
    note: 中文劳动履约键族（阶段/叠加/原因/能力/模式/资格/检查/气泡/行标签）
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
    symbols: Key:PC_PEA.LaborVisual.*（与中文逐键同构）
    note: 英文劳动履约键族
  - path: Textures/Things/Item/PawnEconomy/PC_PEA_WorkContract.png
    type: A
    symbols: AssetID:PC_PEA_WorkContract
    note: 合同纸贴图 128×128 RGBA 透明背景、无烧录文字（程序化绘制，本仓自绘无外部素材）
  # ── 工具 / 测试 ──
  - path: Tools/make_labor_contract_icon.py
    type: A
    symbols: make_labor_contract_icon
    note: 合同纸贴图的可复现生成脚本（Pillow）
  - path: Tests/LaborFlowTests.cs
    type: A
    symbols: LaborFlowRulesTests / LaborFlowServiceTests
    note: 23 项：fail-closed 解析、付款门四态、阶段不可跳与逐阶补记、批次守恒、确定性 ID、能力矩阵、服务幂等与绑定
  - path: Tests/EmploymentLaborGateTests.cs
    type: A
    symbols: EmploymentLaborGateTests
    note: 13 项：旧档缺键语义、资格冻结与授予/阻断、承诺→负债守恒（T31）、三重建点、模式作为条款、代次单调、承接原子写模式
  - path: Tests/LaborFlowScribeTests.cs
    type: A
    symbols: LaborFlowScribeTests
    note: 4 项真 Scribe 往返：流程/提交/批次守恒、旧档空载体、未知版本阻断与载荷保留、行级不可识别值
  # ── 文档（交付物与台账）──
  - path: docs/设计文档/架构/EXC-2026-001.md
    type: M
    symbols: Patch #26 Patch_GenPlaceOutput
    note: 补登本轮新增 Harmony 补丁（GATE-ARCH P1-4）；含理由/替代方案否决/落地纪律/回滚
  - path: docs/changelog/v1.4.x_2026-10-12_劳务履约可视化与实体合同.md
    type: A
    symbols: 用户向 Changelog（Change ID CHG-20261011-LABOR-VISUAL）
    note: 按工程既有 docs/changelog/vX.Y.Z_日期_主题.md 范式；写玩家可感知影响与未验证项，不含行号（REL-004）
  - path: docs/任务派发/CodeBuddyCN/CB-ECON-003_劳务履约过程可视化与实体合同_需求单.md
    type: M
    symbols: front matter 状态 + 实装注记
    note: execution/implementation 状态更新为已实装；追加实装注记（含 GATE 复核修复清单），设计正文原文保留留痕（status 仍为 new，符合 DOC-005 白名单）
  - path: docs/README.md
    type: M
    symbols: 经济需求驱动 AI 主题 CB-ECON-003 索引条目
    note: 由「Prepared，仅设计，未派发实施」更新为已实装，并互链变更标注与 Changelog
  # ── 回执（经理与执行器协作规范 L19–21 的回报协议）──
  - path: docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_执行回报.md
    type: A
    symbols: DOC-REPORT-20261012-CB-ECON-003
    note: 执行回报主文档；状态 ReadyForReview；含实际落地对照、金融边界、验证、Gate 修复、NOT RUN 清单与回滚
  - path: docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_ACK.json
    type: A
    symbols: taskId/changeId/status/taskSha256/revisions
    note: 标准接收回执；本轮由执行器在收到真实任务文本后开工前确认接收、交付时落盘（如实注明未经 CLI 派发链）；不补造设计轮 ACK
  - path: docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_RESULT.json
    type: A
    symbols: binary/tests/negativeControls/gates/roundClosure/changedFiles/invariants/notRun
    note: 机器可读交付结果；与执行回报 §6/§7 同源口径
  - path: docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_经理审计回执_20261012.md
    type: A
    symbols: DOC-MANAGER-RECEIPT-20261012-CB-ECON-003 / CA-01至CA-18 / 返修接收与验收格式
    note: 用户要求向CodeBuddy CN递交经理回执；沿用原任务和Change ID，完整返修优先级/范围/host与恢复验收，不表示执行器已接收或部署已获批准
  # ── 返修 R2（2026-10-12：实机"接合同/提交合同缺失寻路"）──
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Employment/LaborFlow.cs
    type: M
    symbols: LaborWorkflow.IdFor(round)/RoundOf/MaximumRounds / SubmissionRecord.IdFor(coveredTotal) / LaborFlowRules.CapabilityFor（Hunt/Slaughter/Haul → Unsupported）
    note: 轮次后缀即权威；多趟回执键含累计接收量；无权威回执来源的族如实降级 Unsupported
  - path: Source/PersonalChronicle/Data/PawnEconomy/LaborFlowSaveData.cs
    type: M
    symbols: CurrentSchemaVersion=2 / schema 升级 / ReadWorkflowFor(最高轮) / TrimOnLoad(归档按轮降序) / CanAcceptSubmission / SumAcceptedForContract
    note: v1→v2 加性升级；当前轮解析 fail-closed；提交表容量前置预检
  - path: Source/PersonalChronicle/Application/PawnEconomy/Employment/LaborFlowService.cs
    type: M
    symbols: BeginVisit(轮次) / AcceptSubmission(全量预检 + 累计键) / IsDeliveryCovered / PaymentGrant(注记)
    note: CA-04 消除孤立核销；覆盖完成判据落服务层
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborFlow.cs
    type: M
    symbols: PlanContractCollectOutputs / FindBatchThing / HolderKeyOf / TryPendingSubmission / CarriedForEvidence / RemainingForEvidence / SubmitOne(先物理后回执) / TransferProductsToStation(实测件数) / MatchesBatchInstance / InstanceOnMap / CountInContainer / HoldsLiveDocument(持有器链) / LaborSignCommit(到站复验) / LaborIssueDocument(Def 缺失暂停) / TryRevokeLaborIntent(当前轮)
    note: CA-07 收集 Job 规划 + CA-01/02/04/06/11/18 修复
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborOutputs.cs
    type: M
    symbols: NoteGeneratedThing(合堆回落 + 去重窗口) / ResolvePlacedStack / DuplicateNoteWindowTicks / RegisterEvidenceOutputs(失败不丢笔记) / HasAllProductsFor（删除：无调用方）
    note: CA-08 最小修复：真实实例、事件去重、登记失败可重试
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorld.cs
    type: M
    symbols: JobDriver_LaborCollectOutputs / Building_EconomyInstitution.GetChildHolders(DeliveryHolderAdapter) / DeliveryHolder
    note: 新增收集 JobDriver；CA-17 接收区接入原版持有器递归链
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs
    type: M
    symbols: Plan(新账本分支顺序：Visit → Reissue → CollectOutputs → Submission)
    note: 收集环节接进规划链（缺它则永远走不到柜台）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs
    type: M
    symbols: ContractDetailText 的 LaborVisual.Contract.Stage/Next 键（去掉重复前缀）
    note: CA-15：修前查 PC_PEA.PC_PEA.* ⇒ 合同详情显示裸键
  - path: Defs/PawnEconomy_WorkContract.xml
    type: M
    symbols: JobDef:PC_PEA_CollectContractOutput
    note: 收集 Job 的 Def（driverClass 指向新增 JobDriver）
  - path: Languages/ChineseSimplified/DefInjected/JobDef/PawnEconomy_LaborFlow.xml
    type: M
    symbols: Key:PC_PEA_CollectContractOutput.reportString
    note: 新增 JobDef 的中文报告串（DefInjected 与 Def 同名文件）
  - path: Languages/English/DefInjected/JobDef/PawnEconomy_LaborFlow.xml
    type: M
    symbols: Key:PC_PEA_CollectContractOutput.reportString
    note: 新增 JobDef 的英文报告串
  - path: Tests/LaborFlowTests.cs
    type: M
    symbols: AcceptSubmissionIsKeyedByCumulativeReceivedAndConservesQuantity / ArchivedRoundOpensANewRoundInsteadOfReusingTheDeadRow / RoundSuffixIsAuthoritativeAndFailClosed / CapabilityMatrixIsExplicitAndFailClosed（Hunt/Slaughter/Haul）
    note: 多趟守恒与零写入、续轮身份、轮号 fail-closed、能力矩阵降级
  - path: Tests/LaborFlowScribeTests.cs
    type: M
    symbols: RoundTripPreservesWorkflowSubmissionAndBatch（累计键 + 未覆盖完成）
    note: 读档往返后回执键与覆盖判据同源
  - path: docs/changelog/v1.4.x_2026-10-12_劳务履约可视化与实体合同.md
    type: M
    symbols: 返修与部署段 / 返修 R3 段
    note: 用户向变更记录（REL-004：不含行号）
  # ── 返修 R3（2026-10-12：按经理审计回执 §3 逐项补全）──
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
    symbols: PC_PEA.LaborVisual.Next 新增 6 键 / Reason 新增 22 键 / Reconcile.Label·Desc·Title·Resume·WriteOff
    note: CA-15：补齐运行期实际使用的动作/阻断/原因与对账按钮键（中）
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
    symbols: 同上（英）
    note: CA-15：与中文同键同数（缺键回落不再把稳定 ID 暴露给玩家）
  # ── 返修 R4（2026-10-12：任务链台账，用户已批准）──
  - path: docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_PROGRESS.json
    type: A
    symbols: taskId/changeId/status/completedCA/partialCA/remainingCA/remainingSteps(B1–B7)/tests/deployed/modifiedFiles/回执与需求 SHA
    note: 回执 §6 指定的进度台账（CB-ECON-002 同名先例）；记录已完成/部分关闭/剩余 CA、阻塞、部署与测试证据
  - path: Defs/PawnEconomy_Institutions.xml
    type: M
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Perception/LaborSpeechQueue.cs
    type: A
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborCarryPresentation.cs
    type: A
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborCounterPresentation.cs
    type: A
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborInteraction.cs
    type: A
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborPresentation.cs
    type: A
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborTransport.cs
    type: A
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs
    type: M
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedAccountModelView.cs
    type: M
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Account.cs
    type: M
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.Table.cs
    type: M
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Window_LaborRecord.cs
    type: A
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Tests/LaborFlowVisualCycleTests.cs
    type: A
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Tests/LaborPaymentFeedbackTests.cs
    type: A
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Tests/LaborPhysicalTransferTests.cs
    type: A
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Tests/LaborPresentationTests.cs
    type: A
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Tests/LaborSpeechQueueTests.cs
    type: A
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Tests/LaborVisualBoundedWorkloadTests.cs
    type: A
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: _scripts/verify-pawn-economy.ps1
    type: M
    symbols: CB-ECON-003 visual repair / see source-diff.patch
    note: §15实际修复；SHA与逐行差异见final-manifest.json和source-diff.patch
  - path: Tests/LaborOutputCaptureTests.cs
    type: A
    symbols: LaborOutputCaptureTests
    note: 本机GenPlace签名及增量、证据、载体、异常隔离测试；不冒充真实Pawn拾取
  - path: Tests/LaborFulfillmentSchedulingTests.cs
    type: A
    symbols: LaborFulfillmentSchedulingTests
    note: 既有履约Work/Anything安全门和优先级边界测试；实机待验

---

# CHG-20261011-LABOR-VISUAL：劳务履约过程可视化与实体合同（实装）

> **2026-10-12 经理审计：ChangesRequired / 验收 BLOCKED。** 本文保留执行器实装与设计历史；其中 PASS/已闭合声明为历史回报，不代表经理接受。当前结论见 `docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_执行回报.md` §12 与 `CB-ECON-003_RESULT.json.managerReview`。本次只修改本标注、原执行回报、RESULT、`docs/README.md`（均已在上方 files 中声明），另留证据区 Gate/临时探针；未修改产品源码/DLL、需求正文，未部署。

经理实际复跑当前 Release：1568 通过 / 0 失败 / 1 跳过；独立源码审查与 DLL/Scribe/最小 holder 宿主探针发现 18 项需修复问题（3 P0、14 P1、1 P2）。ARCH/DATA/SAVE/COMP/UI/LOC/TEST 均 BLOCKED，PERF/Game/REL NOT RUN。冻结清单 `.codebuddy/tmp/cb-econ-003-manager-audit/audit-evidence.json`；经理 Gate `.codebuddy/audit/gate-records/GR-CHG-20261011-LABOR-VISUAL-MANAGER-AUDIT.md`。不重写执行器统计、不刷新治理基线，返修沿用原 Task/Change ID。

用户后续要求向 CodeBuddy CN 递交经理回执，新增 `docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_经理审计回执_20261012.md`，完整列明18项返修、范围、真实验收与回复格式，已登记 docs/README；本次文件传递不表示执行器已开始返修。实际确认见 `.codebuddy/tmp/cb-econ-003-manager-audit/receipt-dispatch.json`，不冒称命令退出0即接收。

2026-10-12随后按用户「玩家可视化工程完整度检测」复核19:49最新版，冻结主DLL `6C900079…3C81C7`，913个源码/Def/双语/测试/回报文件及单独捕获的合同PNG；原执行回报追加§14，RESULT追加 `managerReview.visualCompletenessReview`，docs/README互链（均为上方已有声明文件）。20个复合检查点：3已接线/13部分/4缺失，实机0项；新增收集/气泡/诊断已确认，玩家全过程验收仍 BLOCKED，不沿用旧问题假装新版未修，也不关闭原18项未充分复验的缺陷。本轮未改产品、未构建/运行游戏/部署。证据在 `.codebuddy/tmp/cb-econ-003-visual-completeness-20261012/`。

执行器进度文件 `docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_PROGRESS.json` 已由返修登记；经理本轮仅只读核对其状态与版本证据，保留执行器原文，不将其中尚未绑定最新版的测试声明改判通过。

**摘要**：把"计划发布即远程签约、劳动完成即付款"改为**可见的地图手续**：计划只形成办理意图 →
Pawn 真实寻路到劳务站 → 到站复验并原子签约 → 签领**实体合同纸**（真实 Thing，带代次与补发）→
原版工作 → 收集**真实产物** → 到工资结算处交验 → 交付核验通过才放行既有账本付款。
薪资单价、计件单位、预算判据、金融真值与幂等身份**一律不变**；旧档与旧合同的权利分毫不受影响。

## 1. 改动与原因（按层）

| 层 | 改动 | 原因 |
|---|---|---|
| Domain | `LaborFlow.cs` 新增；`EmploymentContracts.cs` 扩模式/代次/资格 | 需求 §8.1「纯类型与规则，不引 Verse」；模式与资格必须**冻结在数据上**而非查询时重算 |
| Data | `LaborFlowSaveData.cs` 新增；`EmploymentSaveData.cs` 加字段与裁剪保护 | §9.1 版本化、未知值阻断、**不截断未付债务**；§5.3 承诺→负债守恒 |
| Application | `LaborFlowService.cs` 新增；`EmploymentService.cs` 加资格写入点 | §8.2 命令/结果契约；未知绝不降为成功；确定性 ID 保证重放不复制 |
| Economy | 新 Job/Thing/holder/捕获与三入口接线 | §4.3、§5.1、§6.2；原版对象只在本层适配 |
| Presentation | 劳动履约行 + 合同表阶段 + 气泡队列 | §7.3；只读、不发薪、不推进流程 |
| Def/LOC/Asset | ThingDef + 2 JobDef + 双语键族 + 贴图 | §7.4；GOV-010 全 Keyed |
| Capture | `Patch_GenPlaceOutput.cs` | §6.2 真实产物实例来源（禁止复算/找同 Def 冒充） |
| Tests | 40 项新增自动化 + 5 项隔离负面对照 | §10.2；断言须有牙齿 |

## 2. 改动清单（逐文件）

| 文件 | 类型 | 涉及符号 | 摘要 |
|---|---|---|---|
| Source/PersonalChronicle/Domain/PawnEconomy/Employment/LaborFlow.cs | A | 5 枚举 + LaborWorkflow/SubmissionRecord/ProductBatch/LaborFlowRules | 付款模式与流程阶段正交；资格冻结；批次守恒；PathTo 逐阶 |
| Source/PersonalChronicle/Domain/PawnEconomy/Employment/EmploymentContracts.cs | M | WorkContract 模式/代次；WageObligation 资格；EmploymentCode 新码 | 三重建点全传递；未知模式阻断金融动作 |
| Source/PersonalChronicle/Data/PawnEconomy/LaborFlowSaveData.cs | A | pawnEconomicLaborFlow/schemaVersion/PayloadIntact/三 Row | 版本化节点；未知版本阻断写入且保留载荷 |
| Source/PersonalChronicle/Data/PawnEconomy/EmploymentSaveData.cs | M | 行新字段/Parse*/Sum*/Trim*OnLoad | 承诺并入已赚未付；裁剪优先保未付负债 |
| Source/PersonalChronicle/Application/PawnEconomy/Employment/LaborFlowService.cs | A | BeginVisit/AttachContract/MoveTo/HoldAt/EnsureDocument/RegisterBatch/AcceptSubmission/PaymentGrant/Authoritative | 流程唯一写入点；存档权威收敛；确定性 ID |
| Source/PersonalChronicle/Application/PawnEconomy/Employment/EmploymentService.cs | M | SignRequest.DeliveryMode/TryOccupy/GrantSubmission/BlockCredential/ApplyDocument/UnpaidEarnedFor | 资格创建时冻结；代次单调；服务级付款判据 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborFlow.cs | A | 意图/Plan*/到站/签约/签领/交验/AdvanceStage/MayPayObligation | 地图劳务链主体；到站复验复用原子签约 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborOutputs.cs | A | NoteGeneratedThing/RegisterEvidenceOutputs/HasAllProductsFor/CountCarried | 真实产物两段式捕获；按 Def 核对件数 |
| Source/PersonalChronicle/Economy/PawnEconomy/Thing_WorkContractDocument.cs | A | 身份字段/IsValidFor/GetInspectString/GetGizmos | 实体纸只存身份；纸≠债权；旧代次作废 |
| Source/PersonalChronicle/Economy/PawnEconomy/LaborVisualText.cs | A | Stage/Hold/Reason/Next/Capability/Mode/Eligibility 标签 | 稳定 ID→文本唯一映射（整键回落） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorld.cs | M | delivery/DeliveryContainer/AcceptDelivery/JobDriver_LaborVisit/JobDriver_LaborSubmit | 独立交付接收 holder；两个办理 JobDriver |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs | M | Plan 分支/ExposeLaborFlow/三个周期维护 | 新 planner 前置且自门；tick 槽接入 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs | M | OnPlanPublished/TrySettleAfterEvidence/TrySettlePayable/RetryOutstandingPayables | 三入口统一出口；付款门；信用时点 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkOrders.cs | M | ReportVanillaOrderCompletion | 第三入口同规则 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs | M | 起草/签约/承接三处模式 + ContractDetailText | 冻结新模式（修 P0-1）；合同表显示阶段 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedSpeech.cs | M | PendingLaborSpeech/PostLaborSpeech/FlushLaborSpeech | 业务入队、渲染上下文发泡（解耦） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs | M | BuildModel(laborWorkflow) | 劳动履约快照采样 |
| Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.AccountModel.cs | M | BuildModel/AppendLaborRows/RowLabel | 劳动履约行（复用既有 Details 通道） |
| Source/PersonalChronicle/Capture/Patch_GenPlaceOutput.cs | A | Patch_GenPlaceOutput.Postfix | 真实产物实例捕获（只读） |
| Defs/PawnEconomy_WorkContract.xml | A | PC_PEA_WorkContractDocument/PC_PEA_VisitLaborOffice/PC_PEA_SubmitWorkContract | 合同纸与两个办理 Job |
| Languages/ChineseSimplified/DefInjected/JobDef/PawnEconomy_LaborFlow.xml | A | Key:*.reportString | 中文 JobDef 文本 |
| Languages/English/DefInjected/JobDef/PawnEconomy_LaborFlow.xml | A | Key:*.reportString | 英文 JobDef 文本 |
| Languages/ChineseSimplified/DefInjected/ThingDef/PawnEconomy_WorkContract.xml | A | Key:*.label/.description | 中文 ThingDef 文本 |
| Languages/English/DefInjected/ThingDef/PawnEconomy_WorkContract.xml | A | Key:*.label/.description | 英文 ThingDef 文本 |
| Languages/ChineseSimplified/Keyed/PawnEconomy.xml | M | Key:PC_PEA.LaborVisual.* | 中文键族（86 键） |
| Languages/English/Keyed/PawnEconomy.xml | M | Key:PC_PEA.LaborVisual.* | 英文键族（逐键同构） |
| Textures/Things/Item/PawnEconomy/PC_PEA_WorkContract.png | A | AssetID:PC_PEA_WorkContract | 128×128 RGBA 贴图 |
| Tools/make_labor_contract_icon.py | A | make_labor_contract_icon | 贴图可复现生成脚本 |
| Tests/LaborFlowTests.cs | A | LaborFlowRulesTests/LaborFlowServiceTests | 23 项规则与服务断言 |
| Tests/EmploymentLaborGateTests.cs | A | EmploymentLaborGateTests | 13 项资格门与守恒断言 |
| Tests/LaborFlowScribeTests.cs | A | LaborFlowScribeTests | 4 项真 Scribe 往返 |
| docs/设计文档/架构/EXC-2026-001.md | M | Patch #26 Patch_GenPlaceOutput | Harmony 例外补登 |
| docs/changelog/v1.4.x_2026-10-12_劳务履约可视化与实体合同.md | A | Change ID CHG-20261011-LABOR-VISUAL | 用户向 Changelog（玩家影响 + 未验证项 + 署名） |
| docs/任务派发/CodeBuddyCN/CB-ECON-003_劳务履约过程可视化与实体合同_需求单.md | M | front matter 状态 + 实装注记 | 状态置为已实装；设计正文原文留痕 |
| docs/README.md | M | 经济需求驱动 AI 主题索引条目 | 由「仅设计未实施」更新为已实装并互链 |
| docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_执行回报.md | A | DOC-REPORT-20261012-CB-ECON-003 | 执行回报主文档（ReadyForReview） |
| docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_ACK.json | A | taskId/changeId/taskSha256/revisions | 标准接收回执 |
| docs/任务派发/CodeBuddyCN/回报/CB-ECON-003_RESULT.json | A | binary/tests/gates/notRun | 机器可读交付结果 |

## 3. 关键不变量（修复后逐条有测试与对照）

1. **不存在远程先签**：意图阶段 `ContractId` 为空，签约只在到站复验后发生（`LaborFlowServiceTests.IntentCreatesNoContract`）。
2. **新模式必是显式写入**：起草/签约/承接三处都冻结 `PhysicalSubmission`（`DraftedPostedRowPersistsTheFrozenMode`、`TryOccupyWritesTheFrozenModeInTheSameCommit`）；此前的"默认旧模式"是 GATE-ARCH P0-1。
3. **交验前不付款**：付款门唯一判据 `LaborFlowRules.PaymentAllowed`，三入口与补付链共用（`PaymentGateBlocksUnsubmittedAndUnknown` + NC-A 对照）。
4. **完成不释放资金占用**：`SumOutstandingCommitments = 未完成承诺 + 已赚未付`（`OutstandingCommitmentsIncludeUnpaidEarned` + NC-B 对照）。
5. **旧档权利不变**：合同/应付缺键 ⇒ LegacyImmediate/Immediate ⇒ 既有付款与补付行为逐字保留（`LegacyContractEvidenceStaysImmediatelyPayable`、`RowWithoutModeReadsAsLegacyImmediate`）。
6. **代次不可倒退**：`ApplyDocument` 单调（`ApplyDocumentIsMonotonicAndIdempotent` + NC-C 对照）；旧纸无领取权利。
7. **阶段不可跳过亦不可停滞**：写侧严格 +1，跨过程性阶段由 `LaborFlowRules.PathTo` **逐阶补记**（`PathToWalksEveryIntermediateStageLegally`）；陈旧实例不得回退阶段（NC-D 对照）。
8. **幂等凭据不裁**：提交行作为重复交货的防线不参与载入裁剪（注释说明为何本层无法证明"可安全裁"）。

## 4. 验证与范围

- **编译**：`_scripts/compile_with_sdk.ps1` ⇒ **0 错 0 警**（含新增 8 个源文件、1 个 Capture 补丁）。
- **测试**：`dotnet test Tests\PersonalChronicle.Tests.csproj -c Release` ⇒ **1568 通过 / 0 失败 / 1 跳过**（新增 40 项）。
- **隔离负面对照**：`.codebuddy/tmp/cb-econ-003/negative-controls.ps1` ⇒ **5/5 变异均被对应断言抓到**（付款门 / 承诺占用 / 代次倒退 / 陈旧实例 / 资格校验），结束后**源码逐文件哈希还原一致**。
- **未执行（NOT RUN）**：GameVerified（真实地图连续流程、存读档实机、录像）、性能基准、部署与工坊发布 —— 沙箱内无游戏进程，须由 PM 在实机执行（需求 §10.3 G01–G14）。
- **范围**：本标注 32 个文件为**本轮全部改动**；工作区其余 2369 项为本仓历史/并行未提交工作，已由 `-ExcludeFromFile` 声明为范围外，未做任何清仓、重置或覆盖。

## 5. GATE-DELTA 自检

- [x] 清单覆盖本轮全部实际改动（32/32 命中改动集 A，0 遗漏）
- [x] 清单范围 ⊆ Change Summary.Scope，无超范围改动
- [x] 每个文件标注了类型（A/M）与涉及符号
- [x] Change ID 与需求单 `change_id`、EXC 台账、Changelog 互链一致
- [x] 无调试代码 / 注释掉的死代码 / 临时 TODO 残留（残留扫描：无 `Console.WriteLine` / `Debug.Log` / `TODO` 新增）
- [x] 已用 `gate-runner.ps1 -Base HEAD -ExcludeFromFile .codebuddy/tmp/cb-econ-003/out-of-scope.txt -Blocking` 校验

## 6. 回滚

- **代码回滚**：删除 8 个新增源文件 + 还原 11 个修改文件 ⇒ 完全回到改动前行为（旧链零依赖新类型）。
- **存档回滚**：新增节点 `pawnEconomicLaborFlow` 与合同/应付新字段均为**加性**；旧版本 DLL 载入时忽略未知节点/字段 ⇒ 不损坏原档。已给出部署前备份清单（需求 §12.3）；新模式产生的"未付欠薪/未核销批次"在降级后不可由旧版本识别，须以**升级前存档**恢复（不承诺降级后新债仍保留）。
- **资产回滚**：删 `Textures/Things/Item/PawnEconomy/PC_PEA_WorkContract.png` 只会让合同纸缺图（业务不受影响，Def 缺失时明确降级为无纸呈递）。

---

## 附录：设计轮记录（2026-10-11，由 AI 助手 Codex 起草）

> 保留原文以留痕：本轮之前本 Change ID 只交付**需求与拟定设计**（`docs/任务派发/CodeBuddyCN/CB-ECON-003_劳务履约过程可视化与实体合同_需求单.md`
> 与 `docs/README.md` 索引），无运行代码/Def/资产/语言/存档/部署改动。该两文件不在本轮改动集内（已提交或属设计轮）。

由 AI 助手 Codex 编写。用户直接要求先拟详细完整需求单，该轮只交付设计，不执行源码/资源/存档改动、测试构建或部署。
26 项 REQ、三提交点、正交状态、实体纸、真实产物、无产品分支、异常/存档/性能矩阵、31 自动化与 14 实机场景、
Gate/回执/回滚均在该需求单内；本轮实装按该单执行，并把 GATE-ARCH 复核发现的 P0-1 与四处 P1 一并修复。

## 七、返修 R2：实机"接合同/提交合同缺失寻路"（2026-10-12，AI 助手 CodeBuddy CN）

### 7.1 触发与首因（证据）

- 用户实机反馈：「经济机构没有自主办理服务点，pawn 接合同和提交合同都缺失寻路」。
- **首因（实测）**：游戏部署区 DLL（`Mods\殖民地综合管理系统\Assemblies\PersonalChronicle.dll`，2026-10-11 22:57）
  经 Mono.Cecil 核实**不含** `JobDriver_LaborVisit` —— 上一轮交付的 `implementation_status` 明写"部署 NOT RUN"，
  游戏里跑的是**没有这条链**的旧版本；同时 Defs 目录缺 `PawnEconomy_WorkContract.xml`、Languages 缺
  `PawnEconomy_LaborFlow.xml`。⇒ 修复的第一动作是**全量部署**（DLL + Defs + Languages + Patches 同版本）。
- **次因（源码链路断点，均为实机可见）**：
  ① CA-07：**没有收集产物的 Job** ⇒ 实物型合同永远停在 `ProductNotCarried` 退避，走不到工资结算处；
  ② CA-09：流程 ID 永久绑定"人+机会"，首轮 Archived 后 `BeginVisit` 仍返回旧行 ⇒ **二次求职永远领不到办理 Job**。

### 7.2 本轮修复（逐项 ↔ 经理 CA 编号）

| CA | 等级 | 本轮处置 | 落点 |
|---|---|---|---|
| CA-01 来源核验 | P0 | **部分关闭**：交付物按批次记录的**真实实例 ID**（`thingIDNumber`）逐件核验，第三人同 Def 物品一律拒收；唯一的从宽分支（原实例已被拆走/合堆时）**只对该批次的产出者本人**生效，且只在其原实例确已不在图上时成立 | `MatchesBatchInstance` / `InstanceOnMap` |
| CA-02 先资格后收货 | P0 | **关闭**：交验改为**先物理接收、后写回执**，只有**覆盖完成**才授予付款资格；部分趟只留回执、权利保留、不放款 | `SubmitOne` / `TransferProductsToStation` / `IsDeliveryCovered` |
| CA-03 伪造入库凭据 | P0 | **关闭（fail-closed）**：删除 `accepted:合同|证据` 字符串凭据；Hunt/Slaughter/Haul 无权威回执来源 ⇒ 按需求 §6.1 如实降级 `Unsupported`（不再新签该三族实体合同，旧权利不动） | `LaborFlowRules.CapabilityFor` / `SubmitOne` 的 `ReceiptUnverifiable` 分支 |
| CA-04 半提交/孤立核销 | P1 | **关闭**：`AcceptSubmission` 把"批次存在 / 数量守恒 / 提交表容量 / 写入可用"全部**前置预检**，任一不成立即 Unknown 且**零写入**；回执键由已落盘台账派生（累计接收量），同趟重放同键、不同趟不撞键 | `LaborFlowService.AcceptSubmission` / `CanAcceptSubmission` / `SumAcceptedForContract` |
| CA-06 纸核验不一致 | P1 | **关闭**：呈递/补领统一走 `IsValidFor`（结构+授权持有人+代次+实例），并**沿本人持有器链**（携带→库存→嵌套容器）定位 | `HoldsLiveDocument` / `HoldsDocument` |
| CA-07 无自主收货链 | P1 | **关闭**：新增 `PC_PEA_CollectContractOutput` JobDef + `JobDriver_LaborCollectOutputs`（容量守卫→寻路→取入本人**库存**，跨 Job 保留），接进 `Plan`（Visit → Reissue → CollectOutputs → Submission）；多趟由重复规划承担 | `EconomyWorld.cs` / `PlanContractCollectOutputs` / `EconomyRuntime.Plan` |
| CA-08 捕获来源/重放 | P1 | **部分关闭**：合堆时**回落到落地格上的真实存活堆**（修"传入实例被销毁 ⇒ 本次产出永不登记"）；同一实例短窗口内重复上报**去重**（修"10 件变 20 件"）；**登记失败不再丢笔记**（可重试）。未做：生产人上下文绑定（仍按中心格推断） | `NoteGeneratedThing` / `RegisterEvidenceOutputs` |
| CA-09 第二轮死锁 | P1 | **关闭**：流程 ID 加**轮次后缀**（`…|r{n}`，轮 1 沿用旧格式保证旧档可读；后缀即权威，无第二份字段）；归档后开新轮，旧行/旧合同/旧回执原样保留；同轮幂等 | `LaborWorkflow.IdFor/RoundOf` / `BeginVisit` / `ReadWorkflowFor` |
| CA-11 到站复验不足 | P1 | **关闭**：SignCommit 在最终提交前重查制度门、站点种类、征召/倒地、可达性 | `LaborSignCommit` |
| CA-15 运行本地化裸键 | P2 | **关闭**：合同详情的 `T("PC_PEA.LaborVisual.Contract.*")` 去掉重复前缀（修前查 `PC_PEA.PC_PEA.*` ⇒ 屏上显示裸键） | `EconomyVanillaWork.ContractDetailText` |
| CA-17 接收仓不在 holder 树 | P1 | **关闭**：新增 `DeliveryHolder` 适配器把接收区挂进原版递归持有器链（`ThingOwner` 本身不是 `IThingHolder`，故必须适配） | `Building_EconomyInstitution.GetChildHolders` |
| CA-18 缺 Def 跳过实体门 | P1 | **关闭**：ThingDef 缺失时**暂停**（对账 + 原因）而不是静默推进到"持约履约"、也不播"合同领到了" | `LaborIssueDocument` |
| — 部署 | P0 | **已执行**：`deploy-to-game.ps1` ⇒ 255 文件；工程 DLL == 部署 DLL（SHA-256 `A42B9086…`）；部署区已含 `PC_PEA_CollectContractOutput` 与新语言键 | `_scripts/deploy-to-game.ps1` |

**明确未关闭（如实登记，不冒称）**：CA-05（未知 schema 原始载荷保全）、CA-10（终止后补发恢复与损失豁免出口）、
CA-12（Bills 交付链）、CA-13（主 UI 实时阶段接线）、CA-14（气泡事件队列）、CA-16（超限裁剪的显式阻断）、
CA-01 的"拆分/合堆片段谱系"完整版、CA-08 的"生产人上下文绑定"。这七项 + 两项部分关闭共同构成下一轮的返修清单。

### 7.3 验证（同终态证据）

- 编译：`_scripts/compile_with_sdk.ps1` ⇒ **0 诊断**（第一次因 `Assemblies\PersonalChronicle.dll` 被占用报 CS2012，
  该轮结论已作废，重试成功后才计证据）。
- 测试：`dotnet test Tests/PersonalChronicle.Tests.csproj -c Release` ⇒ **1570 通过 / 0 失败 / 1 既有跳过**
  （新增/改写：多趟守恒与零写入、续轮身份、轮号 fail-closed、能力矩阵降级、读档往返累计键）。
- 部署：`deploy-to-game.ps1` ⇒ 255 文件；Cecil 核实部署区 DLL 含 `JobDriver_LaborVisit` 与
  `JobDriver_LaborCollectOutputs`；Defs/Languages 已同步（见 7.2 末行）。
- **NOT RUN**：GameVerified（真实地图 G01–G14）、性能基准、工坊发布。**必须重启 RimWorld** 才能看到本版。

### 7.4 回滚

- 代码：本轮 15 个文件按 `git checkout --` 单文件回滚即回到 R1 行为（新 JobDef/语言键可留在盘上，不被引用即无副作用）。
- 部署：`_backups\release-deploy\deploy_20261012_184324_054\` 保存部署前 249 个文件，整目录回拷即恢复旧版本。
- 存档：schema v1 → v2 为**加性升级**（无新增必需字段）；多轮流程行与累计键回执在旧版本下**不被识别**，
  须以升级前存档恢复（不承诺降级后新债/新批次仍保留）。

—— 返修 R2 由 AI 助手 **CodeBuddy CN（DeepSeek-V4.1-Flash）** 于 2026-10-12 编写；沿原 Task/Change ID，未新建平行单。

## 八、返修 R3：按经理审计回执 §3 逐项补全（2026-10-12，AI 助手 CodeBuddy CN）

### 8.1 本轮关闭（有独立断言 / 玩家真实入口）

| CA | 等级 | 本轮处置 | 证据 |
|---|---|---|---|
| CA-05 | P1 | **关闭**：未知（更高）版本加载时把本节点**原始 XML 原样留证**（`Scribe.loader.curXmlParent.OuterXml`），保存时原样写回 ⇒ 未来字段跨保存/载入往返仍存在（修前只置 `CanWrite=false`，下次保存照样整段丢失） | 新增真实 Scribe 往返断言 `UnknownSchemaPayloadSurvivesRoundTrip`（手工构造 schemaVersion=99 + futureField=KEEP-ME 的存档，加载→再保存→断言字段与值仍在）；`LaborFlowSaveData.RawPayload` |
| CA-10 | P1 | **关闭（入口已实装）**：新增 `ReconciliationAction{Resume,WriteOffRemaining}` + `LaborFlowService.ResolveReconciliation`（唯一写点：按事实清 Hold / 按可信损失逐批核销剩余，**不碰金额与债权**）；机构建筑新增**对账处置按钮**（仅当确有流程卡在对账时出现；玩家先核验物理事实再二选一） | 服务断言 `ReconciliationCanBeResolvedAndNeverInventsFacts`（核销后剩余 0、覆盖完成、Hold 清除、重放 Replay、未知流程 Unknown）；`Building_EconomyInstitution.GetGizmos` 入口 |
| CA-13 | P1 | **部分关闭**：阶段由**真实 Job 事件**推进 —— 开始搬运 ⇒ `CollectingOutputs`、带货运往 ⇒ `GoingToSettlement`（JobDriver toil 回调 `NoteLaborCollectingStarted` / `NoteLaborGoingToSettlement`；规划与绘制路径一律不写阶段） | `EconomyWorld.cs` 两个 JobDriver 的 announce toil；`LaborFlowService.MoveTo` 仍是唯一阶段写点（严格前进） |
| CA-15 | P2 | **关闭**：补齐运行期实际使用的 6 个动作键 + 22 个阻断/原因键 + 5 个对账按钮键（中英同键同数）；合同详情双前缀已在 R2 修掉 | `audit-keyref-integrity.ps1` 结论：未登记未解析引用 0、Defs 键引用未定义 0 |
| CA-16 | P1 | **关闭**：新写入达上限时**如实阻断**（既有断言 `WriteIsRefusedAtLimitInsteadOfDroppingExistingRows`：前 1024 条成功、第 1025 条 Blocked 且行数不变）；载入裁剪改为**显式记录** `TrimmedOnLoad` + 告警，未终态/未结清行一律不裁 | `LaborFlowSaveData.TrimmedOnLoad` + 两处 `Log.WarningOnce`（0x1EC0F08/0x1EC0F09） |
| CA-08 | P1 | **部分关闭（本轮补强）**：产出者改由**工作上下文绑定** —— 只认「当前 Job 属已识别订单 ∧ Job 目标就是这次落地格/物（±1 格）」的玩家殖民者；匹配到多个 ⇒ 不猜（返回 null 不登记）。配合 R2 的合堆回落、去重窗口、失败笔记可重放 | `EconomyLaborOutputs.FindProducerPawn` / `TargetMatches`（新增）；原 `center.GetFirstPawn(map)` 已删除 |
| CA-01 | P0 | **部分关闭（本轮补强）**：在 R2 的实例 ID 核验之上再要求**合法持有人一致**（批次记录的产出者必须是本次呈递人；旧档缺键时仍须实例 ID 吻合） | `EconomyLaborFlow.MatchesBatchInstance` 的 `producerOk` 门槛 |
| — | — | **已部署**：`deploy-to-game.ps1` ⇒ 目标 DLL 2026-10-12 18:56:51（2,323.0 KB），Defs 38 / Languages 77 / Assemblies 1 同步 | 部署脚本 stdout |
| — | — | **T31 初步取证（未结案）**：`Employment.OutstandingCommitments`（= 承诺 + 未付已赚，**全局**口径；`EmploymentSaveData:392/406/414`）被从**按图/按机构**的 `QueryPayrollFundingMinor(pawn.Map, anchorKey)` 中扣除（`EconomyVanillaWork.cs:393` 签约门、`:510` 付款可用额）⇒ 跨图/跨机构**可能串占用**（需回执要求的跨图宿主证据定案）；**未复现**已 Reserved 被双扣：预留只在 `ShadowTransactionKernel.cs:87/142` 增减，工资可用额读的是 `FinancialAccountSnapshot.AvailableMinor`（Balance − Reserved） | 见左列文件与行号 |

### 8.2 本轮仍**未关闭**（下一轮返修清单，不冒称）

- **CA-03**：狩猎/宰杀/搬运的**真实收尸/产品与合法接收凭证**仍未接通（R2 已把它们如实降级 `Unsupported` 并删除伪造凭据分支；关闭条件要求真实收尸/产品 + 核对原合法接收凭证）。
- **CA-12**：工坊生产（Bills）主链未覆盖。
- **CA-14（R4-B1/B1' 已实装，实机未验）**：气泡已从「每 Pawn 单槽字典」改为**每 Pawn 有界队列（3 条）+ EventId（Pawn+键+tick，确定性派生）去重 + 有界去重环（128 条、30 tick 窗口）**；事件覆盖从 4 类扩到 **9 类**（洽谈 / 办理等待 / 签领 / 收集产物 / 到站核验 / 丢纸 / 待付款 / 交验 / 到账），**时间基准统一**：待播窗口 120 → **1200 tick**（按最坏 4 倍速 × 4.5s 气泡寿命折算，任何速度下不短于屏上寿命）。**R4 追加：缺款（`InsufficientBacking` 时）、拒签（其它原因）、补发（代次>1）亦已接入。** 气泡事件共 **12 类**：阶段类 Waiting / Negotiating / IssuingDocument(SignedDocument) / CollectingOutputs / Verifying / AwaitingPayment / PaymentConfirmed(Paid)，状态类 DocumentMissing / NoFunds / SignRejected / Reissued / SubmittedProducts·SubmittedProof。**如实登记两处未发泡**：① `GoingToSettlement`（搬运途中的目标态；其 announce toil 只推阶段，紧接的核验已有泡）；② `Working` / `Archived`（长期进行态与终态，不发泡是有意设计）。剩余 = 实机 T20/G11（3 倍速下不消失）与全程观感。**实现纪律**：去重环不用 `Queue` 泛型与 `HashSet` 泛型（测试宿主加载 `Queue`1` 抛 TypeLoadException，本仓仅一处 Queue 从未被测试加载），改用全仓 139+ 处验证过的 `Dictionary`。
- **REQ-021 诊断面（R4-B3 已实装）**：新增 `BuildLaborDiagnostics()`（**只读、零副作用、不触发 Scribe**：流程 ID/阶段/Hold/原因/下一步/批次剩余/回执条数，上限 8 条）＋机构建筑「劳务诊断快照」按钮（弹窗可复制 + `Log.Message` 留痕）。此前该出口**完全缺失**（`ExposeLaborFlow()` 只是持久化暴露，曾被误当成诊断面）。
- **CA-13 剩余部分**：账户 / 劳务站 / 工资处 / 合同纸的**只读同源主界面**接线（本轮只接了阶段推进）。
- **CA-01 剩余部分**：拆分/混堆/转 holder 的**片段谱系**完整版（现为实例 ID + 产出者 + 原实例已消失时的从宽分支）。
- **CA-07/CA-09/CA-11/CA-17/CA-18 的实机确认**：代码与离线断言已就位，但 G01–G14 未跑（NOT RUN）。
- **回执 §5 的验收工程**：REQ→T→G 全矩阵、五项指定负面对照、故障注入（预检后/部分转移后/回执写前后/资格写前后/付款 Finalized 后）、四哈希绑定与 PROGRESS/RESULT 追加 —— 本轮**未做**（属下一轮交付工程）。

### 8.3 验证（本轮终态）

- 编译：`_scripts/compile_with_sdk.ps1` ⇒ **0 诊断**。
- 测试：`dotnet test Tests/PersonalChronicle.Tests.csproj -c Release` ⇒ **1573 通过 / 0 失败 / 1 既有跳过**（新增 3 项：CA-05 存档往返、CA-10 对账处置、CA-16 限额拒绝）。
- 语言：`audit-keyref-integrity.ps1` ⇒ 未登记未解析引用 0、Defs 键引用未定义 0；新增键中英同键同数。
- **NOT RUN**：GameVerified（G01–G14）、性能基准、五项指定负面对照、工坊发布。
- **已知未修（非本轮引入，另行归属）**：`_scripts/verify-pawn-economy.ps1` 在 `ShadowPlanPresenter.AccountModel.cs` 上抛出缺键 `PC_PEA.Model.NoPlan`（约 80 个 `PC_PEA.Model.*` 键缺失），属**个人账户/需求页在途任务**的键集，本模块未擅自补（避免覆盖其他在途任务），如实登记。

—— 返修 R3 由 AI 助手 **CodeBuddy CN（DeepSeek-V4.1-Flash）** 于 2026-10-12 编写；沿原 Task/Change ID。


## 九、Codex 可视化修复与收尾（2026-10-12）

当前完整记录见原回报§15；VFC-01–12代码修复、全量1609/0/1、经济/需求UI/双语检查通过，最终文件已部署；GameVerified / Released未放行。37个源码/测试/资源/检查器差异与1098文件冻结清单在 `.codebuddy/tmp/cb-econ-003-visual-repair-20261012/`。最终DLL SHA `8DD291696C6E06222076EE6392B1508C4BE3B58B9FA13C10188B3FD9CF146A01`，测试与Mods主DLL一致；回滚首次21:23备份，最终21:33备份仅中间DLL。原CA-12/Bills、CA-03真实接收凭据、通用外部拆合谱系CA-08、跨图T31及G01–G14未宣告关闭。最终Gate结果追加于§15与RESULT。

### 本轮逐文件差异登记

| 实际文件 | 差异行数 | 行级证据 |
|---|---|---|
| `Defs/PawnEconomy_Institutions.xml` | 12 | `source-diff.patch` / `changed-product-files.json` |
| `Languages/ChineseSimplified/DefInjected/JobDef/PawnEconomy_LaborFlow.xml` | 12 | `source-diff.patch` / `changed-product-files.json` |
| `Languages/ChineseSimplified/Keyed/PawnEconomy.xml` | 135 | `source-diff.patch` / `changed-product-files.json` |
| `Languages/English/DefInjected/JobDef/PawnEconomy_LaborFlow.xml` | 12 | `source-diff.patch` / `changed-product-files.json` |
| `Languages/English/Keyed/PawnEconomy.xml` | 135 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Application/PawnEconomy/Employment/EmploymentService.cs` | 23 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Application/PawnEconomy/Employment/LaborFlowService.cs` | 199 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/BuildStamp.g.cs` | 10 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Data/PawnEconomy/LaborFlowSaveData.cs` | 136 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Domain/PawnEconomy/Employment/LaborFlow.cs` | 122 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Domain/PawnEconomy/Perception/LaborSpeechQueue.cs` | 136 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs` | 40 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborCarryPresentation.cs` | 59 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborCounterPresentation.cs` | 160 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborFlow.cs` | 674 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborInteraction.cs` | 352 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborPresentation.cs` | 241 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborTransport.cs` | 82 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedDashboard.cs` | 16 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/EconomyNeedSpeech.cs` | 288 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs` | 10 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs` | 54 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorld.cs` | 215 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/NeedAccountModelView.cs` | 21 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/NeedsCyclePanel.Account.cs` | 17 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/ShadowPlanPresenter.AccountModel.cs` | 64 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/Thing_WorkContractDocument.cs` | 74 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.Table.cs` | 96 | `source-diff.patch` / `changed-product-files.json` |
| `Source/PersonalChronicle/Economy/PawnEconomy/Window_LaborRecord.cs` | 54 | `source-diff.patch` / `changed-product-files.json` |
| `Tests/LaborFlowScribeTests.cs` | 43 | `source-diff.patch` / `changed-product-files.json` |
| `Tests/LaborFlowVisualCycleTests.cs` | 213 | `source-diff.patch` / `changed-product-files.json` |
| `Tests/LaborPaymentFeedbackTests.cs` | 51 | `source-diff.patch` / `changed-product-files.json` |
| `Tests/LaborPhysicalTransferTests.cs` | 127 | `source-diff.patch` / `changed-product-files.json` |
| `Tests/LaborPresentationTests.cs` | 85 | `source-diff.patch` / `changed-product-files.json` |
| `Tests/LaborSpeechQueueTests.cs` | 131 | `source-diff.patch` / `changed-product-files.json` |
| `Tests/LaborVisualBoundedWorkloadTests.cs` | 62 | `source-diff.patch` / `changed-product-files.json` |
| `_scripts/verify-pawn-economy.ps1` | 22 | `source-diff.patch` / `changed-product-files.json` |

最终治理复跑：**GATE-DELTA PASS，严格模式覆盖60文件、0提示**；2526项无关存量/并行改动显式排除，其中本轮新出现的LONGPROJ-LEDGER/TOOLGOV-W1独立标注未改动。**DocGuard BLOCKED 11，新增阻断0、WARN2**：10项DLC接口参考包和1项公有资产文档既有front matter缺项，基线未动，不能记全局PASS。最终冻结1098文件无漂移，主DLL/测试引用/Mods及10项显式载荷哈希一致；需求与经理回执不变。当前Player.log信息只作旧运行边界，未升级GameVerified。治理日志及实际哈希已写RESULT.managerReview.visualRepair。


## 十、机构Def加载失败热修（2026-10-12）

最新原回报§16；上一版MapMeshAndRealtime错拼导致实机5Def加载失败（日志已冻结），现正确MapMeshAndRealTime，合同纸useHitPoints/MaxHitPoints一并修正；检查器新增实际程序集顶层枚举大小写和可燃HP约束，2隔离负对照实测检出。三个文件此前均已声明，当前行级证据hotfix-diff.patch；2Def已部署，DLL不变、未复跑全量、未实机验证恢复。原档5机构仍完整并备份，未修改。

## 十一、完成后收集产物修复（2026-10-12，Codex）

原回报§17为最新。7生产文件+2测试+EXC行26增量冻结；Harmony由旧后置查邻近堆改为真实9参数out-Thing入口Prefix/WrapPlacedAction，只记本次增量和实际载体，保留原回调。Domain LaborFlowRules.FulfillmentPriority；EconomyRuntime.SafeFulfillmentDecision；EconomyWorld只读优先级；EconomyLaborFlow收尾顺序/无批次对账/Pawn诊断；EconomyLaborOutputs精确来源/证据/实例批次。BuildStamp为14:32 UTC。

Tests/LaborOutputCaptureTests.cs新增12例；Tests/LaborFulfillmentSchedulingTests.cs新增8例。全量1629/0/1；10文件差异在.codebuddy/tmp/cb-econ-003-output-collect-fix-20261012/repair-diff.patch及freeze-manifest.json。只部署新DLL，保留§16正确Def；GameVerified NOT RUN。Niklas此次实机根因未得新日志证实；旧漏登记产物不从附近库存补造。原单剩余验收不变，治理结果见§17末。

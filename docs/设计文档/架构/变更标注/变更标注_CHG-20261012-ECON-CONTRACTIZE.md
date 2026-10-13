---
change_id: CHG-20261012-ECON-CONTRACTIZE
title: 经济合同化：公有储物按市价计费 + 工作需求表 5 列 + 工作合同弹窗 + 机构政策退役
date: 2026-10-12
state: CodeVerified / TestVerified / Deployed（2026-10-11 接续；GameVerified/Released 未验证）
layer: Domain/PawnEconomy（合同/规则）+ Data/PawnEconomy（Scribe）+ Application/PawnEconomy（服务/DTO）+ Economy/PawnEconomy（读模型/UI）
scope: 用户三问题与B1/B2/W1-W4既有交付；接续合同表布局、授权修复、COMP隔离安装、S3完成信号与S4两级定价
author: CodeBuddy / DeepSeek-V4.1-Flash
files:
  # ── 域 / 数据（合同化地基）──
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Employment/EmploymentContracts.cs
    type: M
    note: ContractState.Posted=4（旧档 1/2/3 数字不动）；时段字段 + OccupiedBy + AllowsHour（跨夜）；Posted 允许空持约人
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Employment/WorkContractRules.cs
    type: A
    note: 起草规则纯函数（推荐价/钳制/StepHour/ValidateDraft 稳定 ID/饱和乘/有界采样契约）
  - path: Source/PersonalChronicle/Data/PawnEconomy/EmploymentSaveData.cs
    type: M
    note: windowStart|windowEnd Scribe（旧档缺键=全天）；ReadPostedContractsFor；过期裁剪与工资预留扩展到 Posted
  - path: Source/PersonalChronicle/Application/PawnEconomy/Employment/EmploymentService.cs
    type: M
    note: SignRequest(+时段/+State)；Sign 支持 Posted；TryOccupy（承接）；ReadContract 读回时段；ReadPostedContractsFor
  - path: Source/PersonalChronicle/Application/PawnEconomy/EconomyView.cs
    type: M
    note: EconomyRow.Compose（行点击起草载荷；与 Detail 展开互斥）
  - path: Source/PersonalChronicle/Application/PawnEconomy/WorkspaceView.cs
    type: M
    note: WorkspaceVerb 删 VanillaJobsDown/Up（每份±退役）
  # ── 工作链（起草 → 发布 → 承接）──
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs
    type: M
    note: TryDraftVanillaWorkContract（唯一起草入口）；FindPostedContractFor/TryOccupyPostedContract（承接+FA-11 复核）；RecommendWageMinor（产出市价×50%，有界≤8）；退出判据"政策缺失≠机构关闭"；合同化后不再依赖政策（工资=基准常量）；每份±档位族删除
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyVanillaWorkSource.cs
    type: M
    note: Posted 合同机会段（与政策并存）；政策三门槛退役（工资=基准常量）
  # ── 表格 / 窗口 ──
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs
    type: M
    note: 工作需求表 5 列（工作类型/工作岗位/总需求/已派发/就业率）；mod 岗位行统一进表；每份±行动词与长说明段删除；政策页签（5 处）与政策摘要行删除
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.Table.cs
    type: M
    note: 行点击 → 起草回调（快照期绑定）；OpenWorkContractDialog（解析载荷→算可派发/推荐价→开弹窗→唯一写入口→重快照）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs
    type: M
    note: 每份±动作分支删除；政策承载全删（HostedPolicy 字段/切页聚焦/渲染器分发/Policy 导航动词）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_WorkContract.cs
    type: A
    note: 工作合同弹窗（数量/施工时段/单件薪资，推荐价预填，失败留窗显示稳定原因；零星 Widgets/GUI）
  # ── 储物按市价（问题一）──
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyFoodCostSource.cs
    type: M
    note: 价源改储物市价派生（政策不再参与）；类注释更新（旧裁决"不做市价替身"被本批用户裁决取代）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyStoredFoodPurchase.cs
    type: M
    note: 扣款=件数×单件市价（经每营养价派生，数学等价）；PerNutritionMarketMinor helper
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyStorage.cs
    type: M
    note: QuotedMealPrice 按该件自身市价；LowestFoodQuote 改为扫公有可食物取 min(单件市价÷营养)，零人工报价
  - path: Source/PersonalChronicle/Economy/PawnEconomy/PublicStockpile.cs
    type: M
    note: 「设定食品售价」gizmo 与 SetPublicStockpilePrice 删除
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Building_EconomyStorage.cs
    type: M
    note: 价格 gizmo/FoodPriceCommand 删除；inspect 改「按物品市价计费」
  # ── 政策退役（问题三）──
  - path: Source/PersonalChronicle/Economy/PawnEconomy/PolicyPanel.cs
    type: D
    note: 政策页内容层整体删除（677 行）；UI 面归零后零引用
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionCards.cs
    type: M
    note: 机构卡「政策」按钮与整卡点击移除（卡片改纯只读）；Policy 动词退化为行种标记
  # ── 测试 ──
  - path: Tests/WorkContractRulesTests.cs
    type: A
    note: 起草规则 12 条离线断言（推荐价/回落/时段半开/跨夜/0-0 全天/校验五拒/饱和乘/Posted 承接不改条款）
  - path: Tests/PublicStockpileTests.cs
    type: M
    note: 删人工设价断言与"改价传播"测试（功能已整体移除）
  # ── 语言 / 脚本 ──
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
    note: +5 列表键 +6 弹窗键 +16 弹窗/原因键 +市价计费说明键
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
    note: 同步（术语 contractor/posted 一致）
  - path: _scripts/compile.rsp
    type: M
    note: PolicyPanel.cs 条目移除（rsp 注释占位）
  - path: _scripts/verify-pawn-economy.ps1
    type: M
    note: $ui 断言目标改指 Dialog_WorkContract.cs（政策页已删）
  - path: _scripts/verify-pawn-institution-mode.ps1
    type: M
    note: $ui 与源文件清单两处改指
  # ── B1 小修段（2026-10-11 · 见 §五）──
  - path: Source/PersonalChronicle/Application/PawnEconomy/VanillaContractRow.cs
    type: A
    note: 合同行载荷唯一组装点（纯函数：两型同列数 / 撤销载荷只挂 Posted / 控件格恒空）
  - path: Source/PersonalChronicle/Application/PawnEconomy/EconomyTable.cs
    type: M
    note: EconomyCellControl 列语义（Action=控件格）—— 呈现层不认列键/末列位置（LAND-010）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
    type: M
    note: TryRevokePostedContract（撤销唯一写入口；只接受 Posted；失败给稳定 reason）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyRowList.cs
    type: M
    note: RowButtonTone 增 Revoke ⇒ 破坏性动词统一危险色（色调规则唯一来源）
  - path: Source/PersonalChronicle/Capture/Patch_PublicStockpile.cs
    type: M
    note: 公有储存区 inspect 改同源键 PublicMarketPriced（删旧人工报价/未报价分支）
  - path: Languages/ChineseSimplified/Keyed/PawnEconomyStorage.xml
    type: M
    note: 删 NoQuote / PriceValue 两键（人工报价链已整体移除，LOC-001 无孤儿）
  - path: Languages/English/Keyed/PawnEconomyStorage.xml
    type: M
    note: 同步删除两键
  - path: Tests/EconomyEmploymentTests.cs
    type: M
    note: +3 断言：幂等比较含时段 / Posted 承接就地改状态且拒绝撤销 / 撤销释放资金预留
  - path: Tests/VanillaContractRowTests.cs
    type: A
    note: 合同行载荷契约 3 断言（列数同构 / 撤销载荷只挂 Posted / 键·搜索·排序可用）
  # ── B2 批次（2026-10-11 · 机构政策 4 类型净删 · 见 §六）──
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Governance/InstitutionPolicy.cs
    type: D
    note: 净删（含同文件的 InstitutionPolicyRules / StockTarget / StockTargetCandidate / PolicyCode / PolicyResult）
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Governance/InstitutionCapabilityRules.cs
    type: D
    note: 净删（能力徽章五级原因码；卡片已改单行只读）
  - path: Source/PersonalChronicle/Data/PawnEconomy/InstitutionPolicySaveData.cs
    type: D
    note: 净删（存档节点退役；旧档改由只读探针消费）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyInstitutionWorkSource.cs
    type: D
    note: 净删（车间岗位机会源整体建立在政策之上；裁决⑦冻结）
  - path: Tests/InstitutionPolicyTests.cs
    type: D
    note: 净删（被测类型已删）
  - path: Tests/InstitutionPolicyReadConsistencyTests.cs
    type: D
    note: 净删（被测类型已删）
  - path: Tests/StockTargetTests.cs
    type: D
    note: 净删（库存目标概念整体退役）
  - path: Source/PersonalChronicle/Data/PawnEconomy/LegacyPolicyProbe.cs
    type: A
    note: 旧档只读迁移探针（只声明 anchor；只在 LoadingVars 读；永不写回）
  - path: Tests/LegacyPolicyProbeTests.cs
    type: A
    note: 探针 4 断言（能读 / 缺节点不抛 / 无 anchor 行忽略 / 保存模式不写字段）
  - path: Tests/InstitutionPolicyRemovalLockdownTests.cs
    type: A
    note: 净删封印 4 断言（文件不存在 / 零调用级引用 / 节点只读且恰好一处 / 剥注释器有牙齿）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionPolicy.cs
    type: M
    note: 政策 7 查询 + Apply + 字段全删；新增 QueryServicePriceMinor（建筑字段投影）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorld.cs
    type: M
    note: 体检中心建筑 +ServicePricePerUnitMinor（Scribe）+ 服务价 gizmo（±1 银 / 归零，即时回报）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs
    type: M
    note: 政策节点改为 LoadingVars 只读探针（不再写入）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyUiKit.cs
    type: M
    note: 删 CapabilityColor（能力徽章唯一色映射）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs
    type: M
    note: 机构摘要精简为「类型·名·制度阶段」；删政策深链与库存兜底价
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyConsumption.cs
    type: M
    note: 食品/衣物改按件市价（删时段/未挂牌闸门）；柜台由机构类型选定
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyCare.cs
    type: M
    note: 服务价改读体检中心建筑字段（未挂牌即拒）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyShadowPlanning.cs
    type: M
    note: 机会源三源收敛为单源（车间岗位源已删）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLabels.cs
    type: M
    note: 删 CodeLabel(PolicyCode)
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWindowLayout.cs
    type: M
    note: 删政策字段卡网格与卡头降级纯函数（死几何）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyHostedTabs.cs
    type: M
    note: 删 Policy=7（唯一"非 Query 页签"例外）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/OverviewPanel.cs
    type: M
    note: 总览机构卡删报价读数、劳动卡删区工资报价块
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Production/EconomyProductionReadoutSource.cs
    type: M
    note: 自由库存目标恒 Unknown（概念退役，不发明 0）
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs
    type: M
    note: 注释订正（服务价已迁建筑字段）
  - path: Source/PersonalChronicle/Application/PawnEconomy/Care/CareService.cs
    type: M
    note: 名不副实的 InstitutionPolicyRules_ValidateAmount 改名
  - path: Tests/EconBatch1FixesTests.cs
    type: M
    note: 删能力徽章夹具（制度阶段夹具保留）
  - path: Tests/EconomyWindowPresentationTests.cs
    type: M
    note: 删政策网格/卡头降级用例；BatchKeys 去 Overview.Unpublished
  - path: Tests/ProductionAuthorisationTests.cs
    type: M
    note: 删"改工资清零服务价"用例（整份提交语义随存储退役）
  - path: Tests/WorkspaceSecondaryTabTests.cs
    type: M
    note: ctor 收参 + 卡片判据改读生产常量 + 删政策页渲染器用例
  - path: Tests/ItemValuationTests.cs
    type: M
    note: 调用点稽核改为"两处均只读物品市价、不得再传政策价/兜底价"
  - path: Languages/ChineseSimplified/Keyed/EconomyMode.xml
    type: M
    note: 删 7 政策态键（Mode.Config/Capability/InHours/OpenPolicy/OpenSettings）
  - path: Languages/English/Keyed/EconomyMode.xml
    type: M
    note: 同步删 7 键
# W batch (WORKGATE W1/W2)
  - path: Source/PersonalChronicle/WorkAuthority.cs
    type: A
    note: pure authority core + invalidator
  - path: Source/PersonalChronicle/Economy/PawnEconomy/WorkAuthorityService.cs
    type: A
    note: per-pawn authorised work types + self-invalidation + UI cache
  - path: Source/PersonalChronicle/Capture/Patch_PawnWorkSettings_CacheGivers.cs
    type: A
    note: the single gate (CacheWorkGiversInOrder postfix, both lists)
  - path: Source/PersonalChronicle/Capture/Patch_WorkTab_ContractLock.cs
    type: A
    note: work tab contract lock (manual patch: ECall target)
  - path: Tests/WorkAuthorityTests.cs
    type: A
    note: 7 assertions incl. seal
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
    note: gate switch + whitelist + visual whitelist section
  - path: Languages/ChineseSimplified/Keyed/Archive.xml
    type: M
    note: +11 keys
  - path: Languages/English/Keyed/Archive.xml
    type: M
    note: +11 keys

  # 2026-10-11 Codex 接续：W3/W4补登 + UI/COMP + S3/S4
  - path: docs/设计文档/功能模块/经济体系/实装落地/交接单_20261011_UI迁移与工作目录.md
    type: M
    note: 就地更新最新交付、S3/S4状态、实机边界
  - path: docs/设计文档/架构/变更标注/变更标注_CHG-20261011-WORKCAT.md
    type: M
    note: 旧S1/S2标注注明S3/S4由合同化标注续接
  - path: docs/changelog/v1.4.x_2026-10-10_经济系统默认启用.md
    type: M
    note: 就地补 Added/Changed/Removed/Breaking/Save/Migration/回滚
  - path: Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.Runtime.cs
    type: M
    note: 只读实际班次授权桥，共用运行判据
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
    note: 正式编译自动生成12:05 UTC时间戳
  - path: Source/PersonalChronicle/Capture/CapturePatchDiagnostics.cs
    type: M
    note: 精确签名目标诊断，工作闸门及 S3 目标
  - path: Source/PersonalChronicle/Capture/Patch_ItemFlowPlant.cs
    type: M
    note: 动作前捕获玩家标记，动作后按真实 job/植物计件
  - path: Source/PersonalChronicle/Capture/Patch_VanillaAnimalWork.cs
    type: A
    note: 狩猎宰杀死亡出口去重；成功驯服出口
  - path: Source/PersonalChronicle/Capture/Patch_VanillaHaulWork.cs
    type: A
    note: 原生 Toil 保持原动作，真实交付全量后每趟计件，读档恢复进度
  - path: Source/PersonalChronicle/Capture/Patch_WorkOrderTargets.cs
    type: A
    note: 精确 JobOnThing/HasJobOnThing，订单目标限定与独立授权例外
  - path: Source/PersonalChronicle/ChronicleMod.cs
    type: M
    note: 生产启动调用隔离安装 helper
  - path: Source/PersonalChronicle/Data/PawnEconomy/WorkOrderTripSaveData.cs
    type: A
    note: schema1 有界搬运证据 payload，玩家/地图/job/合同/目标/进度持久化
  - path: Source/PersonalChronicle/Data/PawnEconomy/WorkPriceSaveData.cs
    type: A
    note: schema1 有界价格 payload，未知/坏数据保留且禁新定价
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Employment/WorkPriceRules.cs
    type: A
    note: 两级价格解析、四段合同键、旧三段容量兼容
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_WorkPrices.cs
    type: A
    note: 劳务站13行两级定价 UI，严格银单位输入、新合同生效
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkOrders.cs
    type: A
    note: 不可变玩家意图、合同锚点时段校验、稳定证据键及 Sequence0 去重
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkOrderTrips.cs
    type: A
    note: Expose、pickup存证、同worker/map/job/contract恢复与真实交付累计
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkPrices.cs
    type: A
    note: Scribe 接线、劳务站工种默认与订单覆盖价写入口
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Planning/VanillaOrderCatalogue.cs
    type: M
    note: 8订单均接线；指定收获映射实际 PlantCutting 工种，5组
  - path: Source/PersonalChronicle/HarmonyStartup.cs
    type: A
    note: 按类隔离 Harmony 安装失败，已成功目标不重试
  - path: Source/PersonalChronicle/Settings/Dialog_WorkGateWhitelist.cs
    type: M
    note: 补登 W3；本轮窄屏滚动/截断悬停/Bills 子类排除
  - path: Source/PersonalChronicle/Settings/SettingsCatalog.cs
    type: M
    note: 补登 W3 活设置分节与3动作
  - path: Source/PersonalChronicle/Settings/SettingsDrawer.cs
    type: M
    note: 补登 W3 设置开关/白名单/日志路由
  - path: Source/PersonalChronicle/Settings/WorkGateDiagnostics.cs
    type: M
    note: 补登 W3 真实授权诊断日志
  - path: Tests/EconomyTableOpsTests.cs
    type: M
    note: 表格最小宽、溢出与详情摘要
  - path: Tests/HarmonyStartupTests.cs
    type: M
    note: 单类/部分目标失败隔离与完整生产 PatchAll 目标注册
  - path: Tests/VanillaOrderCaptureTests.cs
    type: M
    note: 植物动物搬运捕获负例及真实交付进度
  - path: Tests/VanillaOrderCatalogueTests.cs
    type: M
    note: 8订单/5工种/实际工种接线
  - path: Tests/WorkOrderTargetTests.cs
    type: A
    note: 工种与订单目标限制及保留强制工作
  - path: Tests/WorkOrderTripScribeTests.cs
    type: A
    note: 真实 Scribe 半途恢复、稳定事件不同tickReplay仅1份工资义务、旧坏未来与容量
  - path: Tests/WorkPriceInputTests.cs
    type: A
    note: 23个严格银单位输入用例
  - path: Tests/WorkPriceRulesTests.cs
    type: A
    note: 覆盖/默认/推荐价、独立锚点、旧键容量与合同价格冻结
  - path: Tests/WorkPriceScribeTests.cs
    type: A
    note: 真实 Scribe 两级往返、缺键、坏/未来保留及真实起草拒绝

  - path: docs/设计文档/架构/EXC-2026-001.md
    type: M
    note: 既有E2例外补S3及订单8目标，沿用到期2027-08-15，明确离线降级/失败/回滚与实机矩阵

  - path: Languages/English/Keyed/PawnEconomyPlanning.xml
    type: M
    note: 同步新增增量收入解释

  - path: Languages/ChineseSimplified/Keyed/PawnEconomyPlanning.xml
    type: M
    note: 新增FundedIncrementalOpportunity，明确逐件补储不是预算覆盖或到账

  - path: Tests/PawnShadowPlanningTests.cs
    type: M
    note: 22计件/真实合同键/优先级与保护回归，保留普通机会一次融资契约
---

# CHG-20261012-ECON-CONTRACTIZE · 经济合同化

## 一、用户三问题与裁决（原文口径）

| # | 用户要求 | 落地 |
|---|---|---|
| 1 | 公有化后不需要主动设置售价，直接采用原版物品市场价格 | 「设定食品售价」gizmo 与人工报价链整体移除；扣款 = **件数 × 单件 MarketValue**（经"每营养价 = 单件市价 ÷ 该件营养"派生，与既有唯一计价式 `NutritionPrice` 组合后数学等价，三处同式结构不变） |
| 2 | 工作目录→**工作需求**；表头换为**工作类型/工作岗位/总需求/已派发/就业率**；原版工作与 mod 工作统一数据表格；删一些文字 | 5 列声明 + 原版组行与 mod 岗位行**统一同一张表**（含就业率，分母 0/未接线显示 `—`）；删两处分区说明 + `VanillaHint` 长段 + 政策摘要行 |
| 3 | 移除全部的机构政策，和每份 ± 界面和代码 | 政策：UI 面全删（页签×5 / 摘要行 / 卡片按钮 / 整卡点击 / 导航动词 / 宿主分发）+ `PolicyPanel.cs` 文件删除 + **派工链不再依赖政策**（工资=基准常量 5.00 银/件）；每份±：档位族/枚举/窗口分支/行动词全删，签约上限回落固定常量 |

## 二、关键设计（供后续维护）

1. **Posted（已派发）状态**：弹窗草拟的合同先为 `Posted`（无持约人、到期即起草期），殖民者承接时**同一份合同**转 `Active`（`OccupiedBy`，条款/时段原样保留）；枚举值 4 追加在末尾，旧档 1/2/3 数字永不改写。
2. **派工链双路径并存 → 政策功能归零**：新增「Posted 合同机会段」与既有政策发布**并存**（任何时刻不出现"无人能派工"的断链态），随后删除政策三道门槛使旧路径不再依赖政策。**政策的删除分四步**（UI 入口 → UI 交互 → 页面文件 → 功能门槛），每步均可编译、可回滚。
3. **时段（合同级）**：`AllowsHour` 支持跨夜；旧档缺键 = 0/0 = **全天**；时段只控制"该工种对该人是否可用"，**不动计酬链**（避免"窗外完成静默漏付"）。
4. **推荐价**：产出物市价 × 50%，采样**有界 ≤8**（植物 `harvestedThingDef.BaseMarketValue × harvestYield`；矿脉 `mineableThing × mineableYield`）；不可得回落 5.00 银/件基准并**如实标注"基准价"**（不伪装成市价）。
5. **弹窗为"回调式"**：位于 Economy/PawnEconomy（UI 层），**不查业务数据、不写状态**；可派发/推荐价由宿主算好传入，写入经唯一入口 `TryDraftVanillaWorkContract`（校验全走纯函数，含国库预留饱和运算）。
6. **零 Widgets/GUI 纪律**：新弹窗全走 RUIF 原子；两个门禁脚本的 `$ui` 断言目标由 `PolicyPanel.cs` 改指 `Dialog_WorkContract.cs`。

## 三、验证证据（实测）

- 正式编译（`Assemblies`）：**0 错 0 警** ｜ BuildStamp 2026-10-11 06:35 UTC
- `dotnet test`：**1323 通过 / 0 失败 / 1 跳过**（相对上批 1325：+12 起草规则断言 −1 整删的"改价传播"测试）
- 部署：**OK**（DLL 14:35:22，2,162 KB）
- 每段另经**临时路径验证编译**（`.codebuddy/tmp/verify-build.ps1`，零冲突通道）：各段 exit=0；期间该通道真实拦下 1 个命名空间错误与 1 个 CS0219 警告，均已修

## 四、未做（如实登记）

| 项 | 说明 |
|---|---|
| **GATE-DELTA** | **NOT RUN** —— 本批交付时工作区存在**并行会话**（Archive 模块大重构，数百文件 M/D），范围外清单需专门整理；标注已按本批真实改动面写全，待并行改动落定后补跑 |
| **四席 Gate**（SAVE 先行 → UI‖ARCH → PERF/TEST） | **NOT RUN**（本批各段以"编译 + 测试 + 纯函数离线断言"自证；Gate 待派） |
| 数据/域类型净删 + **迁移只读探针** | 未做：`InstitutionPolicy`/`InstitutionPolicySaveData`/`InstitutionPolicyRules`/`InstitutionCapabilityRules` 仍在（旧档数据可加载、**无任何写入路径**）；用户裁决④要求"彻底删除 + 迁移"，落**只读探针**的正规做法待下一批 |
| ~60 个政策键 + 11 个政策测试文件 | 未清理（键与测试仍在，测的是已失效代码；不阻塞） |
| 服务价迁体检中心建筑字段（裁决⑥）+ 工坊岗位链冻结标注（裁决⑦） | 未做 |
| **实机验证** | **NOT RUN**（沙箱无游戏进程）—— 待验清单：重启后 ① 非劳务站柜台开窗不报错 ② 工作需求表 5 列与 mod 岗位行 ③ 点行弹窗→派发→殖民者承接 ④ 时段生效 ⑤ 储物按市价扣款 ⑥ 旧档加载（政策数据不报错） |

---

## 五、B1 小修段（2026-10-11 · 交接单 ★ 段的 P1-2 / P2-1 / P2-2 / P2-3）

> 本段是本 ChangeID 的**收口小修**（不新开 ChangeID）。B2（政策 4 类型净删）、B3 实机验收不在本段；本段的 Gate 结论见 §5.5（跑完追加）。

### 5.1 四项改动（问题 → 根因 → 修法）

| # | 问题（交接单口径） | 根因 | 修法（唯一实现点） |
|---|---|---|---|
| **P1-2** | Posted（已派发待承接）合同**不可见、无撤销** | `QueryVanillaContractRows` 只遍历"本图自由殖民者 × 活跃合同"（`QueryActiveContract`）⇒ 无持约人的 Posted 合同永远进不了表；且全链**没有任何**撤销入口（`Terminate` 只被退出判据与到期清理调用） | ① 读模型追加 **Posted 行**（排首；`ReadPostedContractsFor` + `IsVanillaOpportunity` 双过滤），携带撤销载荷；② 新写入口 `TryRevokePostedContract` → `EmploymentService.RevokePosted`（只接受 `State == Posted`，复用既有 `Terminate`）；③ 呈现层在**控件格**内画 RUIF Danger 按钮 + 本体确认框 |
| **P2-1** | 公有储存区 inspect 仍显示旧人工报价 /「未报价」 | `Patch_PublicStockpileInspect` 还在读 `PublicStockpileSettings.PricePerNutritionMinor` 并拼 `Storage.NoQuote` / `Storage.PriceValue`（CONTRACTIZE 段改了建筑侧 inspect，**遗漏此挂点**） | 改为与 `Building_EconomyStorage.GetInspectString` **同源同键** `PersonalChronicle.Storage.PublicMarketPriced`；两个孤儿键（中英双份）删除 |
| **P2-2** | 承接 Posted 的"先终止旧约、再试占用"在竞态失败时**净丢**既有合同与工种 | `TryOccupyPostedContract` 的顺序不可逆：`Terminate(current)` + `ReleaseWorkTypes` → `TryOccupy`（失败直接 return，不回滚） | 改**先校验后终止**：先只读复核"该合同仍是 Posted"（判据与 `EmploymentService.TryOccupy` 同源 —— 不引入时段/过期等不对称判据），通过后才动旧约；失败路径**不动**任何既有状态 |
| **P2-3** | 同合同 ID 仅改时段被判成幂等重放（静默保留旧时段） | `EmploymentService.Sign` 的 Replay 比较面缺 `WindowStartHour/WindowEndHour`（而合同 ID 不含时段） | 比较面补时段：同时段 = `Replay`；不同时段 = `CommandConflict`（条款绝不静默改写） |

### 5.2 关键设计（供后续维护）

1. **控件格是列声明的一部分**（LAND-010）：`EconomyCellControl.Action` 由读模型在列上声明，呈现层**不认列键、不认"末列"位置**；是否真画按钮由**行载荷** `EconomyRow.Revoke` 决定（只有 Posted 行有）。因此"操作列"改名或调序都不会让按钮静默消失。
2. **两型行共用唯一组装点** `Application/PawnEconomy/VanillaContractRow.Compose`（纯函数、零引擎依赖、可离线断言）：Posted / Active **逐格同构**（8 列 = 承接人/工种/已完成/总件数/已付/未付/每件/操作）；列声明处加了**契约自检**（列数 ≠ `VanillaContractRow.ColumnCount` 即 `Log.ErrorOnce`，不静默串列）。
3. **Posted 的已付/未付显示 `0.00` 而非 `—`**：这两列对 Posted 是**适用**的（确实还没产生任何计酬），`—` 会被读成"该列不适用"；与工作目录"未接线 ⇒ `—`"的诚实性口径不冲突（那里口径不存在，这里口径存在且为零）。
4. **撤销只针对 Posted**：已承接合同的"解约"属另一条线（交接单 §7-1 仍待裁决），本段**不做**；写入口对非 Posted 一律拒绝（`ContractNotActive` → 稳定 reason `NotPosted`），失败必给用户可见回报（F4，不静默）。
5. **撤销的连锁效果**：`SumOutstandingCommitments`（资金预留）与 `VanillaJobsCommitted`（件数预留）都按状态统计且**已含 Posted** ⇒ 撤销后两处预留自然回落，无需额外解除步骤。
6. **时段进详情**（`Contract.Detail.Window`）：Posted 行"要不要撤销"的判断依据；判定与 `WorkContract.AllowsHour` 同源（起 == 止 ⇒ 全天），跨夜原样显示（如 22:00–06:00）。
7. **`verify-pawn-economy.ps1` 口径修复**：该脚本的 `T("…")` 键存在性扫描漏了"自带前缀"的第三类文件 `Dialog_WorkContract.cs`（前缀 `PC_PEA.Dialog.WorkContract.`）⇒ 自 CONTRACTIZE 段起**恒 BLOCKED**（`Missing key: Title`）。改为**按文件给前缀映射**并保留逐文件归属，现已 PASS（371 双语键 / 67 处 `T()` 引用）。⚠ 这是"恒红的检查等于没有检查"的又一实证。

### 5.3 验证证据（实测 · 2026-10-11）

| 项 | 结果 | 命令 / 证据 |
|---|---|---|
| 临时验证编译（零冲突通道） | `exit=0` · **0 错 0 警** · rsp 645 文件 | `pwsh .codebuddy\tmp\b1-verify-build.ps1`（先重建 `compile.rsp` ⇒ 新文件必进清单，否则是典型假绿） |
| 正式编译（`Assemblies`） | **0 错 0 警** · BuildStamp `2026-10-11 07:42 UTC` | `pwsh _scripts/compile_with_sdk.ps1`（自动备份上一版 DLL `.bak_20261011154242`） |
| `dotnet test` | **1330 通过 / 0 失败 / 1 跳过**（上批 1324 ⇒ **+6** 新断言） | **先正式编译后测试**（`Tests` 引用 `Assemblies\*.dll`，顺序颠倒即假绿） |
| 新断言**反向对照**（修前必红） | 用**上一版已部署 DLL** 跑 `CHG20261012_SignReplayTreatsTheWorkWindowAsAClause` ⇒ **FAIL**（`Expected: CommandConflict / But was: Replay`）；换回本次 DLL ⇒ **PASS** | 对拷 `.bak_20261011154242` 到 `Tests\bin\Debug\net48` 后 `dotnet test --no-build --filter`（证明断言有牙齿，不是近恒真） |
| 经济域验证脚本 | **PASS**：12 Def 节点 / 5 机构 / 4 Job / 371 双语键 / 67 处 `T()` / RUIF 前端 | `pwsh _scripts/verify-pawn-economy.ps1` |
| 键增删自检 | +9 键（中英各恰好 1 处）· −2 键 ×2 语言 · 行尾与 BOM 未变 · XML 可解析 · zh/en 键集相等 | `.codebuddy\tmp\b1-loc-keys.ps1` / `b1-loc-keys2.ps1` |
| 孤儿键核对 | 本次新增/保留键**全部有引用**；既有孤儿 4 个（`Storage.SetPrice` / `Storage.PriceHelp` / `Workspace.Action.JobsDown` / `JobsUp`）系 CONTRACTIZE 段遗留，按交接单归 **B2** 的键清理批次 | 全仓 grep（`Source` / `Defs` / `Patches` / `_scripts`） |

### 5.4 未做 / 未验证（如实登记）

| 项 | 说明 |
|---|---|
| **实机验证** | **NOT RUN**（沙箱无游戏进程）。待验：派发一份合同 ⇒「工作合同」表出现「待承接」行（含时段详情）⇒ 点「撤销」⇒ 确认框 ⇒ 行消失、「已派发 / 就业率 / 未付」回落；被他人接走后点撤销 ⇒ 如实报「已被承接或已结束」（不静默）。 |
| 已承接合同的「解约」 | 不做（交接单 §7-1 待裁决）；本段只开放 Posted 撤销。 |
| B2（政策 4 类型净删 / ~60 键 / 11 政策测试 / 服务价迁体检中心 / 工坊链冻结标注） | 不做（专门批次）。 |
| `_scripts/verify-pawn-institution-mode.ps1` | **BLOCKED（既有工件钉）**：`Current DLL differs from tested DLL` —— 该脚本把游戏 DLL 哈希 / TRX 路径钉死在别的批次发布工件上（交接单 §5 已登记"不要随手重钉"），本段**未动**、如实登记。 |
| 子 agent Gate（UI+LOC / ARCH / PERF+TEST）与 GATE-DELTA | 见 §5.5（跑完追加）；DATA/SAVE 与 COMP 判 **N/A**（零 Def / 零 Scribe / 零 schema 变更；撤销复用既有 `Terminate`；无新第三方或 Harmony 面 —— `Patch_PublicStockpile` 仅改既有 Postfix 方法体）。 |

### 5.5 子 agent Gate 结论（2026-10-11 跑 · 记录落 `.codebuddy\audit\gate-records\`）

| Gate | 席 | 结论 | 记录 |
|---|---|---|---|
| UI + LOC | `pc-ui-loc-guard` | **UI = PASS / LOC = PASS**（3 条 P3，无 P0/P1；LAND-010 满足 / 控件格事件次序 / 入口不消失 / 色调唯一规则 / 失败不静默 / 9+2 键收口） | `GR-CHG-20261012-ECON-CONTRACTIZE-B1-UI-LOC.md` |
| ARCH | `pc-arch-gatekeeper` | **PASS**（1×P2 治理债 + 数项 P3）；**不触发 Architecture Review、无需新 EXC**；四问独立复核 = Harmony No / Hardcoding No / Cross-Mod No / Static-Mutable No；`EconomyCellControl` 判**不越界**（格语义 ≠ 控件类型） | `…-B1-ARCH.md` |
| PERF + TEST | `pc-perf-tester` | **PERF = PASS / TEST = PASS（Tier B）**；独立复跑：验证编译 `exit=0 errors=0 warnings=0`（645 文件）、`dotnet test` **1330 通过 / 0 失败 / 1 跳过**、部署件与测试 bin **同时间戳同哈希**；反向对照修前 **FAIL**（`Expected: CommandConflict / But was: Replay`）⇒ 修后 PASS（用后已还原） | `…-B1-PERF-TEST.md` |
| DATA + SAVE | `pc-data-steward`（**补跑首发**） | **DATA = PASS / SAVE = PASS WITH EXCEPTION**（例外唯一化 = 实机存读档矩阵未执行）；B1「零 schema 变化」**四重核证属实**；整批真实增量如实登记（`windowStart/windowEnd` 2 新键 + `ContractState.Posted=4` 名字串追加），旧档可加载论证链逐环给出 | `…-DATA-SAVE.md` |
| COMP | — | **N/A**：无新第三方 / DLC / Harmony 面（`Patch_PublicStockpile` 仅改既有 Postfix 方法体） | — |

> ⚠ **补跑原因（如实）**：交接单 ★ 段声称 "SAVE+DATA 已跑：`GR-CHG-…-DATA-SAVE`"，但 `gate-records\` 下**并无该记录**（总控只读核查 + 本批目录逐一核对）⇒ 本段以**补跑首发**做实，交接单该句已更正为引本记录。

**本轮顺带修掉的一处"恒绿检查"**（LOC 席发现）：`verify-pawn-economy.ps1` 的键扫描旧正则 `T\("([^".]+)"\)` **排除点号** ⇒ 含点键（含本段 9 新键）根本不入网、`W(...)` 不扫、储物键文件不在网内 = 扫描面形同虚设。修后：`[TW]\("([^"]+)"\)` + 逐文件前缀映射 + 并入**全部英文 Keyed** 集合 + 储物键集奇偶/在位 ⇒ 扫描面 **67 → 200** 处；并做删键反向对照（删 `PC_PEA.Workspace.Contract.Revoke` ⇒ FAIL 且点名 `referenced in EconomyVanillaWork.cs`；还原 ⇒ PASS）。双语键数口径澄清：**371 = 370 个 `PC_PEA.*` + 1 个 `PersonalChronicle.Storage.PublicMarketPriced`**。

**未处置的 P2/P3（登记，不阻断本段交付）**：

- **P2（ARCH）**：`EconomyVanillaWork.cs` **835 行** > ARC-013 800 行目标 ⇒ 记治理债；建议后续独立小批做 **partial 物理切片**（合同行读模型族 → `EconomyVanillaWork.ContractRows.cs`，零契约改动）。
- **P3（ARCH）**：① 行格数 < 列数时绘制循环静默截断（本表结构上不可达，其他列化表无此保证）⇒ 快照期加 `Cells.Length != Columns.Count ⇒ Log.ErrorOnce`；② 列序重排不被"数量"守卫捕获（建议钉"声明序 = 载荷序"）；③ 控件旗标丢失 ⇒ 按钮静默消失（建议同一守卫处追加"恰一列 `Control == Action`"）；④ `RowButtonTone` 注释补"撤销"；⑤ legacy 分支 `TrySignVanillaWork` 仍"先终止后校验"（当前被上游守卫覆盖、不可达；随 B2 处置政策路径时统一口径）。
- **P3（DATA/SAVE）**：① `windowStart/windowEnd` 的 `Scribe_Values.Look` 未显式传默认值（行为等价，`default(int)=0` 即全天）；② "旧档缺段 ⇒ 空账不抛"无直测（存量同型先例已有 6 处）；③ 未知 State 静默 fallback（存量模式）。
- **P3（LOC）**：① 极端连续拖宽可让后续列宽归零被跳过绘制（既有 S2b 布局边；拖窄方向不受影响）；② 跨段文案残留：`PersonalChronicle.Storage.PublicZoneHelp` 中英仍写"开启后需设置食品售价"，而该入口已移除 ⇒ **转 B2**。
- **既有孤儿键 4 个**（`Storage.SetPrice` / `Storage.PriceHelp` / `Workspace.Action.JobsDown` / `JobsUp`）⇒ 归 **B2** 键清理批次。

### 5.6 改动文件索引（front matter ↔ 正文对照 · GATE-DELTA 弱一致性）

> 逐条列出 front matter `files:` 声明的全部路径（GATE-DELTA 的"声明但正文未提及"提示按**路径或文件名**出现在正文判定，本节即为对照锚点，避免正文只用短名导致假提示）。

**前三段（合同化地基 / 工作链 / 表格窗口 / 储物按市价 / 政策退役）**

- `Source/PersonalChronicle/Domain/PawnEconomy/Employment/EmploymentContracts.cs`（`ContractState.Posted=4` + 时段字段 + `OccupiedBy`/`AllowsHour`）
- `Source/PersonalChronicle/Domain/PawnEconomy/Employment/WorkContractRules.cs`（起草规则纯函数）
- `Source/PersonalChronicle/Data/PawnEconomy/EmploymentSaveData.cs`（`windowStart`/`windowEnd` Scribe + `ReadPostedContractsFor`）
- `Source/PersonalChronicle/Application/PawnEconomy/Employment/EmploymentService.cs`（`SignRequest`/`Sign` 支持 Posted、`TryOccupy`、`ReadContract` 读回时段；B1 另加 `RevokePosted` 与 Replay 时段比较）
- `Source/PersonalChronicle/Application/PawnEconomy/EconomyView.cs`（`EconomyRow.Compose`；B1 另加 `Revoke`）
- `Source/PersonalChronicle/Application/PawnEconomy/WorkspaceView.cs`（`WorkspaceVerb` 删 `VanillaJobsDown/Up`）
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs`（起草入口 / 承接 / 退出判据 / 每份±族删除；B1 另加 Posted 行与 `TryOccupyPostedContract` 顺序修复）
- `Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyVanillaWorkSource.cs`（Posted 机会段、政策三门槛退役）
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs`（工作需求表 5 列 / mod 岗位行并表 / 政策页签与摘要行删除；B1 另加控件格「操作」列 + 列数契约自检）
- `Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.Table.cs`（行点击起草；B1 另加控件格渲染 + 撤销确认 + 快照期预量）
- `Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs`（每份±动作分支与政策承载删除）
- `Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyFoodCostSource.cs`（价源改市价派生）
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyStoredFoodPurchase.cs`（扣款 = 件数 × 单件市价）
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyStorage.cs`（`QuotedMealPrice`/`LowestFoodQuote` 零人工报价）
- `Source/PersonalChronicle/Economy/PawnEconomy/PublicStockpile.cs`（设价 gizmo 与方法删除）
- `Source/PersonalChronicle/Economy/PawnEconomy/Building_EconomyStorage.cs`（价格 gizmo 删除、inspect 同源键）
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionCards.cs`（机构卡政策按钮移除）
- `Tests/WorkContractRulesTests.cs`（12 条起草规则断言，含 `TerminatingAPostedContract_DoesNotThrow`）
- `Tests/PublicStockpileTests.cs`（删人工设价断言）
- `Languages/ChineseSimplified/Keyed/PawnEconomy.xml` / `Languages/English/Keyed/PawnEconomy.xml`（前三段 +5/+6/+16 键；B1 再 +9 键）
- `_scripts/compile.rsp`（PolicyPanel.cs 条目移除；构建脚本自动同步）
- `_scripts/verify-pawn-economy.ps1`（`$ui` 改指 Dialog_WorkContract；B1 再修键扫描口径 C-3/C-4）
- `_scripts/verify-pawn-institution-mode.ps1`（`$ui` 与源文件清单改指）
- `Source/PersonalChronicle/Economy/PawnEconomy/PolicyPanel.cs`（**D**：677 行政策页内容层整体删除）

**B1 小修段（2026-10-11）**

- `Source/PersonalChronicle/Application/PawnEconomy/VanillaContractRow.cs`（**A**：合同行载荷唯一组装点，纯函数）
- `Source/PersonalChronicle/Application/PawnEconomy/EconomyTable.cs`（`EconomyCellControl` 列语义）
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs`（`TryRevokePostedContract` 撤销写入口）
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyRowList.cs`（`RowButtonTone` 增 `Revoke`）
- `Source/PersonalChronicle/Capture/Patch_PublicStockpile.cs`（公有储存区 inspect 改同源键）
- `Languages/ChineseSimplified/Keyed/PawnEconomyStorage.xml` / `Languages/English/Keyed/PawnEconomyStorage.xml`（各删 2 孤儿键）
- `Tests/EconomyEmploymentTests.cs`（+3 断言：幂等含时段 / Posted 承接拒绝撤销 / 撤销释放预留）
- `Tests/VanillaContractRowTests.cs`（**A**：行载荷契约 3 断言）

---

## 六、B2 批次（2026-10-11 · 机构政策 4 类型净删）

> 用户指令原文：「继续 b2，全部做完再实测」。本段为**净删 + 消费面逐处换等价物**，不新增玩法机制；
实机验收统在 B2 之后一次性进行。

### 6.1 净删与逐处裁决

**删（4 类型）**：`InstitutionPolicy.cs`（含同文件的 `InstitutionPolicyRules` / `StockTarget` / `StockTargetCandidate` / `PolicyCode` / `PolicyResult`）·
`InstitutionCapabilityRules.cs` · `InstitutionPolicySaveData.cs` · `EconomyInstitutionWorkSource.cs`（车间岗位机会源）。
另删 3 个被测类型已消失的测试文件（`InstitutionPolicyTests` / `InstitutionPolicyReadConsistencyTests` / `StockTargetTests`）。

| # | 消费面 | 现状（删前） | 本批处置 | 依据 |
|---|---|---|---|---|
| ① | 车间岗位机会源 | 依政策（营业/工资/绑定区）发布机会 | **删文件**；机会源收敛为单源（原版工作） | 裁决⑦冻结 |
| ② | 车间岗位签约 | 政策三道门 + 修订号 | 非 vanilla 键一律稳定拒绝 `WorkshopChainFrozen`（不静默） | ⑦ + F4 |
| ③ | 车间合同退出 | `institutionOpen` 读政策 | 只留**制度阶段**闸门；岗位可用性判据（区/班次/工种）保留，存量合同不被政策清退 | 最小可逆 |
| ④ | 原版合同退出 | 同③ | 同上（政策恒空分支固化） | 同上 |
| ⑤ | 柜台购食 | 政策食品价 + 营业时段闸门 | 改**该件食物自身市价**（`ItemValuation.UnitPriceMinor`），删两闸门；柜台由机构类型选定（最低 anchor 确定性） | B1 裁决「按原版市价按件」 |
| ⑥ | 衣柜（衣物） | 政策衣物价兜底 | 只按该件市价；价值不可用 ⇒ 如实拒绝（不再有第二价源） | 同上 |
| ⑦ | 照护服务价 | 政策 `ServicePricePerUnitMinor` | **迁体检中心建筑字段**（`Building_EconomyInstitution.ServicePricePerUnitMinor` + gizmo + `QueryServicePriceMinor`） | 裁决⑥ |
| ⑧ | 库存目标读数 | 政策发布的品类目标 | 恒 `Unknown(StockTargetNotWired)`（概念整体退役；"未发布 ≠ 目标 0"，M5） | 裁决 + M5 |
| ⑨ | 机构卡 + 能力徽章 | 两行卡（时段 + 五级能力 chip） | **单行只读**（类型 · 建筑名 + 制度阶段色条）；删 `CapabilityColor` 与 `CapabilityRules.Evaluate` | 政策 UI 面退役 |
| ⑩ | 总览 KPI | 营业/工资报价/食品报价/服务报价 + 区工资报价块 | 删上述读数；机构数改数**建筑**；保留照护能力五行与合同/照护计数 | 概念退役 + 不发明数据 |
| ⑪ | 机构摘要 | 四段（配置/时段/能力/修订） | 精简为「类型 · 建筑名 · 制度阶段」；删 `Mode.OpenNotPayment` 提示行 | 同上 |
| ⑫ | 工作台头部/KPI | 营业芯片 + 时段文本；商店四枚报价 KPI | 删营业芯片与时段；商店 KPI 只留在售容量；体检中心服务价改读建筑字段 | 同上 |
| ⑬ | 链阻断读数 | 四闸（工资/待办/国库/动机） | 删两个**恒真或已无载体**的闸门（工资改由合同条款承载；消费定价恒可得）⇒ 保留待办/国库 | 恒真检查=装饰 |
| ⑭ | 死代码残迹 | `WorkspaceHostedRenderer.Policy` / `EconomyHostedTabs.Policy=7` / `EconomyWindowLayout` 政策网格 / `PolicySummary` / `CodeLabel` | 全删（含对应测试用例） | 净删收口 |

### 6.2 关键设计

1. **服务价在建筑上，不在存档账上**：与既有 `Building_EconomyStorage` 先例同型（`public` 字段 + `Scribe_Values` + 旧档缺键 = 0 = **未挂牌**）。
读取走 `QueryServicePriceMinor(mapId)`：体检中心里**值 > 0 且 anchor 键最小者**胜出（确定性单一投影，多台从不相加/取平均）；`null` ⇒ 调用方必须拒绝结算（不是免费）。
写路径 = 该建筑的 gizmo（±1 银 / 归零，`Messages` 当场回报；到界不动时不刷假消息）。
2. **旧档只读探针（`LegacyPolicyProbe`）**：只声明 `anchor` 一个字段、只在 `LoadSaveMode.LoadingVars` 分支被读、**保存分支根本不声明该节点** ⇒ 首存即退役；
读到条目记一行 `Log.Message`（旧档含政策数据是**预期事实**，故不告警）；缺节点/畸形内容一律不抛。
3. **三重封印防复活**：① `InstitutionPolicyRemovalLockdownTests` 断言 4 个类型文件不存在 + 生产源**零调用级引用**；
② 同一测试断言节点名只出现在探针常量处、且读取点**恰好一处并在 `LoadingVars` 守卫内**；③ 该测试自带"剥注释器仍有牙齿"反向对照（防止封印退化成恒真）。
4. **不迁移旧政策数值**（默认裁决）：工资/食品价/衣物价/库存目标随节点退役，服务价也不自动迁移 —— 迁移是一条新的写路径，须独立裁决；
玩家在体检中心重新挂牌即可（gizmo 即时可见）。

5. **回滚与数据窗口（GATE-SAVE/REL 2026-10-11 指出应显式写明）**：B2 之后**首次保存**时 `pawnEconomicPolicies` 
节点不再被声明 ⇒ 该节点从存档中**物理消失**（**不可逆**：沙箱内无法恢复）。
因此**唯一无损回滚路径 = 首存之前做一次存档备份**：备份档 + 重装上一版 DLL（`Assemblies\PersonalChronicle.dll.bak_*` 有逐次备份）可回到 B2 之前的状态。
⚠ 反向（旧 DLL 读 B2 后的新档）**未实测**：新档里政策节点已消失，旧 DLL 会把"无政策记录"读成"未配置"（旧代码把"未配置"与"停业"分开显示，不会崩），但本条**只能实机确认**。
旧政策数值**不迁移**是已定裁决；玩家重新挂牌即可（见上）。

### 6.3 验证证据（实测 · 2026-10-11）

| 项 | 结果 | 证据 |
|---|---|---|
| 临时验证编译 | `exit=0` · **0 错 0 警** · rsp **642** 文件（净删后少 3） | `.codebuddy\tmp\b1-verify-build.ps1` |
| 正式编译 | **0 错 0 警** · BuildStamp `2026-10-11 09:31 UTC` | `_scripts\compile_with_sdk.ps1` |
| 全量测试 | **1310 通过 / 0 失败 / 1 跳过**（B1 末 1331 ⇒ 净删 24 例：政策规则/读一致/库存目标/能力夹具/政策几何，+探针 4 +封印 4） | 先正式编译再 `dotnet test`（防旧 DLL 假绿） |
| 经济域验证脚本 | **PASS**：12 Def 节点 / 5 机构 / 4 Job / **326 双语键** / 173 处 `T()` / RUIF 前端 | `_scripts\verify-pawn-economy.ps1` |
| 语言键净删 | **−57 键 × 2 语言**（PawnEconomy 48 + EconomyMode 7 + Storage 2）· **+4 新键对**（服务价 Help/Clear/Changed、岗位「暂不可用」）· zh/en 键集断言相等 · 行尾/BOM 保持 | `.codebuddy\tmp\b2-key-cleanup.ps1`（含**零引用守卫**：有引用的键跳过不删，实际跳过 `Policy.Authorised`/`Policy.Revoke`） |
| 文案修正 | `PersonalChronicle.Storage.PublicZoneHelp` 中英同步改为"按物品自身市价计费"（B1 登记的跨段残留） | 同上 |
| 用户可见文本守卫**抓到过我的过度删除** | `Overview.Unpublished` 被删后 `EveryBatchKeyResolvesInBothLanguages` 变红 ⇒ 复查确认其唯一用户（区工资报价块）已随本批退役 ⇒ 从该守卫清单移除该键（并留注释说明） | `Tests\EconomyWindowPresentationTests.cs` |
| 部署 | DLL **2026-10-11 17:31:46** · 2,146.5 KB（B1 版 2,165.5 KB，体量随净删下降） | `_scripts\deploy-to-game.ps1` |

### 6.4 未做 / 未验证（如实登记）

| 项 | 说明 |
|---|---|
| **实机验证** | **NOT RUN**（沙箱无游戏进程）。待验：旧档（含政策节点）加载不抛 + 探针日志 + 首存后节点消失 / 体检中心服务价 gizmo 与照护结算 / 购食与衣物按件市价 / 工坊岗位行「暂不可用」且点不动 / 机构卡单行与总览变薄。 |
| 旧政策**数值迁移** | **不做**（默认裁决）。 |
| 既有孤儿键 ~50 个 | 多为早期批次遗留（`Policy.Tool.*` / `Care.*` / `Goal.*` / `Diag.*` / `Workspace.Vanilla*` / `Workspace.Col.Orders|Pay` / `Overview.Services|ProductionInputs|WorkshopCapacity`）——`Policy.Tool.*` 与 `Cap.*` 经前批取证属**动态解析**（`LocalizedId` 组），故保守保留；其余登记为**后续键清理批次**。 |
| 两个保守保留键 | `Policy.Authorised` / `Policy.Revoke`：零引用守卫按"末段字面量"判活（别处有 `"Revoke"`/`"Authorised"` 字面量）⇒ 保留（宁可留，不可误删可见文本）。 |
| 存量车间岗位合同 | **不清退**：由制度阶段 + 岗位判据（区/班次/工种）+ 件数/到期处置（与 B1 口径一致）。 |
| 工坊链冻结的"解冻" | 未做：本批只冻结 + 如实标注，恢复需另开批次（含政策替代物设计）。 |

### 6.5 子 agent Gate 结论（2026-10-11 跑 · 五席）

| Gate | 席 | 结论 | 关键发现 |
|---|---|---|---|
| UI + LOC | `pc-ui-loc-guard` | **UI = PASS / LOC = PASS WITH EXCEPTION** | 机构卡零特判 · 悬停配对 · gizmo 合规（原版 `Command_Action` 与既有先例同型）；**2×P2：① 工坊岗位行首格英文必截断且悬停不覆盖；② 5 处在用文案仍指引"发布/编辑机构政策"**（含设置页与柜台行动区）⇒ 见 §6.7 已修 |
| ARCH | `pc-arch-gatekeeper` | **PASS**（不触发 Review、无需新 EXC；四问 = No/No/No/No） | 跨层净删无残/无倒置；`LegacyPolicyProbe` 落位正确且**永不写回**证据链完整；`TrySignForOpportunity` 恒拒绝判定为**出口守卫**（非装饰死分支）；**P2：`EconomyCompositeWorkSource` 成死类 + `WorkshopChainFrozen` 无行为断言**（前者见 §6.7 已删，后者登记）；`EconomyVanillaWork.cs` 838 行（B1 债未偿，+3） |
| PERF + TEST | `pc-perf-tester` | **PERF = PASS / TEST = PASS（Tier B）** | 独立复跑：验证编译 `rsp=642 / exit=0 / 0 错 0 警`；`dotnet test` **1310 通过 / 0 失败 / 1 跳过**；本批 8 断言单跑 8/0/0；**三方 DLL 同哈希**（工程/部署/测试目录，17:31:46 版）；13 对语言文件键集 diff=0；`QueryInstitutions` 15 处调用点逐一核频均有界（上限 64） |
| DATA + SAVE | `pc-data-steward` | **DATA = PASS / SAVE = PASS WITH EXCEPTION** | 旧档"不抛 / 不写回 / 首存退役"三条**离线论证链**（引原版 `ScribeDeep`/`ScribeExtractor`/`ScribeLoader` 源码逐环）；删除节点**不推 `schemaVersion`**、以只读探针替代迁移的判例成立（边界：未来若要搬迁旧值须回到完整迁移链）；`Defs/` 零改动三重证实；例外 = 实机矩阵 NOT RUN + 发布侧 `Breaking/Removed` 登记待发版 |
| REL + DELTA + 收尾 | `pc-release-clerk` | **REL = PASS WITH EXCEPTION / DELTA = PASS（59 文件）/ 收尾 = PASS（6/1/0/0）** | DELTA 复跑原文 `GATE-DELTA: PASS — 标注覆盖 59 个改动文件，无遗漏/超范围/残留（提示 1 条）`；独立复算 fm 67 条 · 重复 0 · git 判定集 59 · phantom 8（全为 D 类净删）· 无漏声明；**欠账 3 项**：交接单续记 / 回滚说明（已补 §6.2.5）/ Changelog `Removed+Breaking`（转发版窗口） |
| COMP | — | **N/A** | 无组件契约变更（无新第三方/Harmony/DLC 面；与 B1 同判） |

### 6.6 改动文件索引（front matter ↔ 正文对照 · GATE-DELTA 弱一致性）

> 与本标注 front matter `files:` **同源**（生成时从 fm 自动提取）。GATE-DELTA 的"声明但正文未提及"提示按**路径或文件名**出现在正文判定 —— 本节即为对照锚点。

- `_scripts/compile.rsp`
- `_scripts/verify-pawn-economy.ps1`
- `_scripts/verify-pawn-institution-mode.ps1`
- `Languages/ChineseSimplified/Keyed/EconomyMode.xml`
- `Languages/ChineseSimplified/Keyed/PawnEconomy.xml`
- `Languages/ChineseSimplified/Keyed/PawnEconomyStorage.xml`
- `Languages/English/Keyed/EconomyMode.xml`
- `Languages/English/Keyed/PawnEconomy.xml`
- `Languages/English/Keyed/PawnEconomyStorage.xml`
- `Source/PersonalChronicle/Application/PawnEconomy/Care/CareService.cs`
- `Source/PersonalChronicle/Application/PawnEconomy/EconomyTable.cs`
- `Source/PersonalChronicle/Application/PawnEconomy/EconomyView.cs`
- `Source/PersonalChronicle/Application/PawnEconomy/Employment/EmploymentService.cs`
- `Source/PersonalChronicle/Application/PawnEconomy/VanillaContractRow.cs`
- `Source/PersonalChronicle/Application/PawnEconomy/WorkspaceView.cs`
- `Source/PersonalChronicle/Capture/Patch_PublicStockpile.cs`
- `Source/PersonalChronicle/Data/PawnEconomy/EmploymentSaveData.cs`
- `Source/PersonalChronicle/Data/PawnEconomy/InstitutionPolicySaveData.cs`
- `Source/PersonalChronicle/Data/PawnEconomy/LegacyPolicyProbe.cs`
- `Source/PersonalChronicle/Domain/PawnEconomy/Employment/EmploymentContracts.cs`
- `Source/PersonalChronicle/Domain/PawnEconomy/Employment/WorkContractRules.cs`
- `Source/PersonalChronicle/Domain/PawnEconomy/Governance/InstitutionCapabilityRules.cs`
- `Source/PersonalChronicle/Domain/PawnEconomy/Governance/InstitutionPolicy.cs`
- `Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/Building_EconomyStorage.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/Dialog_WorkContract.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyCare.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyConsumption.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyHostedTabs.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionCards.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionPolicy.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyLabels.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyRowList.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyShadowPlanning.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyStorage.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyStoredFoodPurchase.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyUiKit.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyWindowLayout.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorld.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/OverviewPanel.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyFoodCostSource.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyInstitutionWorkSource.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyVanillaWorkSource.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/PolicyPanel.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/Production/EconomyProductionReadoutSource.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/PublicStockpile.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs`
- `Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.Table.cs`
- `Tests/EconBatch1FixesTests.cs`
- `Tests/EconomyEmploymentTests.cs`
- `Tests/EconomyWindowPresentationTests.cs`
- `Tests/InstitutionPolicyReadConsistencyTests.cs`
- `Tests/InstitutionPolicyRemovalLockdownTests.cs`
- `Tests/InstitutionPolicyTests.cs`
- `Tests/ItemValuationTests.cs`
- `Tests/LegacyPolicyProbeTests.cs`
- `Tests/ProductionAuthorisationTests.cs`
- `Tests/PublicStockpileTests.cs`
- `Tests/StockTargetTests.cs`
- `Tests/VanillaContractRowTests.cs`
- `Tests/WorkContractRulesTests.cs`
- `Tests/WorkspaceSecondaryTabTests.cs`

### 6.7 P2 必修项处置（2026-10-11 收口 · 修复后复验）

| # | 来源 | 问题 | 处置 | 复验 |
|---|---|---|---|---|
| ① | GATE-LOC P2 | 5 处在用文案仍指引"发布/编辑机构政策"（`Workspace.Section.Positions` / `Workspace.NoActions` / `Mode.InstitutionNone` / `Mode.Step.Authority` / `Settings.Detail.EconomicInstitution`） | 中英同步改写值文本（**不动键名/不增删键**）：岗位区改"地图口径"、行动区去政策括注、机构空态改"建成后自动注册"、制度步骤删政策分句、设置描述删"政策可在准备期编辑" | `.codebuddy\tmp\b2-stale-copy.ps1`（10 处替换 + zh/en 键集相等断言 PASS） |
| ② | GATE-UI P2 | 工坊岗位行首格（`工作类型` 列默认 110）英文必截断，且行悬停文本**不含首格** ⇒ 截断后无处可查（LAND-008 缺口） | 首格文本抽为 `firstCell` 并**并入 `row.Text`**（悬停即全量文本）；`cells[0]` 复用同一变量（单一来源） | `EconomyWorkspaceQueries.cs` + 0 错 0 警复编译 |
| ③ | GATE-ARCH P2 | `EconomyCompositeWorkSource` 组合机会源已成死类（车间岗位源已删），且文档仍写"两源并列"，与裁决⑦封印相冲突 | 删除该类 + 改写来源文件头叙事（"不再有政策三道门"） | `EconomyVanillaWorkSource.cs`；引用数 0 → 删除后 0 错 0 警 |
| ④ | GATE-LOC P3-a | 新增 4 键中 3 键走 `TranslatedOrId`/`Formatted` 全键形态，**不在任何自动扫描内** | 4 键加入 `EveryBatchKeyResolvesInBothLanguages` 的 `BatchKeys` 清单 | `Tests\EconomyWindowPresentationTests.cs`；全量 1310 通过 |

**修后复验（2026-10-11 17:44）**：验证编译 `rsp=642 / exit=0 / 0 错 0 警` · 正式编译 `BuildStamp 09:44 UTC` · `dotnet test` **1310 通过 / 0 失败 / 1 跳过** · `verify-pawn-economy.ps1` PASS（326 双语键 / 173 处 `T()`）· 重新部署 DLL **2026-10-11 17:44:10**（2,146.0 KB）。

**登记未做（不阻断，转后续批次）**：`WorkshopChainFrozen` 行为断言（ARCH P2）· P3 若干（gizmo 到界置灰/现值 tooltip、体检 KPI「—」补原因、卡片索引未命中回退画 `row.Text`、封印守卫结构块级加固、`HostedPositionOf` 死方法与 8 处陈旧注释、`Tool./Cap.*` 等 ~50 孤儿键与 `Policy.Authorised/Revoke` 两键——LOC 席判**应删**，需命名空间感知守卫）· Changelog `Removed+Breaking` 条目（发版窗口必写）· 实机验收（全部 NOT RUN）。


（本段跑完后追加。）

## 七、W 批次（2026-10-11 · 工作合同闸门 · W1 内核已交付并部署）

> 用户裁决原文：「命令、订单、自发三类全部拦截」+「按默认开工，白名单建议需要一个可视化界面」。本段是**同一 ChangeID 的追加批次**（不新建 ChangeID）。W2（面板锁标）+ 六席 Gate + DELTA + 收尾**尚未跑**（见 §7.4）。

### 7.1 做法（一句话 + 落点）

保持原版工作面板**勾选状态一格不改**，只在 `Pawn_WorkSettings.CacheWorkGiversInOrder`（**唯一入表点**）重建之后，把"未被授权"的工种对应的 giver 从**两张表**（Normal + Emergency）里移除 ⇒ 命令类 / 订单类 / 自发类**一网打尽**；授权 = 合同 ∪ 车间班次托管 ∪ 生存白名单（并集），玩家可视化管理白名单。

**已取证的前提（反编译源）**：`wtsByPrio` 只收 `GetPriority > 0` 的工种；两张表都从它构建；派工侧 `JobGiver_Work:81` 只消费这两张表；`GetPriority`/`WorkIsActive` 同时被 `WidgetsWork`/`BillDialogUtility`/`GenConstruct`/`ITab_Pawn_Visitor`/`Ideo` 读取 ⇒ **绝不能碰**（封印测试钉死）。

### 7.2 改动文件（本段）

- **A** `Source/PersonalChronicle/WorkAuthority.cs` —— 纯判定核（三源并集 / 呈现优先级 / 白名单归一 / 默认白名单 `Firefighter`+`Doctor`）+ `WorkAuthorityInvalidator`（唯一失效实现，根层可调）
- **A** `Source/PersonalChronicle/Economy/PawnEconomy/WorkAuthorityService.cs` —— 每 pawn 授权集合（合同 ∪ 班次 ∪ 白名单）+ 自失效（见 7.3）+ 呈现用 `SourceFor`
- **A** `Source/PersonalChronicle/Capture/Patch_PawnWorkSettings_CacheGivers.cs` —— Harmony Postfix（唯一闸门）+ 补丁生效自检（`[PC] 工作合同闸门已生效`）+ fail-open
- **A** `Tests/WorkAuthorityTests.cs` —— 7 条断言：并集非交集 / 呈现优先级 / 空 `workType` 不纳管 / 白名单按稳定 ID 精确匹配 / 默认白名单口径 / 归一化 / **封印（禁止 patch `GetPriority`/`WorkIsActive`）+ 反向对照**
- **M** `Source/PersonalChronicle/ChronicleSettings.cs` —— 开关 `EnableWorkContractGate`（默认开）+ `WorkGateWhitelist`（Scribe）+ **白名单可视化分节**（逐工种一行：名称 · 状态 · 开关；含"恢复默认 / 全部纳入 / 全部放行"三个快捷；订单类标记由 Def 数据判定 `giverClass == typeof(WorkGiver_DoBill)`，非人工清单）
- **M** `Source/PersonalChronicle/Data/PawnEconomy/EmploymentSaveData.cs` —— `CollectActiveWorkTypesFor`（只读；`Active` 且未过期）
- **M** `Source/PersonalChronicle/Application/PawnEconomy/Employment/EmploymentService.cs` —— 同名委托
- **M** `Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs` —— `CollectWorkAuthorityWorkTypes`（组件写面，只读）
- **M** `Languages/ChineseSimplified/Keyed/Archive.xml` · `Languages/English/Keyed/Archive.xml` —— 各 +10 键（设置页与状态文案）

### 7.3 与方案的两处**如实偏差**

1. **失效挂点换了位置**：原计划挂"维护槽（250 tick）"，但 `EconomyRuntime.cs` 内该行两次精确匹配失败（未落盘改动，避免用模糊替换改业务文件）⇒ 改为**闸门自失效**：`AuthorizedWorkTypes` 被调用时（= 正在重建）若距上次全量失效 ≥250 tick，则让所有殖民者重建一次。效果等价（合同/班次/设置变化 ≤250 tick 生效），代价有界（每 250 tick 一次全局标记），并且**新增写路径不会漏挂**。
2. **设置页不显示"车间托管"状态**：车间托管是**地图级**读数（`AutoAssignService.ManagedWorkTypeDefNames(map)` 在 Automation 层）—— 从设置页（根层）读它会新增 `RootSettings→Automation` 依赖边，故本页只显示「白名单 / 需合同授权」两态 + 订单类标记；完整来源（含车间托管）走 `WorkAuthorityService.SourceFor`（Economy 层）。**豁免本身照常生效**（判定在服务层）。

### 7.4 验证与未做

| 项 | 状态 |
|---|---|
| 验证编译 | `exit=0` · **0 错 0 警** |
| 正式编译 | **0 错 0 警** · BuildStamp 2026-10-11 10:43 UTC |
| 全量测试 | **1317 通过 / 0 失败 / 1 跳过**（上批 1310 ⇒ **+7** 为本段新断言） |
| 部署 | DLL **2026-10-11 18:43:37** |
| **W2：原版工作面板「合同锁」标识 + 悬停说明** | **未做**（用户已点名的下一步；不做则面板"勾了不动"缺明示） |
| 六席 Gate / GATE-DELTA / AI-016 收尾 | **未跑**（本段为 W1 内核；标注 front matter 的文件清单与 DELTA 待 W2 一并补齐） |
| 实机验证 | **NOT RUN** |
| 登记风险 | ① `Notify_UseWorkPrioritiesChanged` 在自失效路径被调用的**间隔**若被第三方 mod 放大，需复核；② 与工作链 mod（WorkTab / Common Sense / PickUpAndHaul 等）的 Harmony 顺序未复核（COMP 席待跑）；③ 玩家手动改格位目前**不触发退让**（台账为方案中的下一件；当前语义 = 授权优先，勾选只表示"可被雇佣"） |

## 八、W2（2026-10-11 · 工作面板「合同锁」标识 + PatchAll 自检冲突的处置）

### 8.1 做了什么

- **格位压暗 + 原版警示叠层**：钉 `WidgetsWork.DrawWorkBoxFor(x, y, p, wType, …)`（实测方块 `25×25`，与调用点同坐标）**只用视觉**；
  **只标"已勾选却无授权"**的格子（未勾选的格子本来没人干，加锁是噪音）。
- **悬停补一行原因**：钉 `WidgetsWork.TipForPawnWorker`，追加 `PersonalChronicle.WorkGate.Lock`（中英各 1 键）——
  与绘制**同一判据**（`ShouldLock`），避免两处口径分叉。
- **UI 单格缓存**：`WorkAuthorityService.IsAuthorizedNow`（同 tick 同 pawn 复用一次计算）——否则每帧每格都会新建一个 `HashSet`（OnGUI 分配）。

### 8.2 一个必须记档的平台冲突（及处置）

**现象**：首版把两个补丁写成 `[HarmonyPatch]` 自动扫，结果工程的离线自检 `HarmonyStartupTests`（全量 PatchAll）直接变红：
`HarmonyException … RimWorld.WidgetsWork::DrawWorkBoxFor … SecurityException: ECall 方法必须打包到系统模块中`——
目标方法体含 `GUI.DrawTexture` 一族 **ECall**，在**无 Unity 的测试宿主**里无法准备补丁。

**处置（两条都不牺牲）**：改为**运行期手动挂载**（`Patch_WorkTab_ContractLock_Draw.ApplyOnce()`：各自 try/catch + 日志，幂等），
由闸门补丁首次生效时触发（闸门关着时不需要锁标）。于是：离线 PatchAll 自检恢复全绿（**1328 通过 / 0 失败**），
游戏内照常生效；挂载失败只退化"不可见"，不影响闸门本身（fail-open）。

### 8.3 验证

| 项 | 结果 |
|---|---|
| 验证编译 / 正式编译 | **0 错 0 警**（BuildStamp 2026-10-11 10:51 UTC） |
| 全量测试 | **1328 通过 / 0 失败 / 1 跳过**（含 `HarmonyStartupTests` 全量 PatchAll 自检恢复绿） |
| 部署 | `deploy-to-game.ps1`（见交接单 ★ 段时间戳） |

### 8.4 未做（如实）

- **六席 Gate / GATE-DELTA / AI-016 收尾**：本段仍未跑（下一轮集中跑；evidence 已在本标注 §七/§八 备齐）。
- **实机验证 NOT RUN**：锁标观感、悬停文案、闸门实操三件都要实机确认。
- **玩家手改格位 ⇒ 退让台账**：经复核**本设计不需要**——闸门对格位**只读不写**（不像车间班次那样会覆盖玩家手改），
  因此不存在"PC 拥有该格"的台账语义；玩家若要临时放行，用设置页的「全部放行」或把工种加进白名单。
  （**逐 pawn × 工种的强制覆盖**登记为后续可选件。）

*本段（W1 工作合同闸门）由 AI 助手 · CodeBuddy（模型：DeepSeek-V4.1-Flash）于 2026-10-11 追加；"已交付"仅指编译/测试/部署实测通过，未跑的门禁与实机已在 §7.4 如实标注。*

*本段（B1 小修）由 AI 助手 · CodeBuddy（模型：DeepSeek-V4.1-Flash）于 2026-10-11 追加；所有"已完成"结论均以编译 / 测试 / 脚本实测为准，未执行项已在 §5.4 明确标注 NOT RUN。*

*（原 §四 签名）本标注由 CodeBuddy / DeepSeek-V4.1-Flash 于 2026-10-12 生成；"已完成"结论均以编译/测试/部署实测为准，未执行项已在 §四 明确标注 NOT RUN。*

## 九、2026-10-11 Codex 接续交付（本段为当前状态，前段验证属于历史 DLL）

由 AI 助手 Codex 实施与总控；layout、compat、workchain、perf_test 分别承载 UI/LOC、COMP、ARCH/DATA/SAVE、PERF/TEST。工程 orchestrator 技能要求真实并行；本轮四个席位可承载多个角色，未声称六个独立审核人。REL/DELTA 另行收尾。用户确认两级价格，计件单位授权由实现者决定。

| 范围 | 具体 diff 与结果 |
|---|---|
| UI | EconomyTable 列声明 MinWidth；合同行8列各自保留最小宽、窄屏横向滚动，窗口宽从数据模型测量；详情一行加全文悬停；撤销控件行高含内边距；白名单自适应屏幕/滚动/缓存标签，DoBill 子类不纳入 |
| 工作授权 | Runtime 在经济准备闸前每250tick刷新缓存；排除未来签约/过期/完成/时段外/无活锚点合同；班次直接共用实际 roster 核，覆盖固定岗位/轮班/设备；三源并集与UI提示同源，不改玩家工作优先级 |
| COMP | ChronicleMod 按补丁类隔离失败，部分已安装目标不重试；工作面板含 ECall 的目标仍手动 ApplyOnce；诊断精确签名；失败明确日志且后续类别继续安装 |
| S3 | 原版8订单完成信号均接线；植物完成前保存玩家标记而非标记消失猜成功；狩猎/宰杀按真实死亡与actor job匹配且同目标去重；驯服仅成功；搬运仅玩家标记且原生交付数量完整/手持空，普通自动搬运无拾取证据不计件 |
| 搬运存档 | WorkOrderTripSaveData schema1 保存worker/map/job/contract/target与交付进度；缓存弱表仅加速，恢复依靠存档证据；保存从实际地图pawn查询裁剪，不依赖滞后索引；完成事件 Sequence 固定0，Tick仍记录完成时刻，跨tick重放不加义务 |
| S4 | 每劳务站独立配置工种默认价与订单覆盖价；覆盖>默认>推荐；0/清除继承，上限1000银/单位，仅新起草合同采用配置；合同已发布/承接单价冻结；显式合同四段key限制订单，旧三段通用合同保留、容量保守扣减 |
| 计件 | 开采/伐木/割除/收获每个标记目标完成1件；狩猎/宰杀每只；驯服每成功1次；搬运每完整送达1趟，可含一叠。Bills保持既有生产交付链；收获归PlantCutting实际WorkGiver，不计普通农田自动采收 |
| 价格坏档 | WorkPriceSaveData schema1；未知/坏payload原串保留并拒写；新quote=0不可发布，起草PriceUnavailable，不偷偷回落推荐价；已发布合同继续按冻结条款 |
| 本地化 | 新增14对键；Archive含订单和授权语义同步；验证脚本给Dialog_WorkPrices独立前缀映射，隔离删键集合负测必红 |

旧自动fallback的通用三段合同继续使用工种默认价，不自动拆成指定订单覆盖价；要使用覆盖价需在劳务站订单行发布新合同。原版标记在模组接管前创建的历史意图不会被追认；世界事件、玩家取消、脚本静默移除不计成功。工作机会仍由现有需求规划选择，发布不等于即时保证承接；本机 Player.log 只有 WORKGATE、无 OCCUPY，用户尚未实测，未声称唯一修复了“派了没人接”。

### 9.1 最终验证及证据边界

- `_scripts/compile_with_sdk.ps1` 重新收集全部源码、写 BuildStamp 2026-10-11 12:05 UTC，0编译诊断；日志 `.codebuddy/tmp/codex-handoff-build-final.txt`。首次尝试因旧DLL被其他工具映射失败；旧文件原样重命名 `.bak_202610112006_locked` 后重编，未终止用户工具。
- 正式 DLL SHA256 `40AF3A99153218F33C9F04514FFC28DB7AE9B226CF9E0A0D360606371DE87BAB`；源码编译后全量测试 `.codebuddy/tmp/codex-handoff-tests/handoff-final.trx` = 1429通过/0失败/1跳过，总1430。跳过内容度量用例不算通过。
- `verify-pawn-economy.ps1` PASS：12 Def节点、5机构、4 Jobs、精确ThinkTree XPath、编译类、340双语键、187处T/W引用、RUIF。隔离负测 `.codebuddy/tmp/codex-handoff-verify-negative.txt` 明确 Missing WorkPrices.Title、exit1；真实语言文件未改。
- 真实Scribe半途搬运→加载→完成只生成1份工资义务→再存读→不同tick重放拒绝；覆盖数据/服务，不冒充原生游戏搬运与账户实际支付端到端。Harmony离线宿主垫片同理不等于GUI ECall实机运行。
- 有界探针/过滤测试与本轮Gate记录见 `.codebuddy/audit/gate-records/`；以更新后记录和冻结DLL绑定为准。UI=Tier C静态/几何，PERF/TEST=Tier B离线，GameVerified/Steam Released=NOT RUN。

### 9.2 迁移、部署与回滚

新增Scribe节点 `pawnEconomicWorkPrices` 与 `pawnEconomicWorkOrderTrips` 均schema1；旧档缺节点为空，不补造搬运证据。未知/损坏payload保留拒新写。B2首存会丢 `pawnEconomicPolicies`，旧政策数值不迁移，体检中心重新挂牌服务价。首次使用新DLL保存前备份存档；旧DLL读取本轮新档未实测。

部署仅生产DLL及PawnEconomy/Archive中英4语言文件，目标 `RimWorld/Mods/殖民地综合管理系统`；packageId必须为 `colony.integratedmanagement`，逐文件备份和SHA比对另落 `.codebuddy/tmp/codex-handoff-deployment.json`，完成后方可称Deployed。无Steam发布。默认工作闸门开启、旧设置缺键也开启、全局影响所有存档；即时回退闸门可在设置页关闭；完整无损版本回滚需匹配首存前存档备份和部署备份DLL/语言。并非“只关闸门”即可恢复退役政策或价格/搬运schema。

实机验收仍需：中英/窄屏/8列横向滚动/撤销，白名单与三源授权；各订单发布→承接→窗口内完成→支付，小时边界/到期失效；搬运半途保存加载后完整送达一次、取消/部分丢失/自动搬运不付、同事件重放不多付；旧档加载、新节点再次保存、未知payload诊断。

### 9.3 累计文件索引（front matter 与正文互含，不代表100个文件均在本轮新增）

| 文件 | 类型 | 说明 |
|---|---|---|
| Source/PersonalChronicle/Domain/PawnEconomy/Employment/EmploymentContracts.cs | M | ContractState.Posted=4（旧档 1/2/3 数字不动）；时段字段 + OccupiedBy + AllowsHour（跨夜）；Posted 允许空持约人 |
| Source/PersonalChronicle/Domain/PawnEconomy/Employment/WorkContractRules.cs | A | 起草规则纯函数（推荐价/钳制/StepHour/ValidateDraft 稳定 ID/饱和乘/有界采样契约） |
| Source/PersonalChronicle/Data/PawnEconomy/EmploymentSaveData.cs | M | windowStart/windowEnd Scribe（旧档缺键=全天）；ReadPostedContractsFor；过期裁剪与工资预留扩展到 Posted |
| Source/PersonalChronicle/Application/PawnEconomy/Employment/EmploymentService.cs | M | SignRequest(+时段/+State)；Sign 支持 Posted；TryOccupy（承接）；ReadContract 读回时段；ReadPostedContractsFor |
| Source/PersonalChronicle/Application/PawnEconomy/EconomyView.cs | M | EconomyRow.Compose（行点击起草载荷；与 Detail 展开互斥） |
| Source/PersonalChronicle/Application/PawnEconomy/WorkspaceView.cs | M | WorkspaceVerb 删 VanillaJobsDown/Up（每份±退役） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs | M | TryDraftVanillaWorkContract（唯一起草入口）；FindPostedContractFor/TryOccupyPostedContract（承接+FA-11 复核）；RecommendWageMinor（产出市价×50%，有界≤8）；退出判据"政策缺失≠机构关闭"；合同化后不再依赖政策（工资=基准常量）；每份±档位族删除 |
| Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyVanillaWorkSource.cs | M | Posted 合同机会段（与政策并存）；政策三门槛退役（工资=基准常量） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkspaceQueries.cs | M | 工作需求表 5 列（工作类型/工作岗位/总需求/已派发/就业率）；mod 岗位行统一进表；每份±行动词与长说明段删除；政策页签（5 处）与政策摘要行删除 |
| Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.Table.cs | M | 行点击 → 起草回调（快照期绑定）；OpenWorkContractDialog（解析载荷→算可派发/推荐价→开弹窗→唯一写入口→重快照） |
| Source/PersonalChronicle/Economy/PawnEconomy/Window_InstitutionWorkspace.cs | M | 每份±动作分支删除；政策承载全删（HostedPolicy 字段/切页聚焦/渲染器分发/Policy 导航动词） |
| Source/PersonalChronicle/Economy/PawnEconomy/Dialog_WorkContract.cs | A | 工作合同弹窗（数量/施工时段/单件薪资，推荐价预填，失败留窗显示稳定原因；零星 Widgets/GUI） |
| Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyFoodCostSource.cs | M | 价源改储物市价派生（政策不再参与）；类注释更新（旧裁决"不做市价替身"被本批用户裁决取代） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyStoredFoodPurchase.cs | M | 扣款=件数×单件市价（经每营养价派生，数学等价）；PerNutritionMarketMinor helper |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyStorage.cs | M | QuotedMealPrice 按该件自身市价；LowestFoodQuote 改为扫公有可食物取 min(单件市价÷营养)，零人工报价 |
| Source/PersonalChronicle/Economy/PawnEconomy/PublicStockpile.cs | M | 「设定食品售价」gizmo 与 SetPublicStockpilePrice 删除 |
| Source/PersonalChronicle/Economy/PawnEconomy/Building_EconomyStorage.cs | M | 价格 gizmo/FoodPriceCommand 删除；inspect 改「按物品市价计费」 |
| Source/PersonalChronicle/Economy/PawnEconomy/PolicyPanel.cs | D | 政策页内容层整体删除（677 行）；UI 面归零后零引用 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionCards.cs | M | 机构卡「政策」按钮与整卡点击移除（卡片改纯只读）；Policy 动词退化为行种标记 |
| Tests/WorkContractRulesTests.cs | A | 起草规则 12 条离线断言（推荐价/回落/时段半开/跨夜/0-0 全天/校验五拒/饱和乘/Posted 承接不改条款） |
| Tests/PublicStockpileTests.cs | M | 删人工设价断言与"改价传播"测试（功能已整体移除） |
| Languages/ChineseSimplified/Keyed/PawnEconomy.xml | M | +5 列表键 +6 弹窗键 +16 弹窗/原因键 +市价计费说明键 |
| Languages/English/Keyed/PawnEconomy.xml | M | 同步（术语 contractor/posted 一致） |
| _scripts/compile.rsp | M | PolicyPanel.cs 条目移除（rsp 注释占位） |
| _scripts/verify-pawn-economy.ps1 | M | $ui 断言目标改指 Dialog_WorkContract.cs（政策页已删） |
| _scripts/verify-pawn-institution-mode.ps1 | M | $ui 与源文件清单两处改指 |
| Source/PersonalChronicle/Application/PawnEconomy/VanillaContractRow.cs | A | 合同行载荷唯一组装点（纯函数：两型同列数 / 撤销载荷只挂 Posted / 控件格恒空） |
| Source/PersonalChronicle/Application/PawnEconomy/EconomyTable.cs | M | EconomyCellControl 列语义（Action=控件格）—— 呈现层不认列键/末列位置（LAND-010） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs | M | TryRevokePostedContract（撤销唯一写入口；只接受 Posted；失败给稳定 reason） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyRowList.cs | M | RowButtonTone 增 Revoke ⇒ 破坏性动词统一危险色（色调规则唯一来源） |
| Source/PersonalChronicle/Capture/Patch_PublicStockpile.cs | M | 公有储存区 inspect 改同源键 PublicMarketPriced（删旧人工报价/未报价分支） |
| Languages/ChineseSimplified/Keyed/PawnEconomyStorage.xml | M | 删 NoQuote / PriceValue 两键（人工报价链已整体移除，LOC-001 无孤儿） |
| Languages/English/Keyed/PawnEconomyStorage.xml | M | 同步删除两键 |
| Tests/EconomyEmploymentTests.cs | M | +3 断言：幂等比较含时段 / Posted 承接就地改状态且拒绝撤销 / 撤销释放资金预留 |
| Tests/VanillaContractRowTests.cs | A | 合同行载荷契约 3 断言（列数同构 / 撤销载荷只挂 Posted / 键·搜索·排序可用） |
| Source/PersonalChronicle/Domain/PawnEconomy/Governance/InstitutionPolicy.cs | D | 净删（含同文件的 InstitutionPolicyRules / StockTarget / StockTargetCandidate / PolicyCode / PolicyResult） |
| Source/PersonalChronicle/Domain/PawnEconomy/Governance/InstitutionCapabilityRules.cs | D | 净删（能力徽章五级原因码；卡片已改单行只读） |
| Source/PersonalChronicle/Data/PawnEconomy/InstitutionPolicySaveData.cs | D | 净删（存档节点退役；旧档改由只读探针消费） |
| Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyInstitutionWorkSource.cs | D | 净删（车间岗位机会源整体建立在政策之上；裁决⑦冻结） |
| Tests/InstitutionPolicyTests.cs | D | 净删（被测类型已删） |
| Tests/InstitutionPolicyReadConsistencyTests.cs | D | 净删（被测类型已删） |
| Tests/StockTargetTests.cs | D | 净删（库存目标概念整体退役） |
| Source/PersonalChronicle/Data/PawnEconomy/LegacyPolicyProbe.cs | A | 旧档只读迁移探针（只声明 anchor；只在 LoadingVars 读；永不写回） |
| Tests/LegacyPolicyProbeTests.cs | A | 探针 4 断言（能读 / 缺节点不抛 / 无 anchor 行忽略 / 保存模式不写字段） |
| Tests/InstitutionPolicyRemovalLockdownTests.cs | A | 净删封印 4 断言（文件不存在 / 零调用级引用 / 节点只读且恰好一处 / 剥注释器有牙齿） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionPolicy.cs | M | 政策 7 查询 + Apply + 字段全删；新增 QueryServicePriceMinor（建筑字段投影） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorld.cs | M | 体检中心建筑 +ServicePricePerUnitMinor（Scribe）+ 服务价 gizmo（±1 银 / 归零，即时回报） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs | M | 政策节点改为 LoadingVars 只读探针（不再写入） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyUiKit.cs | M | 删 CapabilityColor（能力徽章唯一色映射） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs | M | 机构摘要精简为「类型·名·制度阶段」；删政策深链与库存兜底价 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyConsumption.cs | M | 食品/衣物改按件市价（删时段/未挂牌闸门）；柜台由机构类型选定 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyCare.cs | M | 服务价改读体检中心建筑字段（未挂牌即拒） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyShadowPlanning.cs | M | 机会源三源收敛为单源（车间岗位源已删） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyLabels.cs | M | 删 CodeLabel(PolicyCode) |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyWindowLayout.cs | M | 删政策字段卡网格与卡头降级纯函数（死几何） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyHostedTabs.cs | M | 删 Policy=7（唯一"非 Query 页签"例外） |
| Source/PersonalChronicle/Economy/PawnEconomy/OverviewPanel.cs | M | 总览机构卡删报价读数、劳动卡删区工资报价块 |
| Source/PersonalChronicle/Economy/PawnEconomy/Production/EconomyProductionReadoutSource.cs | M | 自由库存目标恒 Unknown（概念退役，不发明 0） |
| Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs | M | 注释订正（服务价已迁建筑字段） |
| Source/PersonalChronicle/Application/PawnEconomy/Care/CareService.cs | M | 名不副实的 InstitutionPolicyRules_ValidateAmount 改名 |
| Tests/EconBatch1FixesTests.cs | M | 删能力徽章夹具（制度阶段夹具保留） |
| Tests/EconomyWindowPresentationTests.cs | M | 删政策网格/卡头降级用例；BatchKeys 去 Overview.Unpublished |
| Tests/ProductionAuthorisationTests.cs | M | 删"改工资清零服务价"用例（整份提交语义随存储退役） |
| Tests/WorkspaceSecondaryTabTests.cs | M | ctor 收参 + 卡片判据改读生产常量 + 删政策页渲染器用例 |
| Tests/ItemValuationTests.cs | M | 调用点稽核改为"两处均只读物品市价、不得再传政策价/兜底价" |
| Languages/ChineseSimplified/Keyed/EconomyMode.xml | M | 删 7 政策态键（Mode.Config/Capability/InHours/OpenPolicy/OpenSettings） |
| Languages/English/Keyed/EconomyMode.xml | M | 同步删 7 键 |
| Source/PersonalChronicle/WorkAuthority.cs | A | pure authority core + invalidator |
| Source/PersonalChronicle/Economy/PawnEconomy/WorkAuthorityService.cs | A | per-pawn authorised work types + self-invalidation + UI cache |
| Source/PersonalChronicle/Capture/Patch_PawnWorkSettings_CacheGivers.cs | A | the single gate (CacheWorkGiversInOrder postfix, both lists) |
| Source/PersonalChronicle/Capture/Patch_WorkTab_ContractLock.cs | A | work tab contract lock (manual patch: ECall target) |
| Tests/WorkAuthorityTests.cs | A | 7 assertions incl. seal |
| Source/PersonalChronicle/ChronicleSettings.cs | M | gate switch + whitelist + visual whitelist section |
| Languages/ChineseSimplified/Keyed/Archive.xml | M | +11 keys |
| Languages/English/Keyed/Archive.xml | M | +11 keys |
| docs/设计文档/功能模块/经济体系/实装落地/交接单_20261011_UI迁移与工作目录.md | M | 就地更新最新交付、S3/S4状态、实机边界 |
| docs/设计文档/架构/变更标注/变更标注_CHG-20261011-WORKCAT.md | M | 旧S1/S2标注注明S3/S4由合同化标注续接 |
| docs/changelog/v1.4.x_2026-10-10_经济系统默认启用.md | M | 就地补 Added/Changed/Removed/Breaking/Save/Migration/回滚 |
| Source/PersonalChronicle/Automation/MapComponent_WorkshopManager.Runtime.cs | M | 只读实际班次授权桥，共用运行判据 |
| Source/PersonalChronicle/BuildStamp.g.cs | M | 正式编译自动生成12:05 UTC时间戳 |
| Source/PersonalChronicle/Capture/CapturePatchDiagnostics.cs | M | 精确签名目标诊断，工作闸门及 S3 目标 |
| Source/PersonalChronicle/Capture/Patch_ItemFlowPlant.cs | M | 动作前捕获玩家标记，动作后按真实 job/植物计件 |
| Source/PersonalChronicle/Capture/Patch_VanillaAnimalWork.cs | A | 狩猎宰杀死亡出口去重；成功驯服出口 |
| Source/PersonalChronicle/Capture/Patch_VanillaHaulWork.cs | A | 原生 Toil 保持原动作，真实交付全量后每趟计件，读档恢复进度 |
| Source/PersonalChronicle/Capture/Patch_WorkOrderTargets.cs | A | 精确 JobOnThing/HasJobOnThing，订单目标限定与独立授权例外 |
| Source/PersonalChronicle/ChronicleMod.cs | M | 生产启动调用隔离安装 helper |
| Source/PersonalChronicle/Data/PawnEconomy/WorkOrderTripSaveData.cs | A | schema1 有界搬运证据 payload，玩家/地图/job/合同/目标/进度持久化 |
| Source/PersonalChronicle/Data/PawnEconomy/WorkPriceSaveData.cs | A | schema1 有界价格 payload，未知/坏数据保留且禁新定价 |
| Source/PersonalChronicle/Domain/PawnEconomy/Employment/WorkPriceRules.cs | A | 两级价格解析、四段合同键、旧三段容量兼容 |
| Source/PersonalChronicle/Economy/PawnEconomy/Dialog_WorkPrices.cs | A | 劳务站13行两级定价 UI，严格银单位输入、新合同生效 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkOrders.cs | A | 不可变玩家意图、合同锚点时段校验、稳定证据键及 Sequence0 去重 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkOrderTrips.cs | A | Expose、pickup存证、同worker/map/job/contract恢复与真实交付累计 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorkPrices.cs | A | Scribe 接线、劳务站工种默认与订单覆盖价写入口 |
| Source/PersonalChronicle/Economy/PawnEconomy/Planning/VanillaOrderCatalogue.cs | M | 8订单均接线；指定收获映射实际 PlantCutting 工种，5组 |
| Source/PersonalChronicle/HarmonyStartup.cs | A | 按类隔离 Harmony 安装失败，已成功目标不重试 |
| Source/PersonalChronicle/Settings/Dialog_WorkGateWhitelist.cs | M | 补登 W3；本轮窄屏滚动/截断悬停/Bills 子类排除 |
| Source/PersonalChronicle/Settings/SettingsCatalog.cs | M | 补登 W3 活设置分节与3动作 |
| Source/PersonalChronicle/Settings/SettingsDrawer.cs | M | 补登 W3 设置开关/白名单/日志路由 |
| Source/PersonalChronicle/Settings/WorkGateDiagnostics.cs | M | 补登 W3 真实授权诊断日志 |
| Tests/EconomyTableOpsTests.cs | M | 表格最小宽、溢出与详情摘要 |
| Tests/HarmonyStartupTests.cs | M | 单类/部分目标失败隔离与完整生产 PatchAll 目标注册 |
| Tests/VanillaOrderCaptureTests.cs | M | 植物动物搬运捕获负例及真实交付进度 |
| Tests/VanillaOrderCatalogueTests.cs | M | 8订单/5工种/实际工种接线 |
| Tests/WorkOrderTargetTests.cs | A | 工种与订单目标限制及保留强制工作 |
| Tests/WorkOrderTripScribeTests.cs | A | 真实 Scribe 半途恢复、稳定事件不同tickReplay仅1份工资义务、旧坏未来与容量 |
| Tests/WorkPriceInputTests.cs | A | 23个严格银单位输入用例 |
| Tests/WorkPriceRulesTests.cs | A | 覆盖/默认/推荐价、独立锚点、旧键容量与合同价格冻结 |
| Tests/WorkPriceScribeTests.cs | A | 真实 Scribe 两级往返、缺键、坏/未来保留及真实起草拒绝 |

| docs/设计文档/架构/EXC-2026-001.md | M | 既有E2例外补登记S3/订单目标，沿用到期2027-08-15及升级触发复审；实机矩阵未执行 |

### 9.4 最终部署与文档门禁实证

2026-10-11 20:11:45 已限定部署5文件；逐文件源/目标/备份哈希已由REL独立重算，生产/Tests bin/游戏Mods DLL均40AF3A…DE87BAB；部署记录及备份根见 .codebuddy/tmp/codex-handoff-deployment.json。未启动游戏、未Steam发布。既有EXC-2026-001沿用批准与2027-08-15到期，本轮补完整目标/失败策略/回滚，COMP范围Tier B。

doc-guard本机输出 BLOCKED（新增113 / BLOCKED11 / WARN2 / 越基线修改13），11项是本轮接手前DLC参考包与公有资产方案的frontmatter缺项，均未在本轮Scope；本轮只更新既有文档与受授权Gate记录，未刷新基线。收尾PASS不代表全工程文档门禁PASS；待独立存量治理收口，不阻断当前代码/限定本地部署验收。

### 9.5 收尾校验

最终R1–R8输出与证据索引存 .codebuddy/tmp/codex-handoff-closure.txt；round-closure输出：收尾结论: PASS（机器项 通过=6 / N/A=0 / WARN=1 / BLOCKED=0 · 人工项 8 项待确认）。只确认客观产物/署名/字段与标题齐备，人工内容不由脚本判PASS；本轮角色独立记录已全部齐备。doc-guard仍历史BLOCKED，如§9.4，未伪造全工程文档通过。全部业务C#冻结哈希、最终DLL/测试TRX/rsp哈希见 .codebuddy/tmp/codex-handoff-source-freeze.json；源码晚于DLL=0，DLL与测试/部署一致。


## 十、2026-10-11 实机报障续修：派发合同但无人承接（本段为最新验证状态）

由 AI 助手 Codex 总控；occupy_review 独立读调用链，recruit_tests 单写规划测试。用户截图：ChopWood合同2件，单价8.40银，0承接；当前Player.log加载12:05 UTC上一段DLL，规划器运行、无OCCUPY。用户点击诊断后tick12030：Niklas/Winters/Dawson均合同覆盖0、只授权Firefighter/Doctor/PC_MealBreak。旧诊断不含预算/理由，因此不能从截图唯一判Niklas具体早退点。

| 文件/符号 | 具体问题与修复 |
|---|---|
| Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs · WorkOpportunity/BuildCore | 单件工资被当成一次完整融资款，必须覆盖整个生活预算缺口；新增只读IncrementalIncome，仅原版计件允许正工资逐件补储，仍检查资金、能力、执行、可达、到期、付款时限。预算余额/缺口不提前减，没有把预期收入计账。普通机会完整融资旧契约保留。PlayerPosted只标明来源；同付款时间已派发合同先于短名称自动fallback，早付款优先仍保留 |
| Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyVanillaWorkSource.cs · ReadOpportunities | 同时发布scope4part合同和相同工种短3partfallback，稳定按ID排序令fallback恒先；fallback签约时工作已全部被Posted预留又拒绝，循环重试无人承接。现在fallback只发布backlog−VanillaJobsCommitted>0的真实剩余工作，Posted/活约的占用全部扣除；设置计件/玩家Posted标志 |
| Source/PersonalChronicle/Economy/PawnEconomy/Planning/EconomyFoodCostSource.cs · ReadCosts | price≤0旧代码complete=true+无切片，误算生活成本0→NoIncomeNeeded；改complete=false，预算WaitingQuote/Unknown，不以无报价假装免费。不会因未知报价伪造贫穷而强迫求职 |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs · OnPlanPublished/RecruitmentDiagnostic | 旧OCCUPY仅占用后失败，预算/保护/制度早退无解释；新增RECRUIT按pawn的Plan/Submission分开去重、最多64键，记录实际decision/reason/制度门/预算现金目标缺口/活动需求/Posted单价能力资金/签约结果；不每周期交替重复Plan和Rejected刷日志 |
| Source/PersonalChronicle/Settings/WorkGateDiagnostics.cs · DumpToLog | 现有一键诊断增加每人的当前规划与预算/等待原因，Unknown原样打印，币值字段明确Minor（100分=1银） |
| Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs · QueryVanillaContractRows/ContractWorkLabel | screenshot合同key为ChopWood却显示工种泛称“割除”；按四段合同真实Order读现有本地化标签，旧三段通用合同保留工种名称，条款/key/金额不变 |
| Tests/PawnShadowPlanningTests.cs | 新增22案例：分件收入不伪造入账、付款时限/资金能力执行可达/正工资/过期守卫；真实3/4段key同付款时间排序/输入顺序/Posted无资金过滤/早付款优先；储备足/缺报价/征召/强制/保护作息不被Posted绕过 |
| Languages/ChineseSimplified/Keyed/PawnEconomyPlanning.xml | 新增增量工资说明，不把分件收入叫完整融资 |
| Languages/English/Keyed/PawnEconomyPlanning.xml | 同源说明与占位符一致 |

### 10.1 缺钱的现行量化契约（未采用强制招募）

目标T是未来2天必要费用减自有覆盖，目前真实接入主要食物成本；下水位ceil(70%T)，应急额floor(8%T)，触发min(T,下水位+应急额)，近似78%T。个人可用余额C低于触发线才启动，启动后持续到C≥T；未知报价/账户不当0。算例T=40银，触发31.20；35未启动不求职，30启动，赚回35继续，到40停止。个人账户不等于国库/殖民地总财富。

用户要求先解释规则，**没有批准“玩家派发即可忽略收入需求招募”**；因此当前继续保留BudgetReady/RefillActive、成年自由殖民者、基本需求、原版紧急保护、Work/Anything与合同工时/真实资金。未来若改变此策略须用户明确决定，本轮PlayerPosted排序不是绕过缺钱门。缺口15银但单价8.40/两件可分件赚16.80，现在不会仅因第一件小于缺口拒绝；未实际支付前仍显示缺口15。未知食物预算的Pawn仍等待报价，诊断如实解释，而不是保证所有Posted立即承接。

### 10.2 最新证据、部署与边界

正式重编BuildStamp 2026-10-11 12:31 UTC，0编译诊断；DLL SHA256 `471AA504240E0AF6A80004F579AE9FDBFA0122DA9F20A1F67344B2A38070113F`。Debug和Release全量各1451通过/0失败/1跳过，总1452；后者TRX `.codebuddy/tmp/codex-recruit-tests/recruit-release.trx`，日志 `.codebuddy/tmp/codex-recruit-test-release.txt`。needs验证必须匹配Release测试DLL，不能拿旧Release工件假绿：真实重测后PASS248双语键/40新需求页case；报告只落tmp，未覆盖历史B1.3证据。经济验证PASS340键/187引用。

反向对照只在tmp复制完整测试宿主与生产rsp，把排序和单位工资门槛恢复到旧逻辑，三个新回归恰好3失败0通过；采用.NETFramework4.8宿主，初次默认宿主0执行不算有效对照。最终有效TRX `.codebuddy/tmp/codex-recruit-counterfactual/counterfactual.trx`，生产DLL未变。合同源剩余容量实际调用链由独立源码/编译IL复核，未冒充原生游戏承接已成功。

本轮不新增Scribe字段或节点；原有价格与搬运schema保留。限定部署生产DLL＋PawnEconomyPlanning中英2文件，备份/源测试部署hash以 `.codebuddy/tmp/codex-recruit-deployment.json` 为准。游戏内验证仍需重启最新DLL后点击同诊断，观察Niklas实际decision/target/gap/Posted以及RECRUIT Accepted或具体等待。若无正收入需求、制度未Active、在保护状态/休息时间、合同工时之外，等待是保留规则；不是承诺立即派工。旧截图会话不是12:31构建的GameVerified。
20:36:38限定三文件部署完成，旧文件备份全部先于写入，源/Debug/Release/目标DLL哈希一致471AA504…070113F，部署JSON=.codebuddy/tmp/codex-recruit-deployment.json。未启动最新DLL实机，不称GameVerified。角色复核与R1-R8见 .codebuddy/tmp/codex-recruit-closure.txt。


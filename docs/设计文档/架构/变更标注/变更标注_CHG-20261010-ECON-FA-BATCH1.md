---
change_id: CHG-20261010-ECON-FA-BATCH1
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedAndDeployedByThisBatch
scope: 经济 AI 全面审计第 1 批（可启动批）FA-01/02/03/10/11/23 —— 新 Pawn 零余额账户、机构首页改读新政策、正常余额不再阻断重开、退出判据唯一化、签约失败补偿、故障显式重审
rule_ids: AI-001, AI-003, AI-005, AI-011, GOV-003, GOV-005, GOV-009, GOV-010, GOV-012, ARC-001, DATA-001, SAVE-003, UI-003, LOC-001, PERF-001, TEST-001
files:
  - path: Source/PersonalChronicle/Application/PawnEconomy/Finance/FundedSettlementLedger.cs
    type: M
  - path: Source/PersonalChronicle/Data/PawnEconomy/SettlementLedgerCodec.cs
    type: M
  - path: Source/PersonalChronicle/Data/PawnEconomy/InstitutionModeSaveData.cs
    type: M
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Governance/InstitutionCapabilityRules.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEnrollment.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLabels.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionMode.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/PawnSettlementSaveHost.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Policy.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/EconomyMode.xml
    type: M
  - path: Languages/English/Keyed/EconomyMode.xml
    type: M
  - path: Tests/EconBatch1FixesTests.cs
    type: A
  - path: Tests/PawnInstitutionModeTests.cs
    type: M
  - path: Tests/PawnTreasuryOperationsTests.cs
    type: M
---

# 经济 AI 审计第 1 批（可启动批）FA-01/02/03/10/11/23

**ChangeID**：`CHG-20261010-ECON-FA-BATCH1`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**依据**：`docs/任务派发/CodeBuddyCN/经济AI全面审计_2026-10-10.md`（§P1 + §CodeBuddy CN 修复批次与验收契约 批次 1）
与 `docs/任务派发/CodeBuddyCN/机构全部停业_专项审计与修复规范_2026-10-10.md`
**当前程序集**：`Assemblies/PersonalChronicle.dll` SHA-256 `4341DB26E5C6581BC2E945FAE514AADBD0506D9FA9C49668EF674560E2939565`（自审两项整改后重建，`BuildStamp -> 2026-10-09 17:04 UTC`）
**部署**：**已部署**（2026-10-10 01:13:12，`_scripts/deploy-to-game.ps1` → `RimWorld\Mods\殖民地综合管理系统`，复制 233 文件 <About 13 / Assemblies 1 / Defs 36 / Fonts 8 / Languages 66 / Patches 3 / README 1 / Textures 105>；部署区 DLL SHA-256 与工程一致 `F445F0EF…A7675AD`；被覆盖文件已备份到 `_backups\release-deploy\deploy_20261010_011312_298`）｜**Save 变更**：**加性可选**（见 §3）

---

## 一、修复的复现（修复前，逐条核实）

| 缺陷 | 修复前证据 | 修复后判据 |
|---|---|---|
| **FA-01** 新 Pawn 无账户 ⇒ 无法自主挣第一笔工资 | `EconomyShadowPlanning.ReadInput` 只在 `planningAccounts` 命中时给 `Known`，否则 `Unknown("AccountUnknown")`；账本初始只有 Treasury（`FundedSettlementLedger.balances` 种子），个人账户**仅在工资/旧权益转入**时由 `Apply` 创建 ⇒ `LifeBudget` 得 `WaitingAccount` ⇒ `ShadowPlan` 停 `WaitingBudget`，永不进入 `SeekWork` | 设计 §4「新 Pawn 账户由明确 EnrollPawn 命令建立，初始零余额；观察查询仅 TryRead，不创建」：新增**明确注册边界** `EnrollObservedPawns()`（本图在册自由殖民者，250 tick 有界登记，仅账本健康时执行） |
| **FA-02** 机构首页读旧记录、管理按钮不操作新制度 | `EconomyCommands.BuildInstitutions` 用 `Institution(anchor)?.Open`；`EconomyInstitutionMode.cs:19 LegacyFeesAllowed => false` 且 `Register` 不再创建旧记录 ⇒ 新机构恒显示"停业"；"管理"仅 `Focus` 选中旧对象 | 首页改为 `QueryInstitutions(mapId)` + `QueryInstitutionPolicy(anchorKey)`；管理按钮走新动词 `Policy`（切政策页并一次性滚动定位）；旧记录独立成「历史对账」区，只作归档展示 |
| **FA-03** 正常托管余额使重开卡在 Draining | `modeOutstanding` 含 `audit.TotalLiabilitiesMinor != 0 \|\| audit.ActualReserveMinor != 0`（= 任何余额/储备非零）⇒ 关闭后永远 Draining；`InstitutionModeService` 明确"Never bypass unfinished rights by toggling back on" | outstanding 只看**在途/待对账结算行**（`Prepared`/`ReconciliationRequired`）+ 旧权益未承接 + 托管操作 Pending；余额与专用储备是已归属保留资产，不阻断重开 |
| **FA-10** 退出修复只进 Query 未进 Maintain | `QueryContractExit` 已读 `Desired`/班次记录/能力三项；`MaintainContract` 一项都没读（只 `policy.Open` + 区域存活）⇒ UI 提示应退出而实际不退出、工作优先级不释放 | 抽出**唯一判据** `EvaluateContractExit`，查询与执行共用同一实现 |
| **FA-11** 签约启用失败只撤合同、不释放已写工作类型 | `TrySignForOpportunity` 失败分支仅 `Employment.Terminate` ⇒ `ActiveContractFor` 查不到，`MaintainContract` 再也无法释放 ⇒ 无合同却占着工作类型；`AllTypesUsable` 只看 `priority > 0`，不看 `WorkTypeIsDisabled` | 失败分支当场 `ReleaseWorkTypes`（玩家改写值仍受 `CanRestore` 保护）；`AllTypesUsable` 同时判"未禁用 且 优先级 > 0" |
| **FA-23** Faulted 无恢复出口、启动诊断不可执行 | `InstitutionModeService.Observe` 在 `next == Faulted` 时跳过全部推进；无任何显式重审入口；首页只报"待完成/保存存在问题"，不给下一步，也把"账本恢复读数"标称成"完整游戏存档验收" | 新增显式重审命令 `ReVerifyInstitutionMode()`（重算**实时**故障源，仍有任一故障即拒绝；全部消失才清 `State.Fault`，revision +1 + 被解除故障码落盘）；面板逐门给出**下一步操作**，并把 SaveAcceptance 措辞改为"只证明账本健康，不等于完整实机验收" |

## 二、改动要点（按层）

- **Domain**（`InstitutionCapabilityRules.cs` 新增，纯规则）：逐能力放行的唯一判据 —— 未配置 → 未挂牌 → 机构停业 → 非营业时段 → 制度未开放，先命中者即原因码；明确"机构 `Open=true` 不等于制度已开放支付"。
- **Application**（`FundedSettlementLedger.cs`）：新增 `EnrollAccount(id)` / `ReadEnrolledAccounts()` / `Restore(rows, enrollments)`。登记**不占条目容量、不进 Version、不改 TotalMinor、不覆盖已有余额**（幂等）；上限 `MaximumEnrolledAccounts = 1024`，越界**拒绝**（可观察）而非静默丢弃。
- **Data**（`SettlementLedgerCodec.cs`、`InstitutionModeSaveData.cs`）：结算账本新增加性元素 `<Account id=.../>`（schema 仍 1，缺失=未登记）；制度节点新增可选属性 `reverifiedFault`（旧档缺失即 null）+ 唯一写入口 `ClearFaultAfterReverification`（仅 Faulted 且故障码非空时成立，revision 单调 +1）。
- **Economy**：`EconomyEnrollment.cs`（登记周期）、`EconomyInstitutionMode.cs`（FA-03 口径 + `ReadLiveFault` 抽取 + 重审命令 + `InstitutionReverifyCode`）、`EconomyCommands.cs`（首页统一展示模型 `InstitutionSummary`：制度 → 机构配置 → 时段 → 能力及原因；`ReverifyHint`；行动词白名单加 `Policy`/`Reverify`）、`EconomyEmployment.cs`（FA-10/FA-11）、`EconomyRuntime.cs`（250 tick 槽调用登记）、`PawnSettlementSaveHost.cs`（宿主登记门，`preserveRaw`/`AuditCore().Fault != null` 一律拒绝）、`Dialog_PawnEconomy.cs`（`Policy` 切页 / `Reverify` 执行 + 如实回报）、`Dialog_PawnEconomy.Policy.cs`（一次性滚动定位；标签回落委托）、`EconomyLabels.cs`（标签回落唯一实现）。
- **Localization**：`EconomyMode.xml`（zh/en 同步）新增 37 键（能力名/原因、制度/配置/时段标签、每门"下一步操作"、重审结果码），并改写 `Mode.Blocker.SaveAcceptance`、`Mode.Outstanding`、`Settings.Detail.EconomicInstitution` 三处措辞。
- **Tests**：新增 `EconBatch1FixesTests.cs`（13 用例）；**修正两条编码了缺陷语义的既有断言**（见 §4）。

## 三、Save 兼容（SAVE-003）

- 两处均为**加性可选**：旧档无 `<Account>` / `reverifiedFault` 即"未登记 / 从未解除"，行为等价于修复前，不需要 Migration，schema 版本不变。
- 分配到的登记集合是**有序且确定**的（Ordinal 排序），编解码可往返；已由余额条目派生的账户不会被登记覆盖（既有工资转入路径不变）。
- 登记集合不进 `Version` ⇒ 不影响任何 `ExpectedVersion` 并发判据；`TotalMinor` 不变 ⇒ 不发钱、不改账实核验。

## 四、验证

| 项 | 结果 |
|---|---|
| 编译（`_scripts/compile_with_sdk.ps1`） | **0 错 0 警**（`BuildStamp -> 2026-10-09 16:50 UTC`） |
| 全量测试 | **1217 总项 / 1216 通过 / 1 跳过 / 0 失败**（`Tests/TestResults/EconBatch1-Full-V2.trx` / `-V3.trx` / 自审整改后的 `-V4.trx` 三次一致） |
| 性能整改（自审发现） | `modeOutstanding` 的"未完成行"判据原先经 `settlement.ReadEntries()`（每次 `ToArray` 复制，容量上限 4096），而该判据每个 250 tick 槽都会被调用 ⇒ 改为 `FundedSettlementLedger.HasUnresolvedRows()` 直读内部列表（零分配），宿主 `PawnSettlementSaveHost.HasUnresolvedRows()` 透传 |
| 新增用例 | `EconBatch1FixesTests.cs` 13 条：登记幂等/不占版本/不发钱、越界拒绝、编解码往返、旧档=未登记、逐能力六类原因、关闭后重开（Off → 非 Draining）、在途权利仍 Draining、Faulted 不自愈、显式解除的门与 revision、`reverifiedFault` 往返 + 旧档合法性 |
| 既有断言语义更正 | ① `Tests/PawnInstitutionModeTests.cs` 的 `B241_SwitchOff*` 由"有资金也必须 Draining"改为"旧权益 ⇒ Draining；仅正常余额 ⇒ Off 且可重开"；② `Tests/PawnTreasuryOperationsTests.cs` 的 `B242_CompleteOfflineCutover*` 关闭态由 `Draining` 改为 `Off` + `HasOutstandingRights == False`。两处均按审计 §FA-03 要求（"合法余额不要求归零才能重开"）改写，**不是**放宽度量 |
| 调用点可达性 | `EnrollObservedPawns`（`EconomyRuntime` 250 tick 槽）、`InstitutionSummary`/`ReverifyHint`（`EconomyCommands.Query`）、`ReVerifyInstitutionMode`（`Dialog_PawnEconomy.Reverify`）、`ClearFaultAfterReverification`（仅 `ReVerifyInstitutionMode`）—— 均有实调用方 |

## 五、未验证（NOT RUN，不得视为通过）

- **GameVerified 全部未跑**：真实 Pawn 的自主签约与首笔工资（FA-01 端到端）、首页/政策/总览一致性（FA-02 实机）、注资→运行→关闭→保留余额→再开启（FA-03 实机）、UI 与真实工资/购买请求交叉验证、故障态重审实机、存档往返。
- **部署已完成但需重启才生效**：本批已于 01:13 部署到 `RimWorld\Mods\殖民地综合管理系统`（DLL 与工程同哈希），程序集只在游戏启动时加载 ⇒ **必须重启 RimWorld**；重启前游戏内看到的仍是旧版本。
- 部署的这份程序集反映**当前整树源码**：除本批 18 个文件外，还包含同一工作树内并行会话（M8/M10 批次）在 00:35–00:36 的改动 —— 那部分**不由本批证据背书**。
- `InstitutionModeService` 的"在途承诺保留并继续履约"仅验证了阶段语义，未验证真实在途交割的持续履约。

## 六、残余风险与未做项（如实登记）

1. **并发写入者**：本工程同一工作树另有会话在写（`变更标注_CHG-20261010-ECON-M8-FULL.md` 00:37、`-M10-FULL.md` 00:30；并修了我新增文件的 `using RimWorld`）。本批证据取自 00:47–01:13 的编译、测试与部署（01:13 部署到 Mods 区、DLL 同哈希，详见 §五）。
2. **逐能力放行未实装**：本批只把"每个能力可用与否 + 原因"如实上屏；真实资金路径仍由全局 `NewCommitmentsAllowed`（制度 Active）统一放行。若 PM 要"每个支付能力以自己的已核验运行时条件放行"，需单独排期（涉及 Wage/Consumption/Service 三条执行链）。
3. **旧制度详情块仍在首页**：焦点选中旧记录时仍显示其归档字段（写按钮已被动词白名单剥离，仅剩展示）。完整"旧记录独立成页"未做（属 FA-02 的 UI 收尾）。
4. **登记上限 1024**：达上限后拒绝登记可被观察，但没有"回收已离图 Pawn 的零余额登记"路径；长局极端场景可能触顶（当前无自动回收，未做）。
5. **批次 2–4 未开始**：FA-04/05/06/07/09/13、FA-08/12/14/15/16/18/22、FA-17/19/20/21/24 按审计顺序留待后续批次。
6. **能力原因码的 UI 未做 1:1 预览对齐**：本批直接以文本行呈现，未走 HTML/RUIF 预览对照流程（LAND 门不适用）。

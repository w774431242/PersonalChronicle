---
change_id: CHG-20261012-INSTGATE-GUIDE
title: 制度门：①「缺哪一步」三处入口同源可见 ②按用户裁决收窄 SaveRecoveryVerified（无内容可恢复 ⇒ 判 N/A）
date: 2026-10-12
state: CodeVerified / TestVerified / Deployed（GameVerified NOT RUN）
layer: Economy/PawnEconomy（就绪判定 + 呈现与交互提示 + 校验脚本）+ 例外登记 / 实装口径文档
scope: 用户实机反馈「新殖民地无法接合同且劳务站只显示制度尚未开放」的可见性修复 + 同轮按裁决收窄制度门适用范围
author: CodeBuddy / DeepSeek-V4.1-Flash
program: PROG-ECON（本轮未占用共享 change-context，见 §七）
exc: EXC-2026-031（E2 · Approved · 审批人=用户 2026-10-12）
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionMode.cs
    type: M
    note: ①就绪第 6 位改 `RecoveryProvenRevision >= 0 || nothingToRecover`；②新增只读判定 NothingToRecover（旧账快照 + LedgerVersionAtFreeze + 在途托管单 + Enabled/Funded 旗标）；③新增只读 FirstGateStep()（三处入口共用第一项待完成门的下一步）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLabels.cs
    type: M
    note: +GateBlocker/GateStep/GateText —— 制度门门名与「下一步」文案唯一实现（缺键回落稳定 ID，不裸键）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyWorld.cs
    type: M
    note: 劳务站 GetInspectString：存在 InstitutionNotReady 流程时追加一行下一步（用户截图处）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyLaborInteraction.cs
    type: M
    note: 右键「接合同」失败提示：制度门被挡时补同源下一步（此前只弹「制度尚未开放新的用工承诺」）
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyCommands.cs
    type: M
    note: 经济页 blocker 行改走唯一实现（原为就地拼接，同句两套回落）
  - path: Tests/InstitutionGateGuidanceTests.cs
    type: A
    note: 3 条离线断言（前缀唯一 / 被挡即显示 / 唯一组装点）+ 反向对照（注入第二处组装 ⇒ 真红）
  - path: Tests/PawnInstitutionModeTests.cs
    type: M
    note: 断言语义订正（空档 ⇒ 豁免 ⇒ Active，且断言 RecoveryProvenRevision 仍为 -1 证明未伪造证据）+ 两条不放宽反证（有旧账 / 旧账空但冻结时账本非空）
  - path: _scripts/verify-pawn-institution-mode.ps1
    type: M
    note: 判据锁定正则同步为 `RecoveryProvenRevision >= 0 \|\| nothingToRecover`（原正则在新实现下必假失败）
  - path: _scripts/verify-pawn-treasury-operations.ps1
    type: M
    note: 同上（第二处同一判据的锁定点）
  - path: docs/设计文档/架构/EXC-2026-031.md
    type: A
    note: 例外登记（E2/Approved/审批人=用户/到期 2027-02-12/复审触发 WI-008）
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/13-实装口径变更_先全实装后统一验收_2026-10-08.md
    type: M
    note: 就地登记被本例外收窄的既有口径（不得进入 Active 的适用范围）
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/15-B4_实装记录.md
    type: M
    note: 同批同步第二份记录（验收阶段口径收窄）
---

# CHG-20261012-INSTGATE-GUIDE · 制度门可见性 + 适用范围收窄

## 一、问题（实机取证，非推测）

用户截图：劳务站 inspect 显示 `· … · 阻塞 (制度尚未开放新的用工承诺)`。

| # | 证据（`Player.log`） | 含义 |
|---|---|---|
| 1 | L192 `Initializing new game with mods:` | 该局面是**新开档**（L99 为读档 Autosave-1） |
| 2 | `[PC][RECRUIT] … mode=Preparing commitments=False decision=SeekWork reason=FundedIncrementalOpportunity selected=vanilla:PlantCutting:…:ChopWood` | pawn **想签**、合同已发布且 `capable/funded=True`，被制度门在 `OnPlanPublished` 早退 |
| 3 | 存档解码：`phase="Active" recoveryProvenRevision="0"`（Autosave-1 / 新移民1） | 做过一次存读档往返的档是 Active；新档恒缺该证据 |

门链唯一判据：`InstitutionModeSnapshot.NewCommitmentsAllowed = Phase==Active && Desired && readiness.AllReady`
（`Domain/PawnEconomy/Governance/InstitutionModeContracts.cs:68`）。七门中唯一对新开档恒假的是
`SaveRecoveryVerified = RecoveryProvenRevision >= 0`；其唯一写入点前置 `LoadedFromSave`（只在**读档**会话置位）
⇒ 新殖民地**在同一会话内不可能**满足 ⇒ 制度永停 `Preparing`，并连带关闭柜台购食、公有储物取食、衣柜取衣
（`EconomyEmployment.OnPlanPublished:602` 早退之后的所有经济解法）= 玩家侧"接不到合同且不知道为什么"。

## 二、本轮交付（两段）

### 段 1 · 可见性（三处入口同源）

用户原话「因为这个问题导致无法接合同」的直接病灶是**要求不可见、不可执行**。做法：

1. `EconomyLabels.GateBlocker/GateStep`：门名与「下一步」文案**唯一实现**（`PC_PEA.Mode.Blocker.*` / `PC_PEA.Mode.Step.*`，缺键回落稳定 ID）。
2. `GameComponent_PawnEconomy.FirstGateStep()`：第一项待完成门的下一步文本（**只读**当前记忆化快照）。
3. **劳务站 inspect**（用户截图处）· **右键「接合同」失败提示**（玩家实际点击失败的那一刻）· **经济页 blocker 行** 三处同源。
4. **零语言键增删**（复用既有 7+7 键，中英 1:1）。

### 段 2 · 按用户裁决收窄适用范围（`EXC-2026-031`）

用户 2026-10-12 裁决：「做，我认这个取舍」。就绪第 6 位改为
`institutionMode.RecoveryProvenRevision >= 0 || nothingToRecover`，其中 `NothingToRecover(original)` 四条**全部取自冻结时刻的持久事实**（故单调、可审计、不随后续收支翻转）：

| # | 判据 | 依据 |
|---|---|---|
| ① | 制度已冻结 · 旧账快照可读 · `Fault == null` | `InstitutionModeSaveData.Frozen` / `LegacySettlementArchive.Fault` |
| ② | 冻结时**账本条目为 0** | `LedgerVersionAtFreeze` = `settlement.Version` = `entries.Count`（已随档持久化，**不新增 schema**） |
| ③ | **无在途托管操作** | `treasuryOperations.Pending == null`（存款单**不进账本**，不能由 ② 蕴含 —— DATA 席 2026-10-12 指出的反例） |
| ④ | 旧账信封未启用/未注资、无记录、无现金债权 | `Report.Records.Count == 0 && !Enabled && !Funded && Claims.Count == 0`（空 `Raw` ⇒ `Report == null` ⇒ 视为"真的没有旧账"，**NPE 防护**已含） |

**不写任何伪证据**：`MarkSaveRecoveryProven` 的语义、落盘字段与唯一写入点一律未动。

## 三、验证证据（实测 · 终版一遍）

| 项 | 结果 | 证据 |
|---|---|---|
| 正式编译 | `exit=0` · **0 错 0 警** · BuildStamp `2026-10-12 15:33 UTC` | `_scripts/compile_with_sdk.ps1`（日志 `.codebuddy/tmp/instgate-build-final.txt`；备份 `.bak_20261012233341`） |
| 全量测试 | **1639 通过 / 0 失败 / 1 跳过**（Debug 与 Release **同结果**） | `dotnet test Tests/PersonalChronicle.Tests.csproj`（先正式编译后测试） |
| DLL 同源 | 工程 == Debug bin == Release bin：`CCD71B96D81847F8436656A94A26E00DABF8EBFD84C14E0DA1EE33B0944A82C0` @ `23:33:43`（2,460,672 B） | `Get-FileHash` 三处一致 |
| 经济域脚本 | `verify-pawn-economy.ps1` **PASS**（16 Def / 5 机构 / 4+3 Job / 556 双语键 / 266 `T()` / RUIF） | 脚本 stdout |
| 制度/国库脚本 | `verify-pawn-institution-mode.ps1` **PASS** · `verify-pawn-treasury-operations.ps1` **PASS** | 判据正则已同步；末尾工件钉经 `dotnet test -c Release` 同源后 PASS |
| 新断言**反向对照** | 注入第二处文案组装（临时探针）⇒ 该断言**真红**（1 失败/1 通过）；删探针 ⇒ 绿 | 探针已删（`Test-Path` 复核） |
| 断言语义订正**留痕** | 首跑 2 处**真红**（`B241_…StaysPreparing…`、`SaveRecoveryGate…` 的 fresh 段 —— 旧口径断言）⇒ 按新口径订正后全绿；`Tests/PawnTreasuryOperationsTests.cs:235`（有旧账）**原样通过** | 本轮实测，非事后补写 |
| 唯一实现稽核 | `Mode.Step.` / `Mode.Blocker.` 字面量在 `Source/PersonalChronicle/**` 仅 `EconomyLabels.cs`；`internal static string FirstGateStep(` 全树恰好 1 处 | `Select-String` 全树扫描 |

## 四、Gate 结论

| Gate | 席 | 结论 | 要点 |
|---|---|---|---|
| ARCH | `pc-arch-gatekeeper` | **PASS**（不触发 Architecture Review） | 四问 = No/No/No/No；无新跨层边（复用同函数已取变量）；`EconomyWorld.cs` 与 `EconomyInstitutionMode.cs` 均 < ARC-013 阈值 |
| DATA + SAVE | `pc-data-steward`（**补跑两轮**） | **DATA = PASS / SAVE = PASS（Tier B 静态）** | 首轮对拟议方案判 **BLOCKED**（空 `Raw` 的 null 面、"启用却裁空"误豁免、豁免面未界定）⇒ 三条必修已全部实装后复判 PASS；无新 Scribe 字段/schema/Def；旧档缺键仍回落 -1 |
| UI + LOC | `pc-ui-loc-guard` | **UI = PASS / LOC = PASS** | 零键增删、7+7 键两语言成对、回落可读、只读纪律成立；P2：inspect 长句窄面板换行未实机验证、inspect 未给门名 |
| PERF + TEST | `pc-perf-tester`（**补跑终版**） | **PERF = PASS / TEST = PASS（Tier B）** | 独立复跑终版：`exit=0` 无诊断、1639/0/1（Debug+Release）、三脚本 PASS、DLL 三方同哈希；`NothingToRecover` IL 普查 71 指令 / **0 分配** / 无循环 / 无反射 / 无全库扫描（唯一调用点 = 250 tick 观测 + 设置变更 + ReVerify） |
| COMP | — | **N/A** | 无第三方/DLC/Harmony 面（未改 `Patches/`） |
| REL + DELTA | `pc-release-clerk` | **REL = PASS WITH NOTE / DELTA = PASS** | 见 §五/§六；Gate 记录 `GR-CHG-20261012-INSTGATE-GUIDE-REL-DELTA.md` |

## 五、口径说明（如实）

1. **两校验脚本的工件钉**：`verify-pawn-institution-mode.ps1` / `verify-pawn-treasury-operations.ps1` 末尾要求
   `Assemblies/PersonalChronicle.dll` 与 **`Tests/bin/Release/net48/PersonalChronicle.dll`** 同哈希。
   ⇒ 正确次序是 **先正式编译 → 再 `dotnet test -c Release`**（否则报 `differs from tested DLL`，与源码无关）。
   该脚本内的 `TRX` 数字取自冻结工件（2026-10-08/10-10），**不能绑定当前 DLL**；当前 DLL 的测试证据由本轮
   Debug+Release 全量复跑承担。PERF/TEST 席建议：正式测试改 `-c Release` 或让脚本钉 Debug 副本。
2. **例外编号**：三处引用曾误写 `EXC-2026-002`（实为 DevTest 反射例外），已全部更正为 `EXC-2026-031`。

## 六、未做 / 未验证（如实登记）

| 项 | 状态 |
|---|---|
| **实机验证** | **NOT RUN**（沙箱无游戏进程）。待验：**新开档**制度应直接 `Active`、劳务站/右键可直接接合同且不再出现「制度未开放」；**已 Active 的旧档**行为不变、不得刷出该提示；inspect 长句在窄面板的换行/截断观感 |
| **WI-008 存档往返实机矩阵** | **未执行** ⇒ 被豁免档的账本恢复路径仍未实证（`EXC-2026-031` 的 `risk` 与 `review_trigger` 已写明到期复审） |
| inspect 未给门名（上下文偏弱） | 登记（P2）：只给「下一步」，未与经济页一样成对显示门名 |
| 键位奇偶断言 | 团队常识已由 REL 席实测 7+7 键成对，但**未固化为断言** ⇒ 登记 |
| Debug/Release 工件钉口径 | 未改（改动会使历史 Gate 记录的断链）⇒ 登记为交付前必补项 |
| 部署 | **已执行**（用户 2026-10-12 裁决「现在就部署」）：`deploy-to-game.ps1` → `RimWorld\Mods\殖民地综合管理系统`；⚠ 该 DLL 反映**整树源码**，含并行会话在途改动（§七），**不由本批证据单独背书**；游戏需重启才生效 |

## 七、并发写入者与共享上下文（如实）

- 本轮**未**调用 `change-context.ps1 -Set` / `finalize-change.ps1` / `gate-runner -GenScope`，**未**改写
  `.codebuddy/audit/change-context.json` 与 `.codebuddy/audit/scope/**` —— 该上下文当时由并行会话
  `CHG-20261012-ECON-HAULCMD`（同一 `PROG-ECON`）持有。
- 故本轮 DELTA 使用自建范围外清单 `.codebuddy/tmp/instgate-out-of-scope.txt`
  = 并行会话 scope 清单 ∪ 其标注声明的 6 个文件 ∪ 其标注本身，**再扣除本批 12 个文件**。
- 台账：按用户裁决 **并入并行会话同波次收敛**（本轮不另立 WI 项）。

## 八、改动文件索引

**生产（5）**：`EconomyInstitutionMode.cs`（就绪位 + `NothingToRecover` + `FirstGateStep`）· `EconomyLabels.cs`（文案唯一实现）· `EconomyWorld.cs`（劳务站 inspect）· `EconomyLaborInteraction.cs`（右键失败提示）· `EconomyCommands.cs`（经济页 blocker 行）
**测试（2）**：`Tests/InstitutionGateGuidanceTests.cs`（**A**）· `Tests/PawnInstitutionModeTests.cs`（语义订正 + 反证）
**脚本（2）**：`_scripts/verify-pawn-institution-mode.ps1` · `_scripts/verify-pawn-treasury-operations.ps1`
**文档（3）**：`docs/设计文档/架构/EXC-2026-031.md`（**A**）· `实装落地/13-实装口径变更_先全实装后统一验收_2026-10-08.md` · `实装落地/15-B4_实装记录.md`

*本标注由 AI 助手 · CodeBuddy（模型：DeepSeek-V4.1-Flash）于 2026-10-12 生成；"已完成"结论均以编译/测试/脚本实测为准，未执行项已在 §六 明确标注 NOT RUN。*

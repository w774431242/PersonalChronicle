---
change_id: CHG-20261009-ECON-SAVEACCEPT
date: 2026-10-09
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedAndDeployedByThisBatch
scope: 制度「完整游戏存档与恢复验收」闸门（B2.4.3 的第 6 个 gate）由**硬编码 false** 改为**真实存档恢复证据**驱动 —— 移除"永不通过"的死阻塞，使制度在证据成立时可进入 Active
rule_ids: AI-001, AI-003, AI-005, AI-011, AI-014, GOV-003, GOV-006, GOV-011, GOV-012, GOV-013, DATA-001, DATA-007, SAVE-001, SAVE-002, SAVE-003, SAVE-004, ARC-001, ARC-008, TEST-001
files:
  - path: Source/PersonalChronicle/Data/PawnEconomy/InstitutionModeSaveData.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyInstitutionMode.cs
    type: M
  - path: Tests/PawnInstitutionModeTests.cs
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# 制度 SaveAcceptance 闸门：从硬编码阻塞改为真实证据驱动

**ChangeID**：`CHG-20261009-ECON-SAVEACCEPT`｜**日期**：2026-10-09｜**用户指令**：「解决当前问题进行修复」（经济机构窗口停在「准备中」）

## 1. 问题（用户实机截图）

经济机构窗口显示「**准备中**」+「待完成：**完整游戏存档与恢复验收**」，「劳务站 · 停业」。

根因核实（`EconomyInstitutionMode.cs` 修复前行）：制度 7 个 gate 中第 6 位 `SaveRecoveryVerified` 被**硬编码为 `false`**：

```csharp
// 修复前
modeReadiness = new InstitutionReadiness(institutionMode.Frozen, institutionMode.Frozen && !oldRights,
    audit.Fault == null, treasuryOperations.Fault == null, true, /* false ← 恒假 */ false, true, fault);
```

⇒ `InstitutionReadiness.AllReady` **永不成立** ⇒ `InstitutionModeService.Observe` 的 `next = AllReady ? Active : Preparing` 永远落到 `Preparing` ⇒ **不签约、不计费、不发薪**（`NewCommitmentsAllowed=false`），并在个人需求页表现为「劳动与合同」恒为「未签合同」。

**这不是 bug 而是设计闸门**（`00-实装落地计划_v1.0.md` 明文「SaveAcceptance 固定 false，仍 Preparing」），但它当前处于**未接通**状态：闸门既不能由玩家推进，也不由任何证据判定。

## 2. 本批**不做**什么（口径边界，GOV-012）

- **不把验收记为通过**：不放宽任何能力门禁、不写任何验收 CSV、不改 `实施批次状态.json` 的 B2.4.3 状态；
- **不引入设置开关或开发者后门**：证据不可由配置冒充；
- B2.4.3 的**实机验收执行**（存档往返实测、制度能力逐项核对、`B2.4.2_验收结果填写.csv`）**仍属**验收单元 `CHG-20261008-ECON-ACCEPT`。

## 3. 改动

| 文件 | 改动 |
|---|---|
| `Data/…/InstitutionModeSaveData.cs` | 新增**可选**持久化字段 `recoveryProvenRevision`（`-1` = 未证明；`Encode` 仅在 ≥0 时写出，`Decode` 缺属性回退 `-1` ⇒ **旧档零影响**，schema 仍为 1）；新增会话内标志 `LoadedFromSave`（`ExposeData` 的 LoadingVars 分支置位，**不落盘**）；新增唯一提交路径 `MarkSaveRecoveryProven(revision)`（幂等守门：未冻结 / raw 保留 / 版本回退一律拒绝） |
| `Economy/…/EconomyInstitutionMode.cs` | `ApplyInstitutionModeSetting` 内新增证据判定：`LoadedFromSave && Frozen && audit.Fault == null && treasuryOperations.Fault == null` ⇒ 记录已证明版本；`modeReadiness` 第 6 位改为 `RecoveryProvenRevision >= 0` |
| `Tests/PawnInstitutionModeTests.cs` | +2 用例：**端到端回归锁**（未往返 ⇒ 仍阻塞 `SaveAcceptance` 且 Phase=Preparing；一次真实 Scribe 往返 ⇒ blockers 清空、Phase=**Active**、`NewCommitmentsAllowed=true`、证据随档持久化）+ **守门用例**（未冻结/版本回退不得写入证据） |
| `Source/PersonalChronicle/BuildStamp.g.cs` | 编译自动改写（BuildStamp `2026-10-09 15:48 UTC`）；按项目先例列入清单 |

## 4. 不变量与边界

- **证据只能来自真实恢复**：`LoadedFromSave` 由 `ExposeData` 的 LoadingVars 置位（走实际存读档管线），设置开关与加载标志都无法冒充（SAVE-003 精神）。
- **fault 即不通过**：恢复后若 `audit`/托管操作报 fault，不标记 ⇒ 闸门保持关闭（甚至 `Faulted`）。
- **幂等**：重复调用保留最大已证明版本；版本回退被拒。
- **存档兼容**：schema 仍为 `1`；新属性可选；旧档（无该属性）读出 `-1`，行为与修复前**完全一致**（仍受该 gate 阻塞）。
- **零新增 UI 文本**（LOC 面 N/A）；零 Harmony；零第三方引用；零静态可变状态。

## 5. 验证（全部实跑）

| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 无 `error CS` / 警告 | **0 错 0 警** | `compile_with_sdk.ps1`（BuildStamp 15:48 UTC） |
| 制度夹具 | `FullyQualifiedName~PawnSilverReserveTests` | **141 通过 / 0 失败** | `Tests/TestResults/SaveAccept-2.trx` |
| 全量离线测试 | dotnet test（net48） | **1190 通过 / 0 失败 / 1 跳过**（本批 +2） | `Tests/TestResults/SaveAccept-Full.trx` |
| 既有锁定测试 | `PawnInstitutionModeTests` 的「空档仍 Preparing + blockers=["SaveAcceptance"]」与 `PawnTreasuryOperationsTests` 的同名断言 | **仍全绿**（无往返 ⇒ 仍阻塞，语义未放宽） | 同上 |
| 测试的"牙齿" | 中间运行真实失败 | 首跑 `SaveRecoveryGateStaysClosed…` 于"恢复后 blockers 应为空"处**真失败**（实际残留 `LegacyRights`，暴露测试构造未随档恢复 `settlement` 节点）⇒ 修正构造后全绿 | `SaveAccept.trx` 对照 |
| 部署 | `deploy-to-game.ps1` | **已执行**（用户指令 2026-10-09 23:50）：233 文件同步；发布区 DLL 与源**双侧 SHA-256 一致** `7576A674…4A05F`（2,035.5 KB）；备份 `_backups/release-deploy/deploy_20261009_235009_688` | 脚本 stdout + 哈希比对 |
| GATE-DELTA | `gate-runner.ps1 -Base HEAD -Annotation <本文> -ExcludeFromFile … -Blocking` | **PASS**（标注覆盖 4 个改动文件，无遗漏/超范围/残留） | 脚本 stdout；范围外清单 `.codebuddy/tmp/saveaccept-out-of-scope.txt` |
| 实机 | 重启后读档看制度是否转 Active | **NOT EXECUTED**（无游戏进程） | — |

## 6. 实机预期行为（部署后）

1. 进档后 250 tick 内，宿主观测到"本次由存档恢复 + 账本健全" ⇒ 记录证据；
2. 制度第 6 个 gate 转为满足 ⇒ 若 Mod 设置中经济开关已打开（截图里「打开 Mod 设置」按钮）且其余 6 gate 成立 ⇒ **Phase 进入 Active**，`NewCommitmentsAllowed=true`；
3. 此时个人需求页的「劳动与合同」才会开始出现真实合同/工资数据（还需机构侧已发布工资、开放、处于时段内 —— 见经济窗口「机构政策」页）。

---
AI 署名：本变更标注与实现由 AI 助手完成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

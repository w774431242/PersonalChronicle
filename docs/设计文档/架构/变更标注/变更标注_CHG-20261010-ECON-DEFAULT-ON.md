---
change_id: CHG-20261010-ECON-DEFAULT-ON
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: Implemented
scope: 用户需求「经济系统默认启用」—— 设置项默认翻转 + 载入后自动请求制度模式 + B2.4.1 契约测试与验证资产同步
rule_ids: AI-001, AI-005, GOV-005, GOV-010, SAVE-001, SAVE-003, SAVE-004, DATA-003, UI-006, LOC-001, TEST-001, DELTA-001
supercedes: B2.4.1 契约「设置默认 false」（仅该条；其余 B2.4.1 契约不变）
files:
  - path: Source/PersonalChronicle/ChronicleSettings.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs
    type: M
  - path: Tests/PawnInstitutionModeTests.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/EconomyMode.xml
    type: M
  - path: Languages/English/Keyed/EconomyMode.xml
    type: M
  - path: _scripts/verify-pawn-institution-mode.ps1
    type: M
---

# 经济系统默认启用 · CHG-20261010-ECON-DEFAULT-ON

**ChangeID**：`CHG-20261010-ECON-DEFAULT-ON`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**用户需求（原话）**：「经济系统默认启用」→ 用户裁决「按推荐进行」＝ **D1=A + D2=A**（载入即自动请求制度模式 + 设置项默认 true，含契约与文案同步）。

---

## 一、为什么不是"改一个默认值"

制度模式的唯一请求入口 `ApplyInstitutionModeSetting(bool)` 此前**只在 Mod 设置页手动切换时**被调用（`SettingsDrawer.cs:619-622`），读档/新档都不触发 ⇒ 只翻转默认值不会产生任何可见变化。因此本次必须**同时**做两件事：

| 代号 | 内容 | 依据 |
|---|---|---|
| **D1** | 载入后按设置**自动请求**制度模式 | 让"默认启用"真正落到运行期 |
| **D2** | 设置项默认值 `false → true`（字段初值 + Scribe defaultValue 双处） | 让意图默认开启；**推翻** B2.4.1 契约该条，测试与脚本同批更新 |

## 二、实装内容（6 文件）

### D2 `Source/PersonalChronicle/ChronicleSettings.cs`
- `public bool EnablePawnEconomicInstitution = true;`（原 false）
- `Scribe_Values.Look(ref …, "enablePawnEconomicInstitution", true);`（原 defaultValue false）
- 语义保持不变：**Intent only**（意图）≠ 能力授予；玩家显式关闭过的档（存档内存了 `false`）永不被默认值覆盖。

### D1 `Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs`
- `FinalizeInit()` 末尾新增 `QueueDefaultInstitutionRequest();`
- 新增 `QueueDefaultInstitutionRequest` / `TryDefaultInstitutionRequest`：
  - **主线程**：`LongEventHandler.ExecuteWhenFinished`（照抄同文件既有范式 `QueuePerceptionInitialization`/`TryInitializePerception`，`EconomyPerception.cs:27-42`）—— 因为 `FinalizeInit` 可能跑在长事件载入 worker 上，而制度请求要 `Freeze` 并读写带主线程所有权断言的结算宿主；
  - **过期回调防护**：`UnityData.IsInMainThread && ReferenceEquals(Current, this)`；
  - **三道闸门**：① 设置项为 true；② 制度**从未被请求过**（`!Frozen && !Desired`）—— 这是"默认"的边界，只覆盖首次，不覆盖任何既成决定；③ `PreserveRaw`（坏 payload）不碰；
  - **幂等**：`ApplyInstitutionModeSetting` 仅在 `!Frozen && !PreserveRaw` 时真正 Freeze；首次请求后 `Frozen` 为真，后续载入与重复回调都不会再触发，也不覆盖已冻结的旧权益快照。
- 同文件保留一段**设计结论注释**：记录旧经济引擎方向（`EconomyService.Enable(true)` 注入）两次被证据否决的理由（无玩家可见能力 + 会制造 cutover 待承接项 / 冻结档 Revision 变化不可解除），防止未来重复踩。

### 契约变更（`Tests/PawnInstitutionModeTests.cs`）
- `B241_SettingsDefaultFalseAndActualScribePreservesExplicitIntent` → 改写为 `CHG20261010_SettingsDefaultTrueAndScribePreservesExplicitIntent`：
  - 默认值断言 `Is.False → Is.True`；
  - 缺键回落断言 `Is.False → Is.True`；
  - **新增风险面覆盖**：显式存入 `false` 必须被 Scribe 保真为 `Is.False`（默认翻转为 true 后，"显式关闭不被覆盖"才是真正的风险面）。
- 契约另一半（Scribe 往返保真 + 缺键回落字段初值）**逐字保留**。

### 文案与验证资产同步
- `Languages/{ChineseSimplified,English}/Keyed/EconomyMode.xml`：`Settings.Use.EconomicInstitution` 与 `Settings.Detail.EconomicInstitution` 由「默认关闭 / Off by default」改为「默认开启 / On by default」，并写明"载入时自动请求；显式关闭过的存档不会被重新打开"（键集合不变，各 59 节点，中英一致）。
- `_scripts/verify-pawn-institution-mode.ps1`：① 默认值断言 `false → true`（附 ChangeID）；② B2.4.1 用例过滤器补 `CHG20261010_*`，保持既有 38 例计数。

## 三、语义边界（重要，请照此理解）

1. **默认启用 ≠ 立即可用**。默认启用让制度**载入即进入「准备中」**；`Active` 仍需通过既有的恢复证明闸门：**空档需要"保存 + 重新载入一次"**（`SaveAcceptance`，制度页顶部会显示下一步），有旧账的档还需逐项承接旧权益。这是设计闸门（"能力只由真实恢复证明"），不是缺陷。
2. **默认请求 ≠ 默认授予能力**。所有能力门（`NewCommitmentsAllowed` 等）与既有 Freeze → 承接流程一律不变。
3. **玩家意图优先**。设置为 false 的档不会被自动请求；已 Frozen/Desired 的档不被干预；玩家停用后下次载入不会被自动重开。
4. **保存副作用**：首次保存会为"从未启用经济"的档新增 FrozenLegacy 与结算快照节点（**schema 无变化**，节点缺失仍走 null→默认）。

## 四、Gate 结果与处置

| Gate | 首轮 | 处置 |
|---|---|---|
| **GATE-SAVE** | **BLOCKED** | 3 项已全部清零：① 变更标注更新至实施态（本文件）；② changelog 登记（`docs/changelog/v1.4.x_2026-10-10_经济系统默认启用.md`）；③ 验证脚本断言同步（默认值 + 用例过滤） |
| **GATE-DATA** | **BLOCKED** | 根因 = 该设置**是运行期行为门**（默认 true 会让公共仓储食品从"免费可食"切到"需 Active + 报价 + 余额"），而文案声称为"Intent only"。处置：`ChronicleSettings` 注释已如实改写（意图 ≠ 能力，但承认其驱动行为门）；玩家可见后果见 §五 P1 |
| **GATE-UI/LOC** | 随文案同步 | 键集合不变、中英双份齐、无占位符、XML 合法（59/59） |
| **GATE-TEST** | **PASS** | 编译 0 错 0 警；`dotnet test` **1285 通过 / 0 失败 / 1 跳过** |
| **GATE-DELTA** | **PASS** | 覆盖 6 个改动文件，无遗漏/超范围/残留 |

## 五、已知风险（未在本轮消除，如实登记）

1. **P1（条件性）· 公有化 × 默认经济**：默认 true + 载入自动请求后，凡启用过「公共储存区（公有化）」的档，在制度**未 Active 期间公共食品会被拒绝付费食用**（`StorageFoodRules.cs:13-14` + `EconomyStorage.cs:161-178` + `Patch_EconomyStoredFood.cs`）。这是"经济启用 ⇒ 要钱"的正常语义，但**未在玩家文案中说明"未就绪期间为何吃不了"**，也未实机验证。建议下一步：补一条储存区 tooltip 说明 + 实机验收。
2. **P2**：坏 payload（`archive.Fault != null`）的档此前仅手动可达，现在会被自动带到 `Preparing/Draining` 且**无项可承接**（展示死胡同）；建议请求前对 `original.Fault` 给出显式提示。
3. **P2**：`_scripts/verify-pawn-institution-mode.ps1:57` 的 `InstitutionReadiness` 断言与 `:107` 的 Release DLL 比对属**既有失配/前置条件**（非本轮引入），未在本轮扩大范围处理。
4. **NOT RUN**：实机验证（沙箱无游戏进程）。需要重启 RimWorld 后核对：新档/存量档载入 ⇒ 经济页显示「准备中」+ SaveAcceptance 步骤；保存重载一次后转「运行中」。

## 六、回滚

- 代码：还原 6 个文件即可（`git checkout -- <6 paths>`）。存在 **Save 侧残留**：已自动 Freeze 的档会保留 FrozenLegacy 与结算快照节点——按既有语义这属于"已请求过制度模式"，回滚后制度停在 Preparing，不会产生资金动作；如需彻底复位，需玩家在制度页清理或使用旧档备份。
- 契约：测试改名与断言翻转属**契约演进**，回滚时需一并还原为 `B241_SettingsDefaultFalse…`。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（6/6）
- [x] 清单范围 ⊆ Scope
- [x] 每个文件标注类型与摘要
- [x] Change ID 与 Gate 结论、契约测试、changelog 可互链
- [x] 无调试代码 / TODO 残留
- [x] 已用 `gate-runner.ps1` 自动校验

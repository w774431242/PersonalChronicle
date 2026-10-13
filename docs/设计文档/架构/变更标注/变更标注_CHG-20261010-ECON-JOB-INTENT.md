---
change_id: CHG-20261010-ECON-JOB-INTENT
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: Implemented
scope: 用户「要精准识别，工作任务的发布和取消」—— 劳务站待办只统计玩家亲自下达且当前仍存在的 designation
rule_ids: AI-001, AI-003, COMP-001, COMP-004, EXC-002, GOV-007, GOV-009, SAVE-003, PERF-001, TEST-001, DELTA-001
files:
  - path: Source/PersonalChronicle/Capture/Patch_DesignationSource.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyVanillaWork.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyRuntime.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  - path: docs/设计文档/架构/EXC-2026-001.md
    type: M
---

# 工作任务的精准识别（发布与取消） · CHG-20261010-ECON-JOB-INTENT

**ChangeID**：`CHG-20261010-ECON-JOB-INTENT`｜**日期**：2026-10-10｜**作者**：AI（CodeBuddy / DeepSeek-V4.1-Flash）
**用户需求（原话）**：「要精准识别，工作任务的发布和取消。」
**报障现象**：玩家取消割除标记后，劳务站待办**恒为 64**（恰为统计上限）。

---

## 一、根因（Cecil IL 实证，非推测）

```
CompAutoCut::CompTickLong()
    if (!autoCut) return;
    DesignatePlantsToCut();
CompAutoCut::DesignatePlantsToCut()
    foreach (var cell in GetAutoCutCells())
        if (CanDesignatePlant(thing))
            designationManager.AddDesignation(new Designation(thing, DesignationDefOf.CutPlant));
```
风力发电机（`CompAutoCutWindTurbine`）等建筑**每 `CompTickLong` 重新标记**范围内植物为割除 ⇒ 玩家一取消就被标回。原版 designation **无来源字段**，因此旧口径（数 designation）无法区分"玩家下达"与"建筑自维护"。

## 二、实现（4 处 + 台账登记）

| # | 改动 | 要点 |
|---|---|---|
| 1 | **新** `Capture/Patch_DesignationSource.cs` | 挂**唯一下游入口** `DesignationManager.AddDesignation`（Cecil 反查显示：各 `Designator` 子类**各自实现** `DesignateThing`/`DesignateSingleCell`，挂基类会漏）。判据 = **调用栈内存在 `Verse.Designator` 帧**（扫描 8 帧）。玩家来源 ⇒ 记录；**自动来源 ⇒ 移除该键**。只读 Prefix，绝不 `return false`；整类 try/catch；启动期签名探测 |
| 2 | `EconomyVanillaWork.cs` | `HashSet<string> playerDesignations`（键 = `defName\|目标键`）+ `RecordPlayerDesignation` / `ForgetPlayerDesignation` / `TrimPlayerDesignations`（上限 4096）+ `ExposePlayerDesignations`（存档往返）。**待办 = "键在集合 ∧ designation 当前存在"** ⇒ **取消立即下架**（无需挂移除路径） |
| 3 | `EconomyRuntime.ExposeData` | 新节点 `pawnEconomicPlayerDesignations`（`Scribe_Collections`，`LookMode.Value`） |
| 4 | 文案（中英） | 「本图待办（**你亲自下达**的砍伐 / 割除 / 采矿任务；取消一条立即下架。建筑自动清障不计入）」 |
| 5 | `EXC-2026-001.md` | 台账登记 **#25**（COMP-004 / EXC-002 / GOV-007 合规） |

## 三、GATE-ARCH 复审与 P1 处置（独立审查，BLOCKED → 逐项闭环）

审查首轮 **BLOCKED**，给 3 项 P1 与若干 P2，处置如下：

| 项 | 审查结论 | 本轮处置 |
|---|---|---|
| **P1-a 判据帧序不可靠** | 「只看首帧」会命中 Harmony 包装帧 ⇒ 几乎必然全量漏记（静默） | 判据改为**扫描栈内是否存在 `Designator` 帧**（任一命中即玩家下达）。同时审查指出的**闭包漏记**（`Designator_MineVein` 的 `<>c__DisplayClass10_0`）被同一改动覆盖（8 帧内可命中 `Designator_MineVein`） |
| **P1-b 残留键 + 自动重标 ⇒ 误纳 ⇒ 错付** | 玩家取消后键保留；建筑重标同一植株 ⇒ 二次条件重新成立 ⇒ 误计并可能实付 | **自动来源清键**：非玩家来源的 `AddDesignation` 会移除该键 ⇒ 意图已被建筑接管 ⇒ 不再计入。错付路径关闭 |
| **P1-c EXC 登记缺失 + 无启动探测** | 命中「未登记 Harmony」失败条件；无签名探测属"静默不生效"防线缺口 | 台账登记 **#25** + 新增 `TargetMethodExists` 启动探测（与 `Patch_ItemFlowPlant` 同构） |
| P2 · 档案自动来源清单 3/4 不实 | `WorkGiver_GrowerSow` / `GenConstruct` / `RoofUtility` 实际**不调用** `AddDesignation` | 档案清单已在 Patch 文件头按实证更正（只保留 `CompAutoCut` 与 `Plant.PlantCollected`） |
| P2 · 陈旧注释 | `EconomyVanillaWork` 文件头仍写"零新挂点" | 已改为如实描述（来源由 `Patch_DesignationSource` 的只读挂点捕获） |
| P2 · 披露缺失 | 旧档归零属语义变更，三处未披露 | 见 §四；文案已更新 |
| P2 · 0 单测 | 核心路径无单测 | **未做**（如实登记，见 §五） |

## 四、必须知道的语义变更（披露）

- **旧档不追溯**：旧存档没有来源节点 ⇒ 集合为空 ⇒ **载入后待办即刻为 0**，地图上既有标记**不会**被追溯为玩家下达。玩家需**重新下达**（这是诚实口径：我们从未记录过旧档的来源，预填等于把原缺陷灌进存档）。已签合同照常核销完成件，但新合同在重新下达前不会增加。
- **取消立即生效**：取消一条 ⇒ 待办立刻少 1（不再等 60 tick 缓存后的重算窗口之外的任何额外条件）。
- **自动清障不计入**：风力发电机等建筑的自动割除**不算**待办，也**不再**参与计酬签约。
- 每份件数与多合同派发（`CHG-20261010-ECON-VANILLA-CUT`）行为不变。

## 五、验证与未做

- 编译 **0 错 0 警**（`BuildStamp 2026-10-10 13:34 UTC`）；`dotnet test` **1286 通过 / 0 失败 / 1 跳过**；Cecil 反查：`Patch_DesignationSource::Prefix -> RecordPlayerDesignation`、`ExposeData -> ExposePlayerDesignations`。
- 部署：`deploy-to-game.ps1` 成功。
- **NOT RUN · 实机验证**（沙箱无游戏进程）：需重启后核对四项 —— ① 对树/草下达砍伐或割除 ⇒ 待办**立即出现**；② **取消一条** ⇒ 待办**立即 −1**；③ 风力发电机周边的自动割除**不进**待办；④ 读档后自己下达的标记**仍在**（持久化生效）。
- **未做**：`Tests/` 无本路径单测（键构建 / 去重 / 封顶 / 往返）—— 审查 P2 项，如实登记，建议下一批补。

## 六、回滚

- 删 `Capture/Patch_DesignationSource.cs` + `git checkout -- <其余 5 路径>`。
- **存档残留**：已记录的来源集合随档保留（无害，仅不再更新）；如需彻底复位，用旧档备份。

### 一致性自检（GATE-DELTA 前置）

- [x] 清单覆盖实际改动全部文件（5 源码/文案 + 1 台账）
- [x] 清单范围 ⊆ Scope
- [x] Change ID 与 Gate 结论、EXC 台账可互链
- [x] 无调试代码 / TODO 残留
- [x] 已用 `gate-runner.ps1` 自动校验

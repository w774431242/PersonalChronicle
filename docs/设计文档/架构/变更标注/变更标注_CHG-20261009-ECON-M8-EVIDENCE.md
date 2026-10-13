---
change_id: CHG-20261009-ECON-M8-EVIDENCE
date: 2026-10-09
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedAndDeployedByThisBatch
scope: U6-B1 —— M8 服务证据面收口：按类原版交付过程三态探针 + 七门改为具名事实（消除字面 true/const 断言）+ 证据三态上屏
rule_ids: AI-001, AI-002, AI-003, AI-011, GOV-006, GOV-009, GOV-011, GOV-012, GOV-013, COMP-001, DATA-001, DATA-004, ARC-001, UI-001, UI-004, UI-008, LOC-001, LOC-002, LOC-003, LOC-005, PERF-001, TEST-001
files:
  # ── 新增（2）──
  - path: Source/PersonalChronicle/Capture/ServiceProcessProbes.cs
    type: A
  - path: Tests/ServiceEvidenceTests.cs
    type: A
  # ── 修改 · 层内（3）──
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Care/CareContracts.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyCare.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.Care.cs
    type: M
  # ── 修改 · 本地化（2）──
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  # ── 治理与生成物（2）──
  - path: docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/实施批次状态.json
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# U6-B1 · M8 服务证据面收口（三态探针 + 具名事实门 + 上屏）

**ChangeID**：`CHG-20261009-ECON-M8-EVIDENCE`（U6-B1）｜**日期**：2026-10-09｜**授权**：用户「继续」

## 1. 背景（总控 AI-001 裁定，含三处输入更正）

| # | 旧表述 | 裁定 |
|---|---|---|
| C1 | 「`SettlementUnavailable` 仍无产生者」（进度源 92 行） | **不实**：真实产生点 `EconomyCare.cs:73/:76`（`CareContracts.cs:15` 定义），本批随附更正 |
| C2 | 「`FamilyParticipation` 是 `.5f` 常量占位」 | **定性错误**：`.5f` 是显式中性默认且 M9 学习通道已开（`LifePolicy.cs:53-64`）；真实缺口是**零消费方**（归 U6-B3） |
| C3 | 「四类服务能力恒停 PublicVanilla」 | 更准确：**四类从未被评估**；本批让它们的证据状态与阻断原因**上屏可读** |

## 2. 改动

| 文件 | 改动 |
|---|---|
| `Capture/ServiceProcessProbes.cs`（新） | 按类探测**原版交付过程存在性**（启动期一次、AccessTools、只读）：Treatment=`TendUtility.DoTend` 已接（CaptureWired）；Childcare=`JobDriver_Breastfeed/BottleFeedBaby`（Biotech）；Education=`JobDriver_Lessontaking/Lessongiving`（Biotech）；**Examination/Bed 无独立原版过程 ⇒ 恒 NotDeliverable**（设计 07:45 禁近似证据）。探测失败按"不存在"处理（宁少报不错报） |
| `Domain/…/Care/CareContracts.cs` | 新增 `ServiceEvidenceStatus` 三态（NotDeliverable / ProcessExistsCaptureNotWired / CaptureWired）、`ServiceEvidenceRules.HasExplanationSurface`、`ServiceReadiness.FromFacts(...)` —— **七门全部由具名事实构造** |
| `Economy/…/EconomyCare.cs` | `RefreshServiceCapability` 改为：证据面=探针、`capacityAndHours`=**执行者真实能力读值**（非 null / 未死亡 / 未倒地 / 未禁用医生工种 / 可操作）、`settlement` 与 `interruptionRecovery`=**账本可读且无故障**（`settlement.ReadAudit()`）、`uiExplanation`=解释面表。**删除 `const CareSettlementWired = true` 与两个字面 `true`**（那三门过去永远为真 ⇒ 什么都证明不了） |
| `Economy/…/Dialog_PawnEconomy.Care.cs` | 能力条每类服务追加「交付过程: <三态>」 |
| `Languages/{zh,en}/Keyed/PawnEconomy.xml` | +4 键（`Care.Evidence` + 三态标签），中英 1:1 |
| `实装落地/实施批次状态.json` | `lastChangeId`/`currentUnit`/部署哈希 + C1/C2 两处更正 |

## 3. 不变量与边界

- **无过程不得报价**：NotDeliverable ⇒ `VanillaAccess`/`DeliveryEvidence` 双假 ⇒ 永不 ReadyPaid。
- **过程在 ≠ 可收费**：ProcessExistsCaptureNotWired ⇒ 访问门真、交付门假 ⇒ 维持不收费（摄取属 U6-B2）。
- **不再有恒真门**：七门全部可被真实读值击落（含反向对照用例）。
- **本轮未做**（如实声明）：①四类的**摄取**（属 U6-B2，需新挂点 ⇒ Harmony ⇒ Architecture Review + EXC）；②照护安排本体（属 U6-B3）；③Exam/Bed 的合法过程/时间片段专项设计（设计禁止造假证据）；④**五类周期化评估扫描**（评估仍由事件驱动：Treatment 在 tend 时评估；其余四类尚无评估触发者 —— 由本批上屏的探针状态如实呈现，不冒充已评估）。

## 4. 验证基线（全部实跑）

| 项 | 判据 | 结果 | 证据 |
|---|---|---|---|
| 编译 | 输出无 `error` | **0 错 0 警** | `_scripts/compile_with_sdk.ps1` |
| 全量离线测试 | dotnet test（net48） | **1180 通过 / 0 失败 / 1 跳过（总 1181）** | `Tests/TestResults/U6B1-Evidence-V2.trx` |
| 新用例确实执行 | 按用例名逐条核对（该 trx 的 `testName` 不含类名，须按方法名匹配） | 5/5 全部执行并通过 | `NoVanillaProcess…` / `AProcessThatExistsBut…` / `AnUnreadableLedger…` / `AnIncapableExecutor…` / `EveryPublishedKind…` |
| 部署 | deploy-to-game.ps1 | Defs 36 / Languages 66 / Assemblies 1 synced；DLL `2026-10-09 23:11:06`，2,030.5 KB，SHA `E4476ECA…` | 脚本输出 |
| **实机** | 照护页证据条渲染 / DLC 门控行为 | **NOT EXECUTED** | — |

## 5. Gate（前置，两席结论见 GR 记录）

- 硬四项：**Harmony=No / Hardcoding=No / Cross-Mod=No / Static Mutable State=No** ⇒ 不触发 Architecture Review（探针为启动期只读缓存，先例 `Patch_TendUtility.TargetMethodExists`；`AccessTools.TypeByName` 只是类型查找，不新增挂点）。
- 归档：`.codebuddy/audit/gate-records/GR-CHG-20261009-ECON-M8-EVIDENCE.md`（gitignore 区，不入 DELTA 判定）。

## 6. 三态

- **阻塞：0**。
- **未做**：U6-B2（Childcare/Education 摄取 + 结算复用 + 授权 UI 放开两类：现状 `Dialog_PawnEconomy.Care.cs` 的授权按钮仍仅 Treatment ⇒ 该项随 B2 一并放开）；U6-B3（照护安排最小闭环 + `FamilyParticipation` 消费）；Exam/Bed 过程设计；实机验收。
- **做不到**：Examination/Bed 的"交付过程"在 1.6 原版不存在 ⇒ 不报价、不造证据（设计 07:45）。

## 7. 文件全集索引（front matter 逐条对应）

除 §2 表逐项点名者外：`Tests/ServiceEvidenceTests.cs`（新增夹具 5 用例：无过程不可提升 / 过程在但未摄取不构成交付证据 / 账本不可读阻断结算与恢复 / 执行者无能或未定价阻断 / 未发布类型不得主张解释面）、`docs/设计文档/功能模块/经济体系/经济需求驱动AI/实装落地/实施批次状态.json`（进度源：lastChangeId / currentUnit / 部署哈希 / C1+C2 两处更正）、`Source/PersonalChronicle/BuildStamp.g.cs`（编译自动改写，按先例列入）。

---
AI 署名：本变更标注由 AI 助手生成 —— CodeBuddy（模型 DeepSeek-V4.1-Flash），2026-10-09。

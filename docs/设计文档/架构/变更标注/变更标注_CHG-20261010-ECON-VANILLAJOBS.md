---
change_id: CHG-20261010-ECON-VANILLAJOBS
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
scope: 需求「经济岗位池扩展到原版工作岗位」的最小可用落地 —— 砍伐与采矿的真实完成接入既有劳动证据链（零新增 Harmony）
rule_ids: AI-001, AI-003, AI-005, AI-011, GOV-003, GOV-012, ARC-003, DATA-001, PERF-002, TEST-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/EconomyEmployment.cs
    type: M
  - path: Source/PersonalChronicle/Capture/Patch_ItemFlowPlant.cs
    type: M
  - path: Source/PersonalChronicle/Capture/Patch_ItemFlowMining.cs
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# 原版作业计酬（砍伐 + 采矿）

**ChangeID**：`CHG-20261010-ECON-VANILLAJOBS`｜**日期**：2026-10-10｜**部署**：见文末

## 一、需求与范围

用户需求（原话）：**"经济岗位池扩展到原版工作岗位"**（对齐自"我想要覆盖原版岗位、车间岗位、不局限一个"）。

**本轮范围（试点）**：**砍伐（`PlantCutting`）+ 采矿（`Mining`）**；计酬粒度 = **每完成 1 个作业对象计一件**；"一个 Pawn 持多份岗位"**单独立项，本轮不混做**。

## 二、★ 关键发现：**零新增 Harmony**（因此不触发 Architecture Review）

原计划以为要给砍伐/采矿各建挂点。取证后发现**项目里已经有**：

| 需求 | 既有挂点（未改动挂接目标） | 可读到 |
|---|---|---|
| 砍伐完成 | `Capture/Patch_ItemFlowPlant.cs:41` → `Plant.PlantCollected(Pawn, PlantDestructionMode)` | 完成事件 + 执行者 Pawn + **销毁模式** |
| 采矿完成 | `Capture/Patch_ItemFlowMining.cs:37` → `Mineable.DestroyMined(Pawn)` | 完成事件 + 执行者 Pawn |

签名经**反射核实**（`Assembly-CSharp.dll`，非凭记忆）：

```
Plant.PlantCollected : Void PlantCollected(Verse.Pawn, RimWorld.PlantDestructionMode)
PlantDestructionMode 成员: Smash, Flame, Chop, Cut
Mineable.DestroyMined : Void DestroyMined(Verse.Pawn)
```

⇒ **Harmony = No（不新增挂接目标，仅在既有 Postfix/Prefix 内追加一行）**、Hardcoding = No、Cross-Mod = No、Static Mutable State = No ⇒ **四问全 No ⇒ 不触发 Architecture Review、无需 EXC**。

## 三、实现（复用整条劳动证据链，不建第二条资金链）

```text
Plant.PlantCollected(mode=Chop) / Mineable.DestroyMined        ← 既有挂点，各加一行上报
  → GameComponent_PawnEconomy.ReportVanillaWorkCompletion(工作类型, 作业对象, Pawn, now)   ← 新增
    → 该 Pawn 的活跃合同是否**声明了该工作类型**（DeclaresWorkType）                        ← 与 R01 同构：那里查"设备在合同车间内"（位置证据），这里查工作类型（工作证据）
      → Employment.AcceptEvidence(WorkEvidence)                                          ← 复用
        → TrySettlePayable                                                               ← 复用 ⇒ 幂等 / 欠薪补付 / 读档保留 / 结算不重复，全部免费获得
```

**细节**：
- **证据键**：`vanilla:{工作类型}:{作业对象稳定 ID}:{PawnKey}` ⇒ 同一 Pawn 同 tick 完成**两个不同对象**各计一件；同一对象的**重复回调被去重**（作业对象 ID 跨读档稳定）。
- **只对 `Chop` 计酬**：`Cut`（割草/收割）、`Smash`、`Flame` **一律不计酬** —— 避免把毁坏/自然焚烧当劳动付钱。
- **异常隔离**：两处上报各自 `try/catch`，异常只降级为一次 `Log.Warning`，**绝不影响原版砍伐/采矿**（与 `Patch_BillProduction` 同范式）。
- **零成本返回**：无合同 / 工作类型不符 ⇒ 立即返回（绝大多数原版作业不属任何经济合同）。
- **无新 Save 字段**：沿用既有 `WorkEvidence` / `WageObligation` 结构 ⇒ **零 schema 变更、零迁移**。

## 四、⚠ 使用前提（必须知道，否则"不起作用"）

计酬的匹配条件是"**该 Pawn 的活跃合同声明了该工作类型**"，而合同的工作类型来自**车间任务类型**
（`zone.WorkshopType.WorkTypeDefNamesOf(shift.TaskType)`）。因此：

> **要让它计酬，车间的任务类型必须包含 `PlantCutting` 或 `Mining`，Pawn 需先在该机构签约上岗**；
> 直接在空地上砍树、没签任何合同 ⇒ **不计酬**（这是设计口径，不是缺陷）。

**本轮未做的**：政策级"招工声明"（让机构可**直接**招原版工、不依赖车间任务类型）—— 现方案下**不需要**它即可工作；若后续要"不建车间也能招原版工"，再单独做那一层（含政策字段 + Save 键 + UI）。

## 五、验证

| 项 | 值 |
|---|---|
| 编译 | **0 错 0 警** |
| 全量测试 | **1220 通过 / 0 失败 / 1 跳过**（`Tests/TestResults/VanillaJobs.trx`） |
| Save | **零 schema 变更** |
| 新增 Harmony 挂接目标 | **零**（仅在两处既有 Postfix/Prefix 内追加逻辑） |
| 实机 | **NOT EXECUTED** |

**无离线测试面**：需真实 `Plant` / `Mineable` / Pawn 与合同 ⇒ 端到端（签约后砍树/挖矿 ⇒ 应付生成 ⇒ 结算到账 ⇒ 读档保留）须**游戏内**验证（GameVerified 未达）。

---
AI 署名：本变更标注由 AI 助手（CodeBuddy / DeepSeek-V4.1-Flash）起草，2026-10-10。

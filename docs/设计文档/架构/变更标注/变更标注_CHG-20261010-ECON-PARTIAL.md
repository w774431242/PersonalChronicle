---
change_id: CHG-20261010-ECON-PARTIAL
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
scope: 修复"感知组 Partial 被判死 ⇒ 规划永久 FactsUnavailable ⇒ 经济整链停摆"（实测定位的根因修复）；同批优化诊断窗排版
rule_ids: AI-001, AI-003, AI-011, GOV-002, GOV-003, UI-003, LOC-001, TEST-001
files:
  - path: Source/PersonalChronicle/Application/PawnEconomy/Perception/PerceptionQuery.cs
    type: M
  - path: Source/PersonalChronicle/Domain/PawnEconomy/Planning/ShadowPlan.cs
    type: M
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Window_PawnEconomyDiagnostics.cs
    type: M
  - path: Tests/PawnPerceptionCoordinatorTests.cs
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# Partial 语义修复（"无法自主"根因）

**ChangeID**：`CHG-20261010-ECON-PARTIAL`｜**日期**：2026-10-10

## 一、实测证据（诊断窗组级读数，tick≈167488）

```
Vikinger (Thing_Human106)   decision=WaitingFacts  reason=FactsUnavailable  opportunities=0
  facts: readiness=Partial  usable=False
    Context:       state=Complete age=40  required=True  readiness=Ready     (Observed)
    Needs:         state=Partial  age=40  required=True  readiness=Partial   (UnsupportedNeed 类)
    Relationships: state=Complete age=400 required=False readiness=Ready     (Observed)
```

**判读**：`Context` 完全就绪；**唯独 `Needs` 因"部分需求不支持/被截断"呈 `Partial`**；`Relationships` 非必需、且就绪。三名殖民者**全部**如此。

## 二、缺陷链（为什么这会让经济彻底停摆）

1. `ShadowPlan` 的 `FactsUnavailable` 判据要求 `Needs.State == Complete` **且** `facts.RequiredGroupsUsable == true`；
2. `PerceptionQuery.QueryFacts` 里 `usable` 只认 `Readiness == Ready`；
3. 而 `Assess` 把 `CaptureState.Partial` 映射为 `PerceptionReadiness.Partial` ⇒ **`usable = false`**；
4. ⇒ 只要**任意一个**需求的 `StandardRatio` 不受支持（`VanillaPawnFactCapture` 的 `unsupported`/`failed`/`truncated` 任一非空，模组环境下极常见），`Needs` 就恒 `Partial` ⇒ **规划永久 `WaitingFacts`** ⇒ 不签约、不购买、不照护、不生产 ⇒ **"无法自主"**。

**关键反证**：采集侧指标一切正常（`oldestDueAgeTicks=0`、`omitted=0`、`errors=0`、`captures` 与会话时长自洽）⇒ 问题**不在采集**，而在**判据过严导致的饿死**。

## 三、修复（语义分离：可用性 ≠ 完整性）

| 位置 | 改动 |
|---|---|
| `PerceptionQuery.cs` | `usable` 增加放行 `Partial` —— **可用性只看"有没有数据"** |
| `ShadowPlan.cs` | 判据从 `State != Complete` 放宽为 `State != Complete && State != Partial`（`Unknown`/`Error` **仍严格拒绝**） |

**设计原则**：`Readiness` 如实报告不完整性（`Partial` 不被伪装成 `Ready`），但**不得**据此判死消费者 —— 规划消费的是条目列表本身，缺几条不应作废整份计划。两处修改都保留了硬拒绝路径（没有数据就是不能用）。

## 四、测试

`PartialRequiredNeedGroupCannotBeUsedAsCompleteDemand`（钉住旧语义）→ 重写为
`PartialRequiredGroupStaysUsableButIsStillReportedAsPartial`，断言三件事：
① `Readiness` 仍为 `Partial`（诚实报告）；② `RequiredGroupsUsable == true`（数据可用）；③ **反向对照**：`Unknown` 仍硬拒（"没有数据必须挡住"）。

## 五、同批 UI 优化（用户要求）

诊断窗组级明细改为**可达性标记 + 降噪**：必需且就绪 `✓` / 必需但未就绪 `⚠` / 非必需 `·`，`age` 与 `required|optional` 同行，`usable=False` 直接追加"⚠ 规划将判 FactsUnavailable"。

## 六、验证

| 项 | 值 |
|---|---|
| 编译 | **0 错 0 警** |
| 全量测试 | **1220 通过 / 0 失败 / 1 跳过**（`PartialFix2.trx`） |
| 语义变更 | 1 条既有测试按新语义重写（含反向对照），**非削弱断言** |
| 实机 | **NOT EXECUTED** —— 需重启游戏后确认规划离开 `WaitingFacts` |

---
AI 署名：本变更标注由 AI 助手（CodeBuddy / DeepSeek-V4.1-Flash）起草，2026-10-10。

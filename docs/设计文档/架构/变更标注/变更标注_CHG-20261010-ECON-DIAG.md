---
change_id: CHG-20261010-ECON-DIAG
date: 2026-10-10
author: AI（CodeBuddy / DeepSeek-V4.1-Flash）
state: ImplementedNotDeployed
scope: 游戏内**经济诊断窗**（只读）—— 把"为什么不动"的判据直接上屏（感知捕获健康度 / 制度门 / 每名殖民者的规划判决与年龄 / 关键计数），支持一键写入日志随反馈贴回
rule_ids: AI-001, AI-003, AI-011, GOV-003, GOV-010, UI-001, UI-003, LOC-001, PERF-002, TEST-001
files:
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Window_PawnEconomyDiagnostics.cs
    type: A
  - path: Source/PersonalChronicle/Economy/PawnEconomy/Dialog_PawnEconomy.cs
    type: M
  - path: Languages/ChineseSimplified/Keyed/PawnEconomy.xml
    type: M
  - path: Languages/English/Keyed/PawnEconomy.xml
    type: M
  - path: Source/PersonalChronicle/BuildStamp.g.cs
    type: M
---

# 游戏内经济诊断窗

**ChangeID**：`CHG-20261010-ECON-DIAG`｜**日期**：2026-10-10｜**部署**：**已部署**（三方哈希一致）
**当前程序集**：`Assemblies/PersonalChronicle.dll` SHA-256 `E440288EF435AFAA33E670548F9D83A8C5C81C1E0195C6A8EFA2C88992C767E4`（= 游戏目录 DLL）

## 一、为什么做它（实测反馈驱动）

玩家反馈：**"无法自主"** —— 决策页显示「拟选行动 = 等待新需求事实 / 原因 = 事实缺失」，但**看不出缺的是哪一层的事实**，只能翻 `Player.log`（而日志常被新会话覆盖）。诊断信息必须**在游戏内可得**。

## 二、交付内容

`Window_PawnEconomyDiagnostics`（新文件，只读）+ 经济窗口右上「经济诊断」按钮（`Dialog_PawnEconomy.cs`）。

显示四块：

| 块 | 内容 | 定位价值 |
|---|---|---|
| **感知** | activeSubjects / retired / omitted / captures / errors / **budgetExceededTicks** / lastCaptureMs / lastPopulationMs / 人口快照 tick 与完整性 / **★ oldestDueAgeTicks** | `oldestDueAgeTicks > 300` 会直接标注"已超过 ShadowPlan 的 300 tick 判据"——这是"感知是否卡住"的**唯一直接指标** |
| **制度** | phase / desired / revision / fault / newCommitmentsAllowed / cutoverFrozen / outstandingRights / blockers | 区分"制度门未开"与"感知缺事实" |
| **殖民者规划**（前 4 名） | decision / reason / builtTick 与 age / opportunities / selected / employmentApplicable / institutionallyBlocked | 直接对照截图里那句"等待新需求事实 / 事实缺失" |
| **写入日志** | 一键 `Log.Message("[PC][Diag]…")` | 随反馈贴回即可定位 |

**只读纪律**：不写任何经济状态、不改感知会话、不触发规划、不影响 Tick；文本每 30 tick 重建一次（非每帧拼接）。显示内容是**技术读数**（稳定 ID / 枚举名 / 计数），刻意不经本地化管道（与 `ReasonLabel` 对未映射码的处理同一口径）；标题与按钮仍 Keyed（4 新键，双语各 **251** 一致）。

## 三、本轮排查记录（**4 个假设全部排除**，如实登记）

为定位"感知快照陈旧 ⇒ `WaitingFacts`"，逐一读了感知链：

| 假设 | 核实结论 |
|---|---|
| 调度"顺序饿死"（后面的主体永远轮不到） | **排除** —— `scheduled` 是 `SortedSet` + `Entry.Due = Min(DirtyDue, FastDue, BodyDue, RelationshipDue)` ⇒ 最旧优先，公平 |
| `Capture` 漏把主体排回队列 | **排除** —— 第 171 行无条件 `scheduled.Add(entry)`；`Dead/OffMap` 走 `Retire`（正确退场） |
| 主体上限（`MaximumSubjects`）超限 | **排除** —— 默认 **4096**；且 `Eligible` 只收**自由殖民者**（排除动物/访客/囚犯），用户殖民地仅数人 |
| 人口扫描状态机卡死（`censusRunning` 不复位） | **排除** —— 第 103 行正确复位并设 `nextCensus` |

**⇒ 根因仍未锁定**，剩余可疑面（需诊断窗输出或日志）：`EconomyShadowPlanning.ReadInput` 中 `TryGetPlanningPawn` 的失败路径会把 `protection`/`employment` 留作 `Unknown`；以及 `QueryFacts` 的逐组 `Assess` 判定。**未凭猜改动感知内核**（它服务整个规划链，盲改风险高于收益）。

## 四、验证

| 项 | 值 |
|---|---|
| 编译 | **0 错 0 警** |
| 本地化 | 双语各 **251** 键、计数一致、无 BOM、4 新键可解析 |
| 全量测试 | 随部署批一并跑（见部署回执） |
| 实机 | **NOT EXECUTED**（诊断窗观感与内容正确性待重启确认） |

## 五、怎么用它定位"无法自主"

1. 经济窗口 → 右上「**经济诊断**」；
2. 看 **感知**块的 `★ oldestDueAgeTicks`：
   - **> 300** ⇒ 感知确实堆积（问题在感知层）；
   - **≈ 0 而规划仍 `WaitingFacts`** ⇒ 问题在**读取/判定**侧（`ReadInput` 或 `Assess`），不在采集侧；
3. 看 **`omitted`**：非 0 ⇒ 有主体被丢弃（虽然 4096 上限下不太可能）；
4. 看 **制度 blockers**：非空 ⇒ 是制度门问题而非感知；
5. 点「写入日志」把整块贴回给我 —— 一次即可定位。

## 六、实测日志回转（2026-10-10，`Player.log`）

玩家点击「写入日志」后取得的 `[PC][Diag]` 段（tick=168086）：

```
感知: active=3 retired=0 omitted=0  captures=43 errors=0 budgetExceededTicks=1
      人口快照 tick=167889 完整=True   ★ oldestDueAgeTicks=0
制度: phase=Active desired=True revision=12  newCommitmentsAllowed=True
      cutoverFrozen=True outstandingRights=False  blockers=（无）
规划: Vikinger / James / Justin  三人全部 decision=WaitingFacts reason=FactsUnavailable
      builtTick≈167939 age≈147  opportunities=0
```

**结论（数据说话）**：

1. **采集侧健全** —— `oldestDueAgeTicks=0`、`omitted=0`、`errors=0`，且 `captures=43` 与会话时长自洽（3 主体 × ~14 轮 × 150 tick）⇒ **感知在正常节奏采集**，"感知卡住"假设被实测排除。
2. **制度门已开** —— `blockers=（无）`、`newCommitmentsAllowed=True` ⇒ 与机构窗显示的"营业：非营业时段"无关（那只影响**机会挂牌**，不影响事实可用性）。
3. **⇒ 问题锁定在判据层**：`ShadowPlan` 的 `FactsUnavailable` 有 7 个触发条件，采集侧与时间戳条件均已排除，只剩 **`snapshot.Context.State != Complete` / `snapshot.Needs.State != Complete`**。规划只请求 `Context|Needs`，**阈值 300 > 采集周期 150**，正常情况下绝不可能因"陈旧"触发。
4. `[ECON-PERF] sourceErrors=0` ⇒ `ReadInput` **未抛异常** ⇒ 是"返回了不合规结果"而非"读取失败"。

**三个候选成因（待组级读数定死）**：`Needs` 恒 `Unknown`（`"NeedTrackerUnavailable"`，`pawn.needs` 为空）；戳校验降级为 `Error`（`"InvalidSourceStamp"`，`VanillaPawnFactCapture` 回传的 `CapturedTick`/`Revision` 与协调器期望不符）；或 `Partial`（部分需求读不出 ⇒ 非 `Complete`）。

**据此增强诊断窗**（本次同批）：Pawn 段追加 **判据层组级明细** ——
`facts: readiness / reason / usable` + 每组 `state / age / required / readiness / reason`，
使一次「写入日志」即可把上面三个候选收敛为一个。

---
AI 署名：本变更标注由 AI 助手（CodeBuddy / DeepSeek-V4.1-Flash）起草，2026-10-10。

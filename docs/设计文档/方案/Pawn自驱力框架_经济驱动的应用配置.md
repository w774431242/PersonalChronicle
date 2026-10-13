# Pawn 自驱力框架 —— 经济驱动的应用配置

> 场景：后期引入**现实向经济体系**。pawn 有「住房」概念，该需求随时间单调增强，
> 迫使 pawn 长时间工作换取白银，购置住房、家具等。
> 本文回答：**在这个场景下，自驱力该怎么配。**
>
> 推导依据始终是原版程序集（`_research/vanilla/Assembly-CSharp/`）；MOD 仅作参考。
> 本文是 SDD 的**应用配置篇**，出现冲突时以 SDD 为准。

---

## 0. 结论摘要

经济场景是整个自驱力框架**压力最大的一个用例**——它同时具备三个别的 drive 没有的特征：

| 特征 | 现有 SDD 能否表达 |
|---|---|
| 需求**随时间越来越强** | ✅ 能，且**不该自己写**——交给原版 `NeedDef.fallPerDay`（`NeedDef.cs:111`） |
| 目标**无法由单个 Job 完成**（工作 N 次 → 攒够 → 交易 → 拥有） | ❌ **不能**。现有 Intent 是「一次 Job」模型，装不下。**需新增 Goal 层**（§4） |
| 需要「这个 pawn 拥有什么 / 有多少钱」 | ⚠️ 原版**几乎没有**，只有两个锚点可用（§1.2） |

**配置的总原则一句话**：

> **压力交给原版 Need 去积累，跨度交给新的 Goal 层去追踪，仲裁仍然只认 AutonomyTier——
> 并且住房必须永远排在吃饭之后。**

第三句是安全底线。经济需求一旦「越级」，pawn 会工作到饿死或精神崩溃，
这正是 §5 要卡死的地方。

---

## 1. 原版能给什么、不能给什么

### 1.1 先分清「原版事实」与「需要自研」

| 你的需求 | 原版有无 | 证据 |
|---|---|---|
| 需求随时间衰减（=随时间变强） | ✅ **现成** | `NeedDef.fallPerDay`，默认 `0.5f`（`NeedDef.cs:111`） |
| 固定节拍结算需求 | ✅ **现成** | `Need.NeedInterval()` 抽象方法（`Need.cs:145`） |
| 每 pawn 独立的 need 状态 + 自动存档 | ✅ **现成** | `Need` 自带 Scribe，`CurLevelPercentage` 可读写（`Need.cs:81,188`） |
| 「这张床是这个 pawn 的」 | ✅ **现成** | `CompAssignableToPawn_Bed.TryAssignPawn`（`:57`） |
| per-pawn **可累积的数值资源** | ⚠️ 仅一处 | `Pawn_RoyaltyTracker.favor`（`Dictionary<Faction,int>`，`GainFavor` `:364`、`RefundPermits` `:233`） |
| pawn **持有白银** | ❌ **没有** | `ThingDefOf.Silver`（`ThingDefOf.cs:17`）是**物品**不是资源，归 colony 共管 |
| pawn **自主发起交易** | ❌ **没有** | 交易是 colony / player 层行为 |
| pawn **主动加班**（越过玩家排的工作顺序） | ❌ **没有** | 工作顺序由 `Pawn_WorkSettings` 承载，属玩家控制权（`:267`） |

### 1.2 ★ 两个可用锚点（这决定了「钱」和「房」怎么落地）

**锚点 A —— 房产用 `OwnedBed`**
不要把「住房」设计成一个自研的抽象概念。原版`pawn 拥有某物`的唯一成熟机制就是
`CompAssignableToPawn`（含 `_Bed` / `_Throne` / `_MeditationSpot` / `_Grave` / `_DeathrestCasket`）。
⇒ **住房/家具所有权应挂 `CompAssignableToPawn`**，好处：原版 UI、存档、指派/回收逻辑全部白拿。

**锚点 B —— 账户照抄 `Pawn_RoyaltyTracker.favor` 的模式**
原版唯一「每 pawn 一份、可累积、可消费、带变动回调」的资源就是 honor。
它给出的可抄结构是：

- 存储 `Dictionary<Faction, int>`
- 变更走专门的入口方法（`GainFavor` / `RefundPermits`），**不直接改字典**
- 有变更回调 `OnFavorChanged`（`:383`）

⇒ 个人账户应实现为**受控的写入口 + 变更事件**，不要让 drive 直接增减数字，
否则「谁扣了我的钱」将无法追责（这也是 M9 观测的前提）。

> ⚠️ 注意：`favor` 是按 **Faction** 分桶的。要不要照搬这个维度取决于你的经济模型
> （若只有 colony 内部货币，单值足够）。此处仅借用其**结构性**做法。

---

## 2. 三层配置模型

经济场景必须拆成三层，**每层的职责不许越界**：

```
┌─ 第 1 层：压力源 ────────────────────────────────────┐
│  Need_Shelter : Need                                 │
│  · 何时变强、变多快 —— 全部由原版 fallPerDay 驱动     │
│  · 只暴露一个 CurLevelPercentage                     │
│  ❌ 不产生任何 Job，不知道工作是啥                    │
└────────────────────┬─────────────────────────────────┘
                     │ 读需求百分比
┌────────────────────▼─────────────────────────────────┐
│  第 2 层：目标（新增 AutonomyGoal）                   │
│  · 把「买房」拆成阶段：攒钱 → 选购 → 拥有 → 维持      │
│  · 追踪 cross-job 的进度（存了多少钱）                │
│  · 按当前阶段产出**一个** Intent                      │
│  ❌ 不自己维护「时间」，那是 Need 的事                │
└────────────────────┬─────────────────────────────────┘
                     │ Intent
┌────────────────────▼─────────────────────────────────┐
│  第 3 层：Intent → 仲裁 → Job（SDD 既有，不改）       │
│  AutonomyTier + 排序键                                │
└──────────────────────────────────────────────────────┘
```

**这条边界是整个配置的命门**：如果让 Goal 自己用 tick 计时「我多久没房了」，
就会和原版 Need 的 Scribe 存档、seeker 回落、`freezeWhileSleeping`（`NeedDef.cs:120`）
全部脱节。**让 Need 负责时间，是零自研的关键取舍。**

---

## 3. 第 1 层：压力源配置（借力，几乎零自研）

### 3.1 NeedDef

```xml
<NeedDef>
  <defName>PAF_Shelter</defName>
  <needClass>PawnAutonomy.Needs.Need_Shelter</needClass>
  <label>PAF_Need_Shelter_Label</label>            <!-- 翻译键，C5 -->
  <description>PAF_Need_Shelter_Desc</description>

  <!-- 「随时间越来越强」由它表达：默认每天掉 0.5 ⇒ 约 2 天见底 -->
  <fallPerDay>0.12</fallPerDay>                    <!-- ⚠️ 建议值，需实测 -->
  <showOnNeedList>true</showOnNeedList>
  <scaleBar>true</scaleBar>

  <!-- ↓ 见 §5.2 的冲突：**保持 false 是刻意的**，不要为了让它更紧迫而改 true -->
  <major>false</major>

  <colonistsOnly>true</colonistsOnly>
  <freezeWhileSleeping>false</freezeWhileSleeping>
</NeedDef>
```

> ⚠️ `fallPerDay=0.12` 是我的**建议起点**（约 8 天从满到空），不是原版数值。
> 真实取值取决于你希望「多久不买房开始难受」，必须实机调。

### 3.2 「越来越强烈」：在 `NeedInterval()` 里做**加速衰减**

线性衰减只能表达「稳定地变强」。用户要的是**随时间推移压力递增**（越拖越焦虑）。
做法：覆写原版自己的结算钩子，把衰减速率做成「无房时长」的函数——

```csharp
// 草案：PawnAutonomy.Needs.Need_Shelter
protected override void NeedInterval()
{
    bool hasHome = ResolveOwnership(pawn);   // 锚点 A：OwnedBed / 自有家具

    if (hasHome)
    {
        // 有房则缓慢回补（相当于原版 seeker 的正向补给）
        CurLevelPercentage = Mathf.Min(1f, CurLevelPercentage + risePerInterval);
        homelessTicks = 0;
        return;
    }

    homelessTicks += 150;                                   // 与 SDD M4 心跳一致
    float days    = homelessTicks / 60000f;
    float ramp    = 1f + PRESSURE_K * Mathf.Min(days, RAMP_CAP_DAYS);  // 递增压力
    CurLevelPercentage = Mathf.Max(0f, CurLevelPercentage - def.fallPerDay * 0.5f * ramp);
}
```

> `homelessTicks` **必须 Scribe 持久化**（SDD M8 / G9），否则读档后压力曲线归零。
> `PRESSURE_K` / `RAMP_CAP_DAYS` 是**待定 tuning**，见 §8。

**为什么 ramp 要有 cap**：不封顶时，长期无房的 pawn 会把需求瞬间压到 0 并长期钉死，
导致 §5 的 tier 映射失去分辨率（全是同一档，排队退化成纯 FIFO）。
封顶后「最惨」是一个稳定值，而不是无界增长。

---

## 4. 第 2 层：目标层（**这是对 SDD 的新增，尚未批准**）

### 4.1 为什么现有 Intent 装不下

SDD M2 的 Intent 定义是「一个待满足的意图 → 产出一个 Job」。它对吃饭完美适用：
饿了 → `JobGiver_GetFood` → 吃完结束。

但「攒钱买房」是：**working N 次都不是在解决这个目标，只是在推进它**。
一次 job 结束后目标**仍未满足**，且必须换一类 job（工作 → 交易）。
用 Intent 表达它，只有两条路，都是错的：

| 硬塞方式 | 后果 |
|---|---|
| 每次都投递「去工作」Intent | 无法知道何时「够了」，也不会切到交易；且每次都是全新意图，老化/去重失效 |
| 投递一个超长 Job 包揽到底 | 违反 SDD C3（Job 契约）；且 Job 一旦被玩家/他人打断，攒钱进度全丢 |

⇒ **需要一个在 Intent 之上、跨 Job 存活的目标层。**

### 4.2 提案：`AutonomyGoalDef`

```xml
<AutonomyGoalDef>
  <defName>PAF_Goal_AcquireHome</defName>
  <label>PAF_Goal_Home_Label</label>                <!-- 翻译键，C5 -->

  <!-- 目标由哪个需求驱动（复用第 1 层） -->
  <needDriving>PAF_Shelter</needDriving>

  <!-- 阶段机：每帧只处于一个阶段，只有 Current 阶段会产 Intent -->
  <stages>
    <StageHomeless>
      <tier>Wanted</tier>                     <!-- ⚠️ 建议值；见 §5 -->
      <intent>PAF_Intent_EarnMoney</intent>
    </StageHomeless>
    <StageSaving>
      <tier>Wanted</tier>
      <intent>PAF_Intent_EarnMoney</intent>
    </StageSaving>
    <StageCanAfford>
      <tier>Urgent</tier>                     <!-- 攒够了 → 提档，别继续傻干 -->
      <intent>PAF_Intent_PurchaseHome</intent>
    </StageCanAfford>
    <StageOwned>
      <tier>Idle</tier>                       <!-- 拥有后降到 Idle，靠 §3.2 回补 -->
      <intent></intent>
    </StageOwned>
  </stages>

  <!-- 进度：跨 Job 累积，必须持久化 -->
  <progressSource>AccountBalance</progressSource>
</AutonomyGoalDef>
```

### 4.3 Goal 与既有模块的关系（对 SDD 的增补提议）

| SDD 模块 | 变化 |
|---|---|
| **M2 意图模型** | **新增** `AutonomyGoal`：持有 `stage` / `progress` / 当前产出的 Intent 引用 |
| **M1 注册** | 新增 `AutonomyGoalDef` 注册；goal 注册时校验 `needDriving` 存在 |
| **M3 仲裁器** | **不变**。Goal 只是 Intent 的生产者，仲裁器仍然只看 Tier + urgency |
| **M4 心跳** | 心跳需**先推进 Goal（切阶段），再收 Intent** —— 顺序不能反 |
| **M8 持久化** | goal 的 `stage` + `progress` 需纳入 schema 版本（G9） |

> 这样 M3 完全不受影响 —— Goal 层的全部复杂度被隔离在 M2 之内。
> 这是刻意的：**仲裁器必须保持为一个纯函数**（G8 / 提级不返工的前提）。

---

## 5. 第 3 层：Tier 配置 —— 安全底线在这里

### 5.1 必须落在哪一档

```
T0 Critical  吃饭 / 睡觉 / 崩溃边沿     ← 住房永远不许到这里
T1 Urgent    娱乐不足 / 戒断           ← 只有「已攒够、去购买」这一步可以上位
T2 Wanted    ★ 住房的常态主场
T3 Idle      默认
T4 Whim      纯改善（好看的家具之类）
```

### 5.2 ★ 一处反直觉但正确的冲突结论

回顾分级文档 §「映射规则」里的 **R6： `major=false` 的需求，档位上限压到 T2。**

直觉反应是「住房很重要，应该改成 major=true 让它更有力」。**不要改。**

理由正是 R6 想保护的东西：

- `major` 在原版里**只用于 UI 分组与场景部件**（`NeedsCardUtility.cs:43-45`、`ScenPart_SetNeedLevel.cs:30`），
  改成 true 只是让它显示更醒目，**不会带来任何 AI 语义**；
- 而保持 `major=false` ⇒ 住房被 R6 钉在 **T2 及以下** ⇒
  **只要 Food 进 T0 / Joy 进 T1，住房就自动让位**。

也就是说：**`major=false` 恰好就是「不能为了买房饿死」这条规则的载体。**
这个限制不是缺陷，是保险。本文明确建议保持 false。

### 5.3 老化不会让住房越级（复核一遍）

争议点：住房意图在队列里排很久，会不会靠老化爬到 T0？

不会。排序键沿用 `(MaxTier - tier) × TIER_WEIGHT`（= 100000），老化上限 `AgingCap = 0.9`：

```
一日 60000 tick × 0.0005老化/tick = 30
档间距                            = 100000
⇒ 差 4 个数量级，老化永远跨不了档
```

反向铁律仍然成立：**`AgingCap ≥ TIER_WEIGHT` 则分级失效**，须由代码或 `ConfigError` 兜住。

### 5.4 「长时间工作」的正确实现方式 ⚠️

用户要的是「**迫使 pawn 长时间进行工作**」。这里有一个会毁掉体验的做法：

> ❌ **不要直接改 `Pawn_WorkSettings` 的工作优先级**
> （`Pawn_WorkSettings.cs:267` 是玩家排的顺序）

理由：① 抢玩家控制权；② 玩家下次打开工作表会发现设置被改了且不知道为什么；
③ 与其他调优先级的 MOD 冲突（这正违背框架的差异化价值）。

> ⚠️ **例外（EXC-2026-012 E2 + EXC-2026-013 E2，2026-10-02）**：本禁令的适用范围**收窄为「工作优先级排序」**，
> 仅对**车间排班联动域**开放两条受控通道：
> ① **受控 Enable**（012）—— 值勤类型 `PC_WorkshopOperation` 由 `0 → 3`；
> ② **受控档位提升**（013）—— 车间类型 Def 映射的**原版**工作类型升到**最高档 1**
> （含 `0 → 1` 重开与 `2/3/4 → 1` 覆盖）；原版仅在「手动优先级」模式下读取档位，
> 故简单模式（`!useWorkPriorities`）下**不做档位写入**，改由车间面板状态行 + 「开启手动优先级」按钮
> 由玩家主动开启（不自动改写全局设置）。
> 两通道均仅事件驱动 / 打开面板对账；禁每 tick、禁绘制路径、禁 `Disable`、禁重排其它类型。
> **自驱力框架自身不得走此例外**（§5.4 的 ✅ 正确做法仍是本框架的唯一实现路径）。

✅ **正确做法**：让 Goal 产出的 Intent 落到**框架自己的 `JobGiver`**，直接给出具体 Job。
效果是「这个 pawn 自己决定去加班」，而玩家的优先级表**一个字都没动**。

> 这与 Baal 的做法同构（意图引擎决定意图，树只做翻译）。

### 5.5 群体治理：破产保护

每个 pawn 都自驱攒钱 ⇒ **可能集体掏空 colony 白银**。这不是 bug 而是经济 mods 的通病。
建议在第 2 层加一个治理钩子（提议入 M10）：

| 机制 | 说明 |
|---|---|
| **预算配额** | 框架按 colony 储量给每个 pawn 分配上限，而非让 pawn 无限取银 |
| **收敛阈值** | colony 白银低于阈值时，全部 Mehousing goal 降档到 Idle |
| **并发上限** | 同时允许几个 pawn 处于「攒钱」阶段，避免全员同时停工挣钱 |

> ⚠️ 具体配额公式未设计，属未决（§8）。

---

## 6. 完整配置草案（示意）

> 均为**草案**：字段名沿用 SDD / 分级文档 §6，`AutonomyGoalDef` 为本文新增且未实现。

```xml
<!-- ① 压力源：交给原版 -->
<NeedDef>
  <defName>PAF_Shelter</defName>
  <needClass>PawnAutonomy.Needs.Need_Shelter</needClass>
  <fallPerDay>0.12</fallPerDay>
  <major>false</major>                    <!-- 刻意，见 §5.2 -->
  <label>PAF_Need_Shelter_Label</label>
</NeedDef>

<!-- ② 目标：跨 Job 的阶段机 -->
<AutonomyGoalDef>
  <defName>PAF_Goal_AcquireHome</defName>
  <needDriving>PAF_Shelter</needDriving>
  <stages><!-- 见 §4.2 --></stages>
  <progressSource>AccountBalance</progressSource>
  <budgetCap>PAF_Budget_Default</budgetCap>   <!-- §5.5 -->
</AutonomyGoalDef>

<!-- ③ 阶段产出物：普通 Intent，走既有仲裁 -->
<AutonomyDriveDef>
  <defName>PAF_Intent_EarnMoney</defName>
  <label>PAF_Intent_Earn_Label</label>
  <tier>Wanted</tier>                     <!-- 静态档，被 goal 覆盖时以 goal 为准 -->
  <maxConcurrent>1</maxConcurrent>
</AutonomyDriveDef>

<AutonomyDriveDef>
  <defName>PAF_Intent_PurchaseHome</defName>
  <label>PAF_Intent_Buy_Label</label>
  <tier>Urgent</tier>                     <!-- 攒够了才上位 -->
  <maxConcurrent>1</maxConcurrent>
</AutonomyDriveDef>
```

对应的 Tier 映射（并入分级文档的主表）：

| 阶段 | Tier | 理由 |
|---|---|---|
| 已拥有 | Idle | 无压力，靠 §3.2 回补 |
| 无房初期 | Wanted | 受 R6 压制，不如吃饭紧急 |
| 攒够可买 | Urgent | 兑现窗口，别继续苦干 |
| **任何时候** | **永不 Critical** | 硬约束：房子不是氧气 |

---

## 7. 实施时的四个硬坑

1. **不要让 Goal 自己计时** —— 时间归 Need（§2）。否则与原版 Scribe / seeker / sleeping 冻结全脱节。
2. **不要碰 `Pawn_WorkSettings` 的「优先级排序」**（重排 / 改档）—— 见 §5.4。
   车间排班联动例外见 §5.4：`EXC-2026-012`（受控 Enable，值勤类型 0→3）
   + `EXC-2026-013`（受控档位提升，映射的原版类型 → 最高档 1；含"重开被关闭项"；
   简单模式下不写档位、由面板按钮由玩家开启手动优先级）。风险与回滚见 013。
3. **`homelessTicks` 必须持久化** —— 不存的话每次读档压力曲线重来.
4. **ramp 必须封顶** —— 否则长期无房者全挤在同一档，排队退化成 FIFO，分级失效。

另需注意（尚未解决）：多个 pawn 同时决策「买同一张床 / 同一件家具」时的
**目标冲突消解**。原版 `CompAssignableToPawn` 有 `AssignedPawns` 上限可挡一部分，
但「谁先得到」不由框架决定 —— 这是个开放问题。

---

## 8. 提议与未决

### 8.1 对 SDD 的增补提议（需批准后并入）

1. **新增 M12 目标层**（或并入 M2）：`AutonomyGoalDef` + `AutonomyGoal`，职责为跨 Job 的进度与阶段切换。
2. **M4 心跳顺序**固化为：**推进 Goal → 收集 Intent → 仲裁**，顺序写进规范。
3. **M10 增加群体治理预算钩子**（§5.5）。
4. **M8 schema** 预留 goal 的 `stage` / `progress` / `homelessTicks` 三个字段及默认值。

### 8.2 未决 / 存疑

| # | 项 | 状态 |
|---|---|---|
| U1 | `fallPerDay = 0.12` 合适值 | 建议起点，**需实机调** |
| U2 | `PRESSURE_K` / `RAMP_CAP_DAYS` | 未设值，需实测「 homeless 多久开始焦虑」 |
| U3 | 目标冲突消解（多 pawn 抢同一物件） | **未设计** |
| U4 | colony 破产保护配额公式 | **未设计** |
| U5 | 账户是否需要按 Faction 分桶（照搬 favor 与否） | 取决于经济模型，未定 |
| U6 | `AutonomyGoalDef` 是否并入 M2 还是独立 M12 | **提议未批准** |

---

## 附录：本文引用的原版源码清单

| 论断 | 证据 |
|---|---|
| 需求随时间衰减 | `RimWorld/NeedDef.cs:111`（`fallPerDay = 0.5f`） |
| 需求结算钩子 | `RimWorld/Need.cs:145`（`abstract NeedInterval()`） |
| 需求值可读写 | `RimWorld/Need.cs:81,188` |
| 睡眠/崩溃冻结 | `RimWorld/NeedDef.cs:120,123` |
| 「自己的床」 | `RimWorld/CompAssignableToPawn_Bed.cs:57`（`TryAssignPawn`）、`:51`（`AssignedAnything`） |
| per-pawn 可累积资源 | `RimWorld/Pawn_RoyaltyTracker.cs:233,251-254,364,383` |
| 白银是物品不是资源 | `RimWorld/ThingDefOf.cs:17` |
| 工作顺序属玩家 | `RimWorld/Pawn_WorkSettings.cs:267` |
| `major` 不参与 AI | `RimWorld/NeedsCardUtility.cs:43-45`、`ScenPart_SetNeedLevel.cs:30` |
| 排序键 / 老化安全 | 分级文档 §3–§4 |

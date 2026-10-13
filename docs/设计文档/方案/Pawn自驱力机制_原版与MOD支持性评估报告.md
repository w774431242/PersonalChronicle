# Pawn 自驱力机制 —— 原版代码遍历与 MOD 生态支持性评估报告

| 项 | 值 |
|---|---|
| 报告日期 | 2026-09-14 |
| 目标游戏版本 | RimWorld **1.6.4871 rev590** |
| 研究问题 | 「原版 pawn 需求只能由玩家解决、缺少 pawn 自驱力」——该机制在 1.6 的原版代码与 MOD 生态中是否具备可落地的支持？ |
| 研究方法 | 反编译原版程序集全量遍历 + 原版 Def XML 全量检索 + 已订阅创意工坊 MOD（247 个）静态审计与按需反编译 |
| 结论 | **支持性充分，且存在官方扩展点与成熟先例**；核心机制纯 XML 即可接入，无需 Harmony |
| 配套产物 | `_research/vanilla/`（原版全量反编译，9226 个 .cs）、`_research/mods_analysis.md`（MOD 手法审计）、`_research/baal/`（关键先例反编译） |

---

## 0. 结论速览

1. **用户观察需先校准**：原版并非「无自驱」，而是**浅层反应式自驱**。pawn 会因饿/困/痛自主找食物、床、治疗（`JobGiver_GetFood` / `GetRest` / `SelfTend`），但这些是**阈值触发的需求满足**，不是**目标驱动的自主行为**。
2. **原版官方为 modder 预留了插入点**：`Humanlike.xml` 内四处 XML 注释直写 `Insertion hook for modders`（`Humanlike_PreMain` / `PostMain` / `PostDuty` / `PostMentalState`）。机制由 `Verse.AI.ThinkNode_SubtreesByTag` 实现——**只需写一个 `ThinkTreeDef`，零 Harmony、零原版文件改写**。
3. **第二条原生通道**：`TraitDegreeData.thinkTree` —— 特质可直接挂思考树（`ThinkNode_TraitBehaviors` 消费），同样是纯 XML。原版 Anomaly 的 Mutants 即用此机制，是「个性调制自驱强度」的现成载体。
4. **存在同构先例**：MOD **「巴尔：国政与邦交」(3768871513)** 已实现 `BPC_PersonalAutonomy`——`insertTag=Humanlike_PostMain` + `insertPriority=-12`（刻意低于原版主分支）+ 自定义 `JobGiver_BpcPersonalAutonomy` + `GameComponent` 意图引擎 + 六轴人格 `autonomousMotivationOffset`。**它已覆盖本报告拟议架构的绝大部分要素**。
5. **生态现状**：247 个已订阅 MOD 中**仅 3 个**使用官方 `insertTag` 通道（VEF、VDE-Dryads、巴尔）。绝大多数想改 pawn 行为的 MOD 走 Harmony 硬拦截。官方扩展点被严重低用——这是机会，也是风险（缺少踩坑先例）。
6. **最关键工程约束（决定架构）**：主思考树**不是每 tick 求值**，只在 ① pawn 无 job ② job 过期且 `checkOverrideOnExpire` ③ 显式 `CheckForJobOverride()` 三类时机求值。因此「纯 XML 方案」只能让自驱在**空闲时**生效；若要求 pawn **工作中途**主动抢占去执行议程，必须引入最小 Harmony 补丁。这是本案唯一「必须要不要 Harmony」的分水岭。

---

## 1. 研究方法与可复现基线

### 1.1 工具链（本机已就绪）

| 项 | 路径 |
|---|---|
| 反编译器 | `D:/toolbox/dnSpyEx/dnSpy.Console.exe`（含批量导出能力） |
| 原版程序集 | `E:/SteamLibrary/steamapps/common/RimWorld/RimWorldWin64_Data/Managed/Assembly-CSharp.dll` |
| 原版 Def 数据 | `E:/SteamLibrary/steamapps/common/RimWorld/Data/{Core,Biotech,Ideology,Royalty,Anomaly,Odyssey}/Defs/` |
| 创意工坊 MOD | `E:/SteamLibrary/steamapps/workshop/content/294100/<ID>/`（247 个） |
| Harmony 程序集 | `E:/SteamLibrary/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll` |

### 1.2 可复现命令

```bash
# 全量反编译原版到可检索源码树
cd /d/toolbox/dnSpyEx
./dnSpy.Console.exe --asm-path "<Managed>" \
  --no-sln --no-resources --no-resx --no-baml --no-color --spaces 4 --threads 8 \
  -o "<输出目录>" "<Managed>/Assembly-CSharp.dll"

# 仅反编译单个类型（快速核查）
./dnSpy.Console.exe --asm-path "<Managed>" --no-color -t "Verse.AI.ThinkNode" "<Managed>/Assembly-CSharp.dll"
```

### 1.3 覆盖率

- 原版反编译：**9226 个 .cs / 64 MB**，覆盖 `Verse.*` 与 `RimWorld.*` 全命名空间。
- 原版 Def 检索：全 `Data/` 树（含 6 个 DLC 目录）。
- MOD 审计：对 8 个高相关 MOD 做了 DLL 反编译逐类阅读（Common Sense / CAI 5000 / VEF / RimTraits / VSE / RimRelations / RimRomance / HugsLib），对巴尔 MOD 做了专项深挖，另对 247 个 MOD 的 XML 做了 `insertTag` 全量检索。

---

## 2. 原版 AI 架构遍历（事实层）

### 2.1 决策总入口：think tree 的求值语义

原版 AI 是**思考树（ThinkNode 树）顺序求值**模型，不是行为树评分模型。

| 节点类型 | 语义 | 证据 |
|---|---|---|
| `ThinkNode_Priority` | **顺序**遍历 `subNodes`，返回第一个有效 job；异常被 try/catch 吞掉并 `Log.Error`，继续下一个 | `Verse/AI/ThinkNode_Priority.cs` |
| `ThinkNode_PrioritySorter` | 按各子节点 `GetPriority(pawn)` **动态排序**，取优先级最高且 > `minPriority` 者；返回无效 job 则移除该节点继续 | `Verse/AI/ThinkNode_PrioritySorter.cs` |
| `ThinkNode_JobGiver` | 叶子：调 `TryGiveJob(pawn)` 产出 job；有 `reportStringOverride` | `Verse/AI/ThinkNode_JobGiver.cs` |
| `ThinkNode_Subtree` | 引用另一个 `ThinkTreeDef`，在 `ResolveSubnodes` 时 DeepCopy 展开 | `Verse/AI/ThinkNode_Subtree.cs` |
| `ThinkNode_SubtreesByTag` | **modder 注入点**：首次求值扫描 `DefDatabase<ThinkTreeDef>.AllDefs`，取 `insertTag` 相同者按 `insertPriority` 降序求值 | `Verse/AI/ThinkNode_SubtreesByTag.cs` |
| `ThinkNode_Conditional*` | 条件门（约 90 个子类，含 `ConditionalNeedPercentageAbove` / `ConditionalColonist` / `ConditionalMentalState` 等） | `Verse/AI/` |
| `ThinkNode_Tagger` | 给下游 job 打 `JobTag`（用于区分"谁下的令"） | `Verse/AI/ThinkNode_Tagger.cs` |
| `ThinkNode_ChancePerHour*` | **概率型自主行为**：`Rand.MTBEventOccurs(mtb, 2500, 2500)`，`Rand.Seed = pawn.RandSeedForHour()` | `Verse/AI/ThinkNode_ChancePerHour.cs` |

> **关键**：`ThinkNode.GetPriority(Pawn)` 是 `public virtual float`（`ThinkNode.cs:106`），且构造时通过 `GetType().IsMethodOverriden("GetPriority")` 自动置 `hasPriority`。**覆写它就能让自己的节点参与 `PrioritySorter` 的动态竞争**——这是「议程紧急度」的天然挂点。

### 2.2 殖民者思考树全貌

`Data/Core/Defs/ThinkTreeDefs/Humanlike.xml` 定义两张树：

**① `Humanlike`（主树，仅在特定时机求值）** 根 `ThinkNode_Priority`，顺序为：

| 序 | 节点 | 说明 |
|---|---|---|
| 1 | `ThinkNode_ConditionalLyingDown` → `ChancePerHour_Lovin` → `JobGiver_DoLovin` | 概率型自主亲密 |
| 2 | `ThinkNode_ConditionalMustKeepLyingDown` → 躺床取乐/冥想 | |
| 3–5 | `Downed` / `BurningResponse` / `MentalStateCritical` 子树 | |
| 6 | `Abilities_Escape`（Biotech） | |
| 7 | `JobGiver_ReactToCloseMeleeThreat` | |
| 8–10 | `MentalStateNonCritical` / `RopedPawn` 子树 | |
| **11** | **`ThinkNode_SubtreesByTag` insertTag=`Humanlike_PostMentalState`** | **官方注入点** |
| 12 | `ThinkNode_QueuedJob` | 队列任务 |
| 13 | `ConditionalColonist` → `Tagger(DraftedOrder)` → `MoveToStandable` / `Orders` | 征召指令 |
| 14 | `ConditionalNPCCanSelfTendNow` → `JobGiver_SelfTend` | **NPC 自愈（原版已有的一处自驱）** |
| 15 | `JoinVoluntarilyJoinableLord(HighPriority)` | |
| **16** | **`ThinkNode_SubtreesByTag` insertTag=`Humanlike_PostDuty`** | **官方注入点** |
| 17 | `ConditionalPrisoner`（囚犯全分支，含 `SatisfyingNeeds` 的 PrioritySorter） | |
| 18 | `ConditionalColonist` → 允许区域/安全温度/丢弃库存/`JobGiver_Work(emergency)` / 饥饿取食 / 母乳 / Lord(Medium) / 拾武器 / 服饰优化 / 造型 / 携库存 / 卸库存 / **`ConditionalNeedPercentageAbove(Food,0.6)` → `JobGiver_PackFood`** | |
| 19 | `ThinkNode_TraitBehaviors` | **特质挂树消费点** |
| **20** | **`ThinkNode_SubtreesByTag` insertTag=`Humanlike_PreMain`** | **官方注入点（VEF 用此点）** |
| 21 | `ConditionalColonist` → `MainColonistBehaviorCore` 子树 | **行为主核** |
| 22 | `ConditionalPawnKind(WildMan)` → `MainWildManBehaviorCore` | |
| **23** | **`ThinkNode_SubtreesByTag` insertTag=`Humanlike_PostMain`** | **官方注入点（巴尔用此点）** |
| 24 | `ConditionalColonist` → `Tagger(Idle)` → 随机取乐 / 闲逛 | 空闲兜底 |
| 25… | 野人 / 访客 / 离场 / 兜底闲逛 / `JobGiver_IdleError` | |

**② `HumanlikeConstant`（常量树，每 30 tick 求值）**：反爆炸、避真空、登船、加入商队、敌对反应、爬行处理、Lord 常量指令。

**行为主核 `MainColonistBehaviorCore`**（`SubTrees_Misc.xml:6-41`）：

```
ThinkNode_Tagger(SatisfyingNeeds)
└── ThinkNode_PrioritySorter
    ├── JobGiver_GetFood              priority: 饥饿时 9.5，否则 0
    ├── JobGiver_GetRest
    ├── JobGiver_SatisfyChemicalNeed / TakeDrugsForDrugPolicy
    ├── JobGiver_GetAgeReversal / MoveDrugsToInventory / GetNeuralSupercharge
    ├── JobGiver_GetHemogen / MeditateInBed / SatifyChemicalDependency
    ├── ThinkNode_Priority_Learn  → JobGiver_Learn      (Biotech)
    ├── ThinkNode_Priority_GetJoy → JobGiver_GetJoy / GetJoyInBed
    ├── JobGiver_Meditate / JobGiver_Reload
    └── JobGiver_Work             priority: Anything 5.5 / Work 9 / Sleep 3 / Joy 2
```

**由此可读出原版的优先级设计意图**：紧急饥饿（9.5）> 工作时间（9）> 一般时段（5.5）> 睡眠时段（3）。`JobGiver_Work` 位于**同一 PrioritySorter 中**——即原版已让「需求」与「工作」同台竞争，而非需求绝对优先。

### 2.3 官方 modder 插入点（硬证据）

原版 `Humanlike.xml` 里 XML 注释**原文**：

```xml
<!-- Insertion hook for modders -->
<li Class="ThinkNode_SubtreesByTag">
  <insertTag>Humanlike_PreMain</insertTag>
</li>
```

全 `Data/` 树检索到的全部官方 tag：

| tag | 宿主树 | 位置语义 | 适用场景 |
|---|---|---|---|
| `Humanlike_PreMain` | Humanlike | 行为主核**之前** | 需压过原版工作/需求的自驱（VEF 采用，priority=100） |
| `Humanlike_PostMain` | Humanlike | 行为主核**之后** | 仅闲时接管的自驱（巴尔采用，priority=-12） |
| `Humanlike_PostDuty` | Humanlike | Lord 指令之后 | |
| `Humanlike_PostMentalState` | Humanlike | 精神状态之后 | 紧急/救援类 |
| `Animal_PreMain` / `Animal_PreWander` | Animal（Insect 复用 Animal_*） | 动物树 | |
| `Insect_PreMain` / `Insect_PreWander` | Insect | 昆虫树 | |
| `Devourer_*` / `Fleshbeast_*` / `Ghoul_*` / `Gorehulk_*` / `Metalhorror_*` / `Shambler_*` | Anomaly 各树 | 异象实体 | |

**机制细节（`ThinkNode_SubtreesByTag.cs`）**：
```csharp
if (this.matchedTrees == null) {          // 懒加载：首次求值才扫描
    this.matchedTrees = DefDatabase<ThinkTreeDef>.AllDefs
        .Where(t => t.insertTag == this.insertTag)
        .OrderByDescending(t => t.insertPriority).ToList();
}
// 逐个 TryIssueJobPackage，第一个有效 job 胜出
```
`ThinkTreeDef` 的相关字段（`Verse/ThinkTreeDef.cs:126-133`）：
```csharp
public ThinkNode thinkRoot;
[NoTranslate] public string insertTag;
public float insertPriority;
```

> 注意：`insertTag` 排序是**降序**（大者先求值）。若优先级相同，`OrderByDescending` 是稳定排序，顺序取决于 `DefDatabase.AllDefs` 的注册顺序（与 mod 加载顺序相关）。

### 2.4 第二条原生通道：特质 → 思考树

`RimWorld/ThinkNode_TraitBehaviors.cs` 全文逻辑：
```csharp
foreach (Trait t in pawn.story.traits.allTraits)
    if (!t.Suppressed && t.CurrentData.thinkTree != null)
        return t.CurrentData.thinkTree.thinkRoot.TryIssueJobPackage(pawn, jobParams);
```

`TraitDegreeData` 的字段（`RimWorld/TraitDegreeData.cs:281`）：
```csharp
public ThinkTreeDef thinkTree;
public MentalStateDef randomMentalState;      // 也可强制精神状态
public MentalStateDef forcedMentalState;
public float forcedMentalStateMtbDays = -1f;
```

原版采用者：Anomaly `Mutants.xml` 的 `Shambler` / `Ghoul` / `AwokenCorpse`（`<thinkTree>` 指向同名 ThinkTreeDef）。

> **意义**：这条通道让「按特质/个性差异化自驱强度」可以做到**纯 XML 数据驱动**——只需自定义一个 `TraitDef`（或 `DefModExtension`）承载强度参数，再挂对应 ThinkTreeDef。**成本远低于 Harmony。**
>
> 但注意：`ThinkNode_TraitBehaviors` **只取第一个非空 thinkTree 即 return**（不是全部求值）。若目标 pawn 已有其他特质挂了 thinkTree，会互相覆盖——设计时须用"单一聚合树"策略而非"一特质一树"。

### 2.5 需求系统：阈值与节拍

`Pawn_NeedsTracker.NeedsTrackerTickInterval` 每 **150 tick**（约 2.5 s）遍历调用 `need.NeedInterval()`。

| 需求 | 关键阈值 | 证据 |
|---|---|---|
| `Need_Food` | `PercentageThreshUrgentlyHungry = FoodLevelPercentageWantEat × 0.4`；`PercentageThreshHungry = × 0.8`；`CurCategory ∈ {Fed,Hungry,UrgentlyHungry,Starving}` | `RimWorld/Need_Food.cs:24-89` |
| `Need_Rest` | `ThreshTired = 0.28`、`ThreshVeryTired = 0.14`、`CanWakeThreshold = 0.2`、`DefaultFallAsleepMaxLevel = 0.75` | `RimWorld/Need_Rest.cs:291-309` |
| 通用 | `Need.CurLevelPercentage`（可读写）、`CurCategory`、`CurInstantLevel` | `RimWorld/Need.cs:81` |

> `NeedInterval` 是天然的「议程评估节拍」——比每 tick 轻 150 倍，且原版已保证它一定会走。可作为自驱引擎的**低频心跳**。

### 2.6 任务抢占与玩家指令语义

`Pawn_JobTracker` 的公开接口（`Verse/AI/Pawn_JobTracker.cs`）：

| 成员 | 作用 |
|---|---|
| `StartJob(Job, JobCondition, ThinkNode jobGiver, …, ThinkTreeDef thinkTree, JobTag?, …)` | 统一任务入口 |
| `TryTakeOrderedJob(Job, JobTag?, bool requestQueueing)` | **玩家右键指令入口** |
| `CheckForJobOverride(float minPriority = 0f, bool ignoreQueue = true)` | **公开的"是否应换任务"重评估入口** |
| `IsCurrentJobPlayerInterruptible()` | 当前任务可否被玩家打断 |
| `TryOpportunisticJob(Job finalizerJob, Job job)` | 机会型任务 |
| `JobTrackerTickInterval(int delta)` | 每 30 tick 跑常量树 |

`CheckForJobOverride` 的调用点（全量检索）——**全部是事件驱动，无周期性调用**：
- `Pawn_JobTracker.JobTrackerTickInterval:157`（仅当 `curJob.checkOverrideOnExpire` 且已过期）
- `Pawn_HealthTracker`（受伤时）、`Pawn_MindState:571`、`Toils_LayDown:73`、`Toils_Recipe:154`
- 若干 JobDriver 内部（Meditate / Reading / Spectate / TendPatient 等）
- `Pawn_JobTracker:1020 / 1092`（下床/被惊动等）

`JobDef` 与 `Job` 上控制"可被打断性"的字段：

| 字段 | 默认 | 含义 |
|---|---|---|
| `JobDef.playerInterruptible` | `true` | 玩家能否打断 |
| `JobDef.casualInterruptible` | `true` | 一般性打断 |
| `JobDef.allowOpportunisticPrefix` | `false` | **是否允许"顺路插入"机会型任务**（Common Sense 依赖此字段） |
| `Job.checkOverrideOnExpire` | `false` | 过期时是否重评估（决定 `CheckForJobOverride` 是否被调） |
| `Job.expiryInterval` | `-1` | 任务超时（-1 = 不超时） |
| `Job.ignoreJoyTimeAssignment` | `false` | 忽略取乐时段限制 |
| `Job.playerForced` | `false` | 是否玩家强制 |

### 2.7 求值节拍总表（决定架构的关键）

| 节拍 | 周期 | 内容 | 可承载的自驱能力 |
|---|---|---|---|
| 逐 tick | 1 tick | `JobTrackerTick` → `curDriver.DriverTick()` | 仅驱动当前任务 |
| **常量思考树** | **30 tick** | `HumanlikeConstant` | 高频反应（原版用于反爆炸/敌对反应）。**无官方 insertTag 钩子** |
| 需求间隔 | 150 tick | `NeedInterval()` | 低频心跳（读数、累积议程强度） |
| **主思考树** | **事件驱动，非周期** | 无 job / job 过期 / 显式 override | **自主行为的唯一自然窗口** |

> **架构分水岭**：主思考树只在「空闲 / 任务过期 / 显式 override」时求值。
> - 若自驱只需在**空闲时**生效 → `insertTag` 纯 XML 方案**完全够用**。
> - 若要求 pawn 在**工作中途**因议程强度上升而主动抢占（例如"工作到一半想起要给恋人送礼物"）→ **必须**引入 Harmony（最小面：定时调 `CheckForJobOverride`，或 patch `JobTrackerTickInterval`）。

---

## 3. 已有 MOD 生态

### 3.1 生态总览

| MOD | ID | 有 DLL | 主要接入手法 | 自驱层级 |
|---|---|---|---|---|
| **巴尔：国政与邦交** | 3768871513 | 是（3 个） | **`insertTag` + 自定义 JobGiver + GameComponent 意图引擎 + 266 个 Harmony patch** | **L2 + L3（完整）** |
| Common Sense | 1561769193 | 是 | Harmony Prefix 拦 `Pawn_JobTracker.StartJob` 插"顺路清洁/搬运"；Transpiler 改 `Priority_GetJoy.GetPriority`；xpath 改原版阈值 | L2（机会型） |
| CAI 5000 - Advanced AI | 3673768803 | 是 | 自定义 DutyDef+think 树 + 运行期改 `mindState.duty` + `PatchOperationInsert` + Prepatcher | 战术型 |
| Vanilla Expanded Framework | 2023507013 | 是 | **`insertTag` 钩子**（`VEF_DraftableHook`，Animal_PreMain, priority 100）+ `DefModExtension` 扩展点体系 | 框架 |
| Vanilla Ideology Exp. - Dryads | 2720631512 | 是 | `insertTag`（Animal_Cleaner） | 动物行为 |
| RimTraits | 2206957172 | **否** | 纯 XML TraitDef（statOffsets/skillGains），无 AI 介入 | 无（间接） |
| Vanilla Skills Expanded | 3400246558 | 是 | 程序化 Harmony 打 Stat/Skill/JobDriver toil 补丁 | 无（数值层） |
| RimRelations | 3737617982 | 是 | Harmony 社交事件 + GameComponent 记录；**不主动发 job** | 无（状态供给） |
| RimRomance | 3775616419 | 是 | 同上（attraction 衰减 + GameComponent） | 无（状态供给） |
| HugsLib | 818773962 | 是 | 基础框架（生命周期/设置/持久化），无 AI | 无 |

> **重要反证**：RimTraits / VSE 证明「只改数值与特质」**不足以**让 pawn 做原版不会做的新事——必须落到 **think tree 或 Job 层**。

### 3.2 三条成熟路线

| 路线 | 介入深度 | 需要 C# | 代表 | 适用 |
|---|---|---|---|---|
| **A. XML `insertTag` 注入** | 浅 | 仅 JobGiver 需一个类（或全用原版节点则零代码） | VEF、VDE-Dryads、巴尔 | 闲时/条件型自驱 |
| **B. Harmony 硬拦截** | 深 | 是 | Common Sense、CAI 5000 | 需改写原版决策、工作中途抢占 |
| **C. 自定义 Duty + 强改 `mindState.duty`** | 最深 | 是 | CAI 5000 | 战斗/编队类强制行为 |

### 3.3 关键先例：巴尔 `BPC_PersonalAutonomy` 完整拆解 ★

**XML 层**（`Defs/ThinkTreeDefs/BPC_PersonalAutonomy.xml`，含作者设计注释）：

```xml
<!-- Personal intentions are discretionary. The entire vanilla main branch,
     including ordinary enabled work and needs, runs before this branch. It
     never schedules routine cleaning, hauling, repair, or production. -->
<ThinkTreeDef>
  <defName>BPC_PersonalAutonomy</defName>
  <insertTag>Humanlike_PostMain</insertTag>
  <insertPriority>-12</insertPriority>
  <thinkRoot Class="ThinkNode_Tagger">
    <tagToGive>SatisfyingNeeds</tagToGive>
    <subNodes>
      <li Class="Baal.PartiesAndCurrents.JobGiver_BpcPersonalAutonomy">
        <leaveJoinableLordIfIssuesJob>true</leaveJoinableLordIfIssuesJob>
      </li>
    </subNodes>
  </thinkRoot>
</ThinkTreeDef>
```

该 MOD 共 3 条自主树，构成一个**优先级谱**，是极佳的设计范式：

| defName | insertTag | insertPriority | 内容 |
|---|---|---|---|
| `BPC_MemoryEmergencyRescue` | Humanlike_PostMentalState | **95** | 紧急救援（可压过原版） |
| `BPC_MemorySocialAutonomy` | Humanlike_PostMain | **-8** | 收礼/情感支持/探病/买礼物 |
| `BPC_PersonalAutonomy` | Humanlike_PostMain | **-12** | 个人意图主入口 |
| `BPC_AutonomousCivicShopping` | Humanlike_PostMain | **-20** | 储蓄/证券/零售（最低） |

> 作者用 `insertPriority` **负值**精确控制"自驱永远排在原版工作之后"，用正值 95 让"紧急救援"能插队。**这就是 `insertPriority` 的正确用法。**

**代码层架构**：

```
JobGiver_BpcPersonalAutonomy : ThinkNode_JobGiver
  └── TryGiveJob(pawn)
      └── GameComponent_BpcPersonalAutonomy.Current
            └── SelectRunnableIntent(pawn, leisure)   ← 议程选择
                  ├── CanRunDiscretionaryAction(pawn) → 门禁
                  ├── IntentsForActor(pawnId)          → 该 pawn 的意图表
                  ├── 过滤: state==Planned && notBeforeTick<=now && expiresTick>now
                  ├── HigherRunnablePriority: urgency > createdTick > intentId
                  ├── OpportunityBlocker(...) → 被阻塞则记录 blocker，试下一个
                  ├── PrepareIntent(actor, intent, out blocker) → 成功即返回
                  └── 失败退避: notBeforeTick = min(expiresTick-1, now + 1200 + attempts*900)
                              attempts++ / 满 4 次 → state = Blocked
      └── JobMaker.MakeJob(jobDef, target...)
            job.count = intent.intentId      ← 把意图 ID 传给 JobDriver 回读
            job.playerForced = false
            job.ignoreJoyTimeAssignment = true
            job.expiryInterval = 5000（leisure 时 min(1200, LeisureTicksRemaining)）
```

**核心数据模型**（`BpcPersonalIntent` / `BpcDriveState` / `BpcAutonomyOpportunityState`）：

| 类型 | 字段 | 作用 |
|---|---|---|
| `BpcPersonalIntent` | `state(Planned/Acting/Completed/Blocked)`、`urgency`、`createdTick`、`expiresTick`、`notBeforeTick`、`attempts`、`blocker`、`actorPawnId`、`targetPawnId`、`targetThingId`、`targetCell`、`intentId` | **一次具体行动意图** |
| `BpcDriveState` | `kind`、`intensity`、`momentum`、`lastSelectedTick` | **驱动力状态（本报告所称"内驱状态机"）** |
| `BpcPersistentGoal` | — | 长期目标（跨意图持续） |
| `BpcHabit` / `BpcHabitKind` | 由 `ReinforceHabit` 强化，`HabitTargetBonus` / `HabitPlanBonus` 加成 | **习惯系统：自驱行为的正反馈** |
| `BpcAutonomyOpportunityState` | `pawnId`、`day`、`usedTicks`、`started`、`planned`、`completed`、`expired`、`lastSeenTick` | **每 pawn 每日自主预算 + 统计** |
| `BpcPersonalAutonomyMode` | `enum { Off, Safe, Full }` | **玩家可见的自治档位开关** |

**`SelectDriveForAction` 的选择算法（工程价值极高）**：

```csharp
// 1) 门槛：intensity >= threshold * 0.82
// 2) 评分：score = intensity + momentum*0.28 + GoalMomentum*0.2
// 3) 软采样（非 argmax）：weight = exp((score - maxScore) * 2.1) + 0.18
//    → 按权重轮盘抽样，产生涌现多样性
// 4) 动量更新：中选驱动 momentum *= 0.82 + 0.22；其余 *= 0.88 - 0.025
// 5) 确定性随机：StableHash(pawnId, now/2500, 91173) → 每 2500 tick 窗口内可复现
```

**性能与稳健性设计（可直接借鉴）**：
- `opportunityStates` 上限 **4096**，超出按 `lastSeenTick` 淘汰最旧；
- `driveCandidateScratch` **复用 List**，避免每次分配；
- 失败**指数级退避**（`1200 + attempts*900`）+ 最多 4 次尝试；
- 随机用 **确定性哈希**而非 `Rand`，保证存档/多人可复现；
- `Scribe_Collections.Look(..., LookMode.Deep)` 持久化全部状态；
- `Patch_PawnJobTracker_ExposeData_DeflessJobRecovery`（Prefix）**在存读档时修复丢失 Def 的 job**——这是自驱 job 跨存档的必备防护。

**个性调制层**（`Defs/BpcPersonalityDefs/BPC_CorePersonalities.xml`，自定义 `BpcPersonalityDef`）：

```
六轴: warmth / expression / initiative / deliberation / stability / optimism
社交: lonelinessSensitivity / socialInitiationFactor / verbosityFactor
      kindInteractionFactor / hostileInteractionFactor / affectSensitivityFactor
行为: workPriorityOffset / engagementOffset / riskAversionOffset
      autonomousMotivationOffset      ← ★ 自驱力偏移
      privacyOffsetBasisPoints
数值: statFactors{WorkSpeedGlobal, MoveSpeed, SocialImpact, ...}
```

实测值：`Enthusiastic = +700`、`Communicative = +450`、`Aloof = -150`、`Reflective = -200`。

> **`autonomousMotivationOffset` 就是本报告设想的「个性调制自驱强度」，且已被实现验证。**

### 3.4 `insertTag` 的实际采用率

对 247 个已订阅 MOD 做 XML 全量检索，**仅 3 个 MOD 使用了官方 `insertTag`**：
- `2023507013` Vanilla Expanded Framework（`Reloading` / `Draftable_Hook` / `Animal_Harvester` / `Animal_WeirdEater`）
- `2720631512` Vanilla Ideology Expanded - Dryads（`Animal_Cleaner`）
- `3768871513` 巴尔（4 个 `BPC_*`）

**推论**：官方扩展点**严重低用**（≈1.2%）。多数作者选择 Harmony 硬拦（Common Sense 全 MOD 检索无 `insertTag`）。这带来两面：
- **机会**：走官方通道的 MOD 极少 → 冲突面小、几乎不会踩别人（VEF 的 `VEF_DraftableHook` 与巴尔同用 Animal/Humanlike 点，是仅有的共存样本）；
- **风险**：缺少成熟踩坑先例与社区文档，需自己实测验证求值顺序。

---

## 4. 支持性评估

### 4.1 扩展点矩阵

| # | 扩展点 | 需要 C# | 需 Harmony | 官方性质 | 冲突风险 | 能力上限 |
|---|---|---|---|---|---|---|
| E1 | `ThinkTreeDef.insertTag` + `insertPriority` + 自定义 `JobGiver` | 是（1 类） | 否 | **官方明示** | 低 | 闲时/事件时机自驱 |
| E2 | `ThinkTreeDef.insertTag` + **仅原版节点组合** | **否** | 否 | **官方明示** | 低 | 仅能重排已有原版行为 |
| E3 | `TraitDegreeData.thinkTree`（特质挂树） | 否 | 否 | 官方（Anomaly 用） | 低 | 按特质差异化自驱 |
| E4 | `LifeStageDef.thinkTreeMainOverride` / `ThingDef.race.thinkTreeMain` | 否 | 否 | 官方 | 低 | 按种族/年龄段整体换树 |
| E5 | 自定义 `ThinkNode`（议程评估器）+ 覆写 `GetPriority` | 是 | 否 | 官方虚方法 | 低 | 动态紧急度竞争 |
| E6 | 自定义 `DutyDef` + 运行期改 `pawn.mindState.duty` | 是 | 否 | 官方结构 | **中**（CAI 5000 强占） | 强制行为接管 |
| E7 | `GameComponent` / `ThingComp` + `Scribe` 承载状态 | 是 | 否 | 官方 | 低 | 持久化 |
| E8 | Harmony patch `Pawn_JobTracker.StartJob`（旁路插入） | 是 | **是** | 非官方 | **高**（Common Sense + CAI 5000 均占） | 工作中途抢插 |
| E9 | Harmony patch `Pawn_JobTracker.CheckForJobOverride` / `JobTrackerTickInterval` | 是 | **是** | 非官方 | **低**（尚无 MOD 占） | **工作中途自主抢占** |
| E10 | `PatchOperationInsert` 改原版 think tree 节点 | 否 | 否 | 官方 XML patch | **中**（xpath 争用） | 精确位置插节点 |
| E11 | `PatchOperationReplace` 改原版节点参数（阈值） | 否 | 否 | 官方 XML patch | **中**（改别人也改的值） | 调阈值（如 PackFood 0.6→0.3） |
| E12 | `DefModExtension` 挂白/黑名单 | 否 | 否 | 官方 XML patch | 低 | 数据驱动开关 |
| E13 | `ThinkNode_ChancePerHour` 概率型自主行为 | 是（子类） | 否 | 官方基类 | 低 | 随机自发行为 |

**结论**：核心机制（L2 预防性 + L3 志向型，闲时执行）可由 **E1/E3/E5/E7 组合实现，全程零 Harmony**。仅"工作中途抢占"需要 **E9**（且 E9 是本机生态中**未被占用**的最优补丁点）。

### 4.2 分层支持度

| 层 | 用户诉求 | 原版现状 | 支持度 | 推荐接入 |
|---|---|---|---|---|
| L1 反应式需求满足 | 饿了吃、困了睡 | **已有**（`PrioritySorter` + 阈值） | 无需改动 | — |
| L2 预防性自我维护 | 未雨绸缪、自整理、自维护装备 | **很弱**（仅 `JobGiver_SelfTend` 给 NPC、`PackFood` 给殖民者） | **完全支持** | E1 + E5 |
| L3 志向型自主目标 | 个人项目、抱负、关系投资、技能志向 | **几乎无**（仅社交/学习有空壳节点） | **完全支持** | E1 + E3 + E5 + E7 |
| L3' 工作中途抢占 | 干活时想起要去做某事的即时性 | 无 | **需 Harmony** | E9 |

### 4.3 硬约束（必须知道的坑）

1. **主思考树非周期求值** —— 纯 XML 方案的自驱只在空闲/过期/override 时被"看到"。不要指望 pawn 工作到一半自己去做别的事。
2. **`ThinkNode_TraitBehaviors` 只取第一个非空 tree 即 return** —— 一特质一树会互相覆盖，须做单一聚合树。
3. **`insertTag` 降序 + 稳定排序** —— 同 priority 者顺序取决于 mod 加载顺序，**不要依赖平手**，务必给不同 priority。
4. **`ThinkNode_SubtreesByTag.matchedTrees` 缓存于首次求值** —— 若某 mod 晚加载并注册同名 tag，其行为在**本局开始后**才可见（def 数据库在游戏启动时已定型，实际影响有限，但热重载/开发调试时需注意）。字段标记 `[Unsaved(false)]`，不入存档。
5. **`JobDef.playerInterruptible` 默认 true** —— 自驱 job 会被玩家指令随时打断，这是**期望行为**（"pawn 提议、玩家处置"），也是玩家 agency 的保险。若某类自驱 job 不允许打断须显式设 false，但会削弱玩家控制感。
6. **`Job.expiryInterval` / `checkOverrideOnExpire` 是自驱 job 的生命线** —— 不设超时的自驱 job 会长期霸占 pawn（原版 `StartJob` 的 `JobTrackerTickInterval` 只在 `expiryInterval>0` 时才走重评估路径）。**自驱 job 必须设 `expiryInterval`**。
7. **需求阈值来自 `RaceProps`** —— `FoodLevelPercentageWantEat` 等由种族决定，异种/外星种族需读 `pawn.RaceProps` 而非硬编码。
8. **原版 `ThinkNode_Priority` 有 try/catch** —— 自定义节点抛异常会被 `Log.Error` 记录后跳过，**不会崩游戏**，这是"失败不影响核心"的天然兜底（但会刷日志，须自己防异常）。

---

## 5. 风险与对策

| 风险 | 等级 | 具体表现 | 对策 |
|---|---|---|---|
| **性能** | 中 | 每 pawn 每 tick 评估议程 → CPU 爆。参考：巴尔用 `GameComponent` + 每日预算 + 4096 上限 + scratch 复用，仍 266 patch | ① 评估绑定 `NeedInterval`（150 tick）或 `insertTag` 求值时机，**不逐 tick**；② 每 pawn 状态上限；③ 复用 scratch 容器；④ 目标数上限 |
| **玩家 agency 被架空** | **高** | 自驱过多 → 玩家觉得自己多余，或指令被频繁打断 | ① `insertPriority` 取负值，自驱永远排在工作之后（巴尔范式）；② 提供 `Mode{Off,Safe,Full}` 玩家开关；③ 自驱 job 保持 `playerInterruptible=true`；④ 不可自足时**转成"请求"而非静默失败** |
| **存档兼容** | 中 | 新增字段无默认值 → 旧档加载崩；自驱 job 引用 Def 丢失 | ① 所有 Scribe 字段给默认值；② 参考巴尔 `Patch_PawnJobTracker_ExposeData_DeflessJobRecovery` 在 `ExposeData` 前清理无 Def 的 job；③ 中途加入（ongoing save）用 `FinalizeInit` 兜底初始化 |
| **跨 MOD 冲突** | 中低 | 与 Common Sense / CAI 5000 争用 `Pawn_JobTracker.StartJob`；与 VEF/巴尔 争用 `Humanlike_*` tag | ① 首选零 Harmony 方案（天然避开 StartJob 争用）；② 若必须 patch，选 `CheckForJobOverride` / `JobTrackerTickInterval`（**本机尚无 MOD 占用**）；③ `insertTag` priority 用非整数唯一值 |
| **行为不可解释** | 中 | pawn 擅自做事，玩家困惑 | 每次自驱必须可见：向 `JobTag` 打标（`ThinkNode_Tagger`）+ 记录 Thought + 可查询日志（巴尔有完整叙事日志体系） |
| **随机不可复现** | 低 | 用 `Rand` 导致读档/多人不同步 | 用确定性哈希（`StableHash(pawnId, now/2500, salt)`）而非全局 `Rand` |
| **文案硬编码** | 中 | 违反工程 P1 铁律（validate_mod.py 报错） | 全部用户可见文案走 `Languages/{English,ChineseSimplified}/Keyed/*.xml` |

---

## 6. 建议架构（落地蓝图）

### 6.1 分层（与 PersonalChronicle 一致：UI→Application→Domain→Data 单向）

```
Domain/          纯规则，无 Verse 依赖（或最小依赖）
  PersonalAgenda.cs         目标/意图模型
  DriveState.cs             驱动力状态（kind/intensity/momentum）
  SelfEfficacy.cs           自足判定：CanSelfComplete(pawn, goal) → (bool, blocker)
  AgendaPriority.cs         排序规则（urgency/deadline/age）
  DriveTaxonomy.cs          驱动力目录（枚举 + 元数据）

Data/            持久化，不依赖 Application
  GameComponent_PawnDrive.cs  持有全部 pawn 的 Agenda/DriveState/预算；Scribe 持久化
  AgendaStore.cs              索引与容量淘汰

Application/     编排
  AgendaService.cs            每 150 tick 心跳：衰减/累积/生成意图
  AssistRequestService.cs     自足失败的"请求"汇总（Letter/Thought）
  PawnDriveSettings.cs        玩家档位（Off/Safe/Full）+ 强度系数

Patches/         捕获层（初版建议为空，见 6.3）
  (可选) Patch_JobTracker_AgendaTick.cs

AI/              原版接入
  ThinkNode_AgendaEvaluator.cs   议程评估节点（覆写 GetPriority）
  JobGiver_Agenda.cs             产出 job
  JobDriver_Agenda.cs            执行 + 回写结果

Defs/            XML
  ThinkTrees/PawnDrive_Agenda.xml    insertTag=Humanlike_PostMain, insertPriority 负值
  (可选) Traits/                     承载自驱强度参数
Languages/{English,ChineseSimplified}/Keyed/PawnDrive.xml
```

### 6.2 数据模型（采纳巴尔的成熟结构）

```csharp
class PersonalIntent : IExposable {
    int intentId; int actorPawnId;
    IntentKind kind;                 // SelfMaintain / Socialize / Learn / Project / …
    IntentState state;               // Planned / Acting / Completed / Blocked
    float urgency;
    int createdTick, expiresTick, notBeforeTick, attempts;
    string blocker;                  // 不可执行的原因（用于"请求"文案）
    int targetPawnId; string targetThingId; IntVec3 targetCell;
}

class DriveState : IExposable {
    DriveKind kind; float intensity, momentum; int lastSelectedTick;
}
```

### 6.3 接入选择（关键决策）

| 阶段 | 能力 | 接入方案 | 是否 Harmony |
|---|---|---|---|
| **P1（最小可用）** | 闲时自驱：L2 自我维护 | `ThinkTreeDef` (insertTag=`Humanlike_PostMain`, priority=负值) + `JobGiver_Agenda` + `GameComponent` 状态 | **否** |
| **P2** | L3 志向型目标 + 个性调制 | + 自定义 `ThinkNode_AgendaEvaluator`（覆写 `GetPriority`）+ 特质/Def 承载强度参数（`autonomousMotivationOffset` 思路） | **否** |
| **P3** | 自足失败的"玩家请求" | `AssistRequestService` → Letter/Thought | **否** |
| **P4（可选）** | 工作中途自主抢占 | Harmony Prefix/Postfix on `Pawn_JobTracker.CheckForJobOverride`（或定时触发它） | **是（最小面）** |
| **P5（可选）** | 玩家交互层 | 设置界面 + 档位 `Off/Safe/Full` + 每 pawn 议程查看 | 否 |

### 6.4 与既有工程约束的对照

| 约束 | 本方案符合性 |
|---|---|
| 双向依赖禁止（UI→Application→Domain→Data） | 6.1 分层已满足 |
| Data 不得依赖 Application | `GameComponent` 仅持数据，编排在 Application |
| 用户可见文案零硬编码 | 全部走 Keyed XML（`AssistRequest` / 意图描述 / 设置项） |
| 单一职责 / 防硬编码 | 驱动力目录用 Def 数据驱动（参考巴尔 `BpcPersonalityDef`），非枚举硬编码 |
| 统计双轨（活读 + 快照） | 议程状态本身即"快照态"，与 PC 的 live/archive 双轨一致 |

### 6.5 架构与兼容裁决（本报告自行给出，Gate agent 未能启动）

> 说明：`.codebuddy/agents/` 的 `pc-arch-gatekeeper` / `pc-compat-auditor` 在本会话**无法启动**——其声明的 `read_file / search_content / search_file / list_dir` 在当前环境无法解析（详见 §7）。以下裁决由本报告基于 §2–§5 的事实自行给出。

**架构裁决（GATE-ARCH）**

| 项 | 裁决 | 理由 |
|---|---|---|
| 分层归属 | **PASS（需微调）** | `SelfEfficacy` 属 Domain 正确；但 `GameComponent` 建议归 **Data**（仅持久化与索引），编排逻辑放 Application，避免 Data 承载业务规则（否则 Data 隐式依赖 Domain 规则语义） |
| Harmony 必要性 | **CONCERN** | P1–P3 零 Harmony 可达成；唯 P4「工作中途抢占」必须 Harmony。**最小面应选 `CheckForJobOverride` / `JobTrackerTickInterval`**——本机 247 个 MOD 中**无任何 MOD patch 这两个方法**（Common Sense 与 CAI 5000 争用的是 `StartJob`）。选此点可同时满足"功能达成"与"冲突最小" |
| 全局/静态状态 | **PASS** | `GameComponent` + `Current.Game.GetComponent<T>()` 符合工程既有惯例（PC 自身即用此模式）。**禁止** static mutable（跨存档泄漏、多人不同步）。中途加入兜底：`FinalizeInit` 末尾回填（PC 已有 `BackfillExistingColonists` 先例） |
| 继承 vs 组合 | **PASS** | 继承 `ThinkNode_JobGiver` / `ThinkNode` 是官方设计意图（原版 242 个 JobGiver 均如此）。但**逻辑必须组合进 Domain 服务**，ThinkNode 子类只做"取结果 → 造 job"，不含规则 |
| 文案硬编码 | **必须遵守** | C# 出现中文即 validate_mod.py P1。所有意图描述/请求文案进 Keyed XML |

**兼容裁决（GATE-COMP）**

| 项 | 裁决 | 理由 |
|---|---|---|
| 依赖声明 | **PASS（零硬依赖）** | 本方案不引用任何第三方类型（仅原版 `ThinkNode*` / `GameComponent`），**无硬依赖、无软依赖**，可做到"目标 MOD 缺失即正常运行" |
| 多 mod 同 tag 共存 | **CONCERN** | 同 `Humanlike_PostMain` 已有巴尔（-8/-12/-20）与（PreMain 有 VEF 100、巴尔无）。降序求值下，**priority 绝对不要用 0 或整数平手值**；建议选唯一且不与他家重叠的负值（如 `-15`）。另注意：`SubtreesByTag` 是"第一个出有效 job 者胜出"，**优先级高者若总出无效 job 则逐级下探**，故高优先级不构成饿死风险，但会带来额外求值开销 |
| Harmony 争用面 | **PASS** | 若按裁决选 `CheckForJobOverride` / `JobTrackerTickInterval`，当前生态**零占用**；`StartJob` 已被 Common Sense + CAI 5000 双占，**应避开** |
| 降级策略 | **PASS** | 原版 `ThinkNode_Priority.TryIssueJobPackage` 有 try/catch → 自定义节点异常被记录后跳过，不影响核心；另需自定义节点内部自兜异常以避免刷日志 |
| Prepatcher 共存 | **PASS（不引入）** | 本方案不依赖 Prepatcher；CAI 5000 的 Prepatcher 注入只影响其自身类型。**不建议**为本方案引入 Prepatcher 依赖（徒增前置依赖） |
| 加载顺序 | **PASS** | `insertTag` 匹配是**首次求值**时扫 `DefDatabase.AllDefs`（此时全部 mod 已加载完），无需 `loadAfter`。**仅当**需要覆盖他家的 `PatchOperationReplace` 结果时才需排序 |
| HugsLib / Prepatcher | **均不建议** | 现代做法用 Harmony + `Mod` + `Scribe` 足够；引入框架会扩大依赖面 |

---

## 7. 尚未验证 / 待办

1. **实机验证 `insertTag` 求值顺序**：本报告的顺序结论来自源码阅读（`OrderByDescending(insertPriority)` + 稳定排序），**未做游戏内实测**。建议在 P1 落地后，用 Dev 模式日志打印 `matchedTrees` 实际顺序确认。
2. **`HumanlikeConstant`（30 tick 树）无官方 insertTag 钩子**：若需高频自驱心跳，需自行 `PatchOperationInsert` 或改用 `NeedInterval`（150 tick）。尚未验证两方案的实际体感差异。
3. **`.codebuddy/agents/` 的 pc-* Gate agent 无法启动**（重要工程问题）：
   - 现象：派发 `pc-arch-gatekeeper` / `pc-compat-auditor` 均失败，报 `none of its declared tools resolved to an available tool (unresolvable entries: read_file, search_content, search_file, list_dir)`。
   - 影响：`.codebuddy/rules/agent-cluster-frontdoor.md` 规定的前置路由协议**在当前环境无法走完**（`route-task.ps1` 可运行但返回 `unrouted`；`audit-subagents.ps1` 派发环节直接失败）。
   - 待办：把各 agent frontmatter 里的工具名改为本环境可解析的名称（如 `Read` / `Grep` / `Glob` / `Bash` 等实际工具名）。
4. **`route-task.ps1` 关键词覆盖不足**：输入含"MOD""遍历原版代码"仍返回 `unrouted: true`，说明规则关键词表可能只匹配小写 `mod` 或特定短语，与 frontdoor 规则宣称的触发词不一致，建议核对。
5. **未验证项**：`ThinkNode_TraitBehaviors` 在同 pawn 多特质挂树时的实际覆盖行为（源码判断为"取第一个非空即 return"，未实测多 MOD 叠加情形）。

---

## 8. 证据索引

### 8.1 原版源码（反编译产物，`_research/vanilla/Assembly-CSharp/`）

| 主题 | 路径 |
|---|---|
| 节点体系 | `Verse/AI/ThinkNode.cs`、`ThinkNode_Priority.cs`、`ThinkNode_PrioritySorter.cs`、`ThinkNode_JobGiver.cs`、`ThinkNode_Subtree.cs`、`ThinkNode_SubtreesByTag.cs`、`ThinkNode_Tagger.cs`、`ThinkNode_ChancePerHour.cs` |
| 思考树定义 | `Verse/ThinkTreeDef.cs`（`insertTag` / `insertPriority` 字段） |
| 特质挂树 | `RimWorld/ThinkNode_TraitBehaviors.cs`、`RimWorld/TraitDegreeData.cs:281` |
| 任务调度 | `Verse/AI/Pawn_JobTracker.cs`（`JobTrackerTickInterval:111`、`CheckForJobOverride:668`、`DetermineNextJob:841`、`ShouldStartJobFromThinkTree:933`、`IsCurrentJobPlayerInterruptible:945`） |
| 思维器 | `Verse/AI/Pawn_Thinker.cs` |
| 需求 | `RimWorld/Pawn_NeedsTracker.cs:83`（150 tick）、`RimWorld/Need.cs`、`RimWorld/Need_Food.cs`、`RimWorld/Need_Rest.cs` |
| 具体 JobGiver | `RimWorld/JobGiver_GetFood.cs`（`GetPriority` 返 9.5）、`RimWorld/JobGiver_Work.cs`（5.5/9/3/2） |
| Job 定义 | `Verse/JobDef.cs`、`Verse/AI/Job.cs` |

### 8.2 原版 Def XML

| 主题 | 路径 |
|---|---|
| 殖民者思考树 + 官方注入点 | `Data/Core/Defs/ThinkTreeDefs/Humanlike.xml`（第 84/122/357/382 行为注入点） |
| 行为主核 | `Data/Core/Defs/ThinkTreeDefs/SubTrees_Misc.xml:6-41` |
| 动物/昆虫树 | `Data/Core/Defs/ThinkTreeDefs/{Animal,Insect}.xml` |
| 特质挂树范例 | `Data/Anomaly/Defs/Misc/Mutants.xml`（Shambler/Ghoul/AwokenCorpse） |

### 8.3 MOD 证据

| MOD | 关键文件 |
|---|---|
| 巴尔 (3768871513) | `Defs/ThinkTreeDefs/{BPC_PersonalAutonomy,BPC_MemorySocialAutonomy,BPC_CivicShopping}.xml`；`Defs/BpcPersonalityDefs/BPC_CorePersonalities.xml`；反编译 → `_research/baal/.../JobGiver_BpcPersonalAutonomy.cs`、`GameComponent_BpcPersonalAutonomy.cs`（5276 行）、`BpcPersonalAutonomyUtility.cs`（2940 行）、`BpcAutonomyOpportunityState.cs`、`BpcPersonalAutonomyMode.cs` |
| VEF (2023507013) | `1.6/Defs/ThinkTreeDefs/{Draftable_Hook,Reloading,Animal_Harvester,Animal_WeirdEater}.xml` |
| Common Sense (1561769193) | `1.6/Assemblies/CommonSense.dll`（Harmony 旁路 `Pawn_JobTracker.StartJob`） |
| CAI 5000 (3673768803) | `1.6/Patches/ThinkTree_Humanlike.xml`、`1.6/Defs/DutyDefs/Duties_Misc.xml` |
| 完整 MOD 手法审计 | `_research/mods_analysis.md` |

---

## 附：一句话总结

**原版为「pawn 自驱」留了官方门（`insertTag` + `TraitDegreeBehavior`），社区已有人（巴尔）从这扇门走通并建成了完整引擎；我们要做的不是"从零发明"，而是"用更干净的架构重做一遍，并补上它未做的部分"。**

# Pawn 自驱力框架 —— SDD（需求与功能模块）

> **推导方法声明**：本文以**原版程序集为第一依据**——所有设计约束先由 RimWorld 1.6 `Assembly-CSharp.dll` 反编译源码推出（本地 `_research/vanilla/Assembly-CSharp/`，共 9226 个 `.cs`）。
> **其他 MOD 仅作可行性验证**，不因其「这么做了」就照做；凡原版无此约束者，一律不上升为框架约束。
> 证据格式为 `路径:行号`，均可在本地反编译产物中复核。

**文档关系**：本文定「**做什么 / 分几个模块**」；`Pawn自驱力框架化_架构设计与路线图.md` 定「**什么时候做 / 分几期**」。两者冲突时以本文（原版推导）为准。

---

## 0. 范围与前提

- 目标产物：**公开框架 MOD**，独立程序集，PersonalChronicle 降级为消费者。
- 本文只写需求与模块，**不含代码、不含工期承诺**。
- ⚠️ 「工作中途抢占」用户决定**延后**；本文将其列为独立模块 M7 并标注可选，架构上已保证可后期追加（见 §1.3-C1 与 §3.7）。

---

## 1. 原版机制基线（全部设计的地基）

### 1.1 挂载通道：`insertTag` 是官方唯一零侵入入口

`Verse/AI/ThinkNode_SubtreesByTag.cs`：

```
:29   foreach (ThinkTreeDef thinkTreeDef in DefDatabase<ThinkTreeDef>.AllDefs)
:31       if (thinkTreeDef.insertTag == this.insertTag)
:36   this.matchedTrees = this.matchedTrees.OrderByDescending(tDef => tDef.insertPriority).ToList();
:38   for (int i = 0; i < this.matchedTrees.Count; i++)
:40       ThinkResult thinkResult = this.matchedTrees[i].thinkRoot.TryIssueJobPackage(pawn, jobParams);
:41       if (thinkResult.IsValid) return thinkResult;      // 首个有效者胜出
:46   return ThinkResult.NoJob;
:55   [Unsaved(false)] private List<ThinkTreeDef> matchedTrees;   // 首次求值后缓存
```

字段定义 `Verse/ThinkTreeDef.cs`：`insertTag` `:130`、`insertPriority`（float）`:133`。

**推出的语义**：
- 同 tag 的树按 `insertPriority` **降序**求值；
- **第一个产出有效 job 者胜出**，高优先级若产出无效则**继续下探**（→ 高优先级不会饿死他家，只是多一次求值开销）；
- 匹配列表在**首次求值**时构建并缓存（`matchedTrees` 为 `Unsaved`）→ 彼时全部 MOD 已加载完，**无需 `loadAfter`**。

### 1.2 求值时机：主树是**事件驱动**，不是每 tick

`Verse/AI/Pawn_JobTracker.cs`：

```
:111  public virtual void JobTrackerTickInterval(int delta)
:113      if (this.pawn.IsHashIntervalTick(30, delta))      // 常量树节拍 30 tick
:668  public void CheckForJobOverride(float minPriority = 0f, bool ignoreQueue = true)
:945  public bool IsCurrentJobPlayerInterruptible()
:977  public bool TryTakeOrderedJob(Job job, JobTag? tag = 0, bool requestQueueing = false)
:740  public Job TryOpportunisticJob(Job finalizerJob, Job job)
:235  public void StartJob(Job newJob, JobCondition lastJobEndCondition = ..., ThinkNode jobGiver = null,
                          ..., ThinkTreeDef thinkTree = null, ...)
```

**推出的结论**：主树只在「无 job / job 到期 / 被显式调用 `CheckForJobOverride`」时被求值。因此**「何时求值」必须与「选什么」解耦**——否则后期加抢占就是重写。

### 1.3 Job 契约：玩家优先序由 JobDef/Job 字段决定

`Verse/JobDef.cs`：`playerInterruptible = true` `:34`、`checkOverrideOnDamage` `:40`、`suspendable = true` `:49`、`casualInterruptible = true` `:52`、`allowOpportunisticPrefix` `:55`、`sleepCanInterrupt` `:70`。

`Verse/AI/Job.cs`：`loadID` `:671`、`expiryInterval = -1` `:677`、`checkOverrideOnExpire` `:680`、`playerForced` `:683`、`ignoreJoyTimeAssignment` `:764`。

**这直接约束框架产出的 job**：
- `playerInterruptible=false` → 会**屏蔽玩家指令**（自主压过玩家，不可接受）；保持 `true` 才是「自主可被玩家随时打断」。
- `playerForced`（`Job.cs:683`）是**玩家强制**标志 → 框架产出的 job **绝不能**置 true。
- 用户可见文案在原版一律是 `MustTranslate`（`JobDef.reportString` `JobDef.cs:30-31`；`ThinkNode_JobGiver.reportStringOverride` `Verse/AI/ThinkNode_JobGiver.cs:51`）→ 框架的 blocker/报告文案必须是**翻译键**，不能是自由文本。

### 1.4 心跳基座

`Verse/GameComponent.cs:15` `public virtual void GameComponentTick()` —— 框架自有节拍挂这里，不占用 `Pawn_JobTracker` 的 30-tick 通道。

### 1.5 ★ 一个被忽略的原版硬约束：ThinkNode 的 save key 唯一性

`Verse/ThinkTreeDef.cs`：

```
:19   ThinkTreeKeyAssigner.AssignKeys(this.thinkRoot, GenText.StableStringHash(this.defName));
:36   if (key == -1) → ConfigError "has invalid save key"
:48   else if (usedKeys.Contains(key)) → ConfigError "Two ThinkNodes have the same unique save key"
:114  "...already has a parent node... This means that it's referenced by more than one think tree
       (should have been copied instead)."
```

**推出**：思维树节点的存档键由**树 defName 派生**（`StableStringHash(defName)` `:19`），且原版 **ConfigErrors 会直接报错**：
- 两个节点 save key 相同 → 报错 `:48-58`；
- 同一节点实例被多棵树引用 → 警告「should have been copied instead」`:114`。

→ **框架设计后果（重要）**：若下游 MOD 向框架注册「自定义 ThinkNode 实例」，框架**必须 `DeepCopy`**（`ThinkNode.DeepCopy(bool resolve)` 原版已提供，`ThinkNode_SubtreesByTag.cs:11`、`ThinkNode_JobGiver.cs:19` 均是此模式的示范），**绝不可共享实例**；否则会触发原版 ConfigError 或静默串树。

### 1.6 由原版推出的硬约束汇总

| 编号 | 约束 | 原版依据 |
|---|---|---|
| **C1** | **触发时机必须与仲裁解耦** | 主树事件驱动（`Pawn_JobTracker.cs:668` 需显式调用） |
| **C2** | 框架只需产出**一个有效 job**；高优先级无效会下探，不饿死他家 | `ThinkNode_SubtreesByTag.cs:40-44` |
| **C3** | 框架 job 必须遵守 interruptible 契约，且 `playerForced` 恒 false | `JobDef.cs:34` / `Job.cs:683` |
| **C4** | 下游提供的节点实例**必须 DeepCopy**，不得跨树共享 | `ThinkTreeDef.cs:48-58` / `:114` |
| **C5** | 一切用户可见字符串是**翻译键**，非自由文本 | `JobDef.cs:30-31` / `ThinkNode_JobGiver.cs:51`（均 `MustTranslate`） |
| **C6** | 自有节拍走 `GameComponentTick`，不占 30-tick 通道 | `GameComponent.cs:15` |

---

## 2. 实现目标

### 2.1 功能目标

| 编号 | 目标 | 原版依据 | 可验证的验收 |
|---|---|---|---|
| **G1** | **中央意图仲裁**：框架独占一个 `insertTag` 槽位，下游只投递驱动力，由框架统一裁决 | `ThinkNode_SubtreesByTag.cs:38-44` | 全框架只有 1 个 `ThinkTreeDef`、1 个 `JobGiver`；新增 10 个下游 drive 后思维树挂载数**不变** |
| **G2** | **双档扩展**：简单驱动力纯 XML（`DefDatabase`），复杂驱动力实现接口注册 | `DefDatabase<T>`（原版 Def 驱动，零 patch） | 仅用 XML 即可新增一个可运行 drive，无需写 C# |
| **G3** | **与原版 Job 生命周期正确共存**：自主不压过玩家、不伪造玩家强制 | C3（`JobDef.cs:34` / `Job.cs:683`） | 框架 job 的 `playerForced` 恒 false、`playerInterruptible=true`；玩家下令可即时打断自主 job |
| **G4** | **触发源可插拔**：空闲期求值（默认）/ 工作中途抢占（可选） | C1（`Pawn_JobTracker.cs:668`） | 追加抢占 = 新增触发源调用同一仲裁入口，**仲裁与 API 代码零改动** |
| **G5** | **可观测可诊断**：可查询任意 pawn 的意图队列与仲裁结果 | 原版自身有 `debugLog` 开关（`Pawn_JobTracker.cs:670`） | 调试面板只读展示，开启后不改变任何仲裁结果 |
| **G6** | **公开 API 稳定可版本化**：冻结契约 + 版本探测 | 纯 C#，无原版强制 | `AutonomyApi.VersionString` 可读；下游仅引用 `Api` 层即可编译 |

### 2.2 非功能目标

| 编号 | 目标 | 依据 / 约束 |
|---|---|---|
| **G7** | **零硬依赖**：核心仅用原版类型；Harmony 仅抢占用到，且为**软依赖**，缺失自动降级为「仅空闲期自主」 | G4；不引入 HugsLib / Prepatcher |
| **G8** | **性能**：热路径无分配、无全库扫描。`GetPriority(Pawn)`（`Verse/AI/ThinkNode.cs:106`，virtual）与 drive 的紧迫度计算必须是**纯函数** | 树求值路径高频调用 |
| **G9** | **持久化**：schema 版本化、旧档可加载、新字段有默认值、迁移**幂等可诊断**；`FinalizeInit` 末尾回填中途加入 | `GameComponent` + `Scribe` |
| **G10** | **双语与零硬编码**：C# 不出现中文案，全部走 Keyed XML | C5 |
| **G11** | **兼容与隔离**：不干扰他家槽位；**per-drive 异常隔离**（单个下游 drive 抛错只跳过它） | C2；原版自身亦「无效则继续下一个」（`ThinkNode_SubtreesByTag.cs:41-44`） |

---

## 3. 功能模块

### 3.0 模块总览

| 模块 | 职责 | 主要原版接入点 | 需 Harmony |
|---|---|---|---|
| **M1** 驱动力注册 | 下游 drive 的注册与目录维护 | `DefDatabase<T>` | 否 |
| **M2** 意图模型 | 意图创建 / 状态机 / 过期 / 退避 | `IExposable` | 否 |
| **M3** 仲裁器 | 选出当前应执行的意图 | 语义对齐 `ThinkNode_SubtreesByTag.cs:40-44` | 否 |
| **M4** 心跳调度 | 意图累积/衰减/过期的节拍推进 | `GameComponent.GameComponentTick()` `GameComponent.cs:15` | 否 |
| **M5** 思维树桥接 | 唯一挂载出口 | `ThinkTreeDef.insertTag`/`insertPriority` `ThinkTreeDef.cs:130,133` | 否 |
| **M6** Job 翻译与回写 | 意图 → 原版 Job，并回写执行结果 | `ThinkNode_JobGiver.TryGiveJob` `:28`、`StartJob` `:235` | 否 |
| **M7** 抢占（**可选/延后**） | 工作中途插入自主行为 | `CheckForJobOverride` `:668`、`IsCurrentJobPlayerInterruptible` `:945` | **是（软依赖）** |
| **M8** 持久化 | 意图与状态存档、版本迁移 | `GameComponent` + `Scribe` | 否 |
| **M9** 观测诊断 | 意图队列与仲裁结果只读展示 | `debugLog` 思路 `Pawn_JobTracker.cs:670` | 否 |
| **M10** 兼容与降级 | 能力探测、per-drive 异常隔离 | 原版容错示范 `:41-44` | 否（探测 M7 用） |
| **M11** 公开 API | 冻结契约 + 版本探测 | 纯 C# | 否 |

### 3.1 M1 驱动力注册（Drive Registry）

- **职责**：接收下游注册的驱动力，维护唯一键目录，供 M3 查询。
- **原版依据**：`DefDatabase<T>` 是原版标准的零 patch 扩展机制；注册发生在 Def 解析后。
- **输入**：`AutonomyDriveDef`（XML 档）或 `IAutonomyDrive`（代码档）。
- **输出**：可枚举、可按键索引的 drive 目录。
- **约束**：唯一键；注册**幂等**（重复注册同一键不产生副本）；两档并存且不区别对待。
- **内建基线分级**：框架自带「原版需求 → `AutonomyTier`」的基线映射（17 个原版需求的原生级别与阈值见 `Pawn自驱力框架_原版需求分级大全与Tier映射.md`），下游可**覆盖**。目的：避免各 drive 自定档位导致量纲不可比，回到「分级」要解决的老问题。
- **★ XML 档的能力边界**：原版分级阈值有三种来源（常数 / **Def 派生 per-race** / **运行期计算 per-pawn**）。纯 XML 档**只能表达前两种**；第三种（如 `Need_Mood` 取 `mentalBreaker.BreakThresholdExtreme`）**必须走代码档**。此边界需在 G2 的双档说明中明示。
- **参考验证**：巴尔以 Def 承载个性/强度参数（数据驱动而非枚举硬编码）——仅验证「Def 驱动可行」。

### 3.2 M2 意图模型（Intent Model）

- **职责**：意图的创建、状态机、过期、退避、回收。
- **原版依据**：持久化实体走 `IExposable`；原版实体自带标识（`Job.loadID` `Job.cs:671`）。
- **状态**：`Planned / Acting / Completed / Blocked`。
- **约束（C5）**：`blockerKey` 是**翻译键**——依据 `JobDef.reportString` 与 `ThinkNode_JobGiver.reportStringOverride` 均为 `MustTranslate`。
- **参考验证**：巴尔的 `PersonalIntent`（含 urgency / attempts / notBeforeTick）——仅验证「这些字段确有必要」，其 `blocker` 是自由字符串，**我们不照抄**（改为翻译键）。
- **⚠️ 已知能力缺口**：Intent 是「一次 Job」模型，**无法表达跨 Job 的长期目标**（如「攒钱买房」需多次 job 推进）。经济类需求需在其上增设目标层，见 `Pawn自驱力框架_经济驱动的应用配置.md` §4，**该增补尚未批准**。

### 3.3 M3 仲裁器（Arbiter）

- **职责**：从候选意图中选出当前应执行的那个。
- **原版依据（C2）**：原版「首个 valid 胜出、无效则下探」（`ThinkNode_SubtreesByTag.cs:40-44`）→ 框架内部同样应**产出即停**，且高优先级产出无效时下探，不做全量排序的浪费。
- **约束**：**纯函数**、无副作用；排序键与并发上限**可配置**。
- **★ 明确不照抄**：巴尔的「并发 3 条 / 顶替阈值 0.06」是其**具体调参**，原版无此约束 → 降级为**默认配置值**，不做成硬编码常量。
- **排队与分级**：多意图并存时的**分级定义、排序键、防饿死老化、出队算法**不在本节展开，见子文档 `Pawn自驱力框架_需求分级与排队设计.md` §3–§4（该子文档对 M1/M2/M3/M4/M5/M9/M10 有增补，见其 §5）。
- **参考验证**：仅验证「中央仲裁 + 单一 JobGiver」这一结构可行。

### 3.4 M4 心跳调度（Scheduler）

- **职责**：推进意图的累积、衰减、过期。
- **原版依据（C6）**：`GameComponent.GameComponentTick()` `GameComponent.cs:15`。原版常量树用 `IsHashIntervalTick(30, delta)`（`Pawn_JobTracker.cs:113`）示意「低频 + hash 分散」是原版既有做法。
- **约束**：不挂每 tick；用 hash interval 分散 pawn，避免同帧尖峰。

### 3.5 M5 思维树桥接（ThinkTree Bridge）

- **职责**：把框架挂进原版思维树，并作为**唯一出口**。
- **原版依据**：`ThinkTreeDef.insertTag` `:130` / `insertPriority` `:133`；求值语义 `ThinkNode_SubtreesByTag.cs:24-47`。
- **约束**：
  1. **只占一个 tag**（`Humanlike_PostMain`）；`insertPriority` 取**唯一不重叠**值，不用 `0` 或整数平手值（降序求值下平手不可预测）。
  2. **C4**：下游若提供自定义节点实例 → **必须 `DeepCopy`**（`ThinkNode.DeepCopy`），否则触发原版 ConfigError（`ThinkTreeDef.cs:48-58`）或跨树引用警告（`:114`）。
  3. 可提供**动态优先级**：覆写 `ThinkNode.GetPriority(Pawn)`（`Verse/AI/ThinkNode.cs:106`，virtual）——但受 **G8** 约束（纯函数、无分配）。**v1 不启用**，留作实测后优化。
- **参考验证**：Misc. Robots 覆写 `GetPriority` —— 仅验证「覆写可行」，其阈值是它自己的业务值，不照抄。

### 3.6 M6 Job 翻译与回写（Job Translation）

- **职责**：把选中意图翻译成原版 `Job`，并在执行结束后回写结果（成功/失败/阻塞）供 M2 更新状态。
- **原版依据**：`ThinkNode_JobGiver` 是官方 leaf 抽象——`protected abstract Job TryGiveJob(Pawn pawn)` `:28`，`TryIssueJobPackage` 负责包装成 `new ThinkResult(job, this, null, false)` `:46`；启动走 `Pawn_JobTracker.StartJob(...)` `:235`。
- **约束（C3）**：
  - `playerForced` **恒 false**（`Job.cs:683`）；
  - `playerInterruptible` 保持 `true`（`JobDef.cs:34`），保证玩家可随时打断；
  - 报告文案走 `MustTranslate` 字段（`ThinkNode_JobGiver.reportStringOverride` `:51`）；
  - 若需到期后重新求值，设置 `expiryInterval` / `checkOverrideOnExpire`（`Job.cs:677,680`）。
- **参考验证**：原版自身 200+ 个 JobGiver 皆为此形态——这是**官方设计意图**，非借鉴 MOD。

### 3.7 M7 抢占（Preemption，可选 / 延后）

- **职责**：在 pawn 正在执行 job 时插入自主行为。
- **原版依据（C1）**：主树事件驱动，需显式 `CheckForJobOverride(float minPriority = 0f, bool ignoreQueue = true)` `Pawn_JobTracker.cs:668`；能否打断由 `IsCurrentJobPlayerInterruptible()` `:945` 判定。
- **约束**：
  1. **必须尊重** `IsCurrentJobPlayerInterruptible()`——玩家不可打断的 job 不得被抢占；
  2. 不得抢占 `playerForced` 的 job（`Job.cs:683`）；
  3. **单一持有**：抢占补丁只由框架持有，下游不得自行 patch；
  4. **G7 降级**：Harmony 缺失 → 自动退回「仅空闲期自主」，不报错不崩溃。
- **★ 为何不会返工**：仲裁在 M3，本模块只是**新增一个触发源**去调用 M3 的同一入口（C1）。
- **参考验证（反例）**：Common Sense / CAI 5000 争用 `StartJob` → 我们**避开**，改选 `CheckForJobOverride` 这条原版原生入口（更低侵入、当前生态零占用）。

### 3.8 M8 持久化（Persistence）

- **职责**：意图与 drive 状态的存档、读档、版本迁移。
- **原版依据**：`GameComponent` + `Scribe`；原版实体自带 `loadID`（`Job.cs:671`）。
- **约束（G9）**：schema 版本可识别；新字段有默认值；旧档可加载；迁移幂等可诊断；`FinalizeInit` 末尾回填（中途加入存档）。

### 3.9 M9 观测诊断（Observability）

- **职责**：只读展示某 pawn 的意图队列、仲裁结果、被跳过的 drive 及原因。
- **原版依据**：原版自身提供 `debugLog` 开关（`Pawn_JobTracker.cs:670` `if (this.debugLog)`）——「可诊断」是原版既有价值观。
- **约束**：严格只读，开启与否**不改变**任何仲裁结果。

### 3.10 M10 兼容与降级（Compatibility & Fallback）

- **职责**：Harmony 等软依赖的能力探测；per-drive 异常隔离；不占用他家槽位。
- **原版依据（G11）**：原版自身即「单点无效 → 继续下一个」（`ThinkNode_SubtreesByTag.cs:41-44`），框架应做**更细粒度的 per-drive 隔离**。
- **约束**：单个 drive 抛异常只跳过该 drive；日志**节流**（避免热路径刷屏）；首个异常后可标记该 drive 本局禁用。

### 3.11 M11 公开 API（Public Api）

- **职责**：冻结的公开契约 + 版本探测门面。
- **约束**：只依赖 `Domain`（不依赖 Engine / Data / AI / UI）；下游**仅引用本层**即可编译；版本化（G6）。
- **参考验证**：本工程 `RUIF.Api`（版本探测门面，`VersionString`）已是同构先例。

---

## 4. 模块依赖与分层映射

沿用工程铁律 `UI → Application → Domain → Data` 单向，`Data` 不依赖 `Application`：

```
下游 MOD ──▶ M11 Api ──▶ M1/M2（Domain）
                            ▲
M9 UI ──▶ M4 Engine(调度) ──┤
                            │
M5 思维树桥接 ──▶ M6 Job翻译 ──▶ M3 仲裁器 ──▶ M1/M2
      │                              ▲
      └──▶ M7 抢占(可选) ────────────┘  （只新增触发源，不改 M3）
M8 持久化 ──▶ Data(GameComponent)
M10 兼容 ──▶ 横切（M7 探测 / M1 注册隔离）
```

- **M3 仲裁器是唯一决策中心**，M5/M6/M7 都只是它的**调用方/翻译层**——这就是 C1 解耦的落点。
- **M8 仅持数据**，编排逻辑全在 M4，避免 `Data` 承载业务规则。

---

## 5. 目标 ↔ 模块 覆盖矩阵

| 目标 | 主要承载模块 | 辅助 |
|---|---|---|
| G1 中央仲裁 | M3 + M5 | M11 |
| G2 双档扩展 | M1 | M11 |
| G3 Job 契约共存 | M6 | M3 |
| G4 触发源可插拔 | M5 / M7 | M4 |
| G5 可观测 | M9 | — |
| G6 API 稳定 | M11 | — |
| G7 零硬依赖/可降级 | M10 | M7 |
| G8 性能 | M3（纯函数）+ M5（GetPriority） | M4 |
| G9 持久化 | M8 | M2 |
| G10 双语零硬编码 | M2（blockerKey）+ M6（reportString） | 全部 |
| G11 兼容隔离 | M10 | M1 |

---

## 6. 参考 MOD 的定位（明确降级为「验证」）

| MOD | 验证了什么 | 我们采纳 | **明确不照抄** |
|---|---|---|---|
| 巴尔 `BPC_PersonalAutonomy` | 「意图引擎 + 单一 JobGiver」结构可行 | 结构（M3 ↔ M6 分层） | 仲裁参数（并发 3 / 阈值 0.06）是其调参，**原版无此约束** → 只作默认值；`blocker` 自由字符串 → 改翻译键（C5） |
| Misc. Robots | 覆写 `GetPriority` 可行 | 「可行」这一事实（依据 `ThinkNode.cs:106` virtual） | 其阈值/时段逻辑是其业务，不照抄 |
| Common Sense / CAI 5000 | —— | —— | 争用 `StartJob` → **避开**；改走 `CheckForJobOverride`（原版原生入口） |
| Hospitality | Duty 树内需回挂原版 tag | 若将来做 Duty 则必须回挂 | 与本框架 v1 无关（我们不用 Duty 通道） |
| 原版自身 200+ JobGiver | `ThinkNode_JobGiver` 是官方 leaf 抽象 | **直接采纳为 M6 形态**（这是官方意图，非借鉴 MOD） | —— |

> 原则：**原版代码是设计来源，MOD 只是「有人这么干过且没炸」的证据。**

---

## 7. 未决与风险

### 7.1 未决

1. **M7 抢占是否纳入**（用户延后）。已按 C1 解耦，追加不返工；但若最终要做，建议在 API 冻结前决策——可能需要新增「触发源注册」类 API。
2. **命名 / Def 前缀**（提案 `PawnAutonomy` / `PAF_`）——影响 `ThinkTreeDef.defName`，而 defName 又**派生 save key**（`ThinkTreeDef.cs:19`），故须在首次挂载前定稿。
3. **`insertTag` 求值顺序从未实机验证**——源码结论（`OrderByDescending`）未经游戏内实测，需在 P1 spike 用 Dev 模式打印 `matchedTrees` 确认。

### 7.2 风险

| 风险 | 缓解 |
|---|---|
| 下游提供共享节点实例 → 触发原版 ConfigError（C4） | M5 强制 `DeepCopy`；M1 注册时校验 |
| 框架 job 误设 `playerForced` → 压过玩家（C3） | M6 硬约束 + 单测断言 |
| `GetPriority` 热路径分配 → 性能（G8） | v1 不启用动态优先级；启用后须纯函数 |
| 公开 API 冻结后重构成本高 | 先完成 P1 spike（含实机验证）再冻结 |
| M7 与生态争用 | 选 `CheckForJobOverride`；避开 `StartJob` |

---

*本文为需求/规划产物，**未产生任何代码改动**。全部原版断言均可在 `_research/vanilla/Assembly-CSharp/` 按 `路径:行号` 复核。*

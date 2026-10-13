# Pawn 自驱力框架 —— 需求分级与排队设计

> 本文是 `Pawn自驱力框架_SDD需求与功能模块.md` 的**深化子文档**，只解决一件事：**自驱力如何分级，需求如何有序排队**。
> 冲突时以 SDD 为准；本文对 SDD 的 M2 / M3 / M5 提出**增补**（见 §5），不改变其职责边界。
> 推导原则同 SDD：**原版程序集是设计来源，MOD 仅作「有人这么干过且没炸」的验证**。

---

## 0. 范围与结论摘要

**要解决的问题**：SDD 的 M3 仲裁器只规定了「选出当前应执行的意图」，但没规定**多个意图同时存在时按什么顺序排队**。若只有连续 urgency 排序，会出现两类失效：

1. **量纲不可比** —— 下游 drive 各自上报的数值没有统一口径（「饥饿 0.8」与「想去散步 0.8」不可比），直接排序无意义；
2. **饿死与抖动** —— 一个持续高值的 drive 会长期霸占出口，低值意图永不出队；且需求在阈值附近抖动时，执行目标会反复横跳。

**结论**：引入**离散分级（`AutonomyTier`）作为主导排序键**，连续 urgency 只在同一档内起作用，档内用 FIFO + 受限老化保证公平。排序键结构直接搬原版 `Pawn_WorkSettings` 的「档位 × 大权重 + 档内微调」，仲裁循环直接搬 `ThinkNode_PrioritySorter` 的「选最大 → 尝试 → 无效则下探」。

---

## 1. 原版基线：原版自己怎么做分级与排队

原版**没有**一处统一的「需求分级与排队」设施，但有四个各自独立、可直接借鉴的实现。逐条给出源码依据。

### 1.1 ★ `Pawn_WorkSettings` —— 最完整的「分级 + 有序排队」范本

`RimWorld/Pawn_WorkSettings.cs`

| 事实 | 源码 |
|---|---|
| 优先级是 **1–4 的整数档**，1 最高 | `:353` `LowestPriority = 4`、`:356` `DefaultPriority = 3` |
| 越界只告警不抛错 | `:177-180` `if (priority < 0 \|\| priority > 4) Log.Message(...)` |
| **排序键 = 档位 × 100000 + 档内微调** | `:267-268` `a.naturalPriority + (4 - GetPriority(a)) * 100000` |
| 用**稳定排序**保同档顺序 | `:265` `InsertionSort`（`Verse` 扩展，稳定） |
| 高优先级可**全局降级为扁平** | `:194-197` 若 `!Find.PlaySettings.useWorkPriorities`，所有 `>0` 的优先级**一律返回 3** |
| 紧急项**单独出一张表** | `:270-295` 按 `WorkGiverDef.emergency` 拆成 `workGiversInOrderEmerg` / `workGiversInOrderNormal` |
| 结果**缓存**，脏标记驱动重算 | `:244` `CacheWorkGiversInOrder()`、`:28` `workGiversDirty` |

**这条最重要**。`(4 - priority) * 100000` 的含义是：**把「档位」抬升到一个足够大的量级，使档内微调值无论多大都不可能跨越档位边界**。这正是「分级让需求有序排队」所需的数学性质——分档必须是**主导项**，而不是和连续分并列相加。

注意 `:265-269` 的比较写法是 `b.CompareTo(a)`，即**降序**（键大的在前）：`priority=1` → `(4-1)*100000=300000`，`priority=4` → `0`，所以数字小的档排前面。

### 1.2 `Need_Food.CurCategory` —— 需求分级，但硬编码在子类里

`RimWorld/Need_Food.cs:69-87`

```csharp
public HungerCategory CurCategory
{
    get
    {
        if (base.CurLevelPercentage <= 0f)                                return HungerCategory.Starving;
        if (base.CurLevelPercentage < this.PercentageThreshUrgentlyHungry) return HungerCategory.UrgentlyHungry;
        if (base.CurLevelPercentage < this.PercentageThreshHungry)         return HungerCategory.Hungry;
        return HungerCategory.Fed;
    }
}
```

阈值本身也是派生的（`:24-39`）：`PercentageThreshHungry = RaceProps.FoodLevelPercentageWantEat * 0.8f`，`UrgentlyHungry = * 0.4f`。

**关键事实**：`Need` 基类（`RimWorld/Need.cs`）**完全没有分级成员**——只有 `CurLevel` / `CurLevelPercentage`（`:66-91`）和 `threshPercents`（`:367`，仅用于画 UI 刻度线）。分级是**每个具体 Need 子类各自用枚举 + 阈值实现**的（`Need_Food` → `HungerCategory`，`Need_Rest` → `RestCategory`，心情走 mood stages）。

即：**原版的分级是「封闭集合 + 各自硬编码」**。这在原版可行（需求类型有限且已知），但**框架不能用**——框架的 drive 是开放集合、由第三方提供、量纲各异。见 §3.4 的处理。

### 1.3 ★ `ThinkNode_PrioritySorter` —— 运行期动态优先级 + 无效下探

`Verse/AI/ThinkNode_PrioritySorter.cs:18-71`，这是原版**运行期仲裁**的完整实现：

| 行为 | 源码 |
|---|---|
| 候选必须 **priority > 0** 且 **≥ minPriority** | `:43` `if (num3 > 0f && num3 >= this.minPriority && num3 > num)` |
| 每轮**只选当前最大**，不预排序 | `:30-48` 线性扫描 |
| 取最大者尝试产出；**无效则移除并继续下一轮** | `:64-68` `if (thinkResult.IsValid) return; ... workingNodes.RemoveAt(num2);` |
| 全部无效 → `NoJob`，交还给上层 | `:70` |
| **per-node 异常隔离**（算优先级时） | `:37-42` `catch { Log.Error(...); }`，不中断循环 |
| **per-node 异常隔离**（产出时） | `:58-63` 同上 |
| 工作列表是 **static 复用**，热路径零分配 | `:77` `private static List<ThinkNode> workingNodes` |
| 平局靠**随机插入**打破 | `:24` `Insert(Rand.Range(0, count - 1), subNodes[i])` |

**这条次重要**，且直接对应 SDD C2（首个 valid 胜出、无效下探）。它的算法骨架应作为框架仲裁器（M3）的**同构实现**——包括异常隔离与零分配，这两点 SDD 的 G8 / G11 已经要求，此处找到了官方写法。

### 1.4 `ThinkNode_Priority` —— 顺序即优先级

`Verse/AI/ThinkNode_Priority.cs:9-31`：按 `subNodes` 顺序逐个尝试，首个 valid 即返回，同样带异常隔离（`:19-24`）。这是**静态顺序**版本，无评分。

### 1.5 其他可用锚点

- **`MentalBreakIntensity`**（`Verse/MentalBreakIntensity.cs:6-16`）：`None / Minor / Major / Extreme` —— 原版**全局分级枚举**的现成范例，可直接对齐框架档位命名。
- **`JobQueue`**（`Verse/AI/JobQueue.cs`）：`EnqueueFirst` `:62` / `EnqueueLast` `:68` / `Dequeue` `:113` / `Peek` `:125` —— 原版字面意义的队列。`EnqueueFirst` 是**插队**语义，对应高分级意图的入队方式（见 §4.2）。
- **`ThinkNode_ConditionalNeedPercentageAbove`**（`RimWorld/ThinkNode_ConditionalNeedPercentageAbove.cs:20-32`）：阈值判定的官方写法，且**对 `Need_Seeker` 用 `CurInstantLevelPercentage`、其余用 `CurLevelPercentage`**（`:27-31`）——框架读需求时需注意这个差异。

### 1.6 原版缺什么（框架必须补的缺口）

| 缺口 | 为什么原版不用管而框架要管 |
|---|---|
| **跨量纲的统一分级标尺** | 原版需求是封闭集合、各自有枚举；框架面对开放第三方 drive（§1.2） |
| **可配置的分级阈值** | 原版阈值硬编码在各 `Need` 子类里，框架不能改原版类（§1.2） |
| **防饿死** | 原版工作优先级依赖玩家手动调档；自主行为无人值守，必须自动老化 |
| **跨 drive 的公平排队** | 原版一个 pawn 同一时刻只走一条工作链；框架是多个来源竞争一个出口 |

---

## 2. 设计目标

| 编号 | 目标 | 判定方式 |
|---|---|---|
| **TG1** | 分级**主导**排序：高档意图恒优先于低档，档内分数不得跨档 | 对任意 `tier_a < tier_b`，恒有 `key(a) > key(b)` |
| **TG2** | 档内**有序**：同档按入队顺序（FIFO），结果可预期、可复现 | 同档同 urgency 时，先入队者先出队 |
| **TG3** | 档内**老化防饿死**：等待越久档内分越高，但**永不跨档** | 老化增量有上限 `AgingCap`，且 `AgingCap < 档间距` |
| **TG4** | 无效下探：队首产出失败时不卡死，自动尝试下一个 | 对齐 `ThinkNode_PrioritySorter.cs:64-68` |
| **TG5** | 分级可**全局扁平化**（排查与性能兜底） | 对齐 `Pawn_WorkSettings.cs:194-197` |
| **TG6** | 热路径**零分配、无异常逃逸** | 复用 static 工作列表；per-drive 异常隔离 |
| **TG7** | 分级**可观测**：能解释「为什么是它」 | 调试面板展示 tier / urgency / aging / 队列位次 |

---

## 3. 分级体系（AutonomyTier）

### 3.1 五档定义与原版锚点

**编号越小越紧急**（与原版工作优先级一致：1 最高、4 最低）。

| 档 | 枚举名 | 语义 | 原版锚点 |
|---|---|---|---|
| **T0** | `Critical` | 生存/崩溃临界，应立即处理 | `HungerCategory.Starving`（`Need_Food.cs:75`）、`MentalBreakIntensity.Extreme` |
| **T1** | `Urgent` | 严重不足，短期会恶化 | `HungerCategory.UrgentlyHungry`（`:77`）、`Major` |
| **T2** | `Wanted` | 明确想要，正常排队处理 | `HungerCategory.Hungry`（`:81`）、`Minor` |
| **T3** | `Idle` | 空闲才做（纯改善型） | 默认档，对齐 `DefaultPriority = 3`（`Pawn_WorkSettings.cs:356`） |
| **T4** | `Whim` | 兴之所至，最低优先 | 框架新增（承载巴尔那类个性驱动，非原版概念） |

**`MaxTier = 4`**，与 `Pawn_WorkSettings.LowestPriority = 4` 同构，使排序键可直接套用 `(MaxTier - tier) * TIER_WEIGHT`。

> ⚠️ `MentalBreakIntensity.None` 不设对应档——框架不表达「无需求」，未分级的意图一律落到 `T3`（见 §3.3）。

### 3.2 排序键公式

```
sortKey(tier, urgency, agingTicks) = (MaxTier - (int)tier) * TIER_WEIGHT
                                   + Normalize01(urgency)
                                   + Aging(agingTicks)
```

| 项 | 取值 | 依据 |
|---|---|---|
| `TIER_WEIGHT` | **`100000f`** | 直接沿用 `Pawn_WorkSettings.cs:267` 的 `100000` |
| `MaxTier` | `4` | 对齐 `LowestPriority = 4` `:353` |
| `Normalize01(urgency)` | `[0, 1)` | 框架约束；要求 drive 自报的 urgency 先归一化 |
| `Aging(t)` | `[0, AgingCap]` | 见 §4.3 |

**§3.1 的 TG1 由此自动成立**：`tier` 每差 1 档，键差 `100000`；而档内两项之和恒 `< 1 + AgingCap ≈ 2`，**比档间距小 5 个数量级**，不可能跨档。

### 3.3 Tier 的三个来源（优先级递减）

1. **运行期上报**：`IAutonomyDrive.EvaluateTier(Pawn)` —— 动态，可响应需求水位变化。
2. **需求阈值映射表**（`tierFromNeed`）：框架在心跳时读原版 need 的百分比，按配置阈值表映射成档 —— **这是纯 XML 档也能动态分级的关键**，否则 XML 档只能静态分档。
   > **★ 后续修正（见 `Pawn自驱力框架_原版需求分级大全与Tier映射.md` §3 F1 / §6.2）**：原版阈值实有**三种**来源——硬编码常数、**Def 派生（per-race，如 `Need_Food` 的 `RaceProps.FoodLevelPercentageWantEat * 0.8`）**、**运行期计算（per-pawn，如 `Need_Mood` 取 `mentalBreaker.BreakThresholdExtreme`）**。故本项须扩展为三选一；**第三种（Mood 类动态阈值）纯 XML 档无法表达，只能走代码档**。另须增加 `invert` 字段——Beauty / Chemical / Outdoors / Indoors 四个需求的判定方向与其他需求相反。
3. **静态声明 / 默认值**：`DriveDef.tier`，缺省 `T3 Idle`。

**阈值映射表是对 §1.2 的外置化**：原版把分级硬编码在每个 `Need` 子类里（`HungerCategory` + `*0.8` / `*0.4`），框架改不了原版类，所以把同样的「阈值 → 离散档」逻辑做成 Def 配置。读需求时须遵 `ThinkNode_ConditionalNeedPercentageAbove.cs:27-31` 的差异：`Need_Seeker` 用 `CurInstantLevelPercentage`，其余用 `CurLevelPercentage`。

### 3.4 对原版的**有意偏离**（须记录）

原版是「**每个需求一套自己的枚举 + 硬编码阈值**」（§1.2），框架改为「**统一五档 + 可配置阈值**」。

**理由**：原版的做法依赖「需求类型封闭且已知」这一前提；框架的 drive 是开放集合、由第三方提供、量纲各异，无法为每一种都定义枚举。**统一标尺是开放集合下的唯一可行解**，代价是损失了各需求的语义化档名（用 `labelKey` 翻译键补偿，遵守 C5）。

---

## 4. 排队模型

### 4.1 队列结构

- **per-pawn 意图队列**（`IntentQueue`），挂在 M8 持久化之下，与 pawn 同生命周期。
  - 依据：原版 `Pawn_JobTracker` 的 job 队列是 per-pawn 的；`Need` 亦 per-pawn（`Need.cs:361` `protected readonly Pawn pawn`）。
- **不做全局队列**：全局队列会让殖民地的自主行为互相挤占，且失去 per-pawn 公平性。
- **队列容量上限** `MaxQueuedIntents`，**默认建议 8**（**建议值，非原版约束**；巴尔并发 3 是其调参，不照抄）。超限时按 §4.2 淘汰。

### 4.2 入队与淘汰

| 情形 | 处理 |
|---|---|
| 新增 `T0 Critical` | `EnqueueFirst` 语义（对齐 `JobQueue.cs:62`）：直接插到**本档队首** |
| 新增其他档 | `EnqueueLast`（`JobQueue.cs:68`）：追加到**本档队尾** |
| 队列已满 | 淘汰**当前最低档中键最小**的一个；若最低档有多个，淘汰最老者 |
| 同一 drive 重复提交 | **合并**（刷新 urgency / tier），不产生副本 —— 对齐 SDD M1 的注册幂等 |

### 4.3 档内老化（防饿死）

```
Aging(agingTicks) = min(agingTicks * AgingRatePerTick, AgingCap)
```

**建议值（可调，非原版约束）**：`AgingRatePerTick = 0.0005f`、`AgingCap = 0.9f`。

**为什么这组值是安全的**（TG3 的跨档约束）：
- 一游戏日 = 60000 tick，老化累积 `60000 × 0.0005 = 30`。
- `AgingCap = 0.9 < 1`，故 `Normalize01(urgency) + Aging < 1.9`。
- 而档间距 = `TIER_WEIGHT = 100000`。
- **老化量级比档间距小约 4 个数量级 ⇒ 永不跨档。** ✅

> 反向约束（写给后续调参者）：若把 `AgingCap` 调到 `≥ TIER_WEIGHT`，分级即失效——所有等待久的低档意图会涌到高档。**此约束应在代码里用断言或 ConfigError 兜住。**

### 4.4 出队与仲裁循环（同构 `ThinkNode_PrioritySorter`）

M3 仲裁器**不预排序**，而是复用 `ThinkNode_PrioritySorter.cs:18-71` 的骨架：

```
1. 把队列中「未被退避、未过期」的意图装入 static 复用工作列表
2. 循环：
   a. 线性扫描，找 sortKey 最大者（要求 > 0 且 ≥ minPriority，对齐 :43）
   b. 无候选 → break，返回 NoJob（交还上层，不阻塞原版主树）
   c. 尝试产出 Job
   d. 有效 → 返回（对齐 :64-66）
   e. 无效 → 从本轮工作列表移除，继续（对齐 :68）
3. per-intent 异常隔离：任何一步抛异常只跳过该意图并节流记日志（对齐 :37-42 / :58-63）
```

**与 SDD C2 的关系**：C2 要求「产出即停、无效下探」，本循环正是其具体实现；**不预排序**同时满足 G8（热路径零分配，`workingNodes` static 复用）。

### 4.5 平局打破：FIFO 而非随机

原版 `ThinkNode_PrioritySorter` 用**随机插入**打破平局（`:24`）。**框架不照抄，改用 `intentId` 升序（FIFO）**。

**理由**：原版随机是合理的——工作类型之间本应平权，随机避免总是先做同一类活。但自驱力的诉求是「**有序排队**」，玩家与设计者需要**可预期、可复现**的结果；随机平局会让同一存档两次加载表现不同，且无法解释「为什么是它」（违反 TG7）。

> 若后续实测发现同档意图长期单调重复，可选方案是「档内 FIFO + 完成时轮换到本档队尾」，仍保持确定性。

### 4.6 退避、阻塞与并发

| 概念 | 处理 | 与排队的关系 |
|---|---|---|
| **退避**（`notBeforeTick`） | 失败后暂时不参与出队，但**老化继续累积** | 防止退避导致永久饿死 |
| **阻塞**（`blockerKey`） | 记录阻塞原因（**翻译键**，C5），保留在队列中 | 阻塞期间可参与老化，待条件缓解后自然上浮 |
| **过期** | 超过有效期直接出队丢弃 | 与老化互斥：过期清理优先 |
| **并发** | 同一时刻**只有一个**意图在执行（框架只有一个 Job 出口，SDD M5） | 「并发上限」是**队列内同时保持活跃的意图数**，非同时执行的 Job 数 |

> ⚠️ 命名澄清：SDD 提到巴尔「并发 3 条」，那是它**同时保留 3 个候选意图**的调参；本框架的「队列」与「并发」是两件事，避免混淆。

---

## 5. 对 SDD 模块的增补

| 模块 | 增补内容 | 是否改变 SDD 职责 |
|---|---|---|
| **M1 注册** | `DriveDef` 增加 `tier`（静态档）、`tierFromNeed`（阈值映射表）；注册时校验阈值表单调性 | 否，纯扩展字段 |
| **M2 意图模型** | 增加 `tier`、`enqueueTick`、`agingTicks`、`intentId` 字段；均为 `IExposable` 持久化 | 否，扩展状态字段 |
| **M3 仲裁器** | 排序键改为 §3.2 公式；循环改为 §4.4 同构 `PrioritySorter`；加 `AgingCap < TIER_WEIGHT` 约束校验 | 否，仍是「选一个」 |
| **M4 心跳** | 增加「按 §3.3 重算 tier」与「推进 agingTicks」两步 | 否，仍是节拍推进 |
| **M5 桥接** | 优先级仍由 `insertPriority` 单一槽位决定；**档位不映射到 `GetPriority`**（避免与原版档位语义混淆），动态优先级仍按 SDD §3.5 留待实测 | 否 |
| **M9 观测** | 调试面板展示队列全量：位次 / tier / urgency / aging / 阻塞原因 | 否，扩展展示项 |
| **M10 降级** | 增加 `useAutonomyTiers` 全局开关；关闭时所有意图压到 `T3`（对齐 `Pawn_WorkSettings.cs:194-197`） | 否，新增降级通道 |

**未新增模块** —— 分级与排队是 M2/M3 的内部机制，不值得单列模块。

---

## 6. Def 配置示意（草案，未实现）

```xml
<!-- 形态示意：纯 XML 档的 drive 也能动态分级 -->
<AutonomyDriveDef>
  <defName>PAF_EatWhenHungry</defName>
  <label>PAF_Drive_Eat_Label</label>              <!-- 翻译键，C5 -->
  <description>PAF_Drive_Eat_Desc</description>   <!-- 翻译键，C5 -->

  <!-- 分级来源：需求阈值映射（§3.3 第 2 优先级） -->
  <tierFromNeed>
    <need>Food</need>
    <!-- 按百分比从高到低匹配，首个命中即生效；映射自 Need_Food.CurCategory（Need_Food.cs:69-87） -->
    <thresholds>
      <li><above>0.00</above><tier>Critical</tier></li>   <!-- <=0 即 Starving -->
      <li><above>0.24</above><tier>Urgent</tier></li>     <!-- 0.8 * 0.4 量级示例值 -->
      <li><above>0.56</above><tier>Wanted</tier></li>     <!-- 0.8 量级示例值 -->
    </thresholds>
    <defaultTier>Idle</defaultTier>
  </tierFromNeed>

  <urgencyFromNeed>                                <!-- 档内连续分，[0,1) -->
    <need>Food</need>
    <invert>true</invert>                          <!-- 越饿 urgency 越高 -->
  </urgencyFromNeed>

  <maxConcurrent>1</maxConcurrent>
</AutonomyDriveDef>
```

> ⚠️ 上表阈值 `0.24 / 0.56` 为**示意值**，实际应取 `RaceProps.FoodLevelPercentageWantEat` 派生（`Need_Food.cs:24-39`）。

---

## 7. 明确不照抄的东西

| 原版 / MOD 做法 | 为什么不照抄 |
|---|---|
| 原版「每个 Need 子类一个枚举 + 硬编码阈值」（§1.2） | 依赖封闭集合；已外置为可配置阈值表（§3.4） |
| `ThinkNode_PrioritySorter` 的**随机**平局打破（`:24`） | 破坏可预期性与可解释性，改确定性 FIFO（§4.5） |
| 巴尔「并发 3 条 / 顶替阈值 0.06」 | 是其**具体调参**，原版无此约束 → SDD §3.3 已降级为默认配置值 |
| Misc. Robots 的 `GetPriority` 阈值 | 只采纳「覆写可行」；阈值是其自家业务值（SDD §3.5 已述） |
| `Pawn_WorkSettings` 的 `emergency` 双表（`:270-295`） | **v1 不采用**。紧急通道已由 `T0 Critical` 档表达；双表会增加一条并行路径，收益不明确，留待实测 |

---

## 8. 未决与风险

| # | 事项 | 说明 |
|---|---|---|
| 1 | **五档是否够用 / 是否过多** | 原版工作优先级是 4 档、精神崩溃强度是 4 级。取 5 档（多一个 `Whim` 承载个性驱动）是**建议**，需实测。档位数量一旦进入公开 API 就难改（SDD G6）。 |
| 2 | `AgingRatePerTick` / `AgingCap` 具体值 | §4.3 给出的是**满足跨档约束的安全起点**，非最优值；需实机观察「低档意图的实际等待时长」再调。 |
| 3 | 队列容量 8 | 建议值。过大增加每轮扫描成本，过小导致意图频繁淘汰。 |
| 4 | `tierFromNeed` 的 `Need_Seeker` 分支 | 需遵 `ThinkNode_ConditionalNeedPercentageAbove.cs:27-31` 用 `CurInstantLevelPercentage`——**此差异尚未实机验证**。 |
| 5 | 老化是否应跨「驱动类型」而非仅跨时间 | 本设计只按等待时长老化。若某 drive 天然高频提交，可能比低频 drive 更占优，是否需按「出队次数」归一化，暂不开口子。 |
| 6 | 全局扁平化开关的默认值 | `useAutonomyTiers` 默认应为 `true`；但是否暴露到 mod 设置界面，取决于受众定位（当前定为公开框架，倾向暴露）。 |

---

## 附录 A：本文引用的原版源码清单

| 文件 | 行/成员 | 用途 |
|---|---|---|
| `RimWorld/Pawn_WorkSettings.cs` | `:353` `:356`、`:177-180`、`:194-197`、`:265-269`、`:270-295`、`:244` | 分级主导排序键、稳定排序、全局扁平化、紧急双表、缓存 |
| `RimWorld/Need_Food.cs` | `:24-39`、`:69-87` | 需求分级（阈值 → 离散档） |
| `RimWorld/Need.cs` | `:66-91`、`:361`、`:367` | 基类无分级成员（§1.2 的关键事实） |
| `Verse/AI/ThinkNode_PrioritySorter.cs` | `:18-71`、`:24`、`:43`、`:64-68`、`:37-42`、`:58-63`、`:77` | 仲裁循环骨架、无效下探、异常隔离、零分配 |
| `Verse/AI/ThinkNode_Priority.cs` | `:9-31`、`:19-24` | 顺序优先 + 异常隔离 |
| `Verse/MentalBreakIntensity.cs` | `:6-16` | 全局分级枚举范例 |
| `Verse/AI/JobQueue.cs` | `:62` `:68` `:113` `:125` | 队列语义（插队/追加/出队） |
| `RimWorld/ThinkNode_ConditionalNeedPercentageAbove.cs` | `:20-32`、`:27-31` | 阈值判定 + `Need_Seeker` 读值差异 |

（路径前缀：`_research/vanilla/Assembly-CSharp/`）

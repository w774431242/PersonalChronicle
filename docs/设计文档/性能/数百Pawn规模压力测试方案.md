# 数百 Pawn 规模压力测试方案

> **性质**：针对「Worker LOD 三档 + 车间吸收/释放 + 工时卡结算」方案的**可执行压力测试规范**（怎么测、测什么、怎么判定、别踩什么坑）。
> **被测对象**：本 Mod 拟议的「车间工位吸收制」——pawn 进入车间后被设备吸收并挂起（零 tick），工时卡解析结算在岗消耗，离开时释放回实体 pawn。
> **前置阅读**：`原版工作系统性能成本分析_2026-09-10.md`（原版工作成本来源）、`全量性能体检报告_2026-09-06.md`（本 Mod 自身热点）。
> **证据等级**：§2 全部代码引用为 `ilspycmd` **反编译原文摘录**；数量为脚本实测；凡估算处标 `【推测】`。
> **本文不含实现代码**。按项目规范（GOV-001 先架构后编码），实现前应另出 REQ/SDD。

---

## 0. 本文定位

| 项 | 内容 |
|---|---|
| **解决** | 把"能装几百个 pawn"从**估算**变成**实测结论** |
| **不解决** | 方案本身的实现（吸收/释放/结算的代码） |
| **产出** | ① 单 pawn 成本 `c` 的实测值；② 三档各自的容量上限；③ 出生点（在哪一档先撞 45ms 刹车）；④ 失败时的诊断路径 |
| **交付形态** | 本文 §6.4 / §9 的表格 + `问题跟踪/待修复问题.md` 登记项 |

---

## 1. 测试目标与判定标准

### 1.1 指标定义

| 指标 | 来源 | 位置 | 说明 |
|---|---|---|---|
| **主指标 `T_tick`** | `TickManager.MeanTickTime` | `Verse.TickManager` | 单 tick 平滑耗时（`rawTickTimeAverage(720)` → `smoothedTickTimeAverage(0.1)`）。**唯一权威判据** |
| **次指标 `TPS`** | `TickManager.TicksThisFrame` 累计 | 同上 | 实际每秒 tick 数。1 速目标 60 |
| 辅助 | 帧时间 / 内存 / 存档体积 | Unity / 文件系统 | 只在主指标达标边缘时看 |

### 1.2 通过阈值

**预算推导**：1 速需 60 tick/s；`TickManagerUpdate` 每帧最多 `TickRateMultiplier × 2 = 2` 个 tick，且帧内累计 `> 45.454544f` ms（=1000/22，`WorstAllowedFPS = 22f`）即刹车。
⇒ **两 tick 合计必须 ≤ 45.45ms（还要留渲染），即 `T_tick` 需 ≤ ~15ms。**

| 等级 | `T_tick`（1 速，稳态） | 判定 |
|---|---|---|
| ✅ 通过 | ≤ 8 ms | 有余量，可上调规模 |
| 🟡 边缘 | 8 ~ 15 ms | 可用但无余量，禁止再加规模 |
| ❌ 失败 | > 15 ms 或 TPS 明显 < 60 | 撞刹车，表现为"游戏变慢" |

### 1.3 判定反例（**必须先读，否则结论必错**）

- ❌ **不要用 FPS 当判据。** 45.45ms 刹车会让 FPS 看起来正常，而游戏时间在慢放。
- ❌ 不要只看"最大规模能不能跑"，要找出**撞刹车的出生档位**——那才是容量上限。
- ❌ 不要在 3 速下判定容量。`tickRateMultiplier × 2` 的上限随速度变化（1 速 2、3 速 6），两种速度下的瓶颈位置不同。

---

## 2. 原版参考（引擎事实 + 可复用探针）

### 2.1 tick 预算模型

```csharp
// Verse.TickManager
public const float WorstAllowedFPS = 22f;
public int TicksThisFrame => ticksThisFrame;
public float MeanTickTime => smoothedTickTimeAverage.GetAverage();

public void TickManagerUpdate()
{
    ticksThisFrame = 0;
    if (Paused) return;
    float curTimePerTick = CurTimePerTick;          // 1 / (60 * TickRateMultiplier)
    ...
    float tickRateMultiplier = TickRateMultiplier;  // 1 速=1, 2 速=3, 3 速=6
    clock.Reset(); clock.Start();
    while (realTimeToTickThrough > 0f && (float)ticksThisFrame < tickRateMultiplier * 2f)
    {
        double t0 = clock.Elapsed.TotalMilliseconds;
        DoSingleTick();
        double t1 = clock.Elapsed.TotalMilliseconds;
        realTimeToTickThrough -= curTimePerTick;
        ticksThisFrame++;
        rawTickTimeAverage.AddValue((float)(t1 - t0));           // SimpleMovingAverage(720)
        smoothedTickTimeAverage.AddValue(rawTickTimeAverage.GetAverage());  // ExponentialMovingAverage(0.1)
        if (Paused || (float)clock.ElapsedMilliseconds > 45.454544f) break;   // ← 刹车
    }
    if (realTimeToTickThrough > 0f) realTimeToTickThrough = 0f;  // ← 追不上就丢弃
}
```

**测压推论**：
- `rawTickTimeAverage` 窗口 **720 tick**，`smoothedTickTimeAverage` 系数 **0.1** ⇒ **冷启动数据必须丢弃**（见 §6.2 预热）。
- `realTimeToTickThrough` 在超预算时被**清零**（丢弃而非积欠）⇒ 观察到的 TPS 会**平台化**在刹车点，正好可以用来定位瓶颈。

### 2.2 tick 分桶与相机降频

```csharp
// Verse.TickList —— Normal 桶间隔 1 ⇒ 全部 spawned pawn 每 tick 都被 DoTick
private int TickInterval => tickType switch { Normal => 1, Rare => 250, Long => 2000, _ => -1 };

// Verse.Thing.DoTick —— Normal 无条件调 Tick()，只有 TickInterval 走降频
if (def.tickerType == TickerType.Normal)
{
    Tick();                                    // ← 无条件，每 tick
    tickDelta++;
    int num = Mathf.Min(Mathf.Max(UpdateRateTicks, MinTickIntervalRate), MaxTickIntervalRate);
    if (tickDelta >= num || GenTicks.IsTickInterval(UpdateRateTickOffset, num)) { TickInterval(tickDelta); tickDelta = 0; }
}

// Verse.GenTicks
public static int GetCameraUpdateRate(Thing thing)
{
    if (!WorldRendererUtility.DrawingMap || Find.CurrentMap == null || thing.MapHeld != Find.CurrentMap) return 15;
    if (!cameraDriver.InViewOf(thing)) return 15;
    return (int)(cameraDriver.CurrentZoom + 1);     // 视口内 = 1 ~ 4
}
```

**测压推论**：
- **相机位置是隐藏自变量**（视口内 1 vs 屏外 15）⇒ **必须锁死相机位置与缩放**，否则同一配置测两次结果能差十几倍。
- `Pawn` 的 `tickerType = Normal`（`Data/Core/Defs/ThingDefs_Races/*.xml` 实测）⇒ 所有 spawned pawn 每 tick 全量 tick，无错峰。

### 2.3 「挂起」的零成本链路（被测方案的核心）

```csharp
// Verse.ISuspendableThingHolder —— 公开接口
public interface ISuspendableThingHolder : IThingHolder { bool IsContentsSuspended { get; } }

// Verse.Thing.Suspended
public virtual bool Suspended
{
    get
    {
        if (Spawned) return false;                                   // 只有非 Spawned 才可能挂起
        if (ParentHolder != null) return ThingOwnerUtility.ContentsSuspended(ParentHolder);
        return false;
    }
}

// Verse.ThingOwnerUtility.ContentsSuspended
while (holder != null)
{
    if (holder is Building_CryptosleepCasket || holder is ISuspendableThingHolder { IsContentsSuspended: true }) return true;
    holder = holder.ParentHolder;
}

// Verse.Thing.DoTick 尾部 —— 挂起的内容不会被 tick
if (Destroyed || !cachedIsHolder || (cachedTickable != null && !cachedTickable.ShouldTickContents)
    || ThingOwnerUtility.ContentsSuspended(cachedHolder)) return;

// Verse.Pawn.Tick()
bool suspended = Suspended;
if (!suspended) { pather.PatherTick(); verbTracker.VerbsTick(); stances.StanceTrackerTick();
                  jobs.JobTrackerTick(); health.HealthTick(); ... }        // ← 整块跳过
if (!suspended) { equipment; abilities; inventory; genes; mutant; ... }    // ← 整块跳过
```

**质证点**：`SpawnSetup` → `Find.TickManager.RegisterAllTickabilityFor(this)`；`DeSpawn` → `DeRegisterAllTickabilityFor(this)`。⇒ 挂起档的成本应当是 **0**（不是"很低"）。**测试必须验证这一点**（见 §5 的 A/B 对照组设计）。

### 2.4 原版对"人多了"的官方答案（对照基线）

```csharp
// RimWorld.Planet.WorldPawns
private HashSet<Pawn> pawnsAlive, pawnsMothballed, pawnsDead;
private const int MothballUpdateInterval = 15000;
private const int TendIntervalTicks = 7500;

public void WorldPawnsTick()
{
    tmpPawnsToTick.AddRange(pawnsAlive);
    for (...) tmpPawnsToTick[i].DoTick();                        // ← 活着的世界 pawn 仍每 tick DoTick
    if (Find.TickManager.TicksGame % 15000 == 0) DoMothballProcessing();
    ... Discard ...
}
private void DoMothballProcessing()
{
    foreach (mothballed) p.TickMothballed(15000);                // ← 一次顶 15000 tick
    foreach (alive) if (ShouldMothball(p)) { pawnsAlive.Remove(p); pawnsMothballed.Add(p); }
}
private bool ShouldMothball(Pawn p)
{
    if (DefPreventingMothball(p) == null && !p.IsCaravanMember()) return !PawnUtility.IsTravelingInTransportPodWorldObject(p);
    return false;
}

// Verse.Pawn
public void TickMothballed(int interval)
{
    if (!Suspended) { ageTracker.AgeTickMothballed(interval); records.RecordsTickMothballed(interval); }
}
```

**测压推论**：原版对"人很多"的立场是 **`pawnsAlive`（逐 tick）要尽量少，其余进 `pawnsMothballed`（1/15000 频率）**。我们的方案（挂起 = 0 频率）比原版更彻底 —— **这是一个可以写进验收报告的对照论据**。

### 2.5 needs 的结算模型（工时卡结算的理论基础）

```csharp
// RimWorld.Need —— 挂起 ⇒ 需求冻结（原版内建）
protected virtual bool IsFrozen
{
    get
    {
        if (pawn.Suspended) return true;                  // ★
        if (def.freezeWhileSleeping && !pawn.Awake()) return true;
        if (def.freezeInMentalState && pawn.InMentalState) return true;
        if (NeedFrozenFromDormanancy()) return true;
        ...
    }
}
public virtual float CurLevel { get => curLevelInt; set => curLevelInt = Mathf.Clamp(value, 0f, MaxLevel); }   // ★ clamp

// RimWorld.Pawn_NeedsTracker —— 150 tick 节拍
public void NeedsTrackerTickInterval(int delta)
{
    if (pawn.IsHashIntervalTick(150, delta))
        for (int i = 0; i < needs.Count; i++) needs[i].NeedInterval();
}

// RimWorld.Need_Food
public const float BaseFoodFallPerTick = 2.6666667E-05f;
public override void NeedInterval()
{
    if (!IsFrozen) CurLevel -= FoodFallPerTick * 150f;
    if (!Starving) lastNonStarvingTick = Find.TickManager.TicksGame;
    if (!IsFrozen || pawn.Deathresting)
    {
        if (Starving) HealthUtility.AdjustSeverity(pawn, HediffDefOf.Malnutrition, MalnutritionSeverityPerInterval);
        else          HealthUtility.AdjustSeverity(pawn, HediffDefOf.Malnutrition, -MalnutritionSeverityPerInterval);
    }
}
private float MalnutritionSeverityPerInterval
    => 0.0011325f * Mathf.Lerp(0.8f, 1.2f, Rand.ValueSeeded(pawn.thingIDNumber ^ 0x26EF7A));   // ★ 种子随机

// RimWorld.Need_Rest —— 按 RestCategory 4 档变速
public float RestFallPerTick => CurCategory switch
{
    Rested => 1.5833333E-05f * RestFallFactor,
    Tired => 1.5833333E-05f * RestFallFactor * 0.7f,
    VeryTired => 1.5833333E-05f * RestFallFactor * 0.3f,
    Exhausted => 1.5833333E-05f * RestFallFactor * 0.6f, _ => 999f,
};

// RimWorld.Need_Joy —— 按 JoyCategory 6 档变速
private float FallPerInterval => CurCategory switch
    { Empty => 0.0015f, VeryLow => 0.0006f, Low => 0.00105f, _ => 0.0015f };

// RimWorld.Building_GrowthVat —— 原版"设备手动推进被吸收 pawn"的范式
selectedPawn.ageTracker.Notify_TickedInGrowthVat(ticks);
float num = BiostarvationDailyOffset / 60000f * HediffDefOf.BioStarvation.maxSeverity;
Hediff h = selectedPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BioStarvation);
if (h != null) h.Severity += num;
```

**测压推论**：
- 挂起 pawn 的需求**不会**被原版推进 ⇒ 结算必须是自研的，且**不能用 `NeedsTrackerTickInterval` 去泵**（`IsFrozen` 短路 + `Need_Rest` 的强制睡眠分支在 despawn pawn 上会出错）。
- 测试必须覆盖 **"结算精度"** 与 **"结算成本"** 两个维度（§5.4）。

### 2.6 可用 / 不可用的探针（★ 含一个真坑）

| 探针 | 可用性 | 用法 |
|---|---|---|
| `TickManager.MeanTickTime` | ✅ 公开 | 主指标 |
| `TickManager.TicksThisFrame` | ✅ 公开 | 累计得 TPS |
| 本项目 `PerfProbe.Start()/End(key, t0)/Tick()` | ✅ | 自定义埋点，见 §4 |
| `Verse.DeepProfiler.Start(label)/End()` | ⚠️ 有条件 | **仅在 `enabled && Prefs.LogVerbose` 为真时生效**（`enabled` 默认 true，靠 `Prefs.LogVerbose` 开关） |
| `Verse.ProfilerBlock.Scope("Tick()")` | ❌ **不可用** | 见下 |
| Unity Profiler | ⚠️ 一般不可用 | RimWorld 正式版非 Development Build |

> **★ 真坑（必须写进报告避免后人重复踩）**：
> `Verse.Thing.DoTick` 里那些 `using (ProfilerBlock.Scope("Tick()"))` / `"TickInterval()"` / `"DoTick()"` 看起来很诱人，但反编译出来是：
> ```csharp
> [StructLayout(LayoutKind.Sequential, Size = 1)]
> public struct ProfilerBlock : IDisposable
> {
>     public ProfilerBlock(string blockName) { }        // ← 空体
>     public void Dispose() { }                          // ← 空体
>     public static ProfilerBlock Scope(string blockName) => new ProfilerBlock(blockName);
> }
> ```
> **在正式版里是彻底的空实现**，既不计时也不输出。别据此设计任何测量方案。

---

## 3. 被测对象与不变量

### 3.1 三档定义（测试的"自变量"）

| 档 | 状态 | 实现要点 | 期望成本 |
|---|---|---|---|
| **A 档** | `Spawned == true`，持有**永久 Job**，原地不动 | `curJob` 恒非 null；自定义 think tree；不进原版工作体系 | 全额 `Pawn.Tick()` |
| **B 档** | `Spawned == false`，在 `ISuspendableThingHolder` 容器内 | `DeSpawnOrDeselect()` + `TryAddOrTransfer()`；容器 `IsContentsSuspended => true` | **期望 0** |
| **C 档** | 不是 Pawn，只是 `WorkerRecord` 数据 | 挂在 `MapComponent` 上；由我们的调度器聚合推进 | **期望 0**（只有我们的聚合遍历） |

### 3.2 七个不变量（任一条被破坏，测试结论即无效）

| # | 不变量 | 为什么 |
|---|---|---|
| I1 | A 档 pawn 的 `curJob` 永不为 null | 否则会触发 `JobGiver_Work` 全量扫描（见前置文档 §4），成本量级完全不同 |
| I2 | A 档 pawn 不移动、不寻路 | 否则引入 `PathFinder` 与区域重建 |
| I3 | B 档容器实现 `ISuspendableThingHolder` 且 `IsContentsSuspended == true` | 否则 `Suspended` 为 false，pawn 仍在被 tick |
| I4 | B 档容器**不在** `TickList` 的 Normal 桶（建议 `Rare`/`Never` 或非 Thing 的 `MapComponent`） | 否则容器自身每 tick `DoTick` |
| I5 | 相机位置与缩放锁定 | 见 §2.2 |
| I6 | 无新增 `WorkGiverDef`（车间指派走显式 `TryTakeOrderedJob`） | 新增 def 会给每个 pawn 的表加一项 |
| I7 | 所有缓存挂 `MapComponent`，无静态可变单例 | 静态状态会跨存档串味，且违反 GOV/AI-004 |

---

## 4. 埋点设计

### 4.1 为什么不能直接测 `Pawn.Tick()`

`Pawn` 不是我们的类型，不加 Harmony 就无法在其入口埋点（项目红线 GOV-007：Harmony 需理由+范围+回滚，且触发 Architecture Review）。
⇒ **单 pawn 成本 `c` 必须用"回归法"反推**：

```
在 I1~I7 全部成立的前提下，把 A 档人数 N 作为唯一自变量，测 T_tick(N)：
      T_tick(N) ≈ T_base + N × c + E
    T_base = 固定世界成本（地图物体、设备、其他系统）
    c      = 单个 A 档 pawn 的每 tick 成本      ← 我们要的值
    E      = 误差（应显著小于 N×c 的跨度）

取样 4~6 个 N（如 0 / 25 / 50 / 100 / 200 / 400），线性回归得斜率 = c。
```

**同理可得**：B 档斜率应 ≈ 0（若显著非 0，说明 I3/I4 被破坏）；C 档斜率应 ≈ 0。

### 4.2 `PerfProbe` 现有 key（源码实测，避免重名）

| key | 位置 |
|---|---|
| `reconcile` | `Data/ChronicleGameComponent.Sampling.cs` |
| `scan.people` | `Domain/ChronicleColonistScanner.cs` |
| `ui.buildCells` / `ui.snapshot` | `Archive/ITab_Pawn_Chronicle.cs` |
| `agri.work` / `agri.growth` / `agri.renovation` | `Automation/CompAutoAgriculture.cs` |

> `PerfProbe` 语义提醒：`Start()` 未启用时返回 `0`，`End(key, 0)` 直接返回（零开销）；每 `ReportIntervalTicks = 3600` 输出一行到 Player.log，前缀 `[PersonalChronicle][Perf]`，输出后统计清零（每行是"本轮"数据）。**key 必须是字面量。**

### 4.3 本次需新增的 key 规划

| key | 埋点位置 | 期望量级 | 说明 |
|---|---|---|---|
| `worker.absorb` | `TryAcceptPawn` 全程 | 单次 ≤ 0.5ms | `DeSpawnOrDeselect` + `TryAddOrTransfer` 很贵，是 LOD 切换的主要成本 |
| `worker.eject` | 离开闸门全程 | 单次 ≤ 0.5ms | `TryDrop` + 补 job |
| `worker.settle` | 工时卡结算（分段积分） | **单次 ≤ 0.05ms** | 必须是 O(1)/O(档数)，**不得**随 elapsed 增长 |
| `worker.pump` | 在岗期间的平滑泵（若有） | 每 250 tick ≤ 2ms | 切片后应远低于此 |
| `worker.lodscan` | LOD 升降档评估 | 每 250 tick ≤ 1ms | 含"哪些车间在视野内"的判定 |
| `worker.schedule` | 排班/工时卡推进 | 每 250 tick ≤ 1ms | C 档聚合推进 |

**验收要求**：`worker.settle` 必须证明"3 天班次"与"1 小时班次"耗时**同量级**（若随时长线性增长，说明写成了逐段循环）。

---

## 5. 场景矩阵（扫参设计）

### 5.1 因子与水平

| 因子 | 水平 | 目的 |
|---|---|---|
| A 档人数 | 0 / 25 / 50 / 100 / 200 / 400 | 求 `c` 与 A 档上限 |
| B 档人数 | 0 / 100 / 500 / 1000 / 2000 | 验证 B 档斜率 ≈ 0 |
| C 档人数 | 0 / 1000 / 5000 | 验证 C 档斜率 ≈ 0 |
| 相机 | 视口内（贴在车间） / 屏外（拉离） | 验证 15 倍档差 |
| 地图物体规模 | 小基地 / 大基地（掉落物/作物/设备满载） | 验证 `listerThings`/`listerBuildings` 的乘性影响 |
| 设备数 | 50 / 200 | 验证"设备反向推高剩余 pawn 成本" |
| 时间速度 | 1 速（判据） / 3 速（参考） | 见 §1.3 |

### 5.2 最小可行矩阵（避免组合爆炸）

用**一次一因子（OFAT）+ 3 组正交确认**：

| 组 | A | B | C | 相机 | 地图 | 设备 | 目的 |
|---|---|---|---|---|---|---|---|
| **G0 基线** | 0 | 0 | 0 | 视口内 | 小 | 0 | 固定成本 `T_base` |
| **G1** | 0→400 | 0 | 0 | 视口内 | 小 | 0 | **求 `c`**（核心） |
| **G2** | 0→400 | 0 | 0 | **屏外** | 小 | 0 | 相机档差 |
| **G3** | 0 | 0→2000 | 0 | 视口内 | 小 | 0 | B 档斜率应 ≈ 0 |
| **G4** | 0 | 0 | 0→5000 | 视口内 | 小 | 0 | C 档斜率应 ≈ 0 |
| **G5** | 50 | 500 | 1000 | 视口内 | 小 | 50 | **混合稳态**（真实形态） |
| **G6** | 50 | 500 | 1000 | 视口内 | **大** | 200 | 真实形态 + 大规模地图/设备 |
| **G7** | 50 | 500 | 1000 | 视口内 | 大 | 200 | **3 速**（参考） |
| **G8** | 0→400 | 0 | 0 | 视口内 | 小 | 0 | **进出抖动**：每人每 250 / 1000 / 5000 tick 进出一轮 |

**G8 是最容易被忽略但最重要的一组**：它验证"LOD 切换成本"是否抵消了挂起收益。判据：
`T_tick(G8@5000) ≪ T_tick(G8@250)`，且 `T_tick(G8@5000) ≈ T_tick(G5)`。

### 5.3 每组配置提示

- 所有配置**必须使用同一份存档副本**（同一 `Data`、同一地图物体统计），否则跨组不可比。
- `N = 0` 的组必须先跑：它是后面所有组的减数。
- 400 个 A 档 pawn 是**预期会失败**的点，用来定位"出生档位"，不要因为失败就中断。

---

## 6. 测量协议

### 6.1 环境锁定清单（逐项打勾，缺一项结论即不可比）

| # | 项 | 做法 |
|---|---|---|
| E1 | 游戏版本 / DLC / Mod 列表 | 记录并冻结；一次会话内不改 |
| E2 | UI 缩放 | 记录 `物理分辨率 / UI缩放`（影响 UI 成本与 `UI点`） |
| E3 | 相机位置 + 缩放 | 固定（建议吸附到同一车间中心 + 同一 `CameraZoomRange`） |
| E4 | 打开的窗口 | 只保留游戏主视图；**关闭**档案 Tab / 总控中心 / 工作 Tab |
| E5 | 自动存档 | 关闭 `Autosaver`；或记录其节拍并剔除该 tick |
| E6 | 时间速度 | 锁定 1 速（判据组）；暂停后再开以保证起始对齐 |
| E7 | `Settings.EnablePerfProbe` | 开启（否则 `PerfProbe.Start()` 返回 0，日志全空） |
| E8 | `Prefs.LogVerbose` | 若要使用 `DeepProfiler`，必须开启 |
| E9 | 事件 | 避免袭击/商队/疫情窗口；必要时用 DevMode 强制 peace |
| E10 | GC / 编辑器 | 关闭一切后台高负载程序 |

### 6.2 预热与采样

| 阶段 | 长度 | 说明 |
|---|---|---|
| **预热** | ≥ **2500 tick** | `rawTickTimeAverage` 窗口 720 + `smoothed` 系数 0.1（约需 10×720）⇒ 丢弃 JIT/贴图/缓存冷启动 |
| 切配置后预热 | ≥ **1500 tick** | 切换配置必然破坏缓存 |
| **采样窗口** | ≥ **18000 tick**（1 速 5 分钟） | 或 `3600 tick × 3` 次重复 |
| 采样方式 | 每 3600 tick 读一次 `MeanTickTime` + `TicksThisFrame` | 用 `PerfProbe` 的 3600 节拍天然对齐 |

### 6.3 统计处理

- 每个配置 **≥ 3 次独立采样**（中间可切回基线再切回，抵消漂移）。
- 报 **中位数**，同时记录 min / max / 标准差。**不要用均值**（一次存档卡顿就能拉偏）。
- 剔除规则（必须记录剔除原因）：自动存档 tick、加载/重载 tick、任何 `Messages` 弹窗密集 tick。
- `c` 用最小二乘拟合 `T_tick(N) = T_base + c·N`；报 `c` 与 `R²`。**`R² < 0.95` 时不得采信该 `c`。**

### 6.4 数据记录表模板（可复制）

| 组 | A | B | C | 相机 | 地图 | 设备 | 速度 | `T_tick` 中位数(ms) | TPS | 备注/剔除 |
|---|---|---|---|---|---|---|---|---|---|---|
| G0 | 0 | 0 | 0 | 视口内 | 小 | 0 | 1× | | | |
| G1 | 25 | 0 | 0 | 视口内 | 小 | 0 | 1× | | | |
| G1 | 50 | 0 | 0 | 视口内 | 小 | 0 | 1× | | | |
| G1 | 100 | 0 | 0 | 视口内 | 小 | 0 | 1× | | | |
| G1 | 200 | 0 | 0 | 视口内 | 小 | 0 | 1× | | | |
| G1 | 400 | 0 | 0 | 视口内 | 小 | 0 | 1× | | | 预期失败 |
| … | | | | | | | | | | |

**回归结果登记**：

| 量 | 值 | R² | 说明 |
|---|---|---|---|
| `T_base` | | | 固定世界成本 |
| `c`（A 档单 pawn/tick） | | | 目标：与 `【推测】5~30μs` 对照 |
| B 档斜率 | | | 应为 ≈ 0 |
| C 档斜率 | | | 应为 ≈ 0 |
| 相机档差倍数 | | | 应接近 15（在 A 档人数足够大时） |

---

## 7. 详细步骤（可照做）

### Step 0 · 前置检查（不通过则停止）
1. 编译 0 错 0 警：`& 'C:\dotnet8\dotnet.exe' build Source\PersonalChronicle.csproj -c Release /p:Platform=x64`
2. 单测通过：`dotnet test Tests\PersonalChronicle.Tests.csproj -c Release`
3. dll 已同步发布区（逐文件 `Get-FileHash` 一致）
4. 记录 E1~E10（§6.1）

### Step 1 · 建立基线存档
1. 用 DevMode 造一张**标准小地图**（例如 75×75），记录：
   - 地图物体统计（`listerThings` 量级、掉落物数、作物格数）
   - 设备数（`listerBuildings.allBuildingsColonist.Count`）
   - 原版殖民者数（应为 0 或固定小值）
2. **存档并复制 3 份**（G1/G2/G3 各用一份，避免污染）。

### Step 2 · 搭建压测装置
1. 在既有 `Source/PersonalChronicle/Archive/DevTestButtons*.cs` 旁新增一个 **Dev-only** 的压测分片（例如 `DevTestButtons.Stress.cs`），**只在 `Prefs.DevMode` 下绘制**：
   - 「生成 N 个 A 档测试 pawn（永久 job、原地）」
   - 「生成 N 个 B 档（吸收进容器）」
   - 「生成 N 个 C 档 WorkerRecord」
   - 「全部清除」
   - 「进出抖动：每 M tick 全员出一轮再进一轮」
2. 每个按钮内部**二次判 `Prefs.DevMode`**，且不写持久状态（对齐已登记的 **LAND §7.1 Dev-Fill 受控偏离**四条约束）。
3. 新增 §4.3 的 `PerfProbe` key 埋点。

> ⚠️ 压测装置本身必须满足 §3.2 的 I1~I7，否则测的是别的东西。

### Step 3 · 跑 G0 基线
1. 打开 `EnablePerfProbe`。
2. 1 速跑 ≥ 2500 tick 预热，再 ≥ 18000 tick 采样。
3. 记 3 次 `T_tick` 中位数 → 填表。
4. `grep "[PersonalChronicle][Perf]" Player.log` 确认埋点在输出（若为空，先查 E7）。

### Step 4 · 跑 G1（求 `c`）——核心
1. 逐档生成 A 档 pawn：25 → 50 → 100 → 200 → 400。
2. **每次生成后必须重新预热 1500 tick**（生成本身会破坏缓存）。
3. 采样、填表。
4. 线性回归求 `c` 与 `R²`；`R² < 0.95` 时回查 §3.2 的不变量与 §6.1 的锁定项。
5. 记录**第一次出现 `T_tick > 15ms` 的 N** = A 档容量上限。

### Step 5 · 跑 G2（相机档差）
1. 保持 A 档人数不变（建议用 G1 里 `T_tick ≈ 8ms` 的那个 N）。
2. 相机：① 吸附到车间（视口内）② 拉到地图另一角（屏外）。
3. 各采样 3 次，比较倍数。**预期 ≈ 15 倍**（`GetCameraUpdateRate` 1 vs 15）。
4. 若倍数远小于 15，说明 `TickInterval` 不是当前瓶颈（`Tick()` 才是）→ 这是一个**重要结论**，要写进报告。

### Step 6 · 跑 G3 / G4（B、C 档斜率）
1. B 档 0 → 2000，C 档 0 → 5000，分别采样。
2. **判据**：斜率显著非 0 时，按 §8.2 的顺序排查（最可能是 I3/I4）。
3. 这一组是**方案成立与否的核心证据**——B/C 档斜率必须 ≈ 0。

### Step 7 · 跑 G5 / G6 / G7（真实形态）
1. G5：`A=50, B=500, C=1000, 设备=50` → 稳态基线。
2. G6：同配置 + 大基地 + 设备 200 → 验证"设备反向成本"。
3. G7：G6 配置切 3 速 → 仅作参考，**不作为判据**。
4. 与 G0/G1 对照，算出"三档方案相比纯 A 档"的收益倍数。

### Step 8 · 跑 G8（进出抖动）
1. 固定 `A=50, B=500`，让 B 档每 M tick 全出一轮再进一轮，M ∈ {250, 1000, 5000}。
2. 观察 `worker.absorb` / `worker.eject` 的单次耗时与总数。
3. **判据**：`T_tick(G8@5000) ≈ T_tick(G5)`；若 250 档明显劣化 → LOD 切换必须节流到 ≥ 2500 tick，并把这个下限写进实现规范。

### Step 9 · 结算精度与成本专项
1. **成本**：对同一名 B 档 pawn，分别结算"1 小时班次"与"3 天班次"，比较 `worker.settle` 耗时 ⇒ 必须同量级。
2. **精度**：对照组——让一个 **A 档** pawn 原地待命同样时长（走原版 `NeedInterval`），与 B 档"结算法"结果比较 `Food` / `Rest` 差值。
   - 目标：在**钳制在安全下限**的前提下（见前置文档 N5），出闸时档位（`HungerCategory` / `RestCategory`）判定与对照组一致。
3. **回归**：确认未调用 `needs.NeedsTrackerTickInterval()`（见 §8.3 致命项）。

### Step 10 · 汇总与登记
1. 填 §6.4 的回归结果表与判定（§9.1）。
2. 失败的配置 → 登记 `问题跟踪/待修复问题.md`（FIX-xxx）。
3. 通过的容量数字 → 写入实现规范（作为 `GATE-PERF` 的证据）。

---

## 8. 避坑指南

### 8.1 测量污染类（会让结论完全错）

| # | 坑 | 症状 | 正确做法 |
|---|---|---|---|
| P1 | 用 FPS 当判据 | "FPS 60 但游戏像慢放" | 只看 `MeanTickTime` + TPS（§1.1） |
| P2 | 不锁相机 | 同一配置两次结果差十几倍 | 锁位置 + 缩放（E3） |
| P3 | 冷启动数据未丢弃 | 前几分钟数据明显偏高 | 预热 ≥ 2500 tick（§6.2） |
| P4 | 切配置不重新预热 | 出现"人数越少反而越慢" | 每次切换后预热 ≥ 1500 tick |
| P5 | 自动存档混入 | 周期性尖峰 | 关闭 `Autosaver` 或剔除（E5） |
| P6 | 忘记 `Settings.EnablePerfProbe` | Player.log 里**一条 Perf 都没有** | 开工先 `grep "[PersonalChronicle][Perf]"` 验证（E7） |
| P7 | 忘记 `Prefs.LogVerbose` | `DeepProfiler` 静默无效 | E8 |
| P8 | 用均值统计 | 一次卡顿拉偏 | 用中位数（§6.3） |
| P9 | 用 3 速判定容量 | 与 1 速结论矛盾 | 判据固定 1 速（§1.3） |
| P10 | 开着档案 Tab / 总控中心测 | 测到的是 UI 成本（U1~U10） | 关闭全部窗口（E4） |
| P11 | 依赖 `ProfilerBlock` | 什么都测不到 | 它是空实现（§2.6） |
| P12 | UI 缩放变了 | 结果不可比 | 记录并冻结（E2） |
| P13 | 不同存档跨组比较 | 自变量不纯 | 复制同一份存档（§5.3） |

### 8.2 被测对象失真类（测的不是你以为的东西）

| # | 坑 | 后果 | 依据/正确做法 |
|---|---|---|---|
| Q1 | A 档 pawn 的 `curJob` 变 null | 触发 `JobGiver_Work` 全量扫描，成本量级完全不同 | I1；前置文档 §4 |
| Q2 | A 档 pawn 会移动 | 引入 `PathFinder` + 区域重建 | I2 |
| Q3 | B 档容器没实现 `ISuspendableThingHolder`，或 `IsContentsSuspended == false` | `Suspended == false` ⇒ pawn 仍在被 tick，测不到 0 | I3；§2.3 |
| Q4 | B 档容器是 `TickerType.Normal` 的 Thing | 容器自身每 tick `DoTick` | I4 |
| Q5 | 忘了 `DeSpawnOrDeselect`，直接 `TryAddOrTransfer` | `Spawned` 与 `holdingOwner` 矛盾态 | §2.3 |
| Q6 | 新增了 `WorkGiverDef` 派发进车间 | 每个 pawn 的 `WorkGiversInOrderNormal` 多一项（乘法因子） | I6；前置文档 §4.2 |
| Q7 | 进出车间不节流 | spawn/despawn 抖动，白省 | G8 |
| Q8 | 用 `needs.NeedsTrackerTickInterval()` 泵挂起 pawn | **① 无效（`IsFrozen` 短路）② `Need_Rest` 的强制睡眠分支在 despawn pawn 上出错** | §2.5；前置文档 N1 |
| Q9 | 结算参数在岗期间被改动却用进场快照 | 整段算错（例如受伤改了 `RestFallFactor`） | 前置文档 N9：变动时立即结算并重置游标 |
| Q10 | 时间轴双记/漏记 | 需求数值系统性偏差 | 前置文档 N6：`[enter, exit)` 与结算游标对齐 |
| Q11 | 只测"稳态"，不测"进出瞬态" | 漏掉 LOD 切换这个主要成本项 | 必跑 G8 |
| Q12 | 静态可变单例存 LOD 状态 | 跨存档串味、无法重置 | I7；AI-004 |

### 8.3 引擎陷阱类（会崩、会坏档、会丢数据）

| # | 坑 | 后果 | 正确做法 |
|---|---|---|---|
| R1 | 建筑被拆/被毁时不弹内容 | **工人随建筑一起消失** | 照抄 `Building_Enterable.DeSpawn` 的 `innerContainer.TryDropAll(base.Position, map, ThingPlaceMode.Near)` |
| R2 | 释放落点非法（墙内/不可站立） | `TryDrop` 失败或人卡墙里 | 落到 `InteractionCell` 附近，用 `ThingPlaceMode.Near`，并检查落点 |
| R3 | 释放后不给 job | 下一拍掉回主树 → 一次全量扫描 | 前置文档 N7 |
| R4 | 车间内着火/袭击不弹出 | `Suspended` 冻结了崩溃/着火逻辑，**人会静静烧死** | 设备着火/被毁 → 强制 Eject 全部内容 |
| R5 | 把被吸收的 pawn 交给 `Find.WorldPawns` | `WorldPawnGC` 会把它回收掉 | 只由我们的容器持有强引用 |
| R6 | 容器内容不 `Scribe_Deep` | **存档丢人** | 照抄 `Building_Enterable.ExposeData` 的 `Scribe_Deep.Look(ref innerContainer, "innerContainer", this)` |
| R7 | 投影 Pawn 被 `LookMode.Reference` 引用 | 一旦重建/销毁即悬空 | 只引用 `WorkerRecord` 的稳定 ID |
| R8 | 新设备用 `TickerType.Normal` | 每 tick 无条件 `Tick()`（200 台 ≈ 200 个 pawn 的壳） | 用 `Rare`(250)/`Never`；需更细响应则 `Normal` + `CompTick()` 留空 + 逻辑放 `CompTickInterval` |
| R9 | 设备把 `listerBuildings` / `listerThings` 推大 | **剩余 pawn 的原版工作扫描变贵**（`WorkGiver_Grower` 遍历 `allBuildingsColonist`、`WorkGiver_DoBill.ShouldSkip` 遍历 `ThingsInGroup(PotentialBillGiver)`） | G6 专门验证；聚合设备建模 |
| R10 | `Need.CurLevel` 直接扣成负值 | 被 `Mathf.Clamp(value, 0f, MaxLevel)` 截断，"饿了多久"信息永久丢失 | 前置文档 N3/N5 |
| R11 | 结算越过分档阈值到 0 | 需复现 `Malnutrition` 的 `thingIDNumber` 种子随机系数、`ticksAtZero`（private）、MTB 强制睡眠 — **做不到** | 前置文档 N5：钳制在安全下限 |
| R12 | 解析积分对 `Mood` 做 | mood 由 thoughts 求和导出，无时间衰减语义 | 前置文档 N8：只算 Food / Rest（+ 可选 Joy） |

---

## 9. 判定与登记

### 9.1 判定流程

```
1) G3 / G4 斜率 ≈ 0 ？
     ├─ 否 → 方案前提不成立，先修 I3/I4（回到 §8.2 Q3/Q4），本轮结论作废
     └─ 是 → 继续
2) G1 的 c 与 A 档容量上限（首次 > 15ms 的 N）
3) G8：5000 tick 抖动的 T_tick ≈ G5 ？
     ├─ 否 → LOD 切换窗口需上调（记录下限）
     └─ 是 → 继续
4) G6：设备 50→200 时 T_tick 增量可接受？
     ├─ 否 → 引入聚合设备建模，重回 G6
     └─ 是 → 给出最终容量结论
5) 输出：① c；② A 档上限；③ B 档上限（若无上限则写"受内存/存档限制"）；④ 三档配比建议
```

### 9.2 失败诊断决策树

```
T_tick 超预算
├─ B 档斜率非 0 ............ → §8.2 Q3/Q4（容器未挂起）
├─ A 档斜率远大于预期 ...... → §8.2 Q1/Q2（有工作扫描或寻路）
├─ G8 明显劣于 G5 .......... → §8.2 Q7（进出抖动）
├─ G6 明显劣于 G5 .......... → §8.3 R9（设备规模反噬）
├─ 相机档差远小于 15 ....... → 瓶颈在 Tick() 而非 TickInterval（这是结论，不是 bug）
├─ 采样方差极大 ............ → §8.1 P5/P10/P13（污染源）
└─ Perf 日志为空 ........... → §8.1 P6/P7（探针未开）
```

### 9.3 结果登记位置与 Gate 对应

| 项 | 登记处 | Gate |
|---|---|---|
| `c` / 容量上限 | 本文 §6.4 表格 | `GATE-PERF`（"至少一份可复核指标"） |
| 失败配置 | `问题跟踪/待修复问题.md`（FIX-xxx） | — |
| 新增压测设施 | 登记为 Dev 分片受控偏离（LAND §7.1） | `GATE-ARCH` / `GATE-PERF` |
| 若引入 Harmony 埋点 | 必须登记 EXC + Architecture Review | `GATE-ARCH`（GOV-007） |
| 实现规范里的容量数字 | 功能模块设计文档 | `GATE-PERF` |

---

## 10. 附录

### 10.1 原版常量速查（本文用到的）

| 常量 | 值 | 出处 |
|---|---|---|
| `TickManager.WorstAllowedFPS` | 22 | `Verse.TickManager` |
| 每帧刹车阈值 | `clock.ElapsedMilliseconds > 45.454544f` | `TickManagerUpdate` |
| 每帧 tick 上限 | `TickRateMultiplier × 2` | 同上 |
| `rawTickTimeAverage` 窗口 | 720 | 同上 |
| `smoothedTickTimeAverage` 系数 | 0.1 | 同上 |
| `GenTicks.TicksPerRealSecond` | 60 | `Verse.GenTicks` |
| `Thing.MinTickIntervalRate` / `MaxTickIntervalRate` | 1 / 15 | `Verse.Thing` |
| `GenTicks.GetCameraUpdateRate` | 视口内 `CurrentZoom+1`（1~4）/ 否则 15 | 同上 |
| `TickList` 分桶间隔 | Normal 1 / Rare 250 / Long 2000 | `Verse.TickList` |
| needs 节拍 | 150 | `Pawn_NeedsTracker` |
| `Need_Food.BaseFoodFallPerTick` | 2.6666667E-05f | `RimWorld.Need_Food` |
| `Need_Rest.BaseRestFallPerTick` | 1.5833333E-05f | `RimWorld.Need_Rest` |
| `Need_Food.MalnutritionSeverityPerInterval` | `0.0011325f × Lerp(0.8,1.2, ValueSeeded(thingIDNumber ^ 0x26EF7A))` | 同上 |
| `WorldPawns.MothballUpdateInterval` | 15000 | `RimWorld.Planet.WorldPawns` |
| `PerfProbe.ReportIntervalTicks` | 3600 | `Source/PersonalChronicle/PerfProbe.cs` |

### 10.2 反编译复现命令

```powershell
$dll = 'E:\SteamLibrary\steamapps\common\RimWorld\RimWorldWin64_Data\Managed\Assembly-CSharp.dll'
ilspycmd $dll -t Verse.TickManager -o <out>
# 注意命名空间（写错会产出 0 字节文件、不报错）：
#   Verse.TickManager / Verse.TickList / Verse.Thing / Verse.Pawn / Verse.GenTicks
#   Verse.DeepProfiler / Verse.ProfilerBlock（空实现）/ Verse.ISuspendableThingHolder / Verse.IThingHolder
#   Verse.ThingOwnerUtility / Verse.Pawn_AgeTracker / Verse.PawnGenerator / Verse.AI.Job
#   RimWorld.Need / Need_Food / Need_Rest / Need_Joy / Need_Seeker / Pawn_NeedsTracker
#   RimWorld.Building_Enterable / Building_GrowthVat / JobDriver_EnterBuilding
#   RimWorld.Pawn_WorkSettings / JobGiver_Work / WorkGiver_Scanner
#   RimWorld.Planet.WorldPawns / RimWorld.CompHasPawnSources
```

**踩坑**：类型名写错时 `ilspycmd` 静默产出 0 字节文件；先用 `ilspycmd $dll -l c | Select-String <关键词>` 确认全限定名。

### 10.3 已核实 / 未核实

- **已核实（反编译原文）**：§2 全部代码引用、§10.1 全部常量、`Pawn.tickerType = Normal`（XML 实测）。
- **未核实（需实测或另查）**：`c` 的真实值（本文只给回归方法）；`【推测】`5~30μs 的数量级；`RegionAndRoomUpdater` 的实际重建频率；Unity Profiler 在 RimWorld 正式版的可用性（按"非 Development Build"判定为一般不可用，未实测）。
- **已确认**：探针开关已可用 —— `ChronicleSettings.EnablePerfProbe`（`Scribe_Values.Look(ref EnablePerfProbe, "enablePerfProbe", false)`）+ 设置页 `listing.CheckboxLabeled("PersonalChronicle.Settings.EnablePerfProbe"...)`（`Source/PersonalChronicle/ChronicleSettings.cs:74`）。测试者无需改代码即可开启。
- **注意**：`PerfProbe` 的 key 清单（§4.2）为**当前源码实测**；若在测试前新增了埋点，须重新核对本表，避免 key 重名或遗漏。

### 10.4 变更日志

| 日期 | 变更 |
|---|---|
| 2026-09-10 | 初版。基于 1.6 `Assembly-CSharp.dll` 反编译实证 + 本项目 PerfProbe/DevTestButtons 现状，制定三档 LOD 方案的压力测试规范。 |

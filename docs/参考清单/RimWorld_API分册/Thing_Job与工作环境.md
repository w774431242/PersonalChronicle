# Thing、Job 与工作环境

> 来源：原总库 §12、§26、§27。

## 12. Thing 系统：最重要的运行时对象

### 12.1 Thing

`Thing` 是 RimWorld 实际游戏对象的核心基类之一。

它通常关联：

```text
ThingID
def
Map
Position
Rotation
Faction
Spawned
Destroyed
HitPoints
StackCount
ThingIDNumber
```

以及：

```text
ThingComp
ThingOwner
Graphics
Overlay
Faction
Ownership
```

### 12.2 ThingWithComps

大量原版 Thing 使用：

```csharp
ThingWithComps
```

它允许通过组件模式动态增加功能。

典型 API：

```csharp
GetComp<T>()
TryGetComp<T>()
AllComps
```

## 26. Job / Work 系统

### 26.1 Job

核心对象：

```text
Job
JobDef
JobDriver
Toil
Toils
JobCondition
```

常见 Job 字段：

```text
def
targetA
targetB
targetC
count
countQueue
verbToUse
playerForced
expiryInterval
loadID
```

### 26.2 JobDriver

典型生命周期：

```text
TryMakePreToilReservations()
MakeNewToils()
ExposeData()
Cleanup()
EndJobWith()
```

典型：

```csharp
protected override IEnumerable<Toil> MakeNewToils()
{
    ...
}
```

## 27. WorkGiver / 工作系统

核心：

```text
WorkGiver
WorkGiver_Scanner
WorkTypeDef
WorkGiverDef
Pawn_WorkSettings
```

典型：

```csharp
public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
```

## 实操骨架：自定义工作 Job（2026-09-05 按 1.6 补全）

> 背景：官方 Wiki《Example Mending Job》页面**尚未发布**（2026-09-05 核验为空页）。以下为通用骨架，字段清单非穷举，**以目标 Build 程序集复核为准**；完整范本可反编译原版 `JobDriver_Repair`、`WorkGiver_FixBrokenDownBuilding`。

### ① JobDef（XML）

```xml
<JobDef>
  <defName>MyMod_Mend</defName>
  <driverClass>MyNamespace.JobDriver_Mend</driverClass>
  <reportString>repairing TargetA.</reportString>   <!-- TargetA/TargetB/TargetC 占位符 -->
  <workAmount>1600</workAmount>                     <!-- 需要接 workgiver 扫描的 Job 才有意义 -->
  <workType>Construction</workType>                 <!-- 归入哪个工作类型（WorkTypeDef） -->
  <casualInterruptible>true</casualInterruptible>
</JobDef>
```

常用 JobDef 字段（非完整清单）：`driverClass` / `reportString` / `workAmount` / `workType` / `requiredCapacities`（能力要求列表）/ `casualInterruptible` / `alwaysStartableInDebug` / `collideWithPawns` / `checkOverrideOnExpiry` / `allowOpportunisticPrefix` / `joyKind`。

### ② WorkGiverDef（XML，把 Job 接入工作系统）

```xml
<WorkGiverDef>
  <defName>MyMod_DoMendJobs</defName>
  <label>mend apparel</label>
  <verb>mend</verb>
  <gerund>mending</gerund>
  <workType>Construction</workType>          <!-- 必须与 JobDef.workType 一致才能被分派 -->
  <giverClass>MyNamespace.WorkGiver_Mend</giverClass>
  <priorityInType>90</priorityInType>
  <scanThings>true</scanThings>
  <scanCells>false</scanCells>
  <requiredCapacities><li>Manipulation</li></requiredCapacities>
  <canBeDoneWhileDrafted>false</canBeDoneWhileDrafted>
</WorkGiverDef>
```

链路契约：**WorkGiverDef.workType == JobDef.workType**，且 pawn 的`WorkSettings` 启用了该 WorkType 才会被扫描；扫描顺序按 `priorityInType`。

### ③ JobDriver（C#）

```csharp
public class JobDriver_Mend : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) =>
        pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOnDestroyedOrNull(TargetIndex.A);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
            .FailOn(() => !pawn.CanReserve(TargetThingA));

        Toil work = ToilMaker.MakeToil("MakeNewToils");   // 1.5+ 引入的 Toil 工厂
        work.initAction = () => { work.actor.pather.StopDead(); };
        work.tickAction = () =>
        {
            Pawn actor = work.actor;
            // 工作量消耗走 JobDriver/Toil 的 workLeft 惯例（JobDriver_Repair 为范本）
        };
        work.defaultCompleteMode = ToilCompleteMode.Delay; // 视需求选 Delay/Opportunistic 等
        yield return work;
    }
}
```

> ⚠️ `workLeft` 消耗与 `defaultCompleteMode` 的组合是版本敏感写法，编码前反编译目标 Build 的 `JobDriver_Repair` 确认；不要照抄旧教程的 `new Toil()`（1.5+ 起逐步由 `ToilMaker.MakeToil` 取代，但旧构造是否仍可用以程序集为准）。

### ④ Toil / JobDriver 助手速查（原版静态类，均为真实存在）

```text
FailOn / FailOnIncapable / FailOnDestroyedOrNull / FailOnDespawnedNullOrForbidden /
FailOnCannotTouch / FailOnThingMissingDesignation        ← this.FailOn*(TargetIndex) 扩展
Toils_Goto.GotoThing / GotoCell                          ← 走位
Toils_Reserve.Reserve / Release                          ← 预定（必做，否则撞车）
Toils_Haul / Toils_Recipe / Toils_Construct / Toils_JobFail / Toils_Jump(JumpIf/JumpIfHaveTargetInCarrier)
```

要点：**预订失败即整个 Job 失败**（`TryMakePreToilReservations` 返回 false 的原因通常是 Reserve 冲突）；`Cleanup(INITIAL/... )` 在异常退出时兜底。

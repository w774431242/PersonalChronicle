# 组件化架构：Comp 与 Component

> 来源：原总库 §13、§14、§16、§42、§96。

## 13. ThingComp API

ThingComp 是推荐的功能扩展模式之一。

常见覆写方法：

```text
Initialize(CompProperties props)
PostExposeData()
CompTick()
CompTickRare()
CompTickLong()
CompGetGizmosExtra()
CompInspectStringExtra()
CompFloatMenuOptions(Pawn selPawn)
PostSpawnSetup(bool respawningAfterLoad)
PostDeSpawn(Map map)
PostDestroy(DestroyMode mode, Map previousMap)
PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
Notify_CompStateChanged()
Notify_Killed(Map prevMap, DamageInfo? dinfo)
PostIngested(Pawn ingester)
PostPostGeneratedForTrader(...)
AllowStackWith(Thing other)
PreAbsorbStack(Thing otherStack, int count)
PostSplitOff(Thing piece)
TransformLabel(string label)
Notify_SignalReceived(Signal signal)
```

RimWorld Wiki 对当前 ThingComp 教程列出了上述大量生命周期/交互入口。来源：
<https://rimworldwiki.com/wiki/Modding_Tutorials/ThingComp>

## 14. CompProperties

组件通常由 `CompProperties` Def 数据驱动。

典型结构：

```csharp
public class CompProperties_MyComp : CompProperties
{
    public float value;

    public CompProperties_MyComp()
    {
        compClass = typeof(CompMyComp);
    }
}
```

XML：

```xml
<comps>
    <li Class="MyNamespace.CompProperties_MyComp">
        <value>10</value>
    </li>
</comps>
```

## 16. HediffComp

用于扩展 Hediff 行为。

典型生命周期方法族：

```text
CompPostMake
CompPostTick
CompPostTickInterval
CompPostInjuryHeal
CompTended
CompPostMerged
CompPostRemoved
CompExposeData
CompGetGizmosExtra
CompDebugString
```

具体方法必须以目标版本程序集为准。

## 42. GameComponent / WorldComponent / MapComponent

这是保存全局状态非常重要的扩展方式。

### GameComponent

一个 Game 实例通常只有一份。

```csharp
public class MyGameComponent : GameComponent
{
    public MyGameComponent(Game game) : base() { }

    public override void ExposeData()
    {
        ...
    }
}
```

典型：

```csharp
Current.Game.GetComponent<MyGameComponent>()
```

### WorldComponent

每个 World 一份。

```csharp
Find.World.GetComponent<MyWorldComponent>()
```

### MapComponent

每张 Map 一份。

```csharp
map.GetComponent<MyMapComponent>()
```

常见生命周期：

```text
FinalizeInit
ExposeData
MapGenerated
MapRemoved
ComponentTick
ComponentUpdate
ComponentOnGUI
```

参考：
<https://rimworldwiki.com/wiki/Modding_Tutorials/GameComponent>

## 96. 官方支持的组件化架构

RimWorld 的组件体系是大型 Mod 最重要的低耦合扩展路线之一。

### 96.1 ThingComp

作用域：单个 Thing。

适用：

- 武器能力
- 建筑行为
- 物品状态
- 可充能行为
- 主动/被动效果

### 96.2 HediffComp

作用域：单个 Hediff。

适用：

- 疾病
- 状态效果
- 部件
- 伤口
- 增益/减益

### 96.3 MapComponent

作用域：Map。

适用于：

- 地图级管理器
- 全图统计
- 多实体协调器
- 地图缓存

### 96.4 WorldComponent

作用域：World。

适用于：

- 世界级数据
- 世界事件
- 多地图共享数据

### 96.5 GameComponent

作用域：Game。

适用于：

- 当前存档全局状态
- 游戏级系统
- 跨地图数据

### 96.6 WorldObjectComp

作用域：WorldObject。

### 选择原则

```text
单个 Thing       → ThingComp
单个 Hediff      → HediffComp
单个 WorldObject → WorldObjectComp
整张 Map         → MapComponent
整个 World       → WorldComponent
当前 Game        → GameComponent
```

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Custom_Comp_Classes
https://rimworldwiki.com/wiki/Modding_Tutorials/GameComponent

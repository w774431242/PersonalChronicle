# RimWorld 1.6 Modder 公开开发 API、变量、Def、组件与开发资料总库

> **文档版本：v1.1**  
> **目标版本：RimWorld 1.6 系列（当前资料基准：1.6.4850 / 2026-06-08 公布的 1.6 更新）**  
> **语言：简体中文**  
> **用途：Mod 开发、AI 辅助编码、API 检索、架构设计、兼容性分析、源码阅读、XML/Def 查询、官方开发能力索引**

---

## 0. 文档声明：什么才叫“RimWorld API”

RimWorld **没有一个由 Ludeon 对外维护、版本独立、覆盖所有类/方法/字段的正式 Mod SDK/API Reference**。RimWorld Wiki 明确指出，RimWorld 没有 formal modding API，大量 Modding 信息来自社区维护；官方论坛也长期采用“XML + 游戏源码/程序集 + 反编译”的开发方式。 

因此本库将 RimWorld Modder 可用开发面拆为 6 层：

| 层级 | 名称 | 官方性 | 典型内容 |
|---|---|---:|---|
| L0 | 官方 Mod 机制 | 官方 | Mod 文件结构、About.xml、Defs、Patches、Languages、Assemblies |
| L1 | 官方随游戏提供的参考源码 | 官方 | `Source/` 中可读源码、示例与结构 |
| L2 | 官方运行程序集 | 官方产品内容，但不是“SDK” | `Assembly-CSharp.dll`、Verse、RimWorld 等实际运行类型 |
| L3 | 官方/社区 Modding Wiki | 社区维护 | XML、C#、Harmony、Comp、Component、保存、UI 等 |
| L4 | 社区反编译/API索引库 | 非官方 | 类、字段、方法、属性、调用链、源码镜像 |
| L5 | 第三方 Mod API | 非官方 | Harmony、HugsLib、Vanilla Expanded Framework、ModSettings 等 |

**核心原则：**

1. **不要把 L4/L5 冒充成 L0/L1。**
2. **不要把反编译出来的类字段直接视为稳定 API。** 私有字段、内部实现、命名、方法签名可能随版本改变。
3. **跨版本开发必须以目标版本实际程序集为最终权威。**
4. **XML 可用字段的最终权威不是 Wiki，而是目标版本 Core/DLC Def 实例 + Def 类字段结构。**
5. **“全量 API”只有在给定具体游戏 Build 后，对该 Build 的程序集逐类型枚举才能真正做到。** 本文因此同时提供“全量资料索引”和“全成员抽取标准”。

---

# 1. 当前版本基准

## 1.1 1.6 发布时间线

- 2025-06：Ludeon 宣布 Odyssey 与 Update 1.6，并明确为 Modder 提供 **1.6 Modder Primer**。
- 2025-07-11：Odyssey 与 Update 1.6 正式发布。
- 2026-06-08：Ludeon 发布 `1.6.4850`。
- 本文以 **1.6 系列**为目标，不将 1.5、1.4 的旧 API 直接标记为 1.6 API。

官方 1.6 发布材料说明，1.6 包含性能重构、地图生成、UI、建筑、飞行动物、以及 Odyssey 配套系统等大量底层变化，因此旧版 Mod 代码不能默认视为 1.6 稳定接口。

来源：
- Ludeon：Announcing Odyssey and update 1.6
- Ludeon：Update 1.6.4850 released
- Steam RimWorld Announcements

---

# 2. 官方与半官方开发资料总索引

## 2.1 Ludeon 官方

### 官方网站

- Ludeon Studios：<https://ludeon.com/>
- RimWorld：<https://rimworldgame.com/>
- RimWorld 官方论坛：<https://ludeon.com/forums/>

### 官方开发相关入口

- 官方 Mods/Help 版块：
  <https://ludeon.com/forums/index.php?board=14.0>
- 官方 Mods 版块：
  <https://ludeon.com/forums/index.php?board=12.0>
- 1.6 + Odyssey 发布说明：
  <https://ludeon.com/blog/2025/06/announcing-odyssey-and-update-1-6/>
- 1.6.4850 更新：
  <https://ludeon.com/blog/2026/06/update-1-6-4850-released/>

### 官方论坛关键 Modding 资料

#### [Tutorial] How to Make a RimWorld Mod, Step by Step

这是 Ludeon 官方论坛上最经典的 Mod 开发入门资料之一，覆盖：

- XML Def
- ThingDef
- C# DLL
- `using RimWorld;`
- `using Verse;`
- 自定义 Def 类
- Project 编译
- Assemblies 部署
- XML → C# 对接

参考：
<https://ludeon.com/forums/index.php?topic=33219.0>

#### XML Auto-Documentation

由社区维护者在 Ludeon 论坛发布，用于扫描 Core XML 使用方式，提供：

- XML 标签
- 父子结构
- 参数使用方式
- Core 使用示例
- Def 结构关系

参考：
<https://ludeon.com/forums/index.php?topic=21440.0>

#### Updated RimWorld XML Auto-Documentation

Epicguru 维护的更新版本，支持：

- Core Def
- DLC Def
- 自动生成 XML 文档
- 本地加入自定义 Mod Def
- 从 Def 文档跳转 XML

参考：
<https://ludeon.com/forums/index.php?topic=55764.0>

---

# 3. RimWorld Wiki Modding 公开开发库

> RimWorld Wiki 是当前最重要的 Modder 社区知识库之一，但**不是官方 SDK**。

总入口：
<https://rimworldwiki.com/wiki/Modding_Tutorials>

Wiki 当前 Modding Hub 列出的核心主题包括：

- Writing Custom Code
- Linking XML and C#
- Harmony
- Adding fields and methods to classes
- Mod settings
- Def Mod Extensions
- Custom Comp Classes
- ThingComp
- Game/World/Map Components
- Def Classes
- Harmony compatibility patches
- TweakValues
- ExposeData
- Useful classes
- Grammar Resolver
- Job
- Config Errors
- Debug Actions
- Linux modding

参考：
<https://rimworldwiki.com/wiki/Modding_Tutorials>

---

# 4. RimWorld Mod 目录与运行时加载接口

标准结构：

```text
MyMod/
├─ About/
│  ├─ About.xml
│  ├─ Preview.png
│  ├─ ModIcon.png
│  └─ PublishedFileId.txt
├─ Assemblies/
│  └─ MyMod.dll
├─ Defs/
│  ├─ ThingDefs/
│  ├─ RecipeDefs/
│  ├─ HediffDefs/
│  └─ ...
├─ Patches/
│  └─ Patches.xml
├─ Languages/
│  ├─ English/
│  └─ ChineseSimplified/
├─ Textures/
├─ Sounds/
├─ LoadFolders.xml
└─ About/
```

## 4.1 About.xml 核心字段

典型字段：

```xml
<ModMetaData>
    <name>My Mod</name>
    <author>Author</author>
    <packageId>Author.MyMod</packageId>
    <supportedVersions>
        <li>1.6</li>
    </supportedVersions>
    <description>...</description>
    <loadAfter>
        <li>Some.Other.Mod</li>
    </loadAfter>
</ModMetaData>
```

### 常用关系字段

| 字段 | 作用 |
|---|---|
| `name` | Mod 显示名 |
| `author` | 作者 |
| `packageId` | Mod 唯一包标识 |
| `supportedVersions` | 支持版本 |
| `description` | 描述 |
| `modDependencies` | 强依赖 |
| `loadAfter` | 载入顺序约束 |
| `loadBefore` | 载入顺序约束 |
| `incompatibleWith` | 冲突声明 |
| `packageId` | Mod 间引用核心身份 |

目录结构参考：
<https://rimworldwiki.com/wiki/Modding_Tutorials/Folder_structure>

---

# 5. 官方核心运行程序集：真正的“底层 API 面”

RimWorld 的 Mod C# 开发并不是针对某个独立 SDK，而是针对游戏运行时程序集进行引用。

## 5.1 核心程序集分类

目标版本中重点关注：

```text
Assembly-CSharp.dll
UnityEngine.*.dll
0Harmony.dll / Harmony相关程序集（随运行环境/加载器而异）
```

### Assembly-CSharp

它是 RimWorld 游戏主要 C# 逻辑集合，通常包含：

```text
Verse
RimWorld
RimWorld.Planet
Verse.AI
Verse.AI.Group
Verse.Grammar
Verse.Sound
Verse.Noise
Verse.Profile
```

以及更多子命名空间。

### Verse

核心底层框架，包含：

- Game/World/Map 基础
- Tick
- Def
- XML
- Thing
- Pawn
- Component
- Map/World 数据
- 保存系统
- UI 基础
- 渲染
- 日志
- 工作系统底层支持
- 数据结构

### RimWorld

游戏规则层，包含：

- Pawn 行为
- 工作类型
- 战斗
- 派系
- 事件
- 任务
- 科技
- 生产
- 健康
- 社交
- Royalty/Ideology/Biotech/Anomaly/Odyssey 等扩展

### RimWorld.Planet

行星、世界地图、Tile、WorldObject、Caravan、飞船/重力航行等世界级系统。

---

# 6. 运行时全局入口：Current / Find / Game / World / Map

这是 Modder 最重要的一组 API。

## 6.1 Current

核心用途：获取当前全局运行状态。

典型：

```csharp
Current.Game
Current.ProgramState
```

常见场景：

- 当前 Game
- 游戏状态判断
- UI / 游戏流程判断

---

## 6.2 Find

`Find` 是 RimWorld 大量全局服务的快捷入口。

常见公开访问模式：

```csharp
Find.CurrentMap
Find.CurrentMapIndex
Find.World
Find.FactionManager
Find.LetterStack
Find.Selector
Find.TickManager
Find.UIRoot
Find.PlaySettings
Find.GameInitData
Find.ListerThings
Find.ListerBuildings
Find.ListerHaulables
Find.ResearchManager
Find.Storyteller
Find.WorldGrid
```

> 注意：不同游戏版本具体属性集合可能改变，实际写代码时必须以目标 Build 的程序集为准。

---

# 7. Def 系统：RimWorld 最大的数据型 Mod API

RimWorld Wiki 对 Def 系统的定位是：游戏内容通过 XML Def 数据驱动，C# 负责行为。

## 7.1 Def 基类

核心概念：

```csharp
Def
Editable
```

常见基础成员：

```text
defName
label
description
shortHash
index
modContentPack
fileName
modExtensions
generated
debugRandomId
```

核心方法族：

```text
ResolveReferences()
PostLoad()
ConfigErrors()
GetModExtension<T>()
HasModExtension<T>()
```

> 字段的精确可见性、类型、属性和实现必须以目标 Build 的程序集为准。

---

# 8. DefDatabase<T>

典型访问：

```csharp
DefDatabase<ThingDef>.GetNamed("Steel");
DefDatabase<ThingDef>.AllDefsListForReading;
```

主要用途：

- 查询 Def
- 枚举所有 Def
- 批量分析
- 运行时引用
- Debug / 数据处理

常见模式：

```csharp
foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
{
    ...
}
```

---

# 9. DefOf 系统

用于静态、强类型、快速引用 Def。

```csharp
[DefOf]
public static class MyDefOf
{
    public static ThingDef MyThing;
    public static JobDef MyJob;
    public static HediffDef MyHediff;
}
```

关键机制：

```text
DefOfHelper.RebindAllDefOfs()
DefOfHelper.EnsureInitializedInCtor(...)
```

典型原版：

```text
ThingDefOf
PawnKindDefOf
JobDefOf
WorkTypeDefOf
HediffDefOf
TraitDefOf
StatDefOf
SkillDefOf
DamageDefOf
BiomeDefOf
FactionDefOf
```

具体 `*DefOf` 列表随 DLC 与版本扩展。

---

# 10. DefModExtension

用于给现有 Def 增加 Mod 专属数据，而不修改核心 Def 类。

典型：

```csharp
public class MyDefExtension : DefModExtension
{
    public float value;
    public bool enabled;
}
```

XML：

```xml
<modExtensions>
    <li Class="MyNamespace.MyDefExtension">
        <value>1.5</value>
        <enabled>true</enabled>
    </li>
</modExtensions>
```

C#：

```csharp
var ext = thing.def.GetModExtension<MyDefExtension>();
```

优势：

- 不需要继承原版 Def
- 对已有 Def 类型友好
- 适合通用扩展
- 相较直接改类兼容性通常更好

限制：

- 数据挂在 Def 上
- 不适合存储每个 Thing 的独立运行时状态

---

# 11. 主要 Def 类型全量分类表

> “完整成员列表”属于版本程序集级数据；本表覆盖 Modder 最主要的 Def 类型族。

## 11.1 核心内容 Def

```text
ThingDef
RecipeDef
RecipeMaker
ResearchProjectDef
ResearchTabDef
StatDef
StatCategoryDef
SkillDef
TraitDef
TraitDegreeData
BackstoryDef
XenotypeDef
GeneDef
GeneDefExtension
HediffDef
HediffStage
ThoughtDef
ThoughtStage
PreceptDef
IdeoDef
RoleDef
AbilityDef
AbilityExtension
QuestScriptDef
QuestNode
QuestNode_Root
```

## 11.2 Pawn / 生物

```text
PawnKindDef
PawnRelationDef
PawnCapacityDef
BodyDef
BodyPartDef
BodyPartGroupDef
BodyPartDefExtension
RaceProperties
```

## 11.3 建筑 / 物体

```text
ThingDef
TerrainDef
RoofDef
TerrainDef
ThingCategoryDef
DesignationCategoryDef
BuildableDef
MoteDef
FleckDef
```

## 11.4 世界

```text
BiomeDef
WorldObjectDef
SitePartDef
CaravanFormationDef
TraderKindDef
FactionDef
FactionDefExtension
```

## 11.5 战斗

```text
DamageDef
DamageArmorCategoryDef
VerbDef
ProjectileDef
ManeuverDef
HediffDef
HediffStage
```

## 11.6 工作 / AI

```text
JobDef
WorkTypeDef
WorkGiverDef
ThinkTreeDef
ThinkNodeDef
DutyDef
DutyDefExtension
MentalStateDef
MentalStateHandler
```

## 11.7 事件 / 任务

```text
IncidentDef
IncidentWorker
IncidentTargetTagDef
QuestScriptDef
QuestNode
QuestPart
SitePartDef
WorldObjectDef
```

## 11.8 UI / 渲染 / 音频

```text
DialogDef（视版本/模组）
Gizmo
Command
GraphicData
ShaderTypeDef
SoundDef
FleckDef
MoteDef
```

---

# 12. Thing 系统：最重要的运行时对象

## 12.1 Thing

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

---

## 12.2 ThingWithComps

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

---

# 13. ThingComp API

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

---

# 14. CompProperties

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

---

# 15. Hediff 系统

Hediff 是 Pawn 身体健康、疾病、伤口、植入物、状态等系统的核心对象。

核心类型：

```text
Hediff
HediffWithComps
Hediff_AddedPart
Hediff_Injury
Hediff_MissingPart
Hediff_Implant
Hediff_Disease
HediffComp
HediffCompProperties
HediffSet
```

常见访问：

```csharp
pawn.health
pawn.health.hediffSet
```

典型：

```csharp
Hediff hediff = HealthUtility.GetHediffOfDef(pawn, HediffDefOf.SomeDef);
```

---

# 16. HediffComp

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

---

# 17. Pawn 核心 API

Pawn 是最重要、最复杂的运行时对象之一。

当前社区反编译资料展示的核心公开子系统包括：

```text
Pawn_AgeTracker
Pawn_HealthTracker
Pawn_RecordsTracker
Pawn_InventoryTracker
Pawn_MeleeVerbs
VerbTracker
Pawn_CarryTracker
Pawn_NeedsTracker
Pawn_MindState
Pawn_RotationTracker
Pawn_PathFollower
Pawn_Thinker
Pawn_JobTracker
Pawn_StanceTracker
Pawn_EquipmentTracker
Pawn_ApparelTracker
Pawn_Ownership
Pawn_SkillTracker
Pawn_TrainingTracker
Pawn_StoryTracker
Pawn_GeneTracker
Pawn_AbilitiesTracker
```

> 部分类型由 DLC 引入，具体可见性依版本/DLC而变。

来源示例：社区公开反编译仓库 `RimWorldDecompiled` / `RW-Decompile`。

---

# 18. Pawn 常见访问入口

```csharp
pawn.def
pawn.kindDef
pawn.RaceProps
pawn.ageTracker
pawn.health
pawn.records
pawn.inventory
pawn.needs
pawn.mindState
pawn.pather
pawn.jobs
pawn.stances
pawn.equipment
pawn.apparel
pawn.story
pawn.skills
pawn.training
pawn.genes
pawn.abilities
```

常见能力判断：

```csharp
pawn.Downed
pawn.Dead
pawn.Spawned
pawn.Drafted
pawn.IsColonist
pawn.IsPrisoner
pawn.IsSlave
pawn.IsMutant
```

> 上述部分属性属于不同版本/DLC组合；不要将本文表格直接当成编译期签名，必须通过目标版本 IDE/反编译器确认。

---

# 19. Pawn Health API

常见入口：

```text
pawn.health
pawn.health.hediffSet
pawn.health.capacities
pawn.health.summaryHealth
pawn.health.immunity
pawn.health.surgeryBills
```

典型判定：

```csharp
pawn.health.Downed
pawn.health.Dead
pawn.health.InPainShock
```

常见方法族：

```text
AddHediff
RemoveHediff
RemoveHediff
PreApplyDamage
PostApplyDamage
Notify_HediffAdded
Notify_HediffRemoved
```

---

# 20. Need 系统

常见：

```text
Need
Need_Food
Need_Rest
Need_Mood
Need_Joy
Need_Comfort
Need_Outdoors
Need_Beauty
Need_Indoors
Need_Authority
Need_Goodwill
```

访问：

```csharp
pawn.needs
pawn.needs.TryGetNeed(NeedDef)
pawn.needs.food
pawn.needs.rest
pawn.needs.mood
```

典型属性：

```text
CurLevel
CurLevelPercentage
MaxLevel
GainPerTick
Def
Pawn
IsFrozen
```

---

# 21. Skill 系统

```csharp
pawn.skills
pawn.skills.GetSkill(SkillDefOf.Shooting)
```

核心对象：

```text
SkillDef
Pawn_SkillTracker
SkillRecord
Passion
Level
XpTotal
XpSinceLastLevel
LearningFactor
```

典型：

```csharp
SkillRecord shooting = pawn.skills.GetSkill(SkillDefOf.Shooting);
int level = shooting.Level;
Passion passion = shooting.passion;
```

---

# 22. Trait 系统

核心对象：

```text
TraitDef
Trait
TraitSet
TraitDegreeData
```

典型：

```csharp
pawn.story.traits.HasTrait(TraitDefOf.Nudist)
pawn.story.traits.GetTrait(TraitDefOf.Nudist)
```

---

# 23. Stat 系统

Stat 是 RimWorld 的高度通用计算框架。

核心：

```text
StatDef
StatCategoryDef
StatPart
StatRequest
StatModifier
StatWorker
```

典型：

```csharp
pawn.GetStatValue(StatDefOf.MoveSpeed)
pawn.GetStatValue(StatDefOf.MovingWeight)
```

常见计算概念：

```text
BaseValue
UnfinalizedValue
FinalValue
Offset
Factor
PostProcess
StatPart
```

---

# 24. Damage 系统

核心类型：

```text
DamageInfo
DamageResult
DamageDef
DamageWorker
DamageWorker_AddInjury
DamageWorker_AddHediff
DamageWorker_Explosion
```

常见 `DamageInfo` 信息：

```text
Def
Amount
ArmorPenetrationInt
Instigator
Weapon
WeaponLinked
Angle
HitPart
Category
```

典型：

```csharp
DamageInfo dinfo = new DamageInfo(
    DamageDefOf.Bullet,
    10f,
    0f,
    -1f,
    instigator,
    hitPart
);
```

---

# 25. Verb / Weapon API

核心：

```text
Verb
VerbTracker
VerbProperties
Verb_Shoot
Verb_MeleeAttack
Verb_LaunchProjectile
Verb_CastAbility
Verb_UseAbility
```

关键关联：

```text
Thing
VerbOwner
VerbProperties
VerbTracker
Equipment
Projectile
DamageDef
SoundDef
EffecterDef
```

典型访问：

```csharp
pawn.equipment.Primary.PrimaryVerb
```

---

# 26. Job / Work 系统

## 26.1 Job

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

---

## 26.2 JobDriver

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

---

# 27. WorkGiver / 工作系统

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

---

# 28. AI / ThinkTree

核心：

```text
ThinkTreeDef
ThinkNode
ThinkNode_Priority
ThinkNode_Conditional
ThinkNode_JobGiver
JobGiver
ThinkResult
Pawn_Thinker
```

常见访问：

```csharp
pawn.thinker
pawn.mindState
```

AI 扩展通常有两类策略：

1. XML 插入/修改 ThinkTree。
2. C# 新增 ThinkNode / JobGiver。

---

# 29. Map API

`Map` 是局部地图的核心对象。

常见访问：

```csharp
Find.CurrentMap
thing.Map
pawn.Map
```

典型 Map 子系统：

```text
terrainGrid
roofGrid
edificeGrid
thingGrid
listerThings
listerBuildings
listerFilthInHomeArea
regionGrid
regionAndRoomUpdater
roomGroupGrid
roomManager
pathFinder
reachability
avoidGrid
designationManager
reservationManager
physicalInteractionReservationManager
mapTemperature
weatherManager
wildPlantSpawner
haulDestinationManager
fogGrid
areaManager
zoneManager
lordManager
storyState
```

> 实际组件数量非常大，1.6 又进行了 Map / pathfinding 等底层性能重构，具体字段不可跨版本硬编码。

---

# 30. IntVec3 / Cell API

地图开发最常用的数据类型之一。

```text
IntVec3
CellRect
IntRange
IntRange?
Rot4
Rot8
```

常见 `IntVec3`：

```text
x
y
z
```

常见访问：

```csharp
thing.Position
thing.PositionHeld
thing.Rotation
thing.Position.Roofed(map)
thing.Position.InBounds(map)
thing.Position.GetFirstThing(map, def)
```

---

# 31. Region / Room / District

地图空间分析核心结构：

```text
Region
RegionGrid
Room
RoomRole
RoomGroup
District
RegionAndRoomUpdater
```

典型查询：

```csharp
pawn.GetRoom()
cell.GetRoom(map)
```

用途：

- 房间判断
- 室内/室外
- 房间统计
- 房间角色
- 房间属性
- 路径连通
- 工作逻辑

---

# 32. World / WorldGrid / Planet

`RimWorld.Planet` 是世界级 API 的核心命名空间。

关键类型：

```text
World
WorldGrid
WorldObject
WorldObjectDef
Tile
PlanetLayer
SurfaceLayer
WorldPathGrid
Caravan
CaravanPawnUsageTracker
Settlement
Site
MapParent
```

访问：

```csharp
Find.World
Find.WorldGrid
```

---

# 33. WorldObject API

典型结构：

```text
WorldObject
Caravan
Settlement
FactionBase
Site
MapParent
TravelingParty
```

常见职责：

- 世界地图对象
- 聚落
- 任务地点
- Caravan
- 飞船/轨道对象
- 地图父对象

---

# 34. Caravan API

核心：

```text
Caravan
CaravanPawnsTracker
CaravanInventoryUtility
Caravan_PathFollower
```

常见：

```csharp
caravan.PawnsListForReading
caravan.Inventory
caravan.Tile
caravan.pather
caravan.IsPlayerControlled
```

---

# 35. Faction API

核心：

```text
Faction
FactionDef
FactionManager
FactionRelation
FactionRelationKind
```

常见访问：

```csharp
Find.FactionManager
pawn.Faction
faction.PlayerGoodwill
```

---

# 36. Incident 系统

事件系统通常由：

```text
IncidentDef
IncidentWorker
IncidentParms
IncidentResult
IncidentTargetTagDef
Storyteller
StorytellerComp
```

典型结构：

```csharp
public class IncidentWorker_MyIncident : IncidentWorker
{
    protected override bool CanFireNowSub(IncidentParms parms)
    {
        ...
    }

    protected override bool TryExecuteWorker(IncidentParms parms)
    {
        ...
    }
}
```

---

# 37. Quest 系统

核心：

```text
Quest
QuestScriptDef
QuestNode
QuestPart
QuestGen
Slate
```

典型结构：

```text
QuestNode_Root
QuestNode_Sequence
QuestNode_Filter
QuestPart_SubquestGenerator
QuestPart_Delay
QuestPart_Choice
```

Odyssey 进一步扩展了任务、地点、飞船、空间等系统，因此 1.6/DLC 目标版本必须优先阅读最新程序集。

---

# 38. UI API

核心 UI 类型族：

```text
Window
MainTabWindow
Dialog_MessageBox
Dialog_Rename
Dialog_Options
FloatMenu
FloatMenuOption
Gizmo
Gizmo_Slider
Gizmo_NonAutoActivate
Command
Command_Action
Command_Toggle
Command_Target
Listing_Standard
Widgets
Rect
Text
TooltipHandler
```

典型自定义窗口：

```csharp
public class Dialog_MyWindow : Window
{
    public override void DoWindowContents(Rect inRect)
    {
        ...
    }
}
```

---

# 39. Gizmo API

Gizmo 是选中对象后出现的交互按钮/信息控件体系。

常见：

```text
Gizmo
Gizmo_EntityInspector
Gizmo_Slider
Command
Command_Action
Command_Toggle
Command_Target
```

ThingComp 添加 Gizmo：

```csharp
public override IEnumerable<Gizmo> CompGetGizmosExtra()
{
    yield return ...;
}
```

---

# 40. Save / Scribe API

RimWorld 保存系统核心：

```text
IExposable
Scribe
Scribe_Values
Scribe_References
Scribe_Collections
Scribe_Deep
Scribe_Defs
ScribeExtractor
LookMode
LoadSaveMode
```

典型：

```csharp
public void ExposeData()
{
    Scribe_Values.Look(ref value, "value", 0);
    Scribe_References.Look(ref pawn, "pawn");
    Scribe_Collections.Look(ref list, "list", LookMode.Deep);
}
```

### 常用 Scribe 方法族

```text
Scribe_Values.Look
Scribe_References.Look
Scribe_Defs.Look
Scribe_Deep.Look
Scribe_Collections.Look
```

### 存档设计原则

- 使用稳定字段键。
- 新增字段必须提供默认值。
- 删除字段要考虑旧存档读取。
- 不保存可从 Def 重建的数据。
- 不直接序列化 Unity 对象。
- 尽量保存稳定引用而非运行时缓存。

---

# 41. Tick 系统

RimWorld 的核心模拟由 Tick 驱动。

核心对象：

```text
TickManager
Find.TickManager
GenTicks
```

典型：

```csharp
Find.TickManager.TicksGame
```

概念：

```text
Game Tick
Rare Tick
Long Tick
Frame
TickRateMultiplier
```

组件常见 Tick 方法：

```text
CompTick
CompTickRare
CompTickLong
MapComponentTick
WorldComponentTick
GameComponentTick
```

1.6 对运行性能、路径和许多分散更新逻辑进行重构，**严禁假设 1.5 的 Tick 调度实现仍然完全相同**。

---

# 42. GameComponent / WorldComponent / MapComponent

这是保存全局状态非常重要的扩展方式。

## GameComponent

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

## WorldComponent

每个 World 一份。

```csharp
Find.World.GetComponent<MyWorldComponent>()
```

## MapComponent

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

---

# 43. Harmony：修改原版代码的主流机制

Harmony 不是 Ludeon 自有 API，而是社区广泛使用的运行时补丁库。

基本结构：

```csharp
[HarmonyPatch(typeof(SomeClass))]
[HarmonyPatch(nameof(SomeClass.SomeMethod))]
static class SomePatch
{
    static void Prefix(...)
    {
    }

    static void Postfix(...)
    {
    }
}
```

常见 Patch 类型：

```text
Prefix
Postfix
Transpiler
Finalizer
```

特殊注入参数：

```text
__instance
__result
__state
__args
```

用途：

- 修改原版行为
- 监听原版方法
- 修改返回值
- 插入逻辑
- 与其他 Mod 做条件兼容

Wiki：
<https://rimworldwiki.com/wiki/Modding_Tutorials/Harmony>

---

# 44. Harmony 使用优先级

Mod 开发最佳实践不是“所有问题都 Harmony”。

推荐优先级：

```text
1. XML Def
2. PatchOperation
3. DefModExtension
4. ThingComp / HediffComp
5. Game/World/MapComponent
6. 新建继承类
7. Extension Method
8. Harmony Prefix/Postfix
9. Harmony Transpiler
10. 反射/内部字段访问
```

Harmony 教程也明确建议在可用的情况下优先考虑 subclass、ThingComp、MapComponent 等低冲突方式。

---

# 45. XML PatchOperation API

当前 Wiki 列出的主要操作类型：

```text
PatchOperationAdd
PatchOperationInsert
PatchOperationRemove
PatchOperationReplace
PatchOperationAttributeAdd
PatchOperationAttributeSet
PatchOperationAttributeRemove
PatchOperationSequence
PatchOperationAddModExtension
PatchOperationSetName
```

来源：
<https://rimworldwiki.com/wiki/Modding_Tutorials/PatchOperations>

典型：

```xml
<Operation Class="PatchOperationAdd">
    <xpath>Defs/ThingDef[defName="Steel"]</xpath>
    <value>
        <someField>123</someField>
    </value>
</Operation>
```

---

# 46. XPath Patch 核心能力

核心定位：

```text
选择节点
新增节点
删除节点
替换节点
修改属性
新增 ModExtension
改变节点名
```

常用 XPath：

```text
Defs/ThingDef[defName="X"]
Defs/ThingDef[thingCategories/li="Building"]
Defs/RecipeDef[defName="X"]
```

### 推荐命名实践

```xml
<xpath>Defs/ThingDef[defName="MyTarget"]</xpath>
```

避免：

```xml
<xpath>Defs/ThingDef[3]</xpath>
```

因为加载顺序、DLC、其他 Mod 均可能改变索引。

---

# 47. XML ParentName / Abstract

Def 支持数据继承。

典型：

```xml
<ThingDef Name="MyBase" Abstract="True">
    ...
</ThingDef>

<ThingDef ParentName="MyBase">
    <defName>MyChild</defName>
</ThingDef>
```

用途：

- 数据模板
- 降低重复
- 多个 Def 共享基础配置
- 大型 Mod 的架构标准化

---

# 48. MayRequire / 条件加载

用于跨 DLC / 跨 Mod 条件数据。

典型：

```xml
<MayRequire>ludeon.rimworld.royalty</MayRequire>
```

或按目标 Def 字段/结构使用对应条件机制。

用途：

- DLC 可选
- Mod 可选依赖
- 避免缺失 Def 导致解析错误

---

# 49. Localization API

## Languages/Keyed

```xml
<LanguageData>
    <MyKey>My translation</MyKey>
</LanguageData>
```

C#：

```csharp
"MyKey".Translate()
```

## DefInjected

用于覆盖 Def 中的：

```text
label
description
jobString
其他可注入字段
```

推荐原则：

- Def XML 不硬编码语言特定长文本。
- 所有本地化文本使用 Keyed / DefInjected。
- 中英文应该共享结构，只更换语言数据。

---

# 50. Graphic / Texture / Material

核心结构：

```text
Graphic
GraphicData
Graphic_Single
Graphic_Multi
Graphic_Collection
Material
ShaderTypeDef
```

XML 常见：

```xml
<graphicData>
    <texPath>MyMod/Thing</texPath>
    <graphicClass>Graphic_Multi</graphicClass>
    <drawSize>1</drawSize>
</graphicData>
```

---

# 51. Sound API

核心：

```text
SoundDef
SoundInfo
ResolvedGrammars
Sustainer
```

常见：

```csharp
soundDef.PlayOneShotOnCamera();
soundDef.PlayOneShot(...);
```

具体播放入口应以目标 Build 及当前 Sound API 为准。

---

# 52. Effecter / Mote / Fleck

视觉表现常见：

```text
EffecterDef
Effecter
Mote
MoteMaker
FleckDef
FleckMaker
```

用途：

- 命中特效
- 烟雾
- 火花
- 粒子
- Buff 图标/视觉反馈
- 武器效果

---

# 53. Log / Debug API

常见：

```csharp
Log.Message("...");
Log.Warning("...");
Log.Error("...");
Log.ErrorOnce("...", hash);
```

Debug 体系：

```text
DebugAction
DebugTools
DebugWindowsOpener
```

可通过特性注册/扩展 Debug Action。

---

# 54. ConfigErrors

Def 可实现配置错误报告：

```csharp
public override IEnumerable<string> ConfigErrors()
{
    foreach (var error in base.ConfigErrors())
        yield return error;

    if (value < 0)
        yield return "value must be >= 0";
}
```

用途：

- Mod 启动时主动检测错误
- 防止错误状态进入游戏
- 给用户明确日志

---

# 55. Reflection / 内部字段访问

可用但高风险。

典型：

```csharp
typeof(SomeClass)
    .GetField("someField", BindingFlags.Instance | BindingFlags.NonPublic);
```

风险：

- 私有字段重命名
- IL 优化改变
- 版本变化
- 与其他 Mod 冲突
- AOT/JIT 环境差异

推荐：

```text
公开字段 > 公共方法 > 官方扩展机制 > Harmony > Reflection
```

---

# 56. Interface / Contract 型扩展面

Mod 可以围绕公共接口做低耦合设计。

常见思路：

```text
IExposable
ILoadReferenceable
IThingHolder
IVerbOwner
IAttackTarget
IAttackTargetSearcher
ISearchableContents
ITrader
IBillGiver
```

注意：接口清单必须按具体 Build 从程序集重新导出。

---

# 57. 核心静态类/工具类族

常见高频工具：

```text
Gen
GenSpawn
GenPlace
GenClosest
GenCollection
GenMath
GenRadial
GenList
GenText
GenDate
GenTemperature
GenWorld
GenExplosion
GenColor
CellFinder
Reachability
ReservationUtility
JobMaker
HealthUtility
PawnUtility
TradeUtility
FilthMaker
ThingMaker
ThingUtility
MapParentUtility
CaravanUtility
FactionUtility
```

这些类通常提供“动作型 API”，例如：

- 生成 Thing
- Spawn
- 查找最近目标
- 计算距离
- 寻路
- 工作者判断
- Pawn 工具
- 健康工具
- 交易工具
- Caravan 工具

**这些类是 AI 编程时最值得建立索引的 API 类族。**

---

# 58. 数据类型库

高频底层类型：

```text
IntVec3
CellRect
Rect
Vector3
Vector2
Rot4
Rot8
Color
ColorInt
IntRange
FloatRange
FloatMenuOption
TaggedString
string
Def
ThingDef
PawnKindDef
JobDef
HediffDef
StatDef
SkillDef
TraitDef
FactionDef
BiomeDef
```

---

# 59. String / Text / Translation

关键类型：

```text
TaggedString
string
Keyed
NamedArgument
NamedArgument<T>
```

典型：

```csharp
"SomeKey".Translate();
"SomeKey".Translate(pawn.Named("PAWN"));
```

避免：

```csharp
"Pawn " + pawn.Name + " is ...";
```

推荐：

```csharp
"MyMod_PawnStatus".Translate(pawn.Named("PAWN"));
```

---

# 60. DLC API 分层

RimWorld DLC / Expansion 会扩展 Def 与 C# 类型。

## Royalty

关注：

```text
Psychic
Psycast
Quest
RoyalTitle
Permit
Empire
PermitWorker
RoyalFavor
```

## Ideology

关注：

```text
Ideo
Precept
Role
Ritual
Style
IdeoRole
Certainty
IdeoBuilding
```

## Biotech

关注：

```text
Gene
Xenotype
Mech
Mechlink
Implant
Pregnancy
Child
Learning
GeneTracker
```

## Anomaly

关注：

```text
Anomaly
Entities
Hediffs
CreepJoiners
Monolith
Void
Study
Containment
```

## Odyssey

关注：

```text
Gravship
Space
Orbit
Shuttle
Flying creatures
Fishing
New biomes
Landmarks
Ancient structures
Quest / site / world objects
Mechhive endgame
```

**以上 DLC API 不能以“基础游戏一定存在”为假设。** 使用时应：

```text
检查是否购买 DLC
检查 Mod 依赖
使用 MayRequire
使用 DefDatabase 查询
尽量避免静态强引用不存在的 DLC 类型
```

---

# 61. 第三方 Mod API：必须与 RimWorld 官方 API 分开

## Harmony

<https://github.com/pardeike/Harmony>

## HugsLib

RimWorld 社区广泛使用的公共 Mod Library，提供：

- Mod 初始化辅助
- Settings
- Logger
- DefDatabase helper
- UI / 工具
- 跨 Mod 基础设施

## Vanilla Expanded Framework

Vanilla Expanded 系列共享框架，为：

- Abilities
- Research
- Traits
- Genes
- Weapons
- Buildings
- Factions
- Quests
- UI

等提供扩展接口。

**注意：它不是 Ludeon 官方 API。**

---

# 62. 社区公开源码/反编译 API 数据库

## 62.1 RimWorldDecompiled

GitHub：
<https://github.com/Chillu1/RimWorldDecompiled>

覆盖大量：

```text
Verse/
RimWorld/
```

可用于查看：

- 类定义
- public 字段
- private 字段
- 属性
- 方法
- 调用关系
- DLC 类型

示例公开文件：

- `Verse/Pawn.cs`
- `Verse/Pawn_HealthTracker.cs`
- `RimWorld/Ability.cs`

**定位：社区反编译参考，不应标为官方源码 API。**

## 62.2 RW-Decompile

GitHub：
<https://github.com/josh-m/RW-Decompile>

同样提供：

- Verse 类
- RimWorld 类
- 运行逻辑参考
- API 浏览

## 62.3 RimWorld-zh/RimWorld-Decompile

GitHub：
<https://github.com/RimWorld-zh/RimWorld-Decompile>

适合中文开发者定位类型/成员。

---

# 63. XML 数据库社区仓库

## RimWorldModdingFiles

GitHub：
<https://github.com/RimWorldMod/RimworldModdingFiles>

仓库定位：

> 为 RimWorld Modding 提供可读 XML 文件、模板与示例。

结构：

```text
Defs/
├─ ThingDefs/
├─ HediffDefs/
├─ RecipeDefs/
└─ ...
```

典型内容：

- Master XML
- Templates
- Examples
- 行级注释

这是非常重要的 XML“结构数据库”。

---

# 64. Def 模板数据库

GitHub：
<https://github.com/RimWorld-zh/RimWorld-Defs-Templates>

提供：

- Def 模板
- 处理后的 Core Def
- Implied Def
- 中文开发说明

注意：模板库是“参考快照”，不是官方稳定 API。

---

# 65. 社区 Modding Reference

GitHub：
<https://github.com/Dev-Jahn/rimworld-modding-reference>

当前公开结构包括：

```text
01-rimworld-core/
02-modding-fundamentals/
...
```

其核心参考文档包括：

```text
game-architecture.md
tick-system.md
def-system.md
save-load.md
map-world-structure.md
mod-structure.md
xml-modding.md
csharp-patterns.md
```

该库非常适合用于：

- AI 编程
- 架构学习
- API 查找
- 模组工程化

但仍属于社区资料，不是 Ludeon 官方 SDK。

---

# 66. API 全量分类树

以下树是本库建议的“API 数据库组织方式”：

```text
RimWorld Mod API
├─ Runtime
│  ├─ Verse
│  │  ├─ Game
│  │  ├─ Map
│  │  ├─ World
│  │  ├─ Thing
│  │  ├─ Pawn
│  │  ├─ Def
│  │  ├─ Component
│  │  ├─ UI
│  │  ├─ Save
│  │  ├─ Tick
│  │  ├─ Rendering
│  │  └─ Utility
│  ├─ RimWorld
│  │  ├─ Pawn
│  │  ├─ Health
│  │  ├─ Combat
│  │  ├─ Jobs
│  │  ├─ Work
│  │  ├─ AI
│  │  ├─ Incident
│  │  ├─ Quest
│  │  ├─ Research
│  │  ├─ Social
│  │  └─ Faction
│  └─ RimWorld.Planet
│     ├─ World
│     ├─ Tile
│     ├─ Caravan
│     ├─ WorldObject
│     ├─ Site
│     └─ Space / Odyssey
│
├─ XML
│  ├─ Def
│  ├─ ParentName
│  ├─ Abstract
│  ├─ MayRequire
│  ├─ Patches
│  └─ Languages
│
├─ Extension
│  ├─ DefModExtension
│  ├─ ThingComp
│  ├─ HediffComp
│  ├─ GameComponent
│  ├─ WorldComponent
│  └─ MapComponent
│
├─ Runtime Patching
│  ├─ Harmony Prefix
│  ├─ Harmony Postfix
│  ├─ Harmony Transpiler
│  └─ Harmony Finalizer
│
├─ Save
│  ├─ IExposable
│  ├─ Scribe_Values
│  ├─ Scribe_References
│  ├─ Scribe_Collections
│  ├─ Scribe_Deep
│  └─ Scribe_Defs
│
└─ External Framework
   ├─ Harmony
   ├─ HugsLib
   ├─ VEF
   └─ Other Mod APIs
```

---

# 67. “变量库”的正确理解

用户常说的“RimWorld API 变量库”，实际上至少包含以下 7 类：

| 类别 | 例子 | 稳定性 |
|---|---|---:|
| Def 字段 | `defName`, `label` | 高 |
| 公开运行时字段 | `pawn.health`, `thing.def` | 中高 |
| 属性 | `pawn.Downed` | 中高 |
| 公共方法 | `GetStatValue()` | 中 |
| 内部字段 | `healthState` 等 | 低 |
| private 字段 | `nameInt` 等 | 极低 |
| 反射/IL细节 | 私有实现 | 极低 |

因此，AI 编程时不能写成：

```text
“RimWorld 的所有变量都可以直接调用。”
```

正确描述是：

```text
目标 Build 的 Assembly-CSharp / Verse / RimWorld 程序集
→ 类型
→ 成员
→ 可见性
→ 参数
→ 返回值
→ 属性
→ 继承关系
→ 调用路径
→ 是否适合 Mod API
```

---

# 68. 稳定性等级标准

建议给每个 API 条目标记稳定性：

```text
S0 = XML 正式 Mod 机制
S1 = 官方公开/明确供 Mod 使用的扩展点
S2 = public 游戏程序集成员
S3 = public 但明显属于内部实现
S4 = protected/internal
S5 = private / reflection
S6 = IL / compiler-specific implementation
```

Mod 架构优先使用：

```text
S0 → S1 → S2
```

尽量避免：

```text
S4 → S5 → S6
```

---

# 69. AI 编程 API 查询标准

任何 AI 编写 RimWorld 1.6 C# 时，应该先确定：

```text
[1] Game Build
[2] DLC
[3] Assembly
[4] Namespace
[5] Type
[6] Member
[7] Visibility
[8] Signature
[9] Lifecycle
[10] Compatibility risk
```

推荐提示词上下文：

```text
目标：RimWorld 1.6.x
DLC：Core + Royalty + Ideology + Biotech + Anomaly + Odyssey

必须：
1. 不猜测 API。
2. 优先 public API。
3. 不存在时说明。
4. 不得将 1.5/1.4 代码当成 1.6 代码。
5. 不得把社区库标记为官方 API。
6. 对 private/internal 访问必须明确标记风险。
7. 优先 Def/Comp/Component/Harmony，而不是直接修改核心类。
8. XML 中使用 XPath 定位 Def，避免序号索引。
9. 保存数据使用 Scribe。
10. 对跨 Mod/DLC 依赖使用条件机制。
```

---

# 70. 完整 API 数据库应该如何落盘

真正适合 AI/IDE 的数据库建议按以下结构：

```text
rimworld-api/
├─ manifest.md
├─ versions/
│  └─ 1.6.4850/
│     ├─ assemblies/
│     │  ├─ Assembly-CSharp.md
│     │  ├─ Verse.md
│     │  ├─ RimWorld.md
│     │  └─ RimWorld.Planet.md
│     ├─ types/
│     ├─ members/
│     ├─ defs/
│     ├─ patch-operations.md
│     ├─ interfaces.md
│     ├─ enums.md
│     ├─ delegates.md
│     ├─ attributes.md
│     └─ changelog.md
├─ dlc/
│  ├─ royalty.md
│  ├─ ideology.md
│  ├─ biotech.md
│  ├─ anomaly.md
│  └─ odyssey.md
└─ third-party/
   ├─ harmony.md
   ├─ hugslib.md
   └─ vef.md
```

---

# 71. 真正“全量变量库”的自动抽取方法

由于 Ludeon 没有发布完整正式 API Reference，最可靠方法是对目标 Build 的程序集自动导出。

## 71.1 程序集来源

Steam 安装目录中的 RimWorld 数据目录通常含：

```text
RimWorld*_Data/
```

核心程序集位置应根据目标 Build 检查 `Managed/` 等实际目录。

## 71.2 使用 ILSpy / dnSpyEx

推荐：

```text
ILSpy
ILSpyCmd
dnSpyEx
JetBrains dotPeek
```

输出：

```text
namespace
class
struct
enum
interface
delegate
attribute
field
property
method
constructor
event
```

## 71.3 自动生成 API Markdown

每个类型生成：

```markdown
# Verse.Pawn

Assembly: Assembly-CSharp.dll
Namespace: Verse
Kind: class
Base: ThingWithComps

## Fields

| Visibility | Type | Name |
|---|---|---|
| public | PawnKindDef | kindDef |
| public | Gender | gender |

## Properties

...

## Methods

...
```

---

# 72. 全量 API 抽取字段规范

每一个成员必须至少记录：

```text
assembly
namespace
full_type_name
member_kind
member_name
visibility
static
abstract
virtual
override
sealed
return_type
parameters
generic_parameters
base_type
interfaces
attributes
source_build
source_file
source_repository
notes
risk_level
```

字段级数据例：

```json
{
  "assembly": "Assembly-CSharp.dll",
  "namespace": "Verse",
  "type": "Pawn",
  "member_kind": "field",
  "name": "health",
  "visibility": "public",
  "type_name": "Pawn_HealthTracker",
  "build": "1.6.4850",
  "stability": "S2"
}
```

---

# 73. Def 全量抽取规范

Def 数据库不能只存 XML tag。

必须同时记录：

```text
def type
field name
C# type
collection element type
def reference target
default value
ParentName source
Abstract
MayRequire
DLC
Core/DLC source file
XML example
```

例如：

```text
ThingDef
├─ defName : string
├─ label : string
├─ description : string
├─ thingClass : Type
├─ graphicData : GraphicData
├─ costList : List<ThingDefCountClass>
├─ statBases : List<StatModifier>
├─ comps : List<CompProperties>
├─ apparel : ApparelProperties
├─ weaponTags : List<string>
└─ tradeTags : List<string>
```

---

# 74. Def 反向索引

建议建立：

```text
XML tag
→ C# field
→ C# type
→ Def type
→ Vanilla example
→ DLC example
→ Wiki page
```

例如：

```text
<comps>
→ ThingDef.comps
→ List<CompProperties>
→ CompProperties_*
→ ThingComp
```

这比单纯的“XML 标签表”更适合 AI。

---

# 75. API 与 Def 的双向索引

最终目标：

```text
C# Type
  ↓
Field
  ↓
XML Tag
  ↓
Def
  ↓
Vanilla Example
  ↓
Source File
```

反向：

```text
XML Tag
  ↓
Field Type
  ↓
Def Type
  ↓
Runtime Type
  ↓
Methods
```

---

# 76. 生命周期数据库

建议对所有可继承扩展点建立生命周期索引：

```text
Mod Startup
→ StaticConstructorOnStartup
→ LongEventHandler
→ Def Loading
→ ResolveReferences
→ PostLoad
→ Components Creation
→ Game Init
→ Map Init
→ Tick
→ Save
→ Load
→ Shutdown
```

Component：

```text
Constructor
→ PostSpawnSetup
→ Tick
→ ExposeData
→ DeSpawn
→ Destroy
```

Harmony：

```text
Original Method
→ Prefix
→ Transpiler/IL
→ Original
→ Postfix
→ Finalizer
```

---

# 77. 官方 / 社区资料可信度矩阵

| 来源 | 权威性 | 用途 |
|---|---:|---|
| Ludeon 官方博客 | ★★★★★ | 版本变化 |
| Ludeon 官方论坛 | ★★★★★ | 官方历史说明/开发讨论 |
| 目标 Build 实际程序集 | ★★★★★ | 精确 API |
| 目标 Build Core XML/DLC XML | ★★★★★ | 精确 Def 数据 |
| RimWorld Wiki | ★★★★☆ | Modding 教程/经验 |
| GitHub 反编译仓库 | ★★★☆☆ | API/源码阅读 |
| GitHub XML 模板库 | ★★★☆☆ | XML 结构示例 |
| 第三方 Mod Framework | ★★★☆☆ | 框架扩展 |
| 老版本论坛帖子 | ★★☆☆☆ | 历史机制 |
| AI 自身记忆 | ★☆☆☆☆ | 绝不能作为 API 真相 |

---

# 78. 当前公开资料中明确存在的“官方 API 不存在”结论

这是本库最重要的限制说明：

**不能声称 Ludeon 已经公开发布一个完整、独立、持续版本化的 RimWorld Mod API SDK。**

RimWorld Wiki 当前明确称“没有 formal modding API”；社区开发模式主要依赖：

```text
XML Def
+
C# 游戏程序集
+
官方 Source 示例
+
Harmony
+
社区 Wiki
+
反编译
```

因此，“全量 API”不是一个官方下载包，而是一个需要从目标版本运行程序集与 Def 数据中建立的数据库。

---

# 79. 官方开发资料缺口

当前公开资料存在以下天然缺口：

```text
1. 没有官方完整 Assembly API Reference
2. 没有官方完整 XML Schema
3. 没有官方完整字段默认值数据库
4. 没有官方完整 public/private 稳定性声明
5. 没有官方保证 public 方法就是 Mod API
6. DLC 对应类型散落于游戏程序集
7. 版本更新可能改变字段/调用链
```

所以“全量”必须定义为：

> **指定 Build 下，对公开运行程序集、Def、PatchOperation、组件扩展点、关键框架、官方/社区开发资料进行结构化收录。**

---

# 80. 建议的最终 Mod API 数据库分层

```text
A. Official Contract
   A1 About.xml
   A2 Defs
   A3 Patches
   A4 Languages
   A5 Assemblies loading

B. Runtime Public API
   B1 Verse
   B2 RimWorld
   B3 RimWorld.Planet
   B4 DLC namespaces

C. Extensibility API
   C1 DefModExtension
   C2 ThingComp
   C3 HediffComp
   C4 GameComponent
   C5 WorldComponent
   C6 MapComponent
   C7 JobDriver
   C8 WorkGiver
   C9 ThinkNode
   C10 QuestNode
   C11 IncidentWorker
   C12 DamageWorker
   C13 Verb
   C14 StatPart
   C15 Gizmo/Command

D. Runtime Patch API
   D1 Harmony Prefix
   D2 Harmony Postfix
   D3 Harmony Transpiler
   D4 Finalizer

E. Data API
   E1 Def
   E2 DefDatabase
   E3 DefOf
   E4 Scribe
   E5 Serialization

F. Content API
   F1 Thing
   F2 Pawn
   F3 Health
   F4 Needs
   F5 Skills
   F6 Traits
   F7 Genes
   F8 Abilities
   F9 Work
   F10 Combat
   F11 Map
   F12 World
   F13 Caravan
   F14 Faction
   F15 Quest
   F16 Incident

G. External Framework
   G1 Harmony
   G2 HugsLib
   G3 VEF
   G4 Other Mod APIs
```

---

# 81. 建议的 AI Mod 开发引用顺序

当 AI 接到 RimWorld 1.6 编码任务，应按照：

```text
任务
↓
确定 DLC
↓
确定 Def / Runtime 类型
↓
查询目标 Build API
↓
查 XML 示例
↓
寻找最小侵入扩展点
↓
优先 Comp / Extension / Component
↓
需要修改原方法时才 Harmony
↓
需要内部状态才 Reflection
↓
保存数据使用 Scribe
↓
最后做启动、运行、存档、兼容性验证
```

---

# 82. API 使用选择决策表

| 需求 | 首选方案 |
|---|---|
| 新物品 | ThingDef |
| 新建筑 | ThingDef |
| 新武器 | ThingDef + Verb/Projectile |
| 新疾病 | HediffDef |
| 自定义行为附加到 Thing | ThingComp |
| 自定义 Hediff 行为 | HediffComp |
| 给 Def 加自定义字段 | DefModExtension |
| 全局存档状态 | GameComponent |
| 世界级状态 | WorldComponent |
| 地图级状态 | MapComponent |
| 新工作 | JobDef + JobDriver |
| 新工作来源 | WorkGiver |
| 新 AI 节点 | ThinkNode |
| 新事件 | IncidentDef + IncidentWorker |
| 新伤害逻辑 | DamageWorker |
| 新技能/能力 | Def + Ability/Comp |
| 原版逻辑轻量修改 | Harmony Prefix/Postfix |
| 原版 IL 流程修改 | Harmony Transpiler |
| 字段持久化 | Scribe |
| 翻译 | Keyed / DefInjected |

---

# 83. 当前最值得纳入本 Mod 工程 Skills 的 API 禁止项

```text
禁止：
1. 猜 API 名。
2. 猜字段类型。
3. 使用旧版 1.5/1.4 API 而不说明。
4. 直接访问 private 字段而不注明 Reflection 风险。
5. 用 XML 复制大量原版内容代替精确 Patch。
6. XPath 使用不稳定的数字索引。
7. 把 Def 静态数据当成实例数据。
8. 用 GameComponent 保存应属于 Thing 的状态。
9. 在 Tick 中执行高成本 LINQ 扫描全部 Pawn。
10. 把 Harmony 当作万能方案。
11. 把第三方框架当官方 API。
12. 把反编译代码当官方源码授权文本。
```

---

# 84. 参考来源总表

## 官方

1. Ludeon Studios：Announcing Odyssey and update 1.6  
   <https://ludeon.com/blog/2025/06/announcing-odyssey-and-update-1-6/>

2. Ludeon Studios：Update 1.6.4850  
   <https://ludeon.com/blog/2026/06/update-1-6-4850-released/>

3. Ludeon Forums：How to Make a RimWorld Mod, Step by Step  
   <https://ludeon.com/forums/index.php?topic=33219.0>

4. Ludeon Forums：XML Auto-Documentation  
   <https://ludeon.com/forums/index.php?topic=21440.0>

5. Ludeon Forums：Updated Rimworld XML Auto-Documentation  
   <https://ludeon.com/forums/index.php?topic=55764.0>

6. Ludeon Forums：Mods / Help  
   <https://ludeon.com/forums/index.php?board=14.0>

## Wiki

7. RimWorld Wiki Modding Tutorials  
   <https://rimworldwiki.com/wiki/Modding_Tutorials>

8. Folder Structure  
   <https://rimworldwiki.com/wiki/Modding_Tutorials/Folder_structure>

9. ThingComp  
   <https://rimworldwiki.com/wiki/Modding_Tutorials/ThingComp>

10. Custom Comp Classes  
    <https://rimworldwiki.com/wiki/Modding_Tutorials/Custom_Comp_Classes>

11. Game/World/Map Component  
    <https://rimworldwiki.com/wiki/Modding_Tutorials/GameComponent>

12. Harmony  
    <https://rimworldwiki.com/wiki/Modding_Tutorials/Harmony>

13. PatchOperations  
    <https://rimworldwiki.com/wiki/Modding_Tutorials/PatchOperations>

14. Modifying classes  
    <https://rimworldwiki.com/wiki/Modding_Tutorials/Modifying_classes>

15. Def classes  
    <https://rimworldwiki.com/wiki/Modding_Tutorials/Def_classes>

## GitHub / 社区

16. RimWorldMod/RimworldModdingFiles  
    <https://github.com/RimWorldMod/RimworldModdingFiles>

17. RimWorld-zh/RimWorld-Defs-Templates  
    <https://github.com/RimWorld-zh/RimWorld-Defs-Templates>

18. Chillu1/RimWorldDecompiled  
    <https://github.com/Chillu1/RimWorldDecompiled>

19. josh-m/RW-Decompile  
    <https://github.com/josh-m/RW-Decompile>

20. RimWorld-zh/RimWorld-Decompile  
    <https://github.com/RimWorld-zh/RimWorld-Decompile>

21. Dev-Jahn/rimworld-modding-reference  
    <https://github.com/Dev-Jahn/rimworld-modding-reference>

22. Kon-on/rimworld-modding-skill  
    <https://github.com/Kon-on/rimworld-modding-skill>

23. Harmony  
    <https://github.com/pardeike/Harmony>


# 86. RimWorld 1.6 Modder Primer：必须纳入的版本级 API 变更

Ludeon 在 1.6 公测期间明确提供了 **1.6 Modder Primer**，RimWorld Wiki 也将它列入“由 Ludeon 直接提供给玩家和 Mod 作者”的官方文档。

官方 Primer：
<https://docs.google.com/document/d/e/2PACX-1vRKE9u5ZW_zG45pxzwNvy4sxvozDeqtxlxpac5jwenOeW6liQCPgmPl9bIbtcMuqL1NPIDHOLFg64M_/pub>

## 86.1 设计器系统

1.6 中：

```text
DraggableDimensions
↓
DrawStyleCategory
↓
DrawStyleCategoryDef
```

XML 从：

```xml
<placingDraggableDimensions>...</placingDraggableDimensions>
```

迁移为：

```xml
<drawStyleCategory>...</drawStyleCategory>
```

官方/社区记录的原版 `DrawStyleCategoryDef` 预设包括：

```text
Default2D
Fill2D
Default1D
FilledRectangle
Walls
Conduits
Defenses
Zones
Areas
Orders
Mine
Paint
Plans
RemovePlans
RemoveZones
Foundations
Floors
Cancel
Plants
```

## 86.2 Buildings / Ambient Sound

旧的：

```xml
<soundAmbient>...</soundAmbient>
```

1.6 改为 ThingComp：

```xml
<li Class="CompProperties_AmbientSound">
    <sound>Toxifier_Working</sound>
    <disabledOnUnpowered>true</disabledOnUnpowered>
</li>
```

这说明 1.6 的一个重要架构趋势是：**原先单字段行为向 Comp 组件化迁移。**

## 86.3 Pawn / RaceProperties

```text
RaceProperties.wildness
↓
StatDef
```

XML 从原先 RaceProperties 字段转向：

```xml
<statBases>
    <Wildness>...</Wildness>
</statBases>
```

同时新增：

```text
RaceProperties.forceGender
```

## 86.4 DamageDef

1.6 新增：

```xml
<ignoreShields>true</ignoreShields>
```

其对盾牌/远程保护机制的影响由伤害类型与护盾逻辑共同决定。

## 86.5 Hediff

字段迁移：

```text
causesNeed                → removed
disablesNeeds             → HediffStage
chemicalNeed              → HediffDef root
enablesNeeds              → HediffStage
```

## 86.6 Terrain

```text
holdSnow
↓
holdSnowOrSand
```

## 86.7 Plant

```text
hideAtSnowDepth
↓
hideAtSnowOrSandDepth
```

## 86.8 Scenario

1.6 引入：

```text
ScenarioBase
```

新的 ScenarioDef 应考虑从该抽象基类继承，以获得地图分层相关字段。

## 86.9 Interaction

```text
InteractionUtility
↓
SocialInteractionUtility
```

注意：旧名对应的新语义不能与现存的 `InteractionUtility` 类混淆。

## 86.10 Transporter 命名变更

```text
ActiveDropPodInfo
↓
ActiveTransporterInfo
```

以及 PawnFinder 中历史上包含 `TransportPods` 的一些 API 命名迁移到 `Transporters`。

## 86.11 Mutant → Subhuman

部分 target 参数：

```text
canTargetMutants
↓
canTargetSubhumans
```

这类 API 重命名是 AI 编程最容易误用的版本差异之一。

## 86.12 Odyssey 锁定开发面

当前公开资料记录的 Odyssey 相关技术面包括：

```text
Gravships
Transport Shuttles
Landmarks
Tile Mutators
新 GenSteps
Space
Asteroid Quests
Oxygen
Vacuum
Fishing
Mixed Biome Maps
Foraging / Targeted Attack / Sentience
Droughts / Frozen Water / Floods / Sandstorms
新增 Questgiver
Substructure
Space/Fishing/Colony Moving 等新 Precept
```

部分条目在 Wiki 发布时曾标注“需要进一步确认”，因此不要将社区 datamining 信息无条件当成官方契约。

---

# 87. 1.6 性能 / 生命周期 API 注意事项

1.6 的重大底层变化包括：

```text
Pathfinding：多线程 + batching
Lighting：多线程
大量系统更新工作重新分散
Caravan 计算优化
Hauling 优化
Animal pen 计算优化
Memory leak 修复
启动时间大幅缩短
```

因此 Mod API 的性能规范必须加入：

```text
Tick 内禁止无界全图扫描
Tick 内禁止重复创建大集合
谨慎使用 LINQ
缓存 Def / 静态数据
尽量使用已有 Lister / Grid / Tracker
MapComponent 中区分高频 tick 与低频 tick
对异步/多线程相关代码绝不默认主线程语义
```

尤其要注意：**1.6 引入的新调度/变量 Tick 率方向意味着“每 Tick 执行一次”的老习惯不能机械照搬。**

---

# 88. 全量 API 数据库的“Build 锚定原则”

本库后续如果继续扩展，不应写：

```text
RimWorld 1.6 API
```

而应该写：

```text
RimWorld 1.6.4850
+ Core
+ Royalty
+ Ideology
+ Biotech
+ Anomaly
+ Odyssey
+ Windows x64 / Unity runtime
```

原因：

```text
1.6.4528 ≠ 1.6.4535 ≠ 1.6.4630 ≠ 1.6.4850
```

即使游戏声明 Mod 兼容，程序集仍可能在修复版本中增加、删除、改名或改变内部实现。

---

# 89. 最终“全量”验收标准

真正完成“全量 RimWorld Modder API 数据库”时，必须能够对以下问题进行逐项回答：

```text
Q1：某个 Type 在哪个 Assembly？
Q2：完整 Namespace 是什么？
Q3：继承谁？
Q4：实现哪些 Interface？
Q5：有哪些 public / protected / internal / private Field？
Q6：每个字段的类型是什么？
Q7：有哪些 Property？
Q8：Property 的 getter/setter 可见性是什么？
Q9：有哪些 Method？
Q10：参数顺序、类型、默认值是什么？
Q11：返回值是什么？
Q12：有哪些 overload？
Q13：有哪些 Enum？
Q14：有哪些 Attribute？
Q15：哪些成员属于 DLC？
Q16：哪些成员在 1.5→1.6 被删除？
Q17：哪些成员被重命名？
Q18：哪些 Def XML tag 映射到这些字段？
Q19：哪些字段可以通过 Def/XML 设置？
Q20：哪些字段只可运行时设置？
Q21：哪些成员是安全扩展点？
Q22：哪些成员属于实现细节？
Q23：哪些字段有存档影响？
Q24：哪些调用必须主线程？
Q25：哪些调用是高频/高成本？
Q26：哪些 API 与 Harmony/Comp/Component 存在替代方案？
Q27：哪个 Build 最后验证过？
```

只有这些信息全部可以自动查询，才能将该库称为真正意义上的“全量 API 库”。

---

# 85. 最终结论

本次检索能够确认的 RimWorld Modder 对外开发生态不是单一“API 文档”，而是一个由 **官方机制 + 游戏程序集 + XML Def + 官方论坛 + 社区 Wiki + 反编译数据库 + Harmony/框架** 共同组成的开发接口体系。

如果目标是制作真正用于 **AI 编程 / RimWorld 1.6 Mod 工程化开发** 的“全量 API 库”，最可靠的目标不是继续人工罗列更多类名，而是：

```text
官方 1.6.x Build
        ↓
实际 Assembly-CSharp / Verse / DLC 程序集
        ↓
自动提取全部 Type / Field / Property / Method / Enum / Interface
        ↓
提取 Core + DLC 全部 Def/XML 字段
        ↓
建立 XML ↔ C# ↔ Runtime 双向索引
        ↓
建立版本差异数据库
        ↓
标注 API 稳定性
        ↓
最终形成真正意义上的“RimWorld 1.6 Mod API 全量数据库”
```

**本文件已经覆盖公开开发生态、官方/社区资料、核心 API 分类、变量体系、Def/Comp/Component/Harmony/Scribe/UI/AI/Map/World/Pawn/DLC 等主要开发面；但“每一个类的每一个字段、属性、方法、参数、枚举值”不能在没有指定并读取对应 1.6 Build 二进制程序集的情况下诚实地声称已经逐项穷尽。**

这一区别必须保留，否则所谓“全量 API”会混入旧版本、反编译误差或社区自定义 API。

---

# 90. 面向 Modder 的官方支持面：除 API/变量之外必须纳入的开发能力

> 本章用于补齐“官方支持”这一概念。RimWorld 没有独立的 Formal Mod SDK，但 Ludeon 通过游戏本体加载器、XML 体系、Def 系统、程序集入口、开发模式、版本化 Mod 目录、DLC/Mod 条件加载、存档系统、设置系统以及官方提供的 1.6 Modder Primer 等方式，实际上向 Modder 暴露了一整套可使用的开发面。
>
> 因此，“官方支持”应理解为：**游戏本体明确提供、允许 Mod 使用、或者官方资料明确针对 Modder 进行说明的扩展能力**；它不等于“稳定 ABI/API”。

## 90.1 官方支持能力总矩阵

| 能力 | 1.6 | 官方/本体支持 | 是否推荐 | 典型入口 |
|---|---:|---|---|---|
| Mod 文件夹加载 | ✓ | 本体 | ✓ | `Mods/` |
| `About.xml` | ✓ | 本体 | ✓ | `About/About.xml` |
| `Preview.png` | ✓ | 本体/Workshop | ✓ | `About/Preview.png` |
| `ModIcon.png` | ✓ | 本体 | ✓ | `About/ModIcon.png` |
| `Assemblies/` | ✓ | 本体 | ✓ | C# DLL |
| `Defs/` | ✓ | 本体 | ✓ | XML Def |
| `Patches/` | ✓ | 本体 | ✓ | `PatchOperation*` |
| `Languages/` | ✓ | 本体 | ✓ | `DefInjected` / `Keyed` / `Strings` |
| `Textures/` | ✓ | 本体 | ✓ | Unity Texture |
| `Sounds/` | ✓ | 本体 | ✓ | 音频资源 |
| `Materials/` | ✓ | 本体 | ✓ | 材质资源 |
| `AssetBundles/` | ✓ | 本体 | ✓ | Unity AssetBundle |
| `LoadFolders.xml` | ✓ | 本体 | ✓ | 版本/DLC/Mod 条件加载 |
| `IfModActive` | ✓ | 本体 | ✓ | 条件目录加载 |
| `IfModNotActive` | ✓ | 本体 | ✓ | 条件目录加载 |
| `IfModActiveAll` | ✓ | 本体，1.6 增强 | ✓ | 多条件 AND |
| 版本化目录 | ✓ | 本体 | ✓ | `1.6/`、`Common/` 等 |
| Mod 依赖声明 | ✓ | 本体 | ✓ | `modDependencies` |
| 替代 packageId | ✓ | 本体，1.6 新增 | ✓ | `alternativePackageIds` |
| Load Before/After | ✓ | 本体 | ✓ | `loadBefore` / `loadAfter` |
| ModSettings | ✓ | 本体 | ✓ | `ModSettings` / `Mod.GetSettings<T>()` |
| DefModExtension | ✓ | 本体架构 | ✓ | 自定义 Def 数据 |
| ThingComp | ✓ | 本体架构 | ✓ | Thing 级行为 |
| HediffComp | ✓ | 本体架构 | ✓ | Hediff 级行为 |
| AbilityComp 等 Comp | ✓ | 本体架构 | ✓ | Def/Thing/Pawn 子系统 |
| MapComponent | ✓ | 本体架构 | ✓ | Map 生命周期 |
| WorldComponent | ✓ | 本体架构 | ✓ | World 生命周期 |
| GameComponent | ✓ | 本体架构 | ✓ | Game 生命周期 |
| WorldObjectComp | ✓ | 本体架构 | ✓ | WorldObject 生命周期 |
| Custom Def Class | ✓ | XML → C# | ✓ | `workerClass` / `thingClass` 等 |
| Custom C# | ✓ | 本体程序集加载 | ✓ | `Assemblies/*.dll` |
| Harmony Patch | ✓ | 第三方运行时库 | ✓ | Prefix/Postfix/Transpiler |
| Reflection | ✓ | .NET/Mono | △ | 私有成员访问 |
| ExposeData | ✓ | 本体存档框架 | ✓ | `Scribe_*` |
| `Log.Error` / Config Errors | ✓ | 本体日志系统 | ✓ | 开发期验证 |
| DebugAction | ✓ | 本体开发模式 | ✓ | `[DebugAction]` |
| Development Mode | ✓ | 本体 | ✓ | Debug Toolbar |
| TweakValue | ✓ | 本体开发工具 | ✓ | 快速调整参数 |
| Steam Workshop 上传 | ✓ | 本体 + Steam | ✓ | Dev Mode / Workshop |
| Rider/VS 调试 | ✓ | Unity/运行时 + 社区工具 | ✓ | Debugger |
| Asset Bundle 平台区分 | ✓ | 本体，1.6 强化 | ✓ | Bundle 内容加载 |

来源：
- RimWorld Wiki：Mod Folder Structure / About.xml / ModSettings / GameComponent / DebugActions / Testing Mods / Asset Bundles
- Ludeon：1.6 + Odyssey 官方发布说明

---

# 91. Mod 文件系统：官方支持的全部主要内容入口

## 91.1 标准目录

```text
MyMod/
├─ About/
│  ├─ About.xml
│  ├─ Preview.png
│  ├─ ModIcon.png
│  └─ PublishedFileId.txt
│
├─ Assemblies/
├─ Defs/
├─ Patches/
├─ Languages/
│  └─ English/
│     ├─ DefInjected/
│     ├─ Keyed/
│     └─ Strings/
├─ Textures/
├─ Sounds/
├─ Materials/
├─ AssetBundles/
├─ LoadFolders.xml
└─ 1.6/                 # 可选版本化目录
   ├─ Assemblies/
   ├─ Defs/
   ├─ Patches/
   ├─ Languages/
   ├─ Textures/
   └─ ...
```

RimWorld 对文件夹和若干特殊路径具有固定大小写要求。跨平台 Mod 必须把大小写当作实际兼容性约束，而不能依赖 Windows 的大小写不敏感文件系统。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Mod_Folder_Structure

## 91.2 About.xml：官方 Mod 元数据与依赖接口

1.6 开发中最重要的字段类别：

- `packageId`
- `name`
- `author` / `authors`
- `description`
- `supportedVersions`
- `url`
- `modDependencies`
- `modDependenciesByVersion`
- `alternativePackageIds`
- `loadBefore`
- `loadAfter`
- 对应的版本化加载/依赖字段

### 1.6 特别值得纳入标准

```xml
<modDependencies>
  <li>
    <packageId>some.mod</packageId>
    <displayName>Some Mod</displayName>
    <alternativePackageIds IgnoreIfNoMatchingField="True">
      <li>some.mod.dev</li>
    </alternativePackageIds>
  </li>
</modDependencies>
```

`alternativePackageIds` 是 1.6 新增的重要依赖兼容能力，可用于稳定版/开发版 packageId 不同的依赖兼容。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/About.xml

---

# 92. LoadFolders.xml：官方支持的版本/DLC/Mod 条件加载系统

这是当前 Mod 工程最容易被低估、但对大型架构极其重要的官方加载机制。

## 92.1 基本结构

```xml
<loadFolders>
  <v1.6>
    <li>/</li>
    <li>1.6</li>
  </v1.6>
</loadFolders>
```

## 92.2 DLC/Mod 条件

```xml
<loadFolders>
  <v1.6>
    <li IfModActive="Ludeon.RimWorld.Odyssey">Odyssey</li>
    <li IfModNotActive="Ludeon.RimWorld.Odyssey">NoOdyssey</li>
  </v1.6>
</loadFolders>
```

## 92.3 1.6 的重要能力：All 条件

`IfModActiveAll` 用于多个 Mod/DLC 同时满足时才加载目标目录，相比 `IfModActive` 的 OR 语义可以建立更清晰的功能矩阵。

### 推荐工程模式

```text
Common/
Odyssey/
VanillaExpanded/
CombatExtended/
Compatibility/
```

通过 `LoadFolders.xml` 把兼容代码从核心实现中隔离，能够显著降低条件分支与程序集耦合。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Mod_Folder_Structure

---

# 93. 官方支持的 XML / Def 扩展路线

XML 并不是“只是配置文件”，而是 RimWorld 的主要内容注入接口。

官方/本体支持的核心路线：

```text
XML Def
  ↓
Def 类
  ↓
C# 字段 / 属性
  ↓
Worker / Comp / Component / Class
  ↓
游戏运行时
```

### 主要方式

| 方式 | 用途 | 推荐度 |
|---|---|---:|
| 新建 Def | 新内容 | ★★★★★ |
| 修改 Def | 修改少量原版数据 | ★★★★☆ |
| XPath Patch | 精确修改现有 Def | ★★★★★ |
| DefModExtension | 增加自定义静态数据 | ★★★★★ |
| Comp | 增加动态行为 | ★★★★★ |
| 自定义 Def Class | 深度改变 Def 行为 | ★★★★☆ |
| Harmony | 修改运行时行为 | ★★★★☆ |
| 覆盖整个 Def | 大范围重构 | ★☆☆☆☆ |

官方社区教程明确推荐 XPath、Comp、DefModExtension 等低侵入方式，并将直接覆盖整个 Def 视为兼容性较差的方案。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Modifying_defs

---

# 94. XML ↔ C# 官方支持的绑定机制

RimWorld 大量 Def 字段直接接受类型名称，因此 XML 可以指定 C# 类。

典型字段：

```xml
<workerClass>MyWorker</workerClass>
<thingClass>MyThing</thingClass>
<hediffClass>MyHediff</hediffClass>
```

或者列表节点：

```xml
<comps>
  <li Class="MyCompProperties">
    <field>value</field>
  </li>
</comps>
```

这意味着 Mod 的稳定工程模式不是“纯 XML”或“纯 C#”，而是：

```text
Def = 数据契约
C# = 行为
Comp = 可组合行为
Patch = 外部兼容修改
Language = 显示层
```

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Linking_XML_and_C

---

# 95. 官方支持的本地化开发接口

## 95.1 DefInjected

针对 Def 字段进行语言覆盖：

```text
Languages/
└ English/
   └ DefInjected/
      └ ThingDef/
         Example.xml
```

## 95.2 Keyed

针对 C# / UI / 消息 / 菜单等非 Def 固定字符串：

```xml
<LanguageData>
  <MyMod_ActionLabel>Perform action</MyMod_ActionLabel>
</LanguageData>
```

代码：

```csharp
"MyMod_ActionLabel".Translate()
```

## 95.3 Strings

用于游戏的字符串数据系统，适合特定格式/字符串需求。

### 工程原则

**不要将面向玩家的文本硬编码进 C#。**

推荐：

```csharp
"MyMod.Action.Label".Translate()
```

而不是：

```csharp
new FloatMenuOption("执行操作", ...)
```

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Localization

---

# 96. 官方支持的组件化架构

RimWorld 的组件体系是大型 Mod 最重要的低耦合扩展路线之一。

## 96.1 ThingComp

作用域：单个 Thing。

适用：

- 武器能力
- 建筑行为
- 物品状态
- 可充能行为
- 主动/被动效果

## 96.2 HediffComp

作用域：单个 Hediff。

适用：

- 疾病
- 状态效果
- 部件
- 伤口
- 增益/减益

## 96.3 MapComponent

作用域：Map。

适用于：

- 地图级管理器
- 全图统计
- 多实体协调器
- 地图缓存

## 96.4 WorldComponent

作用域：World。

适用于：

- 世界级数据
- 世界事件
- 多地图共享数据

## 96.5 GameComponent

作用域：Game。

适用于：

- 当前存档全局状态
- 游戏级系统
- 跨地图数据

## 96.6 WorldObjectComp

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

---

# 97. 官方存档系统：ExposeData / Scribe

RimWorld 原生提供自己的序列化体系。

核心入口：

```csharp
public override void ExposeData()
{
    Scribe_Values.Look(ref myValue, "myValue");
    Scribe_Deep.Look(ref myObject, "myObject");
    Scribe_Collections.Look(ref myList, "myList");
}
```

核心类型族：

- `Scribe_Values`
- `Scribe_References`
- `Scribe_Deep`
- `Scribe_Collections`
- `Scribe_Defs`

适用位置：

- `GameComponent`
- `WorldComponent`
- `MapComponent`
- `ThingComp`
- `HediffComp`
- 自定义可保存对象

### 重要约束

保存数据应当：

1. 有稳定 key。
2. 有缺省值处理。
3. 考虑旧存档迁移。
4. 不依赖运行时瞬时引用。
5. 避免保存可由 Def 恢复的静态数据。

---

# 98. 官方 ModSettings 系统

RimWorld 原生支持 Mod 设置，不需要 HugsLib 才能实现。

典型结构：

```csharp
public class MySettings : ModSettings
{
    public bool enabled = true;
    public float multiplier = 1f;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref enabled, "enabled", true);
        Scribe_Values.Look(ref multiplier, "multiplier", 1f);
    }
}
```

然后在 `Mod` 类中提供：

- `SettingsCategory()`
- `DoSettingsWindowContents(Rect inRect)`
- `GetSettings<T>()`
- `WriteSettings()`

### 官方 UI 常用入口

- `Listing_Standard`
- `Widgets`
- `Rect`
- `FloatMenu`
- `FloatMenuOption`
- `Window`
- `Dialog_*`

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/ModSettings

---

# 99. 官方开发模式 / Debug API

## 99.1 Development Mode

RimWorld 原生 Development Mode 提供：

- Debug Actions
- Debug Outputs
- Inspector
- 路径可视化
- Job 调试
- Incident 调试
- Pawn 调试
- 翻译错误检查
- 大量开发期生成/修改操作

这不是普通玩家功能意义上的“作弊 API”，对于 Mod 开发实际属于官方内置测试平台。

来源：
https://rimworldwiki.com/wiki/Development_mode
https://rimworldwiki.com/wiki/Modding_Tutorials/Testing_mods

## 99.2 DebugAction

可以把任意静态方法注册进开发模式菜单：

```csharp
[DebugAction(
    "MyMod",
    "Do Something",
    actionType = DebugActionType.Action,
    allowedGameStates = AllowedGameStates.PlayingOnMap)]
public static void DoSomething()
{
}
```

可用动作类别包括：

- `Action`
- `ToolMap`
- `ToolMapForPawns`
- `ToolWorld`

`AllowedGameStates` 可以组合使用。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/DebugActions

---

# 100. TweakValues：官方开发期参数调试能力

TweakValues 用于快速修改开发参数，非常适合：

- 数值平衡
- UI 尺寸
- 测试参数
- AI 参数验证
- 性能调试
- 快速迭代

它属于**开发/调试设施**，而不是正式游戏数据存储机制。

工程中建议：

```text
TweakValues
   ↓
开发调试
   ↓
验证最终数值
   ↓
回写 Def / Settings / 常量配置
```

不要把 TweakValues 当成正式 Mod 配置数据库。

---

# 101. 官方支持的错误报告 / 配置错误接口

Mod 可以通过游戏日志系统报告：

- Error
- Warning
- Message
- 配置错误
- 加载错误
- 翻译缺失
- Def 配置问题

推荐在启动阶段尽早发现错误，而不是让异常延迟到用户实际点击功能时才出现。

对于 Mod 工程，可建立：

```text
Config Validation
   ↓
Def Validation
   ↓
Dependency Validation
   ↓
Runtime Validation
```

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Config_Errors

---

# 102. Asset / Rendering 资源支持

## 102.1 Textures

标准路径：

```text
Textures/
```

适合：

- Thing 图像
- Pawn 图像资源
- UI 图形
- 材质贴图

## 102.2 Sounds

标准路径：

```text
Sounds/
```

## 102.3 Materials

1.6 开发环境可通过：

```text
Materials/
```

提供材质资源。

## 102.4 AssetBundles

1.6 对 AssetBundle 的支持明显增强，可用于：

- Shader
- Font
- Mesh
- 复杂 Unity Asset
- 大量纹理
- 预构建资源

对于小型 Mod，普通 `Textures/` / `Sounds/` 更简单；对于复杂 Unity 资源，AssetBundle 是更适合的官方加载路径。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Asset_Bundles

---

# 103. 1.6 官方特别支持：性能与生命周期变化

1.6 不仅新增内容，也对底层运行时做了大量性能工作：

- Pathfinding 多线程/批处理
- Lighting 多线程
- Caravan foraging 优化
- Egg-laying Pawn 优化
- Alert 优化
- Hauling 优化
- Animal pen 计算优化
- 内存泄漏修复
- 启动时间优化

因此 Modder 必须特别注意：**旧版本基于“每帧/每 tick 做一次”的代码假设，不应直接当作 1.6 最佳实现。**

尤其需要审查：

- Tick 调度
- 大规模 LINQ
- Map 全扫描
- Pawn 全扫描
- Thing 全扫描
- Pathfinding 调用
- GUI 每帧重建
- 高频缓存失效

来源：
https://ludeon.com/blog/2025/06/announcing-odyssey-and-update-1-6/
https://store.steampowered.com/news/posts/?appgroupname=RimWorld&appids=294100&enddate=1752076799&feed=steam_community_announcements

---

# 104. 官方 1.6 Modder Primer：必须作为一级资料源

Ludeon 在 1.6 公测阶段明确提供了 **1.6 Modder Primer**，用于帮助 Mod 作者提前迁移 Mod；Wiki 也明确将该文件列为“直接由 Ludeon 提供”的 Modder 文档。

因此 API 资料优先级应调整为：

```text
P0 目标 Build 实际程序集 / Def
P1 Ludeon 官方 1.6 Modder Primer
P2 Ludeon 官方 1.6 Change Log
P3 Ludeon 官方论坛开发资料
P4 RimWorld Wiki Modding Tutorials
P5 社区反编译 / GitHub API 索引
P6 第三方 Framework API
```

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.6_Mod_Updates

---

# 105. 官方支持与第三方支持必须严格区分

## 105.1 官方/本体层

```text
RimWorld Mod Loader
├─ About.xml
├─ LoadFolders.xml
├─ Def Loader
├─ Patch Loader
├─ Language Loader
├─ Assembly Loader
├─ Asset Loader
├─ ModSettings
├─ Scribe
├─ Comp / Component
├─ Development Mode
└─ Steam Workshop integration
```

## 105.2 第三方层

```text
Harmony
HugsLib
Vanilla Expanded Framework
XML Extensions
Community Frameworks
其他 Mod API
```

其中 Harmony 是目前社区修改运行时代码的通用工具，但**不能描述为 Ludeon 自己开发的官方 Mod API**。RimWorld Wiki 将 Harmony 作为修改运行时方法的最佳实践之一，同时建议直接使用 Harmony，而不是仅为了 Harmony 引入 HugsLib。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Harmony

---

# 106. 官方支持的 Mod 分发能力

RimWorld 原生支持 Steam Workshop 发布流程。

典型流程：

```text
本地 Mod
  ↓
About.xml
  ↓
Preview.png
  ↓
Development Mode
  ↓
Mods 菜单
  ↓
Upload on Steam
  ↓
PublishedFileId.txt
```

`PublishedFileId.txt` 保存 Workshop 项目的唯一 ID，用于后续更新。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Distribution

---

# 107. 官方支持的跨版本架构能力

建议将 Mod 架构设计为：

```text
MyMod/
├─ About/
├─ Common/
│  ├─ Defs/
│  ├─ Languages/
│  └─ Textures/
├─ 1.6/
│  ├─ Assemblies/
│  ├─ Defs/
│  ├─ Patches/
│  └─ Languages/
├─ Odyssey/
│  ├─ Defs/
│  └─ Patches/
├─ Compatibility/
│  ├─ ModA/
│  └─ ModB/
└─ LoadFolders.xml
```

这样可以把：

- 游戏版本差异
- DLC 差异
- Mod 兼容层
- Core 基础层
- UI/本地化层
- C# 行为层

彻底隔离。

这与“统一、标准化、防过耦合、防硬编码”的工程目标高度一致。

---

# 108. 官方支持的 Mod 开发调试工具链

## 推荐开发链

```text
Visual Studio / Rider / VS Code
        ↓
.NET / C# 项目
        ↓
RimWorld + Verse + DLC 程序集引用
        ↓
编译 DLL
        ↓
Assemblies/
        ↓
RimWorld Dev Mode
        ↓
Log / DebugAction / Inspector
        ↓
Debugger / ILSpy
```

RimWorld 1.6 使用 Unity 2022.3.35，并运行 Unity 修改版 Mono；普通 .NET 调试器不能简单地按普通 .NET 程序方式附加，因此实际开发通常需要针对 RimWorld/Unity 的调试方案。

来源：
https://rimworldwiki.com/wiki/Modding_Tutorials/Testing_mods
https://rimworldwiki.com/wiki/Modding_Tutorials/Recommended_software

---

# 109. 官方允许的源码研究 / 反编译边界

Ludeon 官方论坛中，Tynan 曾明确说明 Modder 可以使用现有游戏代码来制作免费的 RimWorld Mod；EULA 也允许出于学习或 Mod 制作目的反编译/查看游戏资源，但不允许将游戏资源独立打包传播。

工程上应因此采用：

```text
可以：
读取 / 反编译 / 分析 / 引用行为逻辑 / 作为 Mod 开发参考

不要：
将原版 DLL / 完整游戏资产 / 独立提取的原版资源作为 Mod 内容重新分发
```

来源：
https://ludeon.com/forums/index.php?topic=16672.0
https://ludeon.com/forums/index.php?topic=51391.0

**注意：以上是对官方论坛与 EULA 资料的开发实践归纳，不构成法律意见。发布 Mod 时仍应以当前 EULA 和适用法律为准。**

---

# 110. 官方开发社区支持渠道

## Ludeon 官方论坛

https://ludeon.com/forums/

适合：

- 官方公告
- 版本更新
- Modding 讨论
- 技术问题
- 官方开发者信息

## 官方开发 Discord

Ludeon 在 1.6 发布说明中明确提供官方开发 Discord，用于测试、公测反馈与开发交流。

## Modder 社区 Discord

Ludeon 官方论坛曾长期用于推广 Modder Discord，作为 Mod 作者协作、技术讨论和经验共享渠道。

来源：
https://ludeon.com/forums/index.php?topic=27314.0
https://ludeon.com/blog/2025/06/announcing-odyssey-and-update-1-6/

---

# 111. 面向 AI Mod 开发的“官方支持面”引用优先级

为了避免 AI 编码时把第三方方案误认为原版能力，建议固定使用以下标签：

| 标签 | 含义 |
|---|---|
| `VANILLA_SUPPORTED` | RimWorld 本体直接提供 |
| `OFFICIAL_DOC` | Ludeon 官方文档/公告说明 |
| `OFFICIAL_FORUM` | Ludeon 官方论坛资料 |
| `GAME_ASSEMBLY` | 目标 Build 实际程序集能力 |
| `COMMUNITY_DOC` | Wiki/社区维护资料 |
| `DECOMPILED` | 反编译/源码镜像所得 |
| `THIRD_PARTY_API` | Harmony/HugsLib/VFE 等第三方框架 |
| `UNSTABLE_INTERNAL` | 内部实现，不应视为稳定接口 |

### AI 生成代码时必须先判断

```text
我要的功能
↓
原版 XML 能否解决？
↓ 否
原版 DefModExtension / Comp / Component？
↓ 否
原版公开/可引用 C# 类？
↓ 否
Harmony？
↓ 必要时
Reflection / 私有字段？
↓ 最后手段
```

这样可以尽量保证兼容性与可维护性。

---

# 112. 本库新增“官方支持”覆盖清单

本次整合后，本总库除原有 API/变量/Def/程序集体系外，已经覆盖：

- [x] 官方 1.6 Modder Primer
- [x] 官方 1.6 Change Log
- [x] Mod 文件系统
- [x] About.xml
- [x] Mod dependencies
- [x] alternativePackageIds
- [x] loadBefore / loadAfter
- [x] supportedVersions
- [x] LoadFolders.xml
- [x] IfModActive
- [x] IfModNotActive
- [x] IfModActiveAll
- [x] 版本化目录
- [x] Common 目录
- [x] Defs
- [x] PatchOperations
- [x] DefModExtension
- [x] Comp / Component
- [x] Game/World/Map Component
- [x] WorldObjectComp
- [x] XML ↔ C# class binding
- [x] Languages
- [x] DefInjected
- [x] Keyed
- [x] Strings
- [x] ModSettings
- [x] ExposeData / Scribe
- [x] Development Mode
- [x] DebugAction
- [x] TweakValues
- [x] Error/Config validation
- [x] Texture / Sound / Materials
- [x] AssetBundles
- [x] Workshop distribution
- [x] Debugger / Rider / VS Code / Visual Studio 工具链
- [x] 官方论坛 / 官方开发 Discord
- [x] 官方源码研究与反编译边界
- [x] AI 开发引用优先级

---

# 113. 资料来源更新记录

本次补充重点核验来源：

1. Ludeon Studios — Announcing Odyssey and update 1.6
   https://ludeon.com/blog/2025/06/announcing-odyssey-and-update-1-6/
2. RimWorld Wiki — RimWorld 1.6 Mod Updates
   https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.6_Mod_Updates
3. RimWorld Wiki — About.xml
   https://rimworldwiki.com/wiki/Modding_Tutorials/About.xml
4. RimWorld Wiki — Mod Folder Structure
   https://rimworldwiki.com/wiki/Modding_Tutorials/Mod_Folder_Structure
5. RimWorld Wiki — Localization
   https://rimworldwiki.com/wiki/Modding_Tutorials/Localization
6. RimWorld Wiki — ModSettings
   https://rimworldwiki.com/wiki/Modding_Tutorials/ModSettings
7. RimWorld Wiki — GameComponent
   https://rimworldwiki.com/wiki/Modding_Tutorials/GameComponent
8. RimWorld Wiki — DebugActions
   https://rimworldwiki.com/wiki/Modding_Tutorials/DebugActions
9. RimWorld Wiki — Testing Mods
   https://rimworldwiki.com/wiki/Modding_Tutorials/Testing_mods
10. RimWorld Wiki — Asset Bundles
    https://rimworldwiki.com/wiki/Modding_Tutorials/Asset_Bundles
11. RimWorld Wiki — Distribution
    https://rimworldwiki.com/wiki/Modding_Tutorials/Distribution
12. RimWorld Wiki — Harmony
    https://rimworldwiki.com/wiki/Modding_Tutorials/Harmony
13. Ludeon Forums — RimWorld source code
    https://ludeon.com/forums/index.php?topic=3394.0
14. Ludeon Forums — RimWorld game license, effect on modding
    https://ludeon.com/forums/index.php?topic=16672.0
15. Ludeon Forums — Modding Discord
    https://ludeon.com/forums/index.php?topic=27314.0
16. RimWorldMod — RimworldModdingFiles
    https://github.com/RimWorldMod/RimworldModdingFiles

---

# 114. 最终定位

本文件现在不是单纯的“API 列表”，而应定义为：

> **RimWorld 1.6 Modder 公开开发接口、官方支持能力、Def/XML Schema、组件架构、生命周期、存档、UI、本地化、调试、资源、版本化、兼容层、第三方 API 与开发资料总索引。**

但仍需强调：

```text
“官方支持面全量” ≠ “每一个 C# 成员逐个列出”
```

后者必须建立在具体 RimWorld Build（例如 `1.6.5000`）的程序集快照基础上，对所有 DLL 执行：

```text
Assembly
 → Namespace
 → Type
 → BaseType
 → Interface
 → Constructor
 → Field
 → Property
 → Method
 → Parameter
 → ReturnType
 → Attribute
 → Enum
 → Generic
 → Dependency
```

再与：

```text
Def XML
Patch XML
Languages
DLC
Version Changes
```

建立双向映射，才能形成真正意义上的**可机器检索 RimWorld API Database**。


# Pawn 与 Stat 核心 API

> 来源：原总库 §17–§23。本工程（职业/编年史）高频使用。

## 17. Pawn 核心 API

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

## 18. Pawn 常见访问入口

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

## 19. Pawn Health API

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

## 20. Need 系统

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

## 21. Skill 系统

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

## 22. Trait 系统

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

## 23. Stat 系统

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

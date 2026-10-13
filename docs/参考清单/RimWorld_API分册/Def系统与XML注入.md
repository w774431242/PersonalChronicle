# Def 系统与 XML 注入

> 来源：原总库 §7–§11、§47、§48、§73–§75、§93、§94。

## 7. Def 系统：RimWorld 最大的数据型 Mod API

RimWorld Wiki 对 Def 系统的定位是：游戏内容通过 XML Def 数据驱动，C# 负责行为。

### 7.1 Def 基类

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

## 8. DefDatabase<T>

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

## 9. DefOf 系统

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

## 10. DefModExtension

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

## 11. 主要 Def 类型全量分类表

> “完整成员列表”属于版本程序集级数据；本表覆盖 Modder 最主要的 Def 类型族。

### 11.1 核心内容 Def

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

### 11.2 Pawn / 生物

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

### 11.3 建筑 / 物体

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

### 11.4 世界

```text
BiomeDef
WorldObjectDef
SitePartDef
CaravanFormationDef
TraderKindDef
FactionDef
FactionDefExtension
```

### 11.5 战斗

```text
DamageDef
DamageArmorCategoryDef
VerbDef
ProjectileDef
ManeuverDef
HediffDef
HediffStage
```

### 11.6 工作 / AI

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

### 11.7 事件 / 任务

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

### 11.8 UI / 渲染 / 音频

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

## 47. XML ParentName / Abstract

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

## 48. MayRequire / 条件加载

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

## 73. Def 全量抽取规范

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

## 74. Def 反向索引

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

## 75. API 与 Def 的双向索引

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

## 93. 官方支持的 XML / Def 扩展路线

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

## 94. XML ↔ C# 官方支持的绑定机制

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

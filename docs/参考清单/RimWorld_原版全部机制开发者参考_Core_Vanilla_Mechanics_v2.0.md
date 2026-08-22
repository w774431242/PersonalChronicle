# RimWorld 原版全部机制开发者参考 v2.0
## Core Vanilla Mechanics & Architecture Reference
### 基准版本：RimWorld 1.6.4633
### 截止：2026-08-15
### 范围：Core / Base Game，兼顾 DLC 对 Core 的基础层影响，但不把 DLC 专属玩法伪装成 Core

> **文档性质**
>
> 这不是普通玩家百科，而是一份面向 RimWorld Mod 开发者、AI 编程、架构设计、兼容性审查和长期维护的原版机制参考。
>
> 目标不是只回答“这个机制怎么玩”，而是回答：
>
> **它是什么 → 由哪些对象构成 → 如何运行 → 如何与其他系统交互 → 首次在哪个版本出现 → 后续怎样演化 → Mod 应从哪里接入 → 哪些地方最容易产生兼容问题。**

---

# 0. 文档范围与版本口径

## 0.1 当前基准

RimWorld Wiki 当前记录的 PC 稳定版本为：

**1.6.4633 — 2025-11-04**

截至 2026-08-15，本参考以 **1.6.x Core** 为当前稳定基准。

官方 1.6 发布于 2025-07-11，与 Odyssey 同期，但 1.6 本身是**免费基础游戏更新**，不要求拥有 Odyssey。官方说明明确列出了大规模性能优化、重做地图生成、动物飞行、Caravan、Plans、建筑与 UI 改进等内容。

参考：

- RimWorld Wiki 版本史
- Ludeon 官方 Steam 更新公告

---

# 1. 版本语义

文档中的版本标签严格区分：

| 标签 | 含义 |
|---|---|
| `Alpha` | 早期 Alpha 阶段已经形成；无法可靠定位到具体 Alpha 时不虚构精确版本 |
| `B18` | Beta 18 |
| `B19` | Beta 19 |
| `1.0` | 正式版 |
| `1.x` | 某大版本进入/重构机制 |
| `1.x.xxxx` | 在具体补丁中有明确证据的机制变化 |
| `CURRENT` | 当前 1.6.x 仍有效 |
| `LEGACY` | 机制已被替代或不再作为当前主要实现 |
| `CORE` | 基础游戏 |
| `DLC-TO-CORE` | DLC 同期对 Core 基础层产生影响，但不是 DLC 专属玩法 |
| `DLC` | DLC 专属，本文仅用于边界说明 |

**原则：**

> 不知道具体 Alpha 小版本，就写 Alpha；不知道具体补丁，就写 1.x；不要伪造精确版本。

---

# 2. 全历史版本总线

## 2.1 早期公开阶段

RimWorld 最早公开展示于 **2013 年 9 月**；开发始于 2012 年 2 月。Beta 18 的官方说明明确指出，这时距离最初开发已约五年，并称 B18 已进入“最终收尾阶段”。

### Alpha 时代核心目标

早期版本逐步建立了：

- Pawn
- 工作
- 技能
- 需求
- 心情
- 健康
- 战斗
- 建筑
- 资源
- 农业
- 动物
- 电力
- 温度
- 贸易
- 派系
- 世界
- Caravan
- 事件
- Storyteller
- Research
- Ship / Endgame
- Mod / XML / Def

这些机制后来成为 Core 的稳定基础。

---

# 3. Alpha 版本演化摘要

> 早期 Alpha 的单个版本差异非常多；本节不伪装为逐 commit 数据库，而按已知公开版本节点总结机制演化。

## 3.1 Alpha 9–12：基础模拟快速扩张

主要特征：

- Pawn/Needs/Mood 基础体系成熟
- 动物与畜牧逐步增强
- 农业与营养持续扩展
- 药物系统增强
- 贸易与资源循环增强
- 世界地图逐步形成
- XML / Data Modding 体系扩大

Alpha 12 仍存在典型 Data/XML Mod 与 Code Mod 的兼容差异，官方版本公告已明确讨论 XML-only Mod 与代码 Mod 的兼容方式。

**版本：Alpha 9–12**

---

# 4. Alpha 15

## 4.1 重大新增

Alpha 15 的公开说明明确提到：

- Tutorial
- Learning Helper
- 新 Drug System
- Deep Drilling
- 大量细节改进

其中 Deep Drilling 标志着：

> 即使进入中后期，玩家仍可以通过主动系统从地图获得资源。

**版本：A15**

## 4.2 开发者意义

A15 代表 Core 开始从：

`基本殖民地模拟`

转向：

`长期成长 + 后期资源 + 教学 + 可持续系统`

---

# 5. Alpha 17：世界与远征机制的重要节点

Alpha 17 是一次非常明显的世界层扩展。

## 5.1 Roads / Rivers

加入：

- 世界道路
- Dirt Road
- Stone Road
- Ancient Asphalt
- Ancient Highway
- 多种河流
- 河流生成逻辑
- 道路与局部地图连接

道路会影响：

- Visitors
- Traders
- Caravans
- Raiders

的出入路线。

## 5.2 World Quest

新增：

- Item Stash
- Bandit Camp
- Caravan Request
- Payment Demand
- Site Parts
- Long-range Mineral Scanner

## 5.3 时间体系

Alpha 17 将时间重新设计成：

**Quadrum 四季**

并使四季在整个行星上具有一致性。

## 5.4 Caravan

进一步形成：

- Caravan Route Planner
- 临时地图
- Caravan Incident
- 贸易/战斗/任务连续性

**版本：A17**

---

# 6. Beta 18

Beta 18 的意义不是“内容大爆发”，而是：

> 基础系统进入稳定化与收尾阶段。

官方说明明确表示：

- 不再计划加入完整的新大型系统
- 继续修复设计问题
- 继续平衡
- 继续处理复杂 Mod 兼容性

这意味着 B18/B19 是从“快速创造机制”向“正式产品稳定化”过渡。

**版本：B18**

---

# 7. Beta 19

Beta 19 进一步稳定：

- Pawn
- Combat
- Stats
- World
- Storyteller
- Save
- Def
- Mod API

随后进入 1.0。

**版本：B19**

---

# 8. RimWorld 1.0

## 8.1 正式发布

**2018-10-17**

Steam 官方资料确认 1.0 正式发布。

## 8.2 1.0 的本质

1.0 不是从零开始的游戏，而是：

> **Alpha/Beta 多年模拟系统的稳定基线。**

因此对 Mod 开发最重要的是：

- Core Def 基础体系
- Pawn 生命周期
- Thing
- Job
- Work
- Health
- Hediff
- Stats
- Map
- World
- Faction
- Incident
- Storyteller
- Caravan
- Research
- Save / Scribe
- XML Def
- Harmony 生态

在 1.0 左右形成相对稳定的 API 语言。

---

# 9. 1.1

## 9.1 定位

1.1 是正式版后的第一轮大型 Core 演进。

重点是：

- QoL
- 基础机制扩展
- 生产与物品
- 动物
- Caravan
- UI
- Mod 兼容性
- 性能

## 9.2 DLC 关系

Royalty 与 1.1 同期。

因此必须区分：

```text
Core 1.1
+
Royalty
```

而不能把 Royalty 专属机制错误地标记为原版。

**版本：1.1**

---

# 10. 1.2

## 10.1 定位

1.2 是 Royalty 后的 Core 细化版本。

主要方向：

- 贸易
- Caravan
- UI
- Quest
- 运输
- Permit 接口等 DLC/Core 边缘能力
- Mod 稳定性

部分 1.2 更新同时涉及 Royalty，必须使用来源标记区分。

**版本：1.2**

---

# 11. 1.3

## 11.1 重大 Core 机制

1.3 是非常重要的 Core 机制版本。

### 动物圈养系统

新增：

- Pen
- Pen Marker
- Fence
- Animal Flap
- Rope
- Egg Box
- 自动屠宰
- 动物绝育
- Animal Release

### Raid

新增/强化：

- Breach Raid
- Termite
- Breach Axe

### Pawn

新增：

- Facial Hair
- Wound Overlay
- Prosthetic Overlay

### Food

新增：

- Vegetarian Fine Meal
- Vegetarian Lavish Meal
- Carnivore Fine Meal
- Carnivore Lavish Meal

### UI

新增：

- 搜索框
- 更多对话框搜索
- 选中多个 Pawn 后调整编队间隔

### Technical

最值得 Mod 开发者关注的是：

> 多数 Mote 从 Thing 改成独立的 threaded Fleck 系统。

这意味着渲染与实体对象边界进一步明确。

## 11.2 Goodwill

1.3 重构 Goodwill：

```text
Current Goodwill
        ↓
Natural Goodwill
        ↑
Recent Events
        ↓
Decay Over Time
```

**版本：1.3**

---

# 12. 1.4

## 12.1 核心定位

1.4 与 Biotech 同期。

Core 层进一步强化：

- 基础建筑
- UI
- 生产
- 储存
- Pawn
- Rendering
- Def
- Modding
- 性能

## 12.2 Core/DLC 分界

Biotech 专属：

- Genes
- Xenotypes
- Children
- Pregnancy
- Mechanoid Colony
- Pollution 等

不能直接写进 Core Vanilla。

但是 Biotech 同期会推动 Core 提供更泛化的底层能力。

**版本：1.4**

---

# 13. 1.5

1.5 是又一个非常重要的开发者版本。

官方 1.5 更新说明称这是约 18 个月的工作，完整 Changelog 长达 18 页。

## 13.1 Core 新机制

### Books

新增：

- Books
- Bookcase
- Textbooks
- Schematics
- Novels

书籍成为：

`Recreation + Skill + Research + Room`

的跨系统实体。

### Crawling

倒地但仍有操作能力的 Pawn：

- 可以爬行
- 避开危险
- 寻找安全地点
- 产生血迹

这是：

`Downed → Movement → AI → Health`

的明显扩展。

### Game Over Recovery

当所有殖民者离开/死亡后：

- 可以生成 1–6 个新 Wanderers
- 使用原殖民地遗留设施继续游戏

### UI

新增/重做：

- Search
- Mood Highlight
- Pawn Silhouette
- 12/24 Hour Clock
- Mouse Zoom
- Prisoner Tab
- Health Tab

### Building / Designation

加入：

- Clean Room
- Mine Vein
- Smooth Walls
- Smooth Floors

### Core Health

值得特别注意：

> **Lung Rot 从 Biotech 移入 Core。**

### Rendering

1.5 的技术变化非常大：

- Pawn Rendering 改为 parent/child node system
- Pawn Rendering multithreaded
- Dynamic Thing Culling
- Wall attached buildings
- Asset hot reload

### Save

加入：

- Virtual relationship records
- 减少 Save 膨胀
- 多项序列化优化

### Def / XML

- ThingComp 可影响 Thing stats
- 自定义 XML Loader
- 扩展 Stat
- Damage / Graphic 等底层更加可配置

**版本：1.5**

---

# 14. 1.6

## 14.1 官方定位

1.6 是当前 Core 基准。

官方明确强调：

- massive performance improvements
- reworked map generation
- flying animals
- caravan improvements
- plan improvements
- building
- UI

## 14.2 多线程寻路

Pathfinding 改为：

```text
Path Requests
 ↓
Batch
 ↓
Multithread
 ↓
Result
```

这会影响 Mod：

- 路径查询成本
- Tick 分布
- 并发安全
- 自定义 Pathing

## 14.3 多线程 Lighting

Lighting 也进行并行化。

## 14.4 Variable Tick Rate

后台对象不再始终以完全相同的频率运行。

Mod 不能假设：

> “我的组件每个逻辑时间点都必定精确 Tick 一次。”

## 14.5 Caravan 优化

重点优化：

- forage
- food search
- movement
- packing
- hauling

## 14.6 Plan

加入：

- 9 色
- Copy
- Paste
- Rotate
- Rename

## 14.7 Terrain Layers

地形结构进一步分层：

```text
Natural Terrain
 ↓
Foundation
 ↓
Floor
 ↓
Temporary Terrain
```

## 14.8 Designator Draw Style

设计工具支持不同拖拽绘制样式。

**版本：1.6**

---

# 15. Core 数据模型总览

RimWorld 的核心对象建议理解为：

```text
Def
 ↓
Thing / Pawn / Building / Recipe / Job ...
 ↓
Runtime State
 ↓
Components
 ↓
Systems
 ↓
UI / Rendering / Save
```

---

# 16. Def 系统

Def 是整个 Core 的“数据声明层”。

常见：

```text
ThingDef
RecipeDef
JobDef
WorkGiverDef
HediffDef
ThoughtDef
TraitDef
IncidentDef
ResearchProjectDef
TerrainDef
BiomeDef
FactionDef
PawnKindDef
StatDef
DamageDef
DesignationDef
QuestScriptDef
RulePackDef
```

## 16.1 ThingDef

ThingDef 的 XML 字段极其庞大；公开 Modding 教程指出 ThingDef 本身就有 200+ 有效 XML 标签，还不包括大量子标签。

因此：

> 不应假定“一个 ThingDef = 几个简单字段”。

## 16.2 Def 的开发原则

优先：

```text
Def
+
Comp
+
DefModExtension
```

而不是把所有数据写死在 C#。

---

# 17. DefModExtension

用途：

> 给现有 Def 添加额外数据，而无需创建一个新的 Def 类型。

适合：

- 标签
- 数值
- UI 配置
- Mod 自定义参数
- 跨系统数据

特点：

- 轻量
- 高兼容
- 非常适合扩展数据层

---

# 18. Thing / ThingWithComps

## 18.1 Thing

几乎所有地图实体的共同运行基础。

包括：

- Item
- Building
- Plant
- Corpse
- Projectile
- Pawn 等

## 18.2 ThingWithComps

允许附加组件。

典型：

```text
ThingWithComps
├── CompPower
├── CompRefuelable
├── CompFlickable
├── CompGlower
├── CompSpawner
└── Custom ThingComp
```

### 生命周期

典型流程：

```text
ThingMaker
 ↓
Thing instance
 ↓
PostMake()
 ↓
InitializeComps()
 ↓
Create Comp
 ↓
Set Parent
 ↓
Initialize()
```

这套流程对自定义 ThingComp 特别重要。

**版本：Core 基础机制 / 长期演化**

---

# 19. Component 系统

分为多种层级：

```text
GameComponent
MapComponent
WorldComponent
ThingComp
```

用途：

| 类型 | 作用 |
|---|---|
| GameComponent | 游戏全局数据 |
| MapComponent | 单地图数据 |
| WorldComponent | 世界数据 |
| ThingComp | 单 Thing 数据/行为 |

这是 Mod 解耦的重要扩展入口。

---

# 20. Pawn 生命周期

开发者应理解完整流水线：

```text
PawnKindDef
 ↓
PawnGenerationRequest
 ↓
Pawn Generation
 ↓
Faction
 ↓
Backstory
 ↓
Traits
 ↓
Skills
 ↓
Relations
 ↓
Needs
 ↓
Health
 ↓
Equipment
 ↓
Apparel
 ↓
Spawn
 ↓
Tick
 ↓
Death
 ↓
Corpse
```

---

# 21. Pawn 数据层

主要模块：

- Story
- Bio
- Skills
- Traits
- Backstory
- Relations
- Needs
- Health
- WorkSettings
- JobTracker
- Duty
- Inventory
- Equipment
- Apparel
- MentalState
- Drawer

---

# 22. Skill 系统

核心：

```text
SkillDef
SkillRecord
Passion
XP
Level
```

技能经验受：

- 工作
- 阅读
- Training
- Skilltrainer
- Trait
- Backstory
- Stat / Learning factor

影响。

---

# 23. Trait 系统

Trait：

```text
TraitDef
 ↓
Trait
 ↓
Stat / Thought / Ability / Behavior
```

一个 Trait 可以同时影响：

- Stat
- Work
- Mood
- AI
- Social
- Mental State

---

# 24. Backstory 系统

Backstory 不只是人物介绍。

可能影响：

- 技能等级
- Passion
- 工作能力
- Traits
- 社会关系
- 描述文本

1.5 对 Backstory 的 XML 数据加载方式进行了优化，进一步把数据与逻辑分离。

---

# 25. Stat 系统

这是上一版最重要的补缺之一。

核心：

```text
StatDef
 ↓
StatWorker
 ↓
StatPart
 ↓
Base Value
 ↓
Modifier
 ↓
Final Value
```

常见 Stat：

- MoveSpeed
- WorkSpeed
- GlobalLearningFactor
- ShootingAccuracy
- MeleeHitChance
- Armor
- Beauty
- MarketValue
- AnimalGatherYield
- ButcheryEfficiency
- PlantHarvestYield

## 25.1 StatPart

用于把外部状态注入 Stat：

```text
Base Stat
+
Hediff
+
Apparel
+
Equipment
+
Thought
+
Terrain
+
Weather
+
Comp
=
Final Stat
```

**Mod 开发重要性：★★★★★**

---

# 26. Capacity 系统

Health 并不只靠 HP。

Pawn 具有多种身体能力：

- Consciousness
- Moving
- Manipulation
- Sight
- Hearing
- Talking
- Breathing
- BloodPumping
- BloodFiltration
- Digestion
- Metabolism 等

典型：

```text
Body Part
 ↓
Hediff
 ↓
Part Efficiency
 ↓
Capacity
 ↓
Work / Combat / AI
```

---

# 27. Hediff 系统

核心分类：

```text
Hediff
├── Injury
├── Disease
├── Condition
├── AddedPart
├── MissingPart
├── Implant
├── Scar
└── HediffWithComps
```

关键字段：

- Severity
- Part
- Stage
- TendQuality
- Bleeding
- Pain
- Immunity
- Progress

---

# 28. HediffComp

HediffComp 与 ThingComp 类似：

> 给健康状态附加行为模块。

适合：

- 周期效果
- 触发
- 治疗
- 免疫
- 额外状态
- Stat Modifier
- 毒素/疾病扩展

---

# 29. Job 系统

核心：

```text
JobDef
 ↓
Job
 ↓
JobDriver
 ↓
Toil
```

例如：

```text
搬运
 ↓
Reserve Target
 ↓
GoTo
 ↓
PickUp
 ↓
Carry
 ↓
Drop
```

---

# 30. WorkGiver 系统

工作决策：

```text
Pawn
 ↓
WorkType
 ↓
WorkGiver
 ↓
Potential Jobs
 ↓
CanGiveJob
 ↓
Job
```

---

# 31. Reservation 系统

这是 Pawn AI 与 Job 系统的重要锁机制。

```text
Pawn A
 ↓
Reserve
 ↓
Target
```

确保：

> 多个 Pawn 不会同时争抢一个不可并行的资源或目标。

核心概念：

- Reservation
- ReservationManager
- Reserve
- CanReserve
- Release
- TargetInfo

---

# 32. Target 系统

RimWorld 不直接把所有目标当 Cell。

需要区分：

```text
LocalTargetInfo
GlobalTargetInfo
TargetInfo
```

Target 可以是：

- Cell
- Thing
- Pawn
- World Object
- Position

---

# 33. ThinkTree / Pawn AI

这是上一版另一个严重遗漏。

核心：

```text
ThinkTree
 ↓
ThinkNode
 ↓
JobGiver
 ↓
Job
```

可以继续分：

```text
ThinkNode
├── Priority
├── Conditional
├── Random
├── JobGiver
├── Subtree
└── MentalState
```

---

# 34. Duty 系统

Duty 表示：

> Pawn / Group 当前应该执行的“战略职责”。

例如：

- Defend
- Assault
- Escort
- Wander
- Guard
- Work
- Regroup

---

# 35. Lord / Group AI

多个 Pawn 并非总是独立决策。

```text
Lord
 ↓
LordJob
 ↓
LordToil
 ↓
State / Transition
 ↓
Group Behavior
```

典型：

- Raid
- Siege
- Assault
- Defend
- Flee
- Wander
- Stage

---

# 36. Mental State

必须和 Mood 分离理解：

```text
Mood
 ↓
Mental Break
 ↓
MentalState
 ↓
ThinkTree / Job Override
```

MentalState 是：

> 对 Pawn 日常 AI 的强制行为层。

---

# 37. Need 系统

典型：

- Food
- Rest
- Recreation
- Comfort
- Mood
- Chemical
- Beauty / Environment related

Need 每隔一定时间更新，并与：

- Job
- Schedule
- Thought
- Mood
- AI

产生联动。

---

# 38. Thought 系统

Thought 可以来自：

- Food
- Social
- Room
- Beauty
- Death
- Injury
- Apparel
- Weather
- Work
- Recreation
- Relation
- Events

链：

```text
Event
 ↓
Thought
 ↓
Mood
```

---

# 39. Room 系统

Room 的生成基于 Region / Room graph。

Room 会聚合：

- Beauty
- Space
- Cleanliness
- Temperature
- Wealth
- Impressiveness
- Role

Room Role 进一步决定：

- Bedroom
- Dining Room
- Recreation Room
- Hospital
- Prison
- Workshop 等

---

# 40. Region 系统

Region 是路径与空间分析的重要底层结构。

Region 影响：

- Pathfinding
- Room
- Reachability
- Room Roles
- Door Connectivity

---

# 41. Pathfinding

核心概念：

```text
Map
 ↓
PathGrid
 ↓
Region
 ↓
TraverseParms
 ↓
PathFinder
 ↓
Path
```

考虑：

- Passability
- Danger
- Door
- Terrain
- Pawn
- Faction
- Forbidden
- Reservation
- Cost

## 41.1 1.6

Pathfinding 改为 fully multithreaded + batched。

**版本：1.6**

---

# 42. Designation 系统

玩家并非直接调用 Job。

通常：

```text
Designator
 ↓
Designation
 ↓
DesignationManager
 ↓
WorkGiver / Job
```

例：

- Mine
- Chop
- Harvest
- Hunt
- Deconstruct
- Smooth
- Build

---

# 43. Area 系统

包括：

- Home
- Allowed
- Animal
- Work-related zones
- Snow clearing
- Custom Areas

1.5/1.6 对 Area 生命周期和作用域继续优化。

---

# 44. Zone 系统

主要：

- Growing
- Stockpile
- Dumping

Zone 通常是：

```text
Cell Set
+
Manager
+
Filter
+
Behavior
```

---

# 45. Map 系统

Map 可以理解为：

```text
Map
├── TerrainGrid
├── RoofGrid
├── GlowGrid
├── SnowGrid
├── PathGrid
├── RegionGrid
├── Thing Grid / Listers
├── Room
├── PowerNet
├── Temperature
├── Weather
├── Designation
├── Reservation
├── Area
└── MapComponents
```

---

# 46. Lister 系统

用于快速查询地图对象。

例如：

- ListerThings
- Things by category
- Plants
- Buildings
- Pawns
- Haulables

开发者应避免每 Tick 进行全地图扫描。

优先：

> 使用已有 Lister / Manager / Cache。

---

# 47. Spawn / Despawn

## Spawn

```text
ThingMaker
 ↓
Spawn
 ↓
Map
 ↓
Region/Lister
 ↓
Grid
 ↓
Comp
 ↓
Graphics
```

## Despawn

```text
Despawn
 ↓
Remove from grids
 ↓
Lister update
 ↓
Room/Power/Region update
 ↓
Clear Map reference
```

这是自定义实体最容易出错的生命周期之一。

---

# 48. Terrain

Terrain 影响：

- Walk Speed
- Fertility
- Beauty
- Cleanliness
- Flammability
- Temperature
- Water
- Bridge
- Foundation

1.6 加强多层 Terrain。

---

# 49. Roof

Roof 影响：

- Weather
- Temperature
- Light
- Room
- Fire
- Vacuum-like DLC systems
- Damage

1.6 对建筑边角、Roof 与房间判定继续修正。

---

# 50. Glow / Lighting

光照影响：

- Plant Growth
- Accuracy
- Mood
- Room
- Visual
- AI

1.6 对 Lighting 进行多线程化。

---

# 51. Temperature

计算涉及：

```text
External Temperature
+
Room
+
Roof
+
Walls
+
Doors
+
Heat Sources
+
Coolers
+
Ventilation
+
Weather
 ↓
Ambient Temperature
```

---

# 52. Power

核心：

```text
PowerComp
 ↓
PowerNet
 ↓
Energy
```

包括：

- Production
- Consumption
- Battery
- Conduits
- Short Circuit
- Grid overload / excess
- Switch
- Flickable

---

# 53. Fire

```text
Thing
 ↓
Flammability
 ↓
Fire
 ↓
FireSize
 ↓
Heat
 ↓
Damage / Spread
```

建筑 Fire Damage 会受 Flammability 等属性影响。

---

# 54. Explosion

Explosion 是独立的区域效果系统：

```text
Center
 ↓
Radius
 ↓
Targets
 ↓
Damage
 ↓
Fire / Smoke / Flecks
```

影响：

- Buildings
- Pawns
- Items
- Terrain
- Fire
- Gas
- Visuals

---

# 55. Plant

Plant 本身就是 Thing。

生命周期：

```text
Spawn
 ↓
Growth
 ↓
Mature
 ↓
Harvest
 ↓
Death
```

主要因素：

- Fertility
- Temperature
- Light
- Water / Soil assumptions
- Season
- GrowthRate
- HarvestYield

---

# 56. Agriculture

核心：

```text
GrowingZone
 ↓
PlantDef
 ↓
Sow
 ↓
Growth
 ↓
Harvest
```

---

# 57. Animal

基础：

- Race
- PawnKind
- Animal Body
- Food
- Taming
- Training
- Breeding
- Life stages
- Filth
- Products
- Combat

---

# 58. Pen

1.3：

```text
PenMarker
+
Fence / Door
 ↓
Pen
 ↓
Animal Assignment
 ↓
Handlers
```

---

# 59. Animal Training

训练类型包括：

- Obedience
- Release
- Rescue
- Haul
- Guard / Attack 等具体动物能力

取决于动物种类和 Training Tags。

---

# 60. Breeding / Life Stage

动物具有：

```text
Pawn
 ↓
LifeStage
 ↓
Growth
 ↓
Adult
 ↓
Age
```

涉及：

- Reproduction
- Pregnancy
- Gender
- Mating
- Offspring
- LifeStage

---

# 61. Food / Nutrition

核心链：

```text
Ingredient
 ↓
Recipe
 ↓
Meal
 ↓
Nutrition
 ↓
Need Food
```

---

# 62. Food Poisoning

食物不仅有 Nutrition，还可能有：

- Food Poisoning
- Bad ingredient
- Poor Cooking
- Storage failure

形成：

```text
Food Quality
+
Cooking
+
Meal
 ↓
Food Poisoning Chance
```

---

# 63. Production / Recipe

核心：

```text
RecipeDef
 ↓
Bill
 ↓
Ingredient Search
 ↓
Reservation
 ↓
Haul
 ↓
Work
 ↓
Product
```

---

# 64. Ingredient Search

关键对象/概念：

- ThingRequest
- ThingFilter
- ThingRequestGroup
- IngredientCount
- IngredientValueGetter
- Search radius
- Reservation
- Forbidden

这是生产、建筑、医疗和搬运共享的基础。

---

# 65. Bill

Bill 可定义：

- 次数
- 无限
- 材料
- 工作者
- Skill
- Quality
- Ingredient Filter
- 成品处理

---

# 66. Quality

原版品质：

```text
Awful
Poor
Normal
Good
Excellent
Masterwork
Legendary
```

质量可能影响：

- Stat
- Beauty
- Value
- Combat
- Room Impressiveness

---

# 67. Stuff

Stuff 是材料系统，不是简单字符串。

典型：

```text
ThingDef
+
StuffDef
 ↓
Final Properties
```

材料可以改变：

- Hit Points
- Market Value
- Beauty
- Flammability
- Armor
- Temperature
- Work To Make

---

# 68. Inventory

Pawn Inventory 与 Equipment/Apparel 不应混为一谈。

```text
Pawn
├── Inventory
├── Equipment
└── Apparel
```

不同系统具有不同 AI、访问方式、容量和保存逻辑。

---

# 69. Weapon / Verb

核心：

```text
Equipment
 ↓
VerbTracker
 ↓
Verb
 ↓
Target
 ↓
Attack
```

Verb 可以是：

- Melee
- Ranged
- Ability-like interaction
- Launch Projectile

---

# 70. Damage

核心数据：

```text
DamageInfo
├── DamageDef
├── Amount
├── ArmorPenetration
├── Instigator
├── Weapon
├── HitPart
└── Source
```

流程：

```text
Verb
 ↓
DamageInfo
 ↓
Armor
 ↓
DamageWorker
 ↓
BodyPart
 ↓
Hediff
```

---

# 71. Armor

Armor 可以影响：

- Damage chance
- Damage amount
- Penetration
- Damage type

应与 Apparel / Stat / DamageDef 联合理解。

---

# 72. Cover

Cover 是战斗中的环境状态。

```text
Attacker
 ↓
Cover
 ↓
Target
```

覆盖建筑/地形/障碍物并参与命中算法。

---

# 73. Projectile

Projectile 具有：

- Origin
- Target
- Position
- Rotation
- Speed
- Damage
- Hit check
- Impact
- Graphic

---

# 74. Melee

近战典型流程：

```text
Verb_MeleeAttack
 ↓
Target
 ↓
Hit chance
 ↓
Body Part
 ↓
Damage
```

---

# 75. Combat AI

战斗 AI 不是一个单独算法，而是：

```text
ThinkTree
+
Lord
+
Duty
+
Cover
+
Targeting
+
Weapon
+
Threat
```

共同产生。

---

# 76. Research

核心：

```text
ResearchProjectDef
 ↓
ResearchBench
 ↓
Research
 ↓
Progress
 ↓
Project Complete
 ↓
Unlock
```

研究速度受：

- Intellectual
- Bench
- Research speed
- Room / Facility
- Modifiers

影响。

---

# 77. Trader

交易系统：

```text
Faction
 ↓
TraderKindDef
 ↓
Trader
 ↓
Stock Generation
 ↓
Price
 ↓
Trade
```

---

# 78. Market / Price

价格受到：

- Base Market Value
- Trade Price Factors
- Faction
- Trader Type
- Difficulty / Scenario / Modifiers

影响。

---

# 79. Faction

```text
FactionDef
 ↓
Faction
 ↓
Goodwill
 ↓
Settlement
 ↓
Trader / Raid / Quest
```

---

# 80. Goodwill

1.3 的核心调整：

```text
Current Goodwill
 ↓
Natural Goodwill
```

并加入：

- 最近事件因素
- 时间衰减
- 自然回归

---

# 81. World

World 是地图之外的全局层。

包括：

- WorldGrid
- Tile
- Biome
- Hilliness
- Roads
- Rivers
- Settlement
- Faction
- Caravan
- WorldObject

---

# 82. Biome

Biome 决定：

- 温度
- 降雨
- Plants
- Animals
- Soil
- Resources
- Terrain
- Population / Settlement suitability

---

# 83. Caravan

Caravan 是 Pawn 从局部 Map 进入 World 的桥梁。

```text
Map
 ↓
Caravan
 ↓
World Tile
 ↓
World Movement
 ↓
Temporary Map
 ↓
Return
```

---

# 84. Caravan Food

需要计算：

- Pawn Hunger
- Animal Hunger
- Forage
- Nutrition
- Days Worth
- Pack capacity
- Spoilage

1.6 对 Caravan forage 进行了显著优化。

---

# 85. Temporary Map

Caravan 到达地点后：

```text
World Site
 ↓
Map Generation
 ↓
Temporary Map
 ↓
Incident / Quest
 ↓
Exit
 ↓
Caravan
```

---

# 86. Incident

事件是叙事系统的动态入口。

```text
Storyteller
 ↓
IncidentDef
 ↓
IncidentWorker
 ↓
CanFireNow
 ↓
TryExecute
 ↓
Incident
```

---

# 87. IncidentParms

事件执行参数可包含：

- Points
- Target
- Faction
- PawnKind
- PawnCount
- Trader
- Spawn position
- Quest context

Mod 可通过 IncidentParms 扩展事件上下文。

---

# 88. Storyteller

主要 Core Storyteller：

- Cassandra
- Phoebe
- Randy

作用：

```text
Story State
 ↓
Incident Selection
 ↓
Threat / Event
```

---

# 89. Threat Points

关键概念：

```text
Colony Wealth
+
Population
+
Equipment
+
Time
+
Storyteller
+
Difficulty
 ↓
Threat Points
```

并非所有事件都简单等于 Threat Points。

---

# 90. Raid

典型：

- Assault
- Sappers
- Siege
- Drop Pod
- Breach

1.3：

> Breach Raid 成为 Core 中新的重要攻城模型。

---

# 91. Lord Raid AI

Raid 通常不是：

`所有 Pawn 同时 Attack`

而是：

```text
Lord
 ↓
LordJob_Raid
 ↓
LordToil
 ↓
Group decisions
 ↓
Pawn Jobs
```

---

# 92. Prisoner

核心：

```text
Downed Enemy
 ↓
Capture
 ↓
Prisoner
 ↓
Warden
 ↓
Recruit / Release / Execute
```

---

# 93. Social

Social 由多个层次组成：

```text
Interaction
 ↓
Opinion
 ↓
Relation
 ↓
Thought
 ↓
Mood
```

---

# 94. Relation

包括：

- Friend
- Rival
- Parent
- Child
- Spouse
- Lover
- Enemy

关系会影响：

- Social interactions
- Mood
- Recruitment
- Events
- Death responses

---

# 95. Death / Corpse

死亡后 Pawn 通常进入 Corpse 状态。

```text
Pawn
 ↓
Death
 ↓
Corpse
 ↓
Rot
 ↓
Burial / Cremation / Butcher
```

---

# 96. Filth

Filth 是地图上的动态环境实体。

来源：

- Pawn
- Animal
- Combat
- Blood
- Food
- Fire
- Environment

会影响：

- Cleanliness
- Room
- Beauty
- Some work / health-related calculations

---

# 97. Art

Art 是：

```text
Thing
+
Quality
+
Art generation
+
Beauty
+
Description
```

艺术描述系统还会与：

- Tale
- RulePack
- Grammar

连接。

---

# 98. Tale / History

故事记录系统：

```text
Event
 ↓
Tale
 ↓
History
 ↓
Art / Pawn / Colony narrative
```

它让 RimWorld 不只是计算数值，而能记住事件。

---

# 99. Letter / Message / BattleLog

反馈系统包括：

- Letter
- Message
- BattleLog
- Mote / Fleck
- Tooltip

这些负责把系统结果反馈给玩家。

---

# 100. Quest

Quest 是事件系统之上的结构化任务系统。

```text
Quest
├── QuestNode
├── QuestPart
├── QuestState
├── QuestSignal
├── QuestReward
└── QuestGen
```

---

# 101. Quest Signal

任务可以通过信号驱动状态变化：

```text
Signal
 ↓
QuestPart
 ↓
State transition
 ↓
Reward / Threat / Event
```

---

# 102. Transport Ship / Shuttle 基础层

1.3 对 Shuttle 相关底层架构进行了泛化：

> Shuttle 被重构成通用 Transport Ship，受 Quest、UI、Lord 等系统控制。

这对 Mod 很重要，因为它把“某一个载具”抽象成：

`Transport Ship + Job/Quest control`

**版本：1.3**

---

# 103. Save / Scribe

存档系统属于必须单独掌握的开发机制。

常见：

```text
ExposeData()
Scribe_Values
Scribe_Defs
Scribe_References
Scribe_Collections
```

## 103.1 保存对象

```text
Game
Map
World
Pawn
Thing
Component
Quest
History
Mod data
```

---

# 104. Save Compatibility

Mod 存档兼容必须考虑：

- Def 被删除
- Def 重命名
- 字段变化
- 类型变化
- Reference 失效
- Collection 变化
- Version migration

推荐：

```text
ExposeData()
+
Versioned Migration
+
Null-safe loading
```

---

# 105. Rendering

核心：

```text
GraphicData
 ↓
Graphic
 ↓
Material
 ↓
Mesh
 ↓
Drawer / Renderer
```

Pawn：

```text
Pawn
 ↓
PawnRenderer
 ↓
Graphic / Node
 ↓
Draw
```

---

# 106. 1.5 Rendering 重构

1.5：

- Pawn Rendering parent/child node system
- multithreaded pawn rendering
- Dynamic Thing Culling

因此 1.5 是渲染 Mod 的重要兼容分界。

---

# 107. Fleck / Mote

1.3：

> 大多数 Mote 改为独立 threaded Fleck 系统。

开发影响：

- 粒子不应假设必须是完整 Thing
- 不要用 Thing 生命周期思维操作所有视觉效果
- 高数量视觉效果可以走轻量渲染路径

---

# 108. UI 架构

主要：

```text
Window
Dialog
MainTab
Gizmo
Command
Designator
FloatMenu
InspectPane
Widgets
Listing
Tooltip
```

---

# 109. Gizmo

Gizmo 是：

> Pawn、Building、Map Object 等对象的快速交互命令入口。

典型：

```text
Thing
 ↓
Gizmo
 ↓
Command
 ↓
Action
```

---

# 110. Designator

Designator 更偏地图操作：

```text
Map
 ↓
Designator
 ↓
Designation
 ↓
Job
```

---

# 111. Float Menu

典型右键交互：

```text
Target
 ↓
FloatMenuMaker
 ↓
FloatMenuOption
 ↓
Action
```

---

# 112. Widget / Window

UI 大多以立即模式布局方式组织。

需要考虑：

- Event
- Rect
- Scroll
- Mouse
- Keyboard
- Tooltip
- Text
- Input
- UI Scale

---

# 113. Def Injection / XML Patch

Mod 可以通过：

- XML Def injection
- PatchOperation
- DefModExtension
- C# code
- Harmony

扩展 Core。

---

# 114. XML Patch

适合：

- 添加/修改数据
- 调整已有 Def
- 兼容多个 Mod
- 减少代码耦合

优点：

- 灵活
- 通常兼容性较好

限制：

- 复杂逻辑有限
- 依赖 Def 的结构

---

# 115. Harmony

Harmony 适用于：

> 原版没有公开扩展入口，但必须修改已有运行逻辑。

风险：

- Prefix/Postfix
- Transpiler
- 方法签名改变
- 多 Mod Patch 冲突
- 执行顺序
- 性能
- 维护成本

原则：

> 能用 Def / Comp / Worker / Component 解决，不优先 Harmony。

---

# 116. Mod Load Order

Mod 运行通常涉及：

```text
Core
 ↓
DLC
 ↓
Framework
 ↓
Content
 ↓
Patch
 ↓
Compatibility
```

实际加载顺序必须以 Mod 元数据与当前游戏版本为准。

---

# 117. Language / Translation

RimWorld 的本地化体系也是机制的一部分。

涉及：

- TranslationDef
- keyed translation
- DefInjected
- Grammar / RulePack
- LanguageWorker

## 开发原则

绝对不要把 UI、Def 名称、描述直接硬编码成单一语言。

推荐：

```text
Def
 ↓
Translation Key
 ↓
Language Data
 ↓
UI
```

---

# 118. RulePack / Grammar

文本生成不仅是静态字符串。

```text
RulePack
 ↓
Grammar Rules
 ↓
Symbols
 ↓
Generated Text
```

用于：

- Art descriptions
- Name generation
- Incident text
- Tales
- Quest text
- Relationships

---

# 119. Mod Compatibility 设计原则

## 优先级

```text
Def
 ↓
DefModExtension
 ↓
Comp
 ↓
Worker
 ↓
Component
 ↓
Harmony
```

越靠后，维护风险通常越高。

---

# 120. 机制生命周期模型

对于绝大多数系统，可以统一理解成：

```text
Definition
 ↓
Generation
 ↓
Initialization
 ↓
Spawn
 ↓
Runtime
 ↓
Interaction
 ↓
State Change
 ↓
Tick / Event
 ↓
Save
 ↓
Load
 ↓
Despawn / Death
```

---

# 121. Tick 机制

Pawn、Thing、Map、World、Component 不一定共享完全相同 Tick 频率。

开发时必须区分：

- Normal Tick
- Rare Tick
- Long Tick
- Interval update
- Event-driven update
- Background update

1.6 更加强调：

> **不要把昂贵逻辑挂在每 Tick。**

---

# 122. Performance

## 122.1 主要瓶颈

常见：

- Pathfinding
- Pawn AI
- Jobs
- Hediff
- Need
- Search
- Room Stats
- Beauty
- Lighting
- Rendering
- Hauling
- Animal Pens
- Caravan
- Alerts

## 122.2 1.6

官方重点优化：

- Multithreaded Pathfinding
- Batched Pathfinding
- Multithreaded Lighting
- Caravan forage
- Egg laying
- Alerts
- Hauling
- Animal Pen calculations
- Memory leaks
- Startup

---

# 123. Caching 原则

不要：

```text
Every Tick
 └── Search all Things
```

推荐：

```text
Event
 ↓
Invalidate Cache
 ↓
Recalculate only when needed
```

使用：

- Lister
- Manager
- Cached Stat
- Rare Tick
- Event-driven updates

---

# 124. 1.5 / 1.6 对 Mod API 的重要变化

## 1.5

- Pawn renderer node system
- Rendering multithread
- Dynamic culling
- ThingComp affects stats
- virtual relationship records
- Asset hot reload
- 更多可配置 XML
- 多项性能优化

## 1.6

- Pathfinding multithread
- Pathfinding batching
- Lighting multithread
- Variable Tick Rate
- 更强 Terrain Layer
- Plan system
- Designator draw styles
- Caravan 优化

---

# 125. Dev Mode

Dev Mode 本身也是开发机制。

常见：

- Spawn Thing
- Spawn Pawn
- Add Hediff
- Apply Damage
- Set Faction
- Set Terrain
- Fog
- Glow
- World generation
- Incident debug
- Threat points
- Music debugger
- Map tools

1.3、1.5 都明显增加了开发工具。

---

# 126. Debug / Diagnostics

开发者应关注：

```text
Log
Warning
Error
Exception
Stack Trace
Dev Tool
Debug Action
```

Mod 不应通过：

```text
catch { }
```

吞掉异常。

---

# 127. 版本化 Mod

1.3 官方曾明确支持：

> 一个 Mod 可以同时支持多个游戏版本，可以把不同版本文件放进不同目录，也可以共享相同文件。

推荐结构：

```text
About/
Assemblies/
1.3/
1.4/
1.5/
1.6/
Common/
```

具体实现应根据当前 Mod 复杂度决定。

---

# 128. Core 机制全景图

```text
                         ┌───────────────┐
                         │ Storyteller   │
                         └───────┬───────┘
                                 ↓
                         ┌───────────────┐
                         │   Incident    │
                         └───────┬───────┘
                                 ↓
┌──────────────┐        ┌───────────────┐        ┌──────────────┐
│    World     │───────→│      Map      │←───────│   Faction    │
└──────┬───────┘        └───────┬───────┘        └──────────────┘
       ↓                         ↓
   Caravan                     Pawn
                                 │
         ┌───────────────────────┼─────────────────────────┐
         ↓                       ↓                         ↓
      Health                   Needs                     Work
         ↓                       ↓                         ↓
      Hediff                  Thought                     Job
         ↓                       ↓                         ↓
     Capacity                  Mood                   JobDriver
         ↓                       ↓                         ↓
      Stat                  MentalState                   Toil
         └───────────────────────┼─────────────────────────┘
                                 ↓
                             Interaction
                                 ↓
                              Story
```

---

# 129. 全机制分类索引

| 一级系统 | 核心对象 / 机制 |
|---|---|
| Game | Game、TickManager、Time |
| World | World、Tile、Biome、WorldObject |
| Map | Map、Region、Room、Zone、Area |
| Pawn | Pawn、PawnKind、Generation |
| Skill | SkillDef、SkillRecord、Passion |
| Trait | TraitDef、Trait |
| Backstory | BackstoryDef |
| Need | NeedDef、Need |
| Thought | ThoughtDef、Thought |
| Mood | Mood、Mental Break |
| Health | Body、BodyPart、Hediff |
| Capacity | Capacities |
| Stat | StatDef、StatWorker、StatPart |
| AI | ThinkTree、ThinkNode、JobGiver |
| Work | WorkType、WorkGiver |
| Job | Job、JobDriver、Toil |
| Reservation | ReservationManager |
| Duty | Duty |
| Lord | Lord、LordJob、LordToil |
| Thing | Thing、ThingWithComps |
| Component | GameComponent、MapComponent、WorldComponent、ThingComp |
| Def | ThingDef、RecipeDef、JobDef、HediffDef 等 |
| Material | Stuff、Quality、Stack |
| Building | Building、Blueprint、Frame |
| Terrain | TerrainDef、TerrainGrid |
| Room | Room、RoomRole、RoomStats |
| Temperature | RoomTemp、Heat、Cool |
| Power | PowerNet、CompPower |
| Light | GlowGrid、Lighting |
| Fire | Fire、FireUtility |
| Explosion | Explosion、Damage |
| Plant | Plant、Growth |
| Agriculture | GrowingZone |
| Animal | Taming、Training、Pen、Breeding |
| Food | Nutrition、Meal、Food Poisoning |
| Production | Recipe、Bill、Ingredient |
| Storage | Stockpile、Shelf、ThingFilter |
| Combat | Verb、Projectile、Melee |
| Damage | DamageDef、DamageInfo、DamageWorker |
| Armor | Armor、Penetration |
| Cover | Cover |
| World Travel | Caravan、World Movement |
| Faction | Faction、Goodwill |
| Trade | Trader、Market |
| Event | Incident、IncidentWorker |
| Story | Storyteller、Tale |
| Quest | Quest、QuestNode、QuestPart |
| Prison | Prisoner、Warden |
| Social | Interaction、Relation、Opinion |
| Death | Corpse、Rot、Burial |
| Rendering | Graphic、Material、Mesh、Renderer |
| Visual FX | Fleck、Mote、Overlay |
| UI | Window、Gizmo、Designator、FloatMenu |
| Text | Translation、RulePack、Grammar |
| Save | Scribe、ExposeData |
| Mod | Def Injection、Patch、Harmony |
| Dev | DevMode、Debug Actions |
| Performance | Cache、Rare Tick、Multithreading、Culling |

---

# 130. 一级机制首次形成版本总表

> 早期机制无法可靠定位到单一 Alpha 小版本时，以 `Alpha` 表示。

| 一级机制 | 首次形成 / 首次公开节点 | 关键重构 |
|---|---|---|
| Pawn | Alpha | 持续 |
| Skill | Alpha | 持续 |
| Passion | Alpha | 持续 |
| Trait | Alpha | 持续 |
| Backstory | Alpha | 持续 |
| Need | Alpha | 持续 |
| Mood | Alpha | 持续 |
| Thought | Alpha | 持续 |
| Health | Alpha | 持续 |
| Hediff | Alpha | 持续 |
| Work | Alpha | 持续 |
| Job | Alpha | 持续 |
| Thing | Alpha | 持续 |
| Def | Alpha | 持续 |
| ThingComp | Alpha | 持续 |
| Stat | Alpha | 持续 |
| Capacity | Alpha | 持续 |
| Building | Alpha | 持续 |
| Room | Alpha | 持续 |
| Region | Alpha | 持续 |
| Zone | Alpha | 持续 |
| Temperature | Alpha | 持续 |
| Power | Alpha | 持续 |
| Plant | Alpha | 持续 |
| Animal | Alpha | **1.3 大改** |
| Pen | **1.3** | 持续 |
| Combat | Alpha | 持续 |
| Breach Raid | **1.3** | 持续 |
| Goodwill model | Alpha | **1.3 重构** |
| Quest | Alpha / 后期 Alpha-Beta 逐步成型 | 持续 |
| Roads / Rivers | **A17** | 持续 |
| World Sites | **A17** | 持续 |
| Deep Drilling | **A15** | 持续 |
| Books | **1.5** | 持续 |
| Crawling | **1.5** | 持续 |
| Pawn Renderer Node System | **1.5** | 持续 |
| Virtual Relationship Records | **1.5** | 持续 |
| Plan | **1.6** | 当前 |
| Terrain Layers | **1.6** | 当前 |
| Multithreaded Pathfinding | **1.6** | 当前 |
| Multithreaded Lighting | **1.6** | 当前 |
| Variable Tick Rate | **1.6** | 当前 |

---

# 131. 版本演化对 Mod 开发的意义

## Alpha

特点：

> 玩法与架构同时快速变化。

Mod 原则：

- 尽量不要锁定早期内部实现
- 依赖 Def 比硬编码稳定

## B18 / B19

特点：

> Core 接近正式 API。

Mod 原则：

- 开始建立长期兼容结构
- 关注 Save Compatibility

## 1.0

特点：

> 正式基线。

Mod 原则：

- 开始形成真正长期维护项目结构。

## 1.1–1.2

特点：

> DLC 生态与 Core 分层开始明显。

Mod 原则：

- 必须区分 Core 与 DLC availability。

## 1.3

特点：

> Animal / Raid / UI / Rendering 重要变化。

Mod 原则：

- 检查 Animal、Raid、Fleck、Transport API。

## 1.4

特点：

> Biotech 同期，Core 底层承担更多通用能力。

Mod 原则：

- 不要把 DLC 类型直接当成 Core 必有。

## 1.5

特点：

> Rendering / Save / UI / Performance 重大变化。

Mod 原则：

- 重点检查 Renderer、Scribe、ThingComp、Stat、Pawn。

## 1.6

特点：

> **性能架构重构。**

Mod 原则：

- 线程安全
- 避免每 Tick 重计算
- 不依赖旧 Pathfinding 假设
- 不假设 Rendering 在主线程独占
- 正确处理后台 Tick / Variable Tick

---

# 132. 开发者视角：最稳定的扩展入口

按通常推荐顺序：

```text
1. Def
2. DefModExtension
3. ThingComp / HediffComp
4. Worker
5. GameComponent / MapComponent / WorldComponent
6. Job / JobDriver
7. Gizmo / Designator / Window
8. XML Patch
9. Harmony
```

并非绝对顺序，但一般遵循：

> **优先利用原版已经设计好的扩展点。**

---

# 133. 开发者视角：高风险区域

## 高风险

- Harmony Transpiler
- Pathfinding
- Rendering
- Tick
- Save
- Def replacement
- Global static state
- Reflection
- UI 全局 Hook
- 全地图扫描
- 大量 Pawn 每 Tick 计算

## 中风险

- JobDriver
- ThinkTree
- Incident
- Quest
- StatPart
- HediffComp

## 相对低风险

- Def
- DefModExtension
- ThingComp
- MapComponent
- GameComponent
- XML Patch

---

# 134. 1.6 Mod 必须避免的错误假设

### 错误

> “每个对象每 Tick 都会正常调用我的逻辑。”

### 正确

> Tick 频率、后台更新、缓存和多线程都可能改变执行时机。

---

### 错误

> “所有视觉对象都是 Thing。”

### 正确

> 1.3 后 Fleck 与 Thing 的边界已经明确。

---

### 错误

> “所有 Pawn 数据都直接来自 Pawn 字段。”

### 正确

> 大量实际能力通过 Health、Stat、Capacity、Need、Thought、Comp、Apparel、Equipment 等派生。

---

### 错误

> “修改一个 Def 就只影响这个对象。”

### 正确

> Def 往往被 Recipe、Trader、AI、Stat、UI、生成器和其他 Def 间接引用。

---

# 135. 推荐的 Mod 架构

```text
Mod
├── About
├── Assemblies
│   └── Core
│       ├── Domain
│       ├── Components
│       ├── Systems
│       ├── Workers
│       ├── Jobs
│       ├── AI
│       ├── Integration
│       ├── UI
│       └── Patches
│
├── Common
│   ├── Defs
│   ├── Textures
│   ├── Sounds
│   ├── Languages
│   └── Patches
│
├── 1.6
│   ├── Defs
│   ├── Patches
│   └── Compatibility
│
└── Documentation
    ├── Architecture
    ├── VanillaReference
    └── Changelog
```

---

# 136. 原版机制知识库建议结构

如果把本文件进一步数据库化：

```text
VanillaMechanics/
├── 00_Index.md
├── 01_Versions.md
├── 02_Game/
├── 03_Pawn/
├── 04_Health/
├── 05_Stats/
├── 06_Needs/
├── 07_AI/
├── 08_Work/
├── 09_Job/
├── 10_Thing/
├── 11_Component/
├── 12_Map/
├── 13_Terrain/
├── 14_Room/
├── 15_Environment/
├── 16_Power/
├── 17_Plant/
├── 18_Animal/
├── 19_Production/
├── 20_Combat/
├── 21_Faction/
├── 22_World/
├── 23_Caravan/
├── 24_Incident/
├── 25_Storyteller/
├── 26_Quest/
├── 27_Social/
├── 28_Prison/
├── 29_Rendering/
├── 30_UI/
├── 31_Save/
├── 32_Localization/
├── 33_Modding/
├── 34_Performance/
└── 99_VersionHistory/
```

---

# 137. 单机制标准模板

每个机制应该最终独立为：

```text
# 机制名称

## 中文名称
## 英文 / Core 名称

## 首次版本
## 重大改版
## 当前版本状态

## 设计目的

## 核心对象

## 生命周期

## 数据来源

## Runtime 状态

## Tick / Event

## 依赖

## 被依赖

## UI

## Rendering

## Save

## Multiplayer / Threading 风险
（如适用）

## Mod 扩展点

## 推荐扩展方式

## 不推荐方式

## 版本变化

### Alpha
### B18
### B19
### 1.0
### 1.1
### 1.2
### 1.3
### 1.4
### 1.5
### 1.6
```

---

# 138. 版本变更记录：维护规则

## 每次 RimWorld 更新后

至少检查：

```text
Defs
Jobs
WorkGivers
ThinkTrees
Stats
Hediffs
Damage
Rendering
UI
Save
Components
Tick
Pathfinding
World
Quest
Incident
```

## 变更等级

| 等级 | 定义 |
|---|---|
| V0 | 仅文本/数值 |
| V1 | 数据字段变化 |
| V2 | 生命周期变化 |
| V3 | API/对象结构变化 |
| V4 | 架构变化 |
| V5 | 线程/运行模型变化 |

1.5 的 Pawn Renderer 重构属于接近 **V4**。

1.6 的 Pathfinding + Lighting + Tick 性能体系属于 **V4/V5** 类重要变化。

---

# 139. 本 v2.0 相对于 v1.0 的主要补充

v1.0 偏向“玩法总纲”。

v2.0 已扩展为：

- 全历史版本时间线
- Alpha → B18/B19 → 1.0 → 1.6
- Pawn 生命周期
- Stats
- Capacities
- HediffComp
- ThingComp
- DefModExtension
- ThinkTree
- JobGiver
- Duty
- Lord
- MentalState
- Reservation
- Target
- Region
- Lister
- Spawn / Despawn
- Designation
- Area
- Pathfinding
- DamageInfo
- DamageWorker
- Verb
- Projectile
- Recipe / Bill / Ingredient
- Trader / Market
- QuestNode / QuestPart / Signal
- Tale / History
- Letter / Message / BattleLog
- Rendering
- Fleck / Mote
- UI
- Scribe / Save
- Translation / RulePack
- XML Patch
- Harmony
- Mod Load Order
- Threading
- Caching
- 1.5 Rendering / Save 技术变化
- 1.6 Pathfinding / Lighting / Tick 技术变化

---

# 140. 准确性边界

本文件已经从“玩法百科”提升为“开发者级 Core 机制参考”，但仍需明确：

> **它不是 RimWorld DLL 的逐 Class、逐 Method、逐 Def、逐 XML 字段数据库。**

原因：

1. Core 的 Def 数量和字段非常庞大。
2. ThingDef 等单一类型本身就存在数百个字段/子字段。
3. 许多早期 Alpha 机制没有可靠的单版本公开历史。
4. 后续补丁大量做微调，不能把“每一条 commit”压缩成几个概念而宣称已经逐项复刻。
5. Wiki 本身也仍在持续维护版本页面。

因此，本版本对“全部机制”的定义是：

> **覆盖 Core 的主要机制域、底层运行模型、开发扩展入口、关键对象体系以及从早期公开版本到当前 1.6 的重要机制演化。**

---

# 141. 官方与资料依据

## 官方来源

### RimWorld Steam
RimWorld 官方商店资料：

- 正式发布：2018-10-17
- Ludeon Studios
- Core 游戏概述

### Ludeon 1.6 官方更新公告
重点确认：

- 1.6 免费 Core 更新
- 多线程 Pathfinding
- Batched Pathfinding
- Multithreaded Lighting
- Caravan 优化
- Hauling 优化
- Animal Pen 优化
- Plan
- Designator Draw Styles
- 地图生成重构
- 启动时间优化

### Ludeon 1.5 官方更新公告
重点确认：

- Books
- Bookcases
- Crawling
- Game Over Wanderers
- Search
- Prisoner / Health UI
- Clean Room
- Mine Vein
- 多项 Rendering / Save / Technical 更新

## RimWorld Wiki

用于：

- Version History
- Alpha 15
- Alpha 17
- Beta 18
- 1.0
- 1.2
- 1.3
- 1.4
- 1.5
- 1.6
- Modding Tutorials
- ThingDef
- ThingComp
- XML Def
- Development Mode

---

# 142. 核心引用索引

以下来源用于核对本文档的版本与架构结论：

1. RimWorld Version History  
   `https://rimworldwiki.com/wiki/Version_history`

2. RimWorld 1.6 官方发布与更新说明  
   `Ludeon / Steam Community Announcements`

3. RimWorld 1.5 官方更新说明  
   `Ludeon / Steam Community Announcements`

4. RimWorld 1.3.3066  
   `https://rimworldwiki.com/wiki/Version/1.3.3066`

5. RimWorld Alpha 17  
   `https://rimworldwiki.com/wiki/Version/0.17.1546`

6. RimWorld Alpha 15  
   `https://rimworldwiki.com/wiki/Version/0.15.1279`

7. RimWorld Beta 18  
   `https://rimworldwiki.com/wiki/Version/0.18.1722`

8. RimWorld 1.0  
   `https://rimworldwiki.com/wiki/Version/1.0.0`

9. ThingComp Modding Tutorial  
   `https://rimworldwiki.com/wiki/Modding_Tutorials/ThingComp`

10. ThingDef Modding Tutorial  
    `https://rimworldwiki.com/wiki/Modding_Tutorials/ThingDef`

11. XML Defs  
    `https://rimworldwiki.com/wiki/Modding_Tutorials/XML_Defs`

---

# 143. 最终开发者结论

RimWorld 不应被理解为：

```text
一堆建筑
+
一堆 Pawn
+
一套战斗
```

而应理解为：

```text
                 Definition Layer
                       ↓
                Generation Layer
                       ↓
              Entity / Pawn Layer
                       ↓
     ┌──────────┬──────┼──────┬─────────┐
     ↓          ↓      ↓      ↓         ↓
    AI        Health  Stats   Work     World
     ↓          ↓      ↓      ↓         ↓
    Job       Hediff  Cap    JobDriver Caravan
     └──────────┴──────┼──────┴─────────┘
                       ↓
                 Event / Story
                       ↓
                  Save / UI
                       ↓
              Rendering / Performance
```

真正优秀的 RimWorld Mod，不是“把新功能塞进游戏”，而是：

> **找到原版机制最自然的扩展层，在不破坏其生命周期、保存、Tick、AI、渲染和数据模型的情况下加入新行为。**

对于 1.6，尤其要把：

**Def → Component → Job → AI → Stat → Health → Map → Save → Rendering → Performance**

看成一个整体，而不是互相孤立的功能模块。

---

# 144. v2.0 状态

**文档版本：v2.0**

**基准：RimWorld 1.6.4633**

**状态：开发者级 Core 机制总参考**

**适用：**
- RimWorld 1.6 Mod 开发
- AI Coding Skills
- Mod 架构设计
- Vanilla 机制分析
- 兼容性设计
- 版本升级
- Code Review
- 原版机制检索

**下一层建议：**

将本文件继续拆分成：

```text
VanillaReference/
├── Core_Architecture.md
├── Core_Def_Reference.md
├── Core_API_Reference.md
├── Core_Mechanics_Reference.md
├── Core_AI_Reference.md
├── Core_Save_Reference.md
├── Core_UI_Reference.md
├── Core_Rendering_Reference.md
├── Core_Performance_Reference.md
└── Version_History_Alpha_to_1.6.md
```

从而形成：

**RimWorld 1.6 Vanilla Developer Knowledge Base**

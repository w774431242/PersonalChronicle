# Pawn 自驱力框架 —— 原版需求分级大全与 AutonomyTier 映射

> 本文是 `Pawn自驱力框架_需求分级与排队设计.md` 的**数据支撑文档**。
> 产出：**①** 原版全部需求的原生级别清单（逐个带阈值与 `路径:行号`）；**②** 把原生级别映射到框架五档 `AutonomyTier`；**③** 据此对 SDD 内容的增补（§6）。
> 推导原则：原版程序集 + 原版 Def XML 为唯一事实来源。凡属**我的映射建议**而非原版事实，均以 ⚠️ 标注。

---

## 0. 结论摘要

1. 原版启用中的需求共 **17 个**（Core 10 / Royalty 1 / Ideology 1 / Biotech 5）。Anomaly 与 Odyssey **无** `NeedDefs` 目录。
2. 其中 **11 个有 `CurCategory` 分级枚举**，**1 个（Mood）用翻译键做分级**，**5 个只有 bool 二值**（无枚举）。
3. **★ 最重要的发现**：原版分级阈值有**三种来源**——硬编码常数、Def 派生（per-race）、**运行期计算（per-pawn）**。这**推翻了**上一版设计里「`tierFromNeed` = 静态阈值表」的假设，见 §3 F1 与 §6.2 的修正。
4. `NeedDef.major` **不是** AI 优先级——它只用于 UI 分组与场景部件（§2.4）。**不可当作分级依据直用**，易误用。

---

## 1. 原版需求总表（17 个启用 + 1 个有类无 Def）

来源：`Data/{Core,Royalty,Ideology,Biotech}/Defs/NeedDefs/Needs.xml`

| NeedDef | needClass | `listPriority` | `major` | 来源 | 原生分级 | 档数 |
|---|---|---|---|---|---|---|
| `MechEnergy` | `Need_MechEnergy` | 10000 | ✔ | Biotech | **无枚举**（bool ×2） | — |
| `Mood` | `Need_Mood` | 1000 | ✔ | Core | **翻译键**（`MoodString`） | 6 |
| `Food` | `Need_Food` | 800 | ✔ | Core | `HungerCategory` | 4 |
| `Rest` | `Need_Rest` | 700 | ✔ | Core | `RestCategory` | 4 |
| `Deathrest` | `Need_Deathrest` | 600 | ✔ | Biotech | **无枚举**（bool + 阈值 0.1） | — |
| `Play` | `Need_Play` | 550 | ✔ | Biotech | `PlayCategory` | 6 |
| `Joy` | `Need_Joy` | 500 | ✔ | Core | `JoyCategory` | 6 |
| `Learning` | `Need_Learning` | 500 | ✔ | Biotech | `LearningCategory` | 6 |
| `Beauty` | `Need_Beauty` | 300 | ✘ | Core | `BeautyCategory` | 7 |
| `Comfort` | `Need_Comfort` | 200 | ✘ | Core | `ComfortCategory` | 6 |
| `DrugDesire` | `Need_Chemical_Any` | 150 | ✘ | Core | `DrugDesireCategory` | 3 |
| `Outdoors` | `Need_Outdoors` | 100 | ✘ | Core | `OutdoorsCategory` | 6 |
| `Indoors` | `Need_Indoors` | 100 | ✘ | Core | `IndoorsCategory` | 6 |
| `RoomSize` | `Need_RoomSize` | 100 | ✘ | Core | `RoomSizeCategory` | 4 |
| `KillThirst` | `Need_KillThirst` | — | ✘ | Biotech | **无** | — |
| `Suppression` | `Need_Suppression` | — | ✘ | Ideology | **无枚举**（bool `IsHigh`） | — |
| `Authority` | `Need_Authority` | — | ✘ | Royalty | `AuthorityCategory` | 5（**dummy**） |
| ~~`Sadism`~~ | `Need_Sadism` | — | — | — | 有类**无 Def**，未启用 | — |

**关于 `Sadism`**：`RimWorld/Need_Sadism.cs` 存在（有 `IsHigh` / `IsCritical`），但全 `Data/**/*.xml` 检索**无对应 `NeedDef`** ⇒ 未启用，本文排除。

**关于 `listPriority`**：仅用于需求卡 UI 排序（`NeedsCardUtility`），不参与 AI。

---

## 2. 原生分级明细

### 2.1 有 `CurCategory` 枚举的需求（11 个）

阈值一律对 `CurLevel`（0–1）判定。**注意判定方向**，§3 F2 会展开。

#### Food — `HungerCategory`（4 档）· `Need_Food.cs:69-87`
| 级别 | 判定 | 阈值来源 |
|---|---|---|
| `Starving` | `CurLevelPercentage <= 0f` | 常数 |
| `UrgentlyHungry` | `< PercentageThreshUrgentlyHungry` | **Def 派生**：`RaceProps.FoodLevelPercentageWantEat * 0.4f`（`:24-29`） |
| `Hungry` | `< PercentageThreshHungry` | **Def 派生**：`* 0.8f`（`:34-39`） |
| `Fed` | 其余 | — |

#### Rest — `RestCategory`（4 档）· `Need_Rest.cs:14-32`
`Exhausted <0.01` → `VeryTired <0.14` → `Tired <0.28` → `Rested`（全常数）

#### Joy — `JoyCategory`（6 档）· `Need_Joy.cs:14-40`
`Empty <0.01` → `VeryLow <0.15` → `Low <0.3` → `Satisfied <0.7` → `High <0.85` → `Extreme`

#### Learning — `LearningCategory`（6 档）· `Need_Learning.cs:34-59`
同 Joy 的阈值（`<0.01 / 0.15 / 0.3 / 0.7 / 0.85`）。

#### Play — `PlayCategory`（6 档）· `Need_Play.cs:23-48`
同 Joy 的阈值。

#### Beauty — `BeautyCategory`（7 档）· `Need_Beauty.cs:39-68` ⚠️ **方向相反**
`Beautiful >0.99` → `VeryPretty >0.85` → `Pretty >0.65` → `Neutral >0.35` → `Ugly >0.15` → `VeryUgly >0.01` → `Hideous`
（用 `>` 判定；**值越高越美**，值越低越糟）

#### Comfort — `ComfortCategory`（6 档）· `Need_Comfort.cs:27-52`
`Uncomfortable <0.1` → `Normal <0.6` → `Comfortable <0.7` → `VeryComfortable <0.8` → `ExtremelyComfortable <0.9` → `LuxuriantlyComfortable`

#### Chemical（DrugDesire）— `DrugDesireCategory`（3 档）· `Need_Chemical.cs:22-35` ⚠️ **方向相反**
`Satisfied >0.1` → `Desire >0.01` → `Withdrawal`
（**值越高越满足**，与 Food 相反）

#### Outdoors — `OutdoorsCategory`（6 档）· `Need_Outdoors.cs:27-52` ⚠️ **方向相反**
`Free >0.8` → `NeedFreshAir >0.6` → `CabinFeverLight >0.4` → `CabinFeverSevere >=0.2` → `Trapped >0.05` → `Entombed`

#### Indoors — `IndoorsCategory`（6 档）· `Need_Indoors.cs:37-62` ⚠️ **方向相反**
阈值取自静态数组 `Need_Indoors.cs:137`：`Thresholds = { 0.8f, 0.6f, 0.4f, 0.2f, 0.05f }`
`ComfortablyIndoors >0.8` → `JustOutdoors >0.6` → `Outdoors >0.4` → `LongOutdoors >0.2` → `VeryLongOutdoors >0.05` → `BrutalOutdoors`

#### RoomSize — `RoomSizeCategory`（4 档）· `Need_RoomSize.cs:22-39`
`VeryCramped <0.01` → `Cramped <0.3` → `Normal <0.7` → `Spacious`

#### Authority — `AuthorityCategory`（5 档）· `Need_Authority.cs:30-56`
`Gone <0.01` → `Weak <0.15` → `Uncertain <0.3` → `Strong (>0.7 且 <0.85)` → `Total >=0.85` → 其余 `Normal`
⚠️ 该 need 在 `Royalty/Defs/NeedDefs/Needs.xml:3` 自述为 **「temporary dummy need to suppress errors when loading older saves」** ⇒ 兼容性占位，**不应作为驱动来源**。

### 2.2 Mood —— 用翻译键分级，且阈值 per-pawn 动态

`RimWorld/Need_Mood.cs:34-64`，`MoodString` 返回**翻译键**（不是枚举）：

| 级别（翻译键） | 判定 | 阈值性质 |
|---|---|---|
| `Mood_AboutToBreak` | `< BreakThresholdExtreme` | **★ 运行期计算，per-pawn** |
| `Mood_OnEdge` | `< BreakThresholdExtreme + 0.05` | 动态 + 常数 |
| `Mood_Stressed` | `< BreakThresholdMinor` | **★ 运行期计算，per-pawn** |
| `Mood_Neutral` | `< 0.65` | 常数 |
| `Mood_Content` | `< 0.9` | 常数 |
| `Mood_Happy` | 其余 | — |

阈值来自 `pawn.mindState.mentalBreaker.BreakThresholdExtreme` / `BreakThresholdMinor`（`:42`、`:51`）——**随 trait、难度、状态变化，每个 pawn 不同**。

**这条同时佐证 C5**：原版分级对用户展示的字符串**本身就用翻译键**（`"Mood_AboutToBreak".Translate()`），不是自由文本。框架的分级标签应同此例。

### 2.3 无枚举、仅 bool 二值的需求（5 个）

| NeedDef | 可用判定 | 出处 |
|---|---|---|
| `MechEnergy` | `IsSelfShutdown`、`IsLowEnergySelfShutdown` | `Need_MechEnergy.cs:24`、`:34` |
| `Deathrest` | `Deathresting`；提示串用阈值 `0.1`（`(pct - 0.1) / 0.033333335`，`:115-117`） | `Need_Deathrest.cs:12`、`:112-117` |
| `Suppression` | `IsHigh`、`CanBeSuppressedNow` | `Need_Suppression.cs:12`、`:22` |
| `KillThirst` | 未查到分级成员，仅继承 `CurLevel` | `Need_KillThirst.cs` |
| `Sadism` | `IsHigh`、`IsCritical`（**未启用**） | `Need_Sadism.cs:11`、`:21` |

### 2.4 ⚠️ 关于 `NeedDef.major` —— 易误用点

`major` 的实际用途（全量 grep 结果）**只有两处**：

| 用途 | 出处 |
|---|---|
| 需求卡 UI 分组：非 major 的需求单独分区显示 | `NeedsCardUtility.cs:43-45` |
| 场景部件「设置需求初始等级」只列举 major 需求 | `ScenPart_SetNeedLevel.cs:30` |

**⇒ `major` 是 UI / 编辑器层面的「主次」标记，不参与 AI 决策。** 不能把它当作「AI 优先级」直接读用。
本文仅在 §4 的映射规则里把它当作「官方给出的主次划分」作参考，**且只用于限制档位上限，不用于生成档位**。

---

## 3. ★ 从原版分级推出的 5 条硬事实（对框架的约束）

| # | 事实 | 对框架的影响 |
|---|---|---|
| **F1** | 阈值有**三种来源**：硬编码常数 / **Def 派生（per-race，如 Food）** / **运行期计算（per-pawn，如 Mood）** | **推翻**上一版「`tierFromNeed` = 静态阈值表」的假设。`tierFromNeed` 必须三选一支持，见 §6.2 |
| **F2** | 判定**方向不统一**：Food/Rest/Joy 用「值低 = 缺」；Beauty/Chemical/Outdoors/Indoors 用「值高 = 好」 | 映射必须**显式声明 `invert`**，否则 Chemical 的 `Satisfied`（高值）会被误判为最紧急 |
| **F3** | 档数不统一：**3 ~ 7 档** | 框架的 5 档是**归一化目标**，映射必为多对一；须有明确收敛规则（§4 规则） |
| **F4** | Mood 的分级返回**翻译键** | 佐证 C5；框架分级标签也应 `MustTranslate` |
| **F5** | 5 个需求**无枚举只有 bool** | 框架须支持「二值分级」（bool → 档），不能只支持枚举阈值表 |

---

## 4. 原生级别 → AutonomyTier 映射

### 4.1 映射规则（先立规则，再逐条套用）

| 规则 | 内容 |
|---|---|
| **R1** | 该级别会导致**死亡或直接崩溃** → **T0** |
| **R2** | 该级别会触发**显著心情惩罚 / 能力大幅下降** → **T1** |
| **R3** | 该级别只是**明确不满足、令人不适** → **T2** |
| **R4** | **纯改善型**（`major=false` 的需求，或已接近满足） → **T3 / T4** |
| **R5** | **已满足** → **不入队**（避免产生无意义驱动） |
| **R6** | `major=false` 的需求，**最高只能到 T2**（除原版有明确严重后果者，如 Chemical 的 `Withdrawal`） |

> ⚠️ 规则 R1–R6 与下表具体档位均为**我的映射建议**，非原版事实；原版只提供「级别语义」与「阈值」。是否照此收敛需实测。

### 4.2 逐需求映射表

#### Food（per-race 阈值；`major=true`）
| 原生级别 | → Tier | 理由 |
|---|---|---|
| `Starving` | **T0** | 会饿死（R1） |
| `UrgentlyHungry` | **T1** | 严重 malnutrition（R2） |
| `Hungry` | **T2** | R3 |
| `Fed` | 不入队 | R5 |

#### Rest（`major=true`）
| `Exhausted` (<0.01) | **T0** | 会当场昏倒（R1） |
| `VeryTired` (<0.14) | **T1** | R2 |
| `Tired` (<0.28) | **T2** | R3 |
| `Rested` | 不入队 | R5 |

#### Mood（**动态阈值**；`major=true`）
| `Mood_AboutToBreak` | **T0** | 濒临崩溃（R1） |
| `Mood_OnEdge` | **T1** | R2 |
| `Mood_Stressed` | **T2** | R3 |
| `Mood_Neutral` / `Mood_Content` | **T3** | R4 |
| `Mood_Happy` | 不入队 | R5 |

#### Joy / Play / Learning（同为 6 档同阈值；均 `major=true`）
| `Empty` (<0.01) | **T1** | 娱乐/学习不足不致命，但心情惩罚重（R2） |
| `VeryLow` (<0.15) | **T2** | R3 |
| `Low` (<0.3) | **T2** | R3 |
| `Satisfied` (<0.7) | **T3** | R4 |
| `High` (<0.85) | **T4** | R4 |
| `Extreme` | 不入队 | R5 |

#### MechEnergy（bool；`major=true`）
| `IsSelfShutdown` | **T0** | 机械体停机 = 功能丧失（R1） |
| `IsLowEnergySelfShutdown` | **T1** | R2 |
| 其余低电量 | **T2 / T3**（按百分比） | R3/R4 |

#### Deathrest（bool + 阈值 0.1；`major=true`）
| `CurLevelPercentage` 接近/低于 0.1（进入 exhaustion） | **T1** | ⚠️ 阈值 0.1 取自提示串计算（`:115-117`），**未完整验证 exhaustion 的完整判定链** |
| 明显充裕 | 不入队 | R5 |

#### Chemical / DrugDesire（**方向相反**；`major=false`）
| `Withdrawal` (≤0.01) | **T1** | 戒断造成严重心情惩罚 → 破例高于 R6 上限（R2） |
| `Desire` (≤0.1) | **T2** | R3 |
| `Satisfied` (>0.1) | 不入队 | R5 |

#### Outdoors（**方向相反**；`major=false`）⚠️ 受 R6 限档
| `Entombed` (≤0.05) | **T2** |
| `Trapped` (≤0.2) | **T3** |
| `CabinFeverSevere` (<0.4) | **T3** |
| `CabinFeverLight` (<0.6) | **T4** |
| `NeedFreshAir` (<0.8) | **T4** |
| `Free` (>0.8) | 不入队 |

#### Indoors（**方向相反**；`major=false`）⚠️ 受 R6 限档
| `BrutalOutdoors` (≤0.05) | **T2** |
| `VeryLongOutdoors` (≤0.2) | **T3** |
| `LongOutdoors` (≤0.4) | **T3** |
| `Outdoors` (≤0.6) | **T4** |
| `JustOutdoors` (≤0.8) | **T4** |
| `ComfortablyIndoors` (>0.8) | 不入队 |

#### Beauty（**方向相反**；`major=false`）⚠️ 受 R6 限档
| `Hideous` (≤0.01) | **T3** |
| `VeryUgly` (≤0.15) | **T3** |
| `Ugly` (≤0.35) | **T4** |
| `Neutral` 及以上 (>0.35) | 不入队 |

#### Comfort（`major=false`）⚠️ 受 R6 限档
| `Uncomfortable` (<0.1) | **T3** |
| `Normal` (<0.6) | **T4** |
| `Comfortable` 及以上 | 不入队 |

#### RoomSize（`major=false`）⚠️ 受 R6 限档
| `VeryCramped` (<0.01) | **T3** |
| `Cramped` (<0.3) | **T4** |
| `Normal` 及以上 | 不入队 |

#### Suppression（bool；奴隶机制）
| `IsHigh` | **T2** | ⚠️ 建议；奴隶压制主要属玩家管理范畴，框架是否介入待定 |
| 其余 | 不入队 | R5 |

#### KillThirst（无分级）
| 按 `CurLevel` 越低越紧急 | **T1 / T2** | ⚠️ 建议；未查到原版分级成员 |

#### Authority（**dummy，不推荐映射**）
⚠️ 原版明言为兼容旧档的占位 need（`Royalty/.../Needs.xml:3`）⇒ **不该作为驱动来源**。若坚持映射：`Gone → T2`、`Weak → T3`、其余不入队。

---

## 5. 内建基线的价值：为什么框架必须自带这张表

若每个下游 drive 自行决定「我的需求该算哪一档」，会退回 §0 前的老问题——**量纲不可比，档位各自为政**。

框架自带**原版需求 → Tier 的基线映射**后：
- 下游 drive 作者**默认就有**一个可比对的公共标尺；
- 只在引入**原版之外**的自定义需求时才需要自己声明档位；
- 玩家/整合者可以统一调整「娱乐算不算紧急」这类全局口味。

---

## 6. 对 SDD 与分级文档的增补

### 6.1 新增数据：内建基线映射（Def 化，草案）

```xml
<!-- 框架内置：原版需求的基线分级。可被下游覆盖。 -->
<AutonomyTierBaselineDef>
  <defName>PAF_Baseline_VanillaNeeds</defName>

  <!-- 方向相反的需求必须显式声明 invert（F2） -->
  <mappings>
    <li><need>Food</need>      <invert>false</invert><levels><Starving>T0</Starving><UrgentlyHungry>T1</UrgentlyHungry><Hungry>T2</Hungry></levels></li>
    <li><need>Rest</need>      <invert>false</invert><levels><Exhausted>T0</Exhausted><VeryTired>T1</VeryTired><Tired>T2</Tired></levels></li>
    <li><need>Beauty</need>    <invert>true</invert> <levels><Hideous>T3</Hideous><VeryUgly>T3</VeryUgly><Ugly>T4</Ugly></levels></li>
    <li><need>Comfort</need>   <invert>false</invert><levels><Uncomfortable>T3</Uncomfortable><Normal>T4</Normal></levels></li>
    <li><need>Outdoors</need>  <invert>true</invert> <levels><Entombed>T2</Entombed><Trapped>T3</Trapped></levels></li>
    <li><need>Indoors</need>   <invert>true</invert> <levels><BrutalOutdoors>T2</BrutalOutdoors><VeryLongOutdoors>T3</VeryLongOutdoors></levels></li>
    <li><need>RoomSize</need>  <invert>false</invert><levels><VeryCramped>T3</VeryCramped><Cramped>T4</Cramped></levels></li>
    <!-- Chemical：枚举方向本身即"高=满足"，故 invert=true 才能把 Withdrawal 提到 T1 -->
    <li><need>DrugDesire</need><invert>true</invert> <levels><Withdrawal>T1</Withdrawal><Desire>T2</Desire></levels></li>
  </mappings>
</AutonomyTierBaselineDef>
```

> ⚠️ 上表为**形态草案**，字段命名未定稿。且它**无法覆盖** Mood（动态阈值）与 5 个 bool 需求（F5）——见 6.2。

### 6.2 ★ 修正：`tierFromNeed` 必须支持三种阈值来源（F1）

上一版分级文档 §3.3 把 `tierFromNeed` 设计为**静态阈值表**。依据本次 F1，**该设计不足**，须扩展为三选一：

| 来源 | 原版先例 | 框架表达 |
|---|---|---|
| **A. 常数阈值** | Rest / Joy / Outdoors 等 | `<thresholds>` 静态表（上一版已有） |
| **B. Def 派生** | Food（`RaceProps.FoodLevelPercentageWantEat * 0.8`） | 增加 `thresholdSource="RaceProp"` + 字段名 + 系数；**在 pawn 的 race 上求值** |
| **C. 运行期计算** | Mood（`mentalBreaker.BreakThresholdExtreme`） | 静态表**无法表达** ⇒ 必须留给 `IAutonomyDrive.EvaluateTier(Pawn)`（代码档） |

**⇒ 结论：Mood 这类动态阈值需求，纯 XML 档做不到，只能走代码档。** 这一条需在 SDD 的 G2（双档扩展）里明确记一笔「XML 档的能力边界」。

### 6.3 其他增补

| 对象 | 增补 |
|---|---|
| 分级文档 §3.3 | 增加 `invert` 字段（F2）；原因：Chemical / Beauty / Outdoors / Indoors 方向相反 |
| 分级文档 §3.3 | 增加**二值（bool）分级**支持（F5） |
| 分级文档 §3.1 | 分级标签走 `MustTranslate`（F4，佐证 C5） |
| SDD M1 | 新增 `AutonomyTierBaselineDef` 的注册与覆盖（下游可覆盖框架基线） |
| SDD G2 | 记入「XML 档的能力边界：无法表达 per-pawn 动态阈值」 |

---

## 7. 存疑与未决

| # | 事项 | 状态 |
|---|---|---|
| 1 | **Deathrest 的 0.1 阈值** | 取自提示字符串的计算式（`Need_Deathrest.cs:115-117`），**未完整验证** exhaustion 的完整判定链 |
| 2 | **Mood 的动态阈值如何被 XML 档消费** | 未解。可能方案：框架内置一个 `MoodTierDrive` 代码档，为纯 XML 下游提供已算好的 tier |
| 3 | **`Need_Mood.CurLevel` 与百分比的关系** | `Need_Mood` 的 `MaxLevel` 未核实（`Need.cs:55` 默认 1f）；若非 1，阈值口径需换算 |
| 4 | **映射档位（§4.2）是否偏激进** | 全为 ⚠️ 建议值。特别是 `major=false` 需求被 R6 压到 T2 以下，可能过严，需实测 |
| 5 | **Anomaly / Odyssey 无需求** | 已确认无 `NeedDefs` 目录；但 DLC 后续更新可能新增，基线表需可增量扩展 |
| 6 | **`Need_Sadism` 为何有类无 Def** | 未查证（可能已废弃或预留）。不影响本文结论 |

---

## 附录 A：本文引用的原版源码与 Def 清单

**Def XML**：`Data/Core/Defs/NeedDefs/Needs.xml`、`Data/Royalty/Defs/NeedDefs/Needs.xml`、`Data/Ideology/Defs/NeedDefs/Needs.xml`、`Data/Biotech/Defs/NeedDefs/Needs.xml`

**程序集**（前缀 `_research/vanilla/Assembly-CSharp/`）：

| 文件 | 行 | 用途 |
|---|---|---|
| `RimWorld/Need.cs` | `:66` `:81` `:55` `:367` | 基类无分级；`MaxLevel` 默认 1f |
| `RimWorld/NeedDef.cs` | `:92` `:95` | `major`、`listPriority` 字段定义 |
| `RimWorld/NeedsCardUtility.cs` | `:43-45` | `major` 仅用于 UI 分组 |
| `RimWorld/ScenPart_SetNeedLevel.cs` | `:30` | `major` 仅用于场景部件 |
| `RimWorld/Need_Food.cs` | `:24-39` `:69-87` | 分级 + **Def 派生阈值** |
| `RimWorld/Need_Rest.cs` | `:14-32` | RestCategory |
| `RimWorld/Need_Mood.cs` | `:34-64`、`:42` `:51` | **翻译键分级 + 动态阈值** |
| `RimWorld/Need_Joy.cs` | `:14-40` | JoyCategory |
| `RimWorld/Need_Learning.cs` | `:34-59` | LearningCategory |
| `RimWorld/Need_Play.cs` | `:23-48` | PlayCategory |
| `RimWorld/Need_Beauty.cs` | `:39-68` | BeautyCategory（方向相反） |
| `RimWorld/Need_Comfort.cs` | `:27-52` | ComfortCategory |
| `RimWorld/Need_Chemical.cs` | `:22-35` | DrugDesireCategory（方向相反） |
| `RimWorld/Need_Outdoors.cs` | `:27-52` | OutdoorsCategory（方向相反） |
| `RimWorld/Need_Indoors.cs` | `:37-62`、`:137` | IndoorsCategory + 阈值数组 |
| `RimWorld/Need_RoomSize.cs` | `:22-39` | RoomSizeCategory |
| `RimWorld/Need_Authority.cs` | `:30-56` | AuthorityCategory（dummy） |
| `RimWorld/Need_MechEnergy.cs` | `:24` `:34` | bool 二值 |
| `RimWorld/Need_Deathrest.cs` | `:12`、`:112-117` | bool + 0.1 阈值 |
| `RimWorld/Need_Suppression.cs` | `:12` `:22` | bool 二值 |
| `RimWorld/Need_Sadism.cs` | `:11` `:21` | bool 二值（未启用） |

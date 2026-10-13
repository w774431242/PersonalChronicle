# 08c · 数据层契约与 UI 对接标准（DL 标准）

> 版本：**v1.1** · 2026-09-10
> 依据（三源）：
> ① **源码全量遍历** —— `Source/PersonalChronicle/` 268 个 .cs
> ② **原版程序集反编译（Spy）** —— `ilspycmd 8.2.0.7535` × `Assembly-CSharp.dll`（RimWorld 1.6）：`Scribe` / `Scribe_Values` / `Scribe_Collections` / `Scribe_Deep` / `Scribe_References` / `Scribe_Defs` / `Scribe_TargetInfo` / `ScribeLoader` / `ScribeSaver` / `ScribeMetaHeaderUtility` / `ScribeExtractor` / `LookMode` / `LoadSaveMode` / `PostLoadIniter` / `CrossRefHandler` / `BackCompatibility` / `GameComponent` / `GameComponentUtility`
> ③ **官方与社区资料** —— RimWorld Wiki（Modding Tutorials）、`spdskatr/RWModdingResources` saving-guide、`Dev-Jahn/rimworld-modding-reference` save-load、HugsLib Wiki（World Data Storage）、Ludeon 论坛
> 适用：**后续一切 UI 对接数据与持久化开发**（档案馆新页、ITab 新页签、Dialog、RUIF 消费层、Save schema 变更）
> 关系：本文件是 [03-架构层-数据与扩展规范](03-架构层-数据与扩展规范.md) 的**实操细则**；命名与治理冲突时以 03 为准；**平台机制事实以程序集实证为准**（可信度分级见 §15）。
> v1.1 变更：新增 §12 Scribe 序列化标准 · §13 数据宿主生命周期 · §14 存档兼容与迁移标准 · §15 资料来源与可信度分级（含 1 处社区资料被程序集证伪）；§9 红线新增 DL-19~DL-24；原附录顺延为 §16。

---

## 1. 分层总图与依赖方向

```
┌──────────────────────────────────────────────────────────────┐
│ UI 层            Archive/ (58 文件) · ITab_* · Dialog_*        │
│                  只消费快照 + 翻译/排版；禁排序/禁集合查询       │
└───────────────┬──────────────────────────────────────────────┘
                │ 只读：IArchiveUiDataProvider.Build*(...)
┌───────────────▼──────────────────────────────────────────────┐
│ Application 层   Application/ (44 文件)                        │
│   ├─ IArchiveService : IArchiveQueryService  ← 契约（读写两面） │
│   ├─ ArchiveService (partial ×14)            ← 实现            │
│   ├─ ArchiveLiveViews / WorkIntensityContracts ← Live View 模型 │
│   └─ ReadModels/  ← ★ UI 对接唯一入口                          │
│        ArchiveSectionSnapshot · IArchiveUiDataProvider         │
│        SectionSnapshots.{Home,Overview,Detail,Event,Career}    │
│        ProfessionalGuideView · ProfessionalBonusView           │
└───────────────┬──────────────────────────────────────────────┘
                │ 领域类型 + 纯函数求值
┌───────────────▼──────────────────────────────────────────────┐
│ Domain 层        Domain/ (72 文件)                             │
│   模型(IExposable) + Def + 常量键 + Evaluator(纯静态)          │
└───────────────▲──────────────────────────────────────────────┘
                │ 写入（唯一入口）
┌───────────────┴──────────────────────────────────────────────┐
│ Data 层          Data/ (9 文件)                                │
│   ChronicleGameComponent : GameComponent ← 存档根 + 事件存储    │
└───────────────▲──────────────────────────────────────────────┘
                │ 调用 IArchiveService 写入面
┌───────────────┴──────────────────────────────────────────────┐
│ Capture 层       Capture/ (22 文件)  ← Harmony 补丁，只写不读   │
└──────────────────────────────────────────────────────────────┘

横切：Api/ (19 文件) —— 第三方 Mod 扩展面（Provider/EventSink/QueryService）
```

**规则 DL-1（依赖方向）**：只允许 `UI → Application → Domain ← Data/Capture`。禁止 UI 直接引用 `Data` 或 `Capture`；禁止 Domain 引用 Application/UI。
**规则 DL-2（读写分离）**：UI 只准用 `IArchiveUiDataProvider`（首选）或 `IArchiveQueryService`（次选）；`IArchiveService` 的写入方法（`On*`/`Record*`/`Set*`）**只准 Capture 层与 Application 内部调用**。

---

## 2. 数据流与生命周期

```
游戏事件（殖民者加入/死亡/击杀/制造/建造/研究/关系变化/物品转移…）
   │  ① Capture/ 的 Harmony 补丁触发
   ▼
IArchiveService 写入面   OnColonistJoined / OnPawnDied / OnKillRecorded /
   │                     RecordCareerProduced / OnThingBuilt / OnRelationChanged …
   │  ② 聚合进持久化累加器（不逐条落事件）
   ▼
ChronicleGameComponent（GameComponent，随存档 Scribe 持久化）
   │   Objects(ArchiveObject) · Events(ChronicleEvent) · NextEventId ·
   │   *Aliases / *Overrides · ResearchSpeedBonusByMajor · SchemaVersion
   │   任何写入 → MarkChanged() → DataRevision++
   │  ③ 只读查询（不排序、不过滤）
   ▼
IArchiveQueryService：GetAllRecords / GetEventsFor / GetObjectsOfCategory /
   │                   GetLivePawn / GetWorkTimeStats / GetProductionSummary …
   │  ④ 查询 + 排序 + 过滤 + null 守卫 + 预本地化（Read Model 职责）
   ▼
ArchiveUiDataProvider.BuildHome/BuildOverview/BuildDetail/BuildEvent
   │  → HomeSnapshot / OverviewSnapshot / DetailSnapshot / EventSnapshot
   │  ⑤ 不可变快照，带 BuiltFromRevision
   ▼
UI 绘制（只读快照字段，本轮内不查询、不排序）
```

---

## 3. Data 层契约 —— `ChronicleGameComponent`

`Data/ChronicleGameComponent.cs` + 8 个 partial。**存档根，全 Mod 唯一数据源**。

### 3.1 持久化字段（Scribe 序列化）

| 字段 | 类型 | 说明 |
|---|---|---|
| `Objects` | `List<ArchiveObject>` | 全部档案对象（Pawn/Thing/Battle/Location 多态） |
| `Events` | `List<ChronicleEvent>` | 全量事件流（追加式） |
| `NextEventId` | `long` | 事件自增主键 |
| `BuildingAliases` | `Dictionary<string,string>` | 建筑实例别名，key=`<defName>:<thingIDNumber>` |
| `RoomRoleAliases` | `Dictionary<string,string>` | 房间类型别名，key=`RoomRoleDef.defName` |
| `BuildingLabelOverrides` | `Dictionary<int,string>` | 建筑标签覆盖（按 thingID） |
| `RoomNameOverrides` / `RoomTypeOverrides` | `Dictionary<string,string>` | 房间名/类型覆盖 |
| `ResearchSpeedBonusByMajor` | `Dictionary<string,float>` | 主修方向研究速度加成 |
| `HomeViewMode` | `int` | 首页视图偏好（镜像到服务读侧） |
| `SchemaVersion` | `long` | **存档结构版本**（新增字段必须评估迁移） |
| `DataRevision` | `long { get; private set; }` | 单调递增修订号（由 `MarkChanged()` 自增） |

### 3.2 查询面（UI 可间接使用；推荐经服务层）

```csharp
IReadOnlyList<PawnRecord>        GetAllRecords();
IReadOnlyList<PawnRecord>        GetRecordsFor(Pawn pawn);
IReadOnlyList<ChronicleEvent>    GetEventsFor(string stableId);
ArchiveObject                    GetObject(string stableId);
IReadOnlyList<ArchiveObject>     GetLinkedObjects(string stableId);
IReadOnlyList<ArchiveObject>     GetObjectsOfCategory(string categoryKey);
IReadOnlyList<ChronicleEvent>    GetRecentEvents(int count);
IReadOnlyList<ChronicleEvent>    GetAllEvents();
```

### 3.3 其他 partial 职责

| 文件 | 职责 | 对外 |
|---|---|---|
| `.Queries.cs` | 上述查询 + 内部 Add* | 查询 public / 写入 internal |
| `.Sampling.cs` | `GameComponentTick()` 周期采样（工时/关系回填/地点） | `internal` |
| `.Sampling.Places.cs` | 地点采样 | `internal` |
| `.Aliases.cs` | 建筑/房间别名读写 | `internal` |
| `.Battle.cs` | `LinkRaidLords(BattleObject)` 袭击波次关联 | public |
| `.Qualification.cs` | `RunQualification()` / `ManualGrantQualifiedTitle(...)` | public |
| `.Helpers.cs` | `ResolveDefaultJoinTick()` / 研究速度加成读写 | mixed |

> **DL-3（写入唯一性）**：新增数据写入必须落在 Capture 层补丁 → `IArchiveService` 写入面 → `ChronicleGameComponent`，禁止 UI 或 ReadModel 反向写。
> **DL-4（MarkChanged）**：任何影响 UI 展示的写入后必须 `MarkChanged()`（内部已随 Add* 调用），否则数据已变但 revision 不变。

---

## 4. Domain 层 —— 模型字典与常量键

### 4.1 档案对象多态树

```csharp
public abstract class ArchiveObject : IExposable {
    public string StableId;            // 稳定标识（跨存档不变）
    public string LabelSnapshot;       // 名称快照（预本地化/离线可用）
    public abstract string CategoryKey { get; }
}
├─ PawnRecord : ArchiveObject          → PawnObject : PawnRecord（最深派生）
├─ ThingObject : ArchiveObject
├─ BattleObject : ArchiveObject
└─ LocationObject : ArchiveObject
```

| 模型 | 关键字段 |
|---|---|
| `PawnRecord` | LabelShort · KindDefName · FactionDefName · JoinTick · DeathTick · DeathCauseKey · Role(`PawnRole`) · ArchiveStatus(`PawnArchiveStatus`) · ArchiveExpiryTick |
| `PawnObject` | WorkSnapshot · **SkillSnapshot**/**SkillSnapshotOnDeath** · WorkTime(`WorkTimeAccumulator`) · Production(`ProductionAccumulator`) · Consumption(`ConsumptionAccumulator`) · Childhood/AdulthoodBackstoryDefName · PlaceHistory · Relations · DamageDealtTotal · ParticipatedBattles · Melee/RangedKills · Workplace·WorkplaceHistory · Residence · GrantedMedals · **CareerData** |
| `ThingObject` | ThingDefName · WeakId · CreatedTick · DestroyedTick · CurrentHolderId · HolderHistory · HolderRecords · Decommission |
| `BattleObject` | IncidentDefName · ThreatKey · StartTick · EndTick · RaidCount · RemainingRaidCount · ParticipantIds |
| `LocationObject` | MapId · CellLabel · WorldTile · MapDefName · WorldObjectDefName · FactionDefName · IsPlayerHome · Hilliness/Altitude/IsCoastal/Pollution/AvgTempC/SnowCovered · EstablishedTick/DeinitTick/DeinitReason · CanTrade · TraderKindDefName · PermitRequiredDefName · TradeKindKeys |

### 4.2 事件模型

```csharp
public class ChronicleEvent : IExposable {
    public long Id; public long Tick;
    public string TypeKey;             // → ChronicleEventDef.defName
    public string SourceId;
    public int ImportanceLevel = -1;
    public string PawnStableId;
    public Dictionary<string,string> Params;   // 语言无关键值（见 §4.4）
    public ObjectRef Primary;                  // 主对象边
    public List<ObjectRef> Subjects;           // 关联对象边
}
public class ObjectRef : IExposable { public string CategoryKey; public string StableId; public string LabelSnapshot; }
```

### 4.3 主要累加器（持久化，**不扫事件流**）

| 类型 | 字段 | 用途 |
|---|---|---|
| `ProductionAccumulator` | QuantityByDef · MarketValueByDef · TotalQuantity · TotalMarketValue · LastProductionTick | 产出汇总 |
| `ConsumptionAccumulator` | SilverByCategory · SilverByDay · TotalSilver · LastConsumeTick | 消耗计价（累计/近 7 天/30 天日均） |
| `WorkTimeAccumulator` | tick 累计（按工时） | 工时统计 |
| `WorkplaceSnapshot` | BuildingDefName · BuildingStableId · UseCount · LastUsedTick · RoomRoleDefName · CustomName · MapIndex · Cell | 当前工作地 |
| `ResidenceSnapshot` | RoomRoleDefName · LastSeenTick · MapIndex · Cell | 居住地 |
| `HolderRecord` | StableId · LabelSnapshot · Kind(`own`/`loan`) · StartTick · EndTick · IsFirst | 持有链 |
| `DecommissionRecord` | Tick · LastHolderStableId/Label · LastPlaceLabel · ServiceDays · LastBattleLabel | 退役档案 |
| `PlaceVisit` / `SignificantRelation` / `WorkplaceHistoryEntry` / `MedalRecord(GrantedTitle)` | — | 轨迹/关系/履历/荣誉 |
| `ProfessionalState` | primaryDirection · skills · practiceCountBySkill | 职业状态 |
| `QualificationState` | progress · Status · Practical/Theory/Thesis/DefensePassed · CompositeScore · Review* | 资格状态 |
| `ExamData` / `ThesisData` / `CareerData`(Events·RecordCountByType·Professional) | — | 考试/论文/职业履历 |

### 4.4 数据键常量（**语言无关，禁直接翻译**）

| 常量类 | 值 |
|---|---|
| `ArchiveCategoryKeys` | `Pawn` · `Thing` · `Battle` · `Location` |
| `ChronicleEventParams` | `killer` · `victim` · `victimStableId` · `combatRole`(`kill`) · `assist` · `victimFactionDef` · `victimFactionLabel` · `victimKindDef` · `victimCategory`(`humanlike`/`mechanoid`/`animal`) · `relation` · `relationAction`(`formed`/`ended`) · `__unknown_killer__` |
| `CareerEventType` | `WorkCompleted` · `ItemProduced` · `ResearchCompleted` · `BookProduced` · `ExamPassed` · `ThesisCompleted` · `ThesisDefended` · `TitleGranted` · `MedalGranted`（+ `Allowed` 白名单） |
| `QualificationStatus` | `Locked` · `Eligible` · `Preparing` · `PracticalExam` · `TheoryExam` · `Thesis` · `Defense` · `Review` · `Qualified` · `Granted` |
| `HolderRecord` | `own` · `loan` |

### 4.5 求值器（纯静态，可单测，UI **不得**复制其逻辑）

`ProfessionalEffectResolver`（数值单一真源）· `ProfessionalFitAnalyzer` · `ProfessionalRatingEvaluator` · `ProfessionalXpEvaluator` · `ProfessionalDirectionEvaluator` · `QualificationEvaluator` · `ExamScoring` · `WorkIntensityEvaluator` · `HealthValuationEvaluator` · `MedalAwardEvaluator` · `AchievementEvaluator` · `SocialRelationFilter` · `ThingArchivePolicy` · `DlcStatus`。

> **DL-5（单一真源）**：任何派生数值（加成/评级/资格/估值）必须调用对应 Evaluator，UI 与 ReadModel 都不得手算。

---

## 5. Application 层契约

### 5.1 `IArchiveQueryService`（**UI 只读面，30 个方法**）

```csharp
// —— 档案对象 ——
IReadOnlyList<PawnRecord>     GetAllRecords(); GetActiveRecords(); GetArchivedRecords();
ArchiveObject                 GetObject(string stableId);
IReadOnlyList<ArchiveObject>  GetLinkedObjects(string stableId);
IReadOnlyList<ArchiveObject>  GetObjectsOfCategory(string categoryKey);
ArchiveDepthBehavior          GetCategoryBehavior(string categoryKey);

// —— 事件 ——
IReadOnlyList<ChronicleEvent> GetEventsFor(string stableId);
IReadOnlyList<ChronicleEvent> GetRecentEvents(int count);
IReadOnlyList<ChronicleEvent> GetAllEvents();
IReadOnlyList<ChronicleEvent> GetProductionEvents(string stableId);

// —— 修订与存活 ——
long GetDataRevision();
Pawn GetLivePawn(string stableId);  Pawn GetCurrentHolder(string stableId);
LocationInfo GetLiveLocation(string stableId);  BattleObject GetActiveBattle();
bool IsCurrentlyEnlisted(string stableId);   // 语义与 IsArchived 相反，勿混用

// —— 履历视图（Live View，非快照）——
IReadOnlyList<WorkPriorityView>    GetWorkPriorities(string stableId);
IReadOnlyList<WorkTimeStatView>    GetWorkTimeStats(string stableId);
IReadOnlyList<SkillArchiveView>    GetSkillArchive(string stableId);
ProductionSummaryView              GetProductionSummary(string stableId);
ConsumptionSummaryView             GetConsumptionSummary(string stableId);

// —— 首页计数 / 偏好 / 别名 ——
int GetLiveColonistCount();  void GetLiveColonistCounts(out int free, out int slave, out int prisoner);
int GetActiveSnapshotCount(); GetArchivedSnapshotCount(); GetServiceDays(); GetHomeViewMode();
string GetBuildingAlias(string buildingStableId);  string GetRoomRoleAlias(string roomRoleDefName);
```

### 5.2 `IArchiveService` 写入面（**仅 Capture / 内部**，节选）

`OnColonistJoined(pawn[,role])` · `OnPawnDied(pawn,cause[,weapon,extraParams,killer])` · `OnKillRecorded(killer,victim[,weapon,assist,finishingDamage,isMelee])` · `OnRelationChanged(a,b,def,formed)` · `OnThingCrafted` · `OnThingBuilt` · `OnThingDestroyed` · `OnThingConsumed` · `OnWorkplaceUsed` · `OnBattleStarted` · `OnRaidPawnGone` · `LinkRaidLords` · `RecordCareerProduced` · `RecordCareerConstruction` · `RecordCareerResearch` · `RecordSkillLearn` · `RecordCraftCompleted` · `RecordWorkCompletedQuantum` · `SetCurrentWorkplace` · `SetWorkplaceCustomName` / `SetBuildingAlias` / `SetRoomRoleAlias` / `SetRoomName` / `SetRoomTypeName` · `SetHomeViewMode`。

### 5.3 Live View 模型（`ArchiveLiveViews.cs` / `WorkIntensityContracts.cs`）

全部 `readonly` 字段（**不可变**，`LocationInfo`/`WorkPriorityView`/`WorkTimeStatView`/`SkillArchiveView`/`ProductionSummaryView`/`ProductionTypeView`/`ConsumptionSummaryView`/`ConsumptionTypeView`；`WorkIntensityView`/`WorkIntensityTierView`/`WorkIntensityWorkTypeView`/`ColonyWorkAggregateView`/`ColonyWorkTypeView`/`WorkTimeSample`）。

### 5.4 ★ Read Model（UI 对接核心）

```csharp
public abstract class ArchiveSectionSnapshot {
    public long BuiltFromRevision { get; set; }   // 构建时数据修订号
    public bool IsEmpty { get; set; }             // 无内容（UI 直接画空状态）
}

public interface IArchiveUiDataProvider {
    HomeSnapshot     BuildHome(IArchiveService service, long revision);
    OverviewSnapshot BuildOverview(IArchiveService service, string categoryKey, long revision);
    DetailSnapshot   BuildDetail(IArchiveService service, string detailObjectId, long revision);
    EventSnapshot    BuildEvent(IArchiveService service, ChronicleEvent ev, long revision);
    IReadOnlyList<ChronicleEvent> BuildTimelineEvents(IArchiveService service, long revision);
}
public sealed partial class ArchiveUiDataProvider : IArchiveUiDataProvider { }  // 默认实现
```

**附加静态构建器**（无服务依赖，纯对象 → 视图）：

```csharp
ArchiveUiDataProvider.BuildMedals(PawnObject pawn)                        → IReadOnlyList<MedalView>
ArchiveUiDataProvider.BuildQualRows(CareerData, QualificationDef, float)   → IReadOnlyList<CareerQualView>
ProfessionalGuideSnapshotBuilder.*  /  ProfessionalBonusSnapshotBuilder.*  → ProfessionalGuideView / ProfessionalBonusView
```

#### 5.4.1 快照字段字典

**`HomeSnapshot`**：`ActivePawnCount` · `ArchivedPawnCount` · `LiveColonistCount` · `LiveFreeCount` · `LiveSlaveCount` · `LivePrisonerCount` · `ServiceDays` · `RecentEvents` · `ImportantObjects` · `BattleKpis`(BattleKpisView) · `LocationKpis`(LocationKpisView)

**`OverviewSnapshot`**：`CategoryObjects : Dictionary<string, List<ArchiveObject>>` · `LocationKpis` · `LocationEventCounts : Dictionary<string,int>` · `BattleKpis`
（子视图：`ProductionLineView` · `ProductionCategoryView` · `KillByFactionView`）

**`EventSnapshot`**：`Event : ChronicleEvent` · `FollowupEvents : IReadOnlyList<ChronicleEvent>`

**`DetailSnapshot`**（Pawn / Thing / Location / Battle 共用，40+ 字段）：

| 组 | 字段 |
|---|---|
| 根 | `DetailObject` · `RawEvents` |
| 人物履历 | `LifePhases` · `CareerBars` · `Footprint` · `Milestones` · `KeyEvents` · `Relations` · `WorkExperiences` |
| 战斗 | `Kills` · `BattleCount` · `BattleKpis` · `KillsByFaction` · `ParticipatedBattles` · `DamageDealtTotal` · `MeleeKillRatio` · `MeleeKills` · `RangedKills` |
| 产出/消耗 | `ProductionLines` · `ProductionCategories` · `ProductionTotal` · `ProductionSilverValue` · `WeeklyProductionSilver` · `MonthlyProductionSilver` · `ProductionTypeViews` · `ConsumptionTotalSilver` · `WeeklyConsumptionSilver` · `DailyConsumptionSilver` · `ConsumptionTypeViews` |
| 工作/居住 | `WorkIntensity` · `Workplace`(WorkplaceView) · `Residence`(ResidenceView) |
| 器物传承 | `Origin`(ThingOriginView) · `MakerChain`(MakerChainView) · `CoUse`(CoUseView) · `Decommission`(DecommissionView) · `Legacy`(LegacyView) · `LegacyOffspring` |
| 荣誉/健康/地点 | `Medals` · `Health`(HealthView) · `Location`(LocationDetailView) |
| 职业 | `CareerOverview`(CareerOverviewView) · `FactCounts`(CareerFactCounts) · `TitleTiers` |

**`SectionSnapshots.Career.cs` 子视图**：`CareerOverviewView` · `CareerFactCounts` · `CareerLevelDetail` · `CareerLevelSource` · `CareerQualView` · `CareerPreCheckView` · `CareerTitleTierView`

**`SectionSnapshots.Detail.cs` 子视图**：`WorkplaceView` · `WorkplaceChoiceView` · `DeviceUseItem` · `ResidenceView` · `MedalView` · `LocationDetailView` · `HealthView` · `HealthFactorView` · `HealthEventView` · `LifePhaseKind`(enum) · `LifePhaseView` · `CareerBarView` · `FootprintLedgerView` · `FootstepView` · `MilestoneView` · `KeyEventView` · `RelationView` · `LegacyView` · `LegacyHolderView` · `ThingOriginView` · `MakerChainView` · `CoUseRowView` · `CoUseView` · `DecommissionView` · `WorkExperienceView`

**`SectionSnapshots.Home.cs` 子视图**：`BattleKpisView`(Total·Decisive·Kills·Losses·Roster·Cards) · `BattleCardView`(RaidCount·Kills·Losses·Participants·IsSignificant·ThreatKey) · `LocationKpisView`(Total·Home·Quest·Settle·Ruined·Tradable·Permit·Factions)

> **DL-6（快照不可变）**：快照是**只读显示模型**——只含数据键与**预计算好的显示字符串**（构建时已本地化，字段注释标 `// already localized`）。UI 不得改写快照字段。
> **DL-7（本地化时机）**：翻译在 ReadModel 构建期完成（或 UI 用 `Translate()` 渲染 key）；数据层永远不存翻译结果到存档。
> **DL-8（失败隔离）**：`service == null` 或查询抛异常 → 返回空快照（`IsEmpty = true`），绝不把异常抛进绘制循环。

---

## 6. UI 对接标准流程（SOP）

### 6.1 标准接法（页面级）

```csharp
// ① 窗口持有 provider（实例字段，只建一次）
private ReadModels.IArchiveUiDataProvider uiDataProvider = new ReadModels.ArchiveUiDataProvider();

// ② 绘制入口：节流刷新 → 只读快照
public override void DoWindowContents(Rect inRect)
{
    IArchiveService service = PersonalChronicleMod.ArchiveService;
    if (service == null) { DrawNoService(inRect); return; }   // 服务缺席必须早退
    RefreshCacheIfNeeded(service);                            // ③ 节流
    DrawHeader(...); DrawSidebar(...); DrawContent(...);      // ④ 纯绘制
}

// ⑤ 刷新：tick 门控（权威）+ revision（辅助）
private void RefreshCacheIfNeeded(IArchiveService service)
{
    if (GenTicks.TicksGame < nextRefreshTick) return;         // 节流优先，勿被 revision 绕过
    nextRefreshTick = GenTicks.TicksGame + CacheRefreshInterval;
    RefreshNow(service);
}
private void RefreshNow(IArchiveService service)
{
    long revision = service.GetDataRevision();
    HomeSnapshot home = uiDataProvider.BuildHome(service, revision);
    RebuildPawnCounts(home); RebuildRecentLines(home); RebuildImportantCards(home);
    // 视图作用域短路：不在详情视图时不要 BuildDetail（贵）
    if (view == MainView.PawnDetail || view == MainView.WeaponDetail)
        RebuildDetailCache(service, revision);
}

// ⑥ 特例：立即生效场景（如手动授勋后）显式绕过节流
public void NotifyManualAward() { ... RebuildDetailCache(service, service.GetDataRevision()); }
```

### 6.2 缓存与刷新标准

| 机制 | 用法 | 陷阱 |
|---|---|---|
| `CacheRefreshInterval`（tick 节流） | **权威门控**，绘制路径绝不重算 | — |
| `GetDataRevision()` | 仅用于「数据真的变了才重建视图级缓存」+ 特例立即刷新 | ⚠️ 采样每 `WorkSampleIntervalTicks`(2s) 就 `MarkChanged()`，**不能单独作为刷新门控**，否则每 2 秒全量重建 |
| 视图作用域短路 | 仅重建当前视图需要的快照 | 否则 BuildDetail + 战斗扫描 + 产出/强度/链接全量白跑 |
| `NotifyManualAward()` 类 | 绕过节流的显式刷新钩子 | 新交互（导入/批量修改）需补同类钩子，否则等下一次节流 |
| `BuiltFromRevision` | 断言/调试用；比较后决定跳过重建 | 不要用它做唯一的刷新判据 |

### 6.3 stableId 约定

| 对象 | 格式 | 例 |
|---|---|---|
| 建筑 / 物品 | `<defName>:<thingIDNumber>` | `TableMachining:12345` |
| Pawn | pawn 稳定 ID（`PawnStableId` / `StableId`） | — |
| Battle / Location | 各自 `StableId`（`ObjectRef.StableId`） | — |

解析用 `stableId.IndexOf(':')` 取 defName（见 UI 的 `ThingDefNameFromStableId`）；**禁止**按 `_`/空格切分或截断猜测。

---

## 7. Api 层 —— 第三方扩展点（本 Mod 对外承诺）

```csharp
public interface IPersonalChronicleApi {
    ApiVersion Version { get; }                     // 当前 1.2.0
    IArchiveQueryService Queries { get; }           // 只读查询
    IArchiveEventSink Events { get; }               // 事件写入
    IWorkTimeCaptureService WorkTime { get; }
    IWorkIntensityService WorkIntensity { get; }
    IArchiveProviderRegistry Providers { get; }     // 统一 Provider 注册表（P2+ 唯一入口）
    IWorkIntensityProviderRegistry WorkIntensityProviders { get; }
    bool Supports(int major, int minMinor);         // 能力探测，替代比版本号
}
public static class PersonalChronicleApi { public static bool TryGet(out IPersonalChronicleApi api); }
```

四类领域 Provider（均 `: IArchiveProvider`，含 `*Capabilities` 能力令牌）：

| Provider | 输入 / 输出 | 内置实现 |
|---|---|---|
| `IBattleProvider` | `BattleAppraisalInput(stableId, dataKeys)` → `BattleAppraisal(IsSignificant)` | `BuiltinBattleProvider` |
| `IPlaceProvider` | `PlaceFootprintInput` → `PlaceFootprint` | `BuiltinPlaceProvider` |
| `IProductionProvider` | `ProductionInput` → `ProductionContribution` | `BuiltinProductionProvider` |
| `IRelationProvider` | `RelationAppraisalInput` → `RelationAppraisal` | `BuiltinRelationProvider` |

> **DL-9（Provider 优先）**：UI/ReadModel 需要领域判定（战役意义、产出贡献…）时，**必须**走 `Providers.ForEach<T>(...)` 链并内置 fallback，禁止硬编码判定（`ResolveBattleSignificance` 是标准范例：Provider 未注册时回落 `ThreatKey == "ThreatBig"`，且 try/catch 兜底）。

---

## 8. UI 侧对接点清单（`Archive/` 58 文件的职责边界）

| 类别 | 文件 | 职责 |
|---|---|---|
| 主页签 | `ArchiveMainTabWindow.*`（22 个 partial） | 壳/缓存/各分类页 |
| 缓存 | `ArchiveMainTabWindow.Cache.cs` | `RefreshCacheIfNeeded` / `RefreshNow` / 视图作用域短路 |
| 检视页签 | `ITab_Pawn_Career.*`（7 个 partial） · `ITab_Pawn_Chronicle` | 职业/编年史页签 |
| 对话框 | `Dialog_*`（10 个） | 授勋/改名/资格流程/开发报告 |
| 会话壳 | `ArchivePanelBase` · `ArchiveUiStyle` · `ChronicleInspectTabInjector` | 面板基类/样式/页签注入 |
| 词汇表 | `OverviewFieldRegistry(.Scan)` | 概览字段注册与扫描 |
| 开发工具 | `DevTestButtons*`（3 个） · `DevSelfTest` · `DevCoverageProbe` | 注入样本走**真实捕获管线**验证 UI |

---

## 9. 红线与反模式

| 编号 | 禁止 | 正确做法 |
|---|---|---|
| DL-10 | UI 中写 LINQ 排序/过滤/去重 | 交给 ReadModel 构建器（排序规则集中、可单测） |
| DL-11 | UI 直接 `Find.*` / 遍历地图 / 读 `Pawn` 实时状态取可持久数据 | 走 `GetLivePawn`/`GetLiveLocation` 或快照字段 |
| DL-12 | 绘制路径每帧查询/重建（`GetGizmos` 同类） | 节流刷新 + 实例缓存 + 视图短路 |
| DL-13 | 用 `IsArchived`(`DeathTick>0`) 判断"是否在编" | 用 `IsCurrentlyEnlisted(stableId)` |
| DL-14 | 把翻译结果写入存档 / 把 defName 当显示文本 | 存档只放数据键；显示时 `Translate()` 或读快照已本地化字段 |
| DL-15 | 新增持久化字段不改 `SchemaVersion` / 不评估迁移 | 按 03-数据与扩展规范登记迁移 |
| DL-16 | 在 ReadModel 里 `new Color` / 引用 RUIF 绘图 | 数据层零表现依赖（颜色归 Theme/RUIF） |
| DL-17 | UI 复制 Evaluator 的公式 | 调用唯一真源 Evaluator |
| DL-18 | Provider 缺失时抛异常 / 静默返回默认值 | 链式查询 + 显式 fallback + try/catch（见 DL-9） |
| DL-19 | 用 `Scribe_Values` 保存 `Def` / `Thing` / `IExposable` / `TargetInfo` | 分别用 `Scribe_Defs` / `Scribe_References`\|`Scribe_Deep` / `Scribe_Deep`\|`Scribe_References` / `Scribe_TargetInfo`（原版会 `Log.Error`，见 §12.4） |
| DL-20 | 对**已生成**的 Thing 用 `LookMode.Deep` | 用 `LookMode.Reference`（Deep 会重复序列化、破坏引用完整性） |
| DL-21 | 新增持久化字段不给默认值 / 不写迁移 | `Scribe_Values.Look(..., "key", defaultValue)` 必填默认值；结构变更进迁移链（§14.4） |
| DL-22 | 保存翻译文本 / `Texture` / `Rect` / `GUIStyle` / `Window` / Provider 实例 / 缓存 | 只存稳定 ID、数值、枚举、Def 名、可迁移数据（SAVE-002） |
| DL-23 | 读档后不初始化集合、不重建缓存（依赖 `null` 兜底） | `PostLoadInit` 分支内 `??=` 补集合 + 重建索引 + 修复悬空引用（§12.5） |
| DL-24 | 新增字段复用已废弃的旧 label / 重命名 label 不留别名 | label 是兼容契约：重命名须保留旧名读取路径 ≥1 个兼容周期（SAVE-005） |
| DL-25 | 迁移写在 `ExposeData` 里 / 用 `else-if` 串联版本判断 / 迁移内依赖 UI、语言、第三方 Mod | 迁移放 `FinalizeInit`，用 `if (SchemaVersion < N)` 逐级幂等推进（§14.4 八步） |

---

## 10. 常见任务速查

**① 新页面要展示数据** → 找现有快照字段（§5.4.1）→ 缺字段则在对应 `SectionSnapshots.*.cs` 补字段 + 在 `ArchiveUiDataProvider.*.cs` 对应 `Build*` 里填充 → UI 只读新字段。

**② 需要新的排序/聚合** → 在 ReadModel 中实现（含 null 守卫与稳定 tie-breaker：次键用 `StableId` 的 `StringComparer.Ordinal`，因为 `List<T>.Sort` 不稳定）。

**③ 需要新数据类型（如新档案分类）** → ① `ArchiveCategoryKeys` 加键 → ② `ArchiveObject` 新派生类 + `CategoryKey` → ③ `ArchiveCategoryDef` XML → ④ `ChronicleGameComponent.AddObject` 接线 → ⑤ ReadModel 消费。

**④ 新成就/荣誉触发** → Capture 补丁 → `IArchiveService` 写入面 → `CareerEventType` 常量 + `CareerData.RecordCountByType` → ReadModel 聚合进 `CareerFactCounts`。

**⑤ 第三方 Mod 接入** → `PersonalChronicleApi.TryGet` + `Supports(major, minor)` → 注册 Provider 到 `api.Providers` → 或用 `api.Events` 写入事件。

---

## 12. Scribe 序列化标准（原版程序集实证）

> 全部签名摘自反编译产物（`%TEMP%\rw_spy2\`，`ilspycmd` × Assembly-CSharp 1.6）。

### 12.1 API 家族全签名

```csharp
// ── 门面 ──
public static class Scribe {
    public static ScribeSaver  saver = new ScribeSaver();
    public static ScribeLoader loader = new ScribeLoader();
    public static LoadSaveMode mode = LoadSaveMode.Inactive;   // ExposeData 内据此分支
    public static bool EnterNode(string nodeName);
    public static void ExitNode();
    public static void ForceStop();
}

// ── 值 / Def / 深序列化 / 引用 ──
public static void Scribe_Values.Look<T>(ref T value, string label, T defaultValue = default(T), bool forceSave = false);
public static void Scribe_Defs.Look<T>(ref T value, string label) where T : Def, new();
public static void Scribe_Deep.Look<T>(ref T target, string label, params object[] ctorArgs);
public static void Scribe_Deep.Look<T>(ref T target, bool saveDestroyedThings, string label, params object[] ctorArgs);
public static void Scribe_References.Look<T>(ref T refee, string label, bool saveDestroyedThings = false) where T : ILoadReferenceable;
public static void Scribe_References.Look<T>(ref WeakReference<T> refee, string label, bool saveDestroyedThings = false) where T : class, ILoadReferenceable;

// ── 集合（List/HashSet/Stack/Queue/Dictionary）──
public static void Scribe_Collections.Look<T>(ref List<T> list, string label, bool saveDestroyedThings, LookMode lookMode = LookMode.Undefined, params object[] ctorArgs);
public static void Scribe_Collections.Look<T>(ref List<T> list, string label, LookMode lookMode = LookMode.Undefined, params object[] ctorArgs);
public static void Scribe_Collections.Look<K,V>(ref Dictionary<K,V> dict, string label, LookMode keyLookMode = LookMode.Undefined, LookMode valueLookMode = LookMode.Undefined);
public static void Scribe_Collections.Look<K,V>(ref Dictionary<K,V> dict, string label, LookMode keyLookMode, LookMode valueLookMode,
    ref List<K> keysWorkingList, ref List<V> valuesWorkingList, bool logNullErrors = true,
    bool saveDestroyedKeys = false, bool saveDestroyedValues = false);
public static void Scribe_Collections.Look<T>(ref HashSet<T> valueHashSet, string label, LookMode lookMode = LookMode.Undefined);
public static void Scribe_Collections.Look<T>(ref Stack<T> valueStack, string label, LookMode lookMode = LookMode.Undefined);
public static void Scribe_Collections.Look<T>(ref Queue<T> valueQueue, string label, LookMode lookMode = LookMode.Undefined);

// ── 目标引用（格子 / Thing / Map / 世界）── 每型 4 重载
public static void Scribe_TargetInfo.Look(ref LocalTargetInfo value, string label);
public static void Scribe_TargetInfo.Look(ref LocalTargetInfo value, bool saveDestroyedThings, string label, LocalTargetInfo defaultValue, bool preserveDefaultValues = false);
// TargetInfo / GlobalTargetInfo 同构
```

> **`label` 是兼容契约**：它是写入 `.rws` 的 XML 标签名，改名 = 旧档读不到。同一 `ExposeData()` 内**必须唯一**（重复 = 静默数据丢失，最隐蔽的坑）。

### 12.2 `LookMode` ↔ `Scribe_*` 语义映射（9 值）

| `LookMode` | 等价 Scribe 方法 | 适用 |
|---|---|---|
| `Undefined` | —（须显式指定，否则报错） | — |
| `Value` | `Scribe_Values` | 基础类型/枚举/结构体/string |
| `Deep` | `Scribe_Deep` | 自己字段内、未在别处保存的 `IExposable` |
| `Reference` | `Scribe_References` | 已生成/别处已保存的 `ILoadReferenceable` |
| `Def` | `Scribe_Defs` | 任何 `Def` 引用 |
| `LocalTargetInfo` | `Scribe_TargetInfo` | 单地图格子或 Thing |
| `TargetInfo` | `Scribe_TargetInfo` | 单地图格子/Thing + Map |
| `GlobalTargetInfo` | `Scribe_TargetInfo` | 世界 tile / WorldObject |
| `BodyPart` | 专用 | 身体部件引用 |

**选择口诀（社区 + 程序集一致）**：对象**已生成在地图上**或**已在别的存档容器里** → `Reference`；**只存在于你自己的字段里** → `Deep`；是 `Def` → `Defs`；是值/结构 → `Value`。

### 12.3 保存 / 读取生命周期（★ 本文件实证修正项）

```csharp
public enum LoadSaveMode : byte { Inactive, Saving, LoadingVars, ResolvingCrossRefs, PostLoadInit }
```

| 阶段 | `ExposeData()` 是否被调用 | 实际行为 |
|---|---|---|
| `Saving` | **1 次** | `Scribe_*` 写 XML |
| `LoadingVars` | **第 1 次** | 解析 XML：Value/Def 立即取值；Reference **只登记待解析请求**（拿到 ID 字符串）；Deep 递归加载 |
| `ResolvingCrossRefs` | **不调用** | `CrossRefHandler.ResolveAllCrossReferences()`：注册 `loadedObjectDirectory` → 统一填充所有挂起引用；找不到 → 置 `null` + 警告日志 |
| `PostLoadInit` | **第 2 次** | `PostLoadIniter.DoAllPostLoadInits()`：`Scribe.mode = PostLoadInit` → 遍历待初始化集合 → **再次调用每个 `item.ExposeData()`**（逐个 try/catch，失败不中断整体） → 恢复 `Inactive` |

> **⚠️ 实证修正**：社区资料（`Dev-Jahn/rimworld-modding-reference`）称「读取时 `ExposeData()` 执行 3 遍（含 `ResolvingCrossRefs`）」。经反编译核实：`ResolveAllCrossReferences()` **不调用** `ExposeData`，只有三个 mode 赋值与引用填充；真正重复调用的是 `DoAllPostLoadInits()`。**正确结论：保存 1 次，读取 2 次**（`LoadingVars` + `PostLoadInit`）。以程序集为准。

**加载顺序**（引用可先于引用者）：`Game.ExposeData` → `World.ExposeData`（WorldComponents/派系/world pawns）→ 逐地图 `Map.ExposeData`（Things → MapComponents）→ `ResolveAllCrossReferences()` → `DoAllPostLoadInits()`。

### 12.4 原版自检报错速查（用错 Scribe 方法时官方会拦）

`Scribe_Values.Look` 在 `Saving` 阶段内建类型检查，命中即 `Log.Error`（原文）：

| 你保存的类型 | 原版报错 | 正确方法 |
|---|---|---|
| `TargetInfo` | `Saving a TargetInfo ... with Scribe_Values. TargetInfos must be saved with Scribe_TargetInfo.` | `Scribe_TargetInfo.Look` |
| `Thing` 派生 | `Using Scribe_Values with a Thing reference ... Use Scribe_References or Scribe_Deep instead.` | `Reference`（已生成）/ `Deep` |
| `IExposable` | `Using Scribe_Values with a IExposable reference ...` | `Scribe_Deep` / `Scribe_References` |
| `Def` 派生 | `Using Scribe_Values with a Def ... Use Scribe_Defs instead.` | `Scribe_Defs.Look` |

另：`Scribe_Values` 在 `value.Equals(defaultValue) && !forceSave` 时**跳过写入**（存档瘦身），故"字段没出现在 `.rws` 里"通常不是 bug。

### 12.5 `PostLoadInit` 标准范式（三用途）

```csharp
public override void ExposeData()
{
    base.ExposeData();
    Scribe_Values.Look(ref myInt, "myInt", 0);
    Scribe_Collections.Look(ref myList, "myList", LookMode.Value);
    Scribe_References.Look(ref myPawn, "myPawn");

    if (Scribe.mode == LoadSaveMode.PostLoadInit)   // ① 补集合 ② 修悬空引用 ③ 重建缓存
    {
        myList ??= new List<string>();
        if (myPawn != null && myPawn.Destroyed) myPawn = null;
        RebuildLookupTable();
    }
}
```

### 12.6 集合专项（原版行为，必知）

1. **空集合通常不写入存档** → 读回来是 `null`，**不是**空集合 → 必在 `PostLoadInit` 里 `??=`。
2. **引用集合加载后可能含 `null` 元素**（被引用对象已销毁） → 消费前逐项判空。
3. **字典的键或值任一侧用 `LookMode.Reference` 时，必须传两个工作列表** `ref List<K> keysWorkingList, ref List<V> valuesWorkingList`（两阶段解析需要缓冲区）。本工程 `ChronicleGameComponent` 的别名表用 `LookMode.Value, LookMode.Value` 双值形式，规避了该约束。

### 12.7 离线 Scribe 往返测试驱动法（★ 2026-09-21 实证，`Tests/StockSeriesScribeTests.cs` 已落地）

**目标**：在**无游戏运行时**的 NUnit 里驱动真实 Scribe 管线，兜住"读档静默失败"（读回 null ⇒ 面板永远空态、不报错、不崩 —— 纯靠实机目视极难发现）。

**四步驱动（顺序即纪律）**：

```csharp
Scribe.saver.InitSaving(path, "savegame");   // ① Init* 自管理 mode：**不要预置 Scribe.mode**
Scribe_Deep.Look(ref obj, "label");          // ② ExposeData 第 1 次（写 XML）
Scribe.saver.FinalizeSaving();
Scribe.loader.InitLoading(path);             // ③ 同样不要预置 mode
Scribe_Deep.Look(ref loaded, "label");
Scribe.loader.FinalizeLoading();             // 含 ResolveAllCrossReferences + DoAllPostLoadInits
```

**三条硬约束（缺一即"静默 null"）**：

1. **不得预置 `Scribe.mode`** —— `InitSaving` / `InitLoading` 开头即
   `if (Scribe.mode != Inactive) { Log.Error(...); Scribe.ForceStop(); }`（ilspycmd 实证）。
   预置会被守卫打成 ForceStop：表现为整条读档返回 null，且 `Log.Error` 若被吞掉就完全无线索。
2. **非框架游戏 dll 必须全在测试输出目录** —— `ScribeExtractor.SaveableFromNode` 会调
   `GenTypes.GetTypeInAnyAssembly` ⇒ `Assembly.GetTypes()` 枚举 `Assembly-CSharp` **全类型**；
   缺任一依赖（典型 `com.rlabrecque.steamworks.net`）⇒ `ReflectionTypeLoadException` /
   `FileNotFoundException` 被吞成 `Log.Error` + `target = default`。
   本仓做法 = `Tests/*.csproj` 的 `CopyGameDependencies`（批量拷 `Managed\*.dll`，
   排除 `mscorlib` / `System.*` / `Mono.Security`，避免与 net48 宿主冲突）。
3. **Unity ECall 需短接** —— 管线内会调 `Verse.Log.*`（→ `UnityEngine.Debug.Log*`，离线抛
   "ECall 方法必须打包到系统模块中"）与 `ScribeLoader.FinalizeLoading` 里的
   `Verse.DeepProfiler.Start/End`（离线 NRE）；用 Harmony 前缀 `return false` 短接。
   ⚠ **日志建议捕获而非丢弃**（本仓 `logCapture` 模式）：失败时随断言消息输出，定位一次到位。

**推荐断言面**：多品种 × 六档 **逐根全等**（开/高/低/收/样本数）+ 派生值（涨跌幅）+ 进行中桶；
外加两条契约用例：**空集合往返**（空集合不写档 ⇒ 读回 null ⇒ `??=` 归一）与
**旧档缺段**（目标保持 null 且**不抛** —— Data 层负责归一为空集合）。

---

## 13. 数据宿主标准（GameComponent 与选型）

### 13.1 原版 `Verse.GameComponent` 生命周期（7 个虚方法）

```csharp
public abstract class GameComponent : IExposable {
    public virtual void ExposeData();        // 存档读写（§12）
    public virtual void FinalizeInit();      // 新档与读档都会调用 —— ★迁移链的正确落点
    public virtual void StartedNewGame();    // 仅新档
    public virtual void LoadedGame();        // 仅读档
    public virtual void GameComponentTick(); // 每 tick
    public virtual void GameComponentUpdate();// 每帧
    public virtual void GameComponentOnGUI(); // 每帧 GUI
    public virtual void AppendDebugString(StringBuilder sb);
}
```

调用由 `Verse.GameComponentUtility` 统一分发（`GameComponentTick/Update/OnGUI`、`FinalizeInit`、`StartedNewGame`、`LoadedGame`）。

### 13.2 宿主选型表

| 需求 | 宿主 | 生命周期差异 |
|---|---|---|
| 全档案数据（跨地图、跨世界） | **`GameComponent`** | 随 `Game` 序列化，唯一全局根 |
| 世界级数据 | `WorldComponent` | 随 `World` 序列化 |
| 单地图数据 | `MapComponent` | 随地图序列化 |
| 挂在 Def 实例的行为 | `ThingComp` / `HediffComp` | `CompExposeData` / `PostExposeData` |
| 自己的复合对象 | 自定义 `IExposable` | 必须挂在某个被保存的宿主下（`Scribe_Deep`） |

> 社区结论（HugsLib Wiki 原文）：**优先 `WorldComponent` / `GameComponent`**，不再用 `UtilityWorldObjects` 之类自研容器。

### 13.3 本工程 `ChronicleGameComponent` 映射与技巧

| 原版钩子 | 本工程用法 |
|---|---|
| `ExposeData()` | `base.ExposeData()` → `Scribe_Values.Look(ref SchemaVersion, "schemaVersion", 0L)` → 全部集合 `LookMode.Deep` / 值表双 `LookMode.Value` → `PostLoadInit` 分支（`DataRevision = 0L; cameFromLoad = true;`）+ 所有字典 `??=` 兜底 |
| `FinalizeInit()` | **迁移链落点**（`SchemaVersion < 1..7` 顺序幂等迁移）+ `RebuildIndexes()` + `BackfillExistingColonists()` + 清理跨会话静态累加器 |
| `GameComponentTick()` | 周期采样（`ReconcileIntervalTicks = 600`、`WorkSampleIntervalTicks = 120`）+ `MarkChanged()` → `DataRevision++` |
| `ExposeData` 中的 `Saving` 分支 | 生成 v0.2 降级镜像 `legacyPawns`（`BuildLegacyPawnMirror()`，永不作为真源） |

**`[Unsaved]` 机制（本工程标准做法）**：所有运行期索引/节流状态标记 `[Unsaved]`，不进存档、读档后由 `RebuildIndexes()` 重建。清单：`objectsByStableId`、`eventsByObject`、`relationScanMissStreak`、`reconcileCandidates`、`cameFromLoad`。

**`cameFromLoad` 技巧（可复用范式）**：新档**不会**进入 `PostLoadInit`，读档**会** —— 因此 `if (Scribe.mode == LoadSaveMode.PostLoadInit) cameFromLoad = true;` 是判定"本次是读档"的**权威信号**，比 `TicksGame <= 0` 可靠（新档场景初始化会推进 tick，`FinalizeInit` 时常已 > 0）。

---

## 14. 存档兼容与迁移标准

### 14.1 存档文件结构（`.rws`，人类可读 XML）

```xml
<savegame>
  <meta>
    <gameVersion>1.5.4104 rev435</gameVersion>
    <modIds>    <li>ludeon.rimworld</li> <li>author.mymod</li> </modIds>
    <modNames>  <li>Core</li> <li>My Mod</li> </modNames>
    <modSteamIds>...</modSteamIds>
  </meta>
  <game>
    <components><li Class="MyNamespace.MyGameComponent"><myField>42</myField></li></components>
    <world>...</world>
    <maps><li><uniqueID>0</uniqueID><things>...</things><components/>...</li></maps>
  </game>
</savegame>
```

原版 `ScribeMetaHeaderUtility` 常量：`meta` / `gameVersion` / `modIds` / `modNames` / `modSteamIds`；静态状态含 `loadedGameVersion`、`modListChanged`，并提供 `TryCreateDialogsForVersionMismatchWarnings(...)`、`LoadedModsMatchesActiveMods(...)` —— **Mod 增删的兼容性检查由原版完成**，Mod 只需保证"缺失数据可降级"。

Mod 的组件段位置：`<components><li Class="命名空间.类型名">` —— 调试时直接搜类名即可定位自研数据是否落盘。

存档目录：`%AppData%\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Saves\`；`ModSettings` 在 `Config\Mod_<packageId>_*.xml`（**与存档分离**，同样走 Scribe）。

### 14.2 原版版本兼容判定（`BackCompatibility.IsSaveCompatibleWith`，实证逻辑）

```
同 Major 且同 Minor                      → 兼容
Major ≥ 1 且同 Major 且 Minor ≤ 当前Minor → 兼容（向后读旧档）
Major == 0 且当前 Major == 0             → 查 SaveCompatibleMinorVersions 白名单
其余                                     → 不兼容
```

原版配套机制：`PreLoadSavegame/PostLoadSavegame`、`BackCompatibleDefName`（旧 defName 映射）、`BackCompatibleEnum`、`GetBackCompatibleType`、`WasDefRemoved`、`PostExposeData(object)`。**启示**：Mod 自身的 schema 演进应模仿此模式 —— 版本号 + 映射表 + 逐条降级，而非让整个存档加载失败。

### 14.3 规范条款映射（`03-架构层-数据与扩展规范`）

| 规则 ID | 要点 | 本文件落地位置 |
|---|---|---|
| DATA-010 | 数据契约版本（如 `archive.events.v1`） | §5 快照 / §14.4 `SchemaVersion` |
| DATA-011 | 数据与本地化隔离（不存翻译文本） | DL-14 · DL-22 · §5 DL-7 |
| **SAVE-001** | 存档身份与版本；DefName/事件类型/对象 ID/枚举值写入即契约 | §3.1 `SchemaVersion` · §4.4 常量键 |
| **SAVE-002** | 可持久化边界：ID/基础值/有限集合/时间版本 ✅；Window/Rect/GUIStyle/Texture/Provider/缓存/翻译结果 ❌ | DL-22 |
| **SAVE-003** | 新增字段必须有默认值；未知枚举/缺失 Def/缺失 Mod/损坏单条 → 降级为可诊断空值，**不阻断整体加载** | DL-21 · §12.5 |
| **SAVE-004** | 迁移流程：读旧 schema → 识别版本 → 顺序执行**幂等** Migration → 校验目标 schema → 记录摘要与失败项 → 写当前格式 | §14.4 |
| **SAVE-005** | 重命名/删除须留别名映射，支持读旧值 ≥1 兼容周期；删除前有 Deprecated + Migration + Changelog `Breaking/Removed` | DL-24 · §12.1 label 契约 |
| **SAVE-006** | 幂等 + 失败隔离；迁移可单独测试；日志含旧/新版本、对象 ID、失败原因 | §14.4 |
| **SAVE-007** | Save 测试矩阵（见 §14.5） | §14.5 |
| SAVE-008 | 验收清单 | §9 红线 + §14.5 |

### 14.4 本工程迁移链现状与编写规范

**现状（已实现，`ChronicleGameComponent.FinalizeInit`）**：`public long SchemaVersion` 由 `Scribe_Values.Look(ref SchemaVersion, "schemaVersion", 0L)` 持久化，按 `SchemaVersion < N` 顺序执行 0→7 共 7 级迁移，每级成功即 `SchemaVersion = N`（**幂等，永不重复执行**）：

| 版本 | 迁移内容 |
|---|---|
| 0 → 1 | `MigrateLegacyData()`（v0.2 → v3 archive schema） |
| 1 → 2 | `PruneInitialRosterArtifacts()`（场景编辑器候选人误建档清理） |
| 2 → 3 | `PruneUnconfirmedRosterArtifacts()`（更严格的未确认名册清理） |
| 3 → 4 | 社会关系回填（**条件 latch**：`AllArchivedColonistsHaveRelations()` 满足才写版本，否则下轮重试） |
| 4 → 5 | 地点档案收敛（关闭未到访 settlement/quest site，`DeinitReason="Unvisited"`） |
| 5 → 6 / 6 → 7 | 见 `ChronicleGameComponent.cs`（同模式） |

**新增一级迁移的规范动作**（DL-25）：

```
① 目标版本 +1，写在链尾（永远用 `if (SchemaVersion < N)`，不用 else-if 串）
② 只做"从 N-1 到 N"的增量修复；能条件 latch 的（依赖数据齐备）就不要无脑写版本
③ 迁移函数必须幂等：重复执行结果一致，不追加记录
④ 迁移内不得依赖 UI、当前语言、第三方 Mod 是否安装（SAVE-004）
⑤ 破坏性清理要先判"有历史/有引用则保留"，再关闭或删除（参照 1→2/2→3/4→5）
⑥ 迁移后如索引结构变了，立即 `RebuildIndexes()`
⑦ 记录：版本、处理对象 ID、失败原因（SAVE-006）
⑧ 更新 §14.4 本表 + Changelog；若改变字段含义/删除字段 → Release/Save Review
```

### 14.5 Save 测试矩阵（SAVE-007 强制项）

```
[ ] 当前版本新建存档 → 保存 → 读取（基线）
[ ] 上一发布版本存档 → 读取（字段默认值 + 迁移链）
[ ] 更早版本存档（若有兼容承诺）
[ ] 缺失字段 / 未知字段 / 未知枚举值
[ ] 目标 Mod 存在 / 缺失（Provider 降级，DL-9）
[ ] 语言切换后读取（不得出现存档内翻译残留，DL-14）
[ ] 重复读取同一存档（幂等：不追加记录、不改结果）
[ ] 校验：`Mods\<Mod>\Assemblies` dll 与开发区哈希一致后再实测
```

---

## 15. 资料来源与可信度分级

### 15.1 分级（冲突时高优先级覆盖低优先级）

| 级 | 来源 | 采信规则 |
|---|---|---|
| **P0** | **asm 实证**：`ilspycmd` 反编译 `Assembly-CSharp.dll`（本机 1.6 程序集） | 最终权威；与任何文档冲突以它为准（本节证伪记录即例） |
| **P1** | 官方随包资料：`Data/` Def XML、`Version.txt`、游戏内 DevMode 工具 | 用于 Def/机制事实核对 |
| **P2** | 社区文档：RimWorld Wiki、`spdskatr/RWModdingResources`、`Dev-Jahn/rimworld-modding-reference`、HugsLib Wiki、Ludeon 论坛 | 提供范式与陷阱清单；**机制断言须 P0 复核** |
| **P3** | 本工程 MEMORY / 设计文档 / 经验 | 仅适用于本项目；机制部分同样须 P0 复核 |

项目内既有约束（源自 `00-规范体系总纲` 与知识库 skill）：**RimWorld 没有官方 Mod SDK，API 事实以目标 Build 的程序集反射/反编译为准。**

### 15.2 本次证伪记录（社区资料 vs 程序集）

| 断言 | 来源 | 实证结论 |
|---|---|---|
| 读取时 `ExposeData()` 执行 **3 遍**（含 `ResolvingCrossRefs`） | Dev-Jahn save-load 文档（P2） | ❌ **2 遍**：`LoadingVars` + `PostLoadInit`；`ResolveAllCrossReferences()` 只填引用、不调 `ExposeData`（P0，见 §12.3） |
| `Scribe_Values` 可保存任意类型 | 常见新手误解 | ❌ 原版对 Def/Thing/IExposable/TargetInfo 主动 `Log.Error`（P0，见 §12.4） |

### 15.3 存档问题排查手段（社区 P2 + 程序集 P0 交叉）

| 症状 | 排查动作 |
|---|---|
| 保存后读档字段回默认 | ① 看 `Player.log` 是否有 `Exception while saving`（保存线程 abort → 后续数据静默丢失）② 搜 `.rws` 是否有该 label —— **无 label = `Equals(defaultValue)` 被跳过或写错 label** |
| 读完档数据为 `null` | 集合空 → 未写档；引用对象已销毁 → `null` + 警告日志；Def 被移除 → `null` |
| 引用解析失败 | 日志会出现 `ResolvingCrossRefs` 阶段的警告（找不到 load ID） |
| 自研数据没落盘 | 在 `.rws` 搜 `<components><li Class="你的类名">` |
| 读档行为异常但数据对 | 检查 `PostLoadInit` 是否漏了缓存重建 / 悬空引用修复 |
| Mod 增删导致加载异常 | 看 `<meta>` 的 `modIds/modNames` 与原版不一致告警对话框 |


## 16. 附录：文件规模与位置

| 层 | 路径 | 文件数 | 定位 |
|---|---|---|---|
| Api | `Source/PersonalChronicle/Api/` | 19 | 对外扩展面（Provider/EventSink/QueryService） |
| Application | `Source/PersonalChronicle/Application/` | 44 | 服务 + Live View + **ReadModels/** |
| Archive（UI） | `Source/PersonalChronicle/Archive/` | 58 | 主页签/页签/对话框/开发工具 |
| Automation | `Source/PersonalChronicle/Automation/` | 33 | 车间/农机（独立子系统） |
| Capture | `Source/PersonalChronicle/Capture/` | 22 | Harmony 补丁（只写） |
| Data | `Source/PersonalChronicle/Data/` | 9 | 存档根 + 查询 |
| Domain | `Source/PersonalChronicle/Domain/` | 72 | 模型 + Def + 常量 + Evaluator |

> 注：`Effects/` 目录当前为空；顶层另有 `ChronicleLog` / `ChronicleMod` / `ChronicleSettings` / `PerfProbe` 4 个 .cs。

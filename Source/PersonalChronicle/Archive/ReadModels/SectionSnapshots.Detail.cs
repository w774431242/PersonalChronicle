// Phase 1 partial split (08-架构层-代码轻量化方案.md §3.4): Detail + sub-views (pawn/workplace/medal/legacy/...).
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// v1.1.4 劳模住所/工坊检测：工作场所展示视图。只承载已解析的展示文本
    /// （BuildingDefName 稳定键 → LabelCap 实时解析 + 所在房间角色 label），
    /// 窗口只消费，不重新解析 DefDatabase。
    /// </summary>
    public sealed class WorkplaceView
    {
        /// <summary>工坊建筑 ThingDef.defName（稳定键，供 Debug/tooltip 用）。</summary>
        public string BuildingDefName;
        /// <summary>v1.1.4 工坊实例稳定键（defName:thingIDNumber），供 UI 写全局别名表。</summary>
        public string BuildingStableId;
        /// <summary>工坊显示名：优先 <see cref="CustomName"/>（建筑别名），否则 ThingDef.LabelCap 实时解析名。</summary>
        public string BuildingLabel;
        /// <summary>v1.1.4 建筑别名（玩家自定义名；null/空 = 未改名）。</summary>
        public string CustomName;
        /// <summary>工坊所在房间角色 label（如「工坊」/「厨房」；null = 未知/户外）。</summary>
        public string RoomRoleLabel;
        /// <summary>累计使用（制造完成迭代）次数。</summary>
        public int UseCount;
        /// <summary>最近一次使用的 game tick（-1 = 从未使用）。</summary>
        public long LastUsedTick = -1L;
        /// <summary>工坊所在地图索引（-1 = 未记录）。</summary>
        public int MapIndex = -1;
        /// <summary>工坊位置坐标（无效 = 未记录）。</summary>
        public IntVec3 Cell;
        /// <summary>是否为空（从未捕获到工作场所）。</summary>
        public bool IsEmpty;
    }

    /// <summary>
    /// v1.1.4 劳模住所/工坊检测：住所展示视图。展示层优先级：
    ///   1. RoomNameOverrides[pawnId:role]  整间房昵称 → RoomRoleLabel
    ///   2. RoomTypeOverrides[pawnId:role]  类型名替换 → RoomTypeName
    ///   3. RoomRoleAliases[role]           类型级全局
    ///   4. RoomRoleDef.LabelCap            原版兜底
    /// </summary>
    public sealed class ResidenceView
    {
        /// <summary>住所房间角色 RoomRoleDef.defName（稳定键）。</summary>
        public string RoomRoleDefName;
        /// <summary>住所显示名（最高优先级，房间级整间房昵称；null = 回落类型名）。</summary>
        public string RoomRoleLabel;
        /// <summary>类型名替换（房间级，如「工作间」→「工坊类型」；null = 回落全局/原版）。</summary>
        public string RoomTypeName;
        /// <summary>最近一次采样确认在住所的 game tick（-1 = 从未确认）。</summary>
        public long LastSeenTick = -1L;
        /// <summary>住所所在地图索引（-1 = 未记录）。</summary>
        public int MapIndex = -1;
        /// <summary>住所中心坐标（无效 = 未记录）。</summary>
        public IntVec3 Cell;
        /// <summary>是否为空（暂无/无归属房间）。</summary>
        public bool IsEmpty;
    }

    /// <summary>
    /// v1.1.4 勋章体系：单枚勋章展示视图。Read Model 派生（<see cref="ArchiveUiDataProvider.BuildMedals"/>）：
    /// Label/Desc/BuffText 在此解析翻译键，窗口只消费。§6.9 等级规则：
    /// 同 <see cref="SeriesKey"/>（称号）只显示最高已达档位，由 <see cref="IsHighestTier"/> 标记。
    /// </summary>
    public sealed class MedalView
    {
        /// <summary>MedalDef.defName（稳定键，含 tier 后缀，如 Medal_Labor_Model_Bronze）。</summary>
        public string DefName;

        /// <summary>勋章称号（已翻译 UI.Medal.&lt;defName&gt;.Label，如「劳动模范·铜质」）。</summary>
        public string Label;

        /// <summary>勋章描述（已翻译 UI.Medal.&lt;defName&gt;.Desc，tooltip）。</summary>
        public string Desc;

        /// <summary>材质档位（Bronze/Silver/Gold）。</summary>
        public MedalTier Tier;

        /// <summary>称号分组键（defName 去掉 tier 后缀，如 Medal_Labor_Model），用于「只显示当前档」归并。</summary>
        public string SeriesKey;

        /// <summary>是否可判定（metricKey 已被当前阶段支持）。</summary>
        public bool IsApplicable;

        /// <summary>已授予（GrantedMedals 含此 defName，append-only 历史）。</summary>
        public bool IsGranted;

        /// <summary>当前累计值达标（可能尚未写入授予记录）。</summary>
        public bool IsMet;

        /// <summary>达标且未授予（任务 4 授勋/「新」标记依据；判定引擎 IsNewAward 原样透传）。</summary>
        public bool IsNewAward;

        /// <summary>同 SeriesKey 下最高已达档位（已授予或当前达标即视为已达；UI 只画该枚）。</summary>
        public bool IsHighestTier;

        /// <summary>通道 B 展示增益文案（MedalBuffDef.displayBonus 已翻译，如「+3% 工作速度」；null=无）。</summary>
        public string BuffText;

        /// <summary>当前累计值（ReadModel 从 PawnObject 映射）。</summary>
        public double CurrentValue;

        /// <summary>达标阈值（MedalDef.threshold）。</summary>
        public double Threshold;

        /// <summary>进度 0-1（CurrentValue/Threshold，已 clamp）。</summary>
        public float Progress;
    }

    public sealed class DetailSnapshot : ArchiveSectionSnapshot
    {
        public ArchiveObject DetailObject;
        /// <summary>
        /// Pre-sorted ascending by tick with nulls removed (assembled in
        /// <see cref="ArchiveUiDataProvider.BuildDetail"/>). The window draws from
        /// this single immutable source instead of re-querying / re-sorting per tab.
        /// </summary>
        public IReadOnlyList<ChronicleEvent> RawEvents = new List<ChronicleEvent>();

        /// <summary>
        /// Production ledger lines (crafted/built items grouped by defName, sorted
        /// by most-recent first). Derived in the Read Model; the window only maps it.
        /// </summary>
        public IReadOnlyList<ProductionLineView> ProductionLines = new List<ProductionLineView>();

        /// <summary>v4.15 condense-tab: top production categories (first-level
        /// ThingCategoryDef) for the 产出 cell badge group. Aggregated in the Read
        /// Model; the window only renders the labels (no re-derivation).</summary>
        public IReadOnlyList<ProductionCategoryView> ProductionCategories = new List<ProductionCategoryView>();

        // --- v4.4 Pawn Overview derived content (architecture §3.1 G layer:
        // UI consumes these read-only views only; all derivation lives here,
        // never in the draw path). All fields are runtime-derived, never saved. ---

        /// <summary>Lifecycle stages (origin/join/active/death) in narrative order.</summary>
        public IReadOnlyList<LifePhaseView> LifePhases = new List<LifePhaseView>();

        /// <summary>Top work-type share bars for the career portrait.</summary>
        public IReadOnlyList<CareerBarView> CareerBars = new List<CareerBarView>();

        /// <summary>Footprint ledger: place-visit summary + sorted stays.</summary>
        public FootprintLedgerView Footprint = new FootprintLedgerView();

        /// <summary>Milestone cards (one representative event per kind).</summary>
        public IReadOnlyList<MilestoneView> Milestones = new List<MilestoneView>();

        /// <summary>Key events by salience (top-N, chronological).</summary>
        public IReadOnlyList<KeyEventView> KeyEvents = new List<KeyEventView>();

        /// <summary>
        /// Social ties snapshot (v4.6). Merges live direct relations with archived
        /// historical/initial relations so the ITab digest can render spouse, parent,
        /// friend, etc. even after the pawn has died or left the colony.
        /// </summary>
        public IReadOnlyList<RelationView> Relations = new List<RelationView>();

        // --- v4.15 condense tab core KPI (architecture §3.1 G layer: the ITab digest
        // renders these six cells only; every counter is aggregated here in the Read
        // Model, never in the draw path). All fields runtime-derived, never saved. ---

        /// <summary>集中工时评估（日均工时 + 强度档位）。来自 WorkIntensityEvaluator；可能为 null（未定义）。</summary>
        public WorkIntensityView WorkIntensity;

        /// <summary>该人物累计击杀数（Death 事件 CombatRole=kill 且凶手匹配）。</summary>
        public int Kills;

        /// <summary>战役数（Battle 事件为 colony 级、不绑定单 pawn 参与者，故显示殖民地战役总数作为时代背景）。</summary>
        public int BattleCount;

        /// <summary>v4.15 condense-tab: colony-level Battle KPI strip (5 cells) shown
        /// as this pawn's era context. Aggregated once in the Read Model via
        /// <see cref="ArchiveUiDataProvider.BuildBattleKpis"/>; the window only renders.</summary>
        public BattleKpisView BattleKpis = new BattleKpisView();

        /// <summary>传承后代计数（Relations 中仍存活的亲子/后代关系）。</summary>
        public int LegacyOffspring;

        /// <summary>累计产出件数（ProductionSummaryView.TotalQuantity）。</summary>
        public int ProductionTotal;

        /// <summary>累计产出折算银币总额（ProductionSummaryView.TotalMarketValue）。</summary>
        public float ProductionSilverValue;

        /// <summary>
        /// 近 7 天产出折算银币（周产出）。从 ChronicleEvent 流扫描近 7 日的 Craft/Built
        /// 事件，按 ThingDef.BaseMarketValue 估算。老存档无事件时取 0。
        /// </summary>
        public float WeeklyProductionSilver;

        /// <summary>
        /// 近 30 天产出折算银币（净值）。同周产出算法但窗口扩到 30 天，作为较长周期的
        /// 滚动参考；与周产出互补，避免与累计值重复。
        /// </summary>
        public float MonthlyProductionSilver;

        /// <summary>v4.15 产出宫格真实数据源：来自 <see cref="ProductionAccumulator"/>
        /// 持久化累计（每次 Craft/Built 捕获时累加 Def.MarketValue*quantity），已按
        /// 类目聚合、按产值降序。宫格 bars 与种类数均消费此快照，不再重扫事件流。</summary>
        public IReadOnlyList<ProductionTypeView> ProductionTypeViews = new List<ProductionTypeView>();

        /// <summary>v1.1.4 损耗宫格：累计消耗品银币（ConsumptionAccumulator.TotalSilver）。</summary>
        public float ConsumptionTotalSilver;

        /// <summary>v1.1.4 损耗宫格：近 7 天消耗银币（ConsumptionAccumulator.SilverByDay 求和）。</summary>
        public float WeeklyConsumptionSilver;

        /// <summary>v1.1.4 损耗宫格：近 30 天日均消耗银币（月累计 / 30）。</summary>
        public float DailyConsumptionSilver;

        /// <summary>v1.1.4 损耗宫格：类目构成（ThingCategoryDef.defName → 累计银币），已按银币降序。</summary>
        public IReadOnlyList<ConsumptionTypeView> ConsumptionTypeViews = new List<ConsumptionTypeView>();

        /// <summary>v4.15 condense-tab: kills grouped by victim faction/category
        /// (e.g. 帝国/部落/机械族/动物). Aggregated in the Read Model from Death
        /// events where this pawn is the killer; the window only renders labels.</summary>
        public IReadOnlyList<KillByFactionView> KillsByFaction = new List<KillByFactionView>();

        /// <summary>v6.8 个人战斗维度：该人物累计参战的战役数（持久化累加，非事件流推导）。</summary>
        public int ParticipatedBattles;

        /// <summary>v6.8 个人战斗维度：生涯累计造成伤害总值（持久化累加，近似）。</summary>
        public float DamageDealtTotal;

        /// <summary>
        /// v6.8 个人战斗维度：近战击杀占比（0-1）。由持久化 MeleeKills/(MeleeKills+RangedKills) 推导；
        /// 无任何击杀时取 0.5（中性占位，UI 侧显示"暂无战斗记录"由 hasMeleeData 控制）。
        /// </summary>
        public float MeleeKillRatio;

        /// <summary>v6.8 个人战斗维度：近战击杀数（持久化）。</summary>
        public int MeleeKills;

        /// <summary>v6.8 个人战斗维度：远程击杀数（持久化）。</summary>
        public int RangedKills;

        /// <summary>
        /// v1.1.4 劳模住所/工坊检测：工作场所视图（方案 A 捕获 Bill_Production → 工坊 defName）。
        /// 只消费已解析展示文本；窗口不重新解析 DefDatabase。
        /// </summary>
        public WorkplaceView Workplace = new WorkplaceView();

        /// <summary>
        /// v1.1.4 劳模住所/工坊检测：住所视图（方案 B 定期采样 OwnedRoom.Role）。
        /// 只消费已解析展示文本；窗口不重新解析 DefDatabase。
        /// </summary>
        public ResidenceView Residence = new ResidenceView();

        /// <summary>
        /// v1.1.4 勋章体系：勋章墙视图（阈值类，§6.1~6.4）。Read Model 派生
        /// （<see cref="ArchiveUiDataProvider.BuildMedals"/>）；窗口只消费，不重判定。
        /// 未授予但可判定的勋章也包含（灰态 + 进度），由 IsHighestTier 标记当前档。
        /// </summary>
        public IReadOnlyList<MedalView> Medals = new List<MedalView>();

        /// <summary>
        /// "健康残值 · 资产折旧" view — composite health score, depreciated silver
        /// value, and the positive/negative factors behind it. All derivation lies
        /// in <see cref="ArchiveUiDataProvider"/>; the window only renders this.
        /// </summary>
        public HealthView Health = new HealthView();

        /// <summary>
        /// Legacy chain (传承) for weapon/equipment detail — ownership-transfer
        /// generations, creator, verdict, and the holder table. Derived in the
        /// Read Model; the window renders it without re-aggregation.
        /// </summary>
        public LegacyView Legacy = new LegacyView();

        // --- v4.9 equipment legacy extension (all derived in the Read Model,
        // the window only renders; every view degrades to an empty state). ---

        /// <summary>溯源 (origin): where the thing came from + maker chain.</summary>
        public ThingOriginView Origin = new ThingOriginView();

        /// <summary>工坊署名链 (maker chain): crafter's later fate.</summary>
        public MakerChainView MakerChain = new MakerChainView();

        /// <summary>同袍共用网络 (co-use): parallel sharers of this equipment.</summary>
        public CoUseView CoUse = new CoUseView();

        /// <summary>退役仪式 (decommission): the thing's death record.</summary>
        public DecommissionView Decommission = new DecommissionView();

        // --- v4.13 location atlas (Overview › Location): all views derived in
        // the Read Model, the window renders without re-aggregation. ---

        /// <summary>Location atlas view: identity / ownership / geography / lifecycle / commerce.</summary>
        public LocationDetailView Location = new LocationDetailView();

        /// <summary>
        /// 职业档案 · 工作经历（简历式）：按工坊就职时段分段的成果汇总。
        /// 由 <see cref="ArchiveUiDataProvider.BuildWorkExperiences"/> 从
        /// <c>PawnObject.CareerData.Events</c> + <c>WorkplaceSnapshot</c> 派生；
        /// 窗口（ITab_Pawn_Career）只消费。空列表 = 暂无工作经历（不造假）。
        /// </summary>
        public IReadOnlyList<WorkExperienceView> WorkExperiences = new List<WorkExperienceView>();

        /// <summary>
        /// 职业档案 · 总览视图（职业身份/资格状态/预检/下一职称）。
        /// 由 <see cref="ArchiveUiDataProvider.BuildCareerOverview"/> 从 CareerData 派生；
        /// 窗口只消费。HasData=false = 无职业数据（空态，不造假）。
        /// </summary>
        public CareerOverviewView CareerOverview = new CareerOverviewView();

        /// <summary>
        /// 职业事实计数（9 类事件）。由 <see cref="ArchiveUiDataProvider.BuildCareerFactCounts"/>
        /// 从 RecordCountByType 统一聚合；窗口消费，禁止绘制路径直查 Domain（UI-001 / ARC-002）。
        /// </summary>
        public CareerFactCounts FactCounts = new CareerFactCounts();

        /// <summary>
        /// 职业档案 · 职称链（5 档，对齐 Defs/QualificationDefs.xml）。
        /// 由 <see cref="ArchiveUiDataProvider.BuildTitleTiers"/> 从 DefDatabase + 授予历史派生。
        /// </summary>
        public IReadOnlyList<CareerTitleTierView> TitleTiers = new List<CareerTitleTierView>();
    }

    /// <summary>
    /// v4.13 location atlas detail view (all fields runtime-derived, never saved).
    /// Data keys + raw values; the window owns translation/formatting (its
    /// translation context). KindKey is one of "player"/"settle"/"quest"/"unknown".
    /// HillKey one of "flat"/"hilly"/"mountain"/"impassable".
    /// </summary>
    public sealed class LocationDetailView
    {
        /// <summary>Identity kind key: "player"/"settle"/"quest"/"unknown".</summary>
        public string KindKey;
        /// <summary>Faction defName (null = no man's land).</summary>
        public string FactionDefName;
        /// <summary>Biome defName.</summary>
        public string BiomeDefName;
        /// <summary>Hill key: "flat"/"hilly"/"mountain"/"impassable".</summary>
        public string HillKey;
        /// <summary>Coastal flag.</summary>
        public bool IsCoastal;
        /// <summary>Pollution flag (pollution > 0).</summary>
        public bool IsPolluted;
        /// <summary>Annual mean temperature °C (NaN = unknown).</summary>
        public float AvgTempC = float.NaN;
        /// <summary>Founded tick (from EstablishedTick). -1 = unknown.</summary>
        public long EstablishedTick = -1L;
        /// <summary>Lifecycle: true = still active (DeinitTick == -1).</summary>
        public bool IsActive;
        /// <summary>Ruined reason text key when closed ("Destroyed"/"Abandoned"), null when active.</summary>
        public string DeinitReasonKey;
        /// <summary>Tradable city flag.</summary>
        public bool CanTrade;
        /// <summary>Permit defName (null = none).</summary>
        public string PermitDefName;
        /// <summary>Main sell-category keys (subset of the 8 canonical keys).</summary>
        public IReadOnlyList<string> TradeKindKeys = new List<string>();
    }

    /// <summary>Health residual / asset depreciation view for one pawn.</summary>
    public sealed class HealthView
    {
        public bool IsDefined;
        public float HealthScore;   // 0..100 composite
        public float BodyPercent;   // 0..1 body integrity
        public int AgeYears;
        public int SilverValue;     // final depreciated value (rounded)
        public int BaseSilverValue; // before penalties/health scaling
        public int WeeklySilverEstimate; // estimated weekly silver yield (rounded)
        public bool IsPrime;        // score >= prime threshold
        public bool IsImpaired;     // score < impaired threshold

        // v4.6.1: three explicit dimension scores that drive the per-dimension bars.
        public float BodyIntegrityScore; // 0..100 (肢体完好)
        public float SpiritScore;        // 0..100 (精神饱满) — derived from mental-break proxy
        public float YouthScore;         // 0..100 (未衰老) — derived from age depreciation

        // Per-dimension factor buckets (for the hover window). Already localized at build.
        public IReadOnlyList<HealthFactorView> BodyFactors = new List<HealthFactorView>();
        public IReadOnlyList<HealthFactorView> SpiritFactors = new List<HealthFactorView>();
        public IReadOnlyList<HealthFactorView> YouthFactors = new List<HealthFactorView>();

        // Aggregate positive/negative factors (legacy top-level list, kept for tooltip).
        public IReadOnlyList<HealthFactorView> Factors = new List<HealthFactorView>();

        // v4.6.1: depreciation event log (injuries / scars / illnesses) for the timeline.
        public IReadOnlyList<HealthEventView> Events = new List<HealthEventView>();

        /// <summary>v4.14: one-line data-driven verdict (already localized), shown as
        /// the closing blurb under the health valuation block (preview's hvVerdict).</summary>
        public string VerdictText;
    }

    /// <summary>One signed valuation factor shown in the hover window.</summary>
    public sealed class HealthFactorView
    {
        public bool IsPositive;
        public string LabelText; // already localized at build time
        public int Impact;       // signed silver impact (0 when purely descriptive)
    }

    /// <summary>One depreciation event (injury / scar / illness) on the health timeline.</summary>
    public sealed class HealthEventView
    {
        public string DateText;    // localized; "Unknown" if hediff has no onset tick
        public string Description; // localized description (e.g. "断指一处 · 残值 −40 银")
        public string TagText;     // localized short tag ("掉价"/"报废"/"已康复")
        public string RawDefName;  // original HediffDef.defName for fallback display
        public int Impact;         // signed silver impact (negative = depreciate)
        public long RawTick;       // -1 when unknown
    }

    /// <summary>Semantic kind of a lifecycle phase; lets the UI pick a tint without
    /// hardcoding translation keys.</summary>
    public enum LifePhaseKind
    {
        Origin,
        Join,
        Active,
        Death,
        Unknown
    }

    /// <summary>One node on the lifecycle timeline. Date/Sub use translation keys
    /// resolved at build time; null dateText means "unknown" (mid-install JoinTick=-1).</summary>
    public sealed class LifePhaseView
    {
        public string PhaseKey;   // translation key, e.g. UI.LifePhase.Join
        public string IconKey;    // emoji/glyph rendered by UI; data-neutral here
        public string DateText;   // already localized; null when unknown
        public string SubText;    // already localized sub-info
        public bool IsUnknown;    // true when the stage has no real date (mid-install)
        public LifePhaseKind Kind;// semantic kind for color mapping
    }

    /// <summary>One work-type share bar in the career portrait.</summary>
    public sealed class CareerBarView
    {
        public string WorkTypeLabel; // localized
        public long Ticks;
        public float Share01;        // 0..1 of total work
        public bool IsPrimary;
        public bool IsSecondary;
    }

    /// <summary>Footprint summary + sorted stays (longest dwell first).</summary>
    public sealed class FootprintLedgerView
    {
        public int PlaceCount;
        public string HomePlaceText;   // localized; null when none
        public int HomeDays;
        public int ExpeditionCount;
        public IReadOnlyList<FootstepView> Stays = new List<FootstepView>();
    }

    public sealed class FootstepView
    {
        public string PlaceText;  // localized
        public bool IsWorldTile;  // caravan/expedition vs map biome
        public string DwellText;  // localized duration or "unknown"
        public long DwellTicks;   // raw span for sorting (never display)
        public bool IsHome;        // longest dwell marker
    }

    /// <summary>One milestone card (representative event of a kind).</summary>
    public sealed class MilestoneView
    {
        public string IconKey;
        public string TitleText;  // localized
        public string DateText;   // localized; may be "Unknown"
        public string SubText;    // localized one-liner
        public string KindKey;    // ChronicleEventKind name, for UI tint
        public long RawTick;      // for chronological sorting; -1 means unknown
    }

    /// <summary>One key event in the salience-ranked stream.</summary>
    public sealed class KeyEventView
    {
        public string IconKey;
        public string DateText;   // localized; may be "Unknown"
        public string TitleText;  // localized
        public string TypeText;   // localized kind label
        public bool IsHighlight;  // salience >= highlight threshold
        public int Salience;
        public long RawTick;      // for chronological sorting; -1 means unknown
    }

    /// <summary>
    /// One social tie row in the ITab digest. Labels are already localized by the
    /// Read Model so the UI only maps them to text widgets.
    /// </summary>
    public sealed class RelationView
    {
        public string OtherLabel;
        public string RelationLabel;
        /// <summary>关系稳定键（PawnRelationDef.defName；逻辑判断只用此键，禁止用显示文本）。</summary>
        public string RelationDefName;
        public string StatusLabel;
        public bool IsLive;
    }

    /// <summary>
    /// Legacy chain (传承) read model for a weapon/equipment detail view.
    /// A "generation" is an ownership transfer only ("own"); borrow/lend
    /// ("loan") records are shown for context but never counted toward the
    /// generation count, first-holder, or verdict. All derivation lives in
    /// <see cref="ArchiveUiDataProvider"/>; the window only renders this.
    /// </summary>
    public sealed class LegacyView
    {
        /// <summary>Weapon epithet (传承称号), already localized or null.</summary>
        public string TitleText;

        /// <summary>One-line data-driven verdict, already localized.</summary>
        public string VerdictText;

        /// <summary>First-owner name (already localized), null when unknown.</summary>
        public string CreatedByText;

        /// <summary>First-owner craft date (already localized), null when unknown.</summary>
        public string CreatedAtText;

        /// <summary>Generation count — only "own" records (ownership transfers).</summary>
        public int GenCount;

        /// <summary>Total kills across the whole chain (all records, incl. loans).</summary>
        public int TotalKills;

        /// <summary>Current holder label (already localized), null when none.</summary>
        public string CurrentHolderText;

        /// <summary>Sorted holder records (chronological), already localized.</summary>
        public IReadOnlyList<LegacyHolderView> Holders = new List<LegacyHolderView>();

        /// <summary>True when there is no legacy chain at all.</summary>
        public bool IsEmpty;
    }

    /// <summary>One holder row in the legacy chain table.</summary>
    public sealed class LegacyHolderView
    {
        public string HolderText;     // already localized
        public string HolderStableId; // for navigation
        public int Generation;        // 1-based for own records; 0 for loan rows
        public bool IsFirst;
        public bool IsLoan;
        public bool IsCurrent;
        public string FromText;       // already localized
        public string ToText;         // already localized
        public string DurationText;   // already localized
        public int KillCount;
        public string CreatedByText;  // already localized (usually the same as first owner)
        public string CreatedAtText;  // already localized
        public string RemarkText;     // already localized or null
    }

    /// <summary>
    /// v4.9: 溯源 (origin) read model for an equipment detail view — where the
    /// thing came from (crafted / battle-stripped / traded / salvaged / gifted)
    /// and the maker-chain double narrative (the maker later died by their own
    /// creation). Derived by the Read Model from the Craft/Built/Battle event
    /// stream; the window only renders it. All labels are already localized.
    /// </summary>
    public sealed class ThingOriginView
    {
        /// <summary>Origin kind key, already localized ("craft"/"battle"/...).</summary>
        public string KindText;
        /// <summary>Raw kind key for UI tinting (Craft/Battle/Trade/Salvage/Gift/Unknown).</summary>
        public string KindKey;
        /// <summary>Source object label (crafter / stripped corpse), already localized.</summary>
        public string FromText;
        /// <summary>Stable id of the source object, for navigation (null when anonymous).</summary>
        public string FromStableId;
        /// <summary>Place where it came from, already localized.</summary>
        public string WhereText;
        /// <summary>Origin note (from the event description), already localized.</summary>
        public string NoteText;
        /// <summary>True when no origin info can be derived.</summary>
        public bool IsEmpty;
    }

    /// <summary>
    /// v4.9: 工坊署名链 (maker chain) — the crafter's later fate tied to the
    /// equipment. Pure field association: if the crafter later died and the
    /// killing blow was dealt by this very thing, the UI shows the double
    /// narrative "this maker died by their own creation". No invented text.
    /// </summary>
    public sealed class MakerChainView
    {
        public string MakerText;      // already localized
        public string MakerStableId;  // for navigation
        public bool MakerDiedByOwn;
        public bool IsEmpty;
    }

    /// <summary>One co-use row: a colonist who shared this equipment.</summary>
    public sealed class CoUseRowView
    {
        public string PawnText;       // already localized
        public string PawnStableId;   // for navigation
        public int SharedDays;        // overlapping tenure days
        public int SharePercent;      // 0..100 of the longest sharer
    }

    /// <summary>
    /// v4.9: 同袍共用网络 (co-use) — which colonists used this equipment in
    /// parallel with the current holder (shared tenure overlap). Derived by the
    /// Read Model from HolderRecords overlap; the window only renders it.
    /// </summary>
    public sealed class CoUseView
    {
        public IReadOnlyList<CoUseRowView> Rows = new List<CoUseRowView>();
        public bool IsEmpty;
    }

    /// <summary>
    /// v4.9: 退役仪式 (decommission) — a thing's "death record". Captured
    /// read-only at destroy time (never prevents the destroy); mirrors the
    /// pawn-side OnPawnDied. All labels are already localized.
    /// </summary>
    public sealed class DecommissionView
    {
        public bool HasRecord;
        public string LastHolderText;  // already localized
        public string LastHolderStableId;
        public string LastPlaceText;   // already localized
        public int ServiceDays;
        public string LastBattleText;  // already localized
        public string DateText;        // already localized
    }

    /// <summary>
    /// 职业档案 · 工作经历（简历式）：一段 = 某工坊的就职时段 + 该期间成果。
    /// 对应现实简历「工作经历」写法（公司/岗位/在职时间/产出）。
    /// 由 <see cref="ArchiveUiDataProvider.BuildWorkExperiences"/> 从
    /// <c>PawnObject.CareerData.Events</c> + <c>WorkplaceSnapshot</c> 派生。
    /// 字段均为运行时派生，绝不持久化（append-only 契约不污染）。
    /// </summary>
    public sealed class WorkExperienceView
    {
        /// <summary>工坊显示名（DefDatabase.LabelCap 实时解析 + 全局/自定义别名优先）。</summary>
        public string WorkplaceLabel;

        /// <summary>工坊所在房间角色 label（RoomRoleDef.LabelCap + 别名优先；null = 未知/户外）。</summary>
        public string RoomRoleLabel;

        /// <summary>就职时段起止（游戏年，已本地化文本，如 "5504 – 5505"）；无数据时为 null。</summary>
        public string PeriodText;

        /// <summary>该期间成果条目（已本地化文本，如 "制造产出 320 件基础构件"）。</summary>
        public IReadOnlyList<string> Achievements = new List<string>();

        /// <summary>该期间制造总件数（ItemProduced 计数；无则为 0）。</summary>
        public int ProducedCount;

        /// <summary>该期间优秀+传奇品质件数（用于成果文案）。</summary>
        public int FineCount;

        /// <summary>该工坊累计使用次数（来自 WorkplaceSnapshot.UseCount）。</summary>
        public int UseCount;

        /// <summary>是否为空段（无时段/无成果）；UI 据此显示占位而非造假。</summary>
        public bool IsEmpty;
    }
}

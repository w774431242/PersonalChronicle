using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// Home dashboard read model (P2-6). Holds only raw, pre-sorted, null-guarded
    /// data from <see cref="IArchiveService"/>. The window translates these into its
    /// own display <c>RecentLineView</c> / <c>ImportantCardView</c> structs, keeping
    /// the snapshot free of any window-private type. All ordering and null-guards
    /// happen here, not in the draw path.
    /// </summary>
    public sealed class HomeSnapshot : ArchiveSectionSnapshot
    {
        public int ActivePawnCount;
        public int ArchivedPawnCount;
        public int LiveColonistCount;
        public int LiveFreeCount;
        public int LiveSlaveCount;
        public int LivePrisonerCount;
        public int ServiceDays;

        /// <summary>Most-recent events, pre-sorted descending by tick, nulls removed.</summary>
        public IReadOnlyList<ChronicleEvent> RecentEvents = new List<ChronicleEvent>();

        /// <summary>Top-N important objects (most events), pre-sorted descending.</summary>
        public IReadOnlyList<ArchiveObject> ImportantObjects = new List<ArchiveObject>();
    }

    /// <summary>
    /// Category overview read model (P2-6). Objects per category, fetched once each
    /// and sorted by event count descending with nulls removed.
    /// </summary>
    public sealed class OverviewSnapshot : ArchiveSectionSnapshot
    {
        public Dictionary<string, List<ArchiveObject>> CategoryObjects =
            new Dictionary<string, List<ArchiveObject>>();

        /// <summary>
        /// v4.14: Location atlas KPI strip (8 cells). Aggregated in the Read Model —
        /// the window renders the strip only, never re-derives the counters.
        /// </summary>
        public LocationKpisView LocationKpis = new LocationKpisView();

        /// <summary>
        /// v4.14: per-location event counts (stableId → count) for the atlas card
        /// sub-line ("est · N 事件"). Aggregated in the Read Model so the window
        /// never queries the event store in the draw path.
        /// </summary>
        public Dictionary<string, int> LocationEventCounts = new Dictionary<string, int>();

        /// <summary>
        /// v4.14: Battle KPI strip (5 cells) + per-battle kill/loss/decisive data.
        /// Aggregated in the Read Model — the window renders only.
        /// </summary>
        public BattleKpisView BattleKpis = new BattleKpisView();
    }

    /// <summary>
    /// v4.14: Battle KPI strip counters, aggregated once per rebuild from the
    /// BattleObject snapshot list + the battle-scoped event stream. Total /
    /// decisive (via IBattleProvider) / our kills / our losses / participant count.
    /// </summary>
    public sealed class BattleKpisView
    {
        public int Total;
        public int Decisive;
        public int Kills;
        public int Losses;
        public int Roster;
        /// <summary>Per-battle aggregates (battle stableId → derived view).</summary>
        public Dictionary<string, BattleCardView> Cards = new Dictionary<string, BattleCardView>();
    }

    /// <summary>
    /// v4.14: per-battle card aggregate — force size, our kills, our losses,
    /// participant count, significance and threat key. Derived in the Read Model
    /// from the BattleObject snapshot + battle-scoped Death events (Subjects
    /// carry the battle edge); the window renders only.
    /// </summary>
    public sealed class BattleCardView
    {
        public int RaidCount;
        public int Kills;
        public int Losses;
        public int Participants;
        public bool IsSignificant;
        public string ThreatKey;
    }

    /// <summary>
    /// v4.14: Location atlas KPI strip counters, aggregated once per rebuild from
    /// the LocationObject snapshot list (identity/ownership/lifecycle/commerce).
    /// Total / player home / quest sites / faction cities / ruined + tradable /
    /// permit-required / distinct factions.
    /// </summary>
    public sealed class LocationKpisView
    {
        public int Total;
        public int Home;
        public int Quest;
        public int Settle;
        public int Ruined;
        public int Tradable;
        public int Permit;
        public int Factions;
    }

    /// <summary>
    /// Pawn / weapon detail read model (P2-6). Raw, sorted event list so the window
    /// draws from one immutable source instead of re-querying per tab.
    /// </summary>
    /// <summary>
    /// One production line (defName + count + last-tick + stable id), derived in
    /// the Read Model from the detail object's craft/built events. Lives here so
    /// the window never re-aggregates the event stream (v4.6.5 boundary fix).
    /// </summary>
    public sealed class ProductionLineView
    {
        public string DefName;
        public string Label;
        public int Count;
        public long LastTick;
        public string StableId;
        /// <summary>Total market value of all units (Def.MarketValue * Count), aggregated in the Read Model.</summary>
        public float Value;
    }

    /// <summary>
    /// v4.15 condense-tab production category badge: a first-level
    /// <see cref="ThingCategoryDef"/> (e.g. 食物/制成品/武器) with its aggregated
    /// item count. Derived in the Read Model from <see cref="ProductionLines"/>
    /// via <c>ThingDef.FirstThingCategory</c>; the window only renders the label.
    /// </summary>
    public sealed class ProductionCategoryView
    {
        /// <summary>Localized first-level category label (e.g. "制成品").</summary>
        public string Label;
        /// <summary>Aggregated item count across this category.</summary>
        public int Count;
    }

    /// <summary>
    /// v4.15 condense-tab kill breakdown: a victim group (faction label, or a
    /// category fallback such as 机械族/动物) with its aggregated kill count for
    /// this pawn. Derived in the Read Model from Death events; the window only
    /// renders the label + count (no re-derivation, no hardcoding).
    /// </summary>
    public sealed class KillByFactionView
    {
        /// <summary>Localized group label (e.g. "帝国", "机械族").</summary>
        public string Label;
        /// <summary>Aggregated kill count for this group.</summary>
        public int Count;
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

        /// <summary>v4.15 产出宫格真实数据源：来自 <see cref="ProductionAccumulator"/>
        /// 持久化累计（每次 Craft/Built 捕获时累加 Def.MarketValue*quantity），已按
        /// 类目聚合、按产值降序。宫格 bars 与种类数均消费此快照，不再重扫事件流。</summary>
        public IReadOnlyList<ProductionTypeView> ProductionTypeViews = new List<ProductionTypeView>();

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
        /// "健康残值 · 资产折旧" view — composite health score, depreciated silver
        /// value, and the positive/negative factors behind it. All derivation lives
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
    /// Single-event detail read model (P2-6). The window renders the human-readable
    /// description from <see cref="ChronicleEvent.TypeKey"/> + <see cref="ChronicleEvent.Params"/>
    /// via the event Def template, so the snapshot only carries the raw event.
    /// </summary>
    public sealed class EventSnapshot : ArchiveSectionSnapshot
    {
        public ChronicleEvent Event;

        /// <summary>
        /// Later events of the same primary object (Tick &gt; this event), capped at
        /// 3 and sorted ascending. Derived in the Read Model; the window renders it
        /// without re-querying / re-sorting (v4.6.5 boundary fix).
        /// </summary>
        public IReadOnlyList<ChronicleEvent> FollowupEvents = new List<ChronicleEvent>();
    }
}

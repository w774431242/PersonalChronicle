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

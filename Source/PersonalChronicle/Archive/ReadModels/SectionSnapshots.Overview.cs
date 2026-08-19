// Phase 1 partial split (08-架构层-代码轻量化方案.md §3.4): Overview + production/kill views.
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Archive.ReadModels
{
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
}

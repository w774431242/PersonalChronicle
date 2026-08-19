// Phase 1 partial split (08-架构层-代码轻量化方案.md §3.4): Home + Battle/Location KPI views.
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using Verse;

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
}

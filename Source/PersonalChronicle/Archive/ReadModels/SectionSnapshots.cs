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
    public sealed class DetailSnapshot : ArchiveSectionSnapshot
    {
        public ArchiveObject DetailObject;
        public IReadOnlyList<ChronicleEvent> RawEvents = new List<ChronicleEvent>();
    }

    /// <summary>
    /// Single-event detail read model (P2-6). The window renders the human-readable
    /// description from <see cref="ChronicleEvent.TypeKey"/> + <see cref="ChronicleEvent.Params"/>
    /// via the event Def template, so the snapshot only carries the raw event.
    /// </summary>
    public sealed class EventSnapshot : ArchiveSectionSnapshot
    {
        public ChronicleEvent Event;
    }
}

using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// P2-6 read-model isolation. Every archive tab (Home / Overview / PawnDetail /
    /// WeaponDetail / Event) renders from an <see cref="ArchiveSectionSnapshot"/>
    /// produced by a dedicated section builder. The UI layer never calls
    /// <c>IArchiveService</c> ordering / filtering LINQ directly — that logic lives
    /// in the snapshot builder, so the sort order, null-guards and de-duplication
    /// rules are centralized and unit-testable (design doc §7.4).
    ///
    /// Snapshots are immutable display models: they hold only data keys and
    /// pre-computed display strings (already localized via translation keys at build
    /// time). They are rebuilt on the refresh cadence, never per frame.
    /// </summary>
    public abstract class ArchiveSectionSnapshot
    {
        /// <summary>Monotonic revision this snapshot was built from. Lets the UI skip
        /// a rebuild when neither the data revision nor the view state changed.</summary>
        public long BuiltFromRevision { get; set; }

        /// <summary>True when the source query returned no renderable content.</summary>
        public bool IsEmpty { get; set; }
    }

    /// <summary>
    /// Contract for a section read-model builder. Implementations live in
    /// <see cref="PersonalChronicle.Archive.ReadModels"/> and translate
    /// <see cref="IArchiveService"/> queries into immutable
    /// <see cref="ArchiveSectionSnapshot"/> instances. The main window holds one
    /// provider and asks it for the snapshot it needs, keeping all LINQ / null-guard
    /// logic out of the draw path.
    /// </summary>
    public interface IArchiveUiDataProvider
    {
        /// <summary>Home dashboard snapshot (KPI tiles + recent lines + important cards).</summary>
        HomeSnapshot BuildHome(IArchiveService service, long revision);

        /// <summary>Category overview snapshot (objects grouped/sorted by category).</summary>
        OverviewSnapshot BuildOverview(IArchiveService service, string categoryKey, long revision);

        /// <summary>Pawn / weapon detail snapshot (events, links, combat, production, intensity).</summary>
        DetailSnapshot BuildDetail(IArchiveService service, string detailObjectId, long revision);

        /// <summary>Single event detail snapshot (description tree).</summary>
        EventSnapshot BuildEvent(IArchiveService service, ChronicleEvent ev, long revision);

        /// <summary>Full timeline event stream, sorted ascending by <see cref="ChronicleEvent.Tick"/>
        /// and null-guarded. Centralizes the ordering that the Home timeline needs so the
        /// draw path never orders <see cref="IArchiveService"/> results itself (design doc §7.4).</summary>
        IReadOnlyList<ChronicleEvent> BuildTimelineEvents(IArchiveService service, long revision);
    }
}

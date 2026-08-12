using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Api
{
    /// <summary>
    /// Read-only query surface of the archive. Every method returns a read-only
    /// snapshot or live read-only view; no method mutates archive state.
    ///
    /// This is the contract an external mod (or a future UI) should depend on
    /// when it only needs to READ archive data. <see cref="IArchiveService"/>
    /// extends this interface with the write-entry points; the implementation
    /// (ArchiveService) satisfies both.
    /// </summary>
    public interface IArchiveQueryService
    {
        // ---- pawn records ----
        IReadOnlyList<PawnRecord> GetAllRecords();
        IReadOnlyList<PawnRecord> GetActiveRecords();
        IReadOnlyList<PawnRecord> GetArchivedRecords();

        // ---- object / event resolution ----
        ArchiveObject GetObject(string stableId);
        IReadOnlyList<ArchiveObject> GetLinkedObjects(string stableId);
        IReadOnlyList<ArchiveObject> GetObjectsOfCategory(string categoryKey);
        IReadOnlyList<ChronicleEvent> GetEventsFor(string stableId);
        IReadOnlyList<ChronicleEvent> GetRecentEvents(int count);
        IReadOnlyList<ChronicleEvent> GetAllEvents();
        IReadOnlyList<ChronicleEvent> GetProductionEvents(string stableId);
        ArchiveDepthBehavior GetCategoryBehavior(string categoryKey);

        // ---- revision / cache invalidation ----
        long GetDataRevision();

        // ---- live pawn / location ----
        Pawn GetLivePawn(string stableId);
        Pawn GetCurrentHolder(string stableId);
        LocationInfo GetLiveLocation(string stableId);
        BattleObject GetActiveBattle();

        /// <summary>
        /// True when the pawn is currently "on the roster": alive AND a member
        /// of the current colony population (free/slave/prisoner on maps or in
        /// caravans). Dead/archived pawns, former colonists who left the colony,
        /// and unknown stable ids all return false. This is the semantic
        /// opposite of "archived" (DeathTick &gt; 0) — the archive should not
        /// call a live colony member "archived" just because a snapshot exists.
        /// </summary>
        bool IsCurrentlyEnlisted(string stableId);

        // ---- career / work-time views ----
        IReadOnlyList<WorkPriorityView> GetWorkPriorities(string stableId);
        IReadOnlyList<WorkTimeStatView> GetWorkTimeStats(string stableId);
        IReadOnlyList<SkillArchiveView> GetSkillArchive(string stableId);
        ProductionSummaryView GetProductionSummary(string stableId);

        // ---- live stats (home-page counts) ----
        int GetLiveColonistCount();
        void GetLiveColonistCounts(out int free, out int slave, out int prisoner);
        int GetActiveSnapshotCount();
        int GetArchivedSnapshotCount();
        int GetServiceDays();

        // ---- persisted home view mode (read side) ----
        int GetHomeViewMode();
    }
}

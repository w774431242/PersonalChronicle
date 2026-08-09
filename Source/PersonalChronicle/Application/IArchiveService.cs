using System.Collections.Generic;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// Service surface consumed by the UI layer. All read queries return
    /// read-only snapshots or live read-only views; write entry points are
    /// the only way the capture layer feeds the archive.
    ///
    /// v0.2 methods (GetAllRecords / GetActiveRecords / GetArchivedRecords /
    /// GetEventsFor / OnColonistJoined / OnPawnDied) are preserved unchanged —
    /// v2.1 additions are appended, never breaking existing signatures.
    /// </summary>
    public interface IArchiveService
    {
        // ---- v0.2 surface (unchanged) ----

        IReadOnlyList<PawnRecord> GetAllRecords();
        IReadOnlyList<PawnRecord> GetActiveRecords();
        IReadOnlyList<PawnRecord> GetArchivedRecords();
        IReadOnlyList<ChronicleEvent> GetEventsFor(string stableId);
        void OnColonistJoined(Pawn pawn);
        void OnColonistJoined(Pawn pawn, PawnRole role);
        void OnPawnDied(Pawn pawn, string deathCauseKey);

        // ---- v2.1 additions ----

        /// <summary>Resolves a single archived object by stable id (null if absent).</summary>
        ArchiveObject GetObject(string stableId);

        /// <summary>
        /// Resolves objects associated with the given stable id: every object that
        /// shares an event edge (Primary or Subject) with it, excluding itself.
        /// </summary>
        IReadOnlyList<ArchiveObject> GetLinkedObjects(string stableId);

        /// <summary>Resolves all archived objects of a category ("Pawn"/"Thing"/"Battle"/"Location").</summary>
        IReadOnlyList<ArchiveObject> GetObjectsOfCategory(string categoryKey);

        /// <summary>
        /// Monotonic runtime revision for the archive data. UI consumers use it
        /// to invalidate read caches immediately after a capture write; it is
        /// not persisted and is never a source of truth for archive contents.
        /// </summary>
        long GetDataRevision();

        // ---- v2.1 UI live reads ----

        /// <summary>Resolves the live Pawn matching a stable id (null if absent or dead).</summary>
        Pawn GetLivePawn(string stableId);

        // ---- v2.2 additions ----

        /// <summary>
        /// Global cross-category event stream: the most recent <paramref name="count"/>
        /// events across ALL object types, ordered by Tick descending (newest first).
        /// The returned list is a read-only snapshot — callers never see the live
        /// list. Consumed by the Overview layer; safe to call on a UI refresh
        /// cadence (the caller throttles, not this method).
        /// </summary>
        IReadOnlyList<ChronicleEvent> GetRecentEvents(int count);

        /// <summary>
        /// Resolves the data-driven depth behavior of an archive category.
        /// Returns <see cref="ArchiveDepthBehavior.Record"/> (conservative default)
        /// when no ArchiveCategoryDef exists for <paramref name="categoryKey"/>.
        /// UI layer branches StatOnly vs Event rendering on this value.
        /// </summary>
        ArchiveDepthBehavior GetCategoryBehavior(string categoryKey);

        // ---- v2.3 live tab / combat queries (append-only) ----

        /// <summary>
        /// Legacy work priorities (v2.3). UI v3.1 no longer displays priorities;
        /// prefer <see cref="GetWorkTimeStats"/>. Kept for binary/API compatibility.
        /// </summary>
        IReadOnlyList<WorkPriorityView> GetWorkPriorities(string stableId);

        /// <summary>
        /// v3.1 career work-time stats: cumulative sampled ticks per WorkTypeDef,
        /// sorted by ticks descending with share and rank. Empty when no samples
        /// yet (pre-install history is not backfilled).
        /// </summary>
        IReadOnlyList<WorkTimeStatView> GetWorkTimeStats(string stableId);

        /// <summary>
        /// v3.1 skill archive: join snapshot vs death snapshot (or live levels
        /// while the pawn is still alive). Empty when no skill data was captured.
        /// </summary>
        IReadOnlyList<SkillArchiveView> GetSkillArchive(string stableId);

        /// <summary>v4.0 cumulative production quantity/value summary.</summary>
        ProductionSummaryView GetProductionSummary(string stableId);

        /// <summary>
        /// Live location of a pawn: on-map (biome/map defName) or in a world
        /// caravan (world tile). Returns Kind = None when the pawn is dead,
        /// absent or not resolvable to either.
        /// </summary>
        LocationInfo GetLiveLocation(string stableId);

        /// <summary>
        /// Resolves the live Pawn currently holding a Thing (by its stable id
        /// "defName:thingIDNumber"). Walks the IThingHolder parent chain
        /// (equipment tracker / apparel tracker / containers) until a Pawn is
        /// found; null when the thing is unheld, not found or destroyed. The UI
        /// falls back to ThingObject.CurrentHolderId when no live pawn resolves.
        /// </summary>
        Pawn GetCurrentHolder(string stableId);

        /// <summary>
        /// The most recent ongoing battle (BattleObject with EndTick == -1,
        /// ties broken by the latest associated event tick). Null when no battle
        /// is currently recorded as ongoing.
        /// </summary>
        BattleObject GetActiveBattle();

        /// <summary>
        /// Semantic filter over GetEventsFor: only Craft/Built events (resolved
        /// through ChronicleEventDef.kind, never TypeKey substrings). Provided as
        /// a convenience wrapper; the UI's production tab may aggregate directly.
        /// </summary>
        IReadOnlyList<ChronicleEvent> GetProductionEvents(string stableId);

        // ---- v2.1 Phase 1 capture write entries ----

        /// <summary>
        /// Records a colonist death, optionally linking the killer's weapon as a
        /// Subject edge. <paramref name="weapon"/> may be null (unarmed kill) —
        /// behavior then matches the v0.2 two-argument overload exactly.
        ///
        /// v0.3: <paramref name="extraParams"/> carries language-independent
        /// identity snapshots to be merged into the event's Params, e.g.
        /// ["killer"] = killer pawn LabelShort. Both <paramref name="weapon"/>
        /// and <paramref name="extraParams"/> are optional — existing call sites
        /// keep compiling unchanged.
        /// </summary>
        void OnPawnDied(Pawn pawn, string deathCauseKey, Thing weapon = null, Dictionary<string, string> extraParams = null);

        /// <summary>
        /// v3.1 P2: same as the four-argument death write, plus a live killer
        /// pawn for Subject edges (kill graph + battle participants).
        /// </summary>
        void OnPawnDied(Pawn pawn, string deathCauseKey, Thing weapon, Dictionary<string, string> extraParams, Pawn killer);

        /// <summary>
        /// v3.1 P2: records a kill by a chronicle colonist of a non-archived
        /// victim (e.g. raider). Creates a Death-type event with Primary=victim
        /// and Subject=killer (+ weapon/battle), so GetEventsFor(killer) lists it.
        /// When the victim is itself a chronicle colonist, use
        /// <see cref="OnPawnDied"/> instead (that path already adds the killer edge).
        /// </summary>
        void OnKillRecorded(Pawn killer, Pawn victim, Thing weapon = null);

        /// <summary>
        /// v3.1 P3: significant social relation formed or ended (lover/spouse/family).
        /// Writes a Social ChronicleEvent + updates PawnObject.Relations snapshots
        /// for both parties when they are chronicle-relevant.
        /// </summary>
        void OnRelationChanged(Pawn a, Pawn b, PawnRelationDef relationDef, bool formed);

        /// <summary>
        /// Records a crafted/built thing (weapon, apparel, equipment...).
        /// <paramref name="product"/> is the finished Thing; the worker becomes a
        /// Subject edge when it is a chronicle-relevant pawn.
        /// </summary>
        void OnThingCrafted(Thing product, Pawn worker);

        /// <summary>
        /// Records a finished construction. <paramref name="builtDef"/> is the
        /// completed building's ThingDef; <paramref name="builtStableId"/> is the
        /// caller-supplied stable identity (defName:thingIDNumber of the frame).
        /// </summary>
        void OnThingBuilt(ThingDef builtDef, string builtStableId, Pawn worker);

        /// <summary>
        /// Records a battle-grade incident start. The caller (capture layer) is
        /// responsible for the data-driven filter (IncidentBattleExtension);
        /// this service only persists the fact.
        /// </summary>
        void OnBattleStarted(IncidentDef incidentDef);

        // ---- v2.4 live stats (home-page counts) ----

        /// <summary>
        /// Current (live-read) colony population count — free colonists, slaves
        /// and prisoners combined. Two-source merge (maps + caravans) via
        /// <see cref="PersonalChronicle.Domain.ChronicleColonistScanner"/>
        /// (single predicate source of truth) with an independent 600-tick cache.
        /// Returns 0 defensively when no game/component is active. NOTE: the
        /// write/reconcile path is driven internally by ChronicleGameComponent's
        /// tick hook — the UI read contract never triggers reconciliation.
        /// </summary>
        int GetLiveColonistCount();

        /// <summary>
        /// Current (live-read) colony population broken down by role: free
        /// colonists / slaves / prisoners. Shares the same 600-tick cache as
        /// <see cref="GetLiveColonistCount"/> (a single underlying scan), so
        /// calling it never triggers a second enumeration within the window.
        /// </summary>
        void GetLiveColonistCounts(out int free, out int slave, out int prisoner);

        /// <summary>
        /// Archive-snapshot convention: active = PawnObject.DeathTick == -1.
        /// Counts every archived PawnObject of the Pawn category.
        /// </summary>
        int GetActiveSnapshotCount();

        /// <summary>
        /// Archive-snapshot convention: archived = PawnObject.DeathTick &gt; 0.
        /// Counts every archived PawnObject of the Pawn category.
        /// </summary>
        int GetArchivedSnapshotCount();
    }
}

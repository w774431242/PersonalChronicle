using System.Collections.Generic;
using PersonalChronicle.Api;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// Service surface consumed by the UI layer. Extends the read-only
    /// <see cref="IArchiveQueryService"/> with the write entry points; the
    /// implementation satisfies both so an external mod can depend only on the
    /// narrower query contract.
    ///
    /// All read queries return read-only snapshots or live read-only views;
    /// write entry points are the only way the capture layer feeds the archive.
    ///
    /// v0.2 methods (GetAllRecords / GetActiveRecords / GetArchivedRecords /
    /// GetEventsFor / OnColonistJoined / OnPawnDied) are preserved unchanged —
    /// v2.1 additions are appended, never breaking existing signatures.
    /// </summary>
    public interface IArchiveService : IArchiveQueryService
    {
        // ---- v0.2 write surface (unchanged) ----

        void OnColonistJoined(Pawn pawn);
        void OnColonistJoined(Pawn pawn, PawnRole role);
        void OnPawnDied(Pawn pawn, string deathCauseKey);

        // ---- read queries are inherited from IArchiveQueryService ----
        // (GetRecentEvents / GetAllEvents / GetCategoryBehavior / GetWorkPriorities /
        // GetWorkTimeStats / GetSkillArchive / GetProductionSummary / GetLiveLocation /
        // GetCurrentHolder / GetActiveBattle / GetProductionEvents / live stats /
        // GetHomeViewMode are all declared on IArchiveQueryService and surfaced here
        // through inheritance — do NOT re-declare to avoid CS0108 hide warnings.)

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
        /// <summary>
        /// assistLookup: optional, ordered by damage dealt (descending). The top
        /// entry is treated as the primary kill contributor when it differs from
        /// the finishing instigator, who is then recorded as an assist.
        /// </summary>
        void OnKillRecorded(Pawn killer, Pawn victim, Thing weapon = null, List<Pawn> assistLookup = null);

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
        /// v4.9: records an equipment thing's decommission (退役仪式) — a read-only
        /// "death record" captured at destroy time. Never prevents the destroy.
        /// <paramref name="lastHolder"/> is optional (may be null when the destroy
        /// has no clear holder context, e.g. deterioration in storage).
        /// </summary>
        void OnThingDestroyed(Thing thing, Pawn lastHolder = null);

        /// <summary>
        /// Records a battle-grade incident start. The caller (capture layer) is
        /// responsible for the data-driven filter (IncidentBattleExtension);
        /// this service only persists the fact.
        /// </summary>
        void OnBattleStarted(IncidentDef incidentDef);

        /// <summary>
        /// v4.11 P0: links the freshly-spawned raid Lord(s) on the map to an active
        /// battle and snapshots the raid force size (<see cref="BattleObject.RaidCount"/>)
        /// plus the runtime <see cref="BattleObject.RemainingRaidCount"/> countdown.
        /// Called from <see cref="OnBattleStarted"/> once the BattleObject exists and
        /// the raid workers have already generated their Lords (IncidentWorker.TryExecute
        /// calls TryExecuteWorker synchronously before its postfix runs).
        /// </summary>
        void LinkRaidLords(BattleObject battle);

        /// <summary>
        /// v4.11 P0: one raid pawn left the map (death / capture / exit / downed) for
        /// the Lord identified by <paramref name="lordLoadId"/>. <paramref name="remainingPawns"/>
        /// is the Lord's authoritative <c>ownedPawns.Count</c> after the loss (read in
        /// the capture patch, since Notify_PawnLost already removed the pawn). When it
        /// reaches zero the battle is finalized (EndTick written). Called from the
        /// Lord.Notify_PawnLost capture patch. No-op for Lords not linked to any battle.
        /// </summary>
        void OnRaidPawnGone(int lordLoadId, int remainingPawns);

        // ---- persisted state (write side) ----
        // Read side of the home view mode (GetHomeViewMode) and all live stats are
        // already declared on IArchiveQueryService; only the write entry remains here.

        /// <summary>
        /// v4.0 home view mode persisted in the game component.
        /// </summary>
        void SetHomeViewMode(int mode);

        // ---- v4.1 unified event bridge (legacy ↔ IArchiveEventSink) ----

        /// <summary>
        /// v4.1 bridge: records an event through the unified <see cref="IArchiveEventSink"/>
        /// contract. This is the single connection point between the rich legacy
        /// write methods (OnKillRecorded / OnPawnDied / ...) and the unified event
        /// sink — callers that already hold a fully-formed <see cref="ArchiveEventInput"/>
        /// (e.g. a third-party mod) should use <c>PersonalChronicleApi.TryGet</c> →
        /// <see cref="IArchiveEventSink.TryRecord"/> directly; this method is the
        /// equivalent on the legacy surface and delegates to it. Never throws on
        /// bad input; returns a <see cref="CaptureResult"/>.
        /// </summary>
        PersonalChronicle.Api.CaptureResult RecordEvent(PersonalChronicle.Api.ArchiveEventInput input);
    }
}

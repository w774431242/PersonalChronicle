using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// Persistent storage for the chronicle. Owns the raw lists that are saved to
    /// the savegame; exposes read-only query views and internal write entry points
    /// used only by the application service layer.
    ///
    /// v2.1 model: canonical list is <see cref="Objects"/> (polymorphic
    /// ArchiveObject). The v0.2 "pawns" list is still read for old saves and
    /// re-written as a downgrade mirror (PawnObject → PawnRecord projection),
    /// so rolling back to v0.2 keeps the pawn archive intact. One-time migration
    /// is gated by <see cref="SchemaVersion"/> (idempotent: 0 = legacy).
    /// </summary>
    public sealed class ChronicleGameComponent : GameComponent
    {
        public List<ArchiveObject> Objects = new List<ArchiveObject>();
        public List<ChronicleEvent> Events = new List<ChronicleEvent>();
        public long NextEventId;

        /// <summary>
        /// v4.0: persisted home overview view preference (Kpi dashboard vs Chronicle
        /// timeline). Default Kpi; old saves that never wrote this field fall back
        /// to Kpi safely via the Scribe default value. Mirrored by the main-tab
        /// window; never a source of archive truth.
        /// </summary>
        public int HomeViewMode = 0;

        /// <summary>
        /// Runtime-only monotonic token. It is deliberately not serialized:
        /// save/load rebuilds the indexes and forces every UI read model to
        /// refresh naturally. The token is not archive data.
        /// </summary>
        public long DataRevision { get; private set; }

        /// <summary>
        /// 0 = legacy v0.2 schema, 1 = v3 archive schema, 2 = v4 scope
        /// migration, 3 = current schema.
        /// Persisted so the one-time migration never runs twice.
        /// </summary>
        public long SchemaVersion;

        /// <summary>
        /// v0.2 compatibility slot. On load: populated from old saves. On save:
        /// rebuilt as a downgrade mirror (PawnObjects projected to PawnRecords,
        /// whose class name v0.2 can deserialize). Never a source of truth.
        /// </summary>
        private List<PawnRecord> legacyPawns = new List<PawnRecord>();

        [Unsaved]
        private Dictionary<string, ArchiveObject> objectsByStableId = new Dictionary<string, ArchiveObject>();

        [Unsaved]
        private Dictionary<string, List<ChronicleEvent>> eventsByObject = new Dictionary<string, List<ChronicleEvent>>();

        /// <summary>
        /// Reconcile throttle window: GameComponentTick runs one reconcile pass
        /// at most every 600 ticks (10 seconds at 60 tps), matching the live-read
        /// cache cadence from the approved 统计活读化修复方案.
        /// </summary>
        private const long ReconcileIntervalTicks = 600L;

        /// <summary>
        /// v3.1 career work-time sampling interval (~2s at 60 tps). Independent of
        /// reconcile — low-frequency, no Harmony on Job hot paths.
        /// </summary>
        private const long WorkSampleIntervalTicks = 120L;

        /// <summary>Ticks attributed to a work type per successful sample.</summary>
        private const long WorkSampleCreditTicks = 120L;

        /// <summary>
        /// Confirmation window for reconcile-archived colonists. A candidate must
        /// be present in two consecutive reconcile passes before it is archived —
        /// guards against temporary reinforcements (VE reinforcements / Medieval
        /// mercenaries) being permanently archived. A candidate absent from any
        /// pass is pruned and must restart the window from scratch.
        ///
        /// [Unsaved]: never persisted. A save/load boundary clears the window, so
        /// confirmation restarts cold on the new save — conservative, acceptable.
        /// </summary>
        [Unsaved]
        private HashSet<string> reconcileCandidates = new HashSet<string>();

        // 1.6: GameComponent base has a parameterless constructor. Empty ctor body,
        // do NOT chain : base(game) (CS1729).
        public ChronicleGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref SchemaVersion, "schemaVersion", 0L);
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                legacyPawns = BuildLegacyPawnMirror();
            }
            Scribe_Collections.Look(ref legacyPawns, "pawns", LookMode.Deep);
            Scribe_Collections.Look(ref Objects, "objects", LookMode.Deep);
            Scribe_Collections.Look(ref Events, "events", LookMode.Deep);
            Scribe_Values.Look(ref NextEventId, "nextEventId", 0L);
            Scribe_Values.Look(ref HomeViewMode, "homeViewMode", 0);
            if (legacyPawns == null)
            {
                legacyPawns = new List<PawnRecord>();
            }
            if (Objects == null)
            {
                Objects = new List<ArchiveObject>();
            }
            if (Events == null)
            {
                Events = new List<ChronicleEvent>();
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                DataRevision = 0L;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();

            // Static capture-side accumulators survive a save/load cycle because they live
            // on the assembly, not on the Game. Clear them here (FinalizeInit runs for both
            // "new game" and "load game") so a previous session's pawns can never leak into
            // this one's assist attribution.
            Capture.Patch_PawnTakeDamage.Reset();

            if (SchemaVersion < 1L)
            {
                MigrateLegacyData();
                SchemaVersion = 1L;
            }
            RebuildIndexes();
            if (SchemaVersion < 2L)
            {
                // v4.0 scope correction: older builds could backfill scenario
                // editor candidates during FinalizeInit. Remove only empty,
                // alive, unknown-join artifacts that are absent from the real
                // current population; preserve any object with history.
                if (PruneInitialRosterArtifacts())
                {
                    SchemaVersion = 2L;
                    RebuildIndexes();
                }
            }
            if (SchemaVersion < 3L)
            {
                // v4.0.1: the previous migration preserved candidates that had
                // already received a relation event. Those events could also
                // be emitted while the scenario roster was being initialized,
                // so event presence is not proof of colony membership. The
                // stricter one-time cleanup removes only alive, unknown-join
                // records absent from the current population; explicit joins,
                // dead pawns and current members remain untouched.
                if (PruneUnconfirmedRosterArtifacts())
                {
                    SchemaVersion = 3L;
                    RebuildIndexes();
                }
            }
            BackfillExistingColonists();
            MarkChanged();
        }

        /// <summary>
        /// Periodic reconcile (difference-fill against the live colony): every
        /// ReconcileIntervalTicks, enumerate live chronicle colonists via the
        /// shared Domain scanner and backfill-archive anyone not yet in the
        /// archive. This is the safety net for join paths Patch_SetFaction cannot
        /// see (Biotech pregnancy births, xenogerm conversion, mod recruitment
        /// paths).
        ///
        /// 只增不删: absence is NEVER treated as leaving — a cryptosleep colonist
        /// stays inside AllPawnsAlive and would otherwise pollute the timeline
        /// with a false death; death archiving stays exclusively with
        /// Patch_PawnKill.
        /// </summary>
        public override void GameComponentTick()
        {
            long tick = (long)Find.TickManager.TicksGame;

            // Recording gate shared by work sampling + reconcile.
            if (PersonalChronicleMod.Settings == null || !PersonalChronicleMod.Settings.EnableRecording)
            {
                return;
            }

            // v3.1: sample labour time + place visits every WorkSampleIntervalTicks.
            if (tick % WorkSampleIntervalTicks == 0L)
            {
                SampleWorkTimeAndPlaces(tick);
                // Work-time and place snapshots are live archive data. One
                // token bump per sample keeps the UI fresh without a per-frame
                // event or allocation path.
                MarkChanged();
            }

            // Throttle: one reconcile pass per ReconcileIntervalTicks.
            if (tick % ReconcileIntervalTicks != 0L)
            {
                return;
            }

            List<ColonyMember> live = ChronicleColonistScanner.EnumerateCurrentPeople();

            // Presence set for THIS pass — drives both the confirmation window
            // and the per-pass prune below.
            HashSet<string> nowPresent = new HashSet<string>();
            for (int i = 0; i < live.Count; i++)
            {
                ColonyMember member = live[i];
                if (member == null || member.Pawn == null)
                {
                    continue;
                }
                Pawn pawn = member.Pawn;
                string stableId = pawn.GetUniqueLoadID();
                if (string.IsNullOrEmpty(stableId))
                {
                    continue;
                }
                nowPresent.Add(stableId);

                // Already archived (by Patch_SetFaction, backfill or an earlier
                // reconcile) → 同步最新角色（囚犯被招募后徽标自动更新），不重建。
                ArchiveObject existing;
                if (objectsByStableId.TryGetValue(stableId, out existing))
                {
                    PawnObject existingPawn = existing as PawnObject;
                    if (existingPawn != null && existingPawn.Role != member.Role)
                    {
                        existingPawn.Role = member.Role;
                        MarkChanged();
                    }
                    continue;
                }
                if (reconcileCandidates.Contains(stableId))
                {
                    // Second consecutive presence → confirmed colonist: archive
                    // with unknown join time (-1L), same semantics as backfill.
                    reconcileCandidates.Remove(stableId);
                    PawnObject record = CreateRecord(pawn, -1L, member.Role);
                    if (AddObject(record))
                    {
                        AddDetectedJoinEvent(record);
                    }
                }
                else
                {
                    // First presence: open the confirmation window.
                    reconcileCandidates.Add(stableId);
                }
            }

            // Prune: candidates not present this pass leave the window, so a
            // temporary reinforcement must re-confirm from scratch next time.
            if (reconcileCandidates.Count > 0)
            {
                List<string> stale = null;
                foreach (string id in reconcileCandidates)
                {
                    if (!nowPresent.Contains(id))
                    {
                        if (stale == null)
                        {
                            stale = new List<string>();
                        }
                        stale.Add(id);
                    }
                }
                if (stale != null)
                {
                    for (int i = 0; i < stale.Count; i++)
                    {
                        reconcileCandidates.Remove(stale[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Credits WorkSampleCreditTicks to each living archived colonist whose
        /// current job resolves to a WorkTypeDef. Dead/archived pawns are skipped
        /// (career ledger freezes on death). Failures are silent per-pawn.
        /// </summary>
        private void SampleWorkTimeAndPlaces(long gameTick)
        {
            if (Objects == null || Objects.Count == 0)
            {
                return;
            }
            // One live scan → stableId map (avoid O(n²) per-object rescans).
            Dictionary<string, Pawn> liveById = new Dictionary<string, Pawn>();
            List<ColonyMember> live = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < live.Count; i++)
            {
                ColonyMember m = live[i];
                if (m == null || m.Pawn == null)
                {
                    continue;
                }
                string id = m.Pawn.GetUniqueLoadID();
                if (!string.IsNullOrEmpty(id) && !liveById.ContainsKey(id))
                {
                    liveById[id] = m.Pawn;
                }
            }

            for (int i = 0; i < Objects.Count; i++)
            {
                PawnObject record = Objects[i] as PawnObject;
                if (record == null || record.IsArchived || string.IsNullOrEmpty(record.StableId))
                {
                    continue;
                }
                Pawn pawn;
                if (!liveById.TryGetValue(record.StableId, out pawn) || pawn == null || pawn.Dead || pawn.Downed)
                {
                    continue;
                }
                bool changed = false;
                WorkTypeDef workType = ResolveCurrentWorkType(pawn);
                if (workType != null)
                {
                    if (record.WorkTime == null)
                    {
                        record.WorkTime = new WorkTimeAccumulator();
                    }
                    record.WorkTime.AddSample(workType.defName, WorkSampleCreditTicks, gameTick);
                    changed = true;
                }
                // P3: place enter/leave ledger (always, even when idle).
                if (SamplePlaceVisit(record, pawn, gameTick))
                {
                    changed = true;
                }
                if (changed)
                {
                    MarkChanged();
                }
            }
        }

        /// <summary>
        /// P3: if the pawn's current place key differs from the open visit,
        /// close the previous stay and open a new one. Updates PrimaryPlaceDefName.
        /// </summary>
        private static bool SamplePlaceVisit(PawnObject record, Pawn pawn, long gameTick)
        {
            if (record == null || pawn == null)
            {
                return false;
            }
            string kind;
            string key = ResolvePlaceKey(pawn, out kind);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            if (record.PlaceHistory == null)
            {
                record.PlaceHistory = new List<PlaceVisit>();
            }
            PlaceVisit open = null;
            for (int i = record.PlaceHistory.Count - 1; i >= 0; i--)
            {
                PlaceVisit v = record.PlaceHistory[i];
                if (v != null && v.IsOpen)
                {
                    open = v;
                    break;
                }
            }
            if (open != null && open.PlaceKey == key)
            {
                // Still here — keep primary in sync.
                if (kind == "Map")
                {
                    if (record.PrimaryPlaceDefName != key)
                    {
                        record.PrimaryPlaceDefName = key;
                        return true;
                    }
                }
                return false;
            }
            if (open != null)
            {
                open.LeaveTick = gameTick;
            }
            record.PlaceHistory.Add(new PlaceVisit(key, kind, gameTick));
            // Cap history length to avoid unbounded growth (keeps last 32 stays).
            const int maxVisits = 32;
            while (record.PlaceHistory.Count > maxVisits)
            {
                record.PlaceHistory.RemoveAt(0);
            }
            if (kind == "Map")
            {
                record.PrimaryPlaceDefName = key;
            }
            return true;
        }

        private static string ResolvePlaceKey(Pawn pawn, out string placeKind)
        {
            placeKind = null;
            if (pawn == null)
            {
                return null;
            }
            if (pawn.Map != null && pawn.Map.Biome != null)
            {
                placeKind = "Map";
                return pawn.Map.Biome.defName;
            }
            // Off-map caravan tile (same scan sources as live location).
            List<Caravan> caravans = Find.WorldObjects != null ? Find.WorldObjects.Caravans : null;
            if (caravans != null)
            {
                for (int i = 0; i < caravans.Count; i++)
                {
                    Caravan c = caravans[i];
                    if (c == null || c.PawnsListForReading == null)
                    {
                        continue;
                    }
                    if (c.PawnsListForReading.Contains(pawn))
                    {
                        placeKind = "Caravan";
                        // 1.6: Caravan.Tile is PlanetTile — persist tileId only.
                        return "tile:" + c.Tile.tileId;
                    }
                }
            }
            return null;
        }

        private static void CapturePrimaryPlace(PawnObject record, Pawn pawn)
        {
            if (record == null || pawn == null)
            {
                return;
            }
            string kind;
            string key = ResolvePlaceKey(pawn, out kind);
            if (!string.IsNullOrEmpty(key) && kind == "Map")
            {
                record.PrimaryPlaceDefName = key;
            }
        }

        private static WorkTypeDef ResolveCurrentWorkType(Pawn pawn)
        {
            if (pawn == null || pawn.CurJob == null)
            {
                return null;
            }
            Job job = pawn.CurJob;
            if (job.workGiverDef != null && job.workGiverDef.workType != null)
            {
                return job.workGiverDef.workType;
            }
            // Some jobs carry workType on the def indirectly via workGiver only.
            return null;
        }

        // ---- Read-only queries (public surface for the service layer) ----

        public IReadOnlyList<PawnRecord> GetAllRecords()
        {
            List<PawnRecord> result = new List<PawnRecord>();
            for (int i = 0; i < Objects.Count; i++)
            {
                PawnObject pawnObject = Objects[i] as PawnObject;
                if (pawnObject != null)
                {
                    result.Add(pawnObject);
                }
            }
            return result;
        }

        public IReadOnlyList<PawnRecord> GetRecordsFor(Pawn pawn)
        {
            if (pawn == null)
            {
                return new List<PawnRecord>();
            }
            ArchiveObject obj;
            if (objectsByStableId.TryGetValue(pawn.GetUniqueLoadID(), out obj))
            {
                PawnObject pawnObject = obj as PawnObject;
                if (pawnObject != null)
                {
                    return new List<PawnRecord> { pawnObject };
                }
            }
            return new List<PawnRecord>();
        }

        /// <summary>
        /// Events referencing a stable id. P3-5: returns a defensive read-only
        /// view — the caller may read/iterate but never mutate the live index.
        /// (Element objects are still shared references, as elsewhere in the
        /// model; the view only guards list structure.)
        /// </summary>
        public IReadOnlyList<ChronicleEvent> GetEventsFor(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return new ReadOnlyCollection<ChronicleEvent>(new List<ChronicleEvent>());
            }
            List<ChronicleEvent> list;
            if (eventsByObject.TryGetValue(stableId, out list) && list != null)
            {
                return new ReadOnlyCollection<ChronicleEvent>(list);
            }
            return new ReadOnlyCollection<ChronicleEvent>(new List<ChronicleEvent>());
        }

        public ArchiveObject GetObject(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return null;
            }
            ArchiveObject obj;
            if (objectsByStableId.TryGetValue(stableId, out obj))
            {
                return obj;
            }
            return null;
        }

        public IReadOnlyList<ArchiveObject> GetLinkedObjects(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return new List<ArchiveObject>();
            }
            HashSet<string> linkedIds = new HashSet<string>();
            List<ChronicleEvent> events;
            if (eventsByObject.TryGetValue(stableId, out events))
            {
                for (int i = 0; i < events.Count; i++)
                {
                    CollectEventObjectIds(events[i], linkedIds);
                }
            }
            linkedIds.Remove(stableId);
            List<ArchiveObject> result = new List<ArchiveObject>();
            foreach (string linkedId in linkedIds)
            {
                ArchiveObject obj;
                if (objectsByStableId.TryGetValue(linkedId, out obj) && obj != null)
                {
                    result.Add(obj);
                }
            }
            return result;
        }

        public IReadOnlyList<ArchiveObject> GetObjectsOfCategory(string categoryKey)
        {
            List<ArchiveObject> result = new List<ArchiveObject>();
            if (string.IsNullOrEmpty(categoryKey))
            {
                return result;
            }
            for (int i = 0; i < Objects.Count; i++)
            {
                ArchiveObject obj = Objects[i];
                if (obj != null && obj.CategoryKey == categoryKey)
                {
                    result.Add(obj);
                }
            }
            return result;
        }

        /// <summary>
        /// Global cross-category event stream: the most recent
        /// <paramref name="count"/> events across ALL object types, Tick
        /// descending (newest first). Returns a defensive snapshot — the live
        /// Events list is never exposed to callers.
        ///
        /// Performance: on-demand full sort. Events are appended in tick order
        /// at capture time, but legacy migration / backfill can leave them
        /// unsorted, so a sort is the correct implementation. The Overview UI
        /// consumes this on a throttled refresh cadence (~2s), never per-frame —
        /// at colonist-scale data volume this is negligible.
        /// </summary>
        public IReadOnlyList<ChronicleEvent> GetRecentEvents(int count)
        {
            List<ChronicleEvent> result = new List<ChronicleEvent>();
            if (count <= 0 || Events == null || Events.Count == 0)
            {
                return result;
            }
            result = new List<ChronicleEvent>(Events);
            result.Sort((a, b) => b.Tick.CompareTo(a.Tick));
            if (result.Count > count)
            {
                result.RemoveRange(count, result.Count - count);
            }
            return result;
        }

        // ---- Internal write entry points (service layer only) ----

        internal bool AddObject(ArchiveObject obj)
        {
            if (obj == null || string.IsNullOrEmpty(obj.StableId))
            {
                return false;
            }
            if (objectsByStableId.ContainsKey(obj.StableId))
            {
                return false;
            }
            Objects.Add(obj);
            objectsByStableId[obj.StableId] = obj;
            MarkChanged();
            return true;
        }

        /// <summary>
        /// Adds a production aggregate to a chronicle pawn. This is a data-layer
        /// mutation so the application service never has to reach into storage
        /// collections directly.
        /// </summary>
        internal bool AddProduction(
            string pawnStableId,
            string thingDefName,
            int quantity,
            float marketValue,
            long gameTick)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(thingDefName)
                || quantity <= 0)
            {
                return false;
            }
            PawnObject pawn = GetObject(pawnStableId) as PawnObject;
            if (pawn == null)
            {
                return false;
            }
            if (pawn.Production == null)
            {
                pawn.Production = new ProductionAccumulator();
            }
            float safeValue = marketValue;
            if (float.IsNaN(safeValue) || float.IsInfinity(safeValue) || safeValue < 0f)
            {
                safeValue = 0f;
            }
            pawn.Production.Add(thingDefName, quantity, safeValue, gameTick);
            MarkChanged();
            return true;
        }

        /// <summary>
        /// Public integration samples enter through IWorkTimeCaptureService and
        /// are committed here so the data layer owns mutation/revision rules.
        /// </summary>
        internal bool AddWorkTimeSample(
            string pawnStableId,
            string workTypeDefName,
            long sampleTicks,
            long gameTick)
        {
            if (string.IsNullOrEmpty(pawnStableId)
                || string.IsNullOrEmpty(workTypeDefName)
                || sampleTicks <= 0L)
            {
                return false;
            }
            PawnObject pawn = GetObject(pawnStableId) as PawnObject;
            if (pawn == null || pawn.IsArchived)
            {
                return false;
            }
            if (pawn.WorkTime == null)
            {
                pawn.WorkTime = new WorkTimeAccumulator();
            }
            pawn.WorkTime.AddSample(workTypeDefName, sampleTicks, gameTick);
            MarkChanged();
            return true;
        }

        internal bool ArchivePawn(string stableId, string deathCauseKey, Pawn pawn = null)
        {
            ArchiveObject obj;
            if (!objectsByStableId.TryGetValue(stableId, out obj))
            {
                PawnObject pawnObject = new PawnObject
                {
                    StableId = stableId,
                    JoinTick = -1L,
                    DeathTick = -1L
                };
                // 首次建档（如从未被回填的囚犯）：按生前角色归类
                if (pawn != null)
                {
                    pawnObject.Role = ChronicleColonistScanner.ClassifyRole(pawn);
                }
                Objects.Add(pawnObject);
                objectsByStableId[stableId] = pawnObject;
                obj = pawnObject;
            }
            PawnObject target = obj as PawnObject;
            if (target == null)
            {
                return false;
            }
            if (target.IsArchived)
            {
                return false;
            }
            // 已存在记录：若角色未定（老档默认 / None）且当前能判定，补正生前角色
            if (target.Role == PawnRole.None && pawn != null)
            {
                PawnRole resolved = ChronicleColonistScanner.ClassifyRole(pawn);
                if (resolved != PawnRole.None)
                {
                    target.Role = resolved;
                }
            }
            target.DeathTick = Find.TickManager.TicksGame;
            target.DeathCauseKey = deathCauseKey;
            // v3.1: freeze career skill snapshot at death (WorkTime stops via IsArchived).
            PawnArchiveSnapshots.ApplyDeathSnapshots(target, pawn);
            MarkChanged();
            return true;
        }

        internal void AddEvent(ChronicleEvent ev)
        {
            if (ev == null)
            {
                return;
            }
            if (ev.ImportanceLevel < 0)
            {
                ev.ImportanceLevel = (int)ChronicleEventImportance.Resolve(ev);
            }
            ev.Id = NextEventId;
            NextEventId++;
            Events.Add(ev);
            AddEventEdges(ev);
            MarkChanged();
        }

        /// <summary>Marks a runtime archive mutation for read-model invalidation.</summary>
        internal void MarkChanged()
        {
            DataRevision = DataRevision == long.MaxValue ? 0L : DataRevision + 1L;
        }

        // ---- One-time legacy migration (schemaVersion 0 → 1) ----

        private void MigrateLegacyData()
        {
            // v0.2 Pawns → PawnObjects, field-by-field. Dedupe against Objects so
            // a partially migrated save is never double-written.
            HashSet<string> existing = new HashSet<string>();
            for (int i = 0; i < Objects.Count; i++)
            {
                ArchiveObject obj = Objects[i];
                if (obj != null && !string.IsNullOrEmpty(obj.StableId))
                {
                    existing.Add(obj.StableId);
                }
            }

            if (legacyPawns != null)
            {
                for (int i = 0; i < legacyPawns.Count; i++)
                {
                    PawnRecord legacy = legacyPawns[i];
                    if (legacy == null || string.IsNullOrEmpty(legacy.StableId))
                    {
                        continue;
                    }
                    if (existing.Contains(legacy.StableId))
                    {
                        continue;
                    }
                    Objects.Add(ConvertLegacyPawn(legacy));
                    existing.Add(legacy.StableId);
                }
            }

            // Legacy events: PawnStableId → Primary (no snapshot existed → null).
            for (int i = 0; i < Events.Count; i++)
            {
                ChronicleEvent ev = Events[i];
                if (ev == null)
                {
                    continue;
                }
                if (ev.Subjects == null)
                {
                    ev.Subjects = new List<ObjectRef>();
                }
                if (ev.Primary == null && !string.IsNullOrEmpty(ev.PawnStableId))
                {
                    ev.Primary = new ObjectRef(ArchiveCategoryKeys.Pawn, ev.PawnStableId, null);
                }
            }
        }

        private static PawnObject ConvertLegacyPawn(PawnRecord legacy)
        {
            return new PawnObject
            {
                StableId = legacy.StableId,
                LabelSnapshot = legacy.LabelShort,
                LabelShort = legacy.LabelShort,
                KindDefName = legacy.KindDefName,
                FactionDefName = legacy.FactionDefName,
                JoinTick = legacy.JoinTick,
                DeathTick = legacy.DeathTick,
                DeathCauseKey = legacy.DeathCauseKey,
                Role = legacy.Role
            };
        }

        /// <summary>
        /// Projects the PawnObjects into plain PawnRecords so a save loaded by
        /// v0.2 (which cannot instantiate the PawnObject class) keeps its pawn
        /// archive. Pure projection — never a source of truth.
        /// </summary>
        private List<PawnRecord> BuildLegacyPawnMirror()
        {
            List<PawnRecord> mirror = new List<PawnRecord>();
            for (int i = 0; i < Objects.Count; i++)
            {
                PawnObject pawnObject = Objects[i] as PawnObject;
                if (pawnObject == null || string.IsNullOrEmpty(pawnObject.StableId))
                {
                    continue;
                }
                PawnRecord legacy = new PawnRecord
                {
                    StableId = pawnObject.StableId,
                    LabelSnapshot = pawnObject.LabelSnapshot,
                    LabelShort = pawnObject.LabelShort,
                    KindDefName = pawnObject.KindDefName,
                    FactionDefName = pawnObject.FactionDefName,
                    JoinTick = pawnObject.JoinTick,
                    DeathTick = pawnObject.DeathTick,
                    DeathCauseKey = pawnObject.DeathCauseKey,
                    Role = pawnObject.Role
                };
                mirror.Add(legacy);
            }
            return mirror;
        }

        // ---- Index / backfill ----

        private void RebuildIndexes()
        {
            objectsByStableId = new Dictionary<string, ArchiveObject>();
            eventsByObject = new Dictionary<string, List<ChronicleEvent>>();

            for (int i = 0; i < Objects.Count; i++)
            {
                ArchiveObject obj = Objects[i];
                if (obj == null || string.IsNullOrEmpty(obj.StableId))
                {
                    continue;
                }
                if (!objectsByStableId.ContainsKey(obj.StableId))
                {
                    objectsByStableId[obj.StableId] = obj;
                }
            }

            for (int i = 0; i < Events.Count; i++)
            {
                AddEventEdges(Events[i]);
            }
        }

        /// <summary>
        /// Registers an event under every object it references (Primary first,
        /// then each Subject). Also backfills Primary from the legacy field as a
        /// defensive invariant — Primary/Subjects are the only edge source.
        /// </summary>
        private void AddEventEdges(ChronicleEvent ev)
        {
            if (ev == null)
            {
                return;
            }
            if (ev.Primary == null && !string.IsNullOrEmpty(ev.PawnStableId))
            {
                ev.Primary = new ObjectRef(ArchiveCategoryKeys.Pawn, ev.PawnStableId, null);
            }
            if (ev.Primary != null && !string.IsNullOrEmpty(ev.Primary.StableId))
            {
                AddEdge(ev.Primary.StableId, ev);
            }
            if (ev.Subjects != null)
            {
                for (int j = 0; j < ev.Subjects.Count; j++)
                {
                    ObjectRef subject = ev.Subjects[j];
                    if (subject != null && !string.IsNullOrEmpty(subject.StableId))
                    {
                        AddEdge(subject.StableId, ev);
                    }
                }
            }
        }

        private void AddEdge(string stableId, ChronicleEvent ev)
        {
            List<ChronicleEvent> list;
            if (!eventsByObject.TryGetValue(stableId, out list))
            {
                list = new List<ChronicleEvent>();
                eventsByObject[stableId] = list;
            }
            list.Add(ev);
        }

        private static void CollectEventObjectIds(ChronicleEvent ev, HashSet<string> ids)
        {
            if (ev == null)
            {
                return;
            }
            if (ev.Primary != null && !string.IsNullOrEmpty(ev.Primary.StableId))
            {
                ids.Add(ev.Primary.StableId);
            }
            if (ev.Subjects != null)
            {
                for (int j = 0; j < ev.Subjects.Count; j++)
                {
                    ObjectRef subject = ev.Subjects[j];
                    if (subject != null && !string.IsNullOrEmpty(subject.StableId))
                    {
                        ids.Add(subject.StableId);
                    }
                }
            }
        }

        private void BackfillExistingColonists()
        {
            // 活读当前殖民地全部人口（自由殖民者 / 奴隶 / 囚犯），单一谓词来源。
            // 源 = 地图 + 商队（ChronicleColonistScanner 已移除过宽的
            // Find.WorldPawns.AllPawnsAlive 世界兜底，避免在 8≠3 误统计）。
            // 角色随记录一并写入，UI 据此区分徽标。
            List<ColonyMember> people = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < people.Count; i++)
            {
                ColonyMember member = people[i];
                if (member == null || member.Pawn == null)
                {
                    continue;
                }
                AddObject(CreateRecord(member.Pawn, -1L, member.Role));
            }
        }

        private bool PruneInitialRosterArtifacts()
        {
            HashSet<string> currentIds = new HashSet<string>();
            List<ColonyMember> currentPeople = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < currentPeople.Count; i++)
            {
                ColonyMember member = currentPeople[i];
                if (member != null && member.Pawn != null)
                {
                    string id = member.Pawn.GetUniqueLoadID();
                    if (!string.IsNullOrEmpty(id))
                    {
                        currentIds.Add(id);
                    }
                }
            }

            // During a save-load transition the map lists can be temporarily
            // empty. Do not interpret that transient state as a zero-population
            // colony and delete otherwise valid unknown-join snapshots.
            if (currentIds.Count == 0)
            {
                return false;
            }

            int removed = 0;
            for (int i = Objects.Count - 1; i >= 0; i--)
            {
                PawnObject pawn = Objects[i] as PawnObject;
                if (pawn == null
                    || pawn.IsArchived
                    || pawn.JoinTick >= 0L
                    || currentIds.Contains(pawn.StableId)
                    || GetEventsFor(pawn.StableId).Count > 0)
                {
                    continue;
                }
                Objects.RemoveAt(i);
                objectsByStableId.Remove(pawn.StableId);
                removed++;
            }
            if (removed > 0)
            {
                MarkChanged();
                Log.Message("PersonalChronicle: removed " + removed
                    + " empty scenario-roster archive artifacts during v4 migration.");
            }
            return true;
        }

        private bool PruneUnconfirmedRosterArtifacts()
        {
            HashSet<string> currentIds = new HashSet<string>();
            List<ColonyMember> currentPeople = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < currentPeople.Count; i++)
            {
                ColonyMember member = currentPeople[i];
                if (member != null && member.Pawn != null)
                {
                    string stableId = member.Pawn.GetUniqueLoadID();
                    if (!string.IsNullOrEmpty(stableId))
                    {
                        currentIds.Add(stableId);
                    }
                }
            }

            // Do not consume the migration version during a transient load
            // frame where maps have not populated yet.
            if (currentIds.Count == 0)
            {
                return false;
            }

            HashSet<string> removedIds = new HashSet<string>();
            for (int i = Objects.Count - 1; i >= 0; i--)
            {
                PawnObject pawn = Objects[i] as PawnObject;
                if (pawn == null
                    || pawn.IsArchived
                    || pawn.JoinTick >= 0L
                    || currentIds.Contains(pawn.StableId))
                {
                    continue;
                }
                Objects.RemoveAt(i);
                objectsByStableId.Remove(pawn.StableId);
                removedIds.Add(pawn.StableId);
            }

            // Scenario-editor relation callbacks can leave orphaned events even
            // after their candidate PawnObject is removed. Purge only events
            // touching the exact records removed above; unrelated historical
            // events remain intact.
            int removedEvents = 0;
            if (removedIds.Count > 0)
            {
                for (int i = Events.Count - 1; i >= 0; i--)
                {
                    if (!EventReferencesAny(Events[i], removedIds))
                    {
                        continue;
                    }
                    Events.RemoveAt(i);
                    removedEvents++;
                }
            }
            if (removedIds.Count > 0 || removedEvents > 0)
            {
                MarkChanged();
                Log.Message("PersonalChronicle: removed " + removedIds.Count
                    + " unconfirmed scenario-roster archive artifacts and "
                    + removedEvents + " orphaned events during v4.0.1 migration.");
            }
            return true;
        }

        private static bool EventReferencesAny(ChronicleEvent ev, HashSet<string> stableIds)
        {
            if (ev == null || stableIds == null || stableIds.Count == 0)
            {
                return false;
            }
            if (ev.Primary != null && stableIds.Contains(ev.Primary.StableId))
            {
                return true;
            }
            if (ev.Subjects == null)
            {
                return false;
            }
            for (int i = 0; i < ev.Subjects.Count; i++)
            {
                ObjectRef subject = ev.Subjects[i];
                if (subject != null && stableIds.Contains(subject.StableId))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// "Chronicle colonist" semantics: single source of truth lives in
        /// <see cref="ChronicleColonistScanner"/>. Backfill calls the shared
        /// predicate — never copy it here (口径分裂 = P1).
        /// </summary>
        private static PawnObject CreateRecord(Pawn pawn, long joinTick, PawnRole role)
        {
            PawnObject record = new PawnObject
            {
                StableId = pawn.GetUniqueLoadID(),
                LabelSnapshot = pawn.LabelShort,
                LabelShort = pawn.LabelShort,
                KindDefName = pawn.kindDef != null ? pawn.kindDef.defName : null,
                FactionDefName = pawn.Faction != null && pawn.Faction.def != null ? pawn.Faction.def.defName : null,
                JoinTick = joinTick,
                DeathTick = -1L,
                DeathCauseKey = null,
                Role = role
            };
            PawnArchiveSnapshots.ApplyJoinSnapshots(record, pawn);
            CapturePrimaryPlace(record, pawn);
            return record;
        }

        /// <summary>
        /// Fallback join event for colonists discovered by the reconcile safety
        /// net rather than Pawn.SetFaction. The live scan proves presence but
        /// not the historical join tick, so the event records detection time
        /// while keeping JoinTick unknown (-1) on the snapshot.
        /// </summary>
        private void AddDetectedJoinEvent(PawnObject record)
        {
            if (record == null || string.IsNullOrEmpty(record.StableId))
            {
                return;
            }
            AddEvent(new ChronicleEvent
            {
                Tick = Find.TickManager.TicksGame,
                TypeKey = ChronicleEventType.Join,
                Primary = ObjectRef.ForPawn(record.StableId, record.LabelSnapshot),
                Subjects = new List<ObjectRef>(),
                Params = new Dictionary<string, string>()
            });
        }

        /// <summary>
        /// v4.0 home overview (E / chronicle-timeline view): returns ALL events for
        /// timeline rendering. Caller must filter by Importance / sort as needed; this
        /// is a defensive snapshot of the live Events list, never the list itself.
        /// Consumed on the throttled home refresh cadence, never per-frame.
        /// </summary>
        public IReadOnlyList<ChronicleEvent> GetAllEvents()
        {
            if (Events == null || Events.Count == 0)
            {
                return new List<ChronicleEvent>();
            }
            return new List<ChronicleEvent>(Events);
        }
    }
}

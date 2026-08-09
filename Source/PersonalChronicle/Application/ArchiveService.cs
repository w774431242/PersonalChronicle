using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// Application-layer orchestrator. Stateless: every call resolves the
    /// ChronicleGameComponent from the active game and delegates to it.
    /// Enforces the recording toggle and the per-pawn event cap.
    ///
    /// v2.1: writes go through the ArchiveObject model (PawnObject + ObjectRef
    /// Primary). The v0.2 PawnStableId legacy field is kept in sync as a shadow
    /// inside ChronicleEvent.ExposeData — the service never writes it directly.
    /// </summary>
    public sealed class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService
    {
        // TypeKey constants live in ChronicleEventType (single source of truth,
        // validated against Defs/Chronicle_Events.xml at startup).

        /// <summary>Cache window for live-pawn resolution (in game ticks).</summary>
        private const long LivePawnCacheWindow = 120L;

        /// <summary>Cache window for the live colonist count (home-page stat; in game ticks).</summary>
        private const long LiveCountCacheWindow = 600L;

        /// <summary>Career aggregate cache window; source invalidation is also
        /// keyed by ChronicleGameComponent.DataRevision.</summary>
        private const long WorkIntensityCacheWindow = 600L;

        /// <summary>Prefix of Thing.GetUniqueLoadID() = "Thing_" + defName + "_" + thingIDNumber.</summary>
        private const string ThingIdPrefix = "Thing_";

        /// <summary>
        /// Live-pawn cache keyed by thingIDNumber (int, zero string alloc per
        /// pawn during rebuild). Game-switch detection compares the resolved
        /// game component; a changed instance means a new game/save load.
        /// [Unsaved]: never persisted.
        /// </summary>
        [Unsaved]
        private ChronicleGameComponent cacheGameComponent;

        [Unsaved]
        private Dictionary<int, Pawn> livePawnCache = new Dictionary<int, Pawn>();

        [Unsaved]
        private long livePawnCacheTick = -1L;

        // ---- v2.4 live colonist count cache ----
        // Independent of livePawnCache: different window (600 vs 120 ticks)
        // and different semantics (count, not per-pawn resolution). A full
        // ChronicleColonistScanner scan runs at most once per window; the home
        // page reads the cached int. Game-switch detection mirrors livePawnCache
        // (component-instance comparison).
        [Unsaved]
        private ChronicleGameComponent liveCountCacheComponent;

        [Unsaved]
        private int cachedLiveColonistCount;

        [Unsaved]
        private int cachedFreeColonistCount;

        [Unsaved]
        private int cachedSlaveCount;

        [Unsaved]
        private int cachedPrisonerCount;

        [Unsaved]
        private long liveCountCacheTick = -1L;

        [Unsaved]
        private readonly IWorkIntensityProviderRegistry workIntensityProviders;

        [Unsaved]
        private WorkIntensityPolicySnapshot workIntensityPolicy;

        [Unsaved]
        private ChronicleGameComponent workIntensityCacheComponent;

        [Unsaved]
        private long workIntensityCacheRevision = -1L;

        [Unsaved]
        private long workIntensityCacheTick = -1L;

        [Unsaved]
        private ColonyWorkAggregateView cachedColonyWorkAggregate;

        [Unsaved]
        private readonly HashSet<string> warnedIntensityProviders = new HashSet<string>();

        public ArchiveService()
            : this(new WorkIntensityProviderRegistry())
        {
        }

        public ArchiveService(IWorkIntensityProviderRegistry providers)
        {
            workIntensityProviders = providers ?? new WorkIntensityProviderRegistry();
        }

        private static ChronicleGameComponent Component
        {
            get
            {
                if (Current.Game == null)
                {
                    return null;
                }
                return Current.Game.GetComponent<ChronicleGameComponent>();
            }
        }

        public IReadOnlyList<PawnRecord> GetAllRecords()
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<PawnRecord>();
            }
            return component.GetAllRecords();
        }

        public IReadOnlyList<PawnRecord> GetActiveRecords()
        {
            return GetAllRecords().Where(record => !record.IsArchived).ToList();
        }

        public IReadOnlyList<PawnRecord> GetArchivedRecords()
        {
            return GetAllRecords().Where(record => record.IsArchived).ToList();
        }

        public IReadOnlyList<ChronicleEvent> GetEventsFor(string stableId)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ChronicleEvent>();
            }
            return component.GetEventsFor(stableId);
        }

        public ArchiveObject GetObject(string stableId)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return null;
            }
            return component.GetObject(stableId);
        }

        public IReadOnlyList<ArchiveObject> GetLinkedObjects(string stableId)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ArchiveObject>();
            }
            return component.GetLinkedObjects(stableId);
        }

        public IReadOnlyList<ArchiveObject> GetObjectsOfCategory(string categoryKey)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ArchiveObject>();
            }
            return component.GetObjectsOfCategory(categoryKey);
        }

        /// <summary>
        /// Runtime-only invalidation token for UI read models. The archive
        /// contents remain owned by ChronicleGameComponent; this value only
        /// tells consumers that a fresh snapshot is available.
        /// </summary>
        public long GetDataRevision()
        {
            ChronicleGameComponent component = Component;
            return component != null ? component.DataRevision : -1L;
        }

        /// <summary>
        /// Resolves the live Pawn matching a stable id (null if absent or dead).
        /// UI tabs (Skills/Health/Relations) call this every frame while open;
        /// the O(N) pawn scan is therefore cached: a full rebuild runs at most
        /// once per <see cref="LivePawnCacheWindow"/> ticks, and per-frame
        /// lookups are O(1) dictionary hits (no GetUniqueLoadID allocations).
        ///
        /// The cache is keyed by thingIDNumber parsed from the stableId tail
        /// ("Thing_&lt;defName&gt;_&lt;number&gt;"), matched against
        /// Pawn.thingIDNumber — zero per-pawn string allocation during scans.
        /// Dead/destroyed pawns are evicted on read; a game switch (new
        /// game/load) drops the whole cache via component-instance comparison.
        /// </summary>
        public Pawn GetLivePawn(string stableId)
        {
            if (string.IsNullOrEmpty(stableId) || Current.Game == null)
            {
                return null;
            }
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return null;
            }
            // Game switch (new game / save load): the component instance
            // changes → the cached pawn map belongs to a dead session.
            if (!ReferenceEquals(cacheGameComponent, component))
            {
                cacheGameComponent = component;
                livePawnCache.Clear();
                livePawnCacheTick = -1L;
            }

            if (!TryParseThingId(stableId, out string defName, out int thingId))
            {
                // Non-standard stable id: fall back to exact string matching.
                return FindLivePawnByUniqueLoadId(stableId);
            }

            long tick = GenTicks.TicksGame;
            if (livePawnCacheTick >= 0L && tick - livePawnCacheTick <= LivePawnCacheWindow)
            {
                // Cache window hit: O(1) lookup, no pawn scan.
                if (livePawnCache.TryGetValue(thingId, out Pawn cached))
                {
                    if (cached != null && !cached.Dead && !cached.Destroyed
                        && cached.def != null && cached.def.defName == defName)
                    {
                        return cached;
                    }
                    livePawnCache.Remove(thingId);
                }
                return null;
            }

            RebuildLivePawnCache();
            livePawnCacheTick = tick;
            if (livePawnCache.TryGetValue(thingId, out Pawn hit)
                && hit.def != null && hit.def.defName == defName)
            {
                return hit;
            }
            return null;
        }

        /// <summary>
        /// Parses a Thing stable id "Thing_&lt;defName&gt;_&lt;thingIDNumber&gt;".
        /// Returns false for anything not in that shape (defensive fallback).
        /// </summary>
        private static bool TryParseThingId(string stableId, out string defName, out int thingId)
        {
            defName = null;
            thingId = 0;
            if (string.IsNullOrEmpty(stableId) || !stableId.StartsWith(ThingIdPrefix, StringComparison.Ordinal))
            {
                return false;
            }
            int lastIdx = stableId.LastIndexOf('_');
            if (lastIdx < ThingIdPrefix.Length)
            {
                return false;
            }
            defName = stableId.Substring(ThingIdPrefix.Length, lastIdx - ThingIdPrefix.Length);
            return int.TryParse(stableId.Substring(lastIdx + 1), out thingId);
        }

        /// <summary>
        /// Full rebuild of the live-pawn map: every non-dead pawn on every map
        /// plus alive world pawns, keyed by thingIDNumber. thingIDNumber is a
        /// per-session unique counter, so int keys cannot collide across pawns.
        /// </summary>
        private void RebuildLivePawnCache()
        {
            livePawnCache.Clear();
            List<Map> maps = Find.Maps;
            if (maps != null)
            {
                for (int i = 0; i < maps.Count; i++)
                {
                    Map map = maps[i];
                    if (map == null || map.mapPawns == null)
                    {
                        continue;
                    }
                    List<Pawn> allPawns = map.mapPawns.AllPawns;
                    for (int j = 0; j < allPawns.Count; j++)
                    {
                        Pawn pawn = allPawns[j];
                        if (pawn == null || pawn.Dead || pawn.Destroyed)
                        {
                            continue;
                        }
                        livePawnCache[pawn.thingIDNumber] = pawn;
                    }
                }
            }
            List<Pawn> worldPawns = Find.WorldPawns.AllPawnsAlive;
            if (worldPawns != null)
            {
                for (int i = 0; i < worldPawns.Count; i++)
                {
                    Pawn pawn = worldPawns[i];
                    if (pawn == null || pawn.Dead || pawn.Destroyed)
                    {
                        continue;
                    }
                    livePawnCache[pawn.thingIDNumber] = pawn;
                }
            }
        }

        /// <summary>
        /// Exact-string fallback for stable ids that are not in the standard
        /// "Thing_&lt;defName&gt;_&lt;number&gt;" shape (same logic as the
        /// pre-v2.2 implementation).
        /// </summary>
        private static Pawn FindLivePawnByUniqueLoadId(string stableId)
        {
            List<Map> maps = Find.Maps;
            if (maps != null)
            {
                for (int i = 0; i < maps.Count; i++)
                {
                    Map map = maps[i];
                    if (map == null || map.mapPawns == null)
                    {
                        continue;
                    }
                    List<Pawn> allPawns = map.mapPawns.AllPawns;
                    for (int j = 0; j < allPawns.Count; j++)
                    {
                        Pawn pawn = allPawns[j];
                        if (pawn == null || pawn.Dead || pawn.Destroyed)
                        {
                            continue;
                        }
                        if (pawn.GetUniqueLoadID() == stableId)
                        {
                            return pawn;
                        }
                    }
                }
            }
            List<Pawn> worldPawns = Find.WorldPawns.AllPawnsAlive;
            if (worldPawns != null)
            {
                for (int i = 0; i < worldPawns.Count; i++)
                {
                    Pawn pawn = worldPawns[i];
                    if (pawn == null || pawn.Dead || pawn.Destroyed)
                    {
                        continue;
                    }
                    if (pawn.GetUniqueLoadID() == stableId)
                    {
                        return pawn;
                    }
                }
            }
            return null;
        }

        public IReadOnlyList<ChronicleEvent> GetRecentEvents(int count)
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ChronicleEvent>();
            }
            return component.GetRecentEvents(count);
        }

        public ArchiveDepthBehavior GetCategoryBehavior(string categoryKey)
        {
            if (string.IsNullOrEmpty(categoryKey))
            {
                return ArchiveDepthBehavior.Record;
            }
            // Def-driven: categoryKey bridges data keys to ArchiveCategoryDef.
            // Unknown category falls back to Record (conservative default) so a
            // missing Def degrades to archive-record behavior, never to nothing.
            ArchiveCategoryDef def = DefDatabase<ArchiveCategoryDef>.AllDefs
                .FirstOrDefault(d => d.categoryKey == categoryKey);
            if (def == null)
            {
                return ArchiveDepthBehavior.Record;
            }
            return def.behavior;
        }

        /// <summary>
        /// Work priorities for the Work/Places tab. Live path reads
        /// Pawn_WorkSettings (free colonists only — non-free pawns return an
        /// empty list); dead/absent pawns fall back to the archived
        /// PawnObject.WorkSnapshot. Both paths return only Priority &gt; 0 so a
        /// live pawn and its archived snapshot render identically.
        /// </summary>
        public IReadOnlyList<WorkPriorityView> GetWorkPriorities(string stableId)
        {
            Pawn pawn = GetLivePawn(stableId);
            if (pawn != null && pawn.IsFreeColonist && pawn.workSettings != null)
            {
                List<WorkPriorityView> live = new List<WorkPriorityView>();
                List<WorkTypeDef> workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading;
                for (int i = 0; i < workTypes.Count; i++)
                {
                    WorkTypeDef wt = workTypes[i];
                    if (wt == null)
                    {
                        continue;
                    }
                    int priority = pawn.workSettings.GetPriority(wt);
                    if (priority > 0)
                    {
                        live.Add(new WorkPriorityView(wt.defName, priority));
                    }
                }
                return live;
            }

            // Dead / absent pawn: degraded read from the archived snapshot (LA-1).
            PawnObject pawnObject = GetObject(stableId) as PawnObject;
            if (pawnObject != null && pawnObject.WorkSnapshot != null)
            {
                List<WorkPriorityView> degraded = new List<WorkPriorityView>();
                for (int i = 0; i < pawnObject.WorkSnapshot.Count; i++)
                {
                    WorkPrioritySnapshot entry = pawnObject.WorkSnapshot[i];
                    if (entry != null && !string.IsNullOrEmpty(entry.WorkTypeDefName) && entry.Priority > 0)
                    {
                        degraded.Add(new WorkPriorityView(entry.WorkTypeDefName, entry.Priority));
                    }
                }
                return degraded;
            }
            return new List<WorkPriorityView>();
        }

        /// <summary>
        /// v3.1: cumulative work-time ledger for the Career tab.
        /// </summary>
        public IReadOnlyList<WorkTimeStatView> GetWorkTimeStats(string stableId)
        {
            PawnObject pawnObject = GetObject(stableId) as PawnObject;
            if (pawnObject == null || pawnObject.WorkTime == null
                || pawnObject.WorkTime.TicksByWorkType == null
                || pawnObject.WorkTime.TicksByWorkType.Count == 0)
            {
                return new List<WorkTimeStatView>();
            }
            long total = pawnObject.WorkTime.TotalWorkTicks;
            if (total <= 0L)
            {
                // Recompute total if ledger was loaded without total field.
                total = 0L;
                foreach (KeyValuePair<string, long> pair in pawnObject.WorkTime.TicksByWorkType)
                {
                    total += pair.Value;
                }
            }
            List<KeyValuePair<string, long>> rows = new List<KeyValuePair<string, long>>();
            foreach (KeyValuePair<string, long> pair in pawnObject.WorkTime.TicksByWorkType)
            {
                if (!string.IsNullOrEmpty(pair.Key) && pair.Value > 0L)
                {
                    rows.Add(pair);
                }
            }
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));
            List<WorkTimeStatView> result = new List<WorkTimeStatView>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                KeyValuePair<string, long> row = rows[i];
                long lastTick = 0L;
                if (pawnObject.WorkTime.LastTickByWorkType != null)
                {
                    pawnObject.WorkTime.LastTickByWorkType.TryGetValue(row.Key, out lastTick);
                }
                float share = total > 0L ? (float)row.Value / (float)total : 0f;
                result.Add(new WorkTimeStatView(row.Key, row.Value, lastTick, share, i + 1));
            }
            return result;
        }

        // ---- v4.1 work-intensity service ------------------------------------

        public WorkIntensityView GetWorkIntensity(string stableId)
        {
            PawnObject pawn = GetObject(stableId) as PawnObject;
            if (pawn == null || pawn.WorkTime == null)
            {
                return new WorkIntensityView(
                    WorkIntensityEvaluation.Undefined(null, "builtin"), null);
            }

            long totalTicks = GetTotalWorkTicks(pawn.WorkTime);
            long firstTick = pawn.JoinTick >= 0L
                ? pawn.JoinTick
                : pawn.WorkTime.FirstObservedTick;
            double observedDays = GetObservedDays(pawn, firstTick);
            if (totalTicks <= 0L || observedDays <= 0d)
            {
                return new WorkIntensityView(
                    WorkIntensityEvaluation.Undefined(
                        new WorkIntensityInput(0L, 0d, observedDays, 0d),
                        "builtin"),
                    null);
            }

            ColonyWorkAggregateView aggregate = GetColonyWorkAggregate();
            double totalHours = totalTicks / (double)RimWorld.GenDate.TicksPerHour;
            WorkIntensityInput input = new WorkIntensityInput(
                totalTicks,
                totalHours,
                observedDays,
                aggregate != null ? aggregate.AverageDailyHours : 0d);
            WorkIntensityEvaluation evaluation = EvaluateWithProviders(input);
            WorkIntensityTierSpec tier = FindTier(evaluation != null ? evaluation.TierDefName : null);
            return new WorkIntensityView(evaluation, tier);
        }

        public IReadOnlyList<WorkIntensityTierView> GetIntensityTiers()
        {
            IReadOnlyList<WorkIntensityTierSpec> tiers = GetWorkIntensityPolicy().Tiers;
            List<WorkIntensityTierView> result = new List<WorkIntensityTierView>(tiers.Count);
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i] != null)
                {
                    result.Add(new WorkIntensityTierView(tiers[i]));
                }
            }
            return result;
        }

        public ColonyWorkAggregateView GetColonyWorkAggregate()
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return EmptyColonyWorkAggregate();
            }

            long tick = GenTicks.TicksGame;
            if (cachedColonyWorkAggregate != null
                && ReferenceEquals(workIntensityCacheComponent, component)
                && workIntensityCacheRevision == component.DataRevision
                && tick - workIntensityCacheTick <= WorkIntensityCacheWindow)
            {
                return cachedColonyWorkAggregate;
            }

            cachedColonyWorkAggregate = BuildColonyWorkAggregate(component);
            workIntensityCacheComponent = component;
            workIntensityCacheRevision = component.DataRevision;
            workIntensityCacheTick = tick;
            return cachedColonyWorkAggregate;
        }

        public IReadOnlyList<WorkIntensityWorkTypeView> GetWorkTypeBreakdown(
            string stableId,
            bool includeZeroWorkTypes)
        {
            PawnObject pawn = GetObject(stableId) as PawnObject;
            if (pawn == null || pawn.WorkTime == null)
            {
                return new List<WorkIntensityWorkTypeView>();
            }

            ColonyWorkAggregateView aggregate = GetColonyWorkAggregate();
            Dictionary<string, List<long>> leaderboard = BuildWorkTypeLeaderboard();
            Dictionary<string, long> ownTicks = new Dictionary<string, long>();
            Dictionary<string, long> ownLastTicks = new Dictionary<string, long>();
            if (pawn.WorkTime.TicksByWorkType != null)
            {
                foreach (KeyValuePair<string, long> pair in pawn.WorkTime.TicksByWorkType)
                {
                    if (!string.IsNullOrEmpty(pair.Key) && pair.Value > 0L)
                    {
                        ownTicks[pair.Key] = pair.Value;
                        long lastTick = 0L;
                        if (pawn.WorkTime.LastTickByWorkType != null)
                        {
                            pawn.WorkTime.LastTickByWorkType.TryGetValue(pair.Key, out lastTick);
                        }
                        ownLastTicks[pair.Key] = lastTick;
                    }
                }
            }

            if (includeZeroWorkTypes)
            {
                List<WorkTypeDef> workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading;
                for (int i = 0; i < workTypes.Count; i++)
                {
                    WorkTypeDef workType = workTypes[i];
                    if (workType != null && !ownTicks.ContainsKey(workType.defName))
                    {
                        ownTicks[workType.defName] = 0L;
                        ownLastTicks[workType.defName] = 0L;
                    }
                }
            }

            List<KeyValuePair<string, long>> rows =
                new List<KeyValuePair<string, long>>(ownTicks);
            rows.Sort(CompareWorkRows);
            long ownTotal = GetTotalWorkTicks(pawn.WorkTime);
            List<WorkIntensityWorkTypeView> result =
                new List<WorkIntensityWorkTypeView>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                string workTypeDefName = rows[i].Key;
                long ticks = rows[i].Value;
                ColonyWorkTypeView colonyType = FindColonyWorkType(
                    aggregate, workTypeDefName);
                List<long> values;
                leaderboard.TryGetValue(workTypeDefName, out values);
                int rank = ticks > 0L && values != null ? RankOf(values, ticks) : 0;
                int populationCount = values != null ? values.Count : 0;
                long maximum = colonyType != null ? colonyType.MaximumPawnTicks : 0L;
                float relative = maximum > 0L ? (float)ticks / maximum : 0f;
                float share = ownTotal > 0L ? (float)ticks / ownTotal : 0f;
                long lastTick;
                ownLastTicks.TryGetValue(workTypeDefName, out lastTick);
                result.Add(new WorkIntensityWorkTypeView(
                    workTypeDefName,
                    ticks,
                    lastTick,
                    share,
                    rank,
                    populationCount,
                    colonyType != null ? colonyType.TotalTicks : 0L,
                    maximum,
                    relative));
            }
            return result;
        }

        public bool RecordSample(WorkTimeSample sample)
        {
            if (sample == null || string.IsNullOrEmpty(sample.PawnStableId)
                || string.IsNullOrEmpty(sample.WorkTypeDefName)
                || sample.SampleTicks <= 0L)
            {
                return false;
            }
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return false;
            }
            Pawn live = GetLivePawn(sample.PawnStableId);
            if (live == null || !ChronicleColonistScanner.TryClassifyCurrent(live, out _))
            {
                return false;
            }
            long gameTick = sample.GameTick > 0L ? sample.GameTick : GenTicks.TicksGame;
            return component.AddWorkTimeSample(
                sample.PawnStableId,
                sample.WorkTypeDefName,
                sample.SampleTicks,
                gameTick);
        }

        private WorkIntensityPolicySnapshot GetWorkIntensityPolicy()
        {
            if (workIntensityPolicy == null)
            {
                workIntensityPolicy = WorkIntensityDefCatalog.Load();
            }
            return workIntensityPolicy;
        }

        private WorkIntensityEvaluation EvaluateWithProviders(WorkIntensityInput input)
        {
            IReadOnlyList<IWorkIntensityProvider> providers = workIntensityProviders.Providers;
            for (int i = 0; i < providers.Count; i++)
            {
                IWorkIntensityProvider provider = providers[i];
                if (provider == null)
                {
                    continue;
                }
                try
                {
                    WorkIntensityEvaluation evaluation;
                    if (provider.TryEvaluate(input, out evaluation) && evaluation != null)
                    {
                        return evaluation;
                    }
                }
                catch (Exception ex)
                {
                    if (warnedIntensityProviders.Add(provider.ProviderId))
                    {
                        Log.Warning("PersonalChronicle: work-intensity provider failed once, fallback to next provider: "
                            + provider.ProviderId + " / " + ex.Message);
                    }
                }
            }
            return WorkIntensityEvaluator.Evaluate(input, GetWorkIntensityPolicy());
        }

        private WorkIntensityTierSpec FindTier(string tierDefName)
        {
            if (string.IsNullOrEmpty(tierDefName))
            {
                return null;
            }
            IReadOnlyList<WorkIntensityTierSpec> tiers = GetWorkIntensityPolicy().Tiers;
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i] != null && tiers[i].DefName == tierDefName)
                {
                    return tiers[i];
                }
            }
            WorkIntensityTierSpec externalTier;
            return WorkIntensityDefCatalog.TryLoadTier(tierDefName, out externalTier)
                ? externalTier : null;
        }

        private ColonyWorkAggregateView BuildColonyWorkAggregate(
            ChronicleGameComponent component)
        {
            Dictionary<string, long> totals = new Dictionary<string, long>();
            Dictionary<string, long> maxima = new Dictionary<string, long>();
            Dictionary<string, int> participantCounts = new Dictionary<string, int>();
            List<ColonyMember> people = ChronicleColonistScanner.EnumerateCurrentPeople();
            int participantsWithWork = 0;
            long totalTicks = 0L;
            double dailySum = 0d;
            int dailyCount = 0;

            for (int i = 0; i < people.Count; i++)
            {
                ColonyMember member = people[i];
                if (member == null || member.Pawn == null)
                {
                    continue;
                }
                PawnObject pawn = component.GetObject(member.Pawn.GetUniqueLoadID()) as PawnObject;
                if (pawn == null || pawn.WorkTime == null)
                {
                    continue;
                }
                long pawnTotal = GetTotalWorkTicks(pawn.WorkTime);
                if (pawnTotal <= 0L)
                {
                    continue;
                }
                participantsWithWork++;
                totalTicks = SafeAdd(totalTicks, pawnTotal);
                long firstTick = pawn.JoinTick >= 0L
                    ? pawn.JoinTick : pawn.WorkTime.FirstObservedTick;
                double days = GetObservedDays(pawn, firstTick);
                if (days > 0d)
                {
                    dailySum += pawnTotal / (double)RimWorld.GenDate.TicksPerHour / days;
                    dailyCount++;
                }

                if (pawn.WorkTime.TicksByWorkType == null)
                {
                    continue;
                }
                foreach (KeyValuePair<string, long> pair in pawn.WorkTime.TicksByWorkType)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0L)
                    {
                        continue;
                    }
                    long currentTotal;
                    totals.TryGetValue(pair.Key, out currentTotal);
                    totals[pair.Key] = SafeAdd(currentTotal, pair.Value);
                    long currentMax;
                    maxima.TryGetValue(pair.Key, out currentMax);
                    if (pair.Value > currentMax)
                    {
                        maxima[pair.Key] = pair.Value;
                    }
                    int count;
                    participantCounts.TryGetValue(pair.Key, out count);
                    participantCounts[pair.Key] = count + 1;
                }
            }

            List<string> names = new List<string>(totals.Keys);
            names.Sort((a, b) => totals[b].CompareTo(totals[a]));
            List<ColonyWorkTypeView> workTypes = new List<ColonyWorkTypeView>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                workTypes.Add(new ColonyWorkTypeView(
                    name,
                    totals[name],
                    participantCounts[name],
                    maxima[name]));
            }
            return new ColonyWorkAggregateView(
                people.Count,
                participantsWithWork,
                totalTicks,
                totalTicks / (double)RimWorld.GenDate.TicksPerHour,
                dailyCount > 0 ? dailySum / dailyCount : 0d,
                workTypes);
        }

        private Dictionary<string, List<long>> BuildWorkTypeLeaderboard()
        {
            Dictionary<string, List<long>> result = new Dictionary<string, List<long>>();
            List<ColonyMember> people = ChronicleColonistScanner.EnumerateCurrentPeople();
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return result;
            }
            for (int i = 0; i < people.Count; i++)
            {
                ColonyMember member = people[i];
                if (member == null || member.Pawn == null)
                {
                    continue;
                }
                PawnObject pawn = component.GetObject(member.Pawn.GetUniqueLoadID()) as PawnObject;
                if (pawn == null || pawn.WorkTime == null || pawn.WorkTime.TicksByWorkType == null)
                {
                    continue;
                }
                foreach (KeyValuePair<string, long> pair in pawn.WorkTime.TicksByWorkType)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0L)
                    {
                        continue;
                    }
                    List<long> values;
                    if (!result.TryGetValue(pair.Key, out values))
                    {
                        values = new List<long>();
                        result[pair.Key] = values;
                    }
                    values.Add(pair.Value);
                }
            }
            foreach (List<long> values in result.Values)
            {
                values.Sort((a, b) => b.CompareTo(a));
            }
            return result;
        }

        private static ColonyWorkTypeView FindColonyWorkType(
            ColonyWorkAggregateView aggregate,
            string workTypeDefName)
        {
            if (aggregate == null || aggregate.WorkTypes == null)
            {
                return null;
            }
            for (int i = 0; i < aggregate.WorkTypes.Count; i++)
            {
                ColonyWorkTypeView item = aggregate.WorkTypes[i];
                if (item != null && item.WorkTypeDefName == workTypeDefName)
                {
                    return item;
                }
            }
            return null;
        }

        private static int RankOf(List<long> values, long ticks)
        {
            int rank = 1;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] > ticks)
                {
                    rank++;
                }
            }
            return rank;
        }

        private static int CompareWorkRows(
            KeyValuePair<string, long> a,
            KeyValuePair<string, long> b)
        {
            int ticks = b.Value.CompareTo(a.Value);
            return ticks != 0
                ? ticks
                : string.Compare(a.Key, b.Key, StringComparison.Ordinal);
        }

        private static long GetTotalWorkTicks(WorkTimeAccumulator workTime)
        {
            if (workTime == null)
            {
                return 0L;
            }
            if (workTime.TotalWorkTicks > 0L)
            {
                return workTime.TotalWorkTicks;
            }
            long total = 0L;
            if (workTime.TicksByWorkType != null)
            {
                foreach (KeyValuePair<string, long> pair in workTime.TicksByWorkType)
                {
                    if (pair.Value > 0L)
                    {
                        total = SafeAdd(total, pair.Value);
                    }
                }
            }
            return total;
        }

        private static double GetObservedDays(PawnObject pawn, long firstTick)
        {
            if (pawn == null || firstTick < 0L)
            {
                return 0d;
            }
            long endTick = pawn.IsArchived && pawn.DeathTick > firstTick
                ? pawn.DeathTick
                : GenTicks.TicksGame;
            if (endTick <= firstTick)
            {
                return 0d;
            }
            return (endTick - firstTick) / (double)RimWorld.GenDate.TicksPerDay;
        }

        private static long SafeAdd(long left, long right)
        {
            return right > 0L && left > long.MaxValue - right
                ? long.MaxValue
                : left + right;
        }

        private static ColonyWorkAggregateView EmptyColonyWorkAggregate()
        {
            return new ColonyWorkAggregateView(0, 0, 0L, 0d, 0d,
                new List<ColonyWorkTypeView>());
        }

        /// <summary>
        /// v3.1: skill join snapshot vs death (or live) levels.
        /// </summary>
        public IReadOnlyList<SkillArchiveView> GetSkillArchive(string stableId)
        {
            PawnObject pawnObject = GetObject(stableId) as PawnObject;
            if (pawnObject == null)
            {
                return new List<SkillArchiveView>();
            }
            Dictionary<string, int> join = pawnObject.SkillSnapshot ?? new Dictionary<string, int>();
            Dictionary<string, int> end = new Dictionary<string, int>();
            if (pawnObject.IsArchived && pawnObject.SkillSnapshotOnDeath != null && pawnObject.SkillSnapshotOnDeath.Count > 0)
            {
                foreach (KeyValuePair<string, int> pair in pawnObject.SkillSnapshotOnDeath)
                {
                    end[pair.Key] = pair.Value;
                }
            }
            else
            {
                Pawn live = GetLivePawn(stableId);
                if (live != null && live.skills != null && live.skills.skills != null)
                {
                    List<SkillRecord> skills = live.skills.skills;
                    for (int i = 0; i < skills.Count; i++)
                    {
                        SkillRecord skill = skills[i];
                        if (skill != null && skill.def != null)
                        {
                            end[skill.def.defName] = skill.Level;
                        }
                    }
                }
                else if (join.Count > 0)
                {
                    foreach (KeyValuePair<string, int> pair in join)
                    {
                        end[pair.Key] = pair.Value;
                    }
                }
            }

            HashSet<string> keys = new HashSet<string>();
            foreach (string k in join.Keys) keys.Add(k);
            foreach (string k in end.Keys) keys.Add(k);
            List<SkillArchiveView> result = new List<SkillArchiveView>();
            foreach (string key in keys)
            {
                int j = 0;
                int e = 0;
                join.TryGetValue(key, out j);
                end.TryGetValue(key, out e);
                result.Add(new SkillArchiveView(key, j, e));
            }
            result.Sort((a, b) => b.Delta.CompareTo(a.Delta));
            return result;
        }

        /// <summary>
        /// v4.0 production summary. The aggregate is persisted on PawnObject so
        /// routine craft events do not need to be replayed by the UI.
        /// </summary>
        public ProductionSummaryView GetProductionSummary(string stableId)
        {
            PawnObject pawn = GetObject(stableId) as PawnObject;
            if (pawn == null || pawn.Production == null)
            {
                return new ProductionSummaryView(0, 0f, -1L, new List<ProductionTypeView>());
            }
            ProductionAccumulator production = pawn.Production;
            List<ProductionTypeView> rows = new List<ProductionTypeView>();
            if (production.QuantityByDef != null)
            {
                foreach (KeyValuePair<string, int> pair in production.QuantityByDef)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0)
                    {
                        continue;
                    }
                    float value = 0f;
                    if (production.MarketValueByDef != null)
                    {
                        production.MarketValueByDef.TryGetValue(pair.Key, out value);
                    }
                    rows.Add(new ProductionTypeView(pair.Key, pair.Value, value));
                }
            }
            // Legacy saves have production events but no v4 aggregate. Count
            // those events as a conservative quantity fallback; historical
            // market value is intentionally left at zero because old events
            // never persisted a value snapshot.
            if (rows.Count == 0)
            {
                Dictionary<string, int> legacyCounts = new Dictionary<string, int>();
                IReadOnlyList<ChronicleEvent> legacyEvents = GetProductionEvents(stableId);
                for (int i = 0; i < legacyEvents.Count; i++)
                {
                    ChronicleEvent ev = legacyEvents[i];
                    if (ev == null || ev.Primary == null || string.IsNullOrEmpty(ev.Primary.StableId))
                    {
                        continue;
                    }
                    string defName = ev.Primary.StableId;
                    int colon = defName.IndexOf(':');
                    if (colon > 0)
                    {
                        defName = defName.Substring(0, colon);
                    }
                    int count;
                    if (!legacyCounts.TryGetValue(defName, out count))
                    {
                        count = 0;
                    }
                    legacyCounts[defName] = count + 1;
                }
                foreach (KeyValuePair<string, int> pair in legacyCounts)
                {
                    rows.Add(new ProductionTypeView(pair.Key, pair.Value, 0f));
                }
            }
            rows.Sort((a, b) => b.MarketValue.CompareTo(a.MarketValue));
            int totalQuantity = production.TotalQuantity;
            float totalValue = production.TotalMarketValue;
            if (totalQuantity <= 0 && rows.Count > 0)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    totalQuantity += rows[i].Quantity;
                    totalValue += rows[i].MarketValue;
                }
            }
            return new ProductionSummaryView(
                totalQuantity,
                totalValue,
                production.LastProductionTick,
                rows);
        }

        /// <summary>
        /// Live location of a pawn: on-map (map biome/defName) or in a world
        /// caravan (world tile). None when the pawn is dead, absent or not
        /// resolvable. Matches LocationObject.MapDefName/WorldTile semantics so
        /// the live tab and the archived object describe the same facts.
        /// </summary>
        public LocationInfo GetLiveLocation(string stableId)
        {
            Pawn pawn = GetLivePawn(stableId);
            if (pawn == null)
            {
                return new LocationInfo(LocationKind.None, null, -1);
            }
            Map map = pawn.Map;
            if (map != null)
            {
                // 1.6: Map.Biome (BiomeDef) — Map has no direct `def` property.
                string mapDefName = map.Biome != null ? map.Biome.defName : null;
                return new LocationInfo(LocationKind.Map, mapDefName, -1);
            }
            // Off-map: check world caravans (1.6: Pawn has no IsWorldPawn —
            // presence in a caravan's PawnsListForReading is the check).
            int tile = WorldCaravanTile(pawn);
            if (tile >= 0)
            {
                return new LocationInfo(LocationKind.Caravan, null, tile);
            }
            return new LocationInfo(LocationKind.None, null, -1);
        }

        /// <summary>
        /// Resolves the live Pawn holding the Thing identified by
        /// "defName:thingIDNumber". Walks the IThingHolder parent chain
        /// (pawn.equipment / pawn.apparel / containers) until a Pawn is found;
        /// null for unheld, destroyed or unresolved things.
        /// </summary>
        public Pawn GetCurrentHolder(string stableId)
        {
            Thing thing = FindLiveThing(stableId);
            if (thing == null)
            {
                return null;
            }
            IThingHolder holder = thing.ParentHolder;
            int depth = 0;
            while (holder != null && depth < 32)
            {
                Pawn pawn = holder as Pawn;
                if (pawn != null)
                {
                    return pawn;
                }
                // Explicit tracker handling: IThingHolder.ParentHolder is the only
                // upward link in 1.6 (no `Parent` property on the interface).
                Pawn_EquipmentTracker equipment = holder as Pawn_EquipmentTracker;
                if (equipment != null && equipment.pawn != null)
                {
                    return equipment.pawn;
                }
                Pawn_ApparelTracker apparel = holder as Pawn_ApparelTracker;
                if (apparel != null && apparel.pawn != null)
                {
                    return apparel.pawn;
                }
                holder = holder.ParentHolder;
                depth++;
            }
            return null;
        }

        /// <summary>
        /// Most recent ongoing battle: any BattleObject whose (runtime) EndTick
        /// is still -1, newest first by the latest associated event tick. Null
        /// when no battle is currently recorded as ongoing.
        /// </summary>
        public BattleObject GetActiveBattle()
        {
            IReadOnlyList<ArchiveObject> battles = GetObjectsOfCategory(ArchiveCategoryKeys.Battle);
            BattleObject best = null;
            long bestTick = long.MinValue;
            for (int i = 0; i < battles.Count; i++)
            {
                BattleObject battle = battles[i] as BattleObject;
                if (battle == null || battle.EndTick != -1L)
                {
                    continue;
                }
                long lastTick = LatestBattleTick(battle);
                if (lastTick > bestTick)
                {
                    bestTick = lastTick;
                    best = battle;
                }
            }
            return best;
        }

        /// <summary>
        /// Semantic filter over GetEventsFor: only Craft/Built events, resolved
        /// through ChronicleEventDef.kind (never TypeKey substring matching).
        /// </summary>
        public IReadOnlyList<ChronicleEvent> GetProductionEvents(string stableId)
        {
            IReadOnlyList<ChronicleEvent> all = GetEventsFor(stableId);
            List<ChronicleEvent> result = new List<ChronicleEvent>();
            for (int i = 0; i < all.Count; i++)
            {
                ChronicleEvent ev = all[i];
                if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
                {
                    continue;
                }
                ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
                if (def != null && (def.kind == ChronicleEventKind.Craft || def.kind == ChronicleEventKind.Built))
                {
                    result.Add(ev);
                }
            }
            return result;
        }

        // ---- live query helpers ----

        /// <summary>World tile of the caravan carrying <paramref name="pawn"/> (-1 if none).</summary>
        private static int WorldCaravanTile(Pawn pawn)
        {
            if (pawn == null || Find.World == null || Find.WorldObjects == null)
            {
                return -1;
            }
            List<Caravan> caravans = Find.WorldObjects.Caravans;
            for (int i = 0; i < caravans.Count; i++)
            {
                Caravan caravan = caravans[i];
                if (caravan == null)
                {
                    continue;
                }
                List<Pawn> members = caravan.PawnsListForReading;
                for (int j = 0; j < members.Count; j++)
                {
                    if (members[j] == pawn)
                    {
                        return caravan.Tile;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// Finds a live, non-destroyed Thing by its stable id "defName:thingIDNumber"
        /// across all loaded maps. Conservative full scan over map.listerThings —
        /// correct and cheap enough for a UI refresh cadence.
        /// </summary>
        private static Thing FindLiveThing(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return null;
            }
            int sep = stableId.IndexOf(':');
            if (sep <= 0 || sep >= stableId.Length - 1)
            {
                return null;
            }
            string defName = stableId.Substring(0, sep);
            if (!int.TryParse(stableId.Substring(sep + 1), out int thingId))
            {
                return null;
            }
            List<Map> maps = Find.Maps;
            if (maps == null)
            {
                return null;
            }
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (map == null || map.listerThings == null)
                {
                    continue;
                }
                List<Thing> all = map.listerThings.AllThings;
                for (int j = 0; j < all.Count; j++)
                {
                    Thing t = all[j];
                    if (t == null || t.Destroyed || t.def == null)
                    {
                        continue;
                    }
                    if (t.def.defName == defName && t.thingIDNumber == thingId)
                    {
                        return t;
                    }
                }
            }
            return null;
        }

        /// <summary>Latest event tick associated with a battle (used to pick the newest ongoing one).</summary>
        private long LatestBattleTick(BattleObject battle)
        {
            if (battle == null || string.IsNullOrEmpty(battle.StableId))
            {
                return -1L;
            }
            IReadOnlyList<ChronicleEvent> events = GetEventsFor(battle.StableId);
            long maxTick = -1L;
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev != null && ev.Tick > maxTick)
                {
                    maxTick = ev.Tick;
                }
            }
            return maxTick;
        }

        public void OnColonistJoined(Pawn pawn)
        {
            // 无显式角色时按活读谓词判定（默认 FreeColonist）。
            PawnRole role = ChronicleColonistScanner.TryClassify(pawn, out PawnRole resolved)
                ? resolved
                : PawnRole.FreeColonist;
            OnColonistJoined(pawn, role);
        }

        /// <summary>
        /// Records a colonist join with an explicit role (free colonist / slave /
        /// prisoner). The capture layer (Patch_SetFaction) pre-classifies so the
        /// archive badge matches the join-time reality. Falls back to FreeColonist
        /// when classification is unavailable.
        /// </summary>
        public void OnColonistJoined(Pawn pawn, PawnRole role)
        {
            if (!IsRecordingEnabled() || pawn == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                string stableId = pawn.GetUniqueLoadID();
                string labelSnapshot = pawn.LabelShort;
                PawnObject record = new PawnObject
                {
                    StableId = stableId,
                    LabelSnapshot = labelSnapshot,
                    LabelShort = labelSnapshot,
                    KindDefName = pawn.kindDef != null ? pawn.kindDef.defName : null,
                    FactionDefName = pawn.Faction != null && pawn.Faction.def != null ? pawn.Faction.def.defName : null,
                    JoinTick = Find.TickManager.TicksGame,
                    DeathTick = -1L,
                    DeathCauseKey = null,
                    Role = role
                };
                PawnArchiveSnapshots.ApplyJoinSnapshots(record, pawn);
                if (!component.AddObject(record))
                {
                    return;
                }
                AddEvent(component, stableId, labelSnapshot, ChronicleEventType.Join);
            }
            catch (System.Exception ex)
            {
                Log.Warning("PersonalChronicle: failed to record colonist join for " + (pawn != null ? pawn.LabelShort : "null") + ": " + ex.Message);
            }
        }

        public void OnPawnDied(Pawn pawn, string deathCauseKey)
        {
            OnPawnDied(pawn, deathCauseKey, null);
        }

        /// <summary>
        /// Records a colonist death. v0.3: <paramref name="extraParams"/> entries
        /// (e.g. ["killer"] = killer pawn LabelShort) are merged into the event's
        /// Params as language-independent identity snapshots — never used for
        /// associations (Primary/Subjects remain the only edge source).
        ///
        /// v3.1 P2: when extraParams contains Killer label, the matching killer
        /// pawn (if resolved by capture) should also be passed via the optional
        /// path in Patch_PawnKill — killer is added as a Pawn Subject edge so
        /// GetEventsFor(killerId) lists this death as a kill.
        /// </summary>
        public void OnPawnDied(Pawn pawn, string deathCauseKey, Thing weapon = null, Dictionary<string, string> extraParams = null)
        {
            OnPawnDied(pawn, deathCauseKey, weapon, extraParams, null);
        }

        /// <summary>
        /// Full death write with optional killer pawn for P2 kill-graph edges.
        /// </summary>
        public void OnPawnDied(Pawn pawn, string deathCauseKey, Thing weapon, Dictionary<string, string> extraParams, Pawn killer)
        {
            if (!IsRecordingEnabled() || pawn == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                string stableId = pawn.GetUniqueLoadID();
                string labelSnapshot = pawn.LabelShort;
                if (!component.ArchivePawn(stableId, deathCauseKey, pawn))
                {
                    return;
                }
                ChronicleEvent ev = BuildPawnEvent(stableId, labelSnapshot, ChronicleEventType.Death);
                if (extraParams != null && extraParams.Count > 0)
                {
                    foreach (KeyValuePair<string, string> pair in extraParams)
                    {
                        ev.Params[pair.Key] = pair.Value;
                    }
                }
                if (killer != null && ChronicleColonistScanner.TryClassifyCurrent(killer, out _))
                {
                    EnsurePawnArchivedForCapture(component, killer);
                }
                AttachCombatSubjects(component, ev, weapon, killer, pawn);
                AddEvent(component, stableId, ev);
            }
            catch (System.Exception ex)
            {
                Log.Warning("PersonalChronicle: failed to record pawn death for " + (pawn != null ? pawn.LabelShort : "null") + ": " + ex.Message);
            }
        }

        /// <summary>
        /// v3.1 P2: kill by a chronicle colonist of a non-archive victim (raider etc.).
        /// </summary>
        /// <summary>
        /// v3.1 P3: significant social relation formed/ended.
        /// </summary>
        public void OnRelationChanged(Pawn a, Pawn b, PawnRelationDef relationDef, bool formed)
        {
            if (!IsRecordingEnabled() || a == null || b == null || relationDef == null)
            {
                return;
            }
            if (!SocialRelationFilter.IsSignificant(relationDef))
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                // Only record when at least one party is a chronicle colonist.
                bool aIs = ChronicleColonistScanner.TryClassifyCurrent(a, out _);
                bool bIs = ChronicleColonistScanner.TryClassifyCurrent(b, out _);
                if (!aIs && !bIs)
                {
                    return;
                }

                // Keep the event primary on a real current-colony pawn. The
                // relation patch can be invoked with the non-current side as
                // argument A (especially during scenario initialization); that
                // side must never become an archive owner by accident.
                if (!aIs && bIs)
                {
                    Pawn swapPawn = a;
                    a = b;
                    b = swapPawn;
                    aIs = true;
                    bIs = false;
                }

                long now = Find.TickManager.TicksGame;
                string aId = a.GetUniqueLoadID();
                string bId = b.GetUniqueLoadID();
                string aLabel = a.LabelShort;
                string bLabel = b.LabelShort;
                string relName = relationDef.defName;
                string action = formed
                    ? ChronicleEventParams.RelationActionFormed
                    : ChronicleEventParams.RelationActionEnded;

                // Snapshot list on archived sides (ensure object exists when party is colony).
                if (aIs)
                {
                    EnsurePawnArchivedForSocial(component, a);
                    UpdateRelationSnapshot(component, aId, bId, bLabel, relName, now, formed);
                }
                if (bIs)
                {
                    EnsurePawnArchivedForSocial(component, b);
                    UpdateRelationSnapshot(component, bId, aId, aLabel, relName, now, formed);
                }

                // One Social event with Primary=a, Subject=b (both get indexed via edges).
                ChronicleEvent ev = BuildPawnEvent(aId, aLabel, ChronicleEventType.Social);
                ev.Params[ChronicleEventParams.Relation] = relName;
                ev.Params[ChronicleEventParams.RelationAction] = action;
                if (!SubjectContains(ev, bId))
                {
                    ev.Subjects.Add(ObjectRef.ForPawn(bId, bLabel));
                }
                // Cap against the primary party's budget.
                AddEvent(component, aId, ev);
            }
            catch (System.Exception ex)
            {
                Log.Warning("PersonalChronicle: failed to record relation change: " + ex.Message);
            }
        }

        private static void EnsurePawnArchivedForSocial(ChronicleGameComponent component, Pawn pawn)
        {
            if (component == null || pawn == null)
            {
                return;
            }
            string id = pawn.GetUniqueLoadID();
            if (component.GetObject(id) != null)
            {
                return;
            }
            // Lightweight ensure: join-style record so Relations list can attach.
            // Prefer scanner role; JoinTick unknown.
            if (!ChronicleColonistScanner.TryClassifyCurrent(pawn, out PawnRole role))
            {
                return;
            }
            PawnObject record = new PawnObject
            {
                StableId = id,
                LabelSnapshot = pawn.LabelShort,
                LabelShort = pawn.LabelShort,
                KindDefName = pawn.kindDef != null ? pawn.kindDef.defName : null,
                FactionDefName = pawn.Faction != null && pawn.Faction.def != null ? pawn.Faction.def.defName : null,
                JoinTick = -1L,
                DeathTick = -1L,
                Role = role
            };
            PawnArchiveSnapshots.ApplyJoinSnapshots(record, pawn);
            component.AddObject(record);
        }

        private static void UpdateRelationSnapshot(
            ChronicleGameComponent component,
            string selfId,
            string otherId,
            string otherLabel,
            string relationDefName,
            long now,
            bool formed)
        {
            PawnObject self = component.GetObject(selfId) as PawnObject;
            if (self == null)
            {
                return;
            }
            if (self.Relations == null)
            {
                self.Relations = new List<SignificantRelation>();
            }
            if (formed)
            {
                // Close any still-active matching pair then append.
                for (int i = 0; i < self.Relations.Count; i++)
                {
                    SignificantRelation r = self.Relations[i];
                    if (r != null && r.IsActive
                        && r.RelationDefName == relationDefName
                        && r.OtherStableId == otherId)
                    {
                        r.EndedTick = now;
                    }
                }
                self.Relations.Add(new SignificantRelation
                {
                    RelationDefName = relationDefName,
                    OtherStableId = otherId,
                    OtherLabel = otherLabel,
                    FormedTick = now,
                    EndedTick = -1L
                });
                // Cap relation history.
                const int maxRel = 48;
                while (self.Relations.Count > maxRel)
                {
                    self.Relations.RemoveAt(0);
                }
            }
            else
            {
                for (int i = self.Relations.Count - 1; i >= 0; i--)
                {
                    SignificantRelation r = self.Relations[i];
                    if (r != null && r.IsActive
                        && r.RelationDefName == relationDefName
                        && r.OtherStableId == otherId)
                    {
                        r.EndedTick = now;
                        break;
                    }
                }
            }
            component.MarkChanged();
        }

        public void OnKillRecorded(Pawn killer, Pawn victim, Thing weapon = null)
        {
            if (!IsRecordingEnabled() || killer == null || victim == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                string victimId = victim.GetUniqueLoadID();
                string killerId = killer.GetUniqueLoadID();
                if (string.IsNullOrEmpty(victimId) || string.IsNullOrEmpty(killerId))
                {
                    return;
                }
                if (!ChronicleColonistScanner.TryClassifyCurrent(killer, out _))
                {
                    return;
                }
                EnsurePawnArchivedForCapture(component, killer);
                if (HasRecordedExternalKill(component, killerId, victimId))
                {
                    return;
                }
                string victimLabel = victim.LabelShort;
                ChronicleEvent ev = BuildPawnEvent(victimId, victimLabel, ChronicleEventType.Death);
                ev.Params[ChronicleEventParams.Killer] = killer.LabelShort;
                ev.Params[ChronicleEventParams.Victim] = victimLabel;
                ev.Params[ChronicleEventParams.VictimStableId] = victimId;
                ev.Params[ChronicleEventParams.CombatRole] = ChronicleEventParams.CombatRoleKill;
                AttachCombatSubjects(component, ev, weapon, killer, victim);
                // Cap against the killer's event budget (their combat log).
                AddEvent(component, killerId, ev);
            }
            catch (System.Exception ex)
            {
                Log.Warning("PersonalChronicle: failed to record kill by " + (killer != null ? killer.LabelShort : "null") + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Shared death/kill edge wiring: weapon Subject, killer Subject, battle
        /// Subject + ParticipantIds, weapon holder history.
        /// </summary>
        private void AttachCombatSubjects(ChronicleGameComponent component, ChronicleEvent ev, Thing weapon, Pawn killer, Pawn victim)
        {
            if (ev == null)
            {
                return;
            }
            if (ev.Subjects == null)
            {
                ev.Subjects = new List<ObjectRef>();
            }
            if (weapon != null && weapon.def != null)
            {
                string weaponId = weapon.def.defName + ":" + weapon.thingIDNumber;
                RegisterThingObject(component, weapon, killer);
                ev.Subjects.Add(new ObjectRef(ArchiveCategoryKeys.Thing, weaponId, null));
                NoteWeaponHolder(component, weaponId, killer);
            }
            if (killer != null)
            {
                string killerId = killer.GetUniqueLoadID();
                if (!SubjectContains(ev, killerId))
                {
                    ev.Subjects.Add(ObjectRef.ForPawn(killerId, killer.LabelShort));
                }
                if (ev.Params != null && !ev.Params.ContainsKey(ChronicleEventParams.Killer))
                {
                    ev.Params[ChronicleEventParams.Killer] = killer.LabelShort;
                }
            }
            BattleObject activeBattle = GetActiveBattle();
            if (activeBattle != null && !string.IsNullOrEmpty(activeBattle.StableId))
            {
                if (!SubjectContains(ev, activeBattle.StableId))
                {
                    ev.Subjects.Add(new ObjectRef(ArchiveCategoryKeys.Battle, activeBattle.StableId, null));
                }
                AddBattleParticipant(activeBattle, victim);
                AddBattleParticipant(activeBattle, killer);
            }
        }

        private static bool SubjectContains(ChronicleEvent ev, string stableId)
        {
            if (ev == null || ev.Subjects == null || string.IsNullOrEmpty(stableId))
            {
                return false;
            }
            for (int i = 0; i < ev.Subjects.Count; i++)
            {
                ObjectRef s = ev.Subjects[i];
                if (s != null && s.StableId == stableId)
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddBattleParticipant(BattleObject battle, Pawn pawn)
        {
            if (battle == null || pawn == null)
            {
                return;
            }
            if (battle.ParticipantIds == null)
            {
                battle.ParticipantIds = new List<string>();
            }
            string id = pawn.GetUniqueLoadID();
            if (string.IsNullOrEmpty(id) || battle.ParticipantIds.Contains(id))
            {
                return;
            }
            battle.ParticipantIds.Add(id);
        }

        private static void NoteWeaponHolder(ChronicleGameComponent component, string weaponStableId, Pawn holder)
        {
            if (component == null || string.IsNullOrEmpty(weaponStableId) || holder == null)
            {
                return;
            }
            ThingObject thing = component.GetObject(weaponStableId) as ThingObject;
            if (thing == null)
            {
                return;
            }
            string holderId = holder.GetUniqueLoadID();
            thing.CurrentHolderId = holderId;
            if (thing.HolderHistory == null)
            {
                thing.HolderHistory = new List<ObjectRef>();
            }
            // Append only when holder changed (avoid spam on multi-kill same holder).
            if (thing.HolderHistory.Count > 0)
            {
                ObjectRef last = thing.HolderHistory[thing.HolderHistory.Count - 1];
                if (last != null && last.StableId == holderId)
                {
                    return;
                }
            }
            thing.HolderHistory.Add(ObjectRef.ForPawn(holderId, holder.LabelShort));
            component.MarkChanged();
        }

        public void OnThingCrafted(Thing product, Pawn worker)
        {
            if (!IsRecordingEnabled() || product == null || product.def == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                bool workerIsCurrent = worker != null
                    && ChronicleColonistScanner.TryClassifyCurrent(worker, out _);
                if (workerIsCurrent)
                {
                    int quantity = Math.Max(1, product.stackCount);
                    float unitValue = product.MarketValue;
                    if (float.IsNaN(unitValue) || float.IsInfinity(unitValue) || unitValue < 0f)
                    {
                        unitValue = 0f;
                    }
                    EnsurePawnArchivedForCapture(component, worker);
                    component.AddProduction(
                        worker.GetUniqueLoadID(),
                        product.def.defName,
                        quantity,
                        unitValue * quantity,
                        Find.TickManager.TicksGame);
                }
                string stableId = product.def.defName + ":" + product.thingIDNumber;
                RegisterThingObject(component, product, worker);
                ChronicleEvent ev = BuildThingEvent(stableId, ChronicleEventType.Crafted);
                if (workerIsCurrent)
                {
                    AddPawnSubject(ev, worker);
                }
                string eventOwnerId = workerIsCurrent
                    ? worker.GetUniqueLoadID()
                    : stableId;
                AddEvent(component, eventOwnerId, ev);
            }
            catch (System.Exception ex)
            {
                Log.Warning("PersonalChronicle: failed to record crafted thing " + (product != null && product.def != null ? product.def.defName : "null") + ": " + ex.Message);
            }
        }

        public void OnThingBuilt(ThingDef builtDef, string builtStableId, Pawn worker)
        {
            if (!IsRecordingEnabled() || builtDef == null || string.IsNullOrEmpty(builtStableId))
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                if (component.GetObject(builtStableId) == null)
                {
                    component.AddObject(new ThingObject
                    {
                        StableId = builtStableId,
                        ThingDefName = builtDef.defName,
                        WeakId = builtStableId
                    });
                }
                bool workerIsCurrent = worker != null
                    && ChronicleColonistScanner.TryClassifyCurrent(worker, out _);
                if (workerIsCurrent)
                {
                    EnsurePawnArchivedForCapture(component, worker);
                    component.AddProduction(
                        worker.GetUniqueLoadID(),
                        builtDef.defName,
                        1,
                        builtDef.BaseMarketValue,
                        Find.TickManager.TicksGame);
                }
                ChronicleEvent ev = BuildThingEvent(builtStableId, ChronicleEventType.Built);
                if (workerIsCurrent)
                {
                    AddPawnSubject(ev, worker);
                }
                string eventOwnerId = workerIsCurrent
                    ? worker.GetUniqueLoadID()
                    : builtStableId;
                AddEvent(component, eventOwnerId, ev);
            }
            catch (System.Exception ex)
            {
                Log.Warning("PersonalChronicle: failed to record built thing " + (builtDef != null ? builtDef.defName : "null") + ": " + ex.Message);
            }
        }

        public void OnBattleStarted(IncidentDef incidentDef)
        {
            if (!IsRecordingEnabled() || incidentDef == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                // Battle identity: incident defName + tick is unique within a save.
                string stableId = incidentDef.defName + "@" + Find.TickManager.TicksGame;
                if (component.GetObject(stableId) == null)
                {
                    // A new battle supersedes the previous ongoing one: mark the
                    // old battle ended (its EndTick is otherwise never set, which
                    // would make GetActiveBattle() report it as ongoing forever).
                    ClosePreviousBattle(component, stableId);
                    component.AddObject(new BattleObject
                    {
                        StableId = stableId,
                        IncidentDefName = incidentDef.defName
                    });
                }
                BattleObject battle = component.GetObject(stableId) as BattleObject;
                ChronicleEvent ev = new ChronicleEvent
                {
                    Tick = Find.TickManager.TicksGame,
                    TypeKey = ChronicleEventType.Battle,
                    Primary = new ObjectRef(ArchiveCategoryKeys.Battle, stableId, null),
                    Subjects = new List<ObjectRef>(),
                    Params = new Dictionary<string, string>()
                };
                // P2: snapshot current colony people as participants + Subject edges
                // so GetEventsFor(pawn) returns this battle for every fighter present.
                AttachBattleRoster(component, battle, ev);
                AddEvent(component, stableId, ev);
            }
            catch (System.Exception ex)
            {
                Log.Warning("PersonalChronicle: failed to record battle start " + (incidentDef != null ? incidentDef.defName : "null") + ": " + ex.Message);
            }
        }

        /// <summary>
        /// v3.1 P2: at battle start, attach every live chronicle person as a
        /// Subject edge + ParticipantIds entry (map + caravan roster).
        /// </summary>
        private static void AttachBattleRoster(ChronicleGameComponent component, BattleObject battle, ChronicleEvent ev)
        {
            if (ev == null)
            {
                return;
            }
            List<ColonyMember> people = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < people.Count; i++)
            {
                ColonyMember m = people[i];
                if (m == null || m.Pawn == null || m.Pawn.Dead)
                {
                    continue;
                }
                Pawn p = m.Pawn;
                string id = p.GetUniqueLoadID();
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                if (battle != null)
                {
                    AddBattleParticipant(battle, p);
                }
                if (!SubjectContains(ev, id))
                {
                    ev.Subjects.Add(ObjectRef.ForPawn(id, p.LabelShort));
                }
            }
        }

        private void AddEvent(ChronicleGameComponent component, string stableId, string labelSnapshot, string typeKey)
        {
            ChronicleEvent ev = BuildPawnEvent(stableId, labelSnapshot, typeKey);
            AddEvent(component, stableId, ev);
        }

        /// <summary>
        /// Marks every currently-ongoing BattleObject (EndTick still -1) as
        /// ended at the current tick, excluding the newly-started one. Without
        /// this, EndTick is never assigned (there is no battle-end capture point
        /// in vanilla), so GetActiveBattle() would report the most recent battle
        /// as ongoing forever and LC-2 would link every death to it.
        /// Event-driven: a new battle start IS the previous battle's end.
        /// </summary>
        private static void ClosePreviousBattle(ChronicleGameComponent component, string newBattleStableId)
        {
            if (component == null)
            {
                return;
            }
            long now = Find.TickManager.TicksGame;
            for (int i = 0; i < component.Objects.Count; i++)
            {
                BattleObject battle = component.Objects[i] as BattleObject;
                if (battle == null
                    || battle.EndTick != -1L
                    || battle.StableId == newBattleStableId)
                {
                    continue;
                }
                battle.EndTick = now;
            }
        }

        private void AddEvent(ChronicleGameComponent component, string stableId, ChronicleEvent ev)
        {
            int maxEvents = PersonalChronicleMod.Settings != null ? PersonalChronicleMod.Settings.MaxEventsPerPawn : 200;
            if (maxEvents <= 0)
            {
                return;
            }
            if (ev == null)
            {
                return;
            }
            ev.ImportanceLevel = (int)ChronicleEventImportance.Resolve(ev);
            int retentionLimit = ev.ImportanceLevel <= (int)ChronicleImportance.Routine
                ? Math.Max(24, maxEvents / 3)
                : maxEvents;
            if (component.GetEventsFor(stableId).Count >= retentionLimit)
            {
                return;
            }
            component.AddEvent(ev);
        }

        private static ChronicleEvent BuildPawnEvent(string stableId, string labelSnapshot, string typeKey)
        {
            return new ChronicleEvent
            {
                Tick = Find.TickManager.TicksGame,
                TypeKey = typeKey,
                Primary = ObjectRef.ForPawn(stableId, labelSnapshot),
                Subjects = new List<ObjectRef>(),
                Params = new Dictionary<string, string>()
            };
        }

        private static ChronicleEvent BuildThingEvent(string stableId, string typeKey)
        {
            return new ChronicleEvent
            {
                Tick = Find.TickManager.TicksGame,
                TypeKey = typeKey,
                Primary = new ObjectRef(ArchiveCategoryKeys.Thing, stableId, null),
                Subjects = new List<ObjectRef>(),
                Params = new Dictionary<string, string>()
            };
        }

        /// <summary>
        /// Adds a pawn Subject edge when the worker is a chronicle-relevant pawn.
        /// The worker is a data edge (StableId + label snapshot); its archive
        /// record may or may not exist yet — GetEventsFor still works because the
        /// eventsByObject index registers the edge regardless.
        /// </summary>
        private static void AddPawnSubject(ChronicleEvent ev, Pawn worker)
        {
            if (ev == null || worker == null)
            {
                return;
            }
            ev.Subjects.Add(ObjectRef.ForPawn(worker.GetUniqueLoadID(), worker.LabelShort));
        }

        /// <summary>
        /// Registers (or reuses) a ThingObject for a crafted thing / weapon.
        /// StableId is defName:thingIDNumber — valid within the loaded session,
        /// consistent with ThingObject.WeakId semantics (historical snapshot).
        /// </summary>
        private static void RegisterThingObject(ChronicleGameComponent component, Thing thing, Pawn holder = null)
        {
            if (component == null || thing == null || thing.def == null)
            {
                return;
            }
            string stableId = thing.def.defName + ":" + thing.thingIDNumber;
            if (component.GetObject(stableId) == null)
            {
                component.AddObject(new ThingObject
                {
                    StableId = stableId,
                    ThingDefName = thing.def.defName,
                    WeakId = stableId
                });
            }
            if (holder != null)
            {
                NoteWeaponHolder(component, stableId, holder);
            }
        }

        private static bool IsRecordingEnabled()
        {
            return PersonalChronicleMod.Settings == null || PersonalChronicleMod.Settings.EnableRecording;
        }

        /// <summary>
        /// Ensures a live chronicle pawn has an archive object before an event
        /// references it as a killer/worker. This closes the race where a pawn
        /// acts before SetFaction or the reconcile confirmation window has
        /// completed. It never creates a record for a non-chronicle pawn.
        /// </summary>
        private static PawnObject EnsurePawnArchivedForCapture(
            ChronicleGameComponent component,
            Pawn pawn)
        {
            if (component == null || pawn == null)
            {
                return null;
            }
            string id = pawn.GetUniqueLoadID();
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            PawnObject existing = component.GetObject(id) as PawnObject;
            if (existing != null)
            {
                return existing;
            }
            if (!ChronicleColonistScanner.TryClassifyCurrent(pawn, out PawnRole role))
            {
                return null;
            }
            PawnObject record = new PawnObject
            {
                StableId = id,
                LabelSnapshot = pawn.LabelShort,
                LabelShort = pawn.LabelShort,
                KindDefName = pawn.kindDef != null ? pawn.kindDef.defName : null,
                FactionDefName = pawn.Faction != null && pawn.Faction.def != null ? pawn.Faction.def.defName : null,
                JoinTick = -1L,
                DeathTick = -1L,
                Role = role
            };
            PawnArchiveSnapshots.ApplyJoinSnapshots(record, pawn);
            component.AddObject(record);
            return record;
        }

        private static bool HasRecordedExternalKill(
            ChronicleGameComponent component,
            string killerId,
            string victimId)
        {
            if (component == null || string.IsNullOrEmpty(killerId) || string.IsNullOrEmpty(victimId))
            {
                return false;
            }
            IReadOnlyList<ChronicleEvent> events = component.GetEventsFor(killerId);
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev == null || ev.TypeKey != ChronicleEventType.Death || ev.Params == null)
                {
                    continue;
                }
                string role;
                string recordedVictimId;
                if (ev.Params.TryGetValue(ChronicleEventParams.CombatRole, out role)
                    && role == ChronicleEventParams.CombatRoleKill
                    && ev.Params.TryGetValue(ChronicleEventParams.VictimStableId, out recordedVictimId)
                    && recordedVictimId == victimId)
                {
                    return true;
                }
            }
            return false;
        }

        // ---- v2.4 live stats ----

        /// <summary>
        /// Current (live-read) colony population count — free colonists, slaves
        /// and prisoners combined. Two-source merge (maps + caravans) via
        /// ChronicleColonistScanner.EnumerateCurrentPeople (single predicate
        /// source of truth), cached for <see cref="LiveCountCacheWindow"/> ticks.
        /// The per-role breakdown is cached alongside (see GetLiveColonistCounts).
        /// Game switch (new game / save load) resets the cache. Defensive: 0 when
        /// no game or component is active. Never triggers reconciliation — the
        /// write path is owned by ChronicleGameComponent's internal tick hook.
        /// </summary>
        public int GetLiveColonistCount()
        {
            if (Current.Game == null)
            {
                return 0;
            }
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return 0;
            }
            // Game switch (new game / save load): cached count belongs to a
            // dead session → reset and force a fresh scan on first read.
            if (!ReferenceEquals(liveCountCacheComponent, component))
            {
                liveCountCacheComponent = component;
                liveCountCacheTick = -1L;
            }
            long tick = GenTicks.TicksGame;
            if (liveCountCacheTick >= 0L && tick - liveCountCacheTick <= LiveCountCacheWindow)
            {
                return cachedLiveColonistCount;
            }
            RefreshLiveCount();
            return cachedLiveColonistCount;
        }

        /// <summary>
        /// Current (live-read) colony population broken down by role: free
        /// colonists / slaves / prisoners. Shares the same 600-tick cache as
        /// <see cref="GetLiveColonistCount"/>, so calling this never triggers a
        /// second scan within the window. UI renders the per-role split on the
        /// home "current colonists" stat cell.
        /// </summary>
        public void GetLiveColonistCounts(out int free, out int slave, out int prisoner)
        {
            // Refresh (or hit cache) so out-params are always fresh on first read.
            GetLiveColonistCount();
            free = cachedFreeColonistCount;
            slave = cachedSlaveCount;
            prisoner = cachedPrisonerCount;
        }

        /// <summary>
        /// Single live scan: populates cachedLiveColonistCount plus the per-role
        /// breakdown from one EnumerateCurrentPeople() pass (no double scan).
        /// </summary>
        private void RefreshLiveCount()
        {
            List<ColonyMember> people = ChronicleColonistScanner.EnumerateCurrentPeople();
            cachedLiveColonistCount = people.Count;
            cachedFreeColonistCount = 0;
            cachedSlaveCount = 0;
            cachedPrisonerCount = 0;
            for (int i = 0; i < people.Count; i++)
            {
                switch (people[i].Role)
                {
                    case PawnRole.FreeColonist:
                        cachedFreeColonistCount++;
                        break;
                    case PawnRole.Slave:
                        cachedSlaveCount++;
                        break;
                    case PawnRole.Prisoner:
                        cachedPrisonerCount++;
                        break;
                }
            }
            liveCountCacheTick = GenTicks.TicksGame;

            // 诊断：开启 mod 设置 DebugLiveCount 时，把活读人口逐项明细打到日志，
            // 用于排查"当前人口数与可见殖民者不符"。默认关闭，无性能/噪音影响。
            if (PersonalChronicleMod.Settings != null && PersonalChronicleMod.Settings.DebugLiveCount)
            {
                Log.Message(ChronicleColonistScanner.DumpLivePopulation());
            }
        }

        /// <summary>
        /// Archive-snapshot convention: active = DeathTick == -1. Counts every
        /// archived PawnObject of the Pawn category (single shared scan with
        /// <see cref="GetArchivedSnapshotCount"/>).
        /// </summary>
        public int GetActiveSnapshotCount()
        {
            CountPawnSnapshots(out int active, out int archived);
            return active;
        }

        /// <summary>
        /// Archive-snapshot convention: archived = DeathTick &gt; 0. Counts every
        /// archived PawnObject of the Pawn category (single shared scan with
        /// <see cref="GetActiveSnapshotCount"/>).
        /// </summary>
        public int GetArchivedSnapshotCount()
        {
            CountPawnSnapshots(out int active, out int archived);
            return archived;
        }

        /// <summary>
        /// One traversal over the archived Pawn category computes both counts
        /// (never two scans). archived = DeathTick &gt; 0 (matches
        /// PawnRecord.IsArchived); everything else counts as active.
        /// </summary>
        private static void CountPawnSnapshots(out int active, out int archived)
        {
            active = 0;
            archived = 0;
            if (Current.Game == null)
            {
                return;
            }
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return;
            }
            IReadOnlyList<ArchiveObject> pawns = component.GetObjectsOfCategory(ArchiveCategoryKeys.Pawn);
            for (int i = 0; i < pawns.Count; i++)
            {
                if (!(pawns[i] is PawnObject pawn))
                {
                    continue;
                }
                if (pawn.DeathTick > 0L)
                {
                    archived++;
                }
                else
                {
                    active++;
                }
            }
        }
    }
}

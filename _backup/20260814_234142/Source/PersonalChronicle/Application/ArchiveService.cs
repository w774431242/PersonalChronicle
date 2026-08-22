using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Api;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

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
    ///
    /// v4.1: also implements <see cref="IArchiveQueryService"/> (inherited by
    /// IArchiveService) and <see cref="IArchiveEventSink"/> so integrators can
    /// depend on the narrower read/write contracts via the unified API facade.
    /// </summary>
    public sealed class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
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
        /// v4.11 P0: raid Lord loadID → active BattleObject.StableId. Built when a
        /// battle starts (LinkRaidLords) and consumed by the Lord.Notify_PawnLost
        /// capture patch. Keyed by the Lord's int loadID so the patch needs no field
        /// reflection on the Lord instance. Rebuilt lazily: entries are removed once
        /// a battle is finalized; a stale entry for an unloaded Lord is harmless
        /// (its pawn-left callback simply won't fire again).
        /// </summary>
        private static readonly Dictionary<int, string> raidLordToBattle = new Dictionary<int, string>();

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
            IReadOnlyList<PawnRecord> all = GetAllRecords();
            List<PawnRecord> result = new List<PawnRecord>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i].IsArchived)
                {
                    result.Add(all[i]);
                }
            }
            return result;
        }

        public IReadOnlyList<PawnRecord> GetArchivedRecords()
        {
            IReadOnlyList<PawnRecord> all = GetAllRecords();
            List<PawnRecord> result = new List<PawnRecord>(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].IsArchived)
                {
                    result.Add(all[i]);
                }
            }
            return result;
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
        /// v5.x "在册"判定：存活 且 属于当前殖民地人口 = 在册。
        ///
        /// 判定不依赖 DeathTick（那是"归档"语义，≠ 在册）。一个存档中已有快照
        /// 但尚未死亡的殖民者，只要还活在本殖民地就应显示"在册"；死亡归档或已
        /// 离开殖民地（被放逐/卖掉/转派系）则不在册。
        ///
        /// 实现：GetLivePawn 已驱逐 dead/destroyed pawn → 能解析到即存活；
        /// TryClassifyCurrent 判定"当前殖民地人口"（地图 spawned 成员 + 商队成员）
        /// → 两条件 AND 即"存活且属于殖民地"。
        /// </summary>
        public bool IsCurrentlyEnlisted(string stableId)
        {
            if (string.IsNullOrEmpty(stableId) || Current.Game == null)
            {
                return false;
            }
            Pawn live = GetLivePawn(stableId);
            return live != null && ChronicleColonistScanner.TryClassifyCurrent(live, out _);
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

        public IReadOnlyList<ChronicleEvent> GetAllEvents()
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return new List<ChronicleEvent>();
            }
            return component.GetAllEvents();
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
            // v4.15 condense-tab: rank this pawn's accumulated hours among all
            // current colony members (for the "全殖民地前几" digest cell).
            int rank = 0;
            int population = 0;
            ComputeColonyWorkRank(totalTicks, out rank, out population);
            return new WorkIntensityView(evaluation, tier, rank, population);
        }

        /// <summary>
        /// v4.15: ranks <paramref name="ownTicks"/> against every current colony
        /// member's accumulated work ticks. <paramref name="rank"/> is 1-based
        /// (1 = most hours); <paramref name="population"/> is the total member count.
        /// </summary>
        private void ComputeColonyWorkRank(long ownTicks, out int rank, out int population)
        {
            rank = 0;
            population = 0;
            List<ColonyMember> people = ChronicleColonistScanner.EnumerateCurrentPeople();
            if (people == null) return;
            population = people.Count;
            int better = 0;
            for (int i = 0; i < people.Count; i++)
            {
                ColonyMember member = people[i];
                if (member == null || member.Pawn == null) continue;
                PawnObject other = GetObject(member.Pawn.GetUniqueLoadID()) as PawnObject;
                if (other == null || other.WorkTime == null) continue;
                long otherTicks = GetTotalWorkTicks(other.WorkTime);
                if (otherTicks > ownTicks) better++;
            }
            rank = better + 1;
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
                        ChronicleLog.Warning(ChronicleLog.Category.Provider, "work-intensity provider failed once, fallback to next provider: "
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
            // v4.6.5: aggregate by ThingCategory (e.g. "Weapons") instead of the
            // concrete item (e.g. "Bow_Wood"). The overview shows extraction by
            // broad category, not individual item defs.
            List<ProductionTypeView> categoryRows = AggregateProductionByCategory(rows);
            categoryRows.Sort((a, b) => b.MarketValue.CompareTo(a.MarketValue));
            rows = categoryRows;
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
        /// v1.1.4 损耗宫格：返回该人物消耗品计价累计。直接消费
        /// <see cref="ConsumptionAccumulator"/> 持久化数据（不扫事件流）。
        /// 周损耗 = 近 7 天按天桶求和；日均 = 近 30 天按天桶求和 / 30。
        /// </summary>
        public ConsumptionSummaryView GetConsumptionSummary(string stableId)
        {
            PawnObject pawn = GetObject(stableId) as PawnObject;
            if (pawn == null || pawn.Consumption == null)
            {
                return new ConsumptionSummaryView(0f, 0f, 0f, new List<ConsumptionTypeView>());
            }
            ConsumptionAccumulator acc = pawn.Consumption;
            List<ConsumptionTypeView> rows = new List<ConsumptionTypeView>();
            if (acc.SilverByCategory != null)
            {
                foreach (KeyValuePair<string, float> pair in acc.SilverByCategory)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0f)
                    {
                        continue;
                    }
                    rows.Add(new ConsumptionTypeView(pair.Key, pair.Value));
                }
            }
            rows.Sort((a, b) => b.Silver.CompareTo(a.Silver));
            long now = Find.TickManager.TicksGame;
            float weekly = acc.SilverSince(now - 7L * RimWorld.GenDate.TicksPerDay);
            float monthly = acc.SilverSince(now - 30L * RimWorld.GenDate.TicksPerDay);
            float daily = monthly / 30f;
            return new ConsumptionSummaryView(
                acc.TotalSilver,
                weekly,
                daily,
                rows);
        }

        /// <summary>
        /// v4.6.5: collapses per-item production rows into per-category rows using
        /// each item's top-level ThingCategory (e.g. "Bow_Wood" -> "Weapons"). Items
        /// with no category definition fall back to their own defName.
        /// </summary>
        private static List<ProductionTypeView> AggregateProductionByCategory(
            IReadOnlyList<ProductionTypeView> byDef)
        {
            if (byDef == null || byDef.Count == 0)
            {
                return new List<ProductionTypeView>();
            }
            Dictionary<string, ProductionTypeView> grouped = new Dictionary<string, ProductionTypeView>();
            for (int i = 0; i < byDef.Count; i++)
            {
                ProductionTypeView row = byDef[i];
                if (row == null)
                {
                    continue;
                }
                string key = ResolveProductionCategoryDefName(row.DefName);
                if (string.IsNullOrEmpty(key))
                {
                    key = row.DefName;
                }
                ProductionTypeView existing;
                if (grouped.TryGetValue(key, out existing))
                {
                    grouped[key] = new ProductionTypeView(
                        key, existing.Quantity + row.Quantity, existing.MarketValue + row.MarketValue);
                }
                else
                {
                    grouped[key] = new ProductionTypeView(key, row.Quantity, row.MarketValue);
                }
            }
            return new List<ProductionTypeView>(grouped.Values);
        }

        /// <summary>
        /// Returns the top-level ThingCategory defName for an item defName
        /// (the category whose parent is null), or null when no category applies.
        /// </summary>
        private static string ResolveProductionCategoryDefName(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || def.thingCategories == null || def.thingCategories.Count == 0)
            {
                return null;
            }
            // Prefer the top-level category (no parent) so e.g. "Bow_Wood" groups
            // under "Weapons" rather than a leaf sub-category.
            for (int i = 0; i < def.thingCategories.Count; i++)
            {
                ThingCategoryDef cat = def.thingCategories[i];
                if (cat != null && cat.parent == null)
                {
                    return cat.defName;
                }
            }
            return def.thingCategories[0].defName;
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
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record colonist join for " + (pawn != null ? pawn.LabelShort : "null") + ": " + ex.Message);
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
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record pawn death for " + (pawn != null ? pawn.LabelShort : "null") + ": " + ex.Message);
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
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record relation change: " + ex.Message);
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
            // Prefer scanner role; JoinTick 走统一默认决策（新档=开局0 / 读档=当天起点）
            // ——绝不可硬编码 -1L，否则开局殖民者被社交事件先建档后永久定格为"中途加入"。
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
                JoinTick = component.ResolveDefaultJoinTick(),
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

        public void OnKillRecorded(Pawn killer, Pawn victim, Thing weapon = null, List<Pawn> assistLookup = null)
        {
            // killer may be null when the DamageInfo instigator is unresolvable
            // (melee-forwarded / environment kills). We still record the kill so the
            // combat log is never empty; it attributes to an "unknown killer" bucket.
            if (!IsRecordingEnabled() || victim == null)
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
                // killer 可能为 null（环境致死 / 近战转发 / 敌方互相残杀且凶手无法解析），
                // 属于合法的「未知凶手」路径，绝不能因为 TryClassifyCurrent(null) 而 NRE 并 return。
                string killerId = ChronicleEventParams.UnknownKillerId;
                if (killer != null)
                {
                    killerId = killer.GetUniqueLoadID();
                    if (!ChronicleColonistScanner.TryClassifyCurrent(killer, out _))
                    {
                        // 非本殖民地人口（敌方/野生动物/奴隶等）→ 归入 UnknownKiller 聚合桶
                        killerId = ChronicleEventParams.UnknownKillerId;
                    }
                }
                if (string.IsNullOrEmpty(victimId) || string.IsNullOrEmpty(killerId))
                {
                    return;
                }
                // 跨 bucket 幂等：同一受害者（victimStableId）若已存在任意击杀记录
                // （无论当时记在哪个 killer 桶），不再重复写入，避免极端场景下
                // instigator==null 同时命中 OnPawnDied 与 OnKillRecorded 产生双份 Death。
                if (HasRecordedDeathForVictim(component, victimId))
                {
                    return;
                }
                // 协助者：造成最多伤害、但非补刀者的 chronicle 殖民者（如 A 削 80% 血、B 抢补刀）。
                Pawn assist = assistLookup != null && assistLookup.Count > 0 ? assistLookup[0] : null;
                if (assist != null && killer != null && assist.GetUniqueLoadID() == killer.GetUniqueLoadID())
                {
                    // 主伤害者就是补刀者 → 取次高伤害者作协助
                    assist = assistLookup != null && assistLookup.Count > 1 ? assistLookup[1] : null;
                }
                if (assist != null && killer == null)
                {
                    // 凶手未知时，主伤害者即升格为击杀者，不再单列协助
                    killerId = assist.GetUniqueLoadID();
                    killer = assist;
                    assist = null;
                }
                EnsurePawnArchivedForCapture(component, killer);
                if (HasRecordedExternalKill(component, killerId, victimId))
                {
                    return;
                }
                string victimLabel = victim.LabelShort;
                ChronicleEvent ev = BuildPawnEvent(victimId, victimLabel, ChronicleEventType.Death);
                ev.Params[ChronicleEventParams.Killer] = killer != null ? killer.LabelShort : ChronicleEventParams.UnknownKillerLabel;
                ev.Params[ChronicleEventParams.Victim] = victimLabel;
                ev.Params[ChronicleEventParams.VictimStableId] = victimId;
                ev.Params[ChronicleEventParams.CombatRole] = ChronicleEventParams.CombatRoleKill;

                // v4.3: snapshot victim faction/kind/category for faction-codex aggregation.
                // External victims are never archived, so this is the only point these are available.
                string victimFactionDef = victim.Faction != null && victim.Faction.def != null
                    ? victim.Faction.def.defName
                    : null;
                ev.Params[ChronicleEventParams.VictimFactionDefName] = victimFactionDef;
                ev.Params[ChronicleEventParams.VictimFactionLabel] = victim.Faction != null ? victim.Faction.Name : null;
                ev.Params[ChronicleEventParams.VictimKindDefName] = victim.kindDef != null ? victim.kindDef.defName : null;
                string victimCategory = ChronicleEventParams.VictimCategoryHumanlike;
                if (victim.RaceProps != null && victim.RaceProps.IsMechanoid)
                {
                    victimCategory = ChronicleEventParams.VictimCategoryMechanoid;
                }
                else if (victim.RaceProps != null && victim.RaceProps.Animal)
                {
                    victimCategory = ChronicleEventParams.VictimCategoryAnimal;
                }
                ev.Params[ChronicleEventParams.VictimCategory] = victimCategory;

                if (assist != null)
                {
                    ev.Params[ChronicleEventParams.Assist] = assist.LabelShort;
                    EnsurePawnArchivedForCapture(component, assist);
                }
                AttachCombatSubjects(component, ev, weapon, killer, victim);
                // Cap against the killer's event budget (their combat log).
                AddEvent(component, killerId, ev);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record kill by " + (killer != null ? killer.LabelShort : "null") + ": " + ex.Message);
            }
        }

        /// <summary>
        /// v6.8 重载：在基础击杀记录之上，累加个人战斗维度到凶手 PawnObject。
        /// 仅当 killer 可解析且为本殖民地人口（落入 UnknownKiller 桶的路径不累加）。
        /// 伤害累加采用补刀 DamageInfo.Amount 近似（Pawn.TakeDamage 账本已在基础路径消费，
        /// 此处补刀伤害作为该次击杀的生涯伤害增量，足够支撑"生涯伤害"展示维度）。
        /// </summary>
        public void OnKillRecorded(Pawn killer, Pawn victim, Thing weapon, List<Pawn> assistLookup, float finishingDamage, bool isMelee)
        {
            // 先走基础击杀记录（内部已做幂等 / 凶手解析 / 未知凶手桶判断）。
            OnKillRecorded(killer, victim, weapon, assistLookup);
            if (killer == null)
            {
                return;
            }
            if (!ChronicleColonistScanner.TryClassifyCurrent(killer, out _))
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
                string killerId = killer.GetUniqueLoadID();
                if (string.IsNullOrEmpty(killerId))
                {
                    return;
                }
                PawnObject record = component.GetObject(killerId) as PawnObject;
                if (record == null)
                {
                    return;
                }
                if (finishingDamage > 0f)
                {
                    record.DamageDealtTotal += finishingDamage;
                }
                if (isMelee)
                {
                    record.MeleeKills++;
                }
                else
                {
                    record.RangedKills++;
                }
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to accumulate combat dims for " + killer.LabelShort + ": " + ex.Message);
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

        /// <summary>
        /// v4.14: maps an IncidentDef.category to a stable data key for
        /// <see cref="BattleObject.ThreatKey"/> — "ThreatBig"/"ThreatSmall"/null.
        /// Data-driven (compares against the IncidentCategoryDefOf constants),
        /// never defName string matching. Null for non-threat or custom battles.
        /// </summary>
        private static string BattleThreatKey(IncidentDef incidentDef)
        {
            if (incidentDef == null || incidentDef.category == null)
            {
                return null;
            }
            if (incidentDef.category == IncidentCategoryDefOf.ThreatBig)
            {
                return "ThreatBig";
            }
            if (incidentDef.category == IncidentCategoryDefOf.ThreatSmall)
            {
                return "ThreatSmall";
            }
            return null;
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
            if (thing.HolderRecords == null)
            {
                thing.HolderRecords = new List<HolderRecord>();
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
            // Legacy chain (传承): ownership transfer record. Capture cannot
            // reliably distinguish a true ownership transfer from a borrow/lend
            // (RimWorld pawns carry equipment without a loan flag), so every
            // observed hold is recorded as "own" — context-rich loans are an
            // authoring concern of the UI, not of the capture layer. The first
            // record (craft holder) is marked IsFirst.
            bool isFirst = thing.HolderRecords.Count == 0;
            thing.HolderRecords.Add(new HolderRecord(
                holderId,
                holder.LabelShort,
                Find.TickManager.TicksGame,
                isFirst,
                HolderRecord.HolderKindOwn));
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
                // v4.6.5: only equipment (weapons + apparel) enters the archive
                // object graph and gets a Crafted event; raw materials / food
                // stay as pure production stats above.
                if (IsEquipable(product))
                {
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
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record crafted thing " + (product != null && product.def != null ? product.def.defName : "null") + ": " + ex.Message);
            }
        }

        /// <summary>
        /// v1.1.4 损耗宫格：记录该人物消耗一份可摄入物，按 Def.BaseMarketValue 计价累加。
        /// 来源 <c>Thing.Ingested</c> 捕获（Postfix），高频写入不生成事件流。
        /// </summary>
        public void OnThingConsumed(Pawn eater, Thing food)
        {
            if (!IsRecordingEnabled() || eater == null || food == null || food.def == null)
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
                bool isCurrent = ChronicleColonistScanner.TryClassifyCurrent(eater, out _);
                if (!isCurrent)
                {
                    return;
                }
                float unitValue = food.def.BaseMarketValue;
                if (unitValue <= 0f)
                {
                    return;
                }
                EnsurePawnArchivedForCapture(component, eater);
                string category = (food.def.FirstThingCategory != null)
                    ? food.def.FirstThingCategory.defName
                    : "Other";
                component.AddConsumption(
                    eater.GetUniqueLoadID(),
                    category,
                    unitValue,
                    Find.TickManager.TicksGame);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record consumed thing " + (food != null && food.def != null ? food.def.defName : "null") + ": " + ex.Message);
            }
        }

        /// <summary>
        /// v1.1.4 劳模住所/工坊检测（方案 A）：记录殖民者在一台工作台完成一次制造迭代。
        /// 来源 <c>Bill_Production.Notify_IterationCompleted</c> 捕获（Postfix）；低频，
        /// 不写事件流 —— 工作场所是聚合状态而非事件语义，与 ConsumptionAccumulator 同思路。
        /// 只存 defName 稳定键（Building_WorkTable.def.defName + 工坊所在房间 Room.Role.defName）；
        /// 玩家手动改名后 UI 实时解析 LabelCap 显示新名（改名正确变更契约）。
        /// </summary>
        public void OnWorkplaceUsed(Pawn worker, Building_WorkTable workbench)
        {
            if (!IsRecordingEnabled() || worker == null || workbench == null || workbench.def == null)
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
                bool workerIsCurrent = ChronicleColonistScanner.TryClassifyCurrent(worker, out _);
                if (!workerIsCurrent)
                {
                    return;
                }
                EnsurePawnArchivedForCapture(component, worker);
                // 工坊所在房间角色：RegionAndRoomQuery.GetRoom(Thing, RegionType)
                // —— 1.6 经反射核验的静态 API（Thing/Building 上无 GetRoom 方法）。
                string roomRoleDefName = null;
                Room room = workbench.Map != null
                    ? Verse.RegionAndRoomQuery.GetRoom(workbench, Verse.RegionType.Set_All)
                    : null;
                if (room != null && room.Role != null)
                {
                    roomRoleDefName = room.Role.defName;
                }
                string buildingStableId = workbench.def.defName + ":" + workbench.thingIDNumber;
                component.AddWorkplaceUse(
                    worker.GetUniqueLoadID(),
                    workbench.def.defName,
                    buildingStableId,
                    roomRoleDefName,
                    Find.TickManager.TicksGame);
                // v1.1.4 UI 拓展：记录工坊坐标（供 ITab 定位跳转）。
                PawnObject workRecord = component.GetObject(worker.GetUniqueLoadID()) as PawnObject;
                if (workRecord != null && workRecord.Workplace != null)
                {
                    workRecord.Workplace.RecordLocation(workbench.Map != null ? workbench.Map.Index : -1, workbench.Position);
                }
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record workplace use for " + (worker != null ? worker.LabelShort : "null") + ": " + ex.Message);
            }
        }

        /// <summary>
        /// v1.1.4 建筑别名：为指定殖民者的工作场所设置自定义别名。
        /// <paramref name="customName"/> 为 null/空时清除别名。数据层拥有 mutation 规则；
        /// 无别名时展示层回落 DefDatabase.LabelCap 实时解析名。
        /// </summary>
        public void SetWorkplaceCustomName(string pawnStableId, string customName)
        {
            if (string.IsNullOrEmpty(pawnStableId))
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
                component.SetWorkplaceCustomName(pawnStableId, customName);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to set workplace custom name for " + pawnStableId + ": " + ex.Message);
            }
        }

        /// <summary>
        /// v1.1.4 工坊实例全局别名：key = <c>defName:thingIDNumber</c>（BuildingStableId）。
        /// 设置/清除某台工坊的自定义名，任何使用该工坊的劳模档案共享显示此名。
        /// </summary>
        public void SetBuildingAlias(string buildingStableId, string customName)
        {
            if (string.IsNullOrEmpty(buildingStableId))
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
                component.SetBuildingAlias(buildingStableId, customName);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to set building alias for " + buildingStableId + ": " + ex.Message);
            }
        }

        /// <summary>
        /// v1.1.4 房间类型别名：key = RoomRoleDef.defName（如 Bedroom）。
        /// 设置/清除某房间类型的自定义显示名（mod 内部展示层覆盖，不改原版 Def）。
        /// </summary>
        public void SetRoomRoleAlias(string roomRoleDefName, string customName)
        {
            if (string.IsNullOrEmpty(roomRoleDefName))
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
                component.SetRoomRoleAlias(roomRoleDefName, customName);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to set room role alias for " + roomRoleDefName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// v1.1.4 房间级改名（集中于 ITab，无需家具）：key = <c>pawnStableId:RoomRoleDefName</c>。
        /// 设置「某殖民者的某类型房间」的自定义显示名，粒度到个人+类型。
        /// </summary>
        public void SetRoomName(string pawnStableId, string roomRoleDefName, string customName)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(roomRoleDefName))
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
                component.SetRoomName(pawnStableId, roomRoleDefName, customName);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to set room name for " + pawnStableId + ":" + roomRoleDefName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// v1.1.4 房间级自定义名读取（null = 未改名，回落类型级别名 / RoomRoleDef.LabelCap）。
        /// </summary>
        public string GetRoomName(string pawnStableId, string roomRoleDefName)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(roomRoleDefName))
            {
                return null;
            }
            try
            {
                ChronicleGameComponent component = Component;
                return component != null ? component.GetRoomName(pawnStableId, roomRoleDefName) : null;
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to get room name for " + pawnStableId + ":" + roomRoleDefName + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// v1.1.4 房间级类型名替换（底层 Role 不变）：key = <c>pawnStableId:RoomRoleDefName</c>。
        /// </summary>
        public void SetRoomTypeName(string pawnStableId, string roomRoleDefName, string customName)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(roomRoleDefName))
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
                component.SetRoomTypeName(pawnStableId, roomRoleDefName, customName);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to set room type name for " + pawnStableId + ":" + roomRoleDefName + ": " + ex.Message);
            }
        }

        /// <summary>
        /// v1.1.4 房间级类型名替换读取（null = 未替换）。
        /// </summary>
        public string GetRoomTypeName(string pawnStableId, string roomRoleDefName)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(roomRoleDefName))
            {
                return null;
            }
            try
            {
                ChronicleGameComponent component = Component;
                return component != null ? component.GetRoomTypeName(pawnStableId, roomRoleDefName) : null;
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to get room type name for " + pawnStableId + ":" + roomRoleDefName + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// v1.1.4 工坊实例全局别名读取（null = 未改名）。
        /// </summary>
        public string GetBuildingAlias(string buildingStableId)
        {
            if (string.IsNullOrEmpty(buildingStableId))
            {
                return null;
            }
            try
            {
                ChronicleGameComponent component = Component;
                return component != null ? component.GetBuildingAlias(buildingStableId) : null;
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to read building alias for " + buildingStableId + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// v1.1.4 房间类型别名读取（null = 未改名）。
        /// </summary>
        public string GetRoomRoleAlias(string roomRoleDefName)
        {
            if (string.IsNullOrEmpty(roomRoleDefName))
            {
                return null;
            }
            try
            {
                ChronicleGameComponent component = Component;
                return component != null ? component.GetRoomRoleAlias(roomRoleDefName) : null;
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to read room role alias for " + roomRoleDefName + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// v4.9: records an equipment thing's decommission (退役仪式) — the thing's
        /// "death record", captured read-only at destroy time. Never prevents the
        /// destroy; only writes when the thing has an archive object (was ever
        /// registered) and no prior decommission record (idempotent).
        /// </summary>
        public void OnThingDestroyed(Thing thing, Pawn lastHolder = null)
        {
            if (!IsRecordingEnabled() || thing == null || thing.def == null)
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
                string stableId = thing.def.defName + ":" + thing.thingIDNumber;
                ThingObject thingObj = component.GetObject(stableId) as ThingObject;
                if (thingObj == null || thingObj.Decommission != null)
                {
                    // Not archived (never a chronicle thing) or already retired.
                    return;
                }
                DecommissionRecord rec = new DecommissionRecord
                {
                    Tick = Find.TickManager.TicksGame,
                    LastPlaceLabel = PlaceLabelForDestroyedThing(thing)
                };
                if (lastHolder != null)
                {
                    rec.LastHolderStableId = lastHolder.GetUniqueLoadID();
                    rec.LastHolderLabel = lastHolder.LabelShort;
                }
                else if (!string.IsNullOrEmpty(thingObj.CurrentHolderId))
                {
                    ArchiveObject cur = component.GetObject(thingObj.CurrentHolderId);
                    if (cur != null)
                    {
                        rec.LastHolderStableId = cur.StableId;
                        rec.LastHolderLabel = !string.IsNullOrEmpty(cur.LabelSnapshot)
                            ? cur.LabelSnapshot
                            : cur.StableId;
                    }
                }
                // Service days: derived from the tenure span (first record start →
                // now) so the number stays consistent with the legacy chain.
                if (thingObj.HolderRecords != null && thingObj.HolderRecords.Count > 0)
                {
                    long start = thingObj.HolderRecords[0].StartTick;
                    if (start > 0L)
                    {
                        rec.ServiceDays = Math.Max(0,
                            (int)GenDate.TicksToDays((int)(Find.TickManager.TicksGame - start)));
                    }
                }
                thingObj.Decommission = rec;
                component.MarkChanged();
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record decommission for "
                    + (thing != null && thing.def != null ? thing.def.defName : "null") + ": " + ex.Message);
            }
        }

        /// <summary>
        /// v4.9: place key for a thing being destroyed — the map biome defName when
        /// on a map, "—" otherwise. Stored as a language-independent defName (never a
        /// localized label) so the archived record survives language switches; the
        /// Read Model resolves the biome label at display time via BiomeLabel.
        /// </summary>
        private static string PlaceLabelForDestroyedThing(Thing thing)
        {
            if (thing == null) return "—";
            Map map = thing.Map;
            if (map != null && map.Biome != null && !string.IsNullOrEmpty(map.Biome.defName))
            {
                return map.Biome.defName;
            }
            return "—";
        }

        public void OnThingBuilt(ThingDef builtDef, string builtStableId, Pawn worker)
        {
            if (!IsRecordingEnabled() || builtDef == null || string.IsNullOrEmpty(builtStableId))
            {
                return;
            }
            // v4.6.5: buildings are not equipment — excluded from the archive
            // object graph (the "Thing" category is scoped to weapons + apparel).
            if (!IsEquipableDef(builtDef))
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
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record built thing " + (builtDef != null ? builtDef.defName : "null") + ": " + ex.Message);
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
                        IncidentDefName = incidentDef.defName,
                        // v4.14: snapshot the threat category (ThreatBig/ThreatSmall)
                        // so the overview card + KPI can tint without Def drift.
                        ThreatKey = BattleThreatKey(incidentDef)
                    });
                }
                BattleObject battle = component.GetObject(stableId) as BattleObject;
                if (battle != null && battle.StartTick < 0L)
                {
                    // Snapshot the trigger time exactly once; a re-firing of the same
                    // incident in the same tick overwrites nothing (stableId is tick-bound).
                    battle.StartTick = Find.TickManager.TicksGame;
                }
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
                // v4.11 P0: link the raid Lord(s) just spawned by TryExecuteWorker and
                // snapshot the force size + runtime countdown. TryExecuteWorker ran
                // synchronously inside IncidentWorker.TryExecute, so the Lords exist now.
                LinkRaidLords(battle);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record battle start " + (incidentDef != null ? incidentDef.defName : "null") + ": " + ex.Message);
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
                // v6.8: 该殖民者参与本次战役，累加个人参战次数（幂等由 ParticipantIds 去重保证）。
                PawnObject po = component.GetObject(id) as PawnObject;
                if (po != null)
                {
                    po.ParticipatedBattles++;
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

        /// <summary>
        /// v4.11 P0: links the raid Lord(s) that <c>IncidentWorker_Raid.TryExecuteWorker</c>
        /// just spawned on the map to the active battle, and snapshots the raid force
        /// size. The raid is represented in vanilla by one or more <see cref="Lord"/>s
        /// (e.g. <c>LordJob_AssaultColony</c>) whose <c>ownedPawns</c> are exactly the
        /// enemy raiders. We scan every loaded map's LordManager for hostile,
        /// non-player Lords that are not yet linked, and attribute them to this
        /// battle. This is the precise (no-polling) "force size" capture point: the
        /// Lords exist because TryExecuteWorker ran synchronously before this call.
        ///
        /// Non-Lord threats (infestation, mech cluster, ship part) have no raid Lord,
        /// so RaidCount stays -1 and the battle relies on <see cref="ClosePreviousBattle"/>
        /// (next battle start) as the repulse fallback — consistent with the
        /// archive-only, record-after-process positioning.
        /// </summary>
        public void LinkRaidLords(BattleObject battle)
        {
            if (battle == null || string.IsNullOrEmpty(battle.StableId))
            {
                return;
            }
            try
            {
                int total = 0;
                bool linkedAny = false;
                List<Map> maps = Find.Maps;
                if (maps != null)
                {
                    for (int mi = 0; mi < maps.Count; mi++)
                    {
                        Map map = maps[mi];
                        if (map == null || map.lordManager == null || map.lordManager.lords == null)
                        {
                            continue;
                        }
                        for (int li = 0; li < map.lordManager.lords.Count; li++)
                        {
                            Lord lord = map.lordManager.lords[li];
                            if (lord == null || lord.faction == null)
                            {
                                continue;
                            }
                            // Only enemy raid Lords count toward the force size: the
                            // faction must be hostile to the player. This excludes
                            // caravans, visitors, animal herds and the player's own
                            // Lords, which would otherwise inflate RaidCount.
                            if (!lord.faction.HostileTo(Faction.OfPlayer))
                            {
                                continue;
                            }
                            // Skip Lords already attributed to a battle.
                            if (raidLordToBattle.ContainsKey(lord.loadID))
                            {
                                continue;
                            }
                            if (lord.ownedPawns == null || lord.ownedPawns.Count == 0)
                            {
                                continue;
                            }
                            raidLordToBattle[lord.loadID] = battle.StableId;
                            total += lord.ownedPawns.Count;
                            linkedAny = true;
                        }
                    }
                }
                if (linkedAny)
                {
                    battle.RaidCount = total;
                    battle.RemainingRaidCount = total;
                }
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to link raid lords: " + ex.Message);
            }
        }

        /// <summary>
        /// v4.11 P0: a raid pawn left the map for a linked Lord. <paramref name="remainingPawns"/>
        /// is the Lord's authoritative remaining raider count (ownedPawns.Count after
        /// the loss). When it reaches zero, every raider of this Lord is gone and the
        /// battle is finalized (EndTick written). Lords not linked to any battle are
        /// ignored. We use the authoritative count directly rather than decrementing
        /// a counter, so a pawn reported lost more than once (different
        /// PawnLostCondition values) can never over-decrement, and the battle ends
        /// exactly when the last raider leaves — for both single-Lord and multi-Lord
        /// raids.
        /// </summary>
        public void OnRaidPawnGone(int lordLoadId, int remainingPawns)
        {
            string battleStableId;
            if (!raidLordToBattle.TryGetValue(lordLoadId, out battleStableId)
                || string.IsNullOrEmpty(battleStableId))
            {
                return;
            }
            FinalizeBattleIfRepursed(battleStableId, lordLoadId, remainingPawns);
        }

        /// <summary>
        /// Writes EndTick when every linked Lord has lost all its pawns. We track the
        /// battle's runtime RemainingRaidCount as the MINIMUM remaining-raider count
        /// across the linked Lords (a multi-Lord raid is repulsed only once all its
        /// Lords are empty). A Lord is removed from the link map when its raiders hit
        /// zero, so its stale (later non-zero) notifications can never resurrect a
        /// finalized battle. Battles with RaidCount &lt;= 0 (no linked Lord, e.g.
        /// non-Lord threats) are never finalized here — ClosePreviousBattle covers them.
        /// </summary>
        private void FinalizeBattleIfRepursed(string battleStableId, int lordLoadId, int lordRemaining)
        {
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                BattleObject battle = component.GetObject(battleStableId) as BattleObject;
                if (battle == null || battle.EndTick != -1L)
                {
                    return;
                }
                if (battle.RaidCount <= 0)
                {
                    // No linked Lord force to track; leave to ClosePreviousBattle.
                    return;
                }
                if (lordRemaining <= 0)
                {
                    // This Lord's raiders are all gone: stop tracking it and finalize
                    // if no other linked Lord still has pawns.
                    raidLordToBattle.Remove(lordLoadId);
                    bool anyRemaining = false;
                    foreach (KeyValuePair<int, string> kv in raidLordToBattle)
                    {
                        if (kv.Value == battleStableId)
                        {
                            anyRemaining = true;
                            break;
                        }
                    }
                    if (!anyRemaining)
                    {
                        battle.EndTick = Find.TickManager.TicksGame;
                        battle.RemainingRaidCount = 0;
                        component.MarkChanged();
                    }
                    return;
                }
                // Keep RemainingRaidCount as the smallest seen non-zero remaining across Lords.
                if (battle.RemainingRaidCount < 0 || lordRemaining < battle.RemainingRaidCount)
                {
                    battle.RemainingRaidCount = lordRemaining;
                }
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to finalize battle: " + ex.Message);
            }
        }

        /// <summary>
        /// v4.11 P0: clears the static Lord→battle link map. Called from
        /// ChronicleGameComponent.FinalizeInit on every new game / load so a previous
        /// session's (loadID-scoped) links can never leak into the next save.
        /// </summary>
        public static void ResetRaidLordLinks()
        {
            raidLordToBattle.Clear();
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
            // v4.6.5: the "Thing" category is scoped to equipment only
            // (weapons + wearable apparel). Raw materials, food and buildings
            // are excluded from the archive object graph.
            if (!IsEquipable(thing))
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

        /// <summary>
        /// v4.6.5: the "Thing" archive category is scoped to equipment — weapons
        /// and wearable apparel. Buildings, raw materials and food are excluded.
        /// </summary>
        private static bool IsEquipable(Thing thing)
        {
            if (thing == null || thing.def == null)
            {
                return false;
            }
            return IsEquipableDef(thing.def);
        }

        private static bool IsEquipableDef(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }
            // v4.9.1: data-driven equipment archive policy — weapons always in;
            // apparel only when it carries enough armor to count as combat apparel
            // (dust jackets / work wear / fashion clothes are excluded). Mirrors
            // Patch_ThingDestroy so capture and decommission scopes stay aligned.
            return Domain.ThingArchivePolicy.Captures(def);
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
                // 统一默认决策：新档=开局(0)，读档=发现当天起点。禁止硬编码 -1L。
                JoinTick = component.ResolveDefaultJoinTick(),
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

        /// <summary>
        /// Cross-bucket idempotency: returns true if a death/kill event for the given
        /// victim already exists under ANY killer bucket. Scans the global event list
        /// (kills are rare, so an O(N) pass is acceptable) to prevent duplicate Death
        /// events when the same pawn death is captured by multiple code paths
        /// (e.g. instigator==null hitting both OnPawnDied and OnKillRecorded).
        /// </summary>
        private static bool HasRecordedDeathForVictim(ChronicleGameComponent component, string victimId)
        {
            if (component == null || string.IsNullOrEmpty(victimId) || component.Events == null)
            {
                return false;
            }
            for (int i = 0; i < component.Events.Count; i++)
            {
                ChronicleEvent ev = component.Events[i];
                if (ev == null || ev.TypeKey != ChronicleEventType.Death || ev.Params == null)
                {
                    continue;
                }
                string recordedVictimId;
                if (ev.Params.TryGetValue(ChronicleEventParams.VictimStableId, out recordedVictimId)
                    && recordedVictimId == victimId)
                {
                    return true;
                }
            }
            return false;
        }

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

        /// <summary>
        /// v4.0 home KPI: days since the earliest recorded event or colonist join.
        /// Falls back to the current game tick when the archive is empty so that
        /// freshly-started colonies show 0 rather than a meaningless large number.
        /// </summary>
        public int GetServiceDays()
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
            long firstTick = long.MaxValue;
            IReadOnlyList<ChronicleEvent> events = component.GetAllEvents();
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev != null && ev.Tick < firstTick)
                {
                    firstTick = ev.Tick;
                }
            }
            IReadOnlyList<ArchiveObject> pawns = component.GetObjectsOfCategory(ArchiveCategoryKeys.Pawn);
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i] is PawnObject pawn && pawn.JoinTick >= 0L && pawn.JoinTick < firstTick)
                {
                    firstTick = pawn.JoinTick;
                }
            }
            if (firstTick == long.MaxValue)
            {
                return 0;
            }
            long currentTick = Find.TickManager.TicksGame;
            if (currentTick <= firstTick)
            {
                return 0;
            }
            return (int)GenDate.TicksToDays((int)(currentTick - firstTick));
        }

        /// <summary>
        /// v4.0 home view mode persisted in the game component.
        /// </summary>
        public int GetHomeViewMode()
        {
            if (Current.Game == null)
            {
                return 0;
            }
            ChronicleGameComponent component = Component;
            return component?.HomeViewMode ?? 0;
        }

        /// <summary>
        /// v4.0 home view mode persisted in the game component.
        /// </summary>
        public void SetHomeViewMode(int mode)
        {
            if (Current.Game == null)
            {
                return;
            }
            ChronicleGameComponent component = Component;
            if (component != null)
            {
                component.HomeViewMode = mode;
            }
        }

        // ---------------------------------------------------------------------
        // Legacy ↔ unified event sink bridge (v4.1)
        // ---------------------------------------------------------------------

        /// <summary>
        /// v4.1 bridge: routes a fully-formed <see cref="ArchiveEventInput"/> through
        /// the unified <see cref="IArchiveEventSink.TryRecord"/>. This is the single
        /// connection point between the rich legacy write methods and the unified
        /// event contract — subclasses / external callers on the legacy surface use
        /// this instead of duplicating sink logic. Never throws on bad input.
        /// </summary>
        public CaptureResult RecordEvent(ArchiveEventInput input)
        {
            return TryRecord(input);
        }

        // ---------------------------------------------------------------------
        // IArchiveEventSink (v4.1 unified write entry point)
        // ---------------------------------------------------------------------
        // Idempotency cache scoped to the current game session. Not persisted —
        // it only guards against repeated captures of the same logical event
        // within one playthrough.
        private static readonly HashSet<string> SessionDedupKeys = new HashSet<string>();
        private static Game _dedupGame;

        /// <summary>
        /// Unified record entry. Converts <see cref="ArchiveEventInput"/> to a
        /// <see cref="ChronicleEvent"/>, runs idempotency + validity checks, then
        /// delegates to the existing <see cref="AddEvent"/> pipeline (recording
        /// toggle, per-pawn cap, deduplication-by-stableId inside Component).
        /// Never throws on bad input.
        /// </summary>
        public CaptureResult TryRecord(ArchiveEventInput input)
        {
            if (input == null || !input.IsValid)
            {
                return CaptureResult.Rejected;
            }
            if (Current.Game == null || Component == null)
            {
                return CaptureResult.Unavailable;
            }

            // Session-scoped idempotency for explicit dedup keys.
            if (!string.IsNullOrEmpty(input.DeduplicationKey))
            {
                EnsureDedupScope();
                if (SessionDedupKeys.Contains(input.DeduplicationKey))
                {
                    return CaptureResult.Duplicate;
                }
                SessionDedupKeys.Add(input.DeduplicationKey);
            }

            if (!IsRecordingEnabled())
            {
                return CaptureResult.Rejected;
            }

            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(input.EventTypeDefName);
            if (def == null)
            {
                return CaptureResult.Rejected;
            }

            ChronicleEvent ev = ToChronicleEvent(input, def);
            if (ev == null)
            {
                return CaptureResult.Rejected;
            }

            AddEvent(Component, input.Primary.StableId, ev);
            return CaptureResult.Accepted;
        }

        /// <summary>
        /// Resets the session dedup set whenever the active game changes so a new
        /// playthrough is not polluted by a previous session's dedup keys.
        /// </summary>
        private static void EnsureDedupScope()
        {
            Game game = Current.Game;
            if (!ReferenceEquals(game, _dedupGame))
            {
                SessionDedupKeys.Clear();
                _dedupGame = game;
            }
        }

        private ChronicleEvent ToChronicleEvent(ArchiveEventInput input, ChronicleEventDef def)
        {
            long tick = input.Tick > 0 ? input.Tick : (Current.Game?.tickManager?.TicksGame ?? 0);
            if (tick <= 0)
            {
                return null;
            }

            int importanceLevel = (int)(input.Importance
                ?? ChronicleEventImportance.Resolve(def.defName, input.Parameters));

            Dictionary<string, string> parameters = input.Parameters == null
                ? new Dictionary<string, string>()
                : input.Parameters.ToDictionary(kv => kv.Key, kv => kv.Value);

            List<ObjectRef> subjects = null;
            if (input.Subjects != null && input.Subjects.Count > 0)
            {
                subjects = new List<ObjectRef>();
                foreach (ArchiveEntityRef sub in input.Subjects)
                {
                    if (sub != null && sub.IsValid)
                    {
                        subjects.Add(sub.ToObjectRef());
                    }
                }
            }

            return new ChronicleEvent
            {
                SourceId = input.SourceId,
                TypeKey = input.EventTypeDefName,
                Tick = tick,
                ImportanceLevel = importanceLevel,
                Primary = input.Primary.ToObjectRef(),
                Subjects = subjects,
                Params = parameters,
            };
        }
    }
}

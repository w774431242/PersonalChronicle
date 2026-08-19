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
    /// Partial of <see cref="ArchiveService"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
    {

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

        public bool IsCurrentlyEnlisted(string stableId)
        {
            if (string.IsNullOrEmpty(stableId) || Current.Game == null)
            {
                return false;
            }
            Pawn live = GetLivePawn(stableId);
            return live != null && ChronicleColonistScanner.TryClassifyCurrent(live, out _);
        }

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

        public void GetLiveColonistCounts(out int free, out int slave, out int prisoner)
        {
            // Refresh (or hit cache) so out-params are always fresh on first read.
            GetLiveColonistCount();
            free = cachedFreeColonistCount;
            slave = cachedSlaveCount;
            prisoner = cachedPrisonerCount;
        }

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

        public int GetActiveSnapshotCount()
        {
            CountPawnSnapshots(out int active, out int archived);
            return active;
        }

        public int GetArchivedSnapshotCount()
        {
            CountPawnSnapshots(out int active, out int archived);
            return archived;
        }

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

        public int GetHomeViewMode()
        {
            if (Current.Game == null)
            {
                return 0;
            }
            ChronicleGameComponent component = Component;
            return component?.HomeViewMode ?? 0;
        }

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


    }
}

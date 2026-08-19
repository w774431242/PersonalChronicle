using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Data
{
    /// <summary>Partial of ChronicleGameComponent 鈥?see main file for Scribe/migration.</summary>
    public sealed partial class ChronicleGameComponent : GameComponent
    {

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

        internal bool AddConsumption(
            string pawnStableId,
            string categoryDefName,
            float silver,
            long gameTick)
        {
            if (string.IsNullOrEmpty(pawnStableId)
                || string.IsNullOrEmpty(categoryDefName)
                || silver <= 0f)
            {
                return false;
            }
            PawnObject pawn = GetObject(pawnStableId) as PawnObject;
            if (pawn == null || pawn.IsArchived)
            {
                return false;
            }
            if (pawn.Consumption == null)
            {
                pawn.Consumption = new ConsumptionAccumulator();
            }
            float safe = silver;
            if (float.IsNaN(safe) || float.IsInfinity(safe) || safe < 0f)
            {
                safe = 0f;
            }
            pawn.Consumption.Add(categoryDefName, safe, gameTick);
            MarkChanged();
            return true;
        }

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
                    // 死亡兜底建档也走统一默认决策（新档=开局0 / 读档=当天起点），
                    // 保证 UI 不出现"中途加入"未知态。
                    JoinTick = ResolveDefaultJoinTick(),
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

        internal void MarkChanged()
        {
            DataRevision = DataRevision == long.MaxValue ? 0L : DataRevision + 1L;
        }

        private int CloseStaleLocations()
        {
            IReadOnlyList<ArchiveObject> locations = GetObjectsOfCategory(ArchiveCategoryKeys.Location);
            if (locations == null || locations.Count == 0)
            {
                return 0;
            }
            int closed = 0;
            for (int i = 0; i < locations.Count; i++)
            {
                LocationObject loc = locations[i] as LocationObject;
                if (loc == null || loc.DeinitTick != -1L)
                {
                    continue; // 已关闭 / 非地点
                }
                if (loc.StableId == null
                    || !loc.StableId.StartsWith(PlaceVisitKeys.WorldIdPrefix, System.StringComparison.Ordinal))
                {
                    // 风险规避（与 IsUnvisitedStaleLocation 口径一致）：只收敛
                    // World_ 前缀的"未到访世界对象"（旧版 Source2 误建的世界
                    // settlement / quest site）。Map_ 前缀 = 玩家生成过地图的
                    // 地点（Source1 建档），即使暂无事件也是"玩家到访过"的
                    // 铁证，绝不因迁移关闭——否则后续事件引用会悬空。
                    continue;
                }
                if (loc.IsPlayerHome)
                {
                    continue; // 玩家本家保留
                }
                if (GetEventsFor(loc.StableId).Count > 0)
                {
                    continue; // 有事件历史 = 玩家参与过，保留
                }
                // 关闭未到访地点（仅需标记，索引无需立即重建——下次 RebuildIndexes 会处理）
                loc.DeinitTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
                loc.DeinitReason = PlaceVisitKeys.DeinitReasonUnvisited;
                closed++;
            }
            if (closed > 0)
            {
                MarkChanged();
            }
            return closed;
        }

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

        private void AddDetectedJoinEvent(PawnObject record)
        {
            if (record == null || string.IsNullOrEmpty(record.StableId))
            {
                return;
            }
            AddEvent(new ChronicleEvent
            {
                // 统一决策保证 JoinTick >= 0（新档开局=0 / 读档=当天起点），Join
                // 事件 tick 直接对齐加入日，与生涯 Join 阶段一致。
                Tick = record.JoinTick,
                TypeKey = ChronicleEventType.Join,
                Primary = ObjectRef.ForPawn(record.StableId, record.LabelSnapshot),
                Subjects = new List<ObjectRef>(),
                Params = new Dictionary<string, string>()
            });
        }

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

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
    /// Partial of ChronicleGameComponent — 归档数据的查询 API 与写入入口。
    /// </summary>
    /// <remarks>
    /// · 职责：对外提供对象 / 事件 / 关联 / 分类的只读查询，对内提供对象与事件的写入入口，
    ///   以及索引重建、陈旧地点收敛等维护操作。
    /// · 调用方：ArchiveService（应用层）、Read Model 装配、UI 绘制层。
    /// · 铁律：
    ///   - 任何改变数据的方法都必须调用 MarkChanged() 递增 DataRevision，
    ///     否则以 Revision 为失效依据的 UI 缓存不会刷新。新增写方法时不要漏掉。
    ///   - AddObject 按 StableId 去重，重复添加返回 false 而不是覆盖——覆盖会让既有事件悬空。
    ///   - 查询方法在参数非法时统一返回空集合而不是 null，调用方可直接遍历。
    /// · 约定：
    ///   - 金额与数量在入账前先做 NaN / Infinity / 负数 归一化，避免脏数据污染聚合。
    ///   - 已归档（IsArchived）的 pawn 不再接受消耗与工时入账，归档即冻结。
    ///   - 索引（objectsByStableId / eventsByObject）由 RebuildIndexes 统一重建，
    ///     平时只做增量维护；不要在查询路径里重建索引。
    /// · Scribe 与版本迁移见 ChronicleGameComponent.cs。
    /// </remarks>
    public sealed partial class ChronicleGameComponent : GameComponent
    {

        /// <summary>
        /// 取得全部 pawn 归档记录。
        /// </summary>
        /// <returns>所有 PawnObject 组成的列表；无记录时为空集合。</returns>
        /// <remarks>每次调用都会新建列表并遍历 Objects，属 O(n)；
        /// 在绘制循环中调用前请确认上层是否已缓存。</remarks>
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

        /// <summary>
        /// 按存活 pawn 取其归档记录。
        /// </summary>
        /// <param name="pawn">存活 pawn；为 null 时返回空集合。</param>
        /// <returns>命中时返回只含该记录的列表，否则返回空集合（不是 null）。</returns>
        /// <remarks>以 GetUniqueLoadID 作为 StableId 查索引，这正是 pawn 与归档记录的关联键，
        /// 不要改用其它 ID。</remarks>
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
        /// 取指定对象的事件列表。
        /// </summary>
        /// <param name="stableId">对象稳定 ID。</param>
        /// <returns>事件的只读集合；参数为空或无事件时返回空集合。</returns>
        /// <remarks>返回 ReadOnlyCollection 是刻意的：防止调用方在不知情时改动内部列表，
        /// 绕过 AddEvent 的编号分配与索引维护。</remarks>
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

        /// <summary>
        /// 按稳定 ID 取归档对象。
        /// </summary>
        /// <param name="stableId">对象稳定 ID。</param>
        /// <returns>命中的对象；参数为空或未找到时返回 null。</returns>
        /// <remarks>走字典索引，属 O(1)，可在绘制与采样热路径放心调用。</remarks>
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

        /// <summary>
        /// 取与指定对象通过事件产生关联的全部对象。
        /// </summary>
        /// <param name="stableId">对象稳定 ID。</param>
        /// <returns>关联对象列表（已排除自身）；无关联时返回空集合。</returns>
        /// <remarks>
        /// "关联"的语义等价于"共同出现在同一条事件中"，关系从事件的 Primary 与 Subjects 收集。
        /// 用 HashSet 去重，避免同一对对象因多条事件而重复出现。
        /// </remarks>
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

        /// <summary>
        /// 取指定分类下的全部归档对象。
        /// </summary>
        /// <param name="categoryKey">分类键；为空时返回空集合。</param>
        /// <returns>该分类的对象列表。</returns>
        /// <remarks>线性扫描 Objects，属 O(n)；分类键常量见 ArchiveCategoryKeys。</remarks>
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
        /// 取最近发生的若干条事件（按时间倒序）。
        /// </summary>
        /// <param name="count">条数上限；小于等于 0 时返回空集合。</param>
        /// <returns>按 Tick 降序排列的前 count 条事件。</returns>
        /// <remarks>每次调用都会复制并排序整个事件列表，属 O(n log n)，
        /// 不要在每帧绘制路径中调用，应由上层缓存后再用。</remarks>
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

        /// <summary>
        /// 新增一个归档对象。
        /// </summary>
        /// <param name="obj">待添加对象；StableId 为空则拒绝。</param>
        /// <returns>成功返回 true；对象为 null、无 StableId，或 StableId 已存在时返回 false。</returns>
        /// <remarks>已存在的 StableId 直接拒绝而不是覆盖：
        /// 覆盖会让既有事件指向新对象，造成历史数据悬空。要更新内容请直接改对象字段。</remarks>
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
        /// 入账一条生产记录。
        /// </summary>
        /// <param name="pawnStableId">生产者稳定 ID。</param>
        /// <param name="thingDefName">产物 ThingDef 名。</param>
        /// <param name="quantity">数量，须为正。</param>
        /// <param name="marketValue">市场价值；异常值会被归一化为 0。</param>
        /// <param name="gameTick">发生时刻。</param>
        /// <returns>成功返回 true；参数非法，或对象不存在、非 pawn 时返回 false。</returns>
        /// <remarks>marketValue 的 NaN / Infinity / 负数 归一化是必须的：
        /// 单个异常值会让后续的求和与占比全部变成 NaN，污染整个生产汇总。</remarks>
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
        /// 入账一条消耗记录。
        /// </summary>
        /// <param name="pawnStableId">pawn 稳定 ID。</param>
        /// <param name="categoryDefName">消耗类别 defName。</param>
        /// <param name="silver">银两数额，须为正。</param>
        /// <param name="gameTick">发生时刻。</param>
        /// <returns>成功返回 true；参数非法、对象不存在，或该 pawn 已归档时返回 false。</returns>
        /// <remarks>已归档的 pawn 拒绝入账：归档后不再产生新消耗，
        /// 继续写入会让"生涯消耗"的口径失真。</remarks>
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

        /// <summary>
        /// 入账一条工时采样。
        /// </summary>
        /// <param name="pawnStableId">pawn 稳定 ID。</param>
        /// <param name="workTypeDefName">工种 defName。</param>
        /// <param name="sampleTicks">本次采样时长，须为正。</param>
        /// <param name="gameTick">采样时刻。</param>
        /// <returns>成功返回 true；参数非法、对象不存在，或该 pawn 已归档时返回 false。</returns>
        /// <remarks>
        /// 这是工时账本的唯一写入口：ArchiveService.RecordSample 负责校验，通过后转调这里。
        /// 已归档拒绝入账，与消耗口径一致——归档即冻结。
        /// </remarks>
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

        /// <summary>
        /// 归档一个 pawn：标记死亡时刻与死因，并冻结相关快照。
        /// </summary>
        /// <param name="stableId">pawn 稳定 ID。</param>
        /// <param name="deathCauseKey">死因键。</param>
        /// <param name="pawn">存活 pawn，用于补正角色与冻结快照；可为 null。</param>
        /// <returns>成功返回 true；对象非 PawnObject，或已归档时返回 false。</returns>
        /// <remarks>
        /// 对象不存在时会兜底建档：JoinTick 走 ResolveDefaultJoinTick 的统一决策
        /// （新档为开局 0、读档为当天起点），以保证 UI 不出现"中途加入"的未知态。
        /// 角色只在为 None 时才补正：老档默认或此前无法判定的情况才填，已定角色绝不覆盖。
        /// 死亡时由 PawnArchiveSnapshots.ApplyDeathSnapshots 冻结技能等快照；
        /// 工时则靠 IsArchived 自然停止累加，不需要额外处理。
        /// </remarks>
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

        /// <summary>
        /// 新增一条编年史事件。
        /// </summary>
        /// <param name="ev">待添加事件；为 null 时静默返回。</param>
        /// <remarks>
        /// 统一在此分配自增 Id、解析重要度（仅在 ImportanceLevel 小于 0 时解析），
        /// 随后立即维护事件边索引。因此外部不要自行设置 Id 与 ImportanceLevel，
        /// 也不要绕过本方法直接往 Events 里塞对象。
        /// </remarks>
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

        /// <summary>
        /// 递增数据版本号，标记数据已变更。
        /// </summary>
        /// <remarks>
        /// 所有写操作都必须调用本方法：上层的缓存（如殖民地工时聚合）以 DataRevision 作为失效依据。
        /// 版本号到达 long.MaxValue 时回绕到 0，避免溢出为负数导致缓存永久失效。
        /// </remarks>
        internal void MarkChanged()
        {
            DataRevision = DataRevision == long.MaxValue ? 0L : DataRevision + 1L;
        }

        /// <summary>
        /// 收敛陈旧的未到访地点，返回本次关闭的数量。
        /// </summary>
        /// <returns>被关闭的地点数量。</returns>
        /// <remarks>
        /// 只关闭 World_ 前缀、非玩家本家、且没有任何事件历史的地点，用于清理旧版误建的世界对象。
        /// Map_ 前缀的地点即使暂无事件也必须保留：它表示玩家确实生成过地图，
        /// 是"玩家到访过"的铁证，关闭会让后续事件的引用悬空。
        /// 本方法只打 DeinitTick 与 DeinitReason 标记，不立即重建索引（交由下次 RebuildIndexes）。
        /// 判定口径需与 IsUnvisitedStaleLocation 保持一致，改一处要同步另一处。
        /// </remarks>
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

        /// <summary>
        /// 构建 pawn 记录的兼容镜像（只拷贝基础字段）。
        /// </summary>
        /// <returns>镜像记录列表；无有效 pawn 对象时为空集合。</returns>
        /// <remarks>用于兼容仍依赖 PawnRecord 旧结构的调用方。
        /// 新增需要透传的字段时必须同步补到这里，否则镜像会静默丢字段且不易察觉。</remarks>
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

        /// <summary>
        /// 全量重建稳定 ID 索引与事件边索引。
        /// </summary>
        /// <remarks>
        /// 只在读档或结构变更后调用，属 O(对象数 + 事件数)，不要在查询路径中调用。
        /// 稳定 ID 重复时保留首个、忽略后续：重复 ID 属数据异常，
        /// 静默跳过比抛异常更安全，可避免整个存档无法加载。
        /// </remarks>
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
        /// 收集一条事件引用到的全部对象 ID（Primary 与 Subjects）。
        /// </summary>
        /// <param name="ev">事件；为 null 时直接返回。</param>
        /// <param name="ids">输出集合，会就地累加而不清空。</param>
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

        /// <summary>
        /// 判断一条事件是否引用了给定集合中的任一对象。
        /// </summary>
        /// <param name="ev">事件。</param>
        /// <param name="stableIds">候选稳定 ID 集合；为 null 或空时返回 false。</param>
        /// <returns>Primary 或任一 Subject 命中即返回 true。</returns>
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
        /// 为一个存活 pawn 创建归档记录，并应用入队快照。
        /// </summary>
        /// <param name="pawn">存活 pawn。</param>
        /// <param name="joinTick">入队时刻 tick。</param>
        /// <param name="role">角色。</param>
        /// <returns>新建的 PawnObject；注意尚未加入 Objects 与索引。</returns>
        /// <remarks>
        /// 创建时会一并冻结入队快照（技能等）并捕获主要地点。
        /// 本方法只负责构造，加入集合与索引由调用方配合 AddObject 完成，
        /// 不要在本方法内直接 Add，否则调用方无法做去重判断。
        /// </remarks>
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
        /// 为新发现（补建档案）的 pawn 补记一条 Join 事件。
        /// </summary>
        /// <param name="record">已建档的 pawn 记录。</param>
        /// <remarks>
        /// 事件的 tick 直接对齐 JoinTick，与生涯时间轴的 Join 阶段保持一致。
        /// 必须走 AddEvent 以统一分配编号与维护索引，不要自行 new 后塞进 Events。
        /// </remarks>
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

        /// <summary>
        /// 取得全部事件的副本。
        /// </summary>
        /// <returns>事件列表副本；无事件时返回空集合。</returns>
        /// <remarks>返回副本是刻意的：调用方排序或筛选时不会改动内部列表。
        /// 注意这是 O(n) 复制，避免在绘制循环中高频调用。</remarks>
        /// <summary>
        /// 事件按时间分桶计数（**O(N) 单次遍历，零排序零复制**）：段 0 = 最老 → 段 n-1 = 最近。
        /// 供以「时间序列」表达活跃度的面板使用（人员中心绩效面板的活跃度柱）。
        /// </summary>
        /// <param name="bucketCount">分桶数（&lt;=0 返回空数组）。</param>
        /// <param name="bucketTicks">每桶跨度的 tick 数（&lt;=0 返回空数组）。</param>
        public int[] GetEventBuckets(int bucketCount, long bucketTicks)
        {
            if (bucketCount <= 0 || bucketTicks <= 0L)
            {
                return new int[0];
            }
            int[] buckets = new int[bucketCount];
            if (Events == null || Events.Count == 0)
            {
                return buckets;
            }
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            for (int i = 0; i < Events.Count; i++)
            {
                ChronicleEvent evt = Events[i];
                if (evt == null)
                {
                    continue;
                }
                long age = now - evt.Tick;
                if (age < 0L)
                {
                    continue;
                }
                int index = (int)(age / bucketTicks);
                if (index >= bucketCount)
                {
                    continue;
                }
                buckets[bucketCount - 1 - index]++;   // 段 0 = 最老
            }
            return buckets;
        }

        /// <summary>
        /// TypeKey → 事件类别缓存（Def 加载后不变 ⇒ 永久缓存；避免每次 <c>DefDatabase</c> 查找）。
        /// </summary>
        private static readonly Dictionary<string, ChronicleEventKind> EventKindCache =
            new Dictionary<string, ChronicleEventKind>(System.StringComparer.Ordinal);

        /// <summary>
        /// 按**事件语义类别**分桶计数（段 0 = 最老 → 段 n−1 = 最近），O(N) 单次遍历、零排序。
        ///
        /// <para>类别经 <see cref="ChronicleEventDef.kind"/> 判定 —— <b>不按 TypeKey 文本猜</b>（GOV-009）；
        /// TypeKey→kind 走静态缓存。用于「每季入档人数」这类按类别的历史序列
        /// （调用方自行累计可得到存量曲线）。</para>
        /// </summary>
        public int[] GetEventBucketsByKind(ChronicleEventKind kind, int bucketCount, long bucketTicks)
        {
            if (bucketCount <= 0 || bucketTicks <= 0L)
            {
                return new int[0];
            }
            int[] buckets = new int[bucketCount];
            if (Events == null || Events.Count == 0)
            {
                return buckets;
            }
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            for (int i = 0; i < Events.Count; i++)
            {
                ChronicleEvent evt = Events[i];
                if (evt == null || KindOf(evt) != kind)
                {
                    continue;
                }
                long age = now - evt.Tick;
                if (age < 0L)
                {
                    continue;
                }
                int index = (int)(age / bucketTicks);
                if (index >= bucketCount)
                {
                    continue;
                }
                buckets[bucketCount - 1 - index]++;
            }
            return buckets;
        }

        /// <summary>事件类别（经 Def 的 kind；Def 缺失 ⇒ Other，不猜文本）。</summary>
        private static ChronicleEventKind KindOf(ChronicleEvent evt)
        {
            string key = evt.TypeKey;
            if (string.IsNullOrEmpty(key))
            {
                return ChronicleEventKind.Other;
            }
            ChronicleEventKind cached;
            if (EventKindCache.TryGetValue(key, out cached))
            {
                return cached;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(key);
            ChronicleEventKind kind = def != null ? def.kind : ChronicleEventKind.Other;
            EventKindCache[key] = kind;
            return kind;
        }

        /// <summary>
        /// **分身份存量趋势**（按季回推，段 0 = 最老 → 段 n−1 = 最近）：从当前三态人数出发，
        /// **逆向撤销**带身份的事件（Join −1 / Leave +1 / RoleChanged：from+1、to−1），
        /// 每越过一个季度边界快照一次 ⇒ 得到「该季末的三态人数」。
        ///
        /// <para>⚠ 只认**带身份**的事件（``Params`` 含 role / from / to）；旧链路的 Join（无身份）
        /// 被跳过 —— 那段历史不可追溯（如实呈现，数据从本机制上线起积累）。O(E) 遍历（E = 带身份事件数）。</para>
        /// </summary>
        public int[][] GetIdentityTrends(int bucketCount, long bucketTicks, int[] currentByRole)
        {
            int[][] result = new int[3][];
            for (int r = 0; r < 3; r++)
            {
                result[r] = new int[bucketCount];
                int start = currentByRole != null && r < currentByRole.Length ? currentByRole[r] < 0 ? 0 : currentByRole[r] : 0;
                for (int b = 0; b < bucketCount; b++)
                {
                    result[r][b] = start;
                }
            }
            if (Events == null || Events.Count == 0 || bucketCount <= 0 || bucketTicks <= 0L)
            {
                return result;
            }
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;

            // 收集带身份事件（tick / 类别 / role / from），供逆序撤销。
            List<long> evTick = new List<long>();
            List<int> evKind = new List<int>();      // 0 = Join · 1 = Leave · 2 = RoleChanged
            List<int> evRole = new List<int>();      // Join/Leave 的 role；RoleChanged 的 from
            List<int> evTo = new List<int>();        // RoleChanged 的 to（其他为 -1）
            for (int i = 0; i < Events.Count; i++)
            {
                ChronicleEvent evt = Events[i];
                if (evt == null || evt.Params == null || evt.Params.Count == 0)
                {
                    continue;
                }
                ChronicleEventKind kind = KindOf(evt);
                int kindIndex;
                if (kind == ChronicleEventKind.Join)
                {
                    kindIndex = 0;
                }
                else if (kind == ChronicleEventKind.Leave)
                {
                    kindIndex = 1;
                }
                else if (kind == ChronicleEventKind.RoleChanged)
                {
                    kindIndex = 2;
                }
                else
                {
                    continue;
                }
                string roleText;
                if (!evt.Params.TryGetValue("role", out roleText)
                    && !evt.Params.TryGetValue("to", out roleText))
                {
                    continue;   // 无身份参数（旧链路 Join）⇒ 跳过（历史不可追溯，如实）
                }
                int role;
                if (!int.TryParse(roleText, out role) || role < 0 || role > 2)
                {
                    continue;
                }
                int from = -1;
                string fromText;
                if (kindIndex == 2 && evt.Params.TryGetValue("from", out fromText))
                {
                    int.TryParse(fromText, out from);
                }
                evTick.Add(evt.Tick);
                evKind.Add(kindIndex);
                evRole.Add(role);
                evTo.Add(from);
            }
            if (evTick.Count == 0)
            {
                return result;
            }

            // 逆序撤销：从最近往最老走；每跨过一个季度边界（age 越过 b×bucketTicks）冻结一次快照。
            int[] current = new int[3];
            for (int r = 0; r < 3; r++)
            {
                current[r] = currentByRole != null && r < currentByRole.Length ? currentByRole[r] < 0 ? 0 : currentByRole[r] : 0;
            }
            int bucketIndex = 0;   // 0 = 最近
            for (int e = evTick.Count - 1; e >= 0; e--)
            {
                long age = now - evTick[e];
                if (age < 0L)
                {
                    continue;
                }
                // 冻结：age 已越过「bucketIndex+1」个季度边界 ⇒ 当前状态即该季末人数
                while (bucketIndex < bucketCount - 1 && age >= (long)(bucketIndex + 1) * bucketTicks)
                {
                    for (int r = 0; r < 3; r++)
                    {
                        result[r][bucketCount - 1 - bucketIndex] = current[r];
                    }
                    bucketIndex++;
                }
                int k = evKind[e];
                int role = evRole[e];
                if (k == 0)          // Join：撤销 ⇒ 当时少一人
                {
                    current[role] = (current[role] - 1) < 0 ? 0 : (current[role] - 1);
                }
                else if (k == 1)     // Leave：撤销 ⇒ 当时多一人
                {
                    current[role]++;
                }
                else                  // RoleChanged：撤销 ⇒ from +1、to −1
                {
                    int to = evTo[e];
                    if (to >= 0 && to <= 2)
                    {
                        current[to] = (current[to] - 1) < 0 ? 0 : (current[to] - 1);
                    }
                    if (role >= 0 && role <= 2)
                    {
                        current[role]++;
                    }
                }
            }
            // 最老一段：全部事件撤销完后的状态
            for (int r = 0; r < 3; r++)
            {
                for (int b = bucketIndex; b < bucketCount; b++)
                {
                    result[r][bucketCount - 1 - b] = current[r];
                }
            }
            return result;
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

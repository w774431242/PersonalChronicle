using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 管网中的货物目的地（v0.2 §0.8 D3 定向输出）。
    /// 两种形态二选一：
    /// - <see cref="cell"/>：玩家指定的存储格子（原版/第三方存储通用——物品落格即入仓）；
    /// - <see cref="thing"/>：自家设备（实现 <see cref="IItemStorage"/> 的建筑，走 Push）。
    /// 以 string Key 作距离表缓存键；cell 走 Scribe Value，thing 走 Scribe Reference。
    /// </summary>
    public struct PipeDestination
    {
        private IntVec3 cell;
        private Thing thing;

        public static PipeDestination FromCell(IntVec3 c)
        {
            return new PipeDestination { cell = c, thing = null };
        }

        public static PipeDestination FromThing(Thing t)
        {
            return new PipeDestination { cell = IntVec3.Invalid, thing = t };
        }

        public IntVec3 Cell => cell;
        public Thing Thing => thing;
        public bool IsCell => thing == null && cell.IsValid;
        public bool IsThing => thing != null;
        public bool IsValid => IsCell || IsThing;

        /// <summary>距离表缓存键（同目的地同键）。</summary>
        public string Key => IsThing ? "t:" + thing.thingIDNumber : "c:" + cell.x + "," + cell.z;

        /// <summary>
        /// 该段是否为本目的地的「出口段」：段格与目标呈切比雪夫相邻
        /// （thing 目标贴其占格外扩一圈；cell 目标贴该格外扩一圈）。
        /// </summary>
        public bool IsExitFor(Building_PipeSegment seg)
        {
            if (seg == null || !seg.Spawned || seg.Map == null)
            {
                return false;
            }
            if (IsThing)
            {
                if (thing.Destroyed || !thing.Spawned || thing.Map != seg.Map)
                {
                    return false;
                }
                return thing.OccupiedRect().ExpandedBy(1).Contains(seg.Position);
            }
            if (IsCell)
            {
                return cell.InBounds(seg.Map) && seg.OccupiedRect().ExpandedBy(1).Contains(cell);
            }
            return false;
        }
    }

    /// <summary>
    /// 管网组件（v0.2 §0.8 D2 虚拟流）：登记全图管道段，维护「段 → 到目的地距离」的
    /// 惰性 BFS 缓存，供段间路由与端口反压判定。
    ///
    /// 设计纪律：
    /// - 管网拓扑不持久化：段各自在 Spawn/DeSpawn 时注册注销，组件只存运行态；
    /// - 距离表按目的地惰性构建、管网版本号失效（增删段 version++）；
    /// - 距离 0 = 出口段（切比雪夫贴目标），段只向「距离-1」的邻段推进 → 天然向出口汇聚。
    /// </summary>
    public class MapComponent_PipeNetwork : MapComponent
    {
        private const string LogPrefix = "PipeNet";

        private readonly List<Building_PipeSegment> segments = new List<Building_PipeSegment>();
        private readonly Dictionary<IntVec3, Building_PipeSegment> index = new Dictionary<IntVec3, Building_PipeSegment>();
        private readonly List<CompItemPort> ports = new List<CompItemPort>();

        private int version;
        private int cacheVersion = -1;
        private readonly Dictionary<string, Dictionary<Building_PipeSegment, int>> distanceCache
            = new Dictionary<string, Dictionary<Building_PipeSegment, int>>();

        public MapComponent_PipeNetwork(Map map) : base(map)
        {
        }

        public static MapComponent_PipeNetwork Get(Map map)
        {
            MapComponent_PipeNetwork comp = map.GetComponent<MapComponent_PipeNetwork>();
            if (comp == null)
            {
                comp = new MapComponent_PipeNetwork(map);
                map.components.Add(comp);
            }
            return comp;
        }

        /// <summary>当前管网段数（检视/调试用）。</summary>
        public int SegmentCount => segments.Count;

        /// <summary>已注册端口（段判定 In/Out 视觉用，只读视图）。</summary>
        public IReadOnlyList<CompItemPort> Ports => ports;

        /// <summary>全部管道段（只读视图）：拆除设计器激活时高亮所有段（含隐藏式）。</summary>
        public IReadOnlyList<Building_PipeSegment> AllSegments => segments;

        /// <summary>拓扑/端口版本号（段的动态贴图缓存据此失效）。</summary>
        public int Version => version;

        public Building_PipeSegment SegmentAt(IntVec3 cell)
        {
            return index.TryGetValue(cell, out Building_PipeSegment seg) ? seg : null;
        }

        public void Register(Building_PipeSegment seg)
        {
            if (seg == null || index.ContainsKey(seg.Position))
            {
                return;
            }
            segments.Add(seg);
            index.Add(seg.Position, seg);
            version++;
            DirtyAllSegments();
            ChronicleLog.Info(ChronicleLog.Category.Automation,
                LogPrefix + " register @" + seg.Position + " total=" + segments.Count);
        }

        public void Unregister(Building_PipeSegment seg)
        {
            if (seg == null)
            {
                return;
            }
            if (segments.Remove(seg))
            {
                index.Remove(seg.Position);
                version++;
                DirtyAllSegments();
            }
        }

        // ---- 端口注册（In/Out 视觉判定 + 绑定变化传播）------------------------

        public void RegisterPort(CompItemPort port)
        {
            if (port != null && !ports.Contains(port))
            {
                ports.Add(port);
                version++;
                DirtyAllSegments();
            }
        }

        public void UnregisterPort(CompItemPort port)
        {
            if (port != null && ports.Remove(port))
            {
                version++;
                DirtyAllSegments();
            }
        }

        /// <summary>端口绑定目标变化（绑定/解绑/换目标）：全段视觉失效重绘。</summary>
        public void NotifyPortsChanged()
        {
            version++;
            DirtyAllSegments();
        }

        /// <summary>全段 MapMesh 重绘（段的 Graphic getter 惰性重算，见 Building_PipeSegment）。</summary>
        private void DirtyAllSegments()
        {
            if (map == null || map.mapDrawer == null)
            {
                return;
            }
            for (int i = 0; i < segments.Count; i++)
            {
                map.mapDrawer.MapMeshDirty(segments[i].Position, MapMeshFlagDefOf.Things);
            }
        }

        /// <summary>
        /// 取「段 → 到目的地距离」表（0 = 出口段；不在表内 = 不可达）。
        /// 惰性构建 + 版本失效：管网变化后的第一次查询重建，稳态零开销。
        /// </summary>
        public bool TryGetDistance(Building_PipeSegment from, PipeDestination dest, out int dist)
        {
            dist = int.MaxValue;
            if (from == null || !dest.IsValid || segments.Count == 0)
            {
                return false;
            }
            if (cacheVersion != version)
            {
                distanceCache.Clear();
                cacheVersion = version;
            }
            if (!distanceCache.TryGetValue(dest.Key, out Dictionary<Building_PipeSegment, int> table))
            {
                table = BuildDistances(dest);
                distanceCache.Add(dest.Key, table);
            }
            return table.TryGetValue(from, out dist);
        }

        /// <summary>BFS：从全部出口段（距离 0）沿 4 向邻段扩散。</summary>
        private Dictionary<Building_PipeSegment, int> BuildDistances(PipeDestination dest)
        {
            Dictionary<Building_PipeSegment, int> table = new Dictionary<Building_PipeSegment, int>();
            Queue<Building_PipeSegment> queue = new Queue<Building_PipeSegment>();
            for (int i = 0; i < segments.Count; i++)
            {
                Building_PipeSegment seg = segments[i];
                if (dest.IsExitFor(seg))
                {
                    table[seg] = 0;
                    queue.Enqueue(seg);
                }
            }
            while (queue.Count > 0)
            {
                Building_PipeSegment cur = queue.Dequeue();
                int d = table[cur];
                List<Building_PipeSegment> neighbors = cur.Neighbors4();
                for (int i = 0; i < neighbors.Count; i++)
                {
                    Building_PipeSegment n = neighbors[i];
                    if (!table.ContainsKey(n))
                    {
                        table[n] = d + 1;
                        queue.Enqueue(n);
                    }
                }
            }
            return table;
        }
    }
}

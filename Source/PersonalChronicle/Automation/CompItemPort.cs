using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>物品输出端口参数（Def 级）。</summary>
    public class CompProperties_ItemPort : CompProperties
    {
        /// <summary>取货推送节拍（tick，TickInterval 累加配额）。</summary>
        public int pushIntervalTicks = 90;

        public CompProperties_ItemPort()
        {
            compClass = typeof(CompItemPort);
        }
    }

    /// <summary>
    /// 物品输出端口（v0.2 §0.8 D1/D3）：挂在任何实现 <see cref="IItemStorage"/> 的自动化设备上
    /// （农机三档已接入），设备储物仓即具备「输出端」能力——配合管道即可把物资定向送出，
    /// **无需任何独立交接件建筑**。
    ///
    /// 玩家交互：
    /// - gizmo「设定输出目标…」→ 原版 Targeter 点选：点自家设备（IItemStorage）绑设备走 Push；
    ///   点格子绑「定向存储格」——该格过滤器收什么就送什么（零兼容原理，物品落格即入仓）。
    /// - gizmo「解除输出目标」；检视面板显示目标与管网状态。
    ///
    /// 流动纪律（与管道段一致）：
    /// - 每拍最多取一批（≤ def.stackLimit），推入切比雪夫相邻且可接收的管道段；
    /// - cell 目标先用 1 件探针 <c>StoreUtility.IsValidStorageFor</c> 预检（过滤器不收 → 不取，反压）；
    /// - 管网不可达/邻段满 → 不取货（源仓继续缓冲，语义诚实零丢失）；
    /// - 绑定持久化：cell 走 Scribe Value、设备走 Scribe Reference。
    /// </summary>
    public class CompItemPort : ThingComp
    {
        private const string LogPrefix = "ItemPort";

        private int pushQuota;
        private bool targetIsCell;
        private IntVec3 targetCell = IntVec3.Invalid;
        private Thing targetThing; // ⚠️ Scribe_References 需可写引用。

        /// <summary>输出阈值（件）：仓储总量达到该值才开始向管道输出（0 = 有货即送）。
        /// PM 2026-09-07：产物一进仓就被管道吸走、输送过于零碎——攒批输出。</summary>
        private int outputThreshold;

        private Command_Action manageGizmo;

        // 性能体检 P0-4：运行门控 comp 与相邻段列表缓存（见 PostSpawnSetup / DeviceRunning）。
        private CompPowerTrader powerComp;
        private CompRefuelable fuelComp;
        private CompFlickable flickComp;
        private List<Building_PipeSegment> adjacentSegments;
        private int adjacentSegmentsVersion = -1;

        public CompProperties_ItemPort Props => (CompProperties_ItemPort)props;

        /// <summary>输出阈值（件）。</summary>
        public int OutputThreshold => outputThreshold;

        /// <summary>设定输出阈值（管理窗调用，立即生效并随下次保存持久化）。</summary>
        public void SetOutputThreshold(int value)
        {
            outputThreshold = Mathf.Clamp(value, 0, 9999);
        }

        /// <summary>是否已绑定输出目标。</summary>
        public bool HasTarget => targetIsCell ? targetCell.IsValid : targetThing != null;

        /// <summary>绑定目标格（管道段判定 Out 视觉用；无效时无效格）。</summary>
        public IntVec3 BoundCell => targetIsCell ? targetCell : IntVec3.Invalid;

        /// <summary>绑定目标设备（管道段判定 Out 视觉用；无绑定时 null）。</summary>
        public Thing BoundThing => targetIsCell ? null : targetThing;

        /// <summary>目标描述（检视/日志用）。</summary>
        public string TargetDescription
        {
            get
            {
                if (targetIsCell)
                {
                    return "(" + targetCell.x + "," + targetCell.z + ")";
                }
                return targetThing != null && !targetThing.Destroyed ? targetThing.LabelShortCap : "…";
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            // ⭐ PM 2026-09-07 修读档清空：Scribe_Values 默认对等于 defaultValue 的字段跳过写键
            // （targetIsCell=false 与 targetCell=Invalid 都会跳过），Scribe_References 默认
            // 对目标已毁引用跳过写键——三个目标字段全部 forceSave + saveDestroyedThings=true，
            // 避免保存时因 default 值或瞬时 Destroyed 状态丢键导致读档后清空。
            Scribe_Values.Look(ref targetIsCell, "portTargetIsCell", false, forceSave: true);
            Scribe_Values.Look(ref targetCell, "portTargetCell", IntVec3.Invalid, forceSave: true);
            Scribe_References.Look(ref targetThing, "portTargetThing", saveDestroyedThings: true);
            Scribe_Values.Look(ref outputThreshold, "portOutputThreshold", 0, forceSave: true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (!targetIsCell && targetThing != null && targetThing.Destroyed)
                {
                    ChronicleLog.Info(ChronicleLog.Category.Automation,
                        "ItemPort load: target thing destroyed, target cleared (will need re-bind).");
                    targetThing = null;
                }
                if (targetIsCell && !targetCell.IsValid)
                {
                    ChronicleLog.Info(ChronicleLog.Category.Automation,
                        "ItemPort load: cell target invalid, target cleared (will need re-bind).");
                    targetIsCell = false;
                }
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            // 性能体检 P0-4：parent 上的 comp 集合 Spawn 后固定不变，缓存一次即可。
            powerComp = parent.TryGetComp<CompPowerTrader>();
            fuelComp = parent.TryGetComp<CompRefuelable>();
            flickComp = parent.TryGetComp<CompFlickable>();
            adjacentSegmentsVersion = -1;
            if (parent.Map != null)
            {
                MapComponent_PipeNetwork.Get(parent.Map).RegisterPort(this);
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            if (map != null)
            {
                MapComponent_PipeNetwork.Get(map).UnregisterPort(this);
            }
            base.PostDeSpawn(map, mode);
        }

        public override void CompTickInterval(int interval)
        {
            base.CompTickInterval(interval);
            if (!parent.Spawned || parent.Map == null)
            {
                return;
            }
            pushQuota += interval;
            if (pushQuota < Props.pushIntervalTicks)
            {
                return;
            }
            pushQuota = 0;
            TryPushLot();
        }

        /// <summary>取一批货推入相邻管道段（一拍一批；任何一环不通即整体不取，反压）。</summary>
        private void TryPushLot()
        {
            if (!HasTarget || parent.Faction != Faction.OfPlayer)
            {
                return;
            }
            IItemStorage source = ItemStorageUtility.GetStorage(parent);
            if (source == null || source.StoredTotal <= 0)
            {
                return;
            }
            // ⭐ 输出阈值门控（PM 2026-09-07：输送频率太高，要求攒批输出）——
            // 仓内总量未达阈值不取货；达到后自然推至阈值下方，再攒再推（天然滞回）。
            // 0 = 有货即送（旧行为）。
            if (source.StoredTotal < outputThreshold)
            {
                return;
            }
            // 运行门控三连（与农机一致：通电 ∧ 燃料 ∧ 开关）；没有对应 comp 视为通过。
            if (!DeviceRunning())
            {
                return;
            }
            List<Building_PipeSegment> segs = FindAdjacentSegments();
            if (segs.Count == 0)
            {
                return; // 未接管道：检视面板有提示。
            }
            Map map = parent.Map;
            MapComponent_PipeNetwork net = MapComponent_PipeNetwork.Get(map);
            PipeDestination dest = CurrentDestination();
            if (!dest.IsValid)
            {
                return;
            }
            foreach (ThingDefCountClass batch in source.Buffer)
            {
                if (batch == null || batch.thingDef == null || batch.count <= 0)
                {
                    continue;
                }
                ThingDef def = batch.thingDef;
                int take = Mathf.Min(batch.count, def.stackLimit);
                if (take <= 0)
                {
                    continue;
                }
                for (int i = 0; i < segs.Count; i++)
                {
                    Building_PipeSegment seg = segs[i];
                    // 管网不可达（该段到目标无路）：换下一段。
                    if (!net.TryGetDistance(seg, dest, out _))
                    {
                        continue;
                    }
                    if (!seg.CanAcceptLot(def, take, dest))
                    {
                        continue;
                    }
                    if (targetIsCell)
                    {
                        // 1 件探针预检：目标格过滤器不收这类物品 → 换下一类（不凭空制造例外）。
                        if (!CellAccepts(targetCell, def, map))
                        {
                            continue;
                        }
                    }
                    else if (!ThingAccepts(targetThing, def, map))
                    {
                        continue;
                    }
                    Thing pulled = source.TakeOut(def, take);
                    if (pulled == null || pulled.stackCount <= 0)
                    {
                        continue;
                    }
                    seg.StoreLot(def, pulled.stackCount, dest);
                    // 虚拟流只取数量，实体不再使用：必须显式销毁，否则每拍留一个
                    // 未 Spawn 未 Destroy 的悬空 Thing（带副作用的第三方 thingClass 可能被触发）。
                    int pushedCount = pulled.stackCount; // Destroy 后 stackCount 归零，日志须先记
                    pulled.Destroy(DestroyMode.Vanish);
                    // CHG-20261004-PERF（2026-10-04 性能审计 §0.3）：此日志原为**零门控** ——
                    // 每端口每 90 tick 一行 ⇒ 10 个端口的基地在 3 倍速下可达 ~20 行/秒，
                    // 每行一次同步 IO 写 Player.log，是车间体系单位时间成本最高的一项，
                    // 且完全不影响玩法语义。
                    // ⚠ key 刻意用**全局字面量**：`ChronicleLog.Throttled` 的键表永不清理，
                    //   传端口标识（动态键）会让键数随拆建端口无界增长。
                    //   代价是日志不再逐端口区分 —— 逐端口排查时把间隔改成 0 即恢复原行为。
                    const int TransportLogThrottleTicks = 600;   // 每类日志 10 游戏秒最多一行
                    if (ChronicleLog.Throttled("ItemPort.pushed", TransportLogThrottleTicks))
                    {
                        ChronicleLog.Info(ChronicleLog.Category.Automation,
                            LogPrefix + " pushed " + def.defName + " x" + pushedCount
                            + " from " + parent.LabelShortCap + " to pipe @" + seg.Position
                            + " dest=" + TargetDescription);
                    }
                    return; // 一拍一批。
                }
            }
        }

        /// <summary>
        /// 目标格能否接收该类物品（1 件探针问原版过滤/仓储组规则）。
        /// ⭐ 只问过滤器、不判「堆满」（PM 2026-09-08 深度诊断）：1.6 原版建筑字段
        /// <c>maxItemsInCell</c> 允许一格叠多堆（如 Adaptive Storage 地窖 25/55 组
        /// 全在同一格上），「堆到 stackLimit」≠「收不下」——上一版在这里按
        /// stackLimit 判满，把「1/25 组还有空位」的地窖直接判死停取。
        /// 「整批装得下」由交付端 TryDeliverToCell 的原版 Direct 放置保证。
        /// </summary>
        private static bool CellAccepts(IntVec3 cell, ThingDef def, Map map)
        {
            if (!cell.IsValid || !cell.InBounds(map) || def == null)
            {
                return false;
            }
            Thing probe = ThingMaker.MakeThing(def);
            probe.stackCount = 1;
            return StoreUtility.IsValidStorageFor(cell, map, probe);
        }

        /// <summary>
        /// 建筑目标能否接收该类物品：自家设备（IItemStorage）问余量；
        /// 原版货架/第三方存储建筑逐占格问原版规则（多格深存储任一格可收即可，反压只在全军覆没时触发）。
        /// </summary>
        private static bool ThingAccepts(Thing thing, ThingDef def, Map map)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map != map || def == null)
            {
                return false;
            }
            IItemStorage sink = ItemStorageUtility.GetStorage(thing);
            if (sink != null)
            {
                return sink.AcceptQuantity(def, 1) > 0;
            }
            foreach (IntVec3 cell in thing.OccupiedRect().Cells)
            {
                if (CellAccepts(cell, def, map))
                {
                    return true;
                }
            }
            return false;
        }

        private bool DeviceRunning()
        {
            // 性能体检 P0-4：comp 引用在 Spawn 时缓存一次（comp 集合 Spawn 后固定不变），
            // 原本每次调用 3 次 TryGetComp 遍历——而本方法还在检视面板的每帧路径上。
            if (powerComp != null && !powerComp.PowerOn)
            {
                return false;
            }
            if (fuelComp != null && !fuelComp.HasFuel)
            {
                return false;
            }
            if (flickComp != null && !flickComp.SwitchIsOn)
            {
                return false;
            }
            return true;
        }

        /// <summary>切比雪夫相邻（覆盖多格设备外扩一圈）的全部管道段（推送与 In 视觉一致）。</summary>
        private List<Building_PipeSegment> FindAdjacentSegments()
        {
            List<Building_PipeSegment> result = new List<Building_PipeSegment>();
            Map map = parent.Map;
            if (map == null)
            {
                return result;
            }
            MapComponent_PipeNetwork net = MapComponent_PipeNetwork.Get(map);
            // 性能体检 P0-4：检视面板每帧调用，原本每次 new List + 逐格查表。
            // 管网版本号在段增删/端口绑定变化时 ++，据此失效即可（设备不会自己移动）。
            if (adjacentSegments != null && adjacentSegmentsVersion == net.Version)
            {
                return adjacentSegments;
            }
            adjacentSegments = result;
            adjacentSegmentsVersion = net.Version;
            CellRect own = parent.OccupiedRect();
            CellRect band = own.ExpandedBy(1);
            foreach (IntVec3 cell in band.Cells)
            {
                if (own.Contains(cell))
                {
                    continue;
                }
                Building_PipeSegment seg = net.SegmentAt(cell);
                if (seg != null && !result.Contains(seg))
                {
                    result.Add(seg);
                }
            }
            return result;
        }

        /// <summary>段位是否贴着本端口设备（贴设备 → 该段显示 In 蓝口朝设备）。</summary>
        public bool FacesParentFrom(IntVec3 pos, out Rot4 facing)
        {
            facing = default;
            if (!parent.Spawned || parent.Map == null)
            {
                return false;
            }
            CellRect own = parent.OccupiedRect();
            if (!own.ExpandedBy(1).Contains(pos))
            {
                return false;
            }
            facing = FacingToward(pos, ClampToRect(pos, own));
            return true;
        }

        /// <summary>段位是否贴着本端口绑定的目标（贴目标 → 该段显示 Out 橙口朝目标）。</summary>
        public bool FacesTargetFrom(IntVec3 pos, out Rot4 facing)
        {
            facing = default;
            if (!parent.Spawned || parent.Map == null)
            {
                return false;
            }
            if (targetIsCell)
            {
                if (!targetCell.IsValid)
                {
                    return false;
                }
                int dx = Math.Abs(targetCell.x - pos.x);
                int dz = Math.Abs(targetCell.z - pos.z);
                if (dx > 1 || dz > 1 || dx + dz == 0)
                {
                    return false;
                }
                facing = FacingToward(pos, targetCell);
                return true;
            }
            if (targetThing != null && !targetThing.Destroyed && targetThing.Spawned
                && targetThing.Map == parent.Map)
            {
                CellRect r = targetThing.OccupiedRect();
                if (!r.ExpandedBy(1).Contains(pos))
                {
                    return false;
                }
                facing = FacingToward(pos, ClampToRect(pos, r));
                return true;
            }
            return false;
        }

        /// <summary>取 pos 指向 to 的主朝向（|dx|≥|dz| 取东西，否则南北）。</summary>
        private static Rot4 FacingToward(IntVec3 pos, IntVec3 to)
        {
            int dx = to.x - pos.x;
            int dz = to.z - pos.z;
            if (Math.Abs(dx) >= Math.Abs(dz))
            {
                return dx >= 0 ? Rot4.East : Rot4.West;
            }
            return dz > 0 ? Rot4.North : Rot4.South;
        }

        /// <summary>把 pos 收夹进 rect（最近边缘点；段必在 rect 外，方向即段→目标）。</summary>
        private static IntVec3 ClampToRect(IntVec3 pos, CellRect rect)
        {
            return new IntVec3(
                Math.Min(Math.Max(pos.x, rect.minX), rect.maxX),
                0,
                Math.Min(Math.Max(pos.z, rect.minZ), rect.maxZ));
        }

        private PipeDestination CurrentDestination()
        {
            if (targetIsCell)
            {
                return targetCell.IsValid && targetCell.InBounds(parent.Map)
                    ? PipeDestination.FromCell(targetCell)
                    : default;
            }
            // 跨地图保护：目标被搬运/传送到别的地图后，按同坐标判定会误投递到他图。
            if (targetThing != null && !targetThing.Destroyed && targetThing.Spawned
                && targetThing.Map == parent.Map)
            {
                return PipeDestination.FromThing(targetThing);
            }
            return default;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // ⭐ 三键合一（PM 2026-09-07）：「设定/解除输出目标 + 输出阈值」统一进
            // 「物流管理」弹窗，设备选中后只保留一个物流互动键。
            if (manageGizmo == null)
            {
                manageGizmo = new Command_Action
                {
                    defaultDesc = Tr("PersonalChronicle.Automation.Port.ManageDesc"),
                    icon = ContentFinder<Texture2D>.Get("UI/Buttons/Outfit", false) ?? TexButton.OpenStatsReport,
                    action = () => Find.WindowStack.Add(new Dialog_LogisticsManage(parent, this))
                };
            }
            manageGizmo.defaultLabel = Tr("PersonalChronicle.Automation.Port.Manage");
            yield return manageGizmo;
        }

        /// <summary>原版 Targeter 点选绑定（物流管理窗调用）：点设备（IItemStorage）绑设备，点格子绑定向存储格。</summary>
        internal void BeginBinding()
        {
            TargetingParameters tp = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetBuildings = true,
                canTargetPawns = false,
                canTargetItems = false
            };
            Map map = parent.Map;
            Find.Targeter.BeginTargeting(tp, (Action<LocalTargetInfo>)(t =>
            {
                bool changed = false;
                if (t.HasThing)
                {
                    bool hasIface = ItemStorageUtility.GetStorage(t.Thing) != null;
                    // 排除自身：绑到本机等于 TakeOut→管道→Push 自循环，物品在管网里空转。
                    if (t.Thing == parent)
                    {
                        Messages.Message(Tr("PersonalChronicle.Automation.Port.BindRejected", t.Thing.LabelShortCap),
                            new LookTargets(t.Thing), MessageTypeDefOf.RejectInput, false);
                    }
                    else if (hasIface || t.Thing is IStoreSettingsParent)
                    {
                        // 统一绑定为「建筑目标」：自家设备走 Push；原版货架/第三方存储建筑
                        // 交付时落到其占格内任一可收格子（PM 实测教训：只绑 Position 单格会
                        // 漏掉多格深存储的其余格，管道贴着建筑却永远推不出去）。
                        targetIsCell = false;
                        targetThing = t.Thing;
                        targetCell = IntVec3.Invalid;
                        changed = true;
                        ChronicleLog.Info(ChronicleLog.Category.Automation,
                            LogPrefix + " bound " + parent.LabelShortCap + " -> "
                            + (hasIface ? "device " : "storage building ") + t.Thing.LabelShortCap);
                    }
                    else
                    {
                        // 非存储目标：拒绝并给反馈（曾静默忽略，玩家以为绑上而状态永不变化）。
                        Messages.Message(Tr("PersonalChronicle.Automation.Port.BindRejected", t.Thing.LabelShortCap),
                            new LookTargets(t.Thing), MessageTypeDefOf.RejectInput, false);
                    }
                }
                else
                {
                    targetIsCell = true;
                    targetCell = t.Cell;
                    targetThing = null;
                    changed = true;
                    ChronicleLog.Info(ChronicleLog.Category.Automation,
                        LogPrefix + " bound " + parent.LabelShortCap + " -> cell " + targetCell);
                }
                if (changed && parent.Spawned && parent.Map != null)
                {
                    MapComponent_PipeNetwork.Get(parent.Map).NotifyPortsChanged();
                }
            }));
        }

        internal void Unbind()
        {
            targetIsCell = false;
            targetCell = IntVec3.Invalid;
            targetThing = null;
            if (parent.Spawned && parent.Map != null)
            {
                MapComponent_PipeNetwork.Get(parent.Map).NotifyPortsChanged();
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!HasTarget)
            {
                return Tr("PersonalChronicle.Automation.Port.Inspect.None");
            }
            string text = Tr("PersonalChronicle.Automation.Port.Inspect.Target", TargetDescription);
            // 静默门控逐条明示（推送与农机同门控：通电∧燃料∧开关，缺一即整拍不取货）。
            if (!DeviceRunning())
            {
                return text + "\n" + Tr("PersonalChronicle.Automation.Port.Inspect.Stopped");
            }
            IItemStorage source = ItemStorageUtility.GetStorage(parent);
            if (source == null || source.StoredTotal <= 0)
            {
                return text + "\n" + Tr("PersonalChronicle.Automation.Port.Inspect.Empty");
            }
            // 阈值状态（PM 2026-09-07）：未达线时明示「攒批中」，避免玩家以为推送坏了。
            if (source.StoredTotal < outputThreshold)
            {
                return text + "\n" + Tr("PersonalChronicle.Automation.Port.Inspect.BelowThreshold",
                    source.StoredTotal, outputThreshold);
            }
            List<Building_PipeSegment> segs = FindAdjacentSegments();
            if (segs.Count == 0)
            {
                return text + "\n" + Tr("PersonalChronicle.Automation.Port.Inspect.NoPipe");
            }
            PipeDestination dest = CurrentDestination();
            if (dest.IsValid && !NetReaches(segs, dest))
            {
                // 已绑目标且设备旁有管道，但管网没铺到目标旁：端口永远取不了货，必须明示。
                text += "\n" + Tr("PersonalChronicle.Automation.Port.Inspect.Unreachable");
                return text;
            }
            // 管网已连通但目标不收缓冲内的物品（过滤器/占格全满）：曾全程静默，
            // 玩家只见仓满不动无从排查，必须明示（PM 实测教训）。
            foreach (ThingDefCountClass batch in source.Buffer)
            {
                if (batch?.thingDef == null || batch.count <= 0)
                {
                    continue;
                }
                bool ok = targetIsCell
                    ? CellAccepts(targetCell, batch.thingDef, parent.Map)
                    : ThingAccepts(targetThing, batch.thingDef, parent.Map);
                if (!ok)
                {
                    text += "\n" + Tr("PersonalChronicle.Automation.Port.Inspect.Rejected", batch.thingDef.label);
                    break;
                }
            }
            return text;
        }

        /// <summary>任一相邻段沿管网可到达目标（出口段贴目标，距离 0）即连通；
        /// BFS 距离表按管网版本惰性缓存，检视每帧查询零重算。</summary>
        private bool NetReaches(List<Building_PipeSegment> segs, PipeDestination dest)
        {
            MapComponent_PipeNetwork net = parent.Map != null ? MapComponent_PipeNetwork.Get(parent.Map) : null;
            if (net == null)
            {
                return false;
            }
            for (int i = 0; i < segs.Count; i++)
            {
                if (net.TryGetDistance(segs[i], dest, out _))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>绑定目标高亮（选中设备时由建筑薄壳调用）：格目标画该格，建筑目标画其占格。</summary>
        public void DrawTargetOverlay()
        {
            if (!parent.Spawned || parent.Map == null)
            {
                return;
            }
            if (targetIsCell && targetCell.IsValid)
            {
                GenDraw.DrawFieldEdges(new List<IntVec3> { targetCell });
            }
            else if (!targetIsCell && targetThing != null && targetThing.Spawned
                && targetThing.Map == parent.Map)
            {
                GenDraw.DrawFieldEdges(new List<IntVec3>(targetThing.OccupiedRect().Cells));
            }
        }

        /// <summary>取翻译并按 {0} 占位符回填（避开 1.6 已过时的 Translate(params object[])）。</summary>
        private static TaggedString Tr(string key, params object[] args)
        {
            string raw = key.Translate().ToString();
            return args.Length == 0 ? (TaggedString)raw : string.Format(raw, args);
        }
    }
}

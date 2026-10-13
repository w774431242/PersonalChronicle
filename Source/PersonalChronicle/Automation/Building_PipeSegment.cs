using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 管道段（虚拟流，v0.2 §0.8 D2/D4）。
    ///
    /// ⭐ PM 2026-09-07 定案「借用原生电缆」：视觉/连接/电力全部复用原版 Conduit 体系
    /// （Def 引用 <c>PowerConduit_Atlas</c> + <c>linkType=Transmitter</c> +
    /// <c>linkFlags=PowerConduit</c>，引擎自动按相邻连接物选 Atlas 帧，拖拽 ghost 同走
    /// 原版 DrawGhostThing），本类不再有任何贴图代码——自研 16 掩码动态切图、In/Out
    /// 端口图与 <c>Patch_GhostPipeGraphic</c> 已删除。电力传导由 Def 的
    /// <c>CompPowerTransmitter</c> 承载（管道=电缆，设备相邻管道自动并网）。
    ///
    /// 物流契约（不变）：
    /// - 货物在管网内以「批次」流动：<see cref="cargoDef"/> + <see cref="cargoCount"/> +
    ///   目的地（<see cref="destCell"/> 或 <see cref="destThing"/>），**不生成实体 Thing**；
    ///   只有出口段把批次物化（<see cref="ThingMaker.MakeThing"/>）落到定向目标。
    /// - 路由：每拍向「到目的地距离-1」的 4 向邻段推进（距离表由
    ///   <see cref="MapComponent_PipeNetwork"/> 惰性 BFS 维护，0 = 出口段）→ 天然向出口汇聚。
    /// - 反压：邻段满/不同货/不可达 → 原地滞留，绝不丢弃；出口拒绝（目标仓满/过滤不收）
    ///   → 滞留在出口段，反压逐级传导回端口（端口见邻段满即不再取货）。
    /// - 每段单批次（≤ def.stackLimit）：多批货自然排队用多段，语义简单确定。
    /// - 持久化：cargoDef/destThing 走 Reference，cargoCount/destCell 走 Value；
    ///   管网拓扑不持久化（Spawn 重建）。
    /// </summary>
    public class Building_PipeSegment : Building
    {
        /// <summary>流动节拍：按 TickInterval 累加的配额（1.6 自适应 tick 口径）。</summary>
        public const int FlowIntervalTicks = 90;

        private const string LogPrefix = "PipeSeg";

        private int flowQuota;

        /// <summary>管网组件引用（Spawn 时缓存一次，避免每帧 GetComponent 扫描）。</summary>
        private MapComponent_PipeNetwork netCache;

        // ---- 批次货物（单批次，虚拟流）----------------------------------------
        // ⚠️ Scribe_Collections/Look 需可写引用，字段不能 readonly。
        private ThingDef cargoDef;
        private int cargoCount;
        private IntVec3 destCell = IntVec3.Invalid;
        private Thing destThing;

        public string DeviceLabel => def.label;

        /// <summary>段内是否有货。</summary>
        public bool HasCargo => cargoDef != null && cargoCount > 0;

        /// <summary>段内货物件数（检视用）。</summary>
        public int CargoCount => cargoCount;

        /// <summary>段内货物名（检视用；空段返回 null）。</summary>
        public string CargoLabel => cargoDef != null ? cargoDef.label : null;

        /// <summary>
        /// 本段是否已接管网（Spawn 后注册）。
        /// 性能体检 P0-4：原本每次走 <c>map.GetComponent</c> 线性扫描，改为 Spawn 时缓存引用。
        /// </summary>
        private MapComponent_PipeNetwork Net => Spawned && Map != null ? netCache : null;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            netCache = MapComponent_PipeNetwork.Get(map);
            netCache.Register(this);
        }

        public override void DeSpawn(DestroyMode mode)
        {
            Map oldMap = Map;
            if (oldMap != null)
            {
                MapComponent_PipeNetwork.Get(oldMap).Unregister(this);
            }
            DropCargoBeforeRemoval();
            base.DeSpawn(mode);
        }

        /// <summary>拆除/销毁/搬运前物化管内货物（零丢失铁律：虚拟批次不许随建筑消失；
        /// 必须在 base.DeSpawn 之前调用——之后 Position/Map 即失效）。</summary>
        private void DropCargoBeforeRemoval()
        {
            if (!HasCargo)
            {
                return;
            }
            Thing lot = MakeLotThing();
            ClearCargo();
            if (Map != null && GenPlace.TryPlaceThing(lot, Position, Map, ThingPlaceMode.Near))
            {
                ChronicleLog.Info(ChronicleLog.Category.Automation,
                    LogPrefix + " dropped " + lot.def.defName + " x" + lot.stackCount + " on removal @" + Position);
                return;
            }
            lot.Destroy(); // 落地失败兜底（产线铁律④），与 CompItemStorage.DropAll 同纪律。
        }

        /// <summary>检视：管内货物与去向；目的地失效时明示滞留与回收途径。</summary>
        public override string GetInspectString()
        {
            string inherited = base.GetInspectString();
            if (!HasCargo)
            {
                return inherited;
            }
            PipeDestination dest = CurrentDestination();
            string destText;
            if (dest.IsThing)
            {
                destText = dest.Thing.LabelShortCap;
            }
            else if (dest.Cell.IsValid)
            {
                destText = "(" + dest.Cell.x + "," + dest.Cell.z + ")";
            }
            else
            {
                destText = "?";
            }
            string text = (inherited.NullOrEmpty() ? "" : inherited + "\n")
                + Tr("PersonalChronicle.Automation.Pipe.Inspect.Cargo", CargoLabel, CargoCount.ToString(), destText);
            if (!dest.IsValid)
            {
                text += "\n" + Tr("PersonalChronicle.Automation.Pipe.Inspect.DestLost");
            }
            return text;
        }

        /// <summary>取翻译并按 {0} 占位符回填（避开 1.6 已过时的 Translate(params object[])）。</summary>
        private static string Tr(string key, params object[] args)
        {
            string raw = key.Translate();
            return args.Length == 0 ? raw : string.Format(raw, args);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref cargoDef, "pipeCargoDef");
            Scribe_Values.Look(ref cargoCount, "pipeCargoCount", 0);
            Scribe_Values.Look(ref destCell, "pipeDestCell", IntVec3.Invalid);
            Scribe_References.Look(ref destThing, "pipeDestThing");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && cargoCount <= 0)
            {
                cargoDef = null;
            }
        }

        protected override void TickInterval(int interval)
        {
            base.TickInterval(interval);
            if (!Spawned || Map == null)
            {
                return;
            }
            flowQuota += interval;
            if (flowQuota < FlowIntervalTicks)
            {
                return;
            }
            flowQuota = 0;
            TryFlowCargo();
        }

        // ---- 端口侧入口 ------------------------------------------------------

        /// <summary>能否接收一批指定数量的货物（空段收任何货；同货同目的地可续量）。</summary>
        public bool CanAcceptLot(ThingDef def, int count, PipeDestination dest)
        {
            if (def == null || count <= 0 || !dest.IsValid)
            {
                return false;
            }
            if (!HasCargo)
            {
                return count <= def.stackLimit;
            }
            return cargoDef == def && SameDest(dest) && cargoCount + count <= def.stackLimit;
        }

        /// <summary>接收一批货物（调用前须 <see cref="CanAcceptLot"/> 通过）。</summary>
        public void StoreLot(ThingDef def, int count, PipeDestination dest)
        {
            if (!HasCargo)
            {
                cargoDef = def;
                cargoCount = count;
                destCell = dest.Cell;
                destThing = dest.Thing;
                return;
            }
            cargoCount += count;
        }

        // ---- 流动 ------------------------------------------------------------

        private void TryFlowCargo()
        {
            if (!HasCargo)
            {
                return;
            }
            PipeDestination dest = CurrentDestination();
            if (!dest.IsValid)
            {
                // 目的地失效（建筑被拆/绑定丢失）：滞留待玩家处理，不凭空丢弃。
                return;
            }
            // ① 已在出口段：优先交付。
            if (dest.IsExitFor(this) && TryDeliver(dest))
            {
                return;
            }
            // ② 向「距离-1」的邻段推进。
            TryForward(dest);
        }

        private PipeDestination CurrentDestination()
        {
            if (cargoDef == null)
            {
                return default;
            }
            if (destThing != null)
            {
                if (destThing.Destroyed || !destThing.Spawned)
                {
                    return default;
                }
                return PipeDestination.FromThing(destThing);
            }
            if (destCell.IsValid && destCell.InBounds(Map))
            {
                return PipeDestination.FromCell(destCell);
            }
            return default;
        }

        private bool SameDest(PipeDestination dest)
        {
            if (dest.IsThing)
            {
                return destThing == dest.Thing;
            }
            if (dest.IsCell)
            {
                return destThing == null && destCell == dest.Cell;
            }
            return false;
        }

        /// <summary>
        /// 出口交付（v0.2 §0.8 D4 定向三层：校验 → 物化落格 → 反压滞留）。
        /// 建筑目标：自家设备走 Push；非接口存储建筑（原版货架/第三方深存储）逐占格找可收格。
        /// </summary>
        private bool TryDeliver(PipeDestination dest)
        {
            if (dest.IsThing)
            {
                IItemStorage sink = ItemStorageUtility.GetStorage(dest.Thing);
                if (sink != null)
                {
                    int accepted = sink.Push(cargoDef, cargoCount);
                    cargoCount -= accepted;
                    if (cargoCount <= 0)
                    {
                        ClearCargo();
                    }
                    return accepted > 0;
                }
                // 原版货架/第三方存储建筑：占格内任一格可收即交付（多格深存储只认
                // Position 单格会漏掉其余格——PM 实测「管道贴着建筑却推不出去」的根因）。
                // 「能不能放」统一由 TryDeliverToCell 的原版 Direct 放置判定
                // （同货合堆 / 1.6 maxItemsInCell 一格多堆内开新堆 / 装不下反压不丢货）。
                CellRect rect = dest.Thing.OccupiedRect();
                foreach (IntVec3 cell in rect.Cells)
                {
                    if (cell.InBounds(Map) && TryDeliverToCell(cell))
                    {
                        return true;
                    }
                }
                return false;
            }
            return dest.IsCell && TryDeliverToCell(dest.Cell);
        }

        /// <summary>
        /// 物化并交付到指定格。
        /// ⭐ 两层判定全部交还原版（PM 2026-09-08 深度诊断定案）：
        /// ① <see cref="StoreUtility.IsValidStorageFor"/> 只问过滤器/仓储组——不判「堆满」：
        ///    1.6 原版建筑字段 <c>maxItemsInCell</c> 允许一格叠多堆（Adaptive Storage
        ///    地窖 25/55 组全在一格上），「堆满 stackLimit」≠「收不下」，自判必错。
        /// ② <see cref="GenPlace.TryPlaceThing(Thing,IntVec3,Map,ThingPlaceMode)"/> Direct 模式：
        ///    整批装得下才动手（同货合堆 / maxItemsInCell 配额内开新堆，与原版搬运工
        ///    同一放置语义），装不下返回 false 且不动入参——防丢由原版语义保证。
        /// </summary>
        private bool TryDeliverToCell(IntVec3 cell)
        {
            if (!cell.IsValid || !cell.InBounds(Map))
            {
                return false;
            }
            Thing lot = MakeLotThing();
            if (!StoreUtility.IsValidStorageFor(cell, Map, lot))
            {
                // 过滤器/仓储组不收这类物品：反压滞留，玩家改过滤或改绑。
                lot.Destroy(DestroyMode.Vanish);
                return false;
            }
            if (!GenPlace.TryPlaceThing(lot, cell, Map, ThingPlaceMode.Direct))
            {
                // 整批放不下（全堆满且多堆配额用尽）：反压，不销毁货物。
                if (!lot.Destroyed)
                {
                    lot.Destroy(DestroyMode.Vanish);
                }
                return false;
            }
            // 账目：合堆吸收由原版 API 记账；新堆落地需补仓储组持有清单。
            if (lot.Spawned)
            {
                NotifyStorageParent(lot.Position, lot);
            }
            // CHG-20261004-PERF（2026-10-04 性能审计 §0.3）：此日志原为**零门控** ——
            // 每批搬运到货一行（玩家日志里可见的刷屏源之一），每行一次同步 IO。
            // key 用全局字面量（同 CompItemPort 的理由：Throttled 键表永不清理）。
            const int TransportLogThrottleTicks = 600;   // 每类日志 10 游戏秒最多一行
            if (ChronicleLog.Throttled("PipeSeg.delivered", TransportLogThrottleTicks))
            {
                ChronicleLog.Info(ChronicleLog.Category.Automation,
                    LogPrefix + " delivered " + cargoDef.defName + " x" + cargoCount + " @ " + cell);
            }
            ClearCargo();
            return true;
        }

        /// <summary>向距离-1 的邻段推进（空段或同货同目的地有空间）。</summary>
        private void TryForward(PipeDestination dest)
        {
            MapComponent_PipeNetwork net = Net;
            if (net == null)
            {
                return;
            }
            if (!net.TryGetDistance(this, dest, out int dist))
            {
                return; // 不可达：滞留（管网未连通到目标）。
            }
            List<Building_PipeSegment> neighbors = Neighbors4();
            for (int i = 0; i < neighbors.Count; i++)
            {
                Building_PipeSegment n = neighbors[i];
                if (!net.TryGetDistance(n, dest, out int nd) || nd >= dist)
                {
                    continue;
                }
                if (n.CanAcceptLot(cargoDef, cargoCount, dest))
                {
                    n.StoreLot(cargoDef, cargoCount, dest);
                    ClearCargo();
                    return;
                }
            }
        }

        /// <summary>4 向邻段（管网组件索引查得，拓扑随建拆即时正确）。</summary>
        public List<Building_PipeSegment> Neighbors4()
        {
            List<Building_PipeSegment> result = new List<Building_PipeSegment>(4);
            MapComponent_PipeNetwork net = Net;
            if (net == null)
            {
                return result;
            }
            AddNeighbor(net, Position + IntVec3.North, result);
            AddNeighbor(net, Position + IntVec3.South, result);
            AddNeighbor(net, Position + IntVec3.East, result);
            AddNeighbor(net, Position + IntVec3.West, result);
            return result;
        }

        private static void AddNeighbor(MapComponent_PipeNetwork net, IntVec3 cell, List<Building_PipeSegment> into)
        {
            Building_PipeSegment seg = net.SegmentAt(cell);
            if (seg != null)
            {
                into.Add(seg);
            }
        }

        // ---- 工具 ------------------------------------------------------------

        private Thing MakeLotThing()
        {
            Thing t = ThingMaker.MakeThing(cargoDef);
            t.stackCount = cargoCount;
            return t;
        }

        private void ClearCargo()
        {
            cargoDef = null;
            cargoCount = 0;
            destCell = IntVec3.Invalid;
            destThing = null;
        }

        /// <summary>落格后把物品登记进该格仓储组的持有清单（对齐原版搬运账目）。</summary>
        private void NotifyStorageParent(IntVec3 cell, Thing item)
        {
            List<Thing> things = cell.GetThingList(Map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is ISlotGroupParent parent && things[i] != item)
                {
                    parent.Notify_ReceivedThing(item);
                    return;
                }
            }
        }
    }

    /// <summary>IItemStorage 查找工具（设备本体任意 comp 实现接口即可）。</summary>
    public static class ItemStorageUtility
    {
        public static IItemStorage GetStorage(Thing thing)
        {
            if (thing == null || thing.Destroyed || !(thing is ThingWithComps withComps))
            {
                return null;
            }
            List<ThingComp> comps = withComps.AllComps;
            if (comps == null)
            {
                return null;
            }
            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i] is IItemStorage storage)
                {
                    return storage;
                }
            }
            return null;
        }
    }
}

using System.Collections.Generic;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 传送带段：车间产线的物品缓冲格与短距输送。
    ///
    /// 架构契约（项目铁律，2026-09-04 反射实测）：
    /// - <b>继承 <see cref="Building_Storage"/></b>：白送 slotGroup / IHaulDestination
    ///   自动注册注销 / ITab_Storage / ExposeData——pawn 能直接往段上搬货；格上物品
    ///   由 map 托管，本类<b>不自研物品容器</b>，天然规避"存 Thing 引用读档炸"。
    /// - <b>passability=Standable</b>（XML）：pawn 能站上/走上传送带，否则永远空着。
    /// - 实现 <see cref="IWorkshopDevice"/>：自动纳入车间统计与管理面板（零注册）。
    ///
    /// 输送（MVP）：低频（1.6 自适应 TickInterval 累加，禁假设 250）沿放置朝向
    /// 前送一件物品到相邻同类段；目标段朝向必须一致（防对头互推抖动）；目标为
    /// 空格时前送、为同类物品时合堆。贴图暂无方向指示（Graphic_Single），
    /// Graphic_Multi 四向图列入资产待办。
    /// </summary>
    [StaticConstructorOnStartup]
    public class Building_ConveyorSegment : Building_Storage, IWorkshopDevice, IWorkshopDeviceState
    {
        /// <summary>前送节拍：按 TickInterval 累加的工作配额（1.6 自适应 tick 口径）。</summary>
        private const int PushIntervalTicks = 90;

        /// <summary>吞吐加成后的节拍下限（防物流车间加成叠加后节拍失控）。</summary>
        private const int MinPushIntervalTicks = 30;

        private const string TexPrefix = "Things/Building/Automation/PC_Conveyor_";

        // 三种链位贴图（入口端/中段/出口端），Graphic_Multi 各含四方向箭头。
        private static Graphic startGraphic;
        private static Graphic midGraphic;
        private static Graphic endGraphic;

        private int pushQuota;

        public string DeviceLabel => def.label;

        /// <summary>
        /// 自报状态（2026-09-11 体检 P2）：传送带没有工单、通常也没有电力 comp，
        /// 由原版三输入反推必然得"待命" ⇒ 设备页看不到它在干什么。改为自报：
        /// 断电 → <c>off</c>；段上有可搬运物 → <c>run</c>（输送中）；否则 <c>idle</c>。
        /// </summary>
        public string StateKeyOrNull()
        {
            CompPowerTrader power = GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn)
            {
                return EquipmentStateResolver.KeyOff;
            }
            return HasItemOnCell() ? EquipmentStateResolver.KeyRun : EquipmentStateResolver.KeyIdle;
        }

        private bool HasItemOnCell()
        {
            if (!Spawned || Map == null)
            {
                return false;
            }
            List<Thing> things = Position.GetThingList(Map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] != null && IsHaulableItem(things[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 按链位动态切图：入口侧（朝向反方向）无同类段 = 链首（Start）；
        /// 两侧都有 = 中段（Mid）；出口侧无同类段 = 链尾（End）；孤立段按中段显示。
        /// 方向箭头由 Graphic_Multi 的四方向贴图随 Rotation 自动匹配。
        /// </summary>
        public override Graphic Graphic
        {
            get
            {
                if (!Spawned || Map == null)
                {
                    return base.Graphic;
                }
                bool front = HasSegmentAt(Position + Rotation.FacingCell);
                bool back = HasSegmentAt(Position - Rotation.FacingCell);
                if (back && front)
                {
                    return Mid;
                }
                if (!back && front)
                {
                    return Start;
                }
                if (back && !front)
                {
                    return End;
                }
                return Mid;
            }
        }

        private static Graphic Start => startGraphic ?? (startGraphic = MakeGraphic("Start"));
        private static Graphic Mid => midGraphic ?? (midGraphic = MakeGraphic("Mid"));
        private static Graphic End => endGraphic ?? (endGraphic = MakeGraphic("End"));

        private static Graphic MakeGraphic(string kind)
        {
            return GraphicDatabase.Get<Graphic_Multi>(
                TexPrefix + kind, ShaderDatabase.Cutout, Vector2.one, Color.white);
        }

        private bool HasSegmentAt(IntVec3 cell)
        {
            if (!cell.InBounds(Map))
            {
                return false;
            }
            List<Thing> things = cell.GetThingList(Map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Building_ConveyorSegment)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>本段增删时刷新自身与前后段的贴图（链位可能改变）。</summary>
        private void DirtyChainMesh()
        {
            if (Map == null)
            {
                return;
            }
            Map.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things);
            IntVec3 front = Position + Rotation.FacingCell;
            IntVec3 back = Position - Rotation.FacingCell;
            if (front.InBounds(Map))
            {
                Map.mapDrawer.MapMeshDirty(front, MapMeshFlagDefOf.Things);
            }
            if (back.InBounds(Map))
            {
                Map.mapDrawer.MapMeshDirty(back, MapMeshFlagDefOf.Things);
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            DirtyChainMesh();
        }

        public override void DeSpawn(DestroyMode mode)
        {
            Map oldMap = Map;
            base.DeSpawn(mode);
            if (oldMap != null)
            {
                oldMap.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things);
            }
        }

        protected override void TickInterval(int interval)
        {
            base.TickInterval(interval);
            pushQuota += interval;
            // 先用常量下限短路（2026-09-11 体检 P1-4）：原版 TickInterval 实为 **每 1~15 tick**
            // 一次（Normal 走 GenTicks.GetCameraUpdateRate，视口内更密），而
            // EffectivePushIntervalTicks() 会做 zone 查询 + 在岗加成查询 ⇒ 若每次都算，
            // 等于把"低频节拍"变成高频查询。下限 MinPushIntervalTicks 是节拍绝对下界，
            // 达不到它必然不该推送，语义不变。
            if (pushQuota < MinPushIntervalTicks)
            {
                return;
            }
            if (pushQuota < EffectivePushIntervalTicks())
            {
                return;
            }
            pushQuota = 0;
            TryPushForward();
        }

        /// <summary>
        /// 实际前送节拍 = 基准 <see cref="PushIntervalTicks"/> 按<b>所属车间的吞吐加成</b>
        /// （物流车间 <c>throughputFactor</c>，取在岗人员已缩放值）缩短；无车间 / 无人在岗 → 基准。
        ///
        /// <para>这是 <c>throughputFactor</c> 的落地消费点：物流车间的核心加成从"配了没效果"
        /// 变为真实节拍缩短。下限 <see cref="MinPushIntervalTicks"/> 防加成叠加后节拍失控。</para>
        ///
        /// <para>调用频率：由调用方先用 <see cref="MinPushIntervalTicks"/> 短路后才调用，
        /// 故实际 ≤ 每 30 tick 一次（而非每次 TickInterval）—— 见 <see cref="TickInterval"/>。</para>
        /// </summary>
        private int EffectivePushIntervalTicks()
        {
            if (!Spawned || Map == null)
            {
                return PushIntervalTicks;
            }
            Zone_Workshop zone = WorkshopZoneUtility.WorkshopAt(Position, Map);
            if (zone == null)
            {
                return PushIntervalTicks;
            }
            MapComponent_WorkshopManager manager = MapComponent_WorkshopManager.Get(Map);
            if (manager == null)
            {
                return PushIntervalTicks;
            }
            float factor = manager.ThroughputFactorOf(zone);
            if (factor <= 1.0001f)
            {
                return PushIntervalTicks;
            }
            return Mathf.Clamp(Mathf.RoundToInt(PushIntervalTicks / factor),
                MinPushIntervalTicks, PushIntervalTicks);
        }

        /// <summary>沿朝向前送一件物品（格上物品由 map 托管，本类零自研存储）。</summary>
        private void TryPushForward()
        {
            if (!Spawned || Map == null)
            {
                return;
            }

            IntVec3 target = Position + Rotation.FacingCell;
            if (!target.InBounds(Map))
            {
                return;
            }

            List<Thing> targetThings = target.GetThingList(Map);
            Building_ConveyorSegment next = null;
            ThingWithComps targetItem = null;
            for (int i = 0; i < targetThings.Count; i++)
            {
                Thing t = targetThings[i];
                if (t is Building_ConveyorSegment seg)
                {
                    next = seg;
                }
                else if (IsHaulableItem(t))
                {
                    targetItem = t as ThingWithComps;
                }
            }

            // 目标必须是同类段且朝向一致（防对头互推抖动）。
            if (next == null || next.Rotation != Rotation)
            {
                return;
            }

            ThingWithComps carried = GetCarriedItem() as ThingWithComps;
            if (carried == null)
            {
                return;
            }

            if (targetItem != null)
            {
                // 目标已有物品：仅同类可合堆，否则本格保持缓冲。
                if (targetItem.def == carried.def
                    && targetItem.stackCount < targetItem.def.stackLimit
                    && targetItem.CanStackWith(carried))
                {
                    targetItem.TryAbsorbStack(carried, true);
                }
                return;
            }

            // 空段：teleport 一件（DeSpawn → 改位 → 重 Spawn，正确刷新 thingGrid/region）。
            // teleport 不走原版搬运 Job，须手动补 SlotGroup 通知，否则持有清单与
            // 格上实物漂移，影响原版搬运目标选择。
            carried.DeSpawn();
            carried.Position = target;
            GenSpawn.Spawn(carried, target, Map, Rotation, WipeMode.Vanish, false);
            // 通知走 Building_Storage 的公开方法（SlotGroup 本体无此 API）。
            NotifySlotLost(carried);
            next.NotifySlotReceived(carried);
        }

        /// <summary>本段失去一件物品（teleport 出去）。</summary>
        private void NotifySlotLost(Thing item)
        {
            if (item != null)
            {
                Notify_LostThing(item);
            }
        }

        /// <summary>本段收到一件物品（teleport 进来）。</summary>
        private void NotifySlotReceived(Thing item)
        {
            if (item != null)
            {
                Notify_ReceivedThing(item);
            }
        }

        /// <summary>本段当前承载的可搬运物品（最多处理一件/拍；非建筑、非 pawn、可搬运）。</summary>
        private Thing GetCarriedItem()
        {
            List<Thing> things = Position.GetThingList(Map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                if (t != this && !(t is Building) && IsHaulableItem(t))
                {
                    return t;
                }
            }
            return null;
        }

        private static bool IsHaulableItem(Thing t)
        {
            return t.def != null && t.def.EverHaulable && !(t is Pawn) && !(t is Building);
        }
    }
}

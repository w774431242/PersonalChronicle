using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间区设计器：挂进建筑菜单的「区域」分类，玩家拖拽即可圈出车间。
    ///
    /// 只实现 <see cref="Designator_ZoneAdd"/> 要求的两个 abstract 成员
    /// （<c>NewZoneLabel</c> / <c>MakeNewZone</c>），其余（拖拽、框选、合并相邻区、
    /// 撤销、删除）全部沿用基类行为。
    /// </summary>
    public class Designator_ZoneAdd_Workshop : Designator_ZoneAdd
    {
        /// <summary>最近一次由 <see cref="MakeNewZone"/> 创建的车间，供注册兜底使用。</summary>
        private Zone_Workshop pendingZone;

        protected override string NewZoneLabel =>
            "PersonalChronicle.Automation.Zone.Workshop".Translate().ToString();

        public Designator_ZoneAdd_Workshop()
        {
            defaultLabel = "PersonalChronicle.Automation.Zone.Workshop".Translate().ToString();
            defaultDesc = "PersonalChronicle.Automation.Zone.WorkshopDesc".Translate().ToString();
            // 第一版复用原版区域图标，避免依赖尚未制作的自有贴图（TODO: 换自制图标）。
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Stockpile");
            useMouseIcon = true;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        }

        protected override Zone MakeNewZone()
        {
            ZoneManager zoneManager = Find.CurrentMap.zoneManager;
            // NewZoneName 负责补上「1 / 2 / 3」后缀，保证同地图内名字唯一。
            pendingZone = new Zone_Workshop(zoneManager.NewZoneName(NewZoneLabel), zoneManager);
            return pendingZone;
        }

        /// <summary>
        /// 兜底注册：拖拽圈定后确认 zone 已进 zoneManager。基类负责注册，但若未来
        /// 版本行为变化导致漏注册，zone 会「画得出来却查不到」，属于静默故障，
        /// 因此这里显式兜一次（内部先查后注册，正常情况下不会重复）。
        ///
        /// 基类的 <c>SelectedZone</c> 对外不可访问，故由本类自持刚创建的引用。
        /// </summary>
        public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
        {
            base.DesignateMultiCell(cells);
            EnsureRegistered();
        }

        private void EnsureRegistered()
        {
            Zone zone = pendingZone;
            pendingZone = null;
            if (zone == null || zone.Map == null)
            {
                return;
            }
            List<Zone> all = zone.Map.zoneManager.AllZones;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == zone)
                {
                    return;
                }
            }
            zone.Map.zoneManager.RegisterZone(zone);
        }
    }
}

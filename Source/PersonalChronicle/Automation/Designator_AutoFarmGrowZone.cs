using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 农机专属「圈种植区」设计器。
    ///
    /// 仅在<b>目标农机的覆盖范围内</b>允许圈地（<see cref="CanDesignateCell"/> 加一道
    /// 覆盖约束），落定后由基类 <c>DesignateMultiCell</c> 生成 <see cref="Zone_Growing"/>，
    /// 我们在其后把玩家在弹窗里选好的作物套上去（<c>PlantDefToGrow</c> + <c>allowSow</c>）。
    ///
    /// 拖拽 / 框选 / 合并相邻区 / 撤销 / 删除全部沿用 <see cref="Designator_ZoneAdd"/> 基类，
    /// 这里只补「覆盖范围约束」与「自动套用作物」两件农机专属行为。
    /// </summary>
    public class Designator_AutoFarmGrowZone : Designator_ZoneAdd
    {
        private Building_AutoFarm target;
        private ThingDef crop;
        private Zone_Growing madeZone;

        /// <summary>每次启用前由弹窗注入目标设备与作物。</summary>
        public void Configure(Building_AutoFarm device, ThingDef cropDef)
        {
            target = device;
            crop = cropDef;
        }

        public Designator_AutoFarmGrowZone()
        {
            icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Growing");
            useMouseIcon = true;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        }

        protected override string NewZoneLabel => "growing zone";

        protected override Zone MakeNewZone()
        {
            ZoneManager zm = Find.CurrentMap.zoneManager;
            madeZone = new Zone_Growing(zm);
            return madeZone;
        }

        public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
        {
            base.DesignateMultiCell(cells);
            if (madeZone != null && crop != null)
            {
                ((IPlantToGrowSettable)madeZone).SetPlantDefToGrow(crop);
                madeZone.allowSow = true;
            }
            madeZone = null;
        }

        /// <summary>覆盖约束：圈地只允许落在目标农机的覆盖区内，且通过基类对 zone 合法性的检查。</summary>
        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (target == null || !target.Spawned)
            {
                return false;
            }
            CompAutoAgriculture comp = target.GetComp<CompAutoAgriculture>();
            if (comp == null || !comp.CoverageCells.Contains(c))
            {
                return false;
            }
            return base.CanDesignateCell(c);
        }
    }
}

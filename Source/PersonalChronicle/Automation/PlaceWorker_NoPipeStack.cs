using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 管道放置互斥：一格只允许一个传导物。
    ///
    /// PM 2026-09-07 实机反馈「原版电缆和物流管道可以混淆使用」——同格电缆+管道
    /// 视觉重叠、电网混连。本 Worker 拦两类：
    /// ① 同格已有另一段管道（isEdifice=false 的既有职责，防幽灵段）；
    /// ② 同格已有任何电力传导物（原版电缆/电池/太阳能板/开关，或其蓝图/框架）——
    ///    它们的 <c>EverTransmitsPower</c> 为真。反向（电缆铺管道上）由原版
    ///    <c>PlaceWorker_Conduit</c> 的 EverTransmitsPower 检查拦截，双向闭环。
    /// </summary>
    public class PlaceWorker_NoPipeStack : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map,
            Thing thingToIgnore = null, Thing thing = null)
        {
            if (map == null || !loc.InBounds(map))
            {
                return true;
            }
            foreach (Thing t in map.thingGrid.ThingsListAtFast(loc))
            {
                if (t == thingToIgnore)
                {
                    continue;
                }
                if (t is Building_PipeSegment)
                {
                    return new AcceptanceReport(
                        "PersonalChronicle.Automation.Pipe.AlreadyThere".Translate());
                }
                // 传导物互斥（含蓝图/框架：建成后的 def 才带电力 comp）。
                if (t.def.EverTransmitsPower)
                {
                    return new AcceptanceReport(
                        "PersonalChronicle.Automation.Pipe.ConduitConflict".Translate());
                }
                BuildableDef built = t.def.entityDefToBuild;
                if (built != null && built is ThingDef bt && bt.EverTransmitsPower)
                {
                    return new AcceptanceReport(
                        "PersonalChronicle.Automation.Pipe.ConduitConflict".Translate());
                }
            }
            return true;
        }
    }
}

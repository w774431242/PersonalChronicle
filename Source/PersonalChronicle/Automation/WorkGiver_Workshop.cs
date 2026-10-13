using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间岗位 WorkGiver。
    ///
    /// ⚠️ 刻意<b>不继承 WorkGiver_Scanner</b>：车间岗位没有目标物可扫，若用 Scanner
    /// 而不实现扫描源，<see cref="WorkGiver_Scanner.HasJobOnThing"/> 永不被调用
    /// （参考文档最致命缺陷）。正确写法是继承 <see cref="WorkGiver"/> 并 override
    /// <see cref="WorkGiver.NonScanJob"/>，配套 WorkGiverDef 上 scanThings=false /
    /// scanCells=false —— 已扫描 1.6 程序集确认 NonScanJob 存在。
    ///
    /// 调用时机：原版 JobGiver_Work 在 pawn 工作面板启用本 WorkType 且轮到本工作类型
    /// 时调用一次（非每帧），因此这里做几次小循环完全无性能压力。
    /// </summary>
    public class WorkGiver_Workshop : WorkGiver
    {
        public override Job NonScanJob(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null)
            {
                return null;
            }
            MapComponent_WorkshopManager manager = MapComponent_WorkshopManager.Get(pawn.Map);
            if (manager == null)
            {
                return null;
            }
            return manager.TryGetDutyJob(pawn);
        }
    }
}

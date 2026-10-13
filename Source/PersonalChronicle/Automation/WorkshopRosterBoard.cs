using System.Collections.Generic;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 班次序位查询（<b>唯一实现</b>）：把"参与倒班的班次"映射为连续序位 0..N-1。
    ///
    /// <para><b>2026-10-02 减法审计</b>：本类原是 548 行的"人员排班看板"（含 <c>Draw</c> 绘制树、
    /// KPI 条、日历网格、点检格、班组菜单…）。但 <c>Draw</c> 在全工程<b>无任何调用方</b>
    /// —— 页签枚举里并没有"排班看板"这一页，页面早已被"排班"页取代 ⇒ 整棵绘制树连同其
    /// 私有辅助（<c>DrawHeader</c>/<c>DrawKpiBar</c>/<c>DrawGrid</c>/<c>DrawCell</c>/
    /// <c>OpenCrewMenu</c> 等）一并删除，只保留仍被
    /// <c>WorkshopFloorViewProvider.Detail</c>（经 <c>WorkshopHandoverBoard</c>）间接使用的
    /// 两个序位查询。</para>
    /// </summary>
    internal static class WorkshopRosterBoard
    {
        /// <summary>
        /// 参与倒班的班次数量（= 全部班次里最大 <see cref="WorkshopShift.ShiftSlotIndex"/> + 1；
        /// 未启用轮转 / 未指定序位的班次不计入）。
        /// </summary>
        public static int CountRotationSlots(List<WorkshopShift> shifts)
        {
            int count = 0;
            if (shifts == null)
            {
                return 0;
            }
            for (int i = 0; i < shifts.Count; i++)
            {
                WorkshopShift shift = shifts[i];
                if (shift != null && shift.UseCrewCycle && shift.ShiftSlotIndex >= 0
                    && shift.ShiftSlotIndex + 1 > count)
                {
                    count = shift.ShiftSlotIndex + 1;
                }
            }
            return count;
        }

        /// <summary>取该序位对应的班次；无对应则 null。</summary>
        public static WorkshopShift FindShiftAtSlot(List<WorkshopShift> shifts, int slot)
        {
            if (shifts == null)
            {
                return null;
            }
            for (int i = 0; i < shifts.Count; i++)
            {
                WorkshopShift shift = shifts[i];
                if (shift != null && shift.UseCrewCycle && shift.ShiftSlotIndex == slot)
                {
                    return shift;
                }
            }
            return null;
        }
    }
}

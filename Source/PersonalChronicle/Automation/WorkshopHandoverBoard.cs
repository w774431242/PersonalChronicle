using System.Collections.Generic;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 交接 / 在岗查询（<b>单一实现</b>）：班次序位、距下次交接、在岗人数、车间设备状态计数。
    ///
    /// <para><b>2026-10-02 减法审计</b>：本类原是 350 行的"交接板 + 设备点检表"绘制器
    /// （含 <c>Draw</c>/<c>DrawHandover</c>/<c>DrawInspection</c>/<c>KeeperOf</c>/
    /// <c>BillCountOf</c>/<c>StateColor</c>）。但那三个绘制入口在全工程<b>无调用方</b> ——
    /// 页签枚举里没有"交接板"这一页 ⇒ 整棵绘制树已删除，只保留仍被
    /// <c>WorkshopFloorViewProvider.Detail</c>（设备详情弹窗「本班」块）使用的五个查询。</para>
    /// </summary>
    internal static class WorkshopHandoverBoard
    {
        private const string KeyPrefix = "PersonalChronicle.Automation.Tab.";

        /// <summary>
        /// 车间级状态计数（交接三件事里的数字）。
        ///
        /// <list type="bullet">
        /// <item><paramref name="openBills"/> = 有现在可做工单的设备台数（AnyShouldDoNow）；</item>
        /// <item><paramref name="faults"/> = CompBreakdownable.BrokenDown 的设备台数；</item>
        /// <item><paramref name="unpowered"/> = CompPowerTrader.PowerOn == false 的设备台数。</item>
        /// </list>
        /// </summary>
        internal static void CountWorkshopStatus(List<Thing> devices, out int openBills,
            out int faults, out int unpowered)
        {
            openBills = 0;
            faults = 0;
            unpowered = 0;
            if (devices == null)
            {
                return;
            }
            for (int i = 0; i < devices.Count; i++)
            {
                Thing device = devices[i];
                if (!WorkshopStats.IsBillGiverBench(device))
                {
                    continue;
                }
                BillStack stack = (device as IBillGiver).BillStack;
                if (stack != null && stack.AnyShouldDoNow)
                {
                    openBills++;
                }
                CompBreakdownable breakdown = device.TryGetComp<CompBreakdownable>();
                if (breakdown != null && breakdown.BrokenDown)
                {
                    faults++;
                }
                CompPowerTrader power = device.TryGetComp<CompPowerTrader>();
                if (power != null && !power.PowerOn)
                {
                    unpowered++;
                }
            }
        }

        /// <summary>当前小时落在哪个班次序位（-1 = 非倒班时段）。</summary>
        internal static int SlotAtHour(List<WorkshopShift> shifts, int hour)
        {
            int count = WorkshopRosterBoard.CountRotationSlots(shifts);
            for (int slot = 0; slot < count; slot++)
            {
                WorkshopShift shift = WorkshopRosterBoard.FindShiftAtSlot(shifts, slot);
                if (shift != null && WorkshopHours.InRange(hour, shift.StartHour, shift.EndHour))
                {
                    return slot;
                }
            }
            return -1;
        }

        /// <summary>下一个班次的起始小时（用于「距下次交接」）。</summary>
        internal static int NextShiftStartHour(List<WorkshopShift> shifts, int hour)
        {
            int count = WorkshopRosterBoard.CountRotationSlots(shifts);
            int best = -1;
            for (int slot = 0; slot < count; slot++)
            {
                WorkshopShift shift = WorkshopRosterBoard.FindShiftAtSlot(shifts, slot);
                if (shift == null)
                {
                    continue;
                }
                if (shift.StartHour > hour && (best < 0 || shift.StartHour < best))
                {
                    best = shift.StartHour;
                }
            }
            if (best < 0)
            {
                // 已过今天最后一个班次 ⇒ 取明天的第一个班次
                for (int slot = 0; slot < count; slot++)
                {
                    WorkshopShift shift = WorkshopRosterBoard.FindShiftAtSlot(shifts, slot);
                    if (shift != null && (best < 0 || shift.StartHour < best))
                    {
                        best = shift.StartHour;
                    }
                }
            }
            return best < 0 ? hour : best;
        }

        /// <summary>本车间当前在岗人数（调度器运行态口径：班组成员中在岗车间为本车间者）。</summary>
        internal static int CountOnDuty(MapComponent_WorkshopManager manager, Zone_Workshop zone)
        {
            if (manager == null || zone == null)
            {
                return 0;
            }
            int count = 0;
            List<WorkshopCrew> crews = zone.Crews;
            for (int c = 0; c < crews.Count; c++)
            {
                List<Pawn> members = crews[c] != null ? crews[c].Members : null;
                if (members == null)
                {
                    continue;
                }
                for (int i = 0; i < members.Count; i++)
                {
                    Pawn pawn = members[i];
                    if (pawn != null && manager.ActiveZoneOf(pawn) == zone)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>班次序位的本地化标签（早班 / 中班 / 夜班 / 第 N 班）。</summary>
        internal static string SlotLabel(int slot)
        {
            switch (slot)
            {
                case 0: return T("CrewSlotEarly");
                case 1: return T("CrewSlotMiddle");
                case 2: return T("CrewSlotNight");
                default: return T1("CrewSlotN", slot + 1);
            }
        }

        private static string T(string suffix)
        {
            return (KeyPrefix + suffix).Translate().ToString();
        }

        private static string T1(string suffix, int arg)
        {
            return (KeyPrefix + suffix).Translate(arg).ToString();
        }
    }
}

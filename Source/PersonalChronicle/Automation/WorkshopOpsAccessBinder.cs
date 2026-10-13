using System.Collections.Generic;
using PersonalChronicle.Application.Workshop;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 把「车间运行摘要」实现注入 Application 契约 <see cref="WorkshopOpsAccess"/>
    /// （Automation → Application 为允许方向；与 <c>WorkshopBonusAccessBinder</c> 同范式）。
    ///
    /// <para>口径与唯一真源：</para>
    /// <list type="bullet">
    /// <item>人时 = Σ 各车间 <c>Attendance.ActualTicksInRange / OvertimeTicksInRange</c>
    /// （与排班 KPI 条 <c>WorkshopRosterBoard.DrawKpiBar</c> 同一台账）；</item>
    /// <item>排班覆盖 = 三类排班（固定班 / 班组 / 轮岗）统一折算成 <c>RosterSlot</c> 后交给
    /// <c>WorkshopRosterCoverage</c>（**唯一**覆盖度实现，与排班图同源——构造口径照抄
    /// <c>WorkshopScheduleChart.BuildRoster</c>）；</item>
    /// <item>在岗 = <c>MapComponent_WorkshopManager.ActiveWorkerCount</c>（运行态字典计数）；
    /// 成员 = 各车间 <c>Members</c> 去重（同一殖民者可同时属于多个车间）。</item>
    /// </list>
    ///
    /// <para>失败降级：任一环节异常由契约层 catch ⇒ 返回零值（调用方按"未接入"呈现）。</para>
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class WorkshopOpsAccessBinder
    {
        static WorkshopOpsAccessBinder()
        {
            WorkshopOpsAccess.SetSource(BuildDigest);
        }

        private static WorkshopOpsDigest BuildDigest(Map map, int windowDays)
        {
            WorkshopOpsDigest digest = new WorkshopOpsDigest();
            if (map == null)
            {
                return digest;
            }
            int days = windowDays > 0 ? windowDays : 1;
            int today = GenDate.DaysPassed;
            int fromDay = today - days + 1;

            MapComponent_WorkshopManager manager = MapComponent_WorkshopManager.Get(map);

            // ① 车间级：人时 / 成员 / 车间数
            HashSet<Pawn> members = new HashSet<Pawn>();
            foreach (Zone_Workshop zone in WorkshopZoneUtility.AllWorkshops(map))
            {
                if (zone == null)
                {
                    continue;
                }
                digest.WorkshopCount++;
                WorkshopAttendance attendance = zone.Attendance;
                if (attendance != null)
                {
                    digest.ManHourTicks += attendance.ActualTicksInRange(fromDay, days);
                    digest.OvertimeTicks += attendance.OvertimeTicksInRange(fromDay, days);
                }
                List<Pawn> zoneMembers = zone.Members;
                if (zoneMembers != null)
                {
                    for (int i = 0; i < zoneMembers.Count; i++)
                    {
                        if (zoneMembers[i] != null)
                        {
                            members.Add(zoneMembers[i]);
                        }
                    }
                }
            }
            digest.MemberCount = members.Count;

            // ② 在岗（运行态）
            if (manager != null)
            {
                digest.OnDutyCount = manager.ActiveWorkerCount;
            }

            // ③ 排班覆盖（全厂并集：任一小时只要有任一车间有人即视为已覆盖）
            List<RosterSlot> roster = new List<RosterSlot>();
            if (manager != null)
            {
                AddFixedSlots(roster, manager.Slots);
                AddShifts(roster, manager.Shifts);
                AddRotations(roster, manager.Rotations);
            }
            digest.ScheduledEntries = roster.Count;
            if (roster.Count > 0)
            {
                int[] hourly = WorkshopRosterCoverage.HourlyCoverage(roster);
                digest.PlannedManHours = WorkshopRosterCoverage.ManHoursPerDay(hourly);
                digest.EmptyHours = WorkshopRosterCoverage.EmptyHours(hourly);
            }
            else
            {
                // 未排班：全天空档（调用方按 ScheduledEntries == 0 判"未接入"，不拿 24 当读数画）
                digest.EmptyHours = WorkshopRosterCoverage.HoursPerDay;
            }
            return digest;
        }

        /// <summary>固定班：每个岗位 1 人（口径同 <c>WorkshopScheduleChart.BuildRoster</c>）。</summary>
        private static void AddFixedSlots(List<RosterSlot> roster, List<WorkshopSlot> slots)
        {
            if (slots == null)
            {
                return;
            }
            for (int i = 0; i < slots.Count; i++)
            {
                WorkshopSlot slot = slots[i];
                if (slot != null)
                {
                    roster.Add(new RosterSlot(slot.StartHour, slot.EndHour, 1));
                }
            }
        }

        /// <summary>
        /// 班组班次：名额 = <c>RequiredCount</c>，0 = 不限 ⇒ 按<b>派工同源</b>口径解析
        /// （2026-10-02 审计 P1-B：旧读 <c>EffectivePool</c> 与派工口径不一致，
        /// 多班次车间未指派时"实际空岗 0 人、覆盖率显示满员"）。
        /// </summary>
        private static void AddShifts(List<RosterSlot> roster, List<WorkshopShift> shifts)
        {
            if (shifts == null)
            {
                return;
            }
            for (int i = 0; i < shifts.Count; i++)
            {
                WorkshopShift shift = shifts[i];
                if (shift == null)
                {
                    continue;
                }
                int count = StaffingOf(shift);
                roster.Add(new RosterSlot(shift.StartHour, shift.EndHour, count));
            }
        }

        /// <summary>与派工同源的名额估算（唯一实现见 <see cref="WorkshopZoneUtility.ShiftStaffingOf"/>）。</summary>
        private static int StaffingOf(WorkshopShift shift)
        {
            return WorkshopZoneUtility.ShiftStaffingOf(shift);
        }

        /// <summary>轮岗：每规则 1 人。</summary>
        private static void AddRotations(List<RosterSlot> roster, List<RotationRule> rotations)
        {
            if (rotations == null)
            {
                return;
            }
            for (int i = 0; i < rotations.Count; i++)
            {
                RotationRule rule = rotations[i];
                if (rule != null)
                {
                    roster.Add(new RosterSlot(rule.StartHour, rule.EndHour, 1));
                }
            }
        }
    }
}

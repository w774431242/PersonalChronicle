using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间考勤台账（Phase 2，2026-09-11）：按**绝对游戏日**累计实际在岗人时与加班人时。
    ///
    /// <para><b>口径</b>：</para>
    /// <list type="bullet">
    /// <item>实际人时 = 该车间所有在岗者本次值勤时长之和（`NotifyDutyStarted/Ended` 之间）；</item>
    /// <item>加班人时 = 单次值勤超出**当班班次时长**的部分（现实里的超班时值勤）；</item>
    /// <item>计划人时/出勤率不落库：由排班表（班次时长 × 名额）当场算，避免两处口径漂移。</item>
    /// </list>
    ///
    /// <para>保留最近 <see cref="RetainDays"/> 天，超出即裁剪（有界，不随游戏时长膨胀）。</para>
    /// </summary>
    public sealed class WorkshopAttendance : IExposable
    {
        /// <summary>保留天数（覆盖一个季度 15 天，够看周期报表）。</summary>
        public const int RetainDays = 15;

        private Dictionary<int, long> actualTicksByDay = new Dictionary<int, long>();
        private Dictionary<int, long> overtimeTicksByDay = new Dictionary<int, long>();

        /// <summary>记一次值勤：时长与其中超出班次时长的加班部分。</summary>
        public void AddDuty(int day, long ticks, long overtimeTicks)
        {
            if (ticks <= 0L)
            {
                return;
            }
            if (actualTicksByDay == null)
            {
                actualTicksByDay = new Dictionary<int, long>();
            }
            if (overtimeTicksByDay == null)
            {
                overtimeTicksByDay = new Dictionary<int, long>();
            }
            long current;
            actualTicksByDay.TryGetValue(day, out current);
            actualTicksByDay[day] = current + ticks;
            if (overtimeTicks > 0L)
            {
                overtimeTicksByDay.TryGetValue(day, out current);
                overtimeTicksByDay[day] = current + overtimeTicks;
            }
        }

        public long ActualTicksOn(int day)
        {
            long value;
            return actualTicksByDay != null && actualTicksByDay.TryGetValue(day, out value) ? value : 0L;
        }

        public long OvertimeTicksOn(int day)
        {
            long value;
            return overtimeTicksByDay != null && overtimeTicksByDay.TryGetValue(day, out value) ? value : 0L;
        }

        /// <summary>[fromDay, fromDay+dayCount) 区间的实际人时合计。</summary>
        public long ActualTicksInRange(int fromDay, int dayCount)
        {
            long total = 0L;
            for (int i = 0; i < dayCount; i++)
            {
                total += ActualTicksOn(fromDay + i);
            }
            return total;
        }

        /// <summary>[fromDay, fromDay+dayCount) 区间的加班人时合计。</summary>
        public long OvertimeTicksInRange(int fromDay, int dayCount)
        {
            long total = 0L;
            for (int i = 0; i < dayCount; i++)
            {
                total += OvertimeTicksOn(fromDay + i);
            }
            return total;
        }

        /// <summary>裁剪过期日期（保留 today-RetainDays+1 .. today）。</summary>
        public void Prune(int today)
        {
            PruneMap(actualTicksByDay, today);
            PruneMap(overtimeTicksByDay, today);
        }

        private static void PruneMap(Dictionary<int, long> map, int today)
        {
            if (map == null || map.Count == 0)
            {
                return;
            }
            int floor = today - RetainDays + 1;
            List<int> stale = null;
            foreach (KeyValuePair<int, long> pair in map)
            {
                if (pair.Key < floor || pair.Key > today)
                {
                    if (stale == null)
                    {
                        stale = new List<int>();
                    }
                    stale.Add(pair.Key);
                }
            }
            if (stale != null)
            {
                for (int i = 0; i < stale.Count; i++)
                {
                    map.Remove(stale[i]);
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref actualTicksByDay, "actualTicksByDay", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref overtimeTicksByDay, "overtimeTicksByDay", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (actualTicksByDay == null)
                {
                    actualTicksByDay = new Dictionary<int, long>();
                }
                if (overtimeTicksByDay == null)
                {
                    overtimeTicksByDay = new Dictionary<int, long>();
                }
            }
        }
    }
}

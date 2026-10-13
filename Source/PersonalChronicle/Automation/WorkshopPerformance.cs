using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间<b>按人</b>绩效台账（2026-09-25）：按「pawn × 绝对游戏日」累计
    /// <b>实际在岗人时</b>与<b>产量件数</b>，供班组/人员绩效区块（出勤率、生产率）读取。
    ///
    /// <para><b>口径</b>：</para>
    /// <list type="bullet">
    /// <item>人时：收工结算时写入（与 <see cref="WorkshopAttendance"/> 同一 <c>NotifyDutyEnded</c> 挂载点，
    /// 只多拆一层"到人"）；</item>
    /// <item>产量：成品出炉时写入（<c>Patch_GenRecipe</c> → manager.<c>NotifyProductMade</c>），
    /// 只统计<b>在岗期间</b>的产出 —— 不在岗者不占车间绩效；</item>
    /// <item>计划人时/出勤率/生产率不落库：由绩效区块当场按班次时长与目标产量计算，
    /// 避免口径漂移（与 <see cref="WorkshopAttendance"/> 同原则）。</item>
    /// </list>
    ///
    /// <para>保留最近 <see cref="WorkshopAttendance.RetainDays"/> 天，超出即裁剪（有界）。</para>
    /// </summary>
    public sealed class WorkshopPerformance : IExposable
    {
        private List<WorkshopPawnPerformance> rows = new List<WorkshopPawnPerformance>();

        /// <summary>某 pawn 的台账行（无则建空行，调用方随后写入即生效）。</summary>
        public WorkshopPawnPerformance RowFor(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }
            Ensure();
            for (int i = 0; i < rows.Count; i++)
            {
                WorkshopPawnPerformance row = rows[i];
                if (row != null && row.Pawn == pawn)
                {
                    return row;
                }
            }
            var row2 = new WorkshopPawnPerformance(pawn);
            rows.Add(row2);
            return row2;
        }

        /// <summary>只读查询某 pawn 的台账行（无则返回 null，不创建 —— UI 每帧读取用）。</summary>
        public WorkshopPawnPerformance Peek(Pawn pawn)
        {
            if (pawn == null || rows == null)
            {
                return null;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                WorkshopPawnPerformance row = rows[i];
                if (row != null && row.Pawn == pawn)
                {
                    return row;
                }
            }
            return null;
        }

        /// <summary>记一次值勤人时（收工结算，与车间台账同点写入）。</summary>
        public void AddDuty(Pawn pawn, int day, long ticks)
        {
            if (pawn == null || ticks <= 0L)
            {
                return;
            }
            RowFor(pawn).AddDuty(day, ticks);
        }

        /// <summary>记一次在岗产出（成品出炉）。</summary>
        public void AddProduced(Pawn pawn, int day, int count)
        {
            if (pawn == null || count <= 0)
            {
                return;
            }
            RowFor(pawn).AddProduced(day, count);
        }

        /// <summary>裁剪过期日期与已销毁引用。</summary>
        public void Prune(int today)
        {
            Ensure();
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                WorkshopPawnPerformance row = rows[i];
                if (row == null || row.Pawn == null || row.Pawn.Destroyed)
                {
                    rows.RemoveAt(i);
                    continue;
                }
                row.Prune(today);
            }
        }

        private void Ensure()
        {
            if (rows == null)
            {
                rows = new List<WorkshopPawnPerformance>();
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref rows, "rows", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && rows == null)
            {
                rows = new List<WorkshopPawnPerformance>();
            }
        }
    }

    /// <summary>单个 pawn 的按日绩效（人时 / 产量件数）。</summary>
    public sealed class WorkshopPawnPerformance : IExposable
    {
        private Pawn pawn;
        private Dictionary<int, long> dutyTicksByDay = new Dictionary<int, long>();
        private Dictionary<int, int> producedByDay = new Dictionary<int, int>();

        public Pawn Pawn => pawn;

        public WorkshopPawnPerformance()
        {
        }

        public WorkshopPawnPerformance(Pawn pawn)
        {
            this.pawn = pawn;
        }

        public void AddDuty(int day, long ticks)
        {
            if (ticks <= 0L)
            {
                return;
            }
            Ensure();
            long current;
            dutyTicksByDay.TryGetValue(day, out current);
            dutyTicksByDay[day] = current + ticks;
        }

        public void AddProduced(int day, int count)
        {
            if (count <= 0)
            {
                return;
            }
            Ensure();
            int current;
            producedByDay.TryGetValue(day, out current);
            producedByDay[day] = current + count;
        }

        public long ActualTicksOn(int day)
        {
            long value;
            return dutyTicksByDay != null && dutyTicksByDay.TryGetValue(day, out value) ? value : 0L;
        }

        public int ProducedOn(int day)
        {
            int value;
            return producedByDay != null && producedByDay.TryGetValue(day, out value) ? value : 0;
        }

        public void Prune(int today)
        {
            int floor = today - WorkshopAttendance.RetainDays + 1;
            PruneMap(dutyTicksByDay, today, floor);
            PruneMap(producedByDay, today, floor);
        }

        private static void PruneMap<T>(Dictionary<int, T> map, int today, int floor)
        {
            if (map == null || map.Count == 0)
            {
                return;
            }
            List<int> stale = null;
            foreach (KeyValuePair<int, T> pair in map)
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
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Collections.Look(ref dutyTicksByDay, "dutyTicksByDay", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref producedByDay, "producedByDay", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (dutyTicksByDay == null)
                {
                    dutyTicksByDay = new Dictionary<int, long>();
                }
                if (producedByDay == null)
                {
                    producedByDay = new Dictionary<int, int>();
                }
            }
        }

        private void Ensure()
        {
            if (dutyTicksByDay == null)
            {
                dutyTicksByDay = new Dictionary<int, long>();
            }
            if (producedByDay == null)
            {
                producedByDay = new Dictionary<int, int>();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Api;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// Partial of ArchiveService - work priority read, work-time stats, and sample intake.
    /// Priority read degrades to archived snapshot (LA-1) for dead/absent pawns.
    /// Sample intake is the single write gateway; delegates to ChronicleGameComponent.
    /// </summary>
    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
    {

        /// <summary>
        /// 读取指定 pawn 的工作优先级列表。
        /// </summary>
        /// <param name="stableId">pawn 稳定 ID（GetUniqueLoadID）。</param>
        /// <returns>仅含优先级大于 0 的工种；0 表示未开启，不返回。</returns>
        /// <remarks>
        /// 双路径设计：存活的自由殖民者直读 <c>pawn.workSettings</c>（实时、权威）；
        /// 死亡或离场者降级读 <c>PawnObject.WorkSnapshot</c> 归档快照（LA-1），可能滞后于最后一次写入。
        /// 两条路径都已过滤掉非正优先级，调用方无需再做过滤。
        /// </remarks>
        public IReadOnlyList<WorkPriorityView> GetWorkPriorities(string stableId)
        {
            Pawn pawn = GetLivePawn(stableId);
            if (pawn != null && pawn.IsFreeColonist && pawn.workSettings != null)
            {
                List<WorkPriorityView> live = new List<WorkPriorityView>();
                List<WorkTypeDef> workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading;
                for (int i = 0; i < workTypes.Count; i++)
                {
                    WorkTypeDef wt = workTypes[i];
                    if (wt == null)
                    {
                        continue;
                    }
                    int priority = pawn.workSettings.GetPriority(wt);
                    if (priority > 0)
                    {
                        live.Add(new WorkPriorityView(wt.defName, priority));
                    }
                }
                return live;
            }

            // Dead / absent pawn: degraded read from the archived snapshot (LA-1).
            PawnObject pawnObject = GetObject(stableId) as PawnObject;
            if (pawnObject != null && pawnObject.WorkSnapshot != null)
            {
                List<WorkPriorityView> degraded = new List<WorkPriorityView>();
                for (int i = 0; i < pawnObject.WorkSnapshot.Count; i++)
                {
                    WorkPrioritySnapshot entry = pawnObject.WorkSnapshot[i];
                    if (entry != null && !string.IsNullOrEmpty(entry.WorkTypeDefName) && entry.Priority > 0)
                    {
                        degraded.Add(new WorkPriorityView(entry.WorkTypeDefName, entry.Priority));
                    }
                }
                return degraded;
            }
            return new List<WorkPriorityView>();
        }

        /// <summary>
        /// 按工种统计累计工时，附带占比与排名。
        /// </summary>
        /// <param name="stableId">pawn 稳定 ID。</param>
        /// <returns>按 ticks 降序排列的工时行；无数据时返回空集合（不会是 null）。</returns>
        /// <remarks>
        /// TotalWorkTicks 在旧存档中可能因缺字段而为 0，此时就地按各工种累加回算，
        /// 以保证占比分母有效——不要改成直接返回 0 占比，那会让 UI 全部显示为 0%。
        /// </remarks>
        public IReadOnlyList<WorkTimeStatView> GetWorkTimeStats(string stableId)
        {
            PawnObject pawnObject = GetObject(stableId) as PawnObject;
            if (pawnObject == null || pawnObject.WorkTime == null
                || pawnObject.WorkTime.TicksByWorkType == null
                || pawnObject.WorkTime.TicksByWorkType.Count == 0)
            {
                return new List<WorkTimeStatView>();
            }
            long total = pawnObject.WorkTime.TotalWorkTicks;
            if (total <= 0L)
            {
                // Recompute total if ledger was loaded without total field.
                total = 0L;
                foreach (KeyValuePair<string, long> pair in pawnObject.WorkTime.TicksByWorkType)
                {
                    total += pair.Value;
                }
            }
            List<KeyValuePair<string, long>> rows = new List<KeyValuePair<string, long>>();
            foreach (KeyValuePair<string, long> pair in pawnObject.WorkTime.TicksByWorkType)
            {
                if (!string.IsNullOrEmpty(pair.Key) && pair.Value > 0L)
                {
                    rows.Add(pair);
                }
            }
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));
            List<WorkTimeStatView> result = new List<WorkTimeStatView>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                KeyValuePair<string, long> row = rows[i];
                long lastTick = 0L;
                if (pawnObject.WorkTime.LastTickByWorkType != null)
                {
                    pawnObject.WorkTime.LastTickByWorkType.TryGetValue(row.Key, out lastTick);
                }
                float share = total > 0L ? (float)row.Value / (float)total : 0f;
                result.Add(new WorkTimeStatView(row.Key, row.Value, lastTick, share, i + 1));
            }
            return result;
        }

        /// <summary>
        /// 入账一条工时采样（IWorkTimeCaptureService 的实现）。
        /// </summary>
        /// <param name="sample">采样数据；PawnStableId 与 WorkTypeDefName 非空、且 SampleTicks 为正才有效。</param>
        /// <returns>成功入账返回 true；参数非法、组件缺失、或目标 pawn 不是当前在编成员时返回 false。</returns>
        /// <remarks>
        /// 这是工时写入的唯一收口：校验通过后转交 ChronicleGameComponent.AddWorkTimeSample，本方法不直接改账本。
        /// TryClassifyCurrent 用于排除访客、囚犯等非在编人员——删掉会让工时账本混入非殖民者数据。
        /// GameTick 缺省（小于等于 0）时以当前 GenTicks.TicksGame 兜底。
        /// </remarks>
        public bool RecordSample(WorkTimeSample sample)
        {
            if (sample == null || string.IsNullOrEmpty(sample.PawnStableId)
                || string.IsNullOrEmpty(sample.WorkTypeDefName)
                || sample.SampleTicks <= 0L)
            {
                return false;
            }
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return false;
            }
            Pawn live = GetLivePawn(sample.PawnStableId);
            if (live == null || !ChronicleColonistScanner.TryClassifyCurrent(live, out _))
            {
                return false;
            }
            long gameTick = sample.GameTick > 0L ? sample.GameTick : GenTicks.TicksGame;
            return component.AddWorkTimeSample(
                sample.PawnStableId,
                sample.WorkTypeDefName,
                sample.SampleTicks,
                gameTick);
        }

        /// <summary>
        /// 读取工时总量；TotalWorkTicks 缺失（为 0）时按各工种累加回算。
        /// </summary>
        /// <remarks>回算分支用于兼容缺少 TotalWorkTicks 字段的旧存档，勿删。</remarks>
        private static long GetTotalWorkTicks(WorkTimeAccumulator workTime)
        {
            if (workTime == null)
            {
                return 0L;
            }
            if (workTime.TotalWorkTicks > 0L)
            {
                return workTime.TotalWorkTicks;
            }
            long total = 0L;
            if (workTime.TicksByWorkType != null)
            {
                foreach (KeyValuePair<string, long> pair in workTime.TicksByWorkType)
                {
                    if (pair.Value > 0L)
                    {
                        total = SafeAdd(total, pair.Value);
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// 防溢出的 long 加法：溢出时钳到 long.MaxValue，而不是回绕成负数。
        /// </summary>
        /// <remarks>工时是长期累加值，一旦回绕会让排名与占比整体反转，因此所有工时累加都必须走这里。</remarks>
        private static long SafeAdd(long left, long right)
        {
            return right > 0L && left > long.MaxValue - right
                ? long.MaxValue
                : left + right;
        }

    }
}

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
    /// Partial of ArchiveService - colony work aggregate, work-type leaderboard, and shared helpers.
    /// </summary>
    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
    {

        /// <summary>
        /// 获取全殖民地工时聚合：总工时、日均工时、各工种分布与峰值。
        /// </summary>
        /// <returns>聚合视图；组件缺失时返回零值空聚合（不是 null）。</returns>
        /// <remarks>
        /// 结果按 DataRevision + 时间窗缓存，避免逐帧重算 O(成员数 × 工种数)。
        /// 缓存键额外校验组件引用（ReferenceEquals）：读档或换档导致组件重建时会自然失效。
        /// 去掉该项校验会让窗口在换档后仍读到旧组件的陈旧数据，勿删。
        /// </remarks>
        public ColonyWorkAggregateView GetColonyWorkAggregate()
        {
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return EmptyColonyWorkAggregate();
            }

            long tick = GenTicks.TicksGame;
            if (cachedColonyWorkAggregate != null
                && ReferenceEquals(workIntensityCacheComponent, component)
                && workIntensityCacheRevision == component.DataRevision
                && tick - workIntensityCacheTick <= WorkIntensityCacheWindow)
            {
                return cachedColonyWorkAggregate;
            }

            cachedColonyWorkAggregate = BuildColonyWorkAggregate(component);
            workIntensityCacheComponent = component;
            workIntensityCacheRevision = component.DataRevision;
            workIntensityCacheTick = tick;
            return cachedColonyWorkAggregate;
        }

        /// <summary>
        /// 遍历当前殖民地成员，构建全殖民地工时聚合。
        /// </summary>
        /// <param name="component">数据组件，用于读取各成员的归档对象。</param>
        /// <returns>含总工时、日均、各工种总量/峰值/参与人数的聚合视图。</returns>
        /// <remarks>
        /// 复杂度为 O(成员数 × 工种数)，因此结果由 GetColonyWorkAggregate 缓存，不要在其它热路径直接调用。
        /// 日均口径是"各成员日均的算术平均"，不是"总工时 / 总人天"——后者会被新成员稀释，与 UI 文案不符。
        /// 只有累计工时大于 0 的成员才计入 participantsWithWork，避免未干活的人把日均拉低。
        /// </remarks>
        private ColonyWorkAggregateView BuildColonyWorkAggregate(
            ChronicleGameComponent component)
        {
            Dictionary<string, long> totals = new Dictionary<string, long>();
            Dictionary<string, long> maxima = new Dictionary<string, long>();
            Dictionary<string, int> participantCounts = new Dictionary<string, int>();
            List<ColonyMember> people = ChronicleColonistScanner.EnumerateCurrentPeople();
            int participantsWithWork = 0;
            long totalTicks = 0L;
            double dailySum = 0d;
            int dailyCount = 0;

            // 三个按工种聚合的累加器：总量、单人峰值、参与人数。
            for (int i = 0; i < people.Count; i++)
            {
                ColonyMember member = people[i];
                if (member == null || member.Pawn == null)
                {
                    continue;
                }
                PawnObject pawn = component.GetObject(member.Pawn.GetUniqueLoadID()) as PawnObject;
                if (pawn == null || pawn.WorkTime == null)
                {
                    continue;
                }
                long pawnTotal = GetTotalWorkTicks(pawn.WorkTime);
                if (pawnTotal <= 0L)
                {
                    // 从未干过活的人必须排除，否则会成为日均分母把平均值压向 0。
                    continue;
                }
                participantsWithWork++;
                totalTicks = SafeAdd(totalTicks, pawnTotal);
                long firstTick = pawn.JoinTick >= 0L
                    ? pawn.JoinTick : pawn.WorkTime.FirstObservedTick;
                double days = GetObservedDays(pawn, firstTick);
                if (days > 0d)
                {
                    // 单人日均小时数；最终对 dailyCount 个成员取算术平均，
                    // 而不是用总人天去除总工时（后者会被新入队成员稀释）。
                    dailySum += pawnTotal / (double)RimWorld.GenDate.TicksPerHour / days;
                    dailyCount++;
                }

                if (pawn.WorkTime.TicksByWorkType == null)
                {
                    continue;
                }
                foreach (KeyValuePair<string, long> pair in pawn.WorkTime.TicksByWorkType)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0L)
                    {
                        continue;
                    }
                    long currentTotal;
                    totals.TryGetValue(pair.Key, out currentTotal);
                    totals[pair.Key] = SafeAdd(currentTotal, pair.Value);
                    long currentMax;
                    maxima.TryGetValue(pair.Key, out currentMax);
                    if (pair.Value > currentMax)
                    {
                        maxima[pair.Key] = pair.Value;
                    }
                    int count;
                    participantCounts.TryGetValue(pair.Key, out count);
                    participantCounts[pair.Key] = count + 1;
                }
            }

            List<string> names = new List<string>(totals.Keys);
            names.Sort((a, b) => totals[b].CompareTo(totals[a]));
            List<ColonyWorkTypeView> workTypes = new List<ColonyWorkTypeView>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                workTypes.Add(new ColonyWorkTypeView(
                    name,
                    totals[name],
                    participantCounts[name],
                    maxima[name]));
            }
            return new ColonyWorkAggregateView(
                people.Count,
                participantsWithWork,
                totalTicks,
                totalTicks / (double)RimWorld.GenDate.TicksPerHour,
                dailyCount > 0 ? dailySum / dailyCount : 0d,
                workTypes);
        }

        /// <summary>
        /// 构建"工种到全成员工时样本序列"的排行榜，供个人名次计算使用。
        /// </summary>
        /// <returns>键为工种 defName，值为已降序排列的工时样本列表。</returns>
        /// <remarks>
        /// 每个工种在构建时就排序一次，避免计算名次时反复排序。
        /// RankOf 依赖此处已降序的前提，改动排序方向会直接把名次算反。
        /// </remarks>
        private Dictionary<string, List<long>> BuildWorkTypeLeaderboard()
        {
            Dictionary<string, List<long>> result = new Dictionary<string, List<long>>();
            List<ColonyMember> people = ChronicleColonistScanner.EnumerateCurrentPeople();
            ChronicleGameComponent component = Component;
            if (component == null)
            {
                return result;
            }
            for (int i = 0; i < people.Count; i++)
            {
                ColonyMember member = people[i];
                if (member == null || member.Pawn == null)
                {
                    continue;
                }
                PawnObject pawn = component.GetObject(member.Pawn.GetUniqueLoadID()) as PawnObject;
                if (pawn == null || pawn.WorkTime == null || pawn.WorkTime.TicksByWorkType == null)
                {
                    continue;
                }
                foreach (KeyValuePair<string, long> pair in pawn.WorkTime.TicksByWorkType)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0L)
                    {
                        continue;
                    }
                    List<long> values;
                    if (!result.TryGetValue(pair.Key, out values))
                    {
                        values = new List<long>();
                        result[pair.Key] = values;
                    }
                    values.Add(pair.Value);
                }
            }
            foreach (List<long> values in result.Values)
            {
                values.Sort((a, b) => b.CompareTo(a));
            }
            return result;
        }

        private static ColonyWorkTypeView FindColonyWorkType(
            ColonyWorkAggregateView aggregate,
            string workTypeDefName)
        {
            if (aggregate == null || aggregate.WorkTypes == null)
            {
                return null;
            }
            for (int i = 0; i < aggregate.WorkTypes.Count; i++)
            {
                ColonyWorkTypeView item = aggregate.WorkTypes[i];
                if (item != null && item.WorkTypeDefName == workTypeDefName)
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>
        /// 在已降序的样本列表中计算给定 ticks 的名次（1 为最高）。
        /// </summary>
        /// <remarks>前提：values 已降序。并列时共享较小的名次。</remarks>
        private static int RankOf(List<long> values, long ticks)
        {
            int rank = 1;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] > ticks)
                {
                    rank++;
                }
            }
            return rank;
        }

        /// <summary>
        /// 工种行排序：ticks 降序，相同则按工种名升序。
        /// </summary>
        /// <remarks>名称兜底是为了让排序稳定可复现——List.Sort 不稳定，
        /// 若去掉名称比较，等工时的行会在每次刷新时抖动换位。</remarks>
        private static int CompareWorkRows(
            KeyValuePair<string, long> a,
            KeyValuePair<string, long> b)
        {
            int ticks = b.Value.CompareTo(a.Value);
            return ticks != 0
                ? ticks
                : string.Compare(a.Key, b.Key, StringComparison.Ordinal);
        }

        /// <summary>
        /// 计算已观察天数，作为日均工时的分母。
        /// </summary>
        /// <param name="pawn">目标 pawn 归档对象。</param>
        /// <param name="firstTick">观察起点 tick；为负数时视为无效并直接返回 0。</param>
        /// <returns>观察天数；返回 0 表示窗口不足以计算日均。</returns>
        /// <remarks>
        /// 终点取 DeathTick（仅限已归档）或当前 tick。调用方拿到 0 时必须跳过日均计算，
        /// 否则除零会得到 Infinity / NaN 并污染聚合结果。
        /// </remarks>
        private static double GetObservedDays(PawnObject pawn, long firstTick)
        {
            if (pawn == null || firstTick < 0L)
            {
                return 0d;
            }
            long endTick = pawn.IsArchived && pawn.DeathTick > firstTick
                ? pawn.DeathTick
                : GenTicks.TicksGame;
            if (endTick <= firstTick)
            {
                return 0d;
            }
            return (endTick - firstTick) / (double)RimWorld.GenDate.TicksPerDay;
        }

        private static ColonyWorkAggregateView EmptyColonyWorkAggregate()
        {
            return new ColonyWorkAggregateView(0, 0, 0L, 0d, 0d,
                new List<ColonyWorkTypeView>());
        }

    }
}

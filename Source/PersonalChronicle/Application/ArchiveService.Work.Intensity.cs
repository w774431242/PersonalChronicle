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
    /// Partial of ArchiveService - work-intensity evaluation, tier lookup, colony rank, work-type breakdown.
    /// </summary>
    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
    {

        /// <summary>
        /// 评估单个 pawn 的劳动强度档位，并给出全殖民地累计工时排名。
        /// </summary>
        /// <param name="stableId">pawn 稳定 ID。</param>
        /// <returns>含评估结果与档位定义的视图；数据不足时返回 Undefined 评估且 tier 为 null。</returns>
        /// <remarks>
        /// 观察天数起点优先取 JoinTick（入队时刻），缺失时才回退到 WorkTime.FirstObservedTick——
        /// 只有用 JoinTick 才能让"日均工时"对新老成员保持同一口径。
        /// 已归档的 pawn 用 DeathTick 截断观察窗口，否则死后时间会继续计入分母、把日均越拉越低。
        /// </remarks>
        public WorkIntensityView GetWorkIntensity(string stableId)
        {
            PawnObject pawn = GetObject(stableId) as PawnObject;
            if (pawn == null || pawn.WorkTime == null)
            {
                return new WorkIntensityView(
                    WorkIntensityEvaluation.Undefined(null, "builtin"), null);
            }

            long totalTicks = GetTotalWorkTicks(pawn.WorkTime);
            long firstTick = pawn.JoinTick >= 0L
                ? pawn.JoinTick
                : pawn.WorkTime.FirstObservedTick;
            double observedDays = GetObservedDays(pawn, firstTick);
            if (totalTicks <= 0L || observedDays <= 0d)
            {
                return new WorkIntensityView(
                    WorkIntensityEvaluation.Undefined(
                        new WorkIntensityInput(0L, 0d, observedDays, 0d),
                        "builtin"),
                    null);
            }

            ColonyWorkAggregateView aggregate = GetColonyWorkAggregate();
            double totalHours = totalTicks / (double)RimWorld.GenDate.TicksPerHour;
            WorkIntensityInput input = new WorkIntensityInput(
                totalTicks,
                totalHours,
                observedDays,
                aggregate != null ? aggregate.AverageDailyHours : 0d);
            WorkIntensityEvaluation evaluation = EvaluateWithProviders(input);
            WorkIntensityTierSpec tier = FindTier(evaluation != null ? evaluation.TierDefName : null);
            // v4.15 condense-tab: rank this pawn's accumulated hours among all
            // current colony members (for the "全殖民地前几" digest cell).
            int rank = 0;
            int population = 0;
            ComputeColonyWorkRank(totalTicks, out rank, out population);
            return new WorkIntensityView(evaluation, tier, rank, population);
        }

        /// <summary>
        /// 计算该 pawn 的累计工时在全殖民地成员中的名次。
        /// </summary>
        /// <param name="ownTicks">该 pawn 的累计工时 ticks。</param>
        /// <param name="rank">输出名次，1 为最高。</param>
        /// <param name="population">输出参与统计的成员总数。</param>
        /// <remarks>
        /// 只有严格大于才计数，因此并列者共享较小的名次（如两人并列第 1，则下一人是第 3）。
        /// UI 展示"全殖民地前几"时直接取 rank 即可，无需再去重。
        /// </remarks>
        private void ComputeColonyWorkRank(long ownTicks, out int rank, out int population)
        {
            rank = 0;
            population = 0;
            List<ColonyMember> people = ChronicleColonistScanner.EnumerateCurrentPeople();
            if (people == null) return;
            population = people.Count;
            int better = 0;
            for (int i = 0; i < people.Count; i++)
            {
                ColonyMember member = people[i];
                if (member == null || member.Pawn == null) continue;
                PawnObject other = GetObject(member.Pawn.GetUniqueLoadID()) as PawnObject;
                if (other == null || other.WorkTime == null) continue;
                long otherTicks = GetTotalWorkTicks(other.WorkTime);
                if (otherTicks > ownTicks) better++;
            }
            rank = better + 1;
        }

        /// <summary>
        /// 列出劳动强度策略中定义的全部档位，供图例与说明文本使用。
        /// </summary>
        /// <returns>按策略定义顺序排列的档位视图集合。</returns>
        /// <remarks>档位本身来自 XML 定义（数据驱动），此处不排序、不筛选，顺序即展示顺序。</remarks>
        public IReadOnlyList<WorkIntensityTierView> GetIntensityTiers()
        {
            IReadOnlyList<WorkIntensityTierSpec> tiers = GetWorkIntensityPolicy().Tiers;
            List<WorkIntensityTierView> result = new List<WorkIntensityTierView>(tiers.Count);
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i] != null)
                {
                    result.Add(new WorkIntensityTierView(tiers[i]));
                }
            }
            return result;
        }

        /// <summary>
        /// 拆解单个 pawn 的工种工时，给出占比、全殖民地名次与相对峰值的比例。
        /// </summary>
        /// <param name="stableId">pawn 稳定 ID。</param>
        /// <param name="includeZeroWorkTypes">true 时把从未做过的工种补齐成 0 值行，便于 UI 展示完整表格。</param>
        /// <returns>按 ticks 降序、同 ticks 时按工种名升序（保证排序稳定）的行集合。</returns>
        /// <remarks>
        /// 名次基于全殖民地该工种的样本序列（BuildWorkTypeLeaderboard），序列长度即分母；
        /// 因此返回的 population 指"做过该工种的人数"，不是殖民地总人口，展示文案需区分。
        /// </remarks>
        public IReadOnlyList<WorkIntensityWorkTypeView> GetWorkTypeBreakdown(
            string stableId,
            bool includeZeroWorkTypes)
        {
            PawnObject pawn = GetObject(stableId) as PawnObject;
            if (pawn == null || pawn.WorkTime == null)
            {
                return new List<WorkIntensityWorkTypeView>();
            }

            ColonyWorkAggregateView aggregate = GetColonyWorkAggregate();
            Dictionary<string, List<long>> leaderboard = BuildWorkTypeLeaderboard();
            Dictionary<string, long> ownTicks = new Dictionary<string, long>();
            Dictionary<string, long> ownLastTicks = new Dictionary<string, long>();
            if (pawn.WorkTime.TicksByWorkType != null)
            {
                foreach (KeyValuePair<string, long> pair in pawn.WorkTime.TicksByWorkType)
                {
                    if (!string.IsNullOrEmpty(pair.Key) && pair.Value > 0L)
                    {
                        ownTicks[pair.Key] = pair.Value;
                        long lastTick = 0L;
                        if (pawn.WorkTime.LastTickByWorkType != null)
                        {
                            pawn.WorkTime.LastTickByWorkType.TryGetValue(pair.Key, out lastTick);
                        }
                        ownLastTicks[pair.Key] = lastTick;
                    }
                }
            }

            if (includeZeroWorkTypes)
            {
                List<WorkTypeDef> workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading;
                for (int i = 0; i < workTypes.Count; i++)
                {
                    WorkTypeDef workType = workTypes[i];
                    if (workType != null && !ownTicks.ContainsKey(workType.defName))
                    {
                        ownTicks[workType.defName] = 0L;
                        ownLastTicks[workType.defName] = 0L;
                    }
                }
            }

            List<KeyValuePair<string, long>> rows =
                new List<KeyValuePair<string, long>>(ownTicks);
            rows.Sort(CompareWorkRows);
            long ownTotal = GetTotalWorkTicks(pawn.WorkTime);
            List<WorkIntensityWorkTypeView> result =
                new List<WorkIntensityWorkTypeView>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                string workTypeDefName = rows[i].Key;
                long ticks = rows[i].Value;
                ColonyWorkTypeView colonyType = FindColonyWorkType(
                    aggregate, workTypeDefName);
                List<long> values;
                leaderboard.TryGetValue(workTypeDefName, out values);
                int rank = ticks > 0L && values != null ? RankOf(values, ticks) : 0;
                int populationCount = values != null ? values.Count : 0;
                long maximum = colonyType != null ? colonyType.MaximumPawnTicks : 0L;
                float relative = maximum > 0L ? (float)ticks / maximum : 0f;
                float share = ownTotal > 0L ? (float)ticks / ownTotal : 0f;
                long lastTick;
                ownLastTicks.TryGetValue(workTypeDefName, out lastTick);
                result.Add(new WorkIntensityWorkTypeView(
                    workTypeDefName,
                    ticks,
                    lastTick,
                    share,
                    rank,
                    populationCount,
                    colonyType != null ? colonyType.TotalTicks : 0L,
                    maximum,
                    relative));
            }
            return result;
        }

        /// <summary>
        /// 惰性加载并缓存劳动强度策略（来自 XML 定义，数据驱动）。
        /// </summary>
        /// <remarks>策略仅在首次访问时加载一次。若要支持热重载，必须同时清空本字段
        /// 与 GetColonyWorkAggregate 的缓存，只清其一会读到半新半旧的数据。</remarks>
        private WorkIntensityPolicySnapshot GetWorkIntensityPolicy()
        {
            if (workIntensityPolicy == null)
            {
                workIntensityPolicy = WorkIntensityDefCatalog.Load();
            }
            return workIntensityPolicy;
        }

        /// <summary>
        /// 依次询问劳动强度 provider 责任链，取第一个给出评估的结果。
        /// </summary>
        /// <param name="input">评估入参：总 ticks、总小时数、观察天数、殖民地日均小时。</param>
        /// <returns>首个成功的评估结果；全部 provider 未命中时回退到内置 WorkIntensityEvaluator。</returns>
        /// <remarks>
        /// 单个 provider 抛异常不会中断整条链：捕获后按 ProviderId 去重、只警告一次，然后继续下一个。
        /// 这样外部扩展（其他 mod 提供的 provider）失效时只是降级，不会把 UI 或存档打挂。
        /// </remarks>
        private WorkIntensityEvaluation EvaluateWithProviders(WorkIntensityInput input)
        {
            IReadOnlyList<IWorkIntensityProvider> providers = workIntensityProviders.Providers;
            for (int i = 0; i < providers.Count; i++)
            {
                IWorkIntensityProvider provider = providers[i];
                if (provider == null)
                {
                    continue;
                }
                try
                {
                    WorkIntensityEvaluation evaluation;
                    if (provider.TryEvaluate(input, out evaluation) && evaluation != null)
                    {
                        return evaluation;
                    }
                }
                catch (Exception ex)
                {
                    if (warnedIntensityProviders.Add(provider.ProviderId))
                    {
                        ChronicleLog.Warning(ChronicleLog.Category.Provider, "work-intensity provider failed once, fallback to next provider: "
                            + provider.ProviderId + " / " + ex.Message);
                    }
                }
            }
            return WorkIntensityEvaluator.Evaluate(input, GetWorkIntensityPolicy());
        }

        /// <summary>
        /// 按 defName 查找强度档位定义。
        /// </summary>
        /// <param name="tierDefName">档位 defName；为空时直接返回 null。</param>
        /// <returns>优先返回策略内档位；策略未命中时回退到 WorkIntensityDefCatalog 单独加载外部档位。</returns>
        /// <remarks>回退分支用于支持其他 mod 注入的档位，删除会破坏跨 mod 扩展能力。</remarks>
        private WorkIntensityTierSpec FindTier(string tierDefName)
        {
            if (string.IsNullOrEmpty(tierDefName))
            {
                return null;
            }
            IReadOnlyList<WorkIntensityTierSpec> tiers = GetWorkIntensityPolicy().Tiers;
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i] != null && tiers[i].DefName == tierDefName)
                {
                    return tiers[i];
                }
            }
            WorkIntensityTierSpec externalTier;
            return WorkIntensityDefCatalog.TryLoadTier(tierDefName, out externalTier)
                ? externalTier : null;
        }

    }
}

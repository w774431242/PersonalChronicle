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
    /// Partial of <see cref="ArchiveService"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
    {

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

        private WorkIntensityPolicySnapshot GetWorkIntensityPolicy()
        {
            if (workIntensityPolicy == null)
            {
                workIntensityPolicy = WorkIntensityDefCatalog.Load();
            }
            return workIntensityPolicy;
        }

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
                    continue;
                }
                participantsWithWork++;
                totalTicks = SafeAdd(totalTicks, pawnTotal);
                long firstTick = pawn.JoinTick >= 0L
                    ? pawn.JoinTick : pawn.WorkTime.FirstObservedTick;
                double days = GetObservedDays(pawn, firstTick);
                if (days > 0d)
                {
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

        private static int CompareWorkRows(
            KeyValuePair<string, long> a,
            KeyValuePair<string, long> b)
        {
            int ticks = b.Value.CompareTo(a.Value);
            return ticks != 0
                ? ticks
                : string.Compare(a.Key, b.Key, StringComparison.Ordinal);
        }

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

        private static long SafeAdd(long left, long right)
        {
            return right > 0L && left > long.MaxValue - right
                ? long.MaxValue
                : left + right;
        }

        private static ColonyWorkAggregateView EmptyColonyWorkAggregate()
        {
            return new ColonyWorkAggregateView(0, 0, 0L, 0d, 0d,
                new List<ColonyWorkTypeView>());
        }

        public IReadOnlyList<SkillArchiveView> GetSkillArchive(string stableId)
        {
            PawnObject pawnObject = GetObject(stableId) as PawnObject;
            if (pawnObject == null)
            {
                return new List<SkillArchiveView>();
            }
            Dictionary<string, int> join = pawnObject.SkillSnapshot ?? new Dictionary<string, int>();
            Dictionary<string, int> end = new Dictionary<string, int>();
            if (pawnObject.IsArchived && pawnObject.SkillSnapshotOnDeath != null && pawnObject.SkillSnapshotOnDeath.Count > 0)
            {
                foreach (KeyValuePair<string, int> pair in pawnObject.SkillSnapshotOnDeath)
                {
                    end[pair.Key] = pair.Value;
                }
            }
            else
            {
                Pawn live = GetLivePawn(stableId);
                if (live != null && live.skills != null && live.skills.skills != null)
                {
                    List<SkillRecord> skills = live.skills.skills;
                    for (int i = 0; i < skills.Count; i++)
                    {
                        SkillRecord skill = skills[i];
                        if (skill != null && skill.def != null)
                        {
                            end[skill.def.defName] = skill.Level;
                        }
                    }
                }
                else if (join.Count > 0)
                {
                    foreach (KeyValuePair<string, int> pair in join)
                    {
                        end[pair.Key] = pair.Value;
                    }
                }
            }

            HashSet<string> keys = new HashSet<string>();
            foreach (string k in join.Keys) keys.Add(k);
            foreach (string k in end.Keys) keys.Add(k);
            List<SkillArchiveView> result = new List<SkillArchiveView>();
            foreach (string key in keys)
            {
                int j = 0;
                int e = 0;
                join.TryGetValue(key, out j);
                end.TryGetValue(key, out e);
                result.Add(new SkillArchiveView(key, j, e));
            }
            result.Sort((a, b) => b.Delta.CompareTo(a.Delta));
            return result;
        }


    }
}

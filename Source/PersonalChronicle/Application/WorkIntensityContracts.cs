using System.Collections.Generic;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Application
{
    /// <summary>Read-only intensity result for UI and optional integrations.</summary>
    public sealed class WorkIntensityView
    {
        public readonly bool IsDefined;
        public readonly string TierDefName;
        public readonly string DisplayCode;
        public readonly string LabelKey;
        public readonly string TagKey;
        public readonly string ColorHex;
        public readonly double TotalHours;
        public readonly double DailyHours;
        public readonly double WeeklyHours;
        public readonly double MonthlyHours;
        public readonly double ObservedDays;
        public readonly double RelativeRatio;
        public readonly bool IsOverloaded;
        public readonly bool IsSignificantlyIdle;
        /// <summary>
        /// True when ObservedDays is below the sample threshold. The tier is a
        /// forward projection ("预估"). False means a confirmed rating ("实际").
        /// </summary>
        public readonly bool IsEstimated;
        public readonly string ProviderId;

        public WorkIntensityView(
            WorkIntensityEvaluation evaluation,
            WorkIntensityTierSpec tier)
        {
            IsDefined = evaluation != null && evaluation.IsDefined;
            TierDefName = evaluation != null ? evaluation.TierDefName : null;
            DisplayCode = tier != null ? tier.DisplayCode : null;
            LabelKey = tier != null ? tier.LabelKey : null;
            TagKey = tier != null ? tier.TagKey : null;
            ColorHex = tier != null ? tier.ColorHex : null;
            TotalHours = evaluation != null ? evaluation.DailyHours * evaluation.ObservedDays : 0d;
            DailyHours = evaluation != null ? evaluation.DailyHours : 0d;
            WeeklyHours = evaluation != null ? evaluation.WeeklyHours : 0d;
            MonthlyHours = evaluation != null ? evaluation.MonthlyHours : 0d;
            ObservedDays = evaluation != null ? evaluation.ObservedDays : 0d;
            RelativeRatio = evaluation != null ? evaluation.RelativeRatio : 0d;
            IsOverloaded = evaluation != null && evaluation.IsOverloaded;
            IsSignificantlyIdle = evaluation != null && evaluation.IsSignificantlyIdle;
            IsEstimated = evaluation != null && evaluation.IsEstimated;
            ProviderId = evaluation != null ? evaluation.ProviderId : null;
        }
    }

    /// <summary>Presentation-safe tier metadata for the ladder renderer.</summary>
    public sealed class WorkIntensityTierView
    {
        public readonly string DefName;
        public readonly string DisplayCode;
        public readonly string LabelKey;
        public readonly string TagKey;
        public readonly string ColorHex;
        public readonly float MinimumDailyHours;

        public WorkIntensityTierView(WorkIntensityTierSpec tier)
        {
            DefName = tier != null ? tier.DefName : null;
            DisplayCode = tier != null ? tier.DisplayCode : null;
            LabelKey = tier != null ? tier.LabelKey : null;
            TagKey = tier != null ? tier.TagKey : null;
            ColorHex = tier != null ? tier.ColorHex : null;
            MinimumDailyHours = tier != null ? tier.MinimumDailyHours : 0f;
        }
    }

    /// <summary>One work type in a current-colony comparison.</summary>
    public sealed class WorkIntensityWorkTypeView
    {
        public readonly string WorkTypeDefName;
        public readonly long Ticks;
        public readonly long LastTick;
        public readonly float Share01;
        public readonly int Rank;
        public readonly int PopulationCount;
        public readonly long ColonyTotalTicks;
        public readonly long ColonyMaximumTicks;
        public readonly float RelativeToMaximum01;

        public WorkIntensityWorkTypeView(
            string workTypeDefName,
            long ticks,
            long lastTick,
            float share01,
            int rank,
            int populationCount,
            long colonyTotalTicks,
            long colonyMaximumTicks,
            float relativeToMaximum01)
        {
            WorkTypeDefName = workTypeDefName;
            Ticks = ticks;
            LastTick = lastTick;
            Share01 = share01;
            Rank = rank;
            PopulationCount = populationCount;
            ColonyTotalTicks = colonyTotalTicks;
            ColonyMaximumTicks = colonyMaximumTicks;
            RelativeToMaximum01 = relativeToMaximum01;
        }
    }

    /// <summary>Colony-level aggregate rebuilt from current pawn ledgers.</summary>
    public sealed class ColonyWorkAggregateView
    {
        public readonly int PopulationCount;
        public readonly int ParticipantsWithWork;
        public readonly long TotalWorkTicks;
        public readonly double TotalWorkHours;
        public readonly double AverageDailyHours;
        public readonly IReadOnlyList<ColonyWorkTypeView> WorkTypes;

        public ColonyWorkAggregateView(
            int populationCount,
            int participantsWithWork,
            long totalWorkTicks,
            double totalWorkHours,
            double averageDailyHours,
            IReadOnlyList<ColonyWorkTypeView> workTypes)
        {
            PopulationCount = populationCount;
            ParticipantsWithWork = participantsWithWork;
            TotalWorkTicks = totalWorkTicks;
            TotalWorkHours = totalWorkHours;
            AverageDailyHours = averageDailyHours;
            WorkTypes = workTypes ?? new List<ColonyWorkTypeView>();
        }
    }

    public sealed class ColonyWorkTypeView
    {
        public readonly string WorkTypeDefName;
        public readonly long TotalTicks;
        public readonly int ParticipantCount;
        public readonly long MaximumPawnTicks;

        public ColonyWorkTypeView(
            string workTypeDefName,
            long totalTicks,
            int participantCount,
            long maximumPawnTicks)
        {
            WorkTypeDefName = workTypeDefName;
            TotalTicks = totalTicks;
            ParticipantCount = participantCount;
            MaximumPawnTicks = maximumPawnTicks;
        }
    }

    /// <summary>Typed, language-independent input for external providers.</summary>
    public sealed class WorkTimeSample
    {
        public readonly string PawnStableId;
        public readonly string WorkTypeDefName;
        public readonly long SampleTicks;
        public readonly long GameTick;
        public readonly string SourceId;

        public WorkTimeSample(
            string pawnStableId,
            string workTypeDefName,
            long sampleTicks,
            long gameTick,
            string sourceId)
        {
            PawnStableId = pawnStableId;
            WorkTypeDefName = workTypeDefName;
            SampleTicks = sampleTicks;
            GameTick = gameTick;
            SourceId = sourceId;
        }
    }

    /// <summary>
    /// Optional external evaluator. Providers should return a tier Def name
    /// supplied by their own XML and never return localized display text.
    /// </summary>
    public interface IWorkIntensityProvider
    {
        string ProviderId { get; }
        int Priority { get; }
        bool TryEvaluate(WorkIntensityInput input, out WorkIntensityEvaluation evaluation);
    }

    public interface IWorkIntensityProviderRegistry
    {
        bool Register(IWorkIntensityProvider provider);
        IReadOnlyList<IWorkIntensityProvider> Providers { get; }
    }

    /// <summary>
    /// Instance-owned registry: no global mutable static collection. Providers
    /// are re-created by their owning mod on each game process.
    /// </summary>
    public sealed class WorkIntensityProviderRegistry : IWorkIntensityProviderRegistry
    {
        private readonly List<IWorkIntensityProvider> providers =
            new List<IWorkIntensityProvider>();

        public IReadOnlyList<IWorkIntensityProvider> Providers
        {
            get { return providers; }
        }

        public bool Register(IWorkIntensityProvider provider)
        {
            if (provider == null || string.IsNullOrEmpty(provider.ProviderId))
            {
                return false;
            }
            for (int i = providers.Count - 1; i >= 0; i--)
            {
                if (providers[i] != null && providers[i].ProviderId == provider.ProviderId)
                {
                    providers.RemoveAt(i);
                }
            }
            providers.Add(provider);
            providers.Sort(CompareProviders);
            return true;
        }

        private static int CompareProviders(IWorkIntensityProvider a, IWorkIntensityProvider b)
        {
            if (ReferenceEquals(a, b))
            {
                return 0;
            }
            if (a == null)
            {
                return 1;
            }
            if (b == null)
            {
                return -1;
            }
            int priority = b.Priority.CompareTo(a.Priority);
            return priority != 0
                ? priority
                : string.Compare(a.ProviderId, b.ProviderId, System.StringComparison.Ordinal);
        }
    }

    /// <summary>New append-only service contract for career intensity.</summary>
    public interface IWorkIntensityService
    {
        WorkIntensityView GetWorkIntensity(string stableId);
        IReadOnlyList<WorkIntensityTierView> GetIntensityTiers();
        ColonyWorkAggregateView GetColonyWorkAggregate();
        IReadOnlyList<WorkIntensityWorkTypeView> GetWorkTypeBreakdown(
            string stableId,
            bool includeZeroWorkTypes);
    }

    /// <summary>Append-only capture contract for third-party work systems.</summary>
    public interface IWorkTimeCaptureService
    {
        bool RecordSample(WorkTimeSample sample);
    }
}

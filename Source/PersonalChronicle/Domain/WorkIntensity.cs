using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>Runtime, language-independent tier specification.</summary>
    public sealed class WorkIntensityTierSpec
    {
        public readonly string DefName;
        public readonly string TierKey;
        public readonly string DisplayCode;
        public readonly float MinimumDailyHours;
        public readonly string LabelKey;
        public readonly string TagKey;
        public readonly string ColorHex;
        public readonly int Order;

        public WorkIntensityTierSpec(
            string defName,
            string tierKey,
            string displayCode,
            float minimumDailyHours,
            string labelKey,
            string tagKey,
            string colorHex,
            int order)
        {
            DefName = defName;
            TierKey = tierKey;
            DisplayCode = displayCode;
            MinimumDailyHours = minimumDailyHours;
            LabelKey = labelKey;
            TagKey = tagKey;
            ColorHex = colorHex;
            Order = order;
        }
    }

    /// <summary>Validated runtime policy read from WorkIntensityPolicyDef.</summary>
    public sealed class WorkIntensityPolicySnapshot
    {
        public readonly int MinimumSampleDays;
        public readonly float OverloadRatio;
        public readonly float SlackRatio;
        public readonly IReadOnlyList<WorkIntensityTierSpec> Tiers;

        public WorkIntensityPolicySnapshot(
            int minimumSampleDays,
            float overloadRatio,
            float slackRatio,
            IReadOnlyList<WorkIntensityTierSpec> tiers)
        {
            MinimumSampleDays = Math.Max(0, minimumSampleDays);
            OverloadRatio = overloadRatio > 0f ? overloadRatio : 0f;
            SlackRatio = slackRatio > 0f ? slackRatio : 0f;
            Tiers = tiers ?? new List<WorkIntensityTierSpec>();
        }
    }

    /// <summary>Pure input contract for built-in and external evaluators.</summary>
    public sealed class WorkIntensityInput
    {
        public readonly long TotalWorkTicks;
        public readonly double TotalWorkHours;
        public readonly double ObservedDays;
        public readonly double ColonyAverageDailyHours;

        public WorkIntensityInput(
            long totalWorkTicks,
            double totalWorkHours,
            double observedDays,
            double colonyAverageDailyHours)
        {
            TotalWorkTicks = totalWorkTicks;
            TotalWorkHours = totalWorkHours;
            ObservedDays = observedDays;
            ColonyAverageDailyHours = colonyAverageDailyHours;
        }
    }

    /// <summary>
    /// Language-independent result. UI resolves the returned Def/translation
    /// keys; no rendered text is persisted or passed through this type.
    /// </summary>
    public sealed class WorkIntensityEvaluation
    {
        public readonly bool IsDefined;
        public readonly string TierDefName;
        public readonly double DailyHours;
        public readonly double WeeklyHours;
        public readonly double MonthlyHours;
        public readonly double ObservedDays;
        public readonly double RelativeRatio;
        public readonly bool IsOverloaded;
        public readonly bool IsSignificantlyIdle;
        /// <summary>
        /// True when ObservedDays is below MinimumSampleDays. The tier is still
        /// computed from current data, but flagged as a forward projection
        /// ("预估") rather than a confirmed rating ("实际").
        /// </summary>
        public readonly bool IsEstimated;
        public readonly string ProviderId;

        public WorkIntensityEvaluation(
            bool isDefined,
            string tierDefName,
            double dailyHours,
            double weeklyHours,
            double monthlyHours,
            double observedDays,
            double relativeRatio,
            bool isOverloaded,
            bool isSignificantlyIdle,
            bool isEstimated,
            string providerId)
        {
            IsDefined = isDefined;
            TierDefName = tierDefName;
            DailyHours = dailyHours;
            WeeklyHours = weeklyHours;
            MonthlyHours = monthlyHours;
            ObservedDays = observedDays;
            RelativeRatio = relativeRatio;
            IsOverloaded = isOverloaded;
            IsSignificantlyIdle = isSignificantlyIdle;
            IsEstimated = isEstimated;
            ProviderId = providerId;
        }

        public static WorkIntensityEvaluation Undefined(WorkIntensityInput input, string providerId)
        {
            double daily = input != null && input.ObservedDays > 0d
                ? input.TotalWorkHours / input.ObservedDays : 0d;
            double ratio = input != null && input.ColonyAverageDailyHours > 0d
                ? daily / input.ColonyAverageDailyHours : 0d;
            return new WorkIntensityEvaluation(
                false,
                null,
                daily,
                daily * 7d,
                daily * 30d,
                input != null ? input.ObservedDays : 0d,
                ratio,
                false,
                false,
                false,
                providerId);
        }
    }

    /// <summary>
    /// Pure, deterministic evaluator. It has no RimWorld/UI dependency and can
    /// be reused by external providers or unit tests.
    /// </summary>
    public static class WorkIntensityEvaluator
    {
        public static WorkIntensityEvaluation Evaluate(
            WorkIntensityInput input,
            WorkIntensityPolicySnapshot policy)
        {
            if (input == null || policy == null)
            {
                return WorkIntensityEvaluation.Undefined(input, "builtin");
            }

            double daily = input.ObservedDays > 0d
                ? input.TotalWorkHours / input.ObservedDays : 0d;
            double ratio = input.ColonyAverageDailyHours > 0d
                ? daily / input.ColonyAverageDailyHours : 0d;
            bool overloaded = policy.OverloadRatio > 0f && ratio >= policy.OverloadRatio;
            bool idle = policy.SlackRatio > 0f && ratio > 0d && ratio <= policy.SlackRatio;

            if (input.TotalTicks <= 0L || policy.Tiers.Count == 0)
            {
                return WorkIntensityEvaluation.Undefined(input, "builtin");
            }
            // Below the minimum sample window we still compute a tier from the
            // current data, but flag it as an estimate ("预估") rather than a
            // confirmed rating ("实际").
            bool isEstimated = input.ObservedDays < policy.MinimumSampleDays;

            WorkIntensityTierSpec matched = null;
            for (int i = 0; i < policy.Tiers.Count; i++)
            {
                WorkIntensityTierSpec tier = policy.Tiers[i];
                if (tier != null && daily >= tier.MinimumDailyHours)
                {
                    matched = tier;
                    break;
                }
            }
            if (matched == null)
            {
                return new WorkIntensityEvaluation(
                    false,
                    null,
                    daily,
                    daily * 7d,
                    daily * 30d,
                    input.ObservedDays,
                    ratio,
                    overloaded,
                    idle,
                    isEstimated,
                    "builtin");
            }

            return new WorkIntensityEvaluation(
                true,
                matched.DefName,
                daily,
                daily * 7d,
                daily * 30d,
                input.ObservedDays,
                ratio,
                overloaded,
                idle,
                isEstimated,
                "builtin");
        }
    }
}

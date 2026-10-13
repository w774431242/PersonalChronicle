using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Domain.PawnEconomy.Production
{
    /// <summary>M10 production readout for one product category. Every input is a <see cref="Fact{T}"/>:
    /// a reading that has no real source stays explicitly unknown and is never defaulted to zero, so a
    /// missing domain can never masquerade as "no shortfall" (B6, no fabricated readings).</summary>
    internal sealed class ProductionReadout
    {
        public string CategoryKey { get; }
        public Fact<long> ConfirmedUndeliveredDemand { get; }
        public Fact<long> FreeStockTarget { get; }
        public Fact<long> AuthorisedUndeliveredExports { get; }
        public Fact<long> AvailableStock { get; }
        public Fact<long> CompletableWorkInProgress { get; }
        public Fact<long> AuthorisedNotStarted { get; }
        public ProductionReadout(string categoryKey,
            Fact<long> confirmedUndeliveredDemand, Fact<long> freeStockTarget, Fact<long> authorisedUndeliveredExports,
            Fact<long> availableStock, Fact<long> completableWorkInProgress, Fact<long> authorisedNotStarted)
        {
            CategoryKey = MinorCurrency.Required(categoryKey);
            ConfirmedUndeliveredDemand = confirmedUndeliveredDemand; FreeStockTarget = freeStockTarget;
            AuthorisedUndeliveredExports = authorisedUndeliveredExports; AvailableStock = availableStock;
            CompletableWorkInProgress = completableWorkInProgress; AuthorisedNotStarted = authorisedNotStarted;
        }
    }

    internal enum RestockOutcome
    {
        /// <summary>At least one input has no real source yet: no shortfall is claimed either way.</summary>
        Unknown = 0,
        /// <summary>All inputs known and the formula says the need is already covered.</summary>
        Covered = 1,
        /// <summary>All inputs known and a real production gap remains.</summary>
        Shortfall = 2,
        /// <summary>All inputs known but the arithmetic refuses the values (negative or overflow).</summary>
        Inconsistent = 3
    }

    /// <summary>One evaluated restock result. ReasonCode is a stable diagnostic ID; presentation maps it
    /// to localized text and never parses it.</summary>
    internal sealed class RestockEvaluation
    {
        public string CategoryKey { get; }
        public RestockOutcome Outcome { get; }
        public long ShortfallMinor { get; }
        public string ReasonCode { get; }
        private RestockEvaluation(string key, RestockOutcome outcome, long shortfall, string reason)
        {
            CategoryKey = key; Outcome = outcome; ShortfallMinor = shortfall; ReasonCode = reason;
        }
        public static RestockEvaluation Unknown(string key, string reason)
            => new RestockEvaluation(key, RestockOutcome.Unknown, 0, reason);
        public static RestockEvaluation Covered(string key)
            => new RestockEvaluation(key, RestockOutcome.Covered, 0, RestockReasons.Covered);
        public static RestockEvaluation ShortfallFor(string key, long amount)
            => new RestockEvaluation(key, RestockOutcome.Shortfall, amount, RestockReasons.Shortfall);
        public static RestockEvaluation Inconsistent(string key, string reason)
            => new RestockEvaluation(key, RestockOutcome.Inconsistent, 0, reason);
    }

    internal static class RestockReasons
    {
        public const string Covered = "RestockCovered";
        public const string Shortfall = "RestockShortfall";
        public const string NegativeInput = "RestockNegativeInput";
        public const string Overflow = "RestockOverflow";
    }

    /// <summary>M10 evaluation: the formula in <see cref="RestockPlanner"/> is applied only when every
    /// input is a real observation. A single unknown input yields <see cref="RestockOutcome.Unknown"/>
    /// with the first unknown reason — never a partial or zero-substituted calculation.</summary>
    internal static class RestockEvaluator
    {
        public static RestockEvaluation Evaluate(ProductionReadout readout)
        {
            if (readout == null) throw new ArgumentNullException(nameof(readout));
            if (!readout.ConfirmedUndeliveredDemand.TryGetValue(out long demand))
                return RestockEvaluation.Unknown(readout.CategoryKey, readout.ConfirmedUndeliveredDemand.ReasonCode);
            if (!readout.FreeStockTarget.TryGetValue(out long freeTarget))
                return RestockEvaluation.Unknown(readout.CategoryKey, readout.FreeStockTarget.ReasonCode);
            if (!readout.AuthorisedUndeliveredExports.TryGetValue(out long exports))
                return RestockEvaluation.Unknown(readout.CategoryKey, readout.AuthorisedUndeliveredExports.ReasonCode);
            if (!readout.AvailableStock.TryGetValue(out long stock))
                return RestockEvaluation.Unknown(readout.CategoryKey, readout.AvailableStock.ReasonCode);
            if (!readout.CompletableWorkInProgress.TryGetValue(out long wip))
                return RestockEvaluation.Unknown(readout.CategoryKey, readout.CompletableWorkInProgress.ReasonCode);
            if (!readout.AuthorisedNotStarted.TryGetValue(out long notStarted))
                return RestockEvaluation.Unknown(readout.CategoryKey, readout.AuthorisedNotStarted.ReasonCode);
            try
            {
                long gap = RestockPlanner.Shortfall(new RestockInput(demand, freeTarget, exports, stock, wip, notStarted));
                return gap <= 0 ? RestockEvaluation.Covered(readout.CategoryKey)
                    : RestockEvaluation.ShortfallFor(readout.CategoryKey, gap);
            }
            catch (ArgumentOutOfRangeException)
            {
                return RestockEvaluation.Inconsistent(readout.CategoryKey, RestockReasons.Overflow);
            }
        }
    }

    /// <summary>U6-A3：补货评估的**展示投影**（纯函数，可离线证伪）。两条铁律：
    /// ①**只有** `Shortfall` 才允许显示数值 —— Unknown / Inconsistent 一律只给原因码，绝不显示 0
    /// （0 会被读成"量到零"）；②展示是有界且**确定性**的（Ordinal 排序后取前 N），截断必须由调用方
    /// 明示"显示 N/总 M"，不得静默省略。程序逻辑从不解析这些码，只有表现层做映射。</summary>
    internal static class RestockPresentation
    {
        public const int MaximumRowsShown = 4;

        public static void Describe(RestockEvaluation evaluation, out bool hasNumber, out long amountMinor, out string reasonCode)
        {
            if (evaluation == null) throw new ArgumentNullException(nameof(evaluation));
            hasNumber = evaluation.Outcome == RestockOutcome.Shortfall;
            amountMinor = hasNumber ? evaluation.ShortfallMinor : 0;
            reasonCode = evaluation.ReasonCode;
        }

        public static IReadOnlyList<RestockEvaluation> Select(IReadOnlyList<RestockEvaluation> all, int maximum, out int total)
        {
            total = all == null ? 0 : all.Count;
            if (all == null || all.Count == 0 || maximum <= 0) return Array.Empty<RestockEvaluation>();
            var sorted = new List<RestockEvaluation>(all);
            sorted.Sort((left, right) => string.CompareOrdinal(left?.CategoryKey, right?.CategoryKey));
            if (sorted.Count > maximum) sorted.RemoveRange(maximum, sorted.Count - maximum);
            return sorted.AsReadOnly();
        }
    }
}

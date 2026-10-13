using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>Read-only planning explanation. Ratios preserve overflow; ticks distinguish account and plan sampling.</summary>
    internal sealed class NeedAccountModelView
    {
        public string DecisionLabel { get; }
        public string Reason { get; }
        public string Next { get; }
        public string Target { get; }
        public string ExecutionLabel { get; }
        public IReadOnlyList<NeedTextRow> Details { get; }
        public IReadOnlyList<NeedCycleLine> Cycles { get; }
        public float CoverageRatio { get; }
        public string CoverageText { get; }
        public string ThresholdText { get; }
        public int AccountSampledTick { get; }
        public int PlanSampledTick { get; }
        public string PlanCash { get; }
        public IReadOnlyList<NeedTextRow> LaborRows { get; }
        public NeedAccountModelView(string decision, string reason, string next, string target, string execution,
            IEnumerable<NeedTextRow> details, IEnumerable<NeedCycleLine> cycles, float coverageRatio,
            string coverageText, string thresholdText, int accountTick, int planTick, string planCash,
            IEnumerable<NeedTextRow> laborRows = null)
        {
            DecisionLabel = decision; Reason = reason; Next = next; Target = target; ExecutionLabel = execution;
            Details = PerceptionCollections.Freeze(details); Cycles = PerceptionCollections.Freeze(cycles);
            CoverageRatio = coverageRatio; CoverageText = coverageText; ThresholdText = thresholdText;
            AccountSampledTick = accountTick; PlanSampledTick = planTick; PlanCash = planCash;
            LaborRows = PerceptionCollections.Freeze(laborRows);
        }
    }
}

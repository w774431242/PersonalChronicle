using System;
using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Planning;

namespace PersonalChronicle.Application.PawnEconomy.Planning
{
    /// <summary>M7 priced consumption published for one subject and one planning window.
    /// A source without a real quote publishes nothing and reports complete=false;
    /// unknown is never substituted with MarketValue or a fixed daily meal count (M5).</summary>
    internal interface IRequirementCostSource
    {
        /// <param name="incompleteReason">LR-04：`complete=false` 时的**具体拒绝原因码**（完成时为空串）。
        /// 缺失原因一律回落 `CostEstimateUnavailable` —— 原因码只用于呈现，绝不作为行为判据。</param>
        IReadOnlyList<RequirementCostSlice> ReadCosts(PerceptionSubject subject, int windowStart, int windowEnd, out bool complete, out string incompleteReason);
    }

    /// <summary>M10 funded, executable work published for one subject.
    /// Current work priority is player assignment state and never a capability prohibition (M6).</summary>
    internal interface IWorkOpportunitySource
    {
        IReadOnlyList<WorkOpportunity> ReadOpportunities(PerceptionSubject subject, int now, int deadline, out bool complete);
    }

    /// <summary>Safe zero for both ports: nothing observed, nothing claimed.</summary>
    internal sealed class UnpublishedCostSource : IRequirementCostSource
    {
        public IReadOnlyList<RequirementCostSlice> ReadCosts(PerceptionSubject subject, int windowStart, int windowEnd, out bool complete, out string incompleteReason)
        { complete = false; incompleteReason = "CostEstimateUnavailable"; return Array.Empty<RequirementCostSlice>(); }
    }
    internal sealed class UnpublishedOpportunitySource : IWorkOpportunitySource
    {
        public IReadOnlyList<WorkOpportunity> ReadOpportunities(PerceptionSubject subject, int now, int deadline, out bool complete)
        { complete = false; return Array.Empty<WorkOpportunity>(); }
    }

    /// <summary>Single guarded entry point for planning inputs. Null rows, oversize publications and
    /// source exceptions degrade to "nothing published" and never to a partial publication that
    /// still looks complete.</summary>
    internal static class PlanningSourceGuard
    {
        public static void ReadCosts(IRequirementCostSource source, PerceptionSubject subject, int windowStart, int windowEnd,
            out IReadOnlyList<RequirementCostSlice> rows, out bool complete, out string incompleteReason)
        {
            rows = Array.Empty<RequirementCostSlice>(); complete = false; incompleteReason = "CostEstimateUnavailable";
            if (source == null || subject == null) return;
            try
            {
                var read = source.ReadCosts(subject, windowStart, windowEnd, out bool published, out string reason);
                // LR-04：把来源给出的**具体**拒绝原因带出去（判据本身仍只由 Accept 决定，原因不参与判定）。
                incompleteReason = string.IsNullOrEmpty(reason) ? "CostEstimateUnavailable" : reason;
                if (!Accept(read, published, PlanningPolicy.MaximumSlices)) return;
                rows = read; complete = true; incompleteReason = string.Empty;
            }
            catch (Exception) { rows = Array.Empty<RequirementCostSlice>(); complete = false; incompleteReason = "CostEstimateUnavailable"; }
        }
        public static void ReadOpportunities(IWorkOpportunitySource source, PerceptionSubject subject, int now, int deadline,
            out IReadOnlyList<WorkOpportunity> rows, out bool complete)
        {
            rows = Array.Empty<WorkOpportunity>(); complete = false;
            if (source == null || subject == null) return;
            try
            {
                var read = source.ReadOpportunities(subject, now, deadline, out bool published);
                if (!Accept(read, published, PlanningPolicy.MaximumCandidates)) return;
                rows = read; complete = true;
            }
            catch (Exception) { rows = Array.Empty<WorkOpportunity>(); complete = false; }
        }
        private static bool Accept<T>(IReadOnlyList<T> rows, bool complete, int limit)
        {
            if (!complete || rows == null || rows.Count > limit) return false;
            for (int i = 0; i < rows.Count; i++) if (rows[i] == null) return false;
            return true;
        }
    }
}

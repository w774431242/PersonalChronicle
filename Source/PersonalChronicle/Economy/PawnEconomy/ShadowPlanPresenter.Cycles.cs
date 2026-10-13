using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;

namespace PersonalChronicle.Economy.PawnEconomy
{
    internal sealed partial class ShadowPlanPresenter
    {
        public IReadOnlyList<NeedCycleLine> BuildCycles(ShadowPlan plan, int now = -1)
        {
            var rows = new List<NeedCycleLine>(4);
            bool stale = plan != null && now >= 0 && IsStale(plan, now);
            if (plan == null || stale || plan.Decision == ShadowDecision.OutOfScope || plan.Decision == ShadowDecision.Error)
            {
                var status = stale ? NeedCycleStatus.Stale : plan?.Decision == ShadowDecision.OutOfScope
                    ? NeedCycleStatus.NotApplicable : plan?.Decision == ShadowDecision.Error ? NeedCycleStatus.Blocked : NeedCycleStatus.NotAssessed;
                string reason = stale ? "Stale" : plan?.Reason ?? "NoPlan";
                for (int i = 0; i < 4; i++) rows.Add(Cycle((NeedCycleId)i, status, reason,
                    stale ? T("Stale") : plan == null ? T("Model.NoPlan") : T("Reason." + plan.Reason), plan, stale));
                return rows.AsReadOnly();
            }
            var active = new List<string>();
            bool allSatisfied = true, assessed = false, unknown = false;
            foreach (var goal in plan.Goals)
            {
                if (goal.Active) active.Add("P" + goal.Priority + " " + text.NeedLabel(goal.NeedId));
                if (goal.Gap == null || goal.Gap.TargetState != TargetState.Enabled) continue;
                assessed = true;
                float target = 0f;
                bool known = goal.Gap.ActualRatio.TryGetValue(out float actual) && goal.Gap.TargetRatio.TryGetValue(out target);
                if (!known) { unknown = true; allSatisfied = false; }
                else if (actual < target) allSatisfied = false;
            }
            bool native = plan.Decision == ShadowDecision.NativeYield || plan.Decision == ShadowDecision.NativeNeed;
            NeedCycleStatus instant = active.Count > 0 || native ? NeedCycleStatus.Pending
                : unknown ? NeedCycleStatus.WaitingFacts : assessed && allSatisfied ? NeedCycleStatus.Satisfied : assessed ? NeedCycleStatus.Pending : NeedCycleStatus.NotAssessed;
            rows.Add(Cycle(NeedCycleId.Instant, instant, native ? plan.Reason : active.Count > 0 ? "ActiveNeed" : assessed && !allSatisfied ? "GoalBelowTarget" : "NoActiveNeed",
                active.Count > 0 ? string.Join(" → ", active) : native ? T("Reason." + plan.Reason) : assessed && !allSatisfied && !unknown ? T("Model.GoalBelowTarget") : T("Cycle.NoActiveNeed"), plan,
                focused: native));
            var budget = plan.Budget;
            NeedCycleStatus reserve;
            if (budget == null || budget.State == BudgetState.Unknown) reserve = NeedCycleStatus.NotAssessed;
            else if (budget.State == BudgetState.Inconsistent) reserve = NeedCycleStatus.Blocked;
            else if (budget.State != BudgetState.Ready) reserve = NeedCycleStatus.WaitingFacts;
            else if (!budget.RefillActive.TryGetValue(out bool refill)) reserve = NeedCycleStatus.WaitingFacts;
            else if (refill) reserve = NeedCycleStatus.Pending;
            else if (budget.AvailableMinor.TryGetValue(out long cash) && budget.ReserveTargetMinor.TryGetValue(out long target) && cash >= target)
                reserve = NeedCycleStatus.Satisfied;
            else reserve = NeedCycleStatus.Feasible; // Hysteresis can leave a gap without requesting income.
            rows.Add(Cycle(NeedCycleId.Reserve, reserve, budget?.Reason ?? "NoBudget",
                budget == null ? T("Unknown") : T("Budget." + budget.State), plan,
                focused: plan.Decision == ShadowDecision.WaitingBudget || plan.Decision == ShadowDecision.MaintainReserve));
            NeedCycleStatus labour = plan.Decision == ShadowDecision.SeekWork ? NeedCycleStatus.Feasible
                : plan.Decision == ShadowDecision.WaitingOpportunity ? NeedCycleStatus.Pending
                : plan.Decision == ShadowDecision.WaitingBudget || plan.Decision == ShadowDecision.WaitingFacts ? NeedCycleStatus.WaitingFacts
                : plan.EmploymentApplicable.TryGetValue(out bool applicable) && !applicable ? NeedCycleStatus.NotApplicable
                : NeedCycleStatus.NotAssessed;
            rows.Add(Cycle(NeedCycleId.Labour, labour, plan.Reason,
                T("Model.CandidateCount", plan.Opportunities.Count), plan, focused: plan.Decision == ShadowDecision.SeekWork));
            if (plan.Options.Count == 0)
                rows.Add(Cycle(NeedCycleId.Consume, NeedCycleStatus.NotAssessed, "OptionsNotAssessed", T("Cycle.NotAssessed"), plan));
            else
            {
                var purchase = OptionOf(plan, PlanOptionKind.Purchase);
                var service = OptionOf(plan, PlanOptionKind.Service);
                bool feasible = purchase != null && purchase.Feasible || service != null && service.Feasible;
                bool unaffordable = !feasible && purchase?.ReasonCode == "ConsumptionUnaffordable";
                rows.Add(Cycle(NeedCycleId.Consume, feasible ? NeedCycleStatus.Feasible
                    : unaffordable ? NeedCycleStatus.Blocked : NeedCycleStatus.WaitingFacts,
                    feasible ? "FeasibleOption" : unaffordable ? purchase.ReasonCode : purchase?.ReasonCode ?? service?.ReasonCode ?? "OptionsNotAssessed",
                    OptionText(purchase, PlanOptionKind.Purchase) + " · " + OptionText(service, PlanOptionKind.Service), plan));
            }
            return rows.AsReadOnly();
        }

        private NeedCycleLine Cycle(NeedCycleId id, NeedCycleStatus status, string reason, string detail,
            ShadowPlan plan, bool stale = false, bool focused = false, string source = "ShadowPlan")
            => new NeedCycleLine(id, status, CycleName((int)id), T("Model.State." + status), detail, CycleTone(status),
                reason, source, plan?.BuiltTick ?? -1, stale, focused);
        private static NeedCycleTone CycleTone(NeedCycleStatus status)
            => status == NeedCycleStatus.Blocked ? NeedCycleTone.Blocked
                : status == NeedCycleStatus.Satisfied || status == NeedCycleStatus.Feasible || status == NeedCycleStatus.Executing ? NeedCycleTone.Ok
                : status == NeedCycleStatus.Pending || status == NeedCycleStatus.WaitingFacts || status == NeedCycleStatus.Stale ? NeedCycleTone.Pending
                : NeedCycleTone.Unknown;
    }
}

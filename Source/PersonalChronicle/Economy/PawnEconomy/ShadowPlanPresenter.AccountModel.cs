using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Planning;

namespace PersonalChronicle.Economy.PawnEconomy
{
    internal sealed partial class ShadowPlanPresenter
    {
        /// <summary>Compose sampled facts into an explanation; never computes a budget, dispatches work or changes a ledger.</summary>
        public NeedAccountModelView BuildModel(ShadowPlan plan, int now, FinancialAccountSnapshot account,
            WorkContract contract = null, IReadOnlyList<WageObligation> payables = null, ContractExitCode exit = ContractExitCode.None,
            LaborWorkflow workflow = null, IReadOnlyList<ProductBatch> batches = null,
            IReadOnlyList<LaborFlowEvent> history = null, string stationLabel = null)
        {
            if (plan != null && contract != null && !string.Equals(contract.PawnKey, plan.PawnId, StringComparison.Ordinal)) contract = null;
            if (workflow != null && plan != null && !string.Equals(workflow.PawnKey, plan.PawnId, StringComparison.Ordinal))
            { workflow = null; batches = null; history = null; }
            bool stale = IsStale(plan, now);
            string decision = T(plan == null ? "Model.NoPlan" : stale ? "Stale" : "Model.Decision." + plan.Decision);
            string reason = plan == null ? T("Model.NoPlan") : stale ? T("Stale") : T("Reason." + plan.Reason);
            string next = NextCondition(plan, stale);
            string target = FocusTarget(plan);
            var budget = plan?.Budget;
            string planCash = budget == null ? T("Unknown") : Money(budget.AvailableMinor);
            float ratio = -1f;
            string coverage;
            if (budget != null && budget.State == BudgetState.Ready && budget.ReserveTargetMinor.TryGetValue(out long reserve) && reserve == 0)
                coverage = T("Model.NoReserve");
            else if (budget != null && budget.State == BudgetState.Ready && budget.ReserveTargetMinor.TryGetValue(out reserve) && reserve > 0
                && budget.AvailableMinor.TryGetValue(out long cash) && cash >= 0)
            {
                ratio = (float)cash / reserve; // Preserve >100%; only drawing may clamp the fill.
                coverage = T("Model.Coverage", Money(cash), Money(reserve), ratio.ToString("P0", CultureInfo.CurrentCulture), Money(budget.IncomeGapMinor));
            }
            else coverage = T("Model.CoverageUnknown", budget == null ? T("Model.NoPlan") : T("Budget." + budget.State));
            if (stale) coverage = T("Model.SampledValue", coverage);
            string threshold = budget != null && budget.RefillTriggerMinor.TryGetValue(out long trigger)
                ? T("Model.Threshold", Money(trigger), Money(budget.ReserveTargetMinor)) : T("Model.ThresholdUnknown");
            string execution = ExecutionFeedback(plan, contract, payables, exit, now, out NeedCycleStatus? labourStatus, out string executionReason);
            var cycles = new List<NeedCycleLine>(BuildCycles(plan, now));
            // Real contract evidence refines only income feedback; the decision above keeps actual native control first.
            if (plan != null && !stale && plan.Decision != ShadowDecision.OutOfScope && plan.Decision != ShadowDecision.Error && labourStatus.HasValue)
                cycles[2] = new NeedCycleLine(NeedCycleId.Labour, labourStatus.Value, CycleName(2), T("Model.State." + labourStatus.Value),
                    execution, CycleTone(labourStatus.Value), executionReason, "EmploymentLedger", now, false,
                    plan.Decision == ShadowDecision.SeekWork);
            var rows = new List<NeedTextRow>(16)
            {
                new NeedTextRow(T("Model.Input"), T("Model.AccountSample", account == null ? T("Unknown") : Money(account.AvailableMinor), now)),
                new NeedTextRow(T("Model.PlanSample"), plan == null ? T("Model.NoPlan")
                    : T("Model.PlanSampleValue", planCash, plan.BuiltTick, Math.Max(0L, (long)now - plan.BuiltTick), stale ? T("Stale") : T("Model.Fresh"))),
                new NeedTextRow(T("Model.Focus"), target),
                new NeedTextRow(T("Model.Gap"), coverage),
                new NeedTextRow(T("Model.Trigger"), threshold),
                new NeedTextRow(T("Model.Arbitration"), decision),
                new NeedTextRow(T("Reason"), reason),
                new NeedTextRow(T("Model.NextLabel"), next),
                new NeedTextRow(T("Model.Execution"), execution),
                new NeedTextRow(T("Model.Job"), T("Model.Execution.Unconfirmed")),
            };
            if (plan != null)
            {
                rows.Add(new NeedTextRow(T("Model.Candidates"), T("Model.CandidateCount", plan.Opportunities.Count)));
                for (int i = 0; i < Math.Min(3, plan.Opportunities.Count); i++)
                {
                    var offer = plan.Opportunities[i];
                    rows.Add(new NeedTextRow(T("Model.Candidate", i + 1), T("Offer", Money(offer.WageMinor), Flag(offer.Capable),
                        Flag(offer.Executable), Flag(offer.Funded), offer.Reachable ? T("Yes") : T("No"))));
                }
                if (plan.Opportunities.Count > 3) rows.Add(new NeedTextRow(T("Model.CandidatesMore"), T("Model.CandidateMoreCount", plan.Opportunities.Count - 3)));
                if (!string.IsNullOrEmpty(plan.SelectedOpportunity)) rows.Add(new NeedTextRow(T("Opportunity"), plan.SelectedOpportunity));
            }
            if (contract != null)
            {
                if (contract.State != ContractState.Active || contract.ExpiresTick <= now)
                    rows.Add(new NeedTextRow(T("Model.Execution"), T("Model.RecentContract")));
                SettlementTotals(contract, payables, now, out long unpaidTotal, out long paidTotal, out bool _);
                rows.Add(new NeedTextRow(T("Model.SettlementEvidence"), T("Model.SettlementValue", Money(unpaidTotal), Money(paidTotal))));
                rows.Add(new NeedTextRow(T("Model.ContractEvidence"), T("Model.ContractValue", contract.ContractId, contract.SignedTick,
                    contract.ExpiresTick, contract.CompletedJobs, contract.MaxJobs)));
            }
            // CHG-20261011-LABOR-VISUAL（REQ-LaborVisual-017/018）：**劳动履约行** —— 真实阶段 + 合同短号 +
            // 付款模式 + 阻断原因 + 下一步。它让"已选择机会 ≠ 已签约、已签约 ≠ 正在工作、已交验 ≠ 已到账"
            // 在同一处可读，且**不掩盖**已持约待交货的事实（计划写着"等待收入机会"时这里仍说真话）。
            var laborRows = LaborPresentationRows.Compact(workflow, batches, payables, stationLabel);
            rows.AddRange(laborRows);
            if (workflow != null) rows.AddRange(LaborPresentationRows.History(history));
            return new NeedAccountModelView(decision, reason, next, target, execution, rows, cycles, ratio,
                coverage, threshold, account == null ? -1 : now, plan?.BuiltTick ?? -1, planCash, laborRows);
        }

        private string NextCondition(ShadowPlan plan, bool stale)
        {
            if (plan == null || stale) return T("Model.Next." + (stale ? "Stale" : "WaitingFacts"));
            if (plan.Decision == ShadowDecision.WaitingBudget)
            {
                switch (plan.Reason)
                {
                    case "FoodQuoteNoPublicStorage": case "FoodQuoteZoneNotEnabled": return T("Model.Next.FoodPublicSource");
                    case "FoodQuoteFoodUnreachable": case "FoodQuoteSourceUnreachable": return T("Model.Next.FoodAccess");
                    case "FoodQuoteFoodPendingTrade": return T("Model.Next.FoodTrade");
                    case "FoodQuoteOwnershipNotPublic": return T("Model.Next.FoodOwnership");
                    case "FoodQuoteNotEdible": case "FoodQuoteNoFood": return T("Model.Next.FoodSupply");
                    case "FoodQuoteUnpriced": return T("Model.Next.FoodPricing");
                }
                if (plan.Budget?.State == BudgetState.WaitingAccount) return T("Model.Next.Account");
                if (plan.Budget?.State == BudgetState.Inconsistent) return T("Model.Next.BudgetConsistency");
            }
            return T("Model.Next." + plan.Decision);
        }

        private string FocusTarget(ShadowPlan plan)
        {
            if (plan == null) return T("Model.FocusUnknown");
            ShadowNeedGoal chosen = null;
            foreach (var goal in plan.Goals)
                if (goal.Active && (chosen == null || goal.Priority < chosen.Priority)) chosen = goal;
            if (chosen != null) return "P" + chosen.Priority + " " + text.NeedLabel(chosen.NeedId);
            if (plan.Budget != null && plan.Budget.RefillActive.TryGetValue(out bool refill) && refill) return T("Model.FocusReserve");
            return T("Model.FocusNone");
        }

        private string ExecutionFeedback(ShadowPlan plan, WorkContract contract, IReadOnlyList<WageObligation> payables,
            ContractExitCode exit, int now, out NeedCycleStatus? status, out string reason)
        {
            status = null; reason = "ExecutionUnconfirmed";
            if (plan != null && (contract == null || !string.Equals(contract.PawnKey, plan.PawnId, StringComparison.Ordinal))) contract = null;
            if (contract == null)
                return plan?.Decision == ShadowDecision.SeekWork ? T("Model.Execution.Selected") : T("Model.Execution.Unconfirmed");
            SettlementTotals(contract, payables, now, out long due, out long paid, out bool shortfall);
            if (due > 0)
            {
                status = shortfall ? NeedCycleStatus.Blocked : NeedCycleStatus.Pending;
                reason = shortfall ? "PaymentShortfall" : "WageDue";
                return T(shortfall ? "Model.Execution.Shortfall" : "Model.Execution.Due", Money(due));
            }
            if (contract.State == ContractState.Terminated || exit != ContractExitCode.None || contract.ExpiresTick <= now)
            {
                status = NeedCycleStatus.Blocked; reason = "ContractEnded";
                string ended = T("Model.Execution.Ended", exit == ContractExitCode.None ? T("Model.Execution.Closed") : T("Employment.Exit." + exit));
                return paid > 0 ? ended + " · " + T("Model.Execution.Paid", Money(paid)) : ended;
            }
            bool active = contract.State == ContractState.Active && contract.SignedTick <= now && contract.ExpiresTick > now
                && contract.CompletedJobs < contract.MaxJobs;
            if (active)
            {
                status = NeedCycleStatus.Pending; reason = "ContractActiveJobUnconfirmed";
                return T("Model.Execution.Contract"); // Work authorization alone is not evidence that the current Job belongs to this contract.
            }
            if (paid > 0)
            { status = NeedCycleStatus.Satisfied; reason = "WageSettlementRecorded"; return T("Model.Execution.Paid", Money(paid)); }
            return T("Model.Execution.Unconfirmed");
        }

        private static void SettlementTotals(WorkContract contract, IReadOnlyList<WageObligation> payables, int now,
            out long due, out long paid, out bool shortfall)
        {
            due = 0; paid = 0; shortfall = false;
            if (payables == null) return;
            foreach (var row in payables)
            {
                if (row == null || row.ContractId != contract.ContractId || row.PayeeKey != contract.PawnKey
                    || row.DueTick < contract.SignedTick || row.DueTick > now) continue;
                if (row.State == ObligationState.Paid && row.PaidMinor == row.AmountMinor
                    && !string.IsNullOrEmpty(row.TxId) && !string.IsNullOrEmpty(row.EvidenceId)) paid += row.PaidMinor;
                else if (row.PaidMinor < row.AmountMinor)
                { due += row.AmountMinor - row.PaidMinor; shortfall |= row.State == ObligationState.Shortfall; }
            }
        }
    }
}

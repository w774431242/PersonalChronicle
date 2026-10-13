using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;

namespace PersonalChronicle.Domain.PawnEconomy.Budget
{
    internal enum BudgetState { Unknown, Ready, WaitingQuote, WaitingAccount, Inconsistent }
    internal enum CoverageKind { OwnedOrPaid, Public, HardReserved }
    internal sealed class CostCoverage
    {
        public string ResourceKey { get; }
        public CoverageKind Kind { get; }
        public long AmountMinor { get; }
        public CostCoverage(string key, CoverageKind kind, long amount)
        { ResourceKey = MinorCurrency.Required(key); Kind = kind; MinorCurrency.Validate(amount); AmountMinor = amount; }
    }
    internal sealed class RequirementCostSlice
    {
        public string RequirementKey { get; }
        public int StartTick { get; }
        public int EndTick { get; }
        public bool Necessary { get; }
        public bool Immediate { get; }
        public int RequiredByTick { get; }
        public Fact<long> TotalCostMinor { get; }
        public IReadOnlyList<CostCoverage> Coverage { get; }
        public RequirementCostSlice(string requirement, int start, int end, bool necessary, bool immediate,
            Fact<long> total, IEnumerable<CostCoverage> coverage = null, int? requiredByTick = null)
        {
            RequirementKey = MinorCurrency.Required(requirement);
            if (start < 0 || end <= start) throw new ArgumentException("InvalidCostWindow");
            StartTick = start; EndTick = end; Necessary = necessary; Immediate = immediate; TotalCostMinor = total;
            RequiredByTick = requiredByTick ?? (immediate ? start : end);
            if (RequiredByTick < start || RequiredByTick > end) throw new ArgumentException("InvalidCostDeadline");
            var rows = new List<CostCoverage>();
            foreach (var row in coverage ?? Array.Empty<CostCoverage>())
            { if (row == null || rows.Count == PlanningPolicy.MaximumSlices) throw new ArgumentException("InvalidCoverage"); rows.Add(row); }
            Coverage = rows.AsReadOnly();
        }
    }
    internal sealed class LifeBudget
    {
        public BudgetState State { get; }
        public string Reason { get; }
        public Fact<long> AvailableMinor { get; }
        public Fact<long> ReserveTargetMinor { get; }
        public Fact<long> LowWaterMinor { get; }
        public Fact<long> IncomeGapMinor { get; }
        public Fact<bool> RefillActive { get; }
        /// <summary>
        /// v0.5 §40 应急储备额（储备目标之上的缓冲）。补储的**实际触发线**是
        /// <c>下水位 + 应急额</c>（以储备目标为上界），使"提前寻找岗位"不必等到跌破下水位。
        /// </summary>
        public Fact<long> EmergencyReserveMinor { get; }
        /// <summary>
        /// CB-ECON-002 **补储启动线 H** = <c>min(储备目标, 下水位 + 应急额)</c>（v0.5 §40 的同一算式）。
        /// <para>此前它是 <see cref="LifeBudgetPlanner.Build"/> 里的**局部变量**，只以 <see cref="RefillActive"/>
        /// 的结果露出 —— 界面若要解释"低于目标但为什么暂不补储"，只能自己重算这套公式，两处口径必然缓慢漂移。
        /// 现随预算**只在一处计算**并出口：消费方只读，不重算、不改判定。</para>
        /// 仅在预算 <see cref="BudgetState.Ready"/> 时已知；其它状态为 <c>Unknown</c>（不得当作 0）。
        /// </summary>
        public Fact<long> RefillTriggerMinor { get; }
        // Last known latch survives unknown estimates within the shadow session; never a payment authority.
        public bool LastKnownRefill { get; }
        public long ImmediateAllocationMinor { get; }
        public long SavingAllocationMinor { get; }
        public long OptionalAllocationMinor { get; }
        public LifeBudget(BudgetState state, string reason, Fact<long> available = default, Fact<long> target = default,
            Fact<long> low = default, Fact<long> gap = default, Fact<bool> refill = default, bool latch = false,
            long immediate = 0, long saving = 0, long optional = 0, Fact<long> emergency = default,
            Fact<long> trigger = default)
        {
            State = state; Reason = reason; AvailableMinor = available; ReserveTargetMinor = target;
            LowWaterMinor = low; IncomeGapMinor = gap; RefillActive = refill; LastKnownRefill = latch;
            EmergencyReserveMinor = emergency; RefillTriggerMinor = trigger;
            ImmediateAllocationMinor = immediate; SavingAllocationMinor = saving; OptionalAllocationMinor = optional;
        }
    }
    internal static class LifeBudgetPlanner
    {
        private static long Add(long a, long b) { long value = checked(a + b); MinorCurrency.Validate(value); return value; }
        /// <param name="costsIncompleteReason">LR-04：成本源给出的**具体拒绝原因码**（缺失时保持旧兜底码）。
        /// 本参数只改"等待报价"的**原因文本**，不改任何判定、数值与状态 —— `BudgetState.WaitingQuote`
        /// 语义不变（把它改成"有价/目标 0"会让玩家绕过真实缺口，属红线禁止项）。</param>
        public static LifeBudget Build(Fact<FinancialAccountSnapshot> account, IEnumerable<RequirementCostSlice> slices,
            int start, int end, bool costsComplete, bool previousRefill, int savingBasisPoints = 2000,
            string costsIncompleteReason = "CostEstimateUnavailable")
        {
            if (start < 0 || end <= start || end - (long)start > 3L * PlanningPolicy.DayTicks
                || savingBasisPoints < 0 || savingBasisPoints > 4000) return new LifeBudget(BudgetState.Inconsistent, "InvalidWindow", latch: previousRefill);
            Fact<long> available = account.TryGetValue(out var money) ? Fact<long>.Known(money.AvailableMinor) : Fact<long>.Unknown("AccountUnknown");
            try
            {
                long target = 0, immediate = 0, optional = 0, held = 0; bool missing = !costsComplete;
                var keys = new HashSet<string>(StringComparer.Ordinal); var resources = new HashSet<string>(StringComparer.Ordinal); int count = 0;
                foreach (var slice in slices ?? Array.Empty<RequirementCostSlice>())
                {
                    if (++count > PlanningPolicy.MaximumSlices || slice == null || slice.StartTick != start || slice.EndTick != end
                        || !keys.Add(slice.RequirementKey)) return new LifeBudget(BudgetState.Inconsistent, "DuplicateOrMismatchedSlice", available, latch: previousRefill);
                    if (!slice.TotalCostMinor.TryGetValue(out long cost)) { missing = true; continue; }
                    MinorCurrency.Validate(cost); long covered = 0;
                    foreach (var cover in slice.Coverage)
                    {
                        if (!Enum.IsDefined(typeof(CoverageKind), cover.Kind) || !resources.Add(cover.ResourceKey))
                            return new LifeBudget(BudgetState.Inconsistent, "DuplicateCoverage", available, latch: previousRefill);
                        covered = Add(covered, cover.AmountMinor);
                        if (cover.Kind == CoverageKind.HardReserved) held = Add(held, cover.AmountMinor);
                    }
                    if (covered > cost) return new LifeBudget(BudgetState.Inconsistent, "CoverageExceedsCost", available, latch: previousRefill);
                    long net = cost - covered;
                    if (slice.Necessary) { target = Add(target, net); if (slice.Immediate) immediate = Add(immediate, net); }
                    else optional = Add(optional, net);
                }
                if (money != null && held > money.ReservedMinor)
                    return new LifeBudget(BudgetState.Inconsistent, "UnbackedReservation", available, latch: previousRefill);
                // LR-04：三处统一使用**成本源给出的具体拒绝原因**（缺失时构造期已回落兜底码）——
                // 状态与数值一字未改，只把"等待报价"这一个原因换成可排查的具体原因。
                if (missing) return new LifeBudget(BudgetState.WaitingQuote, costsIncompleteReason, available,
                    target: Fact<long>.Unknown(costsIncompleteReason), refill: Fact<bool>.Unknown(costsIncompleteReason), latch: previousRefill);
                long low = checked((target * PlanningPolicy.LowWaterBasisPoints + PlanningPolicy.BasisPoints - 1) / PlanningPolicy.BasisPoints);
                if (money == null) return new LifeBudget(BudgetState.WaitingAccount, "AccountUnknown", available,
                    Fact<long>.Known(target), Fact<long>.Known(low), latch: previousRefill);
                long cash = money.AvailableMinor, gap = Math.Max(0, target - cash);
                // v0.5 §40：触发线 = 下水位 + 应急额，但以储备目标为**上界**（不得因此无限补储）。
                // CB-ECON-002：同一算式的结果**随预算出口**（RefillTriggerMinor）—— 判定与展示共用这一份计算。
                long emergency = checked(target * PlanningPolicy.EmergencyReserveBasisPoints / PlanningPolicy.BasisPoints);
                long trigger = Math.Min(target, checked(low + emergency));
                bool refill = target > 0 && cash < target && (previousRefill || cash < trigger);
                long necessaryAllocation = Math.Min(cash, immediate), surplus = cash - necessaryAllocation;
                long remainingProtection = Math.Max(0, target - necessaryAllocation);
                long saving = Math.Min(remainingProtection, checked(surplus * savingBasisPoints / PlanningPolicy.BasisPoints));
                return new LifeBudget(BudgetState.Ready, "Ready", available, Fact<long>.Known(target), Fact<long>.Known(low), Fact<long>.Known(gap),
                    Fact<bool>.Known(refill), refill, necessaryAllocation, saving, Math.Min(optional, surplus - saving),
                    Fact<long>.Known(emergency), Fact<long>.Known(trigger));
            }
            catch (Exception error) when (error is OverflowException || error is ArgumentOutOfRangeException)
            { return new LifeBudget(BudgetState.Inconsistent, "InvalidAmount", available, latch: previousRefill); }
        }
    }
}

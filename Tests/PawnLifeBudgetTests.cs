using System;
using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class PawnLifeBudgetTests
    {
        private const int Start = 10, End = 120010;
        private static Fact<FinancialAccountSnapshot> Cash(long amount, long held = 0)
            => Fact<FinancialAccountSnapshot>.Known(new FinancialAccountSnapshot("p", amount, held));
        private static RequirementCostSlice Cost(string id, long amount, bool immediate = false, bool necessary = true, params CostCoverage[] coverage)
            => new RequirementCostSlice(id, Start, End, necessary, immediate, Fact<long>.Known(amount), coverage);
        private static long Value(Fact<long> fact) { Assert.That(fact.TryGetValue(out long amount), Is.True); return amount; }
        private static bool Refill(LifeBudget budget) { Assert.That(budget.RefillActive.TryGetValue(out bool active), Is.True); return active; }
        [Test] public void HardReservationIsRemovedFromBothCashAndTargetWithoutSubtractingSoftNeedsTwice()
        {
            var budget = LifeBudgetPlanner.Build(Cash(1000, 300), new[] {
                Cost("food", 500, false, true, new CostCoverage("order", CoverageKind.HardReserved, 300)),
                Cost("rest", 300, true) }, Start, End, true, false);
            Assert.That(budget.State, Is.EqualTo(BudgetState.Ready)); Assert.That(Value(budget.AvailableMinor), Is.EqualTo(700));
            Assert.That(Value(budget.ReserveTargetMinor), Is.EqualTo(500)); Assert.That(Value(budget.IncomeGapMinor), Is.Zero);
            Assert.That(budget.ImmediateAllocationMinor, Is.EqualTo(300)); Assert.That(Refill(budget), Is.False);
        }
        [Test] public void TwoDayReserveDoesNotAddTheCurrentDayAgain()
        {
            var budget = LifeBudgetPlanner.Build(Cash(500), new[] { Cost("food", 1200) }, Start, End, true, false);
            Assert.That(Value(budget.ReserveTargetMinor), Is.EqualTo(1200)); Assert.That(Value(budget.LowWaterMinor), Is.EqualTo(840));
            Assert.That(Value(budget.IncomeGapMinor), Is.EqualTo(700)); Assert.That(Refill(budget), Is.True);
        }
        // v0.5 §40 应急储备：补储触发线 = 下水位 + 应急额 = 1200×70% + 1200×8% = 936（以储备目标为**上界**）。
        // 因此 840~935 这一段现在会触发补储 —— 这是设计要的**更早**补储，不是回归。
        [TestCase(839, false, true)] [TestCase(900, false, true)] [TestCase(935, false, true)]
        [TestCase(936, false, false)] [TestCase(1199, false, false)]
        [TestCase(840, true, true)] [TestCase(1199, true, true)] [TestCase(1200, true, false)] [TestCase(1300, true, false)]
        public void LowWaterAndHighWaterHaveDistinctBoundaries(long cash, bool previous, bool expected)
            => Assert.That(Refill(LifeBudgetPlanner.Build(Cash(cash), new[] { Cost("food", 1200) }, Start, End, true, previous)), Is.EqualTo(expected));
        // CB-ECON-002 §4.2：补储**启动线 H** 必须由预算自己出口（此前是 Build 里的局部变量 ⇒ 界面只能重算公式）。
        // 量化口径（需求单 §4.2）：T=40.00 / L=28.00 / E=3.20 / H=31.20。
        [Test] public void RefillStartLineIsExportedByTheBudgetAndMatchesTheDocumentedThresholds()
        {
            var budget = LifeBudgetPlanner.Build(Cash(0), new[] { Cost("food", 4000) }, Start, End, true, false);
            Assert.That(Value(budget.ReserveTargetMinor), Is.EqualTo(4000));
            Assert.That(Value(budget.LowWaterMinor), Is.EqualTo(2800));
            Assert.That(Value(budget.EmergencyReserveMinor), Is.EqualTo(320));
            Assert.That(Value(budget.RefillTriggerMinor), Is.EqualTo(3120),
                "H = min(T, L + E) must be exported once by the budget, not recomputed by any consumer");
        }

        // §4.2 五个水位边界：未激活时 **严格小于 H** 才启动；已激活后继续到 T 才退出。
        [TestCase(3500, false, false)] [TestCase(3000, false, true)] [TestCase(3120, false, false)] [TestCase(3119, false, true)]
        [TestCase(3500, true, true)] [TestCase(4000, true, false)] [TestCase(3999, true, true)]
        public void RefillHysteresisFollowsTheExportedStartLine(long cash, bool previous, bool expected)
            => Assert.That(Refill(LifeBudgetPlanner.Build(Cash(cash), new[] { Cost("food", 4000) }, Start, End, true, previous)),
                Is.EqualTo(expected));

        [Test] public void StartLineIsUnknownWheneverTheBudgetCannotBeEstimated()
        {
            // 未知 ≠ 0：预算无法估计时启动线必须是 Unknown —— 消费方不得把它当 0，也不得自行重算。
            var unpriced = LifeBudgetPlanner.Build(Cash(0), new[] { Cost("food", 4000) }, Start, End, false, false);
            Assert.That(unpriced.RefillTriggerMinor.State, Is.EqualTo(FactState.Unknown));
            var noAccount = LifeBudgetPlanner.Build(default, new[] { Cost("food", 4000) }, Start, End, true, false);
            Assert.That(noAccount.RefillTriggerMinor.State, Is.EqualTo(FactState.Unknown));
        }

        [Test] public void CeilingLowWaterAndZeroCostEndRefill()
        {
            Assert.That(Value(LifeBudgetPlanner.Build(Cash(0), new[] { Cost("food", 1) }, Start, End, true, false).LowWaterMinor), Is.EqualTo(1));
            var zero = LifeBudgetPlanner.Build(Cash(0), new[] { Cost("food", 0) }, Start, End, true, true);
            Assert.That(Refill(zero), Is.False); Assert.That(Value(zero.IncomeGapMinor), Is.Zero);
        }
        [Test] public void UnknownCostsDoNotResetTheLastKnownLatchOrPretendTargetIsZero()
        {
            var budget = LifeBudgetPlanner.Build(Cash(500), new[] { Cost("food", 1200) }, Start, End, false, true);
            Assert.That(budget.State, Is.EqualTo(BudgetState.WaitingQuote)); Assert.That(budget.LastKnownRefill, Is.True);
            Assert.That(budget.ReserveTargetMinor.State, Is.EqualTo(FactState.Unknown)); Assert.That(budget.RefillActive.State, Is.EqualTo(FactState.Unknown));
            Assert.That(budget.IncomeGapMinor.State, Is.EqualTo(FactState.Unknown));
        }
        [Test] public void MissingPriceAndMissingPersonalAccountAreSeparateFromNoMoney()
        {
            var unpriced = new RequirementCostSlice("food", Start, End, true, true, Fact<long>.Unknown("QuoteMissing"));
            Assert.That(LifeBudgetPlanner.Build(Cash(10000), new[] { unpriced }, Start, End, true, false).State, Is.EqualTo(BudgetState.WaitingQuote));
            var missing = LifeBudgetPlanner.Build(default, new[] { Cost("food", 1200) }, Start, End, true, false);
            Assert.That(missing.State, Is.EqualTo(BudgetState.WaitingAccount)); Assert.That(missing.IncomeGapMinor.State, Is.EqualTo(FactState.Unknown));
            Assert.That(Value(missing.ReserveTargetMinor), Is.EqualTo(1200));
        }
        [Test] public void PaidAndPublicCoverageReduceTargetWithoutCreatingCash()
        {
            var budget = LifeBudgetPlanner.Build(Cash(0), new[] { Cost("food", 600, false, true,
                new CostCoverage("owned", CoverageKind.OwnedOrPaid, 200), new CostCoverage("public", CoverageKind.Public, 300)) }, Start, End, true, false);
            Assert.That(Value(budget.ReserveTargetMinor), Is.EqualTo(100)); Assert.That(Value(budget.AvailableMinor), Is.Zero);
            Assert.That(Value(budget.IncomeGapMinor), Is.EqualTo(100));
        }
        [Test] public void SameNeedAndSameCoverageCannotBeChargedOrAllocatedTwice()
        {
            var repeated = LifeBudgetPlanner.Build(Cash(1000), new[] { Cost("childFood", 100), Cost("childFood", 100) }, Start, End, true, false);
            Assert.That(repeated.Reason, Is.EqualTo("DuplicateOrMismatchedSlice"));
            var coverage = new CostCoverage("sameMeal", CoverageKind.OwnedOrPaid, 100);
            var duplicate = LifeBudgetPlanner.Build(Cash(1000), new[] { Cost("a", 100, false, true, coverage), Cost("b", 100, false, true, coverage) }, Start, End, true, false);
            Assert.That(duplicate.Reason, Is.EqualTo("DuplicateCoverage"));
        }
        [TestCase(101, 100, "CoverageExceedsCost")] [TestCase(50, 0, "UnbackedReservation")]
        public void InvalidCoverageDoesNotGrantBudgetAuthority(long cover, long held, string reason)
        {
            var budget = LifeBudgetPlanner.Build(Cash(1000, held), new[] { Cost("food", 100, false, true,
                new CostCoverage("claim", CoverageKind.HardReserved, cover)) }, Start, End, true, false);
            Assert.That(budget.State, Is.EqualTo(BudgetState.Inconsistent)); Assert.That(budget.Reason, Is.EqualTo(reason));
        }
        [Test] public void SavingUsesOnlySurplusAndAllSoftAllocationsShareTheSameCash()
        {
            var budget = LifeBudgetPlanner.Build(Cash(300), new[] { Cost("food", 200, true), Cost("future", 500), Cost("upgrade", 1000, false, false) }, Start, End, true, false);
            Assert.That(budget.ImmediateAllocationMinor, Is.EqualTo(200)); Assert.That(budget.SavingAllocationMinor, Is.EqualTo(20));
            Assert.That(budget.OptionalAllocationMinor, Is.EqualTo(80));
            Assert.That(budget.ImmediateAllocationMinor + budget.SavingAllocationMinor + budget.OptionalAllocationMinor, Is.EqualTo(300));
            Assert.That(Value(budget.AvailableMinor), Is.EqualTo(300));
        }
        [Test] public void OptionalWishesDoNotInflateReserveAndFullyNecessaryCashLeavesNoSaving()
        {
            var budget = LifeBudgetPlanner.Build(Cash(300), new[] { Cost("food", 300, true), Cost("weddingUpgrade", 100000, false, false) }, Start, End, true, false);
            Assert.That(Value(budget.ReserveTargetMinor), Is.EqualTo(300)); Assert.That(budget.SavingAllocationMinor, Is.Zero); Assert.That(budget.OptionalAllocationMinor, Is.Zero);
        }
        [TestCase(-1)] [TestCase(long.MaxValue)]
        public void InvalidAmountsAreExplicitErrors(long amount)
            => Assert.That(LifeBudgetPlanner.Build(Cash(0), new[] { Cost("food", amount) }, Start, End, true, false).Reason, Is.EqualTo("InvalidAmount"));
        [Test] public void SummedAmountLimitAndWindowMismatchAreRejected()
        {
            Assert.That(LifeBudgetPlanner.Build(Cash(0), new[] { Cost("a", MinorCurrency.Maximum), Cost("b", 1) }, Start, End, true, false).Reason, Is.EqualTo("InvalidAmount"));
            var wrong = new RequirementCostSlice("a", Start + 1, End, true, false, Fact<long>.Known(100));
            Assert.That(LifeBudgetPlanner.Build(Cash(0), new[] { wrong }, Start, End, true, false).State, Is.EqualTo(BudgetState.Inconsistent));
        }
    }
}

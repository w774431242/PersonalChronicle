using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Production;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Learning;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Production;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class EconomyProductionTests
    {
        [Test] public void StockAlreadyCoveringTheNeedProducesNoNewProduction()
        {
            Assert.That(RestockPlanner.Shortfall(new RestockInput(0, 100, 0, 100, 0, 0)), Is.EqualTo(0));
            Assert.That(RestockPlanner.Shortfall(new RestockInput(0, 100, 50, 200, 0, 0)), Is.EqualTo(0),
                "free stock above the target must never authorise more production");
        }
        [Test] public void AuthorisedNotYetStartedProductionIsNeverAddedTwice()
        {
            var first = RestockPlanner.Shortfall(new RestockInput(200, 0, 0, 0, 0, 0));
            Assert.That(first, Is.EqualTo(200));
            var afterAuthorisation = RestockPlanner.Shortfall(new RestockInput(200, 0, 0, 0, 0, 200));
            Assert.That(afterAuthorisation, Is.EqualTo(0), "a second evaluation must not authorise the same quantity again");
            var withWip = RestockPlanner.Shortfall(new RestockInput(200, 0, 0, 50, 100, 0));
            Assert.That(withWip, Is.EqualTo(50), "stock and completable work in progress both cover demand");
            var mixed = RestockPlanner.Shortfall(new RestockInput(100, 40, 30, 20, 10, 10));
            Assert.That(mixed, Is.EqualTo(130));
        }
        [Test] public void NegativeInputsAndOverflowAreRefusedRatherThanClamped()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RestockInput(-1, 0, 0, 0, 0, 0));
            var huge = new RestockInput(long.MaxValue, long.MaxValue, 0, 0, 0, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => RestockPlanner.Shortfall(huge), "overflow is reported, never clamped");
            Assert.Throws<ArgumentNullException>(() => RestockPlanner.Shortfall(null));
        }
        [Test] public void EveryInstitutionKindHasAtLeastOneReusableToolTemplate()
        {
            Assert.That(InstitutionToolCatalogue.Count, Is.EqualTo(InstitutionToolCatalogue.TemplateCount));
            foreach (InstitutionKind kind in new[] { InstitutionKind.LaborOffice, InstitutionKind.WorkshopOffice,
                InstitutionKind.SettlementOffice, InstitutionKind.Shop, InstitutionKind.ExamCenter })
                Assert.That(InstitutionToolCatalogue.ForKind(kind), Is.Not.Empty, kind.ToString());
            var bench = InstitutionToolCatalogue.Of(InstitutionToolTemplate.WorkshopBench);
            Assert.That(bench, Is.Not.Null);
            Assert.That(bench.ServesKind, Is.EqualTo(InstitutionKind.WorkshopOffice));
            Assert.That(bench.RequiresStock, Is.True, "a work bench consumes stock, a labour board does not");
            Assert.That(InstitutionToolCatalogue.Of(InstitutionToolTemplate.LabourBoard).RequiresStock, Is.False);
            Assert.That(InstitutionToolCatalogue.Of((InstitutionToolTemplate)99), Is.Null, "an unknown template is never invented");
        }        [Test] public void ExperienceStaysBoundedAndKeepsReliabilityInterpretable()
        {
            var summary = new ExperienceSummary("anchor:A|delivery");
            for (int i = 0; i < 200; i++) summary = summary.Record(i % 4 != 0, 100, i);
            Assert.That(summary.Samples, Is.EqualTo(ExperienceSummary.MaximumSamples), "history is bounded");
            Assert.That(summary.Successes, Is.LessThanOrEqualTo(summary.Samples));
            Assert.That(summary.ReliabilityBasisPoints, Is.InRange(0, 10000));
            Assert.That(summary.AverageDelayTicks, Is.GreaterThanOrEqualTo(0));
            Assert.That(summary.LastTick, Is.EqualTo(199));
            Assert.Throws<ArgumentOutOfRangeException>(() => summary.Record(true, -1, 1));
            var empty = new ExperienceSummary("anchor:B|delivery");
            // §3 显式先验 Beta(1,1)（CHG-20261009-ECON-M9-BETA）：无样本时读数落回**中性 0.5**，而不是 0 ——
            // 裸成功率会把"还没证据"显示成"极不可靠"，那正是设计明令禁止的
            // （§6 验收："供应方未知/截止未知 ⇒ 显示证据不足，不记失败或满信誉"）。
            // "不构成可信度主张"这一**意图**保留，但改由样本量门槛承担。
            Assert.That(empty.ReliabilityBasisPoints, Is.EqualTo(5000), "no samples means the neutral prior, not a verdict");
            Assert.That(WageCredibility.From(empty).HasClaim, Is.False, "no samples still means no reliability claim");
        }

        // ---- B6 closing: real readouts, executor contract, bounded learning (CHG-20261009-ECON-B6B7) ----

        [Test] public void MissingInputsProduceExplicitUnknownInsteadOfZero()
        {
            var unknown = new ProductionReadout("Steel",
                Fact<long>.Unknown("DemandDomainNotWired"), Fact<long>.Unknown("StockTargetNotWired"),
                Fact<long>.Unknown("TradeDomainNotWired"), Fact<long>.Known(10),
                Fact<long>.Known(0), Fact<long>.Unknown("ProductionAuthorisationNotWired"));
            var evaluation = RestockEvaluator.Evaluate(unknown);
            Assert.That(evaluation.Outcome, Is.EqualTo(RestockOutcome.Unknown),
                "an unwired demand domain must never read as 'no shortfall'");
            Assert.That(evaluation.ReasonCode, Is.EqualTo("DemandDomainNotWired"), "the first unknown input supplies the reason");
            Assert.That(evaluation.ShortfallMinor, Is.EqualTo(0));
        }

        [Test] public void FullyKnownReadoutProducesCoveredOrShortfall()
        {
            var covered = new ProductionReadout("Steel", Fact<long>.Known(0), Fact<long>.Known(100), Fact<long>.Known(0),
                Fact<long>.Known(100), Fact<long>.Known(0), Fact<long>.Known(0));
            Assert.That(RestockEvaluator.Evaluate(covered).Outcome, Is.EqualTo(RestockOutcome.Covered));
            var gap = new ProductionReadout("Steel", Fact<long>.Known(200), Fact<long>.Known(0), Fact<long>.Known(0),
                Fact<long>.Known(0), Fact<long>.Known(0), Fact<long>.Known(0));
            var evaluation = RestockEvaluator.Evaluate(gap);
            Assert.That(evaluation.Outcome, Is.EqualTo(RestockOutcome.Shortfall));
            Assert.That(evaluation.ShortfallMinor, Is.EqualTo(200));
        }

        [Test] public void OverflowIsReportedAsInconsistentNeverClamped()
        {
            var huge = new ProductionReadout("Steel", Fact<long>.Known(long.MaxValue), Fact<long>.Known(long.MaxValue),
                Fact<long>.Known(0), Fact<long>.Known(0), Fact<long>.Known(0), Fact<long>.Known(0));
            var evaluation = RestockEvaluator.Evaluate(huge);
            Assert.That(evaluation.Outcome, Is.EqualTo(RestockOutcome.Inconsistent));
            Assert.That(evaluation.ReasonCode, Is.EqualTo(RestockReasons.Overflow));
        }

        [Test] public void ReadoutGuardDegradesToNothingPublished()
        {
            ProductionReadoutGuard.Read(null, 1, new[] { "Steel" }, out var rows, out bool complete);
            Assert.That(complete, Is.False); Assert.That(rows, Is.Empty);
            ProductionReadoutGuard.Read(new ThrowingReadoutSource(), 1, new[] { "Steel" }, out rows, out complete);
            Assert.That(complete, Is.False); Assert.That(rows, Is.Empty, "a failing adapter publishes nothing, not a partial list");
            ProductionReadoutGuard.Read(new FixedReadoutSource(ProductionReadoutGuard.MaximumReadouts + 1), 1, new[] { "Steel" }, out rows, out complete);
            Assert.That(complete, Is.False, "an oversize publication is refused");
            ProductionReadoutGuard.Read(new FixedReadoutSource(1), 1, new[] { "Steel" }, out rows, out complete);
            Assert.That(complete, Is.True); Assert.That(rows.Count, Is.EqualTo(1));
        }

        [Test] public void UnwiredExecutorClaimsNothingAndAcceptsNothing()
        {
            var executor = new UnwiredWorkshopExecutor();
            var capacity = executor.ReadCapacity(1);
            Assert.That(capacity.ShiftCount.TryGetValue(out _), Is.False);
            Assert.That(capacity.OpenBills.TryGetValue(out _), Is.False);
            Assert.That(executor.TrySubmitRestockProposal(new ProductionProposal("1", "Steel", 5, null), out _, out _, out string unwiredReason), Is.False);
            Assert.That(unwiredReason, Is.EqualTo(ProductionProposalReasons.SinkNotWired));
            var reading = WorkshopExecutorGuard.ReadCapacity(new ThrowingExecutor(), 1);
            Assert.That(reading.StaffingGap.TryGetValue(out _), Is.False, "a failing contract degrades to unreadable");
        }

        [Test] public void ProposalLedgerIsBoundedDeduplicatingAndNeverExecutes()
        {
            var ledger = new RestockProposalLedger();
            var proposal = new ProductionProposal("1", "Steel", 5, null);
            Assert.That(ledger.TryAccept(proposal, 100, out string first, out bool replay, out _), Is.True);
            Assert.That(replay, Is.False);
            Assert.That(first, Is.EqualTo(RestockProposalLedger.ReceiptPrefix + proposal.ProposalId),
                "the receipt is derived from the proposal id: a reload can never rewrite it");
            Assert.That(ledger.TryAccept(proposal, 140, out string second, out replay, out _), Is.True);
            Assert.That(replay, Is.True, "the same proposal answers with the original receipt");
            Assert.That(second, Is.EqualTo(first));
            Assert.That(ledger.Count, Is.EqualTo(1));
            for (int i = 1; i < RestockProposalLedger.MaximumReceipts; i++)
                Assert.That(ledger.TryAccept(new ProductionProposal("1", "Steel" + i, i, null), 100 + i, out _, out _, out _), Is.True);
            Assert.That(ledger.Count, Is.EqualTo(RestockProposalLedger.MaximumReceipts));
            Assert.That(ledger.TryAccept(new ProductionProposal("1", "Overflow", 1, null), 200, out _, out _, out string fullReason), Is.False,
                "a full ledger refuses new proposals instead of dropping old receipts");
            Assert.That(fullReason, Is.EqualTo(ProductionProposalReasons.ReceiptCapacityReached),
                "a full ledger reports capacity, never 'not wired'");
        }

        [Test] public void ExperienceLedgerStaysBoundedAndRefusesNewKeysWhenFull()
        {
            var ledger = new ExperienceLedger();
            for (int i = 0; i < ExperienceLedger.MaximumKeys; i++)
                Assert.That(ledger.Record("anchor:" + i, true, 10, i), Is.True);
            Assert.That(ledger.Count, Is.EqualTo(ExperienceLedger.MaximumKeys));
            Assert.That(ledger.Record("anchor:late", true, 10, 1), Is.False,
                "a full ledger refuses a new key and never trims existing rows");
            Assert.That(ledger.Record("anchor:0", false, 20, 5), Is.True, "an existing key keeps recording");
            Assert.That(ledger.Read("anchor:0").Samples, Is.EqualTo(2));
            Assert.That(ledger.Read("anchor:missing"), Is.Null);
            Assert.That(ledger.Record(" ", true, 1, 1), Is.False);
            Assert.That(ledger.Record("anchor:2", true, -1, 1), Is.False);
        }

        [Test] public void CredibilityClaimsNothingBelowSampleFloor()
        {
            var none = WageCredibility.From(new ExperienceSummary("anchor:A"));
            Assert.That(none.Band, Is.EqualTo(CredibilityBand.Unproven));
            Assert.That(none.HasClaim, Is.False, "no samples means no claim");
            Assert.That(none.IsLowConfidence, Is.False);
            var thin = new ExperienceSummary("anchor:A");
            for (int i = 0; i < 3; i++) thin = thin.Record(false, 10, i);
            Assert.That(WageCredibility.From(thin).Band, Is.EqualTo(CredibilityBand.Unproven),
                "below the floor nothing is declared, even after failures");
            var weak = new ExperienceSummary("anchor:B");
            for (int i = 0; i < 10; i++) weak = weak.Record(i < 2, 10, i);
            var weakReading = WageCredibility.From(weak);
            Assert.That(weakReading.HasClaim, Is.True);
            Assert.That(weakReading.Band, Is.EqualTo(CredibilityBand.Weak));
            Assert.That(weakReading.IsLowConfidence, Is.True);
            var strong = new ExperienceSummary("anchor:C");
            for (int i = 0; i < 10; i++) strong = strong.Record(true, 5, i);
            Assert.That(WageCredibility.From(strong).Band, Is.EqualTo(CredibilityBand.Strong));
        }

        private sealed class ThrowingReadoutSource : IProductionReadoutSource
        {
            public IReadOnlyList<ProductionReadout> ReadReadouts(int mapId, IReadOnlyList<string> categoryKeys, out bool complete)
                => throw new InvalidOperationException("adapter failure");
        }

        private sealed class FixedReadoutSource : IProductionReadoutSource
        {
            private readonly int count;
            public FixedReadoutSource(int count) { this.count = count; }
            public IReadOnlyList<ProductionReadout> ReadReadouts(int mapId, IReadOnlyList<string> categoryKeys, out bool complete)
            {
                complete = true;
                var rows = new List<ProductionReadout>();
                for (int i = 0; i < count; i++)
                    rows.Add(new ProductionReadout("Steel" + i, Fact<long>.Known(0), Fact<long>.Known(0), Fact<long>.Known(0),
                        Fact<long>.Known(0), Fact<long>.Known(0), Fact<long>.Known(0)));
                return rows;
            }
        }

        private sealed class ThrowingExecutor : IWorkshopExecutorContract
        {
            public WorkshopCapacityReading ReadCapacity(int mapId) => throw new InvalidOperationException("contract failure");
            public bool TrySubmitRestockProposal(ProductionProposal proposal, out string receipt, out bool replay, out string reason)
                => throw new InvalidOperationException("contract failure");
        }
    }
}
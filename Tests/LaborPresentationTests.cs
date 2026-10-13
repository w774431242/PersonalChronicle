using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Economy.PawnEconomy;

namespace PersonalChronicle.Tests
{
    public sealed class LaborPresentationTests
    {
        private static WageObligation Wage(string contract, WageEligibility eligibility, ObligationState state,
            long paid = 0, string tx = null)
            => new WageObligation("w:" + contract, contract, "proof:" + contract, "pawn", 1000, 100,
                paid, state, tx, eligibility);

        [Test]
        public void PhysicalDeliveryAndPaymentAreIndependentAndExcludeOtherContracts()
        {
            var batches = new[]
            {
                new ProductBatch("batch", "c", "e", "pawn", "WoodLog", "employer", 17, 7, 2, 100, 1, "holder"),
                new ProductBatch("other", "x", "x", "otherPawn", "WoodLog", "employer", 900, 700, 0, 100, 2, "other")
            };
            var wages = new[] { Wage("c", WageEligibility.Submitted, ObligationState.Shortfall, 250),
                Wage("x", WageEligibility.Immediate, ObligationState.Paid, 1000, "otherTx") };
            var totals = new LaborUiTotals("c", batches, wages);
            Assert.That(totals.Delivered, Is.EqualTo(7));
            Assert.That(totals.Remaining, Is.EqualTo(8), "Lost products are not presented as waiting for delivery.");
            Assert.That(totals.PaidMinor, Is.EqualTo(250));
            Assert.That(totals.DueMinor, Is.EqualTo(750));
            Assert.That(batches[0].RemainingQuantity, Is.EqualTo(8), "Projection does not mutate authoritative batches.");
            Assert.That(wages[0].Eligibility, Is.EqualTo(WageEligibility.Submitted));
        }

        [Test]
        public void PendingSubmissionDoesNotBecomeEmployerArrearsBeforeEligibility()
        {
            Assert.That(LaborUiTotals.Group(Wage("pending", WageEligibility.PendingSubmission, ObligationState.Shortfall)),
                Is.EqualTo(LaborWageGroup.PendingSubmission));
            Assert.That(LaborUiTotals.Group(Wage("blocked", WageEligibility.Blocked, ObligationState.Shortfall)),
                Is.EqualTo(LaborWageGroup.PendingSubmission));
            Assert.That(LaborUiTotals.Group(Wage("unknown", WageEligibility.Unknown, ObligationState.Due)),
                Is.EqualTo(LaborWageGroup.PendingSubmission));
            Assert.That(LaborUiTotals.Group(Wage("eligible", WageEligibility.Submitted, ObligationState.Due)),
                Is.EqualTo(LaborWageGroup.PendingPayment));
            Assert.That(LaborUiTotals.Group(Wage("unfunded", WageEligibility.Submitted, ObligationState.Shortfall, 200)),
                Is.EqualTo(LaborWageGroup.Arrears));
        }

        [Test]
        public void PaidGroupRequiresConfirmedWholeAmountAndTransactionIdentity()
        {
            Assert.That(LaborUiTotals.Group(Wage("paid", WageEligibility.Submitted, ObligationState.Paid, 1000, "tx:finalized")),
                Is.EqualTo(LaborWageGroup.Paid));
            Assert.That(LaborUiTotals.Group(Wage("partial", WageEligibility.Submitted, ObligationState.Paid, 250, "tx:partial")),
                Is.Not.EqualTo(LaborWageGroup.Paid));
            Assert.That(LaborUiTotals.Group(Wage("noTx", WageEligibility.Submitted, ObligationState.Paid, 1000)),
                Is.Not.EqualTo(LaborWageGroup.Paid));
        }

        [Test]
        public void ExpandedContractKeepsActionIdentityAndNumericSortsInTheirDeclaredColumns()
        {
            var original = VanillaContractRow.Compose("contract", "pawn", "work", 2, 5, 100, 700, 400,
                "1.00", "7.00", "text", "detail", "search", "revoke");
            var expanded = LaborContractProjection.Expand(original, "verifying", 9, 17, 23, "station", "facts", "history");
            Assert.That(expanded.Cells.Length, Is.EqualTo(LaborContractProjection.ColumnCount));
            Assert.That(expanded.CellSorts.Length, Is.EqualTo(expanded.Cells.Length));
            Assert.That(expanded.Cells[7], Is.EqualTo("verifying"));
            Assert.That(expanded.Cells[8], Is.EqualTo("17"));
            Assert.That(expanded.CellSorts[8], Is.EqualTo(17));
            Assert.That(expanded.Cells[9], Is.EqualTo("23"));
            Assert.That(expanded.CellSorts[9], Is.EqualTo(23));
            Assert.That(expanded.Cells[10], Is.EqualTo("station"));
            Assert.That(expanded.Cells[LaborContractProjection.ActionColumn], Is.Empty);
            Assert.That(expanded.Revoke, Is.EqualTo(original.Revoke));
            Assert.That(expanded.Verb, Is.EqualTo(original.Verb));
            Assert.That(expanded.Key, Is.EqualTo(original.Key));
            Assert.That(expanded.Detail, Does.Contain("history"));
            Assert.That(original.Cells.Length, Is.EqualTo(8), "Expansion leaves the original contract payload intact.");
        }
    }
}

using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Employment;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Economy.PawnEconomy;

namespace PersonalChronicle.Tests
{
    public sealed class LaborPaymentFeedbackTests
    {
        [Test]
        public void FailedEligibleAttemptBecomesArrearsWithoutChangingRightsAndCanLaterPay()
        {
            var data = new EmploymentSaveData();
            var service = new EmploymentService(data);
            var row = new WageObligation("w", "c", "e", "pawn", 840, 10, eligibility: WageEligibility.Submitted,
                submissionId: "s", eligibleTick: 20);
            data.WriteObligation(row);
            Assert.That(service.NotePaymentUnavailable("w"), Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.NotePaymentUnavailable("w"), Is.EqualTo(EmploymentCode.Replay));
            var failed = service.ReadObligation("w");
            Assert.That(failed.AmountMinor, Is.EqualTo(row.AmountMinor));
            Assert.That(failed.PaidMinor, Is.Zero);
            Assert.That(failed.PayeeKey, Is.EqualTo(row.PayeeKey));
            Assert.That(failed.SubmissionId, Is.EqualTo("s"));
            Assert.That(failed.EligibleTick, Is.EqualTo(20));
            Assert.That(failed.Eligibility, Is.EqualTo(WageEligibility.Submitted));
            Assert.That(LaborUiTotals.Group(failed), Is.EqualTo(LaborWageGroup.Arrears));
            Assert.That(service.MarkPaid("w", 840, "wage:w"), Is.EqualTo(EmploymentCode.Applied));
            Assert.That(LaborUiTotals.Group(service.ReadObligation("w")), Is.EqualTo(LaborWageGroup.Paid));
            Assert.That(service.NotePaymentUnavailable("w"), Is.EqualTo(EmploymentCode.AlreadyPaid));
        }

        [TestCase((int)WageEligibility.PendingSubmission)]
        [TestCase((int)WageEligibility.Unknown)]
        [TestCase((int)WageEligibility.Blocked)]
        public void UnsubmittedOrUncertainRightsCannotBeMislabelledAsFailedPayment(int eligibilityValue)
        {
            var eligibility = (WageEligibility)eligibilityValue;
            var data = new EmploymentSaveData();
            var service = new EmploymentService(data);
            data.WriteObligation(new WageObligation("w", "c", "e", "pawn", 840, 10, eligibility: eligibility));
            Assert.That(service.NotePaymentUnavailable("w"), Is.EqualTo(EmploymentCode.SubmissionRequired));
            Assert.That(service.ReadObligation("w").State, Is.EqualTo(ObligationState.Due));
            Assert.That(service.ReadObligation("w").Eligibility, Is.EqualTo(eligibility));
        }
    }
}

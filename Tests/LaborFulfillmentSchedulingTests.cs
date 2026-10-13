using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Employment;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class LaborFulfillmentSchedulingTests
    {
        // Scheduling contract: fulfillment may finish pending delivery, but must yield to food/safety.
        private const float OrdinaryWorkPriority = 9f;
        private const float OrdinaryAnythingPriority = 5.5f;
        private const float FoodPriority = 9.5f;

        [TestCase(false, false, false)]
        [TestCase(false, false, true)]
        [TestCase(false, true, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, false)]
        [TestCase(true, false, true)]
        public void UnsafeOrNoPendingSubmissionNeverCompetesForAJob(bool safe, bool pending, bool workAssignment)
        {
            Assert.That(LaborFlowRules.FulfillmentPriority(safe, pending, workAssignment), Is.Zero);
        }

        [Test]
        public void PendingDeliveryDuringWorkWinsAgainstFurtherWorkAndStillYieldsToFood()
        {
            float priority = LaborFlowRules.FulfillmentPriority(true, true, true);
            Assert.That(priority, Is.GreaterThan(OrdinaryWorkPriority),
                "completed output must be collected before choosing another ordinary Work job");
            Assert.That(priority, Is.LessThan(FoodPriority),
                "fulfillment must not displace the higher priority food branch");
        }

        [Test]
        public void PendingDeliveryDuringAnythingWinsAgainstOrdinaryWorkAndStillYieldsToFood()
        {
            float priority = LaborFlowRules.FulfillmentPriority(true, true, false);
            Assert.That(priority, Is.GreaterThan(OrdinaryAnythingPriority));
            Assert.That(priority, Is.LessThan(FoodPriority));
            Assert.That(priority, Is.LessThan(LaborFlowRules.FulfillmentPriority(true, true, true)),
                "the explicit Work assignment must retain its stronger scheduling weight");
        }
    }
}

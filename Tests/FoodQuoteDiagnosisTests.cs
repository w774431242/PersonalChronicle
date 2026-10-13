using NUnit.Framework;
using PersonalChronicle.Economy.PawnEconomy;
using PersonalChronicle.Economy.PawnEconomy.Planning;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class FoodQuoteDiagnosisTests
    {
        [TestCase(0, "FoodQuoteNoPublicStorage")]
        [TestCase(1, "FoodQuoteZoneNotEnabled")]
        [TestCase(2, "FoodQuoteSourceUnreachable")]
        [TestCase(3, "FoodQuoteNoFood")]
        [TestCase(4, "FoodQuoteNotEdible")]
        [TestCase(5, "FoodQuoteOwnershipNotPublic")]
        [TestCase(6, "FoodQuoteFoodPendingTrade")]
        [TestCase(7, "FoodQuoteFoodUnreachable")]
        [TestCase(8, "FoodQuoteUnpriced")]
        public void ActualScanRejectionsKeepTheirConcreteCause(int scenario, string reason)
        {
            var scan = new FoodQuoteScan();
            switch (scenario)
            {
                case 1: scan.ZonesNotEnabled = 1; break;
                case 2: scan.StoresUnreachable = 1; break;
                case 3: scan.SourcesConsidered = 1; break;
                case 4: scan.ItemsSeen = scan.StacksNotEdible = 1; break;
                case 5: scan.ItemsSeen = scan.StacksOwnershipNotPublic = 1; break;
                case 6: scan.ItemsSeen = scan.StacksPending = 1; break;
                case 7: scan.ItemsSeen = scan.StacksUnreachable = 1; break;
                case 8: scan.ItemsSeen = scan.StacksUnpriced = 1; break;
            }
            scan.Refusal = GameComponent_EconomyStorage.DecideFoodQuoteRefusal(scan);
            Assert.That(EconomyFoodCostSource.RefusalReason(scan), Is.EqualTo(reason));
        }

        [Test]
        public void NearUsableFoodTakesPriorityOverUnrelatedDisabledZones()
        {
            var scan = new FoodQuoteScan { ZonesNotEnabled = 2, StoresUnreachable = 1,
                SourcesConsidered = 1, ItemsSeen = 1, StacksOwnershipNotPublic = 1 };
            Assert.That(GameComponent_EconomyStorage.DecideFoodQuoteRefusal(scan),
                Is.EqualTo(FoodQuoteRefusal.OwnershipNotPublic));
        }
    }
}

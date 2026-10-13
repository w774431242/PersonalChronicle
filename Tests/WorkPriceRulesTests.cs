using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Data.PawnEconomy;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public class WorkPriceRulesTests
    {
        [Test] public void OverrideDefaultAndRecommendationHaveDistinctPrecedence()
        {
            Assert.That(WorkPriceRules.Resolve(700, 900, 500), Is.EqualTo(900));
            Assert.That(WorkPriceRules.Resolve(700, 0, 500), Is.EqualTo(700));
            Assert.That(WorkPriceRules.Resolve(0, 0, 500), Is.EqualTo(500));
            Assert.That(WorkPriceRules.Resolve(0, 0, 0), Is.EqualTo(500));
            Assert.That(WorkPriceRules.Resolve(-1, long.MaxValue, 500), Is.EqualTo(500));
        }
        [Test] public void QuotesAreIsolatedByInstitutionAndClearRestoresInheritance()
        {
            var data = new WorkPriceSaveData();
            Assert.That(data.Set("A", "PlantCutting", null, 700), Is.True);
            Assert.That(data.Set("A", "PlantCutting", "CutPlants", 900), Is.True);
            Assert.That(data.Read("B", "PlantCutting"), Is.Zero);
            Assert.That(data.Read("A", "PlantCutting", "ChopWood"), Is.Zero);
            Assert.That(data.Set("A", "PlantCutting", "CutPlants", 0), Is.True);
            Assert.That(WorkPriceRules.Resolve(data.Read("A", "PlantCutting"), data.Read("A", "PlantCutting", "CutPlants"), 500), Is.EqualTo(700));
            Assert.That(data.Set("A", "PlantCutting", null, -1), Is.False);
            Assert.That(data.Read("A", "PlantCutting"), Is.EqualTo(700));
        }
        [Test] public void OldWildcardContractReservesCapacityForEveryOrderAndScopedContractsDoNotCross()
        {
            string legacy = WorkPriceRules.Position("Handling", "A");
            string tame = WorkPriceRules.Position("Handling", "A", "Tame");
            Assert.That(WorkPriceRules.Reserves(legacy, "Handling", "A", "Tame"), Is.True);
            Assert.That(WorkPriceRules.Reserves(legacy, "Handling", "A", "Slaughter"), Is.True);
            Assert.That(WorkPriceRules.Reserves(tame, "Handling", "A", "Slaughter"), Is.False);
            Assert.That(WorkPriceRules.Reserves(tame, "Handling", "A", null), Is.True);
            Assert.That(WorkPriceRules.Reserves(tame, "Handling", "B", "Tame"), Is.False);
            Assert.That(WorkPriceRules.Parse(tame, out string work, out string anchor, out string order), Is.True);
            Assert.That(work, Is.EqualTo("Handling")); Assert.That(anchor, Is.EqualTo("A")); Assert.That(order, Is.EqualTo("Tame"));
        }
        [TestCase("vanilla:Handling:A:")][TestCase("vanilla::A:Tame")][TestCase("vanilla:Handling:A:Tame:extra")]
        public void MalformedScopeCannotBecomeALegacyWildcard(string key)
            => Assert.That(WorkPriceRules.Parse(key, out _, out _, out _), Is.False);
        [Test] public void AStoredContractDoesNotChangeWhenQuotesChange()
        {
            var data = new WorkPriceSaveData(); data.Set("A", "Handling", "Tame", 900);
            var contract = new WorkContract("C", "P", "A", WorkPriceRules.Position("Handling", "A", "Tame"),
                new[] { "Handling" }, data.Read("A", "Handling", "Tame"), 2, 0, 100);
            data.Set("A", "Handling", "Tame", 1400);
            Assert.That(contract.WagePerJobMinor, Is.EqualTo(900));
            Assert.That(contract.With(ContractState.Completed, 2).PositionKey, Is.EqualTo(contract.PositionKey));
        }
    }
}

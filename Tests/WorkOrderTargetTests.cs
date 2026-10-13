using System.Linq;
using NUnit.Framework;
using PersonalChronicle.Capture;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse.AI;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class WorkOrderTargetTests
    {
        [TestCase("CutPlantDesignated", true, "CutPlants")]
        [TestCase("CutPlantDesignated", false, "CutPlants")]
        [TestCase("HarvestDesignated", true, "ChopWood")]
        [TestCase("HarvestDesignated", false, "Harvest")]
        [TestCase("Harvest", false, null)]
        [TestCase("Tame", false, "Tame")]
        [TestCase("Slaughter", false, "Slaughter")]
        [TestCase("Mine", false, "Mine")]
        [TestCase("HaulToCell", false, "Haul")]
        [TestCase("HaulToContainer", false, "Haul")]
        [TestCase("HaulToTransporter", false, null)]
        public void ActualJobDistinguishesTheContractOrder(string job, bool tree, string order)
        {
            Assert.That(WorkOrderTargetRules.OrderForJob(job, tree), Is.EqualTo(order));
        }

        [Test] public void AllEightExactTargetsExistAndAreConcreteVanillaDeclarations()
        {
            var jobs = Patch_WorkOrderTargets.VerifiedTargets().ToArray();
            var available = Patch_WorkOrderTargetAvailability.VerifiedTargets().ToArray();
            Assert.That(jobs, Has.Length.EqualTo(6));
            Assert.That(available, Has.Length.EqualTo(2));
            Assert.That(jobs.Concat(available).Distinct().Count(), Is.EqualTo(8));
            foreach (var target in jobs.Concat(available))
            {
                Assert.That(target.DeclaringType.Assembly, Is.EqualTo(typeof(Job).Assembly));
                Assert.That(target.GetParameters().Length, Is.EqualTo(3));
            }
        }

        [Test] public void SharedWorkTypeDoesNotAuthorizeUnrelatedUnpaidGivers()
        {
            Assert.That(WorkAuthorityService.GiverMatchesOrder("Slaughter", typeof(WorkGiver_Slaughter)), Is.True);
            Assert.That(WorkAuthorityService.GiverMatchesOrder("Slaughter", typeof(WorkGiver_Tame)), Is.False);
            Assert.That(WorkAuthorityService.GiverMatchesOrder("Tame", typeof(WorkGiver_Train)), Is.False);
            Assert.That(WorkAuthorityService.GiverMatchesOrder("Harvest", typeof(WorkGiver_PlantsCut)), Is.True);
            Assert.That(WorkAuthorityService.GiverMatchesOrder("Harvest", typeof(WorkGiver_GrowerHarvest)), Is.False,
                "显式收获合同不能授权种植区自动收获");
            Assert.That(WorkAuthorityService.GiverMatchesOrder("Haul", typeof(WorkGiver_HaulGeneral)), Is.True);
            Assert.That(WorkAuthorityService.GiverMatchesOrder("Haul", typeof(WorkGiver_Haul)), Is.False,
                "仅具体物资搬运 giver，不顺带授权建造/运输仓等其它工种内 giver");
        }
    }
}

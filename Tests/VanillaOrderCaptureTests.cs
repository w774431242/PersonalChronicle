using System;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Capture;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class VanillaOrderCaptureTests
    {
        [TestCase(PlantDestructionMode.Chop, true, "ChopWood")]
        [TestCase(PlantDestructionMode.Chop, false, "Harvest")]
        [TestCase(PlantDestructionMode.Cut, true, "CutPlants")]
        [TestCase(PlantDestructionMode.Cut, false, "CutPlants")]
        [TestCase(PlantDestructionMode.Smash, false, null)]
        [TestCase(PlantDestructionMode.Flame, true, null)]
        public void PlantModesMatchPlayerOrders(PlantDestructionMode mode, bool tree, string expected)
            => Assert.That(Patch_ItemFlowPlant.CompletionOrder(mode, tree), Is.EqualTo(expected));

        [TestCase("Hunt", true, true, true, "Hunt")]
        [TestCase("Slaughter", true, true, true, "Slaughter")]
        [TestCase("PredatorHunt", true, true, true, null)]
        [TestCase("Hunt", false, true, true, null)]
        [TestCase("Hunt", true, false, true, null)]
        [TestCase("Hunt", true, true, false, null)]
        [TestCase(null, true, true, true, null)]
        public void OnlyMatchingLiveAnimalJobsCaptureDeath(string job, bool animal, bool alive, bool target, string expected)
            => Assert.That(VanillaAnimalWork.DeathOrder(job, animal, alive, target), Is.EqualTo(expected));

        [Test]
        public void HaulRequiresFullActualDeliveryAndDoesNotCountDestroyedRemainder()
        {
            var progress = new HaulDeliveryProgress(25);
            Assert.That(progress.Complete(true), Is.False, "An empty carry tracker alone is not delivery evidence.");
            progress.Add(10);
            Assert.That(progress.Complete(true), Is.False, "Dropping ten then destroying fifteen must not pay a trip.");
            progress.Add(15);
            Assert.That(progress.Complete(false), Is.False, "Still carrying means the trip has not finished.");
            Assert.That(progress.Complete(true), Is.True);
            progress.Reported = true;
            Assert.That(progress.Complete(true), Is.False, "A retry of the same delivery must not pay twice.");
            var noPickup = new HaulDeliveryProgress(0);
            noPickup.Add(4);
            Assert.That(noPickup.Complete(true), Is.False);
        }

        [TestCase("HaulToCell", true)]
        [TestCase("HaulToContainer", true)]
        [TestCase("Hunt", false)]
        [TestCase("DoBill", false)]
        [TestCase("HaulToTransporter", false)]
        public void HaulEvidenceCannotPayHuntingOrProductionJobs(string job, bool expected)
            => Assert.That(VanillaHaulWork.IsOrdinaryHaulJob(job), Is.EqualTo(expected));

        [Test]
        public void ToilDecoratorRunsNativeActionExactlyOnceAndPreservesNativeFailure()
        {
            int calls = 0;
            var toil = new Toil { initAction = () => calls++ };
            VanillaHaulWork.WrapPickup(toil, TargetIndex.A);
            toil.initAction();
            Assert.That(calls, Is.EqualTo(1));
            toil = new Toil { initAction = () => { calls++; throw new InvalidOperationException("native"); } };
            VanillaHaulWork.WrapDelivery(toil, TargetIndex.B, true);
            Assert.Throws<InvalidOperationException>(() => toil.initAction());
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void EveryNewCaptureTargetResolvesWithTheVerifiedSignature()
        {
            Assert.That(AccessTools.Method(typeof(ExecutionUtility), nameof(ExecutionUtility.DoExecutionByCut),
                new[] { typeof(Pawn), typeof(Pawn), typeof(int), typeof(bool) }), Is.Not.Null);
            Assert.That(AccessTools.Method(typeof(InteractionWorker_RecruitAttempt), nameof(InteractionWorker_RecruitAttempt.DoRecruit),
                new[] { typeof(Pawn), typeof(Pawn), typeof(string).MakeByRefType(), typeof(string).MakeByRefType(), typeof(bool), typeof(bool) }), Is.Not.Null);
            Assert.That(AccessTools.Method(typeof(Toils_Haul), nameof(Toils_Haul.StartCarryThing),
                new[] { typeof(TargetIndex), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) }), Is.Not.Null);
            Assert.That(AccessTools.Method(typeof(Toils_Haul), nameof(Toils_Haul.PlaceHauledThingInCell),
                new[] { typeof(TargetIndex), typeof(Toil), typeof(bool), typeof(bool) }), Is.Not.Null);
            Assert.That(AccessTools.Method(typeof(Toils_Haul), nameof(Toils_Haul.DepositHauledThingInContainer),
                new[] { typeof(TargetIndex), typeof(TargetIndex), typeof(Action) }), Is.Not.Null);
            Assert.That(AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing),
                new[] { typeof(IntVec3), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(Action<Thing, int>) }), Is.Not.Null);
        }
    }
}

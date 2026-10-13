using System;
using System.IO;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Employment;
using PersonalChronicle.Capture;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class WorkOrderTripScribeTests
    {
        private Harmony host;
        private string path;
        [OneTimeSetUp] public void Start()
        {
            host = new Harmony("personalchronicle.tests.workordertrips");
            foreach (string name in new[] { "Error", "Warning", "Message" })
                host.Patch(AccessTools.Method(typeof(Log), name, new[] { typeof(string) }),
                    prefix: new HarmonyMethod(typeof(WorkOrderTripScribeTests), nameof(Skip)));
            host.Patch(AccessTools.Method(typeof(DeepProfiler), "Start", new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(WorkOrderTripScribeTests), nameof(Skip)));
            host.Patch(AccessTools.Method(typeof(DeepProfiler), "End"),
                prefix: new HarmonyMethod(typeof(WorkOrderTripScribeTests), nameof(Skip)));
        }
        private static bool Skip() => false;
        [OneTimeTearDown] public void Stop() => host.UnpatchAll(host.Id);
        [SetUp] public void Setup() => path = Path.Combine(Path.GetTempPath(), "pc_trip_" + Guid.NewGuid().ToString("N") + ".xml");
        [TearDown] public void Cleanup() { Scribe.ForceStop(); if (File.Exists(path)) File.Delete(path); }

        private static WorkOrderTripSaveData.Row Row(string worker = "Human_7", int job = 42)
            => new WorkOrderTripSaveData.Row { Worker = worker, Contract = "contract:haul", Order = "Haul",
                WorkType = "Hauling", Target = "Steel_9", Map = 3, Job = job, Expected = 25 };

        [Test]
        public void PartialNativeDeliveryScribeReloadCompletesOneActualWageObligationAndRejectsLaterTickReplay()
        {
            var trips = new WorkOrderTripSaveData();
            Assert.That(trips.Upsert(Row()), Is.True);
            Assert.That(trips.AddDelivered("Human_7", 42, 10), Is.True);
            var employment = new EmploymentSaveData();
            var service = new EmploymentService(employment);
            Assert.That(service.Sign(new SignRequest { ContractId = "contract:haul", PawnKey = "Human_7",
                InstitutionKey = "anchor:A", PositionKey = "anchor:A/Hauling/M/Haul", WorkTypeDefNames = new[] { "Hauling" },
                WagePerJobMinor = 500, MaxJobs = 3, PositionCapacity = 1, Now = 10, TermTicks = 60000 }).Code,
                Is.EqualTo(EmploymentCode.Applied));

            RoundTrip(ref trips, ref employment); // Actual ScribeSaver/ScribeLoader, including post-load init.
            Assert.That(trips.TryGet("Human_7", 42, out var restored), Is.True);
            var progress = new HaulDeliveryProgress(restored.Expected);
            progress.Add(restored.Delivered);
            Assert.That(progress.Complete(true), Is.False, "Partial delivery followed by a destroyed remainder is not a trip.");
            Assert.That(trips.AddDelivered("Human_7", 42, 15), Is.True);
            progress.Add(15);
            Assert.That(progress.Complete(true), Is.True);
            service = new EmploymentService(employment);
            string key = "order:Haul:3:Steel_9:haul:42:Steel_9";
            var first = service.AcceptEvidence(new WorkEvidence(restored.Contract, key, "VanillaWork", 0, 1, "job", 20));
            Assert.That(first.Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(first.AmountMinor, Is.EqualTo(500));
            Assert.That(trips.Remove("Human_7", 42), Is.True);

            RoundTrip(ref trips, ref employment); // Save the accepted evidence and empty trip state together.
            service = new EmploymentService(employment);
            Assert.That(service.AcceptEvidence(new WorkEvidence(restored.Contract, key, "VanillaWork", 0, 1, "job", 21)).Code,
                Is.EqualTo(EmploymentCode.Replay), "Different callback ticks must not create a second payment for one trip.");
            Assert.That(service.Obligations, Is.EqualTo(1));
            Assert.That(trips.TryGet("Human_7", 42, out _), Is.False);
        }

        [TestCase("<workOrderTrips schema=\"99\"><future>opaque</future></workOrderTrips>")]
        [TestCase("<workOrderTrips schema=\"1\"><trip broken=\"yes\" /></workOrderTrips>")]
        [TestCase("malformed <xml")]
        public void FutureOrMalformedPayloadSurvivesRealScribeUnchangedAndRefusesNewEvidence(string raw)
        {
            var trips = new WorkOrderTripSaveData(); trips.ImportPayload(raw);
            var employment = new EmploymentSaveData();
            RoundTrip(ref trips, ref employment);
            Assert.That(trips.Blocked, Is.True);
            Assert.That(trips.RetainedPayload, Is.EqualTo(raw));
            Assert.That(trips.Upsert(Row()), Is.False);
            Assert.That(trips.AddDelivered("Human_7", 42, 25), Is.False);
            Assert.That(trips.Remove("Human_7", 42), Is.False);
            RoundTrip(ref trips, ref employment);
            Assert.That(trips.RetainedPayload, Is.EqualTo(raw));
        }

        [Test]
        public void WorkerAndJobIdentityAreRequiredAndNewJobReplacesCancelledTripWithoutCompletingIt()
        {
            var trips = new WorkOrderTripSaveData(); trips.Upsert(Row());
            Assert.That(trips.TryGet("Human_8", 42, out _), Is.False);
            Assert.That(trips.TryGet("Human_7", 43, out _), Is.False);
            Assert.That(trips.AddDelivered("Human_7", 43, 25), Is.False);
            Assert.That(trips.Upsert(Row(job: 43)), Is.True);
            Assert.That(trips.Count, Is.EqualTo(1));
            Assert.That(trips.TryGet("Human_7", 42, out _), Is.False);
            Assert.That(trips.TryGet("Human_7", 43, out var next), Is.True);
            Assert.That(next.Delivered, Is.Zero);
            trips.RemoveInactive(row => false);
            Assert.That(trips.Count, Is.Zero);
        }

        [Test]
        public void BoundedCapacityAllowsExistingWorkerReplacementButRefusesAnotherWorker()
        {
            var trips = new WorkOrderTripSaveData();
            for (int i = 0; i < WorkOrderTripSaveData.MaximumRows; i++) Assert.That(trips.Upsert(Row("worker:" + i)), Is.True);
            Assert.That(trips.Upsert(Row("overflow")), Is.False);
            Assert.That(trips.Upsert(Row("worker:1", 43)), Is.True);
            Assert.That(trips.Count, Is.EqualTo(WorkOrderTripSaveData.MaximumRows));
        }

        [Test]
        public void MissingOldSaveSegmentCreatesNoPickupProof()
        {
            File.WriteAllText(path, "<savegame><other>x</other></savegame>");
            WorkOrderTripSaveData loaded = null;
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref loaded, "trips");
            Scribe.loader.FinalizeLoading();
            Assert.That(loaded, Is.Null);
            loaded = new WorkOrderTripSaveData();
            Assert.That(loaded.TryGet("Human_7", 42, out _), Is.False);
        }

        private void RoundTrip(ref WorkOrderTripSaveData trips, ref EmploymentSaveData employment)
        {
            Scribe.saver.InitSaving(path, "savegame");
            Scribe_Deep.Look(ref trips, "trips");
            Scribe_Deep.Look(ref employment, "employment");
            Scribe.saver.FinalizeSaving();
            trips = null; employment = null;
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref trips, "trips");
            Scribe_Deep.Look(ref employment, "employment");
            Scribe.loader.FinalizeLoading();
        }
    }
}

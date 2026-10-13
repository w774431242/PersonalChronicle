using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class ShadowPlanningLatchTests
    {
        private Harmony shim;
        private string path;

        [OneTimeSetUp] public void Start()
        {
            shim = new Harmony("personalchronicle.tests.planninglatch");
            foreach (string name in new[] { "Error", "Warning", "Message" }) Patch(typeof(Log), name, typeof(string));
            Patch(typeof(DeepProfiler), "Start", typeof(string)); Patch(typeof(DeepProfiler), "End");
        }
        private void Patch(Type type, string name, params Type[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, args, null);
            if (method != null) shim.Patch(method, prefix: new HarmonyMethod(typeof(ShadowPlanningLatchTests).GetMethod(nameof(Skip), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        private static bool Skip() => false;
        [OneTimeTearDown] public void Stop() => shim.UnpatchAll(shim.Id);
        [SetUp] public void Setup() { path = Path.Combine(Path.GetTempPath(), "pc_latch_" + Guid.NewGuid().ToString("N") + ".xml"); }
        [TearDown] public void Cleanup() { Scribe.ForceStop(); if (File.Exists(path)) File.Delete(path); }

        private static FactQueryResult Facts(int tick = 10, string id = "p", float food = .9f)
        {
            NumericNeedFact Need(string name, float value) => new NumericNeedFact(name, name, "test", Fact<float>.Known(value), Fact<float>.Known(1),
                true, "level", 1, tick, Fact<bool>.Known(false));
            var context = new PawnContextFact(SubjectLocation.SpawnedMap, 1, developmentalStage: Fact<string>.Known("Adult"),
                drafted: Fact<bool>.Known(false), downed: Fact<bool>.Known(false), mentalState: Fact<bool>.Known(false),
                playerForced: Fact<bool>.Known(false), timeAssignmentDefName: Fact<string>.Known("Anything"));
            var snapshot = new PawnFactSnapshot(id, 1, tick + 1, 1,
                new FactGroup<PawnContextFact>(CaptureState.Complete, context, tick, 1, "test"),
                new FactGroup<NeedFactSet>(CaptureState.Complete,
                    new NeedFactSet(new[] { Need("Food", food), Need("Rest", .95f), Need("Joy", .8f) }, new[] { "Food", "Rest", "Joy" }, null, null, Fact<float>.Known(.8f)),
                    tick, 1, "test"));
            return new FactQueryResult(PerceptionReadiness.Ready, "Ready", true, snapshot);
        }
        private static ShadowPlanningInput Input(int now = 10, long cash = 1000, float food = .9f, bool priced = true, string id = "p")
            => new ShadowPlanningInput(Facts(now, id, food), new LifePolicy(null),
                Fact<FinancialAccountSnapshot>.Known(new FinancialAccountSnapshot(id, cash)),
                priced ? new[] { new RequirementCostSlice("foodBudget", now, now + 120000, true, false, Fact<long>.Known(1200)) } : null,
                priced, null, true, Fact<bool>.Known(false), Fact<bool>.Known(true));

        [Test] public void SeedContinuesTheReserveLatchOnlyWhenThisSessionHasNoReport()
        {
            var plain = ShadowPlanBuilder.Build("p", 1, 1, 10, Input(), (ShadowPlan)null);
            Assert.That(plain.Decision, Is.EqualTo(ShadowDecision.NoIncomeNeeded));
            Assert.That(plain.Budget.LastKnownRefill, Is.False, "cash 1000 sits below target 1200 but above the 840 low water mark");
            var seeded = ShadowPlanBuilder.BuildSeeded("p", 1, 1, 10, Input(), new ShadowPlanLatch("p", 1, true, null));
            Assert.That(seeded.Budget.LastKnownRefill, Is.True, "the saved latch must carry the hysteresis across a reload");
            Assert.That(seeded.Decision, Is.EqualTo(ShadowDecision.WaitingOpportunity));
            var otherMap = ShadowPlanBuilder.BuildSeeded("p", 1, 2, 10, Input(), new ShadowPlanLatch("p", 1, true, null));
            Assert.That(otherMap.Budget.LastKnownRefill, Is.False, "a latch from another map must not leak into this one");
            var otherPawn = ShadowPlanBuilder.BuildSeeded("q", 1, 1, 10, Input(id: "q"), new ShadowPlanLatch("p", 1, true, null));
            Assert.That(otherPawn.Budget.LastKnownRefill, Is.False);
        }
        [Test] public void LiveReportWinsOverThePersistedSeed()
        {
            var live = ShadowPlanBuilder.Build("p", 1, 1, 10, Input(), (ShadowPlan)null);
            var rebuilt = ShadowPlanBuilder.BuildSeeded("p", 1, 1, 10, Input(), live, new ShadowPlanLatch("p", 1, true, null));
            Assert.That(rebuilt.Budget.LastKnownRefill, Is.False, "an in-session report is newer evidence than the save");
        }
        [Test] public void SeedKeepsANeedActiveBetweenTheEnterAndCompleteThresholds()
        {
            var key = RequirementIdentity.ForNeed("p", "Food", RequirementIdentity.LevelComponent);
            var plain = ShadowPlanBuilder.Build("p", 1, 1, 10, Input(food: .60f), (ShadowPlan)null);
            Assert.That(plain.Decision, Is.EqualTo(ShadowDecision.NoIncomeNeeded), "0.60 is above the 0.55 enter threshold");
            var seeded = ShadowPlanBuilder.BuildSeeded("p", 1, 1, 10, Input(food: .60f), new ShadowPlanLatch("p", 1, false, new[] { key }));
            Assert.That(seeded.Decision, Is.EqualTo(ShadowDecision.NativeNeed), "a need that was already active stays active until complete");
            Assert.That(seeded.LastKnownActiveRequirements, Contains.Item(key));
        }

        [Test] public void StoreKeepsOnlyRealEvidenceAndSurvivesWaitingPlans()
        {
            var store = new ShadowPlanningSaveData();
            var ready = ShadowPlanBuilder.BuildSeeded("p", 1, 1, 10, Input(), new ShadowPlanLatch("p", 1, true, null));
            store.Write(ready);
            Assert.That(store.Count, Is.EqualTo(1));
            Assert.That(store.Read("p", 1).RefillLatch, Is.True);
            var waiting = ShadowPlanBuilder.Build("p", 1, 1, 20, Input(now: 20, priced: false), (ShadowPlan)null);
            Assert.That(waiting.Budget.State, Is.EqualTo(BudgetState.WaitingQuote));
            store.Write(waiting);
            Assert.That(store.Read("p", 1).RefillLatch, Is.True, "a missing quote must not silently reset the latch");
            var exited = ShadowPlanBuilder.BuildSeeded("p", 1, 1, 30, Input(now: 30, cash: 5000), new ShadowPlanLatch("p", 1, true, null));
            Assert.That(exited.Decision, Is.EqualTo(ShadowDecision.NoIncomeNeeded));
            store.Write(exited);
            Assert.That(store.Read("p", 1).RefillLatch, Is.False, "reaching the reserve target is real evidence to leave the latch");
        }
        private ShadowPlanningSaveData RoundTrip(ShadowPlanningSaveData data)
        {
            Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref data, "pawnEconomicPlanningLatches"); Scribe.saver.FinalizeSaving();
            ShadowPlanningSaveData loaded = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "pawnEconomicPlanningLatches"); Scribe.loader.FinalizeLoading();
            return loaded;
        }
        [Test] public void RoundTripPreservesTheLatchAndAMissingNodeReadsAsNoMemory()
        {
            var store = new ShadowPlanningSaveData();
            var key = RequirementIdentity.ForNeed("A", "Food", RequirementIdentity.LevelComponent);
            store.Write(ShadowPlanBuilder.BuildSeeded("A", 1, 1, 10, Input(id: "A", food: .40f), new ShadowPlanLatch("A", 1, true, new[] { key })));
            Assert.That(store.Read("A", 1).ActiveRequirements, Contains.Item(key), "the write path must store the active requirement");
            var loaded = RoundTrip(store);
            var latch = loaded.Read("A", 1);
            Assert.That(latch, Is.Not.Null); Assert.That(latch.RefillLatch, Is.True);
            Assert.That(latch.ActiveRequirements, Contains.Item(key));
            Assert.That(loaded.Read("A", 4), Is.Null, "a map change must not be answered with another map's memory");
            File.WriteAllText(path, "<?xml version=\"1.0\"?><savegame />");
            ShadowPlanningSaveData missing = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref missing, "pawnEconomicPlanningLatches"); Scribe.loader.FinalizeLoading();
            missing = missing ?? new ShadowPlanningSaveData();
            Assert.That(missing.Count, Is.EqualTo(0));
            Assert.That(missing.Read("A", 3), Is.Null);
        }
        [Test] public void AStoredLatchKeepsAtMostOneEntryPerRequirementListBound()
        {
            var store = new ShadowPlanningSaveData();
            var key = RequirementIdentity.ForNeed("A", "Food", RequirementIdentity.LevelComponent);
            store.Write(ShadowPlanBuilder.BuildSeeded("A", 1, 1, 10, Input(id: "A", food: .40f), new ShadowPlanLatch("A", 1, true, new[] { key })));
            var row = store.Read("A", 1);
            Assert.That(row.ActiveRequirements, Has.Count.EqualTo(1));
            Assert.That(row.RefillLatch, Is.True);
        }        [Test] public void EntriesStayBoundedAndTheNewestMapWinsForOnePawn()
        {
            var store = new ShadowPlanningSaveData();
            var key = RequirementIdentity.ForNeed("A", "Food", RequirementIdentity.LevelComponent);
            for (int i = 0; i < ShadowPlanningSaveData.MaximumEntries + 10; i++)
                store.Write(ShadowPlanBuilder.BuildSeeded("p" + i.ToString(), 1, 1, 100 + i, Input(now: 100 + i, id: "p" + i.ToString()), new ShadowPlanLatch("p" + i.ToString(), 1, true, new[] { key })));
            Assert.That(store.Count, Is.EqualTo(ShadowPlanningSaveData.MaximumEntries), "the latch node must never grow without bound");
            var moved = new ShadowPlanningSaveData();
            moved.Write(ShadowPlanBuilder.BuildSeeded("p", 1, 1, 10, Input(), new ShadowPlanLatch("p", 1, true, null)));
            moved.Write(ShadowPlanBuilder.BuildSeeded("p", 1, 9, 20, Input(now: 20), new ShadowPlanLatch("p", 1, true, null)));
            Assert.That(moved.Count, Is.EqualTo(1));
            Assert.That(moved.Read("p", 9), Is.Not.Null);
            Assert.That(moved.Read("p", 1), Is.Null);
        }
    }
}

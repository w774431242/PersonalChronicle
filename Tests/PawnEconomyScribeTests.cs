using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Data;
using PersonalChronicle.Domain.PawnEconomy;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class PawnEconomyScribeTests
    {
        private Harmony shim;
        private string path;
        [OneTimeSetUp] public void Start()
        {
            shim = new Harmony("personalchronicle.tests.economyscribe");
            foreach (string name in new[] { "Error", "Warning", "Message" }) Patch(typeof(Log), name, typeof(string));
            Patch(typeof(DeepProfiler), "Start", typeof(string)); Patch(typeof(DeepProfiler), "End");
        }
        private void Patch(Type type, string name, params Type[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, args, null);
            if (method != null) shim.Patch(method, prefix: new HarmonyMethod(typeof(PawnEconomyScribeTests).GetMethod(nameof(Skip), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        private static bool Skip() => false;
        [OneTimeTearDown] public void Stop() => shim.UnpatchAll(shim.Id);
        [SetUp] public void Setup() { path = Path.Combine(Path.GetTempPath(), "pc_economy_" + Guid.NewGuid().ToString("N") + ".xml"); }
        [TearDown] public void Cleanup() { Scribe.ForceStop(); if (File.Exists(path)) File.Delete(path); }
        private PawnEconomySaveData RoundTrip(PawnEconomySaveData data)
        {
            Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref data, "economicInstitutions"); Scribe.saver.FinalizeSaving();
            PawnEconomySaveData loaded = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "economicInstitutions"); Scribe.loader.FinalizeLoading();
            Assert.That(loaded, Is.Not.Null); return loaded;
        }
        [Test] public void RealScribeRoundTripPreservesBudgetAndPrimitiveReferences()
        {
            var data = new PawnEconomySaveData(); var service = new EconomyService(data.State); service.Enable(true);
            data.State.Add(new EconomyOwnership { AssetKey = "Apparel_345", OwnerKey = "Human_12" });
            var loaded = RoundTrip(data); new EconomyService(loaded.State).RecoverAfterLoad();
            Assert.That(loaded.State.Ready); Assert.That(loaded.State.Funded);
            Assert.That(loaded.State.All<EconomyAccount>().Single().Balance, Is.EqualTo(100000));
            Assert.That(loaded.State.All<EconomyOwnership>().Single().AssetKey, Is.EqualTo("Apparel_345"));
        }
        [Test] public void RealScribePreservesFuturePayloadVerbatimOnResave()
        {
            const string future = "<FutureEconomy><Schema>999</Schema><UnknownContent value=\"保留\" /></FutureEconomy>";
            var data = new PawnEconomySaveData { State = new EconomyState { Schema = 999, Fault = "UnsupportedSchema" } };
            typeof(PawnEconomySaveData).GetField("payload", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(data, future);
            var loaded = RoundTrip(data); Assert.That(loaded.State.Ready, Is.False);
            var again = RoundTrip(loaded);
            Assert.That(typeof(PawnEconomySaveData).GetField("payload", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(again), Is.EqualTo(future));
        }
        [Test] public void MissingSaveKeyDefaultsToDisabledWithoutFunding()
        {
            File.WriteAllText(path, "<?xml version=\"1.0\"?><savegame />"); PawnEconomySaveData data = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref data, "economicInstitutions"); Scribe.loader.FinalizeLoading();
            data = data ?? new PawnEconomySaveData(); Assert.That(data.State.Enabled, Is.False); Assert.That(data.State.Funded, Is.False);
        }
    }
}

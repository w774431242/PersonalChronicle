using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Economy.PawnEconomy;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture, NonParallelizable]
    public class WorkPriceScribeTests
    {
        private Harmony shim; private string path;
        [OneTimeSetUp] public void Start()
        {
            shim = new Harmony("pc.tests.workpricescribe");
            foreach (string name in new[] { "Error", "Warning", "Message" }) Patch(typeof(Log), name, typeof(string));
            Patch(typeof(DeepProfiler), "Start", typeof(string)); Patch(typeof(DeepProfiler), "End");
        }
        private void Patch(Type type, string name, params Type[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, args, null);
            if (method != null) shim.Patch(method, prefix: new HarmonyMethod(typeof(WorkPriceScribeTests), nameof(Skip)));
        }
        private static bool Skip() => false;
        [OneTimeTearDown] public void Stop() => shim.UnpatchAll(shim.Id);
        [SetUp] public void Setup() => path = Path.Combine(Path.GetTempPath(), "pc_prices_" + Guid.NewGuid().ToString("N") + ".xml");
        [TearDown] public void Cleanup() { Scribe.ForceStop(); if (File.Exists(path)) File.Delete(path); }
        private void Save(WorkPriceSaveData data)
        { Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref data, "pawnEconomicWorkPrices"); Scribe.saver.FinalizeSaving(); }
        private WorkPriceSaveData Load()
        {
            WorkPriceSaveData data = null; Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref data, "pawnEconomicWorkPrices"); Scribe.loader.FinalizeLoading(); return data;
        }
        [Test] public void RealScribeRoundTripPreservesTwoLevelsAndAnchorIdentity()
        {
            var data = new WorkPriceSaveData(); data.Set("A", "Handling", null, 750); data.Set("A", "Handling", "Tame", 900);
            Save(data); var loaded = Load();
            Assert.That(loaded.Supported, Is.True); Assert.That(loaded.Read("A", "Handling"), Is.EqualTo(750));
            Assert.That(loaded.Read("A", "Handling", "Tame"), Is.EqualTo(900)); Assert.That(loaded.Read("B", "Handling"), Is.Zero);
        }
        [Test] public void MissingNodeHasNoPricesAndDoesNotInventAZeroWage()
        { File.WriteAllText(path, "<savegame/>"); Assert.That(Load(), Is.Null); }
        [TestCase("&lt;WorkPrices version=\"99\"&gt;&lt;future /&gt;&lt;/WorkPrices&gt;")]
        [TestCase("broken&lt;payload")]
        public void UnsupportedOrMalformedPayloadIsReadOnlyAndPreservedOnSave(string payload)
        {
            File.WriteAllText(path, "<savegame><pawnEconomicWorkPrices><payload>" + payload + "</payload></pawnEconomicWorkPrices></savegame>");
            var loaded = Load(); Assert.That(loaded.Supported, Is.False);
            Assert.That(loaded.Set("A", "Handling", "Tame", 900), Is.False);
            var runtime = new GameComponent_PawnEconomy(null);
            typeof(GameComponent_PawnEconomy).GetField("workPrices", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runtime, loaded);
            Assert.That(runtime.QuotedWorkPrice(null, "A", "Handling", "Tame", out bool fromMarket), Is.Zero);
            Assert.That(fromMarket, Is.False);
            Assert.That(runtime.TryDraftVanillaWorkContract("A", "Handling", 1, 900, 0, 0, out _, out string reason, "Tame"), Is.False);
            Assert.That(reason, Is.EqualTo("PriceUnavailable"), "The real draft entry must not interpret unknown prices as an unset quote");
            Save(loaded); Assert.That(File.ReadAllText(path), Does.Contain(payload));
        }
    }
}

using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class EconomyWorkOwnershipTests
    {
        private Harmony shim;
        private string path;

        [OneTimeSetUp] public void Start()
        {
            shim = new Harmony("personalchronicle.tests.workownership");
            foreach (string name in new[] { "Error", "Warning", "Message" }) Patch(typeof(Log), name, typeof(string));
            Patch(typeof(DeepProfiler), "Start", typeof(string)); Patch(typeof(DeepProfiler), "End");
        }
        private void Patch(Type type, string name, params Type[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, args, null);
            if (method != null) shim.Patch(method, prefix: new HarmonyMethod(typeof(EconomyWorkOwnershipTests).GetMethod(nameof(Skip), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        private static bool Skip() => false;
        [OneTimeTearDown] public void Stop() => shim.UnpatchAll(shim.Id);
        [SetUp] public void Setup() { path = Path.Combine(Path.GetTempPath(), "pc_ownership_" + Guid.NewGuid().ToString("N") + ".xml"); }
        [TearDown] public void Cleanup() { Scribe.ForceStop(); if (File.Exists(path)) File.Delete(path); }

        [Test] public void MedicalAndFirefightingStayUnmanagedWhileHaulingAndCleaningBecomeContractClaimable()
        {
            // 断言语义订正（CHG-20261012-ECON-LABOR2，2026-10-12 用户裁决「红线纳入合同解锁」）：
            // 原断言把**四项**都钉成不可写；现收窄为医疗 / 灭火 —— 搬运 / 清洁必须能被合同启用，
            // 否则搬运合同在 FA-11 复核（要求 priority > 0）处必然 `WorkTypeUnavailable`。
            foreach (string name in new[] { "Doctor", "Firefighter" })
            {
                Assert.That(WorkOwnershipRules.IsManaged(name), Is.False, name);
                Assert.That(WorkOwnershipRules.CanEnable(name, true, false, 0), Is.EqualTo(OwnershipCode.InvalidType), name);
            }
            foreach (string name in new[] { "Hauling", "Cleaning" })
            {
                Assert.That(WorkOwnershipRules.IsManaged(name), Is.True, name);
                Assert.That(WorkOwnershipRules.CanEnable(name, true, false, 0), Is.EqualTo(OwnershipCode.Applied), name);
                // 硬边界不放宽：玩家已打开的档位仍归玩家，本通道永不认领。
                Assert.That(WorkOwnershipRules.CanEnable(name, true, false, 2), Is.EqualTo(OwnershipCode.PlayerOwned), name);
            }
            Assert.That(WorkOwnershipRules.CanEnable("", true, false, 0), Is.EqualTo(OwnershipCode.InvalidType));
            Assert.That(WorkOwnershipRules.CanEnable(null, true, false, 0), Is.EqualTo(OwnershipCode.InvalidType));
        }
        [Test] public void EnableGateIsFailClosedAndNeverClaimsAPlayerChoice()
        {
            Assert.That(WorkOwnershipRules.CanEnable("Crafting", false, false, 0), Is.EqualTo(OwnershipCode.NotInitialized));
            Assert.That(WorkOwnershipRules.CanEnable("Crafting", true, true, 0), Is.EqualTo(OwnershipCode.AbilityDisabled));
            Assert.That(WorkOwnershipRules.CanEnable("Crafting", true, false, 4), Is.EqualTo(OwnershipCode.PlayerOwned));
            Assert.That(WorkOwnershipRules.CanEnable("Crafting", true, false, 0), Is.EqualTo(OwnershipCode.Applied));
        }
        [Test] public void RestoreGateKeepsPlayerEditsAndRestoresOurOwnValue()
        {
            Assert.That(WorkOwnershipRules.CanRestore(WorkOwnershipRules.EnabledPriority, WorkOwnershipRules.EnabledPriority),
                Is.EqualTo(OwnershipCode.Restored));
            Assert.That(WorkOwnershipRules.CanRestore(1, WorkOwnershipRules.EnabledPriority), Is.EqualTo(OwnershipCode.KeptPlayerValue));
            Assert.That(WorkOwnershipRules.CanRestore(0, WorkOwnershipRules.EnabledPriority), Is.EqualTo(OwnershipCode.KeptPlayerValue));
        }
        [Test] public void FirstRecordWinsSoTheOriginalValueCanAlwaysBeRestored()
        {
            var data = new WorkOwnershipSaveData();
            Assert.That(data.Record("p", "Crafting", 3, 0, 10), Is.True);
            Assert.That(data.Record("p", "Crafting", 3, 2, 20), Is.False, "a second record must never rewrite the original");
            var row = data.Read("p", "Crafting");
            Assert.That(row.OriginalPriority, Is.EqualTo(0));
            Assert.That(row.AppliedTick, Is.EqualTo(10));
            Assert.That(data.Count, Is.EqualTo(1));
            Assert.That(data.Read("p", "Hauling"), Is.Null);
        }
        [Test] public void DiscardReleasesOneTypeAndDiscardPawnReleasesTheWholePawn()
        {
            var data = new WorkOwnershipSaveData();
            data.Record("p", "Crafting", 3, 0, 10); data.Record("p", "Construction", 3, 0, 11); data.Record("q", "Crafting", 3, 0, 12);
            Assert.That(data.ReadFor("p"), Has.Count.EqualTo(2));
            data.Discard("p", "Crafting");
            Assert.That(data.Read("p", "Crafting"), Is.Null);
            Assert.That(data.DiscardPawn("p"), Is.EqualTo(1));
            Assert.That(data.Count, Is.EqualTo(1));
            Assert.That(data.Read("q", "Crafting"), Is.Not.Null, "releasing one pawn must not touch another");
        }
        [Test] public void LedgerStaysBoundedAndSurvivesASaveRoundTrip()
        {
            var data = new WorkOwnershipSaveData();
            for (int i = 0; i < WorkOwnershipSaveData.MaximumEntries + 5; i++)
                data.Record("p" + i.ToString(), "Crafting", 3, 0, i);
            Assert.That(data.Count, Is.EqualTo(WorkOwnershipSaveData.MaximumEntries));
            Assert.That(data.Read("p0", "Crafting"), Is.Null, "the oldest record is dropped first");
            Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref data, "pawnEconomicWorkOwnership"); Scribe.saver.FinalizeSaving();
            WorkOwnershipSaveData loaded = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "pawnEconomicWorkOwnership"); Scribe.loader.FinalizeLoading();
            Assert.That(loaded, Is.Not.Null);
            var survivor = loaded.Read("p" + (WorkOwnershipSaveData.MaximumEntries + 4).ToString(), "Crafting");
            Assert.That(survivor, Is.Not.Null); Assert.That(survivor.WrittenPriority, Is.EqualTo(3));
            File.WriteAllText(path, "<?xml version=\"1.0\"?><savegame />");
            WorkOwnershipSaveData missing = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref missing, "pawnEconomicWorkOwnership"); Scribe.loader.FinalizeLoading();
            missing = missing ?? new WorkOwnershipSaveData();
            Assert.That(missing.Count, Is.EqualTo(0), "a save without the node owns nothing");
        }
    }
}

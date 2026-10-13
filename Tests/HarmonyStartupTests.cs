using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Capture;
using RimWorld;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture, NonParallelizable]
    public sealed class HarmonyStartupTests
    {
        [TestCase(false), TestCase(true)]
        public void FullProductionPatchAllCompletesAndRegistersPublicStockpileAndFoodPayment(bool isolated)
        {
            var harmony = new Harmony("personalchronicle.tests.fullstartup");
            var host = new Harmony("personalchronicle.tests.fullstartup.host");
            try
            {
                foreach (string name in new[] { "Error", "Warning", "Message" })
                    host.Patch(AccessTools.Method(typeof(Log), name, new[] { typeof(string) }),
                        prefix: new HarmonyMethod(typeof(HarmonyStartupTests), nameof(Skip)));
                host.Patch(AccessTools.Method(typeof(DeepProfiler), "Start", new[] { typeof(string) }),
                    prefix: new HarmonyMethod(typeof(HarmonyStartupTests), nameof(Skip)));
                host.Patch(AccessTools.Method(typeof(DeepProfiler), "End"),
                    prefix: new HarmonyMethod(typeof(HarmonyStartupTests), nameof(Skip)));
                if (isolated)
                {
                    var errors = new List<string>();
                    Assert.That(HarmonyStartup.PatchAll(harmony, typeof(Patch_PublicStockpileGizmos).Assembly, errors.Add),
                        Is.Zero, string.Join("\n", errors));
                }
                else Assert.That(() => harmony.PatchAll(typeof(Patch_PublicStockpileGizmos).Assembly), Throws.Nothing);
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Zone_Stockpile), "GetGizmos"), typeof(Patch_PublicStockpileGizmos));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Zone_Stockpile), "ExposeData"), typeof(Patch_PublicStockpileSave));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Thing), "Ingested"), typeof(Patch_StoredFoodIngest));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Plant), "PlantCollected"), typeof(Patch_ItemFlowPlant));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Pawn_ApparelTracker), "Wear"), typeof(Patch_PublicApparelWear));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(JobGiver_OptimizeApparel), "TryGiveJob"), typeof(Patch_PublicApparelSelection));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Pawn_WorkSettings), "CacheWorkGiversInOrder", Type.EmptyTypes),
                    typeof(Patch_PawnWorkSettings_CacheGivers));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill), new[] { typeof(DamageInfo?), typeof(Hediff) }), typeof(Patch_VanillaAnimalKill));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(ExecutionUtility), nameof(ExecutionUtility.DoExecutionByCut),
                    new[] { typeof(Pawn), typeof(Pawn), typeof(int), typeof(bool) }), typeof(Patch_VanillaSlaughter));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(InteractionWorker_RecruitAttempt), nameof(InteractionWorker_RecruitAttempt.DoRecruit),
                    new[] { typeof(Pawn), typeof(Pawn), typeof(string).MakeByRefType(), typeof(string).MakeByRefType(), typeof(bool), typeof(bool) }), typeof(Patch_VanillaTame));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Verse.AI.Toils_Haul), nameof(Verse.AI.Toils_Haul.StartCarryThing),
                    new[] { typeof(Verse.AI.TargetIndex), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) }), typeof(Patch_VanillaHaulPickup));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Verse.AI.Toils_Haul), nameof(Verse.AI.Toils_Haul.PlaceHauledThingInCell),
                    new[] { typeof(Verse.AI.TargetIndex), typeof(Verse.AI.Toil), typeof(bool), typeof(bool) }), typeof(Patch_VanillaHaulCell));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Verse.AI.Toils_Haul), nameof(Verse.AI.Toils_Haul.DepositHauledThingInContainer),
                    new[] { typeof(Verse.AI.TargetIndex), typeof(Verse.AI.TargetIndex), typeof(Action) }), typeof(Patch_VanillaHaulContainer));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing),
                    new[] { typeof(IntVec3), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(Action<Thing, int>) }), typeof(Patch_VanillaHaulDrop));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Designator_Haul), nameof(Designator_Haul.CanDesignateThing),
                    new[] { typeof(Thing) }), typeof(Patch_HaulDesignatorThing));
                // R2 返修（CHG-20261012-ECON-HAULCMD）：格内路径改挂最外层虚方法 —— 原先挂的
                // GridsUtility.GetFirstHaulable 被 Mono JIT 内联绕过，实机未生效（Player.log 实证）。
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Designator_Haul), nameof(Designator_Haul.CanDesignateCell),
                    new[] { typeof(IntVec3) }), typeof(Patch_HaulDesignatorCell));
                AssertRegistered(harmony.Id, AccessTools.Method(typeof(Designator_Haul), nameof(Designator_Haul.DesignateSingleCell),
                    new[] { typeof(IntVec3) }), typeof(Patch_HaulDesignatorSingleCell));
                Assert.That(typeof(Patch_WorkTab_ContractLock_Draw).HasHarmonyAttribute(), Is.False,
                    "Unity ECall visual targets must stay deferred manual ApplyOnce patches.");
                Assert.That(typeof(Patch_WorkTab_ContractLock_Tip).HasHarmonyAttribute(), Is.False);
            }
            finally { harmony.UnpatchAll(harmony.Id); host.UnpatchAll(host.Id); }
        }

        [Test]
        public void ClassFailureContinuesAndSuccessfulClassIsInstalledExactlyOnce()
        {
            var harmony = new Harmony("personalchronicle.tests.startup.failure");
            var errors = new List<string>();
            try
            {
                UnrelatedInitialized = false;
                Assert.That(HarmonyStartup.PatchClasses(harmony, new[] {
                    typeof(BeforePatch), typeof(UnrelatedType), typeof(MissingTargetPatch),
                    typeof(AfterPatch), typeof(BeforePatch) }, errors.Add), Is.EqualTo(1));
                Assert.That(Before(), Is.EqualTo(11));
                Assert.That(After(), Is.EqualTo(12));
                AssertPatchCount(harmony.Id, nameof(Before), typeof(BeforePatch), 1);
                AssertPatchCount(harmony.Id, nameof(After), typeof(AfterPatch), 1);
                Assert.That(UnrelatedInitialized, Is.False);
                Assert.That(errors, Has.Count.EqualTo(1));
                StringAssert.Contains(typeof(MissingTargetPatch).FullName, errors[0]);
                StringAssert.Contains(harmony.Id, errors[0]);
                StringAssert.Contains("Undefined target method", errors[0]);
            }
            finally { harmony.UnpatchAll(harmony.Id); }
        }

        [Test]
        public void PartialClassFailureRetainsFirstTargetWithoutRetryAndContinuesLaterClasses()
        {
            var harmony = new Harmony("personalchronicle.tests.startup.partial");
            var errors = new List<string>();
            try
            {
                Assert.That(HarmonyStartup.PatchClasses(harmony, new[] {
                    typeof(BeforePatch), typeof(PartialPatch), typeof(AfterPatch), typeof(PartialPatch) },
                    errors.Add), Is.EqualTo(1));
                Assert.That(PartialFirst(), Is.EqualTo(13), "First target must actually have been patched before the failure.");
                Assert.That(PartialSecond(), Is.EqualTo(4));
                Assert.That(After(), Is.EqualTo(12), "Later class must actually execute its patch.");
                AssertPatchCount(harmony.Id, nameof(PartialFirst), typeof(PartialPatch), 1);
                AssertPatchCount(harmony.Id, nameof(PartialSecond), typeof(PartialPatch), 0);
                AssertPatchCount(harmony.Id, nameof(Before), typeof(BeforePatch), 1);
                Assert.That(errors, Has.Count.EqualTo(1));
                StringAssert.Contains(typeof(PartialPatch).FullName, errors[0]);
                StringAssert.Contains(nameof(PartialSecond), errors[0]);
                StringAssert.Contains("any installed targets retained", errors[0]);
            }
            finally { harmony.UnpatchAll(harmony.Id); }
        }

        [Test]
        public void WorkGateSignaturesResolveWithoutApplyingManualECallTargets()
        {
            var cache = AccessTools.Method(typeof(Pawn_WorkSettings), "CacheWorkGiversInOrder", Type.EmptyTypes);
            Assert.That(cache, Is.Not.Null);
            Assert.That(AccessTools.Method(typeof(WidgetsWork), nameof(WidgetsWork.DrawWorkBoxFor), new[] {
                typeof(float), typeof(float), typeof(Pawn), typeof(WorkTypeDef), typeof(bool) }), Is.Not.Null);
            Assert.That(AccessTools.Method(typeof(WidgetsWork), nameof(WidgetsWork.TipForPawnWorker), new[] {
                typeof(Pawn), typeof(WorkTypeDef), typeof(bool) }), Is.Not.Null);
        }

        private static void AssertPatchCount(string owner, string name, Type adapter, int expected)
        {
            var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(HarmonyStartupTests), name));
            Assert.That(info?.Postfixes.Count(p => p.owner == owner && p.PatchMethod.DeclaringType == adapter) ?? 0,
                Is.EqualTo(expected), adapter.Name + " -> " + name);
        }

        [MethodImpl(MethodImplOptions.NoInlining)] private static int Before() => 1;
        [MethodImpl(MethodImplOptions.NoInlining)] private static int After() => 2;
        [MethodImpl(MethodImplOptions.NoInlining)] private static int PartialFirst() => 3;
        [MethodImpl(MethodImplOptions.NoInlining)] private static int PartialSecond() => 4;
        private static bool UnrelatedInitialized;
        private static class UnrelatedType
        {
            static UnrelatedType() { UnrelatedInitialized = true; throw new InvalidOperationException("Must not initialize"); }
        }
        [HarmonyPatch(typeof(HarmonyStartupTests), nameof(Before))]
        private static class BeforePatch { private static void Postfix(ref int __result) { __result += 10; } }
        [HarmonyPatch(typeof(HarmonyStartupTests), nameof(After))]
        private static class AfterPatch { private static void Postfix(ref int __result) { __result += 10; } }
        [HarmonyPatch(typeof(HarmonyStartupTests), "MissingMethod")]
        private static class MissingTargetPatch { private static void Postfix() { } }
        [HarmonyPatch]
        private static class PartialPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(HarmonyStartupTests), nameof(PartialFirst));
                yield return AccessTools.Method(typeof(HarmonyStartupTests), nameof(PartialSecond));
            }
            private static void Postfix(ref int __result) { __result += 10; }
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
            {
                if (original.Name == nameof(PartialSecond)) throw new InvalidOperationException("injected second target failure");
                return instructions;
            }
        }
        private static void AssertRegistered(string owner, System.Reflection.MethodBase target, Type adapter)
        {
            var info = Harmony.GetPatchInfo(target);
            Assert.That(info, Is.Not.Null, target.Name);
            Assert.That(info.Prefixes.Concat(info.Postfixes).Concat(info.Finalizers)
                .Any(p => p.owner == owner && p.PatchMethod.DeclaringType == adapter), Is.True, adapter.Name);
        }
        private static bool Skip() => false;
    }
}

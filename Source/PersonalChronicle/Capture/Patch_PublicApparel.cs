using System;
using HarmonyLib;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Capture
{
    [HarmonyPatch(typeof(Pawn_ApparelTracker), nameof(Pawn_ApparelTracker.Wear))]
    public static class Patch_PublicApparelWear
    {
        private static bool Prefix(Pawn ___pawn, Apparel __0)
            => GameComponent_PawnEconomy.Current?.BeforeStoredApparelWear(___pawn, __0) ?? true;
        private static Exception Finalizer(Pawn ___pawn, Apparel __0, Exception __exception)
        {
            GameComponent_PawnEconomy.Current?.PreservePurchasedApparel(___pawn, __0);
            return __exception;
        }
    }
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "TryGiveJob")]
    public static class Patch_PublicApparelSelection
    {
        private static void Postfix(Pawn __0, ref Job __result)
        {
            if (__result?.def == JobDefOf.Wear && __result.targetA.Thing is Apparel apparel
                && GameComponent_PawnEconomy.Current?.CanSelectStoredApparel(__0, apparel) == false) __result = null;
        }
    }
}

using System;
using System.Reflection;
using HarmonyLib;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    internal static class VanillaAnimalWork
    {
        internal static string DeathOrder(string jobDefName, bool animal, bool alive, bool jobTargetMatches)
        {
            if (!animal || !alive || !jobTargetMatches) return null;
            return jobDefName == "Hunt" ? "Hunt" : jobDefName == "Slaughter" ? "Slaughter" : null;
        }

        internal static VanillaOrderIntent CaptureDeath(Pawn victim, Pawn worker)
        {
            string order = DeathOrder(worker?.CurJob?.def?.defName, victim?.RaceProps?.Animal == true,
                victim != null && !victim.Dead, worker?.CurJob?.targetA.Thing == victim);
            return order == null ? null : GameComponent_PawnEconomy.Current?.CaptureVanillaOrderIntent(order, victim, worker);
        }

        internal static void ReportDeath(VanillaOrderIntent intent, Pawn victim)
        {
            if (intent == null || victim?.Dead != true) return;
            // Both nested Kill and outer execution use the same stable target
            // evidence key. Employment accepts it once even if both hooks fire.
            GameComponent_PawnEconomy.Current?.ReportVanillaOrderCompletion(intent, Find.TickManager.TicksGame);
        }
    }

    // Ranged hunt deaths pass their actual damage instigator through Pawn.Kill;
    // merely collecting somebody else's corpse is deliberately not a completion.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill), new Type[] { typeof(DamageInfo?), typeof(Hediff) })]
    public static class Patch_VanillaAnimalKill
    {
        internal static void Prefix(Pawn __instance, DamageInfo? dinfo, out VanillaOrderIntent __state)
        {
            __state = null;
            try { __state = VanillaAnimalWork.CaptureDeath(__instance, dinfo?.Instigator as Pawn); }
            catch (Exception ex) { Log.ErrorOnce("[PC][Economy] animal death intent failed: " + ex, 0x5A1811); }
        }
        internal static void Postfix(Pawn __instance, VanillaOrderIntent __state, bool __runOriginal)
        {
            if (!__runOriginal) return;
            try { VanillaAnimalWork.ReportDeath(__state, __instance); }
            catch (Exception ex) { Log.ErrorOnce("[PC][Economy] animal death completion failed: " + ex, 0x5A1812); }
        }
    }

    // Slaughter's public execution entry provides an actor even if a mod strips
    // DamageInfo from the nested Kill call. It does not count non-job executions.
    [HarmonyPatch(typeof(ExecutionUtility), nameof(ExecutionUtility.DoExecutionByCut),
        new Type[] { typeof(Pawn), typeof(Pawn), typeof(int), typeof(bool) })]
    public static class Patch_VanillaSlaughter
    {
        internal static void Prefix(Pawn executioner, Pawn victim, out VanillaOrderIntent __state)
        {
            __state = null;
            try { __state = VanillaAnimalWork.CaptureDeath(victim, executioner); }
            catch (Exception ex) { Log.ErrorOnce("[PC][Economy] slaughter intent failed: " + ex, 0x5A1813); }
        }
        internal static void Postfix(Pawn victim, VanillaOrderIntent __state, bool __runOriginal)
        {
            if (!__runOriginal) return;
            try { VanillaAnimalWork.ReportDeath(__state, victim); }
            catch (Exception ex) { Log.ErrorOnce("[PC][Economy] slaughter completion failed: " + ex, 0x5A1814); }
        }
    }

    [HarmonyPatch]
    public static class Patch_VanillaTame
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(InteractionWorker_RecruitAttempt),
            nameof(InteractionWorker_RecruitAttempt.DoRecruit), new Type[] {
                typeof(Pawn), typeof(Pawn), typeof(string).MakeByRefType(), typeof(string).MakeByRefType(),
                typeof(bool), typeof(bool) });

        internal static void Prefix(Pawn recruiter, Pawn recruitee, out VanillaOrderIntent __state)
        {
            __state = null;
            try
            {
                if (recruitee?.RaceProps?.Animal != true || recruitee.Dead || recruitee.Faction == Faction.OfPlayer
                    || recruiter?.CurJob?.def != JobDefOf.Tame || recruiter.CurJob.targetA.Thing != recruitee) return;
                __state = GameComponent_PawnEconomy.Current?.CaptureVanillaOrderIntent("Tame", recruitee, recruiter);
            }
            catch (Exception ex) { Log.ErrorOnce("[PC][Economy] tame intent failed: " + ex, 0x5A1815); }
        }
        internal static void Postfix(Pawn recruitee, VanillaOrderIntent __state, bool __runOriginal)
        {
            if (__state == null || !__runOriginal || recruitee?.Dead != false || recruitee.Faction != Faction.OfPlayer) return;
            try
            {
                int now = Find.TickManager.TicksGame;
                GameComponent_PawnEconomy.Current?.ReportVanillaOrderCompletion(__state, now, "tame:" + now);
            }
            catch (Exception ex) { Log.ErrorOnce("[PC][Economy] tame completion failed: " + ex, 0x5A1816); }
        }
    }
}

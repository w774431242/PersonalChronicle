using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>EXC-2026-029: read-only Postfix on the vanilla tend callback. It reports one delivered
    /// care act (doctor, patient, tick) to the economy component; it never changes the treated pawn, the
    /// medicine or the outcome, and an unresolvable target leaves the hook dormant.</summary>
    [HarmonyPatch(typeof(TendUtility), "DoTend")]
    public static class Patch_TendUtility
    {
        internal static bool TargetMethodExists { get; private set; }
        static Patch_TendUtility() { TargetMethodExists = Probe(); }
        private static bool Probe()
        {
            try { return AccessTools.Method(typeof(TendUtility), "DoTend", new[] { typeof(Pawn), typeof(Pawn), typeof(Medicine) }) != null; }
            catch (Exception) { return false; }
        }
        [HarmonyPostfix]
        public static void Postfix(Pawn doctor, Pawn patient)
        {
            if (!TargetMethodExists || doctor == null || patient == null || doctor == patient) return;
            try
            {
                var economy = PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy.Current;
                if (economy != null) economy.ReportTendDelivered(doctor, patient, Find.TickManager?.TicksGame ?? 0);
            }
            catch (Exception error) { Log.Warning("[PC][Economy] tend evidence failed: " + error.Message); }
        }
    }
}
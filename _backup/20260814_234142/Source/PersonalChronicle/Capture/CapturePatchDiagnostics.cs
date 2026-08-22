using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// Startup diagnostics for capture targets. Harmony can skip an unresolved
    /// target without making the gameplay symptom obvious; failing loudly here
    /// keeps an event source from silently becoming static after a game update.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CapturePatchDiagnostics
    {
        static CapturePatchDiagnostics()
        {
            Verify(
                "Pawn.SetFaction",
                typeof(Pawn),
                nameof(Pawn.SetFaction),
                new Type[] { typeof(Faction), typeof(Pawn) });
            Verify(
                "Pawn.Kill",
                typeof(Pawn),
                nameof(Pawn.Kill),
                new Type[] { typeof(DamageInfo?), typeof(Hediff) });
            Verify(
                "IncidentWorker.TryExecute",
                typeof(IncidentWorker),
                nameof(IncidentWorker.TryExecute),
                new Type[] { typeof(IncidentParms) });
            Verify(
                "Frame.CompleteConstruction",
                typeof(Frame),
                nameof(Frame.CompleteConstruction),
                new Type[] { typeof(Pawn) });
            Verify(
                "Pawn_RelationsTracker.AddDirectRelation",
                typeof(Pawn_RelationsTracker),
                nameof(Pawn_RelationsTracker.AddDirectRelation),
                new Type[] { typeof(PawnRelationDef), typeof(Pawn) });
            Verify(
                "Pawn_RelationsTracker.RemoveDirectRelation",
                typeof(Pawn_RelationsTracker),
                nameof(Pawn_RelationsTracker.RemoveDirectRelation),
                new Type[] { typeof(PawnRelationDef), typeof(Pawn) });
        }

        private static void Verify(string label, Type type, string methodName, Type[] argumentTypes)
        {
            if (AccessTools.Method(type, methodName, argumentTypes) == null)
            {
                ChronicleLog.Error(ChronicleLog.Category.Capture, "capture target missing: " + label);
            }
        }
    }
}

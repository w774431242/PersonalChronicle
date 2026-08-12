using System;
using HarmonyLib;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// v3.1 P3: capture significant social relation formed/ended events.
    ///
    /// Patch 档案:
    ///   Patch Target      : Pawn_RelationsTracker.AddDirectRelation /
    ///                       RemoveDirectRelation(PawnRelationDef, Pawn)
    ///   Patch Type        : Postfix (read-only)
    ///   Reason            : archive lover/spouse/family ties for Social tab
    ///   Alternative       : poll relations each tick (rejected: cost + missed ends)
    ///   Compatibility Risk: low — many mods add relations; we only read args
    ///   Execution Order   : none required
    ///   Failure Behavior  : catch → Log.Warning, original unaffected
    /// </summary>
    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.AddDirectRelation))]
    public static class Patch_AddDirectRelation
    {
        public static void Postfix(Pawn_RelationsTracker __instance, PawnRelationDef def, Pawn otherPawn)
        {
            try
            {
                Pawn self = GetPawn(__instance);
                if (self == null || def == null || otherPawn == null)
                {
                    return;
                }
                if (!SocialRelationFilter.IsSignificant(def))
                {
                    return;
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                service.OnRelationChanged(self, otherPawn, def, formed: true);
            }
            catch (Exception ex)
            {
                Log.Warning("PersonalChronicle: AddDirectRelation patch failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Pawn_RelationsTracker.pawn is a private field in 1.6 — read via Traverse.
        /// </summary>
        internal static Pawn GetPawn(Pawn_RelationsTracker tracker)
        {
            if (tracker == null)
            {
                return null;
            }
            return Traverse.Create(tracker).Field("pawn").GetValue<Pawn>();
        }
    }

    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.RemoveDirectRelation),
        new Type[] { typeof(PawnRelationDef), typeof(Pawn) })]
    public static class Patch_RemoveDirectRelation
    {
        public static void Postfix(Pawn_RelationsTracker __instance, PawnRelationDef def, Pawn otherPawn)
        {
            try
            {
                Pawn self = Patch_AddDirectRelation.GetPawn(__instance);
                if (self == null || def == null || otherPawn == null)
                {
                    return;
                }
                if (!SocialRelationFilter.IsSignificant(def))
                {
                    return;
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                service.OnRelationChanged(self, otherPawn, def, formed: false);
            }
            catch (Exception ex)
            {
                Log.Warning("PersonalChronicle: RemoveDirectRelation patch failed: " + ex.Message);
            }
        }
    }
}

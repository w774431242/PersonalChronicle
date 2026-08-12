using System;
using HarmonyLib;
using PersonalChronicle.Application;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// v4.9: captures equipment decommission (退役仪式) — a read-only "death
    /// record" for archived weapon/apparel things at destroy time. Never prevents
    /// the destroy; only writes when the thing is archive-relevant (equipable) and
    /// already registered as an archive object.
    ///
    /// Patch 档案:
    ///   Patch Target      : Verse.Thing.Destroy(DestroyMode)
    ///   Patch Type        : Postfix (read-only, never skips the original)
    ///   Reason            : record DecommissionRecord (thing's death record)
    ///   Alternative       : Thing.Destroyed (event; no destroy-mode/state context),
    ///                       Thing.DeSpawn (spawn-based, misses in-holder destroys)
    ///   Compatibility Risk: low — Destroy is a virtual method with a stable
    ///                       signature; other mods patch it rarely, and a Postfix
    ///                       is safe in any execution order
    ///   Failure Behavior  : catch → Log.Warning, original method unaffected
    /// </summary>
    [HarmonyPatch(typeof(Thing), "Destroy", new Type[] { typeof(DestroyMode) })]
    public static class Patch_ThingDestroy
    {
        /// <summary>
        /// Cached probe of the patched target signature (same pattern as
        /// Patch_GenRecipe): if the signature drifts the [HarmonyPatch] silently
        /// skips, so we probe once at startup and log loudly.
        /// </summary>
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();

        private static bool ProbeTargetMethod()
        {
            bool found = AccessTools.Method(
                typeof(Thing),
                "Destroy",
                new Type[] { typeof(DestroyMode) }) != null;
            if (!found)
            {
                Log.Error("PersonalChronicle: Thing.Destroy(DestroyMode) target signature changed; decommission capture silently disabled - update Patch_ThingDestroy");
            }
            return found;
        }

        public static void Postfix(Thing __instance, DestroyMode mode)
        {
            try
            {
                if (__instance == null || __instance.def == null)
                {
                    return;
                }
                // Archive scope: only equipment the archive policy captures can be
                // a chronicle Thing; buildings/raw materials/food are excluded.
                // Must agree with ArchiveService.IsEquipableDef so the decommission
                // capture scope matches the registered thing set.
                if (!Domain.ThingArchivePolicy.Captures(__instance.def))
                {
                    return;
                }
                // Only chronicle-relevant destroys (player map). Off-map destroys
                // (caravan/inventory drops) have no place context worth recording.
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                Pawn holder = __instance.ParentHolder as Pawn;
                service.OnThingDestroyed(__instance, holder);
            }
            catch (Exception ex)
            {
                Log.Warning("PersonalChronicle: Thing.Destroy patch failed: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Startup hook that forces the one-time signature probe for
    /// Thing.Destroy(DestroyMode). Kept in this file so the capture layer stays
    /// self-contained (no dependency on ChronicleStartup).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patch_ThingDestroyStartup
    {
        static Patch_ThingDestroyStartup()
        {
            _ = Patch_ThingDestroy.TargetMethodExists;
        }
    }
}

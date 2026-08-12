using System;
using HarmonyLib;
using PersonalChronicle.Application;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// Captures crafted weapons/apparel/equipment. Postfix on the static
    /// GenRecipe.PostProcessProduct — the per-product finishing step of
    /// production, called for every product that passes through
    /// MakeRecipeProducts (quality/art/style assignment).
    ///
    /// 1.6 target verification (decompiled Assembly-CSharp):
    ///   Verse.GenRecipe::PostProcessProduct(Verse.Thing product,
    ///       Verse.RecipeDef recipeDef, Verse.Pawn worker,
    ///       RimWorld.Precept_ThingStyle precept, Verse.ThingStyleDef style,
    ///       System.Nullable`1<System.Int32> overrideGraphicIndex) [static,
    ///       PRIVATE]
    ///   Chosen over GenRecipe.MakeRecipeProducts, which is an iterator method
    ///   (yield) — its body runs lazily on MoveNext, so a Postfix would read
    ///   the state machine before any product exists. Decompiled MoveNext
    ///   confirms it calls PostProcessProduct once per product; PostProcessProduct
    ///   is therefore the most stable per-product "made" point with the finished
    ///   Thing as an argument. Private is fine for Harmony (string method name).
    ///
    /// Patch 档案:
    ///   Patch Target      : Verse.GenRecipe.PostProcessProduct(Thing, RecipeDef,
    ///                       Pawn, Precept_ThingStyle, ThingStyleDef, int?)
    ///   Patch Type        : Postfix (read-only, never skips the original)
    ///   Reason            : record a Crafted event (PersonalChronicleEventCrafted)
    ///                       with the finished thing as Primary
    ///   Alternative       : Bill_Production.Notify_IterationCompleted (rejected:
    ///                       virtual, no product argument; loses the thing edge);
    ///                       GenRecipe.MakeRecipeProducts (rejected: iterator
    ///                       method, product not materialized in Postfix)
    ///   Compatibility Risk: low — PostProcessProduct is rarely patched by other
    ///                       mods (private static); [HarmonyAfter] not required
    ///   Execution Order   : no ordering contract; read-only Postfix is safe in
    ///                       any order
    ///   Failure Behavior  : catch → Log.Warning, original method unaffected
    /// </summary>
    [HarmonyPatch(
        typeof(GenRecipe),
        "PostProcessProduct",
        new Type[]
        {
            typeof(Thing),
            typeof(RecipeDef),
            typeof(Pawn),
            typeof(Precept_ThingStyle),
            typeof(ThingStyleDef),
            typeof(int?)
        })]
    public static class Patch_GenRecipe
    {
        /// <summary>
        /// Cached probe of the patched target signature. The [HarmonyPatch]
        /// attribute resolves lazily and SILENTLY skips a target whose
        /// signature has drifted (the patch just stops applying, no error),
        /// so we verify the target ourselves exactly once at startup instead
        /// of letting the Crafted capture degrade without a trace. Touched
        /// by Patch_GenRecipeStartup to force the one-time probe.
        /// </summary>
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();

        private static bool ProbeTargetMethod()
        {
            bool found = AccessTools.Method(
                typeof(GenRecipe),
                "PostProcessProduct",
                new Type[]
                {
                    typeof(Thing),
                    typeof(RecipeDef),
                    typeof(Pawn),
                    typeof(Precept_ThingStyle),
                    typeof(ThingStyleDef),
                    typeof(int?)
                }) != null;
            if (!found)
            {
                ChronicleLog.Error(ChronicleLog.Category.Capture, "GenRecipe.PostProcessProduct target signature changed; Crafted capture silently disabled - update Patch_GenRecipe");
            }
            return found;
        }

        public static void Postfix(Thing product, RecipeDef recipeDef, Pawn worker)
        {
            try
            {
                if (product == null || product.def == null)
                {
                    return;
                }
                // Only chronicle-relevant production: record player-faction work.
                if (worker == null || worker.Faction == null || !worker.Faction.IsPlayer)
                {
                    return;
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                service.OnThingCrafted(product, worker);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Capture, "GenRecipe.PostProcessProduct patch failed: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Startup hook that forces the one-time signature probe for
    /// GenRecipe.PostProcessProduct. Kept inside this file so the capture
    /// layer stays self-contained (no dependency on ChronicleStartup).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patch_GenRecipeStartup
    {
        static Patch_GenRecipeStartup()
        {
            _ = Patch_GenRecipe.TargetMethodExists;
        }
    }
}

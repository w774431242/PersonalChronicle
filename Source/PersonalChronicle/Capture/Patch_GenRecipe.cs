using System;
using HarmonyLib;
using PersonalChronicle.Application;
using PersonalChronicle.Application.Effects;
using PersonalChronicle.Domain.Profession;
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
                // P1 CAREER-001：制造事实写入职业 ledger（BE-005）。带 recipe 可派生技能。
                service.RecordCareerProduced(product, recipeDef, worker);
                // P3 专业效果：成品品质偏置（精密制造能力 → 真实品质档位偏移）。
                // 复用本 Postfix 挂载点（原版已 SetQuality，此处事后覆盖），零新增 Patch。
                TryApplyQualityBias(product, recipeDef, worker);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Capture, "GenRecipe.PostProcessProduct patch failed: " + ex.Message);
            }
        }

        /// <summary>
        /// P3 专业效果：成品品质偏置（V2.0 §11 适配层，品质效果挂载点）。
        /// 原版 PostProcessProduct 内部已按 GenerateQualityCreatedByPawn 设好品质；
        /// 本方法在其后（Postfix）按 pawn 的专业能力重算并覆盖品质档位。
        /// 范围精确（仅制造产物），非玩家派系/无品质物品/无职业效果均零影响。
        /// </summary>
        private static void TryApplyQualityBias(Thing product, RecipeDef recipeDef, Pawn worker)
        {
            if (product == null || worker == null)
            {
                return;
            }
            CompQuality compQuality = product.TryGetComp<CompQuality>();
            if (compQuality == null)
            {
                return;
            }
            int levels = ProfessionalEffectService.GetQualityBiasLevels(worker, recipeDef);
            if (levels == 0)
            {
                return;
            }
            QualityCategory current = compQuality.Quality;
            int targetIndex = ProfessionalEffectResolver.ClampQuality((int)current, levels);
            QualityCategory target = (QualityCategory)targetIndex;
            if (target == current)
            {
                return;
            }
            compQuality.SetQuality(target, null);
            if (Prefs.DevMode)
            {
                ChronicleLog.Info(ChronicleLog.Category.Capture,
                    "[CAREER] QualityBias applied: pawn=" + worker.GetUniqueLoadID()
                    + " recipe=" + (recipeDef != null ? recipeDef.defName : "null")
                    + " " + current + " -> " + target + " (levels=" + levels + ")");
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

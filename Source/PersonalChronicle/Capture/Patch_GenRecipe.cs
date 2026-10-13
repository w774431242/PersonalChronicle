using System;
using HarmonyLib;
using PersonalChronicle.Application;
using PersonalChronicle.Application.Effects;
using PersonalChronicle.Application.Workshop;
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
    /// <para><b>归位（2026-09-21 审计修复 · CHG-SHOP-AUDIT-001 · A-2）</b>：本 Patch 原内联
    /// 约 200 行产出业务（品质偏置 / 额外产出 / 退料 / 回炉 / 加成登记）—— 违反归位铁律
    /// 「Capture 只做钩子 → 调 Application，不放业务」。业务已整体下沉到
    /// <see cref="WorkshopProductionEffects"/>（Application，概率判定走 Domain 纯规则），
    /// 本类只保留：null/派系守卫 + 转调（履历、物品流、产出效果）。行为逐项等价。</para>
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
    ///                       with the finished thing as Primary; plus delegation of
    ///                       workshop/profession production effects
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
                // REQ-MVS-013（CHG-MVS-ITEMFLOW-001）：物品级产消读数（市场面板展开行「生产」列）。
                // 与职业 ledger 的 prodQty 同口径（Max(1, stackCount)）；每件成品只记一次。
                service.RecordItemFlow(product.def.defName, Math.Max(1, product.stackCount), true);
                // 车间 + 专业产出效果（品质偏置 / 耐久精度登记 / 额外产出 / 退料 / 回炉）。
                // 复用本 Postfix 挂载点（原版已 SetQuality，此处事后覆盖），零新增 Patch。
                WorkshopProductionEffects.Apply(product, recipeDef, worker);
                // 车间绩效台账（2026-09-25）：在岗期间的产出计入其车间的按人台账（生产率分子）。
                // 2026-10-02 审计修复（P1-C）：改走 Application 契约（WorkshopBonusAccess.NotifyProductMade，
                // 实现由 WorkshopBonusAccessBinder 注入），消除 Capture → Automation 直读（跨层红线）。
                // CHG-20261005-DEVCOLS：把成品一并传下去（设备台账「累计产值」用它取 MarketValue）——
                // 不在归账侧从 worker.CurJob 反推：Postfix 时刻 job 可能已清/已切换。
                WorkshopBonusAccess.NotifyProductMade(worker, product);
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

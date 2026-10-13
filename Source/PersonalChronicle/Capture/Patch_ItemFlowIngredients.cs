using System;
using System.Collections.Generic;
using HarmonyLib;
using PersonalChronicle.Application;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// 物品级产消读数（`REQ-MVS-013`）：**配方原料消耗**捕获点。
    ///
    /// <para>1.6 target verification（Cecil 静态核实，Assembly-CSharp 1.6.4871）：
    ///   <c>Verse.GenRecipe.MakeRecipeProducts(RecipeDef recipeDef, Pawn worker,
    ///   List&lt;Thing&gt; ingredients, Thing dominantIngredient, IBillGiver billGiver,
    ///   Precept_ThingStyle precept, ThingStyleDef style, Nullable&lt;Int32&gt; overrideGraphicIndex)</c>
    ///   [public, static]。**全库唯一调用点** = <c>Toils_Recipe.&lt;FinishRecipeAndStartStoringProduct&gt;b__1</c>
    ///   （覆盖工作台 / CraftingSpot / 屠宰 / 熔炼 / 切割等一切配方路径）。</para>
    ///
    /// <para><b>为什么选 Prefix 而不是 Postfix</b>（GATE-ARCH C-02 更正后的口径）：
    /// 本方法虽为 iterator（Prefix/Postfix 都在迭代开始前执行，两者时点等价），选 Prefix 的
    /// 真正理由是 —— ① 我们记录的是**入参**，而参数在调用瞬间即事实；② 不依赖原方法完成路径。
    /// ⚠ 绝不能 <c>return false</c>（会吞掉原方法 ⇒ 配方零产出）。</para>
    ///
    /// <para><b>数量语义（Cecil 实证）</b>：调用链顺序 = <c>CalculateIngredients</c>（按配方量
    /// <c>SplitOff</c> 取出小叠）→ <b>本方法</b> → <c>ConsumeIngredients</c>（消费）。
    /// ⇒ 进入本方法时 <c>ingredients</c> 里每个 Thing 的 <c>stackCount</c> **正好是本次要消耗的
    /// 件数** —— 如实记录即可，无需复算配方。</para>
    ///
    /// <para><b>已知偏差（登记不修）</b>：少数覆写 <c>RecipeWorker.ConsumeIngredient</c> 的
    /// 特殊配方（如不消耗原料的复制类配方）会在本方法后"不消费" —— 本捕获会多记那些量。</para>
    ///
    /// Patch 档案：
    ///   Patch Target      : Verse.GenRecipe.MakeRecipeProducts(RecipeDef, Pawn, List`1[Thing], Thing,
    ///                       IBillGiver, Precept_ThingStyle, ThingStyleDef, Nullable`1[Int32])
    ///   Patch Type        : Prefix (read-only, never returns false, never skips the original)
    ///   Reason            : 物品级产消读数（市场面板展开行「消费」列）
    ///   Alternative       : `Bill_Production.Notify_IterationCompleted` 加 ingredients 实参 —— **否决**：
    ///                       覆盖差集（屠宰 / 熔炼 / 非 Bill 配方不经过它）；本方法唯一调用点 =
    ///                       Toils_Recipe（Cecil refs 实证），覆盖全部配方路径
    ///   Compatibility Risk: 中 — 制造流是公共 API；只读 Prefix 不改变参数、不跳过原方法 ⇒ 共存安全
    ///   Execution Order   : 无顺序契约；read-only 任意顺序均安全
    ///   Failure Behavior  : catch → Log.Warning（节流交给既有日志类别），原方法不受影响
    ///   Rollback          : 删除本文件即回滚（PatchAll 反射扫描）；EXC-2026-001 台账同步变更记录
    /// </summary>
    [HarmonyPatch(
        typeof(GenRecipe),
        nameof(GenRecipe.MakeRecipeProducts),
        new Type[]
        {
            typeof(RecipeDef),
            typeof(Pawn),
            typeof(List<Thing>),
            typeof(Thing),
            typeof(IBillGiver),
            typeof(Precept_ThingStyle),
            typeof(ThingStyleDef),
            typeof(int?)
        })]
    public static class Patch_ItemFlowIngredients
    {
        /// <summary>签名探测（漂移 ⇒ Harmony 静默跳过 ⇒ 启动期显式报出）。</summary>
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();

        private static bool ProbeTargetMethod()
        {
            bool found = AccessTools.Method(
                typeof(GenRecipe),
                "MakeRecipeProducts",
                new Type[]
                {
                    typeof(RecipeDef),
                    typeof(Pawn),
                    typeof(List<Thing>),
                    typeof(Thing),
                    typeof(IBillGiver),
                    typeof(Precept_ThingStyle),
                    typeof(ThingStyleDef),
                    typeof(int?)
                }) != null;
            if (!found)
            {
                ChronicleLog.Error(ChronicleLog.Category.Capture,
                    "GenRecipe.MakeRecipeProducts target signature changed; item-flow ingredient capture disabled - update Patch_ItemFlowIngredients");
            }
            return found;
        }

        public static void Prefix(RecipeDef recipeDef, Pawn worker, List<Thing> ingredients)
        {
            try
            {
                if (worker == null || ingredients == null || ingredients.Count == 0)
                {
                    return;
                }
                if (worker.Faction == null || !worker.Faction.IsPlayer)
                {
                    return;
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                // ---- 主产物 defName（REQ-MVS-018，2026-09-20：流向记账）----
                // 语义：本次迭代消耗的每种原料 ⇒ 流向该产物（"中心物品 → 各用途"的唯一真实来源）。
                // ⚠ 多产物配方**只取首个产物**：把同一次消耗重复计入每个产物会让份额整体翻倍
                //   （登记为局限；将来要精确分摊需按产物数量/价值拆 —— 那是估算，不是实测）。
                string productDefName = null;
                if (recipeDef != null && recipeDef.products != null && recipeDef.products.Count > 0)
                {
                    Verse.ThingDefCountClass main = recipeDef.products[0];
                    if (main != null && main.thingDef != null)
                    {
                        productDefName = main.thingDef.defName;
                    }
                }
                for (int i = 0; i < ingredients.Count; i++)
                {
                    Thing ingredient = ingredients[i];
                    if (ingredient == null || ingredient.def == null)
                    {
                        continue;
                    }
                    service.RecordItemFlow(ingredient.def.defName, ingredient.stackCount, false);
                    if (!string.IsNullOrEmpty(productDefName))
                    {
                        service.RecordMaterialFlow(
                            ingredient.def.defName, productDefName, ingredient.stackCount);
                    }
                }
            }
            catch (Exception ex)
            {
                // ⚠ Prefix 内必须全包：外抛会中断原方法（配方零产出）。
                ChronicleLog.Warning(ChronicleLog.Category.Capture,
                    "MakeRecipeProducts prefix (item flow) failed: " + ex.Message);
            }
        }
    }

    /// <summary>启动期一次性强制探测（签名漂移 ⇒ 启动日志可见）。</summary>
    [StaticConstructorOnStartup]
    public static class Patch_ItemFlowIngredientsStartup
    {
        static Patch_ItemFlowIngredientsStartup()
        {
            _ = Patch_ItemFlowIngredients.TargetMethodExists;
        }
    }
}

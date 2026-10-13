using System;
using HarmonyLib;
using PersonalChronicle.Application;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// 物品级产消读数（`REQ-MVS-013`）：**收获 / 砍伐产物**捕获点。
    ///
    /// <para>1.6 target verification（Cecil 静态核实，Assembly-CSharp 1.6.4871）：
    ///   <c>RimWorld.Plant.PlantCollected(Pawn by, PlantDestructionMode plantDestructionMode)</c>
    ///   [public, virtual]。**原版唯一调用点** =
    ///   <c>JobDriver_PlantWork.&lt;MakeNewToils&gt;b__1</c>（收获与砍伐共用）。
    ///   ⚠ 本 Mod 自身的农机（<c>CompAutoAgriculture</c>）会以 <c>by == null</c> 调用 —— 见守卫。</para>
    ///
    /// <para><b>为什么选 Prefix</b>：本方法体内会 <c>Destroy</c> 植株（HarvestDestroys 分支），
    /// Postfix 时 <c>YieldNow()</c> 的输入（HitPoints 等）已不可靠；Prefix 时植株完好 ⇒ 复算与
    /// 原版 Toil 同状态。产量公式复刻 <c>JobDriver_PlantWork</c> b__1 的 IL：
    /// <c>count = YieldNow()</c>；<c>f = pawn.GetStatValue(PlantHarvestYield|DrugHarvestYield)</c>；
    /// <c>f &gt; 1 ⇒ count = RoundRandom(count × f)</c>。⚠ 绝不 <c>return false</c>。</para>
    ///
    /// <para><b>已知偏差（登记不修）</b>：① <c>harvestFailable</c> 且 <c>f &lt; 1</c> 的植物
    /// 可能把"失败的收获尝试"记入（该字段默认 false，影响面≈0；不做 Rand 复刻是因为它会
    /// 扰动全局随机序列）；② <c>ThingComp.GetAdditionalHarvestYield</c> 的附加产出不经本公式。</para>
    ///
    /// Patch 档案：
    ///   Patch Target      : RimWorld.Plant.PlantCollected(Pawn, PlantDestructionMode)
    ///   Patch Type        : Prefix (read-only, never returns false, never skips the original)
    ///   Reason            : 物品级产消读数（市场面板展开行「生产」列）
    ///   Alternative       : `JobDriver_PlantWork` 完成 toil —— **否决**（编译器生成 lambda/迭代器
    ///                       目标脆弱，同族先例见 Patch_GenConstruct 的 Alternatives）；
    ///                       `Plant.YieldNow` 直挂 —— **否决**（可被 UI/预览多次调用 ⇒ 重复计数）
    ///   Compatibility Risk: 中 — virtual 方法：若第三方子类覆写且不调 base，本 patch 静默不命中
    ///                       （不崩、不误报；登记为已知捕获缺口）；只读 Prefix 共存安全
    ///   Execution Order   : 无顺序契约；read-only 任意顺序均安全
    ///   Failure Behavior  : catch → Log.Warning，原方法不受影响
    ///   Rollback          : 删除本文件即回滚；EXC-2026-001 台账同步变更记录
    /// </summary>
    [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected), new Type[] { typeof(Pawn), typeof(PlantDestructionMode) })]
    public static class Patch_ItemFlowPlant
    {
        /// <summary>签名探测（漂移 ⇒ Harmony 静默跳过 ⇒ 启动期显式报出）。</summary>
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();

        private static bool ProbeTargetMethod()
        {
            bool found = AccessTools.Method(
                typeof(Plant),
                "PlantCollected",
                new Type[] { typeof(Pawn), typeof(PlantDestructionMode) }) != null;
            if (!found)
            {
                ChronicleLog.Error(ChronicleLog.Category.Capture,
                    "Plant.PlantCollected target signature changed; item-flow harvest capture disabled - update Patch_ItemFlowPlant");
            }
            return found;
        }

        internal static string CompletionOrder(PlantDestructionMode mode, bool isTree)
        {
            if (mode == PlantDestructionMode.Cut) return "CutPlants";
            if (mode == PlantDestructionMode.Chop) return isTree ? "ChopWood" : "Harvest";
            return null;
        }

        internal static void Prefix(Plant __instance, Pawn by, PlantDestructionMode __1, out VanillaOrderIntent __state)
        {
            __state = null;
            // Harmony binds argument names unless a positional __n alias is used. The native
            // second parameter is plantDestructionMode; do not depend on a local name like mode.
            PlantDestructionMode mode = __1;
            try
            {
                if (__instance == null || __instance.def == null || __instance.def.plant == null)
                {
                    return;
                }
                // ⚠ by == null 首行守卫：本 Mod 农机（CompAutoAgriculture）以 null 调用本方法，
                // 且"机器收成无人力"与职业捕获的既有契约一致 ⇒ 不记（跳过一切计算，防 NRE）。
                if (by == null || by.Faction == null || !by.Faction.IsPlayer)
                {
                    return;
                }
                // Snapshot player intent before the original destroys the plant.
                // Payment is per completed target, independent of harvested yield.
                // Chop is shared by tree felling and crop harvesting in vanilla.
                try
                {
                    string order = CompletionOrder(mode, __instance.def.plant.IsTree);
                    if (order != null && by.CurJob?.targetA.Thing == __instance)
                        __state = GameComponent_PawnEconomy.Current?.CaptureVanillaOrderIntent(order, __instance, by);
                }
                catch (Exception error)
                {
                    Log.Warning("[PC][Economy] vanilla plant work evidence failed: " + error.Message);
                }
                ThingDef harvested = __instance.def.plant.harvestedThingDef;
                if (harvested == null)
                {
                    return; // 无产物植物（观赏/装饰，或被割除的草）：劳动已上报，产物流到此为止
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }

                // 复刻 JobDriver_PlantWork b__1：stat 选择 → YieldNow → stat 修正。
                StatDef yieldStat = (harvested.IsDrug || __instance.def.plant.drugForHarvestPurposes)
                    ? StatDefOf.DrugHarvestYield
                    : StatDefOf.PlantHarvestYield;
                float factor = by.GetStatValue(yieldStat);
                int count = __instance.YieldNow();
                if (factor > 1f)
                {
                    count = GenMath.RoundRandom(count * factor);
                }
                if (count <= 0)
                {
                    return; // 未成熟 / 歉收 ⇒ 无产出（真值：不记）
                }
                service.RecordItemFlow(harvested.defName, count, true);
            }
            catch (Exception ex)
            {
                // ⚠ Prefix 内必须全包：外抛会中断原方法（收获失败）。
                ChronicleLog.Warning(ChronicleLog.Category.Capture,
                    "PlantCollected prefix (item flow) failed: " + ex.Message);
            }
        }

        internal static void Postfix(VanillaOrderIntent __state, bool __runOriginal)
        {
            if (__state == null || !__runOriginal) return;
            try
            {
                int now = Find.TickManager.TicksGame;
                GameComponent_PawnEconomy.Current?.ReportVanillaOrderCompletion(__state, now,
                    __state.Order == "Harvest" ? "harvest:" + now : null);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[PC][Economy] plant order completion failed: " + ex, 0x5A1810);
            }
        }
    }

    /// <summary>启动期一次性强制探测。</summary>
    [StaticConstructorOnStartup]
    public static class Patch_ItemFlowPlantStartup
    {
        static Patch_ItemFlowPlantStartup()
        {
            _ = Patch_ItemFlowPlant.TargetMethodExists;
        }
    }
}

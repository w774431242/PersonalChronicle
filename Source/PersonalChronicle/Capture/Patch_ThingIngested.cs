using System;
using HarmonyLib;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// v1.1.4 损耗宫格捕获点：Verse.Thing.Ingested(Pawn ingester, float nutritionWanted)。
    ///
    /// 1.6 target verification（反射核验）：Thing.Ingested 返回 float、参数 (Pawn, float)、
    /// 非 virtual —— 是所有"可摄入物被吃掉"（食物/饮料/成瘾品/药品）的统一完成点，
    /// Harmony Postfix 直接命中，不涉及 override 链。比 patch Pawn.Eat 更稳（后者在
    /// 1.6 已不存在）。
    ///
    /// 只对玩家派系、chronicle 相关、humanlike 的殖民者记录；计价与类目聚合在
    /// <see cref="IArchiveService.OnThingConsumed"/> 完成（按 ThingDef.BaseMarketValue）。
    /// 高频进食不写事件流，仅累加 <see cref="ConsumptionAccumulator"/>。
    ///
    /// Patch 档案：
    ///   Patch Target      : Verse.Thing.Ingested(Verse.Pawn, System.Single)
    ///   Patch Type        : Postfix (read-only, never skips the original)
    ///   Reason            : 记录人物消耗品计价（损耗宫格数据源）
    ///   Alternative       : Pawn.Eat (rejected: 1.6 不存在)；JobDriver_Ingest Toils
    ///                       (rejected: 分散在多子类，patch 面广、易漏)
    ///   Compatibility Risk: 低 — Thing.Ingested 是引擎稳定入口，非 virtual 语义明确
    /// </summary>
    [HarmonyPatch(typeof(Thing), "Ingested")]
    public static class Patch_ThingIngested
    {
        /// <summary>
        /// 缓存签名探测。方法签名漂移时 [HarmonyPatch] 会静默跳过，故启动时显式探测。
        /// </summary>
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();

        private static bool ProbeTargetMethod()
        {
            bool found = AccessTools.Method(
                typeof(Thing),
                "Ingested",
                new Type[] { typeof(Pawn), typeof(float) }) != null;
            if (!found)
            {
                ChronicleLog.Error(ChronicleLog.Category.Capture, "Thing.Ingested target signature changed; consumption capture silently disabled - update Patch_ThingIngested");
            }
            return found;
        }

        public static void Postfix(Pawn ingester, Thing __instance)
        {
            try
            {
                if (ingester == null || __instance == null || __instance.def == null)
                {
                    return;
                }
                if (!ingester.RaceProps.Humanlike)
                {
                    return;
                }
                if (ingester.Faction == null || !ingester.Faction.IsPlayer)
                {
                    return;
                }
                if (!ChronicleColonistScanner.TryClassifyCurrent(ingester, out _))
                {
                    return;
                }
                IArchiveService service = PersonalChronicleMod.ArchiveService;
                if (service == null)
                {
                    return;
                }
                service.OnThingConsumed(ingester, __instance);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Capture, "Thing.Ingested patch failed: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Startup hook that forces the one-time signature probe for
    /// Verse.Thing.Ingested.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patch_ThingIngestedStartup
    {
        static Patch_ThingIngestedStartup()
        {
            _ = Patch_ThingIngested.TargetMethodExists;
        }
    }
}

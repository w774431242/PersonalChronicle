using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    [HarmonyPatch(typeof(Zone_Stockpile), nameof(Zone_Stockpile.GetGizmos))]
    internal static class Patch_PublicStockpileGizmos
    {
        private static void Postfix(Zone_Stockpile __instance, ref IEnumerable<Gizmo> __result)
            => __result = PublicStockpileSettings.Gizmos(__instance, __result);
    }
    [HarmonyPatch(typeof(Zone_Stockpile), nameof(Zone_Stockpile.ExposeData))]
    internal static class Patch_PublicStockpileSave
    {
        private static void Postfix(Zone_Stockpile __instance) => PublicStockpileSettings.Expose(__instance);
    }
    [HarmonyPatch(typeof(Zone_Stockpile), nameof(Zone_Stockpile.Notify_ReceivedThing))]
    internal static class Patch_PublicStockpileReceive
    {
        private static void Postfix(Zone_Stockpile __instance, Thing newItem)
            => GameComponent_EconomyStorage.Current?.ReceivePublicStockpile(__instance, newItem);
    }
    [HarmonyPatch(typeof(Zone_Stockpile), nameof(Zone_Stockpile.AddCell))]
    internal static class Patch_PublicStockpileExpand
    {
        private static void Postfix(Zone_Stockpile __instance, IntVec3 __0)
        {
            if (PublicStockpileSettings.Read(__instance)?.Enabled != true || !__instance.ContainsCell(__0)) return;
            foreach (var item in __0.GetThingList(__instance.Map).ToArray())
                GameComponent_EconomyStorage.Current?.ReceivePublicStockpile(__instance, item);
        }
    }
    [HarmonyPatch(typeof(Zone_Stockpile), nameof(Zone_Stockpile.Accepts))]
    internal static class Patch_PublicStockpileAccepts
    {
        private static void Postfix(Zone_Stockpile __instance, Thing t, ref bool __result)
        {
            if (__result && GameComponent_EconomyStorage.Current?.CanStorePublicStockpile(__instance, t) == false) __result = false;
        }
    }
    [HarmonyPatch(typeof(Zone), nameof(Zone.GetInspectString))]
    internal static class Patch_PublicStockpileInspect
    {
        private static void Postfix(Zone __instance, ref string __result)
        {
            if (!(__instance is Zone_Stockpile zone) || PublicStockpileSettings.Read(zone)?.Enabled != true) return;
            // CHG-20261012-ECON-CONTRACTIZE（用户裁决「公有化后不需要主动设置售价，直接采用原版物品市场价格」）：
            // 与 `Building_EconomyStorage.GetInspectString` **同源同键** —— 定价一律由物品自身 MarketValue 派生，
            // 不再存在人工报价，因此 B1（P2-1）删掉这里残留的「食品售价 / 未报价」两分支与其两个键
            //（`Storage.NoQuote` / `Storage.PriceValue` 中英双份一并删除，LOC-001 无孤儿）。
            __result += "\n" + "PersonalChronicle.Storage.PublicZoneActive".Translate() + "\n"
                + "PersonalChronicle.Storage.PublicMarketPriced".Translate();
        }
    }
}

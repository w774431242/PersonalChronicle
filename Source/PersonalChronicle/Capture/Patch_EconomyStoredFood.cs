using System;
using HarmonyLib;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Capture
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]
    internal static class Patch_StoredFoodIngest
    {
        private static bool Prefix(Thing __instance, Pawn ingester, float nutritionWanted, ref float __result)
        {
            if (__instance.def?.IsNutritionGivingIngestible != true) return true;
            var economy = GameComponent_PawnEconomy.Current;
            return economy == null || economy.BeforeStoredFoodIngest(__instance, ingester, nutritionWanted, out __result);
        }
    }
    [HarmonyPatch(typeof(Toils_Ingest), nameof(Toils_Ingest.FinalizeIngest))]
    internal static class Patch_StoredFoodIngestToil
    {
        private static void Postfix(Pawn ingester, TargetIndex ingestibleInd, Toil __result)
        {
            Action original = __result.initAction;
            __result.initAction = () =>
            {
                Pawn actor = __result.actor;
                Thing food = actor.CurJob?.GetTarget(ingestibleInd).Thing;
                if (food?.def.IsNutritionGivingIngestible == true && GameComponent_EconomyStorage.Current?.CanSelectFood(food, ingester) == false)
                { actor.jobs.EndCurrentJob(JobCondition.Incompletable); return; }
                original?.Invoke();
            };
        }
    }
    [HarmonyPatch(typeof(Thing), nameof(Thing.SplitOff))]
    internal static class Patch_StoredItemSplit
    {
        private static void Prefix(Thing __instance, out StoredItemRight __state) => __state = GameComponent_EconomyStorage.Current?.RightFor(__instance);
        private static void Postfix(Thing __instance, Thing __result, StoredItemRight __state)
            => GameComponent_EconomyStorage.Current?.CopyRights(__state, __instance, __result);
    }
    [HarmonyPatch(typeof(Thing), nameof(Thing.CanStackWith))]
    internal static class Patch_StoredItemStack
    {
        private static void Postfix(Thing __instance, Thing other, ref bool __result)
        { if (__result && GameComponent_EconomyStorage.Current?.CanMerge(__instance, other) == false) __result = false; }
    }
    [HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]
    internal static class Patch_StoredItemAbsorb
    {
        internal struct MergeState { public StoredItemRight Right; public int Count; }
        private static void Prefix(Thing __instance, Thing other, out MergeState __state)
            => __state = new MergeState { Right = GameComponent_EconomyStorage.Current?.RightFor(other), Count = __instance.stackCount };
        private static void Postfix(Thing __instance, MergeState __state)
            => GameComponent_EconomyStorage.Current?.AfterMerge(__instance, __state.Right, __state.Count);
    }
    [HarmonyPatch(typeof(FoodUtility), "SpawnedFoodSearchInnerScan")]
    internal static class Patch_StoredFoodScan
    {
        private static void Prefix(Pawn eater, ref Predicate<Thing> validator)
        {
            Predicate<Thing> original = validator;
            validator = t => (original == null || original(t)) && GameComponent_EconomyStorage.Current?.CanSelectFood(t, eater) != false;
        }
    }
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.BestFoodSourceOnMap))]
    internal static class Patch_StoredFoodMapSearch
    {
        [ThreadStatic] internal static Pawn Eater;
        private static void Prefix(Pawn eater, out Pawn __state) { __state = Eater; Eater = eater; }
        private static void Finalizer(Pawn __state) { Eater = __state; }
    }
    [HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable))]
    internal static class Patch_StoredFoodClosestSearch
    {
        private static void Prefix(ref Predicate<Thing> validator)
        {
            Pawn eater = Patch_StoredFoodMapSearch.Eater;
            if (eater == null) return;
            Predicate<Thing> original = validator;
            validator = t => (original == null || original(t)) && GameComponent_EconomyStorage.Current?.CanSelectFood(t, eater) != false;
        }
    }
    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.BestFoodInInventory))]
    internal static class Patch_StoredFoodInventorySearch
    {
        private static void Postfix(Pawn holder, Pawn eater, FoodPreferability minFoodPref, FoodPreferability maxFoodPref,
            float minStackNutrition, bool allowDrug, bool allowVenerated, ref Thing __result)
        {
            eater ??= holder;
            var storage = GameComponent_EconomyStorage.Current;
            if (__result == null || storage?.CanSelectFood(__result, eater) != false) return;
            __result = null;
            if (holder.inventory == null) return;
            // Vanilla returns the first eligible inventory item. A denied first item must not
            // hide the owner's food later in the inventory; retain all native eligibility filters.
            foreach (Thing food in holder.inventory.innerContainer)
                if (food.def.IsNutritionGivingIngestible && food.IngestibleNow
                    && FoodUtility.WillEat(eater, food, holder, true, allowVenerated)
                    && food.def.ingestible.preferability >= minFoodPref && food.def.ingestible.preferability <= maxFoodPref
                    && (allowDrug || !food.def.IsDrug)
                    && FoodUtility.NutritionForEater(eater, food) * food.stackCount >= minStackNutrition
                    && storage.CanSelectFood(food, eater))
                { __result = food; return; }
        }
    }
    [HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.IsGoodStoreCell))]
    internal static class Patch_StoredItemDestination
    {
        private static void Postfix(IntVec3 c, Map map, Thing t, ref bool __result)
        {
            if (__result && c.GetSlotGroup(map)?.parent is Building_EconomyStorage store
                && GameComponent_EconomyStorage.Current?.CanStore(store, t) == false) __result = false;
            if (__result && c.GetSlotGroup(map)?.parent is Zone_Stockpile zone
                && GameComponent_EconomyStorage.Current?.CanStorePublicStockpile(zone, t) == false) __result = false;
        }
    }
    [HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.Accepts))]
    internal static class Patch_StoredItemAccepts
    {
        private static void Postfix(Building_Storage __instance, Thing t, ref bool __result)
        {
            if (__result && __instance is Building_EconomyStorage store
                && GameComponent_EconomyStorage.Current?.CanStore(store, t) != true) __result = false;
        }
    }
}

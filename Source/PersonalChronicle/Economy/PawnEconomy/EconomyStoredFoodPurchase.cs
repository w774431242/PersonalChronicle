using System;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        internal bool StoragePurchasesAllowed => NewCommitmentsAllowed();
        internal FinancialAccountSnapshot ReadStorageFoodAccount(string pawnId)
        { settlement.BindToMainThread(); return settlement.ReadAccount(pawnId); }

        /// <summary>CHG-20261012：把"一件食物的市价"折算成**每营养价**（minor）= <c>UnitPriceMinor(MarketValue) ÷ 每件营养</c>。
        /// 与 <c>NutritionPrice</c> 组合后在数学上恒等于"件数 × 单件市价"（三处同式的结构不变）。
        /// 市价不可用（≤0）⇒ 返回 0 ⇒ 调用方按"不可计价"拒绝（**绝不白送**）。</summary>
        private static long PerNutritionMarketMinor(Thing item, float nutritionPerUnit)
        {
            if (item == null || nutritionPerUnit <= 0f) return 0L;
            long unitMinor = ItemValuation.UnitPriceMinor(item.MarketValue);
            if (unitMinor <= 0L) return 0L;
            double perNutrition = unitMinor / (double)nutritionPerUnit;
            if (perNutrition >= long.MaxValue) return long.MaxValue;
            return Math.Max(1L, (long)Math.Round(perNutrition, MidpointRounding.AwayFromZero));
        }

        // Called immediately before the real ingestion. The purchased unit is physically split and
        // transferred first, then M11 finalizes payment, then vanilla consumes the owned unit.
        internal bool BeforeStoredFoodIngest(Thing item, Pawn eater, float wanted, out float nutrition)
        {
            nutrition = 0f;
            var storage = GameComponent_EconomyStorage.Current;
            var right = storage?.RightFor(item);
            if (right == null) return true;
            if (right.PendingCommand != null) return false;
            if (right.Kind == StorageRightKind.Unclassified) return true;
            string buyer = eater?.GetUniqueLoadID();
            if (right.Kind == StorageRightKind.Private) return !string.IsNullOrEmpty(right.OwnerKey) && right.OwnerKey == buyer;
            if (right.Kind != StorageRightKind.Public) return false;
            if (PersonalChronicleMod.Settings?.EnablePawnEconomicInstitution != true) return true;
            if (eater?.IsFreeNonSlaveColonist != true || !eater.RaceProps.Humanlike || eater.Map == null
                || !StoragePurchasesAllowed || !item.IngestibleNow || eater.inventory == null
                || item.GetType() != typeof(Thing) && item.GetType() != typeof(ThingWithComps)) return false;
            settlement.BindToMainThread();
            float perUnit = FoodUtility.NutritionForEater(eater, item);
            int units = StorageFoodRules.UnitsForMeal(wanted, perUnit, item.stackCount, item.def.ingestible.maxNumToIngestAtOnce);
            // CHG-20261012（用户裁决「按件单价、参考原版数据」）：每营养价由**这一件自身的市价 ÷ 该件营养**派生
            // ⇒ NutritionPrice(每件营养 × 件数, 每营养价) 在数学上恒等于 **件数 × 单件市价**
            //（units × n × p/n = units × p）；预算/报价/扣款三处同式的结构与 ECON-R09 纪律保持不变。
            long perNutritionMinor = PerNutritionMarketMinor(item, perUnit);
            long amount = Planning.EconomyFoodCostSource.NutritionPrice((double)perUnit * units, perNutritionMinor);
            if (units == 0 || amount <= 0 || amount > MinorCurrency.Maximum
                || (settlement.ReadAccount(buyer)?.AvailableMinor ?? 0) < amount) return false;
            int now = Find.TickManager.TicksGame;
            string command = "stored-food:" + buyer + ":" + now + ":" + settlement.Version + ":" + item.GetUniqueLoadID();
            var prepared = settlement.PreparePurchase(SettlementIntent.ForPurchase(command, settlement.Version, command, buyer, amount, PublicFoodSeller(eater)));
            if (prepared.Code != SettlementCode.Prepared) return false;
            var inventory = eater.inventory.innerContainer;
            Map originMap = item.MapHeld ?? eater.Map;
            IntVec3 originCell = item.Spawned ? item.Position : eater.Position;
            Thing moved = null;
            StoredItemRight delivery = null;
            bool settled = false;
            try
            {
                moved = item.SplitOff(units);
                if (moved == null) { settlement.CancelPurchase(command, "SplitFailed"); return false; }
                delivery = storage.RightFor(moved);
                if (delivery == null || moved.stackCount != units || !inventory.TryAdd(moved, false))
                {
                    if (RestoreStoredFood(moved, inventory, originMap, originCell)) settlement.CancelPurchase(command, "HandOverFailed");
                    else MarkStoragePending(delivery, command, buyer, units);
                    return false;
                }
                MarkStoragePending(delivery, command, buyer, units);
                if (settlement.CompletePurchase(command).Code != SettlementCode.Finalized)
                {
                    if (RestoreStoredFood(moved, inventory, originMap, originCell))
                    { ClearStoragePending(delivery); settlement.CancelPurchase(command, "CompletionFailed"); }
                    return false;
                }
                settled = true;
                delivery.Kind = StorageRightKind.Private; delivery.OwnerKey = buyer; ClearStoragePending(delivery);
                nutrition = moved.Ingested(eater, wanted);
                return false; // The original public stack must not also be consumed.
            }
            catch (Exception error)
            {
                // A paid unit remains owned even if vanilla ingestion fails; no second charge on retry.
                if (!settled)
                {
                    if (RestoreStoredFood(moved, inventory, originMap, originCell))
                    { ClearStoragePending(delivery); settlement.CancelPurchase(command, "ExceptionBeforeCompletion"); }
                    else MarkStoragePending(delivery, command, buyer, units);
                }
                Log.ErrorOnce("[PersonalChronicle] Stored food purchase: " + error, 188101010);
                return false;
            }
        }
        private static bool RestoreStoredFood(Thing moved, ThingOwner inventory, Map map, IntVec3 cell)
        {
            if (moved == null) return true;
            if (moved.Destroyed) return false;
            if (moved.Spawned) return true;
            if (moved.holdingOwner != null && moved.holdingOwner != inventory) return false;
            if (moved.holdingOwner == inventory) inventory.Remove(moved);
            // Keep the actual item and its public ownership. Pending items cannot merge while returning.
            if (map != null && GenPlace.TryPlaceThing(moved, cell, map, ThingPlaceMode.Near)) return true;
            if (!moved.Destroyed && moved.holdingOwner == null) inventory.TryAdd(moved, false);
            return false;
        }
        private static void MarkStoragePending(StoredItemRight right, string command, string buyer, int units)
        { if (right != null) { right.PendingCommand = command; right.PendingBuyer = buyer; right.PendingUnits = units; } }
        private static void ClearStoragePending(StoredItemRight right)
        { if (right != null) { right.PendingCommand = right.PendingBuyer = null; right.PendingUnits = 0; } }
        internal void RecoverStoragePurchase(StoredItemRight right)
        {
            settlement.BindToMainThread();
            var row = settlement.ReadCommand(right.PendingCommand);
            if (row == null || row.Intent.Kind != SettlementKind.Purchase || row.Intent.PayerAccount != right.PendingBuyer) return;
            if (row.Phase == SettlementPhase.Finalized)
            { right.Kind = StorageRightKind.Private; right.OwnerKey = right.PendingBuyer; ClearStoragePending(right); return; }
            if (row.Phase == SettlementPhase.Cancelled) { ClearStoragePending(right); return; }
            var holder = right.Item?.ParentHolder as Pawn_InventoryTracker;
            if (holder?.pawn?.GetUniqueLoadID() != right.PendingBuyer || right.Item.stackCount != right.PendingUnits) return;
            if (settlement.CompletePurchase(right.PendingCommand).Code == SettlementCode.Finalized)
            { right.Kind = StorageRightKind.Private; right.OwnerKey = right.PendingBuyer; ClearStoragePending(right); }
        }
    }
}

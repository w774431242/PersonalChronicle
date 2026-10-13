using System;
using System.Linq;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        internal bool CanSelectStoredApparel(Pawn pawn, Apparel item)
        {
            var right = GameComponent_EconomyStorage.Current?.RightFor(item);
            if (right == null || right.Kind == StorageRightKind.Unclassified) return true;
            if (right.PendingCommand != null) return false;
            if (right.Kind == StorageRightKind.Private) return right.OwnerKey == pawn?.GetUniqueLoadID();
            if (PersonalChronicleMod.Settings?.EnablePawnEconomicInstitution != true) return true;
            if (pawn?.Map == null || !StoragePurchasesAllowed || LowestCounter(pawn.Map.uniqueID, InstitutionKind.Shop) == null) return false;
            long price = ItemValuation.UnitPriceMinor(item.MarketValue);
            return price > 0 && (settlement.ReadAccount(pawn.GetUniqueLoadID())?.AvailableMinor ?? 0) >= price;
        }

        private Job PlanPublicApparelPurchase(Pawn pawn)
        {
            if (!SafeDecision(pawn) || !StoragePurchasesAllowed || pawn.apparel == null) return null;
            var storage = GameComponent_EconomyStorage.Current;
            var item = storage?.PublicMapItems(pawn.Map).OfType<Apparel>()
                .Where(a => a.stackCount == 1 && !a.WornByCorpse && !a.IsForbidden(pawn) && !a.IsBurning()
                    && ApparelUtility.HasPartsToWear(pawn, a.def) && a.PawnCanWear(pawn)
                    && FillsAWearableGap(pawn, a) && CanSelectStoredApparel(pawn, a)
                    && (pawn.outfits?.CurrentApparelPolicy?.filter?.Allows(a) ?? true)
                    && pawn.CanReserveAndReach(a, PathEndMode.Touch, Danger.None)
                    && pawn.apparel.WornApparel.All(w => ApparelUtility.CanWearTogether(w.def, a.def, pawn.RaceProps.body)))
                .OrderBy(a => a.Position.DistanceToSquared(pawn.Position)).ThenBy(a => a.thingIDNumber).FirstOrDefault();
            return item == null ? null : JobMaker.MakeJob(JobDefOf.Wear, item);
        }

        // Payment runs at the real Wear boundary, after the pawn has reached the actual public item.
        // The exact item is held in the buyer's inventory as delivery evidence before finalization.
        internal bool BeforeStoredApparelWear(Pawn pawn, Apparel item)
        {
            var storage = GameComponent_EconomyStorage.Current;
            var right = storage?.RightFor(item);
            if (right == null || right.Kind == StorageRightKind.Unclassified) return true;
            if (right.PendingCommand != null) return false;
            string buyer = pawn?.GetUniqueLoadID();
            if (right.Kind == StorageRightKind.Private) return buyer != null && right.OwnerKey == buyer;
            if (PersonalChronicleMod.Settings?.EnablePawnEconomicInstitution != true) return true;
            if (item.stackCount != 1 || pawn?.IsFreeNonSlaveColonist != true || pawn.Map == null || pawn.inventory == null || !CanSelectStoredApparel(pawn, item)
                || !ApparelUtility.HasPartsToWear(pawn, item.def) || !item.PawnCanWear(pawn)) return false;
            var inventory = pawn.inventory.innerContainer;
            var source = item.holdingOwner;
            if (item.Spawned ? item.Map != pawn.Map || item.Position.DistanceTo(pawn.Position) > 2f
                : source != inventory && source != pawn.carryTracker?.innerContainer) return false;
            var map = item.MapHeld ?? pawn.Map;
            var cell = item.Spawned ? item.Position : pawn.Position;
            storage.ReceivePublicSource(right.SourceKey, right.PricePerNutritionMinor, item);
            right = storage.RightFor(item);
            string command = "stored-apparel:" + Guid.NewGuid().ToString("N");
            long price = ItemValuation.UnitPriceMinor(item.MarketValue);
            var prepared = settlement.PreparePurchase(SettlementIntent.ForPurchase(command, settlement.Version, command, buyer, price,
                InstitutionFundingKey(LowestCounter(pawn.Map.uniqueID, InstitutionKind.Shop))));
            if (prepared.Code != SettlementCode.Prepared) return false;
            bool paid = false;
            try
            {
                if (item.Spawned) item.DeSpawn();
                if (source != null && source != inventory) source.Remove(item);
                if (item.holdingOwner != inventory && !inventory.TryAdd(item, false))
                {
                    if (RestoreStoredFood(item, inventory, map, cell)) settlement.CancelPurchase(command, "ApparelDeliveryFailed");
                    else MarkStoragePending(right, command, buyer, 1);
                    return false;
                }
                MarkStoragePending(right, command, buyer, 1);
                if (settlement.CompletePurchase(command).Code != SettlementCode.Finalized)
                {
                    if (RestoreStoredFood(item, inventory, map, cell))
                    { ClearStoragePending(right); settlement.CancelPurchase(command, "ApparelCompletionFailed"); }
                    return false;
                }
                paid = true; storage.AssignPurchasedItem(item, buyer);
                inventory.Remove(item); // vanilla Wear.TryAdd requires an unheld item
                return true;
            }
            catch (Exception error)
            {
                if (paid) PreservePurchasedApparel(pawn, item);
                else if (RestoreStoredFood(item, inventory, map, cell))
                { ClearStoragePending(right); settlement.CancelPurchase(command, "ApparelException"); }
                else if (!paid) MarkStoragePending(right, command, buyer, 1);
                Log.ErrorOnce("[PersonalChronicle] Public apparel purchase: " + error, 188101011);
                return false;
            }
        }

        internal void PreservePurchasedApparel(Pawn pawn, Apparel item)
        {
            if (item == null || item.Destroyed || item.Spawned || item.holdingOwner != null) return;
            if (GameComponent_EconomyStorage.Current?.RightFor(item)?.OwnerKey == pawn?.GetUniqueLoadID())
                pawn?.inventory?.innerContainer.TryAdd(item, false);
        }
    }
}

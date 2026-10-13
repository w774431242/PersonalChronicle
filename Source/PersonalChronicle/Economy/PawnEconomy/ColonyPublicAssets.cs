using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_EconomyStorage
    {
        internal bool IsColonyPublic(Thing item)
        {
            var right = RightFor(item);
            return item != null && !item.Destroyed && right?.Kind == StorageRightKind.Public && right.PendingCommand == null;
        }
        internal IEnumerable<Thing> PublicMapItems(Map map)
            => map == null ? Enumerable.Empty<Thing>() : map.listerThings.AllThings
                .Where(t => t.Spawned && t.def?.category == ThingCategory.Item && IsColonyPublic(t));
        internal void AssignPurchasedItem(Thing item, string buyer)
        {
            if (item == null || string.IsNullOrEmpty(buyer)) return;
            if (!index.TryGetValue(item, out var right))
            {
                right = new StoredItemRight { Item = item }; rights.Add(right); index[item] = right;
            }
            right.Kind = StorageRightKind.Private; right.OwnerKey = buyer;
            right.PendingCommand = right.PendingBuyer = null; right.PendingUnits = 0;
        }
    }
    public sealed partial class GameComponent_PawnEconomy
    {
        internal string InstitutionFundingKey(Building_EconomyInstitution institution)
            => institution == null ? SettlementLedgerNames.Treasury : SettlementLedgerNames.Institution(institution.GetUniqueLoadID());
        internal long? QueryPublicFundsTotal()
        {
            var audit = settlement.ReadAudit();
            return audit.Fault == null ? SettlementLedgerNames.PublicTotal(audit.Accounts) : (long?)null;
        }
        internal long? QueryInstitutionFunds(Building_EconomyInstitution institution)
        {
            if (settlement.ReadAudit().Fault != null || institution == null) return null;
            return settlement.ReadAccount(InstitutionFundingKey(institution))?.AvailableMinor ?? 0;
        }
        internal IEnumerable<Thing> ShopInventory(Building_EconomyInstitution shop)
        {
            if (shop?.Map == null) return Enumerable.Empty<Thing>();
            var storage = GameComponent_EconomyStorage.Current;
            return shop.Stock.Where(t => t != null && !t.Destroyed && storage?.RightFor(t)?.Kind != StorageRightKind.Private
                    && storage?.RightFor(t)?.PendingCommand == null)
                .Concat(storage?.PublicMapItems(shop.Map) ?? Enumerable.Empty<Thing>())
                .Where(t => t.def != ThingDefOf.Silver).Distinct();
        }
        internal long QueryPendingPublicSilver(Map map)
            => GameComponent_EconomyStorage.Current?.PublicMapItems(map)
                .Where(t => t.def == ThingDefOf.Silver).Sum(t => (long)t.stackCount) ?? 0;
        private string PublicFoodSeller(Pawn pawn) => InstitutionFundingKey(LowestCounter(pawn.Map.uniqueID, InstitutionKind.Shop));
        internal long QueryPayrollFundingMinor(Map map, string institutionKey = null)
        {
            if (settlement.ReadAudit().Fault != null) return 0;
            long available = settlement.ReadAccount(SettlementLedgerNames.Treasury)?.AvailableMinor ?? 0;
            foreach (var bank in PayrollSources(map, institutionKey))
                available += settlement.ReadAccount(InstitutionFundingKey(bank))?.AvailableMinor ?? 0;
            return available;
        }
        private IEnumerable<Building_EconomyInstitution> PayrollSources(Map map, string key)
            => anchors.Where(a => a.Map == map && PayrollInstitution(a) && (PublicBank(a) || a.GetUniqueLoadID() == key));
        // Only the issuing institution and the shared settlement-office budget can fund this wage.
        private string StagePayrollFunds(Map map, long amount, string institutionKey)
        {
            var issuer = PayrollSources(map, institutionKey).FirstOrDefault(a => a.GetUniqueLoadID() == institutionKey);
            if (issuer != null && (settlement.ReadAccount(InstitutionFundingKey(issuer))?.AvailableMinor ?? 0) >= amount)
                return InstitutionFundingKey(issuer);
            if ((settlement.ReadAccount(SettlementLedgerNames.Treasury)?.AvailableMinor ?? 0) >= amount) return SettlementLedgerNames.Treasury;
            if (QueryPayrollFundingMinor(map, institutionKey) < amount) return null;
            foreach (var bank in PayrollSources(map, institutionKey).OrderBy(a => a.GetUniqueLoadID(), StringComparer.Ordinal))
            {
                long remaining = amount - (settlement.ReadAccount(SettlementLedgerNames.Treasury)?.AvailableMinor ?? 0);
                if (remaining <= 0) return SettlementLedgerNames.Treasury;
                string account = InstitutionFundingKey(bank);
                long available = settlement.ReadAccount(account)?.AvailableMinor ?? 0;
                if (available <= 0) continue;
                var transfer = SettlementIntent.ForPublicTransfer("payroll-budget:" + Guid.NewGuid().ToString("N"), settlement.Version,
                    account, SettlementLedgerNames.Treasury, Math.Min(available, remaining));
                if (settlement.TransferPublic(transfer).Code != SettlementCode.Finalized) return null;
            }
            return (settlement.ReadAccount(SettlementLedgerNames.Treasury)?.AvailableMinor ?? 0) >= amount ? SettlementLedgerNames.Treasury : null;
        }
        private static bool PayrollInstitution(Building_EconomyInstitution institution)
        {
            if (institution?.Spawned != true || institution.Destroyed || institution.Faction != Faction.OfPlayer) return false;
            var kind = institution.def.GetModExtension<EconomyInstitutionExtension>()?.kind;
            return kind == InstitutionKind.SettlementOffice || kind == InstitutionKind.LaborOffice || kind == InstitutionKind.WorkshopOffice;
        }
    }
}

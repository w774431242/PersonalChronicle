using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersonalChronicle.Domain.PawnEconomy.Finance;

namespace PersonalChronicle.Application.PawnEconomy.Finance
{
    internal enum LegacyDisposition { PreserveHistory, CancelOrderReleaseReservation, EndContractPreserveWage, RetainOwnership }
    internal sealed class LegacyCutoverItem
    {
        public string Key { get; }
        public LegacyRecordSnapshot Record { get; }
        public LegacyDisposition Disposition { get; }
        public bool Changed { get; }
        public bool OriginalOnly { get; }
        public bool Acknowledged { get; }
        public LegacyCutoverItem(string key, LegacyRecordSnapshot record, LegacyDisposition disposition, bool changed, bool originalOnly, bool acknowledged)
        { Key = key; Record = record; Disposition = disposition; Changed = changed; OriginalOnly = originalOnly; Acknowledged = acknowledged; }
    }
    internal sealed class LegacyCutoverCash
    {
        public LegacyCashClaim Claim { get; }
        public long Allocated { get; }
        public long Remaining => Claim.AmountMinor - Allocated;
        public LegacyCutoverCash(LegacyCashClaim claim, long allocated) { Claim = claim; Allocated = allocated; }
    }
    internal sealed class LegacyCutoverPlan
    {
        public const int MaximumItems = 8192;
        public IReadOnlyList<LegacyCutoverItem> Items { get; }
        public IReadOnlyList<LegacyCutoverCash> Cash { get; }
        public string Fault { get; }
        public bool Complete => Fault == null && Items.All(x => x.Acknowledged) && Cash.All(x => x.Remaining == 0);
        public LegacyCutoverPlan(IEnumerable<LegacyCutoverItem> items, IEnumerable<LegacyCutoverCash> cash, string fault)
        { Items = Array.AsReadOnly(items.ToArray()); Cash = Array.AsReadOnly(cash.ToArray()); Fault = fault; }
        private static string Identity(LegacyRecordSnapshot row) => row.Kind + ":" + row.Id.Value.ToString(CultureInfo.InvariantCulture);
        private static bool Same(LegacyRecordSnapshot a, LegacyRecordSnapshot b) => a.Kind == b.Kind
            && a.Fields.Count == b.Fields.Count && a.Fields.All(x => b.Fields.TryGetValue(x.Key, out string value) && value == x.Value)
            && (a.VisibleConditions == null ? b.VisibleConditions == null : b.VisibleConditions != null && a.VisibleConditions.SequenceEqual(b.VisibleConditions));
        public static LegacyCutoverPlan Build(string originalHash, LegacyReconciliationReport original, IEnumerable<LegacyCashClaim> originalClaims,
            string latestHash, LegacyReconciliationReport latest, IEnumerable<LegacyCashClaim> latestClaims,
            IEnumerable<SettlementEntry> entries, string acknowledgedHash, IReadOnlyDictionary<string, LegacyDisposition> acknowledged)
        {
            var items = new List<LegacyCutoverItem>(); var cash = new List<LegacyCutoverCash>();
            LegacyCutoverPlan Bad(string why) => new LegacyCutoverPlan(items, cash, why);
            if (string.IsNullOrEmpty(originalHash) || string.IsNullOrEmpty(latestHash)
                || original?.DiagnosticChecksPassed == false || latest?.DiagnosticChecksPassed == false) return Bad("InvalidCutoverArchive");
            var before = (original?.Records ?? Array.Empty<LegacyRecordSnapshot>()).ToDictionary(Identity, StringComparer.Ordinal);
            var after = (latest?.Records ?? Array.Empty<LegacyRecordSnapshot>()).ToDictionary(Identity, StringComparer.Ordinal);
            if (before.Keys.Union(after.Keys).Count() > MaximumItems) return Bad("CutoverCapacityReached");
            foreach (var pair in before.Concat(after).GroupBy(x => x.Key).OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                bool exists = after.TryGetValue(pair.Key, out var row); if (!exists) row = before[pair.Key];
                bool changed = !before.TryGetValue(pair.Key, out var prior) || !exists || !Same(prior, row);
                var disposition = exists && row.Kind == "EconomyOrder" && (row.Fields["Status"] == "Prepared" || row.Fields["Status"] == "Active")
                    ? LegacyDisposition.CancelOrderReleaseReservation
                    : exists && row.Kind == "EconomyContract" && row.Fields["Active"] == "True" ? LegacyDisposition.EndContractPreserveWage
                    : exists && row.Kind == "EconomyOwnership" ? LegacyDisposition.RetainOwnership : LegacyDisposition.PreserveHistory;
                string key = (exists ? "current:" : "original:") + pair.Key;
                bool ack = acknowledgedHash == latestHash && acknowledged != null && acknowledged.TryGetValue(key, out var decision) && decision == disposition;
                items.Add(new LegacyCutoverItem(key, row, disposition, changed, !exists, ack));
            }
            // A saved acknowledgement may not silently refer to a different snapshot or lost record.
            var itemMap = items.ToDictionary(x => x.Key, x => x.Disposition, StringComparer.Ordinal);
            if (acknowledged != null && acknowledged.Count != 0 && (acknowledgedHash != latestHash
                || acknowledged.Any(x => !itemMap.TryGetValue(x.Key, out var disposition) || disposition != x.Value))) return Bad("CutoverDecisionBindingChanged");
            var oldClaims = (originalClaims ?? Array.Empty<LegacyCashClaim>()).ToArray();
            var newClaims = (latestClaims ?? Array.Empty<LegacyCashClaim>()).ToArray();
            var allocations = entries.Where(x => x.Intent.Kind == SettlementKind.LegacyAllocation && x.Phase != SettlementPhase.Cancelled).ToArray();
            if (allocations.Any(x => x.Phase != SettlementPhase.Finalized)) return Bad("UnfinishedLegacyAllocation");
            string ClaimIdentity(LegacyCashClaim claim) => claim.Kind + ":" + claim.RecordId.ToString(CultureInfo.InvariantCulture) + ":" + claim.LegacyParty;
            var originalByIdentity = oldClaims.GroupBy(ClaimIdentity).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
            var fundedByClaim = allocations.GroupBy(x => x.Intent.ClaimId).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
            var consumed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var claim in newClaims)
            {
                originalByIdentity.TryGetValue(ClaimIdentity(claim), out var priorClaims);
                var related = (priorClaims ?? Array.Empty<LegacyCashClaim>()).Select(x => x.Id).Concat(new[] { claim.Id }).Distinct(StringComparer.Ordinal);
                var funded = related.SelectMany(x => fundedByClaim.TryGetValue(x, out var matches) ? matches : Array.Empty<SettlementEntry>()).ToArray();
                if (funded.Any(x => x.Intent.TargetAccount != claim.LegacyParty) || claim.LegacyParty == SettlementLedgerNames.Treasury)
                    return Bad("LegacyAllocationPartyConflict");
                long amount = funded.Sum(x => x.Intent.AmountMinor);
                if (amount > claim.AmountMinor) return Bad("LegacyAllocationExceedsFinalClaim");
                foreach (var entry in funded) if (!consumed.Add(entry.Intent.CommandId)) return Bad("LegacyAllocationIdentityConflict");
                cash.Add(new LegacyCutoverCash(claim, amount));
            }
            if (consumed.Count != allocations.Length) return Bad("LegacyAllocationMissingFinalClaim");
            var ownership = after.Values.Where(x => x.Kind == "EconomyOwnership").ToArray();
            if (ownership.Any(x => string.IsNullOrWhiteSpace(x.Fields["AssetKey"]) || string.IsNullOrWhiteSpace(x.Fields["OwnerKey"]))
                || ownership.GroupBy(x => x.Fields["AssetKey"]).Any(x => x.Count() != 1)) return Bad("ConflictingAssetOwnership");
            var owners = ownership.ToDictionary(x => x.Fields["AssetKey"], x => x.Fields["OwnerKey"], StringComparer.Ordinal);
            // Paid, unused assets must retain an explicit owner; never acknowledge a lost title as history.
            foreach (var row in after.Values.Where(x => x.Kind == "EconomyPerson" && !string.IsNullOrEmpty(x.Fields["AcquiredAsset"])))
                if (!owners.TryGetValue(row.Fields["AcquiredAsset"], out string owner) || owner != row.Fields["PawnKey"]) return Bad("AcquiredAssetOwnershipMissing");
            return new LegacyCutoverPlan(items, cash, null);
        }
    }
}

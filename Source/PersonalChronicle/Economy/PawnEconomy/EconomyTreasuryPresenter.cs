using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        private void BuildTreasuryRows(Building_EconomyInstitution selected, List<EconomyRow> rows)
        {
            var audit = settlement.ReadAudit(); var treasury = settlement.ReadAccount(SettlementLedgerNames.Treasury);
            rows.Add(new EconomyRow(T("Treasury.Title")));
            rows.Add(new EconomyRow(TF("Public.Total", QueryPublicFundsTotal().HasValue ? Money(QueryPublicFundsTotal().Value) : T("Treasury.Unknown"))));
            rows.Add(new EconomyRow(TF("Treasury.Funds", Money(treasury?.BalanceMinor ?? 0), Money(treasury?.ReservedMinor ?? 0),
                audit.ActualReserveMinor.HasValue ? Money(audit.ActualReserveMinor.Value) : T("Treasury.Unknown"))));
            rows.Add(Hint(T("Treasury.Hint")));
            rows.Add(new EconomyRow(TF("Public.PendingSilver", QueryPendingPublicSilver(selected?.Map))));
            rows.Add(Hint(T("Public.OwnershipHint")));
            var liveInstitutions = anchors.Where(a => a.Spawned && a.Map == selected?.Map).ToList();
            foreach (var institution in liveInstitutions)
            {
                string accountKey = InstitutionFundingKey(institution);
                long balance = settlement.ReadAccount(accountKey)?.AvailableMinor ?? 0;
                rows.Add(new EconomyRow(TF("Public.Budget", institution.LabelCap.ToString(), Money(balance))));
                foreach (int amount in new[] { 25, 100 })
                    rows.Add(new EconomyRow(TF("Public.AllocateText", amount), T("Public.Allocate"), "Treasury.AllocateBudget", amount, institution.GetUniqueLoadID()));
            }
            // Account ownership survives demolition. Unused budget is always returnable to the colony.
            foreach (var account in audit.Accounts.Where(a => SettlementLedgerNames.IsPublic(a.Id) && a.Id != SettlementLedgerNames.Treasury && a.AvailableMinor > 0))
                rows.Add(new EconomyRow(TF("Public.ReturnText", PublicAccountName(account.Id), Money(account.AvailableMinor)),
                    T("Public.Return"), "Treasury.ReturnBudget", key: account.Id));
            var pending = treasuryOperations.Pending;
            if (pending != null)
                rows.Add(new EconomyRow(TF("Treasury.Pending", pending.Id, T("Treasury.Kind." + pending.Kind), pending.Count, PawnName(pending.PawnKey)),
                    T("Treasury.Cancel"), "Treasury.Cancel", pending.Id));
            else if (PublicBank(selected))
            {
                foreach (int count in new[] { 25, 100 })
                {
                    rows.Add(new EconomyRow(TF("Treasury.DepositText", count), T("Treasury.Deposit"), "Treasury.Deposit", count, selected.GetUniqueLoadID()));
                    rows.Add(new EconomyRow(TF("Treasury.WithdrawText", count), T("Treasury.Withdraw"), "Treasury.Withdraw", count, selected.GetUniqueLoadID()));
                }
            }
            // F4：未选公有银行时**不再**追加引导行 —— 顶部已有同义引导（经济窗口的 "Select an institution"
            // 焦点提示 + 机构页选柜台），同一句话不重复第二遍。
            foreach (var delivery in treasuryOperations.ReadOrders().Reverse().Take(5))
                rows.Add(new EconomyRow(TF("Treasury.DeliveryRecord", delivery.Id, T("Treasury.Kind." + delivery.Kind), delivery.Count,
                    T("Treasury.Status." + delivery.Phase))));
            foreach (var account in audit.Accounts.Where(x => !SettlementLedgerNames.IsPublic(x.Id)).Take(64))
                rows.Add(new EconomyRow(TF("Treasury.Account", PawnName(account.Id), Money(account.BalanceMinor))));
            var plan = ReadCutoverPlan();
            if (plan == null) { rows.Add(Hint(T("Treasury.FreezeFirst"))); return; }
            rows.Add(new EconomyRow(TF("Treasury.CutoverSummary", plan.Items.Count(x => !x.Acknowledged),
                (plan.Cash.Sum(x => (decimal)x.Remaining) / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))));
            if (plan.Fault != null || treasuryOperations.Fault != null)
            { rows.Add(Hint(T("Treasury.Conflict"))); return; }
            foreach (var claim in plan.Cash.Where(x => x.Remaining > 0).Take(64))
                rows.Add(new EconomyRow(T("Treasury.ClaimKind." + claim.Claim.Kind) + " · " + TF("Treasury.Claim", claim.Claim.RecordId, PawnName(claim.Claim.LegacyParty), Money(claim.Remaining)),
                    T("Treasury.Fund"), "Treasury.FundClaim", key: claim.Claim.Id));
            foreach (var item in plan.Items.Where(x => !x.Acknowledged).Take(64))
                rows.Add(new EconomyRow("#" + item.Record.Id + " · " + T("Treasury.Record." + item.Record.Kind) + " · "
                    + T("Treasury.Disposition." + item.Disposition) + (item.OriginalOnly ? " · " + T("Treasury.OriginalOnly") : item.Changed ? " · " + T("Treasury.Changed") : ""),
                    T("Treasury.Acknowledge"), "Treasury.Acknowledge", key: item.Key));
            if (plan.Complete) rows.Add(new EconomyRow(T("Treasury.CutoverComplete")));
        }
        private string PublicAccountName(string key)
        {
            if (key == SettlementLedgerNames.Treasury) return T("Public.Unallocated");
            if (key == "public" || key?.StartsWith("institution:", System.StringComparison.Ordinal) == true) return T("Public.LegacyFunds") + " · " + key;
            return anchors.FirstOrDefault(a => InstitutionFundingKey(a) == key)?.LabelCap.ToString()
                ?? T("Public.FormerInstitution") + " · " + key;
        }
    }
}

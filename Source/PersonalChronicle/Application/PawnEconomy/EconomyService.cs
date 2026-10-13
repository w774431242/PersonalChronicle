using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Domain.PawnEconomy;

namespace PersonalChronicle.Application.PawnEconomy
{
    /// <summary>Engine-independent commands. The game adapter validates physical facts before commit.</summary>
    public sealed class EconomyService
    {
        private readonly EconomyState state;
        public EconomyService(EconomyState state) { this.state = state ?? throw new ArgumentNullException(nameof(state)); }
        public EconomyAccount Account(string key)
        {
            var result = state.All<EconomyAccount>().FirstOrDefault(a => a.Key == key);
            return result ?? state.Add(new EconomyAccount { Key = key });
        }
        public EconomyPerson Person(string key)
        {
            var result = state.All<EconomyPerson>().FirstOrDefault(p => p.PawnKey == key);
            return result ?? state.Add(new EconomyPerson { PawnKey = key });
        }
        public bool Enable(bool enabled)
        {
            if (!state.Ready) return false;
            if (enabled && !state.Funded)
            {
                Account(EconomyState.PublicAccount).Balance = EconomyRules.OpeningBudget;
                state.Funded = true;
                Receipt("opening", "OpeningAllocation", EconomyState.PublicAccount, null, EconomyState.PublicAccount,
                    EconomyRules.OpeningBudget, 0, true);
            }
            state.Enabled = enabled;
            if (!enabled) foreach (var order in state.All<EconomyOrder>().Where(o => o.Pending).ToList()) Cancel(order, "Disabled");
            state.Revision++; return true;
        }
        public EconomyOrder Prepare(EconomyOrderKind kind, string client, EconomyInstitution institution,
            string payer, long amount, int now, string asset = null, long position = 0, string provider = null)
        {
            if (!state.Ready || !state.Enabled || institution == null || !state.Records.Contains(institution) || !institution.Open || amount < 0
                || string.IsNullOrEmpty(client) || string.IsNullOrEmpty(payer) || client == provider
                || !Enum.IsDefined(typeof(EconomyOrderKind), kind)
                || amount > EconomyRules.MaxAmount || state.All<EconomyOrder>().Count(o => o.Pending) >= EconomyRules.MaxPendingOrders
                || state.All<EconomyOrder>().Any(o => o.Pending && (o.ClientKey == client || o.ProviderKey == client
                    || (!string.IsNullOrEmpty(provider) && (o.ProviderKey == provider || o.ClientKey == provider))
                    || (!string.IsNullOrEmpty(asset) && o.AssetKey == asset)))) return null;
            if (kind == EconomyOrderKind.Examination && state.All<EconomyOrder>().Any(o => o.Pending && o.InstitutionId == institution.Id)) return null;
            var source = Account(payer);
            bool held = amount > 0 && payer != institution.AccountKey;
            if (held && !EconomyRules.Reserve(source, amount)) return null;
            var order = state.Add(new EconomyOrder {
                OrderKind = kind, ClientKey = client, ProviderKey = provider, InstitutionId = institution.Id,
                AssetKey = asset, PositionId = position, PayerKey = payer, SellerKey = institution.AccountKey,
                Amount = amount, FundsHeld = held, MapId = institution.MapId, ExpiresTick = now + EconomyRules.OrderLifetime
            });
            Person(client).Goal = EconomicGoalStatus.Executing;
            return order;
        }
        public bool Cancel(EconomyOrder order, string reason)
        {
            if (order == null || !order.Pending) return false;
            if (order.FundsHeld)
            {
                var account = Account(order.PayerKey);
                if (account.Reserved < order.Amount) { state.Fault = "ReservationMismatch"; return false; }
                account.Reserved -= order.Amount; order.FundsHeld = false;
            }
            order.Status = EconomyOrderStatus.Cancelled; order.Reason = reason;
            var person = Person(order.ClientKey); person.Goal = EconomicGoalStatus.Blocked; person.Reason = reason;
            state.Revision++; return true;
        }
        public bool CanCommit(EconomyOrder order)
        {
            if (order == null || !state.Ready || !state.Enabled || !order.Pending || !state.Records.Contains(order)) return false;
            if (order.Amount == 0 || order.PayerKey == order.SellerKey) return true;
            return order.FundsHeld && EconomyRules.CanTransfer(Account(order.PayerKey), Account(order.SellerKey), order.Amount, true);
        }
        public bool Commit(EconomyOrder order, int tick)
        {
            if (order?.Status == EconomyOrderStatus.Committed) return true;
            if (!CanCommit(order) || tick >= order.ExpiresTick) return false;
            if (order.FundsHeld && !EconomyRules.Transfer(Account(order.PayerKey), Account(order.SellerKey), order.Amount, true)) return false;
            order.FundsHeld = false; order.Status = EconomyOrderStatus.Committed;
            Receipt("order:" + order.Id, order.OrderKind.ToString(), order.ClientKey, order.PayerKey, order.SellerKey, order.Amount, tick, true);
            Person(order.ClientKey).Reason = null;
            state.Revision++; return true;
        }
        public bool HasVacancy(EconomyPosition position)
        {
            return position != null && position.Open && position.Capacity > 0 && position.Wage > 0
                && state.All<EconomyContract>().Count(c => c.Active && c.PositionId == position.Id)
                + state.All<EconomyOrder>().Count(o => o.Pending && o.OrderKind == EconomyOrderKind.Employment && o.PositionId == position.Id) < position.Capacity;
        }
        public EconomyContract Sign(EconomyOrder order, int now)
        {
            if (order == null || order.OrderKind != EconomyOrderKind.Employment || !CanCommit(order)
                || state.All<EconomyContract>().Any(c => c.Active && c.PawnKey == order.ClientKey)) return null;
            var position = state.Find<EconomyPosition>(order.PositionId);
            if (position == null || !position.Open || state.All<EconomyContract>().Count(c => c.Active && c.PositionId == position.Id) >= position.Capacity) return null;
            var contract = state.Add(new EconomyContract { PawnKey = order.ClientKey, PositionId = position.Id });
            if (!Commit(order, now)) { state.Records.Remove(contract); return null; }
            Person(order.ClientKey).Goal = EconomicGoalStatus.WaitingIncome; return contract;
        }
        public EconomyReceipt CompleteProduction(EconomyContract contract, int jobId, int tick)
        {
            if (!state.Ready || !state.Enabled || contract == null || !state.Records.Contains(contract) || !contract.Active || jobId < 0 || jobId <= contract.LastProductionJobId) return null;
            var position = state.Find<EconomyPosition>(contract.PositionId);
            if (position == null || position.Wage <= 0 || position.Wage > EconomyRules.MaxAmount
                || state.All<EconomyReceipt>().Any(r => !r.Paid && r.Type == "Wage" && r.SubjectKey == contract.PawnKey)) return null;
            // One real DoBill job produces one standard iteration. The watermark survives journal trimming.
            contract.LastProductionJobId = jobId;
            bool paid = EconomyRules.Transfer(Account(EconomyState.PublicAccount), Account(contract.PawnKey), position.Wage);
            var receipt = Receipt("production:" + contract.Id + ":" + jobId, "Wage", contract.PawnKey,
                EconomyState.PublicAccount, contract.PawnKey, position.Wage, tick, paid);
            if (!paid) position.Open = false; // existing entitlement remains payable; no new hiring promises.
            Person(contract.PawnKey).NextDecisionTick = tick; state.Revision++; return receipt;
        }
        public int SettlePayables()
        {
            int count = 0;
            foreach (var receipt in state.All<EconomyReceipt>().ToList())
                if (!receipt.Paid && receipt.Type == "Wage" && EconomyRules.Transfer(Account(receipt.FromKey), Account(receipt.ToKey), receipt.Amount))
                { receipt.Paid = true; count++; }
            if (count > 0) state.Revision++; return count;
        }
        public EconomyReceipt Receipt(string key, string type, string subject, string from, string to, long amount, int tick, bool paid)
        {
            var prior = state.All<EconomyReceipt>().FirstOrDefault(r => r.Key == key);
            return prior ?? state.Add(new EconomyReceipt { Key = key, Type = type, SubjectKey = subject,
                FromKey = from, ToKey = to, Amount = amount, Tick = tick, Paid = paid });
        }
        public bool PayDoctor(EconomyOrder order, int tick)
        {
            if (order == null || !state.Records.Contains(order) || order.Status != EconomyOrderStatus.Committed || string.IsNullOrEmpty(order.ProviderKey)) return false;
            if (order.ProviderPaid) return true;
            string key = "doctor:" + order.Id;
            if (state.All<EconomyReceipt>().Any(r => r.Key == key)) { order.ProviderPaid = true; return true; }
            long wage = order.Amount * 3 / 5;
            if (wage == 0 || order.PayerKey == order.SellerKey) { order.ProviderPaid = true; return true; }
            if (!EconomyRules.Transfer(Account(order.SellerKey), Account(order.ProviderKey), wage)) return false;
            order.ProviderPaid = true;
            Receipt(key, "ServiceWage", order.ProviderKey, order.SellerKey, order.ProviderKey, wage, tick, true); return true;
        }
        public void RecoverAfterLoad()
        {
            if (state.Schema > EconomyState.CurrentSchema) { state.Fault = "UnsupportedSchema"; return; }
            if (!state.Ready && state.Fault != null) return;
            if (state.Schema < 0) { state.Fault = "InvalidSchema"; return; }
            state.Schema = EconomyState.CurrentSchema;
            if (state.Records.Any(r => r == null) || state.Records.GroupBy(r => r.Id).Any(g => g.Key <= 0 || g.Count() > 1)
                || state.All<EconomyAccount>().GroupBy(a => a.Key).Any(g => string.IsNullOrEmpty(g.Key) || g.Count() > 1)
                || state.All<EconomyAccount>().Any(a => !EconomyRules.Valid(a))
                || state.All<EconomyPerson>().GroupBy(p => p.PawnKey).Any(g => string.IsNullOrEmpty(g.Key) || g.Count() > 1)
                || state.All<EconomyOrder>().Any(o => o.Amount < 0 || o.Amount > EconomyRules.MaxAmount
                    || !Enum.IsDefined(typeof(EconomyOrderKind), o.OrderKind) || !Enum.IsDefined(typeof(EconomyOrderStatus), o.Status)
                    || string.IsNullOrEmpty(o.ClientKey) || string.IsNullOrEmpty(o.PayerKey) || string.IsNullOrEmpty(o.SellerKey)
                    || o.FundsHeld && (!o.Pending || o.Amount == 0 || o.PayerKey == o.SellerKey))
                || state.All<EconomyReceipt>().Any(r => r.Amount < 0 || r.Amount > EconomyRules.MaxAmount)
                || state.All<EconomyInstitution>().Any(i => !Enum.IsDefined(typeof(InstitutionKind), i.InstitutionKind)
                    || i.StartHour < 0 || i.StartHour > 23 || i.EndHour < 0 || i.EndHour > 23))
            { state.Fault = "InvalidLedger"; return; }
            foreach (var account in state.All<EconomyAccount>())
                if (account.Reserved != state.All<EconomyOrder>().Where(o => o.Pending && o.FundsHeld && o.PayerKey == account.Key).Sum(o => o.Amount))
                { state.Fault = "ReservationMismatch"; return; }
            if (state.All<EconomyOrder>().Any(o => o.FundsHeld && !state.All<EconomyAccount>().Any(a => a.Key == o.PayerKey)))
            { state.Fault = "ReservationMismatch"; return; }
            state.Sequence = Math.Max(state.Sequence, state.Records.Count == 0 ? 0 : state.Records.Max(r => r.Id));
            foreach (var order in state.All<EconomyOrder>().Where(o => o.Pending).ToList()) Cancel(order, "Reloaded");
            if (state.All<EconomyAccount>().Any(a => a.Reserved != 0)) state.Fault = "ReservationMismatch";
        }
        public void TrimHistory()
        {
            var remove = state.All<EconomyOrder>().Where(o => !o.Pending).OrderByDescending(o => o.Id).Skip(EconomyRules.RetainedOrders).Cast<EconomyRecord>().ToList();
            remove.AddRange(state.All<EconomyReceipt>().Where(r => r.Paid && r.Key != "opening").OrderByDescending(r => r.Id).Skip(EconomyRules.RetainedReceipts));
            remove.AddRange(state.All<EconomyReport>().GroupBy(r => r.PawnKey).SelectMany(g => g.OrderByDescending(r => r.Id).Skip(2)));
            remove.AddRange(state.All<EconomyContract>().Where(c => !c.Active).OrderByDescending(c => c.Id).Skip(512));
            foreach (var record in remove) state.Records.Remove(record);
        }
    }
}

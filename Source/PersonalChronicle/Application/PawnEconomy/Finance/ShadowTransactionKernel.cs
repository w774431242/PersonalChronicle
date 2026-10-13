using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PersonalChronicle.Domain.PawnEconomy.Finance;

namespace PersonalChronicle.Application.PawnEconomy.Finance
{
    /// <summary>
    /// Pure, session-local transaction simulation. Supplied balances are NOT deposit evidence.
    /// No persistence, money issuance, delivery validation or game payment route exists here.
    /// </summary>
    public sealed class ShadowTransactionKernel
    {
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private readonly int capacity;
        private Dictionary<string, FinancialAccountSnapshot> accounts;
        private readonly Dictionary<string, FinancialTransactionSnapshot> transactions = new Dictionary<string, FinancialTransactionSnapshot>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> resources = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> evidenceOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> creditHeld = new Dictionary<string, long>(StringComparer.Ordinal);
        private long version, lastTick;
        public bool BackingVerified => false;
        public bool CanExecuteInGame => false;
        public long Version { get { AssertThread(); return version; } }
        public ShadowTransactionKernel(IEnumerable<FinancialAccountSnapshot> shadowAccounts, int capacity = 4096)
        {
            if (capacity < 1 || capacity > 100000) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
            var input = (shadowAccounts ?? throw new ArgumentNullException(nameof(shadowAccounts))).Take(4097).ToArray();
            if (input.Length > 4096 || input.Any(a => a == null)) throw new ArgumentException("InvalidAccounts");
            accounts = input.ToDictionary(a => a.Id, StringComparer.Ordinal);
            foreach (var key in accounts.Keys) creditHeld.Add(key, 0);
        }
        private void AssertThread()
        { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("WrongThread"); }
        public IReadOnlyList<FinancialAccountSnapshot> ReadAccounts()
        { AssertThread(); return Array.AsReadOnly(accounts.Values.OrderBy(a => a.Id, StringComparer.Ordinal).ToArray()); }
        public FinancialTransactionSnapshot ReadTransaction(string commandId)
        { AssertThread(); return transactions.TryGetValue(commandId, out var value) ? value : null; }
        public bool IsResourceHeld(string key) { AssertThread(); return resources.ContainsKey(key); }
        private FinancialResult Result(FinancialResultCode code, string id = null)
            => new FinancialResult(code, version, id != null && transactions.TryGetValue(id, out var value) ? value : null);
        // Time does not release holds implicitly. Tick/commit/cancel process expiry explicitly.
        private bool AdvanceClock(long now)
        { if (now < 0 || now < lastTick) return false; lastTick = now; return true; }
        private static Dictionary<string, long> Totals(FinancialIntent intent, bool debit)
        {
            var totals = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var leg in intent.Legs)
            {
                string key = debit ? leg.DebitAccount : leg.CreditAccount;
                totals.TryGetValue(key, out long previous);
                totals[key] = checked(previous + leg.AmountMinor);
            }
            return totals;
        }
        public FinancialResult Prepare(FinancialIntent intent, long now)
        {
            AssertThread(); if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (!AdvanceClock(now)) return Result(FinancialResultCode.ClockReversed);
            if (transactions.TryGetValue(intent.CommandId, out var previous))
            {
                if (!previous.Intent.SameAs(intent)) return Result(FinancialResultCode.CommandConflict, intent.CommandId);
                if (previous.Phase == FinancialPhase.Prepared && now >= previous.Intent.ExpiresTick)
                    Finish(previous, FinancialPhase.Expired);
                return Result(FinancialResultCode.Replay, intent.CommandId);
            }
            if (intent.ExpectedVersion != version) return Result(FinancialResultCode.StaleVersion);
            if (intent.ExpiresTick <= now) return Result(FinancialResultCode.Expired);
            if (transactions.Count >= capacity) return Result(FinancialResultCode.CapacityReached);
            var debit = Totals(intent, true); var credit = Totals(intent, false);
            foreach (var key in debit.Keys.Concat(credit.Keys).Distinct(StringComparer.Ordinal))
            {
                if (!accounts.TryGetValue(key, out var account)) return Result(FinancialResultCode.MissingAccount);
                debit.TryGetValue(key, out long outgoing); credit.TryGetValue(key, out long incoming);
                if (outgoing > account.AvailableMinor) return Result(FinancialResultCode.InsufficientFunds);
                // Reserve credit headroom against ALL prepared bundles, not only this intent.
                if (incoming > MinorCurrency.Maximum - account.BalanceMinor - creditHeld[key])
                    return Result(FinancialResultCode.AmountLimit);
            }
            if (intent.ResourceKeys.Any(resources.ContainsKey)) return Result(FinancialResultCode.ResourceBusy);
            var replacement = new Dictionary<string, FinancialAccountSnapshot>(accounts, StringComparer.Ordinal);
            foreach (var pair in debit)
            {
                var account = accounts[pair.Key];
                replacement[pair.Key] = new FinancialAccountSnapshot(pair.Key, account.BalanceMinor, checked(account.ReservedMinor + pair.Value));
            }
            // No callbacks/re-entry or partial externally observable state within this owner-thread operation.
            accounts = replacement;
            foreach (var pair in credit) creditHeld[pair.Key] = checked(creditHeld[pair.Key] + pair.Value);
            transactions.Add(intent.CommandId, new FinancialTransactionSnapshot(intent, FinancialPhase.Prepared));
            foreach (string key in intent.ResourceKeys) resources.Add(key, intent.CommandId);
            version = checked(version + 1);
            return Result(FinancialResultCode.Applied, intent.CommandId);
        }
        public FinancialResult Commit(string commandId, long expectedVersion, long now, string evaluationEvidenceId)
        {
            AssertThread();
            if (!AdvanceClock(now)) return Result(FinancialResultCode.ClockReversed);
            if (!transactions.TryGetValue(commandId, out var tx)) return Result(FinancialResultCode.UnknownCommand);
            if (tx.Phase == FinancialPhase.Committed)
                return Result(tx.EvidenceId == evaluationEvidenceId ? FinancialResultCode.Replay : FinancialResultCode.EvidenceConflict, commandId);
            if (tx.Phase != FinancialPhase.Prepared) return Result(FinancialResultCode.InvalidPhase, commandId);
            if (expectedVersion != version) return Result(FinancialResultCode.StaleVersion, commandId);
            if (now >= tx.Intent.ExpiresTick)
            { Finish(tx, FinancialPhase.Expired); return Result(FinancialResultCode.Expired, commandId); }
            if (string.IsNullOrWhiteSpace(evaluationEvidenceId) || evaluationEvidenceId.Length > 256)
                return Result(FinancialResultCode.MissingEvidence, commandId);
            if (evidenceOwners.ContainsKey(evaluationEvidenceId)) return Result(FinancialResultCode.EvidenceConflict, commandId);
            Finish(tx, FinancialPhase.Committed, evaluationEvidenceId);
            evidenceOwners.Add(evaluationEvidenceId, commandId);
            return Result(FinancialResultCode.Applied, commandId);
        }
        public FinancialResult Cancel(string commandId, long expectedVersion, long now)
        {
            AssertThread();
            if (!AdvanceClock(now)) return Result(FinancialResultCode.ClockReversed);
            if (!transactions.TryGetValue(commandId, out var tx)) return Result(FinancialResultCode.UnknownCommand);
            if (tx.Phase == FinancialPhase.Cancelled || tx.Phase == FinancialPhase.Expired) return Result(FinancialResultCode.Replay, commandId);
            if (tx.Phase != FinancialPhase.Prepared) return Result(FinancialResultCode.InvalidPhase, commandId);
            if (expectedVersion != version) return Result(FinancialResultCode.StaleVersion, commandId);
            Finish(tx, now >= tx.Intent.ExpiresTick ? FinancialPhase.Expired : FinancialPhase.Cancelled);
            return Result(FinancialResultCode.Applied, commandId);
        }
        public FinancialResult Expire(long now)
        {
            AssertThread();
            if (!AdvanceClock(now)) return Result(FinancialResultCode.ClockReversed);
            foreach (var tx in transactions.Values.Where(t => t.Phase == FinancialPhase.Prepared && t.Intent.ExpiresTick <= now)
                .OrderBy(t => t.Intent.CommandId, StringComparer.Ordinal).ToArray()) Finish(tx, FinancialPhase.Expired);
            return Result(FinancialResultCode.Applied);
        }
        private void Finish(FinancialTransactionSnapshot tx, FinancialPhase phase, string evidence = null)
        {
            var debit = Totals(tx.Intent, true); var credit = Totals(tx.Intent, false);
            var replacement = new Dictionary<string, FinancialAccountSnapshot>(accounts, StringComparer.Ordinal);
            foreach (string key in debit.Keys.Concat(credit.Keys).Distinct(StringComparer.Ordinal))
            {
                var account = accounts[key]; debit.TryGetValue(key, out long outgoing); credit.TryGetValue(key, out long incoming);
                long balance = phase == FinancialPhase.Committed ? checked(account.BalanceMinor - outgoing + incoming) : account.BalanceMinor;
                replacement[key] = new FinancialAccountSnapshot(key, balance, checked(account.ReservedMinor - outgoing));
            }
            accounts = replacement;
            foreach (var pair in credit) creditHeld[pair.Key] = checked(creditHeld[pair.Key] - pair.Value);
            foreach (string key in tx.Intent.ResourceKeys) resources.Remove(key);
            transactions[tx.Intent.CommandId] = new FinancialTransactionSnapshot(tx.Intent, phase, evidence);
            version = checked(version + 1);
        }
    }
}

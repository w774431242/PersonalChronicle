using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PersonalChronicle.Domain.PawnEconomy.Finance;

namespace PersonalChronicle.Application.PawnEconomy.Finance
{
    /// <summary>Zero-seeded settlement ledger. Physical proof is accepted only by the reserve adapter.
    /// Restored terminal records remain untrusted until the host binds every physical journal entry.</summary>
    internal sealed class FundedSettlementLedger
    {
        public const int MaximumEntries = 4096;
        /// <summary>零余额账户登记的硬上限（与 <see cref="MaximumEntries"/> 同量级；远超单局殖民地可达的 Pawn 数）。
        /// 到达上限后 <see cref="EnrollAccount"/> 明确拒绝 —— 拒绝可被观察，静默丢弃不可。</summary>
        public const int MaximumEnrolledAccounts = 1024;
        private int ownerThread = Thread.CurrentThread.ManagedThreadId;
        internal void BindOwnerAtHostBoundary() { ownerThread = Thread.CurrentThread.ManagedThreadId; }
        private readonly List<SettlementEntry> entries = new List<SettlementEntry>();
        private readonly Dictionary<string, SettlementEntry> index = new Dictionary<string, SettlementEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> balances = new Dictionary<string, long>(StringComparer.Ordinal) { [SettlementLedgerNames.Treasury] = 0 };
        // FA-01：零余额账户（设计 §4「新 Pawn 账户由明确 EnrollPawn 命令建立，初始零余额；观察查询仅 TryRead，不创建」）。
        // 它们不占用 entries 容量、不进 Version、不改 TotalMinor —— 只把"该账户确实存在且余额为零"变成可证明的事实。
        private readonly HashSet<string> enrolled = new HashSet<string>(StringComparer.Ordinal);
        public long Version => entries.Count;
        public long TotalMinor => balances.Values.Sum();
        public IReadOnlyList<SettlementEntry> ReadEntries() { AssertThread(); return Array.AsReadOnly(entries.ToArray()); }
        public SettlementEntry Read(string id) { AssertThread(); return id != null && index.TryGetValue(id, out var row) ? row : null; }
        private void AssertThread() { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("WrongSettlementThread"); }
        private static string DebitAccount(SettlementIntent intent) => intent.Kind == SettlementKind.Deposit ? null
            : intent.PayerAccount ?? SettlementLedgerNames.Treasury;
        private long Held(string id) => entries.Where(x => (x.Phase == SettlementPhase.Prepared || x.Phase == SettlementPhase.ReconciliationRequired)
            && DebitAccount(x.Intent) == id).Sum(x => x.Intent.AmountMinor);
        /// <summary>FA-03：是否存在**在途/待对账**结算行（正是 <see cref="Prepare"/> 会拒绝新建事务的那一类）。
        /// 直接扫描内部列表、**不复制数组** —— 本判据由制度观察每 250 tick 调用，不能走每次 ToArray 的读路径。</summary>
        public bool HasUnresolvedRows()
        {
            AssertThread();
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Phase == SettlementPhase.Prepared || entries[i].Phase == SettlementPhase.ReconciliationRequired) return true;
            return false;
        }
        public FinancialAccountSnapshot ReadAccount(string id)
        {
            AssertThread(); return id != null && balances.TryGetValue(id, out long amount)
                ? new FinancialAccountSnapshot(id, amount, Held(id)) : null;
        }
        public IReadOnlyList<FinancialAccountSnapshot> ReadAccounts()
        { AssertThread(); return Array.AsReadOnly(balances.Keys.OrderBy(x => x, StringComparer.Ordinal).Select(ReadAccount).ToArray()); }
        /// <summary>
        /// FA-01（设计 §4）：给一个**合法新主体**建立初始零余额账户。这是一条显式命令，
        /// 不是查询副作用 —— 观察/规划路径只读，永不调用它（<c>ReadInput</c> 仍走 <c>TryGetValue</c>）。
        /// 已有账户（无论余额）一律不动并返回 false：命令幂等，重放不会抹掉余额。
        /// </summary>
        public bool EnrollAccount(string id)
        {
            AssertThread();
            if (string.IsNullOrEmpty(id) || balances.ContainsKey(id)
                || enrolled.Count >= MaximumEnrolledAccounts) return false;
            balances[id] = 0; enrolled.Add(id); return true;
        }
        /// <summary>本账本中由 Enroll 建立（而非由资金条目派生）的账户 id，稳定序 —— 供编解码往返。</summary>
        internal IReadOnlyList<string> ReadEnrolledAccounts()
        { AssertThread(); return Array.AsReadOnly(enrolled.OrderBy(x => x, StringComparer.Ordinal).ToArray()); }
        public SettlementResult Prepare(SettlementIntent intent)
        {
            AssertThread(); if (intent == null) throw new ArgumentNullException(nameof(intent));
            var old = Read(intent.CommandId);
            if (old != null) return new SettlementResult(old.Intent.SameAs(intent) ? SettlementCode.Replay : SettlementCode.CommandConflict, old);
            if (entries.Any(x => x.Phase == SettlementPhase.Prepared || x.Phase == SettlementPhase.ReconciliationRequired)) return new SettlementResult(SettlementCode.NotReady);
            if (intent.ExpectedVersion != Version) return new SettlementResult(SettlementCode.StaleVersion);
            if (entries.Count >= MaximumEntries) return new SettlementResult(SettlementCode.CapacityReached);
            if (intent.Kind == SettlementKind.Deposit)
            { if (TotalMinor > MinorCurrency.Maximum - intent.AmountMinor) return new SettlementResult(SettlementCode.CapacityReached); }
            else if (intent.Kind == SettlementKind.Purchase)
            {
                // A purchase is funded by the buyer, not by the treasury: the payer must hold the money now.
                if ((ReadAccount(intent.PayerAccount)?.AvailableMinor ?? 0) < intent.AmountMinor) return new SettlementResult(SettlementCode.InsufficientFunds);
            }
            else if ((ReadAccount(DebitAccount(intent))?.AvailableMinor ?? 0) < intent.AmountMinor) return new SettlementResult(SettlementCode.InsufficientFunds);
            var row = new SettlementEntry(intent, SettlementPhase.Prepared); entries.Add(row); index.Add(intent.CommandId, row);
            return new SettlementResult(SettlementCode.Prepared, row);
        }
        private SettlementResult Set(SettlementEntry old, SettlementPhase phase, string reason = null)
        {
            var next = new SettlementEntry(old.Intent, phase, reason);
            entries[(int)old.Intent.ExpectedVersion] = next; index[old.Intent.CommandId] = next;
            return new SettlementResult(phase == SettlementPhase.Finalized ? SettlementCode.Finalized : phase == SettlementPhase.Cancelled
                ? SettlementCode.Cancelled : SettlementCode.ReconciliationRequired, next);
        }
        private void Apply(SettlementIntent intent)
        {
            long treasury = balances[SettlementLedgerNames.Treasury];
            if (intent.Kind == SettlementKind.Deposit) balances[SettlementLedgerNames.Treasury] = checked(treasury + intent.AmountMinor);
            else if (intent.Kind == SettlementKind.Purchase || intent.Kind == SettlementKind.PublicTransfer)
            {
                balances.TryGetValue(intent.PayerAccount ?? "", out long payer);
                balances[intent.PayerAccount] = checked(payer - intent.AmountMinor);
                balances.TryGetValue(intent.TargetAccount, out long seller);
                balances[intent.TargetAccount] = checked(seller + intent.AmountMinor);
            }
            else
            {
                string payer = DebitAccount(intent);
                balances[payer] = checked(balances[payer] - intent.AmountMinor);
                if (intent.Kind == SettlementKind.LegacyAllocation || intent.Kind == SettlementKind.WageTransfer)
                { balances.TryGetValue(intent.TargetAccount, out long before); balances[intent.TargetAccount] = checked(before + intent.AmountMinor); }
            }
        }
        public SettlementResult CompletePhysical(string id, ReserveMovementEvidence proof)
        {
            AssertThread(); var row = Read(id);
            if (row == null || row.Intent.Movement == null) return new SettlementResult(SettlementCode.NotReady, row);
            if (row.Phase != SettlementPhase.Prepared) return new SettlementResult(SettlementCode.Replay, row);
            if (proof == null || !row.Intent.Movement.SameAs(proof.Intent) || proof.Phase != ReserveMovementPhase.Completed
                || !proof.HasExactQuantityProof || proof.CompletedMinor != row.Intent.AmountMinor || proof.Error != null
                || string.IsNullOrWhiteSpace(proof.ResultThingId)) return RequireReconciliation(id, "MissingOrConflictingPhysicalProof");
            Apply(row.Intent); return Set(row, SettlementPhase.Finalized);
        }
        public SettlementResult CompleteAllocation(string id)
        {
            AssertThread(); var row = Read(id);
            if (row == null || row.Intent.Kind != SettlementKind.LegacyAllocation) return new SettlementResult(SettlementCode.NotReady, row);
            if (row.Phase != SettlementPhase.Prepared) return new SettlementResult(SettlementCode.Replay, row);
            Apply(row.Intent); return Set(row, SettlementPhase.Finalized);
        }
        /// <summary>M6 wage transfer completion: the treasury debit and the worker credit are one short
        /// synchronous commit; a repeated call replays instead of paying twice.</summary>
        public SettlementResult CompleteTransfer(string id)
        {
            AssertThread(); var row = Read(id);
            if (row == null || row.Intent.Kind != SettlementKind.WageTransfer && row.Intent.Kind != SettlementKind.PublicTransfer) return new SettlementResult(SettlementCode.NotReady, row);
            if (row.Phase != SettlementPhase.Prepared) return new SettlementResult(SettlementCode.Replay, row);
            Apply(row.Intent); return Set(row, SettlementPhase.Finalized);
        }
        /// <summary>M7 purchase completion: buyer debit and treasury credit are one short synchronous
        /// commit that follows the real hand-over of goods; a repeat replays instead of charging twice.</summary>
        public SettlementResult CompletePurchase(string id)
        {
            AssertThread(); var row = Read(id);
            if (row == null || row.Intent.Kind != SettlementKind.Purchase) return new SettlementResult(SettlementCode.NotReady, row);
            if (row.Phase != SettlementPhase.Prepared) return new SettlementResult(SettlementCode.Replay, row);
            Apply(row.Intent); return Set(row, SettlementPhase.Finalized);
        }
        public SettlementResult Cancel(string id, string reason)
        {
            AssertThread(); var row = Read(id);
            if (row == null) return new SettlementResult(SettlementCode.NotReady);
            return row.Phase == SettlementPhase.Prepared ? Set(row, SettlementPhase.Cancelled, reason) : new SettlementResult(SettlementCode.Replay, row);
        }
        public SettlementResult RequireReconciliation(string id, string reason)
        {
            AssertThread(); var row = Read(id);
            if (row == null) return new SettlementResult(SettlementCode.NotReady);
            return row.Phase == SettlementPhase.Prepared ? Set(row, SettlementPhase.ReconciliationRequired, reason) : new SettlementResult(SettlementCode.Replay, row);
        }
        internal static FundedSettlementLedger Restore(IEnumerable<SettlementEntry> rows, IEnumerable<string> enrollments = null)
        {
            var ledger = new FundedSettlementLedger();
            // FA-01：零余额账户**没有资金条目**，因此必须随存档单独恢复；重放顺序无关（Enroll 幂等且不覆盖余额）。
            if (enrollments != null) foreach (var id in enrollments) ledger.EnrollAccount(id);
            foreach (var row in rows)
            {
                if (ledger.Prepare(row.Intent).Code != SettlementCode.Prepared) throw new FormatException("InvalidLedgerSequenceOrFunding");
                var prepared = ledger.Read(row.Intent.CommandId);
                if (row.Phase == SettlementPhase.Finalized) ledger.Apply(row.Intent);
                ledger.Set(prepared, row.Phase, row.Reason);
                if (row.Phase == SettlementPhase.Finalized && row.Reason != null) throw new FormatException("FinalizedWithFailure");
            }
            return ledger;
        }
    }
}

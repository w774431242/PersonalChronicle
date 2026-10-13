using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalChronicle.Domain.PawnEconomy.Finance
{
    // WageTransfer and Purchase are appended so existing serialized values keep their meaning (the codec
    // writes the enum name, and an old save can never contain these values).
    internal enum SettlementKind { Deposit, Withdrawal, LegacyAllocation, WageTransfer, Purchase, PublicTransfer }
    internal enum SettlementPhase { Prepared, Finalized, Cancelled, ReconciliationRequired }
    internal enum SettlementCode
    { Prepared, Finalized, Cancelled, ReconciliationRequired, Replay, CommandConflict, StaleVersion, NotReady, InsufficientFunds, CapacityReached, InvalidClaim }

    internal sealed class SettlementIntent
    {
        public string CommandId { get; }
        public long ExpectedVersion { get; }
        public SettlementKind Kind { get; }
        public long AmountMinor { get; }
        public ReserveMovementIntent Movement { get; }
        public string ClaimId { get; }
        public string TargetAccount { get; }
        /// <summary>Explicit debit account for purchases, budget transfers and institution wages.</summary>
        public string PayerAccount { get; }
        public SettlementIntent(string commandId, long expectedVersion, ReserveMovementIntent movement)
        {
            CommandId = MinorCurrency.Required(commandId);
            if (expectedVersion < 0 || movement == null || movement.CommandId != commandId) throw new ArgumentException("InvalidSettlementIntent");
            ExpectedVersion = expectedVersion; Movement = movement;
            Kind = movement.Kind == ReserveMovementKind.Deposit ? SettlementKind.Deposit : SettlementKind.Withdrawal;
            AmountMinor = MinorCurrency.FromSilver(movement.SilverCount);
        }
        public SettlementIntent(string commandId, long expectedVersion, string claimId, string targetAccount, long amountMinor,
            SettlementKind kind = SettlementKind.LegacyAllocation, string payerAccount = null)
        {
            CommandId = MinorCurrency.Required(commandId); ClaimId = MinorCurrency.Required(claimId);
            TargetAccount = MinorCurrency.Required(targetAccount); MinorCurrency.Validate(amountMinor);
            bool purchase = kind == SettlementKind.Purchase;
            bool publicTransfer = kind == SettlementKind.PublicTransfer;
            if (expectedVersion < 0 || amountMinor == 0
                || !purchase && !publicTransfer && kind != SettlementKind.LegacyAllocation && kind != SettlementKind.WageTransfer
                || kind == SettlementKind.WageTransfer && SettlementLedgerNames.IsPublic(TargetAccount)
                || kind == SettlementKind.LegacyAllocation && (TargetAccount == SettlementLedgerNames.Treasury || TargetAccount.StartsWith(SettlementLedgerNames.InstitutionPrefix, StringComparison.Ordinal))
                || purchase && (!SettlementLedgerNames.IsPublic(TargetAccount) || string.IsNullOrWhiteSpace(payerAccount) || SettlementLedgerNames.IsPublic(payerAccount))
                || publicTransfer && (!SettlementLedgerNames.IsPublic(TargetAccount) || !SettlementLedgerNames.IsPublic(payerAccount) || TargetAccount == payerAccount)
                || kind == SettlementKind.WageTransfer && payerAccount != null && !SettlementLedgerNames.IsPublic(payerAccount))
                throw new ArgumentException("InvalidAllocationIntent");
            ExpectedVersion = expectedVersion; Kind = kind; AmountMinor = amountMinor;
            PayerAccount = purchase || publicTransfer || kind == SettlementKind.WageTransfer && payerAccount != null
                ? MinorCurrency.Required(payerAccount) : null;
        }
        /// <summary>M6/M11 wage transfer: the treasury pays one worker account for one wage obligation.
        /// The obligation id is the claim, so the same obligation can never produce a second transfer.</summary>
        public static SettlementIntent ForWage(string commandId, long expectedVersion, string obligationId, string workerAccount, long amountMinor, string publicPayer = null)
            => new SettlementIntent(commandId, expectedVersion, obligationId, workerAccount, amountMinor, SettlementKind.WageTransfer, publicPayer);
        /// <summary>M7/M11 purchase transfer: one buyer account pays the treasury (public seller) for one
        /// delivered order. The order id is the claim, so the same order can never be charged twice.</summary>
        public static SettlementIntent ForPurchase(string commandId, long expectedVersion, string orderId, string payerAccount, long amountMinor, string publicSeller = SettlementLedgerNames.Treasury)
            => new SettlementIntent(commandId, expectedVersion, orderId, publicSeller, amountMinor,
                SettlementKind.Purchase, payerAccount);
        public static SettlementIntent ForPublicTransfer(string commandId, long expectedVersion, string source, string target, long amountMinor)
            => new SettlementIntent(commandId, expectedVersion, commandId, target, amountMinor, SettlementKind.PublicTransfer, source);
        public bool SameAs(SettlementIntent other) => other != null && CommandId == other.CommandId && ExpectedVersion == other.ExpectedVersion
            && Kind == other.Kind && AmountMinor == other.AmountMinor && ClaimId == other.ClaimId && TargetAccount == other.TargetAccount
            && PayerAccount == other.PayerAccount
            && (Movement == null ? other.Movement == null : Movement.SameAs(other.Movement));
    }
    internal static class SettlementLedgerNames
    {
        public const string Treasury = "Treasury", InstitutionPrefix = "PublicInstitution:";
        public static string Institution(string anchorKey) => InstitutionPrefix + MinorCurrency.Required(anchorKey);
        // Old public/clinic claims only enter this ledger through a real, funded cutover allocation.
        // Recognizing those existing balances does not import the old unfunded bookkeeping amounts.
        public static bool IsPublic(string id) => id == Treasury || id == "public"
            || id?.StartsWith(InstitutionPrefix, StringComparison.Ordinal) == true && id.Length > InstitutionPrefix.Length
            || id?.StartsWith("institution:", StringComparison.Ordinal) == true && id.Length > "institution:".Length;
        public static long PublicTotal(IEnumerable<FinancialAccountSnapshot> accounts)
            => accounts.Where(a => IsPublic(a.Id)).Sum(a => a.BalanceMinor);
    }
    internal sealed class SettlementEntry
    {
        public SettlementIntent Intent { get; }
        public SettlementPhase Phase { get; }
        public string Reason { get; }
        public SettlementEntry(SettlementIntent intent, SettlementPhase phase, string reason = null)
        {
            Intent = intent ?? throw new ArgumentNullException(nameof(intent));
            if (!Enum.IsDefined(typeof(SettlementPhase), phase)) throw new ArgumentException("InvalidSettlementPhase");
            Phase = phase; Reason = reason;
        }
    }
    internal sealed class SettlementResult
    {
        public SettlementCode Code { get; }
        public SettlementEntry Entry { get; }
        public SettlementResult(SettlementCode code, SettlementEntry entry = null) { Code = code; Entry = entry; }
    }
    internal sealed class LegacyCashClaim
    {
        public string Id { get; }
        public long RecordId { get; }
        public string Kind { get; }
        public string LegacyParty { get; }
        public long AmountMinor { get; }
        public LegacyCashClaim(string id, long recordId, string kind, string party, long amount)
        { Id = MinorCurrency.Required(id); RecordId = recordId; Kind = kind; LegacyParty = MinorCurrency.Required(party); MinorCurrency.Validate(amount); AmountMinor = amount; }
    }
    /// <summary>
    /// CB-ECON-002：结算账本的**账户读取可用性**（只读判定，供需求页个人账户摘要区分
    /// 「尚未建立账户」与「账本不可判定 / 读取失败」）。
    /// 三态即 <c>ReadAccount</c> 返回 <c>null</c> 时的三种**不同**事实，禁止合并：
    /// <list type="bullet">
    /// <item><see cref="Ready"/>：账本健康 —— 此时 <c>null</c> 只表示该主体**尚未建立**个人账户；</item>
    /// <item><see cref="Unavailable"/>：账本正在存读档或恢复未完成（瞬时不可判定，不得当作"没有账户"）；</item>
    /// <item><see cref="Unsupported"/>：账本以**原始态保留**（schema / 文本编码不受支持）；</item>
    /// <item><see cref="Faulted"/>：恢复或校验失败（读取失败，须显式呈现而非伪造 0）。</item>
    /// </list>
    /// 本枚举只描述**读取能力**：不授予开户、迁移、补齐资金或解除预留的任何权限。
    /// </summary>
    internal enum SettlementLedgerState { Ready, Unavailable, Unsupported, Faulted }

    internal sealed class SettlementAudit
    {
        public long Version { get; }
        public long? TotalLiabilitiesMinor { get; }
        public long? ActualReserveMinor { get; }
        public long? DeficitMinor => ActualReserveMinor.HasValue && TotalLiabilitiesMinor.HasValue ? Math.Max(0, TotalLiabilitiesMinor.Value - ActualReserveMinor.Value) : (long?)null;
        public string Fault { get; }
        public bool CanExecuteInGame => false;
        public IReadOnlyList<FinancialAccountSnapshot> Accounts { get; }
        public SettlementAudit(long version, long? total, long? reserve, string fault, IEnumerable<FinancialAccountSnapshot> accounts)
        { Version = version; TotalLiabilitiesMinor = total; ActualReserveMinor = reserve; Fault = fault; Accounts = Array.AsReadOnly(accounts.ToArray()); }
    }
}

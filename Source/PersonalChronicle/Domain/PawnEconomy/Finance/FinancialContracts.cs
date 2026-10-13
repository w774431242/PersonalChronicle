using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalChronicle.Domain.PawnEconomy.Finance
{
    public static class MinorCurrency
    {
        public const long PerSilver = 100;
        public const long Maximum = 1000000000000L;
        public static long FromSilver(long count)
        {
            if (count < 0 || count > Maximum / PerSilver) throw new ArgumentOutOfRangeException(nameof(count));
            return checked(count * PerSilver);
        }
        public static long WholeSilver(long minor)
        { Validate(minor); return minor / PerSilver; }
        public static long Remainder(long minor)
        { Validate(minor); return minor % PerSilver; }
        public static void Validate(long minor)
        { if (minor < 0 || minor > Maximum) throw new ArgumentOutOfRangeException(nameof(minor)); }
        internal static string Required(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256) throw new ArgumentException("InvalidStableId");
            return value;
        }
    }

    public sealed class FinancialAccountSnapshot
    {
        public string Id { get; }
        public long BalanceMinor { get; }
        public long ReservedMinor { get; }
        public long AvailableMinor => BalanceMinor - ReservedMinor;
        public FinancialAccountSnapshot(string id, long balanceMinor, long reservedMinor = 0)
        {
            Id = MinorCurrency.Required(id); MinorCurrency.Validate(balanceMinor); MinorCurrency.Validate(reservedMinor);
            if (reservedMinor > balanceMinor) throw new ArgumentException("ReservationExceedsBalance");
            BalanceMinor = balanceMinor; ReservedMinor = reservedMinor;
        }
    }
    public sealed class FinancialTransferLeg
    {
        public string DebitAccount { get; }
        public string CreditAccount { get; }
        public long AmountMinor { get; }
        public FinancialTransferLeg(string debitAccount, string creditAccount, long amountMinor)
        {
            DebitAccount = MinorCurrency.Required(debitAccount); CreditAccount = MinorCurrency.Required(creditAccount);
            MinorCurrency.Validate(amountMinor);
            if (amountMinor == 0 || DebitAccount == CreditAccount) throw new ArgumentException("InvalidTransfer");
            AmountMinor = amountMinor;
        }
    }
    public sealed class FinancialIntent
    {
        public const int MaximumLegs = 64, MaximumResources = 64;
        public string CommandId { get; }
        public long ExpectedVersion { get; }
        public long ExpiresTick { get; }
        public IReadOnlyList<FinancialTransferLeg> Legs { get; }
        public IReadOnlyList<string> ResourceKeys { get; }
        public FinancialIntent(string commandId, long expectedVersion, long expiresTick,
            IEnumerable<FinancialTransferLeg> legs, IEnumerable<string> resourceKeys = null)
        {
            CommandId = MinorCurrency.Required(commandId);
            if (expectedVersion < 0 || expiresTick < 0) throw new ArgumentOutOfRangeException();
            ExpectedVersion = expectedVersion; ExpiresTick = expiresTick;
            var transfers = (legs ?? throw new ArgumentNullException(nameof(legs))).Take(MaximumLegs + 1).ToArray();
            var resources = (resourceKeys ?? Array.Empty<string>()).Take(MaximumResources + 1).ToArray();
            if (transfers.Length == 0 || transfers.Length > MaximumLegs || transfers.Any(x => x == null)
                || resources.Length > MaximumResources) throw new ArgumentException("InvalidBundleSize");
            foreach (var key in resources) MinorCurrency.Required(key);
            if (resources.Distinct(StringComparer.Ordinal).Count() != resources.Length) throw new ArgumentException("DuplicateResource");
            Legs = Array.AsReadOnly(transfers);
            ResourceKeys = Array.AsReadOnly(resources.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        }
        public bool SameAs(FinancialIntent other) => other != null && CommandId == other.CommandId
            && ExpectedVersion == other.ExpectedVersion && ExpiresTick == other.ExpiresTick
            && ResourceKeys.SequenceEqual(other.ResourceKeys, StringComparer.Ordinal) && Legs.Count == other.Legs.Count
            && Legs.Zip(other.Legs, (a, b) => a.DebitAccount == b.DebitAccount && a.CreditAccount == b.CreditAccount
                && a.AmountMinor == b.AmountMinor).All(x => x);
    }
    public enum FinancialPhase { Prepared, Committed, Cancelled, Expired }
    public enum FinancialResultCode
    {
        Applied, Replay, UnknownCommand, CommandConflict, StaleVersion, Expired, ClockReversed,
        MissingAccount, InsufficientFunds, AmountLimit, ResourceBusy, CapacityReached, InvalidPhase,
        MissingEvidence, EvidenceConflict
    }
    public sealed class FinancialTransactionSnapshot
    {
        public FinancialIntent Intent { get; }
        public FinancialPhase Phase { get; }
        public string EvidenceId { get; }
        public FinancialTransactionSnapshot(FinancialIntent intent, FinancialPhase phase, string evidenceId = null)
        { Intent = intent; Phase = phase; EvidenceId = evidenceId; }
    }
    public sealed class FinancialResult
    {
        public FinancialResultCode Code { get; }
        public long Version { get; }
        public FinancialTransactionSnapshot Transaction { get; }
        public bool Accepted => Code == FinancialResultCode.Applied || Code == FinancialResultCode.Replay;
        public FinancialResult(FinancialResultCode code, long version, FinancialTransactionSnapshot transaction = null)
        { Code = code; Version = version; Transaction = transaction; }
    }
}

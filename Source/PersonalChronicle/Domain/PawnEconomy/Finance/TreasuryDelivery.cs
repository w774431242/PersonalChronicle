using System;

namespace PersonalChronicle.Domain.PawnEconomy.Finance
{
    internal enum TreasuryDeliveryPhase { Waiting, Carrying, Completed, Cancelled, Faulted }
    internal sealed class TreasuryDelivery
    {
        public const int MaximumOrders = 512, LifetimeTicks = 5000, MaximumSilverCount = 500;
        public int Id { get; }
        public string CommandId { get; }
        public ReserveMovementKind Kind { get; }
        public TreasuryDeliveryPhase Phase { get; }
        public string PawnKey { get; }
        public string BankKey { get; }
        public int MapId { get; }
        public string SourceKey { get; }
        public string CarryKey { get; }
        public int Count { get; }
        public int ExpiresTick { get; }
        public long LedgerVersion { get; }
        public long ReserveVersion { get; }
        public bool Pending => Phase == TreasuryDeliveryPhase.Waiting || Phase == TreasuryDeliveryPhase.Carrying;
        public TreasuryDelivery(int id, string command, ReserveMovementKind kind, TreasuryDeliveryPhase phase, string pawn,
            string bank, int map, string source, string carry, int count, int expires, long ledgerVersion, long reserveVersion)
        {
            if (id <= 0 || id > MaximumOrders || !Enum.IsDefined(typeof(ReserveMovementKind), kind)
                || !Enum.IsDefined(typeof(TreasuryDeliveryPhase), phase) || map < 0 || count <= 0 || count > MaximumSilverCount
                || expires < 0 || ledgerVersion < 0 || reserveVersion < 0) throw new ArgumentException("InvalidTreasuryDelivery");
            Id = id; CommandId = MinorCurrency.Required(command); PawnKey = MinorCurrency.Required(pawn);
            BankKey = MinorCurrency.Required(bank); SourceKey = MinorCurrency.Required(source);
            if (carry != null) MinorCurrency.Required(carry);
            if (phase == TreasuryDeliveryPhase.Carrying && (carry == null || kind != ReserveMovementKind.Deposit)) throw new ArgumentException("MissingCarryIdentity");
            Kind = kind; Phase = phase; MapId = map; CarryKey = carry; Count = count; ExpiresTick = expires;
            LedgerVersion = ledgerVersion; ReserveVersion = reserveVersion;
        }
        public TreasuryDelivery With(TreasuryDeliveryPhase phase, string carry = null)
            => new TreasuryDelivery(Id, CommandId, Kind, phase, PawnKey, BankKey, MapId, SourceKey, carry ?? CarryKey,
                Count, ExpiresTick, LedgerVersion, ReserveVersion);
        public SettlementIntent Intent(string carriedSource = null)
            => new SettlementIntent(CommandId, LedgerVersion, new ReserveMovementIntent(CommandId, Kind, ReserveVersion,
                PawnKey, Kind == ReserveMovementKind.Deposit ? carriedSource ?? CarryKey ?? SourceKey : SourceKey, Count));
    }
}

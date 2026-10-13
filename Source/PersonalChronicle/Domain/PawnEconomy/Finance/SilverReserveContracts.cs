using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalChronicle.Domain.PawnEconomy.Finance
{
    public enum ReserveMovementKind { Deposit, Withdrawal }
    public enum ReserveMovementPhase { Prepared, Completed, ReconciliationRequired }
    public enum ReserveResultCode
    {
        Completed, Replay, CommandConflict, StaleVersion, Busy, NotReady, CapacityReached,
        InvalidCounterparty, InvalidSource, InsufficientSilver, DestinationRejected, ReconciliationRequired
    }
    public sealed class ReserveMovementIntent
    {
        public string CommandId { get; }
        public ReserveMovementKind Kind { get; }
        public long ExpectedVersion { get; }
        public string CounterpartyId { get; }
        public string SourceThingId { get; }
        public int SilverCount { get; }
        public ReserveMovementIntent(string commandId, ReserveMovementKind kind, long expectedVersion,
            string counterpartyId, string sourceThingId, int silverCount)
        {
            CommandId = MinorCurrency.Required(commandId); CounterpartyId = MinorCurrency.Required(counterpartyId);
            SourceThingId = MinorCurrency.Required(sourceThingId);
            if (!Enum.IsDefined(typeof(ReserveMovementKind), kind) || expectedVersion < 0 || silverCount <= 0)
                throw new ArgumentOutOfRangeException();
            Kind = kind; ExpectedVersion = expectedVersion; SilverCount = silverCount;
        }
        public bool SameAs(ReserveMovementIntent other) => other != null && CommandId == other.CommandId
            && Kind == other.Kind && ExpectedVersion == other.ExpectedVersion && CounterpartyId == other.CounterpartyId
            && SourceThingId == other.SourceThingId && SilverCount == other.SilverCount;
    }
    public sealed class ReserveMovementEvidence
    {
        public ReserveMovementIntent Intent { get; }
        public ReserveMovementPhase Phase { get; }
        public long ReserveBefore { get; }
        public long? ReserveAfter { get; }
        public long CounterpartyBefore { get; }
        public long? CounterpartyAfter { get; }
        public string ResultThingId { get; }
        public int? ApiReturnedCount { get; }
        public string Error { get; }
        public long? ObservedReserveDelta => ReserveAfter.HasValue ? ReserveAfter - ReserveBefore : null;
        public long? CompletedMinor => Phase == ReserveMovementPhase.Completed ? MinorCurrency.FromSilver(Intent.SilverCount) : (long?)null;
        public ReserveMovementEvidence(ReserveMovementIntent intent, ReserveMovementPhase phase, long reserveBefore,
            long? reserveAfter, long counterpartyBefore, long? counterpartyAfter, string resultThingId = null,
            int? apiReturnedCount = null, string error = null)
        {
            Intent = intent ?? throw new ArgumentNullException(nameof(intent)); Phase = phase;
            if (!Enum.IsDefined(typeof(ReserveMovementPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            MinorCurrency.FromSilver(reserveBefore); MinorCurrency.FromSilver(counterpartyBefore);
            if (reserveAfter.HasValue) MinorCurrency.FromSilver(reserveAfter.Value);
            if (counterpartyAfter.HasValue) MinorCurrency.FromSilver(counterpartyAfter.Value);
            ReserveBefore = reserveBefore; ReserveAfter = reserveAfter; CounterpartyBefore = counterpartyBefore;
            CounterpartyAfter = counterpartyAfter; ResultThingId = resultThingId; ApiReturnedCount = apiReturnedCount; Error = error;
        }
        public bool HasExactQuantityProof => ReserveAfter.HasValue && CounterpartyAfter.HasValue
            && (Intent.Kind == ReserveMovementKind.Deposit
                ? ReserveAfter - ReserveBefore == Intent.SilverCount && CounterpartyBefore - CounterpartyAfter == Intent.SilverCount
                : ReserveBefore - ReserveAfter == Intent.SilverCount && CounterpartyAfter - CounterpartyBefore == Intent.SilverCount);
    }
    public sealed class ReserveMovementResult
    {
        public ReserveResultCode Code { get; }
        public long Version { get; }
        public ReserveMovementEvidence Evidence { get; }
        public ReserveMovementResult(ReserveResultCode code, long version, ReserveMovementEvidence evidence = null)
        { Code = code; Version = version; Evidence = evidence; }
    }
    public sealed class ReserveStackSnapshot
    {
        public string ThingId { get; }
        public int SilverCount { get; }
        public ReserveStackSnapshot(string thingId, int count) { ThingId = thingId; SilverCount = count; }
    }
    public sealed class ReserveAuditSnapshot
    {
        public long Version { get; }
        public long? ActualSilver { get; }
        public long? RecordedSilver { get; }
        public long? DifferenceSilver => ActualSilver.HasValue && RecordedSilver.HasValue ? ActualSilver - RecordedSilver : null;
        public long? DeficitMinor => DifferenceSilver.HasValue ? MinorCurrency.FromSilver(Math.Max(0, -DifferenceSilver.Value)) : (long?)null;
        public string Fault { get; }
        public bool CanMove => string.IsNullOrEmpty(Fault) && DifferenceSilver == 0;
        public bool CanExecuteInGame => false;
        public IReadOnlyList<ReserveStackSnapshot> Stacks { get; }
        public ReserveAuditSnapshot(long version, long? actualSilver, long? recordedSilver, string fault,
            IEnumerable<ReserveStackSnapshot> stacks)
        {
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (actualSilver.HasValue) MinorCurrency.FromSilver(actualSilver.Value);
            if (recordedSilver.HasValue) MinorCurrency.FromSilver(recordedSilver.Value);
            Version = version; ActualSilver = actualSilver; RecordedSilver = recordedSilver; Fault = fault;
            Stacks = Array.AsReadOnly(stacks.ToArray());
        }
    }
    public sealed class ReserveWithdrawalPlan
    {
        public long RequestedMinor { get; }
        public long AvailableBeforeMinor { get; }
        public long SilverCount { get; }
        public long DebitedMinor => MinorCurrency.FromSilver(SilverCount);
        public long AvailableAfterMinor => AvailableBeforeMinor - DebitedMinor;
        public long UnconvertedRequestMinor => RequestedMinor - DebitedMinor;
        internal ReserveWithdrawalPlan(long requested, long available)
        { RequestedMinor = requested; AvailableBeforeMinor = available; SilverCount = MinorCurrency.WholeSilver(requested); }
    }
    public static class ReserveWithdrawalRules
    {
        // Arithmetic only. The future ledger coordinator must prove Treasury ownership and hard claims.
        public static bool TryPlan(long requestedMinor, long treasuryAvailableMinor, out ReserveWithdrawalPlan plan)
        {
            MinorCurrency.Validate(requestedMinor); MinorCurrency.Validate(treasuryAvailableMinor); plan = null;
            if (requestedMinor < MinorCurrency.PerSilver || requestedMinor > treasuryAvailableMinor) return false;
            plan = new ReserveWithdrawalPlan(requestedMinor, treasuryAvailableMinor); return true;
        }
    }
}

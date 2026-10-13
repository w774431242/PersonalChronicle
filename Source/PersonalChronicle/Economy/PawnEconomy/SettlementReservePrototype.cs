using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// Isolated physical store. B2.3 attaches it to a save sidecar, never a gameplay payment route.
    /// It proves movement quantities, never Treasury rights or financial ledger issuance.
    /// </summary>
    internal sealed class SettlementReservePrototype : IThingHolder, IExposable
    {
        internal const int MaximumStacks = 4096;
        private int ownerThread = Thread.CurrentThread.ManagedThreadId;
        internal void BindOwnerAtHostBoundary()
        {
            if (busy || loading) throw new InvalidOperationException("CannotRebindActiveReserve");
            ownerThread = Thread.CurrentThread.ManagedThreadId;
        }
        private ThingOwner<Thing> silver;
        private readonly Dictionary<string, ReserveMovementEvidence> journal = new Dictionary<string, ReserveMovementEvidence>(StringComparer.Ordinal);
        private long version;
        private string fault, metadata;
        private bool busy, loading, preserveRaw;
        public IThingHolder ParentHolder => null;
        public bool CanExecuteInGame => false;
        public SettlementReservePrototype() { silver = new ThingOwner<Thing>(this, false, LookMode.Deep); }
        ThingOwner IThingHolder.GetDirectlyHeldThings() => silver;
        public void GetChildHolders(List<IThingHolder> outChildren) { }
        private void AssertThread()
        { if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("WrongThread"); }
        public ReserveMovementEvidence ReadMovement(string commandId)
        { AssertThread(); return journal.TryGetValue(commandId, out var evidence) ? evidence : null; }
        internal IReadOnlyList<ReserveMovementEvidence> ReadMovements()
        { AssertThread(); return Array.AsReadOnly(journal.Values.OrderBy(x => x.Intent.ExpectedVersion).ToArray()); }
        private static bool CountSilver(ThingOwner holder, bool strict, out long count)
        {
            count = 0;
            if (holder == null || holder.Count > MaximumStacks || ThingDefOf.Silver == null) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < holder.Count; i++)
            {
                Thing thing = holder[i];
                if (thing == null) return false;
                if (thing.def != ThingDefOf.Silver) { if (strict) return false; continue; }
                if (thing.Destroyed || thing.Spawned || thing.holdingOwner != holder || thing.stackCount <= 0
                    || thing.stackCount > thing.def.stackLimit || thing.thingIDNumber < 0 || !seen.Add(thing.GetUniqueLoadID())) return false;
                count = checked(count + thing.stackCount);
            }
            return count <= MinorCurrency.Maximum / MinorCurrency.PerSilver;
        }
        private long RecordedSilver() => journal.Values.Where(x => x.Phase == ReserveMovementPhase.Completed)
            .Sum(x => x.Intent.Kind == ReserveMovementKind.Deposit ? (long)x.Intent.SilverCount : -x.Intent.SilverCount);
        public ReserveAuditSnapshot ReadAudit()
        {
            AssertThread(); bool valid = CountSilver(silver, true, out long actual);
            long? recorded = preserveRaw ? null : (long?)RecordedSilver();
            string state = fault ?? (loading ? "Loading" : busy ? "Busy" : journal.Values.Any(x => x.Phase != ReserveMovementPhase.Completed)
                ? "UnresolvedMovement" : !valid ? "InvalidReserveContents"
                : recorded != actual ? "ReserveQuantityMismatch" : null);
            var stacks = valid ? silver.Select(x => new ReserveStackSnapshot(x.GetUniqueLoadID(), x.stackCount)).ToArray()
                : Array.Empty<ReserveStackSnapshot>();
            return new ReserveAuditSnapshot(version, valid ? actual : (long?)null, recorded, state, stacks);
        }
        private bool MatchesCounterparty(ThingOwner counterpart, string expectedId)
        {
            if (counterpart == null || counterpart == silver || counterpart.Owner == null || counterpart.Owner is Map
                || counterpart.Owner.GetDirectlyHeldThings() != counterpart) return false;
            IThingHolder current = counterpart.Owner; string id = null;
            var seen = new HashSet<IThingHolder>();
            for (int depth = 0; current != null && depth < 16; depth++)
            {
                if (!seen.Add(current) || current is SettlementReservePrototype) return false;
                if (id == null && current is ILoadReferenceable reference) id = reference.GetUniqueLoadID();
                current = current.ParentHolder;
            }
            return current == null && id == expectedId;
        }
        public ReserveMovementResult Execute(ReserveMovementIntent intent, ThingOwner counterparty)
        {
            AssertThread(); if (intent == null) throw new ArgumentNullException(nameof(intent));
            ReserveMovementResult Result(ReserveResultCode code, ReserveMovementEvidence evidence = null)
                => new ReserveMovementResult(code, version, evidence);
            if (busy) return Result(ReserveResultCode.Busy);
            if (loading || Scribe.mode != LoadSaveMode.Inactive) return Result(ReserveResultCode.NotReady);
            if (journal.TryGetValue(intent.CommandId, out var previous))
                return Result(previous.Intent.SameAs(intent) ? ReserveResultCode.Replay : ReserveResultCode.CommandConflict, previous);
            if (preserveRaw || !string.IsNullOrEmpty(fault)) return Result(ReserveResultCode.NotReady);
            var audit = ReadAudit();
            if (!audit.CanMove) { fault = audit.Fault ?? "ReserveQuantityMismatch"; return Result(ReserveResultCode.NotReady); }
            if (intent.ExpectedVersion != version) return Result(ReserveResultCode.StaleVersion);
            if (journal.Count >= ReserveJournalCodec.MaximumEntries) return Result(ReserveResultCode.CapacityReached);
            busy = true;
            try
            {
                if (!MatchesCounterparty(counterparty, intent.CounterpartyId)
                    || !CountSilver(counterparty, false, out long partyBefore)) return Result(ReserveResultCode.InvalidCounterparty);
                bool deposit = intent.Kind == ReserveMovementKind.Deposit;
                ThingOwner source = deposit ? counterparty : silver;
                ThingOwner destination = deposit ? silver : counterparty;
                var candidates = source.Where(x => x.GetUniqueLoadID() == intent.SourceThingId).Take(2).ToArray();
                if (candidates.Length != 1 || candidates[0].def != ThingDefOf.Silver || candidates[0].holdingOwner != source)
                    return Result(ReserveResultCode.InvalidSource);
                Thing stack = candidates[0];
                if (stack.stackCount < intent.SilverCount) return Result(ReserveResultCode.InsufficientSilver);
                if (deposit && silver.Count >= MaximumStacks) return Result(ReserveResultCode.CapacityReached);
                if (deposit && intent.SilverCount > MinorCurrency.Maximum / MinorCurrency.PerSilver - audit.ActualSilver.Value)
                    return Result(ReserveResultCode.CapacityReached);
                if (destination.GetCountCanAccept(stack, false) < intent.SilverCount) return Result(ReserveResultCode.DestinationRejected);
                if (!CountSilver(silver, true, out long checkedReserve) || checkedReserve != audit.ActualSilver.Value
                    || !CountSilver(counterparty, false, out long checkedParty) || checkedParty != partyBefore
                    || stack.holdingOwner != source || !source.Contains(stack) || stack.stackCount < intent.SilverCount)
                { fault = "PreflightQuantityChanged"; return Result(ReserveResultCode.ReconciliationRequired); }
                long reserveBefore = audit.ActualSilver.Value;
                var prepared = new ReserveMovementEvidence(intent, ReserveMovementPhase.Prepared, reserveBefore, null, partyBefore, null);
                journal.Add(intent.CommandId, prepared); version++;
                Thing result = null; int? returned = null; string error = null;
                try { returned = source.TryTransferToContainer(stack, destination, intent.SilverCount, out result, false); }
                catch (Exception problem) when (!(problem is OutOfMemoryException) && !(problem is StackOverflowException))
                {
                    error = problem.GetType().FullName;
                    // Whole-stack SplitOff may detach the original before a source-owner callback
                    // throws. Keep that known object saveable; never issue credit or retry delivery.
                    if (!stack.Destroyed && !stack.Spawned && stack.holdingOwner == null && stack.def == ThingDefOf.Silver
                        && stack.stackCount > 0 && stack.stackCount <= stack.def.stackLimit && silver.TryAdd(stack, false))
                    { result = stack; error += ":DetachedSourceSafeguarded"; }
                }
                bool reserveValid = CountSilver(silver, true, out long reserveAfter);
                bool partyValid = CountSilver(counterparty, false, out long partyAfter);
                var observed = new ReserveMovementEvidence(intent, ReserveMovementPhase.ReconciliationRequired, reserveBefore,
                    reserveValid ? reserveAfter : (long?)null, partyBefore, partyValid ? partyAfter : (long?)null,
                    result?.GetUniqueLoadID(), returned, error);
                bool exact = error == null && observed.HasExactQuantityProof && result != null && !result.Destroyed
                    && !result.Spawned && result.def == ThingDefOf.Silver && result.holdingOwner == destination
                    && destination.Contains(result) && result.stackCount == intent.SilverCount;
                var final = new ReserveMovementEvidence(intent, exact ? ReserveMovementPhase.Completed : ReserveMovementPhase.ReconciliationRequired,
                    reserveBefore, observed.ReserveAfter, partyBefore, observed.CounterpartyAfter, observed.ResultThingId,
                    returned, error ?? (exact ? null : "QuantityOrOwnershipMismatch"));
                journal[intent.CommandId] = final;
                if (!exact) fault = "MovementReconciliationRequired";
                return Result(exact ? ReserveResultCode.Completed : ReserveResultCode.ReconciliationRequired, final);
            }
            catch (Exception problem) when (!(problem is OutOfMemoryException) && !(problem is StackOverflowException))
            {
                fault = "ReserveAdapterException:" + problem.GetType().FullName;
                if (journal.TryGetValue(intent.CommandId, out var interrupted))
                {
                    var uncertain = new ReserveMovementEvidence(intent, ReserveMovementPhase.ReconciliationRequired,
                        interrupted.ReserveBefore, null, interrupted.CounterpartyBefore, null, error: fault);
                    journal[intent.CommandId] = uncertain;
                    return Result(ReserveResultCode.ReconciliationRequired, uncertain);
                }
                return Result(ReserveResultCode.ReconciliationRequired);
            }
            finally { busy = false; }
        }
        public void ExposeData()
        {
            AssertThread();
            if (busy) throw new InvalidOperationException("CannotSaveDuringMovement");
            if (Scribe.mode == LoadSaveMode.Saving && !preserveRaw)
                metadata = ReserveJournalCodec.Encode(version, fault, journal.Values.OrderBy(x => x.Intent.ExpectedVersion));
            Scribe_Values.Look(ref metadata, "metadata");
            Scribe_Deep.Look(ref silver, "silver", this, false, LookMode.Deep, true);
            if (silver == null) silver = new ThingOwner<Thing>(this, false, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                loading = true; var loaded = ReserveJournalCodec.Decode(metadata);
                preserveRaw = loaded.PreserveRaw; version = loaded.Version; fault = loaded.Fault; journal.Clear();
                foreach (var entry in loaded.Entries) journal.Add(entry.Intent.CommandId, entry);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                loading = false;
                var audit = ReadAudit();
                if (!audit.CanMove && fault == null) fault = audit.Fault ?? "ReserveQuantityMismatch";
            }
        }
    }
}

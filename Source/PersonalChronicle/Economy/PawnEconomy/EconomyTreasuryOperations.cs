using System;
using System.Globalization;
using System.Linq;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Data;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Governance;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Economy.PawnEconomy
{
    [DefOf]
    public static class TreasuryJobDefOf
    {
        public static JobDef PC_PEA_TreasuryDelivery;
        static TreasuryJobDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(TreasuryJobDefOf)); }
    }
    public sealed partial class GameComponent_PawnEconomy
    {
        private TreasuryOperationsSaveData treasuryOperations = new TreasuryOperationsSaveData();
        private LegacyCutoverPlan cutoverPlan;
        private LegacySettlementArchive planOriginal, planLatest;
        private long planLedgerVersion = -1, planOperationsRevision = -1;
        internal LegacyCutoverPlan ReadCutoverPlan()
        {
            var latest = institutionMode.ReadCutover(); var original = settlement.ReadLegacyArchive();
            if (latest == null || original == null || latest.Fault != null || original.Fault != null) return null;
            long version = settlement.Version;
            if (cutoverPlan == null || planOriginal != original || planLatest != latest || version != planLedgerVersion || planOperationsRevision != treasuryOperations.Revision)
            {
                planOriginal = original; planLatest = latest; planLedgerVersion = version; planOperationsRevision = treasuryOperations.Revision;
                cutoverPlan = LegacyCutoverPlan.Build(original.Hash, original.Report, original.Claims, latest.Hash, latest.Report,
                    latest.Claims, settlement.ReadEntries(), treasuryOperations.SnapshotHash, treasuryOperations.ReadDecisions());
            }
            return cutoverPlan;
        }
        public string QueryTreasuryStamp() => treasuryOperations.Revision.ToString(CultureInfo.InvariantCulture) + "/"
            + settlement.Version.ToString(CultureInfo.InvariantCulture) + "/" + institutionMode.State.Revision.ToString(CultureInfo.InvariantCulture);
        private bool TreasuryAuthorized(bool cleanup, bool inspectFunds = true)
        {
            var phase = institutionMode.State.Phase;
            return Scribe.mode == LoadSaveMode.Inactive && treasuryOperations.Fault == null && institutionMode.State.Fault == null
                && institutionMode.Frozen && (!inspectFunds || settlement.ReadAudit().Fault == null) && ReadCutoverPlan() is LegacyCutoverPlan plan && plan.Fault == null
                && (institutionMode.State.Desired && (phase == InstitutionPhase.Preparing || phase == InstitutionPhase.Active)
                    || cleanup && phase == InstitutionPhase.Draining);
        }
        private static bool PublicBank(Building_EconomyInstitution bank) => bank != null && bank.Spawned && !bank.Destroyed
            && bank.Faction == Faction.OfPlayer && !bank.IsBurning()
            && bank.def.GetModExtension<EconomyInstitutionExtension>()?.kind == InstitutionKind.SettlementOffice;
        public bool ExecuteTreasuryCommand(string verb, int id, string key, string actorKey, string expectedStamp)
        {
            if (expectedStamp != QueryTreasuryStamp() || !TreasuryAuthorized(verb != "Treasury.Deposit")) return false;
            if (verb == "Treasury.AllocateBudget" || verb == "Treasury.ReturnBudget")
            {
                var institution = anchors.FirstOrDefault(a => a.Spawned && !a.Destroyed && a.GetUniqueLoadID() == key);
                if (treasuryOperations.Pending != null) return false;
                string account;
                if (verb == "Treasury.ReturnBudget")
                {
                    account = SettlementLedgerNames.IsPublic(key) ? key : institution == null ? null : InstitutionFundingKey(institution);
                    if (account == null || account == SettlementLedgerNames.Treasury || settlement.ReadAccount(account) == null) return false;
                }
                else
                {
                    if (institution == null || institution.Faction != Faction.OfPlayer) return false;
                    account = InstitutionFundingKey(institution);
                }
                long amount = verb == "Treasury.ReturnBudget" ? settlement.ReadAccount(account)?.AvailableMinor ?? 0
                    : id > 0 && id <= TreasuryDelivery.MaximumSilverCount ? MinorCurrency.FromSilver(id) : 0;
                if (amount <= 0) return false;
                var intent = SettlementIntent.ForPublicTransfer("institution-budget:" + Guid.NewGuid().ToString("N"), settlement.Version,
                    verb == "Treasury.ReturnBudget" ? account : SettlementLedgerNames.Treasury,
                    verb == "Treasury.ReturnBudget" ? SettlementLedgerNames.Treasury : account, amount);
                return settlement.TransferPublic(intent).Code == SettlementCode.Finalized;
            }
            if (verb == "Treasury.Cancel")
            { var order = treasuryOperations.Find(id); if (order == null || !order.Pending) return false; CancelTreasuryDelivery(order); ApplyInstitutionModeSetting(institutionMode.State.Desired); return true; }
            if (verb == "Treasury.Acknowledge")
            {
                if (treasuryOperations.Pending != null) return false;
                var item = ReadCutoverPlan()?.Items.SingleOrDefault(x => x.Key == key);
                if (item == null || !treasuryOperations.Acknowledge(institutionMode.ReadCutover().Hash, item.Key, item.Disposition)) return false;
                ApplyInstitutionModeSetting(institutionMode.State.Desired); return true;
            }
            if (verb == "Treasury.FundClaim")
            {
                if (treasuryOperations.Pending != null) return false;
                var cash = ReadCutoverPlan()?.Cash.SingleOrDefault(x => x.Claim.Id == key);
                if (cash == null || cash.Remaining <= 0) return false;
                var intent = new SettlementIntent("cutover:" + Guid.NewGuid().ToString("N"), settlement.Version, cash.Claim.Id, cash.Claim.LegacyParty, cash.Remaining);
                bool done = settlement.AllocateCutover(intent).Code == SettlementCode.Finalized;
                ApplyInstitutionModeSetting(institutionMode.State.Desired); return done;
            }
            if (verb != "Treasury.Deposit" && verb != "Treasury.Withdraw") return false;
            var bank = anchors.FirstOrDefault(x => x.GetUniqueLoadID() == key);
            var pawn = PawnByKey(actorKey);
            if (!PublicBank(bank) || !BasicPawn(pawn) || pawn.Map != bank.Map || pawn.CurJob?.playerForced == true
                || pawn.carryTracker?.CarriedThing != null || treasuryOperations.Pending != null || treasuryOperations.NextId > TreasuryDelivery.MaximumOrders
                || id <= 0 || id > TreasuryDelivery.MaximumSilverCount || ThingDefOf.Silver == null || id > ThingDefOf.Silver.stackLimit || !pawn.CanReserveAndReach(bank, PathEndMode.Touch, Danger.None)) return false;
            bool deposit = verb == "Treasury.Deposit";
            string source;
            if (deposit)
            {
                var stack = bank.Map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(x => x.Spawned && !x.IsForbidden(pawn)
                    && GameComponent_EconomyStorage.Current?.IsColonyPublic(x) == true
                    && x.stackCount >= id && pawn.CanReserveAndReach(x, PathEndMode.ClosestTouch, Danger.None))
                    .OrderBy(x => x.Position.DistanceToSquared(pawn.Position)).ThenBy(x => x.thingIDNumber).FirstOrDefault();
                if (stack == null) return false; source = stack.GetUniqueLoadID();
            }
            else
            {
                var stack = settlement.ReadReserveAudit().Stacks.FirstOrDefault(x => x.SilverCount >= id);
                if (stack == null) return false; source = stack.ThingId;
            }
            int now = Find.TickManager.TicksGame; if (now < 0 || now > int.MaxValue - TreasuryDelivery.LifetimeTicks) return false;
            var request = new TreasuryDelivery(treasuryOperations.NextId, "treasury:" + Guid.NewGuid().ToString("N"),
                deposit ? ReserveMovementKind.Deposit : ReserveMovementKind.Withdrawal, TreasuryDeliveryPhase.Waiting,
                pawn.GetUniqueLoadID(), bank.GetUniqueLoadID(), pawn.Map.uniqueID, source, null, id, now + TreasuryDelivery.LifetimeTicks, settlement.Version, settlement.ReserveVersion);
            if (!deposit && settlement.PreparePhysical(request.Intent()).Code != SettlementCode.Prepared) return false;
            if (!treasuryOperations.Add(request)) { if (!deposit) settlement.CancelUnmovedPhysical(request.CommandId); return false; }
            ApplyInstitutionModeSetting(institutionMode.State.Desired); return true;
        }
        private Thing DeliverySource(TreasuryDelivery order, Pawn pawn)
        {
            var current = pawn.CurJob;
            return current?.def == TreasuryJobDefOf.PC_PEA_TreasuryDelivery && current.count == order.Id
                ? current.targetB.Thing
                : pawn.Map.listerThings.ThingsOfDef(ThingDefOf.Silver).FirstOrDefault(x => x.GetUniqueLoadID() == order.SourceKey);
        }
        internal bool TreasuryNeedsPickup(int id) => treasuryOperations.Find(id)?.Phase == TreasuryDeliveryPhase.Waiting;
        private Job PlanPublicSilverDelivery(Pawn pawn)
        {
            if (!SafeDecision(pawn) || pawn.carryTracker.CarriedThing != null || treasuryOperations.Pending != null
                || treasuryOperations.NextId > TreasuryDelivery.MaximumOrders || !TreasuryAuthorized(false)) return null;
            var bank = anchors.Where(a => PublicBank(a) && a.Map == pawn.Map && pawn.CanReserveAndReach(a, PathEndMode.Touch, Danger.None))
                .OrderBy(a => a.Position.DistanceToSquared(pawn.Position)).ThenBy(a => a.thingIDNumber).FirstOrDefault();
            if (bank == null) return null;
            var source = pawn.Map.listerThings.ThingsOfDef(ThingDefOf.Silver)
                .Where(t => t.Spawned && GameComponent_EconomyStorage.Current?.IsColonyPublic(t) == true
                    && GameComponent_EconomyStorage.Current.RightFor(t).SourceKey != SettlementLedgerNames.Treasury
                    && !t.IsForbidden(pawn) && pawn.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.None))
                .OrderBy(t => t.Position.DistanceToSquared(pawn.Position)).ThenBy(t => t.thingIDNumber).FirstOrDefault();
            if (source == null) return null;
            int now = Find.TickManager.TicksGame;
            if (now < 0 || now > int.MaxValue - TreasuryDelivery.LifetimeTicks) return null;
            int count = Math.Min(source.stackCount, Math.Min(ThingDefOf.Silver.stackLimit, TreasuryDelivery.MaximumSilverCount));
            var request = new TreasuryDelivery(treasuryOperations.NextId, "public-silver:" + Guid.NewGuid().ToString("N"),
                ReserveMovementKind.Deposit, TreasuryDeliveryPhase.Waiting, pawn.GetUniqueLoadID(), bank.GetUniqueLoadID(),
                pawn.Map.uniqueID, source.GetUniqueLoadID(), null, count, now + TreasuryDelivery.LifetimeTicks, settlement.Version, settlement.ReserveVersion);
            if (!treasuryOperations.Add(request)) return null;
            return PlanTreasuryDelivery(pawn);
        }
        internal bool TreasuryJobValid(Pawn pawn, int id)
        {
            var order = treasuryOperations.Find(id);
            if (order == null || !order.Pending || pawn == null || order.PawnKey != pawn.GetUniqueLoadID() || !SafeDecision(pawn)
                || pawn.Map.uniqueID != order.MapId || Find.TickManager.TicksGame >= order.ExpiresTick || !TreasuryAuthorized(order.Kind == ReserveMovementKind.Withdrawal, false)) return false;
            var bank = anchors.FirstOrDefault(x => x.GetUniqueLoadID() == order.BankKey);
            if (!PublicBank(bank) || bank.Map != pawn.Map) return false;
            var row = settlement.ReadCommand(order.CommandId);
            if (row != null && row.Phase != SettlementPhase.Prepared) return false;
            if (order.Kind == ReserveMovementKind.Withdrawal) return pawn.carryTracker?.CarriedThing == null && row?.Phase == SettlementPhase.Prepared;
            var carried = pawn.carryTracker?.CarriedThing;
            return order.Phase == TreasuryDeliveryPhase.Carrying ? carried != null && carried.GetUniqueLoadID() == order.CarryKey
                && carried.def == ThingDefOf.Silver && carried.stackCount == order.Count
                && GameComponent_EconomyStorage.Current?.IsColonyPublic(carried) == true
                : carried == null && DeliverySource(order, pawn)?.stackCount >= order.Count
                    && GameComponent_EconomyStorage.Current?.IsColonyPublic(DeliverySource(order, pawn)) == true;
        }
        private Job PlanTreasuryDelivery(Pawn pawn)
        {
            var order = treasuryOperations.Pending;
            if (order == null || !TreasuryJobValid(pawn, order.Id)) return null;
            var bank = anchors.First(x => x.GetUniqueLoadID() == order.BankKey);
            if (!pawn.CanReserveAndReach(bank, PathEndMode.Touch, Danger.None)) return null;
            var job = JobMaker.MakeJob(TreasuryJobDefOf.PC_PEA_TreasuryDelivery, bank);
            job.count = order.Id;
            if (order.Kind == ReserveMovementKind.Deposit && order.Phase == TreasuryDeliveryPhase.Waiting)
            {
                var source = DeliverySource(order, pawn);
                if (source == null || !pawn.CanReserveAndReach(source, PathEndMode.ClosestTouch, Danger.None)) return null;
                job.targetB = source;
            }
            return job;
        }
        internal bool PickUpTreasurySilver(Pawn pawn, int id, Thing source)
        {
            var order = treasuryOperations.Find(id);
            if (!TreasuryJobValid(pawn, id) || order.Kind != ReserveMovementKind.Deposit || order.Phase != TreasuryDeliveryPhase.Waiting
                || source == null || source.GetUniqueLoadID() != order.SourceKey || source.IsForbidden(pawn)
                || pawn.CurJob?.def != TreasuryJobDefOf.PC_PEA_TreasuryDelivery || pawn.CurJob.count != id
                || !pawn.Map.reservationManager.ReservedBy(source, pawn, pawn.CurJob)
                || !source.Position.InHorDistOf(pawn.Position, 2f)) return false;
            int moved = 0; string pickupFault = null;
            try { moved = pawn.carryTracker.TryStartCarry(source, order.Count); }
            catch (Exception error) when (!(error is OutOfMemoryException) && !(error is StackOverflowException))
            {
                pickupFault = "TreasuryPickupException:" + error.GetType().Name;
                // Preserve a known whole source detached by native pickup before a callback failed.
                // An unknown partial split is never reconstructed or credited without evidence.
                if (!source.Destroyed && !source.Spawned && source.holdingOwner == null && pawn.carryTracker.CarriedThing == null
                    && source.def == ThingDefOf.Silver && source.stackCount > 0 && source.stackCount <= source.def.stackLimit)
                {
                    try { pawn.carryTracker.innerContainer.TryAdd(source, false); }
                    catch (Exception safeguardError) when (!(safeguardError is OutOfMemoryException) && !(safeguardError is StackOverflowException))
                    { pickupFault += ":SafeguardFailed"; }
                }
            }
            var carried = pawn.carryTracker.CarriedThing;
            if (carried != null && carried.def == ThingDefOf.Silver) treasuryOperations.Update(order.With(TreasuryDeliveryPhase.Carrying, carried.GetUniqueLoadID()));
            if (pickupFault != null) { treasuryOperations.Freeze(pickupFault); ApplyInstitutionModeSetting(institutionMode.State.Desired); return false; }
            return moved == order.Count && carried?.stackCount == order.Count;
        }
        internal bool CompleteTreasuryDelivery(Pawn pawn, int id)
        {
            var order = treasuryOperations.Find(id);
            if (!TreasuryJobValid(pawn, id)) return false;
            var bank = anchors.First(x => x.GetUniqueLoadID() == order.BankKey);
            if (pawn.CurJob?.def != TreasuryJobDefOf.PC_PEA_TreasuryDelivery || pawn.CurJob.count != id
                || !pawn.Map.reservationManager.ReservedBy(bank, pawn, pawn.CurJob)
                || !pawn.Position.InHorDistOf(bank.Position, 3f)) return false;
            if (order.Kind == ReserveMovementKind.Deposit && settlement.ReadCommand(order.CommandId) == null
                && (order.LedgerVersion != settlement.Version || order.ReserveVersion != settlement.ReserveVersion))
            {
                if (!treasuryOperations.RebaseUnpreparedDeposit(order.Id, settlement.Version, settlement.ReserveVersion)) return false;
                order = treasuryOperations.Find(id);
            }
            var result = order.Kind == ReserveMovementKind.Deposit
                ? settlement.ExecutePhysical(order.Intent(), pawn.carryTracker.innerContainer)
                : settlement.DeliverPreparedPhysical(order.Intent(), pawn.carryTracker.innerContainer);
            bool done = result.Entry?.Phase == SettlementPhase.Finalized;
            if (done && order.Kind == ReserveMovementKind.Withdrawal)
                GameComponent_EconomyStorage.Current?.ReceivePublicSource("Treasury", 0, pawn.carryTracker.CarriedThing);
            if (!done && settlement.ReadCommand(order.CommandId)?.Phase == SettlementPhase.Prepared)
                settlement.CancelUnmovedPhysical(order.CommandId);
            treasuryOperations.Update(order.With(done ? TreasuryDeliveryPhase.Completed : result.Entry?.Phase == SettlementPhase.ReconciliationRequired
                ? TreasuryDeliveryPhase.Faulted : TreasuryDeliveryPhase.Cancelled, order.Kind == ReserveMovementKind.Withdrawal
                    ? pawn.carryTracker.CarriedThing?.GetUniqueLoadID() : null));
            ApplyInstitutionModeSetting(institutionMode.State.Desired); return done;
        }
        private void CancelTreasuryDelivery(TreasuryDelivery order)
        {
            if (!order.Pending) return;
            var row = settlement.ReadCommand(order.CommandId);
            if (row?.Phase == SettlementPhase.Finalized)
            {
                string resultId = ReadDeliveryResultThing(order.CommandId);
                treasuryOperations.Update(order.With(TreasuryDeliveryPhase.Completed, order.Kind == ReserveMovementKind.Withdrawal ? resultId : null)); return;
            }
            if (row?.Phase == SettlementPhase.Prepared) settlement.CancelUnmovedPhysical(order.CommandId);
            row = settlement.ReadCommand(order.CommandId);
            treasuryOperations.Update(order.With(row?.Phase == SettlementPhase.Prepared || row?.Phase == SettlementPhase.ReconciliationRequired
                ? TreasuryDeliveryPhase.Faulted : TreasuryDeliveryPhase.Cancelled));
        }
        private string ReadDeliveryResultThing(string command) => settlement.ReadPhysicalEvidence(command)?.ResultThingId;
        internal void FinishTreasuryJob(Pawn pawn, int id)
        {
            var order = treasuryOperations.Find(id);
            if (order == null || pawn == null || order.PawnKey != pawn.GetUniqueLoadID()) return;
            CancelTreasuryDelivery(order); order = treasuryOperations.Find(id);
            if (pawn.Spawned && pawn.carryTracker?.CarriedThing?.GetUniqueLoadID() == order.CarryKey)
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
        }
        private void ReconcileTreasuryOperations(bool desired)
        {
            // The controller never treats a matching command name alone as proof of this delivery.
            if (treasuryOperations.Fault == null)
            foreach (var saved in treasuryOperations.ReadOrders())
            {
                var bound = settlement.ReadCommand(saved.CommandId);
                bool mismatch = bound != null && !saved.Intent().SameAs(bound.Intent)
                    || saved.Phase == TreasuryDeliveryPhase.Completed && (bound?.Phase != SettlementPhase.Finalized
                        || saved.Kind == ReserveMovementKind.Withdrawal && saved.CarryKey != ReadDeliveryResultThing(saved.CommandId))
                    || saved.Phase == TreasuryDeliveryPhase.Cancelled && bound != null && bound.Phase != SettlementPhase.Cancelled
                    || saved.Pending && saved.Kind == ReserveMovementKind.Withdrawal && bound == null
                    || saved.Phase == TreasuryDeliveryPhase.Faulted;
                if (mismatch) { treasuryOperations.Freeze("TreasuryDeliveryBindingConflict"); break; }
            }
            var order = treasuryOperations.Pending; if (order == null || treasuryOperations.Fault != null) return;
            var row = settlement.ReadCommand(order.CommandId);
            var pawn = PawnByKey(order.PawnKey); var bank = anchors.FirstOrDefault(x => x.GetUniqueLoadID() == order.BankKey);
            if (row != null && row.Phase != SettlementPhase.Prepared || !desired && order.Kind == ReserveMovementKind.Deposit
                || !BasicPawn(pawn) || !PublicBank(bank) || pawn.Map.uniqueID != order.MapId || bank.Map != pawn.Map
                || Find.TickManager.TicksGame >= order.ExpiresTick || institutionMode.State.Fault != null)
                CancelTreasuryDelivery(order);
        }
    }
    public sealed class JobDriver_TreasuryDelivery : JobDriver
    {
        private GameComponent_PawnEconomy Economy => GameComponent_PawnEconomy.Current;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed)
                || job.targetB.HasThing && Economy?.TreasuryNeedsPickup(job.count) == true && !pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed))
            { Economy?.FinishTreasuryJob(pawn, job.count); return false; }
            return true;
        }
        protected override System.Collections.Generic.IEnumerable<Toil> MakeNewToils()
        {
            this.AddFinishAction(condition => Economy?.FinishTreasuryJob(pawn, job.count));
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Economy == null || !Economy.TreasuryJobValid(pawn, job.count));
            // Keep toil indices stable on reload even when the old source reference has disappeared.
            var approachBank = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_Jump.JumpIf(approachBank, () => !Economy.TreasuryNeedsPickup(job.count) || !job.targetB.HasThing);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
            var pickup = ToilMaker.MakeToil("TreasurySilverPickup"); pickup.defaultCompleteMode = ToilCompleteMode.Instant;
            pickup.initAction = () => { if (!Economy.PickUpTreasurySilver(pawn, job.count, job.targetB.Thing)) EndJobWith(JobCondition.Incompletable); };
            yield return pickup;
            yield return approachBank;
            var handover = ToilMaker.MakeToil("TreasurySilverHandover"); handover.defaultCompleteMode = ToilCompleteMode.Instant;
            handover.initAction = () => { if (!Economy.CompleteTreasuryDelivery(pawn, job.count)) EndJobWith(JobCondition.Incompletable); };
            yield return handover;
        }
    }
}

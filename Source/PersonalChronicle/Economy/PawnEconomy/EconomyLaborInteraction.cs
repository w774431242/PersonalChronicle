using System;
using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy.Employment;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        internal const int LaborQueueCapacity = 4;
        internal const int LaborQueueTimeoutTicks = 1200;

        // Commands enter the same intent and Job path as autonomous planning. A menu never signs or pays.
        internal IEnumerable<FloatMenuOption> LaborInteractionOptions(Pawn pawn, Building_EconomyInstitution station)
        {
            if (pawn?.Map == null || pawn.Map != station.Map || !pawn.IsColonistPlayerControlled) yield break;
            var kind = station.def?.GetModExtension<EconomyInstitutionExtension>()?.kind;
            if (kind != InstitutionKind.LaborOffice && kind != InstitutionKind.SettlementOffice) yield break;
            string unavailable = LaborInteractionUnavailable(pawn, station);
            if (unavailable != null)
            {
                yield return new FloatMenuOption(LaborText("Interact.Unavailable") + " · " + LaborVisualText.ReasonLabel(unavailable), null);
                yield break;
            }
            if (kind == InstitutionKind.LaborOffice)
            {
                var pending = ContractNeedingDocument(pawn);
                if (pending != null && !HoldsLiveDocument(pawn, pending.ContractId))
                    yield return new FloatMenuOption(LaborText("Interact.Reissue"), () => OrderLaborInteraction(pawn, station, null, true));
                var flow = QueryLaborFlowFor(pawn);
                if (flow != null && string.IsNullOrEmpty(flow.ContractId))
                    yield return new FloatMenuOption(LaborText("Interact.Continue"), () => OrderLaborInteraction(pawn, station, flow.OpportunityKey, false));
                else if (ActiveContractFor(pawn.GetUniqueLoadID()) == null)
                {
                    var posted = Employment.ReadPostedContractsFor(station.GetUniqueLoadID());
                    var offers = new HashSet<string>(StringComparer.Ordinal);
                    bool any = false;
                    for (int i = 0; i < posted.Count && i < 16; i++)
                    {
                        var contract = posted[i];
                        if (contract == null || QueryCapability(contract.PositionKey) == DeliveryCapability.Unsupported) continue;
                        string opportunity = contract.PositionKey;
                        if (!offers.Add(opportunity)) continue;
                        string label = WorkPriceRules.Parse(opportunity, out _, out _, out string order)
                            ? EconomyLabels.TranslatedOrId("PC_PEA.Order." + order, order) : LaborFlowRules.ShortCode(contract.ContractId);
                        any = true;
                        yield return new FloatMenuOption(LaborText("Interact.Accept") + " · " + label,
                            () => OrderLaborInteraction(pawn, station, opportunity, false));
                    }
                    if (!any) yield return new FloatMenuOption(LaborText("Interact.NoOffers"), null);
                }
                else yield return new FloatMenuOption(LaborText("Interact.AlreadyContracted"), null);
            }
            else
            {
                if (TryPendingSubmission(pawn, out _))
                    yield return new FloatMenuOption(LaborText("Interact.Submit"), () => OrderLaborInteraction(pawn, station, null, false));
                else yield return new FloatMenuOption(LaborText("Interact.NoSubmission"), null);
            }
        }

        private static string LaborText(string suffix) => EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual." + suffix, suffix);

        private static string LaborInteractionUnavailable(Pawn pawn, Building_EconomyInstitution station)
        {
            if (pawn.Drafted) return "Drafted";
            if (pawn.Downed || pawn.InMentalState || pawn.Dead) return "Interrupted";
            if (pawn.needs?.food?.CurLevelPercentage < .35f) return "Hungry";
            if (pawn.needs?.rest?.CurLevelPercentage < .25f) return "Tired";
            if (station.Destroyed || !station.Spawned || station.IsBurning()) return "StationUnavailable";
            if (station.IsForbidden(pawn)) return "Forbidden";
            if (!LaborServiceCellUsable(pawn, station)) return "CannotReach";
            return null;
        }

        internal static bool LaborServiceCellUsable(Pawn pawn, Thing station)
        {
            if (pawn?.Map == null || station == null || station.Map != pawn.Map || !station.Spawned) return false;
            var cell = station.InteractionCell;
            return cell.InBounds(pawn.Map) && cell.Standable(pawn.Map) && !cell.IsForbidden(pawn)
                && pawn.CanReach(cell, PathEndMode.OnCell, Danger.None);
        }

        private void OrderLaborInteraction(Pawn pawn, Building_EconomyInstitution station, string opportunity, bool reissue)
        {
            string reason = LaborInteractionUnavailable(pawn, station);
            if (reason == null && opportunity != null
                && !TryBeginLaborIntent(pawn.GetUniqueLoadID(), pawn.Map.uniqueID, opportunity, Find.TickManager.TicksGame, out reason))
                reason = reason ?? "SignRejected";
            Job job = null;
            if (reason == null)
            {
                if (station.def.GetModExtension<EconomyInstitutionExtension>().kind == InstitutionKind.SettlementOffice)
                    job = PlanContractReissue(pawn) ?? PlanContractCollectOutputs(pawn) ?? PlanContractSubmission(pawn);
                else job = reissue ? PlanContractReissue(pawn) : PlanLaborVisit(pawn);
                // Honour the selected station; final commit rechecks its type/map/service surface.
                if (job != null && (job.def.defName == LaborSubmitJobDefName
                    || job.def.defName == LaborVisitJobDefName && station.def.GetModExtension<EconomyInstitutionExtension>().kind == InstitutionKind.LaborOffice)) job.targetA = station;
                if (job == null) reason = "NoActionAvailable";
            }
            if (reason != null)
            {
                // 制度门被挡（`InstitutionNotReady`）时补上**可执行的下一步**（与劳务站 inspect / 经济页 blocker 行同源）——
                // 2026-10-12 实机反馈：此前只弹「制度尚未开放新的用工承诺」，玩家不知道要做什么。
                string gateStep = reason == "InstitutionNotReady" ? GameComponent_PawnEconomy.FirstGateStep() : string.Empty;
                Messages.Message(LaborVisualText.ReasonLabel(reason)
                    + (string.IsNullOrEmpty(gateStep) ? string.Empty : " · " + gateStep), MessageTypeDefOf.RejectInput, false);
                return;
            }
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private WorkContract ContractNeedingDocument(Pawn pawn)
        {
            var flow = QueryLaborFlowFor(pawn);
            var contract = flow == null ? null : Employment.ReadContract(flow.ContractId);
            if (contract != null && contract.DeliveryMode == SettlementDeliveryMode.PhysicalSubmission
                && (contract.State == ContractState.Active || QueryUnpaidMinorFor(contract.ContractId) > 0 || LaborFlow.HasOpenBatches(contract.ContractId)))
                return contract;
            return null;
        }

        internal void LaborDeparture(Pawn pawn, Thing station, bool submission)
        {
            var flow = submission && TryPendingSubmission(pawn, out var obligation)
                ? LaborFlow.ReadWorkflow(obligation.ContractId) : QueryLaborFlowFor(pawn);
            if (flow == null || flow.Hold == FlowHold.ReconciliationRequired) return;
            if (flow.Hold != FlowHold.None) LaborFlow.Resume(flow, Find.TickManager.TicksGame);
            flow = LaborFlow.ReadWorkflowById(flow.WorkflowId);
            LaborFlow.SetStation(flow, station.GetUniqueLoadID(), Find.TickManager.TicksGame);
            MoveLaborStage(flow, submission ? LaborFlowStage.GoingToSettlement
                : string.IsNullOrEmpty(flow.ContractId) ? LaborFlowStage.GoingToLaborOffice : flow.Stage,
                submission ? "GoToSettlement" : string.IsNullOrEmpty(flow.ContractId) ? "GotoLaborOffice" : "ReissueContract");
        }

        internal void LaborQueueEntered(Pawn pawn, bool submission)
        {
            var flow = QueryLaborFlowFor(pawn);
            if (flow == null) return;
            if (!submission && string.IsNullOrEmpty(flow.ContractId)) MoveLaborStage(flow, LaborFlowStage.QueuedAtLaborOffice, "Queue");
            PostLaborSpeech(pawn, "PC_PEA.LaborVisual.Speech.Waiting", flow.WorkflowId + "|queue|" + (pawn.CurJob?.loadID ?? 0));
        }

        internal void LaborVerificationStarted(Pawn pawn)
        {
            if (!TryPendingSubmission(pawn, out var obligation)) return;
            var flow = LaborFlow.ReadWorkflow(obligation.ContractId);
            MoveLaborStage(flow, LaborFlowStage.VerifyingSubmission, "Verify");
            PostLaborSpeech(pawn, "PC_PEA.LaborVisual.Speech.Verifying", flow?.WorkflowId + "|verify|" + obligation.ObligationId + "|" + LaborFlow.ReadSubmissionsFor(obligation.ContractId).Count);
        }

        internal void LaborInterrupted(Pawn pawn, JobCondition condition, Thing target)
        {
            if (condition == JobCondition.Succeeded || pawn == null) return;
            var flow = QueryLaborFlowFor(pawn);
            if (flow == null || flow.Hold == FlowHold.ReconciliationRequired || flow.Hold == FlowHold.Blocked) return;
            string reason = pawn.Drafted ? "Drafted" : pawn.Downed ? "Downed" : pawn.InMentalState ? "MentalState"
                : pawn.needs?.food?.CurLevelPercentage < .35f ? "Hungry" : pawn.needs?.rest?.CurLevelPercentage < .25f ? "Tired"
                : target == null || target.Destroyed || !target.Spawned ? "StationUnavailable"
                : condition == JobCondition.Incompletable ? "CannotReach" : "Interrupted";
            bool blocked = reason == "StationUnavailable" || condition == JobCondition.Incompletable;
            LaborFlow.HoldAt(flow, blocked ? FlowHold.Blocked : FlowHold.Paused, reason, "RetryLater", flow.Stage,
                Find.TickManager.TicksGame, Find.TickManager.TicksGame + LaborFlowRules.BackoffTicks(1));
        }

        private void LaborPlanningBlocked(LaborWorkflow flow, string reason, string next)
        {
            if (flow == null || flow.Hold == FlowHold.ReconciliationRequired) return;
            if (flow.Hold == FlowHold.Blocked && flow.ReasonCode == reason) return;
            int now = Find.TickManager.TicksGame;
            LaborFlow.HoldAt(flow, FlowHold.Blocked, reason, next, flow.Stage, now, now + LaborFlowRules.BackoffTicks(1));
        }

        internal void LaborCounterBusy(Pawn pawn) => LaborPlanningBlocked(QueryLaborFlowFor(pawn), "CounterBusy", "Queue");

        internal static int LaborInventoryCount(Pawn pawn, string defName)
        {
            int total = 0;
            var container = pawn?.inventory?.innerContainer;
            if (container == null || string.IsNullOrEmpty(defName)) return 0;
            for (int i = 0; i < container.Count; i++) if (container[i].def?.defName == defName) total += container[i].stackCount;
            return total;
        }

        internal void LaborInventoryReceived(Pawn pawn, int collectedQuantity, string productDefName)
        {
            var flow = QueryLaborFlowFor(pawn);
            if (flow == null || flow.Hold == FlowHold.ReconciliationRequired) return;
            if (flow.ReasonCode == "DocumentMissing" && HoldsLiveDocument(pawn, flow.ContractId))
            {
                LaborFlow.Resume(flow, Find.TickManager.TicksGame);
                flow = LaborFlow.ReadWorkflowById(flow.WorkflowId);
                MoveLaborStage(flow, LaborFlowStage.Working, "Work");
                PostLaborSpeech(pawn, flow.DocumentGeneration > 1 ? "PC_PEA.LaborVisual.Speech.Reissued"
                    : "PC_PEA.LaborVisual.Speech.SignedDocument", flow.DocumentInstanceId + (flow.DocumentGeneration > 1 ? "|reissued" : "|issued"));
            }
            CacheLaborCarryPresentation(pawn);
            // Picking up a paper must never become a product receipt because other stock is in inventory.
            if (productDefName == WorkContractDocumentDefName) return;
            var product = QueryLaborHeldProduct(pawn);
            if (product != null && product.def?.defName == productDefName && collectedQuantity > 0
                && TryPendingSubmission(pawn, out var obligation)) LaborFlow.RecordEvent(flow, flow.WorkflowId + "|collected|" + pawn.CurJob.loadID,
                "OutputsCollected", Find.TickManager.TicksGame, evidenceId: obligation.EvidenceId,
                quantity: collectedQuantity, productDefName: productDefName, resultCode: "Applied");
        }

        internal LaborFlowCode ResolveLaborReconciliation(string workflowId, ReconciliationAction action, out string reason)
        {
            var code = LaborFlow.ResolveReconciliation(workflowId, action, Find.TickManager.TicksGame, out reason);
            return code;
        }

        private void NoteLaborPaymentUnavailable(Pawn pawn, WageObligation obligation, int now)
        {
            var marked = Employment.NotePaymentUnavailable(obligation.ObligationId);
            var flow = LaborFlow.ReadWorkflow(obligation.ContractId);
            if (flow == null || marked != EmploymentCode.Applied) return;
            if (flow.Stage == LaborFlowStage.AwaitingPayment && flow.Hold != FlowHold.ReconciliationRequired)
                LaborFlow.HoldAt(flow, FlowHold.Blocked, "PaymentUnavailable", "AwaitPayment", flow.Stage, now, 0);
            LaborFlow.RecordEvent(LaborFlow.ReadWorkflowById(flow.WorkflowId), "wage:" + obligation.ObligationId + "|unavailable",
                "PaymentUnavailable", now, obligation.EvidenceId, obligation.SubmissionId, 0, obligation.AmountMinor - obligation.PaidMinor, "PaymentUnavailable");
            PostLaborSpeech(pawn, "PC_PEA.LaborVisual.Speech.PaymentUnavailable", obligation.ObligationId + "|payment-unavailable");
        }

        private int laborActivityCursor;
        private void TickLaborActivity()
        {
            if (Find.TickManager.TicksGame % 30 != 0 || perceptionSource == null) return;
            var subjects = ObservedSubjects;
            for (int index = 0; index < Math.Min(8, subjects.Count); index++)
            {
                if (laborActivityCursor >= subjects.Count) laborActivityCursor = 0;
                var subject = subjects[laborActivityCursor++];
                if (!perceptionSource.TryGetPlanningPawn(subject.PawnId, subject.MapId, out Pawn pawn)) continue;
                CacheLaborCarryPresentation(pawn);
                var flow = QueryLaborFlowFor(pawn);
                if (flow == null || flow.Hold == FlowHold.ReconciliationRequired || pawn.CurJob?.def == null) continue;
                string jobName = pawn.CurJob.def.defName;
                if (jobName == LaborVisitJobDefName || jobName == LaborSubmitJobDefName || jobName == LaborCollectJobDefName) continue;
                var contract = Employment.ReadContract(flow.ContractId);
                if (contract?.State != ContractState.Active) continue;
                if (flow.Stage == LaborFlowStage.Working && flow.Hold == FlowHold.None)
                {
                    string pause = pawn.Drafted ? "Drafted" : pawn.Downed ? "Downed" : pawn.InMentalState ? "MentalState"
                        : jobName == "Ingest" ? "Hungry" : jobName == "LayDown" ? "Tired" : null;
                    if (pause != null)
                    { LaborFlow.HoldAt(flow, FlowHold.Paused, pause, "RetryLater", flow.Stage, Find.TickManager.TicksGame, 0); continue; }
                }
                bool tree = pawn.CurJob.targetA.Thing is Plant plant && plant.def.plant?.IsTree == true;
                string order = WorkPriceRules.OrderForJob(jobName, tree);
                if (jobName == "MineVein") order = "Mine";
                if (order == null || !WorkPriceRules.Parse(contract.PositionKey, out _, out _, out string wanted) || wanted != order) continue;
                if (flow.Hold == FlowHold.Paused) { LaborFlow.Resume(flow, Find.TickManager.TicksGame); flow = LaborFlow.ReadWorkflowById(flow.WorkflowId); }
                if (flow.Hold != FlowHold.None) continue;
                if (!string.IsNullOrEmpty(flow.CurrentStationKey)) LaborFlow.SetStation(flow, string.Empty, Find.TickManager.TicksGame);
                MoveLaborStage(flow, LaborFlowStage.Working, "Work");
            }
        }
    }

    // Reservation manager is the single owner of the four waiting claims and one service-cell claim.
    public abstract class JobDriver_LaborCounter : JobDriver
    {
        private int queueStartedTick;
        public override void ExposeData()
        { base.ExposeData(); Scribe_Values.Look(ref queueStartedTick, "laborQueueStartedTick", 0); }
        protected GameComponent_PawnEconomy Economy => GameComponent_PawnEconomy.Current;
        protected abstract bool IsSubmission { get; }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            var station = job.targetA.Thing;
            if (!GameComponent_PawnEconomy.LaborServiceCellUsable(pawn, station))
            { Economy?.LaborInterrupted(pawn, JobCondition.Incompletable, station); return false; }
            job.targetB = station.InteractionCell;
            if (!pawn.Reserve(station, job, GameComponent_PawnEconomy.LaborQueueCapacity, -1, null, errorOnFailed))
            { Economy?.LaborCounterBusy(pawn); return false; }
            foreach (var cell in GenRadial.RadialCellsAround(station.InteractionCell, 3f, true))
            {
                if (cell == station.InteractionCell || station.OccupiedRect().Contains(cell) || !cell.InBounds(pawn.Map)
                    || !cell.Standable(pawn.Map) || cell.IsForbidden(pawn)
                    || !pawn.CanReserveAndReach(cell, PathEndMode.OnCell, Danger.None)) continue;
                if (!pawn.Reserve(cell, job, 1, -1, null, false)) continue;
                job.targetC = cell;
                return true;
            }
            Economy?.LaborInterrupted(pawn, JobCondition.Incompletable, station);
            return false;
        }

        protected IEnumerable<Toil> ApproachCounter()
        {
            this.AddFinishAction(condition =>
            {
                (job.targetA.Thing as Building_EconomyInstitution)?.ClearLaborPresentation(pawn);
                Economy?.LaborInterrupted(pawn, condition, job.targetA.Thing);
            });
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Economy == null || pawn.Drafted || pawn.Downed || pawn.InMentalState
                || pawn.needs?.food?.CurLevelPercentage < .35f || pawn.needs?.rest?.CurLevelPercentage < .25f);
            this.FailOn(() => !job.targetB.Cell.InBounds(pawn.Map) || !job.targetB.Cell.Standable(pawn.Map)
                || job.targetB.Cell.IsForbidden(pawn) || job.targetA.Thing.IsBurning()
                || job.targetA.Thing.InteractionCell != job.targetB.Cell);
            var depart = ToilMaker.MakeToil("LaborDeparture");
            depart.defaultCompleteMode = ToilCompleteMode.Instant;
            depart.initAction = () => Economy.LaborDeparture(pawn, job.targetA.Thing, IsSubmission);
            yield return depart;
            yield return Toils_Goto.GotoCell(TargetIndex.C, PathEndMode.OnCell);
            var queue = ToilMaker.MakeToil("LaborCounterQueue");
            queue.defaultCompleteMode = ToilCompleteMode.Never;
            queue.initAction = () => { queueStartedTick = Find.TickManager.TicksGame; Economy.LaborQueueEntered(pawn, IsSubmission); };
            queue.tickAction = () =>
            {
                pawn.rotationTracker.FaceTarget(job.targetA);
                if (Find.TickManager.TicksGame - queueStartedTick > GameComponent_PawnEconomy.LaborQueueTimeoutTicks)
                { Economy.LaborCounterBusy(pawn); EndJobWith(JobCondition.Incompletable); return; }
                var occupant = job.targetB.Cell.GetFirstPawn(pawn.Map);
                if (occupant != null && occupant != pawn) return;
                if (pawn.Reserve(job.targetB, job, 1, -1, null, false)) ReadyForNextToil();
            };
            yield return queue;
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
        }

        protected Toil ServiceDelay(string name, int duration, Action start)
        {
            var toil = ToilMaker.MakeToil(name);
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = duration;
            toil.initAction = () => { pawn.rotationTracker.FaceTarget(job.targetA); start(); UpdatePresentation(); };
            toil.tickAction = () => { pawn.rotationTracker.FaceTarget(job.targetA); if (Find.TickManager.TicksGame % 30 == 0) UpdatePresentation(); };
            toil.WithProgressBarToilDelay(TargetIndex.A);
            return toil;
        }

        private void UpdatePresentation() => (job.targetA.Thing as Building_EconomyInstitution)?.PresentLaborDocuments(pawn);

        public override string GetReport()
        {
            if (!IsSubmission && pawn.Position == job.targetB.Cell && CurToilIndex >= 6)
                return EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.Report.Issuing", "Issuing");
            if (!IsSubmission && pawn.Position == job.targetB.Cell && !string.IsNullOrEmpty(Economy?.QueryLaborFlowFor(pawn)?.ContractId))
                return EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual.Report.Reissuing", "Reissuing");
            string suffix = pawn.Position == job.targetB.Cell ? IsSubmission ? "Report.Verifying" : "Report.Negotiating"
                : pawn.Position == job.targetC.Cell ? "Report.Queued" : IsSubmission ? "Report.GoingSettlement" : "Report.GoingOffice";
            return EconomyLabels.TranslatedOrId("PC_PEA.LaborVisual." + suffix, suffix);
        }
    }
}

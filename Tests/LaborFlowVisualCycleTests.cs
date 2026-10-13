using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Employment;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class LaborFlowVisualCycleTests
    {
        private static LaborWorkflow WorkingFlow(LaborFlowSaveData data)
        {
            var flow = new LaborWorkflow("flow:p1|opp1", "contract:1", "p1", "labor1",
                SettlementDeliveryMode.PhysicalSubmission, LaborFlowStage.Working, FlowHold.None,
                LaborFlowStage.None, string.Empty, "Work", 10, 0, 1, "paper:1", "opp1");
            Assert.That(data.WriteWorkflow(flow), Is.True);
            return flow;
        }

        [Test]
        public void SameContractCanWorkAfterPaymentAndCollectAfterPartialTrip()
        {
            var data = new LaborFlowSaveData();
            var service = new LaborFlowService(data);
            var initial = WorkingFlow(data);
            foreach (var stage in new[] { LaborFlowStage.CollectingOutputs, LaborFlowStage.GoingToSettlement,
                LaborFlowStage.VerifyingSubmission, LaborFlowStage.AwaitingPayment, LaborFlowStage.PaymentConfirmed,
                LaborFlowStage.Working, LaborFlowStage.CollectingOutputs, LaborFlowStage.GoingToSettlement,
                LaborFlowStage.VerifyingSubmission, LaborFlowStage.CollectingOutputs })
                Assert.That(service.MoveTo(initial, stage, stage.ToString(), 20 + (int)stage), Is.EqualTo(LaborFlowCode.Applied));
            var current = service.ReadWorkflow("contract:1");
            Assert.That(current.Stage, Is.EqualTo(LaborFlowStage.CollectingOutputs));
            Assert.That(current.ContractId, Is.EqualTo(initial.ContractId));
            Assert.That(current.DocumentInstanceId, Is.EqualTo(initial.DocumentInstanceId));
            Assert.That(data.SubmissionCount, Is.Zero, "observing a cycle cannot forge a submission");
            Assert.That(service.PaymentGrant(initial.ContractId), Is.Null);
        }

        [Test]
        public void UnboundIntentCannotCycleOrSkipSigningAndArchivedRemainsTerminal()
        {
            var data = new LaborFlowSaveData();
            var service = new LaborFlowService(data);
            service.BeginVisit("p1", "opp1", "labor1", SettlementDeliveryMode.PhysicalSubmission, 10, out var intent);
            Assert.That(service.MoveTo(intent, LaborFlowStage.Working, "Work", 11), Is.EqualTo(LaborFlowCode.Blocked));
            var bound = WorkingFlow(new LaborFlowSaveData());
            Assert.That(LaborFlowRules.IsStageTransition(LaborFlowStage.Working, LaborFlowStage.IntentReady, true), Is.False);
            Assert.That(LaborFlowRules.IsStageTransition(LaborFlowStage.Archived, LaborFlowStage.Working, true), Is.False);
            Assert.That(LaborFlowRules.IsStageTransition(LaborFlowStage.PaymentConfirmed, LaborFlowStage.Working, false), Is.False);
        }

        [Test]
        public void StationAndHistoryAreSnapshotsAndEventIdentityIsIndependentOfTick()
        {
            var data = new LaborFlowSaveData();
            var service = new LaborFlowService(data);
            var flow = WorkingFlow(data);
            Assert.That(service.SetStation(flow, "wage1", 100), Is.EqualTo(LaborFlowCode.Applied));
            Assert.That(service.RecordEvent(flow, "payment:obligation1", "Paid", 101, "e1", "s1", 28, 840,
                "Finalized", "WoodLog"), Is.EqualTo(LaborFlowCode.Applied));
            var snapshot = service.ReadHistory(flow.WorkflowId);
            Assert.That(service.RecordEvent(flow, "payment:obligation1", "Paid", 999, "e1", "s1", 28, 840,
                "Finalized", "WoodLog"), Is.EqualTo(LaborFlowCode.Replay));
            Assert.That(service.RecordEvent(flow, "payment:obligation2", "Paid", 101, "e2", "s2", 15, 420),
                Is.EqualTo(LaborFlowCode.Applied), "two obligations in the same tick are distinct events");
            Assert.That(snapshot.Count, Is.EqualTo(2), "existing readonly snapshot cannot change when storage changes");
            var paid = snapshot[1];
            Assert.That(paid.CurrentStationKey, Is.EqualTo("wage1"));
            Assert.That(paid.InstitutionKey, Is.EqualTo("labor1"));
            Assert.That(paid.AmountMinor, Is.EqualTo(840));
            Assert.That(paid.Quantity, Is.EqualTo(28));
            Assert.That(paid.EvidenceId, Is.EqualTo("e1"));
            Assert.That(paid.ProductDefName, Is.EqualTo("WoodLog"));
            Assert.That(((IList<LaborFlowEvent>)snapshot).IsReadOnly, Is.True);
            service.SetStation(flow, null, 102);
            Assert.That(service.ReadWorkflowById(flow.WorkflowId).CurrentStationKey, Is.Empty);
            Assert.That(paid.CurrentStationKey, Is.EqualTo("wage1"), "event station is historical, not current station");
        }

        [Test]
        public void HistoryIsBoundedWithoutChangingBatchesOrSubmissions()
        {
            var data = new LaborFlowSaveData();
            var service = new LaborFlowService(data);
            var flow = WorkingFlow(data);
            service.RegisterBatch(flow.ContractId, "e1", "p1", "WoodLog", "employer", 28, 20, 7, "cell", out var batch);
            for (int i = 0; i < 100; i++)
                Assert.That(service.RecordEvent(flow, "event:" + i, "Observed", 100 + i), Is.EqualTo(LaborFlowCode.Applied));
            var history = service.ReadHistory(flow.WorkflowId);
            Assert.That(history.Count, Is.EqualTo(LaborFlowSaveData.MaximumHistoryPerWorkflow));
            Assert.That(history[0].EventId, Is.EqualTo("event:36"));
            Assert.That(history[63].EventId, Is.EqualTo("event:99"));
            Assert.That(service.ReadBatchesFor(flow.ContractId)[0].RemainingQuantity, Is.EqualTo(28));
            Assert.That(data.SubmissionCount, Is.Zero);
            Assert.That(service.RecordEvent(flow, "negative", "Paid", 200, amountMinor: -1), Is.EqualTo(LaborFlowCode.InvalidCommand));
        }

        [Test]
        public void ResumeReturnsToSavedActionAndKeepsStationAndHistory()
        {
            var data = new LaborFlowSaveData();
            var service = new LaborFlowService(data);
            var flow = WorkingFlow(data);
            service.SetStation(flow, "wage1", 20);
            service.HoldAt(flow, FlowHold.Paused, "Drafted", "Resume", LaborFlowStage.GoingToSettlement, 30);
            service.Resume(flow, 40);
            var resumed = service.ReadWorkflowById(flow.WorkflowId);
            Assert.That(resumed.Stage, Is.EqualTo(LaborFlowStage.GoingToSettlement));
            Assert.That(resumed.Hold, Is.EqualTo(FlowHold.None));
            Assert.That(resumed.CurrentStationKey, Is.EqualTo("wage1"));
            Assert.That(service.ReadHistory(flow.WorkflowId)[1].ReasonCode, Is.EqualTo("Drafted"));
        }

        [Test]
        public void InterruptedNegotiationCanReallyRestartTravelWithoutSkippingSigning()
        {
            var data = new LaborFlowSaveData();
            var service = new LaborFlowService(data);
            service.BeginVisit("p1", "opp1", "labor1", SettlementDeliveryMode.PhysicalSubmission, 1, out var flow);
            service.MoveTo(flow, LaborFlowStage.GoingToLaborOffice, "GotoLaborOffice", 2);
            service.MoveTo(flow, LaborFlowStage.QueuedAtLaborOffice, "Wait", 3);
            service.MoveTo(flow, LaborFlowStage.Negotiating, "Negotiate", 4);
            service.HoldAt(flow, FlowHold.Paused, "Drafted", "Resume", LaborFlowStage.Negotiating, 5);
            service.Resume(flow, 6);
            Assert.That(service.MoveTo(flow, LaborFlowStage.GoingToLaborOffice, "GotoLaborOffice", 7), Is.EqualTo(LaborFlowCode.Applied));
            Assert.That(service.MoveTo(flow, LaborFlowStage.QueuedAtLaborOffice, "Wait", 8), Is.EqualTo(LaborFlowCode.Applied));
            Assert.That(service.MoveTo(flow, LaborFlowStage.Working, "Work", 9), Is.EqualTo(LaborFlowCode.Blocked));
            Assert.That(service.ReadWorkflowById(flow.WorkflowId).ContractId, Is.Empty);
        }

        [Test]
        public void CoveredSubmissionRequiresRealFullReceiptForMatchingPawnAndEvidence()
        {
            var data = new LaborFlowSaveData();
            var service = new LaborFlowService(data);
            var flow = WorkingFlow(data);
            service.RegisterBatch(flow.ContractId, "e1", "p1", "WoodLog", "employer", 28, 20, 7, "cell", out var batch);
            Assert.That(service.ReadCoveredSubmission(flow.ContractId, "e1", "p1"), Is.Null);
            service.AcceptSubmission(flow.ContractId, "p1", "wage1", new[] { "e1" }, new[] { batch.BatchId }, 10, "r1", 30);
            Assert.That(service.ReadCoveredSubmission(flow.ContractId, "e1", "p1"), Is.Null, "partial receipt is not covered");
            var accepted = service.AcceptSubmission(flow.ContractId, "p1", "wage1", new[] { "e1" }, new[] { batch.BatchId }, 18, "r2", 40);
            Assert.That(service.ReadCoveredSubmission(flow.ContractId, "e1", "p1").SubmissionId, Is.EqualTo(accepted.SubmissionId));
            Assert.That(service.ReadCoveredSubmission(flow.ContractId, "e1", "p2"), Is.Null);
            Assert.That(service.ReadCoveredSubmission(flow.ContractId, "other", "p1"), Is.Null);
            Assert.That(service.ReadCoveredSubmission("other", "e1", "p1"), Is.Null);
            var unwitnessed = new LaborFlowSaveData();
            var unwitnessedService = new LaborFlowService(unwitnessed);
            unwitnessed.WriteBatch(batch.Receive(28));
            Assert.That(unwitnessedService.ReadCoveredSubmission(flow.ContractId, "e1", "p1"), Is.Null, "closed batch alone cannot invent a receipt");
        }

        [Test]
        public void PartialTransportKeepsAggregateRightsAndStableReplayIdentity()
        {
            var data = new LaborFlowSaveData();
            var service = new LaborFlowService(data);
            var flow = WorkingFlow(data);
            service.RegisterBatch(flow.ContractId, "e1", "p1", "WoodLog", "employer", 28, 20, 7, "cell", out var batch);
            Assert.That(service.CanTransferBatchIdentity(batch.BatchId, 7, 10, "job1"), Is.True);
            Assert.That(service.TransferBatchIdentity(batch.BatchId, 7, 8, 10, "job1", 30, "pawn:p1"), Is.EqualTo(LaborFlowCode.Applied));
            Assert.That(service.CanTransferBatchIdentity(batch.BatchId, 7, 10, "job1"), Is.False, "do not perform native action again");
            Assert.That(service.TransferBatchIdentity(batch.BatchId, 7, 8, 10, "job1", 99, "pawn:p1"), Is.EqualTo(LaborFlowCode.Replay));
            Assert.That(service.TransferBatchIdentity(batch.BatchId, 7, 9, 10, "job1", 99, "pawn:p1"), Is.EqualTo(LaborFlowCode.Blocked));
            var batches = service.ReadBatchesFor(flow.ContractId);
            Assert.That(batches.Count, Is.EqualTo(2));
            int produced = 0, remaining = 0;
            foreach (var item in batches) { produced += item.ProducedQuantity; remaining += item.RemainingQuantity; }
            Assert.That(produced, Is.EqualTo(28)); Assert.That(remaining, Is.EqualTo(28));
            Assert.That(batches[1].ThingIdNumber, Is.EqualTo(8));
            Assert.That(batches[1].LocationHolderKey, Is.EqualTo("pawn:p1"));
            Assert.That(service.CanTransferBatchIdentity(batch.BatchId, 999, 5, "job2"), Is.False);
        }

        [Test]
        public void WholeTransportWithPriorAcceptanceCreatesChildWhileUnsettledWholeOnlyRelocates()
        {
            var data = new LaborFlowSaveData();
            var service = new LaborFlowService(data);
            var flow = WorkingFlow(data);
            service.RegisterBatch(flow.ContractId, "e1", "p1", "WoodLog", "employer", 28, 20, 7, "cell", out var batch);
            Assert.That(service.TransferBatchIdentity(batch.BatchId, 7, 8, 28, "job1", 30, "pawn:p1"), Is.EqualTo(LaborFlowCode.Applied));
            Assert.That(service.TransferBatchIdentity(batch.BatchId, 7, 8, 28, "job1", 31, "pawn:p1"), Is.EqualTo(LaborFlowCode.Replay));
            Assert.That(data.BatchCount, Is.EqualTo(1));
            service.WriteBatch(service.ReadBatchesFor(flow.ContractId)[0].Receive(10));
            Assert.That(service.TransferBatchIdentity(batch.BatchId, 8, 9, 18, "job2", 40, "pawn:p1"), Is.EqualTo(LaborFlowCode.Applied));
            var batches = service.ReadBatchesFor(flow.ContractId);
            Assert.That(batches.Count, Is.EqualTo(2));
            Assert.That(batches[0].ProducedQuantity, Is.EqualTo(10));
            Assert.That(batches[0].AcceptedQuantity, Is.EqualTo(10));
            Assert.That(batches[1].ProducedQuantity, Is.EqualTo(18));
            Assert.That(batches[1].RemainingQuantity, Is.EqualTo(18));
        }

        [Test]
        public void TransportCapacityIsPreflightedWithoutMutatingSource()
        {
            var data = new LaborFlowSaveData();
            var service = new LaborFlowService(data);
            var flow = WorkingFlow(data);
            service.RegisterBatch(flow.ContractId, "e1", "p1", "WoodLog", "employer", 28, 20, 7, "cell", out var batch);
            for (int i = 1; i < LaborFlowSaveData.MaximumBatches; i++)
                data.WriteBatch(new ProductBatch("capacity:" + i, "c", "e", "p", "WoodLog", "owner", 1, 0, 0, 1, 100 + i, "cell"));
            Assert.That(service.CanTransferBatchIdentity(batch.BatchId, 7, 10, "job1"), Is.False);
            Assert.That(service.TransferBatchIdentity(batch.BatchId, 7, 8, 10, "job1", 30, "pawn:p1"), Is.EqualTo(LaborFlowCode.Blocked));
            Assert.That(service.ReadBatchesFor(flow.ContractId)[0].ProducedQuantity, Is.EqualTo(28));
            Assert.That(service.CanTransferBatchIdentity(batch.BatchId, 7, 28, "job2"), Is.True, "full relocation consumes no additional batch capacity");
        }
    }
}

using System;
using System.Diagnostics;
using NUnit.Framework;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Tests
{
    public sealed class LaborVisualBoundedWorkloadTests
    {
        [Test]
        public void FullSpeechQueueRemainsBoundedUnderRepeatedVisibleAndHiddenRefreshes()
        {
            var queue = new LaborSpeechQueue();
            for (int pawn = 0; pawn < 64; pawn++)
                for (int entry = 0; entry < 8; entry++)
                    Assert.That(queue.Enqueue("p" + pawn, "w" + entry, "Paid", null, null, 2), Is.True);
            Assert.That(queue.Enqueue("excess", "paid", "Paid", null, null, 2), Is.False);
            var timer = Stopwatch.StartNew();
            for (int refresh = 0; refresh < 1000; refresh++) queue.Observe(refresh * .01, entry => false);
            Assert.That(queue.PawnCount, Is.EqualTo(64));
            Assert.That(queue.ExpiredCount, Is.Zero);
            int consumed = 0;
            while (queue.Consume(queue.Peek(entry => true))) consumed++;
            timer.Stop();
            Assert.That(consumed, Is.EqualTo(512));
            Assert.That(queue.PawnCount, Is.Zero);
            TestContext.WriteLine("SpeechQueue max fixture: 64 pawns / 512 events / 1000 hidden refreshes; elapsedMs="
                + timer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }

        [Test]
        public void MaximumWorkflowHistoriesRetainOnly64FactsAndNeverCreateFinancialReceipts()
        {
            var data = new LaborFlowSaveData();
            var timer = Stopwatch.StartNew();
            for (int id = 0; id < LaborFlowSaveData.MaximumWorkflows; id++)
            {
                var flow = new LaborWorkflow("flow:" + id, "contract:" + id, "pawn:" + id, "station",
                    SettlementDeliveryMode.PhysicalSubmission, LaborFlowStage.Working, FlowHold.None,
                    LaborFlowStage.None, string.Empty, "Work", 0, 0, 1, "document:" + id);
                Assert.That(data.WriteWorkflow(flow), Is.True);
                for (int tick = 0; tick < 80; tick++)
                    Assert.That(data.WriteEvent(new LaborFlowEvent("event:" + tick, flow, "StageChanged", tick)), Is.True);
                var history = data.ReadHistory(flow.WorkflowId);
                Assert.That(history.Count, Is.EqualTo(64));
                Assert.That(history[0].Tick, Is.EqualTo(16));
                Assert.That(history[63].Tick, Is.EqualTo(79));
            }
            timer.Stop();
            Assert.That(data.WorkflowCount, Is.EqualTo(1024));
            Assert.That(data.SubmissionCount, Is.Zero);
            Assert.That(data.BatchCount, Is.Zero);
            TestContext.WriteLine("History max fixture: 1024 workflows / 81920 input facts / 65536 retained facts; elapsedMs="
                + timer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}

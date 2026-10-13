using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Employment;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using Verse;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// CHG-20261011-LABOR-VISUAL · **真实 Scribe 往返**（CB-ECON-003 §10.2 的 T24/T26 存档面）。
    /// 走真 <c>Scribe.saver/loader</c>（与 <c>PawnEconomyScribeTests</c> 同一范式），不是内存拷贝：
    /// 只有真往返才能证明「ID 唯一 / 数量守恒 / 未知版本不覆写」这些跨读档不变量。
    /// </summary>
    [TestFixture, NonParallelizable]
    public sealed class LaborFlowScribeTests
    {
        private Harmony shim;
        private string path;

        [OneTimeSetUp] public void Start()
        {
            shim = new Harmony("personalchronicle.tests.laborflowscribe");
            foreach (string name in new[] { "Error", "Warning", "Message" })
                Patch(typeof(Log), name, typeof(string));
            Patch(typeof(DeepProfiler), "Start", typeof(string));
            Patch(typeof(DeepProfiler), "End");
        }
        private void Patch(Type type, string name, params Type[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, args, null);
            if (method != null)
                shim.Patch(method, prefix: new HarmonyMethod(
                    typeof(LaborFlowScribeTests).GetMethod(nameof(Skip), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        private static bool Skip() => false;
        [OneTimeTearDown] public void Stop() => shim.UnpatchAll(shim.Id);
        [SetUp] public void Setup() => path = Path.Combine(Path.GetTempPath(), "pc_laborflow_" + Guid.NewGuid().ToString("N") + ".xml");
        [TearDown] public void Cleanup() { Scribe.ForceStop(); if (File.Exists(path)) File.Delete(path); }

        // CA-05：未知（更高）版本的**原始载荷跨保存/载入往返完整保留** —— 修前只置 CanWrite=false，
        // 未来字段会在下次保存时被整段丢掉（经理探针实证：schema99 的未来字段在往返后消失）。
        [Test]
        public void UnknownSchemaPayloadSurvivesRoundTrip()
        {
            string future = Path.Combine(Path.GetTempPath(), "pc_laborflow_future_" + Guid.NewGuid().ToString("N") + ".xml");
            var utf8 = new System.Text.UTF8Encoding(false);
            File.WriteAllText(future,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<savegame>\n"
                + "  <pawnEconomicLaborFlow>\n"
                + "    <schemaVersion>99</schemaVersion>\n"
                + "    <futureField>KEEP-ME</futureField>\n"
                + "    <futureNested><a>1</a></futureNested>\n"
                + "  </pawnEconomicLaborFlow>\n</savegame>", utf8);
            try
            {
                LaborFlowSaveData loaded = null;
                Scribe.loader.InitLoading(future);
                Scribe_Deep.Look(ref loaded, "pawnEconomicLaborFlow");
                Scribe.loader.FinalizeLoading();
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.PayloadIntact, Is.False, "更高版本 ⇒ 不信任本载体语义");
                Assert.That(loaded.CanWrite, Is.False, "更高版本 ⇒ 一切写入被阻断");
                Assert.That(loaded.RawPayload, Does.Contain("futureField"), "原始载荷必须留证（不是只看 CanWrite=false）");
                // 再保存一次：未来字段必须仍在文件里。
                Scribe.saver.InitSaving(path, "savegame");
                Scribe_Deep.Look(ref loaded, "pawnEconomicLaborFlow");
                Scribe.saver.FinalizeSaving();
                string saved = File.ReadAllText(path, utf8);
                Assert.That(saved, Does.Contain("futureField"), "未知字段必须跨保存往返保留");
                Assert.That(saved, Does.Contain("KEEP-ME"), "未知字段的值不得被吞");
            }
            finally
            {
                Scribe.ForceStop();
                if (File.Exists(future)) File.Delete(future);
            }
        }

        private LaborFlowSaveData RoundTrip(LaborFlowSaveData data)
        {
            Scribe.saver.InitSaving(path, "savegame");
            Scribe_Deep.Look(ref data, "pawnEconomicLaborFlow");
            Scribe.saver.FinalizeSaving();
            LaborFlowSaveData loaded = null;
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref loaded, "pawnEconomicLaborFlow");
            Scribe.loader.FinalizeLoading();
            Assert.That(loaded, Is.Not.Null);
            return loaded;
        }

        [Test]
        public void RoundTripPreservesStationPauseEventHistoryAndTransportedChild()
        {
            var data = new LaborFlowSaveData();
            var service = new LaborFlowService(data);
            service.BeginVisit("p1", "opp1", "labor1", SettlementDeliveryMode.PhysicalSubmission, 1, out var flow);
            service.AttachContract(ref flow, "contract:1", SettlementDeliveryMode.PhysicalSubmission, 2);
            service.SetStation(flow, "wage1", 3);
            service.HoldAt(flow, FlowHold.Paused, "Drafted", "Resume", LaborFlowStage.GoingToSettlement, 4, 250);
            service.RecordEvent(flow, "payment:o1", "Paid", 5, "e1", "s1", 28, 840, "Finalized", "WoodLog");
            service.RegisterBatch("contract:1", "e1", "p1", "WoodLog", "employer", 28, 20, 7, "cell", out var batch);
            service.TransferBatchIdentity(batch.BatchId, 7, 8, 10, "job1", 30, "pawn:p1");
            var restored = new LaborFlowService(RoundTrip(data));
            var current = restored.ReadWorkflowById(flow.WorkflowId);
            Assert.That(current.CurrentStationKey, Is.EqualTo("wage1"));
            Assert.That(current.Hold, Is.EqualTo(FlowHold.Paused));
            Assert.That(current.ResumeStage, Is.EqualTo(LaborFlowStage.GoingToSettlement));
            Assert.That(current.ReasonCode, Is.EqualTo("Drafted"));
            Assert.That(current.NextRetryTick, Is.EqualTo(250));
            var history = restored.ReadHistory(flow.WorkflowId);
            var payment = history[history.Count - 1];
            Assert.That(payment.EventId, Is.EqualTo("payment:o1"));
            Assert.That(payment.AmountMinor, Is.EqualTo(840));
            Assert.That(payment.Quantity, Is.EqualTo(28));
            Assert.That(payment.ProductDefName, Is.EqualTo("WoodLog"));
            Assert.That(payment.CurrentStationKey, Is.EqualTo("wage1"));
            Assert.That(restored.RecordEvent(current, "payment:o1", "Paid", 999, "e1", "s1", 28, 840), Is.EqualTo(LaborFlowCode.Replay));
            var batches = restored.ReadBatchesFor("contract:1");
            Assert.That(batches.Count, Is.EqualTo(2));
            Assert.That(batches[0].ProducedQuantity + batches[1].ProducedQuantity, Is.EqualTo(28));
            Assert.That(batches[1].ThingIdNumber, Is.EqualTo(8));
            Assert.That(restored.TransferBatchIdentity(batch.BatchId, 7, 8, 10, "job1", 999, "pawn:p1"), Is.EqualTo(LaborFlowCode.Replay));
        }

        // T24：流程 + 提交 + 批次跨读档守恒（阶段 / 代次 / 数量 / 身份全部原样）。
        [Test]
        public void RoundTripPreservesWorkflowSubmissionAndBatch()
        {
            var data = new LaborFlowSaveData();
            var service = new LaborFlowService(data);
            service.BeginVisit("p1", "vanilla:Mining:a1", "a1", SettlementDeliveryMode.PhysicalSubmission, 100, out var flow);
            service.AttachContract(ref flow, "contract:1", SettlementDeliveryMode.PhysicalSubmission, 110);
            service.MoveTo(flow, LaborFlowStage.GoingToLaborOffice, "GotoLaborOffice", 120);
            service.EnsureDocument(ref flow, false, 130, out int generation, out string instanceId);
            service.RegisterBatch("contract:1", "evidence:1", "p1", "WoodLog", "a1", 28, 200, 7, "cell:5,5", out var batch);
            // 批次接收的**唯一写点**是 AcceptSubmission（交验提交）—— 调用方不预先核销，否则会重复计入。
            service.AcceptSubmission("contract:1", "p1", "s1", new[] { "evidence:1" },
                new[] { batch.BatchId }, 10, "receipt:1", 300);

            var loaded = RoundTrip(data);

            Assert.That(loaded.SchemaVersion, Is.EqualTo(LaborFlowSaveData.CurrentSchemaVersion));
            Assert.That(loaded.PayloadIntact, Is.True);
            var restored = new LaborFlowService(loaded);
            var reloaded = restored.ReadWorkflowFor("p1", "vanilla:Mining:a1");
            Assert.That(reloaded, Is.Not.Null, "流程行必须跨读档存在");
            Assert.That(reloaded.Stage, Is.EqualTo(LaborFlowStage.GoingToLaborOffice));
            Assert.That(reloaded.ContractId, Is.EqualTo("contract:1"));
            Assert.That(reloaded.DocumentGeneration, Is.EqualTo(generation));
            Assert.That(reloaded.DocumentInstanceId, Is.EqualTo(instanceId));
            Assert.That(reloaded.Mode, Is.EqualTo(SettlementDeliveryMode.PhysicalSubmission));

            var restoredBatch = restored.ReadBatchesFor("contract:1");
            Assert.That(restoredBatch.Count, Is.EqualTo(1));
            Assert.That(restoredBatch[0].ProducedQuantity, Is.EqualTo(28));
            Assert.That(restoredBatch[0].AcceptedQuantity, Is.EqualTo(10), "已接收量必须跨读档保留（否则会重复取货）");
            Assert.That(restoredBatch[0].RemainingQuantity, Is.EqualTo(18));
            Assert.That(restoredBatch[0].ThingIdNumber, Is.EqualTo(7));

            var submission = restored.PaymentGrant("contract:1");
            Assert.That(submission, Is.Not.Null, "提交回执必须跨读档保留（否则会重复交货）");
            Assert.That(submission.AcceptedQuantity, Is.EqualTo(10));
            // 多趟交验（CA-02）后回执键含**累计接收量**（由已落盘回执派生 ⇒ 跨读档可复算、同趟重放同键）。
            Assert.That(submission.SubmissionId,
                Is.EqualTo(SubmissionRecord.IdFor("contract:1", generation, "evidence:1", 10)));
            Assert.That(restored.IsDeliveryCovered("contract:1", "evidence:1"), Is.False,
                "只交了一部分 ⇒ 未覆盖完成（不得据此付款）");
        }

        // T25：旧档**没有该节点** ⇒ 空载体（"从未有过在办流程"），一切既有权利不受影响。
        [Test]
        public void MissingNodeLoadsAsEmptyCarrier()
        {
            File.WriteAllText(path, "<?xml version=\"1.0\"?><savegame />");
            LaborFlowSaveData data = null;
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref data, "pawnEconomicLaborFlow");
            Scribe.loader.FinalizeLoading();
            data = data ?? new LaborFlowSaveData();
            Assert.That(data.WorkflowCount, Is.EqualTo(0));
            Assert.That(data.PayloadIntact, Is.True, "旧档缺节点是**空载体**而非损坏载荷");
            Assert.That(data.CanWrite, Is.True, "空载体可正常写入（新流程照常工作）");
        }

        // T26：**未知（更高）schemaVersion** ⇒ 保留原始载荷并阻断写入，绝不覆写、不截断。
        [Test]
        public void UnknownSchemaVersionIsPreservedAndBlocked()
        {
            var future = new LaborFlowSaveData();
            typeof(LaborFlowSaveData).GetField("schemaVersion", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(future, LaborFlowSaveData.CurrentSchemaVersion + 5);
            var service = new LaborFlowService(future);
            service.BeginVisit("p_new", "vanilla:Mining:a1", "a1", SettlementDeliveryMode.PhysicalSubmission, 100, out _);

            var loaded = RoundTrip(future);

            Assert.That(loaded.SchemaVersion, Is.EqualTo(LaborFlowSaveData.CurrentSchemaVersion + 5), "版本号原样保留");
            Assert.That(loaded.PayloadIntact, Is.False, "陌生版本 ⇒ 载荷不可信");
            Assert.That(loaded.CanWrite, Is.False, "陌生版本 ⇒ 一切写入被阻断");
            var blocked = new LaborFlowService(loaded);
            Assert.That(blocked.BeginVisit("p2", "vanilla:Mining:a1", "a1",
                SettlementDeliveryMode.PhysicalSubmission, 200, out _), Is.EqualTo(LaborFlowCode.Unknown));
            Assert.That(loaded.WorkflowCount, Is.EqualTo(1), "既有行未被裁剪也未被覆写");
        }

        // 行级 fail-closed：无法识别的枚举字符串 ⇒ 该行被忽略（按"无流程"处理），**绝不猜测**推进。
        [Test]
        public void RowWithUnrecognizedStageIsIgnoredNotGuessed()
        {
            var data = new LaborFlowSaveData();
            var service = new LaborFlowService(data);
            service.BeginVisit("p1", "vanilla:Mining:a1", "a1", SettlementDeliveryMode.PhysicalSubmission, 100, out _);
            var workflow = data.ReadWorkflowFor("p1", "vanilla:Mining:a1");
            var row = typeof(LaborFlowSaveData)
                .GetField("workflowIndex", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(data)
                as System.Collections.IDictionary;
            Assert.That(row, Is.Not.Null);
            var stored = row[workflow.WorkflowId];
            stored.GetType().GetField("Stage").SetValue(stored, "NotAStage");
            var ignored = data.ReadWorkflowFor("p1", "vanilla:Mining:a1");
            Assert.That(ignored, Is.Null, "无法识别的阶段 ⇒ 该行不可信，按无流程处理（不猜一个阶段继续推进）");
        }
    }
}

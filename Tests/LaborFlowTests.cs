using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Employment;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// CHG-20261011-LABOR-VISUAL · **领域规则**（CB-ECON-003 §10.2 的纯规则部分）。
    /// 覆盖：模式/资格的 fail-closed（T30/T25/T26）、付款门（T08）、阶段不可跳（T05/REQ-018）、
    /// 批次数量守恒（T11/T12）、确定性身份（T04/T17/T24）、能力矩阵显式（§6.1）。
    /// </summary>
    [TestFixture]
    public sealed class LaborFlowRulesTests
    {
        // ── T30/T25/T26：存储解析必须 fail-closed ──────────────────────────────────────────────

        [Test]
        public void ModeParseIsFailClosed()
        {
            // 缺键（旧档）⇒ 旧模式：既有催付行为分毫不变。
            Assert.That(LaborFlowRules.ParseMode(null, SettlementDeliveryMode.LegacyImmediate),
                Is.EqualTo(SettlementDeliveryMode.LegacyImmediate));
            Assert.That(LaborFlowRules.ParseMode("   ", SettlementDeliveryMode.LegacyImmediate),
                Is.EqualTo(SettlementDeliveryMode.LegacyImmediate));
            // 可识别值 ⇒ 原样。
            Assert.That(LaborFlowRules.ParseMode("PhysicalSubmission", SettlementDeliveryMode.LegacyImmediate),
                Is.EqualTo(SettlementDeliveryMode.PhysicalSubmission));
            // **无法识别的字符串绝不降级为可付款**（拼写错误 / 未来版本写回 / 外部编辑）。
            Assert.That(LaborFlowRules.ParseMode("physicalsubmission", SettlementDeliveryMode.LegacyImmediate),
                Is.EqualTo(SettlementDeliveryMode.Unknown));
            Assert.That(LaborFlowRules.ParseMode("FutureMode9", SettlementDeliveryMode.LegacyImmediate),
                Is.EqualTo(SettlementDeliveryMode.Unknown));
        }

        [Test]
        public void EligibilityParseIsFailClosed()
        {
            Assert.That(LaborFlowRules.ParseEligibility(null, WageEligibility.Immediate),
                Is.EqualTo(WageEligibility.Immediate));      // 旧应付 = 可付款（权利不变）
            Assert.That(LaborFlowRules.ParseEligibility("Submitted", WageEligibility.Immediate),
                Is.EqualTo(WageEligibility.Submitted));
            Assert.That(LaborFlowRules.ParseEligibility("whatever", WageEligibility.Immediate),
                Is.EqualTo(WageEligibility.Unknown));        // 未知 ⇒ 不付款
        }

        // ── T08/T18：付款门 = 唯一判据 ────────────────────────────────────────────────────────

        [Test]
        public void PaymentGateBlocksUnsubmittedAndUnknown()
        {
            Assert.That(LaborFlowRules.PaymentAllowed(WageEligibility.Immediate), Is.True, "旧模式必须可付（零回归）");
            Assert.That(LaborFlowRules.PaymentAllowed(WageEligibility.Submitted), Is.True, "已交验核验 ⇒ 可付");
            Assert.That(LaborFlowRules.PaymentAllowed(WageEligibility.PendingSubmission), Is.False, "模式=交验后付款，未交验不得付款");
            Assert.That(LaborFlowRules.PaymentAllowed(WageEligibility.Unknown), Is.False, "未知资格绝不付款");
            Assert.That(LaborFlowRules.PaymentAllowed(WageEligibility.Blocked), Is.False, "被阻断绝不付款");
        }

        [Test]
        public void EvidenceEligibilityFollowsFrozenMode()
        {
            Assert.That(LaborFlowRules.EligibilityOnEvidence(SettlementDeliveryMode.PhysicalSubmission),
                Is.EqualTo(WageEligibility.PendingSubmission));
            Assert.That(LaborFlowRules.EligibilityOnEvidence(SettlementDeliveryMode.LegacyImmediate),
                Is.EqualTo(WageEligibility.Immediate));
            // 不可信模式 ⇒ 未知资格（调用方据此拒绝创建应付，绝不猜）。
            Assert.That(LaborFlowRules.EligibilityOnEvidence(SettlementDeliveryMode.Unknown),
                Is.EqualTo(WageEligibility.Unknown));
        }

        // ── T05/REQ-018：阶段只能前进一步（禁跳阶 = 禁"假流程"） ────────────────────────────────

        [Test]
        public void StageAdvancesOneStepOnly()
        {
            Assert.That(LaborFlowRules.IsStageForward(LaborFlowStage.IntentReady, LaborFlowStage.GoingToLaborOffice), Is.True);
            Assert.That(LaborFlowRules.IsStageForward(LaborFlowStage.IntentReady, LaborFlowStage.IntentReady), Is.True, "重放合法");
            Assert.That(LaborFlowRules.IsStageForward(LaborFlowStage.IntentReady, LaborFlowStage.Working), Is.False,
                "从「准备出发」直接跳到「履约中」就是远程假流程");
            Assert.That(LaborFlowRules.IsStageForward(LaborFlowStage.Working, LaborFlowStage.IntentReady), Is.False, "不得倒退");
            Assert.That(LaborFlowRules.IsStageForward(LaborFlowStage.Archived, LaborFlowStage.PaymentConfirmed), Is.False, "归档是终态");
            Assert.That(LaborFlowRules.IsStageForward(LaborFlowStage.AwaitingPayment, LaborFlowStage.Archived), Is.True);
        }

        // ── ARCH P1-3 修复：真实 Job 跨过过程性阶段时**逐阶补记**（每一步都合法，且目标可达） ──────

        [Test]
        public void PathToWalksEveryIntermediateStageLegally()
        {
            // 无产品劳务的实况路径：持约履约 → … → 已交付待发薪（没有"收集产物"这一步的真实事实）。
            var path = LaborFlowRules.PathTo(LaborFlowStage.Working, LaborFlowStage.AwaitingPayment);
            Assert.That(path, Is.EqualTo(new[]
            {
                LaborFlowStage.CollectingOutputs, LaborFlowStage.GoingToSettlement,
                LaborFlowStage.VerifyingSubmission, LaborFlowStage.AwaitingPayment
            }));
            // 每一步都在规则层合法（逐阶 = 不跳阶，故不会造出"远程假流程"）。
            var previous = LaborFlowStage.Working;
            foreach (var stage in path)
            {
                Assert.That(LaborFlowRules.IsStageForward(previous, stage), Is.True, previous + "→" + stage);
                previous = stage;
            }
            // 到账与归档同样可达（避免"流程永久停在履约中"的结构性停滞）。
            Assert.That(LaborFlowRules.PathTo(LaborFlowStage.AwaitingPayment, LaborFlowStage.PaymentConfirmed).Count, Is.EqualTo(1));
            Assert.That(LaborFlowRules.PathTo(LaborFlowStage.PaymentConfirmed, LaborFlowStage.Archived).Count, Is.EqualTo(1));
            // 已到达 / 目标在身后 / 已归档 ⇒ 空路径（不倒退）。
            Assert.That(LaborFlowRules.PathTo(LaborFlowStage.Working, LaborFlowStage.Working), Is.Empty);
            Assert.That(LaborFlowRules.PathTo(LaborFlowStage.Working, LaborFlowStage.IntentReady), Is.Empty);
            Assert.That(LaborFlowRules.PathTo(LaborFlowStage.Archived, LaborFlowStage.PaymentConfirmed), Is.Empty);
        }

        // ── T11/T12：批次数量守恒（Produced = Remaining + Accepted + Lost） ────────────────────

        [Test]
        public void BatchQuantityIsConserved()
        {
            var batch = new ProductBatch("batch:1", "c1", "e1", "p1", "WoodLog", "c1", 28, 0, 0, 100, 7, "cell:1,1");
            Assert.That(batch.RemainingQuantity, Is.EqualTo(28));
            var half = batch.Receive(10);
            Assert.That(half.AcceptedQuantity, Is.EqualTo(10));
            Assert.That(half.RemainingQuantity, Is.EqualTo(18), "守恒式在每次接收后仍然成立");
            Assert.That(half.ProducedQuantity, Is.EqualTo(28));
            var rest = half.Receive(18);
            Assert.That(rest.RemainingQuantity, Is.EqualTo(0));
            Assert.That(rest.IsSettled, Is.True);
        }

        [Test]
        public void ProductBatchRejectsOverIssue()
        {
            var batch = new ProductBatch("batch:1", "c1", "e1", "p1", "WoodLog", "c1", 5, 0, 0, 100, 7, null);
            Assert.Throws<ArgumentOutOfRangeException>(() => batch.Receive(6), "超量接收必须抛（不得静默截断）");
            Assert.Throws<ArgumentOutOfRangeException>(() => batch.Receive(0));
            var lost = batch.Wrote(true, 5);
            Assert.That(lost.LostQuantity, Is.EqualTo(5));
            Assert.That(lost.RemainingQuantity, Is.EqualTo(0));
        }

        [Test]
        public void BatchRejectsNonPositiveProduced()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProductBatch("b", "c", "e", "p", "WoodLog", null, 0, 0, 0, 1, 1, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProductBatch("b", "c", "e", "p", "WoodLog", null, 3, 2, 2, 1, 1, null));
        }

        // ── T04/T16/T24/T17：身份必须**确定性派生**（禁 Guid，否则重放会复制物品/重复付款） ───────

        [Test]
        public void IdentitiesAreDeterministic()
        {
            Assert.That(LaborWorkflow.IdFor("p1", "vanilla:Mining:a1"), Is.EqualTo(LaborWorkflow.IdFor("p1", "vanilla:Mining:a1")));
            Assert.That(LaborWorkflow.IdFor("p1", "vanilla:Mining:a1"), Is.Not.EqualTo(LaborWorkflow.IdFor("p2", "vanilla:Mining:a1")));
            Assert.That(ProductBatch.IdFor("c1", "e1", "WoodLog"), Is.EqualTo(ProductBatch.IdFor("c1", "e1", "WoodLog")));
            Assert.That(SubmissionRecord.IdFor("c1", 1, "e1"), Is.EqualTo(SubmissionRecord.IdFor("c1", 1, "e1")));
            Assert.That(SubmissionRecord.IdFor("c1", 1, "e1"), Is.Not.EqualTo(SubmissionRecord.IdFor("c1", 2, "e1")),
                "跨代次的提交身份必须不同（旧纸领款必须被拒）");
            Assert.That(LaborFlowRules.DocumentInstanceIdFor("c1", 1), Is.EqualTo(LaborFlowRules.DocumentInstanceIdFor("c1", 1)));
            Assert.That(LaborFlowRules.DocumentInstanceIdFor("c1", 1), Is.Not.EqualTo(LaborFlowRules.DocumentInstanceIdFor("c1", 2)));
        }

        [Test]
        public void DocumentRejectsNonPositiveGeneration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LaborWorkflow("w", "c", "p", "i",
                SettlementDeliveryMode.PhysicalSubmission, LaborFlowStage.Working, FlowHold.None, LaborFlowStage.None,
                null, null, 0, 0, -1, null));
        }

        // ── §6.1：能力矩阵必须显式声明（未列出的族 = Unsupported，绝不允许新签无法履行交付的合同） ──

        [Test]
        public void CapabilityMatrixIsExplicitAndFailClosed()
        {
            Assert.That(LaborFlowRules.CapabilityFor("ChopWood"), Is.EqualTo(DeliveryCapability.CarryToCounter));
            Assert.That(LaborFlowRules.CapabilityFor("Mine"), Is.EqualTo(DeliveryCapability.CarryToCounter));
            Assert.That(LaborFlowRules.CapabilityFor("Harvest"), Is.EqualTo(DeliveryCapability.CarryToCounter));
            Assert.That(LaborFlowRules.CapabilityFor("CutPlants"), Is.EqualTo(DeliveryCapability.EvidenceOnly));
            Assert.That(LaborFlowRules.CapabilityFor("Tame"), Is.EqualTo(DeliveryCapability.EvidenceOnly));
            // CA-03（2026-10-12 返修）：Hunt / Slaughter 修前标 AlreadyAccepted，但**没有权威接收回执
            // 来源** ⇒ 交验时凭空拼一个 `accepted:…` 当凭据（等同于伪造"雇主已收"）。按需求 §6.1
            // 「没有 CaptureWired 成功证据的能力不得承诺实体模式可结算」如实降级为 Unsupported。
            Assert.That(LaborFlowRules.CapabilityFor("Hunt"), Is.EqualTo(DeliveryCapability.Unsupported),
                "无权威接收回执来源的族不得承诺实体交验（CA-03）");
            Assert.That(LaborFlowRules.CapabilityFor("Slaughter"), Is.EqualTo(DeliveryCapability.Unsupported));
            // CHG-20261012-ECON-HAULCMD（CA-03 **部分撤销**）：Haul 的权威接收回执来源已由
            // Capture/Patch_VanillaHaulWork 落地（真实放置回调校验储存区 AllowedToAccept；容器按实物减少量），
            // 经 ReportVanillaOrderCompletion 入账 ⇒ 凭据由**真实交付**产生，不含任何伪造字符串；
            // 且物品已真实入库，不应再让 Pawn 搬到柜台（CarryToCounter 是错的）⇒ EvidenceOnly。
            Assert.That(LaborFlowRules.CapabilityFor("Haul"), Is.EqualTo(DeliveryCapability.EvidenceOnly),
                "搬运已有真实送达证据来源 ⇒ EvidenceOnly（Hunt / Slaughter 回执来源仍不存在，保持 Unsupported）");
            // 未声明族（含第三方追加）⇒ Unsupported（fail-closed）。
            Assert.That(LaborFlowRules.CapabilityFor("SomeThirdPartyOrder"), Is.EqualTo(DeliveryCapability.Unsupported));
            Assert.That(LaborFlowRules.CapabilityFor(null), Is.EqualTo(DeliveryCapability.Unsupported));
        }

        // ── 设计默认参数的**来源单一**（§4.4；改一处即全链生效） ─────────────────────────────

        [Test]
        public void DesignDefaultsAreSourced()
        {
            Assert.That(LaborFlowRules.NegotiateTicks, Is.GreaterThan(0));
            Assert.That(LaborFlowRules.RetryBackoffTicks, Is.EqualTo(new[] { 250, 500, 1000, 2000 }));
            Assert.That(LaborFlowRules.BackoffTicks(0), Is.EqualTo(0));
            Assert.That(LaborFlowRules.BackoffTicks(1), Is.EqualTo(250));
            Assert.That(LaborFlowRules.BackoffTicks(99), Is.EqualTo(2000), "退避封顶，不无界增长");
        }

        [Test]
        public void ShortCodeNeverThrowsOnOddIds()
        {
            Assert.That(LaborFlowRules.ShortCode(null), Is.EqualTo(string.Empty));
            Assert.That(LaborFlowRules.ShortCode("abc"), Is.EqualTo("abc"));
            Assert.That(LaborFlowRules.ShortCode("contract:a|b|c|0123456789abcdef").Length, Is.LessThanOrEqualTo(8));
        }
    }

    /// <summary>
    /// CHG-20261011-LABOR-VISUAL · **应用服务**（阶段推进 / 代次 / 批次登记 / 交验 idempotence）。
    /// 对应 §10.2 的 T01/T04/T08/T11/T12/T14/T16/T24/T26（离线可断言部分）。
    /// </summary>
    [TestFixture]
    public sealed class LaborFlowServiceTests
    {
        private LaborFlowSaveData data;
        private LaborFlowService service;

        [SetUp] public void Setup() { data = new LaborFlowSaveData(); service = new LaborFlowService(data); }

        private static LaborWorkflow BeginIntent(LaborFlowService target, string pawn = "p1",
            string opportunity = "vanilla:Mining:a1", string anchor = "a1", int now = 100)
        {
            var code = target.BeginVisit(pawn, opportunity, anchor, SettlementDeliveryMode.PhysicalSubmission, now, out var flow);
            Assert.That(code, Is.AnyOf(LaborFlowCode.Applied, LaborFlowCode.Replay));
            return flow;
        }

        // T01：意图**不产生合同**，只产生"准备到劳务站"。REQ-002 的"不存在远程先签后走"以此为基线。
        [Test]
        public void IntentCreatesNoContract()
        {
            var flow = BeginIntent(service);
            Assert.That(flow.Stage, Is.EqualTo(LaborFlowStage.IntentReady));
            Assert.That(flow.ContractId, Is.Empty, "意图阶段没有任何合同身份");
            Assert.That(flow.OpportunityKey, Is.EqualTo("vanilla:Mining:a1"));
            Assert.That(flow.DocumentGeneration, Is.EqualTo(0), "未签约就没有合同纸");
            Assert.That(service.WorkflowCount, Is.EqualTo(1));
        }

        // T04：重建意图是幂等的，且**不倒回**已推进的阶段。
        [Test]
        public void BeginVisitIsIdempotentAndNeverRewinds()
        {
            var flow = BeginIntent(service);
            Assert.That(service.MoveTo(flow, LaborFlowStage.GoingToLaborOffice, "GotoLaborOffice", 110), Is.EqualTo(LaborFlowCode.Applied));
            var moved = service.ReadWorkflowFor("p1", "vanilla:Mining:a1");
            Assert.That(service.BeginVisit("p1", "vanilla:Mining:a1", "a1", SettlementDeliveryMode.PhysicalSubmission, 120, out var again),
                Is.EqualTo(LaborFlowCode.Replay));
            Assert.That(again.Stage, Is.EqualTo(LaborFlowStage.GoingToLaborOffice), "重放不得把阶段倒回 IntentReady");
            Assert.That(moved.Stage, Is.EqualTo(LaborFlowStage.GoingToLaborOffice));
        }

        [Test]
        public void MoveToRejectsSkippingAndBackwards()
        {
            var flow = BeginIntent(service);
            Assert.That(service.MoveTo(flow, LaborFlowStage.Working, "Work", 110), Is.EqualTo(LaborFlowCode.Blocked), "禁跳阶");
            Assert.That(service.ReadWorkflowFor("p1", "vanilla:Mining:a1").Stage, Is.EqualTo(LaborFlowStage.IntentReady), "被拒时不写");
        }

        // T04：SignCommit 写入合同身份；**不得换绑**第二份合同（T02 的"一人一约"在流程侧同样成立）。
        [Test]
        public void AttachContractIsSingleBinding()
        {
            var flow = BeginIntent(service);
            Assert.That(service.AttachContract(ref flow, "contract:1", SettlementDeliveryMode.PhysicalSubmission, 120),
                Is.EqualTo(LaborFlowCode.Applied));
            Assert.That(flow.ContractId, Is.EqualTo("contract:1"));
            Assert.That(service.AttachContract(ref flow, "contract:1", SettlementDeliveryMode.PhysicalSubmission, 130),
                Is.EqualTo(LaborFlowCode.Replay), "同一合同重复绑定 = 重放");
            Assert.That(service.AttachContract(ref flow, "contract:2", SettlementDeliveryMode.PhysicalSubmission, 140),
                Is.EqualTo(LaborFlowCode.Blocked), "已绑定别的合同 ⇒ 拒绝换绑");
            Assert.That(service.ReadWorkflowFor("p1", "vanilla:Mining:a1").ContractId, Is.EqualTo("contract:1"));
        }

        // T14：合同纸幂等 —— 首次签发 g1，重放不重印。
        [Test]
        public void EnsureDocumentIssuesOnceThenReplays()
        {
            var flow = BeginIntent(service);
            service.AttachContract(ref flow, "contract:1", SettlementDeliveryMode.PhysicalSubmission, 120);
            Assert.That(service.EnsureDocument(ref flow, false, 130, out int generation, out string instanceId),
                Is.EqualTo(LaborFlowCode.Applied));
            Assert.That(generation, Is.EqualTo(1));
            Assert.That(instanceId, Is.EqualTo(LaborFlowRules.DocumentInstanceIdFor("contract:1", 1)));
            Assert.That(service.EnsureDocument(ref flow, false, 140, out int again, out string sameInstance),
                Is.EqualTo(LaborFlowCode.Replay), "同代次重复签发 = 重放，不重印");
            Assert.That(again, Is.EqualTo(1));
            Assert.That(sameInstance, Is.EqualTo(instanceId));
        }

        // T16：补发递增代次并使旧纸失效（旧纸领款必须被拒）。
        [Test]
        public void ReissueIncrementsGeneration()
        {
            var flow = BeginIntent(service);
            service.AttachContract(ref flow, "contract:1", SettlementDeliveryMode.PhysicalSubmission, 120);
            service.EnsureDocument(ref flow, false, 130, out _, out string first);
            Assert.That(service.EnsureDocument(ref flow, true, 140, out int generation, out string second),
                Is.EqualTo(LaborFlowCode.Applied));
            Assert.That(generation, Is.EqualTo(2));
            Assert.That(second, Is.Not.EqualTo(first), "补发必须换发新实例 ID");
        }

        // T24/T26：未知 schema ⇒ **所有写入被阻断**，且既有载荷不被改写。
        [Test]
        public void UnknownSchemaBlocksWrites()
        {
            var future = new LaborFlowSaveData();
            typeof(LaborFlowSaveData).GetField("schemaVersion", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(future, LaborFlowSaveData.CurrentSchemaVersion + 7);
            typeof(LaborFlowSaveData).GetField("payloadIntact", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(future, false);
            var blocked = new LaborFlowService(future);
            Assert.That(blocked.PayloadIntact, Is.False);
            Assert.That(blocked.BeginVisit("p1", "vanilla:Mining:a1", "a1",
                SettlementDeliveryMode.PhysicalSubmission, 100, out _), Is.EqualTo(LaborFlowCode.Unknown));
            Assert.That(future.WorkflowCount, Is.EqualTo(0), "载荷不可信时不得写入");
        }

        // T12（CA-02/CA-04 返修）：多趟交验 —— 每趟真实接收各写一条回执（**累计接收量作键**），
        // 数量守恒、不会重复接收同一批货、也不会吞掉前一趟的核销；覆盖完成前不放款。
        [Test]
        public void AcceptSubmissionIsKeyedByCumulativeReceivedAndConservesQuantity()
        {
            var flow = BeginIntent(service);
            service.AttachContract(ref flow, "contract:1", SettlementDeliveryMode.PhysicalSubmission, 120);
            service.RegisterBatch("contract:1", "evidence:1", "p1", "WoodLog", "a1", 28, 200, 5, "cell:1,1", out var batch);
            // 第一趟：10 件（键含累计 10）
            var first = service.AcceptSubmission("contract:1", "p1", "s1", new[] { "evidence:1" },
                new[] { batch.BatchId }, 10, "receipt:1", 300);
            Assert.That(first.Code, Is.EqualTo(LaborFlowCode.Applied));
            Assert.That(service.ReadBatchesFor("contract:1")[0].AcceptedQuantity, Is.EqualTo(10));
            Assert.That(service.PaymentGrant("contract:1").SubmissionId,
                Is.EqualTo(SubmissionRecord.IdFor("contract:1", 0, "evidence:1", 10)), "回执键 = 累计接收量");
            Assert.That(service.IsDeliveryCovered("contract:1", "evidence:1"), Is.False,
                "只交了一部分 ⇒ **不算覆盖完成**（不得拿部分回执放款）");
            // 第二趟：余下 18 件 —— 另一条回执，不撞键、不吞第一趟的核销。
            var second = service.AcceptSubmission("contract:1", "p1", "s1", new[] { "evidence:1" },
                new[] { batch.BatchId }, 18, "receipt:1", 400);
            Assert.That(second.Code, Is.EqualTo(LaborFlowCode.Applied));
            Assert.That(service.ReadBatchesFor("contract:1")[0].AcceptedQuantity, Is.EqualTo(28), "两趟合计 = 产量（守恒）");
            Assert.That(service.ReadBatchesFor("contract:1")[0].RemainingQuantity, Is.EqualTo(0));
            Assert.That(service.IsDeliveryCovered("contract:1", "evidence:1"), Is.True, "覆盖完成 ⇒ 可授予付款资格");
            // 第三趟：再声称 18 件 ⇒ 批次已无剩余 ⇒ **预检即 Unknown 且零写入**（CA-04：不得留下孤立核销）。
            var third = service.AcceptSubmission("contract:1", "p1", "s1", new[] { "evidence:1" },
                new[] { batch.BatchId }, 18, "receipt:1", 500);
            Assert.That(third.Code, Is.EqualTo(LaborFlowCode.Unknown));
            Assert.That(service.ReadBatchesFor("contract:1")[0].AcceptedQuantity, Is.EqualTo(28), "拒收不得改动既有核销");
        }

        // CA-10：对账处置的**真实入口** —— ① 事实查清 ⇒ 清 Hold 继续办理；② 剩余确实无法交付 ⇒ 按损失核销
        // 剩余量（覆盖完成，随后仍走既有付款门）。两者**都不改金额/债权**，只改流程状态与批次核销。
        [Test]
        public void ReconciliationCanBeResolvedAndNeverInventsFacts()
        {
            var flow = BeginIntent(service);
            service.AttachContract(ref flow, "contract:1", SettlementDeliveryMode.PhysicalSubmission, 120);
            service.RegisterBatch("contract:1", "evidence:1", "p1", "WoodLog", "a1", 12, 200, 5, "cell:1,1", out _);
            service.HoldAt(flow, FlowHold.ReconciliationRequired, "SubmissionUnknown", "Diagnose", flow.Stage, 200, 0);
            var held = service.ReadWorkflowFor("p1", "vanilla:Mining:a1");
            Assert.That(held.Hold, Is.EqualTo(FlowHold.ReconciliationRequired));
            // ② 剩余经核验无法交付 ⇒ 按损失核销剩余量
            Assert.That(service.ResolveReconciliation(held.WorkflowId, ReconciliationAction.WriteOffRemaining, 300, out string reason),
                Is.EqualTo(LaborFlowCode.Applied));
            Assert.That(reason, Is.Null, "确有剩余被核销时不带任何原因码");
            Assert.That(service.ReadBatchesFor("contract:1")[0].RemainingQuantity, Is.EqualTo(0), "损失核销后批次不再缺量");
            Assert.That(service.IsDeliveryCovered("contract:1", "evidence:1"), Is.True,
                "按损失核销 ⇒ 覆盖完成（但付款仍由既有付款门裁决，本方法不付款）");
            Assert.That(service.ReadWorkflowFor("p1", "vanilla:Mining:a1").Hold, Is.EqualTo(FlowHold.None),
                "处置后必须清 Hold，否则流程永远卡死");
            // ① 再处置 ⇒ Replay（没有对账可处置，不重复核销）
            Assert.That(service.ResolveReconciliation(held.WorkflowId, ReconciliationAction.Resume, 310, out string again),
                Is.EqualTo(LaborFlowCode.Replay));
            Assert.That(again, Is.EqualTo("NoReconciliation"));
            // 未知流程 ⇒ Unknown（不猜、不写）
            Assert.That(service.ResolveReconciliation("flow:missing|o", ReconciliationAction.Resume, 320, out string unknown),
                Is.EqualTo(LaborFlowCode.Unknown));
            Assert.That(unknown, Is.EqualTo("WorkflowUnknown"));
        }

        // CA-16：**新写入先限额拒绝**（不为了塞进新行而丢掉既有未结权利）。
        [Test]
        public void WriteIsRefusedAtLimitInsteadOfDroppingExistingRows()
        {
            int limit = LaborFlowSaveData.MaximumWorkflows;
            for (int i = 0; i < limit; i++)
                Assert.That(service.BeginVisit("p1", "opp:" + i, "a1", SettlementDeliveryMode.PhysicalSubmission, 100 + i, out _),
                    Is.EqualTo(LaborFlowCode.Applied), "前 " + limit + " 条必须正常写入");
            Assert.That(service.WorkflowIds().Count, Is.EqualTo(limit));
            Assert.That(service.BeginVisit("p1", "opp:overflow", "a1", SettlementDeliveryMode.PhysicalSubmission, 99999, out _),
                Is.EqualTo(LaborFlowCode.Blocked), "达到上限 ⇒ 如实阻断新写入");
            Assert.That(service.WorkflowIds().Count, Is.EqualTo(limit), "阻断新写入时**不得**丢弃既有行");
        }

        // CA-09：归档行不得复用/重置 —— 二次求职必须**开新轮**，否则永远领不到办理 Job（实机复现的死锁）。
        [Test]
        public void ArchivedRoundOpensANewRoundInsteadOfReusingTheDeadRow()
        {
            var flow = BeginIntent(service);                               // 轮 1：flow:p1|vanilla:Mining:a1
            service.AttachContract(ref flow, "contract:1", SettlementDeliveryMode.PhysicalSubmission, 120);
            service.MoveTo(flow, LaborFlowStage.Archived, "Archived", 200);
            Assert.That(service.BeginVisit("p1", "vanilla:Mining:a1", "a1",
                SettlementDeliveryMode.PhysicalSubmission, 300, out var second), Is.EqualTo(LaborFlowCode.Applied));
            Assert.That(second.WorkflowId, Is.EqualTo(LaborWorkflow.IdFor("p1", "vanilla:Mining:a1", 2)));
            Assert.That(second.ContractId, Is.EqualTo(string.Empty), "新轮不继承旧合同身份");
            Assert.That(second.Stage, Is.EqualTo(LaborFlowStage.IntentReady));
            Assert.That(service.ReadWorkflowFor("p1", "vanilla:Mining:a1").WorkflowId, Is.EqualTo(second.WorkflowId),
                "当前轮 = 最高轮");
            Assert.That(service.ReadWorkflowById(LaborWorkflow.IdFor("p1", "vanilla:Mining:a1")).ContractId,
                Is.EqualTo("contract:1"), "旧轮原样保留（历史与权利不重置）");
            Assert.That(service.BeginVisit("p1", "vanilla:Mining:a1", "a1",
                SettlementDeliveryMode.PhysicalSubmission, 310, out var again), Is.EqualTo(LaborFlowCode.Replay),
                "同轮幂等：不得开出轮 3");
            Assert.That(again.WorkflowId, Is.EqualTo(second.WorkflowId));
        }

        // 轮号只从 ID 后缀解析（单一权威）；非法形态 fail-closed（不猜轮号 ⇒ 调用方按"无流程行"处理）。
        [Test]
        public void RoundSuffixIsAuthoritativeAndFailClosed()
        {
            Assert.That(LaborWorkflow.RoundOf(LaborWorkflow.IdFor("p1", "o1")), Is.EqualTo(1), "无后缀 = 轮 1");
            Assert.That(LaborWorkflow.IdFor("p1", "o1", 1), Is.EqualTo(LaborWorkflow.IdFor("p1", "o1")),
                "轮 1 不写后缀（同一逻辑轮不得有两个 ID）");
            Assert.That(LaborWorkflow.RoundOf(LaborWorkflow.IdFor("p1", "o1", 3)), Is.EqualTo(3));
            Assert.That(LaborWorkflow.RoundOf("flow:p1|o1|r1"), Is.EqualTo(0), "`|r1` 非法");
            Assert.That(LaborWorkflow.RoundOf("flow:p1|o1|rx"), Is.EqualTo(0));
            Assert.That(LaborWorkflow.RoundOf(null), Is.EqualTo(0));
        }

        [Test]
        public void AcceptSubmissionReportsUnknownWhenBatchNotCovered()
        {
            var flow = BeginIntent(service);
            service.AttachContract(ref flow, "contract:1", SettlementDeliveryMode.PhysicalSubmission, 120);
            service.RegisterBatch("contract:1", "evidence:1", "p1", "WoodLog", "a1", 4, 200, 5, null, out _);
            // 声称交了 10 件而批次只有 4 件 ⇒ **Unknown（对账）**，绝不静默按 4 处理或按 10 记账。
            var result = service.AcceptSubmission("contract:1", "p1", "s1", new[] { "evidence:1" },
                new[] { ProductBatch.IdFor("contract:1", "evidence:1", "WoodLog") }, 10, null, 300);
            Assert.That(result.Code, Is.EqualTo(LaborFlowCode.Unknown));
        }

        [Test]
        public void PaymentGrantRequiresNonReconciliationSubmission()
        {
            var flow = BeginIntent(service);
            service.AttachContract(ref flow, "contract:1", SettlementDeliveryMode.PhysicalSubmission, 120);
            Assert.That(service.PaymentGrant("contract:1"), Is.Null, "没有提交就没有付款资格");
            service.AcceptSubmission("contract:1", "p1", "s1", new[] { "evidence:1" }, null, 0, "proof:1", 300);
            Assert.That(service.PaymentGrant("contract:1"), Is.Not.Null, "无产品证明型提交可授予资格");
            service.AcceptSubmission("contract:1", "p1", "s1", new[] { "evidence:1" }, null, 0, "proof:1", 310, reconciliation: true);
            // 对账中的提交**不给**付款资格（须人工裁决）。
            Assert.That(service.PaymentGrant("contract:1"), Is.Not.Null, "既有的非对账提交仍有效");
        }

        [Test]
        public void BatchRegistrationAccumulatesButKeepsSettledParts()
        {
            service.RegisterBatch("c1", "e1", "p1", "WoodLog", "a1", 10, 100, 1, null, out var first);
            Assert.That(first.ProducedQuantity, Is.EqualTo(10));
            var received = first.Receive(4);
            service.WriteBatch(received);
            // 同一批次键再次登记（多趟产出）⇒ 只累加产生量，**不吞掉**已接收量。
            service.RegisterBatch("c1", "e1", "p1", "WoodLog", "a1", 6, 200, 2, null, out var merged);
            Assert.That(merged.ProducedQuantity, Is.EqualTo(16));
            Assert.That(merged.AcceptedQuantity, Is.EqualTo(4), "已核销部分不得被覆盖");
            Assert.That(merged.RemainingQuantity, Is.EqualTo(12));
        }

        // T21/REQ-016：纯意图可撤销，但**已绑定合同**的流程一律拒绝删除（债权与过程事实不可丢）。
        [Test]
        public void IntentRevokeRejectedAfterContractBinding()
        {
            var flow = BeginIntent(service);
            string workflowId = flow.WorkflowId;
            Assert.That(service.RemoveIntentWorkflow(workflowId), Is.True, "未签约的纯意图可撤");
            Assert.That(service.ReadWorkflowFor("p1", "vanilla:Mining:a1"), Is.Null);

            var second = BeginIntent(service);
            service.AttachContract(ref second, "contract:1", SettlementDeliveryMode.PhysicalSubmission, 120);
            Assert.That(service.RemoveIntentWorkflow(second.WorkflowId), Is.False, "已绑定合同的流程不得删除");
            Assert.That(service.ReadWorkflowFor("p1", "vanilla:Mining:a1"), Is.Not.Null);
        }

        [Test]
        public void HoldKeepsResumePointAndDoesNotEraseStage()
        {
            var flow = BeginIntent(service);
            service.MoveTo(flow, LaborFlowStage.GoingToLaborOffice, "GotoLaborOffice", 110);
            var live = service.ReadWorkflowFor("p1", "vanilla:Mining:a1");
            Assert.That(service.HoldAt(live, FlowHold.Blocked, "ProductNotCarried", "BringProducts",
                live.Stage, 120, 130), Is.EqualTo(LaborFlowCode.Applied));
            var held = service.ReadWorkflowFor("p1", "vanilla:Mining:a1");
            Assert.That(held.Hold, Is.EqualTo(FlowHold.Blocked));
            Assert.That(held.Stage, Is.EqualTo(LaborFlowStage.GoingToLaborOffice), "叠加状态不抹掉阶段（可恢复）");
            Assert.That(held.ReasonCode, Is.EqualTo("ProductNotCarried"));
            Assert.That(held.NextAction, Is.EqualTo("BringProducts"));
            Assert.That(held.NextRetryTick, Is.EqualTo(130));
            Assert.That(service.Resume(held, 140), Is.EqualTo(LaborFlowCode.Applied));
            Assert.That(service.ReadWorkflowFor("p1", "vanilla:Mining:a1").Hold, Is.EqualTo(FlowHold.None));
        }
    }
}

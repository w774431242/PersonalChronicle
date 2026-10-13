using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Employment;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// CHG-20261011-LABOR-VISUAL · **付款资格门与承诺→负债守恒**（CB-ECON-003 §5.3 与 §10.2 的
    /// T08 / T11 / T25 / T31）。这些断言的共同主题是：**模式与资格冻结在数据行上**，
    /// 旧档缺键的自然语义分毫不变，而新模式在交验前绝不付款、总占用绝不提前释放。
    /// </summary>
    [TestFixture]
    public sealed class EmploymentLaborGateTests
    {
        private EmploymentSaveData data;
        private EmploymentService service;

        [SetUp] public void Setup()
        {
            data = new EmploymentSaveData();
            service = new EmploymentService(data);
        }

        private WorkContract Sign(string contractId, SettlementDeliveryMode mode, long wage = 1000, int maxJobs = 2, int now = 100)
        {
            var result = service.Sign(new SignRequest
            {
                ContractId = contractId, PawnKey = "p1", InstitutionKey = "a1", PositionKey = "vanilla:Mining:a1",
                WorkTypeDefNames = new List<string> { "Mining" },
                WagePerJobMinor = wage, MaxJobs = maxJobs, Now = now, TermTicks = 60000,
                PositionCapacity = 0, DeliveryMode = mode
            });
            Assert.That(result.Code, Is.EqualTo(EmploymentCode.Applied), contractId);
            return service.ReadContract(contractId);
        }

        private static WorkEvidence Evidence(string contractId, string source, int tick)
            => new WorkEvidence(contractId, source, "VanillaWork", 0, 1, "job", tick);

        // ── T25：旧档缺字段的自然语义 = 旧模式 = 既有付款行为分毫不变 ──────────────────────────────

        [Test]
        public void RowWithoutModeReadsAsLegacyImmediate()
        {
            Assert.That(EmploymentSaveData.ParseDeliveryMode(null), Is.EqualTo(SettlementDeliveryMode.LegacyImmediate));
            Assert.That(EmploymentSaveData.ParseEligibility(null), Is.EqualTo(WageEligibility.Immediate));
            Assert.That(EmploymentSaveData.ParseDeliveryMode("PhysicalSubmission"), Is.EqualTo(SettlementDeliveryMode.PhysicalSubmission));
            Assert.That(EmploymentSaveData.ParseDeliveryMode("garbage"), Is.EqualTo(SettlementDeliveryMode.Unknown));
        }

        [Test]
        public void LegacyContractEvidenceStaysImmediatelyPayable()
        {
            var contract = Sign("c-legacy", SettlementDeliveryMode.LegacyImmediate);
            Assert.That(contract.DeliveryMode, Is.EqualTo(SettlementDeliveryMode.LegacyImmediate));
            var accepted = service.AcceptEvidence(Evidence("c-legacy", "tree:1", 200));
            Assert.That(accepted.Code, Is.EqualTo(EmploymentCode.Applied));
            var obligation = service.ReadObligation(accepted.ObligationId);
            Assert.That(obligation.Eligibility, Is.EqualTo(WageEligibility.Immediate), "旧模式创建即可付");
            Assert.That(LaborFlowRules.PaymentAllowed(obligation.Eligibility), Is.True);
            Assert.That(obligation.EligibleTick, Is.EqualTo(200), "旧模式的资格成立时刻 = 创建时刻");
        }

        // ── T08：新模式在交验前**不得**付款（资格冻结在应付行上） ────────────────────────────────

        [Test]
        public void PhysicalModeEvidenceCreatesPendingObligation()
        {
            var contract = Sign("c-phys", SettlementDeliveryMode.PhysicalSubmission);
            Assert.That(contract.DeliveryMode, Is.EqualTo(SettlementDeliveryMode.PhysicalSubmission));
            var accepted = service.AcceptEvidence(Evidence("c-phys", "tree:2", 200));
            Assert.That(accepted.Code, Is.EqualTo(EmploymentCode.Applied), "证据仍然成立并记账");
            var obligation = service.ReadObligation(accepted.ObligationId);
            Assert.That(obligation.Eligibility, Is.EqualTo(WageEligibility.PendingSubmission));
            Assert.That(LaborFlowRules.PaymentAllowed(obligation.Eligibility), Is.False, "未交验不得付款");
            Assert.That(obligation.EligibleTick, Is.EqualTo(0), "资格尚未成立（交货路程不记为雇主欠薪）");
            Assert.That(obligation.CompletedTickOrDue(), Is.EqualTo(200));
        }

        [Test]
        public void GrantSubmissionTurnsEligibleAndIsIdempotent()
        {
            Sign("c-phys", SettlementDeliveryMode.PhysicalSubmission);
            var accepted = service.AcceptEvidence(Evidence("c-phys", "tree:3", 200));
            Assert.That(service.GrantSubmission(accepted.ObligationId, "submission:1", 300),
                Is.EqualTo(EmploymentCode.Applied));
            var granted = service.ReadObligation(accepted.ObligationId);
            Assert.That(granted.Eligibility, Is.EqualTo(WageEligibility.Submitted));
            Assert.That(granted.SubmissionId, Is.EqualTo("submission:1"));
            Assert.That(granted.EligibleTick, Is.EqualTo(300));
            Assert.That(LaborFlowRules.PaymentAllowed(granted.Eligibility), Is.True);
            // 幂等：同一提交重复授予 ⇒ AlreadySubmitted（不重复写、不重复付款）。
            Assert.That(service.GrantSubmission(accepted.ObligationId, "submission:1", 400),
                Is.EqualTo(EmploymentCode.AlreadySubmitted));
            // 跨提交身份（旧纸领款）⇒ 冲突。
            Assert.That(service.GrantSubmission(accepted.ObligationId, "submission:2", 500),
                Is.EqualTo(EmploymentCode.DocumentConflict));
        }

        [Test]
        public void GrantIsRejectedForLegacyAndForUnknownEligibility()
        {
            Sign("c-legacy", SettlementDeliveryMode.LegacyImmediate);
            var legacy = service.AcceptEvidence(Evidence("c-legacy", "tree:4", 200));
            // 旧模式应付的资格是 Immediate ⇒ 不接受"提交"改写（既有付款链不受影响）。
            Assert.That(service.GrantSubmission(legacy.ObligationId, "submission:x", 300),
                Is.EqualTo(EmploymentCode.EligibilityBlocked));
            Assert.That(service.ReadObligation(legacy.ObligationId).Eligibility, Is.EqualTo(WageEligibility.Immediate));
        }

        // ── T16/REQ-016：核验失败只**阻断**，绝不删除应付（欠薪权利保留） ────────────────────────

        [Test]
        public void BlockCredentialKeepsTheObligation()
        {
            Sign("c-phys", SettlementDeliveryMode.PhysicalSubmission);
            var accepted = service.AcceptEvidence(Evidence("c-phys", "tree:5", 200));
            Assert.That(service.BlockCredential(accepted.ObligationId, "SampleNotCovered", 300),
                Is.EqualTo(EmploymentCode.Applied));
            var blocked = service.ReadObligation(accepted.ObligationId);
            Assert.That(blocked, Is.Not.Null, "阻断不得删除应付");
            Assert.That(blocked.Eligibility, Is.EqualTo(WageEligibility.Blocked));
            Assert.That(LaborFlowRules.PaymentAllowed(blocked.Eligibility), Is.False);
            Assert.That(blocked.AmountMinor, Is.EqualTo(1000L), "金额与欠薪权利不变");
            // 旧权利不可被就地阻断。
            Sign("c-legacy", SettlementDeliveryMode.LegacyImmediate);
            var legacy = service.AcceptEvidence(Evidence("c-legacy", "tree:6", 200));
            Assert.That(service.BlockCredential(legacy.ObligationId, "whatever", 300),
                Is.EqualTo(EmploymentCode.InvalidCommand));
        }

        // ── T31：承诺→负债守恒（完成一件**不释放**资金占用；付款才释放） ──────────────────────────

        [Test]
        public void OutstandingCommitmentsIncludeUnpaidEarned()
        {
            // 资金口径示例（§5.3）：100 银 ⇒ 单价 50 × 2 件。
            Sign("c-keep", SettlementDeliveryMode.PhysicalSubmission, wage: 5000, maxJobs: 2);
            Assert.That(service.OutstandingCommitments, Is.EqualTo(10000L), "初始占用 = 未完成承诺");

            var first = service.AcceptEvidence(Evidence("c-keep", "tree:7", 200));
            // 完成一件：承诺侧 −1 件、负债侧 +1 件 ⇒ **总占用不变**（修复前会掉到 5000，即可重复派工）。
            Assert.That(service.OutstandingCommitments, Is.EqualTo(10000L),
                "完成一件但尚未付款 ⇒ 总占用必须不变（承诺→负债只转移，不释放）");

            var second = service.AcceptEvidence(Evidence("c-keep", "tree:8", 210));
            Assert.That(service.OutstandingCommitments, Is.EqualTo(10000L), "两件都完成、都未付 ⇒ 全部转为负债");

            // 付清第一件 ⇒ 只释放这一笔。
            Assert.That(service.MarkPaid(first.ObligationId, 5000L, "wage:1"), Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.OutstandingCommitments, Is.EqualTo(5000L));
            Assert.That(service.UnpaidEarnedMinor, Is.EqualTo(5000L));
            Assert.That(service.MarkPaid(second.ObligationId, 5000L, "wage:2"), Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.OutstandingCommitments, Is.EqualTo(0L), "全部付清才完全释放");
        }

        [Test]
        public void TerminationKeepsUnpaidEarnedOccupation()
        {
            Sign("c-term", SettlementDeliveryMode.PhysicalSubmission, wage: 5000, maxJobs: 4);
            var accepted = service.AcceptEvidence(Evidence("c-term", "tree:9", 200));
            Assert.That(service.Terminate("c-term"), Is.EqualTo(EmploymentCode.Applied));
            // 终止释放**未来未做部分**（3 件 = 15000），但已赚的 5000 仍占用（不能把这笔钱派给别人）。
            Assert.That(service.OutstandingCommitments, Is.EqualTo(5000L));
            Assert.That(service.ReadObligation(accepted.ObligationId), Is.Not.Null, "终止不清除欠薪");
        }

        // ── T25：签约身份的**三重建点**全部保留模式与代次（漏一处即静默回落旧模式） ───────────────

        [Test]
        public void ModeSurvivesAllRebuildPoints()
        {
            var contract = Sign("c-rebuild", SettlementDeliveryMode.PhysicalSubmission, maxJobs: 3);
            // ① With（状态/件数改写）
            var progressed = contract.With(ContractState.Active, 1);
            Assert.That(progressed.DeliveryMode, Is.EqualTo(SettlementDeliveryMode.PhysicalSubmission));
            // ② WithDocument（签发/补发）
            var documented = progressed.WithDocument(2, LaborFlowRules.DocumentInstanceIdFor("c-rebuild", 2));
            Assert.That(documented.DeliveryMode, Is.EqualTo(SettlementDeliveryMode.PhysicalSubmission));
            Assert.That(documented.DocumentGeneration, Is.EqualTo(2));
            // ③ OccupiedBy（承接）
            var occupied = documented.OccupiedBy("p2", 300, 900, SettlementDeliveryMode.PhysicalSubmission);
            Assert.That(occupied.DeliveryMode, Is.EqualTo(SettlementDeliveryMode.PhysicalSubmission));
            Assert.That(occupied.DocumentGeneration, Is.EqualTo(2), "承接不改代次");
            Assert.That(occupied.PawnKey, Is.EqualTo("p2"));
            // 承接时显式覆盖模式（§5.4：旧 Posted 无历史在承接时原子写 PhysicalSubmission）。
            var rewritten = documented.OccupiedBy("p2", 300, 900);
            Assert.That(rewritten.DeliveryMode, Is.EqualTo(SettlementDeliveryMode.PhysicalSubmission), "传 null 保留原模式");
        }

        // ── §5.4：起草冻结模式、承接**与状态转换同一项写入**原子改写（GATE-ARCH P0-1 的回归防线） ────

        [Test]
        public void DraftedPostedRowPersistsTheFrozenMode()
        {
            var drafted = service.Sign(new SignRequest
            {
                ContractId = "c-draft", PawnKey = string.Empty, InstitutionKey = "a1",
                PositionKey = "vanilla:Mining:a1", WorkTypeDefNames = new List<string> { "Mining" },
                WagePerJobMinor = 1000, MaxJobs = 2, Now = 100, TermTicks = 60000,
                PositionCapacity = 0, State = ContractState.Posted,
                DeliveryMode = SettlementDeliveryMode.PhysicalSubmission
            });
            Assert.That(drafted.Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.ReadContract("c-draft").DeliveryMode, Is.EqualTo(SettlementDeliveryMode.PhysicalSubmission),
                "起草即冻结模式；若这里回落到 LegacyImmediate，整条到站交验链在实机不可达（P0-1）");
        }

        [Test]
        public void TryOccupyWritesTheFrozenModeInTheSameCommit()
        {
            service.Sign(new SignRequest
            {
                ContractId = "c-posted", PawnKey = string.Empty, InstitutionKey = "a1",
                PositionKey = "vanilla:Mining:a1", WorkTypeDefNames = new List<string> { "Mining" },
                WagePerJobMinor = 1000, MaxJobs = 1, Now = 100, TermTicks = 60000,
                PositionCapacity = 0, State = ContractState.Posted
            });
            Assert.That(service.ReadContract("c-posted").DeliveryMode, Is.EqualTo(SettlementDeliveryMode.LegacyImmediate),
                "未指定模式 ⇒ 旧档缺键语义（这条正是需要被承接原子改写的形态）");
            Assert.That(service.TryOccupy("c-posted", "p1", 200, 60000, SettlementDeliveryMode.PhysicalSubmission), Is.True);
            var occupied = service.ReadContract("c-posted");
            Assert.That(occupied.State, Is.EqualTo(ContractState.Active));
            Assert.That(occupied.DeliveryMode, Is.EqualTo(SettlementDeliveryMode.PhysicalSubmission),
                "承接与模式必须同一项写入（分两次写会出现「已成 Active 但仍是旧模式」的静默窗口）");
            Assert.That(occupied.PawnKey, Is.EqualTo("p1"));
        }

        [Test]
        public void SignTreatsModeAsAClause()
        {
            Sign("c-clause", SettlementDeliveryMode.PhysicalSubmission, maxJobs: 1);
            // 同 ID 仅改模式 ⇒ **冲突**（不得静默改写条款）。
            var conflict = service.Sign(new SignRequest
            {
                ContractId = "c-clause", PawnKey = "p1", InstitutionKey = "a1", PositionKey = "vanilla:Mining:a1",
                WorkTypeDefNames = new List<string> { "Mining" },
                WagePerJobMinor = 1000, MaxJobs = 1, Now = 100, TermTicks = 60000,
                PositionCapacity = 0, DeliveryMode = SettlementDeliveryMode.LegacyImmediate
            });
            Assert.That(conflict.Code, Is.EqualTo(EmploymentCode.CommandConflict));
            Assert.That(service.ReadContract("c-clause").DeliveryMode, Is.EqualTo(SettlementDeliveryMode.PhysicalSubmission));
        }

        [Test]
        public void UnknownModeRowBlocksNewEvidenceAndCommits()
        {
            // 直接注入一条无法识别模式的行（模拟外部编辑 / 未来版本回写）。
            typeof(EmploymentSaveData).GetMethod("WriteContract").Invoke(data, new object[]
            {
                new WorkContract("c-broken", "p1", "a1", "vanilla:Mining:a1", new List<string> { "Mining" },
                    1000L, 2, 100, 60000, ContractState.Active, 0, 0, 0, SettlementDeliveryMode.LegacyImmediate)
            });
            typeof(EmploymentSaveData).GetField("contractIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(data, null);
            data.ReadContract("c-broken").DeliveryMode = "FutureGarbage";
            typeof(EmploymentSaveData).GetField("contractIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(data, null);

            var contract = service.ReadContract("c-broken");
            Assert.That(contract.DeliveryMode, Is.EqualTo(SettlementDeliveryMode.Unknown), "无法识别的模式 ⇒ Unknown");
            Assert.That(contract.CanCommitNewWork, Is.False, "不可信模式不得承接新工作");
            Assert.That(contract.CanAcceptEvidence(200), Is.EqualTo(EmploymentCode.EligibilityBlocked),
                "不可信模式不得产出新应付（fail-closed，不降级为旧模式）");
        }

        // ── 合同纸代次在合同行上的写入纪律（T04/T17） ──────────────────────────────────────────

        [Test]
        public void ApplyDocumentIsMonotonicAndIdempotent()
        {
            Sign("c-doc", SettlementDeliveryMode.PhysicalSubmission, maxJobs: 2);
            string instance = LaborFlowRules.DocumentInstanceIdFor("c-doc", 1);
            Assert.That(service.ApplyDocument("c-doc", 1, instance), Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.ReadContract("c-doc").DocumentGeneration, Is.EqualTo(1));
            Assert.That(service.ApplyDocument("c-doc", 1, instance), Is.EqualTo(EmploymentCode.Replay), "同代次同实例 = 重放（不重印）");
            Assert.That(service.ApplyDocument("c-doc", 2, LaborFlowRules.DocumentInstanceIdFor("c-doc", 2)),
                Is.EqualTo(EmploymentCode.Applied), "补发递增");
            Assert.That(service.ApplyDocument("c-doc", 1, instance), Is.EqualTo(EmploymentCode.DocumentConflict),
                "代次不得倒退（旧纸不能被重新激活）");
            Assert.That(service.ApplyDocument("c-doc", 1, null), Is.EqualTo(EmploymentCode.InvalidCommand));
            Assert.That(service.ApplyDocument("missing", 1, instance), Is.EqualTo(EmploymentCode.ContractUnknown));
        }
    }

    internal static class ObligationTestExtensions
    {
        /// <summary>测试用只读读数：劳动完成 tick（= <see cref="WageObligation.DueTick"/> 的语义名）。</summary>
        public static int CompletedTickOrDue(this WageObligation obligation) => obligation?.DueTick ?? -1;
    }
}

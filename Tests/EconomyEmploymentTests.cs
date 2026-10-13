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
    [TestFixture, NonParallelizable]
    public sealed class EconomyEmploymentTests
    {
        private Harmony shim;
        private string path;
        [OneTimeSetUp] public void Start()
        {
            shim = new Harmony("personalchronicle.tests.employment");
            foreach (string name in new[] { "Error", "Warning", "Message" }) Patch(typeof(Log), name, typeof(string));
            Patch(typeof(DeepProfiler), "Start", typeof(string)); Patch(typeof(DeepProfiler), "End");
        }
        private void Patch(Type type, string name, params Type[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, args, null);
            if (method != null) shim.Patch(method, prefix: new HarmonyMethod(typeof(EconomyEmploymentTests).GetMethod(nameof(Skip), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        private static bool Skip() => false;
        [OneTimeTearDown] public void Stop() => shim.UnpatchAll(shim.Id);
        [SetUp] public void Setup() { path = Path.Combine(Path.GetTempPath(), "pc_employment_" + Guid.NewGuid().ToString("N") + ".xml"); }
        [TearDown] public void Cleanup() { Scribe.ForceStop(); if (File.Exists(path)) File.Delete(path); }

        private static SignRequest Request(long wage = 500, int maxJobs = 3, int now = 10, int term = 60000)
            => new SignRequest { ContractId = "contract:1", PawnKey = "Human_7", InstitutionKey = "anchor:A",
                PositionKey = "anchor:A/42/7", WorkTypeDefNames = new[] { "Crafting" }, WagePerJobMinor = wage,
                MaxJobs = maxJobs,
                // CHG-20261010-ECON-VANILLA-CUT：岗位份数判据改为**显式**的 PositionCapacity。
                // 本工厂保持既有用例的原语义（`maxJobs` 在旧契约里兼任"岗位空位数"）。
                PositionCapacity = maxJobs,
                Now = now, TermTicks = term };
        private static WorkEvidence Evidence(int sequence, int tick = 20, string source = "bill:9", string phase = "Iteration")
            => new WorkEvidence("contract:1", source, phase, sequence, 1, "iteration", tick);

        [TestCase(8, 18, 8, true)]
        [TestCase(8, 18, 18, false)]
        [TestCase(22, 6, 23, true)]
        [TestCase(22, 6, 5, true)]
        [TestCase(22, 6, 6, false)]
        [TestCase(0, 0, 23, true)]
        [TestCase(0, 24, 23, true)]
        public void WorkAuthorityReadsTheActualContractWindow(int start, int end, int hour, bool authorized)
        {
            var data = new EmploymentSaveData();
            var service = new EmploymentService(data);
            var request = Request(); request.WindowStartHour = start; request.WindowEndHour = end;
            Assert.That(service.Sign(request).Code, Is.EqualTo(EmploymentCode.Applied));
            var names = new System.Collections.Generic.HashSet<string>();
            service.CollectActiveWorkTypesFor("Human_7", 20, hour,
                new System.Collections.Generic.HashSet<string> { "anchor:A" }, names);
            Assert.That(names.Contains("Crafting"), Is.EqualTo(authorized));
            Assert.That(service.ReadContract("contract:1").AllowsHour(hour), Is.EqualTo(authorized),
                "授权与合同条款必须同源");
        }

        [Test] public void AuthorityRejectsWrongMapExpiredFinishedPostedFutureAndInvalidRows()
        {
            var data = new EmploymentSaveData();
            var service = new EmploymentService(data);
            Assert.That(service.Sign(Request()).Code, Is.EqualTo(EmploymentCode.Applied));
            var names = new System.Collections.Generic.HashSet<string>();
            var anchors = new System.Collections.Generic.HashSet<string> { "anchor:other-map" };
            service.CollectActiveWorkTypesFor("Human_7", 20, 12, anchors, names);
            Assert.That(names, Is.Empty, "另图的同工种合同不授予本图工作权限");
            anchors.Add("anchor:A");
            var row = data.ReadContract("contract:1");
            row.ExpiresTick = 20;
            service.CollectActiveWorkTypesFor("Human_7", 20, 12, anchors, names);
            Assert.That(names, Is.Empty, "正好到期时拒绝，即使清理尚未发生");
            row.ExpiresTick = 60000; row.CompletedJobs = row.MaxJobs;
            service.CollectActiveWorkTypesFor("Human_7", 20, 12, anchors, names);
            Assert.That(names, Is.Empty, "未维护的已做满合同不能授权");
            row.CompletedJobs = 0; row.State = ContractState.Posted.ToString();
            service.CollectActiveWorkTypesFor("Human_7", 20, 12, anchors, names);
            Assert.That(names, Is.Empty, "待承接不能授权");
            row.State = ContractState.Active.ToString(); row.SignedTick = 21;
            service.CollectActiveWorkTypesFor("Human_7", 20, 12, anchors, names);
            Assert.That(names, Is.Empty, "时刻回退不能授权尚未成立的合同");
            row.SignedTick = 10; row.WindowStartHour = -1;
            service.CollectActiveWorkTypesFor("Human_7", 20, 12, anchors, names);
            Assert.That(names, Is.Empty, "损坏时段不能以全天猜测");
            row.WindowStartHour = 0;
            service.CollectActiveWorkTypesFor("Human_7", 20, 12, anchors, names);
            Assert.That(names, Is.EquivalentTo(new[] { "Crafting" }), "反向对照：恢复有效行后须确实授权");
            Assert.That(row.CompletedJobs, Is.Zero, "授权只读不会消耗合同");
        }

        [Test] public void SignReplaysTheSameTermsAndRejectsAConflictingRewrite()
        {
            var service = new EmploymentService(new EmploymentSaveData());
            Assert.That(service.Sign(Request()).Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.Contracts, Is.EqualTo(1));
            var reread = service.ReadContract("contract:1");
            Assert.That(reread.WagePerJobMinor, Is.EqualTo(500));
            Assert.That(reread.WorkTypeDefNames, Has.Count.EqualTo(1));
            Assert.That(service.Sign(Request()).Code, Is.EqualTo(EmploymentCode.Replay));
            Assert.That(service.Sign(Request(wage: 900)).Code, Is.EqualTo(EmploymentCode.CommandConflict));
            Assert.That(service.ReadContract("contract:1").WagePerJobMinor, Is.EqualTo(500), "clauses are never rewritten retroactively");
            Assert.That(service.Sign(Request(wage: 0)).Code, Is.EqualTo(EmploymentCode.InvalidCommand));
        }
        [Test] public void AClosedPeriodIsNeverRevivedAndANewPeriodSignsCleanly()
        {
            var service = new EmploymentService(new EmploymentSaveData());
            Assert.That(service.Sign(Request()).Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.Terminate("contract:1"), Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.ReadContract("contract:1").State, Is.EqualTo(ContractState.Terminated));
            Assert.That(service.Sign(Request()).Code, Is.EqualTo(EmploymentCode.Replay),
                "the closed period keeps its record; a new attempt must use its own period id");
            var next = new SignRequest
            { ContractId = "contract:1|r2|30", PawnKey = "Human_7", InstitutionKey = "anchor:A", PositionKey = "anchor:A/42/7",
                WorkTypeDefNames = new[] { "Crafting" }, WagePerJobMinor = 900, MaxJobs = 1, Now = 30, TermTicks = 60000 };
            Assert.That(service.Sign(next).Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.ReadContract("contract:1|r2|30").State, Is.EqualTo(ContractState.Active));
            Assert.That(service.ReadContract("contract:1|r2|30").WagePerJobMinor, Is.EqualTo(900),
                "a republished wage applies to the new period only, never retroactively");
        }
        [Test] public void AFullContractLedgerRefusesNewSigningsInsteadOfHalfCommitting()
        {
            var store = new EmploymentSaveData();
            var service = new EmploymentService(store);
            for (int i = 0; i < EmploymentSaveData.MaximumContracts; i++)
            {
                var request = Request();
                request.ContractId = "contract:" + i.ToString();
                // ECON-R05 后岗位空位自有容量上限：本用例测的是**账本容量**，
                // 故每份合同必须落在不同岗位（否则第 2 份就会被 SlotTaken 挡下）。
                request.PositionKey = "position:" + i.ToString();
                Assert.That(service.Sign(request).Code, Is.EqualTo(EmploymentCode.Applied), "signing " + i.ToString());
            }
            var overflow = Request();
            overflow.ContractId = "contract:overflow";
            overflow.PositionKey = "position:overflow";
            Assert.That(service.Sign(overflow).Code, Is.EqualTo(EmploymentCode.CapacityReached));
            Assert.That(service.ReadContract("contract:overflow"), Is.Null, "a refused signing must leave no record");
        }
        [Test] public void OnePositionAcceptsExactlyItsSlotsAndRefusesTheNextSigning()
        {
            // ECON-R05：岗位空位被占满后，下一份签约必须被拒 —— 两名 Pawn 争一个空位只成功一个。
            // 此前机会源只读班次缺员、签约又不登记占用 ⇒ 同一岗位可被重复承诺付款。
            var store = new EmploymentSaveData();
            var service = new EmploymentService(store);
            var first = Request(maxJobs: 1); first.ContractId = "contract:a";
            Assert.That(service.Sign(first).Code, Is.EqualTo(EmploymentCode.Applied));
            var second = Request(maxJobs: 1); second.ContractId = "contract:b"; second.PawnKey = "Human_8";
            Assert.That(service.Sign(second).Code, Is.EqualTo(EmploymentCode.SlotTaken), "a taken slot must refuse a second contract");
            Assert.That(service.ReadContract("contract:b"), Is.Null, "a refused signing must leave no record");
            Assert.That(service.Contracts, Is.EqualTo(1), "the refused signing must not consume ledger capacity");
            Assert.That(service.OutstandingCommitments, Is.EqualTo(first.WagePerJobMinor * first.MaxJobs),
                "the live contract's remaining commitment stays fully reserved");
        }
        [Test] public void CHG20261010_ZeroPositionCapacityLetsPieceworkContractsStack()
        {
            // CHG-20261010-ECON-VANILLA-CUT（用户需求「100 个割除可以修改派发 10 个合同」）：
            // 件数型合同（原版工作）**没有"岗位空位"语义** —— `PositionCapacity = 0` ⇒ 同一岗位
            // 可并存多份合同，总量由"同工种待办预留"（EconomyVanillaWork）约束。
            // 旧判据用 `MaxJobs`（件数）当份数上限 ⇒ 小余量时误拒合法签约
            //（余 4 件 / 已有 4 份 ⇒ `4 >= 4` ⇒ 第 5 份被拒、剩余工作派不出去）。
            var store = new EmploymentSaveData();
            var service = new EmploymentService(store);
            for (int i = 0; i < 5; i++)
            {
                var request = Request(maxJobs: 4);
                request.PositionCapacity = 0;
                request.ContractId = "contract:piece:" + i.ToString();
                request.PawnKey = "Human_" + i.ToString();
                Assert.That(service.Sign(request).Code, Is.EqualTo(EmploymentCode.Applied), "signing " + i.ToString());
            }
            Assert.That(service.Contracts, Is.EqualTo(5), "件数型岗位不限份数（总量在待办预留侧约束）");
        }
        [Test] public void ClosingAContractFreesItsSlotAndItsReservedCommitment()
        {
            // ECON-R05：岗位占用与工资预留都随合同**结束**释放（否则空位与资金被永久锁死）。
            var store = new EmploymentSaveData();
            var service = new EmploymentService(store);
            var first = Request(maxJobs: 1); first.ContractId = "contract:a";
            Assert.That(service.Sign(first).Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.Terminate("contract:a"), Is.EqualTo(EmploymentCode.Applied));
            var second = Request(maxJobs: 1); second.ContractId = "contract:b"; second.PawnKey = "Human_8";
            Assert.That(service.Sign(second).Code, Is.EqualTo(EmploymentCode.Applied), "a closed contract must free its slot");
            Assert.That(service.OutstandingCommitments, Is.EqualTo(second.WagePerJobMinor * second.MaxJobs),
                "only live contracts reserve wages");
        }
        [Test] public void ExpiredContractsAreListableWithoutAPawnSoOrphansCanBeCleanedUp()
        {
            // ECON-R08 判据基础：离图 / 被销毁的 Pawn 无法自己触发退出复查，清理必须有一条
            // **不依赖 Pawn 实例**的依据 —— 过期即列入（Request 默认 now=10、term=60000 ⇒ 到期 60010）。
            var store = new EmploymentSaveData();
            var service = new EmploymentService(store);
            service.Sign(Request(maxJobs: 1));
            Assert.That(store.ReadExpiredContractIds(60009, 8), Is.Empty, "a contract still in term must not be listed");
            var due = store.ReadExpiredContractIds(60010, 8);
            Assert.That(due.Count, Is.EqualTo(1), "an expiring contract must be reachable without a pawn instance");
            Assert.That(due[0], Is.EqualTo("contract:1"));
            service.Terminate("contract:1");
            Assert.That(store.ReadExpiredContractIds(60010, 8), Is.Empty, "a closed contract is never listed again");
        }
        [Test] public void EvidenceCreatesExactlyOnePayableAndReplaysSafely()
        {
            var service = new EmploymentService(new EmploymentSaveData());
            service.Sign(Request());
            var first = service.AcceptEvidence(Evidence(0));
            Assert.That(first.Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(first.AmountMinor, Is.EqualTo(500));
            Assert.That(first.ObligationId, Is.EqualTo("wage:evidence:contract:1|bill:9|Iteration|0"));
            var again = service.AcceptEvidence(Evidence(0));
            Assert.That(again.Code, Is.EqualTo(EmploymentCode.Replay));
            Assert.That(again.ObligationId, Is.EqualTo(first.ObligationId));
            Assert.That(service.Obligations, Is.EqualTo(1), "a repeated notification must never create a second payable");
            Assert.That(service.ReadContract("contract:1").CompletedJobs, Is.EqualTo(1));
        }
        [Test] public void SeparateIterationsOfOneJobAreSeparateEvidence()
        {
            var service = new EmploymentService(new EmploymentSaveData());
            service.Sign(Request());
            Assert.That(service.AcceptEvidence(Evidence(0)).Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.AcceptEvidence(Evidence(1)).Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.AcceptEvidence(Evidence(0)).Code, Is.EqualTo(EmploymentCode.Replay));
            Assert.That(service.Obligations, Is.EqualTo(2));
            Assert.That(service.AcceptEvidence(new WorkEvidence("contract:9", "bill:9", "Iteration", 0, 1, "iteration", 20)).Code,
                Is.EqualTo(EmploymentCode.ContractUnknown));
        }
        [Test] public void JobCapAndExpiryStopNewPayablesWithoutErasingTheOldOnes()
        {
            var service = new EmploymentService(new EmploymentSaveData());
            service.Sign(Request(maxJobs: 2));
            Assert.That(service.AcceptEvidence(Evidence(0)).Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.AcceptEvidence(Evidence(1)).Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.AcceptEvidence(Evidence(2)).Code, Is.EqualTo(EmploymentCode.JobCapReached));
            var expired = new EmploymentService(new EmploymentSaveData());
            expired.Sign(Request(now: 10, term: 5));
            Assert.That(expired.AcceptEvidence(Evidence(0, tick: 20)).Code, Is.EqualTo(EmploymentCode.ContractExpired));
        }
        [Test] public void TerminatingAContractKeepsPerformedWorkPayable()
        {
            var service = new EmploymentService(new EmploymentSaveData());
            service.Sign(Request());
            var due = service.AcceptEvidence(Evidence(0));
            Assert.That(service.Terminate("contract:1"), Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.AcceptEvidence(Evidence(1)).Code, Is.EqualTo(EmploymentCode.ContractNotActive));
            Assert.That(service.ReadObligation(due.ObligationId).State, Is.EqualTo(ObligationState.Due));
            Assert.That(service.MarkPaid(due.ObligationId, 500, "tx:1"), Is.EqualTo(EmploymentCode.Applied));
        }
        [Test] public void MarkPaidNeverPaysTwiceAndRefusesAMismatchedAmount()
        {
            var service = new EmploymentService(new EmploymentSaveData());
            service.Sign(Request());
            var due = service.AcceptEvidence(Evidence(0));
            Assert.That(service.MarkPaid("wage:missing", 500, "tx:0"), Is.EqualTo(EmploymentCode.ObligationUnknown));
            Assert.That(service.MarkPaid(due.ObligationId, 400, "tx:0"), Is.EqualTo(EmploymentCode.AmountMismatch));
            Assert.That(service.MarkPaid(due.ObligationId, 500, "tx:1"), Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.MarkPaid(due.ObligationId, 500, "tx:1"), Is.EqualTo(EmploymentCode.AlreadyPaid));
            Assert.That(service.ReadObligation(due.ObligationId).PaidMinor, Is.EqualTo(500));
        }
        [Test] public void EmploymentSurvivesASaveRoundTripAndStillDeduplicates()
        {
            var store = new EmploymentSaveData();
            var service = new EmploymentService(store);
            service.Sign(Request());
            var due = service.AcceptEvidence(Evidence(0));
            service.MarkPaid(due.ObligationId, 500, "tx:1");
            Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref store, "pawnEconomicEmployment"); Scribe.saver.FinalizeSaving();
            EmploymentSaveData loaded = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "pawnEconomicEmployment"); Scribe.loader.FinalizeLoading();
            var reloaded = new EmploymentService(loaded);
            Assert.That(reloaded.Contracts, Is.EqualTo(1));
            Assert.That(reloaded.ReadContract("contract:1").CompletedJobs, Is.EqualTo(1));
            Assert.That(reloaded.ReadObligation(due.ObligationId).State, Is.EqualTo(ObligationState.Paid));
            Assert.That(reloaded.AcceptEvidence(Evidence(0)).Code, Is.EqualTo(EmploymentCode.Replay),
                "the evidence guard must survive the save, otherwise a reload could pay twice");
            Assert.That(reloaded.Obligations, Is.EqualTo(1));
        }

        [Test] public void AuthorityRetainsCrossMidnightClausesAcrossSaveReloadAndLegacyDefaults()
        {
            var store = new EmploymentSaveData();
            var service = new EmploymentService(store);
            var request = Request(); request.WindowStartHour = 22; request.WindowEndHour = 6;
            Assert.That(service.Sign(request).Code, Is.EqualTo(EmploymentCode.Applied));
            Scribe.saver.InitSaving(path, "savegame"); Scribe_Deep.Look(ref store, "pawnEconomicEmployment"); Scribe.saver.FinalizeSaving();
            EmploymentSaveData loaded = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "pawnEconomicEmployment"); Scribe.loader.FinalizeLoading();
            var reloaded = new EmploymentService(loaded);
            var names = new System.Collections.Generic.HashSet<string>();
            var anchors = new System.Collections.Generic.HashSet<string> { "anchor:A" };
            reloaded.CollectActiveWorkTypesFor("Human_7", 20, 23, anchors, names);
            Assert.That(names, Does.Contain("Crafting"));
            names.Clear();
            reloaded.CollectActiveWorkTypesFor("Human_7", 20, 6, anchors, names);
            Assert.That(names, Is.Empty, "读档不能把跨夜合约变成全天");
            // 旧档缺 windowStart/windowEnd 的 Scribe 默认值为 0；以实际移除节点证明，不猜默认兼容。
            var xml = new System.Xml.XmlDocument(); xml.Load(path);
            foreach (System.Xml.XmlNode node in xml.SelectNodes("//windowStart|//windowEnd")) node.ParentNode.RemoveChild(node);
            xml.Save(path); loaded = null;
            Scribe.loader.InitLoading(path); Scribe_Deep.Look(ref loaded, "pawnEconomicEmployment"); Scribe.loader.FinalizeLoading();
            names.Clear(); new EmploymentService(loaded).CollectActiveWorkTypesFor("Human_7", 20, 6, anchors, names);
            Assert.That(names, Does.Contain("Crafting"), "真实缺键旧 payload 恢复为 0/0 全天");
        }

        /// <summary>CHG-20261012 · B1（P1-2）：弹窗草拟的 **Posted（已派发待承接）** 合同 —— 无持约人、
        /// PositionCapacity = 0（件数型）、带工作时段。</summary>
        private static SignRequest PostedRequest(int windowStart = 8, int windowEnd = 18)
            => new SignRequest
            { ContractId = "contract:posted", PawnKey = string.Empty, InstitutionKey = "anchor:A",
                PositionKey = "vanilla:PlantCutting:anchor:A", WorkTypeDefNames = new[] { "PlantCutting" },
                WagePerJobMinor = 500, MaxJobs = 8, PositionCapacity = 0, Now = 10, TermTicks = 60000,
                WindowStartHour = windowStart, WindowEndHour = windowEnd, State = ContractState.Posted };

        [Test] public void CHG20261012_SignReplayTreatsTheWorkWindowAsAClause()
        {
            // B1（P2-3）：合同 ID 不含时段（ID = 机构|工种|tick）⇒ 幂等比较必须**自带**时段判据，
            // 否则"同 ID 仅改时段"会被判成重放、静默保留旧时段（玩家以为改了、实际没改，
            // 而"条款不静默改写"是本服务的既定纪律）。
            var service = new EmploymentService(new EmploymentSaveData());
            Assert.That(service.Sign(PostedRequest()).Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.Sign(PostedRequest()).Code, Is.EqualTo(EmploymentCode.Replay),
                "same terms including the window = idempotent replay");
            Assert.That(service.Sign(PostedRequest(9, 17)).Code, Is.EqualTo(EmploymentCode.CommandConflict),
                "a different window is a different clause set");
            Assert.That(service.ReadContract("contract:posted").WindowStartHour, Is.EqualTo(8),
                "clauses are never rewritten retroactively");
            Assert.That(service.ReadContract("contract:posted").WindowEndHour, Is.EqualTo(18));
        }

        [Test] public void CHG20261012_PostedContractIsOccupiedInPlaceAndThenRefusesRevocation()
        {
            // B1（P1-2）：承接 = **同一份合同** Posted → Active（条款/时段原样保留，不产生第二份合同）；
            // 承接之后**不再可撤销** —— 撤销只针对待承接（解约是另一条线，需专门写路径）。
            var service = new EmploymentService(new EmploymentSaveData());
            Assert.That(service.Sign(PostedRequest()).Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.ReadContract("contract:posted").State, Is.EqualTo(ContractState.Posted));
            Assert.That(service.ReadPostedContractsFor("anchor:A"), Has.Count.EqualTo(1));
            Assert.That(service.TryOccupy("contract:posted", "Human_7", 100, 60000), Is.True);
            var live = service.ReadContract("contract:posted");
            Assert.That(live.State, Is.EqualTo(ContractState.Active));
            Assert.That(live.ContractId, Is.EqualTo("contract:posted"), "occupying is not a new contract");
            Assert.That(live.PawnKey, Is.EqualTo("Human_7"));
            Assert.That(live.WindowStartHour, Is.EqualTo(8), "occupying keeps the clauses");
            Assert.That(live.WindowEndHour, Is.EqualTo(18));
            Assert.That(service.ReadPostedContractsFor("anchor:A"), Is.Empty, "an occupied contract is no longer posted");
            Assert.That(service.TryOccupy("contract:posted", "Human_8", 100, 60000), Is.False,
                "a taken contract cannot be occupied a second time");
            Assert.That(service.RevokePosted("contract:posted"), Is.EqualTo(EmploymentCode.ContractNotActive),
                "revocation is reserved for posted contracts");
            Assert.That(service.ReadContract("contract:posted").State, Is.EqualTo(ContractState.Active),
                "a refused revocation changes nothing");
        }

        [Test] public void CHG20261012_RevokePostedEndsOnlyThePostedContractAndReleasesTheReservation()
        {
            var service = new EmploymentService(new EmploymentSaveData());
            Assert.That(service.Sign(PostedRequest()).Code, Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.OutstandingCommitments, Is.EqualTo(500L * 8),
                "a posted contract is a reserved commitment (wage x jobs)");
            Assert.That(service.RevokePosted("contract:missing"), Is.EqualTo(EmploymentCode.ContractUnknown));
            Assert.That(service.RevokePosted("contract:posted"), Is.EqualTo(EmploymentCode.Applied));
            Assert.That(service.ReadContract("contract:posted").State, Is.EqualTo(ContractState.Terminated));
            Assert.That(service.ReadPostedContractsFor("anchor:A"), Is.Empty, "a revoked contract is off the market");
            Assert.That(service.OutstandingCommitments, Is.EqualTo(0L), "revoking releases the reservation");
            Assert.That(service.RevokePosted("contract:posted"), Is.EqualTo(EmploymentCode.ContractNotActive),
                "a revoked contract is not posted any more");
        }
    }
}

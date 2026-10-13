using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Employment;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// CHG-20261012-ECON-CONTRACTIZE：工作合同**起草规则**（推荐价 / 时段 / 校验）的离线断言。
    ///
    /// <para><b>为什么必须离线锁定</b>：这三样直接决定**资金承诺**（Posted 合同计入国库预留）与
    /// **何时能干活**（时段挂起/恢复），而窗口/优先级/弹窗绘制全都无法在沙箱里跑 —— 与上一批
    /// "绿也拦不住"的三个缺陷族同一设防思路：能算成纯函数的，一律在此钉死。</para>
    /// </summary>
    [TestFixture]
    public class WorkContractRulesTests
    {
        // ─────────────────────────── 推荐价 ───────────────────────────

        [Test] public void Recommend_UsesHalfOfOutputMarketValue()
        {
            // 产出市价 10 银/件 ⇒ 推荐 5.00 银/件 = 500 minor，且标记"来自市价"。
            long wage = WorkContractRules.RecommendUnitWageMinor(10d, out bool fromMarket);
            Assert.That(wage, Is.EqualTo(500L), "10 银 × 50% = 5.00 银 = 500 minor");
            Assert.That(fromMarket, Is.True, "市价可得 ⇒ fromMarket 为真（UI 据此显示'市价'而非'基准价'）");
        }

        [Test] public void Recommend_FallsBackToBaseWhenMarketUnknown()
        {
            // 无产出 / 市价未知 / NaN / 负值 ⇒ 基准价，并**如实标记**不是市价（诚实性）。
            foreach (double unknown in new[] { 0d, -3d, double.NaN, double.PositiveInfinity })
            {
                long wage = WorkContractRules.RecommendUnitWageMinor(unknown, out bool fromMarket);
                Assert.That(wage, Is.EqualTo(WorkContractRules.RecommendedBaseMinor),
                    "市价不可得（" + unknown + "）⇒ 基准价 5.00 银/件");
                Assert.That(fromMarket, Is.False, "回落时必须标记 fromMarket=false（不得把基准价伪装成市价）");
            }
        }

        [Test] public void Recommend_NeverReturnsZeroForPositiveMarket()
        {
            // 极低价产出不得变成免费工资（与 ItemValuation 的"下限 1 minor"同口径）。
            long wage = WorkContractRules.RecommendUnitWageMinor(0.0001d, out bool fromMarket);
            Assert.That(wage, Is.GreaterThanOrEqualTo(1L), "正市价 ⇒ 工资至少 1 minor");
            Assert.That(fromMarket, Is.True);
        }

        // ─────────────────────────── 数量钳制 ───────────────────────────

        [Test] public void ClampJobs_RespectsDemandAndFloor()
        {
            Assert.That(WorkContractRules.ClampJobs(0, 32), Is.EqualTo(1), "低于下限 ⇒ 抬到 1");
            Assert.That(WorkContractRules.ClampJobs(100, 32), Is.EqualTo(32), "超过可派发 ⇒ 钳到可派发");
            Assert.That(WorkContractRules.ClampJobs(10, 32), Is.EqualTo(10), "区间内保持原值");
            Assert.That(WorkContractRules.ClampJobs(5, 0), Is.EqualTo(0), "无可派发（无需求）⇒ 0 = 不可起草");
        }

        // ─────────────────────────── 时段 ───────────────────────────

        [Test] public void AllowsHour_DayWindowIsHalfOpen()
        {
            var contract = Contract(windowStart: 8, windowEnd: 18);
            Assert.That(contract.AllowsHour(8), Is.True, "起点含");
            Assert.That(contract.AllowsHour(12), Is.True);
            Assert.That(contract.AllowsHour(18), Is.False, "终点不含（半开区间，22–6 跨夜口径一致）");
            Assert.That(contract.AllowsHour(7), Is.False);
            Assert.That(contract.AllowsHour(23), Is.False);
        }

        [Test] public void AllowsHour_WrapsAcrossMidnight()
        {
            var contract = Contract(windowStart: 22, windowEnd: 6);
            Assert.That(contract.AllowsHour(23), Is.True, "跨夜：22 点后含");
            Assert.That(contract.AllowsHour(3), Is.True, "跨夜：6 点前含");
            Assert.That(contract.AllowsHour(6), Is.False, "终点不含");
            Assert.That(contract.AllowsHour(12), Is.False, "白天不在跨夜窗口内");
        }

        [Test] public void AllowsHour_ZeroZeroMeansAllDay()
        {
            // 旧档合同没有时段键 ⇒ 0/0 ⇒ **全天**（承接旧合同的殖民者不被凭空限时）。
            var legacy = Contract(windowStart: 0, windowEnd: 0);
            for (int h = 0; h < 24; h++) Assert.That(legacy.AllowsHour(h), Is.True, "0/0 = 全天，hour=" + h);
        }

        [Test] public void StepHour_WrapsBothDirections()
        {
            Assert.That(WorkContractRules.StepHour(23, +1), Is.EqualTo(24), "23 → 24");
            Assert.That(WorkContractRules.StepHour(24, +1), Is.EqualTo(0), "24 进位回 0（24 与 0 不在 UI 并存）");
            Assert.That(WorkContractRules.StepHour(0, -1), Is.EqualTo(24), "0 后退回 24");
            Assert.That(WorkContractRules.StepHour(8, +1), Is.EqualTo(9));
        }

        // ─────────────────────────── 起草校验 ───────────────────────────

        [Test] public void Validate_ReportsStableReasons()
        {
            Assert.That(WorkContractRules.ValidateDraft(5, 500L, 8, 18, 0, 100000L, 0L, out string r1), Is.False);
            Assert.That(r1, Is.EqualTo("NoDemand"), "无需求 ⇒ 稳定 ID NoDemand（呈现层翻译）");
            Assert.That(WorkContractRules.ValidateDraft(0, 500L, 8, 18, 32, 100000L, 0L, out string r2), Is.False);
            Assert.That(r2, Is.EqualTo("JobsOutOfRange"));
            Assert.That(WorkContractRules.ValidateDraft(5, 0L, 8, 18, 32, 100000L, 0L, out string r3), Is.False);
            Assert.That(r3, Is.EqualTo("WageOutOfRange"), "零工资合同不得起草（否则是免费劳动）");
            Assert.That(WorkContractRules.ValidateDraft(5, 500L, 8, 25, 32, 100000L, 0L, out string r4), Is.False);
            Assert.That(r4, Is.EqualTo("WindowOutOfRange"));
        }

        [Test] public void Validate_DeductsOutstandingCommitmentsFromTreasury()
        {
            // 国库 100.00 银、既有承诺 90.00 银 ⇒ 只剩 10.00 银：10 件 × 5.00 银（5000 minor）不可起草。
            Assert.That(WorkContractRules.ValidateDraft(10, 500L, 8, 18, 100, 10000L, 9000L, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("InsufficientBacking"), "预留不足必须拒绝（ECON-R05 同源）");
            // 同一比例下 2 件（1000 minor）可以。
            Assert.That(WorkContractRules.ValidateDraft(2, 500L, 8, 18, 100, 10000L, 9000L, out string ok), Is.True);
            Assert.That(ok, Is.Null, "通过时 reason = null");
        }

        [Test] public void SaturatedMultiply_NeverWrapsNegative()
        {
            Assert.That(WorkContractRules.SaturatedMultiply(long.MaxValue, 2), Is.EqualTo(long.MaxValue),
                "饱和：溢出返回上限（绝不让承诺额回绕成负数）");
            Assert.That(WorkContractRules.SaturatedMultiply(500L, 4), Is.EqualTo(2000L));
            Assert.That(WorkContractRules.SaturatedMultiply(0L, 100), Is.EqualTo(0L));
        }

        // ---------- helper：用真实实体构造（顺带锁定 Posted 允许空持约人） ----------

        private static WorkContract Contract(int windowStart, int windowEnd)
            => new WorkContract("contract:t", "", "anchor:L", "vanilla:PlantCutting|anchor:L",
                new[] { "PlantCutting" }, 500L, 32, 100, 100 + WorkContractRules.DraftLifetimeTicks,
                ContractState.Posted, 0, windowStart, windowEnd);

        [Test] public void PostedContract_AllowsEmptyHolder_ActiveDoesNot()
        {
            var posted = Contract(8, 18);
            Assert.That(posted.State, Is.EqualTo(ContractState.Posted));
            Assert.That(posted.PawnKey, Is.Empty, "Posted 合同没有持约人（待承接）");
            Assert.That(posted.CanAcceptEvidence(200), Is.EqualTo(EmploymentCode.ContractNotActive),
                "Posted 不受理工作证据（还没人承接）");
            var occupied = posted.OccupiedBy("Pawn_A", 200, 200 + WorkContractRules.ContractLifetimeTicks);
            Assert.That(occupied.State, Is.EqualTo(ContractState.Active));
            Assert.That(occupied.PawnKey, Is.EqualTo("Pawn_A"));
            Assert.That(occupied.WindowStartHour, Is.EqualTo(8), "承接不改条款：时段必须原样保留");
            Assert.That(occupied.WindowEndHour, Is.EqualTo(18));
            Assert.That(occupied.ContractId, Is.EqualTo(posted.ContractId), "承接同一份合同（不是新合同）");
        }

        [Test] public void TerminatingAPostedContract_DoesNotThrow()
        {
            // GATE-SAVE 2026-10-12 P0-1 回归锁：`TrimExpiredContracts` 对**过期 Posted** 直接 `Terminate`
            // ⇒ `With(Terminated, …)` 必须允许空持约人（"从未承接就作废"）。
            // 缺此放宽时构造器抛 ArgumentException ⇒ 每 250 tick 错误风暴 + 同 tick 结算/对账/裁剪停摆
            // + 工资预留永久饱和（真实的"派发后无法再派发"死锁）。
            var posted = Contract(8, 18);
            var terminated = posted.With(ContractState.Terminated, posted.CompletedJobs);
            Assert.That(terminated.State, Is.EqualTo(ContractState.Terminated));
            Assert.That(terminated.PawnKey, Is.Empty, "从未承接的作废合同没有持约人");
            Assert.That(terminated.WindowStartHour, Is.EqualTo(8), "作废不改条款快照");
            Assert.That(terminated.WindowEndHour, Is.EqualTo(18));
        }
    }
}

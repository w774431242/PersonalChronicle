using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Learning;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// M9 有界退避。每条断言对应设计文档《09-经验反馈与失败恢复》§4 的一句原文；
    /// 退避一旦写成"无界重试"或"含 Tick 的指纹"，只会表现为刷屏与卡顿，很难归因。
    /// </summary>
    [TestFixture]
    public sealed class RetryBackoffTests
    {
        [Test] public void BackoffFollowsThePublishedCurveAndIsCappedAtTheCeiling()
        {
            // 文档原式：min(6000, 150×2^min(failureCount−1,6))
            Assert.That(RetryBackoffRules.BackoffTicks(1), Is.EqualTo(150));
            Assert.That(RetryBackoffRules.BackoffTicks(2), Is.EqualTo(300));
            Assert.That(RetryBackoffRules.BackoffTicks(3), Is.EqualTo(600));
            Assert.That(RetryBackoffRules.BackoffTicks(4), Is.EqualTo(1200));
            Assert.That(RetryBackoffRules.BackoffTicks(5), Is.EqualTo(2400));
            Assert.That(RetryBackoffRules.BackoffTicks(6), Is.EqualTo(4800));
            Assert.That(RetryBackoffRules.BackoffTicks(7), Is.EqualTo(6000), "9600 越顶后被截断");
            for (int f = 8; f <= 40; f++)
                Assert.That(RetryBackoffRules.BackoffTicks(f), Is.EqualTo(RetryBackoffRules.CeilingTicks),
                    "无论失败多少次都不得超过天花板");
        }

        [Test] public void TheFailureCountIsAtLeastOne()
        {
            // 文档："失败数至少 1" —— 0 或负数不得导致 0 退避（那等于无冷却）。
            Assert.That(RetryBackoffRules.BackoffTicks(0), Is.EqualTo(150));
            Assert.That(RetryBackoffRules.BackoffTicks(-9), Is.EqualTo(150));
        }

        [Test] public void TheFifthFailureMarksPersistentBlockedAndSwitchesToTheBaselineProbe()
        {
            RetryBackoffRules.NextRetryTick(1000, 4, "fp", out var cooling);
            Assert.That(cooling, Is.EqualTo(RetryPhase.CoolingDown));
            RetryBackoffRules.NextRetryTick(1000, 5, "fp", out var blocked);
            Assert.That(blocked, Is.EqualTo(RetryPhase.PersistentBlocked), "第 5 次同指纹失败即判结构性受阻");
            Assert.That(RetryBackoffRules.NextRetryTick(1000, 5, "fp", out _), Is.EqualTo(1000 + 6000));
            Assert.That(RetryBackoffRules.NextRetryTick(1000, 99, "fp", out _), Is.EqualTo(1000 + 6000),
                "受阻后不再按失败数倍增，改为平坦兜底探测");
        }

        [Test] public void JitterIsStableSoASaveReloadCannotShiftThePhase()
        {
            int backoff = RetryBackoffRules.BackoffTicks(4);
            int first = RetryBackoffRules.JitterTicks("quote:3|stock:9|5000|Adult|7", backoff);
            int again = RetryBackoffRules.JitterTicks("quote:3|stock:9|5000|Adult|7", backoff);
            Assert.That(again, Is.EqualTo(first), "稳定抖动：同一指纹必须恒得同一相位，否则读档后相位跳变");
        }

        [Test] public void JitterIsBoundedAndSpreadDifferentFingerprintsApart()
        {
            int backoff = RetryBackoffRules.BackoffTicks(6);
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 40; i++)
            {
                int jitter = RetryBackoffRules.JitterTicks("fp" + i, backoff);
                Assert.That(jitter, Is.InRange(0, backoff >> 3), "抖动幅度限定在退避时长的 1/8 内");
                seen.Add(jitter);
            }
            Assert.That(seen.Count, Is.GreaterThan(1), "抖动必须真的散开相位，否则全体同相位同时重试");
            Assert.That(RetryBackoffRules.JitterTicks(null, 0), Is.Zero);
        }

        [Test] public void TheFingerprintDeliberatelyHasNoTickComponent()
        {
            // 结构性防呆：指纹的入参里根本没有 Tick。同一组有意义修订必须恒得同一指纹，
            // 否则每次重算都像"条件变了"，退避形同不存在。
            string a = RetryBackoffRules.Fingerprint("r3", "s9", 5000, "Adult", 7);
            string b = RetryBackoffRules.Fingerprint("r3", "s9", 5000, "Adult", 7);
            Assert.That(b, Is.EqualTo(a));
            // 任一有意义分量变化 ⇒ 指纹变化（这是"真实相关变化"的判定基础）。
            Assert.That(RetryBackoffRules.Fingerprint("r4", "s9", 5000, "Adult", 7), Is.Not.EqualTo(a));
            Assert.That(RetryBackoffRules.Fingerprint("r3", "s9", 5001, "Adult", 7), Is.Not.EqualTo(a));
            Assert.That(RetryBackoffRules.Fingerprint("r3", "s9", 5000, "Child", 7), Is.Not.EqualTo(a));
            Assert.That(RetryBackoffRules.Fingerprint("r3", "s9", 5000, "Adult", 8), Is.Not.EqualTo(a));
        }

        [Test] public void OnlySupplyAndFundingFailuresEnterTheBackoffLedger()
        {
            // 判据已并入《09》§4 的权威七类表（FailureClassRules），此处只钉"退避账进门条件"。
            Assert.That(FailureClassRules.IsBackoff("NoFoodInStock"), Is.True);
            Assert.That(FailureClassRules.IsBackoff("InsufficientFunds"), Is.True);
            Assert.That(FailureClassRules.IsBackoff("HandOverFailed"), Is.True);
            Assert.That(FailureClassRules.IsBackoff("CounterUnavailable"), Is.True);
            // 证据不足：不得记失败，否则玩家会在没有报价时看到莫名其妙的冷却。
            Assert.That(FailureClassRules.IsBackoff("NoPublishedPrice"), Is.False);
            // 本就不需要买：不是失败。
            Assert.That(FailureClassRules.IsBackoff("NativeFoodFirst"), Is.False);
            Assert.That(FailureClassRules.IsBackoff("OutsideHours"), Is.False);
            Assert.That(FailureClassRules.IsBackoff(null), Is.False, "未知原因一律不计失败");
        }

        [Test] public void EmergencyAndRelevantChangeBothBypassTheCooldown()
        {
            // 文档："原版紧急保护不受经济冷却限制"；"有真实相关变化可提前复核"。
            Assert.That(RetryBackoffRules.MayRetry(now: 100, nextRetryTick: 5000, relevantChange: false, emergency: true), Is.True);
            Assert.That(RetryBackoffRules.MayRetry(100, 5000, relevantChange: true, emergency: false), Is.True);
            Assert.That(RetryBackoffRules.MayRetry(100, 5000, false, false), Is.False, "冷却未到且无变化 ⇒ 不放行");
            Assert.That(RetryBackoffRules.MayRetry(5000, 5000, false, false), Is.True, "冷却到点即放行");
        }
    }
}

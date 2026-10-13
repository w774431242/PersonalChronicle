using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Learning;
using PersonalChronicle.Domain.PawnEconomy.Planning;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// M9 学习账本：规则的**持账方**。断言的是"只进不退的账"本身 ——
    /// 额度按维度累计、摘要按 id 去重、被拒的摘要不消耗唯一性、新的一天才清额度。
    /// </summary>
    [TestFixture]
    public sealed class StyleLedgerTests
    {
        private const string Dim = "ReliabilityPreference";

        [Test] public void OneDeliveryMovesTheOffsetByTheFusionFraction()
        {
            var ledger = new StyleLedger();
            Assert.That(ledger.Deliver(Dim, "s1", 0.1f, 0.5f, true), Is.EqualTo(StyleLearningCode.Applied));
            Assert.That(ledger.OffsetOf(Dim), Is.EqualTo(StyleLearningRules.SummaryFusion * 0.1f).Within(1e-5f));
            Assert.That(ledger.ProcessedSummaries, Is.EqualTo(1));
        }

        [Test] public void TheSameSummaryIdNeverAppliesTwiceAcrossTheWholeLedger()
        {
            // 去重是**账本级**的：换维度投递同一条摘要 id 也应当被拒。
            var ledger = new StyleLedger();
            Assert.That(ledger.Deliver(Dim, "s1", 0.1f, 0.5f, true), Is.EqualTo(StyleLearningCode.Applied));
            Assert.That(ledger.Deliver(Dim, "s1", 0.1f, 0.5f, true), Is.EqualTo(StyleLearningCode.ReplaySummary));
            Assert.That(ledger.Deliver("TimeValue", "s1", 0.1f, 0.5f, true), Is.EqualTo(StyleLearningCode.ReplaySummary));
            Assert.That(ledger.OffsetOf(Dim), Is.EqualTo(StyleLearningRules.SummaryFusion * 0.1f).Within(1e-5f),
                "重复投递不得产生额外漂移");
        }

        [Test] public void TheDailyBudgetIsCountedPerDimensionAndResetsOnlyOnANewDay()
        {
            var ledger = new StyleLedger();
            for (int i = 0; i < 10; i++) ledger.Deliver(Dim, "s" + i, 5f, 0.5f, true);
            Assert.That(ledger.UsedToday(Dim), Is.EqualTo(StyleLearningRules.MaximumDailyChange).Within(1e-5f),
                "无论投递多少条，当日累计不得超过原型上限");
            Assert.That(ledger.Deliver(Dim, "extra", 5f, 0.5f, true), Is.EqualTo(StyleLearningCode.DailyBudgetExhausted));
            // 另一个维度有自己的额度（额度是按维度计，不是全局共享）。
            Assert.That(ledger.Deliver("TimeValue", "other", 5f, 0.5f, true), Is.EqualTo(StyleLearningCode.Applied));
            // 跨日：日序号必须**真的前进**才清额度（CHG-20261009-ECON-M9-S6 起 BeginDay 带日序号）。
            Assert.That(ledger.BeginDay(1), Is.True);
            Assert.That(ledger.Deliver(Dim, "next-day", 0.1f, 0.5f, true), Is.EqualTo(StyleLearningCode.Applied));
        }

        [Test] public void ARejectedSummaryDoesNotConsumeItsUniqueness()
        {
            // 被拒（此处为维度未校准）时不记为已处理 —— 否则该证据永久失效，
            // 次日额度恢复或维度启用后也无法生效。
            var ledger = new StyleLedger();
            Assert.That(ledger.Deliver(Dim, "s1", 0.1f, 0.5f, dimensionEnabled: false),
                Is.EqualTo(StyleLearningCode.DimensionDisabled));
            Assert.That(ledger.ProcessedSummaries, Is.Zero);
            Assert.That(ledger.Deliver(Dim, "s1", 0.1f, 0.5f, true), Is.EqualTo(StyleLearningCode.Applied));
        }

        [Test] public void MalformedInputIsReportedAsNoEvidenceAndChangesNothing()
        {
            var ledger = new StyleLedger();
            Assert.That(ledger.Deliver(null, "s1", 0.1f, 0.5f, true), Is.EqualTo(StyleLearningCode.NoEvidence));
            Assert.That(ledger.Deliver(Dim, "  ", 0.1f, 0.5f, true), Is.EqualTo(StyleLearningCode.NoEvidence));
            Assert.That(ledger.OffsetOf(Dim), Is.Zero);
            Assert.That(ledger.ProcessedSummaries, Is.Zero);
        }

        [Test] public void TheDimensionCountIsBounded()
        {
            var ledger = new StyleLedger();
            for (int i = 0; i < StyleLedger.MaximumDimensions; i++)
                Assert.That(ledger.Deliver("d" + i, "s" + i, 0.1f, 0.5f, true), Is.EqualTo(StyleLearningCode.Applied));
            Assert.That(ledger.Deliver("overflow", "sx", 0.1f, 0.5f, true), Is.EqualTo(StyleLearningCode.NoEvidence),
                "八维是设计上界，账本不得无限增长");
        }

        [Test] public void TheLedgerActuallyFeedsThePolicyDimensions()
        {
            // 端到端：账本 → Policy → 维度属性。这是"接线真的通了"的唯一证据。
            var ledger = new StyleLedger();
            ledger.Deliver(Dim, "s1", 5f, 0.5f, true);
            var withLearning = new LifePolicy(null, ledger.Offsets);
            Assert.That(withLearning.ReliabilityPreference, Is.GreaterThan(.5f),
                "经历必须真的改变维度，而不只是被记账");
            Assert.That(new LifePolicy(null).ReliabilityPreference, Is.EqualTo(.5f),
                "无账本时保持显式中性默认");
        }
    }
}

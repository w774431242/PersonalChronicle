using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Learning;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// M9 §3 显式先验 Beta(1,1) 与有界平滑。断言的是**先验的作用**：
    /// 无样本必须落回中性、极端样本不得触到 0 或 1、"未知"不得被显示成"极不可靠"。
    /// </summary>
    [TestFixture]
    public sealed class CredibilityEstimateTests
    {
        [Test] public void WithNoEvidenceTheReadingIsExactlyNeutral()
        {
            // 这是先验存在的全部意义：没有证据 ≠ 极不可靠。
            Assert.That(new CredibilityEstimate(0, 0).Mean, Is.EqualTo(.5f).Within(1e-6f));
            Assert.That(new CredibilityEstimate(0, 0).BasisPoints, Is.EqualTo(5000));
        }

        [Test] public void ThePublishedFormulaIsFollowedExactly()
        {
            // mean = (1+onTime)/(2+knownOutcomes)
            Assert.That(new CredibilityEstimate(8, 8).Mean, Is.EqualTo(9f / 10f).Within(1e-6f));
            Assert.That(new CredibilityEstimate(0, 8).Mean, Is.EqualTo(1f / 10f).Within(1e-6f));
            Assert.That(new CredibilityEstimate(4, 10).Mean, Is.EqualTo(5f / 12f).Within(1e-6f));
        }

        [Test] public void ExtremeSamplesNeverReachZeroOrOne()
        {
            // 显式先验的另一个作用：再糟的记录也不判死刑，再好的记录也不封顶。
            for (int fails = 1; fails <= 50; fails++)
                Assert.That(new CredibilityEstimate(0, fails).Mean, Is.GreaterThan(0f));
            for (int wins = 1; wins <= 50; wins++)
                Assert.That(new CredibilityEstimate(wins, wins).Mean, Is.LessThan(1f));
        }

        [Test] public void ThePriorFadesAsRealSamplesArrive()
        {
            Assert.That(new CredibilityEstimate(0, 0).PriorWeight, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(new CredibilityEstimate(0, 8).PriorWeight, Is.EqualTo(2f / 10f).Within(1e-6f));
            Assert.That(new CredibilityEstimate(0, 8).PriorWeight,
                Is.LessThan(new CredibilityEstimate(0, 2).PriorWeight));
        }

        [Test] public void GarbageInputFallsBackToNeutralInsteadOfAnExtremeJudgement()
        {
            // 脏数据不得产生极端判断：越界/矛盾一律归零样本。
            Assert.That(new CredibilityEstimate(5, 3).Mean, Is.EqualTo(.5f), "onTime > knownOutcomes 是矛盾输入");
            Assert.That(new CredibilityEstimate(-1, 4).Mean, Is.EqualTo(.5f));
            Assert.That(new CredibilityEstimate(0, -4).Mean, Is.EqualTo(.5f));
        }

        [Test] public void TheSummaryNowReportsThePriorBasedReadingNotTheRawRatio()
        {
            // 端到端：摘要 → 估计。裸成功率在这里会是 0，先验口径必须是 5000。
            Assert.That(new ExperienceSummary("inst:1").ReliabilityBasisPoints, Is.EqualTo(5000));
            var one = new ExperienceSummary("inst:1").Record(true, 0, 100);
            Assert.That(one.ReliabilityBasisPoints, Is.EqualTo(6667), "(1+1)/(2+1) = 2/3");
        }

        [Test] public void DurationSmoothingUsesThePublishedAlphaAndAdoptsTheFirstObservation()
        {
            Assert.That(DurationSmoothing.Blend(previousEstimate: 0f, knownSamples: 0, observed: 500f), Is.EqualTo(500f),
                "无旧估计时直接采用观测值，不得向一个不存在的老估计融合");
            Assert.That(DurationSmoothing.Blend(500f, 3, 1000f), Is.EqualTo(600f).Within(1e-4f),
                "alpha=0.20 ⇒ 500 + 0.2×(1000−500) = 600");
        }

        [Test] public void LowEvidenceIsJudgedAgainstACallerSuppliedThreshold()
        {
            // 设计只说"样本少显示 LowEvidence"，没给数值 ⇒ 门槛必须由调用方给，不在此处发明。
            Assert.That(DurationSmoothing.IsLowEvidence(knownSamples: 2, minimumSamples: 5), Is.True);
            Assert.That(DurationSmoothing.IsLowEvidence(knownSamples: 5, minimumSamples: 5), Is.False);
        }
    }
}

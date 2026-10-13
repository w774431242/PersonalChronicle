using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Learning;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// M9 有界慢速学习。每条断言都对应设计文档明列的一条约束；
    /// 缺任何一条都会让"经历修正人格"退化成"反复平滑同一份旧证据"。
    /// </summary>
    [TestFixture]
    public sealed class StyleLearningTests
    {
        [Test] public void ASummaryLearnsOnlyAFractionOfWhatItObserved()
        {
            // 观察差取 0.1，使**融合系数**成为约束；若取 1.0 会先被当日上限 0.03 卡住，测不到融合系数本身。
            var code = StyleLearningRules.Plan(true, false, false, 0f, 0.1f, out float delta);
            Assert.That(code, Is.EqualTo(StyleLearningCode.Applied));
            Assert.That(delta, Is.EqualTo(StyleLearningRules.SummaryFusion * 0.1f).Within(1e-5f),
                "一次摘要把维度拉到位就不是慢调，而是即时情境");
        }

        [Test] public void TheDailyCapBindsBeforeFullStrengthEvidence()
        {
            // 原型参数下 0.03 < 0.10：满强度证据（观察差 1.0）也只能走到当日上限。
            // 钉住这个大小关系 —— 否则有人调大融合系数后会误以为"一次就能学满"。
            Assert.That(StyleLearningRules.MaximumDailyChange, Is.LessThan(StyleLearningRules.SummaryFusion));
            StyleLearningRules.Plan(true, false, false, 0f, 1f, out float delta);
            Assert.That(delta, Is.EqualTo(StyleLearningRules.MaximumDailyChange).Within(1e-5f));
        }

        [Test] public void TheSameSummaryNeverDriftsTwice()
        {
            var code = StyleLearningRules.Plan(true, summaryAlreadyProcessed: true, false, 0f, 1f, out float delta);
            Assert.That(code, Is.EqualTo(StyleLearningCode.ReplaySummary));
            Assert.That(delta, Is.Zero, "同一摘要 ID 只处理一次；否则重复投递同一条经历会持续漂移");
        }

        [Test] public void TimeGoingBackwardsDoesNotRefundTheDailyBudget()
        {
            var code = StyleLearningRules.Plan(true, false, timeWentBackwards: true, 0.03f, 1f, out float delta);
            Assert.That(code, Is.EqualTo(StyleLearningCode.TimeWentBackwards));
            Assert.That(delta, Is.Zero, "读档/时间回退不能重获同一日额度");
        }

        [Test] public void TheDailyCapIsNeverOvershotAndIsNotShrunkToFit()
        {
            // 已用 0.02，剩余 0.01；本次想学 0.10（融合系数×1.0）⇒ 只能 0.01。
            var code = StyleLearningRules.Plan(true, false, false, 0.02f, 1f, out float delta);
            Assert.That(code, Is.EqualTo(StyleLearningCode.Applied));
            Assert.That(delta, Is.EqualTo(0.01f).Within(1e-5f));

            // 额度用尽 ⇒ 直接拒绝，**不得**写回 0 以外的任何值。
            var exhausted = StyleLearningRules.Plan(true, false, false, StyleLearningRules.MaximumDailyChange, 1f, out float none);
            Assert.That(exhausted, Is.EqualTo(StyleLearningCode.DailyBudgetExhausted));
            Assert.That(none, Is.Zero);
        }

        [Test] public void NegativeEvidencePullsTheOtherWayWithTheSameBoundedMagnitude()
        {
            var code = StyleLearningRules.Plan(true, false, false, 0f, -0.1f, out float delta);
            Assert.That(code, Is.EqualTo(StyleLearningCode.Applied));
            Assert.That(delta, Is.EqualTo(-StyleLearningRules.SummaryFusion * 0.1f).Within(1e-5f),
                "负向证据与正向证据必须同样受限，且方向相反");
        }

        [Test] public void AnUncalibratedDimensionStaysUntouched()
        {
            var code = StyleLearningRules.Plan(dimensionEnabled: false, false, false, 0f, 1f, out float delta);
            Assert.That(code, Is.EqualTo(StyleLearningCode.DimensionDisabled));
            Assert.That(delta, Is.Zero, "未经校准的学习维度可先禁用 —— 宁可不学，也不用未校准规则改人格");
        }

        [Test] public void NoDifferenceAndNonFiniteEvidenceBothProduceNothing()
        {
            Assert.That(StyleLearningRules.Plan(true, false, false, 0f, 0f, out float zero), Is.EqualTo(StyleLearningCode.NoEvidence));
            Assert.That(zero, Is.Zero);
            Assert.That(StyleLearningRules.Plan(true, false, false, 0f, float.NaN, out _), Is.EqualTo(StyleLearningCode.NoEvidence));
            Assert.That(StyleLearningRules.Plan(true, false, false, 0f, float.PositiveInfinity, out _), Is.EqualTo(StyleLearningCode.NoEvidence));
        }

        [Test] public void ApplyingAnOffsetKeepsTheCombinedDimensionInsideTheUnitInterval()
        {
            // 偏移可超出 0–1 表示强偏好，但合成值永远不越界（设计："所有维度取 0–1"）。
            Assert.That(StyleLearningRules.Effective(0.97f, StyleLearningRules.ApplyOffset(0.97f, 0f, 0.5f)), Is.EqualTo(1f));
            Assert.That(StyleLearningRules.Effective(0.03f, StyleLearningRules.ApplyOffset(0.03f, 0f, -0.5f)), Is.EqualTo(0f));
            Assert.That(StyleLearningRules.Effective(0.5f, StyleLearningRules.ApplyOffset(0.5f, 0f, 0.03f)), Is.EqualTo(0.53f).Within(1e-5f));
        }

        [Test] public void TheDailyCapAllowsManySmallSummariesButOnlyUpToTheLimit()
        {
            // 累计学习必须收敛到当日上限，且过程中每一笔都不超限。
            float used = 0f, offset = 0f;
            for (int i = 0; i < 20; i++)
            {
                if (StyleLearningRules.Plan(true, false, false, used, 1f, out float delta) != StyleLearningCode.Applied) break;
                used += delta > 0f ? delta : -delta;
                offset = StyleLearningRules.ApplyOffset(0.5f, offset, delta);
            }
            Assert.That(used, Is.LessThanOrEqualTo(StyleLearningRules.MaximumDailyChange + 1e-5f),
                "无论投递多少条摘要，当日总变化不得超过原型上限");
            Assert.That(offset, Is.LessThanOrEqualTo(StyleLearningRules.MaximumDailyChange + 1e-5f));
        }
    }
}

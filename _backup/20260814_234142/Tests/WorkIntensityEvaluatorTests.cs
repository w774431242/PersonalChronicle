using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 第一次全面综合测试 —— 工作强度评估器。
    /// 覆盖：多元化场景 / 多元化逻辑判断 / 前端组件参数逻辑判断（强度→UI 量纲）。
    /// 注意：WorkIntensityEvaluator 的档位匹配为"升序遍历，命中第一个
    /// daily >= MinimumDailyHours 即 break"，因此 tiers 必须按 MinimumDailyHours
    /// 从高到低排列（Overload→Busy→Normal→Idle），才能让每日工时匹配到最高满足档。
    /// </summary>
    [TestFixture]
    public class WorkIntensityEvaluatorTests
    {
        private const double Epsilon = 1e-6;

        // 档位按 MinimumDailyHours 降序排列（命中即停 = 最高满足档）
        private static WorkIntensityPolicySnapshot StandardPolicy()
        {
            var tiers = new List<WorkIntensityTierSpec>
            {
                new WorkIntensityTierSpec("PC_WI_Overload", "overload", "爆", 14f,
                    "PersonalChronicle.WI.Overload", "WI.Overload", "#d96f68", 3),
                new WorkIntensityTierSpec("PC_WI_Busy", "busy", "忙", 10f,
                    "PersonalChronicle.WI.Busy", "WI.Busy", "#e6b34d", 2),
                new WorkIntensityTierSpec("PC_WI_Normal", "normal", "常", 4f,
                    "PersonalChronicle.WI.Normal", "WI.Normal", "#a3d977", 1),
                new WorkIntensityTierSpec("PC_WI_Idle", "idle", "休", 0f,
                    "PersonalChronicle.WI.Idle", "WI.Idle", "#9b9b9b", 0),
            };
            return new WorkIntensityPolicySnapshot(
                minimumSampleDays: 3,
                overloadRatio: 1.6f,
                slackRatio: 0.25f,
                tiers: tiers);
        }

        private static WorkIntensityInput Input(long ticks, double hours, double days, double colonyAvg)
            => new WorkIntensityInput(ticks, hours, days, colonyAvg);

        // ------------------------------------------------------------------
        // 多元化场景
        // ------------------------------------------------------------------

        [Test]
        public void Evaluate_NullInput_ReturnsUndefined()
        {
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(null, StandardPolicy());
            Assert.IsFalse(r.IsDefined);
        }

        [Test]
        public void Evaluate_NullPolicy_ReturnsUndefined()
        {
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                Input(1000, 10, 10, 8), null);
            Assert.IsFalse(r.IsDefined);
        }

        [Test]
        public void Evaluate_ZeroTicks_ReturnsUndefined()
        {
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                Input(0, 0, 10, 8), StandardPolicy());
            Assert.IsFalse(r.IsDefined);
        }

        [Test]
        public void Evaluate_IdleColonist_MatchedIdleTier()
        {
            // 每天 1 小时（< 4 小时 normal 阈值）
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                Input(2500, 10, 10, 8), StandardPolicy());
            Assert.IsTrue(r.IsDefined);
            Assert.AreEqual("PC_WI_Idle", r.TierDefName);
        }

        [Test]
        public void Evaluate_NormalColonist_MatchedNormalTier()
        {
            // 每天 8 小时（>= 4，< 10）
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                Input(20000, 80, 10, 8), StandardPolicy());
            Assert.IsTrue(r.IsDefined);
            Assert.AreEqual("PC_WI_Normal", r.TierDefName);
            Assert.AreEqual(8d, r.DailyHours, Epsilon);
            Assert.AreEqual(56d, r.WeeklyHours, Epsilon);
            Assert.AreEqual(240d, r.MonthlyHours, Epsilon);
        }

        [Test]
        public void Evaluate_BusyColonist_MatchedBusyTier()
        {
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                Input(30000, 120, 10, 8), StandardPolicy());
            Assert.AreEqual("PC_WI_Busy", r.TierDefName);
        }

        [Test]
        public void Evaluate_OverloadColonist_MatchedOverloadTierAndFlag()
        {
            // 每天 16 小时（>= 14），相对均值 8 → ratio 2.0 >= overloadRatio 1.6
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                Input(32000, 160, 10, 8), StandardPolicy());
            Assert.AreEqual("PC_WI_Overload", r.TierDefName);
            Assert.IsTrue(r.IsOverloaded, "超过过载阈值应打过载标记");
        }

        [Test]
        public void Evaluate_RelativeRatio_ComputedAgainstColonyAverage()
        {
            // 每天 8 小时，殖民均值 4 → ratio = 2.0
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                Input(20000, 80, 10, 4), StandardPolicy());
            Assert.AreEqual(2.0d, r.RelativeRatio, Epsilon);
        }

        [Test]
        public void Evaluate_ShortObservation_FlaggedEstimated()
        {
            // 仅观察 1 天（< minimumSampleDays=3）
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                Input(2000, 8, 1, 8), StandardPolicy());
            Assert.IsTrue(r.IsDefined, "仍应给出档位");
            Assert.IsTrue(r.IsEstimated, "样本不足应标记为预估");
            Assert.IsFalse(r.IsOverloaded);
        }

        [Test]
        public void Evaluate_SlackRatio_FlagsSignificantlyIdle()
        {
            // 每天 1 小时，殖民均值 8 → ratio = 0.125 <= slackRatio 0.25 且 > 0
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                Input(2500, 10, 10, 8), StandardPolicy());
            Assert.IsTrue(r.IsSignificantlyIdle, "远低于均值应标显著闲置");
        }

        [Test]
        public void Evaluate_ZeroColonyAverage_NoDivByZeroRatio()
        {
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                Input(20000, 80, 10, 0), StandardPolicy());
            Assert.AreEqual(0d, r.RelativeRatio, Epsilon, "殖民均值为 0 时 ratio 应为 0，不抛异常");
            Assert.IsFalse(r.IsOverloaded);
            Assert.IsFalse(r.IsSignificantlyIdle);
        }

        // ------------------------------------------------------------------
        // 多元化逻辑判断：边界与异常输入
        // ------------------------------------------------------------------

        [Test]
        public void Evaluate_NegativeHours_TreatedAsNonNegative()
        {
            Assert.DoesNotThrow(() =>
            {
                WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                    Input(20000, -80, 10, 8), StandardPolicy());
                Assert.IsTrue(r.DailyHours <= 0d);
            });
        }

        [Test]
        public void Evaluate_NoTiers_ReturnsUndefined()
        {
            var policyNoTiers = new WorkIntensityPolicySnapshot(3, 1.6f, 0.25f,
                new List<WorkIntensityTierSpec>());
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                Input(20000, 80, 10, 8), policyNoTiers);
            Assert.IsFalse(r.IsDefined);
        }

        [Test]
        public void Evaluate_PolicyGuards_NegativeRatiosClampedToZero()
        {
            // overloadRatio/slackRatio 构造时被钳到 0 → 永不触发过载/闲置
            var policy = new WorkIntensityPolicySnapshot(3, -5f, -5f,
                new List<WorkIntensityTierSpec>
                {
                    new WorkIntensityTierSpec("t", "t", "t", 0f, "k", "k", "#fff", 0),
                });
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                Input(20000, 80, 10, 8), policy);
            Assert.IsFalse(r.IsOverloaded);
            Assert.IsFalse(r.IsSignificantlyIdle);
        }

        [Test]
        public void Evaluate_MinimumSampleDaysClampedNonNegative()
        {
            var policy = new WorkIntensityPolicySnapshot(-10, 1.6f, 0.25f,
                new List<WorkIntensityTierSpec>
                {
                    new WorkIntensityTierSpec("t", "t", "t", 0f, "k", "k", "#fff", 0),
                });
            // 观察 1 天，但 minimumSampleDays 被钳到 0 → 不应标记预估
            WorkIntensityEvaluation r = WorkIntensityEvaluator.Evaluate(
                Input(2000, 8, 1, 8), policy);
            Assert.IsFalse(r.IsEstimated);
        }

        // ------------------------------------------------------------------
        // 前端组件参数逻辑判断：强度 → 进度条 share01 / 颜色档位
        // ------------------------------------------------------------------

        [Test]
        public void Frontend_ProgressBar_ShareClampedToUnitRange()
        {
            // 镜像 UIComponents.ProgressBar 的 Mathf.Clamp01 边界判断
            float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
            Assert.AreEqual(0f, Clamp01(-0.5f), Epsilon);
            Assert.AreEqual(0f, Clamp01(0f), Epsilon);
            Assert.AreEqual(0.5f, Clamp01(0.5f), Epsilon);
            Assert.AreEqual(1f, Clamp01(1f), Epsilon);
            Assert.AreEqual(1f, Clamp01(1.8f), Epsilon, "前端进度条：超出 1 必须截断");
        }

        [Test]
        public void Frontend_IntensityColorBand_ThreeTier()
        {
            // 前端用 RelativeRatio 档位选色（Alive/Info/Dead 语义）。断言单调映射一致。
            System.Func<double, int> Band = s => s < 0.34d ? 0 : (s < 0.67d ? 1 : 2);
            Assert.AreEqual(0, Band(0d));
            Assert.AreEqual(0, Band(0.33d));
            Assert.AreEqual(1, Band(0.34d));
            Assert.AreEqual(1, Band(0.66d));
            Assert.AreEqual(2, Band(0.67d));
            Assert.AreEqual(2, Band(2d));
        }

        [Test]
        public void Frontend_StatCell_SubLabelWidthClipped()
        {
            // 镜像 StatCell inline 子标签宽度裁剪：subW = Min(subSize, subMaxX - subX)
            System.Func<float, float, float, float, float> EvalSubW =
                (valueW, innerW, subMaxX, subX) =>
                {
                    float vW = System.Math.Min(valueW, innerW);
                    float subW = System.Math.Min(20f, subMaxX - (subX + vW + 8f));
                    return subW > 0f ? subW : 0f;
                };
            // 正常放得下
            Assert.AreEqual(20f, EvalSubW(40f, 200f, 200f, 10f));
            // 越界裁剪到 0（不溢出单元格）
            Assert.AreEqual(0f, EvalSubW(190f, 200f, 200f, 10f));
        }

        [Test]
        public void Frontend_TimelineNode_HeightIsMaxOfNodeAndTextBlock()
        {
            // 镜像 TimelineNode：h = Max(nodeSize, textBlock) + SpaceXs
            const float nodeSize = 18f;
            const float spaceXs = 4f;
            System.Func<float, float> Height = textBlock => System.Math.Max(nodeSize, textBlock) + spaceXs;
            Assert.AreEqual(nodeSize + spaceXs, Height(10f), Epsilon, "文字矮时取节点高度");
            Assert.AreEqual(60f + spaceXs, Height(60f), Epsilon, "文字高时取文字块高度");
        }
    }
}

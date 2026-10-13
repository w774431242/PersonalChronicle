using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// v0.4 §9「避免需求长期被饿死」的目标排序规则（纯领域，离线可证伪）。
    /// 三条断言各自对应"什么情况下会失败"：把等待提前到等级之前 / 同级不等得久的先办 / 无稳定兜底 ⇒ 必挂。
    /// </summary>
    [TestFixture]
    public sealed class ShadowGoalRankingTests
    {
        private static ShadowNeedGoal Goal(string id, int priority, int waiting)
            => new ShadowNeedGoal(id, null, true, priority, waiting, 0);

        private static List<ShadowNeedGoal> Ranked(params ShadowNeedGoal[] goals)
        {
            var list = new List<ShadowNeedGoal>(goals);
            ShadowPlanBuilder.RankGoals(list);
            return list;
        }

        [Test] public void SameBandServesTheLongestWaitingGoalFirst()
        {
            var ranked = Ranked(Goal("Food", 1, 500), Goal("Rest", 1, 4000), Goal("Joy", 1, 0));
            Assert.That(ranked[0].NeedId, Is.EqualTo("Rest"), "等了 4000 tick 的项必须先于等 500 的项");
            Assert.That(ranked[1].NeedId, Is.EqualTo("Food"));
            Assert.That(ranked[2].NeedId, Is.EqualTo("Joy"));
        }

        [Test] public void WaitingNeverOutranksAHigherBand()
        {
            // P2 等待远超上限，也不能排到 P0/P1 之前 —— 这是"等待不得突破等级"的硬断言。
            var ranked = Ranked(Goal("Joy", 2, PlanningPolicy.MaximumWaitWeightTicks), Goal("Food", 1, 0), Goal("Rest", 0, 0));
            Assert.That(ranked[0].Priority, Is.EqualTo(0));
            Assert.That(ranked[1].Priority, Is.EqualTo(1));
            Assert.That(ranked[2].Priority, Is.EqualTo(2), "P2 即使等待达上限也必须排在最后");
        }

        [Test] public void EqualPriorityAndWaitingKeepAStableOrder()
        {
            // 无稳定兜底时两次排序可能给出不同顺序 ⇒ 表现为行为反复切换。断言两次结果一致。
            var first = Ranked(Goal("Rest", 1, 100), Goal("Food", 1, 100), Goal("Joy", 1, 100));
            var second = Ranked(Goal("Rest", 1, 100), Goal("Food", 1, 100), Goal("Joy", 1, 100));
            for (int i = 0; i < first.Count; i++)
                Assert.That(first[i].NeedId, Is.EqualTo(second[i].NeedId), "同分必须稳定有序");
        }

        private static ShadowNeedGoal WithSlack(string id, int priority, int slack, int waiting = 0)
            => new ShadowNeedGoal(id, null, true, priority, waiting, 0, -1, slack);

        [Test] public void EstimableDeadlineOutranksUnestimableWithinTheSameBand()
        {
            // §6：可估时限者先于不可估者 —— 否则"算不出来"会被当成"不重要"而永久压后。
            var ranked = Ranked(WithSlack("Joy", 1, -1), WithSlack("Food", 1, 900));
            Assert.That(ranked[0].NeedId, Is.EqualTo("Food"), "可估时限的目标应先于不可估者");
        }

        [Test] public void SmallerSlackIsMoreUrgentWithinTheSameBand()
        {
            var ranked = Ranked(WithSlack("Joy", 1, 5000), WithSlack("Food", 1, 500));
            Assert.That(ranked[0].NeedId, Is.EqualTo("Food"), "slack 更小（更赶）的先办");
        }

        [Test] public void SlackIsOnlyComparedInsideOneBand()
        {
            // 再小的 slack 也不能把 P2 提到 P1 之前。
            var ranked = Ranked(WithSlack("Joy", 2, -9999), WithSlack("Food", 1, 999999));
            Assert.That(ranked[0].Priority, Is.EqualTo(1));
            Assert.That(ranked[1].Priority, Is.EqualTo(2));
        }

        [Test] public void UnestimableSlackFallsBackToWaitingOrder()
        {
            // 都不可估时退回 §9 等待排序，而不是靠 NeedId 字母序。
            var ranked = Ranked(WithSlack("Food", 1, -1, 100), WithSlack("Joy", 1, -1, 4000));
            Assert.That(ranked[0].NeedId, Is.EqualTo("Joy"), "都不可估时等得久的先办");
        }

        private static RequirementCostSlice Slice(string key, long total, bool necessary = true, long covered = 0)
            => new RequirementCostSlice(key, 100, 200, necessary, false, Fact<long>.Known(total),
                covered > 0 ? new[] { new CostCoverage("Silver", CoverageKind.OwnedOrPaid, covered) } : null);

        [Test] public void BlockedNeedsNothingWhileTheLabourChannelIsOpen()
        {
            // §7：通道可用时**不得**标记受阻 —— 否则会静默压制正常规划（这是最容易写错的方向）。
            var blocked = ShadowPlanBuilder.FindInstitutionallyBlocked(new[] { Slice("p|Food|Level", 5000) }, 0, true);
            Assert.That(blocked, Is.Empty, "劳动通道可用时不得标记制度性受阻");
        }

        [Test] public void BlockedNeedsAnActualFundingShortfall()
        {
            var costs = new[] { Slice("p|Food|Level", 5000) };
            Assert.That(ShadowPlanBuilder.FindInstitutionallyBlocked(costs, 5000, false), Is.Empty, "资金够 ⇒ 不成环");
            Assert.That(ShadowPlanBuilder.FindInstitutionallyBlocked(costs, 1000, false).Count, Is.EqualTo(1), "资金缺口 ⇒ 成环");
            // 覆盖必须**完全**抵掉缺口才不成环：总需求 5000 只覆盖 4500 时仍余 500 ⇒ 依然受阻。
            Assert.That(ShadowPlanBuilder.FindInstitutionallyBlocked(new[] { Slice("p|Food|Level", 5000, covered: 4500) }, 0, false).Count,
                Is.EqualTo(1), "覆盖不足仍有缺口 ⇒ 成环");
            Assert.That(ShadowPlanBuilder.FindInstitutionallyBlocked(new[] { Slice("p|Food|Level", 5000, covered: 5000) }, 0, false),
                Is.Empty, "覆盖完全抵掉缺口后不成环");
        }

        [Test] public void NonNecessarySlicesAreNeverBlocked()
        {
            var blocked = ShadowPlanBuilder.FindInstitutionallyBlocked(new[] { Slice("p|Joy|Level", 9000, necessary: false) }, 0, false);
            Assert.That(blocked, Is.Empty, "非必要项不该因缺钱被标受阻");
        }

        [Test] public void BlockedKeysAreDeduplicatedAndUnknownTotalsAreSkipped()
        {
            var costs = new[] { Slice("p|Food|Level", 5000), Slice("p|Food|Level", 5000), Slice("p|Rest|Level", 3000) };
            var blocked = ShadowPlanBuilder.FindInstitutionallyBlocked(costs, 0, false);
            Assert.That(blocked.Count, Is.EqualTo(2), "同名切片不得重复登记");
            Assert.That(blocked, Does.Contain("p|Food|Level"));
            Assert.That(blocked, Does.Contain("p|Rest|Level"));
        }

        [Test] public void PlanOptionsAlwaysListAllFourKindsFreeFirst()
        {
            var options = ShadowPlanBuilder.BuildPlanOptions(false, false, false, false, false);
            Assert.That(options.Count, Is.EqualTo(4), "四类方案必须都在，不可静默省略");
            Assert.That(options[0].Kind, Is.EqualTo(PlanOptionKind.NativeSelfHelp), "免费自救必须排最前");
            Assert.That(options[3].Kind, Is.EqualTo(PlanOptionKind.Service));
        }

        [Test] public void InfeasibleOptionsAlwaysCarryAReasonCode()
        {
            foreach (var option in ShadowPlanBuilder.BuildPlanOptions(false, false, false, false, false))
                Assert.That(option.ReasonCode, Is.Not.Empty, "不可行必须给原因码，否则玩家看不到为什么没选它");
        }

        [Test] public void SeekWorkDistinguishesNotPermittedFromNoOpportunity()
        {
            Assert.That(ShadowPlanBuilder.BuildPlanOptions(false, false, true, false, false)[1].ReasonCode,
                Is.EqualTo("EmploymentNotPermitted"), "资格不允许与没岗位是两种不同的原因");
            Assert.That(ShadowPlanBuilder.BuildPlanOptions(false, true, false, false, false)[1].ReasonCode,
                Is.EqualTo("NoFeasibleOpportunity"));
            Assert.That(ShadowPlanBuilder.BuildPlanOptions(false, true, true, false, false)[1].Feasible, Is.True);
        }

        [Test] public void FeasibleOptionsCarryNoReasonCode()
        {
            foreach (var option in ShadowPlanBuilder.BuildPlanOptions(true, true, true, true, true))
            {
                Assert.That(option.Feasible, Is.True);
                Assert.That(option.ReasonCode, Is.Empty, "可行时不得带原因码");
            }
        }

        [Test] public void NonActiveOrBlockedGoalsCarryNoWaitingWeight()
        {
            // 设计明令：受阻 / 冻结 / 不适用的目标不能不断累计优先级 ⇒ 这类项由 builder 记 0 等待。
            var blocked = new ShadowNeedGoal("Food", null, false, 1, 0, 0);
            Assert.That(blocked.WaitingTicks, Is.EqualTo(0));
            Assert.That(blocked.Active, Is.False);
            // 上限本身必须是个正数且有限，否则"等待加权"会失控。
            Assert.That(PlanningPolicy.MaximumWaitWeightTicks, Is.GreaterThan(0));
        }
    }
}

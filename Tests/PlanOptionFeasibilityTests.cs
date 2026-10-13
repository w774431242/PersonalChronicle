using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// v0.9 §2 M4 方案比较 + M7 购买可行性的**原因码契约**。
    /// 每条断言都对应"什么情况下会失败"：三态被糊成一个通用码 / 可行却带码 /
    /// 服务借用岗位市场的原因码 / 选项顺序漂移 ⇒ 必挂。
    /// </summary>
    [TestFixture]
    public sealed class PlanOptionFeasibilityTests
    {
        private static RequirementCostSlice Slice(long total, bool necessary = true, long covered = 0)
        {
            var coverage = new List<CostCoverage>();
            if (covered > 0) coverage.Add(new CostCoverage("carriedFood:p", CoverageKind.OwnedOrPaid, covered));
            return new RequirementCostSlice("Food", 0, 60000, necessary, true, Fact<long>.Known(total), coverage);
        }

        private static PlanOption Option(List<PlanOption> options, PlanOptionKind kind)
            => options.Find(o => o.Kind == kind);

        // ── M7：购买方案的可行性与三态原因码 ───────────────────────────────────────────

        [Test] public void AffordablePricedConsumptionMakesPurchaseFeasibleAndCarriesNoReason()
        {
            Assert.That(ShadowPlanBuilder.PricedConsumptionFeasible(new[] { Slice(5000) }, true, 10000, out string code), Is.True);
            Assert.That(code, Is.Null, "可行方案不得带原因码，否则界面会出现自相矛盾的括号");
        }

        [Test] public void CoverageIsDeductedBeforeAffordabilityIsJudged()
        {
            // 覆盖算法必须与 FindInstitutionallyBlocked 一致：12000 的报价里有 8000 是随身食物抵掉的，
            // 自有现金只要 4000 就够。若这里漏算覆盖，玩家会被误报"买不起"。
            Assert.That(ShadowPlanBuilder.PricedConsumptionFeasible(new[] { Slice(12000, covered: 8000) }, true, 4000, out _), Is.True);
            Assert.That(ShadowPlanBuilder.PricedConsumptionFeasible(new[] { Slice(12000, covered: 8000) }, true, 3999, out _), Is.False);
        }

        [Test] public void UnaffordableQuoteIsDistinguishedFromAMissingQuote()
        {
            Assert.That(ShadowPlanBuilder.PricedConsumptionFeasible(new[] { Slice(5000) }, true, 100,
                out string poor), Is.False);
            Assert.That(poor, Is.EqualTo("ConsumptionUnaffordable"), "有报价但付不起");
            Assert.That(ShadowPlanBuilder.PricedConsumptionFeasible(new RequirementCostSlice[0], true, 999999,
                out string none), Is.False);
            Assert.That(none, Is.EqualTo("NoPricedConsumption"), "根本没有报价");
        }

        [Test] public void IncompleteCostsNeverClaimFeasibilityAndNeverClaimANoQuote()
        {
            // 成本未读全时既不能说可行，也不能声称"没有报价" —— 只能说读不到。
            Assert.That(ShadowPlanBuilder.PricedConsumptionFeasible(new[] { Slice(1) }, false, 999999, out string code), Is.False);
            Assert.That(code, Is.EqualTo("CostEstimateUnavailable"));
        }

        [Test] public void NonNecessaryOrUnknownCostsDoNotCountAsPricedConsumption()
        {
            Assert.That(ShadowPlanBuilder.PricedConsumptionFeasible(new[] { Slice(5000, necessary: false) }, true, 999999,
                out string notNecessary), Is.False);
            Assert.That(notNecessary, Is.EqualTo("NoPricedConsumption"));
            var unknown = new RequirementCostSlice("Food", 0, 60000, true, true, Fact<long>.Unknown("NoQuote"),
                new List<CostCoverage>());
            Assert.That(ShadowPlanBuilder.PricedConsumptionFeasible(new[] { unknown }, true, 999999, out _), Is.False,
                "报价未知的切片不得被当作已定价消费");
        }

        [Test] public void NullCostListIsSafe()
            => Assert.That(ShadowPlanBuilder.PricedConsumptionFeasible(null, true, 999999, out string code), Is.False);

        // ── M4：原因码与顺序 ──────────────────────────────────────────────────────────

        [Test] public void PurchaseCarriesThePreciseReasonGivenByTheCaller()
        {
            var purchase = Option(ShadowPlanBuilder.BuildPlanOptions(false, false, false, false, false, "ConsumptionUnaffordable"),
                PlanOptionKind.Purchase);
            Assert.That(purchase.Feasible, Is.False);
            Assert.That(purchase.ReasonCode, Is.EqualTo("ConsumptionUnaffordable"));
        }

        [Test] public void PurchaseFallsBackToNoPricedConsumptionWhenNoReasonIsGiven()
        {
            var purchase = Option(ShadowPlanBuilder.BuildPlanOptions(false, false, false, false, false), PlanOptionKind.Purchase);
            Assert.That(purchase.ReasonCode, Is.EqualTo("NoPricedConsumption"));
        }

        [Test] public void FeasiblePurchaseIgnoresAnySuppliedReason()
        {
            var purchase = Option(ShadowPlanBuilder.BuildPlanOptions(false, false, false, true, false, "mustBeIgnored"),
                PlanOptionKind.Purchase);
            Assert.That(purchase.Feasible, Is.True);
            Assert.That(purchase.ReasonCode, Is.Empty);
        }

        [Test] public void ServiceNeverBorrowsTheJobMarketReason()
        {
            var service = Option(ShadowPlanBuilder.BuildPlanOptions(false, false, false, false, false), PlanOptionKind.Service);
            Assert.That(service.ReasonCode, Is.EqualTo("ServiceNotImplemented"));
            Assert.That(service.ReasonCode, Is.Not.EqualTo("OpportunityNotPublished"),
                "服务缺的是供给机制而非岗位市场；借码会把玩家引向错误的排查方向");
        }

        [Test] public void OptionOrderIsFixed()
        {
            var options = ShadowPlanBuilder.BuildPlanOptions(false, false, false, true, false);
            Assert.That(options.Count, Is.EqualTo(4));
            Assert.That(options[0].Kind, Is.EqualTo(PlanOptionKind.NativeSelfHelp));
            Assert.That(options[1].Kind, Is.EqualTo(PlanOptionKind.SeekWork));
            Assert.That(options[2].Kind, Is.EqualTo(PlanOptionKind.Purchase));
            Assert.That(options[3].Kind, Is.EqualTo(PlanOptionKind.Service));
        }
    }
}

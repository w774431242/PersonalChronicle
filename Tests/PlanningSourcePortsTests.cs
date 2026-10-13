using System;
using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Application.PawnEconomy.Planning;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class PlanningSourcePortsTests
    {
        private static readonly PerceptionSubject Subject = new PerceptionSubject("p", 1);

        private sealed class ThrowingCostSource : IRequirementCostSource
        {
            public IReadOnlyList<RequirementCostSlice> ReadCosts(PerceptionSubject subject, int start, int end, out bool complete, out string incompleteReason)
            { complete = true; incompleteReason = string.Empty; throw new InvalidOperationException("SourceFailed"); }
        }
        private sealed class StubCostSource : IRequirementCostSource
        {
            private readonly IReadOnlyList<RequirementCostSlice> rows;
            private readonly bool published;
            private readonly string reason;
            public StubCostSource(IReadOnlyList<RequirementCostSlice> rows, bool published, string reason = "")
            { this.rows = rows; this.published = published; this.reason = reason; }
            public IReadOnlyList<RequirementCostSlice> ReadCosts(PerceptionSubject subject, int start, int end, out bool complete, out string incompleteReason)
            { complete = published; incompleteReason = reason; return rows; }
        }
        private sealed class StubWorkSource : IWorkOpportunitySource
        {
            private readonly IReadOnlyList<WorkOpportunity> rows;
            private readonly bool published;
            public StubWorkSource(IReadOnlyList<WorkOpportunity> rows, bool published) { this.rows = rows; this.published = published; }
            public IReadOnlyList<WorkOpportunity> ReadOpportunities(PerceptionSubject subject, int now, int deadline, out bool complete)
            { complete = published; return rows; }
        }
        private static RequirementCostSlice Cost(string key) => new RequirementCostSlice(key, 10, 120010, true, false, Fact<long>.Known(100));
        private static WorkOpportunity Offer(string id = "job")
            => new WorkOpportunity(id, Fact<bool>.Known(true), Fact<bool>.Known(true), Fact<bool>.Known(true), true, 1000, 500, 1200, 0);
        private static FactQueryResult Facts(int tick, string id)
        {
            var need = new NumericNeedFact("Food", "Food", "test", Fact<float>.Known(0.9f), Fact<float>.Known(1),
                true, "level", 1, tick, Fact<bool>.Known(false));
            var context = new PawnContextFact(SubjectLocation.SpawnedMap, 1, developmentalStage: Fact<string>.Known("Adult"),
                drafted: Fact<bool>.Known(false), downed: Fact<bool>.Known(false), mentalState: Fact<bool>.Known(false),
                playerForced: Fact<bool>.Known(false), timeAssignmentDefName: Fact<string>.Known("Anything"));
            var snapshot = new PawnFactSnapshot(id, 1, tick + 1, 1,
                new FactGroup<PawnContextFact>(CaptureState.Complete, context, tick, 1, "test"),
                new FactGroup<NeedFactSet>(CaptureState.Complete,
                    new NeedFactSet(new[] { need }, new[] { "Food" }, null, null, Fact<float>.Known(0.8f)), tick, 1, "test"));
            return new FactQueryResult(PerceptionReadiness.Ready, "Ready", true, snapshot);
        }

        [Test] public void UnpublishedPortsPublishNothingAndNeverClaimCompleteness()
        {
            PlanningSourceGuard.ReadCosts(new UnpublishedCostSource(), Subject, 10, 120010, out var costs, out bool costsComplete, out string costsReason);
            PlanningSourceGuard.ReadOpportunities(new UnpublishedOpportunitySource(), Subject, 10, 120010, out var offers, out bool offersComplete);
            Assert.That(costs, Is.Empty); Assert.That(costsComplete, Is.False);
            // LR-04：安全零也必须给出**可解释**的兜底原因码（不得留空串让界面无从显示）。
            Assert.That(costsReason, Is.EqualTo("CostEstimateUnavailable"));
            Assert.That(offers, Is.Empty); Assert.That(offersComplete, Is.False);
        }
        [Test] public void UnpublishedPortsKeepThePlanWaitingForARealQuoteInsteadOfInventingOne()
        {
            PlanningSourceGuard.ReadCosts(new UnpublishedCostSource(), Subject, 10, 120010, out var costs, out bool costsComplete, out string costsReason);
            PlanningSourceGuard.ReadOpportunities(new UnpublishedOpportunitySource(), Subject, 10, 120010, out var offers, out bool offersComplete);
            var input = new ShadowPlanningInput(Facts(10, "p"), new LifePolicy(null),
                Fact<FinancialAccountSnapshot>.Known(new FinancialAccountSnapshot("p", 5000)),
                costs, costsComplete, offers, offersComplete, Fact<bool>.Known(false), Fact<bool>.Known(true),
                null, costsReason);
            var plan = ShadowPlanBuilder.Build("p", 1, 1, 10, input, null);
            Assert.That(plan.Decision, Is.EqualTo(ShadowDecision.WaitingBudget));
            Assert.That(plan.Budget.State, Is.EqualTo(BudgetState.WaitingQuote));
            Assert.That(plan.SelectedOpportunity, Is.Null);
        }
        [Test] public void SourceExceptionIsIsolatedAndDegradesToNothingPublished()
        {
            PlanningSourceGuard.ReadCosts(new ThrowingCostSource(), Subject, 10, 120010, out var costs, out bool complete, out string reason);
            Assert.That(costs, Is.Empty); Assert.That(complete, Is.False);
            // 源给出原因后抛异常 ⇒ 必须回落兜底码，不得把半途原因带出去（LR-04）。
            Assert.That(reason, Is.EqualTo("CostEstimateUnavailable"));
        }
        [Test] public void NullRowsIncompleteOrOversizePublicationsAreRejectedWholesale()
        {
            PlanningSourceGuard.ReadCosts(new StubCostSource(new[] { Cost("a") }, false), Subject, 10, 120010, out var incomplete, out bool incompletePublished, out _);
            Assert.That(incomplete, Is.Empty); Assert.That(incompletePublished, Is.False);
            PlanningSourceGuard.ReadCosts(new StubCostSource(new RequirementCostSlice[] { null }, true), Subject, 10, 120010, out var nullRow, out bool nullPublished, out _);
            Assert.That(nullRow, Is.Empty); Assert.That(nullPublished, Is.False);
            PlanningSourceGuard.ReadCosts(new StubCostSource(null, true), Subject, 10, 120010, out var nothing, out bool nothingPublished, out _);
            Assert.That(nothing, Is.Empty); Assert.That(nothingPublished, Is.False);
            var oversize = new List<RequirementCostSlice>();
            for (int i = 0; i <= PlanningPolicy.MaximumSlices; i++) oversize.Add(Cost("r" + i));
            PlanningSourceGuard.ReadCosts(new StubCostSource(oversize, true), Subject, 10, 120010, out var dropped, out bool droppedPublished, out _);
            Assert.That(dropped, Is.Empty); Assert.That(droppedPublished, Is.False);
        }
        [Test] public void CompletePublicationPassesThroughUnchanged()
        {
            var rows = new[] { Cost("a"), Cost("b") };
            PlanningSourceGuard.ReadCosts(new StubCostSource(rows, true), Subject, 10, 120010, out var read, out bool complete, out string completeReason);
            Assert.That(read, Has.Count.EqualTo(2)); Assert.That(complete, Is.True);
            // 成功发布时原因必须清空 —— 否则界面会同时显示"有价"与"缺价原因"（自相矛盾）。
            Assert.That(completeReason, Is.Empty);
            PlanningSourceGuard.ReadOpportunities(new StubWorkSource(new[] { Offer() }, true), Subject, 10, 120010, out var offers, out bool offersComplete);
            Assert.That(offers, Has.Count.EqualTo(1)); Assert.That(offersComplete, Is.True);
        }
        /// <summary>LR-04 端到端：源的具体拒绝码必须原样穿到 plan.Budget.Reason（呈现层据此细分文案）。</summary>
        [Test] public void ConcreteRefusalReasonTravelsFromSourceToBudgetReason()
        {
            PlanningSourceGuard.ReadCosts(new StubCostSource(Array.Empty<RequirementCostSlice>(), false, "FoodQuoteZoneNotEnabled"),
                Subject, 10, 120010, out var costs, out bool complete, out string reason);
            Assert.That(complete, Is.False);
            Assert.That(reason, Is.EqualTo("FoodQuoteZoneNotEnabled"));
            var input = new ShadowPlanningInput(Facts(10, "p"), new LifePolicy(null),
                Fact<FinancialAccountSnapshot>.Known(new FinancialAccountSnapshot("p", 5000)),
                costs, complete, Array.Empty<WorkOpportunity>(), true, Fact<bool>.Known(false), Fact<bool>.Known(true),
                null, reason);
            var plan = ShadowPlanBuilder.Build("p", 1, 1, 10, input, null);
            Assert.That(plan.Budget.State, Is.EqualTo(BudgetState.WaitingQuote));
            Assert.That(plan.Budget.Reason, Is.EqualTo("FoodQuoteZoneNotEnabled"), "具体拒绝原因必须透传，不得被压回兜底码");
            Assert.That(plan.Reason, Is.EqualTo("FoodQuoteZoneNotEnabled"));
        }
    }
}

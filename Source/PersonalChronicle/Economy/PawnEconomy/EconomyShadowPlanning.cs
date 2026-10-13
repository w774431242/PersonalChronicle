using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Application.PawnEconomy.Planning;
using PersonalChronicle.Domain.PawnEconomy.Budget;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy : IShadowPlanningSource, IShadowPlanningPersistence, IShadowPlanningSink
    {
        private ShadowPlanningCoordinator shadowPlanning;
        // B3.2 cross-session hysteresis. It stores only decision memory (reserve latch + still-active
        // needs) and is written only when a Ready budget proves the water marks.
        private PersonalChronicle.Data.PawnEconomy.ShadowPlanningSaveData planningLatches
            = new PersonalChronicle.Data.PawnEconomy.ShadowPlanningSaveData();
        ShadowPlanLatch IShadowPlanningPersistence.ReadLatch(string pawnId, int mapId) => planningLatches.Read(pawnId, mapId);
        void IShadowPlanningPersistence.WriteLatch(ShadowPlan plan) => planningLatches.Write(plan);
        // B3.2 publication ports (M7 priced consumption / M10 funded executable work). Both are created
        // once for this component and read through PlanningSourceGuard, so a failing adapter degrades to
        // "nothing published" instead of a plan built from a partial publication.
        private IRequirementCostSource costSource;
        private IWorkOpportunitySource workSource;
        private IRequirementCostSource CostSource => costSource
            ?? (costSource = new PersonalChronicle.Economy.PawnEconomy.Planning.EconomyFoodCostSource(this));
        // CHG-20261012-ECON-CONTRACTIZE · B2：机构政策退役 ⇒ **车间岗位机会源（EconomyInstitutionWorkSource）
        // 已删**（它完全建立在"政策发布工资/绑定车间区/营业时段"之上）。机会源收敛为**单一**：
        // 原版工作合同（识别玩家下达的砍伐/采矿动作，劳务站发布）。车间岗位链按裁决⑦**冻结**
        //（不发布 + 签约路径返回稳定 `WorkshopChainFrozen`），不再用第二套规则替代。
        private IWorkOpportunitySource WorkSource => workSource
            ?? (workSource = new PersonalChronicle.Economy.PawnEconomy.Planning.EconomyVanillaWorkSource(this));
        private IReadOnlyDictionary<string, FinancialAccountSnapshot> planningAccounts;
        private long planningFinanceEpoch;
        private int planningFinanceTick = -1;
        long IShadowPlanningSource.Epoch => PerceptionSessionEpoch;
        IReadOnlyList<PerceptionSubject> IShadowPlanningSource.Subjects => ObservedSubjects;
        private void InvalidateShadowPlanning()
        { shadowPlanning = null; planningAccounts = null; planningFinanceTick = -1; planningFinanceEpoch = 0; }
        private void TickShadowPlanning()
        {
            if (perception == null || !ReferenceEquals(Verse.Current.Game, perceptionGame)) return;
            if (shadowPlanning == null) shadowPlanning = new ShadowPlanningCoordinator(this);
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            int before = shadowPlanning.LastProcessed;
            shadowPlanning.Tick(Find.TickManager.TicksGame);
            RecordPlanningTick(Find.TickManager.TicksGame, started, shadowPlanning.LastProcessed, shadowPlanning.SourceErrors);
        }
        internal ShadowPlan QueryShadowPlan(string pawnId, Map map)
            => map == null || !IsPerceptionMapAvailable(map) ? null : shadowPlanning?.Query(pawnId, map.uniqueID, Find.TickManager?.TicksGame ?? 0);
        internal IReadOnlyDictionary<string, NeedTarget> QueryShadowTargets(string pawnId, Map map)
            => QueryShadowPlan(pawnId, map)?.Policy?.Targets;
        ShadowPlanningInput IShadowPlanningSource.ReadInput(PerceptionSubject subject, int now)
        {
            var facts = QueryPawnFacts(new PerceptionReadRequest(subject.PawnId, PerceptionSessionEpoch, subject.MapId,
                PerceptionPurpose.Planning, PerceptionGroups.Context | PerceptionGroups.Needs, 300, 500, 1200));
            var traits = new List<EffectiveTrait>();
            Fact<bool> protection = Fact<bool>.Unknown("ProtectionUnavailable"), employment = Fact<bool>.Unknown("EmploymentUnavailable");
            if (perceptionSource != null && perceptionSource.TryGetPlanningPawn(subject.PawnId, subject.MapId, out Pawn pawn))
            {
                if (pawn.story?.traits?.allTraits != null)
                    foreach (var trait in pawn.story.traits.allTraits.Take(PlanningPolicy.MaximumSlices))
                        if (trait != null && !trait.Suppressed) traits.Add(new EffectiveTrait(trait.def.defName, trait.Degree));
                protection = ReadPlanningProtection(pawn);
                employment = Fact<bool>.Known(BasicPawn(pawn) && pawn.IsFreeNonSlaveColonist);
            }
            if (planningAccounts == null || planningFinanceEpoch != PerceptionSessionEpoch || now < planningFinanceTick
                || (long)now - planningFinanceTick >= PlanningPolicy.RefreshTicks)
            {
                planningFinanceTick = now; planningFinanceEpoch = PerceptionSessionEpoch;
                var audit = settlement.ReadAudit();
                planningAccounts = audit.Fault == null ? audit.Accounts.ToDictionary(a => a.Id, StringComparer.Ordinal)
                    : new Dictionary<string, FinancialAccountSnapshot>(StringComparer.Ordinal);
            }
            Fact<FinancialAccountSnapshot> account = planningAccounts.TryGetValue(subject.PawnId, out var money)
                ? Fact<FinancialAccountSnapshot>.Known(money) : Fact<FinancialAccountSnapshot>.Unknown("AccountUnknown");
            // v0.9 §2 M9：特质给**基线**，经历给**偏移** —— 把该 pawn 的学习账本喂进 Policy，
            // 六维风格与 LifeStandard/LaborInclination 统一走 p = clamp01(base + learnedOffset)。
            var policy = new LifePolicy(traits, LedgerFor(subject.PawnId).Offsets);
            long windowEnd = now + (long)policy.ReserveDays * PlanningPolicy.DayTicks;
            int deadline = windowEnd > int.MaxValue ? int.MaxValue : (int)windowEnd;
            // M7 priced consumption and M10 funded executable work are read through ports. Until an
            // adapter publishes real evidence the ports return nothing and report incomplete, so the
            // budget stays WaitingQuote and work stays WaitingOpportunity. Legacy offers, MarketValue
            // and fixed daily meal counts are never substituted for a real quote.
            IReadOnlyList<RequirementCostSlice> costs = Array.Empty<RequirementCostSlice>(); bool costsComplete = false;
            string costsIncompleteReason = "CostEstimateUnavailable";   // LR-04：具体拒绝原因（缺失时保持兜底码）
            if (windowEnd <= int.MaxValue)
                PlanningSourceGuard.ReadCosts(CostSource, subject, now, deadline, out costs, out costsComplete, out costsIncompleteReason);
            PlanningSourceGuard.ReadOpportunities(WorkSource, subject, now, deadline, out var opportunities, out bool opportunitiesComplete);
            // v0.9 §2 M9：把台账判定的低可信度**机构**映射到**候选 id**。
            // 岗位 id 的构造格式（AnchorKey/zoneId/shiftLoadId）由本层 EconomyInstitutionWorkSource 定义，
            // 故在**同层**解析合规；Domain 只收到一份 id 集合，不解析任何格式（M9 可靠性取舍的输入面）。
            var lowConfidence = new List<string>();
            for (int i = 0; i < opportunities.Count; i++)
            {
                var offer = opportunities[i];
                if (offer == null) continue;
                int slash = offer.Id.IndexOf('/');
                if (slash <= 0) continue;
                var credibility = QueryWageCredibility(offer.Id.Substring(0, slash));
                if (credibility != null && credibility.IsLowConfidence) lowConfidence.Add(offer.Id);
            }
            return new ShadowPlanningInput(facts, policy, account, costs, costsComplete, opportunities, opportunitiesComplete,
                protection, employment, lowConfidence, costsIncompleteReason);
        }
        private static Fact<bool> ReadPlanningProtection(Pawn pawn)
        {
            if (!pawn.Spawned || pawn.Drafted || pawn.Downed || pawn.InMentalState || pawn.CurJob?.playerForced == true
                || pawn.Map.dangerWatcher.DangerRating == StoryDanger.High
                || pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Fire).Count > 0) return Fact<bool>.Known(true);
            var patients = pawn.Map.mapPawns.FreeColonistsSpawned;
            if (patients.Count > PlanningPolicy.MaximumSubjects) return Fact<bool>.Unknown("ProtectionCapacity");
            for (int i = 0; i < patients.Count; i++)
                if (patients[i].Downed && !patients[i].InBed()
                    || pawn.workSettings?.WorkIsActive(WorkTypeDefOf.Doctor) == true && patients[i].health.HasHediffsNeedingTend())
                    return Fact<bool>.Known(true);
            return Fact<bool>.Known(false);
        }
    }
}

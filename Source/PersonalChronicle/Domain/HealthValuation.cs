using System;
using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Language-independent valuation result. The UI resolves translation keys;
    /// no rendered text is persisted here. Silver value is computed from the
    /// policy's base value, age depreciation, health score scaling, and any
    /// active chronic-condition penalties.
    /// </summary>
    public sealed class HealthValuationResult
    {
        public readonly bool IsDefined;
        public readonly float HealthScore;     // 0..100 composite
        public readonly float BodyPercent;     // pawn.health.summaryHealth.SummaryHealthPercent
        public readonly int AgeYears;          // biological age
        public readonly float SilverValue;     // final depreciated value
        public readonly float BaseSilverValue; // before penalties/health scaling
        public readonly float AgeDepreciation; // 0..1 fraction lost to age
        public readonly float HealthScale;     // score/prime, 0..1
        public readonly bool IsPrime;          // score >= prime threshold
        public readonly bool IsImpaired;       // score < impaired threshold

        // v4.6.1: 3-dimensional scores (肢体完好 / 精神饱满 / 未衰老), each 0..100.
        public readonly float BodyIntegrityScore;
        public readonly float SpiritScore;
        public readonly float YouthScore;

        // v4.6.1: estimated weekly silver yield based on body × work-output factor.
        public readonly float WeeklySilverEstimate;

        // v4.6.1: per-dimension factor buckets (UI displays them in the hover window).
        public readonly IReadOnlyList<HealthFactor> BodyFactors;
        public readonly IReadOnlyList<HealthFactor> SpiritFactors;
        public readonly IReadOnlyList<HealthFactor> YouthFactors;

        // v4.6.1: depreciation event log (injuries, scars, illnesses).
        public readonly IReadOnlyList<HealthDepreciationEvent> Events;

        // Legacy aggregate factors (kept for tooltip top-line).
        public readonly IReadOnlyList<HealthFactor> Factors;

        public HealthValuationResult(
            bool isDefined,
            float healthScore,
            float bodyPercent,
            int ageYears,
            float silverValue,
            float baseSilverValue,
            float ageDepreciation,
            float healthScale,
            bool isPrime,
            bool isImpaired,
            float bodyIntegrityScore,
            float spiritScore,
            float youthScore,
            float weeklySilverEstimate,
            IReadOnlyList<HealthFactor> bodyFactors,
            IReadOnlyList<HealthFactor> spiritFactors,
            IReadOnlyList<HealthFactor> youthFactors,
            IReadOnlyList<HealthDepreciationEvent> events,
            IReadOnlyList<HealthFactor> factors)
        {
            IsDefined = isDefined;
            HealthScore = healthScore;
            BodyPercent = bodyPercent;
            AgeYears = ageYears;
            SilverValue = silverValue;
            BaseSilverValue = baseSilverValue;
            AgeDepreciation = ageDepreciation;
            HealthScale = healthScale;
            IsPrime = isPrime;
            IsImpaired = isImpaired;
            BodyIntegrityScore = bodyIntegrityScore;
            SpiritScore = spiritScore;
            YouthScore = youthScore;
            WeeklySilverEstimate = weeklySilverEstimate;
            BodyFactors = bodyFactors ?? new List<HealthFactor>();
            SpiritFactors = spiritFactors ?? new List<HealthFactor>();
            YouthFactors = youthFactors ?? new List<HealthFactor>();
            Events = events ?? new List<HealthDepreciationEvent>();
            Factors = factors ?? new List<HealthFactor>();
        }

        public static HealthValuationResult Undefined(int ageYears)
        {
            return new HealthValuationResult(
                false, 0f, 0f, ageYears, 0f, 0f, 0f, 0f, false, false,
                0f, 0f, 0f, 0f,
                new List<HealthFactor>(),
                new List<HealthFactor>(),
                new List<HealthFactor>(),
                new List<HealthDepreciationEvent>(),
                new List<HealthFactor>());
        }
    }

    /// <summary>
    /// One positive or negative valuation factor. Positive factors are things
    /// like "no chronic disease"; negative are active penalties. The UI shows
    /// them in a hover window. isPositive drives the tint; labelKey resolves text.
    /// </summary>
    public sealed class HealthFactor
    {
        public readonly bool IsPositive;
        public readonly string LabelKey;
        public readonly float Impact; // signed impact in silver coins

        public HealthFactor(bool isPositive, string labelKey, float impact)
        {
            IsPositive = isPositive;
            LabelKey = labelKey;
            Impact = impact;
        }
    }

    /// <summary>
    /// Language-independent depreciation event (injury / scar / illness / chronic
    /// condition onset). The UI translates the labelKey and tagKey at build time.
    /// RawTick=-1 means the underlying Hediff has no onset information (rare).
    /// </summary>
    public sealed class HealthDepreciationEvent
    {
        public readonly string LabelKey;   // translation key, e.g. UI.HealthValuation.Event.Asthma
        public readonly string TagKey;     // translation key, e.g. UI.HealthValuation.EventTag.Drop
        public readonly string RawDefName; // original HediffDef.defName for fallback display
        public readonly float Impact;      // signed silver impact (negative = depreciate)
        public readonly long RawTick;      // onset tick (best-effort from Hediff)

        public HealthDepreciationEvent(string labelKey, string tagKey, string rawDefName,
            float impact, long rawTick)
        {
            LabelKey = labelKey;
            TagKey = tagKey;
            RawDefName = rawDefName;
            Impact = impact;
            RawTick = rawTick;
        }
    }

    /// <summary>
    /// Pure, deterministic evaluator. Reads only Pawn health/age/hediff data and
    /// the supplied policy; no UI, no translation. Reusable by external providers
    /// or unit tests. A null pawn or policy yields <see cref="HealthValuationResult.Undefined"/>.
    /// </summary>
    public static class HealthValuationEvaluator
    {
        // Default impact (silver coins) for one mild injury/scraped Hediff event.
        // Def-driven via policy.negativeEventImpact / policy.positiveEventImpact
        // would be the data-driven follow-up; for now kept as constants.
        private const float DefaultInjuryImpact = -40f;
        private const float DefaultScarImpact = -25f;
        private const float DefaultIllnessImpact = -60f;

        public static HealthValuationResult Evaluate(Pawn pawn, HealthValuationPolicyDef policy)
        {
            if (pawn == null || policy == null)
            {
                return HealthValuationResult.Undefined(pawn != null ? pawn.ageTracker.AgeBiologicalYears : 0);
            }

            float bodyPercent = pawn.health != null && pawn.health.summaryHealth != null
                ? pawn.health.summaryHealth.SummaryHealthPercent
                : 1f;
            int ageYears = pawn.ageTracker != null ? pawn.ageTracker.AgeBiologicalYears : 0;

            // Composite health score: body integrity blended with summary, capped 0..100.
            float composite = Mathf.Clamp01(bodyPercent) * 100f;
            bool isPrime = composite >= policy.primeHealthThreshold;
            bool isImpaired = composite < policy.impairedHealthThreshold;

            // Age depreciation (linear per year).
            float ageDepreciation = Mathf.Clamp01(policy.ageDepreciationPerYear * ageYears);
            float baseValue = policy.baseSilverValue * (1f - ageDepreciation);
            float healthScale = Mathf.Clamp01(composite / Mathf.Max(1f, policy.primeHealthThreshold));
            float valued = baseValue * healthScale;

            List<HealthFactor> factors = new List<HealthFactor>();
            List<HealthFactor> bodyFactors = new List<HealthFactor>();
            List<HealthFactor> spiritFactors = new List<HealthFactor>();
            List<HealthFactor> youthFactors = new List<HealthFactor>();
            List<HealthDepreciationEvent> events = new List<HealthDepreciationEvent>();

            // === Dimension 1: Body integrity (肢体完好) ===
            // Base = bodyPercent*100. Bonuses for missing chronic penalties and existing
            // implants (per HediffOnSetCategory matching). Penalties per active Hediff.
            float bodyScore = composite;
            bodyFactors.Add(new HealthFactor(true,
                "PersonalChronicle.UI.HealthValuation.Factor.BodyBaseline",
                baseValue * 0.05f));
            int activeChronicCount = 0;
            if (policy.penalties != null && pawn.health != null && pawn.health.hediffSet != null)
            {
                foreach (HealthPenaltyDef pen in policy.penalties)
                {
                    if (pen == null || string.IsNullOrEmpty(pen.hediffDefName)) continue;
                    HediffDef hd = DefDatabase<HediffDef>.GetNamedSilentFail(pen.hediffDefName);
                    if (hd == null) continue;
                    if (pawn.health.hediffSet.HasHediff(hd))
                    {
                        activeChronicCount++;
                        float lost = valued * Mathf.Clamp01(pen.penaltyFraction);
                        valued -= lost;
                        string fk = pen.labelKey ?? pen.defName;
                        bodyFactors.Add(new HealthFactor(false, fk, -lost));
                        events.Add(new HealthDepreciationEvent(
                            fk,
                            "PersonalChronicle.UI.HealthValuation.EventTag.Drop",
                            hd.defName,
                            -lost,
                            SafeHediffOnsetTick(pawn, hd)));
                    }
                }
            }
            // No active chronic bonus (only when there is no chronic).
            if (activeChronicCount == 0)
            {
                bodyFactors.Add(new HealthFactor(true,
                    "PersonalChronicle.UI.HealthValuation.Factor.NoChronic", 0f));
            }
            bodyScore = Mathf.Clamp(bodyScore + activeChronicCount * -5f, 0f, 100f);

            // === Dimension 2: Spirit (精神饱满) ===
            // Heuristic: count mental-break HediffDefs by name pattern. RimWorld 1.6
            // does not expose a stable Hediff_Mental type here, so we use a defName
            // check (broad) — this keeps Domain portable and free of UI imports.
            float spiritScore = 100f;
            if (pawn.health != null && pawn.health.hediffSet != null)
            {
                int mentalIssues = 0;
                foreach (Hediff hd in pawn.health.hediffSet.hediffs)
                {
                    if (hd == null || hd.def == null) continue;
                    string dn = hd.def.defName;
                    if (dn != null && (dn.StartsWith("MentalBreak") || dn.StartsWith("Psychic") || dn.IndexOf("Mental", System.StringComparison.Ordinal) >= 0))
                    {
                        mentalIssues++;
                    }
                }
                spiritScore = Mathf.Clamp(100f - mentalIssues * 25f, 0f, 100f);
                if (mentalIssues == 0)
                {
                    spiritFactors.Add(new HealthFactor(true,
                        "PersonalChronicle.UI.HealthValuation.Factor.SpiritStable", 0f));
                }
                else
                {
                    spiritFactors.Add(new HealthFactor(false,
                        "PersonalChronicle.UI.HealthValuation.Factor.SpiritLoss", -mentalIssues * 50f));
                }
            }
            else
            {
                spiritFactors.Add(new HealthFactor(true,
                    "PersonalChronicle.UI.HealthValuation.Factor.SpiritStable", 0f));
            }

            // === Dimension 3: Youth (未衰老) ===
            // Heuristic: age score = 100 - (ageYears × 1.5), floor 0.
            float youthScore = Mathf.Clamp(100f - ageYears * 1.5f, 0f, 100f);
            if (ageYears < 25)
            {
                youthFactors.Add(new HealthFactor(true,
                    "PersonalChronicle.UI.HealthValuation.Factor.YouthPrime",
                    baseValue * 0.05f));
            }
            else if (ageYears >= 40)
            {
                youthFactors.Add(new HealthFactor(false,
                    "PersonalChronicle.UI.HealthValuation.Factor.YouthWorn",
                    -(policy.baseSilverValue * 0.05f)));
            }

            // === Aggregate legacy factor list (top-line tooltip) ===
            if (composite >= policy.primeHealthThreshold)
            {
                factors.Add(new HealthFactor(true,
                    "PersonalChronicle.UI.HealthValuation.Factor.PrimeBody", 0f));
            }
            else
            {
                float lost = baseValue - valued;
                factors.Add(new HealthFactor(false,
                    "PersonalChronicle.UI.HealthValuation.Factor.HealthLoss", -lost));
            }
            if (ageDepreciation > 0.001f)
            {
                factors.Add(new HealthFactor(false,
                    "PersonalChronicle.UI.HealthValuation.Factor.AgeWear",
                    -(policy.baseSilverValue * ageDepreciation)));
            }

            // === Injury / scar scan: emit a timeline event for each missing-part / scar
            //     / old injury Hediff currently present on the pawn. ===
            if (pawn.health != null && pawn.health.hediffSet != null)
            {
                foreach (Hediff hd in pawn.health.hediffSet.hediffs)
                {
                    if (hd == null || hd.def == null) continue;
                    // Skip mental hediffs (handled in spirit dimension).
                    string dn = hd.def.defName;
                    if (dn != null && (dn.StartsWith("MentalBreak") || dn.StartsWith("Psychic") || dn.IndexOf("Mental", System.StringComparison.Ordinal) >= 0))
                    {
                        continue;
                    }
                    // Skip chronic penalties (already emitted as events above).
                    bool isChronic = false;
                    if (policy.penalties != null)
                    {
                        foreach (HealthPenaltyDef pen in policy.penalties)
                        {
                            if (pen != null && pen.hediffDefName == hd.def.defName)
                            {
                                isChronic = true;
                                break;
                            }
                        }
                    }
                    if (isChronic) continue;
                    EmitHediffEvent(hd, events, factors);
                }
            }

            // Cap event list to top 8 by impact (most negative first) to keep the
            // timeline compact; the underlying data still has all events.
            events.Sort((a, b) => a.Impact.CompareTo(b.Impact));
            if (events.Count > 8)
            {
                events.RemoveRange(8, events.Count - 8);
            }
            events.Sort((a, b) => a.RawTick.CompareTo(b.RawTick));

            // === Weekly silver estimate: a simple "how much silver this pawn earns
            // per in-game week based on body × base yield". Data-driven via the
            // policy's baseWeeklyYieldPerHealthyPawn (defaults to 30 silver). ===
            float baseWeekly = policy.baseWeeklyYieldPerHealthyPawn > 0f
                ? policy.baseWeeklyYieldPerHealthyPawn
                : 30f;
            float weekly = baseWeekly * healthScale * Mathf.Clamp01(1f - ageDepreciation * 0.5f);

            float finalValue = Mathf.Max(policy.minSilverValue, valued);
            return new HealthValuationResult(
                true,
                composite,
                bodyPercent,
                ageYears,
                finalValue,
                policy.baseSilverValue,
                ageDepreciation,
                healthScale,
                isPrime,
                isImpaired,
                bodyScore,
                spiritScore,
                youthScore,
                weekly,
                bodyFactors,
                spiritFactors,
                youthFactors,
                events,
                factors);
        }

        private static void EmitHediffEvent(Hediff hd, List<HealthDepreciationEvent> events,
            List<HealthFactor> factors)
        {
            if (hd == null || hd.def == null) return;
            HediffDef def = hd.def;
            string labelKey = "PersonalChronicle.UI.HealthValuation.Event." + def.defName;
            string tagKey = "PersonalChronicle.UI.HealthValuation.EventTag.Drop";
            float impact;
            if (def.isBad)
            {
                // RimWorld 1.6: tendable=true means bandage/healable, tendable=false
                // means scar/old/permanent. Use tendable as the recovery proxy
                // (there is no isChronic on HediffDef in 1.6).
                if (def.tendable)
                {
                    impact = DefaultInjuryImpact;
                    tagKey = "PersonalChronicle.UI.HealthValuation.EventTag.Recoverable";
                }
                else
                {
                    impact = DefaultScarImpact;
                    tagKey = "PersonalChronicle.UI.HealthValuation.EventTag.Permanent";
                }
            }
            else
            {
                return; // ignore benign / prosthetic additions for the event log.
            }
            events.Add(new HealthDepreciationEvent(labelKey, tagKey, def.defName, impact, -1L));
        }

        private static long SafeHediffOnsetTick(Pawn pawn, HediffDef hd)
        {
            // RimWorld 1.6: Hediff has no public onsetTick field that is reliably
            // accessible here; return -1 so the UI shows "未知日期".
            return -1L;
        }
    }
}

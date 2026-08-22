using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Data-driven policy for the "health residual / asset depreciation" view.
    /// All thresholds, depreciation rates, and the silver base value live in Def
    /// data so another mod can tune the valuation without touching C#.
    /// </summary>
    public sealed class HealthValuationPolicyDef : Def
    {
        public const string DefaultPolicyDefName = "PersonalChronicleHealthValuationPolicy";

        /// <summary>Silver-coin base value of a fully-healthy prime colonist.</summary>
        public float baseSilverValue = 1500f;

        /// <summary>
        /// Per-year depreciation rate applied to the silver value, expressed as a
        /// 0..1 fraction lost per year of chronological age (linear).
        /// </summary>
        public float ageDepreciationPerYear = 0.02f;

        /// <summary>
        /// Health score (0..100) at or above which a pawn is "prime" (no penalty).
        /// Below this, the silver value is scaled by score/100.
        /// </summary>
        public float primeHealthThreshold = 85f;

        /// <summary>
        /// Health score below which the body is considered "impaired" and the
        /// valuation shows a blood-colored warning factor list.
        /// </summary>
        public float impairedHealthThreshold = 60f;

        /// <summary>Silver value floor so even a wrecked pawn keeps a scrap value.</summary>
        public float minSilverValue = 50f;

        /// <summary>
        /// Base weekly silver yield for a fully-healthy prime colonist; the
        /// evaluator scales this by health×(1-ageDepreciation×0.5) to produce
        /// the per-week "estimate" displayed in the榨取总账. Defaults to 30.
        /// </summary>
        public float baseWeeklyYieldPerHealthyPawn = 30f;

        /// <summary>
        /// Named chronic conditions whose presence adds a penalty factor to the
        /// final valuation. Def-driven so mods can extend the list.
        /// labelKey resolves in the current language; penalty is 0..1 fraction lost.
        /// </summary>
        public List<HealthPenaltyDef> penalties = new List<HealthPenaltyDef>();
    }

    /// <summary>
    /// One named chronic condition and its silver penalty fraction.
    ///
    /// Deliberately NOT a <see cref="Verse.Def"/>: this is an inlined data object
    /// nested under <see cref="HealthValuationPolicyDef.penalties"/>, not a global
    /// DefDatabase entry. Declaring it as a Def subclass made RimWorld's Scribe
    /// treat each &lt;li&gt; as a cross-reference and attempt to resolve the whole
    /// serialized node as a defName — which failed and silently left the list empty.
    /// As a plain class it deserializes as a deep inline object, matching the
    /// XML structure (fixed 2026-08-10).
    /// </summary>
    public sealed class HealthPenaltyDef
    {
        /// <summary>
        /// Stable identifier (matches the XML &lt;defName&gt; node, e.g.
        /// "PersonalChronicleHealthPenaltyAsthma"). Used as a display fallback
        /// when <see cref="labelKey"/> is unresolved.
        /// </summary>
        public string defName;

        /// <summary>Hediff def name that triggers this penalty when present.</summary>
        public string hediffDefName;

        /// <summary>0..1 fraction of final value lost while this condition is active.</summary>
        public float penaltyFraction = 0.1f;

        /// <summary>Translation key for the human-readable penalty label.</summary>
        public string labelKey;
    }
}

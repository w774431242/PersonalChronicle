namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Centralised translation-key references for the health-valuation module
    /// (BASE-010 / GOV-006: no bare translation-key literals scattered across
    /// code). Keys are stable strings consumed by the UI at render time;
    /// nothing in Domain resolves them (LOC-006). The shared prefix
    /// "PersonalChronicle.UI.HealthValuation." is intentionally NOT concatenated
    /// here — each key is written out in full so a typo fails fast at compile
    /// time and key-scans (LOC-005) see the complete string.
    /// </summary>
    public static class HealthValuationKeys
    {
        // ---- Section header / empty / stat cells ----
        public const string Title = "PersonalChronicle.UI.HealthValuation.Title";
        public const string NoData = "PersonalChronicle.UI.HealthValuation.NoData";
        public const string SilverValue = "PersonalChronicle.UI.HealthValuation.SilverValue";
        public const string BaseValue = "PersonalChronicle.UI.HealthValuation.BaseValue";
        public const string Score = "PersonalChronicle.UI.HealthValuation.Score";
        public const string Body = "PersonalChronicle.UI.HealthValuation.Body";
        public const string WeeklyYield = "PersonalChronicle.UI.HealthValuation.WeeklyYield";
        public const string TipHeader = "PersonalChronicle.UI.HealthValuation.TipHeader";
        public const string NoEvents = "PersonalChronicle.UI.HealthValuation.NoEvents";
        public const string Verdict = "PersonalChronicle.UI.HealthValuation.Verdict";

        // ---- Closing verdict blurb ----
        public const string VerdictImpaired = "PersonalChronicle.UI.HealthValuation.VerdictImpaired";
        public const string VerdictPrime = "PersonalChronicle.UI.HealthValuation.VerdictPrime";
        public const string VerdictFair = "PersonalChronicle.UI.HealthValuation.VerdictFair";
        public const string VerdictDepleted = "PersonalChronicle.UI.HealthValuation.VerdictDepleted";

        // ---- Dimension bars ----
        public const string DimBody = "PersonalChronicle.UI.HealthValuation.Dim.Body";
        public const string DimSpirit = "PersonalChronicle.UI.HealthValuation.Dim.Spirit";
        public const string DimYouth = "PersonalChronicle.UI.HealthValuation.Dim.Youth";

        // ---- Per-hediff event keys ----
        /// <summary>Prefix for "Event.{HediffDef.defName}" keys (e.g. Event.BadBack).</summary>
        public const string EventPrefix = "PersonalChronicle.UI.HealthValuation.Event.";

        /// <summary>Factor keys used by <see cref="HealthValuationEvaluator"/>.</summary>
        public static class Factor
        {
            public const string BodyBaseline = "PersonalChronicle.UI.HealthValuation.Factor.BodyBaseline";
            public const string NoChronic = "PersonalChronicle.UI.HealthValuation.Factor.NoChronic";
            public const string SpiritStable = "PersonalChronicle.UI.HealthValuation.Factor.SpiritStable";
            public const string SpiritLoss = "PersonalChronicle.UI.HealthValuation.Factor.SpiritLoss";
            public const string YouthPrime = "PersonalChronicle.UI.HealthValuation.Factor.YouthPrime";
            public const string YouthWorn = "PersonalChronicle.UI.HealthValuation.Factor.YouthWorn";
            public const string PrimeBody = "PersonalChronicle.UI.HealthValuation.Factor.PrimeBody";
            public const string HealthLoss = "PersonalChronicle.UI.HealthValuation.Factor.HealthLoss";
            public const string AgeWear = "PersonalChronicle.UI.HealthValuation.Factor.AgeWear";
        }

        /// <summary>Event-tag keys (recoverability classification).</summary>
        public static class EventTag
        {
            public const string Drop = "PersonalChronicle.UI.HealthValuation.EventTag.Drop";
            public const string Recoverable = "PersonalChronicle.UI.HealthValuation.EventTag.Recoverable";
            public const string Permanent = "PersonalChronicle.UI.HealthValuation.EventTag.Permanent";
        }
    }
}

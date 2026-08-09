using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// DLC-state query service. Keeps every gating decision (e.g. excluding
    /// Anomaly subhumans from the chronicle) in one place with a verified API.
    /// All probes are plain cached static reads — no reflection on hot paths.
    ///
    /// API basis (verified against 1.6.4871 Assembly-CSharp metadata, 2026-08-08):
    ///   - Verse.ModsConfig.AnomalyActive   — direct static property (backed by a
    ///     cached static field). The 1.5-era DlcManager/DlcDefOf pair does NOT
    ///     exist in 1.6, so ModsConfig is the stable activation probe.
    ///   - Verse.Pawn.IsSubhuman            — official property; its getter IL is
    ///     `IsMutant &amp;&amp; mutant.Def.consideredSubhuman`, where
    ///     MutantDef.consideredSubhuman is true for Shambler/Ghoul/AwokenCorpse
    ///     (Data/Anomaly/Defs/Misc/Mutants.xml). This is the correct check — a
    ///     conservative name-based fallback on the pawn kind def would be wrong:
    ///     base Anomaly data has NO PawnKindDef whose name contains "Subhuman"
    ///     (subhumans are mutants of an existing kind, not a kind).
    /// </summary>
    public static class DlcStatus
    {
        /// <summary>
        /// Cached on first type access (lazy static-readonly). Safe timing: mod
        /// configuration is finalized before any GameComponent.FinalizeInit or
        /// Harmony patch body runs, so the value is correct whenever this class
        /// is first touched.
        /// </summary>
        private static readonly bool AnomalyActive = ModsConfig.AnomalyActive;

        public static bool IsAnomalyActive
        {
            get { return AnomalyActive; }
        }

        /// <summary>
        /// Returns true when <paramref name="pawn"/> is an Anomaly subhuman.
        /// Short-circuits to false when Anomaly is inactive (zero overhead) or
        /// the pawn is null.
        /// </summary>
        public static bool IsSubhuman(Pawn pawn)
        {
            if (pawn == null || !AnomalyActive)
            {
                return false;
            }
            return pawn.IsSubhuman;
        }
    }
}

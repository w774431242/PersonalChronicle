using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// v4.9.1: resolves (and caches) the active <see cref="ThingArchivePolicyDef"/>
    /// and exposes the single decision point every capture path uses.
    /// Architecture: mirror of <see cref="SocialRelationFilter"/> — Def-driven,
    /// lazy-resolved after defs load, with a built-in fallback so the archive
    /// keeps working even if the policy Def is missing or unpatched.
    ///
    /// Callers:
    ///   - <see cref="Application.ArchiveService.IsEquipableDef"/>   (capture)
    ///   - <see cref="Capture.Patch_ThingDestroy"/>                  (decommission)
    /// Both must agree, otherwise the decommission capture scope drifts from the
    /// registered thing set.
    /// </summary>
    public static class ThingArchivePolicy
    {
        private static ThingArchivePolicyDef cachedPolicy;
        private static bool policyResolved;

        /// <summary>
        /// Resolves (and caches) the active policy. Returns a built-in default
        /// instance when no Def is present so callers never see null.
        /// </summary>
        public static ThingArchivePolicyDef Policy
        {
            get
            {
                if (policyResolved)
                {
                    return cachedPolicy;
                }
                // DefDatabase is only safe after defs load; GetNamedSilentFail
                // returns null if called too early — we do not latch the cache
                // then, and retry on the next call.
                ThingArchivePolicyDef named = DefDatabase<ThingArchivePolicyDef>
                    .GetNamedSilentFail(ThingArchivePolicyDef.DefaultPolicyDefName);
                if (named == null)
                {
                    var all = DefDatabase<ThingArchivePolicyDef>.AllDefsListForReading;
                    if (all != null && all.Count > 0)
                    {
                        named = all[0];
                    }
                }
                cachedPolicy = named ?? DefaultFallback();
                policyResolved = true;
                return cachedPolicy;
            }
        }

        /// <summary>
        /// Whether the archive captures and shows this ThingDef as equipment.
        /// Central decision point; never bypassed in capture code.
        /// </summary>
        public static bool Captures(ThingDef def)
        {
            return Policy.Captures(def);
        }

        private static ThingArchivePolicyDef DefaultFallback()
        {
            return new ThingArchivePolicyDef
            {
                defName = ThingArchivePolicyDef.DefaultPolicyDefName,
                excludeNonCombatApparel = true,
                minCombatArmorForApparel = 0.20f,
                excludeApparelDefNames = null
            };
        }
    }
}

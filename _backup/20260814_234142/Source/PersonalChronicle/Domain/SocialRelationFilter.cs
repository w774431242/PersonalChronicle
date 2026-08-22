using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Decides which social ties the archive treats as "significant".
    ///
    /// The set is driven by <see cref="SocialRelationPolicyDef"/> so third-party
    /// relation/race mods can extend it via PatchOperation. If the policy Def is
    /// missing (broken load order, stripped Defs folder) the vanilla blood-family
    /// + romance whitelist is used as a fallback, so behavior never regresses to
    /// "capture nothing".
    /// </summary>
    public static class SocialRelationFilter
    {
        /// <summary>
        /// Synthetic relation key prefix for ties that vanilla computes from
        /// opinion instead of storing as a PawnRelationDef. Kept distinct from
        /// any real defName so a future vanilla/mod def can never collide.
        /// </summary>
        public const string SyntheticPrefix = "PC_Opinion_";

        public const string FriendRelationKey = SyntheticPrefix + "Friend";
        public const string RivalRelationKey = SyntheticPrefix + "Rival";

        private static readonly string[] FallbackWhitelist =
        {
            "Lover", "Fiance", "Spouse", "ExLover", "ExSpouse",
            "Parent", "Child", "Sibling", "HalfSibling"
        };

        private static readonly string[] FallbackExclusions = { "Bond" };

        private static SocialRelationPolicyDef cachedPolicy;
        private static bool policyResolved;
        private static HashSet<string> cachedAllowed;
        private static HashSet<string> cachedExcluded;

        /// <summary>
        /// Resolves (and caches) the active policy. Returns null when no Def is
        /// present; callers must then use the built-in defaults.
        /// </summary>
        public static SocialRelationPolicyDef Policy
        {
            get
            {
                if (policyResolved)
                {
                    return cachedPolicy;
                }
                // DefDatabase is only safe after defs load; GetNamedSilentFail
                // returns null (no exception) if called too early, in which case
                // we simply do not latch the cache and retry on the next call.
                SocialRelationPolicyDef named = DefDatabase<SocialRelationPolicyDef>
                    .GetNamedSilentFail(SocialRelationPolicyDef.DefaultPolicyDefName);
                if (named == null)
                {
                    List<SocialRelationPolicyDef> all =
                        DefDatabase<SocialRelationPolicyDef>.AllDefsListForReading;
                    if (all != null && all.Count > 0)
                    {
                        named = all[0];
                    }
                }
                if (named != null)
                {
                    cachedPolicy = named;
                    policyResolved = true;
                    BuildLookups(named);
                }
                return named;
            }
        }

        private static void BuildLookups(SocialRelationPolicyDef policy)
        {
            cachedAllowed = new HashSet<string>();
            cachedExcluded = new HashSet<string>();
            if (policy.directRelationDefNames != null)
            {
                for (int i = 0; i < policy.directRelationDefNames.Count; i++)
                {
                    string n = policy.directRelationDefNames[i];
                    if (!string.IsNullOrEmpty(n))
                    {
                        cachedAllowed.Add(n);
                    }
                }
            }
            if (policy.excludedRelationDefNames != null)
            {
                for (int i = 0; i < policy.excludedRelationDefNames.Count; i++)
                {
                    string n = policy.excludedRelationDefNames[i];
                    if (!string.IsNullOrEmpty(n))
                    {
                        cachedExcluded.Add(n);
                    }
                }
            }
        }

        /// <summary>
        /// Test hook / def-reload hook: drops the cached policy so the next call
        /// re-resolves it from the DefDatabase.
        /// </summary>
        public static void ResetCache()
        {
            cachedPolicy = null;
            policyResolved = false;
            cachedAllowed = null;
            cachedExcluded = null;
        }

        public static bool IsSignificant(PawnRelationDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.defName))
            {
                return false;
            }
            return IsSignificant(def.defName);
        }

        public static bool IsSignificant(string relationDefName)
        {
            if (string.IsNullOrEmpty(relationDefName))
            {
                return false;
            }
            // Synthetic opinion ties bypass the direct-relation whitelist; they
            // are gated by the policy's includeOpinionRelations flag instead.
            if (relationDefName.StartsWith(SyntheticPrefix))
            {
                return true;
            }

            SocialRelationPolicyDef policy = Policy;
            if (policy == null || cachedAllowed == null || cachedExcluded == null)
            {
                return IsFallbackSignificant(relationDefName);
            }
            if (cachedExcluded.Contains(relationDefName))
            {
                return false;
            }
            // Empty whitelist = accept everything not explicitly excluded.
            if (cachedAllowed.Count == 0)
            {
                return true;
            }
            return cachedAllowed.Contains(relationDefName);
        }

        private static bool IsFallbackSignificant(string relationDefName)
        {
            for (int i = 0; i < FallbackExclusions.Length; i++)
            {
                if (FallbackExclusions[i] == relationDefName)
                {
                    return false;
                }
            }
            for (int i = 0; i < FallbackWhitelist.Length; i++)
            {
                if (FallbackWhitelist[i] == relationDefName)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when the key is a synthesized opinion-based tie.</summary>
        public static bool IsSynthetic(string relationDefName)
        {
            return !string.IsNullOrEmpty(relationDefName)
                && relationDefName.StartsWith(SyntheticPrefix);
        }
    }
}

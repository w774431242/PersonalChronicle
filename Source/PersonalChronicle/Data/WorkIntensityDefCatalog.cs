using System;
using System.Collections.Generic;
using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// Converts Def data into an immutable domain policy. DefDatabase access is
    /// isolated here so the evaluator and external providers do not depend on
    /// RimWorld's Def API.
    /// </summary>
    internal static class WorkIntensityDefCatalog
    {
        public static WorkIntensityPolicySnapshot Load()
        {
            WorkIntensityPolicyDef policy = FindPolicy();
            int minimumDays = policy != null ? policy.minimumSampleDays : 5;
            float overloadRatio = policy != null ? policy.overloadRatio : 1.5f;
            float slackRatio = policy != null ? policy.slackRatio : 0.5f;

            List<WorkIntensityTierSpec> tiers = new List<WorkIntensityTierSpec>();
            List<WorkIntensityTierDef> defs = DefDatabase<WorkIntensityTierDef>.AllDefsListForReading;
            if (policy != null && policy.tierDefNames != null && policy.tierDefNames.Count > 0)
            {
                for (int i = 0; i < policy.tierDefNames.Count; i++)
                {
                    string defName = policy.tierDefNames[i];
                    if (string.IsNullOrEmpty(defName))
                    {
                        continue;
                    }
                    WorkIntensityTierDef tier = DefDatabase<WorkIntensityTierDef>
                        .GetNamedSilentFail(defName);
                    if (tier != null)
                    {
                        tiers.Add(ToSpec(tier));
                    }
                    else
                    {
                        ChronicleLog.Warning(ChronicleLog.Category.Archive, "WorkIntensityPolicyDef references missing tier " + defName);
                    }
                }
            }
            else
            {
                for (int i = 0; i < defs.Count; i++)
                {
                    if (defs[i] != null)
                    {
                        tiers.Add(ToSpec(defs[i]));
                    }
                }
            }

            tiers.Sort(CompareTiers);
            return new WorkIntensityPolicySnapshot(minimumDays, overloadRatio, slackRatio, tiers);
        }

        public static bool TryLoadTier(string defName, out WorkIntensityTierSpec tier)
        {
            tier = null;
            if (string.IsNullOrEmpty(defName))
            {
                return false;
            }
            WorkIntensityTierDef def = DefDatabase<WorkIntensityTierDef>
                .GetNamedSilentFail(defName);
            if (def == null)
            {
                return false;
            }
            tier = ToSpec(def);
            return true;
        }

        private static WorkIntensityPolicyDef FindPolicy()
        {
            WorkIntensityPolicyDef named = DefDatabase<WorkIntensityPolicyDef>
                .GetNamedSilentFail(WorkIntensityPolicyDef.DefaultPolicyDefName);
            if (named != null)
            {
                return named;
            }
            List<WorkIntensityPolicyDef> policies =
                DefDatabase<WorkIntensityPolicyDef>.AllDefsListForReading;
            return policies.Count > 0 ? policies[0] : null;
        }

        private static WorkIntensityTierSpec ToSpec(WorkIntensityTierDef tier)
        {
            return new WorkIntensityTierSpec(
                tier.defName,
                tier.tierKey,
                tier.displayCode,
                tier.minimumDailyHours,
                tier.labelKey,
                tier.tagKey,
                tier.colorHex,
                tier.order);
        }

        private static int CompareTiers(WorkIntensityTierSpec a, WorkIntensityTierSpec b)
        {
            if (ReferenceEquals(a, b))
            {
                return 0;
            }
            if (a == null)
            {
                return 1;
            }
            if (b == null)
            {
                return -1;
            }
            int threshold = b.MinimumDailyHours.CompareTo(a.MinimumDailyHours);
            if (threshold != 0)
            {
                return threshold;
            }
            int order = a.Order.CompareTo(b.Order);
            if (order != 0)
            {
                return order;
            }
            return string.Compare(a.DefName, b.DefName, StringComparison.Ordinal);
        }
    }
}

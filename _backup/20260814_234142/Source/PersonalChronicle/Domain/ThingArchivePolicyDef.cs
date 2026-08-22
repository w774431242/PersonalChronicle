using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// v4.9.1: data-driven policy describing which equipment things the archive
    /// captures and shows under the "装备" (equipment) category. Before this the
    /// whitelist was a hard-coded "IsWeapon || IsApparel" in
    /// <see cref="Application.ArchiveService.IsEquipableDef"/>, which pulled in
    /// low-value non-combat apparel (dust jackets, work wear, fashion clothes)
    /// that add no chronicle value. Everything is Def data now so the set can be
    /// patched with a PatchOperation and zero C# changes.
    /// </summary>
    public sealed class ThingArchivePolicyDef : Def
    {
        public const string DefaultPolicyDefName = "PersonalChronicleThingArchivePolicy";

        /// <summary>
        /// When true (default), apparel with no meaningful armor rating is treated
        /// as non-combat clothing and NOT captured — dust jackets, work wear,
        /// fashion clothes, tainted rags. "Meaningful" is decided by
        /// <see cref="minCombatArmorForApparel"/>: apparel whose highest of
        /// sharp/blunt armor is below the threshold is skipped.
        /// Weapons are always captured regardless of this flag.
        /// </summary>
        public bool excludeNonCombatApparel = true;

        /// <summary>
        /// The higher of ArmorRating_Sharp and ArmorRating_Blunt below which an
        /// apparel item is considered non-combat. Tested against the HIGHEST of the
        /// two (not their sum) so a dust jacket (Sharp 0.12, Blunt 0.10) is clearly
        /// excluded while a flak jacket (Sharp 0.36, Blunt 0.35) and composite /
        /// power armor pass. Heat armor alone never qualifies a garment.
        /// </summary>
        public float minCombatArmorForApparel = 0.20f;

        /// <summary>
        /// Hard blacklist of ThingDef names never captured even if they are a
        /// weapon or carry armor (escape hatch for modded edge cases).
        /// </summary>
        public List<string> excludeApparelDefNames = new List<string>();

        /// <summary>
        /// Applies the policy to a ThingDef. Weapons are always in; apparel is in
        /// only when not blacklisted and (when <see cref="excludeNonCombatApparel"/>
        /// is on) it carries enough armor to count as combat apparel.
        /// </summary>
        public bool Captures(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }
            if (def.IsWeapon)
            {
                return !IsBlacklisted(def);
            }
            if (def.IsApparel)
            {
                if (IsBlacklisted(def))
                {
                    return false;
                }
                if (!excludeNonCombatApparel)
                {
                    return true;
                }
                return MaxCombatArmor(def) >= minCombatArmorForApparel;
            }
            return false;
        }

        private bool IsBlacklisted(ThingDef def)
        {
            if (excludeApparelDefNames == null || excludeApparelDefNames.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < excludeApparelDefNames.Count; i++)
            {
                if (def.defName == excludeApparelDefNames[i])
                {
                    return true;
                }
            }
            return false;
        }

        private static float MaxCombatArmor(ThingDef def)
        {
            if (def.statBases == null || def.statBases.Count == 0)
            {
                return 0f;
            }
            float sharp = 0f;
            float blunt = 0f;
            for (int i = 0; i < def.statBases.Count; i++)
            {
                StatModifier sm = def.statBases[i];
                if (sm == null || sm.stat == null)
                {
                    continue;
                }
                if (sm.stat == StatDefOf.ArmorRating_Sharp)
                {
                    sharp = sm.value;
                }
                else if (sm.stat == StatDefOf.ArmorRating_Blunt)
                {
                    blunt = sm.value;
                }
                // Heat armor alone never qualifies a combat garment.
            }
            return sharp > blunt ? sharp : blunt;
        }
    }
}

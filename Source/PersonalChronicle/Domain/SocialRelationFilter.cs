using RimWorld;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Whitelist of PawnRelationDef names that the archive treats as
    /// "significant social ties". Blood family + romantic partners only —
    /// animal Bond and obscure virtual relations are excluded to keep the
    /// Social tab readable.
    /// </summary>
    public static class SocialRelationFilter
    {
        public static bool IsSignificant(PawnRelationDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.defName))
            {
                return false;
            }
            // Prefer DefOf identity when available (renames stay linked).
            if (def == PawnRelationDefOf.Lover
                || def == PawnRelationDefOf.Fiance
                || def == PawnRelationDefOf.Spouse
                || def == PawnRelationDefOf.ExLover
                || def == PawnRelationDefOf.ExSpouse
                || def == PawnRelationDefOf.Parent
                || def == PawnRelationDefOf.Child
                || def == PawnRelationDefOf.Sibling
                || def == PawnRelationDefOf.HalfSibling)
            {
                return true;
            }
            // DefName fallback for older saves / custom defs that mirror vanilla.
            string n = def.defName;
            return n == "Lover" || n == "Fiance" || n == "Spouse"
                || n == "ExLover" || n == "ExSpouse"
                || n == "Parent" || n == "Child" || n == "Sibling" || n == "HalfSibling";
        }
    }
}

using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Data-driven policy describing which social ties the archive treats as
    /// "significant". Before v1.0.1 the whitelist lived as hard-coded defName
    /// literals in <see cref="SocialRelationFilter"/>, which meant a third-party
    /// race/relation mod could never surface its own ties. Everything is Def data
    /// now so the set can be patched with a PatchOperation and zero C# changes.
    /// </summary>
    public sealed class SocialRelationPolicyDef : Def
    {
        public const string DefaultPolicyDefName = "PersonalChronicleSocialRelationPolicy";

        /// <summary>
        /// Whitelist of <see cref="RimWorld.PawnRelationDef"/> names captured from
        /// the pawn's direct relation list. An EMPTY list means "accept every
        /// direct relation" (minus <see cref="excludedRelationDefNames"/>), which
        /// is the escape hatch for heavily modded relation sets.
        /// </summary>
        public List<string> directRelationDefNames = new List<string>();

        /// <summary>
        /// Relations never archived regardless of the other settings. Animal
        /// Bond lives here by default: it is not a colonist-to-colonist tie and
        /// would flood the Social tab.
        /// </summary>
        public List<string> excludedRelationDefNames = new List<string>();

        /// <summary>
        /// When true, also capture relations that RimWorld *derives* rather than
        /// stores (grandparent / aunt / cousin / kin ...). These never appear in
        /// DirectRelations, so without this flag most family trees look empty.
        /// </summary>
        public bool includeImpliedRelations = true;

        /// <summary>
        /// When true, capture opinion-based friendships/rivalries. These are not
        /// PawnRelationDefs at all — vanilla computes them from opinion on the
        /// fly — so they must be synthesized to appear in a persisted archive.
        /// </summary>
        public bool includeOpinionRelations = true;

        /// <summary>Opinion at or above which a peer is archived as a friend.</summary>
        public int opinionFriendThreshold = 20;

        /// <summary>Opinion at or below which a peer is archived as a rival.</summary>
        public int opinionRivalThreshold = -20;

        /// <summary>
        /// Upper bound on synthesized opinion relations per pawn, keeping the
        /// backfill O(N*cap) instead of unbounded on mega-colonies. The strongest
        /// opinions (by absolute value) win.
        /// </summary>
        public int maxOpinionRelationsPerPawn = 8;
    }
}

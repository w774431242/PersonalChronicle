using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Shared snapshot helpers for archive write paths (join / death / backfill).
    /// Keeps skill/backstory capture in one place so Data and Application never diverge.
    /// </summary>
    public static class PawnArchiveSnapshots
    {
        public static void CaptureSkills(Pawn pawn, Dictionary<string, int> target)
        {
            if (pawn == null || target == null || pawn.skills == null || pawn.skills.skills == null)
            {
                return;
            }
            target.Clear();
            List<SkillRecord> skills = pawn.skills.skills;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillRecord skill = skills[i];
                if (skill == null || skill.def == null)
                {
                    continue;
                }
                target[skill.def.defName] = skill.Level;
            }
        }

        public static void CaptureBackstories(Pawn pawn, out string childhoodDefName, out string adulthoodDefName)
        {
            childhoodDefName = null;
            adulthoodDefName = null;
            if (pawn == null || pawn.story == null)
            {
                return;
            }
            // 1.6: story.Childhood / Adulthood are BackstoryDef references (nullable).
            BackstoryDef childhood = pawn.story.Childhood;
            BackstoryDef adulthood = pawn.story.Adulthood;
            if (childhood != null)
            {
                childhoodDefName = childhood.defName;
            }
            if (adulthood != null)
            {
                adulthoodDefName = adulthood.defName;
            }
        }

        public static void ApplyJoinSnapshots(PawnObject record, Pawn pawn)
        {
            if (record == null || pawn == null)
            {
                return;
            }
            if (record.SkillSnapshot == null)
            {
                record.SkillSnapshot = new Dictionary<string, int>();
            }
            CaptureSkills(pawn, record.SkillSnapshot);
            string childhood;
            string adulthood;
            CaptureBackstories(pawn, out childhood, out adulthood);
            if (!string.IsNullOrEmpty(childhood))
            {
                record.ChildhoodBackstoryDefName = childhood;
            }
            if (!string.IsNullOrEmpty(adulthood))
            {
                record.AdulthoodBackstoryDefName = adulthood;
            }
            CaptureInitialRelations(pawn, record);
        }

        /// <summary>
        /// Snapshots the pawn's existing significant social ties so the Social tab
        /// can render them even after the pawn has died or left the colony.
        ///
        /// Three sources are merged, because relying on DirectRelations alone
        /// (the pre-v1.0.1 behavior) missed the majority of real ties:
        ///   A. DirectRelations       — stored ties (spouse/parent/sibling...).
        ///   B. Implied relations     — derived kin (grandparent/aunt/cousin...)
        ///                              that vanilla computes and never stores.
        ///   C. Opinion-based ties    — friend/rival, which are not PawnRelationDefs
        ///                              at all and must be synthesized.
        /// Without B and C a typical scenario start (three unrelated colonists)
        /// produced a completely empty Social section.
        ///
        /// Idempotent: an existing active entry for the same (relation, other)
        /// pair is never duplicated, so this is safe to call repeatedly as a
        /// backfill. Ended relations are left untouched and never resurrected.
        /// </summary>
        public static void CaptureInitialRelations(Pawn pawn, PawnObject record)
        {
            if (pawn == null || record == null || pawn.relations == null)
            {
                return;
            }
            if (record.Relations == null)
            {
                record.Relations = new List<SignificantRelation>();
            }

            long anchorTick = ResolveRelationAnchorTick(record);
            SocialRelationPolicyDef policy = SocialRelationFilter.Policy;

            CaptureDirectRelations(pawn, record, anchorTick);
            if (policy == null || policy.includeImpliedRelations)
            {
                CaptureImpliedRelations(pawn, record, anchorTick);
            }
            if (policy == null || policy.includeOpinionRelations)
            {
                CaptureOpinionRelations(pawn, record, policy, anchorTick);
            }
        }

        /// <summary>
        /// Resolves the tick an initial relation should be anchored to. A brand
        /// new game anchors at 0 (the ties predate the colony); a mid-save install
        /// anchors at the pawn's join tick. -1 stays the "unknown" sentinel.
        /// </summary>
        private static long ResolveRelationAnchorTick(PawnObject record)
        {
            if (record.JoinTick > 0L)
            {
                return record.JoinTick;
            }
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            return now > 0 ? (long)now : 0L;
        }

        private static void CaptureDirectRelations(Pawn pawn, PawnObject record, long anchorTick)
        {
            List<DirectPawnRelation> directRelations = pawn.relations.DirectRelations;
            if (directRelations == null)
            {
                return;
            }
            for (int i = 0; i < directRelations.Count; i++)
            {
                DirectPawnRelation rel = directRelations[i];
                if (rel?.def == null || rel.otherPawn == null)
                {
                    continue;
                }
                if (!SocialRelationFilter.IsSignificant(rel.def))
                {
                    continue;
                }
                AddRelationIfAbsent(record, rel.def.defName, rel.otherPawn, anchorTick);
            }
        }

        /// <summary>
        /// Source B: derived kinship. <c>PotentiallyRelatedPawns</c> is the same
        /// candidate set vanilla's social tab walks, and <c>GetRelations</c> runs
        /// the relation workers that produce grandparent/aunt/cousin/kin ties.
        /// </summary>
        private static void CaptureImpliedRelations(Pawn pawn, PawnObject record, long anchorTick)
        {
            IEnumerable<Pawn> candidates;
            try
            {
                candidates = pawn.relations.PotentiallyRelatedPawns;
            }
            catch
            {
                // Defensive: a malformed relation graph from another mod must not
                // abort the whole archive write.
                return;
            }
            if (candidates == null)
            {
                return;
            }
            foreach (Pawn other in candidates)
            {
                if (other == null || other == pawn)
                {
                    continue;
                }
                IEnumerable<PawnRelationDef> defs;
                try
                {
                    defs = pawn.GetRelations(other);
                }
                catch
                {
                    continue;
                }
                if (defs == null)
                {
                    continue;
                }
                foreach (PawnRelationDef def in defs)
                {
                    if (def == null || !SocialRelationFilter.IsSignificant(def))
                    {
                        continue;
                    }
                    AddRelationIfAbsent(record, def.defName, other, anchorTick);
                }
            }
        }

        /// <summary>
        /// Source C: opinion-derived friend/rival ties, synthesized because
        /// vanilla stores no Def for them. Only the strongest |opinion| ties are
        /// kept, capped by the policy, so large colonies stay bounded.
        /// </summary>
        private static void CaptureOpinionRelations(
            Pawn pawn,
            PawnObject record,
            SocialRelationPolicyDef policy,
            long anchorTick)
        {
            if (pawn.Map == null && pawn.Faction == null)
            {
                return;
            }
            int friendThreshold = policy != null ? policy.opinionFriendThreshold : 20;
            int rivalThreshold = policy != null ? policy.opinionRivalThreshold : -20;
            int cap = policy != null ? policy.maxOpinionRelationsPerPawn : 8;
            if (cap <= 0)
            {
                return;
            }

            List<Pawn> peers = CollectSocialPeers(pawn);
            if (peers.Count == 0)
            {
                return;
            }

            List<KeyValuePair<Pawn, int>> scored = new List<KeyValuePair<Pawn, int>>();
            for (int i = 0; i < peers.Count; i++)
            {
                Pawn other = peers[i];
                int opinion;
                try
                {
                    opinion = pawn.relations.OpinionOf(other);
                }
                catch
                {
                    continue;
                }
                if (opinion >= friendThreshold || opinion <= rivalThreshold)
                {
                    scored.Add(new KeyValuePair<Pawn, int>(other, opinion));
                }
            }
            if (scored.Count == 0)
            {
                return;
            }
            // Strongest feelings first, so the cap keeps the most meaningful ties.
            scored.Sort((a, b) => System.Math.Abs(b.Value).CompareTo(System.Math.Abs(a.Value)));

            int taken = 0;
            for (int i = 0; i < scored.Count && taken < cap; i++)
            {
                Pawn other = scored[i].Key;
                int opinion = scored[i].Value;
                string key = opinion >= friendThreshold
                    ? SocialRelationFilter.FriendRelationKey
                    : SocialRelationFilter.RivalRelationKey;
                if (AddRelationIfAbsent(record, key, other, anchorTick))
                {
                    taken++;
                }
            }
        }

        /// <summary>
        /// Humanlike pawns this pawn could plausibly have an opinion about:
        /// current map colonists plus same-faction members. Animals and non
        /// humanlikes are excluded.
        /// </summary>
        private static List<Pawn> CollectSocialPeers(Pawn pawn)
        {
            List<Pawn> peers = new List<Pawn>();
            HashSet<Pawn> seen = new HashSet<Pawn>();

            // Single source of truth for "current colony population" (P1: the
            // definition must not be duplicated). Covers maps + world caravans.
            List<ColonyMember> members;
            try
            {
                members = ChronicleColonistScanner.EnumerateCurrentPeople();
            }
            catch
            {
                members = null;
            }
            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    ColonyMember member = members[i];
                    if (member != null)
                    {
                        AddPeer(pawn, member.Pawn, peers, seen);
                    }
                }
            }

            // Also consider co-located humanlikes (visitors, prisoners not yet
            // counted as population) so pre-colony ties are not missed.
            Map map = pawn.Map;
            if (map != null && map.mapPawns != null)
            {
                IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
                if (spawned != null)
                {
                    for (int i = 0; i < spawned.Count; i++)
                    {
                        AddPeer(pawn, spawned[i], peers, seen);
                    }
                }
            }
            return peers;
        }

        private static void AddPeer(Pawn self, Pawn candidate, List<Pawn> peers, HashSet<Pawn> seen)
        {
            if (candidate == null || candidate == self)
            {
                return;
            }
            if (candidate.RaceProps == null || !candidate.RaceProps.Humanlike)
            {
                return;
            }
            if (!seen.Add(candidate))
            {
                return;
            }
            peers.Add(candidate);
        }

        /// <summary>
        /// Adds a relation entry unless an active one already exists for the same
        /// (relation, other pawn) pair. Returns true when a new entry was added.
        /// </summary>
        private static bool AddRelationIfAbsent(
            PawnObject record,
            string relationDefName,
            Pawn other,
            long anchorTick)
        {
            if (string.IsNullOrEmpty(relationDefName) || other == null)
            {
                return false;
            }
            string otherId = other.GetUniqueLoadID();
            if (string.IsNullOrEmpty(otherId))
            {
                return false;
            }
            for (int j = 0; j < record.Relations.Count; j++)
            {
                SignificantRelation existing = record.Relations[j];
                if (existing != null && existing.IsActive
                    && existing.RelationDefName == relationDefName
                    && existing.OtherStableId == otherId)
                {
                    return false;
                }
            }
            record.Relations.Add(new SignificantRelation
            {
                RelationDefName = relationDefName,
                OtherStableId = otherId,
                OtherLabel = other.LabelShort,
                // DirectPawnRelation.startTicks is relative to the pawn's age, not
                // an absolute game tick, so the archive anchors to a known moment.
                FormedTick = anchorTick,
                EndedTick = -1L
            });
            return true;
        }

        public static void ApplyDeathSnapshots(PawnObject record, Pawn pawn)
        {
            if (record == null)
            {
                return;
            }
            if (record.SkillSnapshotOnDeath == null)
            {
                record.SkillSnapshotOnDeath = new Dictionary<string, int>();
            }
            if (pawn != null)
            {
                CaptureSkills(pawn, record.SkillSnapshotOnDeath);
            }
        }
    }
}

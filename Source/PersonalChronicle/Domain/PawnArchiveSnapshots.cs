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
        /// v4.6: snapshot the pawn's existing significant direct relations at join
        /// time so the Social tab can render initial ties (spouse, parent, friend...)
        /// even after the pawn has died or left the colony. Relations already
        /// captured by live change patches are skipped to avoid duplicates.
        /// </summary>
        public static void CaptureInitialRelations(Pawn pawn, PawnObject record)
        {
            if (pawn?.relations?.DirectRelations == null || record == null)
            {
                return;
            }
            if (record.Relations == null)
            {
                record.Relations = new List<SignificantRelation>();
            }
            List<DirectPawnRelation> directRelations = pawn.relations.DirectRelations;
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
                string otherId = rel.otherPawn.GetUniqueLoadID();
                if (string.IsNullOrEmpty(otherId))
                {
                    continue;
                }
                // Skip if an active snapshot for this pair already exists.
                bool exists = false;
                for (int j = 0; j < record.Relations.Count; j++)
                {
                    SignificantRelation existing = record.Relations[j];
                    if (existing != null && existing.IsActive
                        && existing.RelationDefName == rel.def.defName
                        && existing.OtherStableId == otherId)
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists)
                {
                    continue;
                }
                record.Relations.Add(new SignificantRelation
                {
                    RelationDefName = rel.def.defName,
                    OtherStableId = otherId,
                    OtherLabel = rel.otherPawn.LabelShort,
                    // startTicks on DirectPawnRelation is relative to the pawn's age, not
                    // an absolute game tick; record the join moment as a known anchor.
                    FormedTick = -1L,
                    EndedTick = -1L
                });
            }
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

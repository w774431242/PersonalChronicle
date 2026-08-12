using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Archived pawn (colonist). Carries every v0.2 PawnRecord field so the
    /// legacy view type (and the v0.2 downgrade mirror) stays field-compatible.
    ///
    /// v3.1 career + v3.1 P3 place/social fields are append-only (Scribe-safe).
    /// </summary>
    public sealed class PawnObject : PawnRecord
    {
        /// <summary>Legacy priority snapshot (no longer shown in UI; kept for save compat).</summary>
        public List<WorkPrioritySnapshot> WorkSnapshot = new List<WorkPrioritySnapshot>();

        /// <summary>Skill levels at join / first archive (skillDefName → level).</summary>
        public Dictionary<string, int> SkillSnapshot = new Dictionary<string, int>();

        /// <summary>Skill levels at death (empty while alive).</summary>
        public Dictionary<string, int> SkillSnapshotOnDeath = new Dictionary<string, int>();

        /// <summary>v3.1 cumulative labour by WorkTypeDef.defName.</summary>
        public WorkTimeAccumulator WorkTime = new WorkTimeAccumulator();

        /// <summary>v4.0 cumulative production quantity and market value.</summary>
        public ProductionAccumulator Production = new ProductionAccumulator();

        /// <summary>Language-independent backstory defNames for overview identity.</summary>
        public string ChildhoodBackstoryDefName;
        public string AdulthoodBackstoryDefName;

        /// <summary>Last known map biome / place key (footprint summary).</summary>
        public string PrimaryPlaceDefName;

        /// <summary>v3.1 P3: place visit ledger (enter/leave).</summary>
        public List<PlaceVisit> PlaceHistory = new List<PlaceVisit>();

        /// <summary>v3.1 P3: significant social ties (active + ended).</summary>
        public List<SignificantRelation> Relations = new List<SignificantRelation>();

        /// <summary>v6.8 个人战斗维度（击杀宫格）：生涯累计造成伤害总值（来自每次击杀时补刀 DamageInfo.Amount 近似累加）。</summary>
        public float DamageDealtTotal;

        /// <summary>v6.8 个人战斗维度：累计参与的战役次数（每次战役开局 AttachBattleRoster 对该殖民者 +1）。</summary>
        public int ParticipatedBattles;

        /// <summary>v6.8 个人战斗维度：近战击杀数（武器 PrimaryVerb.IsMelee 判定）。</summary>
        public int MeleeKills;

        /// <summary>v6.8 个人战斗维度：远程击杀数（非近战武器判定）。</summary>
        public int RangedKills;

        // CategoryKey 继承自 PawnRecord（均返回 ArchiveCategoryKeys.Pawn），
        // 无需在此重复 override —— 移除冗余分流逻辑，避免两处维护。
        public PawnObject()
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref WorkSnapshot, "workSnapshot", LookMode.Deep);
            Scribe_Collections.Look(ref SkillSnapshot, "skillSnapshot", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SkillSnapshotOnDeath, "skillSnapshotOnDeath", LookMode.Value, LookMode.Value);
            Scribe_Deep.Look(ref WorkTime, "workTime");
            Scribe_Deep.Look(ref Production, "production");
            Scribe_Values.Look(ref ChildhoodBackstoryDefName, "childhoodBackstoryDefName");
            Scribe_Values.Look(ref AdulthoodBackstoryDefName, "adulthoodBackstoryDefName");
            Scribe_Values.Look(ref PrimaryPlaceDefName, "primaryPlaceDefName");
            Scribe_Collections.Look(ref PlaceHistory, "placeHistory", LookMode.Deep);
            Scribe_Collections.Look(ref Relations, "relations", LookMode.Deep);
            Scribe_Values.Look(ref DamageDealtTotal, "damageDealtTotal", 0f);
            Scribe_Values.Look(ref ParticipatedBattles, "participatedBattles", 0);
            Scribe_Values.Look(ref MeleeKills, "meleeKills", 0);
            Scribe_Values.Look(ref RangedKills, "rangedKills", 0);
            if (WorkSnapshot == null) WorkSnapshot = new List<WorkPrioritySnapshot>();
            if (SkillSnapshot == null) SkillSnapshot = new Dictionary<string, int>();
            if (SkillSnapshotOnDeath == null) SkillSnapshotOnDeath = new Dictionary<string, int>();
            if (WorkTime == null) WorkTime = new WorkTimeAccumulator();
            if (Production == null) Production = new ProductionAccumulator();
            if (PlaceHistory == null) PlaceHistory = new List<PlaceVisit>();
            if (Relations == null) Relations = new List<SignificantRelation>();
        }
    }
}

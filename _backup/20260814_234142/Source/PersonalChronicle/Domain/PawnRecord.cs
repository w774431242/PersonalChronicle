using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// v0.2 persistent snapshot of a colonist; in v2.1 it doubles as the view
    /// base of <see cref="PawnObject"/> (PawnObject IS-A PawnRecord, so v0.2
    /// service/UI signatures keep working). Only stable ids and name snapshots
    /// are saved — no user-visible text, no live pawn references.
    /// Language-independent by design.
    /// </summary>
    public class PawnRecord : ArchiveObject
    {
        public string LabelShort;
        public string KindDefName;
        public string FactionDefName;
        public long JoinTick = -1L;
        public long DeathTick = -1L;
        public string DeathCauseKey;

        /// <summary>
        /// 殖民地人口角色（自由殖民者 / 奴隶 / 囚犯）。默认 FreeColonist，老存档
        /// 读回时若缺字段也回落到 FreeColonist，不破坏历史记录。int 落盘以避免
        /// 枚举 Scribe 兼容性歧义。
        /// </summary>
        public PawnRole Role = PawnRole.FreeColonist;

        public override string CategoryKey
        {
            get { return ArchiveCategoryKeys.Pawn; }
        }

        public bool IsArchived
        {
            get { return DeathTick > 0L; }
        }

        public PawnRecord()
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref LabelShort, "labelShort");
            Scribe_Values.Look(ref KindDefName, "kindDefName");
            Scribe_Values.Look(ref FactionDefName, "factionDefName");
            Scribe_Values.Look(ref JoinTick, "joinTick", -1L);
            Scribe_Values.Look(ref DeathTick, "deathTick", -1L);
            Scribe_Values.Look(ref DeathCauseKey, "deathCauseKey");
            int role = (int)Role;
            Scribe_Values.Look(ref role, "role", (int)PawnRole.FreeColonist);
            Role = (PawnRole)role;
        }
    }
}

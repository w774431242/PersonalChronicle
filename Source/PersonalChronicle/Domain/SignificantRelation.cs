using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Archived significant social tie (lover/spouse/family…). Formed/ended
    /// ticks make the Social tab readable after death. RelationDefName is a
    /// language-independent data key (PawnRelationDef.defName).
    /// </summary>
    public class SignificantRelation : IExposable
    {
        public string RelationDefName;
        public string OtherStableId;
        public string OtherLabel;
        public long FormedTick = -1L;
        /// <summary>-1 while the relation is still active.</summary>
        public long EndedTick = -1L;

        public SignificantRelation()
        {
        }

        public bool IsActive
        {
            get { return EndedTick < 0L; }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref RelationDefName, "relationDefName");
            Scribe_Values.Look(ref OtherStableId, "otherStableId");
            Scribe_Values.Look(ref OtherLabel, "otherLabel");
            Scribe_Values.Look(ref FormedTick, "formedTick", -1L);
            Scribe_Values.Look(ref EndedTick, "endedTick", -1L);
        }
    }
}

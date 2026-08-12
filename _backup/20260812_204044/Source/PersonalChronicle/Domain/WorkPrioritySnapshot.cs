using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// One entry of a pawn's work priority for a single WorkType, stored as a
    /// historical snapshot on <see cref="PawnObject"/>. Used to render a
    /// degraded priority list when the live pawn is dead or absent.
    /// Class name + namespace are persisted by Scribe, so they must stay
    /// stable forever (renaming breaks old saves).
    /// </summary>
    public class WorkPrioritySnapshot : IExposable
    {
        public string WorkTypeDefName;

        public int Priority;

        public WorkPrioritySnapshot()
        {
        }

        public WorkPrioritySnapshot(string workTypeDefName, int priority)
        {
            WorkTypeDefName = workTypeDefName;
            Priority = priority;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref WorkTypeDefName, "workTypeDefName");
            Scribe_Values.Look(ref Priority, "priority", 0);
        }
    }
}

using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 班组倒班例外（Phase 2，2026-09-11）：现实制造业里的**调休 / 请假 / 加班顶班**。
    ///
    /// <para>键 = （绝对游戏日, 班组序号）；类型见
    /// <see cref="WorkshopCrewCycle.ExceptionNone"/> / <c>ExceptionLeave</c> / <c>ExceptionExtraWork</c>。
    /// 只存例外项（正常班表不占存档），并在每次计划重建时裁剪过期项。</para>
    /// </summary>
    public sealed class WorkshopCrewException : IExposable
    {
        /// <summary>绝对游戏日（<c>GenDate.DaysPassed</c>）。</summary>
        public int Day;

        /// <summary>班组序号（<c>Zone_Workshop.Crews</c> 的下标）。</summary>
        public int CrewIndex;

        /// <summary>例外类型（0/1/2，见 WorkshopCrewCycle）。</summary>
        public int Kind;

        public void ExposeData()
        {
            Scribe_Values.Look(ref Day, "day", 0);
            Scribe_Values.Look(ref CrewIndex, "crewIndex", 0);
            Scribe_Values.Look(ref Kind, "kind", 0);
        }
    }
}

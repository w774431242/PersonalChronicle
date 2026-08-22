using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Per-pawn cumulative work-time ledger (archive career data).
    /// Key = WorkTypeDef.defName; values are game ticks spent in that work type.
    /// Filled by low-frequency GameComponent sampling — never work priorities.
    /// </summary>
    public class WorkTimeAccumulator : IExposable
    {
        public Dictionary<string, long> TicksByWorkType = new Dictionary<string, long>();
        public Dictionary<string, long> LastTickByWorkType = new Dictionary<string, long>();
        public long TotalWorkTicks;
        /// <summary>
        /// First tick for which this ledger observed work. This is the safe
        /// fallback for legacy/backfilled records whose JoinTick is unknown.
        /// </summary>
        public long FirstObservedTick = -1L;

        public WorkTimeAccumulator()
        {
        }

        /// <summary>
        /// Adds sample ticks for a work type and records the sample wall-clock tick.
        /// </summary>
        public void AddSample(string workTypeDefName, long sampleTicks, long gameTick)
        {
            if (string.IsNullOrEmpty(workTypeDefName) || sampleTicks <= 0L)
            {
                return;
            }
            if (TicksByWorkType == null)
            {
                TicksByWorkType = new Dictionary<string, long>();
            }
            if (LastTickByWorkType == null)
            {
                LastTickByWorkType = new Dictionary<string, long>();
            }
            if (FirstObservedTick < 0L)
            {
                FirstObservedTick = gameTick;
            }
            long prev;
            if (!TicksByWorkType.TryGetValue(workTypeDefName, out prev))
            {
                prev = 0L;
            }
            TicksByWorkType[workTypeDefName] = prev + sampleTicks;
            LastTickByWorkType[workTypeDefName] = gameTick;
            TotalWorkTicks += sampleTicks;
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref TicksByWorkType, "ticksByWorkType", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref LastTickByWorkType, "lastTickByWorkType", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref TotalWorkTicks, "totalWorkTicks", 0L);
            Scribe_Values.Look(ref FirstObservedTick, "firstObservedTick", -1L);
            if (TicksByWorkType == null)
            {
                TicksByWorkType = new Dictionary<string, long>();
            }
            if (LastTickByWorkType == null)
            {
                LastTickByWorkType = new Dictionary<string, long>();
            }
        }
    }
}

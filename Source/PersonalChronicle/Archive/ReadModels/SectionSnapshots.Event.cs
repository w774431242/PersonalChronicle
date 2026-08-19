// Phase 1 partial split (08-架构层-代码轻量化方案.md §3.4): single-event detail view.
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// Single-event detail read model (P2-6). The window renders the human-readable
    /// description from <see cref="ChronicleEvent.TypeKey"/> + <see cref="ChronicleEvent.Params"/>
    /// via the event Def template, so the snapshot only carries the raw event.
    /// </summary>
    public sealed class EventSnapshot : ArchiveSectionSnapshot
    {
        public ChronicleEvent Event;

        /// <summary>
        /// Later events of the same primary object (Tick &gt; this event), capped at
        /// 3 and sorted ascending. Derived in the Read Model; the window renders it
        /// without re-querying / re-sorting (v4.6.5 boundary fix).
        /// </summary>
        public IReadOnlyList<ChronicleEvent> FollowupEvents = new List<ChronicleEvent>();
    }
}

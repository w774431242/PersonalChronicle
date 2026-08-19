using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Api;
using PersonalChronicle.Api.DomainProviders;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;
using UnityEngine;

namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// Partial of <see cref="ArchiveUiDataProvider"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveUiDataProvider : IArchiveUiDataProvider
    {

        public EventSnapshot BuildEvent(IArchiveService service, ChronicleEvent ev, long revision)
        {
            EventSnapshot snap = new EventSnapshot { BuiltFromRevision = revision, IsEmpty = true, Event = ev };
            if (ev == null)
            {
                return snap;
            }
            snap.IsEmpty = false;
            // v4.6.5: follow-up events derived here (Read Model), not in the window.
            // Later events of the same primary object, capped at 3, ascending by tick.
            if (service != null && ev.Primary != null && !string.IsNullOrEmpty(ev.Primary.StableId))
            {
                IReadOnlyList<ChronicleEvent> evs = service.GetEventsFor(ev.Primary.StableId);
                if (evs != null)
                {
                    snap.FollowupEvents = evs
                        .Where(e => e != null && e.Tick > ev.Tick)
                        .OrderBy(e => e.Tick)
                        .Take(3)
                        .ToList();
                }
            }
            return snap;
        }

        /// <summary>
        /// Full timeline event stream, sorted ascending by <see cref="ChronicleEvent.Tick"/>
        /// and null-guarded. The Home timeline only consumes this snapshot; the ordering
        /// and null-filtering never leak into the window's draw path (design doc §7.4).
        /// </summary>
        public IReadOnlyList<ChronicleEvent> BuildTimelineEvents(IArchiveService service, long revision)
        {
            if (service == null)
            {
                return System.Array.Empty<ChronicleEvent>();
            }
            IReadOnlyList<ChronicleEvent> all = service.GetAllEvents();
            if (all == null)
            {
                return System.Array.Empty<ChronicleEvent>();
            }
            return all
                .Where(e => e != null)
                .OrderBy(e => e.Tick)
                .ToList();
        }

        /// <summary>
        /// Event count for a stable id, memoized so sort comparators never issue
        /// repeated <see cref="IArchiveService.GetEventsFor"/> queries.
        /// </summary>
        private static int EventCount(IArchiveService service, Dictionary<string, int> cache, string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return 0;
            }
            if (cache.TryGetValue(stableId, out int cached))
            {
                return cached;
            }
            IReadOnlyList<ChronicleEvent> evs = service.GetEventsFor(stableId);
            int c = evs == null ? 0 : evs.Count;
            cache[stableId] = c;
            return c;
        }

    }
}

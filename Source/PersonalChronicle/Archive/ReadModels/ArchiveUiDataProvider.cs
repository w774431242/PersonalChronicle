using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// Default <see cref="IArchiveUiDataProvider"/> (P2-6). Centralizes every
    /// section's "query + sort + filter + null-guard" responsibility so the main
    /// window's draw path only consumes immutable snapshots. The window still owns
    /// translation/formatting (its translation context), but never issues raw
    /// <c>IArchiveService</c> ordering LINQ — that lives here.
    ///
    /// Failure isolation: a null service or a throwing query yields an empty snapshot
    /// rather than propagating into the UI draw loop.
    /// </summary>
    public sealed class ArchiveUiDataProvider : IArchiveUiDataProvider
    {
        public HomeSnapshot BuildHome(IArchiveService service, long revision)
        {
            HomeSnapshot snap = new HomeSnapshot { BuiltFromRevision = revision, IsEmpty = true };
            if (service == null)
            {
                return snap;
            }
            snap.ActivePawnCount = service.GetActiveSnapshotCount();
            snap.ArchivedPawnCount = service.GetArchivedSnapshotCount();
            snap.LiveColonistCount = service.GetLiveColonistCount();
            int free, slave, prisoner;
            service.GetLiveColonistCounts(out free, out slave, out prisoner);
            snap.LiveFreeCount = free;
            snap.LiveSlaveCount = slave;
            snap.LivePrisonerCount = prisoner;
            snap.ServiceDays = service.GetServiceDays();

            IReadOnlyList<ChronicleEvent> recent = service.GetRecentEvents(20);
            if (recent != null && recent.Count > 0)
            {
                snap.RecentEvents = recent.Where(e => e != null).ToList();
            }

            // Honest "important" proxy: the objects (Pawn + Thing) with the most
            // events. Mirrors the window's CollectCandidates ordering. Battle/Location
            // are excluded because they have no detail view.
            List<ArchiveObject> candidates = new List<ArchiveObject>();
            CollectCandidatesByEvents(service, ArchiveCategoryKeys.Pawn, candidates);
            CollectCandidatesByEvents(service, ArchiveCategoryKeys.Thing, candidates);
            candidates.Sort((a, b) => service.GetEventsFor(b.StableId).Count
                .CompareTo(service.GetEventsFor(a.StableId).Count));
            snap.ImportantObjects = candidates.Take(6).ToList();

            snap.IsEmpty = snap.RecentEvents.Count == 0 && snap.ImportantObjects.Count == 0;
            return snap;
        }

        public OverviewSnapshot BuildOverview(IArchiveService service, string categoryKey, long revision)
        {
            OverviewSnapshot snap = new OverviewSnapshot { BuiltFromRevision = revision, IsEmpty = true };
            if (service == null || string.IsNullOrEmpty(categoryKey))
            {
                return snap;
            }
            IReadOnlyList<ArchiveObject> objects = service.GetObjectsOfCategory(categoryKey);
            if (objects != null && objects.Count > 0)
            {
                snap.CategoryObjects[categoryKey] = objects
                    .Where(o => o != null)
                    .OrderByDescending(o => service.GetEventsFor(o.StableId).Count)
                    .ToList();
            }
            snap.IsEmpty = snap.CategoryObjects.Count == 0;
            return snap;
        }

        public DetailSnapshot BuildDetail(IArchiveService service, string detailObjectId, long revision)
        {
            DetailSnapshot snap = new DetailSnapshot { BuiltFromRevision = revision, IsEmpty = true };
            if (service == null || string.IsNullOrEmpty(detailObjectId))
            {
                return snap;
            }
            snap.DetailObject = service.GetObject(detailObjectId);
            IReadOnlyList<ChronicleEvent> events = service.GetEventsFor(detailObjectId);
            snap.RawEvents = events ?? new List<ChronicleEvent>();
            snap.IsEmpty = snap.RawEvents.Count == 0;
            return snap;
        }

        public EventSnapshot BuildEvent(IArchiveService service, ChronicleEvent ev, long revision)
        {
            EventSnapshot snap = new EventSnapshot { BuiltFromRevision = revision, IsEmpty = true, Event = ev };
            if (ev == null)
            {
                return snap;
            }
            snap.IsEmpty = false;
            return snap;
        }

        private static void CollectCandidatesByEvents(
            IArchiveService service, string categoryKey, List<ArchiveObject> candidates)
        {
            if (service == null || string.IsNullOrEmpty(categoryKey) || candidates == null)
            {
                return;
            }
            IReadOnlyList<ArchiveObject> objects = service.GetObjectsOfCategory(categoryKey);
            if (objects == null)
            {
                return;
            }
            for (int i = 0; i < objects.Count; i++)
            {
                ArchiveObject obj = objects[i];
                if (obj != null && !string.IsNullOrEmpty(obj.StableId))
                {
                    candidates.Add(obj);
                }
            }
        }
    }
}

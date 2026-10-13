using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Api;
using PersonalChronicle.Api.DomainProviders;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;
using UnityEngine;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// Partial of <see cref="ArchiveUiDataProvider"/> —see main file for the class doc.
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
            try
            {
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
            catch (System.Exception ex)
            {
                // 🔴 类注释承诺失败隔离，此前无实现（2026-09-19 审计）。
                // 严重性同 BuildHome：调用链全程无 catch ⇒ 异常直抛进原版绘制循环。
                // 降级：保留 Event（它是入参，与查询无关），只丢派生出来的后续事件。
                ChronicleLog.Error(ChronicleLog.Category.Ui, "archive event snapshot failed: " + ex);
                return new EventSnapshot
                {
                    BuiltFromRevision = revision,
                    IsEmpty = false,
                    Event = ev
                };
            }
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
            try
            {
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
            catch (System.Exception ex)
            {
                // 🔴 同 BuildHome / BuildEvent：调用链全程无 catch，异常会直抛进原版绘制循环。
                // 降级返回**空数组**（不是 null —— 调用方按 IReadOnlyList 迭代，null 会再炸一次）。
                ChronicleLog.Error(ChronicleLog.Category.Ui, "archive timeline events failed: " + ex);
                return System.Array.Empty<ChronicleEvent>();
            }
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

using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Application.PawnEconomy.Perception
{
    internal sealed partial class PerceptionCoordinator
    {
        // Reads only published scalar snapshots; never calls the source or creates economic records.
        public FactQueryResult QueryFacts(PerceptionReadRequest request, int now)
        {
            AssertOwnerThread();
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.SessionEpoch != SessionEpoch) return new FactQueryResult(PerceptionReadiness.OutOfScope, "SessionMismatch", false);
            if (now < 0 || now < lastTick) return new FactQueryResult(PerceptionReadiness.Error, "ClockMovedBackwards", false);
            if (!snapshots.TryGetValue(request.PawnId, out var snapshot))
                return new FactQueryResult(populationComplete && !active.ContainsKey(request.PawnId)
                    ? PerceptionReadiness.OutOfScope : PerceptionReadiness.Loading, "NotObserved", false);
            var context = snapshot.Context.Data;
            if (context != null && (context.Location == SubjectLocation.Dead || context.Location == SubjectLocation.OffMap))
                return new FactQueryResult(PerceptionReadiness.OutOfScope, context.Location.ToString(), false, snapshot);
            if (active.TryGetValue(request.PawnId, out var entry) && entry.Subject.MapId != request.MapId
                || context != null && context.MapId != request.MapId)
                return new FactQueryResult(PerceptionReadiness.OutOfScope, "MapMismatch", false, snapshot);
            var statuses = new List<GroupReadStatus>(4)
            {
                Assess(PerceptionGroups.Context, snapshot.Context, request, now),
                Assess(PerceptionGroups.Needs, snapshot.Needs, request, now),
                Assess(PerceptionGroups.Body, snapshot.Body, request, now),
                Assess(PerceptionGroups.Relationships, snapshot.Relationships, request, now)
            };
            bool usable = true, partial = false, stale = false, loading = false, error = false;
            foreach (var status in statuses)
            {
                if (status.Readiness != PerceptionReadiness.Ready) partial = true;
                if (!status.Required) continue;
                // ECON-FIX（2026-10-10 实测定位）：`Partial` 表示"该组**有数据**，但部分条目不支持/未取到"
                // （NeedFactSet 的 unsupported / failed / truncated 三者任一非空）。数据本身可用 ⇒ 不得据此判死消费者。
                // 此前只认 `Ready`：实测中 Needs 因某个需求的 StandardRatio 不支持而恒为 Partial，
                // 于是三名殖民者的规划永久 `FactsUnavailable`（opportunities=0、经济整链停摆），
                // 而采集侧指标（oldestDueAgeTicks=0 / errors=0）完全正常 —— 典型的"判据过严导致饿死"。
                if (status.Readiness != PerceptionReadiness.Ready && status.Readiness != PerceptionReadiness.Partial)
                    usable = false;
                error |= status.Readiness == PerceptionReadiness.Error;
                stale |= status.Readiness == PerceptionReadiness.Stale;
                loading |= status.Readiness == PerceptionReadiness.Loading;
            }
            PerceptionReadiness readiness = error ? PerceptionReadiness.Error : stale ? PerceptionReadiness.Stale
                : loading ? PerceptionReadiness.Loading : partial ? PerceptionReadiness.Partial : PerceptionReadiness.Ready;
            return new FactQueryResult(readiness, readiness.ToString(), usable, snapshot, statuses);
        }
        private static GroupReadStatus Assess<T>(PerceptionGroups group, FactGroup<T> fact,
            PerceptionReadRequest request, int now) where T : class
        {
            bool required = (request.RequiredGroups & group) != 0;
            long age = fact.State == CaptureState.Unknown ? -1 : (long)now - fact.CapturedTick;
            PerceptionReadiness readiness = fact.State == CaptureState.Unknown ? PerceptionReadiness.Loading
                : age < 0 ? PerceptionReadiness.Error : fact.State == CaptureState.Error ? PerceptionReadiness.Error
                : age > request.MaximumAge(group) ? PerceptionReadiness.Stale
                : fact.State == CaptureState.Partial ? PerceptionReadiness.Partial : PerceptionReadiness.Ready;
            string reason = age < 0 && fact.State != CaptureState.Unknown ? "FutureSourceTick"
                : readiness == PerceptionReadiness.Stale ? "AgeExceeded" : fact.ReasonCode;
            return new GroupReadStatus(group, fact.State, age, required, readiness, reason);
        }
        public ObservationQueryResult QueryObservation(PerceptionReadRequest request, int now,
            IReadOnlyDictionary<string, NeedTarget> targets)
        {
            var facts = QueryFacts(request, now);
            return new ObservationQueryResult(facts, ProjectObservedNeeds(facts, targets));
        }
        private IReadOnlyList<CurrentNeedGap> ProjectObservedNeeds(FactQueryResult facts,
            IReadOnlyDictionary<string, NeedTarget> targets)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            var result = new List<CurrentNeedGap>();
            var snapshot = facts.Snapshot;
            if (snapshot?.Needs.Data == null) return result.AsReadOnly();
            foreach (var need in snapshot.Needs.Data.Needs)
            {
                NeedTarget target = default;
                targets?.TryGetValue(need.NeedDefName, out target);
                result.Add(CurrentNeedProjector.Project(snapshot.PawnId, need, target));
            }
            return result.AsReadOnly(); // Caller retains facts readiness/ages; projection does not grant action permission.
        }
    }
}

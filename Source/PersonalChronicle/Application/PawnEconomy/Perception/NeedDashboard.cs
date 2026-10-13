using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Application.PawnEconomy.Perception
{
    internal interface INeedDashboardSource
    {
        long Epoch { get; }
        PerceptionReadiness Availability { get; }
        IReadOnlyList<PerceptionSubject> Subjects { get; }
        PerceptionDiagnostics Diagnostics { get; }
        FactQueryResult Read(PerceptionReadRequest request, int now);
    }
    internal sealed class PawnNeedSummary
    {
        public string PawnId { get; }
        public Fact<string> Name { get; }
        public FactQueryResult Facts { get; }
        public PawnNeedSummary(string id, FactQueryResult facts)
        { PawnId = id; Facts = facts; Name = facts.Snapshot?.Context.Data?.DisplayName ?? default; }
    }
    internal sealed class NeedDashboardSnapshot
    {
        public long Epoch { get; }
        public int MapId { get; }
        public int BuiltTick { get; }
        public int MapSubjects { get; }
        public int PageIndex { get; }
        public int PageCount { get; }
        public PerceptionDiagnostics Diagnostics { get; }
        public IReadOnlyList<PawnNeedSummary> Roster { get; }
        public string SelectedId { get; }
        public ObservationQueryResult Selected { get; }
        public NeedDashboardSnapshot(long epoch, int map, int tick, int count, int page, int pages,
            PerceptionDiagnostics diagnostics, IEnumerable<PawnNeedSummary> roster, string selectedId, ObservationQueryResult selected)
        {
            Epoch = epoch; MapId = map; BuiltTick = tick; MapSubjects = count; PageIndex = page; PageCount = pages;
            Diagnostics = diagnostics; Roster = PerceptionCollections.Freeze(roster); SelectedId = selectedId; Selected = selected;
        }
    }
    internal static class NeedDashboardBuilder
    {
        public const int PageSize = 12, FastMaximumAge = 300, BodyMaximumAge = 500, RelationshipMaximumAge = 1200;
        public static NeedDashboardSnapshot Build(INeedDashboardSource source, int mapId, int now, int page,
            string selectedId, IReadOnlyDictionary<string, NeedTarget> targets = null,
            Func<string, IReadOnlyDictionary<string, NeedTarget>> targetProvider = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            long epoch = source.Epoch;
            var diagnostics = source.Diagnostics;
            if (epoch <= 0) return new NeedDashboardSnapshot(0, mapId, now, 0, 0, 1, diagnostics, null, selectedId,
                new ObservationQueryResult(new FactQueryResult(source.Availability, "GameLoading", false), null));
            var ids = new List<string>(); var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var subject in source.Subjects)
                if (subject.MapId == mapId && seen.Add(subject.PawnId)) ids.Add(subject.PawnId);
            ids.Sort(StringComparer.Ordinal);
            int pages = Math.Max(1, (ids.Count + PageSize - 1) / PageSize);
            page = Math.Max(0, Math.Min(page, pages - 1));
            var roster = new List<PawnNeedSummary>();
            FactQueryResult selected = null;
            int start = page * PageSize, end = Math.Min(ids.Count, start + PageSize);
            if (selectedId == null && start < end) selectedId = ids[start];
            for (int i = start; i < end; i++)
            {
                var facts = Read(source, ids[i], epoch, mapId, now, PerceptionGroups.Context | PerceptionGroups.Needs);
                roster.Add(new PawnNeedSummary(ids[i], facts));
                if (ids[i] == selectedId) selected = facts;
            }
            // Keep a vanished selection explicit. Never silently replace it with another Pawn.
            if (selectedId != null && selected == null)
                selected = Read(source, selectedId, epoch, mapId, now, PerceptionGroups.Context | PerceptionGroups.Needs);
            return new NeedDashboardSnapshot(epoch, mapId, now, ids.Count, page, pages, diagnostics, roster, selectedId,
                selected == null ? null : new ObservationQueryResult(selected, Project(selected, selectedId, targets, targetProvider)));
        }
        /// <summary>
        /// 单人查询（Pawn 检视窗「个人需求」宿主）：只读 1 个 Pawn，不构建 12 人名册。
        /// 契约：Roster 冻结为空集合（非 null，presenter 会遍历）、PageIndex=0 / PageCount=1、
        /// MapSubjects 仅统计地图内 subject 且不触发读取；epoch≤0 时与 <see cref="Build"/> 同样零请求。
        /// 缺口投影与 Build 共用 <see cref="Project"/>，防双实现漂移。
        /// <paramref name="targets"/> / <paramref name="targetProvider"/> 与 <see cref="Build"/> 同义：
        /// 不传时目标态为 Unknown（presenter 显示"未配置"），传了才产出目标与缺口。
        /// </summary>
        public static NeedDashboardSnapshot BuildSingle(INeedDashboardSource source, int mapId, int now, string pawnId,
            IReadOnlyDictionary<string, NeedTarget> targets = null,
            Func<string, IReadOnlyDictionary<string, NeedTarget>> targetProvider = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            long epoch = source.Epoch;
            var diagnostics = source.Diagnostics;
            if (epoch <= 0) return new NeedDashboardSnapshot(0, mapId, now, 0, 0, 1, diagnostics, null, pawnId,
                new ObservationQueryResult(new FactQueryResult(source.Availability, "GameLoading", false), null));
            int count = 0; var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var subject in source.Subjects)
                if (subject.MapId == mapId && seen.Add(subject.PawnId)) count++;
            FactQueryResult selected = pawnId == null ? null
                : Read(source, pawnId, epoch, mapId, now, PerceptionGroups.Context | PerceptionGroups.Needs);
            return new NeedDashboardSnapshot(epoch, mapId, now, count, 0, 1, diagnostics, null, pawnId,
                selected == null ? null : new ObservationQueryResult(selected, Project(selected, pawnId, targets, targetProvider)));
        }
        private static List<CurrentNeedGap> Project(FactQueryResult selected, string selectedId,
            IReadOnlyDictionary<string, NeedTarget> targets,
            Func<string, IReadOnlyDictionary<string, NeedTarget>> targetProvider = null)
        {
            var gaps = new List<CurrentNeedGap>();
            targets = targets ?? (selectedId == null ? null : targetProvider?.Invoke(selectedId));
            if (selected?.Snapshot?.Needs.Data == null) return gaps;
            foreach (var reading in selected.Snapshot.Needs.Data.Needs)
            {
                NeedTarget target = default;
                targets?.TryGetValue(reading.NeedDefName, out target);
                gaps.Add(CurrentNeedProjector.Project(selectedId, reading, target));
            }
            return gaps;
        }
        private static FactQueryResult Read(INeedDashboardSource source, string id, long epoch, int map, int now, PerceptionGroups required)
        {
            try
            {
                return source.Read(new PerceptionReadRequest(id, epoch, map, PerceptionPurpose.Display, required,
                    FastMaximumAge, BodyMaximumAge, RelationshipMaximumAge), now)
                    ?? new FactQueryResult(PerceptionReadiness.Error, "DashboardQueryMissing", false);
            }
            catch (Exception) { return new FactQueryResult(PerceptionReadiness.Error, "DashboardQueryFailed", false); }
        }
    }
}

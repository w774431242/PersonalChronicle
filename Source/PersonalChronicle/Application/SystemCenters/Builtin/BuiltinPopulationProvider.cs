using System.Collections.Generic;
using PersonalChronicle.Api;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Application.SystemCenters.Builtin
{
    /// <summary>
    /// 人员系统中心（殖民地档案馆）数据源 ——「① 人员档案 · 档案规模」四张计数卡。
    ///
    /// <para><b>口径（全部来自档案馆已有的持久化计数，零预演数值）</b>：</para>
    /// <list type="bullet">
    /// <item>在册档案 = <see cref="IArchiveQueryService.GetActiveSnapshotCount"/></item>
    /// <item>已归档 = <see cref="IArchiveQueryService.GetArchivedSnapshotCount"/></item>
    /// <item>现役殖民者 = <see cref="IArchiveQueryService.GetLiveColonistCount"/></item>
    /// <item>服务天数 = <see cref="IArchiveQueryService.GetServiceDays"/></item>
    /// </list>
    ///
    /// <para>刻意<b>不</b>用 <c>GetAllEvents()</c> 之类的全量查询去凑「事件总数」：那是全库扫描，
    /// 而 KPI 是每 300 tick 重建一次的热路径（GATE-PERF）。四张卡都是 O(1)/O(档案数) 的计数读取，
    /// 与档案馆首页 KPI 同源同口径（快照即真相，不另算一遍）。</para>
    ///
    /// <para>本 Provider <b>只读</b>：不写存档、不改档案状态、不产生事件。</para>
    ///
    /// <para>partial：档案规模 KPI 在本文件，<b>概览（殖民地级人事聚合）</b>见
    /// <c>BuiltinPopulationProvider.Overview.cs</c>（与制造中心的多视图 partial 同构，
    /// 契约 <see cref="IPopulationOverviewProvider"/> 由本类一并实现）。</para>
    /// </summary>
    public sealed partial class BuiltinPopulationProvider : ISystemCenterDataProvider, ISystemCenterKpiVisibility
    {
        private readonly IArchiveQueryService queries;

        public BuiltinPopulationProvider(IArchiveQueryService queries)
        {
            this.queries = queries;
        }

        public string CenterDefName
        {
            get { return SystemCenterIds.Population; }
        }

        /// <summary>
        /// 详情页**不画** KPI 带：人员中心的业务视图（档案概览）自带统计带
        /// （在册 / 已归档 / 档案跨度 / 档案对象），两条带同屏会把同一批数字画两次
        /// （预览人员中心只有统计带）。KPI 采集本身保留 —— 它同时是「该中心已接入」的判据。
        /// </summary>
        public bool ShowsKpis
        {
            get { return false; }
        }

        public string KpiSectionTitleKey
        {
            get { return "PersonalChronicle.UI.MasterControl.Section.PopulationKpi"; }
        }

        /// <summary>四张计数卡同组一行（档案规模是一个整体读数带，不拆组）。</summary>
        public IReadOnlyList<int> KpiRowSizes
        {
            get { return KpiRowLayout; }
        }

        private static readonly int[] KpiRowLayout = { 4 };

        /// <summary>
        /// 详情页主体是**档案馆主界面本体**（业务视图，走 IArchiveViewDataProvider）；
        /// 清单类章节（职责 / 事件契约 / 依赖）由注册表卡片与悬停提示承载 —— 与制造中心同规。
        /// </summary>
        public bool ShowsCenterManifest
        {
            get { return false; }
        }

        public void AppendKpis(List<SystemCenterKpiView> buffer, Map map)
        {
            if (buffer == null)
            {
                return;
            }
            if (queries == null)
            {
                AppendUnavailable(buffer);
                return;
            }

            try
            {
                buffer.Add(SystemCenterKpiFactory.Count(
                    "PersonalChronicle.UI.MasterControl.Kpi.Archive.Active",
                    queries.GetActiveSnapshotCount(), SystemCenterTone.Good));
                buffer.Add(SystemCenterKpiFactory.Count(
                    "PersonalChronicle.UI.MasterControl.Kpi.Archive.Archived",
                    queries.GetArchivedSnapshotCount(), SystemCenterTone.Flat));
                buffer.Add(SystemCenterKpiFactory.Count(
                    "PersonalChronicle.UI.MasterControl.Kpi.Archive.Colonists",
                    queries.GetLiveColonistCount(), SystemCenterTone.Flat));
                buffer.Add(SystemCenterKpiFactory.Count(
                    "PersonalChronicle.UI.MasterControl.Kpi.Archive.ServiceDays",
                    queries.GetServiceDays(), SystemCenterTone.Flat));
            }
            catch (System.Exception ex)
            {
                // 失败隔离：任一计数抛异常都不许把异常带进绘制循环，降级为「未接入」占位。
                ChronicleLog.Warning(ChronicleLog.Category.Provider,
                    "population KPI tally failed: " + ex.Message);
                buffer.Clear();
                AppendUnavailable(buffer);
            }
        }

        /// <summary>档案服务未就绪时的占位：四张卡都如实显示「未接入」，不画 0 冒充。</summary>
        private static void AppendUnavailable(List<SystemCenterKpiView> buffer)
        {
            string note = "PersonalChronicle.UI.MasterControl.Kpi.Archive.NoService".Translate().ToString();
            buffer.Add(SystemCenterKpiFactory.Pending("PersonalChronicle.UI.MasterControl.Kpi.Archive.Active", note));
            buffer.Add(SystemCenterKpiFactory.Pending("PersonalChronicle.UI.MasterControl.Kpi.Archive.Archived", note));
            buffer.Add(SystemCenterKpiFactory.Pending("PersonalChronicle.UI.MasterControl.Kpi.Archive.Colonists", note));
            buffer.Add(SystemCenterKpiFactory.Pending("PersonalChronicle.UI.MasterControl.Kpi.Archive.ServiceDays", note));
        }
    }
}

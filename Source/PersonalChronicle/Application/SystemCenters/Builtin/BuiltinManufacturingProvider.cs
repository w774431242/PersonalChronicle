using System.Collections.Generic;
using PersonalChronicle.Api;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using Verse;

namespace PersonalChronicle.Application.SystemCenters.Builtin
{
    /// <summary>
    /// 制造系统中心数据源 —— ② 车间俯视 / ③ 分析 / ④ 运营 / ⑤ 事件 / ⑥+⑦ 数据与桥接 /
    /// 调度队列 / 命令条 / 外壳（Chrome）。
    ///
    /// <para>⚠ <b>2026-10-06（用户指令「kpi 也要移除。确保干净」）</b>：原「① 总控 KPI · 九卡」
    /// 已**整块移除** —— 采集口径（设备可用率 / 在产设备率 / 产出效率 / 车间人时 / 排班覆盖率 /
    /// 在岗人数 / 良品率 / 加成效力 / 平均心情）连同其辅助实现一并删除，本类不再产出任何 KPI：
    /// <see cref="AppendKpis"/> 为空实现，<see cref="KpiSectionTitleKey"/> / <see cref="KpiRowSizes"/>
    /// 返回空值（接口成员必须保留 —— <see cref="ISystemCenterDataProvider"/> 要求实现）。</para>
    ///
    /// <para><b>连带效果（有意）</b>：注册表卡片的「首项 KPI」摘要、② 面板 KPI 卡带、
    /// 顶栏 <c>#kpiline</c> 读数带三处**同时消失**（它们都以本类的 KPI 为唯一数据源）；
    /// 经济中心与人员中心的 KPI 走各自的 Provider，**不受影响**。</para>
    ///
    /// <para>本 Provider <b>只读</b>：不写存档、不改设备状态、不产生事件。</para>
    /// </summary>
    public sealed partial class BuiltinManufacturingProvider : ISystemCenterDataProvider,
        IManufacturingAnalysisDataProvider, IManufacturingOperationsDataProvider,
        IManufacturingEventsDataProvider, IManufacturingDataLayerDataProvider,
        IManufacturingStockQueueDataProvider,
        IManufacturingCommandBarDataProvider,
        IManufacturingBridgeDataProvider,
        IManufacturingChromeDataProvider
    {
        /// <summary>
        /// 产出 / 良品统计窗口（天）= 一个 quadrum；与考勤台账 <c>WorkshopAttendance.RetainDays</c>（15）对齐。
        /// </summary>
        /// <remarks>
        /// ⚠ 2026-10-06：本常量原先只服务 KPI 九卡，但 <see cref="CollectProducedStats"/>（现仍被
        /// <c>.Analysis.cs</c> 的良品率统计使用）依赖同一窗口 ⇒ <b>不得随 KPI 一并删除</b>。
        /// </remarks>
        private static readonly int StatsWindowDays = GenDate.DaysPerQuadrum;

        private readonly IArchiveQueryService queries;

        public BuiltinManufacturingProvider(IArchiveQueryService queries)
        {
            this.queries = queries;
        }

        public string CenterDefName
        {
            get { return SystemCenterIds.Manufacturing; }
        }

        /// <summary>
        /// ⚠ 2026-10-06（用户指令）：制造中心 KPI 已移除 ⇒ 本键返回空串（UI 会回落通用标题，
        /// 但制造中心已无 KPI 带可承载它）。接口成员保留以维持 <see cref="ISystemCenterDataProvider"/> 契约。
        /// </summary>
        public string KpiSectionTitleKey
        {
            get { return string.Empty; }
        }

        /// <summary>
        /// ⚠ 2026-10-06（用户指令）：制造中心已无 KPI ⇒ 无分组可言（契约：null/空 = 不分组）。
        /// </summary>
        public IReadOnlyList<int> KpiRowSizes
        {
            get { return null; }
        }

        /// <summary>
        /// 制造中心详情页<b>只放业务视图</b>：清单类章节（职责边界 / 事件契约 / 依赖与能力）
        /// 属「注册凭据」，已由注册表卡片与悬停提示承载，不再混进操作界面。
        /// </summary>
        public bool ShowsCenterManifest
        {
            get { return false; }
        }

        /// <summary>
        /// ⚠ 2026-10-06（用户指令「kpi 也要移除」）：**空实现** —— 制造中心不再产出任何 KPI 卡。
        ///
        /// <para>连带效果（有意，勿"修复"）：<c>SystemCenterReadModelProvider</c> 的
        /// 注册表卡片首项 KPI、<c>AppShell</c> 的 ② 面板 KPI 卡带、<c>Chrome</c> 的顶栏
        /// <c>#kpiline</c> 读数带三处都读本方法的产出，故一并消失 —— 那是本指令的预期结果。</para>
        /// <para>本方法保留空实现而非删除，是因为 <see cref="ISystemCenterDataProvider"/> 强制要求；
        /// 若将来要恢复 KPI，按 git 历史取回九卡实现即可（本文件的旧版本含完整口径与异常降级）。</para>
        /// </summary>
        public void AppendKpis(List<SystemCenterKpiView> buffer, Map map)
        {
            // 有意为空：制造中心不产出 KPI（见上方注释与变更标注 CHG-20261006-KPI-REMOVE）。
        }

        /// <summary>
        /// 产出账本一次遍历，取三个读数（**同源**，避免多处遍历与口径漂移）：
        /// 近一个 quadrum 的产出件数 / 累计有品质产出 / 其中「良好及以上」件数。
        /// 数据源 = 职业 ledger 的 <c>ItemProduced</c>（已持久化），档位门槛复用
        /// <see cref="ExamScoring.QualityRank"/>，不另立门槛表。
        /// </summary>
        /// <remarks>
        /// ⚠ 2026-10-06：KPI 移除后本方法**仍在使用** —— <c>.Analysis.cs</c> 的良品率统计经
        /// <see cref="CollectProducedQuality"/> 调用它；<c>windowProduced</c> 出参保留（同一遍历的
        /// 附带读数，删除会破坏"一次遍历"契约）。
        /// </remarks>
        private void CollectProducedStats(out int windowProduced, out int qualityTotal, out int qualityGood)
        {
            windowProduced = 0;
            qualityTotal = 0;
            qualityGood = 0;
            if (queries == null)
            {
                return;
            }

            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            long windowStart = now - (long)StatsWindowDays * GenDate.TicksPerDay;

            IReadOnlyList<ArchiveObject> objects;
            try
            {
                objects = queries.GetObjectsOfCategory(ArchiveCategoryKeys.Pawn);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Provider,
                    "manufacturing quality tally failed: " + ex.Message);
                return;
            }
            if (objects == null)
            {
                return;
            }

            for (int i = 0; i < objects.Count; i++)
            {
                CareerData career = (objects[i] as PawnObject)?.CareerData;
                if (career == null || career.Events == null)
                {
                    continue;
                }
                for (int e = 0; e < career.Events.Count; e++)
                {
                    CareerEvent ev = career.Events[e];
                    if (ev == null || ev.EventType != CareerEventType.ItemProduced)
                    {
                        continue;
                    }
                    if (ev.Tick >= windowStart)
                    {
                        windowProduced++;
                    }
                    int rank = ExamScoring.QualityRank(ev.Quality);
                    if (rank < 0)
                    {
                        // 无品质产物（食材/原料/零件）：不进良品率分母，否则会系统性拉低可评品质的成品率。
                        continue;
                    }
                    qualityTotal++;
                    if (rank >= (int)QualityCategory.Good)
                    {
                        qualityGood++;
                    }
                }
            }
        }

        /// <summary>
        /// 良品率数据（兼容包装：<c>.Analysis.cs</c> 仍按二元输出的旧签名调用）。
        /// 与产出统计同源（<see cref="CollectProducedStats"/> 一次遍历）。
        /// </summary>
        private void CollectProducedQuality(out int total, out int good)
        {
            int windowProduced;
            CollectProducedStats(out windowProduced, out total, out good);
        }

        /// <summary>
        /// **无参** Keyed 文案（本 partial 类共用：DataLayer / CommandBar / Chrome / Bridge /
        /// StockQueue / Events / Operations 等 partial 都调它 —— 删除会连带编译失败）。
        /// </summary>
        private static string T(string key)
        {
            return key.Translate().ToString();
        }

        /// <summary>
        /// **两参（均为整数）** 格式化翻译（本 partial 类共用：CommandBar / Chrome / DataLayer 等
        /// partial 都调它；<c>.Analysis.cs</c> 另有 <c>T2(string, string, int)</c> 重载，两者并存不冲突）。
        /// </summary>
        /// <remarks>
        /// ⚠ 2026-10-06（KPI 移除时踩到）：本方法原先紧邻 KPI 采集段，一度被误认为"只服务 KPI"而删除
        /// ⇒ CommandBar / Chrome / DataLayer 三处立即编译失败。**它是 partial 类共用工具**，恢复 KPI 或
        /// 再次精简本文件时**不得**再删。
        /// </remarks>
        private static string T2(string key, int arg0, int arg1)
        {
            return key.Translate(arg0, arg1).ToString();
        }
    }
}

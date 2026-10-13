using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Application.SystemCenters.Builtin
{
    /// <summary>
    /// 殖民地经济中心（<c>SystemCenterIds.Economy</c>）的数据源 —— **重建期最小骨架**（`CHG-LCE-001`）。
    ///
    /// <para><b>背景</b>：2026-09-20 用户裁决 —— 原 MVS 市场体系（市场模拟 / 派系画像 / 供需偏置 /
    /// 定价通道）**整体删除**；本中心重建为「本地殖民地经济」= 产能 → 库存 → 财富的观测与记账层
    /// （回归 `经济体系/README.md` 立项定位与红线：账本纯观测，绝不反写 MarketValue）。</para>
    ///
    /// <para><b>本骨架提供</b>：KPI 三格（总财富 / 白银 / 库存市值 —— 全部 ① 原版真值）+
    /// 整页建设中说明（<see cref="IColonyEconomyDataProvider"/>）。三块业务视图
    /// （① 库存与市值 · ② 产消账本含可维持天数 · ③ 劳动力与产出）在后续批次落地，
    /// 数据底座 = 既有实测捕获层（`ItemFlowLedger` / `WorkTimeAccumulator` / `ProductionAccumulator`，
    /// 全部在 `PersonalChronicle.Domain`，不随本批改动）。</para>
    /// </summary>
    /// <summary>
    /// ⚠ 2026-09-20（阶段 3 落地批 ④）：改 `partial` —— 库存 K 线 / 热力图 的整页构建
    /// 拆到 `BuiltinColonyEconomyProvider.StockGallery.cs`（代码轻量化；主文件只留 KPI 面）。
    /// </summary>
    public sealed partial class BuiltinColonyEconomyProvider
        : ISystemCenterDataProvider
        , ISystemCenterKpiVisibility
        , IColonyEconomyDataProvider
    {
        private const string KpiWealth = "PersonalChronicle.Economy.Kpi.Wealth";
        private const string KpiWealthNote = "PersonalChronicle.Economy.Kpi.Wealth.Note";
        private const string KpiSilver = "PersonalChronicle.Economy.Kpi.Silver";
        private const string KpiSilverNote = "PersonalChronicle.Economy.Kpi.Silver.Note";
        private const string KpiStockValue = "PersonalChronicle.Economy.Kpi.StockValue";
        private const string KpiStockValueNote = "PersonalChronicle.Economy.Kpi.StockValue.Note";

        public string CenterDefName => SystemCenterIds.Economy;

        /// <summary>KPI 区块标题：不单独声明，回落通用标题「KPI 指标」。</summary>
        public string KpiSectionTitleKey => null;

        public IReadOnlyList<int> KpiRowSizes => null;

        /// <summary>详情页重心在业务页（骨架页），清单章节不展示（与注册表卡片同规）。</summary>
        public bool ShowsCenterManifest => false;

        /// <summary>KPI 采集能力保留 —— 「有没有 KPI Provider」仍是接入状态判据。</summary>
        public bool ShowsKpis => true;

        public void AppendKpis(List<SystemCenterKpiView> buffer, Map map)
        {
            if (buffer == null)
            {
                return;
            }
            // 失败隔离（与 Builtin*Provider 族同规）：本 Provider 的异常不得冒泡到组合层
            // —— 组合层的 catch 会把**整个详情页**置空（一家抛错全页陪葬，09-19/09-20 审计同族缺陷）。
            // 范式 = 记录起点 → 异常时回滚本方法产出 → 追加 Pending 占位（诚实降级，不伪造数值）。
            int start = buffer.Count;
            try
            {
                AppendKpisCore(buffer, map);
            }
            catch
            {
                if (buffer.Count > start)
                {
                    buffer.RemoveRange(start, buffer.Count - start);
                }
                buffer.Add(SystemCenterKpiFactory.Pending(KpiWealth));
                buffer.Add(SystemCenterKpiFactory.Pending(KpiSilver));
                buffer.Add(SystemCenterKpiFactory.Pending(KpiStockValue));
            }
        }

        private void AppendKpisCore(List<SystemCenterKpiView> buffer, Map map)
        {
            if (map == null)
            {
                // 无地图：仅 KPI 标题占位，值一律 "—"（LAND-009：未验证读数不臆造）。
                buffer.Add(SystemCenterKpiFactory.Pending(KpiWealth));
                buffer.Add(SystemCenterKpiFactory.Pending(KpiSilver));
                buffer.Add(SystemCenterKpiFactory.Pending(KpiStockValue));
                return;
            }

            // KPI 1 · 总财富（① 原版真值：WealthWatcher.WealthTotal）。
            float wealth = map.wealthWatcher != null ? map.wealthWatcher.WealthTotal : 0f;
            buffer.Add(WithNote(
                SystemCenterKpiFactory.Silver(KpiWealth, wealth, SystemCenterTone.Flat),
                T(KpiWealthNote)));

            ResourceCounter counter = map.resourceCounter;

            // KPI 2 · 白银（① 原版真值：全图器物计数）。
            int silver = counter != null ? counter.GetCount(ThingDefOf.Silver) : 0;
            buffer.Add(WithNote(
                SystemCenterKpiFactory.Silver(KpiSilver, silver, SystemCenterTone.Flat),
                T(KpiSilverNote)));

            // KPI 3 · 库存市值（① 原版真值：仓储计数 × 原版单价 BaseMarketValue）。
            // ⚠ 只数 ResourceCounter 记账的物品（`CountAsResource` 才有键）——与既有口径一致：
            //   0 = 仓储空 / 未计数物不参与，两态都如实。
            float stockValue = 0f;
            if (counter != null && counter.AllCountedAmounts != null)
            {
                foreach (KeyValuePair<ThingDef, int> pair in counter.AllCountedAmounts)
                {
                    if (pair.Key == null)
                    {
                        continue;
                    }
                    stockValue += pair.Key.BaseMarketValue * pair.Value;
                }
            }
            buffer.Add(WithNote(
                SystemCenterKpiFactory.Silver(KpiStockValue, stockValue, SystemCenterTone.Flat),
                T(KpiStockValueNote)));
        }

        public ColonyEconomySnapshot BuildColonyEconomy(Map map)
        {
            try
            {
                ColonyEconomySnapshot snapshot = new ColonyEconomySnapshot();
                snapshot.TitleText = T("PersonalChronicle.Economy.Page.Title");
                snapshot.BodyLines.Add(T("PersonalChronicle.Economy.Page.Rebuilding1"));
                snapshot.BodyLines.Add(T("PersonalChronicle.Economy.Page.Rebuilding2"));
                snapshot.BodyLines.Add(T("PersonalChronicle.Economy.Page.Rebuilding3"));
                snapshot.BuiltFromRevision = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
                return snapshot;
            }
            catch
            {
                // 失败隔离（族同规）：读取异常 ⇒ 退化为「仅有标题的骨架页」，不冒泡到组合层。
                return new ColonyEconomySnapshot { TitleText = T("PersonalChronicle.Economy.Page.Title") };
            }
        }

        /// <summary>键 → 文本（缺键时 Translate 返回键本身，便于定位缺失译文）。</summary>
        private static string T(string key)
        {
            return key.Translate().ToString();
        }

        /// <summary>
        /// 给工厂产物补附注（构建期拼好 tooltip，绘制路径零分配）。
        /// 工厂的 `Text/Silver` 不接收附注参数，故在此统一补齐 —— 与
        /// <c>SystemCenterKpiFactory.WithTooltip</c> 同规（该方法是 private，不跨类打开）。
        /// </summary>
        private static SystemCenterKpiView WithNote(SystemCenterKpiView view, string note)
        {
            view.Note = note ?? string.Empty;
            view.TooltipText = string.IsNullOrEmpty(view.Note)
                ? view.Label
                : view.Label + " — " + view.Note;
            return view;
        }
    }
}

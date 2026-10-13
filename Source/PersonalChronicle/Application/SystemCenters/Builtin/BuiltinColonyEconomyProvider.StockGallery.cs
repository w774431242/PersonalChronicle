using System;
using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Application.SystemCenters;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Application.SystemCenters.Builtin
{
    /// <summary>
    /// Partial of <see cref="BuiltinColonyEconomyProvider"/> —— **库存 K 线 + 热力图**整页数据源
    /// （`SDD-LCE-003` §4，阶段 3 落地批 ④）。
    ///
    /// <para><b>数据口径（全部真值/实测，零估算）</b>：</para>
    /// <list type="bullet">
    /// <item>数量 / 价值 / 类目 = ① 原版真值：<c>ResourceCounter</c> × <c>ThingDef.BaseMarketValue</c>
    /// × <c>FirstThingCategory</c>。</item>
    /// <item>蜡烛 / 窗口涨跌幅 = ② 本模组实测：<c>ChronicleGameComponent.StockSeries</c>。</item>
    /// </list>
    ///
    /// <para><b>失败隔离（族同规，P0 风险 R2）</b>：整方法 try/catch ⇒ 空快照 + 日志，
    /// **不冒泡**到组合层（否则整页陪葬，见 <c>BuiltinColonyEconomyProvider.BuildColonyEconomy</c> 注释）。</para>
    ///
    /// <para><b>周期基准（GOV-006）</b>：档位标签、跨度、边界一律取自 <see cref="EconomyPeriodTier"/>；
    /// 本文件不出现任何周期字面量。</para>
    /// </summary>
    public sealed partial class BuiltinColonyEconomyProvider
    {
        public ColonyStockGallerySnapshot BuildStockGallery(Map map, ColonyStockViewRequest request)
        {
            ColonyStockGallerySnapshot snapshot = new ColonyStockGallerySnapshot();
            try
            {
                // 请求的 TierIndex = **显示档位索引**（UI 档位条口径）；槽位映射唯一入口 = TierAt。
                int tier = EconomyPeriodTier.TierAt(request.TierIndex);
                snapshot.SelectedTier = EconomyPeriodTier.DisplayIndexOf(tier);
                for (int d = 0; d < EconomyPeriodTier.DisplayCount; d++)
                {
                    snapshot.TierLabels.Add(T(EconomyPeriodTier.KeyOf(EconomyPeriodTier.TierAt(d))));
                }

                StockSeriesStore store = StockSeriesOrNull();
                if (map == null || map.resourceCounter == null || map.resourceCounter.AllCountedAmounts == null)
                {
                    snapshot.IsEmpty = true;
                    return snapshot;
                }

                // ---- 品种目录（左列表 + 树图格子同源；按库存价值降序）----
                foreach (KeyValuePair<ThingDef, int> pair in map.resourceCounter.AllCountedAmounts)
                {
                    if (pair.Key == null || string.IsNullOrEmpty(pair.Key.defName))
                    {
                        continue;
                    }
                    // ⚠ 0 值过滤（2026-09-21 审计：`AllCountedAmounts` 含大量历史 0 值 def ——
                    // 实机表现为「左列表 386 项全 0 + 热力图整页空白」）。策略：
                    //   数量 > 0 ⇒ 纳入；
                    //   数量 = 0 但**已有实测序列** ⇒ 仍纳入（清零事件要看得见，与"未追踪不建序列"同规）。
                    StockSeries series = store != null ? store.Get(pair.Key.defName) : null;
                    bool tracked = series != null && series.HasAnyData;
                    if (pair.Value <= 0 && !tracked)
                    {
                        continue;
                    }

                    ColonyStockItemRow row = new ColonyStockItemRow();
                    row.DefName = pair.Key.defName;
                    row.Label = pair.Key.LabelCap.ToString();
                    ThingCategoryDef category = pair.Key.FirstThingCategory;
                    row.CategoryLabel = category != null ? category.LabelCap.ToString() : string.Empty;
                    row.Quantity = pair.Value;
                    row.Value = pair.Key.BaseMarketValue * pair.Value;

                    float pct = 0f;
                    row.HasDelta = series != null && series.TryDeltaPct(tier, out pct);
                    row.DeltaPct = row.HasDelta ? pct : 0f;
                    snapshot.Items.Add(row);
                }
                snapshot.Items.Sort(CompareByValueDesc);

                // ---- 选中品种（请求缺失 ⇒ 取价值最高者）----
                string selected = request.DefName;
                if (!string.IsNullOrEmpty(selected))
                {
                    bool found = false;
                    for (int i = 0; i < snapshot.Items.Count; i++)
                    {
                        if (string.Equals(snapshot.Items[i].DefName, selected, StringComparison.Ordinal))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        selected = null;
                    }
                }
                if (string.IsNullOrEmpty(selected) && snapshot.Items.Count > 0)
                {
                    selected = snapshot.Items[0].DefName;
                }
                snapshot.SelectedDef = selected ?? string.Empty;

                // ---- 蜡烛（选中品种 × 选中档）----
                StockSeries chosen = store != null && !string.IsNullOrEmpty(selected) ? store.Get(selected) : null;
                if (chosen != null)
                {
                    int count = chosen.CountOf(tier);
                    for (int i = 0; i < count; i++)
                    {
                        StockCandle candle = chosen.CandleAt(tier, i);
                        if (candle == null)
                        {
                            continue;
                        }
                        ColonyStockCandle view = new ColonyStockCandle();
                        view.Open = candle.Open;
                        view.High = candle.High;
                        view.Low = candle.Low;
                        view.Close = candle.Close;
                        view.Samples = candle.Samples;
                        view.Up = candle.Close >= candle.Open;
                        view.Flat = candle.IsFlat;
                        snapshot.Candles.Add(view);
                    }
                }

                snapshot.HasAnyData = store != null && store.HasAnyData;
                snapshot.IsEmpty = snapshot.Items.Count == 0;
                snapshot.BuiltFromRevision = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
                return snapshot;
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Provider, "stock gallery snapshot failed: " + ex);
                snapshot.IsEmpty = true;
                snapshot.HasError = true;   // 与"真无数据"区分（GATE-UI P2）
                return snapshot;
            }
        }

        /// <summary>取库存序列集合（无 Game / 无组件 ⇒ null；消费方按空态处理）。</summary>
        private static StockSeriesStore StockSeriesOrNull()
        {
            if (Current.Game == null)
            {
                return null;
            }
            ChronicleGameComponent component = Current.Game.GetComponent<ChronicleGameComponent>();
            return component != null ? component.StockSeries : null;
        }

        private static int CompareByValueDesc(ColonyStockItemRow a, ColonyStockItemRow b)
        {
            if (a == null && b == null)
            {
                return 0;
            }
            if (a == null)
            {
                return 1;
            }
            if (b == null)
            {
                return -1;
            }
            int byValue = b.Value.CompareTo(a.Value);
            return byValue != 0 ? byValue : string.CompareOrdinal(a.DefName, b.DefName);
        }
    }
}

using PersonalChronicle.Archive;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// Partial of <see cref="PersonnelArchivePanel"/> ——人物档案总览子页的**「产出 · 工时」双轴图**
    /// （预览 <c>#v-pawn</c> 总览子页 capChart，HTML 1438-1467：柱 = 产出（银/季 · 左轴）·
    /// 线 = 工时（h/季 · 右轴）—— 两者量纲不同，各自归一后叠画在同一 plot 即双轴语义）。
    ///
    /// <para><b>数据源</b>：<see cref="ProductionSummaryView.QuadrumMarketValue"/> /
    /// <see cref="ProductionSummaryView.QuadrumWorkHours"/>（8 季滚动桶，快照构建期已对齐当前季；
    /// 旧档无逐季口径 ⇒ 全 0，如实画空态，绝不填样例数据 —— LAND-009）。</para>
    ///
    /// <para><b>登记差异</b>：预览 plot 240px、带日/周粒度段控；实机 8 季桶只有季粒度 ⇒
    /// plot 110px + 无段控 + 口径行说明（登记进对照清单，非缺陷）。</para>
    /// </summary>
    public sealed partial class PersonnelArchivePanel
    {
        private float DrawLedgerTrendChart(Rect rect, float y)
        {
            float headH = 26f;
            UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, headH),
                "PersonalChronicle.UI.Chart.OutputWork.Title");

            float padX = UITheme.CardPadX;
            float bodyY = y + headH + UITheme.SpaceXxs;
            float innerW = Mathf.Max(60f, rect.width - padX * 2f);

            float[] bars = cachedProductionSummary != null ? cachedProductionSummary.QuadrumMarketValue : null;
            float[] line = cachedProductionSummary != null ? cachedProductionSummary.QuadrumWorkHours : null;
            bool hasBars = bars != null && System.Array.Exists(bars, v => v > 0.01f);
            bool hasLine = line != null && System.Array.Exists(line, v => v > 0.0001f);

            // 摘要行（预览 HTML 1442-1447）：产出合计（accent）· 工时合计（info）· 人均效率（银/h）。
            float totalValue = cachedProductionSummary != null ? cachedProductionSummary.TotalMarketValue : 0f;
            float totalHours = (float)(GetCachedTotalWorkTicks() / (double)GenDate.TicksPerHour);
            string efficiency = totalHours > 0f
                ? "PersonalChronicle.UI.Chart.OutputWork.Efficiency".Translate(
                    (NamedArgument)(totalValue / totalHours).ToString("0.0")).ToString()
                : string.Empty;
            Rect summary = new Rect(rect.x + padX, bodyY, innerW, 22f);
            float sx = summary.x;
            sx = DrawChartSummaryValue(summary, sx,
                FormatSilver(totalValue) + " "
                    + "PersonalChronicle.UI.Ledger.OutputValueUnit".Translate().ToString(),
                UITheme.Accent);
            sx = DrawChartSummaryValue(summary, sx,
                totalHours.ToString("0.0") + " "
                    + "PersonalChronicle.UI.Ledger.TotalWorkUnit".Translate().ToString(),
                UITheme.Info);
            sx = DrawChartSummaryValue(summary, sx, efficiency, UITheme.Muted);

            const float plotH = 110f;
            Rect plot = new Rect(rect.x + padX, summary.yMax + 4f, innerW, plotH);
            if (hasBars)
            {
                // 柱 = 产出（独立归一 ⇒ 左轴语义）。labels = null：逐柱季名在半宽栏会挤压成
                // T6 降级，季口径由下方口径行统一说明（LAND-004 窄档收敛）。
                RuifBridge.TryBarChartV(plot, bars, null);
                if (hasLine)
                {
                    // 线 = 工时（独立归一 ⇒ 右轴语义）；叠画在同一 plot = 预览的双轴并置。
                    RuifBridge.TryLineChart(plot, line, true, false);
                }
            }
            else
            {
                // 空态（LAND-009）：旧档 / 新档无逐季口径 ⇒ 画空槽 + 口径说明。
                Widgets.DrawBoxSolid(plot, UITheme.Window);
                UIComponents.Border(plot, UITheme.BorderSoft);
                UIComponents.Label(plot, "—", GameFont.Small, UITheme.Dim, TextAnchor.MiddleCenter);
            }

            Rect caption = new Rect(rect.x + padX, plot.yMax + 2f, innerW, 18f);
            UIComponents.Label(caption,
                "PersonalChronicle.UI.Chart.OutputWork.Caption".Translate().ToString(),
                GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);
            return caption.yMax;
        }

        /// <summary>摘要行单值（Small·语义色，前间距 12）；返回下一个 x。</summary>
        private static float DrawChartSummaryValue(Rect row, float x, string text, Color color)
        {
            if (string.IsNullOrEmpty(text) || x >= row.xMax)
            {
                return x;
            }
            Text.Font = GameFont.Small;
            float w = Text.CalcSize(text).x;
            UIComponents.Label(new Rect(x, row.y, Mathf.Min(w + 4f, row.xMax - x), row.height),
                text, GameFont.Small, color, TextAnchor.MiddleLeft);
            return x + w + 12f;
        }
    }
}

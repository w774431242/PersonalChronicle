using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// 制造中心 ③ 分析带（对齐预览 §③ 四个面板：工序瓶颈带 / OEE 六大损失 / 产能吞吐趋势 / 在制品 WIP）。
    ///
    /// <para>响应式：按可用宽自动 4 → 2 → 1 列（LAND-005），面板高固定、内容按像素容纳（LAND-007）。</para>
    /// </summary>
    internal static class ManufacturingAnalysisPanels
    {
        internal const float PanelHeight = 158f;
        private const float PanelGap = 8f;
        private const float MinPanelWidth = 250f;

        /// <summary>
        /// 本组面板数（③ 分析带 = 瓶颈 / OEE / 趋势 / WIP 四块）。
        /// **单一真源**：底栏页签「（n）」引用本常量（CHG-20261006-TABUNIFY 3c）。
        /// </summary>
        internal const int PanelCount = 4;
        private const float HeaderHeight = 42f;
        private const float RowHeight = 16f;

        internal static int ColumnsFor(float width)
        {
            int fits = Mathf.FloorToInt((width + PanelGap) / (MinPanelWidth + PanelGap));
            return Mathf.Clamp(fits, 1, PanelCount);
        }

        /// <summary>整块高度（列数 × 行高）—— 与 <see cref="Draw"/> 共用同一套常量。</summary>
        internal static float BlockHeight(float width)
        {
            int rows = Mathf.CeilToInt(PanelCount / (float)ColumnsFor(width));
            return rows * PanelHeight + (rows - 1) * PanelGap;
        }

        /// <summary>绘制分析带；返回新的 y。</summary>
        internal static float Draw(Rect rect, float y, ManufacturingAnalysisSnapshot snapshot)
        {
            if (snapshot == null || rect.width <= 1f)
            {
                return y;
            }
            int columns = ColumnsFor(rect.width);
            float panelWidth = Mathf.Max(200f, (rect.width - PanelGap * (columns - 1)) / columns);

            for (int i = 0; i < PanelCount; i++)
            {
                int row = i / columns;
                int col = i % columns;
                Rect panel = new Rect(
                    rect.x + col * (panelWidth + PanelGap),
                    y + row * (PanelHeight + PanelGap),
                    panelWidth, PanelHeight);
                switch (i)
                {
                    case 0:
                        DrawBottleneckPanel(panel, snapshot);
                        break;
                    case 1:
                        DrawLossPanel(panel, snapshot);
                        break;
                    case 2:
                        DrawTrendPanel(panel, snapshot);
                        break;
                    default:
                        DrawWipPanel(panel, snapshot);
                        break;
                }
            }
            return y + BlockHeight(rect.width);
        }

        // ---- 四个面板 ----

        private static void DrawBottleneckPanel(Rect panel, ManufacturingAnalysisSnapshot snapshot)
        {
            Rect body = DrawPanelChrome(panel,
                "PersonalChronicle.UI.Analysis.Bottleneck.Title",
                "PersonalChronicle.UI.Analysis.Bottleneck.Stencil",
                // 修复（2026-09-12）：此处原传**键名**，而 `DrawPanelChrome` 的第三个参数约定是
                // **已翻译文本**（其他 8 处调用点均传 `T(...)`/快照文案）⇒ 面板副标题直接显示键名。
                T("PersonalChronicle.UI.Analysis.Bottleneck.Sub"));

            List<AnalysisBottleneckView> segments = snapshot.Bottlenecks;
            if (segments == null || segments.Count == 0)
            {
                DrawMuted(body, T("PersonalChronicle.UI.Analysis.Bottleneck.Empty"));
                return;
            }

            Rect track = new Rect(body.x, body.y + 6f, body.width, 24f);
            float x = track.x;
            for (int i = 0; i < segments.Count; i++)
            {
                AnalysisBottleneckView segment = segments[i];
                float width = Mathf.Max(14f, track.width * Mathf.Clamp01(segment.Ratio01));
                if (x + width > track.xMax)
                {
                    width = track.xMax - x;
                }
                if (width <= 1f)
                {
                    break;
                }
                Rect cell = new Rect(x, track.y, width, track.height);
                Color fill = segment.IsBottleneck ? UITheme.Dead : UITheme.WithAlpha(UITheme.Info, 0.35f);
                Widgets.DrawBoxSolid(cell, fill);
                UIComponents.Border(cell, UITheme.WithAlpha(UITheme.Window, 0.6f));
                if (width > 34f)
                {
                    Label(new Rect(cell.x + 2f, cell.y, cell.width - 4f, cell.height), segment.ValueText,
                        GameFont.Tiny, UITheme.Text, TextAnchor.MiddleCenter);
                }
                TooltipHandler.TipRegion(cell, segment.Name + " · " + segment.ValueText);
                x += width;
            }

            Rect tip = new Rect(body.x, track.yMax + 4f, body.width, RowHeight);
            Label(tip, string.IsNullOrEmpty(snapshot.BottleneckSummary) ? "—" : snapshot.BottleneckSummary,
                GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleLeft);
        }

        private static void DrawLossPanel(Rect panel, ManufacturingAnalysisSnapshot snapshot)
        {
            Rect body = DrawPanelChrome(panel,
                "PersonalChronicle.UI.Analysis.Loss.Title",
                "PersonalChronicle.UI.Analysis.Loss.Stencil",
                Translate2("PersonalChronicle.UI.Analysis.Loss.Sub", snapshot.LossUnavailableCount));

            List<AnalysisLossView> rows = snapshot.Losses;
            if (rows == null)
            {
                return;
            }
            int capacity = Mathf.Max(0, Mathf.FloorToInt(body.height / RowHeight));
            int count = Mathf.Min(capacity, rows.Count);
            for (int i = 0; i < count; i++)
            {
                AnalysisLossView row = rows[i];
                Rect line = new Rect(body.x, body.y + i * RowHeight, body.width, RowHeight - 2f);
                float labelWidth = Mathf.Min(74f, line.width * 0.42f);
                Label(new Rect(line.x, line.y, labelWidth, line.height), row.Label,
                    GameFont.Tiny, row.Available ? UITheme.Text : UITheme.Dim, TextAnchor.MiddleLeft);

                float valueWidth = 34f;
                Rect bar = new Rect(line.x + labelWidth + 3f, line.y + (line.height - 6f) * 0.5f,
                    Mathf.Max(10f, line.width - labelWidth - valueWidth - 8f), 6f);
                Widgets.DrawBoxSolid(bar, UITheme.BorderHair);
                if (row.Available)
                {
                    Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(row.Ratio01), bar.height),
                        ToneColor(row.Tone));
                }
                Label(new Rect(bar.xMax + 3f, line.y, valueWidth, line.height),
                    row.Available ? row.ValueText : T("PersonalChronicle.UI.Analysis.Unavailable"),
                    GameFont.Tiny, row.Available ? UITheme.Text : UITheme.Dim, TextAnchor.MiddleRight);
            }
        }

        private static void DrawTrendPanel(Rect panel, ManufacturingAnalysisSnapshot snapshot)
        {
            Rect body = DrawPanelChrome(panel,
                "PersonalChronicle.UI.Analysis.Trend.Title",
                "PersonalChronicle.UI.Analysis.Trend.Stencil",
                string.IsNullOrEmpty(snapshot.TrendNowText)
                    ? T("PersonalChronicle.UI.Analysis.Trend.Empty")
                    : snapshot.TrendNowText);

            List<float> points = snapshot.Trend;
            if (!snapshot.TrendAvailable || points == null || points.Count < 2)
            {
                DrawMuted(body, T("PersonalChronicle.UI.Analysis.Trend.Empty"));
                return;
            }

            Rect chart = new Rect(body.x, body.y + 6f, body.width, Mathf.Max(24f, body.height - 18f));
            Widgets.DrawBoxSolid(chart, UITheme.WithAlpha(UITheme.Window, 0.5f));
            UIComponents.Border(chart, UITheme.BorderHair);

            // 面积：按 2px 列填充（IMGUI 无多边形，竖条拼出面积最省）
            Color area = UITheme.WithAlpha(UITheme.Accent, 0.16f);
            Color line = UITheme.Accent;
            float step = 2f;
            Vector2? previous = null;
            for (float px = chart.x; px <= chart.xMax; px += step)
            {
                float t = Mathf.Clamp01((px - chart.x) / Mathf.Max(1f, chart.width));
                float value = SampleTrend(points, t);
                float top = chart.yMax - chart.height * value;
                Widgets.DrawBoxSolid(new Rect(px, top, step, chart.yMax - top), area);
                Vector2 current = new Vector2(px, top);
                if (previous.HasValue)
                {
                    Widgets.DrawLine(previous.Value, current, line, 2f);
                }
                previous = current;
            }
        }

        private static void DrawWipPanel(Rect panel, ManufacturingAnalysisSnapshot snapshot)
        {
            Rect body = DrawPanelChrome(panel,
                "PersonalChronicle.UI.Analysis.Wip.Title",
                "PersonalChronicle.UI.Analysis.Wip.Stencil",
                null);

            List<AnalysisWipView> cells = snapshot.Wip;
            if (cells == null || cells.Count == 0)
            {
                return;
            }
            const int columns = 2;
            float gap = 6f;
            float cellWidth = Mathf.Max(60f, (body.width - gap) / columns);
            float cellHeight = 34f;
            for (int i = 0; i < cells.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                Rect cell = new Rect(body.x + col * (cellWidth + gap), body.y + 6f + row * (cellHeight + gap),
                    cellWidth, cellHeight);
                UIComponents.Card(cell, cells[i].Available ? UITheme.Info : UITheme.Dim);
                Label(new Rect(cell.x + 6f, cell.y + 2f, cell.width - 12f, 18f),
                    cells[i].Available ? cells[i].Value : T("PersonalChronicle.UI.Analysis.Unavailable"),
                    GameFont.Small, cells[i].Available ? UITheme.Text : UITheme.Dim, TextAnchor.MiddleLeft);
                Label(new Rect(cell.x + 6f, cell.y + 18f, cell.width - 12f, 14f),
                    cells[i].Label, GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            }
        }

        // ---- 面板外壳与小工具 ----

        private static Rect DrawPanelChrome(Rect panel, string titleKey, string stencilKey, string sub)
        {
            UIComponents.Card(panel, UITheme.BorderSoft);
            float pad = 8f;
            float width = panel.width - pad * 2f;
            Label(new Rect(panel.x + pad, panel.y + 3f, width, 18f), T(titleKey),
                GameFont.Small, UITheme.Accent, TextAnchor.MiddleLeft);
            Label(new Rect(panel.x + pad, panel.y + 20f, width, 13f), T(stencilKey),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(sub))
            {
                Label(new Rect(panel.x + pad, panel.y + 32f, width, 13f), sub,
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            }
            return new Rect(panel.x + pad, panel.y + HeaderHeight, width,
                Mathf.Max(10f, panel.height - HeaderHeight - pad));
        }

        private static void DrawMuted(Rect body, string text)
        {
            Label(new Rect(body.x, body.y + 4f, body.width, RowHeight), text,
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
        }

        private static void Label(Rect rect, string text, GameFont font, Color color, TextAnchor anchor)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            UIComponents.Label(rect, UIComponents.TruncateToWidth(text, rect.width, font), font, color, anchor);
        }

        private static float SampleTrend(List<float> points, float t)
        {
            float scaled = t * (points.Count - 1);
            int index = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, points.Count - 2);
            float frac = scaled - index;
            return Mathf.Clamp01(Mathf.Lerp(points[index], points[index + 1], frac));
        }

        private static Color ToneColor(SystemCenterTone tone)
        {
            switch (tone)
            {
                case SystemCenterTone.Good: return UITheme.Alive;
                case SystemCenterTone.Warn: return UITheme.Threat;
                case SystemCenterTone.Bad: return UITheme.Dead;
                default: return UITheme.Info;
            }
        }

        private static string T(string key)
        {
            return key.Translate().ToString();
        }

        private static string Translate2(string key, int arg)
        {
            return key.Translate(arg).ToString();
        }
    }
}

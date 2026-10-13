using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// 制造中心 ⑥ 数据桥（对齐预览 §⑥ 的 6 面板：<c>s4 s4 s4</c> + <c>s6 s3 s3</c>）。
    ///
    /// <para>按 PM 决策**混合**：3 块真接（生产链路看板 / 能源拓扑 / 质量 Pareto）+ 3 块照实占位
    /// （甘特 / 数字孪生 / 维护计划）。布局保持预览的两行权重（4:4:4 与 6:3:3），
    /// 窄屏整体降为单列（LAND-005）。</para>
    /// </summary>
    internal static class ManufacturingBridgePanels
    {
        private const float PanelGap = 8f;
        private const float HeaderHeight = 42f;
        private const float RowHeight = 16f;
        private const float PanelHeight = 150f;
        private const float TwoRowWidth = 900f;

        /// <summary>
        /// 本组面板数（⑥ 数据桥 = 3 真接 + 3 占位，共 6 块）。
        /// **单一真源**：底栏页签「（n）」引用本常量；块高亦由本常量派生（CHG-20261006-TABUNIFY 3c）。
        /// ⚠ 边界：这是**声明值**；运行期实际绘制按 <c>snapshot.Panels.Count</c> 动态（可少于 6）。
        /// </summary>
        internal const int PanelCount = 6;

        internal static float BlockHeight(float width)
        {
            if (width < TwoRowWidth)
            {
                // 窄屏单列：PanelCount 块纵向堆叠（原为魔数 `PanelHeight * 6f + PanelGap * 5f`）。
                return PanelHeight * PanelCount + PanelGap * (PanelCount - 1);
            }
            return PanelHeight * 2f + PanelGap;
        }

        internal static float Draw(Rect rect, float y, ManufacturingBridgeSnapshot snapshot)
        {
            List<BridgePanelView> panels = snapshot?.Panels;
            if (panels == null || panels.Count == 0 || rect.width <= 1f)
            {
                return y;
            }

            if (rect.width < TwoRowWidth)
            {
                float cursor = y;
                for (int i = 0; i < panels.Count; i++)
                {
                    DrawPanel(new Rect(rect.x, cursor, rect.width, PanelHeight), panels[i]);
                    cursor += PanelHeight + PanelGap;
                }
                return cursor;
            }

            // 第一行：权重 4:4:4 → 三等分
            float third = (rect.width - PanelGap * 2f) / 3f;
            for (int i = 0; i < 3 && i < panels.Count; i++)
            {
                DrawPanel(new Rect(rect.x + i * (third + PanelGap), y, third, PanelHeight), panels[i]);
            }
            // 第二行：权重 6:3:3
            float second = y + PanelHeight + PanelGap;
            float half = (rect.width - PanelGap * 2f) * 0.5f;
            float quarter = (rect.width - PanelGap * 2f) * 0.25f;
            if (panels.Count > 3)
            {
                DrawPanel(new Rect(rect.x, second, half, PanelHeight), panels[3]);
            }
            if (panels.Count > 4)
            {
                DrawPanel(new Rect(rect.x + half + PanelGap, second, quarter, PanelHeight), panels[4]);
            }
            if (panels.Count > 5)
            {
                DrawPanel(new Rect(rect.x + half + quarter + PanelGap * 2f, second, quarter, PanelHeight), panels[5]);
            }
            return second + PanelHeight;
        }

        private static void DrawPanel(Rect panel, BridgePanelView view)
        {
            if (view == null)
            {
                return;
            }
            Rect body = DrawChrome(panel, view);
            if (view.Pending)
            {
                Rect badge = new Rect(body.x, body.y + 6f, Mathf.Min(170f, body.width), 24f);
                UIComponents.Card(badge, UITheme.Dim);
                UIComponents.Label(badge, T("PersonalChronicle.UI.Bridge.Pending"),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleCenter);
                TooltipHandler.TipRegion(badge, view.PendingNote);
                if (!string.IsNullOrEmpty(view.PendingNote))
                {
                    Label(new Rect(body.x, badge.yMax + 4f, body.width, 16f), view.PendingNote,
                        GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                }
                return;
            }

            float y = body.y;
            List<string> headers = view.Headers;
            if (headers != null && headers.Count > 0)
            {
                float columnWidth = body.width / headers.Count;
                for (int i = 0; i < headers.Count; i++)
                {
                    Label(new Rect(body.x + i * columnWidth, y, columnWidth - 2f, 15f), headers[i],
                        GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleLeft);
                }
                y += 16f;
                Widgets.DrawBoxSolid(new Rect(body.x, y - 1f, body.width, 1f), UITheme.BorderSoft);
            }

            List<BridgeRowView> rows = view.Rows;
            if (rows == null || rows.Count == 0)
            {
                Label(new Rect(body.x, y + 3f, body.width, 16f),
                    T("PersonalChronicle.UI.Bridge.Empty"), GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                return;
            }
            int capacity = Mathf.Max(0, Mathf.FloorToInt((body.yMax - y) / RowHeight));
            int count = Mathf.Min(capacity, rows.Count);
            int columns = headers != null && headers.Count > 0 ? headers.Count : 3;
            float cellWidth = body.width / columns;
            for (int r = 0; r < count; r++)
            {
                BridgeRowView row = rows[r];
                Rect line = new Rect(body.x, y + r * RowHeight, body.width, RowHeight - 2f);
                for (int c = 0; c < columns && c < row.Cells.Count; c++)
                {
                    Label(new Rect(line.x + c * cellWidth, line.y, cellWidth - 2f, line.height), row.Cells[c],
                        GameFont.Tiny, c == 0 ? UITheme.Text : UITheme.Muted,
                        c == columns - 1 && columns > 1 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
                }
                if (row.Bar01 >= 0f)
                {
                    Rect bar = new Rect(line.x, line.yMax - 1f, line.width * Mathf.Clamp01(row.Bar01), 2f);
                    Widgets.DrawBoxSolid(bar, ToneColor(row.Tone));
                }
            }
        }

        private static Rect DrawChrome(Rect panel, BridgePanelView view)
        {
            UIComponents.Card(panel, view.Pending ? UITheme.Dim : UITheme.BorderSoft);
            float pad = 8f;
            float width = panel.width - pad * 2f;
            Label(new Rect(panel.x + pad, panel.y + 3f, width, 18f), view.Title,
                GameFont.Small, view.Pending ? UITheme.Muted : UITheme.Accent, TextAnchor.MiddleLeft);
            Label(new Rect(panel.x + pad, panel.y + 20f, width, 13f), view.Stencil,
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(view.Sub))
            {
                Label(new Rect(panel.x + pad, panel.y + 31f, width, 13f), view.Sub,
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            }
            return new Rect(panel.x + pad, panel.y + HeaderHeight, width,
                Mathf.Max(10f, panel.height - HeaderHeight - pad));
        }

        private static void Label(Rect rect, string text, GameFont font, Color color, TextAnchor anchor)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            UIComponents.Label(rect, UIComponents.TruncateToWidth(text, rect.width, font), font, color, anchor);
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
    }
}

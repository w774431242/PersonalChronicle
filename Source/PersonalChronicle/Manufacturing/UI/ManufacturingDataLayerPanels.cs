using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// 制造中心 ⑦ 数据层契约（对齐预览 §⑦：<c>s4</c> Def 注册表 + <c>s8</c> Runtime 快照）。
    ///
    /// <para>内容 = **本 mod 真正依赖的原版契约**：QualityCategory 七档枚举顺序、ThingDef/RecipeDef 节选、
    /// 运行时 Thing（stableId / CompPowerTrader / CompBreakdownable / CompQuality / bills）、PowerNet 汇总。
    /// 全部读真实数据，UI 只排版。</para>
    /// </summary>
    internal static class ManufacturingDataLayerPanels
    {
        private const float PanelGap = 8f;
        private const float HeaderHeight = 42f;
        private const float RowHeight = 16f;
        private const float PanelHeight = 300f;
        private const float TwoColumnWidth = 760f;

        /// <summary>
        /// 本组面板数（⑦ 数据层契约 = Def 注册表 + Runtime 快照两块）。
        /// **单一真源**：底栏页签「（n）」引用本常量；块高亦由本常量派生（CHG-20261006-TABUNIFY 3c）。
        /// </summary>
        internal const int PanelCount = 2;

        internal static float BlockHeight(float width)
        {
            return width >= TwoColumnWidth ? PanelHeight : PanelHeight * PanelCount + PanelGap;
        }

        internal static float Draw(Rect rect, float y, ManufacturingDataLayerSnapshot snapshot)
        {
            if (snapshot == null || rect.width <= 1f)
            {
                return y;
            }
            if (rect.width >= TwoColumnWidth)
            {
                float leftWidth = Mathf.Max(240f, rect.width * 0.3f);
                DrawDefsPanel(new Rect(rect.x, y, leftWidth, PanelHeight), snapshot);
                DrawThingsPanel(new Rect(rect.x + leftWidth + PanelGap, y,
                    rect.width - leftWidth - PanelGap, PanelHeight), snapshot);
                return y + PanelHeight;
            }
            DrawDefsPanel(new Rect(rect.x, y, rect.width, PanelHeight), snapshot);
            float second = y + PanelHeight + PanelGap;
            DrawThingsPanel(new Rect(rect.x, second, rect.width, PanelHeight), snapshot);
            return second + PanelHeight;
        }

        /// <summary>Def 注册表：品质枚举 chips + ThingDef 节选 + RecipeDef 节选。</summary>
        private static void DrawDefsPanel(Rect panel, ManufacturingDataLayerSnapshot snapshot)
        {
            Rect body = DrawPanelChrome(panel,
                "PersonalChronicle.UI.DataLayer.Defs.Title",
                "PersonalChronicle.UI.DataLayer.Defs.Stencil",
                snapshot.DefsSub);

            float y = body.y;
            Label(new Rect(body.x, y, body.width, 15f),
                T("PersonalChronicle.UI.DataLayer.Enum.Header"), GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleLeft);
            y += 16f;

            // 品质七档 chips（两行内放得下 7 个短 chip）
            float chipX = body.x;
            for (int i = 0; i < snapshot.QualityChips.Count; i++)
            {
                float width = Mathf.Min(64f, body.width * 0.32f);
                if (chipX + width > body.xMax)
                {
                    chipX = body.x;
                    y += 16f;
                }
                UIComponents.Badge(new Rect(chipX, y, width - 3f, 14f), snapshot.QualityChips[i], UITheme.Info);
                chipX += width;
            }
            y += 20f;

            y = DrawDefRows(body, y, "PersonalChronicle.UI.DataLayer.ThingDef.Header", snapshot.ThingDefs);
            y = DrawDefRows(body, y, "PersonalChronicle.UI.DataLayer.RecipeDef.Header", snapshot.Recipes);
        }

        private static float DrawDefRows(Rect body, float y, string headerKey, List<DefRowView> rows)
        {
            Label(new Rect(body.x, y, body.width, 15f), T(headerKey),
                GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleLeft);
            y += 16f;
            if (rows == null || rows.Count == 0)
            {
                return y;
            }
            int capacity = Mathf.Max(0, Mathf.FloorToInt((body.yMax - y) / RowHeight));
            int count = Mathf.Min(capacity, rows.Count);
            for (int i = 0; i < count; i++)
            {
                DefRowView row = rows[i];
                Rect line = new Rect(body.x, y + i * RowHeight, body.width, RowHeight - 2f);
                Label(new Rect(line.x, line.y, line.width * 0.36f, line.height), row.Name,
                    GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
                Label(new Rect(line.x + line.width * 0.36f, line.y, line.width * 0.34f, line.height), row.Detail,
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                Label(new Rect(line.x + line.width * 0.70f, line.y, line.width * 0.30f, line.height),
                    row.Meta + " · " + row.Extra, GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleRight);
                TooltipHandler.TipRegion(line, row.Name + "\n" + row.Detail + "\n" + row.Meta + " · " + row.Extra);
            }
            return y + count * RowHeight + 6f;
        }

        /// <summary>Runtime 快照：表头 + 对象行 + PowerNet 汇总。</summary>
        private static void DrawThingsPanel(Rect panel, ManufacturingDataLayerSnapshot snapshot)
        {
            Rect body = DrawPanelChrome(panel,
                "PersonalChronicle.UI.DataLayer.Things.Title",
                "PersonalChronicle.UI.DataLayer.Things.Stencil",
                snapshot.ThingsSub);

            List<string> header = snapshot.ThingsHeader;
            if (header == null || header.Count == 0)
            {
                return;
            }
            float columnWidth = body.width / header.Count;
            float y = body.y;
            for (int i = 0; i < header.Count; i++)
            {
                Label(new Rect(body.x + i * columnWidth, y, columnWidth - 2f, 15f), header[i],
                    GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleLeft);
            }
            y += 16f;
            Widgets.DrawBoxSolid(new Rect(body.x, y - 1f, body.width, 1f), UITheme.BorderSoft);

            int capacity = Mathf.Max(0, Mathf.FloorToInt((body.yMax - y - 54f) / RowHeight));
            int count = Mathf.Min(capacity, snapshot.Things.Count);
            for (int r = 0; r < count; r++)
            {
                ThingRowView row = snapshot.Things[r];
                Rect line = new Rect(body.x, y + r * RowHeight, body.width, RowHeight - 2f);
                if (Mouse.IsOver(line))
                {
                    Widgets.DrawHighlight(line);
                }
                for (int c = 0; c < header.Count && c < row.Cells.Count; c++)
                {
                    Label(new Rect(body.x + c * columnWidth, line.y, columnWidth - 2f, line.height),
                        row.Cells[c], GameFont.Tiny,
                        c == 0 ? UITheme.Text : UITheme.Muted, TextAnchor.MiddleLeft);
                }
            }
            y += count * RowHeight + 6f;

            Label(new Rect(body.x, y, body.width, 15f),
                T("PersonalChronicle.UI.DataLayer.PowerNet.Header"), GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleLeft);
            y += 16f;
            for (int i = 0; i < snapshot.PowerNets.Count; i++)
            {
                Label(new Rect(body.x, y + i * 15f, body.width, 14f), snapshot.PowerNets[i],
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            }
        }

        // ---- 外壳与小工具 ----

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
                Label(new Rect(panel.x + pad, panel.y + 31f, width, 13f), sub,
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

        private static string T(string key)
        {
            return key.Translate().ToString();
        }
    }
}

using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// 制造中心 ② 的 `#rightcol`：**生产队列**（`WORK ORDER QUEUE`）+ **原料仓**（`RAW STOCK`）。
    ///
    /// <para>对齐预览：`#rightcol{display:flex;flex-direction:column;gap:14px}` —— 两块**纵向堆叠**、
    /// 位于平面图下方、通栏宽；且**没有额外的区块标题**（它们属于 ②）。</para>
    /// </summary>
    internal static class ManufacturingStockQueuePanels
    {
        private const float PanelGap = 8f;
        private const float HeaderHeight = 50f;

        /// <summary>
        /// 本组面板数（② 调度 = 生产队列 + 原料仓两块，对应预览 `#rightcol` 的两个子块）。
        /// **单一真源**：底栏页签「（n）」引用本常量（CHG-20261006-TABUNIFY 3c）。
        /// </summary>
        internal const int PanelCount = 2;
        private const float RowHeight = 20f;
        private const float QueueHeight = 158f;
        private const float RawHeight = 158f;

        internal static float BlockHeight(float width)
        {
            return QueueHeight + PanelGap + RawHeight;
        }

        internal static float Draw(Rect rect, float y, ManufacturingStockQueueSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsEmpty || rect.width <= 1f)
            {
                return y;
            }
            DrawQueuePanel(new Rect(rect.x, y, rect.width, QueueHeight), snapshot);
            float second = y + QueueHeight + PanelGap;
            DrawRawPanel(new Rect(rect.x, second, rect.width, RawHeight), snapshot);
            return second + RawHeight;
        }

        /// <summary>生产队列（设备 · 配方 · 剩余次数 · 暂停）。</summary>
        private static void DrawQueuePanel(Rect panel, ManufacturingStockQueueSnapshot snapshot)
        {
            Rect body = DrawPanelChrome(panel,
                "PersonalChronicle.UI.Queue.Title",
                "PersonalChronicle.UI.Queue.Stencil",
                snapshot.QueueSub);

            List<WorkOrderRowView> rows = snapshot.Queue;
            if (rows == null || rows.Count == 0)
            {
                Label(new Rect(body.x, body.y + 4f, body.width, 16f),
                    T("PersonalChronicle.UI.Queue.Empty"), GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                return;
            }
            int capacity = Mathf.Max(0, Mathf.FloorToInt(body.height / RowHeight));
            int count = Mathf.Min(capacity, rows.Count);
            for (int i = 0; i < count; i++)
            {
                WorkOrderRowView row = rows[i];
                Rect line = new Rect(body.x, body.y + i * RowHeight, body.width, RowHeight - 2f);
                float machineWidth = Mathf.Min(150f, line.width * 0.3f);
                float amountWidth = 62f;
                Label(new Rect(line.x, line.y, machineWidth, line.height), row.Machine,
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                Label(new Rect(line.x + machineWidth, line.y,
                        Mathf.Max(30f, line.width - machineWidth - amountWidth - 6f), line.height), row.Recipe,
                    GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
                Label(new Rect(line.xMax - amountWidth, line.y, amountWidth, line.height),
                    string.IsNullOrEmpty(row.StateText) ? row.AmountText : row.StateText,
                    GameFont.Tiny, row.Suspended ? UITheme.Threat : UITheme.SecondaryText, TextAnchor.MiddleRight);
                TooltipHandler.TipRegion(line, row.Machine + "\n" + row.Recipe + " · " + row.AmountText
                    + (row.Suspended ? "\n" + row.StateText : string.Empty));
            }
        }

        /// <summary>原料仓（名称 · 相对水位条 · 库存 · LOW）。</summary>
        private static void DrawRawPanel(Rect panel, ManufacturingStockQueueSnapshot snapshot)
        {
            Rect body = DrawPanelChrome(panel,
                "PersonalChronicle.UI.Raw.Title",
                "PersonalChronicle.UI.Raw.Stencil",
                snapshot.RawSub);

            List<RawStockRowView> rows = snapshot.RawStock;
            if (rows == null || rows.Count == 0)
            {
                Label(new Rect(body.x, body.y + 4f, body.width, 16f),
                    T("PersonalChronicle.UI.Raw.Empty"), GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                return;
            }
            int capacity = Mathf.Max(0, Mathf.FloorToInt(body.height / RowHeight));
            int count = Mathf.Min(capacity, rows.Count);
            for (int i = 0; i < count; i++)
            {
                RawStockRowView row = rows[i];
                Rect line = new Rect(body.x, body.y + i * RowHeight, body.width, RowHeight - 2f);
                float nameWidth = Mathf.Min(120f, line.width * 0.26f);
                float countWidth = 64f;
                float lowWidth = row.Low ? 34f : 0f;
                Label(new Rect(line.x, line.y, nameWidth, line.height), row.Name,
                    GameFont.Tiny, row.Low ? UITheme.Dead : UITheme.Text, TextAnchor.MiddleLeft);

                Rect bar = new Rect(line.x + nameWidth + 4f, line.y + (line.height - 7f) * 0.5f,
                    Mathf.Max(20f, line.width - nameWidth - countWidth - lowWidth - 12f), 7f);
                Widgets.DrawBoxSolid(bar, UITheme.BorderHair);
                Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(row.Level01), bar.height),
                    row.Low ? UITheme.Dead : UITheme.Info);
                // 30% 安全线刻度（预览 .ss{left:30%}）
                Widgets.DrawBoxSolid(new Rect(bar.x + bar.width * 0.30f, bar.y - 1f, 1f, bar.height + 2f),
                    UITheme.Accent);

                Label(new Rect(bar.xMax + 4f, line.y, countWidth, line.height), row.CountText,
                    GameFont.Tiny, UITheme.SecondaryText, TextAnchor.MiddleRight);
                if (row.Low)
                {
                    Label(new Rect(line.xMax - lowWidth, line.y, lowWidth, line.height),
                        T("PersonalChronicle.UI.Raw.Low"), GameFont.Tiny, UITheme.Dead, TextAnchor.MiddleRight);
                }
                TooltipHandler.TipRegion(line, row.Name + " · " + row.CountText);
            }
        }

        // ---- 外壳与小工具 ----

        private static Rect DrawPanelChrome(Rect panel, string titleKey, string stencilKey, string sub)
        {
            UIComponents.Card(panel, UITheme.BorderSoft);
            float pad = 8f;
            float width = panel.width - pad * 2f;
            Label(new Rect(panel.x + pad, panel.y + 3f, width, 22f), T(titleKey),
                GameFont.Small, UITheme.Accent, TextAnchor.MiddleLeft);
            Label(new Rect(panel.x + pad, panel.y + 24f, width, 13f), T(stencilKey),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            if (!string.IsNullOrEmpty(sub))
            {
                Label(new Rect(panel.x + pad, panel.y + 34f, width, 13f), sub,
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

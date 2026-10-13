using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// 制造中心 ④ 运营（对齐预览 §④ 的三列：在岗殖民者 / 成品库 / Andon 异常聚合）。
    ///
    /// <para>响应式：可用宽足够时 3 列并排，否则逐级降为 1 列（LAND-005）。三块**等高**，
    /// 高度按"行数最多的那块"算，避免参差。</para>
    ///
    /// <para>与预览的两处有意差异（均已在落地记录登记）：</para>
    /// <list type="bullet">
    /// <item>工人行把"前三技能 chips"收进悬停提示 —— 346px 宽内放不下 chips + 热力格 + 任务三者，
    /// 而热力格已用颜色表达了全部 12 项技能（chips 只是它的文字版）；</item>
    /// <item>成品库第三列由预览的"入库时间"改为**堆叠数 ×N** —— 原版没有逐件产出时间戳，不编造。</item>
    /// </list>
    /// </summary>
    internal static class ManufacturingOperationsPanels
    {
        private const float PanelGap = 8f;
        private const float MinPanelWidth = 260f;

        /// <summary>
        /// 本组面板数（④ 运营 = 在岗 / 成品库 / Andon 三块）。
        /// **单一真源**：底栏页签「（n）」引用本常量（CHG-20261006-TABUNIFY 3c）。
        /// </summary>
        internal const int PanelCount = 3;
        private const float HeaderHeight = 42f;
        private const float WorkerRowHeight = 32f;
        private const float ItemRowHeight = 18f;
        private const float HeatCellSize = 12f;
        private const float HeatCellGap = 1f;

        internal static int ColumnsFor(float width)
        {
            int fits = Mathf.FloorToInt((width + PanelGap) / (MinPanelWidth + PanelGap));
            return Mathf.Clamp(fits, 1, PanelCount);
        }

        /// <summary>三块共用的面板高（按行数最多的块算）。</summary>
        private static float PanelHeightFor(ManufacturingOperationsSnapshot snapshot)
        {
            int workerRows = snapshot?.Workers != null ? snapshot.Workers.Count : 0;
            int depotRows = snapshot?.DepotItems != null ? snapshot.DepotItems.Count : 0;
            int rows = Mathf.Max(workerRows, depotRows);
            return Mathf.Clamp(HeaderHeight + rows * WorkerRowHeight + 10f, 150f, 340f);
        }

        /// <summary>整块高度 —— 与 <see cref="Draw"/> 同式。</summary>
        internal static float BlockHeight(float width, ManufacturingOperationsSnapshot snapshot)
        {
            int columns = ColumnsFor(width);
            float panelHeight = PanelHeightFor(snapshot);
            int rows = Mathf.CeilToInt(PanelCount / (float)columns);
            return rows * panelHeight + (rows - 1) * PanelGap;
        }

        internal static float Draw(Rect rect, float y, ManufacturingOperationsSnapshot snapshot)
        {
            if (snapshot == null || rect.width <= 1f)
            {
                return y;
            }
            int columns = ColumnsFor(rect.width);
            float panelHeight = PanelHeightFor(snapshot);
            float panelWidth = Mathf.Max(200f, (rect.width - PanelGap * (columns - 1)) / columns);

            for (int i = 0; i < PanelCount; i++)
            {
                int row = i / columns;
                int col = i % columns;
                Rect panel = new Rect(
                    rect.x + col * (panelWidth + PanelGap),
                    y + row * (panelHeight + PanelGap),
                    panelWidth, panelHeight);
                switch (i)
                {
                    case 0:
                        DrawCrewPanel(panel, snapshot);
                        break;
                    case 1:
                        DrawDepotPanel(panel, snapshot);
                        break;
                    default:
                        DrawAndonPanel(panel);
                        break;
                }
            }
            return y + BlockHeight(rect.width, snapshot);
        }

        /// <summary>面板一：在岗殖民者（心情点 + 姓名智力 + 技能热力 + 心情 + 任务）。</summary>
        private static void DrawCrewPanel(Rect panel, ManufacturingOperationsSnapshot snapshot)
        {
            Rect body = DrawPanelChrome(panel,
                "PersonalChronicle.UI.Ops.Crew.Title",
                "PersonalChronicle.UI.Ops.Crew.Stencil",
                T("PersonalChronicle.UI.Ops.Crew.Sub"));

            List<OpsWorkerView> workers = snapshot.Workers;
            if (workers == null || workers.Count == 0)
            {
                Muted(body, T("PersonalChronicle.UI.Ops.Crew.Empty"));
                return;
            }

            int capacity = Mathf.Max(0, Mathf.FloorToInt((body.height + PanelGap) / WorkerRowHeight));
            int count = Mathf.Min(capacity, workers.Count);
            for (int i = 0; i < count; i++)
            {
                OpsWorkerView worker = workers[i];
                Rect row = new Rect(body.x, body.y + i * WorkerRowHeight, body.width, WorkerRowHeight - 3f);

                // 心情点
                float dot = 9f;
                DrawDisc(new Vector2(row.x + dot * 0.5f, row.y + 7f), dot * 0.5f, ToneColor(worker.MoodTone));

                // 姓名 + 智力
                float taskWidth = Mathf.Min(140f, row.width * 0.42f);
                float nameWidth = Mathf.Max(40f, row.width - taskWidth - 6f);
                Label(new Rect(row.x + dot + 4f, row.y, nameWidth * 0.62f, 15f), worker.Name,
                    GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
                Label(new Rect(row.x + dot + 4f + nameWidth * 0.62f, row.y, nameWidth * 0.38f, 15f),
                    worker.IntelText, GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                Label(new Rect(row.xMax - taskWidth, row.y, taskWidth, 15f), worker.TaskText,
                    GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleRight);

                // 技能热力格（≤12 格；等级 → 三档颜色）
                float heatX = row.x + dot + 4f;
                float heatY = row.y + 16f;
                List<OpsSkillCell> heat = worker.Heat;
                if (heat != null)
                {
                    for (int c = 0; c < heat.Count; c++)
                    {
                        Rect cell = new Rect(heatX + c * (HeatCellSize + HeatCellGap), heatY,
                            HeatCellSize, 11f);
                        if (cell.xMax > row.xMax - 54f)
                        {
                            break;
                        }
                        Widgets.DrawBoxSolid(cell, SkillColor(heat[c].Level));
                        TooltipHandler.TipRegion(cell,
                            heat[c].Label + " " + heat[c].Level.ToString());
                    }
                }

                // 心情数值（右侧）
                Label(new Rect(row.xMax - 50f, heatY - 2f, 50f, 14f), worker.MoodText,
                    GameFont.Tiny, ToneColor(worker.MoodTone), TextAnchor.MiddleRight);

                TooltipHandler.TipRegion(row, worker.Tooltip);
            }
        }

        /// <summary>面板二：成品库（品质 chip + 名称 + 堆叠数）。</summary>
        private static void DrawDepotPanel(Rect panel, ManufacturingOperationsSnapshot snapshot)
        {
            Rect body = DrawPanelChrome(panel,
                "PersonalChronicle.UI.Ops.Depot.Title",
                "PersonalChronicle.UI.Ops.Depot.Stencil",
                snapshot.DepotCountText);

            if (snapshot.DepotEmpty)
            {
                Muted(body, T("PersonalChronicle.UI.Ops.Depot.Empty"));
                return;
            }

            List<OpsDepotItemView> items = snapshot.DepotItems;
            int capacity = Mathf.Max(0, Mathf.FloorToInt((body.height + PanelGap) / ItemRowHeight));
            int count = Mathf.Min(capacity, items.Count);
            for (int i = 0; i < count; i++)
            {
                OpsDepotItemView item = items[i];
                Rect row = new Rect(body.x, body.y + i * ItemRowHeight, body.width, ItemRowHeight - 2f);
                float chipWidth = 46f;
                UIComponents.Badge(new Rect(row.x, row.y + 1f, chipWidth - 4f, row.height - 2f),
                    item.QualityLabel, ToneColor(item.QualityTone));
                float countWidth = 34f;
                Label(new Rect(row.x + chipWidth, row.y, Mathf.Max(20f, row.width - chipWidth - countWidth), row.height),
                    item.Name, GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
                Label(new Rect(row.xMax - countWidth, row.y, countWidth, row.height),
                    item.CountText, GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleRight);
            }
        }

        /// <summary>面板三：Andon 异常聚合 —— 预览里也是占位面板，照实画「待接入」。</summary>
        private static void DrawAndonPanel(Rect panel)
        {
            Rect body = DrawPanelChrome(panel,
                "PersonalChronicle.UI.Ops.Andon.Title",
                "PersonalChronicle.UI.Ops.Andon.Stencil",
                null);
            Rect badge = new Rect(body.x, body.y + 8f, Mathf.Min(160f, body.width), 24f);
            UIComponents.Card(badge, UITheme.Dim);
            Label(badge, T("PersonalChronicle.UI.Ops.Andon.Pending"),
                GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleCenter);
            TooltipHandler.TipRegion(badge, T("PersonalChronicle.UI.Ops.Andon.Tip"));
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

        private static void Muted(Rect body, string text)
        {
            Label(new Rect(body.x, body.y + 4f, body.width, 16f), text,
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

        private static void DrawDisc(Vector2 center, float radius, Color color)
        {
            // 用 4px 方块近似小圆点（9px 直径用方块肉眼无差，省一张贴图查询）
            Widgets.DrawBoxSolid(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), color);
        }

        /// <summary>技能热力配色（预览：≥12 主强调 / ≥7 健康 / 其余暗淡）。</summary>
        private static Color SkillColor(int level)
        {
            if (level >= 12)
            {
                return UITheme.Accent;
            }
            return level >= 7 ? UITheme.Alive : UITheme.WithAlpha(UITheme.Dim, 0.5f);
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

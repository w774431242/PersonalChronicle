using System.Collections.Generic;
using PersonalChronicle.Archive.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// [DEV] 数据覆盖自检报告窗口。只读展示 <see cref="DevCoverageProbe.CoverageReport"/>，
    /// 表格列出每个数据维度的「已填/总 · 覆盖率 · 状态」。不修改任何数据。
    /// </summary>
    public class Dialog_DevCoverageReport : Window
    {
        private readonly DevCoverageProbe.CoverageReport report;
        private Vector2 scroll = Vector2.zero;

        public Dialog_DevCoverageReport(DevCoverageProbe.CoverageReport report)
        {
            this.report = report;
            this.closeOnClickedOutside = true;
            this.doCloseX = true;
            this.forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(480f, 560f);

        public override void DoWindowContents(Rect inRect)
        {
            // IMGUI 状态配对还原（ArchivePanelBase 提供，避免 GUI 状态泄漏到下一帧）。
            var guiState = ArchivePanelBase.CaptureGuiState();

            // 标题
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 28f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "PersonalChronicle.UI.Dev.CoverageTitle".Translate().ToString());
            Text.Font = GameFont.Small;

            // 摘要行（全局活跃度）
            Rect summaryRect = new Rect(inRect.x, inRect.y + 30f, inRect.width, 20f);
            Color prevColor = GUI.color;
            GUI.color = UITheme.Dim;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(summaryRect,
                "PersonalChronicle.UI.Dev.Cov.Summary".Translate(
                    report.ArchivedTotal, report.HumanlikeTotal,
                    report.BattleObjectCount, report.ChronicleEventCount).ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = prevColor;

            // 列表区（标题 + 滚动）
            float headerY = inRect.y + 54f;
            float rowH = 22f;
            DrawHeader(new Rect(inRect.x, headerY, inRect.width, rowH));

            float listTop = headerY + rowH;
            float listH = inRect.height - (listTop - inRect.y) - 52f;
            Rect listRect = new Rect(inRect.x, listTop, inRect.width, Mathf.Max(0f, listH));
            float viewH = report.Dimensions.Count * rowH;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, viewH);
            Widgets.BeginScrollView(listRect, ref scroll, viewRect);
            float y = 0f;
            foreach (DevCoverageProbe.DimensionResult d in report.Dimensions)
            {
                DrawRow(new Rect(0f, y, viewRect.width, rowH), d);
                y += rowH;
            }
            Widgets.EndScrollView();

            Rect hintRect = new Rect(inRect.x, inRect.yMax - 50f, inRect.width, 22f);
            GUI.color = UITheme.Dim;
            Text.Font = GameFont.Tiny;
            Widgets.Label(hintRect, "PersonalChronicle.UI.Dev.Cov.Hint".Translate().ToString());
            Text.Font = GameFont.Small;
            GUI.color = prevColor;

            Rect closeRect = new Rect(inRect.x + inRect.width * 0.3f, inRect.yMax - 26f, inRect.width * 0.4f, 24f);
            if (Widgets.ButtonText(closeRect, "PersonalChronicle.UI.Dev.CoverageClose".Translate().ToString()))
            {
                Close();
            }

            ArchivePanelBase.RestoreGuiState(guiState);
        }

        private void DrawHeader(Rect rect)
        {
            float w = rect.width;
            float c1 = w * 0.44f;
            float c2 = w * 0.20f;
            float c3 = w * 0.16f;
            Color prev = GUI.color;
            GUI.color = UITheme.Muted;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, rect.y, c1, rect.height),
                "PersonalChronicle.UI.Dev.Cov.ColDim".Translate().ToString());
            Widgets.Label(new Rect(rect.x + c1, rect.y, c2, rect.height),
                "PersonalChronicle.UI.Dev.Cov.ColFill".Translate().ToString());
            Widgets.Label(new Rect(rect.x + c1 + c2, rect.y, c3, rect.height),
                "PersonalChronicle.UI.Dev.Cov.ColRate".Translate().ToString());
            Widgets.Label(new Rect(rect.x + c1 + c2 + c3, rect.y, w - c1 - c2 - c3, rect.height),
                "PersonalChronicle.UI.Dev.Cov.ColStatus".Translate().ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = prev;
        }

        private void DrawRow(Rect rect, DevCoverageProbe.DimensionResult d)
        {
            float w = rect.width;
            float c1 = w * 0.44f;
            float c2 = w * 0.20f;
            float c3 = w * 0.16f;

            Color statusColor = UITheme.Text;
            if (d.Filled <= 0)
            {
                statusColor = d.UnreachableViaDev ? UITheme.Dim : UITheme.Muted;
            }
            else if (d.Filled >= d.Total)
            {
                statusColor = UITheme.Alive;
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, rect.y, c1, rect.height), d.Label);
            Widgets.Label(new Rect(rect.x + c1, rect.y, c2, rect.height), d.Filled + "/" + d.Total);
            Widgets.Label(new Rect(rect.x + c1 + c2, rect.y, c3, rect.height), (d.Coverage * 100f).ToString("F0") + "%");
            Color prev = GUI.color;
            GUI.color = statusColor;
            Widgets.Label(new Rect(rect.x + c1 + c2 + c3, rect.y, w - c1 - c2 - c3, rect.height), d.Status);
            GUI.color = prev;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}

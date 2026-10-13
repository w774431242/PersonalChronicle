// [DEV] 独立自检报告弹窗（2026-08-30 建立）。
//
// 与 Dialog_DevCoverageReport 的关系：两者并列、互不复用。
//   · Dialog_DevCoverageReport —— 覆盖率报告（维度/已填/总/百分比）
//   · 本窗口                    —— 自检报告（断言项 PASS/FAIL + 失败定位线索）
//
// 铁律遵循：
//   · IMGUI 状态（Font/Anchor/GUI.color）经 ArchivePanelBase 捕获 + 帧末还原，绝不外泄
//   · 配色一律取 UITheme 令牌，窗口内不出现 new Color(...)
//   · 纯展示：不修改任何数据，关闭即结束
using PersonalChronicle.Archive.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    public class Dialog_DevSelfTestReport : Window
    {
        private readonly DevSelfTest.SelfTestReport report;
        private Vector2 scroll = Vector2.zero;

        // 两行式行高：断言名 22f + 详情 16f（中文不裁剪，见 UI 行高经验值）
        private const float RowNameH = 22f;
        private const float RowDetailH = 16f;

        public Dialog_DevSelfTestReport(DevSelfTest.SelfTestReport report)
        {
            this.report = report;
            this.closeOnClickedOutside = true;
            this.doCloseX = true;
            this.forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(560f, 520f);

        public override void DoWindowContents(Rect inRect)
        {
            // IMGUI 状态配对还原（避免字体/锚点/颜色泄漏到后续绘制）
            var guiState = ArchivePanelBase.CaptureGuiState();

            if (report == null)
            {
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 28f), "(no report)");
                ArchivePanelBase.RestoreGuiState(guiState);
                return;
            }

            DrawTitle(inRect);
            DrawSummary(inRect);

            // ── 列表区（滚动）──
            float listTop = inRect.y + 84f;
            float bottomBar = 30f;
            float listH = inRect.height - (listTop - inRect.y) - bottomBar;
            Rect listRect = new Rect(inRect.x, listTop, inRect.width, Mathf.Max(0f, listH));
            float rowH = RowNameH + RowDetailH;
            float viewH = report.Checks.Count * rowH;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, viewH);

            Widgets.BeginScrollView(listRect, ref scroll, viewRect);
            float y = 0f;
            foreach (DevSelfTest.CheckResult c in report.Checks)
            {
                DrawRow(new Rect(0f, y, viewRect.width, rowH), c);
                y += rowH;
            }
            Widgets.EndScrollView();

            DrawButtons(inRect);
            ArchivePanelBase.RestoreGuiState(guiState);
        }

        // ── 标题 ──
        private void DrawTitle(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 28f),
                "PersonalChronicle.UI.Dev.SelfTestTitle".Translate().ToString());
            Text.Font = GameFont.Small;
        }

        // ── 摘要行：通过/失败计数，失败>0 时整行转警示色 ──
        private void DrawSummary(Rect inRect)
        {
            Color prev = GUI.color;
            GUI.color = report.AllPassed ? UITheme.Dim : UITheme.StateColor(false, true);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x, inRect.y + 32f, inRect.width, 20f),
                "PersonalChronicle.UI.Dev.SelfTestPass".Translate(report.PassedCount).ToString()
                + "　·　"
                + "PersonalChronicle.UI.Dev.SelfTestFail".Translate(report.FailedCount).ToString()
                + (report.AllPassed ? "　✓" : ""));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = prev;

            // 副标题：说明本报告只读，便于后续开发判断可信度
            Color prev2 = GUI.color;
            GUI.color = UITheme.Muted;
            Widgets.Label(new Rect(inRect.x, inRect.y + 54f, inRect.width, 22f),
                "PersonalChronicle.UI.Dev.SelfTestHint".Translate().ToString());
            GUI.color = prev2;
        }

        // ── 单行：状态标记 + 断言名 + 详情 ──
        private void DrawRow(Rect rect, DevSelfTest.CheckResult c)
        {
            Color cPass = UITheme.StateColor(true, false);
            Color cFail = UITheme.StateColor(false, true);

            // 状态标记
            Color prev = GUI.color;
            GUI.color = c.Passed ? cPass : (c.Critical ? cFail : UITheme.Muted);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(rect.x, rect.y, 52f, RowNameH),
                c.Passed ? "PASS" : "FAIL");
            GUI.color = prev;

            // 断言名（通过=常规色，关键失败=警示色）
            Color prev2 = GUI.color;
            GUI.color = c.Passed ? UITheme.Text : (c.Critical ? cFail : UITheme.Muted);
            Widgets.Label(new Rect(rect.x + 56f, rect.y, rect.width - 56f, RowNameH),
                UIComponents.TruncateToWidth(c.Name, rect.width - 56f, GameFont.Small));
            GUI.color = prev2;

            // 详情行（缩进对齐，Dim 色退到背景）
            Color prev3 = GUI.color;
            GUI.color = UITheme.Dim;
            Widgets.Label(new Rect(rect.x + 56f, rect.y + RowNameH, rect.width - 56f, RowDetailH),
                UIComponents.TruncateToWidth(c.Detail, rect.width - 56f, GameFont.Tiny));
            GUI.color = prev3;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        // ── 底部按钮：关闭 / 重新输出到日志 ──
        private void DrawButtons(Rect inRect)
        {
            float btnW = inRect.width * 0.3f;
            float btnH = 24f;
            float y = inRect.yMax - btnH;

            Rect closeRect = new Rect(inRect.x + inRect.width * 0.18f, y, btnW, btnH);
            if (Widgets.ButtonText(closeRect, "PersonalChronicle.UI.Dev.SelfTestClose".Translate().ToString()))
            {
                Close();
            }

            Rect logRect = new Rect(inRect.x + inRect.width * 0.52f, y, btnW, btnH);
            if (Widgets.ButtonText(logRect, "PersonalChronicle.UI.Dev.SelfTestCopyLog".Translate().ToString()))
            {
                DevSelfTest.DumpReportToLog(report);
            }
        }
    }
}

using System.Collections.Generic;
using PersonalChronicle.Archive.UI;
using RUIF;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// **新手教程确认弹窗**（用户需求 2026-10-06：「单击变成弹窗，不要上一版内容，
    /// 该弹窗内容应该是询问是否开启新手教程，然后提示过程中按键可以退出」）。
    ///
    /// <para><b>职责只有一件事</b>：页头「新手教程」按钮 → 本弹窗（<c>Guide.Ask.*</c>：
    /// 询问语 + 「ESC / 跳过可退出」提示）→「开始引导」关闭本窗并启动
    /// <see cref="MasterControlTabWindow.StartTour"/> 的 spotlight 引导。</para>
    ///
    /// <para><b>旧版七节正文已整体移除</b>（<c>Guide.S1..S7.*</c> 键同步从 zh/en 删除）：
    /// 静态长文与引导层是两种形态，用户明确不要前者。若将来要恢复"手册"，应另开窗口，
    /// 不要把它塞回这里。</para>
    ///
    /// <para><b>基于 RUIF</b>：骨架 <see cref="WindowFrame.Draw(Rect,string,out Rect,out Rect)"/>、
    /// 度量 <see cref="Measure.RowHeight"/>、按钮 <see cref="ButtonAtoms.PrimaryButton"/>、
    /// 底栏槽几何 <see cref="WindowFmt.FooterButtonRect"/>；配色走本 Mod 的 <see cref="UITheme"/>
    /// （与全局主题联动）。</para>
    ///
    /// <para><b>零持久状态</b>：没有任何字段（连滚动都不需要 —— 正文 ≤ 2 行）。
    /// 文案全部键驱动，代码内无裸文本（GATE-UI / GATE-LOC）。</para>
    /// </summary>
    internal sealed class Window_NewcomerGuide : Window
    {
        public Window_NewcomerGuide()
        {
            // 确认窗是"问一句就走"的：X 常显、点窗外不关（防误触）、不暂停游戏。
            doCloseX = true;
            closeOnClickedOutside = false;
            forcePause = false;
            absorbInputAroundWindow = true;
        }

        /// <summary>尺寸：只放「标题 + 2 行正文 + 1 行提示 + 底栏」，不随屏幕放大（弹窗不该空荡）。</summary>
        public override Vector2 InitialSize
        {
            get
            {
                float w = Mathf.Min(520f, UI.screenWidth - 80f);
                float h = Mathf.Min(280f, UI.screenHeight - 120f);
                return new Vector2(Mathf.Max(380f, w), Mathf.Max(200f, h));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            // RUIF 窗口骨架：标题条 + 分隔线；交出内容区与底栏槽。
            WindowFrame.Draw(inRect, UIComponents.Loc("PersonalChronicle.UI.Guide.Ask.Title"),
                out Rect content, out Rect footer);

            float line = Measure.RowHeight(GameFont.Small);
            float tinyLine = Measure.RowHeight(GameFont.Tiny);

            // ⚠ 文本全局态按"保存 → 就地改 → finally 还原"（工程范式；不写回猜测的默认值）。
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            bool prevWrap = Text.WordWrap;
            Color prevColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;

                // 正文（≤ 2 行预算；内容由 Keyed 决定，超长会在窗口内自然折行）
                float bodyHeight = line * 3f;
                Widgets.Label(new Rect(content.x, content.y, content.width,
                    Mathf.Min(bodyHeight, Mathf.Max(line, content.height - tinyLine - 10f))),
                    UIComponents.Loc("PersonalChronicle.UI.Guide.Ask.Body"));

                // 退出提示（Tiny + Muted，紧贴正文下方）："过程中按 ESC / 点跳过可退出"
                float hintY = Mathf.Min(content.y + bodyHeight + 6f, content.yMax - tinyLine);
                GUI.color = UITheme.Muted;
                Widgets.Label(new Rect(content.x, hintY, content.width, tinyLine),
                    UIComponents.Loc("PersonalChronicle.UI.Guide.Ask.EscHint"));
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
                Text.WordWrap = prevWrap;
            }

            // 底栏双按钮（RUIF `n≥2` 等分槽；主按钮 = 最右槽）：以后再说 / 开始引导。
            // 本窗口下限 380 宽 ⇒ 槽宽 ≥ 180，不会落入 D4 垂直堆叠档。
            Rect laterRect = WindowFmt.FooterButtonRect(footer, 2, 0);
            if (ButtonAtoms.PrimaryButton(laterRect, UIComponents.Loc("PersonalChronicle.UI.Guide.Ask.Later")))
            {
                Close();
            }
            Rect startRect = WindowFmt.FooterButtonRect(footer, 2, 1);
            if (ButtonAtoms.PrimaryButton(startRect, UIComponents.Loc("PersonalChronicle.UI.Guide.Tour.Start")))
            {
                // 引导层是**独立全屏透明窗口**（Window_TourOverlay · TOUR2）⇒ 直接压栈即可：
                // 它自己 Begin()（激活锚点注册表），并且**不依赖总控中心是否打开**
                // （用户要"去游戏里划车间"时必须能关掉总控中心，引导仍要跟着走）。
                // 重入防护（守门 P2）：已经在引导中就直接关掉本弹窗，不叠第二层。
                if (!TourRunning())
                {
                    Window_TourOverlay overlay = new Window_TourOverlay();
                    overlay.Begin();
                    Find.WindowStack.Add(overlay);
                }
                Close();
            }
        }

        /// <summary>是否已有引导层在运行（重入防护：避免叠出两层引导；窗口栈极小，遍历即可）。</summary>
        private static bool TourRunning()
        {
            IList<Window> windows = Find.WindowStack.Windows;
            if (windows == null)
            {
                return false;
            }
            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i] is Window_TourOverlay)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

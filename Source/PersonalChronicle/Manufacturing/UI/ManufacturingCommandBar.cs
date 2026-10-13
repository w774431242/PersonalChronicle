using System.Collections.Generic;
using System.Text;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// 制造中心 ② 的 `#cmdbar`（对齐预览：`#route-bar` 产线汇总条 + `#hc-out` 视图体检行）。
    ///
    /// <para><b>LAND-001 第三次实测（D23）</b>：预览最终版把这两行**压成单行**：
    /// <c>.cmdbar{display:flex;align-items:center;gap:10px;padding:4px 13px;white-space:nowrap;overflow:hidden}</c>
    /// <c>.cmdbar .routebar,.cmdbar .hcout{padding:0;border:none;background:none;flex-wrap:nowrap;gap:6px;flex:none}</c>
    /// <c>.cmdbar .cmdsep{width:1px;height:14px;background:var(--line2)}</c>
    /// <c>.cmdbar .hcout{margin-left:auto;font-size:10px}</c>
    /// ⇒ <b>[产线汇总 …] │ [体检结论靠右]</b>，条高 ≈ 24（4+16+4）。</para>
    ///
    /// <para>预览的 <c>#stress-bar</c> 是**仿真控件**（压力注入 + 复位整个仿真），游戏内没有仿真态可注入
    /// ⇒ 有意不落地。</para>
    /// </summary>
    internal static class ManufacturingCommandBar
    {
        /// <summary>
        /// 单行命令条的行高。
        /// <para><b>实测依据（无头浏览器）</b>：预览 <c>#cmdbar</c> 渲染高 <b>36px</b>。
        /// 构成链：<c>.cmdbar{padding:4px 13px}</c>（行 596）→ 内含 <c>.routebar</c>，
        /// 其 padding 由 <c>#floorwrap .routebar{padding:4px 13px}</c>（行 565，<b>ID 优先级压过</b>
        /// <c>.cmdbar .routebar{padding:0}</c> 行 597）决定 → 内层按钮 <c>.cmdbar button{padding:2px 8px;font-size:10px}</c>
        /// → routebar 实高 27px；27 + 4×2 = <b>35~36px</b>。</para>
        /// <para>落地取 28：引擎无「内层容器再套 4px 内距」的双层结构，改为**单层等高**表达
        /// （28 = 按钮行 20 + 上下各 4），视觉与预览同高同密度。若要严格 36 可改 36。</para>
        /// </summary>
        internal const float RowHeight = 28f;
        /// <summary>左右内边距（预览 <c>.cmdbar{padding:4px 13px}</c>）。</summary>
        private const float PadX = 13f;
        /// <summary>上下内边距（预览 4px）。</summary>
        private const float PadY = 4f;
        /// <summary>元素间距（预览 <c>.cmdbar{gap:10px}</c>）。</summary>
        private const float Gap = 10f;
        /// <summary>分隔竖线尺寸（预览 <c>.cmdsep{width:1px;height:14px}</c>）。</summary>
        private const float SepWidth = 1f;
        private const float SepHeight = 14f;

        internal static float StripHeight
        {
            get { return RowHeight + PadY * 2f; }
        }

        /// <summary>绘制命令条（单行）；返回新的 y。</summary>
        internal static float Draw(Rect rect, float y, ManufacturingCommandBarSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsEmpty || rect.width <= 1f)
            {
                return y;
            }
            Rect strip = new Rect(rect.x, y, rect.width, StripHeight);
            UIComponents.Card(strip, UITheme.BorderSoft);

            float rowY = strip.y + PadY;
            float right = strip.xMax - PadX;

            // ---- 左段：产线汇总（标题 · 总加工量 · 三个汇点 chip，横排不换行）----
            float x = strip.x + PadX;
            string caption = snapshot.LineCaption;
            float captionWidth = Mathf.Min(Measure(caption, GameFont.Tiny) + 2f, rect.width * 0.3f);
            UIComponents.Label(new Rect(x, rowY, captionWidth, RowHeight),
                UIComponents.TruncateToWidth(caption, captionWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleLeft);
            x += captionWidth + Gap;

            string total = snapshot.TotalText;
            float totalWidth = Mathf.Min(Measure(total, GameFont.Tiny) + 2f, rect.width * 0.18f);
            UIComponents.Label(new Rect(x, rowY, totalWidth, RowHeight),
                UIComponents.TruncateToWidth(total, totalWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
            x += totalWidth + Gap;

            // ---- 右段：体检结论 + 各项（先量总宽，从右端往左排，保证靠右不被挤掉）----
            float checkWidth = MeasureCheckRow(snapshot) + 6f;
            float sepX = Mathf.Min(x, right - checkWidth - Gap);
            if (sepX > x)
            {
                // 预览 `.cmdsep{width:1px;height:14px;background:var(--line2)}`——居中于行内
                Widgets.DrawBoxSolid(new Rect(sepX, rowY + (RowHeight - SepHeight) * 0.5f,
                    SepWidth, SepHeight), UITheme.BorderSoft);
                x = sepX + SepWidth + Gap;
            }
            else
            {
                sepX = x;
                x = sepX;
            }

            // 汇点 chip 排在分隔线左侧的剩余宽度里（放不下就跳过，与预览 overflow:hidden 等价）
            List<SinkChipView> sinks = snapshot.Sinks;
            if (sinks != null)
            {
                float chipX = strip.x + PadX + captionWidth + Gap + totalWidth + Gap;
                for (int i = 0; i < sinks.Count; i++)
                {
                    SinkChipView sink = sinks[i];
                    string text = sink.Label + " " + (sink.Available
                        ? sink.Value
                        : "PersonalChronicle.UI.Analysis.Unavailable".Translate().ToString());
                    float width = Mathf.Min(Measure(text, GameFont.Tiny) + 16f, rect.width * 0.2f);
                    if (chipX + width > sepX - 6f)
                    {
                        break;
                    }
                    UIComponents.Badge(new Rect(chipX, rowY + 1f, width, RowHeight - 2f), text,
                        ToneColor(sink.Tone, sink.Available));
                    chipX += width + 6f;
                }
            }

            // ---- 右段绘制（体检引擎 + 结论 + 各项）----
            DrawCheckRow(new Rect(x, rowY, Mathf.Max(20f, right - x), RowHeight), snapshot);
            return strip.yMax;
        }

        /// <summary>体检段总宽（用于从右端预留）。</summary>
        private static float MeasureCheckRow(ManufacturingCommandBarSnapshot snapshot)
        {
            float width = Mathf.Max(52f, Measure(T("PersonalChronicle.UI.Cmd.Check.Button"), GameFont.Tiny) + 18f);
            width += Gap;
            width += Measure(snapshot.VerdictText, GameFont.Tiny) + 4f + Gap;
            List<CheckItemView> checks = snapshot.Checks;
            if (checks != null)
            {
                for (int i = 0; i < checks.Count; i++)
                {
                    width += Measure(checks[i].Text, GameFont.Tiny) + Gap;
                }
            }
            return width;
        }

        /// <summary>体检段绘制：体检按钮 + 结论 + 各项横排（放不下即截断，完整清单在 tooltip）。</summary>
        private static void DrawCheckRow(Rect rect, ManufacturingCommandBarSnapshot snapshot)
        {
            float x = rect.x;
            float right = rect.xMax;
            float buttonWidth = Mathf.Max(52f, Measure(T("PersonalChronicle.UI.Cmd.Check.Button"), GameFont.Tiny) + 18f);
            Rect button = new Rect(x, rect.y, buttonWidth, rect.height);
            // 预览 `.cmdbar button{font-size:10px;padding:2px 8px}` ⇒ 小巧的按钮
            if (Widgets.ButtonText(button, T("PersonalChronicle.UI.Cmd.Check.Button")))
            {
                // 体检为即时只读检查：点击只刷新快照（由窗口的强制刷新钩子完成）
            }
            TooltipHandler.TipRegion(button, BuildCheckTooltip(snapshot));
            x += buttonWidth + Gap;

            Color verdictColor = snapshot.Pass ? UITheme.Alive : UITheme.Dead;
            float verdictWidth = Measure(snapshot.VerdictText, GameFont.Tiny) + 4f;
            if (x + verdictWidth > right)
            {
                return;
            }
            UIComponents.Label(new Rect(x, rect.y, verdictWidth, rect.height),
                snapshot.VerdictText, GameFont.Tiny, verdictColor, TextAnchor.MiddleLeft);
            x += verdictWidth + Gap;

            List<CheckItemView> checks = snapshot.Checks;
            if (checks == null)
            {
                return;
            }
            for (int i = 0; i < checks.Count; i++)
            {
                CheckItemView item = checks[i];
                float width = Measure(item.Text, GameFont.Tiny);
                if (x + width > right)
                {
                    break;
                }
                UIComponents.Label(new Rect(x, rect.y, width, rect.height),
                    item.Text, GameFont.Tiny, LevelColor(item.Level), TextAnchor.MiddleLeft);
                x += width + Gap;
            }
        }

        private static string BuildCheckTooltip(ManufacturingCommandBarSnapshot snapshot)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(T("PersonalChronicle.UI.Cmd.Check.Title"));
            List<CheckItemView> checks = snapshot.Checks;
            if (checks != null)
            {
                for (int i = 0; i < checks.Count; i++)
                {
                    builder.Append('\n').Append(checks[i].Text);
                }
            }
            return builder.ToString();
        }

        private static Color LevelColor(string level)
        {
            if (level == "fail")
            {
                return UITheme.Dead;
            }
            return level == "warn" ? UITheme.Threat : UITheme.Muted;
        }

        private static Color ToneColor(SystemCenterTone tone, bool available)
        {
            if (!available)
            {
                return UITheme.Dim;
            }
            switch (tone)
            {
                case SystemCenterTone.Good: return UITheme.Alive;
                case SystemCenterTone.Warn: return UITheme.Threat;
                case SystemCenterTone.Bad: return UITheme.Dead;
                default: return UITheme.Info;
            }
        }

        private static float Measure(string text, GameFont font)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }
            GameFont previous = Text.Font;
            Text.Font = font;
            float width = Text.CalcSize(text).x;
            Text.Font = previous;
            return width;
        }

        private static string T(string key)
        {
            return key.Translate().ToString();
        }
    }
}

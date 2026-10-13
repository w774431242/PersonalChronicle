using System;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 时段编辑器（起始/结束小时 + 快捷档）。小时精度，与 <see cref="WorkshopHours.InRange"/>
    /// 的判定口径完全一致（含"起 == 止 = 全天"与"止 &lt; 起 = 跨午夜"两条约定）。
    ///
    /// <para>固定班与班组共用本弹窗（回调式：只负责取值，写回哪个对象由调用方决定）。</para>
    /// </summary>
    internal sealed class Dialog_TimeRange : Window
    {
        private const string KeyPrefix = "PersonalChronicle.Automation.Tab.";
        private const float RowH = 30f;
        private const float Gap = 8f;

        private readonly Action<int, int> onApply;

        private int start;
        private int end;

        public override Vector2 InitialSize => new Vector2(380f, 240f);

        // ⚠ CHG-20261005-MEALHOUR（2026-10-05 玩家要求「三餐不要时间段，改为时间选择，
        //   选择单个时间」）：本弹窗上一批曾有一个 `closedWhenEqual` 参数，用途是让**用餐窗口**
        //   把"起 == 止"显示成"关闭该餐"（与班次时段的"全天"相对）。
        //   现在三餐改用**单点**选择器（<see cref="Dialog_HourPick"/>）⇒ 用餐侧不再使用本弹窗，
        //   该参数失去唯一调用方，已移除。剩余唯一语义即班次时段：起 == 止 = 全天。

        /// <summary>时段编辑（起 == 止 = 全天班；止 &lt; 起 = 跨午夜）。</summary>
        public Dialog_TimeRange(int startHour, int endHour, Action<int, int> onApply)
        {
            start = Mathf.Clamp(startHour, 0, 23);
            end = Mathf.Clamp(endHour, 0, 23);
            this.onApply = onApply;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = true;
            doCloseX = true;
            draggable = true;
            resizeable = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;
            UIComponents.Label(new Rect(inRect.x, y, inRect.width, RowH),
                T("TimeRangeTitle"), GameFont.Medium, UITheme.Text);
            y += RowH + Gap;

            y = DrawHourRow(inRect, y, T("TimeStart"), ref start);
            y = DrawHourRow(inRect, y, T("TimeEnd"), ref end);

            // 口径提示：起 == 止 = 全天；止 < 起 = 跨午夜（与 WorkshopHours.InRange 同源）。
            string note = WorkshopHours.LabelOf(start, end);
            if (start == end)
            {
                note += " · " + T("TimeAllDay");
            }
            else if (WorkshopHours.CrossesMidnight(start, end))
            {
                note += " · " + T("TimeWrap");
            }
            UIComponents.Label(new Rect(inRect.x, y, inRect.width, RowH), note,
                GameFont.Small, UITheme.Muted);
            y += RowH + Gap;

            // 快捷档（白班 / 夜班 / 全天）——一键覆盖两端。
            float btnW = (inRect.width - Gap * 2f) / 3f;
            if (UIComponents.ThemedButton(new Rect(inRect.x, y, btnW, RowH), T("PresetDay")))
            {
                start = 6;
                end = 18;
            }
            if (UIComponents.ThemedButton(new Rect(inRect.x + btnW + Gap, y, btnW, RowH), T("PresetNight")))
            {
                start = 18;
                end = 6;
            }
            if (UIComponents.ThemedButton(new Rect(inRect.x + (btnW + Gap) * 2f, y, btnW, RowH),
                    T("PresetAll")))
            {
                start = 0;
                end = 0;
            }

            float footerY = inRect.yMax - RowH;
            float okW = 120f;
            if (UIComponents.ThemedButton(new Rect(inRect.xMax - okW, footerY, okW, RowH), T("Ok")))
            {
                if (onApply != null)
                {
                    onApply(start, end);
                }
                Close();
            }
            if (UIComponents.ThemedButton(new Rect(inRect.xMax - okW * 2f - Gap, footerY, okW, RowH), T("Cancel")))
            {
                Close();
            }
        }

        /// <summary>单行小时选择（− 0..23 ＋ + 滑块；滑块用整数步进避免小数小时）。</summary>
        private float DrawHourRow(Rect rect, float y, string label, ref int value)
        {
            const float stepW = 28f;
            UIComponents.Label(new Rect(rect.x, y, 70f, RowH), label, GameFont.Small, UITheme.Muted);
            Rect minus = new Rect(rect.x + 74f, y, stepW, RowH);
            if (UIComponents.ThemedButton(minus, "-"))
            {
                value = (value + 23) % 24;
            }
            Rect plus = new Rect(rect.xMax - stepW, y, stepW, RowH);
            if (UIComponents.ThemedButton(plus, "+"))
            {
                value = (value + 1) % 24;
            }
            Rect slider = new Rect(minus.xMax + Gap, y + 3f, plus.x - minus.xMax - Gap * 2f, RowH - 6f);
            float raw = Widgets.HorizontalSlider(slider, value, 0f, 23f, true,
                value.ToString("00", System.Globalization.CultureInfo.InvariantCulture));
            value = Mathf.Clamp(Mathf.RoundToInt(raw), 0, 23);
            return y + RowH + Gap;
        }

        private static string T(string suffix)
        {
            return (KeyPrefix + suffix).Translate().ToString();
        }
    }
}

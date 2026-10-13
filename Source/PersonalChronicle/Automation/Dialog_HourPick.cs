using System;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 单点时间选择器（小时精度）—— CHG-20261005-MEALHOUR。
    ///
    /// <para><b>为什么不复用 <see cref="Dialog_TimeRange"/></b>：那个是"起止段"编辑器，
    /// 带着一套"半开区间 / 起==止 / 跨天"的语义；对"到点吃饭"这种<b>单个时刻</b>而言
    /// 那些语义是多余的，且实测让玩家困惑（2026-10-05 玩家要求
    /// 「三餐不要时间段，改为时间选择，选择单个时间」）。本窗口只做一件事：选一个小时。</para>
    ///
    /// <para><b>-1 的表达</b>：<paramref name="onApply"/> 收到 <c>-1</c> 表示"关闭该餐"
    /// （对应 <c>WorkshopShift.Meal*Hour == -1</c>）。
    /// <b>取消不发任何回调</b>（连 -1 都不发）—— 取消 ≠ 关掉这餐。</para>
    /// </summary>
    internal sealed class Dialog_HourPick : Window
    {
        private const string KeyPrefix = "PersonalChronicle.Automation.Tab.";
        private const float RowH = 30f;
        private const float Gap = 8f;

        private readonly Action<int> onApply;
        private int hour;

        public override Vector2 InitialSize => new Vector2(320f, 230f);

        /// <param name="hour">当前时间点（-1 = 该餐关闭 ⇒ 从 12:00 起调）。</param>
        /// <param name="onApply">确定时的回调；参数 -1 = 关闭该餐。</param>
        public Dialog_HourPick(int hour, Action<int> onApply)
        {
            this.onApply = onApply;
            this.hour = hour >= 0 && hour <= 23 ? hour : 12;
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
                T("HourPickTitle"), GameFont.Medium, UITheme.Text);
            y += RowH + Gap;

            // 单行：− [滑块] +
            const float stepW = 30f;
            if (UIComponents.ThemedButton(new Rect(inRect.x, y, stepW, RowH), "-"))
            {
                hour = (hour + 23) % 24;
            }
            if (UIComponents.ThemedButton(new Rect(inRect.xMax - stepW, y, stepW, RowH), "+"))
            {
                hour = (hour + 1) % 24;
            }
            Rect slider = new Rect(inRect.x + stepW + Gap, y + 3f,
                inRect.width - stepW * 2f - Gap * 2f, RowH - 6f);
            float raw = Widgets.HorizontalSlider(slider, hour, 0f, 23f, true, LabelOfHour(hour));
            hour = Mathf.Clamp(Mathf.RoundToInt(raw), 0, 23);
            y += RowH + Gap;

            UIComponents.Label(new Rect(inRect.x, y, inRect.width, RowH),
                T1("HourPickHint", LabelOfHour(hour)), GameFont.Small, UITheme.Muted);

            float footerY = inRect.yMax - RowH;
            float okW = 104f;
            // 「关闭该餐」：单点模型下"取消某一餐"必须显式提供 ——
            // 旧的"起 == 止 = 关闭"表达法随起止段一起消失了。
            if (UIComponents.ThemedButton(new Rect(inRect.x, footerY, okW, RowH), T("HourPickClose")))
            {
                if (onApply != null)
                {
                    onApply(-1);
                }
                Close();
            }
            if (UIComponents.ThemedButton(new Rect(inRect.xMax - okW, footerY, okW, RowH), T("Ok")))
            {
                if (onApply != null)
                {
                    onApply(hour);
                }
                Close();
            }
            if (UIComponents.ThemedButton(new Rect(inRect.xMax - okW * 2f - Gap, footerY, okW, RowH),
                    T("Cancel")))
            {
                Close();
            }
        }

        /// <summary>
        /// 小时 → "07:00"（不变文化：不随语言切换改分隔符）。
        /// <para><b>internal 而非 private</b>：班次编辑器也要显示同一个格式
        /// （班中餐按钮上的文本），两处各写一份就会出现"弹窗里 07:00、行里 7 点"的漂移
        /// ⇒ 收口到这里作为**单一实现**（GOV-005）。</para>
        /// </summary>
        internal static string LabelOfHour(int h)
        {
            return h < 0
                ? string.Empty
                : h.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":00";
        }

        private static string T(string suffix)
        {
            return (KeyPrefix + suffix).Translate().ToString();
        }

        private static string T1(string suffix, string arg)
        {
            return (KeyPrefix + suffix).Translate(arg).ToString();
        }
    }
}

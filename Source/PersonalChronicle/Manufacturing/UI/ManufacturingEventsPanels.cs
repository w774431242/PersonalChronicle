using PersonalChronicle.MasterControl.Population;
using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using UnityEngine;
using Verse;
using RimWorld;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// 制造中心 ⑤ 事件与设备健康（对齐预览 §⑤：<c>s8</c> 实时事件流 + <c>s4</c> 设备健康 / TPM）。
    ///
    /// <para><b>事件文本复用档案馆的渲染助手</b>（<see cref="PersonnelArchivePanel"/> 的
    /// <c>EventName</c> / <c>EventTypeToGlyph</c> / <c>EventTypeToColor</c> / <c>FormatDate</c>）——
    /// 事件文案只有一处真源，避免两套写法漂移。</para>
    ///
    /// <para>响应式：宽 ≥ <see cref="TwoColumnWidth"/> 时 2 列（事件 2 : 健康 1），否则上下堆叠（LAND-005）。</para>
    /// </summary>
    internal static class ManufacturingEventsPanels
    {
        private const float PanelGap = 8f;
        private const float HeaderHeight = 42f;
        private const float RowHeight = 17f;
        private const float EventPanelHeight = 262f;
        private const float HealthPanelHeight = 262f;

        /// <summary>
        /// 本组面板数（⑤ 事件与设备健康 = 实时事件流 + 设备健康 / TPM 两块）。
        /// **单一真源**：底栏页签「（n）」引用本常量（CHG-20261006-TABUNIFY 3c）。
        /// </summary>
        internal const int PanelCount = 2;
        private const float TwoColumnWidth = 700f;

        /// <summary>整块高度 —— 与 <see cref="Draw"/> 同式。</summary>
        internal static float BlockHeight(float width)
        {
            return width >= TwoColumnWidth
                ? Mathf.Max(EventPanelHeight, HealthPanelHeight)
                : EventPanelHeight + PanelGap + HealthPanelHeight;
        }

        internal static float Draw(Rect rect, float y, ManufacturingEventsSnapshot snapshot)
        {
            if (snapshot == null || rect.width <= 1f)
            {
                return y;
            }

            if (rect.width >= TwoColumnWidth)
            {
                float healthWidth = Mathf.Max(220f, rect.width * 0.34f);
                float eventWidth = rect.width - healthWidth - PanelGap;
                DrawEventPanel(new Rect(rect.x, y, eventWidth, EventPanelHeight), snapshot);
                DrawHealthPanel(new Rect(rect.x + eventWidth + PanelGap, y, healthWidth, HealthPanelHeight), snapshot);
                return y + Mathf.Max(EventPanelHeight, HealthPanelHeight);
            }

            DrawEventPanel(new Rect(rect.x, y, rect.width, EventPanelHeight), snapshot);
            float second = y + EventPanelHeight + PanelGap;
            DrawHealthPanel(new Rect(rect.x, second, rect.width, HealthPanelHeight), snapshot);
            return second + HealthPanelHeight;
        }

        /// <summary>实时事件流（预览 <c>#log</c>：时间 + 级别徽标 + 文本）。</summary>
        private static void DrawEventPanel(Rect panel, ManufacturingEventsSnapshot snapshot)
        {
            Rect body = DrawPanelChrome(panel,
                "PersonalChronicle.UI.Events.Stream.Title",
                "PersonalChronicle.UI.Events.Stream.Stencil",
                snapshot.EventsSub);

            List<ChronicleEvent> events = snapshot.Events;
            if (events == null || events.Count == 0)
            {
                Label(new Rect(body.x, body.y + 4f, body.width, 16f),
                    T("PersonalChronicle.UI.Events.Stream.Empty"), GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                return;
            }

            int capacity = Mathf.Max(0, Mathf.FloorToInt(body.height / RowHeight));
            int count = Mathf.Min(capacity, events.Count);
            for (int i = 0; i < count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev == null)
                {
                    continue;
                }
                Rect row = new Rect(body.x, body.y + i * RowHeight, body.width, RowHeight - 2f);
                if (Mouse.IsOver(row))
                {
                    Widgets.DrawHighlight(row);
                }

                Color color = PersonnelArchivePanel.EventTypeToColor(ev.TypeKey);
                // 时间（相对"距今"，比绝对日期更适合实时日志）
                string when = RelativeTime(ev.Tick);
                float whenWidth = 62f;
                Label(new Rect(row.x, row.y, whenWidth, row.height), when,
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                // 字形 + 文本
                float glyphWidth = 16f;
                Label(new Rect(row.x + whenWidth, row.y, glyphWidth, row.height),
                    PersonnelArchivePanel.EventTypeToGlyph(ev.TypeKey), GameFont.Tiny, color, TextAnchor.MiddleLeft);
                float textWidth = Mathf.Max(20f, row.width - whenWidth - glyphWidth);
                Label(new Rect(row.x + whenWidth + glyphWidth, row.y, textWidth, row.height),
                    PersonnelArchivePanel.EventName(ev), GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);

                string description = PersonnelArchivePanel.EventDescription(ev);
                TooltipHandler.TipRegion(row, string.IsNullOrEmpty(description)
                    ? PersonnelArchivePanel.EventName(ev)
                    : PersonnelArchivePanel.EventName(ev) + "\n" + description);
            }
        }

        /// <summary>设备健康 / TPM（用户设备名 + 当前状态 + 健康条 + 口径附注）。</summary>
        private static void DrawHealthPanel(Rect panel, ManufacturingEventsSnapshot snapshot)
        {
            Rect body = DrawPanelChrome(panel,
                "PersonalChronicle.UI.Events.Health.Title",
                "PersonalChronicle.UI.Events.Health.Stencil",
                snapshot.HealthSub);

            List<HealthRowView> rows = snapshot.Health;
            if (rows == null || rows.Count == 0)
            {
                Label(new Rect(body.x, body.y + 4f, body.width, 16f),
                    T("PersonalChronicle.UI.Events.Health.Empty"), GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                return;
            }

            int capacity = Mathf.Max(0, Mathf.FloorToInt(body.height / RowHeight));
            int count = Mathf.Min(capacity, rows.Count);
            for (int i = 0; i < count; i++)
            {
                HealthRowView row = rows[i];
                Rect line = new Rect(body.x, body.y + i * RowHeight, body.width, RowHeight - 2f);
                float stateWidth = 34f;
                float barWidth = Mathf.Min(58f, body.width * 0.28f);
                float nameWidth = Mathf.Max(30f, line.width - stateWidth - barWidth - 8f);
                Label(new Rect(line.x, line.y, nameWidth, line.height), row.Name,
                    GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
                Label(new Rect(line.x + nameWidth, line.y, stateWidth, line.height), row.StateText,
                    GameFont.Tiny, ToneColor(row.Tone), TextAnchor.MiddleLeft);
                Rect bar = new Rect(line.xMax - barWidth, line.y + (line.height - 6f) * 0.5f, barWidth, 6f);
                Widgets.DrawBoxSolid(bar, UITheme.BorderHair);
                Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(row.Health01), bar.height),
                    ToneColor(row.Tone));
                if (!string.IsNullOrEmpty(row.Note))
                {
                    TooltipHandler.TipRegion(line, row.Name + " · " + row.StateText + "\n" + row.Note);
                }
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

        /// <summary>
        /// 相对时间（刚刚 / n 小时前 / n 天前 / n 年前）；tick 为负（未知）时留空。
        ///
        /// <para>⚠ CHG-20261006-EVENTSTIME 修复两处缺陷：①**除法基数与单位后缀错位** ——
        /// 原实现按 <c>TicksPerHour</c> 取整却拼 <c>"m"</c>、按 <c>TicksPerDay</c> 拼 <c>"h"</c>、
        /// 按 <c>TicksPerYear</c> 拼 <c>"d"</c>，于是"5 小时"渲染成 <c>5m</c>（读作 5 分钟）、
        /// "10 天"渲染成 <c>10h</c>；②后缀为**裸字面量**，中文界面出现英文单位（LOC-001）。
        /// 现基数与单位一一对齐，且后缀全部走 Keyed。</para>
        /// </summary>
        private static string RelativeTime(long tick)
        {
            if (tick < 0 || Find.TickManager == null)
            {
                return string.Empty;
            }
            long delta = Find.TickManager.TicksGame - tick;
            if (delta < 0)
            {
                delta = 0;
            }
            if (delta < GenDate.TicksPerHour)
            {
                return T("PersonalChronicle.UI.Events.Now");
            }
            if (delta < GenDate.TicksPerDay)
            {
                return T1("PersonalChronicle.UI.Events.HoursAgo", (int)(delta / GenDate.TicksPerHour));
            }
            if (delta < GenDate.TicksPerYear)
            {
                return T1("PersonalChronicle.UI.Events.DaysAgo", (int)(delta / GenDate.TicksPerDay));
            }
            return T1("PersonalChronicle.UI.Events.YearsAgo", (int)(delta / GenDate.TicksPerYear));
        }

        private static string T(string key)
        {
            return key.Translate().ToString();
        }

        /// <summary>
        /// 单参格式化翻译。**必须走 <see cref="NamedArgument"/> 重载** —— 传 <c>object</c> 会落到
        /// 已过时的 <c>params object[]</c> 版本（CS0618），这是本工程已踩过的坑（1.6 翻译 API）。
        /// </summary>
        private static string T1(string key, int arg)
        {
            return key.Translate((NamedArgument)arg).ToString();
        }
    }
}

using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// 统一时间跨度格式化（v5.x）：不同跨度的对象使用同等级时间单位。
    ///
    /// 一次调用官方 <see cref="GenDate.TicksToPeriod"/> 拆出 年/季/日/时，
    /// 避免各处重复手工 tick 除法。单位等级按跨度自动选择：
    ///   years > 0   → "X 年 Y 季"（年粒度）
    ///   quadrums > 0 → "X 季 Y 天"（季粒度）
    ///   days > 0    → "X 天"（天粒度）
    ///   hours ≥ 1   → "X 小时"（时粒度）
    ///   否则        → "X 分钟"
    /// 负数（未知哨兵）返回"未知"。Read Model 与窗口共用，保证口径一致。
    /// </summary>
    public static class SpanText
    {
        /// <summary>tick 跨度 → 同等级单位的中文/英文文本（已本地化）。</summary>
        public static string Format(long ticks)
        {
            if (ticks < 0L)
            {
                return "PersonalChronicle.UI.UnknownDate".Translate().ToString();
            }
            int years;
            int quadrums;
            int days;
            float hours;
            GenDate.TicksToPeriod(ticks, out years, out quadrums, out days, out hours);
            if (years > 0)
            {
                string text = "PersonalChronicle.UI.SpanYear".Translate(years).ToString();
                if (quadrums > 0)
                {
                    text = text + " " + "PersonalChronicle.UI.SpanQuadrum".Translate(quadrums).ToString();
                }
                return text;
            }
            if (quadrums > 0)
            {
                string text = "PersonalChronicle.UI.SpanQuadrum".Translate(quadrums).ToString();
                if (days > 0)
                {
                    text = text + " " + "PersonalChronicle.UI.SpanDay".Translate(days).ToString();
                }
                return text;
            }
            if (days > 0)
            {
                return "PersonalChronicle.UI.SpanDay".Translate(days).ToString();
            }
            if (hours >= 1f)
            {
                return "PersonalChronicle.UI.SpanHour".Translate(Mathf.RoundToInt(hours)).ToString();
            }
            int minutes = Mathf.Max(1, Mathf.RoundToInt(hours * 60f));
            return "PersonalChronicle.UI.SpanMinute".Translate(minutes).ToString();
        }
    }
}

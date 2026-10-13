using System.Globalization;
using PersonalChronicle.Application.ReadModels;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// KPI 视图构造器：把原始数值就地格式化成展示模型（构建期完成本地化与格式化，
    /// 绘制期零计算 —— 与 08c 数据层契约的「快照只含预格式化文本」一致）。
    ///
    /// <para>数值格式固定用 <see cref="CultureInfo.InvariantCulture"/>：千分位分隔符
    /// 不随玩家系统区域漂移，避免同一存档在不同机器上显示不同数字。</para>
    /// </summary>
    internal static class SystemCenterKpiFactory
    {
        /// <summary>「暂无数据源」占位：<see cref="SystemCenterKpiView.HasValue"/> = false。</summary>
        internal static SystemCenterKpiView Pending(string labelKey)
        {
            return Pending(labelKey, null);
        }

        /// <summary>「暂无数据源」占位 + 已本地化附注（如「无设备」「机制未落地」）。</summary>
        internal static SystemCenterKpiView Pending(string labelKey, string note)
        {
            return WithTooltip(new SystemCenterKpiView
            {
                Label = Translate(labelKey),
                Value = string.Empty,
                Note = note ?? string.Empty,
                HasValue = false,
                Tone = SystemCenterTone.Flat,
                Share01 = -1f
            });
        }

        /// <summary>
        /// 比率型 KPI（百分比 + 进度条）。<paramref name="share01"/> 同时是进度条占比，
        /// UI 只按它画条，不重算（快照即真相）。
        /// </summary>
        internal static SystemCenterKpiView Rate(
            string labelKey, string note, float share01, SystemCenterTone tone)
        {
            float clamped = Mathf.Clamp01(share01);
            return WithTooltip(new SystemCenterKpiView
            {
                Label = Translate(labelKey),
                Value = FormatPercent(clamped),
                Note = note ?? string.Empty,
                HasValue = true,
                Tone = tone,
                Share01 = clamped
            });
        }

        /// <summary>整数指标（数量/台数/人数）。</summary>
        internal static SystemCenterKpiView Count(string labelKey, int value, SystemCenterTone tone)
        {
            return Text(labelKey, value.ToString("N0", CultureInfo.InvariantCulture), tone);
        }

        /// <summary>已格式化的文本指标。</summary>
        internal static SystemCenterKpiView Text(string labelKey, string value, SystemCenterTone tone)
        {
            return WithTooltip(new SystemCenterKpiView
            {
                Label = Translate(labelKey),
                Value = value ?? string.Empty,
                HasValue = true,
                Tone = tone,
                Share01 = -1f
            });
        }

        /// <summary>
        /// 已格式化文本指标 + 口径副标（**数字卡**路径：非 0..1 比率量走这里，
        /// 不走表盘，避免 <see cref="Rate"/> 的 Clamp01 把 &gt;1 的数截断 —— 2026-09-22 指标重拟）。
        /// </summary>
        internal static SystemCenterKpiView Text(string labelKey, string value, SystemCenterTone tone, string note)
        {
            SystemCenterKpiView view = Text(labelKey, value, tone);
            view.Note = note ?? string.Empty;
            return WithTooltip(view);   // Note 变更后重算悬停文本
        }

        /// <summary>银值指标（自动补本地化单位）。</summary>
        internal static SystemCenterKpiView Silver(string labelKey, float value, SystemCenterTone tone)
        {
            return Text(labelKey, FormatSilver(value), tone);
        }

        /// <summary>银值格式化（整数 + 千分位 + 本地化单位），供 KPI 与殖民地快照共用。</summary>
        internal static string FormatSilver(float value)
        {
            string unit = Translate("PersonalChronicle.UI.MasterControl.Unit.Silver");
            string number = ((int)value).ToString("N0", CultureInfo.InvariantCulture);
            return number + " " + unit;
        }

        /// <summary>百分比文本（0..1 → 四舍五入整数百分比）。</summary>
        internal static string FormatPercent(float share01)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(share01) * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>
        /// 悬停提示 = 完整标签（+ 附注）。窄卡上标签/附注会被省略号截断，
        /// 提示在构建期拼好，保证绘制路径零分配（PERF）。
        /// </summary>
        private static SystemCenterKpiView WithTooltip(SystemCenterKpiView view)
        {
            view.TooltipText = string.IsNullOrEmpty(view.Note)
                ? view.Label
                : view.Label + " — " + view.Note;
            return view;
        }

        private static string Translate(string key)
        {
            return key.Translate().ToString();
        }
    }
}

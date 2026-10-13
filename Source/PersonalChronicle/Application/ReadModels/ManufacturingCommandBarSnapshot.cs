using System.Collections.Generic;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 制造中心 ② 的 `#cmdbar` 快照：**产线汇总条**（预览 `#route-bar` + `fillFlowSum()`）
    /// 与**视图体检**（预览 `#hc-out` + `runHealthCheck()`）。
    ///
    /// <para>口径纠正（精读预览源码后）：<c>#route-bar</c> 不是"工序链路切换条"，而是产线汇总条；
    /// <c>#stress-bar</c> 是仿真控件（压力注入 / 复位整个仿真），游戏内无对应物，**有意不落地**。</para>
    /// </summary>
    public sealed class ManufacturingCommandBarSnapshot : ArchiveSectionSnapshot
    {
        /// <summary>产线标题（"武器工序车间 ①→⑫ · 12 站 × 2 设备"，用真实计数）。</summary>
        public string LineCaption = string.Empty;

        /// <summary>总加工量（"总加工量 N"，最近 1 游戏日产出）。</summary>
        public string TotalText = string.Empty;

        /// <summary>三个 sink chip：入库 / 外运 / 线边在制。</summary>
        public List<SinkChipView> Sinks = new List<SinkChipView>();

        /// <summary>视图体检结果行（含 PASS/FAIL 与各项）。</summary>
        public List<CheckItemView> Checks = new List<CheckItemView>();

        /// <summary>体检结论（fail 数为 0 即 PASS）。</summary>
        public bool Pass = true;

        /// <summary>体检汇总文字（"PASS" / "FAIL 2"）。</summary>
        public string VerdictText = string.Empty;
    }

    /// <summary>产线汇总里的一个汇点。</summary>
    public sealed class SinkChipView
    {
        public string Label = string.Empty;
        public string Value = string.Empty;
        public SystemCenterTone Tone = SystemCenterTone.Flat;

        /// <summary>false = 该口径原版无出口（画「未接入」，不画 0）。</summary>
        public bool Available = true;
    }

    /// <summary>体检的一项。</summary>
    public sealed class CheckItemView
    {
        /// <summary>严重度：fail / warn / info。</summary>
        public string Level = "info";

        /// <summary>已本地化文本（含前缀符号 ✕ / ! / ·）。</summary>
        public string Text = string.Empty;
    }
}

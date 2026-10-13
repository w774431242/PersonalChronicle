using System.Collections.Generic;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 制造中心 ③ 分析带快照：瓶颈 / 六大损失 / 吞吐趋势 / 在制 WIP（对应预览 §③ 的四个面板）。
    /// 字段全部预格式化，UI 只画不算（08c DL 契约）。
    /// </summary>
    public sealed class ManufacturingAnalysisSnapshot : ArchiveSectionSnapshot
    {
        /// <summary>工序瓶颈带：每台在产设备的"额定工量占比"。</summary>
        public List<AnalysisBottleneckView> Bottlenecks = new List<AnalysisBottleneckView>();

        /// <summary>瓶颈结论（"瓶颈：{设备}（{工量}）"）；无在产设备时为空。</summary>
        public string BottleneckSummary = string.Empty;

        /// <summary>OEE 六大损失（固定 6 行；<see cref="AnalysisLossView.Available"/> = false 表示口径未接入）。</summary>
        public List<AnalysisLossView> Losses = new List<AnalysisLossView>();

        /// <summary>未接入的损失项数（副标用）。</summary>
        public int LossUnavailableCount;

        /// <summary>吞吐趋势（各点已归一化到 0..1，供折线使用）。</summary>
        public List<float> Trend = new List<float>();

        /// <summary>当前吞吐（"N 件/日"）。</summary>
        public string TrendNowText = string.Empty;

        /// <summary>产出记录不足时为 false → UI 画空态而不画一条假线。</summary>
        public bool TrendAvailable;

        /// <summary>在制品 WIP 四格。</summary>
        public List<AnalysisWipView> Wip = new List<AnalysisWipView>();
    }

    /// <summary>瓶颈带一段。</summary>
    public sealed class AnalysisBottleneckView
    {
        public string Name = string.Empty;

        /// <summary>段上文字（"{工量}"）。</summary>
        public string ValueText = string.Empty;

        /// <summary>占比 0..1。</summary>
        public float Ratio01;

        /// <summary>最慢段（瓶颈）。</summary>
        public bool IsBottleneck;
    }

    /// <summary>六大损失一行。</summary>
    public sealed class AnalysisLossView
    {
        public string Label = string.Empty;

        /// <summary>值文字（可测 = 数字；未接入 = 空 → UI 画「—」）。</summary>
        public string ValueText = string.Empty;

        /// <summary>条占比 0..1（未接入时无意义）。</summary>
        public float Ratio01;

        public SystemCenterTone Tone = SystemCenterTone.Flat;

        /// <summary>false = 该口径原版无出口（LAND-009：画成「未接入」而不是 0）。</summary>
        public bool Available;
    }

    /// <summary>WIP 一格。</summary>
    public sealed class AnalysisWipView
    {
        public string Label = string.Empty;
        public string Value = string.Empty;
        public bool Available = true;
    }
}

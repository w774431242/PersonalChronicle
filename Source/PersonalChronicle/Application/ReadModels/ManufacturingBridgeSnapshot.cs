using System.Collections.Generic;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 制造中心 ⑥ 数据桥快照（对齐预览 §⑥ 的 6 个面板）。
    ///
    /// <para>预览里这 6 块**全部是** <c>class="panel pending"</c> 占位（"⏳ 待 DataBridge"）——
    /// 因为它当时要跨 mod 取数。本工程在同一进程内，因此按 PM 决策走**混合**：
    /// <b>3 块真接</b>（生产链路看板 / 能源拓扑 / 质量 Pareto —— 原版数据现成），
    /// <b>3 块照实占位</b>（工单甘特 / 数字孪生 / 维护计划 —— 需自研，不假装）。</para>
    /// </summary>
    public sealed class ManufacturingBridgeSnapshot : ArchiveSectionSnapshot
    {
        /// <summary>6 个面板（顺序与预览一致；Weight = 12 栏栅格权重，用于两行布局）。</summary>
        public List<BridgePanelView> Panels = new List<BridgePanelView>();
    }

    /// <summary>数据桥的一个面板。</summary>
    public sealed class BridgePanelView
    {
        public string Title = string.Empty;
        public string Stencil = string.Empty;
        public string Sub = string.Empty;

        /// <summary>true = 照实占位（画「待接入」+ 落地路径说明）。</summary>
        public bool Pending;

        /// <summary>占位面板的说明（落地路径 / 阻塞原因）。</summary>
        public string PendingNote = string.Empty;

        /// <summary>12 栏栅格权重（预览 s4/s4/s4/s6/s3/s3）。</summary>
        public int Weight = 4;

        /// <summary>表头（可空 = 无表头，直接列 <see cref="Rows"/>）。</summary>
        public List<string> Headers = new List<string>();

        /// <summary>行（每行多列）。</summary>
        public List<BridgeRowView> Rows = new List<BridgeRowView>();
    }

    /// <summary>数据桥面板的一行。</summary>
    public sealed class BridgeRowView
    {
        public List<string> Cells = new List<string>();

        /// <summary>该行条占比 0..1（&lt; 0 = 不画条，如 Pareto / PowerNet 的纯数值行）。</summary>
        public float Bar01 = -1f;

        public SystemCenterTone Tone = SystemCenterTone.Flat;
    }
}

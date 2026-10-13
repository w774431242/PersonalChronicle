using System.Collections.Generic;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 制造中心 ② 的 `#rightcol` 快照：**生产队列**（`WORK ORDER QUEUE`）+ **原料仓**（`RAW STOCK`）。
    ///
    /// <para>预览里这两块是 `#floorwrap` 下方的两个面板（`#rightcol{flex-direction:column}`，纵向堆叠）。</para>
    /// </summary>
    public sealed class ManufacturingStockQueueSnapshot : ArchiveSectionSnapshot
    {
        /// <summary>生产队列副标（口径说明）。</summary>
        public string QueueSub = string.Empty;

        /// <summary>生产队列行（真实 <c>BillStack</c>）。</summary>
        public List<WorkOrderRowView> Queue = new List<WorkOrderRowView>();

        /// <summary>原料仓副标（含安全线口径）。</summary>
        public string RawSub = string.Empty;

        /// <summary>原料仓行（按水位降序）。</summary>
        public List<RawStockRowView> RawStock = new List<RawStockRowView>();
    }

    /// <summary>生产队列一行。</summary>
    public sealed class WorkOrderRowView
    {
        public string Machine = string.Empty;
        public string Recipe = string.Empty;
        public string AmountText = string.Empty;
        public string StateText = string.Empty;
        public bool Suspended;
    }

    /// <summary>原料仓一行。</summary>
    public sealed class RawStockRowView
    {
        public string Name = string.Empty;

        /// <summary>「库存 N」。</summary>
        public string CountText = string.Empty;

        /// <summary>相对水位 0..1（以最富原料为 100%）。</summary>
        public float Level01;

        /// <summary>水位低于安全线（30%）→ 行标红 + LOW 标记。</summary>
        public bool Low;
    }
}

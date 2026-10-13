using PersonalChronicle.Application.ReadModels;
using Verse;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 制造中心 ② `#rightcol`（生产队列 + 原料仓）数据源契约。UI 只判快照非空（LAND-010）。
    /// </summary>
    public interface IManufacturingStockQueueDataProvider
    {
        /// <summary>构建生产队列 + 原料仓快照；实现必须自行吞异常并返回安全快照。</summary>
        ManufacturingStockQueueSnapshot BuildStockQueue(Map map);
    }
}

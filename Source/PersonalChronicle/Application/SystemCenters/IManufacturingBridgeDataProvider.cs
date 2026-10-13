using PersonalChronicle.Application.ReadModels;
using Verse;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 制造中心 ⑥ 数据桥（6 面板）数据源契约。UI 只判快照非空（LAND-010）。
    /// </summary>
    public interface IManufacturingBridgeDataProvider
    {
        /// <summary>构建数据桥快照；实现必须自行吞异常并返回安全快照。</summary>
        ManufacturingBridgeSnapshot BuildBridge(Map map);
    }
}

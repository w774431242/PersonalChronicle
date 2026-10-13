using PersonalChronicle.Application.ReadModels;
using Verse;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 制造中心 ⑦ 数据层契约数据源（Def 注册表 + Runtime 对象快照）。
    /// UI 只判 <c>SystemCenterDetailSnapshot.DataLayer != null</c>（LAND-010）。
    /// </summary>
    public interface IManufacturingDataLayerDataProvider
    {
        /// <summary>构建数据层契约快照；实现必须自行吞异常并返回安全快照。</summary>
        ManufacturingDataLayerSnapshot BuildDataLayer(Map map);
    }
}

using PersonalChronicle.Application.ReadModels;
using Verse;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 制造中心 ② `#cmdbar`（产线汇总 + 视图体检）数据源契约。UI 只判快照非空（LAND-010）。
    /// </summary>
    public interface IManufacturingCommandBarDataProvider
    {
        /// <summary>构建命令条快照。<paramref name="workshopCount"/> / <paramref name="deviceCount"/> 由读模型从俯视图快照带入 —— 车间枚举属 Automation 职责，这里不反向依赖（ARC-001）。</summary>
        ManufacturingCommandBarSnapshot BuildCommandBar(Map map, int workshopCount, int deviceCount);
    }
}

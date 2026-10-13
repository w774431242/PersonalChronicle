using PersonalChronicle.Application.ReadModels;
using Verse;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 制造中心 ⑤ 事件与设备健康数据源契约。UI 只判
    /// <c>SystemCenterDetailSnapshot.Events != null</c>（LAND-010）。
    /// </summary>
    public interface IManufacturingEventsDataProvider
    {
        /// <summary>构建事件流 + 设备健康快照；实现必须自行吞异常并返回安全快照。</summary>
        ManufacturingEventsSnapshot BuildEvents(Map map);
    }
}

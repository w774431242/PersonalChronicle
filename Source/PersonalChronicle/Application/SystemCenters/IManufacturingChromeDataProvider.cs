using PersonalChronicle.Application.ReadModels;
using Verse;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 制造系统中心**页面外壳**（顶栏时钟/电网 + 底栏广播）数据源契约。
    /// UI 只判 <c>SystemCenterDetailSnapshot.Chrome != null</c>（LAND-010）。
    /// </summary>
    public interface IManufacturingChromeDataProvider
    {
        /// <summary>构建外壳快照；实现必须自行吞异常并返回安全快照。</summary>
        ManufacturingChromeSnapshot BuildChrome(Map map);
    }
}

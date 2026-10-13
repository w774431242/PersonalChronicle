using PersonalChronicle.Application.ReadModels;
using Verse;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 制造中心 ④ 运营数据源契约（在岗殖民者 / 成品库 / Andon 占位）。
    ///
    /// <para>与 ③ 分开声明，是为了让"有没有这一块"由实现方决定；UI 只判
    /// <c>SystemCenterDetailSnapshot.Operations != null</c>（LAND-010）。</para>
    /// </summary>
    public interface IManufacturingOperationsDataProvider
    {
        /// <summary>构建运营快照；实现必须自行吞异常并返回安全快照（失败隔离）。</summary>
        ManufacturingOperationsSnapshot BuildOperations(Map map);
    }
}

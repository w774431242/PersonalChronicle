using PersonalChronicle.Application.ReadModels;
using Verse;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 制造中心 ③ 分析带数据源契约（瓶颈 / 六大损失 / 吞吐趋势 / 在制 WIP）。
    ///
    /// <para>与俯视图分开：分析带读的是**工单 + 产出 ledger**（Application 层既能读原版对象，
    /// 又持有档案查询面），不需要 Automation 的车间区资产。UI 只判
    /// <c>SystemCenterDetailSnapshot.Analysis != null</c> 决定是否渲染（LAND-010）。</para>
    /// </summary>
    public interface IManufacturingAnalysisDataProvider
    {
        /// <summary>构建分析带快照；实现必须自行吞异常并返回安全快照（失败隔离）。</summary>
        ManufacturingAnalysisSnapshot BuildAnalysis(Map map);
    }
}

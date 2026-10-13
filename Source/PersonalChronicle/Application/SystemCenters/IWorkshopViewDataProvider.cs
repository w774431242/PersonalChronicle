using PersonalChronicle.Application.ReadModels;
using Verse;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 车间俯视（② 主视图）数据源契约。
    ///
    /// <para>与 <see cref="ISystemCenterDataProvider"/> 分开，是因为**实现方不同**：
    /// KPI 数据源在 Application 层（只读原版对象），而俯视图要读车间区
    /// （<c>Zone_Workshop</c>，属 Automation 模块的公开入口 <c>WorkshopZoneUtility</c>），
    /// 由 Automation 侧实现本契约、在组合根按中心 ID 注册 —— 依赖方向仍是
    /// <c>Automation → Application</c>，不产生反向边（ARC-001 / GOV-005 跨模块走契约）。</para>
    ///
    /// <para>UI 只判 <c>SystemCenterDetailSnapshot.Workshop != null</c> 决定是否渲染本区块，
    /// **不认识任何中心名**（LAND-010）。</para>
    /// </summary>
    public interface IWorkshopViewDataProvider
    {
        /// <summary>
        /// 构建俯视图快照。无地图 → <see cref="WorkshopFloorSnapshot.NoMap"/> = true；
        /// 有地图但没划车间 → <see cref="WorkshopFloorSnapshot.Hint"/> 给出提示（框架仍照画）。
        /// 实现必须自行吞异常并返回安全快照（失败隔离）。
        /// </summary>
        WorkshopFloorSnapshot BuildWorkshopView(Map map);

        /// <summary>
        /// 构建设备详情弹窗快照（对齐预览 <c>#drawer</c>）。
        ///
        /// <para>按 <paramref name="slot"/> / <paramref name="index"/> 定位到**同一台**设备
        /// （口径与 <see cref="WorkshopFloorSnapshot.Devices"/> 一致，不再重复遍历规则），
        /// 然后展开该台的明细：6 格指标 → 4 页签（运行/工单/人员/品质/健康）→ 数据层契约 + 工序导航。</para>
        ///
        /// <para>取不到真实值的指标必须置 <see cref="FloorDetailMetricView.Available"/> = false
        /// 并填「—」（LAND-009）；定位失败返回 <see cref="FloorDeviceDetailSnapshot.HasDevice"/> = false 的空快照。
        /// 实现必须自行吞异常（失败隔离）。</para>
        /// </summary>
        FloorDeviceDetailSnapshot BuildDeviceDetail(Map map, int slot, int index);
    }
}

using PersonalChronicle.Application.ReadModels;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 系统中心「业务视图」契约之三：人员系统中心概览（殖民地级人事聚合面板）。
    ///
    /// 与 IArchiveViewDataProvider 同构（契约在 Application、实现方在业务模块、
    /// 组合根按中心 ID 绑定），差别是本契约真的产出数据：概览面板需要的都是殖民地级聚合，
    /// 档案馆里只有「个人维度 + 4 个计数」，没有这一层，因此必须由本契约补上。
    ///
    /// 为什么不让 UI 直接扫 PawnObject：聚合口径（工时换算、能力等权均值、名册截断上限）
    /// 必须只有一处，否则「总控概览」与「档案馆个人页」会算出两套数字
    /// （08c DL 契约：聚合属 Application，UI 只消费）。
    ///
    /// 实现必须自行吞异常并返回 IsEmpty = true 的安全快照（失败隔离）。
    /// </summary>
    public interface IPopulationOverviewProvider
    {
        /// <summary>构建人员概览快照；服务未装配或采集失败时 IsEmpty = true（UI 画空态，不伪造）。</summary>
        PopulationOverviewSnapshot BuildOverview();
    }
}

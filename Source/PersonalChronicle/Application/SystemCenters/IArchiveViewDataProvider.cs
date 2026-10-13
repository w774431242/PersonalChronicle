using PersonalChronicle.MasterControl.Population;
using PersonalChronicle.Application.ReadModels;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 系统中心「业务视图」契约之二：**把某个业务模块的主界面本体承载进中心详情页**。
    ///
    /// <para>与 <see cref="IWorkshopViewDataProvider"/> 同构（契约在 Application、实现方在业务模块、
    /// 组合根按中心 ID 绑定），差别只在**承载的是界面而不是图形**：本契约返回的快照描述
    /// 「这个中心能不能嵌一块模块主界面、以及它的空间预算」；真正的绘制由拥有该界面的模块
    /// 自己暴露（<c>Archive.UI.PersonnelArchiveHost.Draw</c>），UI 侧只判
    /// <c>SystemCenterDetailSnapshot.ArchiveView != null &amp;&amp; !IsEmpty</c> 决定要不要画
    /// —— **不认识任何中心名**（LAND-010）。</para>
    ///
    /// <para>为什么数据侧只给「预算」不给「内容」：档案馆主界面本体是一个 30-partial 的窗口，
    /// 把它的字段逐一搬成快照等于重写一遍；而它的数据早已走 <c>IArchiveService</c> +
    /// <c>IArchiveUiDataProvider</c> 快照（本次零新增采集）。因此契约只声明「可承载 + 尺寸 + 文案」。
    /// 实现必须自行吞异常并返回安全快照（失败隔离）。</para>
    /// </summary>
    public interface IArchiveViewDataProvider
    {
        /// <summary>
        /// 构建「模块主界面承载」快照。服务可用 → 填 <see cref="ArchiveViewSnapshot"/> 的空间预算与标题；
        /// 服务未装配 → <c>IsEmpty = true</c>（宿主画空态，不伪造）。实现必须自行吞异常。
        /// </summary>
        ArchiveViewSnapshot BuildArchiveView();
    }
}

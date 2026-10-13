using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using Verse;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 系统中心的 KPI 数据源扩展点（规范书 §4.x.7）。
    ///
    /// <para>职责边界：Provider <b>只做采集</b>（读游戏对象 → 产出
    /// <see cref="SystemCenterKpiView"/>），不做聚合展示、不碰本地化以外的表现、
    /// 不写任何状态。中心 Def 只声明「有这么一个中心」，KPI 由 Provider 提供；
    /// 没有 Provider 的中心在 UI 上显示「数据未接入」，不会被伪造的样例数值填充。</para>
    ///
    /// <para>第三方 Mod 可注册自己的 Provider 覆盖或新增中心数据源
    /// （<see cref="SystemCenterDataRegistry.Register"/> 按中心 ID 幂等覆盖），
    /// 核心不认识目标 Mod（GOV-008）。</para>
    /// </summary>
    public interface ISystemCenterDataProvider
    {
        /// <summary>绑定的中心稳定 ID（<c>SystemCenterDef.defName</c>）。</summary>
        string CenterDefName { get; }

        /// <summary>
        /// KPI 区块标题的 Keyed 键（如制造中心的「① 总控 KPI · 运行效率」）。
        /// 返回 null/空时 UI 回落默认标题「KPI 指标」。只放<b>键</b>，文本在语言文件里（GOV-010）。
        /// </summary>
        string KpiSectionTitleKey { get; }

        /// <summary>
        /// KPI 卡的<b>语义分组</b>（如制造中心 ① = <c>[4, 5]</c>：4 张 OEE 率卡 + 5 张仪表）。
        /// UI 据此把同一组放进同一行、组间换行，从而复现预览「4fr : 5fr」的比例；
        /// 返回 null/空 = 不分组，全部按可用宽度流式排布。累加值不足时，剩余卡片自成一组。
        /// </summary>
        IReadOnlyList<int> KpiRowSizes { get; }

        /// <summary>
        /// 该中心的详情页是否展示<b>中心清单章节</b>（职责边界 / 事件契约 / 依赖与能力）。
        /// <para><c>false</c> = 该中心已把界面重心放在业务视图上（如制造中心 ① 总控 KPI），
        /// 清单信息改由注册表卡片与悬停提示承载，详情页只留业务内容 —— 避免把
        /// 「注册凭据」这类文本混进操作界面。默认 <c>true</c>（清单尚未被业务视图取代时照常展示）。</para>
        /// </summary>
        bool ShowsCenterManifest { get; }

        /// <summary>
        /// 采集本中心的 KPI 并追加到 <paramref name="buffer"/>（调用方持有缓冲区，
        /// 绘制路径零分配）。<paramref name="map"/> 为 null（无地图场景）时
        /// 必须产出「无数据」占位 KPI，而不是抛异常。
        /// </summary>
        void AppendKpis(List<SystemCenterKpiView> buffer, Map map);
    }
}

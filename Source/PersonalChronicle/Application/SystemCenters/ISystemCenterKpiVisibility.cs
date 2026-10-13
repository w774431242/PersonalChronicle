namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 可选的系统中心能力契约：**该中心是否要画「总控 KPI 带」**（详情页顶部那排计数卡）。
    ///
    /// <para>为什么需要它：KPI 带是中心框架的通用件（制造/物流/经济/医疗都用它），
    /// 但**人员系统中心的业务视图（档案概览）自己已经画了「在册 / 已归档 / 档案跨度 / 档案对象」
    /// 统计带** —— 两条带同屏 = 同一批数字出现两次（预览里人员中心只有统计带，没有 KPI 带）。
    /// 用「有没有 Provider」判不了这件事，所以由 Provider 自己声明。</para>
    ///
    /// <para>刻意做成**独立可选接口**（而非给 <see cref="ISystemCenterDataProvider"/> 加成员）：
    /// 加成员会强制所有既有/第三方实现同步修改（编译期破坏），而可选接口用
    /// <c>provider as ISystemCenterKpiVisibility</c> 探测 —— 与既有
    /// <c>provider as IManufacturing*DataProvider</c> 完全同形（GOV-002 / GOV-008）。</para>
    /// </summary>
    public interface ISystemCenterKpiVisibility
    {
        /// <summary>false = 详情页不画 KPI 带（业务视图自带同义读数组）；注册表卡片也不取首项 KPI。</summary>
        bool ShowsKpis { get; }
    }
}

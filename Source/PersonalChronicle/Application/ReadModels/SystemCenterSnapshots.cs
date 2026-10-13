using System.Collections.Generic;
using PersonalChronicle.Application.SystemCenters;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 指标语气（决定数值着色）。UI 只做「语气 → 颜色」映射，不参与判定，
    /// 保证阈值判定集中在 Read Model（DL 契约：UI 不重算聚合）。
    /// </summary>
    public enum SystemCenterTone
    {
        /// <summary>中性：正常可读白。</summary>
        Flat = 0,

        /// <summary>正向：达成 / 改善。</summary>
        Good = 1,

        /// <summary>警示：接近阈值。</summary>
        Warn = 2,

        /// <summary>危急：越过阈值。</summary>
        Bad = 3
    }

    /// <summary>
    /// 单个 KPI 指标（已本地化、已格式化的展示模型，绘制期零计算）。
    /// </summary>
    public sealed class SystemCenterKpiView
    {
        /// <summary>指标名（构建期已本地化）。</summary>
        public string Label = string.Empty;

        /// <summary>指标值（构建期已格式化；<see cref="HasValue"/> 为 false 时忽略）。</summary>
        public string Value = string.Empty;

        /// <summary>环比/阈值附注，可为空。</summary>
        public string Note = string.Empty;

        /// <summary>语气（着色由 UI 层按 <see cref="SystemCenterTone"/> 解析）。</summary>
        public SystemCenterTone Tone = SystemCenterTone.Flat;

        /// <summary>false = 该指标尚无数据源，UI 画「—」占位（不伪造数值）。</summary>
        public bool HasValue;

        /// <summary>
        /// 进度条占比 0..1（构建期已 clamp）。&lt; 0 = 不画条（计数/文本类指标）。
        /// UI 只按它画条，不重算 —— 阈值判定集中在 Read Model / Provider（快照即真相）。
        /// </summary>
        public float Share01 = -1f;

        /// <summary>悬停提示（构建期拼好，绘制期零分配）——用于卡片放不下时的完整标签 + 附注。</summary>
        public string TooltipText = string.Empty;
    }

    /// <summary>
    /// 中心接入状态（三态）。
    ///
    /// <para>判据来自运行期事实：Def 的预留位 + 注册表里有没有该中心的数据 Provider。
    /// <b>不写进 Def、不进存档</b> —— Def 只回答「该中心该不该有玩法」，
    /// 「数据源有没有绑上来」只有注册表知道（GOV-003 / LAND-009 观测面诚实）。</para>
    ///
    /// <para>UI 只做「状态 → 配色」映射，不参与判定（与 <see cref="SystemCenterTone"/> 同规）。</para>
    /// </summary>
    public enum SystemCenterWiring
    {
        /// <summary>非预留 + 无数据 Provider：模块存在但尚未接入中心框架（合法状态，不是缺陷）。<b>默认值</b>——漏赋值时宁可说未接入，也不凭空宣称在跑。</summary>
        NotWired = 0,

        /// <summary>非预留 + 已有数据 Provider：正常运行。</summary>
        Running = 1,

        /// <summary>Def 声明为预留（玩法未落地）：只登记 ID 与档位。</summary>
        Reserved = 2
    }

    /// <summary>
    /// 中心卡片（总览网格用）。标签、状态、摘要均已本地化。
    /// </summary>
    public sealed class SystemCenterCardView
    {
        /// <summary>稳定 ID（<see cref="Domain.SystemCenterDef.defName"/>），导航与 Provider 绑定用。</summary>
        public string DefName = string.Empty;

        public string Label = string.Empty;
        public string NameEn = string.Empty;
        public int Tier;

        /// <summary>中心自身版本（<c>SystemCenterDef.centerVersion</c>），纯展示。</summary>
        public string CenterVersion = string.Empty;

        /// <summary>状态文案（运行中 / 数据未接入 / 预留），已本地化；由 <see cref="Wiring"/> 决定。</summary>
        public string StatusText = string.Empty;

        /// <summary>预留中心（Def 声明）：注册表按它分区，KPI 与业务视图一律不接数据源。</summary>
        public bool Reserved;

        /// <summary>
        /// 接入状态（运行期推导，见 <c>SystemCenterReadModelProvider.ResolveWiring</c>）：
        /// 状态徽标 / chip 的文案与配色都由它决定，UI 不参与判定。
        /// </summary>
        public SystemCenterWiring Wiring = SystemCenterWiring.NotWired;

        /// <summary>负责职责拼接文案。</summary>
        public string OwnText = string.Empty;

        /// <summary>依赖摘要文案（已本地化；无依赖时为「无依赖」）。</summary>
        public string DependsText = string.Empty;

        /// <summary>首项 KPI 摘要（卡片右上角展示；无数据时为空）。</summary>
        public string HeadKpiLabel = string.Empty;
        public string HeadKpiValue = string.Empty;
        public SystemCenterTone HeadKpiTone = SystemCenterTone.Flat;

        /// <summary>
        /// 卡片悬停提示（构建期拼好）。放在快照里而不是绘制期拼串，
        /// 保证绘制路径零分配（PERF）。
        /// </summary>
        public string TooltipText = string.Empty;
    }

    /// <summary>
    /// 注册表页快照：中心分区卡片（初始 / 预留）。异常隔离：构建失败时
    /// <see cref="ArchiveSectionSnapshot.IsEmpty"/> 置 true，UI 走空态。
    ///
    /// <para>注册表页是<b>路由入口</b>，只承载中心清单；殖民地级指标不属于总控中心，
    /// 已移除（各中心在自己的详情页给本领域指标）。</para>
    /// </summary>
    public sealed class SystemCenterListSnapshot : ArchiveSectionSnapshot
    {
        public List<SystemCenterCardView> InitialCenters = new List<SystemCenterCardView>();
        public List<SystemCenterCardView> ReservedCenters = new List<SystemCenterCardView>();
    }

    /// <summary>
    /// 中心详情页快照。契约行（事件/依赖/能力/钩子）为稳定 ID 原样，
    /// 职责条目为 Def 描述文本（非逻辑键）。
    /// </summary>
    public sealed class SystemCenterDetailSnapshot : ArchiveSectionSnapshot
    {
        public SystemCenterCardView Card = new SystemCenterCardView();

        /// <summary>
        /// 是否展示中心清单章节（职责边界 / 事件契约 / 依赖与能力）。
        /// 由 Provider 声明（<see cref="SystemCenters.ISystemCenterDataProvider.ShowsCenterManifest"/>）。
        /// </summary>
        public bool ShowManifest = true;

        /// <summary>
        /// 业务视图：车间俯视（② 主视图）。**非 null 即渲染** —— 结构由数据声明，
        /// UI 不做任何中心名判断（LAND-010）。
        /// </summary>
        public WorkshopFloorSnapshot Workshop;

        /// <summary>
        /// 业务视图：分析带（③ 瓶颈 / 六大损失 / 趋势 / 在制）。**非 null 即渲染**（同上）。
        /// </summary>
        public ManufacturingAnalysisSnapshot Analysis;

        /// <summary>
        /// 业务视图：运营（④ 在岗殖民者 / 成品库 / Andon 占位）。**非 null 即渲染**（同上）。
        /// </summary>
        public ManufacturingOperationsSnapshot Operations;

        /// <summary>
        /// 业务视图：事件与设备健康（⑤ 事件流 / TPM）。**非 null 即渲染**（同上）。
        /// </summary>
        public ManufacturingEventsSnapshot Events;

        /// <summary>
        /// 业务视图：数据层契约（⑦ Def 注册表 / Runtime 快照）。**非 null 即渲染**（同上）。
        /// </summary>
        public ManufacturingDataLayerSnapshot DataLayer;

        /// <summary>
        /// ② 的 <c>#rightcol</c>：生产队列 + 原料仓（**非 null 即渲染**，同上）。
        /// </summary>
        public ManufacturingStockQueueSnapshot StockQueue;

        /// <summary>
        /// ② 的 <c>#cmdbar</c>：产线汇总条 + 视图体检（**非 null 即渲染**，同上）。
        /// </summary>
        public ManufacturingCommandBarSnapshot CommandBar;

        /// <summary>
        /// ⑥ 数据桥（6 面板：3 真接 + 3 占位）。**非 null 即渲染**（同上）。
        /// </summary>
        public ManufacturingBridgeSnapshot Bridge;

        /// <summary>
        /// 页面外壳（顶栏品牌/时钟/电网 + 底栏广播）。**非 null 即渲染**（同上）。
        /// </summary>
        public ManufacturingChromeSnapshot Chrome;

        /// <summary>KPI 区块标题（已本地化；Provider 未指定时为默认「KPI 指标」）。</summary>
        public string KpiSectionTitle = string.Empty;

        /// <summary>
        /// KPI 卡的分行分组（如 <c>[4, 5]</c>）；空 = 按可用宽度流式排布。
        /// UI 据此复现预览「4fr : 5fr」的成组比例，不做任何中心特判。
        /// </summary>
        public List<int> KpiRowSizes = new List<int>();

        public List<SystemCenterKpiView> Kpis = new List<SystemCenterKpiView>();
        public List<string> Responsible = new List<string>();
        public List<string> Delegated = new List<string>();
        public List<string> Emits = new List<string>();
        public List<string> Consumes = new List<string>();
        public List<string> DependsOn = new List<string>();
        public List<string> Provides = new List<string>();
        public List<string> Hooks = new List<string>();

        /// <summary>页脚提示（预留 / 未接入说明），可为空。</summary>
        public string NoticeText = string.Empty;

        /// <summary>
        /// 业务视图：**模块主界面承载**（人员系统中心的详情页承载殖民地档案馆主界面本体）。
        /// **非 null 即渲染** —— 与 <see cref="Workshop"/> 同规，UI 不做任何中心名判断（LAND-010）。
        ///
        /// <para>与其它业务视图的差别：其它视图给的是「要画什么」（图形数据），本字段给的是
        /// 「能画多大 + 能不能画」（空间预算与可用性），界面本体由拥有该界面的模块自己绘制。</para>
        /// </summary>
        public ArchiveViewSnapshot ArchiveView;

        /// <summary>
        /// 业务视图：**人员概览**（预览 <c>#v-overview</c> 的殖民地级人事聚合）。
        /// **非 null 即渲染** —— 与 <see cref="ArchiveView"/> 同规，UI 不做中心名判断（LAND-010）。
        ///
        /// <para>由 KPI Provider 兼实现（<c>provider as IPopulationOverviewProvider</c>，
        /// 与制造中心的 <c>IManufacturing*DataProvider</c> 同形），不新增注册表字典：
        /// 「有没有指标」与「有没有聚合面板」对人员中心是同一个数据源。</para>
        /// </summary>
        public PopulationOverviewSnapshot PopulationOverview;

        /// <summary>
        /// 业务视图：**殖民地经济中心整页**（`CHG-LCE-001` 重建期最小骨架）。
        /// **非 null 即渲染** —— 与其它业务视图同规，UI 不做任何中心名判断（LAND-010）。
        ///
        /// <para>⚠ 2026-09-20：原五块经济快照（EconomyMarket / EconomyFlow / EconomyFactions /
        /// EconomySociety / EconomyFactionDetail）随 MVS 市场体系整体删除，由本字段接替
        /// （类型已换、消费面已换，不做兼容保留 —— UI 读模型死字段删除优于留空注释）。</para>
        /// </summary>
        public ColonyEconomySnapshot ColonyEconomy;

        /// <summary>
        /// 经济中心「库存 K 线 + 热力图」整页快照（`SDD-LCE-003` 阶段 3 落地）。
        /// null = 该中心未提供库存画廊 ⇒ UI 回退 KPI/骨架页（LAND-010：只看数据）。
        /// </summary>
        public ColonyStockGallerySnapshot ColonyStockGallery;

    }
    /// <summary>
    /// 「模块主界面承载」快照：可用性 + **空间预算** + 区块文案。
    ///
    /// <para>空间预算放在数据侧而不是 UI 侧，是 LAND-006/011 的要求：界面尺寸必须有单一来源
    /// （= 被承载界面的设计基准），UI 只按它分档，不得在绘制路径里写死像素或按中心名特判。</para>
    /// </summary>
    public sealed class ArchiveViewSnapshot : ArchiveSectionSnapshot
    {
        /// <summary>被承载界面的设计基准宽（= 其原版标签页 <c>RequestedTabSize.x</c>）。</summary>
        public float DesignWidth;

        /// <summary>被承载界面的设计基准高（= 其原版标签页 <c>RequestedTabSize.y</c>）。</summary>
        public float DesignHeight;

        /// <summary>低于此宽度则外壳降级为「顶部导航 + 内容吃满」（LAND-006 降级档 1）。</summary>
        public float CompactWidth;

        /// <summary>绝对最小可渲染宽度；不足则如实提示，不硬挤、不重叠（LAND-006 降级档 3）。</summary>
        public float MinWidth;

        /// <summary>绝对最小可渲染高度。</summary>
        public float MinHeight;

        /// <summary>区块标题（已本地化）。</summary>
        public string TitleText = string.Empty;

        /// <summary>区块副标题（已本地化，含档案规模摘要）。</summary>
        public string SubtitleText = string.Empty;
    }
}

using System;
using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Manufacturing;
using PersonalChronicle.Manufacturing.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// Partial of <see cref="MasterControlTabWindow"/> ——中心详情视图。
    ///
    /// <para>严格按《系统中心标准化规范书》§4 模板的顺序渲染：中心标识 → KPI →
    /// 职责边界 → 事件契约 → 依赖与能力。顺序即契约，不因数据有无而重排。</para>
    /// </summary>
    public sealed partial class MasterControlTabWindow
    {
        private const float SubListCaptionHeight = 24f;
        private const float SubListGap = 8f;
        private const float ChipRowHeight = 30f;
        /// <summary>业务视图标题与画布之间的呼吸间距（= 预览行间距 10，见 SectionGap 注释）。</summary>
        private const float WorkshopTitleGap = 10f;
        /// <summary>
        /// 车间俯视工具条高（三段式 `.ptitle`：主标题 + stencil + 右对齐副标题）。
        /// <para>预览 <c>#floorwrap .ptitle{padding:5px 13px;font-size:12px}</c>
        /// + <c>.sub{margin-left:auto}</c> ⇒ 内容行 ≈16，加上下各 5 ⇒ <b>26</b>。</para>
        /// </summary>
        private const float WorkshopToolbarHeight = 26f;
        /// <summary>预览 <c>#floorwrap .ptitle{padding:… 13px}</c>——左右内边距。</summary>
        private const float WorkshopTitlePadX = 13f;

        /// <summary>
        /// KPI 卡几何（对齐预览 <c>#kpi</c> 的紧凑横排 · LAND-001 第三次实测）
        ///
        /// <para>最终生效值（HTML 561-563，覆盖首版 70/78 的 `10px 16px`/`10px 12px`）：
        /// <c>#kpi #oee,#kpi #gauges{gap:10px}</c>
        /// <c>.oee-c{padding:5px 11px;gap:10px}</c>
        /// <c>.oee-c .big{font-size:18px}</c>
        /// <c>.oee-c .t{font-size:9.5px;line-height:1.35}</c>
        /// <c>.gauge{padding:5px 10px;gap:9px}</c>
        /// <c>.gauge .num{font-size:16px}</c>
        /// ⇒ **实测卡高 63.25px**（无头浏览器 `getBoundingClientRect`）：
        /// `padding 5 + 文本块 51.25 + padding 5`；文本块高由 `.oee-c .t{font-size:9.5px;line-height:1.35}`
        /// 的**两行**撑开（≈51.25），不是数值行撑开 ⇒ 文本块是高度决定项。</para>
        ///
        /// 宽档卡内布局 = [数值] [标签 / 附注] [进度条]，与预览一致；窗口只按快照取值，不重算比例。
        /// </summary>
        private const float KpiCardHeight = 34f;
        private const float KpiCardGap = 10f;
        private const float KpiBarHeight = 8f;

        /// <summary>窄档卡（一行九卡等 1:1 密度场景）：只留数值 + 标签，进度条压成底部细条。</summary>
        private const float CompactKpiCardHeight = 30f;
        private const float CompactKpiBarHeight = 3f;

        /// <summary>
        /// 单卡最小可读宽度（横排卡下限：内边距 16 + 数值 32 + 间隔 + 标签区 ≈60 + 进度条 20）。
        /// 可用宽度不足时该组自动换行，而不是把标签压成两个字（小屏保护）。
        /// </summary>
        private const float MinKpiCardWidth = 136f;

        /// <summary>
        /// 低于此卡宽则整个 KPI 面板切窄档：宽档要放下「标签 + 附注」两行，
        /// 卡太窄时附注必然被截，不如让数值和标签各得其所（附注转悬停提示）。
        /// </summary>
        private const float CompactKpiCardWidth = 160f;

        /// <summary>
        /// 内容区**页头块高** —— <b>已移除</b>（用户需求「移除大标题标签内容腾出空间」，2026-09-16）。
        ///
        /// <para>旧内容区页头块 = 面包屑行 <c>20</c> + <see cref="PageHeaderBlockHeight"/>（大标题
        /// <see cref="PageTitleHeight"/> 30 + api 行 <see cref="PageLineHeight"/> 22 + 说明条
        /// <see cref="NoticeBoxHeight"/> 44 + 尾隙 12 = <b>108</b>）⇒ 合计 <b>128</b>。
        /// 现整块不再绘制，显式记 <b>0</b>：这是**审计锚点**，杜绝「仍按旧公式计高」的幽灵高度
        /// （LAND-002：计高必须等于绘制）。</para>
        /// </summary>
        private const float DetailPageHeaderBlockHeight = 0f;

        /// <summary>
        /// 内容区顶部 **4 视图 tab 行** 的占高 —— <b>2026-09-16 起通常为 0</b>（用户需求「这一排 tab
        /// 的空间占位太大，建议嵌入式」）。
        ///
        /// <para><b>归零理由</b>：4 枚视图 tab 已**嵌入窗口页头工具条**（标题右侧 / 按钮组左侧，
        /// 见 <c>Shell.cs</c> → <c>DrawHeaderPopulationViewTabs</c>）⇒ 内容区不再有 tab 行，占高恒 0。
        /// 旧值 = 条本体 <see cref="PopulationViewTabHeight"/>（26）+ 2 尾隙 = <b>28</b>。</para>
        ///
        /// <para><b>为什么是属性而非常量（同源铁律 LAND-002）</b>：唯一例外是页头**宽度预算连「首字档」
        /// 都放不下**时的兜底 —— 那时 tab 行回落内容区独立行，占高恢复 28。该判定与绘制**同源**：
        /// 本属性、<see cref="ComputeDetailHeight"/>（计高）与 <see cref="DrawDetailContent"/>（绘制）
        /// 都只读同一个标志 <c>PopulationViewTabsInContentArea</c>（由页头那一次布局决策写入）
        /// ⇒ 计高 ≡ 绘制，杜绝「估算多/少一截 ⇒ 滚动条漂移」。</para>
        ///
        /// <para>归零后与 <see cref="DetailPageHeaderBlockHeight"/> 同法保留**符号本身**作为审计锚点。
        /// 累计（相对最初 128px 的页头块）= 128px 全部腾出。</para>
        /// </summary>
        private float DetailHeaderTabsHeight
        {
            get { return PopulationViewTabsInContentArea ? PopulationViewTabHeight + 2f : 0f; }
        }

        /// <summary>中心详情内容高（绘制与视口共用同一组常量）。</summary>
        private float ComputeDetailHeight(float width)
        {
            SystemCenterDetailSnapshot detail = cachedDetail;
            int kpiCount = detail != null ? detail.Kpis.Count : 0;
            int ownRows = Mathf.Max(1, detail != null ? detail.Responsible.Count : 0);
            int delegatedRows = Mathf.Max(1, detail != null ? detail.Delegated.Count : 0);
            bool showManifest = detail == null || detail.ShowManifest;

            // 4 视图 tab 的可用性（概览 / 档案任一非空）—— 与 DrawDetailContent 的
            // headerHasPopulation / headerHasArchive 同源（LAND-010：判据是数据不是中心名）。
            PopulationOverviewSnapshot population = detail != null ? PopulationForDisplay(detail) : null;
            ArchiveViewSnapshot archiveView = detail != null ? ArchiveViewForDisplay(detail) : null;
            bool hasPopulation = population != null && !population.IsEmpty;
            bool hasArchive = archiveView != null && !archiveView.IsEmpty;

            float h = 4f;
            // 顶栏（预览 #top）：只在该中心带业务视图（Chrome 快照）时才有。
            if (detail != null && detail.Chrome != null && !detail.Chrome.IsEmpty)
            {
                h += ChromeBarHeight + WorkshopTitleGap;
            }
            // 【布局优化 2026-09-16】内容区页头块（面包屑 + 大标题 + api 行 + 说明条）已整块移除 ⇒
            //   旧贡献 = 20(面包屑) + PageHeaderBlockHeight()(= 30+22+44+12 = 108) = 128；
            //   同日第二刀把 4 视图 tab 条也**嵌入窗口页头** ⇒ 内容区连那一行也不再占位（再省 28）。
            h += DetailPageHeaderBlockHeight;                 // = 0（原 :84 面包屑 20f + :87 页头块，一并归零）
            // 该符号**内部**已含「兜底档才非 0」的判定 ⇒ 这里不再另加条件，避免出现第二处判据（LAND-002）。
            h += DetailHeaderTabsHeight;                      // 恒 0；仅页头预算不足的兜底档回到 28
            if (showManifest)
            {
                h += ChipRowHeight;       // 标识 chips（清单页才需要：KPI 专页靠标题 + 副标题定位）
            }
            // D1：`#sop>h3.sec{display:none}` ⇒ 无分区标题行；
            // D14：`.fpanes{max-height:min(24vh,214px)}` ⇒ tab 内容区有高度上限（区内滚动）。
            // 六个 tab 的面板组共用这一个高度项（区高 = min(内容高, 上限)），不再逐 tab 叠加。
            h += TabPaneAreaHeight(width) + SectionGap;
            if (showManifest)
            {
                h += UITheme.SectionTitleHeight;                                    // 职责边界
                h += SubListCaptionHeight + ownRows * LineRowHeight + SubListGap;   // 负责
                h += SubListCaptionHeight + delegatedRows * LineRowHeight;          // 委托
                h += SectionGap;
                h += UITheme.SectionTitleHeight + 2f * LineRowHeight + SectionGap;  // 事件契约
                h += UITheme.SectionTitleHeight + 3f * LineRowHeight + SectionGap;  // 依赖与能力
            }
            // 业务视图：车间俯视（② 主视图）—— 标题 + 等比画布（+ 空态提示）
            WorkshopFloorSnapshot workshop = detail != null ? detail.Workshop : null;
            if (workshop != null && !workshop.NoMap)
            {
                h += WorkshopToolbarHeight + WorkshopTitleGap
                    + WorkshopFloorPlan.PlanHeight(width, floorView.Zoom) + SectionGap
                    // hazard 斜纹条（预览 .hazard）。
                    // ⚠ 2026-10-06（用户指令）：底栏 Tab 栏整行已移除 ⇒ 原
                    //   `+ AppTabBarHeight + AppTabBarBottomPad` 两项**去项**（app 路径的
                    //   `DrawAppFloorPanel.fixedRows` 同步去掉）。计高与绘制必须同源，
                    //   否则滚动高漂移（见本文件顶部「计高≡绘制」约定）。
                    + HazardStripHeight;
                if (detail.CommandBar != null && !detail.CommandBar.IsEmpty)
                {
                    h += ManufacturingCommandBar.StripHeight + WorkshopTitleGap;
                }
                if (!string.IsNullOrEmpty(workshop.Hint))
                {
                    h += NoticeBoxHeight + SectionGap;
                }
            }
            // 业务视图：人员中心 = **4 视图 tab**（概览 / 档案总览 / 人物档案 / 分档与契约），
            // 与预览 #navseg 一一对应。**只有当前 tab 的块计入高度**（旧实现是同屏全塞，
            // 导致「概览里混着档案馆承载」= 用户报的"多出旧内容"）。
            // population / archiveView / hasPopulation / hasArchive 已在本方法顶部计算（供 tab 条计高）。
            if (hasPopulation || hasArchive)
            {
                // 视图 tab 条已**嵌入窗口页头**（`Shell.cs` → DrawHeaderPopulationViewTabs），不再占内容区；
                // 这里只累加**当前视图块**的高度。
                switch (populationView)
                {
                    case 1:
                        if (hasArchive)
                        {
                            h += ArchiveBlockHeight(archiveView, width);
                        }
                        break;
                    case 2:
                        if (hasArchive)
                        {
                            // 人物档案 = 承载区的 PawnDetail 视图 + 进入方式提示行（高度同源）。
                            h += PopulationViewHintHeight + ArchiveBlockHeight(archiveView, width);
                        }
                        break;
                    case 3:
                        h += ContractBlockHeight(detail);
                        break;
                    default:
                        if (hasPopulation)
                        {
                            // 高度与绘制共用 PopulationBlockHeight，杜绝「估算与绘制两处不同源」的滚动异常。
                            h += PopulationBlockHeight(width, population);
                        }
                        break;
                }
            }
            // 【经济中心】整页走 `DrawColonyEconomyPage`（殖民地经济重建期骨架 + 一屏适配），
            // **不参与外层滚动** ⇒ 此处不累加该页的任何 BlockHeight（计高与绘制必须同源，LAND-002）。
            // ② 的 #rightcol（生产队列 + 原料仓）—— 属于 ②，无独立区块标题
            // D14：③~⑦ 的面板组已统一由上方 `TabPaneAreaHeight(width)` 计入（区内滚动），
            // 这里不再逐 tab 叠加，否则高度会与绘制路径漂移（两处不同源的经典 bug）。
            h += NoticeBoxHeight + 40f;                                        // 提示 + 底部留白
            return h;
        }

        /// <summary>
        /// tab 内容区的**占用高度**：<c>min(当前 tab 内容高, 上限)</c>。
        /// 上限 = <c>min(214px, 屏幕高 × 24%)</c>（预览 <c>.fpanes{max-height:min(24vh,214px)}</c>）。
        /// 无内容时返回 0（该 tab 不占位）。
        /// </summary>
        private float TabPaneAreaHeight(float width)
        {
            SystemCenterDetailSnapshot detail = cachedDetail;
            float contentWidth = Mathf.Max(20f, width - UITheme.ScrollbarThickness);
            float content = TabPaneContentHeight(contentWidth, detail);
            if (content <= 0.5f)
            {
                return 0f;
            }
            float cap = Mathf.Min(214f, Screen.height * 0.24f);
            return Mathf.Min(content, cap);
        }

        private void DrawDetailContent(Rect inner)
        {
            SystemCenterDetailSnapshot detail = cachedDetail;
            if (detail == null || detail.IsEmpty)
            {
                UIComponents.Label(new Rect(inner.x, inner.y + 10f, inner.width, 24f),
                    Translate("PersonalChronicle.UI.MasterControl.Detail.Missing"),
                    GameFont.Small, UITheme.Dim);
                return;
            }

            // 业务视图专页（cockpit 型）：整页适配一屏的 app shell（无外层滚动）——
            // 预览「整页适配一屏（无滚动）」CSS 的等价实现。
            if (UsesAppShell(detail))
            {
                DrawDetailAppShell(inner, detail);
                return;
            }
            if (UsesColonyEconomyPage(detail))
            {
                DrawColonyEconomyPage(inner, detail);
                return;
            }

            // 平面图高度不再依赖"可见区高"：预览里它是流式等比元素（svg width:100%;height:auto），
            // 高度只由宽度与缩放决定（PlanHeight），超出首屏的部分交给页面滚动（与浏览器一致）。
            Rect viewRect;
            BeginContentScroll(inner,
                ComputeDetailHeight(inner.width - UITheme.ScrollbarThickness),
                out viewRect);
            try
            {
                SystemCenterCardView card = detail.Card;
                float y = viewRect.y + 4f;

                // 顶栏（预览 #top）：品牌 + 游戏时钟 + 电网负载；中心带业务视图时才有。
                ManufacturingChromeSnapshot chrome = detail.Chrome;
                if (chrome != null && !chrome.IsEmpty)
                {
                    y = DrawChromeBar(viewRect, y, chrome);
                    y += WorkshopTitleGap;
                }

                bool showManifest = detail.ShowManifest;
                // 【布局优化 2026-09-16 · 用户需求「移除大标题标签内容腾出空间」+「这一排 tab 建议嵌入式」】：
                //   内容区页头块（面包屑 + 大标题 + api 行 + 说明条）已**整块移除**（旧 = 128）；
                //   4 视图 tab 条也已**嵌入窗口页头**（`Shell.cs` → DrawHeaderPopulationViewTabs）
                //   ⇒ 内容区顶部**没有任何 tab 行**。这里只在**兜底档**（页头预算连首字都放不下）才
                //   回到旧版式的内容区独立行 —— 判据与计高同源（同一个 PopulationViewTabsInContentArea 标志）。
                PopulationOverviewSnapshot headerPopulation = PopulationForDisplay(detail);
                bool headerHasPopulation = headerPopulation != null && !headerPopulation.IsEmpty;
                ArchiveViewSnapshot headerArchive = ArchiveViewForDisplay(detail);
                bool headerHasArchive = headerArchive != null && !headerArchive.IsEmpty;
                if (PopulationViewTabsInContentArea && (headerHasPopulation || headerHasArchive))
                {
                    DrawPopulationViewTabs(viewRect, y, headerHasPopulation, headerHasArchive);
                    // 与 ComputeDetailHeight 消费**同一符号**推进 y（LAND-002：计高 ≡ 绘制）。
                    y += DetailHeaderTabsHeight;
                }
                if (showManifest)
                {
                    y = DrawChipRow(viewRect, y, card);
                    y += SectionGap;
                }

                // ① 总控 KPI 已移入 ② 面板的「总控」tab 内容区（预览 #kpi 是 tab 页的一部分，
                // 不在平面图之上）——见本方法后半段的 tab 内容分发。

                // 业务视图：车间俯视（② 主视图）。数据非空才画；框架本身与数据无关（LAND-009：
                // 没划车间也照画框架 + 一行提示，而不是白屏）。
                WorkshopFloorSnapshot workshop = detail.Workshop;
                if (workshop != null && !workshop.NoMap)
                {
                    y = DrawWorkshopToolbar(viewRect, y, workshop);
                    // ② 的 #cmdbar：产线汇总条 + 视图体检行（预览里在平面图上方、标题之下）
                    if (detail.CommandBar != null && !detail.CommandBar.IsEmpty)
                    {
                        y += ManufacturingCommandBar.Draw(viewRect, y, detail.CommandBar);
                        y += WorkshopTitleGap;
                    }
                    int clickedDevice;
                    y += WorkshopFloorPlan.Draw(
                        new Rect(viewRect.x, y, viewRect.width, 1f), workshop,
                        Time.realtimeSinceStartup, ref floorView, out clickedDevice);
                    // D20：预览 selMachine() → renderDeviceDetail() + openModal()。
                    // 点中设备即弹**居中**详情窗（数据走 Read Model 契约，UI 不重算指标）。
                    if (clickedDevice >= 0 && workshop.Devices != null
                        && clickedDevice < workshop.Devices.Count)
                    {
                        FloorDeviceView picked = workshop.Devices[clickedDevice];
                        OpenDeviceDetail(picked);
                    }
                    y += SectionGap;
                    if (!string.IsNullOrEmpty(workshop.Hint))
                    {
                        y = DrawNoticeBox(viewRect, y, workshop.Hint);
                        y += SectionGap;
                    }
                }

                // ===== tab 内容区（预览 buildFloorTabs() 的 pane：平面图之后、tab 栏之前）=====
                //
                // 【LAND-001 实测 · D1】预览最终版 `#sop>h3.sec{display:none}`（HTML 558）——
                // ①~⑦ 的**分区标题行在最终布局里完全不显示**（每个 tab 按钮已表达主题）。
                // 因此这里不再绘制 DrawPlainTitle / DrawSectionHeader 的区块标题，
                // 面板直接排在 pane 栅格里。分区标题函数保留（`.ptitle` 类面板标题仍在用）。
                //
                // 【LAND-001 实测 · D14】预览 `.fpanes{max-height:min(24vh,214px);overflow-y:auto}`
                // （HTML 571）——tab 内容区**有高度上限且内部滚动**，这是「整页一屏不滚动」的关键：
                // 面板无限生长会让页面被撑长，与预览版式不符。这里按窗口可见高的 24% 与 214px 取小，
                // 内容超出则在区内滚动（区外仍是页面的主滚动）。
                // ⚠ CHG-20261006-TABUNIFY（3b 钳位）：降级路径**不提供**第 7 页签「编排」——
                //   app 路径为画布专禁页内滚动（见 AppShell.DrawActivePane 的 scroll 判定），
                //   而本路径整体是滚动长页，在滚动容器里画布命中会错位 ⇒ 本路径不画它。
                //   若玩家先在 app 路径选了「编排」、再因 Chrome 快照为空 / 车间未划而落入降级态，
                //   `selectedFloorTab` 会**残留 6**（全工程无其它重置点）⇒ 内容区落 default 返回 0
                //   = 空白页签且无提示。此处做**单点钳位**收口（先例：本文件下方 populationView 钳位）。
                if (selectedFloorTab > TabData)
                {
                    selectedFloorTab = TabData;
                    contentScroll = Vector2.zero;
                }
                y = DrawTabPaneArea(viewRect, y, detail);
                y += SectionGap;

                if (showManifest)
                {
                    y = DrawSectionHeader(viewRect, y,
                        Translate("PersonalChronicle.UI.MasterControl.Section.Responsibility"));
                    y = DrawSubList(viewRect, y,
                        Translate("PersonalChronicle.UI.MasterControl.Sub.Responsible"),
                        detail.Responsible,
                        Translate("PersonalChronicle.UI.MasterControl.Tag.Own"), UITheme.Accent);
                    y += SubListGap;
                    y = DrawSubList(viewRect, y,
                        Translate("PersonalChronicle.UI.MasterControl.Sub.Delegated"),
                        detail.Delegated,
                        Translate("PersonalChronicle.UI.MasterControl.Tag.Delegated"), UITheme.Info);
                    y += SectionGap;

                    y = DrawSectionHeader(viewRect, y,
                        Translate("PersonalChronicle.UI.MasterControl.Section.EventContract"));
                    y = DrawTagLine(viewRect, y,
                        Translate("PersonalChronicle.UI.MasterControl.Row.Emit"), detail.Emits);
                    y = DrawTagLine(viewRect, y,
                        Translate("PersonalChronicle.UI.MasterControl.Row.Consume"), detail.Consumes);
                    y += SectionGap;

                    y = DrawSectionHeader(viewRect, y,
                        Translate("PersonalChronicle.UI.MasterControl.Section.Dependency"));
                    y = DrawTagLine(viewRect, y,
                        Translate("PersonalChronicle.UI.MasterControl.Row.DependsOn"), detail.DependsOn);
                    y = DrawTagLine(viewRect, y,
                        Translate("PersonalChronicle.UI.MasterControl.Row.Provides"), detail.Provides);
                    y = DrawTagLine(viewRect, y,
                        Translate("PersonalChronicle.UI.MasterControl.Row.Hooks"), detail.Hooks);
                    y += SectionGap;
                }

                // ===== ② 面板底部 hazard 斜纹条（预览顺序：panes → .hazard）=====
                // ⚠ 2026-10-06（用户指令）：底栏 Tab 栏整行已移除 —— app 路径与本路径**同源删除**
                //   （本路径原先只画前 DegradedVisibleTabCount 个页签的能力裁剪逻辑一并移除）。
                if (workshop != null && !workshop.NoMap)
                {
                    DrawFloorHazardStrip(new Rect(viewRect.x, y, viewRect.width, HazardStripHeight));
                    y += HazardStripHeight + SectionGap;
                }

                // 业务视图：人员中心 = **4 视图 tab**（与预览 #navseg 同构）。
                // 旧实现把概览 + 档案馆承载同屏全塞（用户报「多出额外/旧内容」）——
                // 现在同一时刻只画当前 tab 的内容：概览 / 档案总览 / 人物档案 / 分档与契约。
                // 单一取值点：dev 预览开关打开时返回测试快照（`PopulationViews.cs`）。
                PopulationOverviewSnapshot population = PopulationForDisplay(detail);
                ArchiveViewSnapshot archiveView = ArchiveViewForDisplay(detail);
                bool hasPopulation = population != null && !population.IsEmpty;
                bool hasArchive = archiveView != null && !archiveView.IsEmpty;
                if (hasPopulation || hasArchive)
                {
                    // 视图 tab 条在**窗口页头**（`Shell.cs` → DrawHeaderPopulationViewTabs）；内容区只画当前视图。
                    int current = Mathf.Clamp(populationView, 0, PopulationViewCount - 1);
                    if (current == 1 || current == 2)
                    {
                        // 档案总览 / 人物档案：都由承载区渲染 —— 人物档案走嵌入档的
                        // PawnDetail 视图（从概览或档案表点人进入），不在总控里重写档案。
                        if (hasArchive)
                        {
                            // 【2026-09-16 实机反馈删除】此处原有一行「进入方式提示」
                            //   （`…PopulationView.PawnHint`），两个问题：
                            //   ① 文案过期 —— 写的是「在**概览**或档案总览点选一名成员」，而概览的
                            //      人员名册面板早已整体删除（CHG-20260914-002）⇒ 前半句不成立；
                            //   ② 与承载区空态**同屏重复** —— 未选中时本行与 `DrawEmbeddedPawnEmpty`
                            //      上下叠着两段几乎相同的话。
                            //   现在承载区已改为「无选中即自动落点默认对象」⇒ 提示行不再需要。
                            // ⚠ 2026-09-15 落地：tab 2「人物档案」⇒ 承载区画**详情页**
                            // （预览 `#v-pawn`：Hero 带 + 5 子 Tab）；tab 1「档案总览」画分类浏览
                            // （预览 `#v-archive`）。此前两者共用同一渲染 ⇒ 两个 tab 内容相同。
                            y = DrawArchiveBlock(viewRect, y, archiveView, current == 2);
                        }
                    }
                    else if (current == 3)
                    {
                        y = DrawContractBlock(viewRect, y, detail);
                    }
                    else if (hasPopulation)
                    {
                        y = DrawPopulationBlock(viewRect, y, population);
                    }
                }

                // ===== 经济中心业务视图 =====
                // ⚠ 2026-09-20（CHG-LCE-001）：MVS 五面板已整体删除；殖民地经济中心改走
                // `DrawColonyEconomyPage`（重建期骨架，见 `UsesColonyEconomyPage` 分支与
                // `MasterControlTabWindow.Economy.cs`）。本处不再有经济块。

                // 通用「就绪 / 说明」条**只在没有业务视图时**画：它描述的是总控登记字段
                // （职责 / 事件契约 / 依赖清单）的归属，而人员中心等有业务视图的中心由
                // 「分档与契约」tab 承载同类信息 —— 预览里人员中心也没有这条说明
                // （用户实机报的「多出额外的内容」即含此项）。
                if (!hasPopulation && !hasArchive)
                {
                    DrawNoticeBox(viewRect, y, string.IsNullOrEmpty(detail.NoticeText)
                        ? Translate("PersonalChronicle.UI.MasterControl.Detail.Ready")
                        : detail.NoticeText);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>
        /// tab 内容区（预览 <c>.fpanes</c>）——按当前选中的 tab 绘制对应面板组，
        /// 并在**高度上限内滚动**（预览 <c>.fpanes{max-height:min(24vh,214px);overflow-y:auto}</c>）。
        ///
        /// <para>返回该区实际占用高度。上限 = <c>min(窗口可见高的 24%, 214px)</c>；
        /// 内容不超上限时区高 = 内容高（不会白占空间）。</para>
        /// </summary>
        private float DrawTabPaneArea(Rect rect, float y, SystemCenterDetailSnapshot detail)
        {
            float contentWidth = Mathf.Max(20f, rect.width - UITheme.ScrollbarThickness);
            float contentHeight = TabPaneContentHeight(contentWidth, detail);
            if (contentHeight <= 0.5f)
            {
                return y;   // 该 tab 无内容
            }

            float cap = Mathf.Min(214f, Screen.height * 0.24f);
            Rect area = new Rect(rect.x, y, rect.width, Mathf.Min(contentHeight, cap));
            // 预览 `.fpanes{background:var(--steel3);border-top:1px solid var(--amber)}`
            // 引擎无「比面板再亮一档」的语义槽时，用 OverlayWhite04 提亮作为等价表达。
            UIComponents.TintedBox(area, UITheme.OverlayWhite04);

            if (contentHeight <= cap + 0.5f)
            {
                // 不超上限：直接画，无滚动条（区高 = 内容高）
                DrawTabPaneContent(area, area.y, contentWidth, detail);
                return y + area.height;
            }

            // 超上限：区内滚动（预览的 overflow-y:auto）
            Rect viewRect = new Rect(area.x, area.y, contentWidth, contentHeight);
            Widgets.BeginScrollView(area, ref tabPaneScroll, viewRect);
            try
            {
                DrawTabPaneContent(new Rect(viewRect.x, viewRect.y, contentWidth, contentHeight),
                    viewRect.y, contentWidth, detail);
            }
            finally
            {
                Widgets.EndScrollView();
            }
            return y + area.height;
        }

        /// <summary>当前 tab 的面板组高度合计（与 <see cref="DrawTabPaneContent"/> 一一对应）。</summary>
        private float TabPaneContentHeight(float width, SystemCenterDetailSnapshot detail)
        {
            if (detail == null)
            {
                return 0f;
            }
            switch (selectedFloorTab)
            {
                case TabCockpit:
                    return detail.Kpis.Count > 0
                        ? KpiBlockHeight(width, detail.Kpis, detail.KpiRowSizes)
                        : 0f;
                case TabDispatch:
                    return detail.StockQueue != null && !detail.StockQueue.IsEmpty
                        ? ManufacturingStockQueuePanels.BlockHeight(width)
                        : 0f;
                case TabAnalysis:
                    return detail.Analysis != null && !detail.Analysis.IsEmpty
                        ? ManufacturingAnalysisPanels.BlockHeight(width)
                        : 0f;
                case TabOps:
                    return detail.Operations != null && !detail.Operations.IsEmpty
                        ? ManufacturingOperationsPanels.BlockHeight(width, detail.Operations)
                        : 0f;
                case TabEvent:
                    return detail.Events != null && !detail.Events.IsEmpty
                        ? ManufacturingEventsPanels.BlockHeight(width)
                        : 0f;
                case TabData:
                {
                    float h = 0f;
                    if (detail.DataLayer != null && !detail.DataLayer.IsEmpty)
                    {
                        h += ManufacturingDataLayerPanels.BlockHeight(width) + SectionGap;
                    }
                    if (detail.Bridge != null && !detail.Bridge.IsEmpty)
                    {
                        h += ManufacturingBridgePanels.BlockHeight(width);
                    }
                    return h;
                }
                default:
                    return 0f;
            }
        }

        // UI-P2（CHG-MFG-AUDIT-001）：面板级失败隔离 + 节流日志。
        // 绘制铁律：整体 try/catch 是反模式 ⇒ 每个面板独立隔离，单面板异常只降级为该面板占位，不拖垮整页。
        private static long lastDetailPanelFallbackTicks;
        private const float DetailPanelFallbackHeight = 64f;

        /// <summary>单面板绘制失败时的隔离占位：实色块 + 本地化提示 + 节流日志（5s）。</summary>
        private static float DrawDetailPanelFallback(Rect pane, float y, Exception ex)
        {
            Rect box = new Rect(pane.x, y, pane.width, DetailPanelFallbackHeight);
            Widgets.DrawBoxSolid(box, FloorPalette.HazardBase);
            UIComponents.Label(new Rect(box.x + 8f, box.y + 8f, box.width - 16f, 22f),
                Translate("PersonalChronicle.UI.MasterControl.Detail.PanelFailed"),
                GameFont.Small, UITheme.Threat);
            long now = DateTime.UtcNow.Ticks;
            if (now - lastDetailPanelFallbackTicks > TimeSpan.TicksPerSecond * 5L)
            {
                lastDetailPanelFallbackTicks = now;
                ChronicleLog.Error(ChronicleLog.Category.Ui, "detail panel draw failed (isolated): " + ex);
            }
            return y + DetailPanelFallbackHeight + SectionGap;
        }

        /// <summary>当前 tab 的面板组绘制（区高由 <see cref="TabPaneContentHeight"/> 决定）。</summary>
        private float DrawTabPaneContent(Rect rect, float y, float width, SystemCenterDetailSnapshot detail)
        {
            Rect pane = new Rect(rect.x, rect.y, width, rect.height);
            switch (selectedFloorTab)
            {
                case TabCockpit:
                    if (detail.Kpis.Count > 0)
                    {
                        try { y = DrawKpiGrid(pane, y, detail.Kpis, detail.KpiRowSizes); }
                        catch (Exception ex) { y = DrawDetailPanelFallback(pane, y, ex); }
                    }
                    return y;
                case TabDispatch:
                    if (detail.StockQueue != null && !detail.StockQueue.IsEmpty)
                    {
                        try { y += ManufacturingStockQueuePanels.Draw(pane, y, detail.StockQueue); }
                        catch (Exception ex) { y = DrawDetailPanelFallback(pane, y, ex); }
                    }
                    return y;
                case TabAnalysis:
                    if (detail.Analysis != null && !detail.Analysis.IsEmpty)
                    {
                        try { y += ManufacturingAnalysisPanels.Draw(pane, y, detail.Analysis); }
                        catch (Exception ex) { y = DrawDetailPanelFallback(pane, y, ex); }
                    }
                    return y;
                case TabOps:
                    if (detail.Operations != null && !detail.Operations.IsEmpty)
                    {
                        try { y += ManufacturingOperationsPanels.Draw(pane, y, detail.Operations); }
                        catch (Exception ex) { y = DrawDetailPanelFallback(pane, y, ex); }
                    }
                    return y;
                case TabEvent:
                    if (detail.Events != null && !detail.Events.IsEmpty)
                    {
                        try { y += ManufacturingEventsPanels.Draw(pane, y, detail.Events); }
                        catch (Exception ex) { y = DrawDetailPanelFallback(pane, y, ex); }
                    }
                    return y;
                case TabData:
                    if (detail.DataLayer != null && !detail.DataLayer.IsEmpty)
                    {
                        try { y += ManufacturingDataLayerPanels.Draw(pane, y, detail.DataLayer); }
                        catch (Exception ex) { y = DrawDetailPanelFallback(pane, y, ex); }
                        y += SectionGap;
                    }
                    if (detail.Bridge != null && !detail.Bridge.IsEmpty)
                    {
                        try { y += ManufacturingBridgePanels.Draw(pane, y, detail.Bridge); }
                        catch (Exception ex) { y = DrawDetailPanelFallback(pane, y, ex); }
                    }
                    return y;
                default:
                    return y;
            }
        }

        /// <summary>
        /// 标识行（稳定 ID · Tier · 版本）。
        ///
        /// <para><b>A2 修复（实机瑕疵）</b>：旧实现 `{0} · Tier {1} · v{2}` **无条件拼版本段**，
        /// 而 Def 未登记版本时 Provider 回落的哨兵是 <c>"-"</c> ⇒ 界面上出现 `v-`。
        /// 现：空或哨兵一律走不带版本段的键；判据取**原始 Def 数据**而非翻译文本（GOV-009）。</para>
        /// </summary>
        private static string BuildApiLine(SystemCenterCardView card)
        {
            // S5（2026-09-25 审计）：defName / Tier / 版本是**内部标识**，非开发模式下直显
            //   属调试串残留（实机截图右上角 "manufacturing_center · Tier 10 · v0.1.0-draft"）。
            //   仅在开发模式返回该行；否则返回空串，调用方跳过绘制（GOV-009 调试信息不外露）。
            if (!Prefs.DevMode)
            {
                return string.Empty;
            }
            string version = card.CenterVersion;
            if (string.IsNullOrEmpty(version) || version == "-")
            {
                return Translate2("PersonalChronicle.UI.MasterControl.Detail.ApiNoVersion",
                    card.DefName, card.Tier);
            }
            return Translate3("PersonalChronicle.UI.MasterControl.Detail.Api",
                card.DefName, card.Tier, version);
        }

        /// <summary>标识 chips 行：稳定 ID / 档位 / 状态。</summary>
        private static float DrawChipRow(Rect rect, float y, SystemCenterCardView card)
        {
            float x = rect.x;
            const float chipHeight = 22f;
            // S5 同上：稳定 ID（defName）是内部标识，仅开发模式显示。
            if (Prefs.DevMode)
            {
                x = DrawChip(x, y, chipHeight, card.DefName, UITheme.Info);
            }
            x = DrawChip(x, y, chipHeight,
                Translate("PersonalChronicle.UI.MasterControl.Chip.Tier", card.Tier), UITheme.Muted);
            DrawChip(x, y, chipHeight, card.StatusText, StatusColor(card.Wiring));
            return y + ChipRowHeight;
        }

        private static float DrawChip(float x, float y, float height, string label, Color color)
        {
            float width;
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            width = Mathf.Min(Text.CalcSize(label).x + 18f, 260f);
            Text.Font = prevFont;
            UIComponents.Badge(new Rect(x, y, width, height),
                UIComponents.TruncateToWidth(label, width - 6f, GameFont.Tiny), color);
            return x + width + 6f;
        }

        /// <summary>
        /// 单组 KPI 的实际列数：可用宽度能放下整组就整组一行（复现预览的一行成组），
        /// 放不下才按 <see cref="MinKpiCardWidth"/> 折行（小屏保护）。
        /// </summary>
        /// <summary>
        /// 车间俯视工具条：左标题 + 右缩放控件（`-` / 百分比 / `+` / 重置）。
        /// 滚轮缩放与拖拽平移在画布上直接生效（见 <see cref="WorkshopFloorPlan"/>）；
        /// 视图态是窗口瞬时字段，不入存档（GATE-UI）。
        /// </summary>
        private float DrawWorkshopToolbar(Rect rect, float y, WorkshopFloorSnapshot snapshot)
        {
            const float gap = 10f;
            // 预览 #floorwrap .ptitle{padding:5px 13px}——内容行上下各 5
            float rowY = y + 5f;
            float rowH = WorkshopToolbarHeight - 10f;
            Rect row = new Rect(rect.x + WorkshopTitlePadX, rowY,
                Mathf.Max(40f, rect.width - WorkshopTitlePadX * 2f), rowH);

            // 主标题 + stencil 副标题（对齐预览 .ptitle 的三段式；缩放控件已按预览移到画布右下覆盖条）
            string title = Translate("PersonalChronicle.UI.Floor.SectionTitle");
            float titleWidth = Mathf.Min(MeasureWidth(title, GameFont.Small), row.width * 0.42f);
            UIComponents.Label(new Rect(row.x, row.y, titleWidth, rowH),
                UIComponents.TruncateToWidth(title, titleWidth, GameFont.Small),
                GameFont.Small, UITheme.Accent);

            float x = row.x + titleWidth + gap;

            // 预览 .sub{margin-left:auto}——副标题恒靠右；先算右段宽度再决定 stencil 是否放得下
            string selected = SelectedFloorLabel(snapshot);
            string tailText = selected ?? Translate("PersonalChronicle.UI.Floor.PanelHint");
            float tailWidth = Mathf.Min(MeasureWidth(tailText, GameFont.Tiny), row.width * 0.4f);
            float tailX = row.xMax - tailWidth;
            if (tailWidth >= 40f)
            {
                UIComponents.Label(new Rect(tailX, row.y, tailWidth, rowH),
                    UIComponents.TruncateToWidth(tailText, tailWidth, GameFont.Tiny),
                    GameFont.Tiny, selected != null ? UITheme.Accent : UITheme.Muted);
            }

            string stencil = Translate("PersonalChronicle.UI.Floor.PanelStencil");
            float stencilWidth = MeasureWidth(stencil, GameFont.Tiny);
            float stencilLimit = tailX - gap;
            if (x + stencilWidth <= stencilLimit)
            {
                UIComponents.Label(new Rect(x, row.y, stencilWidth, rowH),
                    stencil, GameFont.Tiny, UITheme.Dim);
            }

            return y + WorkshopToolbarHeight + WorkshopTitleGap;
        }

        /// <summary>
        /// 打开设备详情弹窗（对齐预览 <c>openModal()</c>）。
        ///
        /// <para>数据经 <see cref="SystemCenterReadModelProvider.BuildDeviceDetail"/> 走契约取，
        /// 本方法只负责「定位 → 取快照 → 压栈」；无设备快照时不弹（<c>ShouldOpen</c> 短路）。</para>
        /// </summary>
        private void OpenDeviceDetail(FloorDeviceView device)
        {
            if (device == null || readModel == null)
            {
                return;
            }
            string centerDefName = cachedDetail != null && cachedDetail.Card != null
                ? cachedDetail.Card.DefName
                : null;
            if (string.IsNullOrEmpty(centerDefName))
            {
                return;
            }
            FloorDeviceDetailSnapshot detail =
                readModel.BuildDeviceDetail(centerDefName, device.Slot, device.Index);
            if (!FloorDeviceDetailWindow.ShouldOpen(detail))
            {
                return;
            }
            int slot = device.Slot;
            int index = device.Index;
            FloorDeviceDetailWindow window = new FloorDeviceDetailWindow(detail);
            // ⑦ 生产列表的动作（调序/挂起/移出/追加）会改游戏状态 ⇒ 动作成功后按**同一 (slot,index)**
            // 重建快照并替换窗口内容，弹窗即时刷新（无需重开）。
            window.RefreshAction = delegate
            {
                FloorDeviceDetailSnapshot next =
                    readModel.BuildDeviceDetail(centerDefName, slot, index);
                if (FloorDeviceDetailWindow.ShouldOpen(next))
                {
                    window.ReplaceDetail(next);
                }
            };
            Find.WindowStack.Add(window);
        }

        /// <summary>当前选中的格/设备显示名；未选中返回 null（调用方回落操作提示）。</summary>
        private string SelectedFloorLabel(WorkshopFloorSnapshot snapshot)        {
            if (snapshot == null)
            {
                return null;
            }
            if (floorView.SelectedDevice >= 0 && snapshot.Devices != null
                && floorView.SelectedDevice < snapshot.Devices.Count)
            {
                FloorDeviceView device = snapshot.Devices[floorView.SelectedDevice];
                return device != null
                    ? Translate("PersonalChronicle.UI.Floor.Sel.Readout", device.Name)
                    : null;
            }
            if (floorView.SelectedSlot >= 0 && snapshot.Bays != null
                && floorView.SelectedSlot < snapshot.Bays.Count)
            {
                FloorBayView bay = snapshot.Bays[floorView.SelectedSlot];
                return bay != null
                    ? Translate("PersonalChronicle.UI.Floor.Sel.Readout", bay.Name)
                    : null;
            }
            return null;
        }

        private static int KpiColumnsFor(float width, int groupSize)
        {
            int fits = Mathf.FloorToInt((width + KpiCardGap) / (MinKpiCardWidth + KpiCardGap));
            return Mathf.Clamp(fits, 1, Mathf.Max(1, groupSize));
        }

        /// <summary>一组 KPI 排布后的一行（同卡宽、同列数）。</summary>
        private struct KpiCardRow
        {
            /// <summary>起始卡下标。</summary>
            public int Start;

            /// <summary>本行卡数。</summary>
            public int Count;

            /// <summary>本行卡宽。</summary>
            public float CardWidth;
        }

        // 布局计划缓存：宽度/卡数变化或快照重建后才重算 —— 绘制路径不 new（PERF）。
        private readonly List<KpiCardRow> kpiRowPlan = new List<KpiCardRow>(8);
        private bool kpiRowPlanCompact;
        private float kpiRowPlanWidth = -1f;
        private int kpiRowPlanCount = -1;
        private bool kpiRowPlanDirty = true;

        /// <summary>快照重建后调用：下次布局按新数据（含 Provider 分组）重算。</summary>
        private void InvalidateKpiRowPlan()
        {
            kpiRowPlanDirty = true;
        }

        /// <summary>
        /// 规划 KPI 卡行（宽度/卡数变化或快照重建时才重算）：
        /// ① 全部卡能挤进一行 → 一行（复现预览 <c>#kpi</c> 的一行九卡）；
        /// ② 否则按 Provider 声明的分组，每组尽量一行，放不下才折行。
        /// 整个面板统一一种卡片形态（窄→单行紧凑卡；宽→数值+标签/附注+右侧条），避免混排。
        /// </summary>
        private void EnsureKpiRowPlan(float width, List<SystemCenterKpiView> kpis, List<int> rowSizes)
        {
            int count = kpis != null ? kpis.Count : 0;
            if (!kpiRowPlanDirty && Mathf.Approximately(kpiRowPlanWidth, width) && kpiRowPlanCount == count)
            {
                return;
            }

            kpiRowPlan.Clear();
            kpiRowPlanWidth = width;
            kpiRowPlanCount = count;
            kpiRowPlanDirty = false;
            kpiRowPlanCompact = false;
            if (count <= 0)
            {
                return;
            }

            float narrowest = float.MaxValue;
            if (KpiColumnsFor(width, count) >= count)
            {
                AddKpiRow(0, count, count, width, ref narrowest);
            }
            else
            {
                int index = 0;
                int group = 0;
                while (index < count)
                {
                    int size = KpiGroupSize(rowSizes, group, count - index);
                    int cols = KpiColumnsFor(width, size);
                    for (int offset = 0; offset < size; offset += cols)
                    {
                        AddKpiRow(index + offset, Mathf.Min(cols, size - offset), cols, width, ref narrowest);
                    }
                    index += size;
                    group++;
                }
            }

            kpiRowPlanCompact = narrowest < CompactKpiCardWidth;
        }

        private void AddKpiRow(int start, int rowCount, int columns, float width, ref float narrowest)
        {
            if (rowCount <= 0)
            {
                return;
            }
            float cardWidth = Mathf.Max(60f, (width - KpiCardGap * (columns - 1)) / columns);
            if (cardWidth < narrowest)
            {
                narrowest = cardWidth;
            }
            kpiRowPlan.Add(new KpiCardRow { Start = start, Count = rowCount, CardWidth = cardWidth });
        }

        private float KpiPlanCardHeight
        {
            get { return kpiRowPlanCompact ? CompactKpiCardHeight : KpiCardHeight; }
        }

        /// <summary>
        /// KPI 区块高度：与 <see cref="DrawKpiGrid"/> 共用同一份布局计划与常量，两处不会漂移。
        /// </summary>
        private float KpiBlockHeight(float width, List<SystemCenterKpiView> kpis, List<int> rowSizes)
        {
            EnsureKpiRowPlan(width, kpis, rowSizes);
            int rows = kpiRowPlan.Count;
            if (rows <= 0)
            {
                return 0f;
            }
            return rows * KpiPlanCardHeight + (rows - 1) * KpiCardGap;
        }

        private static int KpiGroupSize(List<int> rowSizes, int group, int remaining)
        {
            int size = rowSizes != null && group < rowSizes.Count ? rowSizes[group] : remaining;
            return Mathf.Clamp(size, 1, Mathf.Max(1, remaining));
        }

        /// <summary>
        /// KPI 网格（自适应档位，对齐预览 <c>#kpi</c> 的紧凑横排）。
        /// 进度条只读快照里的 <c>Share01</c>，窗口不重算任何比例（DL 契约：快照即真相）。
        /// </summary>
        private float DrawKpiGrid(
            Rect rect, float y, List<SystemCenterKpiView> kpis, List<int> rowSizes)
        {
            if (kpis == null || kpis.Count == 0)
            {
                return y;
            }

            EnsureKpiRowPlan(rect.width, kpis, rowSizes);
            float cardHeight = KpiPlanCardHeight;
            for (int r = 0; r < kpiRowPlan.Count; r++)
            {
                KpiCardRow row = kpiRowPlan[r];
                float rowY = y + r * (cardHeight + KpiCardGap);
                for (int i = 0; i < row.Count; i++)
                {
                    Rect card = new Rect(
                        rect.x + i * (row.CardWidth + KpiCardGap), rowY,
                        row.CardWidth, cardHeight);
                    DrawKpiCard(card, kpis[row.Start + i], kpiRowPlanCompact);
                }
            }
            return y + kpiRowPlan.Count * cardHeight + (kpiRowPlan.Count - 1) * KpiCardGap;
        }

        /// <summary>
        /// 单张 KPI 卡。
        /// 宽档 = <c>[数值] [标签 / 附注] [右侧进度条]</c>（预览 <c>.oee-c</c> 形态，附注可读）；
        /// 窄档 = <c>[数值] [标签] + 底部细条</c>（1:1 一行九卡时的密度形态，附注转悬停提示）。
        /// </summary>
        private static void DrawKpiCard(Rect card, SystemCenterKpiView kpi, bool compact)
        {
            if (kpi == null)
            {
                return;
            }

            bool hasValue = kpi.HasValue;
            Color tone = hasValue ? ToneColor(kpi.Tone) : UITheme.Dim;
            UIComponents.Card(card, tone);
            Widgets.DrawHighlightIfMouseover(card);

            const float padX = 8f;
            float inner = Mathf.Max(20f, card.width - padX * 2f);
            string value = hasValue && !string.IsNullOrEmpty(kpi.Value)
                ? kpi.Value
                : Translate("PersonalChronicle.UI.MasterControl.NoValue");
            bool hasBar = kpi.Share01 >= 0f;

            if (compact)
            {
                // 窄档：一行放下数值 + 标签，进度条退到底部细条（卡高 30）。
                float valueWidthC = Mathf.Clamp(card.width * 0.34f, 30f, 48f);
                float labelWidthC = Mathf.Max(20f, inner - valueWidthC - 6f);
                UIComponents.Label(new Rect(card.x + padX, card.y + 2f, valueWidthC, card.height - 4f), value,
                    GameFont.Small, tone, TextAnchor.MiddleLeft);
                // ⚠ 行高 18（Tiny）；卡高 30 − 底部细条 4 ⇒ 上界 26，卡内 y+8 起画正好不重叠。
                UIComponents.Label(new Rect(card.x + padX + valueWidthC + 6f, card.y + 8f, labelWidthC,
                        PopulationRowHeight),
                    UIComponents.TruncateToWidth(kpi.Label, labelWidthC, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Text);
                if (hasBar)
                {
                    UIComponents.ProgressBar(
                        new Rect(card.x + padX, card.yMax - 4f, inner, CompactKpiBarHeight), kpi.Share01, tone);
                }
                TooltipHandler.TipRegion(card, kpi.TooltipText);
                return;
            }

            // 宽档：**按预览 `.oee-c` / `.gauge` 的最终参数 1:1 排**（LAND-001 第三次实测）：
            //   `#kpi #oee,#kpi #gauges{gap:10px}`（HTML 561）
            //   `.oee-c{padding:5px 11px;gap:10px}` / `.gauge{padding:5px 10px;gap:9px}`（562-563）
            //   `.oee-c .big{font-size:18px}` / `.gauge .num{font-size:16px}`（562-563）
            //   `.oee-c .t{font-size:9.5px;line-height:1.35}` | `.bar{flex:1;height:8px}`
            // ⇒ [大数字(自适应宽)] →gap 10→ [两行文本] →gap 10→ [细条吃掉剩余宽度]。
            const float cssPadX = 11f;
            const float cssGap = 10f;
            float x = card.x + cssPadX;
            float right = card.xMax - cssPadX;

            // 预览数值 16~18px ⇒ 引擎 `Small`（旧值 Medium 会明显偏大）
            float valueWidth = Mathf.Min(MeasureWidth(value, GameFont.Small), right - x - 4f);
            UIComponents.Label(new Rect(x, card.y, valueWidth, KpiCardHeight), value,
                GameFont.Small, tone, TextAnchor.MiddleLeft);
            x += valueWidth + cssGap;

            // 两行文本块：第一行 = 指标名，第二行 = 口径/附注（预览同款）；宽度取两者较宽者
            float textWidth = 0f;
            if (!string.IsNullOrEmpty(kpi.Label))
            {
                textWidth = Mathf.Max(textWidth, MeasureWidth(kpi.Label, GameFont.Tiny));
            }
            if (!string.IsNullOrEmpty(kpi.Note))
            {
                textWidth = Mathf.Max(textWidth, MeasureWidth(kpi.Note, GameFont.Tiny));
            }
            float barMin = hasBar ? 24f : 0f;
            textWidth = Mathf.Clamp(textWidth, 24f, Mathf.Max(24f, right - x - cssGap - barMin));
            float lineHeight = Text.LineHeightOf(GameFont.Tiny);
            float textTop = card.y + (KpiCardHeight - lineHeight * 2f) * 0.5f;
            UIComponents.Label(new Rect(x, textTop, textWidth, lineHeight),
                UIComponents.TruncateToWidth(kpi.Label, textWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Text);
            if (!string.IsNullOrEmpty(kpi.Note))
            {
                UIComponents.Label(new Rect(x, textTop + lineHeight, textWidth, lineHeight),
                    UIComponents.TruncateToWidth(kpi.Note, textWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim);
            }
            x += textWidth + cssGap;

            if (hasBar && right - x >= 16f)
            {
                // `.bar{flex:1;height:8px}`：吃掉剩余宽度，垂直居中
                Rect bar = new Rect(x, card.y + (KpiCardHeight - KpiBarHeight) * 0.5f,
                    right - x, KpiBarHeight);
                UIComponents.ProgressBar(bar, kpi.Share01, tone);
                UIComponents.Border(bar, UITheme.BorderSoft);
            }

            TooltipHandler.TipRegion(card, kpi.TooltipText);
        }

        /// <summary>「职责边界」下的一个子列表：caption + 逐行（文本 + own/delegated 标签）。</summary>
        private static float DrawSubList(
            Rect rect, float y, string caption, List<string> items, string tagText, Color tagColor)
        {
            UIComponents.Label(new Rect(rect.x, y, rect.width, 20f), caption, GameFont.Small, UITheme.Accent);
            y += SubListCaptionHeight;
            if (items == null || items.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x + 12f, y, Mathf.Max(0f, rect.width - 12f), LineRowHeight),
                    EmptyText, GameFont.Tiny, UITheme.Dim);
                return y + LineRowHeight;
            }

            const float tagWidth = 78f;
            float textWidth = Mathf.Max(0f, rect.width - 24f - tagWidth);
            for (int i = 0; i < items.Count; i++)
            {
                UIComponents.Label(new Rect(rect.x + 12f, y, textWidth, LineRowHeight),
                    UIComponents.TruncateToWidth(items[i], textWidth, GameFont.Small),
                    GameFont.Small, UITheme.Text);
                UIComponents.Badge(new Rect(rect.xMax - tagWidth, y + 2f, tagWidth - 6f, 18f),
                    tagText, tagColor);
                y += LineRowHeight;
            }
            return y;
        }
    }
}

using PersonalChronicle.MasterControl.Population;
using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// 终局管理系统 · 总控制中心（MainTabWindow，M1 最小落地）。
    ///
    /// <para>本窗口是「系统中心注册表」的观测面（规范书 P6 可观测 / P5 调度与玩法解耦）：
    /// 三个视图 —— 总览（殖民地现状 + 初始/预留中心卡片）、统一调度总线（调度层说明）、
    /// 中心详情（清单 + KPI + 职责边界 + 事件契约 + 依赖能力）。</para>
    ///
    /// <para>边界（AI-002 / GOV-003）：本类<b>只绘制</b>，不采集、不聚合、不写领域状态。
    /// 所有数值来自 <see cref="SystemCenterReadModelProvider"/> 产出的不可变快照
    /// （构建期已本地化/格式化），绘制路径零翻译、零扫描。窗口自身只持有
    /// <b>瞬时 UI 状态</b>（当前视图、滚动位置、选中中心 ID），不参与存档。</para>
    ///
    /// <para>刷新门控：tick 节流是唯一权威门控（08c DL 契约）——只看
    /// <c>nextRefreshTick</c>，不靠「数据有没有变」判定。</para>
    /// </summary>
    public sealed partial class MasterControlTabWindow : MainTabWindow
    {
        /// <summary>三个视图：总览 / 统一调度总线 / 中心详情。</summary>
        private enum ViewKind
        {
            /// <summary>中心注册表 —— 总控中心的主视图，即「路由到各系统中心」的入口页。</summary>
            Registry,

            /// <summary>某个系统中心的详情页。</summary>
            CenterDetail
        }

        /// <summary>
        /// 快照重建节流（tick）。300 tick ≈ 5 秒：中心 KPI 是<b>观测</b>数据，
        /// 不需要每帧精度；节流本身就是防止「每帧全量扫描」的权威门控
        /// （GATE-PERF：热路径无全库扫描）。
        /// </summary>
        private const long RefreshIntervalTicks = 300L;

        // ---- 布局常量（单一来源，绘制与高度估算共用，避免两处漂移）----
        private const float HeaderHeight = UITheme.HeaderHeight;
        private const float Gap = UITheme.Gap;
        private const float CenterCardHeight = 84f;
        private const float CenterCardGap = 8f;
        private const float LineRowHeight = 22f;
        /// <summary>
        /// 面板/区块之间的纵向间距。预览最终值（LAND-001 第三次实测）：
        /// <c>#sop{grid-template-rows:auto 1fr;gap:10px}</c>（HTML 557）与
        /// <c>.fpane.on{gap:10px}</c>（572）——**行间距 10**（列间距才是 14）。
        /// 旧值 14 取的是列间距，作行间距用会偏松。
        /// </summary>
        private const float SectionGap = 10f;
        private const float PageTitleHeight = 30f;
        private const float PageLineHeight = 22f;
        private const float NoticeBoxHeight = 44f;
        /// <summary>朴素标题高（只一行文字，无强调色块 / 无分隔横线）。</summary>
        private const float PlainTitleHeight = 24f;

        // ---- 中心卡片网格（LAND-002/005：按预览卡宽比例缩放，而非写死列数）----
        /// <summary>预览基准：总览中心卡片网格 <c>minmax(392px, 1fr)</c>，其内容宽 1240 → 3 列。</summary>
        private const float PreviewGridContentWidth = 1240f;
        private const float PreviewGridCardMinWidth = 392f;
        /// <summary>可读下限：标题 + 状态徽标 + 摘要行再窄就放不下（LAND-007 文字决定宽度）。</summary>
        private const float MinCenterCardWidth = 300f;

        // ---- 瞬时 UI 状态（不入存档）----
        private ViewKind view = ViewKind.Registry;
        private string selectedCenterDefName;

        /// <summary>
        /// 人员中心当前视图（0 概览 / 1 档案总览 / 2 人物档案 / 3 分档与契约）——
        /// **UI 瞬时状态，不入存档**（与预览 <c>#navseg</c> 的 4 个视图一一对应）。
        /// tab 条的**出现与否由数据决定**（有业务视图才画），不认中心名（LAND-010）。
        /// </summary>
        private int populationView;
        private Vector2 contentScroll;
        /// <summary>tab 内容区（预览 <c>.fpanes</c>）的区内滚动位置——UI 瞬时状态，不入存档。</summary>
        private Vector2 tabPaneScroll;
        private long nextRefreshTick;
        /// <summary>车间俯视视图态（缩放/平移）——UI 瞬时状态，不入存档（GATE-UI）。</summary>
        private PersonalChronicle.Manufacturing.UI.WorkshopFloorPlan.FloorPlanViewState floorView =
            PersonalChronicle.Manufacturing.UI.WorkshopFloorPlan.FloorPlanViewState.Default();

        /// <summary>
        /// ② 俯视面板的**内容区页签**（预览 <c>buildFloorTabs()</c> 的默认 = 第一个 "总控"）。
        /// UI 瞬时状态，不入存档。
        ///
        /// <para>⚠ 2026-10-06（用户指令「移除底栏 Tab 栏整行」）：页签栏已删除 ⇒ 本字段**不再有切换入口**，
        /// 恒为 <c>TabCockpit</c>（总控）；内容区分发（<c>AppShell.DrawActivePane</c>）与计高
        /// （<c>Detail.TabPaneContentHeight</c>）仍按它工作 —— 恢复 Tab 栏即可重新获得全部页签。</para>
        /// </summary>
        private int selectedFloorTab;

        // ---- Read Model ----
        private SystemCenterReadModelProvider readModel;
        private SystemCenterListSnapshot cachedList = new SystemCenterListSnapshot();
        private SystemCenterDetailSnapshot cachedDetail;

        /// <summary>
        /// 只声明「上限尺寸」，实际尺寸由原版 <c>MainTabWindow.InitialSize</c> 按屏幕夹取
        /// （宽 ≤ 屏宽、高 ≤ 屏高−35）→ 小屏/高 UI 缩放时自动变成「近全屏」，
        /// 大屏时是一张留有边距的居中大窗（不再写死 1180×740 那种两头不讨好的尺寸）。
        ///
        /// <para>上限取 1960×1120 = 预览页 <c>#wrap{max-width:1720px}</c> 的内容宽 1680
        /// ＋ 内边距 24 ＋ 滚动条 18 ≈ 1722（本窗口无侧栏，W_g = 窗口宽 − 42）：
        /// 「1080p 且 UI 缩放 1.0」时窗口吃满屏宽，KPI 区可复现预览 12 栏栅格的一行九卡；
        /// 再往上加宽只会把卡片拉成空条，故封顶。</para>
        /// </summary>
        public override Vector2 RequestedTabSize
        {
            get { return new Vector2(1960f, 1120f); }
        }

        /// <summary>
        /// 屏幕定位：原版 <c>MainTabWindow</c> 是「左贴边 + 底贴边」（它在
        /// <c>SetInitialSizeAndPosition</c> 里先把 x 归零、再把 y 钉到主标签栏上方），
        /// 总控中心是一张<b>仪表盘</b>而不是边缘挂靠的列表，故整体重写为
        /// <b>在主标签栏以上的画布内居中</b>，并保持 <see cref="Window.InitialSize"/>
        /// 原有的「宽 ≤ 屏宽、高 ≤ 屏高-35」夹取，小屏不会溢出。
        /// </summary>
        protected override void SetInitialSizeAndPosition()
        {
            Vector2 size = InitialSize;
            float canvasHeight = UI.screenHeight - 35f;
            windowRect = new Rect(
                Mathf.Round((UI.screenWidth - size.x) / 2f),
                Mathf.Round((canvasHeight - size.y) * 0.5f),
                size.x,
                size.y);
        }

        /// <summary>
        /// 关窗钩子（2026-09-21 审计修复 · CHG-SHOP-AUDIT-001 · A-4）：清理编排画布的
        /// **会话级静态态**（独立设备清单 / 拖拽载荷 / 中键平移标志 / 2 秒扫描缓存）。
        /// 不做的话：关窗后它们仍驻留，重开（或换档）时会看到"幽灵设备 / 幽灵拖拽 ghost"。
        /// </summary>
        public override void PreClose()
        {
            base.PreClose();
            // 自动保存收口（2026-09-26）：先落档再清会话 —— 反过来会把"待保存的变更"连同
            // 内存清单一起清掉（且会误删档里的既有项）。
            PersonalChronicle.Manufacturing.UI.WorkshopLayoutCanvas.PersistBeforeClose();
            PersonalChronicle.Manufacturing.UI.WorkshopLayoutCanvas.ClearSession();
            // 两侧轨道的会话态（页签选中 / 轨道宽 / 拖拽把手）同样必须复位：
            // 原实现**无任何清理点** ⇒ 换档后残留上一局选中的「设备库」页签与轨道宽度（P1-8）。
            PersonalChronicle.Manufacturing.UI.WorkshopFloorPlan.ResetRailSession();
            // ⚠ 引导锚点注册表**不在这里 End()**（守门 P1）：本窗口关窗是引导剧本的一部分
            //   （步骤 S3 就教玩家关掉总控中心去划车间），而在 PreClose 里 End() 会把整个引导
            //   打成静默失效（Active=false ⇒ 再打开总控中心也不上报）。
            //   引导生命周期由 `Window_TourOverlay` 独占（Begin/PreClose）；本窗口消失后
            //   锚点靠"新鲜度过期"自动降级（见 TourAnchorRegistry.TryGet）。
        }

        public override void DoWindowContents(Rect inRect)
        {
            // ⚠ 2026-09-19（审计 §8.1 / **P0**）：**整页异常隔离**。
            //
            // 本目录此前**一个 `catch` 都没有**（全目录唯一命中是注释）—— 而本工程**已经有**
            // 正确范式：`PersonnelArchiveHost.DrawEmbedded`（try/catch + 记日志 + finally 恢复 GUI 状态）。
            // 缺了它时，7 个 tab 面板里任一处的空引用 / 坏快照 / 外部 RUIF 抛错，都会让
            // **整帧 + ticker 一起消失**，且日志里查不到原因（异常逃出 OnGUI 会被 Unity 吞掉）。
            //
            // 📌 隔离放在**最外层**：一块坏不得拖垮整页。
            // 📌 与 `UIComponents.KpiGrid` 的裁剪栈修复是**互补**的：那处防"栈失衡"，
            //    这里防"异常逃逸"—— 两者都在才真正兜得住。
            try
            {
                DrawWindowContentsCore(inRect);
            }
            catch (System.Exception ex)
            {
                // ⚠ 必须记录：只吞不记会让"整页空白"变成无法诊断的现象（审计 §8.4 同类问题）。
                ChronicleLog.Error(ChronicleLog.Category.Ui, "MasterControl window failed: " + ex);
            }
        }

        /// <summary>
        /// <see cref="DoWindowContents"/> 的实体（拆出来只为给外层套 try/catch，**逻辑一字未改**）。
        /// </summary>
        private void DrawWindowContentsCore(Rect inRect)
        {
            EnsureReadModel();
            RefreshIfNeeded();

            // 总控中心 = 路由入口，不设侧栏：内容区吃满窗口宽度（W_g = 窗口宽 − 42）。
            // 底栏（预览 #ticker）在窗口最底部通栏，绘制前先把高度从内容区扣掉。
            float tickerHeight = TickerHeightForCurrentView;
            Rect headerRect = new Rect(0f, 0f, inRect.width, HeaderHeight);
            Rect contentRect = new Rect(0f, HeaderHeight + Gap,
                inRect.width, inRect.height - HeaderHeight - Gap - tickerHeight);

            DrawHeader(headerRect);
            DrawContent(contentRect);

            ManufacturingChromeSnapshot chrome = CurrentChrome;
            if (tickerHeight > 0f && chrome != null && !chrome.IsEmpty)
            {
                DrawTicker(inRect, chrome);
            }

            // 引导层已迁到**独立全屏窗口** `Window_TourOverlay`（CHG-20261006-TOUR2）：
            // 本窗口现在只负责"在自己绘制时上报锚点"，不再绘制引导层，也不再抑制交互
            // （引导改为"允许操作 + 条件自动推进"）。
        }

        // ---- 引导（CHG-20261006-TOUR2）：锚点上报用的只读探针 ----

        /// <summary>窗口局部矩形 → **屏幕矩形**（引导锚点一律用屏幕坐标，见注册表类注释）。</summary>
        private Rect ToScreenRect(Rect local)
        {
            return new Rect(windowRect.x + local.x, windowRect.y + local.y, local.width, local.height);
        }

        /// <summary>引导探针：当前是否处于注册表页（引导步骤用它判断"玩家已经回到注册表"）。</summary>
        internal bool TourShowsRegistry
        {
            get { return view == ViewKind.Registry; }
        }

        /// <summary>引导探针：当前是否处于**制造系统中心**详情页。</summary>
        internal bool TourShowsManufacturingDetail
        {
            get
            {
                return view == ViewKind.CenterDetail
                    && selectedCenterDefName == "manufacturing_center";
            }
        }

        // ---- Read Model 生命周期 ----

        private void EnsureReadModel()
        {
            if (readModel == null)
            {
                readModel = new SystemCenterReadModelProvider(PersonalChronicleMod.SystemCenterData);
            }
        }

        /// <summary>tick 节流门控：未到刷新点直接复用上次快照。</summary>
        private void RefreshIfNeeded()
        {
            long ticks = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            if (cachedList != null && ticks < nextRefreshTick)
            {
                return;
            }
            RefreshNow(ticks);
        }

        /// <summary>
        /// 显式刷新钩子：重置节流窗口后立即重建（用于必须即时生效的交互，
        /// 如调试开关切换 —— 不得等下一个 300 tick 窗口）。
        /// </summary>
        private void ForceRefresh()
        {
            nextRefreshTick = 0L;
            RefreshIfNeeded();
        }

        /// <summary>立即重建快照（导航切换 / 手动刷新入口）。</summary>
        private void RefreshNow(long ticks)
        {
            nextRefreshTick = ticks + RefreshIntervalTicks;
            cachedList = readModel.BuildList();
            RebuildDetailSnapshot();
            // 测试内容已移除：不再做随机数据填充（调用点保留注释，理由见 Shell 的页头按钮处）。
        }

        private void RebuildDetailSnapshot()
        {
            // KPI 卡布局依赖 Provider 声明的分组，快照换了就要重算布局计划。
            InvalidateKpiRowPlan();
            if (view != ViewKind.CenterDetail || string.IsNullOrEmpty(selectedCenterDefName))
            {
                cachedDetail = null;
                return;
            }
            // 经济中心 K 线：把窗口瞬时态（选中品种 / 档位）作为**请求**传下去 ——
            // 蜡烛数据量大，不整份入快照；请求缺省由 Provider 解析默认（SDD-LCE-003 §4）。
            cachedDetail = readModel.BuildDetail(selectedCenterDefName,
                new PersonalChronicle.Application.SystemCenters.ColonyStockViewRequest(
                    colonyStockDefName, colonyStockTierIndex));
        }

        // ---- 导航 ----

        /// <summary>回到中心注册表（总控中心主视图）。</summary>
        private void GoRegistry()
        {
            view = ViewKind.Registry;
            selectedCenterDefName = null;
            cachedDetail = null;
            contentScroll = Vector2.zero;
        }

        private void GoCenter(string centerDefName)
        {
            if (string.IsNullOrEmpty(centerDefName))
            {
                return;
            }
            view = ViewKind.CenterDetail;
            selectedCenterDefName = centerDefName;
            // 经济中心 K 线的窗口瞬时态随切换中心重置（UI 瞬时，不落盘；SDD-LCE-003 §4）
            ResetColonyStockView();
            RebuildDetailSnapshot();
            contentScroll = Vector2.zero;
        }

        // ---- 语气 → 颜色（唯一映射点；UI 不参与判定）----

        private static Color ToneColor(SystemCenterTone tone)
        {
            switch (tone)
            {
                case SystemCenterTone.Good: return UITheme.Alive;
                case SystemCenterTone.Warn: return UITheme.Threat;
                case SystemCenterTone.Bad: return UITheme.Dead;
                default: return UITheme.Text;
            }
        }

        /// <summary>
        /// 接入状态 → 徽标配色（唯一映射点；三态必须视觉可辨）：
        /// 运行中 = 绿（Alive）、数据未接入 = 琥珀（Warn，看得见「差一步」而不是灰）、预留 = 灰（Muted）。
        /// </summary>
        private static Color StatusColor(SystemCenterWiring wiring)
        {
            switch (wiring)
            {
                case SystemCenterWiring.Running: return UITheme.Alive;
                case SystemCenterWiring.NotWired: return UITheme.Warn;
                default: return UITheme.Muted;
            }
        }

        /// <summary>
        /// **无参** Keyed 文案。
        ///
        /// <para><b>⚠ 2026-09-19（CHG-20260919-ECON-UI-010）</b>：本助手被**绘制路径**大量调用
        /// （页头 / 面包屑 / 返回按钮 / 各面板标题 / 空态 / 表头 …），原实现
        /// <c>key.Translate().ToString()</c> 会**每帧分配一个新字符串** ⇒ 稳态 GC 压力，
        /// 且违反 GOV-010「绘制期零翻译、零拼接」。现改走 <see cref="UIComponents.Loc"/>
        /// 的**语言级缓存**（切语言自动全量失效）。
        /// 一处改动即覆盖本类全部无参调用点 —— 因为所有无参调用都经过这里。</para>
        ///
        /// <para>带参版本（<see cref="Translate(string, NamedArgument)"/> / <c>Translate2…</c>）
        /// **有意不缓存**：参数逐次不同，缓存会返回上一次的结果（错值比慢严重得多）。</para>
        /// </summary>
        private static string Translate(string key)
        {
            return UIComponents.Loc(key);
        }

        // 格式化翻译统一走 1.6 的 NamedArgument 重载：params object[] 版本在 1.6 已标记
        // Obsolete（CS0618），不能作为通用入口。
        private static string Translate(string key, NamedArgument arg)
        {
            return key.Translate(arg).ToString();
        }

        private static string Translate2(string key, NamedArgument arg0, NamedArgument arg1)
        {
            return key.Translate(arg0, arg1).ToString();
        }

        private static string Translate3(string key, NamedArgument arg0, NamedArgument arg1, NamedArgument arg2)
        {
            return key.Translate(arg0, arg1, arg2).ToString();
        }

        private static string Translate4(string key, NamedArgument arg0, NamedArgument arg1, NamedArgument arg2,
            NamedArgument arg3)
        {
            return key.Translate(arg0, arg1, arg2, arg3).ToString();
        }

        // ---- 高度估算（绘制与视口共用同一组常量，两处漂移会立刻表现为滚动异常）----

        private static int GridRows(int itemCount, int columns)
        {
            if (itemCount <= 0 || columns <= 0)
            {
                return 0;
            }
            return (itemCount + columns - 1) / columns;
        }

        private static float GridBlockHeight(int itemCount, int columns, float cardHeight, float cardGap)
        {
            int rows = GridRows(itemCount, columns);
            if (rows <= 0)
            {
                return 0f;
            }
            return rows * cardHeight + (rows - 1) * cardGap;
        }

        /// <summary>
        /// 中心卡片网格的列数（LAND-002/005）：按预览 <c>minmax(392px,1fr)</c> 的
        /// 卡宽/内容宽比例缩放后再做 auto-fill，而不是写死列数 —— 内容越宽列越多，
        /// 卡宽始终落在 [可读下限 300, 预览上限 392] 内。
        /// </summary>
        private static int CenterGridColumns(float width)
        {
            float scaled = PreviewGridCardMinWidth * (width / PreviewGridContentWidth);
            float minCardWidth = Mathf.Clamp(scaled, MinCenterCardWidth, PreviewGridCardMinWidth);
            return Mathf.Max(1, Mathf.FloorToInt((width + CenterCardGap) / (minCardWidth + CenterCardGap)));
        }

        /// <summary>
        /// 中心注册表内容高（用于滚动视口）。注册表页只放中心卡片：
        /// 标题 3 行 + 初始中心组 + 预留中心组 + 页脚提示。
        /// </summary>
        private float ComputeRegistryHeight(float width)
        {
            int columns = CenterGridColumns(width);
            float h = 4f;
            h += PageHeaderBlockHeight() + SectionGap;
            h += UITheme.SectionTitleHeight
                + GridBlockHeight(cachedList.InitialCenters.Count, columns, CenterCardHeight, CenterCardGap)
                + SectionGap;
            h += UITheme.SectionTitleHeight
                + GridBlockHeight(cachedList.ReservedCenters.Count, columns, CenterCardHeight, CenterCardGap)
                + SectionGap;
            h += NoticeBoxHeight + 40f;
            return h;
        }

        // ---- 数据源 ----

        private IReadOnlyList<SystemCenterCardView> InitialCards
        {
            get { return cachedList != null ? cachedList.InitialCenters : null; }
        }

        private IReadOnlyList<SystemCenterCardView> ReservedCards
        {
            get { return cachedList != null ? cachedList.ReservedCenters : null; }
        }
    }
}

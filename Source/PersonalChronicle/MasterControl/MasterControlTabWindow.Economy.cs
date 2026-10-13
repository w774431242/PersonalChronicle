using System.Collections.Generic;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Economy.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// Partial of <see cref="MasterControlTabWindow"/> ——**殖民地经济中心整页**（`SDD-LCE-003`）。
    ///
    /// <para><b>形态（预览 v9 = 1:1 权威源，LAND-001）</b>：**单面板视图** ——
    /// 默认「品种 K 线」（左品种列表 + 右蜡烛图），顶栏按钮切「热力图」（分组树图）。
    /// 整页适配一屏、**无页面级滚动**（与制造中心 app shell 同规）。</para>
    ///
    /// <para><b>RUIF 合规（2026-09-21 审计整改）</b>：工程铁律「组件方向内容必须引用 RUIF，禁止自研组件」
    /// （`Source/PersonalChronicle.csproj:34-38`）。本页原语**一律走 RUIF**：
    /// 绘制 = <c>RUIF.Draw.Box</c>；度量 = <c>RUIF.Measure.RowHeight/Width</c>；
    /// 档位条 = <c>RUIF.NavAtomsB.LayerSwitch</c>；空态 = <c>RUIF.FeedbackAtoms.EmptyState</c>；
    /// 颜色叠加 = <c>UITheme.WithAlpha</c>（工程官方令牌助手）。
    /// 仅两处自持并已登记例外（`EXC-RUIF-001/002`）：**树图 squarified 布局**（RUIF 无 treemap 原子）
    /// 与**蜡烛实体/网格/十字线/tag**（`ChartCandAtoms.Ohlc` 无实体填充、图表族按设计不读鼠标）。</para>
    ///
    /// <para><b>几何常量全部取自预览实测值并集中于此</b>（审计整改：禁止散落魔数；
    /// 行高一律由 RUIF 度量派生，不手写 ——「组件绘制规范」铁律 1）。</para>
    /// </summary>
    public sealed partial class MasterControlTabWindow
    {
        // ================================================================
        // 常量（审计整改：分四组，禁止散落魔数）
        // ================================================================

        // ---- ① 度量（RUIF 度量派生，禁止手写行高 —— 组件绘制铁律 1）----
        private static readonly float ColonyEconomyRowHeight = RUIF.Measure.RowHeight(GameFont.Tiny);
        private static readonly float ColonyEconomyBodyRowHeight = RUIF.Measure.RowHeight(GameFont.Tiny) + 2f;

        // ---- ② 页内布局 ----
        private const float ColonyEconomyPageGap = 10f;
        private const float ColonyEconomyMinPageHeight = 160f;
        private const float ColonyEconomyMinPanelHeight = 80f;
        private const float ColonyEconomySwitchWidth = 108f;
        private const float ColonyEconomySwitchInsetX = 6f;
        private const float ColonyEconomySwitchInsetY = 4f;
        private const float ColonyEconomySwitchInsetH = 8f;
        private const float ColonyEconomyPickerWidth = 196f;
        private const float ColonyEconomyPaneGap = 8f;
        private const float ColonyEconomyToolbarHeight = 20f;
        private const float ColonyEconomyToolbarTextRatio = 0.5f;
        private const float ColonyEconomyPickerValueCol = 48f;
        private const float ColonyEconomyPickerNameInset = 4f;
        private const float ColonyEconomyPickerTopGap = 2f;
        private const float ColonyEconomyPaneInset = 4f;
        private const float ColonyEconomyViewInset = 8f;
        private const float ColonyEconomyMinPaneWidth = 60f;

        // ---- ③ 图表几何（预览 v9 实测值；EXC-RUIF-002 登记项）----
        private const float ColonyEconomyPlotPadL = 8f;
        private const float ColonyEconomyPlotPadR = 56f;
        private const float ColonyEconomyPlotPadT = 12f;
        private const float ColonyEconomyPlotPadB = 22f;
        private const float ColonyEconomyMinPlotSide = 10f;
        private const float ColonyEconomyToolbarPlotGap = 4f;
        private const float ColonyEconomyRangePadRatio = 0.055f;
        private const float ColonyEconomyGridLevels = 4f;
        private const float ColonyEconomyLineWidth = 1f;
        private const float ColonyEconomyHalfPixel = 0.5f;
        private const float ColonyEconomyBodyWidthRatio = 0.72f;
        private const float ColonyEconomyBodyWidthMin = 2f;
        private const float ColonyEconomyBodyWidthMax = 30f;
        private const float ColonyEconomyDojiMinBody = 2f;
        private const float ColonyEconomyAxisLabelDy = 9f;
        private const float ColonyEconomyAxisLabelH = 18f;
        private const float ColonyEconomyTagH = 18f;
        private const float ColonyEconomyTagInset = 2f;
        private const float ColonyEconomyReadoutH = 18f;
        private const float ColonyEconomyReadoutInset = 2f;

        // ---- ④ 透明度（一律经 UITheme.WithAlpha 施加）----
        private const float ColonyEconomyOverlayAlpha = 0.10f;
        private const float ColonyEconomyGridAlpha = 0.10f;
        private const float ColonyEconomyLastAlpha = 0.55f;
        private const float ColonyEconomyCursorAlpha = 0.45f;
        private const float ColonyEconomyCursorCrossAlpha = 0.25f;
        private const float ColonyEconomyTagAlpha = 0.14f;

        // ---- ⑤ 热力图（布局自持 = EXC-RUIF-001；绘制走 RUIF 原语）----
        private const int ColonyEconomyPickerMaxRows = 26;
        private const int ColonyEconomyHeatMaxItems = 40;
        private const float ColonyEconomyHeatHeader = 18f;
        private const float ColonyEconomyHeatLeafMin = 4f;
        private const float ColonyEconomyHeatGroupMinW = 12f;
        private const float ColonyEconomyHeatGroupMinHExtra = 8f;
        private const float ColonyEconomyHeatMinWeight = 0.0001f;
        private const float ColonyEconomyHeatAlphaBase = 0.14f;
        private const float ColonyEconomyHeatAlphaSpan = 0.42f;
        private const float ColonyEconomyHeatSelectedAlpha = 0.16f;
        private const float ColonyEconomyHeatHoverAlpha = 0.06f;
        private const float ColonyEconomyHeatNameMinH = 38f;
        private const float ColonyEconomyHeatPctMinH = 20f;
        private const float ColonyEconomyHeatPctMinW = 34f;
        // ---- ⑥ 档位条（RUIF LayerSwitch 的可用宽夹取）----
        private const float ColonyEconomyTabWidthMin = 28f;
        private const float ColonyEconomyTabWidthMax = 48f;
        private const float ColonyEconomyHeatLabelPad = 8f;
        private const float ColonyEconomyHeatLabelInset = 2f;
        private const float ColonyEconomyHeatPctBottom = 19f;
        private const float ColonyEconomyHeatHeaderInset = 3f;

        /// <summary>
        /// 经济中心 K 线的**窗口瞬时态**（`SDD-LCE-003` §4）：选中品种（defName）/ 选中档位（−1 = 未选）。
        ///
        /// <para><b>归属</b>：UI 瞬时 —— **不落盘、不进 Def、不进快照内部状态**；
        /// 由 `RebuildDetailSnapshot` 拼成视图请求传给 Provider（蜡烛数据量大 ⇒ 不整份入快照）。</para>
        /// </summary>
        private string colonyStockDefName;

        /// <summary>见 <see cref="colonyStockDefName"/>；档位索引（0..5），−1 = 未选（Provider 解析默认档）。</summary>
        private int colonyStockTierIndex = -1;

        /// <summary>
        /// 视图开关（预览 v9 单面板视图）：`false` = 品种 K 线（默认）；`true` = 热力图（整页）。
        /// 纯绘制态 ⇒ 不参与快照请求（切视图不需要重建数据）。
        /// </summary>
        private bool colonyStockHeatView;

        /// <summary>悬停蜡烛索引（帧内瞬时；−1 = 未悬停）。</summary>
        private int colonyStockHoverIndex = -1;

        /// <summary>
        /// 档位标签数组缓存（`NavAtomsB.LayerSwitch` 需要 `string[]`；**禁止每帧 `new`** ——
        /// 组件规范反模式「绘制路径里 new」）。仅在档位数或当前语言变化时重建。
        /// </summary>
        private string[] colonyStockTierLabels;

        private int colonyStockTierLabelCount = -1;

        private LoadedLanguage colonyStockTierLabelLang;

        // ---- 绘制期文本缓存（PERF-001：绘制路径零拼接 / 零 ToString；按快照 revision 失效）----
        private long colonyStockTextRev = long.MinValue;
        private readonly List<string> colonyStockQtyText = new List<string>();
        private readonly List<string> colonyStockPickTip = new List<string>();
        private readonly List<string> colonyStockPctText = new List<string>();
        private readonly List<string> colonyStockHeatTip = new List<string>();
        private readonly List<string> colonyStockCandleTip = new List<string>();
        private int colonyStockReadoutHover = -2;
        private long colonyStockReadoutRev = long.MinValue;
        private string colonyStockReadoutText = string.Empty;

        /// <summary>
        /// 重置经济中心 K 线的**窗口瞬时态**（切换中心 / 首次进入时调用；不落盘、不进 Def）。
        /// 档位 `−1` 与品种 `null` 表示"未选" ⇒ Provider 解析默认（默认档 + 价值最高品种）。
        /// </summary>
        private void ResetColonyStockView()
        {
            colonyStockDefName = null;
            colonyStockTierIndex = -1;
            colonyStockHeatView = false;
            colonyStockHoverIndex = -1;
        }

        /// <summary>
        /// 是否走殖民地经济整页：**快照非 null** 即成立（LAND-010：只看数据，不看中心名）。
        /// </summary>
        private static bool UsesColonyEconomyPage(SystemCenterDetailSnapshot detail)
        {
            return detail != null && detail.ColonyEconomy != null;
        }

        /// <summary>批次 A（CHG-20261010-ECON-AGG-MIGRATE）：需求观察的**页内视图**开关 ——
        /// 原先是"打开聚合窗口的需求页"，现改为就地切换（见 <see cref="DrawColonyNeedsPage"/>）。</summary>
        private bool colonyEconomyNeedsView;
        /// <summary>批次 A-2：照护的页内视图开关（与需求互斥，见 <see cref="DrawColonyCarePage"/>）。</summary>
        private bool colonyEconomyCareView;
        /// <summary>需求观察的宿主无关内容层状态（与聚合窗口**同一类型**，不是第二套实现）。</summary>
        private PersonalChronicle.Economy.PawnEconomy.NeedPanelState colonyNeedsState
            = new PersonalChronicle.Economy.PawnEconomy.NeedPanelState();
        /// <summary>照护的宿主无关内容层状态（同上：与聚合窗口共用同一份内容层）。</summary>
        private readonly PersonalChronicle.Economy.PawnEconomy.CarePanelState colonyCareState
            = new PersonalChronicle.Economy.PawnEconomy.CarePanelState();
        /// <summary>批次 A-3：总览的页内视图开关与内容层状态（同上，与聚合窗口共用）。</summary>
        private bool colonyEconomyOverviewView;
        private readonly PersonalChronicle.Economy.PawnEconomy.OverviewPanelState colonyOverviewState
            = new PersonalChronicle.Economy.PawnEconomy.OverviewPanelState();
        private PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy colonyNeedsHost;
        private const int ColonyNeedsRefreshTicks = 60;

        /// <summary>画整页（KPI 条 + 单面板主体）。</summary>
        private void DrawColonyEconomyPage(Rect rect, SystemCenterDetailSnapshot detail)
        {
            Rect view = new Rect(rect.x + ColonyEconomyPaneInset, rect.y + ColonyEconomyPaneInset,
                Mathf.Max(ColonyEconomyMinPaneWidth, rect.width - ColonyEconomyViewInset),
                Mathf.Max(ColonyEconomyMinPageHeight, rect.height - ColonyEconomyViewInset));
            float y = view.y;

            // 末批（删聚合窗口）：原第 1 个按钮「市场」是**唯一**跨窗口入口（打开已删除的聚合窗口）⇒ 移除，
            // 三个**就地视图**（需求 / 照护 / 总览）各占 1/3。市场与各柜台改由**建筑自身的 gizmo** 打开
            //（`EconomyWorld.GetGizmos`：声明了机构类型的柜台一律开自己的工作台窗口）。
            float entryWidth = (view.width - ColonyEconomyPageGap * 2f) / 3f;
            // 三个视图互斥：切到其一时关闭其它视图，避免两个视图同时为真。
            if (RUIF.ButtonAtoms.PrimaryButton(new Rect(view.x, y, entryWidth, 32f), "PC_PEA.Needs.Title".Translate()))
            {
                colonyEconomyNeedsView = !colonyEconomyNeedsView;
                if (colonyEconomyNeedsView) { colonyEconomyCareView = false; colonyNeedsState.Invalidate(); }
            }
            if (RUIF.ButtonAtoms.PrimaryButton(new Rect(view.x + entryWidth + ColonyEconomyPageGap, y, entryWidth, 32f), "PC_PEA.Care.Title".Translate()))
            {
                colonyEconomyCareView = !colonyEconomyCareView;
                if (colonyEconomyCareView) { colonyEconomyNeedsView = false; colonyEconomyOverviewView = false; colonyCareState.Invalidate(); }
            }
            // 批次 A-3：第三个按钮「总览」——同样就地切换（互斥于其余两个视图）。
            if (RUIF.ButtonAtoms.PrimaryButton(new Rect(view.x + (entryWidth + ColonyEconomyPageGap) * 2f, y, entryWidth, 32f), "PC_PEA.Overview.Title".Translate()))
            {
                colonyEconomyOverviewView = !colonyEconomyOverviewView;
                if (colonyEconomyOverviewView) { colonyEconomyNeedsView = false; colonyEconomyCareView = false; colonyOverviewState.Invalidate(); }
            }
            y += 40f;

            if (colonyEconomyNeedsView)
            {
                DrawColonyNeedsPage(view, y);
                return;
            }
            if (colonyEconomyCareView)
            {
                DrawColonyCarePage(view, y);
                return;
            }
            if (colonyEconomyOverviewView)
            {
                DrawColonyOverviewPage(view, y);
                return;
            }


            // ---- KPI 条（3 格：总财富 / 白银 / 库存市值；宿主已 RUIF 化）----
            if (detail.Kpis.Count > 0)
            {
                float kpiHeight = KpiBlockHeight(view.width, detail.Kpis, detail.KpiRowSizes);
                if (kpiHeight > 0f)
                {
                    DrawKpiGrid(view, y, detail.Kpis, detail.KpiRowSizes);
                    y += kpiHeight + ColonyEconomyPageGap;
                }
            }

            ColonyEconomySnapshot economy = detail.ColonyEconomy;
            ColonyStockGallerySnapshot gallery = detail.ColonyStockGallery;

            // ---- 单面板主体（共享 chrome；外壳已 RUIF 化）----
            string title = colonyStockHeatView
                ? Translate("PersonalChronicle.Economy.Stock.HeatTitle")
                : Translate("PersonalChronicle.Economy.Stock.Title");
            Rect panel = new Rect(view.x, y, view.width, Mathf.Max(ColonyEconomyMinPanelHeight, view.yMax - y));
            Rect body = EconomyPanelChrome.Draw(panel, title, null, null, null, false, false);

            // ---- 顶栏视图切换（纯绘制态，不重建快照）----
            Rect switchRect = new Rect(panel.xMax - ColonyEconomySwitchWidth - ColonyEconomySwitchInsetX,
                panel.y + ColonyEconomySwitchInsetY, ColonyEconomySwitchWidth,
                EconomyPanelChrome.TitleHeight - ColonyEconomySwitchInsetH);
            string switchKey = colonyStockHeatView
                ? "PersonalChronicle.Economy.Stock.SwitchToKline"
                : "PersonalChronicle.Economy.Stock.SwitchToHeat";
            if (Widgets.ButtonText(switchRect, Translate(switchKey)))
            {
                colonyStockHeatView = !colonyStockHeatView;
                colonyStockHoverIndex = -1;
            }

            // ---- 内容 ----
            if (gallery == null)
            {
                DrawRebuildingBody(body, economy);
                return;
            }
            if (gallery.IsEmpty)
            {
                // Error 与 Empty 分离：异常**不得**被表述成"采样中"（GATE-UI P2）
                string emptyKey = gallery.HasError
                    ? "PersonalChronicle.Economy.Stock.Error"
                    : "PersonalChronicle.Economy.Stock.Empty";
                RUIF.FeedbackAtoms.EmptyState(body, Translate(emptyKey),
                    Translate("PersonalChronicle.Economy.Stock.Source"));
                return;
            }

            if (colonyStockHeatView)
            {
                DrawHeatView(body, gallery);
            }
            else
            {
                DrawKlineView(body, gallery);
            }
        }

        /// <summary>批次 A（CHG-20261010-ECON-AGG-MIGRATE）：**需求观察就地承接**。
        /// 内容层与聚合窗口需求页**完全同源**（`NeedDashboardPanel` + `NeedPanelState`，该内容层自述宿主无关），
        /// 刷新节流与聚合窗口一致（60 tick + 语言 / 地图可用性 / 感知纪元三重失效）。
        /// 迁移纪律：不复制第二份面板实现；缺键整键回落由内容层自理。</summary>
        private void DrawColonyNeedsPage(Rect view, float y)
        {
            var runtime = PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy.Current;
            var map = Find.CurrentMap;
            if (runtime == null || map == null) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (!ReferenceEquals(colonyNeedsHost, runtime))
            {
                colonyNeedsHost = runtime; colonyNeedsState.ResetForNewSubject(); colonyNeedsState.SelectedId = null;
            }
            bool available = runtime.IsPerceptionMapAvailable(map);
            if (colonyNeedsState.View == null
                || colonyNeedsState.View.Snapshot.Epoch != runtime.PerceptionSessionEpoch
                || colonyNeedsState.MapAvailable != available
                || !ReferenceEquals(colonyNeedsState.Language, LanguageDatabase.activeLanguage)
                || now < colonyNeedsState.Tick || now - colonyNeedsState.Tick >= ColonyNeedsRefreshTicks)
            {
                colonyNeedsState.Language = LanguageDatabase.activeLanguage;
                colonyNeedsState.Tick = now; colonyNeedsState.MapAvailable = available;
                var needsView = runtime.QueryNeedDashboard(map, colonyNeedsState.Page, colonyNeedsState.SelectedId);
                colonyNeedsState.Page = needsView.Snapshot.PageIndex;
                colonyNeedsState.SelectedId = needsView.Snapshot.SelectedId;
                colonyNeedsState.View = needsView; colonyNeedsState.MeasuredView = null;
            }
            var area = new Rect(view.x, y, view.width, Mathf.Max(0f, view.yMax - y));
            RUIF.Draw.Label(new Rect(area.x, area.y, Mathf.Max(0f, area.width - 40f), 32f),
                "PC_PEA.Needs.Title".Translate(), GameFont.Medium, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            PersonalChronicle.Economy.PawnEconomy.NeedDashboardPanel.Draw(
                new Rect(area.x, area.y + 38f, area.width, Mathf.Max(0f, area.height - 46f)),
                colonyNeedsState.View, PersonalChronicle.Economy.PawnEconomy.NeedPanelMode.Roster, colonyNeedsState);
        }

        /// <summary>批次 A-2（CHG-20261010-ECON-AGG-MIGRATE）：**照护就地承接**。
        /// 与聚合窗口照护页共用**同一份**宿主无关内容层 <c>CarePanel</c>（内容层唯一，零第二套实现）——
        /// 本方法只负责把宿主的地图与区域传进去；滚动 / 快照 / 菜单状态由 <c>colonyCareState</c> 持有。</summary>
        private void DrawColonyCarePage(Rect view, float y)
        {
            var area = new Rect(view.x, y, view.width, Mathf.Max(0f, view.yMax - y));
            PersonalChronicle.Economy.PawnEconomy.CarePanel.Draw(
                PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy.Current,
                Find.CurrentMap, area, colonyCareState);
        }

        /// <summary>批次 A-3（CHG-20261010-ECON-AGG-MIGRATE）：**总览就地承接**。
        /// 与聚合窗口总览页共用**同一份**宿主无关内容层 <c>OverviewPanel</c>（14 类查询装配逐条照搬，
        /// 无第二套实现）；本方法只传宿主的地图与区域。</summary>
        private void DrawColonyOverviewPage(Rect view, float y)
        {
            var area = new Rect(view.x, y, view.width, Mathf.Max(0f, view.yMax - y));
            PersonalChronicle.Economy.PawnEconomy.OverviewPanel.Draw(
                PersonalChronicle.Economy.PawnEconomy.GameComponent_PawnEconomy.Current,
                Find.CurrentMap, area, colonyOverviewState);
        }

        /// <summary>重建期骨架说明（画廊未提供时）。</summary>
        private void DrawRebuildingBody(Rect body, ColonyEconomySnapshot economy)
        {
            float lineY = body.y;
            for (int i = 0; i < economy.BodyLines.Count; i++)
            {
                string line = economy.BodyLines[i];
                if (!string.IsNullOrEmpty(line))
                {
                    UIComponents.Label(new Rect(body.x, lineY, body.width, ColonyEconomyBodyRowHeight),
                        line, GameFont.Tiny, UITheme.Dim);
                }
                lineY += ColonyEconomyBodyRowHeight;
            }
        }

        // ================================================================
        // 视图 ①：品种 K 线（左列表 + 右蜡烛图）
        // ================================================================
        private void DrawKlineView(Rect body, ColonyStockGallerySnapshot gallery)
        {
            EnsureStockTextCache(gallery);
            Rect picker = new Rect(body.x, body.y, ColonyEconomyPickerWidth, body.height);
            Rect pane = new Rect(body.x + ColonyEconomyPickerWidth + ColonyEconomyPaneGap, body.y,
                Mathf.Max(ColonyEconomyMinPaneWidth, body.width - ColonyEconomyPickerWidth - ColonyEconomyPaneGap),
                body.height);

            DrawStockPicker(picker, gallery);
            DrawStockChart(pane, gallery);
        }

        /// <summary>左：品种选择列表（行高由 RUIF 度量派生；大列表降级取前 N）。</summary>
        private void DrawStockPicker(Rect rect, ColonyStockGallerySnapshot gallery)
        {
            UIComponents.Label(new Rect(rect.x, rect.y, rect.width, ColonyEconomyRowHeight),
                Translate("PersonalChronicle.Economy.Stock.PickItem"), GameFont.Tiny, UITheme.Dim);

            float y = rect.y + ColonyEconomyRowHeight + ColonyEconomyPickerTopGap;
            int rows = Mathf.Min(gallery.Items.Count, ColonyEconomyPickerMaxRows);
            float bottom = rect.yMax - ColonyEconomyRowHeight;

            for (int i = 0; i < rows; i++)
            {
                if (y + ColonyEconomyRowHeight > bottom)
                {
                    break;   // 铁律 3：循环取行前先判剩余高度，不足即 break（禁止往 0 高矩形画文字）
                }
                ColonyStockItemRow row = gallery.Items[i];
                Rect rowRect = new Rect(rect.x, y, rect.width, ColonyEconomyRowHeight);
                bool selected = string.Equals(row.DefName, gallery.SelectedDef, System.StringComparison.Ordinal);
                if (selected)
                {
                    RUIF.Draw.Box(rowRect, UITheme.WithAlpha(UITheme.Text, ColonyEconomyOverlayAlpha));
                }
                else if (Mouse.IsOver(rowRect))
                {
                    // 悬停/点击走引擎原语（RUIF 无 hover 原子；其内部同样使用这两者）
                    Widgets.DrawHighlightIfMouseover(rowRect);
                }
                UIComponents.Label(
                    new Rect(rowRect.x + ColonyEconomyPickerNameInset, rowRect.y,
                        rowRect.width - ColonyEconomyPickerValueCol - ColonyEconomyPickerNameInset, rowRect.height),
                    row.Label, GameFont.Tiny, selected ? UITheme.Text : UITheme.Dim);
                UIComponents.Label(
                    new Rect(rowRect.xMax - ColonyEconomyPickerValueCol, rowRect.y,
                        ColonyEconomyPickerValueCol, rowRect.height),
                    i < colonyStockQtyText.Count ? colonyStockQtyText[i] : row.Quantity.ToString(),
                    GameFont.Tiny, UITheme.Dim);
                TooltipHandler.TipRegion(rowRect,
                    i < colonyStockPickTip.Count ? colonyStockPickTip[i] : row.Label);
                if (Widgets.ButtonInvisible(rowRect))
                {
                    SelectStockItem(gallery, row.DefName);
                }
                y += ColonyEconomyRowHeight;
            }

            if (gallery.Items.Count > rows)
            {
                UIComponents.Label(new Rect(rect.x, rect.yMax - ColonyEconomyRowHeight, rect.width, ColonyEconomyRowHeight),
                    // 走 NamedArgument 重载（params object[] 版本在 1.6 已标记 Obsolete）
                    Translate2("PersonalChronicle.Economy.Stock.More",
                        gallery.Items.Count - rows, ColonyEconomyPickerMaxRows),
                    GameFont.Tiny, UITheme.Dim);
            }
        }

        /// <summary>右：工具条（周期档位）+ 蜡烛绘图区 + OHLC 读数。</summary>
        private void DrawStockChart(Rect rect, ColonyStockGallerySnapshot gallery)
        {
            Rect toolbar = new Rect(rect.x, rect.y, rect.width, ColonyEconomyToolbarHeight);
            UIComponents.Label(
                new Rect(toolbar.x, toolbar.y, Mathf.Max(ColonyEconomyMinPaneWidth,
                    toolbar.width * ColonyEconomyToolbarTextRatio), toolbar.height),
                Translate("PersonalChronicle.Economy.Stock.Source"), GameFont.Tiny, UITheme.Dim);
            DrawTierTabs(toolbar, gallery);

            Rect plot = new Rect(rect.x, rect.y + ColonyEconomyToolbarHeight + ColonyEconomyToolbarPlotGap, rect.width,
                Mathf.Max(ColonyEconomyMinPaneWidth, rect.height - ColonyEconomyToolbarHeight - ColonyEconomyToolbarPlotGap));

            List<ColonyStockCandle> candles = gallery.Candles;
            if (candles.Count == 0)
            {
                RUIF.FeedbackAtoms.EmptyState(plot, Translate("PersonalChronicle.Economy.Stock.Empty"),
                    Translate("PersonalChronicle.Economy.Stock.Source"));
                return;
            }

            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < candles.Count; i++)
            {
                if (candles[i].Low < lo) lo = candles[i].Low;
                if (candles[i].High > hi) hi = candles[i].High;
            }
            if (hi <= lo)
            {
                hi = lo + 1f;
            }
            float pad = (hi - lo) * ColonyEconomyRangePadRatio;
            lo -= pad;
            hi += pad;

            Rect inner = new Rect(plot.x + ColonyEconomyPlotPadL, plot.y + ColonyEconomyPlotPadT,
                Mathf.Max(ColonyEconomyMinPlotSide, plot.width - ColonyEconomyPlotPadL - ColonyEconomyPlotPadR),
                Mathf.Max(ColonyEconomyMinPlotSide, plot.height - ColonyEconomyPlotPadT - ColonyEconomyPlotPadB));

            // 值 → y（闭合区间 [lo,hi] 映射到 inner）
            float Y(float v) { return inner.yMax - (v - lo) / (hi - lo) * inner.height; }

            // 网格 + 右轴
            float step = NiceStep((hi - lo) / ColonyEconomyGridLevels);
            for (float g = Mathf.Ceil(lo / step) * step; g <= hi; g += step)
            {
                float gy = Mathf.Round(Y(g)) + ColonyEconomyHalfPixel;
                RUIF.Draw.Box(new Rect(inner.x, gy, inner.width, ColonyEconomyLineWidth),
                    UITheme.WithAlpha(UITheme.Text, ColonyEconomyGridAlpha));
                // ⚠ 文本矩形高 ≥ 引擎 Tiny 行高（LAND 铁律；低于 18 会压字/裁切）
                UIComponents.Label(new Rect(inner.xMax + ColonyEconomyHeatLabelInset, gy - ColonyEconomyAxisLabelDy,
                    ColonyEconomyAxisLabelH, ColonyEconomyAxisLabelH), g.ToString("0"), GameFont.Tiny, UITheme.Dim);
            }

            float pitch = inner.width / candles.Count;
            float bodyWidth = Mathf.Clamp(Mathf.Floor(pitch * ColonyEconomyBodyWidthRatio),
                ColonyEconomyBodyWidthMin, ColonyEconomyBodyWidthMax);

            // 悬停命中（分片矩形 —— 坐标系平移下的唯一正确写法，见 Roster.DrawIdentityTrendHover）
            int hover = -1;
            for (int i = 0; i < candles.Count; i++)
            {
                Rect hit = new Rect(inner.x + pitch * i, inner.y, pitch, inner.height);
                if (Mouse.IsOver(hit))
                {
                    hover = i;
                    TooltipHandler.TipRegion(hit,
                        i < colonyStockCandleTip.Count ? colonyStockCandleTip[i] : OhlcText(candles[i]));
                }
            }
            colonyStockHoverIndex = hover;

            // 蜡烛：影线（高↔低）在下、实体（开↔收）在上；doji 最小实体 2px
            for (int i = 0; i < candles.Count; i++)
            {
                ColonyStockCandle c = candles[i];
                float cx = Mathf.Round(inner.x + pitch * (i + ColonyEconomyHalfPixel));
                Color color = c.Up ? UITheme.Alive : UITheme.Dead;
                float yHigh = Y(c.High), yLow = Y(c.Low);
                float yOpen = Y(c.Open), yClose = Y(c.Close);
                RUIF.Draw.Box(new Rect(cx - ColonyEconomyHalfPixel, yHigh, ColonyEconomyLineWidth,
                    Mathf.Max(ColonyEconomyLineWidth, yLow - yHigh)), color);
                float top = Mathf.Min(yOpen, yClose);
                float bodyH = Mathf.Max(ColonyEconomyDojiMinBody, Mathf.Round(Mathf.Abs(yClose - yOpen)));
                RUIF.Draw.Box(new Rect(Mathf.Round(cx - bodyWidth / 2f), Mathf.Round(top), bodyWidth, bodyH), color);
            }

            // 悬停十字线（竖 = 周期定位；横 = 该周期收盘价）
            if (colonyStockHoverIndex >= 0 && colonyStockHoverIndex < candles.Count)
            {
                float hx = Mathf.Round(inner.x + pitch * (colonyStockHoverIndex + ColonyEconomyHalfPixel));
                RUIF.Draw.Box(new Rect(hx - ColonyEconomyHalfPixel, inner.y, ColonyEconomyLineWidth, inner.height),
                    UITheme.WithAlpha(UITheme.Text, ColonyEconomyCursorAlpha));
                float hy = Mathf.Round(Y(candles[colonyStockHoverIndex].Close));
                RUIF.Draw.Box(new Rect(inner.x, hy, inner.width, ColonyEconomyLineWidth),
                    UITheme.WithAlpha(UITheme.Text, ColonyEconomyCursorCrossAlpha));
            }

            // 最新收盘价参考线（IMGUI 无虚线 ⇒ 半透明实线，已登记）+ 右侧 tag
            ColonyStockCandle last = candles[candles.Count - 1];
            float ly = Mathf.Round(Y(last.Close));
            RUIF.Draw.Box(new Rect(inner.x, ly, inner.width, ColonyEconomyLineWidth),
                UITheme.WithAlpha(UITheme.Text, ColonyEconomyLastAlpha));
            Rect tag = new Rect(inner.xMax + ColonyEconomyTagInset, ly - ColonyEconomyAxisLabelDy,
                ColonyEconomyPlotPadR - ColonyEconomyTagInset - ColonyEconomyHeatLabelInset, ColonyEconomyTagH);
            RUIF.Draw.Box(tag, UITheme.WithAlpha(UITheme.Text, ColonyEconomyTagAlpha));
            UIComponents.Label(tag, last.Close.ToString("0"), GameFont.Tiny, UITheme.Text);

            // OHLC 读数（悬停优先，否则最新）——**仅在悬停项或快照变化时重建字符串**（绘制期零拼接）
            if (colonyStockReadoutHover != colonyStockHoverIndex || colonyStockReadoutRev != gallery.BuiltFromRevision)
            {
                ColonyStockCandle readout = colonyStockHoverIndex >= 0 && colonyStockHoverIndex < candles.Count
                    ? candles[colonyStockHoverIndex]
                    : last;
                colonyStockReadoutText = (colonyStockHoverIndex >= 0
                        ? Translate("PersonalChronicle.Economy.Stock.Hover")
                        : Translate("PersonalChronicle.Economy.Stock.Last"))
                    + " · " + OhlcText(readout);
                colonyStockReadoutHover = colonyStockHoverIndex;
                colonyStockReadoutRev = gallery.BuiltFromRevision;
            }
            UIComponents.Label(new Rect(inner.x + ColonyEconomyReadoutInset, inner.y + ColonyEconomyReadoutInset,
                inner.width - ColonyEconomyReadoutInset * 2f, ColonyEconomyReadoutH),
                colonyStockReadoutText, GameFont.Tiny, UITheme.Dim);
        }

        /// <summary>周期档位条（RUIF `NavAtomsB.LayerSwitch`；标签数组缓存，禁每帧分配）。</summary>
        private void DrawTierTabs(Rect toolbar, ColonyStockGallerySnapshot gallery)
        {
            string[] labels = EnsureTierLabels(gallery);
            if (labels == null || labels.Length == 0)
            {
                return;
            }
            float tabWidth = Mathf.Min(ColonyEconomyTabWidthMax,
                Mathf.Max(ColonyEconomyTabWidthMin, toolbar.width / (labels.Length + 2f)));
            Rect tabs = new Rect(toolbar.xMax - tabWidth * labels.Length, toolbar.y,
                tabWidth * labels.Length, toolbar.height);

            int selected = gallery.SelectedTier;
            RUIF.NavAtomsB.LayerSwitch(tabs, labels, ref selected);
            if (selected != gallery.SelectedTier)
            {
                colonyStockTierIndex = selected;
                colonyStockDefName = gallery.SelectedDef;
                colonyStockHoverIndex = -1;
                ForceRefresh();
            }
        }

        /// <summary>档位标签数组缓存（档位数或语言变化才重建 —— 绘制路径零 `new`）。</summary>
        private string[] EnsureTierLabels(ColonyStockGallerySnapshot gallery)
        {
            LoadedLanguage lang = LanguageDatabase.activeLanguage;
            int count = gallery.TierLabels.Count;
            if (colonyStockTierLabels == null || colonyStockTierLabelCount != count
                || !ReferenceEquals(colonyStockTierLabelLang, lang))
            {
                colonyStockTierLabels = new string[count];
                for (int i = 0; i < count; i++)
                {
                    colonyStockTierLabels[i] = gallery.TierLabels[i];
                }
                colonyStockTierLabelCount = count;
                colonyStockTierLabelLang = lang;
            }
            return colonyStockTierLabels;
        }

        /// <summary>
        /// 重建绘制期文本缓存（PERF-001）：列表数量/提示、树图百分比/提示、蜡烛提示全部**构建期拼好**，
        /// 绘制路径只做取值 ⇒ 消除每帧 `ToString`/字符串拼接（GATE-PERF E2）。
        /// 失效条件 = 快照 `BuiltFromRevision` 变化，或条目数变化（语言切换由快照重建覆盖）。
        /// </summary>
        private void EnsureStockTextCache(ColonyStockGallerySnapshot gallery)
        {
            if (colonyStockTextRev == gallery.BuiltFromRevision
                && colonyStockQtyText.Count == gallery.Items.Count
                && colonyStockCandleTip.Count == gallery.Candles.Count)
            {
                return;
            }
            colonyStockTextRev = gallery.BuiltFromRevision;
            colonyStockQtyText.Clear();
            colonyStockPickTip.Clear();
            colonyStockPctText.Clear();
            colonyStockHeatTip.Clear();
            colonyStockCandleTip.Clear();

            string valueWord = Translate("PersonalChronicle.Economy.Stock.Value");
            string deltaWord = Translate("PersonalChronicle.Economy.Stock.Delta");
            string sourceWord = Translate("PersonalChronicle.Economy.Stock.Source");
            for (int i = 0; i < gallery.Items.Count; i++)
            {
                ColonyStockItemRow row = gallery.Items[i];
                colonyStockQtyText.Add(row.Quantity.ToString());
                colonyStockPickTip.Add(row.Label + " · " + valueWord + " " + row.Value.ToString("0"));
                colonyStockPctText.Add(row.HasDelta
                    ? ((row.DeltaPct > 0f ? "+" : (row.DeltaPct < 0f ? "-" : "±"))
                       + Mathf.Abs(row.DeltaPct).ToString("0.0") + "%")
                    : "—");
                colonyStockHeatTip.Add(row.Label + " · " + valueWord + " " + row.Value.ToString("0")
                    + " · " + deltaWord + " " + (row.HasDelta ? row.DeltaPct.ToString("0.0") + "%" : "—"));
            }
            for (int i = 0; i < gallery.Candles.Count; i++)
            {
                // 半角括号：全角括号在英文下属未本地化可见串（GATE-LOC P2）
                colonyStockCandleTip.Add(OhlcText(gallery.Candles[i]) + " (" + sourceWord + ")");
            }
            colonyStockReadoutHover = -2;   // 读数缓存随 revision 失效
        }

        /// <summary>选中品种（写瞬时态 + 立即重建，走既有 ForceRefresh 钩子）。</summary>
        private void SelectStockItem(ColonyStockGallerySnapshot gallery, string defName)
        {
            if (string.IsNullOrEmpty(defName)
                || string.Equals(defName, gallery.SelectedDef, System.StringComparison.Ordinal))
            {
                return;
            }
            colonyStockDefName = defName;
            colonyStockTierIndex = gallery.SelectedTier;
            colonyStockHoverIndex = -1;
            ForceRefresh();
        }

        private string OhlcText(ColonyStockCandle c)
        {
            return Translate("PersonalChronicle.Economy.Stock.Ohlc.Open") + " " + c.Open.ToString("0")
                + " · " + Translate("PersonalChronicle.Economy.Stock.Ohlc.High") + " " + c.High.ToString("0")
                + " · " + Translate("PersonalChronicle.Economy.Stock.Ohlc.Low") + " " + c.Low.ToString("0")
                + " · " + Translate("PersonalChronicle.Economy.Stock.Ohlc.Close") + " " + c.Close.ToString("0");
        }

        /// <summary>取整到 1/2/5 × 10^n（网格步长）。</summary>
        private static float NiceStep(float raw)
        {
            if (raw <= 0f)
            {
                return 1f;
            }
            float p = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(raw)));
            float m = raw / p;
            float s = m <= 1f ? 1f : (m <= 2f ? 2f : (m <= 5f ? 5f : 10f));
            return s * p;
        }

        // ================================================================
        // 视图 ②：热力图（分组 squarified 树图；布局自持 = EXC-RUIF-001）
        // ================================================================
        private void DrawHeatView(Rect body, ColonyStockGallerySnapshot gallery)
        {
            EnsureStockTextCache(gallery);
            List<ColonyStockItemRow> items = new List<ColonyStockItemRow>();
            for (int i = 0; i < gallery.Items.Count && items.Count < ColonyEconomyHeatMaxItems; i++)
            {
                items.Add(gallery.Items[i]);
            }
            if (items.Count == 0)
            {
                RUIF.FeedbackAtoms.EmptyState(body, Translate("PersonalChronicle.Economy.Stock.Empty"),
                    Translate("PersonalChronicle.Economy.Stock.Source"));
                return;
            }

            // 分组（一级类目；无类目 ⇒ "其他"）
            List<string> groups = new List<string>();
            Dictionary<string, List<ColonyStockItemRow>> byGroup = new Dictionary<string, List<ColonyStockItemRow>>();
            Dictionary<string, List<int>> byGroupIndex = new Dictionary<string, List<int>>();
            Dictionary<string, float> groupWeight = new Dictionary<string, float>();
            string other = Translate("PersonalChronicle.Economy.Stock.Other");
            float totalWeight = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                string key = string.IsNullOrEmpty(items[i].CategoryLabel) ? other : items[i].CategoryLabel;
                List<ColonyStockItemRow> list;
                if (!byGroup.TryGetValue(key, out list))
                {
                    list = new List<ColonyStockItemRow>();
                    byGroup.Add(key, list);
                    byGroupIndex.Add(key, new List<int>());
                    groups.Add(key);
                    groupWeight.Add(key, 0f);
                }
                list.Add(items[i]);
                byGroupIndex[key].Add(i);   // 保留原始索引 ⇒ 取绘制期文本缓存
                groupWeight[key] = groupWeight[key] + items[i].Value;
                totalWeight += items[i].Value;
            }

            // ⚠ 零权重兜底（2026-09-21 审计：全 0 权重会让 LayoutTreemap 返回全 0 矩形 ⇒ 整页白屏）
            if (totalWeight <= 0f)
            {
                RUIF.FeedbackAtoms.EmptyState(body, Translate("PersonalChronicle.Economy.Stock.NoStock"),
                    Translate("PersonalChronicle.Economy.Stock.Source"));
                return;
            }

            float maxAbs = 1f;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].HasDelta)
                {
                    maxAbs = Mathf.Max(maxAbs, Mathf.Abs(items[i].DeltaPct));
                }
            }

            List<float> weights = new List<float>(groups.Count);
            for (int i = 0; i < groups.Count; i++)
            {
                weights.Add(groupWeight[groups[i]]);
            }
            List<Rect> groupRects = LayoutTreemap(weights, body.x, body.y, body.width, body.height);

            for (int g = 0; g < groups.Count; g++)
            {
                Rect rect = groupRects[g];
                if (rect.width < ColonyEconomyHeatGroupMinW
                    || rect.height < ColonyEconomyHeatHeader + ColonyEconomyHeatGroupMinHExtra)
                {
                    continue;
                }
                UIComponents.Label(new Rect(rect.x + ColonyEconomyHeatHeaderInset, rect.y + 1f,
                    Mathf.Max(ColonyEconomyHeatGroupMinW, rect.width - ColonyEconomyHeatHeaderInset * 2f),
                    ColonyEconomyHeatHeader), groups[g], GameFont.Tiny, UITheme.Dim);

                List<ColonyStockItemRow> list = byGroup[groups[g]];
                List<float> innerWeights = new List<float>(list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    innerWeights.Add(Mathf.Max(ColonyEconomyHeatMinWeight, list[i].Value));
                }
                List<Rect> cellRects = LayoutTreemap(innerWeights, rect.x, rect.y + ColonyEconomyHeatHeader,
                    rect.width, rect.height - ColonyEconomyHeatHeader);

                List<int> indexList = byGroupIndex[groups[g]];
                for (int i = 0; i < list.Count; i++)
                {
                    DrawHeatCell(cellRects[i], list[i], maxAbs, gallery, indexList[i]);
                }
            }
        }

        /// <summary>单个树图格：色阶 = 选中档窗口涨跌幅（sqrt 曲线）；标签自适应；点击选中。</summary>
        private void DrawHeatCell(Rect rect, ColonyStockItemRow row, float maxAbs, ColonyStockGallerySnapshot gallery,
            int itemIndex)
        {
            if (rect.width < ColonyEconomyHeatLeafMin || rect.height < ColonyEconomyHeatLeafMin)
            {
                return;
            }
            bool selected = string.Equals(row.DefName, gallery.SelectedDef, System.StringComparison.Ordinal);
            Color hue = row.DeltaPct >= 0f ? UITheme.Alive : UITheme.Dead;
            float alpha = ColonyEconomyHeatAlphaBase + ColonyEconomyHeatAlphaSpan
                * Mathf.Sqrt(Mathf.Clamp01(Mathf.Abs(row.DeltaPct) / maxAbs));
            RUIF.Draw.Box(rect, UITheme.WithAlpha(hue, alpha));
            if (selected || Mouse.IsOver(rect))
            {
                RUIF.Draw.Box(rect, UITheme.WithAlpha(UITheme.Text,
                    selected ? ColonyEconomyHeatSelectedAlpha : ColonyEconomyHeatHoverAlpha));
            }

            // 标签自适应（主流行为：空间不足则降级；微格仅 tooltip）；宽度用 RUIF 度量精算
            float nameWidth = RUIF.Measure.Width(row.Label, GameFont.Tiny);
            bool showName = rect.height >= ColonyEconomyHeatNameMinH
                && rect.width >= nameWidth + ColonyEconomyHeatLabelPad;
            bool showPct = rect.height >= ColonyEconomyHeatPctMinH && rect.width >= ColonyEconomyHeatPctMinW;
            if (showName)
            {
                UIComponents.Label(new Rect(rect.x + ColonyEconomyHeatLabelInset, rect.y + 1f,
                    rect.width - ColonyEconomyHeatLabelInset * 2f, ColonyEconomyHeatHeader),
                    row.Label, GameFont.Tiny, UITheme.Text);
            }
            if (showPct)
            {
                UIComponents.Label(new Rect(rect.x + ColonyEconomyHeatLabelInset,
                    rect.yMax - ColonyEconomyHeatPctBottom, rect.width - ColonyEconomyHeatLabelInset * 2f,
                    ColonyEconomyHeatHeader),
                    itemIndex >= 0 && itemIndex < colonyStockPctText.Count ? colonyStockPctText[itemIndex] : "—",
                    GameFont.Tiny, UITheme.Text);
            }
            TooltipHandler.TipRegion(rect,
                itemIndex >= 0 && itemIndex < colonyStockHeatTip.Count
                    ? colonyStockHeatTip[itemIndex]
                    : row.Label);
            if (Widgets.ButtonInvisible(rect))
            {
                SelectStockItem(gallery, row.DefName);
            }
        }

        /// <summary>
        /// squarified 树图布局（**布局自持 = `EXC-RUIF-001`**：RUIF 无 treemap 原子；
        /// 绘制原语已全部走 `RUIF.Draw`/令牌）。返回与 <paramref name="weights"/> 同序的矩形。
        /// </summary>
        private static List<Rect> LayoutTreemap(List<float> weights, float x, float y, float w, float h)
        {
            List<Rect> rects = new List<Rect>(weights.Count);
            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                total += Mathf.Max(0f, weights[i]);
                rects.Add(new Rect(x, y, 0f, 0f));
            }
            if (weights.Count == 0 || total <= 0f || w <= 0f || h <= 0f)
            {
                return rects;
            }

            float scale = w * h / total;
            int index = 0;
            float cx = x, cy = y, cw = w, ch = h;
            while (index < weights.Count)
            {
                float shortSide = Mathf.Min(cw, ch);
                float rowSum = 0f;
                int rowEnd = index;
                float best = float.MaxValue;
                while (rowEnd < weights.Count)
                {
                    float newSum = rowSum + Mathf.Max(0f, weights[rowEnd]) * scale;
                    float thick = newSum / shortSide;
                    float worst = 0f;
                    for (int k = index; k <= rowEnd; k++)
                    {
                        float side = Mathf.Max(0f, weights[k]) * scale / thick;
                        float ar = side > thick ? side / thick : thick / side;
                        if (ar > worst) worst = ar;
                    }
                    if (worst <= best)
                    {
                        best = worst;
                        rowSum = newSum;
                        rowEnd++;
                    }
                    else
                    {
                        break;
                    }
                }
                if (rowEnd == index)
                {
                    rowSum += Mathf.Max(0f, weights[index]) * scale;
                    rowEnd = index + 1;
                }

                float rowThick = rowSum / shortSide;
                float pos = 0f;
                for (int k = index; k < rowEnd; k++)
                {
                    float side = Mathf.Max(0f, weights[k]) * scale / rowThick;
                    if (cw >= ch)
                    {
                        rects[k] = new Rect(cx, cy + pos, rowThick, side);
                    }
                    else
                    {
                        rects[k] = new Rect(cx + pos, cy, side, rowThick);
                    }
                    pos += side;
                }
                if (cw >= ch)
                {
                    cx += rowThick;
                    cw -= rowThick;
                }
                else
                {
                    cy += rowThick;
                    ch -= rowThick;
                }
                index = rowEnd;
            }
            return rects;
        }
    }
}

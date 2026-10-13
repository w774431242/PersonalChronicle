using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Automation;
using RimWorld;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    /// <summary>
    /// 设备详情**居中弹窗**（对齐预览 <c>#drawer</c>）。
    ///
    /// <para><b>LAND-001 实测</b>：预览最终版把抽屉改成了居中模态 ——
    /// <c>#drawer{position:fixed;left:50%;top:50%;width:min(960px,94vw);max-height:88vh;transform:translate(-50%,-50%);border:2px solid var(--line2);border-radius:12px}</c>
    /// <c>#drawer::before{top:0;left:0;right:0;height:7px;background:var(--hz)}</c>（黄黑警示条）
    /// <c>#dbody{overflow-y:auto;padding:24px 28px 28px}</c>（HTML:483 终值）
    /// <c>.mkpi{grid-template-columns:repeat(6,1fr);gap:9px}</c>
    /// ⇒ 弹窗宽 = min(960, 94% 视口)，高 = min(内容, 88% 视口)，顶部 7px 警示条，底部可滚动。</para>
    ///
    /// <para><b>引擎降级（LAND-003）</b>：圆角/阴影/缩放过渡在 IMGUI 下不可得 —— 用直角 + 双层描边
    /// 表示"浮起"，不做假动画。字体只有 Tiny/Small/Medium 三档，预览的 19px/9.5px 映射到
    /// Small/Tiny（LAND-007 按真实行高排行，放不下的行进 tooltip）。</para>
    ///
    /// <para><b>数据来源</b>：<see cref="FloorDeviceDetailSnapshot"/>；本类不重算任何阈值，
    /// 只做「值 → 位置」与「语气 → 颜色」映射（LAY-001）。</para>
    /// </summary>
    internal sealed partial class FloorDeviceDetailWindow : Window
    {
        // ---- 预览常量（见类注释的 CSS 行号）----

        private const float PreviewMaxWidth = 960f;
        private const float PreviewWidthRatio = 0.94f;
        private const float PreviewMaxHeightRatio = 0.88f;
        /// <summary>
        /// 窗口高度下限（内容极少时仍要放得下标题带 + 页签条 + 关闭按钮，不是一个薄片）。
        /// S1 几何修复（2026-09-23）：预览的 <c>max-height</c> 是**上限**，不是固定高。
        /// </summary>
        private const float MinWindowHeight = 260f;
        // 诊断实证（2026-10-01）：window=491 而 DoWindowContents 的 inRect 只有 451
        // ⇒ RimWorld 窗口自身扣 ~40 屏幕 px（@uiScale1.5 ≈ 27 逻辑 px）。
        // 补偿 38 逻辑 px ⇒ body ≥ content，滚动条消失；诊断行持续验证。
        private const float WindowChromeAllowance = 38f;
        /// <summary>预览 <c>#drawer::before{height:7px}</c>——顶部黄黑警示条。</summary>
        private const float HazardHeight = 7f;
        /// <summary>预览 <c>#dbody{padding:24px 28px 28px}</c>（HTML:483 后置覆盖=渲染终值；CHG-20261002-UIAUDIT 对齐）。</summary>
        private const float BodyPadTop = 24f;
        private const float BodyPadX = 28f;
        private const float BodyPadBottom = 28f;
        /// <summary>预览 <c>.mkpi{gap:8px}</c>（HTML:450 密度压缩终值；CHG-20261002-UIAUDIT 对齐）。</summary>
        private const float MetricGap = 8f;
        /// <summary>指标格**列数上限**（预览 6 格模式 <c>.mkpi.mk6</c>；实际列数另见 <see cref="MetricColumnCount"/>）。</summary>
        private const int MetricColumns = 6;
        /// <summary>窄档列数上限（预览 <c>#drawer.narrow .mkpi{repeat(3,1fr)}</c>，HTML:147）。</summary>
        private const int MetricColumnsNarrow = 3;
        /// <summary>窄档 + ≤4 格时的列数（预览 <c>.narrow .mkpi.mk4{repeat(2,1fr)}</c>，HTML:148）。</summary>
        private const int MetricColumnsNarrowSmall = 2;

        /// <summary>
        /// CHG-20261002-FLOORDETAIL-AUDIT：**指标格列数 = min(上限, 指标数)**。
        ///
        /// <para>预览默认是 **4 格**模式（<c>.mkpi{grid-template-columns:repeat(4,1fr)}</c>，HTML:167），
        /// 6 格要切 <c>.mk6</c>（工具栏/6 项真值）。旧实现恒按 6 列排 **4** 张卡
        /// ⇒ 每卡只剩 1/6 宽、KPI 行右侧空出约 1/3（2026-10-02 实机截图实证）。</para>
        /// </summary>
        private static int MetricColumnCount(int metricCount, bool wide)
        {
            if (metricCount <= 0)
            {
                return 1;
            }
            if (wide)
            {
                return Mathf.Clamp(metricCount, 1, MetricColumns);
            }
            return Mathf.Clamp(metricCount, 1,
                metricCount <= 4 ? MetricColumnsNarrowSmall : MetricColumnsNarrow);
        }

        // ---- 指标卡内几何（CHG-20261002-FLOORDETAIL-AUDIT）----
        // ⚠ 全部**按字体档真值派生**，不得手填像素（LAND-007）：
        //   旧实现写死「数值 20px 高 / 标签 14px 高」，而 Tiny 行高 18、Medium 更高
        //   ⇒ 中文标签下半截被 GUI.Label 按 rect 裁掉（2026-10-02 实机截图实证，四张卡齐发）。
        /// <summary>卡内横向内缩（预览 <c>.mkpi>div{padding:8px 10px}</c>，HTML:169）。</summary>
        private const float MetricCardPadX = 10f;
        /// <summary>卡内纵向上/下内缩（按字体档反推：卡高 = 2×PadY + 数值行高 + 标签行高）。</summary>
        private const float MetricCardPadY = 4f;

        /// <summary>指标卡**数值字体档**：预览 <c>.mkpi .v{font-size:21px}</c> ⇒ LAND-007 映射 18–22px = <c>Medium</c>（主题令牌 <see cref="UITheme.FontValue"/>）。</summary>
        private static GameFont MetricValueFont
        {
            get { return UITheme.FontValue; }
        }

        private static float MetricValueLineHeight
        {
            get { return Text.LineHeightOf(MetricValueFont); }
        }

        private static float MetricLabelLineHeight
        {
            get { return Text.LineHeightOf(GameFont.Tiny); }
        }

        /// <summary>指标卡高 = 上下内缩 + 数值行高 + 标签行高（与 <c>DrawMetrics</c> 同式；测量侧同用本属性）。</summary>
        private static float MetricCardHeight
        {
            get { return MetricCardPadY * 2f + MetricValueLineHeight + MetricLabelLineHeight; }
        }
        /// <summary>预览 <c>.dtab</c> 页签条高。</summary>
        private const float TabBarHeight = 26f;
        /// <summary>预览 <c>.drow2</c> 行高（10px 字 + 行距）。</summary>
        private const float RowHeight = 22f;   // 深度优化（2026-10-01 二轮）：18 → 22。
                                               // Tiny 字形 18 + 上下呼吸 4 ⇒ 键值行不再"挤兑"；
                                               // 测量（MeasureSectionHeight）与绘制同用本常量 ⇒ 口径恒等。
        /// <summary>小标题 <c>.dsub</c> 行高（Tiny 18 + 呼吸 2）。</summary>
        private const float CaptionHeight = 20f;
        // S1（2026-09-23）：`TimelineHeight` / `TimelineCells` 与整条「状态时序」绘制已删除
        //   （40 格只有 1 段真数据 ⇒ 噪音；当前状态已由标题带状态灯表达）。

        /// <summary>
        /// ⑥「契约」页的页签下标。底部「数据层契约 + 工序导航」两栏**只在该页显示** ——
        /// 2026-09-12 用户实机反馈：此前它被无条件画在每个 tab 底部 ⇒ 界面脏、每个 tab 看起来都一样。
        /// </summary>
        // S2-1（2026-09-23）：`ContractTabIndex` 常量已删除 —— 契约页签只在开发模式出现，
        //   序号不再固定，UI 侧只按 `TabTitles` / `section.TabIndex` 工作（不硬编码页签下标）。

        /// <summary>
        /// 页签标题缓存（RUIF `NavAtoms.TabBar` 需要 `string[]`）。
        /// ⚠ 每帧 `List.ToArray()` 会产生托管分配 ⇒ **必须缓存**（PERF-001）；`ReplaceDetail` 时清空。
        /// </summary>
        private string[] tabTitleCache;

        /// <summary>RUIF 页签指示条位置（由 `NavAtoms.TabBar` 以 ref 持有；窗口瞬时态）。</summary>
        private float tabIndicatorX;

        private FloorDeviceDetailSnapshot detail;   // 非 readonly：⑦ 动作后由 ReplaceDetail 换新快照

        /// <summary>当前页签下标（窗口瞬时态，不入存档）。</summary>
        private int tabIndex;

        /// <summary>批次 5：追加生产的**数量**（窗口瞬时态；1 = 单件；>1 ⇒ 该单按 RepeatCount 模式加）。</summary>
        private int addCount = 1;

        /// <summary>内容滚动位置（内容高于 88vh 时可滚）。</summary>
        private Vector2 scroll;

        /// <summary>
        /// CHG-20261002-PRODCOLLAPSE：**工单摘要折叠态**（窗口瞬时字段；不入存档、不进 Def）。
        /// false = 折叠（默认，对齐预览初态：只显示 IsSummary 行）；true = 展开（全量行）。
        /// 当前仅生产页摘要块声明摘要行；若未来多个块可折叠，需按块拆分状态（登记为局限）。
        /// </summary>
        private bool billSummaryExpanded;

        // 深度检测（2026-10-01）：开窗布局实况 + 绘制/测量偏差各记一次（窗口瞬时态）。
        private bool openLogged;
        private bool parityLogged;
        private float drawnEndY;

        /// <summary>布局缓存：宽度变化才重算（LAND-011）。</summary>
        private float cachedWidth = -1f;
        private readonly List<float> sectionHeights = new List<float>();

        public FloorDeviceDetailWindow(FloorDeviceDetailSnapshot detail)
        {
            this.detail = detail;
            doWindowBackground = false;
            drawShadow = false;
            // S1（2026-09-23）：**唯一关闭入口** —— 本窗口自绘右上关闭按钮（贴预览 `#dclose`），
            //   原版 X 若保持默认 true 会与它并列成"双关闭"，视觉重复（审计列为界面噪音）。
            doCloseX = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            closeOnCancel = true;
            forcePause = false;
            draggable = false;
        }

        public override Vector2 InitialSize
        {
            get
            {
                // 预览：`width:min(960px,94vw)` / `max-height:88vh` ——
                //   ⚠ **`max-height` 是上限不是固定高**。旧实现取 `min(88vh, 屏高-80)` ⇒ 任何内容都撑到 88vh，
                //   内容少时下方留大片空白（用户实机反馈 2026-09-23；审计列为 LAND-003 违反项，S1 漏做）。
                //   现按内容实测高度夹在 [下限, 上限]，内容少则窗口矮（不再有空白），内容多则到 88vh 后滚动。
                float width = Mathf.Min(PreviewMaxWidth,
                    Verse.UI.screenWidth * PreviewWidthRatio);
                float cap = Mathf.Min(Verse.UI.screenHeight * PreviewMaxHeightRatio,
                    Verse.UI.screenHeight - 80f);
                bool wide = width >= 560f;
                // 内容宽度 = 窗口宽 − 左右内边距（与 `DoWindowContents` 的 `body.width` 同口径 ⇒
                // 尺寸缓存命中，不重复量一次）
                float content = MeasureContentHeight(width - BodyPadX * 2f, wide)
                    + BodyPadTop + BodyPadBottom + 2f
                    + WindowChromeAllowance;
                return new Vector2(width, Mathf.Clamp(content, MinWindowHeight, cap));
            }
        }

        /// <summary>是否应弹窗（无设备时不弹，调用方据此短路）。</summary>
        internal static bool ShouldOpen(FloorDeviceDetailSnapshot snapshot)
        {
            return snapshot != null && snapshot.HasDevice;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (detail == null || !detail.HasDevice)
            {
                Close();
                return;
            }

            bool wide = inRect.width >= 560f;

            // ---- 外框：预览 border:2px solid var(--line2) ----
            Widgets.DrawBoxSolid(inRect, UITheme.Window);
            UIComponents.Border(inRect, UITheme.BorderSoft);
            UIComponents.Border(inRect.ContractedBy(1f), UITheme.BorderSoft);

            // ---- 顶部黄黑警示条：预览 #drawer::before{height:7px;background:var(--hz)} ----
            // S1（2026-09-23）：**只在故障态显示** —— 常显会退化成纯装饰，真故障时反而失去警示力
            //   （审计列为噪音项）。
            // CHG-20261002-FLOORDETAIL-AUDIT：该条是**绝对定位覆盖层**（HTML:137-141
            //   `#drawer::before{position:absolute;top:0;left:0;right:0;height:7px;z-index:2}`
            //   + `no-hazard::before{display:none}`）⇒ **任何状态下都不占布局**：
            //   内容顶偏移恒为 `BodyPadTop`（24），窗口高也不含这 7px。
            //   旧实现把 7px 计入内容顶偏移（非故障态留空带、故障态内容反而下沉 7px）——
            //   两个方向都偏离预览（2026-10-02 实机截图 + 总控复核实证）。
            if (detail.StateKey == PersonalChronicle.Domain.EquipmentStateResolver.KeyFault)
            {
                DrawHazardStripe(new Rect(inRect.x, inRect.y, inRect.width, HazardHeight));
            }

            // ---- 关闭按钮：预览 #dclose{position:absolute;top:14px;right:14px} ----
            DrawCloseButton(inRect);

            // ---- 内容区：预览 #dbody{padding:24px 28px 28px}（HTML:483 终值）----
            Rect body = new Rect(
                inRect.x + BodyPadX,
                inRect.y + BodyPadTop,
                inRect.width - BodyPadX * 2f,
                inRect.height - BodyPadTop - BodyPadBottom);
            if (body.width <= 1f || body.height <= 1f)
            {
                return;
            }

            if (cachedWidth != body.width)
            {
                cachedWidth = body.width;
                RebuildSectionHeights(body.width);
            }

            // ---- 先量后画（LAND-011）----
            // S1-6（2026-09-23）：**降级优先于滚动** ——
            //   D3：内容超出可用高时先收窄内边距（四边 2px）再量一次（间距属设计量，可收）；
            //   D2：条目上限由 Provider 侧限制（随 S2/S3 各页内容重组落地，精确值待实机回归）；
            //   兜底：仍超出才开滚动 —— 绝不用裁剪：IMGUI 无自动裁剪，溢出必被后画元素遮盖（铁律 3）。
            float contentHeight = MeasureContentHeight(body.width, wide);
            if (contentHeight > body.height)
            {
                body = body.ContractedBy(2f);
                contentHeight = MeasureContentHeight(body.width, wide);   // 宽度变化 ⇒ 内部按新宽重算
            }
            // 深度检测（2026-10-01）：每次开窗输出一次布局实况到 Player.log ——
            //   核对"游戏加载的构建 / 实际窗口与内容尺寸"（实机"看起来没变化"排查）。
            if (!openLogged)
            {
                openLogged = true;
                Log.Message("[PersonalChronicle] 设备卡布局: window="
                    + windowRect.width.ToString("0") + "x" + windowRect.height.ToString("0")
                    + " body=" + body.width.ToString("0") + "x" + body.height.ToString("0")
                    + " content=" + contentHeight.ToString("0")
                    + " wide=" + wide + " tab=" + tabIndex
                    + " uiScale=" + Prefs.UIScale.ToString("0.00")
                    + " screen=" + Verse.UI.screenWidth + "x" + Verse.UI.screenHeight);
            }
            Rect view = body;
            bool scrollOn = contentHeight > body.height;
            if (scrollOn)
            {
                Rect scrollInner = new Rect(0f, 0f, body.width - 16f, contentHeight);
                Widgets.BeginScrollView(body, ref scroll, scrollInner);
                view = scrollInner;
            }
            // 裁剪栈配对进 finally：任一面板异常也保证 EndScrollView 被调用，不污染整窗渲染栈（UI-A · CHG-MFG-AUDIT-001）
            try
            {
                float y = view.y;
                y = DrawHeader(new Rect(view.x, y, view.width, 0f), y, view.width);
                y = DrawMetrics(new Rect(view.x, y, view.width, 0f), y, view.width, wide);
                y = DrawTabBar(new Rect(view.x, y, view.width, 0f), y, view.width);
                y = DrawTabBody(new Rect(view.x, y, view.width, 0f), y, view.width, wide);
                drawnEndY = y;
                // S2-1（2026-09-23）：旧「底部契约栏」（`ContractRows` + `DrawContract`）已删除 ——
                //   其数据与「⑥ 契约」页签完全重复（同一信息只出现一次）；契约内容现仅随该页签的
                //   section 绘制，且该页签**只在开发模式可见**。
            }
            finally
            {
                if (scrollOn)
                {
                    Widgets.EndScrollView();
                }
            }

            // 深度检测：绘制终点 vs 测量高 —— 偏差 >6px 说明测量口径与绘制口径脱节
            //（这是"窗口开矮 / 滚动条异常 / 底部截断"的直接证据），一次性写日志。
            if (!parityLogged)
            {
                parityLogged = true;
                float diff = drawnEndY - view.y - contentHeight;
                if (Mathf.Abs(diff) > 6f)
                {
                    Log.Error("[PersonalChronicle] 设备卡绘制/测量偏差 "
                        + diff.ToString("0.#") + "px (drawn=" + drawnEndY.ToString("0.#")
                        + " measured=" + contentHeight.ToString("0.#") + ") tab=" + tabIndex);
                }
            }
        }

        // ---- ① 标题带：预览 .dhead ----

        private float DrawHeader(Rect rect, float y, float width)
        {
            // 标题行：设备名（预览 h2 = 13px 加粗）。
            // 深度优化（2026-10-01）：标题用 Small（行高真值 22）—— 旧实现塞进 18px 行
            // ⇒ 下缘溢出 4px 被身份行压住（铁律 1）。测量带同步 +4（见 MeasureContentHeight）。
            // DevMode：右上角叠加**构建时间戳** —— 截图即可核对"加载的是哪份 DLL"
            //（此前多次"改了没变化"实为旧进程/旧副本，见 reference_deploy_path 记忆）。
            float titleWidth = width;
            if (Prefs.DevMode)
            {
                UIComponents.Label(new Rect(rect.x + width - 140f, y, 136f, 22f),
                    "build " + BuildStamp(), GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleRight);
                titleWidth = width - 140f;
            }
            UIComponents.Label(new Rect(rect.x, y, titleWidth, 22f),
                UIComponents.TruncateToWidth(HeaderTitle(), titleWidth, GameFont.Small),
                GameFont.Small, UITheme.Accent);
            y += 22f + 2f;

            // 身份行：预览 .did（车间 · 档位），字号 10px → Tiny
            if (!string.IsNullOrEmpty(detail.Identity))
            {
                UIComponents.Label(new Rect(rect.x, y, width, RowHeight),
                    UIComponents.TruncateToWidth(detail.Identity, width, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim);
                y += RowHeight;
            }

            // 状态行：预览 .dstate（整行色带 + 色点 + 状态文案 + 右挂节拍）
            // CHG-20261002-UIAUDIT：对齐 HTML:157-163（背景 14% 状态色 + 45% 描边）与 :363-364
            //   （.pulsedot 9×9、距左 9px）；呼吸动画/圆角为引擎不可得（LAND-003，受控）。
            Rect stateRow = new Rect(rect.x, y, width, RowHeight);
            Color stateColor = StateColor(detail.StateKey);
            Widgets.DrawBoxSolid(stateRow, UITheme.WithAlpha(stateColor, 0.14f));
            UIComponents.Border(stateRow, UITheme.WithAlpha(stateColor, 0.45f));
            float dot = 9f;
            Widgets.DrawBoxSolid(new Rect(stateRow.x + 9f, stateRow.y + (RowHeight - dot) * 0.5f, dot, dot),
                stateColor);
            float textX = stateRow.x + 9f + dot + 6f;
            float cycleWidth = string.IsNullOrEmpty(detail.CycleText)
                ? 0f
                : MeasureWidth(detail.CycleText, GameFont.Tiny);
            float stateWidth = width - (textX - rect.x) - cycleWidth - 8f;
            if (stateWidth > 20f)
            {
                UIComponents.Label(new Rect(textX, stateRow.y, stateWidth, RowHeight),
                    UIComponents.TruncateToWidth(detail.StateText, stateWidth, GameFont.Tiny),
                    GameFont.Tiny, StateColor(detail.StateKey));
            }
            if (cycleWidth > 0f)
            {
                UIComponents.Label(new Rect(rect.xMax - cycleWidth, stateRow.y, cycleWidth, RowHeight),
                    detail.CycleText, GameFont.Tiny, UITheme.Text);
            }
            return y + RowHeight + 10f;
        }

        private string HeaderTitle()
        {
            string name = detail.Name ?? string.Empty;
            return string.IsNullOrEmpty(detail.DeviceId)
                ? name
                : detail.DeviceId + " " + name;
        }

        /// <summary>构建时间戳（进程级缓存一次）：DevMode 卡头右上显示，核对加载的 DLL。</summary>
        private static string buildStamp;

        private static string BuildStamp()
        {
            if (buildStamp != null)
            {
                return buildStamp;
            }
            // Prepatcher/PurePatcher 内存加载 ⇒ Location 为空、Version 0.0.0.0（实机日志实证）
            // ⇒ 构建身份改读**编译期注入**的 BuildStamp.g.cs 常量（csproj WriteBuildStamp 每次构建重写）。
            try
            {
                string utc = PersonalChronicle.BuildInfo.BuildTimeUtc;
                buildStamp = utc.Length > 5 ? utc.Substring(5) : utc;   // "MM-dd HH:mm"
            }
            catch (System.Exception)
            {
                buildStamp = "?";
            }
            return buildStamp;
        }

        // ---- ② 指标栅格：预览 .mkpi{repeat(6,1fr);gap:9px} ----

        private float DrawMetrics(Rect rect, float y, float width, bool wide)
        {
            List<FloorDetailMetricView> metrics = detail.Metrics;
            if (metrics == null || metrics.Count == 0)
            {
                return y;
            }
            int columns = MetricColumnCount(metrics.Count, wide);
            float cardWidth = (width - MetricGap * (columns - 1)) / columns;
            if (cardWidth <= 10f)
            {
                return y;
            }
            // CHG-20261002-FLOORDETAIL-AUDIT：卡高**按字体档真值派生**（LAND-007），不再手填 52。
            float cardHeight = MetricCardHeight;
            float valueHeight = MetricValueLineHeight;
            float labelHeight = MetricLabelLineHeight;

            int rows = (metrics.Count + columns - 1) / columns;
            for (int i = 0; i < metrics.Count; i++)
            {
                int row = i / columns;
                int column = i % columns;
                Rect card = new Rect(rect.x + column * (cardWidth + MetricGap),
                    y + row * (cardHeight + MetricGap), cardWidth, cardHeight);
                Widgets.DrawBoxSolid(card, UITheme.PanelRaised);
                UIComponents.Border(card, UITheme.BorderHair);

                FloorDetailMetricView metric = metrics[i];
                // 数值：字体档 = Medium（预览 `.v{font-size:21px}` ⇒ LAND-007 18–22px 档；
                //   旧实现用 Small 属错档，与预览的"大数值"观感不符 —— 2026-10-02 实机截图）；
                //   不可得时用暗色「—」（LAND-009 视觉上也看得出缺口）。
                //   行高 = 该字档真实行高 ⇒ 字形不被 rect 裁切（LAND-008：截断另挂悬停）。
                Rect valueRect = new Rect(card.x + MetricCardPadX, card.y + MetricCardPadY,
                    card.width - MetricCardPadX * 2f, valueHeight);
                UIComponents.Label(valueRect,
                    UIComponents.TruncateToWidth(metric.ValueText, valueRect.width, MetricValueFont, valueRect),
                    MetricValueFont, metric.Available ? UITheme.Text : UITheme.Dim);
                Rect labelRect = new Rect(card.x + MetricCardPadX, valueRect.yMax,
                    card.width - MetricCardPadX * 2f, labelHeight);
                UIComponents.Label(labelRect,
                    UIComponents.TruncateToWidth(metric.Label, labelRect.width, GameFont.Tiny, labelRect),
                    GameFont.Tiny, UITheme.Dim);
            }
            return y + rows * (cardHeight + MetricGap) + 4f;
        }

        // ---- ③ 页签条：预览 .dtab ----

        private float DrawTabBar(Rect rect, float y, float width)
        {
            List<string> tabs = detail.TabTitles;
            if (tabs == null || tabs.Count == 0)
            {
                return y;
            }

            // 改造 P5/P7：页签条走 RUIF `NavAtoms.TabBar`（宽度自适应 + 溢出折叠 + 底部指示条）。
            //   标题数组**缓存**（无 Linq，避免每帧分配）；`ReplaceDetail` 时清空以跟随语言/内容变化。
            if (tabTitleCache == null || tabTitleCache.Length != tabs.Count)
            {
                tabTitleCache = new string[tabs.Count];
                for (int t = 0; t < tabs.Count; t++)
                {
                    tabTitleCache[t] = tabs[t];
                }
            }
            if (RuifBridge.TryTabBar(new Rect(rect.x, y, width, TabBarHeight - 4f),
                    tabTitleCache, ref tabIndex, ref tabIndicatorX))
            {
                return y + TabBarHeight + 6f;
            }

            float tabWidth = Mathf.Min(96f, width / tabs.Count);
            for (int i = 0; i < tabs.Count; i++)
            {
                Rect tab = new Rect(rect.x + i * (tabWidth + 4f), y, tabWidth, TabBarHeight - 4f);
                if (tab.xMax > rect.xMax)
                {
                    break;
                }
                bool on = i == tabIndex;
                if (on)
                {
                    Widgets.DrawBoxSolid(tab, UITheme.PanelRaised);
                    UIComponents.Border(tab, UITheme.Accent);
                }
                else
                {
                    UIComponents.Border(tab, UITheme.BorderHair);
                }
                if (Widgets.ButtonInvisible(tab))
                {
                    tabIndex = i;
                    scroll = Vector2.zero;
                }
                UIComponents.Label(tab,
                    UIComponents.TruncateToWidth(tabs[i], tab.width - 8f, GameFont.Tiny),
                    GameFont.Tiny, on ? UITheme.Accent : UITheme.Muted, TextAnchor.MiddleCenter);
            }
            return y + TabBarHeight + 6f;
        }

        // ---- ④ 页签内容：预览 .dsec ----

        // ---- ⑦ 生产列表（可写）几何 ----
        /// <summary>
        /// 行内文本行高 = **Tiny 真值**（<c>Text.LineHeightOf(GameFont.Tiny)</c>；中文字体实测 18，
        /// 与 <see cref="RowHeight"/> 的「18 字形 + 4 呼吸」同源）。**必须实测**，不得手填：
        /// 字体档位/语言变化会改行高，硬编码必然漂移（LAND-007）。
        /// </summary>
        private static readonly float QueueRowLineHeight = Text.LineHeightOf(GameFont.Tiny);

        /// <summary>
        /// CHG-20261002-FLOORDETAIL-AUDIT：工单行高 = **两行 Tiny + 呼吸**。
        ///
        /// <para>预览 `.billrow` 是两行结构（`.bl1` 配方 / `.bl2` 模式·指定人，HTML:213-222），
        /// 行高 22 是**CSS 10px 字号**下的值。IMGUI 最小字号 Tiny 行高 18 ⇒ 22px 行里
        /// 副行只有 11px、18px 字形溢出后被下一条行覆盖成「半截字」
        /// （2026-10-02 实机截图实证）。故按 LAND-007 用真实行高派生：2×18 + 2 呼吸 = 38。</para>
        /// </summary>
        private static readonly float QueueRowHeight = QueueRowLineHeight * 2f + 2f;

        /// <summary>行内文本块上缘内缩（对齐预览 `.bl1/.bl2` 的 1px 呼吸）。</summary>
        private const float QueueRowTextInset = 1f;
        // S6（2026-09-25 审计）：20px 放不下中文字形（"挂 / 复" 走 Small 默认字体）⇒ 提至 26px，
        //   行内按钮区随之加宽（文本区由公式自动回缩，不需另调）。
        //   深度优化（2026-10-02）：按钮高改由行高派生（≤ 单行高 + 呼吸），**垂直居中**于行 ——
        //   旧实现 `line.height - 2` 在两行行高下会长成一根"高条"（预览按钮是 20×26 的小方块）。
        private static readonly float QueueActionHeight = QueueRowLineHeight + 4f;
        private const float QueueButtonWidth = 26f;
        private const float QueueButtonGap = 2f;
        // S3-5d：派工按钮要放得下文字（"人" / "Assign"），单独加宽
        private const float QueueAssignWidth = 44f;
        // 批次 2（2026-09-23）：列表行新增**独立状态列**与**进度条**（预览 `billrow` 结构）
        private const float QueueStateWidth = 88f;
        private const float QueueProgressWidth = 64f;
        private const float QueueStateGap = 6f;

        /// <summary>
        /// ⑦ 生产列表（**本机专属**，预览 §5c-Q L15）：行 = 本机 BillStack 的每条 Bill。
        /// 行内动作 `↑ ↓ 挂/复 ✕` + 底部「＋ 追加生产」（FloatMenu 选配方）。
        /// <para>**只读快照 + 动作回传**：本方法不直接改游戏状态，一律经
        /// <see cref="WorkshopBillActions"/>（原版 API：Reorder / suspended / Delete / AddBill）。</para>
        /// </summary>
        private float DrawBillQueueSection(Rect rect, float y, float width, FloorDetailSectionView section)
        {
            List<FloorBillRowView> rows = section.BillRows;
            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    FloorBillRowView row = rows[i];
                    Rect line = new Rect(rect.x, y, width, QueueRowHeight);
                    if (row.Active)
                    {
                        Widgets.DrawBoxSolid(line, UITheme.WithAlpha(UITheme.Alive, 0.10f));
                    }
                    Color dot = row.Active ? UITheme.Alive : (row.Suspended ? UITheme.Dim : UITheme.Threat);
                    Widgets.DrawBoxSolid(new Rect(line.x + 2f, line.y + line.height * 0.5f - 3f, 6f, 6f), dot);

                    // 批次 2（2026-09-23）：行布局 = **[文本区][进度条][状态列][按钮区]**（预览 `billrow`）——
                    //   预览要求列表有进度条与独立状态列；原实现把状态塞进副行文本，
                    //   扫一眼分不出"这条在跑、在等、还是被挂了"。宽度从右往左分配，全部由常量派生。
                    // 批次 3（2026-09-23）：按钮**回到预览的 4 个**（↑ ↓ 挂起/恢复 ✕）——
                    //   派工改由**人员页排名行点击**承担（S3-5 已有），列表不再重复一个入口。
                    float actionsWidth = QueueButtonWidth * 4f + QueueButtonGap * 3f;
                    float stateX = line.xMax - 2f - actionsWidth - QueueStateGap - QueueStateWidth;
                    float barX = stateX - QueueStateGap - QueueProgressWidth;
                    float textWidth = Mathf.Max(20f, barX - (line.x + 12f) - QueueStateGap);
                    // A2（2026-09-25 审计）：截断挂悬停（配方名长名字常见，截断后要能读到全名）。
                    // CHG-20261002-FLOORDETAIL-AUDIT：两行各占**一个真实行高**（预览 .bl1/.bl2），
                    //   不再按行高比例切（比例切法在 IMGUI 字高下必然裁字）。
                    float textTop = line.y + QueueRowTextInset;
                    Rect recipeRect = new Rect(line.x + 12f, textTop, textWidth, QueueRowLineHeight);
                    UIComponents.Label(recipeRect,
                        UIComponents.TruncateToWidth(row.RecipeLabel, textWidth, GameFont.Tiny, recipeRect),
                        GameFont.Tiny, row.Suspended ? UITheme.Dim : UITheme.Text, TextAnchor.MiddleLeft);
                    // 副行：模式 · 已指定人员（**挂起已上移到状态列**，不在副行重复）
                    string sub = row.ModeText;
                    if (!string.IsNullOrEmpty(row.AssignedName))
                    {
                        sub += " · ◆ " + row.AssignedName;
                    }
                    Rect subRect = new Rect(line.x + 12f, textTop + QueueRowLineHeight, textWidth,
                        QueueRowLineHeight);
                    UIComponents.Label(subRect,
                        UIComponents.TruncateToWidth(sub, textWidth, GameFont.Tiny, subRect),
                        GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                    // A3（2026-09-25 审计）：`RecipeDefName` 是内部逻辑名 ⇒ 仅开发模式进提示，
                    //   玩家提示只给可读文本（GOV-009）。
                    TooltipHandler.TipRegion(line,
                        Prefs.DevMode && !string.IsNullOrEmpty(row.RecipeDefName)
                            ? row.RecipeDefName + " · " + sub
                            : sub);

                    // 进度条：仅"当前执行中"那条有真实值（`Progress01 < 0` 不画，不伪造 0%）。
                    // ⚠ 1.6 的 DrawBoxSolid 会重置 GUI.color ⇒ 显式设色并恢复。
                    if (row.Progress01 >= 0f)
                    {
                        Rect track = new Rect(barX, line.y + line.height * 0.5f - 3f,
                            QueueProgressWidth, 6f);
                        Color prevBar = GUI.color;
                        GUI.color = UITheme.BorderSoft;
                        Widgets.DrawBoxSolid(track, Color.white);
                        GUI.color = UITheme.Alive;
                        Widgets.DrawBoxSolid(new Rect(track.x, track.y,
                            track.width * Mathf.Clamp01(row.Progress01), track.height), Color.white);
                        GUI.color = prevBar;
                    }
                    // 状态列（独立成列、右对齐）
                    if (!string.IsNullOrEmpty(row.StateText))
                    {
                        Rect stateRect = new Rect(stateX, line.y, QueueStateWidth, line.height);
                        UIComponents.Label(stateRect,
                            UIComponents.TruncateToWidth(row.StateText, QueueStateWidth, GameFont.Tiny, stateRect),
                            GameFont.Tiny,
                            row.Suspended ? UITheme.Dim : (row.Active ? UITheme.Alive : UITheme.Muted),
                            TextAnchor.MiddleRight);
                    }

                    float bx = line.xMax - 2f - actionsWidth;
                    // CHG-20261002-FLOORDETAIL-AUDIT：按钮 = 单行高 + 呼吸，**垂直居中**（预览 .acts button
                    //   是 26×20 的小方块；旧式 line.height-2 在 38px 行上会拉成一根高条）。
                    float actionH = Mathf.Min(QueueActionHeight, line.height - 4f);
                    float actionY = line.y + (line.height - actionH) * 0.5f;
                    // 逐个接住返回值（= 上一格右边界 + gap）⇒ 间距连续，不靠手算补偿
                    // S6（2026-09-25 审计）："↑/↓" 在实机字库缺字形 ⇒ 渲染成豆腐块 / 错字。
                    //   改用工程内已在用的几何三角（▲ / ▼，见 MasterControlTabWindow.Roster）。
                    bx = DrawQueueAction(new Rect(bx, actionY, QueueButtonWidth, actionH),
                        "▲", section.BenchStableId, WorkshopBillActions.ActMoveUp, i);
                    bx = DrawQueueAction(new Rect(bx, actionY, QueueButtonWidth, actionH),
                        "▼", section.BenchStableId, WorkshopBillActions.ActMoveDown, i);
                    bx = DrawQueueAction(new Rect(bx, actionY, QueueButtonWidth, actionH),
                        Translate(row.Suspended
                            ? "PersonalChronicle.UI.Floor.Detail.Queue.Resume"
                            : "PersonalChronicle.UI.Floor.Detail.Queue.Suspend"),
                        section.BenchStableId, WorkshopBillActions.ActToggleSuspend, i);
                    // 批次 3：原「指派」按钮已移除（预览列表为 4 按钮）—— 派工入口 = 人员页排名行点击。
                    //   `OpenAssignMenu`（FloatMenu 选人）**保留备用**：它对应原版工单配置页的「工人」下拉，
                    //   未来若做"按条派工"（不同工单派不同人）可直接复用，不再重写一份。
                    DrawQueueAction(new Rect(bx, actionY, QueueButtonWidth, actionH),
                        "✕", section.BenchStableId, WorkshopBillActions.ActDelete, i);
                    y += QueueRowHeight + 2f;
                }
            }

            // 批次 5（2026-09-23）：**数量输入**（预览 `addrow` = 数量 + ＋追加）——
            //   配方选择仍走原版 FloatMenu（RUIF 无下拉控件）；数量优先用真实 RUIF 原子
            //   `InputAtoms.Stepper`（经桥接）。
            // CHG-20261002-FLOORDETAIL-AUDIT：**补上一直缺失的回落分支** ——
            //   桥注释写"RUIF 缺失 ⇒ 调用方回落原版 ButtonText 加减"，但调用点从未实现，
            //   RUIF 探测失败时整块数量控件不显示（2026-10-02 实机截图：追加行只有裸按钮，
            //   玩家无法改数量）。现改为**恒占位**：RUIF 可用走 Stepper，不可用走原版加减。
            //   追加行是单行控件 ⇒ 行高 = RowHeight（不是工单行的两行高）。
            float addHeight = RowHeight;
            float addY = y + 4f;
            float countWidth = Mathf.Min(96f, width * 0.25f);
            Rect countRect = new Rect(rect.x, addY, countWidth, addHeight);
            if (!RuifBridge.TryStepper(countRect, ref addCount, 1, 99, 1))
            {
                DrawCountStepperFallback(countRect);
            }
            float addX = countRect.xMax + 6f;
            float addWidth = Mathf.Min(140f, Mathf.Max(60f, width - (addX - rect.x) - 4f));
            Rect add = new Rect(addX, addY, addWidth, addHeight);
            // S7（2026-09-25 审计）：统一走主题按钮（暗色系 + 边框）；裸 ButtonText 是原版白底控件，
            //   与本窗其余交互件两种观感（工程他处一律 ThemedButton）。
            if (UIComponents.ThemedButton(add, Translate("PersonalChronicle.UI.Floor.Detail.Queue.Add")))
            {
                OpenRecipeMenu(section);
            }
            TooltipHandler.TipRegion(add, Translate("PersonalChronicle.UI.Floor.Detail.Queue.AddTip"));
            return add.yMax + 6f;
        }

        /// <summary>
        /// ⑦ 生产列表动作成功后的**重建快照**回调（由调用方接线；<c>null</c> = 不刷新）。
        /// 窗口自身不知道 `(slot,index)`，故由打开方提供。
        /// </summary>
        internal System.Action RefreshAction;

        /// <summary>用新快照替换当前内容（页签下标越界时回退到 0）。</summary>
        internal void ReplaceDetail(FloorDeviceDetailSnapshot next)
        {
            if (next == null)
            {
                return;
            }
            detail = next;
            tabTitleCache = null;   // 标题数组随新快照重建（语言/内容可能已变）
            // S1 修复（2026-09-23）：内容变了 ⇒ **高度缓存必须失效**（否则刷新后仍按旧内容判定滚动/留白）
            cachedContentHeight = -1f;
            // A8 修复（2026-10-01 实机）：换设备后内容高度可能大变（概览行数不同）⇒
            //   缓存与宽度键全量失效，并**按 InitialSize 重算窗高**（保持左上角不动）。
            //   旧实现只换内容不改窗高：内容变多 ⇒ 溢出滚动 + 底部截断；变少 ⇒ 底部大片空白。
            cachedTabIndex = -1;
            cachedWidth = -1f;
            sectionHeights.Clear();
            Vector2 resized = InitialSize;
            windowRect = new Rect(windowRect.x, windowRect.y, resized.x, resized.y);
            // S2-1（2026-09-23）：页签数由 `TabTitles` 决定 —— `Sections` 已改为「一页可多块」，
            //   不再与页签数相等（旧式 `tabIndex >= Sections.Count` 会在合并页上误重置页签）。
            if (detail.TabTitles == null || tabIndex >= detail.TabTitles.Count)
            {
                tabIndex = 0;
            }
        }

        /// <summary>行内动作按钮（返回下一个 x）。点击 ⇒ 回传给动作层执行。</summary>
        private float DrawQueueAction(Rect rect, string label, string stableId, int action, int index)
        {
            if (Widgets.ButtonText(rect, label))
            {
                ApplyBillAction(stableId, action, index, -1);
            }
            return rect.x + rect.width + QueueButtonGap;
        }

        /// <summary>
        /// 追加生产**数量**控件 —— RUIF <c>InputAtoms.Stepper</c> 不可用时的原版回落
        /// （CHG-20261002-FLOORDETAIL-AUDIT；形态对齐预览 <c>.addrow input{width:96px}</c> + 两侧加减）。
        ///
        /// <para>夹取区间与原 Stepper 调用**同参**（1..99）；三格全部走
        /// <see cref="UIComponents.ThemedButton"/>，不混入原版白底控件（S7 观感一致性）。</para>
        /// </summary>
        private void DrawCountStepperFallback(Rect rect)
        {
            if (rect.width < 40f || rect.height < 8f)
            {
                return;
            }
            float buttonWidth = Mathf.Min(Mathf.Max(18f, rect.height), rect.width * 0.34f);
            Rect minus = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            Rect plus = new Rect(rect.xMax - buttonWidth, rect.y, buttonWidth, rect.height);
            Rect value = new Rect(minus.xMax, rect.y,
                Mathf.Max(16f, rect.width - buttonWidth * 2f), rect.height);
            if (UIComponents.ThemedButton(minus, "-"))
            {
                addCount = Mathf.Max(1, addCount - 1);
            }
            if (UIComponents.ThemedButton(plus, "+"))
            {
                addCount = Mathf.Min(99, addCount + 1);
            }
            Widgets.DrawBoxSolid(value, UITheme.Panel);
            UIComponents.Border(value, UITheme.BorderHair);
            UIComponents.Label(value, addCount.ToString(CultureInfo.InvariantCulture),
                GameFont.Tiny, UITheme.Text, TextAnchor.MiddleCenter);
        }

        /// <summary>配方选择（FloatMenu）——避免自造下拉控件（引擎无原生下拉）。</summary>
        private void OpenRecipeMenu(FloorDetailSectionView section)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            List<string> labels = section.RecipeLabels;
            if (labels != null)
            {
                for (int i = 0; i < labels.Count; i++)
                {
                    int captured = i;
                    string stableId = section.BenchStableId;
                    options.Add(new FloatMenuOption(labels[i], delegate
                    {
                        ApplyBillAction(stableId, 0, -1, captured);
                    }));
                }
            }
            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption(
                    Translate("PersonalChronicle.UI.Floor.Detail.Queue.NoRecipe"), null));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>
        /// S3-5d（2026-09-23）：**指定生产人员**（FloatMenu，等价原版工单配置页的「工人」下拉）。
        ///
        /// <para>写面走 <see cref="WorkshopBillActions.TrySetPawnRestriction"/> → 原版公开
        /// <c>Bill.SetPawnRestriction</c>（已 Cecil 核实：唯一写入口，原版 UI 同路径）；首项
        /// 「不指定」= 恢复自动选人（写面传 0）。</para>
        ///
        /// <para>候选 = 在场自由殖民者（名序）；**本菜单不做技能排序** —— 排序是人员页胜任度排名的
        /// 职责，同一排序两处实现必然漂移（规范红线）。</para>
        /// </summary>
        private void OpenAssignMenu(string benchStableId, int billIndex, string assignedName)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            options.Add(new FloatMenuOption(
                Translate("PersonalChronicle.UI.Floor.Detail.Queue.AssignAuto"),
                delegate { ApplyPawnAssign(benchStableId, billIndex, 0, false); }));
            Map map = Find.CurrentMap;
            List<Pawn> colonists = map != null ? map.mapPawns.FreeColonistsSpawned : null;
            if (colonists != null)
            {
                for (int i = 0; i < colonists.Count; i++)
                {
                    Pawn pawn = colonists[i];
                    if (pawn == null || pawn.Dead)
                    {
                        continue;
                    }
                    int captured = pawn.thingIDNumber;
                    string label = pawn.LabelShort;
                    if (!string.IsNullOrEmpty(assignedName) && assignedName == label)
                    {
                        label = "◆ " + label;
                    }
                    options.Add(new FloatMenuOption(label,
                        delegate { ApplyPawnAssign(benchStableId, billIndex, captured, false); }));
                }
            }
            if (options.Count == 1)
            {
                options.Add(new FloatMenuOption(
                    Translate("PersonalChronicle.UI.Floor.Detail.Crew.Empty"), null));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>动作回传（<paramref name="action"/> = 0 表示追加生产）。成功后**就地刷新**快照。</summary>
        private void ApplyBillAction(string stableId, int action, int index, int recipeIndex)
        {
            // 批次 5：加单带**数量**（`addCount` 为窗口瞬时态；1 ⇒ 仍按 Forever 语义加单，与旧行为一致）
            bool ok = action == 0
                ? WorkshopBillActions.TryAddBill(stableId, recipeIndex, addCount, out string message)
                : WorkshopBillActions.TryApply(stableId, action, index, out message);
            if (!string.IsNullOrEmpty(message))
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (ok)
            {
                // 即时刷新（接线见 MasterControlTabWindow.OpenDeviceDetail）；未接线时退化为提示
                if (RefreshAction != null)
                {
                    RefreshAction();
                }
                else
                {
                    Messages.Message(Translate("PersonalChronicle.UI.Floor.Detail.Queue.Applied"),
                        MessageTypeDefOf.TaskCompletion, false);
                }
            }
        }

        /// <summary>
        /// S3-5（2026-09-23）：人员页「派工」点击的写面入口。
        ///
        /// <para>写面只走 <c>WorkshopBillActions.TrySetPawnRestriction</c> → 原版公开方法
        /// <c>Bill.SetPawnRestriction</c>（已 Cecil 核实：属性只读、私有字段不可直写）；
        /// **再点同一人 = 取消指定**（等价原版下拉的"空"选项）。成功后就地刷新快照。</para>
        /// </summary>
        /// <param name="toggleOnRepeat">
        /// true（人员页点行）= 点同一人**取消指定**；false（生产列表菜单选人）= 明确选人恒为指派 ——
        /// 取消由菜单首项「不指定」负责，避免"选了同一个人反而取消"的反直觉行为。
        /// </param>
        private void ApplyPawnAssign(string benchStableId, int billIndex, int pawnThingId,
            bool toggleOnRepeat = true)
        {
            int target = pawnThingId;
            Thing bench = WorkshopBillActions.FindByStableId(Find.CurrentMap, benchStableId);
            if (toggleOnRepeat && bench is IBillGiver giver && giver.BillStack != null
                && billIndex >= 0 && billIndex < giver.BillStack.Count)
            {
                Pawn assigned = giver.BillStack[billIndex].PawnRestriction;
                if (assigned != null && assigned.thingIDNumber == pawnThingId)
                {
                    target = 0;
                }
            }

            bool ok = WorkshopBillActions.TrySetPawnRestriction(benchStableId, billIndex, target,
                out string message);
            if (!string.IsNullOrEmpty(message))
            {
                Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (!ok)
            {
                return;
            }
            Messages.Message(
                target > 0
                    ? Translate("PersonalChronicle.UI.Floor.Detail.Crew.AssignDone")
                    : Translate("PersonalChronicle.UI.Floor.Detail.Crew.AssignCleared"),
                MessageTypeDefOf.TaskCompletion, false);
            if (RefreshAction != null)
            {
                RefreshAction();
            }
        }

        /// <summary>
        /// 页签内容绘制（S2-1，2026-09-23 起**一页可含多块**）：按 <see cref="FloorDetailSectionView.TabIndex"/>
        /// 取出本页全部块，**顺序绘制、纵向累加**（页签 7→4 后：生产 = 可写列表 + 工单摘要；健康 = TPM + 品质）。
        /// </summary>
        private float DrawTabBody(Rect rect, float y, float width, bool wide)
        {
            List<FloorDetailSectionView> sections = detail.Sections;
            if (sections == null)
            {
                return y;
            }
            for (int i = 0; i < sections.Count; i++)
            {
                FloorDetailSectionView one = sections[i];
                if (one == null || one.TabIndex != tabIndex)
                {
                    continue;
                }
                y = DrawTabSection(rect, y, width, wide, one);
            }
            return y;
        }

        /// <summary>块标题文本缩进（预览 .dsub 的 border-left:3px + padding-left:7px，HTML:185-186）。</summary>
        private const float CaptionIndent = 10f;

        /// <summary>块标题色条（CHG-20261002-UIAUDIT）：预览 .dsub 左缘 3px 语义色条
        /// （border-left:3px solid var(--cyan)，HTML:185-186 / :422「视觉锚点」强化）。</summary>
        private static void DrawCaptionBar(float x, float y)
        {
            Widgets.DrawBoxSolid(new Rect(x, y, 3f, CaptionHeight), UITheme.Info);
        }

        /// <summary>单块绘制（原 <c>DrawTabBody</c> 方法体抽出，供一页多块复用）。</summary>
        private float DrawTabSection(Rect rect, float y, float width, bool wide,
            FloorDetailSectionView section)
        {
            // 层次感（2026-10-01 二轮）：块间 1px 分隔线（画在块尾空隙里，不占测量高度）。
            if (y > rect.y + 2f)
            {
                Widgets.DrawBoxSolid(new Rect(rect.x, y - 6f, width, 1f),
                    UITheme.WithAlpha(UITheme.BorderSoft, 0.7f));
            }
            // 渲染种类由**契约**声明（LAND-010：UI 不认识"制造中心"，只认 RenderKind）
            if (section.RenderKind == FloorDetailRenderKinds.BillQueue)
            {
                if (!string.IsNullOrEmpty(section.Caption))
                {
                    DrawCaptionBar(rect.x, y);
                    UIComponents.Label(new Rect(rect.x + CaptionIndent, y, width - CaptionIndent, CaptionHeight),
                        UIComponents.TruncateToWidth(section.Caption, width - CaptionIndent, GameFont.Tiny),
                        GameFont.Tiny, UITheme.Dim);
                    y += CaptionHeight;
                }
                // CHG-20261002-UIAUDIT：空态提示落地——键 Queue.Empty 此前已声明（BuildBillQueueTab）
                //   但渲染链跳过（BillQueue 分支早退），玩家看不到引导；现按 HTML:794-795 的 .empty 语义在标题下补一行提示（测量侧同式同步，见 MeasureSectionHeight）。
                if ((section.BillRows == null || section.BillRows.Count == 0)
                    && !string.IsNullOrEmpty(section.EmptyHint))
                {
                    UIComponents.Label(new Rect(rect.x, y, width, RowHeight),
                        UIComponents.TruncateToWidth(section.EmptyHint, width, GameFont.Tiny),
                        GameFont.Tiny, UITheme.Muted);
                    y += RowHeight + 12f;
                }
                return DrawBillQueueSection(rect, y, width, section);
            }

            // CHG-20261002-PRODCOLLAPSE：工单摘要折叠开关（行级 IsSummary 声明 ⇒ 提供折叠；
            //   折叠态只画摘要行）。文案两语言 Keyed；状态 = 窗口瞬时字段。
            // CHG-20261002-FLOORDETAIL-AUDIT：**按钮位置移到块末**（预览 `.foldrow` 在行集之后，
            //   HTML:816 —— 旧实现挂在块标题行右端，与预览的阅读顺序相反）。
            bool summaryFold = HasSummaryRows(section);

            if (!string.IsNullOrEmpty(section.Caption))
            {
                // 块级来源标（新预览「数据来源徽标」的块粒度）：整块口径与逐行不同时用
                float captionBadge = 0f;
                if (section.SourceKind > 0)
                {
                    string tag = SourceTagText(section.SourceKind);
                    captionBadge = Mathf.Min(MeasureWidth(tag, GameFont.Tiny) + 10f, width * 0.2f);
                    UIComponents.Badge(new Rect(rect.x + width - captionBadge, y + 2f,
                        captionBadge, CaptionHeight - 4f), tag,
                        SourceTagColor(section.SourceKind));
                }
                // CHG-20261002-UIAUDIT：块标题色条 + 文本缩进（HTML:185-186；宽度同步让位）。
                float titleWidth = Mathf.Max(20f, width - CaptionIndent - captionBadge - 4f);
                DrawCaptionBar(rect.x, y);
                UIComponents.Label(new Rect(rect.x + CaptionIndent, y, titleWidth, CaptionHeight),
                    UIComponents.TruncateToWidth(section.Caption, titleWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim);
                y += CaptionHeight;
            }

            if (!string.IsNullOrEmpty(section.EmptyHint))
            {
                UIComponents.Label(new Rect(rect.x, y, width, RowHeight),
                    UIComponents.TruncateToWidth(section.EmptyHint, width, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted);
                return y + RowHeight + 12f;
            }

            // InspectString 区（新预览「运行」页新增）：**原样**显示 `GetInspectString()` 的每一行。
            // 该串是「基类内容 + 各 CompInspectString」的**官方合成** ⇒ 任何过滤/重排都可能吞掉
            // 火灾、温度等基类告警，因此这里只拆行显示、不拼接（LAND-009 / UI-004）。
            if (section.InspectLines != null && section.InspectLines.Count > 0)
            {
                // CHG-20261002-FLOORDETAIL-AUDIT：**块已声明 Caption 时不再画内部检视标题**
                //   —— 概览页把检视串拆成独立块（Caption = Run.InspectCaption + SourceKind = API）后，
                //   旧式无条件内部标题会与块标题**重复一行**（同一句「设备检视…」连出两次）。
                //   测量侧同式（见 MeasureSectionHeight 的 inspectCaption 段），两处锚点互指。
                if (string.IsNullOrEmpty(section.Caption))
                {
                    const int inspectSourceKind = 1;   // API：原版合成串
                    string tag = SourceTagText(inspectSourceKind);
                    float badge = Mathf.Min(MeasureWidth(tag, GameFont.Tiny) + 10f, width * 0.2f);
                    UIComponents.Badge(new Rect(rect.x + width - badge, y + 2f, badge, CaptionHeight - 4f),
                        tag, SourceTagColor(inspectSourceKind));
                    UIComponents.Label(new Rect(rect.x, y, Mathf.Max(20f, width - badge - 4f), CaptionHeight),
                        UIComponents.TruncateToWidth(
                            Translate("PersonalChronicle.UI.Floor.Detail.Run.InspectCaption"),
                            Mathf.Max(20f, width - badge - 4f), GameFont.Tiny),
                        GameFont.Tiny, UITheme.Dim);
                    y += CaptionHeight;
                }
                for (int i = 0; i < section.InspectLines.Count; i++)
                {
                    Rect lineRect = new Rect(rect.x, y, width, RowHeight);
                    UIComponents.Label(lineRect,
                        UIComponents.TruncateToWidth(section.InspectLines[i], width, GameFont.Tiny),
                        GameFont.Tiny, UITheme.Text);
                    // LAND-008：截断必配悬停提示（检视串常超宽）
                    TooltipHandler.TipRegion(lineRect, section.InspectLines[i]);
                    y += RowHeight;
                }
                y += 8f;
            }

            // S1（2026-09-23）：**时序条已删除** —— 40 格只有 1 段真数据（原版无按台历史）⇒ 纯噪音。
            // 键值行：宽档两栏（预览 `.dcols{grid-template-columns:1fr 1fr}`），窄档单栏；
            // `Wide` 块自身**跨两栏**（预览 `.insp/.giz/.nav2/.chips` 跨栏）⇒ 其内部行改走单栏整宽。
            // S13：把块级来源传下去 ⇒ 同来源的行不再各画一个徽标（块标题处已有一次）。
            y = DrawRows(new Rect(rect.x, y, width, 0f), y, width, section.Rows,
                wide && !section.Wide, section.BenchStableId, section.SourceKind,
                !billSummaryExpanded && HasSummaryRows(section));

            // CHG-20261002-FLOORDETAIL-AUDIT：折叠开关**收在行集之后**（预览 `.foldrow` 位置，HTML:816）。
            //   按钮高按 Small 字档真值派生（旧式 `CaptionHeight-4`=16 < Small 行高 ⇒ 「详情」下半截被裁）。
            if (summaryFold)
            {
                DrawSummaryFoldButton(rect.x, y + SummaryFoldButtonGap, SummaryFoldButtonHeight);
                y += SummaryFoldButtonGap + SummaryFoldButtonHeight;
            }

            // 品质分布（预览 .qh 柱 + .qlab 标签）
            if (section.QualityHistogram != null && section.QualityHistogram.Count > 0
                && section.QualityLabels != null && section.QualityLabels.Count > 0)
            {
                y = DrawQualityChart(new Rect(rect.x, y, width, 0f), y, width, section);
            }
            return y + 12f;
        }


        private void RebuildSectionHeights(float width)
        {
            sectionHeights.Clear();
            cachedContentHeight = -1f;
            cachedTabIndex = -1;
        }

        private float cachedContentHeight = -1f;

        // CHG-20261002-RUNTAB-EMPTYSTATE（LAND-011）：测量缓存键含快照引用（纵深防御，
        //   见 MeasureContentHeight 内注释；窗口瞬时态，不入存档）。
        private FloorDeviceDetailSnapshot cachedDetailRef;

        // A1（2026-09-25 审计）：缓存键必须带**当前页签** —— 旧实现只按宽度缓存，
        //   切页签后沿用旧页签的高度 ⇒ 滚动范围 / 底部留白按错页签算（差可达整页）。
        private int cachedTabIndex = -1;

        private float MeasureContentHeight(float width, bool wide)
        {
            // CHG-20261002-RUNTAB-EMPTYSTATE（LAND-011）：缓存键补**快照引用**——
            //   旧键（高度/页签/宽度）不含内容代次，行集随设备状态收缩/扩张时不失效；
            //   当前替换路径（ReplaceDetail）已全量失效，此处为纵深防御：未来任何绕过
            //   ReplaceDetail 的快照换入不再吃到旧高度。
            if (cachedContentHeight >= 0f
                && ReferenceEquals(cachedDetailRef, detail)
                && cachedTabIndex == tabIndex
                && Mathf.Approximately(cachedWidth, width))
            {
                return cachedContentHeight;
            }
            float height = 0f;
            // 标题带（标题行 22 = Small 行高真值；与 DrawHeader 消耗一一对应）
            height += 22f + 2f + RowHeight + RowHeight + 10f;
            // 指标栅格
            int metricCount = detail.Metrics != null ? detail.Metrics.Count : 0;
            int metricColumns = MetricColumnCount(metricCount, wide);
            if (metricCount > 0)
            {
                int rows = (metricCount + metricColumns - 1) / metricColumns;
                // CHG-20261002-FLOORDETAIL-AUDIT：卡高与绘制同源（`MetricCardHeight`，按字体档派生）
                height += rows * (MetricCardHeight + MetricGap) + 4f;
            }
            // 页签条
            if (detail.TabTitles != null && detail.TabTitles.Count > 0)
            {
                height += TabBarHeight + 6f;
            }
            // S2-1（2026-09-23）：本页**全部块**累加（一页可多块：生产 / 健康均为两块）。
            //   原「底部契约栏」（`ContractRows` + `DrawContract`）已随旧路径删除 ——
            //   其内容与契约页签完全重复（同一信息只出现一次）。
            if (detail.Sections != null)
            {
                for (int i = 0; i < detail.Sections.Count; i++)
                {
                    FloorDetailSectionView one = detail.Sections[i];
                    if (one == null || one.TabIndex != tabIndex)
                    {
                        continue;
                    }
                    height += MeasureSectionHeight(one, wide);
                }
            }
            cachedContentHeight = height;
            cachedTabIndex = tabIndex;
            cachedDetailRef = detail;   // CHG-20261002-RUNTAB-EMPTYSTATE（LAND-011）
            return height;
        }

        /// <summary>
        /// 单块高度（与 <c>DrawTabSection</c> **同式**：估矮会裁内容、估高会留白 ⇒ 必须一一对应；
        /// 由原 <c>MeasureContentHeight</c> 的 section 段抽出，供一页多块复用）。
        /// </summary>
        private float MeasureSectionHeight(FloorDetailSectionView section, bool wide)
        {
            float height = 0f;
            if (section == null)
            {
                return height;
            }
            // 深度优化（2026-10-01）：生产页（可写工单列表）此前**漏测** ⇒ contentHeight
            //   严重低估 ⇒ 窗口开矮 + 滚动 + 底部截断。此处按 DrawBillQueueSection 的
            //   消耗补齐：工单行 + 数量/追加行（估高略高 ⇒ 底部留白优于裁内容）。
            if (section.RenderKind == FloorDetailRenderKinds.BillQueue)
            {
                int bills = section.BillRows != null ? section.BillRows.Count : 0;
                if (!string.IsNullOrEmpty(section.Caption))
                {
                    height += CaptionHeight;
                }
                // CHG-20261002-UIAUDIT：空态提示行计高（与 DrawTabSection 同一条件/同一增量）。
                if (bills == 0 && !string.IsNullOrEmpty(section.EmptyHint))
                {
                    height += RowHeight + 12f;
                }
                // CHG-20261002-FLOORDETAIL-AUDIT：追加行 = 单行控件（RowHeight），
                //   旧式 `QueueRowHeight * 2` 在行高提为两行后会把追加行估成 76px（凭空多 44px 留白）。
                //   与 DrawBillQueueSection 逐项对齐：行区 bills*(行高+2) → 追加行 (4 + RowHeight + 6)。
                return height + bills * (QueueRowHeight + 2f) + RowHeight + 10f;
                // ↑ 4（addY 偏移） + 6（add.yMax 后呼吸） = 10，与绘制返回值严格同式
            }
            if (!string.IsNullOrEmpty(section.Caption))
            {
                height += CaptionHeight;
            }
            if (!string.IsNullOrEmpty(section.EmptyHint))
            {
                return height + RowHeight + 12f;
            }
            if (section.InspectLines != null && section.InspectLines.Count > 0)
            {
                // CHG-20261002-FLOORDETAIL-AUDIT：块已有 Caption 时绘制侧**不再画**内部检视标题
                //   ⇒ 测量侧同步不重复计 CaptionHeight（两处锚点互指，见 DrawTabSection 同名注）。
                height += (string.IsNullOrEmpty(section.Caption) ? CaptionHeight : 0f)
                    + section.InspectLines.Count * RowHeight + 8f;
            }

            int rowCount = section.Rows != null ? section.Rows.Count : 0;
            if (rowCount > 0)
            {
                // 与 DrawRows 同式：`Wide` 块跨栏 ⇒ 其内部行走单栏整宽（否则估矮会裁掉内容）
                int columns = wide && !section.Wide ? 2 : 1;
                // CHG-20261002-PRODCOLLAPSE：折叠态按**可见行数**计高（与 DrawRows 同式口径：
                //   DrawRows 亦以 visibleCount 分栏；否则估高留白 / 估矮裁内容）。
                int visible = !billSummaryExpanded && HasSummaryRows(section)
                    ? CountSummaryRows(section.Rows)
                    : rowCount;
                int perColumn = (visible + columns - 1) / columns;
                // CHG-20261002-RUNTAB-EMPTYSTATE：补计 DrawRows 的块尾呼吸 6f
                //（FloorDeviceDetailWindow.Draw.cs `return y + height + 6f`）—— 旧测量漏计
                // ⇒ 每块估矮 6px、整页累积（概览 2 块 = 12px），滚动开/关边界处溢出被遮盖
                //（IMGUI 无裁剪，铁律 3）＝ 实机"挤兑/裁切"的口径缺口。两处锚点互指，改动须同步。
                height += perColumn * RowHeight + 6f;
            }
            // CHG-20261002-FLOORDETAIL-AUDIT：折叠开关已移到块末 ⇒ 计高与绘制同步
            //（绘制侧净推进 = SummaryFoldButtonGap + SummaryFoldButtonHeight，同一对常量）。
            if (HasSummaryRows(section))
            {
                height += SummaryFoldButtonGap + SummaryFoldButtonHeight;
            }
            if (section.QualityHistogram != null && section.QualityHistogram.Count > 0)
            {
                // CHG-20261002-FLOORDETAIL-AUDIT：与 DrawQualityChart 严格同式
                //（柱区 44 + 间隙 3 + 标签行 Tiny 真值 + 块尾 2；旧值 60+3+16 系估高口径）
                height += QualityChartHeight + QualityChartGap
                    + Text.LineHeightOf(GameFont.Tiny) + QualityChartTail;
            }
            return height + 12f;
        }

        // ---- 关闭按钮 ----

        private void DrawCloseButton(Rect inRect)
        {
            const float size = 64f;
            // CHG-20261002-UIAUDIT：对齐 HTML:140（#dclose right:14px；旧值 8 → 14）。
            Rect close = new Rect(inRect.xMax - size - 14f, inRect.y + 14f, size, 22f);
            if (Widgets.ButtonText(close, Translate("PersonalChronicle.UI.Floor.Detail.Close")))
            {
                Close();
            }
        }

        // ---- 绘制小工具 ----

        /// <summary>黄黑警示条（预览 <c>--hz</c> 斜条）；IMGUI 无渐变 → 等宽交替块。</summary>
        private static void DrawHazardStripe(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, FloorPalette.HazardBase);
            const float stripe = 14f;
            int count = Mathf.CeilToInt(rect.width / (stripe * 2f));
            for (int i = 0; i < count; i++)
            {
                float x = rect.x + i * stripe * 2f;
                float w = Mathf.Min(stripe, rect.xMax - x);
                if (w <= 0f)
                {
                    break;
                }
                Widgets.DrawBoxSolid(new Rect(x, rect.y, w, rect.height), UITheme.Threat);
            }
        }

        /// <summary>状态键 → 灯色（与俯视图同一映射，不在本类重算阈值）。</summary>
        private static Color StateColor(string stateKey)
        {
            switch (stateKey)
            {
                case "run": return UITheme.Alive;
                case "fault": return UITheme.Dead;
                case "starved": return UITheme.Threat;
                case "off": return UITheme.WithAlpha(UITheme.Dim, 0.6f);
                default: return UITheme.Dim;
            }
        }

        /// <summary>语气 → 颜色（0 中性 / 1 好 / 2 注意 / 3 差）。</summary>
        /// <summary>
        /// 数据来源徽标文案（**契约词，不翻译** —— LAND-013：API / LEDGER / SIM 是口径标识，
        /// 与预览逐字一致；翻译反而会让「这行数据从哪来」在两语言下不可比对）。
        /// </summary>
        /// <summary>
        /// 数据来源徽标文案。**委托 <see cref="UiAtoms"/>** —— CHG-20261003-FLOORDETAIL-UNIFY
        /// 把该语义上移到统一 UI 层，本处保留薄封装只为不改动块级徽标等既有调用点。
        /// </summary>
        private static string SourceTagText(int sourceKind)
        {
            return UiAtoms.SourceTagText(sourceKind);
        }

        /// <summary>徽标配色（同上，委托统一层；API=青 · LEDGER=琥珀 · SIM=灰）。</summary>
        private static Color SourceTagColor(int sourceKind)
        {
            return UiAtoms.SourceTagColor(sourceKind);
        }

        /// <summary>行语气色（1=正向 / 2=警示 / 3=负向）。委托 <see cref="UiAtoms"/>。</summary>
        private static Color ToneColor(int tone)
        {
            return UiAtoms.ToneColor(tone);
        }

        /// <summary>品质档位色（预览 .qh 的 7 色阶；index 1/5/6 原裸色值收敛至 FloorPalette，ASSET-002 · CHG-MFG-AUDIT-001 · U-3）。</summary>
        private static Color QualityColor(int index)
        {
            switch (index)
            {
                case 0: return UITheme.Dim;
                case 1: return FloorPalette.DeviceTier1;
                case 2: return UITheme.Info;
                case 3: return UITheme.Alive;
                case 4: return UITheme.Accent;
                case 5: return FloorPalette.DeviceTier5;
                default: return FloorPalette.DeviceTier6;
            }
        }

        /// <summary>CHG-20261002-PRODCOLLAPSE：块内是否存在摘要行（LAND-010 行级声明，UI 只读标志）。</summary>
        private static bool HasSummaryRows(FloorDetailSectionView section)
        {
            List<FloorDetailRowView> rows = section != null ? section.Rows : null;
            if (rows == null)
            {
                return false;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                FloorDetailRowView row = rows[i];
                if (row != null && row.IsSummary)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>摘要行计数（折叠态可见行数；与 DrawRows 分栏同式口径）。</summary>
        private static int CountSummaryRows(List<FloorDetailRowView> rows)
        {
            if (rows == null)
            {
                return 0;
            }
            int count = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                FloorDetailRowView row = rows[i];
                if (row != null && row.IsSummary)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>折叠开关与行集之间的呼吸。</summary>
        private const float SummaryFoldButtonGap = 2f;

        /// <summary>品质直方图柱区高（预览 <c>.qh{height:44px}</c>，HTML:262）—— 绘制/测量共用唯一真源。</summary>
        private const float QualityChartHeight = 44f;
        /// <summary>柱区与标签行之间的呼吸。</summary>
        private const float QualityChartGap = 3f;
        /// <summary>标签行之后的块尾呼吸。</summary>
        private const float QualityChartTail = 2f;

        /// <summary>
        /// 折叠开关按钮高 = Small 字档真实行高 + 上下呼吸（LAND-007：按钮文案走
        /// <see cref="UITheme.FontBody"/>，故高度必须由该字档反推；旧值 <c>CaptionHeight-4</c>=16 会裁字）。
        /// </summary>
        private static float SummaryFoldButtonHeight
        {
            get { return Text.LineHeightOf(UITheme.FontBody) + 4f; }
        }

        /// <summary>
        /// 工单摘要折叠开关（「▶ 详情 / ▼ 收起」）。
        /// <para>符号用 ▶/▼：工程实机已验字形（S6 审计：↑/↓ 在实机字库缺字形被渲染成豆腐块）；
        /// 预览稿的 ▸/▾ 未实机验证 ⇒ 不采用（CHG-20261002-PRODCOLLAPSE 受控偏离，已登记）。</para>
        /// <para>切换 = 布局变化：高度缓存失效 + 按 InitialSize 重算窗高（A8 先例：保持左上角不动）。</para>
        /// <para>CHG-20261002-FLOORDETAIL-AUDIT：位置由「块标题行右端」改为「行集之后、块左缘」
        /// （预览 `.foldrow{...}` 在摘要行下方，HTML:816）；故入参改为**左缘 x**。
        /// 返回按钮实占宽（含 4px 让位），供调用方累加高度。</para>
        /// </summary>
        private float DrawSummaryFoldButton(float x, float y, float height)
        {
            string label = billSummaryExpanded
                ? Translate("PersonalChronicle.UI.Floor.Detail.Bill.DetailHide")
                : Translate("PersonalChronicle.UI.Floor.Detail.Bill.DetailShow");
            float w = MeasureWidth(label, UITheme.FontBody) + 12f;
            if (UIComponents.ThemedButton(new Rect(x, y, w, height), label))
            {
                billSummaryExpanded = !billSummaryExpanded;
                cachedContentHeight = -1f;
                cachedTabIndex = -1;
                cachedWidth = -1f;
                sectionHeights.Clear();
                Vector2 resized = InitialSize;
                windowRect = new Rect(windowRect.x, windowRect.y, resized.x, resized.y);
            }
            return w + 4f;
        }

        private static float MeasureWidth(string text, GameFont font)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }
            GameFont previous = Text.Font;
            Text.Font = font;
            float width = Text.CalcSize(text).x;
            Text.Font = previous;
            return width;
        }

        /// <summary>
        /// **无参** Keyed 文案（走 <see cref="UIComponents.Loc"/> 的语言级缓存）。
        ///
        /// <para>⚠ 2026-09-19（CHG-20260919-ECON-UI-010）：本窗口 <c>OnGUI</c> 内的队列行 /
        /// 按钮 / 说明 / 关闭按钮等原先每帧翻译并分配字符串；改走缓存后一处改动全部覆盖。</para>
        /// </summary>
        private static string Translate(string key)
        {
            return UIComponents.Loc(key);
        }
    }
}

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
    /// Partial of <see cref="MasterControlTabWindow"/> ——**整页适配一屏的 app shell**（业务视图专页）。
    ///
    /// <para>预览有一段专门的「整页适配一屏（无滚动）」CSS（HTML 552–576），此前我误做成**长滚动页**，
    /// 于是平面图按宽度等比得到 549px 高（比游戏可视内容区还高）⇒ tab 栏被推到折叠线之外、
    /// 首屏只剩一块黑。现按原文重建：</para>
    /// <code>
    /// html,body{height:100%;overflow:hidden}                 ← 整页无滚动
    /// #wrap{flex:1;min-height:0;flex-direction:column;overflow:hidden;padding:10px 16px 40px}
    /// #sop{flex:1;grid-template-rows:auto 1fr;gap:10px}      ← ①=auto(固定行) / ②=1fr(吃剩余)
    /// #sop>h3.sec{display:none}                             ← app 模式隐藏分区标题
    /// #floorwrap{grid-row:2;min-height:0;flex-direction:column}
    /// #floorwrap .floorview{flex:1 1 auto;min-height:0;overflow:hidden}  ← 平面图吃剩余高度并裁剪
    /// .fpanes{flex:none;max-height:min(24vh,214px);overflow-y:auto}      ← tab 内容固定上限 + 内部滚动
    /// .ftabs{flex:none} / .hazard / #ticker{height:26px}
    /// </code>
    ///
    /// <para><b>与浏览器的一致性</b>：平面图仍是 `svg{width:100%;height:auto}`（宽度决定设计比例），
    /// 但**高度被容器裁剪**（`overflow:hidden`）—— 这正是预览的做法：看不全就用缩放条
    /// （`适配` = 把整图缩进视口）。100% 缩放 ≠ 看得全，`适配`才看得全。</para>
    /// </summary>
    public sealed partial class MasterControlTabWindow
    {
        // ---- app shell 几何（全部取自预览 552–576 的 CSS）----
        /// <summary>`#wrap{padding:10px 16px 40px}`。</summary>
        private const float WrapPadX = 16f;
        private const float WrapPadBottom = 40f;

        /// <summary>`#sop{gap:10px}`。</summary>
        private const float SopGap = 10f;

        /// <summary>
        /// ⚠ 2026-10-06（用户指令「移除底栏 Tab 栏整行」）：原页签栏几何与缓冲字段
        /// （`AppTabBarHeight = 26f` / `AppTabBarBottomPad = 4f` / `tabBarLabels` / `tabBarWidths`）
        /// 已随 `DrawAppTabBar` 一并删除。
        ///
        /// <para>计高同步点（删除时必须同时改，否则滚动高漂移）：`DrawAppFloorPanel` 的 `fixedRows`、
        /// `Detail.ComputeDetailHeight` 的 `+ AppTabBarHeight + AppTabBarBottomPad`（均已去项）。</para>
        /// <para>恢复方式：本注释处还原常量与 `DrawAppTabBar`（git 历史保留完整实现，
        /// 该实现含 2026-10-02 审计 P1-9 的"统一配额 + 溢出 +N"修复），并在上述两处计高加回。</para>
        /// </summary>

        /// <summary>`.fpanes{max-height:min(24vh,214px)}` 的比例与上限。</summary>
        private const float PaneMaxRatio = 0.24f;
        private const float PaneMaxAbsolute = 214f;

        // ⚠ 2026-10-06（用户指令「kpi 也要移除。确保干净」）：原 app 模式 KPI 卡几何
        //   （`AppKpiCardHeight = 62f` / `AppKpiGap = 10f`）与表盘几何（`KpiDialLabelBand = 15f` /
        //   `KpiDialTickSteps = 20`）已随 KPI 卡带删除 —— 它们只服务 ② 面板的九卡。

        /// <summary>tab 内容区滚动位置（UI 瞬时态，不入存档）。</summary>
        private Vector2 paneScroll;

        /// <summary>最近一次平面图视口尺寸（供「适配」按钮计算 fit 缩放）。</summary>
        private float lastFloorVpWidth = 1f;
        private float lastFloorVpHeight = 1f;

        /// <summary>业务视图是否走 app shell（有 Chrome 快照 = 该中心是 cockpit 型页面）。</summary>
        private bool UsesAppShell(SystemCenterDetailSnapshot detail)
        {
            return detail != null && detail.Chrome != null && !detail.Chrome.IsEmpty
                && detail.Workshop != null && !detail.Workshop.NoMap;
        }

        /// <summary>
        /// 画整页（app shell）。**不使用外层滚动视图**：所有分区按"吃剩余高度"分配，与预览一致。
        /// </summary>
        private void DrawDetailAppShell(Rect rect, SystemCenterDetailSnapshot detail)
        {
            float y = rect.y;

            // **#top 顶栏已整条删除**（新预览 `id="top"` 命中 0；基線 §0a）：
            // brand / 时钟 / 电网 / `#kpiline` 全部并入下方 #cmdbar；播放控制 / 急停 / 蓝图 / 换数据均已删。
            // 故此处不再画任何顶栏（`#kpiline` 的 9 项读数由 ① 总控行常显承担，见基線 §5c-A3 的说明）。

            // 面包屑 + 标题（app 模式下大幅瘦身：`.brand .en{display:none}`、标题与 api 同行的精神）
            y = DrawAppShellHeader(rect, y, detail);

            // #sop：① KPI **在「总控」tab 页里**（`buildFloorTabs()` 把 `groups['①']` 收进 `cockpit` pane），
            // 不再是 `#sop` 的固定第一行 —— 实机反馈"kpi 不处于底栏 tab"即指此。
            // 于是 ② 面板吃掉 #wrap 的**全部**剩余高度（`#sop{grid-template-rows:auto 1fr}` 只剩一个子节点）。
            Rect panelRect = new Rect(rect.x, y, rect.width, Mathf.Max(160f, rect.yMax - y));
            DrawAppFloorPanel(panelRect, detail);
        }

        /// <summary>面包屑 + api + 简介（压到 3 行以内；`#sop>h3.sec{display:none}` 故不画分区标题）。</summary>
        private float DrawAppShellHeader(Rect rect, float y, SystemCenterDetailSnapshot detail)
        {
            SystemCenterCardView card = detail.Card;
            // ⚠ 文本行高一律 ≥ 18（Tiny 行高）：16f 会让文字下缘溢出、被下一行盖住。
            UIComponents.Label(new Rect(rect.x, y, rect.width, PopulationRowHeight),
                Translate("PersonalChronicle.UI.MasterControl.Detail.Crumb", card.Label),
                GameFont.Tiny, UITheme.Dim);
            y += PopulationRowHeight + 2f;
            UIComponents.Label(new Rect(rect.x, y, rect.width * 0.6f, 22f), card.Label,
                GameFont.Small, UITheme.Accent);
            // S5（2026-09-25 审计）：标识行非开发模式为空 ⇒ 整行跳过（不留空位、不画调试串）。
            string apiLine = BuildApiLine(card);
            if (!string.IsNullOrEmpty(apiLine))
            {
                UIComponents.Label(new Rect(rect.x + rect.width * 0.6f, y + 3f, rect.width * 0.4f, 18f),
                    apiLine, GameFont.Tiny, UITheme.Info, TextAnchor.MiddleRight);
            }
            return y + 24f;
        }

        // ---- ① KPI：**已于 2026-10-06 整块移除**（用户指令「kpi 也要移除。确保干净」）----
        // 删除面：`AppKpiBandHeight` / `AppKpiRowCount` / `DrawAppKpiBand` / `DrawAppKpiCard` /
        //   `DrawKpiDial`（PC 自绘表盘回落）/ `DialZoneTone`（表盘分区色）与它们的几何常量。
        // 连带：制造中心 `BuiltinManufacturingProvider.AppendKpis` 改为空实现 ⇒ `detail.Kpis` 恒空
        //   ⇒ 下方 `ActivePaneHeight` / `DrawActivePane` 的 `TabCockpit` 分支恒返回空
        //   （分支保留：它同时是经济中心等其它中心 KPI 的**通用机制**入口，见 `Detail.DrawKpiGrid`）。
        // 恢复方式：从 git 历史取回本段 + `ManufacturingChromeSnapshot.KpiLine*` + `Chrome.DrawKpiLine`
        //   + `SystemCenterReadModelProvider.FillKpiLine` + `BuiltinManufacturingProvider` 九卡实现。

        // ⚠ 2026-10-06（用户指令「kpi 也要移除」）：原 `DrawAppKpiCard`（九卡的单卡绘制：表盘分支 +
        //   数字卡分支，含 2026-09-13 / 09-22 的表盘版本闸门与"口径副标常显"修复）已整块删除。
        //   它只服务已被移除的 ② 面板 KPI 卡带；`RuifBridge.TryDrawGauge` 保留（框架适配层 API，
        //   将来若恢复 KPI 卡即复用）。恢复方式见本文件上方「① KPI：已整块移除」注释。

        // ⚠ 2026-10-06（用户指令「kpi 也要移除」）：原 `DrawKpiDial`（① 总控 KPI 的工业指针表盘 ——
        //   PC 自绘回落路径：分区弧 / 5% 刻度 / 指针 / 中心读数 / 左上标签，含 0912v–0913b 三轮实机
        //   缺陷的几何对齐记录）与 `DialZoneTone`（表盘分区色）**整块删除**。
        //   两者只服务已被移除的 KPI 卡（`DrawAppKpiCard`）；`RuifBridge.TryDrawGauge` 与
        //   `RuifBridge.GaugeFixedVersion` 保留（框架适配层，供将来恢复或其它中心使用）。
        //   恢复方式见本文件上方「① KPI：已整块移除」注释。

        // ---- ② 面板：吃剩余高度（flex:1），内部再分为 指令条 / 平面图 / 缩放行 / tab 内容 ----
        // ⚠ 2026-10-06（用户指令）：底栏 Tab 栏整行（`#ftabs` / 7 个页签）已移除；`.hazard` 早前已按基線删除。

        private void DrawAppFloorPanel(Rect panel, SystemCenterDetailSnapshot detail)
        {
            WorkshopFloorSnapshot workshop = detail.Workshop;
            if (workshop == null || workshop.NoMap)
            {
                return;
            }

            UIComponents.Panel(panel);
            Rect inner = new Rect(panel.x + 13f, panel.y + 5f, panel.width - 26f, panel.height - 10f);

            // #cmdbar（新预览终态 · 基線 §5c-A）：**时间 + 电网(86) + 图例(靠右)** 单行 28px。
            // 已按新预览删除：`.ptitle`（`#floorwrap` 内已无标题行）、产线汇总 / `.routebar` / `.hcout`
            // （静态标记里 `route-bar` / `hcout` 仅剩 CSS 残留 ⇒ 元素已删）。
            float y = DrawAppCmdBar(inner, inner.y);
            // 🆕 #cline（新预览 2026-09-11 实测 23px）：分段(查看/编排) + 工序链 Chip 流
            y = DrawAppClinement(inner, y, workshop, detail);
            // 🆕 #lbar（新预览：**编排态才显示**的编排条，实测 25px；查看态不占高）
            if (floorEditMode)
            {
                y = DrawAppLayoutBar(inner, y, workshop);
            }

            // tab 内容上限（.fpanes{max-height:min(24vh,214px)}）
            float paneMax = Mathf.Min(panel.height * PaneMaxRatio, PaneMaxAbsolute);
            float paneNeeded = ActivePaneHeight(inner.width, detail);
            float paneHeight = Mathf.Clamp(paneNeeded, 0f, paneMax);

            // 固定行：通栏缩放行（`.hazard` 早前已删；tab 栏已于 2026-10-06 按用户指令移除 ⇒ 少一行高 + 一个段距）
            float fixedRows = ZoomRowHeight + SopGap * 2f;
            float floorHeight = inner.yMax - y - paneHeight - fixedRows;
            if (floorHeight < 80f)
            {
                // 高度告急时优先保平面图：压缩 tab 内容区（预览同序：floorview 是 flex:1）
                // ⚠ 「编排」页签例外：它的托盘 + 画布必须在**未滚动**的坐标系里命中
                //   （见 DrawActivePane 的 scroll 判定），压缩它会导致内容被裁且无法操作。
                if (selectedFloorTab != TabLayout)
                {
                    paneHeight = Mathf.Max(0f, paneHeight - (80f - floorHeight));
                }
                floorHeight = inner.yMax - y - paneHeight - fixedRows;
            }

            // 平面图：吃剩余高度并被裁剪；**左右让出轨道槽**（新预览实测 `#floor` 1414 @x318 居中，
            // 两侧 `.siderail` 126 与其**并排**，不再覆盖画布）
            // 传**外框**给 Draw：由它在内部按 railGutter 内缩画布、并把两侧轨道贴外框（并排不覆盖）
            Rect floorRect = new Rect(inner.x, y, inner.width, Mathf.Max(60f, floorHeight));
            lastFloorVpWidth = Mathf.Max(60f, floorRect.width - RailGutter * 2f);   // 画布区（不含轨道槽）
            lastFloorVpHeight = floorRect.height;
            lastFloorVpX = floorRect.x;
            lastFloorVpY = floorRect.y;
            // ⭐ 主画布**唯一绘制路径**（2026-09-12 统一 · 用户实机指正）：
            //   查看态与编排态画的是**同一块自由编排画布**（预览 `layoutMode` 亦只改交互，不改画布内容）。
            //   此前查看态走 `WorkshopFloorPlan`（固定 12 格厂房）⇒ 两块画布，与预览不符。
            //   查看态额外并排两侧轨道（原料库 / 成品库，贴外框、不覆盖画布）。
            float used = floorRect.height;
            Rect canvasArea = new Rect(floorRect.x + RailGutter, floorRect.y,
                Mathf.Max(60f, floorRect.width - RailGutter * 2f), floorRect.height);
            string clickedDeviceId = DrawAppLayoutCanvas(canvasArea, floorEditMode);
            // 两侧轨道**两态都画**（预览 `.siderail` 是 `.floorview` 的常驻 overlay，与模式无关）；
            // 左轨内含「原料库 / 设备库」页签，设备库数据与自由画布同源（同一 palette）。
            WorkshopFloorPlan.DrawRailsOnly(floorRect, workshop, layoutPalette);
            if (!string.IsNullOrEmpty(clickedDeviceId))
            {
                OpenDeviceDetailByStableId(workshop, clickedDeviceId);
            }
            y += used + SopGap;

            // 🔁 #zoom-bar（新预览：由浮层改为**通栏控制行**，位于平面图之后、tab 栏之前，实测 31px）
            y = DrawAppZoomRow(inner, y);

            // tab 内容（固定上限 + 内部滚动）
            if (paneHeight > 1f)
            {
                Rect pane = new Rect(inner.x, y, inner.width, paneHeight);
                DrawActivePane(pane, detail);
                y += paneHeight + SopGap;
            }

            // ⚠ 2026-10-06（用户指令）：底栏 Tab 栏整行已移除 —— 内容区固定渲染 `selectedFloorTab`
            //   （`DrawActivePane`；无切换入口后恒为 TabCockpit = 总控）。恢复方式见本方法上方注释。

            // 基線 §5c-B：预览已删面板底部 .hazard（纯装饰、无数据）⇒ 不再绘制。
            // 弹窗顶部 #drawer::before 斜纹条（FloorDeviceDetailWindow.DrawHazardStripe）与
            // 机身卡片上沿警示纹（WorkshopFloorLayout.HazardStrip*）**未受影响，勿删**。
        }

        // ---- #cmdbar（新预览终态）几何：`.cmdbar{padding:4px 13px;gap:10px}`、实测高 28px ----
        private const float CmdBarHeight = 28f;
        private const float CmdBarPadX = 13f;
        private const float CmdBarGap = 9f;   // measured #cmdbar{gap:9px}
        /// <summary>`#pbar` 终值宽 86px（原 150 已被新预览覆盖，基線 §0a A6）。</summary>
        private const float CmdBarBarWidth = 86f;
        private const float CmdBarBarHeight = 10f;

        /// <summary>
        /// `#cmdbar`（新预览终态，基線 §5c-A）：**时间 + 电网 + 图例**，单行，实测高 28px。
        /// 预览标记原文（`待落地项` 版）：
        /// `.cmdz#clockbox`(`.d` 日期 + `.t` 时刻) · `.cmdz#powerbox`(`.plbl2`「电网」+ `#pbar` 86×10
        /// + `.capmark` + `.pnum`) · `.lgbar`(`.lgcap`「LEGEND 图例」+ 4×`.lgitem` 灯色/名称 + `.lgnote`)，
        /// 且 `.lgbar{margin-left:auto}` 靠右。图例只列**真实 4 态**（待料 `starved` 原版无出口，LAND-009）。
        /// 最右另留 `build` 标记，供实机核对「游戏跑的是哪一版 dll」。
        /// </summary>
        private float DrawAppCmdBar(Rect rect, float y)
        {
            Rect bar = new Rect(rect.x, y, rect.width, CmdBarHeight);
            UIComponents.Rule(new Rect(bar.x, bar.yMax, bar.width, 1f), UITheme.BorderSoft);
            float x = bar.x + CmdBarPadX;

            ManufacturingChromeSnapshot chrome = CurrentChrome;
            if (chrome != null)
            {
                // `.cmdz#clockbox`：日期（Tiny/Dim）+ 时刻（Small/强调色）
                float dateWidth = Mathf.Min(MeasureWidth(chrome.DateText, GameFont.Tiny) + 6f,
                    bar.width * 0.13f);
                UIComponents.Label(new Rect(x, bar.y, dateWidth, bar.height), chrome.DateText,
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                x += dateWidth;
                float timeWidth = Mathf.Min(MeasureWidth(chrome.TimeText, GameFont.Small) + 8f,
                    bar.width * 0.09f);
                UIComponents.Label(new Rect(x, bar.y, timeWidth, bar.height), chrome.TimeText,
                    GameFont.Small, UITheme.Accent, TextAnchor.MiddleLeft);
                x += timeWidth + CmdBarGap;

                // `.cmdz#powerbox`：`.plbl2`「电网」 + `#pbar`(86×10 + `.capmark`) + `.pnum`
                float powerLeft = x;   // 悬停提示的范围 = 整个电网块（标签 + 条 + 读数）
                string grid = Translate("PersonalChronicle.UI.Chrome.GridLabel");
                float gridWidth = MeasureWidth(grid, GameFont.Tiny) + 4f;
                UIComponents.Label(new Rect(x, bar.y, gridWidth, bar.height), grid,
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                x += gridWidth + 4f;

                Rect track = new Rect(x, bar.y + (bar.height - CmdBarBarHeight) * 0.5f,
                    CmdBarBarWidth, CmdBarBarHeight);
                Widgets.DrawBoxSolid(track, UITheme.BorderHair);
                float share = Mathf.Clamp01(chrome.PowerShare01);
                Widgets.DrawBoxSolid(new Rect(track.x, track.y, track.width * share, track.height),
                    share >= 0.85f ? UITheme.Alive : (share >= 0.60f ? UITheme.Threat : UITheme.Dead));
                UIComponents.Border(track, UITheme.BorderSoft);
                // `.capmark{right:0;width:2px;background:var(--bad)}` 容量上限刻线
                Widgets.DrawBoxSolid(new Rect(track.xMax - 2f, track.y, 2f, track.height), UITheme.Dead);
                x = track.xMax + 6f;

                float valueWidth = Mathf.Min(MeasureWidth(chrome.PowerText, GameFont.Tiny) + 6f, 110f);
                UIComponents.Label(new Rect(x, bar.y, valueWidth, bar.height), chrome.PowerText,
                    GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
                x += valueWidth;
                // 悬停解释两个读数的口径（`PowerLabel` = "电网 · 净收支 / 储能水位"）——
                // 实机曾因**单位漏换算**恒显 0W，没有口径提示时只能靠猜「数据没接进来」。
                if (!string.IsNullOrEmpty(chrome.PowerLabel))
                {
                    TooltipHandler.TipRegion(new Rect(powerLeft, bar.y, x - powerLeft, bar.height),
                        chrome.PowerLabel);
                }
                x += CmdBarGap;
            }

            // `.lgbar`（靠右）：LEGEND 图例 + 4 个真实状态灯
            float stampWidth = MeasureWidth("build " + BuildStamp, GameFont.Tiny) + 10f;
            float legendWidth = Mathf.Min(bar.width * 0.42f, 420f);
            Rect legend = new Rect(bar.xMax - stampWidth - legendWidth - CmdBarPadX, bar.y,
                legendWidth, bar.height);
            // 缩放控件**不再放这里**：新预览把它定为**通栏控制行**（见 `DrawAppZoomRow`，
            // 位于平面图之后、tab 栏之前；实测 1686×31），浮层与 cmdbar 两处都已不再需要。
            if (legend.x > x)
            {
                DrawCmdBarLegend(legend);
            }
            UIComponents.Label(new Rect(bar.xMax - stampWidth, bar.y, stampWidth, bar.height),
                "build " + BuildStamp, GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleRight);
            return bar.yMax + 4f;
        }

        /// <summary>
        /// **顶栏缩放控件**（PM 实机裁决：从浮动 `.zoombar` 迁到 `#cmdbar`）。
        ///
        /// <para>预览把缩放放在 `right:16 / bottom:18` 的浮层里 —— 游戏窗口小，浮层会**压住平面图内容**
        /// （实机截图可见它盖在「打包 PACKING」小框上）。故本工程为**有意偏差**：只保留顶栏**一处**
        /// （不保留浮层，避免两份控件），操作语义与 `.zoombar` 一致：`− / 适配 / 全景 / + / 倍率`。</para>
        /// </summary>
        // ---- 统一主画布的视图态（缩放）常量：唯一被驱动的视图态是 `layoutState` ----
        private const float LayoutMinZoom = WorkshopLayoutCanvas.MinZoom;
        private const float LayoutMaxZoom = WorkshopLayoutCanvas.MaxZoom;

        /// <summary>「全景」档：比内容多看约 66%（对齐预览 `VMAX = VB0.w × 1.6`）。</summary>
        private const float LayoutFullZoom = 0.6f;

        /// <summary>缩放 <see cref="layoutState"/>（统一主画布后，缩放条只驱动它）。</summary>
        private void ZoomLayoutBy(float factor)
        {
            layoutState.Zoom = Mathf.Clamp(layoutState.Zoom * factor, LayoutMinZoom, LayoutMaxZoom);
        }

        private void DrawCmdBarZoom(Rect bar, float left, float right)
        {
            const float buttonWidth = 34f;
            const float wideWidth = 46f;
            const float gap = 4f;
            float y = bar.y + 2f;
            float height = bar.height - 4f;
            float x = left;

            if (x + buttonWidth <= right
                && Widgets.ButtonText(new Rect(x, y, buttonWidth, height), "-"))
            {
                ZoomLayoutBy(0.8f);
            }
            x += buttonWidth + gap;

            // 「适配」= 900 设计宽正好铺满视口（= Zoom 1）⇒ 整图可见
            if (x + wideWidth <= right
                && Widgets.ButtonText(new Rect(x, y, wideWidth, height),
                    Translate("PersonalChronicle.UI.Floor.Zoom.Reset")))
            {
                layoutState.Zoom = 1f;
                layoutState.Pan = Vector2.zero;
            }
            x += wideWidth + gap;

            // 「全景」= 比内容多看约 66%
            if (x + wideWidth <= right
                && Widgets.ButtonText(new Rect(x, y, wideWidth, height),
                    Translate("PersonalChronicle.UI.Floor.Zoom.Full")))
            {
                layoutState.Zoom = LayoutFullZoom;
                layoutState.Pan = Vector2.zero;
            }
            x += wideWidth + gap;

            if (x + buttonWidth <= right
                && Widgets.ButtonText(new Rect(x, y, buttonWidth, height), "+"))
            {
                ZoomLayoutBy(1.25f);
            }
            x += buttonWidth + gap;

            if (x + 46f <= right)
            {
                UIComponents.Label(new Rect(x, bar.y, 46f, bar.height),
                    Mathf.RoundToInt(layoutState.Zoom * 100f).ToString() + "%",
                    GameFont.Tiny, UITheme.Info, TextAnchor.MiddleRight);
            }
        }

        /// <summary>`.lgbar`：`LEGEND 图例` + 4 个真实状态灯 + 名称（灯色与平面图同源，LAND-010 不另立口径）。</summary>
        private static void DrawCmdBarLegend(Rect rect)
        {
            float x = rect.x;
            string cap = Translate("PersonalChronicle.UI.Floor.Legend.Title");
            float capWidth = MeasureWidth(cap, GameFont.Tiny) + 6f;
            UIComponents.Label(new Rect(x, rect.y, capWidth, rect.height), cap,
                GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleLeft);
            x += capWidth + 6f;

            string[] keys =
            {
                "PersonalChronicle.UI.Floor.State.Run",
                "PersonalChronicle.UI.Floor.State.Idle",
                "PersonalChronicle.UI.Floor.State.Fault",
                "PersonalChronicle.UI.Floor.State.Off"
            };
            Color[] colors = { UITheme.Alive, UITheme.Threat, UITheme.Dead, UITheme.Dim };
            for (int i = 0; i < keys.Length; i++)
            {
                string label = Translate(keys[i]);
                float itemWidth = MeasureWidth(label, GameFont.Tiny) + 16f;
                if (x + itemWidth > rect.xMax - 4f)
                {
                    break;
                }
                Widgets.DrawBoxSolid(new Rect(x, rect.y + rect.height * 0.5f - 4f, 8f, 8f), colors[i]);
                UIComponents.Label(new Rect(x + 11f, rect.y, itemWidth - 11f, rect.height), label,
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                x += itemWidth + 6f;
            }
        }

        // ⚠ 2026-10-06（用户指令「移除底栏 Tab 栏整行」）：原 `DrawAppTabBar`（含 2026-10-02 审计 P1-9 的
        //   「统一配额 + 等比压缩 + 溢出 +N + 降级路径能力裁剪」完整实现）与 `MinAppTabWidth`
        //   已整块删除。恢复方式：从 git 历史取回本方法，并在 `DrawAppFloorPanel.fixedRows` 与
        //   `Detail.ComputeDetailHeight` 两处计高加回 `AppTabBarHeight + AppTabBarBottomPad`。

        // ---- 工艺路线切换条（迁入「调度」tab）几何 ----
        private const float RouteRowHeight = 22f;
        private const float RouteRowLabelWidth = 56f;

        /// <summary>该中心是否提供工艺路线（有路线才画切换条）。</summary>
        private static bool HasRouteSwitch(SystemCenterDetailSnapshot detail)
        {
            return detail != null && detail.Workshop != null && detail.Workshop.Routes != null
                && detail.Workshop.Routes.Count > 0;
        }

        /// <summary>
        /// **工艺路线切换条**（原预览 <c>#cmdbar .routebar</c> 的等价物）。
        ///
        /// <para>新预览 2026-09-11 把 `#cmdbar` 改为「时间 + 电网 + 图例」三子节点、删除了
        /// <c>route-bar</c> 元素 ⇒ 切换入口失去归属。按用户裁决迁入「调度」tab：路线是生产调度的
        /// 一部分，且此处与 `#rightcol` 的生产队列/原料仓同屏，语义连贯。</para>
        ///
        /// <para>点击写 <see cref="WorkshopFloorPlan.FloorPlanViewState.RouteIndex"/> —— 平面图的
        /// 路线高亮由 `DrawRoutes` 读同一字段，因此两边永远同源（LAND-010：UI 不另立状态）。</para>
        /// </summary>
        private float DrawRouteSwitchRow(Rect rect, float y, WorkshopFloorSnapshot workshop)
        {
            List<FloorRouteView> routes = workshop.Routes;
            Rect bar = new Rect(rect.x, y, rect.width, RouteRowHeight);
            UIComponents.Label(new Rect(bar.x, bar.y, RouteRowLabelWidth, bar.height),
                Translate("PersonalChronicle.UI.Floor.Route.Title"), GameFont.Tiny, UITheme.Dim,
                TextAnchor.MiddleLeft);

            float x = bar.x + RouteRowLabelWidth + 6f;
            for (int i = 0; i < routes.Count; i++)
            {
                FloorRouteView route = routes[i];
                string label = route == null
                    ? "—"
                    : (string.IsNullOrEmpty(route.Name) ? route.NameEn : route.Name);
                float width = Mathf.Min(MeasureWidth(label, GameFont.Tiny) + 18f, rect.width * 0.3f);
                if (x + width > bar.xMax)
                {
                    break;
                }
                Rect button = new Rect(x, bar.y + 1f, width, bar.height - 2f);
                bool active = floorView.RouteIndex == i;
                if (active)
                {
                    Widgets.DrawBoxSolid(button, UITheme.WithAlpha(UITheme.Accent, 0.18f));
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(button);
                }
                UIComponents.Border(button, active ? UITheme.Accent : UITheme.BorderSoft);
                UIComponents.Label(new Rect(button.x + 2f, button.y, button.width - 4f, button.height),
                    UIComponents.TruncateToWidth(label, button.width - 4f, GameFont.Tiny),
                    GameFont.Tiny, active ? UITheme.Accent : UITheme.Dim, TextAnchor.MiddleCenter);
                if (Widgets.ButtonInvisible(button))
                {
                    floorView.RouteIndex = i;
                }
                x += width + 4f;
            }
            return bar.yMax + 4f;
        }

        // ---- B1 外壳三件（新预览 2026-09-11 实测值）----
        /// <summary>`#cline` 实测高 23px（`padding:3px 13px`）。</summary>
        private const float ClineRowHeight = 23f;

        /// <summary>`#zoom-bar` 通栏行实测高 31px（`padding:5px 13px 6px`，`position:static`）。</summary>
        private const float ZoomRowHeight = 31f;

        /// <summary>`#zoom-bar{padding:5px 13px 6px}` 的左右内距。</summary>
        private const float ZoomRowPadX = 13f;

        /// <summary>
        /// 两侧轨道槽宽 = **轨道默认宽** + 与画布间隙 10（预览：轨道 126 → `#floor` 左右各让出 136）。
        ///
        /// <para>⚠ 2026-10-06（布局优化）：由 <see cref="WorkshopFloorPlan.RailWidthDefault"/> **派生** ——
        /// 轨道默认宽抬到 172（窄轨文案硬切是用户截图实证的根因）后，若让位仍写死 136，
        /// 轨道会盖住画布 36px：那一区域的落点会被 <c>Mouse.IsOver(画布)</c> 判真而绘制在轨道下
        /// ⇒ "拖到轨道上松手却落进画布"（历史 P1-11 同类事故）。改常量引用即结构上不可能再漂移。</para>
        /// </summary>
        private const float RailGutter = WorkshopFloorPlan.RailWidthDefault + 10f;

        /// <summary>最近一次平面图视口左上角（供"绝对坐标"读数换算）。</summary>
        private float lastFloorVpX;
        private float lastFloorVpY;

        /// <summary>
        /// ② 主画布模式（预览 `#cline` 的「查看 ｜ 编排」分段）：
        /// <c>false</c> = 只读俯视（<see cref="WorkshopFloorPlan"/>）；
        /// <c>true</c> = 自由编排画布（<see cref="WorkshopLayoutCanvas"/>，与预览"同一画布两种模式"一致）。
        /// <para>**模式本身**是 UI 瞬时状态（GATE-UI，不入存档）；**编排数据**走
        /// <see cref="Application.Manufacturing.ManufacturingLayoutService"/> 入存档。</para>
        /// </summary>
        private bool floorEditMode;

        /// <summary>
        /// 最近一次视口内光标的设计坐标读数（预览 `#xyout` 的等价物）。
        /// <c>X = NaN</c> 表示本帧光标不在画布内（绘制为 `— · —`）。
        /// </summary>
        private Vector2 lastCursorDesign = new Vector2(float.NaN, float.NaN);

        /// <summary>编排画布状态（**位置随存档**，见 MasterControlTabWindow.LoadLayoutFromSave/MirrorLayoutToSave）+ 托盘条目缓存。</summary>
        private WorkshopLayoutCanvas.CanvasState layoutState = WorkshopLayoutCanvas.CanvasState.Create();
        private readonly List<PaletteEntry> layoutPalette =
            new List<PaletteEntry>();

        /// <summary>
        /// 是否已从存档读过布局。
        ///
        /// <para>⚠ 2026-10-02 审计 P0-2：这个"每档只读一次"的闩锁是**设备卡保存不住**的根因之一。
        /// 窗口实例是 <c>MainButtonDef.tabWindowInt</c> <b>缓存单例</b>（Cecil 取证：
        /// <c>MainTabsRoot.ToggleTab → WindowStack.TryRemove → Window.PreClose</c>，切标签只移除窗口、
        /// <b>不销毁实例</b>）⇒ 该标志永不复位 ⇒ 切走标签再回来时既不重读车间布局，
        /// 也不重读设备清单，而设备清单刚被 <c>ClearSession</c> 清空 ⇒ 画布空白（存档仍有数据）。
        /// 现在改为**可重复调用 + 幂等</b>（函数内先 Clear），闩锁只用于避免同帧重复 IO。</para>
        /// </summary>
        private bool layoutLoadedFromSave;

        /// <summary>当前已写入存档的车间区 ID 集（用于检测"被收回"并同步删除）。</summary>
        private readonly List<int> layoutSavedZones = new List<int>();
        private readonly List<int> layoutTempIds = new List<int>();
        private readonly List<Vector2> layoutTempPos = new List<Vector2>();

        /// <summary>
        /// 读档 → 画布状态。<b>幂等</b>：每次调用先清空 <c>PlacedZoneIds / PlacedPos</c>
        /// （旧实现无 Clear，一旦闩锁放开就会重复 Add ⇒ 重复卡片 + 坐标错位）。
        /// 设备卡位置由 <see cref="WorkshopLayoutCanvas.EnsureSessionScope"/> 统一负责重读
        /// （本方法不再重复调用，避免两处加载时机打架）。
        /// </summary>
        private void LoadLayoutFromSave()
        {
            layoutLoadedFromSave = true;
            // ⚠ 顺序：先让画布侧完成**设备清单重读**，再拍基线快照 ——
            //   否则基线里的 DetachedList 是上一局的空清单 ⇒ 第一次"撤销"会把设备全抹掉。
            //   EnsureSessionScope 幂等（作用域未变且无重读请求时立即返回），重复调用无成本。
            PersonalChronicle.Manufacturing.UI.WorkshopLayoutCanvas.EnsureSessionScope();
            layoutState.PlacedZoneIds.Clear();
            layoutState.PlacedPos.Clear();
            Application.Manufacturing.ManufacturingLayoutService.LoadInto(layoutTempIds, layoutTempPos);
            layoutSavedZones.Clear();
            for (int i = 0; i < layoutTempIds.Count; i++)
            {
                layoutSavedZones.Add(layoutTempIds[i]);
            }
            // 车间区是否仍在托盘里要等 palette 采集后才知道 ⇒ 下一帧由 MirrorLayoutToSave 兜底校验，
            // 这里只把**能立刻解析**的填进画布（避免"画布空白 + 存档被清"的双杀）。
            for (int i = 0; i < layoutTempIds.Count; i++)
            {
                if (FindPaletteIndex(layoutTempIds[i]) < 0)
                {
                    continue;   // 车间区已不存在（被删/换图）⇒ 不画，等 Mirror 清理
                }
                layoutState.PlacedZoneIds.Add(layoutTempIds[i]);
                layoutState.PlacedPos.Add(layoutTempPos[i]);
            }
            // 读档完成 ⇒ 立刻拍一次**基线快照**（P0-3：让第一次"撤销"回退到刚读进来的状态，
            // 而不是抹掉它；同时让撤销按钮的门禁从"有快照"起就成立）。
            WorkshopLayoutCanvas.EstablishBaseline(ref layoutState);
        }

        private int FindPaletteIndex(int zoneId)
        {
            return PersonalChronicle.Manufacturing.UI.WorkshopLayoutCanvas
                .PaletteIndexOf(layoutPalette, zoneId);
        }

        /// <summary>
        /// 把画布状态的变更镜像进存档（幂等；每帧调用，n 很小）。
        ///
        /// <para>⚠ 2026-10-02 审计 P1-3：现在**按 ZoneId 直接读写**（不再用托盘下标反查），
        /// 且删除判定用 ZoneId 集合比对 —— 旧实现拿 <c>FindPaletteIndex</c> 的返回值（可能是 -1）
        /// 去和 <c>PlacedOrder</c> 比对，下标位移时会误删玩家没碰过的车间。</para>
        /// </summary>
        private void MirrorLayoutToSave()
        {
            if (layoutPalette.Count == 0)
            {
                return;
            }
            for (int i = 0; i < layoutState.PlacedZoneIds.Count; i++)
            {
                int zoneId = layoutState.PlacedZoneIds[i];
                if (zoneId < 0)
                {
                    continue;
                }
                Application.Manufacturing.ManufacturingLayoutService.Save(zoneId, layoutState.PlacedPos[i]);
                if (!layoutSavedZones.Contains(zoneId))
                {
                    layoutSavedZones.Add(zoneId);
                }
            }
            // 已收回的（档里有、画布上无）⇒ 从档里删掉
            for (int i = layoutSavedZones.Count - 1; i >= 0; i--)
            {
                int zoneId = layoutSavedZones[i];
                if (!layoutState.PlacedZoneIds.Contains(zoneId))
                {
                    Application.Manufacturing.ManufacturingLayoutService.Remove(zoneId);
                    layoutSavedZones.RemoveAt(i);
                }
            }
        }

        // ---- #cline（新预览 R1：分段 + StatChip 流）----
        private const float ClinePadX = 13f;
        private const float ClineChipGap = 4f;

        /// <summary>
        /// `#cline`（新版预览实测 1686×23 · R1）：**「查看 ｜ 编排」分段 + 工序链 Chip 流**。
        ///
        /// <para>预览原文：`[Segmented 查看|编排] [clcap 12 道工序链 · 瓶颈 · 在岗 d/m · 待放置 n]
        /// [chip ×12：序号+车间名+在制] [cltag 自由编排 · 无固定格]`。</para>
        ///
        /// <para>落地口径（LAND-009）：chip 一律取**真实数据** —— 查看态 = `RouteDef` 的工序步骤
        /// （工序号 / 所在车间 / 在制数）；编排态 = 已放置的**真实车间区**。
        /// 「瓶颈高亮」需 ledger 节拍统计（原版 `Bill`/`RecipeDef` 无节拍字段）⇒ 未接入，不画伪高亮。</para>
        /// </summary>
        private float DrawAppClinement(Rect rect, float y, WorkshopFloorSnapshot workshop,
            SystemCenterDetailSnapshot detail)
        {
            Rect bar = new Rect(rect.x, y, rect.width, ClineRowHeight);
            UIComponents.Rule(new Rect(bar.x, bar.yMax, bar.width, 1f), UITheme.BorderSoft);
            float x = bar.x + ClinePadX;

            // ① 左端 Segmented：查看 ｜ 编排（预览 `.cline .seg2`）
            x = DrawClineSegment(bar, x);

            // ② clcap：摘要（查看 = 工序链计数；编排 = 已放置 / 托盘总数）
            string cap = floorEditMode
                ? Translate2("PersonalChronicle.UI.Layout.Placed", layoutState.PlacedZoneIds.Count,
                    layoutPalette.Count)
                : Translate3("PersonalChronicle.UI.Floor.Cline.Summary",
                    workshop != null && workshop.Routes != null ? workshop.Routes.Count : 0,
                    workshop != null ? workshop.WorkshopCount : 0,
                    workshop != null ? workshop.DeviceCount : 0);
            float capWidth = Mathf.Min(MeasureWidth(cap, GameFont.Tiny) + 12f, bar.width * 0.36f);
            UIComponents.Label(new Rect(x, bar.y, capWidth, bar.height),
                UIComponents.TruncateToWidth(cap, capWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            x += capWidth + ClineChipGap;

            // ③ 右端：**缩放控件**（用户指示：与「查看｜编排」同一行的 X 轴，不再占独立通栏行）
            //    宽度 = − 适配 全景 ＋（172）+ 倍率（46）+ 间隔
            const float zoomButtons = 34f + 4f + 46f + 4f + 46f + 4f + 34f;
            const float zoomWidth = zoomButtons + 6f + 46f;
            Rect zoomArea = new Rect(bar.xMax - ClinePadX - zoomWidth, bar.y, zoomWidth, bar.height);
            if (zoomArea.x > x)
            {
                DrawCmdBarZoom(bar, zoomArea.x, zoomArea.xMax);
            }

            // cltag：放在缩放控件左侧（空间不足时不画）
            string tag = Translate("PersonalChronicle.UI.Floor.Cline.Tag");
            float tagWidth = MeasureWidth(tag, GameFont.Tiny) + 4f;
            float zoomLeft = zoomArea.x > x ? zoomArea.x : bar.xMax;
            Rect tagRect = new Rect(zoomLeft - 8f - tagWidth, bar.y, tagWidth, bar.height);
            bool tagShown = tagRect.x > x;
            if (tagShown)
            {
                UIComponents.Label(tagRect, tag, GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleRight);
            }

            // ④ 中部 chip 流
            float chipRight = (tagShown ? tagRect.x : zoomLeft) - 6f;
            float chipHeight = ClineRowHeight - 6f;
            if (floorEditMode)
            {
                x = DrawClinePlacedChips(bar, x, chipRight, chipHeight);
            }
            else
            {
                x = DrawClineRouteChips(bar, x, chipRight, chipHeight, workshop);
            }
            return bar.yMax + 3f;
        }

        /// <summary>`#cline .seg2` 分段（查看 ｜ 编排）—— 切换 ② 主画布模式。返回新的 x。</summary>
        private float DrawClineSegment(Rect bar, float x)
        {
            string view = Translate("PersonalChronicle.UI.Floor.Cline.View");
            string edit = Translate("PersonalChronicle.UI.Floor.Cline.Edit");
            float viewW = MeasureWidth(view, GameFont.Tiny) + 16f;
            float editW = MeasureWidth(edit, GameFont.Tiny) + 16f;
            float h = bar.height - 6f;
            Rect box = new Rect(x, bar.y + 3f, viewW + editW, h);
            UIComponents.Border(box, UITheme.BorderSoft);

            Rect left = new Rect(box.x, box.y, viewW, box.height);
            Rect right = new Rect(box.x + viewW, box.y, editW, box.height);
            DrawClineSegmentButton(left, view, !floorEditMode);
            DrawClineSegmentButton(right, edit, floorEditMode);
            if (Widgets.ButtonInvisible(left))
            {
                floorEditMode = false;
            }
            if (Widgets.ButtonInvisible(right))
            {
                floorEditMode = true;
            }
            return box.xMax + 8f;
        }

        private static void DrawClineSegmentButton(Rect rect, string label, bool on)
        {
            if (on)
            {
                Widgets.DrawBoxSolid(rect, UITheme.WithAlpha(UITheme.Info, 0.20f));
            }
            else
            {
                Widgets.DrawHighlightIfMouseover(rect);
            }
            UIComponents.Label(rect, label, GameFont.Tiny, on ? UITheme.Info : UITheme.Dim,
                TextAnchor.MiddleCenter);
        }

        /// <summary>查看态 chip 流：`RouteDef` 的工序步骤（真实工序号 / 车间 / 在制数）。返回新的 x。</summary>
        private float DrawClineRouteChips(Rect bar, float x, float chipRight, float chipHeight,
            WorkshopFloorSnapshot workshop)
        {
            if (workshop == null || workshop.Routes == null || workshop.Routes.Count == 0)
            {
                return x;
            }
            FloorRouteView route = workshop.Routes[0];
            if (route == null || route.Steps == null)
            {
                return x;
            }
            List<FloorRouteStepView> steps = route.Steps;
            for (int i = 0; i < steps.Count; i++)
            {
                FloorRouteStepView step = steps[i];
                if (step == null)
                {
                    continue;
                }
                string name = workshop.Bays != null && step.Slot >= 0 && step.Slot < workshop.Bays.Count
                    ? workshop.Bays[step.Slot].Name
                    : string.Empty;
                string text = string.IsNullOrEmpty(name)
                    ? step.StepText
                    : step.StepText + " " + name;
                float width = Mathf.Min(MeasureWidth(text, GameFont.Tiny) + 18f, 168f);
                if (x + width > chipRight)
                {
                    break;
                }
                bool selected = floorView.SelectedSlot == step.Slot;
                Rect chip = new Rect(x, bar.y + 3f, width, chipHeight);
                if (selected)
                {
                    Widgets.DrawBoxSolid(chip, UITheme.WithAlpha(UITheme.Accent, 0.18f));
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(chip);
                }
                UIComponents.Border(chip, selected ? UITheme.Accent : UITheme.BorderSoft);
                UIComponents.Label(new Rect(chip.x + 3f, chip.y, chip.width - 6f, chip.height),
                    UIComponents.TruncateToWidth(text, chip.width - 6f, GameFont.Tiny),
                    GameFont.Tiny, selected ? UITheme.Accent : UITheme.Dim, TextAnchor.MiddleCenter);
                TooltipHandler.TipRegion(chip, Translate2("PersonalChronicle.UI.Floor.Cline.ChipTip",
                    step.StepText, step.WipCount));
                if (Widgets.ButtonInvisible(chip))
                {
                    floorView.SelectedSlot = step.Slot;
                    floorView.SelectedDevice = -1;
                }
                x += width + ClineChipGap;
                x = DrawClineChipSeparator(bar, x, chipRight, i < steps.Count - 1);
            }
            return x;
        }

        /// <summary>编排态 chip 流：已放置的真实车间区（点 chip = 闪烁定位）。返回新的 x。</summary>
        private float DrawClinePlacedChips(Rect bar, float x, float chipRight, float chipHeight)
        {
            for (int i = 0; i < layoutState.PlacedZoneIds.Count; i++)
            {
                int paletteIndex = FindPaletteIndex(layoutState.PlacedZoneIds[i]);
                if (paletteIndex < 0)
                {
                    continue;
                }
                string text = layoutPalette[paletteIndex].Label;
                float width = Mathf.Min(MeasureWidth(text, GameFont.Tiny) + 18f, 168f);
                if (x + width > chipRight)
                {
                    break;
                }
                bool flashing = layoutState.FlashIndex == paletteIndex
                    && DateTime.UtcNow.Ticks < layoutState.FlashUntilTicks;
                Rect chip = new Rect(x, bar.y + 3f, width, chipHeight);
                if (flashing)
                {
                    Widgets.DrawBoxSolid(chip, UITheme.WithAlpha(UITheme.Accent, 0.18f));
                }
                else
                {
                    Widgets.DrawHighlightIfMouseover(chip);
                }
                UIComponents.Border(chip, flashing ? UITheme.Accent : UITheme.BorderSoft);
                UIComponents.Label(new Rect(chip.x + 3f, chip.y, chip.width - 6f, chip.height),
                    UIComponents.TruncateToWidth(text, chip.width - 6f, GameFont.Tiny),
                    GameFont.Tiny, flashing ? UITheme.Accent : UITheme.Dim, TextAnchor.MiddleCenter);
                Vector2 design = layoutState.PlacedPos[i];
                TooltipHandler.TipRegion(chip, Translate2("PersonalChronicle.UI.Layout.BlockAt",
                    Mathf.RoundToInt(design.x), Mathf.RoundToInt(design.y)));
                if (Widgets.ButtonInvisible(chip))
                {
                    WorkshopLayoutCanvas.Flash(ref layoutState, paletteIndex);
                }
                x += width + ClineChipGap;
                x = DrawClineChipSeparator(bar, x, chipRight, i < layoutState.PlacedZoneIds.Count - 1);
            }
            return x;
        }

        /// <summary>chip 之间的 `›` 分隔（预览 `.cline .csep`）。返回新的 x。</summary>
        private static float DrawClineChipSeparator(Rect bar, float x, float chipRight, bool more)
        {
            if (!more || x + 12f > chipRight)
            {
                return x;
            }
            UIComponents.Label(new Rect(x, bar.y, 12f, bar.height), "›", GameFont.Tiny,
                UITheme.Muted, TextAnchor.MiddleCenter);
            return x + 12f;
        }

        // ---- #lbar（新预览：编排态才显示，实测 25px）----
        private const float LbarRowHeight = 25f;
        private const float LbarPadX = 13f;

        /// <summary>
        /// `#lbar`（新版预览的**编排条**，编排态显示、查看态不占高）：
        /// `[plab 自由编排] [待放置 n/12] [|] [坐标网格] [清空画布] [撤销] [Save 结构预览（悬停）]`
        ///
        /// <para>预览原文还有「保存布局 / 导出码 / 导入 / ★ 玩家预设 chip」—— 预览侧为**会话内**的
        /// `USER_LAYOUTS`（不入档）；落地侧编排数据已**随存档**（`mfgLayout`），玩家预设列表属新增
        /// 持久化结构，登记为**下一批**（避免本轮擅自扩 Save schema）。</para>
        /// </summary>
        private float DrawAppLayoutBar(Rect rect, float y, WorkshopFloorSnapshot workshop)
        {
            Rect bar = new Rect(rect.x, y, rect.width, LbarRowHeight);
            UIComponents.Rule(new Rect(bar.x, bar.yMax, bar.width, 1f), UITheme.BorderSoft);
            float x = bar.x + LbarPadX;
            float buttonHeight = LbarRowHeight - 8f;

            // plab：琥珀左边线的标签
            string title = Translate("PersonalChronicle.UI.Floor.Lbar.Title");
            float titleWidth = MeasureWidth(title, GameFont.Tiny) + 10f;
            UIComponents.Label(new Rect(x, bar.y, titleWidth, bar.height), title,
                GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleLeft);
            Widgets.DrawBoxSolid(new Rect(x, bar.y + 4f, 2f, bar.height - 8f), UITheme.Accent);
            x += titleWidth + 8f;

            // 待放置 n / m
            WorkshopLayoutCanvas.CollectPalette(Find.CurrentMap, layoutPalette);
            int total = layoutPalette.Count;
            int unplaced = total - layoutState.PlacedZoneIds.Count;
            string count = Translate2("PersonalChronicle.UI.Floor.Lbar.Unplaced", unplaced, total);
            float countWidth = MeasureWidth(count, GameFont.Tiny) + 8f;
            UIComponents.Label(new Rect(x, bar.y, countWidth, bar.height), count,
                GameFont.Tiny, unplaced > 0 ? UITheme.Threat : UITheme.Alive, TextAnchor.MiddleLeft);
            x += countWidth + 8f;
            Widgets.DrawBoxSolid(new Rect(x, bar.y + 6f, 1f, bar.height - 12f), UITheme.BorderSoft);
            x += 9f;

            // ② 托盘 chips **已移除**（用户实机反馈：与左轨「设备库」功能重叠）。
            //    自由编排改为**从设备库放置设备**（拖放或点选，见 `WorkshopLayoutCanvas`），
            //    车间则由底栏「编排」页签的小画布放置。

            // 待放置提示（**两态都可见**）：设备库「点选 → 点画布」的当前选择 + 操作提示。
            //   2026-10-02 审计 P1-7：此前放置**零反馈**（SetDetached 不写 LastAction、
            //   查看态又没有 #lbar / 指标行）⇒ 玩家摆完看不到任何"已放置"迹象，判定"没生效 / 没保存"。
            string placeHint = Translate("PersonalChronicle.UI.Layout.PlaceHintIdle");
            string pending = WorkshopLayoutCanvas.PendingDevicePick;
            if (!string.IsNullOrEmpty(pending))
            {
                placeHint = Translate("PersonalChronicle.UI.Layout.PlaceHintActive");
            }
            if (!string.IsNullOrEmpty(layoutState.LastAction))
            {
                placeHint = layoutState.LastAction;
            }
            float hintWidth = Mathf.Min(MeasureWidth(placeHint, GameFont.Tiny) + 8f,
                bar.width * 0.3f);
            if (x + hintWidth < bar.xMax - LbarPadX - 260f)
            {
                UIComponents.Label(new Rect(x, bar.y, hintWidth, bar.height), placeHint, GameFont.Tiny,
                    string.IsNullOrEmpty(pending) ? UITheme.Muted : UITheme.Info,
                    TextAnchor.MiddleLeft);
                x += hintWidth + 6f;
            }

            // 坐标网格（与编排页签的同一状态字段）
            x = DrawLbarToggle(bar, x, buttonHeight, Translate("PersonalChronicle.UI.Layout.ToggleGrid"),
                layoutState.ShowGrid, ref layoutState.ShowGrid);
            // 叠加标注
            x = DrawLbarToggle(bar, x, buttonHeight,
                Translate("PersonalChronicle.UI.Layout.ToggleAnnotations"), layoutState.ShowAnnotations,
                ref layoutState.ShowAnnotations);
            // 对齐吸附（CHG-MFG-ALIGN-001 · M2）：拖动设备卡时**按住 Shift** 对齐到邻近卡的
            //   边线 / 中心线。整条 #lbar 只在编排态绘制 ⇒ 查看态天然不出现（无需额外门禁）。
            //   提示随「画布上是否还有别的卡」切换措辞 —— 无目标时开关仍可点，不静默失效。
            x = DrawLbarToggle(bar, x, buttonHeight, WorkshopLayoutCanvas.AlignSnapLabel,
                layoutState.SnapEnabled, ref layoutState.SnapEnabled,
                WorkshopLayoutCanvas.AlignSnapTip(
                    WorkshopLayoutCanvas.HasAlignTarget(layoutState, layoutPalette)));

            // 撤销：**仅在真的持有快照时出现**（P0-3：旧门禁在读档后为真 ⇒ 一点即清空全部并删档）
            if (WorkshopLayoutCanvas.CanUndo(layoutState))
            {
                string undo = Translate("PersonalChronicle.UI.Layout.Undo");
                float undoWidth = MeasureWidth(undo, GameFont.Tiny) + 18f;
                if (x + undoWidth <= bar.xMax - LbarPadX)
                {
                    Rect undoRect = new Rect(x, bar.y + 4f, undoWidth, buttonHeight);
                    if (Widgets.ButtonText(undoRect, undo))
                    {
                        WorkshopLayoutCanvas.UndoLast(ref layoutState);
                    }
                    x += undoWidth + 6f;
                }
            }

            // 清空画布
            string clear = Translate("PersonalChronicle.UI.Floor.Lbar.Clear");
            float clearWidth = MeasureWidth(clear, GameFont.Tiny) + 18f;
            if (x + clearWidth <= bar.xMax - LbarPadX)
            {
                Rect clearRect = new Rect(x, bar.y + 4f, clearWidth, buttonHeight);
                if (Widgets.ButtonText(clearRect, clear))
                {
                    WorkshopLayoutCanvas.ClearAll(ref layoutState);
                }
                x += clearWidth + 6f;
            }

            // 右侧：Save 结构预览（真实 scope 文案；悬停展开明细）
            string save = Translate2("PersonalChronicle.UI.Layout.SavePreview",
                layoutState.PlacedZoneIds.Count, WorkshopLayoutCanvas.LayoutSchemaVersion);
            float saveWidth = Mathf.Min(MeasureWidth(save, GameFont.Tiny) + 12f, bar.width * 0.3f);
            Rect saveRect = new Rect(bar.xMax - LbarPadX - saveWidth, bar.y, saveWidth, bar.height);
            UIComponents.Label(saveRect, UIComponents.TruncateToWidth(save, saveWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Info, TextAnchor.MiddleRight);
            // ⚠ 2026-10-02 审计 P0-4（LOC-001）：原文案键含 `{0}` 却走**无参** Translate
            //   ⇒ 悬停恒显示字面量 `{0}`；且它只描述车间容器，看不到设备卡。
            //   现在复用 `SavePreviewText` —— 与编排页签**同一份**明细（两个持久化容器都列出）。
            TooltipHandler.TipRegion(saveRect,
                WorkshopLayoutCanvas.SavePreviewText(ref layoutState, layoutPalette));
            return bar.yMax + 3f;
        }

        /// <summary>编排条上的小开关（点击翻转；返回新的 x）。</summary>
        /// <param name="tip">可选悬停提示（CHG-MFG-ALIGN-001 起支持；此前无提示需求）。</param>
        private static float DrawLbarToggle(Rect bar, float x, float height, string label, bool on,
            ref bool value, string tip = null)
        {
            float width = MeasureWidth(label, GameFont.Tiny) + 16f;
            if (x + width > bar.xMax - LbarPadX)
            {
                return x;
            }
            Rect rect = new Rect(x, bar.y + 4f, width, height);
            if (on)
            {
                Widgets.DrawBoxSolid(rect, UITheme.WithAlpha(UITheme.Accent, 0.18f));
            }
            else
            {
                Widgets.DrawHighlightIfMouseover(rect);
            }
            UIComponents.Border(rect, on ? UITheme.Accent : UITheme.BorderSoft);
            UIComponents.Label(rect, label, GameFont.Tiny, on ? UITheme.Accent : UITheme.Dim,
                TextAnchor.MiddleCenter);
            if (Widgets.ButtonInvisible(rect))
            {
                value = !value;
            }
            if (!string.IsNullOrEmpty(tip))
            {
                TooltipHandler.TipRegion(rect, tip);
            }
            return x + width + 6f;
        }

        /// <summary>
        /// `#zoom-bar`（新预览：由浮层改为**通栏控制行**，实测 1686×31，位于平面图之后、tab 栏之前）。
        ///
        /// <para>预览内容 = 「缩放 − ＋ 适配 全景 ｜ **镜头锁定原点** ｜ **绝对坐标 +X→右 +Y↓下 X · Y** ｜
        /// 滚轮缩放 · 适配/全景均以原点展开 ｜ **倍率**」。落地实现：</para>
        /// <list type="bullet">
        /// <item>左：`缩放` 标签 + `− 适配 全景 +` 按钮组 + 倍率；</item>
        /// <item>右：`原点` 按钮（平移归零 = 镜头锁定画布原点）+ **绝对坐标读数**（相机在设计坐标里的位置，
        /// <c>0,0</c> 即已锁定原点，用绿色表示）。</item>
        /// </list>
        /// </summary>
        private float DrawAppZoomRow(Rect rect, float y)
        {
            Rect bar = new Rect(rect.x, y, rect.width, ZoomRowHeight);
            UIComponents.Rule(new Rect(bar.x, bar.y, bar.width, 1f), UITheme.BorderSoft);
            float x = bar.x + ZoomRowPadX;

            // 缩放按钮组与倍率**已上移到 `#cline` 行**（用户指示），本行只留：提示 → 光标坐标 → 口径 → 原点按钮
            float right = bar.xMax - ZoomRowPadX;

            string hint = Translate("PersonalChronicle.UI.Floor.Zoom.Hint");
            float hintWidth = MeasureWidth(hint, GameFont.Tiny) + 6f;
            if (bar.width > 980f && right - hintWidth - 10f > x)
            {
                right -= hintWidth + 10f;
                UIComponents.Label(new Rect(right, bar.y, hintWidth, bar.height), hint,
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleRight);
            }

            // 光标坐标读数（预览 `#xyout`）：**统一主画布后只有一种口径** = 中央原点绝对坐标
            bool valid = !float.IsNaN(lastCursorDesign.x);
            string coord = valid
                ? Translate2("PersonalChronicle.UI.Floor.Zoom.Cursor",
                    Mathf.RoundToInt(lastCursorDesign.x), Mathf.RoundToInt(lastCursorDesign.y))
                : Translate("PersonalChronicle.UI.Floor.Zoom.CursorNone");
            float coordWidth = Mathf.Max(88f, MeasureWidth(coord, GameFont.Tiny) + 10f);
            if (right - coordWidth > x)
            {
                right -= coordWidth;
                UIComponents.Label(new Rect(right, bar.y, coordWidth, bar.height), coord,
                    GameFont.Tiny, valid ? UITheme.Info : UITheme.Muted, TextAnchor.MiddleRight);
            }

            string scope = Translate("PersonalChronicle.UI.Floor.Zoom.ScopeOrigin");
            float scopeWidth = MeasureWidth(scope, GameFont.Tiny) + 6f;
            if (bar.width > 1220f && right - scopeWidth - 10f > x)
            {
                right -= scopeWidth + 10f;
                UIComponents.Label(new Rect(right, bar.y, scopeWidth, bar.height), scope,
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleRight);
            }

            // 「原点」按钮：镜头归零（统一主画布 ⇒ 只归零 `layoutState`）
            float lockWidth = Mathf.Min(
                MeasureWidth(Translate("PersonalChronicle.UI.Floor.Zoom.LockOrigin"), GameFont.Tiny) + 18f,
                96f);
            if (right - lockWidth > x)
            {
                right -= lockWidth;
                Rect origin = new Rect(right, bar.y + 4f, lockWidth, bar.height - 8f);
                if (Widgets.ButtonText(origin, Translate("PersonalChronicle.UI.Floor.Zoom.LockOrigin")))
                {
                    layoutState.Pan = Vector2.zero;
                }
                TooltipHandler.TipRegion(origin,
                    Translate("PersonalChronicle.UI.Floor.Zoom.LockOriginTip"));
            }
            return bar.yMax + 4f;
        }

        /// <summary>
        /// ② 主画布（查看 / 编排**同一块**）：收集托盘 → 读档 → 画自由画布 → 镜像回档 → 刷新光标读数。
        /// <paramref name="interactive"/> = <c>false</c> 时只读（查看态：不画 × / 不响应拖拽与落点）。
        /// </summary>
        private string DrawAppLayoutCanvas(Rect canvasRect, bool interactive)
        {
            WorkshopLayoutCanvas.CollectPalette(Find.CurrentMap, layoutPalette);
            if (!layoutLoadedFromSave)
            {
                LoadLayoutFromSave();
            }
            WorkshopLayoutCanvas.DrawCanvasBody(canvasRect, layoutPalette, ref layoutState,
                Time.realtimeSinceStartup, interactive, out string clickedDeviceStableId);
            MirrorLayoutToSave();
            UpdateCursorReadoutEdit(canvasRect);
            return clickedDeviceStableId;
        }

        /// <summary>
        /// 自由画布上点了设备卡 ⇒ 按 `stableId`（`&lt;defName&gt;:&lt;thingIDNumber&gt;`）回指快照里的
        /// <see cref="FloorDeviceView"/> 并打开详情弹窗（与旧俯视 `HandleSelection` 等价）。
        /// </summary>
        private void OpenDeviceDetailByStableId(WorkshopFloorSnapshot workshop, string stableId)
        {
            if (workshop == null || workshop.Devices == null)
            {
                return;
            }
            for (int i = 0; i < workshop.Devices.Count; i++)
            {
                FloorDeviceView view = workshop.Devices[i];
                if (view != null && view.StableId == stableId)
                {
                    OpenDeviceDetail(view);
                    return;
                }
            }
        }

        /// <summary>编排态光标读数：**中央原点绝对坐标**（预览 `#xyout` 口径，允许负值）。</summary>
        private void UpdateCursorReadoutEdit(Rect floorRect)
        {
            lastCursorDesign = new Vector2(float.NaN, float.NaN);
            float k = WorkshopLayoutCanvas.Scale(floorRect, layoutState);
            if (k <= 0.0001f || Event.current == null)
            {
                return;
            }
            Vector2 mouse = UI.MousePositionOnUIInverted;
            if (!floorRect.Contains(mouse))
            {
                return;
            }
            Vector2 origin = WorkshopLayoutCanvas.OriginScreenPos(floorRect, layoutState);
            lastCursorDesign = new Vector2((mouse.x - origin.x) / k, (mouse.y - origin.y) / k);
        }

        /// <summary>当前 tab 所需高度（复用各面板的 BlockHeight，与绘制同式）。</summary>
        private float ActivePaneHeight(float width, SystemCenterDetailSnapshot detail)
        {
            switch (selectedFloorTab)
            {
                case TabCockpit:
                    // ⚠ 2026-10-06（用户指令「kpi 也要移除」）：「总控」页签的内容原为 ① 九卡 KPI 带；
                    //   KPI 已整块移除 ⇒ 该页签无内容（高度 0，外层 `paneHeight > 1f` 门控会跳过绘制）。
                    //   分支保留：它维持 tab 语义完整（其余页签同理，见变更标注「6 页签暂无入口」待裁决）。
                    return 0f;
                case TabLayout:
                    // 「编排」：托盘(24) + 画布(≥120) + 指标(20) + 间隙（外层再按 .fpanes 上限 214 夹取）
                    return 172f;
                case TabDispatch:
                    float dispatch = detail.StockQueue != null && !detail.StockQueue.IsEmpty
                        ? ManufacturingStockQueuePanels.BlockHeight(width)
                        : 0f;
                    if (HasRouteSwitch(detail))
                    {
                        dispatch += RouteRowHeight + 4f;   // 路线切换条（见 DrawRouteSwitchRow）
                    }
                    return dispatch;
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
                default:
                    float h = 0f;
                    if (detail.DataLayer != null && !detail.DataLayer.IsEmpty)
                    {
                        h += ManufacturingDataLayerPanels.BlockHeight(width);
                    }
                    if (detail.Bridge != null && !detail.Bridge.IsEmpty)
                    {
                        h += ManufacturingBridgePanels.BlockHeight(width);
                    }
                    return h;
            }
        }

        /// <summary>画当前 tab 的内容（高度已被 <see cref="ActivePaneHeight"/> 限定为 max 214）。</summary>
        private void DrawActivePane(Rect pane, SystemCenterDetailSnapshot detail)
        {
            float needed = ActivePaneHeight(pane.width, detail);
            Rect content = pane;
            // ⚠ 2026-10-02 审计 P1-10：「编排」页签**禁用页内滚动**。
            //   画布用 `Event.current.mousePosition` 做命中与落点换算，而 `BeginScrollView` 会平移
            //   GUI 坐标系 ⇒ 卡位/落点/悬停三者在滚动容器内不同源（点击偏移、ghost 错位）。
            //   该页签高度固定（172），由 `DrawAppFloorPanel` 保证不被压缩，因此不需要滚动。
            bool scroll = selectedFloorTab != TabLayout && needed > pane.height + 1f;
            if (scroll)
            {
                Widgets.BeginScrollView(pane, ref paneScroll, new Rect(0f, 0f, pane.width - 16f, needed));
                content = new Rect(0f, 0f, pane.width - 16f, needed);
            }
            try
            {
                switch (selectedFloorTab)
                {
                    case TabCockpit:
                        // ⚠ 2026-10-06（用户指令「kpi 也要移除」）：原绘 ① 九卡 KPI 带
                        //   （`DrawAppKpiBand`，带逐块失败隔离）。制造中心不再产出 KPI ⇒ 本页签无内容；
                        //   且 `ActivePaneHeight` 对它恒返回 0 ⇒ 外层 `paneHeight > 1f` 门控不会走到这里。
                        //   保留空 case：tab 语义与 6 页签的"待裁决"状态保持一致（见变更标注）。
                        break;
                    case TabLayout:
                        // 第 7 页签「编排」：**自由编排画布**（新版预览 `#fp-layout`）。
                        // 托盘 = 真实车间区；画布 = 原点居中的无边界自由摆放。
                        // 只读俯视仍保留在 ② 主面板（PM 裁决 A：两者并存、互不替代）。
                        // 布局**随存档**（PM 裁决「存入」）：读档加载一次 + 每帧把变更镜像回车间的组件。
                        // UI 只经 Application 门面读写，不直接触碰 Data（ARC-001）。
                        // ⚠ try 覆盖至 MirrorLayoutToSave 为止：渲染失败时**不得**把（可能已坏的）
                        //   会话态再镜像回存档 —— catch 分支不含镜像调用，即为正确收口。
                        try
                        {
                            WorkshopLayoutCanvas.CollectPalette(Find.CurrentMap, layoutPalette);
                            if (!layoutLoadedFromSave)
                            {
                                LoadLayoutFromSave();
                            }
                            WorkshopLayoutCanvas.Draw(content, layoutPalette, ref layoutState,
                                Time.realtimeSinceStartup);
                            MirrorLayoutToSave();
                        }
                        catch (Exception ex)
                        {
                            DrawDetailPanelFallback(content, content.y, ex);
                        }
                        break;
                    case TabDispatch:
                        // 路线切换条与队列块**各自**隔离：前者坏了不连坐后者。
                        float dispatchY = content.y;
                        // 工艺路线切换条：新预览删除了 `#cmdbar .routebar` 元素（我方已上报），
                        // 按裁决迁入「调度」tab —— 路线本就是生产调度的一部分。
                        if (HasRouteSwitch(detail))
                        {
                            try
                            {
                                dispatchY = DrawRouteSwitchRow(content, dispatchY, detail.Workshop);
                            }
                            catch (Exception ex)
                            {
                                dispatchY = DrawDetailPanelFallback(content, dispatchY, ex);
                            }
                        }
                        if (detail.StockQueue != null && !detail.StockQueue.IsEmpty)
                        {
                            try
                            {
                                ManufacturingStockQueuePanels.Draw(content, dispatchY, detail.StockQueue);
                            }
                            catch (Exception ex)
                            {
                                DrawDetailPanelFallback(content, dispatchY, ex);
                            }
                        }
                        break;
                    case TabAnalysis:
                        if (detail.Analysis != null && !detail.Analysis.IsEmpty)
                        {
                            try
                            {
                                ManufacturingAnalysisPanels.Draw(content, content.y, detail.Analysis);
                            }
                            catch (Exception ex)
                            {
                                DrawDetailPanelFallback(content, content.y, ex);
                            }
                        }
                        break;
                    case TabOps:
                        if (detail.Operations != null && !detail.Operations.IsEmpty)
                        {
                            try
                            {
                                ManufacturingOperationsPanels.Draw(content, content.y, detail.Operations);
                            }
                            catch (Exception ex)
                            {
                                DrawDetailPanelFallback(content, content.y, ex);
                            }
                        }
                        break;
                    case TabEvent:
                        if (detail.Events != null && !detail.Events.IsEmpty)
                        {
                            try
                            {
                                ManufacturingEventsPanels.Draw(content, content.y, detail.Events);
                            }
                            catch (Exception ex)
                            {
                                DrawDetailPanelFallback(content, content.y, ex);
                            }
                        }
                        break;
                    case TabData:
                        float y = content.y;
                        if (detail.DataLayer != null && !detail.DataLayer.IsEmpty)
                        {
                            try
                            {
                                y = ManufacturingDataLayerPanels.Draw(content, y, detail.DataLayer);
                            }
                            catch (Exception ex)
                            {
                                y = DrawDetailPanelFallback(content, y, ex);
                            }
                        }
                        if (detail.Bridge != null && !detail.Bridge.IsEmpty)
                        {
                            try
                            {
                                ManufacturingBridgePanels.Draw(content, y, detail.Bridge);
                            }
                            catch (Exception ex)
                            {
                                DrawDetailPanelFallback(content, y, ex);
                            }
                        }
                        break;
                }
            }
            finally
            {
                if (scroll)
                {
                    Widgets.EndScrollView();
                }
            }
        }
    }
}

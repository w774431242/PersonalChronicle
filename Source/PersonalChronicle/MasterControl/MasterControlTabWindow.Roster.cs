using PersonalChronicle.MasterControl.Population;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// Partial of <see cref="MasterControlTabWindow"/> ——人员概览的<b>面板内容</b>与<b>人员名册</b>。
    ///
    /// <para>名册是预览 <c>#v-pawn</c> 的入口：点一行 → 让下方承载的档案馆视图定位到
    /// 该人的档案（**实际 API：`PersonnelArchivePanel.RequestPawnDetail(stableId)`**，见
    /// <c>PersonnelArchivePanel.Nav.cs</c>；⚠ 旧注释里的 `PersonnelArchiveHost.FocusPawn` 是**笔误**，
    /// 该成员从未存在过 —— 2026-09-15 审计核实后校正），而不是在总控里重写一份人物档案
    /// （P0 裁决：禁止重写 <c>PersonnelArchivePanel.PawnDetail</c> 的五段绘制）。</para>
    /// </summary>
    public sealed partial class MasterControlTabWindow
    {
        // ---- 面板内容 ----------------------------------------------------------
        //
        // 【CHG-20260914-002】原「人员名册」面板（9 列表）已删除：承载区的档案视图自带
        // 12 列人物表（`PersonnelArchivePanel.PawnTable.cs`，列更多且可下钻到人物档案），
        // 同屏两张人员表 = 同一批读数画两次（预览人员中心也只有一张表）。

        /// <summary>
        /// ① 规模与构成（预览 597-750）：现役大数 → **身份环形（Donut）** + **性别金字塔**
        /// （左右镜像条）→ 能力均值 → **能力面积图** → 11 项能力条列表。
        /// 环形与面积等图表**一律经 `RuifBridge` 走 RUIF**（`RUIF.Charts.*`）；本工程不再自绘图表
        /// （要求二：禁止自研组件）。
        /// </summary>
        private void DrawScalePanel(Rect row, PopulationOverviewSnapshot snapshot)
        {
            // 【预览 HTML 604】`.pbody.tight{display:grid;grid-template-columns:1.35fr 1fr 1fr;gap:16px}`
            // —— ① 面板内部是**三列**（身份 | 性别 | 能力），不是竖向堆叠；旧实现竖排导致
            // 面板高度近两倍于预览（用户报「布局空间不符合」的主因）。
            // ⚠ 2026-09-15 对照清单修正：列宽**不是三等分** —— 预览实测 `1.35fr : 1fr : 1fr`
            // （1920 档 724.7 / 536.8 / 536.8），身份列需要更多横向空间（环 + 图例 + 趋势卡）。
            float scaleUnit = Mathf.Max(40f, (row.width - ScaleColumnGap * 2f)
                / (ScaleCol1Ratio + ScaleCol2Ratio + ScaleCol3Ratio));
            float col1Width = scaleUnit * ScaleCol1Ratio;
            float col2Width = scaleUnit * ScaleCol2Ratio;
            float col3Width = scaleUnit * ScaleCol3Ratio;
            Rect col1 = new Rect(row.x, row.y, col1Width, row.height);
            Rect col2 = new Rect(col1.xMax + ScaleColumnGap, row.y, col2Width, row.height);
            // 列 3 用**右边界反推**定位，避免浮点累计误差让它比 `row.xMax` 差一两像素。
            Rect col3 = new Rect(row.xMax - col3Width, row.y, col3Width, row.height);

            // 列 1：段标题（预览 `.sec-h`「现役殖民者 N 人 · 身份构成」+ 右端注释）+ 身份 Donut。
            // ⚠ **2026-09-17 用户裁决：移除「身份人数趋势」卡** ⇒ 本列不再画卡（原 164px）；
            //   卡的绘制（`DrawIdentityTrendCard` / 图例切显隐 / 悬停十字线与逐段气泡）与数据采集
            //   （`IdentityTrend*` 快照字段 + 8 季桶）**全部保留**，接回只需恢复 `DrawIdentityRing`
            //   尾部的调用并把 `JoinTrendCardHeight` 计回 `PanelHeights[0]` 的 `scaleCol1`。
            // ⚠ 2026-09-15 结构对齐：旧实现是「标签左 / 数值右」的**键值行**（RowLive），
            //   而预览 HTML 686 是**段标题**（现役人数并进文案、注释在右端、前面还有 info 竖条）
            //   —— 两者形态不同（用户实机截图对比指出：标题形态与预览不一致）。
            float y1 = DrawScaleSectionHeader(col1, col1.y, PopTextOf(PopText.IdentityHeader),
                PopTextOf(PopText.IdentityNote));
            DrawIdentityRing(new Rect(col1.x, y1, col1.width, Mathf.Max(24f, col1.yMax - y1)), snapshot);

            // 列 2：段标题（「性别构成」+ 注释）+ 金字塔 + 性比 + 分身份明细表
            DrawGenderPyramid(new Rect(col2.x, col2.y, col2.width,
                Mathf.Min(GenderPyramidBlockHeight, col2.height)), snapshot);

            // 列 3：段标题（「健康能力」+ 注释）+ **一行读数**（综合能力均值 / 工效）+ 11 项能力条（双列）
            // ⚠ **2026-09-17 用户裁决：移除「综合能力均值」趋势卡**（原 164px = 104px 折线区 + 口径行）：
            //   该卡的数据源是**活读值、无历史**（原设计即空态「暂无季度快照」）⇒ 移除不丢任何真实序列，
            //   也不必再占 164px。**两个真实读数压缩成一行保留**（信息不丢，见 `PopText.CapacityScope`）。
            //   卡内绘制（`DrawCapacityTrendCard`）与数据采集**全部保留**，接回只需恢复上一行的调用
            //   并把 `CapacityTrendCardHeight` 计回 `PanelHeights[0]` 的 `scaleCol3`。
            float y3 = DrawScaleSectionHeader(col3, col3.y, PopTextOf(PopText.HealthHeader),
                PopTextOf(PopText.HealthNote));
            UIComponents.Label(new Rect(col3.x, y3, col3.width, CapacitySummaryRowHeight),
                UIComponents.TruncateToWidth(PopTextOf(PopText.CapacityScope), col3.width, GameFont.Tiny),
                GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            y3 += CapacitySummaryRowHeight;
            // 【分列方式 = 预览的**两个独立 `.bars` 网格**（HTML 823-839）⇒ **列优先**填充】
            // 左列 = 前 ⌈n/2⌉ 项（意识 / 移动 / 操作 / 语言 / 进食 / 视觉 = defs[0..5]）、
            // 右列 = 其余（听觉 / 呼吸 / 血液过滤 / 血液循环 / 消化 = defs[6..10]）。
            // ⚠ 旧实现按 `i % 2` **逐行填充**，且注释断言「预览为 row-major」——**与权威预览相反**：
            //   实机把「视觉 / 听觉」这类相邻能力拆到两列，读数顺序与预览不符。
            float barColumnWidth = Mathf.Max(40f, (col3.width - ScaleBarColumnGap) * 0.5f);
            int leftCount = (snapshot.Capacities.Count + 1) / 2;
            for (int i = 0; i < snapshot.Capacities.Count; i++)
            {
                bool rightColumn = i >= leftCount;
                int column = rightColumn ? 1 : 0;
                int rowInColumn = rightColumn ? i - leftCount : i;
                DrawCapacityBar(
                    new Rect(col3.x + column * (barColumnWidth + ScaleBarColumnGap),
                        y3 + rowInColumn * CapacityRowHeight, barColumnWidth, CapacityRowHeight),
                    snapshot.Capacities[i]);
            }
            // 无能力样本时如实说明（旧实现 `DrawCapacityChart` 静默 return ⇒ 列 3 只剩标题，无任何线索）。
            if (snapshot.Capacities.Count == 0)
            {
                UIComponents.Label(new Rect(col3.x, y3, col3.width, PopulationRowHeight),
                    UIComponents.TruncateToWidth(PopTextOf(PopText.CapacityNoSample), col3.width, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            }
        }

        /// <summary>
        /// ① 面板**列内段标题**（预览 `.sec-h`，CSS 198-200）：3×10 的 info 色竖条 + 标签（Muted）
        /// + 右端注释（Dim，`.sec-h .n{margin-left:auto}`）。
        ///
        /// <para>⚠ 命名带 `Scale` 前缀：`MasterControlTabWindow.Shell.cs` 已有一个 3 参的
        /// `DrawSectionHeader`（系统中心列表页的分区标题）—— 同名重载虽可编译，但两个语义不同的
        /// 标题混在一处会让调用点读不出用哪个（P3 可读性）。</para>
        /// <para>⚠ 实机没有「粗体 10.5px」这一档 ⇒ 取 Tiny 字号（LAND-007 下限约束）；
        /// 注释占可用宽的 55%（**不做 `Text.CalcSize` 测量**：绘制路径零测量，右对齐 + 尾部截断已足够）。</para>
        /// <para>返回下一行的 y（= 段标题行高 <see cref="IdentityHeaderHeight"/>，含 2px 行距）。</para>
        /// </summary>
        private static float DrawScaleSectionHeader(Rect col, float y, string label, string note)
        {
            RUIF.Draw.Box(new Rect(col.x, y + 4f, 3f, 10f), UITheme.Info);
            float textX = col.x + 9f;                 // 3px 竖条 + 6px 间距（预览 gap:6px）
            float textWidth = Mathf.Max(0f, col.xMax - textX);
            float noteWidth = string.IsNullOrEmpty(note) ? 0f : textWidth * 0.55f;
            float labelWidth = Mathf.Max(0f, textWidth - noteWidth - (noteWidth > 0f ? 6f : 0f));
            UIComponents.Label(new Rect(textX, y, labelWidth, PopulationRowHeight),
                UIComponents.TruncateToWidth(label, labelWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            if (noteWidth > 0f)
            {
                UIComponents.Label(new Rect(col.xMax - noteWidth, y, noteWidth, PopulationRowHeight),
                    UIComponents.TruncateToWidth(note, noteWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleRight);
            }
            return y + IdentityHeaderHeight;
        }

        /// <summary>
        /// 环形/图例配色（自由 / 奴隶 / 囚犯）—— 静态复用，不在绘制期分配。
        /// **语义映射取自预览源码**（HTML 622-634）：自由 = `--alive` · 奴隶 = `--warn` · 囚犯 = `--info`。
        /// ⚠ 旧实现用 Info/Threat（蓝/红），与预览的语义不符（用户截图对比指出：预览里奴隶为暖色、囚犯为冷色）。
        /// </summary>
        private static readonly Color[] IdentityRingColors =
        {
            UITheme.Alive, UITheme.Warn, UITheme.Info
        };

        /// <summary>环形数据暂存（IMGUI 单线程；避免每帧 new）。</summary>
        private readonly float[] identityRingValues = new float[3];

        // （原 `TransparentColor` 已删除：它只服务于自研 `UICharts.Ring` 的透明背景参数；
        //   改走 RUIF `DonutChart` 后无调用点。）

        /// <summary>① 身份构成环形：段 = 自由 / 奴隶 / 囚犯；环心写现役人数，右侧图例逐行给「标签 + 数量」。</summary>
        private float DrawIdentityRing(Rect rect, PopulationOverviewSnapshot snapshot)
        {
            // 直径自适应：预览环 150px 是建立在①面板双宽（s8）之上；实机 ① 为等宽三列时
            // 列宽约 196px，环最多占半宽（右侧留图例），其余按高度收缩（LAND-005 响应式）。
            // ⚠ 2026-09-17 用户裁决**移除「身份人数趋势」卡** ⇒ 不再预留其高度（计高侧 `scaleCol1`
            // 已同步去掉 `+4f + JoinTrendCardHeight`）；环改用列体全高（仅留 4px 呼吸），不再被压扁。
            float ringZoneH = Mathf.Max(24f, rect.height - 4f);
            float side = Mathf.Min(IdentityRingSize,
                Mathf.Min(rect.width * 0.5f, ringZoneH));
            Rect ring = new Rect(rect.x, rect.y, side, side);
            identityRingValues[0] = snapshot.FreeShare01;
            identityRingValues[1] = snapshot.SlaveShare01;
            identityRingValues[2] = snapshot.PrisonerShare01;
            // 【RUIF 硬依赖 · 用户要求一/二】身份环走 RUIF `Charts.DonutChart`（禁止自研）。
            // 环孔比：预览环带厚 / 外半径 = 0.298 ⇒ 内径 / 外径 ≈ 0.70（⚠ RUIF `hole01` 的精确语义
            // 待实机核对；先按"内径比"传 0.70 并在此登记）。中心主数字经 `centerLabel` 交给 RUIF 绘制。
            // 数字取值：预览 45px 超 LAND-007 上限 22 ⇒ 由 RUIF 按其字号令牌决定（登记 B）。
            RuifBridge.TryDonutChart(ring, identityRingValues, 0.70f, Num(snapshot.LiveColonists),
                IdentityRingColors, 0.40f);
            // 中心副标「IN SERVICE」（预览 HTML 618；RUIF `centerLabel` 只承载单行 ⇒ 副标仍由文本原子画）。
            UIComponents.Label(new Rect(ring.x, ring.y + ring.height * 0.58f, ring.width, PopulationRowHeight),
                PopTextOf(PopText.InService), GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleCenter);
            float legendX = ring.xMax + 10f;
            float legendWidth = Mathf.Max(0f, rect.xMax - legendX);
            float lineY = rect.y + 2f;
            string[] labels =
            {
                PopTextOf(PopText.RowFree), PopTextOf(PopText.RowSlave), PopTextOf(PopText.RowPrisoner)
            };
            int[] counts =
            {
                snapshot.FreeColonists, snapshot.SlaveColonists, snapshot.PrisonerColonists
            };
            for (int i = 0; i < labels.Length; i++)
            {
                if (lineY + LegendLineHeight > rect.yMax)
                {
                    break;
                }
                RUIF.Draw.Box(new Rect(legendX, lineY + 5f, 7f, 7f), IdentityRingColors[i]);
                // 预览图例 = 「色块 · 标签(dim) · **数量(语义色)** · 百分比(dim)」三段
                // （HTML 622-634：自由殖民者 8 66.7%）—— 数量着色是预览的可读性关键，
                // 单串同色会让「数量」淹没在标签里（用户截图对比指出）。
                float textX = legendX + 12f;
                float textWidth = Mathf.Max(0f, legendWidth - 12f);
                float countWidth = Mathf.Min(26f, textWidth * 0.28f);
                float shareWidth = Mathf.Min(46f, textWidth * 0.34f);
                float nameWidth = Mathf.Max(18f, textWidth - countWidth - shareWidth - 4f);
                UIComponents.Label(new Rect(textX, lineY, nameWidth, PopulationRowHeight),
                    UIComponents.TruncateToWidth(labels[i], nameWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                UIComponents.Label(new Rect(textX + nameWidth + 2f, lineY, countWidth, PopulationRowHeight),
                    Num(counts[i]), GameFont.Tiny, IdentityRingColors[i], TextAnchor.MiddleRight);
                string shareText = ShareTextOf(i, snapshot);
                if (!string.IsNullOrEmpty(shareText))
                {
                    UIComponents.Label(new Rect(textX + nameWidth + countWidth + 4f, lineY, shareWidth, PopulationRowHeight),
                        shareText, GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleRight);
                }
                lineY += LegendLineHeight;
            }

            // ── 趋势卡（预览 ① col1，HTML 726-752）──────────────────────────────
            // 动态卡位：按「环实际直径 vs 图例高度」取大者，避免 IdentityRingBlockHeight 固定 156
            // 在窄列下浪费上部空间、把卡压到底部。
            float legendHeight = Mathf.Min(3f * LegendLineHeight, ringZoneH);
            float ringBlockHeight = Mathf.Max(side, legendHeight);
            // ══ 2026-09-17 用户裁决：**移除「身份人数趋势」卡** ══
            // 原来是「环 + 图例 + 空 4px + 164px 趋势卡（104px 折线区）」，现只画环与图例。
            // 卡内绘制（`DrawIdentityTrendCard` / 图例切换 / 悬停十字线与气泡 / 逐段气泡文本）
            // 与数据采集（`IdentityTrend*` 快照字段 + `SerializeData` 8 季桶）**全部保留** ——
            // 接回只需在本行恢复 `DrawIdentityTrendCard(...)` 并把 `JoinTrendCardHeight` 计回
            // `PanelHeights[0]` 的 `scaleCol1`（见该处 [接回预留]）。
            return rect.y + ringBlockHeight;
        }

        /// <summary>趋势图例文案缓冲（3 项；复用避免每帧 `new string[]` —— PERF-001）。</summary>
        private readonly string[] identityTrendLegendLabels = new string[3];

        /// <summary>
        /// ① 列 1 **身份趋势卡**（预览 HTML 726-752）：标题 + 量程 → 图例（可点切显隐）
        /// → 折线区 104px → x 轴 3 个季度标签；悬停给十字线 + 逐段数值气泡。
        ///
        /// <para><b>交互</b>（状态全部在窗口实例内存，**不入存档** —— GATE-UI）：① 悬停十字线 + 气泡；
        /// ② 图例点击切单系列显隐（至少保留一条）；③ 周期切换（日 / 周 / 季）**不落地** ——
        /// 三种粒度各需一套历史序列，工程只有 8 季桶（LAND-009：不得用单一粒度冒充三档）。</para>
        /// </summary>
        private void DrawIdentityTrendCard(Rect card, PopulationOverviewSnapshot snapshot)
        {
            RUIF.Shapes.InsetPanel(card);
            const float pad = 7f;
            float innerWidth = Mathf.Max(0f, card.width - pad * 2f);
            float y = card.y + 3f;

            // ① 标题（左）+ 量程（右）—— 预览 727-729。
            string range = PopTextOf(PopText.TrendRange);
            float rangeWidth = string.IsNullOrEmpty(range) ? 0f : Mathf.Min(innerWidth * 0.42f, 96f);
            float titleWidth = Mathf.Max(0f, innerWidth - rangeWidth - (rangeWidth > 0f ? 6f : 0f));
            UIComponents.Label(new Rect(card.x + pad, y, titleWidth, PopulationRowHeight),
                UIComponents.TruncateToWidth(PopTextOf(PopText.TrendTitle), titleWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            if (rangeWidth > 0f)
            {
                UIComponents.Label(new Rect(card.xMax - pad - rangeWidth, y, rangeWidth, PopulationRowHeight),
                    UIComponents.TruncateToWidth(range, rangeWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleRight);
            }
            y += PopulationRowHeight;

            // ② 图例（可点切显隐）+ 右端交互提示 —— 预览 736-742。
            int visible = DrawIdentityTrendLegend(new Rect(card.x + pad, y, innerWidth, PopulationRowHeight));
            y += PopulationRowHeight;

            // ③ 折线区（预览 745-746：`svg` 高 104px）。
            Rect plot = new Rect(card.x + pad, y, innerWidth, JoinTrendPlotHeight);
            if (!snapshot.IdentityTrendReady)
            {
                // 空态：如实说明（旧档 / 事件未满一季）—— 不画 0 假线。
                UIComponents.Label(new Rect(plot.x, plot.y + JoinTrendPlotHeight * 0.4f, plot.width,
                        PopulationRowHeight),
                    UIComponents.TruncateToWidth(PopTextOf(PopText.TrendNoHistory), plot.width, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            }
            else if (visible > 0)
            {
                // 【三系列】自由 / 奴隶 / 囚犯 —— 颜色与环形图例同源（IdentityRingColors 语义色）；
                // 隐藏项**从入参里真正剔除**（传全零序列会被 RUIF 归一化当成 0 线，视觉上仍在）。
                RuifBridge.TryLineChartMulti(plot, IdentityTrendSeries(visible, snapshot),
                    IdentityTrendColors(visible), true, true);
                DrawIdentityTrendHover(plot, snapshot);
            }
            y += JoinTrendPlotHeight;

            // ④ x 轴 3 个季度标签（预览 751：`justify-content:space-between` ⇒ 首左 / 中中 / 末右）。
            string[] labels = snapshot.IdentityTrendBucketLabels;
            if (snapshot.IdentityTrendReady && labels != null && labels.Length >= 2)
            {
                int last = labels.Length - 1;
                float third = innerWidth / 3f;
                DrawIdentityAxisLabel(new Rect(card.x + pad, y, third, PopulationRowHeight), labels[0],
                    TextAnchor.MiddleLeft);
                DrawIdentityAxisLabel(new Rect(card.x + pad + third, y, third, PopulationRowHeight),
                    labels[last / 2], TextAnchor.MiddleCenter);
                DrawIdentityAxisLabel(new Rect(card.xMax - pad - third, y, third, PopulationRowHeight),
                    labels[last], TextAnchor.MiddleRight);
            }
        }

        /// <summary>趋势卡 x 轴标签（Dim + 尾部截断）。</summary>
        private static void DrawIdentityAxisLabel(Rect rect, string text, TextAnchor anchor)
        {
            UIComponents.Label(rect, UIComponents.TruncateToWidth(text, rect.width, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, anchor);
        }

        /// <summary>
        /// 趋势卡图例（预览 736-742）：色点 + 标签；**点击切换该系列显隐**（与环形图例同色），
        /// 悬停给「点击显示 / 隐藏」。返回点击后的**可见系列数**（供折线入参裁剪）。
        /// </summary>
        private int DrawIdentityTrendLegend(Rect row)
        {
            identityTrendLegendLabels[0] = PopTextOf(PopText.RowFree);
            identityTrendLegendLabels[1] = PopTextOf(PopText.RowSlave);
            identityTrendLegendLabels[2] = PopTextOf(PopText.RowPrisoner);
            int visible = 0;
            for (int i = 0; i < identityTrendLegendLabels.Length; i++)
            {
                if (!identityTrendHidden[i])
                {
                    visible++;
                }
            }
            float hintWidth = Mathf.Min(row.width * 0.34f, 92f);
            float itemWidth = Mathf.Max(40f, (row.width - hintWidth) / identityTrendLegendLabels.Length);
            for (int i = 0; i < identityTrendLegendLabels.Length; i++)
            {
                Rect item = new Rect(row.x + i * itemWidth, row.y, itemWidth, row.height);
                bool hidden = identityTrendHidden[i];
                RUIF.Draw.Box(new Rect(item.x, item.y + 5f, 7f, 7f),
                    hidden ? UITheme.WithAlpha(IdentityRingColors[i], 0.30f) : IdentityRingColors[i]);
                float textWidth = Mathf.Max(0f, item.width - 11f);
                UIComponents.Label(new Rect(item.x + 11f, item.y, textWidth, item.height),
                    UIComponents.TruncateToWidth(identityTrendLegendLabels[i], textWidth, GameFont.Tiny),
                    GameFont.Tiny, hidden ? UITheme.Dim : UITheme.Muted, TextAnchor.MiddleLeft);
                if (Mouse.IsOver(item))
                {
                    TooltipHandler.TipRegion(item, PopTextOf(PopText.TrendLegendHint));
                }
                if (Widgets.ButtonInvisible(item))
                {
                    if (hidden)
                    {
                        identityTrendHidden[i] = false;
                        visible++;
                    }
                    else if (visible > 1)
                    {
                        // 至少保留一条可见（全隐藏 ⇒ 图无意义，与预览 CSS 注释同规）。
                        identityTrendHidden[i] = true;
                        visible--;
                    }
                }
            }
            UIComponents.Label(new Rect(row.xMax - hintWidth, row.y, hintWidth, row.height),
                UIComponents.TruncateToWidth(PopTextOf(PopText.TrendHint), hintWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleRight);
            return visible;
        }

        /// <summary>
        /// 悬停交互（预览 719-725 ①）：RUIF `Charts.LineChart` **契约上不读 Mouse**
        /// ⇒ 索引判定、十字线与气泡都由调用方自己做。
        ///
        /// <para><b>为什么按「分片命中矩形」而不是读鼠标坐标</b>：<c>Event.current.mousePosition</c>
        /// **认 `GUI.matrix` 但不认 `GUI.BeginGroup` 的平移** —— 概览区块画在带滚动的分组里，
        /// 一旦用坐标反算索引，滚动后必然整体偏移。逐段 `Mouse.IsOver(分片)` 与绘制矩形**同一坐标系**，
        /// 零坐标假设、零状态（GATE-UI：不引持久状态）。</para>
        /// </summary>
        private void DrawIdentityTrendHover(Rect plot, PopulationOverviewSnapshot snapshot)
        {
            float[] series = snapshot.IdentityTrendFree;
            int buckets = series != null ? series.Length : 0;
            if (buckets < 2 || identityTrendTipsCache == null || !Mouse.IsOver(plot))
            {
                return;
            }
            float slice = plot.width / buckets;              // 命中片宽（= 段间距）
            float step = plot.width / (buckets - 1);         // 数据点间距（首末贴边）
            for (int i = 0; i < buckets; i++)
            {
                Rect hit = new Rect(plot.x + i * slice, plot.y, slice, plot.height);
                if (!Mouse.IsOver(hit))
                {
                    continue;
                }
                // 十字线画在该段**数据点**的 x 上（分片中心与数据点位置在首末两段不一致，如实按数据点画）。
                RUIF.Draw.Box(new Rect(plot.x + i * step, plot.y, 1f, plot.height),
                    UITheme.WithAlpha(UITheme.Text, 0.35f));
                if (i < identityTrendTipsCache.Length)
                {
                    TooltipHandler.TipRegion(hit, identityTrendTipsCache[i]);
                }
                return;
            }
        }

        /// <summary>
        /// 可见系列入参（按可见数索引的**预分配**数组 ⇒ 每帧零分配）。
        /// 元素是快照数组的**引用**（不复制），隐藏与否则不参与。
        /// </summary>
        private float[][] IdentityTrendSeries(int visible, PopulationOverviewSnapshot snapshot)
        {
            float[][] buffer = identityTrendSeriesByCount[visible];
            if (buffer == null)
            {
                buffer = new float[visible][];
                identityTrendSeriesByCount[visible] = buffer;
            }
            int slot = 0;
            if (!identityTrendHidden[0]) { buffer[slot++] = snapshot.IdentityTrendFree; }
            if (!identityTrendHidden[1]) { buffer[slot++] = snapshot.IdentityTrendSlave; }
            if (!identityTrendHidden[2]) { buffer[slot] = snapshot.IdentityTrendPrisoner; }
            return buffer;
        }

        /// <summary>可见系列的语义色（与环形图例同源；同样按可见数预分配 ⇒ 零分配）。</summary>
        private Color[] IdentityTrendColors(int visible)
        {
            Color[] buffer = identityTrendColorsByCount[visible];
            if (buffer == null)
            {
                buffer = new Color[visible];
                identityTrendColorsByCount[visible] = buffer;
            }
            int slot = 0;
            for (int i = 0; i < identityTrendHidden.Length; i++)
            {
                if (!identityTrendHidden[i])
                {
                    buffer[slot++] = IdentityRingColors[i];
                }
            }
            return buffer;
        }

        /// <summary>
        /// 图例行高 —— 预览 HTML 620 `flex-column; gap:8px` + 行内容 12px ⇒ 行高 ≈ 22px。
        /// 旧值 16 比预览紧 6px/行（用户截图对比指出图例偏挤）。
        /// </summary>
        private const float LegendLineHeight = 22f;

        /// <summary>
        /// ① 列 2「**性别构成**」（预览 HTML 758-780 全量落地，**段序严格对齐**）：
        /// ① 段标题（`.sec-h`「性别构成」+ `.n` 注释）；
        /// ② 按身份的**金字塔**（3 行镜像条：左男右女，条宽 = 该身份人数 / 最大身份人数，中缝放身份标签）；
        /// ③ **性比行**（`◀ 男 N · x%` ｜ `性比 m/f` ｜ `女 N · y% ▶`）；
        /// ④ **说明行**（`.rule-lbl`「分身份明细…」—— 预览 767 在**表前**，旧实现画在表后，段序不符）；
        /// ⑤ **分身份明细表**（身份 / 男 / 女 / 小计 四列 + accent 合计行）。
        /// 数据 = <c>IdentityFemale[3] / IdentityMale[3]</c>（与性别总数**同循环累加**，见 Provider）。
        /// </summary>
        private float DrawGenderPyramid(Rect rect, PopulationOverviewSnapshot snapshot)
        {
            // ⚠ 行高铁律：Tiny 文本 rect 高度**必须 ≥ 18**（= `Measure.RowHeight(GameFont.Tiny)`）。
            // 原版 `Widgets.Label` **不裁剪**：行高给 16/14 时文字会溢出 rect，被随后绘制的
            // 下一行 / 容器底色盖住（实机现象：「概览页好多文本部分被遮盖」）。
            float y = DrawScaleSectionHeader(rect, rect.y, PopTextOf(PopText.RowGenderLabel),
                PopTextOf(PopText.GenderNote));

            int[] male = snapshot.IdentityMale;
            int[] female = snapshot.IdentityFemale;
            int maxSide = 1;
            // 长度防御：数组由快照保证为 3，但旧档 / 反序列化异常时报错比越界崩面板更可诊断。
            if (male == null || female == null || male.Length < IdentityCount || female.Length < IdentityCount)
            {
                if (ChronicleLog.Throttled("pop_gender_array_probe", 60000))
                {
                    ChronicleLog.Error(ChronicleLog.Category.Ui,
                        "gender pyramid: identity arrays missing/short (male="
                        + (male == null ? "null" : male.Length.ToString()) + ", female="
                        + (female == null ? "null" : female.Length.ToString()) + ")");
                }
                return y;
            }
            for (int i = 0; i < IdentityCount; i++)
            {
                if (male[i] > maxSide) { maxSide = male[i]; }
                if (female[i] > maxSide) { maxSide = female[i]; }
            }
            // 诊断探针（仅异常时输出、60s 节流）：明细表会直接显示 male/female 原值，
            // 若明细表有数而金字塔无条 ⇒ 用本行日志判定是「数据为 0」还是「条被画到看不见的位置」。
            if (maxSide <= 1 && (snapshot.MaleColonists > 0 || snapshot.FemaleColonists > 0)
                && ChronicleLog.Throttled("pop_gender_pyramid_probe", 60000))
            {
                ChronicleLog.Error(ChronicleLog.Category.Ui,
                    "gender pyramid: cross-count empty but totals>0 ⇒ male=["
                    + male[0] + "," + male[1] + "," + male[2] + "] female=["
                    + female[0] + "," + female[1] + "," + female[2] + "] total="
                    + snapshot.MaleColonists + "/" + snapshot.FemaleColonists);
            }

            // ① 金字塔：左男条 · 中缝身份标签 · 右女条（预览 `.pyr-r{m|lbl|f}`，HTML 759-763）
            // ⚠ 语义色取自预览源码：男 = `--info` → `UITheme.Info`、女 = `--t-social`
            //   → `UITheme.TimelineSocial`（旧实现把女性画成 `Threat` 红，与预览不符）。
            // ⚠ 行距 2px / 条高 10px 对齐预览 CSS `.pyr{gap:2px}` + `.pyr-r .m,.f{height:10px}`
            //   （旧实现条高 8、行距 0 ⇒ 金字塔整体偏细偏挤）。
            float labelWidth = Mathf.Min(54f, rect.width * 0.34f);
            float barMax = Mathf.Max(6f, (rect.width - labelWidth) * 0.5f - 8f);
            float centerX = rect.x + rect.width * 0.5f;
            for (int i = 0; i < IdentityCount; i++)
            {
                float barY = y + (PopulationRowHeight - GenderPyramidBarHeight) * 0.5f;
                float femaleWidth = barMax * (female[i] / (float)maxSide);
                float maleWidth = barMax * (male[i] / (float)maxSide);
                if (maleWidth > 0.5f)
                {
                    RUIF.Draw.Box(
                        new Rect(centerX - labelWidth * 0.5f - 5f - maleWidth, barY, maleWidth,
                            GenderPyramidBarHeight),
                        UITheme.Info);
                }
                if (femaleWidth > 0.5f)
                {
                    RUIF.Draw.Box(
                        new Rect(centerX + labelWidth * 0.5f + 5f, barY, femaleWidth, GenderPyramidBarHeight),
                        UITheme.TimelineSocial);
                }
                UIComponents.Label(
                    new Rect(centerX - labelWidth * 0.5f, y, labelWidth, PopulationRowHeight),
                    UIComponents.TruncateToWidth(PopTextOf(IdentityGenderRoles[i]), labelWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleCenter);
                y += GenderPyramidRowHeight;
            }

            // ② 性比行（预览 764-766）：**三段** = `◀ 男 N · x%` ｜ `性比 m/f` ｜ `女 N · y% ▶`
            // 与金字塔同向（左男右女）；两侧百分比分母 = 现役殖民者数（与 Provider Share01 同源）。
            // ⚠ 三段文案全部取**缓存期拼好的 Keyed 模板**（旧实现在绘制期拼接、且没有 ◀ ▶ —— LOC-004）。
            float third = rect.width / 3f;
            UIComponents.Label(new Rect(rect.x, y, third, PopulationRowHeight),
                UIComponents.TruncateToWidth(PopTextOf(PopText.GenderRatioMale), third - 2f, GameFont.Tiny),
                GameFont.Tiny, UITheme.Info, TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(rect.x + third, y, third, PopulationRowHeight),
                UIComponents.TruncateToWidth(PopTextOf(PopText.GenderRatioValue), third - 2f, GameFont.Tiny),
                GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleCenter);
            UIComponents.Label(new Rect(rect.x + third * 2f, y, third, PopulationRowHeight),
                UIComponents.TruncateToWidth(PopTextOf(PopText.GenderRatioFemale), third - 2f, GameFont.Tiny),
                GameFont.Tiny, UITheme.TimelineSocial, TextAnchor.MiddleRight);
            y += PopulationRowHeight;

            // ③ 说明行（预览 767 `.rule-lbl.plain` 位于**表前**）—— 旧实现画在合计行**之后** ⇒ 段序与预览不符。
            UIComponents.Label(new Rect(rect.x, y, rect.width, PopulationRowHeight),
                UIComponents.TruncateToWidth(PopTextOf(PopText.GenderScope), rect.width, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            y += PopulationRowHeight;

            // ④ 分身份明细表（预览 768-780）：列宽 `1fr 34px 34px 42px` + 行高 18 + 合计行 accent。
            float scale = Mathf.Min(1f, rect.width / 160f);
            float numWidth = 34f * scale;
            float lastWidth = 42f * scale;
            float firstWidth = Mathf.Max(28f, rect.width - numWidth * 2f - lastWidth);
            // ⚠ 列序 = **身份 / 男 / 女 / 小计**（预览 HTML 768-772 的表头顺序）。
            DrawGenderTableRow(rect.x, y, firstWidth, numWidth, lastWidth,
                PopTextOf(PopText.GenderColIdentity), PopTextOf(PopText.GenderColMale),
                PopTextOf(PopText.GenderColFemale), PopTextOf(PopText.GenderColSubtotal), false,
                PopulationRowHeight);
            y += PopulationRowHeight;
            for (int i = 0; i < IdentityCount; i++)
            {
                DrawGenderTableRow(rect.x, y, firstWidth, numWidth, lastWidth,
                    PopTextOf(IdentityGenderRoles[i]), male[i].ToString(), female[i].ToString(),
                    (female[i] + male[i]).ToString(), false, PopulationRowHeight);
                y += PopulationRowHeight;
            }
            DrawGenderTableRow(rect.x, y, firstWidth, numWidth, lastWidth,
                PopTextOf(PopText.GenderColTotal), snapshot.MaleColonists.ToString(),
                snapshot.FemaleColonists.ToString(), snapshot.LiveColonists.ToString(), true,
                PopulationRowHeight);
            return y + PopulationRowHeight;
        }

        /// <summary>身份三态的数量（自由 / 奴隶 / 囚犯，顺序 = Provider 交叉计数下标，GOV-009）。</summary>
        private const int IdentityCount = 3;

        /// <summary>身份文案槽（顺序对齐 <c>IdentityMale/IdentityFemale</c> 下标）。</summary>
        private static readonly PopText[] IdentityGenderRoles =
        {
            PopText.RowFree, PopText.RowSlave, PopText.RowPrisoner
        };

        /// <summary>
        /// 身份图例的占比文本（预览 ① col1：自由殖民者 8 66.7%）。
        /// 分母 = 现役殖民者数（与 Provider 的 Share01 同源）；无现役 ⇒ 空串（**不画 0.0%**）。
        /// </summary>
        private static string ShareTextOf(int index, PopulationOverviewSnapshot snapshot)
        {
            if (snapshot.LiveColonists <= 0)
            {
                return string.Empty;
            }
            float share = index == 0
                ? snapshot.FreeShare01
                : (index == 1 ? snapshot.SlaveShare01 : snapshot.PrisonerShare01);
            return PercentText(share);
        }

        /// <summary>
        /// 占比文本（0.4167 → 「41.7%」）。⚠ **不用 "P1"**：InvariantCulture 的百分号格式会
        /// 在数字与 % 之间**插入空格**（"100.0 %"），与预览 "100.0%" 不符（实机截图可见该空格）。
        /// </summary>
        private static string PercentText(float share01)
        {
            return (Mathf.Clamp01(share01) * 100f)
                .ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>性比文本 = 男 / 女（保留 2 位；女为 0 ⇒ 「—」，不除以零、不假装 0）。</summary>
        private static string GenderRatioText(PopulationOverviewSnapshot snapshot)
        {
            if (snapshot.FemaleColonists <= 0)
            {
                return "—";
            }
            return (snapshot.MaleColonists / (float)snapshot.FemaleColonists)
                .ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 性别明细表的一行（首列左对齐；男 / 女 / 小计右对齐；合计行用 accent 色）。
        /// ⚠ 列宽与预览 grid `1fr 34px 34px 42px` 同源：男/女列等宽、**小计列更宽**
        /// （旧实现三列等宽 `width × 0.15`，与预览的 34/34/42 不符）。
        /// </summary>
        private static void DrawGenderTableRow(float x, float y, float firstWidth, float numWidth,
            float lastWidth, string first, string second, string third, string fourth, bool accent, float height)
        {
            Color color = accent ? UITheme.Accent : UITheme.Text;
            Color headColor = accent ? UITheme.Accent : UITheme.Dim;
            UIComponents.Label(new Rect(x, y, firstWidth, height),
                UIComponents.TruncateToWidth(first, firstWidth - 2f, GameFont.Tiny),
                GameFont.Tiny, headColor, TextAnchor.MiddleLeft);
            float cx = x + firstWidth;
            UIComponents.Label(new Rect(cx, y, numWidth, height), second,
                GameFont.Tiny, color, TextAnchor.MiddleRight);
            cx += numWidth;
            UIComponents.Label(new Rect(cx, y, numWidth, height), third,
                GameFont.Tiny, color, TextAnchor.MiddleRight);
            cx += numWidth;
            UIComponents.Label(new Rect(cx, y, lastWidth, height), fourth,
                GameFont.Tiny, color, TextAnchor.MiddleRight);
        }

        /// <summary>小环形的暂存与配色（理念分布 / 专业方向共用；IMGUI 单线程，静态复用避免每帧分配）。</summary>
        private readonly List<float> miniRingValues = new List<float>();

        private static readonly Color[] MiniRingColors =
        {
            UITheme.Accent, UITheme.Info, UITheme.Alive, UITheme.Muted, UITheme.Threat, UITheme.Dim
        };

        /// <summary>
        /// 小环形 + 右侧图例（前 3 行）：段宽 = 行占比 —— <c>Share01</c> 在 Provider 里已按
        /// **总数**归一（真实占比），所以环形是精确的（不足 100% 的部分合并进最后一段，
        /// 保证环闭合；超出色板段数的份额也并入最后一段，不静默丢失）。
        /// </summary>
        private float DrawMiniRing(Rect rect, IReadOnlyList<PopulationCountRow> rows,
            bool withLegend = true)
        {
            if (rows == null || rows.Count == 0 || rect.height < 24f)
            {
                return rect.y;
            }
            int segments = Mathf.Min(rows.Count, MiniRingColors.Length);
            float rest = 0f;
            for (int i = segments; i < rows.Count; i++)
            {
                rest += Mathf.Clamp01(rows[i].Share01);
            }
            miniRingValues.Clear();
            for (int i = 0; i < segments; i++)
            {
                float share = Mathf.Clamp01(rows[i].Share01);
                miniRingValues.Add(i == segments - 1 ? Mathf.Clamp01(share + rest) : share);
            }
            float side = Mathf.Min(MiniRingSize,
                Mathf.Min(withLegend ? rect.width : rect.width, Mathf.Max(24f, rect.height - 2f)));
            Rect ring = new Rect(rect.x, rect.y + 2f, side, side);
            // 【RUIF 硬依赖 · 要求二】小环（理念分布 / 专业方向）走 RUIF `DonutChart`，禁自研。
            // List→复用数组（零每帧分配，PERF-001）。
            if (miniRingBuffer.Length != miniRingValues.Count)
            {
                miniRingBuffer = new float[miniRingValues.Count];
            }
            for (int i = 0; i < miniRingValues.Count; i++)
            {
                miniRingBuffer[i] = miniRingValues[i];
            }
            RuifBridge.TryDonutChart(ring, miniRingBuffer, HoleFromThickness(0.36f), null,
                MiniRingColors);
            if (!withLegend)
            {
                // 仅环模式（预览 ⑦ 左列 106px：只放环形，占比读在右列逐行文本里）。
                return rect.y + Mathf.Min(rect.height, side + 4f);
            }
            float legendX = ring.xMax + 10f;
            float legendWidth = Mathf.Max(0f, rect.xMax - legendX);
            float lineY = rect.y + 2f;
            for (int i = 0; i < 3 && i < segments; i++)
            {
                RUIF.Draw.Box(new Rect(legendX, lineY + 5f, 7f, 7f), MiniRingColors[i]);
                float textWidth = Mathf.Max(0f, legendWidth - 12f);
                string text = string.IsNullOrEmpty(rows[i].ShareText)
                    ? rows[i].Label
                    : rows[i].Label + "  " + rows[i].ShareText;
                UIComponents.Label(new Rect(legendX + 12f, lineY, textWidth, PopulationRowHeight),
                    UIComponents.TruncateToWidth(text, textWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                lineY += 16f;
            }
            return rect.y + MiniRingBlockHeight;
        }

        /// <summary>
        /// chips 行（预览 `.chips &gt; .chip`：横排小圆角标签）。
        /// **单行模式**：放不下即停 —— 高度恒为 <see cref="PopulationChipHeight"/>，
        /// 面板高不随窗口宽漂移（`PanelHeights` 不接收宽度，必须有确定高度）。
        /// </summary>
        private static float DrawChipRow(Rect rect, float y, IReadOnlyList<string> texts, Color color)
        {
            if (texts == null || texts.Count == 0 || rect.width < 40f)
            {
                return y + PopulationChipHeight;
            }
            float x = rect.x;
            for (int i = 0; i < texts.Count; i++)
            {
                string text = texts[i];
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }
                float width = Mathf.Min(Text.CalcSize(text).x + 12f, rect.width);
                if (x > rect.x && x + width > rect.xMax)
                {
                    break;
                }
                Rect chip = new Rect(x, y, width, PopulationChipHeight);
                UIComponents.Panel(chip, UITheme.Card);
                UIComponents.Border(chip, UITheme.WithAlpha(color, 0.55f));
                UIComponents.Label(chip,
                    UIComponents.TruncateToWidth(text, Mathf.Max(0f, width - 8f), GameFont.Tiny),
                    GameFont.Tiny, color, TextAnchor.MiddleCenter);
                x += width + PopulationChipGap;
            }
            return y + PopulationChipHeight;
        }

        /// <summary>
        /// 空柱骨架（无工时数据时的**结构占位**）：12 个等宽空槽 + 基线。
        /// 不画任何假高度 —— 只标示"这里是柱状图位"，与预览 12 工种柱的形状同构。
        /// </summary>
        private static void DrawLaborBarSkeleton(Rect rect)
        {
            if (rect.width < 40f || rect.height < 16f)
            {
                return;
            }
            const int slots = 12;
            float column = rect.width / slots;
            float barWidth = Mathf.Max(3f, column * 0.62f);
            RUIF.Draw.Box(new Rect(rect.x, rect.yMax - 1f, rect.width, UITheme.RuleHeight),
                UITheme.BorderSoft);
            for (int i = 0; i < slots; i++)
            {
                RUIF.Draw.Box(
                    new Rect(rect.x + column * i + (column - barWidth) * 0.5f, rect.yMax - 6f, barWidth, 5f),
                    UITheme.BorderSoft);
            }
        }

        /// <summary>信仰环形数据暂存（理念占比，真实占比归一）。</summary>
        private readonly List<float> ideologyRingValues = new List<float>();

        /// <summary>信仰环逐段配色缓冲（理念段走 MiniRingColors，末尾「无信仰」段走 muted）；
        /// 长度 ≥ 段数即可（RUIF 只按段索引取用，多余项被忽略），扩容 **不缩容** 以免反复分配。</summary>
        private Color[] ideologyRingColorBuffer = new Color[8];

        /// <summary>勋章 chips 文案缓存（绘制期只 clear+add 三条，与既有 `Num()` 拼串同规）。</summary>
        private readonly List<string> medalChipTexts = new List<string>(3);

        /// <summary>专业方向 chips 文案缓存（上限 8 条，与绘制同源）。</summary>
        private readonly List<string> directionChipTexts = new List<string>(8);

        /// <summary>勋章三段配色（每帧从主题令牌刷新，**不缓存** —— 主题切换即时生效，零分配）。</summary>
        private readonly Color[] medalRingColors = new Color[3];

        private readonly float[] medalRingValues = new float[3];

        /// <summary>
        /// ③ 勋章构成**环形**（金 / 银 / 铜；预览荣誉面板的 donut）：段宽 = 各档真实枚数，
        /// 环心写已授予总数，右侧三行图例（标签 + 枚数）。
        /// </summary>
        private float DrawMedalRing(Rect rect, PopulationOverviewSnapshot snapshot)
        {
            medalRingValues[0] = snapshot.MedalGold;
            medalRingValues[1] = snapshot.MedalSilver;
            medalRingValues[2] = snapshot.MedalBronze;
            medalRingColors[0] = UITheme.MedalTierColor(MedalTier.Gold);
            medalRingColors[1] = UITheme.MedalTierColor(MedalTier.Silver);
            medalRingColors[2] = UITheme.MedalTierColor(MedalTier.Bronze);
            float side = Mathf.Min(MiniRingSize, Mathf.Max(24f, rect.height - 2f));
            Rect ring = new Rect(rect.x, rect.y + 2f, side, side);
            // 【RUIF 硬依赖 · 要求二】勋章环走 RUIF；环心数字改由 `centerLabel` 交给 RUIF 绘制
            // （原先自绘的环心 Label 已删除，避免与 RUIF 的 centerLabel 重叠成双份数字）。
            RuifBridge.TryDonutChart(ring, medalRingValues, HoleFromThickness(0.36f),
                Num(snapshot.MedalAwarded), medalRingColors);
            float legendX = ring.xMax + 10f;
            float legendWidth = Mathf.Max(0f, rect.xMax - legendX);
            float lineY = rect.y + 2f;
            string[] labels =
            {
                PopTextOf(PopText.RowMedalGold), PopTextOf(PopText.RowMedalSilver),
                PopTextOf(PopText.RowMedalBronze)
            };
            int[] counts = { snapshot.MedalGold, snapshot.MedalSilver, snapshot.MedalBronze };
            for (int i = 0; i < 3; i++)
            {
                RUIF.Draw.Box(new Rect(legendX, lineY + 5f, 7f, 7f), medalRingColors[i]);
                float textWidth = Mathf.Max(0f, legendWidth - 12f);
                string text = labels[i] + "  " + Num(counts[i]);
                UIComponents.Label(new Rect(legendX + 12f, lineY, textWidth, PopulationRowHeight),
                    UIComponents.TruncateToWidth(text, textWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                lineY += 16f;
            }
            return rect.y + MedalRingBlockHeight;
        }

        /// <summary>
        /// ① 列 3 **综合能力趋势卡**（预览 HTML 794-821）：标题 + 大读数 → 折线区 104px → 口径行。
        ///
        /// <para><b>折线区为何是空态</b>（LAND-009）：综合能力均值是**活读值**
        /// （<c>PawnCapacityDef.GetLevel</c> 的殖民地均值），工程**没有历史序列**；预览 HTML 791-793
        /// 已把该缺口登记为「按季落快照 **或** 如实空态（二者择一）」。本实现取**如实空态** ——
        /// 大读数与口径行照常绘制（都是当前可观测值），折线区不画 0 假线、也**不拿当前值反推历史**。
        /// 预览的「▲ 差值 / 量程 88–94% / 月-季-年 段控」同样依赖历史 ⇒ 一并如实不画
        /// （缺口逐条登记在 `人员系统中心_概览三面板参数对照清单.md`）。</para>
        ///
        /// <para><b>旧实现为何删除</b>：原 <c>DrawCapacityChart</c> 画的是「11 项能力按最大值归一」的
        /// **面积图** —— 预览 <c>#v-overview</c> **没有这个图**（面积图只出现在人物档案视图的
        /// 「累计产出价值趋势」）⇒ 属「我方有 / 预览没有」的残留（三张表判定法 ②：删）。</para>
        /// </summary>
        private float DrawCapacityTrendCard(Rect card, PopulationOverviewSnapshot snapshot)
        {
            RUIF.Shapes.InsetPanel(card);
            const float pad = 7f;
            float innerWidth = Mathf.Max(0f, card.width - pad * 2f);
            float y = card.y + 3f;

            // ① 标题 + 大读数（预览 795-799：标题 9.5px / 值 17px mono alive）。
            //    ⚠ 值取快照**已格式化**文本；无样本画「—」，不画 0 假值。
            string value = string.IsNullOrEmpty(snapshot.CapacityAverageText)
                ? Dash()
                : snapshot.CapacityAverageText;
            float valueWidth = Mathf.Min(innerWidth * 0.34f, 74f);
            float titleWidth = Mathf.Max(0f, innerWidth - valueWidth - 6f);
            UIComponents.Label(new Rect(card.x + pad, y, titleWidth, PopulationRowHeight),
                UIComponents.TruncateToWidth(PopTextOf(PopText.RowCapacity), titleWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(card.xMax - pad - valueWidth, y, valueWidth, PopulationRowHeight),
                UIComponents.TruncateToWidth(value, valueWidth, GameFont.Small),
                GameFont.Small, UITheme.Alive, TextAnchor.MiddleRight);
            y += PopulationRowHeight;

            // ② 折线区（预览 810-811：`svg` 高 104px）—— 无历史序列 ⇒ 如实空态（不画 0 假线）。
            Rect plot = new Rect(card.x + pad, y, innerWidth, JoinTrendPlotHeight);
            UIComponents.Label(new Rect(plot.x, plot.y + plot.height * 0.4f, plot.width, PopulationRowHeight),
                UIComponents.TruncateToWidth(PopTextOf(PopText.CapacityTrendNone), plot.width, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            y += JoinTrendPlotHeight;

            // ③ 口径行（预览 817-820：`border-top` + 两行说明）—— 实机 `Label` **不折行**
            //    （UIComponents.Label 强制 `WordWrap=false`）⇒ 预览的一整段按语义拆成两条 Keyed
            //    （口径 / 工效），各自单行；**工效从此并入本行**（预览没有独立的工效值行）。
            UIComponents.Rule(new Rect(card.x + pad, y, innerWidth, UITheme.RuleHeight),
                UITheme.WithAlpha(UITheme.BorderSoft, 0.55f));
            UIComponents.Label(new Rect(card.x + pad, y + 2f, innerWidth, PopulationRowHeight),
                UIComponents.TruncateToWidth(PopTextOf(PopText.CapacityScope), innerWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(card.x + pad, y + PopulationRowHeight + 2f, innerWidth,
                    PopulationRowHeight),
                UIComponents.TruncateToWidth(PopTextOf(PopText.CapacityEfficiency), innerWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            // 返回卡片下沿 —— 与 `CardHeight` 同源（计高 `CapacityTrendCardHeight` = 3 + 18 + 104 + 36 + 3）。
            return card.y + CapacityTrendCardHeight;
        }

        /// <summary>
        /// 能力条（预览 ① 内联样式 `grid-template-columns:52px 1fr 30px` + `column-gap:7px`，
        /// HTML 823-839）：名称 52px ｜ 条 1fr ｜ 数值 30px（右对齐）；行高 = <see cref="CapacityRowHeight"/>。
        ///
        /// <para>⚠ 预览 ① 用的是**内联窄版栅格**（52/1fr/30），**不是** CSS `.bars` 的
        /// `118px 1fr 74px 92px`（后者是其它视图的版式）—— 旧实现套错了版式，列宽与预览不同源。</para>
        ///
        /// <para>⚠ 配色 = **三档语义色**（预览 HTML 824-838 逐行给定）：≥95% = alive（达标）、
        /// 85%~95% = text（中性）、&lt;85% = warn（偏低）。旧实现只有「≥99.9% alive / 其余 Accent」两档。</para>
        /// </summary>
        private static float DrawCapacityBar(Rect rect, PopulationCapacityRow capacity)
        {
            const float previewLabel = 52f;
            const float previewValue = 30f;
            const float gap = 7f;
            float scale = Mathf.Min(1f, rect.width / 118f);
            float labelWidth = Mathf.Max(32f, previewLabel * scale);
            // 降级（LAND-004 D3）：单元格过窄时**数值列让位**，保住条形本身的可见性 ——
            // 否则窄档的双列单元格里「标签 52 + 数值 30 + 两侧间隙 14」会把条形压成 0px（条直接消失）。
            // 数值改由悬停提供（本方法已有整格 tooltip）。
            float valueWidth = rect.width < 118f ? 0f : previewValue;
            float barWidth = Mathf.Max(0f, rect.width - labelWidth - valueWidth - gap * 2f);
            string labelText = UIComponents.TruncateToWidth(capacity.Label, labelWidth - 2f, GameFont.Tiny);
            UIComponents.Label(new Rect(rect.x, rect.y, labelWidth, rect.height),
                labelText, GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            Rect bar = new Rect(rect.x + labelWidth + gap, rect.y + (rect.height - 6f) * 0.5f, barWidth, 6f);
            UIComponents.ProgressBar(bar, Mathf.Clamp01(capacity.Level01), CapacityBarColor(capacity.Level01));
            if (valueWidth > 0f)
            {
                UIComponents.Label(new Rect(rect.xMax - valueWidth, rect.y, valueWidth, rect.height),
                    capacity.LevelText, GameFont.Tiny, UITheme.Text, TextAnchor.MiddleRight);
            }
            // LAND-008：标签被截断时悬停给完整「能力 · 数值」。
            if (Mouse.IsOver(rect)
                && !string.Equals(labelText, capacity.Label, System.StringComparison.Ordinal))
            {
                TooltipHandler.TipRegion(rect, capacity.Label + " · " + capacity.LevelText);
            }
            return rect.yMax;
        }

        /// <summary>
        /// 能力条**三档语义色**（与预览 HTML 824-838 逐行给定的色阶同源）：
        /// ≥95% = <see cref="UITheme.Alive"/>（达标）· 85%~95% = <see cref="UITheme.Text"/>（中性）
        /// · &lt;85% = <see cref="UITheme.Warn"/>（偏低）。
        /// </summary>
        private static Color CapacityBarColor(float level01)
        {
            if (level01 >= 0.95f)
            {
                return UITheme.Alive;
            }
            return level01 < 0.85f ? UITheme.Warn : UITheme.Text;
        }

        /// <summary>
        /// 核心面板 B **劳动力分区**（并入的辅助面板 ⑤，2026-09-15 合并版）：
        /// 合计 / 人均两行读数 + **工种详细列表**（工种 · 工时 · 占比）。
        /// 用户诉求「最好用详细的列表组件」⇒ 取代原 12 工种竖柱图
        /// （柱图实现 <see cref="DrawLaborBarChart"/> 保留未调用，需要时可接回）。
        ///
        /// <para><b>[接回契约]</b>（v7 起停止调用，方法体与数据采集**全部保留**）：
        /// ① **移除原因**：预览 v7 的 `.coreB` = **一张 8 列表**，右栏「劳动力」已不在其中。
        /// ② **接回步骤**：把本方法的 rows（<c>coreLaborRows</c>）**重新拼进绩效大表**当普通数据行
        /// （预览约定「分组行 = 普通数据行」，HTML 1027），并把 <c>PanelHeights[1]</c> 的行数由 5 加到
        /// 30（4 个分组行 + 26 个数据行），同时把 <c>CoreLaborTableShares</c> / <c>CoreLaborHeaders()</c>
        /// 纳入大表的列宽与表头拼接。若要恢复「右栏上下两分区」旧形态，则把
        /// <c>PopulationCoreColumnGap</c> / <c>PopulationCoreLeftRatio</c> / <c>PopulationCoreSplitGap</c>
        /// 重新投入 <c>DrawOverviewPanel case 1</c> 的分栏布局。
        /// ③ **依赖常量**：<c>CoreLaborTableShares</c> / <c>CoreLaborHeaders()</c> / <c>coreLaborRows</c> /
        /// <c>CoreLaborBlockHeight</c> / <c>LaborChartBlockHeight</c> / 恢复分栏时的三个
        /// <c>PopulationCore*</c> 常量。</para>
        /// </summary>
        private void DrawLaborPanel(Rect row, PopulationOverviewSnapshot snapshot)
        {
            float y = row.y;
            if (snapshot.Labor.Count == 0)
            {
                // **结构占位**：读数画「—」+ 如实说明（与有数据版式同高，见 PanelHeights[1]）。
                y = DrawValueRow(At(row, y), PopTextOf(PopText.RowLaborTotal), Dash(), -1f, UITheme.Dim);
                DrawValueRow(At(row, y), PopTextOf(PopText.NoLaborData), string.Empty, -1f, UITheme.Muted);
                return;
            }
            y = DrawValueRow(At(row, y), PopTextOf(PopText.RowLaborTotal),
                snapshot.LaborTotalText, -1f, UITheme.Accent);
            y = DrawValueRow(At(row, y), PopTextOf(PopText.RowLaborPerCapita),
                string.IsNullOrEmpty(snapshot.LaborPerCapitaText) ? Dash() : snapshot.LaborPerCapitaText,
                -1f, UITheme.Muted);
            float height = (1 + coreLaborRows.Length) * PopulationTableRowHeight;
            DrawCoreTable(new Rect(row.x, y, row.width, height),
                CoreLaborHeaders(), coreLaborRows, CoreLaborTableShares);
        }

        private static void DrawLaborBarChart(Rect rect, PopulationOverviewSnapshot snapshot)
        {
            int count = snapshot.Labor.Count;
            if (count <= 0 || rect.width < 40f || rect.height < 30f)
            {
                return;
            }
            // 柱顶短数值 + 柱底工种名**只在放得下时画**（LAND-004 降级）：柱宽不足则退回纯柱 + 悬停，
            // 不做硬挤（Tiny 字号下数值 4 字符 ≈ 26px、中文 2 字 ≈ 24px）。
            float columnWidth = rect.width / count;
            bool showValue = columnWidth >= 30f;
            bool showLabel = columnWidth >= 24f;
            // ⚠ 柱顶/柱底都是文本 ⇒ 行高必须 = Tiny 行高（18）；给 12 会让文字溢出被柱体/基线盖住。
            float valueRow = showValue ? PopulationRowHeight : 0f;
            float labelRow = showLabel ? PopulationRowHeight : 0f;
            float chartHeight = Mathf.Max(8f, rect.height - 10f - valueRow - labelRow);
            float baseY = rect.y + valueRow + chartHeight;
            float barWidth = Mathf.Max(3f, columnWidth * 0.62f);
            float maxShare = snapshot.Labor[0].Share01;   // 已按 ticks 降序 ⇒ [0] 即最大
            for (int i = 0; i < count; i++)
            {
                PopulationLaborRow labor = snapshot.Labor[i];
                float ratio = maxShare > 0f ? Mathf.Clamp01(labor.Share01 / maxShare) : 0f;
                float height = Mathf.Max(2f, chartHeight * ratio);
                float columnX = rect.x + columnWidth * i;
                Rect bar = new Rect(columnX + (columnWidth - barWidth) * 0.5f, baseY - height, barWidth, height);
                RUIF.Draw.Box(bar, LaborBarColor(i));
                if (showValue)
                {
                    UIComponents.Label(new Rect(columnX, rect.y, columnWidth, valueRow),
                        UIComponents.TruncateToWidth(labor.ShortText, columnWidth, GameFont.Tiny),
                        GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleCenter);
                }
                if (showLabel)
                {
                    UIComponents.Label(new Rect(columnX, baseY + 1f, columnWidth, labelRow),
                        UIComponents.TruncateToWidth(labor.Label, columnWidth, GameFont.Tiny),
                        GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleCenter);
                }
                // 悬停提示**只在悬停时拼串**（常态零分配）：工种名 + 工时。
                Rect column = new Rect(columnX, rect.y, columnWidth, rect.height);
                if (Mouse.IsOver(column))
                {
                    TooltipHandler.TipRegion(column, labor.Label + " · " + labor.HoursText);
                }
            }
            UIComponents.Rule(new Rect(rect.x, baseY, rect.width, UITheme.RuleHeight), UITheme.BorderSoft);
        }

        /// <summary>柱色三色阶（预览 `.barv`：前 4 accent / 中 4 info / 后 4 muted）。</summary>
        private static Color LaborBarColor(int index)
        {
            if (index < 4)
            {
                return UITheme.Accent;
            }
            return index < 8 ? UITheme.Info : UITheme.Muted;
        }

        /// <summary>
        /// 核心面板 B **荣誉分区**（并入的辅助面板 ③，2026-09-15 合并版）：
        /// 已授予读数 + 金银铜 chips + **两张详细列表**（勋章榜 / 职称分布）。
        /// 用户诉求「最好用详细的列表组件」⇒ 取代原「3 行逐条读数 + 逐行职称」。
        ///
        /// <para><b>[接回契约]</b>（v7 起停止调用，方法体与数据采集**全部保留**）：
        /// ① **移除原因**：预览 v7 的 `.coreB` = **一张 8 列表**，右栏「荣誉 / 职称」已不在其中
        /// （HTML 991-993 明确记录 v5 的移除仍然有效）。
        /// ② **接回步骤**：把本方法的 rows（<c>coreMedalRows</c> / <c>coreTitleRows</c>）
        /// **重新拼进绩效大表**当普通数据行（预览约定「分组行 = 普通数据行」，HTML 1027），并把
        /// <c>PanelHeights[1]</c> 的行数由 5 加到 30；同时把 <c>CoreHonorTableShares</c> /
        /// <c>CoreMedalHeaders()</c> / <c>CoreTitleHeaders()</c> 纳入大表的列宽与表头拼接。
        /// ③ **依赖常量**：<c>CoreHonorTableShares</c> / <c>CoreMedalHeaders()</c> / <c>CoreTitleHeaders()</c> /
        /// <c>coreMedalRows</c> / <c>coreTitleRows</c> / <c>CoreHonorBlockHeight</c> /
        /// <c>PopulationChipHeight</c> / <c>PopulationChipGap</c>。</para>
        /// </summary>
        private void DrawHonorPanel(Rect row, PopulationOverviewSnapshot snapshot)
        {
            float y = row.y;
            y = DrawValueRow(At(row, y), PopTextOf(PopText.RowMedalAwarded),
                Num(snapshot.MedalAwarded) + " / " + Num(snapshot.MedalDefTotal), -1f, UITheme.Accent);
            // 金 / 银 / 铜档位（预览 ③ chips；一行、超宽即停）。⚠ 环形早已按预览删除（与 chips 同数据）。
            medalChipTexts.Clear();
            medalChipTexts.Add(PopTextOf(PopText.RowMedalGold) + " " + Num(snapshot.MedalGold));
            medalChipTexts.Add(PopTextOf(PopText.RowMedalSilver) + " " + Num(snapshot.MedalSilver));
            medalChipTexts.Add(PopTextOf(PopText.RowMedalBronze) + " " + Num(snapshot.MedalBronze));
            y = DrawChipRow(new Rect(row.x, y, row.width, PopulationChipHeight), y,
                medalChipTexts, UITheme.MedalTierColor(MedalTier.Gold));
            // ① 勋章榜详细列表（勋章 · 枚数 · 占比；表头即标题）
            if (coreMedalRows.Length > 0)
            {
                float height = (1 + coreMedalRows.Length) * PopulationTableRowHeight;
                DrawCoreTable(new Rect(row.x, y, row.width, height),
                    CoreMedalHeaders(), coreMedalRows, CoreHonorTableShares);
                y += height + 4f;
            }
            // ② 职称分布详细列表（职称 · 人数 · 占比）
            if (coreTitleRows.Length > 0)
            {
                float height = (1 + coreTitleRows.Length) * PopulationTableRowHeight;
                DrawCoreTable(new Rect(row.x, y, row.width, height),
                    CoreTitleHeaders(), coreTitleRows, CoreHonorTableShares);
            }
        }

        private void DrawSpecializationPanel(Rect row, PopulationOverviewSnapshot snapshot)
        {
            float y = row.y;
            // ⚠ 预览 ⑧（HTML 979-1004）**没有环形**：sec-h + chips + 梯队 bars + 缺口 drow + 说明行。
            // 旧实现的「方向占比环形」（60px）与下方 chips 表达同一组数据 ⇒ 已删除。
            y = DrawValueRow(At(row, y), PopTextOf(PopText.RowDirectionSet),
                Num(snapshot.SpecializationSet) + " / " + Num(snapshot.SpecializationTotal), -1f, UITheme.Accent);
            // 方向：预览 ⑧ 是**一行 chips**（`手工·锻造 3 / 建造 2 / …`），不是逐行读数。
            directionChipTexts.Clear();
            for (int i = 0; i < snapshot.Directions.Count && directionChipTexts.Count < 8; i++)
            {
                directionChipTexts.Add(snapshot.Directions[i].Label + " "
                    + Num(snapshot.Directions[i].Count));
            }
            // 预览 ⑧ chips 末项「未设定 3」（与「已设定」互补，构建期算好 ⇒ 绘制期不算数）。
            if (!string.IsNullOrEmpty(snapshot.SpecializationUnsetText))
            {
                directionChipTexts.Add(PopTextOf(PopText.SpecializationUnset) + " "
                    + snapshot.SpecializationUnsetText);
            }
            y = DrawChipRow(new Rect(row.x, y, row.width, PopulationChipHeight), y,
                directionChipTexts, UITheme.Accent);
            // 技能梯队 4 档（预览 ⑧ 的梯队条：专家 ≥15 / 熟练 10–14 / 入门 5–9 / 学徒 <5）
            for (int i = 0; i < snapshot.SkillTiers.Count; i++)
            {
                y = DrawValueRow(At(row, y), snapshot.SkillTiers[i].Label,
                    snapshot.SkillTiers[i].ValueText,
                    snapshot.SkillTiers[i].Share01, UITheme.Info);
            }
            // 预览 ⑧ 底部：方向缺口 drow（「采矿 / 烹饪 无人主攻」）+ 口径说明行。
            if (!string.IsNullOrEmpty(snapshot.SpecializationGapText))
            {
                y = DrawValueRow(At(row, y), PopTextOf(PopText.SpecializationGap),
                    snapshot.SpecializationGapText, -1f, UITheme.Warn);
            }
            UIComponents.Label(new Rect(row.x, y, row.width, PopulationRowHeight),
                UIComponents.TruncateToWidth(PopTextOf(PopText.SpecializationNote), row.width, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
        }

        /// <summary>
        /// ④ 派生属性面板：**真实可算**的殖民地级派生指标（人均产出 / 单点依赖 / 装备达标 /
        /// 情绪风险 / 伤病负担 / 有床率 / 温饱达标 / 技能覆盖）。无活体时不画 0 假值，如实说明。
        /// </summary>
        private void DrawDerivedPanel(Rect row, PopulationOverviewSnapshot snapshot)
        {
            if (!snapshot.DerivedReady || snapshot.Derived.Count == 0)
            {
                UIComponents.Label(At(row, row.y), PopTextOf(PopText.DerivedNoSource),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                return;
            }
            // 【预览 HTML 850】`grid-template-columns:repeat(2,1fr); gap:7px` —— 四类派生指标
            // 各占一张卡片（**2×2 卡片网格**，旧实现是竖排行列表 ⇒ 与预览布局不符）。
            // 组边界 = DerivedGroupAt（与 Provider 输出顺序一一对应）。
            float cardWidth = Mathf.Max(90f, (row.width - DerivedCardGap) * 0.5f);
            for (int g = 0; g < DerivedGroupCount; g++)
            {
                int start = DerivedGroupAt[g];
                int end = g + 1 < DerivedGroupCount ? DerivedGroupAt[g + 1] : snapshot.Derived.Count;
                Rect card = new Rect(row.x + (g % 2) * (cardWidth + DerivedCardGap),
                    row.y + (g / 2) * (DerivedCardHeight + DerivedCardGap), cardWidth, DerivedCardHeight);
                RUIF.Draw.Box(card, UITheme.Card);
                UIComponents.Border(card, UITheme.BorderSoft);
                float y = card.y + 3f;
                UIComponents.Label(new Rect(card.x + 6f, y, Mathf.Max(0f, card.width - 12f), PopulationRowHeight),
                    PopTextOf(DerivedGroupText[g]), GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                y += PopulationRowHeight;
                for (int i = start; i < end && i < snapshot.Derived.Count; i++)
                {
                    PopulationCountRow item = snapshot.Derived[i];
                    // 卡内较窄 ⇒ 不画占比条（预览 `.drow` 卡内同样只有键值对）
                    y = DrawValueRow(
                        new Rect(card.x + 6f, y, Mathf.Max(20f, card.width - 12f), PopulationRowHeight),
                        item.Label, item.ValueText, -1f, UITheme.Accent);
                }
            }
        }

        /// <summary>派生面板分组标题数量（与 <see cref="DerivedGroupAt"/> 等长）。</summary>
        private const int DerivedGroupCount = 4;

        /// <summary>分组标题插入位置（在派生行的该索引**之前**）。</summary>
        private static readonly int[] DerivedGroupAt = { 0, 2, 5, 7 };

        /// <summary>分组标题文案槽（顺序 = 产出 / 保障 / 风险 / 技能）。</summary>
        private static readonly PopText[] DerivedGroupText =
        {
            PopText.DerivedGroupOutput, PopText.DerivedGroupSupport,
            PopText.DerivedGroupRisk, PopText.DerivedGroupSkill
        };

        /// <summary>
        /// ③ 派系与外交（预览 819-837）：一行一个派系 = 名称 + 关系徽章 + 好感度（带色）+ 好感条。
        /// 「本殖民地」行只显示名称（不给自己评好感）。
        /// </summary>
        private void DrawFactionPanel(Rect row, PopulationOverviewSnapshot snapshot)
        {
            float y = row.y;
            if (snapshot.Factions.Count == 0)
            {
                UIComponents.Label(new Rect(row.x, y, row.width, PopulationRowHeight),
                    PopTextOf(PopText.DerivedPending), GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                return;
            }
            for (int i = 0; i < snapshot.Factions.Count; i++)
            {
                PopulationFactionRow faction = snapshot.Factions[i];
                Rect line = new Rect(row.x, y, row.width, PopulationRowHeight);
                bool self = string.IsNullOrEmpty(faction.GoodwillText);
                // 【预览 `.fac-row`（HTML 535-539）】`grid:12px 74px 44px 1fr 46px` + `gap:6px` + 行高 19。
                // 五段：**色点** / 名称 / 科技等级 / 关系条(1fr，高 7px) / 好感值。
                // 关系**文字**（中立/敌对/盟友）预览行内没有 ⇒ 并入悬停提示，避免挤掉名称列。
                const float dotColumn = 12f;
                const float columnGap = 6f;
                float valueWidth = Mathf.Max(34f, line.width * 0.13f);
                float techWidth = self ? 0f : Mathf.Max(38f, line.width * 0.12f);
                float nameWidth = Mathf.Max(54f, line.width * 0.26f);
                float barWidth = Mathf.Max(20f, line.width - dotColumn - nameWidth - techWidth
                    - valueWidth - columnGap * 4f);
                // 色点（预览 `.pdot`：语气色；IMGUI 无圆形 ⇒ 8px 方块，B 类降级）
                RUIF.Draw.Box(
                    new Rect(line.x + 2f, line.y + (line.height - 8f) * 0.5f, 8f, 8f),
                    ToneColorOf(faction.Tone));
                float x = line.x + dotColumn;
                UIComponents.Label(new Rect(x, line.y, nameWidth, line.height),
                    UIComponents.TruncateToWidth(faction.Label, nameWidth - 2f, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
                x += nameWidth;
                if (!string.IsNullOrEmpty(faction.TechText))
                {
                    UIComponents.Label(new Rect(x, line.y, techWidth, line.height),
                        UIComponents.TruncateToWidth(faction.TechText, techWidth - 2f, GameFont.Tiny),
                        GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                }
                x += techWidth;
                if (self)
                {
                    // 本殖民地（预览 `.r-self`）：半条自色，标示「自身」而非好感。
                    RUIF.Draw.Box(
                        new Rect(x + columnGap * 0.5f, line.y + (line.height - 7f) * 0.5f, barWidth * 0.5f, 7f),
                        ToneColorOf(faction.Tone));
                }
                else
                {
                    // 好感度条：0..1 位置对应 -100..100（中点 = 中立），只按快照给的 Goodwill01 画。
                    UIComponents.ProgressBar(
                        new Rect(x + columnGap * 0.5f, line.y + (line.height - 7f) * 0.5f, barWidth, 7f),
                        faction.Goodwill01, ToneColorOf(faction.Tone));
                }
                UIComponents.Label(new Rect(line.xMax - valueWidth, line.y, valueWidth, line.height),
                    faction.GoodwillText, GameFont.Tiny, ToneColorOf(faction.Tone), TextAnchor.MiddleRight);
                if (!self && Mouse.IsOver(line))
                {
                    TooltipHandler.TipRegion(line,
                        faction.Label + " · " + faction.TechText + " · " + faction.RelationText);
                }
                y += PopulationRowHeight;
            }
            // 【预览 ③ 底部 drow（HTML 833-836）】「科技层级 / 文化 CultureDef」两行汇总。
            // 本工程**无 CultureDef 读取面** ⇒ 只汇总可观测的科技层级（文化部分按缺口登记，
            // 不编造）；值在构建期拼好（`TechCount` + `PairDot` 模板），绘制期零拼接。
            if (!string.IsNullOrEmpty(snapshot.FactionTechSummaryText))
            {
                y += 2f;
                DrawValueRow(At(row, y), PopTextOf(PopText.FactionTechSummary),
                    snapshot.FactionTechSummaryText, -1f, UITheme.Muted);
            }
        }

        private static Color ToneColorOf(SystemCenterTone tone)
        {
            switch (tone)
            {
                case SystemCenterTone.Good: return UITheme.Alive;
                case SystemCenterTone.Warn: return UITheme.Threat;
                case SystemCenterTone.Bad: return UITheme.Dead;
                default: return UITheme.Muted;
            }
        }

        /// <summary>
        /// ⑥ 人员绩效（**预览 v7 · `.coreB`：单栏单表**；HTML 1004-1087）：
        /// **一张 8 列 `DataAtoms.StatTable`（5 数据行）→ 表底合计行**，面板内**再无其它内容**。
        ///
        /// <para>⚠ 口径（LAND-009）：8 列**全部可直接观测或由已持久化的 8 季桶算出**
        /// （产出市值 / 工时 / 非零季数 / 段内峰值 / 二者相除得季均），不折算银/时、不编造派生值。</para>
        ///
        /// <para><b>1:1 取证（预览 v7）</b>：`.coreB > .pbody.tight` 只有 2 个子节点
        /// （`.coretbl.tall` + `.drow`，实测 `pbodyChildren:2`）；8 列表头 + 5 数据行 + 表底合计。
        /// 行高 34 = `RUIF.TableFmt.RowHeight(RowDensity.Cozy)`（HTML 195 已证 1:1 映射）。</para>
        ///
        /// <para><b>表外框与底色由本方法补齐</b>（`StatTable` 自身不含）：预览
        /// `.coretbl{border:1px solid var(--hair);background:var(--window)}`（HTML 180）
        /// ⇒ **先铺底色 → 画内容 → 再描 1px 外框**（铁律 3：容器底色必须早于内容）。
        /// 圆角 6px 登记 **B 类**（IMGUI 无圆角 API）。</para>
        ///
        /// <para>⚠ v5 的「累计贡献趋势卡 + 口径说明行 + 左右分栏 + 右栏荣誉/劳动力」四块拼盘
        /// **已从显示移除**（用户判定旧版「完全没有实现基于预览图的 1:1 还原」）。
        /// 四块的**绘制方法与数据采集全部保留**：
        /// [接回预留 · 见 <see cref="DrawPerformanceTrendCard"/> / <see cref="DrawHonorPanel"/> /
        /// <see cref="DrawLaborPanel"/> 的 [接回契约] 段 + <c>PanelHeights[1]</c>]。</para>
        /// </summary>
        private void DrawPerformancePanel(Rect row, PopulationOverviewSnapshot snapshot)
        {
            float y = row.y;
            if (snapshot.Performance.Count == 0)
            {
                // **空态骨架**：与非空态**同构的「表头 + 结构占位行」**——列头 / 列宽 / 行高与
                // DrawCoreTable 完全一致（LAND-002 同源）。占位行 3 行 × 7 列全「—」（未经证实读数按
                // LAND-009 用「—」，不填样例 / 不填 0）。无表底合计行（无数据可合计）；panel 标题与
                // 分隔条已由 DrawOverviewPanel 统一绘制。⚠ 空态版式预览未给 ⇒ 登记 **B 类差异**。
                float emptyTableHeight = (1 + CorePerfEmptyRows.Length) * PerformanceTableRowHeight;  // = StatTable 预留契约
                var emptyTable = new Rect(row.x, y, row.width, emptyTableHeight);
                RUIF.Draw.Box(emptyTable, UITheme.Window);                          // 表底色（先于内容）
                DrawCoreTable(emptyTable, CorePerfHeaders(), CorePerfEmptyRows, CorePerfTableShares,
                    CorePerfColAligns, CorePerfColDims, CorePerfBandCol, CorePerfBandRef,
                    CorePerfEmptyBandValues, CorePerfEmptyBandTexts, PerformanceTableRowHeight);
                RUIF.Draw.Border(emptyTable, UITheme.BorderHair);                   // 1px 外框（后于内容）
                return;
            }

            // ── **一张融合大表**（预览 1029-1081）：8 列 · rowH 34 · 产出市值列阈值着色 ──────
            // 行缓冲按 `BuiltFromRevision` 重建 ⇒ 绘制期零分配（PERF-001）。
            int perfRowCount = snapshot.Performance.Count;
            float tableHeight = (1 + perfRowCount) * PerformanceTableRowHeight;  // = StatTable 预留契约
            var table = new Rect(row.x, y, row.width, tableHeight);
            RUIF.Draw.Box(table, UITheme.Window);                                // 表底色（先于内容）
            DrawCoreTable(table, CorePerfHeaders(), corePerfRows, CorePerfTableShares,
                CorePerfColAligns, CorePerfColDims, CorePerfBandCol, CorePerfBandRef,
                corePerfBandValues, corePerfBandTexts, PerformanceTableRowHeight);
            RUIF.Draw.Border(table, UITheme.BorderHair);                         // 1px 外框（后于内容）
            // 表头**命中层**（悬停高亮 / 点击切排序 / 排序箭标）—— 叠在表内容之上最后画。
            DrawPerformanceHeaderSort(table, PerformanceTableRowHeight);

            // ── 表底合计行（预览 1084 `.drow`；间距 = `.pbody` gap 6 + `.drow` margin-top 6）──
            DrawPerformanceSummaryRow(new Rect(row.x, y + tableHeight + PerformanceSummaryMarginTop,
                row.width, PerformanceSummaryRowHeight), snapshot);
        }

        /// <summary>
        /// 绩效表**表头命中层**（2026-09-17 用户报「无法列头点击排序」）：按
        /// <c>shares ÷ Σshares</c> 自算列位，叠加「悬停高亮 + 点击切排序 + 排序箭标」。
        ///
        /// <para>⚠ <b>「与 RUIF 同一比例式」这句话曾经是错的</b>（2026-09-18 核对 RUIF 源码）：
        /// <c>TableFmt.ColumnSplit</c> 是 <c>widths[i] = shares[i] * rect.width</c>（**直接乘，不除 Σ**）。
        /// 两式只在 Σ = 1 时等价 —— 人口中心三张表恰好都归一化了（见
        /// <c>CorePerfTableShares</c> 的注释），所以一直没露馅；经济市场表传了 Σ≠1 的比例，
        /// 就出现了「首列吃掉整行、其余列全部不可见」。现由
        /// <c>RuifBridge.NormalizeShares</c> 统一收口，本处与 RUIF 侧因此重新等价。</para>
        ///
        /// <para><b>箭标位置 = 对齐方向的**反侧**</b>：数值列右对齐 ⇒ 箭标贴左（否则压住数字）；
        /// 姓名列左对齐 ⇒ 箭标贴右。箭标用几何符号（▼/▲，非文案 ⇒ 不进 Keyed）。</para>
        ///
        /// <para>交互：名次列（索引 0）**不参与**排序；数值列默认**降序**、姓名列默认**升序**；
        /// 同列再点**反向**。状态是窗口瞬时内存（不入存档 · GATE-UI），行序在缓冲重建期生效
        /// （排序状态已纳入缓存键 ⇒ 点击后下一帧即重排）。</para>
        /// </summary>
        private void DrawPerformanceHeaderSort(Rect table, float rowH)
        {
            float sum = 0f;
            for (int i = 0; i < CorePerfTableShares.Length; i++)
            {
                sum += CorePerfTableShares[i];
            }
            if (sum <= 0f || table.width < 24f)
            {
                return;
            }
            float x = table.x;
            for (int i = 0; i < CorePerfTableShares.Length; i++)
            {
                float width = table.width * CorePerfTableShares[i] / sum;
                Rect cell = new Rect(x, table.y, width, rowH);
                x += width;
                // 7 列制下**每一列都可排序**（原「№」列已删 ⇒ 不再有不可排序的首列）。
                bool active = performanceSortColumn == i;
                if (active)
                {
                    bool rightAligned = CorePerfColAligns[i] == 1;
                    Rect arrow = rightAligned
                        ? new Rect(cell.x + 2f, cell.y, 14f, rowH)
                        : new Rect(cell.xMax - 16f, cell.y, 14f, rowH);
                    UIComponents.Label(arrow, performanceSortDescending ? "▼" : "▲",
                        GameFont.Tiny, UITheme.Accent,
                        rightAligned ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
                }
                else if (Mouse.IsOver(cell))
                {
                    Widgets.DrawHighlight(cell);
                }
                TooltipHandler.TipRegion(cell,
                    // ⚠ 2026-09-19（CHG-20260919-ECON-UI-010）：表头排序提示原先**每列每帧**
                    // 翻译 + 分配（审计登记点）；改走本类助手的语言级缓存。
                    Translate("PersonalChronicle.UI.MasterControl.Population.Table.SortTip"));
                if (Widgets.ButtonInvisible(cell))
                {
                    if (active)
                    {
                        performanceSortDescending = !performanceSortDescending;
                    }
                    else
                    {
                        performanceSortColumn = i;
                        performanceSortDescending = i != 0;   // 数值列默认降序、姓名列（索引 0）默认升序
                    }
                }
            }
        }

        // ══════════════ 核心面板 B：详细列表（RUIF `DataAtoms.StatTable`）══════════════
        //
        // 2026-09-15（用户要求「人员绩效合并两个辅助面板，最好用详细的列表组件」）：
        // 绩效 / 荣誉 / 劳动力三块共用**统计表**（表头 + 斑马纹行 + 比例列宽）。

        /// <summary>空表占位（长度为 0 的二维数组，供缓冲区初始值用）。</summary>
        private static readonly string[][] CoreEmptyRows = new string[0][];

        /// <summary>空态占位行缓冲：3 行 × 7 列全「—」（未经证实读数按 LAND-009 用「—」，不填样例 / 不填 0）。</summary>
        private static readonly string[][] CorePerfEmptyRows = BuildPerfEmptyRows();

        /// <summary>空态 band 缓冲（长度与 <see cref="CorePerfEmptyRows"/> 一致）：值全 0 ⇒ 无带色；列文本取 rows[bandCol]="—"。</summary>
        private static readonly float[] CorePerfEmptyBandValues = new float[3];
        private static readonly string[] CorePerfEmptyBandTexts = new string[3];

        /// <summary>构造空态占位行（3 × 7，全「—」）。</summary>
        private static string[][] BuildPerfEmptyRows()
        {
            string[][] rows = new string[3][];
            for (int r = 0; r < 3; r++)
            {
                rows[r] = new string[7];
                for (int c = 0; c < 7; c++)
                {
                    rows[r][c] = Dash();
                }
            }
            return rows;
        }

        /// <summary>回落自绘时的列宽缓冲（静态复用；长度 ≥ 表头列数上限 5）。</summary>
        private static readonly float[] CoreTableWidths = new float[8];

        /// <summary>绩效表行缓冲（`[行][8]`：名次 · 姓名 · 产出市值 · 占比 · 工时 · 平均季产 · 峰值季 · 参与季数）。</summary>
        private string[][] corePerfRows = CoreEmptyRows;

        /// <summary>空浮点缓冲（供绩效 band 缓冲的初始值用）。</summary>
        private static readonly float[] CoreEmptyFloats = new float[0];

        /// <summary>空字符串缓冲（同上级）。</summary>
        private static readonly string[] CoreEmptyStrings = new string[0];

        /// <summary>
        /// 绩效表**阈值带色列**的比值缓冲（每行一个 0..1；来源 = <c>PopulationRankRow.Share01</c>，
        /// 即「值 ÷ 榜首」——与 <see cref="CorePerfBandRef"/>=1 配合使 <c>ValueRatio</c> 恒等）。
        /// 按 `Performance.Count` 随数据版本重建（**实例级**，绘制期零分配 / 零 new）。
        /// </summary>
        private float[] corePerfBandValues = CoreEmptyFloats;

        /// <summary>绩效表**带色列的显示文本**缓冲（= 已格式化的千分位，如 "6,980"）。</summary>
        private string[] corePerfBandTexts = CoreEmptyStrings;

        /// <summary>勋章榜行缓冲（`[行][3]`：勋章 · 枚数 · 占比）。</summary>
        private string[][] coreMedalRows = CoreEmptyRows;

        /// <summary>职称分布行缓冲（`[行][3]`：职称 · 人数 · 占比）。</summary>
        private string[][] coreTitleRows = CoreEmptyRows;

        /// <summary>工种工时行缓冲（`[行][3]`：工种 · 工时 · 占比）。</summary>
        private string[][] coreLaborRows = CoreEmptyRows;

        /// <summary>行缓冲对应的快照数据版本（相同则跳过重建）。</summary>
        private long coreTableRevision = -1L;

        /// <summary>
        /// 行缓冲是否已建过（**不能**只看 `corePerfRows.Length`：绩效为空时长度恒为 0，
        /// 会导致每帧重建全部表缓冲 ⇒ PERF-001 违规）。
        /// </summary>
        private bool coreTablesBuilt;

        /// <summary>绩效表头缓冲 —— **7 列**（2026-09-17 删表内序号列「№」，见 <c>CorePerfTableShares</c>）。</summary>
        private readonly string[] corePerfHeader = new string[7];
        private readonly string[] coreMedalHeader = new string[3];
        private readonly string[] coreTitleHeader = new string[3];
        private readonly string[] coreLaborHeader = new string[3];

        /// <summary>
        /// 绩效表表头 —— **7 列**（2026-09-17 用户要求「不要序号」⇒ 删去原第 0 列「№」）：
        /// `姓名 · 产出市值 · 占比 · 工时 · 平均季产 · 峰值季 · 参与季数`。
        /// <para>⚠ 与 <c>CorePerfTableShares</c> / <c>CorePerfColAligns</c> / <c>CorePerfColDims</c> /
        /// <c>CorePerfBandCol</c> / <c>corePerfRows</c> / <c>PerformanceSortValue</c> 的列索引**必须逐处同步**
        /// （任一脱节 = 列宽与内容错位）。</para>
        /// </summary>
        private string[] CorePerfHeaders()
        {
            corePerfHeader[0] = PopTextOf(PopText.CoreTableName);
            corePerfHeader[1] = PopTextOf(PopText.CoreTableOutput);
            corePerfHeader[2] = PopTextOf(PopText.CoreTableShare);
            corePerfHeader[3] = PopTextOf(PopText.CoreTableHours);
            corePerfHeader[4] = PopTextOf(PopText.CoreTableAvgQuadrum);
            corePerfHeader[5] = PopTextOf(PopText.CoreTablePeakQuadrum);
            corePerfHeader[6] = PopTextOf(PopText.CoreTableQuadrumCount);
            return corePerfHeader;
        }

        private string[] CoreMedalHeaders()
        {
            coreMedalHeader[0] = PopTextOf(PopText.CoreTableMedal);
            coreMedalHeader[1] = PopTextOf(PopText.CoreTableCount);
            coreMedalHeader[2] = PopTextOf(PopText.CoreTableShare);
            return coreMedalHeader;
        }

        private string[] CoreTitleHeaders()
        {
            coreTitleHeader[0] = PopTextOf(PopText.CoreTableTitle);
            coreTitleHeader[1] = PopTextOf(PopText.CoreTablePeople);
            coreTitleHeader[2] = PopTextOf(PopText.CoreTableShare);
            return coreTitleHeader;
        }

        private string[] CoreLaborHeaders()
        {
            coreLaborHeader[0] = PopTextOf(PopText.CoreTableJob);
            coreLaborHeader[1] = PopTextOf(PopText.CoreTableHours);
            coreLaborHeader[2] = PopTextOf(PopText.CoreTableShare);
            return coreLaborHeader;
        }

        // ══════════════ ⑥ 绩效表**表头点击排序**（2026-09-17 · 用户报「无法列头点击排序」）══════════════
        //
        // `DataAtoms.StatTable` **不读 Mouse / Event**（行为契约，已反编译核对）⇒ 排序交互必须由调用方实现：
        //   ① 排序状态（列 / 方向）是**窗口瞬时内存**（不入存档 · GATE-UI）；
        //   ② 行序在行缓冲**重建期**一次算好（`EnsurePerformanceOrder`），绘制期只按序取行（PERF-001）；
        //   ③ 表头命中矩形按 `TableFmt.ColumnSplit` 的**同一比例式**（shares ÷ Σshares）自算；
        //   ④ 排序键一律读**快照数值字段**（`Value` / `Hours` / … ）—— **绝不解析已格式化文本**。

        /// <summary>
        /// 排序键列（**7 列制** · 2026-09-17 删「№」列后索引整体 −1）：
        /// -1 = 快照原序（产出市值降序，Provider 已排好）· 0 = 姓名 · 1..6 = 数值列。
        /// </summary>
        private int performanceSortColumn = -1;

        /// <summary>是否降序（数值列默认降序 = 大到上；姓名列默认升序）。</summary>
        private bool performanceSortDescending = true;

        /// <summary>行缓冲上次构建时的排序状态 —— 缓存键的一部分（不纳入 ⇒ 点了表头行序不更新）。</summary>
        private int coreBuiltSortColumn = -1;
        private bool coreBuiltSortDescending = true;

        /// <summary>行序缓冲（长度 = 绩效行数；默认即 0..n-1 原序）。</summary>
        private int[] corePerfOrder = new int[0];

        /// <summary>
        /// 按当前排序状态重排行序缓冲 —— **插入排序**（n ≤ 30 ⇒ 无分配、无委托、无 LINQ），
        /// 严格比较 ⇒ 相等项保持原序（**稳定排序**，同值时仍按产出市值降序）。
        /// </summary>
        private void EnsurePerformanceOrder(PopulationOverviewSnapshot snapshot)
        {
            List<PopulationRankRow> rows = snapshot.Performance;
            int n = rows.Count;
            if (corePerfOrder.Length != n)
            {
                corePerfOrder = n > 0 ? new int[n] : new int[0];
            }
            for (int i = 0; i < n; i++)
            {
                corePerfOrder[i] = i;
            }
            if (performanceSortColumn < 1 || n < 2)
            {
                return;   // 原序（Provider 已按产出市值降序）
            }
            bool byName = performanceSortColumn == 0;   // 0 = 姓名列（7 列制）
            for (int i = 1; i < n; i++)
            {
                int key = corePerfOrder[i];
                int j = i - 1;
                while (j >= 0 && ComesBefore(rows, key, corePerfOrder[j], byName))
                {
                    corePerfOrder[j + 1] = corePerfOrder[j];
                    j--;
                }
                corePerfOrder[j + 1] = key;
            }
        }

        /// <summary>`a` 是否应排在 `b` **之前**（严格比较 ⇒ 相等返回 false，保持稳定）。</summary>
        private bool ComesBefore(List<PopulationRankRow> rows, int a, int b, bool byName)
        {
            if (byName)
            {
                int cmp = string.CompareOrdinal(rows[a].Label, rows[b].Label);
                return performanceSortDescending ? cmp > 0 : cmp < 0;
            }
            float va = PerformanceSortValue(rows[a], performanceSortColumn);
            float vb = PerformanceSortValue(rows[b], performanceSortColumn);
            return performanceSortDescending ? va > vb : va < vb;
        }

        /// <summary>取某行在指定列的排序数值（**读快照数值字段**；缺列返回 0）。</summary>
        private static float PerformanceSortValue(PopulationRankRow row, int column)
        {
            switch (column)
            {
                case 1: return row.Value;
                case 2: return row.Share01;     // 占比列（与产出市值同序，但按本列语义参与排序）
                case 3: return row.Hours;
                case 4: return row.AvgQuadrum;
                case 5: return row.PeakQuadrum;
                case 6: return row.QuadrumCount;
                default: return 0f;
            }
        }

        /// <summary>
        /// 详细列表行缓冲的**重建**（仅数据版本变化时执行）—— 每行一个复用的 <c>string[cols]</c>，
        /// 内容全部**引用**快照已格式化字段（除名次/枚数等小整数外不产生新字符串）。
        /// 由 `DrawOverviewPanel` 在绘制核心面板 B 前调用一次（帧内幂等）。
        /// </summary>
        private void EnsureCoreTableRows(PopulationOverviewSnapshot snapshot)
        {
            // ⚠ 缓存键**必须包含排序状态**（2026-09-17）：否则点了表头行序不更新（点了没反应）。
            if (coreTablesBuilt && coreTableRevision == snapshot.BuiltFromRevision
                && coreBuiltSortColumn == performanceSortColumn
                && coreBuiltSortDescending == performanceSortDescending)
            {
                return;
            }
            coreTablesBuilt = true;
            coreTableRevision = snapshot.BuiltFromRevision;
            coreBuiltSortColumn = performanceSortColumn;
            coreBuiltSortDescending = performanceSortDescending;

            corePerfRows = AllocCoreRows(snapshot.Performance.Count, 7);
            if (corePerfBandValues.Length != corePerfRows.Length)
            {
                corePerfBandValues = corePerfRows.Length > 0 ? new float[corePerfRows.Length] : CoreEmptyFloats;
            }
            if (corePerfBandTexts.Length != corePerfRows.Length)
            {
                corePerfBandTexts = corePerfRows.Length > 0 ? new string[corePerfRows.Length] : CoreEmptyStrings;
            }
            // 行序按当前排序状态算好（原序 / 姓名 / 数值列）—— 绘制期只按序取行（PERF-001）。
            EnsurePerformanceOrder(snapshot);
            for (int i = 0; i < corePerfRows.Length; i++)
            {
                PopulationRankRow rank = snapshot.Performance[corePerfOrder[i]];
                // **7 列**（2026-09-17 删表内序号列「№」——序号恒等于行序、信息量近零，且它使姓名列右移 3%）。
                corePerfRows[i][0] = rank.Label;
                corePerfRows[i][1] = rank.ValueText;
                corePerfRows[i][2] = rank.ShareText;
                corePerfRows[i][3] = rank.NoteText;
                corePerfRows[i][4] = rank.AvgQuadrumText;
                corePerfRows[i][5] = rank.PeakQuadrumText;
                corePerfRows[i][6] = rank.QuadrumCountText;
                // 阈值带色列（产出市值）：比值 = Share01（值 ÷ 榜首，已入快照）；
                // 文本 = ValueText（已格式化的千分位）。**不新增 snapshot 字段**（禁改数据契约）。
                corePerfBandValues[i] = rank.Share01;
                corePerfBandTexts[i] = rank.ValueText;
            }

            coreMedalRows = AllocCoreRows(snapshot.MedalTop.Count, 3);
            for (int i = 0; i < coreMedalRows.Length; i++)
            {
                PopulationCountRow medal = snapshot.MedalTop[i];
                coreMedalRows[i][0] = medal.Label;
                coreMedalRows[i][1] = Num(medal.Count);
                coreMedalRows[i][2] = medal.ShareText;
            }

            coreTitleRows = AllocCoreRows(snapshot.Titles.Count, 3);
            for (int i = 0; i < coreTitleRows.Length; i++)
            {
                PopulationCountRow title = snapshot.Titles[i];
                coreTitleRows[i][0] = title.Label;
                coreTitleRows[i][1] = Num(title.Count);
                coreTitleRows[i][2] = title.ShareText;
            }

            coreLaborRows = AllocCoreRows(snapshot.Labor.Count, 3);
            for (int i = 0; i < coreLaborRows.Length; i++)
            {
                PopulationLaborRow labor = snapshot.Labor[i];
                coreLaborRows[i][0] = labor.Label;
                coreLaborRows[i][1] = labor.HoursText;
                coreLaborRows[i][2] = labor.ShareText;
            }

            // ⑦ 信仰面板（v11 独立面板）：理念分布（含可选「无信仰」末行）+ 戒律表。
            if (snapshot.IdeologyEnabled)
            {
                int ideoRows = snapshot.Ideologies.Count + (snapshot.IdeologyNoneCount > 0 ? 1 : 0);
                coreIdeoRows = AllocCoreRows(ideoRows, 3);
                for (int i = 0; i < snapshot.Ideologies.Count; i++)
                {
                    PopulationCountRow ideo = snapshot.Ideologies[i];
                    coreIdeoRows[i][0] = ideo.Label;
                    coreIdeoRows[i][1] = Num(ideo.Count);
                    coreIdeoRows[i][2] = ideo.ShareText;
                }
                if (snapshot.IdeologyNoneCount > 0 && coreIdeoRows.Length > 0)
                {
                    // 「无信仰」行放**末尾**（与环的末段同序）；占比按**全人口**口径（与环同分母）。
                    int last = coreIdeoRows.Length - 1;
                    int population = Mathf.Max(1, snapshot.IdeologyPopulationTotal);
                    coreIdeoRows[last][0] = PopTextOf(PopText.RowIdeologyNone);
                    coreIdeoRows[last][1] = Num(snapshot.IdeologyNoneCount);
                    coreIdeoRows[last][2] =
                        (snapshot.IdeologyNoneCount * 100f / population)
                        .ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "%";
                }

                corePreceptRows = AllocCoreRows(snapshot.Precepts.Count, 3);
                for (int i = 0; i < corePreceptRows.Length; i++)
                {
                    PopulationCountRow precept = snapshot.Precepts[i];
                    corePreceptRows[i][0] = precept.Label;
                    corePreceptRows[i][1] = Num(precept.Count);
                    corePreceptRows[i][2] = precept.ShareText;
                }
            }
        }

        /// <summary>按**精确**行数分配行缓冲（长度 = 数据行数，供 StatTable 直接消费）。</summary>
        private static string[][] AllocCoreRows(int rows, int cols)
        {
            if (rows <= 0)
            {
                return CoreEmptyRows;
            }
            string[][] buffer = new string[rows][];
            for (int i = 0; i < rows; i++)
            {
                buffer[i] = new string[cols];
            }
            return buffer;
        }

        /// <summary>
        /// 核心面板的**详细列表**：优先 RUIF `DataAtoms.StatTable`（表头 `PanelRaised` 底 +
        /// 斑马纹行 + 比例列宽 + 可选列样式/阈值带色列）；RUIF 缺失 / 抛异常 ⇒ 回落本工程**同版式**自绘
        /// （同样的列宽比例与斑马纹 ⇒ 观感不跳变，且不因第三方缺失而丢整块信息）。
        ///
        /// <para><b>降级阶梯（D0/D1/D2，由 <see cref="RuifBridge.TryStatTableEx"/> 分派）</b>：
        /// D0 = RUIF ≥ 1.2 走新重载（完整 1:1，含带色列与逐列对齐）；
        /// D1 = RUIF &lt; 1.2 走旧 5 参重载（左对齐、无带色，一次性日志）；
        /// D2 = RUIF 缺失/抛异常 ⇒ 本方法尾部的同版式自绘（**无**带色与右对齐，属降级）。</para>
        ///
        /// <para><paramref name="aligns"/> / <paramref name="dims"/> / <paramref name="bandCol"/> 仅 D0 生效；
        /// 未传（null / -1）时与 v1.1 完全同版式 —— 荣誉榜 / 职称 / 劳动力三表仍走该默认路径（接回预留）。
        /// <b>参数一律用框架内类型</b>（<c>int[]</c> / <c>bool[]</c>）：本方法本身就是 **D2 自绘兜底**，
        /// 若形参是 <c>RUIF.StatCol[]</c>，其 JIT 会解析 RUIF 类型 ⇒ D2 一并失效（ADR-AMD-1 §3 #3）。
        /// 编码：<paramref name="aligns"/> <c>0 = 左 / 1 = 右</c>。</para>
        ///
        /// <para><paramref name="rowH"/> = 表行高；**未传（0）时取概览通用行高 20**
        /// （<see cref="PopulationTableRowHeight"/>）。核心面板 B 传 34
        /// （<see cref="PerformanceTableRowHeight"/>，预览中唯一 `.coretbl tall`）—— 该值同时决定
        /// 早退阈值、`StatTable` 预留契约与 D2 自绘几何，三处**同源**（LAND-002）。</para>
        /// </summary>
        private static void DrawCoreTable(Rect rect, string[] headers, string[][] rows, float[] shares,
            int[] aligns = null, bool[] dims = null, int bandCol = -1, float bandRef = 0f,
            float[] bandValues = null, string[] bandTexts = null, float rowH = 0f)
        {
            float rh = rowH > 0f ? rowH : PopulationTableRowHeight;
            if (headers == null || headers.Length == 0 || rect.height < rh)
            {
                return;
            }
            if (RuifBridge.TryStatTableEx(rect, headers, rows, shares, rh,
                    aligns, dims, bandCol, bandRef, bandValues, bandTexts))
            {
                return;
            }
            // ── D2 回落自绘（与 RUIF StatTable 同版式；RUIF 缺失时无带色 / 无右对齐）──────────
            int columns = headers.Length;
            float totalShare = 0f;
            for (int c = 0; c < columns; c++)
            {
                totalShare += shares != null && c < shares.Length ? shares[c] : 1f;
            }
            if (totalShare <= 0f)
            {
                totalShare = columns;
            }
            float x = rect.x;
            for (int c = 0; c < columns; c++)
            {
                float share = shares != null && c < shares.Length ? shares[c] : 1f;
                CoreTableWidths[c] = c == columns - 1
                    ? Mathf.Max(0f, rect.xMax - x)
                    : rect.width * share / totalShare;
                x += CoreTableWidths[c];
            }
            RUIF.Draw.Box(new Rect(rect.x, rect.y, rect.width, rh),
                UITheme.WithAlpha(UITheme.Text, 0.06f));
            float headerX = rect.x;
            for (int c = 0; c < columns; c++)
            {
                UIComponents.Label(new Rect(headerX, rect.y, CoreTableWidths[c], rh),
                    UIComponents.TruncateToWidth(headers[c], CoreTableWidths[c], GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                headerX += CoreTableWidths[c];
            }
            UIComponents.Rule(new Rect(rect.x, rect.y + rh, rect.width,
                UITheme.RuleHeight), UITheme.BorderSoft);
            int rowCount = rows != null ? rows.Length : 0;
            for (int r = 0; r < rowCount; r++)
            {
                float rowY = rect.y + rh * (r + 1);
                if (rowY + rh > rect.yMax)
                {
                    break;
                }
                if (r % 2 == 1)
                {
                    RUIF.Draw.Box(new Rect(rect.x, rowY, rect.width, rh),
                        UITheme.WithAlpha(UITheme.Text, 0.04f));
                }
                float cellX = rect.x;
                for (int c = 0; c < columns; c++)
                {
                    string text = rows[r] != null && c < rows[r].Length ? rows[r][c] : string.Empty;
                    UIComponents.Label(new Rect(cellX, rowY, CoreTableWidths[c], rh),
                        UIComponents.TruncateToWidth(text, CoreTableWidths[c], GameFont.Tiny),
                        GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
                    cellX += CoreTableWidths[c];
                }
            }
        }

        /// <summary>
        /// ⑥ 绩效**系列色**（Top 1 / 2 / 3 = **金 / 银 / 铜**；越界回落 muted）。
        ///
        /// <para>⚠ 原用 `Accent + Info`：大多数主题下二者同色系（史诗主题：金 #D4AF37 与
        /// 金 #DAB457，ΔRGB ≈ 40 ≪ 100）⇒ 多系列折线**互相不可辨**、堆叠段糊成一片
        /// （预览实机同证）。金银铜三色 ΔRGB ≥ 100，且与「排行名次」语义天然一致
        /// ——与 ③ 荣誉面板的勋章档位同色系。</para>
        /// <para>每次调用现取（主题切换即时生效，与 `medalRingColors` 同规，不做静态缓存）。</para>
        /// </summary>
        private static Color PerformanceSeriesColor(int index)
        {
            switch (index)
            {
                case 0: return UITheme.MedalTierColor(MedalTier.Gold);
                case 1: return UITheme.MedalTierColor(MedalTier.Silver);
                case 2: return UITheme.MedalTierColor(MedalTier.Bronze);
                default: return UITheme.Muted;
            }
        }

        /// <summary>多系列折线的颜色缓冲（RUIF `LineChart(…, Color[], …)` 需要数组；
        /// 绘制前按 `PerformanceSeriesColor` 填充，实例级持有避免每帧分配）。</summary>
        private readonly Color[] performanceSeriesColors = new Color[3];

        /// <summary>
        /// ⑥ **累计贡献趋势卡**（预览 v5 HTML 941-965）：
        /// sec-h 标题行（标题 + 「Top 3 分线 · 起季 → 末季」）
        /// → 卡（`Shapes.InsetPanel`）：图例行（色点 + 姓名 + 右端 y 上界）
        /// → 折线区（RUIF `Charts.LineChart` 多系列，共享 y 轴与水平网格）
        /// → x 轴季度标签行（4 个，与预览 962-963 的 `space-between` 同构）。
        /// 返回卡片底部 y。
        ///
        /// <para><b>[接回契约]</b>（v7 起停止调用，方法体与数据采集**全部保留**）：
        /// ① **移除原因**：预览 v5→v7 把「绩效 / 荣誉 / 劳动力」合并为**一张 8 列表**
        /// （HTML 1011-1027），趋势卡不在 v7 的 `.coreB` 内 —— 旧版把趋势卡 + 左右分栏画成
        /// 四块拼盘，用户判定「完全没有实现基于预览图的 1:1 还原」。
        /// ② **接回步骤**：在 <see cref="DrawPerformancePanel"/> 顶部按
        /// <c>PerformanceTrendCaptionHeight + PerformanceTrendCardHeight</c> 调用本方法并把返回的 y
        /// 作为后续表的上边界；同时把这两项常量加回 <c>PanelHeights[1]</c> 的 `heights[1]` 表达式。
        /// ③ **依赖常量**：<c>PerformanceTrendCaptionHeight</c> / <c>PerformanceTrendCardHeight</c> /
        /// <c>PerformanceTrendPlotHeight</c> / <c>PerformanceTrendLegendHeight</c> /
        /// <c>PerformanceTrendAxisHeight</c> / <c>PerformanceSeriesColor</c> / <c>performanceSeriesColors</c>；
        /// 数据侧依赖 <c>PopulationOverviewSnapshot.PerformanceTrend*</c>（保持采集，未删）。</para>
        /// </summary>
        private float DrawPerformanceTrendCard(Rect rect, PopulationOverviewSnapshot snapshot)
        {
            float y = rect.y;
            // sec-h：标题左、范围右（预览 941 `.sec-h` + `.n`）。
            float titleWidth = rect.width * 0.5f;
            UIComponents.Label(new Rect(rect.x, y, titleWidth, PerformanceTrendCaptionHeight),
                UIComponents.TruncateToWidth(PopTextOf(PopText.PerformanceTrendTitle), titleWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(rect.x + titleWidth, y, rect.width - titleWidth, PerformanceTrendCaptionHeight),
                UIComponents.TruncateToWidth(PopTextOf(PopText.PerformanceTrendRange),
                    Mathf.Max(0f, rect.width - titleWidth), GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleRight);
            y += PerformanceTrendCaptionHeight;

            Rect card = new Rect(rect.x, y, rect.width, PerformanceTrendCardHeight);
            RUIF.Shapes.InsetPanel(card);
            Rect inner = new Rect(card.x + 7f, card.y + 4f,
                Mathf.Max(0f, card.width - 14f), Mathf.Max(0f, card.height - 8f));

            int seriesCount = snapshot.PerformanceTrendSeries != null ? snapshot.PerformanceTrendSeries.Length : 0;
            // 图例行（预览 945-950）：色点 + 姓名逐项，右端「y 上界 X」。
            float capWidth = Mathf.Min(76f, inner.width * 0.32f);
            float legendWidth = Mathf.Max(0f, inner.width - capWidth - 6f);
            if (seriesCount > 0)
            {
                float itemWidth = legendWidth / seriesCount;
                for (int i = 0; i < seriesCount; i++)
                {
                    float itemX = inner.x + i * itemWidth;
                    RUIF.Draw.Box(new Rect(itemX, inner.y + 4f, 6f, 6f), PerformanceSeriesColor(i));
                    string name = snapshot.PerformanceTrendNames != null && i < snapshot.PerformanceTrendNames.Length
                        ? snapshot.PerformanceTrendNames[i]
                        : string.Empty;
                    float nameWidth = Mathf.Max(0f, itemWidth - 11f);
                    UIComponents.Label(new Rect(itemX + 9f, inner.y, nameWidth, PerformanceTrendLegendHeight),
                        UIComponents.TruncateToWidth(name, nameWidth, GameFont.Tiny),
                        GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                }
                UIComponents.Label(new Rect(inner.xMax - capWidth, inner.y, capWidth, PerformanceTrendLegendHeight),
                    UIComponents.TruncateToWidth(PopTextOf(PopText.PerformanceTrendCap), capWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleRight);
            }

            // 折线区（预览 951-961）：RUIF 多系列（共享 y 轴 + 水平网格；失败即不画，不回落自研）。
            Rect plot = new Rect(inner.x, inner.y + PerformanceTrendLegendHeight + 2f,
                inner.width, PerformanceTrendPlotHeight);
            if (seriesCount > 0)
            {
                // 系列色金/银/铜（每帧现取 ⇒ 主题切换即时生效）；缓冲区实例级复用。
                int colorCount = Mathf.Min(seriesCount, performanceSeriesColors.Length);
                for (int i = 0; i < colorCount; i++)
                {
                    performanceSeriesColors[i] = PerformanceSeriesColor(i);
                }
                RuifBridge.TryLineChartMulti(plot, snapshot.PerformanceTrendSeries, performanceSeriesColors, false, true);
            }
            else
            {
                // 空态（旧档无按季累计）：如实说明，不画 0 假线。
                UIComponents.Label(new Rect(inner.x, plot.y + plot.height * 0.35f, inner.width, PopulationRowHeight),
                    UIComponents.TruncateToWidth(PopTextOf(PopText.PerformanceTrendNone), inner.width, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            }

            // x 轴季度标签（预览 962-963）：首左 / 末右 / 中间居中。
            string[] axis = snapshot.PerformanceTrendAxisLabels;
            if (axis != null && axis.Length > 0)
            {
                float slot = inner.width / axis.Length;
                float axisY = plot.yMax + 2f;
                for (int i = 0; i < axis.Length; i++)
                {
                    TextAnchor anchor = i == 0
                        ? TextAnchor.MiddleLeft
                        : (i == axis.Length - 1 ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter);
                    UIComponents.Label(new Rect(inner.x + i * slot, axisY, slot, PerformanceTrendAxisHeight),
                        UIComponents.TruncateToWidth(axis[i], slot, GameFont.Tiny),
                        GameFont.Tiny, UITheme.Dim, anchor);
                }
            }
            return card.yMax;
        }

        /// <summary>
        /// ⑥ 表底合计行（预览 1084 `.drow`）：左「合计 · 人均 · 前 3 集中」/ 右拼好的三段值。
        ///
        /// <para><b>1:1 取证（预览 v7）</b>：① `.drow` **没有任何分隔线** —— 其 `border-bottom`
        /// 被 `.drow:last-child{border-bottom:0}`（HTML 419）取消，浏览器实测该行 `border:0px none`
        /// ⇒ 旧版在行上方画的 Rule **已移除**（那是不存在于预览的元素）。
        /// ② 左端 `.k` 前景 = `var(--dim)`（HTML 420，**不是 Muted**）；右端 `.v` = `var(--text)`。
        /// ③ 两端对齐 = `justify-content:space-between`（HTML 417）⇒ 左 `MiddleLeft` / 右 `MiddleRight`。</para>
        /// </summary>
        private void DrawPerformanceSummaryRow(Rect rect, PopulationOverviewSnapshot snapshot)
        {
            float labelWidth = rect.width * 0.55f;
            UIComponents.Label(new Rect(rect.x, rect.y, labelWidth, rect.height),
                UIComponents.TruncateToWidth(PopTextOf(PopText.PerformanceSummary), labelWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            float valueWidth = Mathf.Max(0f, rect.width - labelWidth);
            UIComponents.Label(new Rect(rect.x + labelWidth, rect.y, valueWidth, rect.height),
                UIComponents.TruncateToWidth(PopTextOf(PopText.PerformanceSummaryValue), valueWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Text, TextAnchor.MiddleRight);
        }

        /// <summary>
        /// 活跃度**面积图**（近 8 季事件数）：逐列填充 + 顶点描线（预览 ⑥ 趋势图的引擎等价）。
        /// 归一化与提示文本都在构建期算好，绘制期零计算、零拼接。
        /// </summary>
        private void DrawActivityBars(Rect rect, PopulationOverviewSnapshot snapshot)
        {
            if (snapshot.ActivityTrend == null || snapshot.ActivityTrend.Length < 2
                || snapshot.ActivityTrendMax <= 0)
            {
                return;
            }
            EnsureActivityTrendValues(snapshot);
            // 【RUIF 硬依赖】同 §能力图：走 RUIF，失败不画、不回落自研。
            if (activityTrendBuffer.Length != activityTrendValues.Count)
            {
                activityTrendBuffer = new float[activityTrendValues.Count];
            }
            for (int i = 0; i < activityTrendValues.Count; i++)
            {
                activityTrendBuffer[i] = activityTrendValues[i];
            }
            RuifBridge.TryAreaChart(rect, activityTrendBuffer);
            if (Mouse.IsOver(rect) && !string.IsNullOrEmpty(activityTrendTip))
            {
                TooltipHandler.TipRegion(rect, activityTrendTip);
            }
        }

        private readonly List<float> activityTrendValues = new List<float>();
        private string activityTrendTip;
        private long activityTrendRevision = -1L;

        /// <summary>
        /// RUIF 图表入参缓冲（`RUIF.Charts.*` 收 `float[]`，而读模型给 `List<float>`）——
        /// **复用数组 + 逐元素拷贝**（8 项量级），避免每帧 `ToArray()` 的堆分配（PERF-001）。
        /// </summary>
        private float[] activityTrendBuffer = new float[0];
        private float[] miniRingBuffer = new float[0];
        private float[] ideologyRingBuffer = new float[0];

        /// <summary>
        /// 环带厚（`UICharts.Ring` 口径 = 厚 / 外半径）→ RUIF `DonutChart` 的 `hole01`（= 内径 / 外径）。
        /// ⚠ RUIF `hole01` 的精确语义（内径比 / 面积比）**待实机核对**（DevDocs 仅写"环孔占比"，
        /// 已登记对照表 §12-B）；两种口径在本工程取值域内观感差异小，先按内径比传入。
        /// </summary>
        private static float HoleFromThickness(float thicknessRatio)
        {
            return Mathf.Clamp(1f - thicknessRatio, 0.05f, 0.95f);
        }

        private void EnsureActivityTrendValues(PopulationOverviewSnapshot snapshot)
        {
            if (activityTrendRevision == snapshot.BuiltFromRevision && activityTrendValues.Count > 0)
            {
                return;
            }
            activityTrendRevision = snapshot.BuiltFromRevision;
            activityTrendValues.Clear();
            int max = snapshot.ActivityTrendMax;
            System.Text.StringBuilder tip = new System.Text.StringBuilder(48);
            for (int i = 0; i < snapshot.ActivityTrend.Length; i++)
            {
                activityTrendValues.Add(max > 0
                    ? Mathf.Clamp01(snapshot.ActivityTrend[i] / (float)max)
                    : 0f);
                if (i > 0)
                {
                    tip.Append(" · ");
                }
                tip.Append(snapshot.ActivityTrend[i]);
            }
            activityTrendTip = tip.ToString();
        }

        /// <summary>
        /// ② 信仰与理念（预览 753-816 的分布部分）：未启用 Ideology 时画**如实提示**
        /// （不画空列表、不伪造理念），启用时逐理念一行（名称 + 人数 + 占比条）+ 可选「无信仰」行。
        /// Meme / Precept / 仪式次数不在本轮范围（需更深的 Ideology 读取面，缺口登记）。
        /// </summary>
        /// <summary>
        /// ⑦ 信仰与理念（**独立面板** · v11 结构）—— 2026-09-15 从核心面板 A 的右栏**拆回独立面板**，
        /// 并按预览 v11 重构为四段：
        ///
        /// <para>① **头部横条**：理念环（**全人口**口径，含「无信仰」末段；环心 = 全人口数）
        /// + 主流理念名 + 「N 人 · X%」读数。② **理念分布表**（`DataAtoms.StatTable`：名称 | 成员 | 占比）
        /// —— 每理念一行 + 可选「无信仰」行。③ **核心理念**（MemeDef）：标签 + chips 一行。
        /// ④ **戒律表**（同表组件：名称 | 人数 | 占比）。</para>
        ///
        /// <para>⚠ 与 <c>IdeologyContentHeight</c> **逐段同源**（LAND-002：不同源立刻表现为
        /// 面板底部被裁 / 白高一截）。</para>
        /// <para>⚠ 缺口如实（LAND-009）：预览 v11 的「信念度 %」「教义取值」「仪式次数」需更深的
        /// Ideo 读取面（尚未落地）⇒ 本方法**不画**这些，读数只取快照已有字段。</para>
        /// </summary>
        private void DrawIdeologyPanel(Rect row, PopulationOverviewSnapshot snapshot)
        {
            if (!snapshot.IdeologyEnabled)
            {
                UIComponents.Label(new Rect(row.x, row.y, row.width, PopulationRowHeight),
                    PopTextOf(PopText.RowIdeologyDisabled), GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                return;
            }
            // 表行缓冲：按快照数据版本重建（帧内幂等）⇒ 绘制期零分配（PERF-001）。
            EnsureCoreTableRows(snapshot);
            float y = row.y;

            // ══════════════ 2026-09-17 **紧缩版式**（用户报「信仰面板溢出导致整页下滑」）══════════════
            // 两处压缩，合计约 −150px（原 ~368 → ~220）：
            //   ① **chips 并入头部横条右列第二行**（原来独立成段：gap 6 + 标题行 18 + chips 行 18）
            //      —— 头部横条高 88 由环（84）决定，右列原本只占一行 ⇒ 第二行**零额外高度**；
            //   ② **理念分布表 ‖ 戒律表 并排两列**（原来上下堆叠）—— 表高由 Σ(1+rows) 变 max(1+rows)
            //      ⇒ 省 (1 + 较少者的行数) × 行高。
            // ⚠ 与 `IdeologyContentHeight`（Population.cs）**逐段同源**（LAND-002）。

            // ── ① 头部横条：环 + 主流理念名 / 读数 + 核心理念 chips（右列第二行）──
            float ring = Mathf.Min(IdeologyRingSize, Mathf.Max(48f, row.width * 0.26f));
            if (snapshot.Ideologies.Count > 0)
            {
                // 环 = **全人口**信仰结构（含「无信仰」段）—— 与预览 v11 同口径。
                EnsureIdeologyRing(snapshot);
                // 【RUIF 硬依赖 · 要求二】理念环走 RUIF；环心总数经 `centerLabel`。
                RuifBridge.TryDonutChart(new Rect(row.x, y, ring, ring), ideologyRingBuffer,
                    HoleFromThickness(0.36f), Num(ideologyRingTotal), ideologyRingColorBuffer);
                // 环心副标（预览「MEMBERS」；RUIF `centerLabel` 只承载单行 ⇒ 副标由文本原子画）。
                UIComponents.Label(new Rect(row.x, y + ring * 0.60f, ring, PopulationRowHeight),
                    PopTextOf(PopText.IdeologyMembers), GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleCenter);

                // 右列：第 1 行 = 主流理念名（左）+「N 人 · X%」读数（右）；第 2 行 = 核心理念 chips。
                float textX = row.x + ring + IdeologyHeadGap;
                float textWidth = Mathf.Max(40f, row.xMax - textX);
                PopulationCountRow main = snapshot.Ideologies[0];
                float nameWidth = Mathf.Max(20f, textWidth * 0.55f);
                UIComponents.Label(new Rect(textX, y + 3f, nameWidth, PopulationRowHeight + 3f),
                    UIComponents.TruncateToWidth(main.Label, nameWidth, GameFont.Small),
                    GameFont.Small, UITheme.Text, TextAnchor.MiddleLeft);
                UIComponents.Label(new Rect(textX + nameWidth, y + 3f, textWidth - nameWidth,
                        PopulationRowHeight + 3f),
                    UIComponents.TruncateToWidth(main.ValueText, textWidth - nameWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleRight);
                if (snapshot.IdeologyMemeChips.Count > 0)
                {
                    float chipY = y + 3f + PopulationRowHeight + 4f;
                    float captionWidth = Mathf.Min(66f, textWidth * 0.3f);
                    UIComponents.Label(new Rect(textX, chipY, captionWidth, PopulationChipHeight),
                        UIComponents.TruncateToWidth(PopTextOf(PopText.RowIdeologyMemes), captionWidth,
                            GameFont.Tiny),
                        GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                    DrawChipRow(new Rect(textX + captionWidth, chipY,
                            Mathf.Max(0f, textWidth - captionWidth), PopulationChipHeight),
                        chipY, snapshot.IdeologyMemeChips, UITheme.Info);
                }
            }
            y += IdeologyHeadHeight;

            // ── ② 理念分布表 ‖ ④ 戒律表（**并排两列**；各自存在时才画）─────────
            int ideoRows = coreIdeoRows.Length;
            int preceptRows = corePreceptRows.Length;
            if (ideoRows > 0 || preceptRows > 0)
            {
                y += IdeologyBlockGap;
                bool both = ideoRows > 0 && preceptRows > 0;
                float colGap = both ? IdeologyTableGap : 0f;
                float colWidth = both ? Mathf.Max(120f, (row.width - colGap) * 0.5f) : row.width;
                // 表高取两表的**较大者**（并排 ⇒ 行数取 max，不再相加）。
                int rows = Mathf.Max(ideoRows, preceptRows);
                float tableHeight = (1 + rows) * PopulationTableRowHeight;
                if (ideoRows > 0)
                {
                    DrawCoreTable(new Rect(row.x, y, colWidth, tableHeight),
                        CoreIdeoHeaders(), coreIdeoRows, CoreHonorTableShares);
                }
                if (preceptRows > 0)
                {
                    DrawCoreTable(new Rect(row.x + colWidth + colGap, y, colWidth, tableHeight),
                        CorePreceptHeaders(), corePreceptRows, CoreHonorTableShares);
                }
                y += tableHeight;
            }

            // 采集期失败的面板数（面板级失败隔离）—— 非 0 时如实提示，绝不静默。
            if (snapshot.FailedSections > 0)
            {
                DrawValueRow(At(row, y), PopTextOf(PopText.FailedNotice),
                    snapshot.FailedSections.ToString(), -1f, UITheme.Threat);
            }
        }

        // ══════════════ ⑦ 信仰面板（v11 结构）辅助 ══════════════

        /// <summary>理念环的**环心总数**（全人口，含无信仰）—— 供 `TryDonutChart` 的 `centerLabel`。</summary>
        private int ideologyRingTotal;

        /// <summary>信仰头部横条：环与右列文本之间的间距。</summary>
        private const float IdeologyHeadGap = 10f;

        /// <summary>理念分布表行缓冲（`[行][3]`：名称 · 成员 · 占比）。</summary>
        private string[][] coreIdeoRows = CoreEmptyRows;

        /// <summary>戒律表行缓冲（`[行][3]`：名称 · 人数 · 占比）。</summary>
        private string[][] corePreceptRows = CoreEmptyRows;

        private readonly string[] coreIdeoHeader = new string[3];
        private readonly string[] corePreceptHeader = new string[3];

        private string[] CoreIdeoHeaders()
        {
            coreIdeoHeader[0] = PopTextOf(PopText.CoreTableName);
            coreIdeoHeader[1] = PopTextOf(PopText.CoreTablePeople);
            coreIdeoHeader[2] = PopTextOf(PopText.CoreTableShare);
            return coreIdeoHeader;
        }

        private string[] CorePreceptHeaders()
        {
            corePreceptHeader[0] = PopTextOf(PopText.CoreTableName);
            corePreceptHeader[1] = PopTextOf(PopText.CoreTablePeople);
            corePreceptHeader[2] = PopTextOf(PopText.CoreTableShare);
            return corePreceptHeader;
        }

        /// <summary>
        /// 理念环数据准备 —— **全人口**口径：各理念段 + 可选「无信仰」末段（muted 色），
        /// 环心 = 全人口数（与预览 v11 同口径：旧实现只含有理念者，环心差 1 人）。
        /// 结果写入复用的三段缓冲（`ideologyRingValues` / `ideologyRingBuffer` / `ideologyRingColorBuffer`）。
        /// </summary>
        private void EnsureIdeologyRing(PopulationOverviewSnapshot snapshot)
        {
            ideologyRingValues.Clear();
            int total = 0;
            for (int i = 0; i < snapshot.Ideologies.Count; i++)
            {
                ideologyRingValues.Add(Mathf.Clamp01(snapshot.Ideologies[i].Share01));
                total += snapshot.Ideologies[i].Count;
            }
            bool withNone = snapshot.IdeologyNoneCount > 0;
            if (withNone)
            {
                int population = Mathf.Max(1, snapshot.IdeologyPopulationTotal);
                ideologyRingValues.Add(Mathf.Clamp01(snapshot.IdeologyNoneCount / (float)population));
                total += snapshot.IdeologyNoneCount;
            }
            ideologyRingTotal = total;
            int segments = ideologyRingValues.Count;
            if (ideologyRingBuffer.Length != segments)
            {
                ideologyRingBuffer = new float[segments];
            }
            if (ideologyRingColorBuffer.Length < segments)
            {
                ideologyRingColorBuffer = new Color[Mathf.Max(8, segments * 2)];
            }
            for (int i = 0; i < segments; i++)
            {
                ideologyRingBuffer[i] = ideologyRingValues[i];
                // 末尾「无信仰」段用 muted（预览 v11 的无信仰即 muted 段）。
                ideologyRingColorBuffer[i] = withNone && i == segments - 1
                    ? UITheme.Muted
                    : MiniRingColors[Mathf.Min(i, MiniRingColors.Length - 1)];
            }
        }

        // ---- 行与原子 ----------------------------------------------------------

        /// <summary>
        /// 取「同列、y 指定的那一行」的矩形。行高 = <see cref="PopulationRowHeight"/> 常量——
        /// **不能**取首行高度：面板方法现在拿到的是「整块内容高」的矩形（供内部栅格用），
        /// 若取首行高度会把每行画成整块高（灾难性拉伸）。
        /// </summary>
        private static Rect At(Rect firstRow, float y)
        {
            return new Rect(firstRow.x, y, firstRow.width, PopulationRowHeight);
        }

        /// <summary>
        /// 键值行：标签左、数值右、可选占比条。<paramref name="share01"/> &lt; 0 表示不画条。
        /// </summary>
        private static float DrawValueRow(Rect row, string label, string value, float share01, Color fill)
        {
            float barW = share01 >= 0f ? PopulationBarWidth : 0f;
            float valueW = 74f;
            float gap = 6f;
            float labelW = Mathf.Max(20f, row.width - valueW - barW - gap * 2f);
            string labelText = UIComponents.TruncateToWidth(label, labelW, GameFont.Tiny);
            string valueText = UIComponents.TruncateToWidth(value, valueW, GameFont.Tiny);
            UIComponents.Label(new Rect(row.x, row.y, labelW, row.height),
                labelText, GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(row.x + labelW + gap, row.y, valueW, row.height),
                valueText, GameFont.Tiny, UITheme.Text, TextAnchor.MiddleRight);
            // LAND-008：任一侧被截断时，悬停行给出完整「标签 · 数值」（只在悬停且确实截断时拼串）。
            if (Mouse.IsOver(row)
                && (!string.Equals(labelText, label, System.StringComparison.Ordinal)
                    || !string.Equals(valueText, value, System.StringComparison.Ordinal)))
            {
                TooltipHandler.TipRegion(row, label + " · " + value);
            }
            if (share01 >= 0f)
            {
                UIComponents.ProgressBar(
                    new Rect(row.x + labelW + gap * 2f + valueW, row.y + (row.height - PopulationBarHeight) * 0.5f,
                        barW, PopulationBarHeight),
                    share01, fill);
            }
            // 【预览 `.drow`（HTML 396-400）】每行底部 `1px dashed` 分隔（`:last-child` 除外）。
            // IMGUI 无虚线 ⇒ 半透明 hair 实线替代（登记 B 类降级）——行分隔节奏与预览一致。
            UIComponents.Rule(new Rect(row.x, row.yMax - 1f, row.width, UITheme.RuleHeight),
                UITheme.WithAlpha(UITheme.BorderSoft, 0.55f));
            return row.y + PopulationRowHeight;
        }

        /// <summary>
        /// 面板四角饰线（预览 `.panel::before/::after`：10×10 的 L 形、2px、accent 58% 透明）。
        /// 只画左上与右下两角（对应预览仅有的两个伪元素），是面板「精致感」的最低成本来源。
        /// </summary>
        private static void DrawPanelCornerAccents(Rect rect)
        {
            const float arm = 10f;
            const float thickness = 2f;
            const float inset = 4f;
            Color color = UITheme.WithAlpha(UITheme.Accent, 0.55f);
            float left = rect.x + inset;
            float top = rect.y + inset;
            float right = rect.xMax - inset;
            float bottom = rect.yMax - inset;
            RUIF.Draw.Box(new Rect(left, top, arm, thickness), color);
            RUIF.Draw.Box(new Rect(left, top, thickness, arm), color);
            RUIF.Draw.Box(new Rect(right - arm, bottom - thickness, arm, thickness), color);
            RUIF.Draw.Box(new Rect(right - thickness, bottom - arm, thickness, arm), color);
        }

        private static string Num(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string Dash()
        {
            return "—";
        }
    }
}

using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// Partial of <see cref="MasterControlTabWindow"/> ——**人员概览区块**（预览 <c>#v-overview</c>）。
    ///
    /// <para>与 <c>MasterControlTabWindow.Archive.cs</c> 同规：本文件<b>只绘制</b>，
    /// 不采集、不聚合、不认中心名（LAND-010）。所有数值来自
    /// <see cref="PopulationOverviewSnapshot"/>（构建期已本地化、已格式化）。</para>
    ///
    /// <para><b>高度与绘制同源</b>：面板高度由 <see cref="PanelHeights"/> 纯函数算出，
    /// <see cref="PopulationBlockHeight"/>（估算）与 <see cref="DrawPopulationBlock"/>（绘制）
    /// 共用它 —— 两处不同源立刻表现为滚动条多半截。</para>
    ///
    /// <para><b>降级</b>：列数按可用宽算（卡宽夹在 [300, 420]），宽不够就少列、不压缩文字；
    /// 名册面板恒定独占一行（它是表格，压窄就没法读）。</para>
    /// </summary>
    public sealed partial class MasterControlTabWindow
    {
        /// <summary>
        /// 区块标题行高 —— **恒为 0**：预览 `#v-overview` 的面板直接排布，没有「人员概览」
        /// 标题条（实机判定为多余外壳）。保留常量符号以便高度估算与绘制继续共用同一处定义。
        /// </summary>
        private const float PopulationTitleHeight = 0f;

        /// <summary>面板标题条高 —— 对齐预览 `.ptitle{padding:7px 13px}` + 12px 字 ≈ 26px。</summary>
        private const float PopulationPanelHeaderHeight = 26f;

        /// <summary>
        /// 概览页**唯一**的 Tiny 文本行高 = **18**（= RUIF `Measure.RowHeight(GameFont.Tiny)`）。
        ///
        /// <para>⛔ 铁律：任何 Tiny 文本的 rect 高度**不得小于它**。原版 `Widgets.Label`（RUIF
        /// <c>Draw.Label</c> 的底层）**不裁剪**：rect 给 16/14/12 时文字会上下溢出，被**随后绘制**的
        /// 元素（下一行文字 / 卡片底色 / 基线）盖住 —— 实机症状就是「概览页好多文本部分被遮盖」。
        /// 新增任何文本行时，高度一律取本常量，行距额外留 2px。</para>
        /// </summary>
        private const float PopulationRowHeight = 18f;

        /// <summary>面板内边距 —— 对齐预览 `.pbody.tight{padding:7px 10px}`。</summary>
        private const float PopulationPanelPadX = 10f;
        private const float PopulationPanelPadY = 7f;

        /// <summary>
        /// 面板间距 —— 对齐预览 `--gap:12px`（>1600px 宽时；≤1600 时预览为 10px）。
        /// <para>⚠ 2026-09-17 **实机取 10**：与预览「≤1600 档」同值（用户报「不是一页式」，取 −2px/行
        /// 的整页紧缩档；面板间隙对信息量零影响，是纯度最高的可压缩项）。</para>
        /// </summary>
        private const float PopulationPanelGap = 10f;

        /// <summary>chips 行高与间距 —— 对齐预览 `.chip`（小圆角标签，单行、放不下即停）。
        /// ⚠ 取 18 = Tiny 行高：chip 内的文本 rect 就是 chip 本身，给 16 会让文字上下各溢出 1px。</summary>
        private const float PopulationChipHeight = 18f;
        private const float PopulationChipGap = 4f;
        private const float PopulationBarWidth = 54f;
        private const float PopulationBarHeight = 6f;

        /// <summary>③ 列断点 —— 对齐预览 `@media(max-width:1320px)`（高于此值恒 3 列，不再增列）。</summary>
        private const float PopulationThreeColumnWidth = 1320f;

        /// <summary>② 列断点（再窄转单列；预览无此档，取面板可读下限 900 作为工程下限）。</summary>
        private const float PopulationTwoColumnWidth = 900f;

        /// <summary>
        /// **面板身份索引的槽位数**（= `PanelHeights` / `PanelTitleKeyOf` / `DrawOverviewPanel`
        /// 的索引域）。与 <see cref="PopulationPanelVisibleCount"/>（栅格显示数）区分开：
        /// 已移出栅格的面板仍占槽位，便于随时接回（索引即契约，GOV-009）。
        /// </summary>
        private const int PopulationPanelSlotCount = 8;

        /// <summary>
        /// 栅格**显示**的面板数（= `PanelDisplayOrder.Length`）。
        /// <para>2026-09-15 **终版（v11 落地）**：概览 = **3 块**（两行）——
        /// 行 1 = ① 规模与构成（12 单位整行）；行 2 = ⑦ 信仰与理念（4） + ② 人员绩效（8）。
        /// 对应预览 v11 的「上方面板拉升扩大」（规模独占整行、内容更多）
        /// +「信仰面板结构重构」（由核心 A 的右栏**拆回独立面板**）。</para>
        /// <para>同日更早的「核心面板合并」仍然有效：荣誉(2) / 劳动力(5) 已并入绩效(1)，
        /// 二者不再是独立面板。另有 ③ 派系(3) · ④ 派生(4) · ⑧ 专业(7) 已移出栅格。</para>
        /// </summary>
        private const int PopulationPanelVisibleCount = 3;

        /// <summary>身份环形直径 —— 对齐预览 ① 的 Donut `width:150px;height:150px`（HTML 609）。</summary>
        private const float IdentityRingSize = 150f;

        /// <summary>身份环形区块高（环 150 + 间隙；列内还有图例时按列高自适应）。</summary>
        private const float IdentityRingBlockHeight = 156f;

        /// <summary>
        /// ① 三列的**段标题行高**（预览 `.sec-h` = 3px info 竖条 + 10.5px 标签 + 右端 9.5px 注释）
        /// —— 实机取 Tiny 行高 18 + 2px 行距（LAND-007：文本 rect 不得小于行高）。
        /// </summary>
        private const float IdentityHeaderHeight = 20f;

        /// <summary>
        /// ① col1 趋势卡总高（预览 HTML 726-752 实测 165.6）——
        /// 上内边距 3 + 标题 18 + 图例 18 + 折线 104 + x 轴 18 + 下内边距 3 = **164**。
        /// ⚠ 旧值 72（= 标题 18 + 折线 34 + 轴 12 + 边距）比预览矮一倍：趋势卡在实机里
        /// 被压成一条窄带（用户截图对比可见），故按预览逐段重算。
        /// </summary>
        private const float JoinTrendCardHeight = 164f;

        /// <summary>趋势卡内折线区高（预览 HTML 745 `svg height:104px`）。</summary>
        private const float JoinTrendPlotHeight = 104f;

        /// <summary>
        /// ① col3 综合能力趋势卡总高（预览 HTML 794-821）——
        /// 上内边距 3 + 标题行 18 + 折线区 104 + 口径行 36 + 下内边距 3 = **164**。
        /// ⚠ 预览另有「月 / 季 / 年」段控行与「▲ 差值 / 量程」badge：三者都需要**历史序列**
        /// （能力均值为活读值），按 LAND-009 如实不画（缺口登记见对照清单）。
        /// </summary>
        private const float CapacityTrendCardHeight = 164f;

        /// <summary>① col3 口径行高（预览 HTML 817-820 的两行说明）。</summary>
        private const float CapacityScopeHeight = 36f;

        /// <summary>
        /// ① col3 **一行读数行**高（2026-09-17 移除「综合能力均值」趋势卡后新增）= Tiny 行高 18 ——
        /// 承载**两个真实读数**（综合能力均值 / 工效 = 意识 × 操作），二者原在 164px 卡的标题行与口径行里。
        /// </summary>
        private const float CapacitySummaryRowHeight = 18f;

        /// <summary>① 面板内部三列列间距（预览 HTML 604 `gap:16px`）。</summary>
        private const float ScaleColumnGap = 16f;

        /// <summary>
        /// 能力条**双列**之间的间距（预览 HTML 823 外层 grid `gap:1px 12px` ⇒ 列间距 12、行间距 1）。
        /// ⚠ 与三列列间距 <see cref="ScaleColumnGap"/>(16) 是两个量：旧实现混用 16 当能力条列间距。
        /// </summary>
        private const float ScaleBarColumnGap = 12f;

        /// <summary>
        /// ① 三列的**宽度比例** —— 对齐预览实测的 `grid-template-columns: 1.35fr 1fr 1fr`
        /// （1920 档实测列宽 724.7 / 536.8 / 536.8）。
        /// ⚠ 旧实现是**三等分**（`/3f`），与预览不符：身份列需要更多横向空间（环 + 右侧图例 + 趋势卡），
        /// 三等分会让趋势卡偏窄、性别/能力列偏宽。比例和 = 3.35，换算见 <see cref="DrawScalePanel"/>。
        /// </summary>
        private const float ScaleCol1Ratio = 1.35f;
        private const float ScaleCol2Ratio = 1f;
        private const float ScaleCol3Ratio = 1f;

        /// <summary>⑤ 派生卡片网格的间距与卡高（预览 HTML 850 `gap:7px`；
        /// 卡高 = 内边距 3 + 标题 18 + 3 行 ×18 + 余量 3 = 78）。⚠ 标题行曾是 16 ⇒ 组标题被卡底压住。</summary>
        private const float DerivedCardGap = 7f;
        private const float DerivedCardHeight = 78f;

        /// <summary>
        /// 性别构成区块高（预览 HTML 758-780 全量）=
        /// 段标题 (18+2) + 金字塔 3×(18+2) + 性比 18 + **说明行 18** + 表头 18 + 明细 3×18 + 合计 18
        /// = **206**。
        /// ⚠ 2026-09-15 结构对齐：标签改**段标题形态**（`.sec-h` + `.n` + info 竖条）、金字塔行距改
        /// **2px**（预览 `.pyr{gap:2px}`）、「分身份明细」说明行由**表下**移到**表上**（预览 767 在表前）。
        /// 标签 / 表头 / 说明行曾用 17 / 15 / 14（&lt; Tiny 行高 18）⇒ 文字溢出被邻行压住。
        /// </summary>
        private const float GenderPyramidBlockHeight = 206f;

        /// <summary>性别金字塔单行高 = 文本行 18 + 行距 2（预览 `.pyr{gap:2px}` + `.pyr-r{height:18px}`）。</summary>
        private const float GenderPyramidRowHeight = PopulationRowHeight + 2f;

        /// <summary>性别金字塔内镜像条高（预览 `.pyr-r .m/.f{height:10px}`；旧值 8 偏细）。</summary>
        private const float GenderPyramidBarHeight = 10f;

        /// <summary>小环形区块高（理念分布 / 专业方向；环 56 + 图例 3 行）。</summary>
        private const float MiniRingBlockHeight = 60f;

        /// <summary>小环形直径。</summary>
        private const float MiniRingSize = 56f;

        /// <summary>勋章构成环形区块高（金 / 银 / 铜；预览 ③ 的 donut）。</summary>
        private const float MedalRingBlockHeight = 60f;

        /// <summary>工种竖柱图区块高 = 图高 + 柱顶数值行 18 + 柱底工种名行 18 + 基线（预览 `.barv{height:96px}`）。
        /// ⚠ 两行都是文本 ⇒ 各占 Tiny 行高 18，不能按 12 计（文字会溢出被柱体盖住）。</summary>
        private const float LaborChartBlockHeight = 116f;

        /// <summary>
        /// 能力条单行高 —— 对齐预览 `.bars{grid-auto-rows:22px}`（HTML 403）。
        /// 旧值 14 比预览矮 8px：① 面板的 11 项能力条整体比预览紧一倍（用户报「组件参数大小」）。
        /// </summary>
        private const float CapacityRowHeight = 22f;

        /// <summary>活跃度趋势柱高（预览 ⑥ 趋势图区高度的柱状版）。
        /// ⚠ 2026-09-15：绩效面板的趋势位已让位给「**累计贡献趋势**」（预览 ⑥ 本体，真实数据），
        /// 活跃度实现保留未调用（`DrawActivityBars`），如需在档案视图复用可直接接回。</summary>
        private const float ActivityChartHeight = 30f;

        // ══════════════ ⑥ 人员绩效（预览 v7 `.coreB`：HTML 1004-1087）══════════════
        //
        // 【v7 形态】面板 = **一张 8 列 `DataAtoms.StatTable`（5 数据行）+ 表底合计行**。
        //   v5 的「趋势卡 + 左右分栏 + 右栏荣誉/劳动力」四块拼盘**已从显示移除**（用户判定
        //   「完全没有实现基于预览图的 1:1 还原」）；但它们的**绘制方法与数据采集全部保留**：
        //   [接回预留 · 见 PanelHeights[1] / DrawOverviewPanel case 1] —— 接回步骤见各方法 XML doc
        //   的 [接回契约] 段。下列趋势卡常量随之停用，接回趋势卡时按原值直接使用。

        /// <summary>⑥ 趋势卡上方 sec-h 标题行高（预览 941 `.sec-h`）—— = Tiny 行高，不得压缩。
        /// <para>[接回预留 · 见 PanelHeights[1] / DrawOverviewPanel case 1]（趋势卡 v7 起停用）。</para></summary>
        private const float PerformanceTrendCaptionHeight = 18f;

        /// <summary>⑥ 趋势卡总高 = 内边距 4 + 图例 18 + 2 + 折线 64 + 2 + 轴 18 + 4（预览 944-964）。
        /// ⚠ 趋势卡内**每一行文本 rect 都必须 ≥ 18**（Tiny 行高）：行高不足 ⇒ 文字溢出 rect
        /// （原版 Label 不裁剪）⇒ 被随后绘制的元素盖住。
        /// 2026-09-15：折线区由 44 放大到 64（配合面板 span 8 的宽度翻倍）。
        /// <para>[接回预留 · 见 PanelHeights[1] / DrawOverviewPanel case 1] —— v7 单表后停用；
        /// 本常量 + `PerformanceTrendCaptionHeight` 是接回趋势卡时 `heights[1]` 的加项。</para></summary>
        private const float PerformanceTrendCardHeight = 112f;

        /// <summary>⑥ 趋势卡内折线区高（预览 `svg height:44px`，HTML 951）。
        /// 2026-09-15：面板升为 **span 8**（宽近翻倍）⇒ 折线区同步放大到 64，趋势更易读。
        /// <para>[接回预留 · 见 PanelHeights[1] / DrawOverviewPanel case 1]。</para></summary>
        private const float PerformanceTrendPlotHeight = 64f;

        /// <summary>⑥ 趋势卡图例行高（预览 8.5px 字 + 色点，HTML 945）—— = Tiny 行高。
        /// <para>[接回预留 · 见 PanelHeights[1] / DrawOverviewPanel case 1]。</para></summary>
        private const float PerformanceTrendLegendHeight = 18f;

        /// <summary>⑥ 趋势卡 x 轴标签行高（预览 HTML 962）—— = Tiny 行高。
        /// <para>[接回预留 · 见 PanelHeights[1] / DrawOverviewPanel case 1]。</para></summary>
        private const float PerformanceTrendAxisHeight = 18f;

        /// <summary>
        /// ⑥ 排行口径说明行高（预览 967 `.rule-lbl`）—— = Tiny 行高。
        ///
        /// <para>[接回预留 · 见 PanelHeights[1] / DrawOverviewPanel case 1] —— v7 起**从显示移除**
        /// （严格 1:1：预览 v7 的 `.coreB` 只有一张表 + 合计行；且其原文「工时见行悬停」因工时已成
        /// 独立列而失真）。<c>DrawPerformancePanel</c> 的对应绘制块与 <c>PopText.PerformanceScope</c>
        /// 的 Keyed 文案**全部保留**，接回时把该行加回 <c>DrawPerformancePanel</c> 顶部并把本常量计入
        /// <c>heights[1]</c> 即可。</para>
        /// </summary>
        private const float PerformanceScopeRowHeight = 18f;

        /// <summary>⑥ 合计行高（预览 975 `.drow`）。</summary>
        private const float PerformanceSummaryRowHeight = 18f;

        /// <summary>
        /// ⑥ 表底合计行与数据表之间的**视觉间距** = 12。
        ///
        /// <para><b>1:1 取证（预览 v7 · `.coreB`）</b>：浏览器实测表底 <c>yMax = 864.27</c>、
        /// 合计行 <c>y = 876.27</c> ⇒ 间距 **12px**，由两段叠加而成 ——
        /// `.pbody.tight{gap:6px}`（HTML 173）**＋** `.drow{margin-top:6px}`（HTML 1084 内联）。
        /// 本工程绘制路径没有 flex gap ⇒ 合并为一个常量（否则间距会少 6px）。</para>
        /// <para>⚠ 架构裁决原文只记了 `.drow margin-top` 的 6f，漏计 flex gap；此处按**实测**取 12
        /// （证据：`_verify_tmp/preview_coreB_params.json` 的 `coretbl.box` / `drow.box`）。</para>
        /// </summary>
        private const float PerformanceSummaryMarginTop = 12f;

        /// <summary>⑥ 排行行姓名列宽（预览 `grid-template-columns:76px 1fr 58px`）。
        /// <para>[接回预留 · 见 PanelHeights[1] / DrawOverviewPanel case 1] —— v4 起「姓名+条+数值」
        /// 三列版式已被 `StatTable` 取代，本常量随 `DrawPerformanceTrendCard` 的旧排行版式一并停用。</para></summary>
        private const float PerformanceNameColumnWidth = 76f;

        /// <summary>⑥ 排行行数值列宽（同上 grid 的 58px 列）。
        /// <para>[接回预留 · 见 PanelHeights[1] / DrawOverviewPanel case 1]。</para></summary>
        private const float PerformanceValueColumnWidth = 58f;

        /// <summary>⑥ 排行行里的条高（预览 `.bar` 的 6px）。
        /// <para>[接回预留 · 见 PanelHeights[1] / DrawOverviewPanel case 1]。</para></summary>
        private const float PerformanceRankBarHeight = 6f;

        // ══════════════ 核心面板 B：详细列表（RUIF `DataAtoms.StatTable`）══════════════
        //
        // 2026-09-15（用户要求「人员绩效合并两个辅助面板，最好用详细的列表组件」）：
        // 绩效 / 荣誉 / 劳动力三块全部改用**统计表**承载 —— 表头 + 斑马纹行 + 比例列宽，
        // 信息密度高于 chips / 柱图，且三块共用同一视觉语言。

        /// <summary>
        /// 概览**通用表行高**（表头与数据行同高）—— 预览 `.coretbl` **默认 20px**。
        ///
        /// <para><b>1:1 取证（预览 v7）</b>：`.coretbl .hd,.coretbl .rw{...height:20px}`（HTML 181）
        /// —— 信仰面板的理念分布表（HTML 910）与戒律表（HTML 937）都是**无 `tall` 的 `.coretbl`**
        /// ⇒ 逐行 20px。带 `tall` 的只有核心面板 B 一张（HTML 1029）⇒ 那处用
        /// <see cref="PerformanceTableRowHeight"/>，**不**改本常量。</para>
        /// <para>依赖方 —— <c>CoreHonorBlockHeight</c> / <c>CoreLaborBlockHeight</c>（接回预留）、
        /// <c>IdeologyContentHeight</c>、<c>DrawCoreTable</c> 自绘回落默认值，以及荣誉 / 职称 /
        /// 劳动力 / 理念 / 戒律五处未传 `rowH` 的 `DrawCoreTable` 调用。</para>
        /// </summary>
        private const float PopulationTableRowHeight = 20f;

        /// <summary>
        /// 核心面板 B（绩效融合大表）的**专用表行高** —— 预览中**唯一** `.coretbl tall`。
        ///
        /// <para>⚠ 2026-09-17 **实机取 30（紧凑档）**（用户报「右侧还有下滑键，不是一页式」）：
        /// 预览的 34 是按 **5 行**标的（`RowDensity.Cozy`），而实机本表 **7 行**（表头 + 6 人）
        /// ⇒ 34 让行 2 面板高 ≈310、整页内容超出一屏。30 仍 **&gt; Tiny 行高 18 + 行内上下留白**
        /// （不违反「行内文本 rect ≥ 行高」铁律），换 −28px 整页高。计高与绘制同源（同用本常量）。</para>
        ///
        /// <para><b>1:1 取证（预览 v7 · `.coreB`）</b>：`.coretbl.tall .hd,.coretbl.tall .rw{height:34px}`
        /// （HTML 195；且 HTML 1029 是**全页唯一** `<div class="coretbl tall">`）—— 浏览器实测表头与
        /// 5 个数据行**逐行 34px**、表总高 206（= 2×1px 外框 + 6×34）。</para>
        /// <para>来源公式 —— 34 = <c>RUIF.TableFmt.RowHeight(RowDensity.Cozy)</c>
        /// （`RUIF/Source/Components/TableAtoms.cs:26`），即**组件原生 Cozy 档**；<c>const</c> 不能调方法
        /// ⇒ 2026-09-17 起实机取 **30**（紧凑档，理由见上）。该值**不再逐一对应** RUIF 的
        /// `RowDensity` 档位 —— `StatTable` 只把 `rowH` 当数值用（行越出 rect 底边即 break），
        /// 故改 RUIF 密度档时**无需**跟改本常量。</para>
        /// </summary>
        private const float PerformanceTableRowHeight = 30f;

        /// <summary>详细列表前的分区标题行高（= Tiny 行高 + 2）。</summary>
        private const float PopulationTableCaptionHeight = 20f;

        /// <summary>
        /// 核心面板 B 右栏两个分区（荣誉 / 劳动力）之间的间隔 + 分区线高度。
        /// <para>[接回预留 · 见 PanelHeights[1] / DrawOverviewPanel case 1] —— v7 起左右分栏取消
        /// （预览 v5→v7 合并为**单表**），本常量随分栏一起停用；`DrawHonorPanel` / `DrawLaborPanel`
        /// / `CoreHonorBlockHeight` / `CoreLaborBlockHeight` 全部保留可接回。</para>
        /// </summary>
        private const float PopulationCoreSplitGap = 13f;

        /// <summary>
        /// 核心面板 B 左右两栏的间距（栏间画 1px 分区线，与预览 `border-left` 同形）。
        /// <para>[接回预留 · 见 PanelHeights[1] / DrawOverviewPanel case 1] —— v7 单栏后停用。</para>
        /// </summary>
        private const float PopulationCoreColumnGap = 16f;

        /// <summary>
        /// 核心面板 B 左栏占行宽比例（其余归右栏）—— 绩效含趋势图与 5 列表，略宽。
        /// <para>[接回预留 · 见 PanelHeights[1] / DrawOverviewPanel case 1] —— v7 单栏后停用。</para>
        /// </summary>
        private const float PopulationCoreLeftRatio = 0.52f;

        // ══════════════ ⑦ 信仰与理念（v11 独立面板结构）══════════════
        // 2026-09-15 v11：从核心面板 A 右栏**拆回独立面板**，并按预览 v11 重构为
        // 「头部横条 + 理念分布表 + 核心理念 chips + 戒律表」四段。

        /// <summary>信仰头部横条的环直径（预览 v11 `width:84px;height:84px`）。</summary>
        private const float IdeologyRingSize = 84f;

        /// <summary>
        /// 信仰头部横条高（环 84 + 上下各 2 余量 = 88）—— **绘制与计高同源**：
        /// 头部高度取「环」与「右侧文本列」的较大者，文本列恒不高过环。
        /// </summary>
        private const float IdeologyHeadHeight = 88f;

        /// <summary>信仰面板内四段之间的间距（预览 v11 `gap:5px` 的实机折算：按 Tiny 行距取 6）。</summary>
        private const float IdeologyBlockGap = 6f;

        /// <summary>
        /// 信仰面板「理念分布表 ‖ 戒律表」**并排两列**之间的列间距（2026-09-17 紧缩版式；
        /// 仅两表都存在时生效 —— 只有一张表时它独占整宽）。
        /// </summary>
        private const float IdeologyTableGap = 10f;

        /// <summary>
        /// 绩效表的列宽比例（**8 列** · v6/v7 预览形态）：
        /// `#` · 姓名 · 产出市值 · 占比 · 工时 · 平均季产 · 峰值季 · 参与季数。
        ///
        /// <para><b>1:1 取证（预览 v7 · `.coreB`）</b>：浏览器实测 1206.67px 表宽下
        /// `.coretbl.tall .hd` 的 `grid-template-columns` **求值像素列宽**
        /// = <c>[34, 366.672, 132, 104, 128, 128, 124, 132]</c>（HTML 1030 / 1046，8 列内联 grid）。
        /// 列宽合计 1148.67（= 表宽 − 行内水平内边距 16 − 6px 列间隙 × 7）⇒ **本表 = 各列宽 ÷ 1148.67**。</para>
        /// <para>⚠ RUIF 的 <c>TableFmt.ColumnSplit</c> **无内边距与列间隙**，按比例切到底；把
        /// 内边距/间隙**均摊进各列**（而非只放大末列）⇒ 数值列**左边界**与预览误差 ≤ 1.3px
        /// （实测：col2..col7 左边界 420.9/559.5/668.7/803.2/937.7/1068 vs 预览 420.67/558.67/668.67/802.67/936.67/1066.67）。</para>
        /// <para>⚠ 与 <c>CorePerfHeaders()</c> / <c>corePerfRows</c> / <c>CorePerfColAligns</c> 的列数**必须一致**
        /// （任一脱节都会列宽错位）。末列由 <c>ColumnSplit</c> 取余宽（≈138.6，比预览 132 宽 6.6px）。</para>
        /// </summary>
        /// <para><b>2026-09-17 重平衡</b>（用户报「列宽有些过宽有些过窄」）：上表的实测值是在
        /// <b>1206.67px</b> 表宽下取的，而实机本面板 = 12 单位里的 **8 单位**（更宽）⇒ 等比放大后
        /// 「姓名」列的**绝对宽**远超需要（CJK 姓名最多 6 字 ≈ 80px，却分到 ≈ 370px 空白），
        /// 而「产出市值 / 占比 / 工时 / 平均季产」等数值列被挤窄（"1,363.0" 这类长数字贴近换列边界）。</para>
        /// <para><b>2026-09-17 删「№」列</b>（用户要求「不要序号」）：表由 **8 列 → 7 列**
        /// （表内序号恒等于行序、信息量近零，且它使「姓名」列右移 3%）。
        /// 下列比例 = 上一版去掉名次列（0.030）后**按剩余 0.970 归一**，故各列实际宽不变。</para>
        private static readonly float[] CorePerfTableShares =
            { 0.216490f, 0.134020f, 0.113400f, 0.134020f, 0.134020f, 0.123710f, 0.144330f };

        /// <summary>
        /// 绩效 8 列的**水平对齐**（框架内类型 · 与 <see cref="CorePerfTableShares"/> 逐列对应）：
        /// `#`左 / `姓名`左 / 其余 6 列右（含表头）。编码 —— <c>0 = MiddleLeft</c> · <c>1 = MiddleRight</c>。
        ///
        /// <para><b>1:1 取证（预览 v7 · `.coreB`）</b>：HTML `.coretbl .num{text-align:right}`
        /// ⇒ 数值列（含表头）右对齐。</para>
        ///
        /// <para><b>跨库边界（ADR-AMD-1 §2 D-1 · §3 #1）</b>：本列样式<strong>禁止</strong>声明为
        /// <c>RUIF.StatCol[]</c> —— 字段类型属「类型位置」，其 <c>.cctor</c> 会在**类型加载期**解析
        /// <c>RUIF.StatCol</c>，与 <c>available</c> / 版本门控 / try-catch 全无关系（值类型同样不豁免）。
        /// 故改用框架内类型（<c>int[]</c> / <c>bool[]</c>）；RUIF 类型仅由
        /// <c>RuifBridge.TryStatTableV12</c> 在**版本门控通过之后**才构造。</para>
        /// <para>静态 readonly ⇒ 绘制期零分配；组件侧<strong>不持有</strong>列状态（GATE-UI）。</para>
        /// </summary>
        /// <para>2026-09-17：删「№」列后为 **7 列** —— `姓名`左 / 其余 6 列右（含表头）。</para>
        private static readonly int[] CorePerfColAligns = { 0, 1, 1, 1, 1, 1, 1 };

        /// <summary>
        /// 绩效 8 列是否**降调前景**（<see cref="Tokens.Dim"/>；与 <see cref="CorePerfColAligns"/> 同序）。
        /// <para>`--dim` 前景见预览 HTML 1047/1049/1051（`#` / 占比 / 参与季数 三列）。框架内类型，
        /// 理由同 <see cref="CorePerfColAligns"/> 的跨库边界说明。</para>
        /// </summary>
        /// <para>2026-09-17：删「№」列后 7 列 —— `占比`（索引 2）与 `参与季数`（索引 6）降调。</para>
        private static readonly bool[] CorePerfColDims = { false, false, true, false, false, false, true };

        /// <summary>绩效表**带色列**下标（产出市值；删「№」列后由 2 → **1**）。</summary>
        private const int CorePerfBandCol = 1;

        /// <summary>带色比的参考值 = 1 —— 因为 <c>bandValues</c> 直接传已归一化的
        /// <see cref="Application.ReadModels.PopulationRankRow.Share01"/>（值 ÷ 榜首同名次口径），
        /// ⇒ <c>ValueRatio(v, 1) == v</c>（恒等）。**不新增 snapshot 字段**（禁改数据契约）。</summary>
        private const float CorePerfBandRef = 1f;

        /// <summary>荣誉两张表（勋章榜 / 职称分布）共用的列宽比例：名称 · 数量 · 占比。</summary>
        private static readonly float[] CoreHonorTableShares = { 0.48f, 0.22f, 0.30f };

        /// <summary>劳动力表的列宽比例：工种 · 工时 · 占比。</summary>
        private static readonly float[] CoreLaborTableShares = { 0.48f, 0.26f, 0.26f };

        // ══════════════ 绘制缓存（PERF-001：绘制路径零翻译、零分配）══════════════
        // 快照 300 tick 才重建一次，而绘制是每帧的 ⇒ 两次刷新之间的文案与几何是**常量**。
        // 本工程既有范式 = UIComponents.ThemeSwitch 的「按 ActiveThemeId 失效的有界缓存」；
        // 这里按 (数据修订, 可用宽, 语言) 失效，缓存条数与面板/列数同阶（有界，非 unbounded）。
        // ⚠ 刻意**不用 static**：窗口实例各自持有，避免跨存档 / 跨界面的静态可变状态（AI-004）。

        /// <summary>概览区块内的全部固定文案（索引 = <see cref="PopText"/>；带参项在重建时格式化）。</summary>
        private enum PopText
        {
            PanelScale = 0,
            PanelLabor = 1,
            PanelHonors = 2,
            PanelSpec = 3,
            PanelDerived = 4,
            /// <summary>① 列 1 段标题的右端注释（预览 `.sec-h .n`：「PawnRole · 折线 = 近 8 季」）。</summary>
            IdentityNote = 6,
            RowCapacity = 7,
            RowLaborTotal = 8,
            RowLaborPerCapita = 9,
            RowLaborTopThree = 10,
            RowMedalAwarded = 11,
            RowMedalGold = 12,
            RowMedalSilver = 13,
            RowMedalBronze = 14,
            RowDirectionSet = 15,
            DerivedNoSource = 16,
            DerivedPending = 17,
            /// <summary>① 列 1 段标题（模板：「现役殖民者 {0} 人 · 身份构成」，预览 HTML 686）。</summary>
            IdentityHeader = 18,
            /// <summary>① 列 2 段标题的右端注释（预览 HTML 758）。</summary>
            GenderNote = 19,
            /// <summary>① 列 1 趋势卡标题（预览「身份人数趋势」，HTML 728）。</summary>
            TrendTitle = 20,
            PanelFactions = 21,
            PanelPerformance = 22,
            RowPerformanceTotal = 23,
            PanelIdeology = 24,
            RowIdeologyDisabled = 25,
            RowIdeologyNone = 26,
            RowIdeologyMemes = 27,
            RowActivityTrend = 28,
            /// <summary>采集失败提示（面板级失败隔离；0 时不画）。</summary>
            FailedNotice = 29,
            RowFree = 30,
            RowSlave = 31,
            RowPrisoner = 32,
            RowMedalTop = 33,
            RowPreceptTop = 34,
            RowGenderLabel = 35,
            DerivedGroupOutput = 36,
            DerivedGroupSupport = 37,
            DerivedGroupRisk = 38,
            DerivedGroupSkill = 39,
            NoLaborData = 40,
            NoPerformanceData = 41,
            /// <summary>性比行标签 / 性别明细表表头与合计（预览 ① col2，HTML 675-691）。</summary>
            GenderRatioLabel = 42,
            GenderColIdentity = 43,
            GenderColFemale = 44,
            GenderColMale = 45,
            GenderColSubtotal = 46,
            GenderColTotal = 47,
            /// <summary>① 列 3 段标题「健康能力」（预览 HTML 784）。</summary>
            HealthHeader = 48,
            /// <summary>分身份明细说明行（预览 ① col2，HTML 678）。</summary>
            GenderScope = 49,
            /// <summary>身份环中心副标（预览 ① col1，HTML 618「IN SERVICE」）。</summary>
            InService = 50,
            /// <summary>① 列 3 段标题的右端注释（预览 HTML 784）。</summary>
            HealthNote = 51,
            /// <summary>① 列 1 趋势卡量程（模板：「{0}–{1} 人」，预览 HTML 729）。</summary>
            TrendRange = 52,
            /// <summary>⑥ 绩效趋势卡：标题 / 范围行 / y 上界 / 空态说明（预览 HTML 941-965）。</summary>
            PerformanceTrendTitle = 53,
            PerformanceTrendRange = 54,
            PerformanceTrendCap = 55,
            PerformanceTrendNone = 56,
            /// <summary>⑥ 排行口径说明行（预览 HTML 967）。</summary>
            PerformanceScope = 57,
            /// <summary>⑥ 合计行标签（预览 HTML 975）。</summary>
            PerformanceSummary = 58,
            /// <summary>⑥ 合计行数值（构建期已按 Keyed 模板拼好：合计 · 人均 · 前 3 集中）。</summary>
            PerformanceSummaryValue = 59,
            /// <summary>③ 派系面板底部 drow 标签（「科技层级分布」）。</summary>
            FactionTechSummary = 60,
            /// <summary>② 信仰环环心副标（预览 773「MEMBERS」）。</summary>
            IdeologyMembers = 61,
            /// <summary>⑧ chips 末项「未设定 N」的标签。</summary>
            SpecializationUnset = 62,
            /// <summary>⑧ 方向缺口行标签（预览 ⑧ 底部 drow）。</summary>
            SpecializationGap = 63,
            /// <summary>⑧ 口径说明行（正式文字，非数值）。</summary>
            SpecializationNote = 64,
            /// <summary>⑥ 劳动力底部 drow：标签 + 拼好的值（前 3 占 X% · Y h/人）。</summary>
            LaborConcentration = 65,
            LaborConcentrationValue = 66,
            // ── 核心面板 B **详细列表表头**（RUIF `DataAtoms.StatTable`，2026-09-15 合并版）──
            /// <summary>
            /// 绩效表头：#（名次）—— **停用**（2026-09-17 用户要求「不要序号」⇒ 表内序号列已删）。
            /// ⚠ 槽位**必须保留**：PopText 索引是缓存槽契约（GOV-009），删掉会让其后所有索引前移。
            /// </summary>
            CoreTableRank = 67,
            /// <summary>表头：姓名 / 名称。</summary>
            CoreTableName = 68,
            /// <summary>表头：产出市值。</summary>
            CoreTableOutput = 69,
            /// <summary>表头：工时。</summary>
            CoreTableHours = 70,
            /// <summary>表头：占比。</summary>
            CoreTableShare = 71,
            /// <summary>表头：勋章。</summary>
            CoreTableMedal = 72,
            /// <summary>表头：枚数。</summary>
            CoreTableCount = 73,
            /// <summary>表头：职称。</summary>
            CoreTableTitle = 74,
            /// <summary>表头：人数。</summary>
            CoreTablePeople = 75,
            /// <summary>表头：工种。</summary>
            CoreTableJob = 76,
            /// <summary>表头：平均季产（绩效表第 6 列）。</summary>
            CoreTableAvgQuadrum = 77,
            /// <summary>表头：峰值季（绩效表第 7 列）。</summary>
            CoreTablePeakQuadrum = 78,
            /// <summary>表头：参与季数（绩效表第 8 列）。</summary>
            CoreTableQuadrumCount = 79,
            /// <summary>① 列 1 趋势卡交互提示（预览「悬停查看数值」，HTML 741）。</summary>
            TrendHint = 80,
            /// <summary>① 列 1 趋势图例的可点提示（预览 `title="点击显示 / 隐藏"`，HTML 738）。</summary>
            TrendLegendHint = 81,
            /// <summary>① 列 1 身份趋势空态（无存量回推数据）。</summary>
            TrendNoHistory = 82,
            /// <summary>① 列 3 综合能力趋势卡空态（能力均值为活读值、无历史 —— 预览 791-793 已登记）。</summary>
            CapacityTrendNone = 83,
            /// <summary>① 列 3 口径行（模板，含工效值；预览 HTML 817-820）。</summary>
            CapacityScope = 84,
            /// <summary>① 列 3 能力条空态（无活体可读）。</summary>
            CapacityNoSample = 85,
            /// <summary>性比行两端读数（模板；预览 HTML 764-766）。</summary>
            GenderRatioMale = 86,
            GenderRatioFemale = 87,
            /// <summary>性比行中段（模板「性比 {0}」；预览 HTML 765 的 `性比 0.71`）。</summary>
            GenderRatioValue = 88,
            /// <summary>① 列 3 口径行第二行（模板，含工效值；预览 HTML 819）。</summary>
            CapacityEfficiency = 89,
            Count = 90
        }

        private string[] populationText;
        private float[] populationPanelHeights;
        private long populationCacheRevision = -1L;
        private float populationCacheWidth = -1f;
        private int populationCacheLang;

        // ══════════════ ① 列 1 身份趋势卡的交互状态（内存 · 不入存档）══════════════
        //
        // 预览 HTML 719-725 列出趋势卡的三项交互：① 悬停十字线 + 数值气泡 ② 图例点击切显隐
        // ③ 时间范围（日 / 周 / 季）。③ **不落地** —— 三种粒度各需一套历史序列，工程只有
        // 8 季桶（LAND-009：不得用单一粒度冒充三档，故如实不画段控）。
        // ①② 需要的最小状态 = **逐系列显隐布尔**（纯 UI 内存，随窗口实例存活，**不写 Scribe**
        // —— GATE-UI：UI State 不得持久化）。悬停索引**不存字段**：每帧由鼠标位置反算。

        /// <summary>逐系列显隐（自由 / 奴隶 / 囚犯）—— 纯 UI 内存状态。</summary>
        private readonly bool[] identityTrendHidden = new bool[3];

        /// <summary>悬停气泡文案（逐段，缓存重建期拼好 ⇒ 绘制期零拼接、零翻译）。</summary>
        private string[] identityTrendTipsCache = new string[0];

        /// <summary>
        /// 可见系列 / 语义色的**按可见数索引**预分配缓冲（索引 1..3；避免每帧裁剪产生新数组 ——
        /// RUIF `LineChart` 收 `float[][]` 且按长度迭代 ⇒ 必须传**恰好长度**的数组，不能传长数组）。
        /// </summary>
        private readonly float[][][] identityTrendSeriesByCount = new float[4][][];
        private readonly Color[][] identityTrendColorsByCount = new Color[4][];

        /// <summary>
        /// 逐段悬停气泡（预览 HTML 719-725 ①）：把段标签与三系列人数按 Keyed 模板在**缓存重建期**
        /// 拼好（8 条，有界）。绘制期只按下标取串（PERF-001）。
        /// </summary>
        private void EnsureIdentityTrendTips(PopulationOverviewSnapshot snapshot)
        {
            float[] free = snapshot.IdentityTrendFree;
            int buckets = free != null ? free.Length : 0;
            if (identityTrendTipsCache.Length != buckets)
            {
                identityTrendTipsCache = new string[buckets];
            }
            string[] labels = snapshot.IdentityTrendBucketLabels;
            for (int i = 0; i < buckets; i++)
            {
                identityTrendTipsCache[i] = Translate4(
                    "PersonalChronicle.UI.MasterControl.Population.Trend.Tip",
                    (NamedArgument)(labels != null && i < labels.Length ? labels[i] : Dash()),
                    (NamedArgument)Mathf.RoundToInt(free[i]),
                    (NamedArgument)Mathf.RoundToInt(snapshot.IdentityTrendSlave[i]),
                    (NamedArgument)Mathf.RoundToInt(snapshot.IdentityTrendPrisoner[i]));
            }
        }

        /// <summary>取缓存文案（数组索引，零分配；缓存由 <see cref="EnsurePopulationDrawCache"/> 建立）。</summary>
        private string PopTextOf(PopText key)
        {
            int index = (int)key;
            return populationText != null && index < populationText.Length ? populationText[index] : string.Empty;
        }

        /// <summary>
        /// 按 (数据修订, 可用宽, 语言) 失效重建绘制缓存。宽度参与失效，因为表头/标题要
        /// 按可用宽**预先截断**（截断结果与宽度绑定，宽度变即失效）。
        /// </summary>
        private void EnsurePopulationDrawCache(PopulationOverviewSnapshot snapshot, float width)
        {
            // 语言标识用 folderName（LoadedLanguage 的稳定字段）：切语言后缓存必须失效，
            // 否则会继续画旧语言的标题（快照 300 tick 才重建，最长 5 秒残留）。
            int lang = LanguageDatabase.activeLanguage != null
                ? (LanguageDatabase.activeLanguage.folderName ?? string.Empty).GetHashCode()
                : 0;
            if (populationText != null
                && populationCacheRevision == snapshot.BuiltFromRevision
                && populationCacheLang == lang
                && Mathf.Abs(populationCacheWidth - width) < 0.5f)
            {
                return;
            }
            populationCacheRevision = snapshot.BuiltFromRevision;
            populationCacheWidth = width;
            populationCacheLang = lang;

            if (populationText == null || populationText.Length < (int)PopText.Count)
            {
                populationText = new string[(int)PopText.Count];
            }
            populationText[(int)PopText.PanelScale] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Scale");
            populationText[(int)PopText.PanelLabor] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Labor");
            populationText[(int)PopText.PanelHonors] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Honors");
            populationText[(int)PopText.PanelSpec] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Specialization");
            populationText[(int)PopText.PanelDerived] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Derived");
            // ① 三列的**段标题**（预览 `.sec-h` = 3px info 竖条 + 标签 + 右端注释）——
            // 2026-09-15 结构对齐：原来的「标签左 / 数值右」键值行**不是**预览形态（预览把
            // 现役人数并进段标题文案、注释放在右端）。带参项在缓存重建期拼好（绘制期零拼接）。
            populationText[(int)PopText.IdentityHeader] = Translate(
                "PersonalChronicle.UI.MasterControl.Population.Scale.IdentityHeader",
                (NamedArgument)snapshot.LiveColonists);
            populationText[(int)PopText.IdentityNote] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Scale.IdentityNote");
            populationText[(int)PopText.GenderNote] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Scale.GenderNote");
            populationText[(int)PopText.HealthHeader] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Scale.HealthHeader");
            populationText[(int)PopText.HealthNote] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Scale.HealthNote");
            populationText[(int)PopText.RowCapacity] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Capacity");
            populationText[(int)PopText.RowLaborTotal] =
                Translate("PersonalChronicle.UI.MasterControl.Population.LaborTotal");
            populationText[(int)PopText.RowLaborPerCapita] =
                Translate("PersonalChronicle.UI.MasterControl.Population.LaborPerCapita");
            populationText[(int)PopText.RowLaborTopThree] =
                Translate("PersonalChronicle.UI.MasterControl.Population.LaborTopThree");
            populationText[(int)PopText.RowMedalAwarded] =
                Translate("PersonalChronicle.UI.MasterControl.Population.MedalAwarded");
            populationText[(int)PopText.RowMedalGold] =
                Translate("PersonalChronicle.UI.MasterControl.Population.MedalGold");
            populationText[(int)PopText.RowMedalSilver] =
                Translate("PersonalChronicle.UI.MasterControl.Population.MedalSilver");
            populationText[(int)PopText.RowMedalBronze] =
                Translate("PersonalChronicle.UI.MasterControl.Population.MedalBronze");
            populationText[(int)PopText.RowDirectionSet] =
                Translate("PersonalChronicle.UI.MasterControl.Population.DirectionSet");
            populationText[(int)PopText.DerivedNoSource] =
                Translate("PersonalChronicle.UI.MasterControl.Population.DerivedNoSource");
            populationText[(int)PopText.DerivedPending] =
                Translate("PersonalChronicle.UI.MasterControl.Population.DerivedPending");
            // 身份图例在构建期拼好（绘制期零拼接）：预览 ① 的 Donut 图例在此降级为「堆叠条 + 图例」。
            populationText[(int)PopText.PanelIdeology] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Ideology");
            populationText[(int)PopText.RowIdeologyDisabled] =
                Translate("PersonalChronicle.UI.MasterControl.Population.IdeologyDisabled");
            populationText[(int)PopText.RowIdeologyNone] =
                Translate("PersonalChronicle.UI.MasterControl.Population.IdeologyNone");
            populationText[(int)PopText.RowIdeologyMemes] =
                Translate("PersonalChronicle.UI.MasterControl.Population.IdeologyMemes");
            populationText[(int)PopText.RowActivityTrend] =
                Translate("PersonalChronicle.UI.MasterControl.Population.ActivityTrend");
            populationText[(int)PopText.FailedNotice] =
                Translate("PersonalChronicle.UI.MasterControl.Population.FailedNotice");
            populationText[(int)PopText.RowFree] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Free");
            populationText[(int)PopText.RowSlave] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Slave");
            populationText[(int)PopText.RowPrisoner] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Prisoner");
            populationText[(int)PopText.RowMedalTop] =
                Translate("PersonalChronicle.UI.MasterControl.Population.MedalTop");
            populationText[(int)PopText.RowPreceptTop] =
                Translate("PersonalChronicle.UI.MasterControl.Population.PreceptTop");
            populationText[(int)PopText.RowGenderLabel] =
                Translate("PersonalChronicle.UI.MasterControl.Population.GenderLabel");
            populationText[(int)PopText.GenderRatioLabel] =
                Translate("PersonalChronicle.UI.MasterControl.Population.GenderRatioLabel");
            populationText[(int)PopText.GenderColIdentity] =
                Translate("PersonalChronicle.UI.MasterControl.Population.GenderCol.Identity");
            populationText[(int)PopText.GenderColFemale] =
                Translate("PersonalChronicle.UI.MasterControl.Population.GenderCol.Female");
            populationText[(int)PopText.GenderColMale] =
                Translate("PersonalChronicle.UI.MasterControl.Population.GenderCol.Male");
            populationText[(int)PopText.GenderColSubtotal] =
                Translate("PersonalChronicle.UI.MasterControl.Population.GenderCol.Subtotal");
            populationText[(int)PopText.GenderColTotal] =
                Translate("PersonalChronicle.UI.MasterControl.Population.GenderCol.Total");
            // ⚠ 2026-09-17 用户裁决：**「综合能力均值」趋势卡已移除**（原 164px = 标题行 18 +
            // 折线区 104 + 口径行 36）⇒ 本槽（原卡内长口径行）改承载**一行读数**：把两个**真实读数**
            // 压成一行（综合能力均值 / 工效 = 意识 × 操作），信息不丢、整列少占 146px。
            // 卡内长 Keyed（`CapacityTrend.Scope` / `CapacityTrend.Efficiency`）与 `CapacityTrendNone`
            // 仍留给 [接回预留] 的卡（`CapacityEfficiency` 槽随之不再被本行使用）；无样本画「—」。
            populationText[(int)PopText.CapacityScope] = Translate2(
                "PersonalChronicle.UI.MasterControl.Population.CapacitySummary",
                (NamedArgument)(string.IsNullOrEmpty(snapshot.CapacityAverageText)
                    ? Dash() : snapshot.CapacityAverageText),
                (NamedArgument)(snapshot.WorkEfficiency01 >= 0f
                    ? PercentText(snapshot.WorkEfficiency01)
                    : Dash()));
            populationText[(int)PopText.CapacityEfficiency] = Translate(
                "PersonalChronicle.UI.MasterControl.Population.CapacityTrend.Efficiency",
                (NamedArgument)(snapshot.WorkEfficiency01 >= 0f
                    ? PercentText(snapshot.WorkEfficiency01)
                    : Dash()));
            populationText[(int)PopText.CapacityTrendNone] =
                Translate("PersonalChronicle.UI.MasterControl.Population.CapacityTrend.None");
            populationText[(int)PopText.CapacityNoSample] =
                Translate("PersonalChronicle.UI.MasterControl.Population.CapacityNoSample");
            populationText[(int)PopText.GenderScope] =
                Translate("PersonalChronicle.UI.MasterControl.Population.GenderScope");
            populationText[(int)PopText.InService] =
                Translate("PersonalChronicle.UI.MasterControl.Population.InService");
            // ① 列 1 趋势卡（预览 HTML 726-751）：标题 / 量程 / 交互提示 / 空态。
            // ⚠ 预览的「日 / 周 / 季」段控**不落地**：工程只有季档历史（见 CN Keyed 注释）。
            populationText[(int)PopText.TrendTitle] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Trend.Title");
            populationText[(int)PopText.TrendHint] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Trend.Hint");
            populationText[(int)PopText.TrendLegendHint] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Trend.LegendHint");
            populationText[(int)PopText.TrendNoHistory] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Trend.NoHistory");
            // 量程文本由 Provider 在构建期拼好（与 `PerformanceTrendCapText` 同一范式：
            // 快照只暴露**已格式化**文本，UI 不做数值聚合 —— 08c DL 契约）。
            populationText[(int)PopText.TrendRange] =
                string.IsNullOrEmpty(snapshot.IdentityTrendRangeText)
                    ? string.Empty
                    : snapshot.IdentityTrendRangeText;
            // 悬停气泡：**逐段**在缓存期拼好（8 条，绘制期零拼接、零翻译）。
            EnsureIdentityTrendTips(snapshot);
            populationText[(int)PopText.DerivedGroupOutput] =
                Translate("PersonalChronicle.UI.MasterControl.Population.DerivedGroup.Output");
            populationText[(int)PopText.DerivedGroupSupport] =
                Translate("PersonalChronicle.UI.MasterControl.Population.DerivedGroup.Support");
            populationText[(int)PopText.DerivedGroupRisk] =
                Translate("PersonalChronicle.UI.MasterControl.Population.DerivedGroup.Risk");
            populationText[(int)PopText.DerivedGroupSkill] =
                Translate("PersonalChronicle.UI.MasterControl.Population.DerivedGroup.Skill");
            populationText[(int)PopText.NoLaborData] =
                Translate("PersonalChronicle.UI.MasterControl.Population.NoLaborData");
            populationText[(int)PopText.NoPerformanceData] =
                Translate("PersonalChronicle.UI.MasterControl.Population.NoPerformanceData");
            populationText[(int)PopText.PanelFactions] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Factions");
            populationText[(int)PopText.PanelPerformance] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Performance");
            populationText[(int)PopText.RowPerformanceTotal] =
                Translate("PersonalChronicle.UI.MasterControl.Population.PerformanceTotal");
            // ⑥ 绩效趋势卡（预览 HTML 941-965）：标题 / 空态 / 带参的范围行与 y 上界。
            populationText[(int)PopText.PerformanceTrendTitle] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Performance.TrendTitle");
            populationText[(int)PopText.PerformanceTrendNone] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Performance.TrendNone");
            populationText[(int)PopText.PerformanceScope] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Performance.Scope");
            populationText[(int)PopText.PerformanceSummary] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Performance.Summary");
            bool hasTrendAxis = snapshot.PerformanceTrendAxisLabels != null
                && snapshot.PerformanceTrendAxisLabels.Length > 0;
            populationText[(int)PopText.PerformanceTrendRange] = Translate2(
                "PersonalChronicle.UI.MasterControl.Population.Performance.TrendRange",
                (NamedArgument)(hasTrendAxis ? snapshot.PerformanceTrendAxisLabels[0] : Dash()),
                (NamedArgument)(hasTrendAxis
                    ? snapshot.PerformanceTrendAxisLabels[snapshot.PerformanceTrendAxisLabels.Length - 1]
                    : Dash()));
            populationText[(int)PopText.PerformanceTrendCap] = Translate(
                "PersonalChronicle.UI.MasterControl.Population.Performance.TrendCap",
                (NamedArgument)(string.IsNullOrEmpty(snapshot.PerformanceTrendCapText)
                    ? Dash()
                    : snapshot.PerformanceTrendCapText));
            // 合计行数值（「18.4K · 1,533 银 · 61.2%」）——在缓存重建期拼好，绘制期零拼接（PERF-001）。
            populationText[(int)PopText.PerformanceSummaryValue] = Translate3(
                "PersonalChronicle.UI.MasterControl.Population.Performance.SummaryValue",
                (NamedArgument)(string.IsNullOrEmpty(snapshot.PerformanceTotalText) ? Dash() : snapshot.PerformanceTotalText),
                (NamedArgument)(string.IsNullOrEmpty(snapshot.PerformancePerCapitaText) ? Dash() : snapshot.PerformancePerCapitaText),
                (NamedArgument)(string.IsNullOrEmpty(snapshot.PerformanceTop3ShareText) ? Dash() : snapshot.PerformanceTop3ShareText));
            // ③ 派系底部 drow / ② 信仰环副标 / ⑧ 专业分布补充行 / ⑥ 劳动力底部 drow（2026-09-15 审计补全）。
            populationText[(int)PopText.FactionTechSummary] =
                Translate("PersonalChronicle.UI.MasterControl.Population.FactionTechSummary");
            populationText[(int)PopText.IdeologyMembers] =
                Translate("PersonalChronicle.UI.MasterControl.Population.IdeologyMembers");
            populationText[(int)PopText.SpecializationUnset] =
                Translate("PersonalChronicle.UI.MasterControl.Population.SpecializationUnset");
            populationText[(int)PopText.SpecializationGap] =
                Translate("PersonalChronicle.UI.MasterControl.Population.SpecializationGap");
            populationText[(int)PopText.SpecializationNote] =
                Translate("PersonalChronicle.UI.MasterControl.Population.SpecializationNote");
            populationText[(int)PopText.LaborConcentration] =
                Translate("PersonalChronicle.UI.MasterControl.Population.LaborConcentration");
            // 带参项在缓存重建期拼好（绘制期零拼接）：「前 3 占 49.7% · 986.8 h/人」。
            populationText[(int)PopText.LaborConcentrationValue] = Translate2(
                "PersonalChronicle.UI.MasterControl.Population.LaborConcentrationValue",
                (NamedArgument)(string.IsNullOrEmpty(snapshot.LaborTopThreeText) ? Dash() : snapshot.LaborTopThreeText),
                (NamedArgument)(string.IsNullOrEmpty(snapshot.LaborPerCapitaText) ? Dash() : snapshot.LaborPerCapitaText));
            // 核心面板 B **详细列表表头**（RUIF StatTable）—— 缓存期翻译，绘制期只查表。
            // ⚠ 序号列已删（2026-09-17）⇒ 本槽不在表头使用；Keyed 与翻译**保留**（槽位契约 GOV-009）——
            //   若序号列回归，把 `CorePerfHeaders()` 的第 0 项加回即可。
            populationText[(int)PopText.CoreTableRank] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Table.Rank");
            populationText[(int)PopText.CoreTableName] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Table.Name");
            populationText[(int)PopText.CoreTableOutput] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Table.Output");
            populationText[(int)PopText.CoreTableHours] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Table.Hours");
            populationText[(int)PopText.CoreTableShare] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Table.Share");
            populationText[(int)PopText.CoreTableMedal] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Table.Medal");
            populationText[(int)PopText.CoreTableCount] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Table.Count");
            populationText[(int)PopText.CoreTableTitle] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Table.Title");
            populationText[(int)PopText.CoreTablePeople] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Table.People");
            populationText[(int)PopText.CoreTableJob] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Table.Job");
            populationText[(int)PopText.CoreTableAvgQuadrum] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Table.AvgQuadrum");
            populationText[(int)PopText.CoreTablePeakQuadrum] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Table.PeakQuadrum");
            populationText[(int)PopText.CoreTableQuadrumCount] =
                Translate("PersonalChronicle.UI.MasterControl.Population.Table.QuadrumCount");
            // 性比行两端读数（预览 HTML 764-766）：模板化 ⇒ 箭头与词序由语言文件决定（LOC-004）。
            populationText[(int)PopText.GenderRatioMale] = Translate2(
                "PersonalChronicle.UI.MasterControl.Population.GenderRatioMale",
                (NamedArgument)snapshot.MaleColonists,
                (NamedArgument)PercentText(snapshot.MaleShare01));
            populationText[(int)PopText.GenderRatioFemale] = Translate2(
                "PersonalChronicle.UI.MasterControl.Population.GenderRatioFemale",
                (NamedArgument)snapshot.FemaleColonists,
                (NamedArgument)PercentText(snapshot.FemaleShare01));
            populationText[(int)PopText.GenderRatioValue] = Translate(
                "PersonalChronicle.UI.MasterControl.Population.GenderRatioValue",
                (NamedArgument)GenderRatioText(snapshot));

            if (populationPanelHeights == null || populationPanelHeights.Length < PopulationPanelSlotCount)
            {
                populationPanelHeights = new float[PopulationPanelSlotCount];
            }
            PanelHeights(snapshot, populationPanelHeights);
        }

        /// <summary>
        /// 各面板内容高（纯函数：只数行数，不读中心名、不算布局）。
        /// 估算与绘制共用 ⇒ 不会漂移。
        /// </summary>
        private static void PanelHeights(PopulationOverviewSnapshot snapshot, float[] heights)
        {
            if (snapshot == null)
            {
                return;
            }
            // 【面板序 = 预览栅格序】（3 列 auto-fill，逐行填格）：
            //   行 1：① 规模与构成 · ② 人员绩效 · ③ 荣誉与勋章
            //   行 2：④ 派系与外交 · ⑤ 派生属性 · ⑥ 劳动力与工时
            //   行 3：⑦ 信仰与理念 · ⑧ 专业分布
            // ⚠ 顺序即契约：本表、PanelTitleKeyOf、DrawOverviewPanel 三处的索引必须同步改。
            // 0 规模与构成：**三列栅格**（预览 HTML 604 `repeat(3,minmax(0,1fr))`）——
            // 高度 = 三列中的**最高列**，而不是竖排各行累加（旧算法让面板高度近预览两倍）。
            //   列 1：现役行 + 身份 Donut 区块
            //   列 2：性别金字塔区块
            //   列 3：一行读数（综合能力均值 / 工效）+ 11 项能力条
            //   列 1 = 段标题（`.sec-h`）+ 身份 Donut 区块
            //   [接回预留] 2026-09-17 用户裁决移除「身份人数趋势卡」⇒ 计高去掉 `+ 4f + JoinTrendCardHeight`
            //   （接回：把该项加回，并恢复 `DrawIdentityRing` 尾部的 `DrawIdentityTrendCard` 调用）。
            float scaleCol1 = IdentityHeaderHeight + IdentityRingBlockHeight;
            float scaleCol2 = GenderPyramidBlockHeight;
            // 列 3 的能力条是**双列**（与绘制同源：half 行 × 行高）
            int capacityHalfRows = (snapshot.Capacities.Count + 1) / 2;
            // 列 3 = 段标题（「健康能力」）+ **一行读数** + 11 项能力条（双列）
            //   [接回预留] 原 `+ CapacityTrendCardHeight + 2f`（164px 趋势卡）已按用户裁决移除
            //   ⇒ 改为 `+ CapacitySummaryRowHeight`（接回：换回该项并去掉那两行读数）。
            float scaleCol3 = IdentityHeaderHeight + CapacitySummaryRowHeight
                + capacityHalfRows * CapacityRowHeight;
            // 【2026-09-15 层级重构】核心面板 A = 规模与构成（左 2fr）+ **信仰与理念**（右 1fr）
            // ⇒ 面板高必须取**两栏的较大者**（右栏块高见 `IdeologyBlockHeight`）。
            // ⚠ 面板高 = **内容高 + 标题条 + 上下内边距**。
            // 旧实现只 `+6f`（等于把面板当成纯内容区）⇒ `DrawScalePanel` 拿到的内容区比需要少
            // ~30px，实机表现为 ① 面板**底部能力条被裁掉半行**（用户截图确认）。其余面板用
            // `RowsHeight()`（含 Header + PadY×2）所以无此问题 —— 只有 ① 是手写公式，必须自带。
            float coreLeftHeight = Mathf.Max(scaleCol1, Mathf.Max(scaleCol2, scaleCol3));
            // 【v11】信仰（索引 6）已**拆回独立面板** ⇒ ① 不再与它取大，只算自身三列内容高。
            heights[0] = coreLeftHeight
                + PopulationPanelHeaderHeight + PopulationPanelPadY * 1.5f + 6f;
            // 1 **核心面板 B**（v7 · 2026-09-16）= **一张 8 列 `DataAtoms.StatTable`（5 数据行）+
            //   表底合计行**（预览 HTML 1004-1087）。v5 的趋势卡 / 左右分栏 / 右栏荣誉·劳动力
            //   **已从显示移除**（用户判定「完全没有实现基于预览图的 1:1 还原」），方法保留可接回
            //   （[接回预留 · 见本块 / DrawOverviewPanel case 1]）。
            //   ⚠ 公式与 `DrawPerformancePanel` **逐项同源**（LAND-002）：
            //     表高 = (表头 1 + 数据行 rows) × PerformanceTableRowHeight（= StatTable 的
            //     `(1 + rows) * rowH` 预留契约）；其后是合计行的间距 + 行高；再加面板标题条与内边距。
            //   无绩效数据时左栏画**结构占位**（表头 + 3 行「—」），与非空态同构：行高统一用
            //   `PerformanceTableRowHeight`（= StatTable 的 `(1 + rows) * rowH` 契约），窗体 chrome
            //   与绘制侧 `DrawPerformancePanel` 空态分支逐项同源（LAND-002）。空态版式预览未给
            //   （B 类差异登记）。
            if (snapshot.Performance.Count == 0)
            {
                heights[1] = (1 + 3) * PerformanceTableRowHeight
                    + PopulationPanelHeaderHeight + PopulationPanelPadY * 1.5f + 6f;
            }
            else
            {
                int perfRows = snapshot.Performance.Count;
                heights[1] = (1 + perfRows) * PerformanceTableRowHeight    // 表头 + 数据行
                    + PerformanceSummaryMarginTop + PerformanceSummaryRowHeight
                    + PopulationPanelHeaderHeight + PopulationPanelPadY * 1.5f + 6f;
            }
            // 2 荣誉与勋章（预览 ③）：已授予读数 + **一行 chips**（金/银/铜 + 职称前 3 档）+ 勋章榜（前 5）。
            //   ⚠ 2026-09-15 审计：预览 ③ **没有环形** ⇒ 已删除「勋章构成环形 + 3 行图例」
            //   （它与 chips 是同一组数据，实机两处重复）；职称档改由 chips 承载（不再逐行）。
            heights[2] = RowsHeight(1 + snapshot.MedalTop.Count
                    + (snapshot.MedalTop.Count > 0 ? 1 : 0))
                + PopulationChipHeight + PopulationChipGap;
            // 3 派系与外交：一行一家（预览 ④）+ 底部「科技层级分布」drow。
            //   ⚠ 2026-09-15：本面板**已移出栅格**（用户要求）——高度计算保留以维持槽位完整。
            heights[3] = RowsHeight(Mathf.Max(1, snapshot.Factions.Count)
                + (string.IsNullOrEmpty(snapshot.FactionTechSummaryText) ? 0 : 1));
            // 4 派生属性：**2×2 卡片网格**（预览 HTML 850 `repeat(2,1fr)`）——高度 = 两行卡片；
            //   无活体时 1 行说明。
            //   ⚠ 2026-09-15：本面板**已移出栅格**（用户要求）；此处保留计算只为「槽位完整」，
            //   恢复接回时无需改动本方法。
            heights[4] = snapshot.DerivedReady
                ? DerivedCardHeight * 2f + DerivedCardGap + 6f
                : RowsHeight(1);
            // 5 劳动力与工时：合计 / 人均 / 前三（3 行）+ 12 工种竖柱图（预览 ⑥）；
            //   无数据时画**结构占位**（读数「—」+ 空柱骨架 + 说明，与有数据的版式同高）
            //   有数据时末尾还有「前三工种占比」条（读数行 + 条）⇒ +1 行 + 条高
            //   有数据时末尾是**单行 drow**（集中度 · 人均）—— 旧实现是「读数行 + 3 段堆叠条」
            //   （预览无堆叠条）⇒ 等量替换为 1 行：3 读数 + 图 + 1 drow = 4 行。
            heights[5] = snapshot.Labor.Count == 0
                ? RowsHeight(2) + LaborChartBlockHeight
                : RowsHeight(4) + LaborChartBlockHeight;
            /// <summary>
            /// 信仰与理念**内容块**高（**不含**面板标题条与内边距）—— v11 起只是
            /// <see cref="IdeologyContentHeight"/> 的转发（保留本局部函数名，便于与历史版本的
            /// `heights[6]` 表达式比对）。
            /// </summary>
            float IdeologyBlockHeight(PopulationOverviewSnapshot snap)
            {
                return IdeologyContentHeight(snap);
            }

            // 6 ⑦ 信仰与理念：**独立面板**（v11 · 行 2 左栏 4 单位）——
            //   四段内容高由 `IdeologyContentHeight` 统一给出（与 `DrawIdeologyPanel` 同源）。
            //   ⚠ 2026-09-15 v11：信仰已从核心面板 A 的右栏**拆回独立面板** ⇒ 不再参与
            //     heights[0] 的「两栏取大」；公式口径与 ① 一致（Header + PadY×1.5 + 内容）。
            heights[6] = IdeologyBlockHeight(snapshot)
                + PopulationPanelHeaderHeight + PopulationPanelPadY * 1.5f + 6f;

            // 7 专业分布（预览 ⑧）：已设定 + 方向 chips + 技能梯队 4 档 + 方向缺口行 + 口径说明行。
            //   ⚠ 2026-09-15 审计：预览 ⑧ **没有环形** ⇒ 已删除「方向占比环形」（与 chips 同数据）。
            //   ⚠ 同日：本面板**已移出栅格**（用户要求）——高度计算保留以维持槽位完整。
            heights[7] = RowsHeight(1 + snapshot.SkillTiers.Count
                    + (string.IsNullOrEmpty(snapshot.SpecializationGapText) ? 0 : 1))
                + PopulationChipHeight + PopulationChipGap + PopulationRowHeight + 2f;
        }

        private static float RowsHeight(int rows)
        {
            return PopulationPanelHeaderHeight + PopulationPanelPadY * 2f + rows * PopulationRowHeight;
        }

        /// <summary>
        /// 核心面板 B **荣誉分区**内容高（并入的辅助面板 ③）：
        /// 已授予读数行 + [勋章榜详细列表] + [职称分布详细列表]。
        /// 集合为空时**不占高**（绘制侧同样不画该表）。
        ///
        /// <para><b>[接回契约]</b>（v7 起 `PanelHeights[1]` 不再调用，本方法保留）：
        /// ① 移除原因 —— 预览 v7 的 `.coreB` 是**一张 8 列表**，右栏荣誉/职称已并入（HTML 991-993）。
        /// ② 接回步骤 —— 单表形态下把徽章/职称 rows 拼进大表并令行数 5 → 30，**无需**本方法；
        /// 仅当恢复「右栏上下两分区」旧形态时，把本值作为右栏荣誉分区的高（配
        /// <c>CoreLaborBlockHeight</c> + <c>PopulationCoreSplitGap</c>）。
        /// ③ 依赖常量 —— <c>PopulationRowHeight</c> / <c>PopulationChipHeight</c> /
        /// <c>PopulationChipGap</c> / <c>PopulationTableRowHeight</c>。</para>
        /// </summary>
        private static float CoreHonorBlockHeight(PopulationOverviewSnapshot snapshot)
        {
            // 已授予 X / Y 读数行 + 金银铜 chips 行（一行、可换行停）。
            float height = PopulationRowHeight + PopulationChipHeight + PopulationChipGap;
            if (snapshot.MedalTop.Count > 0)
            {
                height += (1 + snapshot.MedalTop.Count) * PopulationTableRowHeight + 4f;
            }
            if (snapshot.Titles.Count > 0)
            {
                height += (1 + snapshot.Titles.Count) * PopulationTableRowHeight;
            }
            return height;
        }

        /// <summary>
        /// 核心面板 B **劳动力分区**内容高（并入的辅助面板 ⑤）：
        /// 合计 / 人均两行读数 + 工种详细列表（表头 + N 行）。
        ///
        /// <para><b>[接回契约]</b>（v7 起 `PanelHeights[1]` 不再调用，本方法保留）：
        /// ① 移除原因 —— 预览 v7 的 `.coreB` = 一张 8 列表，右栏「劳动力」已并入（HTML 991-993）。
        /// ② 接回步骤 —— 单表形态下把工种 rows 拼进大表并令行数 5 → 30，**无需**本方法；
        /// 仅当恢复「右栏上下两分区」旧形态时，把本值作为右栏劳动力分区的高。
        /// ③ 依赖常量 —— <c>PopulationRowHeight</c> / <c>PopulationTableRowHeight</c>。</para>
        /// </summary>
        private static float CoreLaborBlockHeight(PopulationOverviewSnapshot snapshot)
        {
            int rows = snapshot.Labor.Count > 0 ? snapshot.Labor.Count : 1;
            return PopulationRowHeight * 2f + (1 + rows) * PopulationTableRowHeight;
        }

        /// <summary>
        /// ⑦ 信仰与理念（**独立面板** · v11 结构）的**纯内容高** —— **不含**面板标题条与内边距
        /// （二者由 `heights[6] = 本值 + Header + PadY×1.5 + 6` 补，与 `DrawOverviewPanel` 里
        /// `contentHeight = heights[i] − Header − PadY×1.5` 的反算口径严格对应）。
        /// 与 <see cref="DrawIdeologyPanel"/> 逐段同源
        /// （LAND-002：两处不同源立刻表现为面板底部被裁或白高一截）。
        ///
        /// <para>四段：① 头部横条（环 <see cref="IdeologyRingSize"/> + 名称 / 主流占比读数）
        /// · ② 理念分布表（表头 + 理念行 + 可选「无信仰」行）· ③ 核心理念（标签 + chips 一行）
        /// · ④ 戒律表（表头 + N 行）。未启用资料片时只有「未启用」提示一行。</para>
        /// </summary>
        private static float IdeologyContentHeight(PopulationOverviewSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IdeologyEnabled)
            {
                return PopulationRowHeight;
            }
            // ⚠ 2026-09-17 **紧缩版式**（与 `DrawIdeologyPanel` 逐段同源 · LAND-002）：
            //   ① 核心理念 chips 已并入**头部横条右列第二行**（头部 88 由环决定 ⇒ 第二行零额外高度）
            //      ⇒ 原来独立的「gap + 标题行 + chips 行」**不再单独计高**；
            //   ② 理念分布表与戒律表**并排两列** ⇒ 表高由 `Σ(1+rows)` 变为 `max(1+rows)`。
            float height = IdeologyHeadHeight;
            int theoryRows = snapshot.Ideologies.Count
                + (snapshot.IdeologyNoneCount > 0 ? 1 : 0);
            int preceptRows = snapshot.Precepts.Count;
            if (theoryRows > 0 || preceptRows > 0)
            {
                int rows = Mathf.Max(theoryRows, preceptRows);
                height += IdeologyBlockGap + (1 + rows) * PopulationTableRowHeight;
            }
            if (snapshot.FailedSections > 0)
            {
                height += PopulationRowHeight;
            }
            return height;
        }

        /// <summary>面板高预算暂存：绘制与计高都在 IMGUI 主线程，静态复用避免每帧 new
        /// （按**槽位数**分配：移出栅格的面板仍需参与 `PanelHeights` 填充）。</summary>
        private static readonly float[] PopulationHeightScratch = new float[PopulationPanelSlotCount];

        /// <summary>区块总高（与 <see cref="DrawPopulationBlock"/> 同源）。</summary>
        private static float PopulationBlockHeight(float width, PopulationOverviewSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsEmpty)
            {
                return 0f;
            }
            // 复用暂存数组（IMGUI 单线程；绘制与计高同为主线程路径）——
            // 旧实现每帧 new float[8]，违反「绘制路径零分配」（PERF-001）。
            PanelHeights(snapshot, PopulationHeightScratch);
            float[] heights = PopulationHeightScratch;
            // 与 DrawPopulationBlock 的取值必须一致（名称册面板已由承载区的 12 列人物表取代，不再计高）。
            return PopulationTitleHeight + SectionGap * 0.5f
                + GridHeight(width, heights, PopulationPanelVisibleCount)
                + SectionGap;
        }

        /// <summary>
        /// 面板跨列单位（**12 单位制**，与预览 `grid-column:span N` 一一对应）。
        ///
        /// <para>2026-09-15 **核心面板合并（终版）**：
        /// **核心面板（各占整行 = 12）** —— 核心 A = ① 规模与构成（承载信仰右栏）·
        /// 核心 B = ② 人员绩效（承载荣誉 + 劳动力两个并入分区）。
        /// 显示序 = { ①12 · ②12 } ⇒ 两行各 12 单位、**满格**。</para>
        /// ⚠ 索引 2（荣誉）/ 5（劳动力）已**并入核心 B**；索引 3 / 4 / 6 / 7 已移出栅格 ——
        /// 全部仍在表中占位（可随时接回）。
        /// ⚠ 顺序即契约：PanelHeights 的面板序、PanelTitleKeyOf、DrawOverviewPanel 与本表必须同步。
        /// </summary>
        private static readonly int[] PanelSpanUnits = { 12, 8, 6, 4, 8, 6, 4, 4 };

        /// <summary>
        /// **栅格显示序 → 面板索引**（与预览 `#v-overview` 的**实际渲染序**一一对应）。
        /// 预览只有 ①② 带 `style="order:…"`（① `order:-2`、② `order:-1`，HTML 597/933），
        /// 其余按 DOM 序 = ⑦(753) ④(819) ⑤(841) ⑥(880) ③(908) ⑧(980)。
        ///
        /// <para>2026-09-15：④ 派生属性(4)、⑧ 专业分布(7)、③ 派系与外交(3) 依次移出栅格
        /// ⇒ 视觉栅格变为 行1 = ①(8) + ⑦(4)；行2 = ②(8) + ③(4)；行3 = ⑤(12 整行)（三行满格）。
        /// 图表承载块（绩效趋势图 / 12 工种柱图）拿到最大宽度 —— 用户诉求「放大趋势图组件」。</para>
        /// ⚠ 面板**身份索引不变**（PanelHeights / PanelTitleKeyOf / DrawOverviewPanel 仍按本数组取）。
        /// </summary>
        private static readonly int[] PanelDisplayOrder = { 0, 6, 1 };

        /// <summary>栅格总单位（预览 12 列栅格）。</summary>
        private const int GridTotalUnits = 12;

        /// <summary>
        /// 某面板在当前宽度档下的跨列单位 —— 与预览媒体查询一一对应：
        ///   ≥ 1320px → 12 单位制（`s8` = 8 单位）
        ///   ≤ 1320px → 每面板 **6 单位**（预览 `@media(max-width:1320px){.s4{span 6}}`，2 列）
        ///   ≤  900px → 每面板 **12 单位**（1 列）
        /// </summary>
        private static int PanelUnitsAt(int index, float width)
        {
            if (width < PopulationTwoColumnWidth)
            {
                return GridTotalUnits;
            }
            if (width < PopulationThreeColumnWidth)
            {
                // **窄档覆盖**（对齐预览 v11 的 `.coreB` 规则）：**绩效恒整行** —— 它的 8 列表
                // 在半宽下会挤爆（列宽 `minmax` 下限合计约 730px）⇒ 规模 / 信仰各占半行并排。
                return index == 1 ? GridTotalUnits : GridTotalUnits / 2;
            }
            return PanelSpanUnits[index];
        }

        // 栅格装箱结果暂存（IMGUI 主线程；计高与绘制顺序共用，避免每帧分配）。
        private static readonly int[] PopulationRowOf = new int[PopulationPanelSlotCount];
        private static readonly int[] PopulationRowFirst = new int[PopulationPanelSlotCount + 1];
        private static readonly int[] PopulationRowCount = new int[PopulationPanelSlotCount + 1];

        /// <summary>
        /// 栅格装箱：按面板序与各自跨列单位**贪心填行**（12 单位/行）。
        /// **绘制与计高必须共用本函数** —— 两处各写一套排布必然漂移（LAND-002）。
        /// 返回行数，并把「每行首面板索引 / 每行面板数 / 每面板所在行」写入暂存数组。
        /// </summary>
        private static int PackGrid(float width)
        {
            int row = 0;
            int used = 0;
            PopulationRowFirst[0] = 0;
            for (int n = 0; n < PopulationPanelVisibleCount; n++)
            {
                // 按**显示位**迭代，跨列单位取该显示位对应的面板索引（① 恒在首位）。
                int units = PanelUnitsAt(PanelDisplayOrder[n], width);
                if (used > 0 && used + units > GridTotalUnits)
                {
                    PopulationRowCount[row] = n - PopulationRowFirst[row];
                    row++;
                    PopulationRowFirst[row] = n;
                    used = 0;
                }
                PopulationRowOf[n] = row;
                used += units;
            }
            PopulationRowCount[row] = PopulationPanelVisibleCount - PopulationRowFirst[row];
            return row + 1;
        }

        private static float GridHeight(float width, float[] heights, int count)
        {
            int rows = PackGrid(width);
            float total = 0f;
            for (int r = 0; r < rows; r++)
            {
                float max = 0f;
                int first = PopulationRowFirst[r];
                int last = Mathf.Min(count, first + PopulationRowCount[r]);
                for (int n = first; n < last; n++)
                {
                    // ⚠ **显示位 → 面板索引**：早期实现直接写 `heights[n]`（把显示位当身份索引），
                    //    而 `PanelDisplayOrder` 非恒等 ⇒ 取错行高 ⇒ **计高与绘制不同源**
                    //    （症状：区块总高偏差、滚动条多半截）。绘制侧用的是 `heights[index]`。
                    float h = heights[PanelDisplayOrder[n]];
                    if (h > max)
                    {
                        max = h;
                    }
                }
                total += max;
            }
            return total + (rows - 1) * PopulationPanelGap;
        }

        /// <summary>绘制人员概览区块；返回下一个可用的 y。</summary>
        private float DrawPopulationBlock(Rect rect, float y, PopulationOverviewSnapshot snapshot)
        {
            if (snapshot == null || snapshot.IsEmpty)
            {
                return y;
            }
            EnsurePopulationDrawCache(snapshot, rect.width);

            // ⛔ 区块工具条删除（CHG-20260914-002）：预览 #v-overview 的面板是**直接排布**的，
            //    没有「人员概览」标题条；实机判定为多余外壳。PopulationTitleHeight 恒 0，
            //    高度估算与绘制仍共用同一符号（两处不同源必然表现为滚动异常）。
            y += PopulationTitleHeight + SectionGap * 0.5f;
            y = DrawPanelGrid(rect, y, snapshot, populationPanelHeights);
            return y + SectionGap;
        }

        private float DrawPanelGrid(Rect rect, float y, PopulationOverviewSnapshot snapshot, float[] heights)
        {
            // 【12 单位栅格】① `span 8` + 其余 `span 4`（预览 `.panel.s8/.s4`）——
            // 旧实现是「等宽 3/2/1 列」，① 只能占 1/3 宽，与预览的 2/3 宽差一倍。
            // 单位宽 = (行宽 − 间隙) / 本行单位总和（预览用 `1fr`，行内拉伸铺满）。
            int rows = PackGrid(rect.width);
            float cursor = y;
            for (int r = 0; r < rows; r++)
            {
                int first = PopulationRowFirst[r];
                int count = PopulationRowCount[r];
                float rowHeight = 0f;
                int unitsInRow = 0;
                for (int n = first; n < first + count; n++)
                {
                    int index = PanelDisplayOrder[n];
                    if (heights[index] > rowHeight)
                    {
                        rowHeight = heights[index];
                    }
                    unitsInRow += PanelUnitsAt(index, rect.width);
                }
                float unitWidth = unitsInRow > 0
                    ? (rect.width - (count - 1) * PopulationPanelGap) / unitsInRow
                    : rect.width;
                float x = rect.x;
                for (int n = first; n < first + count; n++)
                {
                    int index = PanelDisplayOrder[n];
                    float cardWidth = unitWidth * PanelUnitsAt(index, rect.width);
                    DrawOverviewPanel(new Rect(x, cursor, cardWidth, rowHeight), index, snapshot);
                    x += cardWidth + PopulationPanelGap;
                }
                cursor += rowHeight + PopulationPanelGap;
            }
            return cursor - PopulationPanelGap;
        }

        /// <summary>
        /// ① 面板标题条内的**视图 tab 条**（2026-09-17 用户需求：「这部分 tab 嵌入到『人员规模』
        /// 面板内，节省组件空间」）。
        ///
        /// <para>几何：右对齐画在标题条（高 26）内、tab 高 24 ⇒ 垂直居中（上下各 1px）——
        /// <b>零新增高度</b>（标题条本来就存在且右侧空着）；页头那条水平带因此完全归还给标题 / 副标题。
        /// 返回**保留宽**（预算 + 呼吸间距），由调用方从标题可用宽中扣减（UI-009：长标题既不压控件、
        /// 也不与 tab 重叠）。</para>
        ///
        /// <para>绘制与命中检测**复用页头同款** <see cref="DrawPopulationViewTabCapsules"/>
        /// （可用性判据 / 点击切换 / 选中态强调条 / 悬停高亮一字不改）。降级只走到 D2（只留首字）：
        /// 面板标题条**绝不**回落内容区行（D6 在此无意义 —— 面板本身就是托管方）。</para>
        /// </summary>
        private float DrawScalePanelViewTabs(Rect band, PopulationOverviewSnapshot snapshot)
        {
            SystemCenterDetailSnapshot detail = cachedDetail;
            bool hasPopulation = snapshot != null && !snapshot.IsEmpty;
            ArchiveViewSnapshot archive = detail != null ? ArchiveViewForDisplay(detail) : null;
            bool hasArchive = archive != null && !archive.IsEmpty;
            float rowY = band.y + (PopulationPanelHeaderHeight - PopulationViewHeaderTabHeight) * 0.5f;
            float rightEdge = band.xMax - PopulationPanelPadX;
            // ① dev「测试数据」开关（仅 DevMode；非 DevMode 连按钮都不画、返回原右界）——
            //    与用户截图同序：**最右是 dev 开关，其左才是 4 枚视图 tab**。
            //    先落位再排 tab ⇒ 无需复制它的宽度公式（宽度由它自己决定，LAND-007）。
            float afterDev = DrawDevToggleButton(rowY, PopulationViewHeaderTabHeight,
                PopulationViewHeaderTabGap, rightEdge);
            float barRight = afterDev < rightEdge ? afterDev : rightEdge;
            float gaps = PopulationViewTabGap * (PopulationViewCount - 1);
            float budget = Mathf.Min(band.width * 0.62f,
                PopulationViewTabMaxWidth * PopulationViewCount + gaps);
            // 防御分支（正常不可达：页头判据 `PopulationViewTabsHostedByScalePanel` 已含同一预算式）。
            if (budget - gaps < PopulationViewTabMinWidth * PopulationViewCount)
            {
                return rightEdge - barRight + PopulationViewHeaderTabGap;
            }
            float tabWidth = (budget - gaps) / PopulationViewCount;
            int level = tabWidth >= WidestTabLabelWidth() + 8f
                ? TabLabelFull
                : (tabWidth >= PopulationViewTabMinWidth * 2f ? TabLabelTruncated : TabLabelInitial);
            Rect bar = new Rect(barRight - budget, rowY, budget, PopulationViewHeaderTabHeight);
            DrawPopulationViewTabCapsules(bar, tabWidth, hasPopulation, hasArchive, level);
            return rightEdge - bar.x + PopulationViewHeaderTabGap;
        }

        private void DrawOverviewPanel(Rect rect, int index, PopulationOverviewSnapshot snapshot)
        {
            UIComponents.Panel(rect);
            // 【预览 `.panel::before/::after`（HTML 151-156）】四角 L 形饰线（10×10、2px、accent 58%）
            // —— 预览面板最具辨识度的「精致感」来源；IMGUI 无伪元素 ⇒ 自绘（左上 + 右下）。
            DrawPanelCornerAccents(rect);
            float padX = PopulationPanelPadX;
            // ① 面板在概览视图里托管视图 tab 条（见 DrawScalePanelViewTabs；由页头置标志、同源 LAND-002）。
            float tabReserve = index == 0 && PopulationViewTabsInScalePanel
                ? DrawScalePanelViewTabs(new Rect(rect.x, rect.y, rect.width, PopulationPanelHeaderHeight), snapshot)
                : 0f;
            float titleWidth = Mathf.Max(0f, rect.width - padX * 2f - tabReserve);
            UIComponents.Label(new Rect(rect.x + padX, rect.y + 3f, titleWidth,
                    PopulationPanelHeaderHeight - 4f),
                UIComponents.TruncateToWidth(PopTextOf(PanelTitleKeyOf(index)), titleWidth, GameFont.Small),
                GameFont.Small, UITheme.Text, TextAnchor.MiddleLeft);
            UIComponents.Rule(new Rect(rect.x, rect.y + PopulationPanelHeaderHeight, rect.width,
                UITheme.RuleHeight), UITheme.BorderSoft);

            float rowY = rect.y + PopulationPanelHeaderHeight + PopulationPanelPadY * 0.5f;
            float rowWidth = Mathf.Max(0f, rect.width - padX * 2f);
            // 【关键】面板方法拿到的是**整块内容高**的矩形（供面板内部栅格用：① 三列 / ⑦ 两列 /
            // ⑤ 卡片都要按块高布局），而不是一行高 —— 旧实现传一行高（18px）导致：
            //   ⑦ 信仰的环形因「高度 < 24」被整体跳过、① 的环形被压缩到 24px（用户报「参数大小」问题）。
            // 行级绘制由 `At()` 固定行高，不受此影响。
            float contentHeight = Mathf.Max(PopulationRowHeight,
                populationPanelHeights[index] - PopulationPanelHeaderHeight - PopulationPanelPadY * 1.5f);
            Rect rowRect = new Rect(rect.x + padX, rowY, rowWidth, contentHeight);
            switch (index)
            {
                // 与 PanelHeights / PanelTitleKeyOf 的索引契约一致（预览栅格序）
                case 0:
                {
                    // ══ ① 规模与构成（**整行 12 单位**）══
                    // 2026-09-15 v11：信仰（索引 6）**拆回独立面板**（行 2 左栏）⇒ 本面板独占整行、
                    // 三列拿到更大列宽（用户诉求「上方面板可以拉升扩大」）。
                    DrawScalePanel(rowRect, snapshot);
                    break;
                }
                case 6:
                {
                    // ══ ⑦ 信仰与理念（独立面板 · 行 2 左栏 4 单位）══
                    // 2026-09-15 v11：从核心面板 A 的右栏**拆回**；内部结构见 `DrawIdeologyPanel`
                    // （头部横条 + 理念分布表 + 核心理念 chips + 戒律表，与预览 v11 同源）。
                    DrawIdeologyPanel(rowRect, snapshot);
                    break;
                }
                case 1:
                {
                    // ══ 核心面板 B（v7 · 2026-09-16）= **整宽单栏：一张 8 列统计表 + 表底合计行** ══
                    // 预览 v5→v7 把「绩效 / 荣誉 / 劳动力」合并为**一张表**（HTML 1024-1027），
                    // 用户判定旧的四块拼盘「完全没有实现基于预览图的 1:1 还原」⇒ 左右分栏 / 栏间竖线 /
                    // 右栏两分区**全部移除**（`DrawHonorPanel` / `DrawLaborPanel` 保留可接回：
                    // [接回预留 · 见 PanelHeights[1] / 本 case]）。
                    // 行缓冲按快照数据版本重建（帧内幂等）⇒ 绘制期零分配（PERF-001）。
                    EnsureCoreTableRows(snapshot);
                    // 【**置顶对齐**】（2026-09-17 用户报「人员绩效面板的列表没有置顶对齐」）：
                    //   旧实现照预览 `.pbody.tight{justify-content:center}`（HTML 1016 内联）做**垂直居中**
                    //   —— 但同行的 ⑦ 信仰面板更高时，本表悬在半空（实机截图：表上下各留一大块空，
                    //   且与左栏表格的行基线错开，两栏读数一眼对不上）。改为**从内容区顶边起画**。
                    //   ⚠ 预览该条注记随之失效 ⇒ 预览侧登记为「有意偏离（置顶对齐）」。
                    DrawPerformancePanel(rowRect, snapshot);
                    break;
                }
                // case 2（③ 荣誉）与 case 5（⑤ 劳动力）**已并入核心面板 B 的右栏**（2026-09-15）；
                // case 3（派系）· case 4（派生）· case 6（⑥ 信仰，已并入核心 A）· case 7（专业）
                // 均已移出栅格。绘制方法与数据采集**全部保留**：接回只需把索引加回
                // PanelDisplayOrder，并保证每行跨列合计 12 单位（索引即契约，GOV-009）。
            }
        }

        /// <summary>面板序号 → 缓存文案槽（绘制期只查表，不翻译）。</summary>
        private static PopText PanelTitleKeyOf(int index)
        {
            switch (index)
            {
                // 与 PanelHeights / DrawOverviewPanel 的索引契约一致（预览栅格序）
                case 0: return PopText.PanelScale;
                case 1: return PopText.PanelPerformance;
                case 2: return PopText.PanelHonors;
                case 3: return PopText.PanelFactions;
                case 4: return PopText.PanelDerived;
                case 5: return PopText.PanelLabor;
                case 6: return PopText.PanelIdeology;
                default: return PopText.PanelSpec;
            }
        }
    }
}

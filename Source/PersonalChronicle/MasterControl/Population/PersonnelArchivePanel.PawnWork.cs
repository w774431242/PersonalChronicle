using PersonalChronicle.Archive;
using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;
using ReadModels = PersonalChronicle.Application.ReadModels;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// Partial of PersonnelArchivePanel — 职业分区的绘制（生产清单、劳动强度、工时、趋势）。
    /// </summary>
    /// <remarks>
    /// <para><b>v5.x 生涯子页重写（对照清单 t2，预览 HTML 1561-1695）</b>：整页 <c>1fr 1fr</c> 双栏 ——
    /// 左栏 = 生涯摘要（hero + 3 格 + 档位 chip）→ 工作累计工时（<c>.bars</c> 行）→ 工时投入趋势；
    /// 右栏 = 生产（2 格 + 物品格）→ 职业事实计数（2×5）→ 累计产出价值趋势。
    /// 双栏骨架照抄 <c>PawnDetail.DrawPawnOverview</c>（两栏各自按内容高度增长）。</para>
    ///
    /// <para><b>已登记的数据差异</b>：工程只有 <c>Domain/WorkTimeAccumulator.WorkTimeHoursByQuadrum</c>
    /// 的 8 季桶（经 <c>ProductionSummaryView.QuadrumWorkHours</c> 暴露），<b>没有周粒度序列</b> ⇒
    /// 「工时投入趋势」按 8 季桶画柱图，并在图下用 <c>Trend.WorkCaption</c> 如实声明「近 8 季（周粒度未采集）」。
    /// 绝不编造周数据（LAND-009）。</para>
    ///
    /// <para><b>数据来源</b>：全部走窗口既有缓存 —— <c>cachedWorkIntensity</c> / <c>cachedTiers</c> /
    /// <c>cachedIntensityWorkTypes</c> / <c>cachedProductionSummary</c> / <c>cachedDetailObject</c>，
    /// 绘制路径零查询、零遍历、零 new（趋势序列与事实计数在一次性 latch 里算好）。</para>
    ///
    /// <para><b>铁律（绘制态配对，勿改）</b>：改动 <c>GUI.color</c> / <c>Text.Font</c> / <c>Text.Anchor</c>
    /// 的方法必须在进入时保存旧值、在 <c>finally</c> 中原样恢复（否则污染后续分区）。</para>
    /// </remarks>
    public sealed partial class PersonnelArchivePanel
    {
        // ═══════════════ 生涯子页（t2）度量常量 —— 全部等于字体行高或预览固定值 ═══════════════
        /// <summary>生涯摘要 hero 高（预览 :1565，单行「档位徽章 + 强度 + 相对负荷」）。</summary>
        private const float CareerHeroHeight = 40f;
        /// <summary>生涯摘要 hero 下方的 3 格指标卡高（预览 :1573，与旧实现一致）。</summary>
        private const float CareerMetricCardHeight = 84f;
        /// <summary>档位阶梯 chip 高（预览 :1580-1583 + CSS :292-293 ⇒ Tiny 行高 18）。</summary>
        private const float CareerChipHeight = 18f;
        /// <summary>
        /// 工作累计工时节 <c>.bars</c> 行高 —— **28**（预览 CSS `.bars{grid-auto-rows:28px}`，
        /// 1920 档实测 `gridAutoRows = "28px"`；旧值 22 比预览矮 6px/行）。
        /// ⚠ 单点定义、双消费者：`PawnWork.cs` 绘制 + `Metrics.cs:61` 高度预算**共用本常量**
        /// （改这里两处自动同源，无需另改预算公式）。
        /// </summary>
        private const float CareerBarRowHeight = 28f;
        /// <summary>趋势 plot 高（预览 :1608 / :1676 ⇒ 52）。</summary>
        private const float CareerTrendPlotHeight = 52f;
        /// <summary>Tiny 行高（= 字体度量 18；职业事实计数表行高、口径提示行高、排名列高）。</summary>
        private const float CareerTinyLineHeight = 18f;
        /// <summary>生产物品格高 / 行距（预览 .lg-cell ⇒ Tiny 标签 + Small 数值）。</summary>
        private const float CareerItemCellHeight = 54f;
        private const float CareerItemCellPitch = 60f;
        /// <summary>工作累计工时列宽：左标签 118 / 中条 1fr / 右值 74 / 右排名 92（预览 :1588）。</summary>
        private const float CareerBarLabelWidth = 118f;
        private const float CareerBarValueWidth = 74f;
        private const float CareerBarRankWidth = 92f;
        /// <summary>工作累计工时最多绘制行数（预览示例 5 行）。</summary>
        private const int CareerBarRowLimit = 5;
        /// <summary>职业事实计数：左列 5 项（生产类）/ 右列 4 项（资格·荣誉类），共 9 类。</summary>
        private const int CareerFactLeftCount = 5;
        private const int CareerFactCount = 9;

        /// <summary>职业事实的 9 个翻译键（顺序 = 快照字段顺序；绘制路径只消费已缓存的 <c>cachedFactLabels</c>）。</summary>
        private static readonly string[] CareerFactKeys =
        {
            "PersonalChronicle.UI.Career.Fact.WorkCompleted",
            "PersonalChronicle.UI.Career.Fact.ItemProduced",
            "PersonalChronicle.UI.Career.Fact.ConstructionCompleted",
            "PersonalChronicle.UI.Career.Fact.ResearchCompleted",
            "PersonalChronicle.UI.Career.Fact.BookProduced",
            "PersonalChronicle.UI.Career.Fact.ExamPassed",
            "PersonalChronicle.UI.Career.Fact.ThesisDefended",
            "PersonalChronicle.UI.Career.Fact.TitleGranted",
            "PersonalChronicle.UI.Career.Fact.MedalGranted",
        };

        /// <summary>
        /// 事实计数取值（**读模型快照**，顺序严格对应 <see cref="CareerFactKeys"/>）。
        /// <para>消费窗口镜像 <c>cachedFactCounts</c>（= <c>DetailSnapshot.FactCounts</c>，
        /// 由 <c>ArchiveUiDataProvider.BuildCareerFactCounts</c> 从 <c>RecordCountByType</c> 聚合）。
        /// ⛔ 不得改回直查 <c>CareerData.RecordCountByType</c>：UI 只消费读模型，
        /// 禁止绘制路径直查 Domain（见 <c>ArchiveUiDataProvider.Career.cs</c> 的口径说明）。</para>
        /// </summary>
        private static int FactCountAt(ReadModels.CareerFactCounts counts, int index)
        {
            if (counts == null)
            {
                return 0;
            }
            switch (index)
            {
                case 0: return counts.WorkCompleted;
                case 1: return counts.ItemProduced;
                case 2: return counts.ConstructionCompleted;
                case 3: return counts.ResearchCompleted;
                case 4: return counts.BookProduced;
                case 5: return counts.ExamPassed;
                case 6: return counts.ThesisDefended;
                case 7: return counts.TitleGranted;
                default: return counts.MedalGranted;
            }
        }

        // ═══════════════ 生涯子页一次性 latch 缓存（绘制路径零查询/零遍历/零 new）═══════════════
        /// <summary>事实计数 latch 守卫：数据修订号 + 明细对象 id（任一变化才重算 9 次字典读取）。</summary>
        private long careerFactRevision = -1L;
        private string careerFactOwnerId;
        /// <summary>已翻译的事实名（顺序同 <see cref="CareerFactKeys"/>）。</summary>
        private readonly string[] cachedFactLabels = new string[CareerFactCount];
        /// <summary>事实计数（顺序同 <see cref="CareerFactKeys"/>）。</summary>
        private readonly int[] cachedFactValues = new int[CareerFactCount];
        /// <summary>趋势 latch 守卫：<c>cachedProductionSummary</c> 的引用快照（每次重建都是新实例）。</summary>
        private object cachedProductionSummaryRef;
        /// <summary>累计产出价值序列（8 季逐季累加，面积图数据源）。</summary>
        private readonly float[] cachedProductCumulative = new float[QuadrumBuckets.Slots];
        /// <summary>工时投入序列（8 季桶，柱图数据源）。</summary>
        private readonly float[] cachedWorkHoursBuckets = new float[QuadrumBuckets.Slots];
        private float cachedProductTotal;
        private float cachedProductSlope;
        private bool cachedProductHasData;
        private float cachedWorkMean;
        private float cachedWorkPeak;
        private float cachedWorkValley;
        private bool cachedWorkHasData;

        /// <summary>
        /// 绘制生产清单：三列表头加逐行产物记录，行可点击跳转到事物详情。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="service">归档服务，用于行点击后的导航。</param>
        /// <remarks>
        /// 列宽用 Mathf.Max(0f, ...) 做窄面板保护：总宽减去固定列宽后可能变负，负宽会让 Unity 抛异常。
        /// 右侧两列只在行宽不小于 280 时才绘制，窄面板下自动省略而不是挤压变形。
        /// </remarks>
        private void DrawProductionTab(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Tab.Production".Translate().ToString());

            if (cachedProductionLines.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoProduction".Translate().ToString());
                return;
            }

            // Column headers (all translated; no hardcoded copy).
            // 窄面板保护：固定列宽减法可能产生负宽，列宽统一 clamp 到 ≥0。
            Text.Font = GameFont.Tiny;
            GUI.color = UITheme.SecondaryText;
            float typeColW = Mathf.Max(0f, rect.width - 300f);
            Widgets.Label(new Rect(rect.x + 6f, y, typeColW, 18f),
                "PersonalChronicle.UI.ProductionType".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width - 280f, y, 90f, 18f),
                "PersonalChronicle.UI.ProductionCount".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width - 190f, y, 180f, 18f),
                "PersonalChronicle.UI.ProductionLastTime".Translate().ToString());
            GUI.color = prevColor;
            y += 22f;

            for (int i = 0; i < cachedProductionLines.Count; i++)
            {
                ReadModels.ProductionLineView line = cachedProductionLines[i];
                Rect row = new Rect(rect.x, y, rect.width, RowHeight - 4f);

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + 6f, row.y + 4f, typeColW, 22f), line.Label);

                if (row.width >= 280f)
                {
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(row.x + row.width - 280f, row.y + 6f, 90f, 18f), line.Count.ToString());
                    Widgets.Label(new Rect(row.x + row.width - 190f, row.y + 6f, 180f, 18f), FormatDate(line.LastTick));
                }

                // Click → jump to the thing's detail (Weapon/Thing nav target).
                if (Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, NavTarget.Weapon, line.StableId, null);
                }
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += RowHeight + 2f;
            }
            }
            finally
            {
                // v4.17 体检：恢复字体（旧实现空态/行循环后 Text.Font 停留在 Small/Tiny）。
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        /// <summary>
        /// 生涯子页（t2）：整页 <c>1fr 1fr</c> 双栏调度 —— 左 3 组 / 右 3 组（预览 :1561-1695）。
        /// </summary>
        /// <param name="rect">滚动内容区（高由 <c>ComputeDetailPanelHeight</c> 预算，两栏共用）。</param>
        /// <param name="service">归档服务（仅用于强度阶梯的服务能力判定）。</param>
        /// <remarks>
        /// 双栏骨架照抄 <c>PawnDetail.DrawPawnOverview</c>：<c>colW = Mathf.Max(160f, (w - GridGap) * 0.5f)</c>，
        /// 两栏各自从自己的 y 起画，区域高取两栏较大者（本页双栏之后无整宽段，故无需回写）。
        /// latch 缓存必须在任何绘制之前准备好（绘制路径零查询）。
        /// </remarks>
        private void DrawCareerTab(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                // 一次性 latch：事实计数（rev + 对象变化才重算）与趋势序列（快照引用变化才重算）。
                EnsureCareerFactRows(service);
                EnsureCareerTrendSeries();

                float colGap = UITheme.GridGap;
                float colW = Mathf.Max(160f, (rect.width - colGap) * 0.5f);
                Rect leftCol = new Rect(rect.x, rect.y, colW, rect.height);
                Rect rightCol = new Rect(rect.x + colW + colGap, rect.y,
                    Mathf.Max(160f, rect.width - colW - colGap), rect.height);

                DrawCareerLeftColumn(leftCol, service);
                DrawCareerRightColumn(rightCol);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        /// <summary>左栏：生涯摘要 → 工作累计工时 → 工时投入趋势（预览 :1563-1628）。</summary>
        private void DrawCareerLeftColumn(Rect col, IArchiveService service)
        {
            float y = col.y;
            DrawWorkIntensityHeader(col, ref y, service);
            y += UITheme.BlockGap;
            DrawWorkTimeBars(col, ref y);
            y += UITheme.BlockGap;
            DrawWorkTrendGroup(col, ref y);
        }

        /// <summary>右栏：生产 → 职业事实计数 → 累计产出价值趋势（预览 :1631-1694）。</summary>
        private void DrawCareerRightColumn(Rect col)
        {
            float y = col.y;
            DrawProductionGroup(col, ref y);
            y += UITheme.BlockGap;
            DrawCareerFactGroup(col, ref y);
            y += UITheme.BlockGap;
            DrawProductionTrendGroup(col, ref y);
        }

        // ═══════════════════ 左栏 ①：生涯摘要（标题 + hero + 3 格 + 档位 chip）═══════════════════

        /// <summary>
        /// 绘制生涯摘要：分区标题、40 高 hero、其下整栏宽 3 格指标卡、档位阶梯 chip。
        /// </summary>
        /// <param name="rect">栏目区域。</param>
        /// <param name="y">传入并回写当前绘制纵坐标。</param>
        /// <param name="service">归档服务；未实现 <see cref="IWorkIntensityService"/> 时阶梯画「采样中」。</param>
        /// <remarks>预览 :1563-1584。3 格指标卡自 hero 内**移出**（旧实现塞在 hero 里导致 hero 高 92f）。</remarks>
        private void DrawWorkIntensityHeader(Rect rect, ref float y, IArchiveService service)
        {
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.CareerSummary".Translate().ToString());

            DrawWorkIntensityHero(rect, y, rect.width);
            y += CareerHeroHeight + UITheme.SpaceXs;

            DrawWorkIntensityMetricCards(rect, y, rect.width);
            y += CareerMetricCardHeight + UITheme.SpaceXs;

            DrawIntensityTierLadder(rect, ref y, service);
        }

        /// <summary>
        /// 劳动强度 hero（预览 :1565-1572，高 40）：左档位徽章 + 中「强度一行」+ 右对齐「相对负荷」。
        /// </summary>
        /// <param name="rect">栏目区域。</param>
        /// <param name="y">hero 顶部纵坐标。</param>
        /// <param name="width">hero 宽度。</param>
        /// <remarks>
        /// 无档位（样本不足）时徽章与正文都退化为「采样中」，并补「已观测 N 天」——仍如实说明，不画假读数。
        /// 右对齐徽章先按 Tiny 量宽再右锚，窄栏下按 hero 半宽封顶，避免与正文重叠。
        /// </remarks>
        private void DrawWorkIntensityHero(Rect rect, float y, float width)
        {
            WorkIntensityView intensity = cachedWorkIntensity;
            bool hasTier = intensity != null && intensity.IsDefined;
            Color tierColor = ParseIntensityColor(intensity != null ? intensity.ColorHex : null);

            Rect hero = new Rect(rect.x, y, width, CareerHeroHeight);
            ArchiveUiStyle.DrawPanel(hero, ArchiveUiStyle.PanelRaised);

            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                // 左：档位徽章（药丸 chip）。
                string badgeCode = hasTier
                    ? TranslateIntensityKey(intensity.DisplayCode,
                        "PersonalChronicle.UI.NotAvailable".Translate().ToString())
                    : "PersonalChronicle.UI.Intensity.SampleInsufficient".Translate().ToString();
                Rect badge = new Rect(hero.x + UITheme.SpaceXs, hero.y + 11f, 78f, CareerChipHeight);
                UIComponents.Pill(badge, badgeCode, hasTier ? tierColor : ArchiveUiStyle.Muted);

                // 右：相对负荷徽章 —— 先量宽再右锚。
                string relative = BuildIntensityRelativeLabel(intensity);
                bool relativeDefined = intensity != null && intensity.RelativeRatio > 0d;
                Text.Font = GameFont.Tiny;
                float relW = relativeDefined
                    ? Mathf.Min(Text.CalcSize(relative).x + 16f, Mathf.Max(40f, hero.width * 0.5f))
                    : 0f;
                float textX = badge.xMax + UITheme.SpaceXs;
                float textRight = hero.xMax - UITheme.SpaceXs;
                if (relW > 0f)
                {
                    Rect relBadge = new Rect(textRight - relW, hero.y + 11f, relW, CareerChipHeight);
                    UIComponents.Pill(relBadge, relative, ArchiveUiStyle.Alive);
                    textRight = relBadge.x - UITheme.SpaceXs;
                }

                // 中：强度一行（档位名 · 日均工时 · 实测/估算；无档位时退化为采样中 + 观测窗口）。
                string statusKey = (hasTier && intensity.IsEstimated)
                    ? "PersonalChronicle.UI.Intensity.Estimated"
                    : "PersonalChronicle.UI.Intensity.Actual";
                string strength = hasTier
                    ? TranslateIntensityKey(intensity.LabelKey, intensity.DisplayCode)
                        + " · " + FormatHours(intensity.DailyHours)
                        + " · " + statusKey.Translate().ToString()
                    : "PersonalChronicle.UI.Intensity.SampleInsufficient".Translate().ToString()
                        + " · " + "PersonalChronicle.UI.Intensity.ObservedWindow".Translate(
                            FormatDays(intensity != null ? intensity.ObservedDays : 0d)).ToString();
                UIComponents.Label(new Rect(textX, hero.y + 9f, Mathf.Max(0f, textRight - textX), 22f),
                    strength, GameFont.Small, ArchiveUiStyle.Text, TextAnchor.MiddleLeft);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        /// <summary>
        /// 生涯摘要下方整栏宽 3 格指标卡（预览 :1573-1577）：累计工时 / 日均工时 / 周度估算。
        /// </summary>
        /// <remarks>数值用 <see cref="DrawCareerStatCard"/>（GameFont.Small），对齐预览 <c>.lg-cell .v</c> 字号。</remarks>
        private void DrawWorkIntensityMetricCards(Rect rect, float y, float width)
        {
            WorkIntensityView intensity = cachedWorkIntensity;
            float gap = UITheme.SpaceXs;
            float cellW = (width - gap * 2f) / 3f;
            DrawCareerStatCard(new Rect(rect.x, y, cellW, CareerMetricCardHeight),
                "PersonalChronicle.UI.TotalWorkHours".Translate().ToString(),
                FormatHours(intensity != null ? intensity.TotalHours : 0d),
                "PersonalChronicle.UI.Intensity.ObservedWindow".Translate(
                    FormatDays(intensity != null ? intensity.ObservedDays : 0d)).ToString());
            DrawCareerStatCard(new Rect(rect.x + cellW + gap, y, cellW, CareerMetricCardHeight),
                "PersonalChronicle.UI.Intensity.Daily".Translate().ToString(),
                FormatHours(intensity != null ? intensity.DailyHours : 0d),
                "PersonalChronicle.UI.Intensity.MonthlyEst".Translate(
                    FormatHours(intensity != null ? intensity.MonthlyHours : 0d)).ToString());
            DrawCareerStatCard(new Rect(rect.x + 2f * (cellW + gap), y, cellW, CareerMetricCardHeight),
                "PersonalChronicle.UI.Intensity.Weekly".Translate().ToString(),
                FormatHours(intensity != null ? intensity.WeeklyHours : 0d),
                string.Empty);
        }

        /// <summary>
        /// 档位阶梯：<c>.chip</c> 药丸（预览 :1578-1583，高 18）——当前档实色 + 描边，其余 28% 弱化。
        /// </summary>
        /// <param name="rect">栏目区域。</param>
        /// <param name="y">传入并回写当前绘制纵坐标。</param>
        /// <param name="service">归档服务；不支持强度接口或阶数为 0 时画单枚「采样中」chip。</param>
        /// <remarks>
        /// 阶梯按内容宽度依次排布，超出栏宽即停（窄栏保护，绝不越界或换行压叠）。
        /// 服务降级时只画「采样中」chip —— 这是有意的降级，不要改成抛异常。
        /// </remarks>
        private void DrawIntensityTierLadder(Rect rect, ref float y, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(rect.x, y, rect.width, CareerTinyLineHeight),
                    "PersonalChronicle.UI.Career.TierLadder".Translate().ToString());
                GUI.color = prevColor;
                y += CareerTinyLineHeight + UITheme.SpaceXxs;

                WorkIntensityView intensity = cachedWorkIntensity;
                IReadOnlyList<WorkIntensityTierView> tiers = service is IWorkIntensityService ? cachedTiers : null;
                if (tiers == null || tiers.Count == 0)
                {
                    // 无档位（服务不支持 / 阶数为 0）⇒ 如实画「采样中」chip（预览 :1583 注明）。
                    UIComponents.Pill(new Rect(rect.x, y, 64f, CareerChipHeight),
                        "PersonalChronicle.UI.Intensity.SampleInsufficient".Translate().ToString(),
                        ArchiveUiStyle.Muted);
                    y += CareerChipHeight + UITheme.SpaceXs;
                    return;
                }

                float x = rect.x;
                for (int i = 0; i < tiers.Count; i++)
                {
                    WorkIntensityTierView tier = tiers[i];
                    string label = !string.IsNullOrEmpty(tier.DisplayCode) ? tier.DisplayCode : tier.DefName;
                    Text.Font = GameFont.Tiny;
                    float chipW = Mathf.Max(34f, Text.CalcSize(label).x + 12f);
                    if (x + chipW > rect.xMax)
                    {
                        break;   // 窄栏保护：放不下就不再画（不换行、不越界）
                    }
                    Color color = ParseIntensityColor(tier.ColorHex);
                    bool current = intensity != null && intensity.IsDefined
                        && intensity.TierDefName == tier.DefName;
                    Rect chip = new Rect(x, y, chipW, CareerChipHeight);
                    UIComponents.Pill(chip, label, current ? color : UITheme.WithAlpha(color, 0.28f));
                    TooltipHandler.TipRegion(chip,
                        TranslateIntensityKey(tier.LabelKey, tier.DefName));
                    x += chipW + UITheme.SpaceXxs;
                }
                y += CareerChipHeight + UITheme.SpaceXs;
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        // ═══════════════════ 左栏 ②：工作累计工时（.bars 行）═══════════════════

        /// <summary>
        /// 工作累计工时：每行 22 高的条形行（预览 :1586-1595，最多 5 行）。
        /// </summary>
        /// <param name="rect">栏目区域。</param>
        /// <param name="y">传入并回写当前绘制纵坐标。</param>
        /// <remarks>
        /// 列宽 118（工种名）/ 1fr（占比条）/ 74（工时）/ 92（殖民地排名，Tiny 右对齐）——
        /// 旧实现是 2 列 112 高卡片（体积约 3 倍），本版按预览改为紧凑行。
        /// 行内取行前判剩余高度不足即 break；无工时数据时画空态，不伪造读数。
        /// </remarks>
        private void DrawWorkTimeBars(Rect rect, ref float y)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.WorkTime".Translate().ToString());
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(rect.x, y, rect.width, CareerTinyLineHeight),
                    "PersonalChronicle.UI.WorkTimeFootnote".Translate().ToString());
                GUI.color = prevColor;
                y += CareerTinyLineHeight + UITheme.SpaceXxs;

                if (cachedIntensityWorkTypes == null || cachedIntensityWorkTypes.Count == 0)
                {
                    UIComponents.Label(new Rect(rect.x, y, rect.width, 22f),
                        "PersonalChronicle.UI.NoWorkTimeData".Translate().ToString(),
                        GameFont.Small, ArchiveUiStyle.Muted, TextAnchor.MiddleLeft);
                    y += 22f;
                    return;
                }

                float fixedW = CareerBarLabelWidth + CareerBarValueWidth + CareerBarRankWidth
                    + UITheme.SpaceXxs * 3f;
                float barW = Mathf.Max(0f, rect.width - fixedW);
                int drawn = 0;
                for (int i = 0; i < cachedIntensityWorkTypes.Count && drawn < CareerBarRowLimit; i++)
                {
                    WorkIntensityWorkTypeView row = cachedIntensityWorkTypes[i];
                    if (row == null)
                    {
                        continue;
                    }
                    float rowH = CareerBarRowHeight;
                    if (y + rowH > rect.yMax)
                    {
                        break;   // 剩余高度不足即停（滚动内容底部保护）
                    }

                    // 左：工种名（Small·正文色）。
                    UIComponents.Label(new Rect(rect.x, y, CareerBarLabelWidth, rowH),
                        UIComponents.TruncateToWidth(WorkTypeLabel(row.WorkTypeDefName),
                            CareerBarLabelWidth, GameFont.Small),
                        GameFont.Small, ArchiveUiStyle.Text, TextAnchor.MiddleLeft);

                    // 中：个人占比条（条高 = UITheme.ProgressbarH，行内垂直居中）。
                    if (barW > 0f)
                    {
                        UIComponents.ProgressBar(
                            new Rect(rect.x + CareerBarLabelWidth,
                                y + (rowH - UITheme.ProgressbarH) * 0.5f,
                                barW, UITheme.ProgressbarH),
                            Mathf.Clamp01(row.Share01), ArchiveUiStyle.Accent);
                    }

                    // 右：累计工时（Small 右对齐）+ 殖民地排名（Tiny 右对齐，复用既有强度键）。
                    float valueX = rect.xMax - CareerBarRankWidth - CareerBarValueWidth - UITheme.SpaceXxs;
                    UIComponents.Label(new Rect(valueX, y, CareerBarValueWidth, rowH),
                        FormatWorkHours(row.Ticks), GameFont.Small, ArchiveUiStyle.Text, TextAnchor.MiddleRight);
                    UIComponents.Label(new Rect(rect.xMax - CareerBarRankWidth,
                            y + (rowH - CareerTinyLineHeight) * 0.5f, CareerBarRankWidth, CareerTinyLineHeight),
                        row.Rank > 0
                            ? "PersonalChronicle.UI.Intensity.Rank".Translate(row.Rank, row.PopulationCount).ToString()
                            : "PersonalChronicle.UI.Intensity.RankUnknown".Translate().ToString(),
                        GameFont.Tiny, ArchiveUiStyle.Muted, TextAnchor.MiddleRight);

                    UIComponents.Rule(new Rect(rect.x, y + rowH - 1f, rect.width, 1f), UITheme.BorderSoft);
                    y += rowH;
                    drawn++;
                }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        // ═══════════════════ 左栏 ③：工时投入趋势（8 季桶柱图）═══════════════════

        /// <summary>
        /// 工时投入趋势（预览 :1599-1627 为 12 周柱图；工程只有 8 季桶 ⇒ 按 8 季绘制并如实声明）。
        /// </summary>
        /// <param name="rect">栏目区域。</param>
        /// <param name="y">传入并回写当前绘制纵坐标。</param>
        /// <remarks>
        /// 序列来自 <c>cachedProductionSummary.QuadrumWorkHours</c>（8 季滚动桶，段 0 = 最老），
        /// 已由 <see cref="EnsureCareerTrendSeries"/> 在 latch 里归一为定长数组 —— 绘制路径零遍历。
        /// 全 0 / null ⇒ 画空槽 + 口径说明，绝不填样例数据（LAND-009）。
        /// </remarks>
        private void DrawWorkTrendGroup(Rect rect, ref float y)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
                UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.Career.Trend.WorkTitle");
                y += 22f + UITheme.SpaceXxs;

                float padX = UITheme.CardPadX;
                float innerW = Mathf.Max(60f, rect.width - padX * 2f);

                if (cachedWorkHasData)
                {
                    Rect summary = new Rect(rect.x + padX, y, innerW, 22f);
                    float sx = DrawChartSummaryValue(summary, summary.x,
                        "PersonalChronicle.UI.Career.Trend.WorkMean".Translate().ToString() + " "
                            + cachedWorkMean.ToString("0.0") + " h",
                        ArchiveUiStyle.Accent);
                    DrawChartSummaryValue(summary, sx,
                        "PersonalChronicle.UI.Career.Trend.PeakValley".Translate(
                            cachedWorkPeak.ToString("0.0"), cachedWorkValley.ToString("0.0")).ToString(),
                        ArchiveUiStyle.Muted);
                    y = summary.yMax + UITheme.SpaceXxs;
                }

                Rect plot = new Rect(rect.x + padX, y, innerW, CareerTrendPlotHeight);
                if (cachedWorkHasData && plot.width >= 20f)
                {
                    // 8 季桶柱图；labels = null（半宽栏季名会挤成降级，季口径由下方口径行统一说明）。
                    RuifBridge.TryBarChartV(plot, cachedWorkHoursBuckets, null);
                }
                else
                {
                    DrawEmptyTrendPlot(plot);
                }
                y = plot.yMax + UITheme.SpaceXxs;

                UIComponents.Label(new Rect(rect.x + padX, y, innerW, CareerTinyLineHeight),
                    "PersonalChronicle.UI.Career.Trend.WorkCaption".Translate().ToString(),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);
                y += CareerTinyLineHeight;
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        // ═══════════════════ 右栏 ①：生产（2 格 + 物品格）═══════════════════

        /// <summary>
        /// 生产组：累计价值 / 数量 2 格 + 物品格 2 列网格（预览 :1632-1643），数值字号 Small。
        /// </summary>
        /// <param name="rect">栏目区域。</param>
        /// <param name="y">传入并回写当前绘制纵坐标。</param>
        /// <remarks>
        /// 无生产数据时只画空态提示并继续推进 y（不 return，保证后续组位置正确）。
        /// 物品格按实际画出行数推进 y（高度不足 break 时不会多留空白）。
        /// </remarks>
        private void DrawProductionGroup(Rect rect, ref float y)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
                DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Tab.Production".Translate().ToString());
                if (cachedProductionSummary == null || cachedProductionSummary.Types.Count == 0)
                {
                    UIComponents.Label(new Rect(rect.x, y, rect.width, 22f),
                        "PersonalChronicle.UI.NoProduction".Translate().ToString(),
                        GameFont.Small, ArchiveUiStyle.Muted, TextAnchor.MiddleLeft);
                    y += 22f;
                    return;
                }

                IReadOnlyList<ProductionTypeView> types = cachedProductionSummary.Types;
                float gap = UITheme.SpaceXs;
                float cellW = Mathf.Max(0f, (rect.width - gap) * 0.5f);
                DrawCareerStatCard(new Rect(rect.x, y, cellW, 74f),
                    "PersonalChronicle.UI.ProductionValue".Translate().ToString(),
                    FormatMarketValue(cachedProductionSummary.TotalMarketValue),
                    "PersonalChronicle.UI.ProductionQuantity".Translate(
                        cachedProductionSummary.TotalQuantity).ToString());
                DrawCareerStatCard(new Rect(rect.x + cellW + gap, y, cellW, 74f),
                    "PersonalChronicle.UI.ProductionCount".Translate().ToString(),
                    cachedProductionSummary.TotalQuantity.ToString(),
                    "PersonalChronicle.UI.ProductionTypeCount".Translate(types.Count).ToString());
                y += 74f + UITheme.SpaceXs;

                float padX = UITheme.CardPadX;
                float innerW = Mathf.Max(0f, cellW - padX * 2f);
                int drawnRows = 0;
                for (int i = 0; i < types.Count; i++)
                {
                    int rowIndex = i / 2;
                    if (y + rowIndex * CareerItemCellPitch + CareerItemCellHeight > rect.yMax)
                    {
                        break;   // 剩余高度不足即停
                    }
                    ProductionTypeView type = types[i];
                    float x = rect.x + (i % 2) * (cellW + gap);
                    float cellY = y + rowIndex * CareerItemCellPitch;
                    Rect cell = new Rect(x, cellY, cellW, CareerItemCellHeight);
                    ArchiveUiStyle.DrawCard(cell, ArchiveUiStyle.Accent);
                    UIComponents.Label(new Rect(cell.x + padX, cell.y + 6f, innerW, CareerTinyLineHeight),
                        UIComponents.TruncateToWidth(ProductionDefLabel(type.DefName), innerW, GameFont.Tiny),
                        GameFont.Tiny, ArchiveUiStyle.Muted, TextAnchor.MiddleLeft);
                    UIComponents.Label(new Rect(cell.x + padX, cell.y + 26f, innerW, 22f),
                        "PersonalChronicle.UI.ProductionCard".Translate(
                            type.Quantity, FormatMarketValue(type.MarketValue)).ToString(),
                        GameFont.Small, ArchiveUiStyle.Text, TextAnchor.MiddleLeft);
                    drawnRows = Mathf.Max(drawnRows, rowIndex + 1);
                }
                y += drawnRows * CareerItemCellPitch;
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        // ═══════════════════ 右栏 ②：职业事实计数（2 列 × 最多 5 行）═══════════════════

        /// <summary>
        /// 职业事实计数：2 列键值表（预览 :1646-1659，行高 18，左列 5 项 / 右列 4 项）。
        /// </summary>
        /// <param name="rect">栏目区域。</param>
        /// <param name="y">传入并回写当前绘制纵坐标。</param>
        /// <remarks>
        /// 计数与标签来自 <see cref="EnsureCareerFactRows"/> 的 latch 缓存（绘制路径零字典读取、零翻译）。
        /// 计数为 0 的项用 Dim 弱化，非 0 用 Accent（预览 <c>.acc</c>）。
        /// </remarks>
        private void DrawCareerFactGroup(Rect rect, ref float y)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
                DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Career.Fact.Title".Translate().ToString());

                float gap = UITheme.SpaceXs;
                float halfW = Mathf.Max(0f, (rect.width - gap) * 0.5f);
                float countW = 44f;
                float labelW = Mathf.Max(0f, halfW - countW - UITheme.SpaceXxs);
                int rightCount = CareerFactCount - CareerFactLeftCount;
                int rows = Mathf.Max(CareerFactLeftCount, rightCount);

                for (int r = 0; r < rows; r++)
                {
                    if (y + CareerTinyLineHeight > rect.yMax)
                    {
                        break;   // 剩余高度不足即停
                    }
                    for (int c = 0; c < 2; c++)
                    {
                        int limit = c == 0 ? CareerFactLeftCount : rightCount;
                        if (r >= limit)
                        {
                            continue;   // 第 10 格（右列第 5 行）无数据 ⇒ 留空，不画假 0
                        }
                        int index = c == 0 ? r : CareerFactLeftCount + r;
                        float cx = rect.x + c * (halfW + gap);
                        string label = cachedFactLabels[index] ?? string.Empty;
                        int count = cachedFactValues[index];
                        UIComponents.Label(new Rect(cx, y, labelW, CareerTinyLineHeight),
                            UIComponents.TruncateToWidth(label, labelW, GameFont.Tiny),
                            GameFont.Tiny, ArchiveUiStyle.Muted, TextAnchor.MiddleLeft);
                        UIComponents.Label(new Rect(cx + halfW - countW, y, countW, CareerTinyLineHeight),
                            count.ToString(), GameFont.Tiny,
                            count > 0 ? ArchiveUiStyle.Accent : ArchiveUiStyle.Dim,
                            TextAnchor.MiddleRight);
                    }
                    y += CareerTinyLineHeight;
                }
                y += UITheme.SpaceXxs;
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        // ═══════════════════ 右栏 ③：累计产出价值趋势（面积图）═══════════════════

        /// <summary>
        /// 累计产出价值趋势（预览 :1666-1692）：<c>QuadrumMarketValue</c> 逐季累加后画面积图。
        /// </summary>
        /// <param name="rect">栏目区域。</param>
        /// <param name="y">传入并回写当前绘制纵坐标。</param>
        /// <remarks>
        /// 累计序列由 <see cref="EnsureCareerTrendSeries"/> 在 latch 里算好（绘制路径零遍历）。
        /// 全 0 ⇒ 空槽 + 说明，绝不填假数据（LAND-009）。
        /// </remarks>
        private void DrawProductionTrendGroup(Rect rect, ref float y)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
                UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.Career.Trend.ProductionTitle");
                y += 22f + UITheme.SpaceXxs;

                float padX = UITheme.CardPadX;
                float innerW = Mathf.Max(60f, rect.width - padX * 2f);

                if (cachedProductHasData)
                {
                    Rect summary = new Rect(rect.x + padX, y, innerW, 22f);
                    float sx = DrawChartSummaryValue(summary, summary.x,
                        "PersonalChronicle.UI.Career.Trend.ProductionCumulative".Translate().ToString() + " "
                            + FormatMarketValue(cachedProductTotal),
                        ArchiveUiStyle.Accent);
                    DrawChartSummaryValue(summary, sx,
                        "PersonalChronicle.UI.Career.Trend.SlopePerQuadrum".Translate(
                            FormatMarketValue(cachedProductSlope)).ToString(),
                        ArchiveUiStyle.Alive);
                    y = summary.yMax + UITheme.SpaceXxs;
                }

                Rect plot = new Rect(rect.x + padX, y, innerW, CareerTrendPlotHeight);
                if (cachedProductHasData)
                {
                    RuifBridge.TryAreaChart(plot, cachedProductCumulative);
                }
                else
                {
                    DrawEmptyTrendPlot(plot);
                }
                y = plot.yMax + UITheme.SpaceXxs;

                UIComponents.Label(new Rect(rect.x + padX, y, innerW, CareerTinyLineHeight),
                    "PersonalChronicle.UI.Career.Trend.ProductionCaption".Translate().ToString(),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);
                y += CareerTinyLineHeight;
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        // ═══════════════════ latch 缓存构建 ═══════════════════

        /// <summary>
        /// 构建职业事实计数 latch（预览 :1646-1659 的 9 类事实）。
        /// </summary>
        /// <param name="service">归档服务（仅用于取数据修订号作为守卫）。</param>
        /// <remarks>
        /// <para>取数：消费窗口镜像 <c>cachedFactCounts</c>（= <c>DetailSnapshot.FactCounts</c>，
        /// 由 <c>RebuildDetailCache</c> 逐字段镜像，v5.x 补齐该字段的镜像）——
        /// 只做 9 次字段读取，不扫事件流、不聚合、不发任何新查询，且仅在 rev/对象变化时执行一次。</para>
        /// </remarks>
        private void EnsureCareerFactRows(IArchiveService service)
        {
            long revision = service != null ? service.GetDataRevision() : 0L;
            if (careerFactOwnerId == detailObjectId && careerFactRevision == revision)
            {
                return;
            }
            careerFactOwnerId = detailObjectId;
            careerFactRevision = revision;

            for (int i = 0; i < CareerFactCount; i++)
            {
                cachedFactLabels[i] = CareerFactKeys[i].Translate().ToString();
                cachedFactValues[i] = FactCountAt(cachedFactCounts, i);
            }
        }

        /// <summary>
        /// 构建两条趋势序列的 latch（面积图累计产出 + 柱图 8 季工时）。
        /// </summary>
        /// <remarks>
        /// 守卫 = <c>cachedProductionSummary</c> 的引用（<c>RebuildDetailCache</c> 每次重建都赋新实例）
        /// ⇒ 队列变化时重算一次，绘制路径只读定长数组（零遍历/零分配）。
        /// 「近 8 季」窗口内的峰值/谷值/均值也在此算好，避免绘制路径对序列做第二遍扫描。
        /// </remarks>
        private void EnsureCareerTrendSeries()
        {
            ProductionSummaryView summary = cachedProductionSummary;
            if (ReferenceEquals(cachedProductionSummaryRef, summary))
            {
                return;
            }
            cachedProductionSummaryRef = summary;

            // —— 累计产出价值（面积图）：8 季产出市值逐季累加 ——
            float[] market = summary != null ? summary.QuadrumMarketValue : null;
            float running = 0f;
            bool productAny = false;
            for (int i = 0; i < cachedProductCumulative.Length; i++)
            {
                float value = market != null && i < market.Length ? market[i] : 0f;
                if (value > 0.01f)
                {
                    productAny = true;
                }
                running += value;
                cachedProductCumulative[i] = running;
            }
            cachedProductHasData = productAny;
            cachedProductTotal = running;
            int last = cachedProductCumulative.Length - 1;
            cachedProductSlope = last >= 1
                ? cachedProductCumulative[last] - cachedProductCumulative[last - 1]
                : 0f;

            // —— 工时投入（柱图）：8 季工时桶 + 季均/峰值/谷值 ——
            float[] hours = summary != null ? summary.QuadrumWorkHours : null;
            float sum = 0f;
            float peak = 0f;
            float valley = 0f;
            bool workAny = false;
            for (int i = 0; i < cachedWorkHoursBuckets.Length; i++)
            {
                float value = hours != null && i < hours.Length ? hours[i] : 0f;
                cachedWorkHoursBuckets[i] = value;
                if (value > 0.0001f)
                {
                    workAny = true;
                }
                if (i == 0 || value < valley)
                {
                    valley = value;
                }
                if (value > peak)
                {
                    peak = value;
                }
                sum += value;
            }
            cachedWorkHasData = workAny;
            cachedWorkMean = workAny ? sum / cachedWorkHoursBuckets.Length : 0f;
            cachedWorkPeak = workAny ? peak : 0f;
            cachedWorkValley = workAny ? valley : 0f;
        }

        // ═══════════════════ 绘制小工具 ═══════════════════

        /// <summary>
        /// 生涯子页通用指标卡（标签 Tiny / 数值 <b>Small</b> / 副标签 Tiny）——预览 <c>.lg-cell</c> 字号档。
        /// </summary>
        /// <param name="rect">卡片区域。</param>
        /// <param name="label">顶部小标签（Tiny·Muted）。</param>
        /// <param name="value">中部数值（Small·Text）。</param>
        /// <param name="subLabel">底部副标签（Tiny·Muted）；空串则不绘制。</param>
        /// <remarks>
        /// 与共享的 <c>DrawMetricCard</c>（数值 Medium）区分：预览 <c>.lg-cell .v</c> 为 14px 档 ⇒ 走 Small。
        /// 行高全部取字体度量（Tiny 18 / Small 22），不出现任意文本行高字面量。
        /// </remarks>
        private static void DrawCareerStatCard(Rect rect, string label, string value, string subLabel)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
                ArchiveUiStyle.DrawCard(rect, ArchiveUiStyle.Info);
                float padX = UITheme.CardPadX;
                float innerW = Mathf.Max(0f, rect.width - padX * 2f);
                float labelY = rect.y + UITheme.CardPadY;
                float valueY = labelY + 18f + UITheme.SpaceXxs;
                float subLabelY = valueY + 22f + UITheme.SpaceXxs;

                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(rect.x + padX, labelY, innerW, 18f), label ?? string.Empty);
                Text.Font = GameFont.Small;
                GUI.color = ArchiveUiStyle.Text;
                Widgets.Label(new Rect(rect.x + padX, valueY, innerW, 22f), value ?? string.Empty);
                if (!string.IsNullOrEmpty(subLabel))
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = ArchiveUiStyle.Muted;
                    Widgets.Label(new Rect(rect.x + padX, subLabelY, innerW, 18f), subLabel);
                }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        /// <summary>趋势空态：窗口底色空槽 + 边框 + 破折号（不填假数据，LAND-009）。</summary>
        private static void DrawEmptyTrendPlot(Rect plot)
        {
            Widgets.DrawBoxSolid(plot, UITheme.Window);
            UIComponents.Border(plot, UITheme.BorderSoft);
            UIComponents.Label(plot, "—", GameFont.Small, UITheme.Dim, TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// 翻译强度相关的键；键为空时回退到给定兜底文案。
        /// </summary>
        /// <param name="key">翻译键；为空或空白时使用兜底。</param>
        /// <param name="fallback">兜底文案。</param>
        /// <returns>翻译结果或兜底文案，保证永不返回 null。</returns>
        /// <remarks>强度相关文案来自 XML 配置，键可能未配；统一走这里兜底，
        /// 避免各处散落不同的空值处理方式。</remarks>
        private static string TranslateIntensityKey(string key, string fallback)
        {
            return string.IsNullOrEmpty(key)
                ? (fallback ?? string.Empty)
                : key.Translate().ToString();
        }

        /// <summary>
        /// 生成"相对全殖民地平均"的对比文案。
        /// </summary>
        /// <param name="intensity">强度视图；为 null 或比值非正时返回"不可用"文案。</param>
        /// <returns>过载 / 闲散 / 具体倍数三类文案之一。</returns>
        /// <remarks>过载与闲散的判定由视图上的 IsOverloaded / IsSignificantlyIdle 提供，
        /// 本方法不重复定义阈值——要调整判定口径请改 Domain 层，不要在这边加魔法数。</remarks>
        private static string BuildIntensityRelativeLabel(WorkIntensityView intensity)
        {
            if (intensity == null || intensity.RelativeRatio <= 0d)
            {
                return "PersonalChronicle.UI.Intensity.RelativeUnavailable".Translate().ToString();
            }
            if (intensity.IsOverloaded)
            {
                return "PersonalChronicle.UI.Intensity.Overload".Translate().ToString();
            }
            if (intensity.IsSignificantlyIdle)
            {
                return "PersonalChronicle.UI.Intensity.Slack".Translate().ToString();
            }
            return "PersonalChronicle.UI.Intensity.Relative".Translate(
                intensity.RelativeRatio.ToString("0.00")).ToString();
        }

        /// <summary>
        /// 解析十六进制颜色字符串。
        /// </summary>
        /// <param name="hex">形如 #RRGGBB 的颜色串；为空或非法时回退到 ArchiveUiStyle.Info。</param>
        /// <returns>解析得到的颜色。</returns>
        /// <remarks>回退色是刻意设计：档位颜色来自 XML 配置，配置写错时
        /// 界面应退化成中性色，而不是变黑或抛异常打断绘制。</remarks>
        private static Color ParseIntensityColor(string hex)
        {
            Color color;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out color))

            {
                return color;
            }
            return ArchiveUiStyle.Info;
        }

        /// <summary>
        /// 按工种 defName 稳定地生成一个区分色。
        /// </summary>
        /// <param name="defName">工种 defName；为空时回退到 ArchiveUiStyle.Info。</param>
        /// <returns>由名称哈希映射到色相得到的 HSV 颜色（饱和度与明度固定）。</returns>
        /// <remarks>
        /// 用字符串哈希映射色相而不是维护配色表：工种可由任意 mod 定义，查表无法穷举。
        /// 与 0x7fffffff 做与运算是为了去掉符号位，保证色相为正。
        /// 注意：字符串哈希在进程间并不稳定（.NET 的字符串哈希随机化），
        /// 因此该颜色只能用于当次会话内的视觉区分，绝不可持久化到存档或用于跨会话比对。
        /// </remarks>
        private static Color WorkTypeColor(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return ArchiveUiStyle.Info;
            }
            int hash = StringComparer.Ordinal.GetHashCode(defName) & 0x7fffffff;
            float hue = (hash % 360) / 360f;
            Color color = Color.HSVToRGB(hue, 0.38f, 0.82f);
            color.a = 1f;
            return color;
        }

        /// <summary>
        /// 取技能（SkillDef）的显示名。
        /// </summary>
        /// <param name="defName">SkillDef 的 defName；为空时返回空串。</param>
        /// <returns>Def.label；Def 缺失或标签为空时返回原始 defName。</returns>
        /// <remarks>这里刻意不记录缺失日志（与 Labels.cs 中带日志的降级链不同）：
        /// 技能 Def 缺失极为罕见，而本方法在卡片循环中调用频繁，记日志得不偿失。</remarks>
        private static string SkillDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            SkillDef def = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            return defName;
        }


    }
}

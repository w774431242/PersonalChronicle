using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>Partial of ArchiveMainTabWindow 鈥?PawnDetail drawing (BUG-BASE-01 refactor).</summary>
    public sealed partial class ArchiveMainTabWindow
    {

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

        private void DrawCareerTab(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
            float y = rect.y;

            // v4.17: 职业档案界面嵌入个人档案「生涯」tab（职业身份 / 下一职称 / 资格状态）。
            // 数据源 = cachedCareerOverview（Read Model 派生的 DetailSnapshot.CareerOverview），
            // 窗口只消费快照，不做任何查询/排序/判定（架构 §3.1 G 层边界）。
            y = DrawCareerProfileSection(rect, y);

            DrawWorkIntensityHeader(rect, ref y, service);
            DrawWorkIntensityCards(rect, ref y);

            // Production summary: aggregate cards first, detailed type cards
            // second. Routine craft events are no longer the only source of
            // career totals.
            y += 8f;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Tab.Production".Translate().ToString());
            if (cachedProductionSummary == null || cachedProductionSummary.Types.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoProduction".Translate().ToString());
                y += 28f;
            }
            else
            {
                IReadOnlyList<ProductionTypeView> types = cachedProductionSummary.Types;
                float summaryGap = 8f;
                float summaryWidth = (rect.width - summaryGap) / 2f;
                DrawMetricCard(new Rect(rect.x, y, summaryWidth, 72f),
                    "PersonalChronicle.UI.ProductionValue".Translate().ToString(),
                    FormatMarketValue(cachedProductionSummary.TotalMarketValue),
                    "PersonalChronicle.UI.ProductionQuantity".Translate(
                        cachedProductionSummary.TotalQuantity).ToString());
                DrawMetricCard(new Rect(rect.x + summaryWidth + summaryGap, y, summaryWidth, 72f),
                    "PersonalChronicle.UI.ProductionCount".Translate().ToString(),
                    cachedProductionSummary.TotalQuantity.ToString(),
                    "PersonalChronicle.UI.ProductionTypeCount".Translate(types.Count).ToString());
                y += 86f;
                for (int i = 0; i < types.Count; i++)
                {
                    ProductionTypeView type = types[i];
                    float width = (rect.width - UITheme.GridGap) / 2f;
                    float x = rect.x + (i % 2) * (width + UITheme.GridGap);
                    float cardY = y + (i / 2) * 58f;
                    ArchiveUiStyle.DrawCard(new Rect(x, cardY, width, 50f), ArchiveUiStyle.Accent);
                    Text.Font = GameFont.Small;
                    Widgets.Label(new Rect(x + UITheme.CardPadX, cardY + 6f, width - UITheme.CardPadX * 2f, 20f), ProductionDefLabel(type.DefName));
                    Text.Font = GameFont.Tiny;
                    GUI.color = ArchiveUiStyle.Muted;
                    Widgets.Label(new Rect(x + UITheme.CardPadX, cardY + 28f, width - UITheme.CardPadX * 2f, 16f),
                        "PersonalChronicle.UI.ProductionCard".Translate(type.Quantity, FormatMarketValue(type.MarketValue)).ToString());
                    GUI.color = prevColor;
                }
                y += ((types.Count + 1) / 2) * 58f;
            }
            }
            finally
            {
                // v4.17 体检：恢复字体（旧实现空态/卡片行后 Text.Font 停留在 Tiny）。
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        // ---- v4.17 职业档案区块（嵌入「生涯」tab；只消费 CareerOverviewView 快照） ----

        /// <summary>职业档案区块总高度（无职业数据时返回 0，与绘制路径同口径）。</summary>
        private float CareerProfileBlockHeight()
        {
            ReadModels.CareerOverviewView ov = cachedCareerOverview;
            if (ov == null || !ov.HasData)
            {
                return 0f;
            }
            float h = UITheme.SectionTitleHeight + UITheme.SpaceXs;   // 标题
            h += 104f + UITheme.SpaceMd;                               // 职业身份（含 5 指标格）
            h += UITheme.SectionTitleHeight + UITheme.SpaceXs;         // 下一职称标题
            h += (string.IsNullOrEmpty(ov.NextTitle) ? 36f : 84f) + UITheme.SpaceMd;
            h += UITheme.SectionTitleHeight + UITheme.SpaceXs;         // 资格状态标题
            int rows = (ov.Qual != null && ov.Qual.Count > 0) ? (ov.Qual.Count + 1) / 2 : 0;
            h += (rows > 0 ? rows * (30f + 8f) : 26f) + UITheme.SpaceMd;
            return h + 8f;
        }

        private float DrawCareerProfileSection(Rect rect, float y)
        {
            ReadModels.CareerOverviewView ov = cachedCareerOverview;
            if (ov == null || !ov.HasData)
            {
                return y;
            }

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.CareerTab".Translate().ToString());

            // —— 职业身份（主职称大字 + 方向/技能/工时副行 + 5 指标小格）——
            float pad = UITheme.PanelPadding;
            float innerW = rect.width - 2f * pad;
            // v4.17 体检：block 高 92f→104f —— MiniStat 值行（Small 22f）+ 标签行（Tiny 18f）
            // 需 40f 单元，旧 16f/14f 矩形会裁切 Medium 字体且 statY 越过面板底边。
            Rect block = new Rect(rect.x, y, rect.width, 104f);
            ArchiveUiStyle.DrawPanel(block, ArchiveUiStyle.PanelRaised);
            float bx = block.x + pad;
            float by = block.y + 12f;

            string role = string.IsNullOrEmpty(ov.RoleName)
                ? "PersonalChronicle.UI.Career.Ov.Undefined".Translate().ToString()
                : ov.RoleName;
            UIComponents.Label(new Rect(bx, by, innerW, 28f), role, UITheme.FontValue, UITheme.Accent);

            string sub = ov.RoleDesc ?? string.Empty;
            if (!string.IsNullOrEmpty(ov.SkillText))
            {
                sub = string.IsNullOrEmpty(sub) ? ov.SkillText : sub + " · " + ov.SkillText;
            }
            if (!string.IsNullOrEmpty(ov.HoursText))
            {
                sub = string.IsNullOrEmpty(sub) ? ov.HoursText : sub + " · " + ov.HoursText;
            }
            UIComponents.Label(new Rect(bx, by + 32f, innerW, 20f), sub, UITheme.FontLabel, UITheme.Muted);

            float statY = by + 52f;
            float statGap = 10f;
            float statW = (innerW - statGap * 4f) / 5f;
            DrawCareerMiniStat(new Rect(bx, statY, statW, 40f),
                FormatCompact(ov.Made), "PersonalChronicle.UI.Career.Ov.Metric.Made".Translate().ToString());
            DrawCareerMiniStat(new Rect(bx + (statW + statGap), statY, statW, 40f),
                FormatCompact(ov.Built), "PersonalChronicle.UI.Career.Ov.Metric.Built".Translate().ToString());
            DrawCareerMiniStat(new Rect(bx + (statW + statGap) * 2f, statY, statW, 40f),
                FormatCompact(ov.Researched), "PersonalChronicle.UI.Career.Ov.Metric.Research".Translate().ToString());
            DrawCareerMiniStat(new Rect(bx + (statW + statGap) * 3f, statY, statW, 40f),
                FormatCompact(ov.Books), "PersonalChronicle.UI.Career.Ov.Books".Translate().ToString());
            DrawCareerMiniStat(new Rect(bx + (statW + statGap) * 4f, statY, statW, 40f),
                FormatCompact(ov.Results), "PersonalChronicle.UI.Career.Ov.Results".Translate().ToString());
            y += 104f + UITheme.SpaceMd;

            // —— 下一职称（进度条 + 缺口）——
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Career.Ov.NextTitle".Translate().ToString());
            if (string.IsNullOrEmpty(ov.NextTitle))
            {
                UIComponents.TintedBox(new Rect(rect.x, y, rect.width, 36f), UITheme.Panel);
                UIComponents.Label(new Rect(rect.x + 10f, y, rect.width - 20f, 36f),
                    "PersonalChronicle.UI.Career.Ov.Maxed".Translate().ToString(),
                    UITheme.FontLabel, UITheme.PillGold, TextAnchor.MiddleLeft);
                y += 36f + UITheme.SpaceMd;
            }
            else
            {
                float blockH = 84f;
                Rect nb = new Rect(rect.x, y, rect.width, blockH);
                UIComponents.Panel(nb, UITheme.Panel);
                UIComponents.Border(nb, UITheme.BorderSoft);
                float nbx = nb.x + pad;
                float nbw = nb.width - 2f * pad;
                UIComponents.Label(new Rect(nbx, nb.y + 8f, nbw, 24f), ov.NextTitle,
                    UITheme.FontValue, UITheme.Text);
                UIComponents.ProgressBar(new Rect(nbx, nb.y + 36f, nbw, 14f),
                    Mathf.Clamp01(ov.Progress / 100f), UITheme.PillGold, ov.Progress + "%");
                if (ov.NextGaps != null && ov.NextGaps.Count > 0)
                {
                    string gaps = "PersonalChronicle.UI.Career.Ov.Gaps".Translate().ToString()
                        + " " + string.Join(" / ", ov.NextGaps);
                    UIComponents.Label(new Rect(nbx, nb.y + 58f, nbw, 18f), gaps,
                        UITheme.FontLabel, UITheme.Dim);
                }
                y += blockH + UITheme.SpaceMd;
            }

            // —— 资格状态（6 条件 2 列网格）——
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Career.Ov.Qual".Translate().ToString());
            if (ov.Qual == null || ov.Qual.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x, y, rect.width, 20f),
                    "PersonalChronicle.UI.Career.Ov.NoQual".Translate().ToString(),
                    UITheme.FontLabel, UITheme.Dim);
                y += 26f + UITheme.SpaceMd;
            }
            else
            {
                float gap = 8f;
                int cols = 2;
                float rowH = 30f;
                float cellW = (rect.width - gap * (cols - 1)) / cols;
                for (int i = 0; i < ov.Qual.Count; i++)
                {
                    int col = i % cols;
                    int row = i / cols;
                    Rect cell = new Rect(rect.x + col * (cellW + gap), y + row * (rowH + gap), cellW, rowH);
                    DrawCareerQualCell(cell, ov.Qual[i]);
                }
                int rows = (ov.Qual.Count + cols - 1) / cols;
                y += rows * (rowH + gap) + UITheme.SpaceMd;
            }

            return y + 8f;
        }

        /// <summary>
        /// v4.17 体检：MiniStat 值行用 Small（行高 22f）而非 Medium（28f 会裁切 16f 矩形），
        /// 标签行 18f（Tiny CJK 安全）；单元总高 40f 与 DrawCareerProfileSection 布局一致。
        /// </summary>
        private static void DrawCareerMiniStat(Rect r, string value, string label)
        {
            UIComponents.Label(new Rect(r.x, r.y, r.width, 22f), value, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(r.x, r.y + 22f, r.width, 18f), label, UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
        }

        private static void DrawCareerQualCell(Rect r, ReadModels.CareerQualView q)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Verse.Text.Anchor;
            try
            {
                UIComponents.TintedBox(r, UITheme.Panel);
                UIComponents.Border(r, UITheme.BorderSoft);
                bool ok = q != null && q.StateKey == "ok";
                Rect dot = new Rect(r.x + 8f, r.y + (r.height - 12f) / 2f, 12f, 12f);
                UIComponents.TintedBox(dot, ok ? UITheme.PillGreen : UITheme.PillRed);
                Rect txt = new Rect(dot.xMax + 8f, r.y, r.width - 28f, r.height);
                Verse.Text.Font = GameFont.Small;
                Verse.Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = ok ? UITheme.Text : UITheme.Muted;
                Widgets.Label(txt, (q != null ? q.Label : "?") + "  " + (q != null ? q.Note : string.Empty));
            }
            finally
            {
                GUI.color = prevColor;
                Verse.Text.Font = prevFont;
                Verse.Text.Anchor = prevAnchor;
            }
        }

        /// <summary>紧凑数字：≥10000 显示 K 后缀（与职业档案 ITab 同口径）。</summary>
        private static string FormatCompact(int v)
        {
            if (v >= 10000) return (v / 1000.0).ToString("0.0") + "K";
            return v.ToString();
        }

        private void DrawWorkIntensityHeader(Rect rect, ref float y, IArchiveService service)
        {
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.CareerSummary".Translate().ToString());
            DrawWorkIntensityHero(rect, y, rect.width, service);
            y += 92f + 8f;

            WorkIntensityView intensity = cachedWorkIntensity;
            bool hasTier = intensity != null && intensity.IsDefined;
            IWorkIntensityService intensityService = service as IWorkIntensityService;
            if (intensityService == null)
            {
                return;
            }
            IReadOnlyList<WorkIntensityTierView> tiers = cachedTiers;
            if (tiers == null || tiers.Count == 0)
            {
                return;
            }
            float rungWidth = (rect.width - (tiers.Count - 1) * 2f) / tiers.Count;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;
            try
            {
                for (int i = 0; i < tiers.Count; i++)
                {
                    WorkIntensityTierView tier = tiers[i];
                    Rect rung = new Rect(rect.x + i * (rungWidth + 2f), y, rungWidth, 28f);
                    Color color = ParseIntensityColor(tier.ColorHex);
                    bool current = intensity != null && intensity.IsDefined
                        && intensity.TierDefName == tier.DefName;
                    Widgets.DrawBoxSolid(rung, current ? color : UITheme.WithAlpha(color, 0.28f));
                    if (current)
                    {
                        ArchiveUiStyle.DrawBorder(rung, ArchiveUiStyle.Text);
                    }
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = current ? ArchiveUiStyle.Text : ArchiveUiStyle.Muted;
                    Widgets.Label(rung, tier.DisplayCode ?? tier.DefName);
                    GUI.color = prevColor;
                    Text.Anchor = TextAnchor.UpperLeft;
                    TooltipHandler.TipRegion(rung,
                        TranslateIntensityKey(tier.LabelKey, tier.DisplayCode ?? tier.DefName));
                }
            }
            finally
            {
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
                GUI.color = prevColor;
            }
            y += 36f;
        }

        private void DrawWorkIntensityHero(Rect rect, float y, float width, IArchiveService service)
        {
            WorkIntensityView intensity = cachedWorkIntensity;
            bool hasTier = intensity != null && intensity.IsDefined;
            bool isEstimated = hasTier && intensity.IsEstimated;
            Color tierColor = ParseIntensityColor(intensity != null ? intensity.ColorHex : null);
            Rect hero = new Rect(rect.x, y, width, 92f);
            ArchiveUiStyle.DrawPanel(hero, ArchiveUiStyle.PanelRaised);

            string statusKey = isEstimated
                ? "PersonalChronicle.UI.Intensity.Estimated"
                : "PersonalChronicle.UI.Intensity.Actual";
            string badgeCode = hasTier
                ? TranslateIntensityKey(intensity.DisplayCode,
                    "PersonalChronicle.UI.NotAvailable".Translate().ToString())
                : "PersonalChronicle.UI.Intensity.SampleInsufficient".Translate().ToString();
            string badgeStatus = hasTier
                ? statusKey.Translate().ToString()
                : string.Empty;
            Rect badge = new Rect(hero.x + 8f, hero.y + 8f, 170f, hero.height - 16f);
            ArchiveUiStyle.DrawBadge(badge, string.Empty, hasTier ? tierColor : ArchiveUiStyle.Muted);
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;
            try
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(badge.x + 8f, badge.y + 14f, badge.width - 16f, 26f),
                    hasTier && !string.IsNullOrEmpty(intensity.LabelKey)
                        ? TranslateIntensityKey(intensity.LabelKey, intensity.DisplayCode)
                        : "PersonalChronicle.UI.Intensity.SampleInsufficient".Translate().ToString());
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Text.Anchor = TextAnchor.MiddleCenter;
                string badgeSubtitle = hasTier
                    ? badgeStatus + " · " + badgeCode
                    : "PersonalChronicle.UI.Intensity.ObservedWindow".Translate(
                        FormatDays(intensity != null ? intensity.ObservedDays : 0d)).ToString();
                Widgets.Label(new Rect(badge.x + 8f, badge.y + 44f, badge.width - 16f, 20f), badgeSubtitle);
                GUI.color = prevColor;
                Text.Anchor = TextAnchor.UpperLeft;
            }
            finally
            {
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
                GUI.color = prevColor;
            }

            float gap = 8f;
            float statsX = badge.xMax + 10f;
            float statsWidth = hero.xMax - statsX - 8f;
            float cellWidth = (statsWidth - gap * 2f) / 3f;
            DrawMetricCard(new Rect(statsX, hero.y + 8f, cellWidth, 84f),
                "PersonalChronicle.UI.TotalWorkHours".Translate().ToString(),
                FormatHours(intensity != null ? intensity.TotalHours : 0d),
                "PersonalChronicle.UI.Intensity.ObservedWindow".Translate(
                    FormatDays(intensity != null ? intensity.ObservedDays : 0d)).ToString());
            DrawMetricCard(new Rect(statsX + cellWidth + gap, hero.y + 8f, cellWidth, 84f),
                "PersonalChronicle.UI.Intensity.Daily".Translate().ToString(),
                FormatHours(intensity != null ? intensity.DailyHours : 0d),
                "PersonalChronicle.UI.Intensity.MonthlyEst".Translate(
                    FormatHours(intensity != null ? intensity.MonthlyHours : 0d)).ToString());
            DrawMetricCard(new Rect(statsX + 2f * (cellWidth + gap), hero.y + 8f, cellWidth, 84f),
                "PersonalChronicle.UI.Intensity.Weekly".Translate().ToString(),
                FormatHours(intensity != null ? intensity.WeeklyHours : 0d),
                BuildIntensityRelativeLabel(intensity));
        }

        private void DrawWorkIntensityCards(Rect rect, ref float y)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.WorkTime".Translate().ToString());
            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Muted;
            Widgets.Label(new Rect(rect.x, y, rect.width, 28f),
                "PersonalChronicle.UI.WorkTimeFootnote".Translate().ToString());
            GUI.color = prevColor;
            y += 32f;

            if (cachedIntensityWorkTypes == null || cachedIntensityWorkTypes.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoWorkTimeData".Translate().ToString());
                y += 28f;
                return;
            }

            const float cardGap = 8f;
            const float cardHeight = 112f;
            float cardWidth = (rect.width - cardGap) / 2f;
            for (int i = 0; i < cachedIntensityWorkTypes.Count; i++)
            {
                WorkIntensityWorkTypeView row = cachedIntensityWorkTypes[i];
                if (row == null)
                {
                    continue;
                }
                float x = rect.x + (i % 2) * (cardWidth + cardGap);
                float cardY = y + (i / 2) * (cardHeight + cardGap);
                Color accent = WorkTypeColor(row.WorkTypeDefName);
                Rect card = new Rect(x, cardY, cardWidth, cardHeight);
                ArchiveUiStyle.DrawCard(card, accent);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(card.x + 10f, card.y + 10f, card.width - 100f, 22f),
                    WorkTypeLabel(row.WorkTypeDefName));
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(card.x + card.width - 92f, card.y + 30f, 80f, 28f),
                    FormatWorkHours(row.Ticks));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                string rank = row.Rank > 0
                    ? "PersonalChronicle.UI.Intensity.Rank".Translate(row.Rank, row.PopulationCount).ToString()
                    : "PersonalChronicle.UI.Intensity.RankUnknown".Translate().ToString();
                Widgets.Label(new Rect(card.x + UITheme.CardPadX, card.y + 64f, card.width - UITheme.CardPadX * 2f, 18f), rank);
                Widgets.Label(new Rect(card.x + UITheme.CardPadX, card.y + 84f, card.width - UITheme.CardPadX * 2f, 18f),
                    "PersonalChronicle.UI.Intensity.WorkShare".Translate(
                        Mathf.RoundToInt(row.Share01 * 100f),
                        Mathf.RoundToInt(row.RelativeToMaximum01 * 100f)).ToString());
                GUI.color = prevColor;
                Widgets.FillableBar(new Rect(card.x + UITheme.CardPadX, card.y + 106f, card.width - UITheme.CardPadX * 2f, 6f),
                    Mathf.Clamp01(row.Share01));
            }
            int rows = (cachedIntensityWorkTypes.Count + 1) / 2;
            y += rows * (cardHeight + cardGap);
            }
            finally
            {
                // v4.17 体检：恢复字体/锚点（旧实现循环后强制 Text.Font=Small 而非还原原值）。
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

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

        private static string TranslateIntensityKey(string key, string fallback)
        {
            return string.IsNullOrEmpty(key)
                ? (fallback ?? string.Empty)
                : key.Translate().ToString();
        }

        private static Color ParseIntensityColor(string hex)
        {
            Color color;
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out color))

            {
                return color;
            }
            return ArchiveUiStyle.Info;
        }

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

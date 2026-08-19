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
            Text.Font = GameFont.Tiny;
            GUI.color = UITheme.SecondaryText;
            Widgets.Label(new Rect(rect.x + 6f, y, rect.width - 300f, 18f),
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
                Widgets.Label(new Rect(row.x + 6f, row.y + 4f, row.width - 300f, 22f), line.Label);

                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(row.x + row.width - 280f, row.y + 6f, 90f, 18f), line.Count.ToString());
                Widgets.Label(new Rect(row.x + row.width - 190f, row.y + 6f, 180f, 18f), FormatDate(line.LastTick));

                // Click → jump to the thing's detail (Weapon/Thing nav target).
                if (Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, NavTarget.Weapon, line.StableId, null);
                }
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += RowHeight + 2f;
            }
        }

        private void DrawCareerTab(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            float y = rect.y;
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
            Text.Font = GameFont.Small;
            int rows = (cachedIntensityWorkTypes.Count + 1) / 2;
            y += rows * (cardHeight + cardGap);
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

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

        private static float DrawLifeTimeline(Rect rect, float y, IReadOnlyList<ReadModels.LifePhaseView> phases)
        {
            if (phases == null || phases.Count == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoEvents");
            }
            float x = rect.x + 6f;
            float textW = rect.width - 6f - (LifeTimelineNodeSize + 10f);
            // Pre-compute dynamic node heights with real font line-heights so long
            // Chinese titles wrap and never overlap (must match UIComponents.TimelineNode).
            float[] heights = new float[phases.Count];
            float totalH = 0f;
            for (int i = 0; i < phases.Count; i++)
            {
                ReadModels.LifePhaseView p = phases[i];
                float titleH = Text.CalcHeight(p.PhaseKey.Translate().ToString(), textW);
                float block = titleH;
                if (!string.IsNullOrEmpty(p.DateText)) block += Text.CalcHeight(p.DateText, textW) + UITheme.SpaceXxs;
                if (!string.IsNullOrEmpty(p.SubText)) block += Text.CalcHeight(p.SubText, textW) + UITheme.SpaceXxs;
                float h = Mathf.Max(LifeTimelineNodeSize, block) + UITheme.SpaceXs;
                heights[i] = h;
                totalH += h;
            }

            // Vertical spine connecting the centers of all nodes.
            float spineX = x + LifeTimelineNodeSize / 2f;
            float firstCenterY = y + 2f + LifeTimelineNodeSize / 2f;
            float lastCenterY = y + totalH - heights[phases.Count - 1] + 2f + LifeTimelineNodeSize / 2f;
            float spineH = Mathf.Max(0f, lastCenterY - firstCenterY);
            Color prevColor = GUI.color;
            GUI.color = UITheme.TimelineSpine;
            Widgets.DrawLineVertical(spineX, firstCenterY, spineH);
            GUI.color = prevColor;

            float ny = y;
            for (int i = 0; i < phases.Count; i++)
            {
                ReadModels.LifePhaseView p = phases[i];
                Color dot = TimelinePhaseColor(p.Kind);
                ny = UIComponents.TimelineNode(
                    new Rect(x, ny, rect.width - 6f, heights[i]), ny,
                    p.PhaseKey.Translate().ToString(), p.DateText, p.SubText, dot, out _, p.IconKey);
            }
            return y + totalH;
        }

        private static Color TimelinePhaseColor(ReadModels.LifePhaseKind kind)
        {
            switch (kind)
            {
                case ReadModels.LifePhaseKind.Origin:
                case ReadModels.LifePhaseKind.Join:
                    return UITheme.TimelineJoin;
                case ReadModels.LifePhaseKind.Death:
                    return UITheme.TimelineDeath;
                case ReadModels.LifePhaseKind.Unknown:
                    return UITheme.Dead;
                default:
                    return UITheme.Info;
            }
        }

        private static float DrawCareerBars(Rect rect, float y, IReadOnlyList<ReadModels.CareerBarView> bars)
        {
            if (bars == null || bars.Count == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoWorkData");
            }
            float rowH = 24f;
            for (int i = 0; i < bars.Count; i++)
            {
                ReadModels.CareerBarView b = bars[i];
                string tag = b.IsPrimary ? "PersonalChronicle.UI.CareerPrimary".Translate().ToString()
                    : b.IsSecondary ? "PersonalChronicle.UI.CareerSecondary".Translate().ToString() : "";
                Color tagColor = b.IsPrimary ? UITheme.Accent : UITheme.SecondaryText;
                UIComponents.Label(new Rect(rect.x, y, 150f, 18f),
                    b.WorkTypeLabel + (tag != "" ? " (" + tag + ")" : ""),
                    GameFont.Tiny, tagColor);
                // bar track (width driven by share %)
                float barX = rect.x + 156f;
                float barW = rect.width - 156f - 96f;
                UIComponents.ProgressBar(new Rect(barX, y + (rowH - UITheme.ProgressbarH) / 2f, barW, UITheme.ProgressbarH), b.Share01, UITheme.Accent);
                UIComponents.Label(new Rect(rect.x + rect.width - 92f, y, 90f, 18f),
                    FormatWorkHours(b.Ticks) + " · " + (int)(b.Share01 * 100) + "%",
                    GameFont.Tiny, UITheme.Muted);
                y += rowH;
            }
            return y;
        }

        private static float DrawFootprintLedger(Rect rect, float y, ReadModels.FootprintLedgerView led)
        {
            if (led == null || led.PlaceCount == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoPlaceHistory");
            }
            // summary row
            float cardW = (rect.width - UITheme.GridGap * 2f) / 3f;
            float cardH = UIComponents.StatCellMinHeight;
            UIComponents.StatCell(new Rect(rect.x, y, cardW, cardH),
                "PersonalChronicle.UI.FootprintPlaces".Translate().ToString(), led.PlaceCount.ToString());
            UIComponents.StatCell(new Rect(rect.x + cardW + UITheme.GridGap, y, cardW, cardH),
                "PersonalChronicle.UI.FootprintHome".Translate().ToString() + " · " + led.HomeDays + "d",
                led.HomePlaceText != null ? led.HomePlaceText : "—");
            UIComponents.StatCell(new Rect(rect.x + 2f * (cardW + UITheme.GridGap), y, cardW, cardH),
                "PersonalChronicle.UI.FootprintExpeditions".Translate().ToString(), led.ExpeditionCount.ToString());
            y += cardH + UITheme.GridGap;
            // stays (already sorted longest-first)
            float rowH = 22f;
            int maxRows = Mathf.Min(led.Stays.Count, 6);
            for (int i = 0; i < maxRows; i++)
            {
                ReadModels.FootstepView s = led.Stays[i];
                string icon = s.IsWorldTile ? "🌍" : "🏕️";
                UIComponents.Label(new Rect(rect.x, y, 20f, 18f), icon, GameFont.Small, UITheme.Text);
                UIComponents.Label(new Rect(rect.x + 22f, y, rect.width - 110f, 18f),
                    s.PlaceText + (s.IsHome ? " 〔" + "PersonalChronicle.UI.FootprintHomeTag".Translate().ToString() + "〕" : ""),
                    GameFont.Small, s.IsHome ? UITheme.Accent : UITheme.Text);
                UIComponents.Label(new Rect(rect.x + rect.width - 86f, y, 84f, 18f), s.DwellText,
                    GameFont.Tiny, UITheme.Muted);
                y += rowH;
            }
            return y;
        }

        private static float DrawMilestoneGrid(Rect rect, float y, IReadOnlyList<ReadModels.MilestoneView> ms)
        {
            if (ms == null || ms.Count == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoMilestones");
            }
            float cardW = (rect.width - 8f) / 2f;
            int perRow = 2;
            // First pass: compute adaptive card height from real line-heights.
            float maxH = 56f;
            for (int i = 0; i < ms.Count; i++)
            {
                ReadModels.MilestoneView m = ms[i];
                float titleH = Text.CalcHeight(m.TitleText, cardW - 40f);
                float subH = Text.CalcHeight(m.DateText + " · " + m.SubText, cardW - 40f);
                float h = 6f + 20f + 6f + titleH + 2f + subH + 6f;
                if (h > maxH) maxH = h;
            }
            float cardH = maxH;
            for (int i = 0; i < ms.Count; i++)
            {
                ReadModels.MilestoneView m = ms[i];
                float cx = rect.x + (i % perRow) * (cardW + UITheme.GridGap);
                float cy = y + (i / perRow) * (cardH + UITheme.GridGap);
                UIComponents.Card(new Rect(cx, cy, cardW, cardH), UITheme.Border);
                UIComponents.Label(new Rect(cx + UITheme.CardPadX, cy + 6f, 20f, 20f), m.IconKey, GameFont.Medium, UITheme.Text);
                float titleH = Text.CalcHeight(m.TitleText, cardW - 40f);
                UIComponents.Label(new Rect(cx + 32f, cy + 6f, cardW - 40f, titleH), m.TitleText,
                    GameFont.Small, UITheme.Text);
                UIComponents.Label(new Rect(cx + 32f, cy + 6f + 20f + 2f, cardW - 40f,
                        cardH - (6f + 20f + 2f + 6f)),
                    m.DateText + " · " + m.SubText, GameFont.Tiny, UITheme.Muted);
            }
            int rows = (ms.Count + perRow - 1) / perRow;
            return y + rows * (cardH + UITheme.GridGap);
        }

        private static float DrawKeyEventStream(Rect rect, float y, IReadOnlyList<ReadModels.KeyEventView> evs)
        {
            if (evs == null || evs.Count == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoEvents");
            }
            for (int i = 0; i < evs.Count; i++)
            {
                ReadModels.KeyEventView e = evs[i];
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                UIComponents.Rule(new Rect(row.x, row.y + 1f, 3f, TimelineRowHeight - 2f),
                    e.IsHighlight ? UITheme.Accent : UITheme.BorderSoft);
                UIComponents.Label(new Rect(row.x + 10f, row.y + 4f, 120f, 18f), e.DateText,
                    GameFont.Tiny, UITheme.Dim);
                UIComponents.Label(new Rect(row.x + 136f, row.y + 4f, row.width - 136f - 70f, 20f),
                    (e.IsHighlight ? "✦ " : "") + e.TitleText, GameFont.Small, UITheme.Text);
                UIComponents.Label(new Rect(row.x + row.width - 66f, row.y + 4f, 64f, 18f), e.TypeText,
                    GameFont.Tiny, UITheme.Muted);
                UIComponents.Rule(new Rect(row.x, row.yMax - 1f, row.width, 1f), UITheme.BorderSoft);
                y += TimelineRowHeight;
            }
            return y;
        }

        private static float DrawEmptyLine(Rect rect, float y, string key)
        {
            UIComponents.Label(new Rect(rect.x, y, rect.width, 22f), key.Translate().ToString(),
                GameFont.Small, UITheme.Muted);
            return y + 26f;
        }

        private float DrawPlaceHistoryTable(Rect rect, float y, PawnObject pawn, int maxRows)
        {
            Color prevColor = GUI.color;
            if (pawn == null || pawn.PlaceHistory == null || pawn.PlaceHistory.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.SecondaryText;
                Widgets.Label(new Rect(rect.x, y, rect.width, 18f),
                    "PersonalChronicle.UI.NoPlaceHistory".Translate().ToString());
                GUI.color = prevColor;
                return y + 22f;
            }
            Text.Font = GameFont.Tiny;
            GUI.color = UITheme.SecondaryText;
            Widgets.Label(new Rect(rect.x + 6f, y, rect.width * 0.4f, 16f),
                "PersonalChronicle.UI.PlaceName".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width * 0.42f, y, rect.width * 0.28f, 16f),
                "PersonalChronicle.UI.PlaceEnter".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width * 0.72f, y, rect.width * 0.26f, 16f),
                "PersonalChronicle.UI.PlaceLeave".Translate().ToString());
            GUI.color = prevColor;
            y += 18f;
            int shown = 0;
            for (int i = pawn.PlaceHistory.Count - 1; i >= 0 && shown < maxRows; i--)
            {
                PlaceVisit v = pawn.PlaceHistory[i];
                if (v == null)
                {
                    continue;
                }
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x + 6f, y, rect.width * 0.4f, 20f), FormatPlaceKey(v));
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(rect.x + rect.width * 0.42f, y + 2f, rect.width * 0.28f, 18f), FormatDate(v.EnterTick));
                string leave = v.IsOpen
                    ? "PersonalChronicle.UI.PlaceStillThere".Translate().ToString()
                    : FormatDate(v.LeaveTick);
                Widgets.Label(new Rect(rect.x + rect.width * 0.72f, y + 2f, rect.width * 0.26f, 18f), leave);
                y += 22f;
                shown++;
            }
            return y;
        }

        private static string FormatPlaceKey(PlaceVisit v)
        {
            if (v == null || string.IsNullOrEmpty(v.PlaceKey))
            {
                return "—";
            }
            if (v.PlaceKind == PlaceVisitKeys.KindCaravan
                || v.PlaceKey.StartsWith(PlaceVisitKeys.TileKeyPrefix, System.StringComparison.Ordinal))
            {
                string tile = v.PlaceKey.StartsWith(PlaceVisitKeys.TileKeyPrefix, System.StringComparison.Ordinal)
                    ? v.PlaceKey.Substring(PlaceVisitKeys.TileKeyPrefix.Length)
                    : v.PlaceKey;
                return "PersonalChronicle.UI.PlacesWorldTile".Translate(tile).ToString();
            }
            return BiomeLabel(v.PlaceKey);
        }

        private static string FormatWorkHours(long ticks)
        {
            float hours = ticks / (float)RimWorld.GenDate.TicksPerHour;
            return hours.ToString("0.0") + " h";
        }

        private static string FormatMarketValue(float value)
        {
            return "PersonalChronicle.UI.MarketValueFormat".Translate(FormatSilver(value)).ToString();
        }

        private static string FormatSilver(float value)
        {
            if (value < 1000f)
            {
                return Mathf.RoundToInt(value).ToString();
            }
            float scaled;
            string suffix;
            if (value >= 1e9f) { scaled = value / 1e9f; suffix = "B"; }
            else if (value >= 1e6f) { scaled = value / 1e6f; suffix = "M"; }
            else { scaled = value / 1e3f; suffix = "K"; }
            return scaled.ToString("0.##") + suffix;
        }

        private static void DrawMetricCard(Rect rect, string label, string value, string subLabel)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
                ArchiveUiStyle.DrawCard(rect, ArchiveUiStyle.Info);
                bool large = rect.height >= 80f;
                // CJK-safe line heights: Tiny >= 18f, Medium >= 28f. For a 72f card
                // the block is 8 + 18 + 28 + 18 = 72f exactly.
                float labelH = 18f;
                float valueH = 28f;
                float subLabelH = 18f;
                float labelY = rect.y + UITheme.CardPadY;
                float valueY = labelY + labelH + (large ? 4f : 0f);
                float subLabelY = valueY + valueH + (large ? 4f : 0f);

                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(rect.x + UITheme.CardPadX, labelY, rect.width - UITheme.CardPadX * 2f, labelH), label);
                Text.Font = GameFont.Medium;
                GUI.color = ArchiveUiStyle.Text;
                Widgets.Label(new Rect(rect.x + UITheme.CardPadX, valueY, rect.width - UITheme.CardPadX * 2f, valueH), value);
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(rect.x + UITheme.CardPadX, subLabelY, rect.width - UITheme.CardPadX * 2f, subLabelH), subLabel);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        private static string BackstoryLabel(PawnObject pawn)
        {
            if (pawn == null)
            {
                return string.Empty;
            }
            string child = BackstoryDefLabel(pawn.ChildhoodBackstoryDefName);
            string adult = BackstoryDefLabel(pawn.AdulthoodBackstoryDefName);
            if (string.IsNullOrEmpty(child) && string.IsNullOrEmpty(adult))
            {
                return "—";
            }
            if (string.IsNullOrEmpty(child))
            {
                return adult;
            }
            if (string.IsNullOrEmpty(adult))
            {
                return child;
            }
            return child + " → " + adult;
        }

        private static string BackstoryDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            BackstoryDef def = DefDatabase<BackstoryDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                // Prefer Def label; fall back to defName (title fields vary by version).
                if (!string.IsNullOrEmpty(def.label))
                {
                    return def.label;
                }
                string titled = def.TitleFor(Gender.Male);
                if (!string.IsNullOrEmpty(titled))
                {
                    return titled;
                }
            }
            return defName;
        }

        private void DrawDetailStats(Rect rect)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Stats".Translate().ToString());

            DrawStatCell(new Rect(rect.x, y, 150f, 60f), "PersonalChronicle.UI.EventCount".Translate().ToString(), cachedDetailEvents.Count);
            DrawStatCell(new Rect(rect.x + 160f, y, 150f, 60f), "PersonalChronicle.UI.LinkedCount".Translate().ToString(), cachedLinkedObjects.Count);
            y += 70f;

            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.FirstEvent".Translate().ToString(),
                cachedDetailEvents.Count > 0 ? cachedDetailEvents[0].DateText : "PersonalChronicle.UI.UnknownDate".Translate().ToString());
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.LastEvent".Translate().ToString(),
                cachedDetailEvents.Count > 0 ? cachedDetailEvents[cachedDetailEvents.Count - 1].DateText : "PersonalChronicle.UI.UnknownDate".Translate().ToString());
        }

        private void DrawNoLiveData(Rect rect)
        {
            Color prevColor = GUI.color;
            Text.Font = GameFont.Small;
            GUI.color = UITheme.SecondaryText;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f),
                "PersonalChronicle.UI.NoLiveData".Translate().ToString());
            GUI.color = prevColor;
        }

        private static string ResolveBackstoryLabel(string backstoryDefName)
        {
            if (string.IsNullOrEmpty(backstoryDefName)) return "";
            var def = DefDatabase<BackstoryDef>.GetNamedSilentFail(backstoryDefName);
            return def != null ? def.title : backstoryDefName;
        }

        private static string FormatDays(double days)
        {
            return days.ToString("0.0");
        }


    }
}

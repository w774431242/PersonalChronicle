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
    /// <summary>Partial of ArchiveMainTabWindow 鈥?PawnDetail view drawing (BUG-BASE-01 refactor).</summary>
    public sealed partial class ArchiveMainTabWindow
    {
        private void DrawPawnOverview(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            if (!(cachedDetailObject is PawnObject pawn))
            {
                return;
            }

            // v4.6.1: contribution-archive layout (matches contribution-archive-overview.html).
            // 0) Cover header: portrait + name + role description + stamp + verdict.
            // 1) Ledger header: 3-cell榨取总账 (output value / work hours / weekly yield).
            // 2) Output ledger: ProgressBar rows by production type.
            // 3) Health residual: 4 StatCells + 3 dim bars + depreciation event log + verdict.
            // 4) Medal wall (勋章墙): highest reached tier per series (§6.9).
            y = DrawCoverHeader(rect, y, pawn, service);
            y = DrawLedger(rect, y + UITheme.BlockGap, pawn);
            y = DrawOutputLedger(rect, y + UITheme.BlockGap);
            y = DrawHealthValuation(rect, y + UITheme.BlockGap, cachedHealth);
            y = DrawMedalWall(rect, y + UITheme.BlockGap, cachedMedals);
        }

        // ---- v4.4 Overview derived draw helpers ----

        private const float LifeTimelineNodeSize = 18f;


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

        /// <summary>
        /// P2: overview KPI uses the same combat caches as CombatLog tab.
        /// </summary>

        private void CountCombatKpis(out int battleCount, out int killCount)
        {
            battleCount = cachedBattleLines != null ? cachedBattleLines.Count : 0;
            killCount = cachedKillLines != null ? cachedKillLines.Count : 0;
        }


        private int CountLinkedPawns()
        {
            int n = 0;
            for (int i = 0; i < cachedLinkedObjects.Count; i++)
            {
                if (!string.IsNullOrEmpty(cachedLinkedObjects[i].StableId)

                    && cachedLinkedObjects[i].CategoryKey == ArchiveCategoryKeys.Pawn)
                {
                    n++;
                }
            }
            return n;
        }


        private int CountSignificantRelations(PawnObject pawn)
        {
            if (pawn != null && pawn.Relations != null)
            {
                int n = 0;
                for (int i = 0; i < pawn.Relations.Count; i++)
                {
                    SignificantRelation r = pawn.Relations[i];
                    if (r != null && r.IsActive)
                    {
                        n++;
                    }
                }
                if (n > 0)
                {
                    return n;
                }
            }
            // Fall back to event-co-occurrence people when no snapshots yet.
            return CountLinkedPawns();
        }

        /// <summary>
        /// Renders the last <paramref name="maxRows"/> place visits (newest first).
        /// </summary>

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

        // v4.6.8: abbreviate large silver values with K/M/B magnitude suffixes
        // (e.g. 1840 → "1.8K", 2_300_000 → "2.3M"). Keeps the unit sub-label intact.

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


        private void DrawPawnCombat(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            if (cachedDetailObject is PawnObject pawn && pawn.IsArchived)
            {
                DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.DeathDossier".Translate().ToString());
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.DeathDate".Translate().ToString(), FormatDate(pawn.DeathTick));
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.DeathCause".Translate().ToString(), CauseLabel(pawn.DeathCauseKey));
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Killer".Translate().ToString(),
                    string.IsNullOrEmpty(cachedDeathKiller) ? "PersonalChronicle.UI.UnknownDate".Translate().ToString() : cachedDeathKiller);
                // v4.14: 关联战役（死亡事件挂 battle 边时显示）。
                if (!string.IsNullOrEmpty(cachedDeathBattleLabel))
                {
                    y = DrawDetailRow(rect.x, y, rect.width,
                        "PersonalChronicle.UI.DeathBattle".Translate().ToString(),
                        cachedDeathBattleLabel);
                }
                y += 10f;
            }

            // v4.3: faction-codex cards (kills aggregated by faction).
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.FactionCodexTitle".Translate().ToString());
            y = DrawFactionCodex(rect, y, service);
        }


        private void DrawWeaponCraft(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            float y = rect.y;
            if (!string.IsNullOrEmpty(cachedCraftCrafterId))
            {
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Crafter".Translate().ToString(),
                    string.IsNullOrEmpty(cachedCraftCrafterLabel) ? cachedCraftCrafterId : cachedCraftCrafterLabel);
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.CraftedAt".Translate().ToString(), FormatDate(cachedCraftTick));
                y += 10f;
            }
            else
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoCraftRecord".Translate().ToString());
                y += 28f;
            }

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Timeline".Translate().ToString());
            for (int i = 0; i < cachedDetailEvents.Count; i++)
            {
                EventLineView line = cachedDetailEvents[i];
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                DrawEventRow(row, line.DateText, line.NameText, line.ParamsText);
                y += TimelineRowHeight;
            }
        }


        private void DrawPawnItems(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.HeldItems".Translate().ToString());

            int count = 0;
            for (int i = 0; i < cachedLinkedObjects.Count; i++)
            {
                LinkedObjectView link = cachedLinkedObjects[i];
                if (link.CategoryKey != ArchiveCategoryKeys.Thing)
                {
                    continue;
                }
                count++;
                Rect row = new Rect(rect.x, y, rect.width, RowHeight - 4f);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + 6f, row.y + 4f, row.width - 200f, 22f), link.Label);
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.SecondaryText;
                Widgets.Label(new Rect(row.x + row.width - 196f, row.y + 6f, 190f, 18f), link.CategoryLabel);
                GUI.color = prevColor;
                Text.Font = GameFont.Small;
                if (link.Target != NavTarget.None && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, link.Target, link.StableId, null);
                }
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += RowHeight + 2f;
            }

            if (count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoRelated".Translate().ToString());
            }
        }

        // P3-2: Pawn/Weapon stat panels were identical — merged into one.

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


        private void DrawWeaponOverview(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            if (!(cachedDetailObject is ThingObject thing))
            {
                return;
            }

            // v4.14: KPI 4 cells (events / kills / holders / crafter) matching the
            // preview's weapon Overview. Counters come from the cached Read-Model
            // views (never re-derived in the draw path).
            float kpiH = UIComponents.StatCellMinHeight;
            float gap = UITheme.GridGap;
            float kpiW = (rect.width - UITheme.CardPadX * 2f - gap * 3f) / 4f;
            float kpiX = rect.x + UITheme.CardPadX;
            string eventsValue = cachedDetailEvents.Count.ToString();
            string killsValue = cachedKillLines.Count.ToString();
            string holdersValue = cachedLegacy != null ? cachedLegacy.GenCount.ToString() : "—";
            string crafterValue = string.IsNullOrEmpty(cachedCraftCrafterLabel)
                ? (string.IsNullOrEmpty(cachedCraftCrafterId) ? "—" : cachedCraftCrafterId)
                : cachedCraftCrafterLabel;
            UIComponents.StatCell(new Rect(kpiX, y, kpiW, kpiH),
                "PersonalChronicle.UI.KpiEvents".Translate().ToString(), eventsValue);
            UIComponents.StatCell(new Rect(kpiX + (kpiW + gap), y, kpiW, kpiH),
                "PersonalChronicle.UI.KpiKills".Translate().ToString(), killsValue);
            UIComponents.StatCell(new Rect(kpiX + 2f * (kpiW + gap), y, kpiW, kpiH),
                "PersonalChronicle.UI.KpiHolders".Translate().ToString(), holdersValue);
            UIComponents.StatCell(new Rect(kpiX + 3f * (kpiW + gap), y, kpiW, kpiH),
                "PersonalChronicle.UI.KpiCrafter".Translate().ToString(), crafterValue);
            y += kpiH + UITheme.BlockGap;

            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Type".Translate().ToString(), ThingDefLabel(thing.ThingDefName));
            if (!string.IsNullOrEmpty(cachedCraftCrafterId))
            {
                string crafter = string.IsNullOrEmpty(cachedCraftCrafterLabel) ? cachedCraftCrafterId : cachedCraftCrafterLabel;
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Crafter".Translate().ToString(), crafter);
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.CraftedAt".Translate().ToString(), FormatDate(cachedCraftTick));
            }
            else
            {
                y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Crafter".Translate().ToString(),
                    "PersonalChronicle.UI.NoCraftRecord".Translate().ToString());
            }
            y += 10f;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Timeline".Translate().ToString());
            int start = Mathf.Max(0, cachedDetailEvents.Count - 4);
            for (int i = start; i < cachedDetailEvents.Count; i++)
            {
                EventLineView line = cachedDetailEvents[i];
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                DrawEventRow(row, line.DateText, line.NameText, line.ParamsText);
                y += TimelineRowHeight;
            }
            if (cachedDetailEvents.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoEvents".Translate().ToString());
            }
        }

        // ---- Detail: live-read tabs (Skills / Health / Relations) --------------


        private void DrawSkills(Rect rect, IArchiveService service)
        {
            Pawn pawn = service.GetLivePawn(detailObjectId);
            if (pawn == null || pawn.skills == null || pawn.skills.skills == null)
            {
                DrawNoLiveData(rect);
                return;
            }

            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Skills".Translate().ToString());

            List<SkillRecord> skills = pawn.skills.skills;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillRecord skill = skills[i];
                if (skill == null || skill.def == null)
                {
                    continue;
                }
                Rect row = new Rect(rect.x, y, rect.width, 24f);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x, row.y, 140f, 20f), skill.def.label);

                Rect bar = new Rect(row.x + 148f, row.y + 4f, row.width - 148f - 56f, 14f);
                Widgets.FillableBar(bar, Mathf.Clamp01(skill.Level / 20f));

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + row.width - 52f, row.y, 48f, 20f), skill.Level.ToString());
                y += 28f;
            }
        }


        private void DrawHealth(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
            Pawn pawn = service.GetLivePawn(detailObjectId);
            if (pawn == null || pawn.health == null)
            {
                DrawNoLiveData(rect);
                return;
            }

            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.OverallHealth".Translate().ToString());

            float summary = pawn.health.summaryHealth != null ? pawn.health.summaryHealth.SummaryHealthPercent : 1f;
            string healthLabel;
            if (summary > HealthGoodThreshold)
            {
                healthLabel = "PersonalChronicle.UI.HealthGood".Translate().ToString();
            }
            else if (summary > HealthInjuredThreshold)
            {
                healthLabel = "PersonalChronicle.UI.HealthInjured".Translate().ToString();
            }
            else
            {
                healthLabel = "PersonalChronicle.UI.HealthCritical".Translate().ToString();
            }
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.Status".Translate().ToString(), healthLabel);
            y += 8f;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Hediffs".Translate().ToString());

            List<Hediff> hediffs = pawn.health.hediffSet != null ? pawn.health.hediffSet.hediffs : null;
            if (hediffs == null || hediffs.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoHediffs".Translate().ToString());
                return;
            }

            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff == null)
                {
                    continue;
                }
                string label = hediff.LabelBase;
                if (string.IsNullOrEmpty(label) && hediff.def != null)
                {
                    label = hediff.def.label;
                }
                if (string.IsNullOrEmpty(label))
                {
                    continue;
                }
                string part = string.Empty;
                if (hediff.Part != null && hediff.Part.def != null)
                {
                    part = hediff.Part.def.label;
                }
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + 4f, row.y + 3f, row.width - 200f, 20f), label);
                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.SecondaryText;
                Widgets.Label(new Rect(row.x + row.width - 196f, row.y + 6f, 192f, 18f), part);
                GUI.color = prevColor;
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += TimelineRowHeight;
            }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }


        private void DrawRelations(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Relations".Translate().ToString());

            List<RelationRowView> rows = BuildRelationRows(service);
            if (rows.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoRelations".Translate(), UITheme.FontBody, UITheme.Text);
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                RelationRowView row = rows[i];
                Rect rowRect = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                UIComponents.Label(new Rect(rowRect.x + 4f, rowRect.y + 3f, rowRect.width - 260f, 20f),
                    row.OtherLabel, UITheme.FontBody, UITheme.Text);
                UIComponents.Label(new Rect(rowRect.x + rowRect.width - 256f, rowRect.y + 6f, 180f, 18f),
                    row.RelationLabel, UITheme.FontLabel, UITheme.SecondaryText);
                UIComponents.Label(new Rect(rowRect.x + rowRect.width - 72f, rowRect.y + 6f, 68f, 18f),
                    row.StatusLabel, UITheme.FontLabel, UITheme.SecondaryText);
                Widgets.DrawLineHorizontal(rowRect.x, rowRect.yMax, rowRect.width);
                y += TimelineRowHeight;
            }
        }

        private struct RelationRowView
        {
            public string OtherLabel;
            public string RelationLabel;
            public string StatusLabel;
        }

        /// <summary>
        /// v4.6: merge live direct relations with archived snapshots so the Social
        /// tab shows initial ties (spouse/parent/friend at join time) even for dead
        /// or departed pawns. Live state wins when both sources describe the same pair.
        /// </summary>

        private List<RelationRowView> BuildRelationRows(IArchiveService service)
        {
            List<RelationRowView> rows = new List<RelationRowView>();
            HashSet<string> seen = new HashSet<string>();
            PawnObject record = service.GetObject(detailObjectId) as PawnObject;
            Pawn livePawn = service.GetLivePawn(detailObjectId);

            // 1) Live relations (current state).
            if (livePawn?.relations?.DirectRelations != null)
            {
                List<DirectPawnRelation> directRelations = livePawn.relations.DirectRelations;
                for (int i = 0; i < directRelations.Count; i++)
                {
                    DirectPawnRelation rel = directRelations[i];
                    if (rel?.def == null || rel.otherPawn == null)
                    {
                        continue;
                    }
                    if (!SocialRelationFilter.IsSignificant(rel.def))
                    {
                        continue;
                    }
                    string otherId = rel.otherPawn.GetUniqueLoadID();
                    string key = MakeRelationKey(rel.def.defName, otherId);
                    if (!seen.Add(key))
                    {
                        continue;
                    }
                    rows.Add(new RelationRowView
                    {
                        OtherLabel = rel.otherPawn.LabelShort,
                        RelationLabel = RelationLabelFor(rel.def, rel.otherPawn),
                        StatusLabel = rel.otherPawn.Dead
                            ? "PersonalChronicle.UI.Dead".Translate().ToString()
                            : "PersonalChronicle.UI.Alive".Translate().ToString()
                    });
                }
            }

            // 2) Archived relations (historical / initial ties, includes dead/departed pawns).
            if (record?.Relations != null)
            {
                for (int i = 0; i < record.Relations.Count; i++)
                {
                    SignificantRelation rel = record.Relations[i];
                    if (rel == null)
                    {
                        continue;
                    }
                    string key = MakeRelationKey(rel.RelationDefName, rel.OtherStableId);
                    if (!seen.Add(key))
                    {
                        continue;
                    }
                    Pawn otherLive = service.GetLivePawn(rel.OtherStableId);
                    bool otherDead = otherLive != null && otherLive.Dead;
                    string status = otherDead
                        ? "PersonalChronicle.UI.Dead".Translate().ToString()
                        : (rel.IsActive
                            ? "PersonalChronicle.UI.Alive".Translate().ToString()
                            : "PersonalChronicle.UI.RelEnded".Translate().ToString());
                    PawnRelationDef def = DefDatabase<PawnRelationDef>.GetNamedSilentFail(rel.RelationDefName);
                    rows.Add(new RelationRowView
                    {
                        OtherLabel = !string.IsNullOrEmpty(rel.OtherLabel) ? rel.OtherLabel : rel.OtherStableId,
                        RelationLabel = RelationLabelFor(def, otherLive),
                        StatusLabel = status
                    });
                }
            }

            return rows;
        }


        private static string MakeRelationKey(string relationDefName, string otherStableId)
        {
            return (relationDefName ?? string.Empty) + "::" + (otherStableId ?? string.Empty);
        }


        private static string RelationLabelFor(PawnRelationDef def, Pawn otherPawn)
        {
            if (def == null)
            {
                return string.Empty;
            }
            if (otherPawn != null)
            {
                string gendered = def.GetGenderSpecificLabel(otherPawn);
                if (!string.IsNullOrEmpty(gendered))
                {
                    return gendered;
                }
            }
            if (!string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            return def.defName;
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

        // ---- Detail: production tab (LD-1) -----------------------------------


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

        // ---- Detail: live tabs (LD-2/3/4) ------------------------------------

        /// <summary>
        /// v4.0 Career: intensity summary + work ledger + production + skill archive.
        /// </summary>

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

        /// <summary>
        /// Shared intensity hero card used by both Overview (v4.5.2) and Career tab.
        /// </summary>

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

        // ---- 健康残值 · 资产折旧 (window renders only; derivation in ReadModels) ----


        private float DrawHealthValuation(Rect rect, float y, ReadModels.HealthView h)
        {
            // v4.6.1: full HTML-style layout — 4 StatCells + 3 dim bars + event log + verdict.
            float headH = 26f;
            float statRowH = 80f;
            float dimRowH = 16f;
            float dimBlockH = dimRowH * 3f + 8f;
            float eventHeaderH = 18f;
            int evCount = h.Events != null ? Mathf.Min(h.Events.Count, 6) : 0;
            // v4.6.3: 事件行高 22f 适配中文 GameFont.Tiny。
            float eventsH = evCount > 0 ? (evCount * 22f + 4f) : 18f;
            float blockH = headH + statRowH + 8f + dimBlockH + 8f + eventHeaderH + eventsH + 6f;
            Rect block = new Rect(rect.x, y, rect.width, blockH);

            UIComponents.DrawSubsectionHeader(block.TopPartPixels(headH),
                HealthValuationKeys.Title);

            if (!h.IsDefined)
            {
                Rect empty = new Rect(block.x + UITheme.CardPadX, block.y + headH + UITheme.GridGap, block.width - UITheme.CardPadX * 2f, 28f);
                Color prevColor = GUI.color;
                GameFont prevFont = Text.Font;
                TextAnchor prevAnchor = Text.Anchor;
                GUI.color = UITheme.Dim;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(empty, HealthValuationKeys.NoData.Translate().ToString());
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
                return block.yMax;
            }

            Color accent = h.IsImpaired ? UITheme.Blood : UITheme.Alive;

            // === Row 1: 4 StatCells (silver value / composite score / body% / weekly yield) ===
            float statX = block.x + UITheme.CardPadX;
            float statGap = UITheme.GridGap;
            float statW = (block.width - UITheme.CardPadX * 2f - statGap * 3f) / 4f;
            float statY = block.y + headH + 6f;
            UIComponents.StatCell(new Rect(statX, statY, statW, statRowH),
                HealthValuationKeys.SilverValue.Translate().ToString(),
                FormatSilver(h.SilverValue),
                h.IsImpaired ? UITheme.Blood : UITheme.PillGold,
                HealthValuationKeys.BaseValue.Translate(FormatSilver(h.BaseSilverValue)).ToString());
            UIComponents.StatCell(new Rect(statX + (statW + statGap), statY, statW, statRowH),
                HealthValuationKeys.Score.Translate().ToString(),
                Mathf.RoundToInt(h.HealthScore).ToString(),
                accent);
            UIComponents.StatCell(new Rect(statX + 2f * (statW + statGap), statY, statW, statRowH),
                HealthValuationKeys.Body.Translate().ToString(),
                Mathf.RoundToInt(h.BodyPercent * 100f).ToString() + "%",
                accent);
            UIComponents.StatCell(new Rect(statX + 3f * (statW + statGap), statY, statW, statRowH),
                HealthValuationKeys.WeeklyYield.Translate().ToString(),
                FormatSilver(h.WeeklySilverEstimate),
                UITheme.PillGold,
                "PersonalChronicle.UI.Ledger.WeeklyUnit".Translate().ToString(),
                inlineSubLabel: true);

            // === Row 2: 3 dim bars (Body / Spirit / Youth) ===
            float dimY = statY + statRowH + 8f;
            DrawHealthDimBar(new Rect(statX, dimY, block.width - UITheme.CardPadX * 2f, dimRowH),
                HealthValuationKeys.DimBody.Translate().ToString(),
                h.BodyIntegrityScore, h.BodyFactors, HealthValuationKeys.DimBody);
            DrawHealthDimBar(new Rect(statX, dimY + dimRowH, block.width - UITheme.CardPadX * 2f, dimRowH),
                HealthValuationKeys.DimSpirit.Translate().ToString(),
                h.SpiritScore, h.SpiritFactors, HealthValuationKeys.DimSpirit);
            DrawHealthDimBar(new Rect(statX, dimY + 2f * dimRowH, block.width - UITheme.CardPadX * 2f, dimRowH),
                HealthValuationKeys.DimYouth.Translate().ToString(),
                h.YouthScore, h.YouthFactors, HealthValuationKeys.DimYouth);

            // === Row 3: depreciation event log ===
            float evY = dimY + dimBlockH + 4f;
            UIComponents.Label(new Rect(statX, evY, block.width - UITheme.CardPadX * 2f, eventHeaderH),
                HealthValuationKeys.TipHeader.Translate().ToString(),
                GameFont.Tiny, UITheme.SecondaryText);
            if (evCount == 0)
            {
                UIComponents.Label(new Rect(statX, evY + eventHeaderH, block.width - UITheme.CardPadX * 2f, 18f),
                    HealthValuationKeys.NoEvents.Translate().ToString(),
                    GameFont.Tiny, UITheme.Dim);
            }
            else
            {
                // 中文 GameFont.Tiny 行高 ≈ 22f，用 22f 避免字体重叠截断。
                const float lineH = 22f;
                for (int i = 0; i < evCount; i++)
                {
                    ReadModels.HealthEventView e = h.Events[i];
                    if (e == null) continue;
                    string impact = e.Impact == 0 ? "" : ("  " + FormatSilver(e.Impact) + " 银");
                    string tag = string.IsNullOrEmpty(e.TagText) ? "" : ("  [" + e.TagText + "]");
                    string line = e.DateText + "  " + e.Description + impact + tag;
                    UIComponents.Label(new Rect(statX, evY + eventHeaderH + i * lineH,
                        block.width - UITheme.CardPadX * 2f, lineH),
                        line,
                        GameFont.Tiny,
                        e.Impact < 0 ? UITheme.Blood : UITheme.Muted,
                        TextAnchor.MiddleLeft);
                }
            }

            // v4.14: closing verdict blurb (health residual conclusion). Rendered
            // below the event log; height grows by one block so the scroll region
            // stays honest.
            if (!string.IsNullOrEmpty(h.VerdictText))
            {
                float verdictH = 34f;
                Rect verdict = new Rect(block.x, block.yMax, block.width, verdictH);
                Color prevColor2 = GUI.color;
                GUI.color = UITheme.PanelRaised;
                Widgets.DrawBoxSolid(verdict, UITheme.PanelRaised);
                GUI.color = prevColor2;
                UIComponents.Border(verdict, UITheme.BorderSoft);
                UIComponents.Label(new Rect(verdict.x + UITheme.CardPadX, verdict.y + 6f,
                    verdict.width - UITheme.CardPadX * 2f, 20f),
                    HealthValuationKeys.Verdict.Translate().ToString()
                        + " " + h.VerdictText,
                    GameFont.Tiny,
                    h.IsImpaired ? UITheme.Blood : (h.IsPrime ? UITheme.Alive : UITheme.Text),
                    TextAnchor.UpperLeft);
                return verdict.yMax;
            }

            // Per-element hover tips are handled by DrawHealthDimBar (factors) and
            // the event rows below. Do NOT register a whole-block tooltip here, or
            // it swallows the per-dimension tooltips.
            return block.yMax;
        }


        // ---- Medal wall (勋章墙, v1.1.4) ----
        // §6.9: 同 SeriesKey 只画最高已达档位；未授予但当前达标的也显示（灰态+进度）。
        // 窗口只消费 ReadModel 快照，不在此做任何判定/排序。

        private const float MedalWallCardH = 84f;

        private static int MedalWallVisibleCount(IReadOnlyList<ReadModels.MedalView> medals)
        {
            if (medals == null) return 0;
            int count = 0;
            for (int i = 0; i < medals.Count; i++)
            {
                ReadModels.MedalView m = medals[i];
                if (m != null && m.IsApplicable && m.IsHighestTier) count++;
            }
            return count;
        }

        private static float MedalWallHeight(IReadOnlyList<ReadModels.MedalView> medals)
        {
            int count = MedalWallVisibleCount(medals);
            if (count == 0) return UITheme.SectionTitleHeight + 28f;
            int rows = Mathf.CeilToInt(count / 2f);
            return UITheme.SectionTitleHeight + rows * MedalWallCardH + (rows - 1) * UITheme.GridGap;
        }

        private float DrawMedalWall(Rect rect, float y, IReadOnlyList<ReadModels.MedalView> medals)
        {
            DrawSectionTitle(rect, ref y, PersonalChronicle.Application.MedalTranslationKeys.WallTitle().Translate().ToString());

            // §6.9: 只画 IsApplicable && IsHighestTier（最高已达档位归并已由 ReadModel 完成）。
            List<ReadModels.MedalView> shown = null;
            if (medals != null)
            {
                for (int i = 0; i < medals.Count; i++)
                {
                    ReadModels.MedalView m = medals[i];
                    if (m == null || !m.IsApplicable || !m.IsHighestTier) continue;
                    if (shown == null) shown = new List<ReadModels.MedalView>();
                    shown.Add(m);
                }
            }

            if (shown == null || shown.Count == 0)
            {
                Color prevColor = GUI.color;
                GameFont prevFont = Text.Font;
                TextAnchor prevAnchor = Text.Anchor;
                try
                {
                    GUI.color = UITheme.Dim;
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                    Widgets.Label(new Rect(rect.x + UITheme.CardPadX, y, rect.width - UITheme.CardPadX * 2f, 28f),
                        PersonalChronicle.Application.MedalTranslationKeys.WallEmpty().Translate().ToString());
                }
                finally
                {
                    GUI.color = prevColor;
                    Text.Font = prevFont;
                    Text.Anchor = prevAnchor;
                }
                return y + 28f;
            }

            const int cols = 2;
            float gap = UITheme.GridGap;
            float cardW = (rect.width - UITheme.CardPadX * 2f - gap) / cols;
            int rows = Mathf.CeilToInt(shown.Count / (float)cols);
            for (int i = 0; i < shown.Count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                float cx = rect.x + UITheme.CardPadX + col * (cardW + gap);
                float cy = y + row * (MedalWallCardH + gap);
                DrawMedalCard(new Rect(cx, cy, cardW, MedalWallCardH), shown[i]);
            }
            return y + rows * MedalWallCardH + (rows - 1) * gap;
        }

        private static void DrawMedalCard(Rect card, ReadModels.MedalView m)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                Color tierColor = UITheme.MedalTierColor(m.Tier);
                UIComponents.Border(card, UITheme.BorderSoft);

                // 徽章色块 + 称号
                Rect badge = new Rect(card.x + 8f, card.y + 8f, 14f, 16f);
                GUI.color = tierColor;
                Widgets.DrawBoxSolid(badge, tierColor);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect labelRect = new Rect(card.x + 28f, card.y + 6f, card.width - 76f, 24f);
                Widgets.Label(labelRect, m.Label);

                // 档位 Pill（右上）
                Rect pill = new Rect(card.x + card.width - 54f, card.y + 6f, 46f, 18f);
                UIComponents.Pill(pill,
                    PersonalChronicle.Application.MedalTranslationKeys.Tier(m.Tier).Translate().ToString(),
                    tierColor);

                // 进度条
                Rect bar = new Rect(card.x + 8f, card.y + 34f, card.width - 16f, UITheme.ProgressbarH);
                UIComponents.ProgressBar(bar, m.Progress, tierColor);

                // 当前值/阈值（未授予但已达标时用 ProgressBar caption 承载）
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Rect cap = new Rect(card.x + 8f, bar.yMax + 2f, card.width - 16f, 16f);
                GUI.color = UITheme.Muted;
                string ratio = FormatMedalValue(m.CurrentValue) + " / " + FormatMedalValue(m.Threshold);
                Widgets.Label(cap, ratio);

                // 通道 B 增益文案就近展示
                if (!string.IsNullOrEmpty(m.BuffText))
                {
                    GUI.color = tierColor;
                    Text.Anchor = TextAnchor.UpperLeft;
                    Widgets.Label(new Rect(card.x + 8f, cap.yMax + 1f, card.width - 16f, 18f), m.BuffText);
                }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private static string FormatMedalValue(double value)
        {
            if (value >= 10000d) return (value / 1000d).ToString("0.#") + "K";
            return value.ToString("0.#");
        }


        private static void DrawHealthDimBar(Rect rect, string label, float score01to100,
            IReadOnlyList<ReadModels.HealthFactorView> factors, string dimKey)
        {
            // v4.6.5: fixed-size progress bar matching the output-ledger layout:
            // label | bar | value area | pct. This prevents the health bar from
            // stretching across the whole row.
            float labelW = 80f;
            float pctW = 50f;
            float barX = rect.x + labelW;
            float barW = rect.width - labelW - pctW - UITheme.ProgressbarValueW;
            UIComponents.Label(new Rect(rect.x, rect.y, labelW, rect.height),
                label,
                GameFont.Tiny, UITheme.SecondaryText,
                TextAnchor.MiddleLeft);
            float barH = UITheme.ProgressbarH;
            float share = Mathf.Clamp01(score01to100 / 100f);
            bool low = share < 0.3f;
            Color fill = low ? UITheme.Blood : UITheme.PillGold;
            Rect bar = new Rect(barX, rect.y + (rect.height - barH) / 2f, barW, barH);
            UIComponents.ProgressBar(bar, share, fill);
            UIComponents.Label(new Rect(rect.x + rect.width - pctW, rect.y, pctW, rect.height),
                Mathf.RoundToInt(score01to100) + "%",
                GameFont.Tiny, low ? UITheme.Blood : UITheme.Text,
                TextAnchor.MiddleRight);

            // Per-dimension hover tip listing all positive/negative factors.
            if (factors != null && factors.Count > 0)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine(label);
                for (int i = 0; i < factors.Count; i++)
                {
                    ReadModels.HealthFactorView f = factors[i];
                    if (f == null) continue;
                    string tag = f.IsPositive ? "✓" : "✗";
                    string impact = f.Impact == 0 ? "" : (" (" + (f.Impact > 0 ? "+" : "") + f.Impact + ")");
                    sb.AppendLine("  " + tag + " " + f.LabelText + impact);
                }
                TooltipHandler.TipRegion(rect, sb.ToString().TrimEnd());
            }
        }

        // ---- 榨取总账 / 产出核销 (contribution-archive layout, v4.6.1) ----

        /// <summary>
        /// v4.6.2: Cover header (matches contribution-archive-overview.html .cover block).
        /// Portrait + name + role description + in-service stamp + one-line verdict.
        /// All visual goes through UITheme tokens; portrait is drawn via the native
        /// PortraitsCache (3D colonist render). Falls back to a placeholder box if
        /// no live pawn is resolvable.
        /// </summary>

        private float DrawCoverHeader(Rect rect, float y, PawnObject pawn, IArchiveService service)
        {
            float portraitW = 60f;
            float portraitH = 75f;
            float portraitPad = 4f;
            float rowH = portraitH + 8f; // includes vertical breathing room

            // Tier medal inline row (.tier-inline in the design spec) grows the
            // header when a confirmed/estimated tier exists.
            WorkIntensityView intensity = cachedWorkIntensity;
            float tierRowH = (intensity != null && intensity.IsDefined) ? 20f : 0f;
            rowH += tierRowH;

            Rect portraitRect = new Rect(rect.x, y, portraitW, portraitH);
            Rect infoRect = new Rect(
                portraitRect.xMax + portraitPad + 6f,
                y,
                rect.width - portraitW - portraitPad - 6f,
                rowH);

            // ---- Portrait ----
            Pawn livePawn = service != null ? service.GetLivePawn(pawn.StableId) : null;
            DrawPortraitOrPlaceholder(portraitRect, livePawn);

            // ---- Tier medal inline (.tier-inline in spec) ----
            // Drawn at the top of the info column; the name/role/days block shifts down
            // by tierRowH so they never overlap the medal row.
            if (tierRowH > 0f)
            {
                DrawTierMedalInline(new Rect(infoRect.x, infoRect.y, infoRect.width, tierRowH), intensity);
            }

            // ---- Name + role description ----
            float contentY = infoRect.y + tierRowH;
            UIComponents.Label(new Rect(infoRect.x, contentY + 2f, infoRect.width, 26f),
                ObjectDisplayLabel(pawn),
                GameFont.Medium, UITheme.Text,
                TextAnchor.UpperLeft);

            string roleDesc = BuildCoverRoleDescription(pawn);
            UIComponents.Label(new Rect(infoRect.x, contentY + 30f, infoRect.width - 70f, 18f),
                roleDesc,
                GameFont.Tiny, UITheme.SecondaryText,
                TextAnchor.UpperLeft);

            // v4.14: identity dimension pill (role), matching the preview's
            // identity-pill — the cover shows who the pawn is in the colony.
            string identityText = "PersonalChronicle.UI.Cover.Identity".Translate(
                RoleLabel(pawn.Role)).ToString();
            float identityW = Mathf.Min(64f, Text.CalcSize(identityText).x + 10f);
            UIComponents.Pill(
                new Rect(infoRect.x + infoRect.width - identityW, contentY + 30f,
                    identityW, 16f),
                identityText, RolePillColor(pawn.Role));

            string daysText = BuildCoverDaysText(pawn);
            UIComponents.Label(new Rect(infoRect.x, contentY + 50f, infoRect.width, 18f),
                daysText,
                GameFont.Tiny, UITheme.Muted,
                TextAnchor.UpperLeft);

            // ---- In-service days text + stamp on the same row ----
            // v4.6.5: stamp is placed to the right of "在册 X 日" instead of the
            // bottom row, preventing overlap with the days line.
            // v5.x "在册"判定：存活 且 属于当前殖民地人口 → 在册；死亡归档或
            // 已离开殖民地 → 不在册。不再用 IsArchived（DeathTick>0）作唯一依据
            // —— 那会把"存档有快照但还活着"的殖民者误标为不在册。
            bool alive = service != null && service.IsCurrentlyEnlisted(pawn.StableId);
            string stampKey = alive
                ? "PersonalChronicle.UI.Cover.StampAlive"
                : "PersonalChronicle.UI.Cover.StampDead";
            string stampText = stampKey.Translate().ToString();
            float stampW = Mathf.Max(60f, Text.CalcSize(stampText).x + 18f);
            float stampH = 20f;

            float daysY = contentY + 50f;
            Vector2 daysSize = Text.CalcSize(daysText);
            Rect daysRect = new Rect(infoRect.x, daysY, daysSize.x + 4f, 18f);
            UIComponents.Label(daysRect, daysText,
                GameFont.Tiny, UITheme.Muted,
                TextAnchor.MiddleLeft);

            Rect stampRect = new Rect(
                daysRect.xMax + 6f,
                daysY + (18f - stampH) / 2f,
                stampW,
                stampH);
            UIComponents.Pill(stampRect, stampText, alive ? UITheme.Alive : UITheme.Dead);

            // ---- Incomplete badge (mid-install: JoinTick=-1) ----
            if (pawn.JoinTick < 0L)
            {
                string incomplete = "PersonalChronicle.UI.Cover.Incomplete".Translate().ToString();
                float badgeW = Mathf.Min(infoRect.width, Text.CalcSize(incomplete).x + 16f);
                Rect badgeRect = new Rect(infoRect.x, daysY - 22f, badgeW, 18f);
                DrawIncompleteBadge(badgeRect, incomplete);
            }

            return y + rowH;
        }


        private static void DrawPortraitOrPlaceholder(Rect rect, Pawn livePawn)
        {
            // Native portrait frame (blood-tinted border, dark fill, dim label when missing).
            Color prevColor = GUI.color;
            Color prevFontColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                Widgets.DrawBoxSolid(rect, UITheme.Panel);
                Widgets.DrawBox(rect);

                bool hasPortrait = false;
                if (livePawn != null)
                {
                    // Use the native PortraitsCache (RimWorld 1.6). Catch any null
                    // return / RenderTexture issue by falling through to placeholder.
                    RenderTexture portrait = PortraitsCache.Get(
                        livePawn, new Vector2(rect.width, rect.height), Rot4.South);
                    if (portrait != null)
                    {
                        GUI.DrawTexture(rect, portrait);
                        hasPortrait = true;
                    }
                }
                // Placeholder caption ("人物画像 / PortraitsCache") shows ONLY when no
                // live pawn / render is available — never over a real 3D portrait.
                if (!hasPortrait)
                {
                    string label = "PersonalChronicle.UI.Cover.PortraitLabel".Translate().ToString();
                    string sub = "PersonalChronicle.UI.Cover.PortraitLabelSub".Translate().ToString();
                    GUI.color = UITheme.Dim;
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(rect, label);
                    Rect subRect = new Rect(rect.x, rect.yMax - 14f, rect.width, 12f);
                    Widgets.Label(subRect, sub);
                }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }


        private static void DrawIncompleteBadge(Rect rect, string label)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                GUI.color = UITheme.BadgeIncompleteFill;
                Widgets.DrawBoxSolid(rect, UITheme.BadgeIncompleteFill);
                GUI.color = UITheme.Blood;
                Widgets.DrawBox(rect);
                GUI.color = UITheme.Blood;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, label);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        /// <summary>
        /// v4.6.4: tier medal inline row (.tier-inline in the design spec).
        /// Renders a square medal (tier display code on tier-coloured fill) beside
        /// the tier title, an "预估/实际" tag and the projected daily hours. Never
        /// called when the tier is undefined — the row collapses to zero height.
        /// </summary>

        private static void DrawTierMedalInline(Rect rect, WorkIntensityView intensity)
        {
            if (intensity == null || !intensity.IsDefined)
            {
                return;
            }
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                // ---- Medal square (tier colour fill + display code) ----
                float medal = Mathf.Min(rect.height, 18f);
                Rect medalRect = new Rect(rect.x, rect.y + (rect.height - medal) / 2f, medal, medal);
                Color tierColor = UITheme.Accent;
                if (!string.IsNullOrEmpty(intensity.ColorHex)
                    && ColorUtility.TryParseHtmlString(intensity.ColorHex, out Color parsed))
                {
                    tierColor = parsed;
                }
                GUI.color = tierColor;
                Widgets.DrawBoxSolid(medalRect, tierColor);
                GUI.color = UITheme.Window;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(medalRect, intensity.DisplayCode ?? string.Empty);

                // ---- Title + tag + projected daily ----
                float textX = medalRect.xMax + 6f;
                float textW = rect.xMax - textX;
                string title = string.IsNullOrEmpty(intensity.LabelKey)
                    ? string.Empty
                    : intensity.LabelKey.Translate().ToString();
                string tag = (intensity.IsEstimated
                    ? "PersonalChronicle.UI.Intensity.Estimated"
                    : "PersonalChronicle.UI.Intensity.Actual").Translate().ToString();
                string daily = intensity.DailyHours > 0d
                    ? string.Format("≈ {0} h/天", intensity.DailyHours.ToString("0.0"))
                    : string.Empty;
                string line = string.IsNullOrEmpty(daily)
                    ? string.Format("{0} · {1}", title, tag)
                    : string.Format("{0} · {1} · {2}", title, tag, daily);

                GUI.color = UITheme.Text;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(textX, rect.y, textW, rect.height), line);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }


        private string BuildCoverRoleDescription(PawnObject pawn)
        {
            string role = RoleLabel(pawn.Role);
            string faction = FactionLabel(pawn);
            string background = BuildCoverBackground(pawn);
            if (!string.IsNullOrEmpty(faction) && !string.IsNullOrEmpty(background))
            {
                return "PersonalChronicle.UI.Cover.RoleFormat".Translate(role, faction, background).ToString();
            }
            if (string.IsNullOrEmpty(faction) && !string.IsNullOrEmpty(background))
            {
                return "PersonalChronicle.UI.Cover.RoleFormatNoFaction".Translate(role, background).ToString();
            }
            if (!string.IsNullOrEmpty(faction) && string.IsNullOrEmpty(background))
            {
                return "PersonalChronicle.UI.Cover.RoleFormatNoBackground".Translate(role, faction).ToString();
            }
            return "PersonalChronicle.UI.Cover.RoleFormatMinimal".Translate(role).ToString();
        }


        private string BuildCoverBackground(PawnObject pawn)
        {
            string child = ResolveBackstoryLabel(pawn.ChildhoodBackstoryDefName);
            string adult = ResolveBackstoryLabel(pawn.AdulthoodBackstoryDefName);
            if (!string.IsNullOrEmpty(child) && !string.IsNullOrEmpty(adult))
            {
                return child + " / " + adult;
            }
            if (!string.IsNullOrEmpty(child)) return child;
            if (!string.IsNullOrEmpty(adult)) return adult;
            return "";
        }


        private static string ResolveBackstoryLabel(string backstoryDefName)
        {
            if (string.IsNullOrEmpty(backstoryDefName)) return "";
            var def = DefDatabase<BackstoryDef>.GetNamedSilentFail(backstoryDefName);
            return def != null ? def.title : backstoryDefName;
        }


        private string BuildCoverDaysText(PawnObject pawn)
        {
            if (pawn.JoinTick < 0L)
            {
                return "PersonalChronicle.UI.Cover.DaysUnknown".Translate().ToString();
            }
            long endTick = pawn.IsArchived && pawn.DeathTick > 0L
                ? pawn.DeathTick
                : Find.TickManager.TicksGame;
            // 在册时间固定用"日"（翻译键 "在册 {0} 日"），不做年/季/时等日以下
            // 单位换算——官方日数 API 取整，避免语义混乱（"在册 1 年 2 季"）。
            long days = (long)RimWorld.GenDate.TicksToDays((int)(endTick - pawn.JoinTick));
            if (days <= 0L) days = 0L;
            string key = pawn.IsArchived
                ? "PersonalChronicle.UI.Cover.DaysToDeath"
                : "PersonalChronicle.UI.Cover.DaysKnown";
            return key.Translate(days).ToString();
        }


        private float DrawLedger(Rect rect, float y, PawnObject pawn)
        {
            // Header row + 4 StatCells (already realised output, work hours, weekly
            // average, net yield) — matches the v4.6.1 contribution-archive preview.
            float headH = 26f;
            float cellH = UIComponents.StatCellMinHeight;
            float gap = UITheme.GridGap;
            float cellW = (rect.width - UITheme.CardPadX * 2f - gap * 3f) / 4f;

            UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, headH),
                "PersonalChronicle.UI.Ledger.Title");

            float rowY = y + headH + UITheme.SpaceXxs;
            float cellX = rect.x + UITheme.CardPadX;

            bool known = pawn.JoinTick >= 0L;
            string unknown = "PersonalChronicle.UI.Ledger.UnknownValue".Translate().ToString();

            // Already realised output (silver).
            string realisedValue = known && cachedProductionSummary != null && cachedProductionSummary.TotalMarketValue > 0f
                ? FormatSilver(cachedProductionSummary.TotalMarketValue)
                : (known ? "0" : unknown);
            string realisedUnit = "PersonalChronicle.UI.Ledger.OutputValueUnit".Translate().ToString();
            UIComponents.StatCell(new Rect(cellX, rowY, cellW, cellH),
                "PersonalChronicle.UI.Ledger.OutputValue".Translate().ToString(),
                realisedValue,
                realisedUnit,
                inlineSubLabel: true);

            // Total work hours — value carries the numeric, subLabel carries the unit
            // (避免 "10.7 h" + subLabel "h" 的单位重复).
            string workValue = known
                ? (GetCachedTotalWorkTicks() / (float)RimWorld.GenDate.TicksPerHour).ToString("0.0")
                : unknown;
            UIComponents.StatCell(new Rect(cellX + (cellW + gap), rowY, cellW, cellH),
                "PersonalChronicle.UI.Ledger.TotalWork".Translate().ToString(),
                workValue,
                "PersonalChronicle.UI.Ledger.TotalWorkUnit".Translate().ToString(),
                inlineSubLabel: true);

            // Weekly average yield (estimated silver per week from the health evaluator).
            string weeklyValue = known && cachedHealth != null && cachedHealth.IsDefined
                ? FormatSilver(cachedHealth.WeeklySilverEstimate) : unknown;
            UIComponents.StatCell(new Rect(cellX + 2f * (cellW + gap), rowY, cellW, cellH),
                "PersonalChronicle.UI.Ledger.Weekly".Translate().ToString(),
                weeklyValue,
                "PersonalChronicle.UI.Ledger.WeeklyUnit".Translate().ToString(),
                inlineSubLabel: true);

            // Net yield = realised output − work hours × hourly cost rate. Cost
            // rate is a conservative estimate of colony upkeep per work-hour
            // (the preview uses 2 sv/h); the value is honest "estimate" semantics.
            string netValue = known
                ? FormatSilver(NetLedgerSilver(pawn))
                : unknown;
            UIComponents.StatCell(new Rect(cellX + 3f * (cellW + gap), rowY, cellW, cellH),
                "PersonalChronicle.UI.Ledger.Net".Translate().ToString(),
                netValue,
                "PersonalChronicle.UI.Ledger.NetUnit".Translate().ToString(),
                inlineSubLabel: true);

            return rowY + cellH;
        }

        /// <summary>
        /// v4.14: net ledger silver = realised market value − work-hours × cost
        /// rate. Kept in one place so the "净收益" cell and any tooltip agree.
        /// </summary>

        private float NetLedgerSilver(PawnObject pawn)
        {
            const float hourlyCostRate = 2f; // sv per work-hour (preview contract)
            float realised = cachedProductionSummary != null
                ? cachedProductionSummary.TotalMarketValue : 0f;
            float hours = (float)GetCachedTotalWorkTicks() / RimWorld.GenDate.TicksPerHour;
            return realised - hours * hourlyCostRate;
        }


        private long GetCachedTotalWorkTicks()
        {
            // Use the cachedWorkIntensity view as a proxy; falling back to 0 is fine.
            if (cachedWorkIntensity == null || !cachedWorkIntensity.IsDefined) return 0L;
            double hours = cachedWorkIntensity.TotalHours;
            // 1h = GenDate.TicksPerHour（2500），禁止魔法数字。
            return (long)(hours * RimWorld.GenDate.TicksPerHour);
        }


        private float DrawOutputLedger(Rect rect, float y)
        {
            // Header + ProgressBar rows by production type. Empty state shows placeholder.
            float headH = 26f;
            UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, headH),
                "PersonalChronicle.UI.OutputLedger.Title");
            float bodyY = y + headH + 4f;

            IReadOnlyList<ProductionTypeView> types = cachedProductionSummary != null
                ? cachedProductionSummary.Types : null;
            float totalValue = cachedProductionSummary != null
                ? cachedProductionSummary.TotalMarketValue : 0f;

            if (types == null || types.Count == 0 || totalValue <= 0f)
            {
                UIComponents.Label(new Rect(rect.x + UITheme.CardPadX, bodyY, rect.width - UITheme.CardPadX * 2f, 22f),
                    "PersonalChronicle.UI.OutputLedger.Empty".Translate().ToString(),
                    GameFont.Tiny, UITheme.Dim);
                return bodyY + 24f;
            }

            // Sort descending by value for the most-extracted types first.
            List<ProductionTypeView> sorted = new List<ProductionTypeView>(types);
            sorted.Sort((a, b) => b.MarketValue.CompareTo(a.MarketValue));
            int take = Mathf.Min(5, sorted.Count);
            const float rowH = 22f;
            float rowY = bodyY;
            for (int i = 0; i < take; i++)
            {
                ProductionTypeView t = sorted[i];
                if (t == null) continue;
                float share = t.MarketValue / totalValue;
                string label = ResolveProductionTypeLabel(t.DefName);
                string valueText = FormatSilver(t.MarketValue)
                    + " " + "PersonalChronicle.UI.Ledger.OutputValueUnit".Translate().ToString();
                string pctText = Mathf.RoundToInt(share * 100f) + "%";
                DrawProductionRow(new Rect(rect.x + UITheme.CardPadX, rowY, rect.width - UITheme.CardPadX * 2f, rowH),
                    label, valueText, pctText, share);
                rowY += rowH;
            }
            return rowY;
        }


        private static void DrawProductionRow(Rect rect, string label, string valueText, string pctText, float share)
        {
            float labelW = 80f;
            float pctW = 50f;
            float barX = rect.x + labelW;
            float barW = rect.width - labelW - pctW - UITheme.ProgressbarValueW;
            UIComponents.Label(new Rect(rect.x, rect.y, labelW, rect.height),
                label,
                GameFont.Tiny, UITheme.Text,
                TextAnchor.MiddleLeft);
            float barH = UITheme.ProgressbarH;
            Rect bar = new Rect(barX, rect.y + (rect.height - barH) / 2f, barW, barH);
            UIComponents.ProgressBar(bar, Mathf.Clamp01(share), UITheme.Blood);
            UIComponents.Label(new Rect(bar.xMax + 4f, rect.y, UITheme.ProgressbarValueW, rect.height),
                valueText,
                GameFont.Tiny, UITheme.SecondaryText,
                TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(rect.x + rect.width - pctW, rect.y, pctW, rect.height),
                pctText,
                GameFont.Tiny, UITheme.Muted,
                TextAnchor.MiddleRight);
        }


        private static string ResolveProductionTypeLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return "—";
            // v4.6.5: production rows are aggregated by ThingCategory. Try the
            // category def first, then fall back to the item def for uncategorised
            // (or legacy) entries.
            var cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(defName);
            if (cat != null) return cat.label != null ? cat.label : defName;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null) return def.label != null ? def.label : defName;
            return defName;
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


        private static string FormatHours(double hours)
        {
            return "PersonalChronicle.UI.Hours".Translate(hours.ToString("0.0")).ToString();
        }


        private static string FormatDays(double days)
        {
            return days.ToString("0.0");
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

        /// <summary>
        /// v3.1 P3 Social: significant relation snapshots + social events + co-occurrence.
        /// </summary>

        private void DrawSocialTab(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            float y = rect.y;
            PawnObject pawn = cachedDetailObject as PawnObject;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.RelationNetwork".Translate().ToString());
            y = DrawSocialNetwork(rect, y, pawn, service);

            y += 8f;
            // v4.14: significant-relation table (类型/人物/状态), matching the
            // v4.6 preview's importantRel section. Consumes the Read-Model rows.
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.ImportantRelations".Translate().ToString());
            if (cachedRelations != null && cachedRelations.Count > 0)
            {
                float headH = 18f;
                float colX = rect.x + 6f;
                float colW1 = 120f;
                float colW2 = rect.width - colW1 - 90f - 12f;
                float colW3 = 90f;
                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.Muted;
                Widgets.Label(new Rect(colX, y, colW1, headH),
                    "PersonalChronicle.UI.RelType".Translate().ToString());
                Widgets.Label(new Rect(colX + colW1, y, colW2, headH),
                    "PersonalChronicle.UI.RelPerson".Translate().ToString());
                Widgets.Label(new Rect(colX + colW1 + colW2, y, colW3, headH),
                    "PersonalChronicle.UI.RelStatus".Translate().ToString());
                GUI.color = prevColor;
                y += headH + 2f;
                for (int i = 0; i < cachedRelations.Count; i++)
                {
                    ReadModels.RelationView rel = cachedRelations[i];
                    if (rel == null)
                    {
                        continue;
                    }
                    Rect row = new Rect(rect.x, y, rect.width, 20f);
                    Text.Font = GameFont.Tiny;
                    GUI.color = UITheme.Muted;
                    Widgets.Label(new Rect(colX, row.y + 2f, colW1, 18f), rel.RelationLabel ?? "—");
                    GUI.color = UITheme.Text;
                    Widgets.Label(new Rect(colX + colW1, row.y + 2f, colW2, 18f), rel.OtherLabel ?? "—");
                    GUI.color = rel.IsLive ? UITheme.Alive : UITheme.Dead;
                    Widgets.Label(new Rect(colX + colW1 + colW2, row.y + 2f, colW3, 18f), rel.StatusLabel ?? "—");
                    GUI.color = prevColor;
                    Widgets.DrawLineHorizontal(rect.x, row.yMax, rect.width);
                    y += 20f;
                }
                y += 8f;
            }
            else
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoRelationData".Translate().ToString());
                y += 28f;
            }

            y += 8f;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.RelationEvents".Translate().ToString());
            int socialEvents = 0;
            for (int i = 0; i < cachedDetailRawEvents.Count; i++)
            {
                ChronicleEvent ev = cachedDetailRawEvents[i];
                if (ev == null || !IsSocialEvent(ev))
                {
                    continue;
                }
                socialEvents++;
                string action = string.Empty;
                string rel = string.Empty;
                if (ev.Params != null)
                {
                    ev.Params.TryGetValue(ChronicleEventParams.RelationAction, out action);
                    ev.Params.TryGetValue(ChronicleEventParams.Relation, out rel);
                }
                string title = FormatSocialEventTitle(action, rel, ev, service);
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                DrawEventRow(row, FormatDate(ev.Tick), title, FormatParams(ev.Params));
                if (Widgets.ButtonInvisible(row))
                {
                    OpenEventDetail(service, ev);
                }
                y += TimelineRowHeight;
            }
            if (socialEvents == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoRelationEvents".Translate().ToString());
                y += 28f;
            }

            y += 8f;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Intertwined".Translate().ToString());
            int count = 0;
            for (int i = 0; i < cachedLinkedObjects.Count; i++)
            {
                LinkedObjectView link = cachedLinkedObjects[i];
                if (link.CategoryKey != ArchiveCategoryKeys.Pawn)
                {
                    continue;
                }
                count++;
                Rect row = new Rect(rect.x, y, rect.width, RowHeight - 4f);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(row.x + 6f, row.y + 4f, row.width - 200f, 22f), link.Label);
                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.SecondaryText;
                // v4.14: show the actual co-occurrence count, not just the label.
                Widgets.Label(new Rect(row.x + row.width - 196f, row.y + 6f, 190f, 18f),
                    "PersonalChronicle.UI.SharedEventsN".Translate(link.SharedCount).ToString());
                GUI.color = prevColor;
                if (link.Target != NavTarget.None && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, link.Target, link.StableId, null);
                }
                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += RowHeight + 2f;
            }
            if (count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoSocialData".Translate().ToString());
            }
        }


        private float DrawSocialNetwork(Rect rect, float y, PawnObject pawn, IArchiveService service)
        {
            const float basePanelHeight = 246f;
            // Max visible relation nodes on the graph. The grid slot pool supports
            // 26 positions; 24 leaves headroom and keeps the auto-fit readable.
            const float maxNodeCount = 24f;
            Rect panel = new Rect(rect.x, y, rect.width, basePanelHeight);
            ArchiveUiStyle.DrawPanel(panel, ArchiveUiStyle.PanelRaised);

            List<SocialNodeView> nodes = new List<SocialNodeView>();
            HashSet<string> seen = new HashSet<string>();
            if (pawn != null && pawn.Relations != null)
            {
                // Rank first, then cap, so closest ties always win a slot.
                List<SignificantRelation> ranked = new List<SignificantRelation>();
                for (int i = 0; i < pawn.Relations.Count; i++)
                {
                    SignificantRelation candidate = pawn.Relations[i];
                    if (candidate != null && !string.IsNullOrEmpty(candidate.OtherStableId))
                    {
                        ranked.Add(candidate);
                    }
                }
                ranked.Sort((a, b) => SocialNodeRank(a).CompareTo(SocialNodeRank(b)));

                for (int i = 0; i < ranked.Count && nodes.Count < maxNodeCount; i++)
                {
                    SignificantRelation relation = ranked[i];
                    if (!seen.Add(relation.OtherStableId))
                    {
                        continue;
                    }
                    ArchiveObject other = service.GetObject(relation.OtherStableId);
                    nodes.Add(new SocialNodeView
                    {
                        StableId = relation.OtherStableId,
                        Label = other != null ? ObjectDisplayLabel(other) : relation.OtherLabel,
                        RelationLabel = RelationDefLabel(relation.RelationDefName),
                        RelationDefName = relation.RelationDefName,
                        Active = relation.IsActive
                    });
                }
            }
            if (nodes.Count == 0)
            {
                for (int i = 0; i < cachedLinkedObjects.Count && nodes.Count < maxNodeCount; i++)
                {
                    LinkedObjectView link = cachedLinkedObjects[i];
                    if (link.CategoryKey != ArchiveCategoryKeys.Pawn || !seen.Add(link.StableId))
                    {
                        continue;
                    }
                    nodes.Add(new SocialNodeView
                    {
                        StableId = link.StableId,
                        Label = link.Label,
                        RelationLabel = "PersonalChronicle.UI.SharedEvents".Translate().ToString(),
                        RelationDefName = string.Empty,
                        Active = true
                    });
                }
            }

            // Scroll-wheel zoom: scale the whole graph around the panel centre so
            // dense relations separate instead of overlapping. Consume the scroll
            // event so it does not also scroll the surrounding detail view.
            if (nodes.Count > 0 && panel.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.ScrollWheel)
                {
                    float delta = Event.current.delta.y;
                    if (delta != 0f)
                    {
                        socialNetworkZoomTouched = true;
                        socialNetworkZoom = Mathf.Clamp(
                            socialNetworkZoom * (delta < 0f ? 1.1f : 0.9f), 0.6f, 2.4f);
                        Event.current.Use();
                    }
                }
            }

            // Importance-driven grid slots: spouses occupy the symmetric left/right
            // anchor (the "extension centre"), parents sit above, children below,
            // siblings on the sides and friends/rivals/others on the outer ring.
            // All relation cards share the SAME size — only positions change by
            // tier — so the layout reads as a calm family tree rather than a
            // weighted importance chart.
            (int col, int row)[] slots = GridSlotsFor(nodes);
            int maxAbsCol = 0;
            int maxAbsRow = 0;
            for (int s = 0; s < slots.Length; s++)
            {
                if (Mathf.Abs(slots[s].col) > maxAbsCol) maxAbsCol = Mathf.Abs(slots[s].col);
                if (Mathf.Abs(slots[s].row) > maxAbsRow) maxAbsRow = Mathf.Abs(slots[s].row);
            }

            // Grid spacing scales with the card size AND zoom so gaps grow together
            // with the cards: cards never overlap at any zoom level. The gap ratio
            // (30% of card size) plus a 22f minimum keeps the relation-row text
            // from spilling into the next card vertically.
            const float gapRatio = 0.30f;
            const float minNodeGap = 22f;
            float baseNodeW = Mathf.Min(160f, Mathf.Max(110f, panel.width * 0.22f));
            float baseCenterW = 176f;
            float baseCenterH = 50f;
            float baseNodeH = 60f;

            // First entry (not yet scrolled): auto-fit the whole graph inside the
            // panel so every relation is visible without dragging. Only the
            // horizontal extent is constrained by the panel width — the panel
            // height already grows to fit the vertical grid (panelHeight below),
            // so fitY must NOT cap against the fixed 246f base height (which would
            // shrink dense graphs into unreadable clutter).
            // The 0.2f floor lets even very narrow panels shrink a dense graph far
            // enough that no node overflows horizontally; a higher floor would
            // leave outer columns clipped on small windows. At 0.2f cards are
            // small but remain readable and never clip outside the panel.
            float zoom = socialNetworkZoom;
            if (!socialNetworkZoomTouched && nodes.Count > 0)
            {
                float fitW = (maxAbsCol * 2 + 1) * (baseNodeW * (1f + gapRatio) + minNodeGap)
                    + baseNodeW * 2f;
                float fitX = (panel.width - 24f) / Mathf.Max(1f, fitW);
                zoom = Mathf.Clamp(fitX, 0.2f, 1f);
                socialNetworkZoom = zoom;
            }

            float nodeWidth = baseNodeW * zoom;
            float centerCardW = baseCenterW * zoom;
            float centerCardH = baseCenterH * zoom;
            float nodeCardH = baseNodeH * zoom;

            // Gap scales with zoom so enlarged cards keep a proportional gap:
            // spacing = cardSize + gap, gap = cardSize*gapRatio + minNodeGap*zoom.
            float colSpacing = nodeWidth + Mathf.Max(nodeWidth * gapRatio, minNodeGap * zoom);
            float rowSpacing = nodeCardH + Mathf.Max(nodeCardH * gapRatio, minNodeGap * zoom);

            // Grow the panel vertically with zoom AND with the grid extent so the
            // outermost nodes stay inside its frame.
            float panelHeight = Mathf.Max(
                basePanelHeight,
                basePanelHeight * zoom,
                (maxAbsRow * 2 + 1) * rowSpacing + nodeCardH + 32f);
            panel = new Rect(panel.x, panel.y, panel.width, panelHeight);
            ArchiveUiStyle.DrawPanel(panel, ArchiveUiStyle.PanelRaised);

            Vector2 center = new Vector2(panel.center.x, panel.center.y) + socialNetworkPan;
            Rect centerRect = new Rect(
                center.x - centerCardW / 2f,
                center.y - centerCardH / 2f,
                centerCardW,
                centerCardH);

            // Pre-compute node positions/rects so we can tell whether the mouse
            // is over an interactive card before deciding whether to pan.
            List<Vector2> nodeCenters = new List<Vector2>(nodes.Count);
            List<Rect> nodeRects = new List<Rect>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
            {
                (int col, int row) slot = slots[i];
                Vector2 nodeCenter = new Vector2(
                    center.x + slot.col * colSpacing,
                    center.y + slot.row * rowSpacing);
                Rect nodeRect = new Rect(
                    nodeCenter.x - nodeWidth / 2f,
                    nodeCenter.y - nodeCardH / 2f,
                    nodeWidth,
                    nodeCardH);
                nodeCenters.Add(nodeCenter);
                nodeRects.Add(nodeRect);
            }

            // Left-drag pan: move the graph when dragging on empty panel background.
            // Only pan if the cursor is not over the centre card or a node card; node
            // clicks are handled by Widgets.ButtonInvisible below.
            bool mouseOverInteractive = panel.Contains(Event.current.mousePosition)
                && (centerRect.Contains(Event.current.mousePosition)
                    || nodeRects.Any(r => r.Contains(Event.current.mousePosition)));
            if (!mouseOverInteractive && panel.Contains(Event.current.mousePosition)
                && Event.current.type == EventType.MouseDrag
                && Event.current.button == 0)
            {
                socialNetworkPan += Event.current.delta;
                Event.current.Use();
            }

            // Draw orthogonal (horizontal + vertical) links from centre-card edge to
            // each relation-card edge. All relation cards share the same width/height
            // so the elbow points stay aligned on the card midline; link thickness is
            // bumped for active ties to read as smoother, calmer strokes.
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector2 nodeCenter = nodeCenters[i];
                Color linkColor = nodes[i].Active ? ArchiveUiStyle.Accent : ArchiveUiStyle.Border;
                float linkThickness = nodes[i].Active ? 2.5f : 1f;
                DrawOrthogonalLink(center, nodeCenter, linkColor, linkThickness);
            }

            if (nodes.Count == 0)
            {
                // 空态只画提示文字，不画中心卡（分支在中心卡绘制之前提前返回）。
                UIComponents.Label(panel, "PersonalChronicle.UI.NoSignificantRelations".Translate().ToString(),
                    UITheme.FontBody, UITheme.Muted, TextAnchor.MiddleCenter);
                return panel.yMax + UITheme.SpaceXs;
            }

            ArchiveUiStyle.DrawCard(centerRect, ArchiveUiStyle.Accent);
            UIComponents.Label(centerRect, pawn != null ? ObjectDisplayLabel(pawn) : "—",
                UITheme.FontBody, UITheme.Text, TextAnchor.MiddleCenter);

            for (int i = 0; i < nodes.Count; i++)
            {
                Rect nodeRect = nodeRects[i];
                ArchiveUiStyle.DrawCard(nodeRect, nodes[i].Active ? ArchiveUiStyle.Info : ArchiveUiStyle.Muted);
                // 中文行高经验：节点标签（Small）≥22f，关系标签（Tiny）≥18f；
                // 经 UIComponents.Label 渲染，font/color/anchor 内部配对恢复。
                UIComponents.Label(new Rect(nodeRect.x + UITheme.CardPadX, nodeRect.y + UITheme.SpaceXxs,
                    nodeRect.width - UITheme.CardPadX * 2f, UITheme.FontBodyLineHeight * zoom),
                    nodes[i].Label, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleCenter);
                UIComponents.Label(new Rect(nodeRect.x + UITheme.CardPadX, nodeRect.y + UITheme.SpaceLg * zoom,
                    nodeRect.width - UITheme.CardPadX * 2f, 18f * zoom),
                    nodes[i].RelationLabel, UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleCenter);
                if (Widgets.ButtonInvisible(nodeRect))
                {
                    NavigateTarget(service, NavTarget.Pawn, nodes[i].StableId, null);
                }
            }

            return panel.yMax + UITheme.SpaceXs;
        }

        /// <summary>
        /// Returns the (col, row) grid slots for a given relation count. Slots are
        /// returned in the order they should be filled (importance-ranked by caller),
        /// importance-ranked by caller), so position 0 is always the most prominent
        /// tie. Slots are exact integer offsets — DrawSocialNetwork multiplies them
        /// by colSpacing / rowSpacing so every node lands on a grid intersection.
        /// All relation cards share the SAME size — only positions change by tier:
        /// spouses occupy the symmetric left/right anchor (the "extension centre"),
        /// parents sit above, children below, siblings on the sides and
        /// friends/rivals/others on the outer ring.
        /// </summary>
        private static (int col, int row)[] GridSlotsFor(List<SocialNodeView> nodes)
        {
            if (nodes.Count == 0)
            {
                return System.Array.Empty<(int, int)>();
            }

            // Slot pools, one per relation tier. When a pool is exhausted the
            // remaining nodes fall back to the outer ring; same size everywhere.
            var spouseSlots = new (int, int)[]
            {
                (-1, 0), ( 1, 0) // 夫妻对称左右
            };
            var parentSlots = new (int, int)[]
            {
                ( 0, -1), (-1, -1), ( 1, -1), // 父母辈往上
                (-2, -1), ( 2, -1)
            };
            var childSlots = new (int, int)[]
            {
                ( 0, 1), (-1, 1), ( 1, 1), // 子女辈往下
                (-2, 1), ( 2, 1)
            };
            var siblingSlots = new (int, int)[]
            {
                (-2, 0), ( 2, 0) // 平辈左右
            };
            var outerSlots = new (int, int)[]
            {
                (-1, -2), ( 1, -2), (-1, 2), ( 1, 2),
                (-2, -2), ( 2, -2), (-2, 2), ( 2, 2),
                ( 0, -2), ( 0, 2), (-3, 0), ( 3, 0)
            };

            var result = new (int col, int row)[nodes.Count];
            var used = new HashSet<(int, int)>();
            int[] cursors = new int[5]; // spouse / parent / child / sibling / outer
            for (int i = 0; i < nodes.Count; i++)
            {
                SocialRelationTier tier = SocialRelationTierOf(nodes[i].RelationDefName);
                (int col, int row) slot;
                switch (tier)
                {
                    case SocialRelationTier.Spouse:
                        slot = TakeNext(spouseSlots, ref cursors[0], used);
                        break;
                    case SocialRelationTier.Parent:
                        slot = TakeNext(parentSlots, ref cursors[1], used);
                        break;
                    case SocialRelationTier.Child:
                        slot = TakeNext(childSlots, ref cursors[2], used);
                        break;
                    case SocialRelationTier.Sibling:
                        slot = TakeNext(siblingSlots, ref cursors[3], used);
                        break;
                    default:
                        slot = TakeNext(outerSlots, ref cursors[4], used);
                        break;
                }
                result[i] = slot;
            }
            return result;
        }

        private static (int col, int row) TakeNext(
            (int col, int row)[] pool, ref int cursor, HashSet<(int, int)> used)
        {
            // 优先用池内未被占用的槽位；池耗尽则在外圈兜底，保证不重叠。
            for (int k = cursor; k < pool.Length; k++)
            {
                if (used.Add((pool[k].col, pool[k].row)))
                {
                    cursor = k + 1;
                    return pool[k];
                }
            }
            cursor = pool.Length;
            for (int ring = 1; ring < 8; ring++)
            {
                for (int dc = -ring; dc <= ring; dc++)
                {
                    for (int dr = -ring; dr <= ring; dr++)
                    {
                        if (Mathf.Abs(dc) != ring && Mathf.Abs(dr) != ring)
                        {
                            continue;
                        }
                        if (used.Add((dc, dr)))
                        {
                            return (dc, dr);
                        }
                    }
                }
            }
            return (0, 0);
        }

        private enum SocialRelationTier
        {
            Other,
            Spouse,
            Parent,
            Child,
            Sibling
        }

        private static SocialRelationTier SocialRelationTierOf(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return SocialRelationTier.Other;
            }
            if (defName.Contains("Spouse") || defName.Contains("Lover") || defName.Contains("Fiance"))
            {
                return SocialRelationTier.Spouse;
            }
            if (defName.Contains("Parent") || defName.Contains("Mother") || defName.Contains("Father")
                || defName.Contains("Stepparent") || defName.Contains("InLaw")
                || defName.Contains("Grandparent") || defName.Contains("Uncle") || defName.Contains("Aunt"))
            {
                return SocialRelationTier.Parent;
            }
            if (defName.Contains("Child") || defName.Contains("Son") || defName.Contains("Daughter")
                || defName.Contains("Grandchild") || defName.Contains("Nephew") || defName.Contains("Niece"))
            {
                return SocialRelationTier.Child;
            }
            if (defName.Contains("Sibling") || defName.Contains("Brother") || defName.Contains("Sister")
                || defName.Contains("Cousin") || defName.Contains("Half"))
            {
                return SocialRelationTier.Sibling;
            }
            return SocialRelationTier.Other;
        }

        /// <summary>
        /// Draws an orthogonal Z-shaped link between two card centres. The line
        /// starts at card A's midpoint and ends at card B's midpoint — NOT from
        /// the card edges. Both vertical axes run along card A's X midpoint
        /// (center.x) and card B's X midpoint (nodeCenter.x); the two vertical
        /// segments are joined by one horizontal segment at the mid-height.
        /// A small 5f chamfer at each 90° turn keeps the corner smooth.
        /// </summary>
        // Orthogonal link geometry now lives in ArchivePanelBase (BUG-BASE-01 refactor).

    }
}

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
    /// <summary>Partial of ArchiveMainTabWindow 鈥?Home view drawing (BUG-BASE-01 refactor).</summary>
    public sealed partial class ArchiveMainTabWindow
    {
        private void DrawHomeContent(Rect inner, IArchiveService service)
        {
            Color prevColor = GUI.color;
            float contentHeight = ComputeHomeHeight(inner.width);
            float viewHeight = Mathf.Max(inner.height, contentHeight);
            Rect viewRect = new Rect(inner.x, inner.y, inner.width - 16f, viewHeight);

            Widgets.BeginScrollView(inner, ref homeScroll, viewRect);
            try
            {
            float y = viewRect.y + 4f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 28f),
                "PersonalChronicle.UI.ColonyArchive".Translate().ToString());
            y += 30f;

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.SecondaryText;
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 18f),
                "PersonalChronicle.UI.ArchiveHomeDesc".Translate().ToString());
            GUI.color = prevColor;
            Text.Font = GameFont.Small;
            y += 26f;

            // v4.0: view selector (B dashboard vs E chronicle timeline).
            y = DrawHomeViewTabs(viewRect, y, service);

            if (homeViewMode == HomeViewMode.Timeline)
            {
                DrawHomeTimeline(viewRect, y, service);
                return;
            }

            y = DrawHomeKpi(viewRect, y);
            y += 20f;

            float leftWidth = viewRect.width * 0.62f;
            float rightWidth = viewRect.width - leftWidth - 16f;
            Rect leftRect = new Rect(viewRect.x, y, leftWidth, viewHeight - (y - viewRect.y));
            Rect rightRect = new Rect(viewRect.x + leftWidth + 16f, y, rightWidth, viewHeight - (y - viewRect.y));

            DrawRecentHistory(leftRect, service);
            DrawImportantArchives(rightRect, service);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }


        private float DrawHomeViewTabs(Rect viewRect, float y, IArchiveService service)
        {
            float tabWidth = 150f;
            float gap = 8f;
            float x = viewRect.x;
            string[] labels = new[]
            {
                "PersonalChronicle.UI.HomeKpiView".Translate().ToString(),
                "PersonalChronicle.UI.HomeTimelineView".Translate().ToString()
            };
            HomeViewMode[] modes = new[] { HomeViewMode.Kpi, HomeViewMode.Timeline };
            float startY = y;
            for (int i = 0; i < labels.Length; i++)
            {
                Rect tabRect = new Rect(x, y, tabWidth, HomeViewTabHeight);
                bool selected = homeViewMode == modes[i];
                ArchiveUiStyle.DrawSelectedNavigation(tabRect, selected);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(tabRect, labels[i]);
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonInvisible(tabRect) && !selected)
                {
                    homeViewMode = modes[i];
                    PersistHomeViewMode(service);
                    // v4.5.3: switching the home presentation immediately pulls the
                    // view-scoped cache (e.g. the full event stream for Timeline) so
                    // the new view is never blank until the next throttled refresh.
                    nextRefreshTick = 0L;
                    RefreshNow(service);
                }
                x += tabWidth + gap;
            }
            return startY + HomeViewTabHeight + 12f;
        }


        private void PersistHomeViewMode(IArchiveService service)
        {
            service.SetHomeViewMode((int)homeViewMode);
        }


        private void DrawHomeTimeline(Rect viewRect, float y, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
            if (cachedTimelineEvents == null || cachedTimelineEvents.Count == 0)
            {
                Text.Font = GameFont.Small;
                GUI.color = ArchiveUiStyle.SecondaryText;
                Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 24f),
                    "PersonalChronicle.UI.NoTimelineEvents".Translate().ToString());
                return;
            }
            // cachedTimelineEvents is already sorted ascending by Tick at
            // cache-rebuild time; reuse directly (no per-frame allocation/sort).
            IReadOnlyList<ChronicleEvent> ordered = cachedTimelineEvents;
            float spineX = viewRect.x + 14f;
            float nodeX = spineX + 18f;
            float rowH = 54f;
            float currentY = y;
            bool left = true;
            for (int i = 0; i < ordered.Count; i++)
            {
                ChronicleEvent ev = ordered[i];
                if (ev == null)
                {
                    continue;
                }
                if (ChronicleEventImportance.Resolve(ev) < ChronicleImportance.Important)
                {
                    // Timeline shows only key milestones (keeps large saves calm).
                    continue;
                }
                string typeKey = ev.TypeKey;
                string icon = EventTypeToGlyph(typeKey);
                Color color = EventTypeToColor(typeKey);
                string title = EventName(ev);
                string date = GenDate.DateReadoutStringAt(ev.Tick, Vector2.zero);

                // compute card geometry first (connector needs it).
                const float cardGap = 16f;
                const float minCardW = 40f;
                float availableW = Mathf.Max(0f, viewRect.width - nodeX - viewRect.x - 24f);
                float cardW = Mathf.Max(minCardW, availableW / 2f - cardGap / 2f);
                float cardX = left ? nodeX : nodeX + cardW + cardGap;
                Rect cardRect = new Rect(cardX, currentY + 4f, cardW, rowH - 8f);

                // spine segment
                GUI.color = ArchiveUiStyle.TimelineSpine;
                Widgets.DrawLineVertical(spineX, currentY, rowH);
                // node + horizontal connector
                GUI.color = color;
                Rect nodeRect = new Rect(spineX - 5f, currentY + rowH / 2f - 5f, 10f, 10f);
                GUI.DrawTexture(nodeRect, BaseContent.WhiteTex);
                float connectorStart = left ? nodeRect.xMax : cardRect.xMax;
                float connectorEnd = left ? cardRect.x : nodeRect.x;
                float connectorLen = Mathf.Max(0f, connectorEnd - connectorStart);
                Widgets.DrawLineHorizontal(connectorStart, currentY + rowH / 2f, connectorLen);
                GUI.color = prevColor;

                ArchiveUiStyle.DrawPanel(cardRect);
                // v1.1.5: event-kind accent stripe on the card's left edge
                // (mirrors the HTML preview's ::before left border).
                Color prevAccent = GUI.color;
                GUI.color = color;
                Widgets.DrawLineVertical(cardRect.x, cardRect.y + 2f, cardRect.height - 4f);
                GUI.color = prevAccent;
                Rect cardInner = cardRect.ContractedBy(6f);
                Text.Font = GameFont.Small;
                GUI.color = ArchiveUiStyle.Text;
                Widgets.Label(new Rect(cardInner.x, cardInner.y, cardInner.width, 20f), icon + " " + title);
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.SecondaryText;
                Widgets.Label(new Rect(cardInner.x, cardInner.y + 22f, cardInner.width, 16f), date);
                GUI.color = prevColor;
                if (Widgets.ButtonInvisible(cardRect))
                {
                    OpenEventDetail(service, ev);
                }
                left = !left;
                currentY += rowH;
            }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }


        private static float ComputeHomeHeight(float width)
        {
            // tabs + KPI groups (2 section titles + 3 large cards + 5 small cards) + two columns.
            float tabsHeight = HomeViewTabHeight + 12f;
            float kpiHeight = 86f + 58f + 2 * 22f + 2 * 16f;
            float columnsHeight = 6 * TimelineRowHeight + 40f + 4 * 70f + 60f;
            return 4f + 30f + 26f + tabsHeight + kpiHeight + 20f + columnsHeight + 20f;
        }


        private float DrawHomeKpi(Rect viewRect, float y)
        {
            const float groupGap = 16f;
            const float cardGap = 10f;

            // ---------- Real-time indicators (3 large cards, green accent) ----------
            DrawSectionTitle(viewRect, ref y, "PersonalChronicle.UI.HomeRealtimeGroup".Translate().ToString());
            const float liveHeight = 86f;
            int liveCount = 3;
            float liveCardW = Mathf.Max(40f, (viewRect.width - (liveCount - 1) * cardGap) / liveCount);
            DrawHomeMetricCard(
                new Rect(viewRect.x, y, liveCardW, liveHeight),
                "PersonalChronicle.UI.HomeLiveColonists".Translate().ToString(),
                cachedLiveColonistCount.ToString(),
                "PersonalChronicle.UI.HomeColonistBreakdown"
                    .Translate(cachedLiveFreeCount, cachedLiveSlaveCount, cachedLivePrisonerCount)
                    .ToString(),
                ArchiveUiStyle.Alive,
                isLarge: true);
            DrawHomeMetricCard(
                new Rect(viewRect.x + liveCardW + cardGap, y, liveCardW, liveHeight),
                "PersonalChronicle.UI.HomeServiceDays".Translate().ToString(),
                cachedServiceDays.ToString(),
                "PersonalChronicle.UI.HomeServiceSince".Translate().ToString(),
                ArchiveUiStyle.Accent,
                isLarge: true);
            DrawHomeMetricCard(
                new Rect(viewRect.x + 2 * (liveCardW + cardGap), y, liveCardW, liveHeight),
                "PersonalChronicle.UI.HomeLivePawns".Translate().ToString(),
                cachedActivePawnCount.ToString(),
                "PersonalChronicle.UI.HomeSnapshotBreakdown"
                    .Translate(cachedActivePawnCount, cachedArchivedPawnCount)
                    .ToString(),
                ArchiveUiStyle.Text,
                isLarge: true);
            y += liveHeight;

            y += groupGap;

            // ---------- Archive library (5 small cards) ----------
            DrawSectionTitle(viewRect, ref y, "PersonalChronicle.UI.HomeArchiveGroup".Translate().ToString());
            const float archiveHeight = 58f;
            int archiveCount = 5;
            float archiveCardW = Mathf.Max(40f, (viewRect.width - (archiveCount - 1) * cardGap) / archiveCount);
            DrawHomeMetricCard(
                new Rect(viewRect.x, y, archiveCardW, archiveHeight),
                "PersonalChronicle.UI.Category.Pawn".Translate().ToString(),
                cachedCategoryObjects.GetCount(ArchiveCategoryKeys.Pawn).ToString(),
                null,
                ArchiveUiStyle.Text,
                isLarge: false);
            DrawHomeMetricCard(
                new Rect(viewRect.x + archiveCardW + cardGap, y, archiveCardW, archiveHeight),
                "PersonalChronicle.UI.Category.Thing".Translate().ToString(),
                cachedCategoryObjects.GetCount(ArchiveCategoryKeys.Thing).ToString(),
                null,
                ArchiveUiStyle.Text,
                isLarge: false);
            DrawHomeMetricCard(
                new Rect(viewRect.x + 2 * (archiveCardW + cardGap), y, archiveCardW, archiveHeight),
                "PersonalChronicle.UI.Category.Battle".Translate().ToString(),
                cachedCategoryObjects.GetCount(ArchiveCategoryKeys.Battle).ToString(),
                null,
                ArchiveUiStyle.Text,
                isLarge: false);
            DrawHomeMetricCard(
                new Rect(viewRect.x + 3 * (archiveCardW + cardGap), y, archiveCardW, archiveHeight),
                "PersonalChronicle.UI.Category.Location".Translate().ToString(),
                cachedCategoryObjects.GetCount(ArchiveCategoryKeys.Location).ToString(),
                null,
                ArchiveUiStyle.Text,
                isLarge: false);
            DrawHomeMetricCard(
                new Rect(viewRect.x + 4 * (archiveCardW + cardGap), y, archiveCardW, archiveHeight),
                "PersonalChronicle.UI.ArchivedRecords".Translate().ToString(),
                cachedArchivedPawnCount.ToString(),
                null,
                ArchiveUiStyle.Muted,
                isLarge: false);
            y += archiveHeight;

            return y;
        }


        private static void DrawHomeMetricCard(Rect rect, string label, string value, string subLabel, Color accent, bool isLarge)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                ArchiveUiStyle.DrawPanel(rect);
                float pad = isLarge ? 12f : 8f;
                Rect inner = rect.ContractedBy(pad);

                // Label — Tiny, CJK line-height >= 18f.
                Text.Font = GameFont.Tiny;
                const float labelH = 18f;
                GUI.color = UITheme.Muted;
                Widgets.Label(new Rect(inner.x, inner.y, inner.width, labelH), label);

                // Value — Medium, CJK line-height >= 28f (small cards get 28f, large 32f).
                Text.Font = GameFont.Medium;
                float valueH = isLarge ? 32f : 28f;
                float valueY = isLarge ? inner.y + 24f : inner.y + 22f;
                GUI.color = accent;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(inner.x, valueY, inner.width, valueH), value);
                Text.Anchor = TextAnchor.UpperLeft;

                if (!string.IsNullOrEmpty(subLabel))
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = UITheme.Muted;
                    float subY = isLarge ? inner.y + 56f : inner.y + 50f;
                    Widgets.Label(new Rect(inner.x, subY, inner.width, 18f), subLabel);
                }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }


        private void DrawRecentHistory(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.RecentHistory".Translate().ToString());

            if (cachedRecentLines.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoEvents".Translate().ToString());
                return;
            }

            for (int i = 0; i < cachedRecentLines.Count; i++)
            {
                RecentLineView line = cachedRecentLines[i];
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                DrawEventRow(row, line.DateText, line.TitleText, line.TypeText);
                if (line.Event != null && Widgets.ButtonInvisible(row))
                {
                    OpenEventDetail(service, line.Event);
                }
                y += TimelineRowHeight;
            }
        }


        private void DrawImportantArchives(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.ImportantArchives".Translate().ToString());

            if (cachedImportantCards.Count == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f),
                    "PersonalChronicle.UI.NoEvents".Translate().ToString());
                return;
            }

            // 64f = CJK-safe three-line card (Tiny 18 + Small 24 + Tiny 18 = 60f + 4f pad).
            const float cardHeight = 64f;
            const float cardGap = UITheme.GridGap;
            for (int i = 0; i < cachedImportantCards.Count; i++)
            {
                ImportantCardView card = cachedImportantCards[i];
                Rect cardRect = new Rect(rect.x, y, rect.width, cardHeight);
                ArchiveUiStyle.DrawCard(cardRect, ArchiveUiStyle.Accent);

                Color prevColor = GUI.color;
                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Accent;
                Widgets.Label(new Rect(cardRect.x + UITheme.CardPadX, cardRect.y + 4f, cardRect.width - UITheme.CardPadX * 2f, 18f), card.TagLabel);

                Text.Font = GameFont.Small;
                GUI.color = ArchiveUiStyle.Text;
                Widgets.Label(new Rect(cardRect.x + UITheme.CardPadX, cardRect.y + 22f, cardRect.width - UITheme.CardPadX * 2f, 24f), card.Label);

                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(cardRect.x + UITheme.CardPadX, cardRect.y + 46f, cardRect.width - UITheme.CardPadX * 2f, 18f), card.SubLabel);
                GUI.color = prevColor;
                Text.Font = prevFont;

                if (card.Target != NavTarget.None && Widgets.ButtonInvisible(cardRect))
                {
                    NavigateTarget(service, card.Target, card.StableId, null);
                }
                y += cardHeight + cardGap;
            }
        }

        // ---- Overview ----------------------------------------------------------

        /// <summary>
        /// v4.11 P0: Overview › Battle cards. Each card shows the three captured
        /// elements — trigger date, raid force size and repulse duration — drawn
        /// through the Design System (UIComponents.Card + UIComponents.Label with
        /// UITheme tokens; Font/color/anchor pairing is handled inside the
        /// components, never in the window). Battles are stat-only: not clickable
        /// into a detail view. Dimensions are the single source shared with
        /// ComputeOverviewHeight to keep the scroll region height honest.
        /// </summary>
        private const float BattleCardWidth = 250f;
        private const float BattleCardHeight = 150f;
        /// <summary>v4.14: Battle KPI strip row height (StatCell minimum).</summary>
        private const float BattleKpiStripHeight = 64f;

        /// <summary>
        /// v4.14: Battle KPI strip (5 cells) — total / decisive / our kills /
        /// our losses / participants. Counters come from the Read-Model snapshot;
        /// this method only renders (v4.3 boundary).
        /// </summary>

    }
}

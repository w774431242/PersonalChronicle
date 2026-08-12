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
    /// <summary>Partial of ArchiveMainTabWindow 鈥?Overview view drawing (BUG-BASE-01 refactor).</summary>
    public sealed partial class ArchiveMainTabWindow
    {
        private void DrawOverviewContent(Rect inner, IArchiveService service)
        {
            Color prevColor = GUI.color;
            float contentHeight = ComputeOverviewHeight(inner.width);
            float viewHeight = Mathf.Max(inner.height, contentHeight);
            Rect viewRect = new Rect(inner.x, inner.y, inner.width - 16f, viewHeight);

            Widgets.BeginScrollView(inner, ref overviewScroll, viewRect);
            try
            {
            float y = viewRect.y + 4f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 28f),
                "PersonalChronicle.UI.OverviewTitle".Translate().ToString());
            y += 30f;

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.SecondaryText;
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 18f),
                "PersonalChronicle.UI.OverviewDesc".Translate().ToString());
            GUI.color = prevColor;
            Text.Font = GameFont.Small;
            y += 28f;

            for (int i = 0; i < CategoryKeys.Length; i++)
            {
                string key = CategoryKeys[i];
                if (!string.IsNullOrEmpty(overviewCategoryFilter) && overviewCategoryFilter != key)
                {
                    continue;
                }
                if (!cachedCategoryObjects.TryGetValue(key, out List<ArchiveObject> objects) || objects.Count == 0)
                {
                    continue;
                }
                y = DrawOverviewSection(viewRect, y, key, objects, service);
            }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }


        private float ComputeOverviewHeight(float width)
        {
            float height = 4f + 28f + 28f;
            const float gap = 12f;

            for (int i = 0; i < CategoryKeys.Length; i++)
            {
                string key = CategoryKeys[i];
                if (!string.IsNullOrEmpty(overviewCategoryFilter) && overviewCategoryFilter != key)
                {
                    continue;
                }
                if (!cachedCategoryObjects.TryGetValue(key, out List<ArchiveObject> objects) || objects.Count == 0)
                {
                    continue;
                }
                // v4.11 P0: Battle cards are larger (three-element layout) than the
                // generic 190x70 cards, so size the row math per category. Battle
                // dimensions reuse the shared constants so the scroll height can
                // never drift from the drawn card size.
                // v4.14: Location uses its own atlas card size + a KPI strip above
                // the cards — include both so the scroll view never clips.
                float cardWidth = 190f;
                float cardHeight = 70f;
                if (key == ArchiveCategoryKeys.Battle)
                {
                    cardWidth = BattleCardWidth;
                    cardHeight = BattleCardHeight;
                }
                else if (key == ArchiveCategoryKeys.Location)
                {
                    cardWidth = LocationCardWidth;
                    cardHeight = LocationCardHeight;
                }
                int perRow = Mathf.Max(1, (int)((width + gap) / (cardWidth + gap)));
                height += 30f; // section title
                int rows = (objects.Count - 1) / perRow + 1;
                height += rows * (cardHeight + gap);
                height += 14f;
                // v4.14: KPI strip height (one row of StatCells) above the cards.
                if (key == ArchiveCategoryKeys.Location)
                {
                    height += LocationKpiStripHeight + 8f;
                }
                else if (key == ArchiveCategoryKeys.Battle)
                {
                    height += BattleKpiStripHeight + 8f;
                }
            }
            return height + 20f;
        }


        private float DrawOverviewSection(Rect viewRect, float y, string categoryKey, List<ArchiveObject> objects, IArchiveService service)
        {
            Color prevColor = GUI.color;
            Text.Font = GameFont.Small;
            // P4-4: formatted via translation key (no hardcoded " · " glue).
            // 人物分类额外附当前活读人口数，直接回应"人物须捕捉当前殖民者数量"。
            string title = "PersonalChronicle.UI.SectionTitleCount"
                .Translate(CategoryLabel(categoryKey), objects.Count)
                .ToString();
            if (categoryKey == ArchiveCategoryKeys.Pawn)
            {
                title = title + "PersonalChronicle.UI.OverviewLiveSuffix"
                    .Translate(cachedLiveColonistCount).ToString();
            }
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 22f), title);
            y += 28f;

            // v4.11 P0: the Battle category renders richer, three-element cards
            // (trigger date / force size / repulse duration) instead of the
            // generic stat-only card. The data is captured by LinkRaidLords +
            // the Lord.Notify_PawnLost patch.
            if (categoryKey == ArchiveCategoryKeys.Battle)
            {
                // v4.14: KPI strip (5 cells) above the battle cards — total /
                // decisive / our kills / our losses / participants. Counters come
                // from the Read-Model snapshot (never re-derived in the window).
                float stripH = DrawBattleKpiStrip(viewRect, y, cachedBattleKpis);
                return y + stripH + 8f
                    + DrawBattleOverviewCards(viewRect, y + stripH + 8f, objects, 12f, service);
            }

            // v4.13 P1: the Location category renders atlas cards (identity /
            // ownership / geography / lifecycle / commerce) with inline expansion
            // of the place's chronicle. Data derives from the LocationObject
            // snapshot + the read model; no live world queries in the window.
            // Engine APIs verified against 1.6 by reflection — enabled.
            if (categoryKey == ArchiveCategoryKeys.Location)
            {
                // v4.14: KPI strip (8 cells) above the atlas cards, matching the
                // v4.13 design (total/home/quest/settle/ruined + tradable/permit/
                // factions). Counters come from the Read-Model snapshot.
                float stripH = DrawLocationKpiStrip(viewRect, y, cachedLocationKpis);
                return y + stripH + 8f
                    + DrawLocationOverviewCards(viewRect, y + stripH + 8f, objects, 12f, service);
            }

            const float cardWidth = 190f;
            const float cardHeight = 70f;
            const float gap2 = 12f;
            int perRow = Mathf.Max(1, (int)((viewRect.width + gap2) / (cardWidth + gap2)));

            // Def-driven clickability: only non-StatOnly categories are
            // navigable (Pawn/Thing drill into detail; Battle/Location are
            // stats-only). service is non-null on every call path (guarded in
            // DoWindowContents) — the null check below is defensive only.
            bool clickable = service != null
                && service.GetCategoryBehavior(categoryKey) != ArchiveDepthBehavior.StatOnly;
            for (int i = 0; i < objects.Count; i++)
            {
                ArchiveObject obj = objects[i];
                int col = i % perRow;
                int row = i / perRow;
                Rect card = new Rect(
                    viewRect.x + col * (cardWidth + gap2),
                    y + row * (cardHeight + gap2),
                    cardWidth, cardHeight);

                ArchiveUiStyle.DrawCard(card, ArchiveCardAccent(obj));
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(card.x + UITheme.CardPadX, card.y + 4f, card.width - UITheme.CardPadX * 2f, 16f),
                    clickable ? CategoryLabel(categoryKey) : "PersonalChronicle.UI.StatsOnlyNote".Translate().ToString());
                GUI.color = prevColor;

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(card.x + UITheme.CardPadX, card.y + 22f, card.width - UITheme.CardPadX * 2f, 20f), ObjectDisplayLabel(obj));

                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(card.x + UITheme.CardPadX, card.y + 44f, card.width - UITheme.CardPadX * 2f, 18f), ObjectSubLabel(obj));
                GUI.color = prevColor;

                if (clickable && Widgets.ButtonInvisible(card))
                {
                    if (categoryKey == ArchiveCategoryKeys.Pawn)
                    {
                        OpenPawnDetail(service, obj.StableId);
                    }
                    else
                    {
                        OpenWeaponDetail(service, obj.StableId);
                    }
                }
            }
            Text.Font = GameFont.Small;

            int rows = (objects.Count - 1) / perRow + 1;
            return y + rows * (cardHeight + gap2) + 14f;
        }

        // ---- Detail ------------------------------------------------------------


    }
}

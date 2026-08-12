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
    /// <summary>Partial of ArchiveMainTabWindow 鈥?WeaponDetail view drawing (BUG-BASE-01 refactor).</summary>
    public sealed partial class ArchiveMainTabWindow
    {
        private void DrawWeaponCombat(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.FactionCodexTitle".Translate().ToString());
            y = DrawFactionCodex(rect, y, service);
        }

        // ===== v4.3: faction-codex (each card = one faction; click to expand kills inline) =====

        private const float FactionCodexGap = 12f;
        /// <summary>v4.3: rows visible in the expanded detail viewport (inner scrollbar).</summary>
        private const int FactionCodexPreviewRows = 5;


        private float DrawFactionCodex(Rect rect, float y, IArchiveService service)
        {
            if (cachedFactionCodex == null || cachedFactionCodex.Count == 0)
            {
                // Restore font/colour before returning: RimWorld's Text.Font and GUI.color
                // are global IMGUI state, and leaking them corrupts every widget drawn after us.
                GameFont prevEmptyFont = Verse.Text.Font;
                Color prevEmptyColor = GUI.color;
                Verse.Text.Font = GameFont.Small;
                GUI.color = ArchiveUiStyle.Text;
                Widgets.Label(new Rect(rect.x, y, rect.width, FactionCodexEmptyRowHeight),
                    "PersonalChronicle.UI.NoKillRecords".Translate().ToString());
                GUI.color = prevEmptyColor;
                Verse.Text.Font = prevEmptyFont;
                return y + FactionCodexEmptyRowHeight + 4f;
            }

            int cols = rect.width >= FactionCodexTwoColumnWidth ? 2 : 1;
            float cardW = (rect.width - FactionCodexGap * (cols - 1)) / cols;
            int drawnInRow = 0;
            float rowStartY = y;
            float rowMaxH = 0f;

            for (int i = 0; i < cachedFactionCodex.Count; i++)
            {
                FactionCodexView card = cachedFactionCodex[i];
                bool expanded = expandedFactions.Contains(card.FactionKey);
                float cardH = FactionCodexCardHeight(card, expanded);
                float cardX = rect.x + drawnInRow * (cardW + FactionCodexGap);
                Rect cardRect = new Rect(cardX, y, cardW, cardH);
                DrawFactionCodexCard(cardRect, card, expanded, service);
                if (Widgets.ButtonInvisible(cardRect))
                {
                    if (expandedFactions.Contains(card.FactionKey))
                    {
                        expandedFactions.Remove(card.FactionKey);
                    }
                    else
                    {
                        expandedFactions.Add(card.FactionKey);
                    }
                }
                rowMaxH = Mathf.Max(rowMaxH, cardH);
                drawnInRow++;
                if (drawnInRow >= cols)
                {
                    y += rowMaxH + FactionCodexGap;
                    drawnInRow = 0;
                    rowMaxH = 0f;
                }
            }
            if (drawnInRow > 0)
            {
                y += rowMaxH;
            }
            return y;
        }

        /// <summary>
        /// Row pitch inside the expanded detail viewport (row height + separator).
        /// </summary>
        private const float FactionCodexRowPitch = TimelineRowHeight + 2f;

        /// <summary>
        /// Height of a codex card. MUST stay in sync with the layout in
        /// <see cref="DrawFactionCodexCard"/> — both derive from the same constants so the
        /// expanded viewport can never overflow the card background.
        /// Layout: padding → header → stats → gap → bar → padding [→ gap + viewport].
        /// </summary>

        private static float FactionCodexCardHeight(FactionCodexView card, bool expanded)
        {
            float h = FactionCodexPadding
                + FactionCodexHeaderHeight + UITheme.GridGap
                + FactionCodexStatHeight + UITheme.GridGap
                + FactionCodexBarHeight
                + FactionCodexPadding;
            if (expanded && card.MemberLines != null && card.MemberLines.Count > 0)
            {
                // Fixed 5-row viewport: no matter how many kills, the card keeps its
                // height and the rows scroll inside it.
                h += 8f + FactionCodexPreviewRows * FactionCodexRowPitch;
            }
            return h;
        }


        private void DrawFactionCodexCard(Rect rect, FactionCodexView card, bool expanded, IArchiveService service)
        {
            // Snapshot every piece of global IMGUI state we are about to touch,
            // and restore all three before returning (see the tail of this method).
            Color previous = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Text.Anchor;

            Color accent = ArchiveUiStyle.FactionAccent(card.Kind);
            ArchiveUiStyle.DrawCard(rect, accent);

            // Header: dot + name + kind badge + relation badge.
            float hx = rect.x + FactionCodexPadding;
            float hy = rect.y + FactionCodexPadding;
            Widgets.DrawBoxSolid(new Rect(hx, hy, FactionCodexDotSize, FactionCodexDotSize), accent);
            // Name column stops before the relation badge so long faction names
            // (or a long third-party mod label) can never overlap it.
            float nameX = hx + FactionCodexDotSize + 6f;
            float nameW = Mathf.Max(0f, rect.xMax - FactionCodexPadding - FactionCodexRelationWidth - 6f - nameX);
            Text.Anchor = TextAnchor.MiddleLeft;
            Verse.Text.Font = GameFont.Small;
            GUI.color = ArchiveUiStyle.Text;
            Widgets.Label(new Rect(nameX, hy - 1f, nameW, 18f), card.DisplayName);
            Verse.Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Muted;
            Widgets.Label(new Rect(nameX, hy + 15f, nameW, 16f), FactionKindLabel(card.Kind));
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = RelationColor(card.RelationKey);
            Widgets.Label(new Rect(rect.xMax - FactionCodexPadding - FactionCodexRelationWidth, hy, FactionCodexRelationWidth, 18f),
                RelationLabel(card.RelationKey));
            Text.Anchor = TextAnchor.UpperLeft;

            // 4 stat cells. statsY matches FactionCodexCardHeight: padding + header + gap.
            float statsY = rect.y + FactionCodexPadding + FactionCodexHeaderHeight + UITheme.GridGap;
            float innerW = rect.width - FactionCodexPadding * 2f;
            float statW = (innerW - FactionCodexGap * 3) / 4f;
            float statX = rect.x + FactionCodexPadding;
            DrawFactionStat(new Rect(statX, statsY, statW, FactionCodexStatHeight), card.KillCount.ToString(), "PersonalChronicle.UI.StatKills".Translate().ToString(), ArchiveUiStyle.Dead);
            DrawFactionStat(new Rect(statX + statW + FactionCodexGap, statsY, statW, FactionCodexStatHeight), card.RaidCount.ToString(), "PersonalChronicle.UI.StatRaids".Translate().ToString(), ArchiveUiStyle.TimelineBattle);
            DrawFactionStat(new Rect(statX + (statW + FactionCodexGap) * 2, statsY, statW, FactionCodexStatHeight), card.BattleCount.ToString(), "PersonalChronicle.UI.StatBattles".Translate().ToString(), ArchiveUiStyle.Info);
            // 4th cell: player card shows real losses; enemy cards cannot attribute
            // our losses from kill events (victim is the enemy), so show "—" to avoid a misleading 0.
            string lossText = card.Kind == ArchiveUiStyle.FactionCodexKind.Player
                ? card.OurLossCount.ToString()
                : "—";
            DrawFactionStat(new Rect(statX + (statW + FactionCodexGap) * 3, statsY, statW, FactionCodexStatHeight), lossText, "PersonalChronicle.UI.StatOurLosses".Translate().ToString(), ArchiveUiStyle.TimelineDeath);

            // Composition bar (victim-kind breakdown), segmented by proportion.
            float barY = statsY + FactionCodexStatHeight + UITheme.GridGap;
            Rect barRect = new Rect(statX, barY, innerW, FactionCodexBarHeight);
            Widgets.DrawBoxSolid(barRect, ArchiveUiStyle.BorderSoft);
            if (card.Composition != null && card.KillCount > 0)
            {
                float segX = barRect.x;
                for (int ci = 0; ci < card.Composition.Count; ci++)
                {
                    int cnt = card.Composition[ci].Value;
                    float segW = barRect.width * ((float)cnt / card.KillCount);
                    Color segColor = CompositionColor(ci);
                    Widgets.DrawBoxSolid(new Rect(segX, barY, Mathf.Max(0f, segW - 1f), FactionCodexBarHeight), segColor);
                    segX += segW;
                }
            }
            else
            {
                Widgets.DrawBoxSolid(barRect, accent);
            }

            // Expanded kill detail: fixed-height viewport with an inner scrollbar
            // (all rows scrollable inside the card; card height stays constant).
            if (expanded && card.MemberLines != null && card.MemberLines.Count > 0)
            {
                float dy = barY + FactionCodexBarHeight + UITheme.GridGap;
                float rowH = FactionCodexRowPitch;
                float viewH = FactionCodexPreviewRows * rowH;
                float contentH = card.MemberLines.Count * rowH;
                Rect viewRect = new Rect(statX, dy, innerW, viewH);
                // Reserve right edge for the scrollbar when content overflows.
                Rect contentRect = new Rect(0f, 0f, viewRect.width - (contentH > viewH ? FactionCodexScrollbarWidth : 0f), contentH);
                if (!expandedScroll.TryGetValue(card.FactionKey, out Vector2 scrollPos))
                {
                    scrollPos = Vector2.zero;
                }

                Widgets.BeginScrollView(viewRect, ref scrollPos, contentRect);
                try
                {
                    Verse.Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    for (int r = 0; r < card.MemberLines.Count; r++)
                    {
                        CombatLineView kv = card.MemberLines[r];
                        float rowY = r * rowH;
                        Rect row = new Rect(0f, rowY, contentRect.width, TimelineRowHeight);
                        // Title column is what remains after the date column and the weapon column.
                        float titleW = row.width - FactionCodexTitleColOffset - FactionCodexSubColWidth - 4f;
                        GUI.color = ArchiveUiStyle.Muted;
                        Widgets.Label(new Rect(row.x, row.y, FactionCodexDateColWidth, TimelineRowHeight), kv.DateText);
                        GUI.color = ArchiveUiStyle.Text;
                        Widgets.Label(new Rect(row.x + FactionCodexTitleColOffset, row.y, Mathf.Max(0f, titleW), TimelineRowHeight), kv.TitleText);
                        GUI.color = ArchiveUiStyle.Accent;
                        Widgets.Label(new Rect(row.xMax - FactionCodexSubColWidth, row.y, FactionCodexSubColWidth, TimelineRowHeight), kv.SubText);
                    }
                }
                finally
                {
                    // EndScrollView must always run: an escaped exception would leave
                    // Unity's GUI clip stack unbalanced and break every later window.
                    Widgets.EndScrollView();
                }
                expandedScroll[card.FactionKey] = scrollPos;
            }

            GUI.color = previous;
            Verse.Text.Font = prevFont;
            Text.Anchor = prevAnchor;
        }


        private static void DrawFactionStat(Rect rect, string number, string label, Color numColor)
        {
            // v4.5.4: reuse the shared StatCell so every KPI cell shares one
            // renderer and left-aligned rhythm (was a centred hand-rolled twin).
            UIComponents.StatCell(rect, label, number, numColor);
        }


        private static string FactionKindLabel(ArchiveUiStyle.FactionCodexKind kind)
        {
            switch (kind)
            {
                case ArchiveUiStyle.FactionCodexKind.Enemy: return "PersonalChronicle.UI.FactionKindEnemy".Translate().ToString();
                case ArchiveUiStyle.FactionCodexKind.Mechanoid: return "PersonalChronicle.UI.FactionKindMechanoid".Translate().ToString();
                case ArchiveUiStyle.FactionCodexKind.Animal: return "PersonalChronicle.UI.FactionKindAnimal".Translate().ToString();
                default: return "PersonalChronicle.UI.FactionKindUnknown".Translate().ToString();
            }
        }


        private static string RelationLabel(string relationKey)
        {
            if (relationKey == FactionRelationHostile) return "PersonalChronicle.UI.FactionRelHostile".Translate().ToString();
            if (relationKey == FactionRelationNeutral) return "PersonalChronicle.UI.FactionRelNeutral".Translate().ToString();
            if (relationKey == FactionRelationAlly) return "PersonalChronicle.UI.FactionRelAlly".Translate().ToString();
            return "PersonalChronicle.UI.FactionRelUnresolved".Translate().ToString();
        }

        private static readonly Color[] CompositionPalette = new Color[]
        {
            ArchiveUiStyle.TimelineBattle,
            ArchiveUiStyle.Info,
            ArchiveUiStyle.Alive,
            ArchiveUiStyle.Muted
        };


        private static Color CompositionColor(int index)
        {
            return CompositionPalette[index % CompositionPalette.Length];
        }


        private static Color RelationColor(string relationKey)
        {
            if (relationKey == FactionRelationHostile) return ArchiveUiStyle.Dead;
            if (relationKey == FactionRelationAlly) return ArchiveUiStyle.Alive;
            return ArchiveUiStyle.Muted;
        }


        private float DrawCombatLineList(Rect rect, float y, IArchiveService service, List<CombatLineView> lines, string emptyKey)
        {
            if (lines == null || lines.Count == 0)
            {
                GameFont prevFont = Verse.Text.Font;
                Verse.Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, FactionCodexEmptyRowHeight), emptyKey.Translate().ToString());
                Verse.Text.Font = prevFont;
                return y + FactionCodexEmptyRowHeight + 4f;
            }
            for (int i = 0; i < lines.Count; i++)
            {
                CombatLineView line = lines[i];
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                DrawEventRow(row, line.DateText, line.TitleText, line.SubText);
                if ((line.Target != NavTarget.None || line.TargetEvent != null) && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, line.Target, line.StableId, line.TargetEvent);
                }
                y += TimelineRowHeight;
            }
            return y;
        }


    }
}

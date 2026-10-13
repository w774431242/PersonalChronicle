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

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>Partial of PersonnelArchivePanel —WeaponDetail view drawing (BUG-BASE-01 refactor).</summary>
    public sealed partial class PersonnelArchivePanel
    {
        private void DrawWeaponCombat(Rect rect, IArchiveService service)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.FactionCodexTitle".Translate().ToString());
            y = DrawFactionCodex(rect, y, service);
        }

        // ===== v4.3: faction-codex (each card = one faction; click to expand kills inline) =====

        private const float FactionCodexGap = 12f;
        /// <summary>v4.3: rows visible in the expanded detail viewport (inner scrollbar).
        /// v6.x：5 → 6（预览 t3 HTML 1784「展开后最多 6 行伤亡」）。</summary>
        private const int FactionCodexPreviewRows = 6;


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
        /// 折叠态表头下的「明细 2 行」单行行距 —— 沿用 <see cref="DrawDetailRow"/> 的固定 26
        /// （行高是字体度量，铁律 1；预览 <c>.drow</c> ≈ 15px × 尺度 ≈ 26）。
        /// </summary>
        private const float FactionCodexSummaryRowH = 26f;

        /// <summary>
        /// Height of a codex card. MUST stay in sync with the layout in
        /// <see cref="DrawFactionCodexCard"/> — both derive from the same constants so the
        /// expanded viewport can never overflow the card background.
        /// 折叠态：padding → header → gap → 2 行明细（击杀 / 主力·关系）→ padding（≈106）。
        /// 展开态：再追加 4 格统计 → 组成条 → 固定 <see cref="FactionCodexPreviewRows"/> 行伤亡视口。
        /// </summary>
        /// <remarks>
        /// 预览卡高 ≈68px 是**预览页尺度**（.drow 字号 10.5px）；本工程行高锚定 Tiny=18 / Small=22，
        /// 换算后折叠态约 106px —— 与预览同比，不是偏差（对照清单登记尺度换算）。
        /// </remarks>
        private static float FactionCodexCardHeight(FactionCodexView card, bool expanded)
        {
            float h = FactionCodexPadding
                + FactionCodexHeaderHeight + UITheme.GridGap
                + FactionCodexSummaryRowH * 2f
                + FactionCodexPadding;
            if (expanded)
            {
                // 4 格统计 + 组成条原本在折叠态，v6.x 移入展开态（预览 HTML 1781-1786 的展开语义）。
                h += UITheme.GridGap
                    + FactionCodexStatHeight + UITheme.GridGap
                    + FactionCodexBarHeight;
                if (card.MemberLines != null && card.MemberLines.Count > 0)
                {
                    // Fixed viewport: no matter how many kills, the card keeps its
                    // height and the rows scroll inside it.
                    h += UITheme.GridGap + FactionCodexPreviewRows * FactionCodexRowPitch;
                }
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

            // 折叠态：2 行明细（预览 HTML 1760-1761「击杀 / 主力·关系」）——
            // 由 4 格统计 + 组成条降高而来；那两块搬进展开态（见下）。
            float innerW = rect.width - FactionCodexPadding * 2f;
            float rowY = rect.y + FactionCodexPadding + FactionCodexHeaderHeight + UITheme.GridGap;
            rowY = DrawDetailRow(rect.x + FactionCodexPadding, rowY, innerW,
                "PersonalChronicle.UI.StatKills".Translate().ToString(), card.KillCount.ToString());
            bool hasKills = card.KillCount > 0;
            rowY = DrawDetailRow(rect.x + FactionCodexPadding, rowY, innerW,
                hasKills
                    ? "PersonalChronicle.UI.FactionCodexMainKinds".Translate().ToString()
                    : "PersonalChronicle.UI.FactionCodexRelation".Translate().ToString(),
                hasKills ? FactionCodexMainKindText(card) : RelationLabel(card.RelationKey));

            if (expanded)
            {
                // === 展开态（预览 HTML 1781-1786）：4 格统计 + 组成条 + 固定行数伤亡视口 ===
                float statX = rect.x + FactionCodexPadding;
                float statsY = rowY + UITheme.GridGap;
                float statW = (innerW - FactionCodexGap * 3) / 4f;
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
                if (card.MemberLines != null && card.MemberLines.Count > 0)
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
                            float rowY2 = r * rowH;
                            Rect row = new Rect(0f, rowY2, contentRect.width, TimelineRowHeight);
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
            }

            GUI.color = previous;
            Verse.Text.Font = prevFont;
            Text.Anchor = prevAnchor;
        }

        /// <summary>
        /// 折叠态第 2 行「主力」文本：取该阵营受害种类（<see cref="FactionCodexView.Composition"/>）
        /// 中计数最高的一档，解析为 <see cref="PawnKindDef"/> 的 label（缺失则回退原始 defName）。
        /// </summary>
        /// <param name="card">图鉴卡；组成表为空或全是未知桶时返回 "—"。</param>
        /// <returns>主力受害种类显示名。</returns>
        /// <remarks>
        /// 组成表是**已缓存的小视图**（长度 = 该阵营出现过的受害种类数，通常 1~3），此处的取最大值
        /// 只读缓存、零分配，不触碰数据存储（铁律 3）；Def 解析走 <c>DefDatabase</c> 字典，
        /// 与既有 <c>ThingDefLabel</c> / <c>FactionDefLabel</c> 同一约定。
        /// 不画 0 击杀卡的「主力」：无击杀时调用方改画「关系」，避免用 "—" 冒充主力。
        /// </remarks>
        private static string FactionCodexMainKindText(FactionCodexView card)
        {
            if (card == null || card.Composition == null || card.Composition.Count == 0)
            {
                return "—";
            }
            int bestCount = -1;
            string bestKey = null;
            for (int i = 0; i < card.Composition.Count; i++)
            {
                KeyValuePair<string, int> entry = card.Composition[i];
                if (string.IsNullOrEmpty(entry.Key) || entry.Key == FactionBucketUnknown)
                {
                    continue;
                }
                if (entry.Value > bestCount)
                {
                    bestCount = entry.Value;
                    bestKey = entry.Key;
                }
            }
            if (bestKey == null)
            {
                return "—";
            }
            string label = PawnKindDefLabel(bestKey);
            return string.IsNullOrEmpty(label) ? bestKey : label;
        }

        /// <summary>PawnKindDef defName → 显示名；Def 缺失返回 null（调用方回退原始 defName）。</summary>
        private static string PawnKindDefLabel(string defName)
        {
            PawnKindDef def = DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
            return def != null && !string.IsNullOrEmpty(def.label) ? def.label : null;
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

        private static Color CompositionColor(int index)
        {
            switch (index % 4)
            {
                case 0: return ArchiveUiStyle.TimelineBattle;
                case 1: return ArchiveUiStyle.Info;
                case 2: return ArchiveUiStyle.Alive;
                default: return ArchiveUiStyle.Muted;
            }
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

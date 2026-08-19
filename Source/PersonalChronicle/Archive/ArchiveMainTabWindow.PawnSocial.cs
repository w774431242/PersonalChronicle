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


    }
}

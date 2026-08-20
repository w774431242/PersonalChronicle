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
    /// <summary>Partial of ArchiveMainTabWindow 鈥?EventDetail view drawing (BUG-BASE-01 refactor).</summary>
    public sealed partial class ArchiveMainTabWindow
    {
        private string CurrentTabKey()
        {
            string[] tabs = cachedDetailObject is ThingObject ? WeaponTabKeys : PawnTabKeys;
            if (detailTabIndex >= 0 && detailTabIndex < tabs.Length)
            {
                return tabs[detailTabIndex];
            }
            return "Overview";
        }

        // ---- Detail: timeline (shared) ----------------------------------------


        private void DrawDetailTimeline(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            float y = DrawTimelineToolbar(rect, rect.y);
            int visibleCount = 0;
            for (int i = 0; i < cachedDetailEvents.Count; i++)
            {
                EventLineView line = cachedDetailEvents[i];
                if (line.Event == null || !ShouldShowTimelineEvent(line.Event))
                {
                    continue;
                }
                visibleCount++;
                float descHeight = string.IsNullOrEmpty(line.DescriptionText)
                    ? 0f
                    : Text.CalcHeight(line.DescriptionText, rect.width - 8f) + 4f;
                float chipsHeight = line.Chips != null && line.Chips.Count > 0 ? ChipRowHeight : 0f;
                float rowHeight = TimelineRowHeight + descHeight + chipsHeight;
                Rect row = new Rect(rect.x, y, rect.width, rowHeight);

                ArchiveUiStyle.DrawSectionMarker(
                    new Rect(row.x, row.y + 3f, 4f, row.height - 6f),
                    ImportanceColor(ChronicleEventImportance.Resolve(line.Event)));
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(row.x + 4f, row.y + 3f, 150f, 18f), line.DateText);

                // 窄面板保护：固定列宽减法可能产生负宽（小屏/压缩窗口），clamp 到 0。
                Text.Font = GameFont.Small;
                float titleW = Mathf.Max(0f, row.width - 158f - 190f);
                Widgets.Label(new Rect(row.x + 158f, row.y + 3f, titleW, 20f), line.NameText);

                // 右对齐列：面板过窄时整体隐藏（避免与标题列重叠/越界）。
                if (row.width >= 186f + 158f)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = UITheme.SecondaryText;
                    Widgets.Label(new Rect(row.x + row.width - 186f, row.y + 3f, 182f, 18f), line.ParamsText);
                }
                GUI.color = prevColor;

                float cy = row.y + TimelineRowHeight;
                if (descHeight > 0f)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = UITheme.SecondaryText;
                    Widgets.Label(new Rect(row.x + 4f, cy, row.width - 8f, descHeight), line.DescriptionText);
                    GUI.color = prevColor;
                    cy += descHeight;
                }
                if (chipsHeight > 0f)
                {
                    DrawChips(new Rect(row.x + 4f, cy, row.width - 8f, ChipRowHeight), line.Chips, service);
                }

                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width);
                y += rowHeight;
            }
            if (visibleCount == 0)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x, y, rect.width, 24f),
                    "PersonalChronicle.UI.NoTimelineMatches".Translate().ToString());
            }
        }


        private float DrawTimelineToolbar(Rect rect, float y)
        {
            Rect bar = new Rect(rect.x, y, rect.width, 34f);
            ArchiveUiStyle.DrawCard(bar, ArchiveUiStyle.Info);
            float x = bar.x + 8f;
            float toggleWidth = Mathf.Min(145f, Mathf.Max(90f, (bar.width - 230f) / 3f));
            timelineShowCareer = DrawTimelineToggle(
                new Rect(x, bar.y + 5f, toggleWidth, 24f),
                timelineShowCareer,
                "PersonalChronicle.UI.TimelineCareerLayer".Translate().ToString());
            x += toggleWidth + 4f;
            timelineShowCombat = DrawTimelineToggle(
                new Rect(x, bar.y + 5f, toggleWidth, 24f),
                timelineShowCombat,
                "PersonalChronicle.UI.TimelineCombatLayer".Translate().ToString());
            x += toggleWidth + 4f;
            timelineShowSocial = DrawTimelineToggle(
                new Rect(x, bar.y + 5f, toggleWidth, 24f),
                timelineShowSocial,
                "PersonalChronicle.UI.TimelineSocialLayer".Translate().ToString());

            Rect importance = new Rect(bar.xMax - 210f, bar.y + 5f, 202f, 24f);
            ArchiveUiStyle.DrawBadge(importance, ImportanceFilterLabelText(), ImportanceColor(timelineMinimumImportance));
            if (Widgets.ButtonInvisible(importance))
            {
                int next = ((int)timelineMinimumImportance + 1) % ((int)ChronicleImportance.Critical + 1);
                timelineMinimumImportance = (ChronicleImportance)next;
            }
            return y + 42f;
        }


        private static bool DrawTimelineToggle(Rect rect, bool enabled, string label)
        {
            Color prevColor = GUI.color;
            ArchiveUiStyle.DrawSelectedNavigation(rect, enabled);
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = enabled ? ArchiveUiStyle.Text : ArchiveUiStyle.Muted;
            Widgets.Label(new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height), label);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = prevColor;
            if (Widgets.ButtonInvisible(rect))
            {
                enabled = !enabled;
            }
            return enabled;
        }


        private bool ShouldShowTimelineEvent(ChronicleEvent ev)
        {
            if (ev == null || ChronicleEventImportance.Resolve(ev) < timelineMinimumImportance)
            {
                return false;
            }
            if (IsSocialEvent(ev))
            {
                return timelineShowSocial;
            }
            if (IsDeathEvent(ev) || IsBattleEvent(ev))
            {
                return timelineShowCombat;
            }
            if (IsCraftEvent(ev) || IsBuiltEvent(ev) || ev.TypeKey == ChronicleEventType.Join)
            {
                return timelineShowCareer;
            }
            return true;
        }


        private static string ImportanceLabel(ChronicleImportance importance)
        {
            switch (importance)
            {
                case ChronicleImportance.Critical:
                    return "PersonalChronicle.UI.ImportanceCritical".Translate().ToString();
                case ChronicleImportance.Important:
                    return "PersonalChronicle.UI.ImportanceImportant".Translate().ToString();
                case ChronicleImportance.Normal:
                    return "PersonalChronicle.UI.ImportanceNormal".Translate().ToString();
                default:
                    return "PersonalChronicle.UI.ImportanceRoutine".Translate().ToString();
            }
        }


        private string ImportanceFilterLabelText()
        {
            return "PersonalChronicle.UI.TimelineImportance".Translate(ImportanceLabel(timelineMinimumImportance)).ToString();
        }


        private static Color ImportanceColor(ChronicleImportance importance)
        {
            switch (importance)
            {
                case ChronicleImportance.Critical:
                    return ArchiveUiStyle.Dead;
                case ChronicleImportance.Important:
                    return ArchiveUiStyle.Accent;
                case ChronicleImportance.Normal:
                    return ArchiveUiStyle.Info;
                default:
                    return ArchiveUiStyle.Muted;
            }
        }


        private void DrawChips(Rect rect, List<ChipView> chips, IArchiveService service)
        {
            Color prevColor = GUI.color;
            float x = rect.x;
            Text.Font = GameFont.Tiny;
            for (int i = 0; i < chips.Count; i++)
            {
                if (x >= rect.xMax)
                {
                    break;
                }
                ChipView chip = chips[i];
                float chipWidth = Mathf.Min(Text.CalcSize(chip.Label).x + 16f, rect.xMax - x);
                Rect chipRect = new Rect(x, rect.y, chipWidth, ChipRowHeight - 4f);
                if (chip.Target != NavTarget.None)
                {
                    Widgets.DrawHighlightIfMouseover(chipRect);
                }
                GUI.color = chip.Target != NavTarget.None
                    ? UITheme.PillBlue
                    : UITheme.SecondaryText;
                Widgets.Label(chipRect, chip.Label);
                GUI.color = prevColor;
                if (chip.Target != NavTarget.None && Widgets.ButtonInvisible(chipRect))
                {
                    NavigateTarget(service, chip.Target, chip.StableId, null);
                }
                x = chipRect.xMax + 6f;
            }
        }

        // ---- Event view --------------------------------------------------------


        private void DrawEventContent(Rect inner, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            bool scrollBegan = false;
            try
            {
            if (cachedEventDetail == null)
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(inner.x, inner.y + 10f, inner.width, 24f),
                    "PersonalChronicle.UI.NoEventSelected".Translate().ToString());
                return;
            }

            float contentHeight = ComputeEventHeight(inner.width);
            float viewHeight = Mathf.Max(inner.height, contentHeight);
            Rect viewRect = new Rect(inner.x, inner.y, inner.width - 16f, viewHeight);

            Widgets.BeginScrollView(inner, ref eventScroll, viewRect);
            scrollBegan = true;

            float y = viewRect.y + 4f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 28f),
                "PersonalChronicle.UI.Events".Translate().ToString());
            y += 30f;

            Text.Font = GameFont.Tiny;
            GUI.color = UITheme.SecondaryText;
            string desc = FormatDate(cachedEventDetail.Tick);
            if (cachedEventDetail.Primary != null && !string.IsNullOrEmpty(cachedEventDetail.Primary.LabelSnapshot))
            {
                desc = desc + " · " + cachedEventDetail.Primary.LabelSnapshot;
            }
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 18f), desc);
            GUI.color = prevColor;
            y += 26f;

            // Association network tree panel.
            Rect treePanel = new Rect(viewRect.x, y, viewRect.width, 34f + cachedEventTree.Count * 22f + 12f);
            ArchiveUiStyle.DrawPanel(treePanel, ArchiveUiStyle.PanelRaised);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(treePanel.x + 10f, treePanel.y + 6f, treePanel.width - 20f, 22f),
                "PersonalChronicle.UI.RelatedNetwork".Translate().ToString());

            float ty = treePanel.y + 32f;
            DrawTree(viewRect.x + 10f, ty, treePanel.width - 20f, service);
            y = treePanel.yMax + 18f;

            // Event description. 高度按真实文本测量（Text.CalcHeight），长描述可换行
            // 展开而不是被固定 90f 框裁剪（UI-009 多语言布局）；上限 300f 防极端文本撑爆。
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(viewRect.x, y, viewRect.width, 22f),
                "PersonalChronicle.UI.EventDescription".Translate().ToString());
            y += 28f;

            Text.Font = GameFont.Tiny;
            float descTextW = viewRect.width - 24f;
            float descTextH = Text.CalcHeight(
                string.IsNullOrEmpty(cachedEventDescription)
                    ? "PersonalChronicle.UI.NoEvents".Translate().ToString()
                    : cachedEventDescription,
                descTextW);
            float descBoxH = Mathf.Clamp(descTextH + 20f, 90f, 300f);
            Rect descBox = new Rect(viewRect.x, y, viewRect.width, descBoxH);
            UIComponents.TintedBox(descBox, UITheme.OverlayWhite04);
            DrawBorder(descBox, ArchiveUiStyle.Border);
            GUI.color = ArchiveUiStyle.SecondaryText;
            Widgets.Label(new Rect(descBox.x + 12f, descBox.y + 10f, descTextW, descBox.height - 20f),
                string.IsNullOrEmpty(cachedEventDescription)
                    ? "PersonalChronicle.UI.NoEvents".Translate().ToString()
                    : cachedEventDescription);
            GUI.color = prevColor;
            Text.Font = GameFont.Small;

            }
            finally
            {
                // v4.17 体检：cachedEventDetail 为空时从未 BeginScrollView——
                // 旧版无条件 EndScrollView 会多弹出一层 GUI group，窗口错位。
                if (scrollBegan)
                {
                    Widgets.EndScrollView();
                }
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }


        private float ComputeEventHeight(float width)
        {
            // v4.5.4: named constants mirror DrawEventContent's layout — keep in
            // sync when the draw path changes.
            const float topPad = 4f;
            const float titleH = 28f;
            const float titleGap = 26f;
            const float treeHeaderH = 34f;
            const float treeRowH = 22f;
            const float treePad = 12f;
            const float treeGap = 18f;
            const float descTitleH = 22f;
            const float descGap = 28f;
            const float bottomPad = 20f;
            float treeHeight = treeHeaderH + Mathf.Max(1, cachedEventTree.Count) * treeRowH + treePad;
            // 描述框高度与绘制路径同口径（Tiny 字体实际文本测量，clamp 90~300）。
            float descTextW = width - 24f;
            float descTextH = Text.CalcHeight(
                string.IsNullOrEmpty(cachedEventDescription)
                    ? "PersonalChronicle.UI.NoEvents".Translate().ToString()
                    : cachedEventDescription,
                descTextW);
            float descBoxH = Mathf.Clamp(descTextH + 20f, 90f, 300f);
            return topPad + titleH + titleGap + treeHeight + treeGap
                + descTitleH + descGap + descBoxH + bottomPad;
        }


        private void DrawTree(float x, float y, float width, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            const float indent = 18f;
            const float stub = 12f;
            const float rowHeight = 22f;

            Text.Font = GameFont.Tiny;
            try
            {
            for (int i = 0; i < cachedEventTree.Count; i++)
            {
                TreeLineView line = cachedEventTree[i];
                float rowY = y + i * rowHeight;

                if (line.Depth > 0)
                {
                    float stubX = line.Depth == 1
                        ? x + indent
                        : x + (line.Depth - 1) * indent + stub;
                    Widgets.DrawLineHorizontal(stubX, rowY + rowHeight / 2f, stub);
                }

                // Branch (depth 1 with children) draws a vertical connector down
                // to its last leaf.
                if (line.Depth == 1 && i + 1 < cachedEventTree.Count && cachedEventTree[i + 1].Depth > 1)
                {
                    int last = i + 1;
                    while (last + 1 < cachedEventTree.Count && cachedEventTree[last + 1].Depth > 1)
                    {
                        last++;
                    }
                    float vx = x + indent + stub;
                    float fromY = rowY + rowHeight / 2f;
                    float toY = y + last * rowHeight + rowHeight / 2f;
                    Widgets.DrawLineVertical(vx, fromY, toY - fromY);
                }

                float labelX = x + line.Depth * indent + stub + 6f;
                Rect labelRect = new Rect(labelX, rowY, width - (labelX - x), rowHeight);
                GUI.color = line.Target != NavTarget.None
                    ? UITheme.PillBlue
                    : UITheme.SecondaryText;
                Widgets.Label(labelRect, line.Label);
                GUI.color = prevColor;

                if (line.Target != NavTarget.None)
                {
                    Widgets.DrawHighlightIfMouseover(labelRect);
                    if (Widgets.ButtonInvisible(labelRect))
                    {
                        NavigateTarget(service, line.Target, line.StableId, line.TargetEvent);
                    }
                }
            }
            }
            finally
            {
                // v4.17 体检：恢复字体（旧实现循环后 Text.Font 停留在 Tiny，泄漏给后续绘制）。
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        // ---- Shared UI helpers -------------------------------------------------


    }
}

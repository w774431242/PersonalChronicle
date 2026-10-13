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
using ReadModels = PersonalChronicle.Application.ReadModels;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// Partial of <see cref="PersonnelArchivePanel"/> —see main file for the class doc.
    /// </summary>
    public sealed partial class PersonnelArchivePanel
    {













        private void DrawDetailContent(Rect inner, IArchiveService service)
        {
            if (cachedDetailObject == null)
            {
                GoOverview(null);
                return;
            }

            // Keep the object identity and tab bar fixed. Only the selected
            // child container scrolls, so every tab gets an independent safe
            // viewport instead of being clipped by a guessed total height.
            float y = inner.y + 4f;
            y = DrawObjectHeader(inner, y, service);
            y = DrawTabBar(inner, y, service);

            Rect panelRect = new Rect(
                inner.x,
                y,
                inner.width,
                Mathf.Max(60f, inner.yMax - y));
            float contentHeight = ComputeDetailPanelHeight(panelRect);
            float viewWidth = Mathf.Max(1f, panelRect.width - 16f);
            Rect viewRect = new Rect(
                panelRect.x,
                panelRect.y,
                viewWidth,
                Mathf.Max(panelRect.height, contentHeight));

            ArchiveUiStyle.DrawPanel(panelRect, ArchiveUiStyle.PanelRaised);
            Widgets.BeginScrollView(panelRect, ref detailScroll, viewRect);
            try
            {
                DrawDetailPanel(viewRect, service);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private float DrawTabBar(Rect rect, float y, IArchiveService service)
        {
            string[] tabs = cachedDetailObject is ThingObject ? WeaponTabKeys : PawnTabKeys;
            if (detailTabIndex >= tabs.Length)
            {
                detailTabIndex = 0;
            }

            // v3.1: fewer tabs (5/4) — use equal flex width, min clickable ~72.
            float tabWidth = Mathf.Max(72f, (rect.width - 6f) / tabs.Length - 2f);
            const float tabHeight = 28f;
            const float tabGap = 2f;

            Text.Font = GameFont.Small;
            for (int i = 0; i < tabs.Length; i++)
            {
                Rect tab = new Rect(rect.x + i * (tabWidth + tabGap), y, tabWidth, tabHeight);
                bool selected = i == detailTabIndex;
                ArchiveUiStyle.DrawSelectedNavigation(tab, selected);
                string label = TabLabel(tabs[i]);
                UIComponents.Label(tab, label, GameFont.Small,
                    selected ? UITheme.Text : UITheme.Muted, TextAnchor.MiddleCenter);
                if (selected)
                {
                    UIComponents.Rule(new Rect(tab.x, tab.yMax - 2f, tab.width, 2f), UITheme.Accent);
                }
                if (Widgets.ButtonInvisible(tab))
                {
                    detailTabIndex = i;
                    detailScroll = Vector2.zero;
                    socialNetworkZoom = 1f;
                    socialNetworkZoomTouched = false;
                    socialNetworkPan = Vector2.zero;
                }
            }

            ArchiveUiStyle.DrawRule(new Rect(rect.x, y + tabHeight + 2f, rect.width, 1f), ArchiveUiStyle.Border);
            return y + tabHeight + 8f;
        }

        private void DrawDetailPanel(Rect panel, IArchiveService service)
        {
            string[] tabs = cachedDetailObject is ThingObject ? WeaponTabKeys : PawnTabKeys;
            string tab = detailTabIndex >= 0 && detailTabIndex < tabs.Length ? tabs[detailTabIndex] : "Overview";
            bool isWeapon = cachedDetailObject is ThingObject;

            switch (tab)
            {
                case "Overview":
                    if (isWeapon)
                    {
                        DrawWeaponOverview(panel, service);
                    }
                    else
                    {
                        DrawPawnOverview(panel, service);
                    }
                    break;
                case "Career":
                    DrawCareerTab(panel, service);
                    break;
                case "CombatLog":
                    if (isWeapon)
                    {
                        DrawWeaponCombat(panel, service);
                    }
                    else
                    {
                        DrawPawnCombat(panel, service);
                    }
                    break;
                case "Social":
                    DrawSocialTab(panel, service);
                    break;
                case "Health":
                    DrawPawnHealth(panel, service);
                    break;
                case "Legacy":
                    DrawLegacyTab(panel, service);
                    break;
                case "Origin":
                    DrawOriginTab(panel, service);
                    break;
                case "CoUse":
                    DrawCoUseTab(panel, service);
                    break;
                case "Decommission":
                    DrawDecommissionTab(panel, service);
                    break;
                default:
                    DrawCapturePlaceholder(panel);
                    break;
            }
        }

        private static string RelationDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return "—";
            }
            // Opinion-derived ties are synthesized by the archive and have no
            // backing PawnRelationDef; without this the raw key would be shown.
            if (defName == SocialRelationFilter.FriendRelationKey)
            {
                return "PersonalChronicle.Relation.Friend".Translate().ToString();
            }
            if (defName == SocialRelationFilter.RivalRelationKey)
            {
                return "PersonalChronicle.Relation.Rival".Translate().ToString();
            }
            PawnRelationDef def = DefDatabase<PawnRelationDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            return defName;
        }

        private static int SocialNodeRank(SignificantRelation relation)
        {
            if (relation == null)
            {
                return 99;
            }
            if (!relation.IsActive)
            {
                return 50;
            }
            string defName = relation.RelationDefName;
            if (defName == SocialRelationFilter.FriendRelationKey)
            {
                return 20;
            }
            if (defName == SocialRelationFilter.RivalRelationKey)
            {
                return 30;
            }
            return 0;
        }

        private string FormatSocialEventTitle(string action, string relationDefName, ChronicleEvent ev, IArchiveService service)
        {
            string relLabel = RelationDefLabel(relationDefName);
            string other = string.Empty;
            if (ev != null && ev.Subjects != null)
            {
                for (int i = 0; i < ev.Subjects.Count; i++)
                {
                    ObjectRef s = ev.Subjects[i];
                    if (s != null && s.CategoryKey == ArchiveCategoryKeys.Pawn)
                    {
                        other = ResolveRefLabel(s, service);
                        break;
                    }
                }
            }
            if (action == ChronicleEventParams.RelationActionEnded)
            {
                return "PersonalChronicle.UI.SocialEndedTitle".Translate(relLabel, other).ToString();
            }
            return "PersonalChronicle.UI.SocialFormedTitle".Translate(relLabel, other).ToString();
        }

        private static IReadOnlyList<ReadModels.LegacyHolderView> SubList(
            IReadOnlyList<ReadModels.LegacyHolderView> list, int cap)
        {
            if (list == null || list.Count <= cap) return list;
            List<ReadModels.LegacyHolderView> sub = new List<ReadModels.LegacyHolderView>(cap);
            for (int i = 0; i < cap; i++) sub.Add(list[i]);
            return sub;
        }

        private static string WorkTypeLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            WorkTypeDef def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            if (def == null)
            {
                LogMissingDefOnce(defName);
            }
            return defName;
        }

        private static string BiomeLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            BiomeDef def = DefDatabase<BiomeDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            if (def == null)
            {
                LogMissingDefOnce(defName);
            }
            return defName;
        }

        private void DrawCapturePlaceholder(Rect rect)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                float y = rect.y;
                string featureName = TabLabel(CurrentTabKey());

                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(rect.x, y, rect.width, 28f), featureName);
                y += 36f;

                Rect box = new Rect(rect.x, y, rect.width, 110f);
                UIComponents.TintedBox(box, UITheme.OverlayWhite04);
                DrawBorder(box, UITheme.BorderSoft);

                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(box.x + 14f, box.y + 14f, box.width - 28f, 24f),
                    "PersonalChronicle.UI.NoCaptureYet".Translate().ToString());
                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.SecondaryText;
                Widgets.Label(new Rect(box.x + 14f, box.y + 46f, box.width - 28f, 48f),
                    "PersonalChronicle.UI.NoCaptureExplanation".Translate().ToString());
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private static void DrawNoService(Rect inRect)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, inRect.height / 2f - 14f, inRect.width, 28f),
                    "PersonalChronicle.UI.NoService".Translate().ToString());
            }
            finally
            {
                Text.Font = prevFont;
                GUI.color = prevColor;
            }
        }

        private static void DrawSectionTitle(Rect rect, ref float y, string title)
        {
            UIComponents.SectionTitle(rect, y, title);
            y += UITheme.SectionTitleHeight;
        }

        private static void DrawEventRow(Rect row, string dateText, string titleText, string typeText)
        {
            Color previous = GUI.color;
            GameFont prevFont = Text.Font;
            UIComponents.TintedBox(
                new Rect(row.x, row.y + 1f, 2f, Mathf.Max(1f, row.height - 2f)),
                UITheme.InfoSoft);

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Dim;
            Widgets.Label(new Rect(row.x + 8f, row.y + 4f, 146f, 18f), dateText);

            // 窄面板保护：固定列宽减法可能产生负宽（小屏/压缩窗口），clamp 到 0。
            Text.Font = GameFont.Small;
            GUI.color = ArchiveUiStyle.Text;
            float titleW = Mathf.Max(0f, row.width - 162f - 190f);
            Widgets.Label(new Rect(row.x + 162f, row.y + 4f, titleW, 20f), titleText);

            // 右对齐列：面板过窄时整体隐藏（避免与标题列重叠/越界）。
            if (row.width >= 186f + 160f)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(row.x + row.width - 186f, row.y + 4f, 182f, 18f), typeText);
            }
            GUI.color = previous;
            Text.Font = prevFont;

            ArchiveUiStyle.DrawRule(new Rect(row.x, row.yMax - 1f, row.width, 1f), ArchiveUiStyle.BorderSoft);
        }

        private static float DrawDetailRow(float x, float y, float width, string label, string value)
        {
            UIComponents.Label(new Rect(x, y, 150f, 20f), label, GameFont.Tiny, UITheme.SecondaryText);
            UIComponents.Label(new Rect(x + 156f, y, width - 156f, 22f), value, GameFont.Small, UITheme.Text);
            return y + 26f;
        }

        private static void DrawStatCell(Rect rect, string label, int value)
        {
            UIComponents.StatCell(rect, label, value.ToString());
        }

        private static void DrawStatCell(Rect rect, string label, int value, string subLabel)
        {
            UIComponents.StatCell(rect, label, value.ToString(), subLabel);
        }

        private static float DrawPill(float x, float y, string label, Color color)
        {
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            float width = Text.CalcSize(label).x + 18f;
            Text.Font = prevFont;
            Rect rect = new Rect(x, y, width, 20f);
            ArchiveUiStyle.DrawBadge(rect, label, color);
            return x + width + 6f;
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            ArchiveUiStyle.DrawBorder(rect, color);
        }


    }
}

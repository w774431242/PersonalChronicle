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
    /// <summary>
    /// Partial of <see cref="ArchiveMainTabWindow"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveMainTabWindow : MainTabWindow
    {

        private void DrawHeader(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x + 2f, rect.y + 9f, 300f, 32f),
                "PersonalChronicle.UI.ColonyArchive".Translate().ToString());

            // v1.1.4: embedded theme-switch button (top-right). Cycles the UI theme
            // and persists the choice through ChronicleSettings. Tooltip shows the
            // current theme name.
            // v4.17 体检：按钮宽按标签实测 + 截断（长主题名/长翻译不裁切，UI-009）。
            float btnH = 26f;
            string btnLabel = "PersonalChronicle.UI.Theme.Button".Translate()
                .ToString() + " · " + UITheme.GetDisplayName(UITheme.ActiveThemeId);
            GameFont prevFontMeasure = Text.Font;
            Text.Font = GameFont.Small;
            float btnW = Mathf.Clamp(Text.CalcSize(btnLabel).x + 20f, 110f, 180f);
            Text.Font = prevFontMeasure;
            Rect themeBtn = new Rect(rect.xMax - btnW - 10f, rect.y + 14f, btnW, btnH);
            string nextId = UITheme.NextThemeId(UITheme.ActiveThemeId);
            string btnTip = "PersonalChronicle.UI.Theme.ButtonTip".Translate(
                UITheme.GetDisplayName(UITheme.ActiveThemeId),
                UITheme.GetDisplayName(nextId)).ToString();
            if (Widgets.ButtonText(themeBtn, btnLabel))
            {
                UITheme.Apply(nextId);
                if (PersonalChronicleMod.Settings != null)
                {
                    PersonalChronicleMod.Settings.ThemeId = nextId;
                    PersonalChronicleMod.Settings.Write();
                }
            }
            TooltipHandler.TipRegion(themeBtn,
                "PersonalChronicle.UI.Theme.ButtonTip".Translate(
                    UITheme.GetDisplayName(UITheme.ActiveThemeId),
                    UITheme.GetDisplayName(nextId)).ToString());

            Text.Font = GameFont.Small;
            ArchiveUiStyle.DrawRule(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), ArchiveUiStyle.Accent);
        }

        private void DrawSidebar(Rect rect, IArchiveService service)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            ArchiveUiStyle.DrawPanel(rect);
            Rect inner = rect.ContractedBy(10f);

            const float itemHeight = 30f;
            const float itemGap = 3f;
            float y = inner.y;

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Dim;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f),
                "PersonalChronicle.UI.Navigation".Translate().ToString());
            GUI.color = prevColor;
            y += 20f;

            if (DrawSidebarItem(inner.x, y, inner.width, itemHeight,
                "PersonalChronicle.UI.HomeOverview".Translate().ToString(), null, view == MainView.Home))
            {
                GoHome();
            }
            y += itemHeight + itemGap;
            if (DrawSidebarItem(inner.x, y, inner.width, itemHeight,
                "PersonalChronicle.UI.AllArchives".Translate().ToString(), null,
                view == MainView.Overview && string.IsNullOrEmpty(overviewCategoryFilter)))
            {
                GoOverview(null);
            }
            y += itemHeight + itemGap + 8f;

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Dim;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f),
                "PersonalChronicle.UI.Categories".Translate().ToString());
            GUI.color = prevColor;
            y += 20f;

            y = DrawSidebarCategory(inner, y, itemHeight, itemGap, ArchiveCategoryKeys.Pawn, service);
            y = DrawSidebarCategory(inner, y, itemHeight, itemGap, ArchiveCategoryKeys.Thing, service);
            y = DrawSidebarCategory(inner, y, itemHeight, itemGap, ArchiveCategoryKeys.Battle, service);
            y = DrawSidebarCategory(inner, y, itemHeight, itemGap, ArchiveCategoryKeys.Location, service);
            y += 8f;

            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.Dim;
            Widgets.Label(new Rect(inner.x, y, inner.width, 18f),
                "PersonalChronicle.UI.Tools".Translate().ToString());
            GUI.color = prevColor;
            y += 20f;

            y = DrawSidebarTool(inner, y, itemHeight, itemGap, "PersonalChronicle.UI.Favorites");
            y = DrawSidebarTool(inner, y, itemHeight, itemGap, "PersonalChronicle.UI.Milestones");
            DrawSidebarTool(inner, y, itemHeight, itemGap, "PersonalChronicle.UI.Search");
            GUI.color = prevColor;
            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
        }

        private float DrawSidebarCategory(Rect inner, float y, float height, float gap, string categoryKey, IArchiveService service)
        {
            int count = 0;
            if (cachedCategoryObjects.TryGetValue(categoryKey, out List<ArchiveObject> objects))
            {
                count = objects.Count;
            }
            bool selected = view == MainView.Overview && overviewCategoryFilter == categoryKey;
            // P4-4: formatted via translation key (no hardcoded " (n)" glue).
            string label = "PersonalChronicle.UI.CategoryCountLabel"
                .Translate(CategoryLabel(categoryKey), count)
                .ToString();
            if (DrawSidebarItem(inner.x, y, inner.width, height, label, null, selected))
            {
                GoOverview(categoryKey);
            }
            return y + height + gap;
        }

        private float DrawSidebarTool(Rect inner, float y, float height, float gap, string labelKey)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            Rect item = new Rect(inner.x, y, inner.width, height);
            // v4.17 体检：占位工具无点击响应——去掉悬停高亮（避免暗示可点），
            // 仅保留 Tooltip 说明占位（原 DrawSelectedNavigation(false) 会产生悬停高亮）。
            GUI.color = ArchiveUiStyle.Dim;
            Text.Font = GameFont.Small;
            string label = UIComponents.TruncateToWidth(
                labelKey.Translate().ToString(), item.width - 20f, GameFont.Small);
            Widgets.Label(new Rect(item.x + 13f, item.y + 5f, item.width - 20f, UITheme.FontBodyLineHeight), label);
            GUI.color = prevColor;
            Text.Font = prevFont;
            TooltipHandler.TipRegion(item, "PersonalChronicle.UI.ToolsPlaceholder".Translate().ToString());
            return y + height + gap;
        }

        private static bool DrawSidebarItem(float x, float y, float width, float height, string label, string countText, bool selected)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            Rect rect = new Rect(x, y, width, height);
            ArchiveUiStyle.DrawSelectedNavigation(rect, selected);
            Text.Font = GameFont.Small;
            // v4.17 体检：文本矩形行高 ≥ FontBodyLineHeight（22f，CJK 安全），
            // 长分类名（含计数）截断加省略号，不再换行后溢出裁切。
            if (string.IsNullOrEmpty(countText))
            {
                string clipped = UIComponents.TruncateToWidth(label, rect.width - 20f, GameFont.Small);
                Widgets.Label(new Rect(rect.x + 13f, rect.y + 4f, rect.width - 20f, UITheme.FontBodyLineHeight), clipped);
            }
            else
            {
                string clipped = UIComponents.TruncateToWidth(label, rect.width - 60f, GameFont.Small);
                Widgets.Label(new Rect(rect.x + 13f, rect.y + 4f, rect.width - 60f, UITheme.FontBodyLineHeight), clipped);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(rect.x + rect.width - 48f, rect.y + 4f, 38f, UITheme.FontBodyLineHeight), countText);
            }
            Text.Anchor = prevAnchor;
            Text.Font = prevFont;
            GUI.color = prevColor;
            return Widgets.ButtonInvisible(rect);
        }

        private void DrawContent(Rect rect, IArchiveService service)
        {
            ArchiveUiStyle.DrawPanel(rect);
            Rect inner = rect.ContractedBy(ArchiveUiStyle.PanelPadding);

            switch (view)
            {
                case MainView.Home:
                    DrawHomeContent(inner, service);
                    break;
                case MainView.Overview:
                    DrawOverviewContent(inner, service);
                    break;
                case MainView.PawnDetail:
                case MainView.WeaponDetail:
                    DrawDetailContent(inner, service);
                    break;
                case MainView.EventDetail:
                    DrawEventContent(inner, service);
                    break;
            }
        }

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

        private void DrawOrthogonalLink(Vector2 center, Vector2 nodeCenter, Color color, float thickness)
        {
            ArchivePanelBase.DrawOrthogonalLink(center, nodeCenter, color, thickness, socialNetworkZoom);
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

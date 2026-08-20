using UnityEngine;
using RimWorld;
using Verse;
using UI = PersonalChronicle.Archive.UI;
using UIComponents = PersonalChronicle.Archive.UI.UIComponents;
using UITheme = PersonalChronicle.Archive.UI.UITheme;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// v4.5 compatibility shell over the UI Design System.
    ///
    /// All presentational primitives now live in <see cref="UITheme"/> (tokens)
    /// and <see cref="UIComponents"/> (components). This class forwards the
    /// historical <c>ArchiveUiStyle.*</c> surface to those layers so the ~120
    /// existing call sites in <c>ArchiveMainTabWindow</c> stay valid with zero
    /// behavioural change, while new code targets <c>UITheme</c>/<c>UIComponents</c>
    /// directly. Archive-specific concerns (faction codex kinds, selection
    /// navigation, card accent) remain here because they are not generic tokens.
    ///
    /// Since v1.1.4 the forwarded colour fields are <b>non-readonly</b>: they are
    /// re-synced from <see cref="UITheme"/> whenever a theme is applied
    /// (<see cref="RefreshFromTheme"/>). Spacing/metrics constants are theme-neutral
    /// and stay const.
    /// </summary>
    internal static class ArchiveUiStyle
    {
        // ---- Token forwards (single source of truth is UITheme) ----
        internal const float HeaderHeight = UITheme.HeaderHeight;
        internal const float SidebarWidth = UITheme.SidebarWidth;
        internal const float Gap = UITheme.Gap;
        internal const float PanelPadding = UITheme.PanelPadding;
        internal const float CardGap = UITheme.CardGap;

        internal static Color Window;
        internal static Color Panel;
        internal static Color PanelRaised;
        internal static Color Card;
        internal static Color CardHover;
        internal static Color Border;
        internal static Color BorderSoft;
        internal static Color Text;
        internal static Color Muted;
        internal static Color Dim;
        internal static Color Accent;
        internal static Color AccentSoft;
        internal static Color Info;
        internal static Color Alive;
        internal static Color Dead;
        internal static Color SecondaryText;
        internal static Color Warn;
        internal static Color Threat;
        internal static Color FactionAlly;
        internal static Color FactionHostile;
        internal static Color FactionNeutral;
        internal static Color TimelineSpine;
        internal static Color TimelineJoin;
        internal static Color TimelineDeath;
        internal static Color TimelineBattle;
        internal static Color TimelineSocial;
        internal static Color TimelineCraft;
        internal static Color TimelineBuilt;
        internal static Color TimelineOther;
        // v4.17 体检：补全 UITheme 其余 token 的转发（旧兼容壳漏转，新增 token 时静默漂移）。
        internal static Color Blood;
        internal static Color PillBlue;
        internal static Color PillGold;
        internal static Color PillRed;
        internal static Color PillGreen;
        internal static Color OverlayWhite04;
        internal static Color InfoSoft;
        internal static Color BadgeIncompleteFill;
        internal static Color Shadow;

        /// <summary>
        /// Re-sync every forwarded colour from the active <see cref="UITheme"/>
        /// palette. Called by <c>UITheme.Apply</c>; do not call directly.
        /// </summary>
        internal static void RefreshFromTheme()
        {
            Window = UITheme.Window;
            Panel = UITheme.Panel;
            PanelRaised = UITheme.PanelRaised;
            Card = UITheme.Card;
            CardHover = UITheme.CardHover;
            Border = UITheme.Border;
            BorderSoft = UITheme.BorderSoft;
            Text = UITheme.Text;
            Muted = UITheme.Muted;
            Dim = UITheme.Dim;
            Accent = UITheme.Accent;
            AccentSoft = UITheme.AccentSoft;
            Info = UITheme.Info;
            Alive = UITheme.Alive;
            Dead = UITheme.Dead;
            SecondaryText = UITheme.SecondaryText;
            Warn = UITheme.Warn;
            Threat = UITheme.Threat;
            FactionAlly = UITheme.FactionAlly;
            FactionHostile = UITheme.FactionHostile;
            FactionNeutral = UITheme.FactionNeutral;
            TimelineSpine = UITheme.TimelineSpine;
            TimelineJoin = UITheme.TimelineJoin;
            TimelineDeath = UITheme.TimelineDeath;
            TimelineBattle = UITheme.TimelineBattle;
            TimelineSocial = UITheme.TimelineSocial;
            TimelineCraft = UITheme.TimelineCraft;
            TimelineBuilt = UITheme.TimelineBuilt;
            TimelineOther = UITheme.TimelineOther;
            Blood = UITheme.Blood;
            PillBlue = UITheme.PillBlue;
            PillGold = UITheme.PillGold;
            PillRed = UITheme.PillRed;
            PillGreen = UITheme.PillGreen;
            OverlayWhite04 = UITheme.OverlayWhite04;
            InfoSoft = UITheme.InfoSoft;
            BadgeIncompleteFill = UITheme.BadgeIncompleteFill;
            Shadow = UITheme.Shadow;
        }

        // ---- Faction-codex specific (not generic tokens) ----
        internal static Color FactionEnemy => FactionHostile;
        internal static Color FactionMechanoid => Info;
        internal static Color FactionAnimal => Alive;
        internal static Color FactionUnknown => Muted;
        internal static Color FactionPlayer => Accent;

        public enum FactionCodexKind { Enemy, Mechanoid, Animal, Unknown, Player }

        internal static Color FactionAccent(FactionCodexKind kind)
        {
            switch (kind)
            {
                case FactionCodexKind.Enemy: return FactionEnemy;
                case FactionCodexKind.Mechanoid: return FactionMechanoid;
                case FactionCodexKind.Animal: return FactionAnimal;
                case FactionCodexKind.Player: return FactionPlayer;
                default: return FactionUnknown;
            }
        }

        // ---- Drawing forwards (delegated to UIComponents) ----
        internal static void DrawPanel(Rect rect) => UIComponents.Panel(rect);
        internal static void DrawPanel(Rect rect, Color fill) => UIComponents.Panel(rect, fill);
        internal static void DrawCard(Rect rect, Color accent) => UIComponents.Card(rect, accent);
        internal static void DrawRule(Rect rect, Color color) => UIComponents.Rule(rect, color);
        internal static void DrawBorder(Rect rect, Color color) => UIComponents.Border(rect, color);
        internal static void DrawBadge(Rect rect, string label, Color color) => UIComponents.Badge(rect, label, color);

        internal static void DrawSectionMarker(Rect rect) => DrawSectionMarker(rect, Accent);
        internal static void DrawSectionMarker(Rect rect, Color color)
        {
            Color previous = GUI.color;
            try
            {
                GUI.color = color;
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 4f, rect.height), color);
            }
            finally
            {
                GUI.color = previous;
            }
        }

        internal static void DrawSelectedNavigation(Rect rect, bool selected)
        {
            if (selected)
            {
                Widgets.DrawBoxSolid(rect, AccentSoft);
                DrawSectionMarker(new Rect(rect.x, rect.y, 4f, rect.height));
            }
            else
            {
                Widgets.DrawHighlightIfMouseover(rect);
            }
        }
    }
}

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
    /// </summary>
    internal static class ArchiveUiStyle
    {
        // ---- Token forwards (single source of truth is UITheme) ----
        internal const float HeaderHeight = UITheme.HeaderHeight;
        internal const float SidebarWidth = UITheme.SidebarWidth;
        internal const float Gap = UITheme.Gap;
        internal const float PanelPadding = UITheme.PanelPadding;
        internal const float CardGap = UITheme.CardGap;

        internal static readonly Color Window = UITheme.Window;
        internal static readonly Color Panel = UITheme.Panel;
        internal static readonly Color PanelRaised = UITheme.PanelRaised;
        internal static readonly Color Card = UITheme.Card;
        internal static readonly Color CardHover = UITheme.CardHover;
        internal static readonly Color Border = UITheme.Border;
        internal static readonly Color BorderSoft = UITheme.BorderSoft;
        internal static readonly Color Text = UITheme.Text;
        internal static readonly Color Muted = UITheme.Muted;
        internal static readonly Color Dim = UITheme.Dim;
        internal static readonly Color Accent = UITheme.Accent;
        internal static readonly Color AccentSoft = UITheme.AccentSoft;
        internal static readonly Color Info = UITheme.Info;
        internal static readonly Color Alive = UITheme.Alive;
        internal static readonly Color Dead = UITheme.Dead;
        internal static readonly Color SecondaryText = UITheme.SecondaryText;
        internal static readonly Color Warn = UITheme.Warn;
        internal static readonly Color Threat = UITheme.Threat;
        internal static readonly Color FactionAlly = UITheme.FactionAlly;
        internal static readonly Color FactionHostile = UITheme.FactionHostile;
        internal static readonly Color FactionNeutral = UITheme.FactionNeutral;
        internal static readonly Color TimelineSpine = UITheme.TimelineSpine;
        internal static readonly Color TimelineJoin = UITheme.TimelineJoin;
        internal static readonly Color TimelineDeath = UITheme.TimelineDeath;
        internal static readonly Color TimelineBattle = UITheme.TimelineBattle;
        internal static readonly Color TimelineSocial = UITheme.TimelineSocial;
        internal static readonly Color TimelineCraft = UITheme.TimelineCraft;
        internal static readonly Color TimelineBuilt = UITheme.TimelineBuilt;
        internal static readonly Color TimelineOther = UITheme.TimelineOther;

        // ---- Faction-codex specific (not generic tokens) ----
        // Mapped to native ColoredText faction relations (v4.5.3 calibration).
        internal static readonly Color FactionEnemy = UITheme.FactionHostile;
        internal static readonly Color FactionMechanoid = UITheme.Info;
        internal static readonly Color FactionAnimal = UITheme.Alive;
        internal static readonly Color FactionUnknown = UITheme.Muted;
        internal static readonly Color FactionPlayer = UITheme.Accent;

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
            GUI.color = color;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 4f, rect.height), color);
            GUI.color = previous;
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

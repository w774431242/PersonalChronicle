using UnityEngine;
using RimWorld;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// v4.0 archive UI primitives. This class owns presentation constants and
    /// drawing only; it never reads game state or mutates archive data.
    /// </summary>
    internal static class ArchiveUiStyle
    {
        internal const float HeaderHeight = 56f;
        internal const float SidebarWidth = 208f;
        internal const float Gap = 12f;
        internal const float PanelPadding = 12f;
        internal const float CardGap = 8f;

        internal static readonly Color Window = new Color(0.067f, 0.094f, 0.110f, 1f);
        internal static readonly Color Panel = new Color(0.094f, 0.129f, 0.149f, 1f);
        internal static readonly Color PanelRaised = new Color(0.125f, 0.165f, 0.188f, 1f);
        internal static readonly Color Card = new Color(0.145f, 0.188f, 0.212f, 1f);
        internal static readonly Color CardHover = new Color(0.190f, 0.235f, 0.263f, 1f);
        internal static readonly Color Border = new Color(0.270f, 0.325f, 0.357f, 1f);
        internal static readonly Color BorderSoft = new Color(0.180f, 0.231f, 0.255f, 1f);
        internal static readonly Color Text = new Color(0.914f, 0.933f, 0.929f, 1f);
        internal static readonly Color Muted = new Color(0.647f, 0.690f, 0.706f, 1f);
        internal static readonly Color Dim = new Color(0.443f, 0.502f, 0.525f, 1f);
        internal static readonly Color Accent = new Color(0.827f, 0.643f, 0.357f, 1f);
        internal static readonly Color AccentSoft = new Color(0.827f, 0.643f, 0.357f, 0.15f);
        internal static readonly Color Info = new Color(0.475f, 0.678f, 0.761f, 1f);
        internal static readonly Color Alive = new Color(0.412f, 0.788f, 0.557f, 1f);
        internal static readonly Color Dead = new Color(0.859f, 0.467f, 0.471f, 1f);

        internal static void DrawPanel(Rect rect)
        {
            DrawPanel(rect, Panel);
        }

        internal static void DrawPanel(Rect rect, Color fill)
        {
            Widgets.DrawBoxSolid(rect, fill);
            DrawBorder(rect, BorderSoft);
        }

        internal static void DrawCard(Rect rect, Color accent)
        {
            Widgets.DrawBoxSolid(rect, Card);
            DrawBorder(rect, BorderSoft);
            Color previous = GUI.color;
            GUI.color = accent;
            Widgets.DrawLineVertical(rect.x, rect.y, rect.height);
            Widgets.DrawLineVertical(rect.x + 1f, rect.y, rect.height);
            GUI.color = previous;
        }

        internal static void DrawSectionMarker(Rect rect)
        {
            DrawSectionMarker(rect, Accent);
        }

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

        internal static void DrawRule(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
            GUI.color = previous;
        }

        internal static void DrawBorder(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
            Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);
            Widgets.DrawLineVertical(rect.x, rect.y, rect.height);
            Widgets.DrawLineVertical(rect.xMax - 1f, rect.y, rect.height);
            GUI.color = previous;
        }

        internal static void DrawBadge(Rect rect, string label, Color color)
        {
            Color previous = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, 0.14f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            DrawBorder(rect, color);
            GUI.color = color;
            Verse.Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Verse.Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = previous;
        }
    }
}

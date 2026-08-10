using UnityEngine;
using RimWorld;
using Verse;

namespace PersonalChronicle.Archive.UI
{
    /// <summary>
    /// v4.5 UI Design System — Layer 2: reusable components.
    ///
    /// Every method draws with <see cref="UITheme"/> tokens only and always
    /// restores <c>GUI.color</c> / <c>Text.Font</c> / <c>Text.Anchor</c> to its
    /// prior state before returning. Windows must call these instead of
    /// hand-rolling <c>GUI.color = ...; Widgets.DrawBox(...)</c> blocks. This is
    /// the single convergence point for all presentational drawing.
    /// </summary>
    internal static class UIComponents
    {
        // ---- Low-level pairing guard: draw a label with a temporary color+font,
        // then restore both. This is the primitive that removes scattered
        // GUI.color / Text.Font assignments across the window. ----
        internal static void Label(Rect rect, string text, GameFont font, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Verse.Text.Anchor;
            GUI.color = color;
            Verse.Text.Font = font;
            Verse.Text.Anchor = anchor;
            Widgets.Label(rect, text);
            GUI.color = prevColor;
            Verse.Text.Font = prevFont;
            Verse.Text.Anchor = prevAnchor;
        }

        // ---- Panel: filled surface + soft border ----
        internal static void Panel(Rect rect, Color fill = default)
        {
            Color c = fill == default ? UITheme.Panel : fill;
            Widgets.DrawBoxSolid(rect, c);
            Border(rect, UITheme.BorderSoft);
        }

        // ---- Card: raised surface + left accent stripe + soft border ----
        internal static void Card(Rect rect, Color accent)
        {
            Widgets.DrawBoxSolid(rect, UITheme.Card);
            Border(rect, UITheme.BorderSoft);
            Color prev = GUI.color;
            GUI.color = accent;
            Widgets.DrawLineVertical(rect.x, rect.y, rect.height);
            Widgets.DrawLineVertical(rect.x + 1f, rect.y, rect.height);
            GUI.color = prev;
        }

        // ---- Section title: accent marker + label + hairline rule (matches the
        // example's left-border section headers). ----
        internal static void SectionTitle(Rect rect, float y, string title)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            Verse.Text.Font = GameFont.Small;
            // Title block width derived from real text extent (CJK-safe); the rule
            // starts after it instead of a fixed 230px offset.
            float titleW = Mathf.Min(240f, Verse.Text.CalcSize(title).x + 8f);
            GUI.color = UITheme.Accent;
            Widgets.DrawBoxSolid(new Rect(rect.x, y + 4f, 4f, 14f), UITheme.Accent);
            GUI.color = prevColor;
            Widgets.Label(new Rect(rect.x + 10f, y, titleW, 22f), title);
            float ruleX = rect.x + 10f + titleW + 4f;
            Rule(new Rect(ruleX, y + 11f, Mathf.Max(0f, rect.width - (ruleX - rect.x)), UITheme.RuleHeight),
                UITheme.Border);
            Verse.Text.Font = prevFont;
        }

        // ---- Hairline divider between rows/sections ----
        internal static void Rule(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
            GUI.color = prev;
        }

        // ---- Border: 1px frame on all four sides ----
        internal static void Border(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
            Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);
            Widgets.DrawLineVertical(rect.x, rect.y, rect.height);
            Widgets.DrawLineVertical(rect.xMax - 1f, rect.y, rect.height);
            GUI.color = prev;
        }

        // ---- Pill: status chip (alive/dead/role) ----
        internal static void Pill(Rect rect, string label, Color color)
        {
            Color prev = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            GUI.color = new Color(color.r, color.g, color.b, 0.16f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            Border(rect, color);
            GUI.color = color;
            Verse.Text.Font = GameFont.Tiny;
            Verse.Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Verse.Text.Anchor = TextAnchor.UpperLeft;
            Verse.Text.Font = prevFont;
            GUI.color = prev;
        }

        // ---- Badge: dense tag (matches ArchiveUiStyle.DrawBadge) ----
        internal static void Badge(Rect rect, string label, Color color)
        {
            Color prev = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            Verse.Text.Font = GameFont.Tiny;
            GUI.color = new Color(color.r, color.g, color.b, 0.14f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            Border(rect, color);
            GUI.color = color;
            Verse.Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Verse.Text.Anchor = TextAnchor.UpperLeft;
            Verse.Text.Font = prevFont;
            GUI.color = prev;
        }

        // ---- TintedBox: white box under a translucent tint (placeholder /
        // empty-state backgrounds). Keeps the previous GUI.color untouched. ----
        internal static void TintedBox(Rect rect, Color tint)
        {
            Color prev = GUI.color;
            GUI.color = tint;
            Widgets.DrawBoxSolid(rect, Color.white);
            GUI.color = prev;
        }

        // ---- ProgressBar: track + fill by 0..1 share (career bars, intensity) ----
        internal static void ProgressBar(Rect rect, float share01, Color fill)
        {
            Widgets.DrawBoxSolid(rect, UITheme.BorderSoft);
            float fillW = Mathf.Clamp01(share01) * rect.width;
            if (fillW > 0f)
            {
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, fillW, rect.height), fill);
            }
        }

        /// <summary>ProgressBar variant that draws a centered caption above the fill.</summary>
        internal static void ProgressBar(Rect rect, float share01, Color fill, string caption)
        {
            ProgressBar(rect, share01, fill);
            if (!string.IsNullOrEmpty(caption))
            {
                Color prev = GUI.color;
                GameFont prevFont = Verse.Text.Font;
                Verse.Text.Font = GameFont.Tiny;
                GUI.color = UITheme.Text;
                Widgets.Label(new Rect(rect.x + 4f, rect.y, rect.width - 8f, rect.height),
                    caption);
                Verse.Text.Font = prevFont;
                GUI.color = prev;
            }
        }

        // ---- StatCell: KPI cell (label over value, optional sub-label).
        // Layout uses real font line-heights (Text.LineHeight) so Chinese glyphs
        // never overflow the card. Minimum internal content height = 64f. ----
        /// <summary>
        /// StatCell: KPI cell. When inlineSubLabel is true the value and sub-label
        /// render on the same line ("223 银（归我）") matching the design spec.
        /// </summary>
        internal static void StatCell(Rect rect, string label, string value, string subLabel = null, bool inlineSubLabel = false)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            Widgets.DrawBoxSolid(rect, UITheme.PanelRaised);
            Border(rect, UITheme.BorderSoft);

            float padX = 8f;
            float padY = 6f;
            float gap = 4f;
            float innerW = rect.width - padX * 2f;

            Verse.Text.Font = GameFont.Tiny;
            float labelH = Verse.Text.LineHeight;
            GUI.color = UITheme.Muted;
            Widgets.Label(new Rect(rect.x + padX, rect.y + padY, innerW, labelH), label);

            float valueY = rect.y + padY + labelH + gap;
            Verse.Text.Font = GameFont.Medium;
            float valueH = Verse.Text.LineHeight;
            GUI.color = UITheme.Text;
            Rect valueRect = new Rect(rect.x + padX, valueY, innerW, valueH);
            Widgets.Label(valueRect, value);
            // v4.6.7: measure value width with the SAME font it was drawn (Medium).
            // Previously Text.CalcSize was called after switching to Tiny, which made
            // the unit start too far left and appear stuck to the digits.
            Vector2 valueSizeMedium = Text.CalcSize(value);

            if (!string.IsNullOrEmpty(subLabel))
            {
                Verse.Text.Font = GameFont.Tiny;
                float subH = Verse.Text.LineHeight;
                GUI.color = UITheme.Dim;
                if (inlineSubLabel)
                {
                    // Dynamic value↔unit gap: unit tracks the real Medium value width
                    // with a breathing gap; clip to the cell so long units never overflow.
                    Vector2 subSize = Text.CalcSize(subLabel);
                    float subGap = 8f;
                    float valueW = Mathf.Min(valueSizeMedium.x, innerW);
                    float subX = valueRect.x + valueW + subGap;
                    float subMaxX = rect.xMax - padX;
                    float subW = Mathf.Min(subSize.x, subMaxX - subX);
                    if (subW > 0f)
                    {
                        Widgets.Label(new Rect(subX, valueY + (valueH - subH) / 2f, subW, subH), subLabel);
                    }
                }
                else
                {
                    float subY = valueY + valueH + gap;
                    Widgets.Label(new Rect(rect.x + padX, subY, innerW, subH), subLabel);
                }
            }
            GUI.color = prevColor;
            Verse.Text.Font = prevFont;
        }

        /// <summary>StatCell variant that tints the value text (e.g. blood-red when impaired).</summary>
        internal static void StatCell(Rect rect, string label, string value, Color valueColor, string subLabel = null, bool inlineSubLabel = false)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            Widgets.DrawBoxSolid(rect, UITheme.PanelRaised);
            Border(rect, UITheme.BorderSoft);

            float padX = 8f;
            float padY = 6f;
            float gap = 4f;
            float innerW = rect.width - padX * 2f;

            Verse.Text.Font = GameFont.Tiny;
            float labelH = Verse.Text.LineHeight;
            GUI.color = UITheme.Muted;
            Widgets.Label(new Rect(rect.x + padX, rect.y + padY, innerW, labelH), label);

            float valueY = rect.y + padY + labelH + gap;
            Verse.Text.Font = GameFont.Medium;
            float valueH = Verse.Text.LineHeight;
            GUI.color = valueColor;
            Rect valueRect = new Rect(rect.x + padX, valueY, innerW, valueH);
            Widgets.Label(valueRect, value);
            // v4.6.7: measure value width with the SAME font it was drawn (Medium).
            Vector2 valueSizeMedium = Text.CalcSize(value);

            if (!string.IsNullOrEmpty(subLabel))
            {
                Verse.Text.Font = GameFont.Tiny;
                float subH = Verse.Text.LineHeight;
                GUI.color = UITheme.Dim;
                if (inlineSubLabel)
                {
                    // Dynamic value↔unit gap (mirrors the standard overload).
                    Vector2 subSize = Text.CalcSize(subLabel);
                    float subGap = 8f;
                    float valueW = Mathf.Min(valueSizeMedium.x, innerW);
                    float subX = valueRect.x + valueW + subGap;
                    float subMaxX = rect.xMax - padX;
                    float subW = Mathf.Min(subSize.x, subMaxX - subX);
                    if (subW > 0f)
                    {
                        Widgets.Label(new Rect(subX, valueY + (valueH - subH) / 2f, subW, subH), subLabel);
                    }
                }
                else
                {
                    float subY = valueY + valueH + gap;
                    Widgets.Label(new Rect(rect.x + padX, subY, innerW, subH), subLabel);
                }
            }
            GUI.color = prevColor;
            Verse.Text.Font = prevFont;
        }

        /// <summary>Subsection header (small accent rule + localized title key).</summary>
        internal static void DrawSubsectionHeader(Rect rect, string key)
        {
            Color prev = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            Rect rule = new Rect(rect.x, rect.y + 4f, 3f, rect.height - 8f);
            Widgets.DrawBoxSolid(rule, UITheme.Accent);
            Verse.Text.Font = GameFont.Small;
            GUI.color = UITheme.Text;
            Widgets.Label(new Rect(rect.x + 10f, rect.y, rect.width - 10f, rect.height), key.Translate().ToString());
            GUI.color = prev;
            Verse.Text.Font = prevFont;
        }

        /// <summary>Minimum card height StatCell needs to fit label+value+subLabel.</summary>
        internal const float StatCellMinHeight = 64f;

        // ---- TimelineNode: one node on a lifecycle/spine timeline. Height is
        // computed from real font line-heights (Text.LineHeight / CalcHeight) so
        // long Chinese titles/sub-text wrap instead of overlapping. ----
        internal static float TimelineNode(Rect rect, float y, string title, string dateText,
            string subText, Color dotColor, out float nodeHeight, string icon = null)
        {
            const float nodeSize = 18f;
            float textX = rect.x + nodeSize + 6f;
            float textW = rect.width - (nodeSize + 10f);
            float gap = UITheme.SpaceXxs;

            Verse.Text.Font = GameFont.Small;
            float titleH = Verse.Text.CalcHeight(title, textW);
            float dateH = 0f, subH = 0f;
            if (!string.IsNullOrEmpty(dateText))
            {
                Verse.Text.Font = GameFont.Tiny;
                dateH = Verse.Text.CalcHeight(dateText, textW) + gap;
            }
            if (!string.IsNullOrEmpty(subText))
            {
                Verse.Text.Font = GameFont.Tiny;
                subH = Verse.Text.CalcHeight(subText, textW) + gap;
            }
            // Reserve room for the node at the top; node height = max(node, text block).
            float textBlock = titleH + dateH + subH;
            float h = Mathf.Max(nodeSize, textBlock) + UITheme.SpaceXs;
            nodeHeight = h;

            Rect nodeRect = new Rect(rect.x, y + 2f, nodeSize, nodeSize);
            Color prev = GUI.color;
            GUI.color = dotColor;
            Widgets.DrawBoxSolid(nodeRect, dotColor);
            GUI.color = UITheme.Border;
            Widgets.DrawBox(nodeRect);
            GUI.color = prev;

            if (!string.IsNullOrEmpty(icon))
            {
                Label(new Rect(nodeRect), icon, GameFont.Small, Color.white, TextAnchor.MiddleCenter);
            }

            float lineY = y;
            Label(new Rect(textX, lineY, textW, titleH), title, GameFont.Small, UITheme.Text);
            lineY += titleH + gap;
            if (!string.IsNullOrEmpty(dateText))
            {
                Label(new Rect(textX, lineY, textW, dateH), dateText, GameFont.Tiny, UITheme.SecondaryText);
                lineY += dateH;
            }
            if (!string.IsNullOrEmpty(subText))
            {
                Label(new Rect(textX, lineY, textW, subH), subText, GameFont.Tiny, UITheme.SecondaryText);
            }
            return y + h;
        }
    }
}

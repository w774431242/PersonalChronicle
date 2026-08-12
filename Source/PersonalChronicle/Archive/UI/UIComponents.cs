using System.Collections.Generic;
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

        /// <summary>One-line label with a dark drop shadow so it stays readable over
        /// bright progress-bar fills. The shadow is drawn slightly offset and then
        /// the requested foreground color on top.</summary>
        internal static void ShadowLabel(Rect rect, string text, GameFont font, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Verse.Text.Anchor;
            bool prevWrap = Verse.Text.WordWrap;
            Verse.Text.Font = font;
            Verse.Text.Anchor = anchor;
            Verse.Text.WordWrap = false;

            string clipped = TruncateToWidth(text ?? "", rect.width, font);

            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            Widgets.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), clipped);
            GUI.color = color;
            Widgets.Label(rect, clipped);

            GUI.color = prevColor;
            Verse.Text.Font = prevFont;
            Verse.Text.Anchor = prevAnchor;
            Verse.Text.WordWrap = prevWrap;
        }

        /// <summary>Truncates text with "…" so it is guaranteed not to exceed the
        /// given width in the given font. Returns original text if it already fits.</summary>
        internal static string TruncateToWidth(string text, float maxWidth, GameFont font)
        {
            if (string.IsNullOrEmpty(text)) return text;
            GameFont prev = Verse.Text.Font;
            Verse.Text.Font = font;
            try
            {
                Vector2 size = Text.CalcSize(text);
                if (size.x <= maxWidth) return text;

                // Prefer removing from the middle so both ends stay readable.
                int len = text.Length;
                for (int i = 0; i < len; i++)
                {
                    int rightKeep = i / 2;
                    int leftKeep = i - rightKeep;
                    string candidate;
                    if (leftKeep + rightKeep >= len)
                    {
                        candidate = text;
                    }
                    else
                    {
                        candidate = text.Substring(0, leftKeep) + "…" +
                            text.Substring(len - rightKeep, rightKeep);
                    }
                    Vector2 cs = Text.CalcSize(candidate);
                    if (cs.x <= maxWidth) return candidate;
                }
                return "…";
            }
            finally
            {
                Verse.Text.Font = prev;
            }
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

        // ---- v4.15 condense tab: six-cell KPI grid (3x2) ------------------------
        // Pure presentation — every value/tone is supplied by the caller (the ITab
        // reads them from DetailSnapshot). No aggregation, no Read-Model logic here.
        internal struct KpiCell
        {
#pragma warning disable CS0649 // optional fields validly left unset by callers
            public string KindKey;   // "work"/"prod"/"kill"/"battle"/"foot"/"legacy"
            public string TitleKey;  // translation key for the cell title
            public string Value;     // big number / primary text (e.g. weapon name or count)
            public string Unit;      // inline unit after the value (e.g. "h/天", "件", "银") — may be empty
            public string InlineMetric; // right-aligned metric on the SAME row as Value (e.g. "第3/12") — may be empty
            public string Sub;       // one-line meta (may be empty)
            public KpiRow[] Rows;    // compact label/value strip below the value (KPI 条) — may be null
            public KpiBar[] Bars;    // progress-bar list (结构/贡献) — may be null
            public KpiChain[] Chain; // legacy chain rows (传承链) — may be null
            public string[] Badges;  // compact tag group (e.g. production categories) — may be null
#pragma warning restore CS0649
        }

        /// <summary>One label/value row in a cell's KPI strip.</summary>
        internal struct KpiRow
        {
            public string Label;     // localized left label (e.g. "周工时")
            public string Value;     // right value (e.g. "42 h")
        }

        /// <summary>One progress bar: caption + 0..1 share + optional tag (主业/副业).</summary>
        internal struct KpiBar
        {
            public string Caption;   // bar caption (e.g. "种植 · 469h")
            public float Share01;    // 0..1 fill ratio
            public string Tag;       // optional trailing tag (e.g. "主业") — may be empty
        }

        /// <summary>One legacy-chain row: holder label + kill count.</summary>
        internal struct KpiChain
        {
            public string Label;     // holder label (e.g. "戈治钟 · 初代")
            public string Value;     // kill count text (e.g. "18 杀")
        }

        internal static readonly float KpiCardH = 200f;
        internal static readonly float KpiGap = 10f;
        // Badge group metrics (preview E1): height 16, padding 6, wrap with 4f line gap.
        internal static readonly float KpiBadgeH = 16f;
        internal static readonly float KpiBadgePadX = 6f;
        internal static readonly float KpiBadgeGap = 4f;

        /// <summary>
        /// v4.15 condense-tab six-cell (3×N) KPI grid. When the cell count exceeds
        /// the rows that fit in <paramref name="rect"/>, the grid scrolls vertically
        /// via <paramref name="scroll"/> so the digest never crowds the fixed-height
        /// inspect tab. Pure presentation — all values/tones supplied by the caller.
        /// </summary>
        internal static void SixGrid(Rect rect, KpiCell[] cells, ref Vector2 scroll)
        {
            if (cells == null || cells.Length == 0) return;
            int cols = 3;
            float cardW = (rect.width - KpiGap * (cols - 1)) / cols;
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            try
            {
                int rows = (cells.Length + cols - 1) / cols;
                float contentH = rows * (KpiCardH + KpiGap);
                Rect inner = new Rect(0f, 0f, rect.width - UITheme.ScrollbarThickness, Mathf.Max(contentH, rect.height));

                Widgets.BeginScrollView(rect, ref scroll, inner);
                for (int i = 0; i < cells.Length; i++)
                {
                    int col = i % cols;
                    int row = i / cols;
                    float x = inner.x + col * (cardW + KpiGap);
                    float y = inner.y + row * (KpiCardH + KpiGap);
                    Rect card = new Rect(x, y, cardW, KpiCardH);
                    KpiCell c = cells[i];
                    Color tone = UITheme.TintForEventKind(c.KindKey);

                    // card body (token tint, dimmed) + left accent bar
                    Widgets.DrawBoxSolid(card, UITheme.Panel);
                    Rect bar = new Rect(card.x, card.y, 3f, card.height);
                    Widgets.DrawBoxSolid(bar, tone);
                    Border(card, UITheme.Border);

                    // Clip everything to the card so long captions never bleed into
                    // neighbouring cells.
                    GUI.BeginGroup(card);

                    // hover highlight (mouse-over only)
                    Widgets.DrawHighlightIfMouseover(new Rect(0f, 0f, card.width, card.height));

                    float pad = 12f;
                    Rect titleR = new Rect(pad, 8f, card.width - pad * 2, 18f);
                    Label(titleR, c.TitleKey.Translate().ToString(), GameFont.Tiny, UITheme.SecondaryText);

                    // Big value (Medium) + optional inline unit + optional同行 right-aligned metric.
                    Rect valR = new Rect(pad, 26f, card.width - pad * 2, 36f);
                    Rect valueTextR = valR;
                    bool valueEmpty = (c.Value ?? "") == "--";
                    Color valueColor = valueEmpty ? UITheme.Muted : tone;
                    if (!string.IsNullOrEmpty(c.InlineMetric))
                    {
                        // Reserve right portion for the同行 metric; shrink value width.
                        Verse.Text.Font = GameFont.Tiny;
                        Vector2 mSize = Text.CalcSize(c.InlineMetric);
                        Verse.Text.Font = prevFont;
                        float mW = Mathf.Clamp(mSize.x + 6f, 0f, valR.width * 0.55f);
                        float mH = 18f;
                        Rect metricR = new Rect(valR.xMax - mW, valR.y + (valR.height - mH) * 0.5f, mW, mH);
                        Label(metricR, c.InlineMetric, GameFont.Tiny, valueEmpty ? UITheme.Dim : UITheme.Muted, TextAnchor.MiddleRight);
                        valueTextR = new Rect(valR.x, valR.y, valR.width - mW - 4f, valR.height);
                    }
                    if (string.IsNullOrEmpty(c.Unit))
                    {
                        Label(valueTextR, c.Value, GameFont.Medium, valueColor);
                    }
                    else
                    {
                        Verse.Text.Font = GameFont.Medium;
                        Vector2 valSize = Text.CalcSize(c.Value);
                        Verse.Text.Font = prevFont;
                        Label(valueTextR, c.Value, GameFont.Medium, valueColor);
                        Rect unitR = new Rect(valueTextR.x + Mathf.Min(valSize.x, valueTextR.width) + 4f,
                            valueTextR.y + (valueTextR.height - 18f) * 0.5f, valueTextR.width, 18f);
                        Label(unitR, c.Unit, GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                    }

                    // ---- Structured sub-content (mirrors preview v6.6 six-grid) ----
                    float cy = 72f; // below the big value row
                    float contentW = card.width - pad * 2;

                    // KPI strip rows (label left / value right).
                    if (c.Rows != null)
                    {
                        foreach (KpiRow kr in c.Rows)
                        {
                            if (kr.Label == null && kr.Value == null) continue;
                            Rect rR = new Rect(pad, cy, contentW, 18f);
                            Label(new Rect(rR.x, rR.y, rR.width * 0.55f, rR.height), kr.Label ?? "", GameFont.Tiny, UITheme.Dim);
                            Label(new Rect(rR.x + rR.width * 0.55f, rR.y, rR.width * 0.45f, rR.height), kr.Value ?? "", GameFont.Tiny, UITheme.Text, TextAnchor.UpperRight);
                            cy += 18f;
                        }
                    }

                    // Section divider before bars / chain.
                    bool hasBars = c.Bars != null && c.Bars.Length > 0;
                    bool hasChain = c.Chain != null && c.Chain.Length > 0;
                    if (hasBars || hasChain)
                    {
                        cy += 2f;
                        Widgets.DrawLineHorizontal(pad, cy, contentW, UITheme.BorderSoft);
                        cy += 4f;
                    }

                    // Progress bars (结构 / 贡献 / 战损 / 传承链 share).
                    if (hasBars)
                    {
                        foreach (KpiBar kb in c.Bars)
                        {
                            if (cy + 30f > card.height - 4f) break;

                            // Caption row above the bar so long text never overlaps the fill.
                            string tag = kb.Tag ?? "";
                            float tagW = 0f;
                            if (!string.IsNullOrEmpty(tag))
                            {
                                Verse.Text.Font = GameFont.Tiny;
                                Vector2 tagSize = Text.CalcSize(tag);
                                Verse.Text.Font = prevFont;
                                tagW = Mathf.Clamp(tagSize.x + 6f, 0f, contentW * 0.45f);
                            }
                            float capW = contentW - tagW - (tagW > 0f ? 4f : 0f);
                            Rect capR = new Rect(pad, cy, capW, 14f);
                            ShadowLabel(capR, kb.Caption ?? "", GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
                            if (tagW > 0f)
                            {
                                string tagClipped = TruncateToWidth(tag, tagW - 4f, GameFont.Tiny);
                                Rect tagR = new Rect(pad + contentW - tagW, cy, tagW, 14f);
                                ShadowLabel(tagR, tagClipped, GameFont.Tiny, UITheme.SecondaryText, TextAnchor.MiddleRight);
                            }
                            cy += 14f;

                            // Bar: dark track + soft border + tone fill (inset by 1px).
                            Rect barR = new Rect(pad, cy, contentW, 12f);
                            Widgets.DrawBoxSolid(barR, UITheme.BorderHair);
                            Border(barR, UITheme.BorderSoft);
                            float fillW = Mathf.Max(2f, Mathf.Clamp01(kb.Share01) * (barR.width - 2f));
                            if (fillW > 0f)
                            {
                                Color fill = tone;
                                // Make very bright fills slightly more pastel so the caption row above is legible.
                                if (fill.r + fill.g + fill.b > 2.2f)
                                    fill = new Color(fill.r * 0.85f, fill.g * 0.85f, fill.b * 0.85f, fill.a);
                                Widgets.DrawBoxSolid(new Rect(barR.x + 1f, barR.y + 1f, fillW, barR.height - 2f), fill);
                            }
                            cy += 16f;
                        }
                    }

                    // Legacy chain rows (label left / value right).
                    if (hasChain)
                    {
                        foreach (KpiChain ch in c.Chain)
                        {
                            if (cy + 18f > card.height - 4f) break;
                            Rect rR = new Rect(pad, cy, contentW, 18f);
                            Label(new Rect(rR.x, rR.y, rR.width * 0.6f, rR.height), ch.Label ?? "", GameFont.Tiny, UITheme.Dim);
                            Label(new Rect(rR.x + rR.width * 0.6f, rR.y, rR.width * 0.4f, rR.height), ch.Value ?? "", GameFont.Tiny, UITheme.Text, TextAnchor.UpperRight);
                            cy += 18f;
                        }
                    }

                    // One-line sub at the very bottom (legacy fallback / weapon epithet).
                    if (!string.IsNullOrEmpty(c.Sub))
                    {
                        Rect subR = new Rect(pad, card.height - 18f, contentW, 16f);
                        Label(subR, c.Sub, GameFont.Tiny, UITheme.SecondaryText);
                    }

                    // Badge group: compact tags (only used if no structured content).
                    if (c.Badges != null && c.Badges.Length > 0 && !hasBars && !hasChain)
                    {
                        float badgeY = card.height - 22f - KpiBadgeH - KpiBadgeGap;
                        float bx = pad;
                        float maxX = card.width - pad;
                        for (int b = 0; b < c.Badges.Length; b++)
                        {
                            string badge = c.Badges[b];
                            if (string.IsNullOrEmpty(badge)) continue;
                            Verse.Text.Font = GameFont.Tiny;
                            Vector2 bSize = Text.CalcSize(badge);
                            Verse.Text.Font = prevFont;
                            float bw = bSize.x + KpiBadgePadX * 2f;
                            if (bx + bw > maxX && bx > pad)
                            {
                                int remaining = 0;
                                for (int r = b; r < c.Badges.Length; r++)
                                    if (!string.IsNullOrEmpty(c.Badges[r])) remaining++;
                                if (remaining > 0)
                                {
                                    string more = "PersonalChronicle.UI.Kpi.CatMore".Translate(remaining);
                                    Verse.Text.Font = GameFont.Tiny;
                                    Vector2 mSize = Text.CalcSize(more);
                                    Verse.Text.Font = prevFont;
                                    Badge(new Rect(bx, badgeY, mSize.x + KpiBadgePadX * 2f, KpiBadgeH), more, UITheme.Info);
                                }
                                break;
                            }
                            Badge(new Rect(bx, badgeY, bw, KpiBadgeH), badge, tone);
                            bx += bw + KpiBadgeGap;
                        }
                    }

                    GUI.EndGroup();
                }
                Widgets.EndScrollView();
            }
            finally
            {
                GUI.color = prevColor;
                Verse.Text.Font = prevFont;
            }
        }

    }
}

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

            GUI.color = UITheme.Shadow;
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
        // v1.1.5: if the active theme ships a panel texture, draw it; otherwise
        // fall back to the token colour (keeps wuxia/steampunk/gothic intact).
        internal static void Panel(Rect rect, Color fill = default)
        {
            Texture2D tex = UITextureLibrary.Get(UITheme.ActiveThemeId, UITextureLibrary.Panel);
            if (tex != null)
            {
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill);
            }
            else
            {
                Color c = fill == default ? UITheme.Panel : fill;
                Widgets.DrawBoxSolid(rect, c);
            }
            Border(rect, UITheme.BorderSoft);
        }

        // ---- Card: raised surface + left accent stripe + soft border ----
        // v1.1.5: card body uses theme texture when available, else token colour.
        internal static void Card(Rect rect, Color accent)
        {
            Texture2D tex = UITextureLibrary.Get(UITheme.ActiveThemeId, UITextureLibrary.Card);
            if (tex != null)
            {
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolid(rect, UITheme.Card);
            }
            Border(rect, UITheme.BorderSoft);
            Color prev = GUI.color;
            GUI.color = accent;
            Widgets.DrawLineVertical(rect.x, rect.y, rect.height);
            Widgets.DrawLineVertical(rect.x + 1f, rect.y, rect.height);
            GUI.color = prev;
        }

        /// <summary>
        /// v1.1.4 劳模住所/工坊信息条小卡：键值对（左键右值），底 = Card 纹理或令牌色，
        /// 左色条 = accent（住所=Alive 绿 / 工坊=PillGold），边框 = BorderSoft。
        /// 对齐 HTML 预览 <c>.rw-card</c>（住所/工作场所右上角双卡）。
        /// </summary>
        internal static void PairCard(Rect rect, string key, string value, Color accent,
            GameFont keyFont = GameFont.Tiny, GameFont valueFont = GameFont.Small,
            float valueReservedRight = 0f)
        {
            Texture2D tex = UITextureLibrary.Get(UITheme.ActiveThemeId, UITextureLibrary.Card);
            if (tex != null)
            {
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolid(rect, UITheme.Card);
            }
            Border(rect, UITheme.BorderSoft);
            Color prev = GUI.color;
            GUI.color = accent;
            Widgets.DrawLineVertical(rect.x, rect.y, rect.height);
            Widgets.DrawLineVertical(rect.x + 1f, rect.y, rect.height);
            GUI.color = prev;

            float padX = 7f;
            float labelW = Verse.Text.CalcSize(key).x + 4f;
            Label(new Rect(rect.x + padX, rect.y, labelW, rect.height),
                key, keyFont, UITheme.Dim, TextAnchor.MiddleLeft);

            // value 区域：右侧预留 valueReservedRight（按钮区），且按可用宽度截断 + 省略号。
            float valueX = rect.x + padX + labelW;
            float valueRightEdge = rect.xMax - padX - Mathf.Max(0f, valueReservedRight);
            float valueW = Mathf.Max(0f, valueRightEdge - valueX - 2f);
            string drawnValue = TruncateForWidth(value, valueW, valueFont);
            Label(new Rect(valueX, rect.y, valueW, rect.height),
                drawnValue, valueFont, UITheme.Text, TextAnchor.MiddleRight);
        }

        /// <summary>
        /// v1.1.4 UI 优化：按可用宽度截断文字并加省略号，避免长名字撑爆 PairCard 把按钮遮住。
        /// </summary>
        private static string TruncateForWidth(string text, float maxWidth, GameFont font)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0f) return text ?? string.Empty;
            GameFont prev = Verse.Text.Font;
            try
            {
                Verse.Text.Font = font;
                if (Verse.Text.CalcSize(text).x <= maxWidth) return text;
                string ellipsis = "…";
                int len = text.Length;
                while (len > 0 && Verse.Text.CalcSize(text.Substring(0, len) + ellipsis).x > maxWidth)
                {
                    len--;
                }
                if (len <= 0) return ellipsis;
                return text.Substring(0, len) + ellipsis;
            }
            finally
            {
                Verse.Text.Font = prev;
            }
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

        // ---- ClickableCard: key + value + optional jump-to arrow.
        // Whole card clickable (rename); right-side arrow region clickable (jump-to).
        // The sub parameter is accepted for API stability but not rendered (v1.1.4:
        // hide coordinates per user request — jump-to arrow icon indicates affordance).
        // Returns true when the whole card was clicked.
        // out jumpClicked: the right-arrow region was clicked (jump-to position). ----
        internal static bool ClickableCard(Rect rect, string key, string value, string sub,
            Color accent, bool subClickable, out bool jumpClicked,
            GameFont keyFont = GameFont.Tiny, GameFont valueFont = GameFont.Small,
            GameFont subFont = GameFont.Tiny)
        {
            jumpClicked = false;
            float padX = 7f;
            float arrowW = 16f; // right-side jump-to arrow region width

            Texture2D tex = UITextureLibrary.Get(UITheme.ActiveThemeId, UITextureLibrary.Card);
            if (tex != null)
            {
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill);
            }
            else
            {
                Widgets.DrawBoxSolid(rect, UITheme.Card);
            }
            Border(rect, UITheme.BorderSoft);
            Widgets.DrawHighlightIfMouseover(rect);

            Color prev = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            try
            {
                // left accent bar
                GUI.color = accent;
                Widgets.DrawLineVertical(rect.x, rect.y, rect.height);
                Widgets.DrawLineVertical(rect.x + 1f, rect.y, rect.height);

                // v1.1.4 布局优化：垂直布局 —— key 小字上沿（Tiny），value 大字下沿（Small），
                // value 占据卡片整宽（左侧 padX，右侧预留箭头区），长文本加省略号。
                float valueRight = rect.xMax - padX - (subClickable ? arrowW : 0f);
                float valueW = Mathf.Max(0f, valueRight - (rect.x + padX) - 2f);
                // key 行：Tiny 10px 高，居中偏上
                Label(new Rect(rect.x + padX, rect.y + 3f, valueW, 14f),
                    key, keyFont, UITheme.Dim, TextAnchor.UpperLeft);
                // value 行：Small 20px 高，居中对齐卡片中线偏下
                Label(new Rect(rect.x + padX, rect.y + rect.height * 0.42f, valueW, 20f),
                    TruncateForWidth(value, valueW, valueFont), valueFont, UITheme.Text, TextAnchor.UpperLeft);

                // Jump-to arrow on the right (only when subClickable). Coordinates are
                // hidden per user request; the ▶ icon signals "click to jump-to".
                if (subClickable)
                {
                    Rect arrowRect = new Rect(rect.xMax - arrowW, rect.y, arrowW, rect.height);
                    Color arrowColor = accent;
                    GUI.color = arrowColor;
                    Verse.Text.Anchor = TextAnchor.MiddleCenter;
                    Verse.Text.Font = GameFont.Tiny;
                    Widgets.Label(arrowRect, "▶");
                    GUI.color = prev;
                }
            }
            finally
            {
                Verse.Text.Font = prevFont;
                Verse.Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = prev;
            }

            bool clicked = Widgets.ButtonInvisible(rect);

            // jump-to clickable region (right-side arrow strip)
            if (subClickable)
            {
                Rect arrowBtnRect = new Rect(rect.xMax - arrowW, rect.y, arrowW, rect.height);
                if (Widgets.ButtonInvisible(arrowBtnRect))
                {
                    jumpClicked = true;
                    clicked = false;
                }
            }
            return clicked;
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

        // ---- StatCell background helper: theme texture or token colour ----
        private static void DrawStatCellBackground(Rect rect)
        {
            Texture2D tex = UITextureLibrary.Get(UITheme.ActiveThemeId, UITextureLibrary.StatCell);
            if (tex != null)
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill);
            else
                Widgets.DrawBoxSolid(rect, UITheme.PanelRaised);
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
            DrawStatCellBackground(rect);
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
            DrawStatCellBackground(rect);
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
            public string TitleSide; // v1.1.4: right-aligned small text on the SAME row as the title (e.g. 传承锻造者) — may be empty
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

        /// <summary>One legacy-chain row: holder label + kill count.
        /// v1.1.4 传承链改用 <see cref="KpiBar"/>（进度条），本结构保留以防回归。</summary>
        internal struct KpiChain
        {
#pragma warning disable CS0649
            public string Label;     // holder label (e.g. "戈治钟 · 初代")
            public string Value;     // kill count text (e.g. "18 杀")
#pragma warning restore CS0649
        }

        internal static readonly float KpiCardH = 250f;
        internal static readonly float KpiGap = 10f;
        // Badge group metrics (preview E1): height 16, padding 6, wrap with 4f line gap.
        internal static readonly float KpiBadgeH = 16f;
        internal static readonly float KpiBadgePadX = 6f;
        internal static readonly float KpiBadgeGap = 4f;
        // v1.1.4: 六宫格进度条间距统一（caption↔bar 与 bar↔下一 caption 同距 KpiBarGapY）。
        internal static readonly float KpiBarCaptionH = 17f;
        internal static readonly float KpiBarH = 12f;
        // v1.1.4 加大：caption↔bar 间距 4f→8f（用户要求"文本与进度条间隔扩大"）。
        // 锻造者字段不可减少（传承宫格 TitleSide 已装配，KPI 行仍保留代数/当代击杀 2 行 + 3 进度条占位）。
        internal static readonly float KpiBarGapY = 8f;

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
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            try
            {
                int rows = (cells.Length + cols - 1) / cols;
                float contentH = rows * (KpiCardH + KpiGap);
                // v1.1.4 横向修复：inner 宽扣除滚动条，cardW 基于 inner.width 计算，
                // 保证 3 列恰好填满 inner（第三列不再被 scrollview 右边缘裁掉）。
                Rect inner = new Rect(0f, 0f, rect.width - UITheme.ScrollbarThickness, Mathf.Max(contentH, rect.height));
                float cardW = (inner.width - KpiGap * (cols - 1)) / cols;

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
                    // v1.1.4: 标题行左侧=宫格名，右侧=TitleSide（如传承锻造者），右对齐小字。
                    Rect titleR = new Rect(pad, 8f, card.width - pad * 2, 18f);
                    if (!string.IsNullOrEmpty(c.TitleSide))
                    {
                        Verse.Text.Font = GameFont.Tiny;
                        Vector2 sideSize = Text.CalcSize(c.TitleSide);
                        Verse.Text.Font = prevFont;
                        float sideW = Mathf.Clamp(sideSize.x + 6f, 0f, titleR.width * 0.55f);
                        Rect sideR = new Rect(titleR.xMax - sideW, titleR.y, sideW, titleR.height);
                        Label(sideR, c.TitleSide, GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleRight);
                        titleR = new Rect(titleR.x, titleR.y, titleR.width - sideW - 4f, titleR.height);
                    }
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
                    // Adaptive font: long weapon names (e.g. >4 Chinese characters) can
                    // exceed the Medium width and wrap to a second line. Shrink to Small,
                    // then Tiny if necessary, and truncate as last resort.
                    string valText = c.Value ?? "";
                    GameFont valueFont = GameFont.Medium;
                    Verse.Text.Font = valueFont;
                    if (Text.CalcSize(valText).x > valueTextR.width)
                    {
                        valueFont = GameFont.Small;
                        Verse.Text.Font = valueFont;
                        if (Text.CalcSize(valText).x > valueTextR.width)
                        {
                            valueFont = GameFont.Tiny;
                            Verse.Text.Font = valueFont;
                        }
                    }
                    Verse.Text.Font = prevFont;
                    string fittedValue = TruncateToWidth(valText, valueTextR.width, valueFont);

                    if (string.IsNullOrEmpty(c.Unit))
                    {
                        Label(valueTextR, fittedValue, valueFont, valueColor);
                    }
                    else
                    {
                        Label(valueTextR, fittedValue, valueFont, valueColor);
                        Verse.Text.Font = valueFont;
                        Vector2 valSize = Text.CalcSize(fittedValue);
                        Verse.Text.Font = prevFont;
                        Rect unitR = new Rect(valueTextR.x + Mathf.Min(valSize.x, valueTextR.width) + 4f,
                            valueTextR.y + (valueTextR.height - 18f) * 0.5f, valueTextR.width, 18f);
                        Label(unitR, c.Unit, GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
                    }

                    // ---- Structured sub-content (mirrors preview v6.6 six-grid) ----
                    float cy = 72f; // below the big value row
                    float contentW = card.width - pad * 2;

                    // KPI strip rows (label left / value right). When empty, show a
                    // "--" placeholder row so the card layout stays identical between
                    // populated and unpopulated states (easier screenshot diffing).
                    string noRec = "PersonalChronicle.UI.Kpi.NoRecord".Translate().ToString();
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
                    else
                    {
                        Rect rR = new Rect(pad, cy, contentW, 18f);
                        Label(new Rect(rR.x, rR.y, rR.width * 0.55f, rR.height), noRec, GameFont.Tiny, UITheme.Dim);
                        Label(new Rect(rR.x + rR.width * 0.55f, rR.y, rR.width * 0.45f, rR.height), "--", GameFont.Tiny, UITheme.Muted, TextAnchor.UpperRight);
                        cy += 18f;
                    }

                    // Section divider before bars / chain.
                    bool hasBars = c.Bars != null && c.Bars.Length > 0;
                    bool hasChain = c.Chain != null && c.Chain.Length > 0;
                    if (hasBars || hasChain)
                    {
                        cy += 2f;
                        Widgets.DrawLineHorizontal(pad, cy, contentW, UITheme.BorderSoft);
                        cy += 8f;
                    }

                    // Progress bars（结构/贡献/战损/击杀构成等）。
                    // v1.1.4 统一基准：caption 固定在 bar 上方；N 个 bar 等距分布——第 1 条贴近
                    // 分割线下方、第 N 条贴近卡片底（margin 6f），各 bar 槽垂直居中，3 条间隔平均化。
                    if (hasBars)
                    {
                        int nBars = c.Bars.Length;
                        float barsTop = cy;                                  // 第 1 个 caption 顶 y
                        float barsBottom = card.height - 6f;                // 卡片内底（含 6f 边距）
                        float totalH = Mathf.Max(0f, barsBottom - barsTop);
                        float slotH = nBars > 0 ? totalH / nBars : 0f;
                        // 单槽内：caption + gap + bar；多出的 padding 在槽内上下对称。
                        float innerH = KpiBarCaptionH + KpiBarGapY + KpiBarH;
                        for (int bi = 0; bi < nBars; bi++)
                        {
                            KpiBar kb = c.Bars[bi];
                            float slotPad = Mathf.Max(0f, (slotH - innerH) / 2f);
                            float slotY = barsTop + bi * slotH;
                            float capY = slotY + slotPad;
                            float barY = capY + KpiBarCaptionH + KpiBarGapY;

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
                            Rect capR = new Rect(pad, capY, capW, KpiBarCaptionH);
                            ShadowLabel(capR, kb.Caption ?? "", GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
                            if (tagW > 0f)
                            {
                                string tagClipped = TruncateToWidth(tag, tagW - 4f, GameFont.Tiny);
                                Rect tagR = new Rect(pad + contentW - tagW, capY, tagW, KpiBarCaptionH);
                                ShadowLabel(tagR, tagClipped, GameFont.Tiny, UITheme.SecondaryText, TextAnchor.MiddleRight);
                            }

                            // Bar: dark track + soft border + tone fill (inset by 1px).
                            Rect barR = new Rect(pad, barY, contentW, KpiBarH);
                            Widgets.DrawBoxSolid(barR, UITheme.BorderHair);
                            Border(barR, UITheme.BorderSoft);
                            float fillW = Mathf.Max(2f, Mathf.Clamp01(kb.Share01) * (barR.width - 2f));
                            if (fillW > 0f)
                            {
                                Color fill = tone;
                                if (fill.r + fill.g + fill.b > 2.2f)
                                    fill = new Color(fill.r * 0.85f, fill.g * 0.85f, fill.b * 0.85f, fill.a);
                                Widgets.DrawBoxSolid(new Rect(barR.x + 1f, barR.y + 1f, fillW, barR.height - 2f), fill);
                            }
                        }
                    }

                    // Legacy chain rows (label left / value right).
                    // v1.1.4 统一基准：与 bars 一致等距分布（贴 divider 下方、贴卡片底、间隔平均化）。
                    if (hasChain)
                    {
                        int nCh = c.Chain.Length;
                        float chainTop = cy;
                        float chainBottom = card.height - 6f;
                        float totalH = Mathf.Max(0f, chainBottom - chainTop);
                        float slotH = nCh > 0 ? totalH / nCh : 0f;
                        const float chainRowH = 18f;
                        for (int ci = 0; ci < nCh; ci++)
                        {
                            KpiChain ch = c.Chain[ci];
                            float slotPad = Mathf.Max(0f, (slotH - chainRowH) / 2f);
                            float rowY = chainTop + ci * slotH + slotPad;
                            Rect rR = new Rect(pad, rowY, contentW, chainRowH);
                            Label(new Rect(rR.x, rR.y, rR.width * 0.6f, rR.height), ch.Label ?? "", GameFont.Tiny, UITheme.Dim);
                            Label(new Rect(rR.x + rR.width * 0.6f, rR.y, rR.width * 0.4f, rR.height), ch.Value ?? "", GameFont.Tiny, UITheme.Text, TextAnchor.UpperRight);
                        }
                    }

                    // No bars and no chain: keep the sub-content block visible with a
                    // "--" placeholder so populated/unpopulated cards render identically.
                    if (!hasBars && !hasChain)
                    {
                        cy += 2f;
                        Widgets.DrawLineHorizontal(pad, cy, contentW, UITheme.BorderSoft);
                        cy += 8f;
                        Rect rR = new Rect(pad, cy, contentW, 18f);
                        Label(new Rect(rR.x, rR.y, rR.width * 0.55f, rR.height), noRec, GameFont.Tiny, UITheme.Dim);
                        Label(new Rect(rR.x + rR.width * 0.55f, rR.y, rR.width * 0.45f, rR.height), "--", GameFont.Tiny, UITheme.Muted, TextAnchor.UpperRight);
                        cy += 18f;
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

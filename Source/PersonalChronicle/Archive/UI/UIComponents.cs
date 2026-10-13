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
    /// [ARC-013] 本类已 partial 化：六宫格 KPI 复合组件见 UIComponents.KpiGrid.cs，
    /// 本文件保留基础组件（Label / Panel / Card / ProgressBar ...）。零契约改动，仅物理搬移。
    internal static partial class UIComponents
    {
        // ---- Low-level pairing guard: draw a label with a temporary color+font,
        // then restore both. This is the primitive that removes scattered
        // GUI.color / Text.Font assignments across the window. ----
        internal static void Label(Rect rect, string text, GameFont font, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft)
        {
            // 【RUIF 硬依赖 · 要求二】实现**转发 RUIF L0 适配层** `RUIF.Draw.Label`
            // （它同样保存/恢复 GUI.color 与 Text.Font/Anchor，并固化 1.6 的 DrawBoxSolid
            // 重置 GUI.color 坑）——保留本包装只为维持工程调用约定（默认 anchor、命名），
            // 绘制实现**零自研**；调用点无需改动。
            // ⚠ 保险：本工程所有 Label 均配合 TruncateToWidth 使用，预期单行；若链路中
            // 任何一处把 Text.WordWrap 设为 true 且未恢复，定宽标签会被折行（实机表现：
            // 「现役殖民者」拆成两行并压到环内）。强制 false 绘制后恢复，不影响其他模块。
            bool prevWrap = Verse.Text.WordWrap;
            Verse.Text.WordWrap = false;
            try
            {
                RUIF.Draw.Label(rect, text, font, color, anchor);
            }
            finally
            {
                Verse.Text.WordWrap = prevWrap;
            }

            // ⚠ 2026-09-19（审计 §3.2）：**截断补偿的兜底** —— 有些调用点把**完整文本**
            // 直接交给 Label（不预先 `TruncateToWidth`）⇒ 由本方法补上悬停。
            //
            // ⚠ 成本控制：**先做矩形命中测试**（极廉价），只有鼠标真的停在这一条上才做文本实测
            //   ⇒ 绝大多数帧只测 0~1 条，**没有 per-frame 全量测量开销**（这是能放进热路径的前提）。
            // ⚠ 预截断的调用点在这里**补不了**（原文已经丢了，`text` 就是截断后的串）
            //   ⇒ 它们应改用 `TruncateToWidth(..., tipRect)` 重载（见其注释）。
            if (Event.current != null && !string.IsNullOrEmpty(text)
                && rect.Contains(Event.current.mousePosition)
                && !string.Equals(TruncateToWidth(text, rect.width, font), text, System.StringComparison.Ordinal))
            {
                TooltipHandler.TipRegion(rect, text);
            }
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

        /// <summary>
        /// <see cref="TruncateToWidth(string,float,GameFont)"/> + **截断补偿**：
        /// 真的被截断时，在 <paramref name="tipRect"/> 上挂悬停显示**原文**。
        ///
        /// <para>⚠ 2026-09-19（审计 §3.2）：全工程 43 处截断里只有 7 处挂了悬停。
        /// **根因不是"调用点健忘"，而是"截断"与"补偿"被拆成了两条独立语句** ——
        /// 一旦拆开，被截掉的信息就**只存在于调用点的记忆里**，改写/迁移时必然丢失
        /// （本线已多次见到：改一行布局就把补偿丢了）。</para>
        ///
        /// <para>⇒ 把补偿**收进截断本身**：用本重载的调用点，**结构上不可能**只截断不补偿。
        /// 📌 工程纪律：**凡是会丢信息的操作，都要把"补回来"的手段做进同一个调用**。</para>
        ///
        /// <para>⚠ 代价：每条被截断且**鼠标正停在上面**的标签多一次 <c>TipRegion</c>；
        /// 判据用"结果是否变了"而不是重算宽度（截断内部已经量过，不重复测）。</para>
        /// </summary>
        internal static string TruncateToWidth(string text, float maxWidth, GameFont font, Rect tipRect)
        {
            string clipped = TruncateToWidth(text, maxWidth, font);
            if (!string.IsNullOrEmpty(text)
                && !string.Equals(clipped, text, System.StringComparison.Ordinal))
            {
                TooltipHandler.TipRegion(tipRect, text);
            }
            return clipped;
        }

        /// <summary>
        /// 按宽度截断并在**中间**插省略号（两端保留）；本来就放得下则原样返回。
        ///
        /// <para><b>⛔ 2026-09-13 实机修复（影响面 = 全工程所有截断标签）</b>：原实现从 <c>i = 0</c>
        /// 起步递增，而 <c>i = 0</c> 的第一个候选就是<b>最短的 <c>"…"</c></b> —— 只要省略号本身放得下
        /// 就立刻 <c>return "…"</c> ⇒ <b>凡是被截断的文本都只剩三个点</b>
        /// （实机症状：设备卡名称、车间角标、图例全变 <c>…</c>，用户报「设备信息丢失」）。</para>
        ///
        /// <para>修法：**从长到短二分**（候选宽度随保留字数单调不减），第一个放得下的即答案；
        /// 连 <c>"…"</c> 都放不下才返回空串。二分而非线性，是为了绘制路径的测量次数可控
        /// （≤ log₂(长度)，原线性写法在修好后会是 O(n²)）。</para>
        /// </summary>
        internal static string TruncateToWidth(string text, float maxWidth, GameFont font)
        {
            if (string.IsNullOrEmpty(text)) return text;
            GameFont prev = Verse.Text.Font;
            Verse.Text.Font = font;
            try
            {
                if (Text.CalcSize(text).x <= maxWidth) return text;

                int len = text.Length;
                int lo = 1;
                int hi = len - 1;
                string best = null;
                while (lo <= hi)
                {
                    int keep = lo + (hi - lo) / 2;
                    int rightKeep = keep / 2;
                    int leftKeep = keep - rightKeep;
                    string candidate = text.Substring(0, leftKeep) + "…" +
                        text.Substring(len - rightKeep, rightKeep);
                    if (Text.CalcSize(candidate).x <= maxWidth)
                    {
                        best = candidate;
                        lo = keep + 1;
                    }
                    else
                    {
                        hi = keep - 1;
                    }
                }
                if (best != null)
                {
                    return best;
                }
                return Text.CalcSize("…").x <= maxWidth ? "…" : string.Empty;
            }
            finally
            {
                Verse.Text.Font = prev;
            }
        }

        // ---- Panel: filled surface + soft border ----
        // v1.1.5: draw with the UITheme.Panel token colour directly. Texture-backed
        // panels were dropped because the shipped epic panel.png/card.png are light
        // parchment assets that read as "white" on the dark window — see frontend
        // health-check 2026-08-21. Token colour keeps every theme consistent.
        internal static void Panel(Rect rect, Color fill = default)
        {
            Color c = fill == default ? UITheme.Panel : fill;
            RUIF.Draw.Box(rect, c);
            Border(rect, UITheme.BorderSoft);
        }

        // ---- Card: raised surface + left accent stripe + soft border ----
        // v1.1.5: card body uses the UITheme.Card token colour directly (texture
        // backing dropped — see Panel note above re: light parchment assets).
        internal static void Card(Rect rect, Color accent)
        {
            RUIF.Draw.Box(rect, UITheme.Card);
            Border(rect, UITheme.BorderSoft);
            // 【RUIF 硬依赖】左侧 2px accent 竖线走 `RUIF.Draw.LineV`（自带染色作用域）。
            RUIF.Draw.LineV(rect.x, rect.y, rect.height, accent);
            RUIF.Draw.LineV(rect.x + 1f, rect.y, rect.height, accent);
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
            RUIF.Draw.Box(rect, UITheme.Card);
            Border(rect, UITheme.BorderSoft);
            Color prev = GUI.color;
            GUI.color = accent;
            Widgets.DrawLineVertical(rect.x, rect.y, rect.height);
            Widgets.DrawLineVertical(rect.x + 1f, rect.y, rect.height);
            GUI.color = prev;

            float padX = 7f;
            float labelW = 0f;
            if (!string.IsNullOrEmpty(key))
            {
                GameFont prevFont = Verse.Text.Font;
                Verse.Text.Font = keyFont;
                labelW = Verse.Text.CalcSize(key).x + 4f;
                Verse.Text.Font = prevFont;
            }
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
        /// v4.17 体检：maxWidth≤0 时返回空串（旧返回全文会画进 0 宽矩形溢出）。
        /// </summary>
        private static string TruncateForWidth(string text, float maxWidth, GameFont font)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            if (maxWidth <= 0f) return string.Empty;
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
            // starts after it instead of a fixed 230px offset. 长标题按可用宽度截断
            // （UI-009），避免换行后超出 22f 固定高度被裁剪。
            float maxTitleW = Mathf.Max(40f, rect.width - 18f);
            float titleW = Mathf.Min(240f, Verse.Text.CalcSize(title).x + 8f);
            string clipped = TruncateToWidth(title, titleW, GameFont.Small);
            GUI.color = UITheme.Accent;
            RUIF.Draw.Box(new Rect(rect.x, y + 4f, 4f, 14f), UITheme.Accent);
            GUI.color = prevColor;
            Widgets.Label(new Rect(rect.x + 10f, y, Mathf.Min(titleW, maxTitleW), 22f), clipped);
            float ruleX = rect.x + 10f + titleW + 4f;
            Rule(new Rect(ruleX, y + 11f, Mathf.Max(0f, rect.width - (ruleX - rect.x)), UITheme.RuleHeight),
                UITheme.Border);
            Verse.Text.Font = prevFont;
        }

        // ---- Hairline divider between rows/sections ----
        internal static void Rule(Rect rect, Color color)
        {
            // 【RUIF 硬依赖 · 要求二】转发 `RUIF.Draw.LineH` —— 由 RUIF 的染色作用域接管
            //（不再手写 GUI.color 保存/恢复，避免漏恢复导致染色泄漏）。
            RUIF.Draw.LineH(rect.x, rect.y, rect.width, color);
        }

        // ---- Border: 1px frame on all four sides ----
        internal static void Border(Rect rect, Color color)
        {
            // 【RUIF 硬依赖 · 要求二】实现转发 `RUIF.Draw.Border`（四边线，内部已处理染色作用域）。
            RUIF.Draw.Border(rect, color);
        }

        // ---- Pill: status chip (alive/dead/role) ----
        internal static void Pill(Rect rect, string label, Color color)
        {
            Color prev = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Verse.Text.Anchor;
            GUI.color = UITheme.WithAlpha(color, 0.16f);
            RUIF.Draw.Box(rect, GUI.color);
            Border(rect, color);
            GUI.color = color;
            Verse.Text.Font = GameFont.Tiny;
            Verse.Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, TruncateToWidth(label ?? "", Mathf.Max(0f, rect.width - 4f), GameFont.Tiny));
            Verse.Text.Anchor = prevAnchor;
            Verse.Text.Font = prevFont;
            GUI.color = prev;
        }

        // ---- Badge: dense tag (matches ArchiveUiStyle.DrawBadge) ----
        internal static void Badge(Rect rect, string label, Color color)
        {
            Color prev = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Verse.Text.Anchor;
            Verse.Text.Font = GameFont.Tiny;
            GUI.color = UITheme.WithAlpha(color, 0.14f);
            RUIF.Draw.Box(rect, GUI.color);
            Border(rect, color);
            GUI.color = color;
            Verse.Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, TruncateToWidth(label ?? "", Mathf.Max(0f, rect.width - 4f), GameFont.Tiny));
            Verse.Text.Anchor = prevAnchor;
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

            RUIF.Draw.Box(rect, UITheme.Card);
            Border(rect, UITheme.BorderSoft);
            Widgets.DrawHighlightIfMouseover(rect);

            Color prev = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Verse.Text.Anchor;
            try
            {
                // left accent bar
                GUI.color = accent;
                Widgets.DrawLineVertical(rect.x, rect.y, rect.height);
                Widgets.DrawLineVertical(rect.x + 1f, rect.y, rect.height);

                // v1.1.6 优化：key 上移到 y+2f（更贴近顶部），value 垂直居中偏下（0.38→0.40），
                // 配合 ResidenceBarH=52f 让三卡内文字分布更均匀。
                float valueRight = rect.xMax - padX - (subClickable ? arrowW : 0f);
                float valueW = Mathf.Max(0f, valueRight - (rect.x + padX) - 2f);
                // key 行：Tiny，上沿收紧
                float keyH = Verse.Text.LineHeight;
                Label(new Rect(rect.x + padX, rect.y + 2f, valueW, keyH),
                    TruncateToWidth(key ?? "", valueW, keyFont), keyFont, UITheme.Dim, TextAnchor.UpperLeft);
                // value 行：Small，垂直微调居中
                Label(new Rect(rect.x + padX, rect.y + rect.height * 0.40f, valueW, UITheme.FontBodyLineHeight),
                    TruncateForWidth(value, valueW, valueFont), valueFont, UITheme.Text, TextAnchor.UpperLeft);

                // Jump-to arrow on the right (only when subClickable). Coordinates are
                // hidden per user request; the ▶ icon signals "click to jump-to".
                if (subClickable)
                {
                    Rect arrowRect = new Rect(rect.xMax - arrowW, rect.y, arrowW, rect.height);
                    GUI.color = accent;
                    Verse.Text.Anchor = TextAnchor.MiddleCenter;
                    Verse.Text.Font = GameFont.Tiny;
                    Widgets.Label(arrowRect, "▶");
                    GUI.color = prev;
                }
            }
            finally
            {
                Verse.Text.Font = prevFont;
                Verse.Text.Anchor = prevAnchor;
                GUI.color = prev;
            }

            // ⚠ 2026-09-19（审计 §2.4 / P1）：**箭头热区必须先判、且必须排除箭头区**。
            //
            // 原实现顺序相反：先 `ButtonInvisible(rect)`（**整卡**，含右侧 16px 箭头条）
            // —— 这一次点击已经把 `MouseDown` 事件消费掉 ⇒ 后面箭头区的判定**恒为 false**
            // ⇒ 画出来的 ▶ 是**看得见、点不到**的假热区；而真实入口是**没有视觉提示的右键**。
            // （工程自己的问题记录早已承认这条：`docs/问题跟踪/实机测试问题记录.md:398-408`）
            //
            // ⇒ 修法：**先判箭头区**（它优先级更高，因为它是卡内更小的目标），
            //   再判卡片主体时**扣掉箭头区** —— 两者热区不重叠，语义与视觉一致。
            bool clicked = false;
            Rect arrowBtnRect = new Rect(rect.xMax - arrowW, rect.y, arrowW, rect.height);
            if (subClickable && Widgets.ButtonInvisible(arrowBtnRect))
            {
                jumpClicked = true;
            }

            if (!jumpClicked)
            {
                // 右键检测：右键点击整卡区域 → jump-to（镜头跳转）。
                // v1.1.6 增加：用户语义明确为「右键=跳转坐标」（保留，作为无箭头的快捷路径）。
                if (subClickable
                    && Event.current.type == EventType.MouseDown && Event.current.button == 1
                    && rect.Contains(Event.current.mousePosition))
                {
                    jumpClicked = true;
                    Event.current.Use();
                }
                else
                {
                    clicked = Widgets.ButtonInvisible(rect);
                }
            }
            return clicked;
        }

        // ---- TintedBox: white box under a translucent tint (placeholder /
        // empty-state backgrounds). Keeps the previous GUI.color untouched. ----
        internal static void TintedBox(Rect rect, Color tint)
        {
            // v1.1.5 patch6: rimworld 1.6 Widgets.DrawBoxSolid 内部把 GUI.color 重置为
            // 传入的 Color（如 Color.white），忽略外层 GUI.color tint——导致 TintedBox
            // 永远画白色。正确做法：直接调 RUIF.Draw.Box(rect, tint)，让 rimworld
            // 内部把 GUI.color 改为 tint，再绘制 WhiteTex×tint = tint 像素。
            RUIF.Draw.Box(rect, tint);
        }

        // ---- ProgressBar: track + fill by 0..1 share (career bars, intensity) ----
        internal static void ProgressBar(Rect rect, float share01, Color fill)
        {
            // 【RUIF 硬依赖 · 要求二】改用 RUIF **进度条组件** `ProgressAtoms.CapacityBar`
            // （值/容量语义 ⇒ 传 `(share01, 1f)`；内部自绘 track + fill，含溢出红块判定，
            //  本工程 share01 已 Clamp01 ⇒ 永不溢出）。
            RUIF.ProgressAtoms.CapacityBar(rect, Mathf.Clamp01(share01), 1f, fill);
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

        // ---- StatCell background helper: token colour (texture backing dropped) ----
        private static void DrawStatCellBackground(Rect rect)
        {
            RUIF.Draw.Box(rect, UITheme.PanelRaised);
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

        /// <summary>Subsection header (small accent rule + localized title key).
        /// v4.17 体检：长 Key 截断 + 规则高度负值保护（UI-009 / 参数空间）。</summary>
        internal static void DrawSubsectionHeader(Rect rect, string key)
        {
            Color prev = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            Rect rule = new Rect(rect.x, rect.y + 4f, 3f, Mathf.Max(0f, rect.height - 8f));
            RUIF.Draw.Box(rule, UITheme.Accent);
            Verse.Text.Font = GameFont.Small;
            GUI.color = UITheme.Text;
            // ⚠ 2026-09-19（CHG-20260919-ECON-UI-010）：改走语言级缓存。
            // 本原语（分区标题）被各面板在**绘制期**调用，原先每帧翻译 + 分配。
            string title = Loc(key);
            float titleW = Mathf.Max(0f, rect.width - 10f);
            Widgets.Label(new Rect(rect.x + 10f, rect.y, titleW, rect.height),
                TruncateToWidth(title, titleW, GameFont.Small));
            GUI.color = prev;
            Verse.Text.Font = prevFont;
        }

        /// <summary>Minimum card height StatCell needs to fit label+value+subLabel.</summary>
        internal const float StatCellMinHeight = 64f;

        /// <summary>
        /// 紧凑 KPI 格高 —— 预览 `.kstrip` 单格实测 **46.4**（标签 9px / 值 14px / `padding:6px 8px`）：
        /// 实机 Tiny 18 + Small 22 + 上下内距 4×2 + 行距 2 = **48**（见 <see cref="StatCellCompact"/>）。
        /// </summary>
        internal const float StatCellCompactHeight = 48f;

        /// <summary>
        /// **紧凑 KPI 格**（战役 / 地点 KPI 条专用）：标签 `Tiny` + 值 **`Small`**（行高 22）+
        /// 上下内距 4 / 行距 2 ⇒ 总高 <see cref="StatCellCompactHeight"/>。
        ///
        /// <para>⚠ 为什么新增方法而不改 <see cref="StatCell"/>：后者的值用 `Medium`（行高 **30**）⇒
        /// 硬下限 <see cref="StatCellMinHeight"/>=64，且被图鉴等 5+ 处复用；把 64 压到 48 必须
        /// 换字体档，会连带改所有复用点。新增紧凑档 = 只影响两处 KPI 条（回归面可控）。
        /// 预览的两条 KPI 条实测就是 46.4 高、值 14px（= `Small`），与紧凑档一致。</para>
        /// </summary>
        internal static void StatCellCompact(Rect rect, string label, string value, Color valueColor)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            DrawStatCellBackground(rect);
            Border(rect, UITheme.BorderSoft);
            const float padX = 8f;
            const float padY = 4f;
            const float gap = 2f;
            float innerW = Mathf.Max(0f, rect.width - padX * 2f);
            Verse.Text.Font = GameFont.Tiny;
            float labelH = Verse.Text.LineHeight;                       // Tiny = 18
            GUI.color = UITheme.Muted;
            Widgets.Label(new Rect(rect.x + padX, rect.y + padY, innerW, labelH),
                TruncateToWidth(label, innerW, GameFont.Tiny));
            Verse.Text.Font = GameFont.Small;
            float valueH = Verse.Text.LineHeight;                       // Small = 22
            float valueY = rect.y + padY + labelH + gap;
            float available = Mathf.Max(0f, rect.yMax - padY - valueY);
            GUI.color = valueColor;
            Widgets.Label(new Rect(rect.x + padX, valueY, innerW, Mathf.Min(valueH, available)),
                TruncateToWidth(value, innerW, GameFont.Small));
            GUI.color = prevColor;
            Verse.Text.Font = prevFont;
        }

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
            RUIF.Draw.Box(nodeRect, dotColor);
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

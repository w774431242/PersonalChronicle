using PersonalChronicle.MasterControl.Population;
using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;

namespace PersonalChronicle.Archive.UI
{
    /// <summary>
    /// [ARC-013] <see cref="UIComponents"/> 的「六宫格 KPI 复合组件」切片（v4.15 condense tab）。
    ///
    /// 原 UIComponents.cs 单文件 963 行，混合「基础组件」（Label / Panel / Card /
    /// ProgressBar ...）与「六宫格 KPI 复合组件」（KpiCell / KpiRow / KpiBar / KpiChain
    /// 数据类 + SixGrid 绘制）两组职责，违反 [ARC-013] 单一职责与「单文件 ≤ 800 行」。
    /// 按 08 §3「排序→识别→剥离」，本文件剥离其中的六宫格 KPI 复合组件——
    /// 它自包含、纯展示（所有值/色调由调用方从 DetailSnapshot 注入，无聚合/Read-Model 逻辑）。
    ///
    /// 剥离原则（[ARC-013] 零契约改动）：仅物理搬移，类名、方法签名、访问级别、
    /// 命名空间、翻译键一律未改；同 partial 类成员天然互相可见，跨文件调用无需改访问级别。
    /// </summary>
    internal static partial class UIComponents
    {
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
            // ⚠ 2026-09-19（审计 §1.2 / **P0**）：`BeginScrollView` 与 `BeginGroup` **必须在 finally 里闭合**。
            //
            // 原实现把 `EndGroup`/`EndScrollView` 直接写在 try 体的尾部 —— 任一次绘制抛异常
            // （`Label` / `DrawBoxSolid` / RUIF 调用；`RuifBridge` 全部方法都带 try 正说明它会抛）
            // ⇒ **两者都不执行** ⇒ **Unity GUI 组栈与裁剪栈永久失衡** ⇒ 此后所有窗口绘制错位、
            // 整帧异常 —— 这是"一处坏拖垮整个 UI"里最难恢复的一种。
            //
            // 📌 用法与同工程**正确范例**（`PersonnelArchiveHost.DrawEmbedded`）一致：
            // **用状态变量记住开了几层**，在 finally 里按层数逆序闭合。
            bool scrollOpen = false;
            bool groupOpen = false;
            try
            {
                int rows = (cells.Length + cols - 1) / cols;
                // v4.17 体检：内容高 = 行高总和 - 2f 与视口精确对齐（ITab 六宫格
                // 恰好 2 行 520f vs 视口 518f，旧值恒显 2px 垂直滚动条）。
                float contentH = Mathf.Max(0f, rows * (KpiCardH + KpiGap) - 2f);
                // v1.1.4 横向修复：inner 宽扣除滚动条，cardW 基于 inner.width 计算，
                // 保证 3 列恰好填满 inner（第三列不再被 scrollview 右边缘裁掉）。
                // v4.17 体检：极窄容器下宽度 clamp 到 ≥0（避免负宽 Rect）。
                Rect inner = new Rect(0f, 0f,
                    Mathf.Max(0f, rect.width - UITheme.ScrollbarThickness),
                    Mathf.Max(contentH, rect.height));
                float cardW = (inner.width - KpiGap * (cols - 1)) / cols;

                Widgets.BeginScrollView(rect, ref scroll, inner);
                scrollOpen = true;
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
                    groupOpen = true;

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
                        // v4.17 体检：TitleSide 只 clamp 宽度不截断 → 长文本向左溢出盖标题。
                        Label(sideR, UIComponents.TruncateToWidth(c.TitleSide, sideW - 4f, GameFont.Tiny),
                            GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleRight);
                        titleR = new Rect(titleR.x, titleR.y, titleR.width - sideW - 4f, titleR.height);
                    }
                    // ⚠ 2026-09-19（CHG-20260919-ECON-UI-010）：本行在**卡片循环**里 ⇒ 每帧 N 次
                    // 翻译 + 分配；改走语言级缓存（键来自数据，容量兜底已覆盖动态键）。
                    Label(titleR, Loc(c.TitleKey), GameFont.Tiny, UITheme.SecondaryText);

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
                        // v4.17 体检：同行指标截断（长文本向左溢出盖大数值）。
                        Label(metricR, UIComponents.TruncateToWidth(c.InlineMetric, mW - 4f, GameFont.Tiny),
                            GameFont.Tiny, valueEmpty ? UITheme.Dim : UITheme.Muted, TextAnchor.MiddleRight);
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
                    // ⚠ 2026-09-19（CHG-20260919-ECON-UI-010）：本行在**每张卡**的绘制里执行
                    // （空态卡也不例外）⇒ 改走语言级缓存。
                    string noRec = Loc("PersonalChronicle.UI.Kpi.NoRecord");
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
                                    // ⚠ 2026-09-19 复核：本行**有意不走 Loc** —— 它**带参数**
                                    // （`remaining` 逐卡不同），缓存会把上一张卡的数字返回给下一张
                                    // （错值远比慢严重）。它只在标签溢出时触发，且已被外层卡片数限制
                                    // ⇒ 保留原样，登记为**已知的绘制期带参翻译**。
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
                    groupOpen = false;
                }
            }
            finally
            {
                // ⚠ **逆序闭合 + 按"是否真的开了"判断**：即使异常恰好发生在 BeginGroup 与
                //   EndGroup 之间（最坏情况：卡片画到一半抛错），这里也能把栈恢复到
                //   进入本方法之前的状态。而旧实现会**永久少一层**。
                if (groupOpen)
                {
                    GUI.EndGroup();
                }
                if (scrollOpen)
                {
                    Widgets.EndScrollView();
                }
                GUI.color = prevColor;
                Verse.Text.Font = prevFont;
            }
        }
    }
}

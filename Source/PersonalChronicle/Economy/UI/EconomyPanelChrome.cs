using PersonalChronicle.Archive.UI;
using PersonalChronicle.Application.ReadModels;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.UI
{
    /// <summary>
    /// 经济中心五面板的**统一 chrome**（预览 <c>.panel</c> + <c>.ptitle</c> + 四角包角）。
    ///
    /// <para><b>权威源</b>：<c>系统中心蓝图/市场经济框架_玩家预览_RUIF单页统一版.html</c>
    /// （LAND-001：以预览源码 + CSS 选择器为准，注释不得覆盖权威源）。实测值
    /// （CDP <c>getBoundingClientRect</c>，1600×1000 视口，见
    /// <c>_scripts/_tmp_measure_econ.js</c> 产出）：</para>
    /// <code>
    /// .panel{background:var(--panel);border:1px solid var(--border-soft);border-radius:8px}
    /// .panel::before/::after{width:10px;height:10px;border-color:accent 58%;top/left 4px}
    /// .ptitle{padding:7px 13px;font-size:12px;font-weight:800;letter-spacing:.12em}
    ///         ⇒ 实测 32.39px；`.mark{3×13 accent}`；`.stencil{8.5px mono dim}`；
    ///         `.ruif{8.5px info 描边 4px 圆角}`；`.sub{margin-left:auto;10px dim}`
    /// .pbody{padding:10px 12px;gap:8px}  |  .pbody.tight{padding:7px 10px;gap:6px}
    /// </code>
    ///
    /// <para><b>字号档（🅑 B 类不可 1:1）</b>：预览正文 8.5~12.5px，引擎只有
    /// Tiny 12 / Small 14 / Medium 18 三档（行高 18 / 22 / 30）。故所有**行高**取
    /// 「引擎行高 + 2」以守住 LAND 铁律「文本 rect 高 ≥ 行高」，
    /// 标题条取 Small 行高（22）+ 上下各 5 = <b>32</b>（≈ 预览 32.39）。</para>
    /// </summary>
    internal static class EconomyPanelChrome
    {
        /// <summary>标题条高（预览实测 32.39）。</summary>
        internal const float TitleHeight = 32f;

        /// <summary>`.ptitle{padding:… 13px}`。</summary>
        internal const float TitlePadX = 13f;

        /// <summary>`.pbody{padding:10px 12px}`。</summary>
        internal const float BodyPadX = 12f;
        internal const float BodyPadTop = 10f;
        internal const float BodyGap = 8f;

        /// <summary>`.pbody.tight{padding:7px 10px;gap:6px}`。</summary>
        internal const float TightPadX = 10f;
        internal const float TightPadTop = 7f;
        internal const float TightGap = 6f;

        /// <summary>通用行高（`.sec-h` / `.drow` / `.chip` / `.qa` / `.msum` 实测 16~21.8）。</summary>
        internal const float RowHeight = 20f;

        /// <summary>小标题行高（`.sec-h` 实测 17.05）。</summary>
        internal const float SectionHeaderHeight = 20f;

        /// <summary>徽章行高（`.bdg` 实测 17.05）。</summary>
        internal const float BadgeHeight = 18f;

        /// <summary>角标条（`.chi .chbar` 实测 4）。</summary>
        internal const float MiniBarHeight = 4f;

        /// <summary>面板本体 + 标题条的绘制入口；返回 **body 内区**（已扣内边距）。</summary>
        internal static Rect Draw(Rect panel, string title, string stencil, string badge, string sub,
            bool accentPanel, bool tightBody, string titleTraits = null)
        {
            // `.panel`：底 + 描边（`.panel.accent{border-color:accent 42%}`）
            UIComponents.Card(panel, accentPanel ? UITheme.Accent : UITheme.BorderSoft);
            DrawCornerBrackets(panel);

            DrawTitleBar(panel, title, stencil, badge, sub, titleTraits);

            float padX = tightBody ? TightPadX : BodyPadX;
            float padTop = tightBody ? TightPadTop : BodyPadTop;
            return new Rect(panel.x + padX, panel.y + TitleHeight + padTop,
                Mathf.Max(10f, panel.width - padX * 2f),
                Mathf.Max(10f, panel.height - TitleHeight - padTop - (tightBody ? TightPadTop : BodyPadTop)));
        }

        /// <summary>
        /// `.panel::before/::after` 四角包角（10×10，2px 臂，accent 58%）。
        ///
        /// <para>2026-09-21 RUIF 合规审计整改：原 8 条 `Widgets.DrawBoxSolid` 自绘 ⇒
        /// 改用 **`RUIF.Shapes.CornerFrame`**（只画角不画整框，几何逐值等价：armLen 10 / thickness 2）。
        /// 保留 4px 内缩以维持预览观感。</para>
        /// </summary>
        private static void DrawCornerBrackets(Rect panel)
        {
            const float arm = 10f;
            const float thickness = 2f;
            const float inset = 4f;
            Rect frame = new Rect(panel.x + inset, panel.y + inset,
                Mathf.Max(arm, panel.width - inset * 2f), Mathf.Max(arm, panel.height - inset * 2f));
            RUIF.Shapes.CornerFrame(frame, UITheme.WithAlpha(UITheme.Accent, 0.58f), arm, thickness);
        }

        /// <summary>`.ptitle`：竖标 + 主标题 + stencil + 右端（社会特点 / 契约徽章）+ sub。</summary>
        private static void DrawTitleBar(Rect panel, string title, string stencil, string badge, string sub,
            string titleTraits)
        {
            Rect bar = new Rect(panel.x + 1f, panel.y + 1f, panel.width - 2f, TitleHeight - 1f);
            // `.ptitle{background:linear-gradient(90deg,accent-soft,transparent 58%)}` ⇒ 左浓右淡的等价表达：
            // 引擎无渐变，取一层很淡的 accent 平铺（观感等价、不抢正文）。
            UIComponents.TintedBox(bar, UITheme.WithAlpha(UITheme.Accent, 0.10f));
            UIComponents.Rule(new Rect(bar.x, bar.yMax - 1f, bar.width, 1f), UITheme.BorderHair);

            float x = bar.x + TitlePadX;
            // `.mark{width:3px;height:13px;background:accent}`
            Widgets.DrawBoxSolid(new Rect(x, bar.y + (bar.height - 13f) * 0.5f, 3f, 13f), UITheme.Accent);
            x += 3f + 8f;

            float textY = bar.y + (bar.height - 22f) * 0.5f;
            // 先量右端 sub，给中间留出可用宽（LAND：先量后写）
            string subText = sub ?? string.Empty;
            float subWidth = string.IsNullOrEmpty(subText)
                ? 0f
                : Mathf.Min(MeasureWidth(subText, GameFont.Tiny), bar.width * 0.34f);
            float subX = bar.xMax - TitlePadX - subWidth;

            float titleWidth = MeasureWidth(title, GameFont.Small);
            float hardRight = subX - 8f;
            // 徽章度量（先量，供 stencil 让位）
            float badgeWidth = string.IsNullOrEmpty(badge) ? 0f : MeasureWidth(badge, GameFont.Tiny) + 10f;

            if (!string.IsNullOrEmpty(title) && x + 12f < hardRight)
            {
                float w = Mathf.Min(titleWidth, Mathf.Max(24f, hardRight - x - 4f));
                UIComponents.Label(new Rect(x, textY, w, 22f), UIComponents.TruncateToWidth(title, w, GameFont.Small),
                    GameFont.Small, UITheme.Accent);
                x += w + 6f;
            }

            // ⚠ 2026-09-19（要求一「社会特点不要额外组件，嵌入面板顶栏即可」）：
            //   这个视觉位**优先承载真实读数**（社会特点），其次才是契约徽章。
            //   两者**互斥**：有社会特点时**不再画徽章** —— 徽章是装饰（写死 `StatRow`），
            //   与数据抢同一块横向空间只会把数据挤成省略号。
            //   ⚠ 文本走 Tooltip：顶栏极窄，截断几乎必然发生（LAND：截断必须配补偿）。
            if (!string.IsNullOrEmpty(titleTraits))
            {
                float avail = Mathf.Max(16f, hardRight - (x + 6f));
                float w = Mathf.Min(MeasureWidth(titleTraits, GameFont.Tiny), avail);
                Rect traitsRect = new Rect(hardRight - w, textY, w, 22f);
                UIComponents.Label(traitsRect,
                    UIComponents.TruncateToWidth(titleTraits, w, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Info, TextAnchor.MiddleRight);
                TooltipHandler.TipRegion(traitsRect, titleTraits);
                hardRight = traitsRect.x - 6f;
            }
            else if (!string.IsNullOrEmpty(badge) && x + badgeWidth + 6f <= hardRight)
            {
                Rect badgeRect = new Rect(hardRight - badgeWidth, bar.y + (bar.height - BadgeHeight) * 0.5f,
                    badgeWidth, BadgeHeight);
                Badge(badgeRect, badge, UITheme.Info);
                hardRight = badgeRect.x - 6f;
            }

            if (!string.IsNullOrEmpty(stencil) && x + 16f < hardRight)
            {
                float w = Mathf.Min(MeasureWidth(stencil, GameFont.Tiny), Mathf.Max(16f, hardRight - x));
                UIComponents.Label(new Rect(x, textY, w, 22f), stencil, GameFont.Tiny, UITheme.Dim);
            }

            if (subWidth > 8f)
            {
                // ⚠ 2026-09-20（审计-派系列表 P2-C）：副标是**口径/降级声明**的载体（五面板共用）——
                //   截断后必须可悬停查全文（LAND：截断必须配补偿）。四参重载仅在真截断时挂 Tip。
                //   ⚠ 同日复审：title/stencil 仍为三参（未补偿）—— 属审计已登记项，不在本批范围。
                Rect subRect = new Rect(subX, textY, subWidth, 22f);
                UIComponents.Label(subRect,
                    UIComponents.TruncateToWidth(subText, subWidth, GameFont.Tiny, subRect),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleRight);
            }
        }

        /// <summary>`.bdg{font-size:9px;padding:1px 6px;border:1px solid currentColor;border-radius:99px}`。</summary>
        internal static void Badge(Rect rect, string text, Color color)
        {
            if (string.IsNullOrEmpty(text) || rect.width < 8f)
            {
                return;
            }
            UIComponents.Border(new Rect(rect.x, rect.y + 1f, rect.width, rect.height - 2f), color);
            UIComponents.Label(new Rect(rect.x + 3f, rect.y, rect.width - 6f, rect.height),
                UIComponents.TruncateToWidth(text, rect.width - 6f, GameFont.Tiny),
                GameFont.Tiny, color, TextAnchor.MiddleCenter);
        }

        /// <summary>`.bdg.solid{background:currentColor 15%}` —— 带底的徽章。</summary>
        internal static void BadgeSolid(Rect rect, string text, Color color)
        {
            if (string.IsNullOrEmpty(text) || rect.width < 8f)
            {
                return;
            }
            UIComponents.TintedBox(rect, UITheme.WithAlpha(color, 0.15f));
            UIComponents.Border(rect, color);
            UIComponents.Label(new Rect(rect.x + 3f, rect.y, rect.width - 6f, rect.height),
                UIComponents.TruncateToWidth(text, rect.width - 6f, GameFont.Tiny),
                GameFont.Tiny, color, TextAnchor.MiddleCenter);
        }

        /// <summary>按内容宽量一个 `.bdg` 徽章（clamp 到 <paramref name="maxWidth"/>）。</summary>
        internal static float BadgeWidth(string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }
            return Mathf.Min(MeasureWidth(text, GameFont.Tiny) + 12f, maxWidth);
        }

        /// <summary>`.sec-h`：3px info 竖标 + 标签（+ 右端注释）。返回新的 y。</summary>
        /// <param name="tip">
        /// 悬停详解（可空）。
        ///
        /// <para><b>⚠ 2026-09-19（审计 §2.2）</b>：段头此前**完全没有悬停** —— 而它是
        /// "口径注 / 数据级别徽章"的唯一载体（Society / Market / FactionDetail / Inventory 四个面板共用），
        /// 一旦被截断就**无处可读**（LAND：截断必须配补偿；全工程 43 处截断只有 7 处挂了悬停）。
        /// 本参数给调用方一个出口；另外**被截断的 label / note 原文也会自动并入**（见方法尾）。</para>
        /// </param>
        internal static float SectionHeader(Rect rect, float y, string label, string note, Color noteColor,
            string tip = null)
        {
            float textY = y + 1f;
            Widgets.DrawBoxSolid(new Rect(rect.x, y + (SectionHeaderHeight - 10f) * 0.5f, 3f, 10f), UITheme.Info);
            float x = rect.x + 3f + 6f;
            float noteWidth = string.IsNullOrEmpty(note)
                ? 0f
                : Mathf.Min(MeasureWidth(note, GameFont.Tiny), rect.width * 0.45f);
            float right = rect.xMax - noteWidth - (noteWidth > 0f ? 6f : 0f);
            string hover = tip;
            if (x + 8f < right)
            {
                float w = right - x;
                // 先量后画：**知道它会不会被截断**，才能在下面决定要不要把原文并入悬停。
                if (!string.IsNullOrEmpty(label) && MeasureWidth(label, GameFont.Tiny) > w)
                {
                    hover = MergeTip(hover, label);
                }
                UIComponents.Label(new Rect(x, textY, w, SectionHeaderHeight - 2f),
                    UIComponents.TruncateToWidth(label, w, GameFont.Tiny), GameFont.Tiny, UITheme.Muted);
            }
            if (noteWidth > 0f)
            {
                if (MeasureWidth(note, GameFont.Tiny) > noteWidth)
                {
                    hover = MergeTip(hover, note);
                }
                UIComponents.Label(new Rect(rect.xMax - noteWidth, textY, noteWidth, SectionHeaderHeight - 2f),
                    UIComponents.TruncateToWidth(note, noteWidth, GameFont.Tiny),
                    GameFont.Tiny, noteColor, TextAnchor.MiddleRight);
            }
            // ⚠ 挂在**整条段头矩形**上：它永不移动、也不会与别的元素抢热区（唯一的同层元素就是它自己）。
            if (!string.IsNullOrEmpty(hover))
            {
                TooltipHandler.TipRegion(new Rect(rect.x, y, rect.width, SectionHeaderHeight), hover);
            }
            return y + SectionHeaderHeight;
        }

        /// <summary>
        /// 把一段文本并入悬停串。**空项不留空行、不留孤立分隔符**
        /// （与本工程 `JoinMeta` 修过的那个缺陷同一纪律）。
        /// </summary>
        private static string MergeTip(string tip, string extra)
        {
            if (string.IsNullOrEmpty(extra))
            {
                return tip;
            }
            return string.IsNullOrEmpty(tip) ? extra : tip + "\n" + extra;
        }

        /// <summary>
        /// `.drow{display:flex;justify-content:space-between;padding:2.5px 0;
        /// border-bottom:1px dashed color-mix(--hair 75%)}`：左键 + 右值 + 底部细分隔。
        ///
        /// <para><b>⚠ 2026-09-19 修复</b>：原注释称"虚线分隔退化为细横线"，但实现里
        /// **一条线都没有画** —— 注释承诺了实现没做的事（典型注释漂移，审计项 A6）。
        /// 现按预览补上：引擎没有虚线，用 <see cref="UITheme.BorderHair"/> 的 1px 实线
        /// 表达同级弱分隔（这是 🅑 B 类降级，样式从虚线改实线，语义不变）。</para>
        /// </summary>
        internal static float DataRow(Rect rect, float y, string key, string value, Color valueColor,
            float keyWidthRatio)
        {
            float keyWidth = Mathf.Max(40f, rect.width * keyWidthRatio);
            float valueWidth = Mathf.Max(20f, rect.width - keyWidth - 6f);
            UIComponents.Label(new Rect(rect.x, y, keyWidth, RowHeight - 2f),
                UIComponents.TruncateToWidth(key, keyWidth, GameFont.Tiny), GameFont.Tiny, UITheme.Dim);
            UIComponents.Label(new Rect(rect.xMax - valueWidth, y, valueWidth, RowHeight - 2f),
                UIComponents.TruncateToWidth(value, valueWidth, GameFont.Tiny),
                GameFont.Tiny, valueColor, TextAnchor.MiddleRight);
            UIComponents.Rule(new Rect(rect.x, y + RowHeight - 1f, rect.width, 1f), UITheme.BorderHair);
            return y + RowHeight;
        }

        /// <summary>`.fnote`：说明块（虚线框；实线版 = <paramref name="solid"/>）。</summary>
        internal static float Note(Rect rect, float y, string text, int maxLines, bool solid, Color? fillOverride)
        {
            if (string.IsNullOrEmpty(text) || maxLines <= 0)
            {
                return y;
            }
            float line = Text.LineHeightOf(GameFont.Tiny);
            float height = line * maxLines + 10f;
            Rect box = new Rect(rect.x, y, rect.width, height);
            UIComponents.TintedBox(box, fillOverride ?? UITheme.WithAlpha(UITheme.Text, 0.03f));
            UIComponents.Border(box, solid ? UITheme.BorderSoft : UITheme.BorderHair);
            DrawWrapped(box, text, maxLines, UITheme.Muted);
            return y + height;
        }

        /// <summary>
        /// **按可用宽显式折行**（引擎无自动折行：<see cref="UIComponents.Label"/> 强制 WordWrap=false）。
        ///
        /// <para>行内按**二分**找「能放下的最长前缀」（宽度对前缀长度单调 ⇒ 二分合法）：
        /// 每行 O(log n) 次测量，而不是逐字累加的 O(n)。源文本里的 <c>\n</c> 强制断行。</para>
        /// </summary>
        internal static void DrawWrapped(Rect rect, string text, int maxLines, Color color)
        {
            if (string.IsNullOrEmpty(text) || maxLines <= 0)
            {
                return;
            }
            float line = Text.LineHeightOf(GameFont.Tiny);
            float inner = Mathf.Max(8f, rect.width - 16f);
            int start = 0;
            float y = rect.y + 5f;
            for (int li = 0; li < maxLines && start < text.Length; )
            {
                int newline = text.IndexOf('\n', start);
                int segEnd = newline >= 0 ? newline : text.Length;
                int take = FitChars(text, start, segEnd, inner);
                if (take <= 0)
                {
                    // 源文本里的空行：只推进游标，不占显示行（start 严格递增 ⇒ 不会死循环）。
                    if (newline >= 0)
                    {
                        start = newline + 1;
                        continue;
                    }
                    break;
                }
                if (y + line > rect.yMax + line)
                {
                    break;
                }
                UIComponents.Label(new Rect(rect.x + 8f, y, inner, line),
                    text.Substring(start, take), GameFont.Tiny, color);
                start += take;
                if (start >= segEnd && newline >= 0)
                {
                    start = newline + 1;
                }
                y += line;
                li++;
            }
        }

        /// <summary>在 <c>[start, segEnd)</c> 内能放进 <paramref name="width"/> 的最长字符数（二分）。</summary>
        private static int FitChars(string text, int start, int segEnd, float width)
        {
            int low = 0;
            int high = segEnd - start;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (MeasureWidth(text.Substring(start, mid), GameFont.Tiny) <= width)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }
            return low;
        }

        /// <summary>`.chi`：小卡（标题行 + 比例条 + 注释）。返回新的 y。</summary>
        internal static float ChainItem(Rect rect, float y, string name, string flowText, float bar01,
            string note, Color barColor)
        {
            float line = Text.LineHeightOf(GameFont.Tiny);
            float height = line * 2f + MiniBarHeight + 10f;
            Rect box = new Rect(rect.x, y, rect.width, height);
            UIComponents.TintedBox(box, UITheme.WithAlpha(UITheme.Text, 0.05f));
            UIComponents.Border(box, UITheme.BorderHair);

            float inner = box.width - 14f;
            float x = box.x + 7f;
            UIComponents.Label(new Rect(x, box.y + 3f, inner, line),
                UIComponents.TruncateToWidth(name, inner * 0.6f, GameFont.Tiny), GameFont.Tiny, UITheme.Text);
            UIComponents.Label(new Rect(x, box.y + 3f, inner, line),
                UIComponents.TruncateToWidth(flowText, inner * 0.38f, GameFont.Tiny),
                GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleRight);

            Rect bar = new Rect(x, box.y + 3f + line + 2f, inner, MiniBarHeight);
            // RUIF 合规（2026-09-21 审计整改）：原「自铺 10% 白底 + 自算宽度填充」⇒
            // 改走 `RUIF.ProgressAtoms.CapacityBar`（自铺轨道 + 按比例填充；used/cap 语义）。
            RUIF.ProgressAtoms.CapacityBar(bar, Mathf.Clamp01(bar01), 1f, barColor);
            UIComponents.Label(new Rect(x, bar.yMax + 1f, inner, line),
                UIComponents.TruncateToWidth(note, inner, GameFont.Tiny), GameFont.Tiny, UITheme.Dim);
            return y + height + 4f;
        }

        /// <summary>色调 → 颜色（与 <c>MasterControlTabWindow.ToneColor</c> 同源口径）。</summary>
        internal static Color ToneColor(SystemCenterTone tone)
        {
            switch (tone)
            {
                case SystemCenterTone.Good: return UITheme.Alive;
                case SystemCenterTone.Warn: return UITheme.Threat;
                case SystemCenterTone.Bad: return UITheme.Dead;
                default: return UITheme.Text;
            }
        }

        /// <summary>
        /// 语义色徽章/色点：`.bdg.ok/.info/.bad/.dim/.gold`。
        ///
        /// <para>⚠ 2026-09-20（审计-派系列表 P2-D 收口）：`Warn` 由 `UITheme.Warn`（Epic 纯红
        /// (1,0,0)）改 `UITheme.Threat`（琥珀，0.878/0.494/0.196）—— ① 与同文件
        /// <see cref="ToneColor"/> 对 Warn 的映射统一（同一语义两处给出不同色是分歧）；
        /// ② 与登记口径「Medieval 琥珀」一致（对照清单 §4-5）；③ 从此 Bad=深红(crimson)
        /// 与 Warn=琥珀 可辨（原先两个近红，削弱"一眼分组"）。
        /// 生产侧唯一 Warn 来源 = 派系列表色点（Medieval）；其余调用点生产数据为 Good/Bad/Flat。</para>
        /// </summary>
        internal static Color ToneColorOr(SystemCenterTone tone, Color flat)
        {
            switch (tone)
            {
                case SystemCenterTone.Good: return UITheme.Alive;
                case SystemCenterTone.Warn: return UITheme.Threat;
                case SystemCenterTone.Bad: return UITheme.Dead;
                default: return flat;
            }
        }

        /// <summary>文本宽度度量（唯一实现；与宿主 <c>MeasureWidth</c> 同式）。</summary>
        internal static float MeasureWidth(string text, GameFont font)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }
            GameFont previous = Text.Font;
            Text.Font = font;
            float width = Text.CalcSize(text).x;
            Text.Font = previous;
            return width;
        }
    }
}

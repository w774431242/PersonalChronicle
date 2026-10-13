using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive.UI
{
    /// <summary>
    /// **统一 UI 原子层**（CHG-20261003-FLOORDETAIL-UNIFY · 要求一）。
    ///
    /// <para><b>要解决的问题</b>：设备详情窗口 5 个页签的视觉此前是**逐处手绘**的
    /// （RUIF 覆盖率仅 2/28 ≈ 7%），同一语义（键值行 / 分区头 / 卡片底板）在 4 个文件里
    /// 各写一遍，于是同一条信息在不同页签长得不一样、改一处要改四处。</para>
    ///
    /// <para><b>做法</b>：RUIF 原子优先 + 手绘降级，全部收进本类。调用方只描述
    /// <b>「这一行是什么」</b>（<see cref="RowSpec"/>），不描述「怎么画」——
    /// 这样风格改动只需改本类一处，页签侧只补数据。</para>
    ///
    /// <para><b>RUIF 与降级的边界（诚实声明）</b>：RUIF 的 <c>PanelAtoms.StatRow</c>
    /// 只做 label + value + bar，其 <c>labelRatio / gap / barW</c> 是**内部常量不可配**
    /// ⇒ 需要叠加**头像 / 来源徽标**的行**不能**走 RUIF 分支（装饰会与 RUIF 内部布局打架），
    /// 本类据实回落手绘。这是能力边界，不是降级失败。</para>
    ///
    /// <para><b>测量与绘制同源</b>：<see cref="RowHeight"/> 是行高的<b>唯一真源</b>，
    /// 绘制与高度估算都读它（此前两处各写各的，靠注释纪律维持一致 —— 实机已多次出现
    /// 「估矮裁内容 / 估高留白」）。</para>
    /// </summary>
    internal static class UiAtoms
    {
        /// <summary>键值行行高（**唯一真源**：绘制与测量共用）。Tiny 字形 18 + 上下呼吸 4。</summary>
        internal const float RowHeight = 22f;

        /// <summary>列内统一左内边距（命中区同步内缩 ⇒ 命中范围 = 视觉范围）。</summary>
        internal const float RowPadX = 6f;

        /// <summary>标签列固定占比（固定比例 ⇒ 各行值列起点对齐；按标签实测宽浮动会参差）。</summary>
        internal const float LabelColumnRatio = 0.42f;

        /// <summary>一行键值行的完整描述（<b>值类型</b>：绘制期零分配）。</summary>
        internal struct RowSpec
        {
            /// <summary>左标签（暗色）。</summary>
            internal string Label;

            /// <summary>右值（语气色，右对齐）。</summary>
            internal string Value;

            /// <summary>值颜色；<c>null</c> = 用默认前景色。</summary>
            internal Color? ValueColor;

            /// <summary>占比条 0..1；<c>&lt; 0</c> = 不画条。</summary>
            internal float Bar01;

            /// <summary>条色；<c>null</c> = 与 <see cref="ValueColor"/> 同源（条色 = 值色）。</summary>
            internal Color? BarColor;

            /// <summary>来源徽标（API / LEDGER / SIM）；<c>&lt;= 0</c> = 不画。</summary>
            internal int SourceTag;

            /// <summary>要画头像的 Pawn（<c>null</c> = 不画）。</summary>
            internal Pawn Pawn;

            /// <summary>是否可点（整行热区）。<c>false</c> ⇒ 永不消费点击。</summary>
            internal bool Clickable;

            /// <summary>悬停提示；<c>null</c>/空 = 无 tip（截断必须有完整文本出口，LAND-008）。</summary>
            internal string Tip;
        }

        /// <summary>行高（供测量侧调用，避免直接依赖常量语义）。</summary>
        internal static float RowHeightPx
        {
            get { return RowHeight; }
        }
        /// <summary>
        /// 画一行键值行。返回 <c>true</c> = 本帧被点击（仅 <see cref="RowSpec.Clickable"/> 为真时可能）。
        /// </summary>
        /// <remarks>
        /// <b>RUIF 分支的判定条件是刻意收紧的</b>：只在「无头像 + 无来源徽标」时走
        /// <see cref="RuifBridge.TryStatRow"/>。因为 RUIF 该原子的列宽/间距是内部常量，
        /// 叠加装饰会重叠；此时据实回落手绘分支（能力边界，非失败）。
        /// </remarks>
        internal static bool StatRow(Rect rect, RowSpec spec, out bool clicked)
        {
            clicked = false;
            if (rect.width < 8f || rect.height < 4f)
            {
                return false;
            }

            bool simple = spec.Pawn == null && spec.SourceTag <= 0;
            if (simple && RuifBridge.TryStatRow(rect, spec.Label, spec.Value,
                    spec.Bar01 >= 0f ? (float?)spec.Bar01 : null,
                    spec.BarColor.HasValue ? spec.BarColor : (Color?)null))
            {
                DrawTip(rect, spec.Tip);
                return false;
            }

            // ---- 手绘分支（RUIF 不可用 / 本行需叠加装饰）----
            float contentX = rect.x + RowPadX;
            float contentWidth = rect.width - RowPadX;
            if (contentWidth <= 8f)
            {
                return false;
            }

            if (spec.Pawn != null)
            {
                float avatarSize = rect.height - 2f;
                PawnAvatar(new Rect(contentX, rect.y + 1f, avatarSize, avatarSize), spec.Pawn);
                contentX += avatarSize + 6f;
                contentWidth -= avatarSize + 6f;
            }

            // 徽标先量宽并从值列扣除（先扣后画 ⇒ 永不重叠）
            float badgeWidth = 0f;
            if (spec.SourceTag > 0)
            {
                string tag = SourceTagText(spec.SourceTag);
                badgeWidth = Mathf.Min(MeasureWidth(tag, GameFont.Tiny) + 10f,
                    rect.width * 0.32f);
            }

            float labelWidth = contentWidth * LabelColumnRatio;
            Rect labelRect = new Rect(contentX, rect.y, labelWidth, rect.height);
            UIComponents.Label(labelRect,
                UIComponents.TruncateToWidth(spec.Label ?? string.Empty, labelWidth, GameFont.Tiny,
                    labelRect),
                GameFont.Tiny, UITheme.Dim);

            float valueX = contentX + labelWidth + 6f;
            float valueWidth = contentWidth - labelWidth - 6f - (badgeWidth > 0f ? badgeWidth + 4f : 0f);

            if (spec.Bar01 >= 0f && valueWidth > 30f)
            {
                float barWidth = Mathf.Min(64f, valueWidth * 0.45f);
                Rect track = new Rect(valueX + valueWidth - barWidth,
                    rect.y + (rect.height - 8f) * 0.5f, barWidth, 8f);
                // 暗底 + 描边再填充：裸填充在暗卡上是一条突兀白杠（实机 2026-10-01 教训）。
                Widgets.DrawBoxSolid(track, UITheme.WithAlpha(UITheme.Window, 0.9f));
                UIComponents.Border(track, UITheme.BorderSoft);
                // 条色 = 值色（预览基准「条色 = 值色」）；1.6 的 DrawBoxSolid 以参数色绘制
                // 并重置 GUI.color ⇒ 必须显式传色，赋值 GUI.color 无效。
                Color barColor = spec.BarColor ?? spec.ValueColor ?? UITheme.Info;
                Widgets.DrawBoxSolid(new Rect(track.x + 1f, track.y + 1f,
                    (track.width - 2f) * Mathf.Clamp01(spec.Bar01), track.height - 2f), barColor);
                valueWidth -= barWidth + 6f;
            }

            if (valueWidth > 10f)
            {
                Rect valueRect = new Rect(valueX, rect.y, valueWidth, rect.height);
                UIComponents.Label(valueRect,
                    UIComponents.TruncateToWidth(spec.Value ?? string.Empty, valueWidth, GameFont.Tiny,
                        valueRect),
                    GameFont.Tiny, spec.ValueColor ?? UITheme.Text, TextAnchor.UpperRight);
            }

            if (badgeWidth > 0f)
            {
                UIComponents.Badge(new Rect(rect.xMax - badgeWidth, rect.y + 2f,
                    badgeWidth, rect.height - 4f), SourceTagText(spec.SourceTag),
                    SourceTagColor(spec.SourceTag));
            }

            if (spec.Clickable)
            {
                Rect rowRect = new Rect(rect.x + RowPadX, rect.y, rect.width - RowPadX, rect.height);
                if (Mouse.IsOver(rowRect))
                {
                    // ⚠ 1.6 的 DrawBoxSolid 会重置 GUI.color ⇒ 显式设色并恢复。
                    Color prev = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.06f);
                    Widgets.DrawBoxSolid(rowRect, Color.white);
                    GUI.color = prev;
                }
                clicked = Widgets.ButtonInvisible(rowRect);
            }

            DrawTip(rect, spec.Tip);
            return clicked;
        }
        /// <summary>
        /// 画分区头（标题 + 计数 + 折叠态）。RUIF 优先，失败回落手绘。
        /// </summary>
        /// <returns><c>true</c> = 本帧被点击（调用方据此翻转折叠并重测高度）。</returns>
        internal static bool SectionBar(Rect rect, string title, string count, bool expanded,
            out bool clicked)
        {
            clicked = false;
            if (rect.width < 8f || rect.height < 4f)
            {
                return false;
            }
            if (RuifBridge.TrySectionBar(rect, title, count, expanded))
            {
                return Widgets.ButtonInvisible(rect);
            }
            // 手绘回落：左侧竖条 + 标题 + 右端计数（形状与 RUIF 版本同构，避免两态观感割裂）。
            Color accent = UITheme.Accent;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + 2f, 2f, rect.height - 4f), accent);
            float countWidth = 0f;
            if (!string.IsNullOrEmpty(count))
            {
                countWidth = MeasureWidth(count, GameFont.Tiny) + 8f;
                UIComponents.Label(new Rect(rect.xMax - countWidth, rect.y, countWidth, rect.height),
                    count, GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleRight);
            }
            float titleWidth = rect.width - 12f - countWidth;
            if (titleWidth > 10f)
            {
                Rect titleRect = new Rect(rect.x + 8f, rect.y, titleWidth, rect.height);
                UIComponents.Label(titleRect,
                    UIComponents.TruncateToWidth(expanded ? "▾ " + title : "▸ " + title,
                        titleWidth, GameFont.Tiny, titleRect),
                    GameFont.Tiny, UITheme.Text, TextAnchor.MiddleLeft);
            }
            clicked = Widgets.ButtonInvisible(rect);
            return true;
        }

        // ---- 共享辅助（原先散在设备详情窗口里，属通用语义故上移到本层）----

        /// <summary>数据来源徽标文案（契约词，不翻译）。</summary>
        internal static string SourceTagText(int sourceKind)
        {
            switch (sourceKind)
            {
                case 1: return "API";
                case 2: return "LEDGER";
                case 3: return "SIM";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// 徽标配色：API=青（原版直读，最可信）· LEDGER=琥珀（本 mod 累计，口径需说明）·
        /// SIM=灰（原版无此口径，值必为「—」）。
        /// </summary>
        internal static Color SourceTagColor(int sourceKind)
        {
            switch (sourceKind)
            {
                case 1: return UITheme.Info;
                case 2: return UITheme.Accent;
                case 3: return UITheme.Dim;
                default: return UITheme.Muted;
            }
        }

        /// <summary>行语气色（1=正向 / 2=警示 / 3=负向 / 其它=普通）。</summary>
        internal static Color ToneColor(int tone)
        {
            switch (tone)
            {
                case 1: return UITheme.Alive;
                case 2: return UITheme.Threat;
                case 3: return UITheme.Dead;
                default: return UITheme.Text;
            }
        }

        /// <summary>文本像素宽（绘制期热路径 ⇒ 复用 <see cref="Text.CalcSize"/>）。</summary>
        internal static float MeasureWidth(string text, GameFont font)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }
            GameFont previous = Text.Font;
            Text.Font = font;
            float w = Text.CalcSize(text).x;
            Text.Font = previous;
            return w;
        }

        /// <summary>
        /// Pawn 小头像（原版 <c>PortraitsCache</c> 立绘）。<paramref name="pawn"/> 由<b>调用方解析</b>传入
        /// （<c>WorkshopPawnAccess</c> 属上层 Automation，共享 UI 层不得反向依赖）。
        /// 找不到 / 尺寸不足 / 抛异常 ⇒ <b>不画且不抛</b>（单件异常隔离铁律）。
        /// </summary>
        internal static void PawnAvatar(Rect rect, Pawn pawn)
        {
            if (pawn == null || rect.width < 8f)
            {
                return;
            }
            try
            {
                RenderTexture portrait = PortraitsCache.Get(pawn,
                    new Vector2(rect.width, rect.height), Rot4.South,
                    Vector3.zero, 1f, true, true, true, true, null, null, false, null);
                if (portrait == null)
                {
                    return;
                }
                // 立绘底为浅色 ⇒ 暗色界面上是一块突兀的白：先铺卡片同色暗底再画，外描弱边框压边。
                Widgets.DrawBoxSolid(rect, UITheme.Window);
                Widgets.DrawTextureFitted(rect, portrait, 1f);
                UIComponents.Border(rect, UITheme.BorderSoft);
            }
            catch (Exception)
            {
                // 头像失败不得影响整页；不写日志以免每帧刷屏。
            }
        }

        private static void DrawTip(Rect rect, string tip)
        {
            if (!string.IsNullOrEmpty(tip) && Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, tip);
            }
        }
    }
}
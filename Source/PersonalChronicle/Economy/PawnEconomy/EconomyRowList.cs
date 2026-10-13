using System;
using System.Globalization;
using PersonalChronicle.Application.PawnEconomy;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// 行列表的**会话态**（滚动 / 分页 / 测量缓存）—— 由窗口实例持有，**不入存档**。
    /// P0-6（2026-10-10）：行族的实现唯一在 <see cref="EconomyRowList"/>；本类只存状态，
    /// 因此两个窗口（聚合窗口与柜台工作台）能共用同一份测量与绘制。
    /// </summary>
    internal sealed class EconomyRowListState
    {
        public Vector2 Scroll;
        public int Page;
        public int MeasuredPage = -99999;
        public EconomyView MeasuredView;
        public float MeasuredWidth;
        public float[] Heights;
        public string[] WrappedRows;
        public float[] RowButtonWidths;
        public float ContentHeight;
        public bool HasDataRows;
        /// <summary>切页签 / 切机构时复位（等价于旧实现里逐字段清零的那一行）。</summary>
        public void Reset()
        {
            Scroll = Vector2.zero; Page = 0; MeasuredPage = -99999; MeasuredView = null;
        }
    }

    /// <summary>
    /// 通用行族（机构 / 岗位 / 库存 / 账户 / 台账 / 报表，以及柜台工作台的明细区）——
    /// **测量、绘制、分页、空态的唯一实现**。
    ///
    /// <para><b>为什么要有它（P0-6）</b>：柜台工作台窗口若自带一套行卡绘制，就会出现
    /// 「同一元素两处实现」——违反 Engineering Standard 的同类元素同一 helper 纪律，且几何必然分叉。
    /// 现在两个窗口都调这里；窗口之间的差异**只经回调注入**（自定义行高 / 自定义行绘制 / 行尾按钮），
    /// 本类内不出现任何窗口或机构类型特判。</para>
    ///
    /// <para><b>零状态</b>：所有会话态由调用方的 <see cref="EconomyRowListState"/> 持有。</para>
    /// </summary>
    internal static class EconomyRowList
    {
        /// <summary>每页行数上界（库存 / 台账等无界列表的分页；测量与绘制成本都有上界）。</summary>
        public const int PageSize = 100;
        /// <summary>空态块最小高度 —— 空页按视口高度铺开，不是"顶上一小块 + 大片留白"。</summary>
        public const float EmptyStateBlockHeight = 160f;
        /// <summary>卡片在滚动内容区的左右外缩进（与机构卡模板一致）。</summary>
        public const float CardSideInset = 6f;
        /// <summary>卡片之间的垂直间距（机构卡与通用行卡共用）。</summary>
        public const float CardStackGap = 6f;

        /// <summary>按可用宽度显式折行（唯一实现：委托 <c>NeedDashboardTextFlow</c>；放宽为 public 供同域窗口复用）。</summary>
        public static string Flow(string value, float width, GameFont font)
            => NeedDashboardTextFlow.Wrap(value, Math.Max(1f, width - 2f), s => RUIF.Measure.Width(s, font));

        /// <summary>RowHead 规则：按钮宽 = max(文案实测宽 + SpaceMd×2, SpaceLg×4)。</summary>
        public static float RowButtonWidth(string label)
            => Mathf.Max(RUIF.Measure.Width(label, GameFont.Small) + RUIF.Tokens.SpaceMd * 2f, RUIF.Tokens.SpaceLg * 4f);

        /// <summary>行尾按钮的**色调规则（唯一来源）**：破坏性动词（终止 / 提取 / 取消）用危险色调，
        /// 其余用主操作色调。CHG-20261010-ECON-AGG-MIGRATE 批次 B-2：由聚合窗口迁入行族 ——
        /// 柜台工作台现在也要给承载页（账户）挂按钮，规则必须在**同一处**定义，
        /// 否则两个宿主会各调一套（同一动词两种颜色是最典型的双真值劣化）。</summary>
        public static RUIF.ButtonTone RowButtonTone(string verb)
            => verb == "Terminate" || verb == "Withdraw" || verb == "Cancel" || verb == "Revoke"
                ? RUIF.ButtonTone.Danger : RUIF.ButtonTone.Default;

        public static int PageCount(EconomyView view)
            => Mathf.Max(1, (view.Rows.Count + PageSize - 1) / PageSize);

        /// <summary>
        /// 测量当前页。缓存键 = view 引用 + 内容宽 + 当前页。
        /// <paramref name="customRowHeight"/> 返回 &gt; 0 时该行按该高度绘制（例如机构卡片），返回 0 表示通用行。
        /// 卡高一律派生：文本块 = <c>RowHeight(Small)</c> 与实测折行高的较大者；卡高 = 文本块 + <c>CardPadY×2</c>，
        /// 且不低于 <c>ControlHeight + CardPadY×2</c>（保证按钮放得下）。说明行沿用既有行高参数（最小 40、文本下边距 12）。
        /// </summary>
        public static void Measure(EconomyView view, float rectWidth, EconomyRowListState state,
            Func<EconomyRow, float> customRowHeight)
        {
            if (ReferenceEquals(state.MeasuredView, view) && Math.Abs(state.MeasuredWidth - rectWidth) < 0.01f
                && state.MeasuredPage == state.Page) return;
            state.MeasuredView = view; state.MeasuredWidth = rectWidth; state.MeasuredPage = state.Page;
            int pageStart = state.Page * PageSize;
            int pageRows = Mathf.Min(PageSize, view.Rows.Count - pageStart);
            state.Heights = new float[pageRows]; state.WrappedRows = new string[pageRows]; state.RowButtonWidths = new float[pageRows];
            state.ContentHeight = 8f;
            float contentW = Mathf.Max(1f, rectWidth - RUIF.Tokens.ScrollbarThickness);
            float cardW = Mathf.Max(1f, contentW - CardSideInset * 2f);
            float innerW = Mathf.Max(1f, cardW - RUIF.Tokens.CardPadX * 2f);
            GameFont previous = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                state.HasDataRows = false;
                for (int i = 0; i < state.Heights.Length; i++)
                {
                    var row = view.Rows[pageStart + i];
                    if (row.Verb != EconomyWindowLayout.HintRowVerb) state.HasDataRows = true;
                    float custom = customRowHeight != null ? customRowHeight(row) : 0f;
                    if (custom > 0f)
                    {
                        state.WrappedRows[i] = row.Text; state.RowButtonWidths[i] = 0f;
                        state.Heights[i] = custom;
                        state.ContentHeight += state.Heights[i] + CardStackGap; continue;
                    }
                    // 说明行保持纯文本（次要色），沿用既有行高参数（最小 40、文本下边距 12）。
                    if (row.Verb == EconomyWindowLayout.HintRowVerb)
                    {
                        state.WrappedRows[i] = Flow(row.Text, cardW, GameFont.Small);
                        state.Heights[i] = Mathf.Max(40f, Text.CalcHeight(state.WrappedRows[i], cardW) + 12f);
                        state.RowButtonWidths[i] = 0f;
                        state.ContentHeight += state.Heights[i]; continue;
                    }
                    // 数据行：按钮宽由文案精算（RowHead 规则），文本宽 = 卡内宽 − 按钮宽 − SpaceXs（先测后算）。
                    float buttonWidth = string.IsNullOrEmpty(row.Action) ? 0f : RowButtonWidth(row.Action);
                    state.RowButtonWidths[i] = buttonWidth;
                    float textWidth = buttonWidth > 0f ? Mathf.Max(1f, innerW - buttonWidth - RUIF.Tokens.SpaceXs) : innerW;
                    state.WrappedRows[i] = Flow(row.Text, textWidth, GameFont.Small);
                    float textHeight = Mathf.Max(RUIF.Measure.RowHeight(GameFont.Small), Text.CalcHeight(state.WrappedRows[i], textWidth));
                    state.Heights[i] = textHeight + RUIF.Tokens.CardPadY * 2f;
                    if (buttonWidth > 0f)
                        state.Heights[i] = Mathf.Max(state.Heights[i], RUIF.Measure.ControlHeight(GameFont.Small) + RUIF.Tokens.CardPadY * 2f);
                    state.ContentHeight += state.Heights[i] + CardStackGap;
                }
            }
            finally { Text.Font = previous; }
        }

        /// <summary>
        /// 绘制行族。<paramref name="bodyArea"/> 是**未扣分页栏**的可用区（本方法内部按页数扣出真实 body）。
        /// 返回 <c>true</c> = 本帧应中断（发生了翻页、自定义行命中或行尾按钮命中）—— 调用方应立即返回，
        /// 下一帧以新状态重新测量与绘制。
        /// </summary>
        /// <param name="drawCustomRow">自定义行绘制（返回 true = 命中并已改变状态）；传 null 表示无自定义行。</param>
        /// <param name="drawRowAction">行尾按钮绘制（返回 true = 命中）；传 null 表示该窗口的行不挂按钮（只读）。</param>
        public static bool Draw(Rect bodyArea, EconomyView view, EconomyRowListState state,
            Func<EconomyRow, float> customRowHeight,
            Func<Rect, float, EconomyRow, bool> drawCustomRow,
            Func<Rect, EconomyRow, bool> drawRowAction)
        {
            if (view == null) return false;
            int pageCount = PageCount(view);
            if (state.Page >= pageCount) state.Page = pageCount - 1;
            if (state.Page < 0) state.Page = 0;
            bool paged = pageCount > 1;
            float pageBarHeight = paged ? RUIF.Measure.ControlHeight(GameFont.Small) + RUIF.Tokens.SpaceXs : 0f;
            var body = new Rect(bodyArea.x, bodyArea.y, bodyArea.width, Mathf.Max(1f, bodyArea.height - pageBarHeight));
            // 翻页点击先于测量处理：本帧提前返回，下一帧以新页重新测量与绘制（避免"旧测量 / 新页"错位）。
            if (paged && DrawPagingBar(body, pageCount, state)) return true;
            Measure(view, body.width, state, customRowHeight);
            // 零行**或**本页只有说明行（无数据行）都不是"一张白纸" —— 走既有空态原子说明本页签当前无数据。
            bool empty = EconomyWindowLayout.ShowsEmptyState(view.Rows.Count) || !state.HasDataRows;
            float emptyBlock = empty ? Mathf.Max(EmptyStateBlockHeight, body.height - RUIF.Tokens.SpaceLg) : 0f;
            // 空态块 + 其后间距都要计入滚动内容高，否则最后一条说明行的下沿会被裁掉几像素。
            float extent = Mathf.Max(body.height, state.ContentHeight + (empty ? emptyBlock + CardStackGap : 0f));
            Rect content = RUIF.Layout.BeginScroll(body, extent, ref state.Scroll, false);
            // 滚动入栈/出栈**必须成对**：任一行绘制抛异常都会逃出 DoWindowContents，留下未出栈的 GUIClip，
            // 日志刷 "You are pushing more GUIClips than popping…" ⇒ 整局 GUI 卡死。异常只记一次。
            bool interrupted = false;
            try
            {
                float y = 4f;
                if (empty)
                {
                    RUIF.FeedbackAtoms.EmptyState(new Rect(content.x + CardSideInset, y, content.width - CardSideInset * 2f, emptyBlock),
                        EconomyLabels.TranslatedOrId(EconomyWindowLayout.EmptyStateTitleKey, "Empty"),
                        EconomyLabels.TranslatedOrId(EconomyWindowLayout.EmptyStateHintKey, "EmptyHint"));
                    y += emptyBlock + CardStackGap;
                }
                int pageStart = state.Page * PageSize;
                for (int i = 0; i < state.Heights.Length && !interrupted; i++)
                {
                    var row = view.Rows[pageStart + i];
                    float height = state.Heights[i];
                    if (y + height < state.Scroll.y || y > state.Scroll.y + body.height) { y += height + CardStackGap; continue; }
                    float custom = customRowHeight != null ? customRowHeight(row) : 0f;
                    if (custom > 0f)
                    {
                        if (drawCustomRow != null && drawCustomRow(content, y, row)) { interrupted = true; break; }
                        y += height + CardStackGap; continue;
                    }
                    // 说明性长句保持为次要色文本行（分区说明，不是列表项），不与数据行同卡。
                    if (row.Verb == EconomyWindowLayout.HintRowVerb)
                    {
                        var hintRect = new Rect(content.x + CardSideInset, y, content.width - CardSideInset * 2f, height - 6f);
                        RUIF.Draw.Label(hintRect, state.WrappedRows[i], GameFont.Small, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
                        if (Mouse.IsOver(hintRect)) TooltipHandler.TipRegion(hintRect, row.Text);
                        y += height; continue;
                    }
                    var card = new Rect(content.x + CardSideInset, y, content.width - CardSideInset * 2f, height);
                    bool hovered = Mouse.IsOver(card);
                    var inner = EconomyUiKit.CardFrame(card, hovered, null);
                    float buttonWidth = state.RowButtonWidths[i];
                    if (buttonWidth > 0f && drawRowAction != null)
                    {
                        var buttonRect = new Rect(card.xMax - RUIF.Tokens.CardPadX - buttonWidth,
                            card.y + (height - RUIF.Measure.ControlHeight(GameFont.Small)) * 0.5f,
                            buttonWidth, RUIF.Measure.ControlHeight(GameFont.Small));
                        if (drawRowAction(buttonRect, row)) { interrupted = true; break; }
                    }
                    float textWidth = buttonWidth > 0f ? inner.width - buttonWidth - RUIF.Tokens.SpaceXs : inner.width;
                    RUIF.Draw.Label(new Rect(inner.x, inner.y, textWidth, inner.height), state.WrappedRows[i],
                        GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
                    if (hovered) TooltipHandler.TipRegion(card, row.Text);
                    y += height + CardStackGap;
                }
            }
            catch (Exception error) { Log.ErrorOnce("[PC][UI] economy rows failed: " + error, 0x1EC0DE2); }
            finally { RUIF.Layout.EndScroll(); }
            RUIF.MiscAtoms.ThinScrollBar(body, extent, state.Scroll.y);
            return interrupted;
        }

        /// <summary>分页栏（只在行数超出单页时出现）。页码用数字与斜杠表达 —— 无语言依赖、不新增键。
        /// 返回 true = 本帧发生了翻页，调用方应立即返回（下一帧以新页测量）。</summary>
        private static bool DrawPagingBar(Rect body, int pageCount, EconomyRowListState state)
        {
            float size = RUIF.Measure.ControlHeight(GameFont.Small);
            float y = body.yMax + RUIF.Tokens.SpaceXs;
            var prev = new Rect(body.x + CardSideInset, y, size, size);
            var next = new Rect(prev.xMax + RUIF.Tokens.SpaceXs, y, size, size);
            bool changed = false;
            if (RUIF.ButtonAtoms.IconButton(prev, "<", state.Page > 0)) { state.Page--; changed = true; }
            if (RUIF.ButtonAtoms.IconButton(next, ">", state.Page < pageCount - 1)) { state.Page++; changed = true; }
            float labelX = next.xMax + RUIF.Tokens.SpaceSm;
            RUIF.Draw.Label(new Rect(labelX, y, Mathf.Max(0f, body.xMax - labelX), size),
                (state.Page + 1).ToString(CultureInfo.InvariantCulture) + " / " + pageCount.ToString(CultureInfo.InvariantCulture),
                GameFont.Small, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            if (!changed) return false;
            state.Page = Mathf.Clamp(state.Page, 0, pageCount - 1);
            state.Scroll = Vector2.zero;
            return true;
        }
    }
}

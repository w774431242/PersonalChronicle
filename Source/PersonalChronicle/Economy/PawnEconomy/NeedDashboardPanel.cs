using System;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>需求面板形态：名册（经济窗口，含分页选人）或单人（Pawn 检视窗「个人需求」）。</summary>
    internal enum NeedPanelMode
    {
        Roster,
        Single
    }

    /// <summary>
    /// 宿主无关的需求面板状态（测量缓存 / 滚动 / 选择）。
    /// 由宿主持有实例（窗口字段或 ITab 实例字段）；严禁 static、Scribe 持久化、
    /// 写入 Def/Pawn 对象或跨存档复用（GATE-UI：UI State 不持持久状态）。
    /// </summary>
    internal sealed class NeedPanelState
    {
        public NeedDashboardView View;
        public NeedDashboardView MeasuredView;
        public object Language;
        public int Tick = -1;
        public bool MapAvailable;
        public string SelectedId;
        public int Page;
        public Vector2 Scroll = Vector2.zero;
        public bool JumpToDetails;
        public float MeasuredWidth, Height, RowHeight, RosterRowHeight, BannerHeight, SelectionHeight, CoverageHeight;
        public float[] DetailHeights, CardHeights;
        public float[][] CardCellHeights;
        public string[] Headers, CardHeaders, RosterLabels;
        public string[][] Cells, CardCells;
        public NeedTextRow[] MeasuredFacts;
        public string LimitsText, CoverageText, SelectionText;
        /// <summary>「个人需求」需求循环面板的测量缓存；与名册面板字段分开，避免互相失效。</summary>
        public NeedsCycleMeasure Cycle;
        /// <summary>「个人需求」当前页签索引（会话态，不落盘；0=需求 1=决策 2=观测明细）。</summary>
        public int CycleTab;

        /// <summary>选择/翻页后调用：宿主下一帧据此重新查询（面板自身不取数）。</summary>
        public void Invalidate()
        {
            View = null;
            MeasuredView = null;
            Cycle = null;
        }

        /// <summary>切换宿主对象（换 Pawn / 换 runtime）时调用：滚动与选择一并复位。</summary>
        public void ResetForNewSubject()
        {
            Scroll = Vector2.zero;
            Page = 0;
            JumpToDetails = false;
            CycleTab = 0;
            Invalidate();
        }
    }

    /// <summary>
    /// 需求观察内容层（宿主无关）：名册与单人两模式共用同一测量与绘制核心。
    /// 只消费 <see cref="NeedDashboardView"/>；不查询数据、不持有宿引用、不调用
    /// legacy query/command 或原始 Widgets；全部绘制走 RUIF 原子。
    /// 由 <c>Dialog_PawnEconomy.Needs.cs</c> 与经济「需求观察」页共用。
    /// </summary>
    internal static class NeedDashboardPanel
    {
        private const float Gap = 8f, TableMinimumWidth = 760f, CardMinimumWidth = 420f, ReadOnlyHeight = 34f;
        private const int TableColumns = 3, CardColumns = 2;
        private static readonly float[] Shares = { .13f, .16f, .12f, .12f, .18f, .12f, .17f };

        /// <summary>整键回落（P0-4 同一纪律，唯一实现 <see cref="EconomyLabels.TranslatedOrId"/>）：
        /// 缺键显示标识本身，绝不把 <c>PC_PEA.Needs.*</c> 裸键打上屏。</summary>
        private static string N(string suffix) => EconomyLabels.TranslatedOrId("PC_PEA.Needs." + suffix, suffix);

        private static int ColumnCount(float width)
            => width >= TableMinimumWidth ? TableColumns : width >= CardMinimumWidth ? CardColumns : 1;

        /// <summary>
        /// 绘制整个需求页正文（ReadOnly 与 Limits 横幅、覆盖行、名册/分页、选中横幅、需求表与事实明细）。
        /// <paramref name="body"/> 为内容区（其左上角即绘制原点，面板内部所有坐标相对它）。
        /// </summary>
        public static void Draw(Rect body, NeedDashboardView view, NeedPanelMode mode, NeedPanelState state)
        {
            if (view == null || state == null)
            {
                return;
            }

            float width = Math.Max(1f, body.width - RUIF.Tokens.ScrollbarThickness);
            // 只读合同横幅固定在滚动区之外（两种宿主一致），滚动区自其下方开始。
            RUIF.Draw.Label(new Rect(body.x, body.y, body.width, ReadOnlyHeight), N("ReadOnly"), GameFont.Small, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            Rect scrollBody = new Rect(body.x, body.y + ReadOnlyHeight + Gap, body.width, Math.Max(0f, body.height - ReadOnlyHeight - Gap));
            if (scrollBody.height <= 0f)
            {
                return;
            }
            bool oldWrap = Text.WordWrap;
            Text.WordWrap = true;
            try
            {
                Measure(state, view, width);
                int columns = ColumnCount(width);
                float rosterHeight = mode == NeedPanelMode.Roster
                    ? (float)Math.Ceiling(view.Roster.Count / (double)columns) * (state.RosterRowHeight + Gap)
                    : 0f;
                float toolbar = mode == NeedPanelMode.Roster ? RUIF.Measure.ControlHeight(GameFont.Small) : 0f;
                float detailsY = state.BannerHeight + state.CoverageHeight + toolbar + rosterHeight + Gap * 4;
                if (state.JumpToDetails)
                {
                    state.Scroll.y = detailsY;
                    state.JumpToDetails = false;
                }
                RUIF.Layout.BeginScroll(scrollBody, state.Height, ref state.Scroll);
                try
                {
                    DrawContent(scrollBody, width, columns, detailsY, mode, state);
                }
                finally
                {
                    RUIF.Layout.EndScroll();
                }
            }
            finally
            {
                Text.WordWrap = oldWrap;
            }
        }

        private static void DrawContent(Rect body, float width, int columns, float detailsY, NeedPanelMode mode, NeedPanelState state)
        {
            // 滚动内容坐标系：自视口原点 (0,0) 起算，不得混入外层绝对坐标
            // （见 RUIF.Layout.BeginScroll 契约与「个人需求」右栏的实机修复）。
            float x = 0f;
            float y = 0f;
            RUIF.FeedbackAtoms.AlertBanner(new Rect(x, y, width, state.BannerHeight), RUIF.AlertPriority.Normal, state.LimitsText);
            y += state.BannerHeight + Gap;
            RUIF.Draw.Label(new Rect(x, y, width, state.CoverageHeight), state.CoverageText, GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            y += state.CoverageHeight + Gap;
            if (mode == NeedPanelMode.Roster)
            {
                float toolbar = RUIF.Measure.ControlHeight(GameFont.Small), buttonWidth = Math.Min(150, width * .25f);
                if (RUIF.ButtonAtoms.PrimaryButton(new Rect(x, y, buttonWidth, toolbar), N("Previous"), state.Page > 0))
                {
                    state.Page--;
                    state.Scroll = Vector2.zero;
                    state.Invalidate();
                    return;
                }
                if (RUIF.ButtonAtoms.PrimaryButton(new Rect(x + width - buttonWidth, y, buttonWidth, toolbar), N("Next"), state.Page + 1 < state.View.Snapshot.PageCount))
                {
                    state.Page++;
                    state.Scroll = Vector2.zero;
                    state.Invalidate();
                    return;
                }
                RUIF.Draw.Label(new Rect(x + buttonWidth, y, width - buttonWidth * 2, toolbar),
                    string.Format(N("Page"), state.Page + 1, state.View.Snapshot.PageCount), GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleCenter);
                y += toolbar + Gap;
                float cellWidth = (width - Gap * (columns - 1)) / columns;
                for (int i = 0; i < state.View.Roster.Count; i++)
                {
                    var person = state.View.Roster[i];
                    if (RUIF.ButtonAtoms.PrimaryButton(new Rect(x + i % columns * (cellWidth + Gap), y + i / columns * (state.RosterRowHeight + Gap), cellWidth, state.RosterRowHeight), state.RosterLabels[i]))
                    {
                        state.SelectedId = person.Id;
                        state.JumpToDetails = true;
                        state.Scroll = Vector2.zero;
                        state.Invalidate();
                        return;
                    }
                }
            }
            y = detailsY;
            RUIF.FeedbackAtoms.AlertBanner(new Rect(x, y, width, state.SelectionHeight),
                state.View.Snapshot.Selected?.Facts.RequiredGroupsUsable == true ? RUIF.AlertPriority.Normal : RUIF.AlertPriority.Warn,
                state.SelectionText);
            y += state.SelectionHeight + Gap;
            if (state.View.Needs.Count == 0)
            {
                RUIF.FeedbackAtoms.EmptyState(new Rect(x, y, width, 100), N("Empty"), N("EmptyHint"));
                y += 100;
            }
            else if (width >= TableMinimumWidth)
            {
                float height = (state.Cells.Length + 1) * state.RowHeight;
                RUIF.DataAtoms.StatTable(new Rect(x, y, width, height), state.Headers, state.Cells, Shares, state.RowHeight);
                y += height;
            }
            else
            {
                for (int i = 0; i < state.Cells.Length; i++)
                {
                    float h = state.CardCellHeights[i][0];
                    RUIF.Draw.Label(new Rect(x, y, width, h), state.CardCells[i][0], GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
                    y += h;
                    for (int c = 1; c < state.Headers.Length; c++)
                    {
                        h = state.CardCellHeights[i][c];
                        RUIF.PanelAtoms.StatRow(new Rect(x, y, width, h), state.CardHeaders[c], state.CardCells[i][c]);
                        y += h;
                    }
                    y += Gap;
                }
            }
            y += Gap;
            for (int i = 0; i < state.View.Facts.Count; i++)
            {
                RUIF.PanelAtoms.StatRow(new Rect(x, y, width, state.DetailHeights[i]), state.MeasuredFacts[i].Label, state.MeasuredFacts[i].Value);
                y += state.DetailHeights[i];
            }
        }

        /// <summary>
        /// <see cref="Wrapped"/> 返回值中**含有的行后间距**（= 本类的 <c>Gap</c>）。
        /// 放宽为 internal 供同域面板复用：需要"只画文本块"时从实测高里扣掉它即可，
        /// **不得就地镜像 8f**（那会让测量与绘制的口径两处维护）。
        /// </summary>
        internal const float RowSpacing = Gap;

        /// <summary>折行后的实测高度 + 间距。放宽为 internal 供同域面板复用（只读工具，不改行为）。</summary>
        internal static float Wrapped(string value, float width, GameFont font)
        {
            GameFont previous = Text.Font;
            try
            {
                Text.Font = font;
                return Math.Max(RUIF.Measure.RowHeight(font), Text.CalcHeight(value ?? "", Math.Max(1, width))) + Gap;
            }
            finally
            {
                Text.Font = previous;
            }
        }

        private static float StatHeight(string label, string value, float width)
            => Math.Max(Wrapped(label, width * .55f - RUIF.Tokens.SpaceXs, RUIF.Tokens.FontLabel),
                Wrapped(value, width * .45f - RUIF.Tokens.SpaceXs, RUIF.Tokens.FontLabel));

        /// <summary>按可用宽度显式折行。放宽为 internal 供同域面板复用（只读工具，不改行为）。</summary>
        internal static string Flow(string value, float width, GameFont font)
            => NeedDashboardTextFlow.Wrap(value, Math.Max(1, width - 2), s => RUIF.Measure.Width(s, font));

        private static void Measure(NeedPanelState state, NeedDashboardView view, float width)
        {
            if (state.MeasuredView == view && Math.Abs(state.MeasuredWidth - width) < .5f)
            {
                return;
            }
            state.MeasuredView = view;
            state.MeasuredWidth = width;
            var rawHeaders = new[] { N("Column.Need"), N("Column.Current"), N("Column.Target"), N("Column.Gap"), N("Column.Source"), N("Column.Age"), N("Column.Status") };
            state.Headers = new string[rawHeaders.Length];
            state.CardHeaders = new string[rawHeaders.Length];
            state.LimitsText = Flow(N("Limits"), width - 90, RUIF.Tokens.FontLabel);
            state.CoverageText = Flow(view.Coverage, width, GameFont.Small);
            state.SelectionText = Flow(view.Selection + " · " + view.SelectionState, width - 90, RUIF.Tokens.FontLabel);
            state.BannerHeight = Wrapped(state.LimitsText, width - 90, RUIF.Tokens.FontLabel);
            state.CoverageHeight = Wrapped(state.CoverageText, width, GameFont.Small);
            state.SelectionHeight = Wrapped(state.SelectionText, width - 90, RUIF.Tokens.FontLabel);
            state.RosterRowHeight = RUIF.Measure.ControlHeight(GameFont.Small);
            int columns = ColumnCount(width);
            state.RosterLabels = new string[view.Roster.Count];
            float rosterTextWidth = (width - Gap * (columns - 1)) / columns - RUIF.ButtonFmt.FullWidthPad;
            for (int i = 0; i < state.RosterLabels.Length; i++)
            {
                state.RosterLabels[i] = Flow(view.Roster[i].Text, rosterTextWidth, GameFont.Small);
                state.RosterRowHeight = Math.Max(state.RosterRowHeight, Wrapped(state.RosterLabels[i], rosterTextWidth, GameFont.Small));
            }
            state.RowHeight = RUIF.Measure.RowHeight(RUIF.Tokens.FontLabel) + Gap;
            state.Cells = new string[view.Needs.Count][];
            state.CardCells = new string[view.Needs.Count][];
            float totalShares = 0;
            foreach (float share in Shares)
            {
                totalShares += share;
            }
            for (int i = 0; i < state.Headers.Length; i++)
            {
                state.Headers[i] = Flow(rawHeaders[i], width * Shares[i] / totalShares, RUIF.Tokens.FontLabel);
                state.CardHeaders[i] = Flow(rawHeaders[i], width * .55f - RUIF.Tokens.SpaceXs, RUIF.Tokens.FontLabel);
                state.RowHeight = Math.Max(state.RowHeight, Wrapped(state.Headers[i], width * Shares[i] / totalShares, RUIF.Tokens.FontLabel));
            }
            state.CardHeights = new float[view.Needs.Count];
            state.CardCellHeights = new float[view.Needs.Count][];
            for (int i = 0; i < view.Needs.Count; i++)
            {
                var row = view.Needs[i];
                var rawCells = new[] { row.Name, row.Current, row.Target, row.Gap, row.Source, row.Age, row.Status };
                state.Cells[i] = new string[rawCells.Length];
                state.CardCells[i] = new string[rawCells.Length];
                state.CardCells[i][0] = Flow(row.Name, width, GameFont.Small);
                state.CardHeights[i] = Wrapped(state.CardCells[i][0], width, GameFont.Small);
                state.CardCellHeights[i] = new float[state.Headers.Length];
                state.CardCellHeights[i][0] = state.CardHeights[i];
                for (int c = 0; c < state.Headers.Length; c++)
                {
                    state.Cells[i][c] = Flow(rawCells[c], width * Shares[c] / totalShares, RUIF.Tokens.FontLabel);
                    state.RowHeight = Math.Max(state.RowHeight, Wrapped(state.Cells[i][c], width * Shares[c] / totalShares, RUIF.Tokens.FontLabel));
                    if (c != 0)
                    {
                        state.CardCells[i][c] = Flow(rawCells[c], width * .45f - RUIF.Tokens.SpaceXs, RUIF.Tokens.FontLabel);
                        state.CardCellHeights[i][c] = StatHeight(state.CardHeaders[c], state.CardCells[i][c], width);
                        state.CardHeights[i] += state.CardCellHeights[i][c];
                    }
                }
            }
            state.DetailHeights = new float[view.Facts.Count];
            float factHeight = 0;
            state.MeasuredFacts = new NeedTextRow[view.Facts.Count];
            for (int i = 0; i < state.DetailHeights.Length; i++)
            {
                state.MeasuredFacts[i] = new NeedTextRow(Flow(view.Facts[i].Label, width * .55f - RUIF.Tokens.SpaceXs, RUIF.Tokens.FontLabel),
                    Flow(view.Facts[i].Value, width * .45f - RUIF.Tokens.SpaceXs, RUIF.Tokens.FontLabel));
                state.DetailHeights[i] = StatHeight(state.MeasuredFacts[i].Label, state.MeasuredFacts[i].Value, width);
                factHeight += state.DetailHeights[i];
            }
            float tableHeight = (view.Needs.Count + 1) * state.RowHeight;
            if (width < TableMinimumWidth)
            {
                tableHeight = 0;
                foreach (float height in state.CardHeights)
                {
                    tableHeight += height + Gap;
                }
            }
            if (view.Needs.Count == 0)
            {
                tableHeight = 100;
            }
            state.Height = state.BannerHeight + state.CoverageHeight + RUIF.Measure.ControlHeight(GameFont.Small)
                + (float)Math.Ceiling(view.Roster.Count / (double)columns) * (state.RosterRowHeight + Gap)
                + state.SelectionHeight + tableHeight + factHeight + Gap * 8;
        }
    }
}

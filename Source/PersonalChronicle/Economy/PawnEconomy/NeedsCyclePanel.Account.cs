using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>One measured layout serves both positioning and painting; no ledger reads or formatting during paint.</summary>
    internal sealed class NeedAccountMeasure
    {
        public readonly List<NeedAccountText> Text = new List<NeedAccountText>();
        public readonly List<NeedAccountCell> Cells = new List<NeedAccountCell>();
        public Rect Rule, Bar, Details;
        public string DetailsLabel, AccountTip, BudgetTip;
        public Rect MoneyRegion, BudgetRegion, DecisionRegion;
        public string DecisionTip;
        public float Coverage = -1f;
        public float Height;
        public bool WithJump;
    }

    internal sealed class NeedAccountText
    {
        public Rect Rect;
        public string Text;
        public GameFont Font;
        public Color Color;
        public TextAnchor Anchor;
    }

    internal sealed class NeedAccountCell
    {
        public Rect Rect;
        public string Tip;
        public string ControlName;
        public Color Color;
        public bool Focused;
    }

    internal static partial class NeedsCyclePanel
    {
        // Four tiles only when each has a useful width; narrow hosts use 2 x 2.
        private const float AccountFourCycleWidth = 480f;

        private static float AccountTextHeight(string value, float width, GameFont font)
            => NeedDashboardPanel.Wrapped(value, Mathf.Max(1f, width), font) - NeedDashboardPanel.RowSpacing;

        private static float AddAccountText(NeedAccountMeasure layout, string value, float x, float y,
            float width, GameFont font, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            string flow = NeedDashboardPanel.Flow(value ?? string.Empty, Mathf.Max(1f, width), font);
            float height = AccountTextHeight(flow, width, font);
            layout.Text.Add(new NeedAccountText { Rect = new Rect(x, y, Mathf.Max(1f, width), height),
                Text = flow, Font = font, Color = color, Anchor = anchor });
            return height;
        }

        private static float AddAccountPair(NeedAccountMeasure layout, string left, string right, float y,
            float width, float leftShare, GameFont font, Color leftColor, Color rightColor)
        {
            float leftWidth = Mathf.Max(1f, width * leftShare - SpaceXs);
            float rightX = width * leftShare;
            float rightWidth = Mathf.Max(1f, width - rightX);
            return Math.Max(AddAccountText(layout, left, 0f, y, leftWidth, font, leftColor),
                AddAccountText(layout, right, rightX, y, rightWidth, font, rightColor, TextAnchor.UpperRight));
        }

        private static void MeasureAccount(NeedsCycleMeasure m, NeedDashboardView view, float inner, bool withJump)
        {
            m.Account = view.Account;
            if (m.Account == null) { return; }
            var account = m.Account;
            var model = account.Model;
            var layout = new NeedAccountMeasure { WithJump = withJump, AccountTip = account.Tip,
                BudgetTip = account.BudgetTip, DetailsLabel = N("Cycle.Account.DetailsShort") };
            m.AccountCard = layout;
            float y = InnerGap;
            layout.Rule = new Rect(0f, y, inner, RUIF.Tokens.RuleHeight);
            y += RUIF.Tokens.RuleHeight + InnerGap;
            y += AddAccountPair(layout, N("Cycle.Account.Title"), account.IncomeState, y, inner,
                .4f, RUIF.Tokens.FontLabel, RUIF.Tokens.Text, ToneColor(account.IncomeTone)) + InnerGap;

            float moneyStart = layout.Rule.yMax + InnerGap;
            string available = account.Available ?? account.StateText;
            // The main number never shares its rect with other figures. Long units/large amounts wrap intact.
            y += AddAccountText(layout, available, 0f, y, inner, GameFont.Medium,
                account.Available != null ? RUIF.Tokens.Text : ToneColor(account.Tone)) + InnerGap;
            if (account.Available != null)
            {
                string totals = string.Format(N("Cycle.Account.Totals"), account.Balance, account.Reserved);
                y += AddAccountPair(layout, N("Cycle.Account.AvailableShort"), totals, y, inner,
                    .25f, RUIF.Tokens.FontLabel, RUIF.Tokens.Dim, RUIF.Tokens.Muted) + InnerGap;
            }
            layout.MoneyRegion = new Rect(0f, moneyStart, inner, y - moneyStart);
            float budgetStart = y;
            string coverage = model != null ? model.CoverageText
                : string.Format(N("Cycle.Account.ReserveSummary"), account.ReserveTarget, account.IncomeGap);
            y += AddAccountText(layout, coverage, 0f, y, inner, RUIF.Tokens.FontLabel, RUIF.Tokens.Muted) + InnerGap;
            if (model != null)
            {
                y += AddAccountText(layout, model.ThresholdText, 0f, y, inner,
                    RUIF.Tokens.FontLabel, RUIF.Tokens.Dim) + InnerGap;
                layout.Coverage = model.CoverageRatio;
                if (layout.Coverage >= 0f)
                {
                    layout.Bar = new Rect(0f, y, inner, BarHeight * .5f);
                    y += layout.Bar.height + InnerGap;
                }
            }
            layout.BudgetRegion = new Rect(0f, budgetStart, inner, y - budgetStart);

            var cycles = model != null ? model.Cycles : view.Cycles;
            int columns = inner >= AccountFourCycleWidth ? 4 : inner >= AccountTwoColumnWidth ? 2 : 1;
            float cellWidth = Mathf.Max(1f, (inner - SpaceXs * (columns - 1)) / columns);
            for (int first = 0; first < cycles.Count; first += columns)
            {
                float rowHeight = 0f;
                int last = Math.Min(first + columns, cycles.Count);
                for (int index = first; index < last; index++)
                {
                    var cycle = cycles[index];
                    float cellX = (index - first) * (cellWidth + SpaceXs);
                    float textX = cellX + SpaceXs;
                    float textWidth = Mathf.Max(1f, cellWidth - SpaceXs * 2f);
                    float nameHeight = AddAccountText(layout, cycle.Name, textX, y + InnerGap,
                        textWidth, RUIF.Tokens.FontLabel, RUIF.Tokens.Muted);
                    Color color = AccountCycleColor(cycle);
                    float statusHeight = AddAccountText(layout, cycle.State, textX, y + InnerGap + nameHeight,
                        textWidth, RUIF.Tokens.FontLabel, color);
                    float height = nameHeight + statusHeight + InnerGap * 2f;
                    rowHeight = Math.Max(rowHeight, height);
                    layout.Cells.Add(new NeedAccountCell { Rect = new Rect(cellX, y, cellWidth, height),
                        Tip = cycle.Detail, Color = color, Focused = cycle.Focused,
                        ControlName = "PC_NeedsCycle_" + cycle.Id });
                }
                // Equal tile heights in the row. All text retains its measured height.
                for (int index = layout.Cells.Count - (last - first); index < layout.Cells.Count; index++)
                {
                    var cell = layout.Cells[index];
                    cell.Rect = new Rect(cell.Rect.x, cell.Rect.y, cell.Rect.width, rowHeight);
                }
                y += rowHeight + InnerGap;
            }
            if (model != null)
            {
                float decisionStart = y;
                // Snapshot facts are measured and painted with the same wrapping rules as the account.
                // Visible in the primary card; the decision link retains the bounded history and full reasons.
                for (int index = 0; index < model.LaborRows.Count; index++)
                {
                    var row = model.LaborRows[index];
                    y += AddAccountText(layout, row.Label + " · " + row.Value, 0f, y, inner,
                        RUIF.Tokens.FontLabel, index == 0 ? RUIF.Tokens.Info : RUIF.Tokens.Muted) + InnerGap;
                }
                y += AddAccountPair(layout, string.Format(N("Cycle.Account.DecisionValue"), model.DecisionLabel),
                    string.Format(N("Cycle.Account.FocusValue"), model.Target), y, inner, .65f,
                    RUIF.Tokens.FontLabel, RUIF.Tokens.Info, RUIF.Tokens.Muted) + InnerGap;
                y += AddAccountText(layout, string.Format(N("Cycle.Account.ReasonValue"), model.Reason),
                    0f, y, inner, RUIF.Tokens.FontLabel, RUIF.Tokens.Muted) + InnerGap;
                float linkWidth = withJump ? Mathf.Min(inner * .3f,
                    RUIF.Measure.Width(layout.DetailsLabel, RUIF.Tokens.FontLabel) + SpaceXs * 2f) : 0f;
                float nextWidth = Mathf.Max(1f, inner - (withJump ? linkWidth + SpaceXs : 0f));
                float nextHeight = AddAccountText(layout, string.Format(N("Cycle.Account.NextValue"), model.Next),
                    0f, y, nextWidth, RUIF.Tokens.FontLabel, RUIF.Tokens.Dim);
                if (withJump)
                {
                    layout.Details = new Rect(inner - linkWidth, y, linkWidth,
                        Math.Max(nextHeight, RUIF.Measure.ControlHeight(RUIF.Tokens.FontLabel)));
                    nextHeight = layout.Details.height;
                }
                y += nextHeight;
                layout.DecisionRegion = new Rect(0f, decisionStart, inner, y - decisionStart);
                layout.DecisionTip = string.Format(N("Cycle.Account.ExecutionValue"), model.ExecutionLabel);
            }
            else if (withJump)
            {
                layout.Details = new Rect(inner - Mathf.Min(AccountButtonMaxWidth, inner), y,
                    Mathf.Min(AccountButtonMaxWidth, inner), RUIF.Measure.ControlHeight(RUIF.Tokens.FontLabel));
                y += layout.Details.height;
            }
            layout.Height = y + InnerGap;
            m.AccountHeight = layout.Height;
        }

        private static Color AccountCycleColor(NeedCycleLine cycle)
        {
            if (cycle.Stale) { return RUIF.Tokens.Dim; }
            switch (cycle.Status)
            {
                case NeedCycleStatus.Feasible:
                case NeedCycleStatus.Executing: return RUIF.Tokens.Info;
                case NeedCycleStatus.Satisfied: return RUIF.Tokens.Alive;
                case NeedCycleStatus.WaitingFacts:
                case NeedCycleStatus.Pending: return RUIF.Tokens.Warn;
                case NeedCycleStatus.Blocked: return ToneColor(cycle.Tone);
                default: return RUIF.Tokens.Dim;
            }
        }

        private static Rect AccountRect(Rect local, float left, float top)
            => new Rect(left + local.x, top + local.y, local.width, local.height);

        private static void DrawAccount(float left, float y, float inner, NeedsCycleMeasure m, NeedPanelState state)
        {
            var layout = m.AccountCard;
            if (layout == null) { return; }
            RUIF.Draw.LineH(left + layout.Rule.x, y + layout.Rule.y, layout.Rule.width, RUIF.Tokens.BorderSoft);
            if (layout.Bar.height > 0f)
            {
                RUIF.ProgressAtoms.CapacityBar(AccountRect(layout.Bar, left, y),
                    Mathf.Clamp01(layout.Coverage), 1f, RUIF.Tokens.Info);
            }
            for (int index = 0; index < layout.Cells.Count; index++)
            {
                var cell = layout.Cells[index];
                Rect rect = AccountRect(cell.Rect, left, y);
                if (layout.WithJump)
                {
                    if (RUIF.ButtonAtoms.PrimaryButton(rect, string.Empty, controlName: cell.ControlName))
                    { JumpToDecision(state); }
                }
                else { RUIF.Shapes.InsetPanel(rect); }
                if (cell.Focused)
                { RUIF.Draw.Box(rect, RUIF.Draw.WithAlpha(RUIF.Tokens.Info, .08f)); }
                RUIF.Draw.Box(new Rect(rect.x, rect.y + InnerGap,
                    cell.Focused ? RUIF.Tokens.RuleHeight * 2f : RUIF.Tokens.RuleHeight,
                    Mathf.Max(1f, rect.height - InnerGap * 2f)), cell.Color);
                if (!string.IsNullOrEmpty(cell.Tip)) { TooltipHandler.TipRegion(rect, cell.Tip); }
            }
            for (int index = 0; index < layout.Text.Count; index++)
            {
                var text = layout.Text[index];
                RUIF.Draw.Label(AccountRect(text.Rect, left, y), text.Text, text.Font, text.Color, text.Anchor);
            }
            if (!string.IsNullOrEmpty(layout.AccountTip))
            { TooltipHandler.TipRegion(AccountRect(layout.MoneyRegion, left, y), layout.AccountTip); }
            if (!string.IsNullOrEmpty(layout.BudgetTip))
            { TooltipHandler.TipRegion(AccountRect(layout.BudgetRegion, left, y), layout.BudgetTip); }
            if (!string.IsNullOrEmpty(layout.DecisionTip))
            { TooltipHandler.TipRegion(AccountRect(layout.DecisionRegion, left, y), layout.DecisionTip); }
            if (layout.WithJump && layout.Details.height > 0f
                && RUIF.ButtonAtoms.PrimaryButton(AccountRect(layout.Details, left, y), layout.DetailsLabel))
            { JumpToDecision(state); }
        }
    }
}

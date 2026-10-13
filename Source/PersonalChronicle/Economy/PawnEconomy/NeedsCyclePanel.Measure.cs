using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// 「个人需求」页的测量缓存（宿主无关）。
    /// 由 <see cref="NeedPanelState"/> 持有实例，随宿主销毁；严禁 static（GATE-UI：UI State 不持持久状态）。
    /// 所有文本在测量期已折行（WordWrap 作用域内），绘制期只做定位与画。
    /// </summary>
    internal sealed class NeedsCycleMeasure
    {
        public NeedDashboardView View;
        public float Width;
        public bool TinyFontSupported;
        /// <summary>测量时的页签；与 <see cref="NeedPanelState.CycleTab"/> 不一致时重建。</summary>
        public int Tab;

        public string[] TabLabels;
        public float SectionHeight;
        public float RowHeight;
        public bool IsEmpty;
        /// <summary>本页任何一条需求带优先序时，名称列左内缩以容纳芯片（列头与数据行同源）。</summary>
        public bool HasAnyChip;

        // 概览卡（需求页签）
        public float OverviewRatio;
        public string OverviewSummary;
        public string MoodLabel, MoodText;
        public Color MoodColor;
        public Color OverviewColor;
        public float OverviewHeight;

        // Account layout is measured once from the immutable presenter model.
        public NeedAccountView Account;
        public NeedAccountMeasure AccountCard;
        public float AccountHeight;

        // ① 需求页签：原版可见需求的「短期满足度 × 长期目标」对照
        public string[] ShownNames;
        public string[] ShownPercents;
        public string[] ShownTargets;
        public int[] ShownPriorities;
        public bool[] ShownActive;
        public float[] ShownRatios;
        public Color[] ShownColors;
        public string HeaderName, HeaderPercent, HeaderLongTarget, HeaderTarget, HeaderGap;
        public float GapWidth;

        // ①b 需求页签：原版隐藏需求（showOnNeedList=false），单独成区
        public string[] HiddenNames;
        public string[] HiddenPercents;
        public string[] HiddenTargets;
        public int[] HiddenPriorities;
        public bool[] HiddenActive;
        public float[] HiddenRatios;
        public Color[] HiddenColors;

        // ①c 需求页签：长期生活规划卡（政策参数**逐项一行**；可归一者带条）
        public NeedPolicyView Policy;
        public string[] PolicyLabels;
        public string[] PolicyValues;
        public float[] PolicyBars;
        public Color[] PolicyBarColors;
        public float[] PolicyRowHeights;
        public float PolicyHeight;

        // ② 目标页签：每条需求一张卡（双阈值 + 优先级 + 活跃 + 缺口三口径）
        public NeedGoalLine[] Goals;
        public string[] GoalThresholds;
        public string[] GoalDeadlines;
        /// <summary>v0.4 §9 等待加权（该目标已连续等待时长）；本周期才开始等待（0）时为空串，不出行。</summary>
        public string[] GoalWaits;
        public string[] GoalGaps;
        public Color[] GoalColors;
        public float[] GoalHeights;
        public string GoalCountText;

        // ③ 决策页签：四循环 / 仲裁 / 预算 / 候选岗位 / 状态条件 / 观测明细
        public NeedCycleLine[] Cycles;
        public string[] CycleNames;
        public string[] CycleStates;
        public string[] CycleDetails;
        public Color[] CycleColors;
        public float[] CycleHeights;
        public NeedTextRow[] Planning;
        public float[] PlanningHeights;
        public NeedBudgetView Budget;
        public string[] BudgetLabels;
        public string[] BudgetValues;
        public float[] BudgetFieldHeights;
        public string BudgetAllocText;
        public float BudgetAllocHeight;
        public NeedTextRow[] Opportunities;
        public float[] OpportunityHeights;
        public NeedTextRow[] Status;
        public float[] StatusHeights;
        public NeedTextRow[] Details;
        public float[] DetailHeights;
        public NeedTextRow[] AccountModelRows;
        public float[] AccountModelHeights;

        // ③b 决策页签：劳动与合同 / 照护与服务（真实账本事实，与影子计划互补）
        public NeedEmploymentView Employment;
        public string EmploymentState;
        public Color EmploymentColor;
        public float EmploymentStateHeight;
        public NeedTextRow[] EmploymentRows;
        public float[] EmploymentHeights;
        public NeedCareView Care;
        public string CareState;
        public Color CareColor;
        public float CareStateHeight;
        public NeedTextRow[] CareRows;
        public float[] CareHeights;

        public float DemandHeight, GoalHeight, DecisionHeight;
        public float Height;
    }

    internal static partial class NeedsCyclePanel
    {
        private static void Measured(NeedsCycleMeasure m, NeedDashboardView view, float width, int tab)
        {
            m.TabLabels = new[] { N("Cycle.Tab.Demand"), N("Cycle.Tab.Goal"), N("Cycle.Tab.Decision") };
            m.SectionHeight = RUIF.Tokens.SectionTitleHeight;
            m.IsEmpty = view.Needs.Count == 0;
            m.GapWidth = width >= NarrowWidth ? GapColumnWidth : 0f;
            m.HeaderName = N("Column.Need");
            m.HeaderPercent = N("Label.Satisfaction");
            m.HeaderTarget = N("Column.Target");
            m.HeaderGap = N("Label.Gap");
            m.HeaderLongTarget = N("Column.LongTarget");

            float inner = Mathf.Max(1f, width - CardPadX * 2f);
            float nameWidth = NameWidth - SpaceXs;
            float percentWidth = PercentWidth - SpaceXs;

            // ── ① 需求页签 ──────────────────────────────────────────────
            var shownNames = new List<string>(); var shownPercents = new List<string>();
            var shownTargets = new List<string>(); var shownRatios = new List<float>();
            var shownColors = new List<Color>();
            var shownPriorities = new List<int>(); var shownActive = new List<bool>();
            var hiddenNames = new List<string>(); var hiddenPercents = new List<string>();
            var hiddenTargets = new List<string>(); var hiddenRatios = new List<float>();
            var hiddenColors = new List<Color>();
            var hiddenPriorities = new List<int>(); var hiddenActive = new List<bool>();
            var plannedNames = new List<string>();
            var unplanned = new List<string>();
            float ratioSum = 0f; float maxCellHeight = 0f; int ratioCount = 0;

            foreach (var line in view.Needs)
            {
                string name = NeedDashboardPanel.Flow(line.Name, nameWidth - ChipWidth, GameFont.Small);
                // Wrapped() 返回值已含 +Gap，故此处减去后只加一次 InnerGap（避免双重补偿）。
                maxCellHeight = Math.Max(maxCellHeight, Math.Max(0f, NeedDashboardPanel.Wrapped(name, nameWidth - ChipWidth, GameFont.Small) - Gap));
                // 满足度列折行兜底：状态文本（如 "Unsupported"）或未来更长的状态键都不会被硬切。
                string percent = NeedDashboardPanel.Flow(PercentText(line), percentWidth, GameFont.Small);
                maxCellHeight = Math.Max(maxCellHeight, Math.Max(0f, NeedDashboardPanel.Wrapped(percent, percentWidth, GameFont.Small) - Gap));
                // 长期目标列：直接复用 presenter 给出的长期目标文本，表现层不重算政策。
                string target = NeedDashboardPanel.Flow(line.Target, TargetWidth - SpaceXs, RUIF.Tokens.FontLabel);
                Color color = RatioColor(line.Ratio01);
                if (line.Hidden)
                {
                    hiddenNames.Add(name); hiddenPercents.Add(percent); hiddenTargets.Add(target);
                    hiddenRatios.Add(line.Ratio01); hiddenColors.Add(color);
                    hiddenPriorities.Add(line.Priority); hiddenActive.Add(line.Active);
                    continue;
                }
                if (line.Ratio01 >= 0f) { ratioSum += line.Ratio01; ratioCount++; }
                shownNames.Add(name); shownPercents.Add(percent); shownTargets.Add(target);
                shownRatios.Add(line.Ratio01); shownColors.Add(color);
                shownPriorities.Add(line.Priority); shownActive.Add(line.Active);
                if (line.Planned) { plannedNames.Add(line.Name); } else { unplanned.Add(line.Name); }
            }
            m.ShownNames = shownNames.ToArray(); m.ShownPercents = shownPercents.ToArray();
            m.ShownTargets = shownTargets.ToArray(); m.ShownRatios = shownRatios.ToArray();
            m.ShownColors = shownColors.ToArray(); m.ShownPriorities = shownPriorities.ToArray();
            m.ShownActive = shownActive.ToArray();
            m.HiddenNames = hiddenNames.ToArray(); m.HiddenPercents = hiddenPercents.ToArray();
            m.HiddenTargets = hiddenTargets.ToArray(); m.HiddenRatios = hiddenRatios.ToArray();
            m.HiddenColors = hiddenColors.ToArray(); m.HiddenPriorities = hiddenPriorities.ToArray();
            m.HiddenActive = hiddenActive.ToArray();
            m.HasAnyChip = AnyChip(shownPriorities) || AnyChip(hiddenPriorities);
            m.OverviewRatio = ratioCount == 0 ? -1f : ratioSum / ratioCount;
            m.OverviewColor = RatioColor(m.OverviewRatio);
            m.OverviewSummary = string.Format(N("Cycle.Summary"), plannedNames.Count, unplanned.Count);
            if (view.HiddenCount > 0)
            {
                // 采集上限截断必须告知，否则"清单看起来完整"是误导。
                m.OverviewSummary += " · " + string.Format(N("Cycle.Hidden"), view.HiddenCount);
            }
            m.MoodLabel = N("Cycle.Overview.Mood");
            m.MoodText = view.Mood.TryGetValue(out float mood) ? Mathf.RoundToInt(mood * 100f) + "%" : N("Fact." + view.Mood.State);
            m.MoodColor = view.Mood.TryGetValue(out float moodValue) ? RatioColor(moodValue) : RUIF.Tokens.Dim;
            m.Status = MeasureRows(view.Status, width, out m.StatusHeights);
            m.Policy = view.Policy;
            if (m.Policy != null)
            {
                // 政策卡逐项一行：工时 / 闲暇 / 保障期 / 储蓄倾向 / 风格依据。
                var labels = new[] { N("Planning.Policy.WorkHours"), N("Planning.Policy.Leisure"),
                    N("Planning.Policy.Reserve"), N("Planning.Policy.Saving"), N("Planning.Sources") };
                var values = new[] { m.Policy.WorkHours, m.Policy.Leisure, m.Policy.Reserve,
                    m.Policy.Saving, m.Policy.Sources };
                // 可归一者带条（< 0 = 不给条）：保障期与风格依据无自然上界。
                var bars = new[] { m.Policy.WorkHours01, m.Policy.Leisure01, -1f, m.Policy.Saving01, -1f };
                m.PolicyLabels = new string[bars.Length];
                m.PolicyValues = new string[bars.Length];
                m.PolicyBars = new float[bars.Length];
                m.PolicyBarColors = new Color[bars.Length];
                m.PolicyRowHeights = new float[bars.Length];
                float labelWidth = width * ValueShare - SpaceXs;
                float valueWidth = Mathf.Max(1f, width - width * ValueShare - SpaceXs);
                float policyTotal = CardPadY * 2f;
                for (int i = 0; i < bars.Length; i++)
                {
                    m.PolicyLabels[i] = NeedDashboardPanel.Flow(labels[i], labelWidth, RUIF.Tokens.FontLabel);
                    m.PolicyValues[i] = NeedDashboardPanel.Flow(values[i], valueWidth, RUIF.Tokens.FontLabel);
                    m.PolicyBars[i] = bars[i];
                    m.PolicyBarColors[i] = bars[i] >= 0f ? RUIF.Tokens.Info : RUIF.Tokens.Muted;
                    m.PolicyRowHeights[i] = Math.Max(m.RowHeight, Math.Max(
                        NeedDashboardPanel.Wrapped(m.PolicyLabels[i], labelWidth, RUIF.Tokens.FontLabel),
                        NeedDashboardPanel.Wrapped(m.PolicyValues[i], valueWidth, RUIF.Tokens.FontLabel)));
                    policyTotal += m.PolicyRowHeights[i];
                }
                m.PolicyHeight = policyTotal;
            }
            m.RowHeight = Math.Max(RUIF.Measure.RowHeight(GameFont.Small), maxCellHeight) + InnerGap;
            // 「个人账户」摘要区（CB-ECON-002）：**必须先测并计入概览卡高** —— 否则绘制内容超出测量高，
            // 滚动总高不足会把账户底部裁掉。空态下不画页签栏 ⇒ 跳转按钮同步抑制（与绘制侧同源）。
            MeasureAccount(m, view, inner, !m.IsEmpty);
            m.OverviewHeight = CardPadY * 2f + RUIF.Measure.RowHeight(GameFont.Small) * 2f + InnerGap * 2f + BarHeight * 1.5f
                + m.AccountHeight;

            float demand = m.OverviewHeight + Gap;
            if (shownNames.Count > 0) { demand += m.SectionHeight + m.RowHeight * (shownNames.Count + 1) + Gap; }
            // +1 = 列头行（本区新增列头，以对齐需求页签的短期 × 长期对照）。
            if (hiddenNames.Count > 0) { demand += m.SectionHeight + m.RowHeight * (hiddenNames.Count + 1) + Gap; }
            if (m.Status.Length > 0) { demand += m.SectionHeight + Sum(m.StatusHeights) + Gap; }
            if (m.Policy != null) { demand += m.SectionHeight + m.PolicyHeight; }
            m.DemandHeight = demand;

            // ── ② 目标页签：每条需求一张卡 ───────────────────────────────
            int goalCount = view.Goals.Count;
            m.Goals = new NeedGoalLine[goalCount];
            m.GoalThresholds = new string[goalCount];
            m.GoalDeadlines = new string[goalCount];
            m.GoalWaits = new string[goalCount];
            m.GoalGaps = new string[goalCount];
            m.GoalColors = new Color[goalCount];
            m.GoalHeights = new float[goalCount];
            m.GoalCountText = string.Format(N("Cycle.PlannedCount"), goalCount, view.Needs.Count);
            float goalTotal = 0f;
            for (int i = 0; i < goalCount; i++)
            {
                var goal = view.Goals[i];
                m.Goals[i] = goal;
                m.GoalColors[i] = RatioColor(goal.Ratio01);
                m.GoalThresholds[i] = NeedDashboardPanel.Flow(goal.Thresholds, inner - SpaceXs, RUIF.Tokens.FontLabel);
                m.GoalDeadlines[i] = NeedDashboardPanel.Flow(goal.Deadline ?? string.Empty, inner - SpaceXs, RUIF.Tokens.FontLabel);
                // v0.4 §9 等待加权：等得越久越该先办 —— 此前该时长只参与 Domain 排序、界面零显示，
                // 玩家看不到"这条需求被挤占多久了"。0 = 本周期刚开始等待，不出行（避免噪声）。
                m.GoalWaits[i] = goal.WaitingTicks > 0
                    ? NeedDashboardPanel.Flow(string.Format(N("Cycle.Waiting"), goal.WaitingTicks, goal.WaitCapTicks), inner - SpaceXs, RUIF.Tokens.FontLabel)
                    : string.Empty;
                string gaps = N("Cycle.Gap.Relative") + " " + goal.RelativeGap + "  ·  "
                    + N("Cycle.Gap.Absolute") + " " + goal.AbsoluteGap + "  ·  "
                    + N("Cycle.Gap.Raw") + " " + goal.RawGap;
                m.GoalGaps[i] = NeedDashboardPanel.Flow(gaps, inner - SpaceXs, RUIF.Tokens.FontLabel);
                // 卡高 = 内边距 + 标题行 + 条 + 四行文本（各行 Wrapped 已含 +Gap）。
                // 等待行**按结构化条件参与高度**（WaitingTicks > 0 与绘制侧同源）：空串同样会被 Wrapped 计入
                // 一行（≥ RowHeight + Gap），无条件相加会让卡底留白、滚动总高虚高（GATE-UI P2-UI-2）。
                m.GoalHeights[i] = CardPadY * 2f + RUIF.Measure.RowHeight(GameFont.Small) + InnerGap
                    + BarHeight + InnerGap
                    + NeedDashboardPanel.Wrapped(m.GoalThresholds[i], inner - SpaceXs, RUIF.Tokens.FontLabel)
                    + NeedDashboardPanel.Wrapped(m.GoalDeadlines[i], inner - SpaceXs, RUIF.Tokens.FontLabel)
                    + (goal.WaitingTicks > 0 ? NeedDashboardPanel.Wrapped(m.GoalWaits[i], inner - SpaceXs, RUIF.Tokens.FontLabel) : 0f)
                    + NeedDashboardPanel.Wrapped(m.GoalGaps[i], inner - SpaceXs, RUIF.Tokens.FontLabel);
                goalTotal += m.GoalHeights[i] + Gap;
            }
            m.GoalHeight = Math.Max(goalTotal, EmptyHeight);

            // ── ③ 决策页签 ──────────────────────────────────────────────
            int cycleCount = view.Cycles.Count;
            m.Cycles = new NeedCycleLine[cycleCount];
            m.CycleNames = new string[cycleCount];
            m.CycleStates = new string[cycleCount];
            m.CycleDetails = new string[cycleCount];
            m.CycleColors = new Color[cycleCount];
            m.CycleHeights = new float[cycleCount];
            float cycleTotal = 0f;
            float cycleNameW = 96f, cycleStateW = 84f;
            float cycleDetailW = Mathf.Max(1f, width - FactDotSize - SpaceXs - cycleNameW - cycleStateW);
            for (int i = 0; i < cycleCount; i++)
            {
                var cycle = view.Cycles[i];
                m.Cycles[i] = cycle;
                m.CycleNames[i] = NeedDashboardPanel.Flow(cycle.Name, cycleNameW, RUIF.Tokens.FontLabel);
                m.CycleStates[i] = NeedDashboardPanel.Flow(cycle.State, cycleStateW, RUIF.Tokens.FontLabel);
                m.CycleDetails[i] = NeedDashboardPanel.Flow(cycle.Detail, cycleDetailW, RUIF.Tokens.FontLabel);
                m.CycleColors[i] = ToneColor(cycle.Tone);
                m.CycleHeights[i] = Math.Max(m.RowHeight, Math.Max(
                    NeedDashboardPanel.Wrapped(m.CycleNames[i], cycleNameW, RUIF.Tokens.FontLabel),
                    Math.Max(NeedDashboardPanel.Wrapped(m.CycleStates[i], cycleStateW, RUIF.Tokens.FontLabel),
                        NeedDashboardPanel.Wrapped(m.CycleDetails[i], cycleDetailW, RUIF.Tokens.FontLabel))));
                cycleTotal += m.CycleHeights[i];
            }
            // 真实账本事实（只读，装配点已取好）：状态行 + 明细行，结构与其它分区同族。
            m.Employment = view.Employment;
            if (m.Employment != null)
            {
                // 状态行可用宽 = 行宽 −（色点 + 间距），与绘制侧同源（GATE-UI P2-UI-1：两侧口径差 4px 会在绘制期二次折行）。
                float stateWidth = width - FactDotSize - SpaceXs;
                m.EmploymentState = NeedDashboardPanel.Flow(m.Employment.State, stateWidth, RUIF.Tokens.FontLabel);
                m.EmploymentColor = ToneColor(m.Employment.Tone);
                m.EmploymentStateHeight = NeedDashboardPanel.Wrapped(m.EmploymentState, stateWidth, RUIF.Tokens.FontLabel);
                m.EmploymentRows = MeasureRows(m.Employment.Rows, width, out m.EmploymentHeights);
            }
            m.Care = view.Care;
            if (m.Care != null)
            {
                float stateWidth = width - FactDotSize - SpaceXs;
                m.CareState = NeedDashboardPanel.Flow(m.Care.State, stateWidth, RUIF.Tokens.FontLabel);
                m.CareColor = ToneColor(m.Care.Tone);
                m.CareStateHeight = NeedDashboardPanel.Wrapped(m.CareState, stateWidth, RUIF.Tokens.FontLabel);
                m.CareRows = MeasureRows(m.Care.Rows, width, out m.CareHeights);
            }
            m.AccountModelRows = MeasureRows(view.Account?.Model?.Details ?? Array.Empty<NeedTextRow>(), width, out m.AccountModelHeights);
            m.Planning = MeasureRows(view.Planning, width, out m.PlanningHeights);
            m.Opportunities = MeasureRows(view.Opportunities, width, out m.OpportunityHeights);
            m.Details = MeasureRows(view.Facts, width, out m.DetailHeights);
            m.Budget = view.Budget;
            if (m.Budget != null)
            {
                // 预算卡：状态 / 现金 / 储备目标 / 下水位 / 收入缺口 / 补储 + 三分配。
                // 标签与值分开折行，交回 StatRow 排版（其内部走 RowSplit）；现金行另带进度条。
                var labels = new[] { N("Planning.Budget"), N("Planning.Cash"), N("Planning.Target"),
                    N("Planning.LowWater"), N("Planning.Income"), N("Planning.Refill") };
                var values = new[] { m.Budget.State, m.Budget.Cash, m.Budget.ReserveTarget,
                    m.Budget.LowWater, m.Budget.IncomeGap, m.Budget.Refill };
                m.BudgetLabels = new string[labels.Length];
                m.BudgetValues = new string[values.Length];
                m.BudgetFieldHeights = new float[labels.Length];
                float labelWidth = width * ValueShare - SpaceXs;
                float valueWidth = Mathf.Max(1f, width - width * ValueShare - SpaceXs);
                for (int i = 0; i < labels.Length; i++)
                {
                    m.BudgetLabels[i] = NeedDashboardPanel.Flow(labels[i], labelWidth, RUIF.Tokens.FontLabel);
                    m.BudgetValues[i] = NeedDashboardPanel.Flow(values[i], valueWidth, RUIF.Tokens.FontLabel);
                    m.BudgetFieldHeights[i] = Math.Max(m.RowHeight, Math.Max(
                        NeedDashboardPanel.Wrapped(m.BudgetLabels[i], labelWidth, RUIF.Tokens.FontLabel),
                        NeedDashboardPanel.Wrapped(m.BudgetValues[i], valueWidth, RUIF.Tokens.FontLabel)));
                }
                m.BudgetAllocText = NeedDashboardPanel.Flow(
                    N("Cycle.Alloc.Immediate") + " " + m.Budget.Immediate + "  ·  "
                    + N("Cycle.Alloc.Saving") + " " + m.Budget.Saving + "  ·  "
                    + N("Cycle.Alloc.Optional") + " " + m.Budget.Optional,
                    width - SpaceXs, RUIF.Tokens.FontLabel);
                m.BudgetAllocHeight = NeedDashboardPanel.Wrapped(m.BudgetAllocText, width - SpaceXs, RUIF.Tokens.FontLabel);
            }

            float decision = 0f;
            if (m.AccountModelRows.Length > 0) { decision += m.SectionHeight + Sum(m.AccountModelHeights) + Gap; }
            if (cycleCount > 0) { decision += m.SectionHeight + cycleTotal + Gap; }
            // 劳动与合同 / 照护与服务跟在四循环之后：循环说明"是否需要"，这两区说明"实际发生了什么"。
            if (m.Employment != null) { decision += m.SectionHeight + m.EmploymentStateHeight + Sum(m.EmploymentHeights) + Gap; }
            if (m.Care != null) { decision += m.SectionHeight + m.CareStateHeight + Sum(m.CareHeights) + Gap; }
            if (m.Planning.Length > 0) { decision += m.SectionHeight + Sum(m.PlanningHeights) + Gap; }
            if (m.Budget != null) { decision += m.SectionHeight + Sum(m.BudgetFieldHeights) + m.BudgetAllocHeight; }
            if (m.Opportunities.Length > 0) { decision += Gap + m.SectionHeight + Sum(m.OpportunityHeights); }
            if (m.Details.Length > 0) { decision += Gap + m.SectionHeight + Sum(m.DetailHeights); }
            m.DecisionHeight = Math.Max(decision, EmptyHeight);

            float body = tab == TabGoal ? m.GoalHeight : tab == TabDecision ? m.DecisionHeight : m.DemandHeight;
            // 空态（无需求）时绘制内容**与页签无关**（EmptyState + 账户区），故滚动总高必须按同一内容显式计算：
            // 沿用页签高度会让 tab1/2 只有 ≈EmptyHeight 而账户区高达上百像素 ⇒ 账户区下半被裁（GATE-UI H2）。
            m.Height = m.IsEmpty
                ? EmptyHeight + Gap + m.AccountHeight + Gap * 2f
                : body + Gap * 2f;
        }

        private static bool AnyChip(List<int> priorities)
        {
            for (int i = 0; i < priorities.Count; i++) if (priorities[i] > 0) return true;
            return false;
        }

        private static NeedTextRow[] MeasureRows(IReadOnlyList<NeedTextRow> rows, float width, out float[] heights)
        {
            var result = new NeedTextRow[rows.Count];
            heights = new float[rows.Count];
            float labelWidth = width * (1f - ValueShare) - SpaceXs;
            float valueWidth = width * ValueShare - SpaceXs;
            for (int i = 0; i < rows.Count; i++)
            {
                result[i] = new NeedTextRow(NeedDashboardPanel.Flow(rows[i].Label, labelWidth, RUIF.Tokens.FontLabel),
                    NeedDashboardPanel.Flow(rows[i].Value, valueWidth, RUIF.Tokens.FontLabel));
                heights[i] = Math.Max(NeedDashboardPanel.Wrapped(result[i].Label, labelWidth, RUIF.Tokens.FontLabel),
                    NeedDashboardPanel.Wrapped(result[i].Value, valueWidth, RUIF.Tokens.FontLabel));
            }
            return result;
        }

        private static float Sum(float[] values)
        {
            float total = 0f;
            for (int i = 0; i < values.Length; i++) total += values[i];
            return total;
        }
    }
}

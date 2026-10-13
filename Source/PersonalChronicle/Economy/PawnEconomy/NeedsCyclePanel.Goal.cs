using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    internal static partial class NeedsCyclePanel
    {
        /// <summary>
        /// ② 目标（规划层）：**每条需求一张卡** —— 优先级芯片、活跃呼吸点、当前满足度条、
        /// 「进入阈值 → 目标值」、缺口三口径（相对 / 绝对 / 原始）。
        /// 未纳入规划的需求也成行显示（阈值区如实写"不适用"），不再压成一行字符串。
        /// </summary>
        private static void DrawGoalTab(float x, float y, float width, NeedsCycleMeasure m)
        {
            if (m.Goals.Length == 0)
            {
                RUIF.FeedbackAtoms.EmptyState(new Rect(x, y, width, EmptyHeight), N("Cycle.GoalEmpty"), N("Cycle.GoalEmptyHint"));
                return;
            }
            RUIF.PanelAtoms.SectionBar(new Rect(x, y, width, m.SectionHeight), N("Cycle.Section.Goal"), m.GoalCountText, true);
            y += m.SectionHeight;
            for (int i = 0; i < m.Goals.Length; i++)
            {
                DrawGoalCard(x, y, width, m, i);
                y += m.GoalHeights[i] + Gap;
            }
        }

        /// <summary>
        /// 单张目标卡。四段式：标题行（芯片 + 名称 + 满足度）→ 满足度条 → 进入阈值 → 目标值 → 缺口三口径。
        /// 卡片几何与 <see cref="NeedsCycleMeasure"/> 的测量公式**逐项对应**（改一处必须改另一处）。
        /// </summary>
        private static void DrawGoalCard(float x, float y, float width, NeedsCycleMeasure m, int index)
        {
            var goal = m.Goals[index];
            Color color = m.GoalColors[index];
            Rect card = new Rect(x, y, width, m.GoalHeights[index]);
            RUIF.Shapes.InsetPanel(card);
            RUIF.Shapes.CornerFrame(card, RUIF.Tokens.Dim, CornerArm, CornerThickness);
            float inner = Mathf.Max(1f, card.width - CardPadX * 2f);
            float textWidth = Mathf.Max(1f, inner - SpaceXs);
            float cx = card.x + CardPadX;
            float cy = card.y + CardPadY;
            float titleHeight = RUIF.Measure.RowHeight(GameFont.Small);

            // 标题行：优先级芯片（无规划时不画）→ 名称 → 满足度文本（状态色）。
            if (goal.Priority > 0)
            {
                // P0/P1 属"需要处理"，用警示色；P2 用中性色（TraitChips 的 positive 决定配色方向）。
                RUIF.DataAtoms.TraitChips(new Rect(cx, cy, ChipWidth, titleHeight),
                    new[] { "P" + goal.Priority }, new[] { goal.Priority <= 1 ? false : true }, 0f);
            }
            if (goal.Active)
            {
                // 呼吸点表示**目标已激活**（越过进入阈值或尚未补足）；未激活不画点，避免"看起来都在跑"。
                RUIF.StatusAtoms.PulseDot(RUIF.StatusFmt.DotRect(new Rect(cx + ChipWidth, cy, 6f, titleHeight)),
                    RUIF.Tokens.Warn, Time.realtimeSinceStartup);
            }
            RUIF.Draw.Label(new Rect(cx + ChipWidth + 10f, cy, Mathf.Max(1f, inner - ChipWidth - 70f), titleHeight),
                goal.Name, GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            RUIF.Draw.Label(new Rect(card.xMax - CardPadX - 60f, cy, 60f, titleHeight),
                goal.RatioPercent, GameFont.Small, color, TextAnchor.MiddleRight);
            cy += titleHeight + InnerGap;

            // 满足度条（0..1 全长）。
            RUIF.ProgressAtoms.CapacityBar(new Rect(cx, cy, inner, BarHeight), Mathf.Max(0f, goal.Ratio01), 1f, color);
            cy += BarHeight + InnerGap;

            // 「进入阈值 → 目标值」；未纳入规划时为"不适用"（如实，不伪造阈值）。
            float thresholdHeight = NeedDashboardPanel.Wrapped(m.GoalThresholds[index], textWidth, RUIF.Tokens.FontLabel);
            RUIF.Draw.Label(new Rect(cx, cy, inner, thresholdHeight), m.GoalThresholds[index],
                RUIF.Tokens.FontLabel, RUIF.Tokens.Muted, TextAnchor.UpperLeft);
            cy += thresholdHeight;

            // v0.4 §6 风险时限与余量（不可估时如实说明原因，不显示假数字）。
            float deadlineHeight = NeedDashboardPanel.Wrapped(m.GoalDeadlines[index], textWidth, RUIF.Tokens.FontLabel);
            if (!string.IsNullOrEmpty(m.GoalDeadlines[index]))
            {
                RUIF.Draw.Label(new Rect(cx, cy, inner, deadlineHeight), m.GoalDeadlines[index],
                    RUIF.Tokens.FontLabel, RUIF.Tokens.Info, TextAnchor.UpperLeft);
                cy += deadlineHeight;
            }

            // v0.4 §9 等待加权：等得越久越该先办。本周期才开始等待（0）时不画 —— 每张卡都出一行 0 是噪声。
            if (!string.IsNullOrEmpty(m.GoalWaits[index]))
            {
                float waitHeight = NeedDashboardPanel.Wrapped(m.GoalWaits[index], textWidth, RUIF.Tokens.FontLabel);
                RUIF.Draw.Label(new Rect(cx, cy, inner, waitHeight), m.GoalWaits[index],
                    RUIF.Tokens.FontLabel, RUIF.Tokens.Warn, TextAnchor.UpperLeft);
                cy += waitHeight;
            }

            // 缺口三口径（相对 / 绝对 / 原始）；任一不可用时给事实状态文本，不当作 0。
            float gapHeight = NeedDashboardPanel.Wrapped(m.GoalGaps[index], textWidth, RUIF.Tokens.FontLabel);
            RUIF.Draw.Label(new Rect(cx, cy, inner, gapHeight), m.GoalGaps[index],
                RUIF.Tokens.FontLabel, RUIF.Tokens.Dim, TextAnchor.UpperLeft);
        }
    }
}

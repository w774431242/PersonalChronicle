using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    internal static partial class NeedsCyclePanel
    {
        /// <summary>
        /// ③ 决策（仲裁层）：**四个业务循环为主结构** —— 即时满足 / 储蓄保障 / 劳动收入 / 消费与服务；
        /// 之下依次是**劳动与合同**（已在履约的事实：合同 / 退出预判 / 应付 / 退避 / 可靠性）、
        /// **照护与服务**（M8 授权与能力）、影子仲裁、预算与储蓄（含三分配与现金进度条）、候选岗位、原始观测明细。
        /// 循环④读 M7/M8 的真实三态（可行 / 受阻 / 未接通），不再恒显「未实装」。
        /// </summary>
        private static void DrawDecisionTab(float x, float y, float width, NeedsCycleMeasure m)
        {
            if (m.AccountModelRows.Length > 0)
            {
                RUIF.PanelAtoms.SectionBar(new Rect(x, y, width, m.SectionHeight), N("Cycle.Account.ModelName"), string.Empty, true);
                y += m.SectionHeight;
                for (int i = 0; i < m.AccountModelRows.Length; i++)
                {
                    RUIF.PanelAtoms.StatRow(new Rect(x, y, width, m.AccountModelHeights[i]),
                        m.AccountModelRows[i].Label, m.AccountModelRows[i].Value);
                    y += m.AccountModelHeights[i];
                }
                y += Gap;
            }
            if (m.Cycles.Length > 0)
            {
                RUIF.PanelAtoms.SectionBar(new Rect(x, y, width, m.SectionHeight), N("Cycle.Section.Cycles"), string.Empty, true);
                y += m.SectionHeight;
                for (int i = 0; i < m.Cycles.Length; i++)
                {
                    DrawCycleRow(x, y, width, m, i);
                    y += m.CycleHeights[i];
                }
                y += Gap;
            }

            // 劳动与合同 / 照护与服务：循环说明"是否需要"，这两区说明"实际发生了什么"（真实账本事实）。
            if (m.Employment != null)
            {
                DrawFactSection(x, y, width, m, N("Cycle.Section.Employment"), m.EmploymentState,
                    m.EmploymentStateHeight, m.EmploymentColor, m.EmploymentRows, m.EmploymentHeights);
                y += m.SectionHeight + m.EmploymentStateHeight + Sum(m.EmploymentHeights) + Gap;
            }
            if (m.Care != null)
            {
                DrawFactSection(x, y, width, m, N("Cycle.Section.Care"), m.CareState,
                    m.CareStateHeight, m.CareColor, m.CareRows, m.CareHeights);
                y += m.SectionHeight + m.CareStateHeight + Sum(m.CareHeights) + Gap;
            }

            if (m.Planning.Length > 0)
            {
                RUIF.PanelAtoms.SectionBar(new Rect(x, y, width, m.SectionHeight), N("Cycle.Section.Arbitration"), string.Empty, true);
                y += m.SectionHeight;
                for (int i = 0; i < m.Planning.Length; i++)
                {
                    RUIF.PanelAtoms.StatRow(new Rect(x, y, width, m.PlanningHeights[i]), m.Planning[i].Label, m.Planning[i].Value);
                    y += m.PlanningHeights[i];
                }
                y += Gap;
            }

            if (m.Budget != null)
            {
                RUIF.PanelAtoms.SectionBar(new Rect(x, y, width, m.SectionHeight), N("Cycle.Section.Budget"), string.Empty, true);
                y += m.SectionHeight;
                for (int i = 0; i < m.BudgetLabels.Length; i++)
                {
                    Rect row = new Rect(x, y, width, m.BudgetFieldHeights[i]);
                    // 现金行带进度条（现金 / 有限储备目标）；不可计算时不画条，避免把"未知"画成 0。
                    if (i == 1 && m.Budget.CashVsTarget01 >= 0f)
                    {
                        RUIF.PanelAtoms.StatRow(row, m.BudgetLabels[i], m.BudgetValues[i],
                            m.Budget.CashVsTarget01, RatioColor(m.Budget.CashVsTarget01));
                    }
                    else
                    {
                        RUIF.PanelAtoms.StatRow(row, m.BudgetLabels[i], m.BudgetValues[i]);
                    }
                    y += m.BudgetFieldHeights[i];
                }
                // 三分配（即时 / 储蓄 / 可选）：影子口径的预算切分，**不是**可支付承诺。
                RUIF.Draw.Label(new Rect(x, y, width, m.BudgetAllocHeight), m.BudgetAllocText,
                    RUIF.Tokens.FontLabel, RUIF.Tokens.Dim, TextAnchor.UpperLeft);
                y += m.BudgetAllocHeight + Gap;
            }

            if (m.Opportunities.Length > 0)
            {
                RUIF.PanelAtoms.SectionBar(new Rect(x, y, width, m.SectionHeight), N("Cycle.Section.Opportunities"), string.Empty, true);
                y += m.SectionHeight;
                for (int i = 0; i < m.Opportunities.Length; i++)
                {
                    RUIF.PanelAtoms.StatRow(new Rect(x, y, width, m.OpportunityHeights[i]), m.Opportunities[i].Label, m.Opportunities[i].Value);
                    y += m.OpportunityHeights[i];
                }
                y += Gap;
            }

            if (m.Details.Length > 0)
            {
                RUIF.PanelAtoms.SectionBar(new Rect(x, y, width, m.SectionHeight), N("Cycle.Details"), string.Empty, true);
                y += m.SectionHeight;
                for (int i = 0; i < m.Details.Length; i++)
                {
                    RUIF.PanelAtoms.StatRow(new Rect(x, y, width, m.DetailHeights[i]), m.Details[i].Label, m.Details[i].Value);
                    y += m.DetailHeights[i];
                }
            }
        }

        /// <summary>
        /// 单个**事实分区**（劳动与合同 / 照护与服务）：分区标题 → 状态行（色点 + 着色调状态文本）→ 明细行（标签/值两列）。
        /// 几何与 <see cref="NeedsCycleMeasure"/> 的测量公式**逐项对应**（改一处必须改另一处）；
        /// 本方法只画内容，滚动 y 由调用方按同一公式推进。
        /// </summary>
        private static void DrawFactSection(float x, float y, float width, NeedsCycleMeasure m,
            string title, string state, float stateHeight, Color color, NeedTextRow[] rows, float[] heights)
        {
            const float dot = FactDotSize;
            RUIF.PanelAtoms.SectionBar(new Rect(x, y, width, m.SectionHeight), title, string.Empty, true);
            y += m.SectionHeight;
            RUIF.VisualAtoms.Swatch(new Rect(x, y + (stateHeight - dot) / 2f, dot, dot), color);
            RUIF.Draw.Label(new Rect(x + dot + SpaceXs, y, Mathf.Max(1f, width - dot - SpaceXs), stateHeight),
                state, RUIF.Tokens.FontLabel, color, TextAnchor.UpperLeft);
            y += stateHeight;
            for (int i = 0; i < rows.Length; i++)
            {
                RUIF.PanelAtoms.StatRow(new Rect(x, y, width, heights[i]), rows[i].Label, rows[i].Value);
                y += heights[i];
            }
        }

        /// <summary>
        /// 单条业务循环：状态色块 + 循环名 + 状态（着色）+ 明细。
        /// 明细中的优先序（P0/P1/P2）与岗位 ID 都来自结构化数据，不按显示文本反解（GOV-009）。
        /// </summary>
        private static void DrawCycleRow(float x, float y, float width, NeedsCycleMeasure m, int index)
        {
            const float dot = 6f, nameWidth = 96f, stateWidth = 84f;
            Rect row = new Rect(x, y, width, m.CycleHeights[index]);
            Color color = m.CycleColors[index];
            RUIF.VisualAtoms.Swatch(new Rect(row.x, row.y + (row.height - dot) / 2f, dot, dot), color);
            float cx = row.x + dot + SpaceXs;
            RUIF.Draw.Label(new Rect(cx, row.y, nameWidth, row.height), m.CycleNames[index], RUIF.Tokens.FontLabel, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            cx += nameWidth;
            RUIF.Draw.Label(new Rect(cx, row.y, stateWidth, row.height), m.CycleStates[index], RUIF.Tokens.FontLabel, color, TextAnchor.MiddleLeft);
            cx += stateWidth;
            RUIF.Draw.Label(new Rect(cx, row.y, Mathf.Max(1f, row.xMax - cx), row.height), m.CycleDetails[index],
                RUIF.Tokens.FontLabel, RUIF.Tokens.Dim, TextAnchor.MiddleLeft);
        }
    }
}

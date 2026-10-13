using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    internal static partial class NeedsCyclePanel
    {
        /// <summary>
        /// ① 需求（观察层）：生活保障概览 + **短期满足度 × 长期目标**对照 + 状态条件 + 长期生活规划。
        /// 长期列直接复用 presenter 给出的长期目标文本，表现层不重算生活政策。
        /// </summary>
        private static void DrawDemandTab(float x, float y, float width, NeedsCycleMeasure m, NeedPanelState state)
        {
            // 概览卡 = 生活保障读数（标题 / 主进度条 / 心情）+ 其内的「个人账户」摘要区（CB-ECON-002）：
            // 账户区画在**卡片内部**，其高度已计入 OverviewHeight，故此处推进量不变。
            DrawOverview(x, y, width, m, state);
            y += m.OverviewHeight + Gap;

            if (m.ShownNames.Length > 0)
            {
                // count 传空：SectionBar 的 count 画在窄带内只适合短计数，"已规划 N / M" 已在概览卡呈现。
                RUIF.PanelAtoms.SectionBar(new Rect(x, y, width, m.SectionHeight), N("Cycle.Section.Satisfaction"), string.Empty, true);
                y += m.SectionHeight;
                DrawHeaderRow(x, y, width, m, true, false, m.HeaderLongTarget);
                y += m.RowHeight;
                for (int i = 0; i < m.ShownNames.Length; i++)
                {
                    DrawNeedRow(x, y, width, m, m.ShownNames[i], m.ShownRatios[i], m.ShownColors[i],
                        m.ShownPercents[i], m.ShownTargets[i], null, m.ShownPriorities[i], m.ShownActive[i]);
                    y += m.RowHeight;
                }
                y += Gap;
            }

            if (m.HiddenNames.Length > 0)
            {
                // 原版需求页不显示的项（showOnNeedList=false）：保留可见但单独成区，避免与常规需求混淆。
                RUIF.PanelAtoms.SectionBar(new Rect(x, y, width, m.SectionHeight), N("Cycle.Section.OtherNeeds"), string.Empty, true);
                y += m.SectionHeight;
                DrawHeaderRow(x, y, width, m, true, false, m.HeaderLongTarget);
                y += m.RowHeight;
                for (int i = 0; i < m.HiddenNames.Length; i++)
                {
                    DrawNeedRow(x, y, width, m, m.HiddenNames[i], m.HiddenRatios[i], m.HiddenColors[i],
                        m.HiddenPercents[i], m.HiddenTargets[i], null, m.HiddenPriorities[i], m.HiddenActive[i]);
                    y += m.RowHeight;
                }
                y += Gap;
            }

            if (m.Status.Length > 0)
            {
                RUIF.PanelAtoms.SectionBar(new Rect(x, y, width, m.SectionHeight), N("Cycle.Section.Status"), string.Empty, true);
                y += m.SectionHeight;
                for (int i = 0; i < m.Status.Length; i++)
                {
                    RUIF.PanelAtoms.StatRow(new Rect(x, y, width, m.StatusHeights[i]), m.Status[i].Label, m.Status[i].Value);
                    y += m.StatusHeights[i];
                }
                y += Gap;
            }

            if (m.Policy != null)
            {
                // 长期生活规划（长期需求）：政策参数逐项成行；预算（现金/储备/水位/补储）不在此区 —— 归决策页签。
                RUIF.PanelAtoms.SectionBar(new Rect(x, y, width, m.SectionHeight), N("Cycle.Section.LongTerm"), string.Empty, true);
                y += m.SectionHeight;
                DrawPolicyCard(x, y, width, m);
            }
        }

        /// <summary>
        /// 长期生活规划卡：政策参数**逐项一行**，每行按**与需求行相同的列结构**绘制 ——
        /// 标签列 = <see cref="NameWidth"/>、条起点 = <see cref="NameWidth"/> + <see cref="Gap"/>、
        /// 值列 = <see cref="PercentWidth"/>，因此政策条的起点与长度和需求满足度条**逐像素对齐**。
        /// （此前用 <c>PanelAtoms.StatRow</c> 的带条重载，其内部 RowSplit 会把条压在标签与值之间 ⇒ 条又短又偏，
        /// 与需求区的条风格不统一。）
        /// 可归一到比例的参数带条，保障期与风格依据无自然上界故只给文本。
        /// </summary>
        private static void DrawPolicyCard(float x, float y, float width, NeedsCycleMeasure m)
        {
            Rect card = new Rect(x, y, width, m.PolicyHeight);
            RUIF.Shapes.InsetPanel(card);
            RUIF.Shapes.CornerFrame(card, RUIF.Tokens.Dim, CornerArm, CornerThickness);
            float inner = Mathf.Max(1f, card.width - CardPadX * 2f);
            // 政策卡没有「长期目标 / 缺口」两列 ⇒ 条可用宽按同一公式在无目标列时计算（起点与列宽仍与需求行一致）。
            float barWidth = BarWidth(inner, m, false, false);
            float valueX = card.x + CardPadX + NameWidth + Gap + barWidth + Gap;
            float cy = card.y + CardPadY;
            for (int i = 0; i < m.PolicyLabels.Length; i++)
            {
                Rect row = new Rect(card.x + CardPadX, cy, inner, m.PolicyRowHeights[i]);
                RUIF.Draw.Label(new Rect(row.x, row.y, NameWidth, row.height),
                    m.PolicyLabels[i], RUIF.Tokens.FontLabel, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
                if (m.PolicyBars[i] >= 0f)
                {
                    RUIF.ProgressAtoms.CapacityBar(RatioBarRect(row, barWidth), m.PolicyBars[i], 1f, m.PolicyBarColors[i]);
                }
                RUIF.Draw.Label(new Rect(valueX, row.y, Mathf.Max(1f, row.xMax - valueX), row.height),
                    m.PolicyValues[i], RUIF.Tokens.FontLabel, RUIF.Tokens.Text, TextAnchor.MiddleRight);
                cy += m.PolicyRowHeights[i];
            }
        }

        /// <summary>
        /// 概览卡（=「生活保障」）：整体生活保障（原版可见需求的满足度均值）+ 规划计数 + 心情（只读结果量），
        /// 其下以轻量分隔接**个人账户摘要区**（CB-ECON-002）。
        /// 账户金额与需求满足度是**不同量纲**：账户不参与满足度均值、不参与心情数值、也不改变本卡配色。
        /// </summary>
        private static void DrawOverview(float x, float y, float width, NeedsCycleMeasure m, NeedPanelState state)
        {
            Rect card = new Rect(x, y, width, m.OverviewHeight);
            if (card.height <= 0f)
            {
                return;
            }
            RUIF.Shapes.InsetPanel(card);
            // 角框用中性 Dim 而非条色：条色已由卡内进度条表达，角框复述只会增加视觉噪声。
            RUIF.Draw.LineH(card.x, card.y, card.width, RUIF.Tokens.BorderSoft);
            float inner = Mathf.Max(1f, card.width - CardPadX * 2f);
            float titleHeight = RUIF.Measure.RowHeight(GameFont.Small);
            float cy = card.y + CardPadY;
            RUIF.Draw.Label(new Rect(card.x + CardPadX, cy, inner * .5f, titleHeight),
                N("Cycle.Overview"), GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            RUIF.Draw.Label(new Rect(card.x + CardPadX + inner * .5f, cy, inner * .5f, titleHeight),
                m.OverviewSummary, RUIF.Tokens.FontLabel, RUIF.Tokens.Muted, TextAnchor.MiddleRight);
            cy += titleHeight + InnerGap;
            // 语义借用说明：CapacityBar 的 (used, capacity) 在此用于表达**满足度**（used = 当前比例，capacity = 1），
            // 而非容量占用；视觉语义（进度比例 + 溢出段）完全一致，故复用而不另起进度条。
            RUIF.ProgressAtoms.CapacityBar(new Rect(card.x + CardPadX, cy, inner, BarHeight * 1.5f),
                Mathf.Max(0f, m.OverviewRatio), 1f, m.OverviewColor);
            cy += BarHeight * 1.5f + InnerGap;
            RUIF.Draw.Label(new Rect(card.x + CardPadX, cy, inner * .5f, titleHeight), m.MoodLabel, RUIF.Tokens.FontLabel, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            RUIF.Draw.Label(new Rect(card.x + CardPadX + inner * .5f, cy, inner * .5f, titleHeight), m.MoodText, RUIF.Tokens.FontLabel, m.MoodColor, TextAnchor.MiddleRight);
            cy += titleHeight;
            // 账户区画在卡内（同一卡片内部的次级分组）；cy 停在卡底内边距之前，推进量全部取自测量结果。
            DrawAccount(card.x + CardPadX, cy, inner, m, state);
        }

    }
}

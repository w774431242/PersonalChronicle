using System;
using UnityEngine;
using Verse;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// 「个人需求」页正文：**一页式 + 页内三页签**（需求 / 目标 / 决策），整页由 RUIF 绘制。
    /// 三页签对应需求循环的三个可读面：
    ///   ① 需求 = 短期（当前满足度）+ 长期（生活政策目标 + 页尾长期生活规划区）
    ///   ② 目标 = 每条需求的完整目标态（进入阈值 → 目标值、优先级、活跃性、缺口三口径）
    ///   ③ 决策 = 四个业务循环 + 影子仲裁 + 预算与储蓄 + 候选岗位
    ///
    /// **零重复**（2026-10-09 裁决）：生活政策只出现在需求页；**预算细节**只出现在决策页；
    /// 同一份影子计划由 `ShadowPlanPresenter` 的互补入口各自产出。
    /// <para>**2026-10-11 修订（CB-ECON-002，用户明确授权）**：需求页「生活保障」卡内新增
    /// 「个人账户」**摘要**（可用余额 / 账户总额 / 预留金额 / 生活储备目标 / 补储缺口 / 收入状态）。
    /// 摘要不是第二份预算：它的预算两项**直接取用决策页同一份 <c>NeedBudgetView</c> 实例的文本**，
    /// 补储判定与启动线同样只读预算出口的结构化字段，本页不重算任何预算公式、不做滞回判定。
    /// 预算的完整细节（下水位、应急额、三分配）仍只在决策页呈现。</para>
    ///
    /// 边界契约：
    ///   · 只消费 <see cref="NeedDashboardView"/>；不查询数据、不持有宿引用、不写任何状态。
    ///   · **不调用任何原版需求 UI**；原版只作为数据真值来源（读 <c>Need.CurLevelPercentage</c>）。
    ///   · 度量/配色一律直接取 `RUIF.Tokens.*` 常量（编译期绑定，无镜像漂移）；不引用 Archive 的 UITheme。
    ///   · 会话态（滚动 / 测量缓存 / 当前页签）由宿主 <see cref="NeedPanelState"/> 承载；本类无静态可变状态。
    ///   · 分区与数值取自 `NeedTableLine` / `NeedGoalLine` / `NeedBudgetView` / `NeedCycleLine` 等**结构化字段**，
    ///     不按显示文本匹配（GOV-009）。
    ///   · 滚动内容坐标为**相对坐标**（原点 = 视口左上角）；页签栏画在滚动区**之外**。
    ///
    /// 本类按页签拆为 partial：`Measure`（测量）/ `Demand` / `Goal` / `Decision`。
    /// </summary>
    internal static partial class NeedsCyclePanel
    {
        // 度量直接引用 RUIF Token（编译期常量，避免"就地镜像同值"造成后续漂移）。
        private const float Gap = RUIF.Tokens.CardGap;
        private const float CardPadX = RUIF.Tokens.CardPadX;
        private const float CardPadY = RUIF.Tokens.CardPadY;
        private const float BarHeight = RUIF.Tokens.ProgressbarH;
        private const float InnerGap = RUIF.Tokens.SpaceXxs;
        private const float SpaceXs = RUIF.Tokens.SpaceXs;

        // 本页自有布局常量（无 RUIF 对应项）。
        private const float EmptyHeight = 100f;
        private const float ValueShare = .45f;
        private const float CornerArm = 12f;
        private const float CornerThickness = 2f;
        /// <summary>优先级芯片占用的左内边距（行内）。</summary>
        private const float ChipWidth = 26f;
        /// <summary>事实分区状态行的左侧色点直径：**测量与绘制共用同一常量**，防两侧可用宽口径漂移（GATE-UI P2-UI-1）。</summary>
        private const float FactDotSize = 6f;

        // 列宽（列头与数据行共用 ⇒ 永不漂移）。
        private const float NameWidth = 112f;
        private const float PercentWidth = 88f;
        private const float TargetWidth = 72f;
        private const float GapColumnWidth = 76f;
        private const float MinimumBarWidth = 24f;
        /// <summary>低于此宽度时隐藏目标/缺口列，避免窄面板列重叠。</summary>
        private const float NarrowWidth = 400f;

        // 「个人账户」摘要区（CB-ECON-002）：卡片内次级分组，测量与绘制共用下列常量。
        /// <summary>账户字段**两列**排布所需的最小卡片内宽；低于此值改为逐项一行（金额与单位都不被截掉）。</summary>
        private const float AccountTwoColumnWidth = 300f;
        /// <summary>账户区「查看决策详情」按钮宽度上限（窄屏按内宽收缩；标签实测宽优先，不靠省略号撑）。</summary>
        private const float AccountButtonMaxWidth = 132f;

        // 页签索引（与 TabLabels 顺序一致）。
        private const int TabDemand = 0, TabGoal = 1, TabDecision = 2;

        // 呈现阈值（**非**游戏机制阈值）：仅决定满足度条与百分数的配色分档。
        private const float RatioGood = .7f, RatioLow = .4f;

        /// <summary>整键回落（P0-4 同一纪律，唯一实现 <see cref="EconomyLabels.TranslatedOrId"/>）：
        /// 缺键显示标识本身，绝不把 <c>PC_PEA.Needs.*</c> 裸键打上屏。</summary>
        private static string N(string suffix) => EconomyLabels.TranslatedOrId("PC_PEA.Needs." + suffix, suffix);

        internal static Color RatioColor(float ratio01)
            => ratio01 < 0f ? RUIF.Tokens.Dim
                : ratio01 >= RatioGood ? RUIF.Tokens.Alive
                : ratio01 >= RatioLow ? RUIF.Tokens.Warn
                : RUIF.Tokens.Dead;

        /// <summary>四循环色调 → RUIF 状态色（契约层不引第三方枚举，故在表现层做这一处映射）。</summary>
        internal static Color ToneColor(NeedCycleTone tone)
        {
            switch (tone)
            {
                case NeedCycleTone.Ok: return RUIF.Tokens.Alive;
                case NeedCycleTone.Pending: return RUIF.Tokens.Warn;
                case NeedCycleTone.Blocked: return RUIF.Tokens.Dead;
                case NeedCycleTone.Unimplemented: return RUIF.Tokens.Dim;
                default: return RUIF.Tokens.Muted;
            }
        }

        /// <summary>
        /// 满足度文本：有比例时给百分数；无比例时**如实**给出事实状态
        /// （"未适配"/"读取错误"/"不适用"/"未知"），不得一律显示为"未配置"。
        /// </summary>
        private static string PercentText(NeedTableLine line)
            => line.Ratio01 >= 0f ? Mathf.RoundToInt(line.Ratio01 * 100f) + "%" : N("Fact." + line.RatioState);

        /// <summary>
        /// 绘制整页正文。<paramref name="body"/> 为固定区（页签栏 + 滚动区）；
        /// 页签栏画在其顶部，处于滚动区之外。
        /// </summary>
        public static void Draw(Rect body, NeedDashboardView view, NeedPanelState state)
        {
            if (body.width < 1f || body.height < 1f || state == null)
            {
                return;
            }
            if (view == null)
            {
                RUIF.FeedbackAtoms.EmptyState(body, N("Unavailable"), N("UnavailableHint"));
                return;
            }

            float width = Math.Max(1f, body.width - RUIF.Tokens.ScrollbarThickness);
            float tabHeight = RUIF.Measure.ControlHeight(GameFont.Small);
            bool oldWrap = Text.WordWrap;
            Text.WordWrap = true;
            try
            {
                // 测量须在 WordWrap=true 作用域内，否则折行高度按关闭态计算。
                var measure = state.Cycle;
                if (measure == null || !ReferenceEquals(measure.View, view)
                    || Math.Abs(measure.Width - width) >= .5f || measure.Tab != state.CycleTab
                    || measure.TinyFontSupported != Text.TinyFontSupported)
                {
                    // 换页签时复位滚动：各页签内容高不同，沿用旧偏移会被钳制到末尾（观感突兀）。
                    if (measure != null && measure.Tab != state.CycleTab)
                    {
                        state.Scroll = Vector2.zero;
                    }
                    measure = new NeedsCycleMeasure { View = view, Width = width, Tab = state.CycleTab,
                        TinyFontSupported = Text.TinyFontSupported };
                    Measured(measure, view, width, state.CycleTab);
                    state.Cycle = measure;
                }

                // 空态（该 Pawn 无任何可展示需求）时不画页签栏：三个页签均无内容，画了只会留死区。
                if (!measure.IsEmpty)
                {
                    RUIF.NavAtomsB.LayerSwitch(new Rect(body.x, body.y, body.width, tabHeight), measure.TabLabels, ref state.CycleTab);
                }
                float topInset = measure.IsEmpty ? 0f : tabHeight + Gap;

                Rect scrollBody = new Rect(body.x, body.y + topInset, body.width, Mathf.Max(0f, body.height - topInset));
                if (scrollBody.height <= 0f)
                {
                    return;
                }
                RUIF.Layout.BeginScroll(scrollBody, measure.Height, ref state.Scroll);
                try
                {
                    DrawContent(width, measure, state);
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

        /// <summary>
        /// 绘制滚动内容。坐标系是**滚动内容坐标系**（原点 = 视口左上角）：
        /// <c>RUIF.Layout.BeginScroll</c> 返回的内容区即从 (0,0) 起算，混入外层绝对 x
        /// 会与滚动偏移叠加（实机表现为内容整体右移并被裁切）。
        /// </summary>
        private static void DrawContent(float width, NeedsCycleMeasure m, NeedPanelState state)
        {
            const float x = 0f;
            const float y = 0f;

            if (m.IsEmpty)
            {
                RUIF.FeedbackAtoms.EmptyState(new Rect(x, y, width, EmptyHeight), N("Empty"), N("EmptyHint"));
                // 「无需求」不等于「无账户」（CB-ECON-002 A09）：账户区**不得**被需求空态吞掉 ——
                // 该主体没有可展示需求条，但仍可能持有一个合法个人账户。
                // 空态下不画页签栏 ⇒ 跳转按钮无目标页可回，故此处抑制（与 MeasureAccount 的 withJump 同源）。
                // 内宽与卡片路径同源（`width − CardPadX*2`），不得直接把整行宽当卡片内宽（GATE-UI H3）。
                DrawAccount(x + CardPadX, y + EmptyHeight + Gap, Mathf.Max(1f, width - CardPadX * 2f), m, state);
                return;
            }

            switch (state.CycleTab)
            {
                case TabGoal:
                    DrawGoalTab(x, y, width, m);
                    break;
                case TabDecision:
                    DrawDecisionTab(x, y, width, m);
                    break;
                default:
                    DrawDemandTab(x, y, width, m, state);
                    break;
            }
        }

        /// <summary>
        /// 页内跳转到决策页签（**唯一入口**）：只改面板会话态 —— 不新开窗口、不写钱、不签约、不派工、不改政策。
        /// 页签更换后的重测量与滚动复位由 <see cref="Draw"/> 的既有失效分支完成
        /// （<c>measure.Tab != state.CycleTab</c> ⇒ 重建测量并把滚动归零），故此处不自行复位滚动。
        /// 已在决策页时为空操作（避免无谓地打断玩家当前的滚动位置）。
        /// </summary>
        private static void JumpToDecision(NeedPanelState state)
        {
            if (state == null || state.CycleTab == TabDecision)
            {
                return;
            }
            state.CycleTab = TabDecision;
        }

        /// <summary>列头（与数据行共用列宽常量与同一 barW 公式，保证永不漂移）。</summary>
        private static void DrawHeaderRow(float x, float y, float width, NeedsCycleMeasure m,
            bool withTarget, bool withGap, string targetHeader)
        {
            Rect row = new Rect(x, y, width, m.RowHeight);
            float barW = BarWidth(row.width, m, withTarget, withGap);
            float nameX = row.x + (m.HasAnyChip ? ChipWidth : 0f);
            RUIF.Draw.Label(new Rect(nameX, row.y, NameWidth, row.height), m.HeaderName, RUIF.Tokens.FontLabel, RUIF.Tokens.Dim, TextAnchor.MiddleLeft);
            RUIF.Draw.Label(new Rect(row.x + NameWidth + Gap + barW + Gap, row.y, PercentWidth, row.height), m.HeaderPercent, RUIF.Tokens.FontLabel, RUIF.Tokens.Dim, TextAnchor.MiddleRight);
            if (!withTarget)
            {
                return;
            }
            RUIF.Draw.Label(new Rect(row.x + row.width - TargetRight(m, withGap) - TargetWidth, row.y, TargetWidth, row.height),
                targetHeader, RUIF.Tokens.FontLabel, RUIF.Tokens.Dim, TextAnchor.MiddleRight);
            if (withGap && m.GapWidth > 0f)
            {
                RUIF.Draw.Label(new Rect(row.x + row.width - m.GapWidth, row.y, m.GapWidth, row.height), m.HeaderGap, RUIF.Tokens.FontLabel, RUIF.Tokens.Dim, TextAnchor.MiddleRight);
            }
        }

        /// <summary>
        /// 单条需求行：优先级芯片 / 名称 / 满足度条 / 满足度 / 目标 / 缺口。
        /// 目标列与缺口列各自独立（需求页签只给目标列 = 长期目标；目标页签两列都给），
        /// 缺口列仅在宽面板下成立。列头与数据行共用 <see cref="BarWidth"/> 与 <see cref="TargetRight"/>。
        /// </summary>
        private static void DrawNeedRow(float x, float y, float width, NeedsCycleMeasure m,
            string name, float ratio01, Color color, string percent, string targetText, string gapText,
            int priority, bool active)
        {
            Rect row = new Rect(x, y, width, m.RowHeight);
            bool withTarget = targetText != null;
            bool withGap = gapText != null;
            float barW = BarWidth(row.width, m, withTarget, withGap);
            float barX = row.x + NameWidth + Gap;
            if (m.HasAnyChip)
            {
                DrawPriorityChip(new Rect(row.x, row.y, ChipWidth, row.height), priority, active);
            }
            RUIF.Draw.Label(new Rect(row.x + (m.HasAnyChip ? ChipWidth : 0f), row.y, NameWidth - (m.HasAnyChip ? ChipWidth : 0f), row.height),
                name, GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            RUIF.ProgressAtoms.CapacityBar(RatioBarRect(row, barW), Mathf.Max(0f, ratio01), 1f, color);
            RUIF.Draw.Label(new Rect(barX + barW + Gap, row.y, PercentWidth, row.height), percent, GameFont.Small, color, TextAnchor.MiddleRight);
            if (!withTarget)
            {
                return;
            }
            RUIF.Draw.Label(new Rect(row.x + row.width - TargetRight(m, withGap) - TargetWidth, row.y, TargetWidth, row.height),
                targetText, RUIF.Tokens.FontLabel, RUIF.Tokens.Muted, TextAnchor.MiddleRight);
            if (withGap && m.GapWidth > 0f)
            {
                RUIF.Draw.Label(new Rect(row.x + row.width - m.GapWidth, row.y, m.GapWidth, row.height), gapText, RUIF.Tokens.FontLabel, RUIF.Tokens.Muted, TextAnchor.MiddleRight);
            }
        }

        /// <summary>
        /// 优先级芯片 + 活跃呼吸点（同一窄列内上下排布）：
        /// 只有影子规划纳入了这项需求时才有 P 序（0 = 安全级 / 1 = 基本 / 2 = 次要）。
        /// 呼吸点只在目标**已激活**时脉动；未激活时画静默点，避免"看起来都在跑"的误导。
        /// </summary>
        private static void DrawPriorityChip(Rect rect, int priority, bool active)
        {
            if (priority > 0)
            {
                var traits = new[] { "P" + priority };
                // P0/P1 属"需要处理"，用警示色；P2 用中性色。TraitChips 的 positive 决定配色方向。
                RUIF.DataAtoms.TraitChips(new Rect(rect.x, rect.y, rect.width, rect.height),
                    traits, new[] { priority <= 1 ? false : true }, 0f);
            }
            if (!active)
            {
                return;
            }
            // 呼吸点：t 用真实时间，未激活项不脉动（行内只画激活项，避免视觉噪声）。
            RUIF.StatusAtoms.PulseDot(RUIF.StatusFmt.DotRect(new Rect(rect.xMax - 6f, rect.y, 6f, rect.height)),
                RUIF.Tokens.Warn, Time.realtimeSinceStartup);
        }

        /// <summary>
        /// **统一的比率条规格（本页唯一入口）**：所有行内百分比条一律
        /// ① 从同一个起点起画 —— 名称/标签列之后，即 <c>row.x + NameWidth + Gap</c>；
        /// ② 高度一律 <see cref="BarHeight"/>，垂直居中于所在行；
        /// ③ 长度由各区按同一个 <see cref="BarWidth"/> 公式算出（列数不同 ⇒ 长度不同，但起点与规格一致）。
        /// 抽成一处是为了杜绝"某区条短一截、起点偏几像素、粗细不一样"这类风格漂移。
        /// 卡片级的主指标条（概览卡）是**有意放大**的视觉层次，不属行内条规格。
        /// </summary>
        private static Rect RatioBarRect(Rect row, float barWidth)
            => new Rect(row.x + NameWidth + Gap, row.y + (row.height - BarHeight) / 2f, barWidth, BarHeight);

        /// <summary>目标列右缘距行右缘的偏移：含（宽面板下的）缺口列时为缺口列宽 + 间距。</summary>
        private static float TargetRight(NeedsCycleMeasure m, bool withGap)
            => withGap && m.GapWidth > 0f ? m.GapWidth + Gap : 0f;

        /// <summary>满足度条可用宽（两种列式页签共用同一公式，列头与数据行同源）。</summary>
        private static float BarWidth(float rowWidth, NeedsCycleMeasure m, bool withTarget, bool withGap)
        {
            float right = withTarget ? TargetWidth + TargetRight(m, withGap) + Gap : 0f;
            float name = NameWidth + (m.HasAnyChip ? ChipWidth : 0f);
            return Mathf.Max(MinimumBarWidth, rowWidth - name - PercentWidth - right - Gap * 2f);
        }
    }
}

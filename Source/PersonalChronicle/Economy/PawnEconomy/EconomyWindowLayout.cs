namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// 经济窗口的**纯几何 + 空态契约**（CHG-20261010-ECON-UI-BATCH 的 F1/F3）。
    ///
    /// <para>零 RimWorld 依赖 ⇒ 可离线断言：柜台工作台的几何（窗口上下限 / 内边距 / 头部 / KPI / 行动区 /
    /// 切换条）与空态键只在这里定义**一次**，绘制路径与离线测试读同一组数字。</para>
    ///
    /// <para>区分：这不是**自研组件/几何**（UI 原子一律走 RUIF），只是把呈现层的几何关系
    /// 从硬编码常量收敛成**可测的函数**；绘制仍全部经 <c>RUIF.Draw.Label</c> /
    /// <c>RUIF.ButtonAtoms.PrimaryButton</c> / <c>RUIF.FeedbackAtoms.EmptyState</c>。</para>
    ///
    /// <para><b>CHG-20261012 · B2（机构政策净删）</b>：政策字段卡网格（<c>PolicyField*</c> /
    /// <c>MinPolicyNameWidth</c> / <c>FitPolicyHead</c>）**已删除** —— 它们服务的政策页（<c>PolicyPanel</c>）
    /// 早在政策 UI 面退役时即无绘制者，只剩离线测试还在断言这些数字（"测试守着死代码"）。
    /// 删除后 <c>Tests/EconomyWindowPresentationTests</c> 的对应用例一并移除。</para>
    /// </summary>
    internal static class EconomyWindowLayout
    {
        /// <summary>P0-2：页首提示行顶端 —— 此前多个承载页各写一份 <c>118f</c>（改一处不生效）。
        /// 收口为唯一来源，各页共用。</summary>
        internal const float PanelHintTop = 118f;

        /// <summary>F1：零行页签必须走既有空态原子，而不是留一张白纸。</summary>
        internal static bool ShowsEmptyState(int rowCount) => rowCount <= 0;

        /// <summary>F1：空态主标题键（完整键名，供绘制与离线测试共用同一常量）。</summary>
        internal const string EmptyStateTitleKey = "PC_PEA.Tab.Empty";
        /// <summary>F1：空态副文案键。</summary>
        internal const string EmptyStateHintKey = "PC_PEA.Tab.EmptyHint";

        /// <summary>F6-A：说明性长句的行种标记 —— 由 <c>EconomyCommands</c> 在构造行时打上，
        /// 呈现层据此用次要色绘制（与数据行区分）。它不是动作动词：带此标记的行没有按钮，
        /// 永远不会进入 <c>Send</c> 通道。</summary>
        internal const string HintRowVerb = "Hint";

        // ── 柜台工作台（P0-6，2026-10-10）—— 纯几何，供绘制与离线断言共用同一组数字 ──────
        /// <summary>窗口上限（LAND-003：只声明上限，实际尺寸由宿主按屏幕夹取）。</summary>
        internal const float WorkspaceMaxWidth = 1440f, WorkspaceMaxHeight = 760f;
        /// <summary>窗口下限（低于此值不再缩，改用行族自身的降级）。</summary>
        internal const float WorkspaceMinWidth = 760f, WorkspaceMinHeight = 560f;
        /// <summary>窗口内容内边距（与聚合窗口一致）。</summary>
        internal const float WorkspacePadding = 14f;
        /// <summary>U1 头部高度（= Tokens.HeaderHeight）。</summary>
        internal const float WorkspaceHeaderHeight = 56f;
        /// <summary>U2 KPI 卡高与卡间隙（LAND-006 宽档：数值 Medium + 标签 Tiny 同行居中）。
        /// 卡宽由窗口实时计算：窗口下限 760 ⇒ 单卡 (760−28−24)/4 ≈ 177 ≥ LAND-006 的 160 阈值，
        /// 因此 4×1 在**全部可达尺寸**内恒为宽档（不存在需要换窄档的尺寸，故无档位分支 —— 见变更标注 §未做项）。</summary>
        internal const float WorkspaceKpiHeight = 52f, WorkspaceKpiGap = 8f;
        /// <summary>U3 行动区高度（= ControlHeight(Small) 的基准；实际绘制走 Measure）。</summary>
        internal const float WorkspaceActionHeight = 30f;
        /// <summary>U3.5 次级页切换条高度（CHG-20261010-ECON-AGG-MIGRATE 批次 B-1）。
        /// 只在快照声明了可承载页时占位（工资结算处 / 体检中心），其余柜台明细区高度与之前逐像素一致。</summary>
        internal const float WorkspaceStripHeight = 26f;

        /// <summary>无表柜台沿用既有上限；有表柜台以全部表的偏好内容宽预算打开宽度。
        /// frameWidth 包含宿主窗口边距、柜台内边距、卡片外/内边距、滚动条与独立详情槽，均由调用方令牌传入。</summary>
        internal static float PreferredWorkspaceWidth(
            System.Collections.Generic.IReadOnlyList<PersonalChronicle.Application.PawnEconomy.WorkspaceTableSection> tables,
            float frameWidth)
        {
            float preferred = 1080f;
            for (int i = 0; i < (tables?.Count ?? 0); i++)
                preferred = System.Math.Max(preferred,
                    PersonalChronicle.Application.PawnEconomy.EconomyTableLayout.TotalWidth(tables[i].Table.Columns) + frameWidth);
            return System.Math.Min(WorkspaceMaxWidth, preferred);
        }

        /// <summary>屏幕优先于窗口下限，避免小屏幕上窗口越界；内容不足交给表横向滚动。</summary>
        internal static float FitWorkspaceWidth(float screenWidth, float preferred)
            => System.Math.Max(1f, System.Math.Min(System.Math.Max(1f, screenWidth - 80f),
                System.Math.Max(WorkspaceMinWidth, System.Math.Min(WorkspaceMaxWidth, preferred))));

        /// <summary>数据行高 = 内容行高 + 卡片双边距；有控件的表另保控件高度。测量和绘制共用。</summary>
        internal static float TableRowHeight(float densityHeight, float controlHeight, float cardPadY)
            => System.Math.Max(densityHeight, controlHeight) + cardPadY * 2f;
    }
}

using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Governance;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// 柜台工作台 —— 单台经济机构（劳务站 / 工坊管理处 / 工资结算处 / 生活用品商店 / 体检中心）的
    /// **独立窗口**（P0-6，2026-10-10）。CHG-20261010-ECON-AGG-MIGRATE 末批后**聚合窗口已删除**：
    /// 原十页全部由柜台/主控承载（机构列表、岗位、库存、账户、台账、报告 → 各柜台或次级页；
    /// 政策 → 所有柜台的承载页；需求 / 照护 / 总览 → 主控窗口经济分页），本窗口只承载该柜台的聚焦面。
    ///
    /// <para><b>零特判（LAND-010）</b>：机构类型差异（KPI / 行动 / 明细行）全部来自
    /// <see cref="GameComponent_PawnEconomy.QueryWorkspace"/> 的读快照，本文件内不出现任何机构类型判断。</para>
    ///
    /// <para><b>复用</b>：明细区走 <see cref="EconomyRowList"/>（与聚合窗口**同一份**测量 / 绘制 / 分页 / 空态），
    /// 卡片骨架与语调色走 <see cref="EconomyUiKit"/>，几何走 <see cref="EconomyWindowLayout"/>。
    /// 绘制路径零 <c>Measure.Width</c>（全部在 60 tick 快照刷新期预量）。</para>
    ///
    /// <para><b>F4 纪律</b>：明细行**不挂按钮**。旧动词（撤下 / 解约 / 上架 / 指派 / 体检 / 补发）的写路径已由
    /// <c>Execute</c> 的 <c>LegacyFeesAllowed</c> 门整体关闭，挂上去就是"点了没用"的按钮。</para>
    /// </summary>
    public sealed partial class Window_InstitutionWorkspace : Window
    {
        private readonly Map map;
        private readonly Building_EconomyInstitution anchor;
        private readonly string anchorKey;
        private readonly EconomyRowListState rowList = new EconomyRowListState();
        private WorkspaceView view;
        private int lastTick = -99999;
        // CHG-20261010-ECON-AGG-MIGRATE 批次 B-1：次级页承载（工资结算处 → 台账；体检中心 → 报告）。
        // -1 = 显示本柜台业务行（既有行为）；>= 0 = 显示该聚合窗口页签的行族视图（同一读入口、同一行族）。
        private int hostedTab = -1;
        private EconomyView hostedView;
        private EconomyRowListState hostedRows = new EconomyRowListState();
        // S2b：切换条改为**分段件**（选中段可辨）—— 文案数组与总宽在快照期构造（绘制期零分配/零 Measure）。
        private string[] hostedStripOptions;
        private float hostedStripWidth;
        // B-2：承载页的**写上下文**（仅当该页 NeedsCommandHost）。操作人索引与聚合窗口同型（取模回绕，
        // 操作人列表变化不会越界）；国库戳是**并发令牌**，必须与视图同一时刻取样、命令提交时原样回传
        //（ExecuteTreasuryCommand 首行校验），且每次提交后立即失效重取。
        private int hostedActor;
        private string hostedStamp;
        private string hostedActorLabel;
        private float hostedActorWidth;
        // C-1：承载页**焦点**（导航动词 `Focus` 就地重定；null ⇒ 用本柜台 anchorKey）与机构卡列表组件
        //（与聚合窗口共用同一实现；懒建）。**柜台自身的 KPI / 行动始终读 anchorKey，不受焦点影响**。
        private string hostedFocus;
        private EconomyInstitutionCards hostedCards;
        private EconomyInstitutionCards HostedCards
            => hostedCards ?? (hostedCards = new EconomyInstitutionCards(map));
        // CHG-20261012-ECON-CONTRACTIZE：政策页内容层（PolicyPanel）已随用户裁决「移除全部的机构政策」
        // 整体退役 —— 宿主不再持有它的实例（类型与文件的净删见同批收尾段）。
        // 预量缓存（60 tick 快照期重算；绘制期只读 —— PERF-002）
        private string[] kpiValues, kpiLabels;
        private float[] kpiValueWidths;
        private string[] actionLabels;
        private float[] actionWidths;
        private float kindLabelWidth, phaseChipWidth;

        private static string W(string key) => EconomyLabels.TranslatedOrId("PC_PEA." + key, key);

        public Window_InstitutionWorkspace(Map map, Building_EconomyInstitution anchor)
        {
            this.map = map; this.anchor = anchor;
            anchorKey = anchor?.GetUniqueLoadID();
            doCloseX = true; absorbInputAroundWindow = true;
        }

        /// <summary>LAND-003：只声明**上限**，实际尺寸按屏幕夹取（不写死常数）。
        /// ⚠ 必须写 <c>Verse.UI</c>：本工程存在 <c>PersonalChronicle.Economy.UI</c> 命名空间，
        /// 裸 <c>UI</c> 会被解析成那个命名空间（CS0234）。</summary>
        private float initialPreferredWidth;
        public override Vector2 InitialSize
        {
            get
            {
                if (initialPreferredWidth <= 0f)
                {
                    // 打开时读一次全部表声明；窗口尺寸来自数据，无机构/页签号特判。绘制快照仍走 Refresh。
                    var initialView = GameComponent_PawnEconomy.Current?.QueryWorkspace(map, anchorKey);
                    float frame = Margin * 2f + EconomyWindowLayout.WorkspacePadding * 2f
                        + TableFrameWidth + TableDetailGutter;
                    initialPreferredWidth = EconomyWindowLayout.PreferredWorkspaceWidth(initialView?.Tables, frame);
                }
                return new Vector2(EconomyWindowLayout.FitWorkspaceWidth(Verse.UI.screenWidth, initialPreferredWidth),
                    Mathf.Min(Mathf.Max(1f, Verse.UI.screenHeight - 80f), EconomyWindowLayout.WorkspaceMaxHeight));
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            var runtime = GameComponent_PawnEconomy.Current;
            if (runtime == null || map == null || anchor == null || anchor.Destroyed || !anchor.Spawned || anchor.Map != map)
            { Close(); return; }
            if (view == null || Find.TickManager.TicksGame - lastTick >= 60) Refresh(runtime, rect.width);
            if (view == null) return;
            var content = RUIF.Layout.Inset(rect, EconomyWindowLayout.WorkspacePadding, EconomyWindowLayout.WorkspacePadding);
            var column = RUIF.Layout.Column(content, RUIF.Tokens.SpaceSm);
            try
            {
                DrawHeader(column.Take(EconomyWindowLayout.WorkspaceHeaderHeight), view);
                DrawKpis(column.Take(EconomyWindowLayout.WorkspaceKpiHeight), view);
                // 行动命中：本帧立即返回（深链或重新核验已改变窗口栈/快照）。
                if (DrawActions(column.Take(EconomyWindowLayout.WorkspaceActionHeight), view)) return;
                // B-1：次级页切换条（只在快照声明了可承载页时占位 —— 其余柜台的明细区逐像素不变）。
                if (view.SecondaryTabs.Count > 0)
                    DrawStrip(column.Take(EconomyWindowLayout.WorkspaceStripHeight));
                DrawRows(column.Rest);
            }
            catch (System.Exception error)
            { Log.ErrorOnce("[PC][UI] institution workspace failed: " + error, 0x1EC0DF1); }
        }

        /// <summary>60 tick 快照：读模型 + 全部绘制期要用的文字宽度（绘制路径零 Measure.Width）。</summary>
        private void Refresh(GameComponent_PawnEconomy runtime, float windowWidth)
        {
            view = runtime.QueryWorkspace(map, anchorKey);
            lastTick = Find.TickManager.TicksGame;
            // S2b：表格区段（劳务站 = 工作目录 / 工作合同两张表）。会话态按**区段 Key** 寻址
            //（每表一份、切换互不串；首次见到该 Key 才按读模型默认值初始化）；随后构建**派生行视图**
            //（排序 + 筛选 + 搜索在快照期一次完成，绘制期只读）。
            tableRows = null;
            if (view.Tables.Count > 0)
            {
                int index = IndexOfTableSection(activeTableKey);
                if (index < 0) { index = 0; activeTableKey = view.Tables[0].Key; }
                var section = view.Tables[index];
                if (!tableStates.TryGetValue(section.Key, out var state))
                {
                    state = new EconomyTableState();
                    state.Reset(section.Table.SortColumnDefault);
                    tableStates[section.Key] = state;
                }
                tableRows = new EconomyView(view.Rows.Revision, view.Rows.Enabled, view.Rows.Funded, view.Rows.Status,
                    view.Rows.Focus, EconomyTableOps.Apply(section.Rows, state),
                    new System.Collections.Generic.List<EconomyRow>(view.Rows.Actors));
                // PERF-002：工具栏 / 筛选条 / 表头的文本与宽度、列宽布局、展开详情折行**全部在此预量**
                //（绘制路径零 Measure.Width、零拼接；拖拽帧是唯一例外，见 AdvanceTableResize）。
                float areaWidth = Mathf.Max(1f, windowWidth - EconomyWindowLayout.WorkspacePadding * 2f);
                RefreshTableChrome(section, state, areaWidth);
                BuildDetailCache(section);
            }
            int kn = view.Kpis.Count;
            kpiValues = new string[kn]; kpiLabels = new string[kn]; kpiValueWidths = new float[kn];
            for (int i = 0; i < kn; i++)
            {
                kpiValues[i] = view.Kpis[i].Value;
                kpiLabels[i] = view.Kpis[i].Label;
                kpiValueWidths[i] = RUIF.Measure.Width(kpiValues[i], GameFont.Medium);
            }
            int an = view.Actions.Count;
            actionLabels = new string[an]; actionWidths = new float[an];
            for (int i = 0; i < an; i++)
            {
                actionLabels[i] = view.Actions[i].Label;
                actionWidths[i] = EconomyRowList.RowButtonWidth(actionLabels[i]);
            }
            kindLabelWidth = RUIF.Measure.Width(view.KindLabel, GameFont.Tiny);
            // B2：营业芯片与营业时段文本随机构政策退役 ⇒ 头部只留「类型 · 制度阶段」两段（快照期预量不变）。
            phaseChipWidth = RUIF.Measure.Width(view.PhaseText, GameFont.Tiny) + RUIF.Tokens.SpaceSm;
            // B-1：切换条文字宽度（绘制路径零 Measure.Width）；次级页视图随快照一并失效重读。
            hostedView = null;
            if (hostedTab >= view.SecondaryTabs.Count) hostedTab = -1;
            int tabCount = view.SecondaryTabs.Count;
            // S2b：切换条 = 分段件（「本柜台」+ 次级页）—— 文案数组与段宽在快照期算好；
            // 段宽取各段"按钮级宽度"的最大值（等分），总宽 = 段宽 × 段数（窄窗由绘制侧压缩，见 DrawStrip）。
            hostedStripOptions = new string[tabCount + 1];
            hostedStripOptions[0] = W("Workspace.Detail.Primary");
            float widest = EconomyRowList.RowButtonWidth(hostedStripOptions[0]);
            for (int i = 0; i < tabCount; i++)
            {
                hostedStripOptions[i + 1] = view.SecondaryTabs[i].Label;
                float tab = EconomyRowList.RowButtonWidth(hostedStripOptions[i + 1]);
                if (tab > widest) widest = tab;
            }
            hostedStripWidth = widest * (tabCount + 1);
            // B-2：承载页需要写上下文时，在此把视图（含操作人列表）与国库戳一并取样 ——
            // 与绘制期共用同一份缓存（EnsureHostedView），因此不增加额外读成本。
            hostedActorLabel = null;
            if (hostedTab >= 0 && hostedTab < view.SecondaryTabs.Count && view.SecondaryTabs[hostedTab].NeedsCommandHost)
            {
                var hosted = EnsureHostedView(runtime);
                if (hosted != null)
                {
                    hostedActorLabel = ActorChipText(hosted);
                    hostedActorWidth = RUIF.Measure.Width(hostedActorLabel, GameFont.Small);
                }
            }
        }

        /// <summary>U1 头部：柜台图标 + 名称（Medium）+「类型 · 制度阶段芯片」（Tiny）。
        /// B2：营业芯片与营业时段随机构政策退役（政策净删后它们没有权威来源）。</summary>
        private void DrawHeader(Rect rect, WorkspaceView view)
        {
            RUIF.Draw.Box(rect, RUIF.Tokens.Panel);
            RUIF.Draw.Border(rect, RUIF.Tokens.BorderHair);
            float iconSide = rect.height - RUIF.Tokens.SpaceXs * 2f;
            var icon = new Rect(rect.x + RUIF.Tokens.CardPadX, rect.y + RUIF.Tokens.SpaceXs, iconSide, iconSide);
            DrawAnchorIcon(icon);
            var column = RUIF.Layout.Column(new Rect(icon.xMax + RUIF.Tokens.SpaceSm, rect.y + RUIF.Tokens.SpaceXxs,
                Mathf.Max(1f, rect.xMax - icon.xMax - RUIF.Tokens.SpaceSm - RUIF.Tokens.CardPadX),
                rect.height - RUIF.Tokens.SpaceXs * 2f + RUIF.Tokens.SpaceXxs), 2f);
            // LAND-008：四处可截断文本全部配悬停全文（省略号 + tooltip 成对，缺一即劣化）；文本在快照期已拼好。
            var titleRect = column.Take(RUIF.Measure.RowHeight(GameFont.Medium));
            RUIF.Draw.EllipsisLabel(titleRect, view.Title, GameFont.Medium, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            if (Mouse.IsOver(titleRect)) TooltipHandler.TipRegion(titleRect, view.Title + " · " + view.KindLabel);
            var sub = column.Take(RUIF.Measure.RowHeight(GameFont.Tiny));
            float x = sub.x;
            var kindRect = new Rect(x, sub.y, Mathf.Min(kindLabelWidth, sub.width), sub.height);
            RUIF.Draw.EllipsisLabel(kindRect, view.KindLabel, GameFont.Tiny, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            if (Mouse.IsOver(kindRect)) TooltipHandler.TipRegion(kindRect, view.KindLabel);
            x = kindRect.xMax + RUIF.Tokens.SpaceXs;
            var phaseChip = new Rect(x, sub.y, phaseChipWidth, sub.height);
            if (phaseChip.xMax <= sub.xMax)
            {
                var drawnPhase = EconomyUiKit.StatusChip(phaseChip, view.PhaseText, EconomyUiKit.ToneColor(view.PhaseTone), phaseChipWidth);
                if (Mouse.IsOver(drawnPhase)) TooltipHandler.TipRegion(drawnPhase, view.PhaseText);
            }
            // B2：此处原有的「营业芯片（openChip）+ 营业时段文本」随机构政策退役 —— 两者都是政策读数，
            // 政策净删后留下它们就只能靠发明数据（M5）。制度阶段芯片保留：它是"为什么现在不能花钱"的权威答案。
        }

        /// <summary>U2 KPI：一行 N 卡（LAND-006 宽档 —— 数值 Medium 在前、标签 Tiny 在后，同行居中）。
        /// 值宽按预量取，标签拿剩余宽；两者都走省略号 + 悬停全文（LAND-008）。</summary>
        private void DrawKpis(Rect rect, WorkspaceView view)
        {
            int n = view.Kpis.Count;
            if (n <= 0) return;
            float gap = EconomyWindowLayout.WorkspaceKpiGap;
            float cardW = (rect.width - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                var card = new Rect(rect.x + i * (cardW + gap), rect.y, cardW, rect.height);
                var kpi = view.Kpis[i];
                bool hovered = Mouse.IsOver(card);
                RUIF.Draw.Box(card, hovered ? RUIF.Tokens.CardHover : RUIF.Tokens.Card);
                RUIF.Draw.Border(card, hovered ? RUIF.Tokens.Border : RUIF.Tokens.BorderSoft);
                RUIF.Draw.Box(new Rect(card.x, card.y, EconomyUiKit.AccentBarWidth, card.height), EconomyUiKit.ToneColor(kpi.Tone));
                float avail = Mathf.Max(1f, card.width - RUIF.Tokens.CardPadX * 2f);
                float valueW = Mathf.Min(kpiValueWidths[i], avail * 0.6f);
                var valueRect = new Rect(card.x + RUIF.Tokens.CardPadX, card.y, valueW, card.height);
                RUIF.Draw.EllipsisLabel(valueRect, kpiValues[i], GameFont.Medium, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
                var labelRect = new Rect(valueRect.xMax + RUIF.Tokens.SpaceXs, card.y,
                    Mathf.Max(0f, card.xMax - RUIF.Tokens.CardPadX - valueRect.xMax - RUIF.Tokens.SpaceXs), card.height);
                if (labelRect.width > 1f)
                    RUIF.Draw.EllipsisLabel(labelRect, kpiLabels[i], GameFont.Tiny, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
                if (hovered) TooltipHandler.TipRegion(card, kpiLabels[i] + ": " + kpiValues[i]);
            }
        }

        /// <summary>U3 行动区：只列**真正放行**的动词（来自读快照）。返回 true = 本帧命中。</summary>
        private bool DrawActions(Rect rect, WorkspaceView view)
        {
            int n = view.Actions.Count;
            if (n <= 0)
            {
                RUIF.Draw.Label(rect, W("Workspace.NoActions"), GameFont.Small, RUIF.Tokens.Dim, TextAnchor.MiddleLeft);
                return false;
            }
            float x = rect.x;
            for (int i = 0; i < n; i++)
            {
                float width = Mathf.Max(RUIF.Tokens.SpaceLg * 4f, i < actionWidths.Length ? actionWidths[i] : 0f);
                if (x + width > rect.xMax) break;
                var button = new Rect(x, rect.y, width, rect.height);
                if (RUIF.ButtonAtoms.PrimaryButton(button, actionLabels[i], true, RUIF.ButtonTone.Default)
                    && HandleAction(view.Actions[i])) return true;
                x += width + RUIF.Tokens.SpaceXs;
            }
            // CHG-20261010-ECON-LABOR-UI2（A）：按钮右侧的**只读**参数说明（如"每份 32"）。
            // 玩家点 ± 后在同一行立刻看到值变化，不必往下读明细行的"每份"字段。
            if (!string.IsNullOrEmpty(view.ActionNote) && x < rect.xMax)
            {
                var noteRect = new Rect(x + RUIF.Tokens.SpaceSm, rect.y,
                    Mathf.Max(0f, rect.xMax - x - RUIF.Tokens.SpaceSm), rect.height);
                if (noteRect.width > 1f)
                {
                    RUIF.Draw.EllipsisLabel(noteRect, view.ActionNote, GameFont.Tiny, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
                    // LAND-008：省略号与悬停全文成对，缺一即劣化。
                    if (Mouse.IsOver(noteRect)) TooltipHandler.TipRegion(noteRect, view.ActionNote);
                }
            }
            return false;
        }

        /// <summary>行动：核验就地执行并如实回报；「每份件数」就地调节。
        /// <para>B-2 / C-2：**政策与账户都已改为承载页**（切换条）⇒ 行动区不再保留任何深链动作
        ///（同一窗口不留两个同名入口），本窗口因此不再需要「点了没反应」以外的深链语义。</para></summary>
        private bool HandleAction(WorkspaceAction action)
        {
            switch (action.Verb)
            {
                case WorkspaceVerb.WorkPrices:
                    Find.WindowStack.Add(new Dialog_WorkPrices(anchorKey));
                    lastTick = -99999;
                    return true;
                case WorkspaceVerb.Reverify:
                    ReverifyNow();
                    return true;
                default: return false;
            }
        }

        /// <summary>U3.5 次级页切换（B-1）：「本柜台」+ 快照声明的次级页。
        /// 标签与页签号都来自读快照 ⇒ 窗口内零机构类型判断（LAND-010）。
        /// S2b：选中态改用**分段件**（<c>InputAtoms.Segmented</c>）—— 原实现用
        /// <c>PrimaryButton + Accent/Default</c>，而 RUIF 原子下两者同色 ⇒ 当前页不可辨（GATE-UI P1 同源劣化）；
        /// 重复点击当前页不改变状态（避免无意义重读）。</summary>
        private void DrawStrip(Rect rect)
        {
            float x = rect.x;
            if (hostedStripOptions != null && hostedStripOptions.Length > 0)
            {
                // 窄窗降级：分段件等分，总宽超出可用区时压缩到可用区（各段变窄但入口仍在 ——
                // 切换条是唯一的次级页入口，整体隐藏会比变窄更糟）。
                float width = Mathf.Min(hostedStripWidth, rect.width);
                int index = hostedTab + 1;   // 分段件下标：0 = 本柜台，i+1 = 第 i 个次级页
                if (RUIF.InputAtoms.Segmented(new Rect(x, rect.y, width, rect.height), hostedStripOptions, ref index)
                    && index - 1 != hostedTab)
                {
                    hostedTab = index - 1;
                    hostedView = null;
                    // 行族滚动位置属于"上一页"—— 换页必须归零，否则新页会继承别页的滚动（视线错位）；
                    // 顺带丢弃上一页的测量缓存（不同页行高不同，复用会导致首帧几何错位）。
                    hostedRows = new EconomyRowListState();
                    // 强制下一帧走 60 tick 快照期：切换条新增的按钮宽度、操作人芯片宽度都在那里预量
                    //（绘制路径零 Measure.Width，PERF-002）。
                    lastTick = -99999;
                }
                x += width + RUIF.Tokens.SpaceXs;
            }
            // B-2：可写承载页的**操作人选择器**（右侧；点击循环，与聚合窗口同语义：取模回绕 +
            // 无操作人时 NoActor）。国库戳是内部并发令牌，不上屏（聚合窗口同样不显示）。
            var activeHosted = hostedTab >= 0 && hostedTab < view.SecondaryTabs.Count ? view.SecondaryTabs[hostedTab] : null;
            if (activeHosted == null || !activeHosted.NeedsCommandHost || hostedActorLabel == null) return;
            float chipWidth = Mathf.Max(RUIF.Tokens.SpaceLg * 4f, hostedActorWidth + RUIF.Tokens.SpaceMd * 2f);
            var chip = new Rect(Mathf.Max(x, rect.xMax - chipWidth), rect.y, chipWidth, rect.height);
            if (chip.x + chip.width > rect.xMax) return;
            if (RUIF.ButtonAtoms.PrimaryButton(chip, hostedActorLabel, true, RUIF.ButtonTone.Default))
            {
                hostedActor++;
                lastTick = -99999;   // 立即重取样（与聚合窗口"点一下重读一次"同型）
            }
            // LAND-008：按钮文本可能被截断，悬停给全文。
            if (Mouse.IsOver(chip)) TooltipHandler.TipRegion(chip, hostedActorLabel);
        }

        /// <summary>U4 明细：只读行族。CHG-20261010-ECON-LABOR-UI2（C/D）：带进度的行（待办覆盖率、
        /// 合同完成度）走**自定义行回调**画进度条 —— 复用行族既有的自定义行通道，
        /// 共用行族（聚合窗口同样使用）**零改动、零回归**。
        /// B-1：次级页激活时本区改画该页的行族视图（同一读入口与同一行族）。</summary>
        private void DrawRows(Rect area)
        {
            if (hostedTab >= 0) { DrawHostedRows(area); return; }
            if (view?.Rows == null) return;
            // S2b：有表格区段时，明细区 = 工具栏（表切换 / 搜索 / 密度）+ 筛选条（仅当有筛选面）+
            // 表头（可点三态排序 + 列宽拖拽）+ 列化行（行族仍负责滚动/分页/空态）。
            if (view.Tables.Count > 0 && tableRows != null) { DrawTableArea(area); return; }
            EconomyRowList.Draw(area, view.Rows, rowList,
                row => row.Progress >= 0f ? ProgressRowHeight : 0f,
                (content, y, row) => { if (row.Progress >= 0f) DrawProgressRow(content, y, row); return false; },
                null);
        }
        /// <summary>B-1：次级页的明细区 —— 与聚合窗口**同一读入口**（<c>runtime.Query(map, anchorKey, tab)</c>）
        /// 与**同一行族**（<c>EconomyRowList</c>），因此台账 / 报告在柜台窗口里与在聚合窗口里同族同排；
        /// 本窗口只提供宿主与滚动状态，不复制该页任何排版或命令语义（写路径仍在原页，见 F4 纪律）。
        /// 视图按 60 tick 快照失效重读（<c>Refresh</c> 已置 <c>hostedView = null</c>）。</summary>
        private void DrawHostedRows(Rect area)
        {
            var runtime = GameComponent_PawnEconomy.Current;
            if (runtime == null) return;
            var hosted = EnsureHostedView(runtime);
            if (hosted?.Rows == null) return;
            // 行族第二参数收的是**视图**（它内部再用 `view.Rows` 度量与分页），与本窗口既有写法一致。
            // B-2：需要写上下文的承载页（账户）挂行尾按钮；C-1：含**导航**动词的承载页（机构列表）也挂
            //（`HostsRowVerbs` = 两个能力位的或 —— 判据若只写 NeedsCommandHost，机构页的「政策」「配置」
            // 按钮会被静默丢掉）。只读承载页（台账 / 报告）传 null（F4：不搬"点了没用"的按钮进柜台）。
            var rowAction = view.SecondaryTabs[hostedTab].HostsRowVerbs
                ? new System.Func<Rect, EconomyRow, bool>(DrawHostedRowButton) : null;
            // C-1：机构卡列表组件**无条件挂上** —— 它是数据驱动的（非机构行返回高 0 且不绘制、不命中），
            // 因此柜台无需判断"当前是哪一页"（LAND-010：呈现层零页签特判）。
            var cards = HostedCards;
            EconomyRowList.Draw(area, hosted, hostedRows,
                row => cards.HeightOf(row),
                (content, y, row) =>
                {
                    if (!cards.Draw(hosted, content, y, row)) return false;
                    OnHostedVerb(row.Verb, row.Id, row.Key);
                    return true;
                },
                rowAction);
        }
        /// <summary>B-2：承载页视图与**写上下文**的取样点（唯一）—— 视图重建时同批重取国库戳，
        /// 保证提交的戳与屏幕上看到的状态同源（戳过期即被命令首行拒绝，与聚合窗口同型）。</summary>
        private EconomyView EnsureHostedView(GameComponent_PawnEconomy runtime)
        {
            if (hostedTab < 0 || hostedTab >= view.SecondaryTabs.Count) return null;
            if (hostedView == null)
            {
                // ⚠ `hostedTab` 是**切换条位置**（-1 = 本柜台），`Query` 的第三参是**聚合页签号** ——
                // 翻译只走 `WorkspaceView.HostedTabIndex`（纯函数、有离线断言）。
                // GATE-ARCH（2026-10-10）P0：曾把位置直接当页签号（位置 0 ⇒ BuildInstitutions），
                // 整页错且写动词永不出现，而编译与单测都不会报错。
                int tabIndex = view.HostedTabIndex(hostedTab);
                if (tabIndex < 0) return null;
                // C-1：焦点默认 = 本柜台（anchorKey）；被承载页的 `Focus` 动词重定后改用之
                //（与聚合窗口"焦点是窗口级选择"同语义）。
                hostedView = runtime.Query(map, hostedFocus ?? anchorKey, tabIndex);
                hostedStamp = runtime.QueryTreasuryStamp();
            }
            return hostedView;
        }
        /// <summary>操作人芯片文案（缺操作人时用既有 <c>NoActor</c> 键 —— 与聚合窗口同词）。</summary>
        private string ActorChipText(EconomyView hosted)
        {
            int n = hosted?.Actors?.Count ?? 0;
            string actor = n == 0 ? W("NoActor") : hosted.Actors[hostedActor % n].Text;
            return W("SelectedActor") + ": " + actor;
        }
        /// <summary>承载页行尾按钮：绘制 + 分发（重新核验就地执行；其余走写通道）。
        /// 色调走行族的**唯一规则**（<c>EconomyRowList.RowButtonTone</c>），与聚合窗口逐像素同色。</summary>
        private bool DrawHostedRowButton(Rect buttonRect, EconomyRow row)
        {
            if (!RUIF.ButtonAtoms.PrimaryButton(buttonRect, row.Action, true, EconomyRowList.RowButtonTone(row.Verb)))
                return false;
            OnHostedVerb(row.Verb, row.Id, row.Key);
            return true;
        }
        /// <summary>承载页的**动词分发（唯一入口）**：卡内按钮 / 行尾按钮 / 整卡命中都走它。
        /// 按 <see cref="WorkspaceSecondaryTab"/> 的契约分三类：
        /// ① <c>Reverify</c> 就地核验；② **导航** —— <c>Focus</c> 就地重定承载页焦点、<c>Policy</c> 打开该机构
        /// 的政策页（C-2 把政策页搬进柜台后改为就地切页签，**此处是唯一改点**）；
        /// ③ 其余 → 写通道 <see cref="HostedSend"/>（越界动词由命令侧 fail-closed 拒绝并如实回报）。</summary>
        private void OnHostedVerb(string verb, long id, string key)
        {
            if (verb == "Reverify") { ReverifyNow(); return; }
            if (verb == "Focus")
            {
                // 与聚合窗口同义：焦点改为该机构（承载页据此重取）。柜台自身的 KPI / 行动不受影响 ——
                // 它们始终读本柜台的 anchorKey（工作台查询与承载页查询是两个独立入口）。
                hostedFocus = key; view = null; return;
            }
            // CHG-20261012-ECON-CONTRACTIZE：`Policy` 导航动词已随政策页退役而删除 ——
            // 机构卡上的「政策」按钮同步移除（见 EconomyInstitutionCards），不留"点了没反应"的入口（F4）。
            HostedSend(verb, id, key);
        }
        /// <summary>承载页的写分发 —— 与聚合窗口 <c>Send</c> **同语义**（同一套命令与戳校验）：
        /// <c>Treasury.*</c> 走国库命令（带新鲜戳），其余走 <c>Execute</c>（带本页修订号）。
        /// 拒绝时如实回报同一条消息；提交后视图失效 ⇒ 下一帧重读（余额 / 待办 / 戳立即反映）。
        /// 末批删除聚合窗口后，本方法即唯一实现。</summary>
        private void HostedSend(string verb, long id, string key)
        {
            var runtime = GameComponent_PawnEconomy.Current;
            if (runtime == null || hostedView == null) return;
            int n = hostedView.Actors.Count;
            string actorKey = n == 0 ? null : hostedView.Actors[hostedActor % n].Key;
            bool accepted = verb?.StartsWith("Treasury.", System.StringComparison.Ordinal) == true
                ? runtime.ExecuteTreasuryCommand(verb, checked((int)id), key, actorKey, hostedStamp)
                : runtime.Execute(verb, id, key, actorKey, hostedView.Revision);
            if (!accepted)
                Messages.Message(W("Rejected"), MessageTypeDefOf.RejectInput, false);
            view = null;
        }
        /// <summary>显式重新核验（就地执行并如实回报）—— 柜台行动区与承载页行按钮**共用同一实现**，
        /// 避免"两个入口两种回报"。</summary>
        private void ReverifyNow()
        {
            var runtime = GameComponent_PawnEconomy.Current;
            if (runtime == null) return;
            var code = runtime.ReVerifyInstitutionMode();
            bool cleared = code == InstitutionReverifyCode.Cleared;
            Messages.Message(cleared ? W("Mode.Reverified") : W("Mode.FaultHint"),
                cleared ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput, false);
            view = null;
        }

        /// <summary>进度行几何（单点定义 —— 与数据行共用同一卡片边距 <c>EconomyWindowLayout.CardSideInset</c>）。
        /// GATE-UI（2026-10-10）P3-a：此处不再散落字面量；跨文件迁移到 <c>EconomyWindowLayout</c>
        /// 需同步其"纯几何、可离线断言"的既有契约，留待与其余柜台几何一并收口。</summary>
        private const float ProgressRowHeight = 46f;      // 整行高（文本 + 轨道 + 内边距）
        private const float ProgressTextHeight = 20f;     // 文本行高（其余留给轨道）
        private const float ProgressBarThickness = 6f;    // 轨道厚度
        private const float ProgressBarTopGap = 3f;       // 文本与轨道间距
        /// <summary>进度行：文本一行 + 细进度条一行（用 RUIF 容量条原子，含超载虚线段语义）。</summary>
        private void DrawProgressRow(Rect content, float y, EconomyRow row)
        {
            // 与数据行**同族**（GATE-UI P3-b）：同一卡片边距 + EconomyUiKit.CardFrame（含悬停提升底
            // 与描边）+ **卡级**悬停全文（与数据行一致，而非文本级）。
            var card = new Rect(content.x + EconomyRowList.CardSideInset, y,
                Mathf.Max(1f, content.width - EconomyRowList.CardSideInset * 2f), ProgressRowHeight);
            bool hovered = Mouse.IsOver(card);
            var inner = EconomyUiKit.CardFrame(card, hovered, null);
            var textRect = new Rect(inner.x, inner.y, Mathf.Max(1f, inner.width), ProgressTextHeight);
            RUIF.Draw.EllipsisLabel(textRect, row.Text, GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            var bar = new Rect(inner.x, textRect.yMax + ProgressBarTopGap,
                Mathf.Max(1f, inner.width), ProgressBarThickness);
            // ⚠ 必须**显式**传色（工程纪律，见 RuifBridge 的既有说明）：RUIF 的默认取色是
            // `>85% ⇒ Warn`，而这里的语义是"完成度 / 派发覆盖率"—— 越高越好，用默认色会把 100% 画成警示橙。
            RUIF.ProgressAtoms.CapacityBar(bar, row.Progress, 1f, RUIF.Tokens.Alive);
            if (hovered) TooltipHandler.TipRegion(card, row.Text);
        }

        /// <summary>柜台图标 = 该 ThingDef 的 <c>uiIconPath</c>，按比例居中绘制（不拉伸）；缺图显示占位符而非隐藏。</summary>
        private void DrawAnchorIcon(Rect rect)
        {
            RUIF.Draw.Box(rect, RUIF.Tokens.PanelRaised);
            RUIF.Draw.Border(rect, RUIF.Tokens.BorderSoft);
            string path = anchor?.def?.uiIconPath;
            var texture = path.NullOrEmpty() ? null : ContentFinder<Texture2D>.Get(path, false);
            if (texture == null)
            { RUIF.Draw.Label(rect, "—", GameFont.Tiny, RUIF.Tokens.Dim, TextAnchor.MiddleCenter); return; }
            float side = Mathf.Max(1f, Mathf.Min(rect.width, rect.height) - 4f);
            var inner = new Rect(rect.center.x - side * 0.5f, rect.center.y - side * 0.5f, side, side);
            GUI.DrawTexture(inner, texture, ScaleMode.ScaleToFit);
        }
    }
}

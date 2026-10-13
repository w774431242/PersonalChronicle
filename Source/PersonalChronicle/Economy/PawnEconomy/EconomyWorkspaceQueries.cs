using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Automation;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Governance;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// 柜台工作台（<c>Window_InstitutionWorkspace</c>）的**唯一读入口**（P0-6，2026-10-10）。
    ///
    /// <para><b>为什么在这里组装</b>：机构类型差异（显示哪些 KPI / 哪些行动 / 哪些明细行）属**结构信息**，
    /// 必须经契约声明而不是在绘制代码里做 <c>if (kind == …)</c>（LAND-010）。本文件属 **Economy 模块的读模型组装**
    /// （物理层 ≠ `Application/`；后者零 UnityEngine/RUIF，见 GATE-ARCH 2026-10-11 的措辞勘误），
    /// 呈现层只接收 <see cref="WorkspaceView"/> 并按它排版。</para>
    ///
    /// <para><b>写路径纪律</b>：本文件只读。行动只列**真正放行**的项（显式重新核验 / 国库缴存 / 国库提取
    /// 等，见各柜台分支）；旧动词的写路径已由 <c>Execute</c> 的 <c>LegacyFeesAllowed</c> 门整体关闭，
    /// 工作台不挂这些按钮（F4）。</para>
    ///
    /// <para><b>归属口径（如实）</b>：CHG-20261012 · B2 起岗位按**地图**归属（政策绑定的车间区随政策退役）——
    /// 本图全部岗位与落在其上的活跃合同；凭证（<c>EconomyReceipt</c>）与体检报告（<c>EconomyReport</c>）
    /// **没有机构归属字段**，因此本页给出的是全殖民口径并显式标注，不做伪归属。</para>
    /// </summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        internal WorkspaceView QueryWorkspace(Map map, string anchorKey)
        {
            var anchor = anchors.FirstOrDefault(a => a != null && a.Spawned && a.Map == map && a.GetUniqueLoadID() == anchorKey);
            if (map == null || anchorKey.NullOrEmpty())
                return new WorkspaceView(State.Revision, T("NoFocus"), string.Empty, string.Empty,
                    WorkspaceTone.Dim, new List<WorkspaceKpi>(), new List<WorkspaceAction>(),
                    new EconomyView(State.Revision, State.Enabled, State.Funded, string.Empty, T("NoFocus"), new List<EconomyRow>(), new List<EconomyRow>()));
            var kind = anchor?.def?.GetModExtension<EconomyInstitutionExtension>()?.kind ?? InstitutionKind.LaborOffice;
            var mode = QueryInstitutionMode();
            string phaseText = T("Mode.Phase." + mode.Phase);
            var phaseTone = ToneOf(mode.Phase);
            int mapId = map.uniqueID;
            var kpis = new List<WorkspaceKpi>();
            // C-2：**政策页对所有柜台开放为承载页**（见 SecondaryTabsFor）—— 每个柜台都能就地编辑
            // 自己（与全局）的机构政策，因此行动区的「政策」深链动作删除：同一窗口不需要两个同名入口，
            // 且深链会把玩家送去聚合窗口看同一页。行动区从此只列**柜台局部**动作（核验 / 每份件数）。
            var actions = new List<WorkspaceAction>();
            if (mode.Phase == InstitutionPhase.Faulted)
                actions.Add(new WorkspaceAction(T("Mode.Reverify"), WorkspaceVerb.Reverify));
            // B2（政策净删）：岗位归属**不再**来自"政策绑定的车间区"（那条关联随政策退役）⇒ 改**地图口径**：
            // 本图全部岗位 + 落在这些岗位上的活跃合同数。诚实性：本页此前也只是"按政策绑定的那一区"统计，
            // 现在口径变大 = 更完整，不是发明数据。
            var mapPositions = State.All<EconomyPosition>().Where(p => p.MapId == mapId).ToList();
            var mapPositionIds = new HashSet<long>(mapPositions.Select(p => p.Id));
            int contractCount = State.All<EconomyContract>().Count(c => c.Active && mapPositionIds.Contains(c.PositionId));
            long? publicBalance = QueryInstitutionFunds(anchor);
            long unpaidWages = map.mapPawns.FreeColonistsSpawned.SelectMany(p => QueryPayables(p.GetUniqueLoadID()))
                .Sum(o => o.AmountMinor - o.PaidMinor);
            var rows = new List<EconomyRow>();
            // CHG-20261010-ECON-LABOR-UI2（A）：行动区右侧的只读参数说明（默认 null = 不显示）。
            string workspaceNote = null;
            // S2b：表格**区段**声明（劳务站 = 工作目录 + 工作合同两张表，各自列集/筛选面/搜索面；
            // 其余柜台保持行族 ⇒ 零回归）。区段按稳定 Key 寻址（呈现层会话态按 Key 存取，绝不用对象引用）。
            List<WorkspaceTableSection> tableSections = null;
            // CHG-20261012-ECON-CONTRACTIZE：**政策摘要行已移除**（用户裁决「一些文字删除」）——
            // 原先那行"营业 10:00–21:00 · 修订 64 · 计件工资 5.00"随政策概念一起消失
            //（工作时段已下沉到每份合同的弹窗字段；工资是合同自己的条款）。
            switch (kind)
            {
                case InstitutionKind.WorkshopOffice:
                    // B2：车间区数改数**本图岗位覆盖到的区**（去重）—— 不再是"政策绑定的那一个区"。
                    int zoneCount = mapPositions.Select(p => p.ZoneId).Where(id => id > 0).Distinct().Count();
                    kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.Zones"), zoneCount.ToString(CultureInfo.InvariantCulture),
                        zoneCount > 0 ? WorkspaceTone.Accent : WorkspaceTone.Dim));
                    kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.Positions"), mapPositions.Count.ToString(CultureInfo.InvariantCulture),
                        mapPositions.Count > 0 ? WorkspaceTone.Accent : WorkspaceTone.Dim));
                    kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.StaffingGap"), StaffingGap(mapPositions).ToString(CultureInfo.InvariantCulture),
                        StaffingGap(mapPositions) > 0 ? WorkspaceTone.Warn : WorkspaceTone.Dim));
                    kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.Authorisations"), ProductionAuthorisationCount.ToString(CultureInfo.InvariantCulture),
                        ProductionAuthorisationCount > 0 ? WorkspaceTone.Accent : WorkspaceTone.Dim));
                    AddPositionRows(rows, mapPositions, contractCount);
                    break;
                case InstitutionKind.LaborOffice:
                    {
                        // CHG-20261010-VANILLA-JOBS：劳务站改为承载**原版工作合同** —— 识别玩家下达的工作动作
                        //（砍伐 / 采矿），发布为可承接的合同，每完成一件从国库计件付酬。
                        // 车间岗位归工坊管理处；此处**不再读 legacy 岗位表**（旧模型已冻结、写通道恒关）。
                        int carriers = CountVanillaContracts(map, anchorKey);
                        // S1（CHG-20261011-WORKCAT）：KPI 由"两个工种各一枚"改为**注册表口径的两枚** ——
                        // 可计酬待办（已接线族之和）与未接线待办（识别到但尚不能付之和）。
                        // 理由：订单从 2 张扩到 8 张后，逐工种 KPI 会溢出 4 槽宽档（LAND-006 阈值 160）；
                        // 而"能付的活"与"看得见却付不了的活"这两数才是玩家真正需要的。
                        QueryCatalogueBacklogSums(map, VanillaWorkBacklogCap, out int payableBacklog, out int unwiredBacklog);
                        actions.Add(new WorkspaceAction(T("Workspace.WorkPrices"), WorkspaceVerb.WorkPrices));
                        kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.PayableBacklog"), payableBacklog.ToString(CultureInfo.InvariantCulture),
                            payableBacklog > 0 ? WorkspaceTone.Accent : WorkspaceTone.Dim));
                        kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.UnwiredBacklog"), unwiredBacklog.ToString(CultureInfo.InvariantCulture),
                            unwiredBacklog > 0 ? WorkspaceTone.Warn : WorkspaceTone.Dim));
                        kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.VanillaContracts"), carriers.ToString(CultureInfo.InvariantCulture),
                            carriers > 0 ? WorkspaceTone.Alive : WorkspaceTone.Dim));
                        // 链路审计（P2-①）：未付工资改用**本柜台的真实欠薪**（obligations 未付额），
                        // 不再读旧体系 receipts —— 否则"欠着钱却显示 0"（旧 receipts 与新应付账不同源）。
                        VanillaWorkStats(map, anchorKey, out int _, out long _, out long unpaidObligations);
                        long shownUnpaid = unpaidObligations > 0L ? unpaidObligations : 0L;
                        kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.UnpaidWages"), Money(shownUnpaid),
                            shownUnpaid > 0 ? WorkspaceTone.Warn : WorkspaceTone.Dim));
                        // CHG-20261012-ECON-CONTRACTIZE：「每份 ±」行动词与**长说明段**（VanillaHint）整体移除 ——
                        // 件数由「工作需求」表行点击 →「工作合同弹窗」逐份设定（用户裁决「一些文字删除」）；
                        // 说明文字不再上屏，字段语义由弹窗的标签行承载。
                        // S2b：两个表格**区段**（工作目录 / 工作合同）—— 目录行与合同行的列结构不同，
                        // 合并成一列集必然有一种"别人的列头、我的数据"（GATE-ARCH P2-①同族风险：跨分区填槽）。
                        // **工作合同移出本行集**：它有自己的区段，行族按当前区段渲染（不再同屏混排）。
                        AddWorkCatalogueRows(rows, map, anchorKey);
                        tableSections = new List<WorkspaceTableSection>
                        {
                            new WorkspaceTableSection("workcatalogue", T("Workspace.Table.Catalogue"),
                                BuildWorkCatalogueTable(), rows),
                            new WorkspaceTableSection("workcontracts", T("Workspace.Table.Contracts"),
                                BuildVanillaContractTable(), QueryVisualContractRows(map, anchorKey))
                        };
                        // CHG-20261012：件数由弹窗逐份给定 ⇒ 不再有"当前档位"可显示（备注留空）。
                        workspaceNote = null;
                        break;
                    }
                case InstitutionKind.SettlementOffice:
                    kpis.Add(new WorkspaceKpi(T("Public.InstitutionFunds"), publicBalance.HasValue ? Money(publicBalance.Value) : T("Treasury.Unknown"), WorkspaceTone.Accent));
                    kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.UnpaidWages"), Money(unpaidWages),
                        unpaidWages > 0 ? WorkspaceTone.Warn : WorkspaceTone.Dim));
                    kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.TreasuryAvailable"), TreasuryAvailableText(), WorkspaceTone.Accent));
                    kpis.Add(new WorkspaceKpi(T("Public.TotalLabel"), QueryPublicFundsTotal().HasValue ? Money(QueryPublicFundsTotal().Value) : T("Treasury.Unknown"), WorkspaceTone.Accent));
                    // CHG-20261010-ECON-AGG-MIGRATE 批次 B-2：账户页改为**就地承载**（见 SecondaryTabsFor：
                    // 账户 = 可写承载，操作人与国库戳由本柜台窗口提供）⇒ 原先的深链动词删除 ——
                    // 否则同一窗口里会有两个同名入口（行动区按钮 + 切换条页签），且深链把玩家送去聚合
                    // 窗口看同一页。缴存 / 提取的**参数语义**仍只在账户页（BuildTreasuryRows）构造，
                    // 本柜台只提供写上下文，不复制任何命令参数。
                    tableSections = BuildLaborWageSections(map);
                    workspaceNote = LaborPresentationRows.Text("MapScope");
                    break;
                case InstitutionKind.Shop:
                    kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.StockCount"), anchor == null ? "—"
                        : ShopInventory(anchor).Sum(item => (long)item.stackCount).ToString(CultureInfo.InvariantCulture),
                        WorkspaceTone.Accent));
                    // B2：商店的三枚报价 KPI（衣物价 / 库存目标数 / 服务价）全部是政策读数，随政策退役；
                    // 衣物价与食品价现在都是**按件市价**（无单一挂牌价可言），库存目标概念整体退役。
                    // 保留的只剩在售容量 —— 不补造任何替代读数（M5）。
                    AddStockRows(rows, map, anchor);
                    break;
                default:
                    long? clinicBalance = QueryInstitutionFunds(anchor);
                    int reportCount = State.All<EconomyReport>().Count();
                    int validReports = State.All<EconomyReport>().Count(r => Find.TickManager.TicksGame < r.ExpiresTick);
                    // B2（裁决⑥）：服务价改读**体检中心建筑字段**（单一确定性投影）；0/无 ⇒ 显示"—"（未挂牌，
                    // 不是 0 银 —— "未挂牌"与"定价为零"是两件事，M5）。
                    long? servicePrice = QueryServicePriceMinor(mapId);
                    kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.ServicePrice"),
                        servicePrice.HasValue ? Money(servicePrice.Value) : "—", WorkspaceTone.Dim));
                    kpis.Add(new WorkspaceKpi(T("Public.InstitutionFunds"), clinicBalance.HasValue ? Money(clinicBalance.Value) : T("Treasury.Unknown"), WorkspaceTone.Accent));
                    kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.Reports"), reportCount.ToString(CultureInfo.InvariantCulture), WorkspaceTone.Dim));
                    kpis.Add(new WorkspaceKpi(T("Workspace.Kpi.ReportsValid"), validReports.ToString(CultureInfo.InvariantCulture),
                        validReports > 0 ? WorkspaceTone.Alive : WorkspaceTone.Dim));
                    AddReportRows(rows);
                    break;
            }
            // UI 工程（2026-10-10）：原此处追加"旧动词（撤下/解约/…）写入路径已关闭"说明行 ——
            // 那是"解释为何没有按钮"的开发者信息，对玩家是噪音。工作台的诚实性由"行动区只列真正可用
            // 的动作"直接体现（F4），不需要再加一段解释文字，故整行移除（键一并删除，LOC-001 无孤儿）。
            var view = new EconomyView(State.Revision, State.Enabled, State.Funded, phaseText, anchor?.LabelCap.ToString() ?? anchorKey,
                rows, new List<EconomyRow>());
            var workspace = new WorkspaceView(State.Revision, anchor?.LabelCap.ToString() ?? anchorKey, EconomyLabels.KindLabel(kind),
                phaseText, phaseTone, kpis, actions, view);
            workspace.ActionNote = workspaceNote;
            // CHG-20261010-ECON-AGG-MIGRATE 批次 B-1：声明该柜台能就承载的次级页 —— 台账归工资结算处、
            // 报告归体检中心。索引直接用聚合窗口的页签常量（其自述"调用方不得硬编码数字"），
            // 标签用该窗口同一套键（两处同名，玩家不会看到两套命名）。
            workspace.SecondaryTabs = SecondaryTabsFor(kind);
            // S2b：劳务站明细区以**数据表格区段**呈现（列集 + 筛选面 + 搜索面由读模型声明 ⇒ 呈现层零列硬编码）。
            // ⚠ 只走 SetTables（null 归一为空集）—— 直接写字段曾让非劳务站柜台拿到 null（P0：窗口 NRE）。
            workspace.SetTables(tableSections);
            tableSections = null;
            return workspace;
        }

        /// <summary>该柜台能就承载的次级页（B-1）。**这是本地唯一的机构类型判断点** ——
        /// 呈现层读到的只是"有哪些页、叫什么"，因此窗口内保持零特判（LAND-010）。
        /// 未列入的柜台返回空集（不显示切换条，行为与之前完全一致）。</summary>
        private IReadOnlyList<WorkspaceSecondaryTab> SecondaryTabsFor(InstitutionKind kind)
        {
            // CHG-20261012-ECON-CONTRACTIZE：**政策页整体移除**（用户裁决「移除全部的机构政策」）——
            // 原先"所有柜台统一挂一个政策页签"的承载随之消失；派工与定价的唯一入口变为
            // 「工作需求」表的行点击 →「工作合同弹窗」。
            switch (kind)
            {
                // B-2：账户（**可写**：缴存 / 提取 / 承接 / 取消，需操作人与国库戳）+ 台账（只读）。
                case InstitutionKind.SettlementOffice:
                    return new[]
                    {
                        new WorkspaceSecondaryTab(EconomyHostedTabs.Accounts, T("Tab.Accounts"), true),
                        new WorkspaceSecondaryTab(EconomyHostedTabs.Ledger, T("Tab.Ledger"))
                    };
                // C-1/D-1：工坊管理处承载**机构列表**（导航动词 `Focus`）+ **岗位页**（只读：动词白名单剥离）。
                case InstitutionKind.WorkshopOffice:
                    return new[]
                    {
                        new WorkspaceSecondaryTab(EconomyHostedTabs.Institutions, T("Tab.Institutions"), false, true),
                        new WorkspaceSecondaryTab(EconomyHostedTabs.Jobs, T("Tab.Jobs"))
                    };
                case InstitutionKind.ExamCenter:
                    return new[] { new WorkspaceSecondaryTab(EconomyHostedTabs.Reports, T("Tab.Reports")) };
                // D-1：生活用品商店承载**库存页**（只读：动词被白名单剥离）。
                case InstitutionKind.Shop:
                    return new[] { new WorkspaceSecondaryTab(EconomyHostedTabs.Stock, T("Tab.Stock")) };
                default:
                    // 劳务站：业务面就是本柜台明细（工作需求表 + 工作合同表）—— 无承载页。
                    return System.Array.Empty<WorkspaceSecondaryTab>();
            }
        }

        private static WorkspaceTone ToneOf(InstitutionPhase phase)
        {
            switch (phase)
            {
                case InstitutionPhase.Active: return WorkspaceTone.Alive;
                case InstitutionPhase.Preparing: return WorkspaceTone.Warn;
                case InstitutionPhase.Faulted: return WorkspaceTone.Dead;
                case InstitutionPhase.Draining: return WorkspaceTone.Accent;
                default: return WorkspaceTone.Muted;
            }
        }
        // CHG-20261012-ECON-CONTRACTIZE · B2：`PolicySummary(InstitutionPolicy, InstitutionKind)` **已删除**
        // —— 它是政策摘要行的唯一生产者（"营业 10:00–21:00 · 修订 64 · 计件工资 5.00"），
        // 而该行在本批之前就已随政策概念移除（见 QueryWorkspace 内的说明）；函数本身自那时起即死代码。
        private int StaffingGap(List<EconomyPosition> positions)
        {
            int gap = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                int signed = State.All<EconomyContract>().Count(c => c.Active && c.PositionId == positions[i].Id);
                if (signed < positions[i].Capacity) gap += positions[i].Capacity - signed;
            }
            return gap;
        }
        private void AddPositionRows(List<EconomyRow> rows, List<EconomyPosition> positions, int contractCount)
        {
            if (positions.Count == 0) { rows.Add(Hint(T("Workspace.PositionsNone"))); return; }
            rows.Add(Hint(T("Workspace.Section.Positions")));
            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                int signed = State.All<EconomyContract>().Count(c => c.Active && c.PositionId == p.Id);
                rows.Add(new EconomyRow((Zone(p)?.label ?? T("MissingZone")) + " · " + T("Wage") + " " + Money(p.Wage)
                    + " · " + p.StartHour.ToString("00", CultureInfo.InvariantCulture) + "–" + p.EndHour.ToString("00", CultureInfo.InvariantCulture)
                    + " · " + signed.ToString(CultureInfo.InvariantCulture) + "/" + p.Capacity.ToString(CultureInfo.InvariantCulture)
                    + (p.RequiresExam ? " · " + T("RequireExam") : string.Empty) + (p.Open ? string.Empty : " · " + T("ClosedState"))));
            }
            rows.Add(Hint(T("Workspace.ContractsHint") + ": " + contractCount.ToString(CultureInfo.InvariantCulture)));
        }
        /// <summary>**工作目录**（CHG-20261011-WORKCAT · S1）：按**工作类型组**列出全部**已识别**订单 ——
        /// 含**未接线**族（识别到就必须可见，绝不静默漏付）。列：工作（含订单明细）· 待办 ·（已接线组才有）
        /// 已派发 / 可派发 / 每份 · 工资 · 计酬状态。
        /// <para>"已派发 / 可派发 / 每份"只在**已接线**的组显示：合同按工作类型派发，未接线族不可能有合同，
        /// 显示"—"比显示 0 更诚实（0 会被读成"没人接"）。</para></summary>
        private void AddWorkCatalogueRows(List<EconomyRow> rows, Map map, string anchorKey)
        {
            int cap = GameComponent_PawnEconomy.VanillaWorkBacklogCap;
            var groups = VanillaOrderCatalogue.Groups;
            string dash = "—";
            for (int g = 0; g < groups.Length; g++)
            for (int o = 0; o < groups[g].Orders.Length; o++)
            {
                var group = groups[g];
                var order = group.Orders[o];
                int backlog = QueryOrderBacklog(map, order, cap);
                string orderNames = T("Order." + order.Key);
                // 派发覆盖率（整行进度）：只在**已接线**组给（未接线族不可能有合同 ⇒ 不给假进度）。
                float coverage = -1f;
                int committedValue = 0;
                if (order.Wired)
                {
                    committedValue = VanillaJobsCommitted(map, group.WorkTypeDefName, anchorKey, order.Key);
                    coverage = backlog > 0 ? (committedValue >= backlog ? 1f : (float)committedValue / backlog) : -1f;
                }
                // 5 列（CHG-20261012）：工作类型 / 工作岗位 / 总需求 / 已派发 / 就业率。
                // 未接线族的"已派发/就业率"给 "—"（不是 0）：0 会被读成"没人接"，而事实是"该族还付不了"。
                string postedText = order.Wired ? committedValue.ToString(CultureInfo.InvariantCulture) : dash;
                string employmentText = order.Wired && backlog > 0
                    ? System.Math.Min(100, (int)(100f * (committedValue >= backlog ? 1f : (float)committedValue / backlog)))
                        .ToString(CultureInfo.InvariantCulture) + "%"
                    : dash;
                var text = new System.Text.StringBuilder();
                text.Append(WorkTypeLabel(group.WorkTypeDefName))
                    .Append(" · ").Append(orderNames.ToString())
                    .Append(" · ").Append(T("Workspace.Col.Demand")).Append(" ").Append(backlog.ToString(CultureInfo.InvariantCulture))
                    .Append(" · ").Append(T("Workspace.Col.Posted")).Append(" ").Append(postedText)
                    .Append(" · ").Append(T("Workspace.Col.Employment")).Append(" ").Append(employmentText);
                // 起草载荷：**已接线**的原版组可点行起草（未接线族付不了 ⇒ 不挂入口，F4）。
                string compose = order.Wired && WorkPricesSupported ? anchorKey + "|" + group.WorkTypeDefName + "|" + order.Key : null;
                if (!order.Wired) text.Append(" · ").Append(T("Workspace.Pay.NotWired"));
                if (!WorkPricesSupported) text.Append(" · ").Append(T("Dialog.WorkContract.Reason.PriceUnavailable"));
                rows.Add(new EconomyRow(text.ToString(), progress: coverage,
                    cells: new[]
                    {
                        WorkTypeLabel(group.WorkTypeDefName), orderNames.ToString(),
                        backlog.ToString(CultureInfo.InvariantCulture), postedText, employmentText
                    },
                    cellSorts: new[] { 0L, 0L, (long)backlog, (long)committedValue,
                        order.Wired && backlog > 0 ? (long)System.Math.Min(100, 100 * committedValue / backlog) : -1L },
                    tag: order.Wired ? "wired" : "unwired",
                    compose: compose));
            }
            // 本 Mod **岗位族**（统一进同一张表 —— 用户裁决「原版工作与 mod 工作统一数据表格」）。
            AddCataloguePositionRows(rows, map);
            // S2b：原版工作合同**不再是本行集的行**——它有自己的表格区段（"工作合同"，可按承接人/合同号搜索、
            // 可展开只读详情）；此处只保留**完成统计**与**链路阻断**（劳务站总览口径，属工作目录区段尾部）。
            // B（CHG-20261010-ECON-LABOR-UI2）：完成统计 —— 让玩家看到"已干多少、已付多少"。
            // 口径：已完成件数 = 本图该柜台活跃合同的 Σ CompletedJobs（与合同行同源）；
            // 已付工资 = 这些合同对应的应付账里已付部分（同源 obligations，不新造资金读数）。
            VanillaWorkStats(map, anchorKey, out int donePieces, out long paidMinor, out long _);
            rows.Add(new EconomyRow(T("Workspace.VanillaStatsDone") + " "
                + donePieces.ToString(CultureInfo.InvariantCulture)
                + " · " + T("Workspace.VanillaStatsPaid") + " " + Money(paidMinor)));
            // 链路审计（A 组）：**依序呈现阻断读数** —— 让玩家一眼看出链路卡在哪一环。
            // 全部为只读数据、零写路径；顺序与审计的闸门顺序一致（工资 → 待办 → 国库 → 动机）。
            QueryCatalogueBacklogSums(map, cap, out int payableBacklog, out int unwiredBacklog);
            rows.Add(Hint(VanillaChainBlockers(map, backlogTotal: payableBacklog + unwiredBacklog)));
        }
        /// <summary>本 Mod **岗位族**（地图口径，与工坊管理处同源）：车间区 · 工资 · 时段 · 在岗/容量 · 计酬。
        /// 与订单族的差别（见"工作目录"设计）：岗位绑定车间区、以"缺员"而非"待办件数"计量。</summary>
        /// <summary>**工作需求表格声明**（CHG-20261012-ECON-CONTRACTIZE）：5 列 ——
        /// 工作类型 / 工作岗位 / 总需求 / 已派发 / 就业率（用户裁决：「表头换为工作类型、工作岗位、总需求、
        /// 已派发、就业率」，且「原版工作与 mod 工作统一数据表格」⇒ mod 岗位族从行族段落升格为**表格行**）。
        /// 行点击 = 起草合同（<c>EconomyRow.Compose</c>）；筛选面保留 已接线/未接线（诚实性：识别到即可见）。
        /// 列宽在快照期按当前语言实测预量（绘制期零测量）。</summary>
        private EconomyTable BuildWorkCatalogueTable()
        {
            var columns = new List<EconomyTableColumn>
            {
                new EconomyTableColumn("workType", T("Workspace.Col.WorkType"), WorkColumnWidth("Workspace.Col.WorkType", 110f)),
                new EconomyTableColumn("position", T("Workspace.Col.Position"), WorkColumnWidth("Workspace.Col.Position", 150f)),
                new EconomyTableColumn("demand", T("Workspace.Col.Demand"), WorkColumnWidth("Workspace.Col.Demand", 80f), true),
                new EconomyTableColumn("posted", T("Workspace.Col.Posted"), WorkColumnWidth("Workspace.Col.Posted", 80f), true),
                new EconomyTableColumn("employment", T("Workspace.Col.Employment"), WorkColumnWidth("Workspace.Col.Employment", 80f), true)
            };
            var filterKeys = new List<string> { "wired", "unwired" };
            var filterLabels = new List<string> { T("Workspace.Pay.Wired"), T("Workspace.Pay.NotWired") };
            return new EconomyTable(columns, filterKeys, filterLabels);
        }
        /// <summary>**工作合同表格声明**（S2b）：劳务站第二张表 —— 列集与
        /// <c>QueryVisualContractRows</c> 的列化载荷同序（承接人 / 工种 / 已完成 / 总件数 / 已付 / 未付 /
        /// 每件 / 阶段 / 已交 / 待交 / 下一站 / **操作**）。基础八列载荷保留给旧调用者，工作台投影追加四列。
        /// **可自由搜索**（按行的 <c>Search</c> 键 = 承接人 · 合同号）；**无筛选面** ——
        /// 合同行只有"活跃"与"待承接"两种口径，给它加筛选是假选择。
        /// 列宽与工作目录同源 <c>WorkColumnWidth</c>（快照期按当前语言实测）。
        ///
        /// <para><b>B1（CHG-20261012 · P1-2）末列 = 控件格「操作」</b>：语义经
        /// <see cref="EconomyCellControl.Action"/> **声明** —— 呈现层不认列键、不认"末列"位置
        /// （LAND-010）；是否真画控件由行载荷 <c>EconomyRow.Revoke</c> 决定（只有 Posted 行有）。
        /// 非排序（这一列没有可排序的值，挂 ▲▼ 就是假按钮，F4）；最小宽保障按钮与格间距，
        /// 展开指示条由宿主另留独立槽，列不足宽时横向滚动（入口不消失）。</para></summary>
        private EconomyTable BuildVanillaContractTable()
        {
            var columns = new List<EconomyTableColumn>
            {
                ContractColumn("pawn", "Workspace.Contract.Col.Pawn", 120f),
                ContractColumn("work", "Workspace.Col.Work", 110f),
                ContractColumn("done", "Workspace.Contract.Col.Done", 80f, true),
                ContractColumn("total", "Workspace.Contract.Col.Total", 80f, true),
                ContractColumn("paid", "Workspace.Contract.Paid", 90f, true),
                ContractColumn("unpaid", "Workspace.Contract.Col.Unpaid", 90f, true),
                ContractColumn("wage", "Workspace.Col.Wage", 90f, true),
                ContractColumn("stage", "LaborVisual.UI.Stage", 125f),
                ContractColumn("delivered", "LaborVisual.UI.Delivered", 80f, true),
                ContractColumn("remaining", "LaborVisual.UI.Remaining", 80f, true),
                ContractColumn("station", "LaborVisual.UI.Station", 125f),
                ContractColumn("action", "Workspace.Col.Action", 110f, control: EconomyCellControl.Action)
            };
            // 契约自检：工作台行载荷的格数由 LaborContractColumnCount 给出 —— 列声明与它一旦
            // 失配就是"整列静默串列 / 控件凭空消失"（都不报错、只在屏幕上错位），这里**当场报错**（只报一次），
            // 绝不静默通过。改列集时两边必须同步（测试 Tests/VanillaContractRowTests 锁同一常量）。
            if (columns.Count != LaborContractColumnCount)
                Log.ErrorOnce("[PC][UI] work contract table declares " + columns.Count.ToString(CultureInfo.InvariantCulture)
                    + " columns but the row payload carries " + LaborContractColumnCount.ToString(CultureInfo.InvariantCulture)
                    + "; rows will be misaligned.", 0x1EC0E11);
            return new EconomyTable(columns, null, null, -1, searchable: true);
        }
        /// <summary>合同列独立声明可读下限；当前语言的表头、排序留白与操作按钮均在快照期预量。
        /// 偏好宽多留一档间隙，初始窗宽由表声明反推；其他表仍沿用既有压缩行为。</summary>
        private static EconomyTableColumn ContractColumn(string key, string label, float minimum,
            bool numeric = false, EconomyCellControl control = EconomyCellControl.None)
        {
            float min = WorkColumnWidth(label, minimum);
            if (control == EconomyCellControl.Action)
                min = UnityEngine.Mathf.Max(min, EconomyRowList.RowButtonWidth(T("Workspace.Contract.Revoke")) + RUIF.Tokens.SpaceXs);
            return new EconomyTableColumn(key, T(label), min + RUIF.Tokens.SpaceLg, numeric,
                control == EconomyCellControl.None, control, min);
        }
        /// <summary>列宽 = 表头实测宽 + 内边距（下界由 <paramref name="min"/> 兜住）；数字列另有等宽保证。</summary>
        private static float WorkColumnWidth(string label, float min)
        {
            float measured = RUIF.Measure.Width(EconomyLabels.TranslatedOrId("PC_PEA." + label, label), GameFont.Small)
                + RUIF.Tokens.SpaceMd * 2f;
            return measured > min ? measured : min;
        }
        private void AddCataloguePositionRows(List<EconomyRow> rows, Map map)
        {
            int mapId = map.uniqueID;
            var positions = State.All<EconomyPosition>().Where(p => p.MapId == mapId && p.Open).ToList();
            if (positions.Count == 0) { rows.Add(Hint(T("Workspace.NoModPosts"))); return; }
            string dash = "—";
            for (int i = 0; i < positions.Count; i++)
            {
                var p = positions[i];
                int signed = State.All<EconomyContract>().Count(c => c.Active && c.PositionId == p.Id);
                string zoneLabel = Zone(p)?.label ?? T("MissingZone");
                string capacityText = p.Capacity.ToString(CultureInfo.InvariantCulture);
                string signedText = signed.ToString(CultureInfo.InvariantCulture);
                // CHG-20261012：统一进 5 列表 —— 工作类型 / 工作岗位 / 总需求 / 已派发 / 就业率。
                // mod 岗位的"总需求"= 岗位容量、"已派发"= 已签约在岗数（与仓库岗位表同源）。
                string employmentText = p.Capacity > 0
                    ? System.Math.Min(100, (int)(100f * (signed >= p.Capacity ? 1f : (float)signed / p.Capacity)))
                        .ToString(CultureInfo.InvariantCulture) + "%"
                    : dash;
                // tag = wired：岗位可派发 ⇒ 归入「已接线」筛选组（与订单族同一语义；**冻结 ≠ 未接线** ——
                // 认不出来与"暂时不能派"是两件事，筛选口径必须能区分）。
                // 本段**不给 Compose**：车间岗位链冻结（用户裁决 ⑦）⇒ 无点击入口（F4），
                // 且首格显式标注「暂不可用」（`Workspace.Pay.Frozen`）—— 玩家必须看得见"为什么这行点不动"。
                // ⚠ 首格文本**必须并入 `text`**（GATE-UI 2026-10-11 P2）：表格列默认宽 110、
                // 英文"workshop post · Temporarily unavailable"必然截断，而单元格没有独立 tooltip
                // ⇒ 不并入就等于"截断后无处可查"（LAND-008：省略号与全文必须成对）。
                string firstCell = T("Workspace.ModPosition") + " · " + T("Workspace.Pay.Frozen");
                string text = firstCell
                    + " · " + zoneLabel + " · " + T("Wage") + " " + Money(p.Wage)
                    + " · " + p.StartHour.ToString("00", CultureInfo.InvariantCulture) + "–"
                    + p.EndHour.ToString("00", CultureInfo.InvariantCulture)
                    + " · " + T("Workspace.Col.Demand") + " " + capacityText
                    + " · " + T("Workspace.Col.Posted") + " " + signedText
                    + " · " + T("Workspace.Col.Employment") + " " + employmentText
                    + (p.RequiresExam ? " · " + T("RequireExam") : string.Empty);
                rows.Add(new EconomyRow(text,
                    cells: new[] { firstCell, zoneLabel, capacityText, signedText, employmentText },
                    cellSorts: new[] { 0L, 0L, (long)p.Capacity, (long)signed,
                        p.Capacity > 0 ? (long)System.Math.Min(100, 100 * signed / p.Capacity) : -1L },
                    tag: "wired"));
            }
        }
        /// <summary>链路阻断读数（CHG-20261010-ECON-LINKFIX-A，链路审计 A 席 P1-① 修复）：
        /// 按**闸门顺序**列出当前阻断项，全部来自既有只读查询。玩家此前只能看到"毫无动静"；
        /// 这一行把"卡在哪一环、下一步做什么"直接上屏（零写路径、零新命令）。
        ///
        /// <para><b>B2（政策净删）撤掉两个闸门</b>：① 「未发布工资」—— 工资改由**合同条款**承载
        ///（草拟弹窗逐份给），"某个机构有没有挂牌工资"不再影响链路；② 「无已定价消费」—— 食品与衣物
        /// 改按**原版市价按件**计价，价格恒可得 ⇒ 该闸门**恒真**，留着一个恒真的检查就是把装饰当验收。
        /// 保留的两条（待办 / 国库）仍然是真闸门。</para></summary>
        private string VanillaChainBlockers(Map map, int backlogTotal)
        {
            _ = map;   // 两个被撤掉的闸门是仅有的 map 使用者；保留参数以免调用点签名漂移（见上方说明）
            var blockers = new List<string>();
            if (backlogTotal <= 0) blockers.Add(T("Workspace.Chain.NoBacklog"));
            long? treasury = QueryTreasuryAvailableMinor();
            if (!treasury.HasValue) blockers.Add(T("Workspace.Chain.TreasuryUnknown"));
            else if (treasury.Value <= 0L) blockers.Add(T("Workspace.Chain.TreasuryEmpty"));
            return blockers.Count == 0
                ? T("Workspace.Chain.Ready")
                : T("Workspace.Chain.Blocked") + ": " + string.Join(" → ", blockers);
        }

        // S1（CHG-20261011-WORKCAT）：原 `AddVanillaWorkTypeRow`（逐工种硬编码两行）已由
        // `AddWorkCatalogueRows` 取代 —— 待办 / 派发 / 可派发 / 每份 / 工资 / 计酬全部由**工作识别注册表**
        // 驱动（新增订单只需在 `VanillaOrderCatalogue` 加一行，不再改本文件）；
        // 原有的"派发覆盖率"整行进度（`EconomyRow.Progress`）在新实现里**保留**（未接线组不给进度）。
        private void AddStockRows(List<EconomyRow> rows, Map map, Building_EconomyInstitution anchor)
        {
            if (anchor == null) { rows.Add(Hint(T("SelectShop"))); return; }
            _ = map;
            rows.Add(Hint(T("Workspace.Section.Stock")));
            if (!ShopInventory(anchor).Any()) rows.Add(Hint(T("Workspace.StockEmpty")));
            // B2：政策兜底价退役 ⇒ 与结算同款**只按该件市价**（显示价 == 结算价；不可得的件在结算侧被如实拒绝）。
            foreach (var item in ShopInventory(anchor))
                rows.Add(new EconomyRow(item.LabelCap + " · " + Money(ItemValuation.UnitPriceMinor(item.MarketValue))));
            rows.Add(Hint(T("Public.StockHint")));
            // 不沿用 StockHint：那段文案指引"选择搬运殖民者并点击入库"，而该按钮的写路径（Stock 动词）
            // 已由 Execute 的 LegacyFeesAllowed 门整体关闭 —— 指引一个不存在的按钮是本批明确要避免的（F4）。
        }
        private void AddReceiptRows(List<EconomyRow> rows)
        {
            AddCurrentSettlementRows(rows);
            if (!State.All<EconomyReceipt>().Any()) return;
            rows.Add(Hint(T("Mode.LegacyReadOnly")));
            rows.Add(Hint(T("Workspace.Section.Receipts") + " · " + T("Workspace.NoInstitutionField")));
            foreach (var r in State.All<EconomyReceipt>().Where(r => !r.Paid).OrderByDescending(r => r.Id).Take(20))
                rows.Add(new EconomyRow("#" + r.Id.ToString(CultureInfo.InvariantCulture) + " · " + T("Receipt." + r.Type)
                    + " · " + PawnName(r.SubjectKey) + " · " + Money(r.Amount) + " · " + T("Unpaid")));
            foreach (var r in State.All<EconomyReceipt>().Where(r => r.Paid).OrderByDescending(r => r.Id).Take(10))
                rows.Add(new EconomyRow("#" + r.Id.ToString(CultureInfo.InvariantCulture) + " · " + T("Receipt." + r.Type)
                    + " · " + PawnName(r.SubjectKey) + " · " + Money(r.Amount) + " · " + T("Paid")));
        }
        private void AddReportRows(List<EconomyRow> rows)
        {
            rows.Add(Hint(T("Workspace.Section.Reports") + " · " + T("Workspace.NoInstitutionField")));
            foreach (var r in State.All<EconomyReport>().OrderByDescending(r => r.Id).Take(20))
                rows.Add(new EconomyRow(PawnName(r.PawnKey) + " · " + T("Doctor") + " " + PawnName(r.DoctorKey)
                    + " · " + T("Capabilities") + $" {r.Moving:P0}/{r.Manipulation:P0}/{r.Consciousness:P0}"
                    + " · " + (Find.TickManager.TicksGame < r.ExpiresTick ? T("Valid") : T("Expired"))));
            rows.Add(Hint(T("ReportHint")));
        }
        /// <summary>体检中心机构账户键 —— **与 <c>EconomyInstitution.AccountKey</c> 同一公式**
        /// （<c>EconomyModel.cs</c>：体检中心专用账户，其余机构共用公共账）。新体系下不再产生旧
        /// <c>EconomyInstitution</c> 实例，因此没有可调用的域访问器，只能在此按下标锚键重建；
        /// 若将来域侧提供按锚键取账户键的访问器，本方法应改为委托它（唯一来源）。</summary>
        private static string ClinicAccountKey(string anchorKey) => "institution:" + anchorKey;
        private string TreasuryAvailableText()
        {
            long? available = QueryTreasuryAvailableMinor();
            return available.HasValue ? Money(available.Value) : T("Overview.Unknown");
        }
    }
}

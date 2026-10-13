using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy.Perception;
using PersonalChronicle.Domain.PawnEconomy.Care;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy : INeedDashboardSource
    {
        long INeedDashboardSource.Epoch => PerceptionSessionEpoch;
        PerceptionReadiness INeedDashboardSource.Availability => perceptionInitializationFailed ? PerceptionReadiness.Error
            : perception == null ? PerceptionReadiness.Loading : PerceptionReadiness.Ready;
        IReadOnlyList<PerceptionSubject> INeedDashboardSource.Subjects => ObservedSubjects;
        PerceptionDiagnostics INeedDashboardSource.Diagnostics => PerceptionDiagnostics;
        FactQueryResult INeedDashboardSource.Read(PerceptionReadRequest request, int now)
            => perception == null ? QueryPawnFacts(request) : perception.QueryFacts(request, now);
        internal bool IsPerceptionMapAvailable(Map map) => map != null && Find.Maps.Contains(map);
        internal NeedDashboardView QueryNeedDashboard(Map map, int page, string selectedId)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            var snapshot = IsPerceptionMapAvailable(map)
                ? NeedDashboardBuilder.Build(this, map.uniqueID, now, page, selectedId, targetProvider: id => QueryShadowTargets(id, map))
                : new NeedDashboardSnapshot(PerceptionSessionEpoch, map?.uniqueID ?? -1, now, 0, 0, 1,
                    PerceptionDiagnostics, null, selectedId, new ObservationQueryResult(
                        new FactQueryResult(PerceptionReadiness.OutOfScope, "MapUnavailable", false), null));
            var text = new VanillaNeedDashboardText();
            var view = new NeedDashboardPresenter(text).Build(snapshot);
            var details = new List<NeedTextRow>(new ShadowPlanPresenter(text).Build(QueryShadowPlan(snapshot.SelectedId, map), now));
            details.AddRange(view.Facts);
            return new NeedDashboardView(snapshot, view.Coverage, view.Selection, view.SelectionState, view.Roster, view.Needs, details);
        }
        /// <summary>
        /// 单人视图（Pawn 检视窗「个人需求」）：同一只读门面，不构建名册。
        /// 与名册路径口径对齐：同样接通影子规划目标（<c>QueryShadowTargets</c>，否则「目标/缺口」恒为未配置），
        /// 并把影子仲裁行放在独立 <c>Planning</c> 分区（名册路径沿用旧口径并入 Facts，不改其消费方）。
        /// </summary>
        internal NeedDashboardView QueryNeedDashboardSingle(Map map, string pawnId)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            var snapshot = IsPerceptionMapAvailable(map)
                ? NeedDashboardBuilder.BuildSingle(this, map.uniqueID, now, pawnId, targetProvider: id => QueryShadowTargets(id, map))
                : new NeedDashboardSnapshot(PerceptionSessionEpoch, map?.uniqueID ?? -1, now, 0, 0, 1,
                    PerceptionDiagnostics, null, pawnId, new ObservationQueryResult(
                        new FactQueryResult(PerceptionReadiness.OutOfScope, "MapUnavailable", false), null));
            var text = new VanillaNeedDashboardText();
            var planner = new ShadowPlanPresenter(text);
            var plan = QueryShadowPlan(pawnId, map);
            var goals = planner.BuildGoals(plan);
            // 需求行需要影子优先序与活跃性 ⇒ 先算目标态再装配读模型（按 NeedId 结构化对齐，非文本匹配）。
            var view = new NeedDashboardPresenter(text).Build(snapshot, goals);
            // 同一份影子计划拆成互补分区（零重复）：
            //   需求页签 = 长期生活政策；目标页签 = 目标态（双阈值/优先级/缺口三口径）；
            //   决策页签 = 仲裁与预算 + 四循环 + 候选岗位。
            var planning = planner.Build(plan, now);
            var policy = planner.BuildPolicy(plan);
            var budget = planner.BuildBudget(plan);
            var offers = planner.BuildOpportunities(plan);
            var cycles = planner.BuildCycles(plan, now);
            // 真实账本事实（只读，与影子计划互补）：规划层答"该做什么"，此处答"已经发生了什么"。
            // 全部为 O(账本条数) 的字典/列表读取，且本页有 60 tick 刷新门控（ITab_Pawn_PersonalNeeds），
            // 故不引入每帧开销。服务能力用**纯读**的 QueryServiceCapability，不用会推进 revision 的评估路径。
            var contract = QueryActiveContract(pawnId);
            bool located = TryGetPlanningPawn(pawnId, map?.uniqueID ?? -1, out var subject);
            var exit = located && contract != null ? QueryContractExit(subject, now) : ContractExitCode.None;
            var facts = new PawnEconomyFactsPresenter(text);
            var payables = QueryPayables(pawnId);
            var employment = facts.BuildEmployment(contract, exit, now, payables,
                QueryPurchaseBackoffFailures(pawnId),
                contract == null || string.IsNullOrEmpty(contract.InstitutionKey) ? null : QueryWageCredibility(contract.InstitutionKey));
            var care = facts.BuildCare(QueryCareAuthorizations(pawnId), QueryServiceCapability(ServiceKind.Treatment), now);
            // 个人账户摘要（CB-ECON-002）：账本可用性先判（按现有储备节点有界核验，不复制账户数组），**只有 Ready 才读账户** ——
            // 存读档中 / 原始态保留 / 恢复校验失败期间一律不把不可判定的账本当余额显示（既不显示 0.00 也不显示旧数）。
            // 账户归属键 = 当前选中 Pawn 的稳定身份（pawnId 即 GetUniqueLoadID 链路），不混入国库、机构账户或他人余额。
            bool inScope = IsPerceptionMapAvailable(map);
            var ledgerState = inScope ? settlement.ReadLedgerState() : SettlementLedgerState.Unavailable;
            var account = ledgerState == SettlementLedgerState.Ready ? settlement.ReadAccountForDisplay(pawnId) : null;
            var evidenceContract = contract ?? LatestDashboardContract(pawnId, now);
            // CHG-20261011-LABOR-VISUAL：劳动履约快照（只读；取不到就是没有在办流程 ⇒ 不加该两行）。
            var laborWorkflow = inScope ? QueryLaborFlowFor(QueryPawnByKey(pawnId)) : null;
            if (laborWorkflow == null && inScope && evidenceContract != null)
                laborWorkflow = QueryLaborWorkflow(evidenceContract.ContractId);
            var model = inScope ? planner.BuildModel(plan, now, account, evidenceContract, payables, exit, laborWorkflow,
                laborWorkflow == null ? null : LaborFlow.ReadBatchesFor(laborWorkflow.ContractId),
                laborWorkflow == null ? null : LaborFlow.ReadHistory(laborWorkflow.WorkflowId),
                laborWorkflow == null ? null : LaborStationLabel(laborWorkflow.CurrentStationKey)) : null;
            cycles = model?.Cycles ?? cycles;
            var accountView = facts.BuildAccount(account, ledgerState, budget, plan, now, view.Selection, inScope, model);
            // LR-01（经理复核 §10.2 存量 P2）：重打包**必须原样转传** mood / status / hiddenCount ——
            // 数据已在上游 presenter 取到，装配期丢字段会让单人页「心情」恒显示未知、状态条件区与截断提示整区消失，
            // 且该现象**不能解释为真实 Pawn 心情未知**（构造参数取默认值）。新增字段一律显式具名转传，防再次漏传。
            return new NeedDashboardView(snapshot, view.Coverage, view.Selection, view.SelectionState, view.Roster, view.Needs, view.Facts,
                planning, mood: view.Mood, status: view.Status, hiddenCount: view.HiddenCount,
                policy: policy, goals: goals, budget: budget, opportunities: offers, cycles: cycles,
                employment: employment, care: care, account: accountView);
        }
        // The existing holder index is bounded by the employment store capacity. No map or transaction-history scan.
        private WorkContract LatestDashboardContract(string pawnId, int now)
        {
            if (string.IsNullOrEmpty(pawnId)) return null;
            var contracts = employment.ReadContractsFor(pawnId);
            string latestId = null;
            int latestTick = -1;
            for (int i = 0; i < contracts.Count; i++)
            {
                var row = contracts[i];
                if (row == null || row.PawnKey != pawnId || row.SignedTick > now) continue;
                if (row.SignedTick > latestTick) { latestId = row.ContractId; latestTick = row.SignedTick; }
            }
            return latestId == null ? null : Employment.ReadContract(latestId);
        }
    }
    internal sealed class VanillaNeedDashboardText : INeedDashboardText
    {
        /// <summary>整键回落（P0-4 同一纪律）：缺键显示标识本身（<paramref name="suffix"/>），
        /// 绝不把 <c>PC_PEA.Needs.*</c> 裸键（更不会把它带进 string.Format）打上屏。</summary>
        public string Text(string suffix, params object[] arguments)
        {
            string text = EconomyLabels.TranslatedOrId("PC_PEA.Needs." + suffix, suffix);
            return arguments.Length == 0 ? text : string.Format(text, arguments);
        }
        public string NeedLabel(string id) => DefDatabase<NeedDef>.GetNamedSilentFail(id)?.LabelCap.ToString() ?? id;
        /// <summary>完整键名直译（复用 `PC_PEA.Care.*` 等既有命名空间的稳定 ID 标签）。
        /// P2-3：缺键回落到**键尾的稳定 ID**（`PC_PEA.Care.Kind.Shop` → `Shop`），
        /// 绝不把整把裸键打上屏（GOV-010；裸 <c>Translate</c> 是 P0-4 登记的根因形态）。</summary>
        public string IdLabel(string key)
            => string.IsNullOrEmpty(key) ? string.Empty : EconomyLabels.TranslatedOrId(key, StableIdOf(key));
        private static string StableIdOf(string key)
        {
            int dot = key.LastIndexOf('.');
            return dot >= 0 && dot + 1 < key.Length ? key.Substring(dot + 1) : key;
        }
    }
}

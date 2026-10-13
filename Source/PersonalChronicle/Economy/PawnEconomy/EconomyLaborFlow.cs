using System;
using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy.Employment;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**地图劳务链路**（意图 → 寻路到站 → 洽谈 → 原子签约 → 签领实体合同纸 →
    /// 原版工作 → 交验 → 付款 → 盖章归档）。
    ///
    /// <para><b>与既有链的分工</b>：本文件只新增"手续"与"资格门"，**不新建资金链** ——
    /// 签约仍走 <c>TrySignForOpportunity</c>（原子占用唯一实现），付款仍走
    /// <c>wage:&lt;obligationId&gt;</c> 与账本 Finalized/Replay。它把原先"远程直签 + 完成即付"改成
    /// "到站复验签约 + 交验后付款"，同时**不改变**任何金额、单价、件数或幂等身份。</para>
    ///
    /// <para><b>红线</b>：① 查询与绘制**不**签约、不印纸、不付款、不推进流程；
    /// ② 意图撤销只删"什么都没产生"的行；③ 纸不是债权（PayeeKey 由合同裁定）。</para>
    /// </summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        internal const string LaborVisitJobDefName = "PC_PEA_VisitLaborOffice";
        internal const string LaborSubmitJobDefName = "PC_PEA_SubmitWorkContract";
        /// <summary>收集真实产物的 Job（CA-07：此前缺失，导致"提交合同缺失寻路"）。</summary>
        internal const string LaborCollectJobDefName = "PC_PEA_CollectContractOutput";
        internal const string WorkContractDocumentDefName = "PC_PEA_WorkContractDocument";
        /// <summary>合同纸代次上限（有界：防补发无限打印）。到上限后进入对账，**不**继续印。</summary>
        internal const int MaximumDocumentGenerations = 32;
        /// <summary>补发冷却（tick）：防止"纸一被拿走就立刻补印"的抖动。</summary>
        internal const int ReissueCooldownTicks = 60;

        private LaborFlowSaveData laborFlow = new LaborFlowSaveData();
        private LaborFlowService laborFlowService;
        internal LaborFlowService LaborFlow => laborFlowService ?? (laborFlowService = new LaborFlowService(laborFlow));

        /// <summary>
        /// 存档挂点（由 <c>EconomyRuntime.ExposeData</c> 调用；旧档无该节点 ⇒ 空载体）。
        ///
        /// <para><b>必须是 <c>Scribe_Deep</c> 包装的独立节点</b>（DATA/SAVE 席 P1-2 修复）：直接调
        /// <c>laborFlow.ExposeData()</c> 会把四个**裸名**键（<c>schemaVersion/workflows/submissions/batches</c>）
        /// 平铺进组件节点 —— 与同族 <c>pawnEconomicEmployment</c> 的包装式不一致，且隔离仅靠"当前未撞名"，
        /// 未来同层写入有静默串读风险。包装后：命名空间独立、与测试（真档往返用同名节点）与注释一致。</para>
        /// </summary>
        internal void ExposeLaborFlow()
        {
            Scribe_Deep.Look(ref laborFlow, "pawnEconomicLaborFlow");
            if (laborFlow == null) laborFlow = new LaborFlowSaveData();
        }

        // ─────────────────────────── 只读查询（表现层与检查文本共用；一律零副作用） ───────────────────────────

        internal WorkContract QueryContractById(string contractId)
            => string.IsNullOrEmpty(contractId) ? null : Employment.ReadContract(contractId);
        internal LaborWorkflow QueryLaborWorkflow(string contractId)
            => string.IsNullOrEmpty(contractId) ? null : LaborFlow.ReadWorkflow(contractId);
        internal LaborWorkflow QueryLaborWorkflowById(string workflowId) => LaborFlow.ReadWorkflowById(workflowId);
        internal SubmissionRecord QueryLatestSubmission(string contractId)
            => string.IsNullOrEmpty(contractId) ? null : LaborFlow.ReadLatestSubmissionFor(contractId);
        internal Pawn QueryPawnByKey(string pawnKey) => PawnByKey(pawnKey);
        internal DeliveryCapability QueryCapability(string opportunityKey)
        {
            if (!WorkPriceRules.Parse(opportunityKey, out _, out _, out string order)) return DeliveryCapability.Unsupported;
            return LaborFlowRules.CapabilityFor(order);
        }
        /// <summary>该合同累计已付（minor）。只读。</summary>
        internal long QueryPaidMinorFor(string contractId)
        {
            long paid = 0L;
            var rows = Employment.ReadObligationsForContract(contractId);
            for (int i = 0; i < rows.Count; i++) paid += rows[i].PaidMinor;
            return paid;
        }
        /// <summary>该合同累计未付（minor）。只读。</summary>
        internal long QueryUnpaidMinorFor(string contractId)
        {
            long due = 0L;
            var rows = Employment.ReadObligationsForContract(contractId);
            for (int i = 0; i < rows.Count; i++) due += rows[i].AmountMinor - rows[i].PaidMinor;
            return due;
        }
        /// <summary>该 Pawn 的**在办流程**（每 Pawn 至多一个；已归档与已终结不算）。只读。</summary>
        internal LaborWorkflow QueryLaborFlowFor(Pawn pawn)
        {
            if (pawn == null) return null;
            // Unsettled rights remain actionable even after the work contract ended or another intent was created.
            if (TryPendingSubmission(pawn, out var payable))
            {
                var pending = LaborFlow.ReadWorkflow(payable.ContractId);
                if (pending != null && pending.Stage != LaborFlowStage.Archived) return pending;
            }
            var active = ActiveContractFor(pawn.GetUniqueLoadID());
            if (active != null)
            {
                var working = LaborFlow.ReadWorkflow(active.ContractId);
                if (working != null && working.Stage != LaborFlowStage.Archived) return working;
            }
            var flows = LaborFlow.ReadWorkflowsFor(pawn.GetUniqueLoadID());
            LaborWorkflow selected = null;
            for (int i = 0; i < flows.Count; i++)
            {
                var flow = flows[i];
                if (flow == null || flow.Stage == LaborFlowStage.Archived) continue;
                if (selected == null || flow.LastChangedTick > selected.LastChangedTick
                    || flow.LastChangedTick == selected.LastChangedTick
                        && string.CompareOrdinal(flow.WorkflowId, selected.WorkflowId) > 0) selected = flow;
            }
            return selected;
        }

        // ─────────────────────────── 意图（§4.2 IntentReady）：计划只形成办理意图，不签约 ───────────────────────────

        /// <summary>
        /// 把"计划选中了某个机会"登记为**办理意图**（可撤销、可恢复）。**不签约**、不占岗、不启用工种 ——
        /// 合同到站 SignCommit 才产生（REQ-LaborVisual-005 的"不存在远程先签后走"）。
        /// </summary>
        internal bool TryBeginLaborIntent(string pawnId, int mapId, string opportunityKey, int now, out string reason)
        {
            reason = null;
            if (!TryGetPlanningPawn(pawnId, mapId, out var pawn) || pawn?.Map == null) { reason = "PawnUnknown"; return false; }
            if (string.IsNullOrEmpty(opportunityKey)) { reason = "OpportunityMalformed"; return false; }
            if (!NewCommitmentsAllowed()) { reason = "InstitutionNotReady"; return false; }
            if (ActiveContractFor(pawnId) != null) { reason = "AlreadyContracted"; return false; }
            // 能力矩阵前置：**不允许**新签一份无法履行交付的实体模式合同（§6.1）。
            if (QueryCapability(opportunityKey) == DeliveryCapability.Unsupported)
            { reason = "DeliveryUnsupported"; return false; }
            if (!WorkPriceRules.Parse(opportunityKey, out _, out string anchorKey, out _) || string.IsNullOrEmpty(anchorKey))
            { reason = "OpportunityMalformed"; return false; }
            var station = FindLaborStation(pawn.Map, anchorKey);
            if (station == null) { reason = "ZoneUnavailable"; return false; }
            if (!pawn.CanReach(station, PathEndMode.Touch, Danger.None)) { reason = "CannotReach"; return false; }
            var code = LaborFlow.BeginVisit(pawnId, opportunityKey, anchorKey, SettlementDeliveryMode.PhysicalSubmission,
                now, out _);
            if (code == LaborFlowCode.Applied || code == LaborFlowCode.Replay) return true;
            reason = code.ToString();
            return false;
        }

        /// <summary>撤销**纯意图**（尚未签约）：进站前机会失效/玩家改主意时可撤（§4.4）。
        /// 已签约的流程一律不撤（"业务签约成功后不能因意图 TTL 撤销已赚权利"）。</summary>
        internal bool TryRevokeLaborIntent(string pawnId, string opportunityKey, out string reason)
        {
            reason = null;
            // 必须撤**当前轮**的行（CA-09 连带修正）：重新派生旧 ID 会撤到轮 1，或撤不掉 r2 的纯意图。
            var flow = LaborFlow.ReadWorkflowFor(pawnId, opportunityKey);
            if (flow == null) return true;
            if (!string.IsNullOrEmpty(flow.ContractId)) { reason = "AlreadySigned"; return false; }
            if (!LaborFlow.RemoveIntentWorkflow(flow.WorkflowId)) { reason = "RevokeBlocked"; return false; }
            return true;
        }

        /// <summary>周期维护：超期未进站的**纯意图**撤销（有界：单轮最多扫 128 行、撤 32 条）。
        /// TTL 只约束"尚未签约"的意图 —— 签约成功后不能因意图超时撤销已赚权利（§4.4）。</summary>
        internal void TrimLaborIntents(int now)
        {
            if (laborFlow.WorkflowCount == 0) return;
            var ids = LaborFlow.WorkflowIds();
            int scanned = 0, revoked = 0;
            var stale = new List<string>(4);
            for (int i = 0; i < ids.Count && scanned < 128 && revoked < 32; i++)
            {
                scanned++;
                var flow = LaborFlow.ReadWorkflowById(ids[i]);
                if (flow == null || !string.IsNullOrEmpty(flow.ContractId)) continue;
                if (flow.Stage != LaborFlowStage.IntentReady && flow.Stage != LaborFlowStage.GoingToLaborOffice) continue;
                if (now - flow.LastChangedTick < LaborFlowRules.ReservationTtlTicks) continue;
                stale.Add(ids[i]);
            }
            for (int i = 0; i < stale.Count && revoked < 32; i++)
                if (LaborFlow.RemoveIntentWorkflow(stale[i])) revoked++;
        }

        // ─────────────────────────── Job 规划（接入 Plan 的**新账本分支**） ───────────────────────────

        /// <summary>
        /// 办理 Job：把**已有意图**变成真实寻路（REQ-LaborVisual-002）。每 Pawn 至多一个办理 Job；
        /// 已有活跃合同 ⇒ 不再签第二份（与既有"一人一约"纪律一致）。
        /// </summary>
        internal Job PlanLaborVisit(Pawn pawn)
        {
            if (pawn?.Map == null || !SafeDecision(pawn)) return null;
            string pawnKey = pawn.GetUniqueLoadID();
            if (ActiveContractFor(pawnKey) != null) return null;
            var flow = QueryLaborFlowFor(pawn);
            if (flow == null || !string.IsNullOrEmpty(flow.ContractId)) return null;
            if (flow.Hold == FlowHold.ReconciliationRequired) return null;   // 对账中：不自动推进（等玩家/诊断处置）
            if (flow.Hold != FlowHold.None && flow.NextRetryTick > (Find.TickManager?.TicksGame ?? 0)) return null;
            if (flow.Stage != LaborFlowStage.IntentReady && flow.Stage != LaborFlowStage.GoingToLaborOffice
                && flow.Stage != LaborFlowStage.QueuedAtLaborOffice && flow.Stage != LaborFlowStage.Negotiating) return null;
            var station = FindLaborStation(pawn.Map, flow.InstitutionKey);
            if (station == null) { LaborPlanningBlocked(flow, "StationUnavailable", "Replan"); return null; }
            if (!LaborServiceCellUsable(pawn, station)) { LaborPlanningBlocked(flow, "CannotReach", "Replan"); return null; }
            var def = DefDatabase<JobDef>.GetNamedSilentFail(LaborVisitJobDefName);
            return def == null ? null : JobMaker.MakeJob(def, station);
        }

        /// <summary>
        /// 收集 Job：把"已完成劳动、真实产物还在地上/仓库里"的批次变成**本人搬运**（REQ-LaborVisual-007/010）。
        ///
        /// <para><b>这是此前缺失的一环</b>（管理器审计 CA-07）：没有它，Pawn 会在"实物没带够"处反复退避，
        /// 永远走不到工资结算处 —— 玩家看到的就是"提交合同缺失寻路"。多趟由**重复规划**承担
        /// （每趟一个 Job，中断/被打断后按批次剩余量幂等重来），玩家不需要手动搬货。</para>
        ///
        /// <para><b>目标定位</b>：只按批次记录的**真实实例 ID** 在 <c>ListerThings.ThingsOfDef</c>
        /// （按 Def 的有界列表）里找，不扫全图、也不认"同 Def 的第三人物品"（CA-01/CA-08）。</para>
        /// </summary>
        internal Job PlanContractCollectOutputs(Pawn pawn)
        {
            if (pawn?.Map == null || !SafeFulfillmentDecision(pawn)) return null;
            if (!TryPendingSubmission(pawn, out var obligation)) return null;
            var contract = Employment.ReadContract(obligation.ContractId);
            if (contract == null || contract.DeliveryMode != SettlementDeliveryMode.PhysicalSubmission) return null;
            if (!WorkPriceRules.Parse(contract.PositionKey, out _, out _, out string order)) return null;
            if (LaborFlowRules.CapabilityFor(order) != DeliveryCapability.CarryToCounter) return null;  // 只有实物型要搬
            var flow = LaborFlow.ReadWorkflow(obligation.ContractId);
            if (flow == null || flow.Hold == FlowHold.ReconciliationRequired) return null;   // 无可核对流程或对账中：不自动动货
            if (LaborFlow.IsDeliveryCovered(obligation.ContractId, obligation.EvidenceId)) return null;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (!MayAttempt(pawn.GetUniqueLoadID(), "LaborCollect", "BatchUnreachable", now)) return null;
            var batches = LaborFlow.ReadOpenBatchesFor(obligation.ContractId);
            bool hasEvidenceBatch = false;
            foreach (var batch in batches)
                if (batch.EvidenceId == obligation.EvidenceId && batch.RemainingQuantity > 0)
                { hasEvidenceBatch = true; break; }
            if (!hasEvidenceBatch)
            {
                LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "SubmissionUnknown", "Diagnose", flow.Stage, now, 0);
                RecordAttemptFailure(pawn.GetUniqueLoadID(), "LaborCollect", "NoRegisteredOutput", now, obligation.EvidenceId);
                return null; // Preserve earned wages; missing capture is not a successful empty delivery.
            }
            Thing target = null;
            ProductBatch picked = null;
            for (int i = 0; i < batches.Count && target == null; i++)
            {
                var batch = batches[i];
                if (batch == null || batch.RemainingQuantity <= 0) continue;
                if (!string.Equals(batch.EvidenceId, obligation.EvidenceId, StringComparison.Ordinal)) continue;
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(batch.ThingDefName);
                if (thingDef == null) continue;
                target = FindBatchThing(pawn, batch);
                if (target != null) picked = batch;
            }
            if (target == null || picked == null)
            {
                // A registered output that vanished outside our tracked transport path is an uncertain physical fact.
                if (CarriedForEvidence(pawn, obligation) <= 0)
                    foreach (var batch in batches)
                        if (batch.EvidenceId == obligation.EvidenceId && batch.RemainingQuantity > 0
                            && !InstanceOnMap(pawn.Map, batch.ThingDefName, batch.ThingIdNumber))
                        { LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "BatchUnknown", "Diagnose", flow.Stage, now, 0); break; }
                return null;
            }
            var def = DefDatabase<JobDef>.GetNamedSilentFail(LaborCollectJobDefName);
            if (def == null) return null;
            var job = JobMaker.MakeJob(def, target);
            // 本趟取量 = 该批次仍缺的量 ∩ 这一堆的实有量（多趟由重复规划承担）。**必须 > 0**：
            // 原版 Toils_Haul.TakeToInventory 在 count ≤ 0 时会空转（整趟白跑）。
            int take = Math.Min(target.stackCount, picked.RemainingQuantity);
            job.count = take > 0 ? take : 1;
            return job;
        }

        /// <summary>
        /// 交验 Job：把"已完成劳动但尚未交验"的应付变成真实到站（REQ-LaborVisual-010）。
        /// **不依赖下一次计划仍为 SeekWork** —— 它只看应付账，因此预算改变后工资不会被卡住（§4.3.2）。
        /// </summary>
        internal Job PlanContractSubmission(Pawn pawn)
        {
            if (pawn?.Map == null || !SafeFulfillmentDecision(pawn)) return null;
            if (!TryPendingSubmission(pawn, out var obligation)) return null;
            var flow = LaborFlow.ReadWorkflow(obligation.ContractId);
            // 对账中（物理事实不确定）⇒ **不自动重入**：由人工核验处置，否则会反复跑空趟（DATA 席 P1-5）。
            if (flow != null && flow.Hold == FlowHold.ReconciliationRequired) return null;
            var contract = Employment.ReadContract(obligation.ContractId);
            if (contract == null) return null;
            if (!WorkPriceRules.Parse(contract.PositionKey, out _, out _, out string order)) return null;
            var capability = LaborFlowRules.CapabilityFor(order);
            if (capability == DeliveryCapability.Unsupported) return null;   // 未声明能力：不给 Job（见意图阶段拒绝）
            if (capability == DeliveryCapability.CarryToCounter)
            {
                // 手上没有可交的真实产物 ⇒ 先去收集（PlanContractCollectOutputs），不要跑到柜台空手回来。
                // 已持久化接收、但资格写入中断：仍须到站以同一回执恢复资格，不再搬货。
                if (!LaborFlow.IsDeliveryCovered(obligation.ContractId, obligation.EvidenceId)
                    && CarriedForEvidence(pawn, obligation) <= 0) return null;
            }
            // "实物没带够"这类**可恢复失败**走既有退避账：宁可等一会儿再来，也不原地反复失败占 CPU（§4.3.3）。
            int now = Find.TickManager?.TicksGame ?? 0;
            if (!MayAttempt(pawn.GetUniqueLoadID(), "LaborSubmit", "ProductNotCarried", now)) return null;
            var station = FindSettlementForPawn(pawn, out string stationFailure);
            if (station == null) { LaborPlanningBlocked(flow, stationFailure, stationFailure == "CounterBusy" ? "Queue" : "Replan"); return null; }
            var def = DefDatabase<JobDef>.GetNamedSilentFail(LaborSubmitJobDefName);
            return def == null ? null : JobMaker.MakeJob(def, station);
        }

        /// <summary>
        /// 补领 Job：**履行中丢了合同纸**时去劳务站核对补发（REQ-LaborVisual-014）。
        /// 补发递增代次使旧纸失效（债权不随纸转手）；无纸能力（Def 缺失）或已达代次上限时不给 Job。
        /// </summary>
        internal Job PlanContractReissue(Pawn pawn)
        {
            if (pawn?.Map == null || !(HasPendingLaborFulfillment(pawn)
                ? SafeFulfillmentDecision(pawn) : SafeDecision(pawn))) return null;
            var contract = ContractNeedingDocument(pawn);
            if (contract == null || contract.DeliveryMode != SettlementDeliveryMode.PhysicalSubmission) return null;
            if (HoldsLiveDocument(pawn, contract.ContractId)) return null;              // 手上已有当前有效正本
            var flow = LaborFlow.ReadWorkflow(contract.ContractId);
            if (flow == null || flow.Hold == FlowHold.ReconciliationRequired) return null;
            if (DefDatabase<ThingDef>.GetNamedSilentFail(WorkContractDocumentDefName) == null)
            { LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "DocumentAbilityMissing", "Diagnose", flow.Stage, Find.TickManager.TicksGame, 0); return null; }
            var papers = pawn.Map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamedSilentFail(WorkContractDocumentDefName));
            for (int i = 0; i < papers.Count; i++)
            {
                if (!(papers[i] is Thing_WorkContractDocument paper) || !paper.IsValidFor(pawn.GetUniqueLoadID(), contract)
                    || paper.IsForbidden(pawn)
                    || !pawn.CanReserveAndReach(paper, PathEndMode.ClosestTouch, Danger.None)) continue;
                var pickup = DefDatabase<JobDef>.GetNamedSilentFail(LaborCollectJobDefName);
                if (pickup == null) return null;
                var job = JobMaker.MakeJob(pickup, paper); job.count = 1; return job;
            }
            if (contract.DocumentGeneration >= MaximumDocumentGenerations)
            { LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "DocumentReissueLimit", "Diagnose", flow.Stage, Find.TickManager.TicksGame, 0); return null; }
            int now = Find.TickManager?.TicksGame ?? 0;
            if (!MayAttempt(pawn.GetUniqueLoadID(), "LaborReissue", "DocumentMissing", now)) return null;
            var station = FindLaborStation(pawn.Map, contract.InstitutionKey);
            if (station == null || station.Destroyed || station.IsBurning()) { LaborPlanningBlocked(flow, "StationUnavailable", "Replan"); return null; }
            if (!LaborServiceCellUsable(pawn, station)) { LaborPlanningBlocked(flow, "CannotReach", "Replan"); return null; }
            var def = DefDatabase<JobDef>.GetNamedSilentFail(LaborVisitJobDefName);
            return def == null ? null : JobMaker.MakeJob(def, station);
        }

        /// <summary>取该 Pawn 第一条**待交验**应付（"已完成劳动但尚未交验"的唯一判据；只读）。</summary>
        private bool TryPendingSubmission(Pawn pawn, out WageObligation obligation)
        {
            obligation = null;
            if (pawn == null) return false;
            var obligations = Employment.ReadObligationsFor(pawn.GetUniqueLoadID());
            for (int i = 0; i < obligations.Count; i++)
            {
                var row = obligations[i];
                if (row == null || row.State == ObligationState.Paid) continue;
                if (row.Eligibility != WageEligibility.PendingSubmission) continue;
                obligation = row;
                return true;
            }
            return false;
        }

        internal bool HasPendingLaborFulfillment(Pawn pawn)
            => TryPendingSubmission(pawn, out _);

        /// <summary>
        /// CA-13：**真实 Job 事件推进阶段** —— 开始搬运本人产物 ⇒ CollectingOutputs（由收集 JobDriver 调用）。
        /// 规划/绘制不合成阶段；真实收集动作可重新进入同合同下一趟。
        /// </summary>
        internal void NoteLaborCollectingStarted(Pawn pawn)
        {
            if (pawn == null) return; // Actual Job callback; its driver owns ongoing safety checks.
            if (!TryPendingSubmission(pawn, out var obligation)) return;
            var flow = LaborFlow.ReadWorkflow(obligation.ContractId);
            if (flow == null || flow.Hold == FlowHold.ReconciliationRequired) return;
            if (flow.Hold != FlowHold.None) { LaborFlow.Resume(flow, Find.TickManager.TicksGame); flow = LaborFlow.ReadWorkflowById(flow.WorkflowId); }
            LaborFlow.SetStation(flow, string.Empty, Find.TickManager.TicksGame);
            MoveLaborStage(flow, LaborFlowStage.CollectingOutputs, "CollectOutputs");
            // CA-14/REQ-003：**收集产物**阶段的真实事件反馈（此前 12 阶段里这一类没有泡）。
            PostLaborSpeech(pawn, "PC_PEA.LaborVisual.Speech.CollectingOutputs", obligation.ObligationId + "|collect|" + (pawn.CurJob?.loadID ?? 0));
        }

        /// <summary>CA-13：带上真实产物走向结算处 ⇒ GoingToSettlement（由交验 JobDriver 调用）。</summary>
        internal void NoteLaborGoingToSettlement(Pawn pawn)
        {
            if (pawn == null) return;
            if (!TryPendingSubmission(pawn, out var obligation)) return;
            var flow = LaborFlow.ReadWorkflow(obligation.ContractId);
            if (flow == null || flow.Hold != FlowHold.None) return;
            MoveLaborStage(flow, LaborFlowStage.GoingToSettlement, "GoToSettlement");
        }

        /// <summary>真实动作阶段转换入口（签约逐阶、履约可循环；历史追加不倒写）。</summary>
        private void MoveLaborStage(LaborWorkflow flow, LaborFlowStage target, string nextAction)
        {
            if (flow == null || !LaborFlowRules.IsStageTransition(flow.Stage, target, !string.IsNullOrEmpty(flow.ContractId))) return;
            if (flow.Stage == target) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            LaborFlow.MoveTo(flow, target, nextAction, now);
        }

        /// <summary>
        /// REQ-021：**诊断快照**（只读、零副作用、**绝不触发 Scribe 写入**）。
        /// 给玩家/支持人员可复制的稳定事实：流程 ID、阶段、Hold + 原因、下一步、批次剩余与回执条数；
        /// 上限 8 条未归档流程（有界）。注意：<c>ExposeLaborFlow()</c> 只是**持久化暴露**，不是快照面 ——
        /// 本方法才是 REQ-021 要求的诊断出口（此前完全缺失）。
        /// </summary>
        internal string BuildLaborDiagnostics()
        {
            var ids = LaborFlow.WorkflowIds();
            var sb = new System.Text.StringBuilder(512);
            sb.Append("laborFlows=").Append(ids.Count);
            int shown = 0;
            for (int i = 0; i < ids.Count && shown < 8; i++)
            {
                var flow = LaborFlow.ReadWorkflowById(ids[i]);
                if (flow == null || flow.Stage == LaborFlowStage.Archived) continue;
                sb.Append('\n').Append(flow.WorkflowId)
                    .Append(" | ").Append(LaborVisualText.StageLabel(flow.Stage))
                    .Append(" | ").Append(LaborVisualText.HoldLabel(flow.Hold))
                    .Append(" | ").Append(LaborVisualText.ReasonLabel(flow.ReasonCode))
                    .Append(" | ").Append(LaborVisualText.NextActionLabel(flow.NextAction));
                if (!string.IsNullOrEmpty(flow.ContractId))
                {
                    int remaining = 0;
                    var batches = LaborFlow.ReadBatchesFor(flow.ContractId);
                    for (int b = 0; b < batches.Count; b++)
                        if (batches[b] != null && batches[b].RemainingQuantity > 0) remaining += batches[b].RemainingQuantity;
                    sb.Append(" | remaining=").Append(remaining)
                        .Append(" | receipts=").Append(LaborFlow.ReadSubmissionsFor(flow.ContractId).Count);
                }
                shown++;
            }
            AppendLaborPawnDiagnostics(sb);
            return sb.ToString();
        }

        private void AppendLaborPawnDiagnostics(System.Text.StringBuilder sb)
        {
            int shown = 0;
            foreach (var map in Find.Maps)
                foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (shown++ >= 8) return;
                    var active = ActiveContractFor(pawn.GetUniqueLoadID());
                    sb.Append('\n').Append("pawn=").Append(pawn.LabelShort).Append(" key=").Append(pawn.GetUniqueLoadID())
                        .Append(" job=").Append(pawn.CurJob?.def?.defName).Append(" target=").Append(pawn.CurJob?.targetA)
                        .Append(" assignment=").Append(pawn.timetable?.CurrentAssignment?.defName)
                        .Append(" food=").Append(pawn.needs?.food?.CurLevelPercentage)
                        .Append(" rest=").Append(pawn.needs?.rest?.CurLevelPercentage)
                        .Append(" safeRecruit=").Append(SafeDecision(pawn)).Append(" safeFulfill=").Append(SafeFulfillmentDecision(pawn))
                        .Append(" activeContract=").Append(active?.ContractId).Append(" mode=").Append(active?.DeliveryMode);
                    int count = 0;
                    foreach (var row in Employment.ReadObligationsFor(pawn.GetUniqueLoadID()))
                    {
                        if (row.State == ObligationState.Paid || count++ >= 4) continue;
                        var flow = LaborFlow.ReadWorkflow(row.ContractId);
                        sb.Append('\n').Append("  obligation=").Append(row.ObligationId).Append(" eligibility=").Append(row.Eligibility)
                            .Append(" flow=").Append(flow?.Stage).Append('/').Append(flow?.Hold).Append('/').Append(flow?.ReasonCode)
                            .Append(" carried=").Append(CarriedForEvidence(pawn, row))
                            .Append(" paper=").Append(HoldsLiveDocument(pawn, row.ContractId))
                            .Append(" covered=").Append(LaborFlow.IsDeliveryCovered(row.ContractId, row.EvidenceId));
                        int matched = 0;
                        foreach (var batch in LaborFlow.ReadBatchesFor(row.ContractId))
                        {
                            if (batch.EvidenceId != row.EvidenceId) continue;
                            matched++;
                            if (matched > 8) continue;
                            sb.Append('\n').Append("    batch=").Append(batch.BatchId).Append(" thingId=").Append(batch.ThingIdNumber)
                                .Append(" def=").Append(batch.ThingDefName).Append(" produced=").Append(batch.ProducedQuantity)
                                .Append(" accepted=").Append(batch.AcceptedQuantity).Append(" remaining=").Append(batch.RemainingQuantity)
                                .Append(" instanceOnMap=").Append(InstanceOnMap(pawn.Map, batch.ThingDefName, batch.ThingIdNumber));
                        }
                        sb.Append(" | evidenceBatches=").Append(matched);
                    }
                }
        }

        /// <summary>
        /// CA-13/REQ-017：某机构**当前在办**的流程（只读）。与账户页「劳动履约行」、合同纸检查文本
        /// **同一数据源**（`ReadWorkflowById`），因此三处永远显示同一真实阶段 —— 不另存一份呈现态。
        /// </summary>
        internal List<LaborWorkflow> ReadOpenFlowsAtStation(string stationKey)
        {
            var result = new List<LaborWorkflow>(4);
            if (string.IsNullOrEmpty(stationKey)) return result;
            var ids = LaborFlow.WorkflowIds();
            for (int i = 0; i < ids.Count; i++)
            {
                var flow = LaborFlow.ReadWorkflowById(ids[i]);
                if (flow == null || flow.Stage == LaborFlowStage.Archived) continue;
                if (!string.Equals(flow.CurrentStationKey, stationKey, StringComparison.Ordinal)) continue;
                if (flow.Stage != LaborFlowStage.GoingToLaborOffice && flow.Stage != LaborFlowStage.QueuedAtLaborOffice
                    && flow.Stage != LaborFlowStage.Negotiating && flow.Stage != LaborFlowStage.IssuingDocument
                    && flow.Stage != LaborFlowStage.GoingToSettlement && flow.Stage != LaborFlowStage.VerifyingSubmission) continue;
                result.Add(flow);
            }
            return result;
        }

        /// <summary>CA-10：当前**需要人工对账**的流程（只读；供机构按钮列出与处置）。</summary>
        internal List<LaborWorkflow> ReadReconciliationFlows()
        {
            var result = new List<LaborWorkflow>(4);
            var ids = LaborFlow.WorkflowIds();
            for (int i = 0; i < ids.Count; i++)
            {
                var flow = LaborFlow.ReadWorkflowById(ids[i]);
                if (flow != null && flow.Hold == FlowHold.ReconciliationRequired) result.Add(flow);
            }
            return result;
        }

        /// <summary>
        /// CA-10：人工对账处置的**真实入口**（机构按钮在确认框后调用）。逐条走服务层
        /// <c>ResolveReconciliation</c>；返回成功条数，失败条数交回调用方如实显示（绝不静默吞）。
        /// </summary>
        /// <summary>该证据的批次**还差多少件真实产物**（只读；按批次剩余量，不猜、不按 Def 泛匹配）。</summary>
        private int RemainingForEvidence(string contractId, string evidenceId)
        {
            int remaining = 0;
            var batches = LaborFlow.ReadOpenBatchesFor(contractId);
            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                if (batch == null || batch.RemainingQuantity <= 0) continue;
                if (!string.Equals(batch.EvidenceId, evidenceId, StringComparison.Ordinal)) continue;
                remaining += batch.RemainingQuantity;
            }
            return remaining;
        }

        /// <summary>本人容器中按真实实例与产出者核对可交量；同实例只计一次。</summary>
        private int CarriedForEvidence(Pawn pawn, WageObligation obligation)
        {
            if (pawn == null || obligation == null) return 0;
            var batches = LaborFlow.ReadBatchesFor(obligation.ContractId);
            int total = 0;
            var counted = new HashSet<int>();
            var held = new List<Thing>();
            if (pawn.carryTracker?.CarriedThing != null) held.Add(pawn.carryTracker.CarriedThing);
            if (pawn.inventory?.innerContainer != null) foreach (var item in pawn.inventory.innerContainer) held.Add(item);
            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                if (batch == null || batch.RemainingQuantity <= 0 || batch.EvidenceId != obligation.EvidenceId) continue;
                foreach (var item in held)
                    if (item != null && !item.Destroyed && item.thingIDNumber == batch.ThingIdNumber
                        && item.def?.defName == batch.ThingDefName && !counted.Contains(item.thingIDNumber)
                        && batch.ProducerPawnKey == pawn.GetUniqueLoadID())
                    { total += Math.Min(item.stackCount, batch.RemainingQuantity); counted.Add(item.thingIDNumber); }
            }
            return total;
        }

        /// <summary>
        /// 在**按 Def 的有界列表**里找该批次的真实实例（不扫全图、不认同 Def 的第三方物品）。
        /// 实例 ID 优先；旧档批次无实例 ID 时按"生成时登记的位置键"回退；两者都不符 ⇒ 找不到（待对账）。
        /// </summary>
        private Thing FindBatchThing(Pawn pawn, ProductBatch batch)
        {
            if (pawn?.Map == null || batch == null) return null;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(batch.ThingDefName);
            if (def == null) return null;
            var candidates = pawn.Map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < candidates.Count; i++)
            {
                var thing = candidates[i];
                if (thing == null || thing.Destroyed || !thing.Spawned) continue;
                if (batch.ThingIdNumber > 0)
                {
                    if (thing.thingIDNumber != batch.ThingIdNumber) continue;
                }
                else if (!string.Equals(HolderKeyOf(thing), batch.LocationHolderKey, StringComparison.Ordinal)) continue;
                if (thing.IsForbidden(Faction.OfPlayer)) continue;
                if (!pawn.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.None)) continue;
                return thing;
            }
            return null;
        }

        /// <summary>物件的生成时位置键（与 <c>EconomyLaborOutputs</c> 写入批次的口径一致）。</summary>
        private static string HolderKeyOf(Thing thing)
            => thing != null && thing.Spawned
                ? "cell:" + thing.Position.x + "," + thing.Position.z
                : string.Empty;

        // ─────────────────────────── 到站：洽谈 → 复验签约 → 签领实体纸 ───────────────────────────

        /// <summary>到站洽谈开始（真实阶段事件驱动；此处只推进阶段与意向，**不签约**）。</summary>
        internal void LaborArrivedAtOffice(Pawn pawn)
        {
            var flow = QueryLaborFlowFor(pawn);
            if (flow == null) return;
            if (string.IsNullOrEmpty(flow.ContractId)) MoveLaborStage(flow, LaborFlowStage.Negotiating, "Negotiate");
            PostLaborSpeech(pawn, string.IsNullOrEmpty(flow.ContractId) ? "PC_PEA.LaborVisual.Speech.Negotiating"
                : "PC_PEA.LaborVisual.Speech.ReissueChecking", flow.WorkflowId + "|negotiate|" + (pawn.CurJob?.loadID ?? 0));
        }

        /// <summary>
        /// **SignCommit**（§5.1）：到站复验并原子签约。复用唯一实现 <c>TrySignForOpportunity</c>
        /// （内含 Posted 承接的原子占用 / 件数与资金预留 / 工作授权）⇒ 同合同多人竞争只成功一次。
        /// 失败**不产生任何合同**，并把原因写进流程行（不静默）。
        /// </summary>
        internal bool LaborSignCommit(Pawn pawn, Thing station)
        {
            if (pawn?.Map == null || !(station is Building_EconomyInstitution building)) return false;
            if (building.Destroyed || building.IsBurning() || building.Map != pawn.Map) return false;
            if (building.def?.GetModExtension<EconomyInstitutionExtension>()?.kind != InstitutionKind.LaborOffice
                || pawn.Position != building.InteractionCell) return false;
            var flow = QueryLaborFlowFor(pawn);
            if (flow == null) return false;
            if (!string.IsNullOrEmpty(flow.ContractId))
            {
                // 已绑定合同 ⇒ 本 Job 是**补领合同纸**（履行中丢纸）而不是签约：直接放行到签发环节 ——
                // 不重签、不再抢岗位、不动条款（REQ-LaborVisual-014：途中丢失补救不转让债权）。
                return true;
            }
            string pawnKey = pawn.GetUniqueLoadID();
            int now = Find.TickManager?.TicksGame ?? 0;
            // CA-11：90 tick 洽谈期间可能发生制度关闭 / 站点被换 / 被征召 / 机会失效 —— **最终 commit 前整组重查**
            //（与 TryBeginLaborIntent 同源判据）。任一不成立即如实 Hold + 原因、**零写入**，绝不静默签约。
            if (!NewCommitmentsAllowed())
            {
                LaborFlow.HoldAt(flow, FlowHold.Blocked, "InstitutionNotReady", "RetryLater", flow.Stage, now,
                    now + LaborFlowRules.BackoffTicks(1));
                RecordAttemptFailure(pawnKey, "LaborSign", "InstitutionNotReady", now, flow.WorkflowId);
                return false;
            }
            if (building.def?.GetModExtension<EconomyInstitutionExtension>()?.kind != InstitutionKind.LaborOffice)
            {
                LaborFlow.HoldAt(flow, FlowHold.Blocked, "WrongStation", "Replan", flow.Stage, now,
                    now + LaborFlowRules.BackoffTicks(1));
                RecordAttemptFailure(pawnKey, "LaborSign", "WrongStation", now, flow.WorkflowId);
                return false;
            }
            if (pawn.Drafted || pawn.Downed || pawn.InMentalState
                || !pawn.CanReach(building, PathEndMode.Touch, Danger.None))
            {
                LaborFlow.HoldAt(flow, FlowHold.Paused, "Interrupted", "RetryLater", flow.Stage, now,
                    now + LaborFlowRules.BackoffTicks(1));
                return false;
            }
            if (!TrySignForOpportunity(pawnKey, pawn.Map.uniqueID, flow.OpportunityKey, now, out string reason))
            {
                // 到站复验失败（机会被抢 / 资金不足 / 工时或能力变化 / 承办能力不支持）：
                // 如实标 Blocked + 原因 + 下次重试时刻，**绝不静默成功**，也**不**产生半份承诺。
                LaborFlow.HoldAt(flow, FlowHold.Blocked, reason ?? "SignRejected", "RetryLater", flow.Stage, now,
                    now + LaborFlowRules.BackoffTicks(1 + ConsecutiveFailures(pawnKey, "LaborSign", reason ?? "SignRejected")));
                // CA-14：**缺款**与其它拒签原因必须能分辨 —— 缺款要玩家筹钱（不是等一等），
                // 其它原因（机会被抢 / 工时或能力变化）则照旧给通用反馈。
                if (string.Equals(reason, "InsufficientBacking", StringComparison.Ordinal))
                    PostLaborSpeech(pawn, "PC_PEA.LaborVisual.Speech.NoFunds", flow.WorkflowId + "|sign-funds|" + (pawn.CurJob?.loadID ?? 0));
                else
                    PostLaborSpeech(pawn, "PC_PEA.LaborVisual.Speech.SignRejected", flow.WorkflowId + "|rejected|" + (pawn.CurJob?.loadID ?? 0));
                RecordAttemptFailure(pawnKey, "LaborSign", reason ?? "SignRejected", now, flow.WorkflowId);
                return false;
            }
            var contract = ActiveContractFor(pawnKey);
            if (contract == null)
            {
                LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "SignedButContractMissing", "Diagnose",
                    flow.Stage, now, 0);
                return false;
            }
            // 合同身份与模式写入**同一行**（到站承接沿用原 ContractId；模式已由签约路径冻结）。
            LaborFlow.AttachContract(ref flow, contract.ContractId, contract.DeliveryMode, now);
            var live = LaborFlow.ReadWorkflowById(flow.WorkflowId) ?? flow;
            LaborFlow.MoveTo(live, LaborFlowStage.IssuingDocument, "IssueDocument", now);
            return true;
        }

        /// <summary>
        /// 签领：把合同纸**作为真实 Thing**交给劳动者（库存优先；满则落到柜台旁，仅授权者可合法领取）。
        /// 幂等：同一代次已签发 ⇒ Replay；纸丢失/被夺 ⇒ **代次递增补发**（旧纸失效，**不重印同代次**）。
        /// </summary>
        internal bool LaborIssueDocument(Pawn pawn)
        {
            var flow = QueryLaborFlowFor(pawn);
            if (flow == null || string.IsNullOrEmpty(flow.ContractId)) return false;
            int now = Find.TickManager?.TicksGame ?? 0;
            var contract = Employment.ReadContract(flow.ContractId);
            if (contract == null) return false;
            // 旧模式（含旧档承接前的合同）：不强制实体纸，直接进入履约（既有权利不受影响）。
            if (flow.Mode != SettlementDeliveryMode.PhysicalSubmission)
            {
                LaborFlow.MoveTo(flow, LaborFlowStage.Working, "Work", now);
                return true;
            }
            // CA-18：合同纸 **Def / 类能力缺失** 属于数据或能力故障，**不是**"外观降级" ——
            // 此时不得静默推进到"持约履约"、更不得播"合同领到了"。明确暂停 + 原因，既有债权分毫不受影响
            //（§7.4 只允许贴图/资产层面的合法回落；能力缺失必须停在可诊断状态）。
            if (DefDatabase<ThingDef>.GetNamedSilentFail(WorkContractDocumentDefName) == null)
            {
                LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "DocumentAbilityMissing", "Diagnose",
                    flow.Stage, now, 0);
                Log.WarningOnce("[PC][LABORFLOW] ThingDef '" + WorkContractDocumentDefName
                    + "' is missing; document issuing is paused instead of silently proceeding (no fake success).", 0x1EC0F07);
                return false;
            }
            bool holds = HoldsLiveDocument(pawn, flow.ContractId);
            bool reissue = !holds && (contract.DocumentGeneration > 0 || flow.DocumentGeneration > 0);
            if (reissue && contract.DocumentGeneration >= MaximumDocumentGenerations)
            {
                LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "DocumentReissueLimit", "Diagnose", flow.Stage, now, 0);
                return false;
            }
            var code = LaborFlow.EnsureDocument(ref flow, reissue, now, out int generation, out string instanceId);
            if (code == LaborFlowCode.Unknown || string.IsNullOrEmpty(instanceId))
            {
                LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "DocumentIdentityConflict", "Diagnose", flow.Stage, now, 0);
                return false;
            }
            // 合同行与流程行**同一批写入**（代次是两表共同的权威事实，分叉会让旧纸仍显示有效）。
            var applied = Employment.ApplyDocument(flow.ContractId, generation, instanceId);
            if (applied != EmploymentCode.Applied && applied != EmploymentCode.Replay)
            {
                LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, applied.ToString(), "Diagnose", flow.Stage, now, 0);
                return false;
            }
            if (!holds)
            {
                if (!SpawnContractDocument(pawn, flow, generation, instanceId))
                { LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "DocumentIssuanceFailed", "Diagnose", flow.Stage, now, 0); return false; }
                // CA-14：**补发**是真实事件（代次 > 1）；首次签领已有 SignedDocument 泡，不重复发。
            }
            if (!HoldsLiveDocument(pawn, flow.ContractId))
            {
                LaborFlow.HoldAt(flow, FlowHold.Blocked, "DocumentMissing", "CollectDocument", flow.Stage, now, 0);
                return true; // Real paper is at the counter; a separate pickup Job will confirm possession.
            }
            LaborFlow.MoveTo(flow, LaborFlowStage.Working, "Work", now);
            if (!holds && generation > 1) PostLaborSpeech(pawn, "PC_PEA.LaborVisual.Speech.Reissued", instanceId + "|reissued");
            // 成功反馈**只由已提交的签发事件触发**（一次），不在途中或计时结束时播（§7.2）。
            if (code == LaborFlowCode.Applied && generation == 1) PostLaborSpeech(pawn, "PC_PEA.LaborVisual.Speech.SignedDocument", instanceId + "|issued");
            return true;
        }

        /// <summary>
        /// 交验（SubmissionCommit，§5.1）：到工资结算处呈递合同与交付物，核验后**授予付款资格**。
        /// 交付物按能力矩阵分三支：实物（批次核销）/ 权威入库凭据 / 无产品证明。
        /// 付款**不在**本方法发生（资金由既有账本在资格成立后结算）。
        /// </summary>
        internal bool LaborSubmit(Pawn pawn, Thing station)
        {
            if (pawn?.Map == null || !(station is Building_EconomyInstitution building)) return false;
            if (building.Destroyed || building.IsBurning() || building.Map != pawn.Map) return false;
            if (building.def?.GetModExtension<EconomyInstitutionExtension>()?.kind != InstitutionKind.SettlementOffice
                || pawn.Position != building.InteractionCell) return false;
            string pawnKey = pawn.GetUniqueLoadID();
            string stationKey = building.GetUniqueLoadID();
            int now = Find.TickManager?.TicksGame ?? 0;
            var obligations = Employment.ReadObligationsFor(pawnKey);
            bool submitted = false;
            for (int i = 0; i < obligations.Count; i++)
            {
                var obligation = obligations[i];
                if (obligation == null || obligation.State == ObligationState.Paid) continue;
                if (obligation.Eligibility != WageEligibility.PendingSubmission) continue;
                submitted |= SubmitOne(pawn, building, stationKey, obligation, now);
            }
            return submitted;
        }

        /// <summary>从真实动作直接转换当前阶段；不合成途经阶段或虚构历史。</summary>
        private void AdvanceStage(LaborWorkflow workflow, LaborFlowStage target, string nextAction, int now)
        {
            var current = workflow == null ? null : LaborFlow.ReadWorkflowById(workflow.WorkflowId);
            if (current != null) LaborFlow.MoveTo(current, target, nextAction, now);
        }

        /// <summary>到账后的一次性流程补记：逐阶推进到"工资已到账"并清除叠加状态（表现层读同一行，无第二份状态）。</summary>
        private void NotePaymentConfirmed(string contractId, int now)
        {
            var flow = LaborFlow.ReadWorkflow(contractId);
            if (flow == null) return;
            // A prior payable can settle while this contract is doing a later piece. Keep that actual action visible.
            if (flow.Stage == LaborFlowStage.AwaitingPayment) AdvanceStage(flow, LaborFlowStage.PaymentConfirmed, "Paid", now);
            var live = LaborFlow.ReadWorkflow(contractId);
            if (live != null && live.Hold != FlowHold.None && live.ReasonCode == "PaymentUnavailable") LaborFlow.Resume(live, now);
        }

        /// <summary>单笔应付的交验：证件/来源/数量核验 → 接收回执 → 授予付款资格。</summary>
        private bool SubmitOne(Pawn pawn, Building_EconomyInstitution station, string stationKey,
            WageObligation obligation, int now)
        {
            var contract = Employment.ReadContract(obligation.ContractId);
            if (contract == null) return false;
            var flow = LaborFlow.ReadWorkflow(obligation.ContractId);
            if (flow == null || flow.Hold == FlowHold.ReconciliationRequired) return false;
            // ① 证件核验：持约人本人 + 有效代次的实体纸（纸不是债权，但**呈递需有效凭证**）。
            //    旧模式（Immediate）不走此门（其资格已是 Immediate，上面已过滤）。
            // 缺贴图可降级外观；缺 ThingDef 属于凭证能力缺失，进入对账且保留既有债权。
            bool paperRequired = DefDatabase<ThingDef>.GetNamedSilentFail(WorkContractDocumentDefName) != null;
            if (!paperRequired)
            { if (flow != null) LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "DocumentAbilityMissing", "Diagnose", flow.Stage, now, 0); return false; }
            bool paperOk = HoldsLiveDocument(pawn, obligation.ContractId);
            if (!paperOk)
            {
                // 有纸能力但本人手上没有**当前有效代次**的正本 ⇒ 不能呈递（旧纸/复制纸/丢纸都不通融）。
                if (flow != null)
                    LaborFlow.HoldAt(flow, FlowHold.Blocked, "DocumentMissing", "ReissueContract", flow.Stage, now, 0);
                // CA-14/REQ-003：**丢纸**的真实事件反馈（下一步就是去劳务站补领）。
                PostLaborSpeech(pawn, "PC_PEA.LaborVisual.Speech.DocumentMissing", obligation.ObligationId + "|missing|" + (pawn.CurJob?.loadID ?? 0));
                RecordAttemptFailure(pawn.GetUniqueLoadID(), "LaborSubmit", "DocumentMissing", now, obligation.EvidenceId);
                return false;
            }
            // ② 承担能力：该工作族必须有声明的交付能力（Unsupported ⇒ 拒绝并如实说明，绝不伪付）。
            if (!WorkPriceRules.Parse(contract.PositionKey, out _, out _, out string order)) return false;
            var capability = LaborFlowRules.CapabilityFor(order);
            if (capability == DeliveryCapability.CarryToCounter
                && LaborFlow.IsDeliveryCovered(obligation.ContractId, obligation.EvidenceId))
            {
                var receipt = LaborFlow.ReadCoveredSubmission(obligation.ContractId, obligation.EvidenceId, pawn.GetUniqueLoadID());
                if (receipt == null)
                { LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "ReceiptUnverifiable", "Diagnose", flow.Stage, now, 0); return false; }
                var recovered = Employment.GrantSubmission(obligation.ObligationId, receipt.SubmissionId, now);
                if (recovered != EmploymentCode.Applied && recovered != EmploymentCode.AlreadySubmitted) return false;
                LaborFlow.MoveTo(flow, LaborFlowStage.AwaitingPayment, "AwaitPayment", now);
                PostLaborSpeech(pawn, "PC_PEA.LaborVisual.Speech.AwaitingPayment", obligation.ObligationId + "|await-pay");
                return true;
            }
            if (capability == DeliveryCapability.Unsupported)
            {
                Employment.BlockCredential(obligation.ObligationId, "DeliveryUnsupported", now);
                if (flow != null) LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "DeliveryUnsupported", "Diagnose", flow.Stage, now, 0);
                return false;
            }
            // ③ 实物型（CA-02/CA-04/CA-07）：**先把真实产物搬进柜台的独立接收区，再按实测接收量写回执**。
            //    修前顺序相反（先核销批次、先授予资格，后 void 搬货）⇒ 柜台拒收或只收一部分时资格已下发
            //    （管理器探针复现：失败返回 Unknown 但 Accepted 已变 10、回执 0 = 孤立核销）。
            //    分趟交验（REQ-013）：本趟收多少记多少；未覆盖完成前**不放款**。
            var batchIds = new List<string>();
            int quantity = 0;
            if (capability == DeliveryCapability.CarryToCounter)
            {
                var batches = LaborFlow.ReadOpenBatchesFor(obligation.ContractId);
                int planned = 0;
                for (int b = 0; b < batches.Count; b++)
                {
                    var batch = batches[b];
                    if (batch == null) continue;
                    if (!string.Equals(batch.EvidenceId, obligation.EvidenceId, StringComparison.Ordinal)) continue;
                    batchIds.Add(batch.BatchId);
                    planned += batch.RemainingQuantity;
                }
                if (batchIds.Count == 0)
                {
                    // 该证据**没有任何已登记的真实产出**：不猜、不伪付、也不降级为无产品交验（那等于吞掉交货）。
                    if (flow != null)
                        LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "SubmissionUnknown", "Diagnose", flow.Stage, now, 0);
                    RecordAttemptFailure(pawn.GetUniqueLoadID(), "LaborSubmit", "NoRegisteredOutput", now, obligation.EvidenceId);
                    return false;
                }
                if (station.DeliveryRoom <= 0)
                {
                    // 接收区已满：**先预检、后取物** —— 不许"从 Pawn 手里拿走货才发现放不下"（§6.2）。
                    int roomFailures = ConsecutiveFailures(pawn.GetUniqueLoadID(), "LaborSubmit", "DeliveryRoomFull");
                    if (flow != null)
                        LaborFlow.HoldAt(flow, FlowHold.Blocked, "DeliveryRoomFull", "RetryLater", flow.Stage, now,
                            now + LaborFlowRules.BackoffTicks(roomFailures + 1));
                    RecordAttemptFailure(pawn.GetUniqueLoadID(), "LaborSubmit", "DeliveryRoomFull", now, obligation.EvidenceId);
                    return false;
                }
                // **物理接收**（先于任何核销/授予）：只认批次记录的真实实例，返回实测接收件数。
                quantity = TransferProductsToStation(pawn, station, obligation.ContractId, obligation.EvidenceId, planned, out batchIds);
                if (quantity <= 0)
                {
                    // 产物尚未（全部）带到：如实标"待交验"+下一步，按**既有退避账**排下次重试（不每 tick 重试）。
                    string reasonCode = "ProductNotCarried";
                    int failures = ConsecutiveFailures(pawn.GetUniqueLoadID(), "LaborSubmit", reasonCode);
                    if (flow != null)
                        LaborFlow.HoldAt(flow, FlowHold.Blocked, reasonCode, "BringProducts", flow.Stage, now,
                            now + LaborFlowRules.BackoffTicks(failures + 1));
                    RecordAttemptFailure(pawn.GetUniqueLoadID(), "LaborSubmit", reasonCode, now, obligation.EvidenceId);
                    return false;
                }
            }
            if (capability == DeliveryCapability.AlreadyAccepted)
            {
                // CA-03（管理器 P0）：**没有可核验的权威接收回执就不放行** —— 修前这里直接拼
                // `accepted:合同|证据` 当凭据（等于凭空造出"雇主已经收货"）。能力矩阵已把无
                // CaptureWired 证据的工种标为 Unsupported，本分支只应出现在旧档遗留行上：
                // 如实进对账并阻断付款，绝不伪造。
                Employment.BlockCredential(obligation.ObligationId, "ReceiptUnverifiable", now);
                if (flow != null)
                    LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired, "ReceiptUnverifiable", "Diagnose", flow.Stage, now, 0);
                return false;
            }
            string receiptId = capability == DeliveryCapability.CarryToCounter
                ? "receipt:" + obligation.ContractId + "|" + obligation.EvidenceId
                : "proof:" + obligation.ContractId + "|" + obligation.EvidenceId;
            var result = LaborFlow.AcceptSubmission(obligation.ContractId, pawn.GetUniqueLoadID(), stationKey,
                new[] { obligation.EvidenceId }, batchIds, quantity, receiptId, now);
            if (result.Code == LaborFlowCode.Unknown)
            {
                // 写回执失败（容量/表满/批次不守恒）：物资可能**已经进了接收区** ⇒ 进对账，
                // 记下本趟实收量，不猜成功也不重付（§5.1「无法证明物理移动是否发生时进入对账」）。
                Employment.BlockCredential(obligation.ObligationId, result.Reason ?? "SubmissionUnknown", now);
                if (flow != null)
                    LaborFlow.HoldAt(flow, FlowHold.ReconciliationRequired,
                        result.Reason ?? "SubmissionUnknown", "Diagnose", flow.Stage, now, 0);
                return false;
            }
            if (result.Code != LaborFlowCode.Applied && result.Code != LaborFlowCode.Replay) return false;
            if (flow != null && result.Code == LaborFlowCode.Applied)
                LaborFlow.RecordEvent(LaborFlow.ReadWorkflowById(flow.WorkflowId), result.SubmissionId + "|accepted",
                    "SubmissionAccepted", now, obligation.EvidenceId, result.SubmissionId, quantity, 0, result.Code.ToString(),
                    LaborSubmissionProductName(obligation.ContractId, obligation.EvidenceId));
            // ④ 只有**覆盖完成**才授予付款资格（部分趟只留回执：权利保留、不放款；REQ-013）。
            bool covered = capability != DeliveryCapability.CarryToCounter
                || LaborFlow.IsDeliveryCovered(obligation.ContractId, obligation.EvidenceId);
            if (!covered)
            {
                if (flow != null)
                {
                    var live = LaborFlow.ReadWorkflowById(flow.WorkflowId) ?? flow;
                    LaborFlow.HoldAt(live, FlowHold.Blocked, "PartialDelivery", "BringProducts", live.Stage, now,
                        now + LaborFlowRules.BackoffTicks(1));
                }
                RecordAttemptFailure(pawn.GetUniqueLoadID(), "LaborSubmit", "PartialDelivery", now, obligation.EvidenceId);
                SubmissionAcceptedFeedback(pawn, obligation, quantity, paperOk, result.SubmissionId);
                return true;   // 本趟真实接收已记账 —— 不是失败
            }
            var granted = Employment.GrantSubmission(obligation.ObligationId, result.SubmissionId, now);
            if (granted != EmploymentCode.Applied && granted != EmploymentCode.AlreadySubmitted) return false;
            if (flow != null)
            {
                var live = LaborFlow.ReadWorkflowById(flow.WorkflowId) ?? flow;
                LaborFlow.MoveTo(live, LaborFlowStage.AwaitingPayment, "AwaitPayment", now);
            }
            // CA-14/REQ-003：**待付款**（资格已授予、钱尚未到账）与「已到账」是两件事，必须能分辨。
            PostLaborSpeech(pawn, "PC_PEA.LaborVisual.Speech.AwaitingPayment", obligation.ObligationId + "|await-pay");
            SubmissionAcceptedFeedback(pawn, obligation, quantity, paperOk, result.SubmissionId);
            return true;
        }

        /// <summary>
        /// 把真实产物从呈递人转移到柜台的**交付接收 holder**（独立于衣物库存），返回**实测接收件数**。
        ///
        /// <para><b>为什么按"接收区前后实际增量"计数</b>：单个 API 返回值只是"宣称"；合堆会销毁传入实例、
        /// 部分转移会留余量。以容器实际增量为准才能同时满足"数量守恒"与"已收部分不重置"（§6.2）。</para>
        ///
        /// <para><b>来源核验</b>：只认批次记录的真实实例（见 <see cref="MatchesBatchInstance"/>）——
        /// 修前只比 Def，第三人的同 Def 物品可顶替交付（管理器 P0 探针 Thing 11 → Thing 999 仍 valid）。</para>
        /// </summary>
        private int TransferProductsToStation(Pawn pawn, Building_EconomyInstitution station,
            string contractId, string evidenceId, int planned, out List<string> receivedBatchIds)
        {
            receivedBatchIds = new List<string>();
            if (pawn == null || station == null || planned <= 0) return 0;
            var held = new Dictionary<int, Thing>();
            if (pawn.carryTracker?.CarriedThing != null) held[pawn.carryTracker.CarriedThing.thingIDNumber] = pawn.carryTracker.CarriedThing;
            if (pawn.inventory?.innerContainer != null)
                foreach (var item in pawn.inventory.innerContainer) if (item != null) held[item.thingIDNumber] = item;
            int moved = 0;
            foreach (var batch in LaborFlow.ReadOpenBatchesFor(contractId))
            {
                if (moved >= planned || station.DeliveryRoom <= 0) break;
                if (batch.EvidenceId != evidenceId || batch.ProducerPawnKey != pawn.GetUniqueLoadID()
                    || batch.ThingIdNumber <= 0 || !held.TryGetValue(batch.ThingIdNumber, out var item)
                    || item.Destroyed || item.def?.defName != batch.ThingDefName) continue;
                int take = Math.Min(Math.Min(item.stackCount, batch.RemainingQuantity), planned - moved);
                int before = CountInContainer(station.DeliveryContainer, batch.ThingDefName);
                station.AcceptLaborDelivery(item, take);
                int delta = Math.Max(0, CountInContainer(station.DeliveryContainer, batch.ThingDefName) - before);
                if (delta <= 0) continue;
                receivedBatchIds.Add(batch.BatchId); // Only physically received batches may be consumed by the receipt.
                moved += delta;
            }
            return moved;
        }

        /// <summary>容器内指定 Def 的**实际件数**（实测增量用；只读）。</summary>
        private static int CountInContainer(ThingOwner container, string thingDefName)
        {
            if (container == null || string.IsNullOrEmpty(thingDefName)) return 0;
            int total = 0;
            for (int i = 0; i < container.Count; i++)
            {
                var thing = container[i];
                if (thing?.def != null && string.Equals(thing.def.defName, thingDefName, StringComparison.Ordinal))
                    total += thing.stackCount;
            }
            return total;
        }

        /// <summary>只按合同/证据/产出者/已追踪真实实例核验。普通同类物品不能替代。
        /// 收集中的 SplitOff 由批次身份迁移追踪；外部不可证明拆合进入对账。</summary>
        private bool MatchesBatchInstance(Pawn pawn, Thing thing, string contractId, string evidenceId)
        {
            if (thing?.def == null || pawn == null) return false;
            var batches = LaborFlow.ReadBatchesFor(contractId);
            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                if (batch == null || batch.RemainingQuantity <= 0) continue;
                if (!string.Equals(batch.EvidenceId, evidenceId, StringComparison.Ordinal)) continue;
                if (!string.Equals(batch.ThingDefName, thing.def.defName, StringComparison.Ordinal)) continue;
                // CA-01：**合法持有人一致** —— 批次记录的产出者必须是本次呈递人本人（旧档缺该键时按
                // Def+证据+实例的既有判据，不因此凭空放宽：实例 ID 仍必须吻合）。第三人的同 Def 物品在这里就被挡掉。
                if (batch.ProducerPawnKey != pawn.GetUniqueLoadID()) continue;
                if (batch.ThingIdNumber > 0 && thing.thingIDNumber == batch.ThingIdNumber) return true;
                // Unknown external split/merge never authorizes a replacement of the same Def.

            }
            return false;
        }

        /// <summary>该实例是否仍在图上（**按 Def 的有界列表**查，不扫 AllThings）。</summary>
        private static bool InstanceOnMap(Map map, string thingDefName, int thingIdNumber)
        {
            if (map == null || string.IsNullOrEmpty(thingDefName) || thingIdNumber <= 0) return false;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            if (def == null) return false;
            var candidates = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < candidates.Count; i++)
            {
                var thing = candidates[i];
                if (thing != null && !thing.Destroyed && thing.thingIDNumber == thingIdNumber) return true;
            }
            return false;
        }

        // ─────────────────────────── 付款门（W4：与既有唯一付款链同源） ───────────────────────────

        /// <summary>
        /// 该应付当前是否允许发起付款（**唯一判据**，被 TrySettlePayable 与补付链共用）。
        /// 旧模式（含旧档缺键）⇒ 允许（既有行为逐字不变）；新模式 ⇒ 必须已交验核验。
        /// </summary>
        internal bool MayPayObligation(WageObligation obligation)
            => obligation != null && LaborFlowRules.PaymentAllowed(obligation.Eligibility);

        // ─────────────────────────── 内部工具 ───────────────────────────

        /// <summary>本图**劳务站**锚点（按稳定 ID 查找；不存在/燃烧/离图 ⇒ null）。</summary>
        private Building_EconomyInstitution FindLaborStation(Map map, string anchorKey)
        {
            if (map == null || string.IsNullOrEmpty(anchorKey)) return null;
            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == null || !anchor.Spawned || anchor.Destroyed || anchor.IsBurning() || anchor.Map != map) continue;
                if (anchor.GetUniqueLoadID() != anchorKey) continue;
                if (anchor.def?.GetModExtension<EconomyInstitutionExtension>()?.kind != InstitutionKind.LaborOffice) continue;
                return anchor;
            }
            return null;
        }

        /// <summary>本图指定类型的机构锚点（有界；同图多台时取第一个可用者）。</summary>
        private Building_EconomyInstitution FindSettlementForPawn(Pawn pawn, out string failure)
        {
            failure = "StationUnavailable";
            if (pawn?.Map == null) return null;
            bool reachable = false;
            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == null || !anchor.Spawned || anchor.Destroyed || anchor.IsBurning() || anchor.Map != pawn.Map
                    || anchor.def?.GetModExtension<EconomyInstitutionExtension>()?.kind != InstitutionKind.SettlementOffice) continue;
                if (failure == "StationUnavailable") failure = "CannotReach";
                if (anchor.IsForbidden(pawn) || !LaborServiceCellUsable(pawn, anchor)) continue;
                reachable = true;
                if (!pawn.CanReserve(anchor, LaborQueueCapacity)) continue;
                failure = null; return anchor;
            }
            if (reachable) failure = "CounterBusy";
            return null;
        }

        private string LaborSubmissionProductName(string contractId, string evidenceId)
        {
            string name = null;
            foreach (var batch in LaborFlow.ReadBatchesFor(contractId))
            {
                if (batch.EvidenceId != evidenceId) continue;
                if (name != null && name != batch.ThingDefName) return string.Empty;
                name = batch.ThingDefName;
            }
            return name ?? string.Empty;
        }

        /// <summary>
        /// 该 Pawn 是否持有**当前有效**的合同正本（只查本人容器，零全图扫描）。
        ///
        /// <para><b>CA-06 修复</b>：修前只比"合同 ID + 代次"，没有核**实例 ID / 授权持有人 / 结构完整性** ——
        /// 于是"同代次的伪造纸"能被呈递（而检查文本却按实例 ID 判它无效，两处口径相反）；
        /// 且只遍历一层容器，纸放在本人背包/容器里时定位失效。现在统一走
        /// <see cref="Thing_WorkContractDocument.IsValidFor"/>（与检查文本同一判据）并**沿持有器链**查找。</para>
        /// </summary>
        private bool HoldsLiveDocument(Pawn pawn, string contractId)
        {
            if (pawn == null || string.IsNullOrEmpty(contractId)) return false;
            var contract = Employment.ReadContract(contractId);
            if (contract == null || contract.DocumentGeneration <= 0) return false;
            string pawnKey = pawn.GetUniqueLoadID();
            if (HoldsDocument(pawn.carryTracker, pawnKey, contract)) return true;
            return HoldsDocument(pawn.inventory, pawnKey, contract);
        }

        /// <summary>在本人持有器链（携带 → 库存 → 嵌套容器，深度 ≤ 3）里找**当前有效**的合同正本。</summary>
        private static bool HoldsDocument(IThingHolder holder, string pawnKey, WorkContract contract, int depth = 0)
        {
            if (holder == null || depth > 3) return false;
            var things = holder.GetDirectlyHeldThings();
            if (things == null) return false;
            for (int i = 0; i < things.Count; i++)
            {
                var thing = things[i];
                if (thing is Thing_WorkContractDocument document && document.IsValidFor(pawnKey, contract)) return true;
                if (thing is IThingHolder nested && HoldsDocument(nested, pawnKey, contract, depth + 1)) return true;
            }
            return false;
        }

        /// <summary>创建/放置合同纸（库存优先；满或不可入 ⇒ 落到柜台旁的真实 Thing，仅授权者可合法领取）。</summary>
        private bool SpawnContractDocument(Pawn pawn, LaborWorkflow flow, int generation, string instanceId)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(WorkContractDocumentDefName);
            if (def == null)
            {
                // Def 缺失 = 数据错误：**不阻断业务**（签约与债权已成立），但必须可观测（绝不静默）。
                Log.WarningOnce("[PC][LABORFLOW] ThingDef '" + WorkContractDocumentDefName
                    + "' is missing; the contract document cannot be issued (business state is unaffected).", 0x1EC0F06);
                return false;
            }
            var document = ThingMaker.MakeThing(def) as Thing_WorkContractDocument;
            if (document == null) return false;
            document.ContractId = flow.ContractId;
            document.DocumentInstanceId = instanceId;
            document.Generation = generation;
            document.AuthorizedHolderKey = flow.PawnKey;
            document.IssuerInstitutionKey = flow.InstitutionKey;
            if (pawn.inventory?.innerContainer != null
                && pawn.inventory.innerContainer.TryAddOrTransfer(document, 1, false) == 1) return true;
            var station = FindLaborStation(pawn.Map, flow.InstitutionKey);
            IntVec3 cell = station != null ? station.Position : pawn.Position;
            if (!GenPlace.TryPlaceThing(document, cell, pawn.Map, ThingPlaceMode.Near)) return false;
            Messages.Message(EconomyLabels.Formatted("PC_PEA.LaborVisual.Doc.DroppedAtCounter", "DocDroppedAtCounter",
                pawn.LabelShortCap), MessageTypeDefOf.CautionInput, false);
            return true;
        }

        /// <summary>到账/交验的一次性反馈（**只由已提交的真实事件触发**；表现失败不影响业务）。</summary>
        private void SubmissionAcceptedFeedback(Pawn pawn, WageObligation obligation, int quantity, bool paperOk, string submissionId)
        {
            // 无产品劳务用"劳务完成证明已核验"，**不**沿用"货物交上了"（§7.2：不营造系统未确认的事实）。
            string key = quantity > 0 ? "PC_PEA.LaborVisual.Speech.SubmittedProducts"
                : "PC_PEA.LaborVisual.Speech.SubmittedProof";
            string eventId = submissionId + "|accepted";
            if (quantity > 0) PostLaborSpeech(pawn, key, eventId, quantity);
            else PostLaborSpeech(pawn, key, eventId);
            _ = paperOk;
        }

        /// <summary>
        /// 周期维护：把**权利已结清**的流程归档（§4.2 的 <see cref="LaborFlowStage.Archived"/>）。
        ///
        /// <para>门是权利而非合同状态：**合同终止/做满 ≠ 可归档** —— 只要有未付应付或未核销批次，
        /// 流程一律保留在 <see cref="LaborFlowStage.AwaitingPayment"/>/<c>Blocked</c>（§4.2
        /// 「同一份已结束合同仍可存在未交付或未付权利，不能据合同结束归档删除流程」）。</para>
        /// 有界：单轮最多扫 64 行、归档 16 条。
        /// </summary>
        internal void ArchiveSettledFlows(int now)
        {
            if (laborFlow.WorkflowCount == 0) return;
            var ids = LaborFlow.WorkflowIds();
            int scanned = 0, archived = 0;
            for (int i = 0; i < ids.Count && scanned < 64 && archived < 16; i++)
            {
                scanned++;
                var flow = LaborFlow.ReadWorkflowById(ids[i]);
                if (flow == null || flow.Stage == LaborFlowStage.Archived) continue;
                if (string.IsNullOrEmpty(flow.ContractId)) continue;
                if (flow.Stage == LaborFlowStage.AwaitingPayment) continue;   // 待发薪：先由付款链推进到"已到账"
                var contract = Employment.ReadContract(flow.ContractId);
                if (contract == null) continue;
                if (contract.State == ContractState.Active || contract.State == ContractState.Posted) continue;
                if (Employment.UnpaidEarnedFor(flow.ContractId) > 0L) continue;   // 未付欠薪：绝不归档
                if (LaborFlow.HasOpenBatches(flow.ContractId)) continue;          // 未核销批次：绝不归档
                LaborFlow.MoveTo(flow, LaborFlowStage.Archived, "Archived", now);
                archived++;
            }
        }

        /// <summary>该 Pawn 手上（携带或库存）是否有指定 Def 的真实物品 —— **只查本人容器**，零全图扫描。</summary>
        private static bool CarriesDef(Pawn pawn, string thingDefName)
        {
            if (pawn == null || string.IsNullOrEmpty(thingDefName)) return false;
            var carried = pawn.carryTracker?.CarriedThing;
            if (carried?.def != null && string.Equals(carried.def.defName, thingDefName, StringComparison.Ordinal)) return true;
            if (pawn.inventory?.innerContainer == null) return false;
            foreach (var item in pawn.inventory.innerContainer)
                if (item?.def != null && string.Equals(item.def.defName, thingDefName, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}

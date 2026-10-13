using System;
using System.Collections.Generic;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;

namespace PersonalChronicle.Application.PawnEconomy.Employment
{
    /// <summary>命令/结果分类（§8.2）：**未知绝不降为成功或已知零**。</summary>
    internal enum LaborFlowCode
    {
        /// <summary>本次调用真实提交了状态变更。</summary>
        Applied,
        /// <summary>同一命令重放（幂等命中，状态未变）。</summary>
        Replay,
        /// <summary>前置条件不满足（有明确 reason，**不是**失败重试）。</summary>
        Blocked,
        /// <summary>事实不确定（未知 schema / 数量不守恒 / 代次冲突）⇒ 进对账，不猜测。</summary>
        Unknown,
        /// <summary>参数非法。</summary>
        InvalidCommand
    }

    internal sealed class LaborFlowResult
    {
        public LaborFlowCode Code { get; }
        /// <summary>稳定原因码（呈现层翻译，GOV-009）。</summary>
        public string Reason { get; }
        /// <summary>提交身份（<see cref="LaborFlowCode.Applied"/> 时给出）。</summary>
        public string SubmissionId { get; }
        /// <summary>接收到的实物件数（0 = 无产品型）。</summary>
        public int AcceptedQuantity { get; }
        public LaborFlowResult(LaborFlowCode code, string reason = null, string submissionId = null, int acceptedQuantity = 0)
        { Code = code; Reason = reason; SubmissionId = submissionId; AcceptedQuantity = acceptedQuantity; }
    }

    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**劳务履约流程应用服务** —— 阶段推进、实体合同代次、产出批次登记、
    /// 交验提交与资格授予的**唯一写入点**。
    ///
    /// <para><b>职责边界</b>：本服务只写"过程事实"（流程行 / 批次行 / 提交行），**不写**合同与应付
    /// （那是 <see cref="EmploymentService"/> 的权威）；付款资格经 <see cref="WageEligibility"/> 由调用方
    /// 写回应付行。也不创建金钱、不启动 Job、不生成物品。</para>
    ///
    /// <para><b>确定性 ID</b>：WorkflowId / SubmissionId / BatchId 全部由合同与证据键派生（禁 Guid）——
    /// 读档、重放、重复恢复必须命中同一行，否则会复制物品或重复付款。</para>
    /// </summary>
    internal sealed class LaborFlowService
    {
        private readonly LaborFlowSaveData data;
        public LaborFlowService(LaborFlowSaveData data) { this.data = data ?? throw new ArgumentNullException(nameof(data)); }

        public bool PayloadIntact => data.PayloadIntact;
        public int WorkflowCount => data.WorkflowCount;
        public LaborWorkflow ReadWorkflow(string contractId) => data.ReadWorkflow(contractId);
        public LaborWorkflow ReadWorkflowFor(string pawnKey, string opportunityKey)
            => data.ReadWorkflowFor(pawnKey, opportunityKey);
        public LaborWorkflow ReadWorkflowById(string workflowId) => data.ReadWorkflowById(workflowId);
        /// <summary>全部流程 ID 的稳定序快照（周期维护用；有界）。</summary>
        public IReadOnlyList<string> WorkflowIds() => data.WorkflowIds();
        public bool RemoveIntentWorkflow(string workflowId) => data.RemoveIntentWorkflow(workflowId);
        public IReadOnlyList<LaborWorkflow> ReadWorkflowsFor(string pawnKey) => data.ReadWorkflowsFor(pawnKey);
        public IReadOnlyList<ProductBatch> ReadOpenBatchesFor(string contractId) => data.ReadOpenBatchesFor(contractId);
        public IReadOnlyList<ProductBatch> ReadBatchesFor(string contractId) => data.ReadBatchesFor(contractId);
        public SubmissionRecord ReadLatestSubmissionFor(string contractId) => data.ReadLatestSubmissionFor(contractId);
        public IReadOnlyList<SubmissionRecord> ReadSubmissionsFor(string contractId) => data.ReadSubmissionsFor(contractId);
        public IReadOnlyList<LaborFlowEvent> ReadHistory(string workflowId) => data.ReadHistory(workflowId);

        /// <summary>实际 Job 适配层传入稳定业务事件 ID。展示历史不能替代交验回执或付款幂等账本。</summary>
        public LaborFlowCode RecordEvent(LaborWorkflow workflow, string eventId, string actionCode, int now,
            string evidenceId = null, string submissionId = null, int quantity = 0, long amountMinor = 0,
            string resultCode = null, string productDefName = null)
        {
            workflow = Authoritative(workflow);
            if (workflow == null || string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(actionCode)
                || quantity < 0 || amountMinor < 0) return LaborFlowCode.InvalidCommand;
            if (!data.CanWrite) return LaborFlowCode.Unknown;
            if (data.HasEvent(workflow.WorkflowId, eventId)) return LaborFlowCode.Replay;
            return data.WriteEvent(new LaborFlowEvent(eventId, workflow, actionCode, now, evidenceId,
                submissionId, quantity, amountMinor, resultCode, productDefName))
                ? LaborFlowCode.Applied : LaborFlowCode.Blocked;
        }

        public LaborFlowCode SetStation(LaborWorkflow workflow, string stationKey, int now)
        {
            workflow = Authoritative(workflow);
            if (workflow == null) return LaborFlowCode.InvalidCommand;
            if (string.Equals(workflow.CurrentStationKey, stationKey ?? string.Empty, StringComparison.Ordinal))
                return LaborFlowCode.Replay;
            return WriteObserved(workflow.AtStation(stationKey, now), "StationChanged")
                ? LaborFlowCode.Applied : LaborFlowCode.Unknown;
        }

        private bool WriteObserved(LaborWorkflow workflow, string actionCode)
        {
            if (!data.WriteWorkflow(workflow)) return false;
            // 相同状态在同一 tick 的重放命中同键；真实工资/提交事件使用其权威业务 ID 的显式 RecordEvent。
            string id = "flowevent:" + workflow.WorkflowId + "|" + workflow.LastChangedTick + "|" + actionCode
                + "|" + workflow.Stage + "|" + workflow.Hold + "|" + workflow.CurrentStationKey
                + "|" + workflow.DocumentGeneration + "|" + workflow.ReasonCode;
            return data.WriteEvent(new LaborFlowEvent(id, workflow, actionCode, workflow.LastChangedTick));
        }

        /// <summary>
        /// 建立/接续**办理意图**（幂等：同一人同一机会已有流程行 ⇒ 返回既有行，**不重置阶段**）。
        /// 阶段推进只能前进（<see cref="LaborFlowRules.IsStageForward"/>）⇒ 崩溃重启不会把流程倒回。
        /// 本方法**不签约**：意图只是"准备到劳务站"，合同要到站 SignCommit 才产生（§4.2）。
        /// </summary>
        public LaborFlowCode BeginVisit(string pawnKey, string opportunityKey, string institutionKey,
            SettlementDeliveryMode mode, int now, out LaborWorkflow workflow)
        {
            workflow = null;
            if (string.IsNullOrWhiteSpace(pawnKey) || string.IsNullOrWhiteSpace(opportunityKey)
                || string.IsNullOrWhiteSpace(institutionKey)) return LaborFlowCode.InvalidCommand;
            if (mode != SettlementDeliveryMode.PhysicalSubmission && mode != SettlementDeliveryMode.LegacyImmediate)
                return LaborFlowCode.InvalidCommand;
            if (!data.CanWrite) return LaborFlowCode.Unknown;   // 未知 schema：不新建流程行
            var existing = data.ReadWorkflowFor(pawnKey, opportunityKey);
            if (existing != null && existing.Stage != LaborFlowStage.Archived)
            {
                // 已存在 ⇒ 只在无阶段时补 IntentReady，否则原样返回（幂等，不倒回）。
                workflow = existing.Stage == LaborFlowStage.None
                    ? existing.MovedTo(LaborFlowStage.IntentReady, "GotoLaborOffice", now)
                    : existing;
                if (workflow != existing) WriteObserved(workflow, "IntentReady");
                return LaborFlowCode.Replay;
            }
            // 归档行**不重置也不复用**（CA-09）：上一轮的合同身份、回执与批次一律留在原行，
            // 新一轮另开一行（`…|r{n}`）—— 否则二次求职会命中归档行 ⇒ 永远领不到办理 Job。
            int round = existing == null ? 1 : LaborWorkflow.RoundOf(existing.WorkflowId) + 1;
            if (round <= 0 || round > LaborWorkflow.MaximumRounds) return LaborFlowCode.Blocked;
            string workflowId = LaborWorkflow.IdFor(pawnKey, opportunityKey, round);
            if (data.ReadWorkflowById(workflowId) != null) return LaborFlowCode.Blocked;   // 复号：不猜，交人工
            workflow = new LaborWorkflow(workflowId, string.Empty, pawnKey, institutionKey, mode,
                LaborFlowStage.IntentReady, FlowHold.None, LaborFlowStage.None, string.Empty, "GotoLaborOffice",
                now, 0, 0, string.Empty, opportunityKey);
            return WriteObserved(workflow, "IntentReady") ? LaborFlowCode.Applied : LaborFlowCode.Blocked;
        }

        /// <summary>
        /// SignCommit 成功后**把权威合同身份写入同一流程行**（承接 Posted 时保留原 ContractId；
        /// 新建合同时为新的 ContractId）。代次已存在则**不重置**（补发走 <see cref="EnsureDocument"/>）。
        /// </summary>
        public LaborFlowCode AttachContract(ref LaborWorkflow workflow, string contractId,
            SettlementDeliveryMode mode, int now)
        {
            workflow = Authoritative(workflow);
            if (workflow == null || string.IsNullOrWhiteSpace(contractId)) return LaborFlowCode.InvalidCommand;
            if (mode != SettlementDeliveryMode.PhysicalSubmission && mode != SettlementDeliveryMode.LegacyImmediate)
                return LaborFlowCode.InvalidCommand;
            if (!string.IsNullOrEmpty(workflow.ContractId))
                return string.Equals(workflow.ContractId, contractId, StringComparison.Ordinal)
                    ? LaborFlowCode.Replay : LaborFlowCode.Blocked;   // 已绑定别的合同 ⇒ 不换绑
            var bound = workflow.WithContract(contractId, mode, now);
            if (!WriteObserved(bound, "ContractAttached")) return LaborFlowCode.Unknown;
            workflow = bound;
            return LaborFlowCode.Applied;
        }

        /// <summary>
        /// **权威当前行**：存档里有就用它，调用方实例只在行不存在时兜底。
        ///
        /// <para><b>为什么必须有这一层</b>：调用方持有的对象可能是**几步之前**的快照
        ///（例：<c>MoveTo</c> 返回后没有重新读取，紧接着 <c>EnsureDocument</c> 又用旧实例写回），
        /// 直接用陈旧实例构造新行会把中间推进**静默回退**（阶段倒退 = 玩家看到"已出发"又跳回"准备出发"）。
        /// 实测由 <c>LaborFlowScribeTests.RoundTripPreservesWorkflowSubmissionAndBatch</c> 复现。
        /// 所有写方法一律经此收敛。</para>
        /// </summary>
        private LaborWorkflow Authoritative(LaborWorkflow workflow)
        {
            if (workflow == null) return null;
            return data.ReadWorkflowById(workflow.WorkflowId) ?? workflow;
        }

        /// <summary>签约流程逐阶；已签合同允许当前实际动作循环。只写观察事实，不改变交验/付款资格。</summary>
        public LaborFlowCode MoveTo(LaborWorkflow workflow, LaborFlowStage stage, string nextAction, int now)
        {
            workflow = Authoritative(workflow);
            if (workflow == null || !Enum.IsDefined(typeof(LaborFlowStage), stage)) return LaborFlowCode.InvalidCommand;
            if (!LaborFlowRules.IsStageTransition(workflow.Stage, stage, !string.IsNullOrEmpty(workflow.ContractId)))
                return LaborFlowCode.Blocked;
            if (workflow.Stage == stage && string.Equals(workflow.NextAction, nextAction, StringComparison.Ordinal))
                return LaborFlowCode.Replay;
            var moved = workflow.MovedTo(stage, nextAction, now);
            return WriteObserved(moved, "StageChanged") ? LaborFlowCode.Applied : LaborFlowCode.Unknown;
        }

        /// <summary>进入叠加状态（暂停/阻断/对账）—— 保存恢复点与下次重试时刻，**不抹掉**阶段与权利。</summary>
        public LaborFlowCode HoldAt(LaborWorkflow workflow, FlowHold hold, string reason, string nextAction,
            LaborFlowStage resume, int now, int nextRetryTick = 0)
        {
            workflow = Authoritative(workflow);
            if (workflow == null || !Enum.IsDefined(typeof(FlowHold), hold)) return LaborFlowCode.InvalidCommand;
            if (hold == FlowHold.None) return LaborFlowCode.InvalidCommand;   // 解除用 Resume
            var held = workflow.HeldAt(hold, reason ?? string.Empty, nextAction ?? string.Empty, resume, now, nextRetryTick);
            return WriteObserved(held, "Held") ? LaborFlowCode.Applied : LaborFlowCode.Unknown;
        }

        /// <summary>解除叠加状态：回到保存的恢复点（阶段不倒退）。</summary>
        public LaborFlowCode Resume(LaborWorkflow workflow, int now)
        {
            workflow = Authoritative(workflow);
            if (workflow == null) return LaborFlowCode.InvalidCommand;
            if (workflow.Hold == FlowHold.None) return LaborFlowCode.Replay;
            var target = workflow.ResumeStage == LaborFlowStage.None ? workflow.Stage : workflow.ResumeStage;
            var resumed = workflow.HeldAt(FlowHold.None, string.Empty, workflow.NextAction, target, now, 0);
            return WriteObserved(resumed.MovedTo(target, workflow.NextAction, now), "Resumed")
                ? LaborFlowCode.Applied : LaborFlowCode.Unknown;
        }

        /// <summary>
        /// 确保该有合同纸：返回本次应使用的（代次, 实例 ID）。**只递增不重排** ——
        /// 已有代次 ⇒ 原样返回（幂等，重放不重印）；无代次 ⇒ 0 → 1（首次签发）。
        /// 补发由调用方显式 <paramref name="reissue"/> = true 触发（代次 +1，旧纸失效）。
        /// </summary>
        public LaborFlowCode EnsureDocument(ref LaborWorkflow workflow, bool reissue, int now, out int generation, out string instanceId)
        {
            generation = 0; instanceId = string.Empty;
            workflow = Authoritative(workflow);
            if (workflow == null) return LaborFlowCode.InvalidCommand;
            int next = workflow.DocumentGeneration <= 0 ? 1 : (reissue ? workflow.DocumentGeneration + 1 : workflow.DocumentGeneration);
            instanceId = LaborFlowRules.DocumentInstanceIdFor(workflow.ContractId, next);
            if (!reissue && workflow.DocumentGeneration > 0)
            {
                generation = workflow.DocumentGeneration; instanceId = workflow.DocumentInstanceId;
                return LaborFlowCode.Replay;
            }
            var updated = workflow.WithDocument(next, instanceId, now);
            if (!WriteObserved(updated, reissue ? "DocumentReissued" : "DocumentIssued")) return LaborFlowCode.Unknown;
            workflow = updated; generation = next;
            return LaborFlowCode.Applied;
        }

        /// <summary>
        /// 登记**真实产出批次**（来源明细）。同批次键幂等：已存在 ⇒ 只累加产生量（多趟产出的同一证据），
        /// 不覆盖已接收/已损失量（否则会"吃掉"已核销的部分）。
        /// </summary>
        public LaborFlowCode RegisterBatch(string contractId, string evidenceId, string producerPawnKey,
            string thingDefName, string ownerKey, int producedQuantity, int now, int thingIdNumber, string holderKey,
            out ProductBatch batch)
        {
            batch = null;
            if (string.IsNullOrWhiteSpace(contractId) || string.IsNullOrWhiteSpace(evidenceId)
                || string.IsNullOrWhiteSpace(producerPawnKey) || string.IsNullOrWhiteSpace(thingDefName)
                || producedQuantity <= 0) return LaborFlowCode.InvalidCommand;
            if (!data.CanWrite) return LaborFlowCode.Unknown;
            string batchId = ProductBatch.IdFor(contractId, evidenceId, thingDefName);
            var existing = data.ReadBatch(batchId);
            try
            {
                batch = existing == null
                    ? new ProductBatch(batchId, contractId, evidenceId, producerPawnKey, thingDefName, ownerKey,
                        producedQuantity, 0, 0, now, thingIdNumber, holderKey)
                    : new ProductBatch(batchId, contractId, evidenceId, producerPawnKey, thingDefName,
                        string.IsNullOrEmpty(existing.OwnerKey) ? ownerKey : existing.OwnerKey,
                        existing.ProducedQuantity + producedQuantity, existing.AcceptedQuantity, existing.LostQuantity,
                        existing.ProducedTick, existing.ThingIdNumber == 0 ? thingIdNumber : existing.ThingIdNumber,
                        string.IsNullOrEmpty(holderKey) ? existing.LocationHolderKey : holderKey);
            }
            catch (Exception) { return LaborFlowCode.InvalidCommand; }
            return data.WriteBatch(batch) ? LaborFlowCode.Applied : LaborFlowCode.Unknown;
        }

        /// <summary>
        /// 记录一次**交验提交**（接收回执）。数量守恒由 <see cref="ProductBatch.Receive"/> 保证；
        /// 本方法只写提交行与批次核销 —— 付款资格由调用方授予（<see cref="AuthorizeWageSettlement"/> 的判据）。
        /// 幂等：同 SubmissionId 已存在 ⇒ Replay（不重复核销、不重复接收）。
        /// </summary>
        public LaborFlowResult AcceptSubmission(string contractId, string pawnKey, string institutionKey,
            IEnumerable<string> evidenceIds, IReadOnlyList<string> batchIds, int acceptedQuantity,
            string receiptId, int now, bool reconciliation = false)
        {
            if (string.IsNullOrWhiteSpace(contractId) || string.IsNullOrWhiteSpace(pawnKey)
                || string.IsNullOrWhiteSpace(institutionKey)) return new LaborFlowResult(LaborFlowCode.InvalidCommand);
            if (acceptedQuantity < 0) return new LaborFlowResult(LaborFlowCode.InvalidCommand);
            if (!data.CanWrite) return new LaborFlowResult(LaborFlowCode.Unknown, "PayloadBlocked");
            string evidenceKey = Join(evidenceIds);
            if (evidenceKey.Length == 0) return new LaborFlowResult(LaborFlowCode.InvalidCommand);
            var workflow = data.ReadWorkflow(contractId);
            int generation = workflow?.DocumentGeneration ?? 0;
            // 多趟交验的回执键含**累计接收量**（由已落盘回执派生）：同趟重放 ⇒ 同键（Replay，不吞核销），
            // 不同趟 ⇒ 累计量严格递增 ⇒ 不同键（CA-02/T12 的分趟守恒）。
            int coveredTotal = data.SumAcceptedForContract(contractId) + acceptedQuantity;
            string submissionId = SubmissionRecord.IdFor(contractId, generation, evidenceKey, coveredTotal);
            if (data.ReadSubmission(submissionId) != null)
                return new LaborFlowResult(LaborFlowCode.Replay, "AlreadySubmitted", submissionId, acceptedQuantity);

            // ── 全量预检（CA-04）：所有**可失败判定**必须在任何写入之前完成 ──
            // 逐批核算可核销量；任一批次缺失 / 数量不守恒 / 提交表已满 ⇒ 立即 Unknown 且**零写入**
            //（修前实测反例：失败返回 Unknown，但批次已被核销 10 件、回执 0 条 = 孤立核销）。
            var takes = new List<int>(batchIds?.Count ?? 0);
            if (!reconciliation && acceptedQuantity > 0)
            {
                if (batchIds == null || batchIds.Count == 0)
                    return new LaborFlowResult(LaborFlowCode.Unknown, "BatchUnknown");
                if (!data.CanAcceptSubmission)
                    return new LaborFlowResult(LaborFlowCode.Unknown, "SubmissionCapacityReached");
                int remaining = acceptedQuantity;
                for (int i = 0; i < batchIds.Count && remaining > 0; i++)
                {
                    var batch = data.ReadBatch(batchIds[i]);
                    if (batch == null) return new LaborFlowResult(LaborFlowCode.Unknown, "BatchUnknown");
                    int take = Math.Min(batch.RemainingQuantity, remaining);
                    takes.Add(take);
                    remaining -= take;
                }
                if (remaining > 0) return new LaborFlowResult(LaborFlowCode.Unknown, "SampleNotCovered");
            }
            SubmissionRecord record;
            try
            {
                record = new SubmissionRecord(submissionId, contractId, pawnKey, institutionKey,
                    evidenceIds, batchIds, now, acceptedQuantity, receiptId, reconciliation);
            }
            catch (Exception) { return new LaborFlowResult(LaborFlowCode.InvalidCommand); }
            // 预检已保证：批次行都存在、量都不超剩余、提交表有余量 ⇒ 下列写入不会再因这三项失败
            //（既有行更新不占容量；本路径不新增批次行）。
            for (int i = 0; i < takes.Count; i++)
            {
                if (takes[i] <= 0) continue;
                var batch = data.ReadBatch(batchIds[i]);
                if (batch == null) return new LaborFlowResult(LaborFlowCode.Unknown, "BatchUnknown");
                ProductBatch received;
                try { received = batch.Receive(takes[i]); }
                catch (Exception) { return new LaborFlowResult(LaborFlowCode.Unknown, "BatchNotConserved"); }
                if (!data.WriteBatch(received)) return new LaborFlowResult(LaborFlowCode.Unknown, "BatchWriteBlocked");
            }
            return data.WriteSubmission(record)
                ? new LaborFlowResult(LaborFlowCode.Applied, null, submissionId, acceptedQuantity)
                : new LaborFlowResult(LaborFlowCode.Unknown, "SubmissionWriteBlocked");
        }

        /// <summary>
        /// 授予付款资格：返回该合同当前**可用于付款**的提交（须已核验、非对账）。
        /// 这是"交验 → 付款"的唯一桥：付款侧凭 <c>SubmissionId</c> 写回应付的
        /// <see cref="WageEligibility.Submitted"/>，**不**由本服务直接改应付（分层纪律）。
        ///
        /// <para><b>⚠ 多趟交验（CA-02）：本方法只回答"有没有可用的提交回执"。</b>
        /// "这批货是否**已全部接收**"是另一个判据 —— 付款侧必须再过
        /// <see cref="IsDeliveryCovered"/>，否则第一趟的部分接收就能把整笔工资放行。</para>
        /// </summary>
        public SubmissionRecord PaymentGrant(string contractId)
        {
            var latest = data.ReadLatestSubmissionFor(contractId);
            if (latest == null) return null;
            if (latest.Reconciliation) return null;              // 对账中 ⇒ 不给付款资格
            if (latest.AcceptedQuantity <= 0 && string.IsNullOrEmpty(latest.ReceiptId)) return null;
            return latest;
        }

        /// <summary>
        /// 该证据的真实产物是否**已全部接收**（覆盖完成；只读）。判据以**批次台账**为准（含损失核销）——
        /// 不以"最近一条回执的数量"推断。无该证据的批次 ⇒ false（不猜，交给对账）。
        /// </summary>
        /// <summary>
        /// 处置「对账中」（CA-10）：把对账从**死状态**变成有真实入口的可恢复状态。人工先核验物理事实后给出决定 ——
        /// ① <see cref="ReconciliationAction.Resume"/>：事实已查清，清 Hold 回到「按剩余量继续办理」；
        /// ② <see cref="ReconciliationAction.WriteOffRemaining"/>：剩余产物经核验**无法交付**（损毁/丢失/被吞），
        ///    按损失逐批核销剩余量 ⇒ 覆盖完成（随后仍须走既有「全部接收才放款」的门）。
        /// <para>**不减轻债权**：本方法只改流程状态与批次核销，不做任何付款决定（付款仍由既有付款门裁决）。
        /// 未查明事实时**不猜**：契约/批次缺失、写入被阻一律返回 Unknown 并把原因交给调用方。</para>
        /// </summary>
        public LaborFlowCode ResolveReconciliation(string workflowId, ReconciliationAction action, int now, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(workflowId)) { reason = "WorkflowUnknown"; return LaborFlowCode.InvalidCommand; }
            var workflow = data.ReadWorkflowById(workflowId);
            if (workflow == null) { reason = "WorkflowUnknown"; return LaborFlowCode.Unknown; }
            if (!data.CanWrite) { reason = "PayloadBlocked"; return LaborFlowCode.Unknown; }
            if (workflow.Hold != FlowHold.ReconciliationRequired) { reason = "NoReconciliation"; return LaborFlowCode.Replay; }
            if (action == ReconciliationAction.WriteOffRemaining)
            {
                if (string.IsNullOrEmpty(workflow.ContractId)) { reason = "ContractMissing"; return LaborFlowCode.Unknown; }
                var open = data.ReadOpenBatchesFor(workflow.ContractId);
                int written = 0;
                for (int i = 0; i < open.Count; i++)
                {
                    var batch = open[i];
                    if (batch == null || batch.RemainingQuantity <= 0) continue;
                    var code = WriteOffBatch(batch.BatchId, batch.RemainingQuantity);
                    if (code != LaborFlowCode.Applied && code != LaborFlowCode.Replay)
                    {
                        reason = "WriteOffBlocked";
                        return LaborFlowCode.Unknown;
                    }
                    written += batch.RemainingQuantity;
                }
                if (written <= 0) reason = "NothingToWriteOff";
            }
            // 清 Hold：回到保存的恢复点（阶段不倒退；权利与已核销部分原样保留）。
            var live = data.ReadWorkflowById(workflowId) ?? workflow;
            var cleared = Resume(live, now);
            if (cleared != LaborFlowCode.Applied && cleared != LaborFlowCode.Replay)
            {
                reason = "WorkflowWriteBlocked";
                return LaborFlowCode.Unknown;
            }
            return LaborFlowCode.Applied;
        }

        public bool IsDeliveryCovered(string contractId, string evidenceId)
        {
            if (string.IsNullOrEmpty(contractId) || string.IsNullOrEmpty(evidenceId)) return false;
            var all = data.ReadBatchesFor(contractId);
            bool any = false;
            for (int i = 0; i < all.Count; i++)
            {
                var batch = all[i];
                if (batch == null || !string.Equals(batch.EvidenceId, evidenceId, StringComparison.Ordinal)) continue;
                any = true;
                if (batch.RemainingQuantity > 0) return false;
            }
            return any;
        }

        /// <summary>物资已接收但资格写入中断时，只使用匹配受益人/证据的真实回执恢复；绝不再搬货。</summary>
        public SubmissionRecord ReadCoveredSubmission(string contractId, string evidenceId, string pawnKey)
        {
            if (string.IsNullOrWhiteSpace(pawnKey) || !IsDeliveryCovered(contractId, evidenceId)) return null;
            SubmissionRecord latest = null;
            foreach (var submission in data.ReadSubmissionsFor(contractId))
            {
                if (submission == null || submission.Reconciliation
                    || !string.Equals(submission.ContractId, contractId, StringComparison.Ordinal)
                    || !string.Equals(submission.PawnKey, pawnKey, StringComparison.Ordinal)) continue;
                bool matches = false;
                foreach (string id in submission.EvidenceIds)
                    if (string.Equals(id, evidenceId, StringComparison.Ordinal)) { matches = true; break; }
                if (!matches) continue;
                if (latest == null || submission.AcceptedTick > latest.AcceptedTick
                    || (submission.AcceptedTick == latest.AcceptedTick
                        && string.CompareOrdinal(submission.SubmissionId, latest.SubmissionId) > 0)) latest = submission;
            }
            return latest;
        }

        /// <summary>必须在真实 SplitOff/取入库存前调用。已存在运输子批次不允许再次搬取。</summary>
        public bool CanTransferBatchIdentity(string batchId, int expectedSourceThingId, int quantity, string transportId)
        {
            if (!data.CanWrite || expectedSourceThingId <= 0 || quantity <= 0 || string.IsNullOrWhiteSpace(transportId)) return false;
            var source = data.ReadBatch(batchId);
            if (source == null || source.ThingIdNumber != expectedSourceThingId || quantity > source.RemainingQuantity) return false;
            if (data.ReadBatch(batchId + "|transport:" + transportId) != null) return false;
            bool wholeUnsettled = quantity == source.RemainingQuantity && source.AcceptedQuantity == 0 && source.LostQuantity == 0;
            return wholeUnsettled || data.BatchCount < LaborFlowSaveData.MaximumBatches;
        }

        /// <summary>原版取货成功后记录真实实例迁移。部分拆分生成稳定子批次，总产出/接收/损失/剩余守恒。</summary>
        public LaborFlowCode TransferBatchIdentity(string batchId, int expectedSourceThingId, int destThingId,
            int quantity, string transportId, int now, string holderKey)
        {
            if (string.IsNullOrWhiteSpace(batchId) || string.IsNullOrWhiteSpace(transportId)
                || expectedSourceThingId <= 0 || destThingId <= 0 || quantity <= 0) return LaborFlowCode.InvalidCommand;
            if (!data.CanWrite) return LaborFlowCode.Unknown;
            var source = data.ReadBatch(batchId);
            if (source == null) return LaborFlowCode.Blocked;
            string childId = batchId + "|transport:" + transportId;
            var existing = data.ReadBatch(childId);
            if (existing != null)
                return existing.ContractId == source.ContractId && existing.EvidenceId == source.EvidenceId
                    && existing.ProducerPawnKey == source.ProducerPawnKey && existing.ThingDefName == source.ThingDefName
                    && existing.ProducedQuantity == quantity && existing.ThingIdNumber == destThingId
                    && existing.LocationHolderKey == (holderKey ?? string.Empty)
                    ? LaborFlowCode.Replay : LaborFlowCode.Blocked;
            if (quantity == source.RemainingQuantity && source.AcceptedQuantity == 0 && source.LostQuantity == 0
                && source.ThingIdNumber == destThingId && source.LocationHolderKey == (holderKey ?? string.Empty))
                return LaborFlowCode.Replay;
            if (!CanTransferBatchIdentity(batchId, expectedSourceThingId, quantity, transportId)) return LaborFlowCode.Blocked;
            bool wholeUnsettled = quantity == source.RemainingQuantity && source.AcceptedQuantity == 0 && source.LostQuantity == 0;
            if (wholeUnsettled)
            {
                var moved = new ProductBatch(source.BatchId, source.ContractId, source.EvidenceId, source.ProducerPawnKey,
                    source.ThingDefName, source.OwnerKey, source.ProducedQuantity, source.AcceptedQuantity,
                    source.LostQuantity, source.ProducedTick, destThingId, holderKey);
                return data.WriteBatch(moved) ? LaborFlowCode.Applied : LaborFlowCode.Unknown;
            }
            var remainder = new ProductBatch(source.BatchId, source.ContractId, source.EvidenceId, source.ProducerPawnKey,
                source.ThingDefName, source.OwnerKey, source.ProducedQuantity - quantity, source.AcceptedQuantity,
                source.LostQuantity, source.ProducedTick, source.ThingIdNumber, source.LocationHolderKey);
            var child = new ProductBatch(childId, source.ContractId, source.EvidenceId, source.ProducerPawnKey,
                source.ThingDefName, source.OwnerKey, quantity, 0, 0, source.ProducedTick, destThingId, holderKey);
            // 两行与容量都已预检，单线程领域写入没有外部调用；绝不在容量失败后扣原批次数量。
            if (!data.WriteBatch(child)) return LaborFlowCode.Unknown;
            return data.WriteBatch(remainder) ? LaborFlowCode.Applied : LaborFlowCode.Unknown;
        }

        /// <summary>写回一条批次（接收/损失核销后的新状态）。数量守恒由批次值对象自身保证。</summary>
        public bool WriteBatch(ProductBatch batch) => data.WriteBatch(batch);

        /// <summary>可信损失核销（只解除交货资格，**不增工资、不追回已付**）。</summary>
        public LaborFlowCode WriteOffBatch(string batchId, int quantity)
        {
            if (string.IsNullOrWhiteSpace(batchId) || quantity <= 0) return LaborFlowCode.InvalidCommand;
            var batch = data.ReadBatch(batchId);
            if (batch == null) return LaborFlowCode.Blocked;
            ProductBatch written;
            try { written = batch.Wrote(true, quantity); }
            catch (Exception) { return LaborFlowCode.InvalidCommand; }
            return data.WriteBatch(written) ? LaborFlowCode.Applied : LaborFlowCode.Unknown;
        }

        /// <summary>该合同是否仍有未核销批次（流程是否停在交货段的判据；只读）。</summary>
        public bool HasOpenBatches(string contractId)
        {
            var open = data.ReadOpenBatchesFor(contractId);
            return open.Count > 0;
        }

        private static string Join(IEnumerable<string> values)
        {
            if (values == null) return string.Empty;
            var rows = new List<string>();
            foreach (string value in values) if (!string.IsNullOrWhiteSpace(value)) rows.Add(value);
            rows.Sort(StringComparer.Ordinal);   // 确定序 ⇒ 同一证据集合恒定派生同一 SubmissionId
            return string.Join(",", rows.ToArray());
        }
    }
}

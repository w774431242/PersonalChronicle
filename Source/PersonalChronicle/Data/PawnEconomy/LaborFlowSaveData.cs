using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using Verse;

namespace PersonalChronicle.Data.PawnEconomy
{
    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**劳务履约流程的独立版本化载体**（新节点 <c>pawnEconomicLaborFlow</c>）。
    ///
    /// <para><b>为什么独立成节点而不并入 <c>EmploymentSaveData</c></b>：合同/应付是**金融权利**账本
    /// （数值与身份已冻结），流程阶段/代次/批次是**过程事实**。两者生命周期不同（权利长期保留、
    /// 过程可归档裁剪），且新节点的 schema 版本必须可独立识别 —— 混表会让"旧档缺键"与
    /// "新档未知版本"无法区分。</para>
    ///
    /// <para><b>fail-closed</b>：① 缺节点（旧档）⇒ 空载体，一切按"没有流程行"解释，**不影响任何既有权利**；
    /// ② <see cref="SchemaVersion"/> 高于本版本 ⇒ <see cref="PayloadIntact"/> = false ⇒ **所有写入被阻断**
    /// （不覆写、不截断、不猜测），读取仍可尽力呈现（诊断可见）；③ 行内无法识别的枚举值 ⇒ 该行阻断其业务推进。</para>
    /// </summary>
    internal sealed class LaborFlowSaveData : IExposable
    {
        /// <summary>本版本能理解的最新 schema。行内无版本 ⇒ 0（空载体，旧档）。</summary>
        /// <summary>
        /// schema 版本。v2（2026-10-12 CA-09 轮次修复）：流程 ID 允许 `|r{n}` 轮次后缀（**后缀即权威**，
        /// 不新增落盘字段），多趟交验回执 ID 可为 `…|a{累计量}`（旧格式行仍可读）。
        ///
        /// <para><b>为什么必须升版本而不是"沿用 1"</b>：v1 读取者的 <c>ReadWorkflowFor</c> 只会派生旧 ID，
        /// 拿到 v2 档案时读不到当前轮、并会复现"归档后二次求职死锁"；升版本让"被改过的载荷"如实标注自身
        /// 已被 v2 语义改写（SAVE-001：版本必须可识别，不得版本谎报）。旧档（0/1）载入时按下行升级为 2 ——
        /// 本版**没有新增必需字段**，升级是加性且无损的。</para>
        /// </summary>
        public const int CurrentSchemaVersion = 3;
        /// <summary>v3：加性柜台位置与只读事件快照；历史有界，不裁剪业务回执/工资权利。</summary>
        public const int MaximumHistoryPerWorkflow = 64;
        public const int MaximumWorkflows = 1024, MaximumSubmissions = 4096, MaximumBatches = 4096;

        public sealed class WorkflowRow : IExposable
        {
            public string Workflow, Contract, Pawn, Institution, Mode, Stage, Hold, ResumeStage, Reason, NextAction;
            /// <summary>办理机会键（意图阶段的业务身份；合同产生前非空）。</summary>
            public string Opportunity;
            public int LastChangedTick, NextRetryTick, DocumentGeneration;
            public string DocumentInstance;
            public string CurrentStation;
            public List<EventRow> History = new List<EventRow>();
            public void ExposeData()
            {
                Scribe_Values.Look(ref Workflow, "workflow"); Scribe_Values.Look(ref Contract, "contract");
                Scribe_Values.Look(ref Opportunity, "opportunity");
                Scribe_Values.Look(ref Pawn, "pawn"); Scribe_Values.Look(ref Institution, "institution");
                Scribe_Values.Look(ref Mode, "mode"); Scribe_Values.Look(ref Stage, "stage");
                Scribe_Values.Look(ref Hold, "hold"); Scribe_Values.Look(ref ResumeStage, "resumeStage");
                Scribe_Values.Look(ref Reason, "reason"); Scribe_Values.Look(ref NextAction, "nextAction");
                Scribe_Values.Look(ref LastChangedTick, "lastChangedTick");
                Scribe_Values.Look(ref NextRetryTick, "nextRetryTick");
                Scribe_Values.Look(ref DocumentGeneration, "docGen");
                Scribe_Values.Look(ref DocumentInstance, "docInstance");
                Scribe_Values.Look(ref CurrentStation, "currentStation");
                Scribe_Collections.Look(ref History, "history", LookMode.Deep);
                if (Scribe.mode == LoadSaveMode.PostLoadInit && History == null) History = new List<EventRow>();
            }
        }

        public sealed class EventRow : IExposable
        {
            public string Event, Workflow, Contract, Pawn, Institution, Station, Stage, Hold, Reason, NextAction;
            public string Action, Evidence, Submission, Result, ProductDef;
            public int Tick, Quantity;
            public long AmountMinor;
            public void ExposeData()
            {
                Scribe_Values.Look(ref Event, "eventId"); Scribe_Values.Look(ref Workflow, "workflow");
                Scribe_Values.Look(ref Contract, "contract"); Scribe_Values.Look(ref Pawn, "pawn");
                Scribe_Values.Look(ref Institution, "institution"); Scribe_Values.Look(ref Station, "station");
                Scribe_Values.Look(ref Stage, "stage"); Scribe_Values.Look(ref Hold, "hold");
                Scribe_Values.Look(ref Reason, "reason"); Scribe_Values.Look(ref NextAction, "nextAction");
                Scribe_Values.Look(ref Action, "action"); Scribe_Values.Look(ref Evidence, "evidence");
                Scribe_Values.Look(ref Submission, "submission"); Scribe_Values.Look(ref Result, "result");
                Scribe_Values.Look(ref ProductDef, "productDef"); Scribe_Values.Look(ref Tick, "tick");
                Scribe_Values.Look(ref Quantity, "quantity"); Scribe_Values.Look(ref AmountMinor, "amountMinor");
            }
        }

        public sealed class SubmissionRow : IExposable
        {
            public string Submission, Contract, Pawn, Institution;
            public List<string> Evidence = new List<string>();
            public List<string> Batches = new List<string>();
            public int AcceptedTick, AcceptedQuantity;
            public string Receipt;
            public bool Reconciliation;
            public void ExposeData()
            {
                Scribe_Values.Look(ref Submission, "submission"); Scribe_Values.Look(ref Contract, "contract");
                Scribe_Values.Look(ref Pawn, "pawn"); Scribe_Values.Look(ref Institution, "institution");
                Scribe_Collections.Look(ref Evidence, "evidence", LookMode.Value);
                Scribe_Collections.Look(ref Batches, "batches", LookMode.Value);
                Scribe_Values.Look(ref AcceptedTick, "acceptedTick");
                Scribe_Values.Look(ref AcceptedQuantity, "acceptedQuantity");
                Scribe_Values.Look(ref Receipt, "receipt");
                Scribe_Values.Look(ref Reconciliation, "reconciliation");
                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    if (Evidence == null) Evidence = new List<string>();
                    if (Batches == null) Batches = new List<string>();
                }
            }
        }

        public sealed class BatchRow : IExposable
        {
            public string Batch, Contract, Evidence, Producer, Def, Owner, Holder;
            public int Produced, Accepted, Lost, ProducedTick, ThingId;
            public void ExposeData()
            {
                Scribe_Values.Look(ref Batch, "batch"); Scribe_Values.Look(ref Contract, "contract");
                Scribe_Values.Look(ref Evidence, "evidence"); Scribe_Values.Look(ref Producer, "producer");
                Scribe_Values.Look(ref Def, "def"); Scribe_Values.Look(ref Owner, "owner");
                Scribe_Values.Look(ref Holder, "holder");
                Scribe_Values.Look(ref Produced, "produced"); Scribe_Values.Look(ref Accepted, "accepted");
                Scribe_Values.Look(ref Lost, "lost"); Scribe_Values.Look(ref ProducedTick, "producedTick");
                Scribe_Values.Look(ref ThingId, "thingId");
            }
        }

        private int schemaVersion;
        private bool payloadIntact = true;
        /// <summary>未知（更高）版本节点的**原始 XML**（CA-05：跨保存/载入往返原样保留，供未来版本或人工取证）。</summary>
        private string rawPayload;
        /// <summary>载入时是否发生**超限裁剪**（CA-16：显式记录，绝不「截断还声称完整」）。</summary>
        private bool trimmedOnLoad;
        private List<WorkflowRow> workflows = new List<WorkflowRow>();
        private List<SubmissionRow> submissions = new List<SubmissionRow>();
        private List<BatchRow> batches = new List<BatchRow>();
        private Dictionary<string, WorkflowRow> workflowIndex;
        private Dictionary<string, SubmissionRow> submissionIndex;
        private Dictionary<string, BatchRow> batchIndex;

        /// <summary>存档里的 schema 版本（0 = 旧档无节点 / 空载体）。</summary>
        public int SchemaVersion => schemaVersion;
        /// <summary>载荷是否可被本版本安全理解。false ⇒ **一切写入被阻断**（保留原始载荷，不覆写、不截断）。</summary>
        public bool PayloadIntact => payloadIntact;
        /// <summary>未知版本的原始载荷（null = 无未知载荷）。用于人工取证与未来版本迁移。</summary>
        public string RawPayload => rawPayload;
        /// <summary>本载体载入时是否发生过超限裁剪（只裁已归档流程与已结清批次；未结权利一律不裁）。</summary>
        public bool TrimmedOnLoad => trimmedOnLoad;

        public void ExposeData()
        {
            Scribe_Values.Look(ref schemaVersion, "schemaVersion", 0);
            Scribe_Values.Look(ref rawPayload, "rawPayload", null);
            Scribe_Collections.Look(ref workflows, "workflows", LookMode.Deep);
            Scribe_Collections.Look(ref submissions, "submissions", LookMode.Deep);
            Scribe_Collections.Look(ref batches, "batches", LookMode.Deep);
            // CA-05：**未知（更高）版本时先把本节点原始 XML 原样留证**，并在保存时原样写回 ——
            // 修前只置 CanWrite=false：本载体在下次保存时仍会把「未来字段」整段丢掉（经理探针实证 schema99
            // 的未来字段在往返后消失）。这里让未知载荷**跨保存/载入往返仍然存在**。
            if (Scribe.mode == LoadSaveMode.LoadingVars
                && schemaVersion > CurrentSchemaVersion
                && Scribe.loader != null && Scribe.loader.curXmlParent != null)
                rawPayload = Scribe.loader.curXmlParent.OuterXml;
            if (Scribe.mode == LoadSaveMode.Saving && !payloadIntact && !string.IsNullOrEmpty(rawPayload))
                Scribe.saver.WriteElement("rawPayload", rawPayload);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            if (workflows == null) workflows = new List<WorkflowRow>();
            if (submissions == null) submissions = new List<SubmissionRow>();
            if (batches == null) batches = new List<BatchRow>();
            workflowIndex = null; submissionIndex = null; batchIndex = null;
            _ = WorkflowIndex; _ = SubmissionIndex; _ = BatchIndex;
            // 未知（更高）版本：保留原始载荷、阻断不确定写入 —— **不裁剪**（§9.1）。
            payloadIntact = schemaVersion <= CurrentSchemaVersion;
            if (!payloadIntact)
            {
                Log.WarningOnce("[PC][LABORFLOW] save node schemaVersion=" + schemaVersion
                    + " is newer than supported " + CurrentSchemaVersion
                    + "; labor-flow writes are blocked and the original payload is preserved.", 0x1EC0F01);
                return;
            }
            if (schemaVersion < CurrentSchemaVersion) { schemaVersion = CurrentSchemaVersion; }
            TrimOnLoad();
        }

        /// <summary>载入裁剪：**未结权利行一律不裁** —— 未归档流程、未结清批次优先保留（有界兜底）。</summary>
        private void TrimOnLoad()
        {
            foreach (var workflow in workflows)
            {
                if (workflow?.History == null) continue;
                if (workflow.History.Count <= MaximumHistoryPerWorkflow) continue;
                workflow.History.RemoveRange(0, workflow.History.Count - MaximumHistoryPerWorkflow);
                trimmedOnLoad = true;
            }
            if (workflows.Count > MaximumWorkflows)
            {
                var keep = new List<WorkflowRow>(MaximumWorkflows);
                for (int i = 0; i < workflows.Count && keep.Count < MaximumWorkflows; i++)
                {
                    var row = workflows[i];
                    if (row == null) continue;
                    var stage = ParseStage(row.Stage, LaborFlowStage.None);
                    if (stage == LaborFlowStage.Archived) continue;   // 只裁已归档（终态）
                    keep.Add(row);
                }
                // 仍有额度则补回已归档行（保持有界，但不丢未终态）。
                // **归档行按轮号降序保留**（CA-09）：先保最高轮 —— 否则"最高轮被裁、只剩 r1"会让
                // 下一轮派生回 r2 而复号，把两轮的历史与权利混成一行。
                var archived = new List<WorkflowRow>(workflows.Count);
                for (int i = 0; i < workflows.Count; i++)
                {
                    var row = workflows[i];
                    if (row == null || keep.Contains(row)) continue;
                    if (ParseStage(row.Stage, LaborFlowStage.None) != LaborFlowStage.Archived) continue;
                    archived.Add(row);
                }
                archived.Sort((a, b) => LaborWorkflow.RoundOf(b.Workflow).CompareTo(LaborWorkflow.RoundOf(a.Workflow)));
                for (int i = 0; i < archived.Count && keep.Count < MaximumWorkflows; i++) keep.Add(archived[i]);
                workflows = keep; workflowIndex = null;
                trimmedOnLoad = true;
                Log.WarningOnce("[PC][LABORFLOW] labor-flow workflows exceeded " + MaximumWorkflows
                    + "; only archived rounds were trimmed (unsettled rights untouched).", 0x1EC0F08);
            }
            // 提交行**刻意不裁**（2026-10-12 修正）：它们是**幂等回执** —— 丢掉一条会让读档后的重复交验
            // 重新核销同一批货（重复取货、账面与实物脱节）。而"可安全裁"所需的终态证据（该合同已无未付
            // 应付）**不在本载体内**（应付在 EmploymentSaveData）⇒ 本层无法证明安全，故一律保留。
            // 达到上限时 WriteSubmission 如实返回 false（阻断**新**提交），绝不为了腾位置丢掉既有凭据
            //（与同族 evidenceKeys 的"宁可不裁也不丢去重键"同一策略）。
            if (batches.Count > MaximumBatches)
            {
                var keep = new List<BatchRow>(MaximumBatches);
                for (int i = 0; i < batches.Count && keep.Count < MaximumBatches; i++)
                {
                    var row = batches[i];
                    if (row == null) continue;
                    if (row.Produced - row.Accepted - row.Lost <= 0) continue;   // 已核销完毕可裁
                    keep.Add(row);
                }
                for (int i = 0; i < batches.Count && keep.Count < MaximumBatches; i++)
                {
                    var row = batches[i];
                    if (row == null || keep.Contains(row)) continue;
                    if (row.Produced - row.Accepted - row.Lost > 0) continue;
                    keep.Add(row);
                }
                batches = keep; batchIndex = null;
                trimmedOnLoad = true;
                Log.WarningOnce("[PC][LABORFLOW] labor-flow batches exceeded " + MaximumBatches
                    + "; only settled batches were trimmed (open rights untouched).", 0x1EC0F09);
            }
        }

        private Dictionary<string, WorkflowRow> WorkflowIndex
        {
            get
            {
                if (workflowIndex != null) return workflowIndex;
                workflowIndex = new Dictionary<string, WorkflowRow>(StringComparer.Ordinal);
                for (int i = 0; i < workflows.Count; i++)
                {
                    var row = workflows[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.Workflow)) continue;
                    workflowIndex[row.Workflow] = row;
                }
                return workflowIndex;
            }
        }
        private Dictionary<string, SubmissionRow> SubmissionIndex
        {
            get
            {
                if (submissionIndex != null) return submissionIndex;
                submissionIndex = new Dictionary<string, SubmissionRow>(StringComparer.Ordinal);
                for (int i = 0; i < submissions.Count; i++)
                {
                    var row = submissions[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.Submission)) continue;
                    submissionIndex[row.Submission] = row;
                }
                return submissionIndex;
            }
        }
        private Dictionary<string, BatchRow> BatchIndex
        {
            get
            {
                if (batchIndex != null) return batchIndex;
                batchIndex = new Dictionary<string, BatchRow>(StringComparer.Ordinal);
                for (int i = 0; i < batches.Count; i++)
                {
                    var row = batches[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.Batch)) continue;
                    batchIndex[row.Batch] = row;
                }
                return batchIndex;
            }
        }

        public int WorkflowCount => WorkflowIndex.Count;
        public int SubmissionCount => SubmissionIndex.Count;
        public int BatchCount => BatchIndex.Count;

        /// <summary>全部流程 ID 的**稳定序**快照（周期维护用；有界 —— 上限即表容量）。
        /// 返回副本而非枚举器：调用方可在遍历中安全删除行。</summary>
        public IReadOnlyList<string> WorkflowIds()
        {
            var rows = new List<string>(workflows.Count);
            for (int i = 0; i < workflows.Count; i++)
            {
                var row = workflows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.Workflow)) continue;
                rows.Add(row.Workflow);
            }
            return rows.AsReadOnly();
        }
        /// <summary>能否安全写入（未知 schema ⇒ false ⇒ 调用方必须阻断并如实报告）。</summary>
        public bool CanWrite => payloadIntact;

        /// <summary>
        /// 提交表是否还能接收新回执（**前置预检**用）。必须在"核销批次"之前判 —— 否则会出现
        /// "批次已核销、回执写不进去"的孤立核销（管理器审计 CA-04 已复现实例：Accepted=10、回执=0）。
        /// 撤销/覆盖旧回执不受此限（只更新既有行）。
        /// </summary>
        public bool CanAcceptSubmission => payloadIntact && submissions.Count < MaximumSubmissions;

        /// <summary>该合同**累计已接收件数**（多趟交验的回执键与"是否覆盖完成"的权威来源；只读）。
        /// 取自已落盘回执 ⇒ 重放同一趟算出同键，不同趟的累计量严格递增。</summary>
        public int SumAcceptedForContract(string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return 0;
            int total = 0;
            foreach (var row in SubmissionIndex.Values)
            {
                if (row == null || !string.Equals(row.Contract, contractId, StringComparison.Ordinal)) continue;
                if (row.AcceptedQuantity > 0) total += row.AcceptedQuantity;
            }
            return total;
        }

        public LaborWorkflow ReadWorkflow(string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return null;
            foreach (var row in WorkflowIndex.Values)
            {
                if (row == null || !string.Equals(row.Contract, contractId, StringComparison.Ordinal)) continue;
                return Materialize(row);
            }
            return null;
        }
        public LaborWorkflow ReadWorkflowById(string workflowId)
        {
            if (string.IsNullOrEmpty(workflowId) || !WorkflowIndex.TryGetValue(workflowId, out var row)) return null;
            return Materialize(row);
        }
        /// <summary>
        /// 按"人 + 机会"取**当前轮**流程行（意图阶段与签约后同一行；同一对人/机会有多轮时取轮号最高者）。
        ///
        /// <para>轮号**只从 ID 后缀解析**（<see cref="LaborWorkflow.RoundOf"/> 是唯一权威，不设第二份字段）。
        /// 轮号非法（`|r1` / `|rx` / 超上限）或同轮并列 ⇒ 返回 null = **不猜**（调用方按"无流程行"处理，
        /// 回到"什么都没有产生"的安全态；绝不误挑一行当成当前轮）。</para>
        /// </summary>
        public LaborWorkflow ReadWorkflowFor(string pawnKey, string opportunityKey)
        {
            if (string.IsNullOrEmpty(pawnKey)) return null;
            LaborWorkflow best = null;
            int bestRound = 0;
            bool ambiguous = false;
            foreach (var row in WorkflowIndex.Values)
            {
                if (row == null || !string.Equals(row.Pawn, pawnKey, StringComparison.Ordinal)) continue;
                if (!string.Equals(row.Opportunity, opportunityKey, StringComparison.Ordinal)) continue;
                int round = LaborWorkflow.RoundOf(row.Workflow);
                if (round <= 0) return null;
                if (round > bestRound) { best = ReadWorkflowById(row.Workflow); bestRound = round; ambiguous = false; }
                else if (round == bestRound) ambiguous = true;
            }
            return ambiguous ? null : best;
        }

        /// <summary>行 → 值对象。行内无法识别的枚举值 ⇒ 返回 null（调用方按"无流程行"处理，**绝不猜测**推进）。</summary>
        private LaborWorkflow Materialize(WorkflowRow row)
        {
            if (row == null) return null;
            var mode = LaborFlowRules.ParseMode(row.Mode, SettlementDeliveryMode.PhysicalSubmission);
            if (mode == SettlementDeliveryMode.Unknown) { WarnRow(row.Workflow, "mode"); return null; }
            var stage = ParseStage(row.Stage, LaborFlowStage.None);
            var hold = ParseHold(row.Hold, FlowHold.None);
            var resume = ParseStage(row.ResumeStage, LaborFlowStage.None);
            if (stage == LaborFlowStage.None && !string.IsNullOrEmpty(row.Stage)) { WarnRow(row.Workflow, "stage"); return null; }
            if (!string.IsNullOrEmpty(row.Hold) && hold == FlowHold.None
                && !string.Equals(row.Hold, nameof(FlowHold.None), StringComparison.Ordinal)) { WarnRow(row.Workflow, "hold"); return null; }
            try
            {
                return new LaborWorkflow(row.Workflow, row.Contract, row.Pawn, row.Institution, mode, stage, hold, resume,
                    row.Reason, row.NextAction, row.LastChangedTick, row.NextRetryTick, row.DocumentGeneration,
                    row.DocumentInstance, row.Opportunity, row.CurrentStation);
            }
            catch (Exception error)
            {
                Log.WarningOnce("[PC][LABORFLOW] workflow row rejected (" + row.Workflow + "): " + error.Message, 0x1EC0F02);
                return null;
            }
        }
        public IReadOnlyList<LaborWorkflow> ReadWorkflowsFor(string pawnKey)
        {
            var rows = new List<LaborWorkflow>();
            if (string.IsNullOrEmpty(pawnKey)) return rows.AsReadOnly();
            foreach (var row in WorkflowIndex.Values)
            {
                if (row == null || !string.Equals(row.Pawn, pawnKey, StringComparison.Ordinal)) continue;
                var workflow = ReadWorkflowById(row.Workflow);
                if (workflow != null) rows.Add(workflow);
            }
            return rows.AsReadOnly();
        }
        private static void WarnRow(string id, string field)
            => Log.WarningOnce("[PC][LABORFLOW] row " + id + " has an unrecognized " + field
                + " value; the row is ignored (fail-closed, nothing is guessed).", 0x1EC0F03);

        public bool WriteWorkflow(LaborWorkflow workflow)
        {
            if (!CanWrite || workflow == null) return false;
            if (!WorkflowIndex.TryGetValue(workflow.WorkflowId, out var row))
            {
                if (workflows.Count >= MaximumWorkflows) return false;
                row = new WorkflowRow { Workflow = workflow.WorkflowId };
                workflows.Add(row); workflowIndex[workflow.WorkflowId] = row;
            }
            row.Contract = workflow.ContractId; row.Pawn = workflow.PawnKey; row.Institution = workflow.InstitutionKey;
            row.Opportunity = workflow.OpportunityKey;
            row.Mode = workflow.Mode.ToString(); row.Stage = workflow.Stage.ToString(); row.Hold = workflow.Hold.ToString();
            row.ResumeStage = workflow.ResumeStage.ToString(); row.Reason = workflow.ReasonCode;
            row.NextAction = workflow.NextAction; row.LastChangedTick = workflow.LastChangedTick;
            row.NextRetryTick = workflow.NextRetryTick; row.DocumentGeneration = workflow.DocumentGeneration;
            row.DocumentInstance = workflow.DocumentInstanceId;
            row.CurrentStation = workflow.CurrentStationKey;
            return true;
        }

        /// <summary>只返回不可变快照；历史不会在查询中写回、恢复或结算。</summary>
        public IReadOnlyList<LaborFlowEvent> ReadHistory(string workflowId)
        {
            var result = new List<LaborFlowEvent>();
            if (string.IsNullOrEmpty(workflowId) || !WorkflowIndex.TryGetValue(workflowId, out var workflow))
                return result.AsReadOnly();
            foreach (var row in workflow.History ?? new List<EventRow>())
            {
                if (row == null || string.IsNullOrWhiteSpace(row.Event) || string.IsNullOrWhiteSpace(row.Action)) continue;
                if (!Enum.IsDefined(typeof(LaborFlowStage), row.Stage ?? string.Empty)
                    || !Enum.IsDefined(typeof(FlowHold), row.Hold ?? string.Empty)) continue;
                try
                {
                    var snapshot = new LaborWorkflow(row.Workflow, row.Contract, row.Pawn, row.Institution,
                        SettlementDeliveryMode.PhysicalSubmission, ParseStage(row.Stage, LaborFlowStage.None),
                        ParseHold(row.Hold, FlowHold.None), LaborFlowStage.None, row.Reason, row.NextAction,
                        row.Tick, 0, 0, string.Empty, null, row.Station);
                    result.Add(new LaborFlowEvent(row.Event, snapshot, row.Action, row.Tick, row.Evidence,
                        row.Submission, row.Quantity, row.AmountMinor, row.Result, row.ProductDef));
                }
                catch (ArgumentException) { /* malformed snapshot remains in the save; never guessed */ }
            }
            return result.AsReadOnly();
        }

        public bool HasEvent(string workflowId, string eventId)
        {
            if (string.IsNullOrEmpty(workflowId) || !WorkflowIndex.TryGetValue(workflowId, out var workflow)) return false;
            foreach (var row in workflow.History ?? new List<EventRow>())
                if (row != null && string.Equals(row.Event, eventId, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>仅裁剪展示历史，不参与金融幂等。 retained EventId 重放不会追加第二条。</summary>
        public bool WriteEvent(LaborFlowEvent value)
        {
            if (!CanWrite || value == null || !WorkflowIndex.TryGetValue(value.WorkflowId, out var workflow)) return false;
            if (HasEvent(value.WorkflowId, value.EventId)) return true;
            if (workflow.History == null) workflow.History = new List<EventRow>();
            workflow.History.Add(new EventRow
            {
                Event = value.EventId, Workflow = value.WorkflowId, Contract = value.ContractId, Pawn = value.PawnKey,
                Institution = value.InstitutionKey, Station = value.CurrentStationKey, Stage = value.Stage.ToString(),
                Hold = value.Hold.ToString(), Reason = value.ReasonCode, NextAction = value.NextAction,
                Action = value.ActionCode, Evidence = value.EvidenceId, Submission = value.SubmissionId,
                Result = value.ResultCode, ProductDef = value.ProductDefName, Tick = value.Tick,
                Quantity = value.Quantity, AmountMinor = value.AmountMinor
            });
            if (workflow.History.Count > MaximumHistoryPerWorkflow)
                workflow.History.RemoveAt(0);
            return true;
        }

        /// <summary>
        /// 撤销一行**纯意图**流程（无合同绑定、无提交、无批次）—— 意图在进站签约前失效时可撤销（§4.4）。
        /// 只删"什么都没产生"的行：已绑定合同/已有提交/已有批次的流程一律拒绝删除（债权与过程事实不可丢）。
        /// </summary>
        public bool RemoveIntentWorkflow(string workflowId)
        {
            if (!CanWrite || string.IsNullOrEmpty(workflowId)) return false;
            if (!WorkflowIndex.TryGetValue(workflowId, out var row) || row == null) return true;   // 已不存在 = 目标达成
            // 已绑定合同 ⇒ 拒绝：合同行/应付/批次都以合同为外部键，删流程会让过程与权利脱钩。
            if (!string.IsNullOrEmpty(row.Contract)) return false;
            workflows.Remove(row); workflowIndex = null;
            return true;
        }

        public SubmissionRecord ReadSubmission(string submissionId)
        {
            if (string.IsNullOrEmpty(submissionId) || !SubmissionIndex.TryGetValue(submissionId, out var row) || row == null) return null;
            try
            {
                return new SubmissionRecord(row.Submission, row.Contract, row.Pawn, row.Institution,
                    row.Evidence, row.Batches, row.AcceptedTick, row.AcceptedQuantity, row.Receipt, row.Reconciliation);
            }
            catch (Exception error)
            {
                Log.WarningOnce("[PC][LABORFLOW] submission row rejected (" + row.Submission + "): " + error.Message, 0x1EC0F04);
                return null;
            }
        }
        public IReadOnlyList<SubmissionRecord> ReadSubmissionsFor(string contractId)
        {
            var rows = new List<SubmissionRecord>();
            if (string.IsNullOrEmpty(contractId)) return rows.AsReadOnly();
            foreach (var row in SubmissionIndex.Values)
            {
                if (row == null || !string.Equals(row.Contract, contractId, StringComparison.Ordinal)) continue;
                var record = ReadSubmission(row.Submission);
                if (record != null) rows.Add(record);
            }
            return rows.AsReadOnly();
        }
        /// <summary>该合同最近一次**非对账**提交（付款资格的授予凭据）。</summary>
        public SubmissionRecord ReadLatestSubmissionFor(string contractId)
        {
            SubmissionRecord latest = null;
            var rows = ReadSubmissionsFor(contractId);
            for (int i = 0; i < rows.Count; i++)
            {
                var candidate = rows[i];
                if (candidate == null || candidate.Reconciliation) continue;
                if (latest == null || candidate.AcceptedTick >= latest.AcceptedTick) latest = candidate;
            }
            return latest;
        }
        public bool WriteSubmission(SubmissionRecord record)
        {
            if (!CanWrite || record == null) return false;
            if (!SubmissionIndex.TryGetValue(record.SubmissionId, out var row))
            {
                if (submissions.Count >= MaximumSubmissions) return false;
                row = new SubmissionRow { Submission = record.SubmissionId };
                submissions.Add(row); submissionIndex[record.SubmissionId] = row;
            }
            row.Contract = record.ContractId; row.Pawn = record.PawnKey; row.Institution = record.InstitutionKey;
            row.Evidence = new List<string>(record.EvidenceIds);
            row.Batches = new List<string>(record.BatchIds);
            row.AcceptedTick = record.AcceptedTick; row.AcceptedQuantity = record.AcceptedQuantity;
            row.Receipt = record.ReceiptId; row.Reconciliation = record.Reconciliation;
            return true;
        }

        public ProductBatch ReadBatch(string batchId)
        {
            if (string.IsNullOrEmpty(batchId) || !BatchIndex.TryGetValue(batchId, out var row) || row == null) return null;
            try
            {
                return new ProductBatch(row.Batch, row.Contract, row.Evidence, row.Producer, row.Def, row.Owner,
                    row.Produced, row.Accepted, row.Lost, row.ProducedTick, row.ThingId, row.Holder);
            }
            catch (Exception error)
            {
                // 数量不守恒 / 数量非正 ⇒ 该行不可信：**不猜测**（不重建、不修改）。
                Log.WarningOnce("[PC][LABORFLOW] batch row rejected (" + row.Batch + "): " + error.Message, 0x1EC0F05);
                return null;
            }
        }
        public IReadOnlyList<ProductBatch> ReadBatchesFor(string contractId)
        {
            var rows = new List<ProductBatch>();
            if (string.IsNullOrEmpty(contractId)) return rows.AsReadOnly();
            foreach (var row in BatchIndex.Values)
            {
                if (row == null || !string.Equals(row.Contract, contractId, StringComparison.Ordinal)) continue;
                var batch = ReadBatch(row.Batch);
                if (batch != null) rows.Add(batch);
            }
            return rows.AsReadOnly();
        }
        /// <summary>尚未核销完毕的批次（流程阶段与交付预检用）。</summary>
        public IReadOnlyList<ProductBatch> ReadOpenBatchesFor(string contractId)
        {
            var rows = new List<ProductBatch>();
            var all = ReadBatchesFor(contractId);
            for (int i = 0; i < all.Count; i++) if (all[i] != null && !all[i].IsSettled) rows.Add(all[i]);
            return rows.AsReadOnly();
        }
        public bool WriteBatch(ProductBatch batch)
        {
            if (!CanWrite || batch == null) return false;
            if (!BatchIndex.TryGetValue(batch.BatchId, out var row))
            {
                if (batches.Count >= MaximumBatches) return false;
                row = new BatchRow { Batch = batch.BatchId };
                batches.Add(row); batchIndex[batch.BatchId] = row;
            }
            row.Contract = batch.ContractId; row.Evidence = batch.EvidenceId; row.Producer = batch.ProducerPawnKey;
            row.Def = batch.ThingDefName; row.Owner = batch.OwnerKey; row.Holder = batch.LocationHolderKey;
            row.Produced = batch.ProducedQuantity; row.Accepted = batch.AcceptedQuantity; row.Lost = batch.LostQuantity;
            row.ProducedTick = batch.ProducedTick; row.ThingId = batch.ThingIdNumber;
            return true;
        }

        /// <summary>阶段解析（**fail-closed**：无法识别 ⇒ <paramref name="fallback"/>，由调用方判定可信度）。</summary>
        public static LaborFlowStage ParseStage(string value, LaborFlowStage fallback)
            => !string.IsNullOrEmpty(value) && Enum.IsDefined(typeof(LaborFlowStage), value)
                ? (LaborFlowStage)Enum.Parse(typeof(LaborFlowStage), value) : fallback;
        public static FlowHold ParseHold(string value, FlowHold fallback)
            => !string.IsNullOrEmpty(value) && Enum.IsDefined(typeof(FlowHold), value)
                ? (FlowHold)Enum.Parse(typeof(FlowHold), value) : fallback;
    }
}

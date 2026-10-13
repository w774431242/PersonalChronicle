using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Domain.PawnEconomy.Employment
{
    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**付款模式** —— 与合同状态正交，签约时**冻结**并随合同行持久。
    ///
    /// <para><b>为什么不复用 <see cref="ContractState"/></b>：需求 §4.1 明禁在既有枚举中插入/重排数值
    /// （旧档 1/2/3/4 的语义永不改写，SAVE-003）。模式是**另一条正交轴**：同一份 Active 合同既可能是
    /// 旧式"完成即付"，也可能是新式"到站交验后才付"。</para>
    ///
    /// <para><b>存 string 而非枚举数值</b>：合同行以文本持久（既有 <c>ParseState</c> 同式）。
    /// 数值化会让"未来追加枚举项"与"旧档数值"发生静默错配。</para>
    /// </summary>
    internal enum SettlementDeliveryMode
    {
        /// <summary>**未知/非法**：缺键之外的任何无法识别字符串。行级 fail-closed —— 既不按新模式签，
        /// 也不按旧模式即时付款；写入被阻断并进入对账（§9.1「未知值阻断而非默认为已付」）。</summary>
        Unknown = 0,
        /// <summary>旧档缺字段的自然语义：完成证据成立即付款（既有权利分毫不变）。</summary>
        LegacyImmediate = 1,
        /// <summary>新签合同的模式：完成证据只建立应付（"已赚取/待交验"），到工资结算处提交并
        /// 通过核验后才放行付款。</summary>
        PhysicalSubmission = 2
    }

    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**办理流程阶段**（地图过程轴，与业务权利轴正交）。
    /// 顺序即玩家可见的推进顺序；<see cref="LaborFlowRules.IsStageForward"/> 是唯一合法性判据。
    /// </summary>
    internal enum LaborFlowStage
    {
        /// <summary>无在办流程（尚未形成意图 / 已归档）。</summary>
        None = 0,
        /// <summary>计划已选中合法机会，尚未出发签约。</summary>
        IntentReady = 1,
        /// <summary>办理 Job 已启动并真实寻路中。</summary>
        GoingToLaborOffice = 2,
        /// <summary>柜台排队（短时预约；不等于已承接、不等于资金已预留）。</summary>
        QueuedAtLaborOffice = 3,
        /// <summary>到站洽谈中（停留/朝向/意向气泡），尚无签约成功文案。</summary>
        Negotiating = 4,
        /// <summary>签约已原子提交，正在签发/交付实体合同纸。</summary>
        IssuingDocument = 5,
        /// <summary>持约履约；仅真实 Job 关联时称"正在工作"。</summary>
        Working = 6,
        /// <summary>收集指定真实产物（显示已收/待收）。</summary>
        CollectingOutputs = 7,
        /// <summary>持合同/货物走向工资结算处。</summary>
        GoingToSettlement = 8,
        /// <summary>到站核验证件、证据、来源、数量与接收能力。</summary>
        VerifyingSubmission = 9,
        /// <summary>已交付待发薪（资金暂缺或账本不可用）；Pawn 可离开。</summary>
        AwaitingPayment = 10,
        /// <summary>账本 Finalized/Replay 确认且应付已标记 ⇒ 一次到账反馈 + 合同盖章。</summary>
        PaymentConfirmed = 11,
        /// <summary>无继续工作、待交付、未付或未对账节点 ⇒ 可归档查看。</summary>
        Archived = 12
    }

    /// <summary>叠加状态：与阶段并存，**必须**同时保存 <c>ResumeStage / ReasonCode / NextAction</c>。</summary>
    internal enum FlowHold
    {
        None = 0,
        /// <summary>征召/战斗/基本需求/医疗等**可恢复**的中断（不抹掉签约、批次或债务）。</summary>
        Paused = 1,
        /// <summary>目标不可达/站点拆除/接收满等**条件性**阻断（有明确 NextAction）。</summary>
        Blocked = 2,
        /// <summary>无法证明物理事实是否发生（物资已转移但回执未写、未知 schema、代次冲突）——
        /// 进入后不得猜测成功或失败（§5.1）。</summary>
        ReconciliationRequired = 3
    }

    /// <summary>
    /// 人工对账处置决定（CA-10：把「对账中」从**死状态**变成有真实入口的可恢复状态）。
    /// 只允许两种动作 —— 二者都要求人工先核验物理事实（现场/日志/容器），服务层**不猜**。
    /// </summary>
    public enum ReconciliationAction
    {
        /// <summary>事实已查清（货在柜台/在身上/批次已结清）⇒ 清 Hold，按剩余量继续办理。</summary>
        Resume,
        /// <summary>剩余真实产物**经核验已无法交付**（损毁/丢失/被吞）⇒ 按损失核销剩余批次，随后按覆盖完成放行资格。</summary>
        WriteOffRemaining
    }

    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**交付能力**（每工种声明，禁按产品标签推测）。首版实机最低要求
    /// 覆盖伐木、开采/收获、无实物劳务；其余族同样必须有声明（不得静默漏网）。
    /// </summary>
    internal enum DeliveryCapability
    {
        /// <summary>尚不能安全交验：**不允许**新签实体模式合同（原版工作照常，旧已赚权利不删）。</summary>
        Unsupported = 0,
        /// <summary>真实产物由 Pawn 搬运到柜台交付（carryTracker 或多趟）。</summary>
        CarryToCounter = 1,
        /// <summary>原版生产/物流目的地已验收入库，凭权威回执交验（**不搬回工资站重演**）。</summary>
        AlreadyAccepted = 2,
        /// <summary>无实物：合同 + 成功完成证明（明确"无需实物产品"）。</summary>
        EvidenceOnly = 3
    }

    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**工资付款资格** —— 在应付账户创建时**冻结**并随 <c>ObligationRow</c> 持久。
    ///
    /// <para><b>为什么必须冻结在应付上而不是查询时重算</b>：旧档应付没有提交记录、没有实体纸、没有流程行；
    /// 若付款资格按"当前是否存在提交/纸/流程"实时判定，**旧欠薪会被整体堵死**（DATA+SAVE 席 I2 反例）。
    /// 资格一旦写定即不可由窗口、设置、地图状态重算。</para>
    /// </summary>
    internal enum WageEligibility
    {
        /// <summary>未知/非法（fail-closed：不付款、不删除、进对账）。</summary>
        Unknown = 0,
        /// <summary>旧模式：创建即可付（既有 <c>TrySettlePayable</c> 语义原样保留）。</summary>
        Immediate = 1,
        /// <summary>新模式：等待到工资结算处提交核验。</summary>
        PendingSubmission = 2,
        /// <summary>已提交且核验通过（接收回执已写）⇒ 可付款。</summary>
        Submitted = 3,
        /// <summary>凭据/代次/来源校验失败 ⇒ 阻断付款并给出具体原因（不得静默降级为可付）。</summary>
        Blocked = 4
    }

    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**办理流程**聚合（一个 Pawn 一份在办流程；合同内可有多个待交付批次）。
    /// 纯值对象：不含 Pawn/Thing/Map 引用（那些只在 Economy 适配层短暂出现）。
    /// </summary>
    internal sealed class LaborWorkflow
    {
        public string WorkflowId { get; }
        /// <summary>权威合同 ID。**意图阶段为空串** —— 此时尚无合同（合同在到站 SignCommit 才产生/承接），
        /// 业务身份由 <see cref="OpportunityKey"/> 承载（§4.2 IntentReady：「尚未签约」）。</summary>
        public string ContractId { get; }
        /// <summary>办理机会键（`vanilla:{workType}:{anchor}` 等）—— 意图阶段与签约后的稳定业务身份。</summary>
        public string OpportunityKey { get; }
        public string PawnKey { get; }
        public string InstitutionKey { get; }
        /// <summary>当前实际办理柜台；空表示尚未选定/已离柜。签约机构身份不随工资柜台改变。</summary>
        public string CurrentStationKey { get; }
        public SettlementDeliveryMode Mode { get; }
        public LaborFlowStage Stage { get; }
        public FlowHold Hold { get; }
        public LaborFlowStage ResumeStage { get; }
        /// <summary>稳定 reason ID（呈现层翻译，GOV-009）——不存本地化文案。</summary>
        public string ReasonCode { get; }
        /// <summary>稳定 next-action ID（"去哪/做什么"的可执行提示）。</summary>
        public string NextAction { get; }
        public int LastChangedTick { get; }
        public int NextRetryTick { get; }
        /// <summary>**有效代次**：补发递增，旧纸失效（债权不随纸转手）。</summary>
        public int DocumentGeneration { get; }
        /// <summary>当前有效合同纸实例 ID（空 = 尚未签发或已失效）。</summary>
        public string DocumentInstanceId { get; }

        public LaborWorkflow(string workflowId, string contractId, string pawnKey, string institutionKey,
            SettlementDeliveryMode mode, LaborFlowStage stage, FlowHold hold, LaborFlowStage resumeStage,
            string reasonCode, string nextAction, int lastChangedTick, int nextRetryTick,
            int documentGeneration, string documentInstanceId, string opportunityKey = null, string currentStationKey = null)
        {
            if (!Enum.IsDefined(typeof(SettlementDeliveryMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (!Enum.IsDefined(typeof(LaborFlowStage), stage)) throw new ArgumentOutOfRangeException(nameof(stage));
            if (!Enum.IsDefined(typeof(LaborFlowStage), resumeStage)) throw new ArgumentOutOfRangeException(nameof(resumeStage));
            if (!Enum.IsDefined(typeof(FlowHold), hold)) throw new ArgumentOutOfRangeException(nameof(hold));
            if (documentGeneration < 0) throw new ArgumentOutOfRangeException(nameof(documentGeneration));
            WorkflowId = MinorCurrency.Required(workflowId);
            ContractId = contractId ?? string.Empty;   // 意图阶段无合同（见属性注释）
            OpportunityKey = opportunityKey ?? string.Empty;
            PawnKey = MinorCurrency.Required(pawnKey);
            InstitutionKey = MinorCurrency.Required(institutionKey);
            CurrentStationKey = currentStationKey ?? string.Empty;
            Mode = mode; Stage = stage; Hold = hold; ResumeStage = resumeStage;
            ReasonCode = reasonCode ?? string.Empty; NextAction = nextAction ?? string.Empty;
            LastChangedTick = lastChangedTick; NextRetryTick = nextRetryTick;
            DocumentGeneration = documentGeneration; DocumentInstanceId = documentInstanceId ?? string.Empty;
        }

        /// <summary>唯一流程 ID：`flow:{PawnKey}|{OpportunityKey}`。**确定性派生**（禁 Guid）—— 重放/读档
        /// 必须命中同一行。用"人 + 机会"而非合同：意图阶段**尚无合同**（合同在到站 SignCommit 才产生），
        /// 而流程必须在签约前后是**同一行**（否则到站承接会丢失"我来办这份工作"的上下文）。</summary>
        public static string IdFor(string pawnKey, string opportunityKey)
            => "flow:" + (pawnKey ?? string.Empty) + "|" + (opportunityKey ?? string.Empty);

        /// <summary>轮次上限（有界：同一人同一机会不得无限开轮；到顶后如实阻断并留给人工处置）。</summary>
        public const int MaximumRounds = 64;

        /// <summary>
        /// 第 <paramref name="round"/> 轮的流程 ID（CA-09 修复：归档后**开新轮**，不复用也不重置旧行）。
        ///
        /// <para><b>轮 1 必须沿用旧格式</b>（`flow:{Pawn}|{Opp}`，即 <see cref="IdFor(string,string)"/>）——
        /// 旧档行、旧合同、旧回执都以它为外部键；显式写 `|r1` 会让同一逻辑轮出现两个 ID。
        /// 后缀 `|r{n}`（n ≥ 2）是**权威轮号**（见 <see cref="RoundOf"/>），不额外落盘字段，
        /// 避免"ID 后缀与字段不一致"的第二份真相源。</para>
        /// </summary>
        public static string IdFor(string pawnKey, string opportunityKey, int round)
            => round <= 1 ? IdFor(pawnKey, opportunityKey) : IdFor(pawnKey, opportunityKey) + "|r" + round;

        /// <summary>
        /// 从流程 ID 解析轮次（**后缀即权威**）：无后缀 ⇒ 1；`|r{n}`（n ≥ 2）⇒ n；
        /// 形态非法（`|r1` / `|rx` / 空数字 / 超上限）⇒ 0 = **不可信**（调用方 fail-closed，不猜轮号）。
        /// </summary>
        public static int RoundOf(string workflowId)
        {
            if (string.IsNullOrEmpty(workflowId)) return 0;
            int cut = workflowId.LastIndexOf("|r", StringComparison.Ordinal);
            if (cut < 0) return 1;
            string tail = workflowId.Substring(cut + 2);
            if (tail.Length == 0) return 0;
            for (int i = 0; i < tail.Length; i++) if (tail[i] < '0' || tail[i] > '9') return 0;
            int round;
            if (!int.TryParse(tail, out round)) return 0;
            if (round <= 1 || round > MaximumRounds) return 0;   // `|r1` 是非法形态（轮 1 无后缀）
            return round;
        }

        /// <summary>阶段推进（保留 Hold/Resume/代次/回执身份）。</summary>
        public LaborWorkflow MovedTo(LaborFlowStage stage, string nextAction, int now)
            => new LaborWorkflow(WorkflowId, ContractId, PawnKey, InstitutionKey, Mode, stage, Hold,
                stage == LaborFlowStage.Archived ? LaborFlowStage.None : ResumeStage,
                ReasonCode, nextAction, now, NextRetryTick, DocumentGeneration, DocumentInstanceId, OpportunityKey, CurrentStationKey);

        /// <summary>进入/退出叠加状态：退出时回到 <paramref name="resume"/>（显式给出，避免猜）。</summary>
        public LaborWorkflow HeldAt(FlowHold hold, string reasonCode, string nextAction,
            LaborFlowStage resume, int now, int nextRetryTick)
            => new LaborWorkflow(WorkflowId, ContractId, PawnKey, InstitutionKey, Mode, Stage, hold, resume,
                reasonCode, nextAction, now, nextRetryTick, DocumentGeneration, DocumentInstanceId, OpportunityKey, CurrentStationKey);

        /// <summary>签发/补发：代次递增并换发新实例 ID（旧代次纸随之作废）。</summary>
        public LaborWorkflow WithDocument(int generation, string instanceId, int now)
            => new LaborWorkflow(WorkflowId, ContractId, PawnKey, InstitutionKey, Mode, Stage, Hold, ResumeStage,
                ReasonCode, NextAction, now, NextRetryTick, generation, instanceId, OpportunityKey, CurrentStationKey);

        /// <summary>SignCommit 成功：把权威合同身份与冻结模式写入**同一行**（合同身份一变即不可再倒退）。</summary>
        public LaborWorkflow WithContract(string contractId, SettlementDeliveryMode mode, int now)
            => new LaborWorkflow(WorkflowId, contractId, PawnKey, InstitutionKey, mode, Stage, Hold, ResumeStage,
                ReasonCode, NextAction, now, NextRetryTick, DocumentGeneration, DocumentInstanceId, OpportunityKey, CurrentStationKey);

        public LaborWorkflow AtStation(string stationKey, int now)
            => new LaborWorkflow(WorkflowId, ContractId, PawnKey, InstitutionKey, Mode, Stage, Hold, ResumeStage,
                ReasonCode, NextAction, now, NextRetryTick, DocumentGeneration, DocumentInstanceId, OpportunityKey, stationKey);
    }

    /// <summary>只读的玩家履约事件。稳定业务 ID 去重；不会授予提交/付款资格，也不是会计凭证。</summary>
    internal sealed class LaborFlowEvent
    {
        public string EventId { get; }
        public string WorkflowId { get; }
        public string ContractId { get; }
        public string PawnKey { get; }
        public string InstitutionKey { get; }
        public string CurrentStationKey { get; }
        public int Tick { get; }
        public LaborFlowStage Stage { get; }
        public FlowHold Hold { get; }
        public string ReasonCode { get; }
        public string NextAction { get; }
        public string ActionCode { get; }
        public string EvidenceId { get; }
        public string SubmissionId { get; }
        public int Quantity { get; }
        public long AmountMinor { get; }
        public string ResultCode { get; }
        public string ProductDefName { get; }

        public LaborFlowEvent(string eventId, LaborWorkflow workflow, string actionCode, int tick,
            string evidenceId = null, string submissionId = null, int quantity = 0, long amountMinor = 0,
            string resultCode = null, string productDefName = null)
        {
            if (workflow == null) throw new ArgumentNullException(nameof(workflow));
            if (quantity < 0 || amountMinor < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            EventId = MinorCurrency.Required(eventId); ActionCode = MinorCurrency.Required(actionCode);
            WorkflowId = workflow.WorkflowId; ContractId = workflow.ContractId; PawnKey = workflow.PawnKey;
            InstitutionKey = workflow.InstitutionKey; CurrentStationKey = workflow.CurrentStationKey;
            Tick = tick; Stage = workflow.Stage; Hold = workflow.Hold;
            ReasonCode = workflow.ReasonCode; NextAction = workflow.NextAction;
            EvidenceId = evidenceId ?? string.Empty; SubmissionId = submissionId ?? string.Empty;
            Quantity = quantity; AmountMinor = amountMinor; ResultCode = resultCode ?? string.Empty;
            ProductDefName = productDefName ?? string.Empty;
        }
    }

    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**一次交验提交**（到工资结算处呈递合同与交付物的结果事实）。
    /// 稳定 ID 确定性派生：`submission:{ContractId}|{Generation}|{EvidenceKey}`（禁 Guid，重放必须同键）。
    /// </summary>
    internal sealed class SubmissionRecord
    {
        public string SubmissionId { get; }
        public string ContractId { get; }
        public string PawnKey { get; }
        public string InstitutionKey { get; }
        /// <summary>被本次提交核销的证据集合（稳定 EvidenceId，逗号分隔的确定序）。</summary>
        public IReadOnlyList<string> EvidenceIds { get; }
        /// <summary>被本次提交接收的批次集合。</summary>
        public IReadOnlyList<string> BatchIds { get; }
        public int AcceptedTick { get; }
        /// <summary>接收到的实物总件数（0 = 无产品劳务，凭据型）。</summary>
        public int AcceptedQuantity { get; }
        /// <summary>接收回执 ID（权威入库凭据；空 = 无产品型）。</summary>
        public string ReceiptId { get; }
        /// <summary>待对账（物理事实不确定）—— 不删、不猜、不给付款资格。</summary>
        public bool Reconciliation { get; }

        public SubmissionRecord(string submissionId, string contractId, string pawnKey, string institutionKey,
            IEnumerable<string> evidenceIds, IEnumerable<string> batchIds, int acceptedTick, int acceptedQuantity,
            string receiptId, bool reconciliation = false)
        {
            if (acceptedQuantity < 0) throw new ArgumentOutOfRangeException(nameof(acceptedQuantity));
            SubmissionId = MinorCurrency.Required(submissionId);
            ContractId = MinorCurrency.Required(contractId);
            PawnKey = MinorCurrency.Required(pawnKey);
            InstitutionKey = MinorCurrency.Required(institutionKey);
            EvidenceIds = Freeze(evidenceIds);
            BatchIds = Freeze(batchIds);
            AcceptedTick = acceptedTick; AcceptedQuantity = acceptedQuantity;
            ReceiptId = receiptId ?? string.Empty;
            Reconciliation = reconciliation;
        }

        private static IReadOnlyList<string> Freeze(IEnumerable<string> values)
        {
            var rows = new List<string>();
            foreach (string value in values ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(value)) rows.Add(value);
            return PerceptionCollections.Freeze(rows);
        }

        /// <summary>确定性派生（同合同同代次同证据 ⇒ 同回执 ⇒ 幂等，重放不产生第二次接收）。
        /// 这是**旧档格式**（v1 及"一趟交完"）；多趟交验用带累计量的重载。</summary>
        public static string IdFor(string contractId, int generation, string evidenceKey)
            => "submission:" + contractId + "|g" + generation + "|" + evidenceKey;

        /// <summary>
        /// 多趟交验的回执 ID：`…|a{累计接收量}`。累计量**由持久化台账派生**（不是内存计数器）⇒
        /// 同一物理趟重放算出同键（Replay，不重复核销），不同趟的累计量严格递增（不吞前一趟的核销）。
        /// <paramref name="coveredTotal"/> ≤ 0 ⇒ 不写回执（调用方必须早退），故回落旧格式。
        /// </summary>
        public static string IdFor(string contractId, int generation, string evidenceKey, int coveredTotal)
            => coveredTotal <= 0 ? IdFor(contractId, generation, evidenceKey)
             : IdFor(contractId, generation, evidenceKey) + "|a" + coveredTotal;
    }

    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**真实产出批次**（来源明细）。
    ///
    /// <para><b>为什么必须是稳定批次而不是 Thing 引用拼 ID</b>：Thing 是可变化的物理位置
    /// （搬运/合堆/拆分/销毁都会改变或消灭它），而工资身份必须可重放。因此批次以
    /// <c>BatchId</c> 为身份，Thing 只作为**可失效的物理线索**（<see cref="ThingIdNumber"/> /
    /// <see cref="LocationHolderKey"/>）。</para>
    ///
    /// <para><b>数量守恒不变量</b>：<c>Produced = Remaining + Accepted + Lost</c>。
    /// 三项都不经钳制，任何不守恒的写入都是缺陷而非容错。</para>
    /// </summary>
    internal sealed class ProductBatch
    {
        public string BatchId { get; }
        public string ContractId { get; }
        public string EvidenceId { get; }
        public string ProducerPawnKey { get; }
        public string ThingDefName { get; }
        /// <summary>雇主权属键（产品**不是**劳动者私产；代持不等于所有权）。</summary>
        public string OwnerKey { get; }
        public int ProducedQuantity { get; }
        public int AcceptedQuantity { get; }
        public int LostQuantity { get; }
        public int ProducedTick { get; }
        /// <summary>物理线索：原版 <c>Thing.thingIDNumber</c>（跨存档不稳定，只作定位辅助，不作身份）。</summary>
        public int ThingIdNumber { get; }
        /// <summary>位置/holder 快照键（可失效：搬运/销毁后不再命中）。</summary>
        public string LocationHolderKey { get; }

        public ProductBatch(string batchId, string contractId, string evidenceId, string producerPawnKey,
            string thingDefName, string ownerKey, int producedQuantity, int acceptedQuantity, int lostQuantity,
            int producedTick, int thingIdNumber, string locationHolderKey)
        {
            if (producedQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(producedQuantity));
            if (acceptedQuantity < 0 || lostQuantity < 0 || acceptedQuantity + lostQuantity > producedQuantity)
                throw new ArgumentOutOfRangeException(nameof(acceptedQuantity));
            BatchId = MinorCurrency.Required(batchId);
            ContractId = MinorCurrency.Required(contractId);
            EvidenceId = MinorCurrency.Required(evidenceId);
            ProducerPawnKey = MinorCurrency.Required(producerPawnKey);
            ThingDefName = MinorCurrency.Required(thingDefName);
            OwnerKey = ownerKey ?? string.Empty;
            ProducedQuantity = producedQuantity; AcceptedQuantity = acceptedQuantity; LostQuantity = lostQuantity;
            ProducedTick = producedTick; ThingIdNumber = thingIdNumber;
            LocationHolderKey = locationHolderKey ?? string.Empty;
        }

        /// <summary>未接收量（守恒式中的剩余项）。</summary>
        public int RemainingQuantity => ProducedQuantity - AcceptedQuantity - LostQuantity;
        /// <summary>该批次是否已核销完毕（全部接收或确认损失）。</summary>
        public bool IsSettled => RemainingQuantity <= 0;

        /// <summary>确定性派生：`batch:{ContractId}|{EvidenceId}|{ThingDefName}`（同一证据同一产物的多趟共用批次）。</summary>
        public static string IdFor(string contractId, string evidenceId, string thingDefName)
            => "batch:" + contractId + "|" + evidenceId + "|" + thingDefName;

        /// <summary>接收：只接受 0 &lt; quantity ≤ 剩余量；调用方在同一提交里写回，不得分两次（防半提交）。</summary>
        public ProductBatch Receive(int quantity)
        {
            if (quantity <= 0 || quantity > RemainingQuantity) throw new ArgumentOutOfRangeException(nameof(quantity));
            return new ProductBatch(BatchId, ContractId, EvidenceId, ProducerPawnKey, ThingDefName, OwnerKey,
                ProducedQuantity, AcceptedQuantity + quantity, LostQuantity, ProducedTick, ThingIdNumber, LocationHolderKey);
        }

        /// <summary>可信损失核销（只解除交货资格，不增工资、不追回已付）。</summary>
        public ProductBatch Wrote(bool lost, int quantity)
        {
            if (quantity <= 0 || quantity > RemainingQuantity) throw new ArgumentOutOfRangeException(nameof(quantity));
            return lost
                ? new ProductBatch(BatchId, ContractId, EvidenceId, ProducerPawnKey, ThingDefName, OwnerKey,
                    ProducedQuantity, AcceptedQuantity, LostQuantity + quantity, ProducedTick, ThingIdNumber, LocationHolderKey)
                : Receive(quantity);
        }
    }

    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**纯粹规则**（离线可断言）—— 阶段合法性、付款资格门、能力矩阵、
    /// 存储解析（fail-closed）、确定性 ID 派生。不引 Verse/Unity/RUIF（ARC）。
    /// </summary>
    internal static class LaborFlowRules
    {
        /// <summary>设计默认（来源：CB-ECON-003 需求单 v1.0 §4.4）。计时只表示服务表现，成功仍由命令结果判定。</summary>
        public const int NegotiateTicks = 90, IssueTicks = 30, VerifyTicks = 90, PaymentFeedbackTicks = 30;
        // Vanilla ordinary work is 9 during Work, 5.5 during Anything; food is 9.5.
        public static float FulfillmentPriority(bool safe, bool pendingSubmission, bool workAssignment)
            => !safe || !pendingSubmission ? 0f : workAssignment ? 9.1f : 6f;
        /// <summary>柜台预约 TTL（tick）；有效办理可续期。</summary>
        public const int ReservationTtlTicks = 600;
        /// <summary>失败退避序列（tick），末项为上限。</summary>
        public static readonly int[] RetryBackoffTicks = { 250, 500, 1000, 2000 };
        /// <summary>表现层气泡寿命建议（**真实秒**，与金融状态无关）。</summary>
        public const float BubbleLifetimeSeconds = 4.5f;

        /// <summary>
        /// 从 <paramref name="from"/> **逐阶补记**到 <paramref name="to"/> 所需的阶段序列（不含起点、含终点）。
        /// 已到达或目标不在前方 ⇒ 空序列。
        ///
        /// <para>用途：真实 Job 会**合法地跨过**多条过程性阶段（例：无产品劳务从"持约履约"直接到结算处，
        /// 没有"收集产物"）。正确处理是逐阶补记（每一步都对应一个真实已发生的事实），而不是放宽
        /// <see cref="IsStageForward"/>（那会放行"远程假流程"）、也不是让流程永久停滞。</para>
        /// </summary>
        public static IReadOnlyList<LaborFlowStage> PathTo(LaborFlowStage from, LaborFlowStage to)
        {
            var steps = new List<LaborFlowStage>(4);
            if (from == LaborFlowStage.Archived) return steps;   // 终态不可再前进
            if ((int)to <= (int)from) return steps;
            for (int stage = (int)from + 1; stage <= (int)to; stage++) steps.Add((LaborFlowStage)stage);
            return steps;
        }

        /// <summary>合法阶段推进：同阶（重放）或严格前进一步。**禁止跳阶**（远程一键跳到 IssuingDocument 即"假流程"）。</summary>
        public static bool IsStageForward(LaborFlowStage from, LaborFlowStage to)
        {
            if (from == to) return true;
            if (from == LaborFlowStage.Archived) return false;          // 归档是终态
            if (to == LaborFlowStage.Archived) return true;             // 任何阶段可归档（前提由调用方判权利结清）
            return (int)to == (int)from + 1;
        }

        /// <summary>签约阶段仍逐阶。签约后当前动作可循环，金融权利与归档条件仍由各自服务校验。</summary>
        public static bool IsStageTransition(LaborFlowStage from, LaborFlowStage to, bool hasContract)
        {
            if (!Enum.IsDefined(typeof(LaborFlowStage), from) || !Enum.IsDefined(typeof(LaborFlowStage), to)) return false;
            if (IsStageForward(from, to)) return true;
            // 真实签约 Job 被征召/需求打断后重新出发，并没有撤销意图或签约成功。
            // 只允许恢复柜台前动作，不放宽 IssuingDocument/Working 的签约前门。
            if (!hasContract && from >= LaborFlowStage.GoingToLaborOffice && from <= LaborFlowStage.Negotiating
                && to >= LaborFlowStage.GoingToLaborOffice && to <= LaborFlowStage.Negotiating)
                return to <= from;
            if (!hasContract || from == LaborFlowStage.Archived) return false;
            bool actionTarget = to >= LaborFlowStage.Working && to <= LaborFlowStage.VerifyingSubmission;
            bool ongoingSource = from >= LaborFlowStage.Working && from <= LaborFlowStage.PaymentConfirmed;
            return actionTarget && ongoingSource;
        }

        /// <summary>
        /// 付款门（**唯一实现**）：该应付当前是否允许发起付款。
        /// <list type="bullet">
        /// <item><see cref="WageEligibility.Immediate"/> ⇒ 允许（旧模式，与既有行为逐字等价）。</item>
        /// <item><see cref="WageEligibility.Submitted"/> ⇒ 允许（新模式已提交核验）。</item>
        /// <item><see cref="WageEligibility.PendingSubmission"/> ⇒ 拒绝（等待交验；**不是**失败）。</item>
        /// <item><see cref="WageEligibility.Unknown"/> / <see cref="WageEligibility.Blocked"/> ⇒ 拒绝（fail-closed）。</item>
        /// </list>
        /// </summary>
        public static bool PaymentAllowed(WageEligibility eligibility)
            => eligibility == WageEligibility.Immediate || eligibility == WageEligibility.Submitted;

        /// <summary>新模式记录证据时必须给出的初始资格（旧模式给 <see cref="WageEligibility.Immediate"/>）。</summary>
        public static WageEligibility EligibilityOnEvidence(SettlementDeliveryMode mode)
            => mode == SettlementDeliveryMode.PhysicalSubmission ? WageEligibility.PendingSubmission
             : mode == SettlementDeliveryMode.LegacyImmediate ? WageEligibility.Immediate
             : WageEligibility.Unknown;

        /// <summary>
        /// 存储解析（**fail-closed**）：可识别字符串 ⇒ 对应值；空（旧档缺键）⇒ <paramref name="whenAbsent"/>
        /// （合同行的自然缺省 = <see cref="SettlementDeliveryMode.LegacyImmediate"/>）；
        /// **任何其它字符串**（拼写错误 / 未来版本写回 / 外部编辑）⇒ <see cref="SettlementDeliveryMode.Unknown"/>
        /// —— 绝不回落成"可付款"（DATA+SAVE 席：禁复用 <c>ParseState</c> 的宽松 fallback）。
        /// </summary>
        public static SettlementDeliveryMode ParseMode(string value, SettlementDeliveryMode whenAbsent)
        {
            if (string.IsNullOrWhiteSpace(value)) return whenAbsent;
            if (string.Equals(value, nameof(SettlementDeliveryMode.LegacyImmediate), StringComparison.Ordinal))
                return SettlementDeliveryMode.LegacyImmediate;
            if (string.Equals(value, nameof(SettlementDeliveryMode.PhysicalSubmission), StringComparison.Ordinal))
                return SettlementDeliveryMode.PhysicalSubmission;
            return SettlementDeliveryMode.Unknown;
        }

        /// <summary>资格存储解析（fail-closed：无法识别 ⇒ <see cref="WageEligibility.Unknown"/>，不付款）。</summary>
        public static WageEligibility ParseEligibility(string value, WageEligibility whenAbsent)
        {
            if (string.IsNullOrWhiteSpace(value)) return whenAbsent;
            if (string.Equals(value, nameof(WageEligibility.Immediate), StringComparison.Ordinal))
                return WageEligibility.Immediate;
            if (string.Equals(value, nameof(WageEligibility.PendingSubmission), StringComparison.Ordinal))
                return WageEligibility.PendingSubmission;
            if (string.Equals(value, nameof(WageEligibility.Submitted), StringComparison.Ordinal))
                return WageEligibility.Submitted;
            if (string.Equals(value, nameof(WageEligibility.Blocked), StringComparison.Ordinal))
                return WageEligibility.Blocked;
            return WageEligibility.Unknown;
        }

        /// <summary>
        /// **交付能力矩阵**（唯一来源）。按订单键声明；未列出的键 ⇒ <see cref="DeliveryCapability.Unsupported"/>
        /// （fail-closed：绝不允许新签一份无法履行交付的实体模式合同）。
        /// 口径与 <c>VanillaOrderCatalogue</c> 的八族对齐；第三方追加族经 Compat 适配层登记后同样在此声明。
        /// </summary>
        public static DeliveryCapability CapabilityFor(string orderKey)
        {
            switch (orderKey)
            {
                case "ChopWood": return DeliveryCapability.CarryToCounter;      // 真实生成的木材搬柜台
                case "Mine": return DeliveryCapability.CarryToCounter;          // 真实矿物到柜台
                case "Harvest": return DeliveryCapability.CarryToCounter;       // 真实收获物到柜台
                case "CutPlants": return DeliveryCapability.EvidenceOnly;       // 无产物 ⇒ 合同 + 完成证明
                // ⚠ CA-03（2026-10-12 返修）：Hunt / Slaughter 原标 AlreadyAccepted，但**没有任何权威
                // 接收回执来源**（原版死亡过程不存在 CaptureWired 的"雇主已接收"证据），交验路径于是直接
                // 拼一个 `accepted:…` 字符串当凭据 = 凭空造出"已收货"（管理器 P0 反例）。
                // 按需求 §6.1「没有 CaptureWired 成功证据的能力不得承诺实体模式可结算」，这里**如实降级**为
                // Unsupported：不再新签这两族实体合同（原版工作照常、旧已赚权利不受影响），
                // 待真实回执来源落地后再放行。
                case "Hunt": return DeliveryCapability.Unsupported;
                case "Slaughter": return DeliveryCapability.Unsupported;
                // CHG-20261012-ECON-HAULCMD（CA-03 **部分撤销**）：Haul 的权威接收回执来源已落地 ——
                // Capture/Patch_VanillaHaulWork 的四段补丁在真实放置回调里校验储存区
                // SlotGroup.Settings.AllowedToAccept（容器按实物减少量），经 ReportVanillaOrderCompletion 入账
                // ⇒ 凭据由**真实交付**产生，不需要任何伪造的 accepted:… 字符串；
                // 且物品已真实进入合格储存区，不应再让 Pawn 把它搬到柜台（CarryToCounter 是错的）
                // ⇒ 按 EvidenceOnly 放行（无实物交验、完成证据即凭据）。
                // 注：Haul 可被玩家下达的范围放宽见新增补丁 Capture/Patch_HaulDesignator.cs（新 Harmony）。
                case "Haul": return DeliveryCapability.EvidenceOnly;
                case "Tame": return DeliveryCapability.EvidenceOnly;            // 成功驯服证明（完成证据即凭据）
                default: return DeliveryCapability.Unsupported;
            }
        }

        /// <summary>该能力是否需要实体产物交验（用于流程阶段选择与 UI 提示）。</summary>
        public static bool RequiresPhysicalDelivery(DeliveryCapability capability)
            => capability == DeliveryCapability.CarryToCounter;

        /// <summary>该能力是否可在无实物情况下成立（明确告知玩家"无需实物产品"）。</summary>
        public static bool AllowsPaperlessDelivery(DeliveryCapability capability)
            => capability == DeliveryCapability.EvidenceOnly || capability == DeliveryCapability.AlreadyAccepted;

        /// <summary>退避：第 <paramref name="failures"/> 次失败后的等待 tick（末项封顶）。</summary>
        public static int BackoffTicks(int failures)
        {
            if (failures <= 0) return 0;
            int index = failures - 1;
            if (index >= RetryBackoffTicks.Length) index = RetryBackoffTicks.Length - 1;
            return RetryBackoffTicks[index];
        }

        /// <summary>合同纸实例 ID（确定性派生：同合同同代次 ⇒ 同实例，重放不产生第二张纸）。</summary>
        public static string DocumentInstanceIdFor(string contractId, int generation)
            => "contractdoc:" + contractId + "|g" + generation;

        /// <summary>合同短号（玩家可读；主界面只显示短号，完整 ID 在诊断详情可复制）。</summary>
        public static string ShortCode(string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return string.Empty;
            int cut = contractId.LastIndexOf('|');
            string tail = cut >= 0 && cut + 1 < contractId.Length ? contractId.Substring(cut + 1) : contractId;
            return tail.Length <= 8 ? tail : tail.Substring(0, 8);
        }
    }
}

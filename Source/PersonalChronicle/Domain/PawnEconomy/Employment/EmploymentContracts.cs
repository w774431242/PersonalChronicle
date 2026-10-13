using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Domain.PawnEconomy.Employment
{
    internal enum ContractState { Active = 1, Completed = 2, Terminated = 3,
        /// <summary>已派发（待承接）：玩家经「工作合同弹窗」拟定的合同，尚无持约人；殖民者承接时转 <see cref="Active"/>。
        /// 数值 4 追加在末尾 —— 旧档的 1/2/3 语义与数字**永不改写**（SAVE-003）。</summary>
        Posted = 4 }
    internal enum ObligationState { Due = 1, Paid = 2, Shortfall = 3 }
    internal enum EmploymentCode
    { Applied, Replay, CommandConflict, InvalidCommand, ContractUnknown, ContractNotActive, ContractExpired,
      JobCapReached, EvidenceConflict, ObligationUnknown, AlreadyPaid, AmountMismatch, PaymentUnavailable, PaymentConflict,
      CapacityReached,
      /// <summary>ECON-R05：该岗位空位已被其它活跃合同占用（与"这份合同做满件数"的 JobCapReached 不同）。</summary>
      SlotTaken,
      /// <summary>ECON-R05：国库可用余额无法覆盖"既有活跃合同剩余承诺 + 本合同承诺"（工资预留不通过）。</summary>
      InsufficientBacking,
      /// <summary>CHG-20261011-LABOR-VISUAL：该应付等待到工资结算处**交验**（新模式）；**不是失败**，
      /// 也不影响任何既有权利（旧模式应付的资格是 <see cref="WageEligibility.Immediate"/>）。</summary>
      SubmissionRequired,
      /// <summary>CHG-20261011-LABOR-VISUAL：资格被阻断或无法识别（证件/代次/模式异常）⇒ 不付款、不删除、进对账。</summary>
      EligibilityBlocked,
      /// <summary>CHG-20261011-LABOR-VISUAL：同一提交已核验过（幂等重放）。</summary>
      AlreadySubmitted,
      /// <summary>CHG-20261011-LABOR-VISUAL：流程/文档代次与提交凭据不一致（旧纸领款、跨代次呈递）。</summary>
      DocumentConflict }

    /// <summary>M6 work contract: fixed clauses for one position. The terms snapshot never changes
    /// retroactively — a raise only affects new offers.</summary>
    internal sealed class WorkContract
    {
        public const int MaximumWorkTypes = 8;
        public string ContractId { get; }
        /// <summary>持约人。**Posted（已派发待承接）时为空串** —— 弹窗草拟的合同还没有承接人。</summary>
        public string PawnKey { get; }
        public string InstitutionKey { get; }
        public string PositionKey { get; }
        public IReadOnlyList<string> WorkTypeDefNames { get; }
        public long WagePerJobMinor { get; }
        public int MaxJobs { get; }
        public int SignedTick { get; }
        public int ExpiresTick { get; }
        public ContractState State { get; }
        public int CompletedJobs { get; }
        /// <summary>允许工作的时段起（0–24；见 <see cref="AllowsHour"/>）。旧档缺省 0。</summary>
        public int WindowStartHour { get; }
        /// <summary>允许工作的时段止（0–24）。**start == end 表示全天**（旧档缺键的自然语义）。</summary>
        public int WindowEndHour { get; }
        /// <summary>CHG-20261011-LABOR-VISUAL：**付款模式**（签约时冻结，见 <see cref="SettlementDeliveryMode"/>）。
        /// 缺省 = <see cref="SettlementDeliveryMode.LegacyImmediate"/> ⇒ 旧档与既有调用点行为逐字不变（零回归）。</summary>
        public SettlementDeliveryMode DeliveryMode { get; }
        /// <summary>合同纸**有效代次**（0 = 从未签发；补发递增，旧纸随之失效）。</summary>
        public int DocumentGeneration { get; }
        /// <summary>当前有效合同纸实例 ID（空 = 未签发/已失效）。</summary>
        public string DocumentInstanceId { get; }
        public WorkContract(string contractId, string pawnKey, string institutionKey, string positionKey,
            IEnumerable<string> workTypeDefNames, long wagePerJobMinor, int maxJobs, int signedTick, int expiresTick,
            ContractState state = ContractState.Active, int completedJobs = 0,
            int windowStartHour = 0, int windowEndHour = 0,
            SettlementDeliveryMode deliveryMode = SettlementDeliveryMode.LegacyImmediate,
            int documentGeneration = 0, string documentInstanceId = null)
        {
            if (wagePerJobMinor <= 0) throw new ArgumentOutOfRangeException(nameof(wagePerJobMinor));
            if (maxJobs <= 0 || maxJobs > 100000) throw new ArgumentOutOfRangeException(nameof(maxJobs));
            if (signedTick < 0 || expiresTick <= signedTick) throw new ArgumentOutOfRangeException(nameof(expiresTick));
            if (completedJobs < 0 || completedJobs > maxJobs) throw new ArgumentOutOfRangeException(nameof(completedJobs));
            if (!Enum.IsDefined(typeof(ContractState), state)) throw new ArgumentOutOfRangeException(nameof(state));
            if (windowStartHour < 0 || windowStartHour > 24) throw new ArgumentOutOfRangeException(nameof(windowStartHour));
            if (windowEndHour < 0 || windowEndHour > 24) throw new ArgumentOutOfRangeException(nameof(windowEndHour));
            // fail-closed：**未定义**的数值不得构造（数据损坏）；`Unknown`（无法识别的字符串）**允许**构造 ——
            // 它必须保留可见性（玩家能看到"这份合同数据异常"），同时阻断一切金融动作（见 CanCommitNewWork）。
            if (!Enum.IsDefined(typeof(SettlementDeliveryMode), deliveryMode))
                throw new ArgumentOutOfRangeException(nameof(deliveryMode));
            if (documentGeneration < 0) throw new ArgumentOutOfRangeException(nameof(documentGeneration));
            MinorCurrency.Validate(wagePerJobMinor);
            ContractId = MinorCurrency.Required(contractId);
            // Posted 合同没有持约人：空串是"待承接"的表示，其余状态仍要求非空（M6 持约不变式）。
            // 持约人可空的两态：① **Posted**（待承接，尚未有人接手）；② **Terminated**（**从未承接**就作废 ——
            // 过期清理 `TrimExpiredContracts` 对 Posted 直接终结时走这条路径）。Active/Completed 仍强制非空
            //（M6 持约不变式不放宽）。
            // GATE-SAVE 2026-10-12 P0-1（热修）：缺此放宽时，过期 Posted 的终结会在构造器抛
            // ArgumentException ⇒ 每 250 tick 错误风暴 + 同 tick 结算/对账/裁剪停摆 + 工资预留永久饱和
            //（真实的"派发后无法再派发"死锁）；旧档不受影响（无 Posted 行）。
            PawnKey = (state == ContractState.Posted || state == ContractState.Terminated)
                && string.IsNullOrWhiteSpace(pawnKey) ? string.Empty : MinorCurrency.Required(pawnKey);
            InstitutionKey = MinorCurrency.Required(institutionKey); PositionKey = MinorCurrency.Required(positionKey);
            WorkTypeDefNames = Freeze(workTypeDefNames);
            WagePerJobMinor = wagePerJobMinor; MaxJobs = maxJobs; SignedTick = signedTick; ExpiresTick = expiresTick;
            State = state; CompletedJobs = completedJobs;
            WindowStartHour = windowStartHour; WindowEndHour = windowEndHour;
            DeliveryMode = deliveryMode; DocumentGeneration = documentGeneration;
            DocumentInstanceId = documentInstanceId ?? string.Empty;
        }
        private static IReadOnlyList<string> Freeze(IEnumerable<string> names)
        {
            var rows = new List<string>();
            foreach (var name in names ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (rows.Count == MaximumWorkTypes) throw new ArgumentException("TooManyWorkTypes");
                rows.Add(name);
            }
            return PerceptionCollections.Freeze(rows);
        }
        public WorkContract With(ContractState state, int completedJobs)
            => new WorkContract(ContractId, PawnKey, InstitutionKey, PositionKey, WorkTypeDefNames, WagePerJobMinor,
                MaxJobs, SignedTick, ExpiresTick, state, completedJobs, WindowStartHour, WindowEndHour,
                DeliveryMode, DocumentGeneration, DocumentInstanceId);
        /// <summary>承接：Posted → Active，写入持约人并重设 签订/到期 tick（条款快照其余部分不动）。
        /// <paramref name="deliveryMode"/>：**到站承接时与状态转换同一项写入**的模式
        /// （§5.4：旧 Posted 无劳动/应付历史在承接时原子写 PhysicalSubmission）；传 null = 保留原模式（零回归）。</summary>
        public WorkContract OccupiedBy(string pawnKey, int signedTick, int expiresTick,
            SettlementDeliveryMode? deliveryMode = null)
            => new WorkContract(ContractId, pawnKey, InstitutionKey, PositionKey, WorkTypeDefNames, WagePerJobMinor,
                MaxJobs, signedTick, expiresTick, ContractState.Active, 0, WindowStartHour, WindowEndHour,
                deliveryMode ?? DeliveryMode, DocumentGeneration, DocumentInstanceId);
        /// <summary>CHG-20261011-LABOR-VISUAL：签发/补发合同纸 —— 代次递增并换发新实例 ID
        /// （旧代次纸随之作废；**债权不随纸转手**，PayeeKey 由合同裁定）。</summary>
        public WorkContract WithDocument(int generation, string instanceId)
            => new WorkContract(ContractId, PawnKey, InstitutionKey, PositionKey, WorkTypeDefNames, WagePerJobMinor,
                MaxJobs, SignedTick, ExpiresTick, State, CompletedJobs, WindowStartHour, WindowEndHour,
                DeliveryMode, generation, instanceId);
        /// <summary>该小时是否在合同允许工作的时段内（0–24，支持跨夜环绕；start == end = 全天）。</summary>
        public bool AllowsHour(int hour)
            => WorkContractRules.AllowsHour(hour, WindowStartHour, WindowEndHour);
        /// <summary>A contract only pays for work it covers, inside its own window and job cap.
        /// CHG-20261011-LABOR-VISUAL：另加**模式可信**判据 —— 无法识别的付款模式（<see cref="SettlementDeliveryMode.Unknown"/>）
        /// 一律不产出新证据/新应付（保留既有权利，阻断新增不确定写入；fail-closed，不降级为旧模式）。</summary>
        public EmploymentCode CanAcceptEvidence(int now)
        {
            if (State != ContractState.Active) return EmploymentCode.ContractNotActive;
            if (now > ExpiresTick) return EmploymentCode.ContractExpired;
            if (CompletedJobs >= MaxJobs) return EmploymentCode.JobCapReached;
            if (DeliveryMode == SettlementDeliveryMode.Unknown) return EmploymentCode.EligibilityBlocked;
            return EmploymentCode.Applied;
        }
        /// <summary>该合同是否**可信地**可承接新工作（模式已识别）。false ⇒ 阻断签约与计件，且必须给出诊断。</summary>
        public bool CanCommitNewWork => DeliveryMode != SettlementDeliveryMode.Unknown;
    }

    /// <summary>M6 typed work evidence. The dedupe key carries the source instance, the phase and the
    /// completion sequence, so several iterations of one vanilla job are separate evidence while a
    /// repeated notification is the same one.</summary>
    internal sealed class WorkEvidence
    {
        public string ContractId { get; }
        public string SourceInstanceKey { get; }
        public string Phase { get; }
        public int Sequence { get; }
        public int Quantity { get; }
        public string Unit { get; }
        public int Tick { get; }
        public WorkEvidence(string contractId, string sourceInstanceKey, string phase, int sequence, int quantity,
            string unit, int tick)
        {
            if (sequence < 0 || quantity <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            ContractId = MinorCurrency.Required(contractId);
            SourceInstanceKey = MinorCurrency.Required(sourceInstanceKey);
            Phase = MinorCurrency.Required(phase);
            Unit = MinorCurrency.Required(unit);
            Sequence = sequence; Quantity = quantity; Tick = tick;
        }
        public string DedupeKey => ContractId + "|" + SourceInstanceKey + "|" + Phase + "|" + Sequence;
        public string EvidenceId => "evidence:" + DedupeKey;
    }

    /// <summary>M6/M11 payable created by one piece of evidence. The obligation id is derived from the
    /// evidence, so re-accepting the same evidence can never create a second payable.</summary>
    internal sealed class WageObligation
    {
        public string ObligationId { get; }
        public string ContractId { get; }
        public string EvidenceId { get; }
        public string PayeeKey { get; }
        public long AmountMinor { get; }
        public long PaidMinor { get; }
        /// <summary>劳动完成 tick（= 应付创建 tick）。§5.3：工资信用学习用的"尚在交货"起点。</summary>
        public int DueTick { get; }
        public ObligationState State { get; }
        public string TxId { get; }
        /// <summary>CHG-20261011-LABOR-VISUAL：**付款资格** —— 创建时冻结并随行持久（<see cref="WageEligibility"/>）。
        /// 缺省 = <see cref="WageEligibility.Immediate"/> ⇒ 旧档与既有调用点行为逐字不变。</summary>
        public WageEligibility Eligibility { get; }
        /// <summary>授予该资格的交验提交 ID（空 = 无需提交/尚未提交）。</summary>
        public string SubmissionId { get; }
        /// <summary>付款资格成立 tick（§5.3 <c>PaymentEligibleTick</c>）；<c>0</c> = 资格尚未成立。
        /// 与 <see cref="DueTick"/> 分开：交货路程**不**记为雇主欠薪失败（负面评价从资格成立起算）。</summary>
        public int EligibleTick { get; }
        public WageObligation(string obligationId, string contractId, string evidenceId, string payeeKey,
            long amountMinor, int dueTick, long paidMinor = 0, ObligationState state = ObligationState.Due, string txId = null,
            WageEligibility eligibility = WageEligibility.Immediate, string submissionId = null, int eligibleTick = 0)
        {
            if (amountMinor <= 0) throw new ArgumentOutOfRangeException(nameof(amountMinor));
            if (paidMinor < 0 || paidMinor > amountMinor) throw new ArgumentOutOfRangeException(nameof(paidMinor));
            if (!Enum.IsDefined(typeof(ObligationState), state)) throw new ArgumentOutOfRangeException(nameof(state));
            if (!Enum.IsDefined(typeof(WageEligibility), eligibility)) throw new ArgumentOutOfRangeException(nameof(eligibility));
            MinorCurrency.Validate(amountMinor); MinorCurrency.Validate(paidMinor);
            ObligationId = MinorCurrency.Required(obligationId); ContractId = MinorCurrency.Required(contractId);
            EvidenceId = MinorCurrency.Required(evidenceId); PayeeKey = MinorCurrency.Required(payeeKey);
            AmountMinor = amountMinor; PaidMinor = paidMinor; DueTick = dueTick; State = state; TxId = txId;
            Eligibility = eligibility; SubmissionId = submissionId ?? string.Empty; EligibleTick = eligibleTick;
        }
        public static string IdFor(string evidenceId) => "wage:" + evidenceId;
        public WageObligation Paid(string txId)
            => new WageObligation(ObligationId, ContractId, EvidenceId, PayeeKey, AmountMinor, DueTick, AmountMinor,
                ObligationState.Paid, txId, Eligibility, SubmissionId, EligibleTick);
        /// <summary>交验核验通过：资格转为可付款并记录提交身份与资格成立 tick（金额/身份不动）。</summary>
        public WageObligation Submitted(string submissionId, int eligibleTick)
        {
            if (string.IsNullOrWhiteSpace(submissionId)) throw new ArgumentNullException(nameof(submissionId));
            return new WageObligation(ObligationId, ContractId, EvidenceId, PayeeKey, AmountMinor, DueTick, PaidMinor,
                State, TxId, WageEligibility.Submitted, submissionId, eligibleTick);
        }
        /// <summary>核验失败/不确定 ⇒ 阻断付款（**不删除**：欠薪权利保留，进对账）。</summary>
        public WageObligation Blocked(string reasonSubmissionId, int now)
            => new WageObligation(ObligationId, ContractId, EvidenceId, PayeeKey, AmountMinor, DueTick, PaidMinor,
                State, TxId, WageEligibility.Blocked, reasonSubmissionId, EligibleTick);
    }
}

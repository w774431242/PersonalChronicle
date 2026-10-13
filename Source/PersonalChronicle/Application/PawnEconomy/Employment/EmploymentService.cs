using System;
using System.Collections.Generic;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Employment;

namespace PersonalChronicle.Application.PawnEconomy.Employment
{
    internal sealed class SignRequest
    {
        public string ContractId, PawnKey, InstitutionKey, PositionKey;
        public IReadOnlyList<string> WorkTypeDefNames;
        public long WagePerJobMinor;
        public int MaxJobs, Now, TermTicks = 60000;
        /// <summary>该岗位允许的**活跃合同份数**上限；<c>0</c> = 不限份数（件数型合同）。
        /// CHG-20261010-ECON-VANILLA-CUT：原版工作合同按**件数**约束总量（同工种待办预留），
        /// 没有"岗位空位数"语义 —— 传 0 表示该判据不适用。车间岗位仍传
        /// <c>EmploymentPolicy.MaxJobsPerPosition</c>，语义与行为都保持不变。</summary>
        public int PositionCapacity;
        /// <summary>CHG-20261012-ECON-CONTRACTIZE：允许工作时段（0–24）。**默认 0/0 = 全天** ——
        /// 车间岗位等既有调用方不传即保持"不限时段"的旧语义（零回归）。</summary>
        public int WindowStartHour, WindowEndHour;
        /// <summary>起草时的目标状态：<see cref="ContractState.Posted"/> = 「工作合同弹窗」草拟的已派发合同
        /// （无持约人、到期即起草期）。默认 <see cref="ContractState.Active"/> = 既有签约路径。</summary>
        public ContractState State = ContractState.Active;
        /// <summary>CHG-20261011-LABOR-VISUAL：**签约时冻结的付款模式**（§5.3「模式在签约时冻结，
        /// 不按窗口/设置临时切换」）。默认 <see cref="SettlementDeliveryMode.LegacyImmediate"/> 保持既有调用点零回归；
        /// 地图劳务链一律显式传 <see cref="SettlementDeliveryMode.PhysicalSubmission"/>。</summary>
        public SettlementDeliveryMode DeliveryMode = SettlementDeliveryMode.LegacyImmediate;
        /// <summary>签订期（起草 Posted 时的有效期限）。</summary>
        public int DocumentGeneration;
    }
    internal sealed class WageResult
    {
        public EmploymentCode Code { get; }
        public string ObligationId { get; }
        public long AmountMinor { get; }
        public WageResult(EmploymentCode code, string obligationId = null, long amountMinor = 0)
        { Code = code; ObligationId = obligationId; AmountMinor = amountMinor; }
    }

    /// <summary>M6 employment coordination: one signed contract per position, one payable per piece of
    /// evidence, and payment only through an obligation that M11 settles. Nothing here creates money,
    /// starts a job or writes a work priority (that stays with the contract owner and EXC-2026-028).</summary>
    internal sealed class EmploymentService
    {
        private readonly EmploymentSaveData data;
        public EmploymentService(EmploymentSaveData data) { this.data = data ?? throw new ArgumentNullException(nameof(data)); }
        public int Contracts => data.ContractCount;

        /// <summary>CHG-20261012-WORKGATE：该 pawn 仍有效的合同覆盖的工种集合（工作授权判定用，只读）。</summary>
        public void CollectActiveWorkTypesFor(string pawnKey, int now, int hour,
            ISet<string> availableInstitutions, HashSet<string> into)
            => data.CollectActiveWorkTypesFor(pawnKey, now, hour, availableInstitutions, into);
        /// <summary>ECON-R05：所有活跃合同的剩余承诺额之和（工资预留判据，见 Sign 的调用方）。</summary>
        public long OutstandingCommitments => data.SumOutstandingCommitments();
        /// <summary>ECON-R08：已过期仍未结束的合同 ID（有界）—— 离图 Pawn 的清理不依赖 Pawn 实例。</summary>
        public IReadOnlyList<string> ReadExpiredContractIds(int now, int limit) => data.ReadExpiredContractIds(now, limit);
        public int Obligations => data.ObligationCount;

        public WorkContract ReadContract(string contractId)
        {
            var row = data.ReadContract(contractId);
            if (row == null) return null;
            return new WorkContract(row.ContractId, row.PawnKey, row.InstitutionKey, row.PositionKey, row.WorkTypeDefNames,
                row.WagePerJobMinor, row.MaxJobs, row.SignedTick, row.ExpiresTick,
                EmploymentSaveData.ParseState(row.State), row.CompletedJobs,
                row.WindowStartHour, row.WindowEndHour,
                // CHG-20261011-LABOR-VISUAL：缺键 ⇒ LegacyImmediate（旧档自然语义）；无法识别 ⇒ Unknown（阻断金融动作）。
                EmploymentSaveData.ParseDeliveryMode(row.DeliveryMode),
                row.DocumentGeneration, row.DocumentInstanceId);
        }
        /// <summary>CHG-20261012：该机构下所有 **Posted（已派发待承接）** 合同（有界）。
        /// 机会源据此发布、合同表据此显示"待承接"行 —— 与 <c>ReadContractsFor(pawnKey)</c>（按持约人）互补。</summary>
        public IReadOnlyList<WorkContract> ReadPostedContractsFor(string institutionKey)
        {
            var rows = new List<WorkContract>();
            var stored = data.ReadPostedContractsFor(institutionKey);
            for (int i = 0; i < stored.Count; i++)
            {
                var contract = ReadContract(stored[i].ContractId);
                if (contract != null) rows.Add(contract);
            }
            return rows.AsReadOnly();
        }
        public WageObligation ReadObligation(string obligationId)
        {
            var row = data.ReadObligation(obligationId);
            if (row == null) return null;
            return new WageObligation(row.ObligationId, row.ContractId, row.EvidenceId, row.PayeeKey, row.AmountMinor,
                row.DueTick, row.PaidMinor, EmploymentSaveData.ParseObligationState(row.State), row.TxId,
                // CHG-20261011-LABOR-VISUAL：缺键 ⇒ Immediate（旧应付既有权利分毫不变）；无法识别 ⇒ Unknown（不付款）。
                EmploymentSaveData.ParseEligibility(row.Eligibility), row.SubmissionId, row.EligibleTick);
        }
        public IReadOnlyList<WageObligation> ReadObligationsFor(string pawnKey)
        {
            var rows = new List<WageObligation>();
            var stored = data.ReadObligationsFor(pawnKey);
            for (int i = 0; i < stored.Count; i++) rows.Add(ReadObligation(stored[i].ObligationId));
            return rows.AsReadOnly();
        }
        /// <summary>CHG-20261011-LABOR-VISUAL：该合同名下的全部应付（合同纸检查 / 合同表列 / 交验共用；只读）。</summary>
        public IReadOnlyList<WageObligation> ReadObligationsForContract(string contractId)
        {
            var rows = new List<WageObligation>();
            var stored = data.ReadObligationsForContract(contractId);
            for (int i = 0; i < stored.Count; i++)
            {
                var obligation = ReadObligation(stored[i].ObligationId);
                if (obligation != null) rows.Add(obligation);
            }
            return rows.AsReadOnly();
        }

        /// <summary>Signs one contract. Re-issuing the same contract id with the same terms replays; a
        /// different term set for the same id is a conflict and never silently rewrites the clauses.
        /// CHG-20261012-ECON-CONTRACTIZE：<c>request.State = Posted</c> 时签出的是「工作合同弹窗」草拟的
        /// **已派发待承接**合同（无持约人，条款/时段/薪资照常入库）。</summary>
        public WageResult Sign(SignRequest request)
        {
            bool posted = request != null && request.State == ContractState.Posted;
            if (request == null || string.IsNullOrWhiteSpace(request.ContractId)
                || (!posted && string.IsNullOrWhiteSpace(request.PawnKey))
                || string.IsNullOrWhiteSpace(request.InstitutionKey) || string.IsNullOrWhiteSpace(request.PositionKey)
                || request.WagePerJobMinor <= 0 || request.MaxJobs <= 0 || request.TermTicks <= 0) return new WageResult(EmploymentCode.InvalidCommand);
            var existing = ReadContract(request.ContractId);
            if (existing != null)
                return new WageResult(existing.PawnKey == request.PawnKey && existing.PositionKey == request.PositionKey
                    && existing.WagePerJobMinor == request.WagePerJobMinor && existing.MaxJobs == request.MaxJobs
                    // CHG-20261012-ECON-CONTRACTIZE · B1（P2-3）：**时段也是条款**。合同 ID 不含时段
                    // （ID = `contract:{pawn}|{机会键}|w{单价}|{起草 tick}`），漏比时段会让"同 ID 仅改时段"
                    // 被判成幂等重放 ⇒ 静默保留旧时段（玩家以为改了，实际没改；条款不可静默改写是本服务的既定纪律）。
                    && existing.WindowStartHour == request.WindowStartHour
                    && existing.WindowEndHour == request.WindowEndHour
                    // CHG-20261011-LABOR-VISUAL：**模式也是条款** —— 同 ID 仅改付款模式必须判冲突，
                    // 否则"以为改成到站交验、实际仍是即时付款"的静默改写（条款不可静默改写是既定纪律）。
                    && existing.DeliveryMode == request.DeliveryMode
                    ? EmploymentCode.Replay : EmploymentCode.CommandConflict);
            WorkContract contract;
            try
            {
                contract = new WorkContract(request.ContractId, request.PawnKey, request.InstitutionKey, request.PositionKey,
                    request.WorkTypeDefNames, request.WagePerJobMinor, request.MaxJobs, request.Now,
                    checked(request.Now + request.TermTicks), request.State, 0,
                    request.WindowStartHour, request.WindowEndHour, request.DeliveryMode);
            }
            catch (Exception error) when (error is ArgumentException || error is OverflowException)
            { return new WageResult(EmploymentCode.InvalidCommand); }
            if (!data.CanWriteContract) return new WageResult(EmploymentCode.CapacityReached);
            // ECON-R05：岗位空位竞争裁决 —— 同一岗位已有 request.PositionCapacity 份**活跃**合同 ⇒ 不再签新的。
            // 此前机会源只读班次缺员、签约又不登记占用 ⇒ 多名 Pawn 可看到同一空位，
            // 机构却被重复承诺同一笔工资（验收：两 Pawn 争一个空位，只成功一个）。
            // 放在写入前、与容量检查同一提交序：失败即不产生任何合同，无半提交。
            // CHG-20261010-ECON-VANILLA-CUT：判据由 `MaxJobs` 改为**显式的** `PositionCapacity` ——
            // 旧写法把"件数"当"份数上限"，对件数型合同（原版工作）会在小余量时误拒合法签约
            // （例：余 4 件、已有 4 份 ⇒ 4 ≥ 4 ⇒ 第 5 份被拒，剩余工作派不出去）。
            // `PositionCapacity = 0` ⇒ 该岗位不限份数，总量由"同工种待办预留"约束（唯一来源）。
            if (request.PositionCapacity > 0 && data.CountActiveByPosition(request.PositionKey) >= request.PositionCapacity)
                return new WageResult(EmploymentCode.SlotTaken);
            data.WriteContract(contract);
            return new WageResult(EmploymentCode.Applied, contract.ContractId);
        }

        /// <summary>CHG-20261012-ECON-CONTRACTIZE：承接一份 **Posted（已派发）** 合同 —— **同一份合同**转 Active
        /// 并写入持约人（ContractId 不变；条款/时段/薪资原样保留，不产生第二份合同）。
        /// 返回 false = 不存在 / 已被他人承接 / 参数非法（调用方按"机会已消失"处理，**不得静默成功**）。</summary>
        public bool TryOccupy(string contractId, string pawnKey, int now, int termTicks,
            SettlementDeliveryMode? deliveryMode = null)
        {
            if (string.IsNullOrWhiteSpace(contractId) || string.IsNullOrWhiteSpace(pawnKey)) return false;
            if (termTicks <= 0) return false;
            var contract = ReadContract(contractId);
            if (contract == null || contract.State != ContractState.Posted) return false;
            // now（游戏时钟 tick）与 termTicks（日量级常量）相加不可能溢出 int，无需 checked。
            // CHG-20261011-LABOR-VISUAL：模式与状态转换**同一项写入**（§5.4：旧 Posted 无劳动/应付历史
            // 在承接时原子写 PhysicalSubmission；调用方传 null = 保留原模式，零回归）。
            var occupied = contract.OccupiedBy(pawnKey, now, now + termTicks, deliveryMode);
            data.WriteContract(occupied);
            return true;
        }

        /// <summary>CHG-20261012-ECON-CONTRACTIZE · B1（P1-2）：**撤销一份 Posted（已派发待承接）** 合同。
        /// 只接受 Posted —— 已承接（Active）/已终结（Terminated）一律拒绝：**解约是另一条线**（需要玩家裁决
        /// 与专门写路径），这里绝不做"顺手把在途合同作废"。写入复用既有 <see cref="Terminate"/>
        /// （对 Terminated 幂等 Replay）⇒ 零新增 Save API、零 schema 变化；撤销即释放件数承诺与资金预留
        /// （<c>SumOutstandingCommitments</c> 与 <c>VanillaJobsCommitted</c> 都按状态统计，终止后自然回落）。</summary>
        public EmploymentCode RevokePosted(string contractId)
        {
            var contract = ReadContract(contractId);
            if (contract == null) return EmploymentCode.ContractUnknown;
            if (contract.State != ContractState.Posted) return EmploymentCode.ContractNotActive;
            return Terminate(contractId);
        }

        /// <summary>Accepts one piece of evidence and creates exactly one payable. The evidence identity
        /// makes this idempotent: a repeated notification returns the same obligation.</summary>
        public WageResult AcceptEvidence(WorkEvidence evidence)
        {
            if (evidence == null) return new WageResult(EmploymentCode.InvalidCommand);
            string obligationId = WageObligation.IdFor(evidence.EvidenceId);
            if (data.HasEvidence(evidence.EvidenceId))
            {
                var settled = ReadObligation(obligationId);
                return new WageResult(EmploymentCode.Replay, obligationId, settled?.AmountMinor ?? 0);
            }
            var contract = ReadContract(evidence.ContractId);
            if (contract == null) return new WageResult(EmploymentCode.ContractUnknown);
            var code = contract.CanAcceptEvidence(evidence.Tick);
            if (code != EmploymentCode.Applied) return new WageResult(code);
            // CHG-20261011-LABOR-VISUAL：付款资格在**创建时冻结**（新模式 ⇒ PendingSubmission，旧模式 ⇒ Immediate），
            // 并随 ObligationRow 持久。**不得**在查询/付款时按"当前是否有提交记录/实体纸/流程行"重算 ——
            // 旧档应付三者皆无，重算会把旧欠薪整体堵死（旧模式链永不读提交/纸/Job 是硬不变量）。
            var eligibility = LaborFlowRules.EligibilityOnEvidence(contract.DeliveryMode);
            if (eligibility == WageEligibility.Unknown) return new WageResult(EmploymentCode.EligibilityBlocked);
            WageObligation obligation;
            try
            {
                obligation = new WageObligation(obligationId, contract.ContractId, evidence.EvidenceId, contract.PawnKey,
                    contract.WagePerJobMinor, evidence.Tick, 0, ObligationState.Due, null,
                    eligibility, null, eligibility == WageEligibility.Immediate ? evidence.Tick : 0);
            }
            catch (Exception error) when (error is ArgumentException || error is OverflowException)
            { return new WageResult(EmploymentCode.InvalidCommand); }
            // Capacity is checked before anything is committed: a recorded evidence key without its
            // payable (or a payable without a durable dedupe key) would be a half-completed settlement.
            if (!data.CanRecordEvidence(evidence.EvidenceId) || !data.CanWriteObligation)
                return new WageResult(EmploymentCode.CapacityReached);
            data.MarkEvidence(evidence.EvidenceId);
            data.WriteObligation(obligation);
            data.WriteContract(contract.With(ContractState.Active, contract.CompletedJobs + 1));
            return new WageResult(EmploymentCode.Applied, obligation.ObligationId, obligation.AmountMinor);
        }

        /// <summary>Records the settlement of one obligation after M11 finalizes the wage transfer.</summary>
        public EmploymentCode MarkPaid(string obligationId, long paidMinor, string txId)
        {
            var obligation = ReadObligation(obligationId);
            if (obligation == null) return EmploymentCode.ObligationUnknown;
            if (obligation.State == ObligationState.Paid) return EmploymentCode.AlreadyPaid;
            if (paidMinor != obligation.AmountMinor) return EmploymentCode.AmountMismatch;
            data.WriteObligation(obligation.Paid(txId));
            return EmploymentCode.Applied;
        }

        /// <summary>
        /// CHG-20261011-LABOR-VISUAL：**交验核验通过** ⇒ 授予付款资格（唯一写入点）。
        /// 幂等：同一提交重复授予 ⇒ <see cref="EmploymentCode.AlreadySubmitted"/>（不重复写、不重复付款）。
        /// 只对 <see cref="WageEligibility.PendingSubmission"/> 生效；已 <see cref="WageEligibility.Submitted"/>
        /// 的应付回 <see cref="EmploymentCode.AlreadySubmitted"/>；<see cref="WageEligibility.Unknown"/> /
        /// <see cref="WageEligibility.Blocked"/> 一律拒绝（fail-closed，不降级为可付）。
        /// </summary>
        public EmploymentCode GrantSubmission(string obligationId, string submissionId, int now)
        {
            if (string.IsNullOrWhiteSpace(submissionId)) return EmploymentCode.InvalidCommand;
            var obligation = ReadObligation(obligationId);
            if (obligation == null) return EmploymentCode.ObligationUnknown;
            if (obligation.State == ObligationState.Paid) return EmploymentCode.AlreadyPaid;
            if (obligation.Eligibility == WageEligibility.Submitted)
                return string.Equals(obligation.SubmissionId, submissionId, StringComparison.Ordinal)
                    ? EmploymentCode.AlreadySubmitted : EmploymentCode.DocumentConflict;
            if (obligation.Eligibility != WageEligibility.PendingSubmission)
                return EmploymentCode.EligibilityBlocked;
            data.WriteObligation(obligation.Submitted(submissionId, now));
            return EmploymentCode.Applied;
        }

        /// <summary>Record an actual failed payment attempt. Amount, eligibility, recipient and receipt stay unchanged.</summary>
        public EmploymentCode NotePaymentUnavailable(string obligationId)
        {
            var row = ReadObligation(obligationId);
            if (row == null) return EmploymentCode.ObligationUnknown;
            if (row.State == ObligationState.Paid) return EmploymentCode.AlreadyPaid;
            if (!LaborFlowRules.PaymentAllowed(row.Eligibility)) return EmploymentCode.SubmissionRequired;
            if (row.State == ObligationState.Shortfall) return EmploymentCode.Replay;
            data.WriteObligation(new WageObligation(row.ObligationId, row.ContractId, row.EvidenceId, row.PayeeKey,
                row.AmountMinor, row.DueTick, row.PaidMinor, ObligationState.Shortfall, row.TxId,
                row.Eligibility, row.SubmissionId, row.EligibleTick));
            return EmploymentCode.Applied;
        }

        /// <summary>CHG-20261011-LABOR-VISUAL：核验失败/代次冲突/来源不符 ⇒ 阻断付款并留因。
        /// **不删除应付**（欠薪权利保留，进对账；§6.3「不删已赚应付」）。</summary>
        public EmploymentCode BlockCredential(string obligationId, string submissionId, int now)
        {
            var obligation = ReadObligation(obligationId);
            if (obligation == null) return EmploymentCode.ObligationUnknown;
            if (obligation.State == ObligationState.Paid) return EmploymentCode.AlreadyPaid;
            if (obligation.Eligibility == WageEligibility.Immediate) return EmploymentCode.InvalidCommand;   // 旧权利不可就地阻断
            data.WriteObligation(obligation.Blocked(submissionId ?? string.Empty, now));
            return EmploymentCode.Applied;
        }

        /// <summary>CHG-20261011-LABOR-VISUAL：写入/更新合同纸代次（**与合同状态同源写**；调用方保证两者同批提交）。</summary>
        public EmploymentCode ApplyDocument(string contractId, int generation, string instanceId)
        {
            if (string.IsNullOrWhiteSpace(contractId) || generation <= 0
                || string.IsNullOrWhiteSpace(instanceId)) return EmploymentCode.InvalidCommand;
            var contract = ReadContract(contractId);
            if (contract == null) return EmploymentCode.ContractUnknown;
            if (contract.DocumentGeneration == generation
                && string.Equals(contract.DocumentInstanceId, instanceId, StringComparison.Ordinal))
                return EmploymentCode.Replay;   // 同代次同实例 ⇒ 幂等（重放不重印）
            if (contract.DocumentGeneration > generation) return EmploymentCode.DocumentConflict;   // 不得倒退代次
            data.WriteContract(contract.WithDocument(generation, instanceId));
            return EmploymentCode.Applied;
        }

        /// <summary>CHG-20261011-LABOR-VISUAL：已赚但未付的应付余额之和（**资金占用读数**，供派单/签约资金门）。
        /// 与 <c>EmploymentSaveData.SumOutstandingCommitments</c> 的"承诺 + 负债"口径同源。</summary>
        public long UnpaidEarnedMinor => data.SumUnpaidEarnedMinor();
        /// <summary>CHG-20261011-LABOR-VISUAL：**该合同**仍未付清的金额（minor）。归档判据用（只读）。</summary>
        public long UnpaidEarnedFor(string contractId)
        {
            long due = 0L;
            var rows = data.ReadObligationsForContract(contractId);
            for (int i = 0; i < rows.Count; i++) due += rows[i].AmountMinor - rows[i].PaidMinor;
            return due;
        }

        /// <summary>Ends a contract. Already performed work stays payable: obligations are independent of
        /// the contract's own state.</summary>
        public EmploymentCode Terminate(string contractId)
        {
            var contract = ReadContract(contractId);
            if (contract == null) return EmploymentCode.ContractUnknown;
            if (contract.State == ContractState.Terminated) return EmploymentCode.Replay;
            data.WriteContract(contract.With(ContractState.Terminated, contract.CompletedJobs));
            return EmploymentCode.Applied;
        }
        /// <summary>CHG-20261010-ECON-LINKFIX-B：接近上限时裁剪已结清的证据键（见
        /// <c>EmploymentSaveData.TrimSettledEvidenceKeys</c> 的安全性论证）。
        /// 返回实际移除条数（0 = 无事可做）。调用方应在**周期维护**里调用，不放在计酬热路径。</summary>
        public int TrimSettledEvidenceKeys()
        {
            if (data.CanRecordEvidence("probe")) return 0;   // 未接近上限 ⇒ 不裁（避免无谓遍历）
            return data.TrimSettledEvidenceKeys();
        }
        public int TerminateForPawn(string pawnKey)
        {
            int ended = 0;
            var rows = data.ReadContractsFor(pawnKey);
            for (int i = 0; i < rows.Count; i++)
                if (Terminate(rows[i].ContractId) == EmploymentCode.Applied) ended++;
            return ended;
        }
    }
}

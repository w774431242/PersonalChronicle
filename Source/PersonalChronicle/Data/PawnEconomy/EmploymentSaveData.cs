using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using Verse;

namespace PersonalChronicle.Data.PawnEconomy
{
    /// <summary>Additive save node for M6 employment: contracts, accepted evidence keys and wage
    /// obligations. Evidence keys are the replay guard — trimming them would allow paying twice, so the
    /// node keeps every settled key and only drops the whole pawn's history when nothing is outstanding
    /// (bounded by entry count, newest first for unreferenced keys).</summary>
    internal sealed class EmploymentSaveData : IExposable
    {
        public sealed class ContractRow : IExposable
        {
            public string ContractId, PawnKey, InstitutionKey, PositionKey, State;
            public List<string> WorkTypeDefNames = new List<string>();
            public long WagePerJobMinor;
            public int MaxJobs, SignedTick, ExpiresTick, CompletedJobs;
            /// <summary>允许工作时段（CHG-20261012-ECON-CONTRACTIZE）。**旧档无此键 ⇒ 0/0 = 全天**（契约的自然缺省）。</summary>
            public int WindowStartHour, WindowEndHour;
            /// <summary>CHG-20261011-LABOR-VISUAL：付款模式（**string**，见 <see cref="SettlementDeliveryMode"/>）。
            /// **旧档无此键 ⇒ 空 ⇒ LegacyImmediate**（契约的自然缺省）；无法识别的值 ⇒ 行级 fail-closed（Unknown）。</summary>
            public string DeliveryMode;
            /// <summary>合同纸有效代次（旧档无此键 ⇒ 0 = 从未签发）。</summary>
            public int DocumentGeneration;
            /// <summary>当前有效合同纸实例 ID（旧档无此键 ⇒ 空）。</summary>
            public string DocumentInstanceId;
            public void ExposeData()
            {
                Scribe_Values.Look(ref ContractId, "contract"); Scribe_Values.Look(ref PawnKey, "pawn");
                Scribe_Values.Look(ref InstitutionKey, "institution"); Scribe_Values.Look(ref PositionKey, "position");
                Scribe_Values.Look(ref State, "state");
                Scribe_Collections.Look(ref WorkTypeDefNames, "workTypes", LookMode.Value);
                Scribe_Values.Look(ref WagePerJobMinor, "wage"); Scribe_Values.Look(ref MaxJobs, "maxJobs");
                Scribe_Values.Look(ref SignedTick, "signedTick"); Scribe_Values.Look(ref ExpiresTick, "expiresTick");
                Scribe_Values.Look(ref CompletedJobs, "completedJobs");
                Scribe_Values.Look(ref WindowStartHour, "windowStart"); Scribe_Values.Look(ref WindowEndHour, "windowEnd");
                Scribe_Values.Look(ref DeliveryMode, "deliveryMode");
                Scribe_Values.Look(ref DocumentGeneration, "docGen");
                Scribe_Values.Look(ref DocumentInstanceId, "docInstance");
                if (Scribe.mode == LoadSaveMode.PostLoadInit && WorkTypeDefNames == null) WorkTypeDefNames = new List<string>();
            }
        }
        public sealed class ObligationRow : IExposable
        {
            public string ObligationId, ContractId, EvidenceId, PayeeKey, State, TxId;
            public long AmountMinor, PaidMinor;
            public int DueTick;
            /// <summary>CHG-20261011-LABOR-VISUAL：付款资格（**string**，见 <see cref="WageEligibility"/>）。
            /// **旧档无此键 ⇒ 空 ⇒ Immediate**（既有付款权利分毫不变）；无法识别的值 ⇒ fail-closed（Unknown）。</summary>
            public string Eligibility;
            /// <summary>授予资格的交验提交 ID（旧档无此键 ⇒ 空）。</summary>
            public string SubmissionId;
            /// <summary>付款资格成立 tick（旧档无此键 ⇒ 0 = 与创建同刻，按 <see cref="DueTick"/> 解释）。</summary>
            public int EligibleTick;
            public void ExposeData()
            {
                Scribe_Values.Look(ref ObligationId, "obligation"); Scribe_Values.Look(ref ContractId, "contract");
                Scribe_Values.Look(ref EvidenceId, "evidence"); Scribe_Values.Look(ref PayeeKey, "payee");
                Scribe_Values.Look(ref State, "state"); Scribe_Values.Look(ref TxId, "tx");
                Scribe_Values.Look(ref AmountMinor, "amount"); Scribe_Values.Look(ref PaidMinor, "paid");
                Scribe_Values.Look(ref DueTick, "dueTick");
                Scribe_Values.Look(ref Eligibility, "eligibility");
                Scribe_Values.Look(ref SubmissionId, "submission");
                Scribe_Values.Look(ref EligibleTick, "eligibleTick");
            }
        }
        public const int MaximumContracts = 1024, MaximumObligations = 4096, MaximumEvidenceKeys = 4096;
        private List<ContractRow> contracts = new List<ContractRow>();
        private List<ObligationRow> obligations = new List<ObligationRow>();
        private List<string> evidenceKeys = new List<string>();
        private Dictionary<string, ContractRow> contractIndex;
        private Dictionary<string, ObligationRow> obligationIndex;
        private HashSet<string> evidence;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref contracts, "contracts", LookMode.Deep);
            Scribe_Collections.Look(ref obligations, "obligations", LookMode.Deep);
            Scribe_Collections.Look(ref evidenceKeys, "evidenceKeys", LookMode.Value);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            if (contracts == null) contracts = new List<ContractRow>();
            if (obligations == null) obligations = new List<ObligationRow>();
            if (evidenceKeys == null) evidenceKeys = new List<string>();
            contractIndex = null; obligationIndex = null; evidence = null;
            _ = ContractIndex; _ = ObligationIndex; _ = EvidenceSet;
            if (contracts.Count > MaximumContracts) TrimContractsOnLoad();
            if (obligations.Count > MaximumObligations) TrimObligationsOnLoad();
            // Loaded evidence keys are never trimmed: dropping one would allow a reload to pay twice.
        }

        /// <summary>
        /// CHG-20261011-LABOR-VISUAL（GATE-SAVE P1 修复）：载入超限时的裁剪**必须优先保留尚未结清的权利行**。
        ///
        /// <para><b>旧实现的缺陷</b>：直接 <c>RemoveRange(Maximum, count-Maximum)</c> 按列表序**从头之后整段丢弃** ——
        /// 一行"已完成未付欠薪"完全可能落在尾部被静默吞掉（§9.1 明禁"截断未付债务"；且丢一行未付应付
        /// 等于丢一笔真实债权）。</para>
        ///
        /// <para><b>新口径</b>：先把**未付清**的应付行整体前置保留（不裁），再用剩余额度按原序保留已结清行；
        /// 未付行本身超过上限时**全部保留**（宁可超出内存上限，也不静默吞掉债务 —— 诊断另有告警）。</para>
        /// </summary>
        private void TrimObligationsOnLoad()
        {
            var keep = new List<ObligationRow>(obligations.Count);
            var settled = new List<ObligationRow>(obligations.Count);
            for (int i = 0; i < obligations.Count; i++)
            {
                var row = obligations[i];
                if (row == null) continue;
                if (row.PaidMinor < row.AmountMinor) keep.Add(row); else settled.Add(row);
            }
            int room = MaximumObligations - keep.Count;
            if (room > 0)
            {
                int take = settled.Count <= room ? settled.Count : room;
                for (int i = 0; i < take; i++) keep.Add(settled[i]);
            }
            obligations = keep;
            obligationIndex = null;
        }

        /// <summary>合同行的同式裁剪：**有未付应付关联**的合同与仍活跃/已派发的合同整体保留，其余按原序填充剩余额度。</summary>
        private void TrimContractsOnLoad()
        {
            var keep = new List<ContractRow>(contracts.Count);
            var droppable = new List<ContractRow>(contracts.Count);
            for (int i = 0; i < contracts.Count; i++)
            {
                var row = contracts[i];
                if (row == null) continue;
                var state = ParseState(row.State);
                bool live = state == ContractState.Active || state == ContractState.Posted;
                if (live || HasUnpaidObligation(row.ContractId)) keep.Add(row); else droppable.Add(row);
            }
            int room = MaximumContracts - keep.Count;
            if (room > 0)
            {
                int take = droppable.Count <= room ? droppable.Count : room;
                for (int i = 0; i < take; i++) keep.Add(droppable[i]);
            }
            contracts = keep;
            contractIndex = null;
        }

        /// <summary>该合同是否仍有未付清的应付（裁剪保护判据；只读）。</summary>
        public bool HasUnpaidObligation(string contractId)
        {
            if (string.IsNullOrEmpty(contractId)) return false;
            foreach (var row in obligations)
            {
                if (row == null) continue;
                if (!string.Equals(row.ContractId, contractId, StringComparison.Ordinal)) continue;
                if (row.PaidMinor < row.AmountMinor) return true;
            }
            return false;
        }
        private Dictionary<string, ContractRow> ContractIndex
        {
            get
            {
                if (contractIndex != null) return contractIndex;
                contractIndex = new Dictionary<string, ContractRow>(StringComparer.Ordinal);
                for (int i = 0; i < contracts.Count; i++)
                {
                    var row = contracts[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.ContractId)) continue;
                    contractIndex[row.ContractId] = row;
                }
                return contractIndex;
            }
        }
        private Dictionary<string, ObligationRow> ObligationIndex
        {
            get
            {
                if (obligationIndex != null) return obligationIndex;
                obligationIndex = new Dictionary<string, ObligationRow>(StringComparer.Ordinal);
                for (int i = 0; i < obligations.Count; i++)
                {
                    var row = obligations[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.ObligationId)) continue;
                    obligationIndex[row.ObligationId] = row;
                }
                return obligationIndex;
            }
        }
        private HashSet<string> EvidenceSet
        {
            get
            {
                if (evidence != null) return evidence;
                evidence = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < evidenceKeys.Count; i++) if (!string.IsNullOrWhiteSpace(evidenceKeys[i])) evidence.Add(evidenceKeys[i]);
                return evidence;
            }
        }

        public int ContractCount => ContractIndex.Count;
        public int ObligationCount => ObligationIndex.Count;
        public bool HasEvidence(string evidenceId) => evidenceId != null && EvidenceSet.Contains(evidenceId);

        /// <summary>CHG-20261012-WORKGATE：把该 pawn **仍然有效**的合同所覆盖的工种并入 <paramref name="into"/>。
        /// 口径 = Active、未到期/做满、当前小时在合同窗口且发布机构属于当前地图；纯只读。
        /// 有界：先按 pawn 取行（索引），行数上限 = 合同账本容量。</summary>
        public void CollectActiveWorkTypesFor(string pawnKey, int now, int hour,
            ISet<string> availableInstitutions, HashSet<string> into)
        {
            if (string.IsNullOrEmpty(pawnKey) || into == null || availableInstitutions == null || hour < 0 || hour >= 24) return;
            IReadOnlyList<ContractRow> rows = ReadContractsFor(pawnKey);
            for (int i = 0; i < rows.Count; i++)
            {
                ContractRow row = rows[i];
                if (row == null || row.State != ContractState.Active.ToString()) continue;
                if (row.ExpiresTick > 0 && row.ExpiresTick <= now) continue;
                if (row.CompletedJobs >= row.MaxJobs || row.SignedTick > now) continue;
                if (!availableInstitutions.Contains(row.InstitutionKey)) continue;
                if (!WorkContractRules.AllowsHour(hour, row.WindowStartHour, row.WindowEndHour)) continue;
                List<string> names = row.WorkTypeDefNames;
                for (int j = 0; names != null && j < names.Count; j++)
                    if (!string.IsNullOrEmpty(names[j])) into.Add(names[j]);
            }
        }
        public ContractRow ReadContract(string contractId)
            => contractId != null && ContractIndex.TryGetValue(contractId, out var row) ? row : null;
        public ObligationRow ReadObligation(string obligationId)
            => obligationId != null && ObligationIndex.TryGetValue(obligationId, out var row) ? row : null;
        public IReadOnlyList<ContractRow> ReadContractsFor(string pawnKey)
        {
            var rows = new List<ContractRow>();
            if (pawnKey == null) return rows.AsReadOnly();
            foreach (var row in ContractIndex.Values) if (string.Equals(row.PawnKey, pawnKey, StringComparison.Ordinal)) rows.Add(row);
            return rows.AsReadOnly();
        }
        public IReadOnlyList<ObligationRow> ReadObligationsFor(string pawnKey)
        {
            var rows = new List<ObligationRow>();
            if (pawnKey == null) return rows.AsReadOnly();
            foreach (var row in ObligationIndex.Values) if (string.Equals(row.PayeeKey, pawnKey, StringComparison.Ordinal)) rows.Add(row);
            return rows.AsReadOnly();
        }
        /// <summary>该合同名下的全部应付（已付 + 未付；合同表列与合同纸检查共用；只读）。</summary>
        public IReadOnlyList<ObligationRow> ReadObligationsForContract(string contractId)
        {
            var rows = new List<ObligationRow>();
            if (string.IsNullOrEmpty(contractId)) return rows.AsReadOnly();
            foreach (var row in ObligationIndex.Values)
                if (row != null && string.Equals(row.ContractId, contractId, StringComparison.Ordinal)) rows.Add(row);
            return rows.AsReadOnly();
        }
        public void WriteContract(WorkContract contract)
        {
            if (contract == null) return;
            if (!ContractIndex.TryGetValue(contract.ContractId, out var row))
            {
                if (contracts.Count >= MaximumContracts) return;
                row = new ContractRow { ContractId = contract.ContractId };
                contracts.Add(row); contractIndex[contract.ContractId] = row;
            }
            row.PawnKey = contract.PawnKey; row.InstitutionKey = contract.InstitutionKey; row.PositionKey = contract.PositionKey;
            row.State = contract.State.ToString(); row.WorkTypeDefNames = new List<string>(contract.WorkTypeDefNames);
            row.WagePerJobMinor = contract.WagePerJobMinor; row.MaxJobs = contract.MaxJobs;
            row.SignedTick = contract.SignedTick; row.ExpiresTick = contract.ExpiresTick; row.CompletedJobs = contract.CompletedJobs;
            row.WindowStartHour = contract.WindowStartHour; row.WindowEndHour = contract.WindowEndHour;
            row.DeliveryMode = contract.DeliveryMode.ToString();
            row.DocumentGeneration = contract.DocumentGeneration;
            row.DocumentInstanceId = contract.DocumentInstanceId;
        }
        /// <summary>该机构下所有 **Posted（已派发待承接）** 合同（有界枚举；弹窗核验 / 机会源 / 合同表共用的读入口）。
        /// 承接后状态转 Active ⇒ 自然从本结果集消失（模板不同：机会源只在承接前读取）。</summary>
        public IReadOnlyList<ContractRow> ReadPostedContractsFor(string institutionKey)
        {
            var rows = new List<ContractRow>();
            if (string.IsNullOrEmpty(institutionKey)) return rows.AsReadOnly();
            foreach (var row in ContractIndex.Values)
            {
                if (row == null || ParseState(row.State) != ContractState.Posted) continue;
                if (!string.Equals(row.InstitutionKey, institutionKey, StringComparison.Ordinal)) continue;
                rows.Add(row);
            }
            return rows.AsReadOnly();
        }
        public void WriteObligation(WageObligation obligation)
        {
            if (obligation == null) return;
            if (!ObligationIndex.TryGetValue(obligation.ObligationId, out var row))
            {
                if (obligations.Count >= MaximumObligations) return;
                row = new ObligationRow { ObligationId = obligation.ObligationId };
                obligations.Add(row); obligationIndex[obligation.ObligationId] = row;
            }
            row.ContractId = obligation.ContractId; row.EvidenceId = obligation.EvidenceId; row.PayeeKey = obligation.PayeeKey;
            row.State = obligation.State.ToString(); row.TxId = obligation.TxId; row.AmountMinor = obligation.AmountMinor;
            row.PaidMinor = obligation.PaidMinor; row.DueTick = obligation.DueTick;
            row.Eligibility = obligation.Eligibility.ToString();
            row.SubmissionId = obligation.SubmissionId;
            row.EligibleTick = obligation.EligibleTick;
        }
        public bool MarkEvidence(string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(evidenceId)) return false;
            if (EvidenceSet.Contains(evidenceId)) return true;
            if (evidenceKeys.Count >= MaximumEvidenceKeys) return false;
            evidenceKeys.Add(evidenceId); evidence.Add(evidenceId);
            return true;
        }
        public bool CanRecordEvidence(string evidenceId)
            => !string.IsNullOrWhiteSpace(evidenceId)
                && (EvidenceSet.Contains(evidenceId) || evidenceKeys.Count < MaximumEvidenceKeys);
        /// <summary>
        /// CHG-20261010-ECON-LINKFIX-B（链路审计 B 席 P1-③）：**安全裁剪已结清的证据键**。
        ///
        /// <para><b>为什么必须裁剪</b>：`evidenceKeys` 在载入时**刻意不裁**（行 67 的既有注释：
        /// 丢一个键就可能让重放重复付款）⇒ 只增不减，触顶 4096 后 `MarkEvidence` 静默失败
        /// ⇒ **全链永久停付**（劳务站与车间共用此表）。</para>
        ///
        /// <para><b>为什么"已结清"可裁</b>：证据键含**完成 tick**（`DedupeKey` 的 Sequence），
        /// 历史 tick 的上报**不会重放**（同一件工作的同一 tick 只会被记录一次；不同 tick 是新键）。
        /// 因此对"**合同已非活跃 且 其应付账已付清**"的键，删除不会让任何历史事件被重新计酬。
        /// 保留规则从严：只要该键对应的合同仍活跃、或对应义务尚未付清，**一律保留**。</para>
        ///
        /// <para><b>只在接近上限时调用</b>（调用方保证）：本方法是低频维护，不是每次计酬的路径。</para>
        /// </summary>
        public int TrimSettledEvidenceKeys()
        {
            if (evidenceKeys == null || evidenceKeys.Count == 0) return 0;
            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in ObligationIndex.Values)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.EvidenceId)) continue;
                var contract = ReadContract(row.ContractId);
                bool settled = row.PaidMinor >= row.AmountMinor;
                bool closed = contract == null || ParseState(contract.State) != ContractState.Active;
                if (settled && closed) continue;   // 已结清且合同已终态 ⇒ 可丢弃
                keep.Add(row.EvidenceId);
            }
            int removed = 0;
            var next = new List<string>(keep.Count);
            for (int i = 0; i < evidenceKeys.Count; i++)
            {
                string key = evidenceKeys[i];
                if (!string.IsNullOrEmpty(key) && keep.Contains(key)) next.Add(key);
                else removed++;
            }
            if (removed > 0) { evidenceKeys = next; evidence = null; }   // 索引按需重建
            return removed;
        }
        /// <summary>
        /// ECON-R08：已过 <c>ExpiresTick</c> 但仍标记为 Active **或 Posted** 的合同 ID（有界，最多 <paramref name="limit"/> 条）。
        /// 离图/被销毁的 Pawn 无法自己触发退出复查 ⇒ 必须有一条**不依赖 Pawn 实例**的清理依据；
        /// Posted 合同同理（起草后无人承接也必须能到期撤销，否则永久占位）。CHG-20261012-ECON-CONTRACTIZE 扩展。
        /// </summary>
        public IReadOnlyList<string> ReadExpiredContractIds(int now, int limit)
        {
            var rows = new List<string>();
            if (limit <= 0) return rows.AsReadOnly();
            foreach (var row in ContractIndex.Values)
            {
                if (rows.Count >= limit) break;
                if (row == null) continue;
                var state = ParseState(row.State);
                if (state != ContractState.Active && state != ContractState.Posted) continue;
                if (row.ExpiresTick > now) continue;
                rows.Add(row.ContractId);
            }
            return rows.AsReadOnly();
        }
        /// <summary>
        /// ECON-R05：该岗位上**仍然活跃**的合同数（岗位空位竞争的唯一判据入口）。
        /// 只统计 <see cref="ContractState.Active"/> —— 已结束/已过期合同不再占岗。
        /// </summary>
        public int CountActiveByPosition(string positionKey)
        {
            if (string.IsNullOrEmpty(positionKey)) return 0;
            int count = 0;
            foreach (var row in ContractIndex.Values)
                if (row != null && string.Equals(row.PositionKey, positionKey, StringComparison.Ordinal)
                    && ParseState(row.State) == ContractState.Active) count++;
            return count;
        }
        /// <summary>
        /// ECON-R05 工资预留：所有**活跃或已派发**合同的剩余承诺额之和（<c>WagePerJobMinor × (MaxJobs − CompletedJobs)</c>）。
        /// 饱和累加（近 long 上限即返回上限）—— 预留判据只用于比较，溢出比高估更危险。
        /// CHG-20261012：**Posted 也算承诺** —— 弹窗拟定即占用国库预留（承接与否不改变"这笔钱已向玩家承诺"）。
        ///
        /// <para><b>CHG-20261011-LABOR-VISUAL（§5.3 承诺→负债守恒）</b>：结果 = **尚未完成的承诺**
        /// ＋ **已完成未支付的已赚负债**。理由：<c>AcceptEvidence</c> 在创建应付的**同一写入**里把
        /// <c>CompletedJobs</c> 加一 ⇒ 承诺侧减一件、负债侧增一件，**总占用不变**；付款（账本 Finalized ⇒
        /// <c>MarkPaid</c>）才真正释放。修复前只算承诺侧 ⇒ 完成一件即"释放"一笔资金、
        /// 而工资尚未付出 ⇒ 可对同一笔钱重复派工（T31 复现的重复承诺）。</para>
        /// </summary>
        public long SumOutstandingCommitments()
        {
            long total = 0;
            foreach (var row in ContractIndex.Values)
            {
                if (row == null) continue;
                var state = ParseState(row.State);
                if (state != ContractState.Active && state != ContractState.Posted) continue;
                int remaining = row.MaxJobs - row.CompletedJobs;
                if (remaining <= 0 || row.WagePerJobMinor <= 0) continue;
                long slice = (long)row.WagePerJobMinor * remaining;
                if (slice > long.MaxValue - total) return long.MaxValue;
                total += slice;
            }
            return Add(total, SumUnpaidEarnedMinor());
        }

        /// <summary>
        /// 已赚但**尚未支付**的应付余额之和（含已完成 / 已终止 / 已过期合同 —— 债务与合同状态无关，
        /// 见 EmploymentService.Terminate 的既有语义）。饱和累加。这部分**占用**雇主资金池：
        /// 若不在承诺门里扣除，同一笔钱既能派给新合同、又要付给旧欠薪。
        /// </summary>
        public long SumUnpaidEarnedMinor()
        {
            long total = 0;
            foreach (var row in ObligationIndex.Values)
            {
                if (row == null) continue;
                long due = row.AmountMinor - row.PaidMinor;
                if (due <= 0) continue;
                if (due > long.MaxValue - total) return long.MaxValue;
                total += due;
            }
            return total;
        }
        private static long Add(long left, long right)
            => right > long.MaxValue - left ? long.MaxValue : left + right;
        public bool CanWriteContract => contracts.Count < MaximumContracts;
        public bool CanWriteObligation => obligations.Count < MaximumObligations;
        public static ContractState ParseState(string value, ContractState fallback = ContractState.Active)
            => Enum.IsDefined(typeof(ContractState), value ?? "") && !string.IsNullOrEmpty(value)
                ? (ContractState)Enum.Parse(typeof(ContractState), value) : fallback;
        public static ObligationState ParseObligationState(string value, ObligationState fallback = ObligationState.Due)
            => !string.IsNullOrEmpty(value) && Enum.IsDefined(typeof(ObligationState), value)
                ? (ObligationState)Enum.Parse(typeof(ObligationState), value) : fallback;
        /// <summary>CHG-20261011-LABOR-VISUAL：付款模式行级解析。**缺键 ⇒ LegacyImmediate**（旧档自然语义）；
        /// 无法识别的值 ⇒ <see cref="SettlementDeliveryMode.Unknown"/>（fail-closed：既不签也不按旧模式即时付款）。</summary>
        public static SettlementDeliveryMode ParseDeliveryMode(string value)
            => LaborFlowRules.ParseMode(value, SettlementDeliveryMode.LegacyImmediate);
        /// <summary>CHG-20261011-LABOR-VISUAL：付款资格行级解析。**缺键 ⇒ Immediate**（旧应付的既有权利不变）；
        /// 无法识别的值 ⇒ <see cref="WageEligibility.Unknown"/>（不付款、不删除、进对账）。</summary>
        public static WageEligibility ParseEligibility(string value)
            => LaborFlowRules.ParseEligibility(value, WageEligibility.Immediate);
    }
}

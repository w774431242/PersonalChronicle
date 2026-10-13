using System;
using System.Collections.Generic;
using PersonalChronicle.Data.PawnEconomy;
using PersonalChronicle.Domain.PawnEconomy.Care;

namespace PersonalChronicle.Application.PawnEconomy.Care
{
    internal sealed class CreateCareRequest
    {
        public string AuthorizationId, BeneficiaryKey, GuardianKey, ExecutorKey, PayerKey;
        public ServiceKind Kind;
        public bool BeneficiaryIsAdult;
        public long MaxAmountMinor;
        public int CreatedTick, ExpiresTick;
        public long ExpectedRevision;
    }
    internal sealed class CareCapabilityResult
    {
        public ServiceCapabilityState State { get; }
        public bool Persisted { get; }
        public CareCapabilityResult(ServiceCapabilityState state, bool persisted) { State = state; Persisted = persisted; }
    }

    /// <summary>M8 coordination: who benefits, who is authorized to act, who executes and who pays are
    /// four separate identities. Nothing here charges money by itself — a charge is only recorded through
    /// an explicit payer grant, and only a capability that proved every gate may quote a price at all.</summary>
    internal sealed class CareService
    {
        private readonly CareSaveData data;
        public CareService(CareSaveData data) { this.data = data ?? throw new ArgumentNullException(nameof(data)); }
        public int Authorizations => data.AuthorizationCount;

        /// <summary>U6-A4：到期裁剪（由运行时在 250 tick 调用）。返回删除行数；幂等、不抛。</summary>
        public int TrimExpired(int now) => data.TrimExpired(now);

        public CareAuthorization ReadAuthorization(string authorizationId) => data.ReadAuthorization(authorizationId);
        public IReadOnlyList<CareAuthorization> ReadForBeneficiary(string beneficiaryKey) => data.ReadForBeneficiary(beneficiaryKey);
        public ServiceCapabilityState ReadCapability(ServiceKind kind) => data.ReadCapability(kind);
        /// <summary>The first authorization of one kind for one beneficiary, or null.</summary>
        public CareAuthorization ReadGrant(ServiceKind kind, string beneficiaryKey)
        {
            var grants = data.ReadForBeneficiary(beneficiaryKey);
            for (int i = 0; i < grants.Count; i++) if (grants[i].Kind == kind) return grants[i];
            return null;
        }
        /// <summary>该受益人该服务类型中**未过期**的授权，或 null。与 <see cref="ReadGrant"/> 的区别：
        /// 后者取"第一份"（扣费侧沿用），本方法只认仍在有效期内的，供签发侧防重使用 ——
        /// 到期授权不占名额，可以重新签发。</summary>
        public CareAuthorization ReadActiveGrant(ServiceKind kind, string beneficiaryKey, int now)
        {
            var grants = data.ReadForBeneficiary(beneficiaryKey);
            for (int i = 0; i < grants.Count; i++)
                if (grants[i].Kind == kind && !grants[i].IsExpired(now)) return grants[i];
            return null;
        }

        /// <summary>Creates the four-identity grant. Re-issuing the same id with the same terms replays; a
        /// different term set for the same id is a conflict and never rewrites the grant in place.</summary>
        public CareCode Create(CreateCareRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.AuthorizationId)) return CareCode.InvalidCommand;
            var gate = CareRules.CanCreate(request.BeneficiaryIsAdult, request.BeneficiaryKey, request.GuardianKey,
                request.PayerKey, request.CreatedTick, request.ExpiresTick);
            if (gate != CareCode.Applied) return gate;
            if (!AmountWithinCareCap(request.MaxAmountMinor)) return CareCode.InvalidCommand;
            var existing = ReadAuthorization(request.AuthorizationId);
            if (existing != null)
                return existing.BeneficiaryKey == request.BeneficiaryKey && existing.Kind == request.Kind
                    && existing.PayerKey == request.PayerKey && existing.MaxAmountMinor == request.MaxAmountMinor
                    && existing.ExpiresTick == request.ExpiresTick ? CareCode.Replay : CareCode.CommandConflict;
            if (request.ExpectedRevision > 0) return CareCode.StaleRevision;
            // v0.9 §2 M8「防止儿童需求重复」：签发侧防重。扣费侧（EconomyCare）只认该受益人的第一份同类型授权，
            // 所以多签的授权额度永远不会被用到，一旦第一份过期扣费对象还会静默换人 —— 必须在源头拦住。
            if (CareRules.CanIssue(ReadActiveGrant(request.Kind, request.BeneficiaryKey, request.CreatedTick),
                request.BeneficiaryKey, request.Kind, request.CreatedTick) == CareCode.AlreadyAuthorized)
                return CareCode.AlreadyAuthorized;
            // 四角色之三：受益者不得兼任执行者（自己在四角色里占两席，协作就退化成自给并重复记账）。
            var executorGate = CareRules.CanExecute(request.BeneficiaryKey, request.ExecutorKey);
            if (executorGate != CareCode.Applied) return executorGate;
            var authorization = new CareAuthorization(request.AuthorizationId, request.Kind, request.BeneficiaryKey,
                request.GuardianKey, request.ExecutorKey, request.PayerKey, request.MaxAmountMinor, request.CreatedTick,
                request.ExpiresTick);
            return data.WriteAuthorization(authorization) ? CareCode.Applied : CareCode.CapacityReached;
        }
        // CHG-20261012 · B2：方法名此前叫 `InstitutionPolicyRules_ValidateAmount`，但它从来只调用
        // `ServiceCapabilityRules.MaximumCapMinor`（照护能力上限），与机构政策无关 —— 政策类删除后
        // 这个名不副实的名字会误导后来者去找一个不存在的类型，故按实现改名。
        private static bool AmountWithinCareCap(long amount) => amount >= 0 && amount <= ServiceCapabilityRules.MaximumCapMinor;

        /// <summary>Records one charge against an explicit payer grant. The same guard rails apply whether
        /// the caller is a player command or an automatic settlement.</summary>
        public CareCode Charge(string authorizationId, string payerKey, long amountMinor, int now)
        {
            var authorization = ReadAuthorization(authorizationId);
            var gate = CareRules.CanCharge(authorization, payerKey, amountMinor, now);
            if (gate != CareCode.Applied) return gate;
            long charged;
            try { charged = CareRules.ApplyCharge(authorization, amountMinor); }
            catch (OverflowException) { return CareCode.CapExceeded; }
            var next = authorization.WithCharged(charged, authorization.Revision + 1);
            return data.WriteAuthorization(next) ? CareCode.Applied : CareCode.CapacityReached;
        }

        /// <summary>
        /// M8（2026-10-10）：**唯一的可扣费授权选择入口** —— 证据核验与扣费对象都必须经此选择。
        /// 三重过滤：① 未过期 ② 仍有剩余额度 ③ 若授权已指定执行者，则必须与**真实执行者**一致。
        /// 与 <see cref="ReadActiveGrant"/> 的区别：后者只过滤到期，供**签发防重**使用（"有未过期同类型即不许重签"）；
        /// 本方法供**消费侧**使用（"这一笔到底该记在谁头上"），故额外看额度与执行者。
        /// 返回 null 表示"没有可用授权"，调用方按结果码区分原因，绝不当作 0 或"没授权"。
        /// </summary>
        public CareAuthorization SelectChargeable(ServiceKind kind, string beneficiaryKey, int now, string executorKey)
        {
            var grants = data.ReadForBeneficiary(beneficiaryKey);
            for (int i = 0; i < grants.Count; i++)
            {
                var grant = grants[i];
                if (grant.Kind != kind || grant.IsExpired(now)) continue;
                if (grant.MaxAmountMinor - grant.ChargedMinor <= 0L) continue;                       // 额度已用尽
                if (CareRules.CanExecuteUnder(grant, beneficiaryKey, executorKey) != CareCode.Applied) continue;
                return grant;
            }
            return null;
        }

        /// <summary>
        /// 照护证据的唯一定键式（单一生产者）：`care-evidence:{kind}:{beneficiary}|{doctor}|{tick}`。
        /// 此前 <see cref="AcceptCareAct"/> 与 `EconomyCare.EvidenceIdOf` 各存一份副本 —— 两处漂移会让
        /// 去重与重放同时失效（同一事件被记两次）。现由本类独占生产，结算侧一律调用它。
        /// </summary>
        public static string EvidenceIdOf(ServiceKind kind, string beneficiaryKey, string doctorKey, int tick)
            => "care-evidence:" + kind.ToString() + ":" + beneficiaryKey + "|" + doctorKey + "|" + tick.ToString();

        public CareCode AcceptCareAct(ServiceKind kind, string doctorKey, string beneficiaryKey, int tick)
        {
            if (string.IsNullOrWhiteSpace(doctorKey) || string.IsNullOrWhiteSpace(beneficiaryKey)) return CareCode.InvalidCommand;
            string evidenceId = EvidenceIdOf(kind, beneficiaryKey, doctorKey, tick);
            if (data.HasCareEvidence(evidenceId)) return CareCode.Replay;
            // M8（2026-10-10）：不再"只要存在同类型授权"就记证据 —— 必须有**可用**授权：
            // 未过期 + 有剩余额度 + （已指定执行者时）执行者与该授权相符。
            // 此前三项都不查 ⇒ 过期授权、额度用尽的授权、以及任意一名医生，都能为同一条证据记账，
            // 随后扣费侧再按"第一份"取授权 ⇒ 旧授权/错误执行者占据扣费对象。
            var grant = SelectChargeable(kind, beneficiaryKey, tick, doctorKey);
            if (grant == null) return CareCode.AuthorizationUnknown;
            if (!data.CanRecordCareEvidence(evidenceId)) return CareCode.CapacityReached;
            return data.MarkCareEvidence(evidenceId) ? CareCode.Applied : CareCode.CapacityReached;
        }
        public CareCode Revoke(string authorizationId)
        {
            if (ReadAuthorization(authorizationId) == null) return CareCode.AuthorizationUnknown;
            data.RemoveAuthorization(authorizationId);
            return CareCode.Applied;
        }

        /// <summary>Re-evaluates one service capability from its real readiness. Missing gates keep the
        /// capability on the vanilla public path, so no price is ever quoted from a partial proof set.</summary>
        public CareCapabilityResult EvaluateCapability(ServiceKind kind, ServiceReadiness readiness)
        {
            var current = data.ReadCapability(kind);
            var capability = ServiceCapabilityRules.Evaluate(current?.Capability ?? ServiceCapability.PublicVanilla, readiness);
            long revision = (current?.Revision ?? 0) + 1;
            var state = new ServiceCapabilityState(kind, capability, revision, readiness?.Missing());
            if (!data.CanWriteCapability(kind)) return new CareCapabilityResult(state, false);
            return new CareCapabilityResult(state, data.WriteCapability(state));
        }
        public bool MayCharge(ServiceKind kind) => ServiceCapabilityRules.MayCharge((ReadCapability(kind)?.Capability) ?? ServiceCapability.PublicVanilla);
    }
}

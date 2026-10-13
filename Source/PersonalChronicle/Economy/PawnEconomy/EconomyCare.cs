using System;
using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy.Care;
using PersonalChronicle.Domain.PawnEconomy.Care;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>B5 host surface for M8 care and M7 service capabilities. Read-mostly: the four-identity
    /// grants and capability states are written through the application service, which keeps the payer
    /// gate, the cap gate and the "no price without every piece of evidence" gate in one place.</summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        private PersonalChronicle.Data.PawnEconomy.CareSaveData care
            = new PersonalChronicle.Data.PawnEconomy.CareSaveData();
        private CareService careService;
        internal CareService Care => careService ?? (careService = new CareService(care));

        internal IReadOnlyList<CareAuthorization> QueryCareAuthorizations(string beneficiaryKey)
            => Care.ReadForBeneficiary(beneficiaryKey);
        internal CareAuthorization QueryCareAuthorization(string authorizationId) => Care.ReadAuthorization(authorizationId);
        internal ServiceCapabilityState QueryServiceCapability(ServiceKind kind) => Care.ReadCapability(kind);
        internal bool ServiceMayCharge(ServiceKind kind) => Care.MayCharge(kind);
        /// <summary>Re-evaluates one capability from real evidence. Until the caller can prove every gate
        /// the capability stays on the vanilla public path, so nothing is charged for it.</summary>
        internal CareCapabilityResult EvaluateServiceCapability(ServiceKind kind, ServiceReadiness readiness)
            => Care.EvaluateCapability(kind, readiness);
        internal CareCode CreateCareAuthorization(CreateCareRequest request) => Care.Create(request);
        internal CareCode ChargeCareAuthorization(string authorizationId, string payerKey, long amountMinor, int now)
            => Care.Charge(authorizationId, payerKey, amountMinor, now);
        internal CareCode RevokeCareAuthorization(string authorizationId) => Care.Revoke(authorizationId);
        internal int CareAuthorizationCount => Care.Authorizations;
        /// <summary>True only once the delivery event settles money end to end; kept false until that wiring exists.</summary>
        /// <summary>Settlement is wired: the delivery event charges the payer's own account through the
        /// ledger, keyed by the evidence so a repeat never charges twice. In-game verification is deferred
        /// to the acceptance unit (NOT EXECUTED), which is why the capability reports code-level facts only.</summary>
        private const bool CareSettlementWired = true;

        /// <summary>Reports one delivered vanilla tend act (EXC-2026-029). Evidence only: no money
        /// moves here, and an unauthorized beneficiary is refused outright.</summary>
        internal CareCode ReportTendDelivered(Pawn doctor, Pawn patient, int now)
        {
            if (doctor == null || patient == null) return CareCode.InvalidCommand;
            var code = Care.AcceptCareAct(ServiceKind.Treatment, doctor.GetUniqueLoadID(), patient.GetUniqueLoadID(), now);
            if (code != CareCode.Applied && code != CareCode.Replay) return code;
            var capability = RefreshServiceCapability(ServiceKind.Treatment, doctor);
            // Money only moves for a service that proved every gate, including a published price.
            if (capability.State.Capability != ServiceCapability.ReadyPaid) return code;
            TrySettleCareAct(ServiceKind.Treatment, EvidenceIdOf(ServiceKind.Treatment, patient, doctor, now), patient, doctor, now);
            return code;
        }
        /// <summary>M7 settlement of one delivered care act: the explicitly granted payer's own account pays
        /// the treasury for exactly one act. The command id derives from the evidence, so a repeat never
        /// charges twice, and an unfunded payer leaves the act uncharged (the grant keeps its remaining cap)
        /// instead of creating a silent shortfall.</summary>
        /// <summary>
        /// M8（2026-10-10）：定键式由 <see cref="CareService.EvidenceIdOf"/> **独占生产**。
        /// 此前本方法与 `CareService.AcceptCareAct` 各存一份格式副本 —— 两处漂移会让去重与重放同时失效
        /// （同一交付事件被记为两条证据、扣两次额度）。
        /// </summary>
        internal static string EvidenceIdOf(ServiceKind kind, Pawn beneficiary, Pawn executor, int tick)
            => CareService.EvidenceIdOf(kind, beneficiary.GetUniqueLoadID(), executor.GetUniqueLoadID(), tick);
        internal CareCode TrySettleCareAct(ServiceKind kind, string evidenceId, Pawn patient, Pawn doctor, int now)
        {
            if (patient == null || string.IsNullOrEmpty(evidenceId)) return CareCode.InvalidCommand;
            // M8（2026-10-10）：扣费对象改走**唯一活跃选择器** —— 未过期 + 仍有额度 + （已指定执行者时）执行者相符。
            // 此前取"第一份"同类型授权 ⇒ 旧授权或与真实执行者无关的授权会占据扣费对象
            // （审计："避免错误执行者、旧授权占据扣费对象"）。选择失败沿用既有结果码，不扩大改动面。
            var grant = Care.SelectChargeable(kind, patient.GetUniqueLoadID(), now, doctor?.GetUniqueLoadID());
            if (grant == null || grant.PayerKey == null) return CareCode.PayerNotAuthorized;
            // B2（裁决⑥）：服务价改读**体检中心建筑字段**（同一纪律：单一确定性投影）。
            // null = 未挂牌 ⇒ 拒绝结算（既有的 InvalidCommand 结果码，不新增码面）；这**不是**免费。
            long? servicePrice = QueryServicePriceMinor(doctor?.Map?.uniqueID ?? patient.Map?.uniqueID ?? 0);
            if (servicePrice == null) return CareCode.InvalidCommand;
            long amount = servicePrice.Value;
            var gate = CareRules.CanCharge(grant, grant.PayerKey, amount, now);
            if (gate != CareCode.Applied) return gate;   // expired, over cap, or a payer that was never granted
            string command = "care:" + evidenceId;
            var prepared = settlement.PreparePurchase(SettlementIntent.ForPurchase(command, settlement.Version, evidenceId, grant.PayerKey, amount));
            if (prepared.Code == SettlementCode.Replay)
            {
                // ECON-R03：重放**不得再次消耗授权额度**。只有已 Finalized 才代表这笔钱确实只扣过一次 ⇒
                // 返回既有结果、额度不再累加；Prepared/Cancelled 表示钱并未扣成 ⇒ 额度也不消费，
                // 交给重试走完整路径。此前无条件走 ChargeAndMarkFacts ⇒ 钱包扣一次、额度被重复扣。
                if (prepared.Entry == null || prepared.Entry.Phase != SettlementPhase.Finalized)
                    return CareCode.SettlementUnavailable;
                return CareCode.Applied;
            }
            if (prepared.Code != SettlementCode.Prepared) return CareCode.SettlementUnavailable;
            var completed = settlement.CompletePurchase(command);
            if (completed.Code != SettlementCode.Finalized)
            { settlement.CancelPurchase(command, "CareSettlementFailed"); return CareCode.SettlementUnavailable; }
            return ChargeAndMarkFacts(patient, grant, amount, now);
        }

        /// <summary>U6-A6（2026-10-09）：**事件驱动感知失效**接线 —— 真实结算落地后，受益人的事实
        /// 已经变了（照护/诊疗改变其需求与身体），应立即重采，而不是等下一个轮询窗口。
        /// 只标 `Needs|Body`：这两个域确实被该事件改变；标 `All` 会让一次照护触发全量重采（浪费）。
        /// `MarkPerceptionDirty` 是 owner-thread only、对未登记 Pawn 为 no-op —— 这里不吞异常：
        /// 若有人在非主线程调用本方法，那本身就是要修的缺陷，掩盖它比报错更危险。</summary>
        private CareCode ChargeAndMarkFacts(Pawn patient, CareAuthorization grant, long amount, int now)
        {
            var charged = Care.Charge(grant.AuthorizationId, grant.PayerKey, amount, now);
            if (charged == CareCode.Applied)
                MarkPerceptionDirty(patient.GetUniqueLoadID(), PerceptionGroups.Needs | PerceptionGroups.Body);
            return charged;
        }
        /// <summary>Capability comes from named facts, never from an assertion (U6-B1): the evidence face
        /// is a startup probe per kind, the executor gate is a real pawn-capacity reading, and the
        /// settlement/recovery gates read the ledger itself. The old implementation asserted the last three
        /// with a const and two literal `true`s — those could never fail, so they proved nothing.</summary>
        internal CareCapabilityResult RefreshServiceCapability(ServiceKind kind, Pawn context)
        {
            var readiness = ServiceReadiness.FromFacts(kind,
                evidence: PersonalChronicle.Capture.ServiceProcessProbes.StatusOf(kind),
                executorCapable: ExecutorCapable(context),
                pricePublished: QueryServicePriceMinor(context?.Map?.uniqueID ?? 0) != null,
                ledgerReadable: LedgerReadable());
            return EvaluateServiceCapability(kind, readiness);
        }

        /// <summary>该类型的原版过程核实状态（U6-B1，只读）。</summary>
        internal ServiceEvidenceStatus ServiceEvidenceStatusOf(ServiceKind kind)
            => PersonalChronicle.Capture.ServiceProcessProbes.StatusOf(kind);

        /// <summary>U6-A4：照护授权到期裁剪（250 tick 调用点）。</summary>
        internal int TrimCareAuthorizations(int now) => Care.TrimExpired(now);

        /// <summary>U6-A4：非 CaptureWired 服务类型的**周期复核** —— 让"过程在·摄取未接 / 原版无过程"
        /// 的诚实状态真正落到能力表上（此前只有 Treatment 在 tend 时被评估，其余四类永不推进，
        /// 能力表长期停在 PublicVanilla 而屏上证据条却在说"过程在"）。
        /// ⚠ 刻意**跳过 CaptureWired 类型**：它们的容量门需要真实执行者上下文，无上下文的批量评估会把
        /// 已证成的 ReadyPaid 降级 —— 那是"用代理值放行/降级"的反面错误，宁可少评估也不误伤。</summary>
        internal int RefreshUnwiredServiceCapabilities()
        {
            int evaluated = 0;
            for (int i = 0; i < ServiceSweepKinds.Length; i++)
            {
                var kind = ServiceSweepKinds[i];
                if (PersonalChronicle.Capture.ServiceProcessProbes.StatusOf(kind) == ServiceEvidenceStatus.CaptureWired) continue;
                RefreshServiceCapability(kind, null);
                evaluated++;
            }
            return evaluated;
        }
        private static readonly ServiceKind[] ServiceSweepKinds =
        { ServiceKind.Examination, ServiceKind.Bed, ServiceKind.Treatment, ServiceKind.Childcare, ServiceKind.Education };

        /// <summary>执行者真实能力读值：null / 死亡 / 倒地 / 无法操作 ⇒ 不能执行 ⇒ 容量门不成立。
        /// 旧实现用 `context != null` 代理，等于把"有个人"当"有能力"。</summary>
        private static bool ExecutorCapable(Pawn pawn)
            => pawn != null && !pawn.Dead && !pawn.Downed && pawn.health?.capacities != null
                && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)
                && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);

        /// <summary>结算与中断恢复的真实来源：结算账本本身可读且无故障（null / Fault ⇒ false，绝不假设）。</summary>
        private bool LedgerReadable()
        {
            var audit = settlement.ReadAudit();
            return audit != null && audit.Fault == null;
        }
    }
}

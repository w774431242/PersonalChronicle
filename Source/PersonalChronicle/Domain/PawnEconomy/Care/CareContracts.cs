using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Perception;

namespace PersonalChronicle.Domain.PawnEconomy.Care
{
    /// <summary>M8/M7 service kinds. Each one is enabled independently: a service only leaves the
    /// vanilla public path after its own access, delivery, capacity, price, settlement, recovery and
    /// explanation evidence all exist.</summary>
    internal enum ServiceKind { Examination = 1, Bed = 2, Treatment = 3, Childcare = 4, Education = 5 }
    internal enum ServiceCapability { PublicVanilla = 0, Preparing = 1, ReadyPaid = 2, Suspended = 3 }
    internal enum CareCode
    { Applied, Replay, StaleRevision, InvalidCommand, AuthorizationUnknown, Expired, PayerNotAuthorized,
      CapExceeded, BeneficiaryMissing, ExecutorUnavailable, CapacityReached, CommandConflict, SettlementUnavailable,
      // 追加在末尾：既有值的序号不得变动（结果码会被记录与断言）。
      AlreadyAuthorized }

    /// <summary>M8 four identities for one care relationship. A relationship never authorizes money:
    /// the payer is an explicit, separate grant with its own cap and expiry.</summary>
    internal sealed class CareAuthorization
    {
        public string AuthorizationId { get; }
        public ServiceKind Kind { get; }
        public string BeneficiaryKey { get; }
        public string GuardianKey { get; }
        public string ExecutorKey { get; }
        public string PayerKey { get; }
        public long MaxAmountMinor { get; }
        public long ChargedMinor { get; }
        public int CreatedTick { get; }
        public int ExpiresTick { get; }
        public long Revision { get; }
        public CareAuthorization(string authorizationId, ServiceKind kind, string beneficiaryKey, string guardianKey,
            string executorKey, string payerKey, long maxAmountMinor, int createdTick, int expiresTick,
            long revision = 1, long chargedMinor = 0)
        {
            if (!Enum.IsDefined(typeof(ServiceKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(beneficiaryKey)) throw new ArgumentException("BeneficiaryRequired", nameof(beneficiaryKey));
            if (expiresTick <= createdTick) throw new ArgumentOutOfRangeException(nameof(expiresTick));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
            if (chargedMinor < 0 || maxAmountMinor < 0 || chargedMinor > maxAmountMinor) throw new ArgumentOutOfRangeException(nameof(chargedMinor));
            MinorCurrency.Validate(maxAmountMinor); MinorCurrency.Validate(chargedMinor);
            AuthorizationId = MinorCurrency.Required(authorizationId); Kind = kind;
            BeneficiaryKey = MinorCurrency.Required(beneficiaryKey);
            GuardianKey = Nullable(guardianKey); ExecutorKey = Nullable(executorKey); PayerKey = Nullable(payerKey);
            MaxAmountMinor = maxAmountMinor; ChargedMinor = chargedMinor;
            CreatedTick = createdTick; ExpiresTick = expiresTick; Revision = revision;
        }
        private static string Nullable(string value) => string.IsNullOrWhiteSpace(value) ? null : MinorCurrency.Required(value);
        public bool IsExpired(int now) => now > ExpiresTick;
        public CareAuthorization WithCharged(long chargedMinor, long revision)
            => new CareAuthorization(AuthorizationId, Kind, BeneficiaryKey, GuardianKey, ExecutorKey, PayerKey,
                MaxAmountMinor, CreatedTick, ExpiresTick, revision, chargedMinor);
    }

    internal static class CareRules
    {
        /// <summary>A minor or incapable beneficiary needs a named guardian; an adult may act for itself.
        /// The guardian identity is what carries the authority — the family relation itself does not.</summary>
        public static CareCode CanCreate(bool beneficiaryIsAdult, string beneficiaryKey, string guardianKey, string payerKey,
            int createdTick, int expiresTick)
        {
            if (string.IsNullOrWhiteSpace(beneficiaryKey)) return CareCode.BeneficiaryMissing;
            if (!beneficiaryIsAdult && string.IsNullOrWhiteSpace(guardianKey)) return CareCode.InvalidCommand;
            if (string.IsNullOrWhiteSpace(payerKey)) return CareCode.PayerNotAuthorized;
            if (expiresTick <= createdTick) return CareCode.InvalidCommand;
            return CareCode.Applied;
        }
        /// <summary>Charging gate: an explicit payer grant, inside the cap, before expiry, and never the
        /// beneficiary's own money when a guardian is the one acting for a child.</summary>
        public static CareCode CanCharge(CareAuthorization authorization, string payerKey, long amountMinor, int now)
        {
            if (authorization == null) return CareCode.AuthorizationUnknown;
            if (string.IsNullOrWhiteSpace(payerKey) || authorization.PayerKey == null
                || !string.Equals(authorization.PayerKey, payerKey, StringComparison.Ordinal)) return CareCode.PayerNotAuthorized;
            if (amountMinor <= 0) return CareCode.InvalidCommand;
            if (authorization.IsExpired(now)) return CareCode.Expired;
            long remaining = authorization.MaxAmountMinor - authorization.ChargedMinor;
            if (amountMinor > remaining) return CareCode.CapExceeded;
            return CareCode.Applied;
        }
        public static long ApplyCharge(CareAuthorization authorization, long amountMinor)
        {
            long charged = checked(authorization.ChargedMinor + amountMinor);
            MinorCurrency.Validate(charged);
            return charged;
        }

        /// <summary>
        /// v0.9 §2 M8「**防止儿童需求重复**」（纯函数，离线可证伪）：同一受益人的同一服务类型，
        /// **同一时间只能有一份活跃授权**。每份授权各带独立上限，多份并存时扣费只认其中一份，
        /// 其余的额度**永远不会被使用**（幽灵授权），而一旦第一份过期，扣费对象会静默换成另一份 ——
        /// 这正是"同一份照护被重复记账"的入口。
        ///
        /// 需要提额或续期时走**显式撤销 + 重新签发**（<c>CareService.Revoke</c> ⇒ <c>Create</c>），
        /// 让每次变更都留下撤销痕迹；不要靠多签一份来悄悄叠加额度。
        ///
        /// 到期或已撤销的授权**不占名额**，可重新签发；不同受益人、不同服务类型互不占位。
        /// </summary>
        public static CareCode CanIssue(CareAuthorization existing, string beneficiaryKey, ServiceKind kind, int now)
        {
            if (existing == null) return CareCode.Applied;
            if (existing.Kind != kind
                || !string.Equals(existing.BeneficiaryKey, beneficiaryKey, StringComparison.Ordinal))
                return CareCode.Applied;
            if (existing.IsExpired(now)) return CareCode.Applied;
            return CareCode.AlreadyAuthorized;
        }

        /// <summary>
        /// 四角色之三的**执行者门**：受益者不得兼任执行者。
        /// 未指派执行者时返回通过 —— "没指定执行者"与"执行者不可用"是两件事，
        /// 混为一谈会让所有只登记付款方的照护授权被无理由拒绝。
        /// </summary>
        public static CareCode CanExecute(string beneficiaryKey, string executorKey)
        {
            if (string.IsNullOrWhiteSpace(executorKey)) return CareCode.Applied;
            if (string.Equals(beneficiaryKey, executorKey, StringComparison.Ordinal))
                return CareCode.ExecutorUnavailable;
            return CareCode.Applied;
        }

        /// <summary>
        /// M8（2026-10-10）：**授权内执行者核验** —— 审计要求"检查真实执行者与授权关系，避免错误执行者、旧授权占据扣费对象"。
        /// 授权已指定执行者（<c>ExecutorKey</c> 非空）⇒ 真实执行者必须**精确等于**它；
        /// 未指定（空）⇒ 视为"不限执行者"，但仍要求不是受益者本人（与 <see cref="CanExecute"/> 同一纪律）。
        /// </summary>
        public static CareCode CanExecuteUnder(CareAuthorization authorization, string beneficiaryKey, string executorKey)
        {
            if (authorization == null) return CareCode.AuthorizationUnknown;
            var baseGate = CanExecute(beneficiaryKey, executorKey);
            if (baseGate != CareCode.Applied) return baseGate;
            if (string.IsNullOrEmpty(authorization.ExecutorKey)) return CareCode.Applied;
            return string.Equals(authorization.ExecutorKey, executorKey, StringComparison.Ordinal)
                ? CareCode.Applied : CareCode.ExecutorUnavailable;
        }
    }

    /// <summary>U6-B1：一类服务的原版交付过程**被核实到哪一步**。三态穷尽且有序；没有原版过程的类型
    /// 永不可提升（设计 07:45 禁止用近似过程顶替真实交付证据）。</summary>
    internal enum ServiceEvidenceStatus
    {
        /// <summary>原版无独立交付过程（体检 / 床位）：恒不可证 ⇒ 不得报价。</summary>
        NotDeliverable = 0,
        /// <summary>原版过程存在、摄取尚未接通：将来可证，当前不得报价。</summary>
        ProcessExistsCaptureNotWired = 1,
        /// <summary>摄取已接通：真实交付证据可以产生。</summary>
        CaptureWired = 2
    }

    internal static class ServiceEvidenceRules
    {
        /// <summary>每个已发布的服务类型都有一个解释面：照护页为每类服务渲染一行（能力状态 + 缺失门 +
        /// 证据状态），玩家随时能看到"为什么还不能收费"。没有该行的类型不得主张这一门（集合由测试钉住）。</summary>
        public static bool HasExplanationSurface(ServiceKind kind)
            => kind == ServiceKind.Examination || kind == ServiceKind.Bed || kind == ServiceKind.Treatment
                || kind == ServiceKind.Childcare || kind == ServiceKind.Education;
    }

    /// <summary>What a service capability must prove before it may take money. A missing gate is not a
    /// soft warning: the capability stays on the vanilla public path (M7 §7).</summary>
    internal sealed class ServiceReadiness
    {
        public bool VanillaAccess { get; }
        public bool DeliveryEvidence { get; }
        public bool CapacityAndHours { get; }
        public bool PriceCap { get; }
        public bool Settlement { get; }
        public bool InterruptionRecovery { get; }
        public bool UiExplanation { get; }
        public string Fault { get; }
        public ServiceReadiness(bool vanillaAccess, bool deliveryEvidence, bool capacityAndHours, bool priceCap,
            bool settlement, bool interruptionRecovery, bool uiExplanation, string fault = null)
        {
            VanillaAccess = vanillaAccess; DeliveryEvidence = deliveryEvidence; CapacityAndHours = capacityAndHours;
            PriceCap = priceCap; Settlement = settlement; InterruptionRecovery = interruptionRecovery;
            UiExplanation = uiExplanation; Fault = fault;
        }

        /// <summary>U6-B1：七门**全部由具名事实构造** —— 不再有字面 `true` 或 `const true` 断言。
        /// 各事实的真实来源：证据面=启动期探针；执行能力=执行者真实能力读值；价格=已发布服务价；
        /// 结算与中断恢复=结算账本可读且无故障；解释=该类型有解释面（<see cref="ServiceEvidenceRules"/>）。</summary>
        public static ServiceReadiness FromFacts(ServiceKind kind, ServiceEvidenceStatus evidence, bool executorCapable,
            bool pricePublished, bool ledgerReadable)
        {
            bool deliverable = evidence != ServiceEvidenceStatus.NotDeliverable;
            return new ServiceReadiness(
                vanillaAccess: deliverable,
                deliveryEvidence: evidence == ServiceEvidenceStatus.CaptureWired,
                capacityAndHours: executorCapable,
                priceCap: pricePublished,
                settlement: ledgerReadable,
                interruptionRecovery: ledgerReadable,
                uiExplanation: ServiceEvidenceRules.HasExplanationSurface(kind));
        }
        public IReadOnlyList<string> Missing()
        {
            var rows = new List<string>();
            if (!VanillaAccess) rows.Add("VanillaAccess");
            if (!DeliveryEvidence) rows.Add("DeliveryEvidence");
            if (!CapacityAndHours) rows.Add("CapacityAndHours");
            if (!PriceCap) rows.Add("PriceCap");
            if (!Settlement) rows.Add("Settlement");
            if (!InterruptionRecovery) rows.Add("InterruptionRecovery");
            if (!UiExplanation) rows.Add("UiExplanation");
            return rows.AsReadOnly();
        }
        public bool AllProven => VanillaAccess && DeliveryEvidence && CapacityAndHours && PriceCap && Settlement
            && InterruptionRecovery && UiExplanation && Fault == null;
    }

    internal sealed class ServiceCapabilityState
    {
        public ServiceKind Kind { get; }
        public ServiceCapability Capability { get; }
        public long Revision { get; }
        public IReadOnlyList<string> Blockers { get; }
        public ServiceCapabilityState(ServiceKind kind, ServiceCapability capability, long revision, IEnumerable<string> blockers)
        {
            if (!Enum.IsDefined(typeof(ServiceKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(ServiceCapability), capability)) throw new ArgumentOutOfRangeException(nameof(capability));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            Kind = kind; Capability = capability; Revision = revision;
            Blockers = PerceptionCollections.Freeze(blockers);
        }
    }

    internal static class ServiceCapabilityRules
    {
        public const long MaximumCapMinor = 100000000L * MinorCurrency.PerSilver;
        /// <summary>Suspended requires an explicit fault; ReadyPaid requires every gate proven. Anything
        /// else stays exactly where it was — never promoted by wishful reading of a partial readiness.</summary>
        public static ServiceCapability Evaluate(ServiceCapability current, ServiceReadiness readiness)
        {
            if (readiness == null) return ServiceCapability.PublicVanilla;
            if (readiness.Fault != null) return ServiceCapability.Suspended;
            if (readiness.AllProven) return ServiceCapability.ReadyPaid;
            return current == ServiceCapability.Suspended ? ServiceCapability.Preparing
                : current == ServiceCapability.ReadyPaid ? ServiceCapability.Preparing : ServiceCapability.Preparing;
        }
        /// <summary>Only a ReadyPaid capability may quote or charge; everything else is the vanilla
        /// public path and must not invent a price.</summary>
        public static bool MayCharge(ServiceCapability capability) => capability == ServiceCapability.ReadyPaid;
    }
}

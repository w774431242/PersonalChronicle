using PersonalChronicle.Domain.PawnEconomy.Employment;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// CHG-20261011-LABOR-VISUAL：**稳定 ID → 玩家可读文本**的唯一映射点（GOV-009/GOV-010）。
    ///
    /// <para>纪律：① 全部走整键回落（缺键显示稳定 ID，绝不空白、绝不裸键上屏）；
    /// ② 阶段/叠加/原因/能力各自一个键族，**不拼字符串猜语义**；
    /// ③ 本类只做映射，不读账本、不改状态（可在 OnGUI 与检查文本中安全调用）。</para>
    /// </summary>
    internal static class LaborVisualText
    {
        private const string Prefix = "PC_PEA.LaborVisual.";
        private static string T(string key) => EconomyLabels.TranslatedOrId(key, key.Substring("PC_PEA.".Length));

        /// <summary>流程阶段标签（12 阶段全覆盖；None 也有键，不留空）。</summary>
        public static string StageLabel(LaborFlowStage stage)
        {
            switch (stage)
            {
                case LaborFlowStage.IntentReady: return T(Prefix + "Stage.IntentReady");
                case LaborFlowStage.GoingToLaborOffice: return T(Prefix + "Stage.GoingToLaborOffice");
                case LaborFlowStage.QueuedAtLaborOffice: return T(Prefix + "Stage.QueuedAtLaborOffice");
                case LaborFlowStage.Negotiating: return T(Prefix + "Stage.Negotiating");
                case LaborFlowStage.IssuingDocument: return T(Prefix + "Stage.IssuingDocument");
                case LaborFlowStage.Working: return T(Prefix + "Stage.Working");
                case LaborFlowStage.CollectingOutputs: return T(Prefix + "Stage.CollectingOutputs");
                case LaborFlowStage.GoingToSettlement: return T(Prefix + "Stage.GoingToSettlement");
                case LaborFlowStage.VerifyingSubmission: return T(Prefix + "Stage.VerifyingSubmission");
                case LaborFlowStage.AwaitingPayment: return T(Prefix + "Stage.AwaitingPayment");
                case LaborFlowStage.PaymentConfirmed: return T(Prefix + "Stage.PaymentConfirmed");
                case LaborFlowStage.Archived: return T(Prefix + "Stage.Archived");
                default: return T(Prefix + "Stage.None");
            }
        }

        /// <summary>叠加状态标签（含"无"）。</summary>
        public static string HoldLabel(FlowHold hold)
        {
            switch (hold)
            {
                case FlowHold.Paused: return T(Prefix + "Hold.Paused");
                case FlowHold.Blocked: return T(Prefix + "Hold.Blocked");
                case FlowHold.ReconciliationRequired: return T(Prefix + "Hold.Reconciliation");
                default: return T(Prefix + "Hold.None");
            }
        }

        /// <summary>稳定原因码 → 标签（未知码原样显示 —— 绝不吞掉排查信息）。</summary>
        public static string ReasonLabel(string reasonCode)
        {
            if (string.IsNullOrEmpty(reasonCode)) return T(Prefix + "Reason.None");
            string key = Prefix + "Reason." + reasonCode;
            string text = EconomyLabels.TranslatedOrId(key, reasonCode);
            return text;
        }

        /// <summary>稳定 next-action ID → 玩家可读的下一步（"去哪/做什么"）。</summary>
        public static string NextActionLabel(string nextAction)
        {
            if (string.IsNullOrEmpty(nextAction)) return string.Empty;
            return EconomyLabels.TranslatedOrId(Prefix + "Next." + nextAction, nextAction);
        }

        /// <summary>交付能力标签（能力矩阵的呈现面；Unsupported 如实标注）。</summary>
        public static string CapabilityLabel(DeliveryCapability capability)
        {
            switch (capability)
            {
                case DeliveryCapability.CarryToCounter: return T(Prefix + "Capability.CarryToCounter");
                case DeliveryCapability.AlreadyAccepted: return T(Prefix + "Capability.AlreadyAccepted");
                case DeliveryCapability.EvidenceOnly: return T(Prefix + "Capability.EvidenceOnly");
                default: return T(Prefix + "Capability.Unsupported");
            }
        }

        /// <summary>付款模式标签（旧档 LegacyImmediate 与新模式并存时玩家必须能分辨）。</summary>
        public static string ModeLabel(SettlementDeliveryMode mode)
        {
            switch (mode)
            {
                case SettlementDeliveryMode.PhysicalSubmission: return T(Prefix + "Mode.PhysicalSubmission");
                case SettlementDeliveryMode.LegacyImmediate: return T(Prefix + "Mode.LegacyImmediate");
                default: return T(Prefix + "Mode.Unknown");
            }
        }

        /// <summary>付款资格标签（"已赚取/待交验"与"已到账"必须可区分 —— REQ-LaborVisual-018）。</summary>
        public static string EligibilityLabel(WageEligibility eligibility)
        {
            switch (eligibility)
            {
                case WageEligibility.Immediate: return T(Prefix + "Eligibility.Immediate");
                case WageEligibility.PendingSubmission: return T(Prefix + "Eligibility.PendingSubmission");
                case WageEligibility.Submitted: return T(Prefix + "Eligibility.Submitted");
                case WageEligibility.Blocked: return T(Prefix + "Eligibility.Blocked");
                default: return T(Prefix + "Eligibility.Unknown");
            }
        }
    }
}

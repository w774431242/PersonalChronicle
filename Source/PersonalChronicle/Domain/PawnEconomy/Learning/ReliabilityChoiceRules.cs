namespace PersonalChronicle.Domain.PawnEconomy.Learning
{
    /// <summary>
    /// v0.9 §2 M9 的**可靠性取舍**（纯函数，离线可证伪）。
    ///
    /// 出处：《02-长期生活规划_代码设计方案》§3 ——
    /// `| ReliabilityPreference | 对到账延误、排队、失败证据的敏感度 | 在不确定工资/服务与稳定方案之间作**有限**取舍 |`
    ///
    /// 本条把该维**接进真实决策**：`WageCredibility.IsLowConfidence`（样本足够**且**记录差）判定的机构
    /// 会被高可靠性偏好的 pawn 跳过。三条边界必须同时成立，缺一条就会走偏：
    /// ① **证据不足不构成避开理由** —— 不得把"没有证据"当成"不可靠"；
    /// ② **取舍是有限的** —— 只改变"在多个可行候选之间选哪个"，不改变"是否存在可行候选"，
    ///    无替代时必须仍签约，否则会以"求稳"之名放弃收入；
    /// ③ **中性偏好不改变选择** —— 设计原文："0.5 只是显式中性默认，不是已测量人格"。
    /// </summary>
    internal static class ReliabilityChoiceRules
    {
        /// <summary>显式中性默认：偏好不高于此则不因可信度改变选择。</summary>
        public const float NeutralPreference = .5f;

        /// <summary>
        /// 是否因可信度而**避开**该候选。<paramref name="isLowConfidence"/> 必须来自
        /// <c>WageCredibility.IsLowConfidence</c>（已内含样本量门槛），不得由调用方自行推断。
        /// </summary>
        public static bool Avoid(float reliabilityPreference, bool isLowConfidence)
            => isLowConfidence && reliabilityPreference > NeutralPreference;

        /// <summary>
        /// 取舍的**有限性**：仅当存在**另一个候选**时才切换。
        /// 无替代时返回 false ⇒ 调用方仍签约，只以原因码标明"已知风险"。
        /// </summary>
        public static bool ShouldSwitchToAlternative(float reliabilityPreference, bool currentIsLowConfidence,
            bool alternativeAvailable)
            => alternativeAvailable && Avoid(reliabilityPreference, currentIsLowConfidence);
    }
}

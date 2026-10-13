using System;

namespace PersonalChronicle.Domain.PawnEconomy.Learning
{
    /// <summary>
    /// §3 **带显式先验的 Beta(1,1)** 可信度估计（纯函数，离线可证伪）。
    ///
    /// 出处：《09-经验反馈与失败恢复_代码设计方案》§3 原文：
    /// "及时交付/到账可信度可用带**显式先验**的 Beta(1,1) 作为原型估计，**显示先验/样本数**：
    ///  `mean=(1+onTime)/(2+knownOutcomes)`。**只在结果和截止时间均已知时更新；未知不记失败**。
    ///  此分数不是实际成功概率证明，不能用于生命安全保证或压掉原版风险。"
    ///
    /// **显式先验的意义**：样本为 0 时读数恰为 0.5（中性），而不是 0 —— 裸成功率会把"还没证据"
    /// 显示成"极不可靠"，那正是设计禁止的"供应方未知 ⇒ 显示证据不足，不记失败或满信誉"。
    /// </summary>
    internal readonly struct CredibilityEstimate
    {
        /// <summary>Beta(1,1) 的伪计数：一次"及时"与一次"总共"。</summary>
        public const int PriorOnTime = 1;
        public const int PriorTotal = 2;

        public int OnTime { get; }
        public int KnownOutcomes { get; }

        public CredibilityEstimate(int onTime, int knownOutcomes)
        {
            // 未知/非法输入一律归零样本，读数落回中性 0.5，绝不因脏数据给出极端判断。
            if (onTime < 0 || knownOutcomes < 0 || onTime > knownOutcomes)
            { OnTime = 0; KnownOutcomes = 0; return; }
            OnTime = onTime; KnownOutcomes = knownOutcomes;
        }

        /// <summary><c>mean = (1+onTime)/(2+knownOutcomes)</c>；样本为 0 时恒为 0.5。</summary>
        public float Mean => (float)(PriorOnTime + OnTime) / (PriorTotal + KnownOutcomes);

        /// <summary>先验在读数中的占比（1 = 全是先验）。供展示"先验/样本数"，样本越多先验越淡。</summary>
        public float PriorWeight => (float)PriorTotal / (PriorTotal + KnownOutcomes);

        /// <summary>折算为万分比，供既有按基点分档的口径直接消费。</summary>
        public int BasisPoints => (int)Math.Round(10000d * Mean);
    }

    /// <summary>
    /// §3 **有界平滑摘要**（纯函数）：新观测耗时与旧估计在**已标明样本/场景**内融合，原型 <c>alpha=0.20</c>。
    ///
    /// 与"忘记"的纪律配套（§3）："遗忘使用有限窗口/权重，不能反复 Tick 衰减造成同一事件被重复学习" ——
    /// 故本函数只做**显式投递**时的一次融合，**没有**任何按时间自动衰减的入口。
    /// </summary>
    internal static class DurationSmoothing
    {
        /// <summary>原型融合系数（§3 明列参数）。</summary>
        public const float Alpha = .20f;

        /// <summary>
        /// 把一次新观测耗时融进旧估计。无旧估计（<paramref name="knownSamples"/> 为 0）时**直接采用**观测值 ——
        /// 向一个不存在的"旧估计"融合会让首次读数被 0 拉偏。
        /// </summary>
        public static float Blend(float previousEstimate, int knownSamples, float observed)
            => knownSamples <= 0 ? observed : previousEstimate + Alpha * (observed - previousEstimate);

        /// <summary>
        /// 是否样本太少、应显示"证据不足"而不是把平均值当准点保证（§3："样本少显示 LowEvidence"）。
        /// 门槛由调用方给出 —— 设计未规定具体数值，**不自行发明一个数冒充权威参数**。
        /// </summary>
        public static bool IsLowEvidence(int knownSamples, int minimumSamples) => knownSamples < minimumSamples;
    }
}

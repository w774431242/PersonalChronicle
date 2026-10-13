using System;

namespace PersonalChronicle.Domain.PawnEconomy.Learning
{
    /// <summary>M9 慢速学习的判决码。<see cref="Applied"/> 之外一律**不写回**。</summary>
    internal enum StyleLearningCode
    { Applied, DimensionDisabled, ReplaySummary, TimeWentBackwards, DailyBudgetExhausted, NoEvidence }

    /// <summary>
    /// v0.9 §2 M9 的**有界慢速学习**（纯函数，离线可证伪）。
    ///
    /// 出处：《02-长期生活规划_代码设计方案》§3「慢速更新与即时情境分开」——
    /// 稳定向量 <c>p = clamp01(base + learnedOffset)</c>；只有 M9 的**可靠、有界、多次观察摘要**更新
    /// <c>learnedOffset</c>；原型**每日最大绝对变化 0.03**、单个摘要**融合系数 0.10**；
    /// 用**未分配的每日变化额度限幅**；**同一摘要 ID 只处理一次**；
    /// **时间回退/读档不能重获同一日额度**；**未经校准的学习维度可先禁用**。
    ///
    /// 本类**不持有状态**：账目由 <see cref="StyleLedger"/> 持有、由调用方持久化，这里只判决
    /// "这一次能不能学、最多能学多少"。这样"只进不退的账"落在账本里，规则本身可离线证伪。
    ///
    /// 与**即时情境**的分工（同节）：怀孕、受伤、实际欠薪、照护变更立即改近期计划，
    /// **不等待**人格慢调；一次事故也不永久改变风格 —— 本条只负责后者。
    /// </summary>
    internal static class StyleLearningRules
    {
        /// <summary>原型每日最大绝对变化（超限即拒，不按比例缩水到刚好用完）。</summary>
        public const float MaximumDailyChange = .03f;
        /// <summary>单个摘要的融合系数：证据不会一次把维度拉到位。</summary>
        public const float SummaryFusion = .10f;

        /// <summary>
        /// 判决一次摘要能否更新某一维，并给出**本次允许的变化量**。
        ///
        /// <paramref name="observedMinusBase"/> 是"观察到的水平 − 该维基线"（正=比基线更偏好）。
        /// 变化量取 <c>融合系数 × 观察差</c>，再按**当日剩余额度**双向限幅。
        /// 限额耗尽时返回 <see cref="StyleLearningCode.DailyBudgetExhausted"/> 且变化量为 0 ——
        /// **不得**缩水成"刚好用完额度"再写回，那会让同一份证据被反复平滑。
        /// </summary>
        public static StyleLearningCode Plan(bool dimensionEnabled, bool summaryAlreadyProcessed,
            bool timeWentBackwards, float usedToday, float observedMinusBase, out float delta)
        {
            delta = 0f;
            // 未经校准的维度可先禁用：宁可不动，也不要用未校准的规则改人格。
            if (!dimensionEnabled) return StyleLearningCode.DimensionDisabled;
            // 同一摘要 ID 只处理一次：重复投递同一条经历不得产生额外漂移。
            if (summaryAlreadyProcessed) return StyleLearningCode.ReplaySummary;
            // 时间回退/读档不能重获同一日额度。
            if (timeWentBackwards) return StyleLearningCode.TimeWentBackwards;
            if (float.IsNaN(observedMinusBase) || float.IsInfinity(observedMinusBase)) return StyleLearningCode.NoEvidence;
            float remaining = MaximumDailyChange - Math.Max(0f, usedToday);
            if (remaining <= 0f) return StyleLearningCode.DailyBudgetExhausted;
            float desired = SummaryFusion * observedMinusBase;
            if (desired > remaining) desired = remaining;
            if (desired < -remaining) desired = -remaining;
            if (desired == 0f) return StyleLearningCode.NoEvidence;
            delta = desired;
            return StyleLearningCode.Applied;
        }

        /// <summary>
        /// 把变化量写进 <c>learnedOffset</c>，并保证 <c>p = clamp01(base + offset)</c> 落在 0–1。
        /// 钳制作用在**合成结果**上而不是偏移上：偏移可以超出 0–1 表示强偏好，
        /// 但人格向量本身永远不越界（设计："所有维度取 0–1"）。
        /// </summary>
        public static float ApplyOffset(float baseValue, float learnedOffset, float delta)
        {
            float next = learnedOffset + delta;
            float total = baseValue + next;
            if (total > 1f) next = 1f - baseValue;
            if (total < 0f) next = -baseValue;
            return next;
        }

        /// <summary>合成后的维度值 <c>p = clamp01(base + learnedOffset)</c>。</summary>
        public static float Effective(float baseValue, float learnedOffset)
        {
            float total = baseValue + learnedOffset;
            return total < 0f ? 0f : total > 1f ? 1f : total;
        }
    }
}

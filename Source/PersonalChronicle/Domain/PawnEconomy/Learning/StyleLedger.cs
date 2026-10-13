using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain.PawnEconomy.Learning
{
    /// <summary>
    /// v0.9 §2 M9 的**学习账本**：一个 Pawn 的每维 <c>learnedOffset</c>、当日已用额度、已处理过的摘要 id。
    ///
    /// 它是 <see cref="StyleLearningRules"/> 的**持账方** —— 规则本身不持有状态，
    /// 由本类承担"只进不退的账"：额度按维度累计、摘要按 id 去重。
    ///
    /// **本类不落盘**：持久化由调用方（Economy 层）负责。若落盘缺失，损失的是"人格随经历的微调"，
    /// 不是任何权利或金额 —— 与 <see cref="WageCredibility"/> 同级的规划提示，绝不是支付凭证。
    /// </summary>
    internal sealed class StyleLedger
    {
        /// <summary>八维风格的上界（设计："所有维度取 0–1、独立表达"）。</summary>
        public const int MaximumDimensions = 8;

        private readonly Dictionary<string, float> offsets = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> usedToday = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly HashSet<string> processed = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, float> Offsets => offsets;
        public int ProcessedSummaries => processed.Count;

        /// <summary>
        /// 投递一条经历摘要：判决 → 限幅 → 写 offset。
        /// 只有 <see cref="StyleLearningCode.Applied"/> 才把摘要记为已处理 —— 被拒的摘要
        /// **不消耗**它的唯一性（否则额度次日恢复后，同一条证据将永久无法生效）。
        /// </summary>
        public StyleLearningCode Deliver(string dimension, string summaryId, float observedMinusBase,
            float baseValue, bool dimensionEnabled)
        {
            if (string.IsNullOrWhiteSpace(dimension) || string.IsNullOrWhiteSpace(summaryId))
                return StyleLearningCode.NoEvidence;
            if (processed.Contains(summaryId)) return StyleLearningCode.ReplaySummary;
            if (!offsets.ContainsKey(dimension) && offsets.Count >= MaximumDimensions)
                return StyleLearningCode.NoEvidence;
            float used = UsedToday(dimension);
            var code = StyleLearningRules.Plan(dimensionEnabled, false, false, used, observedMinusBase, out float delta);
            if (code != StyleLearningCode.Applied) return code;
            offsets[dimension] = StyleLearningRules.ApplyOffset(baseValue, OffsetOf(dimension), delta);
            usedToday[dimension] = used + (delta > 0f ? delta : -delta);
            processed.Add(summaryId);
            return code;
        }

        /// <summary>当日额度所属的**游戏日序号**（-1 = 未知）。落盘后据此判定是否真的跨日。</summary>
        public int DayStamp { get; private set; } = -1;
        /// <summary>已处理摘要水位（供落盘导出）。</summary>
        public IReadOnlyCollection<string> ProcessedIds => processed;
        /// <summary>每维已学偏移（供落盘导出）。</summary>
        public IReadOnlyDictionary<string, float> UsedTodayByDimension => usedToday;

        /// <summary>
        /// 新的一天到来时清空当日额度。**只在新的一天真的前进时清** ——
        /// 同一天内（读档、Tick 回退）返回 false 且**不清**，这是 §6
        /// "Tick 回退不产生负等待或**重获已消费学习额度**"的落点。
        /// 传入负的日序号（时间不可信）同样不清。
        /// </summary>
        public bool BeginDay(int day)
        {
            if (day < 0) return false;
            if (DayStamp >= 0 && day <= DayStamp) return false;
            DayStamp = day;
            usedToday.Clear();
            return true;
        }

        /// <summary>
        /// 从存档**原样**恢复（不经 <see cref="BeginDay"/>，避免读档即清空额度）。
        /// 与存档长度不齐的并列项按较短者截断，绝不越界写入。
        /// </summary>
        public void Restore(int dayStamp, IEnumerable<KeyValuePair<string, float>> offsets,
            IEnumerable<KeyValuePair<string, float>> used, IEnumerable<string> processedIds)
        {
            DayStamp = dayStamp;
            offsets = offsets ?? Array.Empty<KeyValuePair<string, float>>();
            used = used ?? Array.Empty<KeyValuePair<string, float>>();
            foreach (var pair in offsets)
                if (!string.IsNullOrWhiteSpace(pair.Key)) this.offsets[pair.Key] = pair.Value;
            foreach (var pair in used)
                if (!string.IsNullOrWhiteSpace(pair.Key)) usedToday[pair.Key] = pair.Value < 0f ? 0f : pair.Value;
            foreach (string id in processedIds ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(id)) processed.Add(id);
        }

        public float UsedToday(string dimension)
            => dimension != null && usedToday.TryGetValue(dimension, out float value) ? value : 0f;

        public float OffsetOf(string dimension)
            => dimension != null && offsets.TryGetValue(dimension, out float value) ? value : 0f;
    }
}

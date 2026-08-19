using System.Collections.Generic;

namespace PersonalChronicle.Domain.Qualification
{
    /// <summary>
    /// P5 考试评分纯函数（Domain 零 Verse 依赖，NUnit 可测）。
    /// 复用 P1 ItemProduced 采集点，通过 Metadata["examId"] 关联实践考试证据（D-E1）。
    /// </summary>
    public static class ExamScoring
    {
        /// <summary>
        /// 实践考试评分（V2.0 §15）：
        ///   q   = min(ProducedCount, RequiredCount) / RequiredCount        数量达标率
        ///   qd  = 达到 MinQuality 及以上件数 / ProducedCount               品质达标率
        ///   t   = 是否在 StartedTick+TimeLimitTicks 内完成（超时 ×0.6）
        ///   Score = 100 * q * (0.5 + 0.5*qd) * (t ? 1 : 0.6)
        /// 无产出（ProducedCount=0）→ 0 分且不通过。
        /// </summary>
        public static float ScorePractical(int requiredCount, int producedCount, List<string> producedQualities,
            string minQuality, long startedTick, long timeLimitTicks, long nowTick)
        {
            if (requiredCount <= 0 || producedCount <= 0 || producedQualities == null || producedQualities.Count == 0)
            {
                return 0f;
            }
            float q = (float)System.Math.Min(producedCount, requiredCount) / requiredCount;
            int qualityMet = 0;
            int minRank = QualityRank(minQuality);
            for (int i = 0; i < producedQualities.Count; i++)
            {
                if (QualityRank(producedQualities[i]) >= minRank)
                {
                    qualityMet++;
                }
            }
            float qd = (float)qualityMet / producedQualities.Count;
            bool inTime = timeLimitTicks <= 0L || (nowTick <= startedTick + timeLimitTicks);
            float tFactor = inTime ? 1f : 0.6f;
            return 100f * q * (0.5f + 0.5f * qd) * tFactor;
        }

        /// <summary>品质档位排名（无品质=-1，Awful=0…Legendary=6，对齐 RimWorld 1.6 QualityCategory）。</summary>
        public static int QualityRank(string quality)
        {
            if (string.IsNullOrEmpty(quality)) return -1;
            switch (quality)
            {
                case "Awful": return 0;
                case "Poor": return 1;
                case "Normal": return 2;
                case "Good": return 3;
                case "Excellent": return 4;
                case "Masterwork": return 5;
                case "Legendary": return 6;
                default: return -1;
            }
        }

        /// <summary>
        /// 达到最低品质（含）的产出件数。纯函数，供实践考试"最低品质"硬门槛判定
        /// （V2.0 §15：最低品质要求是考试任务门槛，不能仅靠评分加权软性体现）。
        /// </summary>
        public static int CountAtLeast(List<string> qualities, string minQuality)
        {
            if (qualities == null || qualities.Count == 0 || string.IsNullOrEmpty(minQuality))
            {
                return 0;
            }
            int minRank = QualityRank(minQuality);
            if (minRank < 0)
            {
                return 0;
            }
            int met = 0;
            for (int i = 0; i < qualities.Count; i++)
            {
                if (QualityRank(qualities[i]) >= minRank)
                {
                    met++;
                }
            }
            return met;
        }

        /// <summary>
        /// 理论考试评分（V2.0 §16，D-E2 加权合成，无选择题 UI）：
        ///   Score = wBook*BookScore + wResearch*ResearchScore + wSkill*SkillScore + wActivity*ActivityScore
        /// 权重阶段一常量 0.4/0.3/0.2/0.1（可由调用方归一化）。各分项已为 0~100。
        /// </summary>
        public const float WBook = 0.4f;
        public const float WResearch = 0.3f;
        public const float WSkill = 0.2f;
        public const float WActivity = 0.1f;

        public static float ScoreTheory(float bookScore, float researchScore, float skillScore, float activityScore)
        {
            return WBook * Clamp100(bookScore)
                 + WResearch * Clamp100(researchScore)
                 + WSkill * Clamp100(skillScore)
                 + WActivity * Clamp100(activityScore);
        }

        /// <summary>论文质量（D-D1 之外的基础聚合，阶段一常量权重）：0.4 书 + 0.3 研究 + 0.3 专业。</summary>
        public static float ScoreThesis(float bookAvg, float researchScore, float professionalScore)
        {
            return 0.4f * Clamp100(bookAvg) + 0.3f * Clamp100(researchScore) + 0.3f * Clamp100(professionalScore);
        }

        /// <summary>答辩最终分 = ThesisQuality*0.5 + CommitteeScore*0.5。</summary>
        public static float ScoreDefense(float thesisQuality, float committeeScore)
        {
            return 0.5f * Clamp100(thesisQuality) + 0.5f * Clamp100(committeeScore);
        }

        private static float Clamp100(float v)
        {
            if (v < 0f) return 0f;
            if (v > 100f) return 100f;
            return v;
        }
    }
}

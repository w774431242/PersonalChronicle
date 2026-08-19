using System.Collections.Generic;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 二级方向判定纯函数（V2.0 §9 / P2-A §7）。Domain 层，可离线单测。
    /// 推荐 ≠ 强制：只产出 DirectionFit[] 供 UI 参考，玩家最终选择存 ProfessionalState.primaryDirection。
    /// </summary>
    public sealed class DirectionFit
    {
        /// <summary>方向 defName。</summary>
        public string DirectionDefName;

        /// <summary>方向名（Def.LabelCap，UI 解析）。</summary>
        public string DirectionLabel;

        /// <summary>综合得分 0~100。</summary>
        public float Score;

        /// <summary>技能相关度分量 0~100。</summary>
        public float SkillFit;

        /// <summary>实践适配分量 0~100。</summary>
        public float PracticeFit;

        /// <summary>实践质量分量 0~100。</summary>
        public float QualityFit;

        /// <summary>成果贡献分量 0~100。</summary>
        public float ContributionFit;

        /// <summary>专业能力分量 0~100。</summary>
        public float MasteryFit;
    }

    /// <summary>
    /// 方向评分所需的聚合输入（由 Application/UI 层从 DefDatabase 与 Pawn 状态组装，纯数据）。
    /// </summary>
    public sealed class DirectionFitInput
    {
        /// <summary>该方向的 ProfessionalSkillDef。defName 列表。</summary>
        public List<string> SkillDefNames = new List<string>();

        /// <summary>原版技能等级（skillDefName → 0~20）。</summary>
        public Dictionary<string, float> SkillLevels = new Dictionary<string, float>();

        /// <summary>方向内累计实践次数。</summary>
        public int PracticeCount;

        /// <summary>实践品质加权均值 0~100（由各品质制造次数折算）。</summary>
        public float AverageQuality;

        /// <summary>能力 XP 相对占比（abilityKey → 0~1，能力强的方向更推荐）。</summary>
        public Dictionary<string, float> AbilityShare = new Dictionary<string, float>();

        /// <summary>能力 mastery 均值 0~100。</summary>
        public float AverageMastery;

        /// <summary>方向名（用于输出，可选）。</summary>
        public string DirectionLabel;
    }

    /// <summary>
    /// DirectionScore = 技能相关度×0.35 + 实践适配×0.25 + 实践质量×0.15 + 成果贡献×0.15 + 专业能力×0.10。
    /// </summary>
    public static class ProfessionalDirectionEvaluator
    {
        private const float WSkill = 0.35f;
        private const float WPractice = 0.25f;
        private const float WQuality = 0.15f;
        private const float WContribution = 0.15f;
        private const float WMastery = 0.10f;

        /// <summary>实践归一化基准（对齐前端 practiceNorm：次数/320）。</summary>
        private const float PracticeNormBase = 320f;

        /// <summary>单方向评分。</summary>
        public static DirectionFit Evaluate(DirectionFitInput input)
        {
            var fit = new DirectionFit();
            if (input == null)
            {
                return fit;
            }

            // ① 技能相关度：方向内各技能的原版技能等级，经边际递减归一化后取均值（0~100）
            float skillFit = ComputeSkillFit(input);
            // ② 实践适配：次数/320，封顶 100
            float practiceFit = input.PracticeCount <= 0 ? 0f : System.Math.Min(100f, (float)input.PracticeCount / PracticeNormBase * 100f);
            // ③ 实践质量：品质加权均值（0~100，已由调用方折算）
            float qualityFit = Clamp100(input.AverageQuality);
            // ④ 成果贡献：能力 XP 相对占比均值（0~1 → 0~100）
            float contributionFit = 0f;
            if (input.AbilityShare != null && input.AbilityShare.Count > 0)
            {
                float sum = 0f;
                int n = 0;
                foreach (var kv in input.AbilityShare)
                {
                    sum += kv.Value;
                    n++;
                }
                contributionFit = n == 0 ? 0f : Clamp100(sum / n * 100f);
            }
            // ⑤ 专业能力：能力 mastery 均值（0~100）
            float masteryFit = Clamp100(input.AverageMastery);

            float score = skillFit * WSkill + practiceFit * WPractice
                        + qualityFit * WQuality + contributionFit * WContribution
                        + masteryFit * WMastery;

            fit.DirectionDefName = input.SkillDefNames != null && input.SkillDefNames.Count > 0 ? input.SkillDefNames[0] : null;
            fit.DirectionLabel = input.DirectionLabel;
            fit.Score = score;
            fit.SkillFit = skillFit;
            fit.PracticeFit = practiceFit;
            fit.QualityFit = qualityFit;
            fit.ContributionFit = contributionFit;
            fit.MasteryFit = masteryFit;
            return fit;
        }

        /// <summary>对多个输入评分并降序排序（供 UI 渲染）。</summary>
        public static List<DirectionFit> EvaluateAll(IEnumerable<DirectionFitInput> inputs)
        {
            var list = new List<DirectionFit>();
            if (inputs == null)
            {
                return list;
            }
            foreach (DirectionFitInput input in inputs)
            {
                DirectionFit f = Evaluate(input);
                if (f != null)
                {
                    list.Add(f);
                }
            }
            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            return list;
        }

        /// <summary>
        /// 技能相关度：对输入的原版技能等级取 skillBase 边际递减归一化均值。
        /// skillBase(L) = 100 × (1 − (1 − L/20)^1.6)。
        /// </summary>
        private static float ComputeSkillFit(DirectionFitInput input)
        {
            if (input.SkillLevels == null || input.SkillLevels.Count == 0)
            {
                return 0f;
            }
            float sum = 0f;
            int n = 0;
            foreach (var kv in input.SkillLevels)
            {
                sum += SkillBase(kv.Value);
                n++;
            }
            return n == 0 ? 0f : sum / n;
        }

        /// <summary>对齐前端 skillBase：边际递减。</summary>
        private static float SkillBase(float level)
        {
            if (level <= 0f)
            {
                return 0f;
            }
            float l = level > 20f ? 20f : level;
            return 100f * (1f - (float)System.Math.Pow(1f - l / 20f, 1.6d));
        }

        private static float Clamp100(float v)
        {
            if (v <= 0f) return 0f;
            if (v >= 100f) return 100f;
            return v;
        }
    }
}

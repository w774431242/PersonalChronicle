// 专业适配分析（第一版职业"宇宙"骨架，算法对齐 docs/UI预览/人物档案视窗/职业档案Tab预览.html
// 前端原型：MAJOR_WEIGHTS / skillBase / analyzeMajor 五维公式）。
// 定位：只提供决策依据（"最适合哪些专业、为什么、差距在哪"），不替玩家决定职业。
// 纯逻辑（零 Verse UI 依赖），输入/输出均为稳定键与数字，可离线单测。
// 输入键 = 原版 SkillDef.defName（Shooting/Melee/Construction/Mining/Cooking/Plants/
// Animals/Crafting/Artistic/Medicine/Social/Intellectual）。
using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>12 个一级专业（稳定 key，UI 经翻译键 Career.Major.&lt;key&gt; 显示）。</summary>
    public static class ProfessionalMajor
    {
        public const string Engineering = "Engineering";
        public const string Manufacturing = "Manufacturing";
        public const string Agriculture = "Agriculture";
        public const string Forestry = "Forestry";
        public const string AnimalHusbandry = "AnimalHusbandry";
        public const string Medicine = "Medicine";
        public const string Weapons = "Weapons";
        public const string Mining = "Mining";
        public const string Research = "Research";
        public const string Cooking = "Cooking";
        public const string Art = "Art";
        public const string Management = "Management";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Engineering, Manufacturing, Agriculture, Forestry, AnimalHusbandry,
            Medicine, Weapons, Mining, Research, Cooking, Art, Management
        };
    }

    /// <summary>专业适配分析输入（全部来自真实游戏事实：原版技能等级/兴趣 + 职业实践/品质分布）。</summary>
    public sealed class ProfessionalFitInput
    {
        /// <summary>原版技能等级（SkillDefName → level 0~20）。</summary>
        public Dictionary<string, int> SkillLevels = new Dictionary<string, int>();

        /// <summary>实践次数（SkillDefName → 行为计数；由 ProfessionalState.practiceCountBySkill
        /// 或 CareerData.Events 按 SkillDefName 聚合，缺失 = 0）。</summary>
        public Dictionary<string, int> Practice = new Dictionary<string, int>();

        /// <summary>兴趣（SkillDefName → 0 无 / 50 兴趣 / 100 热情）。</summary>
        public Dictionary<string, int> Passion = new Dictionary<string, int>();

        /// <summary>品质分布（QualityCategory 名 → 件数；Normal/Good/Excellent/Masterwork/Legendary）。</summary>
        public Dictionary<string, int> QualityCounts = new Dictionary<string, int>();

        /// <summary>成长潜力基础值（第一版固定样本，不塞年龄/特性避免黑箱；可后续接真实数据源）。</summary>
        public int BaseGrowth = 70;

        public int GetLevel(string skill) => SkillLevels.TryGetValue(skill, out int v) ? v : 0;
        public int GetPractice(string skill) => Practice.TryGetValue(skill, out int v) ? v : 0;
        public int GetPassion(string skill) => Passion.TryGetValue(skill, out int v) ? v : 0;
    }

    /// <summary>单专业适配结果（UI 只消费；文案经翻译键解析）。</summary>
    public sealed class ProfessionalFitResult
    {
        public string Major;
        /// <summary>综合适配分 0~100。</summary>
        public int Fit;
        /// <summary>五维分量（0~100）。</summary>
        public int SkillScore;
        public int PracticeScore;
        public int PassionScore;
        public int AchievementScore;
        public int GrowthScore;
        /// <summary>该专业权重最高的 3 个技能（SkillDefName，降序）——优势/短板判定依据。</summary>
        public List<string> TopSkills = new List<string>();
        /// <summary>优势技能（基础分 ≥ 70；SkillDefName）。</summary>
        public List<string> Pros = new List<string>();
        /// <summary>短板技能（基础分 &lt; 40；SkillDefName）。</summary>
        public List<string> Cons = new List<string>();
    }

    /// <summary>
    /// 专业适配分析器（纯函数）。
    /// 五维公式（对齐前端原型）：
    ///   ProfessionalFit = 基础能力×0.45 + 实践适配×0.25 + 兴趣×0.10 + 成果表现×0.10 + 成长潜力×0.10
    ///   基础能力 skillBase(L) = 100×(1−(1−L/20)^1.6)（边际递减）；
    ///   实践适配 practiceNorm = min(1, 次数/320)×100；
    ///   成果表现 = 全局品质代表值 × (0.5 + 0.5×实践占比)（实践越多代表性越高）。
    /// 12 专业权重矩阵每行权重和 = 100（一个原版 Skill 可进入多个专业，形成不同 CareerSkill）。
    /// </summary>
    public static class ProfessionalFitAnalyzer
    {
        /// <summary>实践次数归一化上限（对齐前端 PRACTICE_CAP）。</summary>
        public const int PracticeCap = 320;

        /// <summary>品质权重（对齐前端 QUAL_W）。</summary>
        private static readonly Dictionary<string, int> QualityWeights = new Dictionary<string, int>
        {
            { "Normal", 40 }, { "Good", 60 }, { "Excellent", 80 },
            { "Masterwork", 95 }, { "Legendary", 100 }
        };

        /// <summary>12 专业 → 技能权重矩阵（权重和 = 100；键为原版 SkillDefName）。</summary>
        private static readonly Dictionary<string, Dictionary<string, int>> MajorWeights =
            new Dictionary<string, Dictionary<string, int>>
            {
                { ProfessionalMajor.Engineering, W("Construction", 35, "Crafting", 25, "Intellectual", 20, "Mining", 10, "Artistic", 5, "Plants", 5) },
                { ProfessionalMajor.Manufacturing, W("Crafting", 50, "Intellectual", 15, "Construction", 15, "Mining", 10, "Artistic", 10) },
                { ProfessionalMajor.Agriculture, W("Plants", 60, "Animals", 15, "Intellectual", 10, "Construction", 5, "Crafting", 5, "Medical", 5) },
                { ProfessionalMajor.Forestry, W("Plants", 50, "Construction", 15, "Mining", 10, "Animals", 10, "Crafting", 10, "Intellectual", 5) },
                { ProfessionalMajor.AnimalHusbandry, W("Animals", 60, "Plants", 15, "Medical", 10, "Social", 5, "Intellectual", 5, "Crafting", 5) },
                { ProfessionalMajor.Medicine, W("Medical", 55, "Intellectual", 25, "Social", 5, "Crafting", 5, "Animals", 5, "Plants", 5) },
                { ProfessionalMajor.Weapons, W("Shooting", 30, "Crafting", 25, "Melee", 20, "Intellectual", 10, "Construction", 5, "Mining", 5, "Medical", 5) },
                { ProfessionalMajor.Mining, W("Mining", 65, "Construction", 10, "Crafting", 10, "Intellectual", 10, "Plants", 5) },
                { ProfessionalMajor.Research, W("Intellectual", 70, "Medical", 10, "Crafting", 5, "Construction", 5, "Artistic", 5, "Social", 5) },
                { ProfessionalMajor.Cooking, W("Cooking", 70, "Plants", 10, "Animals", 10, "Medical", 5, "Crafting", 5) },
                { ProfessionalMajor.Art, W("Artistic", 70, "Crafting", 15, "Social", 5, "Intellectual", 5, "Construction", 5) },
                { ProfessionalMajor.Management, W("Social", 45, "Intellectual", 25, "Medical", 5, "Crafting", 5, "Construction", 5, "Animals", 5, "Plants", 5, "Shooting", 5) }
            };

        private static Dictionary<string, int> W(params object[] kv)
        {
            Dictionary<string, int> d = new Dictionary<string, int>();
            for (int i = 0; i < kv.Length; i += 2)
            {
                d[(string)kv[i]] = (int)kv[i + 1];
            }
            return d;
        }

        /// <summary>12 专业权重矩阵（供测试/校验：每行权重和必须 = 100）。</summary>
        public static IReadOnlyDictionary<string, Dictionary<string, int>> MajorWeightsTable => MajorWeights;

        /// <summary>第一层：原版技能边际递减基础分（0~100）。</summary>
        public static int SkillBase(int level)
        {
            int l = Math.Max(0, Math.Min(20, level));
            return (int)Math.Round(100.0 * (1.0 - Math.Pow(1.0 - l / 20.0, 1.6)));
        }

        /// <summary>实践归一化（0~100）。</summary>
        public static int PracticeNorm(int count)
        {
            return (int)Math.Round(Math.Min(1.0, Math.Max(0, count) / (double)PracticeCap) * 100.0);
        }

        /// <summary>全局品质代表值（0~100；无样本 = 0）。</summary>
        public static int QualityScore(Dictionary<string, int> counts)
        {
            if (counts == null) return 0;
            double num = 0.0, den = 0.0;
            foreach (KeyValuePair<string, int> kv in counts)
            {
                if (!QualityWeights.TryGetValue(kv.Key ?? "", out int w)) continue;
                int c = Math.Max(0, kv.Value);
                num += w * c;
                den += c;
            }
            return den > 0.0 ? (int)Math.Round(num / den) : 0;
        }

        /// <summary>分析全部 12 个专业，按适配分降序返回。</summary>
        public static List<ProfessionalFitResult> Analyze(ProfessionalFitInput input)
        {
            List<ProfessionalFitResult> results = new List<ProfessionalFitResult>();
            if (input == null) return results;

            int baseQuality = QualityScore(input.QualityCounts);
            foreach (string major in ProfessionalMajor.All)
            {
                if (!MajorWeights.TryGetValue(major, out Dictionary<string, int> weights))
                {
                    continue;
                }
                results.Add(AnalyzeOne(major, weights, input, baseQuality));
            }
            results.Sort((a, b) => b.Fit.CompareTo(a.Fit));
            return results;
        }

        private static ProfessionalFitResult AnalyzeOne(string major, Dictionary<string, int> weights,
            ProfessionalFitInput input, int baseQuality)
        {
            double skill = 0.0, practice = 0.0, passion = 0.0, growth = 0.0;
            foreach (KeyValuePair<string, int> kv in weights)
            {
                double w = kv.Value / 100.0;
                string s = kv.Key;
                skill += w * SkillBase(input.GetLevel(s));
                practice += w * PracticeNorm(input.GetPractice(s));
                passion += w * input.GetPassion(s);
                growth += w * GrowthFor(input, s);
            }
            // 成果表现：全局品质按该专业实践占比加权（实践越多 → 产出越多 → 品质分布更具代表性）。
            double practiceRatio = Math.Min(1.0, practice / 100.0);
            double achievement = baseQuality * (0.5 + 0.5 * practiceRatio);
            int fit = (int)Math.Round(skill * 0.45 + practice * 0.25 + passion * 0.10
                + achievement * 0.10 + growth * 0.10);

            ProfessionalFitResult r = new ProfessionalFitResult
            {
                Major = major,
                Fit = Math.Max(0, Math.Min(100, fit)),
                SkillScore = (int)Math.Round(skill),
                PracticeScore = (int)Math.Round(practice),
                PassionScore = (int)Math.Round(passion),
                AchievementScore = (int)Math.Round(achievement),
                GrowthScore = (int)Math.Round(growth)
            };

            // 优势√ / 短板△：权重最高的 3 个技能中，基础分 ≥70 为优势，<40 为短板。
            List<string> top = new List<string>(weights.Keys);
            top.Sort((a, b) => weights[b].CompareTo(weights[a]));
            if (top.Count > 3) top = top.GetRange(0, 3);
            foreach (string s in top)
            {
                int sb = SkillBase(input.GetLevel(s));
                if (sb >= 70) r.Pros.Add(s);
                else if (sb < 40) r.Cons.Add(s);
            }
            r.TopSkills.AddRange(top);
            return r;
        }

        /// <summary>成长潜力（第一版固定样本 + 兴趣加成；不塞年龄/特性避免黑箱）。</summary>
        private static double GrowthFor(ProfessionalFitInput input, string skill)
        {
            int passion = input.GetPassion(skill);
            double bonus = passion > 0 ? (passion >= 100 ? 12.0 : 6.0) : 0.0;
            return Math.Min(100.0, Math.Max(0.0, input.BaseGrowth + bonus));
        }
    }
}

// 专业适配分析（内在禀赋主导版）。
// 设计转向（2026-09-03，PM 指示"优先 pawn 背景/特性/技能"）：
//   Fit = 内在禀赋(0.80) + 行为佐证(0.20)
//   内在 = 原版技能等级 0.40 + 背景加成 0.15 + 特性加成 0.15 + 热情 0.10
//   行为 = 实践 0.12 + 品质 0.08
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

    /// <summary>
    /// 专业适配分析输入（全部来自真实游戏事实：原版技能等级/兴趣/禁用 + 背景/特性先天加成
    /// + 职业实践/品质分布）。键一律为原版 SkillDef.defName。
    /// </summary>
    public sealed class ProfessionalFitInput
    {
        /// <summary>原版技能等级（SkillDefName → level 0~20；禁用技能不写入）。</summary>
        public Dictionary<string, int> SkillLevels = new Dictionary<string, int>();

        /// <summary>实践次数（SkillDefName → 行为计数；来源 = practiceCountBySkill 经
        /// ProfessionalSkillDef.sourceSkills 映射回原版键，缺失 = 0）。</summary>
        public Dictionary<string, int> Practice = new Dictionary<string, int>();

        /// <summary>兴趣（SkillDefName → 0 无 / 50 兴趣 / 100 热情）。</summary>
        public Dictionary<string, int> Passion = new Dictionary<string, int>();

        /// <summary>背景先天技能加成（SkillDefName → amount 合计；来自 Childhood/Adulthood
        /// BackstoryDef.skillGains，0 = 无信息）。</summary>
        public Dictionary<string, int> BackstoryGains = new Dictionary<string, int>();

        /// <summary>特性技能加成（SkillDefName → amount 合计；来自 Trait.CurrentData.skillGains，
        /// 可为负，0 = 无信息）。</summary>
        public Dictionary<string, int> TraitGains = new Dictionary<string, int>();

        /// <summary>被禁用的原版技能（TotallyDisabled；等级/兴趣/加成一律按 0 计）。</summary>
        public HashSet<string> DisabledSkills = new HashSet<string>();

        /// <summary>分技能品质分布（SkillDefName → QualityCategory 名 → 件数；行为佐证）。</summary>
        public Dictionary<string, Dictionary<string, int>> QualityBySkill = new Dictionary<string, Dictionary<string, int>>();

        /// <summary>全局品质分布（QualityBySkill 为空时的回退口径；供测试/诊断）。</summary>
        public Dictionary<string, int> QualityCounts = new Dictionary<string, int>();

        /// <summary>[已废弃] 旧版"成长潜力"固定样本；新公式不再使用，仅为旧调用点兼容保留。</summary>
        public int BaseGrowth = 70;

        /// <summary>禁用技能一律按无数据计（等级/兴趣/先天加成全 0）。</summary>
        private bool IsDisabled(string skill)
        {
            return skill != null && DisabledSkills.Contains(skill);
        }

        public int GetLevel(string skill)
        {
            return IsDisabled(skill) ? 0 : (SkillLevels.TryGetValue(skill, out int v) ? v : 0);
        }

        public int GetPractice(string skill)
        {
            return Practice.TryGetValue(skill, out int v) ? v : 0;
        }

        public int GetPassion(string skill)
        {
            return IsDisabled(skill) ? 0 : (Passion.TryGetValue(skill, out int v) ? v : 0);
        }

        public int GetBackstory(string skill)
        {
            return IsDisabled(skill) ? 0 : (BackstoryGains.TryGetValue(skill, out int v) ? v : 0);
        }

        public int GetTrait(string skill)
        {
            return IsDisabled(skill) ? 0 : (TraitGains.TryGetValue(skill, out int v) ? v : 0);
        }
    }

    /// <summary>单专业适配结果（UI 只消费；文案经翻译键解析）。</summary>
    public sealed class ProfessionalFitResult
    {
        public string Major;
        /// <summary>综合适配分 0~100。</summary>
        public int Fit;
        /// <summary>六维分量（0~100）。</summary>
        public int SkillScore;
        public int BackstoryScore;
        public int TraitScore;
        public int PassionScore;
        public int PracticeScore;
        public int AchievementScore;
        /// <summary>[兼容保留] 旧"成长潜力"维度的合成值（背景/特性/热情均值），不参与 Fit 计算。</summary>
        public int GrowthScore;
        /// <summary>该专业权重最高的 3 个技能（SkillDefName，降序）——优势/短板判定依据。</summary>
        public List<string> TopSkills = new List<string>();
        /// <summary>优势技能（基础分 ≥ 70；SkillDefName）。</summary>
        public List<string> Pros = new List<string>();
        /// <summary>短板技能（基础分 &lt; 40；SkillDefName）。</summary>
        public List<string> Cons = new List<string>();
    }

    /// <summary>
    /// 专业适配分析器（纯函数，内在禀赋主导）。
    ///   ProfessionalFit = 等级×0.40 + 背景×0.15 + 特性×0.15 + 兴趣×0.10 + 实践×0.12 + 品质×0.08
    ///   等级基础分 skillBase(L) = 100×(1−(1−L/20)^1.15)（弱边际递减，高端差距比旧 ^1.6 曲线更显著）；
    ///   背景/特性 AptitudeNorm(a) = clamp(50 + a×6.25, 0, 100)（±8 加成 → 0~100，0 = 中性 50）；
    ///   实践 practiceNorm = min(1, 次数/320)×100；
    ///   品质 = 该专业权重内有数据技能的加权均值（无数据回退全局 QualityCounts）。
    /// 12 专业权重矩阵每行权重和 = 100（一个原版 Skill 可进入多个专业，形成不同 CareerSkill）。
    /// </summary>
    public static class ProfessionalFitAnalyzer
    {
        /// <summary>实践次数归一化上限。</summary>
        public const int PracticeCap = 320;

        // ---- 六维权重（合计 = 1.0；内在禀赋 0.80 + 行为佐证 0.20） ----
        public const double WLevel = 0.40;
        public const double WBackstory = 0.15;
        public const double WTrait = 0.15;
        public const double WPassion = 0.10;
        public const double WPractice = 0.12;
        public const double WQuality = 0.08;

        /// <summary>先天加成归一步长：±1 点技能加成 → ∓6.25 分（±8 覆满 0~100）。</summary>
        public const double AptitudeNormStep = 6.25;

        /// <summary>品质权重（对齐前端 QUAL_W）。</summary>
        private static readonly Dictionary<string, int> QualityWeights = new Dictionary<string, int>
        {
            { "Normal", 40 }, { "Good", 60 }, { "Excellent", 80 },
            { "Masterwork", 95 }, { "Legendary", 100 }
        };

        /// <summary>12 专业 → 技能权重矩阵（权重和 = 100；键为原版 SkillDefName）。
        /// [GOV-006] 已修复 4 处 Medical→Medicine（原版无 Medical defName）。运行期以
        /// MajorFitDef 注入矩阵优先，本表仅作 Loader 缺失时的内置回退。</summary>
        private static readonly Dictionary<string, Dictionary<string, int>> MajorWeights =
            new Dictionary<string, Dictionary<string, int>>
            {
                { ProfessionalMajor.Engineering, W("Construction", 35, "Crafting", 25, "Intellectual", 20, "Mining", 10, "Artistic", 5, "Plants", 5) },
                { ProfessionalMajor.Manufacturing, W("Crafting", 50, "Intellectual", 15, "Construction", 15, "Mining", 10, "Artistic", 10) },
                { ProfessionalMajor.Agriculture, W("Plants", 60, "Animals", 15, "Intellectual", 10, "Construction", 5, "Crafting", 5, "Medicine", 5) },
                { ProfessionalMajor.Forestry, W("Plants", 50, "Construction", 15, "Mining", 10, "Animals", 10, "Crafting", 10, "Intellectual", 5) },
                { ProfessionalMajor.AnimalHusbandry, W("Animals", 60, "Plants", 15, "Medicine", 10, "Social", 5, "Intellectual", 5, "Crafting", 5) },
                { ProfessionalMajor.Medicine, W("Medicine", 55, "Intellectual", 25, "Social", 5, "Crafting", 5, "Animals", 5, "Plants", 5) },
                { ProfessionalMajor.Weapons, W("Shooting", 30, "Crafting", 25, "Melee", 20, "Intellectual", 10, "Construction", 5, "Mining", 5, "Medicine", 5) },
                { ProfessionalMajor.Mining, W("Mining", 65, "Construction", 10, "Crafting", 10, "Intellectual", 10, "Plants", 5) },
                { ProfessionalMajor.Research, W("Intellectual", 70, "Medicine", 10, "Crafting", 5, "Construction", 5, "Artistic", 5, "Social", 5) },
                { ProfessionalMajor.Cooking, W("Cooking", 70, "Plants", 10, "Animals", 10, "Medicine", 5, "Crafting", 5) },
                { ProfessionalMajor.Art, W("Artistic", 70, "Crafting", 15, "Social", 5, "Intellectual", 5, "Construction", 5) },
                { ProfessionalMajor.Management, W("Social", 45, "Intellectual", 25, "Medicine", 5, "Crafting", 5, "Construction", 5, "Animals", 5, "Plants", 5, "Shooting", 5) }
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

        // ---- [铁律 6.5 数据驱动] Def 注入源（启动期由 ProfessionalMajorFitLoader 载入；null = 回退内置矩阵） ----

        /// <summary>Def 驱动的专业权重矩阵源（major → skill → weight，行和=100 由 Loader 校验）。</summary>
        private static Dictionary<string, Dictionary<string, int>> _majorWeightsSource;

        /// <summary>Def 驱动的品质权重源（QualityCategory 名 → weight；未覆盖档回退内置）。</summary>
        private static Dictionary<string, int> _qualityWeightsSource;

        /// <summary>注入 Def 驱动的专业权重矩阵（启动期调用一次）。未覆盖的专业回退内置矩阵；
        /// 矩阵可能引入新技能键，故重建 [GOV-006] 单一真源缓存。</summary>
        public static void SetWeightsSource(Dictionary<string, Dictionary<string, int>> major)
        {
            _majorWeightsSource = major;
            _vanillaSkillKeys = null;
        }

        /// <summary>注入 Def 驱动的品质权重（启动期调用一次）。未覆盖的品质档回退内置权重。</summary>
        public static void SetQualityWeightsSource(Dictionary<string, int> quality)
        {
            _qualityWeightsSource = quality;
        }

        /// <summary>解析某专业的最终权重：Def 源优先，否则内置矩阵；都不含 → null。</summary>
        private static Dictionary<string, int> ResolveMajorWeights(string major)
        {
            if (_majorWeightsSource != null && _majorWeightsSource.TryGetValue(major, out Dictionary<string, int> w)
                && w != null)
            {
                return w;
            }
            MajorWeights.TryGetValue(major, out Dictionary<string, int> hw);
            return hw;
        }

        /// <summary>解析品质权重：Def 源优先，否则内置。</summary>
        private static bool TryQualityWeight(string key, out int w)
        {
            if (_qualityWeightsSource != null && key != null
                && _qualityWeightsSource.TryGetValue(key, out w))
            {
                return true;
            }
            return QualityWeights.TryGetValue(key ?? "", out w);
        }

        /// <summary>12 专业权重矩阵（供测试/校验：每行权重和必须 = 100）。</summary>
        public static IReadOnlyDictionary<string, Dictionary<string, int>> MajorWeightsTable => MajorWeights;

        /// <summary>
        /// 权重矩阵中出现过的**全部原版技能键**（去重，顺序 = 首次出现序）。
        /// [GOV-006] 单一真源：UI / 采集侧一律遍历本属性，禁止复制技能字面量数组。
        /// </summary>
        public static IReadOnlyList<string> VanillaSkillKeys
        {
            get
            {
                if (_vanillaSkillKeys == null)
                {
                    List<string> acc = new List<string>();
                    HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (KeyValuePair<string, Dictionary<string, int>> row in MajorWeights)
                    {
                        if (row.Value == null) continue;
                        foreach (KeyValuePair<string, int> cell in row.Value)
                        {
                            if (string.IsNullOrEmpty(cell.Key)) continue;
                            if (seen.Add(cell.Key)) acc.Add(cell.Key);
                        }
                    }
                    _vanillaSkillKeys = acc;
                }
                return _vanillaSkillKeys;
            }
        }
        private static IReadOnlyList<string> _vanillaSkillKeys;

        /// <summary>第一层：原版技能基础分（0~100；弱边际递减曲线，L20 与 L10 差距 ≈ 2 倍）。</summary>
        public static int SkillBase(int level)
        {
            int l = Math.Max(0, Math.Min(20, level));
            return (int)Math.Round(100.0 * (1.0 - Math.Pow(1.0 - l / 20.0, 1.15)));
        }

        /// <summary>先天加成归一（0~100；a=0 → 中性 50，a=+8 → 100，a=−8 → 0）。</summary>
        public static int AptitudeNorm(int amount)
        {
            double v = 50.0 + amount * AptitudeNormStep;
            return (int)Math.Round(Math.Max(0.0, Math.Min(100.0, v)));
        }

        /// <summary>实践归一化（0~100）。</summary>
        public static int PracticeNorm(int count)
        {
            return (int)Math.Round(Math.Min(1.0, Math.Max(0, count) / (double)PracticeCap) * 100.0);
        }

        /// <summary>品质代表值（0~100；无样本 = 0）。</summary>
        public static int QualityScore(Dictionary<string, int> counts)
        {
            if (counts == null) return 0;
            double num = 0.0, den = 0.0;
            foreach (KeyValuePair<string, int> kv in counts)
            {
                if (!TryQualityWeight(kv.Key ?? "", out int w)) continue;
                int c = Math.Max(0, kv.Value);
                num += w * c;
                den += c;
            }
            return den > 0.0 ? (int)Math.Round(num / den) : 0;
        }

        /// <summary>分析全部专业，按适配分降序返回。</summary>
        public static List<ProfessionalFitResult> Analyze(ProfessionalFitInput input)
        {
            List<ProfessionalFitResult> results = new List<ProfessionalFitResult>();
            if (input == null) return results;

            HashSet<string> seenMajors = new HashSet<string>(StringComparer.Ordinal);
            foreach (string major in ProfessionalMajor.All)
            {
                seenMajors.Add(major);
                Dictionary<string, int> weights = ResolveMajorWeights(major);
                if (weights == null)
                {
                    continue;
                }
                results.Add(AnalyzeOne(major, weights, input));
            }
            // [铁律 6.5] Def 矩阵可扩展内置 12 专业之外的专业（追加在尾部分析）。
            if (_majorWeightsSource != null)
            {
                foreach (KeyValuePair<string, Dictionary<string, int>> kv in _majorWeightsSource)
                {
                    if (kv.Key == null || kv.Value == null || !seenMajors.Add(kv.Key)) continue;
                    results.Add(AnalyzeOne(kv.Key, kv.Value, input));
                }
            }
            results.Sort((a, b) => b.Fit.CompareTo(a.Fit));
            return results;
        }

        /// <summary>
        /// 单专业六维加权（内在禀赋 0.80 + 行为佐证 0.20；详见类头注释）。
        /// </summary>
        private static ProfessionalFitResult AnalyzeOne(string major, Dictionary<string, int> weights,
            ProfessionalFitInput input)
        {
            double level = WeightedDim(weights, s => SkillBase(input.GetLevel(s)));
            double story = WeightedDim(weights, s => AptitudeNorm(input.GetBackstory(s)));
            double trait = WeightedDim(weights, s => AptitudeNorm(input.GetTrait(s)));
            double passion = WeightedDim(weights, s => input.GetPassion(s));
            double practice = WeightedDim(weights, s => PracticeNorm(input.GetPractice(s)));
            double achievement = WeightedQuality(weights, input);

            int fit = (int)Math.Round(level * WLevel + story * WBackstory + trait * WTrait
                + passion * WPassion + practice * WPractice + achievement * WQuality);

            ProfessionalFitResult r = new ProfessionalFitResult
            {
                Major = major,
                Fit = Math.Max(0, Math.Min(100, fit)),
                SkillScore = (int)Math.Round(level),
                BackstoryScore = (int)Math.Round(story),
                TraitScore = (int)Math.Round(trait),
                PassionScore = (int)Math.Round(passion),
                PracticeScore = (int)Math.Round(practice),
                AchievementScore = (int)Math.Round(achievement),
                GrowthScore = (int)Math.Round((story + trait + passion) / 3.0)
            };

            FillProsAndCons(r, weights, input);
            return r;
        }

        /// <summary>按专业权重求加权和（权重和 = 100，故结果天然落在各维度值域内）。</summary>
        private static double WeightedDim(Dictionary<string, int> weights, Func<string, double> valueOf)
        {
            double acc = 0.0;
            foreach (KeyValuePair<string, int> kv in weights)
            {
                acc += kv.Value / 100.0 * valueOf(kv.Key);
            }
            return acc;
        }

        /// <summary>品质维：专业权重内**有数据技能**的加权均值；全专业无分技能数据时回退全局口径。</summary>
        private static double WeightedQuality(Dictionary<string, int> weights, ProfessionalFitInput input)
        {
            bool hasPerSkill = input.QualityBySkill != null && input.QualityBySkill.Count > 0;
            if (!hasPerSkill)
            {
                return QualityScore(input.QualityCounts);
            }
            double num = 0.0, den = 0.0;
            foreach (KeyValuePair<string, int> kv in weights)
            {
                if (input.QualityBySkill.TryGetValue(kv.Key, out Dictionary<string, int> q)
                    && q != null && q.Count > 0)
                {
                    num += kv.Value / 100.0 * QualityScore(q);
                    den += kv.Value / 100.0;
                }
            }
            return den > 0.0 ? num / den : 0.0;
        }

        /// <summary>优势√ / 短板△：权重最高的 3 个技能中，基础分 ≥70 为优势，&lt;40 为短板。</summary>
        private static void FillProsAndCons(ProfessionalFitResult r, Dictionary<string, int> weights,
            ProfessionalFitInput input)
        {
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
        }
    }
}

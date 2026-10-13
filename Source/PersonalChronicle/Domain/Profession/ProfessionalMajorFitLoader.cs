using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 启动期将 MajorFit / QualityWeight Def 载入 ProfessionalFitAnalyzer（铁律 6.5 数据驱动）。
    /// 必须在 DefDatabase 填充后调用（[StaticConstructorOnStartup]），故由 ChronicleStartup 触发。
    /// 每行 fitWeights 权重和必须 = 100；不一致仅 Log.Error 不阻断加载（设计容错）。
    /// </summary>
    public static class ProfessionalMajorFitLoader
    {
        public static void Load()
        {
            Dictionary<string, Dictionary<string, int>> major =
                new Dictionary<string, Dictionary<string, int>>();
            foreach (ProfessionalMajorFitDef def in DefDatabase<ProfessionalMajorFitDef>.AllDefsListForReading)
            {
                Dictionary<string, int> inner = new Dictionary<string, int>();
                int sum = 0;
                if (def.fitWeights != null)
                {
                    foreach (FitWeightEntry e in def.fitWeights)
                    {
                        if (string.IsNullOrEmpty(e.skillDefName)) continue;
                        inner[e.skillDefName] = e.weight;
                        sum += e.weight;
                    }
                }
                if (sum != 100)
                {
                    Log.Error("[PersonalChronicle] MajorFit row sum != 100 for major=" + def.major
                        + " (sum=" + sum + "); 权重矩阵已按 Def 载入但平衡将偏移。");
                }
                major[def.major] = inner;
            }
            ProfessionalFitAnalyzer.SetWeightsSource(major);

            Dictionary<string, int> quality = new Dictionary<string, int>();
            foreach (ProfessionalQualityWeightDef def in DefDatabase<ProfessionalQualityWeightDef>.AllDefsListForReading)
            {
                if (!string.IsNullOrEmpty(def.qualityCategory))
                {
                    quality[def.qualityCategory] = def.weight;
                }
            }
            ProfessionalFitAnalyzer.SetQualityWeightsSource(quality);
        }
    }
}

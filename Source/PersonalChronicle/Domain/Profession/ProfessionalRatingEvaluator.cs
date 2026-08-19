using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 专业评级派生（V2.0 §13 / P4-A）。纯函数、无 Verse 运行时依赖，可离线单测。
    ///
    /// 由专业能力 Level 经软上限档位派生：取「阈值 ≤ level 的最高档位」（order 最小者）。
    /// 未达熟练阈值 → 返回 null（无评级、无额外效果，退回 P3 行为）。
    /// </summary>
    public static class ProfessionalRatingEvaluator
    {
        /// <summary>
        /// 由 Level 解析评级档位。
        /// </summary>
        /// <param name="level">专业能力等级（可为 0）。</param>
        /// <param name="ratingDefs">全部 ProfessionalRatingDef（按 DefDatabase 顺序；可空）。</param>
        /// <returns>命中的最高档位 Def；无 RatingDef 或未达最低阈值返回 null。</returns>
        public static ProfessionalRatingDef ResolveRating(int level, List<ProfessionalRatingDef> ratingDefs)
        {
            if (ratingDefs == null || ratingDefs.Count == 0)
            {
                return null;
            }
            ProfessionalRatingDef best = null;
            for (int i = 0; i < ratingDefs.Count; i++)
            {
                ProfessionalRatingDef def = ratingDefs[i];
                if (def == null || level < def.minLevel)
                {
                    continue;
                }
                // 取 order 最小（最高档）者；同 order 取后定义者（Def 加载顺序保证最终定义）
                if (best == null || def.order < best.order)
                {
                    best = def;
                }
            }
            return best;
        }

        /// <summary>
        /// 便捷重载：经 DefDatabase 全局索引。
        /// </summary>
        public static ProfessionalRatingDef ResolveRating(int level)
        {
            return ResolveRating(level, DefDatabase<ProfessionalRatingDef>.AllDefsListForReading);
        }
    }
}

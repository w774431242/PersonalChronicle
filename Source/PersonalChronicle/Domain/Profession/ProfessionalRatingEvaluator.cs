using System;
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

        /// <summary>
        /// 按<b>档位偏移</b>移动评级（纯函数，可离线单测）。约定与 <see cref="ResolveRating"/> 一致：
        /// <c>order</c> 越小档位越高。
        ///
        /// <para>语义（车间环境加成的消费点）：<paramref name="tiers"/> &gt; 0 = 升档（如质检车间
        /// 「在岗时按更高评级结算」），&lt; 0 = 降档；越界夹到最高/最低档端点。</para>
        ///
        /// <para><b><paramref name="current"/> 为 null 时恒返回 null</b>：未达任何评级门槛意味着
        /// 该技能尚无评级，车间环境**不得凭空授予**评级（否则 0 级就能吃到满档红利），
        /// 只能放大已有评级。</para>
        ///
        /// <para>性能：零分配（三次 O(n) 扫描，n = 评级 Def 数，通常 4~5）；<paramref name="tiers"/>
        /// 为 0（绝大多数调用）时立即返回原对象。</para>
        /// </summary>
        public static ProfessionalRatingDef ShiftRating(
            ProfessionalRatingDef current, int tiers, List<ProfessionalRatingDef> ratingDefs)
        {
            if (current == null || tiers == 0 || ratingDefs == null || ratingDefs.Count == 0)
            {
                return current;
            }
            // 只有"确有偏移"时才建立有序快照：tiers != 0 属罕见路径（仅当 pawn 在岗且
            // 其车间类型声明了 ratingOffset），n = 评级 Def 数（通常 4~5）。
            ProfessionalRatingDef[] ordered = BuildOrdered(ratingDefs);
            if (ordered.Length == 0)
            {
                return current;
            }
            int idx = Array.IndexOf(ordered, current);
            if (idx < 0)
            {
                return current;                          // current 不在列表内（防御）
            }
            int target = idx - tiers;                    // order 越小档位越高 ⇒ 升档 = 索引减小
            if (target < 0)
            {
                target = 0;
            }
            else if (target > ordered.Length - 1)
            {
                target = ordered.Length - 1;
            }
            return target == idx ? current : ordered[target];
        }

        /// <summary>有效 Def 按 order 升序（高→低）快照；同 order 保持 DefDatabase 顺序。</summary>
        private static ProfessionalRatingDef[] BuildOrdered(List<ProfessionalRatingDef> defs)
        {
            var valid = new List<ProfessionalRatingDef>(defs.Count);
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i] != null)
                {
                    valid.Add(defs[i]);
                }
            }
            valid.Sort((a, b) => a.order.CompareTo(b.order));
            return valid.ToArray();
        }
    }
}

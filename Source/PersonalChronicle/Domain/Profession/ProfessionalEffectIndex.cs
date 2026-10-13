using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Profession
{
    /// <summary>
    /// 专业效果 Def 索引（V2.0 §10 Effect Library 的数据侧）。
    ///
    /// [GOV-006 评审 2026-08-29] 原与 StatPart 注入逻辑混在 Capture/Effects/ProfessionalEffectRegistry，
    /// 导致 Application/Effects/ProfessionalEffectService 为取索引而反向依赖 Capture（违反分层单向
    /// UI → Application → Domain ← Data/Capture）。索引属纯数据，随 ProfessionalEffectDef /
    /// ProfessionalRatingDef 同归 Domain；StatPart 注入属 RimWorld 适配，按 V2.0 §11/§21 归顶层 Effects/。
    ///
    /// 消费方通过参数把索引传给 ProfessionalEffectResolver（纯函数，不反向依赖本索引）。
    ///
    /// ARC-008 受控静态状态声明：本类静态字段为「进程级只读缓存」，属规范允许的受控单例
    /// （非游戏状态/Save/UI 状态）。生命周期：Def 加载后 [StaticConstructorOnStartup] 构建；
    /// Def reload 由 <see cref="Rebuild"/> 幂等重建；增长受 Def 数量上限天然约束。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ProfessionalEffectIndex
    {
        /// <summary>全部 ProfessionalEffectDef 索引（defName → Def），供 Resolver 查询。受控静态缓存（ARC-008）。</summary>
        public static readonly Dictionary<string, ProfessionalEffectDef> EffectDefs =
            new Dictionary<string, ProfessionalEffectDef>();

        /// <summary>全部 ProfessionalRatingDef 列表（供 Resolver 评级加权查询）。受控静态缓存（ARC-008）。</summary>
        public static readonly List<ProfessionalRatingDef> RatingDefs = new List<ProfessionalRatingDef>();

        static ProfessionalEffectIndex()
        {
            Rebuild();
        }

        /// <summary>
        /// 幂等重建索引（Def reload 安全）。重复 defName 后者覆盖（Def 加载顺序保证最终定义生效）。
        ///
        /// 由静态构造与 Effects 层注入器共用——注入器在自身静态构造内先行调用本方法，
        /// 以规避两个 [StaticConstructorOnStartup] 类之间构造顺序不确定的风险。
        /// </summary>
        public static void Rebuild()
        {
            EffectDefs.Clear();
            List<ProfessionalEffectDef> all = DefDatabase<ProfessionalEffectDef>.AllDefsListForReading;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && !string.IsNullOrEmpty(all[i].defName))
                {
                    EffectDefs[all[i].defName] = all[i];
                }
            }
            // 评级 Def 索引（P4 评级加权）；readonly 字段用 Clear+AddRange 原地更新
            RatingDefs.Clear();
            RatingDefs.AddRange(DefDatabase<ProfessionalRatingDef>.AllDefsListForReading);
        }
    }
}

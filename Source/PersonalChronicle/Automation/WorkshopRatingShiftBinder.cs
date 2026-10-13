using PersonalChronicle.Application.Effects;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 把「在岗车间的评级档位偏移」注入 Application 契约
    /// <see cref="WorkshopRatingShift"/>（Automation → Application 为允许方向）。
    ///
    /// <para>这就是 <c>WorkshopTypeDef.ratingOffset</c> 的落地消费链路：
    /// 车间类型声明偏移 → 该车间在岗 pawn 的上岗缓存算出实际偏移
    /// （<c>MapComponent_WorkshopManager.BonusOf</c>，已按技能/覆盖度/方向缩放）→
    /// <c>ProfessionalEffectResolver.EffectiveRating</c> 在解析评级档位时叠加 →
    /// 影响该 pawn 的效果强度（速度/品质/通用 Stat）。</para>
    ///
    /// <para>绑定发生在静态构造期，但取值委托只在被调用时才访问 MapComponent
    /// （<c>ConditionalWeakTable</c> 惰性 Get），启动期无副作用、不写日志。</para>
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class WorkshopRatingShiftBinder
    {
        static WorkshopRatingShiftBinder()
        {
            WorkshopRatingShift.SetSource(RatingShiftOf);
        }

        /// <summary>未上岗 / 无车间类型 / 该类型未声明 ratingOffset 时一律返回 0。</summary>
        private static int RatingShiftOf(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return 0;
            }
            MapComponent_WorkshopManager manager = MapComponent_WorkshopManager.Get(pawn.Map);
            // 与产出/工时同口径（2026-09-11）：在岗 ∪ 物理位于车间内。
            return manager != null ? manager.WorkingBonusOf(pawn).ratingOffset : 0;
        }
    }
}

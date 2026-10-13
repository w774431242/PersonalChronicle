using System;
using Verse;

namespace PersonalChronicle.Application.Effects
{
    /// <summary>
    /// 「在岗车间的评级档位偏移」查询口（Application 层契约；取值实现由 Automation 在启动时注入）。
    ///
    /// <para><b>为什么要这一层间接</b>：偏移的来源只存在于 Automation
    /// （<c>MapComponent_WorkshopManager.BonusOf(pawn).ratingOffset</c>），而消费点分布在
    /// Application（<see cref="ProfessionalEffectService"/>、<c>ProfessionalBonusView</c>）。
    /// 依赖方向禁止 Application → Automation，故此处建立一个**受控静态钩子**（ARC-008）：
    /// Application 只依赖本抽象，Automation 用一个 <c>[StaticConstructorOnStartup]</c> 绑定器
    /// 注入取值委托（Automation → Application 是允许方向）。</para>
    ///
    /// <para><b>降级</b>：未注入 / pawn 为空 / 未上岗 / 车间未指定类型 → <c>0</c>（零影响）；
    /// 委托抛异常 → 返回 <c>0</c>（读取失败不得中断 stat 求值或 UI 绘制）。</para>
    /// </summary>
    public static class WorkshopRatingShift
    {
        private static Func<Pawn, int> source;

        /// <summary>绑定取值委托（幂等：重复绑定覆盖旧委托；传 null 可解绑）。</summary>
        public static void SetSource(Func<Pawn, int> value)
        {
            source = value;
        }

        /// <summary>
        /// 该 pawn 当前的车间评级档位偏移（无则 0）。热路径安全：一次委托调用 + 异常兜底，
        /// 无分配、无字典查找（取值实现自身只做两次字典命中）。
        /// </summary>
        public static int Of(Pawn pawn)
        {
            Func<Pawn, int> fn = source;
            if (fn == null || pawn == null)
            {
                return 0;
            }
            try
            {
                return fn(pawn);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}

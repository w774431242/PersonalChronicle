using System;
using Verse;

namespace PersonalChronicle.Application.Effects
{
    /// <summary>
    /// 「车间加成」受控访问口（Application 层契约；取值/写入实现由 Automation 在启动时注入）。
    ///
    /// <para><b>为什么需要这层间接（2026-09-21 审计修复 · CHG-SHOP-AUDIT-001 · A-1）</b>：
    /// 加成来源只存在于 Automation（<c>MapComponent_WorkshopManager.WorkingBonusOf</c> /
    /// <c>WorkshopProductRegistry</c>），而消费点分布在 Effects（三个 StatPart）与
    /// Capture（<c>Patch_GenRecipe</c> 转调的产出效果服务）。依赖方向禁止
    /// Effects/Capture → Automation 直读内部实现，故此处建立**受控静态钩子**（ARC-008，
    /// 与 <see cref="WorkshopRatingShift"/> 同范式）：契约只依赖 Application 自己，
    /// Automation 用一个 <c>[StaticConstructorOnStartup]</c> 绑定器注入实现
    /// （Automation → Application 是允许方向）。</para>
    ///
    /// <para><b>降级</b>：未注入 / pawn 为空 / 未上岗 / 无车间类型 → 中性加成（零影响）；
    /// 委托抛异常 → 同样回落中性/空（读取失败不得中断 stat 求值或产出结算）。</para>
    /// </summary>
    public static class WorkshopBonusAccess
    {
        private static Func<Pawn, WorkshopEffectiveBonus> workingBonusSource;
        private static Func<Pawn, string> workingTypeLabelSource;
        private static Func<int, WorkshopProductBonus?> productBonusSource;
        private static Action<int, float, float> productBonusRegistrar;

        /// <summary>
        /// 绑定实现委托（幂等：重复绑定覆盖旧委托；全部传 null 可解绑）。
        /// 缺失的维度各自回落中性/空/无操作。
        /// </summary>
        public static void SetSources(
            Func<Pawn, WorkshopEffectiveBonus> workingBonus,
            Func<Pawn, string> workingTypeLabel,
            Func<int, WorkshopProductBonus?> productBonus,
            Action<int, float, float> productBonusRegister)
        {
            workingBonusSource = workingBonus;
            workingTypeLabelSource = workingTypeLabel;
            productBonusSource = productBonus;
            productBonusRegistrar = productBonusRegister;
        }

        /// <summary>
        /// 该 pawn 当前「工作时」的车间加成（在岗缓存优先，否则按物理位置现场解析并缓存；
        /// 未在车间内 → 中性）。热路径安全：一次委托调用 + 异常兜底，实现侧只做
        /// 一次字典命中（未命中才解析一次）。
        /// </summary>
        public static WorkshopEffectiveBonus WorkingBonusOf(Pawn pawn)
        {
            Func<Pawn, WorkshopEffectiveBonus> fn = workingBonusSource;
            if (fn == null || pawn == null)
            {
                return WorkshopEffectiveBonus.Neutral;
            }
            try
            {
                return fn(pawn);
            }
            catch (Exception)
            {
                return WorkshopEffectiveBonus.Neutral;
            }
        }

        /// <summary>
        /// 该 pawn 当前工作车间的<b>类型显示名</b>（Def label；未在车间/无类型 → null）。
        /// 供 StatPart 的 ExplanationPart 拼"来自哪个车间"的说明文本。
        /// </summary>
        public static string WorkingTypeLabelOf(Pawn pawn)
        {
            Func<Pawn, string> fn = workingTypeLabelSource;
            if (fn == null || pawn == null)
            {
                return null;
            }
            try
            {
                return fn(pawn);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 取某件成品的车间加成（精度/耐久）。未登记 / 未注入 / 委托异常 → false（零影响）。
        /// </summary>
        public static bool TryGetProductBonus(int thingId, out WorkshopProductBonus bonus)
        {
            Func<int, WorkshopProductBonus?> fn = productBonusSource;
            if (fn != null)
            {
                try
                {
                    WorkshopProductBonus? value = fn(thingId);
                    if (value.HasValue)
                    {
                        bonus = value.Value;
                        return true;
                    }
                }
                catch (Exception)
                {
                    // 读取失败按"未登记"处理（零影响），不得中断 stat 求值。
                }
            }
            bonus = default;
            return false;
        }

        /// <summary>
        /// 登记一件成品的车间加成（耐久/精度），供后续 StatPart 求值取用。
        /// 未注入 / 委托异常 → 无操作（宁可不加，不可错加）。
        /// </summary>
        public static void RegisterProductBonus(int thingId, float maxHpFactor, float accuracyFactor)
        {
            Action<int, float, float> fn = productBonusRegistrar;
            if (fn == null)
            {
                return;
            }
            try
            {
                fn(thingId, maxHpFactor, accuracyFactor);
            }
            catch (Exception)
            {
                // 登记失败只影响该件成品的加成展示，不得中断产出结算。
            }
        }

        private static Action<Pawn, Thing> productMadeSink;

        /// <summary>
        /// 绑定「产出归账」实现（2026-10-02 审计 P1-C）：Automation 侧在启动时注入，
        /// Capture 层的 <c>Patch_GenRecipe</c> 只转调本方法 —— 消除
        /// <c>Capture → Automation</c> 直读（跨层红线，此前为
        /// <c>MapComponent_WorkshopManager.Get(map).NotifyProductMade(worker)</c>）。
        /// 独立于 <see cref="SetSources"/>，避免改动既有调用点签名。
        ///
        /// <para><b>CHG-20261005-DEVCOLS（2026-10-05）</b>：委托由 <c>Action&lt;Pawn&gt;</c> 扩为
        /// <c>Action&lt;Pawn, Thing&gt;</c> —— 多带一个<b>成品</b>引用，供设备台账登记「累计产值」
        /// （<c>product.MarketValue</c>）。为什么不从 <c>worker.CurJob</c> 反推产物：job 在
        /// Postfix 时刻可能已被清除/切换，反推会得到空或错的产物；而 Postfix 手上本来就有
        /// <c>product</c> ⇒ 由它传下来是唯一准确且零额外扫描的做法。</para>
        /// </summary>
        public static void SetProductMadeSink(Action<Pawn, Thing> sink)
        {
            productMadeSink = sink;
        }

        /// <summary>
        /// 产出归账入口（<c>Patch_GenRecipe</c> Postfix 调用）：
        /// 未注入 / pawn 为空 / 委托异常 → 无操作（归账失败不得中断产出结算链）。
        /// </summary>
        /// <param name="pawn">产出者（可为空：非人制造路径）。</param>
        /// <param name="product">刚出炉的成品（CHG-20261005-DEVCOLS 起用于产值归账；可为空）。</param>
        public static void NotifyProductMade(Pawn pawn, Thing product)
        {
            Action<Pawn, Thing> fn = productMadeSink;
            if (fn == null || pawn == null)
            {
                return;
            }
            try
            {
                fn(pawn, product);
            }
            catch (Exception)
            {
                // 归账失败只影响本次绩效/设备台账计数，不得中断产出结算。
            }
        }
    }
}

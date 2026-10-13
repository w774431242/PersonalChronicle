using PersonalChronicle.Application.Effects;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 把「车间加成」取值实现注入 Application 契约 <see cref="WorkshopBonusAccess"/>
    /// （Automation → Application 为允许方向；2026-09-21 审计修复 · CHG-SHOP-AUDIT-001 · A-1）。
    ///
    /// <para>与 <see cref="WorkshopRatingShiftBinder"/> 同构：绑定发生在静态构造期，
    /// 但委托只在被调用时才访问 MapComponent / 注册表（惰性 Get），
    /// 启动期无副作用、不写日志。</para>
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class WorkshopBonusAccessBinder
    {
        static WorkshopBonusAccessBinder()
        {
            WorkshopBonusAccess.SetSources(EffectiveBonusOf, WorkingTypeLabelOf, ProductBonusOf, MarkProductBonus);
            // 2026-10-02 审计修复（P1-C）：产出归账同样走契约 —— Capture 层（Patch_GenRecipe）
            // 不再直读 MapComponent_WorkshopManager（跨层红线 ARCH P1-C）。
            WorkshopBonusAccess.SetProductMadeSink(ProductMadeOf);
        }

        /// <summary>
        /// 产出归账（<c>Patch_GenRecipe</c> Postfix → 契约 → 本方法）：把成品计入
        /// 工人所在车间的按人绩效与其工作台的设备台账。非场上 pawn ⇒ 无操作。
        ///
        /// <para><b>CHG-20261005-DEVCOLS</b>：多接一个 <paramref name="product"/>
        /// （成品引用），供设备台账登记「累计产值」（<c>MarketValue</c>）。
        /// 非场上 pawn / 空产物均不阻断 —— 产物为空时只记件数不记产值。</para>
        /// </summary>
        private static void ProductMadeOf(Pawn pawn, Thing product)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }
            MapComponent_WorkshopManager.Get(pawn.Map)?.NotifyProductMade(pawn, product);
        }

        /// <summary>「工作时加成」取值（在岗缓存 ∪ 物理位于车间内；未在车间 → 中性）。</summary>
        private static WorkshopEffectiveBonus EffectiveBonusOf(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return WorkshopEffectiveBonus.Neutral;
            }
            MapComponent_WorkshopManager manager = MapComponent_WorkshopManager.Get(pawn.Map);
            if (manager == null)
            {
                return WorkshopEffectiveBonus.Neutral;
            }
            return manager.WorkingBonusOf(pawn);
        }

        /// <summary>当前工作车间的类型显示名（无车间/未指定类型 → null）。</summary>
        private static string WorkingTypeLabelOf(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return null;
            }
            MapComponent_WorkshopManager manager = MapComponent_WorkshopManager.Get(pawn.Map);
            if (manager == null)
            {
                return null;
            }
            Zone_Workshop zone = manager.WorkingZoneOf(pawn);
            return zone != null && zone.WorkshopType != null ? zone.WorkshopType.label : null;
        }

        /// <summary>成品加成登记查询（未登记 → null）。</summary>
        private static WorkshopProductBonus? ProductBonusOf(int thingId)
        {
            WorkshopProductBonus bonus;
            return WorkshopProductRegistry.TryGet(thingId, out bonus)
                ? bonus
                : (WorkshopProductBonus?)null;
        }

        /// <summary>成品加成登记（写入注册表；注册表自带 Game 作用域守卫与有界保护）。</summary>
        private static void MarkProductBonus(int thingId, float maxHpFactor, float accuracyFactor)
        {
            WorkshopProductRegistry.Mark(thingId, maxHpFactor, accuracyFactor);
        }
    }
}

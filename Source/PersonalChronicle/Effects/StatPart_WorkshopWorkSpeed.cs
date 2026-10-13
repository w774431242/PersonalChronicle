using PersonalChronicle.Application.Effects;
using RimWorld;
using Verse;

namespace PersonalChronicle.Effects
{
    /// <summary>
    /// 车间工时 / 维修速度加成 StatPart（落地管线 Step 5）。
    ///
    /// 照搬已验证的 <see cref="StatPart_ProfessionalWorkSpeed"/> 模式：
    /// 由 <see cref="WorkshopStatPartInjector"/> 注入到各配方的 workSpeedStat，
    /// 引擎评估该 stat 时执行 TransformValue。
    ///
    /// 取值走 <see cref="WorkshopBonusAccess"/>（Application 契约；实现由 Automation 注入）
    /// —— 2026-09-21 审计修复（CHG-SHOP-AUDIT-001 · A-1）：此前本类直读
    /// <c>MapComponent_WorkshopManager</c>，违反 Effects ✕→ Automation 的依赖方向。
    ///
    /// 数值方向（易错点）：stat 的 val 是<b>速度</b>（越大越快），
    /// 而蓝图的 workTimeFactor 是<b>工时</b>（越小越快），故换算为乘 1/factor：
    ///   工时 ×0.60 ⇒ 速度 ×1.667。
    /// 维修速度 repairSpeedFactor 本身就是速度，直接乘。
    /// 两类因子不会重复计：每个车间类型只会声明其中一个（维修车间只声明维修速度）。
    ///
    /// 性能：热路径只有「一次委托调用 → 若干次乘法」；加成在上岗时已解析好并缓存，
    /// 此处零解析、零遍历。
    /// </summary>
    public class StatPart_WorkshopWorkSpeed : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Pawn pawn = req.Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }
            // 「工作时加成」：在岗缓存 ∪ 物理位于车间内（2026-09-11 语义修正），
            // 否则在车间里干活（必须先收工）的成员永远吃不到加成。
            WorkshopEffectiveBonus bonus = WorkshopBonusAccess.WorkingBonusOf(pawn);

            float factor = 1f;
            // 工时 → 速度（<1 的工时因子换算为 >1 的速度因子）
            if (bonus.workTimeFactor > 0f && bonus.workTimeFactor < 0.999f)
            {
                factor *= 1f / bonus.workTimeFactor;
            }
            // 维修速度（仅维修车间声明，直接乘）
            if (bonus.repairSpeedFactor > 1.0001f)
            {
                factor *= bonus.repairSpeedFactor;
            }
            if (factor <= 1.0001f)
            {
                return;
            }
            val *= factor;
        }

        public override string ExplanationPart(StatRequest req)
        {
            Pawn pawn = req.Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return null;
            }
            // 当前工作车间的类型显示名（无车间/未指定类型 → null，与旧实现判定等价）。
            string typeLabel = WorkshopBonusAccess.WorkingTypeLabelOf(pawn);
            if (typeLabel == null)
            {
                return null;
            }
            WorkshopEffectiveBonus bonus = WorkshopBonusAccess.WorkingBonusOf(pawn);
            float factor = 1f;
            if (bonus.workTimeFactor > 0f && bonus.workTimeFactor < 0.999f)
            {
                factor *= 1f / bonus.workTimeFactor;
            }
            if (bonus.repairSpeedFactor > 1.0001f)
            {
                factor *= bonus.repairSpeedFactor;
            }
            if (factor <= 1.0001f)
            {
                return null;
            }
            int percent = (int)((factor - 1f) * 100f);
            if (percent <= 0)
            {
                return null;
            }
            return "PersonalChronicle.Workshop.WorkSpeed.StatPart"
                .Translate(typeLabel, percent);
        }
    }
}

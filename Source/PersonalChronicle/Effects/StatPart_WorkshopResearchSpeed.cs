using PersonalChronicle.Application.Effects;
using RimWorld;
using Verse;

namespace PersonalChronicle.Effects
{
    /// <summary>
    /// 车间研究速度加成 StatPart（落地管线 Step 5d，仅研发车间生效）。
    ///
    /// 单独一个 Part 的原因：研究走的是 <c>ResearchSpeed</c> 这一独立 StatDef，
    /// 不能挂在配方 workSpeedStat 上——否则普通生产车间也会误加研究速度。
    /// 本 Part 只注入 ResearchSpeed，且只读 researchSpeedFactor
    /// （除研发车间外该值恒为 1，零影响）。
    ///
    /// 取值走 <see cref="WorkshopBonusAccess"/>（Application 契约；2026-09-21 审计修复
    /// CHG-SHOP-AUDIT-001 · A-1：此前直读 <c>MapComponent_WorkshopManager</c>）。
    ///
    /// 性能：与工时 Part 同构，热路径仅「一次委托调用 → 一次乘法」。
    /// </summary>
    public class StatPart_WorkshopResearchSpeed : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Pawn pawn = req.Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }
            // 「工作时加成」：在岗缓存 ∪ 物理位于车间内（2026-09-11 语义修正）。
            float factor = WorkshopBonusAccess.WorkingBonusOf(pawn).researchSpeedFactor;
            if (factor > 1.0001f)
            {
                val *= factor;
            }
        }

        /// <summary>
        /// 说明文本（2026-09-11 补：此前恒 null ⇒ 玩家在面板里看不到车间加成的来源）。
        /// </summary>
        public override string ExplanationPart(StatRequest req)
        {
            Pawn pawn = req.Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return null;
            }
            string typeLabel = WorkshopBonusAccess.WorkingTypeLabelOf(pawn);
            if (typeLabel == null)
            {
                return null;
            }
            int percent = (int)((WorkshopBonusAccess.WorkingBonusOf(pawn).researchSpeedFactor - 1f) * 100f);
            if (percent <= 0)
            {
                return null;
            }
            return "PersonalChronicle.Workshop.ResearchSpeed.StatPart"
                .Translate(typeLabel, percent);
        }
    }
}

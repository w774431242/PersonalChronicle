using PersonalChronicle.Application.Effects;
using RimWorld;
using Verse;

namespace PersonalChronicle.Effects
{
    /// <summary>
    /// 车间成品「耐久」加成 StatPart（落地管线 Step 5c）。
    ///
    /// 挂在原版 <c>MaxHitPoints</c> 上：调质渗碳 / 磷化电镀让成品的耐久上限提高。
    /// 判定只看「这件东西是不是登记的车间产出」，未登记 → 直接返回，零影响。
    /// 热路径成本 = 一次字典查找 + 一次乘法（成品数量有界，见 WorkshopProductRegistry）。
    /// </summary>
    public class StatPart_WorkshopMaxHp : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Thing thing = req.Thing;
            if (thing == null)
            {
                return;
            }
            WorkshopProductBonus bonus;
            if (!WorkshopBonusAccess.TryGetProductBonus(thing.thingIDNumber, out bonus))
            {
                return;
            }
            if (bonus.maxHpFactor > 1.0001f)
            {
                val *= bonus.maxHpFactor;
            }
        }

        /// <summary>说明文本（2026-09-11 补：此前恒 null ⇒ 物品信息里看不到车间加成来源）。</summary>
        public override string ExplanationPart(StatRequest req)
        {
            Thing thing = req.Thing;
            if (thing == null)
            {
                return null;
            }
            WorkshopProductBonus bonus;
            if (!WorkshopBonusAccess.TryGetProductBonus(thing.thingIDNumber, out bonus)
                || bonus.maxHpFactor <= 1.0001f)
            {
                return null;
            }
            int percent = (int)((bonus.maxHpFactor - 1f) * 100f);
            return percent <= 0
                ? null
                : "PersonalChronicle.Workshop.Product.Hp.StatPart".Translate(percent);
        }
    }

    /// <summary>
    /// 车间成品「精度」加成 StatPart（落地管线 Step 5c）。
    ///
    /// 挂在四个 Accuracy* 上（Touch/Short/Medium/Long）：
    /// 枪管膛线与调试校准让成品弹道更一致。
    /// 与 <see cref="StatPart_WorkshopMaxHp"/> 同源，只是读另一个系数。
    /// </summary>
    public class StatPart_WorkshopAccuracy : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Thing thing = req.Thing;
            if (thing == null)
            {
                return;
            }
            WorkshopProductBonus bonus;
            if (!WorkshopBonusAccess.TryGetProductBonus(thing.thingIDNumber, out bonus))
            {
                return;
            }
            if (bonus.accuracyFactor > 1.0001f)
            {
                val *= bonus.accuracyFactor;
            }
        }

        /// <summary>说明文本（2026-09-11 补）。</summary>
        public override string ExplanationPart(StatRequest req)
        {
            Thing thing = req.Thing;
            if (thing == null)
            {
                return null;
            }
            WorkshopProductBonus bonus;
            if (!WorkshopBonusAccess.TryGetProductBonus(thing.thingIDNumber, out bonus)
                || bonus.accuracyFactor <= 1.0001f)
            {
                return null;
            }
            int percent = (int)((bonus.accuracyFactor - 1f) * 100f);
            return percent <= 0
                ? null
                : "PersonalChronicle.Workshop.Product.Accuracy.StatPart".Translate(percent);
        }
    }
}

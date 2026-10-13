using PersonalChronicle.Application.Effects;
using RimWorld;
using Verse;

namespace PersonalChronicle.Effects
{
    /// <summary>
    /// 专业通用 Stat 加成 StatPart（V2.0 §11 适配层，2026-08-22 v2.0 新增）。
    ///
    /// 由 <see cref="ProfessionalEffectStatPartInjector"/> 注入到「技能声明 statDefName」对应的官方
    /// StatDef.parts（成功率/移动/交易/战斗/产量等 Pawn 常驻 Stat）。
    /// TransformValue 聚合当前 statDefName 上所有通用 kind（StatBonus/CombatStat/YieldBonus）的加成：
    ///   factor = Π kind 的 (1 ± Σvalue)。
    /// 负向 Stat（isReduction）经 Resolver 返回 (1 - value)。
    ///
    /// 引擎事实：StatDef.parts 为 public List&lt;StatPart&gt;；TransformValue 每 tick 评估。
    /// </summary>
    public class StatPart_ProfessionalStatBonus : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Pawn pawn = req.Pawn;
            if (pawn == null || pawn.Faction == null || !pawn.Faction.IsPlayer)
            {
                return;
            }
            if (parentStat == null)
            {
                return;
            }
            float factor = ProfessionalEffectService.GetGenericStatFactor(pawn, parentStat.defName);
            if (UnityEngine.Mathf.Abs(factor - 1f) <= 0.0001f)
            {
                return;
            }
            val *= factor;
        }

        public override string ExplanationPart(StatRequest req)
        {
            Pawn pawn = req.Pawn;
            if (pawn == null || pawn.Faction == null || !pawn.Faction.IsPlayer)
            {
                return null;
            }
            if (parentStat == null)
            {
                return null;
            }
            float factor = ProfessionalEffectService.GetGenericStatFactor(pawn, parentStat.defName);
            if (UnityEngine.Mathf.Abs(factor - 1f) <= 0.0001f)
            {
                return null;
            }
            int percent = (int)((factor - 1f) * 100f);
            if (percent == 0)
            {
                return null;
            }
            string key = percent > 0
                ? "PersonalChronicle.ProfessionalEffect.StatBonus.StatPart"
                : "PersonalChronicle.ProfessionalEffect.StatBonus.StatPartReduction";
            return key.Translate(percent);
        }
    }
}

using PersonalChronicle.Application.Effects;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Capture.Effects
{
    /// <summary>
    /// 专业制造速度 StatPart（V2.0 §11 适配层，速度效果挂载点）。
    ///
    /// 由 <see cref="ProfessionalEffectRegistry"/> 注入到白名单配方的 workSpeedStat.parts。
    /// 每次 RimWorld 评估该 stat（制造 tick 反复调用）时执行 TransformValue：
    ///   仅当 pawn 为玩家派系 + 拥有声明 WorkSpeed 效果的技能(level≥1) +
    ///   正在制造(JobDriver_DoBill) 且当前 recipe 命中该技能白名单 → 乘算加成。
    /// 任一不满足即零影响（val 不变）。
    ///
    /// 引擎事实（反射核验）：Pawn_JobTracker.curDriver/curJob、Job.bill、Bill.recipe 均 public。
    /// </summary>
    public class StatPart_ProfessionalWorkSpeed : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Pawn pawn = req.Pawn;
            if (pawn == null || pawn.Faction == null || !pawn.Faction.IsPlayer)
            {
                return;
            }
            // 仅制造场景生效：当前正在执行制造 job 且拿得到配方
            RecipeDef recipe = CurrentRecipeFor(pawn);
            if (recipe == null)
            {
                return;
            }
            float factor = ProfessionalEffectService.GetSpeedFactor(pawn, recipe);
            if (factor <= 1.0001f)
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
            RecipeDef recipe = CurrentRecipeFor(pawn);
            if (recipe == null)
            {
                return null;
            }
            float factor = ProfessionalEffectService.GetSpeedFactor(pawn, recipe);
            if (factor <= 1.0001f)
            {
                return null;
            }
            int percent = (int)((factor - 1f) * 100f);
            if (percent <= 0)
            {
                return null;
            }
            return "PersonalChronicle.ProfessionalEffect.WorkSpeed.StatPart".Translate(percent);
        }

        /// <summary>
        /// 取 pawn 当前正在制造的配方；非制造场景返回 null。
        /// </summary>
        private static RecipeDef CurrentRecipeFor(Pawn pawn)
        {
            if (pawn.jobs == null || pawn.jobs.curDriver == null)
            {
                return null;
            }
            JobDriver_DoBill doBill = pawn.jobs.curDriver as JobDriver_DoBill;
            if (doBill == null)
            {
                return null;
            }
            Job job = pawn.jobs.curJob;
            if (job == null || job.bill == null)
            {
                return null;
            }
            return job.bill.recipe;
        }
    }
}

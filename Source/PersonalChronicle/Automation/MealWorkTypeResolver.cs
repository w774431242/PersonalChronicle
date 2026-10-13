using PersonalChronicle.Domain.Meal;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// CHG-20261004-MEALSCHEDULE-D · 「用餐」工作类型的唯一 Def 解析点。
    ///
    /// 写档位的 MapComponent_MealSchedule 与发 Job 的 WorkGiver_Meal 都需要同一个 WorkTypeDef；
    /// 解析失败属降级场景（Def 改名 / 未加载），必须「只告警一次、绝不抛异常」。
    /// 两处各写一份会得到两套告警状态与两份缓存，违反 GOV-005「不重复实现」。
    /// </summary>
    internal static class MealWorkTypeResolver
    {
        private static WorkTypeDef cached;
        private static bool warnedMissing;

        /// <summary>取「用餐」工作类型；未加载时返回 null（调用方必须自行降级）。</summary>
        public static WorkTypeDef Get()
        {
            if (cached != null)
            {
                return cached;
            }
            WorkTypeDef def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(
                MealScheduleDefaults.MealWorkTypeDefName);
            if (def == null)
            {
                if (!warnedMissing)
                {
                    warnedMissing = true;
                    Log.Warning("[PersonalChronicle] Meal work type not found (meal schedule disabled): "
                        + MealScheduleDefaults.MealWorkTypeDefName);
                }
                return null;
            }
            cached = def;
            return cached;
        }
    }
}
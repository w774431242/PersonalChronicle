using HarmonyLib;
using PersonalChronicle.Automation;
using PersonalChronicle.Domain.Meal;
using RimWorld;
using System.Reflection;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// CHG-20261004-MEALSCHEDULE-B · 玩家改动作息表后<b>立即</b>重算吃饭档位。
    ///
    /// <para><b>为什么需要</b>：玩家在管制表里把某几小时涂成「用餐」后，
    /// 该 pawn 的"是否在饭点"立刻改变，而 <c>MapComponent_MealSchedule</c> 的 tick 差分
    /// 只在<b>整点规则档位变化</b>时重算 ⇒ 逐人 timetable 改动若不唤醒，会延迟到下一个餐段
    /// （最坏一整天）才生效。这里在原版写入口 Postfix 唤醒一次。</para>
    ///
    /// <para><b>为什么 patch 这里</b>：<c>Pawn_TimetableTracker.SetAssignment</c> 是原版
    /// <b>唯一</b>的时段写入入口（点击格子、Copy/Paste、读档都经它），
    /// 在此挂一次即覆盖全部写入路径，无需重复挂钩（GOV-005）。</para>
    ///
    /// <para><b>成本</b>：只在玩家<b>真的改动某小时</b>时触发（低频事件），
    /// 每次触发一次本图 O(殖民者数) 重算；不在任何 tick 热路径（GATE-PERF）。</para>
    /// </summary>
    [HarmonyPatch(typeof(Pawn_TimetableTracker), nameof(Pawn_TimetableTracker.SetAssignment))]
    internal static class Patch_PawnColumnWorker_Timetable
    {
        /// <summary>
        /// <c>Pawn_TimetableTracker.pawn</c> 是 <b>private</b> 字段（Cecil 核实），且原版没有
        /// 任何公开的 getter ⇒ 用一次性缓存的 FieldInfo 读取。
        /// <para>取不到时<b>静默跳过</b>（功能退化为"下一个餐段再生效"），不抛异常打断 UI。</para>
        /// </summary>
        private static readonly FieldInfo PawnField = typeof(Pawn_TimetableTracker)
            .GetField("pawn", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void Postfix(Pawn_TimetableTracker __instance, int hour, TimeAssignmentDef ta)
        {
            if (PawnField == null)
            {
                return;
            }
            if (ta == null || ta.defName != MealScheduleDefaults.MealTimeAssignmentDefName)
            {
                // 只关心"被涂成吃饭"的瞬间；涂成别的类型不需要重算（饭点只会减少，不会增加）。
                return;
            }
            Pawn pawn = PawnField.GetValue(__instance) as Pawn;
            if (pawn == null || pawn.Map == null)
            {
                return;
            }
            MapComponent_MealSchedule component = pawn.Map.GetComponent<MapComponent_MealSchedule>();
            if (component != null)
            {
                component.Notify_ColonistsChanged();
            }
        }
    }
}
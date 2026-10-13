using HarmonyLib;
using Verse.AI;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// CHG-20261004-MEALSCHEDULE-C · 把 1.6 的 <c>Pawn_JobTracker.Notify_WorkTypeDisabled</c>
    /// 桩方法补成 <b>no-op</b>（EXC-2026-023）。
    ///
    /// <para><b>为什么必须修</b>：<c>Pawn_WorkSettings.SetPriority(w, 0)</c> 的 IL 实证是
    /// <c>priority == 0 → pawn.jobs.Notify_WorkTypeDisabled()</c>，而 1.6 里
    /// <c>Verse.AI.Pawn_JobTracker.Notify_WorkTypeDisabled(WorkTypeDef)</c> 是
    /// <b>未实现的桩</b>（public 但方法体抛 <c>NullReferenceException: The method or operation
    /// is not implemented</c>）。⇒ <b>任何把工作档位清零的调用都会抛异常</b>：
    /// 实机 Player.log 证据 —— 餐段离场时 PC 写 <c>SetPriority(PC_MealBreak, 0)</c> 即抛，
    /// 堆栈直指 <c>MapComponent_MealSchedule.ApplyToAll</c>。</para>
    ///
    /// <para><b>为什么不能换 API 绕开</b>：<c>Pawn_WorkSettings.Disable(w)</c> 的 IL 就是
    /// <c>SetPriority(w, 0)</c>（同一路径）；而 <c>DisableAll()</c> 虽然安全
    /// （直接 <c>priorities.SetAll(0)</c>，压根不通知），但它会清掉<b>全部</b>工作类型的档位，
    /// 绝不能用于"只清一个类型"。所以在 1.6 下唯一正确的做法是补全这个通知。</para>
    ///
    /// <para><b>为什么 no-op 是安全语义</b>：该方法的原意是"某工作类型被禁用 ⇒ 中断正在跑的
    /// 相关 Job"。1.6 把它留成桩，说明引擎自己也没走这条路径（否则每次清零都会崩）。
    /// 而 PC 的产品语义正是<b>不打断正在跑的 Job</b>（餐段到点只影响"下一个 Job"的选择），
    /// 与 no-op 完全一致。另外"玩家手动取消勾选"走的也是这条路径 —— 补上之后，
    /// 玩家在工作面板里取消一个类型不再触发红字。</para>
    ///
    /// <para><b>连带修好</b>：车间<b>接管</b>通道（<c>MapComponent_WorkshopManager.WorkTakeover</c>）
    /// 与其还原路径同样用 <c>SetPriority(x, 0)</c> ⇒ 在本补丁之前它们也会抛同一异常。</para>
    /// <para>⚠ 2026-10-05 更正：原文写的是"与**降档还原**同样用 <c>SetPriority(x, 0)</c>"——
    /// 两处事实有误：① 降档还原写的是 **4**（`LowestPriority`）不是 0；
    /// ② 降档通道已随 `CHG-20261005-NODEMOTE` 整体移除。现表述已改为"接管通道及其还原路径"。</para>
    /// </summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.Notify_WorkTypeDisabled))]
    internal static class Patch_PawnJobTracker_WorkTypeDisabled
    {
        /// <summary>跳过原版（未实现桩）。返回 <c>false</c> = 不执行原方法。</summary>
        private static bool Prefix()
        {
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using PersonalChronicle.Domain.Meal;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// CHG-20261004-MEALSCHEDULE · 三餐作息的 WorkGiver。
    ///
    /// 用原版 JobDefOf.Ingest 而不自造 JobDriver（AI-003）：原版进食 Job
    /// （JobDriver_Ingest + Toils_Ingest）已处理取食物/找座/进食/释放预约等全部细节。
    /// 本类只决定「现在该不该去吃、去吃哪一份」。
    ///
    /// <para><b>两道准入闸门，缺一不可</b>（2026-10-04 实机事故教训）：</para>
    /// <list type="number">
    /// <item><b>ShouldSkip（第一道）</b>：原版工作链在遍历 WorkGiver <b>之前</b> 调用
    /// <c>JobGiver_Work.PawnCanUseWorkGiver</c> → <c>WorkGiver.ShouldSkip</c>（IL_01E7）。
    /// 退避必须写在这里，写在 <see cref="NonScanJob"/> 里是<b>错位</b>的：那样每个 WorkGiver
    /// 轮次仍会被调用一次，治不了"一 tick 连发 10 个 Job"的空转。</item>
    /// <item><b>CanActuallyEat（第二道）</b>：食物"能吃"≠"吃得下去"。地图上没有椅子、
    /// 食物旁也没有合法站立格时，<c>JobDriver_Ingest</c> 会在第一个 toil 就失败
    /// （<c>Toils_Ingest.TryFindChairOrSpot</c> 返回 false，IL 核实）⇒ 必须先验再加。</item>
    /// </list>
    /// </summary>
    public class WorkGiver_Meal : WorkGiver
    {
        /// <summary>
        /// 首个进食 Job 失败后的退避时长（tick）。250 ≈ 4 游戏秒。
        /// <para>原版 <c>WorkGiver_Scanner</c> 靠 <c>HasJobOnThing</c> 事前验证目标可行性来避免
        /// 死循环；本类直接 MakeJob，必须自己补这个闸门。</para>
        /// </summary>
        private const int BaseCooldownTicks = 250;

        /// <summary>退避上限（tick）。2500 ≈ 40 游戏秒：连续失败时最多浪费这么久一次。</summary>
        private const int MaxCooldownTicks = 2500;

        /// <summary>
        /// 一轮里最多试几份食物（每份都要跑一次 <c>CanActuallyEat</c> 的两次寻路）。
        /// 3 份足以在"最近的三份都塞在岩壁边"时找到第四份；再多是浪费。
        /// </summary>
        private const int MaxFoodAttempts = 3;

        /// <summary>
        /// 判定"上一个 Job 确实失败了"的宽限期（tick）。
        /// <para>原版 <c>Pawn_JobTracker</c> 让一个 Job 至少存活若干 tick 才允许被替换；
        /// 这里留同样的宽限，避免把"刚接手还没走两步"误判成失败。</para>
        /// </summary>
        private const int FailDetectGraceTicks = 60;

        /// <summary>每 pawn 的退避状态（key = pawn.thingIDNumber）。</summary>
        private struct Cooldown
        {
            /// <summary>退避到哪个 tick。</summary>
            public int UntilTick;

            /// <summary>连续失败次数（用于指数退避 + 上限钳制）。</summary>
            public int FailStreak;
        }

        /// <summary>
        /// 退避台账。容量有界（PERF-001）：超出即整体重建，
        /// 语义上只是"忘记历史 ⇒ 退避从头开始"，不会破坏正确性。
        /// </summary>
        private static readonly Dictionary<int, Cooldown> cooldowns = new Dictionary<int, Cooldown>();

        private const int CooldownCapacity = 64;

        /// <summary>本轮已判定"吃下去会失败"的候选（复用原版 ClosestThing 的排除参数）。</summary>
        private static readonly List<Thing> excludedScratch = new List<Thing>(MaxFoodAttempts);

        /// <summary>
        /// 第一道闸门：原版在遍历本 WorkGiver 之前调用。
        /// <para>
        /// <b>返回 true = 跳过本 WorkGiver</b>（不是"什么都不做"，而是让工作链去看下一个
        /// 工作类型——这正是"到饭点但吃不上"时殖民者该去干别的事的语义）。
        /// </para>
        /// </summary>
        public override bool ShouldSkip(Pawn pawn, bool forced)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Downed)
            {
                return true;
            }
            if (pawn.Map == null || !pawn.IsFreeColonist)
            {
                return true;
            }
            // 唯一判据 = 「用餐」工作档位 > 0（谁写档位谁生效，三个入口自然一致）。
            WorkTypeDef mealWorkType = MealWorkTypeResolver.Get();
            if (mealWorkType == null)
            {
                return true;   // Def 缺失：降级为不介入（解析器已告警一次）
            }
            if (pawn.workSettings == null || pawn.workSettings.GetPriority(mealWorkType) <= 0)
            {
                return true;
            }
            // 不饿 ⇒ 饭点到了也不驱动（原版饥饿兜底与本功能互不干扰）。
            Need_Food foodNeed = pawn.needs != null ? pawn.needs.food : null;
            if (foodNeed == null || foodNeed.CurLevel >= MealScheduleDefaults.HungerTrigger)
            {
                return true;
            }

            // 正在吃 ⇒ 放行（让原版正常持有并跑完当前 Job，不打断）。
            Job cur = pawn.CurJob;
            if (cur != null && cur.def == JobDefOf.Ingest)
            {
                return false;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            int pawnId = pawn.thingIDNumber;

            if (cooldowns.TryGetValue(pawnId, out Cooldown cd))
            {
                // 上次发的 Job 没还在跑 ⇒ 判定失败，累加失败次数并延长退避。
                // 只有超过宽限期才判，避免误伤"刚接手"的正常 Job。
                if (now < cd.UntilTick)
                {
                    return true;
                }
                if (cd.FailStreak > 0 && now - cd.UntilTick < FailDetectGraceTicks)
                {
                    cd.FailStreak = Math.Min(cd.FailStreak + 1, MaxCooldownTicks / BaseCooldownTicks);
                    cd.UntilTick = now + Math.Min(BaseCooldownTicks * cd.FailStreak, MaxCooldownTicks);
                    cooldowns[pawnId] = cd;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 非扫描式取 Job。没有「可扫的目标物」，故不继承 WorkGiver_Scanner，
        /// WorkGiverDef 上 scanThings=false / scanCells=false。
        /// </summary>
        public override Job NonScanJob(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Downed
                || pawn.Map == null || !pawn.IsFreeColonist)
            {
                return null;
            }
            WorkTypeDef mealWorkType = MealWorkTypeResolver.Get();
            if (mealWorkType == null
                || pawn.workSettings == null
                || pawn.workSettings.GetPriority(mealWorkType) <= 0)
            {
                return null;
            }
            // 已经在吃 ⇒ 不重复下发。
            Job cur = pawn.CurJob;
            if (cur != null && cur.def == JobDefOf.Ingest)
            {
                return null;
            }
            Need_Food foodNeed = pawn.needs != null ? pawn.needs.food : null;
            if (foodNeed == null || foodNeed.CurLevel >= MealScheduleDefaults.HungerTrigger)
            {
                return null;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            int pawnId = pawn.thingIDNumber;

            // 第二道闸门：找一份"现在真的吃得下去"的食物。
            // 最近的几份可能全都塞在岩壁边 / 无处落座 ⇒ 逐份排除后重试，
            // 避免对着同一份永远吃不掉的食物空转（2026-10-04 实机事故的根因修复）。
            Thing food = null;
            excludedScratch.Clear();
            for (int attempt = 0; attempt < MaxFoodAttempts; attempt++)
            {
                Thing candidate = MealFoodSearch.FindFood(pawn, pawn.Map, excludedScratch);
                if (candidate == null)
                {
                    break;
                }
                if (MealFoodSearch.CanActuallyEat(pawn, candidate))
                {
                    food = candidate;
                    break;
                }
                excludedScratch.Add(candidate);
            }
            excludedScratch.Clear();

            if (food == null)
            {
                return null;   // 附近没有吃得下去的东西 ⇒ 本轮不驱动，交还原版饥饿兜底
            }

            // 发 Job 前登记退避窗口（连续失败时按指数延长，见 ShouldSkip）。
            if (cooldowns.Count >= CooldownCapacity)
            {
                cooldowns.Clear();
            }
            Cooldown state;
            cooldowns.TryGetValue(pawnId, out state);
            state.FailStreak = 0;
            state.UntilTick = now + BaseCooldownTicks;
            cooldowns[pawnId] = state;

            return JobMaker.MakeJob(JobDefOf.Ingest, food);
        }
    }
}

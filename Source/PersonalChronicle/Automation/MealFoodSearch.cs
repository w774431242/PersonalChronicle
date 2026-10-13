using PersonalChronicle.Domain.Meal;
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// CHG-20261004-MEALSCHEDULE · 「找一份能吃的」<b>唯一实现</b>（引擎侧）。
    ///
    /// <para><b>为什么单独成文件</b>：本类需要 <c>Verse</c> 的枚举（<c>Danger</c>/<c>RegionType</c>/
    /// <c>TraverseMode</c>）与 <c>GenClosest</c>，属引擎适配；而 <c>Domain.Meal</c> 必须保持
    /// 「不引用 Verse」才能离线单测（09 §5 红线）。两者混在一处会污染 Domain。</para>
    ///
    /// <para><b>参数逐项对齐原版</b>（Cecil 核实 Build 1.6.4871，
    /// <c>RimWorld.JobGiver_EatRandom::TryGiveJob</c> 的 IL 实参）⇒ PC 与原版"愿意走多远、
    /// 愿意冒多大险"完全一致，不会出现"原版肯去吃、PC 不肯"的不一致。</para>
    /// </summary>
    internal static class MealFoodSearch
    {
        /// <summary>危险度：原版 IL <c>ldc.i4.3</c> ⇒ <c>Danger.Deadly</c>。</summary>
        private const Danger MaxDanger = Danger.Deadly;

        /// <summary>遍历模式：原版 IL <c>ldc.i4.0</c> ⇒ <c>TraverseMode.ByPawn</c>。</summary>
        private const TraverseMode Traverse = TraverseMode.ByPawn;

        /// <summary>终点模式：原版 IL <c>ldc.i4.1</c> ⇒ <c>PathEndMode.OnCell</c>。</summary>
        private const PathEndMode PathEnd = PathEndMode.OnCell;

        /// <summary>可通行区域类型：原版 IL <c>ldc.i4.s 14</c> ⇒ <c>RegionType.Set_Passable</c>。</summary>
        private const RegionType Region = RegionType.Set_Passable;

        /// <summary>区域搜索下限：原版 IL <c>ldc.i4.0</c>。</summary>
        private const int RegionsMin = 0;

        /// <summary>区域搜索上限：原版 IL <c>ldc.i4.m1</c>（-1 = 不限）。</summary>
        private const int RegionsMax = -1;

        /// <summary>
        /// 检索请求：原版 IL <c>ThingRequest.ForGroup(ldc.i4.4)</c>
        /// ⇒ <c>ThingRequestGroup.HaulableAlways</c>，再由 <see cref="IsEdibleFor"/> 筛掉不可吃的。
        /// </summary>
        private static readonly ThingRequest FoodRequest =
            ThingRequest.ForGroup(ThingRequestGroup.HaulableAlways);

        /// <summary>找最近的、此刻能吃且能预约的食物；找不到返回 null（调用方保持不发 Job）。</summary>
        /// <param name="excluded">要排除的候选（已判定"吃下去"失败的）。原版就是用这个参数
        /// 排掉已预约目标的；PC 复用它来排掉"无处可就餐"的目标。null 等价于不排除。</param>
        public static Thing FindFood(Pawn pawn, Map map, List<Thing> excluded = null)
        {
            if (pawn == null || map == null)
            {
                return null;
            }
            TraverseParms parms = TraverseParms.For(
                pawn,
                MaxDanger,
                Traverse,
                false,   // canBashDoors（原版 false）
                false,   // alwaysUseAvoidGrid（原版 false）
                false,   // canBashFences（原版 false）
                true);   // avoidPersistentDanger（原版 true）

            return GenClosest.ClosestThingReachable(
                pawn.Position,
                map,
                FoodRequest,
                PathEnd,
                parms,
                MealScheduleDefaults.MaxFoodSearchDistance,
                t => IsEdibleFor(t, pawn),
                excluded,
                RegionsMin,
                RegionsMax,
                false,   // forceAllowGlobalSearch
                Region,
                false,   // ignoreEntirelyForbiddenRegions
                false);  // lookInHaulSources
        }

        /// <summary>
        /// 「这份食物现在<b>吃得下去</b>吗」——原版执行器的准入闸门（2026-10-04 实机事故修复）。
        ///
        /// <para><b>为什么必须加这道检查</b>：<c>IsEdibleFor</c> 只回答"食物本身能不能吃"
        /// （可食 / 种族可食 / 可预约），<b>不回答"有没有地方吃"</b>。而
        /// <c>JobDriver_Ingest</c> 的第一步 toil 就会失败于后者：
        /// <c>Toils_Ingest.TryFindChairOrSpot(pawn, ingestible, out cell)</c> 的 IL 显示——</para>
        /// <list type="bullet">
        /// <item><c>radius = ingestible.def.ingestible.chairSearchRadius</c>：<b>大于 0</b> 时
        /// 先找椅子（<c>GenClosest</c> 搜 <c>ThingRequestGroup.HaulableAlways</c>，半径 = 该值）；</item>
        /// <item>找不到椅子才退而求其次 <c>RCellFinder.SpotToChewStandingNear(pawn, ingestible)</c>
        /// 找"站在食物旁吃"的格位；</item>
        /// <item>两者都失败 ⇒ <c>cell</c> 保持 <c>IntVec3.Invalid</c> ⇒ 返回 <c>false</c>
        /// ⇒ Job 立刻结束（空转），而工作链会立刻重取 Job。</li>
        /// </list>
        ///
        /// <para><b>实机症状（Player.log 实证）</b>：啤酒堆紧贴岩壁，<c>chairSearchRadius &gt; 0</c>
        /// 找不到椅子、身旁也无合法站立格 ⇒ 每 250 tick 发 10 个同目标 Ingest Job 全部空转：
        /// <c>"Javier started 10 jobs in one tick … A = Thing_Beer4203"</c>（Job 号连续到 14000+）。
        /// PC 的退避只降低了频率，<b>没有断掉根因</b>。</para>
        ///
        /// <para><b>为什么不在 IsEdibleFor 谓词里做</b>：谓词会被
        /// <c>GenClosest.ClosestThingReachable</c> 对<b>每个候选反复调用</b>，而本方法内部含
        /// <c>GenClosest</c> + <c>RCellFinder</c> 两次寻路 ⇒ 复杂度放大到 O(候选数²)，踩 PERF-001。
        /// 正确做法是<b>选中之后只查一次</b>，失败则把该目标塞进 <c>excluded</c> 重新找。</para>
        /// </summary>
        /// <param name="pawn">进食者。</param>
        /// <param name="food">候选食物（须已通过 <see cref="IsEdibleFor"/>）。</param>
        /// <returns>true = 有椅子或合法站立位，这份食物现在吃得下去。</returns>
        public static bool CanActuallyEat(Pawn pawn, Thing food)
        {
            if (pawn == null || food == null)
            {
                return false;
            }
            // 原版内部只在"找不到椅子"这一支里可能 Log.Error；这里提前用 CanEverEat
            // 与 IngestibleNow 挡住最常见的无意义调用，省掉一次寻路。
            if (food.Destroyed || !food.IngestibleNow)
            {
                return false;
            }
            if (pawn.RaceProps == null || !pawn.RaceProps.CanEverEat(food.def))
            {
                return false;
            }
            return Toils_Ingest.TryFindChairOrSpot(pawn, food, out _);
        }

        /// <summary>
        /// 「这份食物现在能不能给这个 pawn 吃」。
        ///
        /// <para>四条判据与原版 <c>JobGiver_EatRandom</c> 的谓词闭包<b>逐条一致</b>
        /// （IL：<c>ldfld category → ldc.i4.2(ThingCategory.Item)</c> → <c>get_IngestibleNow</c>
        /// → <c>RaceProps.CanEverEat</c> → <c>ReservationUtility.CanReserve(...,1,-1,null,0)</c>）。</para>
        /// </summary>
        public static bool IsEdibleFor(Thing t, Pawn pawn)
        {
            if (t == null || t.def == null || pawn == null || pawn.RaceProps == null)
            {
                return false;
            }
            if (t.def.category != ThingCategory.Item)
            {
                return false;
            }
            if (!t.IngestibleNow)
            {
                return false;
            }
            if (!pawn.RaceProps.CanEverEat(t.def))
            {
                return false;
            }
            if (PersonalChronicle.Economy.PawnEconomy.GameComponent_EconomyStorage.Current?.CanSelectFood(t, pawn) == false)
                return false;
            return ReservationUtility.CanReserve(
                pawn,
                t,
                MealScheduleDefaults.FoodReserveMaxPawns,
                MealScheduleDefaults.FoodReserveStackCount,
                null,
                false);
        }
    }
}

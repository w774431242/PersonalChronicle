using System.Collections.Generic;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// CHG-20261005-AUTOASSIGN · 自动分配工作优先级（手动派发）。
    ///
    /// <para><b>「直接实现班次」的关键</b>：本服务最终只调
    /// <c>Pawn_WorkSettings.SetPriority(WorkTypeDef,int)</c> —— 与玩家手点原版格子
    /// <b>完全同一个入口、同一套语义</b>，不另造任何 UI/ 事件/ 状态。</para>
    ///
    /// <para><b>班次协同（车间域权威）</b>：凡被本地图任何班次覆盖的工作类型
    /// （<c>WorkshopTypeDef.WorkTypeDefNamesOf(shift.TaskType)</c> + 值勤类型）
    /// 一律<b>跳过不写</b>。理由：<c>MapComponent_WorkshopManager.WorkEnable.EnableWorkTypes</c>
    /// 已在按班次写这些格位（值勤 3、映射类型 1），自动分配若也写就是第二次改写同一格位，
    /// 玩家点一次排班变更就会被静默回滚（收敛链必然，非 bug，但体验是"改了又没改"）。
    /// 跳过 = 车间托管项的档位只由车间决定。</para>
    ///
    /// <para><b>M1 退让</b>：写入前显式丢弃车间降档/接管两本台账的对应记录。
    /// 语义 = 复用既有裁决「玩家已改动 ⇒ PC 不再拥有」，<b>零新授权面</b>
    /// （<c>SetPriority(x,0)</c> 的唯一调用点仍在 WorkTakeover.cs）。</para>
    /// </summary>
    public static class AutoAssignService
    {
        /// <summary>值勤工作类型 defName（与 WorkEnable.cs 的 private const 同值，此处不复用私有常量）。</summary>
        private const string DutyWorkTypeDefName = "PC_WorkshopOperation";

        public class Result
        {
            /// <summary>实际改写的格位数。</summary>
            public int Touched;
            /// <summary>因车间托管 / 无技能映射 / 隐藏而跳过的格位数。</summary>
            public int Skipped;
            /// <summary>其中「由未勾选(0)变为已勾选」的格位数（强干预的核心计数）。</summary>
            public int NewlyEnabled;
            /// <summary>失去车间还原权的格位数（M1 退让的代价，须在预览里告知玩家）。</summary>
            public int Relinquished;
            /// <summary>因「只保留 1 项」而被取消勾选的格位数。</summary>
            public int Disabled;
        }

        /// <summary>本地图被车间班次托管的工作类型名集合（值勤 + 各班次岗位映射类型）。</summary>
        public static HashSet<string> ManagedWorkTypeDefNames(Map map)
        {
            HashSet<string> set = new HashSet<string>();
            MapComponent_WorkshopManager mgr = map != null
                ? map.GetComponent<MapComponent_WorkshopManager>()
                : null;
            List<WorkshopShift> shifts = mgr != null ? mgr.Shifts : null;
            if (shifts != null)
            {
                for (int i = 0; i < shifts.Count; i++)
                {
                    WorkshopShift s = shifts[i];
                    if (s == null || s.TargetZone == null || s.TargetZone.WorkshopType == null)
                    {
                        continue;
                    }
                    List<string> names = s.TargetZone.WorkshopType.WorkTypeDefNamesOf(s.TaskType);
                    if (names == null)
                    {
                        continue;
                    }
                    for (int k = 0; k < names.Count; k++)
                    {
                        if (!string.IsNullOrEmpty(names[k]))
                        {
                            set.Add(names[k]);
                        }
                    }
                }
            }
            set.Add(DutyWorkTypeDefName);
            // CHG-20261004-MEALSCHEDULE：用餐类型由班次驱动机独占管理
            // （MapComponent_MealSchedule 按**管制表里的用餐时段**写档位，见 CHG-20261005-MEALBYSHIFT），
            // 与车间排班同属「系统托管」。
            // 不排除的话，自动分配会给每人挑一项最高优先级工作，可能把三餐类型勾成 1 档，
            // 于是殖民者一有空就跑去吃饭（而档位 2/1 在原版排序里又会恒垫底 ⇒ 行为自相矛盾）。
            set.Add(Domain.Meal.MealScheduleDefaults.MealWorkTypeDefName);
            return set;
        }

        /// <summary>执行自动分配。作用域由调用方给定（本功能不做"当前选中"）。</summary>
        public static Result Run(Map map, List<Pawn> pawns)
        {
            Result res = new Result();
            if (map == null || pawns == null || pawns.Count == 0)
            {
                return res;
            }
            ChronicleGameComponent comp = Current.Game?.GetComponent<ChronicleGameComponent>();
            if (comp == null)
            {
                return res;
            }

            HashSet<string> managed = ManagedWorkTypeDefNames(map);
            // AllDefsListForReading 是原版公开的 List<T> 入口（Cecil 核实：字段 defsList 的公开包装）。
            List<WorkTypeDef> all = DefDatabase<WorkTypeDef>.AllDefsListForReading;
            if (all == null)
            {
                return res;
            }

            for (int pi = 0; pi < pawns.Count; pi++)
            {
                Pawn pawn = pawns[pi];
                if (pawn == null || pawn.workSettings == null || !pawn.IsFreeColonist)
                {
                    continue;
                }

                // ---- 第一遍：算档位，找最优的那 1 项 ----
                WorkTypeDef best = null;
                int bestPriority = int.MaxValue;
                for (int wi = 0; wi < all.Count; wi++)
                {
                    WorkTypeDef wt = all[wi];
                    if (wt.defName == null || managed.Contains(wt.defName))
                    {
                        res.Skipped++;
                        continue;
                    }
                    float avg = pawn.skills.AverageOfRelevantSkillsFor(wt);
                    Passion passion = pawn.skills.MaxPassionOfRelevantSkillsFor(wt);
                    int target;
                    if (!AutoAssignPriority.TryCompute(wt, avg, passion, out target))
                    {
                        res.Skipped++;
                        continue;
                    }
                    if (target < bestPriority)
                    {
                        bestPriority = target;
                        best = wt;
                    }
                }
                if (best == null)
                {
                    continue;
                }

                // ---- 第二遍：写入。规则 =「只保留最优 1 项，其余取消勾选」----
                // 这样离班时（车间类被 OffShift pass 取消）该殖民者无任何可做之事 ⇒ 直接去睡觉。
                for (int wi = 0; wi < all.Count; wi++)
                {
                    WorkTypeDef wt = all[wi];
                    if (wt.defName == null || managed.Contains(wt.defName))
                    {
                        continue;
                    }
                    // 保护名单（搬运/清洁/医疗/灭火/患者照护）永不取消：
                    // 它们是殖民者的生存底线（搬食物、清污、看病），与"只做一件事"无关。
                    if (AutoAssignPriority.IsProtected(wt))
                    {
                        continue;
                    }
                    // 无技能映射的类型保持原样（S3，见 AutoAssignPriority 类注释）。
                    if (wt.relevantSkills == null || wt.relevantSkills.Count == 0)
                    {
                        continue;
                    }
                    int current = pawn.workSettings.GetPriority(wt);

                    if (wt == best)
                    {
                        if (current == bestPriority)
                        {
                            continue;
                        }
                        if (WriteCell(comp, pawn, wt, current, bestPriority))
                        {
                            res.Relinquished++;
                        }
                        res.Touched++;
                        if (current <= 0 && bestPriority > 0)
                        {
                            res.NewlyEnabled++;
                        }
                        continue;
                    }

                    // 其余一律取消勾选（current > 0 时才动，幂等）
                    if (current <= 0)
                    {
                        continue;
                    }
                    if (WriteCell(comp, pawn, wt, current, 0))
                    {
                        res.Relinquished++;
                    }
                    res.Touched++;
                    res.Disabled++;
                }
                pawn.workSettings.Notify_UseWorkPrioritiesChanged();
            }
            return res;
        }

        /// <summary>
        /// 单格写入的<b>唯一出口</b>：M1 退让 + 记账 + 钳位后写入。
        ///
        /// <para><b>M1 退让必须在写 0 之前做</b>：0 是 EXC-2026-016 接管的哨兵值，
        /// 其闸门是「当前值 == 0 才还原」。若台账仍有记录，玩家点「解除接管」会把档位
        /// 还原成原值⇒ 离班期间被重新启用。退让后台账无记录 ⇒ 闸门遍历不到。</para>
        ///
        /// <para><b>internal 而非 private</b>（CHG-20261005-POSTLAND）：岗位落地
        /// （<see cref="WorkshopPostLandingService"/>）也要走同一套「退让 + 记账 + 钳位」，
        /// 各写一份会让两处对同一格位的裁决分叉（GOV-005）。</para>
        /// </summary>
        /// <returns>是否发生了 M1 退让（调用方用于计数）。</returns>
        internal static bool WriteCell(ChronicleGameComponent comp, Pawn pawn,
            WorkTypeDef wt, int current, int target)
        {
            bool relinquished = comp.DemotionLedger.Discard(pawn.thingIDNumber, wt.defName);
            comp.DiscardTakeover(pawn.thingIDNumber, wt.defName);
            comp.AutoAssignLedger.Record(pawn.thingIDNumber, wt.defName, current, target);
            pawn.workSettings.SetPriority(wt, AutoAssignPriority.Clamp(target));
            return relinquished;
        }
    }
}
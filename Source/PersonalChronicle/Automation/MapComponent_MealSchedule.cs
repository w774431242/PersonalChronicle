using PersonalChronicle.Data;
using PersonalChronicle.Domain.Meal;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// CHG-20261004-MEALSCHEDULE / -B · 「吃饭」驱动：决定每个自由殖民者的
    /// <c>PC_MealBreak</c> 工作档位该是多少。
    ///
    /// <para><b>唯一判定源（CHG-20261005-MEALBYSHIFT）</b>：看该殖民者<b>作息表的当前格</b>
    /// 是不是「用餐」（<c>Pawn_TimetableTracker.CurrentAssignment == PC_Meal</c>）——
    /// 逐人独立，正是原版作息表的语义。</para>
    ///
    /// <para>⚠ 在此之前还有第二套来源："PC 三餐作息规则"（设置页配置的全局固定餐段）。
    /// 玩家要求「删除全局三餐 mod 设置，统一口径以班次设置中的三餐时间执行」⇒ 那套规则连同
    /// 设置页一起删除。现在链条是：<b>班次配置 → 管制表（∩ 该班次工作时段）→ 档位</b>，
    /// 中间不再有按"当前几点"的旁路判定。</para>
    ///
    /// <para>命中 ⇒ 写入档位 <see cref="MealScheduleDefaults.DutyMealPriority"/>；
    /// 未命中 ⇒ 清零。<b>原版饥饿驱动完全不受影响</b>（WorkGiver_Meal 不在饭点恒返回 null）。</para>
    ///
    /// <para><b>为什么写档位而不是直接派 Job</b>：写 <c>Pawn_WorkSettings.SetPriority</c>
    /// 与玩家手点原版格子<b>是同一个入口</b>，从而继承原版工作链的全部仲裁：
    /// 不打断当前 Job、被更紧急工作抢占、尊重 simple-priorities 模式。</para>
    ///
    /// <para><b>玩家手动优先（2026-10-04 决策）</b>：格位归属由
    /// <see cref="MealPriorityLedger"/> 裁决 —— 玩家在 Work 表的「用餐」列改过之后，
    /// PC 对该殖民者永久不再写入（与车间降档/接管台账同一套语义）。</para>
    ///
    /// <para><b>性能</b>：每 tick 只比较一个整数（当前小时 vs 上次），<b>O(1)</b>；
    /// 仅当小时变化或收到唤醒时才遍历 <c>MapPawns.FreeColonists</c>。
    /// 玩家在管制表逐人排班属于低频事件（每次点击触发一次重算，
    /// 由 <see cref="Notify_ColonistsChanged"/> 显式唤醒）。</para>
    /// </summary>
    public class MapComponent_MealSchedule : MapComponent
    {
        /// <summary>
        /// 上次兜底重算时的小时（-1 = 从未）。
        /// <para>CHG-20261005-MEALBYSHIFT：原先存的是"规则档位"（由全局三餐窗口按小时算出），
        /// 该规则已随全局三餐设置一并删除 ⇒ 这里改为按小时兜底（见 <see cref="MapComponentTick"/>）。</para>
        /// </summary>
        private int lastRuleHour = -1;

        /// <summary>
        /// 唯一构造入口（1.6 <c>Verse.MapComponent</c> 只有 <c>.ctor(Map)</c>，Cecil 核实）。
        /// </summary>
        public MapComponent_MealSchedule(Map map) : base(map)
        {
        }

        /// <summary>
        /// 每 tick 调用（1.6 入口名为 <c>MapComponentTick</c>，不是 <c>Tick()</c>：
        /// <c>Verse.MapComponent</c> 的虚方法集合 = MapComponentUpdate/MapComponentTick/
        /// MapComponentOnGUI/MapComponentDraw）。
        /// </summary>
        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (map == null || Find.TickManager == null)
            {
                return;
            }
            // CHG-20261005-MEALBYSHIFT：档位现在**只由管制表决定** ⇒ 不再存在"规则档位"
            // 这个每 tick 差分量。保留一条**每小时一次**的兜底重算：玩家操作都会被
            // Patch_PawnColumnWorker_Timetable 唤醒（见 Notify_ColonistsChanged），
            // 但第三方 Mod / 脚本改作息表不一定走那个补丁 ⇒ 每小时对齐一次。
            // 成本 = 每 2500 tick 遍历一次自由殖民者（人数有界），可忽略。
            int hour = GenLocalDate.HourOfDay(map);
            if (hour != lastRuleHour)
            {
                lastRuleHour = hour;
                ApplyToAll();
            }
        }

        /// <summary>
        /// 强制重算一次全图（玩家在管制表/Work 表改过排班后调用）。
        /// <para>事件驱动而非每 tick 轮询：逐人 timetable 各不相同，无法用单一差分覆盖，
        /// 因此由 UI 侧改动事件唤醒（见 <c>Patch_PawnColumnWorker_Timetable</c>），
        /// 并在读档/开局时由 <see cref="MapComponentUpdate"/> 兜底一次。</para>
        /// </summary>
        public void Notify_ColonistsChanged()
        {
            // 记住当前小时，避免紧接的 tick 立刻重复一次兜底重算（幂等，无正确性影响）。
            lastRuleHour = map != null ? GenLocalDate.HourOfDay(map) : 0;
            ApplyToAll();
        }

        /// <summary>读档/开局兜底：MapComponent 首次更新时对齐一次（此时 lastRulePriority 仍是 -1）。</summary>
        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if (lastRuleHour == -1 && map != null)
            {
                lastRuleHour = GenLocalDate.HourOfDay(map);
                ApplyToAll();
            }
        }

        /// <summary>
        /// 把档位写入本图全体自由殖民者。
        ///
        /// <para><b>只读遍历</b> <c>MapPawns.FreeColonists</c>：该属性返回 MapPawns <b>内部复用的
        /// 同一个缓存 List</b>（63 处 IL 消费点）⇒ 本方法只读取，绝不移除或替换元素。</para>
        /// </summary>
        private void ApplyToAll()
        {
            WorkTypeDef mealWorkType = ResolveMealWorkType();
            if (mealWorkType == null)
            {
                return;
            }
            List<Pawn> colonists = map.mapPawns.FreeColonists;
            if (colonists == null || colonists.Count == 0)
            {
                return;
            }

            MealPriorityLedger ledger = MealLedger();
            TimeAssignmentDef mealAssignment = ResolveMealTimeAssignment();

            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (pawn == null || pawn.Destroyed || pawn.workSettings == null)
                {
                    continue;
                }
                if (pawn.Dead || pawn.Downed)
                {
                    continue;
                }

                int desired = DesiredPriorityOf(pawn, mealAssignment);
                Pawn_WorkSettings settings = pawn.workSettings;
                int current = settings.GetPriority(mealWorkType);

                // SetPriority 对被禁用的工作类型写非 0 档会 Log.Error 并 return（IL 实证），
                // 故先做同一道守卫。
                if (desired > 0 && pawn.WorkTypeIsDisabled(mealWorkType))
                {
                    continue;
                }

                // 玩家手动优先：台账判定"这一格归谁"。玩家改过 ⇒ PC 永不写。
                if (!ledger.ShouldWrite(pawn.thingIDNumber, current, desired))
                {
                    continue;
                }
                // 单个 pawn 的写入失败不得打断整批（否则一个人出问题，全殖民地这轮都不生效）。
                // 1.6 的 SetPriority(x,0) 会走 Notify_WorkTypeDisabled 桩（已由
                // Patch_PawnJobTracker_WorkTypeDisabled 补成 no-op），此处再加一层防御：
                // 万一将来出现别的抛异常路径，也只记一次日志、不刷屏、不中断。
                try
                {
                    settings.SetPriority(mealWorkType, desired);
                    ledger.RecordWrite(pawn.thingIDNumber, desired);
                }
                catch (System.Exception ex)
                {
                    LogOnceWriteFailure(ex);
                }
            }
        }

        /// <summary>
        /// 该殖民者此刻应得的档位 —— CHG-20261005-MEALBYSHIFT 起<b>只看管制表</b>：
        /// 他的作息表当前格是「用餐」⇒ 写档位；否则 0（清零，交还原版饥饿驱动）。
        ///
        /// <para><b>为什么不再看"现在几点"</b>：全局三餐设置已删除，餐点唯一来源是
        /// <b>班次配置</b>，而班次把餐点写进管制表（<c>WorkshopDutyCoordinator.ApplyWorkHoursToCrew</c>）。
        /// 判定因此退化为一次字段读取 —— 与玩家要求"统一口径以班次设置中的三餐时间执行"
        /// 是同一条链：<b>班次 → 管制表 → 档位</b>，中间不再有第二套按小时的规则。</para>
        ///
        /// <para>顺带消除一个旧边界：原先"表里是用餐"与"全局窗口命中"是两套来源，
        /// 若玩家的表在 PC 餐段内但格不是 PC 涂的，第二次判定又会按全局小时把档位打开
        /// ⇒ 两套来源互相打架。现在只有一处判据。</para>
        ///
        /// <para>档位取 <see cref="PersonalChronicle.Domain.Meal.MealScheduleDefaults.DutyMealPriority"/>
        /// （= 正常档：压过全部原版工作，让位给紧急类）—— 原先由玩家在设置页二选一，
        /// 该设置已随全局三餐一并删除 ⇒ 收敛为固定档，避免"配置项没了但语义不明"。</para>
        /// </summary>
        private int DesiredPriorityOf(Pawn pawn, TimeAssignmentDef mealAssignment)
        {
            if (mealAssignment == null)
            {
                return 0;
            }
            Pawn_TimetableTracker tracker = pawn.timetable;
            return tracker != null && tracker.CurrentAssignment == mealAssignment
                ? PersonalChronicle.Domain.Meal.MealScheduleDefaults.DutyMealPriority
                : 0;
        }

        private static MealPriorityLedger MealLedger()
        {
            ChronicleGameComponent comp = Current.Game != null
                ? Current.Game.GetComponent<ChronicleGameComponent>()
                : null;
            // 无 GameComponent（极早期/测试环境）⇒ 返回一次性空台账，
            // 语义 = "无历史" ⇒ 判定退化为首次接管，不抛异常。
            return comp != null ? comp.MealPriorityLedger : new MealPriorityLedger();
        }

        /// <summary>写入失败只告警一次（避免每 tick 刷屏）。</summary>
        private static void LogOnceWriteFailure(System.Exception ex)
        {
            if (warnedWriteFailure)
            {
                return;
            }
            warnedWriteFailure = true;
            Log.Warning("[PersonalChronicle] Meal work-priority write failed once (further failures suppressed): "
                + ex.GetType().Name + " " + ex.Message);
        }

        private static bool warnedWriteFailure;

        // ---- Def 解析（启动期一次性缓存；失败只告警一次，不打断游戏）----

        private static WorkTypeDef ResolveMealWorkType()
        {
            // 解析收口到 MealWorkTypeResolver（与 WorkGiver_Meal 共用同一份缓存与告警状态，GOV-005）。
            return MealWorkTypeResolver.Get();
        }

        private static TimeAssignmentDef cachedMealAssignment;
        private static bool warnedMissingMealAssignment;

        /// <summary>
        /// 解析「用餐」时段类型（启动期一次性缓存）。
        /// <para><b>internal 而非 private</b>（CHG-20261004-MEALASSIGN）：车间排班写作息表时
        /// 也要涂这个时段（见 <c>WorkshopDutyCoordinator.ApplyWorkHoursToCrew</c>），
        /// 两处必须用<b>同一个</b>解析结果与告警状态（GOV-005 单一实现），故不再各写一份。</para>
        /// </summary>
        internal static TimeAssignmentDef ResolveMealTimeAssignment()
        {
            if (cachedMealAssignment != null)
            {
                return cachedMealAssignment;
            }
            cachedMealAssignment = DefDatabase<TimeAssignmentDef>.GetNamedSilentFail(
                MealScheduleDefaults.MealTimeAssignmentDefName);
            if (cachedMealAssignment == null && !warnedMissingMealAssignment)
            {
                warnedMissingMealAssignment = true;
                Log.Warning("[PersonalChronicle] Meal time assignment not found (schedule entry disabled): "
                    + MealScheduleDefaults.MealTimeAssignmentDefName);
            }
            return cachedMealAssignment;
        }
    }
}
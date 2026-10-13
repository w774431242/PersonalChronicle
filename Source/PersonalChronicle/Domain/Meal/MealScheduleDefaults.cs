namespace PersonalChronicle.Domain.Meal
{
    /// <summary>
    /// CHG-20261004-MEALSCHEDULE · 三餐作息的<b>唯一常量源</b>（GOV-006：禁散落魔法数字）。
    ///
    /// <para>下游模块一律引用本类，<b>不得</b>再写自己的 "PC_MealBreak"、10f、0.9f。</para>
    /// </summary>
    public static class MealScheduleDefaults
    {
        // ---- Def 标识（GOV-009：逻辑只依赖稳定 defName，不依赖 label/翻译文本）----

        /// <summary>隐藏工作类型 defName（Defs/Automation_WorkType.xml）。</summary>
        public const string MealWorkTypeDefName = "PC_MealBreak";

        /// <summary>工作giver defName（Defs/Automation_Jobs.xml）。</summary>
        public const string MealWorkGiverDefName = "PC_MealGiver";

        /// <summary>
        /// 吃饭<b>时段类型</b> defName（Defs/Automation_TimeAssignment.xml）——
        /// 管制/作息表里玩家可涂的「用餐」时段。
        /// <para>与 <see cref="MealWorkTypeDefName"/> 是<b>两个不同 Def</b>：
        /// 前者决定"几点吃"（时段），后者决定"吃这件事属于哪类工作"（可勾选/可排班的工作类型）。</para>
        /// </summary>
        public const string MealTimeAssignmentDefName = "PC_Meal";

        // ---- 档位 ----

        /// <summary>餐段档位「正常」：<c>naturalPriority + (4 - 3) * 100000</c> ⇒ 压过全部原版工作，但让位给紧急类。</summary>
        public const int PriorityNormal = 3;

        /// <summary>餐段档位「优先」：最高档，餐段内吃饭优先于一切普通工作。</summary>
        public const int PriorityUrgent = 4;

        /// <summary>
        /// 把任意输入夹到允许的档位集合 {3, 4}。
        /// <para>为什么只给两档：原版工作链排序键 = <c>naturalPriority + (4 - 档位) × 100000</c>，
        /// 档位每差 1 就是 100000 的量级差，而原版 <c>naturalPriority</c> 全域集中在 0~400。
        /// ⇒ 档位 2/1 会让本类型<b>恒垫底</b>（等同关闭），对玩家没有可观测差异，
        /// 暴露它们只会制造"我调了档但没变化"的假 bug。</para>
        /// </summary>
        public static int ClampPriority(int priority)
        {
            return priority == PriorityUrgent ? PriorityUrgent : PriorityNormal;
        }

        // ---- 饥饿门槛 ----

        /// <summary>
        /// 餐段内的进食触发门槛（<c>Need_Food.CurLevel</c> 的<b>上界</b>）：
        /// 低于它才驱动去吃。
        ///
        /// <para><b>为什么是 0.9 而不是原版的 0.25</b>：原版阈值回答的是"会不会饿死"，
        /// 本功能回答的是"到饭点了没" ⇒ 用接近满值的门槛才能实现"到点去吃饭"。
        /// 真正饥饿（低于原版 <c>PercentageThreshHungry</c>）时仍由原版兜底驱动。</para>
        ///
        /// <para><b>为什么不是 1.01（2026-10-04 回退）</b>：曾为实现"到点必去"取 1.01，
        /// 实机立刻刷屏 —— <c>JobDriver_Ingest</c> 对<b>已经吃饱</b>的 pawn 会立即失败
        /// （这一口吃不下），而 <c>Pawn_JobTracker</c> 失败后立刻重取 Job
        /// ⇒ Player.log 出现 "started 10 jobs in one tick … newJob=Ingest"。
        /// 即"越过饱腹线"不是"更饿"，而是"发一个注定失败的 Job"。
        /// 现回到 0.9：<b>刚吃饱不去吃饭</b>，这本来就是合理的；
        /// 真饿时（低于 0.9）无论是否在餐段都会去。</para>
        /// </summary>
        public const float HungerTrigger = 0.9f;

        // ---- 食物搜索 ----

        /// <summary>
        /// 食物搜索距离（格）。与原版 <c>JobGiver_EatRandom</c> 的 IL 常量一致（Cecil 核实：
        /// <c>ldc.r4 10</c>），刻意同值 ⇒ 两者的"愿意走多远去吃"口径完全相同。
        /// </summary>
        public const float MaxFoodSearchDistance = 10f;

        /// <summary>每餐占一个预约位的堆叠上限（与原版 IL 的 <c>CanReserve(..., 1, -1, null, 0)</c> 一致）。</summary>
        public const int FoodReserveMaxPawns = 1;

        public const int FoodReserveStackCount = -1;

        // ⚠ CHG-20261005-MEALBYSHIFT（2026-10-05 玩家要求「删除全局三餐 mod 设置，统一口径以
        //   班次设置中的三餐时间执行」）：**全局三餐的默认窗口常量已整批删除** ——
        //   MealScheduleEnabled / MealPriority / 三餐起止共 8 个设置字段、设置页「作息与用餐」
        //   的三餐时间控件、以及 MapComponent/服务层的按小时规则，全部随全局设置一起移除。
        //
        //   餐点现值有两个来源之一：**班次配置**（WorkshopShift 的 6 个 Meal* 字段）——
        //   而餐点还会被"∩ 该班次自己的工作时段"过滤（见 WorkshopDutyCoordinator.AddMealHoursOfShift），
        //   所以白班的 7–8 点不会被涂到夜班（18–6）的表上。

        /// <summary>小时下限/上限（闭区间）。</summary>
        public static int ClampHour(int hour)
        {
            if (hour < 0)
            {
                return 0;
            }
            return hour > 23 ? 23 : hour;
        }

        /// <summary>
        /// 按班次三餐执行时统一的**档位**（CHG-20261005-MEALBYSHIFT）。
        ///
        /// <para>原先由玩家在设置页二选一（正常/优先），该设置已随全局三餐一并删除
        /// ⇒ 收敛为固定值，避免"配置项没了但语义不明"。</para>
        ///
        /// <para>取<b>正常档</b>（<see cref="PriorityNormal"/>）的依据：其语义正是
        /// "压过全部原版普通工作、让位给紧急类（救火/抢救）"—— 到点去吃饭应该优先于常规生产，
        /// 但不该压过真正的紧急事务。</para>
        /// </summary>
        public const int DutyMealPriority = PriorityNormal;
    }
}
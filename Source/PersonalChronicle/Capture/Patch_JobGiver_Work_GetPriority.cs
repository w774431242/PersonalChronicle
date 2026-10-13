using HarmonyLib;
using PersonalChronicle.Domain.Meal;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// CHG-20261004-MEALSCHEDULE-D · 给<b>自定义时段类型</b>补工作链 / 睡眠决策的优先级
    /// （EXC-2026-024）。
    ///
    /// <para><b>根因（IL 实证）</b>：原版有<b>三个</b>方法把时段类型硬编码成若干分支，
    /// 其余一律 <c>throw new NotImplementedException()</c>：</para>
    /// <list type="number">
    /// <item><c>RimWorld.JobGiver_Work.GetPriority(Pawn)</c>
    /// —— Anything 5.5 / Work 9 / Sleep 3 / Joy 2 / Meditate 2，其余 throw；</item>
    /// <item><c>RimWorld.JobGiver_GetRest.GetPriority(Pawn)</c>
    /// —— Anything·Joy·Meditate 在 rest 低时 8、Work 0、Sleep 恒 8，其余 throw；</item>
    /// <item><c>RimWorld.ThinkNode_Priority_GetJoy.GetPriority(Pawn)</c>
    /// —— Anything 在 joy &lt; 0.35 时 6、Joy 在 joy &lt; 0.95 时 7、Sleep 在 joy &lt; 0.95 时 2、
    /// Meditate 0，其余 throw。<b>2026-10-04 第三次取证发现</b>（取证方式：
    /// <c>refs get_CurrentAssignment</c> 列出全部读时段的原版方法，再逐个 <c>il</c> 看是否含
    /// <c>newobj NotImplementedException</c>）：这一处恰好越过 <c>!allowJoy → 0</c> 那道前置闸门，
    /// 落进 throw 分支 ⇒ <b>「摸鱼」时段（<c>PC_Idle</c>，<c>allowJoy=true</c>）的娱乐被静默降权为 0</b>，
    /// 人仍然原地站着 —— 即"新增时段靠原版 GetJoy 自动接管"这个设计，<b>必须靠本 patch 才成立</b>。</item>
    /// </list>
    /// <para>PC 新增的「用餐」时段（<c>Defs/Automation_TimeAssignment.xml</c>）一旦被涂到
    /// 某个小时，<c>pawn.timetable.CurrentAssignment</c> 就是它 ⇒ 这两个方法都会抛。</para>
    ///
    /// <para><b>实机表现（Player.log 实证）</b>：<c>ThinkNode_PrioritySorter.TryIssueJobPackage</c>
    /// 内部有 try/catch，异常被吞<b>并按优先级 0 处理</b>，只留一行红字：</para>
    /// <code>
    /// Exception in Verse.AI.ThinkNode_PrioritySorter GetPriority: System.NotImplementedException
    ///   at RimWorld.JobGiver_GetRest.GetPriority(Verse.Pawn pawn) [0x00147]
    ///   at Verse.AI.ThinkNode_PrioritySorter.TryIssueJobPackage(...)
    /// </code>
    /// <para>后果：该节点被静默降权 ⇒ 该时段<b>不去睡觉</b>（疲劳累积）。</para>
    ///
    /// <para><b>为什么用 Finalizer 而不是 Prefix</b>（2026-10-04 二次修复的教训）：
    /// 这两个方法在时段分支<b>之前</b>都有一串前置闸门——</para>
    /// <code>
    /// // JobGiver_Work
    /// if (pawn.workSettings == null || !pawn.workSettings.EverWork) return 0f;
    /// // JobGiver_GetRest（7 道）
    /// if (rest == null) return 0f;
    /// if (rest.CurCategory &lt; minCategory) return 0f;
    /// if (rest.CurLevelPercentage &gt; maxLevelPercentage) return 0f;   // 睡够了
    /// if (TicksGame &lt; mindState.canSleepTick) return 0f;
    /// if (lord != null &amp;&amp; !lord.CurLordToil.AllowSatisfyLongNeeds) return 0f;
    /// if (!RestUtility.CanFallAsleep(pawn)) return 0f;
    /// </code>
    /// <para>早先的 Prefix 写法「直接给结果并 <c>return false</c>」会<b>整块跳过这些闸门</b>
    /// ⇒ 睡饱了的人也会被判定成"该去睡"、EverWork=false 的人被判定成"该干活"。
    /// Finalizer 让原方法<b>完整跑完所有闸门</b>，只在它抛 <c>NotImplementedException</c>
    /// 的那一刻接管 ⇒ 既不复制原版逻辑（原版改动不会与本 patch 失配），
    /// 也不会漏掉任何前置条件。</para>
    ///
    /// <para><b>兜底取值口径</b>：</para>
    /// <list type="bullet">
    /// <item><c>JobGiver_Work</c>：「用餐」= 9（与 Work 同级 —— 该时段要去执行的正事，
    /// 且 PC 餐段规则要靠它驱动 <c>PC_MealBreak</c> 档位）；其他未知时段 = 5.5
    /// （与 Anything 同级，"能工作、优先级中性"，最不容易改变原版行为）。</item>
    /// <item><c>JobGiver_GetRest</c>：一律 <c>rest.CurLevel &lt; 0.3f ? 8f : 0f</c>
    /// —— 逐字对齐原版 Anything 分支（IL：<c>ldc.r4 0.3</c> → <c>ldc.r4 8</c> / <c>0</c>）。
    /// <b>不发明"饭点不许睡"的新规则</b>：睡觉是生理刚需，原版「任意」时段的策略就是
    /// "累到 30% 以下就睡"；饭可以下一餐再吃，累垮不可逆。</item>
    /// </list>
    /// </summary>
    internal static class MealTimeAssignmentPriority
    {
        /// <summary>原版 Anything 档（IL <c>ldc.r4 5.5</c>）——未知时段的默认兜底。</summary>
        internal const float AnythingPriority = 5.5f;

        /// <summary>原版 Work 档（IL <c>ldc.r4 9</c>）——「用餐」这类正事型时段取此值。</summary>
        internal const float WorkPriority = 9f;

        /// <summary>原版 GetRest 里 Anything/Joy 分支的"累"阈值（IL <c>ldc.r4 0.3</c>）。</summary>
        internal const float RestSleepThreshold = 0.3f;

        /// <summary>原版 GetRest 里"该去睡"的返回值（IL <c>ldc.r4 8</c>）。</summary>
        internal const float RestSleepPriority = 8f;

        /// <summary>
        /// 原版 GetJoy 里 Anything 分支的"想娱乐"阈值（IL <c>ldc.r4 0.35</c>）：
        /// <c>joy.CurLevel &lt; 0.35</c> 才去玩。
        /// </summary>
        internal const float JoyNeedThreshold = 0.35f;

        /// <summary>原版 GetJoy 里 Anything 分支的返回值（IL <c>ldc.r4 6</c>）。</summary>
        internal const float JoyAnythingPriority = 6f;

        /// <summary>
        /// 是否为原版硬编码覆盖的 5 种时段（Anything / Work / Sleep / Joy / Meditate）。
        /// <para><c>TimeAssignmentDefOf.Meditate</c> 只在 Royalty 激活时才有值（DefOf 静态字段
        /// 为 null），故比较必须 null-safe。</para>
        /// </summary>
        internal static bool IsVanillaAssignment(TimeAssignmentDef def)
        {
            return Same(def, TimeAssignmentDefOf.Anything)
                || Same(def, TimeAssignmentDefOf.Work)
                || Same(def, TimeAssignmentDefOf.Sleep)
                || Same(def, TimeAssignmentDefOf.Joy)
                || Same(def, TimeAssignmentDefOf.Meditate);
        }

        /// <summary>是否为 PC 的「用餐」时段。</summary>
        internal static bool IsMealAssignment(TimeAssignmentDef def)
        {
            return def != null
                && def.defName == MealScheduleDefaults.MealTimeAssignmentDefName;
        }

        /// <summary>当前时段类型（无时段表 ⇒ Anything，与原版取值路径一致）。</summary>
        internal static TimeAssignmentDef CurrentOf(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }
            Pawn_TimetableTracker tracker = pawn.timetable;
            return tracker != null ? tracker.CurrentAssignment : null;
        }

        /// <summary>
        /// 自定义时段在 <c>ThinkNode_Priority_GetJoy.GetPriority</c> 里的娱乐优先级。
        ///
        /// <para><b>取值口径 = 逐字对齐原版 Anything 分支</b>：原版该方法的语义是
        /// "先看当前时段允不允许娱乐（<c>TimeAssignmentDef.allowJoy</c>），允许了再按具体时段
        /// 给不同优先级"。<c>Anything</c> 允许娱乐 ⇒ 挂 6（joy &lt; 0.35 时）；
        /// <c>Work</c>/<c>Sleep</c> 等 <c>allowJoy=false</c> 的时段在原版<b>前置闸门</b>就
        /// <c>return 0</c> 了，根本走不到时段分支。</para>
        ///
        /// <para>⇒ 自定义时段只需回答一个问题：<b>这时段允许娱乐吗</b>。
        /// 允许（<c>PC_Idle</c> 摸鱼）⇒ 与原版 Anything 同值（真的会去下棋聊天）；
        /// 不允许（<c>PC_Meal</c> 用餐）⇒ 0。刻意<b>不发明</b>"摸鱼优先级更高"之类的新规则 ——
        /// 摸鱼是"没工位时的兜底"，不该比原版正常娱乐更抢手。</para>
        /// </summary>
        internal static float JoyPriorityOf(Pawn pawn)
        {
            TimeAssignmentDef current = CurrentOf(pawn);
            if (current == null || !current.allowJoy)
            {
                return 0f;
            }
            Need_Joy joy = pawn != null && pawn.needs != null ? pawn.needs.joy : null;
            float level = joy != null ? joy.CurLevel : 1f;
            return level < JoyNeedThreshold ? JoyAnythingPriority : 0f;
        }

        private static bool Same(TimeAssignmentDef a, TimeAssignmentDef b)
        {
            return a != null && b != null && a.defName == b.defName;
        }
    }

    /// <summary>
    /// <c>JobGiver_Work.GetPriority</c>：自定义时段原本会抛异常 ⇒ 那些小时「工作」节点被降权到 0。
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_Work), nameof(JobGiver_Work.GetPriority))]
    internal static class Patch_JobGiver_Work_GetPriority
    {
        /// <summary>
        /// 仅在原方法抛 <c>NotImplementedException</c>（= 未知时段）时接管；
        /// 其余异常原样放行，不掩盖真实故障。
        /// </summary>
        private static System.Exception Finalizer(
            Pawn pawn, System.Exception __exception, ref float __result)
        {
            if (!(__exception is System.NotImplementedException))
            {
                return __exception;
            }
            TimeAssignmentDef current = MealTimeAssignmentPriority.CurrentOf(pawn);
            __result = MealTimeAssignmentPriority.IsMealAssignment(current)
                ? MealTimeAssignmentPriority.WorkPriority
                : MealTimeAssignmentPriority.AnythingPriority;
            return null;   // 吞掉：已给出等价结果
        }
    }

    /// <summary>
    /// <c>JobGiver_GetRest.GetPriority</c>：同一根因的第二个出口
    /// （2026-10-04 实机红字即出自这里）。
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_GetRest), nameof(JobGiver_GetRest.GetPriority))]
    internal static class Patch_JobGiver_GetRest_GetPriority
    {
        private static System.Exception Finalizer(
            Pawn pawn, System.Exception __exception, ref float __result)
        {
            if (!(__exception is System.NotImplementedException))
            {
                return __exception;
            }
            // 逐字对齐原版 Anything 分支：低于阈值 ⇒ 去睡（8），否则不睡（0）。
            Need_Rest rest = pawn != null && pawn.needs != null ? pawn.needs.rest : null;
            float level = rest != null ? rest.CurLevel : 1f;
            __result = level < MealTimeAssignmentPriority.RestSleepThreshold
                ? MealTimeAssignmentPriority.RestSleepPriority
                : 0f;
            return null;
        }
    }

    /// <summary>
    /// <c>ThinkNode_Priority_GetJoy.GetPriority</c>：同一根因的<b>第三个</b>出口
    /// （2026-10-04 第三次取证发现）。
    ///
    /// <para><b>为什么这一处最要命</b>：前两处只影响"该不该工作/该不该睡"，
    /// 而这一处决定<b>自定义时段能不能去娱乐</b>。原版语义是"先看
    /// <c>TimeAssignmentDef.allowJoy</c>，不允许就直接 0"，之后才按时段分支取值——
    /// 自定义时段（<c>allowJoy=true</c>，如 <c>PC_Idle</c>）恰好越过第一道闸门，
    /// 却不在后面任何一个分支里 ⇒ 抛异常 ⇒ 整个 Joy 节点优先级取不到
    /// ⇒ "摸鱼的人去做娱乐"永远不发生。</para>
    ///
    /// <para>兜底口径见 <see cref="MealTimeAssignmentPriority.JoyPriorityOf"/>：
    /// 允许娱乐的自定义时段逐字对齐原版 <c>Anything</c> 分支（joy &lt; 0.35 ⇒ 6，否则 0）。</para>
    /// </summary>
    [HarmonyPatch(typeof(ThinkNode_Priority_GetJoy), nameof(ThinkNode_Priority_GetJoy.GetPriority))]
    internal static class Patch_ThinkNode_Priority_GetJoy_GetPriority
    {
        private static System.Exception Finalizer(
            Pawn pawn, System.Exception __exception, ref float __result)
        {
            if (!(__exception is System.NotImplementedException))
            {
                return __exception;
            }
            __result = MealTimeAssignmentPriority.JoyPriorityOf(pawn);
            return null;
        }
    }
}

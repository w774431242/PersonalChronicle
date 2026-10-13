using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 值勤模块的纯判定：连续值勤时长 / 到点是否收工 / 作息表 Work 小时推导
    /// （<b>纯函数，可离线单测；零 Verse 依赖</b>）。
    ///
    /// <para><b>模块边界（GOV-004 · 三模块拆分 · 值勤侧）</b>：
    /// 本类只回答"这个人现在该不该收工"，<b>不碰 JobDriver、不写 timetable</b>。
    /// 真正下发/收工 Job 的动作在 Automation.WorkshopDutyCoordinator。</para>
    ///
    /// <para><b>修成因 B 的核心（用户实机确认）</b>：旧实现用
    /// <c>JobDriver.FailOn(() =&gt; !IsScheduledForDuty(pawn))</c> 做"到点即收工"。
    /// 而 <c>EnsureDutyPlan</c> 是<b>按小时</b>重建计划的（<c>dutyPlanHour != hour</c> 即重建）
    /// ⇒ 跨小时那一瞬若人仍在岗而窗口已过，Job 被立刻打断
    /// =&gt; <b>同一班内反复开工/收工</b>。</para>
    ///
    /// <para><b>为什么不能用 FailOn 做"到点收工"</b>：<c>FailOn</c> 一旦触发 Job 立刻结束，
    /// <b>不可撤销、不可延迟</b>。而我们要的是"未达最小时长则<b>暂不</b>收工"
    /// —— 这是"再等等"的语义，只能由调用方在每 tick 主动求值后决定
    /// （<c>Toil.tickIntervalAction</c> + <c>EndJobWith</c>）。</para>
    /// </summary>
    public static class WorkshopDutyRules
    {
        /// <summary>
        /// 已连续值勤的小时数（<paramref name="joinedHour"/> =&gt; <paramref name="currentHour"/>，
        /// 按 24 小时环绕递增；<b>不跨天回绕</b>）。
        ///
        /// <para>例：23 点上岗、1 点查询 =&gt; 2 小时（跨午夜）；同小时 =&gt; 0。</para>
        /// </summary>
        public static int HoursSince(int joinedHour, int currentHour)
        {
            int diff = currentHour - joinedHour;
            if (diff < 0)
            {
                diff += 24;
            }
            return diff;
        }

        /// <summary>
        /// <b>到点是否收工</b>（修成因 B 的判定核心，<b>纯函数</b>）。
        ///
        /// <para><b>收工条件（两条都要满足）</b>：</para>
        /// <list type="number">
        /// <item>已过当班班次的结束小时（<paramref name="shiftEndHour"/>）<b>且</b>不在班次窗口内
        /// —— 即"确实到点了"；</item>
        /// <item>已连续值勤 &ge; <paramref name="minContinuousHours"/>
        /// —— 避免"开工 5 分钟就被踢"。</item>
        /// </list>
        ///
        /// <para><b>为何要有第 2 条</b>：值勤 Job 可能在班次开始后很久才排到
        /// （前一个 Job 未结束 / 走路耗时）。若无最小时长，他会在刚站定就被收工，
        /// 表现为"反复开工又收工"——正是本次要修的现象。</para>
        ///
        /// <para><b>何时返回 false（继续值勤）</b>：① 仍在班次窗口内；② 已到点但未达最小时长。
        /// <b>何时返回 true（收工）</b>：到点 <b>且</b>已达最小时长。</para>
        /// </summary>
        /// <param name="joinedHour">本次上岗的小时（0-23）。</param>
        /// <param name="currentHour">当前小时（0-23）。</param>
        /// <param name="shiftStartHour">班次起始小时（0-23）。</param>
        /// <param name="shiftEndHour">班次结束小时（0-23；小于起始 = 跨午夜）。</param>
        /// <param name="minContinuousHours">最小连续值勤时长（&lt;=0 走缺省 1，见
        /// <see cref="WorkshopShiftPolicy.NormalizeMinContinuousHours"/>）。</param>
        /// <returns><c>true</c> = 应主动收工；<c>false</c> = 继续值勤。</returns>
        public static bool ShouldEndDuty(int joinedHour, int currentHour,
            int shiftStartHour, int shiftEndHour, int minContinuousHours)
        {
            // 仍在班次窗口内 ⇒ 不到点，继续。
            if (WorkshopTime.InRange(currentHour, shiftStartHour, shiftEndHour))
            {
                return false;
            }
            int min = WorkshopShiftPolicy.NormalizeMinContinuousHours(minContinuousHours);
            // 已到点但未达最小连续时长 ⇒ 不打断（避免"开工 5 分钟就被踢"）。
            return HoursSince(joinedHour, currentHour) >= min;
        }
        /// <summary>
        /// 某人在某小时<b>是否当班</b>（班次模块对值勤模块暴露的唯一查询；
        /// 取代旧实现里"直接读按小时重建的 dutyPlan"这一做法）。
        ///
        /// <para><b>与旧口径的差异（刻意）</b>：旧 <c>IsScheduledForDuty</c> 读的是
        /// <b>本小时重建</b>的调度计划，跨小时那一瞬计划已按新小时重算 ⇒ 人可能"忽然不在岗"
        /// ⇒ Job 被 FailOn 打断（成因 B）。本函数改为按<b>该人所属班次的窗口</b>判定，
        /// 与计划重建<b>解耦</b>，边界稳定。</para>
        /// </summary>
        /// <param name="shiftStartHour">班次起始小时（0-23）。</param>
        /// <param name="shiftEndHour">班次结束小时（0-23；小于起始 = 跨午夜）。</param>
        /// <param name="hour">待判定小时（0-23）。</param>
        public static bool IsOnDutyAt(int shiftStartHour, int shiftEndHour, int hour)
        {
            return WorkshopTime.InRange(hour, shiftStartHour, shiftEndHour);
        }

        /// <summary>
        /// 把一个班次窗口标记进 24 位 Work 小时表（<b>纯函数</b>；就地写入 <paramref name="work"/>）。
        ///
        /// <para><b>全天班（start == end）必须标满 24 小时</b>：旧实现用
        /// <c>for (h = start; h != end; ...)</c>，全天班时循环一次都不执行
        /// =&gt; 语义反转成"全天班一小时的 Work 都不写"（已由单测钉死）。</para>
        /// </summary>
        public static void MarkWorkHours(int shiftStartHour, int shiftEndHour, bool[] work)
        {
            if (work == null || work.Length < 24)
            {
                return;
            }
            if (shiftStartHour == shiftEndHour)
            {
                for (int h = 0; h < 24; h++)
                {
                    work[h] = true;
                }
                return;
            }
            for (int h = shiftStartHour; h != shiftEndHour; h = (h + 1) % 24)
            {
                work[h] = true;
            }
        }

        /// <summary>统计 24 位表里的 Work 小时数（可观测性读数；O(24)）。</summary>
        public static int CountWorkHours(bool[] work)
        {
            if (work == null || work.Length < 24)
            {
                return 0;
            }
            int n = 0;
            for (int h = 0; h < 24; h++)
            {
                if (work[h])
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// <b>固定绑定策略下</b>，一个班组当日应写入作息表的班次窗口
        /// （纯函数；<b>不按天变化</b>，这正是修成因 A 的落点）。
        ///
        /// <para><b>返回值语义</b>：<c>false</c> = 不写该班组的作息表（无班次计划）
        /// ⇒ 调用方应<b>保持玩家手排不动</b>（"真·无计划"）；<c>true</c> +
        /// <paramref name="work"/> 已按该班次标记。</para>
        /// </summary>
        /// <param name="assignedShiftCount">该班组被指派的班次数量（&lt;=0 = 无班次计划）。</param>
        /// <param name="boundShiftStart">绑定班次的起始小时。</param>
        /// <param name="boundShiftEnd">绑定班次的结束小时。</param>
        /// <param name="work">输出：24 位 Work 小时表（调用方负责分配，长度 &ge; 24）。</param>
        public static bool ResolveFixedShiftWorkHours(int assignedShiftCount,
            int boundShiftStart, int boundShiftEnd, bool[] work)
        {
            if (work == null || assignedShiftCount <= 0)
            {
                return false;
            }
            MarkWorkHours(boundShiftStart, boundShiftEnd, work);
            return true;
        }
    }
}

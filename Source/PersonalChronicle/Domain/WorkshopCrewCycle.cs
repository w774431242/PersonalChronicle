using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 倒班循环（现实制造业「轮班制度」的数学模型）—— 纯函数，可离线单测。
    ///
    /// <para><b>模型</b>：设班组数为 <c>C</c>、班次数为 <c>S</c>（早/中/夜 = 3）。
    /// 第 <c>d</c> 天，第 <c>c</c> 个班组的"岗位序位" = <c>(c + d) mod C</c>；
    /// 序位 &lt; S ⇒ 上对应班次，否则**当天休息**。</para>
    ///
    /// <para><b>它还原了哪些现实制度</b>（已由单测钉死）：</para>
    /// <list type="bullet">
    /// <item><c>C=3, S=3</c> ⇒ <b>三班倒</b>：甲乙丙班每天各占早/中/夜，无休，每 3 天轮一圈；</item>
    /// <item><c>C=2, S=2</c> ⇒ <b>两班倒</b>：白/夜两班，每 2 天互换（即"白夜倒班"）；</item>
    /// <item><c>C=4, S=3</c> ⇒ <b>四班三运转</b>：每天恰好 1 个班组休息、每班上"3 天工作 + 1 天休息"，
    /// 这是现实中连续生产（冶炼/化工）最常见的制度；</item>
    /// <item><c>C&gt;S</c> 的一般情形 ⇒ 每天休息班组数 = <c>C − S</c>。</item>
    /// </list>
    ///
    /// <para>相位（错峰）：调用方用 <c>dayIndex − CycleAnchorDay</c> 传入，不同车间可错开交班时刻。</para>
    /// </summary>
    public static class WorkshopCrewCycle
    {
        /// <summary>该班组当天应承担的班次序位（0 = 第 1 班）；<c>-1</c> = 当天休息。</summary>
        public static int ShiftSlotOfCrew(int dayIndex, int crewIndex, int crewCount, int shiftCount)
        {
            if (crewCount <= 0 || shiftCount <= 0 || crewIndex < 0 || crewIndex >= crewCount)
            {
                return -1;
            }
            int position = Mod(crewIndex + dayIndex, crewCount);
            return position < shiftCount ? position : -1;
        }

        /// <summary>当天第 <paramref name="shiftSlot"/> 班由哪个班组承担；<c>-1</c> = 无人（班组编制不足）。</summary>
        public static int CrewOfShiftSlot(int dayIndex, int shiftSlot, int crewCount, int shiftCount)
        {
            if (crewCount <= 0 || shiftCount <= 0 || shiftSlot < 0 || shiftSlot >= shiftCount)
            {
                return -1;
            }
            for (int crew = 0; crew < crewCount; crew++)
            {
                if (ShiftSlotOfCrew(dayIndex, crew, crewCount, shiftCount) == shiftSlot)
                {
                    return crew;
                }
            }
            return -1;
        }

        /// <summary>
        /// 当天第 <paramref name="shiftSlot"/> 班由哪个班组承担（**考虑调休/顶班例外**）；
        /// <c>-1</c> = 该班次当天无人（请假未顶或编制不足）。
        /// 运行期与 UI 共用，保证"表上看到的"与"实际派的"同源。
        /// </summary>
        public static int CrewOfShiftSlot(int dayIndex, int shiftSlot, int crewCount, int shiftCount,
            IList<int> exceptionKinds)
        {
            if (crewCount <= 0 || shiftCount <= 0 || shiftSlot < 0 || shiftSlot >= shiftCount)
            {
                return -1;
            }
            for (int crew = 0; crew < crewCount; crew++)
            {
                if (EffectiveShiftSlotOfCrew(dayIndex, crew, crewCount, shiftCount, exceptionKinds) == shiftSlot)
                {
                    return crew;
                }
            }
            return -1;
        }

        /// <summary>该班组当天是否休息。</summary>
        public static bool IsResting(int dayIndex, int crewIndex, int crewCount, int shiftCount)
        {
            return ShiftSlotOfCrew(dayIndex, crewIndex, crewCount, shiftCount) < 0;
        }

        // ---- 例外（调休 / 请假 / 顶班）-----------------------------------------
        // 2026-09-11 Phase 2：倒班表上允许逐格改（现实里的调休与顶班），
        // 语义在此集中定义，UI 与运行期共用。

        /// <summary>无例外（按倒班表）。</summary>
        public const int ExceptionNone = 0;

        /// <summary>请假 / 调休：本应上班 ⇒ 当天改休（并计入缺勤）。</summary>
        public const int ExceptionLeave = 1;

        /// <summary>顶班 / 加班：本应休息 ⇒ 补到当天缺人的班次（通常因为有人请假）。</summary>
        public const int ExceptionExtraWork = 2;

        /// <summary>考虑例外后，该班组当天应承担的班次序位（-1 = 休）。</summary>
        public static int EffectiveShiftSlotOfCrew(int dayIndex, int crewIndex, int crewCount, int shiftCount,
            IList<int> exceptionKinds)
        {
            int kind = ExceptionOf(exceptionKinds, crewIndex);
            int slot = ShiftSlotOfCrew(dayIndex, crewIndex, crewCount, shiftCount);
            if (kind == ExceptionLeave)
            {
                return -1;
            }
            if (slot >= 0)
            {
                // 本就上班 ⇒ 「顶班」标记无效（避免同一班组一天上两个班）。
                return slot;
            }
            if (kind != ExceptionExtraWork)
            {
                return -1;
            }
            return FirstUnstaffedSlot(dayIndex, crewCount, shiftCount, exceptionKinds);
        }

        /// <summary>
        /// 当天第一个没有班组承担的班次序位（-1 = 三班都有人）。
        /// 语义：把"请假者让出的班次"让给"加班顶班者"。
        /// </summary>
        public static int FirstUnstaffedSlot(int dayIndex, int crewCount, int shiftCount, IList<int> exceptionKinds)
        {
            if (crewCount <= 0 || shiftCount <= 0)
            {
                return -1;
            }
            var used = new bool[shiftCount];
            for (int crew = 0; crew < crewCount; crew++)
            {
                if (ExceptionOf(exceptionKinds, crew) == ExceptionLeave)
                {
                    continue;
                }
                int slot = ShiftSlotOfCrew(dayIndex, crew, crewCount, shiftCount);
                if (slot >= 0 && slot < shiftCount)
                {
                    used[slot] = true;
                }
            }
            for (int slot = 0; slot < shiftCount; slot++)
            {
                if (!used[slot])
                {
                    return slot;
                }
            }
            return -1;
        }

        /// <summary>
        /// 后置审计：考虑例外（含顶班）后，是否每个班次都有人。
        ///
        /// <para>与 <see cref="FirstUnstaffedSlot"/> 的区别是该函数是**派工前的空缺查询**
        /// （顶班者尚未落位，故它仍会报出那个空缺）；本函数用于"表上/派工后"的检查，
        /// 例如倒班表标注停班格或概览提示缺员。</para>
        /// </summary>
        public static bool AllSlotsStaffed(int dayIndex, int crewCount, int shiftCount, IList<int> exceptionKinds)
        {
            if (shiftCount <= 0 || crewCount <= 0)
            {
                return false;
            }
            for (int slot = 0; slot < shiftCount; slot++)
            {
                if (CrewOfShiftSlot(dayIndex, slot, crewCount, shiftCount, exceptionKinds) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>取某班组的例外类型（越界/空表 ⇒ 无例外）。</summary>
        public static int ExceptionOf(IList<int> exceptionKinds, int crewIndex)
        {
            if (exceptionKinds == null || crewIndex < 0 || crewIndex >= exceptionKinds.Count)
            {
                return ExceptionNone;
            }
            int kind = exceptionKinds[crewIndex];
            return kind == ExceptionLeave || kind == ExceptionExtraWork ? kind : ExceptionNone;
        }

        /// <summary>UI 循环：正常 → 请假 → 顶班 → 正常。</summary>
        public static int NextException(int current)
        {
            switch (current)
            {
                case ExceptionNone: return ExceptionLeave;
                case ExceptionLeave: return ExceptionExtraWork;
                default: return ExceptionNone;
            }
        }

        /// <summary>周期长度（天）= max(班组数, 班次数)；整个倒班表按此循环。</summary>
        public static int CycleLengthDays(int crewCount, int shiftCount)
        {
            int max = crewCount > shiftCount ? crewCount : shiftCount;
            return max < 1 ? 1 : max;
        }

        /// <summary>每天休息的班组数（<c>max(0, C − S)</c>；三班倒/两班倒为 0，四班三运转为 1）。</summary>
        public static int RestingCrewsPerDay(int crewCount, int shiftCount)
        {
            int rest = crewCount - shiftCount;
            return rest > 0 ? rest : 0;
        }

        /// <summary>
        /// 该班组在<b>一个周期内</b>的工作天数（用于排班表底部"月工作日/休息日"统计）。
        /// 例：C=4,S=3 ⇒ 4 天里 3 天上班。
        /// </summary>
        public static int WorkingDaysPerCycle(int crewCount, int shiftCount)
        {
            if (crewCount <= 0 || shiftCount <= 0)
            {
                return 0;
            }
            int shiftLen = CycleLengthDays(crewCount, shiftCount);
            int works = shiftCount < shiftLen ? shiftCount : shiftLen;
            return works;
        }

        private static int Mod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }
}

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 班次时段的纯时间数学（2026-10-02 从 Automation 下沉）。
    ///
    /// <para><b>为什么下沉</b>：InRange / CrossesMidnight 是纯整数区间判定，不依赖任何 Verse / UI 类型，
    /// 却原住在 Automation/WorkshopSchedule.cs（UI 目录）—— 而 Domain/WorkshopShiftPatterns.cs
    /// 的窗口语义必须与它保持一致，只能在注释里口头声明（Domain 不该知道 Automation 的类型）。
    /// 下沉后两者同层，一致关系由编译器保证而非注释保证。</para>
    ///
    /// <para><b>口径</b>（参考文档 5.1 IsHourInRange，原样采纳；单测见 Tests/WorkshopHoursTests.cs）：
    /// 左闭右开；start == end 视为全天 24 小时；end &lt; start 视为跨午夜。</para>
    ///
    /// <para><b>归属</b>：显示格式（HH:00 – HH:00）仍在 UI 层的 Automation.WorkshopHours.LabelOf，
    /// 不与本类混在一起 —— 数据判定与显示格式是两种关注点。</para>
    /// </summary>
    public static class WorkshopTime
    {
        /// <summary>是否跨午夜（结束小时早于起始小时，且非全天）。</summary>
        public static bool CrossesMidnight(int start, int end)
        {
            return start != end && end < start;
        }

        /// <summary>该小时是否落在 [start, end) 窗口内（跨午夜环绕；start == end = 全天）。</summary>
        public static bool InRange(int hour, int start, int end)
        {
            if (hour < 0 || hour > 23)
            {
                return false;
            }
            if (start == end)
            {
                return true;
            }
            return start < end
                ? hour >= start && hour < end
                : hour >= start || hour < end;
        }
    }
}

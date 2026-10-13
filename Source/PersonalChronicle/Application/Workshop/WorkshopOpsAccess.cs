using System;
using Verse;

namespace PersonalChronicle.Application.Workshop
{
    /// <summary>
    /// 车间**运行摘要**（跨车间汇总的只读快照，一次取齐 KPI 需要的全部人力/排班读数）。
    ///
    /// <para>字段全部是原版或本 Mod 已持久化的真实量，无派生猜测：</para>
    /// <list type="bullet">
    /// <item><b>ManHourTicks</b>：窗口内在岗人时（含加班），来源 = 各车间考勤台账；</item>
    /// <item><b>OnDutyCount</b> / <b>MemberCount</b>：当前在岗人数 / 编内成员数（去重）；</item>
    /// <item><b>PlannedManHours</b> / <b>EmptyHours</b>：全厂排班计划人时（人时/日）与无人值守小时数（并集）；</item>
    /// <item><b>ScheduledEntries</b>：排班条目数（0 = 尚未排班 ⇒ 覆盖率按「未接入」呈现，不用 0 冒充）。</item>
    /// </list>
    /// </summary>
    public struct WorkshopOpsDigest
    {
        public int WorkshopCount;
        public long ManHourTicks;
        public long OvertimeTicks;
        public int OnDutyCount;
        public int MemberCount;
        public int PlannedManHours;
        public int EmptyHours;
        public int ScheduledEntries;
    }

    /// <summary>
    /// 「车间运行摘要」只读查询口（Application 层契约；实现由 Automation 在启动时注入）。
    ///
    /// <para><b>为什么需要这层间接</b>：摘要是从 Automation 的运行态与车间存档
    /// （<c>Zone_Workshop.Attendance</c> / <c>MapComponent_WorkshopManager</c> 的排班表 /
    /// <c>WorkshopRosterCoverage</c>）汇总出来的，而消费方（制造中心 KPI）在 Application 层 ——
    /// 依赖方向禁止 Application → Automation 直读。故按工程既定范式
    /// （<see cref="PersonalChronicle.Application.Effects.WorkshopBonusAccess"/> 同族）建立
    /// 受控静态钩子：Automation 侧用 <c>[StaticConstructorOnStartup]</c> 注入实现
    /// （Automation → Application 是允许方向）。</para>
    ///
    /// <para><b>降级</b>：未注入 / map 为空 / 实现抛异常 ⇒ 返回 <c>default</c>（全 0，
    /// 调用方据各字段是否为 0 走「未接入」文案，不编造数值）。</para>
    /// </summary>
    public static class WorkshopOpsAccess
    {
        private static Func<Map, int, WorkshopOpsDigest> source;

        /// <summary>绑定实现（幂等：重复绑定覆盖旧委托；传 null 可解绑）。</summary>
        public static void SetSource(Func<Map, int, WorkshopOpsDigest> value)
        {
            source = value;
        }

        /// <summary>
        /// 取车间运行摘要。<paramref name="windowDays"/> = 人时统计窗口（天）；≤0 时按 1 天处理。
        /// 未注入 / 抛异常 → <c>default</c>（零值）。
        /// </summary>
        public static WorkshopOpsDigest Digest(Map map, int windowDays)
        {
            Func<Map, int, WorkshopOpsDigest> fn = source;
            if (fn == null || map == null)
            {
                return default;
            }
            try
            {
                return fn(map, windowDays);
            }
            catch (Exception)
            {
                return default;
            }
        }
    }
}

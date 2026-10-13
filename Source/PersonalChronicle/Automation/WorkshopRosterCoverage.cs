using System.Collections.Generic;

namespace PersonalChronicle.Automation
{
    /// <summary>排班覆盖计算的最小输入：一个时段 + 该时段同时在岗人数。</summary>
    public struct RosterSlot
    {
        public int StartHour;
        public int EndHour;
        public int Count;

        public RosterSlot(int startHour, int endHour, int count)
        {
            StartHour = startHour;
            EndHour = endHour;
            Count = count;
        }
    }

    /// <summary>
    /// 排班覆盖度计算（月度排班图的数据源）—— 纯函数，可离线单测。
    ///
    /// <para><b>时刻口径</b>：直接复用 <see cref="WorkshopHours.InRange"/>（全工程唯一实现），
    /// 因此"起 == 止 = 全天""止 &lt; 起 = 跨午夜"与固定班/班组/轮岗的判定完全一致，
    /// 图表不会与排班实际行为出现偏差。</para>
    ///
    /// <para><b>为什么需要</b>：排班最实际的两个问题——"哪些小时没人（空档）"与
    /// "每天投入多少人力（人时）"——在纯列表 UI 里看不出来：列表要玩家自己在脑子里叠加时段。
    /// 本类把三类排班（固定班 / 班组名额 / 轮岗）折算成 24 小时覆盖曲线。</para>
    /// </summary>
    public static class WorkshopRosterCoverage
    {
        public const int HoursPerDay = 24;

        /// <summary>
        /// 每小时在岗人数（长度 24，索引 = 小时）。
        /// 同时段多条排班**累加**（例如固定班与班组在一个小时里都有人）。
        /// </summary>
        public static int[] HourlyCoverage(IList<RosterSlot> slots)
        {
            int[] hourly = new int[HoursPerDay];
            if (slots == null)
            {
                return hourly;
            }
            for (int i = 0; i < slots.Count; i++)
            {
                RosterSlot slot = slots[i];
                int count = slot.Count > 0 ? slot.Count : 0;
                if (count == 0)
                {
                    continue;
                }
                for (int hour = 0; hour < HoursPerDay; hour++)
                {
                    if (WorkshopHours.InRange(hour, slot.StartHour, slot.EndHour))
                    {
                        hourly[hour] += count;
                    }
                }
            }
            return hourly;
        }

        /// <summary>一天的计划人时（= 每小时在岗人数之和）。</summary>
        public static int ManHoursPerDay(int[] hourly)
        {
            if (hourly == null)
            {
                return 0;
            }
            int total = 0;
            for (int i = 0; i < hourly.Length; i++)
            {
                total += hourly[i];
            }
            return total;
        }

        /// <summary>无人值守的小时数（排班空档；0 = 全天有人）。</summary>
        public static int EmptyHours(int[] hourly)
        {
            if (hourly == null)
            {
                return HoursPerDay;
            }
            int empty = 0;
            for (int i = 0; i < hourly.Length; i++)
            {
                if (hourly[i] <= 0)
                {
                    empty++;
                }
            }
            return empty;
        }

        /// <summary>单小时最大在岗人数（图表颜色的归一化基准）。</summary>
        public static int MaxCoverage(int[] hourly)
        {
            if (hourly == null)
            {
                return 0;
            }
            int max = 0;
            for (int i = 0; i < hourly.Length; i++)
            {
                if (hourly[i] > max)
                {
                    max = hourly[i];
                }
            }
            return max;
        }

        /// <summary>
        /// 把覆盖曲线折算成 0..1 的色深（0 = 空档，1 = 当日峰值）。
        /// 空档单独由调用方着色（本项目约定：空档用警示色，不靠色深表达）。
        /// </summary>
        public static float IntensityOf(int coverage, int maxCoverage)
        {
            if (coverage <= 0 || maxCoverage <= 0)
            {
                return 0f;
            }
            float value = (float)coverage / maxCoverage;
            return value < 0.35f ? 0.35f : value;
        }
    }
}

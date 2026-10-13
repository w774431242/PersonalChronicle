using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>一个班次的时间窗口（小时精度；起 == 止 = 全天，止 &lt; 起 = 跨午夜）。</summary>
    public struct ShiftWindow
    {
        public int StartHour;
        public int EndHour;

        public ShiftWindow(int startHour, int endHour)
        {
            StartHour = startHour;
            EndHour = endHour;
        }
    }

    /// <summary>
    /// 班制（排班模板）：现实工业里的「常白班 / 两班倒 / 三班倒」——
    /// 一组固定窗口，一键铺成多条班组或轮岗规则。
    /// </summary>
    public sealed class ShiftPattern
    {
        /// <summary>稳定 ID（UI 键后缀与单测锚点；禁依赖显示文本 —— GOV-009）。</summary>
        public string Key;

        /// <summary>班次窗口列表（顺序即班次序号 A/B/C）。</summary>
        public List<ShiftWindow> Windows = new List<ShiftWindow>();

        public ShiftPattern(string key, params ShiftWindow[] windows)
        {
            Key = key;
            Windows = new List<ShiftWindow>(windows);
        }

        /// <summary>班次数量（两班倒 = 2，三班倒 = 3）。</summary>
        public int ShiftCount
        {
            get { return Windows != null ? Windows.Count : 0; }
        }

        /// <summary>该班制覆盖多少小时/天（窗口小时数之和；用于"是否全天有人"的口径与图例）。</summary>
        public int CoveredHoursPerDay
        {
            get
            {
                int total = 0;
                if (Windows == null)
                {
                    return 0;
                }
                for (int i = 0; i < Windows.Count; i++)
                {
                    total += WorkshopShiftPatterns.HoursOf(Windows[i]);
                }
                return total;
            }
        }
    }

    /// <summary>
    /// 班制目录 —— 纯数据 + 纯函数（可离线单测；不依赖引擎与 Automation 层）。
    ///
    /// <para><b>为什么需要</b>：现实里排班单位是"班制"（两班倒 12+12、三班倒 8×3），
    /// 而原实现只能一条条手工加时段 ⇒ 玩家的心智模型对不上界面。本目录提供预设窗口，
    /// UI 负责把它们铺成 N 条班组/轮岗规则，玩家仍可逐条微调。</para>
    ///
    /// <para>窗口口径与 <c>WorkshopHours.InRange</c> 一致（起 == 止 = 全天；止 &lt; 起 = 跨午夜）。
    /// 三班倒的"16:00 → 00:00"用 <c>EndHour = 0</c> 表达（= [16,24)），不是"全天"。</para>
    /// </summary>
    public static class WorkshopShiftPatterns
    {
        /// <summary>常白班：08:00–16:00（单班，8 小时）。</summary>
        public const string KeyDayShift = "DayShift";

        /// <summary>两班倒：08:00–20:00 / 20:00–08:00（12 + 12 小时，全天无空档）。</summary>
        public const string KeyTwoShift = "TwoShift";

        /// <summary>三班倒（6 点起）：06–14 / 14–22 / 22–06（8 × 3 = 24 小时）。</summary>
        public const string KeyThreeShift6 = "ThreeShift6";

        /// <summary>三班倒（8 点起）：08–16 / 16–00 / 00–08（8 × 3 = 24 小时）。</summary>
        public const string KeyThreeShift8 = "ThreeShift8";

        private static readonly List<ShiftPattern> Catalog = new List<ShiftPattern>
        {
            new ShiftPattern(KeyDayShift, new ShiftWindow(8, 16)),
            new ShiftPattern(KeyTwoShift, new ShiftWindow(8, 20), new ShiftWindow(20, 8)),
            new ShiftPattern(KeyThreeShift6, new ShiftWindow(6, 14), new ShiftWindow(14, 22), new ShiftWindow(22, 6)),
            new ShiftPattern(KeyThreeShift8, new ShiftWindow(8, 16), new ShiftWindow(16, 0), new ShiftWindow(0, 8))
        };

        /// <summary>全部可用班制（UI 遍历用；顺序 = 由简到繁）。</summary>
        public static List<ShiftPattern> All
        {
            get { return Catalog; }
        }

        /// <summary>按稳定 ID 找班制（找不到返回 null，不抛异常）。</summary>
        public static ShiftPattern ByKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            for (int i = 0; i < Catalog.Count; i++)
            {
                if (Catalog[i].Key == key)
                {
                    return Catalog[i];
                }
            }
            return null;
        }

        /// <summary>窗口的小时数（跨午夜按 +24 计算；起 == 止 = 全天 24 小时）。</summary>
        public static int HoursOf(ShiftWindow window)
        {
            if (window.StartHour == window.EndHour)
            {
                return 24;
            }
            int length = window.EndHour - window.StartHour;
            return length > 0 ? length : length + 24;
        }

        /// <summary>该班制是否覆盖全天 24 小时（两班倒/三班倒 = true，常白班 = false）。</summary>
        public static bool CoversWholeDay(ShiftPattern pattern)
        {
            return pattern != null && pattern.CoveredHoursPerDay >= 24;
        }

        /// <summary>
        /// 生成时的建议名额：把候选池人数按班次数均分（至少 1 人/班）。
        /// 例：6 人三班倒 ⇒ 2 人/班；2 人三班倒 ⇒ 1 人/班（不足则每班 1 人，宁可少排也不虚报）。
        ///
        /// <para>⚠ <b>当前未接线（2026-10-02）</b>：名额已从班次编辑器与班次表撤下（用户实测结论
        /// 「名额、门槛等等无作用」），<c>WorkshopManageView.ApplyPattern</c> 不再调用本函数。
        /// <b>保留原因</b>：本工程源码目录未纳入版本控制（删除不可恢复），且本函数是纯函数、由
        /// <c>Tests/WorkshopShiftPatternsTests.cs</c> 覆盖；若将来恢复「名额」设置，接线即可。</para>
        /// </summary>
        public static int SuggestedRequiredCount(int poolSize, int shiftCount)
        {
            if (shiftCount <= 0)
            {
                return 1;
            }
            int per = poolSize / shiftCount;
            return per < 1 ? 1 : per;
        }
    }
}

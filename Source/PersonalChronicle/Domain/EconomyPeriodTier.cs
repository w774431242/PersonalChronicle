using RimWorld;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// **统一周期基准**（用户 2026-09-20 指令「统一蜡烛图周期为基准」；`SDD-LCE-003` §2）。
    ///
    /// <para><b>为什么单独成类</b>：经济中心的**时间轴只有一份真相** —— K 线档位、热力图色阶窗口、
    /// 上卷边界、文档口径全部从这里取值（GOV-006：周期/跨度/保留上限不得散落字面量；
    /// DELTA grep 判据 = 周期数字仅出现在本类与单测）。</para>
    ///
    /// <para><b>层级与依赖方向</b>：Domain 纯静态常量 + 纯函数（先例 <see cref="QuadrumBuckets"/>）；
    /// 只允许 Data / Application / UI / Tests / 文档**单向引用**。UI 文案经
    /// <see cref="KeyOf"/>（本地化键）→ `Translate`，不在此处持显示文本（LOC-001）。</para>
    ///
    /// <para><b>五档口径（游戏时间）</b>：日 = 1 日；周 = 7 日（连续分桶，不与历法对齐）；
    /// 月 = 1 quadrum = 15 日（游戏日历上就是「月」）；季 = 2 月 = 30 日；年 = 4 月 = 原版 1 年。
    /// ⚠ 与 <see cref="ItemFlowLedger"/>「季 = 1 quadrum = 15 日」的旧口径**不同名同义** ——
    /// 已在该处登记别名，**不改其实现**（GOV-006 例外登记项）。</para>
    ///
    /// <para><b>退役槽位（60分，2026-09-21 用户裁决移除）</b>：槽位 0 保留但**退役** —— 不再采样、
    /// 不再显示、读档即被 <c>Trim(0)</c> 清空（<see cref="RetentionOf"/> 返回 0 = "清空"语义，
    /// 与 `TierRing.Trim` 既有约定一致）。**保留索引位 = 存档形状双向兼容**：
    /// 升级**无需迁移**（旧档 6 槽一一对齐），降级回旧版 dll 也**不误读**（旧版只看到空的 60分 档）。</para>
    ///
    /// <para><b>保留上限（2026-09-21 用户裁决「无上限」）</b>：日/周/月/季/年 = <see cref="Unbounded"/>
    /// —— 用 `int.MaxValue` 表达"无上限"并**复用既有有界机制**（`Add` 的上限比较永不触发、
    /// `Trim` 的裁剪循环永不进入），避免"用 0 当无界"这类会**静默清空整档 / 静默不写入**的陷阱。
    /// 代价与约束（存档体积随游玩时长线性增长、绘制抽稀、EXC 登记）见
    /// `docs/设计文档/功能模块/经济体系/评估_蜡烛线无界保存-2026-09-21.md`。</para>
    ///
    /// <para><b>双轴纪律</b>：桶边界 = **TicksAbs**（与游戏日历的 日/quadrum/年 显示同相）；
    /// 采样门 = `TicksGame % 250`（与 `ChronicleGameComponent.Sampling` 先例同轴、相位稳定）。
    /// 二者在同一存档内只差常数 `GameStartAbsTick`，不互相换算。</para>
    ///
    /// <para><b>上卷图（仅封桶时刻，不做跨档重复采样）</b>：`日 → 周/月/季/年`
    /// （基准档 = 日，封桶时**直喂全部高档**）。</para>
    /// </summary>
    public static class EconomyPeriodTier
    {
        /// <summary>
        /// **已退役**：60分 档槽位（保留索引位 = 存档形状双向兼容；不采样 / 不显示 / 读档即清空）。
        /// 只在"槽位对齐 + 迁移语义"处被引用，**不得**用于新写入路径（GOV-006）。
        /// </summary>
        public const int Hour60 = 0;

        /// <summary>日档 —— **基准档**（采样落入 / 上卷起点的唯一入口）。</summary>
        public const int Day = 1;

        /// <summary>周档（7 日连续分桶）。</summary>
        public const int Week = 2;

        /// <summary>月档（= 原版 1 quadrum = 15 日）。</summary>
        public const int Month = 3;

        /// <summary>季档（= 2 月 = 30 日）。</summary>
        public const int Quarter = 4;

        /// <summary>年档（= 4 月 = 原版 1 年）。</summary>
        public const int Year = 5;

        /// <summary>槽位数（**含**退役的 60分 槽位；= 数组长度 / 存档形状，勿改）。</summary>
        public const int Count = 6;

        /// <summary>显示档位数（UI / Provider 迭代用）= 日/周/月/季/年。</summary>
        public const int DisplayCount = Count - 1;

        /// <summary>首个显示档位（= <see cref="Day"/>）。</summary>
        public const int FirstDisplayedTier = Day;

        /// <summary>基准档（采样落入 / 封桶上卷起点）。</summary>
        public const int BaseTier = Day;

        /// <summary>默认档（= <see cref="Day"/>）—— 未选择时的解析结果。</summary>
        public const int DefaultTier = Day;

        /// <summary>
        /// **无上限哨兵**（用户 2026-09-21 裁决「全档无上限」）。
        /// 取 `int.MaxValue`：既有 `TierRing.Add/Trim` 的上限比较永不触发，语义 = 永不裁剪。
        /// ⚠ **不得**用 0 或负数表达无界（那两个值在既有约定里 = 不写入 / 清空整档）。
        /// </summary>
        public const int Unbounded = int.MaxValue;

        /// <summary>1 游戏小时（原版常量；**仅迁移/单测**引用 —— 60分 档已退役）。</summary>
        public const long TicksPerHour60 = GenDate.TicksPerHour;

        /// <summary>1 游戏日。</summary>
        public const long TicksPerDay = GenDate.TicksPerDay;

        /// <summary>1 周 = 7 日（连续分桶，非历法对齐）。</summary>
        public const long TicksPerWeek = 7L * GenDate.TicksPerDay;

        /// <summary>1 月 = 1 quadrum = 15 日（游戏日历上的「月」）。</summary>
        public const long TicksPerMonth = GenDate.TicksPerQuadrum;

        /// <summary>1 季 = 2 月 = 30 日。</summary>
        public const long TicksPerQuarter = 2L * GenDate.TicksPerQuadrum;

        /// <summary>1 年 = 4 月（原版年）。</summary>
        public const long TicksPerYear = GenDate.TicksPerYear;

        private static readonly long[] Spans = new long[Count]
        {
            TicksPerHour60, TicksPerDay, TicksPerWeek, TicksPerMonth, TicksPerQuarter, TicksPerYear
        };

        /// <summary>
        /// 保留上限（根）：槽位 0（已退役 60分）= **0 = 清空**；其余五档 = <see cref="Unbounded"/>。
        /// ⚠ 索引必须与槽位一一对应（存档形状按位置序列化）。
        /// </summary>
        private static readonly int[] RetentionCap = new int[Count]
        {
            0, Unbounded, Unbounded, Unbounded, Unbounded, Unbounded
        };

        private static readonly string[] KeySuffix = new string[Count] { "60m", "d", "w", "mo", "q", "y" };

        /// <summary>槽位归一（越界 = 默认档，避免调用点各自判空）。</summary>
        public static int Resolve(int tier)
        {
            return (tier >= 0 && tier < Count) ? tier : DefaultTier;
        }

        /// <summary>
        /// **显示索引 → 槽位**（UI / Provider / 请求的唯一映射入口）。
        /// 越界（含"未选"的 −1）⇒ <see cref="DefaultTier"/>。
        /// </summary>
        public static int TierAt(int displayIndex)
        {
            if (displayIndex < 0 || displayIndex >= DisplayCount)
            {
                return DefaultTier;
            }
            return FirstDisplayedTier + displayIndex;
        }

        /// <summary>
        /// **槽位 → 显示索引**（退役槽位/越界 ⇒ 0）。
        /// 与 <see cref="TierAt"/> 互为逆映射（显示域内），UI 档位条以显示索引为准。
        /// </summary>
        public static int DisplayIndexOf(int tier)
        {
            int d = Resolve(tier) - FirstDisplayedTier;
            return (d >= 0 && d < DisplayCount) ? d : 0;
        }

        /// <summary>该档的桶跨度（ticks）。</summary>
        public static long TicksOf(int tier)
        {
            return Spans[Resolve(tier)];
        }

        /// <summary>
        /// 该档的桶起点（**TicksAbs 轴**，floor-mod；与游戏日历的 日/quadrum/年 同相）。
        /// </summary>
        public static long BoundaryOf(int tier, long absTick)
        {
            long span = Spans[Resolve(tier)];
            long tick = absTick > 0L ? absTick : 0L;
            return tick - (tick % span);
        }

        /// <summary>
        /// 该档的保留上限（根）：退役槽位 = 0（清空）；其余 = <see cref="Unbounded"/>（永不裁剪）。
        /// </summary>
        public static int RetentionOf(int tier)
        {
            return RetentionCap[Resolve(tier)];
        }

        /// <summary>
        /// 该档的本地化键（UI 文案唯一入口；键缺失回退键名，LOC-001）。
        /// ⚠ **只对显示档位调用**（退役槽位无语言键，属刻意的零孤儿键策略）。
        /// </summary>
        public static string KeyOf(int tier)
        {
            return "PersonalChronicle.Economy.Stock.Tier." + KeySuffix[Resolve(tier)];
        }
    }
}

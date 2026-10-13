using System;
using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Application.Workshop
{
    /// <summary>
    /// **产出品质分布**（按 <c>QualityCategory</c> 七档的直方图，近 N 日窗口）。
    ///
    /// <para>口径与「① 总控 KPI · 良品率」**同源** —— 同样来自职业台账的 <c>ItemProduced</c> 事件，
    /// 同样用 <c>ExamScoring.QualityRank</c> 判定档位（**不另立门槛表**）；差别只是这里保留**每档计数**
    /// 而不是只算"良好及以上"的比例。</para>
    ///
    /// <para>近 15 日窗口内的产出，可**按配方过滤**（台账事件带 <c>RecipeDefName</c>；旧存档事件该字段为
    /// null ⇒ 归入全厂口径，不会错误地算进某个配方）。</para>
    /// </summary>
    public struct WorkshopQualityDigest
    {
        /// <summary>七档计数（下标 = <c>QualityCategory</c>：0 Awful … 6 Legendary）。</summary>
        public int[] Bins;

        /// <summary>可评品质的产出总数（= <see cref="Bins"/> 之和；**无品质产物不计入**）。</summary>
        public int Total;

        /// <summary>「良好及以上」件数（<c>QualityCategory.Good</c> 起）。</summary>
        public int Good;

        /// <summary>是否真的取到台账数据（<c>false</c> ⇒ 调用方走空态，**不显示一排 0 冒充"没有产出"**）。</summary>
        public bool HasData;
    }

    /// <summary>
    /// 「产出品质分布」只读查询口（Application 层契约；实现由 Automation 在启动时注入）。
    ///
    /// <para><b>为什么需要这层间接</b>：直方图要从职业台账（<c>IArchiveQueryService</c> 的
    /// Pawn 类目 → <c>CareerData.Events</c>）逐事件累计，而消费方（设备详情弹窗）在 Automation，
    /// 台账服务在 Application/Archive 侧 —— 直接互读会破坏依赖方向。故按工程既定范式
    /// （<see cref="WorkshopOpsAccess"/> / <c>WorkshopBonusAccess</c> 同族）建立受控静态钩子：
    /// Automation 侧用 <c>[StaticConstructorOnStartup]</c> 注入实现（Automation → Application 是允许方向）。</para>
    ///
    /// <para><b>降级</b>：未注入 / map 为空 / 实现抛异常 ⇒ 返回 <c>default</c>
    /// （<c>HasData = false</c>），调用方据此走空态。</para>
    /// </summary>
    public static class WorkshopQualityAccess
    {
        private static Func<Map, string, int, WorkshopQualityDigest> source;

        /// <summary>绑定实现（幂等：重复绑定覆盖旧委托；传 null 可解绑）。</summary>
        public static void SetSource(Func<Map, string, int, WorkshopQualityDigest> value)
        {
            source = value;
        }

        /// <summary>
        /// 取近 <paramref name="windowDays"/> 日的产出品质分布。
        ///
        /// <para><paramref name="recipeDefName"/> 非空 ⇒ **只统计该配方的产出**（台账 <c>RecipeDefName</c>
        /// 精确匹配）；为空 ⇒ 全厂口径。窗口 ≤ 0 时按 15 日处理。</para>
        /// </summary>
        public static WorkshopQualityDigest Digest(Map map, string recipeDefName, int windowDays)
        {
            Func<Map, string, int, WorkshopQualityDigest> fn = source;
            if (fn == null || map == null)
            {
                return default;
            }
            try
            {
                return fn(map, recipeDefName, windowDays);
            }
            catch (Exception)
            {
                // 单点异常不得冒泡到 UI（弹窗路径，铁律：异常隔离）
                return default;
            }
        }

        /// <summary>各档显示标签的顺序（与 <c>QualityCategory</c> 数值序一致，UI 直接按下标取词）。</summary>
        public static readonly string[] BinKeys =
        {
            "Awful", "Poor", "Normal", "Good", "Excellent", "Masterwork", "Legendary"
        };
    }
}

using Verse;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// CHG-20261004-MEALSCHEDULE-B · 吃饭格位归属台账的存档接线。
    ///
    /// <para><b>不 bump SchemaVersion</b>：旧档读回 null ⇒ 兜底空台账 ⇒ 无行为变化
    /// （与 <c>workshopWorkDemotion</c> / <c>stockSeries</c> / <c>workshopWorkAutoAssign</c>
    /// 同款先例，GATE-SAVE 加性可选字段）。</para>
    ///
    /// <para><b>为什么用 <c>Scribe_Deep</c> 而非 <c>Scribe_Collections</c></b>：
    /// 台账内是 <c>HashSet&lt;int&gt;</c> + <c>Dictionary&lt;int,int&gt;</c> 两个集合，
    /// 由 <see cref="MealPriorityLedger"/> 自己的 <c>ExposeData</c> 序列化语义更清楚。</para>
    /// </summary>
    public sealed partial class ChronicleGameComponent
    {
        /// <summary>
        /// ⚠ 必须内联初始化（GATE-SAVE P0-1 既有教训）：ExposeData 只在存/读档执行，
        /// 而餐段写入可能在任何一次存/读档之前就发生 ⇒ 无初始化器必然 NPE。
        /// </summary>
        public MealPriorityLedger MealPriority = new MealPriorityLedger();

        /// <summary>由主 ExposeData 链尾调用（保持主文件只加一行）。</summary>
        internal void ExposeMealPriority()
        {
            Scribe_Deep.Look(ref MealPriority, "mealPriorityLedger");
            if (MealPriority == null)
            {
                MealPriority = new MealPriorityLedger();
            }
            if (MealPriority.PlayerOwnedPawnIds == null)
            {
                MealPriority.PlayerOwnedPawnIds = new System.Collections.Generic.HashSet<int>();
            }
            if (MealPriority.LastWrittenByPawnId == null)
            {
                MealPriority.LastWrittenByPawnId = new System.Collections.Generic.Dictionary<int, int>();
            }
        }

        /// <summary>永不为 null 的消费入口。</summary>
        public MealPriorityLedger MealPriorityLedger
        {
            get
            {
                if (MealPriority == null)
                {
                    MealPriority = new MealPriorityLedger();
                }
                return MealPriority;
            }
        }
    }
}
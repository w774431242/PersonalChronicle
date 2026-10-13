using System;
using System.Collections.Generic;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 稳定降序排序（**同分保持原顺序**）—— 纯逻辑，可离线单测。
    ///
    /// <para><b>为何不用 <c>List&lt;T&gt;.Sort</c></b>：它是内省排序，**不稳定**，
    /// 同分元素的相对次序未定义。体检发现班组"同优先级按定义顺序"的注释与实际行为不符：
    /// 多个同优先级班组争抢同一名 pawn 时，人选会随列表增删而变化（岗位来回换人）。
    /// 名额分配侧的 <c>WorkshopShiftPlanner</c> 用"池内索引"做 tie-break 才是正确做法，
    /// 本类把同一纪律补到排序场景。</para>
    ///
    /// <para><b>规模与性能</b>：插入排序 O(n²)，但 n = 玩家定义的排班条数（个位数~几十条），
    /// 且只在调度计划重建时调用（有 dirty / 小时变化 / 60 tick 三重节流），无实际影响。</para>
    /// </summary>
    public static class StableOrder
    {
        /// <summary>按 <paramref name="keyOf"/> 降序稳定排序（原地）。</summary>
        public static void SortByDescending<T>(List<T> list, Func<T, int> keyOf)
        {
            if (list == null || keyOf == null || list.Count < 2)
            {
                return;
            }
            for (int i = 1; i < list.Count; i++)
            {
                T current = list[i];
                int key = keyOf(current);
                int j = i - 1;
                while (j >= 0 && keyOf(list[j]) < key)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = current;
            }
        }
    }
}

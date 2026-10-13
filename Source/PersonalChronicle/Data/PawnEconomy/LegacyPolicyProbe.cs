using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Data.PawnEconomy
{
    /// <summary>CHG-20261012-ECON-CONTRACTIZE · B2「旧档只读迁移探针」（SAVE-003 / M5）。
    ///
    /// <para><b>为什么需要</b>：旧档里已经写入过 <c>pawnEconomicPolicies</c> 节点（
    /// <c>InstitutionPolicySaveData</c> 的 <c>institutionPolicies</c> 列表）。B2 把机构政策整体净删后，
    /// 这个节点**不再有人声明** —— 若不做任何处理，"旧档能不能加载"就变成一条无从验证的断言。</para>
    ///
    /// <para><b>本类做什么</b>：以**极简结构**（只声明 <c>anchor</c> 一个字段）消费旧档的同一节点，
    /// 数出条目数并留一个锚点样本供诊断。旧节点的其它字段 Scribe 按"类里没声明的字段不读"的既有行为
    /// 自动忽略（这也是原版允许删字段的依据）。</para>
    ///
    /// <para><b>三条硬纪律</b>：① **只读** —— <see cref="ExposeData"/> 与内部 <see cref="Entry"/> 都在
    /// <c>mode != LoadingVars</c> 时直接返回，即使本类被误接进保存路径也**写不出任何东西**；
    /// ② **不抛** —— 缺节点 ⇒ 调用方拿到 null；缺 <c>anchor</c> 的行被跳过计数；畸形内容不抛异常；
    /// ③ **不静默** —— 真的读到条目时由 <see cref="Report"/> 记一行诊断（旧档含政策数据是**预期事实**，
    /// 因此走 <c>Log.Message</c> 而**不是** Warning/Error，免得把正常旧档刷成假告警）。</para>
    /// </summary>
    public sealed class LegacyPolicyProbe : IExposable
    {
        /// <summary>旧档节点名（唯一真值；生产代码只有一处引用它，见 GATE-DELTA 的写回封印断言）。</summary>
        public const string NodeName = "pawnEconomicPolicies";
        /// <summary>旧档列表键（与 <c>InstitutionPolicySaveData.ExposeData</c> 逐字一致）。</summary>
        public const string ListKey = "institutionPolicies";

        private List<Entry> entries;

        /// <summary>有效条目数（有 <c>anchor</c> 的行）——0 表示"旧档没有政策数据"。</summary>
        public int Count { get; private set; }
        /// <summary>首个有效条目的机构键（诊断用样本；无条目时为 null）。</summary>
        public string FirstAnchor { get; private set; }

        /// <summary>单条旧政策的最小投影：只认机构键。其余旧字段（工资/食品价/服务价/时段/库存目标…）
        /// 一律不声明 ⇒ Scribe 不读、更不写。</summary>
        public sealed class Entry : IExposable
        {
            public string AnchorKey;
            public void ExposeData()
            {
                if (Scribe.mode != LoadSaveMode.LoadingVars) return;
                Scribe_Values.Look(ref AnchorKey, "anchor");
            }
        }

        public void ExposeData()
        {
            if (Scribe.mode != LoadSaveMode.LoadingVars) return;
            Scribe_Collections.Look(ref entries, ListKey, LookMode.Deep);
            Count = 0;
            FirstAnchor = null;
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                var row = entries[i];
                if (row == null || string.IsNullOrEmpty(row.AnchorKey)) continue;
                Count++;
                if (FirstAnchor == null) FirstAnchor = row.AnchorKey;
            }
        }

        /// <summary>读档后的一行诊断（<c>Count &lt;= 0</c> 时**不出声**，不给正常旧档刷噪音）。</summary>
        public void Report()
        {
            if (Count <= 0) return;
            Log.Message("[PC][ECON] legacy policy node detected: " + Count + (Count == 1 ? " entry" : " entries")
                + " (first anchor: " + FirstAnchor + "). Node is read-only on load and is never written back;"
                + " policy values do not migrate (see CHG-20261012-ECON-CONTRACTIZE B2).");
        }
    }
}

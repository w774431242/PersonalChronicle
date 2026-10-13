using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// CHG-20261005-AUTOASSIGN · 自动分配台账的存档接线（加性可选字段）。
    ///
    /// <para>不 bump SchemaVersion：旧档读回 null ⇒ 兜底空台账 ⇒ 无任何行为变化
    /// （与workshopWorkDemotion / stockSeries 同款先例）。</para>
    /// </summary>
    public sealed partial class ChronicleGameComponent
    {
        /// ⚠ 必须内联初始化（GATE-SAVE P0-1 既有教训）：ExposeData 只在存/读档执行，
        /// 而派发按钮可能早于任何一次存/读档 ⇒ 无初始化器必然 NPE。
        public AutoAssignLedger AutoAssign = new AutoAssignLedger();

        /// <summary>由主 ExposeData 链尾调用（保持主文件只加一行）。</summary>
        internal void ExposeAutoAssign()
        {
            Scribe_Deep.Look(ref AutoAssign, "workshopWorkAutoAssign");
            if (AutoAssign == null)
            {
                AutoAssign = new AutoAssignLedger();
            }
        }

        /// <summary>永不为 null 的消费入口。</summary>
        public AutoAssignLedger AutoAssignLedger
        {
            get
            {
                if (AutoAssign == null)
                {
                    AutoAssign = new AutoAssignLedger();
                }
                return AutoAssign;
            }
        }
    }
}
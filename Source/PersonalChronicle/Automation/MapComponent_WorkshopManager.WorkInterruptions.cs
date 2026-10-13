using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// M9 §5（CHG-20261009-ECON-M9-S5）：「自动车间工作被中断」的**事实上报面**。
    ///
    /// <para>UpdateWorkshopDutyRuntime 是当前唯一会在生产环境主动
    /// EndCurrentJob(JobCondition.InterruptForced) 的地方（§5 第 2 步「结束本行动租约」）。
    /// 依赖方向是 Economy → Automation（经济层读车间状态）；Automation 不得反向调用经济层
    /// （MATRIX-001）。故本层只**报告事实**（谁 / 何因 / 何时），判定与记账由经济层取走
    /// （Drain，方向仍为 Economy → Automation）后按 Domain 的 §5/§4 规则完成。</para>
    ///
    /// <para><b>运行态，不入存档</b>（与 Runtime.cs 头注「运行态只重建」一致）：读档代价 =
    /// 极少数未被取走的事实丢失；不涉及金额、权利或凭据，故可接受且如实登记。</para>
    /// </summary>
    public partial class MapComponent_WorkshopManager
    {
        /// <summary>待取走事实的容量上限。满载后丢弃**新**事实（宁可少记，不要无界增长）。</summary>
        private const int MaximumInterruptionFacts = 64;

        /// <summary>待取走的中断事实（运行态；按 PawnKey + CauseCode 去重，取走后可再次入队）。</summary>
        private readonly List<WorkInterruptionFact> economicWorkInterruptions = new List<WorkInterruptionFact>();

        /// <summary>一次「经济活动相关的工作中断」的可观察事实。判定与记账都不在本层。</summary>
        public readonly struct WorkInterruptionFact
        {
            public string PawnKey { get; }
            public string CauseCode { get; }
            public int Tick { get; }
            public int MapId { get; }
            public WorkInterruptionFact(string pawnKey, string causeCode, int tick, int mapId)
            {
                PawnKey = pawnKey; CauseCode = causeCode; Tick = tick; MapId = mapId;
            }
        }

        /// <summary>
        /// 上报一次中断（幂等去重：同一 PawnKey + CauseCode 在被取走前只保留一条）。
        /// 原因码由 Domain 的失败分类表定义，见 FailureClassRules 的原因码常量。
        /// </summary>
        public void ReportEconomicWorkInterruption(Pawn pawn, string causeCode)
        {
            if (pawn == null || string.IsNullOrEmpty(causeCode) || map == null) return;
            int tick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            string key = pawn.GetUniqueLoadID();
            for (int i = 0; i < economicWorkInterruptions.Count; i++)
            {
                var row = economicWorkInterruptions[i];
                if (row.PawnKey == key && row.CauseCode == causeCode) return;
            }
            if (economicWorkInterruptions.Count >= MaximumInterruptionFacts) return;
            economicWorkInterruptions.Add(new WorkInterruptionFact(key, causeCode, tick, map.uniqueID));
        }

        /// <summary>取走全部事实并清空（经济层调用）。返回取走条数。</summary>
        public int DrainEconomicWorkInterruptions(List<WorkInterruptionFact> into)
        {
            if (into == null || economicWorkInterruptions.Count == 0) return 0;
            int moved = economicWorkInterruptions.Count;
            into.AddRange(economicWorkInterruptions);
            economicWorkInterruptions.Clear();
            return moved;
        }
    }
}

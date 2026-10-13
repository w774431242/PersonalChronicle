using System.Collections.Generic;

namespace PersonalChronicle.Domain.PawnEconomy.Learning
{
    /// <summary>一次中断**实际推进到**的阶段（取最靠后为真的一档）。</summary>
    internal enum InterruptionStage { NotExecuted = 0, Delivered = 1, Consumed = 2, Settled = 3 }

    /// <summary>
    /// 《09-经验反馈与失败恢复》§5 中断协议的**去重规则**（纯函数）。
    ///
    /// §5 第 5 条原文："**不能一个中断同时记『未买到、买到未吃、服务失败』三份相同失败**"。
    /// 即：一次中断只产生**一条**失败记录，取实际推进到的最靠后阶段 —— 否则同一次事故会被
    /// 记成三份失败，进而在退避账与可靠性上产生三倍惩罚。
    /// </summary>
    internal static class InterruptionRules
    {
        /// <summary>阶段定格：取最靠后为真的一档（已结算 &gt; 已使用 &gt; 已交付 &gt; 未执行）。</summary>
        public static InterruptionStage Stage(bool delivered, bool consumed, bool settled)
        {
            if (settled) return InterruptionStage.Settled;
            if (consumed) return InterruptionStage.Consumed;
            if (delivered) return InterruptionStage.Delivered;
            return InterruptionStage.NotExecuted;
        }

        /// <summary>
        /// 该阶段是否应记一次失败。**已结算不算失败** —— 钱物两清的中断只是行为被打断，
        /// 不是任何一方的违约。只有未结算且推进不完整时，才按"实际卡在哪一步"记一条。
        /// </summary>
        public static bool ShouldRecordFailure(InterruptionStage stage) => stage != InterruptionStage.Settled;
    }

    /// <summary>
    /// §5 中断协议与 §4 失败分类的**合成判决**（纯函数）。一次中断**至多产生一条**记录：
    /// 先按持久证据定格实际推进到的阶段（<see cref="InterruptionRules"/>），
    /// 再按**真实失败原因码**取规定处置（<see cref="FailureClassRules"/>）。
    /// </summary>
    internal readonly struct InterruptionVerdict
    {
        public InterruptionStage Stage { get; }
        /// <summary>是否应写**一条** M9 失败记录（安排复核由调用方按退避账落实）。</summary>
        public bool RecordFailure { get; }
        /// <summary>是否应把该差异交 M7/M11 对账（M9 不自行重放付款）。</summary>
        public bool RouteToReconciliation { get; }
        /// <summary>是否计入服务失败并更新可靠性（§4 只对 ActualDefault 开启）。</summary>
        public bool CountsAsServiceFailure { get; }
        public InterruptionVerdict(InterruptionStage stage, bool recordFailure,
            bool routeToReconciliation, bool countsAsServiceFailure)
        {
            Stage = stage; RecordFailure = recordFailure;
            RouteToReconciliation = routeToReconciliation; CountsAsServiceFailure = countsAsServiceFailure;
        }
    }

    /// <summary>
    /// §5 第 5 条（记录**一次**实际失败原因/经验并安排复核）的落地规则。两道闸：
    /// ① 已结算的中断不是失败（零记录，无论原因码怎么写）；
    /// ② §4 里"让路（ProtectedAction）／适配未知（GetterError）"两类**不进任何账**
    /// （宁可少记，不要错记）—— 其余类别允许写一条。
    /// </summary>
    internal static class InterruptionProtocolRules
    {
        public static InterruptionVerdict Resolve(bool delivered, bool consumed, bool settled, string reasonCode)
        {
            var stage = InterruptionRules.Stage(delivered, consumed, settled);
            if (!InterruptionRules.ShouldRecordFailure(stage))
                return new InterruptionVerdict(stage, false, false, false);
            var disposition = FailureClassRules.DispositionOf(FailureClassRules.Classify(reasonCode));
            return disposition.Backoff
                ? new InterruptionVerdict(stage, true, disposition.RouteToReconciliation, disposition.CountsAsServiceFailure)
                : new InterruptionVerdict(stage, false, false, false);
        }
    }

    /// <summary>
    /// §6 有界历史的一条动作结果。**只保留裁剪所需的最小信息** —— 设计明令
    /// "不保存原版对象快照/全量路径"。
    /// </summary>
    internal readonly struct HistoryEntry
    {
        public bool Terminal { get; }
        public int Tick { get; }
        /// <summary>是否持有**未清对账义务**（凭据，不得随经验裁剪）。</summary>
        public bool HoldsOpenReconciliation { get; }
        public HistoryEntry(bool terminal, int tick, bool holdsOpenReconciliation)
        {
            Terminal = terminal; Tick = tick; HoldsOpenReconciliation = holdsOpenReconciliation;
        }
    }

    /// <summary>
    /// §6 有界历史的**裁剪规则**（纯函数）。原型上限：每 Pawn 最多 64 个最近动作结果、最多 16 个活跃重试。
    ///
    /// 硬性例外（设计原文）："**未清对账义务和账本去重信息不随经验裁剪**" ——
    /// 它们不是经验，是财务凭据；裁剪掉会让工资或订单重新可结算（§2："经验不是账本"）。
    /// 故当"可裁条目"不足时，**宁可超限也不丢凭据**。
    /// </summary>
    internal static class BoundedHistoryRules
    {
        public const int MaximumRecentOutcomes = 64;
        public const int MaximumActiveRetries = 16;

        /// <summary>
        /// 给出应被移除的条目下标。顺序：先删**已终态**的（最老优先），仍超额再删**未终态**的（最老优先）。
        /// 持有未清对账义务的**一律不入选**。返回空集合表示无需裁剪。
        /// </summary>
        public static IReadOnlyList<int> Trim(IReadOnlyList<HistoryEntry> entries, int maximum)
        {
            var drop = new List<int>();
            if (entries == null || maximum < 0 || entries.Count <= maximum) return drop;
            int excess = entries.Count - maximum;
            // 先终态、后未终态；同级按 Tick 升序（最老先走）。§6："超出按终态/年龄裁剪"。
            Collect(entries, drop, excess, terminalPass: true);
            if (drop.Count < excess) Collect(entries, drop, excess, terminalPass: false);
            return drop;
        }

        private static void Collect(IReadOnlyList<HistoryEntry> entries, List<int> drop, int excess, bool terminalPass)
        {
            while (drop.Count < excess)
            {
                int best = -1;
                for (int i = 0; i < entries.Count; i++)
                {
                    var row = entries[i];
                    if (row.HoldsOpenReconciliation) continue;   // 凭据不可裁
                    if (row.Terminal != terminalPass) continue;
                    if (Contains(drop, i)) continue;
                    if (best < 0 || row.Tick < entries[best].Tick) best = i;
                }
                if (best < 0) return;   // 这一轮没有可裁的了
                drop.Add(best);
            }
        }

        private static bool Contains(List<int> rows, int value)
        {
            for (int i = 0; i < rows.Count; i++) if (rows[i] == value) return true;
            return false;
        }
    }
}

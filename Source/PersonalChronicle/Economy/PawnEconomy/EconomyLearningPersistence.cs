using System;
using System.Collections.Generic;
using PersonalChronicle.Data.PawnEconomy;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// M9 §6 落盘的**导入导出桥**：把会话内的 <c>styleLedgers</c> / <c>retryFailures</c> / <c>retryNextTick</c>
    /// 与 <see cref="LearningSaveData"/> 互转。只做搬运，不含规则（规则在 Domain）。
    ///
    /// 顺序纪律：**导出必须在 Scribe_Deep 之前**、恢复在 PostLoadInit —— 否则写出去的是上一次的快照。
    /// </summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        private LearningSaveData learning = new LearningSaveData();

        /// <summary>把会话内状态导出为可序列化行（写盘前调用）。</summary>
        internal void ExportLearningInto(LearningSaveData target)
        {
            if (target == null) return;
            target.Version = LearningSaveData.SchemaVersion;
            target.Ledgers.Clear();
            foreach (var pair in styleLedgers)
            {
                var ledger = pair.Value;
                if (ledger == null) continue;
                var row = new LearningSaveData.LedgerRow { PawnKey = pair.Key, DayStamp = ledger.DayStamp };
                foreach (var offset in ledger.Offsets)
                { row.OffsetDimensions.Add(offset.Key); row.OffsetValues.Add(offset.Value); }
                foreach (var used in ledger.UsedTodayByDimension)
                { row.BudgetDimensions.Add(used.Key); row.BudgetUsed.Add(used.Value); }
                foreach (string id in ledger.ProcessedIds) row.ProcessedSummaries.Add(id);
                target.Ledgers.Add(row);
            }
            target.Retries.Clear();
            foreach (var pair in retryNextTick)
            {
                retryFailures.TryGetValue(pair.Key, out int failures);
                target.Retries.Add(new LearningSaveData.RetryRow
                { Key = pair.Key, Failures = failures, NextTick = pair.Value });
            }
        }

        /// <summary>
        /// 从存档恢复。**不清空当日额度** —— §6 要求 Tick 回退/读档不得重获已消费额度，
        /// 故恢复走 <c>StyleLedger.Restore</c> 而非 <c>BeginDay</c>。
        /// </summary>
        internal void RestoreLearningFrom(LearningSaveData source)
        {
            if (source == null) return;
            styleLedgers.Clear();
            for (int i = 0; i < source.Ledgers.Count; i++)
            {
                var row = source.Ledgers[i];
                if (row == null || string.IsNullOrEmpty(row.PawnKey)) continue;
                var ledger = new PersonalChronicle.Domain.PawnEconomy.Learning.StyleLedger();
                ledger.Restore(row.DayStamp, Zip(row.OffsetDimensions, row.OffsetValues),
                    Zip(row.BudgetDimensions, row.BudgetUsed), row.ProcessedSummaries);
                styleLedgers[row.PawnKey] = ledger;
            }
            retryFailures.Clear();
            retryNextTick.Clear();
            for (int i = 0; i < source.Retries.Count; i++)
            {
                var row = source.Retries[i];
                if (row == null || string.IsNullOrEmpty(row.Key)) continue;
                retryFailures[row.Key] = row.Failures < 0 ? 0 : row.Failures;
                retryNextTick[row.Key] = row.NextTick;
            }
        }

        /// <summary>并列列表 → 键值对；长度不齐时按**较短者**截断，绝不越界读出错值。</summary>
        private static IEnumerable<KeyValuePair<string, float>> Zip(List<string> keys, List<float> values)
        {
            var rows = new List<KeyValuePair<string, float>>();
            if (keys == null || values == null) return rows;
            int count = keys.Count < values.Count ? keys.Count : values.Count;
            for (int i = 0; i < count; i++) rows.Add(new KeyValuePair<string, float>(keys[i], values[i]));
            return rows;
        }
    }
}

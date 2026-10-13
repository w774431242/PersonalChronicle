using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Data.PawnEconomy
{
    /// <summary>
    /// M9 §6 **有界历史与保存**。持久化**摘要、重试状态与有界事件去重水位**；
    /// **不保存原版对象快照或全量路径**（§6 明令）。
    ///
    /// **"经验不是账本"（§2）**：本容器的一切裁剪都不会使工资或订单重新可结算 ——
    /// 财务证据去重由 M11 的账本另存。故这里的丢失上限是"人格微调与重试历史回退"，不是权利或金额。
    /// </summary>
    internal sealed class LearningSaveData : IExposable
    {
        /// <summary>容器自身版本，供未来迁移识别（SAVE-001）。</summary>
        public const int SchemaVersion = 1;
        /// <summary>有界事件去重水位：已处理摘要 id 的保留上限（§6 各上限均属待剖析参数）。</summary>
        public const int MaximumProcessedSummaries = 256;
        public const int MaximumLedgers = 512;
        public const int MaximumRetries = 256;

        public int Version = SchemaVersion;
        public List<LedgerRow> Ledgers = new List<LedgerRow>();
        public List<RetryRow> Retries = new List<RetryRow>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref Version, "version", SchemaVersion);
            Scribe_Collections.Look(ref Ledgers, "ledgers", LookMode.Deep);
            Scribe_Collections.Look(ref Retries, "retries", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            // 旧档无此节点时，字段保持构造时的默认值 —— 这就是 SAVE-003 的"新字段有默认值"。
            if (Ledgers == null) Ledgers = new List<LedgerRow>();
            if (Retries == null) Retries = new List<RetryRow>();
            for (int i = Ledgers.Count - 1; i >= 0; i--) if (Ledgers[i] == null || string.IsNullOrEmpty(Ledgers[i].PawnKey)) Ledgers.RemoveAt(i);
            for (int i = Retries.Count - 1; i >= 0; i--) if (Retries[i] == null || string.IsNullOrEmpty(Retries[i].Key)) Retries.RemoveAt(i);
            while (Ledgers.Count > MaximumLedgers) Ledgers.RemoveAt(0);
            while (Retries.Count > MaximumRetries) Retries.RemoveAt(0);
        }

        /// <summary>每 Pawn 一本的 M9 学习账：每维已学偏移、按维度计的当日已用额度、已处理摘要水位。</summary>
        public sealed class LedgerRow : IExposable
        {
            public string PawnKey;
            /// <summary>当日额度所属的**游戏日序号**；-1 = 未知。用于"回退不重获额度"的判定。</summary>
            public int DayStamp = -1;
            public List<string> OffsetDimensions = new List<string>();
            public List<float> OffsetValues = new List<float>();
            public List<string> BudgetDimensions = new List<string>();
            public List<float> BudgetUsed = new List<float>();
            public List<string> ProcessedSummaries = new List<string>();

            public void ExposeData()
            {
                Scribe_Values.Look(ref PawnKey, "pawn");
                Scribe_Values.Look(ref DayStamp, "day", -1);
                Scribe_Collections.Look(ref OffsetDimensions, "dim", LookMode.Value);
                Scribe_Collections.Look(ref OffsetValues, "offset", LookMode.Value);
                Scribe_Collections.Look(ref BudgetDimensions, "bdim", LookMode.Value);
                Scribe_Collections.Look(ref BudgetUsed, "bused", LookMode.Value);
                Scribe_Collections.Look(ref ProcessedSummaries, "seen", LookMode.Value);
                if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
                OffsetDimensions = OffsetDimensions ?? new List<string>();
                OffsetValues = OffsetValues ?? new List<float>();
                BudgetDimensions = BudgetDimensions ?? new List<string>();
                BudgetUsed = BudgetUsed ?? new List<float>();
                ProcessedSummaries = ProcessedSummaries ?? new List<string>();
                // 两个并列列表长度不齐（旧档/损坏）时按**较短者**截断：宁可少恢复一维，也不越界读到错值。
                if (OffsetValues.Count > OffsetDimensions.Count) OffsetValues.RemoveRange(OffsetDimensions.Count, OffsetValues.Count - OffsetDimensions.Count);
                if (BudgetUsed.Count > BudgetDimensions.Count) BudgetUsed.RemoveRange(BudgetDimensions.Count, BudgetUsed.Count - BudgetDimensions.Count);
                while (ProcessedSummaries.Count > MaximumProcessedSummaries) ProcessedSummaries.RemoveAt(0);
            }
        }

        /// <summary>一条 M9 重试状态。键为 `pawnId|actionClass|reason`（Goal/ActionClass/Reason 的复合）。</summary>
        public sealed class RetryRow : IExposable
        {
            public string Key;
            public int Failures;
            public int NextTick;
            /// <summary>条件指纹（不含 Tick）；由 <c>RetryBackoffRules.Fingerprint</c> 产出，供诊断与变化比对。</summary>
            public string Fingerprint;

            public void ExposeData()
            {
                Scribe_Values.Look(ref Key, "key");
                Scribe_Values.Look(ref Failures, "failures");
                Scribe_Values.Look(ref NextTick, "next");
                Scribe_Values.Look(ref Fingerprint, "fp");
                if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
                if (Failures < 0) Failures = 0;
            }
        }
    }
}

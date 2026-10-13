using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// Partial of <see cref="ChronicleGameComponent"/> ——「接管原版工作」台账
    /// （<c>CHG-20261003-TAKEOVER</c> · Phase 2 / EXC-2026-016）。
    ///
    /// <para><b>为什么不复用降档台账</b>（<c>workshopWorkDemotion</c>）：降档把档位写成 <b>4</b>，
    /// 其还原闸门是「当前值<b>仍为 4</b> 才还原」；接管把档位写成 <b>0</b>，会被降档闸门判为
    /// 「玩家已改动」而<b>丢弃且永不还原</b>。两者语义互斥（一个是"降到最低"，一个是"取消勾选"），
    /// 混用会导致玩家点「解除接管」后<strong>永远还不回来</strong>。</para>
    ///
    /// <para><b>SchemaVersion 影响：无（加性可选字段）</b>，与既有两段布局台账同款策略：
    /// 旧档读回 <c>null</c> ⇒ 回落空台账，<b>不需要 Migration、不需要 bump 版本</b>。</para>
    /// </summary>
    public sealed partial class ChronicleGameComponent
    {
        /// <summary>一条接管记录：被 PC 取消勾选的工作类型 + 动手前的原档位。</summary>
        public sealed class TakeoverEntry : IExposable
        {
            /// <summary>原版 WorkTypeDef 的 defName（稳定 ID，GOV-009）。</summary>
            public string WorkTypeDefName = string.Empty;

            /// <summary>PC 动手前的档位（1..4）。</summary>
            public int OriginalPriority;

            public void ExposeData()
            {
                Scribe_Values.Look(ref WorkTypeDefName, "workType", string.Empty);
                Scribe_Values.Look(ref OriginalPriority, "originalPriority", 0);
            }
        }

        /// <summary>一个 pawn 的接管记录集合。</summary>
        public sealed class TakeoverPawnRecord : IExposable
        {
            /// <summary>pawn 唯一 ID（<c>Pawn.GetUniqueLoadID()</c> 的哈希形式）。</summary>
            public int PawnId;

            public List<TakeoverEntry> entries = new List<TakeoverEntry>();

            public void ExposeData()
            {
                Scribe_Values.Look(ref PawnId, "pawnId", 0);
                Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
                if (Scribe.mode == LoadSaveMode.PostLoadInit && entries == null)
                {
                    entries = new List<TakeoverEntry>();
                }
            }
        }

        private List<TakeoverPawnRecord> workTakeover;

        /// <summary>只读视图；未初始化时回落为空列表（调用方不必判空）。</summary>
        internal List<TakeoverPawnRecord> WorkTakeoverLedger
        {
            get
            {
                return workTakeover
                    ?? (workTakeover = new List<TakeoverPawnRecord>());
            }
        }

        /// <summary>由 <see cref="ExposeData"/> 调用（紧跟 <c>ExposeWorkDemotion()</c>）。</summary>
        private void ExposeWorkTakeover()
        {
            Scribe_Collections.Look(ref workTakeover, "workshopWorkTakeover", LookMode.Deep);
        }

        // ---- 记录维护（纯集合操作，不做引擎查询）------------------------------------

        /// <summary>
        /// 记录一次接管（<b>首记优先</b>：已有记录不覆盖，避免二次接管把原值越写越低）。
        /// </summary>
        public void RecordTakeover(int pawnId, string workTypeDefName, int originalPriority)
        {
            if (pawnId == 0 || string.IsNullOrEmpty(workTypeDefName))
            {
                return;
            }
            List<TakeoverPawnRecord> all = WorkTakeoverLedger;
            TakeoverPawnRecord rec = null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].PawnId == pawnId)
                {
                    rec = all[i];
                    break;
                }
            }
            if (rec == null)
            {
                rec = new TakeoverPawnRecord { PawnId = pawnId };
                all.Add(rec);
            }
            if (rec.entries == null)
            {
                rec.entries = new List<TakeoverEntry>();
            }
            for (int i = 0; i < rec.entries.Count; i++)
            {
                TakeoverEntry e = rec.entries[i];
                if (e != null && e.WorkTypeDefName == workTypeDefName)
                {
                    return;   // 首记优先
                }
            }
            rec.entries.Add(new TakeoverEntry
            {
                WorkTypeDefName = workTypeDefName,
                OriginalPriority = originalPriority
            });
        }

        /// <summary>丢弃一条记录（还原成功后、或玩家已自行改动时）。</summary>
        public void DiscardTakeover(int pawnId, string workTypeDefName)
        {
            List<TakeoverPawnRecord> all = WorkTakeoverLedger;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                TakeoverPawnRecord rec = all[i];
                if (rec == null || rec.PawnId != pawnId || rec.entries == null)
                {
                    continue;
                }
                for (int n = rec.entries.Count - 1; n >= 0; n--)
                {
                    TakeoverEntry e = rec.entries[n];
                    if (e != null && e.WorkTypeDefName == workTypeDefName)
                    {
                        rec.entries.RemoveAt(n);
                    }
                }
            }
        }

        /// <summary>丢弃一个 pawn 的全部记录（pawn 失效时）。</summary>
        public void DiscardTakeoverPawn(int pawnId)
        {
            List<TakeoverPawnRecord> all = WorkTakeoverLedger;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                if (all[i] != null && all[i].PawnId == pawnId)
                {
                    all.RemoveAt(i);
                }
            }
        }

        /// <summary>台账条目总数（UI 读数）。</summary>
        public int TakeoverEntryCount
        {
            get
            {
                int n = 0;
                List<TakeoverPawnRecord> all = WorkTakeoverLedger;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].entries != null)
                    {
                        n += all[i].entries.Count;
                    }
                }
                return n;
            }
        }
    }
}
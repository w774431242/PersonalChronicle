using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// CHG-20261005-AUTOASSIGN · 自动分配台账（第三本，与降档/接管并列但互不干涉）。
    ///
    /// <para>为什么必须单独一本：降档台账闸门是「当前==4」、接管台账是「当前==0」，
    /// 都是单值比较；而自动分配写入 1/2/3/4 四种值，需要「current == 本次写入值」的
    /// 多值闸门，否则无法区分「玩家自己改成 4」与「自动分配写成 4」。</para>
    ///
    /// <para>全程不写 0：0 是接管哨兵值，全工程只有 WorkTakeover.cs 一个授权调用点。
    /// 本台账写入一律 ∈ [1,4]。</para>
    ///
    /// <para>wasDisabled 记录该格位被自动分配【从 0 勾上】，撤销时才能恢复成「未勾选」，
    /// 而不只是恢复档位数字。</para>
    /// </summary>
    public class AutoAssignLedger : IExposable
    {
        public const int MaxPawnRecords = 256;
        public const int MaxEntriesPerPawn = 512;

        public class Entry : IExposable
        {
            public string workTypeDefName = string.Empty;
            public int originalPriority;
            public bool wasDisabled;
            public int assignedPriority;

            public void ExposeData()
            {
                Scribe_Values.Look(ref workTypeDefName, "workTypeDefName", string.Empty);
                Scribe_Values.Look(ref originalPriority, "originalPriority", 0);
                Scribe_Values.Look(ref wasDisabled, "wasDisabled", false);
                Scribe_Values.Look(ref assignedPriority, "assignedPriority", 0);
            }
        }

        public class PawnRecord : IExposable
        {
            public int pawnId;
            public List<Entry> entries = new List<Entry>();

            public void ExposeData()
            {
                Scribe_Values.Look(ref pawnId, "pawnId", 0);
                Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
                if (entries == null)
                {
                    entries = new List<Entry>();
                }
            }

            public Entry Find(string defName)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] != null && entries[i].workTypeDefName == defName)
                    {
                        return entries[i];
                    }
                }
                return null;
            }
        }

        public List<PawnRecord> pawns = new List<PawnRecord>();

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                Sanitize();
            }
            Scribe_Collections.Look(ref pawns, "pawns", LookMode.Deep);
            if (pawns == null)
            {
                pawns = new List<PawnRecord>();
            }
        }

        /// <summary>后记优先：连续两次分配时 original 应为第一次写入前的值。</summary>
        public Entry Record(int pawnId, string defName, int original, int assigned)
        {
            PawnRecord rec = RecordOf(pawnId);
            if (rec == null)
            {
                return null;
            }
            Entry e = rec.Find(defName);
            if (e == null)
            {
                if (rec.entries.Count >= MaxEntriesPerPawn)
                {
                    return null;
                }
                e = new Entry { workTypeDefName = defName };
                rec.entries.Add(e);
            }
            e.originalPriority = original;
            e.assignedPriority = assigned;
            e.wasDisabled = original <= 0;
            return e;
        }

        public PawnRecord RecordOf(int pawnId)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i] != null && pawns[i].pawnId == pawnId)
                {
                    return pawns[i];
                }
            }
            if (pawns.Count >= MaxPawnRecords)
            {
                return null;
            }
            PawnRecord rec = new PawnRecord { pawnId = pawnId };
            pawns.Add(rec);
            return rec;
        }

        public void Clear()
        {
            pawns.Clear();
        }

        /// <summary>XML 非法字符会让 XmlWellFormedWriter 进入 error 态 ⇒ 整档保存失败
        /// （2026-10-02 P0 事故）。保存前清洗，幂等。</summary>
        public void Sanitize()
        {
            if (pawns == null) { return; }
            for (int i = pawns.Count - 1; i >= 0; i--)
            {
                PawnRecord rec = pawns[i];
                if (rec == null) { pawns.RemoveAt(i); continue; }
                for (int j = rec.entries.Count - 1; j >= 0; j--)
                {
                    Entry e = rec.entries[j];
                    if (e == null) { rec.entries.RemoveAt(j); continue; }
                    e.workTypeDefName = WorkshopWorkDemotionLedger.SanitizeKey(e.workTypeDefName);
                    if (string.IsNullOrEmpty(e.workTypeDefName)) { rec.entries.RemoveAt(j); }
                }
                if (rec.entries.Count == 0) { pawns.RemoveAt(i); }
            }
        }
    }
}
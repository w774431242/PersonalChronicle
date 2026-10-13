using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 车间专属工位 —— PC 降档<b>台账</b>（`CHG-20261003-WORKSTATION`；后经 `CHG-20261005-NODEMOTE` **停用**）。
    ///
    /// <para><b>⚠ 2026-10-05 现状（`CHG-20261005-NODEMOTE`，玩家裁定移除降档通道）</b>：
    /// 本台账**已停止写入**（`EXC-2026-015` 的 Status 已改为 Superseded），只剩两个用途：</para>
    /// <list type="number">
    /// <item><b>旧档一次性迁移</b>：<c>MapComponent_WorkshopManager.RestoreAllDemotedWork</c>
    /// 由 <c>WorkshopManageView.Refresh</c> 幂等调用，把旧档里被压到 4 档的项还原
    /// （防原出口删除后这些项永久卡住）；</item>
    /// <item><b>存档兼容留档</b>：Scribe 段 <c>workshopWorkDemotion</c> 保留不删，避免改键结构。</item>
    /// </list>
    /// <para>⚠ 因此本类中 <c>PruneDeadPawns</c> 等"写入期维护"方法**已无生产调用方**（仅单测引用），
    /// 保留是为了不动既有单测与历史留档 —— **不要据此认为降档仍在运行**。</para>
    ///
    /// <para><b>为什么需要台账（历史背景，保留以便理解键设计）</b>：玩家在车间管理里排班 / 派工后，
    /// PC 会把「车间相关工作」提到最高档 1、把<b>其余所有工作统一降到最低档 4</b>（保持勾选，
    /// 绝不 <c>Disable</c>）。这直接冲撞 EXC-2026-013 的「绝不反向：不重排其它工作类型」承诺，
    /// 故由 EXC-2026-015 显式承接并授权。要让该写入<b>可逆</b>，必须精确记住
    /// 「PC 动过哪一项、改之前是多少」—— 否则一键解除会连玩家自己排的档位一起抹掉。</para>
    ///
    /// <para><b>键设计（GOV-009：只认稳定 ID）</b>：记录键 = <b>(pawn 标识, workTypeDefName)</b>，
    /// <b>刻意不含车间 / 班次 / 岗位</b>。这样"换档"（pawn 从 A 车间调到 B 车间、或岗位由生产
    /// 改为质检）在结构上<b>不可能</b>把 A 档的记录误用到 B 档 —— 记录只认"哪个 pawn 的哪个工作
    /// 类型被 PC 动过、原值多少"，与当前编排无关。还原时也只按同一 (pawn, workType) 精确回写。</para>
    ///    /// <para><b>pawn 标识用 <c>Pawn.thingIDNumber</c></b>（本工程既有跨档 pawn 键约定，同
    /// <c>ArchiveService.Live</c> 的 <c>livePawnCache</c> / <c>FloorDeviceDetailSnapshot.PawnThingId</c>）。
    /// 它随 Pawn 序列化，读档后不变；但<b>新 pawn 可能复用已死 pawn 的 ID</b> ⇒ 必须配
    /// 「死亡 / 离殖民地即丢弃 + 还原前校验当前值仍是 PC 写的 4」两道自愈，见各方法注释。</para>
    ///
    /// <para><b>纯逻辑（无 Pawn / WorkTypeDef 引用）</b>：本类只存稳定 ID 与整数档位，
    /// 因此可离线单测；把 pawn 解析与写入交给 Automation 门面（MATRIX-003：写入单点）。</para>
    ///
    /// <para><b>Scribe 纪律（GATE-SAVE）</b>：加性可选段，旧档读回 null ⇒ 上层归一为空台账，
    /// <b>不需要 bump SchemaVersion</b>（同 <c>ExposeItemFlow</c> / <c>ExposeStockSeries</c> 先例）。
    /// 键在保存前清洗为 XML 合法字符（同 <c>ItemFlowLedger.SanitizeSlotKeys</c> 的 P0 教训：
    /// 一个非法字符会让<b>整个存档保存失败</b>）。</para>
    /// </summary>
    public sealed class WorkshopWorkDemotionLedger : IExposable
    {
        /// <summary>单个 pawn 的记录条数上限（有界性硬约束，PERF-001）。
        /// 原版 WorkTypeDef ≈ 15，本 Mod 4 个 ⇒ 正常 ≤ 20；留 3 倍余量。</summary>
        public const int MaxEntriesPerPawn = 64;

        /// <summary>全台账 pawn 条数上限。超出后<b>拒绝新 pawn</b>（既有条目照常可用）。</summary>
        public const int MaxPawnRecords = 256;

        /// <summary>一条降档记录：某 pawn 的某工作类型被 PC 降到最低档之前，<b>它本来是多少</b>。</summary>
        public sealed class Entry : IExposable
        {
            /// <summary>原版 WorkTypeDef 的 defName（稳定 ID；不存 label / 翻译文本）。</summary>
            public string workTypeDefName;

            /// <summary>PC 动它之前的档位（1..4）。还原时按此回写。</summary>
            public int originalPriority;

            public void ExposeData()
            {
                Scribe_Values.Look(ref workTypeDefName, "workTypeDefName");
                Scribe_Values.Look(ref originalPriority, "originalPriority", 0);
            }
        }
        /// <summary>一个 pawn 的全部降档记录。</summary>
        public sealed class PawnRecord : IExposable
        {
            /// <summary>pawn 稳定标识 = <c>Pawn.thingIDNumber</c>。</summary>
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
        }

        /// <summary>全台账（落盘）。<b>旧档读回可能为 null</b> —— 消费方必须判 null。</summary>
        public List<PawnRecord> pawns = new List<PawnRecord>();

        // ---- 查询 ---------------------------------------------------------------

        /// <summary>记录总数（UI「台账中 N 项可解除」的数据源）。</summary>
        public int TotalEntryCount
        {
            get
            {
                int n = 0;
                if (pawns == null)
                {
                    return 0;
                }
                for (int i = 0; i < pawns.Count; i++)
                {
                    if (pawns[i] != null && pawns[i].entries != null)
                    {
                        n += pawns[i].entries.Count;
                    }
                }
                return n;
            }
        }

        /// <summary>该 pawn 的记录条数（状态行「已对齐 N 人」的数据源之一）。</summary>
        public int EntryCountOf(int pawnId)
        {
            PawnRecord rec = RecordOf(pawnId);
            return rec == null || rec.entries == null ? 0 : rec.entries.Count;
        }

        /// <summary>取该 pawn 的记录（内部用；不暴露可写集合）。</summary>
        public PawnRecord RecordOf(int pawnId)
        {
            if (pawns == null)
            {
                return null;
            }
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i] != null && pawns[i].pawnId == pawnId)
                {
                    return pawns[i];
                }
            }
            return null;
        }
        /// <summary>查某 (pawn, workType) 记录的原档位；无记录返回 false。</summary>
        public bool TryGetOriginal(int pawnId, string workTypeDefName, out int originalPriority)
        {
            originalPriority = 0;
            PawnRecord rec = RecordOf(pawnId);
            if (rec == null || rec.entries == null || string.IsNullOrEmpty(workTypeDefName))
            {
                return false;
            }
            for (int i = 0; i < rec.entries.Count; i++)
            {
                Entry e = rec.entries[i];
                if (e != null && e.workTypeDefName == workTypeDefName)
                {
                    originalPriority = e.originalPriority;
                    return true;
                }
            }
            return false;
        }

        // ---- 写入 ---------------------------------------------------------------

        /// <summary>
        /// 记一条降档（幂等、<b>首记优先</b>）。
        ///
        /// <para>若 (pawn, workType) 已有记录，说明 PC 早已动过这一项 —— 此刻读到的"当前档位"
        /// 可能是<b>玩家改动过的值</b>，拿它当 original 会把玩家意图洗掉。保留最早的原值才能真正
        /// 还原"PC 动过之前"的现场。返回 <c>true</c> = 新增了一条。</para>
        /// </summary>
        public bool Record(int pawnId, string workTypeDefName, int originalPriority)
        {
            if (string.IsNullOrEmpty(workTypeDefName))
            {
                return false;
            }
            EnsureList();
            PawnRecord rec = RecordOf(pawnId);
            if (rec == null)
            {
                if (pawns.Count >= MaxPawnRecords)
                {
                    return false;
                }
                rec = new PawnRecord { pawnId = pawnId };
                pawns.Add(rec);
            }
            if (rec.entries == null)
            {
                rec.entries = new List<Entry>();
            }
            for (int i = 0; i < rec.entries.Count; i++)
            {
                if (rec.entries[i] != null && rec.entries[i].workTypeDefName == workTypeDefName)
                {
                    return false;
                }
            }
            if (rec.entries.Count >= MaxEntriesPerPawn)
            {
                return false;
            }
            rec.entries.Add(new Entry
            {
                workTypeDefName = workTypeDefName,
                originalPriority = originalPriority
            });
            return true;
        }
        /// <summary>
        /// 丢弃一条记录（幂等）。用于「该记录已不再有效」的自愈：pawn 死亡 / 离殖民地、
        /// 该工作类型已被玩家改动、该类型重回保留集，等等。
        /// </summary>
        public bool Discard(int pawnId, string workTypeDefName)
        {
            PawnRecord rec = RecordOf(pawnId);
            if (rec == null || rec.entries == null || string.IsNullOrEmpty(workTypeDefName))
            {
                return false;
            }
            for (int i = 0; i < rec.entries.Count; i++)
            {
                if (rec.entries[i] != null && rec.entries[i].workTypeDefName == workTypeDefName)
                {
                    rec.entries.RemoveAt(i);
                    if (rec.entries.Count == 0)
                    {
                        pawns.Remove(rec);
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>丢弃某 pawn 的全部记录（pawn 死亡 / 离殖民地 / 退出排班范围）。</summary>
        public int DiscardPawn(int pawnId)
        {
            if (pawns == null)
            {
                return 0;
            }
            PawnRecord rec = RecordOf(pawnId);
            if (rec == null || rec.entries == null)
            {
                return 0;
            }
            int n = rec.entries.Count;
            pawns.Remove(rec);
            return n;
        }
        /// <summary>
        /// 剪掉已不属于本殖民地的 pawn 记录（死亡 / 离殖民地 / 释放为无主）。
        ///
        /// <para><b>为什么必须剪</b>：<c>thingIDNumber</c> 在新 pawn 生成时可能<b>复用</b>已死
        /// pawn 的 ID。若不剪，一次"一键全解除"会把 A 档（原 pawn）的记录写到 B 档（新 pawn）
        /// 头上 —— 这正是需求明令禁止的"把 A 档记录应用到 B 档"。</para>
        ///
        /// <para><b>失败隔离</b>：谓词抛异常时按"保留"处理（宁可台账脏一点，也不要误删玩家的
        /// 还原凭据）。</para>
        /// </summary>
        public int PruneDeadPawns(System.Func<int, bool> stillRelevant)
        {
            if (pawns == null || stillRelevant == null)
            {
                return 0;
            }
            int removed = 0;
            for (int i = pawns.Count - 1; i >= 0; i--)
            {
                PawnRecord rec = pawns[i];
                if (rec == null)
                {
                    pawns.RemoveAt(i);
                    removed++;
                    continue;
                }
                bool keep = true;
                try
                {
                    keep = stillRelevant(rec.pawnId);
                }
                catch (System.Exception)
                {
                    keep = true;
                }
                if (!keep)
                {
                    pawns.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }
        // ---- Scribe -------------------------------------------------------------

        public void ExposeData()
        {
            // ⚠ 存档硬约束（同 ItemFlowLedger P0 事故）：非法 XML 字符会让 XmlWellFormedWriter
            // 进入 error 态 ⇒ **整个存档保存失败**。保存前就地清洗，保证「内存键 == 存档键」。
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

        /// <summary>清洗全部键为 XML 合法字符（保存前调用；幂等）。</summary>
        public void Sanitize()
        {
            if (pawns == null)
            {
                return;
            }
            for (int i = pawns.Count - 1; i >= 0; i--)
            {
                PawnRecord rec = pawns[i];
                if (rec == null)
                {
                    pawns.RemoveAt(i);
                    continue;
                }
                if (rec.entries == null)
                {
                    pawns.RemoveAt(i);
                    continue;
                }
                for (int j = rec.entries.Count - 1; j >= 0; j--)
                {
                    Entry e = rec.entries[j];
                    if (e == null)
                    {
                        rec.entries.RemoveAt(j);
                        continue;
                    }
                    e.workTypeDefName = SanitizeKey(e.workTypeDefName);
                    if (string.IsNullOrEmpty(e.workTypeDefName))
                    {
                        rec.entries.RemoveAt(j);
                    }
                }
                if (rec.entries.Count == 0)
                {
                    pawns.RemoveAt(i);
                }
            }
        }
        /// <summary>
        /// 去掉 XML 1.0 不允许的字符（<c>XmlConvert.IsXmlChar</c> 的等价实现，
        /// 不引 <c>System.Xml</c> 依赖 —— 存档键的洁净度不值得多引一个程序集）。
        /// </summary>
        public static string SanitizeKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }
            int n = key.Length;
            char[] buf = null;
            for (int i = 0; i < n; i++)
            {
                char c = key[i];
                if (!IsXmlChar(c))
                {
                    if (buf == null)
                    {
                        buf = key.ToCharArray();
                    }
                    buf[i] = '_';
                }
            }
            return buf == null ? key : new string(buf);
        }

        /// <summary>XML 1.0 <c>Char</c> 判定（#x9 / #xA / #xD / [#x20-#xD7FF] / [#xE000-#xFFFD]）。</summary>
        private static bool IsXmlChar(char c)
        {
            if (c == 0x09 || c == 0x0A || c == 0x0D)
            {
                return true;
            }
            if (c < 0x20)
            {
                return false;
            }
            if (c <= 0xD7FF)
            {
                return true;
            }
            if (c < 0xE000)
            {
                return false;
            }
            return c <= 0xFFFD;
        }

        private void EnsureList()
        {
            if (pawns == null)
            {
                pawns = new List<PawnRecord>();
            }
        }
    }
}
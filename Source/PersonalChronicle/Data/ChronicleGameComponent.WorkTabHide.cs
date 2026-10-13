using System.Collections.Generic;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// CHG-20261004-WORKTAB-HIDE · 隐藏名单的存档层（加性可选字段）。
    ///
    /// <para>为什么不 bump SchemaVersion：本段是<strong>加性可选</strong>字段，旧档读回
    /// <c>null</c> ⇒ 兜底为空集合 ⇒ 面板行为与旧档完全一致，无需迁移（与
    /// <c>workshopWorkDemotion</c> / <c>stockSeries</c> 同款先例）。</para>
    ///
    /// <para>只存 <c>ThingID</c> 字符串：pawn 引用（LookMode.Reference）在跨地图 /
    /// 读档解析失败时会产生悬挂引用，字符串键更稳（Colonist Bar Hiding Reloaded 已实证该键稳定）。</para>
    /// </summary>
    public sealed partial class ChronicleGameComponent
    {
        /// ⚠ 必须内联初始化（GATE-SAVE P0-1 既有教训）：<c>ExposeData</c> 只在存/读档执行，
        /// 而 Gizmo 入口可能早于任何一次存/读档 ⇒ 无初始化器必然 NPE。
        public HashSet<string> hiddenWorkTabColonists = new HashSet<string>();

        /// <summary>
        /// PC **自动**隐藏的成员（CHG-20261005-NODEMOTE，2026-10-05 玩家批准 A 方案）。
        ///
        /// <para><b>为什么必须与 <see cref="hiddenWorkTabColonists"/> 分账</b>：后者同时装着
        /// "PC 自动隐藏的"与"玩家自己用 gizmo 隐藏的"。玩家裁定「出口自动化：人员被移除车间
        /// 体系后自动恢复显示原版工作面板」—— 若不分账，离开车间时会把**玩家手动隐藏的人也一并
        /// 放出来**（那是玩家自己的意图，PC 无权撤销）。</para>
        ///
        /// <para><b>显示判定 = 两集合的并集</b>（见 <see cref="IsHiddenFromWorkTab"/>）；
        /// 离开车间时**只从本集合移除** ⇒ 玩家手动隐藏的人始终不受影响。</para>
        ///
        /// <para><b>加性可选字段</b>：旧档缺键读回空集合 ⇒ 既有隐藏项**全部视为"玩家手动"**
        /// （保守，不动它们）⇒ 不需要 Migration（SAVE-004）。</para>
        /// </summary>
        public HashSet<string> autoHiddenWorkTabColonists = new HashSet<string>();

        /// <summary>由主 <c>ExposeData</c> 链尾调用（保持主文件只加一行）。</summary>
        internal void ExposeWorkTabHide()
        {
            // ⚠ 必须在 Saving 阶段清洗，保证「内存键 == 存档键」。
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                SanitizeHiddenWorkTab();
            }

            Scribe_Collections.Look(ref hiddenWorkTabColonists, "hiddenWorkTabColonists", LookMode.Value);
            // ⚠ CHG-20261005-NODEMOTE：新键（旧档缺省为空 ⇒ 既有隐藏项全视为"玩家手动"）。
            Scribe_Collections.Look(ref autoHiddenWorkTabColonists, "pcAutoHiddenWorkTab", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 幂等兜底：旧档 null →空集合；清洗逻辑可重复执行。
                if (hiddenWorkTabColonists == null)
                {
                    hiddenWorkTabColonists = new HashSet<string>();
                }
                if (autoHiddenWorkTabColonists == null)
                {
                    autoHiddenWorkTabColonists = new HashSet<string>();
                }
                SanitizeHiddenWorkTab();
            }
        }

        /// <summary>永不为 null 的消费入口（统一走这里，杜绝判 null 漏写）。</summary>
        public HashSet<string> HiddenWorkTabColonists
        {
            get
            {
                if (hiddenWorkTabColonists == null)
                {
                    hiddenWorkTabColonists = new HashSet<string>();
                }
                return hiddenWorkTabColonists;
            }
        }

        /// <summary>
        /// 是否在工作面板中隐藏（CHG-20261005-NODEMOTE：**两集合的并集**）：
        /// 玩家手动隐藏 ∪ PC 自动隐藏。分账后判定必须查两处，否则自动隐藏的成员会"藏了不生效"。
        /// </summary>
        public bool IsHiddenFromWorkTab(Pawn pawn)
        {
            if (pawn == null) return false;
            string id = pawn.ThingID;
            return WorkTabHideFilter.ShouldHide(id, HiddenWorkTabColonists)
                || WorkTabHideFilter.ShouldHide(id, AutoHiddenWorkTabColonists);
        }

        /// <summary>PC 自动隐藏集合的永不为 null 入口（与 <see cref="HiddenWorkTabColonists"/> 同规）。</summary>
        public HashSet<string> AutoHiddenWorkTabColonists
        {
            get
            {
                if (autoHiddenWorkTabColonists == null)
                {
                    autoHiddenWorkTabColonists = new HashSet<string>();
                }
                return autoHiddenWorkTabColonists;
            }
        }

        /// <summary>
        /// 隐藏成员的**并集去重计数**（CHG-20261005-NODEMOTE-AUDIT，审计 P1 修复）。
        ///
        /// <para><b>为什么必须去重</b>：同一人可能同时出现在两个集合里（旧档项被归入手动集、
        /// 之后又因加入车间被自动隐藏）⇒ 直接相加会重复计数，导致两处失真：
        /// ① 设置页「恢复显示全部殖民者」的**门控**误判（仅自动隐藏时不渲染按钮 ⇒
        /// 分账后的常态下逃生舱不可达）；② 清空后的「已恢复显示 N 名」虚高。</para>
        ///
        /// <para><b>零分配</b>：设置页绘制路径会调用它（每帧），故不得 new HashSet ——
        /// 用"手动计数 + 自动集中不在手动集的数量"直接算（自动集规模 = 车间成员数，很小）。</para>
        /// </summary>
        public int HiddenWorkTabCount
        {
            // 算法收口在 Domain 的纯函数里（单一实现 + 可离线单测，见 WorkTabHideFilter.CountUnion）。
            get { return WorkTabHideFilter.CountUnion(HiddenWorkTabColonists, AutoHiddenWorkTabColonists); }
        }

        /// <summary>
        /// PC 自动隐藏 / 恢复显示（CHG-20261005-NODEMOTE）：**只碰自动集合**，
        /// 绝不触碰玩家手动隐藏的项（那是玩家自己的意图，PC 无权撤销）。
        /// </summary>
        /// <returns>true = 集合发生了变化。</returns>
        public bool SetAutoHiddenFromWorkTab(Pawn pawn, bool hidden)
        {
            if (pawn == null) return false;
            string id = WorkTabHideFilter.SanitizeKey(pawn.ThingID);
            if (string.IsNullOrEmpty(id)) return false;
            bool changed = hidden
                ? AutoHiddenWorkTabColonists.Add(id)
                : AutoHiddenWorkTabColonists.Remove(id);
            if (changed)
            {
                NotifyWorkTabChanged();
            }
            return changed;
        }

        public void ToggleHiddenFromWorkTab(Pawn pawn)
        {
            SetHiddenFromWorkTab(pawn, !IsHiddenFromWorkTab(pawn));
        }

        /// <summary>
        /// 清空隐藏名单（= 恢复"工作面板显示全部殖民者"）。
        ///
        /// <para><b>为什么需要它</b>（CHG-20261004-WORKTAB-ORIGINAL）：自绘工作面板已按玩家决策删除，
        /// 面板内那颗「显示全部已隐藏」按钮随之消失。本方法是 <b>Mod 设置里唯一的恢复入口</b> ——
        /// 没有它，玩家一旦隐藏过某个殖民者就只能改存档才能看到。</para>
        ///
        /// <returns>被清除的条目数（0 = 本来就没有隐藏）。</returns>
        public int ClearHiddenFromWorkTab()
        {
            // CHG-20261005-NODEMOTE：分账后，「恢复显示全部殖民者」必须**两集合一起清** ——
            // 否则 PC 自动隐藏的那批仍留在面板外，玩家点一次却"没全恢复"（UI-009 不可自证）。
            // ⚠ NODEMOTE-AUDIT：计数取**并集去重**（原为两集合相加 ⇒ 同一人双集合时虚高）。
            int removed = HiddenWorkTabCount;
            if (removed == 0)
            {
                return 0;
            }
            HiddenWorkTabColonists.Clear();
            AutoHiddenWorkTabColonists.Clear();
            NotifyWorkTabChanged();
            return removed;
        }

        public void SetHiddenFromWorkTab(Pawn pawn, bool hidden)
        {
            if (pawn == null) return;

            string id = WorkTabHideFilter.SanitizeKey(pawn.ThingID);
            if (string.IsNullOrEmpty(id)) return;

            bool changed;
            if (hidden)
            {
                bool alreadyHidden = HiddenWorkTabColonists.Contains(id);
                bool roomLeft = HiddenWorkTabColonists.Count < WorkTabHideFilter.MaxHiddenColonists;
                changed = alreadyHidden || roomLeft;
                if (changed)
                {
                    HiddenWorkTabColonists.Add(id);
                }
            }
            else
            {
                changed = HiddenWorkTabColonists.Remove(id);
            }

            if (changed)
            {
                NotifyWorkTabChanged();
            }
        }

        /// <summary>
        /// 键洁净化 + 有界裁剪（保存前与读档后各跑一次，幂等）。
        /// </summary>
        private void SanitizeHiddenWorkTab()
        {
            if (hiddenWorkTabColonists == null) return;

            hiddenWorkTabColonists.RemoveWhere(
                id => string.IsNullOrEmpty(WorkTabHideFilter.SanitizeKey(id)));

            // ⚠ NODEMOTE-AUDIT（P2 修复）：**自动集合也必须清洗**。
            //   分账后原先只清洗手动集合，导致自动集合的过期键（消亡/离队成员的 ThingID）
            //   永不被清理 ⇒「内存键 == 存档键」与「名单有界」两条既有承诺只兑现了一半。
            if (autoHiddenWorkTabColonists != null)
            {
                autoHiddenWorkTabColonists.RemoveWhere(
                    id => string.IsNullOrEmpty(WorkTabHideFilter.SanitizeKey(id)));
            }

            if (hiddenWorkTabColonists.Count <= WorkTabHideFilter.MaxHiddenColonists) return;

            // 超限裁剪：保留前 N 个，其余丢弃，保证名单有界（PERF-001）。
            // ⚠ 上限**只约束手动集合**（玩家可主动隐藏的量）；自动集合的规模由"车间成员数"
            //   自然约束（每个成员一条），不参与裁剪 —— 否则成员多时会把玩家手动隐藏的挤掉。
            HashSet<string> kept = new HashSet<string>();
            foreach (string id in hiddenWorkTabColonists)
            {
                if (kept.Count >= WorkTabHideFilter.MaxHiddenColonists) break;
                kept.Add(id);
            }
            hiddenWorkTabColonists = kept;
        }

        /// <summary>
        /// 通知所有已打开的 PawnTable 型主标签重算名单。
        ///
        /// <para>为什么必须显式通知：<c>PawnTable</c> 缓存 <c>cachedPawns</c>，只在 dirty 时重算
        /// （IL 实测 <c>RecachePawns</c>）。<c>SetDirty()</c> 是 protected，
        /// 对外公开入口只有 <c>MainTabWindow_PawnTable.Notify_PawnsChanged()</c>。</para>
        ///
        /// <para>为什么遍历全部 PawnTable 标签而不是只指 Work：隐藏名单是「该殖民者不参与
        /// 优先级排班」，语义上对分配 / 作息表同样成立；且 <c>MainTabWindow</c> 是缓存单例
        /// （切标签触发 PreClose 但不销毁），已打开的窗口必须一并刷新。</para>
        /// </summary>
        internal static void NotifyWorkTabChanged()
        {
            WindowStack stack = Find.WindowStack;
            if (stack == null) return;

            // 注意：WindowStack 本身不实现 IEnumerable（1.6 实测），只能走它的 Windows 索引器。
            IList<Window> windows = stack.Windows;
            if (windows == null) return;

            for (int i = 0; i < windows.Count; i++)
            {
                MainTabWindow_PawnTable table = windows[i] as MainTabWindow_PawnTable;
                if (table != null)
                {
                    table.Notify_PawnsChanged();
                }
            }
        }
    }
}

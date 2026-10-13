using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// <see cref="ChronicleGameComponent"/> 的物品级产消账本分部（`REQ-MVS-013` /
    /// `CHG-MVS-ITEMFLOW-001`，2026-09-20）。
    ///
    /// <para><b>归属（GATE-DATA）</b>：账本是**殖民地的运行时事实**（可从捕获事件重建？
    /// 否 —— 它是**外部输入的历史累积**，读档后无法重算 ⇒ 属 Save Data，按
    /// 「必落盘 ⇔ 跨会话不可重建」判据落盘（`WorldComponent_Market.cs` 同款判据）。</para>
    ///
    /// <para><b>为什么不推进 SchemaVersion</b>：本字段是**加性可选字段** —— 旧档没有
    /// `itemFlow` 键 ⇒ `Scribe_Deep.Look` 读回 null ⇒ 归一为空账本 ⇒ 面板三列如实显示 `—`
    /// （"无历史数据"），无需迁移。这与编排布局的既有先例完全同规
    /// （`ChronicleGameComponent.cs` ExposeData 的 `ExposeLayout()` 注释：
    /// "加性可选字段，旧档读回空列表，无需 SchemaVersion 迁移"）。</para>
    ///
    /// <para>⚠ 写入口只在主线程（Capture 的 Harmony 钩子 = 游戏逻辑线程），与 Scribe
    /// 不交错 ⇒ 旋转期间不可能被存档读到（GATE-ARCH 评审 B-04 已裁定）。</para>
    /// </summary>
    public sealed partial class ChronicleGameComponent
    {
        /// <summary>
        /// 物品级产消账本（落盘）。**旧档读回 null** —— 消费方必须判 null（回退"无读数"）。
        /// </summary>
        public ItemFlowLedger ItemFlow;

        /// <summary>由主 <see cref="ExposeData"/> 链尾调用（保持主文件只加一行）。</summary>
        internal void ExposeItemFlow()
        {
            Scribe_Deep.Look(ref ItemFlow, "itemFlow");
            if (ItemFlow == null)
            {
                ItemFlow = new ItemFlowLedger();
            }
        }

        /// <summary>记一笔物品流（生产/消费）。懒建账本；读取当期 tick（无 TickManager 时记 0）。</summary>
        internal void RecordItemFlow(string defName, int qty, bool production)
        {
            if (ItemFlow == null)
            {
                ItemFlow = new ItemFlowLedger();
            }
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            ItemFlow.Record(defName, qty, now, production);
        }

        /// <summary>
        /// 读某物品的近两季日均速率（件/日）。<c>false</c> = 无账本/未建立（调用方画 `—`）。
        /// ⚠ 内部会做幂等的季度对齐（见 <see cref="ItemFlowLedger"/> 类注释的"对齐纪律"）。
        /// </summary>
        internal bool TryGetItemFlowRate(string defName, bool production, out float perDay)
        {
            perDay = 0f;
            if (ItemFlow == null)
            {
                return false;
            }
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            return ItemFlow.TryRatePerDay(defName, production, now, out perDay);
        }

        /// <summary>
        /// 记一笔**原料流向**（2026-09-20，`REQ-MVS-018`）：material 被 product 的生产消耗了 qty 件。
        /// 懒建账本；读当期 tick（无 TickManager 时记 0）。
        /// </summary>
        internal void RecordMaterialFlow(string material, string product, int qty)
        {
            if (ItemFlow == null)
            {
                ItemFlow = new ItemFlowLedger();
            }
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            ItemFlow.RecordMaterialFlow(material, product, qty, now);
        }

        /// <summary>
        /// 读某物品作为原料时近两季流向各产物的件数（降序）。
        /// <c>false</c> = **无流向记录**（⚠ 不等于"无流向" —— 只覆盖配方消耗路径）。
        /// </summary>
        internal bool TryGetMaterialFlows(string material,
            System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>> result)
        {
            if (ItemFlow == null)
            {
                return false;
            }
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            return ItemFlow.TryMaterialFlows(material, now, result);
        }
    }
}

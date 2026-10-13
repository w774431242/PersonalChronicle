using System.Collections.Generic;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// Partial of <see cref="ChronicleGameComponent"/> —— **库存数量蜡烛序列**（`SDD-LCE-003` §3，
    /// 经济中心阶段 3 落地批 ③）。
    ///
    /// <para><b>归属（GATE-DATA）</b>：与 <see cref="ItemFlowLedger"/> 同判据 —— 它是**外部输入的
    /// 历史累积**，读档后无法重算 ⇒ 属 Save Data，必须落盘；落法同为**加性可选段**
    /// （旧档无 `stockSeries` 键 ⇒ null ⇒ 空集合 ⇒ 面板画空态，**无需 Migration**）。</para>
    ///
    /// <para><b>采样性能（REQ §4.1 / PERF-001）</b>：节奏 1 次 / <see cref="StockSampleIntervalTicks"/>
    /// （250 ticks ⇒ ≈10 点 / 小时档）；单次成本 = 遍历 `ResourceCounter.AllCountedAmounts`
    /// （**O(有计数的品种)**，典型 ≤ 100，一次字典读 + 一次序列写入）⇒ 平均 &lt; 20 读/秒；
    /// 无全库扫描、无反射、无字符串拼接、无每帧分配。暂停时 tick 不跑 ⇒ 天然不采样。</para>
    ///
    /// <para><b>双轴纪律（SDD §2）</b>：采样门 = `TicksGame % 250`（与既有 `Sampling.cs` 同轴）；
    /// **入桶时刻 = `TicksAbs`**（与游戏日历的 小时/日/quadrum/年 同相）—— 本文件只做一次取值。</para>
    ///
    /// <para><b>惰建纪律</b>：数量为 0 且从未追踪过的物品**不建序列**（避免为"从未拥有"的物品
    /// 铺满全表）；已追踪的物品照实记录 0（清零事件要在 K 线上看得见）。</para>
    /// </summary>
    public sealed partial class ChronicleGameComponent : GameComponent
    {
        /// <summary>
        /// 库存序列采样节奏（ticks）：1 次 / 250 ticks ⇒ ≈10 点 / 小时档（REQ §4.1）。
        /// ⚠ 唯一定义点；周期跨度一律取 <see cref="EconomyPeriodTier"/>（GOV-006）。
        /// </summary>
        public const long StockSampleIntervalTicks = 250L;

        /// <summary>
        /// 全品种库存数量蜡烛（落盘）。
        ///
        /// <para>⚠ **必须内联初始化**（GATE-SAVE P0-1 修复）：`ExposeData` 只在**存/读档**执行，
        /// **新档不会进入**（`ChronicleGameComponent.cs:119-126` 自证；`08c` §13.1：新档钩子是
        /// `StartedNewGame`），而采样在 `GameComponentTick` 每 250 tick 解引用本字段 ⇒
        /// 若无初始化器 + 写入路径无惰建，**新档必然 NPE**（并连带跳过同 tick 的 reconcile）。</para>
        ///
        /// 旧档读回 null ⇒ 归一为空集合（空态，无需迁移）。
        /// </summary>
        public StockSeriesStore StockSeries = new StockSeriesStore();

        /// <summary>由主 <see cref="ExposeData"/> 链尾调用（主文件只加一行）。</summary>
        internal void ExposeStockSeries()
        {
            Scribe_Deep.Look(ref StockSeries, "stockSeries");
            if (StockSeries == null)
            {
                StockSeries = new StockSeriesStore();
            }
        }

        /// <summary>
        /// 采一次**全殖民地**库存数量（主线程；O(有计数的品种)）。无主图 / 无计数 ⇒ 直接返回。
        /// </summary>
        internal void SampleStockSeries()
        {
            try
            {
                SampleStockSeriesCore();
            }
            catch (System.Exception ex)
            {
                // ⚠ tick 路径不得让异常外泄：会跳过同 tick 的 reconcile，并波及其它 GameComponent 的 tick
                // （与 `Sampling.cs` 的 `RecordIdentityEvent` 整方法 try/catch 同规）。
                ChronicleLog.Error(ChronicleLog.Category.Provider, "stock series sampling failed: " + ex);
            }
        }

        private void SampleStockSeriesCore()
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map == null || map.resourceCounter == null)
            {
                return;
            }
            Dictionary<ThingDef, int> amounts = map.resourceCounter.AllCountedAmounts;
            if (amounts == null || amounts.Count == 0)
            {
                return;
            }
            if (StockSeries == null)
            {
                // 双重保险（与 ItemFlow 写入路径的惰建同规）：读档归一失败/未来重构下仍不得 NPE
                // （GATE-SAVE P0-1：写入路径在 GameComponentTick 中，异常会连带跳过 reconcile）。
                StockSeries = new StockSeriesStore();
            }
            long absTick = Find.TickManager != null ? Find.TickManager.TicksAbs : 0L;

            foreach (KeyValuePair<ThingDef, int> pair in amounts)
            {
                if (pair.Key == null)
                {
                    continue;
                }
                string defName = pair.Key.defName;          // 稳定 ID（GOV-009：不依赖 label）
                if (string.IsNullOrEmpty(defName))
                {
                    continue;
                }
                int qty = pair.Value;
                if (qty <= 0 && StockSeries.Get(defName) == null)
                {
                    continue;   // 从未拥有 ⇒ 不建序列（有界性）
                }
                StockSeries.AppendSample(defName, absTick, qty);
            }
        }
    }
}

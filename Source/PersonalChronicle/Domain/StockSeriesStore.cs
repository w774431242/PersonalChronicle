using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 全品种的库存数量蜡烛集合（**按 defName 惰建**，`SDD-LCE-003` §3）。
    ///
    /// <para><b>有界性（PERF 硬约束，GOV-006）</b>：键数上限
    /// <see cref="MaxTrackedDefs"/>（同 <see cref="ItemFlowLedger.MaxKeysPerSlot"/> 量级思路），
    /// 超出**拒绝新键**（已存在的键照常写入）+ 节流告警，不得改为无界增长。</para>
    ///
    /// <para><b>惰建</b>：只有真正采到过的品种才建序列（原版可计数 ThingDef 远少于全表）。
    /// 读档后键缺失 ⇒ 空集合，消费方画空态（无 Migration，SAVE-001 加性可选段）。</para>
    /// </summary>
    public sealed class StockSeriesStore : IExposable
    {
        /// <summary>
        /// 可追踪品种数上限（有界性）。原版 `CountAsResource` 物品远少于此，
        /// 留足 Mod 物品余量；超出后拒绝新键。
        /// </summary>
        public const int MaxTrackedDefs = 1024;

        /// <summary>告警节流间隔（tick）—— 沿用 `ItemFlowLedger.OverflowWarnIntervalTicks` 范式。</summary>
        public const int OverflowWarnIntervalTicks = 6000;

        private Dictionary<string, StockSeries> items = new Dictionary<string, StockSeries>();

        /// <summary>告警节流（会话内瞬态，不落盘）。</summary>
        [Unsaved]
        private long lastOverflowWarnTick = -1L;

        /// <summary>已追踪品种数。</summary>
        public int Count
        {
            get { return items != null ? items.Count : 0; }
        }

        /// <summary>取序列（未追踪 ⇒ `null`）。</summary>
        public StockSeries Get(string defName)
        {
            if (string.IsNullOrEmpty(defName) || items == null)
            {
                return null;
            }
            StockSeries series;
            return items.TryGetValue(defName, out series) ? series : null;
        }

        /// <summary>
        /// 取或建（**写入路径**）。超过 <see cref="MaxTrackedDefs"/> ⇒ 拒绝新键并节流告警。
        /// </summary>
        public StockSeries GetOrCreate(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }
            if (items == null)
            {
                items = new Dictionary<string, StockSeries>();
            }
            StockSeries series;
            if (items.TryGetValue(defName, out series))
            {
                return series;
            }
            if (items.Count >= MaxTrackedDefs)
            {
                WarnOverflow(defName);
                return null;
            }
            series = new StockSeries();
            items.Add(defName, series);
            return series;
        }

        /// <summary>并入一个采样点（数量型；内部转 float）。</summary>
        public void AppendSample(string defName, long absTick, int quantity)
        {
            StockSeries series = GetOrCreate(defName);
            if (series == null)
            {
                return;
            }
            series.AppendSample(absTick, quantity);
        }

        /// <summary>全部条目（只读遍历；绘制期**不得**改写）。</summary>
        public IEnumerable<KeyValuePair<string, StockSeries>> Entries
        {
            get { return items != null ? (IEnumerable<KeyValuePair<string, StockSeries>>)items : EmptyEntries; }
        }

        private static readonly List<KeyValuePair<string, StockSeries>> EmptyEntries =
            new List<KeyValuePair<string, StockSeries>>();

        /// <summary>是否有任何品种的任何档有数据（整页空态判据）。</summary>
        public bool HasAnyData
        {
            get
            {
                if (items == null || items.Count == 0)
                {
                    return false;
                }
                foreach (KeyValuePair<string, StockSeries> pair in items)
                {
                    if (pair.Value != null && pair.Value.HasAnyData)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 有界键溢出告警（节流）。
        ///
        /// <para>⚠ **离线隔离**（`ItemFlowLedger.WarnOverflow` 同款先例，`[NoInlining]` + try/catch）：
        /// 单测环境没有 `TickManager`/`Log` 宿主 —— 若不隔离，`MaxTrackedDefs` 这条**有界性硬约束**
        /// 反而无法写用例（一碰就抛），等于永久失去回归保护。</para>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WarnOverflow(string defName)
        {
            long now = 0L;
            try
            {
                now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            }
            catch (System.Exception)
            {
                now = 0L;
            }
            if (lastOverflowWarnTick >= 0L && now - lastOverflowWarnTick < OverflowWarnIntervalTicks)
            {
                return;
            }
            lastOverflowWarnTick = now;
            try
            {
                Log.Warning("[PersonalChronicle] 库存序列品种数已达上限 " + MaxTrackedDefs
                    + "，拒绝新键：" + defName + "（有界性硬约束，不得改为无界增长）");
            }
            catch (System.Exception)
            {
                // 无日志宿主（离线/单测）：仅节流计数生效，不回抛 —— 告警不得反过来崩掉写入路径。
            }
        }

        /// <summary>
        /// Scribe：`defName → StockSeries`（键 = 稳定 ID，值 = Deep）。
        /// ⚠ 不可用嵌套字典的 `LookMode.Value`（<see cref="ItemFlowLedger"/> 已记录该存档级事故）。
        /// </summary>
        /// <summary>
        /// 本段（stockSeries）的**段内版本位**（v1 = 1）。
        ///
        /// <para>⚠ 为什么需要（GATE-SAVE EXC-2）：宿主 `SchemaVersion` 只表达**全局**身份，
        /// 无法区分"本段形状变了" ⇒ 未来改档数 / 保留阶梯 / 字段语义时没有识别手段。
        /// 缺省值取 1 ⇒ **旧段无 `v` 键读回 1**，与现状语义一致 ⇒ 加性可选、零迁移。</para>
        /// </summary>
        public int StockSeriesSchema = 1;

        public void ExposeData()
        {
            Scribe_Values.Look(ref StockSeriesSchema, "v", 1);   // 缺省 1：旧档读回 1（无迁移）
            Scribe_Collections.Look(ref items, "items", LookMode.Value, LookMode.Deep);
            if (items == null)
            {
                items = new Dictionary<string, StockSeries>();
            }
        }
    }
}

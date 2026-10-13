using System.Collections.Generic;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 单根蜡烛（**快照侧**副本，`SDD-LCE-003` §4）。
    ///
    /// <para><b>为什么不直接暴露 <c>Domain.StockCandle</c></b>：快照是 UI 的唯一数据源，
    /// 与领域类型解耦 ⇒ UI 不反向依赖 Domain 的内部类型（GOV-005 / ARC-001）；
    /// 且 <c>Up</c> 等绘制判据在**构建期**算好，绘制路径零计算、零分配（PERF-001）。</para>
    /// </summary>
    public sealed class ColonyStockCandle
    {
        /// <summary>开（桶内首个样本）。</summary>
        public float Open;

        /// <summary>高（桶内极大）。</summary>
        public float High;

        /// <summary>低（桶内极小）。</summary>
        public float Low;

        /// <summary>收（桶内最新样本）。</summary>
        public float Close;

        /// <summary>桶内样本数（0 = 无样本）。</summary>
        public int Samples;

        /// <summary>收 ≥ 开（构建期算好的涨/跌判据）。</summary>
        public bool Up;

        /// <summary>是否平线（doji；绘制期按最小实体处理）。</summary>
        public bool Flat;
    }

    /// <summary>
    /// 品种行（左侧选择列表 + 热力图格子同源，`SDD-LCE-003` §4）。
    /// </summary>
    public sealed class ColonyStockItemRow
    {
        /// <summary>稳定 ID（defName；GOV-009：逻辑不依赖 label）。</summary>
        public string DefName = string.Empty;

        /// <summary>显示名（① 原版真值；已本地化，键缺失回退原版 label）。</summary>
        public string Label = string.Empty;

        /// <summary>一级类目标签（① 原版真值；树图分组口径）。</summary>
        public string CategoryLabel = string.Empty;

        /// <summary>当前库存数量（① 原版真值：ResourceCounter）。</summary>
        public int Quantity;

        /// <summary>库存价值 = 数量 × BaseMarketValue（① 原版真值；树图面积权重）。</summary>
        public float Value;

        /// <summary>选中档位的窗口涨跌幅（%）。</summary>
        public float DeltaPct;

        /// <summary>
        /// `false` = 无读数（无序列 / 首根开为 0）⇒ UI 画 `—`，**不画 0**（LAND-009 诚实）。
        /// </summary>
        public bool HasDelta;
    }

    /// <summary>
    /// 经济中心「库存 K 线 + 热力图」整页快照（`SDD-LCE-003` §4）。
    ///
    /// <para><b>非 null 即渲染（LAND-010）</b>：<c>IsEmpty</c> = 无任何可行数据 ⇒
    /// UI 走空态说明（"采样中 · 尚无记录"），不画占位 0 值蜡烛/格子。</para>
    /// </summary>
    public sealed class ColonyStockGallerySnapshot : ArchiveSectionSnapshot
    {
        /// <summary>解析后的选中品种（空 = 无可选品种）。</summary>
        public string SelectedDef = string.Empty;

        /// <summary>
        /// 选中的**显示档位索引**（0..<c>EconomyPeriodTier.DisplayCount</c>−1 = 日/周/月/季/年）。
        /// 槽位映射唯一入口 = <c>EconomyPeriodTier.TierAt</c>（退役的 60分 槽位不在显示域内）。
        /// </summary>
        public int SelectedTier = -1;

        /// <summary>
        /// 显示档位标签（已本地化；五档 = 日/周/月/季/年；顺序 = 显示索引）。
        /// </summary>
        public List<string> TierLabels = new List<string>();

        /// <summary>品种行（按库存价值降序；左列表与树图共用）。</summary>
        public List<ColonyStockItemRow> Items = new List<ColonyStockItemRow>();

        /// <summary>选中品种 × 选中档的蜡烛（最老在前）。</summary>
        public List<ColonyStockCandle> Candles = new List<ColonyStockCandle>();

        /// <summary>
        /// 是否有**任何**实测记录（区分"采样中"与"真无数据"，LAND-009）。
        /// 与 `IsEmpty` 不同：目录可能非空（有库存真值）但序列**尚无一根蜡烛**。
        /// </summary>
        public bool HasAnyData;

        /// <summary>
        /// 构建期是否发生异常（失败隔离族）：`true` ⇒ UI 走 Error 文案，**不得**表述为"采样中"。
        /// </summary>
        public bool HasError;
    }
}

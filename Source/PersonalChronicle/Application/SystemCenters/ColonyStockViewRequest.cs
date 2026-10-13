namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 经济中心「库存 K 线 / 热力图」的**视图请求**（`SDD-LCE-003` §4）。
    ///
    /// <para><b>为什么有这层</b>：蜡烛数据量大（档位 × 品种 × 有界蜡烛），
    /// 不能整份塞进快照（那是无界的）。交互选中态（品种 / 档位）必须**显式传参**，
    /// 而不是让 Provider 去猜 UI 状态 —— 快照侧只读、请求侧只传值。</para>
    ///
    /// <para><b>状态归属</b>：请求由**窗口瞬时态**字段拼装（UI 瞬时，不入档、不进 Def，GOV-003）；
    /// 缺省值 =「未选」⇒ Provider 自行解析（档位取 <c>EconomyPeriodTier.DefaultTier</c>、
    /// 品种取库存价值最高者）。</para>
    /// </summary>
    public struct ColonyStockViewRequest
    {
        /// <summary>选中品种（defName；null / 空 = 未选）。</summary>
        public string DefName;

        /// <summary>选中档位（0..5；-1 = 未选）。</summary>
        public int TierIndex;

        public ColonyStockViewRequest(string defName, int tierIndex)
        {
            DefName = defName;
            TierIndex = tierIndex;
        }

        /// <summary>未选请求（Provider 解析默认）。</summary>
        public static ColonyStockViewRequest Unselected
        {
            get { return new ColonyStockViewRequest(null, -1); }
        }
    }
}

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 嵌入承载档位（由「可用宽高 + 快照声明的空间预算」判定，纯函数、离线可测）。
    ///
    /// <para>把降级判定放在 Read Model 侧而不是绘制侧，理由与 <see cref="SystemCenterTone"/> 同规：
    /// 阈值判定集中在数据/模型层，UI 只做「档位 → 画法」映射，不允许各处自行比大小
    /// ——否则同一个界面在不同调用点会出现不同的折叠行为。</para>
    /// </summary>
    public enum ArchiveViewTier
    {
        /// <summary>完整外壳：左侧导航 + 内容区并存。</summary>
        Full = 0,

        /// <summary>紧凑外壳：左侧导航折叠为顶部横条，内容区吃满宽度（LAND-006 降级档 1）。</summary>
        Compact = 1,

        /// <summary>空间不足：不渲染主界面，只如实提示需要的最小尺寸（LAND-009 诚实 &gt; 观感）。</summary>
        TooSmall = 2
    }

    /// <summary>
    /// 承载档位判定（纯函数）：只读快照里声明的阈值，不读任何中心名、不读 Def。
    /// </summary>
    public static class ArchiveViewLayout
    {
        /// <summary>
        /// 判定档位。<paramref name="width"/> / <paramref name="height"/> 为**实际可用区**，
        /// 阈值来自 <paramref name="snapshot"/>（空间预算的唯一来源是提供方声明的数据，不在 UI 侧硬编码）。
        /// 快照为空、或预算字段缺失（&lt;= 0）时一律按 <see cref="ArchiveViewTier.TooSmall"/> 处理：
        /// 宁可提示「空间不足」，也不按猜测的尺寸硬画。
        /// </summary>
        public static ArchiveViewTier Resolve(float width, float height, ArchiveViewSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return ArchiveViewTier.TooSmall;
            }
            float minWidth = snapshot.MinWidth > 0f ? snapshot.MinWidth : float.MaxValue;
            float minHeight = snapshot.MinHeight > 0f ? snapshot.MinHeight : float.MaxValue;
            if (width < minWidth || height < minHeight)
            {
                return ArchiveViewTier.TooSmall;
            }
            float compactWidth = snapshot.CompactWidth > 0f ? snapshot.CompactWidth : minWidth;
            return width < compactWidth ? ArchiveViewTier.Compact : ArchiveViewTier.Full;
        }
    }
}

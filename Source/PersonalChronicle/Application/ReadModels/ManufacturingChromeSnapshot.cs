namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 制造系统中心的**页面外壳**快照（对应预览的 `#top` 顶栏 + `#ticker` 底栏）。
    ///
    /// <para>预览整页结构 = <b>顶栏（品牌 / 游戏时钟 / 电网负载 / 换数据）+ ①~⑦ 分区 + 底栏（工厂广播）</b>，
    /// 三者缺一都会让"整体布局"与预览对不上 —— 本快照承载顶栏与底栏需要的全部字段（预格式化）。</para>
    /// </summary>
    public sealed class ManufacturingChromeSnapshot : ArchiveSectionSnapshot
    {
        /// <summary>品牌名（"制造系统中心"，与预览 `#top .brand b` 同文）。</summary>
        public string Brand = string.Empty;

        /// <summary>游戏日期（预览 `#gdate` 形如 "Y3·春·D17"）。</summary>
        public string DateText = string.Empty;

        /// <summary>游戏时刻（预览 `#gtime` 形如 "08:32"）。</summary>
        public string TimeText = string.Empty;

        /// <summary>电网负载标签（预览左侧两行小字："电网负载 · 发-储-载"）。</summary>
        public string PowerLabel = string.Empty;

        /// <summary>电网负载读数（预览 `#pnum`："已用 / 总容量 W"）。</summary>
        public string PowerText = string.Empty;

        /// <summary>负载占比 0..1（画 `#pbar` 的填充；预览按 60%/85% 分段着色）。</summary>
        public float PowerShare01;

        // ⚠ 2026-10-06（用户指令「kpi 也要移除」）：原 `KpiLineItem` 结构与 `KpiLine` 字段已删除 ——
        //   顶栏 `#kpiline` 是 ① 区九张 KPI 卡的单行摘抄，而制造中心不再产出 KPI ⇒ 无源可抄。
        //   连带删除：`SystemCenterReadModelProvider.FillKpiLine` / `Chrome.DrawKpiLine` /
        //   `Chrome.KpiLineToneColor`。经济中心与人员中心的 KPI 各有其条带机制，不受影响。

        /// <summary>底栏广播文本（预览 `#tkmove` 的滚动内容）。</summary>
        public string TickerText = string.Empty;
    }
}

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// **聚合页的页签号（hosted tab id）** —— CHG-20261010-ECON-AGG-MIGRATE 末批：原先是
    /// <c>Dialog_PawnEconomy</c> 上的常量，聚合窗口删除后迁到这里，继续作为
    /// 「柜台切换条 ↔ 读入口 <c>Query(map, focus, tab)</c>」之间的稳定契约。
    ///
    /// <para><b>数字必须与 <c>GameComponent_PawnEconomy.Query</c> 的 <c>switch (tab)</c> 一一对应</b>
    ///（<c>0..5</c> = 机构 / 岗位 / 库存 / 账户 / 台账 / 报表）—— 改数字即破坏所有承载页。</para>
    ///
    /// <para><b>CHG-20261012 · B2</b>：`Policy = 7` **已删除** —— 它曾是唯一"不是 <c>Query</c> 页签"的例外
    /// （政策页由 <c>PolicyPanel</c> 自绘，编号只用于切换条导航）。政策页整体退役后该例外不存在，
    /// 承载页全部是 <c>Query</c> 页签 ⇒ 页签号与渲染器之间不再需要任何特殊说明。</para>
    /// </summary>
    internal static class EconomyHostedTabs
    {
        internal const int Institutions = 0;
        internal const int Jobs = 1;
        internal const int Stock = 2;
        internal const int Accounts = 3;
        internal const int Ledger = 4;
        internal const int Reports = 5;
    }
}

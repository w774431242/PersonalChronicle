using System.Globalization;

namespace PersonalChronicle.Application.PawnEconomy
{
    /// <summary>CHG-20261012-ECON-CONTRACTIZE · B1（P1-2）：「工作合同」表**行载荷的唯一组装点**。
    ///
    /// <para><b>为什么单独成类</b>：合同行有两型 —— **Posted（已派发待承接）** 与 **Active（承接中）** ——
    /// 二者必须**逐格同构**：同一张表的列集只有一份，行载荷格数一旦分叉就是"整列静默串列"或
    /// "控件凭空消失"（两者都不报错、只在屏幕上错位）。收进一个纯函数后，三条契约可**离线断言**：
    /// ① 两型同列数（= <see cref="ColumnCount"/>）；② 撤销载荷只挂在 Posted 行；③ 末列恒空
    /// （由**控件格**绘制，见 <c>EconomyCellControl.Action</c>）。</para>
    ///
    /// <para><b>纯函数</b>：入参是**已本地化**的文本与数值，本类零引擎依赖、零翻译调用 ⇒ 与
    /// <see cref="EconomyTableOps"/> 同属可离线断言的 Application 层（GOV-003：UI 数据不掺业务状态）。</para>
    /// </summary>
    internal static class VanillaContractRow
    {
        /// <summary>列数 —— 与读模型 <c>BuildVanillaContractTable</c> 的列声明**同源**（声明处带一致性自检，
        /// 失配当场报错）。行载荷的格数必须与之一致，故本常量是"格数"的唯一来源。</summary>
        internal const int ColumnCount = 8;
        /// <summary>末列 = **控件格**（操作）：文本恒空，呈现层按 <see cref="EconomyRow.Revoke"/> 决定是否画控件。</summary>
        internal const int ActionColumnIndex = 7;
        /// <summary>撤销动词（色调规则按动词取色，见 <c>EconomyRowList.RowButtonTone</c>：破坏性动词统一危险色）。</summary>
        internal const string RevokeVerb = "Revoke";
        private const string EmptyCell = "";

        /// <summary>组装一行。**Posted 与 Active 共用本函数** —— <paramref name="revokeLabel"/> 非空即 Posted 型
        /// （该行可撤销：文案由读模型给，载荷 = 合同号）；<paramref name="paidText"/>/<paramref name="unpaidText"/>
        /// 由调用方按**同源** obligations 汇总后传入（Posted 无应付账 ⇒ 传 0.00，如实而非"—"：
        /// 这份合同确实一分钱都还没产生，不是"不适用"）。</summary>
        internal static EconomyRow Compose(string contractId, string holderLabel, string workLabel,
            int completedJobs, int maxJobs, long paidMinor, long unpaidMinor, long wageMinor,
            string paidText, string unpaidText, string text, string detail, string search, string revokeLabel)
        {
            string doneText = completedJobs.ToString(CultureInfo.InvariantCulture);
            string totalText = maxJobs.ToString(CultureInfo.InvariantCulture);
            // 完成度进度：列化行不消费 Progress（行高与绘制都由 Cells 分支接管），保持与既有 Active 行同式。
            float progress = maxJobs > 0
                ? (completedJobs >= maxJobs ? 1f : (float)completedJobs / maxJobs) : -1f;
            var cells = new string[ColumnCount];
            cells[0] = holderLabel; cells[1] = workLabel; cells[2] = doneText; cells[3] = totalText;
            cells[4] = paidText; cells[5] = unpaidText;
            cells[6] = (wageMinor / 100m).ToString("0.00", CultureInfo.InvariantCulture);
            cells[ActionColumnIndex] = EmptyCell;
            var sorts = new long[ColumnCount];
            sorts[0] = 0L; sorts[1] = 0L; sorts[2] = completedJobs; sorts[3] = maxJobs;
            sorts[4] = paidMinor; sorts[5] = unpaidMinor; sorts[6] = wageMinor; sorts[7] = 0L;
            return new EconomyRow(text, key: contractId, progress: progress, cells: cells, cellSorts: sorts,
                search: search, detail: detail,
                action: revokeLabel, verb: revokeLabel == null ? null : RevokeVerb,
                revoke: revokeLabel == null ? null : contractId);
        }
    }
}

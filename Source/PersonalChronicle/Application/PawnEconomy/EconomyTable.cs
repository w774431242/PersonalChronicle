using System.Collections.Generic;

namespace PersonalChronicle.Application.PawnEconomy
{
    /// <summary>排序态（**三态循环**：无 → 升 → 降 → 无）。
    /// 语义与 RUIF 的 `SortState` 对齐，但**本层零引用**（不依赖任何 UI 框架，可离线断言）
    /// —— 呈现层负责显式映射（`Window_InstitutionWorkspace.ToRuifSort`）。</summary>
    public enum EconomySortOrder { None = 0, Asc = 1, Desc = 2 }

    /// <summary>列的**格语义**（CHG-20261012-ECON-CONTRACTIZE · B1）。这是列声明的一部分：呈现层据此
    /// 决定"这一格画文本还是画控件"——**不认列键、不认列位置**（LAND-010：结构经契约声明，UI 不特判）。
    /// 控件本身仍由**行载荷**决定（如 <c>EconomyRow.Revoke</c> 为空 ⇒ 该行不出现控件）。</summary>
    public enum EconomyCellControl
    {
        /// <summary>文本格（默认）：按 <c>EconomyRow.Cells</c> 绘制文本。</summary>
        None = 0,
        /// <summary>控件格：该列由行载荷放一个按钮（文案取 <c>EconomyRow.Action</c>、色调取 <c>Verb</c>）；
        /// 对应格子的 <c>Cells</c> 值恒为空串（不画文本，避免"文字被按钮压住"）。</summary>
        Action = 1
    }

    /// <summary>表格的一列（由读模型声明 ⇒ 呈现层零列硬编码，LAND-010）。
    /// **零 UnityEngine 引用**：本文件可离线断言（AI-001 约束）。</summary>
    public sealed class EconomyTableColumn
    {
        /// <summary>稳定列键（排序/筛选状态按它持久；**不得用显示文本**，GOV-009）。</summary>
        public readonly string Key;
        /// <summary>表头文本（已本地化）。</summary>
        public readonly string Label;
        /// <summary>宽（快照期预量；呈现层按此排布，不做二次测量）。</summary>
        public readonly float Width;
        /// <summary>可读下限（快照期按语言预量）；0 保持既有等比压缩。空间不足时宿主滚动，不再压缩此下限。</summary>
        public readonly float MinWidth;
        /// <summary>数字列 ⇒ 右对齐 + 按数值排序。</summary>
        public readonly bool Numeric;
        public readonly bool Sortable;
        /// <summary>格语义（见 <see cref="EconomyCellControl"/>）；默认文本格 ⇒ 既有列声明零改动。</summary>
        public readonly EconomyCellControl Control;
        public EconomyTableColumn(string key, string label, float width, bool numeric = false, bool sortable = true,
            EconomyCellControl control = EconomyCellControl.None, float minWidth = 0f)
        {
            Key = key; Label = label; MinWidth = System.Math.Max(0f, minWidth);
            Width = System.Math.Max(width, MinWidth); Numeric = numeric; Sortable = sortable; Control = control;
        }
    }

    /// <summary>
    /// 表格声明（列集 + 可筛选面 + 可搜索位）。行本身仍在 <see cref="EconomyView.Rows"/> / 承载区段的行集里
    /// （列化行为 <see cref="EconomyRow.Cells"/> 非空的行）—— 这样行族继续负责测量/滚动/分页/空态，
    /// 表格只负责"表头 + 列的排布与排序/筛选/搜索"，**不产生第二套行族实现**（AI-001 裁决）。
    /// </summary>
    public sealed class EconomyTable
    {
        public readonly int SortColumnDefault = -1;
        public readonly IReadOnlyList<EconomyTableColumn> Columns;
        /// <summary>可筛选面：`{ 键, 已本地化标签 }`（空集 ⇒ 不显示筛选条）。</summary>
        public readonly IReadOnlyList<string> FilterKeys;
        public readonly IReadOnlyList<string> FilterLabels;
        /// <summary>可自由搜索（承接人 / 合同号等）⇒ 显示搜索框；命中按 <see cref="EconomyRow.Search"/>。</summary>
        public readonly bool Searchable;
        public EconomyTable(List<EconomyTableColumn> columns, List<string> filterKeys = null, List<string> filterLabels = null,
            int sortColumnDefault = -1, bool searchable = false)
        {
            Columns = columns; SortColumnDefault = sortColumnDefault; Searchable = searchable;
            FilterKeys = filterKeys ?? (IReadOnlyList<string>)System.Array.Empty<string>();
            var labels = filterLabels ?? new List<string>();
            // 防御（GATE-UI P3-⑦）：标签少于键时补空串 —— 绘制期按 `FilterKeys.Count + 1` 取标签，
            // 缺项会越界；这里把"两列等长"的契约在构造点收紧（唯一入口，调用方零负担）。
            for (int i = labels.Count; i < FilterKeys.Count; i++) labels.Add(string.Empty);
            FilterLabels = labels;
        }
    }

    /// <summary>表格**会话态**（排序 / 筛选 / 搜索 / 列宽）—— 由宿主窗口持有，**不入存档**（与滚动、焦点一致）。
    /// 多表承载时**每表一份**，并按表的**稳定键**寻址（禁止静态共享、禁止互串）。</summary>
    public sealed class EconomyTableState
    {
        /// <summary>排序列下标（-1 = 读模型给定顺序）。</summary>
        public int SortColumn = -1;
        public bool SortDescending;
        /// <summary>当前筛选键（null/"" = 全部）。</summary>
        public string FilterKey;
        /// <summary>搜索词（**防抖后的 query**；null/"" = 不过滤）。</summary>
        public string Search;
        /// <summary>列宽覆盖（像素；<c>&lt;= 0</c> = 未覆盖 ⇒ 按声明宽等比缩放）。惰性分配；列数变化时重建。</summary>
        public float[] ColumnWidths;

        public void Reset(int sortColumnDefault)
        {
            SortColumn = sortColumnDefault; SortDescending = false; FilterKey = null; Search = null; ColumnWidths = null;
        }

        /// <summary>**三态推进**（同列：无 → 升 → 降 → 无；换列：该列升序）。
        /// 与 `RUIF.TableFmt.NextSort` 同口径（呈现层据此画 ▲▼）。</summary>
        public void NextSort(int column)
        {
            if (SortColumn != column) { SortColumn = column; SortDescending = false; return; }
            if (!SortDescending) { SortDescending = true; return; }
            SortColumn = -1; SortDescending = false;
        }

        /// <summary>该列当前排序态（呈现层据此取箭头/色调）。</summary>
        public EconomySortOrder OrderOf(int column)
            => SortColumn != column ? EconomySortOrder.None : (SortDescending ? EconomySortOrder.Desc : EconomySortOrder.Asc);

        /// <summary>取列宽覆盖缓冲（长度与列数不符时重建为全 -1）。拖拽写入点唯一。</summary>
        public float[] WidthsFor(int columns)
        {
            if (columns <= 0) return null;
            if (ColumnWidths != null && ColumnWidths.Length == columns) return ColumnWidths;
            var fresh = new float[columns];
            for (int i = 0; i < columns; i++) fresh[i] = -1f;
            ColumnWidths = fresh;
            return fresh;
        }
    }

    /// <summary>表格的**纯函数**操作（排序 / 筛选 / 搜索）—— 全部在 **60 tick 快照期**调用一次，
    /// 绘制路径只读结果（PERF：帧内零分配、零 LINQ）。可离线断言。</summary>
    public static class EconomyTableOps
    {
        /// <summary>按列排序：数字列用 <see cref="EconomyRow.CellSorts"/> 的**数值**（缺失则退化为文本序），
        /// 文本列按序号比较；**稳定**（同值保持原序 —— 插入排序，行数有界 ≤ 单页 100）—— 返回新列表，不改原集合。
        ///
        /// <para><b>作用域 = 连续列化段</b>（GATE-ARCH P2-①）：只有 `Cells != null` 的**相邻连续段**参与重排，
        /// 段与段之间不互相填槽（否则第二个列化分区会被排到第一个分区下面）；说明 / 分区行（Cells == null）
        /// 留在原位 —— 分区标题被排进数据中间会让表格失去语境（"这一行属于哪一区"读不出来）。</para></summary>
        public static List<EconomyRow> Sort(IReadOnlyList<EconomyRow> rows, int column, bool descending)
        {
            var result = new List<EconomyRow>(rows.Count);
            for (int i = 0; i < rows.Count; i++) result.Add(rows[i]);
            if (column < 0) return result;
            int index = 0;
            while (index < result.Count)
            {
                while (index < result.Count && result[index]?.Cells == null) index++;   // 跳过骨架行
                int start = index;
                while (index < result.Count && result[index]?.Cells != null) index++;   // 收集连续列化段
                int count = index - start;
                if (count > 1) SortSegment(result, start, count, column, descending);
            }
            return result;
        }

        /// <summary>段内**稳定**插入排序（原地回填；零额外分配、零比较器闭包）。</summary>
        private static void SortSegment(List<EconomyRow> rows, int start, int count, int column, bool descending)
        {
            for (int i = 1; i < count; i++)
            {
                var row = rows[start + i];
                int j = i - 1;
                while (j >= 0 && Precedes(row, rows[start + j], column, descending))
                { rows[start + j + 1] = rows[start + j]; j--; }
                rows[start + j + 1] = row;
            }
        }

        private static bool Precedes(EconomyRow a, EconomyRow b, int column, bool descending)
        {
            int order = Compare(a, b, column);
            return descending ? order > 0 : order < 0;
        }

        private static int Compare(EconomyRow a, EconomyRow b, int column)
        {
            long left = CellSort(a, column), right = CellSort(b, column);
            int order = left.CompareTo(right);
            if (order != 0) return order;
            string ls = CellText(a, column), rs = CellText(b, column);
            return string.CompareOrdinal(ls ?? string.Empty, rs ?? string.Empty);
        }

        private static long CellSort(EconomyRow row, int column)
        {
            if (row?.CellSorts != null && column < row.CellSorts.Length) return row.CellSorts[column];
            return 0L;
        }

        private static string CellText(EconomyRow row, int column)
        {
            if (row?.Cells != null && column < row.Cells.Length) return row.Cells[column];
            return null;
        }

        /// <summary>按 **Tag** 筛选（null/"" = 全部）：行集不变时只丢弃不匹配项，保持原序。</summary>
        public static List<EconomyRow> Filter(IReadOnlyList<EconomyRow> rows, string filterKey)
        {
            var result = new List<EconomyRow>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                // 说明行 / 分区行**始终保留**：它们是分区的骨架，筛掉会让剩余行失去语境。
                if (string.IsNullOrEmpty(filterKey) || row.Tag == null) { result.Add(row); continue; }
                if (string.Equals(row.Tag, filterKey, System.StringComparison.Ordinal)) result.Add(row);
            }
            return result;
        }

        /// <summary>自由搜索（按 <see cref="EconomyRow.Search"/> 键做**大小写不敏感子串**匹配，序数比较）。
        /// 与 <see cref="Filter"/> 的差别：搜索是"找行"而不是"筛分区" ⇒ **无搜索键的骨架行被丢弃**
        /// （保留它们会让结果里混进与关键词无关的分区标题）。空搜索 = 原样返回新列表。</summary>
        public static List<EconomyRow> Search(IReadOnlyList<EconomyRow> rows, string query)
        {
            var result = new List<EconomyRow>(rows.Count);
            bool active = !string.IsNullOrEmpty(query);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                if (!active) { result.Add(row); continue; }
                if (row.Search != null
                    && row.Search.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0) result.Add(row);
            }
            return result;
        }

        /// <summary>筛选 + 搜索 + 排序一次完成（快照期唯一入口）。</summary>
        public static List<EconomyRow> Apply(IReadOnlyList<EconomyRow> rows, EconomyTableState state)
        {
            if (state == null) return Filter(rows, null);
            return Sort(Search(Filter(rows, state.FilterKey), state.Search), state.SortColumn, state.SortDescending);
        }
    }

    /// <summary>
    /// 列宽的**唯一布局函数**（GATE-UI P1：表头 / 数据行 / 拖拽必须读同一份列边界，
    /// 否则三者各算一次必然错位）。零 UnityEngine、零分配（写入调用方缓冲），可离线断言。
    ///
    /// <para>语义：未覆盖列（<c>overrides[i] &lt;= 0</c>）在可读下限以上的弹性部分等比缩小（只缩不放）；
    /// 下限0的旧表逐像素保持S2a行为。用户覆盖列保留覆盖宽但不低于声明下限。
    /// 下限总和超出可用宽时返回实际所需宽，宿主提供横向滚动；最后一列不作特判。</para>
    /// </summary>
    public static class EconomyTableLayout
    {
        /// <summary>声明宽/可读下限总和；供宿主预算窗口与横向滚动，无列数或表类型特判。</summary>
        public static float TotalWidth(IReadOnlyList<EconomyTableColumn> columns, bool minimum = false)
        {
            float total = 0f;
            for (int i = 0; i < (columns?.Count ?? 0); i++) total += minimum ? columns[i].MinWidth : columns[i].Width;
            return total;
        }

        /// <summary>拖拽列下界与其他列最低空间来自同一声明，旧表继续用通用拖拽下界。</summary>
        public static float ResizeMaximum(float available, IReadOnlyList<EconomyTableColumn> columns, int column, float fallbackMinimum)
        {
            float remaining = 0f;
            for (int i = 0; i < columns.Count; i++)
                if (i != column) remaining += System.Math.Max(fallbackMinimum, columns[i].MinWidth);
            return System.Math.Max(System.Math.Max(fallbackMinimum, columns[column].MinWidth), available - remaining);
        }

        /// <summary>详情仅显示一行摘要；全文仍由行 Detail 给悬停。快照期测量、按文本元素截断，避免切开姓名中的组合字符。</summary>
        public static string DetailSummary(string detail, float width, System.Func<string, float> measure)
        {
            if (string.IsNullOrEmpty(detail) || width <= 0f) return string.Empty;
            string line = detail.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", " · ");
            if (measure(line) <= width) return line;
            const string ellipsis = "…";
            if (measure(ellipsis) > width) return string.Empty;
            int[] starts = System.Globalization.StringInfo.ParseCombiningCharacters(line);
            int low = 0, high = starts.Length;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                int end = middle < starts.Length ? starts[middle] : line.Length;
                if (measure(line.Substring(0, end) + ellipsis) <= width) low = middle;
                else high = middle - 1;
            }
            int length = low < starts.Length ? starts[low] : line.Length;
            return line.Substring(0, length) + ellipsis;
        }

        /// <summary>把每列像素宽写入 <paramref name="widths"/>（长度须 ≥ 列数；长度不足时不做任何写入）。</summary>
        public static void Compute(float available, IReadOnlyList<EconomyTableColumn> columns, float[] overrides, float[] widths)
        {
            int n = columns?.Count ?? 0;
            if (n == 0 || widths == null || widths.Length < n) return;
            float fixedTotal = 0f, restMinimum = 0f, restFlexible = 0f;
            for (int i = 0; i < n; i++)
            {
                float over = overrides != null && i < overrides.Length ? overrides[i] : -1f;
                if (over > 0f) { widths[i] = System.Math.Max(over, columns[i].MinWidth); fixedTotal += widths[i]; }
                else
                {
                    widths[i] = -1f; restMinimum += columns[i].MinWidth;
                    restFlexible += columns[i].Width - columns[i].MinWidth;
                }
            }
            float restAvail = available - fixedTotal - restMinimum;
            if (restAvail < 0f) restAvail = 0f;
            float scale = restFlexible > 0f ? restAvail / restFlexible : 0f;
            if (scale > 1f) scale = 1f;
            for (int i = 0; i < n; i++) if (widths[i] < 0f)
                widths[i] = columns[i].MinWidth + (columns[i].Width - columns[i].MinWidth) * scale;
        }
    }
}

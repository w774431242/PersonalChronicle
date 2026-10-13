using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Economy.PawnEconomy;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// CHG-20261011-WORKCAT（S2a + S2b）：数据表格**排序 / 筛选 / 搜索 / 列宽布局纯函数**的离线断言。
    ///
    /// <para>锁定七件事（都是"看起来对、实际错"的经典坑）：
    /// ① **数字列必须按数值排**（`"10" &lt; "9"` 是字符串序 —— 用 `CellSorts` 而非 `Cells` 比较）；
    /// ② **说明/分区行在任何筛选下都保留**（否则筛完只剩数据行，玩家失去分区语境）；
    /// ③ 排序/筛选**不改原集合**（快照不可变，DL-6），返回新列表；
    /// ④ S2b：排序**稳定**且**只在连续列化段内**回填（两段列化行不得跨段填槽 —— GATE-ARCH P2-①）；
    /// ⑤ S2b：搜索大小写不敏感，且**丢弃骨架行**（搜索是"找行"不是"筛分区"）；
    /// ⑥ S2b：排序三态循环（无 → 升 → 降 → 无）；
    /// ⑦ S2b：列宽布局**只缩不放**、被拖拽列固定、其余在剩余宽里按声明宽比例分配。</para>
    /// </summary>
    [TestFixture]
    public class EconomyTableOpsTests
    {
        private static EconomyRow Cell(string text, long sort, string tag = null)
            => new EconomyRow(text, cells: new[] { text }, cellSorts: new[] { sort }, tag: tag);

        private static List<EconomyRow> Sample()
            => new List<EconomyRow>
            {
                new EconomyRow("分区：原版命令"),              // 说明行（Tag = null）
                Cell("9", 9, "wired"),
                Cell("10", 10, "unwired"),
                Cell("100", 100, "wired")
            };

        [Test] public void S2a_SortUsesNumericPayloadNotText()
        {
            var sorted = EconomyTableOps.Sort(Sample(), 0, false);
            Assert.That(sorted[0].Text, Is.EqualTo("分区：原版命令"), "分区行**留在原位**（排序不得把它排进数据中间）");
            Assert.That(sorted[1].Cells[0], Is.EqualTo("9"));
            Assert.That(sorted[2].Cells[0], Is.EqualTo("10"));
            Assert.That(sorted[3].Cells[0], Is.EqualTo("100"), "若按文本排会是 10 < 100 < 9（字符串序）");
        }

        [Test] public void S2a_SortDescendingAndColumnMinusOne()
        {
            var desc = EconomyTableOps.Sort(Sample(), 0, true);
            Assert.That(desc[0].Text, Is.EqualTo("分区：原版命令"), "分区行位置不动");
            Assert.That(desc[1].Cells[0], Is.EqualTo("100"));
            Assert.That(desc[3].Cells[0], Is.EqualTo("9"));
            var untouched = EconomyTableOps.Sort(Sample(), -1, true);
            Assert.That(untouched[1].Cells[0], Is.EqualTo("9"), "列 -1 = 读模型原序");
        }

        [Test] public void S2a_FilterKeepsSectionRowsAndMatchesTag()
        {
            var wired = EconomyTableOps.Filter(Sample(), "wired");
            Assert.That(wired.Count, Is.EqualTo(3), "2 个 wired 行 + 1 个分区行");
            Assert.That(wired[0].Text, Is.EqualTo("分区：原版命令"), "分区行必须保留（否则剩余行失去语境）");
            Assert.That(EconomyTableOps.Filter(Sample(), null).Count, Is.EqualTo(4), "null = 全部");
            Assert.That(EconomyTableOps.Filter(Sample(), "unwired").Count, Is.EqualTo(2), "1 个 unwired 行 + 分区行");
        }

        [Test] public void S2a_ApplyDoesNotMutateInput()
        {
            var rows = Sample();
            var state = new EconomyTableState { SortColumn = 0, SortDescending = true, FilterKey = "wired" };
            var result = EconomyTableOps.Apply(rows, state);
            Assert.That(rows.Count, Is.EqualTo(4), "原集合不得被改动（快照不可变）");
            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result[1].Cells[0], Is.EqualTo("100"), "筛选后降序：100 → 9");
        }

        // ── S2b（CHG-20261011-WORKCAT）：三态排序 / 稳定 / 分段 / 搜索 / 列宽布局 ──────────

        [Test] public void S2b_NextSortCyclesThreeStates()
        {
            var state = new EconomyTableState();
            Assert.That(state.OrderOf(0), Is.EqualTo(EconomySortOrder.None));
            state.NextSort(0);
            Assert.That(state.OrderOf(0), Is.EqualTo(EconomySortOrder.Asc));
            Assert.That(state.SortDescending, Is.False);
            state.NextSort(0);
            Assert.That(state.OrderOf(0), Is.EqualTo(EconomySortOrder.Desc));
            Assert.That(state.SortDescending, Is.True);
            state.NextSort(0);
            Assert.That(state.OrderOf(0), Is.EqualTo(EconomySortOrder.None), "第三次点击回到读模型原序（取消排序）");
            Assert.That(state.SortColumn, Is.EqualTo(-1));
            state.NextSort(1);
            Assert.That(state.OrderOf(1), Is.EqualTo(EconomySortOrder.Asc), "换列 = 该列升序");
            Assert.That(state.OrderOf(0), Is.EqualTo(EconomySortOrder.None));
        }

        [Test] public void S2b_SortIsStableWithinSegment()
        {
            var rows = new List<EconomyRow>
            {
                new EconomyRow("a", cells: new[] { "同" }, cellSorts: new[] { 5L }),
                new EconomyRow("b", cells: new[] { "同" }, cellSorts: new[] { 5L }),
                new EconomyRow("c", cells: new[] { "同" }, cellSorts: new[] { 5L })
            };
            var sorted = EconomyTableOps.Sort(rows, 0, false);
            Assert.That(sorted[0].Text, Is.EqualTo("a"), "同值保持原序（稳定：插入排序而非 List.Sort）");
            Assert.That(sorted[1].Text, Is.EqualTo("b"));
            Assert.That(sorted[2].Text, Is.EqualTo("c"));
        }

        [Test] public void S2b_SortDoesNotCrossSegments()
        {
            var rows = new List<EconomyRow>
            {
                Cell("9", 9),
                Cell("1", 1),
                new EconomyRow("分区：合同"),   // 骨架行（Cells == null）
                Cell("8", 8),
                Cell("2", 2)
            };
            var sorted = EconomyTableOps.Sort(rows, 0, false);
            Assert.That(sorted[0].Cells[0], Is.EqualTo("1"), "第一段升序");
            Assert.That(sorted[1].Cells[0], Is.EqualTo("9"));
            Assert.That(sorted[2].Text, Is.EqualTo("分区：合同"), "骨架行原位");
            Assert.That(sorted[3].Cells[0], Is.EqualTo("2"), "第二段独立升序（8/2 不得跳到第一段 —— GATE-ARCH P2-①）");
            Assert.That(sorted[4].Cells[0], Is.EqualTo("8"));
        }

        [Test] public void S2b_SearchMatchesKeyCaseInsensitivelyAndDropsSkeleton()
        {
            var rows = new List<EconomyRow>
            {
                new EconomyRow("分区标题"),                                        // 骨架行（Search == null）
                new EconomyRow("Alice 的合同", search: "Alice C-100"),
                new EconomyRow("Bob 的合同", search: "Bob C-200")
            };
            var hit = EconomyTableOps.Search(rows, "c-1");
            Assert.That(hit.Count, Is.EqualTo(1), "只留命中行；骨架行在搜索下被丢弃（它不是结果）");
            Assert.That(hit[0].Text, Is.EqualTo("Alice 的合同"));
            Assert.That(EconomyTableOps.Search(rows, "ALICE").Count, Is.EqualTo(1), "大小写不敏感");
            Assert.That(EconomyTableOps.Search(rows, null).Count, Is.EqualTo(3), "空搜索 = 全量（含骨架行）");
            Assert.That(EconomyTableOps.Search(rows, "无此词").Count, Is.EqualTo(0));
        }

        [Test] public void S2b_ApplyCombinesFilterSearchAndSort()
        {
            var rows = new List<EconomyRow>
            {
                new EconomyRow("分区：原版命令"),
                new EconomyRow("a", cells: new[] { "9" }, cellSorts: new[] { 9L }, tag: "wired", search: "alpha"),
                new EconomyRow("b", cells: new[] { "10" }, cellSorts: new[] { 10L }, tag: "wired", search: "beta"),
                new EconomyRow("c", cells: new[] { "100" }, cellSorts: new[] { 100L }, tag: "unwired", search: "alpha")
            };
            var state = new EconomyTableState { SortColumn = 0, SortDescending = true, FilterKey = "wired", Search = "alpha" };
            var result = EconomyTableOps.Apply(rows, state);
            Assert.That(result.Count, Is.EqualTo(1), "wired ∩ alpha = 仅 a 行");
            Assert.That(result[0].Cells[0], Is.EqualTo("9"));
        }

        [Test] public void S2b_ColumnLayoutScalesAndHonorsOverrides()
        {
            var columns = new List<EconomyTableColumn>
            {
                new EconomyTableColumn("a", "A", 100f),
                new EconomyTableColumn("b", "B", 100f)
            };
            var widths = new float[2];
            EconomyTableLayout.Compute(300f, columns, null, widths);
            Assert.That(widths[0], Is.EqualTo(100f).Within(0.01f), "可用宽富余时**只缩不放**（与 S2a 的 TableScale 同语义）");
            Assert.That(widths[1], Is.EqualTo(100f).Within(0.01f));
            EconomyTableLayout.Compute(150f, columns, null, widths);
            Assert.That(widths[0], Is.EqualTo(75f).Within(0.01f), "无覆盖 ⇒ 等比缩放（逐像素等价旧行为）");
            EconomyTableLayout.Compute(150f, columns, new[] { 120f, -1f }, widths);
            Assert.That(widths[0], Is.EqualTo(120f).Within(0.01f), "被拖拽的列用固定覆盖宽");
            Assert.That(widths[1], Is.EqualTo(30f).Within(0.01f), "其余列在剩余宽里按声明宽比例分配");
            EconomyTableLayout.Compute(100f, columns, new[] { 120f, -1f }, widths);
            Assert.That(widths[1], Is.EqualTo(0f).Within(0.01f), "覆盖总和超过可用 ⇒ 剩余 0（不为负、不溢出）");
        }

        [Test] public void S2b_WidthsForRebuildsWhenColumnCountChanges()
        {
            var state = new EconomyTableState();
            var two = state.WidthsFor(2);
            Assert.That(two.Length, Is.EqualTo(2));
            Assert.That(two[0], Is.EqualTo(-1f), "未覆盖 = -1（参与等比缩放）");
            two[0] = 120f;
            Assert.That(state.WidthsFor(2)[0], Is.EqualTo(120f), "同列数 ⇒ 同一缓冲（保留用户拖拽）");
            var three = state.WidthsFor(3);
            Assert.That(three.Length, Is.EqualTo(3), "列数变化 ⇒ 重建（旧拖拽不再适用）");
            Assert.That(three[0], Is.EqualTo(-1f));
        }

        private static List<EconomyTableColumn> ContractColumns(float actionMinimum = 110f)
            => new List<EconomyTableColumn>
            {
                new EconomyTableColumn("pawn", "Contractor", 144f, minWidth: 120f),
                new EconomyTableColumn("work", "Work", 134f, minWidth: 110f),
                new EconomyTableColumn("done", "Completed", 104f, true, minWidth: 80f),
                new EconomyTableColumn("total", "Total", 104f, true, minWidth: 80f),
                new EconomyTableColumn("paid", "Paid", 114f, true, minWidth: 90f),
                new EconomyTableColumn("unpaid", "Unpaid", 114f, true, minWidth: 90f),
                new EconomyTableColumn("wage", "Per item", 114f, true, minWidth: 90f),
                new EconomyTableColumn("action", "Actions", actionMinimum + 24f, false, false,
                    EconomyCellControl.Action, actionMinimum)
            };

        [TestCase(630f)] [TestCase(900f)] [TestCase(1600f)]
        public void ContractColumnsRemainReadableAndControlFitsAtNarrowAndWideWidths(float available)
        {
            var columns = ContractColumns();
            var widths = new float[8];
            EconomyTableLayout.Compute(available, columns, null, widths);
            float total = 0f;
            for (int i = 0; i < columns.Count; i++)
            {
                Assert.That(widths[i], Is.GreaterThanOrEqualTo(columns[i].MinWidth));
                Assert.That(widths[i], Is.LessThanOrEqualTo(columns[i].Width));
                total += widths[i];
            }
            Assert.That(widths[7] - 8f, Is.GreaterThanOrEqualTo(96f), "控件格扣格间距后仍容纳96px按钮，详情槽独立");
            if (available < EconomyTableLayout.TotalWidth(columns, true))
                Assert.That(total, Is.GreaterThan(available), "小屏应交给共同横滚，不能压缩最小宽或丢末列");
            else Assert.That(total, Is.LessThanOrEqualTo(available + 0.01f));
        }

        [Test] public void ContractResizeCannotStealOtherColumnsMinimumsOrShrinkLongLocalizedAction()
        {
            var columns = ContractColumns(180f); // 当前语言的长撤销文案/表头经快照预量后的下限
            var widths = new float[8];
            EconomyTableLayout.Compute(630f, columns, new[] { 300f, -1f, -1f, -1f, -1f, -1f, -1f, 40f }, widths);
            Assert.That(widths[0], Is.EqualTo(300f));
            Assert.That(widths[7], Is.EqualTo(180f), "旧列覆盖也要被新语言控件下限纠正");
            Assert.That(widths[1], Is.EqualTo(110f), "被其他列挤压仍保最小可读宽");
            Assert.That(EconomyTableLayout.ResizeMaximum(920f, columns, 0, 40f), Is.EqualTo(200f));
        }

        [Test] public void WorkspaceInitialWidthComesFromAllDeclaredTablesAndFitsSmallScreen()
        {
            var small = new EconomyTable(new List<EconomyTableColumn> { new EconomyTableColumn("a", "A", 80f) });
            var wide = new EconomyTable(ContractColumns(240f));
            var tables = new List<WorkspaceTableSection>
            {
                new WorkspaceTableSection("unrelated-key", "First", small, new List<EconomyRow>()),
                new WorkspaceTableSection("another-key", "Second", wide, new List<EconomyRow>())
            };
            float preferred = EconomyWindowLayout.PreferredWorkspaceWidth(tables, 132f);
            Assert.That(preferred, Is.EqualTo(EconomyTableLayout.TotalWidth(wide.Columns) + 132f));
            tables.Reverse();
            Assert.That(EconomyWindowLayout.PreferredWorkspaceWidth(tables, 132f), Is.EqualTo(preferred),
                "不能依赖表位置/页签编号；首次打开亦预算未选中的合同表");
            Assert.That(EconomyWindowLayout.FitWorkspaceWidth(700f, preferred), Is.EqualTo(620f), "小屏不强撑760px越界");
            Assert.That(EconomyWindowLayout.FitWorkspaceWidth(1920f, preferred), Is.EqualTo(preferred));
        }

        [Test] public void ExpandedDetailsUseOneCachedLineWithoutLosingSourceForTooltip()
        {
            const string full = "Contract #42\r\nSigned today\nExpires tomorrow\nWindow 23-06\nPosition abc";
            string shortLine = EconomyTableLayout.DetailSummary(full, 28f, s => s.Length);
            Assert.That(shortLine.Length, Is.LessThanOrEqualTo(28));
            Assert.That(shortLine.EndsWith("…"), Is.True);
            Assert.That(shortLine.Contains("\n"), Is.False);
            Assert.That(EconomyTableLayout.DetailSummary(full, 300f, s => s.Length), Does.Contain("Window 23-06"));
            Assert.That(full, Does.Contain("\nPosition abc"), "摘要不覆盖行原始详情，悬停仍有全五项");
            Assert.That(EconomyTableLayout.DetailSummary("A\u0301BC", 3f, s => s.Length), Is.EqualTo("A\u0301…"),
                "截断不能切开组合字符");
            Assert.That(EconomyWindowLayout.TableRowHeight(22f, 30f, 8f), Is.EqualTo(46f),
                "紧凑行的30px按钮必须留在卡片双8px内边距之内");
        }
    }
}

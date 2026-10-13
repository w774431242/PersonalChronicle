using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive.UI
{
    /// <summary>
    /// 列表/表格组件组：<see cref="TableHeader"/> / <see cref="ZebraRow"/> / <see cref="EmptyState"/>。
    /// 从车间管理面板（Automation）的两处重复实现收敛而来（提取组件重构，2026-09-04）。
    ///
    /// 组件层契约：只用 <see cref="UITheme"/> 令牌，绘制后恢复
    /// <c>GUI.color</c> / <c>Text.Font</c> / <c>Text.Anchor</c>。
    /// </summary>
    internal static partial class UIComponents
    {
        /// <summary>表头列宽复用缓冲（IMGUI 单线程顺序绘制，无嵌套调用 ⇒ 安全）。
        /// 长度 ≥ 任何单表的列数（16 留余量）。</summary>
        private static readonly float[] headerWidths = new float[16];

        /// <summary>
        /// 表头（兼容重载）：无列样式、无列间距、无排序。<b>所有列名一律左对齐</b>。
        /// <para>保留是为了让尚未接入列样式的表（如第三方扩展）行为**逐字不变**。</para>
        /// </summary>
        internal static void TableHeader(Rect rect, string[] cols, float[] shares)
        {
            var state = RUIF.SortState.None;
            int sortCol = -1;
            TableHeader(rect, cols, shares, null, 0f, ref sortCol, ref state);
        }

        /// <summary>
        /// 表头（<b>列样式 + 列间距</b>）：CHG-20261005-RUIFTABLE。
        ///
        /// <para><b>为什么必须有 <paramref name="gap"/></b>：数据行用
        /// <see cref="RUIF.TableFmt.ColumnSplit"/> 切列时会**扣掉 gap**，而旧表头是
        /// <c>shares[i] × rect.width</c>（不扣）⇒ 两端的列宽**本来就不同**，越靠右误差越大
        /// （这就是"表头与数据错位"的第二个来源）。现在两端都走 <c>ColumnSplit</c>
        /// ⇒ 切分口径唯一。</para>
        ///
        /// <para><paramref name="styles"/> 为 null / 越界列 ⇒ 回退左对齐。</para>
        /// </summary>
        internal static void TableHeader(Rect rect, string[] cols, float[] shares,
            RUIF.StatCol[] styles, float gap)
        {
            var state = RUIF.SortState.None;
            int sortCol = -1;
            TableHeader(rect, cols, shares, styles, gap, ref sortCol, ref state);
        }

        /// <summary>
        /// 表头（<b>列样式 + 列间距 + 三态排序</b>）：CHG-20261005-TABLESORT。
        ///
        /// <para>点击某列 ⇒ 切到该列并置「升序」；再点同列 ⇒ 升 → 降 → 无（循环），
        /// 三态循环复用 <see cref="RUIF.TableFmt.NextSort"/>（与 RUIF
        /// <c>TableCandAtoms.PawnTableFrame</c> 同一套语义）。</para>
        ///
        /// <para>排序指示：当前排序列的列名加 <c>▲/▼</c> 并改用 <see cref="UITheme.Accent"/>
        /// —— 与 RUIF <c>TableAtoms.HeaderCell</c> 的视觉语言一致；但**锚点仍由列样式决定**
        /// （<c>HeaderCell</c> 写死左对齐，直接用会让数值列表头又跑到左边）。</para>
        ///
        /// <para><b>不排序任何数据</b>：本方法只维护"点了哪列、什么方向"，真正的重排由调用方做
        /// （本工程放在 Refresh 期 ⇒ 绘制路径零成本）。</para>
        /// </summary>
        /// <returns>本次被点击的列下标（-1 = 未点击）。</returns>
        /// <remarks>
        /// ⚠ <paramref name="sortCol"/> 必须是 <c>ref</c>（2026-10-05 实机 bug 修复）：
        /// 首次实现写成按值传递，切列时只改了**方法局部副本**，调用方字段永远是 -1
        /// ⇒ 表现为「点表头完全没反应、不显示 ▲▼」。
        /// </remarks>
        internal static int TableHeader(Rect rect, string[] cols, float[] shares,
            RUIF.StatCol[] styles, float gap, ref int sortCol, ref RUIF.SortState sortState)
        {
            int clicked = -1;
            float[] widths = headerWidths;
            RUIF.TableFmt.ColumnSplit(rect, shares, gap, widths);
            float x = rect.x;
            for (int i = 0; i < cols.Length; i++)
            {
                Rect cell = new Rect(x, rect.y, Mathf.Max(0f, widths[i]), rect.height);
                bool sorted = i == sortCol && sortState != RUIF.SortState.None;
                string label = cols[i];
                if (sorted)
                {
                    label = (label ?? string.Empty)
                        + (sortState == RUIF.SortState.Asc ? " ▲" : " ▼");
                }
                Label(cell, label, UITheme.FontLabel,
                    sorted ? UITheme.Accent : UITheme.Muted,
                    RUIF.TableFmt.ColumnAlignOf(styles, i));
                if (Widgets.ButtonInvisible(cell))
                {
                    clicked = i;
                }
                x += widths[i] + gap;
            }
            if (clicked >= 0)
            {
                if (sortCol == clicked)
                {
                    sortState = RUIF.TableFmt.NextSort(sortState);
                }
                else
                {
                    sortCol = clicked;
                    sortState = RUIF.SortState.Asc;
                }
            }
            Rule(new Rect(rect.x, rect.yMax, rect.width, 1f), UITheme.BorderSoft);
            return clicked;
        }

        /// <summary>
        /// 斑马纹行底：奇数行浅色叠加 + 鼠标悬停高亮（微交互，暗示可读行边界）。
        /// 在行内容绘制之前调用。
        /// </summary>
        /// <param name="interactive">
        /// true = 该行可点（画悬停高亮）；false = 纯展示行。
        /// 审计修复（2026-09-30 · P3）：旧实现对**所有**行画悬停 ⇒ 不可点行也被暗示"可点"。
        /// </param>
        internal static void ZebraRow(Rect rect, int index, bool interactive = false)
        {
            if (index % 2 == 1)
            {
                TintedBox(rect, UITheme.OverlayWhite04);
            }
            if (interactive && Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
        }

        /// <summary>
        /// 空状态（有教育意义的三级结构）：accent 装饰短横条 + 主标题 + 副文案，
        /// 整体在给定区域内垂直居中。用于空列表、未开放页签的占位。
        /// </summary>
        internal static void EmptyState(Rect rect, string title, string sub)
        {
            float cy = rect.y + rect.height / 2f;
            RUIF.Draw.Box(
                new Rect(rect.x + rect.width / 2f - 14f, cy - 30f, 28f, 3f), UITheme.Accent);
            Label(new Rect(rect.x, cy - 18f, rect.width, 24f),
                title, UITheme.FontBody, UITheme.SecondaryText, TextAnchor.MiddleCenter);
            Label(new Rect(rect.x, cy + 6f, rect.width, 18f),
                sub, UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleCenter);
        }
    }
}

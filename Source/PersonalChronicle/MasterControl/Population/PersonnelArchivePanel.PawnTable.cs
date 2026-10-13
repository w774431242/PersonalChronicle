using PersonalChronicle.Archive;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// Partial of <see cref="PersonnelArchivePanel"/> ——嵌入档人物 pane 的**12 列明细表 + 分段筛选**
    /// （预览 `#v-archive` 的 ③ 人物明细区，HTML 1086-1132）。
    ///
    /// <para><b>列与真源（预览表头行 1096-1102）</b>：
    /// 档案编号 / 姓名 / 种类 / 角色 / 状态 / 入册日 / 在册天数 / 劳动工时 h / 产出件 / 击杀 / 事件 / 职称
    /// —— 每列的数据来源见 <see cref="BuildEmbeddedPawnRow"/>（预览 1126-1132 的「列来源说明」）。</para>
    ///
    /// <para><b>几何（预览 CSS）</b>：`th{font-size:9px;padding:5px 7px;background:raised}`(254-258) ·
    /// `td{padding:3.5px 7px}`(259) ⇒ 行高 20（Tiny 行高 18 + 上下各 1）；
    /// `.num{text-align:right;tabular-nums}`(272) ⇒ 数值列右对齐。</para>
    ///
    /// <para><b>性能（PERF-001）</b>：行文本按**数据修订号**缓存，仅在快照变化时重建
    /// （每行含 1 次事件查询与 1 次工时聚合 ⇒ 绝不能每帧做）；列宽缓冲按可用宽重建。</para>
    /// </summary>
    public sealed partial class PersonnelArchivePanel
    {
        private const int EmbeddedColumnCount = 12;

        /// <summary>
        /// 数据行高 —— **25**。预览实测 td 高 **25.1**（11px 行高 + `padding:3.5px 7px`×2）；
        /// 实机 Tiny 行高 18 + 7 = **25** ⇒ 与预览逐像素吻合。旧值 20 比预览矮 5px/行。
        /// </summary>
        private const float EmbeddedRowHeight = 25f;

        /// <summary>
        /// 表头行高 —— **24**。预览实测 th 高 **23.9**（9px 行高 + `padding:5px 7px`×2）；
        /// 实机 Tiny 行高 18 + 6 = **24**。旧值 20 比预览矮 4px。
        /// </summary>
        private const float EmbeddedHeaderRowHeight = 24f;

        private const float EmbeddedFilterHeight = 22f;

        /// <summary>
        /// 列宽比例（合计 1.0）—— ⚠ **近似值，不可能 1:1**：预览用 `<table>` 原生
        /// `table-layout:auto`（列宽由**内容**决定，实测 159/172/128/112/129/184/161/190/137/113/113/195），
        /// IMGUI 无 auto 表格 ⇒ 只能用比例近似（已登记 B 类降级）。
        /// </summary>
        private static readonly float[] EmbeddedColumnRatios =
        {
            0.115f, 0.130f, 0.080f, 0.070f, 0.065f, 0.080f,
            0.070f, 0.075f, 0.065f, 0.050f, 0.060f, 0.140f
        };

        /// <summary>数值列右对齐（预览 `.num`）：在册天数 / 工时 / 产出 / 击杀 / 事件。</summary>
        private static readonly bool[] EmbeddedColumnRightAligned =
        {
            false, false, false, false, false, false, true, true, true, true, true, false
        };

        private struct EmbeddedPawnRow
        {
            public string Id;
            public string Name;
            public string Kind;
            public string Role;
            public string Status;
            public string Join;
            public string Days;
            public string Hours;
            public string Produced;
            public string Kills;
            public string Events;
            public string Title;
            /// <summary>
            /// 职称档位 0..4（员/助/中/副高/正高），**-1 = 无职称** —— 预览 `.q.tier0..4`
            /// 的圆角徽章配色用（色值见 `UITheme.TitleTierColor`）。档位 = 该职称在
            /// `QualificationDef`（按 `order` 排序的资格阶梯）中的下标 ——
            /// 与职业档案页「职称链」同源（不另立映射）。
            /// </summary>
            public int TitleTier;
            /// <summary>0 在世 / 1 已死亡 / 2 已归档（配色用；程序逻辑不比对文本，GOV-009）。</summary>
            public byte State;
            /// <summary>整行全文（构建期拼好）——LAND-008：被截断的单元格信息必须可达，提示零绘制期拼接。</summary>
            public string Tooltip;

            // ---- 排序列（数值排序列用真实数值，不用格式化文本：否则 "9" 会大于 "10"）----
            public int DaysValue;
            public float HoursValue;
            public int ProducedValue;
            public int KillsValue;
            public int EventsValue;
        }

        private readonly List<EmbeddedPawnRow> embeddedPawnRows = new List<EmbeddedPawnRow>();
        private long embeddedPawnRowsRevision = -1L;
        private string embeddedPawnRowsFilter;
        private float[] embeddedColumnWidths;
        private Vector2 embeddedTableScroll;
        private Vector2 embeddedPaneScroll;

        private void DrawEmbeddedPawnTable(Rect pane, List<ArchiveObject> objects, IArchiveService service)
        {
            float y = pane.y;
            y = DrawEmbeddedPawnFilterSeg(new Rect(pane.x, y, pane.width, EmbeddedFilterHeight));
            y += 4f;
            y = DrawEmbeddedPawnSearch(new Rect(pane.x, y, pane.width, EmbeddedFilterHeight));
            y += 2f;

            // 【1:1 实测 · 补全方案 P1】表下预留「列来源」说明（预览实测高 **29.1** = 两行 + 间距）。
            const float columnsNoteHeight = 30f;
            Rect table = new Rect(pane.x, y, pane.width, Mathf.Max(0f, pane.yMax - y - columnsNoteHeight));
            if (table.width < 120f || table.height < 40f)
            {
                return;
            }
            EnsureEmbeddedPawnRows(objects, service);
            // 列宽按「内容视口宽」计算（扣除滚动条位）—— 旧实现按 table.width-2 算、
            // 滚动时行宽却是 body.width-16，最后一列与滚动条重叠 ~14px（UI P1 修复）。
            DrawEmbeddedColumnWidths(table.width - 2f - EmbeddedScrollbarWidth);

            Widgets.DrawBoxSolid(table, UITheme.Panel);
            UIComponents.Border(table, UITheme.BorderSoft);
            Rect header = new Rect(table.x + 1f, table.y + 1f, table.width - 2f, EmbeddedHeaderRowHeight);
            DrawEmbeddedTableHeader(header);
            UIComponents.Rule(new Rect(header.x, header.yMax - 1f, header.width, UITheme.RuleHeight),
                UITheme.BorderSoft);

            Rect body = new Rect(table.x + 1f, header.yMax, table.width - 2f,
                Mathf.Max(0f, table.yMax - header.yMax - 1f));
            float contentHeight = embeddedPawnRows.Count * EmbeddedRowHeight;
            Rect viewRect = new Rect(body.x, body.y, Mathf.Max(1f, body.width - EmbeddedScrollbarWidth),
                Mathf.Max(body.height, contentHeight));
            if (contentHeight <= body.height)
            {
                DrawEmbeddedPawnRows(body, body.y, body.yMax);
            }
            else
            {
                Widgets.BeginScrollView(body, ref embeddedTableScroll, viewRect);
                try
                {
                    DrawEmbeddedPawnRows(new Rect(viewRect.x, viewRect.y, viewRect.width, contentHeight),
                        body.y, body.yMax);
                }
                finally
                {
                    Widgets.EndScrollView();
                }
            }

            // 表下「列来源」说明（预览 HTML 1218-1224，实测高 29.1 = 两行）——
            // 实机 `Label` **不折行** ⇒ 按语义拆两条 Keyed，各自单行；截断时整行悬停给全文（LAND-008）。
            DrawEmbeddedColumnsNote(pane, pane.yMax - columnsNoteHeight, columnsNoteHeight);
        }

        /// <summary>
        /// 表下「列来源」说明（预览 1218-1224）：两行 dim 小字，交代每列取自哪个 Domain 字段 ——
        /// 让玩家/维护者能核对「表里的数字是哪来的」，属**诚实性**陈述而非装饰。
        /// </summary>
        private static void DrawEmbeddedColumnsNote(Rect pane, float y, float height)
        {
            if (pane.width < 200f || height < 36f)
            {
                return;
            }
            string[] keys =
            {
                "PersonalChronicle.UI.Archive.Pawn.ColumnsNote1",
                "PersonalChronicle.UI.Archive.Pawn.ColumnsNote2"
            };
            for (int i = 0; i < keys.Length; i++)
            {
                float lineY = y + i * 18f;
                string text = keys[i].Translate().ToString();
                string shown = UIComponents.TruncateToWidth(text, pane.width, GameFont.Tiny);
                UIComponents.Label(new Rect(pane.x, lineY, pane.width, 18f), shown,
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
                if (Mouse.IsOver(pane) && !string.Equals(shown, text, System.StringComparison.Ordinal))
                {
                    TooltipHandler.TipRegion(new Rect(pane.x, lineY, pane.width, 18f), text);
                }
            }
        }

        /// <summary>分段筛选（预览 `.seg`：连体、选中项 20% accent 底、左对齐）。</summary>
        private float DrawEmbeddedPawnFilterSeg(Rect rect)
        {
            string[] keys =
            {
                "PersonalChronicle.UI.PawnFilter.All",
                "PersonalChronicle.UI.PawnFilter.Alive",
                "PersonalChronicle.UI.PawnFilter.Dead",
                "PersonalChronicle.UI.PawnFilter.Archived"
            };
            // 【1:1 实测修正】预览 `.seg button` 是**内容宽**（`padding:3px 12px`；
            //   实测 45.8 / 45.8 / 56.3 / 55.5 × 19.6）。旧实现写死 `segWidth = 76f`
            //   ⇒ 四种文案都比预览宽一截，且中英切换时不随文案变化。
            Text.Font = GameFont.Tiny;
            float[] widths = new float[keys.Length];
            float totalWidth = 2f;
            for (int i = 0; i < keys.Length; i++)
            {
                widths[i] = Text.CalcSize(keys[i].Translate().ToString()).x + 24f;   // 内容 + 左右各 12
                totalWidth += widths[i];
            }
            Rect group = new Rect(rect.x, rect.y, totalWidth, rect.height);
            Widgets.DrawBoxSolid(group, UITheme.Panel);
            UIComponents.Border(group, UITheme.BorderSoft);
            float cellX = group.x + 1f;
            for (int i = 0; i < keys.Length; i++)
            {
                Rect cell = new Rect(cellX, group.y + 1f, widths[i], group.height - 2f);
                cellX += widths[i];
                bool on = overviewPawnFilter == i;
                if (on)
                {
                    Widgets.DrawBoxSolid(cell, UITheme.WithAlpha(UITheme.Accent, 0.20f));
                }
                else if (Mouse.IsOver(cell))
                {
                    Widgets.DrawHighlight(cell);
                }
                UIComponents.Label(cell,
                    UIComponents.TruncateToWidth(keys[i].Translate().ToString(), cell.width - 4f, GameFont.Tiny),
                    GameFont.Tiny, on ? UITheme.Accent : UITheme.Muted, TextAnchor.MiddleCenter);
                if (Widgets.ButtonInvisible(cell))
                {
                    overviewPawnFilter = i;
                    embeddedPawnRowsRevision = -1L;
                }
            }
            return rect.y + rect.height;
        }

        private void DrawEmbeddedTableHeader(Rect rect)
        {
            string[] keys =
            {
                "PersonalChronicle.UI.Archive.Col.Id",
                "PersonalChronicle.UI.MasterControl.Population.Col.Name",
                "PersonalChronicle.UI.Archive.Col.Kind",
                "PersonalChronicle.UI.MasterControl.Population.Col.Role",
                "PersonalChronicle.UI.MasterControl.Population.Col.Status",
                "PersonalChronicle.UI.Archive.Col.JoinDate",
                "PersonalChronicle.UI.MasterControl.Population.Col.Days",
                "PersonalChronicle.UI.MasterControl.Population.Col.Hours",
                "PersonalChronicle.UI.MasterControl.Population.Col.Produced",
                "PersonalChronicle.UI.MasterControl.Population.Col.Kills",
                "PersonalChronicle.UI.MasterControl.Population.Col.Events",
                "PersonalChronicle.UI.MasterControl.Population.Col.Title"
            };
            float x = rect.x;
            for (int i = 0; i < EmbeddedColumnCount; i++)
            {
                Rect cell = new Rect(x, rect.y, embeddedColumnWidths[i], rect.height);
                float width = Mathf.Max(0f, embeddedColumnWidths[i] - 6f);
                bool active = embeddedSortColumn == i;
                if (Mouse.IsOver(cell))
                {
                    Widgets.DrawHighlight(cell);
                }
                string text = UIComponents.TruncateToWidth(keys[i].Translate().ToString(),
                    Mathf.Max(0f, width - 8f), GameFont.Tiny);
                if (active)
                {
                    // 排序标记（符号不进 Keyed，与工程既有约定一致）
                    text = (embeddedSortDescending ? "▼ " : "▲ ") + text;
                }
                UIComponents.Label(new Rect(x + 3f, rect.y, width, rect.height), text,
                    GameFont.Tiny, active ? UITheme.Text : UITheme.Dim, TextAnchor.MiddleLeft);
                // 点击列头 = 排序（同列再点切换升降序）。**排序在构建期做**，这里只置脏位。
                if (Widgets.ButtonInvisible(cell))
                {
                    if (embeddedSortColumn == i)
                    {
                        embeddedSortDescending = !embeddedSortDescending;
                    }
                    else
                    {
                        embeddedSortColumn = i;
                        embeddedSortDescending = true;
                    }
                    embeddedPawnRowsRevision = -1L;
                }
                x += embeddedColumnWidths[i];
            }
        }

        /// <summary>搜索框（预览 1023 的「表内搜索」）：输入即过滤，过滤在构建期做。</summary>
        private float DrawEmbeddedPawnSearch(Rect rect)
        {
            Rect box = new Rect(rect.x, rect.y, Mathf.Min(240f, rect.width), rect.height);
            string typed = Widgets.TextField(box, embeddedPawnSearch);
            if (!string.Equals(typed, embeddedPawnSearch, System.StringComparison.Ordinal))
            {
                embeddedPawnSearch = typed ?? string.Empty;
                embeddedPawnRowsRevision = -1L;
            }
            if (string.IsNullOrEmpty(embeddedPawnSearch))
            {
                UIComponents.Label(new Rect(box.x + 5f, box.y, Mathf.Max(0f, box.width - 10f), box.height),
                    UIComponents.TruncateToWidth(
                        "PersonalChronicle.UI.Archive.Table.SearchHint".Translate().ToString(),
                        Mathf.Max(0f, box.width - 10f), GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            }
            // 命中计数（有搜索词时才显示；如实反映过滤后的行数）
            if (!string.IsNullOrEmpty(embeddedPawnSearch))
            {
                string countText = embeddedPawnRows.Count.ToString();
                UIComponents.Label(new Rect(box.xMax + 6f, box.y, Mathf.Max(0f, rect.xMax - box.xMax - 6f), box.height),
                    countText, GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleLeft);
            }
            return rect.y + rect.height + 4f;
        }

        private void DrawEmbeddedPawnRows(Rect rect, float viewTop, float viewBottom)
        {
            // 可见区窗口化（PERF P1 修复）：只画滚动视口内的行（±1 行缓冲）。
            // 旧实现每帧遍历全部行 × 12 列做 TruncateToWidth（内含 CalcSize 与子串分配），
            // 几百行时白白烧帧。行内容缓存不变，仅跳过不可见行。
            int first = 0;
            int last = embeddedPawnRows.Count - 1;
            if (rect.height > 0f && viewBottom > viewTop)
            {
                first = Mathf.Max(0, Mathf.FloorToInt((viewTop - rect.y) / EmbeddedRowHeight) - 1);
                last = Mathf.Min(last, Mathf.CeilToInt((viewBottom - rect.y) / EmbeddedRowHeight) + 1);
            }
            for (int i = first; i <= last; i++)
            {
                Rect row = new Rect(rect.x, rect.y + i * EmbeddedRowHeight, rect.width, EmbeddedRowHeight);
                if (row.yMax > rect.yMax + 0.5f)
                {
                    break;   // 剩余高度不足：不往 0 高矩形里画（IMGUI 无裁剪）
                }
                EmbeddedPawnRow data = embeddedPawnRows[i];
                if (Mouse.IsOver(row))
                {
                    Widgets.DrawBoxSolid(row, UITheme.WithAlpha(UITheme.Accent, 0.06f));
                    // LAND-008：整行全文提示（构建期拼好），被截断的单元格信息可达。
                    if (!string.IsNullOrEmpty(data.Tooltip))
                    {
                        TooltipHandler.TipRegion(row, data.Tooltip);
                    }
                }
                float x = row.x;
                for (int c = 0; c < EmbeddedColumnCount; c++)
                {
                    float width = embeddedColumnWidths[c] - 6f;
                    Color color = c == 4 ? EmbeddedStateColor(data.State) : UITheme.Text;
                    TextAnchor anchor = EmbeddedColumnRightAligned[c] ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                    string cellText = EmbeddedCellValue(data, c);
                    if (c == 4)
                    {
                        // 预览 `.st`（CSS 289-291）= **6px 语义色点（gap 4）+ 文本**（字色 = 语义色）；
                        // IMGUI 无圆角 ⇒ 方点（B 类降级，与画像同口径）。gap 由 5 修为实测的 4。
                        const float dotSize = 6f;
                        const float dotGap = 4f;
                        Widgets.DrawBoxSolid(new Rect(x + 3f, row.y + (row.height - dotSize) * 0.5f,
                            dotSize, dotSize), color);
                        float textW = Mathf.Max(0f, width - dotSize - dotGap);
                        UIComponents.Label(new Rect(x + 3f + dotSize + dotGap, row.y, textW, row.height),
                            UIComponents.TruncateToWidth(cellText, textW, GameFont.Tiny),
                            GameFont.Tiny, color, TextAnchor.MiddleLeft);
                    }
                    else if (c == 11 && data.TitleTier >= 0)
                    {
                        // 预览 `.q.tierN`（CSS 292）= **描边徽章**（`border:1px solid currentColor` ·
                        // `border-radius:99px` · 字色 = 档位色 · `padding:1px 6px`）。旧实现画纯文本
                        // ⇒ 与预览形态不同。IMGUI 无圆角 ⇒ 直角描边（B 类降级）。
                        // 档位色取 `UITheme.TitleTierColor`（跨主题固定的 5 档品质色阶，与职业档案页同源）。
                        Color tierColor = UITheme.TitleTierColor(data.TitleTier);
                        Text.Font = GameFont.Tiny;
                        float pillW = Mathf.Min(width, Text.CalcSize(cellText).x + 12f);
                        float pillH = Mathf.Min(row.height, 18f);
                        Rect pill = new Rect(x + 3f, row.y + (row.height - pillH) * 0.5f, pillW, pillH);
                        Widgets.DrawBoxSolid(pill, UITheme.WithAlpha(tierColor, 0.14f));
                        UIComponents.Border(pill, tierColor);
                        UIComponents.Label(pill,
                            UIComponents.TruncateToWidth(cellText, Mathf.Max(0f, pillW - 8f), GameFont.Tiny),
                            GameFont.Tiny, tierColor, TextAnchor.MiddleCenter);
                    }
                    else
                    {
                        UIComponents.Label(new Rect(x + 3f, row.y, width, row.height),
                            UIComponents.TruncateToWidth(cellText, width, GameFont.Tiny),
                            GameFont.Tiny, color, anchor);
                    }
                    x += embeddedColumnWidths[c];
                }
                if (Widgets.ButtonInvisible(row) && !string.IsNullOrEmpty(data.Id))
                {
                    RequestPawnDetail(data.Id);
                }
            }
        }

        private static string EmbeddedCellValue(EmbeddedPawnRow row, int column)
        {
            switch (column)
            {
                case 0: return row.Id;
                case 1: return row.Name;
                case 2: return row.Kind;
                case 3: return row.Role;
                case 4: return row.Status;
                case 5: return row.Join;
                case 6: return row.Days;
                case 7: return row.Hours;
                case 8: return row.Produced;
                case 9: return row.Kills;
                case 10: return row.Events;
                default: return row.Title;
            }
        }

        private static Color EmbeddedStateColor(byte state)
        {
            switch (state)
            {
                case 2: return UITheme.Dim;
                case 1: return UITheme.Dead;
                default: return UITheme.Alive;
            }
        }

        private void DrawEmbeddedColumnWidths(float width)
        {
            if (embeddedColumnWidths == null || embeddedColumnWidths.Length < EmbeddedColumnCount)
            {
                embeddedColumnWidths = new float[EmbeddedColumnCount];
            }
            for (int i = 0; i < EmbeddedColumnCount; i++)
            {
                embeddedColumnWidths[i] = width * EmbeddedColumnRatios[i];
            }
        }

        /// <summary>
        /// 按（数据修订 × 筛选档 × 排序 × 搜索词）重建行缓存 —— 行内含事件查询与工时聚合，
        /// 不可每帧做。**排序与搜索都在这里（构建期）完成**，绘制期只按结果顺序画行（PERF-001）。
        /// </summary>
        private void EnsureEmbeddedPawnRows(List<ArchiveObject> objects, IArchiveService service)
        {
            long revision = service != null ? service.GetDataRevision() : 0L;
            string filterKey = overviewPawnFilter + "|" + embeddedSortColumn + "|"
                + (embeddedSortDescending ? "d" : "a") + "|" + embeddedPawnSearch;
            if (embeddedPawnRowsRevision == revision
                && string.Equals(embeddedPawnRowsFilter, filterKey, System.StringComparison.Ordinal))
            {
                return;
            }
            embeddedPawnRowsRevision = revision;
            embeddedPawnRowsFilter = filterKey;
            embeddedPawnRows.Clear();

            List<ArchiveObject> source = overviewPawnFilter > 0
                ? FilterPawnObjects(objects, overviewPawnFilter)
                : objects;
            long nowTicks = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            for (int i = 0; i < source.Count; i++)
            {
                PawnObject pawn = source[i] as PawnObject;
                if (pawn == null)
                {
                    continue;
                }
                embeddedPawnRows.Add(BuildEmbeddedPawnRow(pawn, nowTicks, service));
            }
            ApplyPawnSearchAndSort();
        }

        // ---- 搜索 / 排序（预览 1023 行自注「列排序为预览演示」⇒ 实机真做）----

        private int embeddedSortColumn = -1;
        private bool embeddedSortDescending;
        private string embeddedPawnSearch = string.Empty;

        private void ApplyPawnSearchAndSort()
        {
            // 纯空白 = 不过滤（Trim 后为空却仍进过滤分支 ⇒ 全量命中，看起来像"搜索失效"）
            string needle = embeddedPawnSearch != null ? embeddedPawnSearch.Trim() : string.Empty;
            if (needle.Length > 0)
            {
                for (int i = embeddedPawnRows.Count - 1; i >= 0; i--)
                {
                    if (!MatchesPawnSearch(embeddedPawnRows[i], needle))
                    {
                        embeddedPawnRows.RemoveAt(i);
                    }
                }
            }
            if (embeddedSortColumn >= 0 && embeddedPawnRows.Count > 1)
            {
                embeddedPawnRows.Sort(ComparePawnRows);
            }
        }

        /// <summary>任一列（含 tooltip 之外的全部可见单元格）命中即算匹配，忽略大小写。</summary>
        private static bool MatchesPawnSearch(EmbeddedPawnRow row, string needle)
        {
            for (int c = 0; c < EmbeddedColumnCount; c++)
            {
                string value = EmbeddedCellValue(row, c);
                if (!string.IsNullOrEmpty(value)
                    && value.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private int ComparePawnRows(EmbeddedPawnRow a, EmbeddedPawnRow b)
        {
            float left;
            float right;
            switch (embeddedSortColumn)
            {
                case 6: left = a.DaysValue; right = b.DaysValue; break;
                case 7: left = a.HoursValue; right = b.HoursValue; break;
                case 8: left = a.ProducedValue; right = b.ProducedValue; break;
                case 9: left = a.KillsValue; right = b.KillsValue; break;
                case 10: left = a.EventsValue; right = b.EventsValue; break;
                default:
                    int textOrder = string.CompareOrdinal(
                        EmbeddedCellValue(a, embeddedSortColumn),
                        EmbeddedCellValue(b, embeddedSortColumn));
                    return embeddedSortDescending ? -textOrder : textOrder;
            }
            int order = left.CompareTo(right);
            return embeddedSortDescending ? -order : order;
        }

        /// <summary>
        /// 单行构建（预览 1126-1132 的列来源）：工时走 <c>GetWorkTimeStats</c> 求和 —— 与档案馆
        /// 个人页**同源**（含旧档回算），不得直读 <c>TotalWorkTicks</c>（CHG-001 审计 P1-1 的教训）。
        /// </summary>
        private static EmbeddedPawnRow BuildEmbeddedPawnRow(PawnObject pawn, long nowTicks, IArchiveService service)
        {
            long workTicks = 0L;
            if (service != null)
            {
                IReadOnlyList<WorkTimeStatView> stats = service.GetWorkTimeStats(pawn.StableId);
                if (stats != null)
                {
                    for (int i = 0; i < stats.Count; i++)
                    {
                        workTicks += stats[i].Ticks;
                    }
                }
            }

            int events = 0;
            if (service != null)
            {
                IReadOnlyList<ChronicleEvent> evt = service.GetEventsFor(pawn.StableId);
                events = evt != null ? evt.Count : 0;
            }

            byte state = pawn.DeathTick <= 0L
                ? (byte)0
                : (pawn.ArchiveStatus == PawnArchiveStatus.Active ? (byte)1 : (byte)2);
            string statusKey = state == 2
                ? "PersonalChronicle.UI.PawnFilter.Archived"
                : (state == 1 ? "PersonalChronicle.UI.PawnFilter.Dead" : "PersonalChronicle.UI.PawnFilter.Alive");

            int titleTier;
            string titleText = HighestTitleOf(pawn, out titleTier);
            EmbeddedPawnRow row = new EmbeddedPawnRow
            {
                Id = pawn.StableId ?? string.Empty,
                Name = !string.IsNullOrEmpty(pawn.LabelShort) ? pawn.LabelShort : (pawn.LabelSnapshot ?? "?"),
                Kind = KindLabel(pawn.KindDefName),
                Role = RoleLabel(pawn.Role),
                Status = statusKey.Translate().ToString(),
                Join = pawn.JoinTick > 0L ? FormatDate(pawn.JoinTick) : UnknownCell(),
                Days = pawn.JoinTick > 0L && nowTicks > pawn.JoinTick
                    ? ((nowTicks - pawn.JoinTick) / GenDate.TicksPerDay).ToString()
                    : UnknownCell(),
                Hours = (workTicks / (double)GenDate.TicksPerHour).ToString("F1"),
                Produced = pawn.Production != null ? pawn.Production.TotalQuantity.ToString() : "0",
                Kills = (pawn.MeleeKills + pawn.RangedKills).ToString(),
                Events = events.ToString(),
                Title = titleText,
                TitleTier = titleTier,
                State = state,
                DaysValue = pawn.JoinTick > 0L && nowTicks > pawn.JoinTick
                    ? (int)((nowTicks - pawn.JoinTick) / GenDate.TicksPerDay)
                    : -1,
                HoursValue = (float)(workTicks / (double)GenDate.TicksPerHour),
                ProducedValue = pawn.Production != null ? (int)pawn.Production.TotalQuantity : 0,
                KillsValue = pawn.MeleeKills + pawn.RangedKills,
                EventsValue = events
            };
            row.Tooltip = JoinedPawnTooltip(row);
            return row;
        }

        /// <summary>整行全文（构建期一次拼好）：表格单元格被截断时，悬停行即可读到全部列值。</summary>
        private static string JoinedPawnTooltip(EmbeddedPawnRow row)
        {
            string text = string.Empty;
            for (int c = 0; c < EmbeddedColumnCount; c++)
            {
                string value = EmbeddedCellValue(row, c);
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }
                if (text.Length > 0)
                {
                    text += " · ";
                }
                text += value;
            }
            return text;
        }

        private static string KindLabel(string kindDefName)
        {
            if (string.IsNullOrEmpty(kindDefName))
            {
                return UnknownCell();
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(kindDefName);
            return def != null ? def.LabelCap.ToString() : kindDefName;
        }

        /// <summary>最高职称（按 <c>ProfessionalTitleDef.order</c>）；无职称画「未知」而非留空。</summary>
        private static string HighestTitleOf(PawnObject pawn)
        {
            int tier;
            return HighestTitleOf(pawn, out tier);
        }

        /// <summary>
        /// 最高职称 + 其**档位**（预览 `.q.tierN` 徽章配色用）。档位 = 该职称在
        /// `QualificationDef`（按 `order` 升序的资格阶梯，与职业档案页「职称链」同源）
        /// 中的下标；找不到对应资格 ⇒ -1（UI 退化为中性色，不编造档位）。
        /// </summary>
        private static string HighestTitleOf(PawnObject pawn, out int tier)
        {
            tier = -1;
            if (pawn.CareerData == null || pawn.CareerData.GrantedTitles == null
                || pawn.CareerData.GrantedTitles.Count == 0)
            {
                return UnknownCell();
            }
            ProfessionalTitleDef best = null;
            for (int i = 0; i < pawn.CareerData.GrantedTitles.Count; i++)
            {
                Domain.Honor.GrantedTitle granted = pawn.CareerData.GrantedTitles[i];
                if (granted == null || string.IsNullOrEmpty(granted.TitleDefName))
                {
                    continue;
                }
                ProfessionalTitleDef def =
                    DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(granted.TitleDefName);
                if (def != null && (best == null || def.order > best.order))
                {
                    best = def;
                }
            }
            if (best == null)
            {
                return UnknownCell();
            }
            tier = QualTierIndexOf(best.defName);
            return best.LabelCap.ToString();
        }

        /// <summary>
        /// 职称 → 资格阶梯下标（0..4）。与 `ArchiveUiDataProvider.BuildTitleTiers` **同源判定**
        /// （同一组 `QualificationDef`，同按 `order` 升序）⇒ 表格徽章与职业档案页职称链不会互相矛盾。
        /// 未收录 ⇒ -1。
        /// </summary>
        private static int QualTierIndexOf(string titleDefName)
        {
            if (string.IsNullOrEmpty(titleDefName))
            {
                return -1;
            }
            List<QualificationDef> quals = DefDatabase<QualificationDef>.AllDefsListForReading;
            if (quals == null)
            {
                return -1;
            }
            // 档位 = 该资格在「按 `order` 升序的阶梯」中的名次。用「与其它项逐对比较」直接得名次
            //（O(n²)，n = 资格数 5 量级 ⇒ 只在**行构建期**跑，可忽略；不排序、零分配、无静态状态）。
            // ⚠ 与 `ArchiveUiDataProvider.BuildTitleTiers` 的 `OrderBy(order)` + 下标取色**同源**
            //   （`ITab_Pawn_Career.Qualification.cs` 用 `TitleTierColor(i)`，i 即本处的名次）。
            for (int i = 0; i < quals.Count; i++)
            {
                QualificationDef target = quals[i];
                if (target == null
                    || !string.Equals(target.titleDefName, titleDefName, System.StringComparison.Ordinal))
                {
                    continue;
                }
                int rank = 0;
                for (int k = 0; k < quals.Count; k++)
                {
                    QualificationDef other = quals[k];
                    if (other == null || k == i || string.IsNullOrEmpty(other.titleDefName))
                    {
                        continue;
                    }
                    // order 更小 ⇒ 排在前面；order 相同则按 Def 序稳定处理（避免同 order 抖动）。
                    if (other.order < target.order || (other.order == target.order && k < i))
                    {
                        rank++;
                    }
                }
                return rank;
            }
            return -1;
        }

        private static string UnknownCell()
        {
            return "PersonalChronicle.UI.Population.Roster.Unknown".Translate().ToString();
        }
    }
}

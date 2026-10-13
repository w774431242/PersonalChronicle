using System.Collections.Generic;
using PersonalChronicle.Application.PawnEconomy;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>柜台工作台 · **数据表格部分**（S2b · CHG-20261011-WORKCAT 的物理切片）。
    /// ARC-013 大文件纪律：表格 chrome（工具栏 / 筛选条 / 搜索 / 密度 / 列宽拖拽 / 行展开）
    /// 从主文件切出 —— **零语义变化**（同类 partial；表格会话态字段随本部分一起迁移）。
    ///
    /// <para><b>纪律</b>（GATE-UI / GATE-ARCH 2026-10-11 结转）：
    /// ① 列宽只由 <see cref="EconomyTableLayout"/> 算一次，表头 / 数据行 / 拖拽**共用同一结果** ⇒ 逐像素对齐；
    /// ② 排序 / 筛选 / 搜索的推进只写会话态 + 强制重快照（绘制路径不做数据变换，PERF-002）；
    /// ③ 会话态按区段**稳定 Key** 寻址，每表一份（绝不用对象引用判断"是否同一张表"）；
    /// ④ 一切文本宽度与折行都在快照期（拖拽帧是唯一例外）。</para></summary>
    public sealed partial class Window_InstitutionWorkspace
    {
        // S2b（CHG-20261011-WORKCAT）：数据表格的**会话态**（排序/筛选/搜索/列宽；不入存档）与**派生行视图**。
        // 会话态**按区段的稳定 Key 寻址**（每表一份，切换互不串；绝不用对象引用判断"是否同一张表"——
        // 读模型每次快照都新建 EconomyTable 实例，引用比较恒假曾是 S2a 的 P0：点击排序/筛选后 1 帧被
        // 复位成"点了没反应"，GATE-UI / GATE-ARCH 2026-10-11 一致 BLOCKED）。
        // 派生视图必须是**新引用**：行族的测量缓存键 = view 引用 + 宽 + 页，排序/筛选改了行序而不换引用
        // 会画出一屏陈旧布局（AI-001 明确点出的坑）。
        private readonly Dictionary<string, EconomyTableState> tableStates = new Dictionary<string, EconomyTableState>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, string> tableSearchBuffers = new Dictionary<string, string>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, float> tableSearchTimes = new Dictionary<string, float>(System.StringComparer.Ordinal);
        private string activeTableKey;                 // 当前区段（null/失配 ⇒ 快照期回到第一张）
        private EconomyView tableRows;
        // 列宽（**唯一布局结果**：表头 / 数据行 / 拖拽全部读它 —— GATE-UI P1：三者各算一次必然错位）。
        private float[] tableWidths = new float[0];
        private float tableColumnAreaWidth = -1f;      // 列区可用宽（快照期由窗口宽派生；拖拽期复用）
        private float tableContentWidth;
        private Vector2 tableHorizontalScroll;
        private int resizingColumn = -1;               // 拖拽中的列（-1 = 无）
        private float resizingStartX, resizingStartWidth;
        private RUIF.RowDensity density = RUIF.RowDensity.Compact;
        // 行展开（会话态）：按行的**稳定 Key** 记（行序会随排序变化，下标不可用）。
        private readonly HashSet<string> expandedRows = new HashSet<string>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, string> detailTexts = new Dictionary<string, string>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, float> detailHeights = new Dictionary<string, float>(System.StringComparer.Ordinal);
        private string detailOpenLabel;
        private float detailOpenWidth;

        // ── S2b：数据表格渲染（工具栏 / 筛选条 / 表头 / 列化行 / 行展开）────────────────
        private const float TableToolbarHeight = 26f;
        private const float TableFilterHeight = 24f;
        private const float TableHeaderHeight = 26f;
        private const float TableDetailGutter = 14f;     // 独立展开槽，不从任何数据/控件列扣宽
        private const float TableFrameWidth = (EconomyRowList.CardSideInset + RUIF.Tokens.CardPadX) * 2f
            + RUIF.Tokens.ScrollbarThickness;
        private const float TableMinColumnWidth = 40f;   // 列宽拖拽下界
        private const float TableMinSearchWidth = 140f;  // 搜索框最小可用宽（不足则整件隐藏，不做半截输入框）
        private const float TableDensityWidth = 184f;    // 密度三档（RUIF DensitySwitch：56×3 + SpaceXs×2）
        // 快照期预量（绘制期只读）
        private string[] tableTabLabels;
        private float tableTabWidth;
        private string[] tableFilterLabels, tableFilterKeys;
        private float tableFilterWidth;
        private string[] tableHeaderLabels;
        private bool tableHasDetails;
        private bool tableHasControls;
        private string tableSearchPlaceholder;
        /// <summary>**起草回调**（CHG-20261012）：工作需求表可点击行（<c>EconomyRow.Compose != null</c>）
        /// 被点击时把载荷 <c>{anchorKey}|{workType}</c> 交给宿主窗口（由它算可派发/推荐价并开弹窗）。</summary>
        private System.Action<string> onComposeRow;
        /// <summary>**撤销回调**（B1 · CHG-20261012）：**控件格**（<see cref="EconomyCellControl.Action"/>）里的
        /// 按钮被按下时，把行的撤销载荷（合同号）交给宿主窗口（由它确认后走唯一写入口）。</summary>
        private System.Action<string> onRevokeRow;
        /// <summary>控件格按钮的**文案宽**（快照期按文案预量 ⇒ 绘制路径零 Measure，PERF-002）。
        /// 键 = 按钮文案（同一文案复用一个宽度；行数有界 ⇒ 条目有界）。</summary>
        private readonly Dictionary<string, float> tableActionWidths = new Dictionary<string, float>(System.StringComparer.Ordinal);
        /// <summary>控件高度（快照期取一次 —— 绘制路径零 Measure）。</summary>
        private float tableControlHeight;

        private int IndexOfTableSection(string key)
        {
            if (string.IsNullOrEmpty(key)) return -1;
            for (int i = 0; i < view.Tables.Count; i++)
                if (string.Equals(view.Tables[i].Key, key, System.StringComparison.Ordinal)) return i;
            return -1;
        }
        /// <summary>列区几何（**唯一表达式**：表头与列化行共用 ⇒ 与行族 <c>CardFrame</c> 的内容区逐像素重合）。
        /// 行族内容区 = 明细区宽 − 滚动条厚；卡 = 内容区 − <c>CardSideInset</c>×2；卡内容 = 卡 − <c>CardPadX</c>×2。</summary>
        private Rect TableColumnArea(Rect rect)
        {
            float inset = EconomyRowList.CardSideInset + RUIF.Tokens.CardPadX;
            return new Rect(rect.x + inset, rect.y,
                Mathf.Max(1f, rect.width - TableFrameWidth - (tableHasDetails ? TableDetailGutter : 0f)), rect.height);
        }
        private float TableColumnsWidth(float areaWidth)
            => Mathf.Max(1f, areaWidth - TableFrameWidth - (tableHasDetails ? TableDetailGutter : 0f));
        /// <summary>列宽布局（唯一入口）：表声明 + 会话态覆盖 → <see cref="tableWidths"/>。</summary>
        private void LayoutTableColumns(EconomyTable table, EconomyTableState state)
        {
            int columns = table.Columns.Count;
            if (tableWidths == null || tableWidths.Length != columns) tableWidths = new float[columns];
            EconomyTableLayout.Compute(Mathf.Max(1f, tableColumnAreaWidth), table.Columns, state?.ColumnWidths, tableWidths);
            float total = 0f;
            for (int i = 0; i < tableWidths.Length; i++) total += tableWidths[i];
            tableContentWidth = total + TableFrameWidth + (tableHasDetails ? TableDetailGutter : 0f);
        }
        /// <summary>展开详情的一行摘要与高度在快照期算好；全文仅悬停，绘制期零测量（PERF-002）。</summary>
        private void BuildDetailCache(WorkspaceTableSection section)
        {
            detailTexts.Clear(); detailHeights.Clear();
            if (expandedRows.Count == 0) return;
            // 摘要按视口列区预算，拖拽改变虚拟内容宽时仍容纳在一行内；全文不受此预算影响。
            float width = Mathf.Max(1f, tableColumnAreaWidth - detailOpenWidth - RUIF.Tokens.SpaceSm);
            GameFont previous = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                for (int i = 0; i < section.Rows.Count; i++)
                {
                    var row = section.Rows[i];
                    if (row?.Detail == null || row.Key == null || !expandedRows.Contains(row.Key)) continue;
                    detailTexts[row.Key] = EconomyTableLayout.DetailSummary(row.Detail, width,
                        s => RUIF.Measure.Width(s, GameFont.Small));
                    detailHeights[row.Key] = RUIF.Measure.ControlHeight(GameFont.Small) + RUIF.Tokens.SpaceXs;
                }
            }
            finally { Text.Font = previous; }
        }
        /// <summary>行高变化（密度 / 展开 / 列宽）后手动丢弃行族测量缓存 —— 其缓存键（view+宽+页）不含高度函数。</summary>
        private void InvalidateTableMeasure()
        {
            rowList.MeasuredPage = -99999;
            rowList.MeasuredView = null;
        }
        /// <summary>快照期预量工具栏 / 筛选条 / 表头 / 列宽（绘制路径零 Measure.Width、零拼接）。</summary>
        private void RefreshTableChrome(WorkspaceTableSection section, EconomyTableState state, float areaWidth)
        {
            var table = section.Table;
            detailOpenLabel = LaborPresentationRows.Text("OpenRecord");
            detailOpenWidth = EconomyRowList.RowButtonWidth(detailOpenLabel);
            int tabs = view.Tables.Count;
            tableTabLabels = new string[tabs];
            float widest = 0f;
            for (int i = 0; i < tabs; i++)
            {
                tableTabLabels[i] = view.Tables[i].Label;
                float tab = RUIF.Measure.Width(tableTabLabels[i], GameFont.Tiny) + RUIF.Tokens.SpaceLg;
                if (tab > widest) widest = tab;
            }
            tableTabWidth = widest * tabs;
            int filters = table.FilterKeys.Count;
            tableFilterLabels = new string[filters + 1]; tableFilterKeys = new string[filters + 1];
            tableFilterLabels[0] = W("Workspace.Table.All"); tableFilterKeys[0] = string.Empty;
            float filterMax = RUIF.Measure.Width(tableFilterLabels[0], GameFont.Tiny) + RUIF.Tokens.SpaceLg;
            for (int i = 0; i < filters; i++)
            {
                tableFilterLabels[i + 1] = table.FilterLabels[i]; tableFilterKeys[i + 1] = table.FilterKeys[i];
                float label = RUIF.Measure.Width(tableFilterLabels[i + 1], GameFont.Tiny) + RUIF.Tokens.SpaceLg;
                if (label > filterMax) filterMax = label;
            }
            tableFilterWidth = filterMax * (filters + 1);
            int columns = table.Columns.Count;
            tableHeaderLabels = new string[columns];
            for (int i = 0; i < columns; i++) tableHeaderLabels[i] = table.Columns[i].Label;
            tableHasDetails = false;
            for (int i = 0; i < section.Rows.Count; i++)
                if (section.Rows[i]?.Detail != null) { tableHasDetails = true; break; }
            tableHasControls = false;
            for (int i = 0; i < columns; i++)
                if (table.Columns[i].Control != EconomyCellControl.None) { tableHasControls = true; break; }
            tableSearchPlaceholder = W("Workspace.Table.Search");   // 翻译一次（绘制路径零键拼接）
            tableColumnAreaWidth = TableColumnsWidth(areaWidth);
            LayoutTableColumns(table, state);
            // 起草回调（快照期设一次 —— 方法组转换有分配，绝不放进绘制路径）。
            onComposeRow = OpenWorkContractDialog;
            // B1（P1-2）：控件格（操作列）的按钮文案宽与回执也在快照期备好 —— 绘制路径零 Measure（PERF-002）。
            tableControlHeight = RUIF.Measure.ControlHeight(GameFont.Small);
            tableActionWidths.Clear();
            for (int i = 0; i < section.Rows.Count; i++)
            {
                var row = section.Rows[i];
                if (row?.Action == null || row.Revoke == null) continue;
                if (tableActionWidths.ContainsKey(row.Action)) continue;
                tableActionWidths[row.Action] = EconomyRowList.RowButtonWidth(row.Action);
            }
            onRevokeRow = ConfirmRevokeContract;
        }
        private static RUIF.SortState ToRuifSort(EconomySortOrder order)
        {
            switch (order)
            {
                case EconomySortOrder.Asc: return RUIF.SortState.Asc;
                case EconomySortOrder.Desc: return RUIF.SortState.Desc;
                default: return RUIF.SortState.None;
            }
        }
        /// <summary>明细区（表格区段）总入口：工具栏 → 筛选条（若有筛选面）→ 表头 → 行族。</summary>
        private void DrawTableArea(Rect area)
        {
            int index = IndexOfTableSection(activeTableKey);
            if (index < 0) return;
            var section = view.Tables[index];
            tableStates.TryGetValue(section.Key, out var state);
            var body = area;
            DrawTableToolbar(new Rect(body.x, body.y, body.width, TableToolbarHeight), section, state);
            body = CutTop(body, TableToolbarHeight + RUIF.Tokens.SpaceXs);
            if (section.Table.FilterKeys.Count > 0)
            {
                DrawTableFilter(new Rect(body.x, body.y, body.width, TableFilterHeight), state);
                body = CutTop(body, TableFilterHeight + RUIF.Tokens.SpaceXs);
            }
            // 表头和行列表置于同一横向视口；列最小宽超预算仍可到达末列操作，而非整列静默丢失。
            bool horizontal = tableContentWidth > body.width + 0.5f;
            if (horizontal)
            {
                var virtualBody = new Rect(0f, 0f, tableContentWidth,
                    Mathf.Max(1f, body.height - RUIF.Tokens.ScrollbarThickness));
                tableHorizontalScroll.y = 0f;
                Widgets.BeginScrollView(body, ref tableHorizontalScroll, virtualBody);
                try { DrawTableScrollableBody(virtualBody, section, state); }
                finally { Widgets.EndScrollView(); }
            }
            else
            {
                tableHorizontalScroll = Vector2.zero;
                DrawTableScrollableBody(body, section, state);
            }
        }
        private void DrawTableScrollableBody(Rect body, WorkspaceTableSection section, EconomyTableState state)
        {
            DrawTableHeader(new Rect(body.x, body.y, body.width, TableHeaderHeight), section, state);
            body = CutTop(body, TableHeaderHeight + RUIF.Tokens.SpaceXs);
            EconomyRowList.Draw(body, tableRows, rowList, RowHeightOf,
                (content, y, row) =>
                {
                    if (row.Cells != null) return DrawTableRow(content, y, row, section);
                    if (row.Progress >= 0f) DrawProgressRow(content, y, row);
                    return false;
                }, null);
        }
        private static Rect CutTop(Rect rect, float height)
            => new Rect(rect.x, rect.y + height, rect.width, Mathf.Max(1f, rect.height - height));
        /// <summary>行高：列化行随密度与展开；进度行固定；其余交行族通用逻辑（返回 0）。</summary>
        private float TableRowContentHeight()
            => RUIF.TableFmt.RowHeight(density) + (tableHasControls
                ? Mathf.Max(0f, tableControlHeight - RUIF.TableFmt.RowHeight(RUIF.RowDensity.Compact)) : 0f);
        private float RowHeightOf(EconomyRow row)
        {
            if (row.Cells == null) return row.Progress >= 0f ? ProgressRowHeight : 0f;
            float height = EconomyWindowLayout.TableRowHeight(TableRowContentHeight(),
                tableHasControls ? tableControlHeight : 0f, RUIF.Tokens.CardPadY);
            if (row.Key != null && expandedRows.Contains(row.Key)
                && detailHeights.TryGetValue(row.Key, out float detail)) height += detail;
            return height;
        }
        /// <summary>工具栏：表切换（≥2 表）· 搜索（可搜表）· 密度三档（右侧；窄窗降级为隐藏）。</summary>
        private void DrawTableToolbar(Rect rect, WorkspaceTableSection section, EconomyTableState state)
        {
            if (state == null) return;
            float x = rect.x;
            if (view.Tables.Count > 1 && tableTabLabels != null)
            {
                int index = IndexOfTableSection(activeTableKey);
                if (index < 0) index = 0;
                float width = Mathf.Min(tableTabWidth, rect.width);
                // InputAtoms.Segmented：选中段自带填充（Selected 态可辨）。S2a 的筛选条曾用
                // PrimaryButton + Accent/Default 色调区分，而 RUIF 原子下两者同色 ⇒ "选中"实际不可辨
                //（GATE-UI P1）—— 凡"选中态"一律走分段件。
                bool changed = false;
                if (tableTabWidth > rect.width)
                {
                    if (RUIF.ButtonAtoms.PrimaryButton(new Rect(x, rect.y, width, rect.height), tableTabLabels[index] + " ▾"))
                    {
                        var options = new List<FloatMenuOption>();
                        for (int i = 0; i < view.Tables.Count; i++)
                        {
                            string selectedKey = view.Tables[i].Key;
                            options.Add(new FloatMenuOption(view.Tables[i].Label, () => SelectTableSection(selectedKey)));
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                }
                else changed = RUIF.InputAtoms.Segmented(new Rect(x, rect.y, width, rect.height), tableTabLabels, ref index);
                if (changed)
                {
                    string picked = view.Tables[index].Key;
                    // Segmented 点"当前段"也返回 changed ⇒ 这里按 Key 判**真变化**（避免无意义重快照）。
                    if (!string.Equals(picked, activeTableKey, System.StringComparison.Ordinal))
                    {
                        activeTableKey = picked;
                        tableHorizontalScroll = Vector2.zero;
                        rowList.Reset();     // 行族滚动/页码属于上一张表（与切换条"换页归零"同纪律）
                        lastTick = -99999;   // 换表 ⇒ 立即重快照（行集/列集/预量全换）
                        return;
                    }
                }
                x += width + RUIF.Tokens.SpaceSm;
            }
            float right = rect.xMax;
            if (right - x > TableDensityWidth + RUIF.Tokens.SpaceSm + TableMinSearchWidth)
            {
                var densityRect = new Rect(right - TableDensityWidth, rect.y, TableDensityWidth, rect.height);
                if (RUIF.TableAtoms.DensitySwitch(densityRect, ref density)) InvalidateTableMeasure();
                right = densityRect.x - RUIF.Tokens.SpaceSm;
            }
            if (!section.Table.Searchable) return;
            float searchWidth = right - x;
            if (searchWidth < TableMinSearchWidth) return;   // 窄窗降级：宽度不足则整件隐藏
            if (searchWidth > 320f) searchWidth = 320f;
            string key = section.Key;
            string query = state.Search ?? string.Empty;
            string buffer = tableSearchBuffers.TryGetValue(key, out var cached) ? cached : query;
            float last = tableSearchTimes.TryGetValue(key, out var stamp) ? stamp : 0f;
            if (RUIF.InputAtoms.SearchField(new Rect(x, rect.y, searchWidth, rect.height), ref query, ref buffer,
                    tableSearchPlaceholder, ref last))
            {
                state.Search = query;
                lastTick = -99999;   // 命中集变了 ⇒ 下一帧重快照（搜索不进绘制路径）
            }
            tableSearchBuffers[key] = buffer;
            tableSearchTimes[key] = last;
        }
        private void SelectTableSection(string key)
        {
            if (string.Equals(activeTableKey, key, System.StringComparison.Ordinal)) return;
            activeTableKey = key; tableHorizontalScroll = Vector2.zero; rowList.Reset(); lastTick = -99999;
        }
        /// <summary>筛选条（仅当读模型声明了筛选面）：分段件，选中段可辨；点击即切换并强制重快照。</summary>
        private void DrawTableFilter(Rect rect, EconomyTableState state)
        {
            if (state == null || tableFilterLabels == null || tableFilterLabels.Length <= 1) return;
            int index = 0;
            for (int i = 1; i < tableFilterKeys.Length; i++)
                if (string.Equals(state.FilterKey ?? string.Empty, tableFilterKeys[i], System.StringComparison.Ordinal)) { index = i; break; }
            float width = Mathf.Min(tableFilterWidth, rect.width);
            if (RUIF.InputAtoms.Segmented(new Rect(rect.x, rect.y, width, rect.height), tableFilterLabels, ref index))
            {
                string key = index <= 0 || string.IsNullOrEmpty(tableFilterKeys[index]) ? null : tableFilterKeys[index];
                // Segmented 点"当前段"也返回 changed ⇒ 按值判**真变化**（避免无意义重快照）。
                if (!string.Equals(key ?? string.Empty, state.FilterKey ?? string.Empty, System.StringComparison.Ordinal))
                {
                    state.FilterKey = key;
                    lastTick = -99999;   // 行集变了 ⇒ 立刻重快照（排序/筛选不进绘制路径）
                }
            }
        }
        /// <summary>表头：可排序列走 RUIF 表头原子（内置 ▲▼ 三态）；不可排序列画静态标签（**不挂死按钮**，F4）。
        /// 列右缘是列宽拖拽手柄（<c>TableAtoms.ResizeHandle</c> **只作视觉**；拖拽由本窗口自持状态机驱动）。</summary>
        private void DrawTableHeader(Rect rect, WorkspaceTableSection section, EconomyTableState state)
        {
            if (state == null) return;
            var area = TableColumnArea(rect);
            // ── 拖拽状态机**先于任何 ButtonInvisible**（含 ResizeHandle 内部那颗）：
            // IMGUI 的 hot control 会把事件 type 改成 Used —— 之后再判 MouseDrag/MouseUp 必然失败。
            // 另：`ResizeHandle` 的返回值来自 `Widgets.ButtonInvisible`（GUI.Button 契约 = **MouseUp 帧**才为 true），
            // 不能当"按下"判据（GATE-UI 2026-10-11 P1：曾因此拖拽全程不可用）。
            bool resizingNow = resizingColumn >= 0;
            bool justEnded = AdvanceTableResize(section, state);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                // 兜底（GATE-ARCH 第二轮 P2）：上一次拖拽的 MouseUp 若丢失（松手于游戏窗口外 / 被其它控件吞），
                // **本次按下即复位**悬空状态 —— 否则悬空期间任意 MouseDrag 都会误改列宽
                //（对照 WorkshopLayoutCanvas 的"MouseDown 复位"先例）。正常路径下此处已为 -1，无影响。
                if (resizingNow && !justEnded) { resizingColumn = -1; resizingNow = false; }
                if (BeginTableResizeOnHandle(area, section, Event.current.mousePosition)) resizingNow = true;
            }
            bool headerClickable = !resizingNow && !justEnded;
            float x = area.x;
            for (int i = 0; i < section.Table.Columns.Count && i < tableWidths.Length; i++)
            {
                float width = tableWidths[i];
                if (width <= 0f) break;
                if (i > 0 && x + width > area.xMax + 0.5f) break;   // 浮点容差（不留"差半像素被整列裁掉"）
                var column = section.Table.Columns[i];
                var cell = new Rect(x, area.y, width, area.height);
                if (i < section.Table.Columns.Count - 1)
                    RUIF.TableAtoms.ResizeHandle(cell, x + width);   // 仅视觉（列分隔线/把手）
                if (column.Sortable)
                {
                    if (RUIF.TableAtoms.HeaderCell(cell, tableHeaderLabels[i], ToRuifSort(state.OrderOf(i)))
                        && headerClickable)
                    {
                        state.NextSort(i);
                        lastTick = -99999;
                    }
                }
                else RUIF.Draw.Label(cell, column.Label, RUIF.Tokens.FontLabel, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
                // LAND-008：表头文本走 RUIF 原子（Tiny、无省略号）—— 窄列会硬裁，悬停给全文（文本为快照期缓存）。
                if (headerClickable && Mouse.IsOver(cell)) TooltipHandler.TipRegion(cell, tableHeaderLabels[i]);
                x += width;
            }
        }
        /// <summary>按鼠标位置命中列宽把手（几何与 <c>ResizeHandle</c> 的把手一致：列右缘 ±3px，命中放宽到 ±4px）。
        /// 自己命中而**不用** <c>ResizeHandle</c> 的返回值 —— 后者是"MouseUp 帧的点击"（GUI.Button 契约）。
        /// **竖向约束**（GATE-UI 第二轮 P2）：只在表头带内起手 —— 否则数据行 / 工具栏上恰好落到分割线 x 的按下
        /// 也会改列宽；命中循环同时镜像绘制的溢出断点（第二轮回审 P3）。</summary>
        private bool BeginTableResizeOnHandle(Rect area, WorkspaceTableSection section, Vector2 mouse)
        {
            if (!area.Contains(mouse)) return false;
            int last = section.Table.Columns.Count - 1;
            float x = area.x;
            for (int i = 0; i < last && i < tableWidths.Length; i++)
            {
                float width = tableWidths[i];
                if (width <= 0f) break;
                x += width;
                if (i > 0 && x > area.xMax + 0.5f) break;   // 与绘制同断点：不命中未绘制的边界
                if (Mathf.Abs(mouse.x - x) <= 4f) { BeginTableResize(i, width); return true; }
            }
            return false;
        }
        private void BeginTableResize(int column, float startWidth)
        {
            resizingColumn = column;
            resizingStartWidth = startWidth;
            resizingStartX = Event.current.mousePosition.x;
        }
        /// <summary>拖拽推进（**绘制期唯一例外**：列宽必须逐帧跟随指针；其余一律快照期）。
        /// 返回 <c>true</c> = 本帧**刚结束**一次拖拽（调用方须抑制表头点击，避免松手瞬间误触排序）。
        /// 松手时强制一次重快照 —— 把拖拽期"只更列宽"的状态并回快照期统一预量（列宽覆盖已写会话态，
        /// 快照期据此重建 chrome；展开详情的折行宽取整列区宽，不随单列变化，第二轮回审已纠正此表述）。
        ///
        /// <para><b>为什么用 `Event.current` 而不是 `Input.GetMouseButton`</b>：本工程的编译引用集
        /// （`_scripts/compile.rsp`）不含 `UnityEngine.InputLegacyModule.dll`（全仓 0 处使用 `Input`），
        /// 引入新模块引用属构建链变更；IMGUI 的 MouseDrag / MouseUp 事件是原版拖拽的既有实现方式
        /// （`Verse.Widgets` 同源），零新增依赖。滑出窗口导致 MouseUp 丢失的极端情况由"下次按下重设起点"兜底。</para></summary>
        private bool AdvanceTableResize(WorkspaceTableSection section, EconomyTableState state)
        {
            if (resizingColumn < 0 || state == null) return false;
            var current = Event.current;
            if (current == null) return false;
            if (current.type == EventType.MouseUp && current.button == 0)
            {
                resizingColumn = -1;
                lastTick = -99999;
                return true;
            }
            // 只在拖拽帧更新；静止帧（Repaint 等）保持 —— 指针没动就没有需要刷新的东西。
            if (current.type != EventType.MouseDrag || current.button != 0) return false;
            var overrides = state.WidthsFor(section.Table.Columns.Count);
            float min = Mathf.Max(TableMinColumnWidth, section.Table.Columns[resizingColumn].MinWidth);
            float max = EconomyTableLayout.ResizeMaximum(tableColumnAreaWidth, section.Table.Columns,
                resizingColumn, TableMinColumnWidth);
            overrides[resizingColumn] = RUIF.TableFmt.ResizeWidth(resizingStartWidth,
                current.mousePosition.x - resizingStartX, min, max);
            LayoutTableColumns(section.Table, state);
            return false;
        }
        /// <summary>列化行：按列序排布（数字列右对齐）；每格 D1 省略；整行悬停给全量文本（LAND-008）；
        /// 有详情的行点击整行展开/收起一行摘要（文本已在快照期截断）。返回 true = 命中并已改变状态。</summary>
        private bool DrawTableRow(Rect content, float y, EconomyRow row, WorkspaceTableSection section)
        {
            float baseHeight = TableRowContentHeight();
            bool expanded = row.Key != null && expandedRows.Contains(row.Key);
            float detailHeight = expanded && detailHeights.TryGetValue(row.Key, out float detail) ? detail : 0f;
            var card = new Rect(content.x + EconomyRowList.CardSideInset, y,
                Mathf.Max(1f, content.width - EconomyRowList.CardSideInset * 2f),
                EconomyWindowLayout.TableRowHeight(baseHeight, 0f, RUIF.Tokens.CardPadY) + detailHeight);
            bool hovered = Mouse.IsOver(card);
            var inner = EconomyUiKit.CardFrame(card, hovered, null);
            // 列区 = **卡片内容区**（inner）：与表头的 TableColumnArea(body) 数值相等（body−18−12−24 = body−54）。
            // 曾对已扣滚动条的 content 再调 TableColumnArea ⇒ 二次扣除 18px（展开指示条与末列右缘错位，GATE-UI P2）。
            var area = inner;
            float x = area.x;
            // 控件格几何（由**列声明**给出 —— 呈现层不认列键、不认"末列"位置，LAND-010）。
            Rect controlCell = default;
            for (int i = 0; i < section.Table.Columns.Count && i < row.Cells.Length && i < tableWidths.Length; i++)
            {
                float width = tableWidths[i];
                if (width <= 0f) break;
                var cell = new Rect(x, inner.y, Mathf.Max(1f, width - RUIF.Tokens.SpaceXs), baseHeight);
                if (section.Table.Columns[i].Control == EconomyCellControl.Action) controlCell = cell;
                else
                    RUIF.Draw.EllipsisLabel(cell, row.Cells[i], GameFont.Small, RUIF.Tokens.Text,
                        section.Table.Columns[i].Numeric ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
                x += width;
            }
            // ── 控件格（B1 · P1-2）：只在**行载荷**给了撤销键、且列声明为控件格时出现。必须在**行级展开
            // 按钮之前**处理：IMGUI 的 hot control 只有一个，整卡 ButtonInvisible 先跑会把这次点击吃掉
            //（与表头拖拽"状态机先于任何 ButtonInvisible"同源教训）。命中即中断本帧。
            if (row.Revoke != null && row.Action != null && controlCell.width > 1f)
            {
                float wanted = tableActionWidths.TryGetValue(row.Action, out float cached) ? cached : controlCell.width;
                var button = new Rect(controlCell.x, inner.y + (baseHeight - tableControlHeight) * 0.5f,
                    Mathf.Min(wanted, controlCell.width), tableControlHeight);
                if (RUIF.ButtonAtoms.PrimaryButton(button, row.Action, true, EconomyRowList.RowButtonTone(row.Verb)))
                {
                    onRevokeRow?.Invoke(row.Revoke);
                    return true;
                }
                if (Mouse.IsOver(button)) TooltipHandler.TipRegion(button, row.Action + "\n" + row.Text);
            }
            if (tableHasDetails && row.Detail != null)
                RUIF.Draw.Label(new Rect(x, inner.y, TableDetailGutter, baseHeight),
                    expanded ? "▾" : "▸", GameFont.Tiny, RUIF.Tokens.Muted, TextAnchor.MiddleCenter);
            if (expanded && row.Key != null && detailTexts.TryGetValue(row.Key, out string flowed))
            {
                float buttonWidth = Mathf.Min(detailOpenWidth, inner.width);
                var open = new Rect(inner.xMax - buttonWidth, inner.y + baseHeight, buttonWidth,
                    Mathf.Max(1f, detailHeight - RUIF.Tokens.SpaceXs));
                RUIF.Draw.Label(new Rect(inner.x, inner.y + baseHeight,
                    Mathf.Max(1f, inner.width - buttonWidth - RUIF.Tokens.SpaceSm), open.height),
                    flowed, GameFont.Small, RUIF.Tokens.Dim, TextAnchor.UpperLeft);
                if (RUIF.ButtonAtoms.PrimaryButton(open, detailOpenLabel))
                {
                    Find.WindowStack.Add(new Window_LaborRecord(row.Detail));
                    return true;
                }
            }
            if (hovered)
                TooltipHandler.TipRegion(card, row.Detail == null ? row.Text : row.Text + "\n" + row.Detail);
            // CHG-20261012-ECON-CONTRACTIZE：**点行起草合同**（工作需求表）—— 与行展开**互斥**
            // （Compose 行不参与展开：一次点击只有一个含义，F4）。命中区 = 行卡本身（不含表头与列宽把手，
            // 后者在表头带内 —— 竖向约束的教训已由 S2b 记档）。
            if (row.Compose != null && Widgets.ButtonInvisible(card))
            {
                onComposeRow?.Invoke(row.Compose);
                return true;
            }
            if (row.Detail != null && row.Key != null && Widgets.ButtonInvisible(card))
            {
                if (!expandedRows.Remove(row.Key)) expandedRows.Add(row.Key);
                InvalidateTableMeasure();
                lastTick = -99999;   // 详情摘要/高度回快照期重算
                return true;         // 中断本帧：下一帧以新行高重新测量绘制
            }
            return false;
        }

        /// <summary>CHG-20261012-ECON-CONTRACTIZE：工作需求表的**行点击起草** —— 解析载荷
        /// <c>{anchorKey}|{workType}</c>，用与表格**同源**的读数算好"可派发件数 + 推荐价"，打开合同弹窗；
        /// 弹窗确认时经**唯一写入口** <c>TryDraftVanillaWorkContract</c> 落库（成功 ⇒ 立即重快照，
        /// 表格的"已派发/就业率"与新合同同步上屏）。</summary>
        private void OpenWorkContractDialog(string compose)
        {
            var runtime = GameComponent_PawnEconomy.Current;
            if (string.IsNullOrEmpty(compose) || runtime == null || map == null) return;
            int separator = compose.IndexOf('|');
            if (separator <= 0 || separator >= compose.Length - 1) return;
            string anchor = compose.Substring(0, separator);
            string[] workParts = compose.Substring(separator + 1).Split(new[] { '|' });
            string workType = workParts[0];
            string orderKey = workParts.Length > 1 ? workParts[1] : null;
            var order = GameComponent_PawnEconomy.FindVanillaOrder(orderKey);
            int available = order == null
                ? runtime.QueryVanillaWorkBacklog(map, workType, GameComponent_PawnEconomy.VanillaWorkBacklogCap)
                    - runtime.VanillaJobsCommitted(map, workType, anchor)
                : runtime.AvailableOrderJobs(map, anchor, order);
            long recommended = runtime.QuotedWorkPrice(map, anchor, workType, orderKey, out bool fromMarket);
            string label = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workType)?.labelShort ?? workType;
            if (order != null) label += " · " + W("Order." + order.Key);
            Find.WindowStack.Add(new Dialog_WorkContract(label, available, recommended, fromMarket,
                (jobs, wage, start, end) =>
                {
                    string error = null;
                    bool ok = runtime.TryDraftVanillaWorkContract(anchor, workType, jobs, wage, start, end,
                        out string _, out error, orderKey);
                    if (ok) lastTick = -99999;   // 起草成功 ⇒ 立刻重快照
                    return ok ? null : error;
                }, runtime.ReadWorkPrice(anchor, workType) > 0 || runtime.ReadWorkPrice(anchor, workType, orderKey) > 0
                    ? W("WorkPrices.Applied") : null));
        }

        /// <summary>B1（CHG-20261012 · P1-2）：「工作合同」表**控件格「撤销」的回执** —— 破坏性操作先经本体
        /// 确认框（自动化域既有先例），确认后走**唯一写入口** <c>TryRevokePostedContract</c>。
        /// 成功 ⇒ 立即重快照（该行消失，"已派发 / 就业率 / 未付 / 资金预留"随之回落）；失败 ⇒ 按稳定 reason
        /// 键如实回报（缺键回落原 ID，可解释；**绝不静默**，F4）。</summary>
        private void ConfirmRevokeContract(string contractId)
        {
            var runtime = GameComponent_PawnEconomy.Current;
            if (runtime == null || string.IsNullOrEmpty(contractId)) return;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                W("Workspace.Contract.RevokeConfirm"),
                () =>
                {
                    if (runtime.TryRevokePostedContract(contractId, out string reason))
                    { lastTick = -99999; return; }
                    Messages.Message(W("Workspace.Contract.Revoke.Reason." + reason), MessageTypeDefOf.RejectInput, false);
                },
                destructive: true));
        }
    }
}

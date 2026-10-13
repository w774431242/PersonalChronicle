using PersonalChronicle.Archive;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// Partial of <see cref="PersonnelArchivePanel"/> ——嵌入档物品 pane 的**7 列明细表**
    /// （预览 <c>#cat-thing</c>，HTML 1136-1168）。
    ///
    /// <para><b>列与真源（预览表头 1141-1143）</b>：
    /// 弱标识 / 物品 / 末任持有 / 持有代 / 击杀 / 当前持有 / 退役。
    /// 其中两列是**如实缺口**（预览自己也把品质列为缺口，HTML 1160-1164）：</para>
    /// <list type="bullet">
    /// <item>「制造者」—— <c>ThingObject</c> / <c>HolderRecord</c> / <c>DecommissionRecord</c>
    ///       均无制造者字段 ⇒ 本表列名改为「末任持有」（取 <c>Decommission.LastHolderLabel</c>），
    ///       而不是把末任持有者冒充制造者。</item>
    /// <item>「击杀」—— 物品级击杀需要**按武器筛选的事件索引**，本轮未接 ⇒ 该列画「—」，
    ///       并在表下如实说明（绝不填 0 冒充「无击杀」）。</item>
    /// </list>
    ///
    /// <para><b>几何</b>：沿用人物表同规（行高 20 / 表头 20 / 数值列右对齐），列宽比例表合计 1.0。</para>
    /// </summary>
    public sealed partial class PersonnelArchivePanel
    {
        private const int EmbeddedThingColumnCount = 7;
        /// <summary>
        /// 物品表行高 —— **25**（预览实测 td 高 **24.1**；与人物表同口径：Tiny 行高 18 + `3.5px` 上下）。
        /// 旧值 20 比预览矮 4~5px。
        /// </summary>
        private const float EmbeddedThingRowHeight = 25f;

        /// <summary>列宽比例（合计 1.0）：弱标识与物品名较长，数值列窄。</summary>
        private static readonly float[] EmbeddedThingColumnRatios =
        {
            0.235f, 0.195f, 0.155f, 0.080f, 0.070f, 0.155f, 0.110f
        };

        /// <summary>数值列右对齐（持有代 / 击杀）。</summary>
        private static readonly bool[] EmbeddedThingRightAligned =
        {
            false, false, false, true, true, false, false
        };

        private struct EmbeddedThingRow
        {
            public string StableId;
            public string WeakId;
            public string Thing;
            public string LastHolder;
            public string HolderGens;
            public string Kills;
            public string HolderState;
            public string Retired;
            /// <summary>0 在用 / 1 已退役 / 2 已毁（配色用；程序逻辑不比对文本，GOV-009）。</summary>
            public byte State;

            /// <summary>当前持有状态：0 无持有者 / 1 在世 / 2 已归档。**配色只读本字段**，绝不比对翻译文本（GOV-009）。</summary>
            public byte HolderStateCode;

            /// <summary>整行全文（构建期拼好）——LAND-008：被截断的单元格信息必须可达，提示零绘制期拼接。</summary>
            public string Tooltip;

            /// <summary>持有代数值（排序列用真实数字，不用格式化文本）。</summary>
            public int HolderGensValue;
        }

        private readonly List<EmbeddedThingRow> embeddedThingRows = new List<EmbeddedThingRow>();
        private long embeddedThingRowsRevision = -1L;
        private float[] embeddedThingColumnWidths;
        private Vector2 embeddedThingScroll;

        private void DrawEmbeddedThingTable(Rect pane, List<ArchiveObject> objects, IArchiveService service)
        {
            float top = DrawEmbeddedThingSearch(new Rect(pane.x, pane.y, pane.width, EmbeddedFilterHeight));

            // 【1:1 实测 · 补全方案 P1】预览 `#cat-thing` 是**两列**：左 = 7 列表（实测 898.6 ≈ 49.6% 宽），
            //   右 = 「真实字段清单 + 如实缺口」列（HTML 1246-1257），列间距 10。
            //   窄档（< 900）降级为单列：只画表 + 表下原有的一行缺口说明（LAND-004：不硬挤两列）。
            const float columnGap = 10f;
            bool twoColumn = pane.width >= 900f;
            float tableWidth = twoColumn ? Mathf.Max(360f, pane.width * 0.5f - columnGap * 0.5f) : pane.width;
            Rect table = new Rect(pane.x, top, tableWidth, Mathf.Max(0f, pane.yMax - top));
            if (table.width < 140f || table.height < 40f)
            {
                return;
            }
            EnsureEmbeddedThingRows(objects, service);
            // 列宽按「内容视口宽」计算（与人物表同规；避免滚动时最后一列压住滚动条）。
            DrawEmbeddedThingColumnWidths(table.width - 2f - EmbeddedScrollbarWidth);

            Widgets.DrawBoxSolid(table, UITheme.Panel);
            UIComponents.Border(table, UITheme.BorderSoft);
            Rect header = new Rect(table.x + 1f, table.y + 1f, table.width - 2f, EmbeddedThingRowHeight);
            DrawEmbeddedThingHeader(header);
            UIComponents.Rule(new Rect(header.x, header.yMax - 1f, header.width, UITheme.RuleHeight),
                UITheme.BorderSoft);

            // 表下如实说明（缺口）—— 占一行，计入内容高。两列档下该行**不再画**（内容已移到右列）。
            float footerHeight = twoColumn ? 0f : 16f;
            Rect body = new Rect(table.x + 1f, header.yMax, table.width - 2f,
                Mathf.Max(0f, table.yMax - header.yMax - 1f - footerHeight));
            float contentHeight = embeddedThingRows.Count * EmbeddedThingRowHeight;
            Rect viewRect = new Rect(body.x, body.y, Mathf.Max(1f, body.width - EmbeddedScrollbarWidth),
                Mathf.Max(body.height, contentHeight));
            if (contentHeight <= body.height)
            {
                DrawEmbeddedThingRows(body, body.y, body.yMax);
            }
            else
            {
                Widgets.BeginScrollView(body, ref embeddedThingScroll, viewRect);
                try
                {
                    DrawEmbeddedThingRows(new Rect(viewRect.x, viewRect.y, viewRect.width, contentHeight),
                        body.y, body.yMax);
                }
                finally
                {
                    Widgets.EndScrollView();
                }
            }

            if (twoColumn)
            {
                DrawThingFieldColumn(new Rect(pane.x + tableWidth + columnGap, top,
                    Mathf.Max(0f, pane.width - tableWidth - columnGap), Mathf.Max(0f, pane.yMax - top)));
                return;
            }
            float footerWidth = Mathf.Max(0f, table.width - 12f);
            UIComponents.Label(new Rect(table.x + 6f, table.yMax - footerHeight, footerWidth, footerHeight),
                UIComponents.TruncateToWidth(
                    "PersonalChronicle.UI.Archive.Item.Gap".Translate().ToString(), footerWidth, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
        }

        // ---- 物品 pane 右列：真实字段清单 + 如实缺口（预览 HTML 1246-1257）----------------

        /// <summary>
        /// 字段**标识符**（代码标识符，按预览原样显示，**不翻译** —— GOV-010 约束的是可翻译文案，
        /// 而这里展示的是 `Domain/ThingObject` 的字段名本身）。右列为对应的人话说明（Keyed）。
        /// </summary>
        private static readonly string[] ThingFieldIdentifiers =
        {
            "ThingDefName", "WeakId", "HolderHistory / HolderRecords", "Decommission",
            "CreatedTick / DestroyedTick"
        };

        private static readonly string[] ThingFieldValueKeys =
        {
            "PersonalChronicle.UI.Archive.Item.Fields.ThingDefName",
            "PersonalChronicle.UI.Archive.Item.Fields.WeakId",
            "PersonalChronicle.UI.Archive.Item.Fields.HolderHistory",
            "PersonalChronicle.UI.Archive.Item.Fields.Decommission",
            "PersonalChronicle.UI.Archive.Item.Fields.Ticks"
        };

        /// <summary>
        /// 右列整体（实测行高：`rule-lbl` 18 · `drow` **21** · 说明行 18）——
        /// 字段清单 → 如实缺口 → Tab 序说明。全部为**静态说明文案**，与数据无关。
        /// </summary>
        private static void DrawThingFieldColumn(Rect rect)
        {
            if (rect.width < 160f || rect.height < 80f)
            {
                return;
            }
            float y = rect.y;
            y = DrawThingInfoLabel(rect, y, "PersonalChronicle.UI.Archive.Item.Fields.Title");
            for (int i = 0; i < ThingFieldIdentifiers.Length; i++)
            {
                y = DrawThingInfoRow(rect, y, ThingFieldIdentifiers[i], ThingFieldValueKeys[i],
                    i == ThingFieldIdentifiers.Length - 1);
            }
            y += 6f;
            y = DrawThingInfoLabel(rect, y, "PersonalChronicle.UI.Archive.Item.Gaps.Title");
            y = DrawThingInfoLine(rect, y, "PersonalChronicle.UI.Archive.Item.Gaps.Body1");
            DrawThingInfoLine(rect, y, "PersonalChronicle.UI.Archive.Item.Gaps.Body2");
        }

        /// <summary>`.rule-lbl`（预览：短横线 + dim 小字）—— 实机以 1px 实线替代渐变线。</summary>
        private static float DrawThingInfoLabel(Rect rect, float y, string key)
        {
            UIComponents.Rule(new Rect(rect.x, y + 8f, 14f, UITheme.RuleHeight), UITheme.BorderSoft);
            float textW = Mathf.Max(0f, rect.width - 18f);
            UIComponents.Label(new Rect(rect.x + 18f, y, textW, 18f),
                UIComponents.TruncateToWidth(key.Translate().ToString(), textW, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            return y + 18f;
        }

        /// <summary>`.drow`（键左 dim / 值右 text；行高 21）—— 末行（刻意不持久化）用 Warn 色标记。</summary>
        private static float DrawThingInfoRow(Rect rect, float y, string identifier, string valueKey, bool warn)
        {
            float idW = Mathf.Min(rect.width * 0.45f, 190f);
            float valW = Mathf.Max(0f, rect.width - idW - 6f);
            UIComponents.Label(new Rect(rect.x, y, idW, 21f),
                UIComponents.TruncateToWidth(identifier, idW - 2f, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(rect.x + idW, y, valW, 21f),
                UIComponents.TruncateToWidth(valueKey.Translate().ToString(), valW, GameFont.Tiny),
                GameFont.Tiny, warn ? UITheme.Warn : UITheme.Text, TextAnchor.MiddleLeft);
            return y + 21f;
        }

        /// <summary>说明行（缺口正文；每行 18，截断时悬停给全文 —— LAND-008）。</summary>
        private static float DrawThingInfoLine(Rect rect, float y, string key)
        {
            string text = key.Translate().ToString();
            string shown = UIComponents.TruncateToWidth(text, rect.width, GameFont.Tiny);
            UIComponents.Label(new Rect(rect.x, y, rect.width, 18f), shown,
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            if (Mouse.IsOver(rect) && !string.Equals(shown, text, System.StringComparison.Ordinal))
            {
                TooltipHandler.TipRegion(new Rect(rect.x, y, rect.width, 18f), text);
            }
            return y + 18f;
        }

        private void DrawEmbeddedThingHeader(Rect rect)
        {
            string[] keys =
            {
                "PersonalChronicle.UI.Archive.Item.Col.WeakId",
                "PersonalChronicle.UI.Archive.Item.Col.Thing",
                "PersonalChronicle.UI.Archive.Item.Col.LastHolder",
                "PersonalChronicle.UI.Archive.Item.Col.HolderGens",
                "PersonalChronicle.UI.Archive.Item.Col.Kills",
                "PersonalChronicle.UI.Archive.Item.Col.HolderState",
                "PersonalChronicle.UI.Archive.Item.Col.Retired"
            };
            float x = rect.x;
            for (int i = 0; i < EmbeddedThingColumnCount; i++)
            {
                Rect cell = new Rect(x, rect.y, embeddedThingColumnWidths[i], rect.height);
                float width = Mathf.Max(0f, embeddedThingColumnWidths[i] - 6f);
                bool active = embeddedThingSortColumn == i;
                if (Mouse.IsOver(cell))
                {
                    Widgets.DrawHighlight(cell);
                }
                string text = UIComponents.TruncateToWidth(keys[i].Translate().ToString(),
                    Mathf.Max(0f, width - 8f), GameFont.Tiny);
                if (active)
                {
                    text = (embeddedThingSortDescending ? "▼ " : "▲ ") + text;
                }
                UIComponents.Label(new Rect(x + 3f, rect.y, width, rect.height), text,
                    GameFont.Tiny, active ? UITheme.Text : UITheme.Dim, TextAnchor.MiddleLeft);
                if (Widgets.ButtonInvisible(cell))
                {
                    if (embeddedThingSortColumn == i)
                    {
                        embeddedThingSortDescending = !embeddedThingSortDescending;
                    }
                    else
                    {
                        embeddedThingSortColumn = i;
                        embeddedThingSortDescending = true;
                    }
                    embeddedThingRowsRevision = -1L;
                }
                x += embeddedThingColumnWidths[i];
            }
        }

        /// <summary>物品表搜索框（与人物表同规：输入即过滤，过滤在构建期做）。</summary>
        private float DrawEmbeddedThingSearch(Rect rect)
        {
            Rect box = new Rect(rect.x, rect.y, Mathf.Min(240f, rect.width), rect.height);
            string typed = Widgets.TextField(box, embeddedThingSearch);
            if (!string.Equals(typed, embeddedThingSearch, System.StringComparison.Ordinal))
            {
                embeddedThingSearch = typed ?? string.Empty;
                embeddedThingRowsRevision = -1L;
            }
            if (string.IsNullOrEmpty(embeddedThingSearch))
            {
                float hintWidth = Mathf.Max(0f, box.width - 10f);
                UIComponents.Label(new Rect(box.x + 5f, box.y, hintWidth, box.height),
                    UIComponents.TruncateToWidth(
                        "PersonalChronicle.UI.Archive.Table.SearchHint".Translate().ToString(),
                        hintWidth, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);
            }
            else
            {
                UIComponents.Label(new Rect(box.xMax + 6f, box.y,
                    Mathf.Max(0f, rect.xMax - box.xMax - 6f), box.height),
                    embeddedThingRows.Count.ToString(), GameFont.Tiny, UITheme.Accent, TextAnchor.MiddleLeft);
            }
            return rect.y + rect.height + 4f;
        }

        private void DrawEmbeddedThingRows(Rect rect, float viewTop, float viewBottom)
        {
            // 可见区窗口化（PERF P1 修复；与人物表同规）：只画滚动视口内的行（±1 行缓冲）。
            int first = 0;
            int last = embeddedThingRows.Count - 1;
            if (rect.height > 0f && viewBottom > viewTop)
            {
                first = Mathf.Max(0,
                    Mathf.FloorToInt((viewTop - rect.y) / EmbeddedThingRowHeight) - 1);
                last = Mathf.Min(last,
                    Mathf.CeilToInt((viewBottom - rect.y) / EmbeddedThingRowHeight) + 1);
            }
            for (int i = first; i <= last; i++)
            {
                Rect row = new Rect(rect.x, rect.y + i * EmbeddedThingRowHeight, rect.width, EmbeddedThingRowHeight);
                if (row.yMax > rect.yMax + 0.5f)
                {
                    break;
                }
                EmbeddedThingRow data = embeddedThingRows[i];
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
                for (int c = 0; c < EmbeddedThingColumnCount; c++)
                {
                    float width = embeddedThingColumnWidths[c] - 6f;
                    Color color = c == 6
                        ? EmbeddedThingStateColor(data.State)
                        : (c == 5 ? EmbeddedHolderStateColor(data.HolderStateCode) : UITheme.Text);
                    TextAnchor anchor = EmbeddedThingRightAligned[c]
                        ? TextAnchor.MiddleRight
                        : TextAnchor.MiddleLeft;
                    UIComponents.Label(new Rect(x + 3f, row.y, width, row.height),
                        UIComponents.TruncateToWidth(EmbeddedThingCellValue(data, c), width, GameFont.Tiny),
                        GameFont.Tiny, color, anchor);
                    x += embeddedThingColumnWidths[c];
                }
                if (Widgets.ButtonInvisible(row) && !string.IsNullOrEmpty(data.StableId))
                {
                    // 物品行点击 = 打开该物品的档案（与既有物品卡片同一下钻路径）。
                    RequestWeaponDetail(data.StableId);
                }
            }
        }

        private static string EmbeddedThingCellValue(EmbeddedThingRow row, int column)
        {
            switch (column)
            {
                case 0: return row.WeakId;
                case 1: return row.Thing;
                case 2: return row.LastHolder;
                case 3: return row.HolderGens;
                case 4: return row.Kills;
                case 5: return row.HolderState;
                default: return row.Retired;
            }
        }

        private static Color EmbeddedThingStateColor(byte state)
        {
            switch (state)
            {
                case 2: return UITheme.Dead;    // 已毁
                case 1: return UITheme.Muted;   // 已退役
                default: return UITheme.Accent; // 在用
            }
        }

        /// <summary>「当前持有」列配色：**按状态码**（1 在世 = Alive 色），不比对翻译文本（GOV-009）。</summary>
        private static Color EmbeddedHolderStateColor(byte holderStateCode)
        {
            return holderStateCode == 1 ? UITheme.Alive : UITheme.Dim;
        }

        private void DrawEmbeddedThingColumnWidths(float width)
        {
            if (embeddedThingColumnWidths == null || embeddedThingColumnWidths.Length < EmbeddedThingColumnCount)
            {
                embeddedThingColumnWidths = new float[EmbeddedThingColumnCount];
            }
            for (int i = 0; i < EmbeddedThingColumnCount; i++)
            {
                embeddedThingColumnWidths[i] = width * EmbeddedThingColumnRatios[i];
            }
        }

        private void EnsureEmbeddedThingRows(List<ArchiveObject> objects, IArchiveService service)
        {
            long revision = service != null ? service.GetDataRevision() : 0L;
            string queryKey = embeddedThingSortColumn + "|" + (embeddedThingSortDescending ? "d" : "a")
                + "|" + embeddedThingSearch;
            if (embeddedThingRowsRevision == revision
                && string.Equals(embeddedThingQueryKey, queryKey, System.StringComparison.Ordinal))
            {
                return;
            }
            embeddedThingRowsRevision = revision;
            embeddedThingQueryKey = queryKey;
            embeddedThingRows.Clear();
            for (int i = 0; i < objects.Count; i++)
            {
                ThingObject thing = objects[i] as ThingObject;
                if (thing == null)
                {
                    continue;
                }
                embeddedThingRows.Add(BuildEmbeddedThingRow(thing, service));
            }
            ApplyThingSearchAndSort();
        }

        // ---- 搜索 / 排序（与人物表同规：过滤与排序都在**构建期**做）----

        private string embeddedThingQueryKey = string.Empty;
        private int embeddedThingSortColumn = -1;
        private bool embeddedThingSortDescending;
        private string embeddedThingSearch = string.Empty;

        private void ApplyThingSearchAndSort()
        {
            // 纯空白 = 不过滤（与人物表同规）
            string needle = embeddedThingSearch != null ? embeddedThingSearch.Trim() : string.Empty;
            if (needle.Length > 0)
            {
                for (int i = embeddedThingRows.Count - 1; i >= 0; i--)
                {
                    if (!MatchesThingSearch(embeddedThingRows[i], needle))
                    {
                        embeddedThingRows.RemoveAt(i);
                    }
                }
            }
            if (embeddedThingSortColumn >= 0 && embeddedThingRows.Count > 1)
            {
                embeddedThingRows.Sort(CompareThingRows);
            }
        }

        private static bool MatchesThingSearch(EmbeddedThingRow row, string needle)
        {
            for (int c = 0; c < EmbeddedThingColumnCount; c++)
            {
                string value = EmbeddedThingCellValue(row, c);
                if (!string.IsNullOrEmpty(value)
                    && value.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private int CompareThingRows(EmbeddedThingRow a, EmbeddedThingRow b)
        {
            if (embeddedThingSortColumn == 3)
            {
                int order = a.HolderGensValue.CompareTo(b.HolderGensValue);
                return embeddedThingSortDescending ? -order : order;
            }
            int textOrder = string.CompareOrdinal(
                EmbeddedThingCellValue(a, embeddedThingSortColumn),
                EmbeddedThingCellValue(b, embeddedThingSortColumn));
            return embeddedThingSortDescending ? -textOrder : textOrder;
        }

        private static EmbeddedThingRow BuildEmbeddedThingRow(ThingObject thing, IArchiveService service)
        {
            bool retired = thing.Decommission != null && !thing.Decommission.IsEmpty;
            bool destroyed = thing.DestroyedTick > 0L;
            byte state = destroyed ? (byte)2 : (retired ? (byte)1 : (byte)0);

            string lastHolder = NullCell();
            if (thing.Decommission != null && !string.IsNullOrEmpty(thing.Decommission.LastHolderLabel))
            {
                lastHolder = thing.Decommission.LastHolderLabel;
            }
            else if (thing.HolderRecords != null && thing.HolderRecords.Count > 0)
            {
                HolderRecord last = thing.HolderRecords[thing.HolderRecords.Count - 1];
                if (last != null && !string.IsNullOrEmpty(last.LabelSnapshot))
                {
                    lastHolder = last.LabelSnapshot;
                }
            }

            string holderState = DashText();
            byte holderStateCode = 0;
            if (!string.IsNullOrEmpty(thing.CurrentHolderId))
            {
                Pawn live = service != null ? service.GetLivePawn(thing.CurrentHolderId) : null;
                if (live != null)
                {
                    holderState = AliveStateText;
                    holderStateCode = 1;
                }
                else
                {
                    holderState = ArchivedStateText;
                    holderStateCode = 2;
                }
            }

            EmbeddedThingRow result = new EmbeddedThingRow
            {
                StableId = thing.StableId ?? string.Empty,
                WeakId = string.IsNullOrEmpty(thing.WeakId) ? DashText() : thing.WeakId,
                Thing = ThingLabel(thing.ThingDefName),
                LastHolder = lastHolder,
                HolderGens = thing.HolderRecords != null ? thing.HolderRecords.Count.ToString() : "0",
                HolderGensValue = thing.HolderRecords != null ? thing.HolderRecords.Count : 0,
                // 击杀列 = 如实缺口（需按武器筛选的事件索引）⇒ 画「—」，不填 0 冒充「无击杀」。
                Kills = DashText(),
                HolderState = holderState,
                HolderStateCode = holderStateCode,
                Retired = retired ? RetiredText : DashText(),
                State = state
            };
            result.Tooltip = JoinedThingTooltip(result);
            return result;
        }

        /// <summary>整行全文（构建期一次拼好）：表格单元格被截断时，悬停行即可读到全部列值。</summary>
        private static string JoinedThingTooltip(EmbeddedThingRow row)
        {
            string text = string.Empty;
            for (int c = 0; c < EmbeddedThingColumnCount; c++)
            {
                string value = EmbeddedThingCellValue(row, c);
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

        private static string ThingLabel(string thingDefName)
        {
            if (string.IsNullOrEmpty(thingDefName))
            {
                return NullCell();
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            return def != null ? def.LabelCap.ToString() : thingDefName;
        }

        private static string DashText()
        {
            return "—";
        }

        private static string NullCell()
        {
            return "PersonalChronicle.UI.Population.Roster.Unknown".Translate().ToString();
        }

        private static string AliveStateText
        {
            get { return "PersonalChronicle.UI.PawnFilter.Alive".Translate().ToString(); }
        }

        private static string ArchivedStateText
        {
            get { return "PersonalChronicle.UI.PawnFilter.Archived".Translate().ToString(); }
        }

        private static string RetiredText
        {
            get { return "PersonalChronicle.UI.Archive.Item.Retired".Translate().ToString(); }
        }
    }
}

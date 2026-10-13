using System.Collections.Generic;
using PersonalChronicle.Automation;
using RimWorld;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Archive.UI;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Manufacturing.UI
{
    internal sealed partial class FloorDeviceDetailWindow : Window
    {
        // S1（2026-09-23）：`DrawTimeline`（状态时序条，固定 40 格）已整段删除 ——
        //   40 格只有 1 段真数据（原版无按台历史），绘制出来是"几乎全空的一条" + 常驻说明行 ⇒ 纯噪音；
        //   当前状态已由标题带的状态灯表达。`StateColor` 保留（标题带状态灯与设备卡仍共用它）。


        /// <summary>键值行：宽档两栏，窄档单栏（LAND-005 窄档降级）。</summary>
        /// <param name="blockSourceKind">块级来源（section.SourceKind）。与行来源相同则不重复画
        /// 行徽标 —— S13（2026-09-25 审计）：同来源逐行冒标 ⇒ 右缘堆成一片噪音。</param>
        private float DrawRows(Rect rect, float y, float width, List<FloorDetailRowView> rows, bool wide,
            string benchStableId = null, int blockSourceKind = -1, bool summaryOnly = false)
        {
            if (rows == null || rows.Count == 0)
            {
                return y;
            }
            // CHG-20261002-PRODCOLLAPSE：折叠态（summaryOnly）= 只画**摘要行**（IsSummary 行级声明）；
            //   分栏按可见行数计算 —— 测量侧（MeasureSectionHeight）用同一计数口径，两处不漂移。
            int visibleCount = summaryOnly ? CountSummaryRows(rows) : rows.Count;
            if (visibleCount <= 0)
            {
                return y;
            }
            int columns = wide ? 2 : 1;
            // 预览 `.dsec.on{gap:0 26px}`（HTML:181 / :432 终值）—— CHG-20261002-FLOORDETAIL-AUDIT：
            //   旧值 30f 与预览差 4px（总控复核 D3），此处按预览对齐。
            float columnGap = wide ? 26f : 0f;
            float columnWidth = (width - columnGap * (columns - 1)) / columns;
            int perColumn = (visibleCount + columns - 1) / columns;
            float height = perColumn * RowHeight;

            int drawn = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                FloorDetailRowView row = rows[i];
                if (summaryOnly && !row.IsSummary)
                {
                    continue;
                }
                int column = drawn / perColumn;
                int rowInColumn = drawn % perColumn;
                drawn++;
                float x = rect.x + column * (columnWidth + columnGap);
                float rowY = y + rowInColumn * RowHeight;

                // S1（2026-09-25 审计）：列内统一左内边距，热区同步内缩（命中范围 = 视觉范围）。
                // CHG-20261003-FLOORDETAIL-UNIFY：整行绘制下沉到 `UiAtoms.StatRow`（RUIF 优先 + 手绘降级）。
                //   本方法只负责「把快照行翻译成 RowSpec」+ 「把点击转成动作」；
                //   行高/内边距/标签列比例改由 `UiAtoms` 单一真源提供，与 `MeasureSectionHeight` 同源。
                Rect rowRect = new Rect(x, rowY, columnWidth, RowHeight);
                UiAtoms.RowSpec spec = new UiAtoms.RowSpec
                {
                    Label = row.Label,
                    Value = row.Value,
                    ValueColor = UiAtoms.ToneColor(row.Tone),
                    Bar01 = row.Bar01,
                    BarColor = UiAtoms.ToneColor(row.Tone),
                    // 同来源徽标不逐行重复（实机右缘曾"冒出一片小蓝块"）⇒ 与块级一致时省掉。
                    SourceTag = (row.SourceKind > 0 && row.SourceKind != blockSourceKind)
                        ? row.SourceKind
                        : 0,
                    Pawn = row.PawnThingId > 0
                        ? WorkshopPawnAccess.FindByThingId(Find.CurrentMap, row.PawnThingId)
                        : null,
                    Clickable = row.ActionKind > 0 && !string.IsNullOrEmpty(benchStableId),
                    Tip = row.Label,
                };
                if (UiAtoms.StatRow(rowRect, spec, out bool rowClicked) && rowClicked)
                {
                    ApplyPawnAssign(benchStableId, row.ActionIndex, row.PawnThingId);
                }
            }
            return y + height + 6f;
        }

        /// <summary>品质分布柱（预览 <c>.qh&gt;i</c>：7 档柱 + 标签）。</summary>
        private float DrawQualityChart(Rect rect, float y, float width, FloorDetailSectionView section)
        {
            List<int> histogram = section.QualityHistogram;
            int max = 1;
            for (int i = 0; i < histogram.Count; i++)
            {
                if (histogram[i] > max)
                {
                    max = histogram[i];
                }
            }
            // 预览 `.qh{height:44px}`（HTML:262）—— 常量在窗口侧（测量/绘制唯一真源）
            float chartHeight = QualityChartHeight;
            float cell = width / histogram.Count;
            for (int i = 0; i < histogram.Count; i++)
            {
                float ratio = Mathf.Clamp01(histogram[i] / (float)max);
                float barHeight = 2f + ratio * (chartHeight - 2f);
                Rect bar = new Rect(rect.x + i * cell + cell * 0.2f,
                    y + chartHeight - barHeight, Mathf.Max(2f, cell * 0.6f), barHeight);
                Widgets.DrawBoxSolid(bar, QualityColor(i));
            }
            y += chartHeight + 3f;

            // 标签行（CHG-20261002-FLOORDETAIL-AUDIT：标签行高按 Tiny 真值 —— 旧值 14 < Tiny 行高 18
            //   ⇒ 7 档中文标签（极差/较差/…）下半截被 GUI.Label 按 rect 裁掉）
            float labelLine = Text.LineHeightOf(GameFont.Tiny);
            if (section.QualityLabels != null)
            {
                for (int i = 0; i < section.QualityLabels.Count && i < histogram.Count; i++)
                {
                    Rect label = new Rect(rect.x + i * cell, y, cell, labelLine);
                    UIComponents.Label(label,
                        UIComponents.TruncateToWidth(section.QualityLabels[i], cell, GameFont.Tiny),
                        GameFont.Tiny, UITheme.Dim, TextAnchor.UpperCenter);
                }
            }
            return y + labelLine + 2f;
        }

        // ---- 内容高度（先量后画：滚动判定必须与实际绘制一致）----
    }
}

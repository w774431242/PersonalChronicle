using PersonalChronicle.Archive;
using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;
using ReadModels = PersonalChronicle.Application.ReadModels;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// Partial of PersonnelArchivePanel — 总览分区的绘制与展示格式化。
    /// </summary>
    /// <remarks>
    /// · 职责：绘制人生时间轴、职业工种条形图、足迹台账、里程碑网格、关键事件流、
    ///   地点历史表与详情统计；并提供工时、银两、天数等展示格式化。
    /// · 调用方：PawnDetail 与 Overview 的分区调度。
    /// · 铁律（绘制态配对，勿改）：
    ///   - 改动 GUI.color / Text.Font 的绘制方法必须在进入时保存旧值、退出时（含异常路径）原样恢复。
    ///   - 绘制只消费 Read Model 视图，禁止在此重新聚合、排序或做业务判定。
    /// · 约定（本文件最容易被改坏的两点）：
    ///   - 中文行高下限：Tiny 不低于 18f、Medium 不低于 28f。节点与卡片高度必须用
    ///     Text.CalcHeight 实测的真实行高累加，写死常量会让长中文标题换行后重叠。
    ///   - 所有绘制方法统一返回推进后的 y，调用方以返回值接续排版；
    ///     不要改成 void 后用字段传递纵坐标，那会让排版顺序隐式依赖调用次序。
    ///   - 文案一律走 Translate()，禁止硬编码中文；货币与时长统一走 FormatSilver / FormatWorkHours。
    /// · 待清理：ResolveBackstoryLabel 与 BackstoryDefLabel 功能重叠且取值口径不同
    ///   （一个取 def.title，另一个优先 def.label 再回退 TitleFor），列为 P1 合并项。
    /// </remarks>
    public sealed partial class PersonnelArchivePanel
    {

        /// <summary>
        /// 绘制人生阶段时间轴。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="y">起始纵坐标。</param>
        /// <param name="phases">人生阶段视图；为 null 或空时画空态。</param>
        /// <returns>排版结束后的 y 值。</returns>
        /// <remarks>
        /// 两趟式排版：第一趟用 Text.CalcHeight 按当前字体的真实行高预算每个节点高度，
        /// 第二趟才绘制并据预算值画贯穿的主轴。
        /// 这个预计算是必须的——中文标题会换行，写死节点高度会让长标题与下一节点重叠。
        /// 节点内部的排版规则需与 UIComponents.TimelineNode 保持一致。
        /// </remarks>
        private static float DrawLifeTimeline(Rect rect, float y, IReadOnlyList<ReadModels.LifePhaseView> phases)
        {
            if (phases == null || phases.Count == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoEvents");
            }
            float x = rect.x + 6f;
            float textW = rect.width - 6f - (LifeTimelineNodeSize + 10f);
            // Pre-compute dynamic node heights with real font line-heights so long
            // Chinese titles wrap and never overlap (must match UIComponents.TimelineNode).
            float[] heights = new float[phases.Count];
            float totalH = 0f;
            for (int i = 0; i < phases.Count; i++)
            {
                ReadModels.LifePhaseView p = phases[i];
                float titleH = Text.CalcHeight(p.PhaseKey.Translate().ToString(), textW);
                float block = titleH;
                if (!string.IsNullOrEmpty(p.DateText)) block += Text.CalcHeight(p.DateText, textW) + UITheme.SpaceXxs;
                if (!string.IsNullOrEmpty(p.SubText)) block += Text.CalcHeight(p.SubText, textW) + UITheme.SpaceXxs;
                float h = Mathf.Max(LifeTimelineNodeSize, block) + UITheme.SpaceXs;
                heights[i] = h;
                totalH += h;
            }

            // Vertical spine connecting the centers of all nodes.
            float spineX = x + LifeTimelineNodeSize / 2f;
            float firstCenterY = y + 2f + LifeTimelineNodeSize / 2f;
            float lastCenterY = y + totalH - heights[phases.Count - 1] + 2f + LifeTimelineNodeSize / 2f;
            float spineH = Mathf.Max(0f, lastCenterY - firstCenterY);
            Color prevColor = GUI.color;
            GUI.color = UITheme.TimelineSpine;
            Widgets.DrawLineVertical(spineX, firstCenterY, spineH);
            GUI.color = prevColor;

            float ny = y;
            for (int i = 0; i < phases.Count; i++)
            {
                ReadModels.LifePhaseView p = phases[i];
                Color dot = TimelinePhaseColor(p.Kind);
                ny = UIComponents.TimelineNode(
                    new Rect(x, ny, rect.width - 6f, heights[i]), ny,
                    p.PhaseKey.Translate().ToString(), p.DateText, p.SubText, dot, out _, p.IconKey);
            }
            return y + totalH;
        }

        /// <summary>
        /// 取人生阶段对应的时间轴配色。
        /// </summary>
        /// <param name="kind">阶段种类。</param>
        /// <returns>UITheme 令牌色；未知种类回退到 Info 色。</returns>
        /// <remarks>Origin 与 Join 共用入队色：出身与入队同属"加入"语义，刻意不做区分。</remarks>
        private static Color TimelinePhaseColor(ReadModels.LifePhaseKind kind)
        {
            switch (kind)
            {
                case ReadModels.LifePhaseKind.Origin:
                case ReadModels.LifePhaseKind.Join:
                    return UITheme.TimelineJoin;
                case ReadModels.LifePhaseKind.Death:
                    return UITheme.TimelineDeath;
                case ReadModels.LifePhaseKind.Unknown:
                    return UITheme.Dead;
                default:
                    return UITheme.Info;
            }
        }

        /// <summary>
        /// 绘制职业工种条形图（按工时占比）。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="y">起始纵坐标。</param>
        /// <param name="bars">工种条视图；为 null 或空时画空态。</param>
        /// <returns>排版结束后的 y 值。</returns>
        /// <remarks>
        /// 主/副职业在标签后追加"（主）""（副）"并用强调色区分。
        /// 判定完全来自视图上的 IsPrimary / IsSecondary，本方法不参与任何业务判定。
        /// </remarks>
        private static float DrawCareerBars(Rect rect, float y, IReadOnlyList<ReadModels.CareerBarView> bars)
        {
            if (bars == null || bars.Count == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoWorkData");
            }
            float rowH = 24f;
            for (int i = 0; i < bars.Count; i++)
            {
                ReadModels.CareerBarView b = bars[i];
                string tag = b.IsPrimary ? "PersonalChronicle.UI.CareerPrimary".Translate().ToString()
                    : b.IsSecondary ? "PersonalChronicle.UI.CareerSecondary".Translate().ToString() : "";
                Color tagColor = b.IsPrimary ? UITheme.Accent : UITheme.SecondaryText;
                UIComponents.Label(new Rect(rect.x, y, 150f, 18f),
                    b.WorkTypeLabel + (tag != "" ? " (" + tag + ")" : ""),
                    GameFont.Tiny, tagColor);
                // bar track (width driven by share %)
                float barX = rect.x + 156f;
                float barW = rect.width - 156f - 96f;
                UIComponents.ProgressBar(new Rect(barX, y + (rowH - UITheme.ProgressbarH) / 2f, barW, UITheme.ProgressbarH), b.Share01, UITheme.Accent);
                UIComponents.Label(new Rect(rect.x + rect.width - 92f, y, 90f, 18f),
                    FormatWorkHours(b.Ticks) + " · " + (int)(b.Share01 * 100) + "%",
                    GameFont.Tiny, UITheme.Muted);
                y += rowH;
            }
            return y;
        }

        /// <summary>
        /// 绘制足迹台账：三格汇总卡加最多 6 条停留记录。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="y">起始纵坐标。</param>
        /// <param name="led">足迹台账视图；为 null 或无地点时画空态。</param>
        /// <returns>排版结束后的 y 值。</returns>
        /// <remarks>
        /// Stays 已在 Read Model 侧按停留时长降序排好，此处直接取前 6 条，
        /// 不要再排序——排序职责在提供快照的一侧。
        /// </remarks>
        private static float DrawFootprintLedger(Rect rect, float y, ReadModels.FootprintLedgerView led)
        {
            if (led == null || led.PlaceCount == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoPlaceHistory");
            }
            // summary row
            float cardW = (rect.width - UITheme.GridGap * 2f) / 3f;
            float cardH = UIComponents.StatCellMinHeight;
            UIComponents.StatCell(new Rect(rect.x, y, cardW, cardH),
                "PersonalChronicle.UI.FootprintPlaces".Translate().ToString(), led.PlaceCount.ToString());
            UIComponents.StatCell(new Rect(rect.x + cardW + UITheme.GridGap, y, cardW, cardH),
                "PersonalChronicle.UI.FootprintHome".Translate().ToString() + " · " + led.HomeDays + "d",
                led.HomePlaceText != null ? led.HomePlaceText : "—");
            UIComponents.StatCell(new Rect(rect.x + 2f * (cardW + UITheme.GridGap), y, cardW, cardH),
                "PersonalChronicle.UI.FootprintExpeditions".Translate().ToString(), led.ExpeditionCount.ToString());
            y += cardH + UITheme.GridGap;
            // stays (already sorted longest-first)
            float rowH = 22f;
            int maxRows = Mathf.Min(led.Stays.Count, 6);
            for (int i = 0; i < maxRows; i++)
            {
                ReadModels.FootstepView s = led.Stays[i];
                string icon = s.IsWorldTile ? "🌍" : "🏕️";
                UIComponents.Label(new Rect(rect.x, y, 20f, 18f), icon, GameFont.Small, UITheme.Text);
                UIComponents.Label(new Rect(rect.x + 22f, y, rect.width - 110f, 18f),
                    s.PlaceText + (s.IsHome ? " 〔" + "PersonalChronicle.UI.FootprintHomeTag".Translate().ToString() + "〕" : ""),
                    GameFont.Small, s.IsHome ? UITheme.Accent : UITheme.Text);
                UIComponents.Label(new Rect(rect.x + rect.width - 86f, y, 84f, 18f), s.DwellText,
                    GameFont.Tiny, UITheme.Muted);
                y += rowH;
            }
            return y;
        }

        /// <summary>
        /// 绘制里程碑卡片网格（两列，行高自适应）。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="y">起始纵坐标。</param>
        /// <param name="ms">里程碑视图；为 null 或空时画空态。</param>
        /// <returns>排版结束后的 y 值。</returns>
        /// <remarks>
        /// 第一趟用 Text.CalcHeight 求所有卡片的最大高度并以其统一行高，保证同行卡片等高；
        /// 第二趟才实际绘制。删掉第一趟会让长中文标题溢出卡片边框。
        /// 行数按 (Count + perRow - 1) / perRow 向上取整，改动列数时需同步改 perRow。
        /// </remarks>
        private static float DrawMilestoneGrid(Rect rect, float y, IReadOnlyList<ReadModels.MilestoneView> ms)
        {
            if (ms == null || ms.Count == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoMilestones");
            }
            float cardW = (rect.width - 8f) / 2f;
            int perRow = 2;
            // First pass: compute adaptive card height from real line-heights.
            float maxH = 56f;
            for (int i = 0; i < ms.Count; i++)
            {
                ReadModels.MilestoneView m = ms[i];
                float titleH = Text.CalcHeight(m.TitleText, cardW - 40f);
                float subH = Text.CalcHeight(m.DateText + " · " + m.SubText, cardW - 40f);
                float h = 6f + 20f + 6f + titleH + 2f + subH + 6f;
                if (h > maxH) maxH = h;
            }
            float cardH = maxH;
            for (int i = 0; i < ms.Count; i++)
            {
                ReadModels.MilestoneView m = ms[i];
                float cx = rect.x + (i % perRow) * (cardW + UITheme.GridGap);
                float cy = y + (i / perRow) * (cardH + UITheme.GridGap);
                UIComponents.Card(new Rect(cx, cy, cardW, cardH), UITheme.Border);
                UIComponents.Label(new Rect(cx + UITheme.CardPadX, cy + 6f, 20f, 20f), m.IconKey, GameFont.Medium, UITheme.Text);
                float titleH = Text.CalcHeight(m.TitleText, cardW - 40f);
                UIComponents.Label(new Rect(cx + 32f, cy + 6f, cardW - 40f, titleH), m.TitleText,
                    GameFont.Small, UITheme.Text);
                UIComponents.Label(new Rect(cx + 32f, cy + 6f + 20f + 2f, cardW - 40f,
                        cardH - (6f + 20f + 2f + 6f)),
                    m.DateText + " · " + m.SubText, GameFont.Tiny, UITheme.Muted);
            }
            int rows = (ms.Count + perRow - 1) / perRow;
            return y + rows * (cardH + UITheme.GridGap);
        }

        /// <summary>
        /// 绘制关键事件流（逐行：日期 / 标题 / 类型）。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="y">起始纵坐标。</param>
        /// <param name="evs">关键事件视图；为 null 或空时画空态。</param>
        /// <returns>排版结束后的 y 值。</returns>
        /// <remarks>高亮行（IsHighlight）用强调色竖条并在标题前加星标，
        /// 视觉权重明显高于普通行，用于在长事件流中突出少数关键事件。</remarks>
        private static float DrawKeyEventStream(Rect rect, float y, IReadOnlyList<ReadModels.KeyEventView> evs)
        {
            if (evs == null || evs.Count == 0)
            {
                return DrawEmptyLine(rect, y, "PersonalChronicle.UI.NoEvents");
            }
            for (int i = 0; i < evs.Count; i++)
            {
                ReadModels.KeyEventView e = evs[i];
                Rect row = new Rect(rect.x, y, rect.width, TimelineRowHeight);
                UIComponents.Rule(new Rect(row.x, row.y + 1f, 3f, TimelineRowHeight - 2f),
                    e.IsHighlight ? UITheme.Accent : UITheme.BorderSoft);
                UIComponents.Label(new Rect(row.x + 10f, row.y + 4f, 120f, 18f), e.DateText,
                    GameFont.Tiny, UITheme.Dim);
                UIComponents.Label(new Rect(row.x + 136f, row.y + 4f, row.width - 136f - 70f, 20f),
                    (e.IsHighlight ? "✦ " : "") + e.TitleText, GameFont.Small, UITheme.Text);
                UIComponents.Label(new Rect(row.x + row.width - 66f, row.y + 4f, 64f, 18f), e.TypeText,
                    GameFont.Tiny, UITheme.Muted);
                UIComponents.Rule(new Rect(row.x, row.yMax - 1f, row.width, 1f), UITheme.BorderSoft);
                y += TimelineRowHeight;
            }
            return y;
        }

        /// <summary>
        /// 绘制空态提示行。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="y">起始纵坐标。</param>
        /// <param name="key">空态文案的翻译键。</param>
        /// <returns>y 加 26（行高 22 加间距 4）。</returns>
        /// <remarks>本文件所有"无数据"分支统一走这里，
        /// 保证空态的字体、颜色与间距在整份总览里完全一致。</remarks>
        private static float DrawEmptyLine(Rect rect, float y, string key)
        {
            UIComponents.Label(new Rect(rect.x, y, rect.width, 22f), key.Translate().ToString(),
                GameFont.Small, UITheme.Muted);
            return y + 26f;
        }

        /// <summary>
        /// 绘制地点历史表（地点 / 进入 / 离开），展示最近的若干条。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="y">起始纵坐标。</param>
        /// <param name="pawn">pawn 归档对象。</param>
        /// <param name="maxRows">最多显示的行数。</param>
        /// <returns>排版结束后的 y 值。</returns>
        /// <remarks>
        /// 从列表末尾向前遍历，因此展示顺序是"最近在前"。
        /// 仍在当地（IsOpen）的记录，离开列显示"仍在此地"而不是格式化 LeaveTick。
        /// 空态分支提前 return 前也要恢复 GUI.color，本方法已处理，勿简化掉这一行。
        /// </remarks>
        private float DrawPlaceHistoryTable(Rect rect, float y, PawnObject pawn, int maxRows)
        {
            Color prevColor = GUI.color;
            if (pawn == null || pawn.PlaceHistory == null || pawn.PlaceHistory.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = UITheme.SecondaryText;
                Widgets.Label(new Rect(rect.x, y, rect.width, 18f),
                    "PersonalChronicle.UI.NoPlaceHistory".Translate().ToString());
                GUI.color = prevColor;
                return y + 22f;
            }
            Text.Font = GameFont.Tiny;
            GUI.color = UITheme.SecondaryText;
            Widgets.Label(new Rect(rect.x + 6f, y, rect.width * 0.4f, 16f),
                "PersonalChronicle.UI.PlaceName".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width * 0.42f, y, rect.width * 0.28f, 16f),
                "PersonalChronicle.UI.PlaceEnter".Translate().ToString());
            Widgets.Label(new Rect(rect.x + rect.width * 0.72f, y, rect.width * 0.26f, 16f),
                "PersonalChronicle.UI.PlaceLeave".Translate().ToString());
            GUI.color = prevColor;
            y += 18f;
            int shown = 0;
            for (int i = pawn.PlaceHistory.Count - 1; i >= 0 && shown < maxRows; i--)
            {
                PlaceVisit v = pawn.PlaceHistory[i];
                if (v == null)
                {
                    continue;
                }
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(rect.x + 6f, y, rect.width * 0.4f, 20f), FormatPlaceKey(v));
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(rect.x + rect.width * 0.42f, y + 2f, rect.width * 0.28f, 18f), FormatDate(v.EnterTick));
                string leave = v.IsOpen
                    ? "PersonalChronicle.UI.PlaceStillThere".Translate().ToString()
                    : FormatDate(v.LeaveTick);
                Widgets.Label(new Rect(rect.x + rect.width * 0.72f, y + 2f, rect.width * 0.26f, 18f), leave);
                y += 22f;
                shown++;
            }
            return y;
        }

        /// <summary>
        /// 把地点访问记录的键格式化为可读地名。
        /// </summary>
        /// <param name="v">地点访问记录。</param>
        /// <returns>商队或地块记录返回"世界地块 N"，其余按生物群系 Def 取标签；无键时返回破折号。</returns>
        /// <remarks>判定同时覆盖 KindCaravan 标记与 TileKeyPrefix 前缀两种写法：
        /// 早期存档只写了前缀而未标种类。不要简化成只判一种，否则旧档会显示成生物群系名。</remarks>
        private static string FormatPlaceKey(PlaceVisit v)
        {
            if (v == null || string.IsNullOrEmpty(v.PlaceKey))
            {
                return "—";
            }
            if (v.PlaceKind == PlaceVisitKeys.KindCaravan
                || v.PlaceKey.StartsWith(PlaceVisitKeys.TileKeyPrefix, System.StringComparison.Ordinal))
            {
                string tile = v.PlaceKey.StartsWith(PlaceVisitKeys.TileKeyPrefix, System.StringComparison.Ordinal)
                    ? v.PlaceKey.Substring(PlaceVisitKeys.TileKeyPrefix.Length)
                    : v.PlaceKey;
                return "PersonalChronicle.UI.PlacesWorldTile".Translate(tile).ToString();
            }
            return BiomeLabel(v.PlaceKey);
        }

        /// <summary>
        /// 把 ticks 格式化为小时文本。
        /// </summary>
        /// <param name="ticks">工时 ticks。</param>
        /// <returns>形如 "12.5 h" 的文本（一位小数并带单位）。</returns>
        /// <remarks>换算必须用 GenDate.TicksPerHour，禁止硬编码 2500。</remarks>
        private static string FormatWorkHours(long ticks)
        {
            float hours = ticks / (float)RimWorld.GenDate.TicksPerHour;
            return hours.ToString("0.0") + " h";
        }

        /// <summary>
        /// 把市场价值格式化为带货币单位的展示文本。
        /// </summary>
        /// <param name="value">市场价值。</param>
        /// <returns>套用 MarketValueFormat 翻译键后的文本。</returns>
        /// <remarks>数值缩写由 FormatSilver 负责，本方法只负责套翻译模板；
        /// 不要在调用处自行拼接货币符号，否则无法适配其它语言。</remarks>
        private static string FormatMarketValue(float value)
        {
            return "PersonalChronicle.UI.MarketValueFormat".Translate(FormatSilver(value)).ToString();
        }

        /// <summary>
        /// 把银两数值缩写为 K / M / B 单位。
        /// </summary>
        /// <param name="value">银两数值。</param>
        /// <returns>小于 1000 时返回取整后的原值；否则返回带 K / M / B 后缀的缩写（最多两位小数）。</returns>
        /// <remarks>阈值与后缀属于展示层约定，改动会同时影响生产汇总卡与产物卡，需一并核对。</remarks>
        private static string FormatSilver(float value)
        {
            if (value < 1000f)
            {
                return Mathf.RoundToInt(value).ToString();
            }
            float scaled;
            string suffix;
            if (value >= 1e9f) { scaled = value / 1e9f; suffix = "B"; }
            else if (value >= 1e6f) { scaled = value / 1e6f; suffix = "M"; }
            else { scaled = value / 1e3f; suffix = "K"; }
            return scaled.ToString("0.##") + suffix;
        }

        /// <summary>
        /// 绘制指标卡（标签 / 数值 / 副标签 三段式）。
        /// </summary>
        /// <param name="rect">卡片区域。</param>
        /// <param name="label">顶部小标签。</param>
        /// <param name="value">中部大数值。</param>
        /// <param name="subLabel">底部副标签。</param>
        /// <remarks>
        /// 行高按中文安全值固定：Tiny 18f、Medium 28f，三段合计正好铺满 72f 高的卡片；
        /// 卡片高度不小于 80f 时额外加 4f 段间距。改动这些常量前先确认现有卡片的固定高度。
        /// 绘制态在 finally 中恢复，勿删除 try/finally。
        /// </remarks>
        private static void DrawMetricCard(Rect rect, string label, string value, string subLabel)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            try
            {
                ArchiveUiStyle.DrawCard(rect, ArchiveUiStyle.Info);
                bool large = rect.height >= 80f;
                // CJK-safe line heights: Tiny >= 18f, Medium >= 28f. For a 72f card
                // the block is 8 + 18 + 28 + 18 = 72f exactly.
                float labelH = 18f;
                float valueH = 28f;
                float subLabelH = 18f;
                float labelY = rect.y + UITheme.CardPadY;
                float valueY = labelY + labelH + (large ? 4f : 0f);
                float subLabelY = valueY + valueH + (large ? 4f : 0f);

                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(rect.x + UITheme.CardPadX, labelY, rect.width - UITheme.CardPadX * 2f, labelH), label);
                Text.Font = GameFont.Medium;
                GUI.color = ArchiveUiStyle.Text;
                Widgets.Label(new Rect(rect.x + UITheme.CardPadX, valueY, rect.width - UITheme.CardPadX * 2f, valueH), value);
                Text.Font = GameFont.Tiny;
                GUI.color = ArchiveUiStyle.Muted;
                Widgets.Label(new Rect(rect.x + UITheme.CardPadX, subLabelY, rect.width - UITheme.CardPadX * 2f, subLabelH), subLabel);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
            }
        }

        /// <summary>
        /// 取背景故事的展示文本，童年与成年用箭头连接。
        /// </summary>
        /// <param name="pawn">pawn 归档对象；为 null 时返回空串。</param>
        /// <returns>"童年 → 成年"；只有一侧时返回该侧；两侧都缺失时返回破折号。</returns>
        private static string BackstoryLabel(PawnObject pawn)
        {
            if (pawn == null)
            {
                return string.Empty;
            }
            string child = BackstoryDefLabel(pawn.ChildhoodBackstoryDefName);
            string adult = BackstoryDefLabel(pawn.AdulthoodBackstoryDefName);
            if (string.IsNullOrEmpty(child) && string.IsNullOrEmpty(adult))
            {
                return "—";
            }
            if (string.IsNullOrEmpty(child))
            {
                return adult;
            }
            if (string.IsNullOrEmpty(adult))
            {
                return child;
            }
            return child + " → " + adult;
        }

        /// <summary>
        /// 取背景故事 Def 的显示名。
        /// </summary>
        /// <param name="defName">BackstoryDef 的 defName；为空时返回空串。</param>
        /// <returns>优先 def.label，其次 def.TitleFor(Gender.Male)，最后退到 defName。</returns>
        /// <remarks>多层回退是因为 title 字段在不同版本间的数据结构差异较大，
        /// 只取一层会在部分版本上显示空白。性别固定取 Male 仅为拿到一个通用标题，
        /// 不作为性别呈现。</remarks>
        private static string BackstoryDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            BackstoryDef def = DefDatabase<BackstoryDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                // Prefer Def label; fall back to defName (title fields vary by version).
                if (!string.IsNullOrEmpty(def.label))
                {
                    return def.label;
                }
                string titled = def.TitleFor(Gender.Male);
                if (!string.IsNullOrEmpty(titled))
                {
                    return titled;
                }
            }
            return defName;
        }

        /// <summary>
        /// 绘制详情统计块：事件数、关联对象数、首末事件日期。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <remarks>
        /// 首末事件直接取缓存列表的首尾元素——该列表已按时间升序排好，此处不重新排序。
        /// 无任何事件时首末日期都显示"未知日期"，不要改成显示当前日期。
        /// </remarks>
        private void DrawDetailStats(Rect rect)
        {
            float y = rect.y;
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Stats".Translate().ToString());

            DrawStatCell(new Rect(rect.x, y, 150f, 60f), "PersonalChronicle.UI.EventCount".Translate().ToString(), cachedDetailEvents.Count);
            DrawStatCell(new Rect(rect.x + 160f, y, 150f, 60f), "PersonalChronicle.UI.LinkedCount".Translate().ToString(), cachedLinkedObjects.Count);
            y += 70f;

            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.FirstEvent".Translate().ToString(),
                cachedDetailEvents.Count > 0 ? cachedDetailEvents[0].DateText : "PersonalChronicle.UI.UnknownDate".Translate().ToString());
            y = DrawDetailRow(rect.x, y, rect.width, "PersonalChronicle.UI.LastEvent".Translate().ToString(),
                cachedDetailEvents.Count > 0 ? cachedDetailEvents[cachedDetailEvents.Count - 1].DateText : "PersonalChronicle.UI.UnknownDate".Translate().ToString());
        }

        /// <summary>
        /// 绘制"无实时数据"空态。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <remarks>已归档（死亡或离场）的 pawn 不再有实时技能与健康数据，
        /// 依赖实时数据的分区统一用本方法给出明确文案，而不是显示空白面板。
        /// 本方法会自行保存并恢复 GUI.color，调用方无需额外配对。</remarks>
        private void DrawNoLiveData(Rect rect)
        {
            Color prevColor = GUI.color;
            Text.Font = GameFont.Small;
            GUI.color = UITheme.SecondaryText;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f),
                "PersonalChronicle.UI.NoLiveData".Translate().ToString());
            GUI.color = prevColor;
        }

        /// <summary>
        /// 取背景故事的标题（直接取 def.title）。
        /// </summary>
        /// <param name="backstoryDefName">BackstoryDef 的 defName；为空时返回空串。</param>
        /// <returns>def.title；Def 缺失时返回原始 defName。</returns>
        /// <remarks>
        /// 待清理：与 BackstoryDefLabel 功能重叠，但取值口径不同（本方法取 def.title，
        /// 后者优先 def.label 再回退 TitleFor）。合并前务必确认调用方期望哪一种，
        /// 贸然统一会改变部分界面的显示结果。列为 P1 合并项。
        /// </remarks>
        private static string ResolveBackstoryLabel(string backstoryDefName)
        {
            if (string.IsNullOrEmpty(backstoryDefName)) return "";
            var def = DefDatabase<BackstoryDef>.GetNamedSilentFail(backstoryDefName);
            return def != null ? def.title : backstoryDefName;
        }

        /// <summary>
        /// 把天数格式化为一位小数文本。
        /// </summary>
        /// <param name="days">天数。</param>
        /// <returns>形如 "12.3" 的文本；不带单位，单位由调用处的翻译模板提供。</returns>
        private static string FormatDays(double days)
        {
            return days.ToString("0.0");
        }


    }
}

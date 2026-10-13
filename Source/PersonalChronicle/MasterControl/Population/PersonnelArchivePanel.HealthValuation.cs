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

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>
    /// [ARC-013] <see cref="PersonnelArchivePanel"/> 的「健康估值（Health Valuation）」绘制切片。
    ///
    /// 原 PersonnelArchivePanel.PawnMedal.cs 单文件 805 行，混合「健康估值」「勋章墙」「封面」
    /// 「账本产出」多组职责，违反 [ARC-013] 单一职责与「单文件 ≤ 800 行」。按 08 §3
    /// 「排序→识别→剥离」，本文件剥离其中的健康估值绘制（DrawHealthValuation 及其 hover-tip 约定）。
    ///
    /// 剥离原则（[ARC-013] 零契约改动）：仅物理搬移，类名、方法签名、访问级别、
    /// 命名空间、翻译键一律未改；同 partial 类成员天然互相可见，跨文件调用无需改访问级别。
    /// </summary>
    public sealed partial class PersonnelArchivePanel
    {
        private float DrawHealthValuation(Rect rect, float y, Application.ReadModels.HealthView h)
        {
            // v4.6.1: full HTML-style layout — 4 StatCells + 3 dim bars + 残值折旧曲线 + event log + verdict.
            float headH = 26f;
            float statRowH = 80f;
            float dimRowH = 16f;
            float dimBlockH = dimRowH * 3f + 8f;
            float curveH = 20f + 76f + 18f;
            float eventHeaderH = 18f;
            int evCount = h.Events != null ? Mathf.Min(h.Events.Count, 6) : 0;
            // v4.6.3: 事件行高 22f 适配中文 GameFont.Tiny。
            float eventsH = evCount > 0 ? (evCount * 22f + 4f) : 18f;
            float blockH = headH + statRowH + 8f + dimBlockH + 6f + curveH + 6f
                + eventHeaderH + eventsH + 6f;
            Rect block = new Rect(rect.x, y, rect.width, blockH);

            UIComponents.DrawSubsectionHeader(block.TopPartPixels(headH),
                HealthValuationKeys.Title);

            if (!h.IsDefined)
            {
                Rect empty = new Rect(block.x + UITheme.CardPadX, block.y + headH + UITheme.GridGap, block.width - UITheme.CardPadX * 2f, 28f);
                Color prevColor = GUI.color;
                GameFont prevFont = Text.Font;
                TextAnchor prevAnchor = Text.Anchor;
                GUI.color = UITheme.Dim;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(empty, HealthValuationKeys.NoData.Translate().ToString());
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
                return block.yMax;
            }

            Color accent = h.IsImpaired ? UITheme.Blood : UITheme.Alive;

            // === Row 1: 4 StatCells (silver value / composite score / body% / weekly yield) ===
            float statX = block.x + UITheme.CardPadX;
            float statGap = UITheme.GridGap;
            float statW = (block.width - UITheme.CardPadX * 2f - statGap * 3f) / 4f;
            float statY = block.y + headH + 6f;
            UIComponents.StatCell(new Rect(statX, statY, statW, statRowH),
                HealthValuationKeys.SilverValue.Translate().ToString(),
                FormatSilver(h.SilverValue),
                h.IsImpaired ? UITheme.Blood : UITheme.PillGold,
                HealthValuationKeys.BaseValue.Translate(FormatSilver(h.BaseSilverValue)).ToString());
            UIComponents.StatCell(new Rect(statX + (statW + statGap), statY, statW, statRowH),
                HealthValuationKeys.Score.Translate().ToString(),
                Mathf.RoundToInt(h.HealthScore).ToString(),
                accent);
            UIComponents.StatCell(new Rect(statX + 2f * (statW + statGap), statY, statW, statRowH),
                HealthValuationKeys.Body.Translate().ToString(),
                Mathf.RoundToInt(h.BodyPercent * 100f).ToString() + "%",
                accent);
            UIComponents.StatCell(new Rect(statX + 3f * (statW + statGap), statY, statW, statRowH),
                HealthValuationKeys.WeeklyYield.Translate().ToString(),
                FormatSilver(h.WeeklySilverEstimate),
                UITheme.PillGold,
                "PersonalChronicle.UI.Ledger.WeeklyUnit".Translate().ToString(),
                inlineSubLabel: true);

            // === Row 2: 3 dim bars (Body / Spirit / Youth) ===
            float dimY = statY + statRowH + 8f;
            DrawHealthDimBar(new Rect(statX, dimY, block.width - UITheme.CardPadX * 2f, dimRowH),
                HealthValuationKeys.DimBody.Translate().ToString(),
                h.BodyIntegrityScore, h.BodyFactors, HealthValuationKeys.DimBody);
            DrawHealthDimBar(new Rect(statX, dimY + dimRowH, block.width - UITheme.CardPadX * 2f, dimRowH),
                HealthValuationKeys.DimSpirit.Translate().ToString(),
                h.SpiritScore, h.SpiritFactors, HealthValuationKeys.DimSpirit);
            DrawHealthDimBar(new Rect(statX, dimY + 2f * dimRowH, block.width - UITheme.CardPadX * 2f, dimRowH),
                HealthValuationKeys.DimYouth.Translate().ToString(),
                h.YouthScore, h.YouthFactors, HealthValuationKeys.DimYouth);

            // === Row 3: 残值折旧曲线（v5.x，对照清单 P2-⑥；预览 HTML 1516-1537）===
            float curveY = dimY + dimBlockH + 6f;
            DrawDepreciationCurve(new Rect(statX, curveY, block.width - UITheme.CardPadX * 2f, curveH), h);

            // === Row 4: depreciation event log ===
            float evY = curveY + curveH + 6f;
            UIComponents.Label(new Rect(statX, evY, block.width - UITheme.CardPadX * 2f, eventHeaderH),
                HealthValuationKeys.TipHeader.Translate().ToString(),
                GameFont.Tiny, UITheme.SecondaryText);
            if (evCount == 0)
            {
                UIComponents.Label(new Rect(statX, evY + eventHeaderH, block.width - UITheme.CardPadX * 2f, 18f),
                    HealthValuationKeys.NoEvents.Translate().ToString(),
                    GameFont.Tiny, UITheme.Dim);
            }
            else
            {
                // 中文 GameFont.Tiny 行高 ≈ 22f，用 22f 避免字体重叠截断。
                const float lineH = 22f;
                for (int i = 0; i < evCount; i++)
                {
                    Application.ReadModels.HealthEventView e = h.Events[i];
                    if (e == null) continue;
                    string impact = e.Impact == 0 ? "" : ("  " + FormatSilver(e.Impact) + "PersonalChronicle.UI.SilverUnit".Translate().ToString());
                    string tag = string.IsNullOrEmpty(e.TagText) ? "" : ("  [" + e.TagText + "]");
                    string line = e.Description + impact + tag;
                    if (!string.IsNullOrEmpty(e.DateText))
                    {
                        line = e.DateText + "  " + line;
                    }
                    UIComponents.Label(new Rect(statX, evY + eventHeaderH + i * lineH,
                        block.width - UITheme.CardPadX * 2f, lineH),
                        line,
                        GameFont.Tiny,
                        e.Impact < 0 ? UITheme.Blood : UITheme.Muted,
                        TextAnchor.MiddleLeft);
                }
            }

            // v4.14: closing verdict blurb (health residual conclusion). Rendered
            // below the event log; height grows by one block so the scroll region
            // stays honest.
            if (!string.IsNullOrEmpty(h.VerdictText))
            {
                float verdictH = 34f;
                Rect verdict = new Rect(block.x, block.yMax, block.width, verdictH);
                Color prevColor2 = GUI.color;
                GUI.color = UITheme.PanelRaised;
                Widgets.DrawBoxSolid(verdict, UITheme.PanelRaised);
                GUI.color = prevColor2;
                UIComponents.Border(verdict, UITheme.BorderSoft);
                UIComponents.Label(new Rect(verdict.x + UITheme.CardPadX, verdict.y + 6f,
                    verdict.width - UITheme.CardPadX * 2f, 20f),
                    HealthValuationKeys.Verdict.Translate().ToString()
                        + " " + h.VerdictText,
                    GameFont.Tiny,
                    h.IsImpaired ? UITheme.Blood : (h.IsPrime ? UITheme.Alive : UITheme.Text),
                    TextAnchor.UpperLeft);
                return verdict.yMax;
            }

            // Per-element hover tips are handled by DrawHealthDimBar (factors) and
            // the event rows below. Do NOT register a whole-block tooltip here, or
            // it swallows the per-dimension tooltips.
            return block.yMax;
        }

        /// <summary>残值轨迹序列（缓存构建期算好；绘制路径零遍历）。</summary>
        private float[] cachedDepreciationSeries = System.Array.Empty<float>();

        /// <summary>
        /// 由折旧事件**反推**的残值轨迹（预览 HTML 1516-1537 的残值曲线）：
        /// 末点 = 当前残值，向前逐事件回加影响 ⇒ 值 i = 当前 − Σ(影响 j | j &gt; i)。
        /// 预览那条 polyline 是**手写假数据**，此处必须用真实事件序列；
        /// 事件 &lt; 2 条或无起病 tick ⇒ 返回空数组 ⇒ 如实画空态（LAND-009），不编造曲线。
        /// </summary>
        private static float[] BuildDepreciationSeries(Application.ReadModels.HealthView h)
        {
            if (h == null || !h.IsDefined || h.Events == null)
            {
                return System.Array.Empty<float>();
            }
            List<Application.ReadModels.HealthEventView> dated =
                new List<Application.ReadModels.HealthEventView>();
            for (int i = 0; i < h.Events.Count; i++)
            {
                Application.ReadModels.HealthEventView e = h.Events[i];
                if (e != null && e.RawTick > 0L && e.Impact != 0)
                {
                    dated.Add(e);
                }
            }
            if (dated.Count < 2)
            {
                return System.Array.Empty<float>();
            }
            dated.Sort((a, b) => a.RawTick.CompareTo(b.RawTick));
            const int maxPoints = 12;
            if (dated.Count > maxPoints - 1)
            {
                dated.RemoveRange(0, dated.Count - (maxPoints - 1));
            }
            float[] series = new float[dated.Count + 1];
            float value = h.SilverValue;
            series[dated.Count] = Mathf.Max(0f, value);
            for (int i = dated.Count - 1; i >= 0; i--)
            {
                value -= dated[i].Impact;
                series[i] = Mathf.Max(0f, value);
            }
            return series;
        }

        /// <summary>
        /// 残值折旧块（标题行 + 轨迹图 + 口径行）。空态画空槽而不是假曲线。
        /// </summary>
        private void DrawDepreciationCurve(Rect rect, Application.ReadModels.HealthView h)
        {
            float headerH = 20f;
            UIComponents.Label(new Rect(rect.x, rect.y, 86f, headerH),
                "PersonalChronicle.UI.HealthValuation.Curve.Title".Translate().ToString(),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleLeft);

            Text.Font = GameFont.Small;
            string silverText = FormatSilver(h.SilverValue);
            float valueW = Text.CalcSize(silverText).x;
            UIComponents.Label(new Rect(rect.x + 88f, rect.y, valueW + 4f, headerH),
                silverText, GameFont.Small, UITheme.Warn, TextAnchor.MiddleLeft);

            int delta = h.SilverValue - h.BaseSilverValue;
            float deltaX = rect.x + 88f + valueW + 8f;
            float deltaW = Mathf.Max(0f, rect.xMax - 122f - deltaX);
            if (delta != 0 && deltaW > 40f)
            {
                string deltaText = "PersonalChronicle.UI.HealthValuation.Curve.Delta".Translate(
                    (NamedArgument)FormatSilver(Mathf.Abs(delta)),
                    (NamedArgument)FormatSilver(h.BaseSilverValue)).ToString();
                UIComponents.Label(new Rect(deltaX, rect.y, deltaW, headerH),
                    UIComponents.TruncateToWidth(deltaText, deltaW, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Blood, TextAnchor.MiddleLeft);
            }
            UIComponents.Label(new Rect(rect.xMax - 118f, rect.y, 118f, headerH),
                "PersonalChronicle.UI.HealthValuation.Curve.Legend".Translate().ToString(),
                GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleRight);

            const float plotH = 76f;
            Rect plot = new Rect(rect.x, rect.y + headerH, rect.width, plotH);
            if (cachedDepreciationSeries != null && cachedDepreciationSeries.Length >= 2)
            {
                RuifBridge.TryLineChart(plot, cachedDepreciationSeries, true, true);
            }
            else
            {
                Widgets.DrawBoxSolid(plot, UITheme.Window);
                UIComponents.Border(plot, UITheme.BorderSoft);
                UIComponents.Label(plot,
                    HealthValuationKeys.NoEvents.Translate().ToString(),
                    GameFont.Tiny, UITheme.Dim, TextAnchor.MiddleCenter);
            }

            int evCount = h.Events != null ? h.Events.Count : 0;
            UIComponents.Label(new Rect(rect.x, plot.yMax + 1f, rect.width, 16f),
                "PersonalChronicle.UI.HealthValuation.Curve.Caption".Translate(
                    (NamedArgument)evCount).ToString(),
                GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);
        }
    }
}

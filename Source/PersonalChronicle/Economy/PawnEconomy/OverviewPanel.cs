using System;
using System.Collections.Generic;
using System.Text;
using PersonalChronicle.Application.PawnEconomy.Finance;
using PersonalChronicle.Domain.PawnEconomy.Care;
using PersonalChronicle.Domain.PawnEconomy.Learning;
using PersonalChronicle.Domain.PawnEconomy.Production;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>总览页会话态（滚动 / 卡快照 / 门控）—— 由宿主持有，不入存档。
    /// CHG-20261010-ECON-AGG-MIGRATE 批次 A-3：宿主无关化。</summary>
    internal sealed class OverviewPanelState
    {
        public Vector2 Scroll;
        public int Tick = -99999;
        public object Language;
        public List<OverviewPanel.Card> Cards;
        public string Note;
        public bool Empty;
        public float ContentHeight;
        public void Invalidate() { Tick = -99999; }
    }

    /// <summary>
    /// 总览页**宿主无关内容层**（B7 overview tab）：一页回答"经济现在在做什么"，
    /// 数据全部来自其它页共用的**同一批已发布读模型** —— 机构与其报价、雇佣与应付、照护授权与服务能力、
    /// 以及由储备背书的国库。**本层不计算第二份真相，也从不写任何状态。**
    ///
    /// <para><b>回归纪律（P1-2 既有）</b>：14 类查询的**装配与 60 tick 门控逐条照搬**，
    /// 只把"行文本"重组为分组指标卡；搬迁不得改变查询序列。</para>
    ///
    /// <para><b>宿主边界</b>：只经参数接收 <c>runtime</c> / <c>map</c> / <c>rect</c> / <c>state</c>；
    /// 跨页工具一律调共享实现（<c>EconomyLabels.*</c>、<c>CarePanel.*</c>、<c>EconomyRowList.CardSideInset/CardStackGap</c>）。</para>
    /// </summary>
    internal static class OverviewPanel
    {
        private static string O(string suffix) => EconomyLabels.TranslatedOrId("PC_PEA.Overview." + suffix, suffix);
        private static string NeedsLabel(string suffix) => EconomyLabels.TranslatedOrId("PC_PEA.Needs." + suffix, suffix);

        // ── 分组指标卡（快照期构建；绘制路径只读、不测量）────────────────────
        /// <summary>一条指标：左标签 / 右值（值色可带语义）。</summary>
        internal sealed class Metric
        {
            public string Label;
            public string Value;
            public Color Tone;
            public string Tooltip;
        }
        /// <summary>一张组卡：标题 + 指标行；高度与标签列宽在构建期算好（绘制期零测量）。</summary>
        internal sealed class Card
        {
            public string Title;
            public List<Metric> Metrics;
            public float Height;
            public float LabelColumnWidth;
        }

        private const float EmptyHeight = 72f;

        /// <summary>绘制整页（标题 + 分组卡 / 空态 + 页尾说明）。</summary>
        public static void Draw(GameComponent_PawnEconomy runtime, Map map, Rect rect, OverviewPanelState state)
        {
            if (runtime == null || map == null)
            { RUIF.FeedbackAtoms.EmptyState(rect, NeedsLabel("Unavailable"), NeedsLabel("UnavailableHint")); return; }
            // ⚠ 原点纪律（GATE-ARCH 2026-10-10 P1-B2）：外层矩形相对传入 `rect` 定位（同 CarePanel）。
            RUIF.Draw.Label(new Rect(rect.x, rect.y, rect.width - 40, 32), O("Title"), GameFont.Medium, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            int now = Find.TickManager?.TicksGame ?? 0;
            EnsureSnapshot(runtime, map, state, now);
            float bodyTop = 40f;
            Rect body = new Rect(rect.x, rect.y + bodyTop, rect.width, Mathf.Max(60f, rect.height - bodyTop - 8f));
            float extent = Mathf.Max(body.height, state.ContentHeight);
            Rect content = RUIF.Layout.BeginScroll(body, extent, ref state.Scroll, false);
            try
            {
                float y = 4f;
                if (state.Empty)
                {
                    RUIF.FeedbackAtoms.EmptyState(new Rect(content.x + EconomyRowList.CardSideInset, y,
                            content.width - EconomyRowList.CardSideInset * 2f,
                            Mathf.Max(EmptyHeight, body.height - RUIF.Tokens.SpaceLg)),
                        EconomyLabels.TranslatedOrId(EconomyWindowLayout.EmptyStateTitleKey, "Empty"),
                        EconomyLabels.TranslatedOrId(EconomyWindowLayout.EmptyStateHintKey, "EmptyHint"));
                }
                else
                {
                    if (state.Cards != null)
                        for (int i = 0; i < state.Cards.Count; i++)
                        {
                            DrawCard(content, y, state.Cards[i]);
                            y += state.Cards[i].Height + EconomyRowList.CardStackGap;
                        }
                    if (!string.IsNullOrEmpty(state.Note))
                    {
                        var note = new Rect(content.x + EconomyRowList.CardSideInset, y,
                            content.width - EconomyRowList.CardSideInset * 2f, RUIF.Measure.RowHeight(GameFont.Small));
                        RUIF.Draw.EllipsisLabel(note, state.Note, GameFont.Small, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
                        if (Mouse.IsOver(note)) TooltipHandler.TipRegion(note, state.Note);
                    }
                }
            }
            catch (Exception error) { Log.ErrorOnce("[PC][UI] overview rows failed: " + error, 0x1EC0DE3); }
            finally { RUIF.Layout.EndScroll(); }
            RUIF.MiscAtoms.ThinScrollBar(body, extent, state.Scroll.y);
        }

        /// <summary>与其它页同一 60 tick 节拍；查询调用序列与原实现**逐条一致**。</summary>
        private static void EnsureSnapshot(GameComponent_PawnEconomy runtime, Map map, OverviewPanelState state, int now)
        {
            if (state.Cards != null && now - state.Tick < 60 && state.Tick != -99999
                && ReferenceEquals(state.Language, LanguageDatabase.activeLanguage)) return;
            state.Tick = now;
            state.Language = LanguageDatabase.activeLanguage;
            var cards = new List<Card>();

            // ── 制度（机构 / 服务能力）──────────────────────────────────────────
            // B2（机构政策净删）：本卡只保留**政策之外仍然成立**的读数 —— 机构数与五类照护服务能力。
            // 「营业 / 工资报价 / 食品报价 / 服务报价」四项全部来自政策，随政策退役（继续显示就得发明数据，
            // M5：未知不当零）。机构数改数**建筑**（旧的 policies.Count 数的是政策记录，不是机构）。
            var institutionsCard = new Card { Title = O("Institutions"), Metrics = new List<Metric>() };
            AddMetric(institutionsCard, O("Institutions"), runtime.QueryInstitutions(map.uniqueID).Count.ToString());
            var kinds = new[] { ServiceKind.Examination, ServiceKind.Bed, ServiceKind.Treatment, ServiceKind.Childcare, ServiceKind.Education };
            for (int i = 0; i < kinds.Length; i++)
            {
                var capability = runtime.QueryServiceCapability(kinds[i]);
                string value = CarePanel.CapabilityLabel(capability?.Capability ?? ServiceCapability.PublicVanilla);
                if (capability != null && capability.Blockers.Count > 0)
                    value += " (" + O("Unproven") + ": " + capability.Blockers.Count.ToString() + ")";
                AddMetric(institutionsCard, CarePanel.ServiceLabel(kinds[i]), value);
            }
            cards.Add(institutionsCard);

            // ── 资金（国库 / 旧账对账）──────────────────────────────────────────
            var treasuryCard = new Card { Title = O("Treasury"), Metrics = new List<Metric>() };
            long? treasury = runtime.QueryTreasuryAvailableMinor();
            AddMetric(treasuryCard, O("Treasury"), treasury.HasValue ? EconomyLabels.FormatMinor(treasury.Value) : O("Unknown"),
                treasury.HasValue ? (Color?)null : RUIF.Tokens.Dim);
            // U6-A6：报告自述 BackingVerified => false ⇒ 如实显示「背书未证」，不得显示成已核验。
            try
            {
                var report = runtime.QueryLegacyReconciliation();
                if (report == null) AddMetric(treasuryCard, O("Reconciliation"), O("Unknown"), RUIF.Tokens.Dim);
                else
                {
                    AddMetric(treasuryCard, O("Reconciliation"), report.SourceCount.ToString() + O("RecordCount"));
                    AddMetric(treasuryCard, O("Diagnostics"),
                        report.DiagnosticChecksPassed ? O("Passed") : O("Issues") + ":" + report.Issues.Count.ToString(),
                        report.DiagnosticChecksPassed ? (Color?)null : RUIF.Tokens.Warn);
                    AddMetric(treasuryCard, O("UnpaidWages"), report.UnpaidWagesMinor.ToString("0.##"));
                    AddMetric(treasuryCard, O("BackingUnverified"), string.Empty, RUIF.Tokens.Warn);
                    if (!string.IsNullOrEmpty(report.Fault)) AddMetric(treasuryCard, O("Diagnostics"), report.Fault, RUIF.Tokens.Dead);
                }
            }
            catch (InvalidOperationException) { AddMetric(treasuryCard, O("Reconciliation"), O("Unknown"), RUIF.Tokens.Dim); }
            cards.Add(treasuryCard);

            // ── 劳动（合同 / 照护）──────────────────────────────────────────────
            // B2：整块「车间区工资报价」随政策退役（工资改由**合同条款**承载 —— 见工作合同表/弹窗）。
            var contractsCard = new Card { Title = O("Contracts"), Metrics = new List<Metric>() };
            AddMetric(contractsCard, O("Contracts"), runtime.QueryContractCount.ToString());
            AddMetric(contractsCard, O("CareGrants"), runtime.CareAuthorizationCount.ToString());
            cards.Add(contractsCard);

            // ── 生产（读数 / 补货输入 / 补货评估 / 车间）────────────────────────
            var productionCard = new Card { Title = O("Production"), Metrics = new List<Metric>() };
            var categories = runtime.QueryProducedCategoryKeys(map);
            var readouts = runtime.QueryProductionReadouts(map, categories, out bool readoutsComplete);
            long stockTotal = 0, wipTotal = 0; int readableCategories = 0;
            for (int i = 0; i < readouts.Count; i++)
            {
                if (!readouts[i].AvailableStock.TryGetValue(out long stock)) continue;
                if (!readouts[i].CompletableWorkInProgress.TryGetValue(out long wip)) continue;
                stockTotal += stock; wipTotal += wip; readableCategories++;
            }
            AddMetric(productionCard, O("Categories"),
                readableCategories.ToString() + (readoutsComplete ? string.Empty : " (" + O("Unknown") + ")"));
            AddMetric(productionCard, O("Stock"), stockTotal.ToString());
            AddMetric(productionCard, O("Wip"), wipTotal.ToString());
            AddMetric(productionCard, O("Wired"),
                O("Stock") + "·" + O("Wip") + "·" + O("Authorised") + "·" + O("StockTarget"));
            AddMetric(productionCard, O("NotWired"), O("NotWiredInputs"), RUIF.Tokens.Dim);
            var restockRows = runtime.QueryRestockEvaluations(map, categories, out int restockTotal, out _);
            if (restockRows.Count == 0) AddMetric(productionCard, O("Restock"), O("RestockUnavailable"), RUIF.Tokens.Dim);
            else
            {
                var restock = new StringBuilder();
                for (int i = 0; i < restockRows.Count; i++)
                {
                    var evaluation = restockRows[i];
                    RestockPresentation.Describe(evaluation, out bool hasNumber, out long amountMinor, out string reasonCode);
                    restock.Append(i == 0 ? string.Empty : "    ").Append(EconomyLabels.CategoryLabel(evaluation.CategoryKey)).Append("=");
                    restock.Append(hasNumber ? amountMinor.ToString() : O("RestockBlocked") + ":" + EconomyLabels.ReasonLabel(reasonCode));
                }
                restock.Append("    ").Append(O("RestockShown")).Append("=")
                    .Append(restockRows.Count.ToString()).Append("/").Append(restockTotal.ToString());
                AddMetric(productionCard, O("Restock"), restock.ToString());
            }
            var capacity = runtime.QueryWorkshopCapacity(map);
            AddMetric(productionCard, O("Shifts"), IntOf(capacity.ShiftCount));
            AddMetric(productionCard, O("StaffingGap"), IntOf(capacity.StaffingGap));
            AddMetric(productionCard, O("OpenBills"), IntOf(capacity.OpenBills));
            AddMetric(productionCard, O("Proposals"), runtime.ProductionAuthorisationCount.ToString());
            AddMetric(productionCard, O("Ownership"), runtime.OwnershipCount.ToString());
            cards.Add(productionCard);

            // ── 风险（工资可信度）────────────────────────────────────────────────
            var credibilityCard = new Card { Title = O("Credibility"), Metrics = new List<Metric>() };
            var credibilityKeys = runtime.QueryCredibilityKeys();
            int strong = 0, weak = 0;
            for (int i = 0; i < credibilityKeys.Count; i++)
            {
                var credibility = runtime.QueryWageCredibility(credibilityKeys[i]);
                if (credibility == null || !credibility.HasClaim) continue;
                if (credibility.Band == CredibilityBand.Strong) strong++;
                else if (credibility.Band == CredibilityBand.Weak) weak++;
            }
            AddMetric(credibilityCard, O("CredibilityTracked"), credibilityKeys.Count.ToString());
            AddMetric(credibilityCard, O("CredibilityStrong"), strong.ToString(), strong > 0 ? (Color?)RUIF.Tokens.Alive : null);
            AddMetric(credibilityCard, O("CredibilityWeak"), weak.ToString(), weak > 0 ? (Color?)RUIF.Tokens.Warn : null);
            cards.Add(credibilityCard);

            // 卡高与标签列宽（构建期定量；绘制期零测量）与整页内容高。
            state.Note = O("Note");
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].LabelColumnWidth = LabelColumnWidth(cards[i]);
                cards[i].Height = CardHeight(cards[i]);
            }
            state.Cards = cards;
            // B2：机构数改数**建筑**（政策记录已退役）；空态判据语义不变 = 三类读数都拿不到。
            state.Empty = runtime.QueryInstitutions(map.uniqueID).Count == 0 && !treasury.HasValue && categories.Count == 0;
            state.ContentHeight = ContentHeight(state);
        }

        /// <summary>绘制一张组卡：标题行 + 指标行（标签与数值紧邻成对，各自省略 + 悬停全文）。</summary>
        private static void DrawCard(Rect content, float y, Card card)
        {
            var rect = new Rect(content.x + EconomyRowList.CardSideInset, y,
                content.width - EconomyRowList.CardSideInset * 2f, card.Height);
            bool hovered = Mouse.IsOver(rect);
            var inner = EconomyUiKit.CardFrame(rect, hovered, null);
            float rowY = inner.y;
            var titleRect = new Rect(inner.x, rowY, inner.width, RUIF.Measure.RowHeight(GameFont.Small));
            RUIF.Draw.EllipsisLabel(titleRect, card.Title, GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            if (Mouse.IsOver(titleRect)) TooltipHandler.TipRegion(titleRect, card.Title);
            rowY += RUIF.Measure.RowHeight(GameFont.Small) + RUIF.Tokens.SpaceXs;
            float labelWidth = Mathf.Min(card.LabelColumnWidth, inner.width * 0.45f);
            float valueWidth = Mathf.Max(0f, inner.width - labelWidth);
            for (int i = 0; i < card.Metrics.Count; i++)
            {
                var metric = card.Metrics[i];
                var row = new Rect(inner.x, rowY, inner.width, MetricLineHeight);
                RUIF.Draw.EllipsisLabel(new Rect(row.x, row.y, labelWidth, row.height),
                    metric.Label, GameFont.Tiny, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
                RUIF.Draw.EllipsisLabel(new Rect(row.x + labelWidth, row.y, valueWidth, row.height),
                    metric.Value, GameFont.Tiny, metric.Tone, TextAnchor.MiddleLeft);
                if (Mouse.IsOver(row)) TooltipHandler.TipRegion(row, metric.Tooltip);
                rowY += MetricLineHeight;
            }
        }
        private static float MetricLineHeight => RUIF.Measure.RowHeight(GameFont.Tiny) + RUIF.Tokens.SpaceXxs;
        private static float LabelColumnWidth(Card card)
        {
            float width = 0f;
            for (int i = 0; i < card.Metrics.Count; i++)
                width = Mathf.Max(width, RUIF.Measure.Width(card.Metrics[i].Label, GameFont.Tiny));
            return width + RUIF.Tokens.SpaceMd;
        }
        private static float CardHeight(Card card)
            => RUIF.Tokens.CardPadY * 2f + RUIF.Measure.RowHeight(GameFont.Small) + RUIF.Tokens.SpaceXs
               + card.Metrics.Count * MetricLineHeight;
        private static float ContentHeight(OverviewPanelState state)
        {
            float height = 12f;
            if (state.Empty) return height + EmptyHeight;
            for (int i = 0; i < state.Cards.Count; i++) height += state.Cards[i].Height + EconomyRowList.CardStackGap;
            if (!string.IsNullOrEmpty(state.Note)) height += RUIF.Measure.RowHeight(GameFont.Small);
            return height;
        }
        /// <summary>组装一条指标（tooltip = 标签 · 值 的完整文本）。</summary>
        private static void AddMetric(Card card, string label, string value, Color? tone = null)
            => card.Metrics.Add(new Metric
            {
                Label = label,
                Value = value,
                Tone = tone ?? RUIF.Tokens.Text,
                Tooltip = string.IsNullOrEmpty(value) ? label : label + " · " + value
            });
        private static string IntOf(PersonalChronicle.Domain.PawnEconomy.Perception.Fact<int> fact)
            => fact.TryGetValue(out int value) ? value.ToString() : O("Unknown");
    }
}

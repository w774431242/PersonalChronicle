using System;
using System.Collections.Generic;
using System.Text;
using PersonalChronicle.Application.PawnEconomy.Care;
using PersonalChronicle.Domain.PawnEconomy.Care;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// 照护页的**会话态**（滚动 / 快照 / Hint 测量缓存）—— 由宿主持有，**不入存档**。
    /// CHG-20261010-ECON-AGG-MIGRATE 批次 A-2：内容层与宿主分离（从 `Dialog_PawnEconomy` 的
    /// 会话字段原样搬出）—— 聚合窗口与总控面板从此共用**同一份**状态类型与绘制实现。
    /// </summary>
    internal sealed class CarePanelState
    {
        public Vector2 Scroll;
        public int Tick = -99999;
        public object Language;
        public List<CarePanel.CapabilityRow> CapabilityRows;
        public List<CarePanel.AuthRow> AuthRows;
        public List<CarePanel.GrantCandidate> GrantCandidates;
        public float RevokeButtonWidth;
        public IReadOnlyList<CareAuthorization> Authorizations = Array.Empty<CareAuthorization>();
        public string HintWrapped;
        public float HintHeight;
        public float HintWidth = -1f;
        public object HintLanguage;
        /// <summary>强制下次绘制重建快照（撤销 / 新建授权后调用）。</summary>
        public void Invalidate() { Tick = -99999; }
        /// <summary>换图 / 换 runtime 时重置（滚动归零 + 快照作废）。</summary>
        public void ResetForNewSubject() { Scroll = Vector2.zero; CapabilityRows = null; Invalidate(); }
    }

    /// <summary>
    /// 照护页**宿主无关内容层**（B5 care tab 的全部绘制与快照）。
    ///
    /// <para><b>语义不变</b>：展示每条照护授权的四个身份（受益人 / 监护人 / 执行者 / 付款方）与额度消耗，
    /// 以及每种服务的能力档与仍然缺失的门。**本层不定价**：服务只有在每一道门都被证明后才可计费，
    /// 未证明时条上如实说明。</para>
    ///
    /// <para><b>宿主边界</b>（批次 A-2 抽取）：只经参数接收 <c>runtime</c> / <c>map</c> / <c>rect</c> / <c>state</c>；
    /// 不再引用任何窗口字段。跨页工具一律调共享实现（<c>EconomyLabels.ShortKey/FormatMinor/LocalizedId</c>、
    /// <c>EconomyRowList.CardSideInset/CardStackGap</c>），**不复制第二份**。</para>
    ///
    /// <para><b>性能</b>：快照 60 tick 或语言切换时重建；绘制路径零 `Measure.Width`、零拼接、零查询。
    /// 滚动入栈/出栈严格成对（异常只记一次日志，`EndScroll` 无条件在 finally 执行）。</para>
    /// </summary>
    internal static class CarePanel
    {
        // ── 文本（整键回落：缺键显示标识本身，绝不把裸键打上屏）──────────────────
        private static string C(string suffix) => EconomyLabels.TranslatedOrId("PC_PEA.Care." + suffix, suffix);
        private static string NeedsLabel(string suffix) => EconomyLabels.TranslatedOrId("PC_PEA.Needs." + suffix, suffix);
        /// <summary>服务类型标签（整键回落）。**public**：总览页复用同一映射，避免复制第二份。</summary>
        public static string ServiceLabel(ServiceKind kind) => EconomyLabels.LocalizedId("Service.", kind.ToString());
        /// <summary>能力档标签（整键回落）。**public**：同上，总览页复用。</summary>
        public static string CapabilityLabel(ServiceCapability capability)
            => EconomyLabels.LocalizedId("Capability.", capability.ToString());
        /// <summary>F5：七门稳定 ID → 呈现层本地化键（Domain 不引本地化；未知 ID 回落 ID 本身）。</summary>
        private static string GateLabel(string gateId) => EconomyLabels.TranslatedOrId("PC_PEA.Care.Gate." + gateId, gateId);
        private static string GateList(IReadOnlyList<string> blockers)
        {
            var text = new StringBuilder();
            for (int i = 0; i < blockers.Count; i++)
            { if (i > 0) text.Append(", "); text.Append(GateLabel(blockers[i])); }
            return text.ToString();
        }
        private static string Wrap(string value, float width, GameFont font)
            => NeedDashboardTextFlow.Wrap(value, Math.Max(1f, width - 2f), s => RUIF.Measure.Width(s, font));

        // ── 行模型（原 Dialog_PawnEconomy 的嵌套私有类，原样搬出并放宽为 internal）──
        /// <summary>能力条快照（P2-3 重排版）：服务名 chip + 分段读数（状态 / 证据 / 缺失门）——
        /// 每段文本与预量宽都在快照期算好（绘制期零测量、零拼接）。</summary>
        internal sealed class CapabilityRow
        {
            public string Name;
            public float NameChipWidth;
            public Color Tone;
            public string StatusText;
            public float StatusWidth;
            public string EvidenceText;
            public float EvidenceWidth;
            public string MissingText;
            public float MissingWidth;
            public string Tooltip;
        }
        /// <summary>授权卡快照：服务 chip / 受益人行 / 细节行 / 语义色 / 预量 chip 宽。</summary>
        internal sealed class AuthRow
        {
            public string AuthorizationId, ServiceName, Head, Detail, Tooltip;
            public Color Tone;
            public float ChipWidth;
        }
        /// <summary>可签发授权的殖民者：标签在快照期拼好（上限 32，与原循环一致）。</summary>
        internal sealed class GrantCandidate
        {
            public Pawn Pawn;
            public string Label;
        }

        private const long DefaultCareCapMinor = 1000L;
        private const int PlanningPolicy_DayTicks = 60000;
        private const float EmptyHeight = 72f;
        private static readonly ServiceKind[] ServiceKindValues =
        { ServiceKind.Examination, ServiceKind.Bed, ServiceKind.Treatment, ServiceKind.Childcare, ServiceKind.Education };
        private const int ServiceKindCount = 5;

        // ── 几何（全部由度量派生）──────────────────────────────────────────
        private static float CapabilityLineHeight => RUIF.Measure.RowHeight(GameFont.Small);
        private static float AuthCardHeight
            => RUIF.Tokens.CardPadY * 2f + RUIF.Measure.RowHeight(GameFont.Small) + RUIF.Tokens.SpaceXxs + RUIF.Measure.RowHeight(GameFont.Tiny);
        private static float GrantLineHeight => RUIF.Measure.ControlHeight(GameFont.Small) + RUIF.Tokens.SpaceXs;
        private static float HintTop => EconomyWindowLayout.PanelHintTop;

        /// <summary>快照重建：能力条 / 授权卡 / grant 候选 / 撤销按钮宽与 Hint 测量全部在此完成。</summary>
        private static void EnsureSnapshot(GameComponent_PawnEconomy runtime, Map map, CarePanelState state, int now)
        {
            EnsureHint(state, state.HintWidth > 0f ? state.HintWidth : 400f);
            if (state.CapabilityRows != null && now - state.Tick < 60 && state.Tick != -99999
                && ReferenceEquals(state.Language, LanguageDatabase.activeLanguage)) return;
            state.Tick = now;
            state.Language = LanguageDatabase.activeLanguage;
            var colonists = map?.mapPawns?.FreeColonistsSpawned;
            var rows = new List<CareAuthorization>();
            if (colonists != null)
                for (int i = 0; i < colonists.Count; i++)
                    if (colonists[i] != null) rows.AddRange(runtime.QueryCareAuthorizations(colonists[i].GetUniqueLoadID()));
            state.Authorizations = rows.AsReadOnly();
            state.CapabilityRows = new List<CapabilityRow>(ServiceKindCount);
            for (int i = 0; i < ServiceKindValues.Length; i++)
            {
                var kind = ServiceKindValues[i];
                var capability = runtime.QueryServiceCapability(kind)?.Capability ?? ServiceCapability.PublicVanilla;
                string name = ServiceLabel(kind);
                string status = CapabilityLabel(capability);
                string evidence = C("Evidence") + ": " + EconomyLabels.LocalizedId("Evidence.",
                    runtime.ServiceEvidenceStatusOf(kind).ToString());
                var capabilityState = runtime.QueryServiceCapability(kind);
                string missing = capabilityState != null && capabilityState.Blockers.Count > 0
                    ? C("Missing") + ": " + GateList(capabilityState.Blockers) : string.Empty;
                state.CapabilityRows.Add(new CapabilityRow
                {
                    Name = name,
                    NameChipWidth = RUIF.Measure.Width(name, GameFont.Tiny) + RUIF.Tokens.SpaceSm,
                    Tone = EconomyUiKit.CareCapabilityColor(capability),
                    StatusText = status,
                    StatusWidth = SegmentWidth(status),
                    EvidenceText = evidence,
                    EvidenceWidth = SegmentWidth(evidence),
                    MissingText = missing,
                    MissingWidth = SegmentWidth(missing),
                    Tooltip = name + " · " + status + "    " + evidence
                        + (missing.Length == 0 ? string.Empty : "    " + missing)
                });
            }
            state.AuthRows = new List<AuthRow>(state.Authorizations.Count);
            for (int i = 0; i < state.Authorizations.Count; i++)
            {
                var authorization = state.Authorizations[i];
                var detail = new StringBuilder();
                detail.Append(C("Guardian")).Append(": ").Append(EconomyLabels.ShortKey(authorization.GuardianKey));
                detail.Append("    ").Append(C("Payer")).Append(": ").Append(EconomyLabels.ShortKey(authorization.PayerKey));
                if (!string.IsNullOrEmpty(authorization.ExecutorKey))
                    detail.Append("    ").Append(C("Executor")).Append(": ").Append(EconomyLabels.ShortKey(authorization.ExecutorKey));
                detail.Append("    ").Append(C("Charged")).Append(": ")
                    .Append(EconomyLabels.FormatMinor(authorization.ChargedMinor)).Append(" / ")
                    .Append(EconomyLabels.FormatMinor(authorization.MaxAmountMinor));
                detail.Append("    ").Append(C("Expires")).Append(": ").Append(authorization.ExpiresTick.ToString());
                string serviceName = ServiceLabel(authorization.Kind);
                string head = C("Beneficiary") + ": " + EconomyLabels.ShortKey(authorization.BeneficiaryKey);
                string detailText = detail.ToString();
                state.AuthRows.Add(new AuthRow
                {
                    AuthorizationId = authorization.AuthorizationId,
                    ServiceName = serviceName,
                    Head = head,
                    Detail = detailText,
                    Tooltip = serviceName + " · " + head + "    " + detailText,
                    Tone = EconomyUiKit.AuthorizationColor(authorization.IsExpired(now),
                        authorization.MaxAmountMinor - authorization.ChargedMinor <= 0L),
                    ChipWidth = RUIF.Measure.Width(serviceName, GameFont.Tiny) + RUIF.Tokens.SpaceSm
                });
            }
            state.GrantCandidates = new List<GrantCandidate>();
            if (colonists != null)
                for (int i = 0; i < colonists.Count && i < 32; i++)
                {
                    var pawn = colonists[i];
                    if (pawn == null || HasCareGrant(runtime, pawn, ServiceKind.Treatment)) continue;
                    state.GrantCandidates.Add(new GrantCandidate
                    { Pawn = pawn, Label = C("Grant") + " · " + pawn.LabelShortCap });
                }
            state.RevokeButtonWidth = Mathf.Max(RUIF.Measure.Width(C("Revoke"), GameFont.Small) + RUIF.Tokens.SpaceMd * 2f,
                RUIF.Tokens.SpaceLg * 4f);
        }

        /// <summary>Hint 测量缓存（语言 / 宽度变化才重算；绘制路径零 Measure.Width）。</summary>
        private static void EnsureHint(CarePanelState state, float width)
        {
            if (state.HintWrapped != null && state.HintWidth == width
                && ReferenceEquals(state.HintLanguage, LanguageDatabase.activeLanguage)) return;
            state.HintWidth = width; state.HintLanguage = LanguageDatabase.activeLanguage;
            state.HintWrapped = Wrap(C("Hint"), width, GameFont.Small);
            state.HintHeight = Text.CalcHeight(state.HintWrapped, width);
        }

        /// <summary>绘制整页（标题 + Hint + 能力条 + 授权卡 / 空态 + Grant 行）。</summary>
        public static void Draw(GameComponent_PawnEconomy runtime, Map map, Rect rect, CarePanelState state)
        {
            if (runtime == null || map == null)
            { RUIF.FeedbackAtoms.EmptyState(rect, NeedsLabel("Unavailable"), NeedsLabel("UnavailableHint")); return; }
            // ⚠ 原点纪律（GATE-ARCH 2026-10-10 P1-B2）：外层矩形相对传入 `rect` 定位 —— 聚合窗口原点是 (0,0)
            //（等价），但总控面板 / 柜台传入的是列布局里的 `area`（非零原点）；按绝对 (0,0) 画会叠到窗口左上角。
            // 滚动视图内部仍是局部坐标（`BeginScroll` 返回原点 0 的内容区），故下方 (content.x, y) 写法不变。
            RUIF.Draw.Label(new Rect(rect.x, rect.y, rect.width - 40, 32), C("Title"), GameFont.Medium, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            float hintWidth = rect.width - 40f;
            state.HintWidth = hintWidth;
            EnsureHint(state, hintWidth);
            RUIF.Draw.Label(new Rect(rect.x, rect.y + HintTop, hintWidth, state.HintHeight), state.HintWrapped,
                GameFont.Small, RUIF.Tokens.Muted, TextAnchor.UpperLeft);

            int now = Find.TickManager?.TicksGame ?? 0;
            EnsureSnapshot(runtime, map, state, now);
            float bodyTop = HintTop + state.HintHeight + 6f;
            Rect body = new Rect(rect.x, rect.y + bodyTop, rect.width, Mathf.Max(60f, rect.height - bodyTop - 8f));
            float contentHeight = ContentHeight(state, body.height);
            Rect content = RUIF.Layout.BeginScroll(body, contentHeight, ref state.Scroll, false);
            try
            {
                float y = 4f;
                for (int i = 0; i < state.CapabilityRows.Count; i++)
                {
                    DrawCapabilityRow(content, y, state.CapabilityRows[i]);
                    y += CapabilityLineHeight + RUIF.Tokens.SpaceXs;
                }
                y += RUIF.Tokens.SpaceXs;
                RUIF.Draw.LineH(content.x + EconomyRowList.CardSideInset, y,
                    content.width - EconomyRowList.CardSideInset * 2f, RUIF.Tokens.BorderHair);
                y += RUIF.Tokens.RuleHeight + RUIF.Tokens.SpaceXs;
                if (state.AuthRows.Count == 0)
                {
                    RUIF.FeedbackAtoms.EmptyState(new Rect(content.x + EconomyRowList.CardSideInset, y,
                        content.width - EconomyRowList.CardSideInset * 2f, EmptyHeight),
                        C("None"), EconomyLabels.TranslatedOrId(EconomyWindowLayout.EmptyStateHintKey, "EmptyHint"));
                    y += EmptyHeight + EconomyRowList.CardStackGap;
                }
                else
                {
                    for (int i = 0; i < state.AuthRows.Count; i++)
                    {
                        if (DrawAuthCard(content, y, state.AuthRows[i], runtime, state)) { state.Invalidate(); break; }
                        y += AuthCardHeight + EconomyRowList.CardStackGap;
                    }
                }
                for (int i = 0; i < state.GrantCandidates.Count; i++)
                {
                    if (DrawGrantRow(content, y, state.GrantCandidates[i]))
                    {
                        OpenGrantMenu(runtime, map, state.GrantCandidates[i].Pawn, now, state);
                        break;
                    }
                    y += GrantLineHeight;
                }
            }
            catch (Exception error) { Log.ErrorOnce("[PC][UI] care rows failed: " + error, 0x1EC0DE5); }
            finally { RUIF.Layout.EndScroll(); }
            RUIF.MiscAtoms.ThinScrollBar(body, contentHeight, state.Scroll.y);
        }

        private static float ContentHeight(CarePanelState state, float bodyHeight)
        {
            float height = 12f;
            height += state.CapabilityRows.Count * (CapabilityLineHeight + RUIF.Tokens.SpaceXs);
            height += RUIF.Tokens.SpaceXs + RUIF.Tokens.RuleHeight + RUIF.Tokens.SpaceXs;
            height += state.AuthRows.Count == 0
                ? EmptyHeight + EconomyRowList.CardStackGap
                : state.AuthRows.Count * (AuthCardHeight + EconomyRowList.CardStackGap);
            height += state.GrantCandidates.Count * GrantLineHeight;
            return Mathf.Max(bodyHeight, height);
        }

        private static void DrawCapabilityRow(Rect content, float y, CapabilityRow row)
        {
            var line = new Rect(content.x + EconomyRowList.CardSideInset, y,
                content.width - EconomyRowList.CardSideInset * 2f, CapabilityLineHeight);
            var chip = EconomyUiKit.StatusChip(line, row.Name, row.Tone, row.NameChipWidth);
            float cursor = chip.xMax + RUIF.Tokens.SpaceXs;
            cursor = DrawSegment(line, cursor, row.StatusText, row.StatusWidth, row.Tone);
            if (cursor >= 0f) cursor = DrawSegment(line, cursor, row.EvidenceText, row.EvidenceWidth, RUIF.Tokens.Dim);
            if (cursor >= 0f) DrawSegment(line, cursor, row.MissingText, row.MissingWidth, RUIF.Tokens.Muted);
            if (Mouse.IsOver(line)) TooltipHandler.TipRegion(line, row.Tooltip);
        }
        /// <summary>能力行的一段读数。放不下返回 -1（D2 降级：少画一段）。段宽在快照期实测。</summary>
        private static float DrawSegment(Rect line, float x, string text, float width, Color color)
        {
            if (string.IsNullOrEmpty(text) || width <= 0f) return x;
            if (x + width > line.xMax) return -1f;
            RUIF.Draw.Label(new Rect(x, line.y, width, line.height), text, GameFont.Tiny, color, TextAnchor.MiddleLeft);
            return x + width + RUIF.Tokens.SpaceMd;
        }
        /// <summary>段宽（快照期唯一测量点）：实测宽 + 4px 安全余量。</summary>
        private static float SegmentWidth(string text)
            => string.IsNullOrEmpty(text) ? 0f : RUIF.Measure.Width(text, GameFont.Tiny) + 4f;

        /// <summary>授权卡（双行：chip + 受益人 / 细节；右侧撤销）。返回 true = 撤销成功（调用方刷新快照）。</summary>
        private static bool DrawAuthCard(Rect content, float y, AuthRow row, GameComponent_PawnEconomy runtime, CarePanelState state)
        {
            var card = new Rect(content.x + EconomyRowList.CardSideInset, y,
                content.width - EconomyRowList.CardSideInset * 2f, AuthCardHeight);
            bool hovered = Mouse.IsOver(card);
            var inner = EconomyUiKit.CardFrame(card, hovered, row.Tone);
            var head = new Rect(inner.x, inner.y, inner.width, RUIF.Measure.RowHeight(GameFont.Small));
            var chip = EconomyUiKit.StatusChip(head, row.ServiceName, row.Tone, row.ChipWidth);
            float buttonWidth = state.RevokeButtonWidth;
            var button = new Rect(card.xMax - RUIF.Tokens.CardPadX - buttonWidth,
                card.y + (AuthCardHeight - RUIF.Measure.ControlHeight(GameFont.Small)) * 0.5f,
                buttonWidth, RUIF.Measure.ControlHeight(GameFont.Small));
            bool revoke = RUIF.ButtonAtoms.PrimaryButton(button, C("Revoke"), true, RUIF.ButtonTone.Danger);
            float textX = chip.xMax + RUIF.Tokens.SpaceXs;
            float textWidth = Mathf.Max(0f, button.x - textX - RUIF.Tokens.SpaceXs);
            RUIF.Draw.EllipsisLabel(new Rect(textX, head.y, textWidth, head.height), row.Head,
                GameFont.Small, RUIF.Tokens.Text, TextAnchor.MiddleLeft);
            var detail = new Rect(inner.x, head.yMax + RUIF.Tokens.SpaceXxs, inner.width,
                RUIF.Measure.RowHeight(GameFont.Tiny));
            RUIF.Draw.EllipsisLabel(detail, row.Detail, GameFont.Tiny, RUIF.Tokens.Muted, TextAnchor.MiddleLeft);
            if (hovered) TooltipHandler.TipRegion(card, row.Tooltip);
            return revoke && runtime.RevokeCareAuthorization(row.AuthorizationId) == CareCode.Applied;
        }
        /// <summary>Grant 行：宽 = min(10×SpaceLg, 卡内宽)（保持既有 240px 上限；纯算术，无测量）。</summary>
        private static bool DrawGrantRow(Rect content, float y, GrantCandidate candidate)
            => RUIF.ButtonAtoms.PrimaryButton(new Rect(content.x + EconomyRowList.CardSideInset, y,
                Mathf.Min(RUIF.Tokens.SpaceLg * 10f, content.width - EconomyRowList.CardSideInset * 2f),
                RUIF.Measure.ControlHeight(GameFont.Small)), candidate.Label);

        /// <summary>M8：Grant 的**执行者选择** —— "不限执行者"（默认）或指定一名非受益者的殖民者。</summary>
        private static void OpenGrantMenu(GameComponent_PawnEconomy runtime, Map map, Pawn beneficiary, int now, CarePanelState state)
        {
            var options = new List<FloatMenuOption>
            { new FloatMenuOption(C("GrantAnyExecutor"), () => SubmitGrant(runtime, beneficiary, null, now, state)) };
            var colonists = map?.mapPawns?.FreeColonistsSpawned;
            if (colonists != null)
                for (int i = 0; i < colonists.Count; i++)
                {
                    var candidate = colonists[i];
                    if (candidate == null || candidate == beneficiary || candidate.Dead
                        || candidate.Downed || candidate.InMentalState) continue;
                    string executorKey = candidate.GetUniqueLoadID();
                    options.Add(new FloatMenuOption(C("GrantExecutor") + " · " + candidate.LabelShortCap,
                        () => SubmitGrant(runtime, beneficiary, executorKey, now, state)));
                }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        /// <summary>提交一条 Treatment 授权；executorKey 为 null ⇒ 不限执行者。执行者被编进 AuthorizationId。</summary>
        private static void SubmitGrant(GameComponent_PawnEconomy runtime, Pawn beneficiary, string executorKey, int now,
            CarePanelState state)
        {
            int tick = Find.TickManager?.TicksGame ?? now;
            var request = new CreateCareRequest
            {
                AuthorizationId = "care:" + beneficiary.GetUniqueLoadID() + "|treatment|"
                    + (string.IsNullOrEmpty(executorKey) ? "any" : executorKey) + "|" + tick.ToString(),
                Kind = ServiceKind.Treatment, BeneficiaryKey = beneficiary.GetUniqueLoadID(),
                GuardianKey = null, ExecutorKey = executorKey, PayerKey = beneficiary.GetUniqueLoadID(),
                BeneficiaryIsAdult = beneficiary.DevelopmentalStage == DevelopmentalStage.Adult,
                MaxAmountMinor = DefaultCareCapMinor, CreatedTick = tick,
                ExpiresTick = tick + PlanningPolicy_DayTicks, ExpectedRevision = 0
            };
            if (runtime.CreateCareAuthorization(request) == CareCode.Applied) state.Invalidate();
            else Messages.Message(C("GrantRejected"), MessageTypeDefOf.RejectInput, false);
        }
        /// <summary>M8：只在存在"仍可用"授权时才隐藏 Grant 按钮（过期或额度用尽的不占名额）。</summary>
        private static bool HasCareGrant(GameComponent_PawnEconomy runtime, Pawn pawn, ServiceKind kind)
        {
            var rows = runtime.QueryCareAuthorizations(pawn.GetUniqueLoadID());
            int now = Find.TickManager?.TicksGame ?? 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Kind != kind || row.IsExpired(now)) continue;
                if (row.MaxAmountMinor - row.ChargedMinor <= 0L) continue;
                return true;
            }
            return false;
        }
    }
}

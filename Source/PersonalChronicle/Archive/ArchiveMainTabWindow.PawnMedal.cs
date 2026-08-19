using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>Partial of ArchiveMainTabWindow 鈥?PawnDetail drawing (BUG-BASE-01 refactor).</summary>
    public sealed partial class ArchiveMainTabWindow
    {

        private float DrawHealthValuation(Rect rect, float y, ReadModels.HealthView h)
        {
            // v4.6.1: full HTML-style layout — 4 StatCells + 3 dim bars + event log + verdict.
            float headH = 26f;
            float statRowH = 80f;
            float dimRowH = 16f;
            float dimBlockH = dimRowH * 3f + 8f;
            float eventHeaderH = 18f;
            int evCount = h.Events != null ? Mathf.Min(h.Events.Count, 6) : 0;
            // v4.6.3: 事件行高 22f 适配中文 GameFont.Tiny。
            float eventsH = evCount > 0 ? (evCount * 22f + 4f) : 18f;
            float blockH = headH + statRowH + 8f + dimBlockH + 8f + eventHeaderH + eventsH + 6f;
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

            // === Row 3: depreciation event log ===
            float evY = dimY + dimBlockH + 4f;
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
                    ReadModels.HealthEventView e = h.Events[i];
                    if (e == null) continue;
                    string impact = e.Impact == 0 ? "" : ("  " + FormatSilver(e.Impact) + "PersonalChronicle.UI.SilverUnit".Translate().ToString());
                    string tag = string.IsNullOrEmpty(e.TagText) ? "" : ("  [" + e.TagText + "]");
                    string line = e.DateText + "  " + e.Description + impact + tag;
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

        private static int MedalWallVisibleCount(IReadOnlyList<ReadModels.MedalView> medals)
        {
            if (medals == null) return 0;
            int count = 0;
            for (int i = 0; i < medals.Count; i++)
            {
                ReadModels.MedalView m = medals[i];
                if (m != null && m.IsApplicable && m.IsHighestTier) count++;
            }
            return count;
        }

        private static float MedalWallHeight(IReadOnlyList<ReadModels.MedalView> medals)
        {
            int count = MedalWallVisibleCount(medals);
            if (count == 0) return UITheme.SectionTitleHeight + 28f;
            int rows = Mathf.CeilToInt(count / 2f);
            return UITheme.SectionTitleHeight + rows * MedalWallCardH + (rows - 1) * UITheme.GridGap;
        }

        private float DrawMedalWall(Rect rect, float y, IReadOnlyList<ReadModels.MedalView> medals, IArchiveService service)
        {
            Rect titleRect = new Rect(rect.x, y, rect.width, UITheme.SectionTitleHeight);
            DrawSectionTitle(rect, ref y, PersonalChronicle.Data.MedalTranslationKeys.WallTitle().Translate().ToString());

            // v1.1.4 手动授勋入口：仅作用于当前详情页 Pawn（活读实例）。
            float btnW = 96f;
            float btnH = 22f;
            Rect awardBtn = new Rect(rect.xMax - btnW, titleRect.y + (UITheme.SectionTitleHeight - btnH) / 2f, btnW, btnH);
            if (Widgets.ButtonText(awardBtn, MedalTranslationKeys.ManualAwardButton().Translate().ToString())
                && cachedDetailObject is PawnObject medalPawn)
            {
                OpenManualAwardDialog(medalPawn, service);
            }

            // §6.9: 只画 IsApplicable && IsHighestTier（最高已达档位归并已由 ReadModel 完成）。
            List<ReadModels.MedalView> shown = null;
            if (medals != null)
            {
                for (int i = 0; i < medals.Count; i++)
                {
                    ReadModels.MedalView m = medals[i];
                    if (m == null || !m.IsApplicable || !m.IsHighestTier) continue;
                    if (shown == null) shown = new List<ReadModels.MedalView>();
                    shown.Add(m);
                }
            }

            if (shown == null || shown.Count == 0)
            {
                Color prevColor = GUI.color;
                GameFont prevFont = Text.Font;
                TextAnchor prevAnchor = Text.Anchor;
                try
                {
                    GUI.color = UITheme.Dim;
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                    Widgets.Label(new Rect(rect.x + UITheme.CardPadX, y, rect.width - UITheme.CardPadX * 2f, 28f),
                        PersonalChronicle.Data.MedalTranslationKeys.WallEmpty().Translate().ToString());
                }
                finally
                {
                    GUI.color = prevColor;
                    Text.Font = prevFont;
                    Text.Anchor = prevAnchor;
                }
                return y + 28f;
            }

            const int cols = 2;
            float gap = UITheme.GridGap;
            float cardW = (rect.width - UITheme.CardPadX * 2f - gap) / cols;
            int rows = Mathf.CeilToInt(shown.Count / (float)cols);
            for (int i = 0; i < shown.Count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                float cx = rect.x + UITheme.CardPadX + col * (cardW + gap);
                float cy = y + row * (MedalWallCardH + gap);
                DrawMedalCard(new Rect(cx, cy, cardW, MedalWallCardH), shown[i]);
            }
            return y + rows * MedalWallCardH + (rows - 1) * gap;
        }

        /// <summary>
        /// v1.1.4 打开手动授勋对话框。仅当能取到该殖民者的活读 Pawn 实例与持久化组件时
        /// 才弹出（死亡/离队归档者无 live Pawn，不授勋，与自动授勋语义一致）。授予成功后
        /// 经 <see cref="NotifyManualAward"/> 强制重建勋章墙快照。
        /// </summary>
        private void OpenManualAwardDialog(PawnObject pawnObject, IArchiveService service)
        {
            if (pawnObject == null || service == null)
            {
                return;
            }
            Pawn livePawn = service.GetLivePawn(pawnObject.StableId);
            if (livePawn == null)
            {
                return;
            }
            ChronicleGameComponent component = Current.Game != null
                ? Current.Game.GetComponent<ChronicleGameComponent>()
                : null;
            if (component == null)
            {
                return;
            }
            Find.WindowStack.Add(new Dialog_ManualMedalAward(livePawn, component, NotifyManualAward));
        }

        private static void DrawMedalCard(Rect card, ReadModels.MedalView m)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                Color tierColor = UITheme.MedalTierColor(m.Tier);
                UIComponents.Border(card, UITheme.BorderSoft);

                // 徽章色块 + 称号
                Rect badge = new Rect(card.x + 8f, card.y + 8f, 14f, 16f);
                GUI.color = tierColor;
                Widgets.DrawBoxSolid(badge, tierColor);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect labelRect = new Rect(card.x + 28f, card.y + 6f, card.width - 76f, 24f);
                Widgets.Label(labelRect, m.Label);

                // 档位 Pill（右上）
                Rect pill = new Rect(card.x + card.width - 54f, card.y + 6f, 46f, 18f);
                UIComponents.Pill(pill,
                    PersonalChronicle.Data.MedalTranslationKeys.Tier(m.Tier).Translate().ToString(),
                    tierColor);

                // 进度条
                Rect bar = new Rect(card.x + 8f, card.y + 34f, card.width - 16f, UITheme.ProgressbarH);
                UIComponents.ProgressBar(bar, m.Progress, tierColor);

                // 当前值/阈值（未授予但已达标时用 ProgressBar caption 承载）
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Rect cap = new Rect(card.x + 8f, bar.yMax + 2f, card.width - 16f, 16f);
                GUI.color = UITheme.Muted;
                string ratio = FormatMedalValue(m.CurrentValue) + " / " + FormatMedalValue(m.Threshold);
                Widgets.Label(cap, ratio);

                // 通道 B 增益文案就近展示
                if (!string.IsNullOrEmpty(m.BuffText))
                {
                    GUI.color = tierColor;
                    Text.Anchor = TextAnchor.UpperLeft;
                    Widgets.Label(new Rect(card.x + 8f, cap.yMax + 1f, card.width - 16f, 18f), m.BuffText);
                }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private static string FormatMedalValue(double value)
        {
            if (value >= 10000d) return (value / 1000d).ToString("0.#") + "K";
            return value.ToString("0.#");
        }

        private static void DrawHealthDimBar(Rect rect, string label, float score01to100,
            IReadOnlyList<ReadModels.HealthFactorView> factors, string dimKey)
        {
            // v4.6.5: fixed-size progress bar matching the output-ledger layout:
            // label | bar | value area | pct. This prevents the health bar from
            // stretching across the whole row.
            float labelW = 80f;
            float pctW = 50f;
            float barX = rect.x + labelW;
            float barW = rect.width - labelW - pctW - UITheme.ProgressbarValueW;
            UIComponents.Label(new Rect(rect.x, rect.y, labelW, rect.height),
                label,
                GameFont.Tiny, UITheme.SecondaryText,
                TextAnchor.MiddleLeft);
            float barH = UITheme.ProgressbarH;
            float share = Mathf.Clamp01(score01to100 / 100f);
            bool low = share < 0.3f;
            Color fill = low ? UITheme.Blood : UITheme.PillGold;
            Rect bar = new Rect(barX, rect.y + (rect.height - barH) / 2f, barW, barH);
            UIComponents.ProgressBar(bar, share, fill);
            UIComponents.Label(new Rect(rect.x + rect.width - pctW, rect.y, pctW, rect.height),
                Mathf.RoundToInt(score01to100) + "%",
                GameFont.Tiny, low ? UITheme.Blood : UITheme.Text,
                TextAnchor.MiddleRight);

            // Per-dimension hover tip listing all positive/negative factors.
            if (factors != null && factors.Count > 0)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine(label);
                for (int i = 0; i < factors.Count; i++)
                {
                    ReadModels.HealthFactorView f = factors[i];
                    if (f == null) continue;
                    string tag = f.IsPositive ? "✓" : "✗";
                    string impact = f.Impact == 0 ? "" : (" (" + (f.Impact > 0 ? "+" : "") + f.Impact + ")");
                    sb.AppendLine("  " + tag + " " + f.LabelText + impact);
                }
                TooltipHandler.TipRegion(rect, sb.ToString().TrimEnd());
            }
        }

        private float DrawCoverHeader(Rect rect, float y, PawnObject pawn, IArchiveService service)
        {
            float portraitW = 60f;
            float portraitH = 75f;
            float portraitPad = 4f;
            float rowH = portraitH + 8f; // includes vertical breathing room

            // Tier medal inline row (.tier-inline in the design spec) grows the
            // header when a confirmed/estimated tier exists.
            WorkIntensityView intensity = cachedWorkIntensity;
            float tierRowH = (intensity != null && intensity.IsDefined) ? 20f : 0f;
            rowH += tierRowH;

            Rect portraitRect = new Rect(rect.x, y, portraitW, portraitH);
            Rect infoRect = new Rect(
                portraitRect.xMax + portraitPad + 6f,
                y,
                rect.width - portraitW - portraitPad - 6f,
                rowH);

            // ---- Portrait ----
            Pawn livePawn = service != null ? service.GetLivePawn(pawn.StableId) : null;
            DrawPortraitOrPlaceholder(portraitRect, livePawn);

            // ---- Tier medal inline (.tier-inline in spec) ----
            // Drawn at the top of the info column; the name/role/days block shifts down
            // by tierRowH so they never overlap the medal row.
            if (tierRowH > 0f)
            {
                DrawTierMedalInline(new Rect(infoRect.x, infoRect.y, infoRect.width, tierRowH), intensity);
            }

            // ---- Name + role description ----
            float contentY = infoRect.y + tierRowH;
            UIComponents.Label(new Rect(infoRect.x, contentY + 2f, infoRect.width, 26f),
                ObjectDisplayLabel(pawn),
                GameFont.Medium, UITheme.Text,
                TextAnchor.UpperLeft);

            string roleDesc = BuildCoverRoleDescription(pawn);
            UIComponents.Label(new Rect(infoRect.x, contentY + 30f, infoRect.width - 70f, 18f),
                roleDesc,
                GameFont.Tiny, UITheme.SecondaryText,
                TextAnchor.UpperLeft);

            // v4.14: identity dimension pill (role), matching the preview's
            // identity-pill — the cover shows who the pawn is in the colony.
            string identityText = "PersonalChronicle.UI.Cover.Identity".Translate(
                RoleLabel(pawn.Role)).ToString();
            float identityW = Mathf.Min(64f, Text.CalcSize(identityText).x + 10f);
            UIComponents.Pill(
                new Rect(infoRect.x + infoRect.width - identityW, contentY + 30f,
                    identityW, 16f),
                identityText, RolePillColor(pawn.Role));

            string daysText = BuildCoverDaysText(pawn);
            UIComponents.Label(new Rect(infoRect.x, contentY + 50f, infoRect.width, 18f),
                daysText,
                GameFont.Tiny, UITheme.Muted,
                TextAnchor.UpperLeft);

            // ---- In-service days text + stamp on the same row ----
            // v4.6.5: stamp is placed to the right of "在册 X 日" instead of the
            // bottom row, preventing overlap with the days line.
            // v5.x "在册"判定：存活 且 属于当前殖民地人口 → 在册；死亡归档或
            // 已离开殖民地 → 不在册。不再用 IsArchived（DeathTick>0）作唯一依据
            // —— 那会把"存档有快照但还活着"的殖民者误标为不在册。
            bool alive = service != null && service.IsCurrentlyEnlisted(pawn.StableId);
            string stampKey = alive
                ? "PersonalChronicle.UI.Cover.StampAlive"
                : "PersonalChronicle.UI.Cover.StampDead";
            string stampText = stampKey.Translate().ToString();
            float stampW = Mathf.Max(60f, Text.CalcSize(stampText).x + 18f);
            float stampH = 20f;

            float daysY = contentY + 50f;
            Vector2 daysSize = Text.CalcSize(daysText);
            Rect daysRect = new Rect(infoRect.x, daysY, daysSize.x + 4f, 18f);
            UIComponents.Label(daysRect, daysText,
                GameFont.Tiny, UITheme.Muted,
                TextAnchor.MiddleLeft);

            Rect stampRect = new Rect(
                daysRect.xMax + 6f,
                daysY + (18f - stampH) / 2f,
                stampW,
                stampH);
            UIComponents.Pill(stampRect, stampText, alive ? UITheme.Alive : UITheme.Dead);

            // ---- Incomplete badge (mid-install: JoinTick=-1) ----
            if (pawn.JoinTick < 0L)
            {
                string incomplete = "PersonalChronicle.UI.Cover.Incomplete".Translate().ToString();
                float badgeW = Mathf.Min(infoRect.width, Text.CalcSize(incomplete).x + 16f);
                Rect badgeRect = new Rect(infoRect.x, daysY - 22f, badgeW, 18f);
                DrawIncompleteBadge(badgeRect, incomplete);
            }

            return y + rowH;
        }

        private static void DrawPortraitOrPlaceholder(Rect rect, Pawn livePawn)
        {
            // Native portrait frame (blood-tinted border, dark fill, dim label when missing).
            Color prevColor = GUI.color;
            Color prevFontColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                Widgets.DrawBoxSolid(rect, UITheme.Panel);
                Widgets.DrawBox(rect);

                bool hasPortrait = false;
                if (livePawn != null)
                {
                    // Use the native PortraitsCache (RimWorld 1.6). Catch any null
                    // return / RenderTexture issue by falling through to placeholder.
                    RenderTexture portrait = PortraitsCache.Get(
                        livePawn, new Vector2(rect.width, rect.height), Rot4.South);
                    if (portrait != null)
                    {
                        GUI.DrawTexture(rect, portrait);
                        hasPortrait = true;
                    }
                }
                // Placeholder caption ("人物画像 / PortraitsCache") shows ONLY when no
                // live pawn / render is available — never over a real 3D portrait.
                if (!hasPortrait)
                {
                    string label = "PersonalChronicle.UI.Cover.PortraitLabel".Translate().ToString();
                    string sub = "PersonalChronicle.UI.Cover.PortraitLabelSub".Translate().ToString();
                    GUI.color = UITheme.Dim;
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(rect, label);
                    Rect subRect = new Rect(rect.x, rect.yMax - 14f, rect.width, 12f);
                    Widgets.Label(subRect, sub);
                }
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private static void DrawIncompleteBadge(Rect rect, string label)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                GUI.color = UITheme.BadgeIncompleteFill;
                Widgets.DrawBoxSolid(rect, UITheme.BadgeIncompleteFill);
                GUI.color = UITheme.Blood;
                Widgets.DrawBox(rect);
                GUI.color = UITheme.Blood;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, label);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private static void DrawTierMedalInline(Rect rect, WorkIntensityView intensity)
        {
            if (intensity == null || !intensity.IsDefined)
            {
                return;
            }
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            try
            {
                // ---- Medal square (tier colour fill + display code) ----
                float medal = Mathf.Min(rect.height, 18f);
                Rect medalRect = new Rect(rect.x, rect.y + (rect.height - medal) / 2f, medal, medal);
                Color tierColor = UITheme.Accent;
                if (!string.IsNullOrEmpty(intensity.ColorHex)
                    && ColorUtility.TryParseHtmlString(intensity.ColorHex, out Color parsed))
                {
                    tierColor = parsed;
                }
                GUI.color = tierColor;
                Widgets.DrawBoxSolid(medalRect, tierColor);
                GUI.color = UITheme.Window;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(medalRect, intensity.DisplayCode ?? string.Empty);

                // ---- Title + tag + projected daily ----
                float textX = medalRect.xMax + 6f;
                float textW = rect.xMax - textX;
                string title = string.IsNullOrEmpty(intensity.LabelKey)
                    ? string.Empty
                    : intensity.LabelKey.Translate().ToString();
                string tag = (intensity.IsEstimated
                    ? "PersonalChronicle.UI.Intensity.Estimated"
                    : "PersonalChronicle.UI.Intensity.Actual").Translate().ToString();
                string daily = intensity.DailyHours > 0d
                    ? string.Format("PersonalChronicle.UI.HoursPerDay".Translate(), intensity.DailyHours.ToString("0.0"))
                    : string.Empty;
                string line = string.IsNullOrEmpty(daily)
                    ? string.Format("{0} · {1}", title, tag)
                    : string.Format("{0} · {1} · {2}", title, tag, daily);

                GUI.color = UITheme.Text;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(textX, rect.y, textW, rect.height), line);
            }
            finally
            {
                GUI.color = prevColor;
                Text.Font = prevFont;
                Text.Anchor = prevAnchor;
            }
        }

        private string BuildCoverRoleDescription(PawnObject pawn)
        {
            string role = RoleLabel(pawn.Role);
            string faction = FactionLabel(pawn);
            string background = BuildCoverBackground(pawn);
            if (!string.IsNullOrEmpty(faction) && !string.IsNullOrEmpty(background))
            {
                return "PersonalChronicle.UI.Cover.RoleFormat".Translate(role, faction, background).ToString();
            }
            if (string.IsNullOrEmpty(faction) && !string.IsNullOrEmpty(background))
            {
                return "PersonalChronicle.UI.Cover.RoleFormatNoFaction".Translate(role, background).ToString();
            }
            if (!string.IsNullOrEmpty(faction) && string.IsNullOrEmpty(background))
            {
                return "PersonalChronicle.UI.Cover.RoleFormatNoBackground".Translate(role, faction).ToString();
            }
            return "PersonalChronicle.UI.Cover.RoleFormatMinimal".Translate(role).ToString();
        }

        private string BuildCoverBackground(PawnObject pawn)
        {
            string child = ResolveBackstoryLabel(pawn.ChildhoodBackstoryDefName);
            string adult = ResolveBackstoryLabel(pawn.AdulthoodBackstoryDefName);
            if (!string.IsNullOrEmpty(child) && !string.IsNullOrEmpty(adult))
            {
                return child + " / " + adult;
            }
            if (!string.IsNullOrEmpty(child)) return child;
            if (!string.IsNullOrEmpty(adult)) return adult;
            return "";
        }

        private string BuildCoverDaysText(PawnObject pawn)
        {
            if (pawn.JoinTick < 0L)
            {
                return "PersonalChronicle.UI.Cover.DaysUnknown".Translate().ToString();
            }
            long endTick = pawn.IsArchived && pawn.DeathTick > 0L
                ? pawn.DeathTick
                : Find.TickManager.TicksGame;
            // 在册时间固定用"日"（翻译键 "在册 {0} 日"），不做年/季/时等日以下
            // 单位换算——官方日数 API 取整，避免语义混乱（"在册 1 年 2 季"）。
            long days = (long)RimWorld.GenDate.TicksToDays((int)(endTick - pawn.JoinTick));
            if (days <= 0L) days = 0L;
            string key = pawn.IsArchived
                ? "PersonalChronicle.UI.Cover.DaysToDeath"
                : "PersonalChronicle.UI.Cover.DaysKnown";
            return key.Translate(days).ToString();
        }

        private float DrawLedger(Rect rect, float y, PawnObject pawn)
        {
            // Header row + 4 StatCells (already realised output, work hours, weekly
            // average, net yield) — matches the v4.6.1 contribution-archive preview.
            float headH = 26f;
            float cellH = UIComponents.StatCellMinHeight;
            float gap = UITheme.GridGap;
            float cellW = (rect.width - UITheme.CardPadX * 2f - gap * 3f) / 4f;

            UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, headH),
                "PersonalChronicle.UI.Ledger.Title");

            float rowY = y + headH + UITheme.SpaceXxs;
            float cellX = rect.x + UITheme.CardPadX;

            bool known = pawn.JoinTick >= 0L;
            string unknown = "PersonalChronicle.UI.Ledger.UnknownValue".Translate().ToString();

            // Already realised output (silver).
            string realisedValue = known && cachedProductionSummary != null && cachedProductionSummary.TotalMarketValue > 0f
                ? FormatSilver(cachedProductionSummary.TotalMarketValue)
                : (known ? "0" : unknown);
            string realisedUnit = "PersonalChronicle.UI.Ledger.OutputValueUnit".Translate().ToString();
            UIComponents.StatCell(new Rect(cellX, rowY, cellW, cellH),
                "PersonalChronicle.UI.Ledger.OutputValue".Translate().ToString(),
                realisedValue,
                realisedUnit,
                inlineSubLabel: true);

            // Total work hours — value carries the numeric, subLabel carries the unit
            // (避免 "10.7 h" + subLabel "h" 的单位重复).
            string workValue = known
                ? (GetCachedTotalWorkTicks() / (float)RimWorld.GenDate.TicksPerHour).ToString("0.0")
                : unknown;
            UIComponents.StatCell(new Rect(cellX + (cellW + gap), rowY, cellW, cellH),
                "PersonalChronicle.UI.Ledger.TotalWork".Translate().ToString(),
                workValue,
                "PersonalChronicle.UI.Ledger.TotalWorkUnit".Translate().ToString(),
                inlineSubLabel: true);

            // Weekly average yield (estimated silver per week from the health evaluator).
            string weeklyValue = known && cachedHealth != null && cachedHealth.IsDefined
                ? FormatSilver(cachedHealth.WeeklySilverEstimate) : unknown;
            UIComponents.StatCell(new Rect(cellX + 2f * (cellW + gap), rowY, cellW, cellH),
                "PersonalChronicle.UI.Ledger.Weekly".Translate().ToString(),
                weeklyValue,
                "PersonalChronicle.UI.Ledger.WeeklyUnit".Translate().ToString(),
                inlineSubLabel: true);

            // Net yield = realised output − work hours × hourly cost rate. Cost
            // rate is a conservative estimate of colony upkeep per work-hour
            // (the preview uses 2 sv/h); the value is honest "estimate" semantics.
            string netValue = known
                ? FormatSilver(NetLedgerSilver(pawn))
                : unknown;
            UIComponents.StatCell(new Rect(cellX + 3f * (cellW + gap), rowY, cellW, cellH),
                "PersonalChronicle.UI.Ledger.Net".Translate().ToString(),
                netValue,
                "PersonalChronicle.UI.Ledger.NetUnit".Translate().ToString(),
                inlineSubLabel: true);

            return rowY + cellH;
        }

        private float NetLedgerSilver(PawnObject pawn)
        {
            const float hourlyCostRate = 2f; // sv per work-hour (preview contract)
            float realised = cachedProductionSummary != null
                ? cachedProductionSummary.TotalMarketValue : 0f;
            float hours = (float)GetCachedTotalWorkTicks() / RimWorld.GenDate.TicksPerHour;
            return realised - hours * hourlyCostRate;
        }

        private long GetCachedTotalWorkTicks()
        {
            // Use the cachedWorkIntensity view as a proxy; falling back to 0 is fine.
            if (cachedWorkIntensity == null || !cachedWorkIntensity.IsDefined) return 0L;
            double hours = cachedWorkIntensity.TotalHours;
            // 1h = GenDate.TicksPerHour（2500），禁止魔法数字。
            return (long)(hours * RimWorld.GenDate.TicksPerHour);
        }

        private float DrawOutputLedger(Rect rect, float y)
        {
            // Header + ProgressBar rows by production type. Empty state shows placeholder.
            float headH = 26f;
            UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, headH),
                "PersonalChronicle.UI.OutputLedger.Title");
            float bodyY = y + headH + 4f;

            IReadOnlyList<ProductionTypeView> types = cachedProductionSummary != null
                ? cachedProductionSummary.Types : null;
            float totalValue = cachedProductionSummary != null
                ? cachedProductionSummary.TotalMarketValue : 0f;

            if (types == null || types.Count == 0 || totalValue <= 0f)
            {
                UIComponents.Label(new Rect(rect.x + UITheme.CardPadX, bodyY, rect.width - UITheme.CardPadX * 2f, 22f),
                    "PersonalChronicle.UI.OutputLedger.Empty".Translate().ToString(),
                    GameFont.Tiny, UITheme.Dim);
                return bodyY + 24f;
            }

            // Sort descending by value for the most-extracted types first.
            List<ProductionTypeView> sorted = new List<ProductionTypeView>(types);
            sorted.Sort((a, b) => b.MarketValue.CompareTo(a.MarketValue));
            int take = Mathf.Min(5, sorted.Count);
            const float rowH = 22f;
            float rowY = bodyY;
            for (int i = 0; i < take; i++)
            {
                ProductionTypeView t = sorted[i];
                if (t == null) continue;
                float share = t.MarketValue / totalValue;
                string label = ResolveProductionTypeLabel(t.DefName);
                string valueText = FormatSilver(t.MarketValue)
                    + " " + "PersonalChronicle.UI.Ledger.OutputValueUnit".Translate().ToString();
                string pctText = Mathf.RoundToInt(share * 100f) + "%";
                DrawProductionRow(new Rect(rect.x + UITheme.CardPadX, rowY, rect.width - UITheme.CardPadX * 2f, rowH),
                    label, valueText, pctText, share);
                rowY += rowH;
            }
            return rowY;
        }

        private static void DrawProductionRow(Rect rect, string label, string valueText, string pctText, float share)
        {
            float labelW = 80f;
            float pctW = 50f;
            float barX = rect.x + labelW;
            float barW = rect.width - labelW - pctW - UITheme.ProgressbarValueW;
            UIComponents.Label(new Rect(rect.x, rect.y, labelW, rect.height),
                label,
                GameFont.Tiny, UITheme.Text,
                TextAnchor.MiddleLeft);
            float barH = UITheme.ProgressbarH;
            Rect bar = new Rect(barX, rect.y + (rect.height - barH) / 2f, barW, barH);
            UIComponents.ProgressBar(bar, Mathf.Clamp01(share), UITheme.Blood);
            UIComponents.Label(new Rect(bar.xMax + 4f, rect.y, UITheme.ProgressbarValueW, rect.height),
                valueText,
                GameFont.Tiny, UITheme.SecondaryText,
                TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(rect.x + rect.width - pctW, rect.y, pctW, rect.height),
                pctText,
                GameFont.Tiny, UITheme.Muted,
                TextAnchor.MiddleRight);
        }

        private static string ResolveProductionTypeLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return "—";
            // v4.6.5: production rows are aggregated by ThingCategory. Try the
            // category def first, then fall back to the item def for uncategorised
            // (or legacy) entries.
            var cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(defName);
            if (cat != null) return cat.label != null ? cat.label : defName;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null) return def.label != null ? def.label : defName;
            return defName;
        }


    }
}

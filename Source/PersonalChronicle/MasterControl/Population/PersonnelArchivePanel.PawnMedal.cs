using PersonalChronicle.Archive;
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
using ReadModels = PersonalChronicle.Application.ReadModels;

namespace PersonalChronicle.MasterControl.Population
{
    /// <summary>Partial of PersonnelArchivePanel —PawnDetail drawing (BUG-BASE-01 refactor).</summary>
    public sealed partial class PersonnelArchivePanel
    {

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
            Find.WindowStack.Add(new Dialog_ManualMedalAward(livePawn, NotifyManualAward));
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
            // ⚠ 2026-09-15（人物档案参数对照清单 · A 类修正）：画像尺寸对齐预览 `#v-pawn` 实测
            //   **88 × 88**（原 60 × 75 与预览不符；Hero 带随之为 96 = portraitH + 8，
            //   预览实测 Hero 带总高 103.6 含上下内边距 ✓）。
            //   ⚠ 预览是**圆形**（`border-radius:50%` + 1.5px accent 描边 + 径向渐变兜底）；
            //   IMGUI 无 `border-radius` ⇒ 按组件绘制规范记为 **B 类降级**（方形 + accent 描边
            //   + `PortraitsCache` 真图），差异已登记进对照清单，不作为缺陷。
            float portraitW = 88f;
            float portraitH = 88f;
            float portraitPad = 4f;
            float rowH = portraitH + 8f; // includes vertical breathing room

            // Tier medal inline row (.tier-inline in the design spec) grows the
            // header when a confirmed/estimated tier exists.
            WorkIntensityView intensity = cachedWorkIntensity;
            float tierRowH = (intensity != null && intensity.IsDefined) ? 20f : 0f;
            rowH += tierRowH;

            // v5.x（对照清单 P1-④）：Hero 带 **7 格指标带**（预览 `.hstat` 7 列 · HTML 1382-1390；
            //   人物档案清单 §5-1 确认的真缺口）。高 = 标签 18（Tiny）+ 数值 22（Small）= 40，
            //   起点 = contentY + 72（名字 26 + 角色行 18 + 在册行 18 + 间距）。
            //   必须参与 rowH 预算（铁律 4：先算空间预算再画），否则会被滚动边界截断。
            float hstatBandH = 40f;
            // 内容列排布（L7b/L8 后）：名行 y+2（22 高）→ 次行 y+26（18 高）⇒ 内容底 = 44，
            // 指标带起点 = 44 + 4 间距 = **48**（旧值 72 是按「名行 + 角色行 + 独立的 days 行」三段算的，
            // days 改成徽章并入名行后那 18px 不再需要）。
            float hstatTopOffset = tierRowH + 48f;
            rowH = Mathf.Max(rowH, hstatTopOffset + hstatBandH + 4f);

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
            // 【1:1 实测 · 补全方案 L7b/L8】名行 **27.6**（姓名 13px ≈ Small）+ 右端徽章（身份 · 已观测）。
            //   ⚠ 修前：姓名用 `GameFont.Medium`（**行高 30**）塞进 **26** 高 rect ⇒ 溢出 4px
            //     （「行高铁律」违规；被下一行压住下半截）。现改 `GameFont.Small`（行高 22）+ 24 高 rect。
            //   ⚠ 「已观测 N 天」由**独立文本行**改为**徽章**并入名行（预览实测 `.bdg dim` 82×18.4 与身份徽章同行）。
            // ══ 名行（预览 27.6）+ 次行（14.5）—— 与预览同构：名行右端放两枚徽章 ══
            float contentY = infoRect.y + tierRowH;
            string identityText = "PersonalChronicle.UI.Cover.Identity".Translate(
                RoleLabel(pawn.Role)).ToString();
            string daysText = BuildCoverDaysText(pawn);
            Text.Font = GameFont.Tiny;
            float daysBadgeW = Text.CalcSize(daysText).x + 14f;
            float identityW = Mathf.Min(150f, Text.CalcSize(identityText).x + 14f);
            float nameLimit = Mathf.Max(60f, infoRect.width - daysBadgeW - identityW - 18f);
            UIComponents.Label(new Rect(infoRect.x, contentY + 2f, nameLimit, 22f),
                UIComponents.TruncateToWidth(ObjectDisplayLabel(pawn), nameLimit, GameFont.Small),
                GameFont.Small, UITheme.Text, TextAnchor.MiddleLeft);
            float badgeY = contentY + 4f;
            Rect identityBadge = new Rect(infoRect.xMax - identityW, badgeY, identityW, 16f);
            UIComponents.Pill(identityBadge, identityText, RolePillColor(pawn.Role));
            Rect daysBadge = new Rect(identityBadge.x - 6f - daysBadgeW, badgeY, daysBadgeW, 16f);
            UIComponents.Badge(daysBadge, daysText, UITheme.Dim);

            // ══ 次行：角色描述（左）+ 在册章 / 建档中徽章（右端）══
            // v5.x「在册」判定：存活 且 属于当前殖民地人口 → 在册；死亡归档或已离开 → 不在册。
            // 不再用 IsArchived（DeathTick>0）作唯一依据 —— 那会把「有快照但还活着」误标为不在册。
            // ⚠ 预览 Hero 带实测**没有在册章**（无对应 `.bdg`）⇒ 保留属「功能多于预览」，已在清单登记。
            bool alive = service != null && service.IsCurrentlyEnlisted(pawn.StableId);
            string stampText = (alive
                ? "PersonalChronicle.UI.Cover.StampAlive"
                : "PersonalChronicle.UI.Cover.StampDead").Translate().ToString();
            float stampW = Mathf.Max(60f, Text.CalcSize(stampText).x + 18f);
            bool incompletePawn = pawn.JoinTick < 0L;
            string incompleteText = incompletePawn
                ? "PersonalChronicle.UI.Cover.Incomplete".Translate().ToString()
                : string.Empty;
            float incompleteW = incompletePawn ? Text.CalcSize(incompleteText).x + 16f : 0f;
            float roleLimit = Mathf.Max(60f,
                infoRect.width - stampW - incompleteW - (incompletePawn ? 12f : 6f));
            UIComponents.Label(new Rect(infoRect.x, contentY + 26f, roleLimit, 18f),
                UIComponents.TruncateToWidth(BuildCoverRoleDescription(pawn), roleLimit, GameFont.Tiny),
                GameFont.Tiny, UITheme.SecondaryText, TextAnchor.UpperLeft);
            float row2Mid = contentY + 26f + 9f;
            Rect stampRect = new Rect(infoRect.xMax - stampW, row2Mid - 10f, stampW, 20f);
            UIComponents.Pill(stampRect, stampText, alive ? UITheme.Alive : UITheme.Dead);
            if (incompletePawn)
            {
                DrawIncompleteBadge(new Rect(stampRect.x - 6f - incompleteW, row2Mid - 9f, incompleteW, 18f),
                    incompleteText);
            }

            // ---- v5.x: 7 格指标带（预览 `.hstat`：档案编号/入册/在册天数/工时/产出/击杀/事件）----
            //   文本全部来自缓存构建期（RebuildHeroStats），绘制路径零查询（PERF-001）。
            if (cachedHeroStats.Valid && infoRect.width >= 200f)
            {
                string[] hstatLabels =
                {
                    "PersonalChronicle.UI.Cover.Hstat.Id".Translate().ToString(),
                    "PersonalChronicle.UI.Cover.Hstat.Joined".Translate().ToString(),
                    "PersonalChronicle.UI.Cover.Hstat.Days".Translate().ToString(),
                    "PersonalChronicle.UI.Cover.Hstat.Hours".Translate().ToString(),
                    "PersonalChronicle.UI.Cover.Hstat.Produced".Translate().ToString(),
                    "PersonalChronicle.UI.Cover.Hstat.Kills".Translate().ToString(),
                    "PersonalChronicle.UI.Cover.Hstat.Events".Translate().ToString()
                };
                string[] hstatValues =
                {
                    cachedHeroStats.Id, cachedHeroStats.Joined, cachedHeroStats.Days,
                    cachedHeroStats.Hours, cachedHeroStats.Produced, cachedHeroStats.Kills,
                    cachedHeroStats.Events
                };
                // 【1:1 实测】预览 `.hstat` 7 格 **gap = 9**（每格 227.6 宽，合计 1647.5 = 7×227.6 + 6×9）；
                //   旧值 6 ⇒ 7 格各宽 2.6px。⚠ 总高 40 是实机下限（Tiny 18 + Small 22），
                //   预览为 31.5（8.5px + 12.5px 两行）⇒ 已登记 B 类降级（LAND-007 行高下限）。
                const float hgap = 9f;
                float cellW = (infoRect.width - hgap * 6f) / 7f;
                float hstatY = infoRect.y + hstatTopOffset;
                for (int i = 0; i < 7; i++)
                {
                    float cx = infoRect.x + (cellW + hgap) * i;
                    UIComponents.Label(new Rect(cx, hstatY, cellW, 18f),
                        UIComponents.TruncateToWidth(hstatLabels[i], cellW, GameFont.Tiny),
                        GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);
                    Rect valueRect = new Rect(cx, hstatY + 18f, cellW, 22f);
                    UIComponents.Label(valueRect,
                        UIComponents.TruncateToWidth(hstatValues[i], cellW, GameFont.Small),
                        GameFont.Small, UITheme.Text, TextAnchor.UpperLeft);
                    // LAND-008：被截断的单元格信息必须可达 —— 悬停给全值。
                    TooltipHandler.TipRegion(valueRect, hstatLabels[i] + ": " + hstatValues[i]);
                }
            }

            return y + rowH;
        }

        /// <summary>Hero 带 7 格指标带的行文本（预览 `.hstat`）——缓存构建期一次拼好。</summary>
        private struct HeroStatLine
        {
            public bool Valid;
            public string Id;
            public string Joined;
            public string Days;
            public string Hours;
            public string Produced;
            public string Kills;
            public string Events;
        }

        private HeroStatLine cachedHeroStats;

        /// <summary>
        /// Hero 指标带取数（缓存构建期调用，与 <c>ClearDetailCache</c> / 详情刷新同节奏）：
        /// 入册日与名册表同源（<c>FormatDate</c>）；工时与榨取总账同源
        /// （<c>GetCachedTotalWorkTicks</c>）；事件数走 <c>CountEventReferences</c>（内存计数，零服务查询）。
        /// </summary>
        private void RebuildHeroStats(PawnObject pawn, long nowTicks, IArchiveService service)
        {
            cachedHeroStats = new HeroStatLine
            {
                Valid = pawn != null,
                Id = pawn != null ? (pawn.StableId ?? string.Empty) : string.Empty,
                Joined = pawn != null && pawn.JoinTick > 0L ? FormatDate(pawn.JoinTick) : UnknownCell(),
                Days = pawn != null && pawn.JoinTick > 0L && nowTicks > pawn.JoinTick
                    ? ((nowTicks - pawn.JoinTick) / GenDate.TicksPerDay).ToString("N0")
                    : UnknownCell(),
                Hours = (GetCachedTotalWorkTicks() / (double)GenDate.TicksPerHour).ToString("F1"),
                Produced = pawn != null && pawn.Production != null
                    ? pawn.Production.TotalQuantity.ToString("N0") : "0",
                Kills = pawn != null ? (pawn.MeleeKills + pawn.RangedKills).ToString("N0") : "0",
                Events = pawn != null ? CountEventReferences(pawn.StableId).ToString("N0") : "0"
            };
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
                // ⚠ 2026-09-15：描边改用 **accent**（预览 `.portrait{border:1.5px solid var(--accent)}`）。
                //   原 `Widgets.DrawBox`（默认灰框）与预览的强调色描边不符。
                UIComponents.Border(rect, UITheme.Accent);

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
            // v5.x（对照清单 P1-②）：预览已把原 4 格卡片**捏合为一条算式带** ——
            // 产出 − 工时（含「折算成本」子行）= 净收益；去掉「周平均」格
            // （周均已由图内季粒度体现，重复）。Keyed 三键沿用：OutputValue / TotalWork / Net。
            float headH = 26f;
            UIComponents.DrawSubsectionHeader(new Rect(rect.x, y, rect.width, headH),
                "PersonalChronicle.UI.Ledger.Title");

            float bandY = y + headH + UITheme.SpaceXxs;
            float bandH = 68f;
            Rect band = new Rect(rect.x + UITheme.CardPadX, bandY,
                Mathf.Max(120f, rect.width - UITheme.CardPadX * 2f), bandH);
            DrawLedgerFormulaBand(band, pawn);

            // 口径脚注（预览 HTML 1464-1467）：净收益 = 产出市值 − 折算工时 × 2 银/h。
            Rect note = new Rect(band.x, band.yMax + 3f, band.width, 18f);
            UIComponents.Label(note,
                "PersonalChronicle.UI.Ledger.FormulaNote".Translate().ToString(),
                GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);
            return note.yMax;
        }

        /// <summary>
        /// 算式带（预览 HTML 1420-1436）：单条卡片底上排「产出 − 工时 = 净收益」。
        /// 段宽比例 1 : 1 : 1.15（预览 flex），减号/等号为语言无关算术符号。
        /// </summary>
        private void DrawLedgerFormulaBand(Rect rect, PawnObject pawn)
        {
            Widgets.DrawBoxSolid(rect, UITheme.Card);
            UIComponents.Border(rect, UITheme.BorderSoft);

            bool known = pawn.JoinTick >= 0L;
            string unknown = "PersonalChronicle.UI.Ledger.UnknownValue".Translate().ToString();
            float hours = known ? (float)(GetCachedTotalWorkTicks() / (double)GenDate.TicksPerHour) : 0f;
            float realised = cachedProductionSummary != null ? cachedProductionSummary.TotalMarketValue : 0f;
            string costText = known && hours > 0f
                ? "PersonalChronicle.UI.Ledger.CostFormat".Translate(
                    (NamedArgument)FormatSilver(hours * NetLedgerHourlyCostRate)).ToString()
                : string.Empty;

            const float signW = 24f;
            float segW = (rect.width - signW * 2f - 24f) / 3.15f; // 24f = 段内右间距合计
            float x = rect.x + 12f;
            float seg1W = segW;
            DrawLedgerSegment(new Rect(x, rect.y, seg1W, rect.height),
                "PersonalChronicle.UI.Ledger.OutputValue".Translate().ToString(),
                known && realised > 0f ? FormatSilver(realised) : (known ? "0" : unknown),
                "PersonalChronicle.UI.Ledger.OutputValueUnit".Translate().ToString(),
                string.Empty, UITheme.Text);
            x += seg1W;
            UIComponents.Label(new Rect(x, rect.y, signW, rect.height), "-",
                GameFont.Small, UITheme.Dim, TextAnchor.MiddleCenter);
            x += signW;
            float seg2W = segW;
            DrawLedgerSegment(new Rect(x, rect.y, seg2W, rect.height),
                "PersonalChronicle.UI.Ledger.TotalWork".Translate().ToString(),
                known ? hours.ToString("0.0") : unknown,
                "PersonalChronicle.UI.Ledger.TotalWorkUnit".Translate().ToString(),
                costText, UITheme.Info);
            x += seg2W;
            UIComponents.Label(new Rect(x, rect.y, signW, rect.height), "=",
                GameFont.Small, UITheme.Accent, TextAnchor.MiddleCenter);
            x += signW;
            DrawLedgerSegment(new Rect(x, rect.y, Mathf.Max(0f, rect.xMax - 12f - x), rect.height),
                "PersonalChronicle.UI.Ledger.Net".Translate().ToString(),
                known ? FormatSilver(NetLedgerSilver(pawn)) : unknown,
                "PersonalChronicle.UI.Ledger.NetUnit".Translate().ToString(),
                string.Empty, UITheme.Accent);
        }

        /// <summary>算式带单段：标签（Tiny）+ 大数值（Small·语义色）+ 单位（Tiny 内联）+ 可选子行。</summary>
        private static void DrawLedgerSegment(Rect rect, string label, string value, string unit,
            string subLabel, Color valueColor)
        {
            UIComponents.Label(new Rect(rect.x, rect.y + 5f, rect.width, 18f),
                UIComponents.TruncateToWidth(label, rect.width, GameFont.Tiny),
                GameFont.Tiny, UITheme.Dim, TextAnchor.UpperLeft);

            Text.Font = GameFont.Small;
            float valueW = Text.CalcSize(value).x;
            UIComponents.Label(new Rect(rect.x, rect.y + 23f, Mathf.Min(valueW + 4f, rect.width), 22f),
                value, GameFont.Small, valueColor, TextAnchor.MiddleLeft);
            float unitX = rect.x + valueW + 6f;
            if (unitX < rect.xMax && !string.IsNullOrEmpty(unit))
            {
                UIComponents.Label(new Rect(unitX, rect.y + 25f, Mathf.Max(0f, rect.xMax - unitX), 18f),
                    UIComponents.TruncateToWidth(unit, rect.xMax - unitX, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.UpperLeft);
            }
            if (!string.IsNullOrEmpty(subLabel))
            {
                UIComponents.Label(new Rect(rect.x, rect.y + 45f, rect.width, 18f),
                    UIComponents.TruncateToWidth(subLabel, rect.width, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.UpperLeft);
            }
        }

        /// <summary>每小时成本率（银/h，预览契约）——算式带子行与净值共用同一常量（GOV-006）。</summary>
        private const float NetLedgerHourlyCostRate = 2f;

        private float NetLedgerSilver(PawnObject pawn)
        {
            float realised = cachedProductionSummary != null
                ? cachedProductionSummary.TotalMarketValue : 0f;
            float hours = (float)GetCachedTotalWorkTicks() / RimWorld.GenDate.TicksPerHour;
            return realised - hours * NetLedgerHourlyCostRate;
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
                // v5.x（对照清单 P2-⑤）：预览前 2 行是 accent 渐变条、其余 info
                //   （`.bar.grad` / `.bar.info`），旧实现一律 Blood —— 把「压榨主项」与「其余」区分开。
                DrawProductionRow(new Rect(rect.x + UITheme.CardPadX, rowY, rect.width - UITheme.CardPadX * 2f, rowH),
                    label, valueText, pctText, share, i < 2 ? UITheme.Accent : UITheme.Info);
                rowY += rowH;
            }

            // v5.x（对照清单 P2-⑤）：预览 `#outStack` 五色堆叠条（HTML 1479-1485，h=12px）——
            //   段宽 = MarketValue 占比，五色与下行图例一一对应（语义色，非单色阶）。
            float innerW = rect.width - UITheme.CardPadX * 2f;
            const float stackH = 12f;
            Rect stack = new Rect(rect.x + UITheme.CardPadX, rowY + 3f, innerW, stackH);
            Widgets.DrawBoxSolid(stack, UITheme.BorderSoft);
            float sx = stack.x;
            for (int i = 0; i < take; i++)
            {
                float share = Mathf.Clamp01(sorted[i].MarketValue / totalValue);
                float segW = i == take - 1 ? Mathf.Max(0f, stack.xMax - sx) : stack.width * share;
                if (segW > 0.5f)
                {
                    Widgets.DrawBoxSolid(new Rect(sx, stack.y, segW, stack.height), OutputSegmentColor(i));
                }
                sx += segW;
            }
            rowY += stackH + 7f;

            // 图例行（预览 HTML 1486-1492）：同色小方块 + 名称 + 占比。
            float legendH = 16f;
            float cellW = take > 0 ? innerW / take : innerW;
            for (int i = 0; i < take; i++)
            {
                float lx = rect.x + UITheme.CardPadX + cellW * i;
                Rect swatch = new Rect(lx, rowY + (legendH - 7f) * 0.5f, 7f, 7f);
                Widgets.DrawBoxSolid(swatch, OutputSegmentColor(i));
                string item = ResolveProductionTypeLabel(sorted[i].DefName) + " "
                    + Mathf.RoundToInt(Mathf.Clamp01(sorted[i].MarketValue / totalValue) * 100f) + "%";
                float textW = Mathf.Max(0f, cellW - 13f);
                UIComponents.Label(new Rect(lx + 11f, rowY, textW, legendH),
                    UIComponents.TruncateToWidth(item, textW, GameFont.Tiny),
                    GameFont.Tiny, UITheme.Muted, TextAnchor.MiddleLeft);
            }
            rowY += legendH + 2f;

            // 合计行（预览 HTML 1493-1495）。
            string totalText = "PersonalChronicle.UI.OutputLedger.Total".Translate(
                (NamedArgument)(cachedProductionSummary != null
                    ? cachedProductionSummary.TotalQuantity : 0)).ToString();
            UIComponents.Label(new Rect(rect.x + UITheme.CardPadX, rowY, innerW, 18f),
                totalText, GameFont.Tiny, UITheme.SecondaryText, TextAnchor.MiddleLeft);
            return rowY + 18f;
        }

        /// <summary>产出堆叠条/图例的段色（预览 `.bar.grad`/`.info` 与五段五色：accent 金 / alive 绿 / text 米白 / muted 灰 / dead 深红）。</summary>
        private static Color OutputSegmentColor(int index)
        {
            switch (index)
            {
                case 0: return UITheme.Accent;
                case 1: return UITheme.Alive;
                case 2: return UITheme.Text;
                case 3: return UITheme.Muted;
                default: return UITheme.Dead;
            }
        }

        private static void DrawProductionRow(Rect rect, string label, string valueText, string pctText,
            float share, Color barColor)
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
            UIComponents.ProgressBar(bar, Mathf.Clamp01(share), barColor);
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

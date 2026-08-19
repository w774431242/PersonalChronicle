using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// Partial of <see cref="ArchiveMainTabWindow"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveMainTabWindow : MainTabWindow
    {

        private float DrawObjectHeader(Rect rect, float y, IArchiveService service)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width, 28f), ObjectDisplayLabel(cachedDetailObject));
            y += 32f;

            if (cachedDetailObject is PawnObject pawn)
            {
                PawnRole displayRole = pawn.Role;
                Pawn livePawn = service.GetLivePawn(pawn.StableId);
                if (ChronicleColonistScanner.TryClassify(livePawn, out PawnRole liveRole))
                {
                    displayRole = liveRole;
                }
                float px = DrawPill(rect.x, y, pawn.IsArchived
                        ? "PersonalChronicle.UI.Dead".Translate().ToString()
                        : "PersonalChronicle.UI.Alive".Translate().ToString(),
                    pawn.IsArchived ? DeadPill : AlivePill);
                DrawPill(px, y, RoleLabel(displayRole), RolePillColor(displayRole));
                string meta = KindLabel(pawn);
                if (!string.IsNullOrEmpty(FactionLabel(pawn)))
                {
                    meta = meta + " · " + FactionLabel(pawn);
                }
                meta = meta + " · " + "PersonalChronicle.UI.JoinDate".Translate().ToString() + " " + FormatDate(pawn.JoinTick);
                DrawMetaLine(rect, y + 30f, meta);
            }
            else if (cachedDetailObject is ThingObject thing)
            {
                DrawPill(rect.x, y, CategoryLabel(thing.CategoryKey), BluePill);
                DrawMetaLine(rect, y + 30f, ThingDefLabel(thing.ThingDefName));
            }
            else
            {
                DrawPill(rect.x, y, CategoryLabel(cachedDetailObject.CategoryKey), BluePill);
                DrawMetaLine(rect, y + 30f, ObjectSubLabel(cachedDetailObject));
            }

            Text.Font = GameFont.Small;
            return y + 54f;
        }

        private static void DrawMetaLine(Rect rect, float y, string text)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Tiny;
            GUI.color = ArchiveUiStyle.SecondaryText;
            Widgets.Label(new Rect(rect.x, y, rect.width, 18f), text);
            GUI.color = prevColor;
            Text.Font = prevFont;
        }

        private void DrawLegacyTab(Rect rect, IArchiveService service)
        {
            ReadModels.LegacyView legacy = cachedLegacy ?? new ReadModels.LegacyView();
            float y = rect.y;
            float w = rect.width;

            // 传承称号 (epithet).
            if (!string.IsNullOrEmpty(legacy.TitleText))
            {
                UIComponents.Label(
                    new Rect(rect.x, y, w, UITheme.FontBodyLineHeight + 4f),
                    legacy.TitleText, GameFont.Small, UITheme.Accent);
                y += UITheme.FontBodyLineHeight + 8f;
            }

            // 传承评价 (verdict).
            if (!string.IsNullOrEmpty(legacy.VerdictText))
            {
                UIComponents.Label(
                    new Rect(rect.x, y, w, UITheme.FontBodyLineHeight * 2f),
                    legacy.VerdictText, GameFont.Small, UITheme.Text);
                y += UITheme.FontBodyLineHeight * 2f + UITheme.SpaceSm;
            }

            if (legacy.IsEmpty)
            {
                UIComponents.Label(
                    new Rect(rect.x, y, w, 22f),
                    "PersonalChronicle.UI.Legacy.Empty".Translate().ToString(),
                    GameFont.Small, UITheme.Muted);
                return;
            }

            // 传承概要 (summary KPI).
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Legacy.Summary".Translate().ToString());
            float cellW = (w - UITheme.SpaceXs * 3f) / 4f;
            float cellH = UIComponents.StatCellMinHeight;
            Rect c1 = new Rect(rect.x, y, cellW, cellH);
            Rect c2 = new Rect(c1.xMax + UITheme.SpaceXs, y, cellW, cellH);
            Rect c3 = new Rect(c2.xMax + UITheme.SpaceXs, y, cellW, cellH);
            Rect c4 = new Rect(c3.xMax + UITheme.SpaceXs, y, cellW, cellH);
            UIComponents.StatCell(c1, "PersonalChronicle.UI.Legacy.CreatedBy".Translate().ToString(),
                legacy.CreatedByText ?? "—");
            UIComponents.StatCell(c2, "PersonalChronicle.UI.Legacy.CreatedAt".Translate().ToString(),
                legacy.CreatedAtText ?? "—");
            UIComponents.StatCell(c3, "PersonalChronicle.UI.Legacy.Gen".Translate().ToString(),
                legacy.GenCount.ToString(),
                "PersonalChronicle.UI.Legacy.GenNote".Translate().ToString());
            UIComponents.StatCell(c4, "PersonalChronicle.UI.Legacy.TotalKills".Translate().ToString(),
                legacy.TotalKills.ToString());
            y += cellH + UITheme.SpaceSm;

            // Current holder line.
            y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Legacy.CurrentHolder".Translate().ToString(),
                legacy.CurrentHolderText ?? "—");

            // 历届持有者 (holder table), collapsible past a cap.
            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Legacy.Holders".Translate().ToString());
            if (legacy.Holders == null || legacy.Holders.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x, y, w, 22f),
                    "PersonalChronicle.UI.Legacy.NoHolders".Translate().ToString(),
                    GameFont.Small, UITheme.Muted);
                return;
            }

            const int cap = 5;
            bool expanded = legacyExpanded;
            IReadOnlyList<ReadModels.LegacyHolderView> shown = expanded
                ? legacy.Holders
                : SubList(legacy.Holders, cap);

            // Header row.
            // v4.9.1: rebalanced widths so 2-line CJK date strings ("5500年 翠象 翠1天")
            // have room to wrap instead of being clipped. Wider From/To (110f), Dur
            // (74f), Kills (54f); remark narrows to consume the rest. Floor at 80f so
            // very narrow layouts still keep a usable remark column.
            float colGen = 46f;
            float colHolder = 100f;
            float colFrom = 110f;
            float colTo = 110f;
            float colDur = 74f;
            float colKills = 54f;
            float colRemark = w - (colGen + colHolder + colFrom + colTo + colDur + colKills);
            if (colRemark < 80f) colRemark = 80f;
            string[] headerKeys =
            {
                "PersonalChronicle.UI.Legacy.ColGen",
                "PersonalChronicle.UI.Legacy.ColHolder",
                "PersonalChronicle.UI.Legacy.ColFrom",
                "PersonalChronicle.UI.Legacy.ColTo",
                "PersonalChronicle.UI.Legacy.ColDur",
                "PersonalChronicle.UI.Legacy.ColKills",
                "PersonalChronicle.UI.Legacy.ColRemark"
            };
            float[] colW = { colGen, colHolder, colFrom, colTo, colDur, colKills, colRemark };
            float x = rect.x;
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            for (int i = 0; i < headerKeys.Length; i++)
            {
                UIComponents.Label(new Rect(x, y, colW[i], UITheme.FontBodyLineHeight),
                    headerKeys[i].Translate().ToString(), GameFont.Tiny, UITheme.Muted);
                x += colW[i];
            }
            y += UITheme.FontBodyLineHeight + 2f;
            UIComponents.Rule(new Rect(rect.x, y, w, 1f), UITheme.BorderSoft);
            y += 3f;

            // Data rows.
            for (int i = 0; i < shown.Count; i++)
            {
                ReadModels.LegacyHolderView h = shown[i];
                if (h == null) continue;
                // v4.9.1: row height must accommodate 2-line CJK From/To text. Use the
                // largest of (single-line baseline, measured From/To height) so short
                // rows stay compact while long dates get full room without clipping.
                float singleLineRowH = UITheme.FontBodyLineHeight + 6f;
                // v4.9.1: CalcHeight is relative to the CURRENT Text.Font, but the
                // From/To cells render in GameFont.Tiny. Pin the font to Tiny for the
                // measurement (and restore) so multi-line height matches the actual
                // glyph box instead of drifting with the caller's font state.
                GameFont measurePrevFont = Verse.Text.Font;
                Verse.Text.Font = GameFont.Tiny;
                // Measure at the ACTUAL render width (col width minus the 4f inner
                // padding the label rects use), otherwise a wider measurement width
                // under-reports wrap lines and the row can clip the last line.
                float fromRenderW = Mathf.Max(1f, colFrom - 4f);
                float toRenderW = Mathf.Max(1f, colTo - 4f);
                float fromH = !string.IsNullOrEmpty(h.FromText)
                    ? Verse.Text.CalcHeight(h.FromText, fromRenderW)
                    : singleLineRowH;
                float toH = !string.IsNullOrEmpty(h.ToText)
                    ? Verse.Text.CalcHeight(h.ToText, toRenderW)
                    : singleLineRowH;
                Verse.Text.Font = measurePrevFont;
                float rowH = Mathf.Max(singleLineRowH, Mathf.Max(fromH, toH) + 6f);
                // Plus a small bottom inset so two-line rows breathe.
                if (rowH > singleLineRowH) rowH += 4f;
                Rect row = new Rect(rect.x, y, w, rowH);

                if (h.IsCurrent)
                {
                    // v4.9.1: Accent (NameColor) at 0.18 alpha — the global AccentSoft
                    // token sits at 0.15 which fades on dark panels; bumping this row's
                    // tint slightly keeps the current holder unmistakably highlighted
                    // without leaving the industrial-terminal palette. Rendered via the
                    // TintedBox component so the window never hand-edits GUI.color.
                    UIComponents.TintedBox(row, UITheme.WithAlpha(UITheme.Accent, 0.18f));
                }

                // Generation cell: loan rows show the loan badge instead of a gen.
                if (h.IsLoan)
                {
                    UIComponents.Badge(
                        new Rect(rect.x, y + 2f, 42f, UITheme.FontBodyLineHeight - 2f),
                        "PersonalChronicle.UI.Legacy.Loan".Translate().ToString(),
                        UITheme.Dim);
                }
                else
                {
                    UIComponents.Label(new Rect(rect.x + 2f, y, colGen - 2f, rowH),
                        h.Generation > 0
                            ? "PersonalChronicle.UI.Legacy.GenN".Translate(h.Generation).ToString()
                            : "—",
                        GameFont.Small, UITheme.Text);
                }

                // Holder: label + first badge. Inner padding 4f gives the badge
                // breathing room from the holder text (was -6f touching the badge).
                UIComponents.Label(new Rect(rect.x + colGen + 4f, y, colHolder - 4f, rowH),
                    h.HolderText ?? "—", GameFont.Small, UITheme.Text);
                if (h.IsFirst)
                {
                    UIComponents.Badge(
                        new Rect(rect.x + colGen + colHolder - 62f, y + 2f, 56f, UITheme.FontBodyLineHeight - 2f),
                        "PersonalChronicle.UI.Legacy.First".Translate().ToString(),
                        UITheme.Accent);
                }

                // From / To / Duration / Kills. Inner padding 4f so CJK date strings
                // don't touch the previous column's edge.
                UIComponents.Label(new Rect(rect.x + colGen + colHolder + 4f, y, colFrom - 4f, rowH),
                    h.FromText ?? "—", GameFont.Tiny, UITheme.Muted);
                UIComponents.Label(new Rect(rect.x + colGen + colHolder + colFrom + 4f, y, colTo - 4f, rowH),
                    h.ToText ?? "—", GameFont.Tiny, UITheme.Muted);
                UIComponents.Label(new Rect(rect.x + colGen + colHolder + colFrom + colTo + 4f, y, colDur - 4f, rowH),
                    h.DurationText ?? "—", GameFont.Tiny, UITheme.Muted);
                // Kills cell: non-zero kills rendered in Alive green at Medium size
                // so the data point stands out; zero stays in Muted Small to avoid
                // visual noise. Row height must accommodate the Medium line height
                // (~28f empirical) so the digit isn't clipped.
                GameFont killFont = h.KillCount > 0 ? GameFont.Medium : GameFont.Small;
                Color killColor = h.KillCount > 0 ? UITheme.Alive : UITheme.Muted;
                if (killFont == GameFont.Medium && rowH < 28f + 4f) rowH = 28f + 4f;
                UIComponents.Label(new Rect(rect.x + colGen + colHolder + colFrom + colTo + colDur + 4f, y, colKills - 4f, rowH),
                    h.KillCount.ToString(), killFont, killColor);

                // Remark (loan note) — fits in the remaining column with 4f right padding.
                UIComponents.Label(new Rect(rect.x + colGen + colHolder + colFrom + colTo + colDur + colKills + 4f,
                    y, colRemark - 8f, rowH),
                    h.RemarkText ?? "—", GameFont.Tiny, UITheme.Dim);

                // Click to navigate to the holder pawn.
                if (!string.IsNullOrEmpty(h.HolderStableId) && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, NavTarget.Pawn, h.HolderStableId, null);
                }
                UIComponents.Rule(new Rect(rect.x, row.yMax - 1f, w, 1f), UITheme.BorderHair);
                y += rowH;
            }
            GUI.color = prevColor;
            Verse.Text.Font = prevFont;

            // Collapse / expand toggle.
            if (legacy.Holders.Count > cap)
            {
                y += UITheme.SpaceXxs;
                string toggle = expanded
                    ? "PersonalChronicle.UI.Legacy.Collapse".Translate().ToString()
                    : "PersonalChronicle.UI.Legacy.ExpandAll".Translate(legacy.Holders.Count).ToString();
                Rect btn = new Rect(rect.x, y, w, 24f);
                if (Widgets.ButtonText(btn, toggle, true, false, true))
                {
                    legacyExpanded = !legacyExpanded;
                }
                y += 30f;
            }
        }

        private void DrawOriginTab(Rect rect, IArchiveService service)
        {
            ReadModels.ThingOriginView origin = cachedOrigin ?? new ReadModels.ThingOriginView();
            ReadModels.MakerChainView maker = cachedMakerChain ?? new ReadModels.MakerChainView();
            float y = rect.y;
            float w = rect.width;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Origin.Title".Translate().ToString());
            if (origin.IsEmpty)
            {
                UIComponents.Label(new Rect(rect.x, y, w, 22f),
                    "PersonalChronicle.UI.Origin.Empty".Translate().ToString(),
                    GameFont.Small, UITheme.Muted);
                return;
            }

            // Kind pill (来源 chip).
            UIComponents.Pill(new Rect(rect.x, y, 120f, 22f),
                origin.KindText ?? "—", UITheme.Accent);
            y += 30f;

            // From / Where rows.
            if (!string.IsNullOrEmpty(origin.FromStableId))
            {
                Rect row = new Rect(rect.x, y, w, UITheme.FontBodyLineHeight + 4f);
                DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Origin.From".Translate().ToString(),
                    origin.FromText ?? "—");
                if (Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, NavTarget.Pawn, origin.FromStableId, null);
                }
                y += UITheme.FontBodyLineHeight + 8f;
            }
            else
            {
                y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Origin.From".Translate().ToString(),
                    origin.FromText ?? "—");
            }
            if (!string.IsNullOrEmpty(origin.WhereText))
            {
                y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Origin.Where".Translate().ToString(),
                    origin.WhereText);
            }
            if (!string.IsNullOrEmpty(origin.NoteText))
            {
                y += UITheme.SpaceXs;
                UIComponents.Label(new Rect(rect.x, y, w, UITheme.FontBodyLineHeight * 2f),
                    origin.NoteText, GameFont.Small, UITheme.Muted);
                y += UITheme.FontBodyLineHeight * 2f + UITheme.SpaceSm;
            }

            // 工坊署名链 (maker chain): the crafter's later fate.
            if (!maker.IsEmpty)
            {
                DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Origin.MakerChain".Translate().ToString());
                Rect row = new Rect(rect.x, y, w, UITheme.FontBodyLineHeight + 4f);
                string makerText = maker.MakerText ?? "—";
                UIComponents.Label(new Rect(rect.x, y, w - 130f, UITheme.FontBodyLineHeight),
                    makerText, GameFont.Small, UITheme.Text);
                if (maker.MakerDiedByOwn)
                {
                    UIComponents.Badge(
                        new Rect(rect.x + w - 126f, y, 122f, UITheme.FontBodyLineHeight),
                        "PersonalChronicle.UI.Origin.MakerDiedByOwn".Translate().ToString(),
                        UITheme.Threat);
                }
                if (!string.IsNullOrEmpty(maker.MakerStableId) && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, NavTarget.Pawn, maker.MakerStableId, null);
                }
                y += UITheme.FontBodyLineHeight + 10f;
            }
        }

        private void DrawCoUseTab(Rect rect, IArchiveService service)
        {
            ReadModels.CoUseView coUse = cachedCoUse ?? new ReadModels.CoUseView();
            float y = rect.y;
            float w = rect.width;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.CoUse.Title".Translate().ToString());
            UIComponents.Label(new Rect(rect.x, y, w, UITheme.FontBodyLineHeight),
                "PersonalChronicle.UI.CoUse.Hint".Translate().ToString(),
                GameFont.Tiny, UITheme.Muted);
            y += UITheme.FontBodyLineHeight + 6f;

            if (coUse.IsEmpty || coUse.Rows == null || coUse.Rows.Count == 0)
            {
                UIComponents.Label(new Rect(rect.x, y, w, 22f),
                    "PersonalChronicle.UI.CoUse.Empty".Translate().ToString(),
                    GameFont.Small, UITheme.Muted);
                return;
            }

            float colName = 140f;
            float colDays = 60f;
            float colBar = w - colName - colDays - 16f;
            for (int i = 0; i < coUse.Rows.Count; i++)
            {
                ReadModels.CoUseRowView rowView = coUse.Rows[i];
                if (rowView == null) continue;
                float rowH = UITheme.FontBodyLineHeight + 4f;
                Rect row = new Rect(rect.x, y, w, rowH);
                UIComponents.Label(new Rect(rect.x, y, colName, rowH),
                    rowView.PawnText ?? "—", GameFont.Small, UITheme.Text);
                UIComponents.Label(new Rect(rect.x + colName, y, colDays, rowH),
                    rowView.SharedDays.ToString(), GameFont.Tiny, UITheme.Muted);
                Rect bar = new Rect(rect.x + colName + colDays + 8f, y + 4f, colBar, 10f);
                UIComponents.ProgressBar(bar, Mathf.Clamp01(rowView.SharePercent / 100f), UITheme.Accent);
                if (!string.IsNullOrEmpty(rowView.PawnStableId) && Widgets.ButtonInvisible(row))
                {
                    NavigateTarget(service, NavTarget.Pawn, rowView.PawnStableId, null);
                }
                UIComponents.Rule(new Rect(rect.x, row.yMax, w, 1f), UITheme.BorderHair);
                y += rowH + 4f;
            }
        }

        private void DrawDecommissionTab(Rect rect, IArchiveService service)
        {
            ReadModels.DecommissionView d = cachedDecommission ?? new ReadModels.DecommissionView();
            float y = rect.y;
            float w = rect.width;

            DrawSectionTitle(rect, ref y, "PersonalChronicle.UI.Decommission.Title".Translate().ToString());
            if (!d.HasRecord)
            {
                UIComponents.Label(new Rect(rect.x, y, w, 22f),
                    "PersonalChronicle.UI.Decommission.Empty".Translate().ToString(),
                    GameFont.Small, UITheme.Muted);
                return;
            }

            // Retire stamp.
            UIComponents.Pill(new Rect(rect.x, y, 120f, 22f),
                "PersonalChronicle.UI.Decommission.Stamp".Translate().ToString(), UITheme.Dim);
            y += 30f;

            Rect holderRow = new Rect(rect.x, y, w, UITheme.FontBodyLineHeight + 4f);
            y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Decommission.LastHolder".Translate().ToString(),
                d.LastHolderText ?? "—");
            if (!string.IsNullOrEmpty(d.LastHolderStableId) && Widgets.ButtonInvisible(holderRow))
            {
                NavigateTarget(service, NavTarget.Pawn, d.LastHolderStableId, null);
            }
            y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Decommission.Place".Translate().ToString(),
                d.LastPlaceText ?? "—");
            y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Decommission.ServiceDays".Translate().ToString(),
                d.ServiceDays > 0
                    ? d.ServiceDays + " " + "PersonalChronicle.UI.DaysUnit".Translate().ToString()
                    : "—");
            y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Decommission.LastBattle".Translate().ToString(),
                d.LastBattleText ?? "—");
            y = DrawDetailRow(rect.x, y, w, "PersonalChronicle.UI.Decommission.Date".Translate().ToString(),
                d.DateText ?? "—");
        }


    }
}

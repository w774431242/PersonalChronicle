// 职业档案 ITab · 总览子页（partial）。
// 范围：仅前端 UI 表达优化（视觉得分 + 信息层级），不触碰数据/后端/Read Model。
// 设计系统：全部经 UIComponents + UITheme；禁止散落 GUI.color / new Color。
// CJK 行高基线：大区标题 ≤ SectionTitle(24f) / 区块内 ValueRow ≥ 22f / 小字 ≥ 18f。
using System.Collections.Generic;
using PersonalChronicle.Archive.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Data;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    public partial class ITab_Pawn_Career
    {
        // 总览区块行高
        private const float OvValueRowH = 24f;

        private float DrawOverviewTab(Rect rect, Pawn pawn, DetailSnapshot snap)
        {
            CareerOverviewView ov = snap != null ? snap.CareerOverview : null;
            // v4.17 体检：滚动内容宽度预留滚动条位（右缘元素不再被滚动条覆盖）。
            float width = rect.width - UITheme.ScrollbarThickness;
            float viewH = CalcOverviewHeight(ov, width);
            Rect view = new Rect(0f, 0f, width, Mathf.Max(viewH, 1f));
            Widgets.BeginScrollView(rect, ref scroll, view);

            float             y = 0f;
            if (ov == null || !ov.HasData)
            {
                UIComponents.TintedBox(view, UITheme.Panel);
                UIComponents.Label(new Rect(view.x + 12f, view.y + 12f, view.width - 24f, 24f),
                    "PersonalChronicle.UI.Career.Ov.NoData".Translate().ToString(), UITheme.FontBody, UITheme.Muted);
                Widgets.EndScrollView();
                return viewH;
            }

            y = DrawIdentityBlock(view, y, width, ov);
            y = DrawPlanSection(view, y, width, pawn);
            y = DrawQualSection(view, y, width, ov);
            y = DrawNextTitleSection(view, y, width, ov);

            Widgets.EndScrollView();
            return viewH;
        }

        // ================= 职业身份（强锚点，带分隔节奏）=================
        private float DrawIdentityBlock(Rect view, float y, float width, CareerOverviewView ov)
        {
            float pad = UITheme.PanelPadding;
            float innerW = width - 2f * pad;

            // v4.17 体检：block 高 92f→104f —— MiniStat 值行（Small 22f）+ 标签行（Tiny 18f）
            // 单元需 40f，旧 16f/14f 矩形裁切 Medium 字体且 statY 越过面板底边。
            Rect block = new Rect(view.x, y, width, 104f);
            UIComponents.Panel(block, UITheme.Panel);
            UIComponents.Border(block, UITheme.BorderSoft);

            float bx = block.x + pad;
            float bw = block.width - 2f * pad;
            float by = block.y + 12f;

            // 主职称（大字 + 方向色）
            ProfessionalDirectionDef dirDef = TryGetDirectionDef(cachedSnapshot);
            Color accent = dirDef != null ? ParseHex(dirDef.colorHex, UITheme.Accent) : UITheme.Accent;
            string role = string.IsNullOrEmpty(ov.RoleName)
                ? "PersonalChronicle.UI.Career.Ov.Undefined".Translate().ToString()
                : ov.RoleName;
            UIComponents.Label(new Rect(bx, by, bw, 28f), role, UITheme.FontValue, accent);

            // 副标题：方向 · 技能 · 工时（一行小字）
            string sub = ov.RoleDesc ?? "";
            if (!string.IsNullOrEmpty(ov.SkillText))
            {
                sub = string.IsNullOrEmpty(sub) ? ov.SkillText : sub + " · " + ov.SkillText;
            }
            if (!string.IsNullOrEmpty(ov.HoursText))
            {
                sub = string.IsNullOrEmpty(sub) ? ov.HoursText : sub + " · " + ov.HoursText;
            }
            UIComponents.Label(new Rect(bx, by + 32f, bw, 20f), sub, UITheme.FontLabel, UITheme.Muted);

            // 指标行（制造/建造/研究 三宫格 + 著作 + 成果）
            float statY = by + 52f;
            float statGap = 10f;
            float statW = (bw - statGap * 4f) / 5f;
            DrawMiniStat(new Rect(bx, statY, statW, 40f), FormatValue(ov.Made), "PersonalChronicle.UI.Career.Ov.Metric.Made".Translate().ToString());
            DrawMiniStat(new Rect(bx + statW + statGap, statY, statW, 40f), FormatValue(ov.Built), "PersonalChronicle.UI.Career.Ov.Metric.Built".Translate().ToString());
            DrawMiniStat(new Rect(bx + (statW + statGap) * 2f, statY, statW, 40f), FormatValue(ov.Researched), "PersonalChronicle.UI.Career.Ov.Metric.Research".Translate().ToString());
            DrawMiniStat(new Rect(bx + (statW + statGap) * 3f, statY, statW, 40f), FormatValue(ov.Books), "PersonalChronicle.UI.Career.Ov.Books".Translate().ToString());
            DrawMiniStat(new Rect(bx + (statW + statGap) * 4f, statY, statW, 40f), FormatValue(ov.Results), "PersonalChronicle.UI.Career.Ov.Results".Translate().ToString());

            return y + block.height + UITheme.SpaceMd;
        }

        /// <summary>v4.17 体检：值行 Small 22f + 标签行 Tiny 18f（单元 40f，CJK 安全）。</summary>
        private static void DrawMiniStat(Rect r, string value, string label)
        {
            UIComponents.Label(new Rect(r.x, r.y, r.width, 22f), value, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(r.x, r.y + 22f, r.width, 18f), label, UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleLeft);
        }

        // ================= 职业规划（12 专业 chips + 「选」角标）=================
        private float DrawPlanSection(Rect view, float y, float width, Pawn pawn)
        {
            y = DrawSectionTitle(view, y, width, "PersonalChronicle.UI.Career.Ov.Plan".Translate());

            float btnH = 30f;
            Rect btnRect = new Rect(view.x, y, width, btnH);
            DrawPlanButton(btnRect, pawn);
            y += btnH + UITheme.SpaceXs;

            // 12 专业 chip 网格（4 列 x 3 行），选中小高亮、主方向青色「选」角标、
            // 适配分析 top1/高分 金色「荐」角标（对齐 HTML .plan-chip .rec）。
            int cols = 4;
            float gap = 8f;
            float chipH = 28f;
            float chipW = (width - gap * (cols - 1)) / cols;
            ProfessionalFitResult topFit = fitResults != null && fitResults.Count > 0 ? fitResults[0] : null;
            for (int i = 0; i < 12; i++)
            {
                int col = i % cols;
                int row = i / cols;
                string mkey = MajorKeys[i];
                bool selected = mkey == currentMajorKey;
                bool isTop1 = topFit != null && topFit.Major == mkey;
                Rect chip = new Rect(view.x + col * (chipW + gap), y + row * (chipH + gap), chipW, chipH);
                DrawPlanChip(chip, mkey, selected, isTop1, FitFor(fitResults, mkey));
            }
            y += 3 * (chipH + gap) + UITheme.SpaceMd;
            return y;
        }

        private void DrawPlanChip(Rect r, string majorKey, bool selected, bool isTop1, ProfessionalFitResult fit)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Verse.Text.Anchor;
            try
            {
                string label = MajorLabel(majorKey);
                string glyph = MajorGlyph(majorKey);
                Color chipBg = selected ? UITheme.PanelRaised : UITheme.Panel;
                UIComponents.TintedBox(r, chipBg);
                if (selected) UIComponents.Border(r, UITheme.PillGold);
                else UIComponents.Border(r, UITheme.BorderSoft);

                Rect glyphRect = new Rect(r.x + 4f, r.y + (r.height - 18f) / 2f, 18f, 18f);
                UIComponents.TintedBox(glyphRect, selected ? UITheme.PillGold : UITheme.PanelRaised);
                UIComponents.Label(glyphRect, glyph, UITheme.FontLabel, selected ? UITheme.Text : UITheme.Muted, TextAnchor.MiddleCenter);

                Rect textRect = new Rect(glyphRect.xMax + 4f, r.y, r.width - 22f - 18f, r.height);
                // v4.17 体检：英文专业名（如 "Animal Husbandry"）超 chipW 会换行被裁——
                // 截断加省略号（UI-009）。
                string clippedLabel = UIComponents.TruncateToWidth(label, textRect.width, GameFont.Small);
                Verse.Text.Font = GameFont.Small;
                Verse.Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = selected ? UITheme.Text : UITheme.Muted;
                Widgets.Label(textRect, clippedLabel);

                bool isMajor = IsMajorChosen(majorKey);
                // 角标规则（对齐 HTML .plan-chip .rec/.rank）：「选」青角标只给 primary；
                // 「荐」金角标给适配 top1 或 Fit≥80（与「选」互斥，选优先）。
                if (isMajor)
                {
                    Rect badge = new Rect(r.xMax - 18f, r.y + 2f, 16f, 14f);
                    UIComponents.TintedBox(badge, UITheme.Info);
                    UIComponents.Label(badge, "PersonalChronicle.UI.Career.Plan.Chosen".Translate(),
                        UITheme.FontLabel, UITheme.Text, TextAnchor.MiddleCenter);
                }
                else if (isTop1 || (fit != null && fit.Fit >= 80))
                {
                    Rect badge = new Rect(r.xMax - 18f, r.y + 2f, 16f, 14f);
                    UIComponents.TintedBox(badge, UITheme.PillGold);
                    UIComponents.Label(badge, "PersonalChronicle.UI.Career.Plan.Rec".Translate(),
                        UITheme.FontLabel, UITheme.Text, TextAnchor.MiddleCenter);
                }
                // 悬停明细：适配分 + 五维（对齐 HTML .plan-pop）。
                if (fit != null)
                {
                    string tip = "PersonalChronicle.UI.Career.Plan.Match.Overall".Translate(fit.Fit.ToString())
                        + "\n"
                        + FitDimText(fit);
                    TooltipHandler.TipRegion(r, new TipSignal(tip));
                }
            }
            finally
            {
                GUI.color = prevColor;
                Verse.Text.Font = prevFont;
                Verse.Text.Anchor = prevAnchor;
            }
        }

        private static ProfessionalFitResult FitFor(List<ProfessionalFitResult> fits, string majorKey)
        {
            if (fits == null || string.IsNullOrEmpty(majorKey)) return null;
            for (int i = 0; i < fits.Count; i++)
            {
                if (fits[i] != null && fits[i].Major == majorKey) return fits[i];
            }
            return null;
        }

        private static string FitDimText(ProfessionalFitResult fit)
        {
            return "PersonalChronicle.UI.Career.Plan.Dim.Skill".Translate() + " " + fit.SkillScore
                + "\n" + "PersonalChronicle.UI.Career.Plan.Dim.Practice".Translate() + " " + fit.PracticeScore
                + "\n" + "PersonalChronicle.UI.Career.Plan.Dim.Passion".Translate() + " " + fit.PassionScore
                + "\n" + "PersonalChronicle.UI.Career.Plan.Dim.Achieve".Translate() + " " + fit.AchievementScore
                + "\n" + "PersonalChronicle.UI.Career.Plan.Dim.Growth".Translate() + " " + fit.GrowthScore;
        }

        private void DrawPlanButton(Rect r, Pawn pawn)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Verse.Text.Anchor;
            try
            {
                Verse.Text.Font = GameFont.Small;
                if (Widgets.ButtonText(r, "PersonalChronicle.UI.Career.Ov.SetPrimary".Translate().ToString()))
                {
                    Find.WindowStack.Add(new Dialog_SetPrimaryDirection(
                        pawn,
                        Current.Game != null ? Current.Game.GetComponent<ChronicleGameComponent>() : null,
                        currentMajorKey,
                        () =>
                        {
                            scroll = Vector2.zero;
                        }));
                }
            }
            finally
            {
                GUI.color = prevColor;
                Verse.Text.Font = prevFont;
                Verse.Text.Anchor = prevAnchor;
            }
        }

        // ================= 当前资格状态（6 条件，图标化 badge）=================
        private float DrawQualSection(Rect view, float y, float width, CareerOverviewView ov)
        {
            y = DrawSectionTitle(view, y, width, "PersonalChronicle.UI.Career.Ov.Qual".Translate());
            if (ov.Qual == null || ov.Qual.Count == 0)
            {
                UIComponents.Label(new Rect(view.x, y, width, 20f),
                    "PersonalChronicle.UI.Career.Ov.NoQual".Translate().ToString(), UITheme.FontLabel, UITheme.Dim);
                return y + 24f + UITheme.SpaceMd;
            }

            y += UITheme.SpaceXs;
            float gap = 8f;
            int cols = 2;
            float rowH = 30f;
            float cellW = (width - gap * (cols - 1)) / cols;
            for (int i = 0; i < ov.Qual.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                Rect cell = new Rect(view.x + col * (cellW + gap), y + row * (rowH + gap), cellW, rowH);
                DrawQualCell(cell, ov.Qual[i]);
            }
            int rows = (ov.Qual.Count + cols - 1) / cols;
            y += rows * (rowH + gap) + UITheme.SpaceMd;
            return y;
        }

        private static void DrawQualCell(Rect r, CareerQualView q)
        {
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Verse.Text.Anchor;
            try
            {
                UIComponents.TintedBox(r, UITheme.Panel);
                UIComponents.Border(r, UITheme.BorderSoft);

                bool ok = q.StateKey == "ok";
                Rect dot = new Rect(r.x + 8f, r.y + (r.height - 12f) / 2f, 12f, 12f);
                UIComponents.TintedBox(dot, ok ? UITheme.PillGreen : UITheme.PillRed);

                Rect txt = new Rect(dot.xMax + 8f, r.y, r.width - 28f, r.height);
                Verse.Text.Font = GameFont.Small;
                Verse.Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = ok ? UITheme.Text : UITheme.Muted;
                Widgets.Label(txt, q.Label + "  " + q.Note);
            }
            finally
            {
                GUI.color = prevColor;
                Verse.Text.Font = prevFont;
                Verse.Text.Anchor = prevAnchor;
            }
        }

        // ================= 下一职称（进度条 + 5 档阶梯 + 缺口）=================
        private float DrawNextTitleSection(Rect view, float y, float width, CareerOverviewView ov)
        {
            y = DrawSectionTitle(view, y, width, "PersonalChronicle.UI.Career.Ov.NextTitle".Translate());
            if (string.IsNullOrEmpty(ov.NextTitle))
            {
                UIComponents.TintedBox(new Rect(view.x, y, width, 36f), UITheme.Panel);
                UIComponents.Label(new Rect(view.x + 10f, y, width - 20f, 36f),
                    "PersonalChronicle.UI.Career.Ov.Maxed".Translate().ToString(), UITheme.FontLabel, UITheme.PillGold, TextAnchor.MiddleLeft);
                return y + 36f + UITheme.SpaceMd;
            }

            float padX = UITheme.PanelPadding;
            float blockH = 30f + OvValueRowH * 2f + 8f + 16f;
            Rect block = new Rect(view.x, y, width, blockH);
            UIComponents.Panel(block, UITheme.Panel);
            UIComponents.Border(block, UITheme.BorderSoft);
            float bx = block.x + padX;
            float bw = block.width - 2f * padX;
            float by = block.y + 8f;

            UIComponents.Label(new Rect(bx, by, bw, 24f), ov.NextTitle, UITheme.FontValue, UITheme.Text);

            float barY = by + 28f;
            UIComponents.ProgressBar(new Rect(bx, barY, bw, 14f), Mathf.Clamp01(ov.Progress / 100f),
                UITheme.PillGold, ov.Progress + "%");

            // 缺口（若有；v4.17 体检：多缺口英文串换行被 18f 行高裁剪 → 截断）
            float gapY = barY + 20f;
            if (ov.NextGaps != null && ov.NextGaps.Count > 0)
            {
                string gaps = "PersonalChronicle.UI.Career.Ov.Gaps".Translate().ToString()
                    + " " + string.Join(" / ", ov.NextGaps);
                string clippedGaps = UIComponents.TruncateToWidth(gaps, bw, UITheme.FontLabel);
                UIComponents.Label(new Rect(bx, gapY, bw, 18f), clippedGaps, UITheme.FontLabel, UITheme.Dim);
            }

            return y + block.height + UITheme.SpaceMd;
        }

        // ================= 通用 =================
        private float DrawSectionTitle(Rect view, float y, float width, string title)
        {
            float h = 24f;
            UIComponents.SectionTitle(new Rect(view.x, y, width, h), y, title);
            return y + h + UITheme.SpaceXs;
        }

        private float CalcOverviewHeight(CareerOverviewView ov, float width)
        {
            float h = UITheme.SpaceMd;
            h += 104f + UITheme.SpaceMd;                      // 身份（含 5 指标格）
            h += 24f + UITheme.SpaceXs;                       // 规划标题
            h += 30f + UITheme.SpaceXs;                       // 设定按钮
            h += 3 * (28f + 8f) + UITheme.SpaceMd;            // 12 chips
            h += 24f + UITheme.SpaceXs;                       // 资格标题
            if (ov != null && ov.Qual != null && ov.Qual.Count > 0)
            {
                int rows = (ov.Qual.Count + 1) / 2;
                h += UITheme.SpaceXs + rows * (30f + 8f) + UITheme.SpaceMd;
            }
            else h += 24f + UITheme.SpaceMd;
            h += 24f + UITheme.SpaceXs;                       // 下一职称标题
            h += (string.IsNullOrEmpty(ov?.NextTitle) ? 36f : (30f + OvValueRowH * 2f + 8f + 16f)) + UITheme.SpaceMd;
            h += UITheme.SpaceMd;
            return h;
        }
    }
}

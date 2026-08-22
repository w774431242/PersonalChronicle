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
            // 原生 BeginScrollView 用法（对齐 ArchiveMainTabWindow）：不自定义背景，
            // 让 rimworld 窗口默认背景生效——避免自定义 TintedBox 干扰造成的白屏。
            float width = rect.width - 16f;
            float viewH = CalcOverviewHeight(ov, width);
            float viewHeight = Mathf.Max(rect.height, viewH);
            Rect viewRect = new Rect(rect.x, rect.y, width, viewHeight);
            Widgets.BeginScrollView(rect, ref scroll, viewRect);
            try
            {
                float y = viewRect.y + 4f;
                // v1.1.5：删除"暂无职业数据"空态分支——无论 HasData 与否都画全部区块，字段值
                // 由各 DrawXxx 用 `--` / `0` 占位；UI 不再因数据缺失而出现刺眼白色背景。
                y = DrawIdentityBlock(viewRect, y, width, ov);
                y = DrawPlanSection(viewRect, y, width, pawn);
                y = DrawQualSection(viewRect, y, width, ov);
                y = DrawNextTitleSection(viewRect, y, width, ov);
            }
            finally
            {
                Widgets.EndScrollView();
            }
            return viewH;
        }

        // ================= 职业身份（强锚点，带分隔节奏）=================
        private float DrawIdentityBlock(Rect view, float y, float width, CareerOverviewView ov)
        {
            float pad = UITheme.PanelPadding;
            float innerW = width - 2f * pad;

            // v4.17 体检：block 高 92f→104f —— MiniStat 值行（Small 22f）+ 标签行（Tiny 18f）
            // 单元需 40f，旧 16f/14f 矩形裁切 Medium 字体且 statY 越过面板底边。
            // v8.2 体检：删组标签+分隔线后恢复 104f——by+12 起 5 单元底 by+92，+12f 底 padding = 104f。
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

            // 指标行：制造产出 / 建造 / 研究 / 著作 / 重大成果（5 单元等宽，单元自带标签，无需额外分组标识）
            float statY = by + 52f;
            float statGap = 8f;
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
                bool selected = IsMajorChosen(mkey);
                bool isTop1 = topFit != null && topFit.Major == mkey;
                Rect chip = new Rect(view.x + col * (chipW + gap), y + row * (chipH + gap), chipW, chipH);
                DrawPlanChip(chip, pawn, mkey, selected, isTop1, FitFor(fitResults, mkey));
            }
            y += 3 * (chipH + gap) + UITheme.SpaceMd;
            return y;
        }

        private void DrawPlanChip(Rect r, Pawn pawn, string majorKey, bool selected, bool isTop1, ProfessionalFitResult fit)
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

                // 点击职业类 chip → 打开该一级专业下的二级方向选择（移除旧「设定主方向」按钮入口）。
                // ButtonInvisible 必须先于视觉绘制之外的逻辑注册；此处放在 try 内末尾，
                // 与 DrawDirectionCard 同样遵守 BeginScrollView 内点击铁律（chip 不在 scrollview 内，
                // 但统一风格避免误判）。
                if (Widgets.ButtonInvisible(r))
                {
                    Find.WindowStack.Add(new Dialog_SetPrimaryDirection(
                        pawn,
                        Current.Game != null ? Current.Game.GetComponent<ChronicleGameComponent>() : null,
                        majorKey,
                        currentMajorKey,
                        () => { scroll = Vector2.zero; }));
                    Event.current.Use();
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

        // ================= 当前资格状态（6 条件，按资格流程时间线单列）=================
        // 流程顺序：基础条件（等级/资历/评分）→ 准入（实践/理论）→ 评审（论文/答辩）。
        // 单列 6 行，每行=条件名（左）+ 状态点 + 值（右），信息流清晰可控。
        private float DrawQualSection(Rect view, float y, float width, CareerOverviewView ov)
        {
            y = DrawSectionTitle(view, y, width, "PersonalChronicle.UI.Career.Ov.Qual".Translate());
            // v1.1.5：无论数据是否完整都画 7 行占位（label + `--`），不用"NoQual"提示；
            // ov.Qual 满 7 行 → 直接画；空 → 7 行 `--` 占位。
            // v9：新增"评级评审"行（v2.0 §14 Review 期），统一化资格状态前端管理。
            string[] qualLabels =
            {
                "PersonalChronicle.UI.Career.Qual.Level",
                "PersonalChronicle.UI.Career.Qual.Time",
                "PersonalChronicle.UI.Career.Qual.Score",
                "PersonalChronicle.UI.Career.Qual.Practical",
                "PersonalChronicle.UI.Career.Qual.Theory",
                "PersonalChronicle.UI.Career.Qual.Defense",
                "PersonalChronicle.UI.Career.Qual.Review"
            };
            y += UITheme.SpaceXs;
            float rowH = 28f;
            float gap = 6f;
            int totalCells = qualLabels.Length;
            for (int i = 0; i < totalCells; i++)
            {
                Rect cell = new Rect(view.x, y + i * (rowH + gap), width, rowH);
                CareerQualView q = (ov.Qual != null && i < ov.Qual.Count) ? ov.Qual[i]
                    : new CareerQualView
                    {
                        Label = qualLabels[i].Translate().ToString(),
                        Note = "--",
                        StateKey = "wait",
                        StateText = "--"
                    };
                DrawQualCell(cell, q);
            }
            y += totalCells * (rowH + gap) + UITheme.SpaceMd;
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
                // 状态点：满足=绿，未满足=暗红（与配色板一致，未满足不必强警示）。
                Rect dot = new Rect(r.x + 8f, r.y + (r.height - 12f) / 2f, 12f, 12f);
                UIComponents.TintedBox(dot, ok ? UITheme.PillGreen : UITheme.Dead);

                // 3 列布局：左条件名（30%） + 中资格要求（40%） + 右状态文本（30%）
                // 左：条件名
                Rect labelRect = new Rect(dot.xMax + 8f, r.y, r.width * 0.3f, r.height);
                Verse.Text.Font = GameFont.Small;
                Verse.Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = ok ? UITheme.Text : UITheme.Muted;
                Widgets.Label(labelRect, q.Label);

                // 中：资格要求描述（对齐 P9 HTML：如"Q_Precision_Specialist ≥ 38"、"相关工作 ≥ 1200000 tick"）
                Rect noteRect = new Rect(r.x + r.width * 0.3f + 16f, r.y, r.width * 0.4f, r.height);
                Verse.Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = ok ? UITheme.Alive : UITheme.Muted;
                Widgets.Label(noteRect, q.Note);

                // 右：状态文本（"✓ 满足"/"○ 未满足"）
                Rect stateRect = new Rect(r.x + r.width * 0.7f + 16f, r.y, r.width * 0.3f - 24f, r.height);
                Verse.Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = ok ? UITheme.Alive : UITheme.Dead;
                Widgets.Label(stateRect, (ok ? "✓ " : "○ ") + q.StateText);

                // 悬停：显示结构化透视（多行模板 + <b> 分节，maxWidth 360 防挤压）
                if (!string.IsNullOrEmpty(q.Tooltip))
                {
                    TooltipHandler.TipRegion(r, new TipSignal(q.Tooltip, 360f));
                }
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
            // v1.1.5：空数据 vs 已封顶 区分。无数据（HasData=false）→ 未评定；无 NextTitle 但 HasData=true → 已封顶。
            if (string.IsNullOrEmpty(ov.NextTitle))
            {
                string emptyLabel = ov != null && !ov.HasData
                    ? "PersonalChronicle.UI.Career.Ov.Undefined".Translate().ToString()
                    : "PersonalChronicle.UI.Career.Ov.Maxed".Translate().ToString();
                Color emptyColor = ov != null && !ov.HasData ? UITheme.Dim : UITheme.PillGold;
                UIComponents.TintedBox(new Rect(view.x, y, width, 36f), UITheme.Panel);
                UIComponents.Label(new Rect(view.x + 10f, y, width - 20f, 36f),
                    emptyLabel, UITheme.FontLabel, emptyColor, TextAnchor.MiddleLeft);
                return y + 36f + UITheme.SpaceMd;
            }

            float padX = UITheme.PanelPadding;
            // 缺口 pill 动态高度：每行最多 3 个，自动换行。
            int gapCount = (ov.NextGaps != null) ? ov.NextGaps.Count : 0;
            int gapRows = gapCount == 0 ? 0 : Mathf.CeilToInt(gapCount / 3f);
            float gapsH = gapCount > 0 ? 18f + gapRows * 24f : 0f;
            float blockH = 8f + 24f + 20f + 14f + gapsH + 8f;
            Rect block = new Rect(view.x, y, width, blockH);
            UIComponents.Panel(block, UITheme.Panel);
            UIComponents.Border(block, UITheme.BorderSoft);
            float bx = block.x + padX;
            float bw = block.width - 2f * padX;
            float by = block.y + 8f;

            UIComponents.Label(new Rect(bx, by, bw, 24f), ov.NextTitle, UITheme.FontValue, UITheme.Text);

            float barY = by + 28f;
            // 进度条：金填充 + 抬升底色（v8 体检：Steampunk 主题的 accent=青绿，与 PanelRaised
            // 形成明显对比，色相混淆时仍可凭饱和度识别）
            float share01 = Mathf.Clamp01(ov.Progress / 100f);
            Rect barRect = new Rect(bx, barY, bw, 14f);
            Widgets.DrawBoxSolid(barRect, UITheme.PanelRaised);
            if (share01 > 0f)
            {
                Widgets.DrawBoxSolid(new Rect(bx, barY, bw * share01, 14f), UITheme.PillGold);
            }
            // 进度文字（居中）
            UIComponents.Label(new Rect(bx, barY, bw, 14f), ov.Progress + "%", UITheme.FontLabel, UITheme.Text, TextAnchor.MiddleCenter);

            // 缺口（横向 pill 列表，自动换行，避免「/」连写挤一行）
            float gapY = barY + 20f;
            if (gapCount > 0)
            {
                UIComponents.Label(new Rect(bx, gapY, bw, 18f),
                    "PersonalChronicle.UI.Career.Ov.Gaps".Translate().ToString(),
                    UITheme.FontLabel, UITheme.Muted);
                float pillY = gapY + 20f;
                float pillX = bx;
                float pillH = 20f;
                float pillGap = 6f;
                for (int i = 0; i < gapCount; i++)
                {
                    string g = ov.NextGaps[i];
                    Verse.Text.Font = GameFont.Tiny;
                    float pillW = Verse.Text.CalcSize(g).x + 16f;
                    Verse.Text.Font = GameFont.Small;
                    if (pillX + pillW > bx + bw)
                    {
                        pillX = bx;
                        pillY += pillH + pillGap;
                    }
                    Rect pill = new Rect(pillX, pillY, pillW, pillH);
                    UIComponents.TintedBox(pill, UITheme.PanelRaised);
                    UIComponents.Border(pill, UITheme.Warn);
                    UIComponents.Label(pill, g, UITheme.FontLabel, UITheme.Warn, TextAnchor.MiddleCenter);
                    pillX += pillW + pillGap;
                }
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
            h += 104f + UITheme.SpaceMd;                      // 身份（含 5 指标格 + 底内边距）
            h += 24f + UITheme.SpaceXs;                       // 规划标题
            h += 30f + UITheme.SpaceXs;                       // 设定按钮
            h += 3 * (28f + 8f) + UITheme.SpaceMd;            // 12 chips
            h += 24f + UITheme.SpaceXs;                       // 资格标题
            if (ov != null && ov.Qual != null && ov.Qual.Count > 0)
            {
                h += UITheme.SpaceXs + ov.Qual.Count * (28f + 6f) + UITheme.SpaceMd;
            }
            else h += 24f + UITheme.SpaceMd;
            h += 24f + UITheme.SpaceXs;                       // 下一职称标题
            if (string.IsNullOrEmpty(ov?.NextTitle))
            {
                h += 36f + UITheme.SpaceMd;
            }
            else
            {
                int gc = (ov.NextGaps != null) ? ov.NextGaps.Count : 0;
                int grow = gc == 0 ? 0 : Mathf.CeilToInt(gc / 3f);
                h += (8f + 24f + 20f + 14f + (gc > 0 ? 18f + grow * 24f : 0f) + 8f) + UITheme.SpaceMd;
            }
            h += UITheme.SpaceMd;
            return h;
        }
    }
}

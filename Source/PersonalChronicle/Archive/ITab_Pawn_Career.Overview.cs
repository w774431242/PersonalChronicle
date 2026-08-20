// ITab_Pawn_Career partial：总览子页（职业规划/职业身份/资格状态/预检/下一职称）。
// ARC-013 文件治理，物理切片零契约改动；见主文件 ITab_Pawn_Career.cs 类文档。
using System;
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    public partial class ITab_Pawn_Career
    {
        // ================= 子页：总览 =================
        private void DrawOverviewTab(Rect rect, Pawn pawn, DetailSnapshot snap)
        {
            Rect view = new Rect(rect.x, rect.y, rect.width, rect.height);
            float innerW = view.width - UITheme.ScrollbarThickness;
            float contentH = EstimateOverviewH(snap);
            contentH = Mathf.Max(contentH, view.height);
            Widgets.BeginScrollView(view, ref scroll, new Rect(view.x, view.y, innerW, contentH));
            try
            {
                float y = view.y;
                y = DrawPlanSection(view, y, innerW, pawn);
                y = DrawIdentityBlock(view, y, innerW, snap);
                y = DrawQualBlock(view, y, innerW, snap);
                y = DrawPreCheckBlock(view, y, innerW, snap);
                y = DrawNextTitleBlock(view, y, innerW, snap);
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static float EstimateOverviewH(DetailSnapshot snap)
        {
            CareerOverviewView ov = snap != null ? snap.CareerOverview : null;
            float h = 0f;
            h += 30f + 12f + 3f * (ChipH + 6f);            // 职业规划（标题 + 3 行 chips）
            h += 30f + 150f;                               // 职业身份
            h += 30f + (ov != null && ov.Qual != null ? ov.Qual.Count * 30f : 0f) + 8f;      // 资格状态
            h += 30f + (ov != null && ov.PreCheck != null ? ov.PreCheck.Count * 30f : 0f) + 8f; // 预检
            h += 30f + 130f;                               // 下一职称
            return h + UITheme.SpaceMd * 4f;
        }

        // ---- 职业规划（12 专业适配分析 chips）----
        private float DrawPlanSection(Rect view, float y, float innerW, Pawn pawn)
        {
            UIComponents.SectionTitle(new Rect(view.x, y, innerW, 0f), y,
                "PersonalChronicle.UI.Career.Plan.Title".Translate().ToString());
            y += UITheme.SectionTitleHeight;

            // P2 缺口补全：玩家主方向选择入口（推荐≠强制；写入 primaryDirection）。
            // 仅当已有方向 Def 时才显示按钮，避免空列表误导。
            if (DefDatabase<ProfessionalDirectionDef>.AllDefsListForReading.Count > 0)
            {
                float btnW = 132f;
                Rect dirBtn = new Rect(view.x + innerW - btnW, y - UITheme.SectionTitleHeight - 2f, btnW, 24f);
                Color btnPrevColor = GUI.color;
                GameFont btnPrevFont = Verse.Text.Font;
                TextAnchor btnPrevAnchor = Verse.Text.Anchor;
                try
                {
                    string currentDir = CurrentPrimaryDirection(pawn);
                    string btnLabel = string.IsNullOrEmpty(currentDir)
                        ? "PersonalChronicle.UI.Career.SetDirection.Button".Translate().ToString()
                        : "PersonalChronicle.UI.Career.SetDirection.ButtonCurrent".Translate(
                            DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(currentDir)?.LabelCap ?? currentDir).ToString();
                    if (Widgets.ButtonText(dirBtn, btnLabel))
                    {
                        OpenSetDirectionDialog(pawn);
                    }
                }
                finally
                {
                    GUI.color = btnPrevColor;
                    Verse.Text.Font = btnPrevFont;
                    Verse.Text.Anchor = btnPrevAnchor;
                }
            }

            if (fitResults == null || fitResults.Count == 0)
            {
                UIComponents.Label(new Rect(view.x, y, innerW, 22f),
                    "PersonalChronicle.UI.Career.Plan.Empty".Translate().ToString(),
                    UITheme.FontBody, UITheme.Muted);
                return y + 26f;
            }

            float chipGap = 6f;
            float perCol = ChipW + chipGap;
            int perRow = Mathf.Max(1, (int)(innerW / perCol));
            Color prevColor = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            TextAnchor prevAnchor = Verse.Text.Anchor;
            try
            {
                for (int i = 0; i < fitResults.Count; i++)
                {
                    ProfessionalFitResult r = fitResults[i];
                    int col = i % perRow;
                    int row = i / perRow;
                    Rect chip = new Rect(view.x + col * perCol, y + row * (ChipH + chipGap), ChipW, ChipH);
                    bool selected = string.Equals(currentMajorKey, r.Major, StringComparison.Ordinal);
                    bool isTop1 = i == 0;
                    bool recommend = r.Fit >= 80 || isTop1;
                    // P2 缺口补全：持久化选定的主方向（primaryDirection）对应专业显示「选」角标（推荐≠强制）。
                    bool chosen = IsMajorChosen(pawn, r.Major);

                    UIComponents.TintedBox(chip, selected ? UITheme.PanelRaised : UITheme.Panel);
                    UIComponents.Border(chip, selected ? UITheme.PillGold : UITheme.BorderSoft);
                    if (recommend && !selected)
                    {
                        UIComponents.Border(chip, UITheme.PillGold);
                    }

                    // 排名 + 专业名 + 适配分。
                    Verse.Text.Font = GameFont.Tiny;
                    Verse.Text.Anchor = TextAnchor.MiddleLeft;
                    GUI.color = UITheme.Dim;
                    Widgets.Label(new Rect(chip.x + 4f, chip.y, 20f, ChipH), (i + 1).ToString());
                    GUI.color = selected ? UITheme.Text : UITheme.Muted;
                    Widgets.Label(new Rect(chip.x + 20f, chip.y, ChipW - 44f, ChipH),
                        MajorLabel(r.Major));
                    Verse.Text.Anchor = TextAnchor.MiddleRight;
                    GUI.color = r.Fit >= 65 ? UITheme.PillGold : UITheme.Dim;
                    Widgets.Label(new Rect(chip.x + ChipW - 30f, chip.y, 26f, ChipH), r.Fit.ToString());
                    Verse.Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = prevColor;
                    Verse.Text.Font = prevFont;
                    Verse.Text.Anchor = prevAnchor;

                    // 「荐」角标（右上角小标签）。
                    if (recommend)
                    {
                        Rect rec = new Rect(chip.xMax - 14f, chip.y - 5f, 16f, 14f);
                        UIComponents.Badge(rec, "PersonalChronicle.UI.Career.Plan.Rec".Translate().ToString(), UITheme.PillGold);
                    }

                    // 「选」角标（左上角青色，玩家持久化选定的主方向）。
                    if (chosen)
                    {
                        Rect chosenBadge = new Rect(chip.x - 4f, chip.y - 5f, 16f, 14f);
                        UIComponents.Badge(chosenBadge, "PersonalChronicle.UI.Career.Plan.Chosen".Translate().ToString(), UITheme.PillGreen);
                    }

                    // 悬停：五维明细 tooltip（星级 + 五维 + 优势/短板 + 推荐方向）。
                    TooltipHandler.TipRegion(chip, new TipSignal(BuildPlanTooltip(r)));

                    // 点击：选中为当前专业（身份卡联动；再点取消）。
                    if (Widgets.ButtonInvisible(chip))
                    {
                        currentMajorKey = string.Equals(currentMajorKey, r.Major, StringComparison.Ordinal)
                            ? null
                            : r.Major;
                    }
                }
            }
            finally
            {
                GUI.color = prevColor;
                Verse.Text.Font = prevFont;
                Verse.Text.Anchor = prevAnchor;
            }
            return y + 3f * (ChipH + chipGap) + UITheme.SpaceSm;
        }

        private string BuildPlanTooltip(ProfessionalFitResult r)
        {
            string title = MajorLabel(r.Major) + " · " + r.Fit + "/100";
            string tag = r.Fit >= 85
                ? "PersonalChronicle.UI.Career.Plan.Match.High".Translate().ToString()
                : (r.Fit >= 65
                    ? "PersonalChronicle.UI.Career.Plan.Match.Mid".Translate().ToString()
                    : "PersonalChronicle.UI.Career.Plan.Match.Low".Translate().ToString());
            int stars = r.Fit >= 85 ? 5 : (r.Fit >= 65 ? 4 : 3);
            string starLine = new string('★', stars) + new string('☆', 5 - stars) + "  " + tag;
            string nl = "\n";
            string dims = "PersonalChronicle.UI.Career.Plan.Dim.Skill".Translate() + " " + r.SkillScore + nl
                + "PersonalChronicle.UI.Career.Plan.Dim.Practice".Translate() + " " + r.PracticeScore + nl
                + "PersonalChronicle.UI.Career.Plan.Dim.Passion".Translate() + " " + r.PassionScore + nl
                + "PersonalChronicle.UI.Career.Plan.Dim.Achieve".Translate() + " " + r.AchievementScore + nl
                + "PersonalChronicle.UI.Career.Plan.Dim.Growth".Translate() + " " + r.GrowthScore;
            string pros = r.Pros.Count > 0
                ? "✓ " + JoinSkillNames(r.Pros)
                : "PersonalChronicle.UI.Career.Plan.NoPros".Translate().ToString();
            string cons = r.Cons.Count > 0
                ? "△ " + JoinSkillNames(r.Cons)
                : "";
            string dir = "PersonalChronicle.UI.Career.Plan.Dir".Translate() + MajorDirLabel(r.Major);
            return title + nl + starLine + nl + nl + dims + nl + nl
                + "PersonalChronicle.UI.Career.Plan.Basis".Translate() + pros + (cons.Length > 0 ? nl + cons : "")
                + nl + dir;
        }

        private string JoinSkillNames(List<string> defNames)
        {
            if (defNames == null || defNames.Count == 0) return "";
            List<string> names = new List<string>();
            for (int i = 0; i < defNames.Count; i++)
            {
                SkillDef sd = DefDatabase<SkillDef>.GetNamedSilentFail(defNames[i]);
                names.Add(sd != null ? sd.LabelCap : defNames[i]);
            }
            return string.Join("、", names);
        }

        // ---- 主方向选择入口（P2 缺口补全）----
        private static string CurrentPrimaryDirection(Pawn pawn)
        {
            if (pawn == null) return null;
            IArchiveService service = PersonalChronicleMod.ArchiveService;
            if (service == null) return null;
            PawnObject po = service.GetObject(pawn.GetUniqueLoadID()) as PawnObject;
            if (po == null || po.CareerData == null || po.CareerData.Professional == null) return null;
            return po.CareerData.Professional.primaryDirection;
        }

        /// <summary>判断某 12 专业 key 是否对应玩家持久化选定的主方向（primaryDirection）。
        /// 映射：方向 Def.profession == Major key（如 Manufacturing_Precision.profession = "Manufacturing"）。</summary>
        private bool IsMajorChosen(Pawn pawn, string majorKey)
        {
            if (pawn == null || string.IsNullOrEmpty(majorKey)) return false;
            string dir = CurrentPrimaryDirection(pawn);
            if (string.IsNullOrEmpty(dir)) return false;
            ProfessionalDirectionDef dirDef = DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(dir);
            if (dirDef == null) return false;
            return string.Equals(dirDef.profession, majorKey, System.StringComparison.Ordinal);
        }

        private void OpenSetDirectionDialog(Pawn pawn)
        {
            if (pawn == null || Current.Game == null) return;
            ChronicleGameComponent component = Current.Game.GetComponent<ChronicleGameComponent>();
            if (component == null) return;
            string current = CurrentPrimaryDirection(pawn);
            // 选择落盘后重建本 ITab 快照，使身份卡/规划区即时反映。
            Dialog_SetPrimaryDirection dialog = new Dialog_SetPrimaryDirection(
                pawn, component, current,
                () =>
                {
                    IArchiveService service = PersonalChronicleMod.ArchiveService;
                    if (service != null) EnsureSnapshot(service, pawn);
                });
            Find.WindowStack.Add(dialog);
        }

        // ---- 职业身份 ----
        private float DrawIdentityBlock(Rect view, float y, float innerW, DetailSnapshot snap)
        {
            CareerOverviewView ov = snap != null ? snap.CareerOverview : null;
            UIComponents.SectionTitle(new Rect(view.x, y, innerW, 0f), y,
                "PersonalChronicle.UI.Career.Ov.Identity".Translate().ToString());
            y += UITheme.SectionTitleHeight;

            Rect panel = new Rect(view.x, y, innerW, 74f);
            UIComponents.Panel(panel, UITheme.Panel);
            string roleName = ov != null && !string.IsNullOrEmpty(ov.RoleName)
                ? ov.RoleName
                : "PersonalChronicle.UI.Career.Ov.Undefined".Translate().ToString();
            UIComponents.Label(new Rect(panel.x + 10f, panel.y + 6f, panel.width - 20f, 26f),
                UIComponents.TruncateToWidth(roleName, panel.width - 20f, UITheme.FontValue),
                UITheme.FontValue, UITheme.Text);
            string roleDesc = ov != null ? (ov.RoleDesc ?? "--") : "--";
            UIComponents.Label(new Rect(panel.x + 10f, panel.y + 34f, panel.width - 20f, 18f),
                UIComponents.TruncateToWidth(roleDesc, panel.width - 20f, UITheme.FontLabel),
                UITheme.FontLabel, UITheme.Muted);
            string nextLine = "PersonalChronicle.UI.Career.Ov.NextLine".Translate(
                (ov != null && !string.IsNullOrEmpty(ov.NextTitle))
                    ? ov.NextTitle
                    : "PersonalChronicle.UI.Career.Ov.TopTitle".Translate().ToString());
            UIComponents.Label(new Rect(panel.x + 10f, panel.y + 54f, panel.width - 20f, 18f),
                UIComponents.TruncateToWidth(nextLine, panel.width - 20f, UITheme.FontLabel),
                UITheme.FontLabel, UITheme.PillGold);
            y += panel.height + UITheme.SpaceXs;

            // 数据值采用简单直白的文本列表（对齐用户要求：不额外卡片样式、紧凑省空间）。
            // 行 = label（左）+ value（右对齐），7 行：专业技能/相关工时/重大成果/专业著作/制造/建造/研究。
            // UI-001：Made/Built/Researched 全部来自快照（Provider 已从 RecordCountByType 聚合），
            // 窗口不直查/直聚合 Domain（移除旧 CountEvents 直查）。
            CareerFactCounts fc = snap != null ? snap.FactCounts : null;
            int made = fc != null ? fc.ItemProduced : 0;
            int built = fc != null ? fc.ConstructionCompleted : 0;
            int researched = fc != null ? fc.ResearchCompleted : 0;
            y = ValueRow(view, y, innerW,
                "PersonalChronicle.UI.Career.Ov.Skill".Translate().ToString(),
                ov != null ? (ov.SkillText ?? "--") : "--");
            y = ValueRow(view, y, innerW,
                "PersonalChronicle.UI.Career.Ov.Hours".Translate().ToString(),
                ov != null ? (ov.HoursText ?? "--") : "--");
            y = ValueRow(view, y, innerW,
                "PersonalChronicle.UI.Career.Ov.Results".Translate().ToString(),
                ov != null ? ov.Results.ToString() : "0");
            y = ValueRow(view, y, innerW,
                "PersonalChronicle.UI.Career.Ov.Books".Translate().ToString(),
                ov != null ? ov.Books.ToString() : "0");
            y = ValueRow(view, y, innerW,
                "PersonalChronicle.UI.Career.Ov.Metric.Made".Translate().ToString(), made.ToString());
            y = ValueRow(view, y, innerW,
                "PersonalChronicle.UI.Career.Ov.Metric.Built".Translate().ToString(), built.ToString());
            y = ValueRow(view, y, innerW,
                "PersonalChronicle.UI.Career.Ov.Metric.Research".Translate().ToString(), researched.ToString());
            y += UITheme.SpaceSm;
            return y;
        }

        /// <summary>文本列表行：label 左 / value 右对齐，无卡片样式。</summary>
        private float ValueRow(Rect view, float y, float innerW, string label, string value)
        {
            UIComponents.Label(new Rect(view.x, y, innerW * 0.42f, 20f),
                UIComponents.TruncateToWidth(label, innerW * 0.42f, UITheme.FontBody),
                UITheme.FontBody, UITheme.Text);
            UIComponents.Label(new Rect(view.x + innerW * 0.42f, y, innerW * 0.58f, 20f),
                UIComponents.TruncateToWidth(value ?? "--", innerW * 0.58f, UITheme.FontBody),
                UITheme.FontBody, UITheme.Muted, TextAnchor.UpperRight);
            return y + 22f;
        }

        // ---- 当前资格状态（6 条件行）----
        private float DrawQualBlock(Rect view, float y, float innerW, DetailSnapshot snap)
        {
            CareerOverviewView ov = snap != null ? snap.CareerOverview : null;
            UIComponents.SectionTitle(new Rect(view.x, y, innerW, 0f), y,
                "PersonalChronicle.UI.Career.Ov.QualState".Translate().ToString());
            y += UITheme.SectionTitleHeight;
            if (ov == null || ov.Qual == null || ov.Qual.Count == 0)
            {
                UIComponents.Label(new Rect(view.x, y, innerW, 22f),
                    "PersonalChronicle.UI.Career.Ov.NoQual".Translate().ToString(),
                    UITheme.FontBody, UITheme.Muted);
                return y + 26f;
            }
            for (int i = 0; i < ov.Qual.Count; i++)
            {
                CareerQualView q = ov.Qual[i];
                if (q == null) continue;
                Rect row = new Rect(view.x, y, innerW, 28f);
                UIComponents.Label(new Rect(row.x, row.y, innerW * 0.22f, 22f),
                    q.Label ?? "", UITheme.FontBody, UITheme.Text);
                UIComponents.Label(new Rect(row.x + innerW * 0.22f, row.y, innerW * 0.5f, 22f),
                    UIComponents.TruncateToWidth(q.Note ?? "", innerW * 0.5f, UITheme.FontLabel),
                    UITheme.FontLabel, UITheme.Muted);
                Color stateColor = string.Equals(q.StateKey, "ok", StringComparison.Ordinal) ? UITheme.PillGreen : UITheme.PillRed;
                UIComponents.Pill(new Rect(row.x + innerW * 0.72f, row.y + 3f, innerW * 0.28f - 4f, 20f),
                    q.StateText ?? "", stateColor);
                y += 30f;
            }
            y += UITheme.SpaceSm;
            return y;
        }

        // ---- 资格预检（✓/●/○ 条件行）----
        private float DrawPreCheckBlock(Rect view, float y, float innerW, DetailSnapshot snap)
        {
            CareerOverviewView ov = snap != null ? snap.CareerOverview : null;
            UIComponents.SectionTitle(new Rect(view.x, y, innerW, 0f), y,
                "PersonalChronicle.UI.Career.Ov.PreCheck".Translate().ToString());
            y += UITheme.SectionTitleHeight;
            if (ov == null || ov.PreCheck == null || ov.PreCheck.Count == 0)
            {
                UIComponents.Label(new Rect(view.x, y, innerW, 22f),
                    "PersonalChronicle.UI.Career.Ov.NoQual".Translate().ToString(),
                    UITheme.FontBody, UITheme.Muted);
                return y + 26f;
            }
            for (int i = 0; i < ov.PreCheck.Count; i++)
            {
                CareerPreCheckView p = ov.PreCheck[i];
                if (p == null) continue;
                Rect row = new Rect(view.x, y, innerW, 28f);
                bool done = string.Equals(p.StateKey, "done", StringComparison.Ordinal);
                bool pending = string.Equals(p.StateKey, "pending", StringComparison.Ordinal);
                string icon = done ? "✓" : (pending ? "●" : "○");
                Color iconColor = done ? UITheme.PillGreen : (pending ? UITheme.PillGold : UITheme.Dim);
                UIComponents.Label(new Rect(row.x, row.y, 24f, 22f), icon, UITheme.FontBody, iconColor);
                UIComponents.Label(new Rect(row.x + 24f, row.y, innerW * 0.5f, 22f),
                    p.Label ?? "", UITheme.FontBody, UITheme.Text);
                UIComponents.Label(new Rect(row.x + innerW * 0.5f, row.y, innerW * 0.5f - 4f, 22f),
                    UIComponents.TruncateToWidth(p.StateText ?? "", innerW * 0.5f - 4f, UITheme.FontLabel),
                    UITheme.FontLabel, done ? UITheme.PillGreen : UITheme.Muted, TextAnchor.UpperRight);
                y += 30f;
            }
            y += UITheme.SpaceSm;
            return y;
        }

        // ---- 下一职称 ----
        private float DrawNextTitleBlock(Rect view, float y, float innerW, DetailSnapshot snap)
        {
            CareerOverviewView ov = snap != null ? snap.CareerOverview : null;
            UIComponents.SectionTitle(new Rect(view.x, y, innerW, 0f), y,
                "PersonalChronicle.UI.Career.Ov.NextTitle".Translate().ToString());
            y += UITheme.SectionTitleHeight;

            Rect panel = new Rect(view.x, y, innerW, 116f);
            UIComponents.Panel(panel, UITheme.Panel);
            string nextTitle = (ov != null && !string.IsNullOrEmpty(ov.NextTitle))
                ? ov.NextTitle
                : "PersonalChronicle.UI.Career.Ov.TopTitle".Translate().ToString();
            UIComponents.Label(new Rect(panel.x + 10f, panel.y + 6f, panel.width - 20f, 24f),
                UIComponents.TruncateToWidth(nextTitle, panel.width - 20f, UITheme.FontValue),
                UITheme.FontValue, UITheme.Text);

            int progress = ov != null ? ov.Progress : 0;
            Rect barRow = new Rect(panel.x + 10f, panel.y + 36f, panel.width - 20f, 20f);
            UIComponents.Label(new Rect(barRow.x, barRow.y, 90f, 18f),
                "PersonalChronicle.UI.Career.Ov.Progress".Translate().ToString(),
                UITheme.FontLabel, UITheme.Muted);
            UIComponents.ProgressBar(new Rect(barRow.x + 90f, barRow.y + 2f, barRow.width - 150f, 14f),
                progress / 100f, UITheme.Accent);
            UIComponents.Label(new Rect(barRow.x + barRow.width - 52f, barRow.y, 52f, 18f),
                progress + "%", UITheme.FontLabel, UITheme.Accent, TextAnchor.UpperRight);

            Rect gapRow = new Rect(panel.x + 10f, panel.y + 62f, panel.width - 20f, 22f);
            if (ov != null && ov.NextGaps != null && ov.NextGaps.Count > 0)
            {
                UIComponents.Label(new Rect(gapRow.x, gapRow.y, 90f, 18f),
                    "PersonalChronicle.UI.Career.Ov.Gaps".Translate().ToString(),
                    UITheme.FontLabel, UITheme.Muted);
                string gapText = string.Join(" / ", ov.NextGaps);
                UIComponents.Label(new Rect(gapRow.x + 90f, gapRow.y, gapRow.width - 90f, 20f),
                    UIComponents.TruncateToWidth(gapText, gapRow.width - 90f, UITheme.FontLabel),
                    UITheme.FontLabel, UITheme.Warn);
            }
            else
            {
                UIComponents.Label(new Rect(gapRow.x, gapRow.y, gapRow.width, 18f),
                    "PersonalChronicle.UI.Career.Ov.NoGap".Translate().ToString(),
                    UITheme.FontLabel, UITheme.Muted);
            }

            // 依赖链 + 口径注释（对齐 HTML .dep-chain）。
            UIComponents.Label(new Rect(panel.x + 10f, panel.y + 90f, panel.width - 20f, 18f),
                "PersonalChronicle.UI.Career.Ov.DepChain".Translate().ToString(),
                UITheme.FontLabel, UITheme.Dim);
            y += panel.height + UITheme.SpaceMd;
            return y;
        }
    }
}

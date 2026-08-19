// ITab_Pawn_Career partial：资格子页（P9 落地——考试/理论/论文/答辩/评级评审流程入口与状态）。
// ARC-013 文件治理，物理切片零契约改动；见主文件 ITab_Pawn_Career.cs 类文档。
// 规则继承：顺序门控（理论须实践过、论文须理论提交、答辩须论文完成）+ 按档隔离
// （QualificationDefName 匹配）+ 评级评审期（RunQualification 门控，答辩后 Review→到期授予）。
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.ReadModels;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    public partial class ITab_Pawn_Career
    {
        // 职称 5 档元数据（对齐 Defs/QualificationDefs.xml 精密制造方向；显示门槛用）
        private static readonly string[] QualTierNames =
        {
            "精密制造初级技工", "精密制造中级技工", "精密制造高级技工", "精密制造技师", "精密制造高级技师"
        };
        private static readonly string[] QualTierKeys = { "junior", "assistant", "senior", "specialist", "master" };
        private static readonly int[] QualTierLevels = { 5, 15, 25, 38, 45 };
        private static readonly int[] QualTierHours = { 25, 80, 240, 480, 720 };

        /// <summary>资格子页（P9）：职称阶梯 + 申报条件 + 流程操作（考试/论文/答辩）+ 评级评审。</summary>
        private void DrawQualificationTab(Rect rect, Pawn pawn, DetailSnapshot snap)
        {
            PawnObject po = snap != null ? snap.DetailObject as PawnObject : null;
            CareerData cd = po != null ? po.CareerData : null;
            if (cd == null)
            {
                UIComponents.Label(rect, "PersonalChronicle.UI.Career.Qual.Empty".Translate(),
                    UITheme.FontBody, UITheme.Muted);
                return;
            }

            // 当前申报档 = 已授予最高档 + 1
            int grantedIdx = HighestGrantedTier(cd);
            int applyIdx = grantedIdx + 1;
            bool capped = applyIdx >= QualTierNames.Length;

            Rect view = new Rect(rect.x, rect.y, rect.width, rect.height);
            float y = view.y;
            float contentW = view.width - 18f;

            // ── 职称阶梯（5 档） ──
            UIComponents.Label(new Rect(view.x, y, contentW, 20f),
                "PersonalChronicle.UI.Career.Qual.Ladder".Translate(), UITheme.FontLabel, UITheme.Muted);
            y += 24f;
            float stepW = (contentW - 4f * 6f) / 5f;
            for (int i = 0; i < QualTierNames.Length; i++)
            {
                Rect step = new Rect(view.x + i * (stepW + 6f), y, stepW, 52f);
                Color fill = UITheme.Panel;
                string state = "locked";
                if (i <= grantedIdx) state = "granted";
                else if (i == applyIdx && !capped) state = "applying";
                if (i == applyIdx && !capped)
                {
                    fill = UITheme.PanelRaised;
                    UIComponents.Border(step, UITheme.Accent);
                }
                else if (state == "granted")
                {
                    fill = UITheme.PanelRaised;
                    UIComponents.Border(step, UITheme.PillGreen);
                }
                else
                {
                    UIComponents.Border(step, UITheme.BorderSoft);
                }
                UIComponents.TintedBox(step, fill);
                UIComponents.Label(new Rect(step.x + 4f, step.y + 4f, step.width - 8f, 16f),
                    "Lv" + (i + 1) + " · ≥" + QualTierLevels[i] + " · ≥" + QualTierHours[i] + "h",
                    UITheme.FontLabel, UITheme.Muted);
                UIComponents.Label(new Rect(step.x + 4f, step.y + 20f, step.width - 8f, 18f),
                    UIComponents.TruncateToWidth(QualTierNames[i], step.width - 8f, UITheme.FontLabel),
                    UITheme.FontLabel, state == "granted" ? UITheme.PillGreen : (state == "applying" ? UITheme.Accent : UITheme.Dim));
                string stLabel = state == "granted" ? "✓" : (state == "applying" ? "申报中" : "·");
                UIComponents.Label(new Rect(step.x + 4f, step.y + 38f, step.width - 8f, 12f),
                    stLabel, UITheme.FontLabel, state == "granted" ? UITheme.PillGreen : (state == "applying" ? UITheme.Accent : UITheme.Dim));
            }
            y += 58f;

            if (capped)
            {
                UIComponents.Label(new Rect(view.x, y, contentW, 20f),
                    "PersonalChronicle.UI.Career.Qual.Capped".Translate(QualTierNames[QualTierNames.Length - 1]),
                    UITheme.FontBody, UITheme.Muted);
                return;
            }

            QualificationDef def = DefDatabase<QualificationDef>.GetNamedSilentFail(
                "Q_Precision_" + QualTierKeys[applyIdx]);
            if (def == null)
            {
                UIComponents.Label(new Rect(view.x, y, contentW, 20f),
                    "PersonalChronicle.UI.Career.Qual.NoDef".Translate(),
                    UITheme.FontBody, UITheme.Muted);
                return;
            }

            y += 6f;
            UIComponents.Rule(new Rect(view.x, y, contentW, 1f), UITheme.BorderSoft);
            y += 10f;

            // ── 申报条件检查（7 行，对齐 QualificationEvaluator 门槛链 + 评审期） ──
            UIComponents.Label(new Rect(view.x, y, contentW, 20f),
                "PersonalChronicle.UI.Career.Qual.Conditions".Translate(QualTierNames[applyIdx]),
                UITheme.FontLabel, UITheme.Muted);
            y += 26f;

            int level = 0;
            if (cd.Professional != null && !string.IsNullOrEmpty(def.professionalSkillDefName))
            {
                ProfessionalSkillData sd = cd.Professional.GetSkill(def.professionalSkillDefName);
                if (sd != null) level = sd.level;
            }
            long spanTicks = 0L;
            if (cd.Events != null && cd.Events.Count > 0)
            {
                long first = long.MaxValue, last = 0L;
                for (int i = 0; i < cd.Events.Count; i++)
                {
                    if (cd.Events[i] == null) continue;
                    if (cd.Events[i].Tick < first) first = cd.Events[i].Tick;
                    if (cd.Events[i].Tick > last) last = cd.Events[i].Tick;
                }
                if (first != long.MaxValue) spanTicks = last - first;
            }
            int hours = (int)(spanTicks / 2400L);

            bool practicalPassed = FlowPassed(cd, def.defName, "practical");
            bool theoryPassed = QualificationFlowService.TheoryPassedFor(po, def.defName);
            bool thesisDone = FlowPassed(cd, def.defName, "thesis");
            bool defenseDone = FlowPassed(cd, def.defName, "defense");
            QualificationProgress progress = cd.Qualification != null ? cd.Qualification.Get(def.defName) : null;
            bool reviewing = progress != null && progress.ReviewStartedTick > 0L
                && !QualificationReview.IsDue(progress.ReviewStartedTick, progress.ReviewDays, Find.TickManager.TicksGame);
            bool reviewDone = progress != null && progress.ReviewStartedTick > 0L
                && QualificationReview.IsDue(progress.ReviewStartedTick, progress.ReviewDays, Find.TickManager.TicksGame);

            float composite = 0.25f * (practicalPassed ? 90f : 0f)
                + 0.20f * (practicalPassed ? 85f : 0f)
                + 0.20f * (thesisDone && defenseDone ? 88f : 0f)
                + 0.15f * (thesisDone && defenseDone ? 90f : 0f)
                + 0.20f * (level / 50f * 100f);

            DrawConditionRow(view, ref y, contentW, "专业等级", "精密制造 ≥ " + def.requiredMinLevel, level >= def.requiredMinLevel);
            DrawConditionRow(view, ref y, contentW, "职业资历", "相关工作 ≥ " + def.requiredCareerTimeTicks + " tick", spanTicks >= def.requiredCareerTimeTicks);
            DrawConditionRow(view, ref y, contentW, "综合评分", "资格评定 ≥ " + def.minimumScore + "（当前 " + composite.ToString("0.0") + "）", composite >= def.minimumScore);
            DrawConditionRow(view, ref y, contentW, "实践考试", practicalPassed ? "通过（真实制造证据）" : (HasActiveExam(cd, def.defName) ? "进行中" : "未报名"), practicalPassed);
            DrawConditionRow(view, ref y, contentW, "理论考试", theoryPassed ? "通过（证据加权合成）" : "待提交", theoryPassed);
            DrawConditionRow(view, ref y, contentW, "论文 / 答辩", defenseDone ? "通过" : (thesisDone ? "答辩待进行" : "待进行"), thesisDone && defenseDone);
            DrawConditionRow(view, ref y, contentW, "评级评审", reviewing ? "评审中（N 个工作日后答复）" : (reviewDone ? "已答复" : "待结算"), reviewDone);

            y += 8f;

            // ── 流程操作（顺序门控） ──
            UIComponents.Label(new Rect(view.x, y, contentW, 20f),
                "PersonalChronicle.UI.Career.Qual.Actions".Translate(),
                UITheme.FontLabel, UITheme.Muted);
            y += 26f;

            float btnW = (contentW - 3f * 6f) / 4f;
            bool hasActiveExam = HasActiveExam(cd, def.defName);
            string gateExam = QualificationFlowService.CanApplyExam(def, level, spanTicks, hasActiveExam);
            bool canApplyExam = gateExam == QualificationFlowService.Ok;
            bool canSubmitTheory = practicalPassed && !theoryPassed;
            bool canStartThesis = theoryPassed && !thesisDone;
            bool canStartDefense = thesisDone && !defenseDone;

            if (DrawFlowButton(new Rect(view.x, y, btnW, 30f), "报名实践考试", canApplyExam,
                gateExam != QualificationFlowService.Ok ? "需等级/资历达标且无进行中考试" : ""))
            {
                string r = QualificationFlowService.ApplyForPracticalExam(po, def, Find.TickManager.TicksGame);
                if (r == QualificationFlowService.Ok)
                {
                    Find.WindowStack.Add(new Dialog_QualificationFlow(pawn, po, def, "exam"));
                }
                else
                {
                    Messages.Message("PersonalChronicle.UI.Career.Qual.Gate".Translate(r), pawn, MessageTypeDefOf.NeutralEvent);
                }
            }
            if (DrawFlowButton(new Rect(view.x + (btnW + 6f), y, btnW, 30f), "提交理论考试", canSubmitTheory,
                !practicalPassed ? "需先通过实践考试" : ""))
            {
                string r = QualificationFlowService.SubmitTheoryExam(po, def);
                Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(r), pawn,
                    r == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
            }
            if (DrawFlowButton(new Rect(view.x + 2f * (btnW + 6f), y, btnW, 30f), "论文 / 答辩", canStartThesis || canStartDefense,
                !theoryPassed ? "需先提交理论考试" : ""))
            {
                Find.WindowStack.Add(new Dialog_QualificationFlow(pawn, po, def, "thesis"));
            }
            if (DrawFlowButton(new Rect(view.x + 3f * (btnW + 6f), y, btnW, 30f), "召开答辩", canStartDefense,
                !thesisDone ? "需先完成论文" : ""))
            {
                Find.WindowStack.Add(new Dialog_QualificationFlow(pawn, po, def, "defense"));
            }
            y += 38f;

            // ── 考试任务卡（当前申报档） ──
            if (hasActiveExam || practicalPassed)
            {
                PracticalExamRecord exam = ActiveExam(cd, def.defName);
                if (exam != null || practicalPassed)
                {
                    if (exam != null)
                    {
                        string produced = exam.ProducedQualities != null ? string.Join("、", exam.ProducedQualities.ToArray()) : "";
                        int maxN = exam.MaxProduced > 0 ? exam.MaxProduced : exam.RequiredCount * 2;
                        UIComponents.Label(new Rect(view.x, y, contentW, 18f),
                            "任务：" + def.defName + " · 要求 " + exam.RequiredCount + " 件 ≥ " + exam.MinQuality
                            + " · 上限 " + maxN + " 件 · 已产出 " + exam.ProducedCount + " 件（" + produced + "）"
                            + (exam.Passed ? " · ✅ 已通过 " + exam.Score.ToString("0.0") + " 分"
                                : (exam.Finished ? " · ✗ 未通过（可重新报名）" : " · 进行中（制造对应配方采集证据）")),
                            UITheme.FontLabel, exam.Passed ? UITheme.PillGreen : UITheme.Muted);
                        y += 22f;
                    }
                    else
                    {
                        UIComponents.Label(new Rect(view.x, y, contentW, 18f),
                            "实践考试已通过（本档）", UITheme.FontLabel, UITheme.PillGreen);
                        y += 22f;
                    }
                }
            }

            // ── 评级评审卡 ──
            if (reviewing)
            {
                UIComponents.Label(new Rect(view.x, y, contentW, 18f),
                    "⏳ 评级评审中：N 个工作日后答复（答辩后结算，非即时授予）",
                    UITheme.FontLabel, UITheme.Warn);
            }
        }

        // ── 辅助 ──

        private static int HighestGrantedTier(CareerData cd)
        {
            int idx = -1;
            if (cd.GrantedTitles != null)
            {
                for (int i = 0; i < cd.GrantedTitles.Count; i++)
                {
                    if (cd.GrantedTitles[i] == null) continue;
                    for (int t = 0; t < QualTierKeys.Length; t++)
                    {
                        if (cd.GrantedTitles[i].TitleDefName != null
                            && cd.GrantedTitles[i].TitleDefName.EndsWith("_" + QualTierKeys[t], System.StringComparison.OrdinalIgnoreCase)
                            && t > idx)
                        {
                            idx = t;
                        }
                    }
                }
            }
            return idx;
        }

        private static bool FlowPassed(CareerData cd, string qualDefName, string kind)
        {
            if (cd == null) return false;
            if (kind == "practical")
            {
                if (cd.Exams == null || cd.Exams.Practical == null) return false;
                for (int i = 0; i < cd.Exams.Practical.Count; i++)
                {
                    if (cd.Exams.Practical[i] != null && cd.Exams.Practical[i].Passed
                        && string.Equals(cd.Exams.Practical[i].QualificationDefName, qualDefName, System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }
            if (kind == "thesis")
            {
                if (cd.Thesis == null || cd.Thesis.Theses == null) return false;
                for (int i = 0; i < cd.Thesis.Theses.Count; i++)
                {
                    if (cd.Thesis.Theses[i] != null && cd.Thesis.Theses[i].Completed
                        && string.Equals(cd.Thesis.Theses[i].QualificationDefName, qualDefName, System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }
            if (kind == "defense")
            {
                if (cd.Thesis == null || cd.Thesis.Defenses == null) return false;
                for (int i = 0; i < cd.Thesis.Defenses.Count; i++)
                {
                    if (cd.Thesis.Defenses[i] != null && cd.Thesis.Defenses[i].Passed
                        && string.Equals(cd.Thesis.Defenses[i].QualificationDefName, qualDefName, System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }
            return false;
        }

        private static bool HasActiveExam(CareerData cd, string qualDefName)
        {
            if (cd == null || cd.Exams == null || cd.Exams.Practical == null) return false;
            for (int i = 0; i < cd.Exams.Practical.Count; i++)
            {
                PracticalExamRecord r = cd.Exams.Practical[i];
                if (r != null && !r.Passed && !r.Finished && r.StartedTick > 0L
                    && string.Equals(r.QualificationDefName, qualDefName, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static PracticalExamRecord ActiveExam(CareerData cd, string qualDefName)
        {
            if (cd == null || cd.Exams == null || cd.Exams.Practical == null) return null;
            for (int i = 0; i < cd.Exams.Practical.Count; i++)
            {
                PracticalExamRecord r = cd.Exams.Practical[i];
                if (r != null && string.Equals(r.QualificationDefName, qualDefName, System.StringComparison.Ordinal)
                    && !r.Passed && !r.Finished && r.StartedTick > 0L)
                {
                    return r;
                }
            }
            return null;
        }

        private void DrawConditionRow(Rect view, ref float y, float contentW, string name, string note, bool ok)
        {
            Rect row = new Rect(view.x, y, contentW, 22f);
            UIComponents.Label(new Rect(row.x, row.y, 90f, 20f), name, UITheme.FontLabel, UITheme.Muted);
            UIComponents.Label(new Rect(row.x + 96f, row.y, row.width - 96f - 56f, 20f),
                UIComponents.TruncateToWidth(note, row.width - 96f - 56f, UITheme.FontLabel),
                UITheme.FontLabel, UITheme.Dim);
            UIComponents.Label(new Rect(row.x + row.width - 56f, row.y, 56f, 20f),
                ok ? "✓ 满足" : "○ 未满足", UITheme.FontLabel, ok ? UITheme.PillGreen : UITheme.Warn);
            y += 24f;
        }

        private bool DrawFlowButton(Rect rect, string label, bool enabled, string tip)
        {
            Color prev = GUI.color;
            GameFont prevFont = Verse.Text.Font;
            try
            {
                if (!enabled)
                {
                    UIComponents.TintedBox(rect, UITheme.Panel);
                    UIComponents.Border(rect, UITheme.BorderSoft);
                    UIComponents.Label(rect, label, UITheme.FontLabel, UITheme.Dim);
                    if (!string.IsNullOrEmpty(tip))
                    {
                        TooltipHandler.TipRegion(rect, tip);
                    }
                    return false;
                }
                bool click = Widgets.ButtonText(rect, label);
                return click;
            }
            finally
            {
                GUI.color = prev;
                Verse.Text.Font = prevFont;
            }
        }
    }
}

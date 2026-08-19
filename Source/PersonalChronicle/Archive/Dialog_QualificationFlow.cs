// Dialog_QualificationFlow — P9 资格流程对话框（报名实践考试 / 论文 / 答辩）。
// D-D2：ITab footer 按钮 + Dialog 触发，零新增 Harmony patch。
// 规则：顺序门控 + 按档隔离（QualificationDefName）+ 制造上限 + 评级评审期（RunQualification 门控）。
using System.Collections.Generic;
using PersonalChronicle.Application;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Qualification;
using PersonalChronicle.Archive.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    public class Dialog_QualificationFlow : Window
    {
        private readonly Pawn pawn;
        private readonly PawnObject pawnObject;
        private readonly QualificationDef def;
        private readonly string mode; // exam / thesis / defense

        private List<Pawn> committeeCache;

        public Dialog_QualificationFlow(Pawn pawn, PawnObject pawnObject, QualificationDef def, string mode)
        {
            this.pawn = pawn;
            this.pawnObject = pawnObject;
            this.def = def;
            this.mode = mode;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnCancel = true;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(480f, mode == "exam" ? 360f : 440f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            GUI.BeginGroup(inRect);
            try
            {
                if (mode == "exam") DrawExam(inRect);
                else if (mode == "thesis") DrawThesis(inRect);
                else DrawDefense(inRect);
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        // ── 实践考试：报名确认 + 任务说明 + 实时证据 ──
        private void DrawExam(Rect r)
        {
            UIComponents.Label(new Rect(0f, 6f, r.width, 24f),
                "实践考试 · " + def.defName, UITheme.FontValue, UITheme.Text);
            UIComponents.Rule(new Rect(0f, 34f, r.width, 1f), UITheme.BorderSoft);

            float y = 46f;
            UIComponents.Label(new Rect(0f, y, r.width, 20f),
                "要求：制造 3 件工业/太空组件，最低品质 Excellent（真实制造采集证据）",
                UITheme.FontLabel, UITheme.Muted);
            y += 24f;
            UIComponents.Label(new Rect(0f, y, r.width, 20f),
                "制造上限：6 件 · 时限：100000 tick（超限/超时未达标即失败，可重新报名）",
                UITheme.FontLabel, UITheme.Muted);
            y += 24f;

            CareerData cd = pawnObject != null ? pawnObject.CareerData : null;
            PracticalExamRecord active = null;
            if (cd != null && cd.Exams != null && cd.Exams.Practical != null)
            {
                for (int i = 0; i < cd.Exams.Practical.Count; i++)
                {
                    PracticalExamRecord rec = cd.Exams.Practical[i];
                    if (rec != null && string.Equals(rec.QualificationDefName, def.defName, System.StringComparison.Ordinal)
                        && !rec.Passed && !rec.Finished && rec.StartedTick > 0L)
                    {
                        active = rec;
                        break;
                    }
                }
            }
            if (active != null)
            {
                int maxN = active.MaxProduced > 0 ? active.MaxProduced : active.RequiredCount * 2;
                string produced = active.ProducedQualities != null && active.ProducedQualities.Count > 0
                    ? string.Join("、", active.ProducedQualities.ToArray()) : "—";
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "已产出：" + active.ProducedCount + "/" + maxN + " 件（" + produced + "）",
                    UITheme.FontLabel, active.Passed ? UITheme.PillGreen : UITheme.Muted);
                y += 24f;
                UIComponents.Label(new Rect(0f, y, r.width, 40f),
                    "证据由真实制造自动采集（Patch_GenRecipe 链路）。请在游戏中制造工业组件/太空组件完成考试。",
                    UITheme.FontLabel, UITheme.Dim);
                y += 44f;
            }
            else
            {
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "尚未报名。点击下方按钮报名本档实践考试。",
                    UITheme.FontLabel, UITheme.Dim);
                y += 26f;
            }

            y = r.height - 40f;
            if (active == null && Widgets.ButtonText(new Rect(0f, y, 150f, 30f), "📋 确认报名"))
            {
                string res = QualificationFlowService.ApplyForPracticalExam(pawnObject, def, Find.TickManager.TicksGame);
                Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(res), pawn,
                    res == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                Close();
            }
            if (Widgets.ButtonText(new Rect(160f, y, 120f, 30f), "关闭"))
            {
                Close();
            }
        }

        // ── 论文：选题（自动）→ 引用书籍×2/研究×2 → 完成（0.4 书 + 0.3 研 + 0.3 专业） ──
        private void DrawThesis(Rect r)
        {
            UIComponents.Label(new Rect(0f, 6f, r.width, 24f),
                "论文 · " + def.defName, UITheme.FontValue, UITheme.Text);
            UIComponents.Rule(new Rect(0f, 34f, r.width, 1f), UITheme.BorderSoft);

            CareerData cd = pawnObject != null ? pawnObject.CareerData : null;
            ThesisEvidence thesis = FindThesis(cd);

            float y = 46f;
            UIComponents.Label(new Rect(0f, y, r.width, 20f),
                "课题：《精密制造工艺研究》（引用书籍与研究完成论文）",
                UITheme.FontLabel, UITheme.Muted);
            y += 26f;

            if (thesis == null)
            {
                if (Widgets.ButtonText(new Rect(0f, y, 200f, 30f), "📝 论文选题"))
                {
                    string res = QualificationFlowService.StartThesis(pawnObject, def,
                        "thesis_" + def.defName + "_" + Find.TickManager.TicksGame);
                    Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(res), pawn,
                        res == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                }
                return;
            }

            int books = thesis.SourceBookIds != null ? thesis.SourceBookIds.Count : 0;
            int research = thesis.SourceResearchEventIds != null ? thesis.SourceResearchEventIds.Count : 0;
            UIComponents.Label(new Rect(0f, y, r.width, 20f),
                "已引用书籍 " + books + "/2 · 研究 " + research + "/2",
                UITheme.FontLabel, UITheme.Muted);
            y += 26f;

            if (!thesis.Completed)
            {
                if (books < 2 && Widgets.ButtonText(new Rect(0f, y, 150f, 30f), "📚 引用书籍"))
                {
                    QualificationFlowService.CiteThesisBook(pawnObject, def, "book_" + (books + 1));
                }
                if (research < 2 && Widgets.ButtonText(new Rect(160f, y, 150f, 30f), "🔬 引用研究"))
                {
                    string res = CiteResearch();
                    Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(res), pawn,
                        res == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                }
                y += 38f;
                bool canFinish = books >= 2 && research >= 2;
                if (Widgets.ButtonText(new Rect(0f, y, 150f, 30f), "✅ 完成论文"))
                {
                    string res = QualificationFlowService.CompleteThesis(pawnObject, def);
                    Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(res), pawn,
                        res == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                }
                if (!canFinish)
                {
                    UIComponents.Label(new Rect(160f, y + 6f, r.width - 160f, 20f),
                        "需引用 2 本书籍 + 2 项研究",
                        UITheme.FontLabel, UITheme.Dim);
                }
            }
            else
            {
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "✅ 论文完成：" + thesis.ComputedScore.ToString("0.0") + " 分",
                    UITheme.FontLabel, UITheme.PillGreen);
                y += 26f;
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "下一步：召开答辩（答辩子页按钮）。",
                    UITheme.FontLabel, UITheme.Dim);
            }
        }

        // ── 答辩：自动召集委员（同阵营高 Crafting）→ 委员会评分（D-D1） ──
        private void DrawDefense(Rect r)
        {
            UIComponents.Label(new Rect(0f, 6f, r.width, 24f),
                "答辩 · " + def.defName, UITheme.FontValue, UITheme.Text);
            UIComponents.Rule(new Rect(0f, 34f, r.width, 1f), UITheme.BorderSoft);

            CareerData cd = pawnObject != null ? pawnObject.CareerData : null;
            DefenseRecord defense = FindDefense(cd);

            float y = 46f;
            if (committeeCache == null)
            {
                committeeCache = GatherCommittee();
            }
            if (committeeCache.Count > 0)
            {
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "评审委员会（同阵营 · 按 Crafting 等级自动召集）：",
                    UITheme.FontLabel, UITheme.Muted);
                y += 24f;
                for (int i = 0; i < committeeCache.Count; i++)
                {
                    Pawn cm = committeeCache[i];
                    int lv = cm != null && cm.skills != null && cm.skills.GetSkill(SkillDefOf.Crafting) != null
                        ? cm.skills.GetSkill(SkillDefOf.Crafting).Level : 0;
                    UIComponents.Label(new Rect(0f, y, r.width, 20f),
                        "• " + (cm != null ? cm.LabelShort : "?") + " · 制造 Lv" + lv,
                        UITheme.FontLabel, UITheme.Dim);
                    y += 22f;
                }
            }
            else
            {
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "未找到可召集的委员（需同阵营殖民者）。",
                    UITheme.FontLabel, UITheme.Warn);
                y += 24f;
            }

            if (defense == null || !defense.Passed)
            {
                y = r.height - 40f;
                if (Widgets.ButtonText(new Rect(0f, y, 150f, 30f), "🎤 召开答辩"))
                {
                    string res = QualificationFlowService.StartDefense(pawnObject, def, committeeCache);
                    Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(res), pawn,
                        res == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                }
                if (defense != null && Widgets.ButtonText(new Rect(160f, y, 150f, 30f), "🗳 委员会评分"))
                {
                    string res = QualificationFlowService.GradeDefense(pawnObject, def, committeeCache);
                    Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(res), pawn,
                        res == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                    Close();
                }
            }
            else
            {
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "✅ 答辩通过：" + defense.FinalScore.ToString("0.0") + " 分（评级评审自动开始，N 个工作日后答复授予）",
                    UITheme.FontLabel, UITheme.PillGreen);
            }
        }

        // ── 辅助 ──

        private ThesisEvidence FindThesis(CareerData cd)
        {
            if (cd == null || cd.Thesis == null || cd.Thesis.Theses == null) return null;
            for (int i = 0; i < cd.Thesis.Theses.Count; i++)
            {
                if (cd.Thesis.Theses[i] != null
                    && string.Equals(cd.Thesis.Theses[i].QualificationDefName, def.defName, System.StringComparison.Ordinal))
                {
                    return cd.Thesis.Theses[i];
                }
            }
            return null;
        }

        private DefenseRecord FindDefense(CareerData cd)
        {
            if (cd == null || cd.Thesis == null || cd.Thesis.Defenses == null) return null;
            for (int i = 0; i < cd.Thesis.Defenses.Count; i++)
            {
                if (cd.Thesis.Defenses[i] != null
                    && string.Equals(cd.Thesis.Defenses[i].QualificationDefName, def.defName, System.StringComparison.Ordinal))
                {
                    return cd.Thesis.Defenses[i];
                }
            }
            return null;
        }

        private string CiteResearch()
        {
            ThesisEvidence t = FindThesis(pawnObject != null ? pawnObject.CareerData : null);
            if (t == null || t.Completed) return "noThesis";
            if (t.SourceResearchEventIds == null) t.SourceResearchEventIds = new List<string>();
            if (t.SourceResearchEventIds.Count < 2)
            {
                t.SourceResearchEventIds.Add("research_" + (t.SourceResearchEventIds.Count + 1));
                return QualificationFlowService.Ok;
            }
            return "already";
        }

        private List<Pawn> GatherCommittee()
        {
            List<Pawn> result = new List<Pawn>();
            if (pawnObject == null) return result;
            string selfId = pawn != null ? pawn.GetUniqueLoadID() : null;
            List<ColonyMember> live = ChronicleColonistScanner.EnumerateCurrentPeople();
            List<Pawn> sorted = new List<Pawn>();
            for (int i = 0; i < live.Count; i++)
            {
                ColonyMember m = live[i];
                if (m == null || m.Pawn == null) continue;
                string id = m.Pawn.GetUniqueLoadID();
                if (id == selfId) continue;
                if (m.Pawn.skills != null && m.Pawn.skills.GetSkill(SkillDefOf.Crafting) != null
                    && m.Pawn.skills.GetSkill(SkillDefOf.Crafting).Level >= 5)
                {
                    sorted.Add(m.Pawn);
                }
            }
            sorted.Sort((a, b) => b.skills.GetSkill(SkillDefOf.Crafting).Level.CompareTo(
                a.skills.GetSkill(SkillDefOf.Crafting).Level));
            int take = sorted.Count < 3 ? sorted.Count : 3;
            for (int i = 0; i < take; i++)
            {
                result.Add(sorted[i]);
            }
            return result;
        }
    }
}

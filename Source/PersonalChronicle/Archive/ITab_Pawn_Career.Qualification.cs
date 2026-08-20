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
using PersonalChronicle.Domain.Honor;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    public partial class ITab_Pawn_Career
    {
        // 职称 5 档元数据（对齐 Defs/QualificationDefs.xml 精密制造方向；显示门槛用）
        // 档名经 Professional.Title.Title_<key>.Label 翻译键动态解析，禁止硬编码中文。
        private static readonly string[] QualTierKeys = { "Junior", "Assistant", "Senior", "Specialist", "Master" };
        private static readonly int[] QualTierLevels = { 5, 15, 25, 38, 45 };
        private static readonly int[] QualTierHours = { 25, 80, 240, 480, 720 };

        private static string QualTierLabel(int i)
        {
            if (i < 0 || i >= QualTierKeys.Length) return "--";
            string key = "Professional.Title.Title_Precision_" + QualTierKeys[i] + ".Label";
            string v = key.Translate().ToString();
            return string.IsNullOrEmpty(v) || v == key ? QualTierKeys[i] : v;
        }

        /// <summary>资格子页（P9，方案 A 单页 6 卡片）：①资格进度 ②实践考试 ③理论考试 ④论文 ⑤答辩 ⑥职称记录。
        /// 纯前端重组——按钮拆回各面板，新增只读职称记录块；后端/服务/数据零改动。</summary>
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
            bool capped = applyIdx >= QualTierKeys.Length;

            // 申报档 Def（封顶/无 Def 时可为 null）
            QualificationDef def = capped ? null
                : DefDatabase<QualificationDef>.GetNamedSilentFail("Q_Precision_" + QualTierKeys[applyIdx]);

            // 滚动视图容器
            Rect view = new Rect(rect.x, rect.y, rect.width, rect.height);
            float contentW = view.width - 18f;

            // 计算全部 6 区块高度（封顶时省略 ②③④⑤ 操作区，仅留 ①⑥）
            float totalH = CalcQualHeight(cd, def, capped, contentW);
            // v4.17 体检：viewRect 起点须与内容坐标一致（x=view.x、y=0）——
            // 旧 (0,0) 起点 + 内容绝对坐标混合导致整页右移 12px、右缘压滚动条。
            Widgets.BeginScrollView(rect, ref scroll,
                new Rect(view.x, 0f, view.width, Mathf.Max(totalH, 1f)));
            float y = 0f;

            // 预计算流程态（各区块共享；v4.17 体检：全部子块复用，不再各自扫描事件流）。
            // 封顶/无 Def 时 def==null，① 只画阶梯（条件行省略），这些值不参与绘制。
            int level = 0;
            long spanTicks = 0L;
            bool practicalPassed = false;
            bool theoryPassed = false;
            bool thesisDone = false;
            bool defenseDone = false;
            bool hasActiveExam = false;
            string gateExam = string.Empty;
            if (def != null)
            {
                if (cd.Professional != null && !string.IsNullOrEmpty(def.professionalSkillDefName))
                {
                    ProfessionalSkillData sd = cd.Professional.GetSkill(def.professionalSkillDefName);
                    if (sd != null) level = sd.level;
                }
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
                practicalPassed = FlowPassed(cd, def.defName, "practical");
                theoryPassed = QualificationFlowService.TheoryPassedFor(po, def.defName);
                thesisDone = FlowPassed(cd, def.defName, "thesis");
                defenseDone = FlowPassed(cd, def.defName, "defense");
                hasActiveExam = HasActiveExam(cd, def.defName);
                gateExam = QualificationFlowService.CanApplyExam(def, level, spanTicks, hasActiveExam);
            }
            bool canApplyExam = gateExam == QualificationFlowService.Ok;
            bool canSubmitTheory = practicalPassed && !theoryPassed;
            bool canStartThesis = theoryPassed && !thesisDone;
            bool canStartDefense = thesisDone && !defenseDone;

            // ① 资格进度（阶梯 + 7 行条件，纯只读）
            y = DrawQualProgressBlock(view, y, contentW, po, cd, def, grantedIdx, applyIdx, capped,
                level, spanTicks, practicalPassed, theoryPassed, thesisDone, defenseDone);
            if (capped)
            {
                // 封顶：仅展示 ① + ⑥
                y = DrawGrantedTitlesBlock(view, y, contentW, cd);
                Widgets.EndScrollView();
                return;
            }
            if (def == null)
            {
                DrawEmptyDefNote(view, y, contentW);
                y += 24f;
                y = DrawGrantedTitlesBlock(view, y, contentW, cd);
                Widgets.EndScrollView();
                return;
            }

            // ② 实践考试（按钮 + 任务卡）
            y = DrawPracticalExamBlock(view, y, contentW, pawn, po, cd, def, canApplyExam, gateExam, hasActiveExam, practicalPassed);
            // ③ 理论考试（按钮 + 状态）
            y = DrawTheoryBlock(view, y, contentW, pawn, po, def, practicalPassed, theoryPassed, canSubmitTheory);
            // ④ 论文（按钮 + 状态）
            y = DrawThesisBlock(view, y, contentW, pawn, po, def, theoryPassed, thesisDone, canStartThesis, canStartDefense);
            // ⑤ 答辩（按钮 + 状态 + 评审结算）
            y = DrawDefenseBlock(view, y, contentW, pawn, po, def, thesisDone, defenseDone, canStartDefense, cd);
            // ⑥ 职称记录（只读，新增）
            y = DrawGrantedTitlesBlock(view, y, contentW, cd);

            Widgets.EndScrollView();
        }

        // ============ ① 资格进度 ============
        // v4.17 体检：level/span/flow 由 DrawQualificationTab 计算一次传入
        // （旧实现在块内重复扫描全事件流，千级事件每帧 2 次 O(n)，PERF-001）。
        private float DrawQualProgressBlock(Rect view, float y, float contentW, PawnObject po, CareerData cd,
            QualificationDef def, int grantedIdx, int applyIdx, bool capped,
            int level, long spanTicks, bool practicalPassed, bool theoryPassed,
            bool thesisDone, bool defenseDone)
        {
            float pad = UITheme.PanelPadding;
            // 阶梯 5 档
            float blockTop = y;
            UIComponents.SectionTitle(new Rect(view.x, y, contentW, 24f), y,
                "PersonalChronicle.UI.Career.Qual.Ladder".Translate());
            y += 28f;
            float stepW = (contentW - pad * 2f - 4f * 6f) / 5f;
            float innerX = view.x + pad;
            for (int i = 0; i < QualTierKeys.Length; i++)
            {
                Rect step = new Rect(innerX + i * (stepW + 6f), y, stepW, 52f);
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
                // v4.17 体检：状态行 12f→18f（CJK「已授予/申请中/未解锁」Tiny 需 ≥18f）；
                // 门槛行与职称名行相应压缩，阶梯 52f 内恰好容纳。
                UIComponents.Label(new Rect(step.x + 4f, step.y + 3f, step.width - 8f, 12f),
                    "Lv" + (i + 1) + " · ≥" + QualTierLevels[i] + " · ≥" + QualTierHours[i] + "h",
                    UITheme.FontLabel, UITheme.Muted);
                UIComponents.Label(new Rect(step.x + 4f, step.y + 17f, step.width - 8f, 16f),
                    UIComponents.TruncateToWidth(QualTierLabel(i), step.width - 8f, UITheme.FontLabel),
                    UITheme.FontLabel, state == "granted" ? UITheme.PillGreen : (state == "applying" ? UITheme.Accent : UITheme.Dim));
                string stLabel = state == "granted"
                    ? "PersonalChronicle.UI.Career.Qual.Step.Granted".Translate()
                    : (state == "applying" ? "PersonalChronicle.UI.Career.Qual.Step.Applying".Translate()
                        : "PersonalChronicle.UI.Career.Qual.Step.Locked".Translate());
                UIComponents.Label(new Rect(step.x + 4f, step.y + 33f, step.width - 8f, 18f),
                    stLabel, UITheme.FontLabel, state == "granted" ? UITheme.PillGreen : (state == "applying" ? UITheme.Accent : UITheme.Dim));
            }
            y += 58f;
            y += UITheme.SpaceMd;

            if (def != null)
            {
                // 7 行申报条件
                UIComponents.SectionTitle(new Rect(view.x, y, contentW, 24f), y,
                    "PersonalChronicle.UI.Career.Qual.Conditions".Translate(QualTierLabel(applyIdx)));
                y += 28f;

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

                string dirName = !string.IsNullOrEmpty(def.LabelCap) ? def.LabelCap : def.defName;
                DrawConditionRow(view, ref y, contentW, "PersonalChronicle.UI.Career.Qual.Cond.Level".Translate(),
                    "PersonalChronicle.UI.Career.Qual.Cond.Level.Note".Translate(dirName, def.requiredMinLevel), level >= def.requiredMinLevel);
                DrawConditionRow(view, ref y, contentW, "PersonalChronicle.UI.Career.Qual.Cond.Time".Translate(),
                    "PersonalChronicle.UI.Career.Qual.Cond.Time.Note".Translate(def.requiredCareerTimeTicks), spanTicks >= def.requiredCareerTimeTicks);
                DrawConditionRow(view, ref y, contentW, "PersonalChronicle.UI.Career.Qual.Cond.Score".Translate(),
                    "PersonalChronicle.UI.Career.Qual.Cond.Score.Note".Translate(def.minimumScore, composite.ToString("0.0")), composite >= def.minimumScore);
                DrawConditionRow(view, ref y, contentW, "PersonalChronicle.UI.Career.Qual.Cond.Practical".Translate(),
                    practicalPassed
                        ? "PersonalChronicle.UI.Career.Qual.Cond.Practical.Passed".Translate()
                        : (HasActiveExam(cd, def.defName) ? "PersonalChronicle.UI.Career.Qual.Cond.Practical.Active".Translate()
                            : "PersonalChronicle.UI.Career.Qual.Cond.Practical.None".Translate()),
                    practicalPassed);
                DrawConditionRow(view, ref y, contentW, "PersonalChronicle.UI.Career.Qual.Cond.Theory".Translate(),
                    theoryPassed ? "PersonalChronicle.UI.Career.Qual.Cond.Theory.Passed".Translate()
                        : "PersonalChronicle.UI.Career.Qual.Cond.Theory.Todo".Translate(),
                    theoryPassed);
                DrawConditionRow(view, ref y, contentW, "PersonalChronicle.UI.Career.Qual.Cond.Paper".Translate(),
                    defenseDone
                        ? "PersonalChronicle.UI.Career.Qual.Cond.Paper.Passed".Translate()
                        : (thesisDone ? "PersonalChronicle.UI.Career.Qual.Cond.Paper.Pending".Translate()
                            : "PersonalChronicle.UI.Career.Qual.Cond.Paper.Todo".Translate()),
                    thesisDone && defenseDone);
                DrawConditionRow(view, ref y, contentW, "PersonalChronicle.UI.Career.Qual.Cond.Review".Translate(),
                    reviewing
                        ? "PersonalChronicle.UI.Career.Qual.Cond.Review.Running".Translate()
                        : (reviewDone ? "PersonalChronicle.UI.Career.Qual.Cond.Review.Done".Translate()
                            : "PersonalChronicle.UI.Career.Qual.Cond.Review.Todo".Translate()),
                    reviewDone);
                y += UITheme.SpaceMd;
            }
            return y;
        }

        // ============ ② 实践考试 ============
        private float DrawPracticalExamBlock(Rect view, float y, float contentW, Pawn pawn, PawnObject po,
            CareerData cd, QualificationDef def, bool canApplyExam, string gateExam, bool hasActiveExam, bool practicalPassed)
        {
            float pad = UITheme.PanelPadding;
            float blockH = 28f + 30f + 8f + 22f + UITheme.SpaceMd;
            Rect block = new Rect(view.x, y, contentW, blockH);
            UIComponents.Panel(block, UITheme.Panel);
            UIComponents.Border(block, UITheme.BorderSoft);

            UIComponents.SectionTitle(new Rect(block.x, block.y, contentW, 24f), block.y,
                "PersonalChronicle.UI.Career.Qual.Block.Practical".Translate());

            float btnY = block.y + 28f;
            if (DrawFlowButton(new Rect(view.x + pad, btnY, contentW - pad * 2f, 30f),
                "PersonalChronicle.UI.Career.Qual.Btn.ApplyPractical".Translate(),
                canApplyExam, gateExam != QualificationFlowService.Ok ? gateExam : ""))
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

            // 任务卡
            float taskY = btnY + 36f;
            if (hasActiveExam || practicalPassed)
            {
                PracticalExamRecord exam = ActiveExam(cd, def.defName);
                if (exam != null)
                {
                    string produced = exam.ProducedQualities != null ? string.Join("、", exam.ProducedQualities.ToArray()) : "";
                    int maxN = exam.MaxProduced > 0 ? exam.MaxProduced : exam.RequiredCount * 2;
                    string suffix = exam.Passed
                        ? "PersonalChronicle.UI.Career.Qual.Task.Passed".Translate(exam.Score.ToString("0.0"))
                        : (exam.Finished ? "PersonalChronicle.UI.Career.Qual.Task.Failed".Translate()
                            : "PersonalChronicle.UI.Career.Qual.Task.Active".Translate());
                    UIComponents.Label(new Rect(view.x + pad, taskY, contentW - pad * 2f, 20f),
                        "PersonalChronicle.UI.Career.Qual.Task.Label".Translate(
                            def.defName, exam.RequiredCount, exam.MinQuality, maxN, exam.ProducedCount, produced, suffix),
                        UITheme.FontLabel, exam.Passed ? UITheme.PillGreen : UITheme.Muted);
                }
                else
                {
                    UIComponents.Label(new Rect(view.x + pad, taskY, contentW - pad * 2f, 20f),
                        "PersonalChronicle.UI.Career.Qual.Practical.Passed".Translate(),
                        UITheme.FontLabel, UITheme.PillGreen);
                }
            }
            return y + blockH;
        }

        // ============ ③ 理论考试 ============
        private float DrawTheoryBlock(Rect view, float y, float contentW, Pawn pawn, PawnObject po,
            QualificationDef def, bool practicalPassed, bool theoryPassed, bool canSubmitTheory)
        {
            float pad = UITheme.PanelPadding;
            float blockH = 28f + 30f + 8f + 20f + UITheme.SpaceMd;
            Rect block = new Rect(view.x, y, contentW, blockH);
            UIComponents.Panel(block, UITheme.Panel);
            UIComponents.Border(block, UITheme.BorderSoft);

            UIComponents.SectionTitle(new Rect(block.x, block.y, contentW, 24f), block.y,
                "PersonalChronicle.UI.Career.Qual.Block.Theory".Translate());

            float btnY = block.y + 28f;
            if (DrawFlowButton(new Rect(view.x + pad, btnY, contentW - pad * 2f, 30f),
                "PersonalChronicle.UI.Career.Qual.Btn.SubmitTheory".Translate(),
                canSubmitTheory,
                !practicalPassed ? "PersonalChronicle.UI.Career.Qual.Gate.PracticalFirst".Translate() : ""))
            {
                string r = QualificationFlowService.SubmitTheoryExam(po, def);
                Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(r), pawn,
                    r == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
            }
            UIComponents.Label(new Rect(view.x + pad, btnY + 36f, contentW - pad * 2f, 20f),
                theoryPassed ? "PersonalChronicle.UI.Career.Qual.Theory.Passed".Translate()
                    : "PersonalChronicle.UI.Career.Qual.Theory.Todo".Translate(),
                UITheme.FontLabel, theoryPassed ? UITheme.PillGreen : UITheme.Dim);
            return y + blockH;
        }

        // ============ ④ 论文 ============
        private float DrawThesisBlock(Rect view, float y, float contentW, Pawn pawn, PawnObject po,
            QualificationDef def, bool theoryPassed, bool thesisDone, bool canStartThesis, bool canStartDefense)
        {
            float pad = UITheme.PanelPadding;
            float blockH = 28f + 30f + 8f + 20f + UITheme.SpaceMd;
            Rect block = new Rect(view.x, y, contentW, blockH);
            UIComponents.Panel(block, UITheme.Panel);
            UIComponents.Border(block, UITheme.BorderSoft);

            UIComponents.SectionTitle(new Rect(block.x, block.y, contentW, 24f), block.y,
                "PersonalChronicle.UI.Career.Qual.Block.Thesis".Translate());

            float btnY = block.y + 28f;
            if (DrawFlowButton(new Rect(view.x + pad, btnY, contentW - pad * 2f, 30f),
                "PersonalChronicle.UI.Career.Qual.Btn.Thesis".Translate(),
                canStartThesis || canStartDefense,
                !theoryPassed ? "PersonalChronicle.UI.Career.Qual.Gate.TheoryFirst".Translate() : ""))
            {
                Find.WindowStack.Add(new Dialog_QualificationFlow(pawn, po, def, "thesis"));
            }
            UIComponents.Label(new Rect(view.x + pad, btnY + 36f, contentW - pad * 2f, 20f),
                thesisDone ? "PersonalChronicle.UI.Career.Qual.Thesis.Done".Translate()
                    : "PersonalChronicle.UI.Career.Qual.Thesis.Todo".Translate(),
                UITheme.FontLabel, thesisDone ? UITheme.PillGreen : UITheme.Dim);
            return y + blockH;
        }

        // ============ ⑤ 答辩 ============
        private float DrawDefenseBlock(Rect view, float y, float contentW, Pawn pawn, PawnObject po,
            QualificationDef def, bool thesisDone, bool defenseDone, bool canStartDefense, CareerData cd)
        {
            float pad = UITheme.PanelPadding;
            // 与 CalcQualHeight ⑤ 对齐：标题 28 + 按钮 30 + 间隔 8 + 状态 20 + 评审提示 20 + SpaceMd。
            float blockH = 28f + 30f + 8f + 20f + 20f + UITheme.SpaceMd;
            Rect block = new Rect(view.x, y, contentW, blockH);
            UIComponents.Panel(block, UITheme.Panel);
            UIComponents.Border(block, UITheme.BorderSoft);

            UIComponents.SectionTitle(new Rect(block.x, block.y, contentW, 24f), block.y,
                "PersonalChronicle.UI.Career.Qual.Block.Defense".Translate());

            float btnY = block.y + 28f;
            if (DrawFlowButton(new Rect(view.x + pad, btnY, contentW - pad * 2f, 30f),
                "PersonalChronicle.UI.Career.Qual.Btn.Defense".Translate(),
                canStartDefense,
                !thesisDone ? "PersonalChronicle.UI.Career.Qual.Gate.ThesisFirst".Translate() : ""))
            {
                Find.WindowStack.Add(new Dialog_QualificationFlow(pawn, po, def, "defense"));
            }
            float noteY = btnY + 36f;
            if (defenseDone)
            {
                UIComponents.Label(new Rect(view.x + pad, noteY, contentW - pad * 2f, 20f),
                    "PersonalChronicle.UI.Career.Qual.Defense.Passed".Translate(),
                    UITheme.FontLabel, UITheme.PillGreen);
            }
            else
            {
                UIComponents.Label(new Rect(view.x + pad, noteY, contentW - pad * 2f, 20f),
                    "PersonalChronicle.UI.Career.Qual.Defense.Todo".Translate(),
                    UITheme.FontLabel, UITheme.Dim);
            }
            // 评级评审结算提示
            QualificationProgress progress = cd.Qualification != null ? cd.Qualification.Get(def.defName) : null;
            if (progress != null && progress.ReviewStartedTick > 0L)
            {
                bool due = QualificationReview.IsDue(progress.ReviewStartedTick, progress.ReviewDays, Find.TickManager.TicksGame);
                UIComponents.Label(new Rect(view.x + pad, noteY + 20f, contentW - pad * 2f, 20f),
                    due ? "PersonalChronicle.UI.Career.Qual.Review.Due".Translate()
                        : "PersonalChronicle.UI.Career.Qual.Review.Running".Translate(),
                    UITheme.FontLabel, UITheme.Warn);
            }
            return y + blockH;
        }

        // ============ ⑥ 职称记录（只读，新增） ============
        private float DrawGrantedTitlesBlock(Rect view, float y, float contentW, CareerData cd)
        {
            float pad = UITheme.PanelPadding;
            UIComponents.SectionTitle(new Rect(view.x, y, contentW, 24f), y,
                "PersonalChronicle.UI.Career.Qual.Block.Titles".Translate());
            y += 28f;

            if (cd.GrantedTitles == null || cd.GrantedTitles.Count == 0)
            {
                UIComponents.Label(new Rect(view.x + pad, y, contentW - pad * 2f, 20f),
                    "PersonalChronicle.UI.Career.Qual.NoTitle".Translate().ToString(),
                    UITheme.FontLabel, UITheme.Dim);
                return y + 26f + UITheme.SpaceMd;
            }

            float rowH = 26f;
            float listH = cd.GrantedTitles.Count * (rowH + 4f) + UITheme.SpaceMd;
            Rect block = new Rect(view.x, y, contentW, listH);
            UIComponents.Panel(block, UITheme.Panel);
            UIComponents.Border(block, UITheme.BorderSoft);
            float ry = block.y + 6f;
            for (int i = 0; i < cd.GrantedTitles.Count; i++)
            {
                GrantedTitle g = cd.GrantedTitles[i];
                if (g == null) continue;
                ProfessionalTitleDef tdef = DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(g.TitleDefName);
                string name = tdef != null
                    ? ("Professional.Title." + tdef.defName + ".Label").Translate().ToString()
                    : g.TitleDefName;
                string when = g.GrantedTick > 0L ? GenDate.DateReadoutStringAt(g.GrantedTick, Vector2.zero) : "—";
                Rect rr = new Rect(block.x + pad, ry, block.width - pad * 2f, rowH);
                UIComponents.TintedBox(rr, UITheme.PanelRaised);
                // v4.17 体检：长职称名/英文长日期截断（否则换行被 rowH 裁剪）。
                UIComponents.Label(new Rect(rr.x + 6f, rr.y, rr.width - 96f, rowH),
                    UIComponents.TruncateToWidth(name, rr.width - 96f, UITheme.FontLabel),
                    UITheme.FontLabel, UITheme.Text);
                UIComponents.Label(new Rect(rr.x + rr.width - 84f, rr.y, 78f, rowH),
                    UIComponents.TruncateToWidth(when, 78f, UITheme.FontLabel),
                    UITheme.FontLabel, UITheme.Dim, TextAnchor.MiddleRight);
                ry += rowH + 4f;
            }
            return y + listH + UITheme.SpaceMd;
        }

        private void DrawEmptyDefNote(Rect view, float y, float contentW)
        {
            UIComponents.Label(new Rect(view.x, y, contentW, 20f),
                "PersonalChronicle.UI.Career.Qual.NoDef".Translate().ToString(),
                UITheme.FontBody, UITheme.Muted);
        }

        // 计算 6 区块总高度（封顶时仅 ① + ⑥）
        private float CalcQualHeight(CareerData cd, QualificationDef def, bool capped, float contentW)
        {
            float h = 0f;
            h += 28f + 58f + UITheme.SpaceMd;            // ① 阶梯
            if (def != null) h += 28f + 7f * 24f + UITheme.SpaceMd; // ① 条件 7 行
            else if (!capped) h += 24f;
            if (capped || def == null)
            {
                // ⑥（无记录态或封顶态）——v4.17 体检：按实际职称记录数计高
                // （旧固定 28+26 在 5 档全授时低估约 124px，底部职称行被裁不可达）。
                h += 28f + Mathf.Max(1, cd != null && cd.GrantedTitles != null ? cd.GrantedTitles.Count : 0)
                    * 30f + UITheme.SpaceMd;
                return h + UITheme.SpaceMd;
            }
            h += 28f + 30f + 8f + 22f + UITheme.SpaceMd; // ②
            h += 28f + 30f + 8f + 20f + UITheme.SpaceMd; // ③
            h += 28f + 30f + 8f + 20f + UITheme.SpaceMd; // ④
            h += 28f + 30f + 8f + 20f + 20f + UITheme.SpaceMd; // ⑤（含评审提示 2 行）
            h += 28f + Mathf.Max(1, cd != null && cd.GrantedTitles != null ? cd.GrantedTitles.Count : 0)
                * 30f + UITheme.SpaceMd;                 // ⑥（按记录数）
            return h + UITheme.SpaceMd;
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
            // v4.17 体检：条件名固定 90f 不截断 → 英文长标签换行被裁。
            UIComponents.Label(new Rect(row.x, row.y, 90f, 20f),
                UIComponents.TruncateToWidth(name, 90f, UITheme.FontLabel), UITheme.FontLabel, UITheme.Muted);
            UIComponents.Label(new Rect(row.x + 96f, row.y, row.width - 96f - 56f, 20f),
                UIComponents.TruncateToWidth(note, row.width - 96f - 56f, UITheme.FontLabel),
                UITheme.FontLabel, UITheme.Dim);
            UIComponents.Label(new Rect(row.x + row.width - 56f, row.y, 56f, 20f),
                ok ? "PersonalChronicle.UI.Career.Qual.Cond.Satisfied".Translate()
                    : "PersonalChronicle.UI.Career.Qual.Cond.Unsatisfied".Translate(),
                UITheme.FontLabel, ok ? UITheme.PillGreen : UITheme.Warn);
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

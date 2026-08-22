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
            int grantedIdx = QualificationFlowService.GrantedTierIndex(cd, QualTierKeys);
            int applyIdx = grantedIdx + 1;
            bool capped = applyIdx >= QualTierKeys.Length;

            // 申报档 Def（封顶/无 Def 时可为 null）
            QualificationDef def = capped ? null
                : DefDatabase<QualificationDef>.GetNamedSilentFail("Q_Precision_" + QualTierKeys[applyIdx]);

            // 滚动视图容器（原生 BeginScrollView 用法，对齐 ArchiveMainTabWindow：
            // 不自定义背景，让 rimworld 窗口默认背景生效，避免 TintedBox 干扰造成的白屏）。
            Rect view = new Rect(rect.x, rect.y, rect.width, rect.height);
            float contentW = view.width - 16f;

            // 计算全部 6 区块高度（封顶时省略 ②③④⑤ 操作区，仅留 ①⑥）
            float totalH = CalcQualHeight(cd, def, capped, contentW);
            Rect qualViewRect = new Rect(view.x, view.y, view.width - 16f, Mathf.Max(totalH, view.height, 1f));
            Widgets.BeginScrollView(rect, ref scroll, qualViewRect);
            float y = view.y + 4f;

            // 身份头（参考 P9 预览 personbar）：方向 + 当前档 + 已获状态——让玩家初始视图就能直观看到 P9 流程上下文
            y = DrawQualHeaderBlock(view, contentW, cd, def, grantedIdx, applyIdx, capped, pawn, y);

            // 预计算流程态（各区块共享；全部经 QualificationFlowService 判定，UI 不重复实现业务规则，UI-001）。
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
                level = QualificationFlowService.CalcSkillLevel(cd, def.professionalSkillDefName);
                spanTicks = QualificationFlowService.CalcCareerSpanTicks(cd);
                practicalPassed = QualificationFlowService.FlowPassed(cd, def.defName, "practical");
                theoryPassed = QualificationFlowService.TheoryPassedFor(po, def.defName);
                thesisDone = QualificationFlowService.FlowPassed(cd, def.defName, "thesis");
                defenseDone = QualificationFlowService.FlowPassed(cd, def.defName, "defense");
                hasActiveExam = QualificationFlowService.HasActiveExam(cd, def.defName);
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
            // 阶梯 5 档：纵向列表（每档一行），避免窄窗下 5 横卡挤压导致职称名截断（P0）。
            UIComponents.SectionTitle(new Rect(view.x, y, contentW, 24f), y,
                "PersonalChronicle.UI.Career.Qual.Ladder".Translate());
            y += 28f;
            float stepH = 24f;
            float stepGap = 4f;
            for (int i = 0; i < QualTierKeys.Length; i++)
            {
                Rect step = new Rect(view.x, y + i * (stepH + stepGap), contentW, stepH);
                string state = "locked";
                if (i <= grantedIdx) state = "granted";
                else if (i == applyIdx && !capped) state = "applying";
                // 状态填充色区分：granted/applying 抬升底，locked 平底（边框+文字色三态区分）
                Color fill = state == "locked" ? UITheme.Panel : UITheme.PanelRaised;
                Color borderCol = state == "granted" ? UITheme.PillGreen
                    : (state == "applying" ? UITheme.Accent : UITheme.BorderSoft);
                Color textCol = state == "granted" ? UITheme.Alive
                    : (state == "applying" ? UITheme.Accent : UITheme.Dim);
                UIComponents.TintedBox(step, fill);
                UIComponents.Border(step, borderCol);
                // Lv + 职称名（左）
                UIComponents.Label(new Rect(step.x + 8f, step.y, step.width * 0.62f, stepH),
                    "Lv" + (i + 1) + " · " + QualTierLabel(i),
                    UITheme.FontBody, textCol, TextAnchor.MiddleLeft);
                // 门槛（中）
                UIComponents.Label(new Rect(step.x + step.width * 0.62f, step.y, step.width * 0.22f, stepH),
                    "≥" + QualTierLevels[i] + " · ≥" + QualTierHours[i] + "h",
                    UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
                // 状态（右）
                string stLabel = state == "granted"
                    ? "PersonalChronicle.UI.Career.Qual.Step.Granted".Translate()
                    : (state == "applying" ? "PersonalChronicle.UI.Career.Qual.Step.Applying".Translate()
                        : "PersonalChronicle.UI.Career.Qual.Step.Locked".Translate());
                UIComponents.Label(new Rect(step.x + step.width * 0.84f, step.y, step.width * 0.16f - 6f, stepH),
                    stLabel, UITheme.FontLabel, textCol, TextAnchor.MiddleRight);
            }
            y += QualTierKeys.Length * (stepH + stepGap) + UITheme.SpaceMd;

            if (def != null)
            {
                // 7 行申报条件
                UIComponents.SectionTitle(new Rect(view.x, y, contentW, 24f), y,
                    "PersonalChronicle.UI.Career.Qual.Conditions".Translate(QualTierLabel(applyIdx)));
                y += 28f;

                QualificationProgress progress = cd.Qualification != null ? cd.Qualification.Get(def.defName) : null;
                // 注：progress/Review 状态读取为展示所需实时数据，业务判定（IsDue）为 Domain 纯函数，与 Overview 页一致。
                bool reviewing = progress != null && progress.ReviewStartedTick > 0L
                    && !QualificationReview.IsDue(progress.ReviewStartedTick, progress.ReviewDays, Find.TickManager.TicksGame);
                bool reviewDone = progress != null && progress.ReviewStartedTick > 0L
                    && QualificationReview.IsDue(progress.ReviewStartedTick, progress.ReviewDays, Find.TickManager.TicksGame);
                // 7 行申报条件：复用总览的 BuildQualRows 快照 + DrawQualCell 渲染（v2.0 §14 一致性）
                // composite 取 QualificationEvaluator.Evaluate 精确值（与 Overview 同口径，不再用粗略公式）
                float composite = 0f;
                List<QualificationEvaluator.Eligibility> eligList = QualificationEvaluator.Evaluate(po);
                for (int ei = 0; ei < eligList.Count; ei++)
                {
                    if (eligList[ei] != null && eligList[ei].Def != null
                        && eligList[ei].Def.defName == def.defName)
                    {
                        composite = eligList[ei].CompositeScore;
                        break;
                    }
                }
                var qualRows = ArchiveUiDataProvider.BuildQualRows(cd, def, composite);
                if (qualRows != null && qualRows.Count > 0)
                {
                    float qRowH = 28f;
                    float qGap = 6f;
                    for (int qi = 0; qi < qualRows.Count; qi++)
                    {
                        Rect cell = new Rect(view.x, y, contentW, qRowH);
                        DrawQualCell(cell, qualRows[qi]);
                        y += qRowH + qGap;
                    }
                    y += UITheme.SpaceMd;
                }
            }
            return y;
        }

        // ============ 身份头（personbar）：方向 + 当前申报档 + 已获最高职称 ============
        // 参考 P9 资格考试UI预览.html 的 personbar：让玩家初始视图就直观看到 P9 流程上下文。
        // 当前档名+已获档名都走 UITheme/翻译键，禁止硬编码中文。
        private float DrawQualHeaderBlock(Rect view, float contentW, CareerData cd, QualificationDef def,
            int grantedIdx, int applyIdx, bool capped, Pawn pawn, float y)
        {
            string pawnName = pawn != null && pawn.LabelCap != null ? pawn.LabelCap : "PersonalChronicle.UI.Career.Qual.Header.Pawn".Translate().ToString();
            // 方向名：取 def.professionalSkillDefName → ProfessionalSkillDef.LabelCap（走 Def 标准翻译键）
            string dirName = "PersonalChronicle.UI.Career.Qual.Header.Undefined".Translate().ToString();
            if (def != null && !string.IsNullOrEmpty(def.professionalSkillDefName))
            {
                ProfessionalSkillDef skillDef = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(def.professionalSkillDefName);
                if (skillDef != null) dirName = skillDef.LabelCap.ToString();
            }
            string currentTier = capped
                ? "PersonalChronicle.UI.Career.Qual.Header.Maxed".Translate().ToString()
                : (applyIdx < QualTierKeys.Length ? QualTierLabel(applyIdx)
                    : "PersonalChronicle.UI.Career.Qual.Header.Maxed".Translate().ToString());
            string highestGranted = grantedIdx >= 0
                ? QualTierLabel(grantedIdx)
                : "PersonalChronicle.UI.Career.Qual.Header.None".Translate().ToString();

            // 头部面板（36f 高，单行布局：左 = 姓名（小字）+ 方向（强调色）；右 = 已获 + 申报，避免文本被裁切）
            float padX = UITheme.PanelPadding;
            float blockH = 36f;
            Rect block = new Rect(view.x, y, contentW, blockH);
            UIComponents.Panel(block, UITheme.Panel);
            UIComponents.Border(block, UITheme.Accent);

            // 左侧：姓名（窄 25% + 方向 25%），固定宽度避免 CalcSize 字体依赖
            float leftW = contentW * 0.5f;
            float nameW = leftW * 0.5f;
            float dirW = leftW * 0.5f;
            UIComponents.Label(new Rect(block.x + padX, block.y, nameW, blockH),
                pawnName, UITheme.FontLabel, UITheme.Muted, TextAnchor.MiddleLeft);
            UIComponents.Label(new Rect(block.x + padX + nameW, block.y, dirW, blockH),
                dirName, UITheme.FontBody, UITheme.Text, TextAnchor.MiddleLeft);

            // 右侧：已获 + 申报（单行：已获 Alive，申报 Accent）
            float rx = block.x + contentW * 0.5f;
            float rw = contentW * 0.5f - padX;
            string grantedLine = "PersonalChronicle.UI.Career.Qual.Header.Granted".Translate(highestGranted).ToString();
            string applyingLine = "PersonalChronicle.UI.Career.Qual.Header.Applying".Translate(currentTier).ToString();
            UIComponents.Label(new Rect(rx, block.y, rw, blockH),
                grantedLine + "   " + applyingLine,
                UITheme.FontBody, UITheme.Accent, TextAnchor.MiddleRight);

            return y + blockH + UITheme.SpaceMd;
        }

        // ============ ② 实践考试 ============
        // v8 体检：始终显示说明 + 要求（件数/最低品质/上限）+ 当前进度，
        // 不靠占位文字——玩家直观看到「要做多少件 / 当前做了多少」。
        private float DrawPracticalExamBlock(Rect view, float y, float contentW, Pawn pawn, PawnObject po,
            CareerData cd, QualificationDef def, bool canApplyExam, string gateExam, bool hasActiveExam, bool practicalPassed)
        {
            float pad = UITheme.PanelPadding;
            // 标题28 + 按钮30 + 描述(2行 36) + 要求(3行 54) + 状态(20) + SpaceMd
            float blockH = 28f + 30f + 36f + 3f * 18f + 20f + UITheme.SpaceMd;
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

            // 说明
            float descY = btnY + 36f;
            DrawWrappedDesc(view.x + pad, descY, contentW - pad * 2f,
                "PersonalChronicle.UI.Career.Qual.Practical.Desc".Translate().ToString(),
                UITheme.FontLabel, UITheme.Muted, 36f);

            // 当前考试记录（取最新一条匹配档位的）
            PracticalExamRecord exam = QualificationFlowService.ActiveExam(po != null ? po.CareerData : null, def.defName);
            // 若已通过但考试记录已被回收，回退到任何 PracticalExamRecord
            if (exam == null && practicalPassed && cd != null && cd.Exams != null && cd.Exams.Practical != null)
            {
                for (int i = cd.Exams.Practical.Count - 1; i >= 0; i--)
                {
                    if (cd.Exams.Practical[i] != null
                        && string.Equals(cd.Exams.Practical[i].QualificationDefName, def.defName, System.StringComparison.Ordinal))
                    {
                        exam = cd.Exams.Practical[i]; break;
                    }
                }
            }

            float reqY = descY + 40f;
            if (exam != null)
            {
                int maxN = exam.MaxProduced > 0 ? exam.MaxProduced : exam.RequiredCount * 2;
                DrawCheckRow(view.x + pad, reqY, contentW - pad * 2f,
                    "PersonalChronicle.UI.Career.Qual.Practical.ReqCount".Translate(exam.RequiredCount.ToString()).ToString(),
                    exam.RequiredCount > 0);
                DrawCheckRow(view.x + pad, reqY + 18f, contentW - pad * 2f,
                    "PersonalChronicle.UI.Career.Qual.Practical.MinQuality".Translate(exam.MinQuality ?? "--").ToString(),
                    !string.IsNullOrEmpty(exam.MinQuality));
                DrawCheckRow(view.x + pad, reqY + 36f, contentW - pad * 2f,
                    "PersonalChronicle.UI.Career.Qual.Practical.MaxCount".Translate(maxN.ToString()).ToString(),
                    maxN > 0);

                float noteY = reqY + 54f;
                string noteText;
                Color noteCol;
                if (exam.Passed)
                {
                    noteText = "PersonalChronicle.UI.Career.Qual.Task.Passed".Translate(exam.Score.ToString("0.0"));
                    noteCol = UITheme.PillGreen;
                }
                else if (exam.Finished)
                {
                    noteText = "PersonalChronicle.UI.Career.Qual.Task.Failed".Translate();
                    noteCol = UITheme.Warn;
                }
                else
                {
                    // 进行中：拼接「当前进度」行
                    int meetN = 0;
                    for (int i = 0; i < exam.ProducedQualities.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(exam.MinQuality)
                            && exam.ProducedQualities[i] == exam.MinQuality) meetN++;
                    }
                    noteText = "PersonalChronicle.UI.Career.Qual.Practical.Current".Translate(
                        exam.ProducedCount.ToString(), exam.RequiredCount.ToString(), meetN.ToString());
                    noteCol = UITheme.Muted;
                }
                UIComponents.Label(new Rect(view.x + pad, noteY, contentW - pad * 2f, 20f), noteText,
                    UITheme.FontLabel, noteCol);
            }
            else
            {
                // 未报名：仍显示要求（取 Def 默认门槛估算）与状态
                int reqCount = 3, maxN = 6; // 默认占位（Def 没显式字段，从当前档传统取值）
                DrawCheckRow(view.x + pad, reqY, contentW - pad * 2f,
                    "PersonalChronicle.UI.Career.Qual.Practical.ReqCount".Translate(reqCount.ToString()).ToString(), false);
                DrawCheckRow(view.x + pad, reqY + 18f, contentW - pad * 2f,
                    "PersonalChronicle.UI.Career.Qual.Practical.MinQuality".Translate("--").ToString(), false);
                DrawCheckRow(view.x + pad, reqY + 36f, contentW - pad * 2f,
                    "PersonalChronicle.UI.Career.Qual.Practical.MaxCount".Translate(maxN.ToString()).ToString(), false);

                float noteY = reqY + 54f;
                UIComponents.Label(new Rect(view.x + pad, noteY, contentW - pad * 2f, 20f),
                    practicalPassed ? "PersonalChronicle.UI.Career.Qual.Practical.Passed".Translate().ToString()
                        : "PersonalChronicle.UI.Career.Qual.Practical.NotApplied".Translate().ToString(),
                    UITheme.FontLabel, practicalPassed ? UITheme.PillGreen : UITheme.Dim);
            }
            return y + blockH;
        }

        // ============ ③ 理论考试 ============
        // v8 体检：始终显示说明 + 4 依据当前评分（Book/Research/Skill/Activity），
        // 让玩家直观看到 P9 流程进度，不靠占位文字。
        private float DrawTheoryBlock(Rect view, float y, float contentW, Pawn pawn, PawnObject po,
            QualificationDef def, bool practicalPassed, bool theoryPassed, bool canSubmitTheory)
        {
            float pad = UITheme.PanelPadding;
            // 标题28 + 按钮30 + 描述(2行 36) + 4依据(4×18) + 状态(20) + SpaceMd
            float blockH = 28f + 30f + 36f + 4f * 18f + 20f + UITheme.SpaceMd;
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

            // 说明（多行 wrap，2 行高）
            float descY = btnY + 36f;
            string desc = "PersonalChronicle.UI.Career.Qual.Theory.Desc".Translate().ToString();
            DrawWrappedDesc(view.x + pad, descY, contentW - pad * 2f, desc, UITheme.FontLabel, UITheme.Muted, 36f);

            // 4 项依据当前评分
            TheoryExamRecord theoryRec = QualificationFlowService.FindTheoryRecord(po, def.defName);
            float checkY = descY + 40f;
            float bookScore = theoryRec != null ? theoryRec.BookScore : 0f;
            float resScore = theoryRec != null ? theoryRec.ResearchScore : 0f;
            float skillScore = theoryRec != null ? theoryRec.SkillScore : 0f;
            float actScore = theoryRec != null ? theoryRec.ActivityScore : 0f;
            int bookTopics = theoryRec != null && theoryRec.RequiredBookTopics != null ? theoryRec.RequiredBookTopics.Count : 0;
            int reqResearch = theoryRec != null ? theoryRec.RequiredResearchCount : 0;

            DrawCheckRow(view.x + pad, checkY, contentW - pad * 2f, "PersonalChronicle.UI.Career.Qual.Theory.Book".Translate(bookScore.ToString("0.0"), bookTopics.ToString()).ToString(), bookScore > 0f);
            DrawCheckRow(view.x + pad, checkY + 18f, contentW - pad * 2f, "PersonalChronicle.UI.Career.Qual.Theory.Research".Translate(resScore.ToString("0.0"), reqResearch.ToString()).ToString(), resScore > 0f);
            DrawCheckRow(view.x + pad, checkY + 36f, contentW - pad * 2f, "PersonalChronicle.UI.Career.Qual.Theory.Skill".Translate(skillScore.ToString("0.0")).ToString(), skillScore > 0f);
            DrawCheckRow(view.x + pad, checkY + 54f, contentW - pad * 2f, "PersonalChronicle.UI.Career.Qual.Theory.Activity".Translate(actScore.ToString("0.0")).ToString(), actScore > 0f);

            // 状态
            float noteY = checkY + 72f;
            UIComponents.Label(new Rect(view.x + pad, noteY, contentW - pad * 2f, 20f),
                theoryPassed ? "PersonalChronicle.UI.Career.Qual.Theory.Passed".Translate()
                    : "PersonalChronicle.UI.Career.Qual.Theory.NotApplied".Translate(),
                UITheme.FontLabel, theoryPassed ? UITheme.PillGreen : UITheme.Dim);
            return y + blockH;
        }

        // ============ helper：绘制 wrap 多行说明（让 Verse.Text 自动 wrap，超高 maxH 不裁剪） ============
        private static void DrawWrappedDesc(float x, float y, float w, string text, GameFont font, Color col, float maxH)
        {
            if (string.IsNullOrEmpty(text)) return;
            UIComponents.Label(new Rect(x, y, w, maxH), text, font, col);
        }

        // ============ helper：绘制单行 ✓/○ 进度行 ============
        private static void DrawCheckRow(float x, float y, float w, string text, bool ok)
        {
            Color prevC = GUI.color;
            GameFont prevF = Verse.Text.Font;
            try
            {
                Rect dot = new Rect(x, y + 4f, 10f, 10f);
                UIComponents.TintedBox(dot, ok ? UITheme.PillGreen : UITheme.Dim);
                Rect txt = new Rect(x + 16f, y, w - 16f, 18f);
                Verse.Text.Font = GameFont.Tiny;
                GUI.color = ok ? UITheme.Text : UITheme.Muted;
                Widgets.Label(txt, text);
            }
            finally
            {
                GUI.color = prevC;
                Verse.Text.Font = prevF;
            }
        }

        // ============ ④ 论文 ============
        // v8 体检：始终显示说明 + 课题 + 引用书籍/研究进度
        private float DrawThesisBlock(Rect view, float y, float contentW, Pawn pawn, PawnObject po,
            QualificationDef def, bool theoryPassed, bool thesisDone, bool canStartThesis, bool canStartDefense)
        {
            float pad = UITheme.PanelPadding;
            // 标题28 + 按钮30 + 描述(2行 36) + 课题/引用(3×18) + 状态(20) + SpaceMd
            float blockH = 28f + 30f + 36f + 3f * 18f + 20f + UITheme.SpaceMd;
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

            // 说明
            float descY = btnY + 36f;
            DrawWrappedDesc(view.x + pad, descY, contentW - pad * 2f,
                "PersonalChronicle.UI.Career.Qual.Thesis.Desc".Translate().ToString(),
                UITheme.FontLabel, UITheme.Muted, 36f);

            ThesisEvidence thesis = QualificationFlowService.FindThesisRecord(po, def.defName);
            float checkY = descY + 40f;

            // 课题行（默认占位）
            DrawCheckRow(view.x + pad, checkY, contentW - pad * 2f,
                thesis != null
                    ? "PersonalChronicle.UI.Career.Qual.Thesis.Topic".Translate(thesis.ThesisId ?? "--").ToString()
                    : "PersonalChronicle.UI.Career.Qual.Thesis.NoTopic".Translate().ToString(),
                thesis != null);
            // 引用书籍
            DrawCheckRow(view.x + pad, checkY + 18f, contentW - pad * 2f,
                "PersonalChronicle.UI.Career.Qual.Thesis.CitedBook".Translate(
                    (thesis != null && thesis.SourceBookIds != null ? thesis.SourceBookIds.Count : 0).ToString()).ToString(),
                thesis != null && thesis.SourceBookIds != null && thesis.SourceBookIds.Count > 0);
            // 引用研究
            DrawCheckRow(view.x + pad, checkY + 36f, contentW - pad * 2f,
                "PersonalChronicle.UI.Career.Qual.Thesis.CitedResearch".Translate(
                    (thesis != null && thesis.SourceResearchEventIds != null ? thesis.SourceResearchEventIds.Count : 0).ToString()).ToString(),
                thesis != null && thesis.SourceResearchEventIds != null && thesis.SourceResearchEventIds.Count > 0);

            float noteY = checkY + 54f;
            UIComponents.Label(new Rect(view.x + pad, noteY, contentW - pad * 2f, 20f),
                thesisDone ? "PersonalChronicle.UI.Career.Qual.Thesis.Done".Translate()
                    : "PersonalChronicle.UI.Career.Qual.Thesis.Todo".Translate(),
                UITheme.FontLabel, thesisDone ? UITheme.PillGreen : UITheme.Dim);
            return y + blockH;
        }

        // ============ ⑤ 答辩 ============
        // v8 体检：始终显示说明 + 评审委员会人数 + 评分 + 评级评审期
        private float DrawDefenseBlock(Rect view, float y, float contentW, Pawn pawn, PawnObject po,
            QualificationDef def, bool thesisDone, bool defenseDone, bool canStartDefense, CareerData cd)
        {
            float pad = UITheme.PanelPadding;
            // 标题28 + 按钮30 + 描述(2行 36) + 委员会+评分(2×18) + 评审提示(20) + SpaceMd
            float blockH = 28f + 30f + 36f + 2f * 18f + 20f + UITheme.SpaceMd;
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

            // 说明
            float descY = btnY + 36f;
            DrawWrappedDesc(view.x + pad, descY, contentW - pad * 2f,
                "PersonalChronicle.UI.Career.Qual.Defense.Desc".Translate().ToString(),
                UITheme.FontLabel, UITheme.Muted, 36f);

            DefenseRecord defRec = QualificationFlowService.FindDefenseRecord(po, def.defName);
            float checkY = descY + 40f;

            int committeeCount = defRec != null && defRec.CommitteePawnIds != null ? defRec.CommitteePawnIds.Count : 0;
            DrawCheckRow(view.x + pad, checkY, contentW - pad * 2f,
                "PersonalChronicle.UI.Career.Qual.Defense.Committee".Translate(committeeCount.ToString()).ToString(),
                committeeCount > 0);
            DrawCheckRow(view.x + pad, checkY + 18f, contentW - pad * 2f,
                "PersonalChronicle.UI.Career.Qual.Defense.Score".Translate(defRec != null ? defRec.CommitteeScore : 0f).ToString(),
                defRec != null && defRec.Passed);

            // 评级评审结算提示（如果评审已启动）
            QualificationProgress progress = cd.Qualification != null ? cd.Qualification.Get(def.defName) : null;
            float noteY = checkY + 36f;
            string statusText = defenseDone
                ? "PersonalChronicle.UI.Career.Qual.Defense.Passed".Translate()
                : (defRec != null ? "PersonalChronicle.UI.Career.Qual.Defense.Todo".Translate()
                    : "PersonalChronicle.UI.Career.Qual.Defense.NoDefense".Translate());
            Color statusCol = defenseDone ? UITheme.PillGreen : UITheme.Dim;
            if (progress != null && progress.ReviewStartedTick > 0L)
            {
                bool due = QualificationReview.IsDue(progress.ReviewStartedTick, progress.ReviewDays, Find.TickManager.TicksGame);
                statusText = due ? "PersonalChronicle.UI.Career.Qual.Review.Due".Translate()
                    : "PersonalChronicle.UI.Career.Qual.Review.Running".Translate();
                statusCol = UITheme.Warn;
            }
            UIComponents.Label(new Rect(view.x + pad, noteY, contentW - pad * 2f, 20f), statusText,
                UITheme.FontLabel, statusCol);
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
            h += 40f + UITheme.SpaceMd; // 身份头（personbar）
            h += 28f + QualTierKeys.Length * (24f + 4f) + UITheme.SpaceMd; // ① 阶梯（纵向列表，紧凑行高）
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
            // v8 体检：考试块从「按钮+占位」改为「说明+进度+状态」详细布局，高度统一公式
            // 标题28 + 按钮30 + 描述36 + 检查项(N×18) + 状态20 + SpaceMd
            h += 28f + 30f + 36f + 3f * 18f + 20f + UITheme.SpaceMd; // ② 实践（要求3项）
            h += 28f + 30f + 36f + 4f * 18f + 20f + UITheme.SpaceMd; // ③ 理论（依据4项）
            h += 28f + 30f + 36f + 3f * 18f + 20f + UITheme.SpaceMd; // ④ 论文（课题/引用/进度3项）
            h += 28f + 30f + 36f + 2f * 18f + 20f + UITheme.SpaceMd; // ⑤ 答辩（委员会/评分2项 + 评审提示1行）
            h += 28f + Mathf.Max(1, cd != null && cd.GrantedTitles != null ? cd.GrantedTitles.Count : 0)
                * 30f + UITheme.SpaceMd;                 // ⑥（按记录数）
            return h + UITheme.SpaceMd;
        }

        // ── 辅助 ──
        // 注：资格流程判定（FlowPassed / HasActiveExam / ActiveExam / Find*Record / GrantedTierIndex /
        // CalcSkillLevel / CalcCareerSpanTicks）已统一上移至 Application.QualificationFlowService，
        // 本页只调用，不重复实现业务规则（UI-001 治理）。
        // 注：申报条件 7 行已统一调 ArchiveUiDataProvider.BuildQualRows + DrawQualCell 渲染（与总览一致），
        // 旧的 DrawConditionRow 已删除（v2.0 §14 一致性：资格状态前端统一管理）。

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

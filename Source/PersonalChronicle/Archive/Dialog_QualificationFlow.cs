// Dialog_QualificationFlow — P9 资格流程对话框（报名实践考试 / 论文 / 答辩）。
// D-D2：由 ITab footer 按钮 + Dialog 触发，零新增 Harmony patch。
// 规则：顺序门控 + 按档隔离（QualificationDefName）+ 制造上限 + 评级评审期（RunQualification 门控）。
//
// 文件头契约
// · 职责：三种资格流程的交互界面——实践考试报名与进度展示、
//   论文四阶段推进（选题 → 引书 ×2 → 引研究 ×2 → 完成）、答辩委员召集与评分。
// · 调用方：ITab_Pawn_Career 的 footer 按钮。
// · 铁律（勿改）：
//   - 本文件只做交互与展示。所有流程判定与状态推进一律委托 QualificationFlowService，
//     禁止在此直接改 CareerData（单一真源）。
//   - 写操作的结果以 Service 返回码反馈，与 QualificationFlowService.Ok 比较后决定消息类型，
//     不要在本文件自行判定成败。
//   - 文案一律走 Translate()，键前缀 PersonalChronicle.UI.Career.Flow.*；禁止硬编码中文。
//   - 颜色与字体一律走 UITheme / UIComponents 令牌，不得直接 new Color。
// · 边界副作用：论文与答辩完成后会生成实体物（卷轴 / 证书）并发信件通知。
//   这是 Application 到 Presentation 边界上必要的副作用，集中在本文件末尾，勿下沉到 Service。
// · 绘制态：DoWindowContents 用 BeginGroup / EndGroup 配对且 EndGroup 置于 finally 中；
//   原生 Window 背景为浅色，已在 BeginGroup 之前用 UITheme.Window 覆盖，两者顺序不可调换。
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
    /// <summary>
    /// 资格流程对话框：按 mode 分派到实践考试 / 论文 / 答辩三种流程界面。
    /// </summary>
    /// <remarks>本类不持有流程状态，所有状态都在 CareerData 上，
    /// 本类只负责展示与把用户操作转交给 QualificationFlowService。</remarks>
    public class Dialog_QualificationFlow : Window
    {
        /// <summary>目标存活 pawn，用于信件收件人与产物生成位置。</summary>
        private readonly Pawn pawn;
        /// <summary>目标 pawn 的归档对象，是流程状态的载体（须经 Service 访问）。</summary>
        private readonly PawnObject pawnObject;
        /// <summary>目标资格定义，同时充当按档隔离的键。</summary>
        private readonly QualificationDef def;
        /// <summary>流程模式：<c>exam</c> / <c>thesis</c> / <c>defense</c>。</summary>
        private readonly string mode; // exam / thesis / defense

        /// <summary>答辩委员会候选缓存；首次绘制时惰性召集，之后复用。</summary>
        private List<Pawn> committeeCache;

        /// <summary>
        /// 构造资格流程对话框。
        /// </summary>
        /// <param name="pawn">目标存活 pawn。</param>
        /// <param name="pawnObject">目标 pawn 的归档对象。</param>
        /// <param name="def">目标资格定义。</param>
        /// <param name="mode">流程模式 exam / thesis / defense；未知值按 defense 处理。</param>
        /// <remarks>强制暂停并吸收周边输入：流程涉及真实资源消耗（书籍、研究文献），
        /// 必须由玩家明确确认，不能被游戏在后台推进。</remarks>
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

        /// <summary>
        /// 窗口初始尺寸。
        /// </summary>
        /// <remarks>考试流程内容较少用 360 高，论文与答辩步骤较多用 440 高。
        /// 新增流程模式时记得在此补尺寸，否则会沿用 440 造成留白或内容溢出。</remarks>
        public override Vector2 InitialSize
        {
            get { return new Vector2(480f, mode == "exam" ? 360f : 440f); }
        }

        /// <summary>
        /// 绘制窗口内容，并按 mode 分派到对应的流程绘制。
        /// </summary>
        /// <param name="inRect">窗口内容区域（各 Draw* 方法内部使用组内相对坐标）。</param>
        /// <remarks>
        /// 底色覆盖必须在 BeginGroup 之前：原生 Window 背景是浅色，
        /// 若在分组之后才覆盖会盖住已绘制的内容。
        /// EndGroup 置于 finally 中，保证绘制抛异常时也能正确出栈，
        /// 否则后续所有 GUI 绘制都会整体错位。
        /// </remarks>
        public override void DoWindowContents(Rect inRect)
        {
            // 原生 Window 默认浅色背景——覆盖为 UITheme.Window 深色（须在 BeginGroup 之前）。
            UIComponents.TintedBox(inRect, UITheme.Window);

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
        /// <summary>
        /// 绘制实践考试流程：要求说明、当前进度与产物质量证据、报名按钮。
        /// </summary>
        /// <param name="r">内容区域（组内相对坐标）。</param>
        /// <remarks>
        /// "进行中"的考试需同时满足三个条件：QualificationDefName 匹配、未通过、未结束且已开始。
        /// 只按 defName 匹配会把历史已完成甚至已失败的记录误判为进行中。
        /// 制造上限展示优先取 MaxProduced，缺失时回退到 RequiredCount 的两倍。
        /// 报名成功后立即关闭窗口，结果由消息条反馈。
        /// </remarks>
        private void DrawExam(Rect r)
        {
            UIComponents.Label(new Rect(0f, 6f, r.width, 24f),
                "PersonalChronicle.UI.Career.Flow.Title.Exam".Translate(def.defName),
                UITheme.FontValue, UITheme.Text);
            UIComponents.Rule(new Rect(0f, 34f, r.width, 1f), UITheme.BorderSoft);

            float y = 46f;
            UIComponents.Label(new Rect(0f, y, r.width, 20f),
                "PersonalChronicle.UI.Career.Flow.Exam.Requirement".Translate(),
                UITheme.FontLabel, UITheme.Muted);
            y += 24f;
            UIComponents.Label(new Rect(0f, y, r.width, 20f),
                "PersonalChronicle.UI.Career.Flow.Exam.Limit".Translate(),
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
                    "PersonalChronicle.UI.Career.Flow.Exam.Produced".Translate(
                        active.ProducedCount, maxN, produced),
                    UITheme.FontLabel, active.Passed ? UITheme.PillGreen : UITheme.Muted);
                y += 24f;
                UIComponents.Label(new Rect(0f, y, r.width, 40f),
                    "PersonalChronicle.UI.Career.Flow.Exam.Evidence".Translate(),
                    UITheme.FontLabel, UITheme.Dim);
                y += 44f;
            }
            else
            {
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "PersonalChronicle.UI.Career.Flow.Exam.NotApplied".Translate(),
                    UITheme.FontLabel, UITheme.Dim);
                y += 26f;
            }

            y = r.height - 40f;
            if (active == null && Widgets.ButtonText(new Rect(0f, y, 150f, 30f),
                "PersonalChronicle.UI.Career.Flow.Btn.Confirm".Translate()))
            {
                string res = QualificationFlowService.ApplyForPracticalExam(pawnObject, def, Find.TickManager.TicksGame);
                Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(res), pawn,
                    res == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                Close();
            }
            if (Widgets.ButtonText(new Rect(160f, y, 120f, 30f),
                "PersonalChronicle.UI.Career.Flow.Btn.Close".Translate()))
            {
                Close();
            }
        }

        // ── 论文：选题 → 引用书籍×2（真实消耗库存）→ 引用研究×2（真实消耗+全局加成）→ 完成 ──
        /// <summary>
        /// 绘制论文流程：四阶段顺序门控（选题 → 引书 ×2 → 引研究 ×2 → 完成）。
        /// </summary>
        /// <param name="r">内容区域（组内相对坐标）。</param>
        /// <remarks>
        /// 阶段之间是严格顺序依赖：引研究要求书籍已齐（books 大于等于 2），
        /// 完成要求书籍与研究都已齐。放宽任一门控都会让论文评分建立在缺失证据之上。
        /// 引书与引研究会真实消耗库存中的实体书与研究文献，属不可逆操作，
        /// 因此窗口强制暂停（见构造函数）是必要保护。
        /// 完成后调用 SpawnThesisScroll 与 SendThesisLetter 产生边界副作用。
        /// </remarks>
        private void DrawThesis(Rect r)
        {
            UIComponents.Label(new Rect(0f, 6f, r.width, 24f),
                "PersonalChronicle.UI.Career.Flow.Title.Thesis".Translate(def.defName),
                UITheme.FontValue, UITheme.Text);
            UIComponents.Rule(new Rect(0f, 34f, r.width, 1f), UITheme.BorderSoft);

            CareerData cd = pawnObject != null ? pawnObject.CareerData : null;
            ThesisEvidence thesis = FindThesis(cd);

            float y = 46f;
            UIComponents.Label(new Rect(0f, y, r.width, 20f),
                "PersonalChronicle.UI.Career.Flow.Thesis.Topic".Translate(),
                UITheme.FontLabel, UITheme.Muted);
            y += 26f;

            Map map = pawn != null ? pawn.Map : null;

            if (thesis == null || thesis.Stage < 1)
            {
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "PersonalChronicle.UI.Career.Flow.Thesis.NoTopic".Translate(),
                    UITheme.FontLabel, UITheme.Dim);
                y += 26f;
                if (Widgets.ButtonText(new Rect(0f, y, 220f, 30f),
                    "PersonalChronicle.UI.Career.Flow.Btn.ChooseTopic".Translate()))
                {
                    string res = QualificationFlowService.StartThesis(pawnObject, def,
                        "thesis_" + def.defName + "_" + Find.TickManager.TicksGame, def.majorKey);
                    Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(res), pawn,
                        res == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                }
                return;
            }

            int books = thesis.SourceBookIds != null ? thesis.SourceBookIds.Count : 0;
            int research = thesis.SourceResearchEventIds != null ? thesis.SourceResearchEventIds.Count : 0;
            UIComponents.Label(new Rect(0f, y, r.width, 20f),
                "PersonalChronicle.UI.Career.Flow.Thesis.Progress".Translate(books, research),
                UITheme.FontLabel, UITheme.Muted);
            y += 26f;

            if (thesis.Completed)
            {
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "PersonalChronicle.UI.Career.Flow.Thesis.Done".Translate(thesis.ComputedScore.ToString("0.0")),
                    UITheme.FontLabel, UITheme.PillGreen);
                y += 26f;
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "PersonalChronicle.UI.Career.Flow.Thesis.Next".Translate(),
                    UITheme.FontLabel, UITheme.Dim);
                return;
            }

            // 阶段2：引用书籍（真实消耗该方向实体书 ×2）
            if (books < 2)
            {
                if (Widgets.ButtonText(new Rect(0f, y, 200f, 30f),
                    "PersonalChronicle.UI.Career.Flow.Btn.CiteBook".Translate()))
                {
                    string res = QualificationFlowService.CiteThesisBooks(pawnObject, def, map);
                    Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(res), pawn,
                        res == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                }
                UIComponents.Label(new Rect(210f, y + 6f, r.width - 210f, 20f),
                    "PersonalChronicle.UI.Career.Flow.Thesis.NeedBooks".Translate(),
                    UITheme.FontLabel, UITheme.Dim);
                y += 38f;
            }

            // 阶段3：书籍齐备后才能引用研究（真实消耗研究文献 ×2 + 全局科研加成）
            if (books >= 2 && research < 2)
            {
                if (Widgets.ButtonText(new Rect(0f, y, 200f, 30f),
                    "PersonalChronicle.UI.Career.Flow.Btn.CiteResearch".Translate()))
                {
                    string res = QualificationFlowService.CiteResearch(pawnObject, def, map);
                    Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(res), pawn,
                        res == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                }
                UIComponents.Label(new Rect(210f, y + 6f, r.width - 210f, 20f),
                    "PersonalChronicle.UI.Career.Flow.Thesis.NeedDocs".Translate(),
                    UITheme.FontLabel, UITheme.Dim);
                y += 38f;
            }

            // 阶段4：书籍+研究齐备 → 完成论文
            bool canFinish = books >= 2 && research >= 2;
            if (canFinish)
            {
                if (Widgets.ButtonText(new Rect(0f, y, 200f, 30f),
                    "PersonalChronicle.UI.Career.Flow.Btn.FinishThesis".Translate()))
                {
                    string res = QualificationFlowService.CompleteThesis(pawnObject, def);
                    Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(res), pawn,
                        res == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                    if (res == QualificationFlowService.Ok)
                    {
                        ThesisEvidence done = FindThesis(pawnObject != null ? pawnObject.CareerData : null);
                        SpawnThesisScroll(done);
                        SendThesisLetter(done);
                    }
                }
            }
            else
            {
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "PersonalChronicle.UI.Career.Flow.Thesis.NeedRefs".Translate(),
                    UITheme.FontLabel, UITheme.Dim);
            }
        }

        // ── 答辩：召集委员（自由殖民者 + Crafting≥5；高一级委员缺失→外部函评）→ 委员会评分 ──
        /// <summary>
        /// 绘制答辩流程：委员列表、外部函评提示、举行答辩与评分按钮。
        /// </summary>
        /// <param name="r">内容区域（组内相对坐标）。</param>
        /// <remarks>
        /// 委员会在首次绘制时惰性召集并缓存，避免每次重绘都扫描全殖民地。
        /// 缺少「高一级职称」委员时显示外部函评提示，并把该标记一并传给 Service，
        /// 由 Service 决定评分口径——本文件不参与任何评分判定。
        /// 评分成功后生成证书并发信，随后关闭窗口。
        /// </remarks>
        private void DrawDefense(Rect r)
        {
            UIComponents.Label(new Rect(0f, 6f, r.width, 24f),
                "PersonalChronicle.UI.Career.Flow.Title.Defense".Translate(def.defName),
                UITheme.FontValue, UITheme.Text);
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
                    "PersonalChronicle.UI.Career.Flow.Defense.Committee".Translate(),
                    UITheme.FontLabel, UITheme.Muted);
                y += 24f;
                for (int i = 0; i < committeeCache.Count; i++)
                {
                    Pawn cm = committeeCache[i];
                    int lv = cm != null && cm.skills != null && cm.skills.GetSkill(SkillDefOf.Crafting) != null
                        ? cm.skills.GetSkill(SkillDefOf.Crafting).Level : 0;
                    UIComponents.Label(new Rect(0f, y, r.width, 20f),
                        "PersonalChronicle.UI.Career.Flow.Defense.Member".Translate(
                            cm != null ? cm.LabelShort : "?", lv),
                        UITheme.FontLabel, UITheme.Dim);
                    y += 22f;
                }
            }
            else
            {
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "PersonalChronicle.UI.Career.Flow.Defense.NoCommittee".Translate(),
                    UITheme.FontLabel, UITheme.Warn);
                y += 24f;
            }
            if (!hasSeniorCommitteeCache)
            {
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "PersonalChronicle.UI.Career.Flow.Defense.ExternalReview".Translate(),
                    UITheme.FontLabel, UITheme.Warn);
                y += 22f;
            }

            if (defense == null || !defense.Passed)
            {
                y = r.height - 40f;
                if (Widgets.ButtonText(new Rect(0f, y, 150f, 30f),
                    "PersonalChronicle.UI.Career.Flow.Btn.HoldDefense".Translate()))
                {
                    string res = QualificationFlowService.StartDefense(pawnObject, def, committeeCache, hasSeniorCommitteeCache);
                    Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(res), pawn,
                        res == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                }
                if (defense != null && Widgets.ButtonText(new Rect(160f, y, 150f, 30f),
                    "PersonalChronicle.UI.Career.Flow.Btn.Grade".Translate()))
                {
                    string res = QualificationFlowService.GradeDefense(pawnObject, def);
                    Messages.Message("PersonalChronicle.UI.Career.Qual.SubmitResult".Translate(res), pawn,
                        res == QualificationFlowService.Ok ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
                    if (res == QualificationFlowService.Ok)
                    {
                        SpawnDiploma(defense);
                        SendDefenseLetter(defense);
                    }
                    Close();
                }
            }
            else
            {
                UIComponents.Label(new Rect(0f, y, r.width, 20f),
                    "PersonalChronicle.UI.Career.Flow.Defense.Passed".Translate(defense.FinalScore.ToString("0.0")),
                    UITheme.FontLabel, UITheme.PillGreen);
            }
        }

        // ── 辅助 ──

        /// <summary>
        /// 按资格 defName 查找对应的论文记录。
        /// </summary>
        /// <param name="cd">职业数据；为 null 或其论文结构缺失时返回 null。</param>
        /// <returns>命中的论文记录；尚未开始论文时返回 null。</returns>
        /// <remarks>按 QualificationDefName 隔离，保证不同资格档位的论文互不干扰。</remarks>
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

        /// <summary>
        /// 按资格 defName 查找对应的答辩记录。
        /// </summary>
        /// <param name="cd">职业数据；为 null 或其答辩结构缺失时返回 null。</param>
        /// <returns>命中的答辩记录；尚未答辩时返回 null。</returns>
        /// <remarks>与 FindThesis 同样按 QualificationDefName 隔离。</remarks>
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

        /// <summary>
        /// 委员会中是否存在「高一级职称」委员（决定采用本地评审还是外部函评）。
        /// </summary>
        /// <remarks>由 GatherCommittee 顺带填充，与 committeeCache 必须同步更新；
        /// 只重置其中一个会造成"展示的委员"与"实际评分口径"不一致。</remarks>
        private bool hasSeniorCommitteeCache;

        /// <summary>
        /// 召集答辩委员：仅取本殖民地的自由殖民者且 Crafting 不小于 5 级，按等级取前 3 名；
        /// 顺带标记是否存在「高一级职称」委员（决定本地评审 / 外部函评）。
        /// </summary>
        /// <returns>委员列表；无合格候选人时返回空列表（不是 null）。</returns>
        /// <remarks>
        /// 排除本人（按 GetUniqueLoadID 比较），并排除访客、囚犯、奴隶——只认自由殖民者。
        /// 等级门槛 5 与取前 3 名属于流程规则的一部分，
        /// 调整会直接影响答辩评分，改动前需与资格流程的设计口径确认。
        /// </remarks>
        private List<Pawn> GatherCommittee()
        {
            List<Pawn> result = new List<Pawn>();
            hasSeniorCommitteeCache = false;
            if (pawnObject == null || def == null) return result;
            string selfId = pawn != null ? pawn.GetUniqueLoadID() : null;
            List<ColonyMember> live = ChronicleColonistScanner.EnumerateCurrentPeople();
            List<Pawn> sorted = new List<Pawn>();
            for (int i = 0; i < live.Count; i++)
            {
                ColonyMember m = live[i];
                if (m == null || m.Pawn == null) continue;
                if (m.Pawn.GetUniqueLoadID() == selfId) continue;
                if (!m.Pawn.IsColonist) continue; // 仅自由殖民者（排除访客/囚犯/奴隶）
                int lv = m.Pawn.skills != null && m.Pawn.skills.GetSkill(SkillDefOf.Crafting) != null
                    ? m.Pawn.skills.GetSkill(SkillDefOf.Crafting).Level : 0;
                if (lv < 5) continue;
                PawnObject cmObj = ChroniclePawnAccess.GetPawnObject(m.Pawn);
                if (QualificationFlowService.IsSeniorCommittee(m.Pawn, cmObj, def)) hasSeniorCommitteeCache = true;
                sorted.Add(m.Pawn);
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

        // ── 产物 Spawn + Letter 通知（Application→Presentation 边界必要副作用，翻译键集中） ──

        /// <summary>
        /// 在 pawn 所在位置生成论文卷轴实体。
        /// </summary>
        /// <param name="done">已完成的论文记录；为 null 时不生成。</param>
        /// <remarks>ThingDef "PC_Exam_Thesis" 缺失时静默跳过：
        /// 产物 Def 可能尚未加载或被第三方移除，此时不应打断流程或抛红字错误。</remarks>
        private void SpawnThesisScroll(ThesisEvidence done)
        {
            if (pawn == null || pawn.Map == null || done == null) return;
            ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail("PC_Exam_Thesis");
            if (td == null) return;
            Thing thing = ThingMaker.MakeThing(td);
            GenSpawn.Spawn(thing, pawn.Position, pawn.Map);
        }

        /// <summary>
        /// 在 pawn 所在位置生成学位证书实体。
        /// </summary>
        /// <param name="defense">已通过的答辩记录；为 null 时不生成。</param>
        /// <remarks>与 SpawnThesisScroll 一致：ThingDef "PC_Exam_Diploma" 缺失时静默跳过。</remarks>
        private void SpawnDiploma(DefenseRecord defense)
        {
            if (pawn == null || pawn.Map == null || defense == null) return;
            ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail("PC_Exam_Diploma");
            if (td == null) return;
            Thing thing = ThingMaker.MakeThing(td);
            GenSpawn.Spawn(thing, pawn.Position, pawn.Map);
        }

        /// <summary>
        /// 发送论文完成的通知信件。
        /// </summary>
        /// <param name="done">已完成的论文记录。</param>
        /// <remarks>信件文案的翻译键集中在本方法内，调用方不要自行拼接信件内容，
        /// 否则多语言支持会不一致。</remarks>
        private void SendThesisLetter(ThesisEvidence done)
        {
            if (pawn == null || done == null) return;
            string label = "PersonalChronicle.UI.Career.Flow.Thesis.Letter.Label".Translate(def.defName).ToString();
            string text = "PersonalChronicle.UI.Career.Flow.Thesis.Letter".Translate(
                pawn.LabelShort, def.defName, done.ComputedScore.ToString("0.0")).ToString();
            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.PositiveEvent, new LookTargets(pawn));
        }

        /// <summary>
        /// 发送答辩通过的通知信件。
        /// </summary>
        /// <param name="defense">已通过的答辩记录。</param>
        /// <remarks>与 SendThesisLetter 相同：翻译键集中在本方法内统一维护。</remarks>
        private void SendDefenseLetter(DefenseRecord defense)
        {
            if (pawn == null || defense == null) return;
            string label = "PersonalChronicle.UI.Career.Flow.Defense.Letter.Label".Translate(def.defName).ToString();
            string text = "PersonalChronicle.UI.Career.Flow.Defense.Letter".Translate(
                pawn.LabelShort, def.defName, defense.FinalScore.ToString("0.0")).ToString();
            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.PositiveEvent, new LookTargets(pawn));
        }
    }
}

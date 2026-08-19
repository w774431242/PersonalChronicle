using System.Collections.Generic;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// P5~P8 职业评价写入层（资格/考试/论文/答辩/职称/勋章）。
    ///
    /// 设计铁律（延续 P1~P4）：
    /// - 只把"评价结果"回写为 CareerEvent 事实（TitleGranted/MedalGranted/ExamPassed/ThesisDefended），
    ///   不污染 ItemProduced 等原始事实。
    /// - Domain 纯逻辑（QualificationEvaluator / AchievementEvaluator / ExamScoring）在 Application 层编排，
    ///   本文件只做持久化挂接 + MarkChanged + 回写事件。
    /// - 自动授予（D-T1 autoGrant=true）默认开启；写入方即唯一写入方，避免逻辑分叉。
    /// </summary>
    public sealed partial class ArchiveService
    {
        // ───────────────────────── P5 资格自动授予 ─────────────────────────

        /// <summary>
        /// 对当前存活归档殖民者执行一次资格判定与自动授予。
        /// 默认自动授予（QualificationDef→ProfessionalTitleDef.autoGrant）：判定 Qualified
        /// 即写入 GrantedTitle + 回写 CareerEvent(TitleGranted)，并触发 MarkChanged。
        /// 返回本次新授予职称的 (Pawn, TitleDef) 列表，调用方可据此弹公告。
        /// </summary>
        /// <summary>
        /// 资格自动授予转发（MATRIX-010 治理后实现已下沉 Data.ChronicleGameComponent；
        /// 本方法保留以维持 IArchiveService 接口与外部调用兼容）。
        /// </summary>
        public List<KeyValuePair<Pawn, ProfessionalTitleDef>> RunQualification(ChronicleGameComponent component)
        {
            return component != null
                ? component.RunQualification()
                : new List<KeyValuePair<Pawn, ProfessionalTitleDef>>();
        }


        // ───────────────────────── P5 实践考试证据捕获（D-E1 复用 P1 采集点） ─────────────────────────

        /// <summary>
        /// 由 Patch_GenRecipe（P1 ItemProduced 采集点）在制造完成时调用：若本次制造命中某
        /// 进行中的实践考试（Metadata examId 关联），记入实践证据并评分。零新增 Harmony patch。
        /// </summary>
        public static void RecordExamProduced(PawnObject pawnObject, string recipeDefName, string quality, long tick)
        {
            if (pawnObject == null || pawnObject.CareerData == null || pawnObject.CareerData.Exams == null)
            {
                return;
            }
            ExamData ex = pawnObject.CareerData.Exams;
            if (ex.Practical == null)
            {
                return;
            }
            for (int i = 0; i < ex.Practical.Count; i++)
            {
                PracticalExamRecord rec = ex.Practical[i];
                if (rec == null || rec.Passed || rec.Finished || rec.StatusNotActive())
                {
                    continue;
                }
                // 限定配方匹配
                if (rec.TargetRecipeDefNames != null && rec.TargetRecipeDefNames.Count > 0
                    && !rec.TargetRecipeDefNames.Contains(recipeDefName))
                {
                    continue;
                }
                rec.ProducedCount++;
                if (!string.IsNullOrEmpty(quality))
                {
                    rec.ProducedQualities.Add(quality);
                }

                // 2026-08-19 验收 P1-2/P1-3 修复：
                // - 最低品质为硬门槛：达 MinQuality 及以上件数 ≥ RequiredCount 才可能通过
                //   （V2.0 §15 "最低品质 Excellent" 是任务要求，不能仅靠评分软性加权）。
                // - 超时即结束考试：以当前证据评分（×0.6 罚分）并置 Finished，不再卡死；
                //   超时后品质硬门槛仍须满足才通过。
                bool timed = rec.TimeLimitTicks > 0L;
                bool inTime = !timed || tick <= rec.StartedTick + rec.TimeLimitTicks;
                if (!inTime)
                {
                    rec.Score = ExamScoring.ScorePractical(
                        rec.RequiredCount, rec.ProducedCount, rec.ProducedQualities,
                        rec.MinQuality, rec.StartedTick, rec.TimeLimitTicks, tick);
                    int met = ExamScoring.CountAtLeast(rec.ProducedQualities, rec.MinQuality);
                    rec.Passed = rec.Score > 0f && met >= rec.RequiredCount;
                    rec.Finished = true;
                    continue;
                }
                if (rec.ProducedCount >= rec.RequiredCount)
                {
                    rec.Score = ExamScoring.ScorePractical(
                        rec.RequiredCount, rec.ProducedCount, rec.ProducedQualities,
                        rec.MinQuality, rec.StartedTick, rec.TimeLimitTicks, tick);
                    int met = ExamScoring.CountAtLeast(rec.ProducedQualities, rec.MinQuality);
                    if (met >= rec.RequiredCount)
                    {
                        rec.Passed = rec.Score > 0f;
                        rec.Finished = true;
                        continue;
                    }
                    // 数量已够但品质未达标：考试继续，等待更高品质产出（不结束）。
                }
                // 制造上限（2026-08-19 流程修补）：一次报名最多制造 MaxProduced（0=2×RequiredCount）
                // 件，超限未达标即失败结束——考试件数有界，避免无限制造。
                int maxN = rec.MaxProduced > 0 ? rec.MaxProduced : rec.RequiredCount * 2;
                if (rec.ProducedCount >= maxN)
                {
                    rec.Score = ExamScoring.ScorePractical(
                        rec.RequiredCount, rec.ProducedCount, rec.ProducedQualities,
                        rec.MinQuality, rec.StartedTick, rec.TimeLimitTicks, tick);
                    rec.Passed = false;
                    rec.Finished = true;
                }
            }
        }

        // ───────────────────────── P6 书籍证据捕获 ─────────────────────────

        /// <summary>
        /// 由 P1 BookProduced 采集点调用：同步构造 BookEvidence 存入 CareerData.Books（D-B1）。
        /// </summary>
        public static void RecordBookEvidence(PawnObject pawnObject, string bookThingId, string authorPawnId, string topic, string quality, string field, long tick)
        {
            if (pawnObject == null || pawnObject.CareerData == null)
            {
                return;
            }
            if (pawnObject.CareerData.Books == null)
            {
                pawnObject.CareerData.Books = new List<BookEvidence>();
            }
            pawnObject.CareerData.Books.Add(new BookEvidence
            {
                BookThingId = bookThingId,
                AuthorPawnId = authorPawnId,
                Topic = topic,
                Quality = quality,
                Field = field,
                CreatedTick = tick,
                Relevance = 1f
            });
        }
    }

    /// <summary>PracticalExamRecord 辅助（活动态判定）。</summary>
    internal static class PracticalExamRecordExtensions
    {
        public static bool StatusNotActive(this PersonalChronicle.Domain.Qualification.PracticalExamRecord rec)
        {
            return rec.StartedTick <= 0L;
        }
    }

    /// <summary>
    /// P9 资格流程编排（2026-08-19 落地：考试报名/理论提交/论文/答辩）。
    /// 规则继承：顺序门控 + 按档隔离（QualificationDefName 匹配）+ 制造上限（RecordExamProduced 已含）
    /// + 评级评审期（RunQualification 已含）。只写状态/事实，不直接渲染。
    /// </summary>
    public static class QualificationFlowService
    {
        public const string Ok = "ok";

        // ── 实践考试报名（顺序：当前申报档，无进行中考试，等级/资历达标） ──

        /// <summary>可否报名实践考试（纯逻辑，可单测）。返回 Ok 或原因码。</summary>
        public static string CanApplyExam(QualificationDef def, int level, long spanTicks, bool hasActiveExam)
        {
            if (def == null) return "noDef";
            if (hasActiveExam) return "active";
            if (level < def.requiredMinLevel) return "level";
            if (spanTicks < def.requiredCareerTimeTicks) return "careerTime";
            return Ok;
        }

        /// <summary>创建一次实践考试任务（真实制造证据由 RecordExamProduced 捕获，D-E1）。</summary>
        public static string ApplyForPracticalExam(PawnObject pawnObject, QualificationDef def, long nowTick, long timeLimitTicks = 100000L)
        {
            if (pawnObject == null || pawnObject.CareerData == null || def == null) return "noData";
            CareerData cd = pawnObject.CareerData;
            ExamData ex = cd.Exams ?? (cd.Exams = new ExamData());
            if (ex.Practical == null) ex.Practical = new List<PracticalExamRecord>();
            bool active = false;
            for (int i = 0; i < ex.Practical.Count; i++)
            {
                PracticalExamRecord r = ex.Practical[i];
                if (r != null && string.Equals(r.QualificationDefName, def.defName, System.StringComparison.Ordinal)
                    && !r.Passed && !r.Finished && r.StartedTick > 0L)
                {
                    active = true;
                    break;
                }
            }
            if (active) return "active";
            // 等级/资历前置（纯逻辑校验；时长按事件首末跨度估算）
            int level = 0;
            ProfessionalState ps = cd.Professional;
            if (ps != null && !string.IsNullOrEmpty(def.professionalSkillDefName))
            {
                ProfessionalSkillData sd = ps.GetSkill(def.professionalSkillDefName);
                if (sd != null) level = sd.level;
            }
            long span = 0L;
            if (cd.Events != null && cd.Events.Count > 0)
            {
                long first = long.MaxValue, last = 0L;
                for (int i = 0; i < cd.Events.Count; i++)
                {
                    if (cd.Events[i] == null) continue;
                    if (cd.Events[i].Tick < first) first = cd.Events[i].Tick;
                    if (cd.Events[i].Tick > last) last = cd.Events[i].Tick;
                }
                if (first != long.MaxValue) span = last - first;
            }
            string gate = CanApplyExam(def, level, span, false);
            if (gate != Ok) return gate;
            ex.Practical.Add(new PracticalExamRecord
            {
                ExamId = "exam_" + def.defName + "_" + nowTick,
                QualificationDefName = def.defName,
                TargetRecipeDefNames = new List<string>(),
                RequiredCount = 3,
                MinQuality = "Excellent",
                MaxProduced = 0, // 默认 2×RequiredCount
                TimeLimitTicks = timeLimitTicks,
                StartedTick = nowTick,
            });
            return Ok;
        }

        // ── 理论考试提交（顺序：本档实践考试通过；评分 = 书籍/研究/技能/活动 加权） ──

        /// <summary>理论考试是否已通过（本档）。</summary>
        public static bool TheoryPassedFor(PawnObject pawnObject, string qualDefName)
        {
            if (pawnObject == null || pawnObject.CareerData == null || pawnObject.CareerData.Exams == null
                || pawnObject.CareerData.Exams.Theory == null)
            {
                return false;
            }
            List<TheoryExamRecord> list = pawnObject.CareerData.Exams.Theory;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Passed
                    && string.Equals(list[i].QualificationDefName, qualDefName, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 提交理论考试（V2.0 §16 加权合成）：0.4 书籍 + 0.3 研究 + 0.2 技能 + 0.1 活动。
        /// 书籍=Books 数折算；研究=ResearchCompleted 事件数折算；技能=专业等级折算；活动=阶段一常量 70。
        /// </summary>
        public static string SubmitTheoryExam(PawnObject pawnObject, QualificationDef def)
        {
            if (pawnObject == null || pawnObject.CareerData == null || def == null) return "noData";
            CareerData cd = pawnObject.CareerData;
            // 顺序门控：本档实践考试须已通过
            bool practicalPassed = false;
            if (cd.Exams != null && cd.Exams.Practical != null)
            {
                for (int i = 0; i < cd.Exams.Practical.Count; i++)
                {
                    PracticalExamRecord r = cd.Exams.Practical[i];
                    if (r != null && r.Passed
                        && string.Equals(r.QualificationDefName, def.defName, System.StringComparison.Ordinal))
                    {
                        practicalPassed = true;
                        break;
                    }
                }
            }
            if (!practicalPassed) return "examFirst";
            if (TheoryPassedFor(pawnObject, def.defName)) return "already";
            ExamData ex = cd.Exams ?? (cd.Exams = new ExamData());
            if (ex.Theory == null) ex.Theory = new List<TheoryExamRecord>();

            int books = cd.Books != null ? cd.Books.Count : 0;
            int research = 0;
            if (cd.Events != null)
            {
                for (int i = 0; i < cd.Events.Count; i++)
                {
                    if (cd.Events[i] != null
                        && string.Equals(cd.Events[i].EventType, CareerEventType.ResearchCompleted, System.StringComparison.Ordinal))
                    {
                        research++;
                    }
                }
            }
            int level = 0;
            if (cd.Professional != null && !string.IsNullOrEmpty(def.professionalSkillDefName))
            {
                ProfessionalSkillData sd = cd.Professional.GetSkill(def.professionalSkillDefName);
                if (sd != null) level = sd.level;
            }
            float bookScore = ScoreEvidence(books, 85f);
            float researchScore = ScoreEvidence(research, 80f);
            float skillScore = level > 0 ? Mathf.Clamp(level / 50f * 100f, 0f, 100f) : 0f;
            float activityScore = 70f;
            float score = ExamScoring.ScoreTheory(bookScore, researchScore, skillScore, activityScore);
            ex.Theory.Add(new TheoryExamRecord
            {
                QualificationDefName = def.defName,
                BookScore = bookScore,
                ResearchScore = researchScore,
                SkillScore = skillScore,
                ActivityScore = activityScore,
                Passed = score >= 60f,
                Score = score,
            });
            return Ok;
        }

        // ── 论文（按档；引用书籍/研究 → 论文质量） ──

        /// <summary>开始论文（本档；前置：本档理论考试已提交）。</summary>
        public static string StartThesis(PawnObject pawnObject, QualificationDef def, string thesisId)
        {
            if (pawnObject == null || pawnObject.CareerData == null || def == null) return "noData";
            CareerData cd = pawnObject.CareerData;
            if (!TheoryPassedFor(pawnObject, def.defName)) return "theoryFirst";
            ThesisData td = cd.Thesis ?? (cd.Thesis = new ThesisData());
            if (td.Theses == null) td.Theses = new List<ThesisEvidence>();
            for (int i = 0; i < td.Theses.Count; i++)
            {
                if (td.Theses[i] != null && td.Theses[i].Completed
                    && string.Equals(td.Theses[i].QualificationDefName, def.defName, System.StringComparison.Ordinal))
                {
                    return "already";
                }
            }
            td.Theses.Add(new ThesisEvidence
            {
                ThesisId = thesisId,
                QualificationDefName = def.defName,
                SourceBookIds = new List<string>(),
                SourceResearchEventIds = new List<string>(),
                BaseQuality = 0f,
                ComputedScore = 0f,
                Completed = false,
            });
            return Ok;
        }

        /// <summary>引用书籍到本档论文（对齐 D-B1：书籍是理论证据）。</summary>
        public static string CiteThesisBook(PawnObject pawnObject, QualificationDef def, string bookThingId)
        {
            ThesisEvidence t = FindThesis(pawnObject, def);
            if (t == null) return "noThesis";
            if (t.Completed) return "done";
            if (t.SourceBookIds == null) t.SourceBookIds = new List<string>();
            if (!t.SourceBookIds.Contains(bookThingId)) t.SourceBookIds.Add(bookThingId);
            return Ok;
        }

        /// <summary>完成论文（ThesisQuality = 0.4 书籍 + 0.3 研究 + 0.3 专业成果）。</summary>
        public static string CompleteThesis(PawnObject pawnObject, QualificationDef def)
        {
            ThesisEvidence t = FindThesis(pawnObject, def);
            if (t == null) return "noThesis";
            if (t.Completed) return "done";
            int books = t.SourceBookIds != null ? t.SourceBookIds.Count : 0;
            int research = t.SourceResearchEventIds != null ? t.SourceResearchEventIds.Count : 0;
            if (books < 2 || research < 2) return "evidence";
            int level = 0;
            if (pawnObject.CareerData != null && pawnObject.CareerData.Professional != null)
            {
                ProfessionalSkillData sd = pawnObject.CareerData.Professional.GetSkill(def.professionalSkillDefName);
                if (sd != null) level = sd.level;
            }
            float bookAvg = ScoreEvidence(books, 85f);
            float researchScore = ScoreEvidence(research, 80f);
            float professionalScore = level > 0 ? Mathf.Clamp(level / 50f * 100f, 0f, 100f) : 0f;
            t.BaseQuality = bookAvg;
            t.ComputedScore = ExamScoring.ScoreThesis(bookAvg, researchScore, professionalScore);
            t.Completed = true;
            t.CompletedTick = Find.TickManager.TicksGame;
            return Ok;
        }

        // ── 答辩（按档；论文完成后召集委员 → 委员会评分） ──

        /// <summary>召开答辩（前置：本档论文完成；委员由 UI 层提供——同阵营高等级 Pawn，D-D1）。</summary>
        public static string StartDefense(PawnObject pawnObject, QualificationDef def, List<Pawn> committee)
        {
            if (pawnObject == null || pawnObject.CareerData == null || def == null) return "noData";
            ThesisEvidence t = FindThesis(pawnObject, def);
            if (t == null || !t.Completed) return "thesisFirst";
            CareerData cd = pawnObject.CareerData;
            ThesisData td = cd.Thesis ?? (cd.Thesis = new ThesisData());
            if (td.Defenses == null) td.Defenses = new List<DefenseRecord>();
            for (int i = 0; i < td.Defenses.Count; i++)
            {
                if (td.Defenses[i] != null && td.Defenses[i].Passed
                    && string.Equals(td.Defenses[i].QualificationDefName, def.defName, System.StringComparison.Ordinal))
                {
                    return "already";
                }
            }
            List<string> ids = new List<string>();
            if (committee != null)
            {
                for (int i = 0; i < committee.Count; i++)
                {
                    if (committee[i] != null) ids.Add(committee[i].GetUniqueLoadID());
                }
            }
            td.Defenses.Add(new DefenseRecord
            {
                ThesisId = t.ThesisId,
                QualificationDefName = def.defName,
                CommitteePawnIds = ids,
                CommitteeScore = 0f,
                FinalScore = 0f,
                Passed = false,
                HeldTick = Find.TickManager.TicksGame,
            });
            return Ok;
        }

        /// <summary>委员会评分（委员分 = 其 Crafting 等级派生；Final = Thesis×0.5 + Committee×0.5）。</summary>
        public static string GradeDefense(PawnObject pawnObject, QualificationDef def, List<Pawn> committee)
        {
            if (pawnObject == null || pawnObject.CareerData == null || def == null) return "noData";
            ThesisData td = pawnObject.CareerData.Thesis;
            if (td == null || td.Defenses == null) return "noDefense";
            DefenseRecord d = null;
            for (int i = 0; i < td.Defenses.Count; i++)
            {
                if (td.Defenses[i] != null && !td.Defenses[i].Passed
                    && string.Equals(td.Defenses[i].QualificationDefName, def.defName, System.StringComparison.Ordinal))
                {
                    d = td.Defenses[i];
                    break;
                }
            }
            if (d == null) return "noDefense";
            ThesisEvidence t = FindThesis(pawnObject, def);
            if (t == null || !t.Completed) return "thesisFirst";
            float committeeScore = 0f;
            int n = committee != null ? committee.Count : 0;
            if (n > 0)
            {
                float sum = 0f;
                for (int i = 0; i < n; i++)
                {
                    Pawn p = committee[i];
                    int craft = p != null && p.skills != null && p.skills.GetSkill(SkillDefOf.Crafting) != null
                        ? p.skills.GetSkill(SkillDefOf.Crafting).Level : 8;
                    sum += Mathf.Clamp(70f + craft * 1.5f, 70f, 95f);
                }
                committeeScore = sum / n;
            }
            else
            {
                committeeScore = 80f; // 无委员兜底（正常流程 UI 会提供委员）
            }
            d.CommitteeScore = committeeScore;
            d.FinalScore = ExamScoring.ScoreDefense(t.ComputedScore, committeeScore);
            d.Passed = d.FinalScore >= 60f;
            return Ok;
        }

        // ── 辅助 ──

        private static ThesisEvidence FindThesis(PawnObject pawnObject, QualificationDef def)
        {
            if (pawnObject == null || pawnObject.CareerData == null || pawnObject.CareerData.Thesis == null
                || pawnObject.CareerData.Thesis.Theses == null || def == null)
            {
                return null;
            }
            List<ThesisEvidence> list = pawnObject.CareerData.Thesis.Theses;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null
                    && string.Equals(list[i].QualificationDefName, def.defName, System.StringComparison.Ordinal))
                {
                    return list[i];
                }
            }
            return null;
        }

        private static float ScoreEvidence(int count, float baseScore)
        {
            if (count <= 0) return 0f;
            return baseScore + Mathf.Min(15f, count * 2f);
        }
    }
}

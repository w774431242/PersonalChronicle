using System.Collections.Generic;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
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
}

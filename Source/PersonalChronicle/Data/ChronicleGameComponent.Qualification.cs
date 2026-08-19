// ChronicleGameComponent partial：P5~P8 职业评价写入层（资格自动授予）。
// 按 MATRIX-010 治理：RunQualification 从 Application 下沉到 Data 层（编排依赖
// QualificationEvaluator(Domain) + 本组件写入能力，无 Application 依赖）；
// Application.ArchiveService.RunQualification 保留为转发（接口兼容）。
// 设计铁律：只把"评价结果"回写为 CareerEvent 事实（TitleGranted），不污染原始事实；
// 自动授予（autoGrant=true）默认开启，写入方即唯一写入方。
using System.Collections.Generic;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using Verse;

namespace PersonalChronicle.Data
{
    public sealed partial class ChronicleGameComponent
    {
        /// <summary>
        /// 对当前存活归档殖民者执行一次资格判定与自动授予。
        /// 判定 Qualified 即写入 GrantedTitle + 回写 CareerEvent(TitleGranted)，并触发 MarkChanged。
        /// 返回本次新授予职称的 (Pawn, TitleDef) 列表。
        /// </summary>
        public List<KeyValuePair<Pawn, ProfessionalTitleDef>> RunQualification()
        {
            List<KeyValuePair<Pawn, ProfessionalTitleDef>> granted = new List<KeyValuePair<Pawn, ProfessionalTitleDef>>();
            if (Objects == null)
            {
                return granted;
            }
            if (PersonalChronicleMod.Settings == null || !PersonalChronicleMod.Settings.EnableRecording)
            {
                return granted;
            }

            List<ColonyMember> live = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < live.Count; i++)
            {
                ColonyMember member = live[i];
                if (member == null || member.Pawn == null)
                {
                    continue;
                }
                Pawn pawn = member.Pawn;
                IReadOnlyList<PawnRecord> records = GetRecordsFor(pawn);
                if (records == null || records.Count == 0)
                {
                    continue;
                }
                PawnObject pawnObject = records[0] as PawnObject;
                if (pawnObject == null || pawnObject.IsArchived || pawnObject.CareerData == null)
                {
                    continue;
                }

                List<QualificationEvaluator.Eligibility> eligibilities = QualificationEvaluator.Evaluate(pawnObject);
                for (int j = 0; j < eligibilities.Count; j++)
                {
                    QualificationEvaluator.Eligibility e = eligibilities[j];
                    if (e == null || e.Def == null || !e.Eligible)
                    {
                        continue;
                    }
                    // 更新进度状态机
                    QualificationProgress progress = EnsureQualificationProgress(pawnObject, e.Def.defName);
                    progress.CompositeScore = e.CompositeScore;
                    long now = Find.TickManager.TicksGame;

                    // 2026-08-19 评级评审期：结算评级以工作日答复，不自动即时授予。
                    // 资格满足（含答辩）首次检测 → 进入 Review（记录开始 tick）；未到期 → 保持 Review。
                    if (progress.ReviewStartedTick <= 0L)
                    {
                        progress.ReviewStartedTick = now;
                        progress.ReviewDays = e.Def.reviewDays > 0 ? e.Def.reviewDays : QualificationReview.DefaultReviewDays;
                        progress.Status = QualificationStatus.Review;
                        progress.DecidedTick = now;
                        continue;
                    }
                    if (!QualificationReview.IsDue(progress.ReviewStartedTick, progress.ReviewDays, now))
                    {
                        progress.Status = QualificationStatus.Review;
                        continue;
                    }
                    // 评审到期 → 授予（或标记 Qualified 等待 UI 确认，autoGrant=false 时）
                    progress.Status = QualificationStatus.Qualified;

                    ProfessionalTitleDef titleDef = DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(e.Def.titleDefName);
                    if (titleDef == null || !titleDef.autoGrant)
                    {
                        continue;
                    }
                    // 防重复授予
                    if (HasTitle(pawnObject, titleDef.defName))
                    {
                        progress.Status = QualificationStatus.Granted;
                        continue;
                    }
                    GrantTitle(pawn, pawnObject, titleDef, e.Def);
                    progress.Status = QualificationStatus.Granted;
                    granted.Add(new KeyValuePair<Pawn, ProfessionalTitleDef>(pawn, titleDef));
                }
                MarkChanged();
            }
            return granted;
        }

        private static QualificationProgress EnsureQualificationProgress(PawnObject pawnObject, string defName)
        {
            if (pawnObject.CareerData.Qualification == null)
            {
                pawnObject.CareerData.Qualification = new QualificationState();
            }
            return pawnObject.CareerData.Qualification.GetOrAdd(defName);
        }

        private static bool HasTitle(PawnObject pawnObject, string titleDefName)
        {
            if (pawnObject.CareerData.GrantedTitles == null)
            {
                return false;
            }
            for (int i = 0; i < pawnObject.CareerData.GrantedTitles.Count; i++)
            {
                if (pawnObject.CareerData.GrantedTitles[i] != null
                    && string.Equals(pawnObject.CareerData.GrantedTitles[i].TitleDefName, titleDefName, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void GrantTitle(Pawn pawn, PawnObject pawnObject, ProfessionalTitleDef titleDef, QualificationDef qualDef)
        {
            long tick = Find.TickManager.TicksGame;
            string pawnId = pawn != null ? pawn.GetUniqueLoadID() : (pawnObject.StableId ?? string.Empty);
            if (pawnObject.CareerData.GrantedTitles == null)
            {
                pawnObject.CareerData.GrantedTitles = new List<GrantedTitle>();
            }
            pawnObject.CareerData.GrantedTitles.Add(new GrantedTitle(titleDef.defName, qualDef.defName, tick));
            // 回写事实：职称授予（不写评价数值，仅事实）
            pawnObject.CareerData.Events.Add(new CareerEvent(
                pawnId + ":" + tick + ":title:" + titleDef.defName,
                pawnId,
                tick,
                CareerEventType.TitleGranted,
                titleDef.defName,
                qualDef.professionalSkillDefName,
                null,
                null,
                1,
                null));
        }
    }
}

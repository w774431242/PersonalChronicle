using System.Collections.Generic;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Qualification;

namespace PersonalChronicle.Domain.Honor
{
    /// <summary>
    /// P8 成就评价引擎（V2.0 §20：CareerEvent 事实 → AchievementEvaluator → HonorEligibility）。
    /// Domain 纯逻辑、零 Verse 依赖、零副作用；只聚合 CareerEvent 事实，不读专业等级、
    /// 不写任何持久化字段（写入方是调用层）。
    ///
    /// 阶段一成就键（制造类）：
    ///   MajorProjects  大型制造项目数（Metadata["major"]=="1" 标记）
    ///   LegendaryMade  传奇品质制造次数（Quality=="Legendary" 的 ItemProduced）
    ///   LongServiceTicks 职业时长（按 ItemProduced 首末事件跨度估算，无则 0）
    ///   TitleCount     已获职称数（GrantedTitles.Count）
    ///   ExamPassCount  考试通过数（ExamPassed 事件数）
    /// </summary>
    public static class AchievementEvaluator
    {
        public const string MajorProjects = "MajorProjects";
        public const string LegendaryMade = "LegendaryMade";
        public const string LongServiceTicks = "LongServiceTicks";
        public const string TitleCount = "TitleCount";
        public const string ExamPassCount = "ExamPassCount";

        /// <summary>聚合某 Pawn 的全部成就键 → 值。null/空 CareerData 返回空字典。</summary>
        public static Dictionary<string, double> Aggregate(PawnObject pawn)
        {
            Dictionary<string, double> result = new Dictionary<string, double>();
            if (pawn == null || pawn.CareerData == null || pawn.CareerData.Events == null)
            {
                return result;
            }
            List<CareerEvent> events = pawn.CareerData.Events;

            int major = 0;
            int legendary = 0;
            int examPass = 0;
            int titleGrant = 0;
            long firstTick = long.MaxValue;
            long lastTick = 0L;

            for (int i = 0; i < events.Count; i++)
            {
                CareerEvent ev = events[i];
                if (ev == null)
                {
                    continue;
                }
                if (ev.Tick < firstTick) firstTick = ev.Tick;
                if (ev.Tick > lastTick) lastTick = ev.Tick;

                if (ev.EventType == CareerEventType.ItemProduced)
                {
                    if (string.Equals(ev.Quality, "Legendary", System.StringComparison.Ordinal))
                    {
                        legendary++;
                    }
                    if (ev.Metadata != null && ev.Metadata.TryGetValue("major", out string mv) && mv == "1")
                    {
                        major++;
                    }
                }
                else if (ev.EventType == CareerEventType.ExamPassed)
                {
                    examPass++;
                }
                else if (ev.EventType == CareerEventType.TitleGranted)
                {
                    titleGrant++;
                }
            }

            result[MajorProjects] = major;
            result[LegendaryMade] = legendary;
            result[ExamPassCount] = examPass;
            result[TitleCount] = titleGrant;
            result[LongServiceTicks] = firstTick == long.MaxValue ? 0L : (double)(lastTick - firstTick);
            return result;
        }
    }
}

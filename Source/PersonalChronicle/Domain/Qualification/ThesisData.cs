using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Qualification
{
    /// <summary>
    /// P6 书籍证据（V2.0 §17：理论证据而非道具）。
    /// 由 P1 BookProduced 采集点同步构造，存于 CareerData.Books。
    /// </summary>
    public sealed class BookEvidence : IExposable
    {
        public string BookThingId;
        public string AuthorPawnId;
        public string Topic;
        public string Quality;
        public string Field;
        public long CreatedTick;
        /// <summary>与申请资格的相关度 0~1（由 Def 映射，阶段一常量 1.0）。</summary>
        public float Relevance = 1f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref BookThingId, "bookThingId");
            Scribe_Values.Look(ref AuthorPawnId, "authorPawnId");
            Scribe_Values.Look(ref Topic, "topic");
            Scribe_Values.Look(ref Quality, "quality");
            Scribe_Values.Look(ref Field, "field");
            Scribe_Values.Look(ref CreatedTick, "createdTick", 0L);
            Scribe_Values.Look(ref Relevance, "relevance", 1f);
        }
    }

    /// <summary>
    /// P6 论文证据（V2.0 §18：书籍+研究+专业成果+履历+专家 共同形成质量，非 +1 Thesis）。
    /// </summary>
    public sealed class ThesisEvidence : IExposable
    {
        public string ThesisId;
        public string QualificationDefName;
        public List<string> SourceBookIds = new List<string>();
        public List<string> SourceResearchEventIds = new List<string>();
        /// <summary>由书籍/研究聚合的基础质量 0~100。</summary>
        public float BaseQuality;
        /// <summary>综合论文质量 0~100。</summary>
        public float ComputedScore;
        public bool Completed;
        public long CompletedTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ThesisId, "thesisId");
            Scribe_Values.Look(ref QualificationDefName, "qualificationDefName");
            Scribe_Collections.Look(ref SourceBookIds, "sourceBookIds", LookMode.Value);
            Scribe_Collections.Look(ref SourceResearchEventIds, "sourceResearchEventIds", LookMode.Value);
            Scribe_Values.Look(ref BaseQuality, "baseQuality", 0f);
            Scribe_Values.Look(ref ComputedScore, "computedScore", 0f);
            Scribe_Values.Look(ref Completed, "completed", false);
            Scribe_Values.Look(ref CompletedTick, "completedTick", 0L);
            if (SourceBookIds == null) SourceBookIds = new List<string>();
            if (SourceResearchEventIds == null) SourceResearchEventIds = new List<string>();
        }
    }

    /// <summary>
    /// P6 答辩记录（V2.0 §18 + D-D1：同阵营高等级 Pawn 自动委员，不依赖 DLC Activity API）。
    /// 2026-08-19 验收 P1-4 修复：新增 QualificationDefName 关联字段（答辩归属资格而非论文）。
    /// 旧存档记录该字段为空 → 判定回退 ThesisId 匹配（仅兼容早期 DevTest 数据）。
    /// </summary>
    public sealed class DefenseRecord : IExposable
    {
        public string ThesisId;
        /// <summary>关联 QualificationDef.defName（答辩归属的资格；新数据必填）。</summary>
        public string QualificationDefName;
        public List<string> CommitteePawnIds = new List<string>();
        /// <summary>委员平均评分 0~100。</summary>
        public float CommitteeScore;
        /// <summary>最终评分 = ThesisQuality*0.5 + CommitteeScore*0.5。</summary>
        public float FinalScore;
        public bool Passed;
        public long HeldTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ThesisId, "thesisId");
            Scribe_Values.Look(ref QualificationDefName, "qualificationDefName");
            Scribe_Collections.Look(ref CommitteePawnIds, "committeePawnIds", LookMode.Value);
            Scribe_Values.Look(ref CommitteeScore, "committeeScore", 0f);
            Scribe_Values.Look(ref FinalScore, "finalScore", 0f);
            Scribe_Values.Look(ref Passed, "passed", false);
            Scribe_Values.Look(ref HeldTick, "heldTick", 0L);
            if (CommitteePawnIds == null) CommitteePawnIds = new List<string>();
        }
    }

    /// <summary>P6 论文/答辩数据容器（挂 CareerData 下）。</summary>
    public sealed class ThesisData : IExposable
    {
        public List<ThesisEvidence> Theses = new List<ThesisEvidence>();
        public List<DefenseRecord> Defenses = new List<DefenseRecord>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref Theses, "theses", LookMode.Deep);
            Scribe_Collections.Look(ref Defenses, "defenses", LookMode.Deep);
            if (Theses == null) Theses = new List<ThesisEvidence>();
            if (Defenses == null) Defenses = new List<DefenseRecord>();
        }
    }
}

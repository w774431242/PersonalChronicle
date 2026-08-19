using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Qualification
{
    /// <summary>
    /// P5 实践考试记录（V2.0 §15：真实制造行为形成证据，禁假"考试按钮"）。
    /// 证据捕获复用 P1 ItemProduced 采集点（D-E1），通过 Metadata["examId"] 关联。
    /// </summary>
    public sealed class PracticalExamRecord : IExposable
    {
        /// <summary>考试实例稳定 id（运行时生成，写入 CareerEvent.Metadata["examId"]）。</summary>
        public string ExamId;

        /// <summary>关联 QualificationDef.defName。</summary>
        public string QualificationDefName;

        /// <summary>限定配方白名单（空=不限）。</summary>
        public List<string> TargetRecipeDefNames = new List<string>();

        /// <summary>要求数量。</summary>
        public int RequiredCount;

        /// <summary>最低品质（QualityCategory name，如 "Excellent"）。</summary>
        public string MinQuality;

        /// <summary>时限（tick）。</summary>
        public long TimeLimitTicks;

        public long StartedTick;
        public int ProducedCount;
        public List<string> ProducedQualities = new List<string>();

        public bool Passed;
        /// <summary>
        /// 考试是否已结束（通过或超时终止）。append-only 新字段（2026-08-19 验收 P1-3 修复）：
        /// 旧存档 null/缺失 = 未结束，兼容。超时后考试以当前证据评分并结束，不再继续累计。
        /// </summary>
        public bool Finished;
        /// <summary>评分 0~100。</summary>
        public float Score;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ExamId, "examId");
            Scribe_Values.Look(ref QualificationDefName, "qualificationDefName");
            Scribe_Collections.Look(ref TargetRecipeDefNames, "targetRecipeDefNames", LookMode.Value);
            Scribe_Values.Look(ref RequiredCount, "requiredCount", 0);
            Scribe_Values.Look(ref MinQuality, "minQuality");
            Scribe_Values.Look(ref TimeLimitTicks, "timeLimitTicks", 0L);
            Scribe_Values.Look(ref StartedTick, "startedTick", 0L);
            Scribe_Values.Look(ref ProducedCount, "producedCount", 0);
            Scribe_Collections.Look(ref ProducedQualities, "producedQualities", LookMode.Value);
            Scribe_Values.Look(ref Passed, "passed", false);
            Scribe_Values.Look(ref Finished, "finished", false);
            Scribe_Values.Look(ref Score, "score", 0f);
            if (TargetRecipeDefNames == null) TargetRecipeDefNames = new List<string>();
            if (ProducedQualities == null) ProducedQualities = new List<string>();
        }
    }

    /// <summary>
    /// P5 理论考试记录（V2.0 §16：第一阶段无 AI 问答，加权合成）。
    /// 成绩 = wBook*BookScore + wResearch*ResearchScore + wSkill*SkillScore + wActivity*ActivityScore。
    /// </summary>
    public sealed class TheoryExamRecord : IExposable
    {
        public string QualificationDefName;

        /// <summary>需阅读的书籍主题白名单（与 BookEvidence.Topic 匹配）。</summary>
        public List<string> RequiredBookTopics = new List<string>();

        public int RequiredResearchCount;
        public float BookScore;
        public float ResearchScore;
        public float SkillScore;
        public float ActivityScore;

        public bool Passed;
        /// <summary>评分 0~100。</summary>
        public float Score;

        public void ExposeData()
        {
            Scribe_Values.Look(ref QualificationDefName, "qualificationDefName");
            Scribe_Collections.Look(ref RequiredBookTopics, "requiredBookTopics", LookMode.Value);
            Scribe_Values.Look(ref RequiredResearchCount, "requiredResearchCount", 0);
            Scribe_Values.Look(ref BookScore, "bookScore", 0f);
            Scribe_Values.Look(ref ResearchScore, "researchScore", 0f);
            Scribe_Values.Look(ref SkillScore, "skillScore", 0f);
            Scribe_Values.Look(ref ActivityScore, "activityScore", 0f);
            Scribe_Values.Look(ref Passed, "passed", false);
            Scribe_Values.Look(ref Score, "score", 0f);
            if (RequiredBookTopics == null) RequiredBookTopics = new List<string>();
        }
    }

    /// <summary>P5 考试数据容器（挂 CareerData 下）。</summary>
    public sealed class ExamData : IExposable
    {
        public List<PracticalExamRecord> Practical = new List<PracticalExamRecord>();
        public List<TheoryExamRecord> Theory = new List<TheoryExamRecord>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref Practical, "practicalExams", LookMode.Deep);
            Scribe_Collections.Look(ref Theory, "theoryExams", LookMode.Deep);
            if (Practical == null) Practical = new List<PracticalExamRecord>();
            if (Theory == null) Theory = new List<TheoryExamRecord>();
        }
    }
}

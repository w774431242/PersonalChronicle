using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain.Qualification
{
    /// <summary>
    /// P5 资格状态机容器（V2.0 §14 + 阶段状态机模型）。
    /// 挂在 <see cref="PersonalChronicle.Domain.Career.CareerData"/> 下（D-Q1 决策），
    /// 与 Professional 同级；承载每个 QualificationDef 的进度派生，append-only。
    /// 仅状态派生（由 CareerEvent / ExamData / ThesisData 事实计算），绝不反向写 Events。
    /// </summary>
    public sealed class QualificationState : IExposable
    {
        /// <summary>按 QualificationDef.defName 索引的资格进度。</summary>
        public List<QualificationProgress> progress = new List<QualificationProgress>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref progress, "qualificationProgress", LookMode.Deep);
            if (progress == null) progress = new List<QualificationProgress>();
        }

        /// <summary>取指定资格的进度；无则 null（不自动创建，避免污染存档）。</summary>
        public QualificationProgress Get(string defName)
        {
            if (string.IsNullOrEmpty(defName) || progress == null)
            {
                return null;
            }
            for (int i = 0; i < progress.Count; i++)
            {
                if (string.Equals(progress[i].DefName, defName, System.StringComparison.Ordinal))
                {
                    return progress[i];
                }
            }
            return null;
        }

        /// <summary>取或创建指定资格的进度（写入方用）。</summary>
        public QualificationProgress GetOrAdd(string defName)
        {
            QualificationProgress existing = Get(defName);
            if (existing != null)
            {
                return existing;
            }
            if (progress == null)
            {
                progress = new List<QualificationProgress>();
            }
            QualificationProgress p = new QualificationProgress { DefName = defName };
            progress.Add(p);
            return p;
        }
    }

    /// <summary>
    /// 单条资格进度（P5 状态机：Locked→Eligible→Preparing→PracticalExam→
    /// TheoryExam→Thesis→Defense→Qualified→Granted，失败回 Preparing）。
    /// 持久化用 string Status（兼容存档，枚举名稳定）。
    /// </summary>
    public sealed class QualificationProgress : IExposable
    {
        /// <summary>关联 QualificationDef.defName。</summary>
        public string DefName;

        /// <summary>当前状态机阶段（QualificationStatus 枚举的 name）。</summary>
        public string Status = "Locked";

        public bool PracticalPassed;
        public bool TheoryPassed;
        public bool ThesisPassed;
        public bool DefensePassed;

        /// <summary>综合评分（0~100，资格判定门槛用）。</summary>
        public float CompositeScore;

        public long AppliedTick;
        public long DecidedTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref DefName, "defName");
            Scribe_Values.Look(ref Status, "status", "Locked");
            Scribe_Values.Look(ref PracticalPassed, "practicalPassed", false);
            Scribe_Values.Look(ref TheoryPassed, "theoryPassed", false);
            Scribe_Values.Look(ref ThesisPassed, "thesisPassed", false);
            Scribe_Values.Look(ref DefensePassed, "defensePassed", false);
            Scribe_Values.Look(ref CompositeScore, "compositeScore", 0f);
            Scribe_Values.Look(ref AppliedTick, "appliedTick", 0L);
            Scribe_Values.Look(ref DecidedTick, "decidedTick", 0L);
        }
    }

    /// <summary>P5 资格状态机阶段（string 持久化兼容）。</summary>
    public static class QualificationStatus
    {
        public const string Locked = "Locked";
        public const string Eligible = "Eligible";
        public const string Preparing = "Preparing";
        public const string PracticalExam = "PracticalExam";
        public const string TheoryExam = "TheoryExam";
        public const string Thesis = "Thesis";
        public const string Defense = "Defense";
        public const string Qualified = "Qualified";
        public const string Granted = "Granted";

        /// <summary>判定当前状态是否允许进入"已合格"前提（实践/理论/论文/答辩全过）。</summary>
        public static bool IsFullyPrepared(QualificationProgress p)
        {
            return p != null && p.PracticalPassed && p.TheoryPassed && p.ThesisPassed && p.DefensePassed;
        }
    }
}

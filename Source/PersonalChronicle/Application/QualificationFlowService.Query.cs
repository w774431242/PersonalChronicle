using System.Collections.Generic;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// [ARC-013] <see cref="QualificationFlowService"/> 的「只读查询 / 判定」切片。
    ///
    /// 原 `ArchiveService.Qualification.cs` 单文件 852 行、且一个文件内混装了
    /// 3 个类（ArchiveService partial / PracticalExamRecordExtensions / QualificationFlowService），
    /// 违反 [ARC-013] 单一职责与「单文件 ≤ 800 行」。按 08 §3「排序→识别→剥离」，
    /// 本文件剥离其中**叶子功能单元**（无下游依赖的纯查询与判定），主文件保留流程编排与写入。
    ///
    /// 剥离原则（[ARC-013] 零契约改动）：仅做物理搬移，类名、方法签名、访问级别、
    /// Save schema、翻译键一律未改；同 partial 类成员天然互相可见，跨文件调用无需改访问级别。
    /// </summary>
    public static partial class QualificationFlowService
    {
        // ── 资格子页只读查询（UI-001 治理：业务判定唯一归属 Application 层，UI 只调用不再重复实现） ──

        /// <summary>取专业技能当前等级（无技能/无数据 → 0）。</summary>
        public static int CalcSkillLevel(CareerData cd, string professionalSkillDefName)
        {
            if (cd == null || cd.Professional == null || string.IsNullOrEmpty(professionalSkillDefName))
            {
                return 0;
            }
            ProfessionalSkillData sd = cd.Professional.GetSkill(professionalSkillDefName);
            return sd != null ? sd.level : 0;
        }

        /// <summary>职业资历跨度（首末事件 tick 差；无事件 → 0）。</summary>
        public static long CalcCareerSpanTicks(CareerData cd)
        {
            if (cd == null || cd.Events == null || cd.Events.Count == 0) return 0L;
            long first = long.MaxValue, last = 0L;
            for (int i = 0; i < cd.Events.Count; i++)
            {
                if (cd.Events[i] == null) continue;
                if (cd.Events[i].Tick < first) first = cd.Events[i].Tick;
                if (cd.Events[i].Tick > last) last = cd.Events[i].Tick;
            }
            return first != long.MaxValue ? last - first : 0L;
        }

        /// <summary>某流程是否已通过（kind: practical / thesis / defense；按 QualificationDefName 精确匹配）。</summary>
        public static bool FlowPassed(CareerData cd, string qualDefName, string kind)
        {
            if (cd == null || string.IsNullOrEmpty(qualDefName)) return false;
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

        /// <summary>是否存在进行中的实践考试（未通过且未结束）。</summary>
        public static bool HasActiveExam(CareerData cd, string qualDefName)
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

        /// <summary>取当前档进行中/最新实践考试记录（用于展示进度）。</summary>
        public static PracticalExamRecord ActiveExam(CareerData cd, string qualDefName)
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

        /// <summary>取当前档最近一条理论考试记录（展示 4 项依据评分）。</summary>
        public static TheoryExamRecord FindTheoryRecord(PawnObject po, string qualDefName)
        {
            CareerData cd = po != null ? po.CareerData : null;
            if (cd == null || cd.Exams == null || cd.Exams.Theory == null) return null;
            TheoryExamRecord latest = null;
            for (int i = 0; i < cd.Exams.Theory.Count; i++)
            {
                TheoryExamRecord r = cd.Exams.Theory[i];
                if (r == null) continue;
                if (!string.Equals(r.QualificationDefName, qualDefName, System.StringComparison.Ordinal)) continue;
                if (latest == null) latest = r;
            }
            return latest;
        }

        /// <summary>取当前档最近一条论文记录。</summary>
        public static ThesisEvidence FindThesisRecord(PawnObject po, string qualDefName)
        {
            CareerData cd = po != null ? po.CareerData : null;
            if (cd == null || cd.Thesis == null || cd.Thesis.Theses == null) return null;
            ThesisEvidence latest = null;
            for (int i = 0; i < cd.Thesis.Theses.Count; i++)
            {
                ThesisEvidence r = cd.Thesis.Theses[i];
                if (r == null) continue;
                if (!string.Equals(r.QualificationDefName, qualDefName, System.StringComparison.Ordinal)) continue;
                if (latest == null) latest = r;
            }
            return latest;
        }

        /// <summary>取当前档最近一条答辩记录。</summary>
        public static DefenseRecord FindDefenseRecord(PawnObject po, string qualDefName)
        {
            CareerData cd = po != null ? po.CareerData : null;
            if (cd == null || cd.Thesis == null || cd.Thesis.Defenses == null) return null;
            DefenseRecord latest = null;
            for (int i = 0; i < cd.Thesis.Defenses.Count; i++)
            {
                DefenseRecord r = cd.Thesis.Defenses[i];
                if (r == null) continue;
                if (!string.Equals(r.QualificationDefName, qualDefName, System.StringComparison.Ordinal)) continue;
                if (latest == null) latest = r;
            }
            return latest;
        }

        /// <summary>已授予最高档索引（按 tierKeys 顺序匹配 TitleDefName 末尾；无匹配 → -1）。UI 展示索引推导，逻辑归属 Application。</summary>
        public static int GrantedTierIndex(CareerData cd, string[] tierKeys)
        {
            int idx = -1;
            if (cd == null || cd.GrantedTitles == null || tierKeys == null) return idx;
            for (int i = 0; i < cd.GrantedTitles.Count; i++)
            {
                if (cd.GrantedTitles[i] == null) continue;
                for (int t = 0; t < tierKeys.Length; t++)
                {
                    if (cd.GrantedTitles[i].TitleDefName != null
                        && cd.GrantedTitles[i].TitleDefName.EndsWith("_" + tierKeys[t], System.StringComparison.OrdinalIgnoreCase)
                        && t > idx)
                    {
                        idx = t;
                    }
                }
            }
            return idx;
        }
    }
}

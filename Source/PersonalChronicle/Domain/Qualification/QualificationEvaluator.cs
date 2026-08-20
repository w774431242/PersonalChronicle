using System.Collections.Generic;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Profession;
using Verse;

namespace PersonalChronicle.Domain.Qualification
{
    /// <summary>
    /// P5 资格评价引擎（V2.0 §14：职业等级不直接等于职称，必须过 QualificationEvaluator）。
    /// Domain 纯逻辑、零 Verse 依赖、零副作用；输入 ProfessionalState + CareerData +
    /// 已聚合成就，输出每条 QualificationDef 的 Eligible(Reason) 与 CompositeScore。
    /// 不写任何持久化字段（写入方是 QualificationService）。
    ///
    /// 评分权重（阶段一常量，后续可 Def 化）：
    ///   实践 25% / 理论 20% / 论文 20% / 答辩 15% / 专业等级 20%（各 0~100 折算）。
    /// </summary>
    public static class QualificationEvaluator
    {
        public const float WPractical = 0.25f;
        public const float WTheory = 0.20f;
        public const float WThesis = 0.20f;
        public const float WDefense = 0.15f;
        public const float WLevel = 0.20f;

        public sealed class Eligibility
        {
            public QualificationDef Def;
            public bool Eligible;
            public string Reason;
            public float CompositeScore;
        }

        /// <summary>运行期入口：从 DefDatabase 取 QualificationDef 列表，并解析技能 maxLevel 作为
        /// 职业等级分项的归一基准（无技能 Def 时 50 兜底）。</summary>
        public static List<Eligibility> Evaluate(PawnObject pawn)
        {
            Dictionary<string, int> skillMaxLevels = null;
            List<ProfessionalSkillDef> skillDefs = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading;
            if (skillDefs != null && skillDefs.Count > 0)
            {
                skillMaxLevels = new Dictionary<string, int>();
                for (int i = 0; i < skillDefs.Count; i++)
                {
                    ProfessionalSkillDef sd = skillDefs[i];
                    if (sd != null && !string.IsNullOrEmpty(sd.defName) && sd.maxLevel > 0)
                    {
                        skillMaxLevels[sd.defName] = sd.maxLevel;
                    }
                }
            }
            return Evaluate(pawn, DefDatabase<QualificationDef>.AllDefsListForReading, skillMaxLevels);
        }

        /// <summary>
        /// 可注入 defs 的入口（NUnit 纯逻辑测试用）。skillMaxLevels = 技能 defName → maxLevel 映射
        /// （2026-08-19 验收 P2-1 修复：职业等级分项按技能 maxLevel 归一，而非资格门槛等级）；
        /// 缺省时按 50 兜底，测试环境不依赖 DefDatabase。
        /// </summary>
        public static List<Eligibility> Evaluate(PawnObject pawn, IEnumerable<QualificationDef> defs,
            Dictionary<string, int> skillMaxLevels = null)
        {
            List<Eligibility> list = new List<Eligibility>();
            if (defs == null)
            {
                return list;
            }
            Dictionary<string, double> achievements = AchievementEvaluator.Aggregate(pawn);
            ProfessionalState ps = pawn != null && pawn.CareerData != null ? pawn.CareerData.Professional : null;
            CareerData cd = pawn != null ? pawn.CareerData : null;
            QualificationState qs = cd != null ? cd.Qualification : null;
            ExamData ex = cd != null ? cd.Exams : null;
            ThesisData td = cd != null ? cd.Thesis : null;

            foreach (QualificationDef def in defs)
            {
                if (def == null)
                {
                    continue;
                }
                int maxLevel = 50;
                if (skillMaxLevels != null && !string.IsNullOrEmpty(def.professionalSkillDefName)
                    && skillMaxLevels.TryGetValue(def.professionalSkillDefName, out int ml) && ml > 0)
                {
                    maxLevel = ml;
                }
                Eligibility e = EvaluateOne(def, ps, qs, ex, td, cd, achievements, maxLevel);
                list.Add(e);
            }
            return list;
        }

        private static Eligibility EvaluateOne(QualificationDef def, ProfessionalState ps, QualificationState qs,
            ExamData ex, ThesisData td, CareerData cd, Dictionary<string, double> achievements, int skillMaxLevel)
        {
            Eligibility e = new Eligibility { Def = def, Eligible = false, Reason = string.Empty, CompositeScore = 0f };

            // 1. 专业等级
            int level = 0;
            if (ps != null && !string.IsNullOrEmpty(def.professionalSkillDefName))
            {
                ProfessionalSkillData sd = ps.GetSkill(def.professionalSkillDefName);
                if (sd != null) level = sd.level;
            }
            if (level < def.requiredMinLevel)
            {
                e.Reason = "level";
                return e;
            }

            // 2. 职业时长（按 CareerEvent 首末跨度；无事件则 0）
            long span = 0L;
            if (cd != null && cd.Events != null && cd.Events.Count > 0)
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
            if (span < def.requiredCareerTimeTicks)
            {
                e.Reason = "careerTime";
                return e;
            }

            // 3. 事实门槛（EventType 计数）
            if (def.requiredEvents != null && cd != null)
            {
                for (int i = 0; i < def.requiredEvents.Count; i++)
                {
                    QualificationEventReq req = def.requiredEvents[i];
                    if (req == null || string.IsNullOrEmpty(req.eventType)) continue;
                    int count = CountEventType(cd, req.eventType);
                    if (count < req.minCount)
                    {
                        e.Reason = "event:" + req.eventType;
                        return e;
                    }
                }
            }

            // 4. 成就门槛（P8 产出键）
            if (def.requiredAchievements != null && achievements != null)
            {
                for (int i = 0; i < def.requiredAchievements.Count; i++)
                {
                    QualificationAchievementReq req = def.requiredAchievements[i];
                    if (req == null || string.IsNullOrEmpty(req.achievementKey)) continue;
                    double val = achievements.TryGetValue(req.achievementKey, out double v) ? v : 0.0;
                    if (val < req.minValue)
                    {
                        e.Reason = "achievement:" + req.achievementKey;
                        return e;
                    }
                }
            }

            // 5. 前置职称（职称/资格双键匹配，见 HasGrantedTitleKey 注释）
            if (!string.IsNullOrEmpty(def.requiredPreviousTitle) && cd != null)
            {
                if (!HasGrantedTitleKey(cd, def.requiredPreviousTitle))
                {
                    e.Reason = "previousTitle";
                    return e;
                }
            }

            // 6. 考试/论文/答辩通过标记
            bool practical = ExamPassed(ex, def.defName, true);
            bool theory = ExamPassed(ex, def.defName, false);
            bool thesisDone = ThesisPassed(td, def.defName);
            bool defenseDone = DefensePassed(td, def.defName);

            if (def.requiredExam && (!practical || !theory))
            {
                e.Reason = "exam";
                return e;
            }
            if (def.requiredThesis && !thesisDone)
            {
                e.Reason = "thesis";
                return e;
            }
            if (def.requiredDefense && !defenseDone)
            {
                e.Reason = "defense";
                return e;
            }

            // 综合评分
            float practicalScore = ScoreOf(practical, ex, def.defName, true);
            float theoryScore = ScoreOf(theory, ex, def.defName, false);
            float thesisScore = thesisDone ? ThesisScore(td, def.defName) : 0f;
            float defenseScore = defenseDone ? DefenseScore(td, def.defName) : 0f;
            float levelScore = skillMaxLevel > 0 ? (float)level / skillMaxLevel * 100f : 0f;

            float composite = WPractical * practicalScore
                            + WTheory * theoryScore
                            + WThesis * thesisScore
                            + WDefense * defenseScore
                            + WLevel * levelScore;
            e.CompositeScore = composite;

            if (composite < def.minimumScore)
            {
                e.Reason = "score";
                return e;
            }

            e.Eligible = true;
            e.Reason = "ok";
            return e;
        }

        // ---- 辅助读取 ----

        private static int CountEventType(CareerData cd, string eventType)
        {
            if (cd.Events == null) return 0;
            int c = 0;
            for (int i = 0; i < cd.Events.Count; i++)
            {
                if (cd.Events[i] != null && string.Equals(cd.Events[i].EventType, eventType, System.StringComparison.Ordinal))
                {
                    c++;
                }
            }
            return c;
        }

        private static bool ExamPassed(ExamData ex, string defName, bool practical)
        {
            if (ex == null) return false;
            if (practical && ex.Practical != null)
            {
                for (int i = 0; i < ex.Practical.Count; i++)
                {
                    if (ex.Practical[i] != null && string.Equals(ex.Practical[i].QualificationDefName, defName, System.StringComparison.Ordinal) && ex.Practical[i].Passed)
                    {
                        return true;
                    }
                }
            }
            if (!practical && ex.Theory != null)
            {
                for (int i = 0; i < ex.Theory.Count; i++)
                {
                    if (ex.Theory[i] != null && string.Equals(ex.Theory[i].QualificationDefName, defName, System.StringComparison.Ordinal) && ex.Theory[i].Passed)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static float ScoreOf(bool passed, ExamData ex, string defName, bool practical)
        {
            if (!passed) return 0f;
            if (ex == null) return 100f;
            if (practical && ex.Practical != null)
            {
                for (int i = 0; i < ex.Practical.Count; i++)
                {
                    if (ex.Practical[i] != null && string.Equals(ex.Practical[i].QualificationDefName, defName, System.StringComparison.Ordinal))
                    {
                        return ex.Practical[i].Score;
                    }
                }
            }
            if (!practical && ex.Theory != null)
            {
                for (int i = 0; i < ex.Theory.Count; i++)
                {
                    if (ex.Theory[i] != null && string.Equals(ex.Theory[i].QualificationDefName, defName, System.StringComparison.Ordinal))
                    {
                        return ex.Theory[i].Score;
                    }
                }
            }
            return 100f;
        }

        private static bool ThesisPassed(ThesisData td, string defName)
        {
            if (td == null || td.Theses == null) return false;
            for (int i = 0; i < td.Theses.Count; i++)
            {
                if (td.Theses[i] != null && string.Equals(td.Theses[i].QualificationDefName, defName, System.StringComparison.Ordinal) && td.Theses[i].Completed)
                {
                    return true;
                }
            }
            return false;
        }

        private static float ThesisScore(ThesisData td, string defName)
        {
            if (td == null || td.Theses == null) return 0f;
            for (int i = 0; i < td.Theses.Count; i++)
            {
                if (td.Theses[i] != null && string.Equals(td.Theses[i].QualificationDefName, defName, System.StringComparison.Ordinal))
                {
                    return td.Theses[i].ComputedScore;
                }
            }
            return 0f;
        }

        private static bool DefensePassed(ThesisData td, string defName)
        {
            if (td == null || td.Defenses == null) return false;
            for (int i = 0; i < td.Defenses.Count; i++)
            {
                DefenseRecord d = td.Defenses[i];
                if (d == null || !d.Passed) continue;
                // 2026-08-19 验收 P1-4 修复：优先按 QualificationDefName 精确匹配；
                // 旧记录字段为空时回退 ThesisId 匹配（兼容早期 DevTest 数据）。
                if (!string.IsNullOrEmpty(d.QualificationDefName))
                {
                    if (string.Equals(d.QualificationDefName, defName, System.StringComparison.Ordinal)) return true;
                }
                else if (string.Equals(d.ThesisId, defName, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static float DefenseScore(ThesisData td, string defName)
        {
            if (td == null || td.Defenses == null) return 0f;
            for (int i = 0; i < td.Defenses.Count; i++)
            {
                DefenseRecord d = td.Defenses[i];
                if (d == null) continue;
                if (!string.IsNullOrEmpty(d.QualificationDefName))
                {
                    if (string.Equals(d.QualificationDefName, defName, System.StringComparison.Ordinal))
                    {
                        return d.FinalScore;
                    }
                }
                else if (string.Equals(d.ThesisId, defName, System.StringComparison.Ordinal))
                {
                    return d.FinalScore;
                }
            }
            return 0f;
        }

        // ───────────────────────── 职称链共享判定（Read Model 与 UI 复用） ─────────────────────────

        /// <summary>
        /// 已授予职称双键匹配：key 既可能是职称 defName（Title_Precision_Junior），
        /// 也可能是资格 defName（Q_Precision_Junior）。Defs 的 <c>requiredPreviousTitle</c>
        /// 存的是资格 defName，而 GrantedTitles 记录职称 defName —— 必须双键比较，
        /// 否则前置职称链从第二档起永远判定不满足（2026-08-19 数据模拟工具暴露）。
        /// 本方法是全模组唯一的前置职称判定入口（QualificationEvaluator / Read Model 共用）。
        /// </summary>
        public static bool HasGrantedTitleKey(CareerData cd, string titleOrQualDefName)
        {
            if (cd == null || cd.GrantedTitles == null || string.IsNullOrEmpty(titleOrQualDefName))
            {
                return false;
            }
            for (int i = 0; i < cd.GrantedTitles.Count; i++)
            {
                GrantedTitle g = cd.GrantedTitles[i];
                if (g == null)
                {
                    continue;
                }
                if (string.Equals(g.TitleDefName, titleOrQualDefName, System.StringComparison.Ordinal)
                    || string.Equals(g.QualificationDefName, titleOrQualDefName, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 下一档资格选择（纯逻辑，可注入 defs 供离线单测）：
        /// 候选 = 未授予 + 前置职称已授予（双键；无前置 = 第一档）+ 档位严格高于当前已授最高档。
        /// 取 order 最小者；封顶（全部授予）返回 null。
        /// 守卫说明：不按全局 order 取「第一个未授予」——否则已授「高级技工」(order 2) 但低档
        /// 「初级技工」(order 0) 未授予时会把「下一职称」算成低级档，出现当前职称高于下一职称。
        /// 档位顺序以 QualificationDef.order 为准（与 ProfessionalTitleDef.order 同源同值）。
        /// </summary>
        public static QualificationDef NextQualification(CareerData cd, IEnumerable<QualificationDef> defs)
        {
            if (cd == null || defs == null)
            {
                return null;
            }
            int currentMaxOrder = -1;
            if (cd.GrantedTitles != null)
            {
                foreach (QualificationDef q in defs)
                {
                    if (q == null || string.IsNullOrEmpty(q.titleDefName)) continue;
                    if (HasGrantedTitleKey(cd, q.titleDefName) && q.order > currentMaxOrder)
                    {
                        currentMaxOrder = q.order;
                    }
                }
            }
            QualificationDef best = null;
            foreach (QualificationDef q in defs)
            {
                if (q == null || string.IsNullOrEmpty(q.titleDefName)) continue;
                // 1) 已授予 → 跳过。
                if (HasGrantedTitleKey(cd, q.titleDefName)) continue;
                // 2) 前置职称：有前置必须已授予（双键），否则跳过。
                if (!string.IsNullOrEmpty(q.requiredPreviousTitle)
                    && !HasGrantedTitleKey(cd, q.requiredPreviousTitle)) continue;
                // 3) 严格高于当前已授最高档：杜绝「当前职称高于下一职称」的倒挂显示。
                if (currentMaxOrder >= 0 && q.order <= currentMaxOrder) continue;
                if (best == null || q.order < best.order) best = q;
            }
            return best;
        }
    }
}

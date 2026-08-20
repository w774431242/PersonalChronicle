using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// P5~P8 职业资格与荣誉体系纯逻辑测试（可离线，无 DefDatabase / Verse 依赖）。
    /// 覆盖：ExamScoring 评分、QualificationEvaluator 资格判定、AchievementEvaluator
    /// 成就聚合、MedalAwardEvaluator 的 Achievement 类勋章路径（D-M1 扩展）。
    /// </summary>
    [TestFixture]
    public class CareerQualificationTests
    {
        private const double Epsilon = 1e-6;

        // ───────────────────────── P5 实践考试评分 ─────────────────────────

        [Test]
        public void ScorePractical_NoProduction_ReturnsZero()
        {
            float s = ExamScoring.ScorePractical(3, 0, new List<string>(), "Excellent", 0, 1000, 500);
            Assert.AreEqual(0f, s, Epsilon);
        }

        [Test]
        public void ScorePractical_FullCountAllExcellent_InTime_Returns100()
        {
            float s = ExamScoring.ScorePractical(
                3, 3,
                new List<string> { "Excellent", "Excellent", "Excellent" },
                "Excellent", 0, 1000, 500);
            Assert.AreEqual(100f, s, Epsilon);
        }

        [Test]
        public void ScorePractical_LateSubmission_Penalized()
        {
            float inTime = ExamScoring.ScorePractical(
                3, 3,
                new List<string> { "Excellent", "Excellent", "Excellent" },
                "Excellent", 0, 1000, 500);
            float late = ExamScoring.ScorePractical(
                3, 3,
                new List<string> { "Excellent", "Excellent", "Excellent" },
                "Excellent", 0, 1000, 2000);
            Assert.IsTrue(late < inTime, "late should be penalized");
            Assert.AreEqual(inTime * 0.6f, late, Epsilon);
        }

        [Test]
        public void ScorePractical_QualityShortfall_ReducesScore()
        {
            // 3 件仅 1 件达 Excellent → qd=1/3
            float s = ExamScoring.ScorePractical(
                3, 3,
                new List<string> { "Excellent", "Normal", "Poor" },
                "Excellent", 0, 1000, 500);
            // q=1, qd=1/3 → 100*1*(0.5+0.5/3) ≈ 66.67
            Assert.AreEqual(100f * (0.5f + 0.5f / 3f), s, 1e-3);
        }

        // ───────────────────────── P5 理论考试评分 ─────────────────────────

        [Test]
        public void ScoreTheory_WeightedSum()
        {
            float s = ExamScoring.ScoreTheory(100f, 100f, 100f, 100f);
            Assert.AreEqual(100f, s, Epsilon);
            float s2 = ExamScoring.ScoreTheory(0f, 0f, 0f, 0f);
            Assert.AreEqual(0f, s2, Epsilon);
        }

        // ───────────────────────── P5 资格判定 ─────────────────────────

        private static PawnObject MakePawnWithLevel(string skillDefName, int level, long spanTicks)
        {
            PawnObject p = new PawnObject();
            p.CareerData = new CareerData();
            p.CareerData.Professional = new ProfessionalState();
            p.CareerData.Professional.skills.Add(new ProfessionalSkillData
            {
                skillDefName = skillDefName,
                level = level,
                firstAcquiredTick = 0,
                lastPracticeTick = spanTicks
            });
            // 制造两件事实，跨度 = spanTicks
            p.CareerData.Events.Add(new CareerEvent("a:0:x", "a", 0L, CareerEventType.ItemProduced, "ThingA", null, "Good", null, 1, null));
            p.CareerData.Events.Add(new CareerEvent("a:" + spanTicks + ":y", "a", spanTicks, CareerEventType.ItemProduced, "ThingB", null, "Good", null, 1, null));
            return p;
        }

        private static QualificationDef MakeQual(string defName, string skill, int minLevel, long careerTime, bool reqExam)
        {
            return new QualificationDef
            {
                defName = defName,
                professionalSkillDefName = skill,
                titleDefName = "Title_X",
                requiredMinLevel = minLevel,
                requiredCareerTimeTicks = careerTime,
                requiredExam = reqExam,
                minimumScore = 0f,
                order = 0
            };
        }

        [Test]
        public void Qualification_LevelInsufficient_NotEligible()
        {
            PawnObject p = MakePawnWithLevel("SkillX", 5, 1000);
            QualificationDef def = MakeQual("Q1", "SkillX", 25, 0, false);
            var list = QualificationEvaluator.Evaluate(p, new List<QualificationDef> { def });
            Assert.IsFalse(list[0].Eligible);
            Assert.AreEqual("level", list[0].Reason);
        }

        [Test]
        public void Qualification_LevelAndTimeOk_NoExamRequired_Eligible()
        {
            PawnObject p = MakePawnWithLevel("SkillX", 30, 2000);
            QualificationDef def = MakeQual("Q1", "SkillX", 25, 1000, false);
            var list = QualificationEvaluator.Evaluate(p, new List<QualificationDef> { def });
            Assert.IsTrue(list[0].Eligible);
            Assert.AreEqual("ok", list[0].Reason);
        }

        [Test]
        public void Qualification_ExamRequired_ButNotPassed_NotEligible()
        {
            PawnObject p = MakePawnWithLevel("SkillX", 30, 2000);
            QualificationDef def = MakeQual("Q1", "SkillX", 25, 1000, true);
            var list = QualificationEvaluator.Evaluate(p, new List<QualificationDef> { def });
            Assert.IsFalse(list[0].Eligible);
            Assert.AreEqual("exam", list[0].Reason);
        }

        [Test]
        public void Qualification_ExamPassed_Eligible()
        {
            PawnObject p = MakePawnWithLevel("SkillX", 30, 2000);
            p.CareerData.Exams = new ExamData
            {
                Practical = new List<PracticalExamRecord> { new PracticalExamRecord { QualificationDefName = "Q1", Passed = true, Score = 90f } },
                Theory = new List<TheoryExamRecord> { new TheoryExamRecord { QualificationDefName = "Q1", Passed = true, Score = 90f } }
            };
            QualificationDef def = MakeQual("Q1", "SkillX", 25, 1000, true);
            var list = QualificationEvaluator.Evaluate(p, new List<QualificationDef> { def });
            Assert.IsTrue(list[0].Eligible);
        }

        // ───────────────────────── 2026-08-19 前置职称双键匹配（模拟工具暴露的 bug） ─────────────────────────

        [Test]
        public void Qualification_PreviousTitle_MatchByQualificationDefName()
        {
            // requiredPreviousTitle 存资格 defName（如 Q_Precision_Junior），GrantedTitles 记录
            // 职称 defName（如 Title_Precision_Junior）——按资格 defName 匹配须成立
            PawnObject p = MakePawnWithLevel("SkillX", 30, 2000);
            p.CareerData.GrantedTitles = new List<GrantedTitle>
            {
                new GrantedTitle("Title_Precision_Junior", "Q_Precision_Junior", 100L)
            };
            QualificationDef def = new QualificationDef
            {
                defName = "Q2",
                professionalSkillDefName = "SkillX",
                titleDefName = "Title_X",
                requiredMinLevel = 25,
                requiredCareerTimeTicks = 1000,
                requiredPreviousTitle = "Q_Precision_Junior", // 资格 defName
                minimumScore = 0f,
                order = 1
            };
            var list = QualificationEvaluator.Evaluate(p, new List<QualificationDef> { def });
            Assert.IsTrue(list[0].Eligible);
        }

        [Test]
        public void Qualification_PreviousTitle_Unmet_NotEligible()
        {
            PawnObject p = MakePawnWithLevel("SkillX", 30, 2000);
            p.CareerData.GrantedTitles = new List<GrantedTitle>
            {
                new GrantedTitle("Title_Other", "Q_Other", 100L)
            };
            QualificationDef def = new QualificationDef
            {
                defName = "Q2",
                professionalSkillDefName = "SkillX",
                titleDefName = "Title_X",
                requiredMinLevel = 25,
                requiredCareerTimeTicks = 1000,
                requiredPreviousTitle = "Q_Precision_Junior",
                minimumScore = 0f,
                order = 1
            };
            var list = QualificationEvaluator.Evaluate(p, new List<QualificationDef> { def });
            Assert.IsFalse(list[0].Eligible);
            Assert.AreEqual("previousTitle", list[0].Reason);
        }

        // ───────────────────────── v4.17 下一职称链（防倒挂 + 双键匹配） ─────────────────────────
        // 回归背景：主界面「职业档案」列表出现「当前职称(高级技工) > 下一职称(初级技工)」。
        // 根因：requiredPreviousTitle 存资格 defName，Read Model 曾只比职称 defName，
        // 前置链永远不满足 → 回落最低档。修复后 NextQualification 双键匹配 + 严格高于当前档。

        private static QualificationDef MakeLadderQual(string defName, string titleDefName,
            string requiredPreviousTitle, int order)
        {
            return new QualificationDef
            {
                defName = defName,
                professionalSkillDefName = "SkillX",
                titleDefName = titleDefName,
                requiredMinLevel = 5,
                requiredCareerTimeTicks = 0,
                requiredPreviousTitle = requiredPreviousTitle,
                minimumScore = 0f,
                order = order
            };
        }

        /// <summary>精密制造 5 档阶梯（对齐 Defs/QualificationDefs.xml）。</summary>
        private static List<QualificationDef> MakePrecisionLadder()
        {
            return new List<QualificationDef>
            {
                MakeLadderQual("Q_Precision_Junior", "Title_Precision_Junior", null, 0),
                MakeLadderQual("Q_Precision_Assistant", "Title_Precision_Assistant", "Q_Precision_Junior", 1),
                MakeLadderQual("Q_Precision_Senior", "Title_Precision_Senior", "Q_Precision_Assistant", 2),
                MakeLadderQual("Q_Precision_Specialist", "Title_Precision_Specialist", "Q_Precision_Senior", 3),
                MakeLadderQual("Q_Precision_Master", "Title_Precision_Master", "Q_Precision_Specialist", 4)
            };
        }

        private static CareerData MakeCareerWithTitles(params string[] titleDefNames)
        {
            PawnObject p = new PawnObject();
            p.CareerData = new CareerData();
            p.CareerData.GrantedTitles = new List<GrantedTitle>();
            long tick = 100L;
            foreach (string t in titleDefNames)
            {
                p.CareerData.GrantedTitles.Add(new GrantedTitle(t, t.Replace("Title_", "Q_"), tick));
                tick += 100L;
            }
            return p.CareerData;
        }

        [Test]
        public void HasGrantedTitleKey_MatchesByTitleDefName()
        {
            CareerData cd = MakeCareerWithTitles("Title_Precision_Junior");
            Assert.IsTrue(QualificationEvaluator.HasGrantedTitleKey(cd, "Title_Precision_Junior"));
            Assert.IsFalse(QualificationEvaluator.HasGrantedTitleKey(cd, "Title_Precision_Senior"));
        }

        [Test]
        public void HasGrantedTitleKey_MatchesByQualificationDefName()
        {
            // requiredPreviousTitle 存的是资格 defName —— 必须能命中职称记录
            CareerData cd = MakeCareerWithTitles("Title_Precision_Junior");
            Assert.IsTrue(QualificationEvaluator.HasGrantedTitleKey(cd, "Q_Precision_Junior"));
        }

        [Test]
        public void NextQualification_NoTitles_PicksFirstTier()
        {
            CareerData cd = MakeCareerWithTitles();
            QualificationDef next = QualificationEvaluator.NextQualification(cd, MakePrecisionLadder());
            Assert.IsNotNull(next);
            Assert.AreEqual("Q_Precision_Junior", next.defName);
        }

        [Test]
        public void NextQualification_HighTitleOnly_NeverBelowCurrent()
        {
            // 只授「高级技工」(order 2)：下一职称必须严格高于当前档 → 技师(Specialist, order 3)，
            // 绝不能回落到「初级技工」(order 0) —— 修复「当前职称高于下一职称」倒挂回归用例。
            CareerData cd = MakeCareerWithTitles("Title_Precision_Senior");
            QualificationDef next = QualificationEvaluator.NextQualification(cd, MakePrecisionLadder());
            Assert.IsNotNull(next);
            Assert.AreEqual("Q_Precision_Specialist", next.defName);
            Assert.IsTrue(next.order > 2, "next order must exceed current highest order");
        }

        [Test]
        public void NextQualification_MissingLowerTier_NotFirstTier()
        {
            // 授予「中级技工 + 高级技工」（低档缺失）：仍应取高级技工的下一档，
            // 而不是把缺失的「初级技工」当作下一职称。
            CareerData cd = MakeCareerWithTitles("Title_Precision_Assistant", "Title_Precision_Senior");
            QualificationDef next = QualificationEvaluator.NextQualification(cd, MakePrecisionLadder());
            Assert.IsNotNull(next);
            Assert.AreEqual("Q_Precision_Specialist", next.defName);
        }

        [Test]
        public void NextQualification_FullChain_CappedReturnsNull()
        {
            CareerData cd = MakeCareerWithTitles(
                "Title_Precision_Junior", "Title_Precision_Assistant", "Title_Precision_Senior",
                "Title_Precision_Specialist", "Title_Precision_Master");
            Assert.IsNull(QualificationEvaluator.NextQualification(cd, MakePrecisionLadder()));
        }

        [Test]
        public void NextQualification_PrerequisiteByQualificationKey_Satisfied()
        {
            // 前置职称按资格 defName 存储时，双键匹配必须放行第二档（回归：旧实现永远跳过第二档起）
            CareerData cd = MakeCareerWithTitles("Title_Precision_Junior");
            QualificationDef next = QualificationEvaluator.NextQualification(cd, MakePrecisionLadder());
            Assert.IsNotNull(next);
            Assert.AreEqual("Q_Precision_Assistant", next.defName);
        }

        // ───────────────────────── P8 成就聚合 ─────────────────────────

        [Test]
        public void Achievement_LegendaryMade_Counted()
        {
            PawnObject p = new PawnObject();
            p.CareerData = new CareerData();
            p.CareerData.Events.Add(new CareerEvent("a:1:x", "a", 1, CareerEventType.ItemProduced, "T", null, "Legendary", null, 1, null));
            p.CareerData.Events.Add(new CareerEvent("a:2:y", "a", 2, CareerEventType.ItemProduced, "T", null, "Normal", null, 1, null));
            var agg = AchievementEvaluator.Aggregate(p);
            Assert.AreEqual(1.0, agg[AchievementEvaluator.LegendaryMade], Epsilon);
            // 普通 ItemProduced 不带 major 标记 → MajorProjects=0
            Assert.AreEqual(0.0, agg[AchievementEvaluator.MajorProjects], Epsilon);
        }

        [Test]
        public void Achievement_TitleCount_FromTitleGrantedEvents()
        {
            // TitleCount 由 CareerEvent(TitleGranted) 事实计数派生（与 GrantedTitles 列表是
            // 不同来源：前者是长期事实 DB，后者是状态快照）。
            PawnObject p = new PawnObject();
            p.CareerData = new CareerData();
            p.CareerData.Events.Add(new CareerEvent("a:1:t", "a", 1, CareerEventType.TitleGranted, "Title_A", null, null, null, 1, null));
            p.CareerData.Events.Add(new CareerEvent("a:2:t", "a", 2, CareerEventType.TitleGranted, "Title_B", null, null, null, 1, null));
            var agg = AchievementEvaluator.Aggregate(p);
            Assert.AreEqual(2.0, agg[AchievementEvaluator.TitleCount], Epsilon);
        }

        // ───────────────────────── P8 MedalDef Achievement 路径 ─────────────────────────

        [Test]
        public void MedalEvaluator_AchievementMedal_MetWhenAggregateReachesThreshold()
        {
            PawnObject p = new PawnObject();
            p.CareerData = new CareerData();
            p.CareerData.Events.Add(new CareerEvent("a:1:x", "a", 1, CareerEventType.ItemProduced, "T", null, "Legendary", null, 1, null));
            p.GrantedMedals = new List<string>();

            MedalDef def = new MedalDef
            {
                defName = "Medal_Craft_Legend_Bronze",
                kind = MedalKind.Achievement,
                ownerType = MedalOwner.Pawn,
                achievementKey = AchievementEvaluator.LegendaryMade,
                achievementThreshold = 1f,
                order = 27
            };

            var result = MedalAwardEvaluator.EvaluatePawn(p, new List<MedalDef> { def });
            Assert.AreEqual(1, result.Items.Count);
            Assert.IsTrue(result.Items[0].IsMet);
            Assert.IsTrue(result.Items[0].IsNewAward);
        }

        [Test]
        public void MedalEvaluator_AchievementMedal_NotMetBelowThreshold()
        {
            PawnObject p = new PawnObject();
            p.CareerData = new CareerData();
            p.GrantedMedals = new List<string>();
            MedalDef def = new MedalDef
            {
                defName = "Medal_Craft_Legend_Gold",
                kind = MedalKind.Achievement,
                ownerType = MedalOwner.Pawn,
                achievementKey = AchievementEvaluator.LegendaryMade,
                achievementThreshold = 15f,
                order = 29
            };
            var result = MedalAwardEvaluator.EvaluatePawn(p, new List<MedalDef> { def });
            Assert.IsFalse(result.Items[0].IsMet);
            Assert.IsFalse(result.Items[0].IsNewAward);
        }

        [Test]
        public void MedalEvaluator_ThresholdPath_Unchanged()
        {
            // 确保扩展未破坏既有 Threshold 路径（P1~P4 已落地行为）
            PawnObject p = new PawnObject();
            p.CareerData = new CareerData();
            p.WorkTime = new WorkTimeAccumulator();
            p.WorkTime.TotalWorkTicks = 4800000L; // 达到 Bronze 阈值
            p.GrantedMedals = new List<string>();
            MedalDef def = new MedalDef
            {
                defName = "Medal_Labor_Model_Bronze",
                kind = MedalKind.Threshold,
                ownerType = MedalOwner.Pawn,
                metricKey = MedalMetricKeys.WorkTime,
                threshold = 4800000f,
                order = 0
            };
            var result = MedalAwardEvaluator.EvaluatePawn(p, new List<MedalDef> { def });
            Assert.IsTrue(result.Items[0].IsMet);
            Assert.IsTrue(result.Items[0].IsNewAward);
        }

        // ───────────────────────── 2026-08-19 验收 P1-1 回归：品质枚举 ─────────────────────────

        [Test]
        public void QualityRank_Masterwork_RanksAboveExcellent()
        {
            // RimWorld 1.6 QualityCategory：Excellent=4 Masterwork=5 Legendary=6；无 Superior 档
            Assert.AreEqual(4, ExamScoring.QualityRank("Excellent"));
            Assert.AreEqual(5, ExamScoring.QualityRank("Masterwork"));
            Assert.AreEqual(6, ExamScoring.QualityRank("Legendary"));
            Assert.AreEqual(-1, ExamScoring.QualityRank("Superior"));
            Assert.AreEqual(-1, ExamScoring.QualityRank(null));
        }

        [Test]
        public void CountAtLeast_CountsOnlyQualified()
        {
            var qs = new List<string> { "Excellent", "Normal", "Masterwork", "Poor" };
            Assert.AreEqual(2, ExamScoring.CountAtLeast(qs, "Excellent"));
            Assert.AreEqual(1, ExamScoring.CountAtLeast(qs, "Masterwork"));
            Assert.AreEqual(0, ExamScoring.CountAtLeast(qs, "Legendary"));
            Assert.AreEqual(0, ExamScoring.CountAtLeast(null, "Excellent"));
            Assert.AreEqual(0, ExamScoring.CountAtLeast(qs, null));
        }

        // ───────────────────────── 2026-08-19 验收 P1-2/P1-3 回归：实践考试写入 ─────────────────────────

        private static PawnObject MakePawnWithPracticalExam(string minQuality, long startedTick, long timeLimitTicks)
        {
            PawnObject p = new PawnObject();
            p.CareerData = new CareerData();
            p.CareerData.Exams = new ExamData();
            p.CareerData.Exams.Practical = new List<PracticalExamRecord>
            {
                new PracticalExamRecord
                {
                    ExamId = "exam1",
                    QualificationDefName = "Q1",
                    RequiredCount = 3,
                    MinQuality = minQuality,
                    StartedTick = startedTick,
                    TimeLimitTicks = timeLimitTicks
                }
            };
            return p;
        }

        [Test]
        public void RecordExamProduced_QualityBelowMinimum_NotPassedAndContinues()
        {
            PawnObject p = MakePawnWithPracticalExam("Excellent", 100L, 1000L);
            ArchiveService.RecordExamProduced(p, "Make_A", "Normal", 200L);
            ArchiveService.RecordExamProduced(p, "Make_A", "Normal", 300L);
            ArchiveService.RecordExamProduced(p, "Make_A", "Normal", 400L);
            PracticalExamRecord rec = p.CareerData.Exams.Practical[0];
            Assert.IsFalse(rec.Passed);
            Assert.IsFalse(rec.Finished, "数量已够但品质未达标：考试应继续等待更高品质产出");
            Assert.AreEqual(3, rec.ProducedCount);
        }

        [Test]
        public void RecordExamProduced_AllQualified_PassesAndFinishes()
        {
            PawnObject p = MakePawnWithPracticalExam("Excellent", 100L, 1000L);
            ArchiveService.RecordExamProduced(p, "Make_A", "Excellent", 200L);
            ArchiveService.RecordExamProduced(p, "Make_A", "Excellent", 300L);
            ArchiveService.RecordExamProduced(p, "Make_A", "Excellent", 400L);
            PracticalExamRecord rec = p.CareerData.Exams.Practical[0];
            Assert.IsTrue(rec.Passed);
            Assert.IsTrue(rec.Finished);
            Assert.AreEqual(100f, rec.Score, Epsilon);
        }

        [Test]
        public void RecordExamProduced_Overtime_EndsExamWithPenalty()
        {
            // 第 3 件在超时后产出：以当前证据评分（×0.6）并结束；品质全达标仍通过
            PawnObject p = MakePawnWithPracticalExam("Excellent", 100L, 1000L);
            ArchiveService.RecordExamProduced(p, "Make_A", "Excellent", 200L);
            ArchiveService.RecordExamProduced(p, "Make_A", "Excellent", 300L);
            ArchiveService.RecordExamProduced(p, "Make_A", "Excellent", 2000L);
            PracticalExamRecord rec = p.CareerData.Exams.Practical[0];
            Assert.IsTrue(rec.Finished, "超时后考试必须结束（不再卡死）");
            Assert.IsTrue(rec.Passed);
            Assert.AreEqual(60f, rec.Score, 0.01f); // 100 × 0.6（浮点链容差放宽到业务精度）
        }

        [Test]
        public void RecordExamProduced_Overtime_QualityShortfall_FailsAndEnds()
        {
            // 超时且品质不足：考试结束但不过（品质硬门槛仍生效）
            PawnObject p = MakePawnWithPracticalExam("Excellent", 100L, 1000L);
            ArchiveService.RecordExamProduced(p, "Make_A", "Normal", 200L);
            ArchiveService.RecordExamProduced(p, "Make_A", "Normal", 300L);
            ArchiveService.RecordExamProduced(p, "Make_A", "Normal", 2000L);
            PracticalExamRecord rec = p.CareerData.Exams.Practical[0];
            Assert.IsTrue(rec.Finished);
            Assert.IsFalse(rec.Passed);
            Assert.AreEqual(30f, rec.Score, 0.01f); // 100 × 0.5 × 0.6（浮点链容差放宽到业务精度）
        }

        [Test]
        public void RecordExamProduced_ReachMaxUnqualified_FailsAndEnds()
        {
            // 2026-08-19 流程修补：制造上限 = MaxProduced（0=2×RequiredCount=6），
            // 达到上限仍未达标 → 失败结束（考试件数有界，避免无限制造）
            PawnObject p = MakePawnWithPracticalExam("Excellent", 100L, 100000L);
            for (int i = 0; i < 6; i++)
            {
                ArchiveService.RecordExamProduced(p, "Make_A", "Normal", 200L + i * 1000L);
            }
            PracticalExamRecord rec = p.CareerData.Exams.Practical[0];
            Assert.IsTrue(rec.Finished, "达到制造上限应结束考试");
            Assert.IsFalse(rec.Passed, "品质硬门槛未满足不得通过");
            // 上限后再制造：考试已结束，不再累计
            ArchiveService.RecordExamProduced(p, "Make_A", "Excellent", 9000L);
            Assert.AreEqual(6, rec.ProducedCount);
        }

        [Test]
        public void RecordExamProduced_CustomMaxProduced_Respected()
        {
            // 显式 MaxProduced=4：第 4 件仍未达标 → 结束失败
            PawnObject p = MakePawnWithPracticalExam("Excellent", 100L, 100000L);
            p.CareerData.Exams.Practical[0].MaxProduced = 4;
            for (int i = 0; i < 4; i++)
            {
                ArchiveService.RecordExamProduced(p, "Make_A", "Normal", 200L + i * 1000L);
            }
            PracticalExamRecord rec = p.CareerData.Exams.Practical[0];
            Assert.IsTrue(rec.Finished);
            Assert.IsFalse(rec.Passed);
        }

        // ───────────────────────── 2026-08-19 评级评审期（结算以工作日答复，不自动即时授予） ─────────────────────────

        [Test]
        public void QualificationReview_NotStarted_NotDue()
        {
            // 未进入评审（ReviewStartedTick=0）→ 永不视为到期
            Assert.IsFalse(QualificationReview.IsDue(0L, 3, 999999L));
        }

        [Test]
        public void QualificationReview_NotDue_WithinDays()
        {
            // 开始后 3 个工作日内（60000 tick/日）未到期
            long start = 100000L;
            Assert.IsFalse(QualificationReview.IsDue(start, 3, start + 3L * 60000L - 1L));
        }

        [Test]
        public void QualificationReview_Due_AfterDays()
        {
            // 满 3 个工作日（含）即到期
            long start = 100000L;
            Assert.IsTrue(QualificationReview.IsDue(start, 3, start + 3L * 60000L));
            Assert.IsTrue(QualificationReview.IsDue(start, 3, start + 500000L));
        }

        [Test]
        public void QualificationReview_DefaultDays_WhenZero()
        {
            // reviewDays=0 → 缺省 3 个工作日
            long start = 100000L;
            Assert.IsFalse(QualificationReview.IsDue(start, 0, start + 3L * 60000L - 1L));
            Assert.IsTrue(QualificationReview.IsDue(start, 0, start + 3L * 60000L));
        }

        // ───────────────────────── 2026-08-19 验收 P1-4 回归：答辩匹配 ─────────────────────────

        private static QualificationDef MakeFullQual(string defName, string skill, bool reqThesis, bool reqDefense)
        {
            return new QualificationDef
            {
                defName = defName,
                professionalSkillDefName = skill,
                titleDefName = "Title_X",
                requiredMinLevel = 25,
                requiredCareerTimeTicks = 1000,
                requiredExam = true,
                requiredThesis = reqThesis,
                requiredDefense = reqDefense,
                minimumScore = 0f,
                order = 0
            };
        }

        private static void AttachFullPipeline(PawnObject p)
        {
            p.CareerData.Exams = new ExamData
            {
                Practical = new List<PracticalExamRecord> { new PracticalExamRecord { QualificationDefName = "Q1", Passed = true, Score = 90f } },
                Theory = new List<TheoryExamRecord> { new TheoryExamRecord { QualificationDefName = "Q1", Passed = true, Score = 90f } }
            };
            p.CareerData.Thesis = new ThesisData
            {
                Theses = new List<ThesisEvidence>
                {
                    new ThesisEvidence { ThesisId = "T1", QualificationDefName = "Q1", ComputedScore = 90f, Completed = true }
                },
                Defenses = new List<DefenseRecord>
                {
                    new DefenseRecord { ThesisId = "T1", QualificationDefName = "Q1", FinalScore = 90f, Passed = true }
                }
            };
        }

        [Test]
        public void Qualification_Defense_MatchByQualificationDefName()
        {
            PawnObject p = MakePawnWithLevel("SkillX", 30, 2000);
            AttachFullPipeline(p);
            QualificationDef def = MakeFullQual("Q1", "SkillX", true, true);
            var list = QualificationEvaluator.Evaluate(p, new List<QualificationDef> { def });
            Assert.IsTrue(list[0].Eligible);
        }

        [Test]
        public void Qualification_Defense_LegacyRecordFallsBackToThesisId()
        {
            // 旧 DevTest 数据兼容：DefenseRecord.QualificationDefName 为空时回退 ThesisId 匹配
            PawnObject p = MakePawnWithLevel("SkillX", 30, 2000);
            p.CareerData.Exams = new ExamData
            {
                Practical = new List<PracticalExamRecord> { new PracticalExamRecord { QualificationDefName = "Q1", Passed = true, Score = 90f } },
                Theory = new List<TheoryExamRecord> { new TheoryExamRecord { QualificationDefName = "Q1", Passed = true, Score = 90f } }
            };
            p.CareerData.Thesis = new ThesisData
            {
                Theses = new List<ThesisEvidence>
                {
                    new ThesisEvidence { ThesisId = "Q1", QualificationDefName = "Q1", ComputedScore = 90f, Completed = true }
                },
                Defenses = new List<DefenseRecord>
                {
                    new DefenseRecord { ThesisId = "Q1", FinalScore = 90f, Passed = true }
                }
            };
            QualificationDef def = MakeFullQual("Q1", "SkillX", true, true);
            var list = QualificationEvaluator.Evaluate(p, new List<QualificationDef> { def });
            Assert.IsTrue(list[0].Eligible);
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 勋章判定引擎纯逻辑测试（T5）。注入 defs 入口，测试环境无 DefDatabase。
    /// 覆盖：过滤规则 / 阈值判定 / 已授予去重 / 排序 / metric 读取 / SeriesKey 解析。
    /// </summary>
    [TestFixture]
    public class MedalAwardEvaluatorTests
    {
        private const double Epsilon = 1e-6;

        private static MedalDef ThresholdDef(string defName, MedalTier tier,
            string metricKey, float threshold, int order = 0)
        {
            return new MedalDef
            {
                defName = defName,
                kind = MedalKind.Threshold,
                ownerType = MedalOwner.Pawn,
                tier = tier,
                metricKey = metricKey,
                threshold = threshold,
                order = order,
            };
        }

        private static PawnObject NewPawn()
        {
            return new PawnObject();
        }

        // ------------------------------------------------------------------
        // 入口防护
        // ------------------------------------------------------------------

        [Test]
        public void Evaluate_NullPawn_ReturnsEmptyResult()
        {
            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(null,
                new List<MedalDef> { ThresholdDef("M.L.Work.Gold", MedalTier.Gold, MedalMetricKeys.WorkTime, 10f) });
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void Evaluate_NullDefs_ReturnsEmptyResult()
        {
            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(NewPawn(), null);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void Evaluate_EmptyDefs_ReturnsEmptyResult()
        {
            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(NewPawn(),
                new List<MedalDef>());
            Assert.AreEqual(0, result.Items.Count);
        }

        // ------------------------------------------------------------------
        // 过滤规则
        // ------------------------------------------------------------------

        [Test]
        public void Evaluate_SkipsNullDef()
        {
            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(NewPawn(),
                new List<MedalDef> { null });
            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void Evaluate_SkipsRankKind()
        {
            MedalDef rank = new MedalDef
            {
                defName = "M.Rank",
                kind = MedalKind.Rank,
                ownerType = MedalOwner.Pawn,
                metricKey = MedalMetricKeys.WorkTime,
                threshold = 0f,
            };
            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(NewPawn(),
                new List<MedalDef> { rank });
            Assert.AreEqual(0, result.Items.Count);
        }

        [Test]
        public void Evaluate_SkipsWorkplaceOwner()
        {
            MedalDef workplace = new MedalDef
            {
                defName = "M.Workplace",
                kind = MedalKind.Threshold,
                ownerType = MedalOwner.Workplace,
                metricKey = MedalMetricKeys.WorkTime,
                threshold = 0f,
            };
            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(NewPawn(),
                new List<MedalDef> { workplace });
            Assert.AreEqual(0, result.Items.Count);
        }

        // ------------------------------------------------------------------
        // 阈值判定 + 授予状态
        // ------------------------------------------------------------------

        [Test]
        public void Evaluate_MetricMet_NotGranted_MarksNewAward()
        {
            PawnObject pawn = NewPawn();
            pawn.WorkTime.TotalWorkTicks = 1000L; // 达标
            MedalDef def = ThresholdDef("M.Labor.Work.Gold", MedalTier.Gold, MedalMetricKeys.WorkTime, 500f);

            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(pawn,
                new List<MedalDef> { def });

            Assert.AreEqual(1, result.Items.Count);
            MedalEvaluation item = result.Items[0];
            Assert.IsTrue(item.IsApplicable);
            Assert.IsTrue(item.IsMet);
            Assert.IsFalse(item.IsGranted);
            Assert.IsTrue(item.IsNewAward);
            Assert.AreEqual(1000.0, item.CurrentValue, Epsilon);
            Assert.AreEqual(1, result.NewAwards.Count);
        }

        [Test]
        public void Evaluate_MetricBelowThreshold_NoNewAward()
        {
            PawnObject pawn = NewPawn();
            pawn.WorkTime.TotalWorkTicks = 100L; // 未达标
            MedalDef def = ThresholdDef("M.Labor.Work.Gold", MedalTier.Gold, MedalMetricKeys.WorkTime, 500f);

            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(pawn,
                new List<MedalDef> { def });

            Assert.AreEqual(1, result.Items.Count);
            Assert.IsFalse(result.Items[0].IsMet);
            Assert.IsFalse(result.Items[0].IsNewAward);
            Assert.AreEqual(0, result.NewAwards.Count);
        }

        [Test]
        public void Evaluate_MetricExactlyAtThreshold_MarksNewAward()
        {
            PawnObject pawn = NewPawn();
            pawn.Production.TotalQuantity = 50;
            MedalDef def = ThresholdDef("M.Prod.Count.Silver", MedalTier.Silver,
                MedalMetricKeys.ProductionQuantity, 50f);

            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(pawn,
                new List<MedalDef> { def });

            Assert.IsTrue(result.Items[0].IsMet);
            Assert.IsTrue(result.Items[0].IsNewAward);
        }

        [Test]
        public void Evaluate_AlreadyGranted_NotNewAward()
        {
            PawnObject pawn = NewPawn();
            pawn.WorkTime.TotalWorkTicks = 1000L;
            pawn.AddGrantedMedal("M.Labor.Work.Gold");
            MedalDef def = ThresholdDef("M.Labor.Work.Gold", MedalTier.Gold, MedalMetricKeys.WorkTime, 500f);

            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(pawn,
                new List<MedalDef> { def });

            Assert.AreEqual(1, result.Items.Count);
            Assert.IsTrue(result.Items[0].IsMet);
            Assert.IsTrue(result.Items[0].IsGranted);
            Assert.IsFalse(result.Items[0].IsNewAward);
            Assert.AreEqual(0, result.NewAwards.Count);
        }

        [Test]
        public void Evaluate_GrantedNullList_DoesNotThrow()
        {
            PawnObject pawn = NewPawn();
            pawn.WorkTime.TotalWorkTicks = 1000L;
            pawn.GrantedMedals = null;
            MedalDef def = ThresholdDef("M.Labor.Work.Gold", MedalTier.Gold, MedalMetricKeys.WorkTime, 500f);

            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(pawn,
                new List<MedalDef> { def });

            Assert.IsFalse(result.Items[0].IsGranted);
            Assert.IsTrue(result.Items[0].IsNewAward);
        }

        // ------------------------------------------------------------------
        // 排序
        // ------------------------------------------------------------------

        [Test]
        public void Evaluate_ResultsSortedByOrder()
        {
            PawnObject pawn = NewPawn();
            pawn.WorkTime.TotalWorkTicks = 1000L;
            MedalDef bronze = ThresholdDef("M.Labor.Work.Bronze", MedalTier.Bronze,
                MedalMetricKeys.WorkTime, 100f, order: 1);
            MedalDef gold = ThresholdDef("M.Labor.Work.Gold", MedalTier.Gold,
                MedalMetricKeys.WorkTime, 900f, order: 3);
            MedalDef silver = ThresholdDef("M.Labor.Work.Silver", MedalTier.Silver,
                MedalMetricKeys.WorkTime, 500f, order: 2);

            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(pawn,
                new List<MedalDef> { gold, bronze, silver });

            Assert.AreEqual("M.Labor.Work.Bronze", result.Items[0].Def.defName);
            Assert.AreEqual("M.Labor.Work.Silver", result.Items[1].Def.defName);
            Assert.AreEqual("M.Labor.Work.Gold", result.Items[2].Def.defName);
            // 三档均达标，Bronze/Silver 也在 NewAwards 中（本引擎不淘汰低档，归并由 ReadModel 做）
            Assert.AreEqual(3, result.NewAwards.Count);
        }

        // ------------------------------------------------------------------
        // metric 支持面
        // ------------------------------------------------------------------

        [Test]
        public void Evaluate_UnsupportedMetricKey_NotApplicable()
        {
            MedalDef def = ThresholdDef("M.Unsupported", MedalTier.Bronze, "heirloomHolders", 1f);
            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(NewPawn(),
                new List<MedalDef> { def });

            Assert.AreEqual(1, result.Items.Count);
            Assert.IsFalse(result.Items[0].IsApplicable);
            Assert.IsFalse(result.Items[0].IsMet);
            Assert.IsFalse(result.Items[0].IsNewAward);
        }

        [Test]
        public void TryReadPawnMetric_WorkTime_ReadsTotalTicks()
        {
            PawnObject pawn = NewPawn();
            pawn.WorkTime.TotalWorkTicks = 12345L;
            double value;
            Assert.IsTrue(MedalAwardEvaluator.TryReadPawnMetric(pawn, MedalMetricKeys.WorkTime, out value));
            Assert.AreEqual(12345.0, value, Epsilon);
        }

        [Test]
        public void TryReadPawnMetric_ProductionQuantity_ReadsTotal()
        {
            PawnObject pawn = NewPawn();
            pawn.Production.TotalQuantity = 77;
            double value;
            Assert.IsTrue(MedalAwardEvaluator.TryReadPawnMetric(pawn, MedalMetricKeys.ProductionQuantity, out value));
            Assert.AreEqual(77.0, value, Epsilon);
        }

        [Test]
        public void TryReadPawnMetric_ProductionSilver_ReadsTotalValue()
        {
            PawnObject pawn = NewPawn();
            pawn.Production.TotalMarketValue = 999.5f;
            double value;
            Assert.IsTrue(MedalAwardEvaluator.TryReadPawnMetric(pawn, MedalMetricKeys.ProductionSilver, out value));
            Assert.AreEqual(999.5, value, Epsilon);
        }

        [Test]
        public void TryReadPawnMetric_Kills_SumsMeleeAndRanged()
        {
            PawnObject pawn = NewPawn();
            pawn.MeleeKills = 3;
            pawn.RangedKills = 4;
            double value;
            Assert.IsTrue(MedalAwardEvaluator.TryReadPawnMetric(pawn, MedalMetricKeys.Kills, out value));
            Assert.AreEqual(7.0, value, Epsilon);
        }

        [Test]
        public void TryReadPawnMetric_DamageDealt_ReadsTotal()
        {
            PawnObject pawn = NewPawn();
            pawn.DamageDealtTotal = 543.25f;
            double value;
            Assert.IsTrue(MedalAwardEvaluator.TryReadPawnMetric(pawn, MedalMetricKeys.DamageDealt, out value));
            Assert.AreEqual(543.25, value, Epsilon);
        }

        [Test]
        public void TryReadPawnMetric_ParticipatedBattles_ReadsCount()
        {
            PawnObject pawn = NewPawn();
            pawn.ParticipatedBattles = 12;
            double value;
            Assert.IsTrue(MedalAwardEvaluator.TryReadPawnMetric(pawn, MedalMetricKeys.ParticipatedBattles, out value));
            Assert.AreEqual(12.0, value, Epsilon);
        }

        [Test]
        public void TryReadPawnMetric_ConsumptionSilver_ReadsTotal()
        {
            PawnObject pawn = NewPawn();
            pawn.Consumption.TotalSilver = 88.75f;
            double value;
            Assert.IsTrue(MedalAwardEvaluator.TryReadPawnMetric(pawn, MedalMetricKeys.ConsumptionSilver, out value));
            Assert.AreEqual(88.75, value, Epsilon);
        }

        [Test]
        public void TryReadPawnMetric_NullAccumulator_SafeZero()
        {
            PawnObject pawn = NewPawn();
            pawn.WorkTime = null;
            pawn.Production = null;
            pawn.Consumption = null;
            double value;
            Assert.IsTrue(MedalAwardEvaluator.TryReadPawnMetric(pawn, MedalMetricKeys.WorkTime, out value));
            Assert.AreEqual(0.0, value, Epsilon);
            Assert.IsTrue(MedalAwardEvaluator.TryReadPawnMetric(pawn, MedalMetricKeys.ProductionSilver, out value));
            Assert.AreEqual(0.0, value, Epsilon);
            Assert.IsTrue(MedalAwardEvaluator.TryReadPawnMetric(pawn, MedalMetricKeys.ConsumptionSilver, out value));
            Assert.AreEqual(0.0, value, Epsilon);
        }

        [Test]
        public void TryReadPawnMetric_NullArguments_ReturnsFalse()
        {
            double value;
            Assert.IsFalse(MedalAwardEvaluator.TryReadPawnMetric(null, MedalMetricKeys.WorkTime, out value));
            Assert.IsFalse(MedalAwardEvaluator.TryReadPawnMetric(NewPawn(), null, out value));
            Assert.IsFalse(MedalAwardEvaluator.TryReadPawnMetric(NewPawn(), string.Empty, out value));
        }

        [Test]
        public void TryReadPawnMetric_UnknownKey_ReturnsFalse()
        {
            double value;
            Assert.IsFalse(MedalAwardEvaluator.TryReadPawnMetric(NewPawn(), "no.such.key", out value));
            Assert.AreEqual(0.0, value, Epsilon);
        }

        // ------------------------------------------------------------------
        // SeriesKey 解析（ReadModel 归并 / 公告共用）
        // ------------------------------------------------------------------

        [Test]
        public void SeriesKeyOf_StripsTierSuffix()
        {
            Assert.AreEqual("Medal_Labor_Model",
                MedalDef.SeriesKeyOf("Medal_Labor_Model_Gold"));
            Assert.AreEqual("Medal_Labor_Model",
                MedalDef.SeriesKeyOf("Medal_Labor_Model_Silver"));
            Assert.AreEqual("Medal_Labor_Model",
                MedalDef.SeriesKeyOf("Medal_Labor_Model_Bronze"));
        }

        [Test]
        public void SeriesKeyOf_MultiPartDefName_KeepsPrefix()
        {
            Assert.AreEqual("Medal_Kills_Total",
                MedalDef.SeriesKeyOf("Medal_Kills_Total"));
        }

        [Test]
        public void SeriesKeyOf_NoDot_ReturnsSame()
        {
            Assert.AreEqual("Medal", MedalDef.SeriesKeyOf("Medal"));
        }

        [Test]
        public void SeriesKeyOf_NullOrEmpty_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, MedalDef.SeriesKeyOf(null));
            Assert.AreEqual(string.Empty, MedalDef.SeriesKeyOf(string.Empty));
        }

        // ------------------------------------------------------------------
        // AddGrantedMedal（append-only 去重）
        // ------------------------------------------------------------------

        [Test]
        public void AddGrantedMedal_AppendsAndDeduplicates()
        {
            PawnObject pawn = NewPawn();
            pawn.AddGrantedMedal("M.A");
            pawn.AddGrantedMedal("M.B");
            pawn.AddGrantedMedal("M.A");
            Assert.AreEqual(2, pawn.GrantedMedals.Count);
            Assert.AreEqual("M.A", pawn.GrantedMedals[0]);
            Assert.AreEqual("M.B", pawn.GrantedMedals[1]);
        }

        [Test]
        public void AddGrantedMedal_IgnoresNullAndEmpty()
        {
            PawnObject pawn = NewPawn();
            pawn.AddGrantedMedal(null);
            pawn.AddGrantedMedal(string.Empty);
            Assert.AreEqual(0, pawn.GrantedMedals.Count);
        }

        [Test]
        public void AddGrantedMedal_NullList_Initializes()
        {
            PawnObject pawn = NewPawn();
            pawn.GrantedMedals = null;
            pawn.AddGrantedMedal("M.A");
            Assert.IsNotNull(pawn.GrantedMedals);
            Assert.AreEqual(1, pawn.GrantedMedals.Count);
        }
    }
}

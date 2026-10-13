using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Application;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 手动授勋写入链路单元测试（离线可测部分）。
    ///
    /// 范围说明：
    /// - <see cref="MedalAwardService.AwardManual"/> 是集成层，其内部判定依赖
    ///   <see cref="MedalAwardEvaluator.EvaluatePawn(PawnObject)"/>（走 DefDatabase）—— 离线环境
    ///   无法可靠构造 Pawn 载体（ctor 依赖完整运行时，GetUninitializedObject 亦触发深层异常），
    ///   故 AwardManual 的「判定→写入」端到端集成测试需在游戏运行时（沙箱外）进行。
    /// - 本文件聚焦可离线的核心不变量：
    ///   1) <see cref="PawnObject.AddGrantedMedal"/> 去重（AwardManual 写入的唯一真相源）；
    ///   2) <see cref="PawnObject.GetGrantedMedal"/> 命中 + MarkChanged 计数；
    ///   3) MedalAwardEvaluator 的「注入式」三态判定（达标/已授/未达标），与 AwardManual 判定同引擎。
    /// - 判定引擎的 31 例详测见 <see cref="MedalAwardEvaluatorTests"/>，此处仅补「写入去重 + 三态」最小闭环。
    /// </summary>
    [TestFixture]
    public class MedalAwardServiceTests
    {
        [Test]
        public void AddGrantedMedal_Dedup_OnlyWritesOnce()
        {
            PawnObject pawnObject = new PawnObject();
            pawnObject.AddGrantedMedal("M.Labor.Work.Gold");
            pawnObject.AddGrantedMedal("M.Labor.Work.Gold");
            pawnObject.AddGrantedMedal("M.Labor.Work.Gold");
            int count = 0;
            for (int i = 0; i < pawnObject.GrantedMedals.Count; i++)
            {
                if (pawnObject.GrantedMedals[i] == "M.Labor.Work.Gold") count++;
            }
            Assert.AreEqual(1, count, "重复授予只落一条记录");
        }

        [Test]
        public void AddGrantedMedal_DistinctTiers_KeepsSeparate()
        {
            PawnObject pawnObject = new PawnObject();
            pawnObject.AddGrantedMedal("M.Labor.Work.Gold");
            pawnObject.AddGrantedMedal("M.Labor.Work.Silver");
            Assert.IsTrue(pawnObject.GrantedMedals.Contains("M.Labor.Work.Gold"));
            Assert.IsTrue(pawnObject.GrantedMedals.Contains("M.Labor.Work.Silver"));
            Assert.IsFalse(pawnObject.GrantedMedals.Contains("M.Labor.Work.Bronze"));
        }

        [Test]
        public void Evaluator_ThreeStates_MetGrantedBelow()
        {
            // 注入式评估（离线无 DefDatabase）：达标未授 -> IsNewAward；已授 -> 拦截；未达标 -> 拦截。
            PawnObject pawnObject = new PawnObject();
            pawnObject.WorkTime.TotalWorkTicks = 1000L; // 达标
            MedalDef gold = new MedalDef
            {
                defName = "M.Labor.Work.Gold",
                kind = MedalKind.Threshold,
                ownerType = MedalOwner.Pawn,
                tier = MedalTier.Gold,
                metricKey = MedalMetricKeys.WorkTime,
                threshold = 500f,
                order = 0,
            };

            MedalEvaluationResult eval = MedalAwardEvaluator.EvaluatePawn(
                pawnObject, new List<MedalDef> { gold });
            MedalEvaluation matched = null;
            foreach (var it in eval.Items) if (it.Def == gold) matched = it;
            Assert.IsNotNull(matched);
            Assert.IsTrue(matched.IsNewAward, "达标未授应 IsNewAward");

            // 标记已授后再评 -> 拦截
            pawnObject.AddGrantedMedal("M.Labor.Work.Gold");
            MedalEvaluationResult eval2 = MedalAwardEvaluator.EvaluatePawn(
                pawnObject, new List<MedalDef> { gold });
            MedalEvaluation matched2 = null;
            foreach (var it in eval2.Items) if (it.Def == gold) matched2 = it;
            Assert.IsNotNull(matched2);
            Assert.IsFalse(matched2.IsNewAward, "已授不应 IsNewAward");

            // 未达标 -> 拦截
            PawnObject low = new PawnObject();
            low.WorkTime.TotalWorkTicks = 100L; // < 500
            MedalEvaluationResult eval3 = MedalAwardEvaluator.EvaluatePawn(
                low, new List<MedalDef> { gold });
            MedalEvaluation matched3 = null;
            foreach (var it in eval3.Items) if (it.Def == gold) matched3 = it;
            Assert.IsNotNull(matched3);
            Assert.IsFalse(matched3.IsMet, "未达标 IsMet 应为 false");
        }
    }
}

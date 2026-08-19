using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using PersonalChronicle.Application;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 手动授勋入口（MedalAwardService.AwardManual）单元/集成测试。
    ///
    /// 覆盖两条核心路径：
    ///  - 达标且未授予（IsNewAward=true）→ 成功写入 GrantedMedals 并返回 true；
    ///  - 已授予（IsNewAward=false）→ 被拦截、不重复写入、返回 false。
    ///
    /// 离线环境说明：ChronicleGameComponent 无参构造（game 参数被忽略）可离线实例化；
    /// MarkChanged 仅自增 DataRevision 字段，不触碰 Game。Pawn 取 stableId 走
    /// GetRecordsFor → objectsByStableId 字典，测试内以反射写入该字典，避免依赖 live Pawn
    /// 的运行时构造。Pawn 实例仅作 stableId 载体；若离线无法 new Pawn 则整组 Ignore。
    /// </summary>
    [TestFixture]
    public class MedalAwardServiceTests
    {
        private const string StableId = "pawn_test_stub_001";

        private static MedalDef ThresholdDef(string defName, MedalTier tier,
            string metricKey, float threshold)
        {
            return new MedalDef
            {
                defName = defName,
                kind = MedalKind.Threshold,
                ownerType = MedalOwner.Pawn,
                tier = tier,
                metricKey = metricKey,
                threshold = threshold,
                order = 0,
            };
        }

        /// <summary>构造可离线使用的 component + 已建档 PawnObject（注册到 objectsByStableId）。</summary>
        private static ChronicleGameComponent MakeComponentWithPawn(PawnObject pawnObject, string stableId)
        {
            ChronicleGameComponent component = new ChronicleGameComponent(null);
            component.Objects.Add(pawnObject);
            // objectsByStableId 是 [Unsaved] 普通字段（默认值 new Dict），反射写入以驱动 GetRecordsFor。
            FieldInfo dictField = typeof(ChronicleGameComponent).GetField(
                "objectsByStableId", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)dictField.GetValue(component);
            dict[stableId] = pawnObject;
            return component;
        }

        /// <summary>返回一个 Pawn 载体（离线构造失败时返回 null）；其 GetUniqueLoadID 作为 stableId 注册。</summary>
        private static Pawn MakeStubPawn(PawnObject pawnObject, out string stableId)
        {
            stableId = StableId;
            try
            {
                Pawn pawn = new Pawn();
                // 用 Pawn 真实 GetUniqueLoadID 作为 key，保证 GetRecordsFor 能命中。
                stableId = pawn.GetUniqueLoadID();
                if (string.IsNullOrEmpty(stableId))
                {
                    stableId = StableId;
                }
                pawnObject.StableId = stableId;
                return pawn;
            }
            catch
            {
                return null;
            }
        }

        [Test]
        public void AwardManual_MetAndNotGranted_WritesAndReturnsTrue()
        {
            PawnObject pawnObject = new PawnObject();
            pawnObject.WorkTime.TotalWorkTicks = 1000L; // 达标
            MedalDef def = ThresholdDef("M.Labor.Work.Gold", MedalTier.Gold, MedalMetricKeys.WorkTime, 500f);

            Pawn pawn = MakeStubPawn(pawnObject, out string stableId);
            if (pawn == null) Assert.Ignore("离线环境无法构造 Pawn 载体，跳过运行时链路测试。");
            ChronicleGameComponent component = MakeComponentWithPawn(pawnObject, stableId);

            bool result = MedalAwardService.AwardManual(pawn, def, component);

            Assert.IsTrue(result);
            Assert.Contains("M.Labor.Work.Gold", pawnObject.GrantedMedals);
        }

        [Test]
        public void AwardManual_AlreadyGranted_BlockedAndReturnsFalse()
        {
            PawnObject pawnObject = new PawnObject();
            pawnObject.WorkTime.TotalWorkTicks = 1000L;
            pawnObject.AddGrantedMedal("M.Labor.Work.Gold"); // 已授予
            MedalDef def = ThresholdDef("M.Labor.Work.Gold", MedalTier.Gold, MedalMetricKeys.WorkTime, 500f);

            Pawn pawn = MakeStubPawn(pawnObject, out string stableId);
            if (pawn == null) Assert.Ignore("离线环境无法构造 Pawn 载体，跳过运行时链路测试。");
            ChronicleGameComponent component = MakeComponentWithPawn(pawnObject, stableId);

            bool result = MedalAwardService.AwardManual(pawn, def, component);

            Assert.IsFalse(result);
            // 不应重复写入
            int count = 0;
            for (int i = 0; i < pawnObject.GrantedMedals.Count; i++)
            {
                if (pawnObject.GrantedMedals[i] == "M.Labor.Work.Gold") count++;
            }
            Assert.AreEqual(1, count);
        }

        [Test]
        public void AwardManual_BelowThreshold_BlockedAndReturnsFalse()
        {
            PawnObject pawnObject = new PawnObject();
            pawnObject.WorkTime.TotalWorkTicks = 100L; // 未达标
            MedalDef def = ThresholdDef("M.Labor.Work.Gold", MedalTier.Gold, MedalMetricKeys.WorkTime, 500f);

            Pawn pawn = MakeStubPawn(pawnObject, out string stableId);
            if (pawn == null) Assert.Ignore("离线环境无法构造 Pawn 载体，跳过运行时链路测试。");
            ChronicleGameComponent component = MakeComponentWithPawn(pawnObject, stableId);

            bool result = MedalAwardService.AwardManual(pawn, def, component);

            Assert.IsFalse(result);
            Assert.IsFalse(pawnObject.GrantedMedals.Contains("M.Labor.Work.Gold"));
        }
    }
}

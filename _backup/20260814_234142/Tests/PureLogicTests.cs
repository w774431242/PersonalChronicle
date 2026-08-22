using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 第一次全面综合测试 —— 纯逻辑模块。
    /// 说明：SocialRelationFilter / ChronicleEventImportance 的运行时路径会触发
    /// Assembly-CSharp（引擎）程序集加载，无法在离线测试主机执行；这些用例以
    /// [Ignore] 标注，需在挂载 RimWorld 的运行时环境（游戏内测试/调试构建）执行。
    /// 本文件覆盖离线安全的纯构造与契约逻辑。
    /// </summary>
    [TestFixture]
    public class PureLogicTests
    {
        // ------------------------------------------------------------------
        // WorkIntensityPolicySnapshot / Undefined：纯构造与数学（离线安全）
        // ------------------------------------------------------------------

        [Test]
        public void WorkIntensity_Undefined_ReturnsNotDefined()
        {
            var input = new WorkIntensityInput(1000, 10, 10, 8);
            WorkIntensityEvaluation u = WorkIntensityEvaluation.Undefined(input, "PC");
            Assert.IsFalse(u.IsDefined);
        }

        [Test]
        public void WorkIntensity_Undefined_ComputesHoursFromInput()
        {
            // Undefined 仍计算 DailyHours = TotalWorkHours / ObservedDays
            var input = new WorkIntensityInput(20000, 80, 10, 8);
            WorkIntensityEvaluation u = WorkIntensityEvaluation.Undefined(input, "PC");
            Assert.AreEqual(8d, u.DailyHours, 1e-6);
            Assert.AreEqual(56d, u.WeeklyHours, 1e-6);
            Assert.AreEqual(240d, u.MonthlyHours, 1e-6);
            Assert.AreEqual(1.0d, u.RelativeRatio, 1e-6);
        }

        [Test]
        public void WorkIntensityPolicySnapshot_ConstructsWithValidConfig()
        {
            var tiers = new List<WorkIntensityTierSpec>
            {
                new WorkIntensityTierSpec("t0", "idle", "休", 0f, "k0", "k0", "#999", 0),
                new WorkIntensityTierSpec("t1", "normal", "常", 4f, "k1", "k1", "#9c9", 1),
            };
            var snap = new WorkIntensityPolicySnapshot(
                minimumSampleDays: 3,
                overloadRatio: 1.6f,
                slackRatio: 0.25f,
                tiers: tiers);
            Assert.AreEqual(3, snap.MinimumSampleDays);
            Assert.AreEqual(1.6f, snap.OverloadRatio, 1e-6f);
            Assert.AreEqual(0.25f, snap.SlackRatio, 1e-6f);
            Assert.AreEqual(2, snap.Tiers.Count);
        }

        [Test]
        public void WorkIntensityPolicySnapshot_NegativeRatiosClampedToZero()
        {
            var tiers = new List<WorkIntensityTierSpec>
            {
                new WorkIntensityTierSpec("t", "t", "t", 0f, "k", "k", "#fff", 0),
            };
            var snap = new WorkIntensityPolicySnapshot(-5, -2f, -3f, tiers);
            Assert.AreEqual(0, snap.MinimumSampleDays, "minimumSampleDays 应钳到 >=0");
            Assert.AreEqual(0f, snap.OverloadRatio, 1e-6f, "overloadRatio 应钳到 >=0");
            Assert.AreEqual(0f, snap.SlackRatio, 1e-6f, "slackRatio 应钳到 >=0");
        }

        [Test]
        public void WorkIntensityPolicySnapshot_NullTiersTreatedAsEmpty()
        {
            var snap = new WorkIntensityPolicySnapshot(3, 1.6f, 0.25f, null);
            Assert.IsNotNull(snap.Tiers);
            Assert.AreEqual(0, snap.Tiers.Count);
        }

        [Test]
        public void WorkIntensityPolicySnapshot_TierSpecExposesThreshold()
        {
            var tier = new WorkIntensityTierSpec("PC_WI_Normal", "normal", "常", 4f,
                "PersonalChronicle.WI.Normal", "WI.Normal", "#a3d977", 1);
            Assert.AreEqual("PC_WI_Normal", tier.DefName);
            Assert.AreEqual(4f, tier.MinimumDailyHours, 1e-6f);
            Assert.AreEqual("WI.Normal", tier.TagKey);
        }

        // ------------------------------------------------------------------
        // 以下用例需引擎程序集（已通过 CopyModAssembly 拷入测试输出目录）。
        // SocialRelationFilter 的 string 重载在 Policy 缺失时走 fallback 白名单；
        // ChronicleEventImportance.Resolve(string, dict) 的纯字符串映射路径不查 Def。
        // ------------------------------------------------------------------

        [Test]
        public void SocialRelationFilter_Fallback_WhitelistRelationsSignificant()
        {
            // fallback 白名单（与源码注释一致，精确 defName）：
            // Lover/Fiance/Spouse/ExLover/ExSpouse/Parent/Child/Sibling/HalfSibling
            // 注意：fallback 为精确匹配，不归一大小写/空白；调用方（PawnRelationDef 重载）
            // 传入的是规范 defName。
            string[] whitelist =
            {
                "Lover", "Fiance", "Spouse", "ExLover", "ExSpouse",
                "Parent", "Child", "Sibling", "HalfSibling",
            };
            foreach (string rel in whitelist)
            {
                Assert.IsTrue(SocialRelationFilter.IsSignificant(rel),
                    $"fallback 应判定 {rel} 为重要关系");
            }
        }

        [Test]
        public void SocialRelationFilter_Fallback_BondExcludedByDesign()
        {
            // 源码明确：Bond 不算关系（宠物羁绊），应返回 false
            Assert.IsFalse(SocialRelationFilter.IsSignificant("Bond"),
                "Bond（宠物羁绊）设计上不计入社会关系图");
        }

        [Test]
        public void SocialRelationFilter_Fallback_NonWhitelistNotSignificant()
        {
            string[] others =
            {
                "Neutral", "Acquaintance", "Colleague", "Prisoner",
                "Slave", "Guest", "Kin", "Cousin", "Nephew", "Grandchild", "",
                "SomeModdedRelation",
            };
            foreach (string rel in others)
            {
                Assert.IsFalse(SocialRelationFilter.IsSignificant(rel),
                    $"fallback 不应判定 {rel} 为重要关系");
            }
        }

        [Test]
        public void SocialRelationFilter_Fallback_ExactMatchNoNormalization()
        {
            // fallback 为精确 defName 匹配，不归一大小写/空白/裁剪。
            // 这是设计契约：调用方（PawnRelationDef 重载）传入规范 defName。
            Assert.IsTrue(SocialRelationFilter.IsSignificant("Spouse"), "精确规范 defName 应命中");
            Assert.IsFalse(SocialRelationFilter.IsSignificant("spouse"), "小写不命中（不归一）");
            Assert.IsFalse(SocialRelationFilter.IsSignificant("  spouse  "), "空白不裁剪");
            Assert.IsFalse(SocialRelationFilter.IsSignificant("FRIEND"), "非白名单（Friend/Rival 走合成路径）");
            Assert.IsFalse(SocialRelationFilter.IsSignificant("child"), "小写不命中");
            Assert.IsFalse(SocialRelationFilter.IsSignificant("  neutral  "), "空白不裁剪且不命中");
        }

        [Test]
        public void SocialRelationFilter_NullRelation_DefinedAndFalse()
        {
            string nullRel = null;
            Assert.DoesNotThrow(() => SocialRelationFilter.IsSignificant(nullRel));
            Assert.IsFalse(SocialRelationFilter.IsSignificant(nullRel));
        }

        [Test]
        public void ChronicleEventImportance_Death_ByKill_IsImportant()
        {
            var p = new Dictionary<string, string>
            {
                [ChronicleEventParams.CombatRole] = ChronicleEventParams.CombatRoleKill,
            };
            Assert.AreEqual(ChronicleImportance.Important,
                ChronicleEventImportance.Resolve(ChronicleEventType.Death, p));
        }

        [Test]
        public void ChronicleEventImportance_Death_NotKill_IsCritical()
        {
            var p = new Dictionary<string, string>(); // 无 combatRole
            Assert.AreEqual(ChronicleImportance.Critical,
                ChronicleEventImportance.Resolve(ChronicleEventType.Death, p));
        }

        [Test]
        public void ChronicleEventImportance_BattleAndJoin_AreImportant()
        {
            Assert.AreEqual(ChronicleImportance.Important,
                ChronicleEventImportance.Resolve(ChronicleEventType.Battle, null));
            Assert.AreEqual(ChronicleImportance.Important,
                ChronicleEventImportance.Resolve(ChronicleEventType.Join, null));
            Assert.AreEqual(ChronicleImportance.Important,
                ChronicleEventImportance.Resolve(ChronicleEventType.Social, null));
        }

        [Test]
        public void ChronicleEventImportance_CraftedAndBuilt_AreRoutine()
        {
            Assert.AreEqual(ChronicleImportance.Routine,
                ChronicleEventImportance.Resolve(ChronicleEventType.Crafted, null));
            Assert.AreEqual(ChronicleImportance.Routine,
                ChronicleEventImportance.Resolve(ChronicleEventType.Built, null));
        }

        [Test]
        public void ChronicleEventImportance_UnknownType_DefaultsNormal()
        {
            Assert.AreEqual(ChronicleImportance.Normal,
                ChronicleEventImportance.Resolve("SomeModdedEventType", null));
        }

        [Test]
        public void ChronicleEventImportance_NullEvent_ResolvesRoutine()
        {
            Assert.AreEqual(ChronicleImportance.Routine,
                ChronicleEventImportance.Resolve(null));
        }
    }
}

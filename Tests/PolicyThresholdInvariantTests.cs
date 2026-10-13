using NUnit.Framework;
using PersonalChronicle.Domain.PawnEconomy.Planning;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 审计修复（2026-10-09）：钉住 M3「双阈值」与 M5「储备水位阶梯」的**不变量**。
    ///
    /// 这些常量此前只有生产/展示消费、**零单测** —— 调错一个数不会有任何测试报警，
    /// 而它们的语义是"越往下越紧急"：顺序一旦颠倒，规划会先花大钱再谈保障，
    /// 而这种错误在实机上只表现为"钱花得怪"，很难归因。
    /// </summary>
    [TestFixture]
    public sealed class PolicyThresholdInvariantTests
    {
        [Test] public void EntryThresholdsSitAboveTheNativeSelfHelpLine()
        {
            // 进入规划的门槛必须**高于**原生自给线：先进入规划，再跌破自给线时才轮到免费手段接手。
            // 反过来会出现"还没开始规划就已经该自给"，需求驱动的规划永远不触发。
            Assert.That(PlanningPolicy.FoodEnter, Is.GreaterThan(PlanningPolicy.FoodNativeYield));
            Assert.That(PlanningPolicy.RestEnter, Is.GreaterThan(PlanningPolicy.RestNativeYield));
        }

        [Test] public void EveryEntryThresholdIsAProbability()
        {
            float[] enters = { PlanningPolicy.FoodEnter, PlanningPolicy.RestEnter, PlanningPolicy.JoyEnter };
            foreach (float value in enters)
            {
                Assert.That(value, Is.GreaterThan(0f), "进入阈值是比例，不能为 0（否则永远不进入规划）");
                Assert.That(value, Is.LessThanOrEqualTo(1f), "进入阈值是比例，不能超过 1（否则立刻进入规划）");
            }
        }

        [Test] public void ReserveLadderIsOrderedFromTightToLoose()
        {
            // 应急储备 < 下水位 < 全额：语义是"越往下越紧急"。顺序颠倒会让补储提前被小钱满足，
            // 真到应急时反而没有缓冲。
            Assert.That(PlanningPolicy.EmergencyReserveBasisPoints, Is.GreaterThan(0),
                "应急储备必须是非零的缓冲，否则这一档没有意义");
            Assert.That(PlanningPolicy.EmergencyReserveBasisPoints, Is.LessThan(PlanningPolicy.LowWaterBasisPoints),
                "应急储备必须低于下水位，否则下水位判定会被吞掉");
            Assert.That(PlanningPolicy.LowWaterBasisPoints, Is.LessThan(PlanningPolicy.BasisPoints),
                "下水位必须低于全额，否则永远处于补水状态");
        }

        [Test] public void NativeYieldLinesAreLowerThanEveryEntryThreshold()
        {
            // 自给线一律低于进入阈值：两条线不得交叉。
            Assert.That(PlanningPolicy.FoodNativeYield, Is.LessThan(PlanningPolicy.FoodEnter));
            Assert.That(PlanningPolicy.RestNativeYield, Is.LessThan(PlanningPolicy.RestEnter));
            Assert.That(PlanningPolicy.FoodNativeYield, Is.GreaterThan(0f));
            Assert.That(PlanningPolicy.RestNativeYield, Is.GreaterThan(0f));
        }
    }
}

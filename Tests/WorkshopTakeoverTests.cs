using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 「接管原版工作」安全闸门离线单测（CHG-20261003-TAKEOVER · Phase 2）。
    ///
    /// <para>钉死的三条<b>安全红线</b>（任一被绕过都会造成殖民地级死锁，故必须有钉子）：
    /// ① 搬运 / 清洁 / 医疗 / 灭火<b>永不接管</b>，即使岗位声称能交付；
    /// ② 车间不能独立交付的工作<b>不得接管</b>（否则"接管了但没人能做"）；
    /// ③ 接管后必须仍留有下限数量的自由工人，否则拒绝（否则殖民地无可用之人）。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopTakeoverTests
    {
        [Test]
        public void MandatoryKeep_NeverTakeover_EvenIfPostClaimsDelivery()
        {
            foreach (string w in WorkshopTakeover.MandatoryKeep)
            {
                Assert.IsTrue(WorkshopTakeover.IsMandatoryKeep(w), w + " 必须列入强制保留");
                // 闸门①优先于闸门②：即便 deliveredByPost = true 也必须拒绝。
                Assert.IsFalse(WorkshopTakeover.IsTakeoverAllowed(w, true),
                    w + " 属硬红线，岗位声称能交付也不得接管");
            }
        }

        [Test]
        public void MandatoryKeep_CoversHaulingCleaningDoctorFirefighter()
        {
            // 这四项正是"产线停摆 / filth 堆积 / 伤病无人治 / 火灾失控"的直接成因。
            CollectionAssert.IsSubsetOf(new[]
            {
                "Hauling", "Cleaning", "Doctor", "Firefighter"
            }, WorkshopTakeover.MandatoryKeep);
        }

        [Test]
        public void IsTakeoverAllowed_RequiresDeliveredByPost()
        {
            // 车间能独立交付（操作员承接 Crafting 且 deliversOwnService=true）⇒ 允许
            Assert.IsTrue(WorkshopTakeover.IsTakeoverAllowed("Crafting", true));
            // 车间不能独立交付（岗位 deliversOwnService=false）⇒ 拒绝
            Assert.IsFalse(WorkshopTakeover.IsTakeoverAllowed("Research", false),
                "不能独立交付的工作不得接管，否则接管后无人能做");
        }

        [Test]
        public void IsTakeoverAllowed_RejectsEmptyName()
        {
            Assert.IsFalse(WorkshopTakeover.IsTakeoverAllowed(null, true));
            Assert.IsFalse(WorkshopTakeover.IsTakeoverAllowed(string.Empty, true));
        }

        [Test]
        public void FreeWorkerFloor_RejectsWhenEveryoneIsInWorkshop()
        {
            // 3 人全部排进车间 ⇒ 接管后自由人 0 < 下限 1 ⇒ 必须拒绝
            Assert.AreEqual(0, WorkshopTakeover.FreeWorkersAfter(3, 3, 1));
            Assert.IsFalse(WorkshopTakeover.PassesFreeWorkerFloor(3, 3, 1),
                "全员进车间时不得接管（殖民地会无可用之人）");
        }

        [Test]
        public void FreeWorkerFloor_PassesWhenOneWorkerRemains()
        {
            Assert.AreEqual(1, WorkshopTakeover.FreeWorkersAfter(3, 2, 1));
            Assert.IsTrue(WorkshopTakeover.PassesFreeWorkerFloor(3, 2, 1));
        }

        [Test]
        public void FreeWorkersAfter_NeverNegative_EvenIfCrewExceedsTotal()
        {
            // 防御性：班组人数统计异常（重复计数 / 跨图）时不得返回负数
            Assert.AreEqual(0, WorkshopTakeover.FreeWorkersAfter(2, 5, 1));
            Assert.IsFalse(WorkshopTakeover.PassesFreeWorkerFloor(2, 5, 1));
        }

        [Test]
        public void ShouldRestore_OnlyWhenCurrentIsZeroAndOriginalInRange()
        {
            // 当前仍为 0（还是 PC 写的禁用）+ 原值合法 ⇒ 还原
            Assert.IsTrue(WorkshopTakeover.ShouldRestore(0, 1));
            Assert.IsTrue(WorkshopTakeover.ShouldRestore(0, 3));
            Assert.IsTrue(WorkshopTakeover.ShouldRestore(0, 4));
        }

        [Test]
        public void ShouldRestore_RefusesWhenPlayerChangedIt()
        {
            // 玩家已手动重新勾选并给了档位 => PC 不再拥有这一项，绝不覆盖玩家的新值
            Assert.IsFalse(WorkshopTakeover.ShouldRestore(1, 3));
            Assert.IsFalse(WorkshopTakeover.ShouldRestore(2, 3));
            Assert.IsFalse(WorkshopTakeover.ShouldRestore(4, 3));
        }

        [Test]
        public void ShouldRestore_RefusesOutOfRangeOriginal()
        {
            // 原值越界（旧档 / 手工改档）⇒ 丢弃，绝不硬写
            Assert.IsFalse(WorkshopTakeover.ShouldRestore(0, 0));
            Assert.IsFalse(WorkshopTakeover.ShouldRestore(0, -1));
            Assert.IsFalse(WorkshopTakeover.ShouldRestore(0, 5));
            Assert.IsFalse(WorkshopTakeover.ShouldRestore(0, int.MaxValue));
        }
        [Test]
        public void FreeWorkerFloor_IsDiagnosticOnly_HardRedLinesMakeDeadlockImpossible()
        {
            // 回归钉子（2026-10-03 实机 100% 误拒）：3 人全部进车间时，自由人 = 0。
            // 旧实现据此**整次拒绝接管** ⇒ 小殖民地"全员进车间"这一**目标用法**完全失效
            // （实机：原版工作面板毫无变化）。
            //
            // 现在它只是**提示**：因为 MandatoryKeep 四项（搬运/清洁/医疗/灭火）永不接管，
            // 任何殖民者永远还剩这 4 项工作 ⇒ 死锁在数学上不可能发生 ⇒ 用它拦截没有意义。
            Assert.IsFalse(WorkshopTakeover.PassesFreeWorkerFloor(3, 3, 1),
                "3 人全进车间时自由人确实为 0（该读数本身没错）");
            // 关键：硬红线必须覆盖这 4 项，否则"自由人为 0"才是真风险。
            foreach (string w in new[] { "Hauling", "Cleaning", "Doctor", "Firefighter" })
            {
                Assert.IsTrue(WorkshopTakeover.IsMandatoryKeep(w),
                    w + " 属硬红线 ⇒ 永不被接管 ⇒ 自由人为 0 也不构成死锁");
            }
        }    }
}
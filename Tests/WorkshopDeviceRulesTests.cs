using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 设备识别规则（<see cref="WorkshopDeviceRules"/>）离线单测。
    ///
    /// <para>钉死的契约：① 机台 = **建筑 ∧ IBillGiver**；② <b>pawn 不是设备</b>
    /// （原版 `Verse.Pawn : IBillGiver`，曾是实机缺陷：设备页列出"人类"）；
    /// ③ 显式声明 `IWorkshopDevice` 的设备即使不是建筑也算（传送带/农机/第三方）；
    /// ④ 普通建筑、非建筑、蓝图框架都不算。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopDeviceRulesTests
    {
        [Test]
        public void IsBillGiverBench_RequiresBuilding()
        {
            Assert.IsTrue(WorkshopDeviceRules.IsBillGiverBench(true, true));
            Assert.IsFalse(WorkshopDeviceRules.IsBillGiverBench(false, true));
            Assert.IsFalse(WorkshopDeviceRules.IsBillGiverBench(true, false));
            Assert.IsFalse(WorkshopDeviceRules.IsBillGiverBench(false, false));
        }

        [Test]
        public void PawnLikeThing_IsNotADevice()
        {
            // isBuilding = false, isBillGiver = true —— 这正是 Verse.Pawn 的形状。
            Assert.IsFalse(WorkshopDeviceRules.IsWorkshopDevice(false, false, true));
            Assert.IsFalse(WorkshopDeviceRules.IsWorkshopDevice(false, false, false));
        }

        [Test]
        public void DeclaredInterface_CountsEvenIfNotBuilding()
        {
            Assert.IsTrue(WorkshopDeviceRules.IsWorkshopDevice(true, false, false));
            Assert.IsTrue(WorkshopDeviceRules.IsWorkshopDevice(true, true, false));
            Assert.IsTrue(WorkshopDeviceRules.IsWorkshopDevice(true, false, true));
        }

        [Test]
        public void BuildingWorktable_Counts()
        {
            // 原版 Building_WorkTable：建筑 + IBillGiver，但未声明我们的接口。
            Assert.IsTrue(WorkshopDeviceRules.IsWorkshopDevice(false, true, true));
        }

        [Test]
        public void PlainBuilding_DoesNotCount()
        {
            // 普通建筑（墙/床/地板）既非机台也未声明接口。
            Assert.IsFalse(WorkshopDeviceRules.IsWorkshopDevice(false, true, false));
        }
    }
}

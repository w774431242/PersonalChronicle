using NUnit.Framework;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 设备状态派生的离线单测（车间俯视 ② 的灯色语义）。
    ///
    /// <para>判定顺序是契约：故障 &gt; 断电 &gt; 有活/没活。顺序错了会表现为
    /// 「断电的机床亮绿」这类误导性读数，属于必须钉死的语义。</para>
    /// </summary>
    [TestFixture]
    public sealed class EquipmentStateResolverTests
    {
        [Test]
        public void Resolve_BrokenBeatsEverything()
        {
            Assert.AreEqual(EquipmentState.Fault,
                EquipmentStateResolver.Resolve(brokenDown: true, powerOn: true, hasExecutableBill: true));
            Assert.AreEqual(EquipmentState.Fault,
                EquipmentStateResolver.Resolve(brokenDown: true, powerOn: false, hasExecutableBill: false));
        }

        [Test]
        public void Resolve_NoPowerIsOff_EvenWithBill()
        {
            Assert.AreEqual(EquipmentState.Off,
                EquipmentStateResolver.Resolve(brokenDown: false, powerOn: false, hasExecutableBill: true));
        }

        [Test]
        public void Resolve_RunningVsIdle()
        {
            Assert.AreEqual(EquipmentState.Run,
                EquipmentStateResolver.Resolve(brokenDown: false, powerOn: true, hasExecutableBill: true));
            Assert.AreEqual(EquipmentState.Idle,
                EquipmentStateResolver.Resolve(brokenDown: false, powerOn: true, hasExecutableBill: false));
        }

        [Test]
        public void KeyOf_IsStableAndLanguageNeutral()
        {
            Assert.AreEqual("run", EquipmentStateResolver.KeyOf(EquipmentState.Run));
            Assert.AreEqual("idle", EquipmentStateResolver.KeyOf(EquipmentState.Idle));
            Assert.AreEqual("fault", EquipmentStateResolver.KeyOf(EquipmentState.Fault));
            Assert.AreEqual("off", EquipmentStateResolver.KeyOf(EquipmentState.Off));
        }

        [Test]
        public void TranslationKeyOf_MapsEveryStateKey()
        {
            Assert.AreEqual("PersonalChronicle.UI.Floor.State.Run",
                EquipmentStateText.TranslationKeyOf("run"));
            Assert.AreEqual("PersonalChronicle.UI.Floor.State.Fault",
                EquipmentStateText.TranslationKeyOf("fault"));
            // 未知键（旧档/第三方）不得抛异常，回落「停机」文案键。
            Assert.AreEqual("PersonalChronicle.UI.Floor.State.Off",
                EquipmentStateText.TranslationKeyOf("something-else"));
        }
    }
}

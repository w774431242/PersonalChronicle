using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 车间「设备匹配度」判定（匹配 / 多余 / 缺失）的离线单测。
    ///
    /// <para>钉死的契约：① 未声明期望设备是<b>合法状态</b>（全部计"多余"、缺失为空、
    /// HasExpected=false），UI 据此显示"未声明"而不是误导性的"缺失"；
    /// ② 缺失列表永不 null；③ 同名设备多台只算匹配多台、不会重复计入缺失；
    /// ④ 期望清单里写了两次同一 defName 时缺失判定不重复。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopDeviceFitEvaluatorTests
    {
        [Test]
        public void Evaluate_NoExpected_AllExtraAndNoMissing()
        {
            WorkshopDeviceFit fit = WorkshopDeviceFitEvaluator.Evaluate(
                new List<string>(), new List<string> { "ElectricSmithy", "TableMachining" });

            Assert.IsFalse(fit.HasExpected);
            Assert.AreEqual(0, fit.Matched);
            Assert.AreEqual(2, fit.Extra);
            Assert.AreEqual(0, fit.MissingDefNames.Count);
        }

        [Test]
        public void Evaluate_NullExpected_TreatedAsUnspecified()
        {
            WorkshopDeviceFit fit = WorkshopDeviceFitEvaluator.Evaluate(null, new List<string> { "A" });

            Assert.IsFalse(fit.HasExpected);
            Assert.AreEqual(1, fit.Extra);
            Assert.IsNotNull(fit.MissingDefNames);
        }

        [Test]
        public void Evaluate_NullActual_AllExpectedMissing()
        {
            WorkshopDeviceFit fit = WorkshopDeviceFitEvaluator.Evaluate(
                new List<string> { "A", "B" }, null);

            Assert.IsTrue(fit.HasExpected);
            Assert.AreEqual(0, fit.Matched);
            Assert.AreEqual(0, fit.Extra);
            CollectionAssert.AreEqual(new[] { "A", "B" }, fit.MissingDefNames);
        }

        [Test]
        public void Evaluate_MatchExtraAndMissing()
        {
            WorkshopDeviceFit fit = WorkshopDeviceFitEvaluator.Evaluate(
                new List<string> { "A", "B" },
                new List<string> { "A", "C" });

            Assert.AreEqual(1, fit.Matched);
            Assert.AreEqual(1, fit.Extra);
            CollectionAssert.AreEqual(new[] { "B" }, fit.MissingDefNames);
        }

        [Test]
        public void Evaluate_DuplicateActual_MatchedPerInstanceAndMissingNotDuplicated()
        {
            WorkshopDeviceFit fit = WorkshopDeviceFitEvaluator.Evaluate(
                new List<string> { "A" },
                new List<string> { "A", "A" });

            Assert.AreEqual(2, fit.Matched);
            Assert.AreEqual(0, fit.Extra);
            Assert.AreEqual(0, fit.MissingDefNames.Count);
        }

        [Test]
        public void Evaluate_NullDefNameEntry_CountsAsExtra()
        {
            WorkshopDeviceFit fit = WorkshopDeviceFitEvaluator.Evaluate(
                new List<string> { "A" },
                new List<string> { null, "A" });

            Assert.AreEqual(1, fit.Matched);
            Assert.AreEqual(1, fit.Extra);
            Assert.AreEqual(0, fit.MissingDefNames.Count);
        }
    }
}

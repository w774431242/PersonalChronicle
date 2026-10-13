using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 设备匹配度「缺失项去重」回归（2026-09-11 体检发现：期望清单重复写同一 defName 时
    /// 会显示"缺失 2：[台, 台]"）。
    /// </summary>
    [TestFixture]
    public sealed class WorkshopDeviceFitDedupTests
    {
        [Test]
        public void DuplicateExpectedDefNames_ProduceSingleMissingEntry()
        {
            var expected = new List<string> { "TableMachining", "TableMachining" };
            var actual = new List<string>();
            WorkshopDeviceFit fit = WorkshopDeviceFitEvaluator.Evaluate(expected, actual);
            Assert.IsTrue(fit.HasExpected);
            Assert.AreEqual(1, fit.MissingDefNames.Count);
            Assert.AreEqual("TableMachining", fit.MissingDefNames[0]);
        }

        [Test]
        public void DuplicateExpectedDefNames_WithOnePresent_ReportNoMissing()
        {
            var expected = new List<string> { "TableMachining", "TableMachining" };
            var actual = new List<string> { "TableMachining" };
            WorkshopDeviceFit fit = WorkshopDeviceFitEvaluator.Evaluate(expected, actual);
            Assert.AreEqual(0, fit.MissingDefNames.Count);
            Assert.AreEqual(1, fit.Matched);
            Assert.AreEqual(0, fit.Extra);
        }
    }
}

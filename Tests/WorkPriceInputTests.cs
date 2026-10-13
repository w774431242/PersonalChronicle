using System.Globalization;
using System.Threading;
using NUnit.Framework;
using PersonalChronicle.Economy.PawnEconomy;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class WorkPriceInputTests
    {
        [TestCase("0", 0L)]
        [TestCase("0.00", 0L)]
        [TestCase("0.01", 1L)]
        [TestCase("1.2", 120L)]
        [TestCase(" 12.34 ", 1234L)]
        [TestCase("1000.00", 100000L)]
        public void DecimalSilverInputMapsExactlyToMinorUnits(string text, long expected)
        {
            Assert.That(WorkPriceInput.TryParse(text, out long minor), Is.True);
            Assert.That(minor, Is.EqualTo(expected), "不能把银单位直接当分，也不能舍入小数");
            Assert.That(WorkPriceInput.TryParse(WorkPriceInput.Format(minor), out long roundtrip), Is.True);
            Assert.That(roundtrip, Is.EqualTo(expected));
        }

        [TestCase(null)] [TestCase("")] [TestCase(" ")]
        [TestCase("-1")] [TestCase("+1")] [TestCase("1,20")] [TestCase("1,000.00")]
        [TestCase("1e2")] [TestCase("NaN")] [TestCase("Infinity")]
        [TestCase("1.234")] [TestCase("1.")] [TestCase(".50")]
        [TestCase("1000.01")] [TestCase("79228162514264337593543950335")]
        [TestCase("999999999999999999999999999999999999999999999999")]
        public void AmbiguousOrUnrepresentableAmountIsRejectedWithoutOverflowOrRounding(string text)
        {
            Assert.That(WorkPriceInput.TryParse(text, out long minor), Is.False);
            Assert.That(minor, Is.Zero, "失败不得泄露可误提交的部分金额");
        }

        [Test] public void MachineLocaleCannotChangeDecimalInterpretationOrFormat()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Assert.That(WorkPriceInput.TryParse("12.50", out long minor), Is.True);
                Assert.That(minor, Is.EqualTo(1250L));
                Assert.That(WorkPriceInput.TryParse("12,50", out _), Is.False);
                Assert.That(WorkPriceInput.Format(minor), Is.EqualTo("12.50"));
            }
            finally { Thread.CurrentThread.CurrentCulture = previous; }
        }
    }
}

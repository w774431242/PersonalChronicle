using System;
using NUnit.Framework;
using PersonalChronicle.MasterControl.Population;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class PersonnelArchivePanelTests
    {
        [Test]
        public void CanCreateIndependentPanelsWithoutLegacyMainButtonDef()
        {
            // Regression: the personnel center previously borrowed MainButtonDef.TabWindow.
            // Creating the replacement must work without registering or opening that tab.
            var first = new PersonnelArchivePanel();
            var second = new PersonnelArchivePanel();
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(typeof(Window).IsAssignableFrom(first.GetType()), Is.False);
            Assert.That(typeof(PersonnelArchivePanel).Assembly.GetType(
                "PersonalChronicle.Archive.ArchiveMainTabWindow"), Is.Null);
        }
    }
}

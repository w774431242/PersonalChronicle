using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Linq;
using NUnit.Framework;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Automation;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class ProductionDeviceCardTests
    {
        private static ThingDef Definition(string name, ProductionDeviceCardExtension card = null)
        {
            var def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            def.defName = name;
            def.modExtensions = card == null ? null : new List<DefModExtension> { card };
            return def;
        }

        [TestCase(ProductionDeviceTier.T1)]
        [TestCase(ProductionDeviceTier.T2)]
        [TestCase(ProductionDeviceTier.T3)]
        public void DeclaredTier_MatchesIndependentOfDeviceNameAndLabel(ProductionDeviceTier tier)
        {
            var extension = new ProductionDeviceCardExtension { tier = tier, texturePath = "Custom/CardNorth" };
            var def = Definition("ThirdPartyDevice", extension);
            def.label = "重命名的设备";
            Assert.IsTrue(ProductionDeviceCards.TryResolve(def, out var found));
            Assert.AreSame(extension, found);
            Assert.AreEqual("PersonalChronicle.ProductionCard." + tier,
                ProductionDeviceCardTier.TranslationKey(found.tier));
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(-1)]
        public void UndeclaredTier_DoesNotInventCardIdentity(int tier)
        {
            Assert.IsFalse(ProductionDeviceCards.TryResolve(Definition("PC_T1_LooksLikeT1",
                new ProductionDeviceCardExtension { tier = (ProductionDeviceTier)tier, texturePath = "Existing/Art" }), out _));
        }

        [Test]
        public void MissingMetadataOrPath_RetainsGenericCardFallback()
        {
            Assert.IsFalse(ProductionDeviceCards.TryResolve(null, out _));
            Assert.IsFalse(ProductionDeviceCards.TryResolve(Definition("PC_T2_Future"), out _));
            Assert.IsFalse(ProductionDeviceCards.TryResolve(Definition("MissingArt",
                new ProductionDeviceCardExtension { tier = ProductionDeviceTier.T3, texturePath = " " }), out _));
            Assert.IsNull(ProductionDeviceCards.Build(null));
        }

        [Test]
        public void EveryShippedT1DeclaresItsOwnRealNorthTexture_WithoutFutureFakeDevices()
        {
            var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName, "Defs", "T1_Production.xml"))) root = root.Parent;
            Assert.IsNotNull(root);
            var defs = XDocument.Load(Path.Combine(root.FullName, "Defs", "T1_Production.xml"));
            var benches = defs.Root.Elements("ThingDef").ToList();
            Assert.AreEqual(10, benches.Count);
            foreach (var def in benches)
            {
                string id = (string)def.Element("defName");
                var card = def.Element("modExtensions").Elements("li").Single(x =>
                    (string)x.Attribute("Class") == typeof(ProductionDeviceCardExtension).FullName);
                Assert.AreEqual("T1", (string)card.Element("tier"), id);
                string path = (string)card.Element("texturePath");
                Assert.AreEqual("Things/Building/T1Production/" + id + "_north", path);
                Assert.IsTrue(File.Exists(Path.Combine(root.FullName, "Textures", path + ".png")), id);
            }
        }
    }
}

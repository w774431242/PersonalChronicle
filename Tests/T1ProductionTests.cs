using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using NUnit.Framework;
using PersonalChronicle.Application;
using PersonalChronicle.Automation;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    public sealed class T1ProductionTests
    {
        private static string Root
        {
            get
            {
                var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "Defs", "T1_Production.xml"))) return dir.FullName;
                    dir = dir.Parent;
                }
                throw new DirectoryNotFoundException("Cannot locate T1 developer content");
            }
        }

        [Test]
        public void PoweredManualBenches_AreInAutomation_AndHaveNoFallbackOrStuff()
        {
            var defs = XDocument.Load(Path.Combine(Root, "Defs", "T1_Production.xml"));
            var benches = defs.Root.Elements("ThingDef").ToList();
            Assert.AreEqual(10, benches.Count);
            Assert.IsFalse(defs.Root.Elements("DesignatorDropdownGroupDef").Any(),
                "T1 benches must not be merged into a dropdown group");
            foreach (var def in benches)
            {
                string id = (string)def.Element("defName");
                Assert.AreEqual("BenchBase", (string)def.Attribute("ParentName"), id);
                Assert.AreEqual("PC_Automation", (string)def.Element("designationCategory"), id);
                Assert.IsNull(def.Element("designatorDropdown"), id + ": T1 bench must be a standalone build button");
                Assert.AreEqual("(3,1)", (string)def.Element("size"), id);
                Assert.AreEqual("(0,0,-1)", (string)def.Element("interactionCellOffset"), id);
                Assert.AreEqual("0", (string)def.Element("building").Element("unpoweredWorkTableWorkSpeedFactor"), id);
                Assert.IsNull(def.Element("stuffCategories"), id);
                Assert.IsTrue(def.Element("researchPrerequisites").Elements("li")
                    .Any(x => x.Value == "PC_Research_T1Production"), id);
                var comps = def.Element("comps").Elements("li").ToList();
                Assert.AreEqual("False", (string)def.Element("comps").Attribute("Inherit"), id);
                Assert.AreEqual(comps.Count, comps.Select(x => (string)x.Attribute("Class")).Distinct().Count(), id);
                foreach (string cls in new[] { "CompProperties_Power", "CompProperties_Breakdownable", "CompProperties_Flickable", "CompProperties_AffectedByFacilities", "CompProperties_ReportWorkSpeed" })
                    Assert.AreEqual(1, comps.Count(x => (string)x.Attribute("Class") == cls), id + ": " + cls);
                string typeName = (string)def.Element("thingClass");
                Type type = typeof(Building_SmartBench).Assembly.GetType(typeName, true);
                Assert.IsTrue(typeof(Building_WorkTable).IsAssignableFrom(type), id);
                Assert.IsTrue(typeof(IWorkshopDevice).IsAssignableFrom(type), id);
                Assert.AreEqual(typeof(Building_WorkTable), type.GetMethod("ExposeData").DeclaringType, id);
            }
        }

        [Test]
        public void Speed_UsesPrototypeBaseline_AndHeatUsesOriginalValues()
        {
            var nodes = XDocument.Load(Path.Combine(Root, "Defs", "T1_Production.xml")).Root.Elements("ThingDef");
            string rw = Directory.GetParent(Directory.GetParent(Root).FullName).FullName;
            var vanilla = XDocument.Load(Path.Combine(rw, "Data", "Core", "Defs", "ThingDefs_Buildings", "Buildings_Production.xml"));
            foreach (var def in nodes)
            {
                string id = (string)def.Element("defName");
                string prototype = (string)def.Element("modExtensions").Element("li").Element("prototype");
                var proto = vanilla.Root.Elements("ThingDef").Single(x => (string)x.Element("defName") == prototype);
                double baseline = (double?)proto.Element("statBases")?.Element("WorkTableWorkSpeedFactor") ?? 1.0;
                double speed = (double)def.Element("statBases").Element("WorkTableWorkSpeedFactor");
                Assert.AreEqual(4.0, speed / baseline, 0.0001, id + ": processing speed relative to vanilla prototype");
                if (id == "PC_T1_Smelter" || id == "PC_T1_Cooking")
                {
                    Assert.AreEqual(typeof(Building_SmartBenchHeated).FullName, (string)def.Element("thingClass"));
                    Assert.AreEqual(id == "PC_T1_Smelter" ? "0.32" : "0.10",
                        (string)def.Element("building").Element("heatPerTickWhileWorking"));
                }
            }
            // Current installed API, rather than relying only on the decompiled mirror.
            Assert.IsTrue(typeof(Building_WorkTable).GetMethod("UsedThisTick").IsVirtual);
            MethodInfo usable = typeof(Building_WorkTable).GetMethod("CurrentlyUsableForBills");
            Assert.IsTrue(!usable.IsVirtual || usable.IsFinal, "Interface implementation is not overridable");
        }

        [Test]
        public void BenchResearch_DoesNotBypassOriginalTechnologyGates()
        {
            string rw = Directory.GetParent(Directory.GetParent(Root).FullName).FullName;
            var original = XDocument.Load(Path.Combine(rw, "Data", "Core", "Defs", "ThingDefs_Buildings", "Buildings_Production.xml"));
            foreach (var def in XDocument.Load(Path.Combine(Root, "Defs", "T1_Production.xml")).Root.Elements("ThingDef"))
            {
                string prototype = (string)def.Element("modExtensions").Element("li").Element("prototype");
                var proto = original.Root.Elements("ThingDef").Single(x => (string)x.Element("defName") == prototype);
                var research = def.Element("researchPrerequisites").Elements("li").Select(x => x.Value).ToList();
                if (proto.Element("researchPrerequisites") != null)
                    foreach (string required in proto.Element("researchPrerequisites").Elements("li").Select(x => x.Value))
                        CollectionAssert.Contains(research, required, (string)def.Element("defName"));
                if (prototype == "CraftingSpot")
                    Assert.AreEqual("", (string)def.Element("building").Element("workTableRoomRole"));
            }
        }

        [Test]
        public void WorkRows_UseTheActualVanillaWorkType_AndEveryBenchHasOneRow()
        {
            string rw = Directory.GetParent(Directory.GetParent(Root).FullName).FullName;
            var vanilla = XDocument.Load(Path.Combine(rw, "Data", "Core", "Defs", "WorkGiverDefs", "WorkGivers.xml"));
            var workRows = XDocument.Load(Path.Combine(Root, "Defs", "T1_WorkshopProcesses.xml")).Root.Elements().ToList();
            foreach (var def in XDocument.Load(Path.Combine(Root, "Defs", "T1_Production.xml")).Root.Elements("ThingDef"))
            {
                string id = (string)def.Element("defName");
                string prototype = (string)def.Element("modExtensions").Element("li").Element("prototype");
                var giver = vanilla.Root.Elements("WorkGiverDef").First(x => x.Element("fixedBillGiverDefs") != null
                    && x.Element("fixedBillGiverDefs").Elements("li").Any(y => y.Value == prototype));
                var row = workRows.Single(x => x.Element("expectedDeviceDefNames").Element("li").Value == id);
                Assert.AreEqual((string)giver.Element("workType"), (string)row.Element("workTypeDefNames").Element("li"), id);
            }
        }

        [Test]
        public void Binding_RepairsEarlyRecipeCache_AndIsIdempotent()
        {
            // BuildableDef 构造会加载 Unity 原生 Shader；离线只创建本测试需要的托管字段。
            var prototype = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            prototype.defName = "TestPrototype";
            var target = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            target.defName = "TestT1";
            target.recipes = new List<RecipeDef>();
            var earlyCache = target.AllRecipes;
            var skill = new SkillDef { defName = "TestSkill" };
            var research = new ResearchProjectDef { defName = "TestResearch" };
            var recipe = new RecipeDef { defName = "TestGeneratedOrRecipeUsers", workSkill = skill, researchPrerequisite = research };
            var originalList = new List<ThingDef> { prototype };
            var giver = new WorkGiverDef { giverClass = typeof(WorkGiver_DoBill), fixedBillGiverDefs = originalList };
            var unrelated = new WorkGiverDef { giverClass = typeof(WorkGiver_DoBill), fixedBillGiverDefs = new List<ThingDef>() };
            var givers = new List<WorkGiverDef> { giver, unrelated };
            Assert.AreEqual(1, SmartBenchBootstrap.Bind(target, prototype, new[] { recipe, recipe }, givers));
            Assert.AreSame(earlyCache, target.AllRecipes);
            Assert.AreEqual(1, target.AllRecipes.Count);
            Assert.AreSame(recipe, target.AllRecipes[0]);
            Assert.AreSame(skill, target.AllRecipes[0].workSkill);
            Assert.AreSame(research, target.AllRecipes[0].researchPrerequisite);
            Assert.AreEqual(1, originalList.Count, "XML-shared work list must not be mutated");
            Assert.AreEqual(2, giver.fixedBillGiverDefs.Count);
            Assert.AreEqual(0, unrelated.fixedBillGiverDefs.Count);
            SmartBenchBootstrap.Bind(target, prototype, new[] { recipe }, givers);
            Assert.AreEqual(1, target.AllRecipes.Count);
            Assert.AreEqual(2, giver.fixedBillGiverDefs.Count);
        }

        [Test]
        public void GraphicsSwitch_ReplacesCachedBuildingAndMenuGraphic()
        {
            var target = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            var old = (Graphic)FormatterServices.GetUninitializedObject(typeof(Graphic_Single));
            var replacement = (Graphic)FormatterServices.GetUninitializedObject(typeof(Graphic_Multi));
            target.graphic = old;
            target.uiIconAngle = 180f;
            var data = new GraphicData { texPath = "NewT1Path" };
            SmartBenchBootstrap.ApplyResolvedGraphic(target, data, replacement, null);
            Assert.AreSame(data, target.graphicData);
            Assert.AreSame(replacement, target.graphic, "Actual drawing must not retain the old cached graphic");
            Assert.IsNull(target.uiIcon);
            Assert.IsNull(target.uiIconMaterial);
            Assert.AreEqual(0f, target.uiIconAngle);
        }

        [Test]
        public void BlueprintSwitch_UpdatesShape_AndPreservesBlueprintShaderAndTint()
        {
            var shader = new ShaderTypeDef { defName = "TestEdgeDetect" };
            var old = new GraphicData { texPath = "OldBench", shaderType = shader,
                color = new UnityEngine.Color(.2f, .4f, 1f, .5f), renderQueue = 2950 };
            var building = new GraphicData { texPath = "CustomT1", graphicClass = typeof(Graphic_Multi),
                drawSize = new UnityEngine.Vector2(3.5f, 1.5f), drawRotated = false, allowFlip = false };
            var prepared = SmartBenchBootstrap.PrepareBlueprintGraphic(old, building);
            Assert.AreEqual(building.texPath, prepared.texPath);
            Assert.AreEqual(building.drawSize, prepared.drawSize);
            Assert.AreEqual(typeof(Graphic_Multi), prepared.graphicClass);
            Assert.AreSame(shader, prepared.shaderType);
            Assert.AreEqual(old.color, prepared.color);
            Assert.AreEqual(2950, prepared.renderQueue);
            Assert.AreEqual("OldBench", old.texPath, "Shared generated blueprint config must not be mutated");
        }

        [Test]
        public void PrototypeRecipes_IncludeLateGeneratedRecipeUsers_EvenAfterEarlyCache()
        {
            var prototype = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            prototype.defName = "LatePrototype";
            prototype.recipes = new List<RecipeDef>();
            Assert.IsEmpty(prototype.AllRecipes);
            var explicitRecipe = new RecipeDef { defName = "ExplicitRecipe" };
            var generatedRecipe = new RecipeDef { defName = "GeneratedRecipe", recipeUsers = new List<ThingDef> { prototype } };
            var unrelated = new RecipeDef { defName = "UnrelatedRecipe", recipeUsers = new List<ThingDef>() };
            prototype.recipes.Add(explicitRecipe);
            var collected = SmartBenchBootstrap.CollectPrototypeRecipes(prototype, new[] { generatedRecipe, unrelated });
            CollectionAssert.AreEquivalent(new[] { explicitRecipe, generatedRecipe }, collected);
        }

        [Test]
        public void Equivalence_OneT1SatisfiesAlternativePrototypes_WithoutSatisfyingOtherProcesses()
        {
            Func<string, string> canonical = n => n == "Fuel" || n == "T1" ? "Electric" : n;
            var fit = WorkshopDeviceFitEvaluator.Evaluate(new List<string> { "Electric", "Fuel" },
                new[] { "T1", "Aux", "WrongProcess" }, canonical, new[] { "Aux" });
            Assert.AreEqual(2, fit.Matched);
            Assert.AreEqual(1, fit.Extra);
            Assert.IsEmpty(fit.MissingDefNames);
            fit = WorkshopDeviceFitEvaluator.Evaluate(new List<string> { "Electric", "Fuel" },
                new[] { "Aux" }, canonical, new[] { "Aux" });
            Assert.AreEqual(1, fit.Matched);
            CollectionAssert.AreEqual(new[] { "Electric" }, fit.MissingDefNames);
        }

        [TestCase(true, false, true, true, "fault")]
        [TestCase(false, false, true, true, "off")]
        [TestCase(false, true, true, false, "SmartBenchWaiting")]
        [TestCase(false, true, false, false, "idle")]
        [TestCase(false, true, true, true, "run")]
        public void State_DoesNotClaimProductionFromPendingBills(bool broken, bool powered, bool bill, bool used, string expected)
        {
            Assert.AreEqual(expected, SmartBenchState.KeyOf(broken, powered, bill, used));
            Assert.AreEqual("PersonalChronicle.T1.Waiting", EquipmentStateText.TranslationKeyOf(SmartBenchState.Waiting));
        }
    }
}

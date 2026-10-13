using System.Collections.Generic;
using System.Runtime.Serialization;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Capture;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// CHG-20261012-ECON-HAULCMD：**原版搬运命令放宽判据**的离线断言。
    ///
    /// <para>钉住四件事：① 放宽判据的边界（未生成 / 非 EverHaulable / 非 Item / 已标记 / 已在合格储存区）；
    /// ② 格内兜底与放宽受理**同源**（放宽受理 ⇒ 形态成立；否则 CanDesignateCell 与 DesignateSingleCell 口径分叉）；
    /// ③ 两个 Harmony 挂点签名存在且启动期探测为真（漂移即红灯，而不是静默跳过）；
    /// ④ 既有 8 族能力矩阵未被波及（Hunt / Slaughter / 未声明族 / null 仍 Unsupported）。</para>
    /// </summary>
    [TestFixture]
    public sealed class HaulCommandRulesTests
    {
        private static ThingDef Def(ThingCategory category, bool alwaysHaulable, bool designateHaulable, string name)
        {
            var def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
            def.defName = name;
            def.category = category;
            def.alwaysHaulable = alwaysHaulable;
            def.designateHaulable = designateHaulable;
            return def;
        }

        [Test] public void ShapeRejectsNonSpawnedNonEverHaulableNonItem()
        {
            Assert.That(HaulCommandRules.IsCommandableShape(false, true, true), Is.False, "未生成 ⇒ 不受理");
            Assert.That(HaulCommandRules.IsCommandableShape(true, false, true), Is.False, "非 EverHaulable ⇒ 不受理");
            Assert.That(HaulCommandRules.IsCommandableShape(true, true, false), Is.False, "非 Item ⇒ 不受理（与 GetFirstHaulable 口径一致）");
            Assert.That(HaulCommandRules.IsCommandableShape(true, true, true), Is.True);
        }

        [Test] public void ThingAdapterReadsEverHaulableAndCategory()
        {
            Assert.That(HaulCommandRules.IsCommandableShape((Thing)null), Is.False);
            var food = Def(ThingCategory.Item, true, false, "PC_Test_Food");
            Assert.That(food.EverHaulable, Is.True, "alwaysHaulable ⇒ EverHaulable（原版语义）");
            // 未生成（holder == null）⇒ 形态不成立：这条锁住 Spawned 守卫（在背包 / 容器里的物品不该被命令搬运）。
            Assert.That(HaulCommandRules.IsCommandableShape(new Thing { def = food }), Is.False);
            var building = Def(ThingCategory.Building, false, true, "PC_Test_Building");
            Assert.That(building.EverHaulable, Is.True, "designateHaulable ⇒ EverHaulable");
            Assert.That(HaulCommandRules.IsCommandableShape(new Thing { def = building }), Is.False, "非 Item 类别不得成为候选");
        }

        [Test] public void RelaxationIsExactlyShapeMinusDesignatedAndStored()
        {
            var flags = new[] { false, true };
            foreach (bool spawned in flags)
                foreach (bool ever in flags)
                    foreach (bool item in flags)
                        foreach (bool designated in flags)
                            foreach (bool stored in flags)
                            {
                                bool shape = HaulCommandRules.IsCommandableShape(spawned, ever, item);
                                bool relax = HaulCommandRules.ShouldRelaxVanillaRejection(spawned, ever, item, designated, stored);
                                Assert.That(relax, Is.EqualTo(shape && !designated && !stored),
                                    "判据矩阵不一致: spawned=" + spawned + " ever=" + ever + " item=" + item
                                    + " designated=" + designated + " stored=" + stored);
                            }
        }

        [Test] public void RelaxationImpliesCellFallbackShapeAndAlwaysGivesAVerdict()
        {
            // 唯一来源纪律：放宽受理的目标必须也能被 GetFirstHaulable 兜底取到，否则拖动框选
            // （CanDesignateCell → CanDesignateThing）与单点下达（DesignateSingleCell）口径分叉，
            // 后者会拿到 null 并 new Designation(null, ...) ⇒ NRE。
            var flags = new[] { false, true };
            foreach (bool spawned in flags)
                foreach (bool ever in flags)
                    foreach (bool item in flags)
                        foreach (bool designated in flags)
                            foreach (bool stored in flags)
                            {
                                bool shape = HaulCommandRules.IsCommandableShape(spawned, ever, item);
                                bool relax = HaulCommandRules.ShouldRelaxVanillaRejection(spawned, ever, item, designated, stored);
                                if (relax) Assert.That(shape, Is.True, "放宽受理必须蕴含格内兜底形态");
                                if (shape) Assert.That(relax || designated || stored, Is.True,
                                    "形态成立的目标必须给出结论：放宽受理 / 原版已标记 / 储存区理由（不得既不放宽也不给理由）");
                            }
        }

        /// <summary>
        /// 钉住 2026-10-12 实机「框选后没有石块那样的高亮」的根因：原版 <c>WasRejected</c> 的
        /// <c>Reason</c> 是**空串而非 null**（<c>Verse/AcceptanceReport.cs:43-52</c>）。旧判据
        /// <c>Reason != null</c> 对每一条裸拒都判成「已有理由」⇒ 放宽分支永不执行 ⇒
        /// <c>CanDesignateThing</c> 一直只受理石块类 ⇒ 高亮与对象级受理双双失效。
        /// </summary>
        [Test] public void BareRejectionIsEmptyReasonNotNul()
        {
            Assert.That(AcceptanceReport.WasRejected.Reason, Is.Not.Null, "原版 WasRejected.Reason 是空串，不是 null");
            Assert.That(AcceptanceReport.WasRejected.Reason, Is.Empty);
            Assert.That(HaulCommandRules.IsBareRejection(AcceptanceReport.WasRejected), Is.True);
            Assert.That(HaulCommandRules.IsBareRejection(false), Is.True, "隐式 bool→AcceptanceReport 的 false 也是裸拒");
            Assert.That(HaulCommandRules.IsBareRejection(AcceptanceReport.WasAccepted), Is.False);
            Assert.That(HaulCommandRules.IsBareRejection("MessageAlreadyInStorage"), Is.False,
                "带具名理由的拒绝不是裸拒（必须原样透出，否则成了静默拒绝）");
            // 放宽链路：形态成立 + 未标记 + 未入库 ⇒ 该裸拒必须被放宽（高亮与受理同源）。
            Assert.That(HaulCommandRules.ShouldRelaxVanillaRejection(true, true, true, false, false), Is.True);
        }

        [Test] public void HarmonyTargetsAndSignatureProbesAreHealthy()
        {
            Assert.That(AccessTools.Method(typeof(Designator_Haul), "CanDesignateThing", new[] { typeof(Thing) }), Is.Not.Null);
            Assert.That(AccessTools.Method(typeof(Designator_Haul), "CanDesignateCell", new[] { typeof(IntVec3) }), Is.Not.Null);
            Assert.That(AccessTools.Method(typeof(Designator_Haul), "DesignateSingleCell", new[] { typeof(IntVec3) }), Is.Not.Null);
            Assert.That(Patch_HaulDesignatorThing.TargetMethodExists, Is.True);
            // R2 返修（CHG-20261012-ECON-HAULCMD）：格内路径不再挂 GridsUtility.GetFirstHaulable
            //（6 行静态小方法被 Mono JIT 内联 ⇒ 补丁挂在不被执行的调用点，实机未生效）。
            Assert.That(Patch_HaulDesignatorCell.TargetMethodExists, Is.True);
            Assert.That(Patch_HaulDesignatorSingleCell.TargetMethodExists, Is.True);
        }

        /// <summary>
        /// 格内候选选择器：**与放宽受理同源**且**绝不抛**（fail-closed 返回 null）。
        /// 离线无法构造 Spawned 的 Thing ⇒ 这里只钉「空表 / 含 null / 非候选一律得 null、不抛」；
        /// 「能挑出真实可搬运物」属游戏内验证项（见 HaulCommandRules 的 R2 注释）。
        /// </summary>
        [Test] public void CellSelectorIsNullSafeAndFailClosed()
        {
            Assert.That(HaulCommandRules.FirstCommandableIn(null), Is.Null);
            Assert.That(HaulCommandRules.FirstCommandableIn(new List<Thing>()), Is.Null);
            Assert.That(HaulCommandRules.FirstCommandableIn(new List<Thing> { null, null }), Is.Null);
            var food = Def(ThingCategory.Item, true, false, "PC_Test_Food2");
            // 未生成（无 holder）⇒ 形态不成立 ⇒ 不是候选（在背包 / 容器里的物品不得被命令搬运）。
            Assert.That(HaulCommandRules.FirstCommandableIn(new List<Thing> { new Thing { def = food } }), Is.Null);
            Assert.That(HaulCommandRules.FindCommandableInCell(new IntVec3(0, 0, 0), null), Is.Null);
        }

        [Test] public void ExistingCapabilityMatrixIsUntouchedExceptHaul()
        {
            Assert.That(LaborFlowRules.CapabilityFor("Hunt"), Is.EqualTo(DeliveryCapability.Unsupported));
            Assert.That(LaborFlowRules.CapabilityFor("Slaughter"), Is.EqualTo(DeliveryCapability.Unsupported));
            Assert.That(LaborFlowRules.CapabilityFor("Haul"), Is.EqualTo(DeliveryCapability.EvidenceOnly));
            Assert.That(LaborFlowRules.CapabilityFor("SomeThirdPartyOrder"), Is.EqualTo(DeliveryCapability.Unsupported));
            Assert.That(LaborFlowRules.CapabilityFor(null), Is.EqualTo(DeliveryCapability.Unsupported));
        }
    }
}

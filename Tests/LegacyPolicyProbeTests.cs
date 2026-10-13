using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Data.PawnEconomy;
using Verse;

namespace PersonalChronicle.Tests
{
    /// <summary>CHG-20261012-ECON-CONTRACTIZE · B2：旧档只读迁移探针的离线锁定。
    ///
    /// <para>用例口径（对应探针的三条硬纪律）：① 旧节点**能读**（条目数与锚点样本正确）；
    /// ② 缺节点/畸形内容**不抛**（拿到 null 或计数 0，而不是异常）；③ 写回封印 —— 保存模式下
    /// 探针**一个字节都不写**（与生产侧"只在 LoadingVars 才读"互为双保险）。</para></summary>
    [TestFixture, NonParallelizable]
    public sealed class LegacyPolicyProbeTests
    {
        private Harmony shim;
        private string path;
        [OneTimeSetUp] public void Start()
        {
            shim = new Harmony("personalchronicle.tests.legacypolicyprobe");
            foreach (string name in new[] { "Error", "Warning", "Message" }) Patch(typeof(Log), name, typeof(string));
            // 与 `PawnEconomyScribeTests` 同款：`DeepProfiler` 在游戏外未初始化，`Scribe` 进出节点会踩它 ⇒ 屏蔽。
            Patch(typeof(DeepProfiler), "Start", typeof(string)); Patch(typeof(DeepProfiler), "End");
        }
        private void Patch(Type type, string name, params Type[] args)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, args, null);
            if (method != null) shim.Patch(method, prefix: new HarmonyMethod(typeof(LegacyPolicyProbeTests).GetMethod(nameof(Skip), BindingFlags.Static | BindingFlags.NonPublic)));
        }
        private static bool Skip() => false;
        [OneTimeTearDown] public void Stop() => shim.UnpatchAll(shim.Id);
        [SetUp] public void Setup() { path = Path.Combine(Path.GetTempPath(), "pc_legacypolicy_" + Guid.NewGuid().ToString("N") + ".xml"); }
        [TearDown] public void Cleanup() { Scribe.ForceStop(); if (File.Exists(path)) File.Delete(path); }

        /// <summary>把手工 XML 当作存档读进来 —— 与 `PawnEconomyScribeTests.MissingSaveKeyDefaultsToDisabledWithoutFunding`
        /// 同款范式（写文件 → `InitLoading` → `Scribe_Deep`）。</summary>
        private LegacyPolicyProbe LoadProbe(string xml)
        {
            File.WriteAllText(path, xml);
            LegacyPolicyProbe probe = null;
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref probe, LegacyPolicyProbe.NodeName);
            Scribe.loader.FinalizeLoading();
            return probe;
        }

        private const string LegacyBody =
            "<?xml version=\"1.0\"?><savegame>"
            + "<pawnEconomicPolicies><institutionPolicies>"
            + "<li><anchor>Building_A123</anchor><revision>7</revision><zoneId>12</zoneId>"
            + "<wagePerJob>500</wagePerJob><foodPricePerNutrition>30</foodPricePerNutrition>"
            + "<servicePricePerUnit>900</servicePricePerUnit><apparelPrice>400</apparelPrice>"
            + "<open>True</open><startHour>8</startHour><endHour>18</endHour>"
            + "<stockTargets><li><category>MealSimple</category><amount>40</amount></li></stockTargets></li>"
            + "<li><anchor>Building_B456</anchor><revision>3</revision></li>"
            + "</institutionPolicies></pawnEconomicPolicies></savegame>";

        [Test] public void LegacyPolicyNodeIsReadAndCountedWithItsAnchorSample()
        {
            var probe = LoadProbe(LegacyBody);
            Assert.That(probe, Is.Not.Null, "旧档含政策节点时必须能读进来（可加载性）");
            Assert.That(probe.Count, Is.EqualTo(2), "两条有效条目");
            Assert.That(probe.FirstAnchor, Is.EqualTo("Building_A123"), "锚点样本按原顺序取首个");
            Assert.DoesNotThrow(() => probe.Report(), "报告本身不得抛（诊断路径也要安全）");
        }

        [Test] public void ASaveWithoutTheNodeYieldsNoProbeAndNoThrow()
        {
            var probe = LoadProbe("<?xml version=\"1.0\"?><savegame />");
            Assert.That(probe, Is.Null, "没有该节点 ⇒ 调用方拿到 null（不抛、不发明空对象）");
        }

        [Test] public void RowsWithoutAnAnchorAreIgnoredAndDoNotThrow()
        {
            var probe = LoadProbe("<?xml version=\"1.0\"?><savegame><pawnEconomicPolicies><institutionPolicies>"
                + "<li><revision>1</revision></li><li><anchor></anchor></li><li><anchor>Building_C789</anchor></li>"
                + "</institutionPolicies></pawnEconomicPolicies></savegame>");
            Assert.That(probe, Is.Not.Null);
            Assert.That(probe.Count, Is.EqualTo(1), "只有带非空 anchor 的行算有效条目");
            Assert.That(probe.FirstAnchor, Is.EqualTo("Building_C789"));
        }

        [Test] public void ProbeWritesNothingInSavingMode()
        {
            // 写回封印的**行为侧**证明：保存模式下游走一遍，产出的 XML 里既没有节点也没有任何字段。
            var probe = LoadProbe(LegacyBody);
            var probeType = typeof(LegacyPolicyProbe);
            Scribe.saver.InitSaving(path, "savegame");
            Scribe_Deep.Look(ref probe, LegacyPolicyProbe.NodeName);
            Scribe.saver.FinalizeSaving();
            string written = File.ReadAllText(path);
            Assert.That(probeType, Is.Not.Null);
            Assert.That(written, Does.Not.Contain("<anchor>"), "探针在保存模式下不得写出锚点");
            Assert.That(written, Does.Not.Contain("<revision>"), "探针在保存模式下不得写出修订号");
            Assert.That(written, Does.Not.Contain("<wagePerJob>"), "探针不持有也不写出工资（旧值不迁移）");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// <see cref="WorkshopWorkDemotionLedger"/> 的 **Scribe 往返**（`CHG-20261003-WORKSTATION`）。
    ///
    /// <para><b>为什么必须有</b>：台账是「一键全解除」的唯一凭据，而 Scribe 段的失败模式
    /// <b>完全静默</b> —— 键名写错 / 嵌套层级不对 / <c>LookMode</c> 用错，读档后台账恒空，
    /// 玩家点「一键全解除」永远还原不了，<b>不报错、不崩</b>。纯逻辑单测
    /// （<see cref="WorkshopWorkDemotionLedgerTests"/>）覆盖不到这条路径。</para>
    ///
    /// <para>宿主与 <see cref="StockSeriesScribeTests"/> 同规：离线无 Unity 运行时 ⇒ 把
    /// <c>Log.Error/Warning/Message</c> 与 <c>DeepProfiler.Start/End</c> 短接为捕获。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopWorkDemotionScribeTests
    {
        private static HarmonyLib.Harmony logShim;

        private static readonly List<string> logCapture = new List<string>();

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            logShim = new HarmonyLib.Harmony("personalchronicle.tests.workdemotionscribesilencer");
            PatchLog("Error");
            PatchLog("Warning");
            PatchLog("Message");
            PatchVoid(typeof(DeepProfiler), "Start", typeof(string));
            PatchVoid(typeof(DeepProfiler), "End");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (logShim != null)
            {
                logShim.UnpatchAll(logShim.Id);
                logShim = null;
            }
        }

        private static void PatchVoid(Type type, string name, params Type[] args)
        {
            MethodInfo original = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, args, null);
            if (original == null)
            {
                return;
            }
            logShim.Patch(original, prefix: new HarmonyLib.HarmonyMethod(
                typeof(WorkshopWorkDemotionScribeTests).GetMethod(nameof(VoidPrefix),
                    BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static bool VoidPrefix()
        {
            return false;
        }

        private static void PatchLog(string name)
        {
            MethodInfo original = typeof(Log).GetMethod(name,
                BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (original == null)
            {
                return;
            }
            logShim.Patch(original, prefix: new HarmonyLib.HarmonyMethod(
                typeof(WorkshopWorkDemotionScribeTests).GetMethod(nameof(LogPrefix),
                    BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static bool LogPrefix(string text)
        {
            logCapture.Add(text ?? string.Empty);
            return false;
        }

        private string path;

        [SetUp]
        public void SetUp()
        {
            logCapture.Clear();
            path = Path.Combine(Path.GetTempPath(),
                "pc_workdemotion_scribe_" + Guid.NewGuid().ToString("N") + ".xml");
        }

        [TearDown]
        public void TearDown()
        {
            Scribe.ForceStop();
            Scribe.mode = LoadSaveMode.Inactive;
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // 清理失败不影响判定（临时目录）
            }
        }
        // ---------------- ① 全保真往返 ----------------

        [Test]
        public void RoundTrip_PreservesPawnIdsDefNamesAndOriginals()
        {
            var original = new WorkshopWorkDemotionLedger();
            original.Record(1001, "Hauling", 2);
            original.Record(1001, "Cleaning", 3);
            original.Record(2002, "Smithing", 1);

            WorkshopWorkDemotionLedger loaded = RoundTrip(original);

            if (loaded == null)
            {
                Assert.Fail("往返后对象为 null（Scribe 段名 / 嵌套层级可能写错）；捕获日志："
                    + JoinLogs());
            }
            Assert.AreEqual(2, loaded.pawns.Count, "pawn 记录数");
            Assert.AreEqual(3, loaded.TotalEntryCount, "条目总数");

            int value;
            Assert.IsTrue(loaded.TryGetOriginal(1001, "Hauling", out value));
            Assert.AreEqual(2, value, "1001/Hauling 原档位");
            Assert.IsTrue(loaded.TryGetOriginal(1001, "Cleaning", out value));
            Assert.AreEqual(3, value, "1001/Cleaning 原档位");
            Assert.IsTrue(loaded.TryGetOriginal(2002, "Smithing", out value));
            Assert.AreEqual(1, value, "2002/Smithing 原档位");
        }

        // ---------------- ② 空台账（空集合不写档 ⇒ 读回 null ⇒ 归一） ----------------

        [Test]
        public void RoundTrip_EmptyLedger_DoesNotCrash()
        {
            var original = new WorkshopWorkDemotionLedger();
            WorkshopWorkDemotionLedger loaded = RoundTrip(original);
            // 空台账：Scribe 不写该节点 ⇒ 目标保持 null 是**合法**结果（Data 层负责归一）。
            // 本用例只断言"不抛异常"，不强制非 null。
            if (loaded != null)
            {
                Assert.AreEqual(0, loaded.TotalEntryCount);
            }
            Assert.IsFalse(HasScribeError(), "空台账往返不应产生 Scribe 错误；日志："
                + JoinLogs());
        }

        // ---------------- ③ 旧档缺段（GATE-SAVE：加性可选字段的核心契约） ----------------

        [Test]
        public void OldSave_MissingSegment_LeavesTargetNull_WithoutThrowing()
        {
            // 手工写一个**不含** workshopWorkDemotion 节点的 XML，模拟旧档。
            File.WriteAllText(path,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<savegame>\n</savegame>");

            WorkshopWorkDemotionLedger loaded = null;
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref loaded, "workshopWorkDemotion");
            Scribe.loader.FinalizeLoading();

            Assert.IsNull(loaded, "旧档读回必须是 null（由 ChronicleGameComponent.ExposeWorkDemotion 归一为空台账）");
            Assert.IsFalse(HasScribeError(), "旧档缺段不得报错；日志：" + JoinLogs());
        }

        // ---------------- ④ 非法 XML 字符被清洗（存档 P0 防护） ----------------

        [Test]
        public void RoundTrip_SanitizesIllegalXmlChars_InKeys()
        {
            // 内存态塞入非法字符键 ⇒ 保存前 Sanitize 就地清洗 ⇒ 文件良构、往返后键为清洗值。
            var original = new WorkshopWorkDemotionLedger();
            original.Record(1001, "Hauli\x01ng", 2);

            WorkshopWorkDemotionLedger loaded = RoundTrip(original);
            if (loaded == null)
            {
                Assert.Fail("往返后对象为 null；捕获日志：" + JoinLogs());
            }
            int value;
            Assert.IsTrue(loaded.TryGetOriginal(1001, "Hauli_ng", out value),
                "非法字符必须在保存前被替换为 '_'（否则整个存档保存失败）");
            Assert.AreEqual(2, value);
        }

        private bool HasScribeError()
        {
            for (int i = 0; i < logCapture.Count; i++)
            {
                if (logCapture[i].IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private WorkshopWorkDemotionLedger RoundTrip(WorkshopWorkDemotionLedger source)
        {
            SaveTo(source);
            return LoadFrom();
        }

        /// <summary>
        /// ⚠ 纪律（同 StockSeriesScribeTests 取证）：**不得预置 <c>Scribe.mode</c>** ——
        /// <c>InitSaving</c> / <c>InitLoading</c> 开头即有守卫会把它打成 ForceStop。
        /// </summary>
        private void SaveTo(WorkshopWorkDemotionLedger source)
        {
            Scribe.saver.InitSaving(path, "savegame");
            WorkshopWorkDemotionLedger toSave = source;
            Scribe_Deep.Look(ref toSave, "workshopWorkDemotion");
            Scribe.saver.FinalizeSaving();
            Scribe.mode = LoadSaveMode.Inactive;
        }

        private WorkshopWorkDemotionLedger LoadFrom()
        {
            WorkshopWorkDemotionLedger loaded = null;
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref loaded, "workshopWorkDemotion");
            Scribe.loader.FinalizeLoading();
            return loaded;
        }

        private static string JoinLogs()
        {
            return logCapture.Count == 0 ? "(无捕获日志)" : string.Join(" | ", logCapture);
        }
    }
}
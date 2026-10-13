using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using NUnit.Framework;
using PersonalChronicle.Automation;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Tests
{
    [TestFixture]
    [NonParallelizable]
    public sealed class WorkshopDutyRuntimeTests
    {
        [TestCase("Work", true)]
        [TestCase("Anything", true)]
        [TestCase(null, true)]
        [TestCase("PC_Meal", false)]
        [TestCase("Sleep", false)]
        [TestCase("Joy", false)]
        [TestCase("Meditate", false)]
        [TestCase("PC_Idle", false)]
        public void TimetableOverridesShift(string assignment, bool expected)
        {
            Assert.AreEqual(expected, WorkshopDutyRuntimeRules.CanWork(true, assignment));
            Assert.IsFalse(WorkshopDutyRuntimeRules.CanWork(false, assignment));
        }

        [Test]
        public void MealSleepAndOffShiftStopLongRunningProduction()
        {
            Assert.IsTrue(WorkshopDutyRuntimeRules.ShouldInterrupt(true, false, false, true, "PC_Meal"));
            Assert.IsTrue(WorkshopDutyRuntimeRules.ShouldInterrupt(true, false, false, true, "Sleep"));
            Assert.IsTrue(WorkshopDutyRuntimeRules.ShouldInterrupt(true, false, false, false, "Work"));
            Assert.IsFalse(WorkshopDutyRuntimeRules.ShouldInterrupt(true, false, false, true, "Work"));
        }

        [Test]
        public void BreakNeverInterruptsMealsEmergencyOrPlayerForcedJobs()
        {
            foreach (string assignment in new[] { "PC_Meal", "Sleep", "PC_Idle" })
            {
                Assert.IsFalse(WorkshopDutyRuntimeRules.ShouldInterrupt(false, false, false, true, assignment));
                Assert.IsFalse(WorkshopDutyRuntimeRules.ShouldInterrupt(true, true, false, true, assignment));
                Assert.IsFalse(WorkshopDutyRuntimeRules.ShouldInterrupt(true, false, true, true, assignment));
            }
        }

        [Test]
        public void NightShiftMealThenReturnAndEndAcrossMidnight()
        {
            // 20-08：午夜仍上班；用餐覆盖窗口；餐后回来；08:00 必须下班。
            Assert.IsTrue(WorkshopDutyRuntimeRules.CanWork(WorkshopTime.InRange(23, 20, 8), "Work"));
            Assert.IsFalse(WorkshopDutyRuntimeRules.CanWork(WorkshopTime.InRange(0, 20, 8), "PC_Meal"));
            Assert.IsTrue(WorkshopDutyRuntimeRules.CanWork(WorkshopTime.InRange(1, 20, 8), "Work"));
            Assert.IsFalse(WorkshopDutyRuntimeRules.CanWork(WorkshopTime.InRange(8, 20, 8), "Work"));
        }

        private sealed class DutyProbe : JobDriver_WorkshopDuty
        {
            public List<Toil> BuildToils() => MakeNewToils().ToList();
        }

        [Test]
        public void ActualWaitingDriverCompletesAndReturnsToJobSelection()
        {
            // 直接检查真实 JobDriver 构建的 toil 链，旧 Never 站岗在此会失败。
            // 游戏 SimplePool 调用 Mono 的 Queue.TryDequeue；net48 离线宿主只替换分配器。
            var harmony = new Harmony("personalchronicle.tests.workshoptoilpool");
            try
            {
                harmony.Patch(AccessTools.Method(typeof(ToilMaker), "MakeToil"),
                    prefix: new HarmonyMethod(typeof(WorkshopDutyRuntimeTests), nameof(MakeOfflineToil)));
                var toils = new DutyProbe().BuildToils();
                Assert.AreEqual(2, toils.Count);
                Assert.AreEqual(ToilCompleteMode.Delay, toils[1].defaultCompleteMode);
                Assert.AreEqual(300, toils[1].defaultDuration);
                Assert.IsNotNull(toils[1].initAction);
                Assert.IsNotEmpty(toils[1].preTickActions);
            }
            finally { harmony.UnpatchAll(harmony.Id); }
        }

        private static bool MakeOfflineToil(ref Toil __result)
        {
            __result = new Toil();
            return false;
        }

        private static TimeAssignmentDef offlineAssignment;
        private static bool CurrentAssignmentOffline(ref TimeAssignmentDef __result)
        {
            __result = offlineAssignment;
            return false;
        }
        private static bool NoGameClock(ref TickManager __result)
        {
            __result = null;
            return false;
        }

        [TestCase("Cooking")]
        [TestCase("Research")]
        [TestCase("Hauling")]
        [TestCase("Cleaning")]
        public void ActualWorkWindowGateBlocksUnmappedJobsDuringMealsAndSleep(string workName)
        {
            var harmony = new Harmony("personalchronicle.tests.workshopbreakgate");
            try
            {
                harmony.Patch(AccessTools.PropertyGetter(typeof(Find), "TickManager"),
                    prefix: new HarmonyMethod(typeof(WorkshopDutyRuntimeTests), nameof(NoGameClock)));
                harmony.Patch(AccessTools.PropertyGetter(typeof(Pawn_TimetableTracker), "CurrentAssignment"),
                    prefix: new HarmonyMethod(typeof(WorkshopDutyRuntimeTests), nameof(CurrentAssignmentOffline)));
                var pawn = (Pawn)FormatterServices.GetUninitializedObject(typeof(Pawn));
                pawn.def = (ThingDef)FormatterServices.GetUninitializedObject(typeof(ThingDef));
                pawn.def.defName = "Human";
                pawn.timetable = (Pawn_TimetableTracker)FormatterServices.GetUninitializedObject(typeof(Pawn_TimetableTracker));
                var manager = new MapComponent_WorkshopManager(null);
                var roster = (Dictionary<Pawn, HashSet<string>>)AccessTools.Field(
                    typeof(MapComponent_WorkshopManager), "runtimeWorkTypes").GetValue(manager);
                roster[pawn] = new HashSet<string> { "Cooking", "PC_WorkshopOperation" };
                AccessTools.Field(typeof(MapComponent_WorkshopManager), "runtimeRosterTick").SetValue(manager, 0);
                var type = (WorkTypeDef)FormatterServices.GetUninitializedObject(typeof(WorkTypeDef));
                type.defName = workName;
                foreach (string assignment in new[] { "PC_Meal", "Sleep", "PC_Idle" })
                {
                    offlineAssignment = (TimeAssignmentDef)FormatterServices.GetUninitializedObject(typeof(TimeAssignmentDef));
                    offlineAssignment.defName = assignment;
                    Assert.IsFalse(manager.CanRunScheduledWork(pawn, type), workName + " / " + assignment);
                    type.defName = "Doctor";
                    Assert.IsTrue(manager.CanRunScheduledWork(pawn, type));
                    type.defName = workName;
                }
                roster.Clear(); // 非在编人员的管制不被本系统接管。
                Assert.IsTrue(manager.CanRunScheduledWork(pawn, type));
            }
            finally { harmony.UnpatchAll(harmony.Id); }
        }

        private sealed class DeviceProbe
        {
            public int Distance;
            public bool Executable;
            public object Job = new object();
        }

        [Test]
        public void MaterialArrivalAfterWaitingMakesBillSelectableWithoutOpeningUI()
        {
            var device = new DeviceProbe { Distance = 1, Executable = false };
            var devices = new[] { device };
            Assert.IsNull(WorkshopBillSelection.NearestExecutable(devices, d => d.Distance,
                d => d.Executable ? d.Job : null, j => { }));
            device.Executable = true; // 材料到达 / 设备预约释放，下一轮必须重新求值。
            Assert.AreSame(device.Job, WorkshopBillSelection.NearestExecutable(devices, d => d.Distance,
                d => d.Executable ? d.Job : null, j => { }));
        }

        [Test]
        public void UnusableClosestDeviceDoesNotBlockAnotherDevice()
        {
            var occupied = new DeviceProbe { Distance = 1, Executable = false };
            var ready = new DeviceProbe { Distance = 20, Executable = true };
            Assert.AreSame(ready.Job, WorkshopBillSelection.NearestExecutable(new[] { occupied, ready },
                d => d.Distance, d => d.Executable ? d.Job : null, j => { }));
        }

        [Test]
        public void ReplacedCandidateIsReturnedToPoolAndClosestValidJobWins()
        {
            var far = new DeviceProbe { Distance = 20, Executable = true };
            var near = new DeviceProbe { Distance = 2, Executable = true };
            var discarded = new List<object>();
            Assert.AreSame(near.Job, WorkshopBillSelection.NearestExecutable(new[] { far, near },
                d => d.Distance, d => d.Executable ? d.Job : null, discarded.Add));
            CollectionAssert.AreEqual(new[] { far.Job }, discarded);
        }

        [Test]
        public void RuntimeHooksMatchInstalledGameSignaturesAndCanBePatched()
        {
            var method = AccessTools.Method(typeof(JobGiver_Work), "PawnCanUseWorkGiver");
            Assert.AreEqual(typeof(bool), method.ReturnType);
            CollectionAssert.AreEqual(new[] { typeof(Pawn), typeof(WorkGiver) },
                method.GetParameters().Select(p => p.ParameterType).ToArray());
            var assembly = typeof(JobDriver_WorkshopDuty).Assembly;
            var harmony = new Harmony("personalchronicle.tests.workshopruntime");
            try
            {
                foreach (var pair in new[] {
                    new KeyValuePair<MethodInfo, string>(method, "Patch_WorkshopDutyWorkWindow"),
                    new KeyValuePair<MethodInfo, string>(AccessTools.Method(typeof(WorkGiver_DoBill), "JobOnThing"), "Patch_WorkshopDutyBillTarget") })
                {
                    var patch = assembly.GetType("PersonalChronicle.Automation." + pair.Value);
                    harmony.Patch(pair.Key, prefix: new HarmonyMethod(AccessTools.Method(patch, "Prefix")));
                    Assert.IsTrue(Harmony.GetPatchInfo(pair.Key).Owners.Contains(harmony.Id));
                }
                var start = AccessTools.Method(typeof(Pawn_JobTracker), "StartJob");
                var end = AccessTools.Method(typeof(Pawn_JobTracker), "EndCurrentJob");
                harmony.Patch(start, postfix: new HarmonyMethod(AccessTools.Method(assembly.GetType(
                    "PersonalChronicle.Automation.Patch_WorkshopDutyActualJobStarted"), "Postfix")));
                harmony.Patch(end, prefix: new HarmonyMethod(AccessTools.Method(assembly.GetType(
                    "PersonalChronicle.Automation.Patch_WorkshopDutyActualJobEnded"), "Prefix")));
                Assert.IsTrue(Harmony.GetPatchInfo(start).Owners.Contains(harmony.Id));
                Assert.IsTrue(Harmony.GetPatchInfo(end).Owners.Contains(harmony.Id));
            }
            finally { harmony.UnpatchAll(harmony.Id); }
        }
    }
}

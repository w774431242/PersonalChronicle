using System.Collections.Generic;
using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 倒班例外（调休 / 请假 / 顶班）离线单测 —— Phase 2 的核心交互语义。
    ///
    /// <para>钉死的现实工作流：① 请假 = 让出班次（该班次当天无人，除非有人顶）；
    /// ② 顶班 = 休息组补到"当天没人顶的班次"（通常就是请假让出的那班）；
    /// ③ 本应上班者标"顶班"不生效（不能一组一天上两班）；④ 当天已满员时"顶班"无处可顶（仍休）；
     /// ⑤ 点击循环 正常→请假→顶班→正常。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopCrewExceptionTests
    {
        private const int Crews = 4;
        private const int Shifts = 3;
        private const int Day = 0;

        private static List<int> Kinds(params int[] values)
        {
            return new List<int>(values);
        }

        [Test]
        public void Leave_FreesTheSlot()
        {
            // 四班三运转 day0：甲0 乙1 丙2 丁休
            var kinds = Kinds(WorkshopCrewCycle.ExceptionNone, WorkshopCrewCycle.ExceptionLeave,
                WorkshopCrewCycle.ExceptionNone, WorkshopCrewCycle.ExceptionNone);
            Assert.AreEqual(-1, WorkshopCrewCycle.EffectiveShiftSlotOfCrew(Day, 1, Crews, Shifts, kinds));
            // 乙班让出的"中班"（序位 1）当天没人
            Assert.AreEqual(-1, WorkshopCrewCycle.CrewOfShiftSlot(Day, 1, Crews, Shifts, kinds));
            Assert.AreEqual(1, WorkshopCrewCycle.FirstUnstaffedSlot(Day, Crews, Shifts, kinds));
            Assert.IsFalse(WorkshopCrewCycle.AllSlotsStaffed(Day, Crews, Shifts, kinds));
        }

        [Test]
        public void Cover_FillsTheVacatedSlot()
        {
            // 丙请中班假 + 丁（当天轮休）顶班 ⇒ 中班由丁顶
            var kinds = Kinds(WorkshopCrewCycle.ExceptionNone, WorkshopCrewCycle.ExceptionLeave,
                WorkshopCrewCycle.ExceptionNone, WorkshopCrewCycle.ExceptionExtraWork);
            Assert.AreEqual(1, WorkshopCrewCycle.EffectiveShiftSlotOfCrew(Day, 3, Crews, Shifts, kinds));
            Assert.AreEqual(3, WorkshopCrewCycle.CrewOfShiftSlot(Day, 1, Crews, Shifts, kinds));
            // 注意语义：FirstUnstaffedSlot 是"派工前的空缺查询"（顶班者尚未落位，故仍报 1）；
            // 派工后的审计要看 AllSlotsStaffed。
            Assert.AreEqual(1, WorkshopCrewCycle.FirstUnstaffedSlot(Day, Crews, Shifts, kinds));
            Assert.IsTrue(WorkshopCrewCycle.AllSlotsStaffed(Day, Crews, Shifts, kinds));
        }

        [Test]
        public void Cover_WithoutVacancy_StaysResting()
        {
            // 四班三运转里其余三班都有人 ⇒ 休息组标"顶班"也无处可顶（仍休），避免一天两班
            var kinds = Kinds(WorkshopCrewCycle.ExceptionNone, WorkshopCrewCycle.ExceptionNone,
                WorkshopCrewCycle.ExceptionNone, WorkshopCrewCycle.ExceptionExtraWork);
            Assert.AreEqual(-1, WorkshopCrewCycle.EffectiveShiftSlotOfCrew(Day, 3, Crews, Shifts, kinds));
        }

        [Test]
        public void Cover_OnAWorkingDay_IsNoOp()
        {
            // 甲当天本就上早班；标"顶班"不应变成上第二个班
            var kinds = Kinds(WorkshopCrewCycle.ExceptionExtraWork, WorkshopCrewCycle.ExceptionNone,
                WorkshopCrewCycle.ExceptionNone, WorkshopCrewCycle.ExceptionNone);
            Assert.AreEqual(0, WorkshopCrewCycle.EffectiveShiftSlotOfCrew(Day, 0, Crews, Shifts, kinds));
        }

        [Test]
        public void LeaveAndCover_KeepAllSlotsStaffed()
        {
            var kinds = Kinds(WorkshopCrewCycle.ExceptionLeave, WorkshopCrewCycle.ExceptionNone,
                WorkshopCrewCycle.ExceptionNone, WorkshopCrewCycle.ExceptionExtraWork);
            for (int slot = 0; slot < Shifts; slot++)
            {
                Assert.GreaterOrEqual(
                    WorkshopCrewCycle.CrewOfShiftSlot(Day, slot, Crews, Shifts, kinds), 0,
                    "班次 " + slot + " 应有人顶");
            }
        }

        [Test]
        public void NextException_CyclesNormalLeaveCover()
        {
            int kind = WorkshopCrewCycle.ExceptionNone;
            kind = WorkshopCrewCycle.NextException(kind);
            Assert.AreEqual(WorkshopCrewCycle.ExceptionLeave, kind);
            kind = WorkshopCrewCycle.NextException(kind);
            Assert.AreEqual(WorkshopCrewCycle.ExceptionExtraWork, kind);
            kind = WorkshopCrewCycle.NextException(kind);
            Assert.AreEqual(WorkshopCrewCycle.ExceptionNone, kind);
        }

        [Test]
        public void MissingOrInvalidKinds_AreTreatedAsNone()
        {
            Assert.AreEqual(WorkshopCrewCycle.ExceptionNone,
                WorkshopCrewCycle.ExceptionOf(null, 0));
            Assert.AreEqual(WorkshopCrewCycle.ExceptionNone,
                WorkshopCrewCycle.ExceptionOf(Kinds(99), 0));
            Assert.AreEqual(WorkshopCrewCycle.ExceptionNone,
                WorkshopCrewCycle.ExceptionOf(Kinds(1), 5));
            // 空例外表 = 纯倒班表
            Assert.AreEqual(WorkshopCrewCycle.ShiftSlotOfCrew(Day, 0, Crews, Shifts),
                WorkshopCrewCycle.EffectiveShiftSlotOfCrew(Day, 0, Crews, Shifts, null));
        }
    }
}

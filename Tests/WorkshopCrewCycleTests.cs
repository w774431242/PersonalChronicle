using NUnit.Framework;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Tests
{
    /// <summary>
    /// 倒班循环（<see cref="WorkshopCrewCycle"/>）离线单测 —— 现实制造业制度还原的验收锚点。
    ///
    /// <para>契约：① <b>三班倒</b>（3 班组 × 3 班次）每天各班都有人、每 3 天轮一圈、无人休息；
    /// ② <b>两班倒</b>（2×2）白夜逐日互换；③ <b>四班三运转</b>（4×3）每天恰好 1 个班组休息、
    /// 每班组"3 天工作 + 1 天休息"；④ 班组不足（C&lt;S）时某些班次当天无人（-1）；
     /// ⑤ 周期长度 = max(C,S)；⑥ 相位（anchor）只整体平移，不改变轮转结构。</para>
    /// </summary>
    [TestFixture]
    public sealed class WorkshopCrewCycleTests
    {
        private const int Early = 0;
        private const int Middle = 1;
        private const int Night = 2;

        [Test]
        public void ThreeShift_RotatesEveryDayWithNoRest()
        {
            const int crews = 3;
            const int shifts = 3;
            // day 0: 甲早 乙中 丙夜
            Assert.AreEqual(Early, WorkshopCrewCycle.ShiftSlotOfCrew(0, 0, crews, shifts));
            Assert.AreEqual(Middle, WorkshopCrewCycle.ShiftSlotOfCrew(0, 1, crews, shifts));
            Assert.AreEqual(Night, WorkshopCrewCycle.ShiftSlotOfCrew(0, 2, crews, shifts));
            // day 1: 每人往后一班
            Assert.AreEqual(Middle, WorkshopCrewCycle.ShiftSlotOfCrew(1, 0, crews, shifts));
            Assert.AreEqual(Night, WorkshopCrewCycle.ShiftSlotOfCrew(1, 1, crews, shifts));
            Assert.AreEqual(Early, WorkshopCrewCycle.ShiftSlotOfCrew(1, 2, crews, shifts));
            // 3 天一轮回
            Assert.AreEqual(WorkshopCrewCycle.ShiftSlotOfCrew(0, 1, crews, shifts),
                WorkshopCrewCycle.ShiftSlotOfCrew(3, 1, crews, shifts));
            Assert.AreEqual(0, WorkshopCrewCycle.RestingCrewsPerDay(crews, shifts));
        }

        [Test]
        public void TwoShift_SwapsDayAndNightDaily()
        {
            const int crews = 2;
            const int shifts = 2;
            Assert.AreEqual(0, WorkshopCrewCycle.ShiftSlotOfCrew(0, 0, crews, shifts));
            Assert.AreEqual(1, WorkshopCrewCycle.ShiftSlotOfCrew(0, 1, crews, shifts));
            Assert.AreEqual(1, WorkshopCrewCycle.ShiftSlotOfCrew(1, 0, crews, shifts));
            Assert.AreEqual(0, WorkshopCrewCycle.ShiftSlotOfCrew(1, 1, crews, shifts));
            Assert.AreEqual(0, WorkshopCrewCycle.RestingCrewsPerDay(crews, shifts));
        }

        [Test]
        public void FourCrewThreeShift_OneRestsEveryDay_AndWorksThreeOfFour()
        {
            const int crews = 4;
            const int shifts = 3;
            for (int day = 0; day < 8; day++)
            {
                int resting = 0;
                bool[] slotUsed = new bool[shifts];
                for (int crew = 0; crew < crews; crew++)
                {
                    int slot = WorkshopCrewCycle.ShiftSlotOfCrew(day, crew, crews, shifts);
                    if (slot < 0)
                    {
                        resting++;
                        continue;
                    }
                    Assert.IsFalse(slotUsed[slot], "day " + day + " 班次 " + slot + " 被两个班组占用");
                    slotUsed[slot] = true;
                }
                Assert.AreEqual(1, resting, "day " + day + " 应有且仅有 1 个班组休息");
                Assert.AreEqual(1, WorkshopCrewCycle.RestingCrewsPerDay(crews, shifts));
            }
            // 每班组在 4 天里上 3 天班
            Assert.AreEqual(3, WorkshopCrewCycle.WorkingDaysPerCycle(crews, shifts));
            Assert.AreEqual(4, WorkshopCrewCycle.CycleLengthDays(crews, shifts));
        }

        [Test]
        public void ReverseLookup_CrewOfShiftSlot_IsConsistent()
        {
            const int crews = 4;
            const int shifts = 3;
            for (int day = 0; day < 6; day++)
            {
                for (int slot = 0; slot < shifts; slot++)
                {
                    int crew = WorkshopCrewCycle.CrewOfShiftSlot(day, slot, crews, shifts);
                    Assert.GreaterOrEqual(crew, 0, "每天都应有人上班次 " + slot);
                    Assert.AreEqual(slot, WorkshopCrewCycle.ShiftSlotOfCrew(day, crew, crews, shifts));
                }
            }
        }

        [Test]
        public void UndermannedCrews_LeaveSlotsUnstaffed()
        {
            // 2 个班组撑 3 班：每天只有 2 个班次有人，第 3 班当天无人（-1）。
            const int crews = 2;
            const int shifts = 3;
            int unstaffed = 0;
            for (int slot = 0; slot < shifts; slot++)
            {
                if (WorkshopCrewCycle.CrewOfShiftSlot(0, slot, crews, shifts) < 0)
                {
                    unstaffed++;
                }
            }
            Assert.AreEqual(1, unstaffed);
            // 注意：班组少（C < S）时**没有班组休息**（每班组天天上班），
            // 缺的是"班次没人顶上" ⇒ 休息班组数 = max(0, C − S) = 0。
            Assert.AreEqual(0, WorkshopCrewCycle.RestingCrewsPerDay(crews, shifts));
        }

        [Test]
        public void InvalidInputs_AreSafe()
        {
            Assert.AreEqual(-1, WorkshopCrewCycle.ShiftSlotOfCrew(0, 0, 0, 3));
            Assert.AreEqual(-1, WorkshopCrewCycle.ShiftSlotOfCrew(0, 0, 3, 0));
            Assert.AreEqual(-1, WorkshopCrewCycle.ShiftSlotOfCrew(0, 5, 3, 3));
            Assert.AreEqual(-1, WorkshopCrewCycle.CrewOfShiftSlot(0, 2, 3, 2));
            Assert.AreEqual(-1, WorkshopCrewCycle.CrewOfShiftSlot(0, -1, 3, 3));
            Assert.AreEqual(1, WorkshopCrewCycle.CycleLengthDays(0, 0));
            Assert.IsTrue(WorkshopCrewCycle.IsResting(0, 3, 4, 3));
        }

        [Test]
        public void AnchorPhase_ShiftsTheWholeRoster()
        {
            // 相位语义：调用方传 (day − anchor)。锚点必须是**周期的整数倍**才等价于 day=0
            // （4 天周期的车间，锚点 4 与 0 同相位；锚点 5 等价于 day=1）。
            const int crews = 4;
            const int shifts = 3;
            const int anchor = 4;
            for (int crew = 0; crew < crews; crew++)
            {
                Assert.AreEqual(
                    WorkshopCrewCycle.ShiftSlotOfCrew(0, crew, crews, shifts),
                    WorkshopCrewCycle.ShiftSlotOfCrew(anchor, crew, crews, shifts));
            }
            // 非整周期锚点 = 整体平移 1 天。
            for (int crew = 0; crew < crews; crew++)
            {
                Assert.AreEqual(
                    WorkshopCrewCycle.ShiftSlotOfCrew(1, crew, crews, shifts),
                    WorkshopCrewCycle.ShiftSlotOfCrew(anchor + 1, crew, crews, shifts));
            }
        }
    }
}

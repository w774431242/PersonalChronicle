using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>T1 人工设备。没有自主生产 Tick，也不改变原版工单持久化。</summary>
    public class Building_SmartBench : Building_WorkTable, IWorkshopDevice, IWorkshopDeviceState
    {
        // 仅是界面读数；读档后等到真正作业回调才显示在产，不补造历史。
        private int lastWorkTick = -1;
        public string DeviceLabel => LabelCap;

        public override void UsedThisTick()
        {
            base.UsedThisTick();
            lastWorkTick = Find.TickManager.TicksGame;
        }

        public string StateKeyOrNull()
        {
            CompBreakdownable breakdown = GetComp<CompBreakdownable>();
            CompPowerTrader power = GetComp<CompPowerTrader>();
            bool working = lastWorkTick >= 0 && Find.TickManager != null
                && Find.TickManager.TicksGame - lastWorkTick >= 0
                && Find.TickManager.TicksGame - lastWorkTick <= 1;
            return SmartBenchState.KeyOf(breakdown != null && breakdown.BrokenDown,
                power != null && power.PowerOn, BillStack != null && BillStack.AnyShouldDoNow, working);
        }

        public override string GetInspectString()
        {
            string state = StateKeyOrNull();
            string info = "PersonalChronicle.T1.Manual".Translate() + "\n"
                + "PersonalChronicle.T1.Speed".Translate(this.GetStatValue(StatDefOf.WorkTableWorkSpeedFactor).ToStringPercent())
                + "\n" + "PersonalChronicle.T1.State".Translate(EquipmentStateText.TranslationKeyOf(state).Translate());
            if (state == SmartBenchState.Waiting) info += "\n" + "PersonalChronicle.T1.WaitHint".Translate();
            string original = base.GetInspectString();
            return string.IsNullOrEmpty(original) ? info : original + "\n" + info;
        }
    }

    /// <summary>与原版热工作台同样在人工实际作业时放热；静置热量由原型电力产热 Comp 处理。</summary>
    public sealed class Building_SmartBenchHeated : Building_SmartBench
    {
        public override void UsedThisTick()
        {
            base.UsedThisTick();
            if (this.IsHashIntervalTick(30))
                GenTemperature.PushHeat(this, def.building.heatPerTickWhileWorking * 30f);
        }
    }
}

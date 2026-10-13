using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 「本体功耗 + 光伏抵扣」一体电力参数（PM 2026-09-07 规格定稿：三档统一配光伏与
    /// 应急电池，且<b>发电恒小于功耗</b>——光伏只抵扣、永不成永动机）。
    ///
    /// ⭐ 2026-09-08 实现换血：基类从 CompPowerPlantSolar 改为 <b>CompPowerTrader</b>。
    /// 原因：CompPowerPlant.UpdateDesiredPowerOutput 有 `!PowerOn → PowerOutput = 0f` 门控，
    /// 我们这类「净耗电设备」在电网供不应求时会陷入
    /// PowerOn=false→输出归零→电网盈余→PowerOn=true→重新耗电→电网亏空 的<b>振荡</b>，
    /// 面板表现为「未接入电网」（实机 PM 报告根因）。CompPowerTrader 是电器语义：
    /// 需求恒定声明、由电网决定供不供电，不振荡。
    /// </summary>
    public class CompProperties_PowerTraderWithSolar : CompProperties_Power
    {
        /// <summary>本体恒定功耗（W，光照无关，昼夜一致）。必须大于光伏峰值。</summary>
        public float extraPowerConsumption = 0f;

        public CompProperties_PowerTraderWithSolar()
        {
            compClass = typeof(CompPowerTraderWithSolar);
        }
    }

    /// <summary>
    /// 电器语义的「光伏抵扣」电力组件（继承 CompPowerTrader）：
    /// <b>PowerOutput = 光伏 − 本体功耗</b>（正=向电网供电，负=从电网取电）。
    /// - 白天：光伏 100/400/700W 抵扣后仍为净耗（发电恒小于功耗）；
    /// - 夜间：光伏归零，纯耗本体功耗；
    /// - 需求每 <see cref="SkySampleIntervalTicks"/> 重算一次（光照变化慢，无需每 tick）。
    /// </summary>
    public class CompPowerTraderWithSolar : CompPowerTrader
    {
        /// <summary>光照采样节拍（tick）——与植物 Long ticker 同拍，开销可忽略。</summary>
        private const int SkySampleIntervalTicks = 250;

        private int skyQuota;
        private bool demandInitialized;

        /// <summary>同 Thing 兄弟电力 comp 的 transNet 已对齐（对齐成功后置位，spawn 时复位）。</summary>
        private bool transNetAligned;

        private CompProperties_PowerTraderWithSolar SolarProps => (CompProperties_PowerTraderWithSolar)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            UpdateNetDemand();
            // ⭐ 电网注册是延迟的（PowerNetManager.Notify_TransmitterSpawned 只进
            // delayedActions 队列，之后 tick 才真正建网）——此刻 PowerNet 必为 null，
            // 兄弟 comp 对齐只能在 tick 里做（上一版写在这里从未生效，PM 实测）。
            transNetAligned = false;
        }

        /// <summary>
        /// 同 Thing 多电力 comp 的 transNet 对齐（1.6 反编译定案）：
        /// PowerNetMaker.ContiguousPowerBuildings 每建筑只取 PowerComp（主电力 comp）建网，
        /// 构造器只给传入的 transmitter 赋 transNet → 农机电池 comp（第二个电力 comp）
        /// transNet 恒 null → 检视永远追加「未接入电网。」；而 RegisterAllComponentsOf 已把
        /// 电池收进 net.batteryComps（充放电正常），纯显示层缺链。
        /// 延迟注册就位后补挂一次（幂等、带 flag 停查）；未就位则下拍再试。
        /// </summary>
        private void AlignSiblingTransNet()
        {
            if (transNetAligned || !(parent is ThingWithComps withComps))
            {
                return;
            }
            PowerNet net = PowerNet;
            if (net == null)
            {
                return; // 电网尚未注册完成（延迟建网），下一拍再试。
            }
            foreach (CompPower comp in withComps.GetComps<CompPower>())
            {
                if (comp != this && comp.PowerNet == null)
                {
                    comp.transNet = net;
                }
            }
            transNetAligned = true;
        }

        public override void SetUpPowerVars()
        {
            base.SetUpPowerVars();
            UpdateNetDemand();
        }

        public override void CompTickInterval(int interval)
        {
            base.CompTickInterval(interval);
            AlignSiblingTransNet();
            skyQuota += interval;
            if (skyQuota < SkySampleIntervalTicks && demandInitialized)
            {
                return;
            }
            skyQuota = 0;
            UpdateNetDemand();
        }

        /// <summary>重算净需求：PowerOutput = 光伏 − 本体功耗（原版符号：正=发电，负=耗电）。</summary>
        private void UpdateNetDemand()
        {
            Map map = parent.Map;
            if (map == null)
            {
                return;
            }
            float solar = SolarOutputNow(map);
            PowerOutput = solar - SolarProps.extraPowerConsumption;
            demandInitialized = true;
        }

        /// <summary>当前光伏发电量：峰值(−basePowerConsumption) × 天空亮度 × 露天比例。
        /// 与原版 CompPowerPlantSolar 同公式（屋顶按比例扣减）。</summary>
        private float SolarOutputNow(Map map)
        {
            // PowerConsumption 在 CompProperties_Power 上（负值=发电峰值，原版太阳能板同约定）。
            float configured = SolarProps.PowerConsumption;
            if (configured >= 0f)
            {
                return 0f; // 未配置发电峰值（正=普通电器语义），不发电。
            }
            float peak = -configured;
            float glow = map.skyManager != null ? map.skyManager.CurSkyGlow : 0f;
            return peak * glow * UnroofedFactor(map);
        }

        /// <summary>露天比例：占地格中无屋顶格数 / 总格数（照抄原版 RoofedPowerOutputFactor）。</summary>
        private float UnroofedFactor(Map map)
        {
            int total = 0;
            int roofed = 0;
            foreach (IntVec3 cell in parent.OccupiedRect())
            {
                total++;
                if (map.roofGrid.Roofed(cell))
                {
                    roofed++;
                }
            }
            return total > 0 ? (float)(total - roofed) / (float)total : 0f;
        }
    }
}

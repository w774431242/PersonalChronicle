using System.Collections.Generic;
using RimWorld;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 电网读数的**唯一换算点**（单位口径 + 引擎方法选择）。
    ///
    /// <para><b>⛔ 为什么必须有它（2026-09-13 实机 bug）</b>：<c>PowerNet.CurrentEnergyGainRate()</c>
    /// 的单位是 <b>Wd/tick</b>，<b>不是 W</b> —— 它是 <c>Σ CompPowerTrader.EnergyOutputPerTick</c>，
    /// 而 <c>EnergyOutputPerTick = PowerOutput × CompPower.WattsToWattDaysPerTick</c>（= 1/60000）。
    /// 消费侧若直接当 W 用，<c>Mathf.RoundToInt(0.0166) = 0</c> ⇒ 读数恒为 <c>0W</c>
    /// （实机截图：`电网 ▢▢▢▢ 0W · 0Wd`，用户据此判定"电网数据没接进来"）。</para>
    ///
    /// <para><b>原版口径（照抄，不自创）</b>：<c>CompPower.CompInspectStringExtra</c> 的
    /// <c>"PowerConnectedRateStored"</c> 一行就是
    /// <c>PowerNet.CurrentEnergyGainRate() / WattsToWattDaysPerTick</c> + <c>CurrentStoredEnergy()</c>，
    /// 即「<b>净收支 W</b>（正 = 盈余 / 负 = 缺口）+ <b>已储 Wd</b>」。</para>
    ///
    /// <para><b>为什么放在 Domain</b>：Chrome（顶栏）· DataLayer（⑦ 数据层）· Bridge（数据桥）三处都要这两个数，
    /// 之前各自手写、两处漏换算（同一缺陷三份实现）⇒ 抽成单一真相源，
    /// 且 <c>WattsToWattDaysPerTick</c> 的用法只此一处可查。</para>
    /// </summary>
    internal static class PowerGridReading
    {
        /// <summary>净收支（<b>W</b>）：正 = 盈余、负 = 缺口。换算依据见类注释。</summary>
        internal static float NetWatts(PowerNet net)
        {
            return net == null ? 0f : net.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
        }

        /// <summary>
        /// 已储能量（<b>Wd</b>）。用引擎方法而不是自己遍历电池：
        /// <c>CurrentStoredEnergy()</c> 会**排除被 EMP 眩晕的电池**（自己遍历会多算）。
        /// </summary>
        internal static float StoredWattDays(PowerNet net)
        {
            return net == null ? 0f : net.CurrentStoredEnergy();
        }

        /// <summary>
        /// 储能满容量（<b>Wd</b>）= Σ(已储 + 还可充)。原版没有"总容量"字段 ⇒ 用它反推"水位"。
        /// 无电池 = 0（调用方据此走"无储能"降级，不要画成 0% 水位）。
        /// </summary>
        internal static float CapacityWattDays(PowerNet net)
        {
            List<CompPowerBattery> batteries = net != null ? net.batteryComps : null;
            if (batteries == null)
            {
                return 0f;
            }
            float capacity = 0f;
            for (int i = 0; i < batteries.Count; i++)
            {
                CompPowerBattery battery = batteries[i];
                if (battery == null)
                {
                    continue;
                }
                capacity += battery.StoredEnergy + battery.AmountCanAccept;
            }
            return capacity;
        }
    }
}

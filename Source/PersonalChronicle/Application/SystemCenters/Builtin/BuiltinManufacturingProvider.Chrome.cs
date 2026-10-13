using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Application.SystemCenters.Builtin
{
    /// <summary>
    /// 制造系统中心**页面外壳**（预览 `#top` 顶栏 + `#ticker` 底栏）—— 数据构建部分。
    ///
    /// <para>口径：</para>
    /// <list type="bullet">
    /// <item><b>游戏时钟</b>：<c>GenDate.Year / DayOfQuadrum / HourOfDay</c>（真实 tide 值，非样例）。</item>
    /// <item><b>电网</b>：净收支（W）+ 储能水位（Wd）—— <b>单位换算与引擎方法一律走
    /// <see cref="PowerGridReading"/></b>（`CurrentEnergyGainRate()` 是 <b>Wd/tick</b>，
    /// 直接当 W 用会恒为 0，实机 `0W · 0Wd` 即此）。水位 = 已储 / Σ(已储 + 还可充) ——
    /// 原版没有"总容量"字段，用**电池充填度**表示水位；**无电池 ⇒ 走"无储能"降级，不画 0% 水位**。</item>
    /// <item><b>底栏广播</b>：由**真实厂况**拼一行（在产 / 故障 / 在制工单 / 成品在库），
    /// 对应预览 `#tkmove` 的滚动播报。</item>
    /// </list>
    /// </summary>
    public sealed partial class BuiltinManufacturingProvider : IManufacturingChromeDataProvider
    {
        public ManufacturingChromeSnapshot BuildChrome(Map map)
        {
            ManufacturingChromeSnapshot snapshot = new ManufacturingChromeSnapshot();
            try
            {
                snapshot.Brand = T("PersonalChronicle.UI.Chrome.Brand");
                snapshot.PowerLabel = T("PersonalChronicle.UI.Chrome.PowerLabel");
                FillClock(snapshot);
                if (map != null)
                {
                    FillPower(snapshot, map);
                    FillTicker(snapshot, map);
                }
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Provider, "manufacturing chrome failed: " + ex);
                snapshot.IsEmpty = true;
            }
            return snapshot;
        }

        private static void FillClock(ManufacturingChromeSnapshot snapshot)
        {
            TickManager ticks = Find.TickManager;
            if (ticks == null)
            {
                return;
            }
            long now = ticks.TicksGame;
            try
            {
                int year = GenDate.Year(now, 0f);
                int day = GenDate.DayOfQuadrum(now, 0f) + 1;
                float hour = GenDate.HourFloat(now, 0f);
                snapshot.DateText = T2("PersonalChronicle.UI.Chrome.Date", year, day);
                int h = Mathf.Clamp((int)hour, 0, 23);
                int m = Mathf.Clamp((int)((hour - h) * 60f), 0, 59);
                snapshot.TimeText = h.ToString("00", CultureInfo.InvariantCulture)
                    + ":" + m.ToString("00", CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                snapshot.DateText = string.Empty;
                snapshot.TimeText = string.Empty;
            }
        }

        /// <summary>电网：净收支（W）+ 电池充填度（水位条）。</summary>
        private static void FillPower(ManufacturingChromeSnapshot snapshot, Map map)
        {
            List<PowerNet> nets = null;
            try
            {
                if (map.powerNetManager != null)
                {
                    nets = map.powerNetManager.AllNetsListForReading;
                }
            }
            catch (Exception)
            {
                nets = null;
            }
            if (nets == null)
            {
                return;
            }

            float netRate = 0f;
            float stored = 0f;
            float capacity = 0f;
            for (int i = 0; i < nets.Count; i++)
            {
                PowerNet net = nets[i];
                if (net == null)
                {
                    continue;
                }
                try
                {
                    // 口径与单位统一走 Domain（**唯一换算点**）：
                    // `CurrentEnergyGainRate()` 是 Wd/tick 不是 W —— 三处调用点此前各自手写、两处漏换算
                    // ⇒ 实机读数恒为 0（用户报「电网数据没接进来」）。详见 PowerGridReading 类注释。
                    netRate += PowerGridReading.NetWatts(net);
                    stored += PowerGridReading.StoredWattDays(net);
                    capacity += PowerGridReading.CapacityWattDays(net);
                }
                catch (Exception)
                {
                    // 单网失败不影响其余电网汇总
                }
            }

            snapshot.PowerText = T2("PersonalChronicle.UI.Chrome.PowerValue",
                Mathf.RoundToInt(netRate), Mathf.RoundToInt(stored));
            snapshot.PowerShare01 = capacity > 1f ? Mathf.Clamp01(stored / capacity) : 0f;
        }

        /// <summary>底栏广播：真实厂况一行（在产 / 故障 / 在制工单 / 成品在库）。</summary>
        private static void FillTicker(ManufacturingChromeSnapshot snapshot, Map map)
        {
            int devices = 0;
            int broken = 0;
            int bills = 0;
            List<Building> buildings = map.listerBuildings != null
                ? map.listerBuildings.allBuildingsColonist
                : null;
            if (buildings != null)
            {
                for (int i = 0; i < buildings.Count; i++)
                {
                    IBillGiver giver = buildings[i] as IBillGiver;
                    if (giver == null)
                    {
                        continue;
                    }
                    devices++;
                    CompBreakdownable breakdown = buildings[i].TryGetComp<CompBreakdownable>();
                    if (breakdown != null && breakdown.BrokenDown)
                    {
                        broken++;
                    }
                    BillStack stack = giver.BillStack;
                    if (stack != null)
                    {
                        bills += stack.Count;
                    }
                }
            }

            int depot = CountDepotItems(map);
            snapshot.TickerText = T4("PersonalChronicle.UI.Chrome.Ticker", devices, broken, bills, depot);
        }

        private static string T4(string key, int arg0, int arg1, int arg2, int arg3)
        {
            return key.Translate(arg0, arg1, arg2, arg3).ToString();
        }
    }
}

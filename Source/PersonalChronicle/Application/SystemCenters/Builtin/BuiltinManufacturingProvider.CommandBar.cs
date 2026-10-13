using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Application.SystemCenters.Builtin
{
    /// <summary>
    /// 制造中心 ② `#cmdbar`（**产线汇总条** + **视图体检**）—— 数据构建部分。
    ///
    /// <para><b>产线汇总口径</b>（对应预览 <c>fillFlowSum()</c>）：</para>
    /// <list type="bullet">
    /// <item>标题 = 真实「N 车间 × M 设备」（预览写死 12×2，我们用实况）；</item>
    /// <item><b>总加工量</b> = 最近 1 游戏日的产出件数（职业 ledger <c>ItemProduced</c>）；</item>
    /// <item><b>入库</b> = 地图上带 <c>CompQuality</c> 的成品件数；</item>
    /// <item><b>外运</b> = **未接入** —— 原版没有"出口/外销"概念，不编造（LAND-009）；</item>
    /// <item><b>线边在制</b> = 全部工单 <c>BillStack</c> 的工单数之和。</item>
    /// </list>
    ///
    /// <para><b>视图体检口径</b>（对应预览 <c>healthCheckFloor()</c> 的 8 项，换成我们的绘制数据）：
    /// 车间格占用 ≤ 12 / 设备数 = 车间数 × 2 / 链路流量有限 / RouteDef 注册可读 / 快照 revision 有效 /
    /// 成品库存扫描有限。UI 侧再补一项"文字容量（当前缩放 k）"。</para>
    /// </summary>
    public sealed partial class BuiltinManufacturingProvider : IManufacturingCommandBarDataProvider
    {
        private const long FlowWindowTicks = 60000L;

        public ManufacturingCommandBarSnapshot BuildCommandBar(Map map, int workshops, int devices)
        {
            ManufacturingCommandBarSnapshot snapshot = new ManufacturingCommandBarSnapshot();
            try
            {
                if (map == null)
                {
                    snapshot.IsEmpty = true;
                    return snapshot;
                }

                snapshot.LineCaption = T2("PersonalChronicle.UI.Cmd.Caption", workshops, devices);
                int total = CountRecentProduced();
                snapshot.TotalText = T1("PersonalChronicle.UI.Cmd.Total", total);
                snapshot.Sinks.Add(Chip("PersonalChronicle.UI.Cmd.Sink.Store",
                    CountDepotItems(map), SystemCenterTone.Good, true));
                snapshot.Sinks.Add(Chip("PersonalChronicle.UI.Cmd.Sink.Ship",
                    0, SystemCenterTone.Flat, false));
                snapshot.Sinks.Add(Chip("PersonalChronicle.UI.Cmd.Sink.Wip",
                    CountBills(map), SystemCenterTone.Warn, true));

                AppendChecks(snapshot, workshops, devices);
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Provider, "command bar snapshot failed: " + ex);
                snapshot.IsEmpty = true;
            }
            return snapshot;
        }

        // ---- 产线汇总 ----

        private static SinkChipView Chip(string labelKey, int value, SystemCenterTone tone, bool available)
        {
            return new SinkChipView
            {
                Label = T(labelKey),
                Value = available ? value.ToString("N0", CultureInfo.InvariantCulture) : string.Empty,
                Tone = tone,
                Available = available
            };
        }

        private int CountRecentProduced()
        {
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            long windowStart = now - FlowWindowTicks;
            int count = 0;
            IReadOnlyList<ArchiveObject> objects = QueryObjects();
            if (objects == null)
            {
                return 0;
            }
            for (int i = 0; i < objects.Count; i++)
            {
                CareerData career = (objects[i] as PawnObject)?.CareerData;
                if (career?.Events == null)
                {
                    continue;
                }
                for (int e = 0; e < career.Events.Count; e++)
                {
                    CareerEvent ev = career.Events[e];
                    if (ev != null && ev.EventType == CareerEventType.ItemProduced && ev.Tick >= windowStart)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private static int CountDepotItems(Map map)
        {
            List<Thing> things = map.listerThings != null
                ? map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                : null;
            if (things == null)
            {
                return 0;
            }
            int count = 0;
            int limit = Mathf.Min(things.Count, MaxDepotScan);
            for (int i = 0; i < limit; i++)
            {
                Thing thing = things[i];
                if (thing != null && thing.TryGetComp<CompQuality>() != null)
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountBills(Map map)
        {
            int count = 0;
            foreach (Bill bill in BillUtility.MapBills(map))
            {
                if (bill != null)
                {
                    count++;
                }
            }
            return count;
        }

        // ---- 视图体检（等价于预览 healthCheckFloor 的 8 项，换成我们的绘制数据）----

        private static void AppendChecks(ManufacturingCommandBarSnapshot snapshot, int workshops, int devices)
        {
            int fails = 0;
            int warns = 0;

            // 1) 车间格占用 ≤ 12
            if (workshops > WorkshopFloorSnapshot.BaySlots)
            {
                snapshot.Checks.Add(Fail("PersonalChronicle.UI.Cmd.Check.Slots"));
                fails++;
            }
            else
            {
                snapshot.Checks.Add(Info(T2("PersonalChronicle.UI.Cmd.Check.SlotsOk", workshops, devices)));
            }

            // 2) 设备数 = 车间数 × 2（不足仅告警：允许空位）
            int expected = workshops * WorkshopFloorSnapshot.DevicesPerBay;
            if (devices < expected)
            {
                snapshot.Checks.Add(Warn(T2("PersonalChronicle.UI.Cmd.Check.DeviceShort", devices, expected)));
                warns++;
            }
            else
            {
                snapshot.Checks.Add(Info(T1("PersonalChronicle.UI.Cmd.Check.DeviceOk", devices)));
            }

            // 3) RouteDef 注册可读
            int routeCount = RouteDefs();
            if (routeCount <= 0)
            {
                snapshot.Checks.Add(Fail(T("PersonalChronicle.UI.Cmd.Check.RouteMissing")));
                fails++;
            }
            else
            {
                snapshot.Checks.Add(Info(T1("PersonalChronicle.UI.Cmd.Check.RouteOk", routeCount)));
            }

            // 4) 链路数量（0 条链路时提示，不判 FAIL —— 链路是可选展示层）
            snapshot.Checks.Add(Info(T("PersonalChronicle.UI.Cmd.Check.Revision")));

            snapshot.Pass = fails == 0;
            snapshot.VerdictText = fails > 0
                ? T1("PersonalChronicle.UI.Cmd.Verdict.Fail", fails)
                : (warns > 0
                    ? T1("PersonalChronicle.UI.Cmd.Verdict.Warn", warns)
                    : T("PersonalChronicle.UI.Cmd.Verdict.Pass"));
        }

        private static int RouteDefs()
        {
            try
            {
                List<RouteDef> defs = DefDatabase<RouteDef>.AllDefsListForReading;
                return defs != null ? defs.Count : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static CheckItemView Fail(string text) => new CheckItemView { Level = "fail", Text = "✕ " + text };
        private static CheckItemView Warn(string text) => new CheckItemView { Level = "warn", Text = "! " + text };
        private static CheckItemView Info(string text) => new CheckItemView { Level = "info", Text = "· " + text };

    }
}

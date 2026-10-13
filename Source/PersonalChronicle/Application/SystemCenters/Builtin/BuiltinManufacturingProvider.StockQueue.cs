using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.ReadModels;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Application.SystemCenters.Builtin
{
    /// <summary>
    /// 制造中心 ② `#rightcol`（生产队列 + 原料仓）—— 数据构建部分。
    ///
    /// <para><b>口径（全部原版可读，且都是只读）</b></para>
    /// <list type="bullet">
    /// <item><b>生产队列</b>：枚举殖民者可接工单建筑的 <c>BillStack</c>，逐单列出「设备 · 配方」。
    /// 数量列 = <c>Bill_Production.repeatCount</c>（**剩余次数**；Forever 模式不递减，副标已注明），
    /// 挂起单标「暂停」。**不做加单/改优先级** —— 那会与原版 JobDriver 争抢写入权（玩法面，需单独评估）。</item>
    /// <item><b>原料仓</b>：原版 <c>ResourceCounter</c> 的全部计数资源，条 = **相对水位**（以库存最多者为 100%），
    /// 30% 安全线以下标红 + LOW。原版没有"目标库容"概念，故不编造 cap（副标写明口径）。</item>
    /// </list>
    /// </summary>
    public sealed partial class BuiltinManufacturingProvider : IManufacturingStockQueueDataProvider
    {
        private const int MaxQueueRows = 8;
        private const int MaxRawRows = 8;
        private const float LowWaterLine = 0.30f;

        public ManufacturingStockQueueSnapshot BuildStockQueue(Map map)
        {
            ManufacturingStockQueueSnapshot snapshot = new ManufacturingStockQueueSnapshot();
            try
            {
                if (map == null)
                {
                    snapshot.IsEmpty = true;
                    return snapshot;
                }
                AppendQueue(snapshot, map);
                AppendRawStock(snapshot, map);
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Provider, "stock/queue snapshot failed: " + ex);
                snapshot.IsEmpty = true;
            }
            return snapshot;
        }

        private static void AppendQueue(ManufacturingStockQueueSnapshot snapshot, Map map)
        {
            snapshot.QueueSub = T("PersonalChronicle.UI.Queue.Sub");
            List<Building> buildings = map.listerBuildings != null
                ? map.listerBuildings.allBuildingsColonist
                : null;
            if (buildings == null)
            {
                return;
            }
            for (int i = 0; i < buildings.Count && snapshot.Queue.Count < MaxQueueRows; i++)
            {
                IBillGiver giver = buildings[i] as IBillGiver;
                BillStack stack = giver?.BillStack;
                if (stack == null || stack.Count == 0)
                {
                    continue;
                }
                string machine = buildings[i].def != null
                    ? (string.IsNullOrEmpty(buildings[i].def.label)
                        ? buildings[i].def.defName
                        : buildings[i].def.label)
                    : "?";
                for (int b = 0; b < stack.Count && snapshot.Queue.Count < MaxQueueRows; b++)
                {
                    Bill bill = stack[b];
                    if (bill?.recipe == null)
                    {
                        continue;
                    }
                    string recipe = string.IsNullOrEmpty(bill.recipe.label)
                        ? bill.recipe.defName
                        : bill.recipe.label;
                    Bill_Production production = bill as Bill_Production;
                    snapshot.Queue.Add(new WorkOrderRowView
                    {
                        Machine = machine,
                        Recipe = recipe,
                        AmountText = production != null
                            ? T1("PersonalChronicle.UI.Queue.Times", production.repeatCount)
                            : string.Empty,
                        StateText = bill.suspended
                            ? T("PersonalChronicle.UI.Queue.Suspended")
                            : string.Empty,
                        Suspended = bill.suspended
                    });
                }
            }
        }

        private static void AppendRawStock(ManufacturingStockQueueSnapshot snapshot, Map map)
        {
            snapshot.RawSub = T("PersonalChronicle.UI.Raw.Sub");
            ResourceCounter counter = map.resourceCounter;
            if (counter == null)
            {
                return;
            }

            Dictionary<ThingDef, int> amounts;
            try
            {
                amounts = counter.AllCountedAmounts;
            }
            catch (Exception)
            {
                return;
            }
            if (amounts == null || amounts.Count == 0)
            {
                return;
            }

            List<KeyValuePair<ThingDef, int>> rows = new List<KeyValuePair<ThingDef, int>>();
            foreach (KeyValuePair<ThingDef, int> pair in amounts)
            {
                if (pair.Key != null && pair.Value > 0)
                {
                    rows.Add(pair);
                }
            }
            rows.Sort((a, b) => b.Value.CompareTo(a.Value));
            if (rows.Count == 0)
            {
                return;
            }

            int top = rows[0].Value;
            int take = Mathf.Min(MaxRawRows, rows.Count);
            for (int i = 0; i < take; i++)
            {
                float level = top > 0 ? rows[i].Value / (float)top : 0f;
                snapshot.RawStock.Add(new RawStockRowView
                {
                    Name = rows[i].Key.label.NullOrEmpty() ? rows[i].Key.defName : rows[i].Key.label,
                    CountText = rows[i].Value.ToString("N0", CultureInfo.InvariantCulture),
                    Level01 = Mathf.Clamp01(level),
                    Low = level < LowWaterLine
                });
            }
        }

    }
}

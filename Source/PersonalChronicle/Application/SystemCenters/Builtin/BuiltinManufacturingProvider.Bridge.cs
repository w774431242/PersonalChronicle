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
    /// 制造中心 ⑥ 数据桥（6 面板）—— 数据构建部分。按 PM 决策走**混合**：
    ///
    /// <para><b>真接 3 块</b>（原版数据现成）</para>
    /// <list type="bullet">
    /// <item><b>生产链路看板</b>（预览标 <c>DataBridge·BillStack</c>）：逐台设备的工单（设备 · 配方 · 剩余 · 暂停）；</item>
    /// <item><b>能源拓扑</b>（<c>DataBridge·PowerNet</c>）：每个电网的能量收支速率与已储能量；</item>
    /// <item><b>质量 SPC · Pareto</b>（<c>Charts</c>）：职业 ledger 的 7 档品质分布，按**件数降序 + 累计占比**（Pareto 口径）。</item>
    /// </list>
    ///
    /// <para><b>照实占位 3 块</b>：工单排程甘特（需排程时间轴）/ 数字孪生俯视（与 ② 重复）/
    /// 维护计划·备件（原版无维护计划）—— 画「待接入」并写明落地路径，不用假数据填充。</para>
    /// </summary>
    public sealed partial class BuiltinManufacturingProvider : IManufacturingBridgeDataProvider
    {
        private const int MaxFlowRows = 12;
        private const int MaxPowerRows = 6;

        public ManufacturingBridgeSnapshot BuildBridge(Map map)
        {
            ManufacturingBridgeSnapshot snapshot = new ManufacturingBridgeSnapshot();
            try
            {
                snapshot.Panels.Add(BuildProductionFlowPanel(map));
                snapshot.Panels.Add(BuildPowerPanel(map));
                snapshot.Panels.Add(BuildQualityPanel());
                snapshot.Panels.Add(PendingPanel(
                    "PersonalChronicle.UI.Bridge.Gantt.Title",
                    "PersonalChronicle.UI.Bridge.Gantt.Stencil",
                    "PersonalChronicle.UI.Bridge.Gantt.Note", 6));
                snapshot.Panels.Add(PendingPanel(
                    "PersonalChronicle.UI.Bridge.Twin.Title",
                    "PersonalChronicle.UI.Bridge.Twin.Stencil",
                    "PersonalChronicle.UI.Bridge.Twin.Note", 3));
                snapshot.Panels.Add(PendingPanel(
                    "PersonalChronicle.UI.Bridge.Tpm.Title",
                    "PersonalChronicle.UI.Bridge.Tpm.Stencil",
                    "PersonalChronicle.UI.Bridge.Tpm.Note", 3));
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Provider, "bridge snapshot failed: " + ex);
                snapshot.IsEmpty = true;
            }
            return snapshot;
        }

        /// <summary>面板一：生产链路看板（逐台设备的工单 = BillStack 的真实投影）。</summary>
        private static BridgePanelView BuildProductionFlowPanel(Map map)
        {
            BridgePanelView panel = new BridgePanelView
            {
                Title = T("PersonalChronicle.UI.Bridge.Flow.Title"),
                Stencil = T("PersonalChronicle.UI.Bridge.Flow.Stencil"),
                Sub = T("PersonalChronicle.UI.Bridge.Flow.Sub"),
                Weight = 4
            };
            panel.Headers.Add(T("PersonalChronicle.UI.Bridge.Col.Machine"));
            panel.Headers.Add(T("PersonalChronicle.UI.Bridge.Col.Recipe"));
            panel.Headers.Add(T("PersonalChronicle.UI.Bridge.Col.Amount"));
            panel.Headers.Add(T("PersonalChronicle.UI.Bridge.Col.State"));

            if (map?.listerBuildings == null)
            {
                return panel;
            }
            List<Building> buildings = map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < buildings.Count && panel.Rows.Count < MaxFlowRows; i++)
            {
                IBillGiver giver = buildings[i] as IBillGiver;
                BillStack stack = giver?.BillStack;
                if (stack == null || stack.Count == 0)
                {
                    continue;
                }
                string machine = LabelOf(buildings[i].def);
                for (int b = 0; b < stack.Count && panel.Rows.Count < MaxFlowRows; b++)
                {
                    Bill bill = stack[b];
                    if (bill?.recipe == null)
                    {
                        continue;
                    }
                    Bill_Production production = bill as Bill_Production;
                    BridgeRowView row = new BridgeRowView
                    {
                        Tone = bill.suspended ? SystemCenterTone.Warn : SystemCenterTone.Flat
                    };
                    row.Cells.Add(machine);
                    row.Cells.Add(string.IsNullOrEmpty(bill.recipe.label) ? bill.recipe.defName : bill.recipe.label);
                    row.Cells.Add(production != null
                        ? production.repeatCount.ToString(CultureInfo.InvariantCulture)
                        : "—");
                    row.Cells.Add(bill.suspended
                        ? T("PersonalChronicle.UI.Queue.Suspended")
                        : T("PersonalChronicle.UI.Bridge.State.Running"));
                    panel.Rows.Add(row);
                }
            }
            return panel;
        }

        /// <summary>面板二：能源拓扑（每个电网的收支速率与储能）。</summary>
        private static BridgePanelView BuildPowerPanel(Map map)
        {
            BridgePanelView panel = new BridgePanelView
            {
                Title = T("PersonalChronicle.UI.Bridge.Power.Title"),
                Stencil = T("PersonalChronicle.UI.Bridge.Power.Stencil"),
                Sub = T("PersonalChronicle.UI.Bridge.Power.Sub"),
                Weight = 4
            };
            panel.Headers.Add(T("PersonalChronicle.UI.Bridge.Col.Grid"));
            panel.Headers.Add(T("PersonalChronicle.UI.Bridge.Col.Gain"));
            panel.Headers.Add(T("PersonalChronicle.UI.Bridge.Col.Stored"));

            List<PowerNet> nets = null;
            try
            {
                if (map?.powerNetManager != null)
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
                return panel;
            }
            for (int i = 0; i < nets.Count && panel.Rows.Count < MaxPowerRows; i++)
            {
                PowerNet net = nets[i];
                if (net == null)
                {
                    continue;
                }
                // 单位走 Domain 单一真相源：`CurrentEnergyGainRate()` 是 **Wd/tick** 不是 W
                //（此前直接当 W 用 ⇒ 恒 0，实机 `0W · 0Wd`）。Tone 用符号判定，换算前后等价。
                float gain = PowerGridReading.NetWatts(net);
                float stored = PowerGridReading.StoredWattDays(net);
                BridgeRowView row = new BridgeRowView
                {
                    Tone = gain >= 0f ? SystemCenterTone.Good : SystemCenterTone.Warn
                };
                row.Cells.Add("#" + (i + 1).ToString(CultureInfo.InvariantCulture));
                row.Cells.Add(Mathf.RoundToInt(gain).ToString(CultureInfo.InvariantCulture) + "W");
                row.Cells.Add(Mathf.RoundToInt(stored).ToString(CultureInfo.InvariantCulture) + "Wd");
                panel.Rows.Add(row);
            }
            return panel;
        }

        private BridgePanelView BuildQualityPanel()
        {
            BridgePanelView panel = new BridgePanelView
            {
                Title = T("PersonalChronicle.UI.Bridge.Quality.Title"),
                Stencil = T("PersonalChronicle.UI.Bridge.Quality.Stencil"),
                Sub = T("PersonalChronicle.UI.Bridge.Quality.Sub"),
                Weight = 4
            };
            panel.Headers.Add(T("PersonalChronicle.UI.Bridge.Col.Quality"));
            panel.Headers.Add(T("PersonalChronicle.UI.Bridge.Col.Count"));
            panel.Headers.Add(T("PersonalChronicle.UI.Bridge.Col.Cumulative"));

            int[] counts = new int[7];
            int total = 0;
            IReadOnlyList<ArchiveObject> objects = QueryObjects();
            if (objects != null)
            {
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
                        if (ev == null || ev.EventType != CareerEventType.ItemProduced)
                        {
                            continue;
                        }
                        int rank = Domain.Qualification.ExamScoring.QualityRank(ev.Quality);
                        if (rank < 0 || rank > 6)
                        {
                            continue;
                        }
                        counts[rank]++;
                        total++;
                    }
                }
            }
            if (total == 0)
            {
                return panel;
            }

            int max = 1;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > max)
                {
                    max = counts[i];
                }
            }

            // Pareto：件数降序 + 累计占比
            List<int> ranks = new List<int>();
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > 0)
                {
                    ranks.Add(i);
                }
            }
            ranks.Sort((a, b) => counts[b].CompareTo(counts[a]));
            int cumulative = 0;
            for (int i = 0; i < ranks.Count; i++)
            {
                int rank = ranks[i];
                cumulative += counts[rank];
                BridgeRowView row = new BridgeRowView
                {
                    Bar01 = counts[rank] / (float)max,
                    Tone = rank >= (int)QualityCategory.Good ? SystemCenterTone.Good : SystemCenterTone.Warn
                };
                row.Cells.Add(((QualityCategory)rank).GetLabel());
                row.Cells.Add(counts[rank].ToString("N0", CultureInfo.InvariantCulture));
                row.Cells.Add(Mathf.RoundToInt(cumulative * 100f / total).ToString(CultureInfo.InvariantCulture) + "%");
                panel.Rows.Add(row);
            }
            return panel;
        }

        private static BridgePanelView PendingPanel(string titleKey, string stencilKey, string noteKey, int weight)
        {
            return new BridgePanelView
            {
                Title = T(titleKey),
                Stencil = T(stencilKey),
                Pending = true,
                PendingNote = T(noteKey),
                Weight = weight
            };
        }

    }
}

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
    /// 制造中心 ③ 分析带（瓶颈 / 六大损失 / 吞吐趋势 / 在制 WIP）—— 数据构建部分。
    ///
    /// <para><b>口径（全部可实证，禁止拿预览样例值凑数）</b></para>
    /// <list type="bullet">
    /// <item><b>瓶颈带</b>：每台"有工单"设备的**额定工量** = Σ <c>recipe.WorkAmountTotal</c>；
    /// 占比 = 该台 / 全部；最长段 = 瓶颈。预览用"节拍(s/件)"，原版没有节拍概念，故用**额定工量**替代
    /// （单位是"工"，面板副标与 tooltip 注明）。</item>
    /// <item><b>六大损失</b>：原版只有 3 项有出口 —— 停机/空转（= 待命设备数）、故障（= 故障设备数）、
    /// 次品（= 有品质产出里 rank &lt; 良好 的累计）；**待料 / 性能减速 / 换型启动 三项无出口，
    /// 画成「未接入」而不是 0**（LAND-009）。</item>
    /// <item><b>吞吐趋势</b>：从职业 ledger 的 <c>ItemProduced.Tick</c> 按最近 24 游戏小时（60000 tick）
    /// 分 6 桶；无记录 → <see cref="ManufacturingAnalysisSnapshot.TrendAvailable"/> = false（不画假线）。</item>
    /// <item><b>WIP</b>：在制工单（<c>BillUtility.MapBills</c>）、在产设备（Run 态）、成品累计（ledger 有品质产出）、
    /// 待料设备（未接入）。</item>
    /// </list>
    /// </summary>
    public sealed partial class BuiltinManufacturingProvider
    {
        /// <summary>瓶颈带最多画几段（预览约 12 段）。</summary>
        private const int MaxBottleneckSegments = 12;

        /// <summary>趋势窗口 = 最近 1 游戏日（RimWorld：1 日 = 60000 tick）。</summary>
        private const long TrendWindowTicks = 60000L;

        /// <summary>趋势分桶数。</summary>
        private const int TrendBuckets = 6;

        public ManufacturingAnalysisSnapshot BuildAnalysis(Map map)
        {
            ManufacturingAnalysisSnapshot snapshot = new ManufacturingAnalysisSnapshot();
            try
            {
                if (map == null)
                {
                    snapshot.IsEmpty = true;
                    return snapshot;
                }

                int idle = 0;
                int fault = 0;
                int running = 0;
                List<string> segmentNames = new List<string>();
                List<double> segmentWorks = new List<double>();
                double totalWork = 0d;

                List<Building> buildings = map.listerBuildings != null
                    ? map.listerBuildings.allBuildingsColonist
                    : null;
                if (buildings != null)
                {
                    for (int i = 0; i < buildings.Count; i++)
                    {
                        Building building = buildings[i];
                        IBillGiver giver = building as IBillGiver;
                        if (giver == null)
                        {
                            continue;
                        }

                        BillStack stack = giver.BillStack;
                        CompBreakdownable breakdown = building.TryGetComp<CompBreakdownable>();
                        CompPowerTrader power = building.TryGetComp<CompPowerTrader>();
                        EquipmentState state = EquipmentStateResolver.Resolve(
                            breakdown != null && breakdown.BrokenDown,
                            power == null || power.PowerOn,
                            stack != null && stack.AnyShouldDoNow);
                        if (state == EquipmentState.Run)
                        {
                            running++;
                        }
                        else if (state == EquipmentState.Fault)
                        {
                            fault++;
                        }
                        else if (state == EquipmentState.Idle)
                        {
                            idle++;
                        }

                        if (stack == null)
                        {
                            continue;
                        }
                        double work = 0d;
                        for (int b = 0; b < stack.Count; b++)
                        {
                            Bill bill = stack[b];
                            RecipeDef recipe = bill?.recipe;
                            if (recipe != null)
                            {
                                work += recipe.WorkAmountTotal(null);
                            }
                        }
                        if (work <= 0d)
                        {
                            continue;
                        }
                        segmentNames.Add(LabelOf(building.def));
                        segmentWorks.Add(work);
                        totalWork += work;
                    }
                }

                AppendBottlenecks(snapshot, segmentNames, segmentWorks, totalWork);
                AppendLosses(snapshot, idle, fault);
                AppendTrend(snapshot);
                AppendWip(snapshot, map, running);
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Provider, "manufacturing analysis failed: " + ex);
                snapshot.IsEmpty = true;
            }
            return snapshot;
        }

        /// <summary>瓶颈带：按额定工量降序取前 N 段，最长者为瓶颈（其段标红由 UI 按 IsBottleneck 决定）。</summary>
        private static void AppendBottlenecks(
            ManufacturingAnalysisSnapshot snapshot, List<string> names, List<double> works, double totalWork)
        {
            int count = names.Count;
            if (count == 0 || totalWork <= 0d)
            {
                return;
            }

            List<int> order = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                order.Add(i);
            }
            order.Sort((a, b) => works[b].CompareTo(works[a]));

            int take = Mathf.Min(MaxBottleneckSegments, count);
            for (int k = 0; k < take; k++)
            {
                int i = order[k];
                int work = Mathf.RoundToInt((float)works[i]);
                snapshot.Bottlenecks.Add(new AnalysisBottleneckView
                {
                    Name = names[i],
                    ValueText = T1("PersonalChronicle.UI.Analysis.Bottleneck.Value", work),
                    Ratio01 = Mathf.Clamp((float)(works[i] / totalWork), 0.02f, 1f),
                    IsBottleneck = k == 0
                });
            }

            int first = order[0];
            snapshot.BottleneckSummary = T2("PersonalChronicle.UI.Analysis.Bottleneck.Summary",
                names[first], Mathf.RoundToInt((float)works[first]));
        }

        /// <summary>六大损失：6 行固定，只填原版有出口的 3 项。</summary>
        private void AppendLosses(ManufacturingAnalysisSnapshot snapshot, int idle, int fault)
        {
            CollectProducedQuality(out int qualityTotal, out int qualityGood);
            int defect = Mathf.Max(0, qualityTotal - qualityGood);

            string[] labelKeys =
            {
                "PersonalChronicle.UI.Analysis.Loss.Down",
                "PersonalChronicle.UI.Analysis.Loss.Starve",
                "PersonalChronicle.UI.Analysis.Loss.Fault",
                "PersonalChronicle.UI.Analysis.Loss.Slow",
                "PersonalChronicle.UI.Analysis.Loss.Change",
                "PersonalChronicle.UI.Analysis.Loss.Defect"
            };
            int[] values = { idle, 0, fault, 0, 0, defect };
            bool[] available =
            {
                true,
                false,                       // 待料：原版无出口
                true,
                false,                       // 性能减速：原版无出口
                false,                       // 换型/启动：原版无出口
                qualityTotal > 0
            };
            SystemCenterTone[] tones =
            {
                SystemCenterTone.Flat,
                SystemCenterTone.Warn,
                SystemCenterTone.Bad,
                SystemCenterTone.Flat,
                SystemCenterTone.Flat,
                SystemCenterTone.Warn
            };

            int max = 1;
            for (int i = 0; i < values.Length; i++)
            {
                if (available[i] && values[i] > max)
                {
                    max = values[i];
                }
            }

            int unavailable = 0;
            for (int i = 0; i < labelKeys.Length; i++)
            {
                if (!available[i])
                {
                    unavailable++;
                }
                snapshot.Losses.Add(new AnalysisLossView
                {
                    Label = T(labelKeys[i]),
                    ValueText = available[i] ? values[i].ToString(CultureInfo.InvariantCulture) : string.Empty,
                    Ratio01 = available[i] ? values[i] / (float)max : 0f,
                    Tone = tones[i],
                    Available = available[i]
                });
            }
            snapshot.LossUnavailableCount = unavailable;
        }

        /// <summary>吞吐趋势：最近 1 游戏日按 6 桶统计产出件数（数据源 = 职业 ledger 的 ItemProduced.Tick）。</summary>
        private void AppendTrend(ManufacturingAnalysisSnapshot snapshot)
        {
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            long windowStart = now - TrendWindowTicks;
            int[] buckets = new int[TrendBuckets];
            int inWindow = 0;

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
                        if (ev == null || ev.EventType != CareerEventType.ItemProduced || ev.Tick < windowStart)
                        {
                            continue;
                        }
                        int bucket = Mathf.Clamp(
                            (int)((ev.Tick - windowStart) * TrendBuckets / TrendWindowTicks),
                            0, TrendBuckets - 1);
                        buckets[bucket]++;
                        inWindow++;
                    }
                }
            }

            snapshot.TrendAvailable = inWindow > 0;
            int max = 1;
            for (int i = 0; i < buckets.Length; i++)
            {
                if (buckets[i] > max)
                {
                    max = buckets[i];
                }
            }
            for (int i = 0; i < buckets.Length; i++)
            {
                snapshot.Trend.Add(buckets[i] / (float)max);
            }
            snapshot.TrendNowText = T1("PersonalChronicle.UI.Analysis.Trend.Sub", inWindow);
        }

        /// <summary>在制 WIP：在制工单 / 在产设备 / 待料（未接入）/ 成品累计。</summary>
        private void AppendWip(ManufacturingAnalysisSnapshot snapshot, Map map, int running)
        {
            int bills = 0;
            foreach (Bill bill in BillUtility.MapBills(map))
            {
                if (bill != null)
                {
                    bills++;
                }
            }
            CollectProducedQuality(out int qualityTotal, out _);

            snapshot.Wip.Add(new AnalysisWipView
            {
                Label = T("PersonalChronicle.UI.Analysis.Wip.Jobs"),
                Value = bills.ToString(CultureInfo.InvariantCulture)
            });
            snapshot.Wip.Add(new AnalysisWipView
            {
                Label = T("PersonalChronicle.UI.Analysis.Wip.Running"),
                Value = running.ToString(CultureInfo.InvariantCulture)
            });
            snapshot.Wip.Add(new AnalysisWipView
            {
                Label = T("PersonalChronicle.UI.Analysis.Wip.Starved"),
                Available = false
            });
            snapshot.Wip.Add(new AnalysisWipView
            {
                Label = T("PersonalChronicle.UI.Analysis.Wip.Produced"),
                Value = qualityTotal.ToString("N0", CultureInfo.InvariantCulture)
            });
        }

        private static string LabelOf(ThingDef def)
        {
            return def == null ? "?" : (string.IsNullOrEmpty(def.label) ? def.defName : def.label);
        }

        /// <summary>档案查询面：只看 Pawn 类对象（职业 ledger 挂在 PawnObject 上）。失败返回 null（失败隔离）。</summary>
        private IReadOnlyList<ArchiveObject> QueryObjects()
        {
            if (queries == null)
            {
                return null;
            }
            try
            {
                return queries.GetObjectsOfCategory(ArchiveCategoryKeys.Pawn);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Provider, "analysis ledger query failed: " + ex.Message);
                return null;
            }
        }

        private static string T1(string key, int arg)
        {
            return key.Translate(arg).ToString();
        }

        /// <summary>T2(key, string, int) —— 与 KPI 分册的 T2(int,int) 同名的不同重载。</summary>
        private static string T2(string key, string arg0, int arg1)
        {
            return key.Translate(arg0, arg1).ToString();
        }
    }
}

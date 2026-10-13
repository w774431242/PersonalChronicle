using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Api;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Application.SystemCenters;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间俯视（制造中心 ② 主视图）数据源实现 —— **本模块自带**（车间区是 Automation 的资产）。
    ///
    /// <para>契约在 Application（<see cref="IWorkshopViewDataProvider"/>），实现在此处并在组合根按
    /// 中心 ID 注册：依赖方向保持 <c>Automation → Application</c>，不产生反向边（ARC-001）。
    /// 车间归属判定走本模块公开入口 <see cref="WorkshopZoneUtility"/>，不自己遍历 zoneManager（GOV-005）。</para>
    ///
    /// <para>数据口径（全部原版现状，LAND-009 不用样例值）：</para>
    /// <list type="bullet">
    /// <item>格 = 真实 <see cref="Zone_Workshop"/>（按包围盒行优先排序，稳定可复现）；</item>
    /// <item>设备 = 位置落在该车间内的殖民者可接工单建筑（<c>IBillGiver</c>），每格至多 2 台；</item>
    /// <item>状态 = <see cref="EquipmentStateResolver"/> 四态派生（缺料态原版无出口，M2 补）；</item>
    /// <item>进度 = <c>JobDriver_DoBill.workLeft / RecipeDef.WorkAmountTotal</c> —— **近似**
    /// （分子受技能/品质修正，分母是额定工时），无人在做则不画进度条；</item>
    /// <item>人员 = <c>Zone_Workshop.Members</c>（已随原版 Zone 存档）。</item>
    /// </list>
    /// </summary>
    public sealed partial class WorkshopFloorViewProvider : IWorkshopViewDataProvider
    {
        /// <summary>每格最多显示的成员圆点（与 <c>WorkshopFloorLayout.MaxWorkersPerBay</c> 一致）。</summary>
        private const int MaxWorkersPerBay = 4;

        /// <summary>工序流量窗口 = 最近 1 游戏日（60000 tick），与 ③ 的吞吐趋势同口径。</summary>
        private const long FlowWindowTicks = 60000L;

        private readonly IArchiveQueryService queries;

        public WorkshopFloorViewProvider(IArchiveQueryService queries)
        {
            this.queries = queries;
        }

        public WorkshopFloorSnapshot BuildWorkshopView(Map map)
        {
            WorkshopFloorSnapshot snapshot = new WorkshopFloorSnapshot();
            try
            {
                if (map == null)
                {
                    snapshot.NoMap = true;
                    snapshot.IsEmpty = true;
                    snapshot.Hint = T("PersonalChronicle.UI.Floor.NoMap");
                    return snapshot;
                }

                List<Zone_Workshop> workshops = CollectWorkshops(map);
                List<IBillGiver> benches = CollectBenches(map);
                // 读数读取器与自由画布设备卡共用（Domain 单一实现）
                Dictionary<Thing, Pawn> activeWorkers = WorkshopDeviceReader.CollectActiveWorkers(map);

                snapshot.WorkshopCount = workshops.Count;
                for (int slot = 0; slot < WorkshopFloorSnapshot.BaySlots; slot++)
                {
                    Zone_Workshop zone = slot < workshops.Count ? workshops[slot] : null;
                    snapshot.Bays.Add(BuildBay(snapshot, slot, zone, benches, activeWorkers, map));
                }

                snapshot.DeviceCount = snapshot.Devices.Count;
                snapshot.Caption = T2("PersonalChronicle.UI.Floor.Caption",
                    snapshot.WorkshopCount, snapshot.DeviceCount);

                // 两侧轨道（预览 #rail-left / #rail-right）：原料库 / 成品库
                FillRails(snapshot, map, benches);

                // 工序链路（预览 buildRouteLayer）：按 RouteDef 的格序 + 真实流量/在制
                FillRoutes(snapshot, map, workshops, benches);
                if (snapshot.WorkshopCount == 0)
                {
                    snapshot.Hint = T("PersonalChronicle.UI.Floor.NoWorkshop");
                }
                else if (snapshot.DeviceCount == 0)
                {
                    snapshot.Hint = T("PersonalChronicle.UI.Floor.NoDevice");
                }
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Ui, "Workshop floor snapshot failed: " + ex);

                // 🔴 2026-09-19 审计修复：旧实现**只写 Hint**，把失败前已追加的半截数据
                // （Bays / Devices 等）原样留在快照里 ⇒ 会画出「有设备、但没有轨道/链路」的
                // 残缺视图。现在**清干净**，并给出诚实的失败提示。
                //
                // ⚠ 有意**不置 NoMap**：UI 的门控是 `!workshop.NoMap`
                //（MasterControlTabWindow.Detail.cs:149 / :303 / :381）——
                // 置 true 会让整块车间视图**无声消失**，玩家只看到制造中心少了一块，
                // 不知道发生了什么。清空 + Hint 则画出框架与失败提示，符合 LAND-009
                // 「框架与数据无关，没数据也照画框架 + 一行提示」。
                snapshot.Bays.Clear();
                snapshot.Devices.Clear();
                snapshot.Workers.Clear();
                snapshot.RawItems.Clear();
                snapshot.Routes.Clear();
                snapshot.Depot = new FloorDepotView();
                snapshot.WorkshopCount = 0;
                snapshot.DeviceCount = 0;
                snapshot.Caption = string.Empty;
                snapshot.IsEmpty = true;
                snapshot.Hint = T("PersonalChronicle.UI.Floor.ReadFailed");
            }
            return snapshot;
        }

        private static FloorBayView BuildBay(
            WorkshopFloorSnapshot snapshot, int slot, Zone_Workshop zone,
            List<IBillGiver> benches, Dictionary<Thing, Pawn> activeWorkers, Map map)
        {
            FloorBayView bay = new FloorBayView
            {
                Slot = slot,
                Occupied = zone != null,
                Number = (slot + 1).ToString(CultureInfo.InvariantCulture)
            };

            if (zone == null)
            {
                bay.Name = T("PersonalChronicle.UI.Floor.EmptySlot");
                bay.Meta = string.Empty;
                return bay;
            }

            bay.Name = string.IsNullOrEmpty(zone.label) ? zone.GetType().Name : zone.label;
            bay.TypeLabel = WorkshopTypeLabel(zone);

            int placed = 0;
            for (int i = 0; i < benches.Count && placed < WorkshopFloorSnapshot.DevicesPerBay; i++)
            {
                IBillGiver bench = benches[i];
                Thing thing = bench as Thing;
                if (thing == null || !thing.Spawned)
                {
                    continue;
                }
                // 归属判定唯一入口：不自己遍历 zoneManager（WorkshopZoneUtility 的既定契约）。
                if (!BenchBelongsToBay(thing, zone, map))
                {
                    continue;
                }
                snapshot.Devices.Add(BuildDevice(slot, placed, thing, activeWorkers));
                placed++;
            }

            bay.Meta = T1("PersonalChronicle.UI.Floor.BayMeta", placed);
            bay.Highlight = placed > 0;

            List<Pawn> members = zone.Members;
            if (members != null)
            {
                int index = 0;
                for (int i = 0; i < members.Count && index < MaxWorkersPerBay; i++)
                {
                    Pawn member = members[i];
                    if (member == null || member.Dead)
                    {
                        continue;
                    }
                    string label = member.LabelShort;
                    snapshot.Workers.Add(new FloorWorkerView
                    {
                        Slot = slot,
                        Index = index,
                        Initial = string.IsNullOrEmpty(label) ? "?" : label.Substring(0, 1)
                    });
                    index++;
                }
            }
            return bay;
        }

        private static FloorDeviceView BuildDevice(
            int slot, int index, Thing thing, Dictionary<Thing, Pawn> activeWorkers)
        {
            activeWorkers.TryGetValue(thing, out Pawn operatorPawn);
            // 读数（状态 / 功率 / 工单 / 作业者 / 进度）统一走 Domain 读取器 ——
            // 自由画布的设备卡共用同一实现（此前两份实现，画布那份缺 3 项、功率口径已漂移）。
            WorkshopDeviceReading reading = WorkshopDeviceReader.Read(thing, operatorPawn);

            string defLabel = thing.def != null
                ? (string.IsNullOrEmpty(thing.def.label) ? thing.def.defName : thing.def.label)
                : "?";
            string subText = reading.BillCount > 0
                ? T1("PersonalChronicle.UI.Floor.BillCount", reading.BillCount)
                : T("PersonalChronicle.UI.Floor.NoBill");

            FloorDeviceView device = new FloorDeviceView
            {
                // 稳定 ID（自由画布的设备卡靠它回指本视图 ⇒ 点击弹详情）
                StableId = thing.def.defName + ":" + thing.thingIDNumber.ToString(CultureInfo.InvariantCulture),
                Slot = slot,
                Index = index,
                Card = ProductionDeviceCards.Build(thing),
                Name = defLabel + (index == 0 ? " A" : " B"),
                StateKey = reading.StateKey,
                StateText = T(EquipmentStateText.TranslationKeyOf(reading.StateKey)),
                PowerText = reading.PowerText,
                SubText = subText,
                OperatorText = reading.OperatorText,
                Progress01 = reading.Progress01
            };
            // 悬停提示构建期拼好（绘制路径零分配，PERF-002）。
            device.Tooltip = device.Name
                + (device.Card == null ? string.Empty : "\n" + device.Card.Tooltip)
                + "\n" + device.StateText
                + (string.IsNullOrEmpty(reading.PowerText) ? string.Empty : " · " + reading.PowerText)
                + " · " + subText
                + (string.IsNullOrEmpty(device.OperatorText)
                    ? string.Empty
                    : "\n" + device.OperatorText)
                + (reading.Progress01 >= 0f
                    ? "\n" + T("PersonalChronicle.UI.Floor.ProgressNote")
                    : string.Empty);
            return device;
        }

        // 近似进度（`JobDriver_DoBill.workLeft ÷ RecipeDef.WorkAmountTotal`）已迁入
        // `Domain/WorkshopDeviceReader.ResolveProgress` —— 与自由画布设备卡共用同一实现（2026-09-13 去重）。

        private static List<Zone_Workshop> CollectWorkshops(Map map)
        {
            List<Zone_Workshop> list = new List<Zone_Workshop>();
            foreach (Zone_Workshop zone in WorkshopZoneUtility.AllWorkshops(map))
            {
                if (zone != null)
                {
                    list.Add(zone);
                }
            }
            // 行优先（先上后下、先左后右）：顺序稳定可复现，玩家一眼能把格与地图对上。
            list.Sort(CompareByPosition);
            return list;
        }

        private static int CompareByPosition(Zone_Workshop a, Zone_Workshop b)
        {
            GetTopLeft(a, out int ax, out int az);
            GetTopLeft(b, out int bx, out int bz);
            int byRow = az.CompareTo(bz);
            return byRow != 0 ? byRow : ax.CompareTo(bx);
        }

        private static void GetTopLeft(Zone_Workshop zone, out int minX, out int minZ)
        {
            minX = int.MaxValue;
            minZ = int.MaxValue;
            List<IntVec3> cells = zone.Cells;
            if (cells != null)
            {
                for (int i = 0; i < cells.Count; i++)
                {
                    IntVec3 cell = cells[i];
                    if (cell.x < minX)
                    {
                        minX = cell.x;
                    }
                    if (cell.z < minZ)
                    {
                        minZ = cell.z;
                    }
                }
            }
            if (minX == int.MaxValue)
            {
                minX = 0;
                minZ = 0;
            }
        }

        private static List<IBillGiver> CollectBenches(Map map)
        {
            List<IBillGiver> benches = new List<IBillGiver>();
            List<Building> buildings = map.listerBuildings != null
                ? map.listerBuildings.allBuildingsColonist
                : null;
            if (buildings == null)
            {
                return benches;
            }
            for (int i = 0; i < buildings.Count; i++)
            {
                IBillGiver giver = buildings[i] as IBillGiver;
                if (giver != null)
                {
                    benches.Add(giver);
                }
            }
            return benches;
        }

        // 「正在执行 DoBill 的殖民者：设备 → 人」已迁入 `Domain/WorkshopDeviceReader.CollectActiveWorkers`
        // —— 与自由画布设备卡共用同一实现（2026-09-13 去重）。

        /// <summary>车间类型标签（蓝图 WorkshopTypeDef）；未选类型返回空串（UI 画「—」）。</summary>
        private static string WorkshopTypeLabel(Zone_Workshop zone)
        {
            try
            {
                WorkshopTypeDef typeDef = zone.WorkshopType;
                if (typeDef != null)
                {
                    return string.IsNullOrEmpty(typeDef.label) ? typeDef.defName : typeDef.label;
                }
            }
            catch (Exception)
            {
                // 旧档 / 未选类型：回落空串，不阻断快照。
            }
            return string.Empty;
        }

        // ---- 工序链路（预览 buildRouteLayer）----

        /// <summary>设备是否属于该车间格（归属判定唯一入口，与 <see cref="BuildBay"/> 共用）。</summary>
        private static bool BenchBelongsToBay(Thing thing, Zone_Workshop zone, Map map)
        {
            return thing != null && thing.Spawned && WorkshopZoneUtility.WorkshopAt(thing.Position, map) == zone;
        }

        /// <summary>
        /// 工序链路：按 <see cref="RouteDef"/> 的格序生成工序序列，并填**真实数据**：
        /// <list type="bullet">
        /// <item><b>段流量</b> = 该工序涉及的 recipe 在**最近 1 游戏日**的产出件数（职业 ledger 的
        /// <c>ItemProduced.RecipeDefName</c> 归属到格）；</item>
        /// <item><b>线边在制</b> = 该格设备的 <c>BillStack.Count</c>（原版没有"段间 WIP"，
        /// 用该工序排队中的工单数作代理，tooltip 注明）。</item>
        /// </list>
        /// </summary>
        private void FillRoutes(WorkshopFloorSnapshot snapshot, Map map,
            List<Zone_Workshop> workshops, List<IBillGiver> benches)
        {
            List<RouteDef> defs = DefDatabase<RouteDef>.AllDefsListForReading;
            if (defs == null || defs.Count == 0)
            {
                return;
            }

            int slots = WorkshopFloorSnapshot.BaySlots;
            int[] billsBySlot = new int[slots];
            Dictionary<int, HashSet<string>> recipesBySlot = new Dictionary<int, HashSet<string>>();

            for (int slot = 0; slot < slots && slot < workshops.Count; slot++)
            {
                Zone_Workshop zone = workshops[slot];
                HashSet<string> recipes = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < benches.Count; i++)
                {
                    Thing thing = benches[i] as Thing;
                    if (!BenchBelongsToBay(thing, zone, map))
                    {
                        continue;
                    }
                    BillStack stack = benches[i].BillStack;
                    if (stack == null)
                    {
                        continue;
                    }
                    billsBySlot[slot] += stack.Count;
                    for (int b = 0; b < stack.Count; b++)
                    {
                        RecipeDef recipe = stack[b]?.recipe;
                        if (recipe != null)
                        {
                            recipes.Add(recipe.defName);
                        }
                    }
                }
                recipesBySlot[slot] = recipes;
            }

            int[] flowBySlot = new int[slots];
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            long windowStart = now - FlowWindowTicks;
            IReadOnlyList<ArchiveObject> objects = QueryLedger();
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
                        for (int slot = 0; slot < slots; slot++)
                        {
                            if (recipesBySlot.TryGetValue(slot, out HashSet<string> set)
                                && set.Count > 0 && set.Contains(ev.RecipeDefName))
                            {
                                flowBySlot[slot]++;
                                break;
                            }
                        }
                    }
                }
            }

            int maxFlow = 1;
            for (int slot = 0; slot < slots; slot++)
            {
                if (flowBySlot[slot] > maxFlow)
                {
                    maxFlow = flowBySlot[slot];
                }
            }

            for (int d = 0; d < defs.Count; d++)
            {
                RouteDef def = defs[d];
                if (def?.steps == null || def.steps.Count < 2)
                {
                    continue;
                }
                FloorRouteView route = new FloorRouteView
                {
                    Name = LabelOf(def),
                    NameEn = def.nameEn,
                    CycleSeconds = def.cycleSeconds <= 0.1f ? 6f : def.cycleSeconds,
                    HasFeed = def.feedFromRawStore
                };

                for (int k = 0; k < def.steps.Count; k++)
                {
                    int slot = Mathf.Clamp(def.steps[k], 0, slots - 1);
                    route.Steps.Add(new FloorRouteStepView
                    {
                        Slot = slot,
                        StepText = (k + 1).ToString(CultureInfo.InvariantCulture),
                        FlowCount = flowBySlot[slot],
                        FlowRatio01 = Mathf.Clamp01(flowBySlot[slot] / (float)maxFlow),
                        WipCount = billsBySlot[slot]
                    });
                }

                int lastSlot = route.Steps[route.Steps.Count - 1].Slot;
                route.EndFlowCount = flowBySlot[lastSlot];
                route.EndText = T1("PersonalChronicle.UI.Floor.Route.End", route.EndFlowCount);
                snapshot.Routes.Add(route);
            }
        }

        private IReadOnlyList<ArchiveObject> QueryLedger()
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
                ChronicleLog.Warning(ChronicleLog.Category.Provider, "route flow query failed: " + ex.Message);
                return null;
            }
        }

        private static string LabelOf(RouteDef def)
        {
            return string.IsNullOrEmpty(def.label) ? def.defName : def.label;
        }

        // ---- 两侧轨道（预览 #rail-left 原料库 / #rail-right 成品库）----

        /// <summary>左轨原料条数上限（轨道高固定，多了按"最紧张优先"截断）。</summary>
        private const int MaxRawRows = 6;

        /// <summary>右轨 Top 成品条数上限（预览取前 8）。</summary>
        private const int MaxDepotRows = 6;

        /// <summary>成品库存扫描的物品数上限（防超大殖民地卡顿；超出部分按前 N 计）。</summary>
        private const int MaxDepotScan = 6000;

        /// <summary>
        /// 填充两侧轨道。
        ///
        /// <para><b>左轨（原料库）口径</b>：列出**当前工单涉及的原料**，条 = 库存 / 单次开单需求，
        /// 库存不足一单即标红。预览用固定 <c>cap</c> 与 30% 安全线（样例数据），
        /// 我们改用"单次需求"这个可实证的分母，并在行内写明口径。</para>
        ///
        /// <para><b>右轨（成品库）口径</b>：地图上**带 <c>CompQuality</c> 的可搬运物品**（即手工程度成品）
        /// 按 def 计数取 Top N，并按 <c>QualityCategory</c> 给品质分布（原版 <c>GetLabel()</c> 本地化）。</para>
        /// </summary>
        private static void FillRails(WorkshopFloorSnapshot snapshot, Map map, List<IBillGiver> benches)
        {
            FillRawRail(snapshot, map, benches);
            // 成品库按**本车间**范围统计（见 FillDepotRail 注释）
            FillDepotRail(snapshot, map);
        }

        private static void FillRawRail(WorkshopFloorSnapshot snapshot, Map map, List<IBillGiver> benches)
        {
            if (map.resourceCounter == null)
            {
                return;
            }

            // 1) 工单原料 → 单次需求总量（同一 def 被多个工单用到就累加）
            Dictionary<ThingDef, int> demand = new Dictionary<ThingDef, int>();
            for (int i = 0; i < benches.Count; i++)
            {
                BillStack stack = benches[i].BillStack;
                if (stack == null)
                {
                    continue;
                }
                for (int b = 0; b < stack.Count; b++)
                {
                    Bill bill = stack[b];
                    RecipeDef recipe = bill?.recipe;
                    if (recipe?.ingredients == null)
                    {
                        continue;
                    }
                    for (int g = 0; g < recipe.ingredients.Count; g++)
                    {
                        IngredientCount ingredient = recipe.ingredients[g];
                        if (ingredient?.filter == null)
                        {
                            continue;
                        }
                        int amount = Mathf.Max(1, Mathf.CeilToInt(ingredient.GetBaseCount()));
                        int guard = 0;
                        foreach (ThingDef def in ingredient.filter.AllowedThingDefs)
                        {
                            if (def == null)
                            {
                                continue;
                            }
                            if (++guard > 120)
                            {
                                break; // 宽泛过滤器（如"任意食材"）只取前 N 个，避免全库遍历
                            }
                            demand[def] = (demand.TryGetValue(def, out int current) ? current : 0) + amount;
                        }
                    }
                }
            }
            if (demand.Count == 0)
            {
                return;
            }

            // 2) 组装 + 按"紧张度"升序（库存/需求 越低越靠前）
            List<KeyValuePair<ThingDef, int>> rows = new List<KeyValuePair<ThingDef, int>>(demand.Count);
            foreach (KeyValuePair<ThingDef, int> pair in demand)
            {
                rows.Add(pair);
            }
            rows.Sort((a, b) =>
            {
                float ra = map.resourceCounter.GetCount(a.Key) / (float)Mathf.Max(1, a.Value);
                float rb = map.resourceCounter.GetCount(b.Key) / (float)Mathf.Max(1, b.Value);
                return ra.CompareTo(rb);
            });

            // 3) 需求分档（预览 #rail-left 的 .rabc 角标；2026-10-06 补齐）：
            //    按**单次需求**降序累计占比 → A ≤ 50% · B ≤ 80% · C 其余。
            //    与预览 railAbc() 同法，口径差：预览按"全部配方需求"（静态演示），本工程按"当前工单需求"
            //    （可动态变化）——差异写在轨道 Floor.Raw.AbcTip 的悬停提示里（LAND-009 不编造）。
            List<KeyValuePair<ThingDef, int>> byDemand = new List<KeyValuePair<ThingDef, int>>(rows);
            byDemand.Sort((a, b) => b.Value.CompareTo(a.Value));
            long totalDemand = 0L;
            for (int i = 0; i < byDemand.Count; i++)
            {
                totalDemand += byDemand[i].Value;
            }
            Dictionary<ThingDef, string> tierOf = new Dictionary<ThingDef, string>(byDemand.Count);
            long accumulated = 0L;
            for (int i = 0; i < byDemand.Count; i++)
            {
                accumulated += byDemand[i].Value;
                float share = totalDemand <= 0L ? 1f : accumulated / (float)totalDemand;
                tierOf[byDemand[i].Key] = share <= 0.5f ? "A" : (share <= 0.8f ? "B" : "C");
            }

            int take = Mathf.Min(MaxRawRows, rows.Count);
            for (int i = 0; i < take; i++)
            {
                ThingDef def = rows[i].Key;
                int need = Mathf.Max(1, rows[i].Value);
                int have = map.resourceCounter.GetCount(def);
                snapshot.RawItems.Add(new FloorRawItemView
                {
                    Name = LabelOf(def),
                    // LOW 前缀（新版预览 `⚠LOW`）：口径 = 库存 < 单次开单需求（分母写在 stencil 里）
                    CountText = (have < need ? "⚠ " : string.Empty)
                        + have.ToString("N0", CultureInfo.InvariantCulture),
                    Note = T1("PersonalChronicle.UI.Floor.Raw.Demand", need),
                    Ratio01 = Mathf.Clamp01(have / (float)need),
                    Low = have < need,
                    TierKey = tierOf.TryGetValue(def, out string tier) ? tier : string.Empty
                });
            }
        }

        private static void FillDepotRail(WorkshopFloorSnapshot snapshot, Map map)
        {
            Dictionary<string, int> byName = new Dictionary<string, int>();
            Dictionary<QualityCategory, int> byQuality = new Dictionary<QualityCategory, int>();
            int total = 0;

            // **识别范围 = 本车间内**（实机反馈"成品库识别问题"的根因）：
            // 早前这里扫的是**全地图**所有带 `CompQuality` 的可搬运物 ⇒ 把殖民地任意角落的
            // 带品质物品都当成"本车间成品"，与右轨标题（成品库）语义不符。
            // 现在先取车间 zone 的格集合，再按物品位置过滤；zone 不可用时如实退回全地图口径
            // （此时轨道 stencil 会写明"全地图"，见 Keyed `Floor.Depot.Stencil`）。
            // 本快照是**整张地图**级（聚合所有车间），故"本车间"= 落在**任一车间 zone 范围内**。
            HashSet<IntVec3> cells = new HashSet<IntVec3>();
            try
            {
                System.Collections.Generic.IEnumerable<Zone_Workshop> workshops =
                    WorkshopZoneUtility.AllWorkshops(map);
                if (workshops != null)
                {
                    foreach (Zone_Workshop workshop in workshops)
                    {
                        if (workshop == null || workshop.Cells == null)
                        {
                            continue;
                        }
                        foreach (IntVec3 cell in workshop.Cells)
                        {
                            cells.Add(cell);
                        }
                    }
                }
            }
            catch (Exception)
            {
                cells.Clear();   // 取车间范围失败 ⇒ 退回全地图口径（stencil 会写明范围）
            }
            bool scoped = cells.Count > 0;

            List<Thing> things = map.listerThings != null
                ? map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                : null;
            if (things != null)
            {
                int limit = Mathf.Min(things.Count, MaxDepotScan);
                for (int i = 0; i < limit; i++)
                {
                    Thing thing = things[i];
                    if (thing == null)
                    {
                        continue;
                    }
                    if (scoped && !cells.Contains(thing.PositionHeld))
                    {
                        continue;   // 不在本车间内 ⇒ 不属于本车间成品库
                    }
                    CompQuality quality = thing.TryGetComp<CompQuality>();
                    if (quality == null)
                    {
                        continue;
                    }
                    int count = Mathf.Max(1, thing.stackCount);
                    total += count;
                    string name = LabelOf(thing.def);
                    byName[name] = (byName.TryGetValue(name, out int currentName) ? currentName : 0) + count;
                    byQuality[quality.Quality] =
                        (byQuality.TryGetValue(quality.Quality, out int currentQuality) ? currentQuality : 0) + count;
                }
            }

            snapshot.Depot.TotalText = T1("PersonalChronicle.UI.Floor.Depot.Total", total);
            snapshot.Depot.Empty = total == 0;
            if (total == 0)
            {
                return;
            }

            List<KeyValuePair<string, int>> names = new List<KeyValuePair<string, int>>(byName);
            names.Sort((a, b) => b.Value.CompareTo(a.Value));
            int take = Mathf.Min(MaxDepotRows, names.Count);
            for (int i = 0; i < take; i++)
            {
                snapshot.Depot.TopItems.Add(names[i].Key + "  "
                    + names[i].Value.ToString("N0", CultureInfo.InvariantCulture));
            }

            List<QualityCategory> qualities = new List<QualityCategory>(byQuality.Keys);
            qualities.Sort((a, b) => byQuality[b].CompareTo(byQuality[a]));
            for (int i = 0; i < qualities.Count; i++)
            {
                snapshot.Depot.QualityChips.Add(qualities[i].GetLabel() + " "
                    + byQuality[qualities[i]].ToString("N0", CultureInfo.InvariantCulture));
            }
        }

        private static string LabelOf(ThingDef def)
        {
            return def == null ? "?" : (string.IsNullOrEmpty(def.label) ? def.defName : def.label);
        }

        private static string T(string key)
        {
            return key.Translate().ToString();
        }

        private static string T1(string key, int arg)
        {
            return key.Translate(arg).ToString();
        }

        private static string T2(string key, int arg0, int arg1)
        {
            return key.Translate(arg0, arg1).ToString();
        }
    }
}

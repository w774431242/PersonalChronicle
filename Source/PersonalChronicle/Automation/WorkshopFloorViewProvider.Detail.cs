using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Application;
using PersonalChronicle.Application.Effects;
using PersonalChronicle.Application.Workshop;   // 批次3：WorkshopOpsAccess / WorkshopOpsDigest
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// Partial of <see cref="WorkshopFloorViewProvider"/> —— **设备详情弹窗**（对齐预览 <c>#drawer</c>）。
    ///
    /// <para><b>定位口径复用俯视图</b>：按「第几格第几台」重走同一套
    /// <see cref="CollectWorkshops"/> / <see cref="CollectBenches"/> / <see cref="BenchBelongsToBay"/>，
    /// 不另立规则（LAND-010 结构走契约、不特判）。</para>
    ///
    /// <para><b>LAND-009 数据缺口如实</b>：预览 <c>.mkpi</c> 里的「单台 OEE」「有效节拍」「良品率」
    /// 需要按台累计的运行时历史（<c>METRICS</c>），1.6 原版没有按台暴露的口径 —— 这些项
    /// <c>Available=false</c>、值填「—」，绝不用整厂值或估算值冒充。真正取得到的（额定功率、
    /// 工单数、当前 CompQuality、技能等级、组件字典）全部来自原版对象。</para>
    /// </summary>
    public sealed partial class WorkshopFloorViewProvider
    {
        /// <summary>状态时序条段数（预览 <c>m.hist</c> 长度 40）。</summary>
        private const int TimelineSegments = 40;

        /// <summary>品质 7 档（原版 <see cref="QualityCategory"/> 顺序）。</summary>
        private static readonly QualityCategory[] QualityOrder =
        {
            QualityCategory.Awful, QualityCategory.Poor, QualityCategory.Normal,
            QualityCategory.Good, QualityCategory.Excellent, QualityCategory.Masterwork,
            QualityCategory.Legendary
        };

        public FloorDeviceDetailSnapshot BuildDeviceDetail(Map map, int slot, int index)
        {
            FloorDeviceDetailSnapshot detail = new FloorDeviceDetailSnapshot();
            try
            {
                if (map == null || slot < 0 || index < 0)
                {
                    return detail;
                }
                if (!TryLocate(map, slot, index, out Zone_Workshop zone, out IBillGiver bench, out Thing thing))
                {
                    return detail;
                }

                detail.HasDevice = true;
                BuildHeader(detail, zone, bench, thing);
                BuildMetrics(detail, bench, thing);
                BuildTabs(detail, zone, bench, thing);
                // S2-1（2026-09-23）：`BuildNavigation`（恒禁用的工序导航）已删除 ——
                //   原版没有「同链路前/后道」概念，该栏只能是恒禁用装饰（审计列为界面噪音）。
            }
            catch (Exception)
            {
                // 失败隔离：任何异常都退化成"没有设备"，不让弹窗路径影响主界面。
                detail.HasDevice = false;
            }
            return detail;
        }

        // ---- 定位：复刻俯视图的格/台口径 ----

        private static bool TryLocate(Map map, int slot, int index,
            out Zone_Workshop zone, out IBillGiver bench, out Thing thing)
        {
            zone = null;
            bench = null;
            thing = null;
            if (slot >= WorkshopFloorSnapshot.BaySlots || index >= WorkshopFloorSnapshot.DevicesPerBay)
            {
                return false;
            }

            List<Zone_Workshop> workshops = CollectWorkshops(map);
            if (slot >= workshops.Count)
            {
                return false;
            }
            zone = workshops[slot];
            if (zone == null)
            {
                return false;
            }

            List<IBillGiver> benches = CollectBenches(map);
            int placed = 0;
            for (int i = 0; i < benches.Count && placed < WorkshopFloorSnapshot.DevicesPerBay; i++)
            {
                IBillGiver candidate = benches[i];
                Thing candidateThing = candidate as Thing;
                if (candidateThing == null || !candidateThing.Spawned)
                {
                    continue;
                }
                if (!BenchBelongsToBay(candidateThing, zone, map))
                {
                    continue;
                }
                if (placed == index)
                {
                    bench = candidate;
                    thing = candidateThing;
                    return true;
                }
                placed++;
            }
            return false;
        }

        // ---- .dhead 标题带 ----

        /// <summary>
        /// 设备有效显示名（CHG-20261005-DEVRENAME）：车间自定义名 ?? 档案覆盖 ?? 传入的默认名。
        /// <para>无管理器/无地图时回落 <paramref name="fallback"/>（纯函数式，不抛异常）。</para>
        /// </summary>
        private static string EffectiveDeviceName(Thing thing, string fallback)
        {
            if (thing == null)
            {
                return fallback;
            }
            Map map = thing.Map;
            MapComponent_WorkshopManager mgr =
                map == null ? null : map.GetComponent<MapComponent_WorkshopManager>();
            return mgr != null ? mgr.EffectiveNameOf(thing) : fallback;
        }

        private static void BuildHeader(FloorDeviceDetailSnapshot detail, Zone_Workshop zone,
            IBillGiver bench, Thing thing)
        {
            string defLabel = LabelOf(thing.def);
            ThingDef def = thing.def;
            // CHG-20261005-DEVRENAME：设备卡标题走**有效名**（车间名 ?? 档案覆盖 ?? def 标签），
            // 与设备页 / 原版检查面板同口径 —— 否则弹窗标题仍显示旧名（同屏两名，GOV-005）。
            detail.Name = EffectiveDeviceName(thing, defLabel);

            // 编号：优先用设备自带的可读标识，取不到就不编造（预览的 M01 是仿真编号）。
            detail.DeviceId = string.Empty;

            string zoneName = string.IsNullOrEmpty(zone.label)
                ? T("PersonalChronicle.UI.Floor.Detail.UnnamedZone")
                : zone.label;
            ProductionDeviceCardView card = ProductionDeviceCards.Build(thing);
            string tier = card != null ? card.TierText
                : def != null && def.techLevel > TechLevel.Undefined
                ? def.techLevel.ToStringHuman().CapitalizeFirst()
                : string.Empty;
            detail.Identity = string.IsNullOrEmpty(tier)
                ? zoneName
                : Translate2("PersonalChronicle.UI.Floor.Detail.Identity", zoneName, tier);
            if (card != null) detail.Identity += " · " + card.SpeedText;

            // 2026-10-02 审计（P2-5 · 同一语义两套实现）：状态键统一走 WorkshopDeviceStateResolver.KeyOf ——
            // 它含「设备自报优先」分支（IWorkshopDeviceState）；原用的 EquipmentStateResolver.Resolve
            // 只看 (故障 / 供电 / 有活) 三个布尔 ⇒ 实现 IWorkshopDeviceState 的自研设备
            // （自动农场 / 传送带）在本详情页显示为 idle/off，与倒班表、点检表**同机不同态**。
            // 本方法同属 Automation 层，可直接改用唯一实现（Domain 层读数器仍走旧口径，
            // 需在 Domain 侧加"自报键"入参才能统一 —— 登记为下一批）。
            string stateKey = WorkshopDeviceStateResolver.KeyOf(thing);
            detail.StateKey = stateKey;
            detail.StateText = T(EquipmentStateText.TranslationKeyOf(stateKey));

            // 状态行右侧补充（预览 .dstate .sp = **当前在做什么**，如「正在烹饪 · 简单餐」，HTML:155）。
            // CHG-20261002-FLOORDETAIL-AUDIT：旧值 = 「节拍采集中」（CycleAvailable）—— 那是"进度
            //   可算"的**内部状态**，对玩家零信息量，且与预览的语义完全不同（2026-10-02 截图实证）。
            //   现改为当前作业：JobDef 本地化标签 + 配方名（两者皆原版直读）；取不到则留空，
            //   不编造（LAND-009）。键 ...CycleAvailable 两语言保留未删（失去引用，回滚成本最低）。
            Pawn cycleOperator = FindOperator(thing);
            Bill cycleBill = FindActiveBill(bench.BillStack);
            string cycleJob = cycleOperator != null && cycleOperator.CurJobDef != null
                ? cycleOperator.CurJobDef.LabelCap.ToString()
                : null;
            detail.CycleText = string.IsNullOrEmpty(cycleJob)
                ? string.Empty
                : (cycleBill != null && cycleBill.recipe != null
                    ? cycleJob + " · " + cycleBill.recipe.LabelCap.ToString()
                    : cycleJob);
        }

        // ---- 设备清单短缓存（2026-10-02 减法审计 P0）------------------------

        /// <summary>设备清单缓存（zone.ID → (清单, 采样 tick)）。</summary>
        private static readonly Dictionary<int, KeyValuePair<List<Thing>, int>> deviceScanCache
            = new Dictionary<int, KeyValuePair<List<Thing>, int>>();

        /// <summary>设备清单缓存时长（tick）：60 = 1 游戏秒。</summary>
        private const int DeviceScanCacheTicks = 60;

        /// <summary>空清单共享实例（避免"无设备"时每次分配）。</summary>
        private static readonly List<Thing> EmptyDeviceScan = new List<Thing>();

        /// <summary>
        /// 按 zone 缓存的设备清单（60 tick = 1 游戏秒）。
        ///
        /// <para>2026-10-02 减法审计（P0）：楼层详情是"开窗即绘制"的热路径，
        /// 直接 <see cref="WorkshopStats.ScanDevices"/> 会<b>每帧全量重扫车间区</b>。
        /// 与 <c>WorkshopRosterBoard.AvailabilityOf</c> 同一先例；对"待加工 / 故障 / 断电"
        /// 三项计数而言，1 秒延迟完全不可感知。</para>
        /// </summary>
        private static List<Thing> CachedScanDevices(Zone_Workshop zone)
        {
            if (zone == null || zone.Map == null)
            {
                return EmptyDeviceScan;
            }
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            KeyValuePair<List<Thing>, int> hit;
            if (deviceScanCache.TryGetValue(zone.ID, out hit)
                && now - hit.Value < DeviceScanCacheTicks)
            {
                return hit.Key;
            }
            List<Thing> devices = WorkshopStats.ScanDevices(zone);
            deviceScanCache[zone.ID] = new KeyValuePair<List<Thing>, int>(devices, now);
            return devices;
        }

        // ---- .mkpi 六格指标 ----

        private static void BuildMetrics(FloorDeviceDetailSnapshot detail, IBillGiver bench, Thing thing)
        {
            CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
            BillStack stack = bench.BillStack;

            // S1（2026-09-23）：**指标 6 → 4 格，且 4 格全真值** ——
            //   删掉 3 个**永久「—」**（单台 OEE / 有效节拍 / 良品率：原版无按台历史，LAND-009 不许编造，
            //   摆在首屏只会让人以为界面坏了），以及标签不实的"额定功率"（原版 `PowerOutput` 是**当前**输出）。
            //   保留：在册工单 / **当前功率** / **作业剩余** / 当前品质档 —— 全部原版直读。
            //   ⚠ 方案原建议第 4 格是「车间加成」，但 S3-1 已把加成做成概览页的**加成条**
            //     （同时给工时/品质/耗材三项）⇒ 指标格再放一格属重复（同一信息只出现一次）。
            detail.Metrics.Add(new FloorDetailMetricView
            {
                ValueText = stack != null ? stack.Count.ToString(CultureInfo.InvariantCulture) : "—",
                Label = T("PersonalChronicle.UI.Floor.Detail.Metric.Bills"),
                Available = stack != null
            });
            detail.Metrics.Add(new FloorDetailMetricView
            {
                ValueText = power != null
                    ? Mathf.RoundToInt(Mathf.Abs(power.PowerOutput)).ToString(CultureInfo.InvariantCulture) + " W"
                    : "—",
                Label = T("PersonalChronicle.UI.Floor.Detail.Metric.CurrentPower"),
                Available = power != null
            });
            detail.Metrics.Add(new FloorDetailMetricView
            {
                ValueText = ResolveWorkLeftText(thing),
                Label = T("PersonalChronicle.UI.Floor.Detail.Metric.WorkLeft"),
                Available = true
            });
            // 第 4 格：**车间工时加成**（预览 `mkpi` 第 4 格）—— 与概览页「车间加成条」**同口径同源**
            //   （`WorkshopBonusAccess.WorkingBonusOf`）；无操作者 / 契约未绑定 ⇒ 如实「—」，
            //   不显示中性 ×1.00 冒充"有加成"。
            //   CHG-20261002-RUNTAB-100PCT：值形态 ×N → NN %（FormatBonusPercent，与概览页加成行同源同形）。
            //   ⚠ 原第 4 格「当前品质档」按预览移出 4 格集（预览把它列为 **6 格模式**的补充项）；
            //     品质档本身在健康页「品质分布」块仍可见，信息不丢。
            Pawn metricsOperator = FindOperator(thing);
            string workTimeBonus = "—";
            if (metricsOperator != null)
            {
                WorkshopEffectiveBonus metricsBonus = WorkshopBonusAccess.WorkingBonusOf(metricsOperator);
                workTimeBonus = FormatBonusPercent(metricsBonus.workTimeFactor);
            }
            detail.Metrics.Add(new FloorDetailMetricView
            {
                ValueText = workTimeBonus,
                Label = T("PersonalChronicle.UI.Floor.Detail.Metric.Bonus"),
                Available = metricsOperator != null
            });
        }

        /// <summary>
        /// 批次 2（2026-09-23）：**所需研究**文案 —— `researchPrerequisites`（列表）优先 →
        /// 回退 `researchPrerequisite`（单个）→ 「无」。两者均已 Cecil 核实存在于 `Verse.RecipeDef`。
        /// </summary>
        private static string BuildResearchText(RecipeDef recipe)
        {
            if (recipe == null)
            {
                return "—";
            }
            if (recipe.researchPrerequisites != null && recipe.researchPrerequisites.Count > 0)
            {
                return string.Join(" · ", recipe.researchPrerequisites.ConvertAll(
                    rp => rp != null ? rp.LabelCap.ToString() : "—").ToArray());
            }
            return recipe.researchPrerequisite != null
                ? recipe.researchPrerequisite.LabelCap.ToString()
                : T("PersonalChronicle.UI.Floor.Detail.Bill.NoResearch");
        }

        /// <summary>
        /// 批次 3（2026-09-23）：人员排名行的**当班状态**（预览 `rk-tag`：在岗 / 空闲 / 其他工作）。
        ///
        /// <para>判定只用已核实可读的字段：本台操作者（调用方传入 `isOperator`）/ `CurJobDef == null`。
        /// ⚠ **「休息」态未做** —— 需把 `timetable.CurrentAssignment` 映射为语义档，该映射未核实
        /// ⇒ 如实归入「其他工作」，不猜（LAND-009）。</para>
        /// </summary>
        private static string BuildDutyLabel(Pawn pawn, bool isOperator)
        {
            if (pawn == null || pawn.Dead || pawn.Downed)
            {
                return T("PersonalChronicle.UI.Floor.Detail.Crew.Duty.Unavailable");
            }
            if (isOperator)
            {
                return T("PersonalChronicle.UI.Floor.Detail.Crew.Duty.OnPost");
            }
            // 「休息」：作息表当前档 = `Sleep`（`TimeAssignmentDefOf.Sleep` 已 Cecil 核实存在；
            //   原版另有 Anything / Work / Joy / Meditate，只有 Sleep 语义等价"在睡觉"）。
            if (pawn.timetable != null
                && pawn.timetable.CurrentAssignment == TimeAssignmentDefOf.Sleep)
            {
                return T("PersonalChronicle.UI.Floor.Detail.Crew.Duty.Resting");
            }
            return pawn.CurJobDef == null
                ? T("PersonalChronicle.UI.Floor.Detail.Crew.Duty.Idle")
                : T("PersonalChronicle.UI.Floor.Detail.Crew.Duty.Other");
        }

        private static FloorDetailMetricView Unavailable(string label)
        {
            return new FloorDetailMetricView { ValueText = "—", Label = label, Available = false };
        }

        // ---- .dtab 页签内容 ----

        // ---- 数据来源徽标号（与 FloorDetailRowView.SourceKind 对齐；UI 只按号映射颜色/文案）----
        /// <summary>原版字段直读。</summary>
        internal const int SourceApi = 1;

        /// <summary>
        /// 来源 = **本 mod 计算 / 累计**（契约值 2，LAND-010）。S3-4 起用于人员页胜任度排名
        /// （值由 <c>WorkshopCrewScoring</c> 纯函数算出，非原版字段直读）。
        /// </summary>
        internal const int SourceLedger = 2;
        /// <summary>本 mod 累计 / Provider 计算。</summary>
        // ⚠ 审计结论（2026-09-12）：**本弹窗不存在 LEDGER 口径**，故原 `SourceLedger = 2` 常量已删除（GOV-006：不留未使用常量）。
        //   依据：全文件逐行核对后，所有行要么是原版成员直读（SourceApi），要么是"原版/本 mod 机制未落地、值取「—」+ 原因"（SourceSim）。
        //   预览侧的 37 处 LEDGER 对应的是**按台统计与按台历史**（单台 OEE / 有效节拍 / 良品率 / 时间轴段数），
        //   这些在本工程**尚无 ledger 数据源** ⇒ 一律如实标 SIM 并显示「—」，而不是标成"能算却没算"的 LEDGER。
        //   将来 ledger 落地时再恢复该常量（并在本表补标）。
        /// <summary>原版**无此口径** ⇒ 值必须为「—」并写明原因（LAND-009）。</summary>
        internal const int SourceSim = 3;

        private static void BuildTabs(FloorDeviceDetailSnapshot detail, Zone_Workshop zone,
            IBillGiver bench, Thing thing)
        {
            // ---- S2-1（2026-09-23）：页签 **7 → 4** 重组（审计方案落地）----
            //   ① 概览（运行读数 + 电气）  ② 生产（**可写列表 + 工单只读摘要合并**）
            //   ③ 人员（胜任度排名 + 派工）④ 健康（**TPM + 品质合并**）
            //   原「⑥ 契约」= 开发者数据（组件字典 / 内部类名 / gizmo 计数）⇒ **仅开发模式可见**：
            //   玩家侧界面不再出现（LAND-003 干净度），开发者开 `DevMode` 仍在（能力不丢）。
            detail.TabTitles.Add(T("PersonalChronicle.UI.Floor.Detail.Tab.Overview"));    // 0 概览
            detail.TabTitles.Add(T("PersonalChronicle.UI.Floor.Detail.Tab.Production"));  // 1 生产
            detail.TabTitles.Add(T("PersonalChronicle.UI.Floor.Detail.Tab.Crew"));        // 2 人员
            detail.TabTitles.Add(T("PersonalChronicle.UI.Floor.Detail.Tab.Health"));      // 3 健康

            AddSection(detail, BuildRunTab(bench, thing, zone), 0);
            // CHG-20261002-FLOORDETAIL-AUDIT：概览 = 两块（15 行键值 → 设备检视），
            //   与预览 HTML:691-738 的块序一致；检视串取不到时不登记（不留空标题）。
            FloorDetailSectionView runInspect = BuildRunInspectTab(thing);
            if (runInspect.InspectLines.Count > 0)
            {
                AddSection(detail, runInspect, 0);
            }

            // 生产：**可写列表在前**（主交互），工单只读摘要在后（同页两块，不再各自占页签）
            AddSection(detail, BuildBillQueueTab(bench, thing), 1);
            AddSection(detail, BuildBillTab(bench, thing), 1);
            // CHG-20261002-FLOORDETAIL-AUDIT：配料 = 独立第三块（预览 HTML:837-850 同级块；
            //   旧实现挂在摘要块内 ⇒ 默认折叠态下配料整块不可见）。
            FloorDetailSectionView bom = BuildBomTab(bench, thing);
            if (bom.Rows.Count > 0)
            {
                AddSection(detail, bom, 1);
            }

            AddSection(detail, BuildCrewTab(zone, thing), 2);

            // 健康：TPM 在前、品质在后（同页两块）
            AddSection(detail, BuildHealthTab(bench, thing), 3);
            AddSection(detail, BuildQualTab(thing), 3);

            // ⚠ CHG-20261003-FLOORDETAIL-UNIFY：原「契约」页（开发者数据契约可视化：组件字典 /
            //   内部类名 / gizmo 计数 / 工序导航）已**彻底删除**，含 DevMode 通道。
            //   理由：① 它把 `workSpeedStat.defName`、`thingClass.Name`、`productsHaveQuality` 之类
            //   **内部逻辑键**直接显示给玩家，与 LAND-003 干净度和 LAND-009 缺口如实相悖；
            //   ② 它与概览/生产/人员/健康四页的信息**零重叠**，玩家零使用价值；
            //   ③ 开发者要查这些值应走 Dbg / 反编译，而不是塞进玩家界面。
            //   删除回收 **125 行**（`BuildContractTab` + `ContractCompNames`，实测
            //   `Detail.Tabs.cs` 316 → 191 行）与中英各 **19** 个键（实测 XML 良构通过）。
            //   ⚠ `AddRow` **保留**：人员页 / 健康页仍在用（见 `Detail.cs:905` 起的多处调用）。
        }

        /// <summary>
        /// S2-1：把块登记到指定页签（**一页可多块**）—— <c>Sections</c> 与 <c>TabTitles</c>
        /// 不再下标一一对应，改用 <see cref="FloorDetailSectionView.TabIndex"/> 关联（UI 侧按该值过滤）。
        /// </summary>
        private static void AddSection(FloorDeviceDetailSnapshot detail,
            FloorDetailSectionView section, int tabIndex)
        {
            section.TabIndex = tabIndex;
            detail.Sections.Add(section);
        }

        private static FloorDetailSectionView BuildRunTab(IBillGiver bench, Thing thing, Zone_Workshop zone)
        {
            FloorDetailSectionView section = new FloorDetailSectionView
            {
                Caption = T("PersonalChronicle.UI.Floor.Detail.Run.Caption")
            };

            // 操作者在本块与「当前作业」块共用，故在此提前取一次（避免重复调用 FindOperator）。
            Pawn operatorPawn = FindOperator(thing);

            // S3-1（2026-09-23）：**车间加成**（本 mod 核心价值 —— 此前弹窗完全未接入）。
            //   数据源 = Application 契约 WorkshopBonusAccess.WorkingBonusOf(pawn)
            //   （Automation → Application 为允许方向；实现由 WorkshopBonusAccessBinder 注入）。
            //   CHG-20261002-RUNTAB-EMPTYSTATE：行集恒定（LAND-009）—— 无操作者/契约未绑定
            //   时 3 行恒占位「—」（不冒充有加成），不再整段消失；行数不随操作者进出漂移 ⇒
            //   测量与绘制的行数口径稳定（HTML 基准 696-713：15 行恒有值，无"空行消失"形态）。
            //   WorkshopEffectiveBonus 为值类型 ⇒ 用 Nullable 承载"无操作者"（LAND-009）。
            //   CHG-20261002-RUNTAB-100PCT：倍率加成 ×N → NN %（用户裁决）；空态「—」→「无」（仅 RunTab）。
            WorkshopEffectiveBonus? bonus = operatorPawn != null
                ? WorkshopBonusAccess.WorkingBonusOf(operatorPawn)
                : null;
            section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Bonus.WorkTime"),
                bonus.HasValue ? FormatBonusPercent(bonus.Value.workTimeFactor) : NoneValue, 0));
            section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Bonus.Quality"),
                bonus.HasValue
                    ? (bonus.Value.qualityOffset >= 0 ? "+" : string.Empty)
                    + bonus.Value.qualityOffset.ToString(CultureInfo.InvariantCulture)
                    : NoneValue, 0));
            section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Bonus.StuffCost"),
                bonus.HasValue ? FormatBonusPercent(bonus.Value.stuffCostFactor) : NoneValue, 0));

            // S2-2（2026-09-23）：**当前作业**（进度数值此前已取到，却只用于"是否显示节拍"的布尔判断）
            //   —— 现在如实显示：配方 / 进度 / 操作者 / 剩余工时。
            Bill activeBill = FindActiveBill(bench.BillStack);
            RecipeDef activeRecipe = activeBill != null ? activeBill.recipe : null;
            float jobProgress = WorkshopDeviceReader.ResolveProgress(operatorPawn, thing);
            section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Job.Recipe"),
                activeRecipe != null ? activeRecipe.LabelCap.ToString() : NoneValue, 0));
            section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Job.Progress"),
                jobProgress >= 0f
                    ? Mathf.RoundToInt(Mathf.Clamp01(jobProgress) * 100f)
                        .ToString(CultureInfo.InvariantCulture) + " %"
                    : NoneValue, 0));
            section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Job.Operator"),
                operatorPawn != null ? operatorPawn.LabelShort : NoneValue, 0));

            // S2-3（2026-09-23）：**本班**（交接班口径）—— 复用 WorkshopHandoverBoard 的班次计算
            //   （该板的 SlotAtHour / NextShiftStartHour / CountOnDuty / SlotLabel 已提为 internal），
            //   **不重复实现"三班倒"轮转逻辑**（规范红线：同功能不得两处实现）。
            //   本块四项已齐全：当前班次 · 在岗 / 距下次交接 / 待加工 / 异常设备（后两项见下方 S2-3 补全）。
            //   CHG-20261002-RUNTAB-EMPTYSTATE：本段 4 行**恒占位**（LAND-009）—— 无车间区/
            //   无班次表时恒占位（对齐 HTML 基准 702-708 恒有值形态），不再整段条件消失；CHG-20261002-RUNTAB-100PCT 起空态值=「无」。
            string shiftCurrent = NoneValue;
            string shiftNext = NoneValue;
            string shiftOpenBills = NoneValue;
            string shiftFaultsUnpowered = NoneValue;
            if (zone != null && zone.Map != null)
            {
                MapComponent_WorkshopManager manager =
                    zone.Map.GetComponent<MapComponent_WorkshopManager>();
                List<WorkshopShift> shifts = manager != null ? manager.Shifts : null;
                if (shifts != null && shifts.Count > 0)
                {
                    int hour = GenLocalDate.HourOfDay(zone.Map);
                    int shiftCount = WorkshopRosterBoard.CountRotationSlots(shifts);
                    List<WorkshopCrew> crews = zone.Crews;
                    int[] kinds = zone.ExceptionKindsFor(GenDate.DaysPassed, crews.Count);
                    int phase = GenDate.DaysPassed - zone.CycleAnchorDay;
                    int slot = WorkshopHandoverBoard.SlotAtHour(shifts, hour);
                    int crewIndex = slot >= 0 && shiftCount > 0
                        ? WorkshopCrewCycle.CrewOfShiftSlot(phase, slot, crews.Count, shiftCount, kinds)
                        : -1;
                    string crewName = crewIndex >= 0 && crewIndex < crews.Count
                        ? crews[crewIndex].Name
                        : "—";
                    shiftCurrent = WorkshopHandoverBoard.SlotLabel(slot) + " · " + crewName + " · "
                        + T1("PersonalChronicle.UI.Floor.Detail.Shift.OnDuty",
                            manager != null ? WorkshopHandoverBoard.CountOnDuty(manager, zone) : 0);
                    int nextHour = WorkshopHandoverBoard.NextShiftStartHour(shifts, hour);
                    int untilNext = nextHour >= hour ? nextHour - hour : 24 - hour + nextHour;
                    shiftNext = T1("PersonalChronicle.UI.Floor.Detail.Shift.InHours", untilNext);

                    // S2-3 补全（2026-09-23）：**待加工 / 异常设备**（"交接三件事"的另两件）——
                    //   设备清单走 `WorkshopStats.ScanDevices`（Read Model 口径），计数走交班板
                    //   新提的 `CountWorkshopStatus`（**同一实现**，不重写第二份循环）；
                    //   文案直接复用交班板的键（同源同格式，不另造同义文案）。
                    // 2026-10-02 减法审计（P0）：设备清单此前**每帧**全量重扫车间区 ——
                    // 楼层详情是「开窗即绘制」的热路径。改为按 zone 缓存 60 tick（1 游戏秒），
                    // 与 WorkshopRosterBoard.AvailabilityOf 同一先例；对三项计数而言 1 秒延迟不可感知。
                    WorkshopHandoverBoard.CountWorkshopStatus(CachedScanDevices(zone),
                        out int openBills, out int faults, out int unpowered);
                    // CHG-20261002-FLOORDETAIL-AUDIT：本行标签已是「待加工工单」（Shift.OpenBills），
                    //   值若沿用交班板的 `HandoverOpenBills`（"待加工工单 {0} 项"）会**标签与值同文**
                    //   （实机："待加工工单 | 待加工工单 1 项"，HTML:725 已把它登记为游戏侧待修项）
                    //   ⇒ 值改用本弹窗自有的**纯数量**键 `Shift.OpenBillsValue`（{0} 项）。
                    //   交班板仍用原键（唯一实现不变，仅本处换值形态）。
                    shiftOpenBills = T1("PersonalChronicle.UI.Floor.Detail.Shift.OpenBillsValue", openBills);
                    // 批次1（2026-09-23）：预览要求**故障与断电分开呈现**（`0 / 1`）——
                    //   原实现把两者相加成「异常设备 {0}」，看不出是坏了还是没电（处置动作完全不同）。
                    shiftFaultsUnpowered = faults.ToString(CultureInfo.InvariantCulture) + " / "
                        + unpowered.ToString(CultureInfo.InvariantCulture);
                }
            }
            section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Shift.Current"), shiftCurrent, 0));
            section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Shift.Next"), shiftNext, 0));
            section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Shift.OpenBills"), shiftOpenBills, 0));
            section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Shift.FaultsUnpowered"),
                shiftFaultsUnpowered, 0));

            // CHG-20261002-FLOORDETAIL-AUDIT：检视串**已移出本块** ⇒ 独立块 `BuildRunInspectTab`
            //   （预览把它排在 15 行键值**之后**，HTML:729-738）。旧实现把它塞在本块内部 ⇒ 渲染侧
            //   「先检视、后行集」把实机顺序变成「块标题 → 检视串 → 15 行」，与预览相反。
            //   ⚠ 行序不变（本块 15 行的相对次序与数量是 LAND 契约，勿动）。

            CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
            CompBreakdownable breakdown = thing.TryGetComp<CompBreakdownable>();
            // CHG-20261002-RUNTAB-EMPTYSTATE：CompFlickable 局部变量随 EmptyHint 分支一并删除
            //   （其唯一消费点即该分支；「电源开关」行此前已按审计 P3 删除）。

            // S1-2/S1-3（2026-09-23）：值一律本地化（原为裸 true/false 与英文 token）；
            //   并删除「电源开关」行 —— 与「通电状态」语义重复（审计 P3）。
            section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Run.PowerOn",
                power != null ? BoolText(power.PowerOn) : NoneValue, 0));
            section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Run.CurrentPower",
                power != null
                    ? (power.PowerOn
                        ? Mathf.RoundToInt(Mathf.Abs(power.PowerOutput)).ToString(CultureInfo.InvariantCulture) + " W"
                        : "0 W")
                    : NoneValue, 0));
            // 批次 1 收尾（2026-09-23）：**电网储能**（本台所属网）—— 预览要求给出"本台读数之外的全网水平"，
            //   否则只有一串孤立数字（看不出这台在网里算大用户还是小用户）。
            //   `CompPower.PowerNet` 为 **public 属性**（Cecil 已核实）⇒ 只取本台那一张网，不遍历全网列表。
            //   ⚠ 只报**储能**（`CurrentStoredEnergy()`，单位 Wd）；`CurrentEnergyGainRate()` 的单位是
            //     **Wd/tick** 而非 W（工程既有注释），换算公式未核实 ⇒ 不报、不估算（LAND-009）。
            //   CHG-20261002-RUNTAB-EMPTYSTATE：恒占位（LAND-009）—— 无电组件/无电网时「—」。
            section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Run.Grid"),
                power != null && power.PowerNet != null
                    ? power.PowerNet.CurrentStoredEnergy().ToString("0.#", CultureInfo.InvariantCulture)
                    + " Wd"
                    : NoneValue, 0));
            section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Run.BrokenDown",
                breakdown != null
                    ? (breakdown.BrokenDown
                        ? T("PersonalChronicle.UI.Floor.Detail.Run.Faulted")
                        : T("PersonalChronicle.UI.Floor.Detail.Run.Normal"))
                    : NoneValue,
                breakdown != null && breakdown.BrokenDown ? 3 : (breakdown != null ? 1 : 0)));
            // CHG-20261002-RUNTAB-100PCT：RunTab 空态=「无」—— ResolveWorkLeftText 与 KPI 卡共用 ⇒ 行级映射（不动共享函数）。
            string workLeftText = ResolveWorkLeftText(thing);
            section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Run.WorkLeft",
                workLeftText == "—" ? NoneValue : workLeftText, 0));

            // S1（2026-09-23）：**删除「状态时序」全链** —— 40 格时序条只有 1 段真数据（原版无按台历史），
            //   再配一行「原版无按台历史」的常驻说明 ⇒ 纯占地方的噪音（审计列为界面噪音项）。
            //   当前状态本身在标题带状态灯里已表达，无需时序条重复。

            // CHG-20261002-RUNTAB-EMPTYSTATE：删除 RunTab 的 EmptyHint 分支 —— 原逻辑
            //   （电源/开关/损坏三组件全无 ⇒ 设 EmptyHint）会让绘制/测量提前收口
            //   （FloorDeviceDetailWindow.cs 的 DrawTabSection / MeasureSectionHeight 早退分支），
            //   把本块 15 行契约整体替换成一行提示，实机表现为"内容行消失 + 挤兑"。
            //   现电源相关行恒占位（通电状态/当前功率/电网储能 = 「—」），空态语义由行值承载
            //   （LAND-009）。键 `...Run.Empty` 两语言保留未删（仅失去引用，回滚成本最低）。
            return section;
        }

        /// <summary>
        /// 概览第二块 = **设备检视**（CHG-20261002-FLOORDETAIL-AUDIT）：
        /// 预览把检视串当作**独立块**排在 15 行键值之后（HTML:729-738，块标题 = Run.InspectCaption
        /// + API 徽标）；旧实现把检视串塞在运行块内部、由渲染侧「先检视后行集」提前 ⇒ 实机
        /// 表现为「块标题 → 检视串 → 15 行」，与预览顺序相反（2026-10-02 截图实证）。
        ///
        /// <para>数据 = <c>Thing.GetInspectString()</c> 的**原样**拆行（基类内容 + 各
        /// <c>CompInspectString</c> 的官方合成）⇒ 只拆行，不拼接、不过滤、不重排
        /// （任何加工都可能吞掉基类的火灾 / 温度告警，LAND-009 / UI-004）。</para>
        /// </summary>
        private static FloorDetailSectionView BuildRunInspectTab(Thing thing)
        {
            FloorDetailSectionView section = new FloorDetailSectionView
            {
                Caption = T("PersonalChronicle.UI.Floor.Detail.Run.InspectCaption"),
                SourceKind = SourceApi
            };
            try
            {
                string inspect = thing.GetInspectString();
                if (!string.IsNullOrEmpty(inspect))
                {
                    string[] inspectLines = inspect.Split('\n');
                    for (int i = 0; i < inspectLines.Length; i++)
                    {
                        string line = inspectLines[i].TrimEnd('\r');
                        if (string.IsNullOrEmpty(line))
                        {
                            continue;
                        }
                        // 深度优化（2026-10-01）：外部 Comp 的检视串可能直出**内部 token**
                        //（实机："MechanicalPower.cycle.05 / 283.8 Wd"）—— 玩家不可读。
                        //   非开发模式按"内部标识形串"过滤；DevMode 仍显示（可诊断，GOV-009）。
                        if (!Prefs.DevMode && LooksLikeInternalToken(line))
                        {
                            continue;
                        }
                        section.InspectLines.Add(line);
                    }
                }
            }
            catch (Exception)
            {
                // 检视串取失败不影响其余行（诚实留空，不伪造）
            }
            return section;
        }

        /// <summary>
        /// 生产页第三块 = **配料**（CHG-20261002-FLOORDETAIL-AUDIT）：预览把它与「当前工单配方」
        /// 并列为同级块（HTML:837-850）。旧实现把配料行**追加进工单摘要块**内 ⇒ 该块默认折叠 ⇒
        /// 配料在默认视图里完全不可见（2026-10-02 截图实证："生产页看不到配料"）。
        /// 空（无工单 / 无配料需求）时不登记本块（不显示空标题）。
        /// </summary>
        private static FloorDetailSectionView BuildBomTab(IBillGiver bench, Thing thing)
        {
            FloorDetailSectionView section = new FloorDetailSectionView
            {
                Caption = T("PersonalChronicle.UI.Floor.Detail.Bill.BomCaption")
            };
            AppendAggregatedBom(section, bench, thing);
            return section;
        }

        /// <summary>
        /// S3-3（2026-09-23）：本机**全部工单**的配料需求合计 vs 殖民地库存。
        ///
        /// <para><b>口径与俯视图「原料库」同源</b>（<c>WorkshopFloorViewProvider.FillRawRail</c>）：
        /// 遍历 BillStack 全部 Bill → 按 <c>IngredientCount.count</c>（<c>GetBaseCount()</c> 已 Cecil 核实
        /// = 直读 <c>count</c>）累加需求 → 与 <c>ResourceCounter.GetCount</c> 对照 →
        /// 按「库存/需求」升序（越紧张越靠前）；范围收窄为**本机**（单台设备）。
        /// 宽泛筛选（如"任意食材"）取 filter 允许物品的库存合计，每个 filter 最多遍历 120 个 def
        /// （同 FillRawRail 的无界保护）。库存不足时行前置 ⚠ 且语气转「差」。</para>
        /// </summary>
        private static void AppendAggregatedBom(FloorDetailSectionView section, IBillGiver bench, Thing thing)
        {
            BillStack stack = bench.BillStack;
            Map map = thing.Map;
            if (stack == null || stack.Count == 0 || map == null || map.resourceCounter == null)
            {
                return;
            }

            // 修复（2026-09-23 实机反馈）：**按 filter 摘要聚合**（一个材料类别一行）——
            //   原实现按 filter 内**每个 ThingDef** 各起一行 ⇒ 宽泛筛选（"生食"允许十余种物品）
            //   会刷出十几行同样类别，把整页挤满；预览 `matrow` 的形态是「食物（任意） 需 20 · 存 120」一行。
            Dictionary<string, int> demand = new Dictionary<string, int>();
            Dictionary<string, int> stock = new Dictionary<string, int>();
            for (int b = 0; b < stack.Count; b++)
            {
                Bill bill = stack[b];
                RecipeDef billRecipe = bill != null ? bill.recipe : null;
                if (billRecipe == null || billRecipe.ingredients == null)
                {
                    continue;
                }
                for (int g = 0; g < billRecipe.ingredients.Count; g++)
                {
                    IngredientCount ing = billRecipe.ingredients[g];
                    if (ing == null || ing.filter == null || ing.filter.AllowedThingDefs == null)
                    {
                        continue;
                    }
                    string category = ing.filter.Summary;
                    if (string.IsNullOrEmpty(category))
                    {
                        continue;
                    }
                    int amount = Mathf.Max(1, Mathf.CeilToInt(ing.GetBaseCount()));
                    demand[category] = (demand.TryGetValue(category, out int cur) ? cur : 0) + amount;
                    // 库存 = 该 filter **允许物品的库存合计**（原版无按 filter 统计的公开入口 ⇒ 逐 def 求和）；
                    // ⚠ 库存是全局量：多工单命中同一类别时**不累加**（覆盖），否则"存"会翻倍虚高。
                    int stockOfCategory = 0;
                    int guard = 0;
                    foreach (ThingDef def in ing.filter.AllowedThingDefs)
                    {
                        if (def == null)
                        {
                            continue;
                        }
                        if (++guard > 120)
                        {
                            break;
                        }
                        stockOfCategory += map.resourceCounter.GetCount(def);
                    }
                    stock[category] = stockOfCategory;
                }
            }
            if (demand.Count == 0)
            {
                return;
            }

            List<KeyValuePair<string, int>> rows = new List<KeyValuePair<string, int>>(demand);
            rows.Sort((a, b) =>
            {
                int ha = stock.TryGetValue(a.Key, out int sa) ? sa : 0;
                int hb = stock.TryGetValue(b.Key, out int sb) ? sb : 0;
                return (ha / (float)Mathf.Max(1, a.Value)).CompareTo(hb / (float)Mathf.Max(1, b.Value));
            });

            for (int i = 0; i < rows.Count; i++)
            {
                string category = rows[i].Key;
                int need = Mathf.Max(1, rows[i].Value);
                int have = stock.TryGetValue(category, out int s) ? s : 0;
                FloorDetailRowView bomRow = RowText(
                    (have < need ? "⚠ " : string.Empty) + category,
                    Translate2("PersonalChronicle.UI.Floor.Detail.Bill.BomRow",
                        need.ToString(CultureInfo.InvariantCulture),
                        have.ToString(CultureInfo.InvariantCulture)),
                    have < need ? 3 : 1);
                // 库存/需求比例条（预览 `matrow` 的 `.mb`）—— 一眼看出"够不够"，不用心算两个数；
                //   CHG-20261002-RUNTAB-100PCT：充足行语气 0→1（good）＝对齐 HTML :837-838「条色=值色」。
                bomRow.Bar01 = Mathf.Clamp01(have / (float)need);
                section.Rows.Add(bomRow);
            }
        }

        /// <summary>页签②工单：BillStack + 当前 Bill 的配方（RecipeDef 直读）。</summary>
        /// <remarks>CHG-20261002-PRODCOLLAPSE：本块声明**折叠摘要行**（IsSummary = 配方 / 基础工时 / 重复模式 / 目标数量 ↔ 预览「摘要 3 行」语义)；窗口折叠态只显示摘要行、开关「▶ 详情 / ▼ 收起」（LAND-010 行级契约）。</remarks>
        private static FloorDetailSectionView BuildBillTab(IBillGiver bench, Thing thing)
        {
            FloorDetailSectionView section = new FloorDetailSectionView
            {
                Caption = T("PersonalChronicle.UI.Floor.Detail.Bill.Caption")
            };

            BillStack stack = bench.BillStack;
            if (stack == null || stack.Count == 0)
            {
                section.EmptyHint = T("PersonalChronicle.UI.Floor.Detail.Bill.Empty");
                return section;
            }

            Bill current = FindActiveBill(stack);
            RecipeDef recipe = current != null ? current.recipe : null;
            // repeatMode / targetCount 只存在于 Bill_Production（已用 Cecil 核实基类 Bill 无此成员）。
            Bill_Production production = current as Bill_Production;
            // 批次 2（2026-09-23）：**按预览重排为完整字典表**（预览 `dt-prod` 配方详情 9 行：
            //   配方 / 基础工时 / 工作技能 / 速度属性 / 产物 / 所需研究 / 重复模式 / 目标数量 / 已挂起）。
            //   ⚠ S2-4 曾以"与生产列表重复"删掉其中 3 行 + 改动 1 行 —— 但预览把此块定义为**查阅面**
            //     （列表 = 操作面，同一信息在两处的**用途不同**）⇒ 本次回归。产出行在原位（多行、逐产物）。
            section.Rows.Add(RowSummary("PersonalChronicle.UI.Floor.Detail.Bill.Recipe",
                recipe != null ? recipe.LabelCap.ToString() : "—", 0));
            section.Rows.Add(RowSummary("PersonalChronicle.UI.Floor.Detail.Bill.WorkAmount",
                recipe != null
                    ? Mathf.RoundToInt(recipe.WorkAmountTotal(null)).ToString(CultureInfo.InvariantCulture)
                    : "—", 0));
            // S3-2（2026-09-23）：配方详情补「工作技能」（`RecipeDef.workSkill` 直读；此前弹窗未展示）。
            section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Bill.WorkSkill",
                recipe != null && recipe.workSkill != null
                    ? recipe.workSkill.LabelCap.ToString()
                    : "—", 0));
            // 批次 2：恢复「速度属性」—— 值改 **`LabelCap`**（原为 `StatDef.defName` 直显 = 内部逻辑键
            //   泄漏，这正是 S2-4 删它的原因；预览要求保留此信息 ⇒ 以本地化形式回归）。
            section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Bill.WorkSpeedStat",
                recipe != null && recipe.workSpeedStat != null
                    ? recipe.workSpeedStat.LabelCap.ToString()
                    : "—", 0));
            // 批次 2：**所需研究**（预览列出；`RecipeDef.researchPrerequisites` 列表优先，
            //   回退单个 `researchPrerequisite` —— 两者均已 Cecil 核实存在）。
            section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Bill.Research",
                BuildResearchText(recipe), 0));
            // 批次 2：恢复「重复模式 / 目标数量 / 已挂起」三行（列表侧的独立状态列由批次 2 另加）。
            section.Rows.Add(RowSummary("PersonalChronicle.UI.Floor.Detail.Bill.RepeatMode",
                production != null && production.repeatMode != null
                    ? production.repeatMode.LabelCap.ToString()
                    : "—", 0));
            section.Rows.Add(RowSummary("PersonalChronicle.UI.Floor.Detail.Bill.TargetCount",
                production != null && production.repeatMode == BillRepeatModeDefOf.RepeatCount
                    ? production.targetCount.ToString(CultureInfo.InvariantCulture)
                    : T("UnlimitedMark"), 0));
            section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Bill.Suspended",
                current != null ? BoolText(current.suspended) : "—", 0));

            // ---- 预览 9 行之外的真值行（保留：均原版直读且与玩家决策相关）----
            // `repeatCount`：Forever 模式**不递减**（原版语义），故 Forever 时显示 ∞ 而不是 0
            section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Bill.RepeatCount",
                production == null ? "—"
                    : (production.repeatMode == BillRepeatModeDefOf.Forever
                        ? T("UnlimitedMark")
                        : production.repeatCount.ToString(CultureInfo.InvariantCulture)), 0));
            // `paused` 在 **Bill_Production** 上（反编译核实：基类 Bill 无此成员）
            section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Bill.Paused",
                production != null ? BoolText(production.paused) : "—", 0));
            section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Bill.ShouldDoNow",
                current != null ? BoolText(current.ShouldDoNow()) : "—", 0));
            // `pawnRestriction` 是 **private 字段**，对外只有属性 **`PawnRestriction`**（反编译核实）
            section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Bill.PawnRestriction",
                current != null && current.PawnRestriction != null
                    ? current.PawnRestriction.LabelShort
                    : "—", 0));
            // S2-4：技能要求改**本地化技能名**（原为 `SkillDef.defName` 直显 ⇒ 内部逻辑键泄漏）
            section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Bill.SkillReq",
                recipe != null && recipe.skillRequirements != null && recipe.skillRequirements.Count > 0
                    ? string.Join(" · ", recipe.skillRequirements.ConvertAll(
                        sr => sr.skill != null ? sr.skill.LabelCap.ToString() : "—").ToArray())
                    : "—", 0));

            // S3-3（2026-09-23）：配料 = **本机全部工单需求合计** vs 殖民地库存。
            // CHG-20261002-FLOORDETAIL-AUDIT：**已移出本块** ⇒ 独立块 `BuildBomTab`（预览 HTML:837-850
            //   把配料与工单摘要列为同级块）。旧实现追加在本块内 ⇒ 本块默认折叠时配料不可见。

            // products[]：配方产出 —— CHG-20261002-RUNTAB-100PCT 对齐 HTML :820「产物 | 名 ×数」形态（原「产出 {0}」标签拆分形态废弃，键值同步去 {0}）。
            if (recipe != null && recipe.products != null && recipe.products.Count > 0)
            {
                for (int i = 0; i < recipe.products.Count; i++)
                {
                    ThingDefCountClass product = recipe.products[i];
                    if (product == null || product.thingDef == null)
                    {
                        continue;
                    }
                    FloorDetailRowView row = RowText(T("PersonalChronicle.UI.Floor.Detail.Bill.ProductLabel"),
                        product.thingDef.LabelCap.ToString() + " ×"
                            + product.count.ToString(CultureInfo.InvariantCulture), 0);
                    row.SourceKind = SourceApi;
                    section.Rows.Add(row);
                }
            }
            return section;
        }

        /// <summary>
        /// CHG-20261002-RUNTAB-100PCT：概览页空态值文案 —— 用户 2026-10-02 裁决「空机状态默认
        /// 字段内容『无』」，覆盖 LAND-009「—」占位惯例；**仅限 RunTab 15 行**（其它页签/块维持
        /// 「—」，避免全 mod 文案突变）。两语言键 ...Detail.EmptyValue（无/None）。
        /// </summary>
        private static string NoneValue
        {
            get { return T("PersonalChronicle.UI.Floor.Detail.EmptyValue"); }
        }

        /// <summary>
        /// CHG-20261002-RUNTAB-100PCT：倍率型车间加成的**百分比形态**（用户 2026-10-02 裁决）——
        /// 因子 0.85 → 「85 %」（×100 后最多两位小数；「空格+%」与既有「64 %」用法一致）。
        /// 概览页加成行与 KPI 卡第 4 格共用（同数据同形态）。品质加成 = int 档位非百分比，维持 +N。
        /// </summary>
        private static string FormatBonusPercent(float factor)
        {
            return (factor * 100f).ToString("0.##", CultureInfo.InvariantCulture) + " %";
        }

        /// <summary>布尔显示（与运行页同款小写字面量，属契约词）。</summary>
        private static string BoolText(bool value)
        {
            // S1-2（2026-09-23）：原为裸 true/false ⇒ 改走原版 Keyed（LOC-001，两语言都可读）。
            return value ? "Yes".Translate().ToString() : "No".Translate().ToString();
        }

        /// <summary>页签③人员：当前操作者 + 技能等级（真实 SkillRecord）。</summary>
        private static FloorDetailSectionView BuildCrewTab(Zone_Workshop zone, Thing thing)
        {
            FloorDetailSectionView section = new FloorDetailSectionView
            {
                Caption = T("PersonalChronicle.UI.Floor.Detail.Crew.Caption"),
                // S13 实机修复（2026-10-01）：块级 SourceApi 覆盖人员页多数行 ⇒ 块级徽标一次；
                //   但排名行(:841)/车间加成(:885)/综合系数(:888)/人时(:922)/加班(:925) 五行实值来自本 mod
                //   计算/累计（WorkshopCrewScoring 纯函数 / WorkshopBonusAccess / WorkshopOpsAccess.Digest）
                //   ⇒ SourceLedger(2)≠SourceApi(1)，经 Draw.cs:111 放行逐行冒标 LEDGER = 有意保留的
                //   真源差异（GOV-009），非噪音；旧注释误称「块等值规则自动抑制」与实值不符，特此更正
                //   （F5 · CHG-20261001-LAND-FLOORDETAIL；括注行号=裁决时点快照）。
                SourceKind = SourceApi,
                // S3-5：派工写面要按稳定 ID 找设备（工单页同源口径，GOV-009）
                BenchStableId = thing.def.defName + ":"
                    + thing.thingIDNumber.ToString(CultureInfo.InvariantCulture)
            };

            Map map = thing.Map;
            List<Pawn> candidates = map != null ? map.mapPawns.FreeColonistsSpawned : null;
            if (candidates == null || candidates.Count == 0)
            {
                section.EmptyHint = T("PersonalChronicle.UI.Floor.Detail.Crew.Empty");
                return section;
            }

            // S3-4（2026-09-23）：**胜任度排名（最佳优先）** —— 回答"谁最该上这台机器"。
            //   技能入参取自本台配方的**主技能**（RelevantSkills 的第一个）；
            //   打分走 Domain 纯函数 WorkshopCrewScoring（可离线单测）⇒ UI 只显示、不重算阈值。
            //   行内 PawnThingId 供窗口层画头像（快照不带 Pawn 引用）；Bar01 供画分数条。
            //   同时清理：删掉「24h 时段条未接入」「监管带宽未落地」两条常驻 SIM 说明行
            //   （审计列为界面噪音；按 LAND-009"无源不显示"处理，不占屏）。
            Pawn operatorPawn = FindOperator(thing);
            List<SkillDef> skills = RelevantSkills(thing);
            SkillDef primarySkill = skills.Count > 0 ? skills[0] : null;

            // 当前时段排班（Pawn_TimetableTracker.CurrentAssignment 直读 ⇒ 本地化显示）
            // CHG-20261002-FLOORDETAIL-AUDIT：**行集恒定** —— 预览的人员页是 18 行定表
            //   （HTML:862-889），无操作者时"当前工作 / 当前时段排班"整段消失会让列分栏与行序
            //   整体漂移（实机：无操作者时页面少两行、两栏重新切分）。与 RunTab 空态取值
            //   CHG-20261002-RUNTAB-EMPTYSTATE 同一纪律（LAND-009），此处空态维持「—」。
            string crewJobText = "—";
            string crewTimetableText = "—";
            if (operatorPawn != null)
            {
                if (operatorPawn.CurJobDef != null)
                {
                    System.Type driver = operatorPawn.CurJobDef.driverClass;
                    bool doBill = driver != null && driver.Name == "JobDriver_DoBill";
                    crewJobText = operatorPawn.CurJobDef.LabelCap.ToString()
                        + (doBill ? " · " + T("PersonalChronicle.UI.Floor.Detail.Crew.DoBill") : string.Empty);
                }
                if (operatorPawn.timetable != null && operatorPawn.timetable.CurrentAssignment != null)
                {
                    crewTimetableText = operatorPawn.timetable.CurrentAssignment.LabelCap.ToString();
                }
            }
            AddRow(section, "PersonalChronicle.UI.Floor.Detail.Crew.Job", crewJobText, 0, SourceApi);
            AddRow(section, "PersonalChronicle.UI.Floor.Detail.Crew.Timetable", crewTimetableText, 0, SourceApi);

            List<KeyValuePair<Pawn, int>> ranked =
                new List<KeyValuePair<Pawn, int>>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn pawn = candidates[i];
                if (pawn == null || pawn.Dead)
                {
                    continue;
                }
                SkillRecord record = primarySkill != null && pawn.skills != null
                    ? pawn.skills.GetSkill(primarySkill)
                    : null;
                int level = record != null ? record.Level : 0;
                int passion = record != null ? PassionLevelOf(record.passion) : 0;
                float mood = pawn.needs?.mood != null ? pawn.needs.mood.CurLevelPercentage : 0f;
                bool available = !pawn.Downed && pawn.Spawned;
                ranked.Add(new KeyValuePair<Pawn, int>(pawn,
                    WorkshopCrewScoring.Score(level, passion, mood, available)));
            }
            if (ranked.Count == 0)
            {
                section.EmptyHint = T("PersonalChronicle.UI.Floor.Detail.Crew.Empty");
                return section;
            }

            // 不可用（-1）排末尾；同分按名字序（稳定、不随遍历顺序抖动）
            ranked.Sort((a, b) =>
            {
                bool aOk = a.Value >= 0;
                bool bOk = b.Value >= 0;
                if (aOk != bOk)
                {
                    return aOk ? -1 : 1;
                }
                if (aOk && a.Value != b.Value)
                {
                    return b.Value.CompareTo(a.Value);
                }
                return string.CompareOrdinal(a.Key.LabelShort, b.Key.LabelShort);
            });

            section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Crew.RankBasis"),
                primarySkill != null ? primarySkill.LabelCap.ToString() : "—", 0));

            // S3-5（2026-09-23）：派工目标 = **BillStack 第一条**（原版语义：第一条就是下一个要做的工单）。
            //   行里只带"能不能派 + 派给谁"，**具体写入由窗口层经写面动作类执行**（快照不碰原版状态）。
            Bill targetBill = null;
            if (thing is IBillGiver billGiver && billGiver.BillStack != null
                && billGiver.BillStack.Count > 0)
            {
                targetBill = billGiver.BillStack[0];
            }
            Pawn assignedPawn = targetBill != null ? targetBill.PawnRestriction : null;
            // CHG-20261002-FLOORDETAIL-AUDIT：本行同样**恒占位**（无工单时值「—」，不整行消失），
            //   理由与上一条同（预览人员页是 18 行定表，行数漂移会带动两栏重排）。
            AddRow(section, "PersonalChronicle.UI.Floor.Detail.Crew.AssignHint",
                targetBill != null
                    ? T("PersonalChronicle.UI.Floor.Detail.Crew.AssignTarget") + " · " + targetBill.Label
                    : "—",
                0, SourceApi);

            for (int i = 0; i < ranked.Count; i++)
            {
                Pawn pawn = ranked[i].Key;
                int score = ranked[i].Value;
                SkillRecord record = primarySkill != null && pawn.skills != null
                    ? pawn.skills.GetSkill(primarySkill)
                    : null;
                bool isOperator = operatorPawn != null && ReferenceEquals(operatorPawn, pawn);
                bool isAssigned = assignedPawn != null && ReferenceEquals(assignedPawn, pawn);
                section.Rows.Add(new FloorDetailRowView
                {
                    // 批次 3（2026-09-23）：**序号列**（预览 `rk-n`）+ 标记 ——
                    //   ◆ = 本工单指定的生产人员（原版 `Bill.PawnRestriction`）；★ = 当前正在这台机器上干活的人
                    Label = (i + 1).ToString(CultureInfo.InvariantCulture) + ". "
                        + (isAssigned ? "◆ " : string.Empty)
                        + (isOperator ? "★ " : string.Empty) + pawn.LabelShort,
                    // 批次 3：值 = 分数 · 等级 · **当班状态**（预览 `rk-tag`；原来只有分数，看不出这人现在忙不忙）
                    Value = score >= 0
                        ? Translate2("PersonalChronicle.UI.Floor.Detail.Crew.RankValue",
                            score.ToString(CultureInfo.InvariantCulture),
                            (record != null ? record.Level : 0).ToString(CultureInfo.InvariantCulture))
                            + " · " + BuildDutyLabel(pawn, isOperator)
                        : T("PersonalChronicle.UI.Floor.Detail.Crew.Unavailable"),
                    Tone = score >= 0 ? 0 : 3,
                    PawnThingId = pawn.thingIDNumber,
                    Bar01 = score > 0 ? score / (float)WorkshopCrewScoring.MaxScore : 0f,
                    SourceKind = SourceLedger,
                    ActionKind = targetBill != null ? FloorDetailActionKinds.AssignPawn : 0,
                    ActionIndex = targetBill != null ? 0 : -1
                });
            }

            // 批次 3（2026-09-23）：**指派详情**（预览 `pickcard`）—— 当前工单 + 该目标的技能/心情/
            /// **影响幅度**（技能效果 × 车间加成 = 综合系数）。只显示一人（优先当前操作者，
            //   其次已指定者），避免整页铺开；点击排名行仍可直接改指派（S3-5）。
            Pawn pickPawn = operatorPawn ?? assignedPawn;
            if (pickPawn != null && targetBill != null && targetBill.recipe != null)
            {
                // P0（2026-09-25 审计 S8）：旧代码把**翻译键原样当文案**交给 RowText ⇒
                //   玩家界面直显 "PersonalChronicle.UI.Floor.Detail.Crew.Pick.Title"。
                //   RowText 收的是**已翻译文本**（Row() 才自己翻译键）⇒ 键必须先过 T()。
                section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Crew.Pick.Title"),
                    pickPawn.LabelShort + " · " + targetBill.recipe.LabelCap.ToString(), 0));
                SkillRecord pickRecord = primarySkill != null && pickPawn.skills != null
                    ? pickPawn.skills.GetSkill(primarySkill)
                    : null;
                if (primarySkill != null)
                {
                    AddRow(section, "PersonalChronicle.UI.Floor.Detail.Crew.Pick.Skill",
                        primarySkill.LabelCap.ToString() + " Lv"
                        + (pickRecord != null ? pickRecord.Level : 0).ToString(CultureInfo.InvariantCulture),
                        0, SourceApi);
                }
                AddRow(section, "PersonalChronicle.UI.Floor.Detail.Crew.Pick.Mood",
                    Mathf.RoundToInt(pickPawn.needs?.mood?.CurLevelPercentage * 100f ?? 0f)
                        .ToString(CultureInfo.InvariantCulture) + " %",
                    0, SourceApi);
                // 影响幅度：技能效果 = 该小人 `recipe.workSpeedStat` 的**当前值**（原版该 stat 基准为 1.0，
                //   技能/激情经 StatPart 注入 ⇒ 值即倍数；**不做环境因子估算**，与预览口径一致）；
                //   车间加成 = `WorkshopBonusAccess.WorkingBonusOf().workTimeFactor`（与加成条同源）。
                float skillFactor = 1f;
                if (targetBill.recipe.workSpeedStat != null)
                {
                    skillFactor = pickPawn.GetStatValue(targetBill.recipe.workSpeedStat);
                }
                WorkshopEffectiveBonus pickBonus = WorkshopBonusAccess.WorkingBonusOf(pickPawn);
                float shopFactor = pickBonus.workTimeFactor;
                AddRow(section, "PersonalChronicle.UI.Floor.Detail.Crew.Pick.SkillFactor",
                    "×" + skillFactor.ToString("0.00", CultureInfo.InvariantCulture), 0, SourceApi);
                AddRow(section, "PersonalChronicle.UI.Floor.Detail.Crew.Pick.ShopFactor",
                    "×" + shopFactor.ToString("0.00", CultureInfo.InvariantCulture), 0, SourceLedger);
                AddRow(section, "PersonalChronicle.UI.Floor.Detail.Crew.Pick.TotalFactor",
                    "×" + (skillFactor * shopFactor).ToString("0.00", CultureInfo.InvariantCulture),
                    0, SourceLedger);
            }

            // 批次 3：**本车间编制**（预览「本车间编制」表）—— 数据源 `WorkshopOpsAccess.Digest`
            //   （Application 契约，**零新采集**）+ `Zone_Workshop.Members`；人时/加班由 tick 换算成小时
            //   （原版 1 天 = 60000 tick ⇒ 1 h = 2500 tick）。
            if (zone != null && zone.Map != null)
            {
                List<Pawn> shopMembers = zone.Members;
                if (shopMembers != null && shopMembers.Count > 0)
                {
                    List<string> memberNames = new List<string>();
                    for (int i = 0; i < shopMembers.Count; i++)
                    {
                        memberNames.Add(shopMembers[i] != null ? shopMembers[i].LabelShort : "—");
                    }
                    AddRow(section, "PersonalChronicle.UI.Floor.Detail.Crew.MemberCount",
                        shopMembers.Count.ToString(CultureInfo.InvariantCulture) + " （"
                        + string.Join(" · ", memberNames.ToArray()) + "）", 0, SourceApi);
                }
                List<WorkshopCrew> shopCrews = zone.Crews;
                if (shopCrews != null && shopCrews.Count > 0)
                {
                    AddRow(section, "PersonalChronicle.UI.Floor.Detail.Crew.Roster",
                        T1("PersonalChronicle.UI.Floor.Detail.Crew.RosterValue", shopCrews.Count),
                        0, SourceApi);
                }
                // ⚠ `WorkshopOpsDigest` 是 **struct**（不可与 null 比较）⇒ 用 `MemberCount > 0`
                //   判"是否真有编制数据"；无编制时不报 0 h（那会显得"算过但没考勤"，实际是没数据）。
                WorkshopOpsDigest opsDigest = WorkshopOpsAccess.Digest(zone.Map, 15);
                if (opsDigest.MemberCount > 0)
                {
                    AddRow(section, "PersonalChronicle.UI.Floor.Detail.Crew.ManHours",
                        (opsDigest.ManHourTicks / 2500f).ToString("0.#", CultureInfo.InvariantCulture) + " h",
                        0, SourceLedger);
                    AddRow(section, "PersonalChronicle.UI.Floor.Detail.Crew.Overtime",
                        (opsDigest.OvertimeTicks / 2500f).ToString("0.#", CultureInfo.InvariantCulture) + " h",
                        0, SourceLedger);
                }
            }
            return section;
        }

        /// <summary>
        /// 激情档位 → 评分入参（0 = 无 / 1 = 次要 / 2 = 主要），与
        /// <see cref="PersonalChronicle.Domain.WorkshopCrewScoring.Score"/> 的约定一致。
        /// </summary>
        private static int PassionLevelOf(Passion passion)
        {
            switch (passion)
            {
                case Passion.Major: return 2;
                case Passion.Minor: return 1;
                default: return 0;
            }
        }

        /// <summary>热情档位（原版 `Passion` 枚举：Major/Minor/None）——契约词，不翻译。</summary>
        private static string PassionText(Passion passion)
        {
            switch (passion)
            {
                case Passion.Major: return "Major";
                case Passion.Minor: return "Minor";
                default: return "None";
            }
        }

        /// <summary>页签④健康：TPM（CompBreakdownable 直读）。</summary>
        private static FloorDetailSectionView BuildHealthTab(IBillGiver bench, Thing thing)
        {
            FloorDetailSectionView section = new FloorDetailSectionView
            {
                Caption = T("PersonalChronicle.UI.Floor.Detail.Health.Caption")
            };

            CompBreakdownable breakdown = thing.TryGetComp<CompBreakdownable>();
            CompQuality quality = thing.TryGetComp<CompQuality>();

            if (breakdown == null && quality == null)
            {
                section.EmptyHint = T("PersonalChronicle.UI.Floor.Detail.Health.Empty");
                return section;
            }

            // 耐久 HP —— **只画一次**（审计 D-1：工作台常同时挂 CompBreakdownable 与 CompQuality，
            // 原实现两个分支各画一行 ⇒ 界面上出现两行「耐久 HP」）。
            int hpRaw = Mathf.RoundToInt(thing.HitPoints);
            int maxHpNow = Mathf.RoundToInt(thing.MaxHitPoints);
            // P0（2026-09-25 审计 S14）：`MaxHitPoints` 是**派生值**（受品质 / 材料等 stat 影响），
            //   引擎在因子变化后不回夹 HitPoints ⇒ 真实存在 "187 / 180" 这种 >100% 的状态。
            //   显示层如实钳到上限（不编造数），并用告警语气（2）标记，避免渲染出 104% 的误导读数。
            bool hpOverflow = maxHpNow > 0 && hpRaw > maxHpNow;
            int hpNow = hpOverflow ? maxHpNow : hpRaw;
            FloorDetailRowView hpRow = RowText(T("PersonalChronicle.UI.Floor.Detail.Health.HitPoints"),
                hpNow + " / " + maxHpNow,
                hpOverflow ? 2 : (maxHpNow > 0 && hpNow / (float)maxHpNow < 0.4f ? 2 : 0));
            // 批次1（2026-09-23）：预览要求**耐久条**（不只两位数字）—— 复用行内条（`Bar01`），
            //   窗口层 `DrawRows` 已在值左侧绘制；比例即"剩多少能修"，比读数直观。
            hpRow.Bar01 = maxHpNow > 0 ? Mathf.Clamp01(hpNow / (float)maxHpNow) : 0f;
            section.Rows.Add(hpRow);

            if (breakdown != null)
            {
                // 批次1（2026-09-23）：值原为 `BrokenDown` / `ok`（内部 token 直显）⇒ 改本地化，
                //   复用运行块的「故障 / 正常」键（同义同源，不另造文案）。
                section.Rows.Add(Row("PersonalChronicle.UI.Floor.Detail.Health.CompBreakdownable",
                    breakdown.BrokenDown
                        ? T("PersonalChronicle.UI.Floor.Detail.Run.Faulted")
                        : T("PersonalChronicle.UI.Floor.Detail.Run.Normal"),
                    breakdown.BrokenDown ? 3 : 1));

                // CanBreakdownNow 是 **protected**（已用 Cecil 核实）⇒ 外部读不到。
                // 新预览要求"如实说明"⇒ 单列一行标 SIM，写明不可读，而不是留空让人以为是 0。
                AddRow(section, "PersonalChronicle.UI.Floor.Detail.Health.CanBreakdown",
                    T("PersonalChronicle.UI.Floor.Detail.Health.CanBreakdownUnavailable"), 0, SourceSim);

                // 可燃性 / 劣化速率：原版 StatDef 直读（API）
                AddRow(section, "PersonalChronicle.UI.Floor.Detail.Health.Flammability",
                    thing.GetStatValue(StatDefOf.Flammability).ToString("0.00",
                        CultureInfo.InvariantCulture), 0, SourceApi);
                AddRow(section, "PersonalChronicle.UI.Floor.Detail.Health.DeteriorationRate",
                    thing.GetStatValue(StatDefOf.DeteriorationRate).ToString("0.0",
                        CultureInfo.InvariantCulture), 0, SourceApi);
            }

            if (quality != null)
            {
                // 耐久 HP 已在分支外统一画过（审计 D-1 修复），此处不再重复
                section.Rows.Add(RowText(T("PersonalChronicle.UI.Floor.Detail.Health.Quality"),
                    quality.Quality.GetLabel(), 0));

                // 批次 4（2026-09-23）：**品质分布接真值** —— 本 mod 台账近 15 日产出，**按本台配方过滤**。
                //   口径与 KPI 良品率**同源**（同一条 `ItemProduced` 事件流 + 同一个 `ExamScoring.QualityRank`）；
                //   数据经 Application 契约 `WorkshopQualityAccess`（不直读台账，依赖方向合规）。
                //   ⚠ 台账事件属**人**（谁做的），不含"哪台设备做的" ⇒ 只能按**配方**近似、无法严格按台；
                //     文案如实写明（LAND-009），不与"单台"混。
                section.QualityLabels.AddRange(QualityLabels());
                Bill qualBill = bench != null ? FindActiveBill(bench.BillStack) : null;
                string qualRecipe = qualBill != null && qualBill.recipe != null
                    ? qualBill.recipe.defName
                    : null;
                WorkshopQualityDigest qualDigest =
                    WorkshopQualityAccess.Digest(thing.Map, qualRecipe, 15);
                if (qualDigest.HasData && qualDigest.Total > 0 && qualDigest.Bins != null)
                {
                    section.QualityHistogram.Clear();
                    for (int i = 0; i < qualDigest.Bins.Length; i++)
                    {
                        section.QualityHistogram.Add(qualDigest.Bins[i]);
                    }
                    AddRow(section, "PersonalChronicle.UI.Floor.Detail.Health.QualityTotal",
                        qualDigest.Total.ToString(CultureInfo.InvariantCulture), 0, SourceLedger);
                    int goodPct = Mathf.RoundToInt(qualDigest.Good * 100f / qualDigest.Total);
                    AddRow(section, "PersonalChronicle.UI.Floor.Detail.Health.QualityGood",
                        qualDigest.Good.ToString(CultureInfo.InvariantCulture) + " （"
                        + goodPct.ToString(CultureInfo.InvariantCulture) + " %）", 0, SourceLedger);
                    AddRow(section, "PersonalChronicle.UI.Floor.Detail.Health.QualityScope",
                        "—", 0, SourceSim);
                }
            }
            return section;
        }

        // ---- 小工具 ----

        /// <summary>
        /// 当前工单口径**唯一实现已迁入** <see cref="WorkshopDeviceReader.FindActiveBill"/>
        /// （设备详情弹窗与设备卡共用一份；本包装保留以便既有调用点零改动）。
        /// </summary>
        private static Bill FindActiveBill(BillStack stack)
        {
            return WorkshopDeviceReader.FindActiveBill(stack);
        }

        private static Pawn FindOperator(Thing thing)
        {
            if (thing == null || thing.Map == null)
            {
                return null;
            }
            IReadOnlyList<Pawn> pawns = thing.Map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.jobs == null)
                {
                    continue;
                }
                if (pawn.jobs.curDriver is JobDriver_DoBill driver
                    && (Thing)driver.BillGiver == thing)
                {
                    return pawn;
                }
            }
            return null;
        }

        private static List<SkillDef> RelevantSkills(Thing thing)
        {
            List<SkillDef> skills = new List<SkillDef>();
            IBillGiver bench = thing as IBillGiver;
            BillStack stack = bench != null ? bench.BillStack : null;
            Bill bill = FindActiveBill(stack);
            RecipeDef recipe = bill != null ? bill.recipe : null;
            if (recipe != null && recipe.workSkill != null)
            {
                skills.Add(recipe.workSkill);
            }
            if (recipe != null && recipe.workSkillLearnFactor > 0f && recipe.workSkill != null
                && !skills.Contains(recipe.workSkill))
            {
                skills.Add(recipe.workSkill);
            }
            return skills;
        }

        private static string ResolveWorkLeftText(Thing thing)
        {
            Pawn worker = FindOperator(thing);
            if (worker == null)
            {
                return "—";
            }
            try
            {
                JobDriver_DoBill driver = worker.jobs?.curDriver as JobDriver_DoBill;
                if (driver == null)
                {
                    return "—";
                }
                return Mathf.RoundToInt(driver.workLeft).ToString(CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return "—";
            }
        }

        private static List<string> QualityLabels()
        {
            List<string> labels = new List<string>(QualityOrder.Length);
            for (int i = 0; i < QualityOrder.Length; i++)
            {
                labels.Add(QualityOrder[i].GetLabel());
            }
            return labels;
        }

        /// <summary>
        /// CHG-20261002-PRODCOLLAPSE：**摘要行**（折叠面）—— 语义 = 预览「摘要 3 行」
        /// （配方 / 基础工时 / 重复模式·目标数）；窗口折叠态只显示带本标志的行。
        /// </summary>
        private static FloorDetailRowView RowSummary(string labelKey, string value, int tone)
        {
            FloorDetailRowView row = Row(labelKey, value, tone);
            row.IsSummary = true;
            return row;
        }

        private static FloorDetailRowView Row(string labelKey, string value, int tone)
        {
            return new FloorDetailRowView { Label = T(labelKey), Value = value, Tone = tone };
        }

        private static FloorDetailRowView RowText(string label, string value, int tone)
        {
            return new FloorDetailRowView { Label = label, Value = value, Tone = tone };
        }

        /// <summary>
        /// 内部标识形串判定：如 "MechanicalPower.cycle.05 / 283.8 Wd" ——
        /// " / " 左侧是 ≥2 段点分标识符（字母开头，仅字母/数字/下划线/点）。
        /// 原版检视文本是自然语言，不会匹配。
        /// </summary>
        private static bool LooksLikeInternalToken(string line)
        {
            int slash = line.IndexOf(" / ", StringComparison.Ordinal);
            if (slash <= 0)
            {
                return false;
            }
            string head = line.Substring(0, slash);
            if (!char.IsLetter(head[0]))
            {
                return false;
            }
            int dots = 0;
            for (int i = 0; i < head.Length; i++)
            {
                char ch = head[i];
                if (ch == '.')
                {
                    dots++;
                    continue;
                }
                if (!char.IsLetterOrDigit(ch) && ch != '_')
                {
                    return false;
                }
            }
            return dots >= 2;
        }

        /// <summary>两段**文本**占位（与 <c>T2</c> 的整数重载区分，避免 string→int 隐式失败）。</summary>
        private static string Translate2(string key, string arg0, string arg1)
        {
            return key.Translate(arg0, arg1).ToString();
        }

        /// <summary>单段文本占位。</summary>
        private static string Translate1(string key, string arg0)
        {
            return key.Translate(arg0).ToString();
        }
    }
}

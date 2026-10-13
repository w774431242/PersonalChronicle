using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Application.SystemCenters;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.MasterControl
{
    /// <summary>
    /// Partial of <see cref="MasterControlTabWindow"/> ——**仅开发者模式**的「随机数据填充」。
    ///
    /// <para>用途：殖民地还没有设备 / 产出时，KPI 全是「—」，无法检查卡片排版（截断、换行、
    /// 分档、语气配色）。本分片提供一个开关：打开后把快照里的<b>数值</b>换成随机数，
    /// 专门用来看排版；关掉即恢复真实数据。</para>
    ///
    /// <para>隔离约束（LAND-009「数据缺口如实」的可审计例外）：</para>
    /// <list type="bullet">
    /// <item>按钮<b>只在 <c>Prefs.DevMode</c> 为真时绘制</b>，填充方法内再判一次 ——
    /// 运行中关掉开发者模式会立即失效并复位开关，普通玩家不可能触发；</item>
    /// <item>只改<b>数值 / 占比 / 语气</b>；标签与「口径说明」仍来自真实本地化键，
    /// 只有「缺数据的原因」（无设备 / 机制未落地）会随填充一起清空，避免
    /// 「42% / 无设备」这种自相矛盾的展示；</item>
    /// <item>不写任何持久状态：开关是窗口瞬时字段，不入存档、不进 Def、不影响 Provider；</item>
    /// <item>整个文件可整份删除而不影响生产路径（唯一触点 = 页头一个按钮 + 快照重建后一行调用）。</item>
    /// </list>
    /// </summary>
    public sealed partial class MasterControlTabWindow
    {
        /// <summary>随机填充开关（瞬时状态；非 DevMode 时恒为 false）。</summary>
        private bool devRandomFill;

        /// <summary>
        /// 页头「随机数据」按钮（自右向左排布的最后一个）。非开发者模式直接返回原右边界，
        /// 连按钮都不画（而不是画一个灰按钮）—— 避免普通玩家看到不存在的能力。
        /// </summary>
        private float DrawDevRandomButton(float y, float btnHeight, float btnGap, float right)
        {
            if (!Prefs.DevMode)
            {
                devRandomFill = false;
                return right;
            }

            string label = Translate("PersonalChronicle.UI.MasterControl.Header.DevRandom")
                + (devRandomFill ? " ✓" : string.Empty);
            float width = MeasureWidth(label, GameFont.Small) + 24f;
            Rect btn = new Rect(right - width, y, width, btnHeight);
            if (Widgets.ButtonText(btn, label))
            {
                devRandomFill = !devRandomFill;
                ForceRefresh();
            }
            TooltipHandler.TipRegion(btn,
                Translate("PersonalChronicle.UI.MasterControl.Header.DevRandomTip"));
            return btn.x - btnGap;
        }

        /// <summary>
        /// 快照重建后调用：开关打开时用随机数覆盖数值字段。
        /// 每次刷新都重新填（跟随 300 tick 节流），顺便验证刷新链路是否活着。
        /// </summary>
        private void ApplyDevRandomFillIfNeeded()
        {
            if (!devRandomFill)
            {
                return;
            }
            if (!Prefs.DevMode)
            {
                // 运行中关掉开发者模式：立即复位，不留任何"假数据"残留。
                devRandomFill = false;
                ForceRefresh();
                return;
            }

            if (cachedList != null)
            {
                FillCardHeadKpis(cachedList.InitialCenters);
                FillCardHeadKpis(cachedList.ReservedCenters);
            }
            if (cachedDetail != null)
            {
                // ⚠ 2026-10-06（用户指令「kpi 也要移除」）：原 `FillKpiValues(cachedDetail.Kpis)` 已删除
                //   —— 制造中心不再产出 KPI（`AppendKpis` 空实现），无卡可填。
                FillWorkshopSnapshot(cachedDetail.Workshop);
                FillAnalysisSnapshot(cachedDetail.Analysis);
                FillOperationsSnapshot(cachedDetail.Operations);
            }
        }

        /// <summary>
        /// Dev-Fill（④ 运营）：只在**真实数据为空**时补（成品库逐件清单取真实 ThingDef 名 + 真实品质文案），
        /// 工人名单保持真实（名字/技能来自殖民者），只随机心情 —— 尽量少编造。
        /// </summary>
        private static void FillOperationsSnapshot(ManufacturingOperationsSnapshot ops)
        {
            if (ops == null)
            {
                return;
            }

            if (ops.DepotEmpty)
            {
                List<ThingDef> samples = DevSampleBenches();
                if (samples.Count > 0)
                {
                    QualityCategory[] qualities =
                    {
                        QualityCategory.Awful, QualityCategory.Normal, QualityCategory.Good,
                        QualityCategory.Excellent, QualityCategory.Masterwork
                    };
                    ops.DepotItems.Clear();
                    int rows = Mathf.Min(5, samples.Count);
                    for (int i = 0; i < rows; i++)
                    {
                        QualityCategory quality = qualities[i % qualities.Length];
                        ops.DepotItems.Add(new OpsDepotItemView
                        {
                            QualityLabel = quality.GetLabel(),
                            QualityTone = quality >= QualityCategory.Excellent
                                ? SystemCenterTone.Good
                                : (quality >= QualityCategory.Good ? SystemCenterTone.Flat : SystemCenterTone.Warn),
                            Name = DefLabel(samples[i]),
                            CountText = "×" + Rand.RangeInclusive(1, 40).ToString(CultureInfo.InvariantCulture)
                        });
                    }
                    ops.DepotEmpty = false;
                    ops.DepotCountText = Translate("PersonalChronicle.UI.Ops.Depot.Count", ops.DepotItems.Count);
                }
            }

            for (int i = 0; i < ops.Workers.Count; i++)
            {
                OpsWorkerView worker = ops.Workers[i];
                int mood = Rand.RangeInclusive(15, 100);
                worker.Mood01 = mood / 100f;
                worker.MoodText = mood.ToString(CultureInfo.InvariantCulture) + "%";
                worker.MoodTone = mood > 70
                    ? SystemCenterTone.Good
                    : (mood > 50 ? SystemCenterTone.Warn : SystemCenterTone.Bad);
            }
        }

        /// <summary>Dev-Fill（③ 分析带）：填瓶颈/损失/趋势/WIP 的**数值**（文案仍取真实 Keyed）。</summary>
        private static void FillAnalysisSnapshot(ManufacturingAnalysisSnapshot analysis)
        {
            if (analysis == null)
            {
                return;
            }

            List<ThingDef> samples = DevSampleBenches();
            if (samples.Count == 0)
            {
                return;
            }

            // 瓶颈带：从真实 ThingDef 名取前 6 台，工量随机 → 最长者为瓶颈
            analysis.Bottlenecks.Clear();
            int segments = Mathf.Min(6, samples.Count);
            double total = 0d;
            double[] works = new double[segments];
            for (int i = 0; i < segments; i++)
            {
                works[i] = Rand.Range(20f, 260f);
                total += works[i];
            }
            int bottleneck = 0;
            for (int i = 0; i < segments; i++)
            {
                if (works[i] > works[bottleneck])
                {
                    bottleneck = i;
                }
                analysis.Bottlenecks.Add(new AnalysisBottleneckView
                {
                    Name = DefLabel(samples[i]),
                    ValueText = Translate("PersonalChronicle.UI.Analysis.Bottleneck.Value",
                        Mathf.RoundToInt((float)works[i])),
                    Ratio01 = Mathf.Clamp((float)(works[i] / total), 0.02f, 1f),
                    IsBottleneck = false
                });
            }
            // 重建一次 IsBottleneck（上面循环时还不知道谁是瓶颈）
            analysis.Bottlenecks[bottleneck].IsBottleneck = true;
            analysis.BottleneckSummary = Translate2("PersonalChronicle.UI.Analysis.Bottleneck.Summary",
                DefLabel(samples[bottleneck]), Mathf.RoundToInt((float)works[bottleneck]));

            // 损失：只填"已接入"项，未接入项保持不可用（验证「—」呈现）
            for (int i = 0; i < analysis.Losses.Count; i++)
            {
                AnalysisLossView row = analysis.Losses[i];
                if (!row.Available)
                {
                    continue;
                }
                int value = Rand.RangeInclusive(0, 9);
                row.ValueText = value.ToString(CultureInfo.InvariantCulture);
                row.Ratio01 = value / 9f;
            }

            // 趋势：6 个随机点（验证折线与面积绘制）
            analysis.Trend.Clear();
            for (int i = 0; i < 6; i++)
            {
                analysis.Trend.Add(Rand.Value);
            }
            analysis.TrendAvailable = true;
            analysis.TrendNowText = Translate("PersonalChronicle.UI.Analysis.Trend.Sub", Rand.RangeInclusive(3, 60));

            // WIP：已接入项填随机数
            for (int i = 0; i < analysis.Wip.Count; i++)
            {
                AnalysisWipView cell = analysis.Wip[i];
                if (cell.Available)
                {
                    cell.Value = Rand.RangeInclusive(0, 40).ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        // GOV-006 硬编码登记：分类 = Dev-Fill 样本设备的 defName（稳定 ID，**不是显示文本**）；
        // 来源 = 原版工作台 Def；生命周期 = 用 GetNamedSilentFail 取，原版改名/删除时自动跳过。
        private static readonly string[] DevSampleBenchDefNames =
        {
            "TableMachining", "TableSmithing", "TableStonecutter", "ElectricStove",
            "ElectricSmelter", "ElectricSmithy", "TableSculpting", "HandTailorBench",
            "FabricationBench", "TableButcher", "DrugLab", "CraftingSpot",
            "SimpleResearchBench", "HiTechResearchBench", "FueledSmithy", "FueledStove"
        };

        /// <summary>
        /// Dev-Fill（车间俯视）：把 12 个格位填满，用来在没有车间/没有设备的存档里验排版。
        ///
        /// <para>仍然遵守 LAND §7.1 的「范围」约束：**所有文字取自真实本地化来源** ——
        /// 格名/设备名 = 原版 <see cref="ThingDef.label"/>，状态 = Keyed 状态键，
        /// 人员首字 = 真实殖民者 <c>LabelShort</c>；只有"有没有格/有没有设备/数值多少"是编的。</para>
        /// </summary>
        /// <summary>Dev-Fill 样本设备：从原版工作台 defName 取真实 ThingDef（名字用其 label，本地化）。</summary>
        private static List<ThingDef> DevSampleBenches()
        {
            List<ThingDef> benches = new List<ThingDef>();
            for (int i = 0; i < DevSampleBenchDefNames.Length; i++)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(DevSampleBenchDefNames[i]);
                if (def != null)
                {
                    benches.Add(def);
                }
            }
            return benches;
        }

        private static void FillWorkshopSnapshot(WorkshopFloorSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            List<ThingDef> benches = DevSampleBenches();
            if (benches.Count == 0)
            {
                return;
            }

            List<string> initials = ColonistInitials();
            snapshot.NoMap = false;
            snapshot.Bays.Clear();
            snapshot.Devices.Clear();
            snapshot.Workers.Clear();

            int deviceCount = 0;
            for (int slot = 0; slot < WorkshopFloorSnapshot.BaySlots; slot++)
            {
                ThingDef bayDef = benches[slot % benches.Count];
                snapshot.Bays.Add(new FloorBayView
                {
                    Slot = slot,
                    Occupied = true,
                    Number = (slot + 1).ToString(CultureInfo.InvariantCulture),
                    Name = DefLabel(bayDef),
                    TypeLabel = bayDef.defName,
                    Meta = Translate("PersonalChronicle.UI.Floor.BayMeta",
                        WorkshopFloorSnapshot.DevicesPerBay),
                    Highlight = true
                });

                for (int i = 0; i < WorkshopFloorSnapshot.DevicesPerBay; i++)
                {
                    ThingDef def = benches[(slot + i) % benches.Count];
                    EquipmentState state = (EquipmentState)Rand.RangeInclusive(0, 3);
                    string stateKey = EquipmentStateResolver.KeyOf(state);
                    string stateText = Translate(EquipmentStateText.TranslationKeyOf(stateKey));
                    int bills = Rand.RangeInclusive(0, 3);
                    string power = Mathf.RoundToInt(Rand.Range(80f, 600f))
                        .ToString(CultureInfo.InvariantCulture) + "W";

                    FloorDeviceView device = new FloorDeviceView
                    {
                        Slot = slot,
                        Index = i,
                        Name = DefLabel(def) + (i == 0 ? " A" : " B"),
                        StateKey = stateKey,
                        StateText = stateText,
                        PowerText = power,
                        SubText = bills > 0
                            ? Translate("PersonalChronicle.UI.Floor.BillCount", bills)
                            : Translate("PersonalChronicle.UI.Floor.NoBill"),
                        OperatorText = initials[(slot * 2 + i) % initials.Count],
                        Progress01 = state == EquipmentState.Run ? Rand.Value : -1f
                    };
                    device.Tooltip = device.Name + "\n" + stateText + " · " + power;
                    snapshot.Devices.Add(device);
                    deviceCount++;
                }

                for (int w = 0; w < 2; w++)
                {
                    snapshot.Workers.Add(new FloorWorkerView
                    {
                        Slot = slot,
                        Index = w,
                        Initial = initials[(slot * 2 + w + 1) % initials.Count]
                    });
                }
            }

            snapshot.WorkshopCount = WorkshopFloorSnapshot.BaySlots;
            snapshot.DeviceCount = deviceCount;
            snapshot.Caption = Translate2("PersonalChronicle.UI.Floor.Caption",
                snapshot.WorkshopCount, deviceCount);
            snapshot.Hint = string.Empty;

            // 两侧轨道（原料库 / 成品库）也填上，验证轨道排版（名与品质取原版本地化文本）
            snapshot.RawItems.Clear();
            int rawRows = Mathf.Min(5, benches.Count);
            for (int i = 0; i < rawRows; i++)
            {
                int need = Rand.RangeInclusive(20, 200);
                int have = Rand.RangeInclusive(0, 320);
                snapshot.RawItems.Add(new FloorRawItemView
                {
                    Name = DefLabel(benches[i]),
                    CountText = have.ToString("N0", CultureInfo.InvariantCulture),
                    Note = Translate("PersonalChronicle.UI.Floor.Raw.Demand", need),
                    Ratio01 = Mathf.Clamp01(have / (float)need),
                    Low = have < need
                });
            }
            snapshot.Depot.TotalText = Translate("PersonalChronicle.UI.Floor.Depot.Total",
                Rand.RangeInclusive(1, 240));
            snapshot.Depot.Empty = false;
            snapshot.Depot.TopItems.Clear();
            for (int i = 0; i < 4 && i < benches.Count; i++)
            {
                snapshot.Depot.TopItems.Add(DefLabel(benches[i]) + "  "
                    + Rand.RangeInclusive(1, 60).ToString(CultureInfo.InvariantCulture));
            }
            snapshot.Depot.QualityChips.Clear();
            snapshot.Depot.QualityChips.Add(QualityCategory.Normal.GetLabel() + " 12");
            snapshot.Depot.QualityChips.Add(QualityCategory.Good.GetLabel() + " 8");
            snapshot.Depot.QualityChips.Add(QualityCategory.Excellent.GetLabel() + " 3");

            FillRoutes(snapshot);
        }

        /// <summary>Dev-Fill（工序链路）：按真实 <see cref="RouteDef"/> 的格序造一条链路，流量/在制随机。</summary>
        private static void FillRoutes(WorkshopFloorSnapshot snapshot)
        {
            snapshot.Routes.Clear();
            List<RouteDef> defs = DefDatabase<RouteDef>.AllDefsListForReading;
            RouteDef def = defs != null && defs.Count > 0 ? defs[0] : null;
            List<int> slots = def != null && def.steps != null && def.steps.Count >= 2
                ? def.steps
                : null;
            if (slots == null)
            {
                slots = new List<int>();
                for (int i = 0; i < WorkshopFloorSnapshot.BaySlots; i++)
                {
                    slots.Add(i);
                }
            }

            FloorRouteView route = new FloorRouteView
            {
                Name = def != null && !string.IsNullOrEmpty(def.label) ? def.label : string.Empty,
                NameEn = def != null ? def.nameEn : string.Empty,
                CycleSeconds = def != null && def.cycleSeconds > 0.1f ? def.cycleSeconds : 6f,
                HasFeed = def == null || def.feedFromRawStore,
                EndFlowCount = Rand.RangeInclusive(1, 24)
            };
            for (int i = 0; i < slots.Count; i++)
            {
                int flow = Rand.RangeInclusive(0, 12);
                route.Steps.Add(new FloorRouteStepView
                {
                    Slot = slots[i],
                    StepText = (i + 1).ToString(CultureInfo.InvariantCulture),
                    FlowCount = flow,
                    FlowRatio01 = flow / 12f,
                    WipCount = Rand.RangeInclusive(0, 4)
                });
            }
            route.EndText = Translate("PersonalChronicle.UI.Floor.Route.End", route.EndFlowCount);
            snapshot.Routes.Add(route);
        }

        private static string DefLabel(ThingDef def)
        {
            return string.IsNullOrEmpty(def.label) ? def.defName : def.label;
        }

        /// <summary>真实殖民者的姓名首字（没有殖民者时回落 '?'，不编造人名）。</summary>
        private static List<string> ColonistInitials()
        {
            List<string> initials = new List<string>();
            Map map = Find.CurrentMap;
            List<Pawn> colonists = map != null && map.mapPawns != null
                ? map.mapPawns.FreeColonistsSpawned
                : null;
            if (colonists != null)
            {
                for (int i = 0; i < colonists.Count; i++)
                {
                    Pawn pawn = colonists[i];
                    string label = pawn != null ? pawn.LabelShort : null;
                    if (!string.IsNullOrEmpty(label))
                    {
                        initials.Add(label.Substring(0, 1));
                    }
                }
            }
            if (initials.Count == 0)
            {
                initials.Add("?");
            }
            return initials;
        }

        private static void FillCardHeadKpis(List<SystemCenterCardView> cards)
        {
            if (cards == null)
            {
                return;
            }
            for (int i = 0; i < cards.Count; i++)
            {
                SystemCenterCardView card = cards[i];
                if (card == null || string.IsNullOrEmpty(card.HeadKpiLabel))
                {
                    // 没有数据源的中心没有 KPI 标签（「预留 · 未接入」是真实状态，不是数据缺口），
                    // 不为它编一个指标名。
                    continue;
                }
                card.HeadKpiValue = SystemCenterKpiFactory.FormatPercent(Rand.Value);
                card.HeadKpiTone = RandomTone();
            }
        }

        // ⚠ 2026-10-06（用户指令「kpi 也要移除。确保干净」）：原 `FillKpiValues`（把快照里的 KPI 卡
        //   填成随机演示值）**方法体一并删除** —— 制造中心不再产出 KPI，无卡可填。
        //   ⚠ 教训：本轮首版只删了调用点、漏删方法体 ⇒ 被 GATE-UI 守门记为 P2「死方法残留」。
        //   删除本类任何"演示填充"时，**调用点与方法体必须同时删**（编译不会为未用私有方法报警）。

        /// <summary>随机语气：只用于让四种配色都能被看到，不复制 Provider 的业务阈值。</summary>
        private static SystemCenterTone RandomTone()
        {
            return (SystemCenterTone)Rand.RangeInclusive(0, 3);
        }
    }
}

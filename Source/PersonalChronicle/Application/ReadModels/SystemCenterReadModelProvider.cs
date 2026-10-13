using System;
using System.Collections.Generic;
using System.Text;
using PersonalChronicle.Application.SystemCenters;
using PersonalChronicle.Application.SystemCenters.Builtin;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 总控中心 Read Model 构建器：把「中心注册表（Def）+ 中心数据源（Provider）+
    /// 殖民地现状」聚合成不可变快照，供 UI 只读消费。
    ///
    /// <para>契约（08c 数据层契约 / DL-*）：</para>
    /// <list type="bullet">
    /// <item>快照字段<b>全部预本地化、预格式化</b>，UI 绘制期不做字符串拼接与翻译；</item>
    /// <item>中心接入状态（运行中 / 数据未接入 / 预留）在构建期由「Def 预留位 + 注册表里有无 Provider」
    /// 推导，UI 只读结果、不参与判定（Def 不存运行态，GOV-003）；</item>
    /// <item>失败隔离：任何异常 → <see cref="ArchiveSectionSnapshot.IsEmpty"/> = true，
    /// 不让异常进入绘制循环；</item>
    /// <item>本类<b>无状态</b>，可被任意窗口实例持有，不参与存档。</item>
    /// </list>
    /// </summary>
    public sealed class SystemCenterReadModelProvider
    {
        private readonly SystemCenterDataRegistry dataRegistry;

        public SystemCenterReadModelProvider(SystemCenterDataRegistry dataRegistry)
        {
            this.dataRegistry = dataRegistry;
        }

        /// <summary>总览快照：殖民地现状 + 初始中心卡片 + 预留中心卡片。</summary>
        public SystemCenterListSnapshot BuildList()
        {
            SystemCenterListSnapshot snapshot = new SystemCenterListSnapshot();
            try
            {
                Map map = Find.CurrentMap;
                IReadOnlyList<SystemCenterDef> initial = SystemCenterRegistry.InitialCenters;
                IReadOnlyList<SystemCenterDef> reserved = SystemCenterRegistry.ReservedCenters;

                for (int i = 0; i < initial.Count; i++)
                {
                    snapshot.InitialCenters.Add(BuildCard(initial[i], map));
                }
                for (int i = 0; i < reserved.Count; i++)
                {
                    // 预留中心不接数据源：用样例数值填充与否是原则问题，一律显示清单态。
                    snapshot.ReservedCenters.Add(BuildCard(reserved[i], null));
                }

                snapshot.IsEmpty = snapshot.InitialCenters.Count == 0 && snapshot.ReservedCenters.Count == 0;
                snapshot.BuiltFromRevision = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Ui, "SystemCenter list snapshot failed: " + ex);
                snapshot.IsEmpty = true;
            }
            return snapshot;
        }

        /// <summary>
        /// 设备详情弹窗快照（对齐预览 <c>#drawer</c>）。
        ///
        /// <para>与 <see cref="BuildDetail"/> 走同一套「中心 → 车间视图契约」解析路径，
        /// UI 不关心是哪个中心（LAND-010）：只判返回的
        /// <see cref="FloorDeviceDetailSnapshot.HasDevice"/> 决定弹不弹窗。
        /// 实现方在 Automation 模块（<c>IWorkshopViewDataProvider</c>）。</para>
        /// </summary>
        public FloorDeviceDetailSnapshot BuildDeviceDetail(string centerDefName, int slot, int index)
        {
            FloorDeviceDetailSnapshot detail = new FloorDeviceDetailSnapshot();
            try
            {
                SystemCenterDef def = SystemCenterRegistry.GetNamed(centerDefName);
                if (def == null || def.reserved || dataRegistry == null)
                {
                    return detail;
                }
                IWorkshopViewDataProvider workshopView = dataRegistry.GetWorkshopView(def.defName);
                Map map = Find.CurrentMap;
                if (workshopView == null || map == null)
                {
                    return detail;
                }
                return workshopView.BuildDeviceDetail(map, slot, index);
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Ui, "Device detail snapshot failed: " + ex);
                detail.HasDevice = false;
                return detail;
            }
        }

        /// <summary>中心详情快照；未知中心返回空快照（UI 回退总览）。</summary>
        public SystemCenterDetailSnapshot BuildDetail(string centerDefName,
            ColonyStockViewRequest request = default(ColonyStockViewRequest))
        {
            SystemCenterDetailSnapshot snapshot = new SystemCenterDetailSnapshot();
            try
            {
                SystemCenterDef def = SystemCenterRegistry.GetNamed(centerDefName);
                if (def == null)
                {
                    snapshot.IsEmpty = true;
                    return snapshot;
                }

                Map map = Find.CurrentMap;
                snapshot.Card = BuildCard(def, def.reserved ? null : map);
                CopyInto(def.responsible, snapshot.Responsible);
                CopyInto(def.delegated, snapshot.Delegated);
                CopyInto(def.emits, snapshot.Emits);
                CopyInto(def.consumes, snapshot.Consumes);
                CopyInto(def.dependsOn, snapshot.DependsOn);
                CopyInto(def.provides, snapshot.Provides);
                CopyInto(def.hooks, snapshot.Hooks);

                ISystemCenterDataProvider provider = def.reserved || dataRegistry == null
                    ? null
                    : dataRegistry.Get(def.defName);

                // KPI 区块标题由 Provider 声明（如制造中心「① 总控 KPI · 运行效率」），
                // 未声明时回落通用标题；键→文本的翻译在这里完成，UI 不做翻译。
                snapshot.KpiSectionTitle = Translate(
                    provider != null && !string.IsNullOrEmpty(provider.KpiSectionTitleKey)
                        ? provider.KpiSectionTitleKey
                        : "PersonalChronicle.UI.MasterControl.Section.Kpi");

                // 清单章节的可见性由 Provider 声明：无 Provider（预留中心 / 未接入）时照常展示，
                // 因为那时清单是页面唯一内容。
                snapshot.ShowManifest = provider == null || provider.ShowsCenterManifest;

                // KPI 分行分组（如 ① 的 [4,5]）：由 Provider 声明语义分组，UI 只负责排版。
                IReadOnlyList<int> rowSizes = provider?.KpiRowSizes;
                if (rowSizes != null)
                {
                    for (int i = 0; i < rowSizes.Count; i++)
                    {
                        if (rowSizes[i] > 0)
                        {
                            snapshot.KpiRowSizes.Add(rowSizes[i]);
                        }
                    }
                }

                // KPI 带的可见性由 Provider 声明（可选契约）：人员中心的业务视图自带同义统计带，
                // 两条带同屏 = 同一批数字画两次 ⇒ 该中心声明不画 KPI（预览即为如此）。
                // 注意：**KPI 采集能力保留** —— 「有没有 Provider」仍是该中心「已接入」的判据。
                ISystemCenterKpiVisibility kpiVisibility = def.reserved
                    ? null
                    : provider as ISystemCenterKpiVisibility;
                bool showKpis = kpiVisibility == null || kpiVisibility.ShowsKpis;
                if (provider != null && map != null && showKpis)
                {
                    provider.AppendKpis(snapshot.Kpis, map);
                }

                // 业务视图（② 车间俯视）：由另一条契约提供（实现方在 Automation 模块）。
                // 非空即渲染，UI 不关心是哪个中心。
                IWorkshopViewDataProvider workshopView = def.reserved || dataRegistry == null
                    ? null
                    : dataRegistry.GetWorkshopView(def.defName);
                if (workshopView != null && map != null)
                {
                    snapshot.Workshop = workshopView.BuildWorkshopView(map);
                }
                // 业务视图：**模块主界面承载**（档案馆主界面 → 人员系统中心详情页）。
                // 与其它视图同规「非空即渲染」，但**不要求有地图**：档案是殖民地级历史数据，
                // 无地图时（总控标签页 validWithoutMap）照样可读，故不套 map != null 前置。
                IArchiveViewDataProvider archiveView = def.reserved || dataRegistry == null
                    ? null
                    : dataRegistry.GetArchiveView(def.defName);
                if (archiveView != null)
                {
                    snapshot.ArchiveView = archiveView.BuildArchiveView();
                }

                // 业务视图：**人员概览**（殖民地级人事聚合；由 KPI Provider 兼实现，同上面
                // IManufacturing* 的「provider as 契约」同形）。无地图也可读（档案是殖民地级数据）。
                IPopulationOverviewProvider overview = def.reserved
                    ? null
                    : provider as IPopulationOverviewProvider;
                if (overview != null)
                {
                    snapshot.PopulationOverview = overview.BuildOverview();
                }

                // 业务视图：③ 分析带（由 KPI Provider 兼实现，读工单 + 产出 ledger）。
                IManufacturingAnalysisDataProvider analysis = def.reserved
                    ? null
                    : provider as IManufacturingAnalysisDataProvider;
                if (analysis != null && map != null)
                {
                    snapshot.Analysis = analysis.BuildAnalysis(map);
                }

                // 业务视图：④ 运营（在岗殖民者 / 成品库 / Andon 占位）
                IManufacturingOperationsDataProvider operations = def.reserved
                    ? null
                    : provider as IManufacturingOperationsDataProvider;
                if (operations != null && map != null)
                {
                    snapshot.Operations = operations.BuildOperations(map);
                }

                // 业务视图：⑤ 事件与设备健康
                IManufacturingEventsDataProvider events = def.reserved
                    ? null
                    : provider as IManufacturingEventsDataProvider;
                if (events != null)
                {
                    snapshot.Events = events.BuildEvents(map);
                }

                // 业务视图：⑦ 数据层契约（Def 注册表 + Runtime 对象快照）
                IManufacturingDataLayerDataProvider dataLayer = def.reserved
                    ? null
                    : provider as IManufacturingDataLayerDataProvider;
                if (dataLayer != null && map != null)
                {
                    snapshot.DataLayer = dataLayer.BuildDataLayer(map);
                }

                // ② 的 #rightcol：生产队列 + 原料仓
                IManufacturingStockQueueDataProvider stockQueue = def.reserved
                    ? null
                    : provider as IManufacturingStockQueueDataProvider;
                if (stockQueue != null && map != null)
                {
                    snapshot.StockQueue = stockQueue.BuildStockQueue(map);
                }

                // ② 的 #cmdbar：产线汇总条 + 视图体检
                // 车间/设备计数来自上方已构建的俯视图快照（车间枚举是 Automation 的职责，
                // Application 不反向依赖 —— ARC-001）。
                IManufacturingCommandBarDataProvider commandBar = def.reserved
                    ? null
                    : provider as IManufacturingCommandBarDataProvider;
                if (commandBar != null && map != null)
                {
                    int workshopCount = snapshot.Workshop != null ? snapshot.Workshop.WorkshopCount : 0;
                    int deviceCount = snapshot.Workshop != null ? snapshot.Workshop.DeviceCount : 0;
                    snapshot.CommandBar = commandBar.BuildCommandBar(map, workshopCount, deviceCount);
                }

                // ⑥ 数据桥（6 面板：3 真接 + 3 占位）
                IManufacturingBridgeDataProvider bridge = def.reserved
                    ? null
                    : provider as IManufacturingBridgeDataProvider;
                if (bridge != null && map != null)
                {
                    snapshot.Bridge = bridge.BuildBridge(map);
                }

                // 页面外壳（顶栏 + 底栏广播）
                IManufacturingChromeDataProvider chrome = def.reserved
                    ? null
                    : provider as IManufacturingChromeDataProvider;
                if (chrome != null)
                {
                    snapshot.Chrome = chrome.BuildChrome(map);
                    // ⚠ 2026-10-06（用户指令「kpi 也要移除。确保干净」）：原 `FillKpiLine(snapshot)`
                    //   （把 ① 九卡抄成顶栏 `#kpiline` 单行读数带）已删除 —— 制造中心不再产出 KPI
                    //   ⇒ 无源可抄；连带 `Chrome.KpiLine` 字段、`ManufacturingChromeSnapshot.KpiLineItem`、
                    //   `Chrome.cs` 的 `DrawKpiLine` / `KpiLineToneColor` 一并移除。
                }

                // ---- 经济中心：殖民地经济整页（CHG-LCE-001 重建期最小骨架；替代原五块 MVS 业务视图）----
                // 与人员中心的 PopulationOverview 同规「provider as 可选契约」，不新增注册表字典。
                // 非 null 即渲染（LAND-010）—— UI 只看数据、不看中心名。
                IColonyEconomyDataProvider colonyEconomy = def.reserved
                    ? null
                    : provider as IColonyEconomyDataProvider;
                if (colonyEconomy != null && map != null)
                {
                    snapshot.ColonyEconomy = colonyEconomy.BuildColonyEconomy(map);
                    snapshot.ColonyStockGallery = colonyEconomy.BuildStockGallery(map, request);
                }

                if (def.reserved)
                {
                    snapshot.NoticeText = Translate("PersonalChronicle.UI.MasterControl.ReservedNotice");
                }
                else if (provider == null || map == null)
                {
                    // 无数据源 / 无地图时不产出占位 KPI：宁可不画 KPI 区，也不画一屏「—」。
                    snapshot.NoticeText = Translate("PersonalChronicle.UI.MasterControl.NoDataNotice");
                }

                snapshot.BuiltFromRevision = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Ui, "SystemCenter detail snapshot failed: " + ex);
                snapshot.IsEmpty = true;
            }
            return snapshot;
        }

        private SystemCenterCardView BuildCard(SystemCenterDef def, Map map)
        {
            bool reserved = def.reserved;
            // 接入状态是**运行期事实**（该中心有没有数据 Provider），不是 Def 里手填的字段。
            // 预留中心不查数据源：预留 = 玩法未落地，接不接都按「预留」显示；
            // 「预留却有 Provider」这种真不一致由启动期校验报警（ChronicleMod.ValidateSystemCenterProviders）。
            ISystemCenterDataProvider provider = reserved || dataRegistry == null
                ? null
                : dataRegistry.Get(def.defName);
            SystemCenterWiring wiring = ResolveWiring(reserved, provider != null);

            SystemCenterCardView card = new SystemCenterCardView
            {
                DefName = def.defName,
                Label = string.IsNullOrEmpty(def.label) ? def.defName : def.label,
                NameEn = def.nameEn,
                Tier = def.priorityTier,
                CenterVersion = string.IsNullOrEmpty(def.centerVersion) ? "-" : def.centerVersion,
                Reserved = reserved,
                Wiring = wiring,
                StatusText = Translate(StatusKeyOf(wiring)),
                OwnText = JoinList(def.responsible),
                DependsText = def.dependsOn.Count > 0
                    ? Translate("PersonalChronicle.UI.MasterControl.Card.Depends", JoinDefLabels(def.dependsOn))
                    : Translate("PersonalChronicle.UI.MasterControl.Card.NoDepends")
            };
            // 预览用首项 KPI：只有「运行中」（= 非预留且已接入）才取数；未接入 / 预留 / 无地图
            // 一律不填 —— 卡片右上角宁可不画，也不画样例数值（LAND-009 诚实性 > 观感）。
            ISystemCenterKpiVisibility kpiVisibility = provider as ISystemCenterKpiVisibility;
            if (wiring == SystemCenterWiring.Running && map != null
                && (kpiVisibility == null || kpiVisibility.ShowsKpis))
            {
                List<SystemCenterKpiView> kpis = new List<SystemCenterKpiView>(4);
                provider.AppendKpis(kpis, map);
                if (kpis.Count > 0 && kpis[0] != null && kpis[0].HasValue)
                {
                    card.HeadKpiLabel = kpis[0].Label;
                    card.HeadKpiValue = kpis[0].Value;
                    card.HeadKpiTone = kpis[0].Tone;
                }
            }

            card.TooltipText = BuildCardTooltip(card, def, reserved);
            return card;
        }

        /// <summary>
        /// 接入状态判定（三态，纯函数、离线可测）：
        /// 预留（Def 声明该中心该不该有玩法）&gt; 有 Provider（运行期事实）&gt; 两者皆无 = 数据未接入。
        ///
        /// <para>判据刻意放在运行期而不是 Def 字段上：手填一个 isWired 字段迟早与事实漂移，
        /// 而「有没有 Provider」在任何时刻都是注册表里的既成事实。</para>
        /// </summary>
        public static SystemCenterWiring ResolveWiring(bool reserved, bool hasProvider)
        {
            if (reserved)
            {
                return SystemCenterWiring.Reserved;
            }
            return hasProvider ? SystemCenterWiring.Running : SystemCenterWiring.NotWired;
        }

        /// <summary>接入状态 → 状态文案 Keyed（唯一映射点；UI 不选文案）。</summary>
        private static string StatusKeyOf(SystemCenterWiring wiring)
        {
            switch (wiring)
            {
                case SystemCenterWiring.Running:
                    return "PersonalChronicle.UI.MasterControl.Status.Running";
                case SystemCenterWiring.NotWired:
                    return "PersonalChronicle.UI.MasterControl.Status.NotWired";
                default:
                    return "PersonalChronicle.UI.MasterControl.Status.Reserved";
            }
        }

        /// <summary>
        /// 卡片悬停提示：标题 + 稳定 ID + 档位 + 状态 + 职责 + 依赖。
        /// 构建期拼好（绘制期零分配），并<b>不使用</b>未接入数据源的中心 KPI。
        /// </summary>
        private static string BuildCardTooltip(SystemCenterCardView card, SystemCenterDef def, bool reserved)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(card.Label);
            if (!string.IsNullOrEmpty(card.NameEn))
            {
                builder.Append(" (").Append(card.NameEn).Append(')');
            }
            builder.Append('\n').Append(def.defName)
                .Append(" · Tier ").Append(def.priorityTier)
                .Append(" · v").Append(def.centerVersion);
            builder.Append('\n').Append(card.StatusText);
            if (!string.IsNullOrEmpty(card.OwnText))
            {
                builder.Append('\n').Append(Translate("PersonalChronicle.UI.MasterControl.Sub.Responsible"))
                    .Append(": ").Append(card.OwnText);
            }
            builder.Append('\n').Append(card.DependsText);
            if (!reserved)
            {
                builder.Append('\n').Append(Translate("PersonalChronicle.UI.MasterControl.Tooltip.Open"));
            }
            return builder.ToString();
        }

        /// <summary>把依赖的中心 ID 翻译成显示名（找不到时回落原 ID，便于排查漂移）。</summary>
        private static string JoinDefLabels(List<string> defNames)
        {
            if (defNames == null || defNames.Count == 0)
            {
                return string.Empty;
            }
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < defNames.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" / ");
                }
                SystemCenterDef dep = SystemCenterRegistry.GetNamed(defNames[i]);
                builder.Append(dep != null && !string.IsNullOrEmpty(dep.label) ? dep.label : defNames[i]);
            }
            return builder.ToString();
        }

        private static string JoinList(List<string> items)
        {
            if (items == null || items.Count == 0)
            {
                return string.Empty;
            }
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" · ");
                }
                builder.Append(items[i]);
            }
            return builder.ToString();
        }

        private static void CopyInto(List<string> source, List<string> target)
        {
            if (source == null)
            {
                return;
            }
            for (int i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        // ⚠ 2026-10-06（用户指令「kpi 也要移除。确保干净」）：原 `FillKpiLine`（把 ① 九卡抄成顶栏
        //   `#kpiline` 单行读数带）、其专用语气映射 `KpiLineTone`（预览 oc()/bc() 阈值）与专用解析器
        //   `ParsePercent`（从 "63.2%" 取数）三个方法**整块删除** —— 制造中心不再产出 KPI，
        //   该读数带无数据源；三者在全库再无其它调用点（已核）。
        //   恢复方式：从 git 历史取回本段 + `ManufacturingChromeSnapshot.KpiLine/KpiLineItem`
        //   + `Chrome.DrawKpiLine/KpiLineToneColor` + `Chrome` 里 cmdbar 的绘制段。

        private static string Translate(string key)
        {
            return key.Translate().ToString();
        }

        private static string Translate(string key, int arg)
        {
            return key.Translate(arg).ToString();
        }

        private static string Translate(string key, string arg)
        {
            return key.Translate(arg).ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using PersonalChronicle.Api;
using PersonalChronicle.Application.SystemCenters.Builtin;
using Verse;

namespace PersonalChronicle.Application.SystemCenters
{
    /// <summary>
    /// 中心数据源注册表：中心 ID → <see cref="ISystemCenterDataProvider"/>。
    ///
    /// <para>与 <c>ArchiveProviderRegistry</c> 同构：内置 Provider 先注册，第三方 Mod
    /// 后注册同 ID 即覆盖（GOV-002 组合优先 / GOV-008 核心不认识第三方）。注册表
    /// 本身不认识任何具体中心，新增中心数据源无需改动本类。</para>
    /// </summary>
    public sealed class SystemCenterDataRegistry
    {
        private readonly Dictionary<string, ISystemCenterDataProvider> byCenter =
            new Dictionary<string, ISystemCenterDataProvider>(StringComparer.Ordinal);

        /// <summary>已注册 Provider 数量（启动期漂移校验用）。</summary>
        public int Count
        {
            get { return byCenter.Count; }
        }

        /// <summary>已注册的 Provider 快照（只读遍历用）。</summary>
        public IEnumerable<ISystemCenterDataProvider> All
        {
            get { return byCenter.Values; }
        }

        /// <summary>
        /// 注册（或按中心 ID 覆盖）一个 Provider。中心 ID 为空的 Provider 被拒绝，
        /// 避免污染注册表导致查不到数据还找不到原因。
        /// </summary>
        public bool Register(ISystemCenterDataProvider provider)
        {
            if (provider == null || string.IsNullOrEmpty(provider.CenterDefName))
            {
                return false;
            }
            byCenter[provider.CenterDefName] = provider;
            return true;
        }

        /// <summary>按中心 ID 取 Provider；未接入返回 null（UI 显示「数据未接入」）。</summary>
        public ISystemCenterDataProvider Get(string centerDefName)
        {
            if (string.IsNullOrEmpty(centerDefName))
            {
                return null;
            }
            return byCenter.TryGetValue(centerDefName, out ISystemCenterDataProvider provider)
                ? provider
                : null;
        }

        // ---- 业务视图（② 车间俯视）：与 KPI 分开注册，因为实现方不同模块 ----
        // 车间区是 Automation 的资产，其 Provider 由该模块实现（Automation → Application），
        // 组合根在这里做「中心 ID → 视图 Provider」的绑定；UI 只认快照非空，不认中心名（LAND-010）。
        private readonly Dictionary<string, IWorkshopViewDataProvider> workshopViews =
            new Dictionary<string, IWorkshopViewDataProvider>(StringComparer.Ordinal);

        /// <summary>把某个中心绑定到车间俯视图数据源。</summary>
        public void RegisterWorkshopView(string centerDefName, IWorkshopViewDataProvider provider)
        {
            if (string.IsNullOrEmpty(centerDefName) || provider == null)
            {
                return;
            }
            workshopViews[centerDefName] = provider;
        }

        /// <summary>取该中心的车间俯视图数据源；没有返回 null（UI 不渲染该区块）。</summary>
        public IWorkshopViewDataProvider GetWorkshopView(string centerDefName)
        {
            if (string.IsNullOrEmpty(centerDefName))
            {
                return null;
            }
            return workshopViews.TryGetValue(centerDefName, out IWorkshopViewDataProvider provider)
                ? provider
                : null;
        }

        // ---- 业务视图（模块主界面承载）：档案馆主界面植入人员系统中心 ----
        // 与车间视图同规：契约在 Application、实现方在拥有该界面的业务模块、组合根在这里做
        // 「中心 ID → 承载视图」绑定；UI 只认快照非空（LAND-010）。之所以与 ISystemCenterDataProvider
        // 分开注册，是因为「有没有 KPI 数据源」（决定接入状态徽标）与「能不能嵌一块主界面」
        // （决定详情页画什么）是两件独立的事 —— 混成一个字典会让「有界面但还没有指标」的中心无处安放。
        private readonly Dictionary<string, IArchiveViewDataProvider> archiveViews =
            new Dictionary<string, IArchiveViewDataProvider>(StringComparer.Ordinal);

        /// <summary>把某个中心绑定到「模块主界面承载」数据源（组合根调用）。</summary>
        public void RegisterArchiveView(string centerDefName, IArchiveViewDataProvider provider)
        {
            if (string.IsNullOrEmpty(centerDefName) || provider == null)
            {
                return;
            }
            archiveViews[centerDefName] = provider;
        }

        /// <summary>取该中心的主界面承载数据源；没有返回 null（UI 不渲染该区块）。</summary>
        public IArchiveViewDataProvider GetArchiveView(string centerDefName)
        {
            if (string.IsNullOrEmpty(centerDefName))
            {
                return null;
            }
            return archiveViews.TryGetValue(centerDefName, out IArchiveViewDataProvider provider)
                ? provider
                : null;
        }

        /// <summary>
        /// 内置 Provider 全集（组合根调用；顺序即注册顺序）。
        /// <paramref name="queries"/> 为档案只读查询面：制造中心的良品率需要它读
        /// 已持久化的产出 ledger（<c>CareerEvent.Quality</c> 累计），而不是另收一份数据。
        /// </summary>
        public static SystemCenterDataRegistry CreateWithBuiltins(IArchiveQueryService queries)
        {
            SystemCenterDataRegistry registry = new SystemCenterDataRegistry();

            registry.Register(new BuiltinManufacturingProvider(queries));
            // ⚠ CHG-20261006-CENTER-RESERVE（2026-10-06 玩家裁决）：内务（`logistics_center`）与医疗
            //   （`medical_center`）已降级为**预留中心**（`Defs/SystemCenterDefs.xml` 的 `reserved` 改为 true），
            //   因此**不在此注册数据源**。两件事必须成对（预留 Def ⟺ 无 Provider）：只改 Def 不改这里，
            //   就会命中 `ChronicleMod.ValidateSystemCenterProviders` 的反向校验，**每次启动**写一条
            //   `Log.Warning`「provider registered for a reserved center」——那正是为「接了数据源却忘了把
            //   reserved 改回 false」准备的探测器，不是可以忽略的噪音。
            //   复活（拷回 Provider 原件 + 恢复下面两行 + 把 Def 的 reserved 改回 false，三者缺一不可）：
            //     registry.Register(new BuiltinLogisticsProvider());
            //     registry.Register(new BuiltinMedicalProvider());
            //   原件快照：`_backups/centers_retire_20261006/`。
            // 殖民地经济中心（CHG-LCE-001 重建期最小骨架：KPI 三格 + 建设中说明；
            // 原 MVS 契约接线随市场体系删除 —— 不再有需要预接线的组合根步骤）。
            registry.Register(new BuiltinColonyEconomyProvider());
            // 人员系统中心：档案规模 KPI。注册即让该中心在总控注册表里显示「运行中」
            // （ResolveWiring 的判据是「有没有 KPI 数据源」，不是「有没有界面」）。
            registry.Register(new BuiltinPopulationProvider(queries));
            return registry;
        }
    }
}

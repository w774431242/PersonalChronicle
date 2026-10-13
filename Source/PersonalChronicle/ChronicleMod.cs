using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PersonalChronicle.Api;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using UnityEngine;
using Verse;

namespace PersonalChronicle
{
    /// <summary>
    /// Mod entry point (composition root): owns the settings instance, the
    /// archive service singleton consumed by the capture layer, and the public
    /// API facade (<see cref="Api"/>) used by integrators.
    /// </summary>
    public sealed class PersonalChronicleMod : Mod
    {
        public static PersonalChronicleMod Instance { get; private set; }
        public static ChronicleSettings Settings { get; private set; }
        public static IArchiveService ArchiveService { get; private set; }
        public static IWorkIntensityService WorkIntensityService { get; private set; }
        public static IWorkTimeCaptureService WorkTimeCaptureService { get; private set; }
        public static IWorkIntensityProviderRegistry WorkIntensityProviders { get; private set; }

        /// <summary>
        /// 系统中心数据源注册表（总控制中心 KPI 采集扩展点，v1.3.0）。
        /// 组合根装配内置 Provider；第三方 Mod 可注册同中心 ID 覆盖或新增。
        /// </summary>
        public static Application.SystemCenters.SystemCenterDataRegistry SystemCenterData { get; private set; }

        /// <summary>
        /// v4.1 unified integration facade. Preferred entry point for third-party
        /// mods — replaces the scattered static service properties. Retained legacy
        /// statics remain for binary compatibility.
        /// </summary>
        public static IPersonalChronicleApi Api { get; private set; }

        public PersonalChronicleMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<ChronicleSettings>();
            // v1.1.4: restore the persisted UI theme as soon as settings exist, so the
            // whole window (and its ArchiveUiStyle forwards) renders in the saved skin.
            UITheme.Apply(Settings.ThemeId);
            WorkIntensityProviders = new WorkIntensityProviderRegistry();
            ArchiveService service = new ArchiveService(WorkIntensityProviders);
            ArchiveService = service;
            WorkIntensityService = service;
            WorkTimeCaptureService = service;

            // P2: one unified registry owns every domain provider. Built-in providers
            // register first at priority 0; third-party mods register higher to override.
            ArchiveProviderRegistry providerRegistry = new ArchiveProviderRegistry();
            providerRegistry.Register(new PersonalChronicle.Api.DomainProviders.Builtin.BuiltinProductionProvider(service));
            providerRegistry.Register(new PersonalChronicle.Api.DomainProviders.Builtin.BuiltinBattleProvider(service));
            providerRegistry.Register(new PersonalChronicle.Api.DomainProviders.Builtin.BuiltinRelationProvider(service));
            providerRegistry.Register(new PersonalChronicle.Api.DomainProviders.Builtin.BuiltinPlaceProvider(service));

            // 总控制中心：中心 KPI 数据源（只读采集，不持有状态）。
            // service 同时是 IArchiveQueryService —— 制造中心 ① 的良品率复用已持久化的产出 ledger。
            SystemCenterData = Application.SystemCenters.SystemCenterDataRegistry.CreateWithBuiltins(service);
            // 业务视图绑定：制造中心 ② 车间俯视的数据源由 Automation 模块实现（它拥有车间区资产）。
            SystemCenterData.RegisterWorkshopView(
                Domain.SystemCenterIds.Manufacturing, new Automation.WorkshopFloorViewProvider(service));
            // 业务视图绑定：人员系统中心详情页**承载殖民地档案馆主界面本体**（不是跳转按钮）。
            // 承载方在 Archive 模块（它拥有该界面），依赖方向 Archive → Application，与上一行同向。
            SystemCenterData.RegisterArchiveView(
                Domain.SystemCenterIds.Population, new Archive.ArchiveCenterViewProvider(service));

            Api = new PersonalChronicleApiImpl(
                service, service, service, WorkIntensityService, WorkIntensityProviders, providerRegistry);
        }

        /// <summary>
        /// Optional integration point for another mod. Registration is
        /// instance-owned and idempotent by ProviderId; the core has no direct
        /// reference to the registering mod.
        /// </summary>
        public static bool RegisterWorkIntensityProvider(IWorkIntensityProvider provider)
        {
            return WorkIntensityProviders != null
                && WorkIntensityProviders.Register(provider);
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (Settings != null)
            {
                Settings.DoSettingsWindowContents(inRect);
            }
        }

        public override string SettingsCategory()
        {
            return "PersonalChronicle.Settings.Category".Translate().ToString();
        }
    }

    [StaticConstructorOnStartup]
    public static class ChronicleStartup
    {
        static ChronicleStartup()
        {
            // 运行身份标记（2026-10-01）：日志首行 = 本 DLL 的构建时间 ——
            //   实证：Prepatcher/PurePatcher 内存加载 ⇒ Location 为空、Version 0.0.0.0，
            //   故改读**编译期注入**的 BuildStamp.g.cs 常量（csproj 每次构建重写）。
            try
            {
                Log.Message("[PersonalChronicle] build "
                    + PersonalChronicle.BuildInfo.BuildTimeUtc + " UTC");
            }
            catch (Exception ex)
            {
                Log.Error("[PersonalChronicle] build identity failed: " + ex);
            }

            // Same discovery as Harmony.PatchAll, with failure isolation per class.
            // Never replay already processed classes; partial class installs are
            // retained and diagnosed. InspectTab injection remains independent.
            try
            {
                Harmony harmony = new Harmony("colony.integratedmanagement");
                HarmonyStartup.PatchAll(harmony, Assembly.GetExecutingAssembly(), Log.Error);
                Capture.CapturePatchDiagnostics.VerifyWorkGateInstallation(harmony.Id);
            }
            catch (Exception ex)
            {
                Log.Error("[PersonalChronicle] Harmony PatchAll failed: " + ex);
            }

            try
            {
                ValidateEventTypeKeys();
            }
            catch (Exception ex)
            {
                Log.Error("[PersonalChronicle] ValidateEventTypeKeys failed: " + ex);
            }

            // T1 人工辅助设备：Def 全部加载后接入原型的完整配方和原版作业名单。
            try
            {
                Automation.SmartBenchBootstrap.Initialize();
            }
            catch (Exception ex)
            {
                Log.Error("[PersonalChronicle] T1 equipment initialization failed: " + ex);
            }

            // v4.6: add the per-pawn "档案" tab to every humanlike inspect pane.
            // Runs here because the DefDatabase is fully populated at this point.
            try
            {
                // 类型集合由组合根注入（注入器不认识任何业务域，规避 Archive↔Economy 循环）。
                // 顺序敏感：按序 Insert(index 1)，逆序渲染后视觉顺序 = 档案｜职业档案｜个人需求。
                Archive.ChronicleInspectTabInjector.InjectAll(new[]
                {
                    typeof(Archive.ITab_Pawn_Chronicle),
                    typeof(Archive.ITab_Pawn_Career),
                    typeof(Economy.PawnEconomy.ITab_Pawn_PersonalNeeds)
                });
            }
            catch (Exception ex)
            {
                Log.Error("[PersonalChronicle] InspectTab injection failed: " + ex);
            }

            // 总控制中心：校验「内置数据源绑定的中心 ID」都能在 Def 注册表里找到。
            // 与 ValidateEventTypeKeys 同构：常量/Def 漂移必须在启动期大声报出来，
            // 否则表现为「中心一直在，只是 KPI 永远显示未接入」这种静默故障。
            try
            {
                ValidateSystemCenterProviders();
            }
            catch (Exception ex)
            {
                Log.Error("[PersonalChronicle] ValidateSystemCenterProviders failed: " + ex);
            }

            // v4.7: 启动期扫描全部已加载 mod 贡献的 StatDef / NeedDef，
            // 自动注册为自定义字段（按 mod 来源分组）。每次启动重建 All 列表，
            // 天然支持增删 mod 后下次启动自动刷新，无需持久化缓存。
            try
            {
                Archive.OverviewFieldRegistry.ScanExternal();
            }
            catch (Exception ex)
            {
                Log.Error("[PersonalChronicle] OverviewFieldRegistry.ScanExternal failed: " + ex);
            }

        }

        /// <summary>
        /// Startup invariant check: every event TypeKey constant must resolve to
        /// a loaded ChronicleEventDef. A null lookup means ChronicleEventType and
        /// Defs/Chronicle_Events.xml have drifted — the UI resolves event names
        /// through these Defs, so the mismatch must surface loudly at load.
        /// Runs after DefDatabase is populated ([StaticConstructorOnStartup]).
        /// </summary>
        private static void ValidateEventTypeKeys()
        {
            string[] typeKeys = new string[]
            {
                ChronicleEventType.Join,
                ChronicleEventType.Death,
                ChronicleEventType.Crafted,
                ChronicleEventType.Built,
                ChronicleEventType.Battle,
                ChronicleEventType.Social
            };
            for (int i = 0; i < typeKeys.Length; i++)
            {
                if (DefDatabase<ChronicleEventDef>.GetNamedSilentFail(typeKeys[i]) == null)
                {
                    ChronicleLog.Error(ChronicleLog.Category.Mod, "TypeKey/Def drift detected for: " + typeKeys[i] + " (update ChronicleEventType or Defs/Chronicle_Events.xml)");
                }
            }
        }

        /// <summary>
        /// Startup invariant check for the system-center registry, in both directions:
        ///
        /// <list type="bullet">
        /// <item><b>Provider to Def</b>: every registered provider must be bound to a center id
        /// that exists in the Def registry. A null lookup means <c>Domain/SystemCenterIds.cs</c>
        /// and <c>Defs/SystemCenterDefs.xml</c> have drifted, which would silently degrade to
        /// "center is listed but its KPIs are never wired" — surfaced loudly at load (error).</item>
        /// <item><b>Def to Provider</b>: a center marked <c>reserved=true</c> must have no provider
        /// registered for it — such a provider is dead code (the master-control UI keeps showing
        /// "reserved" and never calls it), so it is a real inconsistency. Warn only: the Def bit is
        /// the authority and the fix is one line of XML.</item>
        /// </list>
        ///
        /// <para>The reverse case is deliberately <b>not</b> reported: a center with
        /// <c>reserved=false</c> and no provider is a legal, expected state — it is exactly how
        /// "the module exists but is not wired into the center framework yet" is expressed. The
        /// registry UI renders it as <c>Status.NotWired</c>, so it is visible without load-time noise.</para>
        /// </summary>
        private static void ValidateSystemCenterProviders()
        {
            Application.SystemCenters.SystemCenterDataRegistry registry = PersonalChronicleMod.SystemCenterData;
            if (registry == null)
            {
                return;
            }
            foreach (Application.SystemCenters.ISystemCenterDataProvider provider in registry.All)
            {
                Domain.SystemCenterDef def = Domain.SystemCenterRegistry.GetNamed(provider.CenterDefName);
                if (def == null)
                {
                    ChronicleLog.Error(ChronicleLog.Category.Provider,
                        "SystemCenter provider bound to unknown center id: " + provider.CenterDefName
                        + " (update Domain/SystemCenterIds.cs or Defs/SystemCenterDefs.xml)");
                }
                else if (def.reserved)
                {
                    // 预留中心不该有数据源：UI 按 Def 显示「预留」且永远不会调用该 Provider，
                    // 等同于死代码（多半是「接了数据源但忘了把 reserved 改回 false」）。
                    // 只报警告、不判启动失败：运行期表现仍然可用，修正点就在 XML 一行。
                    // 反向（reserved=false 且无 Provider）刻意不报 —— 那是本次要表达的合法状态：
                    // 「模块已存在但未接入中心框架」，UI 会如实显示「数据未接入」。
                    ChronicleLog.Warning(ChronicleLog.Category.Provider,
                        "SystemCenter provider registered for a reserved center: " + provider.CenterDefName
                        + " — the master-control UI shows it as reserved and never calls the provider"
                        + " (set reserved=false in Defs/SystemCenterDefs.xml if it really is wired)");
                }
            }
        }
    }

    /// <summary>
    /// Internal implementation of <see cref="IPersonalChronicleApi"/>. Aggregates
    /// the existing service singletons behind one facade so integrators depend on
    /// a single, capability-probed entry point. Holds no state of its own.
    /// </summary>
    internal sealed class PersonalChronicleApiImpl : IPersonalChronicleApi
    {
        public ApiVersion Version { get; } = ApiVersion.Current;
        public IArchiveQueryService Queries { get; }
        public IArchiveEventSink Events { get; }
        public IWorkTimeCaptureService WorkTime { get; }
        public IWorkIntensityService WorkIntensity { get; }
        public IArchiveProviderRegistry Providers { get; }
        public IWorkIntensityProviderRegistry WorkIntensityProviders { get; }

        public PersonalChronicleApiImpl(
            IArchiveQueryService queries,
            IArchiveEventSink events,
            IWorkTimeCaptureService workTime,
            IWorkIntensityService workIntensity,
            IWorkIntensityProviderRegistry workIntensityProviders,
            IArchiveProviderRegistry providers)
        {
            Queries = queries;
            Events = events;
            WorkTime = workTime;
            WorkIntensity = workIntensity;
            WorkIntensityProviders = workIntensityProviders;
            Providers = providers;
        }

        public bool Supports(int major, int minMinor)
        {
            return Version.Satisfies(major, minMinor);
        }
    }
}

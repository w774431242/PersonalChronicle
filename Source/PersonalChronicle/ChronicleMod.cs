using System.Reflection;
using HarmonyLib;
using PersonalChronicle.Api;
using PersonalChronicle.Application;
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
        /// v4.1 unified integration facade. Preferred entry point for third-party
        /// mods — replaces the scattered static service properties. Retained legacy
        /// statics remain for binary compatibility.
        /// </summary>
        public static IPersonalChronicleApi Api { get; private set; }

        public PersonalChronicleMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<ChronicleSettings>();
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
            Harmony harmony = new Harmony("PersonalChronicle.Archive");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            ValidateEventTypeKeys();
            // v4.6: add the per-pawn "档案" tab to every humanlike inspect pane.
            // Runs here because the DefDatabase is fully populated at this point.
            Archive.ChronicleInspectTabInjector.InjectAll();
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

using System.Reflection;
using HarmonyLib;
using PersonalChronicle.Application;
using PersonalChronicle.Domain;
using UnityEngine;
using Verse;

namespace PersonalChronicle
{
    /// <summary>
    /// Mod entry point (composition root): owns the settings instance and the
    /// archive service singleton consumed by the capture layer.
    /// </summary>
    public sealed class PersonalChronicleMod : Mod
    {
        public static PersonalChronicleMod Instance { get; private set; }
        public static ChronicleSettings Settings { get; private set; }
        public static IArchiveService ArchiveService { get; private set; }
        public static IWorkIntensityService WorkIntensityService { get; private set; }
        public static IWorkTimeCaptureService WorkTimeCaptureService { get; private set; }
        public static IWorkIntensityProviderRegistry WorkIntensityProviders { get; private set; }

        public PersonalChronicleMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<ChronicleSettings>();
            WorkIntensityProviders = new WorkIntensityProviderRegistry();
            ArchiveService service = new ArchiveService(WorkIntensityProviders);
            ArchiveService = service;
            WorkIntensityService = service;
            WorkTimeCaptureService = service;
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
                    Log.Error("PersonalChronicle: TypeKey/Def drift detected for: " + typeKeys[i] + " (update ChronicleEventType or Defs/Chronicle_Events.xml)");
                }
            }
        }
    }
}

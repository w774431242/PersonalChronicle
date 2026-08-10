using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// v4.6: injects <see cref="ITab_Pawn_Chronicle"/> into every humanlike pawn
    /// def's inspect pane at load time.
    ///
    /// Why code injection instead of an XML PatchOperation (architecture §2
    /// extension-point priority): the set of humanlike races is open-ended —
    /// other mods add their own. An XML patch would have to enumerate defNames
    /// and would silently miss modded races, whereas iterating the DefDatabase
    /// after all defs are loaded covers every race, vanilla or modded, without
    /// hardcoding a single defName.
    ///
    /// Safety: purely additive (never removes another mod's tabs), idempotent
    /// (skips defs that already carry the tab), and failure-isolated per def so a
    /// single malformed race def cannot abort the whole injection pass.
    /// </summary>
    public static class ChronicleInspectTabInjector
    {
        private static bool injected;

        /// <summary>
        /// Runs once after the DefDatabase is fully populated. Idempotent: safe to
        /// call again (e.g. after a def reload) without duplicating tabs.
        /// </summary>
        public static void InjectAll()
        {
            if (injected)
            {
                return;
            }
            injected = true;

            Type tabType = typeof(ITab_Pawn_Chronicle);
            int count = 0;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                try
                {
                    if (def == null || def.race == null || !def.race.Humanlike)
                    {
                        continue;
                    }

                    // Only augment defs that already show an inspect pane with tabs;
                    // a humanlike with no tabs at all is not a normal selectable pawn.
                    if (def.inspectorTabs == null)
                    {
                        def.inspectorTabs = new List<Type>();
                    }
                    if (def.inspectorTabs.Contains(tabType))
                    {
                        continue;
                    }
                    def.inspectorTabs.Add(tabType);

                    // inspectorTabsResolved is the list the game actually renders; it
                    // is built during ResolveReferences, so when we inject afterwards
                    // we must mirror the addition there too.
                    if (def.inspectorTabsResolved == null)
                    {
                        def.inspectorTabsResolved = new List<InspectTabBase>();
                    }
                    def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(tabType));
                    count++;
                }
                catch (Exception ex)
                {
                    Log.Warning("PersonalChronicle: failed to inject chronicle tab into '"
                        + (def != null ? def.defName : "<null>") + "': " + ex.Message);
                }
            }

            if (Prefs.DevMode)
            {
                Log.Message("PersonalChronicle: chronicle inspect tab injected into " + count + " humanlike def(s).");
            }
        }
    }
}

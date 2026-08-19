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
            // 职业档案 ITab：与六宫格 ITab 并列的第二个 Pawn inspect 页（P1 落地，履历真实数据）。
            Type careerTabType = typeof(ITab_Pawn_Career);
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
                    if (def.inspectorTabsResolved == null)
                    {
                        def.inspectorTabsResolved = new List<InspectTabBase>();
                    }

                    // 注入两个 Tab（六宫格档案 + 职业档案），各自幂等去重。
                    InjectTab(def, tabType, ref count);
                    InjectTab(def, careerTabType, ref count);
                }
                catch (Exception ex)
                {
                    ChronicleLog.Warning(ChronicleLog.Category.Ui, "failed to inject chronicle tab into '"
                        + (def != null ? def.defName : "<null>") + "': " + ex.Message);
                }
            }

            if (Prefs.DevMode)
            {
                ChronicleLog.Info(ChronicleLog.Category.Ui, "chronicle inspect tab injected into " + count + " humanlike def(s).");
            }
        }

        /// <summary>
        /// 向单个 ThingDef 幂等注入一个 ITab 类型（同时写入 inspectorTabs 与
        /// inspectorTabsResolved，后者是游戏实际渲染的列表）。已存在则跳过。
        /// </summary>
        private static void InjectTab(ThingDef def, Type tabType, ref int count)
        {
            if (def.inspectorTabs.Contains(tabType))
            {
                return;
            }
            def.inspectorTabs.Add(tabType);
            def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(tabType));
            count++;
        }
    }
}

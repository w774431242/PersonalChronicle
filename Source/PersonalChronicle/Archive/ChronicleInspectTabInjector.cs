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
        /// <paramref name="tabTypes"/> 由组合根（ChronicleMod）提供，按序注入且顺序敏感；
        /// 本类不引用任何业务域类型（ARCH：Economy 已引用 Archive.UI，反向直引会构成循环）。
        /// </summary>
        public static void InjectAll(IReadOnlyList<Type> tabTypes)
        {
            if (injected)
            {
                return;
            }
            injected = true;
            if (tabTypes == null)
            {
                return;
            }

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

                    // 逐一注入（各自幂等去重）。顺序 = 参数顺序。
                    for (int i = 0; i < tabTypes.Count; i++)
                    {
                        InjectTab(def, tabTypes[i], ref count);
                    }
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

            // v1.1.5 起固定插入 index 1（不追加到列表末尾）。幂等性不受影响。
            // 2026-10-08 实证更正（原注释称"前移到左侧、紧邻角色"，与实机不符）：
            // 原版 BasePawn.inspectorTabs 顺序为 [Health, Needs, Character, …, Gear, Log]
            // （Core/Defs/ThingDefs_Races/Races_Animal_Base.xml），而 inspect pane 按该列表
            // **逆序**铺开 ⇒ index 1 实际落在视觉最右端、紧邻「健康」；连续插入形成
            // 「档案｜职业档案｜个人需求｜健康」的右侧聚簇（见实机截图）。
            int index = Math.Min(1, def.inspectorTabs.Count);
            def.inspectorTabs.Insert(index, tabType);
            def.inspectorTabsResolved.Insert(index, InspectTabManager.GetSharedInstance(tabType));
            count++;
        }
    }
}

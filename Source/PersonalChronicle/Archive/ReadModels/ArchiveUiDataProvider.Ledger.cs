using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Api;
using PersonalChronicle.Api.DomainProviders;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;
using UnityEngine;

namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// Partial of <see cref="ArchiveUiDataProvider"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveUiDataProvider : IArchiveUiDataProvider
    {

        private static IReadOnlyList<ProductionLineView> BuildProductionLines(
            IReadOnlyList<ChronicleEvent> events)
        {
            List<ProductionLineView> lines = new List<ProductionLineView>();
            if (events == null || events.Count == 0) return lines;
            Dictionary<string, ProductionLineView> byDef = new Dictionary<string, ProductionLineView>();
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev == null || !IsProductionEvent(ev)) continue;
                ObjectRef primary = ev.Primary;
                if (primary == null || string.IsNullOrEmpty(primary.StableId)) continue;
                string defName = ThingDefNameFromStableId(primary.StableId);
                if (string.IsNullOrEmpty(defName)) continue;
                if (byDef.TryGetValue(defName, out ProductionLineView line))
                {
                    line.Count++;
                    if (ev.Tick > line.LastTick)
                    {
                        line.LastTick = ev.Tick;
                        line.StableId = primary.StableId;
                    }
                    byDef[defName] = line;
                }
                else
                {
                    byDef[defName] = new ProductionLineView
                    {
                        DefName = defName,
                        Label = ThingDefLabelLocal(defName),
                        Count = 1,
                        LastTick = ev.Tick,
                        StableId = primary.StableId
                    };
                }
            }
            List<ProductionLineView> result = new List<ProductionLineView>(byDef.Values);
            // v6.6: aggregate per-line market value (Def.MarketValue * Count) so the
            // 产出 cell can show value-contribution bars (Read Model only).
            for (int i = 0; i < result.Count; i++)
            {
                ProductionLineView line = result[i];
                ThingDef td = (!string.IsNullOrEmpty(line.DefName)) ? DefDatabase<ThingDef>.GetNamed(line.DefName, false) : null;
                float unit = (td != null) ? td.BaseMarketValue : 0f;
                line.Value = unit * line.Count;
                result[i] = line;
            }
            result.Sort((a, b) => b.LastTick.CompareTo(a.LastTick));
            return result;
        }

        /// <summary>
        /// 扫描该 pawn 全部 ChronicleEvent 流，按窗口回溯 7 / 30 天累计 Craft/Built 事件的
        /// ThingDef.BaseMarketValue 估值。写回 <see cref="DetailSnapshot.WeeklyProductionSilver"/>
        /// 与 <see cref="DetailSnapshot.MonthlyProductionSilver"/>，仅用于 ITab 周产出/净值 KPI 行。
        /// 不修改 ProductionAccumulator 持久化字段；Def 缺失或 market value 为 0 时静默跳过。
        /// </summary>
        private static void ComputeProductionWindowValue(DetailSnapshot snap)
        {
            if (snap == null) return;
            if (snap.RawEvents == null || snap.RawEvents.Count == 0)
            {
                snap.WeeklyProductionSilver = 0f;
                snap.MonthlyProductionSilver = 0f;
                return;
            }
            long now = Find.TickManager.TicksGame;
            long weekCutoff = now - 7L * RimWorld.GenDate.TicksPerDay;
            long monthCutoff = now - 30L * RimWorld.GenDate.TicksPerDay;
            float weekSum = 0f;
            float monthSum = 0f;
            for (int i = 0; i < snap.RawEvents.Count; i++)
            {
                ChronicleEvent ev = snap.RawEvents[i];
                if (ev == null || !IsProductionEvent(ev)) continue;
                if (ev.Tick < monthCutoff) continue;
                ObjectRef primary = ev.Primary;
                if (primary == null || string.IsNullOrEmpty(primary.StableId)) continue;
                string defName = ThingDefNameFromStableId(primary.StableId);
                if (string.IsNullOrEmpty(defName)) continue;
                ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (td == null) continue;
                float unit = td.BaseMarketValue;
                if (unit <= 0f) continue;
                if (ev.Tick >= weekCutoff) weekSum += unit;
                if (ev.Tick >= monthCutoff) monthSum += unit;
            }
            snap.WeeklyProductionSilver = weekSum;
            snap.MonthlyProductionSilver = monthSum;
        }

        /// <summary>
        /// v4.15 condense-tab: group production lines by their first-level
        /// <see cref="ThingCategoryDef"/> (official one-level category) and return
        /// the top categories by aggregated count. The resulting labels are already
        /// localized (via <see cref="ThingCategoryDef.LabelCap"/>) so the window
        /// stays free of any category-name hardcoding. Categories in the
        /// "plants/corpses" exclusion set (per design doc §C) are dropped to keep
        /// the badge group meaningful.
        /// </summary>
        private static IReadOnlyList<ProductionCategoryView> BuildProductionCategories(
            IReadOnlyList<ProductionLineView> lines)
        {
            List<ProductionCategoryView> empty = new List<ProductionCategoryView>();
            if (lines == null || lines.Count == 0) return empty;

            // First-level category defName → aggregated count.
            Dictionary<string, int> byCat = new Dictionary<string, int>();
            foreach (ProductionLineView line in lines)
            {
                if (line == null || string.IsNullOrEmpty(line.DefName)) continue;
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(line.DefName);
                if (def == null) continue;
                ThingCategoryDef cat = def.FirstThingCategory;
                if (cat == null) continue;
                string key = cat.defName;
                // Skip non-meaningful categories for the digest badge group.
                if (key == "Plants" || key == "Corpses" || key == "Corpse" || key == "Chunks") continue;
                int prev;
                byCat.TryGetValue(key, out prev);
                byCat[key] = prev + line.Count;
            }
            if (byCat.Count == 0) return empty;

            List<ProductionCategoryView> cats = new List<ProductionCategoryView>(byCat.Count);
            foreach (var kv in byCat)
            {
                ThingCategoryDef cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(kv.Key);
                if (cat == null) continue;
                cats.Add(new ProductionCategoryView
                {
                    Label = cat.LabelCap,
                    Count = kv.Value
                });
            }
            // Largest categories first; cap to the top 4 so the badge group stays compact.
            cats.Sort((a, b) => b.Count.CompareTo(a.Count));
            if (cats.Count > 4) cats.RemoveRange(4, cats.Count - 4);
            return cats;
        }

        /// <summary>
        /// v4.15 condense-tab: convert the per-group kill counts (already keyed by
        /// localized faction/category label) into the digest badge list, largest
        /// first and capped to the top 4 so the 击杀 cell stays compact.
        /// </summary>
        private static IReadOnlyList<KillByFactionView> BuildKillsByFaction(Dictionary<string, int> byFaction)
        {
            List<KillByFactionView> empty = new List<KillByFactionView>();
            if (byFaction == null || byFaction.Count == 0) return empty;
            List<KillByFactionView> list = new List<KillByFactionView>(byFaction.Count);
            foreach (var kv in byFaction)
            {
                list.Add(new KillByFactionView { Label = kv.Key, Count = kv.Value });
            }
            list.Sort((a, b) => b.Count.CompareTo(a.Count));
            if (list.Count > 4) list.RemoveRange(4, list.Count - 4);
            return list;
        }

        /// <summary>
        /// v4.15 condense-tab: maps the stored victim-category key to a localized
        /// label for the 击杀 cell badge group. Tokens, not hardcoded display text.
        /// </summary>
        private static string VictimCategoryLabel(string victimCategory)
        {
            if (victimCategory == ChronicleEventParams.VictimCategoryMechanoid)
            {
                return "PersonalChronicle.UI.FactionKindMechanoid".Translate().ToString();
            }
            if (victimCategory == ChronicleEventParams.VictimCategoryAnimal)
            {
                return "PersonalChronicle.UI.FactionKindAnimal".Translate().ToString();
            }
            // Humanlike victims without a known faction fall back to the generic label.
            return "PersonalChronicle.UI.FactionKindUnknown".Translate().ToString();
        }

        private static bool IsProductionEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey)) return false;
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && (def.kind == ChronicleEventKind.Craft || def.kind == ChronicleEventKind.Built);
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Archive.UI;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// Partial of <see cref="ArchiveMainTabWindow"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveMainTabWindow : MainTabWindow
    {

        private static string TabLabel(string tabKey)
        {
            return ("PersonalChronicle.UI.Tab." + tabKey).Translate().ToString();
        }

        private static string CategoryLabel(string categoryKey)
        {
            ArchiveCategoryDef def = DefDatabase<ArchiveCategoryDef>.AllDefs
                .FirstOrDefault(d => d.categoryKey == categoryKey);
            if (def != null && !def.label.NullOrEmpty())
            {
                return def.label;
            }
            return ("PersonalChronicle.UI.Category." + categoryKey).Translate().ToString();
        }

        private static string ObjectDisplayLabel(ArchiveObject obj)
        {
            if (obj == null)
            {
                return string.Empty;
            }
            if (obj is PawnObject pawn)
            {
                return string.IsNullOrEmpty(pawn.LabelShort) ? pawn.StableId : pawn.LabelShort;
            }
            if (obj is ThingObject thing)
            {
                return ThingDefLabel(thing.ThingDefName);
            }
            if (obj is BattleObject battle)
            {
                return IncidentDefLabel(battle.IncidentDefName);
            }
            if (obj is LocationObject location)
            {
                if (!string.IsNullOrEmpty(location.CellLabel))
                {
                    return location.CellLabel;
                }
                if (!string.IsNullOrEmpty(location.LabelSnapshot))
                {
                    return location.LabelSnapshot;
                }
                return location.StableId;
            }
            return !string.IsNullOrEmpty(obj.LabelSnapshot) ? obj.LabelSnapshot : obj.StableId;
        }

        private static string ObjectSubLabel(ArchiveObject obj)
        {
            if (obj is PawnObject pawn)
            {
                string life = pawn.IsArchived
                    ? "PersonalChronicle.UI.Dead".Translate().ToString()
                    : "PersonalChronicle.UI.Alive".Translate().ToString();
                return life + " · " + RoleLabel(pawn.Role);
            }
            if (obj is LocationObject location)
            {
                return location.MapId;
            }
            return string.Empty;
        }

        private static string RoleLabel(PawnRole role)
        {
            switch (role)
            {
                case PawnRole.Slave:
                    return "PersonalChronicle.UI.RoleSlave".Translate().ToString();
                case PawnRole.Prisoner:
                    return "PersonalChronicle.UI.RolePrisoner".Translate().ToString();
                default:
                    return "PersonalChronicle.UI.RoleFreeColonist".Translate().ToString();
            }
        }

        private static Color RolePillColor(PawnRole role)
        {
            switch (role)
            {
                case PawnRole.Slave:
                    return UITheme.PillGold;
                case PawnRole.Prisoner:
                    return UITheme.PillRed;
                default:
                    return AlivePill;
            }
        }

        private static Color ArchiveCardAccent(ArchiveObject obj)
        {
            PawnObject pawn = obj as PawnObject;
            if (pawn != null)
            {
                return RolePillColor(pawn.Role);
            }
            return obj != null && obj.CategoryKey == ArchiveCategoryKeys.Pawn
                ? ArchiveUiStyle.Info
                : ArchiveUiStyle.Accent;
        }

        private static string ResolveRefLabel(ObjectRef r, IArchiveService service)
        {
            if (r == null)
            {
                return string.Empty;
            }
            if (!string.IsNullOrEmpty(r.LabelSnapshot))
            {
                return r.LabelSnapshot;
            }
            if (service != null && !string.IsNullOrEmpty(r.StableId))
            {
                ArchiveObject o = service.GetObject(r.StableId);
                if (o != null)
                {
                    return ObjectDisplayLabel(o);
                }
            }
            return r.StableId ?? string.Empty;
        }

        private static string EventName(ChronicleEvent ev)
        {
            if (ev == null)
            {
                return string.Empty;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            if (def != null && !string.IsNullOrEmpty(def.labelKey))
            {
                return def.labelKey.Translate().ToString();
            }
            return ev.TypeKey;
        }

        private static string EventDescription(ChronicleEvent ev)
        {
            if (ev == null)
            {
                return string.Empty;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            if (def == null || string.IsNullOrEmpty(def.descriptionKey))
            {
                return string.Empty;
            }
            return def.descriptionKey.Translate().ToString();
        }

        private static string ChronicleEventTypeLabel(string typeKey)
        {
            if (string.IsNullOrEmpty(typeKey))
            {
                return "PersonalChronicle.UI.EvOther".Translate().ToString();
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(typeKey);
            if (def != null && !string.IsNullOrEmpty(def.LabelCap))
            {
                return def.LabelCap;
            }
            return "PersonalChronicle.UI.EvOther".Translate().ToString();
        }

        private static string EventTypeToGlyph(string typeKey)
        {
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(typeKey);
            if (def == null)
            {
                return "•";
            }
            switch (def.kind)
            {
                case ChronicleEventKind.Join:
                    return "✚";
                case ChronicleEventKind.Death:
                    return "✝";
                case ChronicleEventKind.Battle:
                    return "⚔";
                case ChronicleEventKind.Social:
                    return "❖";
                case ChronicleEventKind.Craft:
                    return "⚒";
                case ChronicleEventKind.Built:
                    return "▣";
                case ChronicleEventKind.Other:
                default:
                    return "•";
            }
        }

        private static Color EventTypeToColor(string typeKey)
        {
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(typeKey);
            if (def == null)
            {
                return ArchiveUiStyle.TimelineOther;
            }
            switch (def.kind)
            {
                case ChronicleEventKind.Join:
                    return ArchiveUiStyle.TimelineJoin;
                case ChronicleEventKind.Death:
                    return ArchiveUiStyle.TimelineDeath;
                case ChronicleEventKind.Battle:
                    return ArchiveUiStyle.TimelineBattle;
                case ChronicleEventKind.Social:
                    return ArchiveUiStyle.TimelineSocial;
                case ChronicleEventKind.Craft:
                    return ArchiveUiStyle.TimelineCraft;
                case ChronicleEventKind.Built:
                    return ArchiveUiStyle.TimelineBuilt;
                case ChronicleEventKind.Other:
                default:
                    return ArchiveUiStyle.TimelineOther;
            }
        }

        private static string KindLabel(PawnRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.KindDefName))
            {
                return string.Empty;
            }
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(record.KindDefName);
            if (kindDef != null && !string.IsNullOrEmpty(kindDef.label))
            {
                return kindDef.label;
            }
            if (kindDef == null)
            {
                LogMissingDefOnce(record.KindDefName);
            }
            return record.KindDefName;
        }

        private static string FactionLabel(PawnRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.FactionDefName))
            {
                return string.Empty;
            }
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(record.FactionDefName);
            if (factionDef != null && !string.IsNullOrEmpty(factionDef.label))
            {
                return factionDef.label;
            }
            if (factionDef == null)
            {
                LogMissingDefOnce(record.FactionDefName);
            }
            return record.FactionDefName;
        }

        private static string ThingDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            if (def == null)
            {
                LogMissingDefOnce(defName);
            }
            // Missing Def (e.g. third-party mod removed after the object was
            // archived): fall back to the raw defName — never crash, never red-text.
            return defName;
        }

        private static string ProductionDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            ThingCategoryDef cat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail(defName);
            if (cat != null && !string.IsNullOrEmpty(cat.label))
            {
                return cat.label;
            }
            // Neither resolved: it is a genuine missing def (third-party mod
            // uninstalled) — log once and degrade to the raw key.
            LogMissingDefOnce(defName);
            return defName;
        }

        private static string IncidentDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            if (def == null)
            {
                LogMissingDefOnce(defName);
            }
            return defName;
        }

        private static string FactionDefLabel(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return string.Empty;
            }
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
            if (def != null && !string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            return defName;
        }

        private static void LogMissingDefOnce(string defName)
        {
            if (string.IsNullOrEmpty(defName) || loggedMissingDefs.Contains(defName))
            {
                return;
            }
            loggedMissingDefs.Add(defName);
            // Warning (not Error): expected after mod removal; no red-text.
            ChronicleLog.Warning(ChronicleLog.Category.Ui, "missing def for display: " + defName);
        }

        private static string FormatDate(long tick)
        {
            // tick 0 是新档第 1 天（开局殖民者 JoinTick=0 即此），是合法日期；
            // 仅 -1（未知哨兵）才显示"未知"。
            if (tick < 0L)
            {
                return "PersonalChronicle.UI.UnknownDate".Translate().ToString();
            }
            return GenDate.DateReadoutStringAt(tick, Vector2.zero);
        }

        private static string CauseLabel(string deathCauseKey)
        {
            if (string.IsNullOrEmpty(deathCauseKey))
            {
                return string.Empty;
            }
            return deathCauseKey.Translate().ToString();
        }

        private static bool IsSocialEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Social;
        }

        private static bool IsDeathEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Death;
        }

        private static bool IsBattleEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Battle;
        }

        private static bool IsCraftEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Craft;
        }

        private static bool IsBuiltEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
            {
                return false;
            }
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && def.kind == ChronicleEventKind.Built;
        }
    }

    internal static class ArchiveCacheExtensions
    {
        public static int GetCount(this Dictionary<string, List<ArchiveObject>> dict, string key)
        {
            if (dict != null && dict.TryGetValue(key, out List<ArchiveObject> list) && list != null)
            {
                return list.Count;
            }
            return 0;
        }
    }
}

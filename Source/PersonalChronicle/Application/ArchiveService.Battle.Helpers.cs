using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Api;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// [ARC-013] <see cref="ArchiveService"/> 战斗域的「事件构造与登记辅助」切片。
    ///
    /// 原 ArchiveService.Battle.cs 单文件 1027 行，混合「战斗生命周期」「物品事实录入」
    /// 与「通用事件构造」三组职责，违反 [ARC-013] 单一职责与「单文件 ≤ 800 行」。
    /// 按 08 §3「排序→识别→剥离」，本文件剥离其中**叶子功能单元**（无下游依赖的
    /// 事件构造 / 事件登记 / 装备判定辅助），主文件保留战斗生命周期与物品事实录入。
    ///
    /// 剥离原则（[ARC-013] 零契约改动）：仅物理搬移，类名、方法签名、访问级别、
    /// Save schema、翻译键一律未改；同 partial 类成员天然互相可见，跨文件调用无需改访问级别。
    /// </summary>
    public sealed partial class ArchiveService
    {
        private void AddEvent(ChronicleGameComponent component, string stableId, ChronicleEvent ev)
        {
            int maxEvents = PersonalChronicleMod.Settings != null ? PersonalChronicleMod.Settings.MaxEventsPerPawn : 200;
            if (maxEvents <= 0)
            {
                return;
            }
            if (ev == null)
            {
                return;
            }
            ev.ImportanceLevel = (int)ChronicleEventImportance.Resolve(ev);
            int retentionLimit = ev.ImportanceLevel <= (int)ChronicleImportance.Routine
                ? Math.Max(24, maxEvents / 3)
                : maxEvents;
            if (component.GetEventsFor(stableId).Count >= retentionLimit)
            {
                return;
            }
            component.AddEvent(ev);
        }

        private static ChronicleEvent BuildPawnEvent(string stableId, string labelSnapshot, string typeKey)
        {
            return new ChronicleEvent
            {
                Tick = Find.TickManager.TicksGame,
                TypeKey = typeKey,
                Primary = ObjectRef.ForPawn(stableId, labelSnapshot),
                Subjects = new List<ObjectRef>(),
                Params = new Dictionary<string, string>()
            };
        }

        private static ChronicleEvent BuildThingEvent(string stableId, string typeKey)
        {
            return new ChronicleEvent
            {
                Tick = Find.TickManager.TicksGame,
                TypeKey = typeKey,
                Primary = new ObjectRef(ArchiveCategoryKeys.Thing, stableId, null),
                Subjects = new List<ObjectRef>(),
                Params = new Dictionary<string, string>()
            };
        }

        private static void AddPawnSubject(ChronicleEvent ev, Pawn worker)
        {
            if (ev == null || worker == null)
            {
                return;
            }
            ev.Subjects.Add(ObjectRef.ForPawn(worker.GetUniqueLoadID(), worker.LabelShort));
        }

        private static void RegisterThingObject(ChronicleGameComponent component, Thing thing, Pawn holder = null)
        {
            if (component == null || thing == null || thing.def == null)
            {
                return;
            }
            // v4.6.5: the "Thing" category is scoped to equipment only
            // (weapons + wearable apparel). Raw materials, food and buildings
            // are excluded from the archive object graph.
            if (!IsEquipable(thing))
            {
                return;
            }
            string stableId = thing.def.defName + ":" + thing.thingIDNumber;
            if (component.GetObject(stableId) == null)
            {
                component.AddObject(new ThingObject
                {
                    StableId = stableId,
                    ThingDefName = thing.def.defName,
                    WeakId = stableId
                });
            }
            if (holder != null)
            {
                NoteWeaponHolder(component, stableId, holder);
            }
        }

        private static bool IsEquipable(Thing thing)
        {
            if (thing == null || thing.def == null)
            {
                return false;
            }
            return IsEquipableDef(thing.def);
        }

        private static bool IsEquipableDef(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }
            // v4.9.1: data-driven equipment archive policy — weapons always in;
            // apparel only when it carries enough armor to count as combat apparel
            // (dust jackets / work wear / fashion clothes are excluded). Mirrors
            // Patch_ThingDestroy so capture and decommission scopes stay aligned.
            return Domain.ThingArchivePolicy.Captures(def);
        }

        private static bool HasRecordedExternalKill(
            ChronicleGameComponent component,
            string killerId,
            string victimId)
        {
            if (component == null || string.IsNullOrEmpty(killerId) || string.IsNullOrEmpty(victimId))
            {
                return false;
            }
            IReadOnlyList<ChronicleEvent> events = component.GetEventsFor(killerId);
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev == null || ev.TypeKey != ChronicleEventType.Death || ev.Params == null)
                {
                    continue;
                }
                string role;
                string recordedVictimId;
                if (ev.Params.TryGetValue(ChronicleEventParams.CombatRole, out role)
                    && role == ChronicleEventParams.CombatRoleKill
                    && ev.Params.TryGetValue(ChronicleEventParams.VictimStableId, out recordedVictimId)
                    && recordedVictimId == victimId)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasRecordedDeathForVictim(ChronicleGameComponent component, string victimId)
        {
            if (component == null || string.IsNullOrEmpty(victimId) || component.Events == null)
            {
                return false;
            }
            for (int i = 0; i < component.Events.Count; i++)
            {
                ChronicleEvent ev = component.Events[i];
                if (ev == null || ev.TypeKey != ChronicleEventType.Death || ev.Params == null)
                {
                    continue;
                }
                string recordedVictimId;
                if (ev.Params.TryGetValue(ChronicleEventParams.VictimStableId, out recordedVictimId)
                    && recordedVictimId == victimId)
                {
                    return true;
                }
            }
            return false;
        }
    }
}

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
    /// [ARC-013] <see cref="ArchiveService"/> 的「物品事实录入」切片
    /// （OnThingCrafted / OnThingDestroyed / OnThingBuilt）。
    ///
    /// 这组方法原本混在 ArchiveService.Battle.cs（战斗域）中，与文件主题职责错配，
    /// 违反 [ARC-013]「一个文件 / 一个 partial 只承载一类职责」。现按业务域归位到本文件。
    ///
    /// 剥离原则（[ARC-013] 零契约改动）：仅物理搬移，方法签名与访问级别未改
    /// （仍为 IArchiveService / IArchiveEventSink 契约实现），同 partial 类成员天然互相可见。
    /// </summary>
    public sealed partial class ArchiveService
    {
        public void OnThingCrafted(Thing product, Pawn worker)
        {
            if (!IsRecordingEnabled() || product == null || product.def == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                bool workerIsCurrent = worker != null
                    && ChronicleColonistScanner.TryClassifyCurrent(worker, out _);
                if (workerIsCurrent)
                {
                    int quantity = Math.Max(1, product.stackCount);
                    float unitValue = product.MarketValue;
                    if (float.IsNaN(unitValue) || float.IsInfinity(unitValue) || unitValue < 0f)
                    {
                        unitValue = 0f;
                    }
                    EnsurePawnArchivedForCapture(component, worker);
                    component.AddProduction(
                        worker.GetUniqueLoadID(),
                        product.def.defName,
                        quantity,
                        unitValue * quantity,
                        Find.TickManager.TicksGame);
                }
                string stableId = product.def.defName + ":" + product.thingIDNumber;
                // v4.6.5: only equipment (weapons + apparel) enters the archive
                // object graph and gets a Crafted event; raw materials / food
                // stay as pure production stats above.
                if (IsEquipable(product))
                {
                    RegisterThingObject(component, product, worker);
                    ChronicleEvent ev = BuildThingEvent(stableId, ChronicleEventType.Crafted);
                    if (workerIsCurrent)
                    {
                        AddPawnSubject(ev, worker);
                    }
                    string eventOwnerId = workerIsCurrent
                        ? worker.GetUniqueLoadID()
                        : stableId;
                    AddEvent(component, eventOwnerId, ev);
                }
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record crafted thing " + (product != null && product.def != null ? product.def.defName : "null") + ": " + ex.Message);
            }
        }

        public void OnThingDestroyed(Thing thing, Pawn lastHolder = null)
        {
            if (!IsRecordingEnabled() || thing == null || thing.def == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                string stableId = thing.def.defName + ":" + thing.thingIDNumber;
                ThingObject thingObj = component.GetObject(stableId) as ThingObject;
                if (thingObj == null || thingObj.Decommission != null)
                {
                    // Not archived (never a chronicle thing) or already retired.
                    return;
                }
                DecommissionRecord rec = new DecommissionRecord
                {
                    Tick = Find.TickManager.TicksGame,
                    LastPlaceLabel = PlaceLabelForDestroyedThing(thing)
                };
                if (lastHolder != null)
                {
                    rec.LastHolderStableId = lastHolder.GetUniqueLoadID();
                    rec.LastHolderLabel = lastHolder.LabelShort;
                }
                else if (!string.IsNullOrEmpty(thingObj.CurrentHolderId))
                {
                    ArchiveObject cur = component.GetObject(thingObj.CurrentHolderId);
                    if (cur != null)
                    {
                        rec.LastHolderStableId = cur.StableId;
                        rec.LastHolderLabel = !string.IsNullOrEmpty(cur.LabelSnapshot)
                            ? cur.LabelSnapshot
                            : cur.StableId;
                    }
                }
                // Service days: derived from the tenure span (first record start →
                // now) so the number stays consistent with the legacy chain.
                if (thingObj.HolderRecords != null && thingObj.HolderRecords.Count > 0)
                {
                    long start = thingObj.HolderRecords[0].StartTick;
                    if (start > 0L)
                    {
                        rec.ServiceDays = Math.Max(0,
                            (int)GenDate.TicksToDays((int)(Find.TickManager.TicksGame - start)));
                    }
                }
                thingObj.Decommission = rec;
                component.MarkChanged();
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record decommission for "
                    + (thing != null && thing.def != null ? thing.def.defName : "null") + ": " + ex.Message);
            }
        }

        private static string PlaceLabelForDestroyedThing(Thing thing)
        {
            if (thing == null) return "—";
            Map map = thing.Map;
            if (map != null && map.Biome != null && !string.IsNullOrEmpty(map.Biome.defName))
            {
                return map.Biome.defName;
            }
            return "—";
        }

        public void OnThingBuilt(ThingDef builtDef, string builtStableId, Pawn worker)
        {
            if (!IsRecordingEnabled() || builtDef == null || string.IsNullOrEmpty(builtStableId))
            {
                return;
            }
            // v4.6.5: buildings are not equipment — excluded from the archive
            // object graph (the "Thing" category is scoped to weapons + apparel).
            if (!IsEquipableDef(builtDef))
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                if (component.GetObject(builtStableId) == null)
                {
                    component.AddObject(new ThingObject
                    {
                        StableId = builtStableId,
                        ThingDefName = builtDef.defName,
                        WeakId = builtStableId
                    });
                }
                bool workerIsCurrent = worker != null
                    && ChronicleColonistScanner.TryClassifyCurrent(worker, out _);
                if (workerIsCurrent)
                {
                    EnsurePawnArchivedForCapture(component, worker);
                    component.AddProduction(
                        worker.GetUniqueLoadID(),
                        builtDef.defName,
                        1,
                        builtDef.BaseMarketValue,
                        Find.TickManager.TicksGame);
                }
                ChronicleEvent ev = BuildThingEvent(builtStableId, ChronicleEventType.Built);
                if (workerIsCurrent)
                {
                    AddPawnSubject(ev, worker);
                }
                string eventOwnerId = workerIsCurrent
                    ? worker.GetUniqueLoadID()
                    : builtStableId;
                AddEvent(component, eventOwnerId, ev);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record built thing " + (builtDef != null ? builtDef.defName : "null") + ": " + ex.Message);
            }
        }
    }
}

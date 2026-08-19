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
    /// Partial of <see cref="ArchiveService"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
    {

        public ConsumptionSummaryView GetConsumptionSummary(string stableId)
        {
            PawnObject pawn = GetObject(stableId) as PawnObject;
            if (pawn == null || pawn.Consumption == null)
            {
                return new ConsumptionSummaryView(0f, 0f, 0f, new List<ConsumptionTypeView>());
            }
            ConsumptionAccumulator acc = pawn.Consumption;
            List<ConsumptionTypeView> rows = new List<ConsumptionTypeView>();
            if (acc.SilverByCategory != null)
            {
                foreach (KeyValuePair<string, float> pair in acc.SilverByCategory)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0f)
                    {
                        continue;
                    }
                    rows.Add(new ConsumptionTypeView(pair.Key, pair.Value));
                }
            }
            rows.Sort((a, b) => b.Silver.CompareTo(a.Silver));
            long now = Find.TickManager.TicksGame;
            float weekly = acc.SilverSince(now - 7L * RimWorld.GenDate.TicksPerDay);
            float monthly = acc.SilverSince(now - 30L * RimWorld.GenDate.TicksPerDay);
            float daily = monthly / 30f;
            return new ConsumptionSummaryView(
                acc.TotalSilver,
                weekly,
                daily,
                rows);
        }

        public void OnThingConsumed(Pawn eater, Thing food)
        {
            if (!IsRecordingEnabled() || eater == null || food == null || food.def == null)
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
                bool isCurrent = ChronicleColonistScanner.TryClassifyCurrent(eater, out _);
                if (!isCurrent)
                {
                    return;
                }
                float unitValue = food.def.BaseMarketValue;
                if (unitValue <= 0f)
                {
                    return;
                }
                EnsurePawnArchivedForCapture(component, eater);
                string category = (food.def.FirstThingCategory != null)
                    ? food.def.FirstThingCategory.defName
                    : "Other";
                component.AddConsumption(
                    eater.GetUniqueLoadID(),
                    category,
                    unitValue,
                    Find.TickManager.TicksGame);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record consumed thing " + (food != null && food.def != null ? food.def.defName : "null") + ": " + ex.Message);
            }
        }

        public void OnWorkplaceUsed(Pawn worker, Building_WorkTable workbench)
        {
            if (!IsRecordingEnabled() || worker == null || workbench == null || workbench.def == null)
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
                bool workerIsCurrent = ChronicleColonistScanner.TryClassifyCurrent(worker, out _);
                if (!workerIsCurrent)
                {
                    return;
                }
                EnsurePawnArchivedForCapture(component, worker);
                // 工坊所在房间角色：RegionAndRoomQuery.GetRoom(Thing, RegionType)
                // —— 1.6 经反射核验的静态 API（Thing/Building 上无 GetRoom 方法）。
                string roomRoleDefName = null;
                Room room = workbench.Map != null
                    ? Verse.RegionAndRoomQuery.GetRoom(workbench, Verse.RegionType.Set_All)
                    : null;
                if (room != null && room.Role != null)
                {
                    roomRoleDefName = room.Role.defName;
                }
                string buildingStableId = workbench.def.defName + ":" + workbench.thingIDNumber;
                component.AddWorkplaceUse(
                    worker.GetUniqueLoadID(),
                    workbench.def.defName,
                    buildingStableId,
                    roomRoleDefName,
                    Find.TickManager.TicksGame);
                // v1.1.4 UI 拓展：记录工坊坐标（供 ITab 定位跳转）。
                PawnObject workRecord = component.GetObject(worker.GetUniqueLoadID()) as PawnObject;
                if (workRecord != null && workRecord.Workplace != null)
                {
                    workRecord.Workplace.RecordLocation(workbench.Map != null ? workbench.Map.Index : -1, workbench.Position);
                }
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record workplace use for " + (worker != null ? worker.LabelShort : "null") + ": " + ex.Message);
            }
        }


    }
}

using System;
using System.Collections.Generic;
using PersonalChronicle.Data.PawnEconomy;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        private WorkOrderTripSaveData workOrderTrips = new WorkOrderTripSaveData();
        private bool tripSchemaWarned;

        internal void ExposeWorkOrderTrips()
        {
            if (Scribe.mode == LoadSaveMode.Saving && !workOrderTrips.Blocked && Verse.Current.Game != null)
            {
                // The periodic PawnByKey index can lag behind newly arrived
                // workers. Never prune authoritative pickup proof using it.
                var live = new Dictionary<string, Pawn>(StringComparer.Ordinal);
                foreach (Map map in Find.Maps)
                    foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                        if (pawn?.Faction == RimWorld.Faction.OfPlayer) live[pawn.GetUniqueLoadID()] = pawn;
                workOrderTrips.RemoveInactive(row =>
                {
                    return live.TryGetValue(row.Worker, out Pawn worker) && worker.Map != null
                        && worker.Map.uniqueID == row.Map && worker.CurJob?.loadID == row.Job;
                });
            }
            Scribe_Deep.Look(ref workOrderTrips, "pawnEconomicWorkOrderTrips");
            if (workOrderTrips == null) workOrderTrips = new WorkOrderTripSaveData();
            WarnTripSchemaOnce();
        }

        private void WarnTripSchemaOnce()
        {
            if (!workOrderTrips.Blocked || tripSchemaWarned) return;
            tripSchemaWarned = true;
            Log.Warning("[PC][Economy] work-order trip payload retained; new trip evidence disabled: " + workOrderTrips.BlockReason);
        }

        internal bool StartHaulTrip(VanillaOrderIntent intent, Job job, int count)
        {
            if (intent == null || job == null || count <= 0 || intent.Order != "Haul") return false;
            WarnTripSchemaOnce();
            return workOrderTrips.Upsert(new WorkOrderTripSaveData.Row { Worker = intent.Worker.GetUniqueLoadID(),
                Contract = intent.ContractId, Order = intent.Order, WorkType = intent.WorkType, Target = intent.TargetId,
                Map = intent.MapId, Job = job.loadID, Expected = count });
        }
        internal bool RestoreHaulTrip(Pawn worker, Job job, out VanillaOrderIntent intent, out int expected, out int delivered)
        {
            intent = null; expected = delivered = 0;
            WarnTripSchemaOnce();
            if (worker?.Map == null || !worker.Spawned || worker.Dead || job == null || worker.CurJob != job
                || !workOrderTrips.TryGet(worker.GetUniqueLoadID(), job.loadID, out var row)) return false;
            var contract = ActiveContractFor(row.Worker);
            if (worker.Faction != RimWorld.Faction.OfPlayer || row.Map != worker.Map.uniqueID
                || contract == null || contract.ContractId != row.Contract) return false;
            intent = new VanillaOrderIntent(row.Order, row.WorkType, row.Target, worker, row.Map, row.Contract);
            expected = row.Expected; delivered = row.Delivered;
            return true;
        }
        internal bool RecordHaulDelivery(VanillaOrderIntent intent, int jobId, int added)
            => intent != null && workOrderTrips.AddDelivered(intent.Worker.GetUniqueLoadID(), jobId, added);
        internal void FinishHaulTrip(VanillaOrderIntent intent, int jobId)
        {
            if (intent != null) workOrderTrips.Remove(intent.Worker.GetUniqueLoadID(), jobId);
        }
    }
}

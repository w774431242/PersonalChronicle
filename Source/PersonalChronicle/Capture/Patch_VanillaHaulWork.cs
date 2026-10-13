using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Capture
{
    /// <summary>One hauling trip, paid only after every carried item was actually delivered.</summary>
    internal sealed class HaulDeliveryProgress
    {
        internal readonly int Expected;
        internal int Delivered { get; private set; }
        internal bool Reported;
        internal HaulDeliveryProgress(int expected) { Expected = Math.Max(0, expected); }
        internal void Add(int added) { if (added > 0) Delivered = (int)Math.Min(int.MaxValue, (long)Delivered + added); }
        internal bool Complete(bool carryingEmpty) => !Reported && Expected > 0 && Delivered >= Expected && carryingEmpty;
    }

    internal static class VanillaHaulWork
    {
        private sealed class Trip
        {
            internal VanillaOrderIntent Intent;
            internal HaulDeliveryProgress Progress;
            internal int JobId;
        }
        private sealed class Delivery
        {
            internal Pawn Worker;
            internal Trip Trip;
            internal IntVec3 Cell;
            internal bool ToCell;
            internal int Before;
        }
        // Job-owned weak keys do not retain ended jobs or their pawns/maps.
        private static readonly ConditionalWeakTable<Job, Trip> Trips = new ConditionalWeakTable<Job, Trip>();
        [ThreadStatic] private static Delivery currentDelivery;

        internal static bool IsOrdinaryHaulJob(string jobDefName)
            => jobDefName == "HaulToCell" || jobDefName == "HaulToContainer";

        internal static void WrapPickup(Toil toil, TargetIndex index)
        {
            if (toil?.initAction == null) return;
            Action native = toil.initAction;
            toil.initAction = () =>
            {
                Pawn worker = toil.actor;
                Job job = worker?.CurJob;
                VanillaOrderIntent intent = null;
                try
                {
                    if (job != null && IsOrdinaryHaulJob(job.def?.defName) && !Trips.TryGetValue(job, out _))
                        intent = GameComponent_PawnEconomy.Current?.CaptureVanillaOrderIntent("Haul", job.GetTarget(index).Thing, worker);
                }
                catch (Exception ex) { Log.ErrorOnce("[PC][Economy] haul pickup intent failed: " + ex, 0x5A1817); }
                native(); // The original action always runs exactly once; its exceptions keep their native behavior.
                try
                {
                    int count = worker?.carryTracker?.CarriedThing?.stackCount ?? 0;
                    if (intent != null && worker.CurJob == job && count > 0
                        && GameComponent_PawnEconomy.Current?.StartHaulTrip(intent, job, count) == true)
                        Trips.Add(job, new Trip { Intent = intent, Progress = new HaulDeliveryProgress(count), JobId = job.loadID });
                }
                catch (Exception ex) { Log.ErrorOnce("[PC][Economy] haul pickup snapshot failed: " + ex, 0x5A1818); }
            };
        }

        internal static void WrapDelivery(Toil toil, TargetIndex destination, bool toCell, bool storageMode = true)
        {
            if (toil?.initAction == null) return;
            Action native = toil.initAction;
            toil.initAction = () =>
            {
                Delivery delivery = null;
                Job job = toil.actor?.CurJob;
                try
                {
                    if (storageMode && job != null && IsOrdinaryHaulJob(job.def?.defName))
                    {
                        var runtime = GameComponent_PawnEconomy.Current;
                        if (!Trips.TryGetValue(job, out Trip trip) && runtime != null
                            && runtime.RestoreHaulTrip(toil.actor, job,
                                out VanillaOrderIntent restored, out int expected, out int delivered))
                        {
                            trip = new Trip { Intent = restored, Progress = new HaulDeliveryProgress(expected), JobId = job.loadID };
                            trip.Progress.Add(delivered);
                            Trips.Add(job, trip);
                        }
                        if (trip != null) delivery = new Delivery { Worker = toil.actor, Trip = trip, ToCell = toCell,
                            Cell = job.GetTarget(destination).Cell, Before = toil.actor.carryTracker?.CarriedThing?.stackCount ?? 0 };
                    }
                }
                catch (Exception ex) { Log.ErrorOnce("[PC][Economy] haul delivery snapshot failed: " + ex, 0x5A1819); }
                Delivery previous = currentDelivery;
                currentDelivery = delivery;
                try { native(); }
                finally { currentDelivery = previous; }
                try
                {
                    if (delivery != null && delivery.Trip.Progress.Complete(delivery.Worker.carryTracker?.CarriedThing == null))
                    {
                        delivery.Trip.Progress.Reported = true;
                        GameComponent_PawnEconomy.Current?.ReportVanillaOrderCompletion(delivery.Trip.Intent,
                            Find.TickManager.TicksGame, "haul:" + job.loadID + ":" + delivery.Trip.Intent.TargetId);
                        GameComponent_PawnEconomy.Current?.FinishHaulTrip(delivery.Trip.Intent, job.loadID);
                    }
                }
                catch (Exception ex) { Log.ErrorOnce("[PC][Economy] haul trip completion failed: " + ex, 0x5A181A); }
            };
        }

        internal static void WrapPlacedAction(Pawn_CarryTracker tracker, IntVec3 cell, ref Action<Thing, int> action)
        {
            Delivery delivery = currentDelivery;
            if (delivery == null || !delivery.ToCell || tracker.pawn != delivery.Worker || cell != delivery.Cell) return;
            Action<Thing, int> native = action;
            action = (thing, added) =>
            {
                native?.Invoke(thing, added);
                try
                {
                    // A native placement callback is actual transfer evidence;
                    // removing a designation or destroying the remainder is not.
                    if (thing?.Spawned != true || thing.Map != delivery.Worker.Map || thing.Position != cell) return;
                    var group = thing.Map.haulDestinationManager.SlotGroupAt(cell);
                    if (group != null && group.Settings.AllowedToAccept(thing)) ConfirmDelivery(delivery.Trip, added);
                }
                catch (Exception ex) { Log.ErrorOnce("[PC][Economy] haul placement evidence failed: " + ex, 0x5A181B); }
            };
        }

        internal static void ContainerDeposited()
        {
            Delivery delivery = currentDelivery;
            if (delivery == null || delivery.ToCell) return;
            int remaining = delivery.Worker.carryTracker?.CarriedThing?.stackCount ?? 0;
            // Vanilla invokes onDeposited only when TryTransferToContainer > 0.
            ConfirmDelivery(delivery.Trip, Math.Max(0, delivery.Before - remaining));
        }

        private static void ConfirmDelivery(Trip trip, int added)
        {
            // Persist proof before trusting the ephemeral progress counter. An
            // unknown/malformed payload refuses new evidence on both paths.
            if (GameComponent_PawnEconomy.Current?.RecordHaulDelivery(trip.Intent, trip.JobId, added) == true)
                trip.Progress.Add(added);
        }
    }

    [HarmonyPatch(typeof(Toils_Haul), nameof(Toils_Haul.StartCarryThing), new Type[] {
        typeof(TargetIndex), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    public static class Patch_VanillaHaulPickup
    {
        private static void Postfix(Toil __result, TargetIndex haulableInd)
        {
            try { VanillaHaulWork.WrapPickup(__result, haulableInd); }
            catch (Exception ex) { Log.ErrorOnce("[PC][Economy] haul pickup decorator failed: " + ex, 0x5A181C); }
        }
    }

    [HarmonyPatch(typeof(Toils_Haul), nameof(Toils_Haul.PlaceHauledThingInCell),
        new Type[] { typeof(TargetIndex), typeof(Toil), typeof(bool), typeof(bool) })]
    public static class Patch_VanillaHaulCell
    {
        private static void Postfix(Toil __result, TargetIndex cellInd, bool storageMode)
        {
            try { VanillaHaulWork.WrapDelivery(__result, cellInd, true, storageMode); }
            catch (Exception ex) { Log.ErrorOnce("[PC][Economy] haul cell decorator failed: " + ex, 0x5A181D); }
        }
    }

    [HarmonyPatch]
    public static class Patch_VanillaHaulDrop
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(Pawn_CarryTracker),
            nameof(Pawn_CarryTracker.TryDropCarriedThing), new Type[] {
                typeof(IntVec3), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(Action<Thing, int>) });
        private static void Prefix(Pawn_CarryTracker __instance, IntVec3 dropLoc, ref Action<Thing, int> placedAction)
        {
            try { VanillaHaulWork.WrapPlacedAction(__instance, dropLoc, ref placedAction); }
            catch (Exception ex) { Log.ErrorOnce("[PC][Economy] haul drop decorator failed: " + ex, 0x5A181E); }
        }
    }

    [HarmonyPatch(typeof(Toils_Haul), nameof(Toils_Haul.DepositHauledThingInContainer),
        new Type[] { typeof(TargetIndex), typeof(TargetIndex), typeof(Action) })]
    public static class Patch_VanillaHaulContainer
    {
        private static void Prefix(ref Action onDeposited)
        {
            Action native = onDeposited;
            onDeposited = () =>
            {
                native?.Invoke();
                try { VanillaHaulWork.ContainerDeposited(); }
                catch (Exception ex) { Log.ErrorOnce("[PC][Economy] haul container evidence failed: " + ex, 0x5A181F); }
            };
        }
        private static void Postfix(Toil __result, TargetIndex containerInd)
        {
            try { VanillaHaulWork.WrapDelivery(__result, containerInd, false); }
            catch (Exception ex) { Log.ErrorOnce("[PC][Economy] haul container decorator failed: " + ex, 0x5A1820); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using PersonalChronicle.Domain.PawnEconomy.Employment;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    internal static class LaborOutputCaptureRules
    {
        public static bool ValidPlacedIncrement(int quantity, int resultingStackCount)
            => quantity > 0 && resultingStackCount >= quantity;
        public static bool MatchesEvidence(string sourceContract, string sourceOrder, string sourceEvidence,
            string contract, string order, string evidence)
            => !string.IsNullOrEmpty(evidence) && string.Equals(sourceContract, contract, StringComparison.Ordinal)
                && string.Equals(sourceOrder, order, StringComparison.Ordinal)
                && string.Equals(sourceEvidence, evidence, StringComparison.Ordinal);
        public static string BatchIdFor(string contract, string evidence, string def, int thingId)
            => ProductBatch.IdFor(contract, evidence, def) + "|output:" + thingId;
        public static string EvidenceIdFor(string contract, string work, string target, string pawn, int tick)
            => new WorkEvidence(contract, "vanilla:" + work + ":" + target + ":" + pawn,
                "VanillaWork", tick, 1, "job", tick).EvidenceId;
    }

    /// <summary>CHG-20261011-LABOR-VISUAL：真实落地 callback 增量缓冲；不找附近物、不复算产量。</summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        internal sealed class GeneratedOutputContext
        {
            public string PawnKey, ContractId, OrderKey, EvidenceId;
            public int Tick;
        }

        private sealed class GeneratedOutput
        {
            public string DefName, OrderKey, ContractId, EvidenceId, HolderKey;
            public int ThingId, StackCount, Tick;
        }

        private const int MaximumOutputPawns = 512, MaximumOutputsPerPawn = 24;
        private const int OutputNoteLifetimeTicks = 600;
        private readonly Dictionary<string, List<GeneratedOutput>> generatedOutputs
            = new Dictionary<string, List<GeneratedOutput>>(StringComparer.Ordinal);

        /// <summary>在原版落地/合堆操作之前冻结实际 Job 目标、劳动者、合同与未来完成证据身份。</summary>
        internal GeneratedOutputContext BeginGeneratedOutputCapture(Thing generated, IntVec3 center, Map map)
        {
            if (generated?.def?.category != ThingCategory.Item || map == null) return null;
            var colonists = map.mapPawns?.FreeColonistsSpawned;
            if (colonists == null) return null;
            Pawn producer = null;
            string order = null;
            for (int i = 0; i < colonists.Count; i++)
            {
                var candidate = colonists[i];
                var job = candidate?.CurJob;
                var target = job?.targetA.Thing;
                if (target?.def == null || candidate.Map != map || candidate.Faction != Faction.OfPlayer) continue;
                bool tree = target is Plant plant && plant.def.plant?.IsTree == true;
                string candidateOrder = WorkPriceRules.OrderForJob(job.def.defName, tree);
                if (candidateOrder == null && job.def.defName == "MineVein") candidateOrder = "Mine";
                bool mining = candidateOrder == "Mine" && target is Mineable && target.Position == center
                    && target.def.building?.mineableThing == generated.def;
                bool plants = (candidateOrder == "ChopWood" || candidateOrder == "Harvest" || candidateOrder == "CutPlants")
                    && target is Plant && candidate.Position == center && target.Position.AdjacentTo8WayOrInside(center);
                if (!mining && !plants) continue;
                if (producer != null) return null; // 同目标上下文有歧义：不把产出记到路人头上。
                producer = candidate; order = candidateOrder;
            }
            if (producer == null) return null;
            string pawnKey = producer.GetUniqueLoadID();
            var contract = ActiveContractFor(pawnKey);
            if (contract == null || !WorkPriceRules.Parse(contract.PositionKey, out string work, out _, out string scopedOrder)
                || scopedOrder != order || !LaborFlowRules.RequiresPhysicalDelivery(LaborFlowRules.CapabilityFor(order))) return null;
            if (!IsNativeProductionCaller(order)) return null;
            int now = Find.TickManager?.TicksGame ?? 0;
            return new GeneratedOutputContext
            {
                PawnKey = pawnKey, ContractId = contract.ContractId, OrderKey = order,
                EvidenceId = LaborOutputCaptureRules.EvidenceIdFor(contract.ContractId, work,
                    producer.CurJob.targetA.Thing.GetUniqueLoadID(), pawnKey, now),
                Tick = now
            };
        }

        /// <summary>确认生成调用来自实际原版产出函数，普通搬运/掉落不因附近有人工作而变成新产出。</summary>
        private static bool IsNativeProductionCaller(string order)
        {
            var frames = new StackTrace(false).GetFrames();
            if (frames == null) return false;
            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                for (Type type = method?.DeclaringType; type != null; type = type.DeclaringType)
                {
                    if (order == "Mine" && type == typeof(Mineable) && method.Name == "TrySpawnYield") return true;
                    if (order != "Mine" && type == typeof(JobDriver_PlantWork)) return true;
                }
            }
            return false;
        }

        /// <summary>消费原版 placedAction(实际存活Thing, 本次增量)，合堆后的原库存不属于本次劳动。</summary>
        internal void NoteGeneratedThing(GeneratedOutputContext context, Thing placed, int quantity)
        {
            if (context == null || placed?.def?.category != ThingCategory.Item || placed.Destroyed || !placed.Spawned
                || !LaborOutputCaptureRules.ValidPlacedIncrement(quantity, placed.stackCount)) return;
            if (!generatedOutputs.TryGetValue(context.PawnKey, out var notes))
            {
                if (generatedOutputs.Count >= MaximumOutputPawns) return;
                generatedOutputs[context.PawnKey] = notes = new List<GeneratedOutput>(4);
            }
            foreach (var note in notes)
            {
                if (note.ThingId != placed.thingIDNumber || note.DefName != placed.def.defName
                    || note.ContractId != context.ContractId || note.EvidenceId != context.EvidenceId) continue;
                note.StackCount = checked(note.StackCount + quantity);
                return; // 同一次完成证据分多次吸收进同一载体，增量相加，不能按tick吞掉真实第二次回调。
            }
            if (notes.Count >= MaximumOutputsPerPawn) return; // 不删尚未绑定的真实来源；缺口交由对账。
            notes.Add(new GeneratedOutput
            {
                DefName = placed.def.defName, OrderKey = context.OrderKey, ContractId = context.ContractId,
                EvidenceId = context.EvidenceId, HolderKey = "cell:" + placed.Position.x + "," + placed.Position.z,
                ThingId = placed.thingIDNumber, StackCount = quantity, Tick = context.Tick
            });
        }

        /// <summary>证据成立后只消费同合同、同订单、同目标的产出；每个真实载体保留独立稳定批次身份。</summary>
        private void RegisterEvidenceOutputs(Pawn pawn, WageObligation obligation, int now)
        {
            if (pawn == null || obligation == null) return;
            string pawnKey = pawn.GetUniqueLoadID();
            if (!generatedOutputs.TryGetValue(pawnKey, out var notes) || notes.Count == 0) return;
            var contract = Employment.ReadContract(obligation.ContractId);
            if (contract == null || !WorkPriceRules.Parse(contract.PositionKey, out _, out _, out string order)
                || !LaborFlowRules.RequiresPhysicalDelivery(LaborFlowRules.CapabilityFor(order))) return;
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                var note = notes[i];
                if (note == null) { notes.RemoveAt(i); continue; }
                if (!LaborOutputCaptureRules.MatchesEvidence(note.ContractId, note.OrderKey, note.EvidenceId,
                    contract.ContractId, order, obligation.EvidenceId)) continue;
                string id = LaborOutputCaptureRules.BatchIdFor(contract.ContractId, obligation.EvidenceId, note.DefName, note.ThingId);
                ProductBatch existing = null;
                foreach (var batch in LaborFlow.ReadBatchesFor(contract.ContractId))
                    if (batch.BatchId == id) { existing = batch; break; }
                if (existing != null)
                {
                    if (existing.ProducedQuantity == note.StackCount && existing.ProducerPawnKey == pawnKey
                        && existing.ThingIdNumber == note.ThingId) notes.RemoveAt(i); // 稳定源批次重放，绝不累加产量。
                    continue;
                }
                var generatedBatch = new ProductBatch(id, contract.ContractId, obligation.EvidenceId, pawnKey,
                    note.DefName, contract.InstitutionKey, note.StackCount, 0, 0, note.Tick, note.ThingId, note.HolderKey);
                if (LaborFlow.WriteBatch(generatedBatch)) notes.RemoveAt(i);
            }
            if (notes.Count == 0) generatedOutputs.Remove(pawnKey);
        }
        private static int CountCarried(Pawn pawn, string thingDefName)
        {
            if (pawn == null || string.IsNullOrEmpty(thingDefName)) return 0;
            int count = 0;
            var carried = pawn.carryTracker?.CarriedThing;
            if (carried?.def != null && string.Equals(carried.def.defName, thingDefName, StringComparison.Ordinal))
                count += carried.stackCount;
            if (pawn.inventory?.innerContainer != null)
                foreach (var item in pawn.inventory.innerContainer)
                    if (item?.def != null && string.Equals(item.def.defName, thingDefName, StringComparison.Ordinal))
                        count += item.stackCount;
            return count;
        }

        /// <summary>周期维护：清掉过期产物笔记（有界：单轮最多扫 64 个 Pawn 槽）。</summary>
        internal void TrimGeneratedOutputs(int now)
        {
            if (generatedOutputs.Count == 0) return;
            var stale = new List<string>(4);
            int scanned = 0;
            foreach (var pair in generatedOutputs)
            {
                if (scanned++ >= 64) break;
                var notes = pair.Value;
                if (notes == null || notes.Count == 0) { stale.Add(pair.Key); continue; }
                for (int i = notes.Count - 1; i >= 0; i--)
                {
                    var note = notes[i];
                    if (note == null || now - note.Tick > OutputNoteLifetimeTicks) notes.RemoveAt(i);
                }
                if (notes.Count == 0) stale.Add(pair.Key);
            }
            for (int i = 0; i < stale.Count; i++) generatedOutputs.Remove(stale[i]);
        }
    }
}


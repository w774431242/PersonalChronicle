using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Domain.PawnEconomy.Finance;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed class EconomyStorageExtension : DefModExtension { public bool privateStorage; }
    public sealed class Building_EconomyStorage : Building_Storage
    {
        public Pawn Owner;
        public long PricePerNutritionMinor;
        public bool Private => def.GetModExtension<EconomyStorageExtension>()?.privateStorage == true;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref Owner, "economyStorageOwner");
            Scribe_Values.Look(ref PricePerNutritionMinor, "foodPricePerNutritionMinor", 0L);
        }
        public override void Notify_ReceivedThing(Thing newItem)
        {
            base.Notify_ReceivedThing(newItem);
            GameComponent_EconomyStorage.Current?.Receive(this, newItem);
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad) GameComponent_EconomyStorage.Current?.Register(this);
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos()) yield return gizmo;
            if (Private)
                yield return new Command_Action
                {
                    defaultLabel = "PersonalChronicle.Storage.AssignOwner".Translate(),
                    defaultDesc = "PersonalChronicle.Storage.OwnerHelp".Translate(),
                    icon = TexCommand.ForbidOff,
                    action = () =>
                    {
                        if (GetSlotGroup().HeldThings.Any())
                        { Messages.Message("PersonalChronicle.Storage.EmptyFirst".Translate(), MessageTypeDefOf.RejectInput, false); return; }
                        var options = new List<FloatMenuOption>
                            { new FloatMenuOption("PersonalChronicle.Storage.Unassigned".Translate(), () => Owner = null) };
                        foreach (Pawn pawn in Map.mapPawns.FreeColonistsSpawned)
                        {
                            Pawn selected = pawn;
                            if (pawn.RaceProps.Humanlike && pawn.IsFreeNonSlaveColonist)
                                options.Add(new FloatMenuOption(pawn.LabelShortCap, () => Owner = selected));
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                };
            else
            {
                // CHG-20261012-ECON-CONTRACTIZE（用户裁决「公有化后不需要主动设置售价，直接采用原版物品
                // 市场价格」）：**「设定食品售价」入口已移除** —— 定价由食物自身的 MarketValue 派生
                //（EconomyStorage.QuotedMealPrice / LowestFoodQuote）。保留入口就是"改了没效果的按钮"（F4）。
            }
        }
        public override string GetInspectString() => base.GetInspectString() + "\n" + (Private
            ? StorageLabel("OwnerValue", Owner?.LabelShortCap.ToString() ?? "PersonalChronicle.Storage.Unassigned".Translate().ToString())
            : "PersonalChronicle.Storage.PublicMarketPriced".Translate().ToString());
        private static string StorageLabel(string key, params object[] args)
            => EconomyLabels.Formatted("PersonalChronicle.Storage." + key, key, args);
    }
}

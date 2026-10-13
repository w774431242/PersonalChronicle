using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy
    {
        private Job PlanPreservedAssetUse(Pawn pawn)
        {
            if (!State.Ready || !SafeDecision(pawn) || pawn.inventory == null) return null;
            string owner = pawn.GetUniqueLoadID();
            var person = State.All<PersonalChronicle.Domain.PawnEconomy.EconomyPerson>().FirstOrDefault(x => x.PawnKey == owner);
            if (person?.Autonomy == false) return null;
            var rights = State.All<PersonalChronicle.Domain.PawnEconomy.EconomyOwnership>().Where(x => x.OwnerKey == owner).Take(8192).Select(x => x.AssetKey).ToArray();
            if (rights.Length == 0) return null;
            var item = pawn.inventory.innerContainer.OfType<Apparel>()
                .Concat(pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel).OfType<Apparel>())
                .Where(x => rights.Contains(x.GetUniqueLoadID()) && Wearable(pawn, x)
                    && (x.GetUniqueLoadID() == person?.AcquiredAsset || Gain(pawn, x) >= .05f)
                    && (!x.Spawned || pawn.CanReserveAndReach(x, PathEndMode.Touch, Danger.None)))
                .Take(128).OrderBy(x => x.GetUniqueLoadID() == person?.AcquiredAsset ? 0 : 1).ThenBy(x => x.thingIDNumber).FirstOrDefault();
            return item == null ? null : MakeJob("PC_PEA_WearOwned", item);
        }
    }
}

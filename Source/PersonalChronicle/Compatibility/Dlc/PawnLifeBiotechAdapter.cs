using System.Collections.Generic;
using PersonalChronicle.Domain.PawnEconomy.Perception;
using PersonalChronicle.Economy.PawnEconomy.Perception;
using RimWorld;
using Verse;

namespace PersonalChronicle.Compatibility.Dlc
{
    internal sealed class PawnLifeBiotechAdapter : IVanillaLifeExtension
    {
        public void RegisterNeeds(VanillaNeedRegistry registry)
        {
            if (!ModsConfig.BiotechActive) return;
            registry.Register("Play", typeof(Need_Play), "play", "NeedLevel", n => ((Need_Play)n).CurCategory.ToString());
            registry.Register("Learning", typeof(Need_Learning), "learning", "NeedLevel", n => ((Need_Learning)n).CurCategory.ToString());
        }
        public Fact<bool> VisiblePregnancy(IReadOnlyList<Hediff> visible, bool complete)
        {
            if (!ModsConfig.BiotechActive) return Fact<bool>.NotApplicable("BiotechInactive");
            foreach (var hediff in visible) if (hediff is Hediff_Pregnant) return Fact<bool>.Known(true);
            return complete ? Fact<bool>.Known(false) : Fact<bool>.Unknown("VisibleHealthIncomplete");
        }
    }
}

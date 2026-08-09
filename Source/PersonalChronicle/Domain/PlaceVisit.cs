using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// One stay at a place (map biome or caravan tile). Append-only place
    /// history for the overview footprint / social-adjacent biography.
    /// LeaveTick == -1 means the visit is still open.
    /// </summary>
    public class PlaceVisit : IExposable
    {
        /// <summary>Map biome defName, or "tile:{id}" for caravan.</summary>
        public string PlaceKey;

        /// <summary>"Map" or "Caravan" (language-independent discriminator).</summary>
        public string PlaceKind;

        public long EnterTick = -1L;
        public long LeaveTick = -1L;

        public PlaceVisit()
        {
        }

        public PlaceVisit(string placeKey, string placeKind, long enterTick)
        {
            PlaceKey = placeKey;
            PlaceKind = placeKind;
            EnterTick = enterTick;
            LeaveTick = -1L;
        }

        public bool IsOpen
        {
            get { return LeaveTick < 0L; }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref PlaceKey, "placeKey");
            Scribe_Values.Look(ref PlaceKind, "placeKind");
            Scribe_Values.Look(ref EnterTick, "enterTick", -1L);
            Scribe_Values.Look(ref LeaveTick, "leaveTick", -1L);
        }
    }
}

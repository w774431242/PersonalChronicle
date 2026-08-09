using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Archived location. <see cref="MapId"/> is the stable map identifier;
    /// <see cref="CellLabel"/> is a language-independent label snapshot (raw
    /// identifier or translation key, resolved at render time).
    /// </summary>
    public sealed class LocationObject : ArchiveObject
    {
        public string MapId;

        /// <summary>Cell/name snapshot (raw identifier or translation key).</summary>
        public string CellLabel;

        /// <summary>World map tile (-1 = none).</summary>
        public int WorldTile = -1;

        /// <summary>Map biome/defName snapshot at archive time.</summary>
        public string MapDefName;

        public override string CategoryKey
        {
            get { return ArchiveCategoryKeys.Location; }
        }

        public LocationObject()
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref MapId, "mapId");
            Scribe_Values.Look(ref CellLabel, "cellLabel");
            Scribe_Values.Look(ref WorldTile, "worldTile", -1);
            Scribe_Values.Look(ref MapDefName, "mapDefName");
        }
    }
}

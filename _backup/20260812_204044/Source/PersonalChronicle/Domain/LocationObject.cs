using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Archived location. <see cref="MapId"/> is the stable map identifier;
    /// <see cref="CellLabel"/> is a language-independent label snapshot (raw
    /// identifier or translation key, resolved at render time).
    ///
    /// v4.13 location atlas (five layers): identity (L1), ownership (L2),
    /// geography (L3), lifecycle (L4), statistics (L5, derived from the event
    /// stream — no persisted fields). Commerce (tradable city + main sell types)
    /// is a pure Def-derived snapshot taken when the map is first established —
    /// the archive never records trade history (that is a P2 optional path).
    ///
    /// Persistence contract: every added field MUST have a Scribe default equal
    /// to the "absent" value so old saves (pre-v4.13) migrate with zero code —
    /// Save Schema version is NOT bumped for this expansion.
    /// </summary>
    public sealed class LocationObject : ArchiveObject
    {
        // ---- L1 identity ----

        public string MapId;

        /// <summary>Cell/name snapshot (raw identifier or translation key).</summary>
        public string CellLabel;

        /// <summary>World map tile (-1 = none).</summary>
        public int WorldTile = -1;

        /// <summary>Map biome/defName snapshot at archive time.</summary>
        public string MapDefName;

        /// <summary>v4.13: MapParent (WorldObjectDef) defName — discriminates settlement / quest site / etc.</summary>
        public string WorldObjectDefName;

        /// <summary>v4.13: MapParent def.mapGenerator defName — how the map was generated (Base_Player / quest-specific / ...).</summary>
        public string MapGeneratorDefName;

        /// <summary>v4.13: map size snapshot "WxH" (e.g. "250x250"). Null/empty when unknown.</summary>
        public string MapSize;

        // ---- L2 ownership ----

        /// <summary>v4.13: owning faction defName (null/empty = no man's land).</summary>
        public string FactionDefName;

        /// <summary>v4.13: true when the owning faction is the player.</summary>
        public bool IsPlayerHome;

        // ---- L3 geography (world tile endowment) ----

        /// <summary>v4.13: Tile.hilliness enum snapshot ("Flat"/"Hilly"/"Mountainous"/"Impassable"). Null = unknown.</summary>
        public string Hilliness;

        /// <summary>v4.13: Tile.altitude snapshot (-1 = unknown).</summary>
        public float Altitude = -1f;

        /// <summary>v4.13: WorldGrid.IsCoastal(tile).</summary>
        public bool IsCoastal;

        /// <summary>v4.13: Tile.pollution snapshot (-1 = unknown).</summary>
        public float Pollution = -1f;

        /// <summary>v4.13: GenTemperature.GetAvgAnnualTemperature(tile) in °C (annual, not instant). float.NaN = unknown.</summary>
        public float AvgTempC = float.NaN;

        /// <summary>v4.13: Tile.snowCovered snapshot.</summary>
        public bool SnowCovered;

        // ---- L4 lifecycle ----

        /// <summary>v4.13: tick the map was first established / archived (-1 = unknown).</summary>
        public long EstablishedTick = -1L;

        /// <summary>v4.13: tick the map was deinited (-1 = still active).</summary>
        public long DeinitTick = -1L;

        /// <summary>v4.13: how the map ended ("Destroyed"/"Abandoned"/null = active/unknown).</summary>
        public string DeinitReason;

        // ---- Commerce (pure Def-derived snapshot, no trade history) ----

        /// <summary>v4.13: tradable = owning faction has a non-empty baseTraderKinds.</summary>
        public bool CanTrade;

        /// <summary>v4.13: snapshot of the trader kind defName (data key, never translated).</summary>
        public string TraderKindDefName;

        /// <summary>v4.13: permitRequiredForTrading defName (null/empty = no permit needed).</summary>
        public string PermitRequiredDefName;

        /// <summary>
        /// v4.13: main sell categories normalized from TraderKindDef.stockGenerators
        /// (StockGenerator_Category.categoryDef / StockGenerator_Tag.tradeTag /
        /// StockGenerator_SingleDef.thingDef) into the 8 canonical keys:
        /// "res"/"cloth"/"food"/"drug"/"weapon"/"armor"/"implant"/"tech".
        /// Data keys only; UI resolves labels via TradeCategoryView.
        /// </summary>
        public List<string> TradeKindKeys = new List<string>();

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
            // v4.13 expansion — every field has an "absent" default so old saves
            // migrate with zero code (Save Schema NOT bumped).
            Scribe_Values.Look(ref WorldObjectDefName, "worldObjectDefName");
            Scribe_Values.Look(ref MapGeneratorDefName, "mapGeneratorDefName");
            Scribe_Values.Look(ref MapSize, "mapSize");
            Scribe_Values.Look(ref FactionDefName, "factionDefName");
            Scribe_Values.Look(ref IsPlayerHome, "isPlayerHome", false);
            Scribe_Values.Look(ref Hilliness, "hilliness");
            Scribe_Values.Look(ref Altitude, "altitude", -1f);
            Scribe_Values.Look(ref IsCoastal, "isCoastal", false);
            Scribe_Values.Look(ref Pollution, "pollution", -1f);
            Scribe_Values.Look(ref AvgTempC, "avgTempC", float.NaN);
            Scribe_Values.Look(ref SnowCovered, "snowCovered", false);
            Scribe_Values.Look(ref EstablishedTick, "establishedTick", -1L);
            Scribe_Values.Look(ref DeinitTick, "deinitTick", -1L);
            Scribe_Values.Look(ref DeinitReason, "deinitReason");
            Scribe_Values.Look(ref CanTrade, "canTrade", false);
            Scribe_Values.Look(ref TraderKindDefName, "traderKindDefName");
            Scribe_Values.Look(ref PermitRequiredDefName, "permitRequiredDefName");
            Scribe_Collections.Look(ref TradeKindKeys, "tradeKindKeys", LookMode.Value);
            if (TradeKindKeys == null) TradeKindKeys = new List<string>();
        }
    }
}

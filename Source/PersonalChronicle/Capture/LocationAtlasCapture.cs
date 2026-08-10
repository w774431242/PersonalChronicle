using System;
using System.Collections.Generic;
using HarmonyLib;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// v4.13 location atlas capture: establishes and closes LocationObjects for
    /// maps and world objects (settlements / quest sites) without polling and
    /// without a single Harmony patch.
    ///
    /// Alignment with the archive positioning: this is a read-only chronicle —
    /// we only snapshot a place's identity (L1), ownership (L2), geography (L3)
    /// and lifecycle (L4) at the moment the map/world object is first observed,
    /// plus the pure Def-derived commerce snapshot (tradable city + main sell
    /// types). No trade history is ever recorded (that is a P2 optional path).
    ///
    /// Capture surface: called from ChronicleGameComponent.GameComponentTick's
    /// existing reconcile window (every ReconcileIntervalTicks) — same cadence as
    /// colonist reconcile, so no extra polling and no hot-path work.
    ///
    /// Idempotency: AddObject discards duplicates by StableId, and EstablishedTick
    /// is only ever written once. Deinit detection runs on archived locations that
    /// are no longer present in Find.Maps / Find.WorldObjects — best effort, the
    /// exact tick is the reconcile pass's tick (acceptable: the archive is not a
    /// real-time recorder).
    ///
    /// Engine API reality (1.6, verified by reflection, NOT guessed):
    ///   Map.TileInfo (Tile struct) — .PrimaryBiome / .elevation / .hilliness /
    ///     .pollution / .WaterCovered / .IsCoastal / .MaxTemperature / .MinTemperature
    ///   Map.Tile (PlanetTile) — .tileId / .Tile (Tile struct)
    ///   Map.Parent (MapParent) — .MapGeneratorDef / .def / .Tile
    ///   Map.ParentFaction (Faction) — direct property, no MapParent.ParentFaction
    ///   Settlement.TraderKind (TraderKindDef) — direct, plus .CanTradeNow
    ///   FactionDef.baseTraderKinds / caravanTraderKinds / visitorTraderKinds / orbitalTraderKinds
    ///   TraderKindDef.stockGenerators / .permitRequiredForTrading (RoyalTitlePermitDef)
    ///   StockGenerator_Category.categoryDef / StockGenerator_Tag.tradeTag / StockGenerator_SingleDef.thingDef
    /// </summary>
    public static class LocationAtlasCapture
    {
        /// <summary>
        /// One reconcile pass: archive newly observed maps/world objects and close
        /// archived locations that are no longer alive. Must be called from the
        /// game component's throttle window; never from a per-frame path.
        /// </summary>
        public static void Reconcile(ChronicleGameComponent component, long gameTick)
        {
            if (component == null || gameTick < 0L)
            {
                return;
            }
            try
            {
                // Source 1: live maps (player maps + any generated map).
                // Also collect the set of tiles that already have a live map, so
                // Source 2 below does not double-archive a settlement whose map is
                // currently generated (a settlement can appear in both sources).
                HashSet<int> liveMapTiles = new HashSet<int>();
                List<Map> maps = Find.Maps;
                if (maps != null)
                {
                    for (int i = 0; i < maps.Count; i++)
                    {
                        Map map = maps[i];
                        if (map == null)
                        {
                            continue;
                        }
                        EnsureMapArchived(component, map, gameTick);
                        if (map.Parent != null)
                        {
                            liveMapTiles.Add(map.Parent.Tile.tileId);
                        }
                        else
                        {
                            liveMapTiles.Add(map.Tile.tileId);
                        }
                    }
                }

                // Source 2: world objects (settlements / quest sites / other
                // map parents) that may not have a generated Map yet. Settlements
                // are faction cities; MapParent covers quest sites & temporary maps.
                // Skip any world object whose tile already has a live map — that
                // place was archived via Source 1 (avoids Map_/World_ duplicates).
                if (Find.World != null && Find.WorldObjects != null)
                {
                    List<WorldObject> worldObjects = Find.WorldObjects.AllWorldObjects;
                    if (worldObjects != null)
                    {
                        for (int i = 0; i < worldObjects.Count; i++)
                        {
                            WorldObject wo = worldObjects[i];
                            if (wo == null || wo.Destroyed)
                            {
                                continue;
                            }
                            if (wo is Settlement || wo is MapParent)
                            {
                                if (liveMapTiles.Contains(wo.Tile.tileId))
                                {
                                    continue; // already archived via Source 1
                                }
                                EnsureWorldObjectArchived(component, wo, gameTick);
                            }
                        }
                    }
                }

                CloseMissingLocations(component, gameTick);
            }
            catch (Exception ex)
            {
                Log.Warning("PersonalChronicle: location atlas reconcile failed: " + ex.Message);
            }
        }

        /// <summary>Archives a live map if not already archived. MapId = Map.uniqueID string.</summary>
        private static void EnsureMapArchived(ChronicleGameComponent component, Map map, long gameTick)
        {
            if (map == null)
            {
                return;
            }
            string mapId = MapStableId(map);
            if (string.IsNullOrEmpty(mapId))
            {
                return;
            }
            if (component.GetObject(mapId) != null)
            {
                return; // already archived
            }

            LocationObject loc = BuildFromMap(map, gameTick);
            if (loc != null)
            {
                component.AddObject(loc);
            }
        }

        /// <summary>
        /// Archives a world object (settlement / quest site) that has no live map
        /// yet. StableId = "World_" + defName + "_" + tile (world objects have no
        /// unique load id; defName+tile is stable enough and never collides with
        /// map ids).
        /// </summary>
        private static void EnsureWorldObjectArchived(ChronicleGameComponent component, WorldObject wo, long gameTick)
        {
            if (wo == null || wo.def == null)
            {
                return;
            }
            string stableId = WorldObjectStableId(wo);
            if (string.IsNullOrEmpty(stableId))
            {
                return;
            }
            if (component.GetObject(stableId) != null)
            {
                return; // already archived
            }

            LocationObject loc = BuildFromWorldObject(wo, gameTick);
            if (loc != null)
            {
                component.AddObject(loc);
            }
        }

        /// <summary>
        /// Closes archived locations whose map/world object is no longer alive.
        /// Best effort: DeinitTick = current reconcile tick; DeinitReason =
        /// "Destroyed" when a destroyed world object exists at the tile, else
        /// "Abandoned".
        /// </summary>
        private static void CloseMissingLocations(ChronicleGameComponent component, long gameTick)
        {
            IReadOnlyList<ArchiveObject> locations = component.GetObjectsOfCategory(ArchiveCategoryKeys.Location);
            if (locations == null || locations.Count == 0)
            {
                return;
            }

            // Alive map ids + alive world object ids this pass. Destroyed world
            // objects are tracked separately so a ruined settlement can be
            // closed with DeinitReason="Destroyed" instead of "Abandoned".
            HashSet<string> alive = new HashSet<string>();
            HashSet<string> destroyedThisPass = new HashSet<string>();
            List<Map> maps = Find.Maps;
            if (maps != null)
            {
                for (int i = 0; i < maps.Count; i++)
                {
                    if (maps[i] != null)
                    {
                        alive.Add(MapStableId(maps[i]));
                    }
                }
            }
            if (Find.World != null && Find.WorldObjects != null)
            {
                List<WorldObject> worldObjects = Find.WorldObjects.AllWorldObjects;
                if (worldObjects != null)
                {
                    for (int i = 0; i < worldObjects.Count; i++)
                    {
                        WorldObject wo = worldObjects[i];
                        if (wo == null || wo.def == null)
                        {
                            continue;
                        }
                        if (wo.Destroyed)
                        {
                            destroyedThisPass.Add(WorldObjectStableId(wo));
                        }
                        else
                        {
                            alive.Add(WorldObjectStableId(wo));
                        }
                    }
                }
            }

            for (int i = 0; i < locations.Count; i++)
            {
                LocationObject loc = locations[i] as LocationObject;
                if (loc == null || loc.DeinitTick != -1L)
                {
                    continue; // closed already / not a location
                }
                if (!alive.Contains(loc.StableId))
                {
                    loc.DeinitTick = gameTick;
                    loc.DeinitReason = destroyedThisPass.Contains(loc.StableId)
                        ? "Destroyed" : "Abandoned";
                    component.MarkChanged();
                }
            }
        }

        /// <summary>Builds a location snapshot from a live map (L1-L4 + commerce).</summary>
        private static LocationObject BuildFromMap(Map map, long gameTick)
        {
            if (map == null)
            {
                return null;
            }
            try
            {
                LocationObject loc = new LocationObject
                {
                    StableId = MapStableId(map),
                    MapId = MapStableId(map),
                    EstablishedTick = gameTick,
                    DeinitTick = -1L
                };

                // L1 identity.
                if (map.Biome != null)
                {
                    loc.MapDefName = map.Biome.defName;
                    loc.CellLabel = map.Biome.defName;
                }
                loc.LabelSnapshot = loc.CellLabel ?? loc.StableId;
                if (map.Parent != null)
                {
                    if (map.Parent.def != null)
                    {
                        loc.WorldObjectDefName = map.Parent.def.defName;
                    }
                    loc.MapGeneratorDefName = map.Parent.MapGeneratorDef != null
                        ? map.Parent.MapGeneratorDef.defName : null;
                    loc.MapSize = map.Size.x + "x" + map.Size.z;
                    // MapParent.Tile is a PlanetTile; use its tileId.
                    int parentTile = map.Parent.Tile.tileId;
                    loc.WorldTile = parentTile >= 0 ? parentTile : map.Tile.tileId;
                }
                else
                {
                    loc.WorldTile = map.Tile.tileId;
                }

                // L2 ownership.
                Faction parentFaction = map.ParentFaction;
                if (parentFaction != null)
                {
                    loc.FactionDefName = parentFaction.def.defName;
                    loc.IsPlayerHome = parentFaction.IsPlayer;
                }

                // L3 geography (Map.TileInfo Tile struct — the authoritative 1.6 source).
                ApplyTileSnapshot(loc, map.TileInfo);

                // Commerce (pure Def snapshot, no trade history).
                ApplyCommerceSnapshot(loc, parentFaction);

                return loc;
            }
            catch (Exception ex)
            {
                Log.Warning("PersonalChronicle: location atlas map snapshot failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Builds a location snapshot from a world object (settlement / quest site).</summary>
        private static LocationObject BuildFromWorldObject(WorldObject wo, long gameTick)
        {
            if (wo == null || wo.def == null)
            {
                return null;
            }
            try
            {
                LocationObject loc = new LocationObject
                {
                    StableId = WorldObjectStableId(wo),
                    MapId = WorldObjectStableId(wo),
                    WorldObjectDefName = wo.def.defName,
                    WorldTile = wo.Tile.tileId,
                    EstablishedTick = gameTick,
                    DeinitTick = -1L,
                    LabelSnapshot = wo.def.label ?? wo.def.defName,
                    CellLabel = wo.def.defName
                };

                // L2 ownership.
                Faction faction = wo.Faction;
                if (faction != null)
                {
                    loc.FactionDefName = faction.def.defName;
                    loc.IsPlayerHome = faction.IsPlayer;
                }

                // L3 geography (world tile endowment) via the tile's Tile struct.
                // WorldGrid indexer takes a PlanetTile; int implicitly converts.
                if (Find.World != null && loc.WorldTile >= 0)
                {
                    ApplyTileSnapshot(loc, Find.WorldGrid[loc.WorldTile]);
                }

                // Commerce: Settlement exposes TraderKind directly; otherwise use
                // the faction's baseTraderKinds.
                if (wo is Settlement settlement && settlement.TraderKind != null)
                {
                    ApplyTraderKindSnapshot(loc, settlement.TraderKind);
                }
                else
                {
                    ApplyCommerceSnapshot(loc, faction);
                }

                return loc;
            }
            catch (Exception ex)
            {
                Log.Warning("PersonalChronicle: location atlas world-object snapshot failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Copies the world-tile endowment into the location snapshot.</summary>
        private static void ApplyTileSnapshot(LocationObject loc, Tile tile)
        {
            if (loc == null)
            {
                return;
            }
            try
            {
                if (tile.PrimaryBiome != null)
                {
                    loc.MapDefName = tile.PrimaryBiome.defName;
                }
                loc.Hilliness = tile.hilliness.ToString();
                loc.Altitude = tile.elevation;
                loc.Pollution = tile.pollution;
                loc.IsCoastal = tile.IsCoastal;
                // Annual temperature approximation: mean of tile min/max.
                float min = tile.MinTemperature;
                float max = tile.MaxTemperature;
                if (!float.IsNaN(min) && !float.IsNaN(max))
                {
                    loc.AvgTempC = (min + max) / 2f;
                }
                else
                {
                    loc.AvgTempC = tile.temperature;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("PersonalChronicle: tile snapshot failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Commerce snapshot: tradable = faction has a non-empty baseTraderKinds
        /// (or settlement exposes a TraderKind). Pure Def read — never a
        /// trade-history record.
        /// </summary>
        private static void ApplyCommerceSnapshot(LocationObject loc, Faction faction)
        {
            if (loc == null)
            {
                return;
            }
            if (faction == null || faction.def == null)
            {
                return; // no man's land: never tradable
            }
            List<TraderKindDef> traderKinds = faction.def.baseTraderKinds;
            if (traderKinds == null || traderKinds.Count == 0)
            {
                loc.CanTrade = false;
                return;
            }
            loc.CanTrade = true;
            ApplyTraderKindSnapshot(loc, traderKinds[0]);
        }

        /// <summary>Applies a trader kind snapshot (trader def + permit + sell categories).</summary>
        private static void ApplyTraderKindSnapshot(LocationObject loc, TraderKindDef kind)
        {
            if (loc == null || kind == null)
            {
                return;
            }
            loc.CanTrade = true;
            loc.TraderKindDefName = kind.defName;
            if (kind.permitRequiredForTrading != null)
            {
                loc.PermitRequiredDefName = kind.permitRequiredForTrading.defName;
            }

            // Normalize main sell categories from stockGenerators into the 8
            // canonical keys (deduplicated, capped at 4).
            if (loc.TradeKindKeys == null)
            {
                loc.TradeKindKeys = new List<string>();
            }
            if (kind.stockGenerators != null)
            {
                foreach (StockGenerator generator in kind.stockGenerators)
                {
                    if (loc.TradeKindKeys.Count >= 4)
                    {
                        break;
                    }
                    string key = NormalizeSellCategory(generator);
                    if (key != null && !loc.TradeKindKeys.Contains(key))
                    {
                        loc.TradeKindKeys.Add(key);
                    }
                }
            }
        }

        /// <summary>
        /// Maps a StockGenerator to one of the 8 canonical sell-category keys:
        /// "res" (resources) / "cloth" / "food" / "drug" / "weapon" / "armor" /
        /// "implant" / "tech". Falls back to null when no canonical category
        /// applies (data-key only, never translated here).
        /// </summary>
        private static string NormalizeSellCategory(StockGenerator generator)
        {
            if (generator == null)
            {
                return null;
            }
            // StockGenerator_Tag.tradeTag is public in 1.6.
            if (generator is StockGenerator_Tag tag && !string.IsNullOrEmpty(tag.tradeTag))
            {
                string key = TagToKey(tag.tradeTag);
                if (key != null)
                {
                    return key;
                }
            }
            // StockGenerator_Category.categoryDef and StockGenerator_SingleDef.thingDef
            // are internal in 1.6 — read via Harmony Traverse (reflection), which
            // keeps the compile contract clean and degrades gracefully on drift.
            if (generator is StockGenerator_Category)
            {
                ThingCategoryDef categoryDef = ReadCategoryField(generator);
                if (categoryDef != null)
                {
                    string key = CategoryToKey(categoryDef.defName);
                    if (key != null)
                    {
                        return key;
                    }
                }
            }
            if (generator is StockGenerator_SingleDef)
            {
                ThingDef thingDef = ReadThingDefField(generator);
                if (thingDef != null)
                {
                    // Single-item generator: classify by thing category ancestry.
                    ThingCategoryDef cat = thingDef.thingCategories != null
                        && thingDef.thingCategories.Count > 0 ? thingDef.thingCategories[0] : null;
                    if (cat != null)
                    {
                        string key = CategoryToKey(cat.defName);
                        if (key != null)
                        {
                            return key;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>Reads the internal categoryDef field via Harmony Traverse (safe, no throw).</summary>
        private static ThingCategoryDef ReadCategoryField(StockGenerator generator)
        {
            try
            {
                return Traverse.Create(generator).Field("categoryDef").GetValue<ThingCategoryDef>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Reads the internal thingDef field via Harmony Traverse (safe, no throw).</summary>
        private static ThingDef ReadThingDefField(StockGenerator generator)
        {
            try
            {
                return Traverse.Create(generator).Field("thingDef").GetValue<ThingDef>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string CategoryToKey(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }
            if (defName == "ResourcesRaw") return "res";
            if (defName == "Textiles") return "cloth";
            if (defName == "FoodRaw" || defName == "FoodMeals") return "food";
            if (defName == "Drugs" || defName == "MedicalItems") return "drug";
            if (defName.IndexOf("Weapon", StringComparison.Ordinal) >= 0
                || defName.IndexOf("Melee", StringComparison.Ordinal) >= 0
                || defName.IndexOf("Ranged", StringComparison.Ordinal) >= 0
                || defName.IndexOf("Gun", StringComparison.Ordinal) >= 0) return "weapon";
            if (defName.IndexOf("Armor", StringComparison.Ordinal) >= 0
                || defName.IndexOf("Apparel", StringComparison.Ordinal) >= 0
                || defName.IndexOf("Clothes", StringComparison.Ordinal) >= 0) return "armor";
            if (defName.IndexOf("Implant", StringComparison.Ordinal) >= 0) return "implant";
            if (defName.IndexOf("Tech", StringComparison.Ordinal) >= 0
                || defName.IndexOf("Neurotrainer", StringComparison.Ordinal) >= 0
                || defName.IndexOf("Artifact", StringComparison.Ordinal) >= 0
                || defName.IndexOf("Book", StringComparison.Ordinal) >= 0) return "tech";
            return null;
        }

        private static string TagToKey(string tradeTag)
        {
            if (string.IsNullOrEmpty(tradeTag))
            {
                return null;
            }
            if (tradeTag.IndexOf("Melee", StringComparison.Ordinal) >= 0
                || tradeTag.IndexOf("Weapon", StringComparison.Ordinal) >= 0
                || tradeTag.IndexOf("Gun", StringComparison.Ordinal) >= 0) return "weapon";
            if (tradeTag.IndexOf("Armor", StringComparison.Ordinal) >= 0
                || tradeTag.IndexOf("Apparel", StringComparison.Ordinal) >= 0) return "armor";
            if (tradeTag.IndexOf("Implant", StringComparison.Ordinal) >= 0) return "implant";
            if (tradeTag.IndexOf("Tech", StringComparison.Ordinal) >= 0
                || tradeTag.IndexOf("Neurotrainer", StringComparison.Ordinal) >= 0
                || tradeTag.IndexOf("Artifact", StringComparison.Ordinal) >= 0
                || tradeTag.IndexOf("Serum", StringComparison.Ordinal) >= 0) return "tech";
            if (tradeTag.IndexOf("Drug", StringComparison.Ordinal) >= 0) return "drug";
            if (tradeTag.IndexOf("Food", StringComparison.Ordinal) >= 0) return "food";
            if (tradeTag.IndexOf("Textile", StringComparison.Ordinal) >= 0
                || tradeTag.IndexOf("Cloth", StringComparison.Ordinal) >= 0) return "cloth";
            if (tradeTag.IndexOf("Resource", StringComparison.Ordinal) >= 0
                || tradeTag.IndexOf("Raw", StringComparison.Ordinal) >= 0) return "res";
            return null;
        }

        /// <summary>Stable id for a live map: "Map_" + Map.uniqueID.</summary>
        private static string MapStableId(Map map)
        {
            if (map == null)
            {
                return null;
            }
            return "Map_" + map.uniqueID;
        }

        /// <summary>Stable id for a world object: "World_" + defName + "_" + tileId.</summary>
        private static string WorldObjectStableId(WorldObject wo)
        {
            if (wo == null || wo.def == null)
            {
                return null;
            }
            return "World_" + wo.def.defName + "_" + wo.Tile.tileId;
        }
    }
}

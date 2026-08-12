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
                // v4.14 严格触发重构：地点档案只记录"殖民者亲自到访/建立/参与过"的
                // 地点，绝不扫描整个世界。
                //
                // Source 1: 殖民者当前所在的地图（地图即"到访"铁证——只有玩家
                //   进入过的地方才会生成 Map 实例）。玩家本家基地 / 任务地图 /
                //   殖民者在场的商队所抵达的 settlement 地图都会生成 Map，
                //   Find.Maps 天然就是"玩家到访过的地图集合"。
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

                // Source 2: 世界上的世界对象（settlement / quest site）——但绝不
                // 主动扫描 AllWorldObjects。只对"殖民者当前所在位置"的世界对象
                // 建档：
                //   - 商队中的殖民者：商队 tile 上若有 Settlement/MapParent（玩家
                //     已派遣商队抵达该地），才建档；敌对方/未接触 settlement 永远
                //     不建档。
                //   - 已生成 Map 的世界对象：由 Source 1 覆盖（liveMapTiles 去重）。
                ArchiveLocationsByColonists(component, gameTick, liveMapTiles);

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
        /// v4.14 严格触发：只对"殖民者当前所在位置"的世界对象建档。
        ///
        /// 遍历当前殖民地全部殖民者（地图 + 商队），对商队中的殖民者：
        ///   取商队 tile 上的世界对象（Settlement / MapParent / Caravan），
        ///   若该 tile 尚无 live map（未被 Source 1 覆盖）则建档。
        /// 地图上的殖民者已由 Source 1（Find.Maps）覆盖，这里不重复。
        ///
        /// 关键：绝不无条件扫描 Find.WorldObjects.AllWorldObjects 全量——只
        /// 在"玩家商队含殖民地人口且停在某 tile"时执行一次按 tile 匹配的遍历，
        /// 未到访的 settlement 永远不入档（商队抵达前不会有殖民者在那里）。
        ///
        /// 风险规避（v4.14）：
        /// ① 不再用 WorldObjectAt<WorldObject>（同 tile 多对象如 Settlement +
        ///    Caravan 并存时返回歧义，可能漏建档）——改为先收集商队 tile 集合，
        ///    再遍历 AllWorldObjects 按 tileId 精确匹配，覆盖同 tile 全部对象。
        /// ② 商队移动中（pather.MovingNow）不建档——跨 tile 移动只是短暂路过，
        ///    等到达稳定位置后由下轮 reconcile 建档，避免误录"途经地"。
        /// </summary>
        private static void ArchiveLocationsByColonists(
            ChronicleGameComponent component, long gameTick, HashSet<int> liveMapTiles)
        {
            if (component == null || Find.World == null || Find.WorldObjects == null)
            {
                return;
            }
            List<Caravan> caravans = Find.WorldObjects.Caravans;
            if (caravans == null || caravans.Count == 0)
            {
                return;
            }

            // 第一步：收集"殖民者所在且已停稳"的商队 tile 集合。
            HashSet<int> visitedTiles = new HashSet<int>();
            for (int i = 0; i < caravans.Count; i++)
            {
                Caravan caravan = caravans[i];
                if (caravan == null || caravan.Destroyed || caravan.PawnsListForReading == null)
                {
                    continue;
                }
                // 只处理玩家派系的商队（殖民地人口在其中的商队）。
                if (caravan.Faction == null || !caravan.Faction.IsPlayer)
                {
                    continue;
                }
                // 风险规避②：移动中的商队不建档（pather 可能为 null，防御）。
                if (caravan.pather != null && caravan.pather.MovingNow)
                {
                    continue;
                }
                // 商队中有当前殖民地人口吗？有才算"殖民者到访该地"。
                bool hasColonist = false;
                List<Pawn> caravanPawns = caravan.PawnsListForReading;
                for (int p = 0; p < caravanPawns.Count; p++)
                {
                    Pawn pawn = caravanPawns[p];
                    if (pawn != null && ChronicleColonistScanner.TryClassifyCurrent(pawn, out _))
                    {
                        hasColonist = true;
                        break;
                    }
                }
                if (!hasColonist)
                {
                    continue;
                }
                int tileId = caravan.Tile.tileId;
                if (tileId < 0 || liveMapTiles.Contains(tileId))
                {
                    continue; // 已有 live map（Source 1 覆盖）或非法 tile
                }
                visitedTiles.Add(tileId);
            }
            if (visitedTiles.Count == 0)
            {
                return;
            }

            // 第二步：按 visitedTiles 匹配世界对象（仅当玩家商队停靠时执行）。
            // 风险规避①：全量遍历仅作为"按 tile 过滤"的容器，不匹配的 tile
            // 一律跳过；同 tile 的 Settlement 与 MapParent 全部覆盖，无歧义。
            List<WorldObject> worldObjects = Find.WorldObjects.AllWorldObjects;
            if (worldObjects == null)
            {
                return;
            }
            for (int i = 0; i < worldObjects.Count; i++)
            {
                WorldObject wo = worldObjects[i];
                if (wo == null || wo.Destroyed || wo.def == null)
                {
                    continue;
                }
                if (!(wo is Settlement || wo is MapParent))
                {
                    continue; // 仅 settlement / quest site / 地图父节点
                }
                if (!visitedTiles.Contains(wo.Tile.tileId))
                {
                    continue; // 玩家商队未停靠的 tile 一律跳过
                }
                EnsureWorldObjectArchived(component, wo, gameTick);
            }
        }

        /// <summary>
        /// Closes archived locations whose map/world object is no longer alive.
        /// Best effort: DeinitTick = current reconcile tick; DeinitReason =
        /// "Destroyed" when a destroyed world object exists at the tile, else
        /// "Abandoned".
        ///
        /// v4.14 修复：额外清理 Source2 旧版误建的"敌对且无任何事件历史"
        /// settlement —— 这些是 v4.14 之前累积的世界全量 settlement 中
        /// 玩家实际未到访的部分，标 DeinitReason="Unvisited" 让其自然消亡
        /// （新逻辑 Source2 不再新建此类记录）。
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
                        ? PlaceVisitKeys.DeinitReasonDestroyed
                        : PlaceVisitKeys.DeinitReasonAbandoned;
                    component.MarkChanged();
                }
                else if (loc.StableId != null
                         && loc.StableId.StartsWith(PlaceVisitKeys.WorldIdPrefix, StringComparison.Ordinal)
                         && IsUnvisitedStaleLocation(component, loc))
                {
                    // v4.14 修复：旧版 Source2 误建的地点记录（玩家未到访的
                    // 世界 settlement / quest site）持续收敛。保留：玩家本家
                    // （IsPlayerHome）与有任何事件历史的地点。幂等：已关闭的
                    // 跳过。此逻辑与 SchemaVersion 迁移互补，保证新档首轮
                    // reconcile 后也立即收缩（无需等读档迁移）。
                    //
                    // 风险规避：只对 World_ 前缀（未到访世界对象）做无事件收敛。
                    // Map_ 前缀且仍在 alive（当前 live map，Source1 建档）的
                    // 记录不在此列——玩家可能刚进入任务地图、事件尚未发生，
                    // 若此刻因"暂无事件"关闭，随后战斗事件写入时 AddObject
                    // 会拒绝已关闭记录，导致事件引用悬空。live map 是"当前
                    // 到访"铁证，绝不因暂无事件而关闭。
                    loc.DeinitTick = gameTick;
                    loc.DeinitReason = PlaceVisitKeys.DeinitReasonUnvisited;
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

        // Centralized trade-category normalization. Source DefNames come from
        // vanilla StockGenerator_Category.categoryDef; the mapping below is the
        // single place that translates them into the 8 display keys used by the
        // location-atlas UI. New categories are added here, not via scattered
        // string comparisons across the capture layer.
        private static readonly Dictionary<string, string> CategoryDefNameToKey =
            new Dictionary<string, string>(System.StringComparer.Ordinal)
            {
                { "ResourcesRaw", "res" },
                { "Textiles", "cloth" },
                { "FoodRaw", "food" },
                { "FoodMeals", "food" },
                { "Drugs", "drug" },
                { "MedicalItems", "drug" },
            };

        // Keyword fragments for DefNames that have no fixed base name (e.g.
        // weapon/apparel families). Kept next to the explicit table above.
        private static readonly (string Fragment, string Key)[] CategoryDefNameFragments =
        {
            ("Weapon", "weapon"), ("Melee", "weapon"), ("Ranged", "weapon"), ("Gun", "weapon"),
            ("Armor", "armor"), ("Apparel", "armor"), ("Clothes", "armor"),
            ("Implant", "implant"),
            ("Tech", "tech"), ("Neurotrainer", "tech"), ("Artifact", "tech"), ("Book", "tech"),
        };

        private static string CategoryToKey(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return null;
            }
            if (CategoryDefNameToKey.TryGetValue(defName, out string directKey))
            {
                return directKey;
            }
            foreach (var (fragment, key) in CategoryDefNameFragments)
            {
                if (defName.IndexOf(fragment, StringComparison.Ordinal) >= 0)
                {
                    return key;
                }
            }
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
            return PlaceVisitKeys.WorldIdPrefix + wo.def.defName + "_" + wo.Tile.tileId;
        }

        /// <summary>
        /// v4.14 修复：判定一个 LocationObject 是否为"玩家未实际到访的过期地点"。
        ///
        /// 保留规则（与设计文档"地点 = 玩家亲自到访/建立/参与过"一致）：
        ///   - 玩家本家（IsPlayerHome == true）—— 自己的基地永远在册；
        ///   - 有任何编年史事件历史的地点 —— 玩家参与过（战斗/贸易/事件）。
        /// 其余一律视为过期（Unvisited）并关闭。
        ///
        /// 相比旧 IsUnvisitedEnemySettlement：不再依赖 WorldObjectDefName 含
        /// "Settlement"、FactionDefName 派系关系等脆弱条件——派系关系会漂移
        /// （和平条约/招安）、quest site 类名不含 Settlement、Map 类记录的
        /// WorldObjectDefName 是 map.Parent.def（不含 Settlement），这些都会
        /// 让旧逻辑漏判，导致 267 只清到 115。新逻辑以"是否玩家接触过"为唯一
        /// 依据，任何场景都稳健收敛。
        /// </summary>
        private static bool IsUnvisitedStaleLocation(
            ChronicleGameComponent component, LocationObject loc)
        {
            if (component == null || loc == null || string.IsNullOrEmpty(loc.StableId))
            {
                return false;
            }
            if (loc.IsPlayerHome)
            {
                return false; // 玩家本家保留
            }
            return component.GetEventsFor(loc.StableId).Count == 0;
        }
    }
}

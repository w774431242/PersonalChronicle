namespace PersonalChronicle.Domain
{
    /// <summary>
    /// Centralised business-identity string constants (BASE-010 / GOV-006:
    /// no magic business strings scattered across code).
    ///
    /// WARNING: these values are PERSISTED (PlaceVisit.PlaceKind /
    /// LocationObject.StableId / DeinitReason are written into saves) and are
    /// part of the external IPlaceProvider contract. Never change their string
    /// values — that would silently break old saves and third-party providers.
    /// </summary>
    public static class PlaceVisitKeys
    {
        /// <summary>PlaceKind for a map-biome stay (persisted value).</summary>
        public const string KindMap = "Map";

        /// <summary>PlaceKind for an off-map caravan stay (persisted value).</summary>
        public const string KindCaravan = "Caravan";

        /// <summary>Prefix of a world-object stable id: "World_" + defName + "_" + tileId.</summary>
        public const string WorldIdPrefix = "World_";

        /// <summary>Prefix of a caravan place key: "tile:" + tileId.</summary>
        public const string TileKeyPrefix = "tile:";

        /// <summary>DeinitReason value for unvisited stale locations (persisted value).</summary>
        public const string DeinitReasonUnvisited = "Unvisited";

        /// <summary>DeinitReason value for a location destroyed in-world (persisted value).</summary>
        public const string DeinitReasonDestroyed = "Destroyed";

        /// <summary>DeinitReason value for a location abandoned / no longer alive (persisted value).</summary>
        public const string DeinitReasonAbandoned = "Abandoned";
    }
}

using System.Collections.Generic;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// Where a live pawn currently is. Display-only view type — never persisted
    /// (Scribe has no involvement with these read models).
    /// </summary>
    public enum LocationKind
    {
        /// <summary>No resolvable location (pawn dead/absent/off-map).</summary>
        None,

        /// <summary>On a map (see <see cref="LocationInfo.MapDefName"/>).</summary>
        Map,

        /// <summary>Travelling in a world caravan (see <see cref="LocationInfo.WorldTile"/>).</summary>
        Caravan
    }

    /// <summary>
    /// Immutable display model for a pawn's live location.
    /// Map case: MapDefName is the biome/map defName snapshot. Caravan case:
    /// WorldTile is the world map tile. Never a live list exposed to callers.
    /// </summary>
    public sealed class LocationInfo
    {
        public readonly LocationKind Kind;
        public readonly string MapDefName;
        public readonly int WorldTile;

        public LocationInfo(LocationKind kind, string mapDefName, int worldTile)
        {
            Kind = kind;
            MapDefName = mapDefName;
            WorldTile = worldTile;
        }
    }

    /// <summary>
    /// Immutable display model for a single work-type priority entry, shared by
    /// the live read path (Pawn_WorkSettings) and the dead-pawn fallback path
    /// (PawnObject.WorkSnapshot). Legacy — UI v3.1 no longer consumes this.
    /// </summary>
    public sealed class WorkPriorityView
    {
        public readonly string WorkTypeDefName;
        public readonly int Priority;

        public WorkPriorityView(string workTypeDefName, int priority)
        {
            WorkTypeDefName = workTypeDefName;
            Priority = priority;
        }
    }

    /// <summary>
    /// v3.1 career work-time row (archive cumulative ticks per WorkTypeDef).
    /// </summary>
    public sealed class WorkTimeStatView
    {
        public readonly string WorkTypeDefName;
        public readonly long Ticks;
        public readonly long LastTick;
        public readonly float Share01;
        public readonly int Rank;

        public WorkTimeStatView(string workTypeDefName, long ticks, long lastTick, float share01, int rank)
        {
            WorkTypeDefName = workTypeDefName;
            Ticks = ticks;
            LastTick = lastTick;
            Share01 = share01;
            Rank = rank;
        }
    }

    /// <summary>
    /// v3.1 skill archive row: join snapshot vs death (or live "now") level.
    /// </summary>
    public sealed class SkillArchiveView
    {
        public readonly string SkillDefName;
        public readonly int JoinLevel;
        public readonly int EndLevel;
        public readonly int Delta;

        public SkillArchiveView(string skillDefName, int joinLevel, int endLevel)
        {
            SkillDefName = skillDefName;
            JoinLevel = joinLevel;
            EndLevel = endLevel;
            Delta = endLevel - joinLevel;
        }
    }

    /// <summary>Read-only career production summary for one pawn.</summary>
    public sealed class ProductionSummaryView
    {
        public readonly int TotalQuantity;
        public readonly float TotalMarketValue;
        public readonly long LastProductionTick;
        public readonly IReadOnlyList<ProductionTypeView> Types;

        public ProductionSummaryView(
            int totalQuantity,
            float totalMarketValue,
            long lastProductionTick,
            IReadOnlyList<ProductionTypeView> types)
        {
            TotalQuantity = totalQuantity;
            TotalMarketValue = totalMarketValue;
            LastProductionTick = lastProductionTick;
            Types = types ?? new List<ProductionTypeView>();
        }
    }

    public sealed class ProductionTypeView
    {
        public readonly string DefName;
        public readonly int Quantity;
        public readonly float MarketValue;

        public ProductionTypeView(string defName, int quantity, float marketValue)
        {
            DefName = defName;
            Quantity = quantity;
            MarketValue = marketValue;
        }
    }

    /// <summary>v1.1.4 损耗宫格：该人物消耗品计价只读视图。</summary>
    public sealed class ConsumptionSummaryView
    {
        public readonly float TotalSilver;   // 累计损耗（银）
        public readonly float WeeklySilver;  // 近 7 天（银）
        public readonly float DailySilver;   // 近 30 天日均（银/天）
        public readonly IReadOnlyList<ConsumptionTypeView> Types; // 类目构成，按银币降序

        public ConsumptionSummaryView(
            float totalSilver,
            float weeklySilver,
            float dailySilver,
            IReadOnlyList<ConsumptionTypeView> types)
        {
            TotalSilver = totalSilver;
            WeeklySilver = weeklySilver;
            DailySilver = dailySilver;
            Types = types ?? new List<ConsumptionTypeView>();
        }
    }

    /// <summary>v1.1.4 损耗构成：一个消耗品类目（ThingCategoryDef.defName → 累计银币）。</summary>
    public sealed class ConsumptionTypeView
    {
        public readonly string CategoryDefName;
        public readonly float Silver;

        public ConsumptionTypeView(string categoryDefName, float silver)
        {
            CategoryDefName = categoryDefName;
            Silver = silver;
        }
    }
}

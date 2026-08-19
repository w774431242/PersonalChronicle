using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Api;
using PersonalChronicle.Api.DomainProviders;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;
using UnityEngine;

namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// Default <see cref="IArchiveUiDataProvider"/> (P2-6). Centralizes every
    /// section's "query + sort + filter + null-guard" responsibility so the main
    /// window's draw path only consumes immutable snapshots. The window still owns
    /// translation/formatting (its translation context), but never issues raw
    /// <c>IArchiveService</c> ordering LINQ — that lives here.
    ///
    /// Failure isolation: a null service or a throwing query yields an empty snapshot
    /// rather than propagating into the UI draw loop.
    /// </summary>
    public sealed partial class ArchiveUiDataProvider : IArchiveUiDataProvider
    {
        public HomeSnapshot BuildHome(IArchiveService service, long revision)
        {
            HomeSnapshot snap = new HomeSnapshot { BuiltFromRevision = revision, IsEmpty = true };
            if (service == null)
            {
                return snap;
            }
            snap.ActivePawnCount = service.GetActiveSnapshotCount();
            snap.ArchivedPawnCount = service.GetArchivedSnapshotCount();
            snap.LiveColonistCount = service.GetLiveColonistCount();
            int free, slave, prisoner;
            service.GetLiveColonistCounts(out free, out slave, out prisoner);
            snap.LiveFreeCount = free;
            snap.LiveSlaveCount = slave;
            snap.LivePrisonerCount = prisoner;
            snap.ServiceDays = service.GetServiceDays();

            IReadOnlyList<ChronicleEvent> recent = service.GetRecentEvents(20);
            if (recent != null && recent.Count > 0)
            {
                snap.RecentEvents = recent.Where(e => e != null).ToList();
            }

            // Honest "important" proxy: the objects (Pawn + Thing) with the most
            // events. Mirrors the window's CollectCandidates ordering. Battle/Location
            // are excluded because they have no detail view.
            List<ArchiveObject> candidates = new List<ArchiveObject>();
            CollectCandidatesByEvents(service, ArchiveCategoryKeys.Pawn, candidates);
            CollectCandidatesByEvents(service, ArchiveCategoryKeys.Thing, candidates);
            // Prefetch counts once: every Sort comparison would otherwise issue a
            // GetEventsFor query against the store per object pair.
            Dictionary<string, int> countCache = new Dictionary<string, int>();
            candidates.Sort((a, b) =>
            {
                int byCount = EventCount(service, countCache, b.StableId)
                    .CompareTo(EventCount(service, countCache, a.StableId));
                // Deterministic tie-breaker: identical event counts must not reorder
                // between sessions (List<T>.Sort is unstable). StableId is a stable key.
                return byCount != 0 ? byCount : string.CompareOrdinal(a.StableId, b.StableId);
            });
            snap.ImportantObjects = candidates.Take(6).ToList();

            snap.IsEmpty = snap.RecentEvents.Count == 0 && snap.ImportantObjects.Count == 0;
            return snap;
        }

        public OverviewSnapshot BuildOverview(IArchiveService service, string categoryKey, long revision)
        {
            OverviewSnapshot snap = new OverviewSnapshot { BuiltFromRevision = revision, IsEmpty = true };
            if (service == null || string.IsNullOrEmpty(categoryKey))
            {
                return snap;
            }
            IReadOnlyList<ArchiveObject> objects = service.GetObjectsOfCategory(categoryKey);
            if (objects != null && objects.Count > 0)
            {
                Dictionary<string, int> countCache = new Dictionary<string, int>();
                snap.CategoryObjects[categoryKey] = objects
                    .Where(o => o != null)
                    .OrderByDescending(o => EventCount(service, countCache, o.StableId))
                    .ThenBy(o => o.StableId, System.StringComparer.Ordinal)
                    .ToList();
                // v4.14: Location atlas KPI strip + per-location event counts —
                // aggregated here (Read Model), the window renders only.
                if (categoryKey == ArchiveCategoryKeys.Location)
                {
                    snap.LocationKpis = BuildLocationKpis(snap.CategoryObjects[categoryKey]);
                    snap.LocationEventCounts = BuildLocationEventCounts(
                        service, countCache, snap.CategoryObjects[categoryKey]);
                }
                // v4.14: Battle KPI strip + per-battle card aggregates — Read Model.
                else if (categoryKey == ArchiveCategoryKeys.Battle)
                {
                    snap.BattleKpis = BuildBattleKpis(service, snap.CategoryObjects[categoryKey]);
                }
            }
            snap.IsEmpty = snap.CategoryObjects.Count == 0;
            return snap;
        }

        /// <summary>
        /// v4.14: aggregates the 5-cell Battle KPI strip + per-battle card views
        /// from the BattleObject snapshot list. Kills/losses come from the
        /// battle-scoped Death events (the battle stable id appears in the event
        /// Subjects). Significance delegates to <see cref="IBattleProvider"/>.
        /// Pure Read-Model aggregation — the window never re-derives these.
        /// </summary>
        private static BattleKpisView BuildBattleKpis(
            IArchiveService service, List<ArchiveObject> objects)
        {
            BattleKpisView kpi = new BattleKpisView();
            if (service == null || objects == null || objects.Count == 0)
            {
                return kpi;
            }
            // Kill vs our-loss discrimination: a Death event whose Subject
            // contains the battle AND whose Params carry the victim category.
            // Our losses = victim faction is the player's; kills = CombatRole kill
            // (victim is an enemy). Reuse the same event stream per battle.
            Dictionary<string, List<ChronicleEvent>> eventsByBattle =
                new Dictionary<string, List<ChronicleEvent>>(System.StringComparer.Ordinal);
            for (int i = 0; i < objects.Count; i++)
            {
                BattleObject battle = objects[i] as BattleObject;
                if (battle == null || string.IsNullOrEmpty(battle.StableId))
                {
                    continue;
                }
                kpi.Total++;
                BattleCardView card = new BattleCardView
                {
                    RaidCount = battle.RaidCount,
                    Participants = battle.ParticipantIds != null ? battle.ParticipantIds.Count : 0,
                    ThreatKey = battle.ThreatKey,
                    IsSignificant = ResolveBattleSignificance(service, battle)
                };
                // Battle-scoped Death events (this battle in Subjects).
                List<ChronicleEvent> evs;
                if (!eventsByBattle.TryGetValue(battle.StableId, out evs))
                {
                    evs = CollectBattleEvents(service, battle.StableId);
                    eventsByBattle[battle.StableId] = evs;
                }
                for (int e = 0; e < evs.Count; e++)
                {
                    ChronicleEvent ev = evs[e];
                    if (ev == null)
                    {
                        continue;
                    }
                    bool isKill = IsBattleKillEvent(ev);
                    if (isKill)
                    {
                        card.Kills++;
                    }
                    else
                    {
                        card.Losses++;
                    }
                }
                if (card.IsSignificant)
                {
                    kpi.Decisive++;
                }
                kpi.Kills += card.Kills;
                kpi.Losses += card.Losses;
                kpi.Roster += card.Participants;
                kpi.Cards[battle.StableId] = card;
            }
            return kpi;
        }

        /// <summary>
        /// v4.14: collects the events whose Subjects reference this battle stable id.
        /// </summary>
        private static List<ChronicleEvent> CollectBattleEvents(
            IArchiveService service, string battleStableId)
        {
            List<ChronicleEvent> result = new List<ChronicleEvent>();
            if (service == null || string.IsNullOrEmpty(battleStableId))
            {
                return result;
            }
            IReadOnlyList<ChronicleEvent> all = service.GetAllEvents();
            if (all == null)
            {
                return result;
            }
            for (int i = 0; i < all.Count; i++)
            {
                ChronicleEvent ev = all[i];
                if (ev == null || ev.Subjects == null)
                {
                    continue;
                }
                for (int s = 0; s < ev.Subjects.Count; s++)
                {
                    ObjectRef sub = ev.Subjects[s];
                    if (sub != null
                        && sub.CategoryKey == ArchiveCategoryKeys.Battle
                        && sub.StableId == battleStableId)
                    {
                        result.Add(ev);
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// v4.14: distinguishes a kill (our colony killed an enemy) from an own
        /// loss. Only Death-type events are counted (kills and losses are both
        /// Death events; the kill role is distinguished by the CombatRole param).
        /// Non-Death events attached to the battle (e.g. the battle start event's
        /// subject edges) are never counted as casualties.
        /// </summary>
        private static bool IsBattleKillEvent(ChronicleEvent ev)
        {
            if (ev == null || ev.TypeKey != ChronicleEventType.Death || ev.Params == null)
            {
                return false;
            }
            if (ev.Params.TryGetValue(ChronicleEventParams.CombatRole, out string role)
                && role == ChronicleEventParams.CombatRoleKill)
            {
                return true;
            }
            // No explicit kill role → our side died (a colony pawn Death event
            // linked to this battle via AttachCombatSubjects).
            return false;
        }

        /// <summary>
        /// v4.14: significance via the battle provider chain (IBattleProvider).
        /// Falls back to threat-key (ThreatBig = significant) when no provider
        /// is registered or the provider is undefined.
        /// </summary>
        private static bool ResolveBattleSignificance(IArchiveService service, BattleObject battle)
        {
            if (service == null || battle == null)
            {
                return false;
            }
            bool defined = false;
            bool significant = false;
            try
            {
                PersonalChronicle.Api.IPersonalChronicleApi api;
                if (PersonalChronicle.Api.PersonalChronicleApi.TryGet(out api)
                    && api != null && api.Providers != null)
                {
                    Dictionary<string, string> dataKeys = new Dictionary<string, string>
                    {
                        { "outcome", battle.ThreatKey ?? string.Empty }
                    };
                    BattleAppraisalInput input =
                        new BattleAppraisalInput(battle.StableId, dataKeys);
                    api.Providers.ForEach<IBattleProvider>(
                        provider =>
                        {
                            if (defined || provider == null)
                            {
                                return;
                            }
                            BattleAppraisal appraisal;
                            if (provider.TryAppraise(input, out appraisal))
                            {
                                defined = true;
                                significant = appraisal.IsSignificant;
                            }
                        });
                }
            }
            catch (System.Exception)
            {
                defined = false;
            }
            if (!defined)
            {
                return battle.ThreatKey == "ThreatBig";
            }
            return significant;
        }

        /// <summary>
        /// v4.14: aggregates the 8-cell Location KPI strip from the snapshot list.
        /// Kind classification reuses <see cref="ResolveLocationKindKey"/> so the
        /// card and the strip always agree. Pure Read-Model aggregation — the
        /// window never re-derives these counters.
        /// </summary>
        private static LocationKpisView BuildLocationKpis(List<ArchiveObject> objects)
        {
            LocationKpisView kpi = new LocationKpisView();
            if (objects == null || objects.Count == 0)
            {
                return kpi;
            }
            HashSet<string> factions = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < objects.Count; i++)
            {
                LocationObject loc = objects[i] as LocationObject;
                if (loc == null)
                {
                    continue;
                }
                kpi.Total++;
                string kind = ResolveLocationKindKey(loc);
                if (kind == "player") kpi.Home++;
                else if (kind == "quest") kpi.Quest++;
                else if (kind == "settle") kpi.Settle++;
                if (loc.DeinitTick != -1L) kpi.Ruined++;
                if (loc.CanTrade)
                {
                    kpi.Tradable++;
                    if (!string.IsNullOrEmpty(loc.PermitRequiredDefName)) kpi.Permit++;
                }
                if (loc.IsPlayerHome)
                {
                    factions.Add("player");
                }
                else if (!string.IsNullOrEmpty(loc.FactionDefName))
                {
                    factions.Add(loc.FactionDefName);
                }
            }
            kpi.Factions = factions.Count;
            return kpi;
        }

        /// <summary>
        /// v4.14: per-location event counts for the atlas card sub-line, reusing
        /// the same count cache as the overview ordering (no extra store queries).
        /// </summary>
        private static Dictionary<string, int> BuildLocationEventCounts(
            IArchiveService service, Dictionary<string, int> countCache, List<ArchiveObject> objects)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            if (service == null || objects == null || objects.Count == 0)
            {
                return counts;
            }
            for (int i = 0; i < objects.Count; i++)
            {
                LocationObject loc = objects[i] as LocationObject;
                if (loc == null || string.IsNullOrEmpty(loc.StableId))
                {
                    continue;
                }
                counts[loc.StableId] = EventCount(service, countCache, loc.StableId);
            }
            return counts;
        }

        /// <summary>
        /// v4.14: canonical location kind key — "player"/"settle"/"quest"/"unknown".
        /// Single source of truth for both the card (LocationDetailView) and the
        /// KPI strip aggregation; never duplicated in the window.
        /// </summary>
        private static string ResolveLocationKindKey(LocationObject loc)
        {
            if (loc == null)
            {
                return "unknown";
            }
            if (loc.IsPlayerHome)
            {
                return "player";
            }
            if (!string.IsNullOrEmpty(loc.WorldObjectDefName))
            {
                if (loc.WorldObjectDefName.IndexOf("Settlement", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "settle";
                }
                if (loc.WorldObjectDefName.IndexOf("Quest", System.StringComparison.OrdinalIgnoreCase) >= 0
                    || loc.WorldObjectDefName.IndexOf("Site", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "quest";
                }
            }
            return "unknown";
        }


        private static string SanitizeDefNameForDisplay(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return "—";
            }
            // Drop known prefixes and split CamelCase / snake_case into readable words.
            string cleaned = defName;
            string[] prefixes = new[]
            {
                HealthValuationKeys.EventPrefix,
            };
            for (int i = 0; i < prefixes.Length; i++)
            {
                if (cleaned.StartsWith(prefixes[i], System.StringComparison.Ordinal))
                {
                    cleaned = cleaned.Substring(prefixes[i].Length);
                    break;
                }
            }
            cleaned = cleaned.Replace('_', ' ');
            if (string.IsNullOrEmpty(cleaned))
            {
                return defName;
            }
            // Simple CamelCase split: insert space before uppercase letters (except first).
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < cleaned.Length; i++)
            {
                char c = cleaned[i];
                if (i > 0 && char.IsUpper(c) && char.IsLower(cleaned[i - 1]))
                {
                    sb.Append(' ');
                }
                sb.Append(c);
            }
            string result = sb.ToString();
            if (string.IsNullOrWhiteSpace(result))
            {
                return defName;
            }
            return result;
        }

        // kind weight table (architecture: weights centralized here, tunable)
        private static int KindWeight(ChronicleEventKind kind)
        {
            switch (kind)
            {
                case ChronicleEventKind.Death: return 100;
                case ChronicleEventKind.Join: return 90;
                case ChronicleEventKind.Battle: return 60;
                case ChronicleEventKind.Social: return 50;
                case ChronicleEventKind.Craft: return 40;
                case ChronicleEventKind.Built: return 35;
                default: return 10;
            }
        }

        // ---- local helpers (data-neutral; use Verse translation, NOT UI types) ----

        private static string FormatDateLocal(long tick)
        {
            // tick 0 是新档第 1 天（开局殖民者 JoinTick=0 即此），是合法日期；
            // 仅 -1（未知哨兵）才显示"未知"。
            if (tick < 0L) return "PersonalChronicle.UI.UnknownDate".Translate().ToString();
            return RimWorld.GenDate.DateReadoutStringAt(tick, UnityEngine.Vector2.zero);
        }

        private static string WorkTypeLabelLocal(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return "—";
            var def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
            return def != null ? def.labelShort != null ? def.labelShort : def.defName : defName;
        }

        private static string PlaceTextLocal(PlaceVisit v)
        {
            if (v == null) return "—";
            if (v.PlaceKind == PlaceVisitKeys.KindCaravan
                || (v.PlaceKey != null && v.PlaceKey.StartsWith(PlaceVisitKeys.TileKeyPrefix, System.StringComparison.Ordinal)))
            {
                string tile = v.PlaceKey != null
                    && v.PlaceKey.StartsWith(PlaceVisitKeys.TileKeyPrefix, System.StringComparison.Ordinal)
                    ? v.PlaceKey.Substring(PlaceVisitKeys.TileKeyPrefix.Length)
                    : v.PlaceKey;
                return "PersonalChronicle.UI.PlacesWorldTile".Translate(tile).ToString();
            }
            var biome = DefDatabase<BiomeDef>.GetNamedSilentFail(v.PlaceKey);
            return biome != null ? biome.LabelCap : (v.PlaceKey ?? "—");
        }

        /// <summary>
        /// Resolves a biome defName to its localized label; when the string is not a
        /// resolvable defName (e.g. a pre-v4.9.1 save storing a raw label), returns it
        /// unchanged so both record formats display.
        /// </summary>
        private static string ResolveBiomeLabelOrRaw(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "—") return "—";
            var biome = DefDatabase<BiomeDef>.GetNamedSilentFail(value);
            return biome != null ? biome.LabelCap : value;
        }

        private static string BackstoryText(PawnObject pawn)
        {
            if (pawn == null || string.IsNullOrEmpty(pawn.KindDefName)) return "—";
            var def = DefDatabase<PawnKindDef>.GetNamedSilentFail(pawn.KindDefName);
            return def != null ? def.LabelCap : pawn.KindDefName;
        }

        private static string FactionText(PawnObject pawn)
        {
            if (pawn == null || string.IsNullOrEmpty(pawn.FactionDefName)) return "—";
            var def = DefDatabase<FactionDef>.GetNamedSilentFail(pawn.FactionDefName);
            return def != null ? def.LabelCap : pawn.FactionDefName;
        }

        private static string DeathText(PawnObject pawn)
        {
            if (pawn == null) return "—";
            string cause = string.IsNullOrEmpty(pawn.DeathCauseKey) ? "" : pawn.DeathCauseKey.Translate().ToString();
            return string.IsNullOrEmpty(cause) ? "—" : cause;
        }

        private static string EventGlyph(ChronicleEventDef def)
        {
            if (def == null) return "•";
            switch (def.kind)
            {
                case ChronicleEventKind.Join: return "🚪";
                case ChronicleEventKind.Death: return "💀";
                case ChronicleEventKind.Battle: return "⚔️";
                case ChronicleEventKind.Craft: return "🔨";
                case ChronicleEventKind.Built: return "🏛️";
                case ChronicleEventKind.Social: return "💞";
                default: return "•";
            }
        }

        private static string EventTitleLocal(ChronicleEvent ev)
        {
            if (ev == null) return "—";
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            if (def != null && !string.IsNullOrEmpty(def.labelKey))
            {
                string t = def.labelKey.Translate().ToString();
                if (!string.IsNullOrEmpty(t)) return t;
            }
            return ev.TypeKey ?? "—";
        }

        private static string EventSubLocal(ChronicleEvent ev)
        {
            if (ev == null) return "";
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            if (def != null && !string.IsNullOrEmpty(def.descriptionKey))
            {
                string d = def.descriptionKey.Translate().ToString();
                if (!string.IsNullOrEmpty(d)) return d;
            }
            return "";
        }

        private static string KindLabelLocal(ChronicleEventKind kind)
        {
            return ("PersonalChronicle.UI.Ev" + kind.ToString()).Translate().ToString();
        }

    }
}

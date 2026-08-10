using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
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
    public sealed class ArchiveUiDataProvider : IArchiveUiDataProvider
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
            candidates.Sort((a, b) => EventCount(service, countCache, b.StableId)
                .CompareTo(EventCount(service, countCache, a.StableId)));
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
                    .ToList();
            }
            snap.IsEmpty = snap.CategoryObjects.Count == 0;
            return snap;
        }

        public DetailSnapshot BuildDetail(IArchiveService service, string detailObjectId, long revision)
        {
            DetailSnapshot snap = new DetailSnapshot { BuiltFromRevision = revision, IsEmpty = true };
            if (service == null || string.IsNullOrEmpty(detailObjectId))
            {
                return snap;
            }
            snap.DetailObject = service.GetObject(detailObjectId);
            IReadOnlyList<ChronicleEvent> events = service.GetEventsFor(detailObjectId);
            // v4.5.4: ordering / null-guard belong to the Read Model (v4.3 boundary) —
            // the window consumes the snapshot already sorted ascending by tick.
            snap.RawEvents = (events == null)
                ? (IReadOnlyList<ChronicleEvent>)new List<ChronicleEvent>()
                : events.Where(e => e != null).OrderBy(e => e.Tick).ToList();
            snap.IsEmpty = snap.RawEvents.Count == 0;

            // v4.6.5: production ledger derived here (Read Model), not in the window.
            // Group crafted/built events by defName, keep latest, sort by recency.
            snap.ProductionLines = BuildProductionLines(events);

            // v4.4: derive Pawn Overview content. All derivation is centralized here
            // (architecture §3.1 G layer) — the window only consumes these views.
            // Failure isolation: any throw yields empty derived lists, not a broken UI.
            try
            {
                if (snap.DetailObject is PawnObject pawn)
                {
                    snap.LifePhases = BuildLifePhases(pawn);
                    snap.CareerBars = BuildCareerBars(service, detailObjectId);
                    snap.Footprint = BuildFootprint(pawn);
                    snap.Milestones = BuildMilestones(events);
                    snap.Health = BuildHealth(service, detailObjectId);
                    snap.Relations = BuildRelations(service, pawn);
                }
                else if (snap.DetailObject is ThingObject thing)
                {
                    // v4.7: legacy chain (传承) for equipment — ownership-transfer
                    // generations, creator, verdict, holder table.
                    snap.Legacy = BuildLegacy(service, thing, events);
                }
                snap.KeyEvents = BuildKeyEvents(events);
            }
            catch (System.Exception ex)
            {
                // Derivation must never break the detail view; fall back to empty.
                Log.Warning("[PersonalChronicle] Overview derivation failed for "
                    + detailObjectId + ": " + ex.Message);
                snap.LifePhases = new List<LifePhaseView>();
                snap.CareerBars = new List<CareerBarView>();
                snap.Footprint = new FootprintLedgerView();
                snap.Milestones = new List<MilestoneView>();
                snap.KeyEvents = new List<KeyEventView>();
                snap.Health = new HealthView();
                snap.Legacy = new LegacyView();
                snap.Relations = new List<RelationView>();
            }
            return snap;
        }

        // ---- v4.4 Pawn Overview derivation (Read Model only) ----

        private static IReadOnlyList<LifePhaseView> BuildLifePhases(PawnObject pawn)
        {
            List<LifePhaseView> phases = new List<LifePhaseView>();
            if (pawn == null) return phases;

            // Origin (backstory) — always present as the narrative root.
            phases.Add(new LifePhaseView
            {
                PhaseKey = "PersonalChronicle.UI.LifePhase.Origin",
                IconKey = "🌱",
                DateText = null,
                SubText = BackstoryText(pawn),
                IsUnknown = false,
                Kind = LifePhaseKind.Origin
            });

            // Join — only when we have a real join tick (mid-install JoinTick=-1 skips).
            // Note: tick 0 is valid for pawns generated at the very start of a new colony.
            if (pawn.JoinTick >= 0L)
            {
                phases.Add(new LifePhaseView
                {
                    PhaseKey = "PersonalChronicle.UI.LifePhase.Join",
                    IconKey = "🚪",
                    DateText = FormatDateLocal(pawn.JoinTick),
                    SubText = FactionText(pawn),
                    IsUnknown = false,
                    Kind = LifePhaseKind.Join
                });
            }
            else
            {
                phases.Add(new LifePhaseView
                {
                    PhaseKey = "PersonalChronicle.UI.LifePhase.JoinUnknown",
                    IconKey = "⚠",
                    DateText = null,
                    SubText = "PersonalChronicle.UI.LifePhase.JoinUnknownSub".Translate().ToString(),
                    IsUnknown = true,
                    Kind = LifePhaseKind.Unknown
                });
            }

            // Active span.
            long activeEnd = pawn.IsArchived && pawn.DeathTick > 0L
                ? pawn.DeathTick
                : Find.TickManager.TicksGame;
            string activeDate = null;
            if (pawn.JoinTick >= 0L && activeEnd > pawn.JoinTick)
            {
                long days = (activeEnd - pawn.JoinTick) / RimWorld.GenDate.TicksPerDay;
                activeDate = FormatDateLocal(pawn.JoinTick) + " → " + FormatDateLocal(activeEnd)
                    + " (" + days + " " + "PersonalChronicle.UI.DaysUnit".Translate().ToString() + ")";
            }
            string activeSub = pawn.IsArchived
                ? "PersonalChronicle.UI.LifePhase.ActiveSub.Archived".Translate().ToString()
                : "PersonalChronicle.UI.LifePhase.ActiveSub.Alive".Translate().ToString();
            phases.Add(new LifePhaseView
            {
                PhaseKey = "PersonalChronicle.UI.LifePhase.Active",
                IconKey = "⏳",
                DateText = activeDate,
                SubText = activeSub,
                IsUnknown = false,
                Kind = LifePhaseKind.Active
            });

            // Death — only when archived.
            if (pawn.IsArchived)
            {
                phases.Add(new LifePhaseView
                {
                    PhaseKey = "PersonalChronicle.UI.LifePhase.Death",
                    IconKey = "💀",
                    DateText = FormatDateLocal(pawn.DeathTick),
                    SubText = DeathText(pawn),
                    IsUnknown = false,
                    Kind = LifePhaseKind.Death
                });
            }
            return phases;
        }

        private static IReadOnlyList<CareerBarView> BuildCareerBars(IArchiveService service, string id)
        {
            List<CareerBarView> bars = new List<CareerBarView>();
            if (service == null) return bars;
            IReadOnlyList<WorkTimeStatView> stats = service.GetWorkTimeStats(id);
            if (stats == null || stats.Count == 0) return bars;

            long total = 0L;
            for (int i = 0; i < stats.Count; i++) total += stats[i].Ticks;
            if (total <= 0L) return bars;

            // Top 5 by ticks.
            List<WorkTimeStatView> top = new List<WorkTimeStatView>(stats);
            top.Sort((a, b) => b.Ticks.CompareTo(a.Ticks));
            int take = System.Math.Min(5, top.Count);
            for (int i = 0; i < take; i++)
            {
                WorkTimeStatView w = top[i];
                bars.Add(new CareerBarView
                {
                    WorkTypeLabel = WorkTypeLabelLocal(w.WorkTypeDefName),
                    Ticks = w.Ticks,
                    Share01 = (float)w.Ticks / total,
                    IsPrimary = i == 0,
                    IsSecondary = i == 1
                });
            }
            return bars;
        }

        private static FootprintLedgerView BuildFootprint(PawnObject pawn)
        {
            FootprintLedgerView led = new FootprintLedgerView();
            if (pawn == null || pawn.PlaceHistory == null || pawn.PlaceHistory.Count == 0)
            {
                return led;
            }
            List<FootstepView> stays = new List<FootstepView>();
            int homeIdx = -1;
            long homeDays = -1L;
            int expeditions = 0;
            long now = Find.TickManager.TicksGame;

            for (int i = 0; i < pawn.PlaceHistory.Count; i++)
            {
                PlaceVisit v = pawn.PlaceHistory[i];
                if (v == null) continue;
                bool isWorld = v.PlaceKind == "Caravan" || (v.PlaceKey != null && v.PlaceKey.StartsWith("tile:"));
                if (isWorld) expeditions++;
                long enter = v.EnterTick > 0L ? v.EnterTick : -1L;
                long leave = v.IsOpen ? now : (v.LeaveTick > 0L ? v.LeaveTick : -1L);
                long days = -1L;
                if (enter > 0L && leave > 0L && leave >= enter)
                {
                    days = (leave - enter) / RimWorld.GenDate.TicksPerDay;
                }
                if (days > homeDays) { homeDays = days; homeIdx = stays.Count; }
                stays.Add(new FootstepView
                {
                    PlaceText = PlaceTextLocal(v),
                    IsWorldTile = isWorld,
                    DwellText = days >= 0 ? (days + " " + "PersonalChronicle.UI.DaysUnit".Translate().ToString()) : "PersonalChronicle.UI.UnknownDate".Translate().ToString(),
                    IsHome = false
                });
            }

            // Longest dwell first.
            stays.Sort((a, b) =>
            {
                long da = a.DwellText == "PersonalChronicle.UI.UnknownDate".Translate().ToString() ? -1 : ParseDays(a.DwellText);
                long db = b.DwellText == "PersonalChronicle.UI.UnknownDate".Translate().ToString() ? -1 : ParseDays(b.DwellText);
                return db.CompareTo(da);
            });
            if (homeIdx >= 0 && homeIdx < stays.Count) stays[homeIdx].IsHome = true;

            led.PlaceCount = pawn.PlaceHistory.Count;
            led.HomePlaceText = homeIdx >= 0 ? stays[homeIdx].PlaceText : null;
            led.HomeDays = homeDays >= 0 ? (int)homeDays : 0;
            led.ExpeditionCount = expeditions;
            led.Stays = stays;
            return led;
        }

        private static IReadOnlyList<MilestoneView> BuildMilestones(IReadOnlyList<ChronicleEvent> events)
        {
            List<MilestoneView> ms = new List<MilestoneView>();
            if (events == null || events.Count == 0) return ms;

            // One representative per kind; Other excluded (avoids noise).
            Dictionary<string, ChronicleEvent> best = new Dictionary<string, ChronicleEvent>();
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev == null) continue;
                ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
                if (def == null) continue;
                if (def.kind == ChronicleEventKind.Other) continue;
                int imp = (int)ChronicleEventImportance.Resolve(ev);
                string kind = def.kind.ToString();
                if (!best.TryGetValue(kind, out ChronicleEvent cur)
                    || imp > (int)ChronicleEventImportance.Resolve(cur))
                {
                    best[kind] = ev;
                }
            }
            foreach (var kv in best)
            {
                ChronicleEvent ev = kv.Value;
                ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
                ms.Add(new MilestoneView
                {
                    IconKey = EventGlyph(def),
                    TitleText = EventTitleLocal(ev),
                    DateText = FormatDateLocal(ev.Tick),
                    SubText = EventSubLocal(ev),
                    KindKey = kv.Key,
                    RawTick = ev.Tick
                });
            }
            // Chronological order; unknown dates sink to the end.
            ms.Sort((a, b) => a.RawTick.CompareTo(b.RawTick));
            return ms;
        }

        private static IReadOnlyList<KeyEventView> BuildKeyEvents(IReadOnlyList<ChronicleEvent> events)
        {
            List<KeyEventView> list = new List<KeyEventView>();
            if (events == null) return list;
            // Deduplicate by (TypeKey, Tick) to avoid duplicate death/battle records
            // from multiple capture points (e.g. Death recorded twice in the same tick).
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev == null) continue;
                string dedup = (ev.TypeKey ?? "") + ":" + ev.Tick;
                if (!seen.Add(dedup)) continue;
                ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
                if (def == null) continue;
                int kindWeight = KindWeight(def.kind);
                int imp = (int)ChronicleEventImportance.Resolve(ev);
                int salience = kindWeight + imp;
                list.Add(new KeyEventView
                {
                    IconKey = EventGlyph(def),
                    DateText = FormatDateLocal(ev.Tick),
                    TitleText = EventTitleLocal(ev),
                    TypeText = KindLabelLocal(def.kind),
                    IsHighlight = salience >= 90,
                    Salience = salience,
                    RawTick = ev.Tick
                });
            }
            // Top 3 by salience, then chronological.
            list.Sort((a, b) => b.Salience.CompareTo(a.Salience));
            if (list.Count > 3) list.RemoveRange(3, list.Count - 3);
            list.Sort((a, b) => a.RawTick.CompareTo(b.RawTick));
            return list;
        }

        // ---- 健康残值 · 资产折旧 (derivation only; window renders HealthView) ----

        private static HealthView BuildHealth(IArchiveService service, string stableId)
        {
            HealthView empty = new HealthView();
            if (service == null || string.IsNullOrEmpty(stableId)) return empty;
            Pawn live = service.GetLivePawn(stableId);
            if (live == null)
            {
                Log.Warning($"[PersonalChronicle] BuildHealth: no live pawn for stableId={stableId}.");
                return empty;
            }

            HealthValuationPolicyDef policy = DefDatabase<HealthValuationPolicyDef>.GetNamedSilentFail(
                HealthValuationPolicyDef.DefaultPolicyDefName);
            if (policy == null)
            {
                Log.Warning($"[PersonalChronicle] BuildHealth: HealthValuationPolicyDef '{HealthValuationPolicyDef.DefaultPolicyDefName}' not loaded. Check Defs/HealthValuation.xml uses <defName> element.");
                return empty;
            }

            HealthValuationResult r = HealthValuationEvaluator.Evaluate(live, policy);
            if (!r.IsDefined) return empty;

            List<HealthFactorView> factors = new List<HealthFactorView>();
            for (int i = 0; i < r.Factors.Count; i++)
            {
                HealthFactor f = r.Factors[i];
                if (f == null) continue;
                string label = string.IsNullOrEmpty(f.LabelKey)
                    ? "—"
                    : f.LabelKey.Translate().ToString();
                factors.Add(new HealthFactorView
                {
                    IsPositive = f.IsPositive,
                    LabelText = label,
                    Impact = Mathf.RoundToInt(f.Impact)
                });
            }

            List<HealthFactorView> bodyFactors = BuildHealthFactorViews(r.BodyFactors);
            List<HealthFactorView> spiritFactors = BuildHealthFactorViews(r.SpiritFactors);
            List<HealthFactorView> youthFactors = BuildHealthFactorViews(r.YouthFactors);

            List<HealthEventView> eventViews = new List<HealthEventView>();
            for (int i = 0; i < r.Events.Count; i++)
            {
                HealthDepreciationEvent e = r.Events[i];
                if (e == null) continue;
                string desc = ResolveEventDescription(e);
                string tag = string.IsNullOrEmpty(e.TagKey)
                    ? ""
                    : e.TagKey.Translate().ToString();
                string dateText = e.RawTick > 0L
                    ? GenDate.DateReadoutStringAt(e.RawTick, Vector2.zero)
                    : "PersonalChronicle.UI.UnknownDate".Translate().ToString();
                eventViews.Add(new HealthEventView
                {
                    DateText = dateText,
                    Description = desc,
                    TagText = tag,
                    RawDefName = e.RawDefName,
                    Impact = Mathf.RoundToInt(e.Impact),
                    RawTick = e.RawTick
                });
            }

            return new HealthView
            {
                IsDefined = true,
                HealthScore = r.HealthScore,
                BodyPercent = r.BodyPercent,
                AgeYears = r.AgeYears,
                SilverValue = Mathf.RoundToInt(r.SilverValue),
                BaseSilverValue = Mathf.RoundToInt(r.BaseSilverValue),
                WeeklySilverEstimate = Mathf.RoundToInt(r.WeeklySilverEstimate),
                IsPrime = r.IsPrime,
                IsImpaired = r.IsImpaired,
                BodyIntegrityScore = r.BodyIntegrityScore,
                SpiritScore = r.SpiritScore,
                YouthScore = r.YouthScore,
                BodyFactors = bodyFactors,
                SpiritFactors = spiritFactors,
                YouthFactors = youthFactors,
                Factors = factors,
                Events = eventViews
            };
        }

        private static List<HealthFactorView> BuildHealthFactorViews(
            IReadOnlyList<HealthFactor> src)
        {
            List<HealthFactorView> list = new List<HealthFactorView>();
            if (src == null) return list;
            for (int i = 0; i < src.Count; i++)
            {
                HealthFactor f = src[i];
                if (f == null) continue;
                string label = string.IsNullOrEmpty(f.LabelKey)
                    ? "—"
                    : f.LabelKey.Translate().ToString();
                list.Add(new HealthFactorView
                {
                    IsPositive = f.IsPositive,
                    LabelText = label,
                    Impact = Mathf.RoundToInt(f.Impact)
                });
            }
            return list;
        }

        private static string ResolveEventDescription(HealthDepreciationEvent e)
        {
            // Prefer explicit translation (Event.{defName}). If that key is missing
            // the translator returns the key verbatim, so fall back to the human-readable
            // HediffDef.label (e.g. "腰损" for BadBack). As a last resort, sanitise the
            // raw defName so we never expose a full translation key like
            // "PersonalChronicle.UI.HealthValuation.Event.Scratch" in the UI.
            if (!string.IsNullOrEmpty(e.LabelKey))
            {
                string translated = e.LabelKey.Translate().ToString();
                bool usable = !string.IsNullOrWhiteSpace(translated)
                    && !string.Equals(translated, e.LabelKey, System.StringComparison.Ordinal);
                if (usable)
                {
                    return translated;
                }
            }
            if (!string.IsNullOrEmpty(e.RawDefName))
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(e.RawDefName);
                if (def != null && !string.IsNullOrEmpty(def.label))
                {
                    return def.label;
                }
                return SanitizeDefNameForDisplay(e.RawDefName);
            }
            return "—";
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
                "PersonalChronicle.UI.HealthValuation.Event.",
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
            if (tick <= 0L) return "PersonalChronicle.UI.UnknownDate".Translate().ToString();
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
            if (v.PlaceKind == "Caravan" || (v.PlaceKey != null && v.PlaceKey.StartsWith("tile:")))
            {
                string tile = v.PlaceKey != null && v.PlaceKey.StartsWith("tile:")
                    ? v.PlaceKey.Substring(5) : v.PlaceKey;
                return "PersonalChronicle.UI.PlacesWorldTile".Translate(tile).ToString();
            }
            var biome = DefDatabase<BiomeDef>.GetNamedSilentFail(v.PlaceKey);
            return biome != null ? biome.LabelCap : (v.PlaceKey ?? "—");
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

        private static long ParseDays(string dwell)
        {
            // dwell format: "<n> <DaysUnit>" or UnknownDate; extract leading number.
            if (string.IsNullOrEmpty(dwell)) return -1L;
            int sp = dwell.IndexOf(' ');
            string num = sp > 0 ? dwell.Substring(0, sp) : dwell;
            long v;
            return long.TryParse(num, out v) ? v : -1L;
        }

        public EventSnapshot BuildEvent(IArchiveService service, ChronicleEvent ev, long revision)
        {
            EventSnapshot snap = new EventSnapshot { BuiltFromRevision = revision, IsEmpty = true, Event = ev };
            if (ev == null)
            {
                return snap;
            }
            snap.IsEmpty = false;
            // v4.6.5: follow-up events derived here (Read Model), not in the window.
            // Later events of the same primary object, capped at 3, ascending by tick.
            if (service != null && ev.Primary != null && !string.IsNullOrEmpty(ev.Primary.StableId))
            {
                IReadOnlyList<ChronicleEvent> evs = service.GetEventsFor(ev.Primary.StableId);
                if (evs != null)
                {
                    snap.FollowupEvents = evs
                        .Where(e => e != null && e.Tick > ev.Tick)
                        .OrderBy(e => e.Tick)
                        .Take(3)
                        .ToList();
                }
            }
            return snap;
        }

        /// <summary>
        /// Full timeline event stream, sorted ascending by <see cref="ChronicleEvent.Tick"/>
        /// and null-guarded. The Home timeline only consumes this snapshot; the ordering
        /// and null-filtering never leak into the window's draw path (design doc §7.4).
        /// </summary>
        public IReadOnlyList<ChronicleEvent> BuildTimelineEvents(IArchiveService service, long revision)
        {
            if (service == null)
            {
                return System.Array.Empty<ChronicleEvent>();
            }
            IReadOnlyList<ChronicleEvent> all = service.GetAllEvents();
            if (all == null)
            {
                return System.Array.Empty<ChronicleEvent>();
            }
            return all
                .Where(e => e != null)
                .OrderBy(e => e.Tick)
                .ToList();
        }

        /// <summary>
        /// Event count for a stable id, memoized so sort comparators never issue
        /// repeated <see cref="IArchiveService.GetEventsFor"/> queries.
        /// </summary>
        private static int EventCount(IArchiveService service, Dictionary<string, int> cache, string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return 0;
            }
            if (cache.TryGetValue(stableId, out int cached))
            {
                return cached;
            }
            IReadOnlyList<ChronicleEvent> evs = service.GetEventsFor(stableId);
            int c = evs == null ? 0 : evs.Count;
            cache[stableId] = c;
            return c;
        }

        // ---- v4.6.5 production ledger (Read Model only; window maps the snapshot) ----

        private static IReadOnlyList<ProductionLineView> BuildProductionLines(
            IReadOnlyList<ChronicleEvent> events)
        {
            List<ProductionLineView> lines = new List<ProductionLineView>();
            if (events == null || events.Count == 0) return lines;
            Dictionary<string, ProductionLineView> byDef = new Dictionary<string, ProductionLineView>();
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev == null || !IsProductionEvent(ev)) continue;
                ObjectRef primary = ev.Primary;
                if (primary == null || string.IsNullOrEmpty(primary.StableId)) continue;
                string defName = ThingDefNameFromStableId(primary.StableId);
                if (string.IsNullOrEmpty(defName)) continue;
                if (byDef.TryGetValue(defName, out ProductionLineView line))
                {
                    line.Count++;
                    if (ev.Tick > line.LastTick)
                    {
                        line.LastTick = ev.Tick;
                        line.StableId = primary.StableId;
                    }
                    byDef[defName] = line;
                }
                else
                {
                    byDef[defName] = new ProductionLineView
                    {
                        DefName = defName,
                        Label = ThingDefLabelLocal(defName),
                        Count = 1,
                        LastTick = ev.Tick,
                        StableId = primary.StableId
                    };
                }
            }
            List<ProductionLineView> result = new List<ProductionLineView>(byDef.Values);
            result.Sort((a, b) => b.LastTick.CompareTo(a.LastTick));
            return result;
        }

        private static bool IsProductionEvent(ChronicleEvent ev)
        {
            if (ev == null || string.IsNullOrEmpty(ev.TypeKey)) return false;
            ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
            return def != null && (def.kind == ChronicleEventKind.Craft || def.kind == ChronicleEventKind.Built);
        }

        // ---- v4.7 Legacy chain (传承) derivation ----

        private static LegacyView BuildLegacy(IArchiveService service, ThingObject thing, IReadOnlyList<ChronicleEvent> events)
        {
            LegacyView view = new LegacyView { IsEmpty = true };
            if (service == null || thing == null)
            {
                return view;
            }
            // Work on a local record list: the Read Model never writes back to the
            // Domain object (boundary rule). Pre-v4.7 saves fall back to the legacy
            // HolderHistory, materialized into local HolderRecords.
            List<HolderRecord> records;
            if (thing.HolderRecords != null && thing.HolderRecords.Count > 0)
            {
                records = new List<HolderRecord>(thing.HolderRecords.Count);
                for (int i = 0; i < thing.HolderRecords.Count; i++)
                {
                    HolderRecord src = thing.HolderRecords[i];
                    if (src == null) continue;
                    records.Add(new HolderRecord(src.StableId, src.LabelSnapshot, src.StartTick, src.IsFirst, src.Kind));
                }
            }
            else if (thing.HolderHistory != null && thing.HolderHistory.Count > 0)
            {
                records = new List<HolderRecord>(thing.HolderHistory.Count);
                for (int i = 0; i < thing.HolderHistory.Count; i++)
                {
                    ObjectRef h = thing.HolderHistory[i];
                    if (h == null || string.IsNullOrEmpty(h.StableId)) continue;
                    records.Add(new HolderRecord(
                        h.StableId,
                        h.LabelSnapshot ?? h.StableId,
                        -1L,
                        i == 0,
                        HolderRecord.HolderKindOwn));
                }
            }
            else
            {
                return view;
            }
            if (records.Count == 0)
            {
                return view;
            }

            // Kill events of this weapon: Death events whose subject is the weapon.
            List<ChronicleEvent> killEvents = new List<ChronicleEvent>();
            if (events != null)
            {
                for (int i = 0; i < events.Count; i++)
                {
                    ChronicleEvent ev = events[i];
                    if (ev == null || ev.TypeKey != ChronicleEventType.Death) continue;
                    // GetEventsFor(weaponId) already returns only events linked to
                    // this weapon; Death events among them are kills by it.
                    killEvents.Add(ev);
                }
            }

            // Assign an exclusive end tick per record (the next record's start) so
            // kill events can be bucketed by tenure. Computed on the local copies.
            long now = Find.TickManager.TicksGame;
            long[] ends = new long[records.Count];
            for (int i = 0; i < records.Count; i++)
            {
                HolderRecord rec = records[i];
                if (rec == null) continue;
                ends[i] = (i + 1 < records.Count && records[i + 1] != null)
                    ? records[i + 1].StartTick
                    : now;
            }

            List<LegacyHolderView> holders = new List<LegacyHolderView>();
            int genCounter = 0;
            int totalKills = 0;
            string currentHolderText = null;
            string currentHolderId = thing.CurrentHolderId;
            if (!string.IsNullOrEmpty(currentHolderId))
            {
                ArchiveObject cur = service.GetObject(currentHolderId);
                currentHolderText = cur != null ? ObjectLabelLocal(cur) : currentHolderId;
            }

            for (int i = 0; i < records.Count; i++)
            {
                HolderRecord rec = records[i];
                if (rec == null) continue;
                bool isLoan = rec.Kind == HolderRecord.HolderKindLoan;
                long endTick = ends[i];
                int kills = CountKillsInWindow(killEvents, rec.StartTick, endTick);
                totalKills += kills;

                HolderRecord firstRec = records[0];
                LegacyHolderView h = new LegacyHolderView
                {
                    HolderStableId = rec.StableId,
                    HolderText = ResolveHolderLabel(service, rec),
                    Generation = isLoan ? 0 : ++genCounter,
                    IsFirst = rec.IsFirst,
                    IsLoan = isLoan,
                    IsCurrent = !string.IsNullOrEmpty(rec.StableId) && rec.StableId == currentHolderId,
                    FromText = FormatDateLocal(rec.StartTick),
                    ToText = endTick > 0L && endTick >= rec.StartTick
                        ? FormatDateLocal(endTick)
                        : "PersonalChronicle.UI.Legacy.Now".Translate().ToString(),
                    DurationText = DurationTextLocal(rec.StartTick, endTick),
                    KillCount = kills,
                    CreatedByText = firstRec != null
                        ? ResolveHolderLabel(service, firstRec)
                        : null,
                    CreatedAtText = firstRec != null
                        ? FormatDateLocal(firstRec.StartTick)
                        : null,
                    RemarkText = isLoan
                        ? "PersonalChronicle.UI.Legacy.LoanRemark".Translate().ToString()
                        : null
                };
                holders.Add(h);
            }

            int genCount = genCounter;
            string createdBy = holders.Count > 0 ? holders[0].HolderText : null;
            string createdAt = holders.Count > 0 ? holders[0].FromText : null;

            view.TitleText = LegacyTitleText(thing.ThingDefName);
            view.VerdictText = LegacyVerdictText(genCount, totalKills);
            view.CreatedByText = createdBy;
            view.CreatedAtText = createdAt;
            view.GenCount = genCount;
            view.TotalKills = totalKills;
            view.CurrentHolderText = currentHolderText;
            view.Holders = holders;
            view.IsEmpty = holders.Count == 0;
            return view;
        }

        private static int CountKillsInWindow(List<ChronicleEvent> killEvents, long start, long end)
        {
            if (killEvents == null || killEvents.Count == 0) return 0;
            int count = 0;
            for (int i = 0; i < killEvents.Count; i++)
            {
                ChronicleEvent ev = killEvents[i];
                if (ev == null) continue;
                if (start >= 0L && ev.Tick < start) continue;
                if (end >= 0L && ev.Tick > end) continue;
                count++;
            }
            return count;
        }

        private static string ResolveHolderLabel(IArchiveService service, HolderRecord rec)
        {
            if (rec == null) return "—";
            if (!string.IsNullOrEmpty(rec.LabelSnapshot)) return rec.LabelSnapshot;
            if (service != null && !string.IsNullOrEmpty(rec.StableId))
            {
                ArchiveObject obj = service.GetObject(rec.StableId);
                if (obj != null) return ObjectLabelLocal(obj);
            }
            return rec.StableId ?? "—";
        }

        private static string ObjectLabelLocal(ArchiveObject obj)
        {
            if (obj == null) return "—";
            if (!string.IsNullOrEmpty(obj.LabelSnapshot)) return obj.LabelSnapshot;
            return obj.StableId ?? "—";
        }

        private static string DurationTextLocal(long start, long end)
        {
            if (start < 0L)
            {
                return "PersonalChronicle.UI.UnknownDate".Translate().ToString();
            }
            if (end < start)
            {
                return "PersonalChronicle.UI.Legacy.Now".Translate().ToString();
            }
            long days = (end - start) / RimWorld.GenDate.TicksPerDay;
            return days + " " + "PersonalChronicle.UI.DaysUnit".Translate().ToString();
        }

        /// <summary>Data-driven epithet (传承称号) from the thing def; tokenized so
        /// the UI stays zero-hardcoded (see Defs/Legacy.xml when shipped).</summary>
        private static string LegacyTitleText(string thingDefName)
        {
            if (string.IsNullOrEmpty(thingDefName)) return null;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            if (def == null) return null;
            // Epithets are authored per weapon tier via a legacy epithet map keyed
            // by ThingCategoryDef (not by concrete defName), keeping this data-driven.
            ThingCategoryDef cat = def.FirstThingCategory;
            if (cat != null && LegacyEpithetCatalog.TryGetValue(cat.defName, out string epithetKey))
            {
                return epithetKey.Translate().ToString();
            }
            return null;
        }

        /// <summary>Epithet catalog: weapon category → translation key. Tokens, not
        /// hardcoded display strings; extend via XML (see LegacyEpithetDef when shipped).</summary>
        private static readonly Dictionary<string, string> LegacyEpithetCatalog =
            new Dictionary<string, string>
            {
                { "Weapons", "PersonalChronicle.UI.Legacy.EpithetWeapons" },
                { "Apparel", "PersonalChronicle.UI.Legacy.EpithetApparel" }
            };

        private static string LegacyVerdictText(int genCount, int totalKills)
        {
            if (genCount <= 0)
            {
                return "PersonalChronicle.UI.Legacy.VerdictNoGen".Translate().ToString();
            }
            if (totalKills >= 3)
            {
                return "PersonalChronicle.UI.Legacy.VerdictBlooded".Translate().ToString();
            }
            if (totalKills > 0)
            {
                return "PersonalChronicle.UI.Legacy.VerdictTasted".Translate().ToString();
            }
            if (genCount >= 2)
            {
                return "PersonalChronicle.UI.Legacy.VerdictPassed".Translate().ToString();
            }
            return "PersonalChronicle.UI.Legacy.VerdictFresh".Translate().ToString();
        }

        private static string ThingDefNameFromStableId(string stableId)
        {
            if (string.IsNullOrEmpty(stableId)) return string.Empty;
            int colon = stableId.IndexOf(':');
            return colon >= 0 ? stableId.Substring(0, colon) : stableId;
        }

        private static string ThingDefLabelLocal(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return "—";
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return def != null && !string.IsNullOrEmpty(def.label) ? def.label : defName;
        }

        private static void CollectCandidatesByEvents(
            IArchiveService service, string categoryKey, List<ArchiveObject> candidates)
        {
            if (service == null || string.IsNullOrEmpty(categoryKey) || candidates == null)
            {
                return;
            }
            IReadOnlyList<ArchiveObject> objects = service.GetObjectsOfCategory(categoryKey);
            if (objects == null)
            {
                return;
            }
            for (int i = 0; i < objects.Count; i++)
            {
                ArchiveObject obj = objects[i];
                if (obj != null && !string.IsNullOrEmpty(obj.StableId))
                {
                    candidates.Add(obj);
                }
            }
        }

        // ---- v4.6 social ties (Read Model; merges live state + archived snapshots) ----

        private static IReadOnlyList<RelationView> BuildRelations(IArchiveService service, PawnObject pawn)
        {
            List<RelationView> rows = new List<RelationView>();
            HashSet<string> seen = new HashSet<string>();
            if (service == null || pawn == null)
            {
                return rows;
            }

            Pawn livePawn = service.GetLivePawn(pawn.StableId);

            // 1) Current live relations.
            if (livePawn?.relations?.DirectRelations != null)
            {
                List<DirectPawnRelation> directRelations = livePawn.relations.DirectRelations;
                for (int i = 0; i < directRelations.Count; i++)
                {
                    DirectPawnRelation rel = directRelations[i];
                    if (rel?.def == null || rel.otherPawn == null)
                    {
                        continue;
                    }
                    if (!SocialRelationFilter.IsSignificant(rel.def))
                    {
                        continue;
                    }
                    string otherId = rel.otherPawn.GetUniqueLoadID();
                    string key = MakeRelationKey(rel.def.defName, otherId);
                    if (!seen.Add(key))
                    {
                        continue;
                    }
                    rows.Add(new RelationView
                    {
                        OtherLabel = rel.otherPawn.LabelShort,
                        RelationLabel = RelationLabelFor(rel.def, rel.otherPawn),
                        StatusLabel = rel.otherPawn.Dead
                            ? "PersonalChronicle.UI.Dead".Translate().ToString()
                            : "PersonalChronicle.UI.Alive".Translate().ToString(),
                        IsLive = true
                    });
                }
            }

            // 2) Archived relations (initial ties captured at join, plus historical changes).
            if (pawn.Relations != null)
            {
                for (int i = 0; i < pawn.Relations.Count; i++)
                {
                    SignificantRelation rel = pawn.Relations[i];
                    if (rel == null)
                    {
                        continue;
                    }
                    string key = MakeRelationKey(rel.RelationDefName, rel.OtherStableId);
                    if (!seen.Add(key))
                    {
                        continue;
                    }
                    Pawn otherLive = service.GetLivePawn(rel.OtherStableId);
                    bool otherDead = otherLive != null && otherLive.Dead;
                    string status = otherDead
                        ? "PersonalChronicle.UI.Dead".Translate().ToString()
                        : (rel.IsActive
                            ? "PersonalChronicle.UI.Alive".Translate().ToString()
                            : "PersonalChronicle.UI.RelEnded".Translate().ToString());
                    PawnRelationDef def = DefDatabase<PawnRelationDef>.GetNamedSilentFail(rel.RelationDefName);
                    rows.Add(new RelationView
                    {
                        OtherLabel = !string.IsNullOrEmpty(rel.OtherLabel) ? rel.OtherLabel : rel.OtherStableId,
                        RelationLabel = RelationLabelFor(def, otherLive),
                        StatusLabel = status,
                        IsLive = false
                    });
                }
            }

            return rows;
        }

        private static string MakeRelationKey(string relationDefName, string otherStableId)
        {
            return (relationDefName ?? string.Empty) + "::" + (otherStableId ?? string.Empty);
        }

        private static string RelationLabelFor(PawnRelationDef def, Pawn otherPawn)
        {
            if (def == null)
            {
                return string.Empty;
            }
            if (otherPawn != null)
            {
                string gendered = def.GetGenderSpecificLabel(otherPawn);
                if (!string.IsNullOrEmpty(gendered))
                {
                    return gendered;
                }
            }
            if (!string.IsNullOrEmpty(def.label))
            {
                return def.label;
            }
            return def.defName;
        }
    }
}

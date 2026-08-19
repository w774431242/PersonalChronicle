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
    /// Partial of <see cref="ArchiveUiDataProvider"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveUiDataProvider : IArchiveUiDataProvider
    {

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

            // Assign an exclusive [start, end) window per record so kill events can
            // be bucketed by tenure without double counting. Computed on the local
            // copies. Pre-v4.9 legacy records may have StartTick < 0 (no stored time);
            // give them a derived start (the previous record's end, or 0 for the
            // first) so a kill is still attributed to exactly one holder instead of
            // falling into every record and inflating totalKills.
            long now = Find.TickManager.TicksGame;
            long[] starts = new long[records.Count];
            long[] ends = new long[records.Count];
            for (int i = 0; i < records.Count; i++)
            {
                HolderRecord rec = records[i];
                if (rec == null) continue;
                long end = (i + 1 < records.Count && records[i + 1] != null)
                    ? records[i + 1].StartTick
                    : now;
                if (end < 0L) end = now;
                ends[i] = end;
                long start = rec.StartTick;
                if (start < 0L)
                {
                    start = (i > 0) ? ends[i - 1] : 0L;
                    if (start > end) start = end;
                }
                starts[i] = start;
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
                long startTick = starts[i];
                long endTick = ends[i];
                int kills = CountKillsInWindow(killEvents, startTick, endTick);
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

        // ---- v4.9 equipment legacy extension (溯源 / 工坊署名链 / 同袍共用 / 退役仪式) ----

        /// <summary>
        /// 溯源 (origin): derives where the thing came from by scanning the event
        /// stream. Craft/Built → "craft" (maker = first craft subject); otherwise
        /// if the thing has battle/death records the origin is at least "battle"
        /// (field-tested). Every label is localized here; the window never sorts.
        /// </summary>
        private static ThingOriginView BuildOrigin(
            IArchiveService service, ThingObject thing, IReadOnlyList<ChronicleEvent> events)
        {
            ThingOriginView view = new ThingOriginView { IsEmpty = true };
            if (service == null || thing == null || events == null)
            {
                return view;
            }
            // First relevant event decides the origin kind.
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev == null || string.IsNullOrEmpty(ev.TypeKey)) continue;
                ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
                if (def == null) continue;
                if (def.kind == ChronicleEventKind.Craft || def.kind == ChronicleEventKind.Built)
                {
                    view.KindText = "PersonalChronicle.UI.Origin.Craft".Translate().ToString();
                    view.KindKey = "craft";
                    if (ev.Subjects != null)
                    {
                        for (int s = 0; s < ev.Subjects.Count; s++)
                        {
                            ObjectRef sub = ev.Subjects[s];
                            if (sub != null && sub.CategoryKey == ArchiveCategoryKeys.Pawn
                                && !string.IsNullOrEmpty(sub.StableId))
                            {
                                view.FromStableId = sub.StableId;
                                view.FromText = ResolveObjectLabel(service, sub);
                                break;
                            }
                        }
                    }
                    if (string.IsNullOrEmpty(view.FromText))
                    {
                        view.FromText = "PersonalChronicle.UI.UnknownDate".Translate().ToString();
                    }
                    // v4.14: 来源地点 = the location/battle subject on the craft
                    // event (workshop / field / expedition). Pure subject-edge
                    // resolution, no invented text.
                    view.WhereText = ResolveOriginPlaceLabel(service, ev);
                    view.IsEmpty = false;
                    return view;
                }
                if (def.kind == ChronicleEventKind.Battle || def.kind == ChronicleEventKind.Death)
                {
                    // Field-tested in combat before any craft record → battle origin.
                    view.KindText = "PersonalChronicle.UI.Origin.Battle".Translate().ToString();
                    view.KindKey = "battle";
                    view.IsEmpty = false;
                    return view;
                }
            }
            return view;
        }

        /// <summary>
        /// v4.14: resolves the place/battle label attached to an event's Subjects
        /// (the origin "where"). Returns null when the event carries no location
        /// or battle edge.
        /// </summary>
        private static string ResolveOriginPlaceLabel(IArchiveService service, ChronicleEvent ev)
        {
            if (ev == null || ev.Subjects == null)
            {
                return null;
            }
            for (int s = 0; s < ev.Subjects.Count; s++)
            {
                ObjectRef sub = ev.Subjects[s];
                if (sub == null)
                {
                    continue;
                }
                if (sub.CategoryKey == ArchiveCategoryKeys.Location
                    || sub.CategoryKey == ArchiveCategoryKeys.Battle)
                {
                    if (!string.IsNullOrEmpty(sub.LabelSnapshot))
                    {
                        return sub.LabelSnapshot;
                    }
                    ArchiveObject obj = service != null ? service.GetObject(sub.StableId) : null;
                    if (obj != null && !string.IsNullOrEmpty(obj.LabelSnapshot))
                    {
                        return obj.LabelSnapshot;
                    }
                    return sub.StableId;
                }
            }
            return null;
        }

        /// <summary>
        /// 工坊署名链 (maker chain): the crafter's later fate. Reads the maker's
        /// PawnRecord: if the maker later died and the death event lists this thing
        /// as a subject edge, show the "died by own creation" double narrative.
        /// </summary>
        private static MakerChainView BuildMakerChain(
            IArchiveService service, ThingObject thing, ThingOriginView origin)
        {
            MakerChainView view = new MakerChainView { IsEmpty = true };
            if (service == null || thing == null || origin == null || origin.IsEmpty)
            {
                return view;
            }
            string makerId = origin.FromStableId;
            if (string.IsNullOrEmpty(makerId))
            {
                return view;
            }
            ArchiveObject maker = service.GetObject(makerId);
            if (maker is PawnObject pawn)
            {
                view.MakerStableId = makerId;
                view.MakerText = string.IsNullOrEmpty(pawn.LabelSnapshot)
                    ? pawn.StableId
                    : pawn.LabelSnapshot;
                view.IsEmpty = false;
                if (pawn.IsArchived)
                {
                    // Died after crafting — check whether this thing dealt the blow.
                    IReadOnlyList<ChronicleEvent> makerEvents = service.GetEventsFor(makerId);
                    if (makerEvents != null)
                    {
                        for (int i = 0; i < makerEvents.Count; i++)
                        {
                            ChronicleEvent ev = makerEvents[i];
                            if (ev == null || ev.TypeKey != ChronicleEventType.Death) continue;
                            if (HasSubjectThing(ev, thing.StableId))
                            {
                                view.MakerDiedByOwn = true;
                                break;
                            }
                        }
                    }
                }
            }
            return view;
        }

        /// <summary>
        /// 同袍共用网络 (co-use): colonists who used this equipment in parallel
        /// with the current holder. Computed from HolderRecords tenure overlap —
        /// two records overlap when the later start < the earlier end. The window
        /// only renders the derived rows (share % of the longest sharer).
        /// </summary>
        private static CoUseView BuildCoUse(
            IArchiveService service, ThingObject thing, IReadOnlyList<ChronicleEvent> events)
        {
            CoUseView view = new CoUseView();
            if (service == null || thing == null
                || thing.HolderRecords == null || thing.HolderRecords.Count < 2)
            {
                view.IsEmpty = true;
                return view;
            }
            List<HolderRecord> records = new List<HolderRecord>();
            for (int i = 0; i < thing.HolderRecords.Count; i++)
            {
                HolderRecord rec = thing.HolderRecords[i];
                if (rec == null || string.IsNullOrEmpty(rec.StableId)) continue;
                records.Add(rec);
            }
            if (records.Count < 2)
            {
                view.IsEmpty = true;
                return view;
            }
            // Build tenures with exclusive end ticks (same derivation as BuildLegacy).
            long now = Find.TickManager.TicksGame;
            List<long> ends = new List<long>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                ends.Add(i + 1 < records.Count
                    ? records[i + 1].StartTick
                    : now);
            }
            string currentHolderId = thing.CurrentHolderId;
            Dictionary<string, CoUseRowView> byPawn = new Dictionary<string, CoUseRowView>();
            for (int i = 0; i < records.Count; i++)
            {
                HolderRecord rec = records[i];
                // Exclude the current sole holder: co-use is about parallel sharers.
                if (!string.IsNullOrEmpty(currentHolderId) && rec.StableId == currentHolderId) continue;
                for (int j = i + 1; j < records.Count; j++)
                {
                    HolderRecord other = records[j];
                    if (other == null) continue;
                    long overlapStart = System.Math.Max(rec.StartTick, other.StartTick);
                    long overlapEnd = System.Math.Min(ends[i], ends[j]);
                    if (overlapEnd > overlapStart)
                    {
                        long overlapTicks = overlapEnd - overlapStart;
                        int days = (int)GenDate.TicksToDays((int)overlapTicks);
                        if (days <= 0) continue;
                        if (!byPawn.TryGetValue(other.StableId, out CoUseRowView row))
                        {
                            row = new CoUseRowView
                            {
                                PawnStableId = other.StableId,
                                PawnText = ResolvePawnLabel(service, other),
                                SharedDays = 0
                            };
                            byPawn[other.StableId] = row;
                        }
                        row.SharedDays += days;
                    }
                }
            }
            if (byPawn.Count == 0)
            {
                view.IsEmpty = true;
                return view;
            }
            List<CoUseRowView> rows = new List<CoUseRowView>(byPawn.Values);
            rows.Sort((a, b) => b.SharedDays.CompareTo(a.SharedDays));
            int maxDays = rows.Count > 0 ? rows[0].SharedDays : 0;
            for (int r = 0; r < rows.Count; r++)
            {
                rows[r].SharePercent = maxDays > 0 ? (int)(rows[r].SharedDays * 100f / maxDays) : 0;
            }
            view.Rows = rows;
            view.IsEmpty = false;
            return view;
        }

        /// <summary>
        /// 退役仪式 (decommission): the thing's death record. Reads the persisted
        /// DecommissionSnapshot on the domain object; no record → empty state.
        /// </summary>
        private static DecommissionView BuildDecommission(
            IArchiveService service, ThingObject thing, IReadOnlyList<ChronicleEvent> events)
        {
            DecommissionView view = new DecommissionView { HasRecord = false };
            if (thing == null || thing.Decommission == null || thing.Decommission.IsEmpty)
            {
                return view;
            }
            Domain.DecommissionRecord rec = thing.Decommission;
            view.HasRecord = true;
            view.LastHolderStableId = rec.LastHolderStableId;
            view.LastHolderText = !string.IsNullOrEmpty(rec.LastHolderLabel)
                ? rec.LastHolderLabel
                : (!string.IsNullOrEmpty(rec.LastHolderStableId)
                    ? ResolveObjectLabel(service, rec.LastHolderStableId)
                    : "—");
            // LastPlaceLabel stores a language-independent biome defName (older saves
            // may hold a localized label); resolve to a label, falling back to the raw
            // string so both old and new records display.
            view.LastPlaceText = !string.IsNullOrEmpty(rec.LastPlaceLabel)
                ? ResolveBiomeLabelOrRaw(rec.LastPlaceLabel)
                : "—";
            view.ServiceDays = rec.ServiceDays;
            view.LastBattleText = !string.IsNullOrEmpty(rec.LastBattleLabel)
                ? rec.LastBattleLabel
                : "—";
            view.DateText = rec.Tick > 0L
                ? GenDate.DateReadoutStringAt(rec.Tick, Vector2.zero)
                : "PersonalChronicle.UI.UnknownDate".Translate().ToString();
            return view;
        }

        private static bool HasSubjectThing(ChronicleEvent ev, string thingStableId)
        {
            if (ev == null || string.IsNullOrEmpty(thingStableId) || ev.Subjects == null)
            {
                return false;
            }
            for (int i = 0; i < ev.Subjects.Count; i++)
            {
                ObjectRef sub = ev.Subjects[i];
                if (sub != null && sub.CategoryKey == ArchiveCategoryKeys.Thing
                    && sub.StableId == thingStableId)
                {
                    return true;
                }
            }
            return false;
        }

        private static string ResolveObjectLabel(IArchiveService service, ObjectRef objRef)
        {
            if (objRef == null) return "—";
            return ResolveObjectLabel(service, objRef.StableId);
        }

        private static string ResolveObjectLabel(IArchiveService service, string stableId)
        {
            if (string.IsNullOrEmpty(stableId)) return "—";
            if (service != null)
            {
                ArchiveObject obj = service.GetObject(stableId);
                if (obj != null)
                {
                    if (!string.IsNullOrEmpty(obj.LabelSnapshot)) return obj.LabelSnapshot;
                    if (obj is ThingObject t) return ThingDefLabelLocal(t.ThingDefName);
                    if (obj is PawnObject p) return string.IsNullOrEmpty(p.LabelSnapshot) ? p.StableId : p.LabelSnapshot;
                    return obj.StableId;
                }
            }
            return stableId;
        }

        private static string ResolvePawnLabel(IArchiveService service, HolderRecord rec)
        {
            if (rec == null) return "—";
            if (!string.IsNullOrEmpty(rec.LabelSnapshot)) return rec.LabelSnapshot;
            if (service != null && !string.IsNullOrEmpty(rec.StableId))
            {
                ArchiveObject obj = service.GetObject(rec.StableId);
                if (obj is PawnObject pawn)
                {
                    return string.IsNullOrEmpty(pawn.LabelSnapshot) ? pawn.StableId : pawn.LabelSnapshot;
                }
            }
            return rec.StableId ?? "—";
        }

        private static int CountKillsInWindow(List<ChronicleEvent> killEvents, long start, long end)
        {
            if (killEvents == null || killEvents.Count == 0) return 0;
            int count = 0;
            for (int i = 0; i < killEvents.Count; i++)
            {
                ChronicleEvent ev = killEvents[i];
                if (ev == null) continue;
                // v4.9.1: use half-open [start, end) so the boundary tick where the
                // next holder takes over (end == next StartTick) is attributed to the
                // new holder only, not double-counted on both sides. Previously a kill
                // at the exact transfer tick was tallied twice (totalKills inflated
                // and the previous holder's KillCount wrongly ticked up).
                if (start >= 0L && ev.Tick < start) continue;
                if (end >= 0L && ev.Tick >= end) continue;
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
            return SpanText.Format(end - start);
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
                        RelationDefName = rel.def != null ? rel.def.defName : null,
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
                        RelationDefName = rel.RelationDefName,
                        RelationLabel = RelationLabelFor(def, otherLive, rel.RelationDefName),
                        StatusLabel = status,
                        IsLive = false
                    });
                }
            }

            // Stable presentation order: partners → blood kin → friends → rivals →
            // ended ties. Sorting belongs to the Read Model, never to the window.
            // Labels are resolved once here rather than per comparison, since a
            // sort evaluates the comparator O(n log n) times.
            string endedLabel = "PersonalChronicle.UI.RelEnded".Translate().ToString();
            string friendLabel = FriendLabel();
            string rivalLabel = RivalLabel();
            rows.Sort((a, b) =>
            {
                int ra = RelationSortRank(a, endedLabel, friendLabel, rivalLabel);
                int rb = RelationSortRank(b, endedLabel, friendLabel, rivalLabel);
                if (ra != rb)
                {
                    return ra.CompareTo(rb);
                }
                return string.Compare(a?.OtherLabel ?? string.Empty,
                    b?.OtherLabel ?? string.Empty, System.StringComparison.CurrentCulture);
            });

            return rows;
        }

        /// <summary>
        /// Presentation rank for the social list. Ended ties always sink to the
        /// bottom so the current social circle reads first.
        /// </summary>
        private static int RelationSortRank(
            RelationView row, string endedLabel, string friendLabel, string rivalLabel)
        {
            if (row == null)
            {
                return 99;
            }
            if (row.StatusLabel == endedLabel)
            {
                return 50;
            }
            string label = row.RelationLabel ?? string.Empty;
            if (label == friendLabel)
            {
                return 20;
            }
            if (label == rivalLabel)
            {
                return 30;
            }
            return row.IsLive ? 0 : 10;
        }

        private static string FriendLabel()
        {
            return "PersonalChronicle.Relation.Friend".Translate().ToString();
        }

        private static string RivalLabel()
        {
            return "PersonalChronicle.Relation.Rival".Translate().ToString();
        }

        private static string MakeRelationKey(string relationDefName, string otherStableId)
        {
            return (relationDefName ?? string.Empty) + "::" + (otherStableId ?? string.Empty);
        }

        private static string RelationLabelFor(PawnRelationDef def, Pawn otherPawn)
        {
            return RelationLabelFor(def, otherPawn, null);
        }

        /// <summary>
        /// Resolves a display label. <paramref name="relationDefName"/> lets
        /// synthesized opinion ties (friend/rival) resolve to a translated label
        /// even though they have no backing PawnRelationDef.
        /// </summary>
        private static string RelationLabelFor(PawnRelationDef def, Pawn otherPawn, string relationDefName)
        {
            if (def == null)
            {
                if (relationDefName == SocialRelationFilter.FriendRelationKey)
                {
                    return FriendLabel();
                }
                if (relationDefName == SocialRelationFilter.RivalRelationKey)
                {
                    return RivalLabel();
                }
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

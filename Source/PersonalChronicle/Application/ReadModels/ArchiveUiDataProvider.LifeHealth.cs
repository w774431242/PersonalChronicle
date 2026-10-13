using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application;
using PersonalChronicle.Api;
using PersonalChronicle.Api.DomainProviders;
using PersonalChronicle.Domain;
using PersonalChronicle.Data;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using Verse;
using UnityEngine;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// Partial of <see cref="ArchiveUiDataProvider"/> — 生命/健康与足迹 Read Model 构建。
    /// </summary>
    /// <remarks>
    /// · 职责：BuildFootprint（足迹台账）、BuildLocation（地点详情）、BuildHealth（健康状态）三个投影。
    /// · 调用方：ArchiveUiDataProvider（详情快照装配链路）。
    /// · 禁止：本文件只做投影；健康阈值与死亡率口径归 Domain 层，不在此处派生。
    /// · 拆分：自 Detail.cs 物理拆分（机械切片，方法体原样）；private 成员跨 partial 文件可见，编译安全。
    /// </remarks>
    public sealed partial class ArchiveUiDataProvider : IArchiveUiDataProvider
    {
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
                activeDate = FormatDateLocal(pawn.JoinTick) + " → " + FormatDateLocal(activeEnd)
                    + " (" + SpanText.Format(activeEnd - pawn.JoinTick) + ")";
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
                bool isWorld = v.PlaceKind == PlaceVisitKeys.KindCaravan
                    || (v.PlaceKey != null && v.PlaceKey.StartsWith(PlaceVisitKeys.TileKeyPrefix, System.StringComparison.Ordinal));
                if (isWorld) expeditions++;
                long enter = v.EnterTick > 0L ? v.EnterTick : -1L;
                long leave = v.IsOpen ? now : (v.LeaveTick > 0L ? v.LeaveTick : -1L);
                long dwellTicks = (enter > 0L && leave > 0L && leave >= enter) ? (leave - enter) : -1L;
                long days = dwellTicks >= 0L ? (long)RimWorld.GenDate.TicksToDays((int)dwellTicks) : -1L;
                if (days > homeDays) { homeDays = days; homeIdx = stays.Count; }
                stays.Add(new FootstepView
                {
                    PlaceText = PlaceTextLocal(v),
                    IsWorldTile = isWorld,
                    DwellText = dwellTicks >= 0L ? SpanText.Format(dwellTicks) : "PersonalChronicle.UI.UnknownDate".Translate().ToString(),
                    DwellTicks = dwellTicks,
                    IsHome = false
                });
            }

            // Longest dwell first (raw tick span, never string parsing).
            stays.Sort((a, b) => b.DwellTicks.CompareTo(a.DwellTicks));
            if (homeIdx >= 0 && homeIdx < stays.Count) stays[homeIdx].IsHome = true;

            led.PlaceCount = pawn.PlaceHistory.Count;
            led.HomePlaceText = homeIdx >= 0 ? stays[homeIdx].PlaceText : null;
            led.HomeDays = homeDays >= 0 ? (int)homeDays : 0;
            led.ExpeditionCount = expeditions;
            led.Stays = stays;
            return led;
        }

        // ---- v4.13 location atlas derivation (Read Model only) ----

        /// <summary>
        /// Derives the location detail view (identity / ownership / geography /
        /// lifecycle / commerce) from a LocationObject. Pure data-key derivation —
        /// the window owns translation/formatting. Empty-safe.
        /// </summary>
        private static LocationDetailView BuildLocation(LocationObject loc)
        {
            LocationDetailView view = new LocationDetailView();
            if (loc == null)
            {
                return view;
            }
            view.EstablishedTick = loc.EstablishedTick;
            view.IsActive = loc.DeinitTick == -1L;
            view.DeinitReasonKey = view.IsActive ? null : loc.DeinitReason;
            view.FactionDefName = loc.FactionDefName;
            view.BiomeDefName = loc.MapDefName;

            // Kind key: player home / faction settlement / quest site / unknown.
            view.KindKey = ResolveLocationKindKey(loc);

            // Hill key.
            if (string.IsNullOrEmpty(loc.Hilliness))
            {
                view.HillKey = null;
            }
            else if (loc.Hilliness == "Flat") view.HillKey = "flat";
            else if (loc.Hilliness == "Hilly") view.HillKey = "hilly";
            else if (loc.Hilliness == "Mountainous") view.HillKey = "mountain";
            else if (loc.Hilliness == "Impassable") view.HillKey = "impassable";
            else view.HillKey = null;

            view.IsCoastal = loc.IsCoastal;
            view.IsPolluted = loc.Pollution > 0.001f;
            view.AvgTempC = loc.AvgTempC;

            // Commerce.
            view.CanTrade = loc.CanTrade;
            view.PermitDefName = loc.PermitRequiredDefName;
            if (loc.TradeKindKeys != null)
            {
                view.TradeKindKeys = loc.TradeKindKeys;
            }
            return view;
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
                string label = ResolveKeyedOrDash(f.LabelKey);
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
                    : (e.TagKey.CanTranslate() ? e.TagKey.Translate().ToString() : "");
                string dateText = e.RawTick > 0L
                    ? GenDate.DateReadoutStringAt(e.RawTick, Vector2.zero)
                    : "";
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
                Events = eventViews,
                // v4.14: data-driven one-line verdict (健康残值结论). Thresholds
                // mirror the impaired/prime semantics of the evaluator — no UI
                // hardcoding, translation keys carry the text.
                VerdictText = BuildHealthVerdict(r.HealthScore, r.IsPrime, r.IsImpaired)
            };
        }

        /// <summary>v4.14: health-residual verdict line (data-driven, localized).</summary>
        private static string BuildHealthVerdict(float score, bool isPrime, bool isImpaired)
        {
            if (isImpaired)
            {
                return HealthValuationKeys.VerdictImpaired.Translate().ToString();
            }
            if (isPrime)
            {
                return HealthValuationKeys.VerdictPrime.Translate().ToString();
            }
            if (score >= 40f)
            {
                return HealthValuationKeys.VerdictFair.Translate().ToString();
            }
            return HealthValuationKeys.VerdictDepleted.Translate().ToString();
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
                string label = ResolveKeyedOrDash(f.LabelKey);
                list.Add(new HealthFactorView
                {
                    IsPositive = f.IsPositive,
                    LabelText = label,
                    Impact = Mathf.RoundToInt(f.Impact)
                });
            }
            return list;
        }

        private static string ResolveKeyedOrDash(string key)
        {
            if (string.IsNullOrEmpty(key) || !key.CanTranslate())
            {
                return "—";
            }
            string translated = key.Translate().ToString();
            return string.IsNullOrEmpty(translated) ? "—" : translated;
        }

        private static string ResolveEventDescription(HealthDepreciationEvent e)
        {
            // Keyed override only when the key actually exists. Comparing
            // Translate() to the raw key is a false positive under DevMode
            // pseudo-translation (grave accents on missing keys).
            if (!string.IsNullOrEmpty(e.LabelKey) && e.LabelKey.CanTranslate())
            {
                string translated = e.LabelKey.Translate().ToString();
                if (!string.IsNullOrWhiteSpace(translated))
                {
                    return translated;
                }
            }
            if (!string.IsNullOrEmpty(e.RawDefName))
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(e.RawDefName);
                if (def != null)
                {
                    string cap = def.LabelCap.ToString();
                    if (!string.IsNullOrEmpty(cap))
                    {
                        return cap;
                    }
                }
                return SanitizeDefNameForDisplay(e.RawDefName);
            }
            return "—";
        }
    }
}
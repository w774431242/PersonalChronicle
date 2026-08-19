using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Api;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// Partial of <see cref="ArchiveService"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveService : IArchiveService, IWorkIntensityService, IWorkTimeCaptureService, IArchiveQueryService, IArchiveEventSink
    {

        public BattleObject GetActiveBattle()
        {
            IReadOnlyList<ArchiveObject> battles = GetObjectsOfCategory(ArchiveCategoryKeys.Battle);
            BattleObject best = null;
            long bestTick = long.MinValue;
            for (int i = 0; i < battles.Count; i++)
            {
                BattleObject battle = battles[i] as BattleObject;
                if (battle == null || battle.EndTick != -1L)
                {
                    continue;
                }
                long lastTick = LatestBattleTick(battle);
                if (lastTick > bestTick)
                {
                    bestTick = lastTick;
                    best = battle;
                }
            }
            return best;
        }

        public IReadOnlyList<ChronicleEvent> GetProductionEvents(string stableId)
        {
            IReadOnlyList<ChronicleEvent> all = GetEventsFor(stableId);
            List<ChronicleEvent> result = new List<ChronicleEvent>();
            for (int i = 0; i < all.Count; i++)
            {
                ChronicleEvent ev = all[i];
                if (ev == null || string.IsNullOrEmpty(ev.TypeKey))
                {
                    continue;
                }
                ChronicleEventDef def = DefDatabase<ChronicleEventDef>.GetNamedSilentFail(ev.TypeKey);
                if (def != null && (def.kind == ChronicleEventKind.Craft || def.kind == ChronicleEventKind.Built))
                {
                    result.Add(ev);
                }
            }
            return result;
        }

        private static int WorldCaravanTile(Pawn pawn)
        {
            if (pawn == null || Find.World == null || Find.WorldObjects == null)
            {
                return -1;
            }
            List<Caravan> caravans = Find.WorldObjects.Caravans;
            for (int i = 0; i < caravans.Count; i++)
            {
                Caravan caravan = caravans[i];
                if (caravan == null)
                {
                    continue;
                }
                List<Pawn> members = caravan.PawnsListForReading;
                for (int j = 0; j < members.Count; j++)
                {
                    if (members[j] == pawn)
                    {
                        return caravan.Tile;
                    }
                }
            }
            return -1;
        }

        private static Thing FindLiveThing(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
            {
                return null;
            }
            int sep = stableId.IndexOf(':');
            if (sep <= 0 || sep >= stableId.Length - 1)
            {
                return null;
            }
            string defName = stableId.Substring(0, sep);
            if (!int.TryParse(stableId.Substring(sep + 1), out int thingId))
            {
                return null;
            }
            List<Map> maps = Find.Maps;
            if (maps == null)
            {
                return null;
            }
            for (int i = 0; i < maps.Count; i++)
            {
                Map map = maps[i];
                if (map == null || map.listerThings == null)
                {
                    continue;
                }
                List<Thing> all = map.listerThings.AllThings;
                for (int j = 0; j < all.Count; j++)
                {
                    Thing t = all[j];
                    if (t == null || t.Destroyed || t.def == null)
                    {
                        continue;
                    }
                    if (t.def.defName == defName && t.thingIDNumber == thingId)
                    {
                        return t;
                    }
                }
            }
            return null;
        }

        private long LatestBattleTick(BattleObject battle)
        {
            if (battle == null || string.IsNullOrEmpty(battle.StableId))
            {
                return -1L;
            }
            IReadOnlyList<ChronicleEvent> events = GetEventsFor(battle.StableId);
            long maxTick = -1L;
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev != null && ev.Tick > maxTick)
                {
                    maxTick = ev.Tick;
                }
            }
            return maxTick;
        }

        public void OnPawnDied(Pawn pawn, string deathCauseKey)
        {
            OnPawnDied(pawn, deathCauseKey, null);
        }

        public void OnPawnDied(Pawn pawn, string deathCauseKey, Thing weapon = null, Dictionary<string, string> extraParams = null)
        {
            OnPawnDied(pawn, deathCauseKey, weapon, extraParams, null);
        }

        public void OnPawnDied(Pawn pawn, string deathCauseKey, Thing weapon, Dictionary<string, string> extraParams, Pawn killer)
        {
            if (!IsRecordingEnabled() || pawn == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                string stableId = pawn.GetUniqueLoadID();
                string labelSnapshot = pawn.LabelShort;
                if (!component.ArchivePawn(stableId, deathCauseKey, pawn))
                {
                    return;
                }
                ChronicleEvent ev = BuildPawnEvent(stableId, labelSnapshot, ChronicleEventType.Death);
                if (extraParams != null && extraParams.Count > 0)
                {
                    foreach (KeyValuePair<string, string> pair in extraParams)
                    {
                        ev.Params[pair.Key] = pair.Value;
                    }
                }
                if (killer != null && ChronicleColonistScanner.TryClassifyCurrent(killer, out _))
                {
                    EnsurePawnArchivedForCapture(component, killer);
                }
                AttachCombatSubjects(component, ev, weapon, killer, pawn);
                AddEvent(component, stableId, ev);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record pawn death for " + (pawn != null ? pawn.LabelShort : "null") + ": " + ex.Message);
            }
        }

        public void OnKillRecorded(Pawn killer, Pawn victim, Thing weapon = null, List<Pawn> assistLookup = null)
        {
            // killer may be null when the DamageInfo instigator is unresolvable
            // (melee-forwarded / environment kills). We still record the kill so the
            // combat log is never empty; it attributes to an "unknown killer" bucket.
            if (!IsRecordingEnabled() || victim == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                string victimId = victim.GetUniqueLoadID();
                // killer 可能为 null（环境致死 / 近战转发 / 敌方互相残杀且凶手无法解析），
                // 属于合法的「未知凶手」路径，绝不能因为 TryClassifyCurrent(null) 而 NRE 并 return。
                string killerId = ChronicleEventParams.UnknownKillerId;
                if (killer != null)
                {
                    killerId = killer.GetUniqueLoadID();
                    if (!ChronicleColonistScanner.TryClassifyCurrent(killer, out _))
                    {
                        // 非本殖民地人口（敌方/野生动物/奴隶等）→ 归入 UnknownKiller 聚合桶
                        killerId = ChronicleEventParams.UnknownKillerId;
                    }
                }
                if (string.IsNullOrEmpty(victimId) || string.IsNullOrEmpty(killerId))
                {
                    return;
                }
                // 跨 bucket 幂等：同一受害者（victimStableId）若已存在任意击杀记录
                // （无论当时记在哪个 killer 桶），不再重复写入，避免极端场景下
                // instigator==null 同时命中 OnPawnDied 与 OnKillRecorded 产生双份 Death。
                if (HasRecordedDeathForVictim(component, victimId))
                {
                    return;
                }
                // 协助者：造成最多伤害、但非补刀者的 chronicle 殖民者（如 A 削 80% 血、B 抢补刀）。
                Pawn assist = assistLookup != null && assistLookup.Count > 0 ? assistLookup[0] : null;
                if (assist != null && killer != null && assist.GetUniqueLoadID() == killer.GetUniqueLoadID())
                {
                    // 主伤害者就是补刀者 → 取次高伤害者作协助
                    assist = assistLookup != null && assistLookup.Count > 1 ? assistLookup[1] : null;
                }
                if (assist != null && killer == null)
                {
                    // 凶手未知时，主伤害者即升格为击杀者，不再单列协助
                    killerId = assist.GetUniqueLoadID();
                    killer = assist;
                    assist = null;
                }
                EnsurePawnArchivedForCapture(component, killer);
                if (HasRecordedExternalKill(component, killerId, victimId))
                {
                    return;
                }
                string victimLabel = victim.LabelShort;
                ChronicleEvent ev = BuildPawnEvent(victimId, victimLabel, ChronicleEventType.Death);
                ev.Params[ChronicleEventParams.Killer] = killer != null ? killer.LabelShort : ChronicleEventParams.UnknownKillerLabel;
                ev.Params[ChronicleEventParams.Victim] = victimLabel;
                ev.Params[ChronicleEventParams.VictimStableId] = victimId;
                ev.Params[ChronicleEventParams.CombatRole] = ChronicleEventParams.CombatRoleKill;

                // v4.3: snapshot victim faction/kind/category for faction-codex aggregation.
                // External victims are never archived, so this is the only point these are available.
                string victimFactionDef = victim.Faction != null && victim.Faction.def != null
                    ? victim.Faction.def.defName
                    : null;
                ev.Params[ChronicleEventParams.VictimFactionDefName] = victimFactionDef;
                ev.Params[ChronicleEventParams.VictimFactionLabel] = victim.Faction != null ? victim.Faction.Name : null;
                ev.Params[ChronicleEventParams.VictimKindDefName] = victim.kindDef != null ? victim.kindDef.defName : null;
                string victimCategory = ChronicleEventParams.VictimCategoryHumanlike;
                if (victim.RaceProps != null && victim.RaceProps.IsMechanoid)
                {
                    victimCategory = ChronicleEventParams.VictimCategoryMechanoid;
                }
                else if (victim.RaceProps != null && victim.RaceProps.Animal)
                {
                    victimCategory = ChronicleEventParams.VictimCategoryAnimal;
                }
                ev.Params[ChronicleEventParams.VictimCategory] = victimCategory;

                if (assist != null)
                {
                    ev.Params[ChronicleEventParams.Assist] = assist.LabelShort;
                    EnsurePawnArchivedForCapture(component, assist);
                }
                AttachCombatSubjects(component, ev, weapon, killer, victim);
                // Cap against the killer's event budget (their combat log).
                AddEvent(component, killerId, ev);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record kill by " + (killer != null ? killer.LabelShort : "null") + ": " + ex.Message);
            }
        }

        public void OnKillRecorded(Pawn killer, Pawn victim, Thing weapon, List<Pawn> assistLookup, float finishingDamage, bool isMelee)
        {
            // 先走基础击杀记录（内部已做幂等 / 凶手解析 / 未知凶手桶判断）。
            OnKillRecorded(killer, victim, weapon, assistLookup);
            if (killer == null)
            {
                return;
            }
            if (!ChronicleColonistScanner.TryClassifyCurrent(killer, out _))
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                string killerId = killer.GetUniqueLoadID();
                if (string.IsNullOrEmpty(killerId))
                {
                    return;
                }
                PawnObject record = component.GetObject(killerId) as PawnObject;
                if (record == null)
                {
                    return;
                }
                if (finishingDamage > 0f)
                {
                    record.DamageDealtTotal += finishingDamage;
                }
                if (isMelee)
                {
                    record.MeleeKills++;
                }
                else
                {
                    record.RangedKills++;
                }
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to accumulate combat dims for " + killer.LabelShort + ": " + ex.Message);
            }
        }

        private void AttachCombatSubjects(ChronicleGameComponent component, ChronicleEvent ev, Thing weapon, Pawn killer, Pawn victim)
        {
            if (ev == null)
            {
                return;
            }
            if (ev.Subjects == null)
            {
                ev.Subjects = new List<ObjectRef>();
            }
            if (weapon != null && weapon.def != null)
            {
                string weaponId = weapon.def.defName + ":" + weapon.thingIDNumber;
                RegisterThingObject(component, weapon, killer);
                ev.Subjects.Add(new ObjectRef(ArchiveCategoryKeys.Thing, weaponId, null));
                NoteWeaponHolder(component, weaponId, killer);
            }
            if (killer != null)
            {
                string killerId = killer.GetUniqueLoadID();
                if (!SubjectContains(ev, killerId))
                {
                    ev.Subjects.Add(ObjectRef.ForPawn(killerId, killer.LabelShort));
                }
                if (ev.Params != null && !ev.Params.ContainsKey(ChronicleEventParams.Killer))
                {
                    ev.Params[ChronicleEventParams.Killer] = killer.LabelShort;
                }
            }
            BattleObject activeBattle = GetActiveBattle();
            if (activeBattle != null && !string.IsNullOrEmpty(activeBattle.StableId))
            {
                if (!SubjectContains(ev, activeBattle.StableId))
                {
                    ev.Subjects.Add(new ObjectRef(ArchiveCategoryKeys.Battle, activeBattle.StableId, null));
                }
                AddBattleParticipant(activeBattle, victim);
                AddBattleParticipant(activeBattle, killer);
            }
        }

        private static bool SubjectContains(ChronicleEvent ev, string stableId)
        {
            if (ev == null || ev.Subjects == null || string.IsNullOrEmpty(stableId))
            {
                return false;
            }
            for (int i = 0; i < ev.Subjects.Count; i++)
            {
                ObjectRef s = ev.Subjects[i];
                if (s != null && s.StableId == stableId)
                {
                    return true;
                }
            }
            return false;
        }

        private static string BattleThreatKey(IncidentDef incidentDef)
        {
            if (incidentDef == null || incidentDef.category == null)
            {
                return null;
            }
            if (incidentDef.category == IncidentCategoryDefOf.ThreatBig)
            {
                return "ThreatBig";
            }
            if (incidentDef.category == IncidentCategoryDefOf.ThreatSmall)
            {
                return "ThreatSmall";
            }
            return null;
        }

        private static void AddBattleParticipant(BattleObject battle, Pawn pawn)
        {
            if (battle == null || pawn == null)
            {
                return;
            }
            if (battle.ParticipantIds == null)
            {
                battle.ParticipantIds = new List<string>();
            }
            string id = pawn.GetUniqueLoadID();
            if (string.IsNullOrEmpty(id) || battle.ParticipantIds.Contains(id))
            {
                return;
            }
            battle.ParticipantIds.Add(id);
        }

        private static void NoteWeaponHolder(ChronicleGameComponent component, string weaponStableId, Pawn holder)
        {
            if (component == null || string.IsNullOrEmpty(weaponStableId) || holder == null)
            {
                return;
            }
            ThingObject thing = component.GetObject(weaponStableId) as ThingObject;
            if (thing == null)
            {
                return;
            }
            string holderId = holder.GetUniqueLoadID();
            thing.CurrentHolderId = holderId;
            if (thing.HolderHistory == null)
            {
                thing.HolderHistory = new List<ObjectRef>();
            }
            if (thing.HolderRecords == null)
            {
                thing.HolderRecords = new List<HolderRecord>();
            }
            // Append only when holder changed (avoid spam on multi-kill same holder).
            if (thing.HolderHistory.Count > 0)
            {
                ObjectRef last = thing.HolderHistory[thing.HolderHistory.Count - 1];
                if (last != null && last.StableId == holderId)
                {
                    return;
                }
            }
            thing.HolderHistory.Add(ObjectRef.ForPawn(holderId, holder.LabelShort));
            // Legacy chain (传承): ownership transfer record. Capture cannot
            // reliably distinguish a true ownership transfer from a borrow/lend
            // (RimWorld pawns carry equipment without a loan flag), so every
            // observed hold is recorded as "own" — context-rich loans are an
            // authoring concern of the UI, not of the capture layer. The first
            // record (craft holder) is marked IsFirst.
            bool isFirst = thing.HolderRecords.Count == 0;
            thing.HolderRecords.Add(new HolderRecord(
                holderId,
                holder.LabelShort,
                Find.TickManager.TicksGame,
                isFirst,
                HolderRecord.HolderKindOwn));
            component.MarkChanged();
        }

        public void OnThingCrafted(Thing product, Pawn worker)
        {
            if (!IsRecordingEnabled() || product == null || product.def == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                bool workerIsCurrent = worker != null
                    && ChronicleColonistScanner.TryClassifyCurrent(worker, out _);
                if (workerIsCurrent)
                {
                    int quantity = Math.Max(1, product.stackCount);
                    float unitValue = product.MarketValue;
                    if (float.IsNaN(unitValue) || float.IsInfinity(unitValue) || unitValue < 0f)
                    {
                        unitValue = 0f;
                    }
                    EnsurePawnArchivedForCapture(component, worker);
                    component.AddProduction(
                        worker.GetUniqueLoadID(),
                        product.def.defName,
                        quantity,
                        unitValue * quantity,
                        Find.TickManager.TicksGame);
                }
                string stableId = product.def.defName + ":" + product.thingIDNumber;
                // v4.6.5: only equipment (weapons + apparel) enters the archive
                // object graph and gets a Crafted event; raw materials / food
                // stay as pure production stats above.
                if (IsEquipable(product))
                {
                    RegisterThingObject(component, product, worker);
                    ChronicleEvent ev = BuildThingEvent(stableId, ChronicleEventType.Crafted);
                    if (workerIsCurrent)
                    {
                        AddPawnSubject(ev, worker);
                    }
                    string eventOwnerId = workerIsCurrent
                        ? worker.GetUniqueLoadID()
                        : stableId;
                    AddEvent(component, eventOwnerId, ev);
                }
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record crafted thing " + (product != null && product.def != null ? product.def.defName : "null") + ": " + ex.Message);
            }
        }

        public void OnThingDestroyed(Thing thing, Pawn lastHolder = null)
        {
            if (!IsRecordingEnabled() || thing == null || thing.def == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                string stableId = thing.def.defName + ":" + thing.thingIDNumber;
                ThingObject thingObj = component.GetObject(stableId) as ThingObject;
                if (thingObj == null || thingObj.Decommission != null)
                {
                    // Not archived (never a chronicle thing) or already retired.
                    return;
                }
                DecommissionRecord rec = new DecommissionRecord
                {
                    Tick = Find.TickManager.TicksGame,
                    LastPlaceLabel = PlaceLabelForDestroyedThing(thing)
                };
                if (lastHolder != null)
                {
                    rec.LastHolderStableId = lastHolder.GetUniqueLoadID();
                    rec.LastHolderLabel = lastHolder.LabelShort;
                }
                else if (!string.IsNullOrEmpty(thingObj.CurrentHolderId))
                {
                    ArchiveObject cur = component.GetObject(thingObj.CurrentHolderId);
                    if (cur != null)
                    {
                        rec.LastHolderStableId = cur.StableId;
                        rec.LastHolderLabel = !string.IsNullOrEmpty(cur.LabelSnapshot)
                            ? cur.LabelSnapshot
                            : cur.StableId;
                    }
                }
                // Service days: derived from the tenure span (first record start →
                // now) so the number stays consistent with the legacy chain.
                if (thingObj.HolderRecords != null && thingObj.HolderRecords.Count > 0)
                {
                    long start = thingObj.HolderRecords[0].StartTick;
                    if (start > 0L)
                    {
                        rec.ServiceDays = Math.Max(0,
                            (int)GenDate.TicksToDays((int)(Find.TickManager.TicksGame - start)));
                    }
                }
                thingObj.Decommission = rec;
                component.MarkChanged();
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record decommission for "
                    + (thing != null && thing.def != null ? thing.def.defName : "null") + ": " + ex.Message);
            }
        }

        private static string PlaceLabelForDestroyedThing(Thing thing)
        {
            if (thing == null) return "—";
            Map map = thing.Map;
            if (map != null && map.Biome != null && !string.IsNullOrEmpty(map.Biome.defName))
            {
                return map.Biome.defName;
            }
            return "—";
        }

        public void OnThingBuilt(ThingDef builtDef, string builtStableId, Pawn worker)
        {
            if (!IsRecordingEnabled() || builtDef == null || string.IsNullOrEmpty(builtStableId))
            {
                return;
            }
            // v4.6.5: buildings are not equipment — excluded from the archive
            // object graph (the "Thing" category is scoped to weapons + apparel).
            if (!IsEquipableDef(builtDef))
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                if (component.GetObject(builtStableId) == null)
                {
                    component.AddObject(new ThingObject
                    {
                        StableId = builtStableId,
                        ThingDefName = builtDef.defName,
                        WeakId = builtStableId
                    });
                }
                bool workerIsCurrent = worker != null
                    && ChronicleColonistScanner.TryClassifyCurrent(worker, out _);
                if (workerIsCurrent)
                {
                    EnsurePawnArchivedForCapture(component, worker);
                    component.AddProduction(
                        worker.GetUniqueLoadID(),
                        builtDef.defName,
                        1,
                        builtDef.BaseMarketValue,
                        Find.TickManager.TicksGame);
                }
                ChronicleEvent ev = BuildThingEvent(builtStableId, ChronicleEventType.Built);
                if (workerIsCurrent)
                {
                    AddPawnSubject(ev, worker);
                }
                string eventOwnerId = workerIsCurrent
                    ? worker.GetUniqueLoadID()
                    : builtStableId;
                AddEvent(component, eventOwnerId, ev);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record built thing " + (builtDef != null ? builtDef.defName : "null") + ": " + ex.Message);
            }
        }

        public void OnBattleStarted(IncidentDef incidentDef)
        {
            if (!IsRecordingEnabled() || incidentDef == null)
            {
                return;
            }
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                // Battle identity: incident defName + tick is unique within a save.
                string stableId = incidentDef.defName + "@" + Find.TickManager.TicksGame;
                if (component.GetObject(stableId) == null)
                {
                    // A new battle supersedes the previous ongoing one: mark the
                    // old battle ended (its EndTick is otherwise never set, which
                    // would make GetActiveBattle() report it as ongoing forever).
                    ClosePreviousBattle(component, stableId);
                    component.AddObject(new BattleObject
                    {
                        StableId = stableId,
                        IncidentDefName = incidentDef.defName,
                        // v4.14: snapshot the threat category (ThreatBig/ThreatSmall)
                        // so the overview card + KPI can tint without Def drift.
                        ThreatKey = BattleThreatKey(incidentDef)
                    });
                }
                BattleObject battle = component.GetObject(stableId) as BattleObject;
                if (battle != null && battle.StartTick < 0L)
                {
                    // Snapshot the trigger time exactly once; a re-firing of the same
                    // incident in the same tick overwrites nothing (stableId is tick-bound).
                    battle.StartTick = Find.TickManager.TicksGame;
                }
                ChronicleEvent ev = new ChronicleEvent
                {
                    Tick = Find.TickManager.TicksGame,
                    TypeKey = ChronicleEventType.Battle,
                    Primary = new ObjectRef(ArchiveCategoryKeys.Battle, stableId, null),
                    Subjects = new List<ObjectRef>(),
                    Params = new Dictionary<string, string>()
                };
                // P2: snapshot current colony people as participants + Subject edges
                // so GetEventsFor(pawn) returns this battle for every fighter present.
                AttachBattleRoster(component, battle, ev);
                AddEvent(component, stableId, ev);
                // v4.11 P0: link the raid Lord(s) just spawned by TryExecuteWorker and
                // snapshot the force size + runtime countdown. TryExecuteWorker ran
                // synchronously inside IncidentWorker.TryExecute, so the Lords exist now.
                component.LinkRaidLords(battle);
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to record battle start " + (incidentDef != null ? incidentDef.defName : "null") + ": " + ex.Message);
            }
        }

        private static void AttachBattleRoster(ChronicleGameComponent component, BattleObject battle, ChronicleEvent ev)
        {
            if (ev == null)
            {
                return;
            }
            List<ColonyMember> people = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < people.Count; i++)
            {
                ColonyMember m = people[i];
                if (m == null || m.Pawn == null || m.Pawn.Dead)
                {
                    continue;
                }
                Pawn p = m.Pawn;
                string id = p.GetUniqueLoadID();
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                if (battle != null)
                {
                    AddBattleParticipant(battle, p);
                }
                // v6.8: 该殖民者参与本次战役，累加个人参战次数（幂等由 ParticipantIds 去重保证）。
                PawnObject po = component.GetObject(id) as PawnObject;
                if (po != null)
                {
                    po.ParticipatedBattles++;
                }
                if (!SubjectContains(ev, id))
                {
                    ev.Subjects.Add(ObjectRef.ForPawn(id, p.LabelShort));
                }
            }
        }

        private static void ClosePreviousBattle(ChronicleGameComponent component, string newBattleStableId)
        {
            if (component == null)
            {
                return;
            }
            long now = Find.TickManager.TicksGame;
            for (int i = 0; i < component.Objects.Count; i++)
            {
                BattleObject battle = component.Objects[i] as BattleObject;
                if (battle == null
                    || battle.EndTick != -1L
                    || battle.StableId == newBattleStableId)
                {
                    continue;
                }
                battle.EndTick = now;
            }
        }
        public void LinkRaidLords(BattleObject battle)
        {
            // MATRIX-010 治理：实现已下沉 Data.ChronicleGameComponent；本方法保留为
            // 接口转发（IArchiveService.LinkRaidLords 兼容），Application 内部直调 component。
            ChronicleGameComponent component = Component;
            if (component != null)
            {
                component.LinkRaidLords(battle);
            }
        }

        public void OnRaidPawnGone(int lordLoadId, int remainingPawns)
        {
            string battleStableId;
            if (!ChronicleGameComponent.RaidLordToBattle.TryGetValue(lordLoadId, out battleStableId)
                || string.IsNullOrEmpty(battleStableId))
            {
                return;
            }
            FinalizeBattleIfRepursed(battleStableId, lordLoadId, remainingPawns);
        }

        private void FinalizeBattleIfRepursed(string battleStableId, int lordLoadId, int lordRemaining)
        {
            try
            {
                ChronicleGameComponent component = Component;
                if (component == null)
                {
                    return;
                }
                BattleObject battle = component.GetObject(battleStableId) as BattleObject;
                if (battle == null || battle.EndTick != -1L)
                {
                    return;
                }
                if (battle.RaidCount <= 0)
                {
                    // No linked Lord force to track; leave to ClosePreviousBattle.
                    return;
                }
                if (lordRemaining <= 0)
                {
                    // This Lord's raiders are all gone: stop tracking it and finalize
                    // if no other linked Lord still has pawns.
                    ChronicleGameComponent.RaidLordToBattle.Remove(lordLoadId);
                    bool anyRemaining = false;
                    foreach (KeyValuePair<int, string> kv in ChronicleGameComponent.RaidLordToBattle)
                    {
                        if (kv.Value == battleStableId)
                        {
                            anyRemaining = true;
                            break;
                        }
                    }
                    if (!anyRemaining)
                    {
                        battle.EndTick = Find.TickManager.TicksGame;
                        battle.RemainingRaidCount = 0;
                        component.MarkChanged();
                    }
                    return;
                }
                // Keep RemainingRaidCount as the smallest seen non-zero remaining across Lords.
                if (battle.RemainingRaidCount < 0 || lordRemaining < battle.RemainingRaidCount)
                {
                    battle.RemainingRaidCount = lordRemaining;
                }
            }
            catch (System.Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Archive, "failed to finalize battle: " + ex.Message);
            }
        }

        private void AddEvent(ChronicleGameComponent component, string stableId, ChronicleEvent ev)
        {
            int maxEvents = PersonalChronicleMod.Settings != null ? PersonalChronicleMod.Settings.MaxEventsPerPawn : 200;
            if (maxEvents <= 0)
            {
                return;
            }
            if (ev == null)
            {
                return;
            }
            ev.ImportanceLevel = (int)ChronicleEventImportance.Resolve(ev);
            int retentionLimit = ev.ImportanceLevel <= (int)ChronicleImportance.Routine
                ? Math.Max(24, maxEvents / 3)
                : maxEvents;
            if (component.GetEventsFor(stableId).Count >= retentionLimit)
            {
                return;
            }
            component.AddEvent(ev);
        }

        private static ChronicleEvent BuildPawnEvent(string stableId, string labelSnapshot, string typeKey)
        {
            return new ChronicleEvent
            {
                Tick = Find.TickManager.TicksGame,
                TypeKey = typeKey,
                Primary = ObjectRef.ForPawn(stableId, labelSnapshot),
                Subjects = new List<ObjectRef>(),
                Params = new Dictionary<string, string>()
            };
        }

        private static ChronicleEvent BuildThingEvent(string stableId, string typeKey)
        {
            return new ChronicleEvent
            {
                Tick = Find.TickManager.TicksGame,
                TypeKey = typeKey,
                Primary = new ObjectRef(ArchiveCategoryKeys.Thing, stableId, null),
                Subjects = new List<ObjectRef>(),
                Params = new Dictionary<string, string>()
            };
        }

        private static void AddPawnSubject(ChronicleEvent ev, Pawn worker)
        {
            if (ev == null || worker == null)
            {
                return;
            }
            ev.Subjects.Add(ObjectRef.ForPawn(worker.GetUniqueLoadID(), worker.LabelShort));
        }

        private static void RegisterThingObject(ChronicleGameComponent component, Thing thing, Pawn holder = null)
        {
            if (component == null || thing == null || thing.def == null)
            {
                return;
            }
            // v4.6.5: the "Thing" category is scoped to equipment only
            // (weapons + wearable apparel). Raw materials, food and buildings
            // are excluded from the archive object graph.
            if (!IsEquipable(thing))
            {
                return;
            }
            string stableId = thing.def.defName + ":" + thing.thingIDNumber;
            if (component.GetObject(stableId) == null)
            {
                component.AddObject(new ThingObject
                {
                    StableId = stableId,
                    ThingDefName = thing.def.defName,
                    WeakId = stableId
                });
            }
            if (holder != null)
            {
                NoteWeaponHolder(component, stableId, holder);
            }
        }

        private static bool IsEquipable(Thing thing)
        {
            if (thing == null || thing.def == null)
            {
                return false;
            }
            return IsEquipableDef(thing.def);
        }

        private static bool IsEquipableDef(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }
            // v4.9.1: data-driven equipment archive policy — weapons always in;
            // apparel only when it carries enough armor to count as combat apparel
            // (dust jackets / work wear / fashion clothes are excluded). Mirrors
            // Patch_ThingDestroy so capture and decommission scopes stay aligned.
            return Domain.ThingArchivePolicy.Captures(def);
        }

        private static bool HasRecordedExternalKill(
            ChronicleGameComponent component,
            string killerId,
            string victimId)
        {
            if (component == null || string.IsNullOrEmpty(killerId) || string.IsNullOrEmpty(victimId))
            {
                return false;
            }
            IReadOnlyList<ChronicleEvent> events = component.GetEventsFor(killerId);
            for (int i = 0; i < events.Count; i++)
            {
                ChronicleEvent ev = events[i];
                if (ev == null || ev.TypeKey != ChronicleEventType.Death || ev.Params == null)
                {
                    continue;
                }
                string role;
                string recordedVictimId;
                if (ev.Params.TryGetValue(ChronicleEventParams.CombatRole, out role)
                    && role == ChronicleEventParams.CombatRoleKill
                    && ev.Params.TryGetValue(ChronicleEventParams.VictimStableId, out recordedVictimId)
                    && recordedVictimId == victimId)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasRecordedDeathForVictim(ChronicleGameComponent component, string victimId)
        {
            if (component == null || string.IsNullOrEmpty(victimId) || component.Events == null)
            {
                return false;
            }
            for (int i = 0; i < component.Events.Count; i++)
            {
                ChronicleEvent ev = component.Events[i];
                if (ev == null || ev.TypeKey != ChronicleEventType.Death || ev.Params == null)
                {
                    continue;
                }
                string recordedVictimId;
                if (ev.Params.TryGetValue(ChronicleEventParams.VictimStableId, out recordedVictimId)
                    && recordedVictimId == victimId)
                {
                    return true;
                }
            }
            return false;
        }


    }
}

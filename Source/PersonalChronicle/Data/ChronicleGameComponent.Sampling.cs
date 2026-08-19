using System.Collections.Generic;
using System.Collections.ObjectModel;
using PersonalChronicle.Domain;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Data
{
    /// <summary>Partial of ChronicleGameComponent — see main file for Scribe/migration.</summary>
    public sealed partial class ChronicleGameComponent : GameComponent
    {

        public override void GameComponentTick()
        {
            long tick = (long)Find.TickManager.TicksGame;

            // Recording gate shared by work sampling + reconcile.
            if (PersonalChronicleMod.Settings == null || !PersonalChronicleMod.Settings.EnableRecording)
            {
                return;
            }

            // v3.1: sample labour time + place visits every WorkSampleIntervalTicks.
            if (tick % WorkSampleIntervalTicks == 0L)
            {
                SampleWorkTimeAndPlaces(tick);
                // Work-time and place snapshots are live archive data. One
                // token bump per sample keeps the UI fresh without a per-frame
                // event or allocation path.
                MarkChanged();
            }

            // Throttle: one reconcile pass per ReconcileIntervalTicks.
            if (tick % ReconcileIntervalTicks != 0L)
            {
                return;
            }

            // P3-2（2026-08-19 修复）：DevMode Def reload 会重建 StatDef.parts 导致专业速度
            // StatPart 丢失；此处按 reconcile 节流幂等补注入（存在则零操作，成本可忽略）。
            Capture.Effects.ProfessionalEffectRegistry.EnsureInjected();

            // v4.13 location atlas: archive newly observed maps / settlements /
            // quest sites and close deinited ones. Same reconcile cadence as the
            // colonist pass below — no extra polling, idempotent (AddObject).
            // Engine APIs verified against 1.6 by reflection (Map.TileInfo /
            // Map.ParentFaction / WorldGrid[PlanetTile] / Settlement.TraderKind /
            // StockGenerator_Tag.tradeTag; internal categoryDef/thingDef read via
            // Harmony Traverse). No Harmony patch — reconcile-driven only.
            Capture.LocationAtlasCapture.Reconcile(this, tick);

            List<ColonyMember> live = ChronicleColonistScanner.EnumerateCurrentPeople();

            // Presence set for THIS pass — drives both the confirmation window
            // and the per-pass prune below.
            HashSet<string> nowPresent = new HashSet<string>();
            for (int i = 0; i < live.Count; i++)
            {
                ColonyMember member = live[i];
                if (member == null || member.Pawn == null)
                {
                    continue;
                }
                Pawn pawn = member.Pawn;
                string stableId = pawn.GetUniqueLoadID();
                if (string.IsNullOrEmpty(stableId))
                {
                    continue;
                }
                nowPresent.Add(stableId);

                // Already archived (by Patch_SetFaction, backfill or an earlier
                    // reconcile) → 同步最新角色（囚犯被招募后徽标自动更新），不重建。
                    ArchiveObject existing;
                    if (objectsByStableId.TryGetValue(stableId, out existing))
                    {
                        PawnObject existingPawn = existing as PawnObject;
                        if (existingPawn != null && existingPawn.Role != member.Role)
                        {
                            existingPawn.Role = member.Role;
                            MarkChanged();
                        }
                        // Relation catch-up. On a fresh colony the scenario's relation
                        // workers may not have run yet at FinalizeInit, so the first
                        // reconcile passes are what actually populate the social graph.
                        // Additive + de-duplicating, hence safe every pass; it stops
                        // finding anything new once the graph is settled.
                        EnsureRelationsBackfilled(pawn);
                        // v1.1.4 UI 拓展：reconcile 时同步该殖民者当前装备的武器到 ThingObject
                        // 的 HolderRecords / CurrentHolderId，让 ITab 神器传承卡能正确识别
                        // 玩家当前手持的武器（之前仅在战斗/制造时记录，导致无战斗时传承卡空）。
                        SyncEquippedWeapon(pawn, tick);
                        continue;
                    }
                if (reconcileCandidates.Contains(stableId))
                {
                    // Second consecutive presence → confirmed colonist: archive.
                    // JoinTick 走统一默认决策：新档=开局当天(0)，读档=发现当天起点。
                    // 不可再硬编码 -1L —— 否则开局殖民者会被永久定格为"中途加入"。
                    reconcileCandidates.Remove(stableId);
                    PawnObject record = CreateRecord(pawn, ResolveDefaultJoinTick(), member.Role);
                    if (AddObject(record))
                    {
                        AddDetectedJoinEvent(record);
                    }
                }
                else
                {
                    // First presence: open the confirmation window.
                    reconcileCandidates.Add(stableId);
                }
            }

            // Prune: candidates not present this pass leave the window, so a
            // temporary reinforcement must re-confirm from scratch next time.
            if (reconcileCandidates.Count > 0)
            {
                List<string> stale = null;
                foreach (string id in reconcileCandidates)
                {
                    if (!nowPresent.Contains(id))
                    {
                        if (stale == null)
                        {
                            stale = new List<string>();
                        }
                        stale.Add(id);
                    }
                }
                if (stale != null)
                {
                    for (int i = 0; i < stale.Count; i++)
                    {
                        reconcileCandidates.Remove(stale[i]);
                    }
                }
            }

            // v1.1.4 勋章授勋：与 reconcile 同节流（ReconcileIntervalTicks）。
            // MedalAwardService 内部自行做 recording gate、活读扫描、阈值判定、
            // 去重写入 PawnObject.GrantedMedals 与 MarkChanged；返回值（本次新授予）
            // 暂忽略，供后续金质公告（T3）消费。
            MedalAwardService.Run(this);

            // P5~P7 职业资格自动授予（与勋章同节流）：判定 Qualified → 自动授予职称
            // → 回写 CareerEvent(TitleGranted)。内部自行做 recording gate、活读扫描、
            // 去重写入 CareerData.GrantedTitles 与 MarkChanged。
            RunQualification();
        }

        private void SampleWorkTimeAndPlaces(long gameTick)
        {
            if (Objects == null || Objects.Count == 0)
            {
                return;
            }
            // One live scan → stableId map (avoid O(n²) per-object rescans).
            Dictionary<string, Pawn> liveById = new Dictionary<string, Pawn>();
            List<ColonyMember> live = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < live.Count; i++)
            {
                ColonyMember m = live[i];
                if (m == null || m.Pawn == null)
                {
                    continue;
                }
                string id = m.Pawn.GetUniqueLoadID();
                if (!string.IsNullOrEmpty(id) && !liveById.ContainsKey(id))
                {
                    liveById[id] = m.Pawn;
                }
            }

            for (int i = 0; i < Objects.Count; i++)
            {
                PawnObject record = Objects[i] as PawnObject;
                if (record == null || record.IsArchived || string.IsNullOrEmpty(record.StableId))
                {
                    continue;
                }
                Pawn pawn;
                if (!liveById.TryGetValue(record.StableId, out pawn) || pawn == null || pawn.Dead || pawn.Downed)
                {
                    continue;
                }
                bool changed = false;
                WorkTypeDef workType = ResolveCurrentWorkType(pawn);
                if (workType != null)
                {
                    if (record.WorkTime == null)
                    {
                        record.WorkTime = new WorkTimeAccumulator();
                    }
                    record.WorkTime.AddSample(workType.defName, WorkSampleCreditTicks, gameTick);
                    changed = true;
                }
                // P3: place enter/leave ledger (always, even when idle).
                if (SamplePlaceVisit(record, pawn, gameTick))
                {
                    changed = true;
                }
                // v1.1.4 方案 B：定期采样住所（pawn.ownership.OwnedRoom.Role），
                // 只存 RoomRoleDef.defName 稳定键（UI 实时解析 label，改名/翻译自适应）。
                if (SampleResidence(record, pawn, gameTick))
                {
                    changed = true;
                }
                if (changed)
                {
                    MarkChanged();
                }
            }
        }

        private static bool SamplePlaceVisit(PawnObject record, Pawn pawn, long gameTick)
        {
            if (record == null || pawn == null)
            {
                return false;
            }
            string kind;
            string key = ResolvePlaceKey(pawn, out kind);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            if (record.PlaceHistory == null)
            {
                record.PlaceHistory = new List<PlaceVisit>();
            }
            PlaceVisit open = null;
            for (int i = record.PlaceHistory.Count - 1; i >= 0; i--)
            {
                PlaceVisit v = record.PlaceHistory[i];
                if (v != null && v.IsOpen)
                {
                    open = v;
                    break;
                }
            }
            if (open != null && open.PlaceKey == key)
            {
                // Still here — keep primary in sync.
                if (kind == PlaceVisitKeys.KindMap)
                {
                    if (record.PrimaryPlaceDefName != key)
                    {
                        record.PrimaryPlaceDefName = key;
                        return true;
                    }
                }
                return false;
            }
            if (open != null)
            {
                open.LeaveTick = gameTick;
            }
            record.PlaceHistory.Add(new PlaceVisit(key, kind, gameTick));
            // Cap history length to avoid unbounded growth (keeps last 32 stays).
            const int maxVisits = 32;
            while (record.PlaceHistory.Count > maxVisits)
            {
                record.PlaceHistory.RemoveAt(0);
            }
            if (kind == PlaceVisitKeys.KindMap)
            {
                record.PrimaryPlaceDefName = key;
            }
            return true;
        }

        private static bool SampleResidence(PawnObject record, Pawn pawn, long gameTick)
        {
            if (record == null || pawn == null || pawn.ownership == null)
            {
                return false;
            }
            Room room = pawn.ownership.OwnedRoom;
            RoomRoleDef role = (room != null) ? room.Role : null;
            string roleDefName = (role != null) ? role.defName : null;
            if (string.IsNullOrEmpty(roleDefName))
            {
                // 无归属房间角色：保留上次快照（不覆盖、不误清）。
                return false;
            }
            if (record.Residence == null)
            {
                record.Residence = new ResidenceSnapshot();
            }
            if (record.Residence.RoomRoleDefName == roleDefName
                && record.Residence.LastSeenTick == gameTick)
            {
                return false;
            }
            // 房间中心坐标：用房间 extents 中心近似（Room.ExtentsClose.CenterCell）。
            IntVec3 center = default(IntVec3);
            int mapIndex = -1;
            if (room.CellCount > 0)
            {
                center = room.ExtentsClose.CenterCell;
                if (pawn.Map != null)
                {
                    mapIndex = pawn.Map.Index;
                }
            }
            bool changed = record.Residence.RoomRoleDefName != roleDefName;
            record.Residence.RecordSeen(roleDefName, gameTick, mapIndex, center);
            return changed;
        }

        internal bool AddWorkplaceUse(
            string pawnStableId,
            string buildingDefName,
            string buildingStableId,
            string roomRoleDefName,
            long gameTick)
        {
            if (string.IsNullOrEmpty(pawnStableId) || string.IsNullOrEmpty(buildingDefName))
            {
                return false;
            }
            PawnObject pawn = GetObject(pawnStableId) as PawnObject;
            if (pawn == null || pawn.IsArchived)
            {
                return false;
            }
            if (pawn.Workplace == null)
            {
                pawn.Workplace = new WorkplaceSnapshot();
            }
            pawn.Workplace.RecordUse(buildingDefName, buildingStableId, roomRoleDefName, gameTick);
            MarkChanged();
            return true;
        }

        private static PawnObject ConvertLegacyPawn(PawnRecord legacy)
        {
            return new PawnObject
            {
                StableId = legacy.StableId,
                LabelSnapshot = legacy.LabelShort,
                LabelShort = legacy.LabelShort,
                KindDefName = legacy.KindDefName,
                FactionDefName = legacy.FactionDefName,
                JoinTick = legacy.JoinTick,
                DeathTick = legacy.DeathTick,
                DeathCauseKey = legacy.DeathCauseKey,
                Role = legacy.Role
            };
        }

        private void ResetBattleRaidCounters()
        {
            if (Objects == null)
            {
                return;
            }
            for (int i = 0; i < Objects.Count; i++)
            {
                BattleObject battle = Objects[i] as BattleObject;
                if (battle == null || battle.EndTick != -1L)
                {
                    continue;
                }
                battle.RemainingRaidCount = battle.RaidCount > 0 ? battle.RaidCount : -1;
            }
        }

        private void RelinkOngoingBattles()
        {
            if (Objects == null)
            {
                return;
            }
            for (int i = 0; i < Objects.Count; i++)
            {
                BattleObject battle = Objects[i] as BattleObject;
                if (battle == null || battle.EndTick != -1L || battle.RaidCount <= 0)
                {
                    continue;
                }
                LinkRaidLords(battle);
            }
        }

        private void AddEventEdges(ChronicleEvent ev)
        {
            if (ev == null)
            {
                return;
            }
            if (ev.Primary == null && !string.IsNullOrEmpty(ev.PawnStableId))
            {
                ev.Primary = new ObjectRef(ArchiveCategoryKeys.Pawn, ev.PawnStableId, null);
            }
            if (ev.Primary != null && !string.IsNullOrEmpty(ev.Primary.StableId))
            {
                AddEdge(ev.Primary.StableId, ev);
            }
            if (ev.Subjects != null)
            {
                for (int j = 0; j < ev.Subjects.Count; j++)
                {
                    ObjectRef subject = ev.Subjects[j];
                    if (subject != null && !string.IsNullOrEmpty(subject.StableId))
                    {
                        AddEdge(subject.StableId, ev);
                    }
                }
            }
        }

        private void AddEdge(string stableId, ChronicleEvent ev)
        {
            List<ChronicleEvent> list;
            if (!eventsByObject.TryGetValue(stableId, out list))
            {
                list = new List<ChronicleEvent>();
                eventsByObject[stableId] = list;
            }
            list.Add(ev);
        }

        private void BackfillExistingColonists()
        {
            // 活读当前殖民地全部人口（自由殖民者 / 奴隶 / 囚犯），单一谓词来源。
            // 源 = 地图 + 商队（ChronicleColonistScanner 已移除过宽的
            // Find.WorldPawns.AllPawnsAlive 世界兜底，避免在 8≠3 误统计）。
            // 角色随记录一并写入，UI 据此区分徽标。
            //
            // JoinTick 走统一默认决策（ResolveDefaultJoinTick）：新档现存人口一律为
            // 开局殖民者(JoinTick=0)；读档时存档缺失、后来发现的存活人口归到发现当天
            // 起点。不再依赖 TicksGame<=0，也不再用 -1 制造"中途加入"态。
            long joinTick = ResolveDefaultJoinTick();
            List<ColonyMember> people = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < people.Count; i++)
            {
                ColonyMember member = people[i];
                if (member == null || member.Pawn == null)
                {
                    continue;
                }
                PawnObject record = CreateRecord(member.Pawn, joinTick, member.Role);
                if (!AddObject(record))
                {
                    // Already archived: AddObject discards the freshly built
                    // record, so the relation snapshot it carries would be lost.
                    // Backfill onto the persisted record instead — this is the
                    // only path by which pre-existing saves ever gain the social
                    // ties captured since the three-source rewrite. Forced: this
                    // one-shot load path must not be skipped by the throttle.
                    EnsureRelationsBackfilled(member.Pawn, true);
                }
            }
        }

        internal bool EnsureRelationsBackfilled(Pawn pawn)
        {
            return EnsureRelationsBackfilled(pawn, false);
        }

        internal bool EnsureRelationsBackfilled(Pawn pawn, bool force)
        {
            if (pawn == null)
            {
                return false;
            }
            string stableId = pawn.GetUniqueLoadID();
            if (string.IsNullOrEmpty(stableId))
            {
                return false;
            }
            ArchiveObject existing;
            if (!objectsByStableId.TryGetValue(stableId, out existing))
            {
                return false;
            }
            PawnObject record = existing as PawnObject;
            if (record == null || record.IsArchived)
            {
                return false;
            }
            // Throttle: the capture walks the colony to derive kin and opinion
            // ties, which is O(population) per pawn. Once a pawn's graph has
            // stopped yielding new ties we stop re-scanning it every reconcile;
            // live relation changes still arrive through the relation patches.
            if (!force && IsRelationScanSettled(stableId))
            {
                return false;
            }
            int before = record.Relations != null ? record.Relations.Count : 0;
            PawnArchiveSnapshots.CaptureInitialRelations(pawn, record);
            int after = record.Relations != null ? record.Relations.Count : 0;
            if (after > before)
            {
                relationScanMissStreak.Remove(stableId);
                MarkChanged();
                return true;
            }
            NoteRelationScanMiss(stableId);
            return false;
        }

        private void SyncEquippedWeapon(Pawn pawn, long gameTick)
        {
            if (pawn == null || pawn.equipment == null) return;
            ThingWithComps weapon = pawn.equipment.Primary;
            if (weapon == null || weapon.def == null) return;
            // 仅追踪武器类（melee/ranged），不追踪工具。
            if (!weapon.def.IsWeapon) return;

            string stableId = weapon.def.defName + ":" + weapon.thingIDNumber;
            string holderId = pawn.GetUniqueLoadID();
            if (string.IsNullOrEmpty(holderId)) return;

            // 注册 ThingObject（若不存在）。
            if (GetObject(stableId) == null)
            {
                ThingObject newThing = new ThingObject
                {
                    StableId = stableId,
                    ThingDefName = weapon.def.defName,
                    WeakId = stableId
                };
                AddObject(newThing);
            }

            ThingObject thing = GetObject(stableId) as ThingObject;
            if (thing == null) return;

            // 已是当前持有者则跳过 HolderRecords 追加（避免每 tick 重复记录）。
            if (thing.CurrentHolderId == holderId) return;

            // 切换持有者 → 关闭前任 record（如有），写新 record。
            if (thing.HolderRecords == null) thing.HolderRecords = new List<HolderRecord>();
            // 关闭当前 last record（若有，且与新持有者不同）。
            if (thing.HolderRecords.Count > 0)
            {
                HolderRecord last = thing.HolderRecords[thing.HolderRecords.Count - 1];
                if (last != null && last.EndTick < 0 && last.StableId != holderId)
                {
                    last.EndTick = gameTick;
                }
            }
            // 写新 record。
            bool isFirst = thing.HolderRecords.Count == 0;
            thing.HolderRecords.Add(new HolderRecord(
                holderId,
                pawn.LabelShort,
                gameTick,
                isFirst,
                HolderRecord.HolderKindOwn));
            // HolderHistory 同步追加。
            if (thing.HolderHistory == null) thing.HolderHistory = new List<ObjectRef>();
            if (thing.HolderHistory.Count == 0
                || thing.HolderHistory[thing.HolderHistory.Count - 1].StableId != holderId)
            {
                thing.HolderHistory.Add(ObjectRef.ForPawn(holderId, pawn.LabelShort));
            }
            thing.CurrentHolderId = holderId;
            MarkChanged();
        }

        private bool IsRelationScanSettled(string stableId)
        {
            int misses;
            return relationScanMissStreak.TryGetValue(stableId, out misses)
                && misses >= RelationScanSettleThreshold;
        }

        private void NoteRelationScanMiss(string stableId)
        {
            int misses;
            if (relationScanMissStreak.TryGetValue(stableId, out misses))
            {
                if (misses < RelationScanSettleThreshold)
                {
                    relationScanMissStreak[stableId] = misses + 1;
                }
                return;
            }
            relationScanMissStreak[stableId] = 1;
        }

        private bool AllArchivedColonistsHaveRelations()
        {
            List<ColonyMember> people = ChronicleColonistScanner.EnumerateCurrentPeople();
            if (people.Count == 0)
            {
                // Transient empty population during a load transition: do not
                // latch the migration, retry later.
                return false;
            }
            for (int i = 0; i < people.Count; i++)
            {
                ColonyMember member = people[i];
                if (member == null || member.Pawn == null)
                {
                    continue;
                }
                string stableId = member.Pawn.GetUniqueLoadID();
                if (string.IsNullOrEmpty(stableId))
                {
                    continue;
                }
                ArchiveObject existing;
                if (!objectsByStableId.TryGetValue(stableId, out existing))
                {
                    continue;
                }
                PawnObject record = existing as PawnObject;
                if (record == null || record.IsArchived)
                {
                    continue;
                }
                if (record.Relations == null || record.Relations.Count == 0)
                {
                    // A genuine hermit is indistinguishable from "not captured
                    // yet" here, so stay unmigrated and let the reconcile retry.
                    // Worst case is a few redundant passes, never lost data.
                    return false;
                }
            }
            return true;
        }

        private bool PruneInitialRosterArtifacts()
        {
            HashSet<string> currentIds = new HashSet<string>();
            List<ColonyMember> currentPeople = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < currentPeople.Count; i++)
            {
                ColonyMember member = currentPeople[i];
                if (member != null && member.Pawn != null)
                {
                    string id = member.Pawn.GetUniqueLoadID();
                    if (!string.IsNullOrEmpty(id))
                    {
                        currentIds.Add(id);
                    }
                }
            }

            // During a save-load transition the map lists can be temporarily
            // empty. Do not interpret that transient state as a zero-population
            // colony and delete otherwise valid unknown-join snapshots.
            if (currentIds.Count == 0)
            {
                return false;
            }

            int removed = 0;
            for (int i = Objects.Count - 1; i >= 0; i--)
            {
                PawnObject pawn = Objects[i] as PawnObject;
                if (pawn == null
                    || pawn.IsArchived
                    || pawn.JoinTick >= 0L
                    || currentIds.Contains(pawn.StableId)
                    || GetEventsFor(pawn.StableId).Count > 0)
                {
                    continue;
                }
                Objects.RemoveAt(i);
                objectsByStableId.Remove(pawn.StableId);
                removed++;
            }
            if (removed > 0)
            {
                MarkChanged();
                ChronicleLog.Save("removed " + removed
                    + " empty scenario-roster archive artifacts during v4 migration.");
            }
            return true;
        }

        private bool PruneUnconfirmedRosterArtifacts()
        {
            HashSet<string> currentIds = new HashSet<string>();
            List<ColonyMember> currentPeople = ChronicleColonistScanner.EnumerateCurrentPeople();
            for (int i = 0; i < currentPeople.Count; i++)
            {
                ColonyMember member = currentPeople[i];
                if (member != null && member.Pawn != null)
                {
                    string stableId = member.Pawn.GetUniqueLoadID();
                    if (!string.IsNullOrEmpty(stableId))
                    {
                        currentIds.Add(stableId);
                    }
                }
            }

            // Do not consume the migration version during a transient load
            // frame where maps have not populated yet.
            if (currentIds.Count == 0)
            {
                return false;
            }

            HashSet<string> removedIds = new HashSet<string>();
            for (int i = Objects.Count - 1; i >= 0; i--)
            {
                PawnObject pawn = Objects[i] as PawnObject;
                if (pawn == null
                    || pawn.IsArchived
                    || pawn.JoinTick >= 0L
                    || currentIds.Contains(pawn.StableId))
                {
                    continue;
                }
                Objects.RemoveAt(i);
                objectsByStableId.Remove(pawn.StableId);
                removedIds.Add(pawn.StableId);
            }

            // Scenario-editor relation callbacks can leave orphaned events even
            // after their candidate PawnObject is removed. Purge only events
            // touching the exact records removed above; unrelated historical
            // events remain intact.
            int removedEvents = 0;
            if (removedIds.Count > 0)
            {
                for (int i = Events.Count - 1; i >= 0; i--)
                {
                    if (!EventReferencesAny(Events[i], removedIds))
                    {
                        continue;
                    }
                    Events.RemoveAt(i);
                    removedEvents++;
                }
            }
            if (removedIds.Count > 0 || removedEvents > 0)
            {
                MarkChanged();
                ChronicleLog.Save("removed " + removedIds.Count
                    + " unconfirmed scenario-roster archive artifacts and "
                    + removedEvents + " orphaned events during v4.0.1 migration.");
            }
            return true;
        }


    }
}

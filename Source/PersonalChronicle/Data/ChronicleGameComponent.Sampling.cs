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
            // 性能探针驱动：必须在 recording gate 之前，否则关闭录制时永远不输出。
            PerfProbe.Tick();

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

            // SDD-LCE-003 经济中心 K 线：库存数量采样（1 次 / 250 ticks ⇒ ≈10 点 / 小时档）。
            // 与工时采样同轴（TicksGame 判等）；入桶时刻取 TicksAbs（见 StockSeries partial 注释）。
            if (tick % StockSampleIntervalTicks == 0L)
            {
                SampleStockSeries();
                MarkChanged();
            }

            // Throttle: one reconcile pass per ReconcileIntervalTicks.
            if (tick % ReconcileIntervalTicks != 0L)
            {
                return;
            }

            // 性能探针：整趟 reconcile（人口扫描 + 勋章 + 资格 + 自动归档 + 地点图集）。
            long perfReconcile = PerfProbe.Start();

            // P3-2（2026-08-19 修复）：DevMode Def reload 会重建 StatDef.parts 导致专业速度
            // StatPart 丢失；此处按 reconcile 节流幂等补注入（存在则零操作，成本可忽略）。
            // ⚠ 2026-09-15 修复（审计 P0）：**旧管线 `Capture.Effects.ProfessionalEffectRegistry`
            // 已删除** —— 它与本行下方的 `Effects.ProfessionalEffectStatPartInjector` 同时把「同类但
            // 不同命名空间」的 StatPart 注入同一 `StatDef.parts`，导致 factor 被**乘算两次**。
            // 现只保留顶层 `Effects/` 单一管线（索引走 `Domain/Profession/ProfessionalEffectIndex`）。
            PersonalChronicle.Effects.ProfessionalEffectStatPartInjector.EnsureInjected();
            // 车间 StatPart 同理：Def reload 会重建 StatDef.parts，按 reconcile 节流幂等补注入。
            PersonalChronicle.Effects.WorkshopStatPartInjector.EnsureInjected();

            // v4.13 location atlas: archive newly observed maps / settlements /
            // quest sites and close deinited ones. Same reconcile cadence as the
            // colonist pass below — no extra polling, idempotent (AddObject).
            // Engine APIs verified against 1.6 by reflection (Map.TileInfo /
            // Map.ParentFaction / WorldGrid[PlanetTile] / Settlement.TraderKind /
            // StockGenerator_Tag.tradeTag; internal categoryDef/thingDef read via
            // Harmony Traverse). No Harmony patch — reconcile-driven only.
            Capture.LocationAtlasCapture.Reconcile(this, tick);

            List<ColonyMember> live = ChronicleColonistScanner.EnumerateCurrentPeople();

            // ── A 方案采集（2026-09-15）：身份快照差分 → Leave / RoleChanged 事件 ──────────
            // 与 reconcile 同节流（600 tick = 10 秒），**无 Harmony、无新增 Save 字段**；
            // 缺场需连续 AbsenceConfirmPasses 次才判离档（兼容既有「只增不删」原则，
            // 防 cryptosleep / 临时外出 / 商队期间被误判）。
            CaptureIdentityDeltas(live, tick);

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
            // 去重写入 CareerData.GrantedTitles 与 MarkChanged();
            RunQualification();

            // v4.x 自动归档：扫描已死亡 pawn，按价值评估决定归档/保留/销毁。
            RunAutoArchive(tick);

            PerfProbe.End("reconcile", perfReconcile);
        }

        // ══════════════════ A 方案：身份增量采集（2026-09-15）══════════════════
        //
        // **目的**：让「分身份存量趋势」（预览 ① col1 的三系列折线）有真实数据。
        // 工程事件模型原本只有 Join/Death ⇒ 无法回推三系列（已核实）；补上 Leave /
        // RoleChanged 两种语义后即可按季回推。
        //
        // **实现取舍**：
        //   · 挂在既有 Reconcile 节流上（600 tick）—— **零 Harmony**、零新轮询；
        //   · 快照**不持久化**（`[Unsaved]`）—— 重启后重建，最多丢一个周期的变更；
        //   · 缺场需**连续 N 次**确认才判离档 —— 兼容既有注释明确警告过的
        //     「cryptosleep 会被误判为离开」风险。

        /// <summary>缺场确认次数（连续 N 次 reconcile 不在场 ⇒ 判离档；3 × 600 tick ≈ 30 秒）。</summary>
        private const int AbsenceConfirmPasses = 3;

        [Unsaved] private Dictionary<string, int> identityRoleSnapshot = new Dictionary<string, int>(System.StringComparer.Ordinal);
        [Unsaved] private Dictionary<string, int> identityCurrent = new Dictionary<string, int>(System.StringComparer.Ordinal);
        [Unsaved] private Dictionary<string, int> identityAbsence = new Dictionary<string, int>(System.StringComparer.Ordinal);
        [Unsaved] private readonly List<string> identityStaleIds = new List<string>();

        /// <summary>
        /// 身份快照差分：① 身份变更（自由 ↔ 奴隶 ↔ 囚犯）⇒ <c>RoleChanged</c>；
        /// ② 连续缺场达阈值 ⇒ <c>Leave</c>。零分配要点：字典**轮换复用**（current/snapshot 交换引用），
        /// 事件只在真有变更时构造。
        /// </summary>
        private void CaptureIdentityDeltas(List<ColonyMember> live, long tick)
        {
            if (live == null)
            {
                return;
            }
            // `PersonalChronicleMod.ArchiveService` 的静态类型是 IArchiveService，而事件写入走
            // IArchiveEventSink（同一实例的另一契约面）⇒ 用 as 取（实现类同时实现两者）。
            PersonalChronicle.Api.IArchiveEventSink sink =
                PersonalChronicleMod.ArchiveService as PersonalChronicle.Api.IArchiveEventSink;
            if (sink == null)
            {
                return;
            }
            bool firstPass = identityRoleSnapshot.Count == 0;
            identityCurrent.Clear();
            for (int i = 0; i < live.Count; i++)
            {
                ColonyMember member = live[i];
                Pawn pawn = member.Pawn;
                if (pawn == null)
                {
                    continue;
                }
                string id = pawn.GetUniqueLoadID();
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                int role = (int)member.Role;
                identityCurrent[id] = role;
                identityAbsence.Remove(id);          // 在场 ⇒ 清缺场计数
                if (!firstPass)
                {
                    int prevRole;
                    if (!identityRoleSnapshot.TryGetValue(id, out prevRole))
                    {
                        // **带身份的入档**：旧的 `Join` 记录由既有链路产生、**不含身份**，无法用于
                        // 分身份回推 ⇒ 本机制上线后新入档的成员在这里补记一条带 `role` 的入档事件
                        // （复用 Join 的 defName + 参数标记；回推侧只认带 `role` 的那些）。
                        RecordIdentityEvent(sink, id, pawn.LabelShort, "PersonalChronicleEventJoin", tick,
                            "role", role.ToString());
                    }
                    else if (prevRole != role)
                    {
                        RecordIdentityEvent(sink, id, pawn.LabelShort, "PersonalChronicleEventRoleChanged", tick,
                            "from", prevRole.ToString(), "to", role.ToString());
                    }
                }
            }
            identityStaleIds.Clear();
            foreach (KeyValuePair<string, int> pair in identityRoleSnapshot)
            {
                if (identityCurrent.ContainsKey(pair.Key))
                {
                    continue;
                }
                int streak;
                identityAbsence.TryGetValue(pair.Key, out streak);
                streak++;
                if (streak >= AbsenceConfirmPasses)
                {
                    identityStaleIds.Add(pair.Key);
                }
                else
                {
                    identityAbsence[pair.Key] = streak;
                }
            }
            for (int i = 0; i < identityStaleIds.Count; i++)
            {
                string id = identityStaleIds[i];
                // 带出「离开时的身份」—— 三系列回推需要它（缺了只能算总量近似）。
                int lastRole;
                identityRoleSnapshot.TryGetValue(id, out lastRole);
                RecordIdentityEvent(sink, id, null, "PersonalChronicleEventLeave", tick,
                    "role", lastRole.ToString());
                identityAbsence.Remove(id);
            }
            // 轮换：current 变本轮快照，旧快照复用为下轮草稿（零拷贝、零每趟分配）。
            Dictionary<string, int> swap = identityRoleSnapshot;
            identityRoleSnapshot = identityCurrent;
            identityCurrent = swap;
        }

        /// <summary>
        /// 写一条身份增量事件（失败节流记日志，不影响 reconcile）。
        /// <paramref name="parameterPairs"/> = 交替的 key/value（如 <c>"role","1"</c>）—— 三系列回推与
        /// 存档诊断都依赖这些参数（keys 用短名，非用户可见文本，不入 Keyed）。
        /// </summary>
        private static void RecordIdentityEvent(PersonalChronicle.Api.IArchiveEventSink sink, string stableId,
            string label, string defName, long tick, params string[] parameterPairs)
        {
            if (sink == null || string.IsNullOrEmpty(stableId))
            {
                return;
            }
            try
            {
                Dictionary<string, string> parameters = null;
                if (parameterPairs != null && parameterPairs.Length >= 2)
                {
                    parameters = new Dictionary<string, string>(System.StringComparer.Ordinal);
                    for (int i = 0; i + 1 < parameterPairs.Length; i += 2)
                    {
                        if (!string.IsNullOrEmpty(parameterPairs[i]))
                        {
                            parameters[parameterPairs[i]] = parameterPairs[i + 1];
                        }
                    }
                }
                sink.TryRecord(new PersonalChronicle.Api.ArchiveEventInput
                {
                    SourceId = "PersonalChronicle.Capture",
                    EventTypeDefName = defName,
                    Tick = tick,
                    Primary = new PersonalChronicle.Api.ArchiveEntityRef("Pawn", stableId, label),
                    Parameters = parameters,
                    DeduplicationKey = "PersonalChronicle.Capture:" + stableId + ":" + defName + ":" + tick
                });
            }
            catch (System.Exception ex)
            {
                if (ChronicleLog.Throttled("identity_delta_fail", 60000))
                {
                    ChronicleLog.Error(ChronicleLog.Category.Capture,
                        "identity delta record failed (" + defName + "): " + ex);
                }
            }
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
            // v1.1.6 多工坊历史：按 buildingStableId 去重，合并使用次数与坐标。
            if (pawn.WorkplaceHistory == null)
            {
                pawn.WorkplaceHistory = new List<WorkplaceHistoryEntry>();
            }
            WorkplaceHistoryEntry hist = null;
            for (int i = 0; i < pawn.WorkplaceHistory.Count; i++)
            {
                if (pawn.WorkplaceHistory[i] != null
                    && string.Equals(pawn.WorkplaceHistory[i].BuildingStableId, buildingStableId, System.StringComparison.Ordinal))
                {
                    hist = pawn.WorkplaceHistory[i];
                    break;
                }
            }
            int mapIndex = -1;
            IntVec3 cell = default(IntVec3);
            if (pawn.Workplace != null)
            {
                mapIndex = pawn.Workplace.MapIndex;
                cell = pawn.Workplace.Cell;
            }
            if (hist == null)
            {
                hist = new WorkplaceHistoryEntry(buildingDefName, buildingStableId, roomRoleDefName, mapIndex, cell, gameTick);
                pawn.WorkplaceHistory.Add(hist);
            }
            else
            {
                hist.MergeUse(roomRoleDefName, mapIndex, cell, gameTick);
            }
            MarkChanged();
            return true;
        }

        /// <summary>
        /// v1.1.6 切换档案记录的「当前工作场所」：把该殖民者的 WorkplaceSnapshot 改指选中的
        /// 工坊（不实际指挥殖民者做工，仅改档案记录，安全可逆）。用于工坊卡弹窗「设为当前」。
        /// </summary>
        internal bool SetCurrentWorkplace(string pawnStableId, string buildingDefName,
            string buildingStableId, string roomRoleDefName, int mapIndex, IntVec3 cell)
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
            pawn.Workplace.RecordUse(buildingDefName, buildingStableId, roomRoleDefName, GenTicks.TicksGame);
            if (mapIndex >= 0 && cell.IsValid)
            {
                pawn.Workplace.RecordLocation(mapIndex, cell);
            }
            // 同步进历史（若此前未记录过该工坊，则补一条）。
            if (pawn.WorkplaceHistory == null)
            {
                pawn.WorkplaceHistory = new List<WorkplaceHistoryEntry>();
            }
            bool found = false;
            for (int i = 0; i < pawn.WorkplaceHistory.Count; i++)
            {
                if (pawn.WorkplaceHistory[i] != null
                    && string.Equals(pawn.WorkplaceHistory[i].BuildingStableId, buildingStableId, System.StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                pawn.WorkplaceHistory.Add(new WorkplaceHistoryEntry(
                    buildingDefName, buildingStableId, roomRoleDefName, mapIndex, cell, GenTicks.TicksGame));
            }
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

        // ---- v4.x 自动归档 ----

        /// <summary>
        /// 每 ReconcileIntervalTicks (600 ticks = 10 秒) 调用一次。
        /// 扫描所有已死亡 PawnObject，按 HealthValuation 银币价值决定：
        ///   - 价值 ≥ 阈值 → Archived（永久保留，不过期）
        ///   - 价值 ＜ 阈值 → Archived + 设 ArchiveExpiryTick（30 天后 PendingPurge）
        ///   - 已到 ArchiveExpiryTick → PendingPurge
        ///   - PendingPurge → 真正删除（Objects + 关联 Events）
        /// </summary>
        private void RunAutoArchive(long tick)
        {
            var settings = PersonalChronicleMod.Settings;
            if (settings == null || !settings.AutoArchiveEnabled)
                return;

            float threshold = settings.AutoArchiveSilverThreshold;
            int purgeDays = settings.AutoArchivePurgeDays;
            long purgeTicks = (long)purgeDays * 60000L; // 60 tps * 60 sec * 24 hr * days

            int archivedCount = 0;
            int purgedCount = 0;
            System.Collections.Generic.List<string> purgedIds =
                new System.Collections.Generic.List<string>();

            for (int i = Objects.Count - 1; i >= 0; i--)
            {
                Domain.PawnObject pawn = Objects[i] as Domain.PawnObject;
                if (pawn == null || pawn.DeathTick <= 0L)
                    continue; // 只处理已死亡

                // 阶段 1：尚未归档的死亡档案 → 首次评估
                if (pawn.ArchiveStatus == Domain.PawnArchiveStatus.Active)
                {
                    float silverValue = EvaluatePawnSilverValue(pawn);
                    if (silverValue < threshold)
                    {
                        // 低价值：标记归档 + 设过期时间
                        pawn.ArchiveStatus = Domain.PawnArchiveStatus.Archived;
                        pawn.ArchiveExpiryTick = pawn.DeathTick + purgeTicks;
                        archivedCount++;
                        MarkChanged();
                    }
                    else
                    {
                        // 高价值：标记归档但永不过期
                        pawn.ArchiveStatus = Domain.PawnArchiveStatus.Archived;
                        pawn.ArchiveExpiryTick = -1L; // -1 = 永不过期
                        archivedCount++;
                        MarkChanged();
                    }
                    continue;
                }

                // 阶段 2：已归档且有过期时间 → 检查是否到期
                if (pawn.ArchiveStatus == Domain.PawnArchiveStatus.Archived
                    && pawn.ArchiveExpiryTick > 0L && tick >= pawn.ArchiveExpiryTick)
                {
                    pawn.ArchiveStatus = Domain.PawnArchiveStatus.PendingPurge;
                    MarkChanged();
                }

                // 阶段 3：待销毁 → 真正删除
                if (pawn.ArchiveStatus == Domain.PawnArchiveStatus.PendingPurge)
                {
                    purgedIds.Add(pawn.StableId);
                    Objects.RemoveAt(i);
                    objectsByStableId.Remove(pawn.StableId);
                    purgedCount++;
                }
            }

            // 清理被销毁 pawn 的关联事件
            if (purgedIds.Count > 0)
            {
                System.Collections.Generic.HashSet<string> purgeSet =
                    new System.Collections.Generic.HashSet<string>(purgedIds);
                for (int i = Events.Count - 1; i >= 0; i--)
                {
                    if (EventReferencesAny(Events[i], purgeSet))
                    {
                        Events.RemoveAt(i);
                    }
                }
                ChronicleLog.Save("auto-archive purged " + purgedIds.Count
                    + " low-value dead pawn archives (threshold " + threshold + " silver).");
                MarkChanged();
            }
        }

        /// <summary>
        /// 粗略评估一个已死亡 PawnObject 的银币价值。
        /// 基于 HealthValuation 系统（如果有技能/事件数据则加权）；
        /// 无完整数据时 fallback 到基础估值（事件数 × 系数）。
        /// </summary>
        private static float EvaluatePawnSilverValue(Domain.PawnObject pawn)
        {
            // 注意：此处不能直接调 service（GameComponent 层无 service 引用），
            // 用轻量启发式——后续可注入 IArchiveService 做精确评估。
            // 当前简化：死因非空(+50) + 有技能快照(+30) + 有工作历史(+20) + 有社交关系(+20)
            float value = 0f;
            if (!string.IsNullOrEmpty(pawn.DeathCauseKey)) value += 50f;
            if (pawn.SkillSnapshot != null && pawn.SkillSnapshot.Count > 0) value += 30f;
            if (pawn.WorkplaceHistory != null && pawn.WorkplaceHistory.Count > 0) value += 20f;
            if (pawn.Relations != null && pawn.Relations.Count > 0) value += 20f;
            // 有授勋记录 = 高价值人物
            if (pawn.GrantedMedals != null && pawn.GrantedMedals.Count > 0) value += 100f;
            if (pawn.CareerData != null && pawn.CareerData.GrantedTitles != null && pawn.CareerData.GrantedTitles.Count > 0) value += 80f;
            return value;
        }


    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Application.PawnEconomy;
using PersonalChronicle.Automation;
using PersonalChronicle.Data;
using PersonalChronicle.Domain.PawnEconomy;
using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Economy.PawnEconomy
{
    public sealed partial class GameComponent_PawnEconomy : GameComponent
    {
        private PawnEconomySaveData save = new PawnEconomySaveData();
        private PawnSettlementSaveHost settlement = new PawnSettlementSaveHost();
        private EconomyService service;
        private readonly List<Building_EconomyInstitution> anchors = new List<Building_EconomyInstitution>();
        private readonly Dictionary<string, Pawn> pawnIndex = new Dictionary<string, Pawn>();
        private EconomyState State => save.State;
        private EconomyService Service => service ?? (service = new EconomyService(State));
        public static GameComponent_PawnEconomy Current => Verse.Current.Game?.GetComponent<GameComponent_PawnEconomy>();
        public GameComponent_PawnEconomy(Game game) { perceptionGame = game; }
        public override void ExposeData()
        {
            Scribe_Deep.Look(ref save, "pawnEconomicInstitutions");
            if (save == null) save = new PawnEconomySaveData();
            if (Scribe.mode == LoadSaveMode.Saving) settlement.CaptureLegacy(save.OriginalPayload, "FirstSaveSnapshot");
            Scribe_Deep.Look(ref settlement, "pawnEconomicSettlement");
            if (settlement == null) settlement = new PawnSettlementSaveHost();
            if (Scribe.mode == LoadSaveMode.LoadingVars) settlement.CaptureLegacy(save.OriginalPayload, "BeforeLegacyRecovery");
            Scribe_Deep.Look(ref institutionMode, "pawnEconomicMode");
            if (institutionMode == null) institutionMode = new PersonalChronicle.Data.PawnEconomy.InstitutionModeSaveData();
            Scribe_Deep.Look(ref treasuryOperations, "pawnEconomicTreasuryOperations");
            if (treasuryOperations == null) treasuryOperations = new PersonalChronicle.Data.PawnEconomy.TreasuryOperationsSaveData();
            // CHG-20261012-ECON-CONTRACTIZE · B2（机构政策净删）：该节点**不再写入**（保存分支不声明它 ⇒
            // 首存即退役）。仅读档时用极简探针消费旧档的同一节点：只读、不写回、读到才记一行诊断
            //（缺节点 ⇒ 探针为 null，不抛）。旧政策数值一律**不迁移**。
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                PersonalChronicle.Data.PawnEconomy.LegacyPolicyProbe legacyPolicyProbe = null;
                Scribe_Deep.Look(ref legacyPolicyProbe, PersonalChronicle.Data.PawnEconomy.LegacyPolicyProbe.NodeName);
                if (legacyPolicyProbe != null) legacyPolicyProbe.Report();
            }
            Scribe_Deep.Look(ref planningLatches, "pawnEconomicPlanningLatches");
            if (planningLatches == null) planningLatches = new PersonalChronicle.Data.PawnEconomy.ShadowPlanningSaveData();
            Scribe_Deep.Look(ref workOwnership, "pawnEconomicWorkOwnership");
            if (workOwnership == null) workOwnership = new PersonalChronicle.Data.PawnEconomy.WorkOwnershipSaveData();
            // M9 §6：学习账与退避账落盘。**导出必须在 Scribe_Deep 之前**（否则写的是上一次的快照），
            // 恢复放在 PostLoadInit。旧档无此节点时字段保持默认值 ⇒ 旧档可加载（SAVE-003）。
            if (Scribe.mode == LoadSaveMode.Saving) ExportLearningInto(learning);
            Scribe_Deep.Look(ref learning, "pawnEconomicLearning");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) RestoreLearningFrom(learning);
            // U6-A：M10 授权账落盘（同一范式：导出在 Scribe_Deep 之前、恢复在 PostLoadInit；旧档无节点 ⇒ 空账）。
            if (Scribe.mode == LoadSaveMode.Saving) ExportProductionAuthorisationsInto(productionAuthorisations);
            Scribe_Deep.Look(ref productionAuthorisations, "pawnEconomicProductionAuthorisations");
            if (productionAuthorisations == null) productionAuthorisations = new PersonalChronicle.Data.PawnEconomy.ProductionAuthorisationSaveData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) RestoreProductionAuthorisationsFrom(productionAuthorisations);
            Scribe_Deep.Look(ref employment, "pawnEconomicEmployment");
            if (employment == null) employment = new PersonalChronicle.Data.PawnEconomy.EmploymentSaveData();
            ExposeWorkPrices();
            ExposeWorkOrderTrips();
            Scribe_Deep.Look(ref care, "pawnEconomicCare");
            if (care == null) care = new PersonalChronicle.Data.PawnEconomy.CareSaveData();
            // CHG-20261010-ECON-JOB-INTENT：玩家下达的 designation 来源集合（劳务站待办口径）。
            // 旧档没有该节点 ⇒ 空集合（诚实：我们从未记录过旧档的来源，不做任何猜测性预填）。
            ExposePlayerDesignations();
            // CHG-20261011-LABOR-VISUAL：劳务履约流程的独立版本化节点（流程阶段 / 合同纸代次 / 产出批次 / 交验回执）。
            // 旧档无该节点 ⇒ 空载体（自然语义："从未有过在办流程"），一切既有权利与付款行为分毫不变；
            // 未知（更高）schemaVersion ⇒ 载体自身阻断写入并保留原始载荷（见 LaborFlowSaveData.PayloadIntact）。
            ExposeLaborFlow();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InvalidatePerception(); service = null; modeService = null; modeInitialized = false;
                modeSnapshot = null; modeSnapshotState = null;
                cutoverPlan = null; employmentService = null; careService = null;
                laborFlowService = null;
            }
        }
        public override void FinalizeInit()
        {
            settlement.BindToMainThread();
            QueuePerceptionInitialization();
            RefreshPawns();
            anchors.Clear();
            foreach (var map in Find.Maps)
                foreach (var anchor in map.listerThings.AllThings.OfType<Building_EconomyInstitution>()) Register(anchor);
            if (LegacyFeesAllowed && State.Ready) Reconcile();
            QueueDefaultInstitutionRequest();
        }
        /// <summary>
        /// CHG-20261010-ECON-DEFAULT-ON（D1=A，用户需求「经济系统默认启用」）：**载入后按设置自动请求制度模式**，
        /// 让新档与"从未启用过"的存量档开箱即用。
        ///
        /// <para><b>为什么必须排到主线程</b>：<c>FinalizeInit</c> 可能跑在长事件载入 worker 上
        /// （既有先例 <c>QueuePerceptionInitialization</c>：同文件注释 "FinalizeInit may run on the long-event
        /// loading worker"）；而制度请求会 <c>Freeze</c> 制度并读写结算宿主，后者带主线程所有权断言。
        /// 因此沿用同一模式：<c>LongEventHandler.ExecuteWhenFinished</c> + 实例同一性校验。</para>
        ///
        /// <para><b>三道闸门（缺一不可）</b>：
        /// ① <c>Settings.EnablePawnEconomicInstitution == true</c> —— 设置是"玩家意图"的唯一来源，
        ///    显式关闭者永不被自动重开；
        /// ② 制度从未被请求过（<c>!Frozen &amp;&amp; !Desired</c>）—— 这是"默认"的边界：只覆盖**首次**，
        ///    不覆盖玩家在制度页/设置页做过的任何既成决定；
        /// ③ 坏 payload 不碰（<c>PreserveRaw</c>）。</para>
        ///
        /// <para><b>幂等</b>：<c>ApplyInstitutionModeSetting</c> 仅在 <c>!Frozen &amp;&amp; !PreserveRaw</c> 时真正
        /// <c>Freeze</c>；首次请求后 <c>Frozen</c> 为真，后续载入与重复回调都不会再次触发。
        /// 注意默认请求 ≠ 默认授予能力：制度仍须走 Freeze → 旧权益承接 → 恢复证明，能力闸门不变。</para>
        /// </summary>
        private void QueueDefaultInstitutionRequest()
        {
            LongEventHandler.ExecuteWhenFinished(TryDefaultInstitutionRequest);
        }
        private void TryDefaultInstitutionRequest()
        {
            // 过期回调防护：当前 Game 的组件必须仍是本实例（同 TryInitializePerception 范式）。
            if (!UnityData.IsInMainThread || !ReferenceEquals(Current, this)) return;
            if (PersonalChronicleMod.Settings?.EnablePawnEconomicInstitution != true) return;
            if (institutionMode.Frozen || institutionMode.State.Desired || institutionMode.PreserveRaw) return;
            ApplyInstitutionModeSetting(true);
        }
        // CHG-20261010-ECON-DEFAULT-ON（用户需求「经济系统默认启用」）设计结论 —— 旧引擎方向**故意不写盘**：
        //
        // 一度在此实现过「载入时自动调用 EconomyService.Enable(true)」（= 旧经济引擎的开盘预算注入 + Enabled/Funded），
        // 复核后回退。两条理由（GATE-DATA/SAVE 2026-10-10 逐条给出证据）：
        //   ① 旧经济引擎在新体系下是"参考实现，永不作为支付所有者"（`LegacyFeesAllowed => false`），
        //      其 Enabled/Funded 不驱动任何玩家可见能力 ⇒ 对"默认启用"无正面作用；
        //   ② 注入的 1000 银公共账会被旧权益承接识别为一笔 LegacyCashClaim（非零余额账户一律生成 claim）
        //      ⇒ 给 cutover 计划平添待承接项；且"已冻结 + 尚未启用"的存档会因 Revision 变化被判
        //      `LegacyStateChangedAfterFreeze`（**不可解除**，切换设置与显式重核验都救不回）。
        // 所以「默认启用」由**新制度链**承担：设置项 `ChronicleSettings.EnablePawnEconomicInstitution`
        // 默认 true（字段初值与 ExposeData 的 defaultValue 双处一致），意图经既有
        // `ApplyInstitutionModeSetting` 生效；制度进入 Active 仍需走既有的 Freeze → 旧权益承接流程。
        public void Register(Building_EconomyInstitution anchor)
        {
            if (!anchors.Contains(anchor)) anchors.Add(anchor);
            if (anchor.Faction == Faction.OfPlayer)
                foreach (var item in anchor.Stock.ToArray())
                    GameComponent_EconomyStorage.Current?.ReceivePublicSource(anchor.GetUniqueLoadID(), 0, item);
            if (!LegacyFeesAllowed || !State.Ready) return;
            var model = Institution(anchor);
            if (model == null && State.All<EconomyInstitution>().Count() < 256)
                State.Add(new EconomyInstitution { AnchorKey = anchor.GetUniqueLoadID(), MapId = anchor.Map.uniqueID,
                    InstitutionKind = anchor.def.GetModExtension<EconomyInstitutionExtension>().kind });
        }
        public void Unregister(Building_EconomyInstitution anchor)
        {
            anchors.Remove(anchor);
            if (!LegacyFeesAllowed || !State.Ready) return;
            var model = Institution(anchor);
            if (model != null) { model.Open = false; CancelInstitution(model.Id, "InstitutionClosed"); }
        }
        private void CancelInstitution(long id, string reason)
        {
            foreach (var order in State.All<EconomyOrder>().Where(o => o.Pending && o.InstitutionId == id).ToList()) Service.Cancel(order, reason);
            foreach (var position in State.All<EconomyPosition>().Where(p => p.InstitutionId == id)) position.Open = false;
            foreach (var contract in State.All<EconomyContract>().Where(c => c.Active).ToList())
                if (State.Find<EconomyPosition>(contract.PositionId)?.InstitutionId == id) EndContract(contract);
            State.Revision++;
        }
        private EconomyInstitution Institution(Building_EconomyInstitution anchor) => State.All<EconomyInstitution>().FirstOrDefault(i => i.AnchorKey == anchor.GetUniqueLoadID());
        private Building_EconomyInstitution Anchor(long id)
        {
            string key = State.Find<EconomyInstitution>(id)?.AnchorKey;
            return anchors.FirstOrDefault(a => a.Spawned && a.GetUniqueLoadID() == key);
        }
        private void RefreshPawns()
        {
            pawnIndex.Clear();
            foreach (var map in Find.Maps)
                foreach (var pawn in map.mapPawns.FreeColonistsSpawned) pawnIndex[pawn.GetUniqueLoadID()] = pawn;
        }
        private Pawn PawnByKey(string key) => key != null && pawnIndex.TryGetValue(key, out var pawn) ? pawn : null;
        private Zone_Workshop Zone(EconomyPosition p) => Find.Maps.FirstOrDefault(m => m.uniqueID == p.MapId)?.zoneManager.AllZones.OfType<Zone_Workshop>().FirstOrDefault(z => z.ID == p.ZoneId);
        private bool Open(EconomyInstitution i) => i != null && i.Open && Anchor(i.Id) is Building_EconomyInstitution b
            && !b.IsBurning() && EconomyRules.InHours(GenLocalDate.HourOfDay(b), i.StartHour, i.EndHour);
        private static bool BasicPawn(Pawn p) => p != null && p.Spawned && p.IsFreeColonist && !p.Dead && !p.Downed
            && !p.Drafted && !p.InMentalState && p.DevelopmentalStage == DevelopmentalStage.Adult && !p.IsQuestLodger()
            && p.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && p.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
        public static bool SafeDecision(Pawn p)
            => SafeEconomicAction(p, false);

        // Earned work needs a delivery window during Work as well as Anything.
        // Keep the same food, rest, emergency and player-order protections.
        public static bool SafeFulfillmentDecision(Pawn p)
            => SafeEconomicAction(p, true);

        private static bool SafeEconomicAction(Pawn p, bool allowWork)
        {
            if (!BasicPawn(p) || p.CurJob?.playerForced == true) return false;
            var assignment = p.timetable?.CurrentAssignment;
            if ((assignment != TimeAssignmentDefOf.Anything && !(allowWork && assignment == TimeAssignmentDefOf.Work))
                || (p.needs.food != null && p.needs.food.CurLevelPercentage < .5f)
                || (p.needs.rest != null && p.needs.rest.CurLevelPercentage < .4f)) return false;
            if (p.Map.listerThings.ThingsInGroup(ThingRequestGroup.Fire).Count != 0 || p.Map.dangerWatcher.DangerRating == StoryDanger.High) return false;
            foreach (var patient in p.Map.mapPawns.FreeColonistsSpawned)
                if (patient.Downed && !patient.InBed() || p.workSettings?.WorkIsActive(WorkTypeDefOf.Doctor) == true && patient.health.HasHediffsNeedingTend()) return false;
            return true;
        }
        private bool CanJoin(Pawn pawn, EconomyPosition position)
        {
            if (position == null) return false;
            var zone = Zone(position);
            if (!BasicPawn(pawn) || pawn.Map.uniqueID != position.MapId || !WorkshopZoneUtility.IsAlive(zone)
                || zone.WorkshopType == null || !Open(State.Find<EconomyInstitution>(position.InstitutionId))) return false;
            var manager = MapComponent_WorkshopManager.Get(pawn.Map);
            if (State.All<EconomyReceipt>().Any(r => r.Type == "Wage" && !r.Paid && r.SubjectKey == pawn.GetUniqueLoadID())) return false;
            if (manager.Slots.Any(s => s.AssignedPawn == pawn) || manager.Rotations.Any(r => r.Pool.Contains(pawn))
                || pawn.Map.zoneManager.AllZones.OfType<Zone_Workshop>().Any(z => z.Crews.Any(c => c.Members.Contains(pawn)))) return false;
            return zone.WorkshopType.WorkTypeDefNamesOf(WorkshopTaskType.Production).Any(name => {
                var type = DefDatabase<WorkTypeDef>.GetNamedSilentFail(name);
                return type != null && !pawn.WorkTypeIsDisabled(type) && pawn.workSettings != null && pawn.workSettings.WorkIsActive(type);
            });
        }
        private bool HasReport(Pawn pawn) => State.All<EconomyReport>().Any(r => r.PawnKey == pawn.GetUniqueLoadID()
            && Find.TickManager.TicksGame < r.ExpiresTick && r.Moving >= .6f && r.Manipulation >= .6f && r.Consciousness >= .7f);
        private void EndContract(EconomyContract contract)
        {
            contract.Active = false;
            foreach (var map in Find.Maps)
            {
                var manager = MapComponent_WorkshopManager.Get(map);
                foreach (var slot in manager.Slots.Where(s => s.EconomicContractId == contract.Id).ToList()) manager.RemoveSlot(slot);
            }
            State.Revision++;
        }
        private void Reconcile()
        {
            foreach (var order in State.All<EconomyOrder>().Where(o => o.Pending).ToList())
                if (Find.TickManager.TicksGame >= order.ExpiresTick || !Open(State.Find<EconomyInstitution>(order.InstitutionId))
                    || !BasicPawn(PawnByKey(order.ClientKey))) Service.Cancel(order, "Unavailable");
            foreach (var contract in State.All<EconomyContract>().Where(c => c.Active).ToList())
            {
                var position = State.Find<EconomyPosition>(contract.PositionId);
                var pawn = PawnByKey(contract.PawnKey);
                // Missing actors, institutions or player-removed economic slots terminate only our own assignment.
                if (position == null || !position.Open || pawn == null || !WorkshopZoneUtility.IsAlive(Zone(position))
                    || Anchor(position.InstitutionId) == null
                    || !MapComponent_WorkshopManager.Get(pawn.Map).Slots.Any(s => s.EconomicContractId == contract.Id)) EndContract(contract);
            }
            foreach (var map in Find.Maps)
            {
                var manager = MapComponent_WorkshopManager.Get(map);
                foreach (var slot in manager.Slots.Where(s => s.EconomicContractId != 0).ToList())
                    if (State.Find<EconomyContract>(slot.EconomicContractId)?.Active != true || !State.Enabled) manager.RemoveSlot(slot);
            }
            // Dropping a purchased garment does not surrender its title. Ownership survives caravans and map changes.
        }
        public override void GameComponentTick()
        {
            settlement.BindToMainThread();
            WorkAuthorityService.Tick(Find.TickManager?.TicksGame ?? 0);
            TickPerception(); // Observation is independent of fee mode, ledger readiness and open windows.
            TickNeedSpeech(); // Presentation reads published needs and the actual current job.
            TickLaborActivity(); // Bounded observation of actual native contract jobs; never a GUI query.
            TickInstitutionMode();
            DrainWorkshopInterruptions(); // M9 §5：取走车间中断事实并定格（一次中断至多一条记录）。
            if (Find.TickManager != null && Find.TickManager.TicksGame % 250 == 0)
            {
                int now = Find.TickManager.TicksGame;
                TrimProductionAuthorisations(now);   // U6-A：授权账年龄裁剪（防持久化满额永久锁死）
                TrimCareAuthorizations(now);         // U6-A4：照护授权到期裁剪（表不再被历史行填满）
                RefreshUnwiredServiceCapabilities(); // U6-A4：非 CaptureWired 类型的周期复核（诚实状态落表）
                TrimExpiredContracts(now);           // ECON-R08：过期合同的生命周期清理（离图 Pawn 也在内）
                TrimLaborIntents(now);               // LABOR-VISUAL：超期未进站的**纯意图**撤销（有界；已签约一律不撤）
                TrimGeneratedOutputs(now);           // LABOR-VISUAL：过期产物笔记清理（内存态缓冲，有界）
                ArchiveSettledFlows(now);            // LABOR-VISUAL：权利结清的流程归档（有欠薪/待交货者一律保留）
                EnrollObservedPawns();               // FA-01：合法新 Pawn 的零余额账户（明确注册边界，非查询副作用）
                Employment.TrimSettledEvidenceKeys(); // LINKFIX-B：证据键接近上限时裁掉已结清项（防长局永久停付）
            }
            TickShadowPlanning(); // Read-only planning remains independent of fee execution and open windows.
            if (!LegacyFeesAllowed || !State.Ready || !State.Enabled || Find.TickManager.TicksGame % 250 != 0) return;
            RefreshPawns(); Reconcile(); Service.SettlePayables(); Service.TrimHistory();
        }
        public Job Plan(Pawn pawn)
        {
            // CHG-20261011-LABOR-VISUAL：新账本分支先接续**已开始的办理意图**（到站签约）与**已赚待交验**
            // （到工资站交验），再考虑既有四类交付 Job。两支各自**自门**（SafeDecision + 意图/应付账 + 可达性），
            // 因此不依赖 Ready/Enabled —— 与分支内既有 planner 的纪律一致（ARCH 席③）。
            // Finish earned rights first. Deliver a carried batch before collecting the next one;
            // new recruitment must not displace an outstanding delivery.
            if (!LegacyFeesAllowed) return PlanContractReissue(pawn) ?? PlanContractSubmission(pawn)
                ?? PlanContractCollectOutputs(pawn) ?? PlanLaborVisit(pawn)
                ?? PlanTreasuryDelivery(pawn) ?? PlanPublicSilverDelivery(pawn)
                ?? PlanPublicApparelPurchase(pawn) ?? PlanPreservedAssetUse(pawn);
            if (!State.Ready || !State.Enabled || !SafeDecision(pawn)) return null;
            string key = pawn.GetUniqueLoadID(); int now = Find.TickManager.TicksGame;
            pawnIndex[key] = pawn;
            if (!State.All<EconomyPerson>().Any(p => p.PawnKey == key) && State.All<EconomyPerson>().Count() >= 4096) return null;
            var person = Service.Person(key);
            var pending = State.All<EconomyOrder>().FirstOrDefault(o => o.Pending && o.ClientKey == key);
            if (pending != null) return JobFor(pending, false);
            if (!person.Autonomy || now < person.NextDecisionTick) return null;
            person.NextDecisionTick = now + EconomyRules.DecisionInterval;
            if (!string.IsNullOrEmpty(person.AcquiredAsset))
            {
                var held = pawn.inventory?.innerContainer.OfType<Apparel>().FirstOrDefault(a => a.GetUniqueLoadID() == person.AcquiredAsset);
                if (held != null && Wearable(pawn, held)) return MakeJob("PC_PEA_WearOwned", held);
                var dropped = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel).OfType<Apparel>()
                    .FirstOrDefault(a => a.GetUniqueLoadID() == person.AcquiredAsset);
                if (dropped != null)
                {
                    person.Goal = EconomicGoalStatus.AcquiredPendingUse;
                    return Wearable(pawn, dropped) && pawn.CanReach(dropped, PathEndMode.Touch, Danger.None)
                        ? MakeJob("PC_PEA_WearOwned", dropped) : null;
                }
                if (pawn.apparel.WornApparel.Any(a => a.GetUniqueLoadID() == person.AcquiredAsset))
                { person.Goal = EconomicGoalStatus.Satisfied; person.AcquiredAsset = null; }
                else if (held == null) { person.AcquiredAsset = null; person.Goal = EconomicGoalStatus.None; }
                return null;
            }
            return RecoverOwnedClothing(pawn, person) ?? PlanTradeOrEmployment(pawn, person, key, now);
        }
        private Job RecoverOwnedClothing(Pawn pawn, EconomyPerson person)
        {
            var ownedKeys = new HashSet<string>(State.All<EconomyOwnership>().Where(o => o.OwnerKey == pawn.GetUniqueLoadID()).Select(o => o.AssetKey));
            if (ownedKeys.Count == 0) return null;
            var candidates = pawn.inventory.innerContainer.OfType<Apparel>().Concat(pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel).OfType<Apparel>());
            var best = candidates.Where(a => ownedKeys.Contains(a.GetUniqueLoadID()) && Gain(pawn, a) >= .05f
                && (!a.Spawned || pawn.CanReach(a, PathEndMode.Touch, Danger.None))).Take(128).OrderByDescending(a => Gain(pawn, a)).FirstOrDefault();
            if (best == null) return null;
            person.AcquiredAsset = best.GetUniqueLoadID(); person.Goal = EconomicGoalStatus.AcquiredPendingUse;
            return MakeJob("PC_PEA_WearOwned", best);
        }
        private Job PlanTradeOrEmployment(Pawn pawn, EconomyPerson person, string key, int now)
        {
            var shop = anchors.Where(a => a.Spawned && a.Map == pawn.Map && Open(Institution(a)) && Institution(a).InstitutionKind == InstitutionKind.Shop)
                .SelectMany(a => a.Stock.OfType<Apparel>().Take(64).Select(item => new { Shop = a, Item = item, Gain = Gain(pawn, item) }))
                .Take(128).Where(x => x.Gain >= .05f).OrderByDescending(x => x.Gain).ThenBy(x => x.Item.thingIDNumber).FirstOrDefault();
            if (shop == null) { person.Goal = EconomicGoalStatus.WaitingSupply; return null; }
            var store = Institution(shop.Shop);
            if (Service.Account(key).Available >= store.Price)
            {
                var order = Service.Prepare(EconomyOrderKind.Purchase, key, store, key, store.Price, now, shop.Item.GetUniqueLoadID());
                return JobFor(order, false);
            }
            person.Goal = EconomicGoalStatus.WaitingIncome;
            if (State.All<EconomyContract>().Any(c => c.Active && c.PawnKey == key)) return null;
            var office = anchors.FirstOrDefault(a => a.Map == pawn.Map && Institution(a)?.InstitutionKind == InstitutionKind.LaborOffice && Open(Institution(a)));
            var offer = State.All<EconomyPosition>().Where(p => Service.HasVacancy(p) && CanJoin(pawn, p)).OrderByDescending(p => p.Wage).ThenBy(p => p.Id).FirstOrDefault();
            if (office == null || offer == null) return null;
            if (offer.RequiresExam && !HasReport(pawn))
            {
                person.Goal = EconomicGoalStatus.WaitingExamination;
                var clinic = anchors.FirstOrDefault(a => a.Map == pawn.Map && Institution(a)?.InstitutionKind == InstitutionKind.ExamCenter && Open(Institution(a)) && DoctorValid(Institution(a)));
                return clinic == null ? null : JobFor(Service.Prepare(EconomyOrderKind.Examination, key, Institution(clinic), EconomyState.PublicAccount,
                    Institution(clinic).Price, now, provider: Institution(clinic).DoctorKey), false);
            }
            return JobFor(Service.Prepare(EconomyOrderKind.Employment, key, Institution(office), key, 0, now, position: offer.Id), false);
        }
        private static Job MakeJob(string def, Thing target) => JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed(def), target);
        private Job JobFor(EconomyOrder order, bool doctor)
        {
            if (order == null || !order.Pending || Anchor(order.InstitutionId) == null) return null;
            var job = MakeJob(doctor ? "PC_PEA_PerformExam" : "PC_PEA_Visit", Anchor(order.InstitutionId));
            job.count = (int)order.Id; return job;
        }
        public bool OrderActive(int id) => LegacyFeesAllowed && State.Ready && State.Enabled && State.Find<EconomyOrder>(id) is EconomyOrder o
            && o.Pending && Find.TickManager.TicksGame < o.ExpiresTick;
        public bool JobOrderValid(int id) => LegacyFeesAllowed && State.Ready && State.Enabled && State.Find<EconomyOrder>(id) is EconomyOrder o
            && (o.Status == EconomyOrderStatus.Committed || o.Pending && Find.TickManager.TicksGame < o.ExpiresTick);
        public bool IsPurchase(int id) => State.Find<EconomyOrder>(id)?.OrderKind == EconomyOrderKind.Purchase;
        public void WearAcquired(Pawn pawn)
        {
            if (!State.Ready || !BasicPawn(pawn)) return;
            string key = State.All<EconomyPerson>().FirstOrDefault(x => x.PawnKey == pawn.GetUniqueLoadID())?.AcquiredAsset;
            var item = pawn.inventory?.innerContainer.OfType<Apparel>().FirstOrDefault(a => a.GetUniqueLoadID() == key);
            if (item != null) WearOwned(pawn, item);
        }
        public void CancelJob(int id) { if (LegacyFeesAllowed && State.Ready) Service.Cancel(State.Find<EconomyOrder>(id), "Interrupted"); }
        public bool FinishVisit(int id, Pawn pawn)
        {
            var order = State.Find<EconomyOrder>(id);
            if (!OrderActive(id) || order.ClientKey != pawn.GetUniqueLoadID() || !BasicPawn(pawn)
                || !Open(State.Find<EconomyInstitution>(order.InstitutionId))) return false;
            if (order.OrderKind == EconomyOrderKind.Examination) return false;
            if (order.OrderKind == EconomyOrderKind.Employment)
            {
                var position = State.Find<EconomyPosition>(order.PositionId);
                if (!CanJoin(pawn, position) || position.RequiresExam && !HasReport(pawn)) return false;
                var contract = Service.Sign(order, Find.TickManager.TicksGame);
                if (contract == null) return false;
                MapComponent_WorkshopManager.Get(pawn.Map).AddSlot(new WorkshopSlot { AssignedPawn = pawn, TargetZone = Zone(position),
                    StartHour = position.StartHour, EndHour = position.EndHour, EconomicContractId = contract.Id });
                return true;
            }
            var shop = Anchor(order.InstitutionId);
            var item = shop.Stock.OfType<Apparel>().FirstOrDefault(a => a.GetUniqueLoadID() == order.AssetKey);
            if (item == null || !Wearable(pawn, item) || !Service.CanCommit(order) || pawn.inventory == null
                || State.All<EconomyOwnership>().Count() >= 8192) return false;
            if (pawn.inventory.innerContainer.TryAddOrTransfer(item, 1, false) != 1) return false;
            if (!Service.Commit(order, Find.TickManager.TicksGame)) { shop.Stock.TryAddOrTransfer(item, 1, false); return false; }
            State.Add(new EconomyOwnership { AssetKey = item.GetUniqueLoadID(), OwnerKey = order.ClientKey });
            item.SetForbidden(true, false); // Keep private goods out of vanilla public apparel optimization after dropping.
            var person = Service.Person(order.ClientKey); person.AcquiredAsset = item.GetUniqueLoadID();
            person.Goal = EconomicGoalStatus.AcquiredPendingUse; person.NextDecisionTick = 0; return true;
        }
        public bool IsExam(int id) => State.Find<EconomyOrder>(id)?.OrderKind == EconomyOrderKind.Examination;
        private bool DoctorValid(EconomyInstitution i)
        {
            var doctor = PawnByKey(i.DoctorKey);
            return BasicPawn(doctor) && doctor.Map.uniqueID == i.MapId && !doctor.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)
                && doctor.workSettings?.WorkIsActive(WorkTypeDefOf.Doctor) == true && doctor.skills.GetSkill(SkillDefOf.Medicine).Level >= 4;
        }
        public IEnumerable<Thing> Clinics(Pawn doctor) => LegacyFeesAllowed ? anchors.Where(a => a.Spawned && a.Map == doctor.Map && Institution(a)?.DoctorKey == doctor.GetUniqueLoadID()) : Enumerable.Empty<Thing>();
        public Job DoctorJob(Pawn doctor, Thing anchor)
        {
            if (!LegacyFeesAllowed) return null;
            var institution = Institution((Building_EconomyInstitution)anchor);
            if (!State.Ready || !State.Enabled || !Open(institution) || !DoctorValid(institution) || doctor.CurJob?.playerForced == true) return null;
            var order = State.All<EconomyOrder>().FirstOrDefault(o => o.Pending && o.OrderKind == EconomyOrderKind.Examination
                && o.InstitutionId == institution.Id && o.ProviderKey == doctor.GetUniqueLoadID());
            return JobFor(order, true);
        }
        public void Pulse(int id, Pawn pawn, bool doctor)
        {
            var order = State.Find<EconomyOrder>(id);
            if (!OrderActive(id)) return;
            var institution = State.Find<EconomyInstitution>(order.InstitutionId);
            var client = PawnByKey(order.ClientKey); var provider = PawnByKey(order.ProviderKey); var counter = Anchor(order.InstitutionId);
            if (!Open(institution) || !DoctorValid(institution) || !BasicPawn(client) || !BasicPawn(provider)
                || (doctor ? order.ProviderKey : order.ClientKey) != pawn.GetUniqueLoadID()) { Service.Cancel(order, "Unavailable"); return; }
            int now = Find.TickManager.TicksGame;
            if (doctor) order.ProviderPulse = now; else order.ClientPulse = now;
            if (now == order.LastProgressTick || order.ClientPulse < now - 1 || order.ProviderPulse < now - 1
                || !client.Position.AdjacentTo8WayOrInside(counter.Position) || !provider.Position.AdjacentTo8WayOrInside(counter.Position)) return;
            order.LastProgressTick = now; order.Progress++;
            if (order.Progress < EconomyRules.ExamDuration) return;
            // Validate both transfers before committing. No medical mutation or hidden-condition disclosure.
            var doctorAccount = Service.Account(order.ProviderKey); var seller = Service.Account(order.SellerKey);
            long wage = order.Amount * 3 / 5;
            if (!Service.CanCommit(order) || doctorAccount.Balance > EconomyRules.MaxAmount - wage
                || doctorAccount.Income > long.MaxValue - wage || seller.Expense > long.MaxValue - wage)
            { Service.Cancel(order, "PaymentUnavailable"); return; }
            if (!Service.Commit(order, now)) return;
            Service.PayDoctor(order, now);
            State.Add(new EconomyReport { PawnKey = order.ClientKey, DoctorKey = order.ProviderKey, OrderId = order.Id,
                Tick = now, ExpiresTick = now + EconomyRules.ReportLifetime,
                Moving = client.health.capacities.GetLevel(PawnCapacityDefOf.Moving),
                Manipulation = client.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation),
                Consciousness = client.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness),
                VisibleConditions = client.health.hediffSet.hediffs.Where(h => h.Visible).Select(h => h.def.defName).Distinct().ToList() });
            Service.Person(order.ClientKey).NextDecisionTick = 0;
        }
        public static bool Wearable(Pawn pawn, Apparel item)
        {
            return item != null && !item.Destroyed && pawn.apparel != null && pawn.outfits != null
                && pawn.outfits.CurrentApparelPolicy.filter.Allows(item) && item.PawnCanWear(pawn, false) && ApparelUtility.HasPartsToWear(pawn, item.def)
                && item.def.apparel.developmentalStageFilter.Has(pawn.DevelopmentalStage)
                && (item.def.apparel.gender == Gender.None || item.def.apparel.gender == pawn.gender)
                && (!CompBiocodable.IsBiocoded(item) || CompBiocodable.IsBiocodedFor(item, pawn))
                && pawn.apparel.WornApparel.All(a => ApparelUtility.CanWearTogether(a.def, item.def, pawn.RaceProps.body)
                    || pawn.outfits.forcedHandler.AllowedToAutomaticallyDrop(a) && !pawn.apparel.IsLocked(a));
        }
        private static float Gain(Pawn pawn, Apparel item)
        {
            if (!Wearable(pawn, item)) return -1f;
            // Local utility uses real durability, armor and insulation. Vanilla's public score uses private
            // shared neededWarmth state, so calling it here would depend on whichever pawn optimized last.
            float gain = ClothingScore(item);
            foreach (var worn in pawn.apparel.WornApparel)
                if (!ApparelUtility.CanWearTogether(worn.def, item.def, pawn.RaceProps.body)) gain -= ClothingScore(worn);
            return gain;
        }
        private static float ClothingScore(Apparel a) => .6f * (a.def.useHitPoints ? (float)a.HitPoints / a.MaxHitPoints : 1f)
            + .3f * a.GetStatValue(StatDefOf.ArmorRating_Sharp) + .01f * a.GetStatValue(StatDefOf.Insulation_Cold);
        public void WearOwned(Pawn pawn, Apparel item)
        {
            if (!State.Ready || !BasicPawn(pawn) || pawn.CurJob?.playerForced == true || !Wearable(pawn, item)
                || !State.All<EconomyOwnership>().Any(o => o.OwnerKey == pawn.GetUniqueLoadID() && o.AssetKey == item.GetUniqueLoadID())) return;
            if (!pawn.inventory.innerContainer.Contains(item))
            {
                if (!item.Spawned || item.Map != pawn.Map || !pawn.Position.AdjacentTo8WayOrInside(item.Position)) return;
                var oldPosition = item.Position; item.DeSpawn();
                if (!pawn.inventory.innerContainer.TryAdd(item, false))
                { GenPlace.TryPlaceThing(item, oldPosition, pawn.Map, ThingPlaceMode.Near); return; }
            }
            pawn.inventory.innerContainer.Remove(item);
            pawn.apparel.Wear(item);
            if (!pawn.apparel.WornApparel.Contains(item))
            { pawn.inventory.innerContainer.TryAdd(item, false); return; }
            if (!LegacyFeesAllowed) return; // Use already-owned goods without rewriting the frozen ledger.
            var person = Service.Person(pawn.GetUniqueLoadID()); person.Goal = EconomicGoalStatus.Satisfied;
            person.AcquiredAsset = null; person.NextDecisionTick = Find.TickManager.TicksGame + EconomyRules.DecisionInterval; State.Revision++;
        }
        public static void ProductionCompleted(Bill_Production bill, Pawn pawn)
        {
            try { Current?.OnProduction(bill, pawn); }
            catch (Exception ex) { Log.ErrorOnce("[PersonalChronicle] Economic production settlement: " + ex, 188100831); }
        }
        private void OnProduction(Bill_Production bill, Pawn pawn)
        {
            if (!LegacyFeesAllowed || !State.Ready || !State.Enabled || !BasicPawn(pawn) || pawn.CurJob?.def != JobDefOf.DoBill
                || bill == null || (bill.GetType() != typeof(Bill_Production) && bill.GetType() != typeof(Bill_ProductionWithUft))
                || bill.recipe?.products == null || bill.recipe.products.Count == 0
                || bill.recipe.Worker.GetType() != typeof(RecipeWorker) || !bill.recipe.specialProducts.NullOrEmpty()
                || bill.recipe.products.Any(p => p.count <= 0 || p.thingDef == null)
                || bill.recipe.efficiencyStat != null && pawn.GetStatValue(bill.recipe.efficiencyStat) <= 0
                || !(bill.billStack?.billGiver is Building_WorkTable table)
                || bill.recipe.workTableEfficiencyStat != null && table.GetStatValue(bill.recipe.workTableEfficiencyStat) <= 0) return;
            var contract = State.All<EconomyContract>().FirstOrDefault(c => c.Active && c.PawnKey == pawn.GetUniqueLoadID());
            var position = contract == null ? null : State.Find<EconomyPosition>(contract.PositionId);
            if (position == null || table.Map != pawn.Map || table.Position.GetZone(table.Map) != Zone(position)
                || !MapComponent_WorkshopManager.Get(pawn.Map).Slots.Any(s => s.EconomicContractId == contract.Id && s.AssignedPawn == pawn)
                || !EconomyRules.InHours(GenLocalDate.HourOfDay(pawn), position.StartHour, position.EndHour)) return;
            Service.CompleteProduction(contract, pawn.CurJob.loadID, Find.TickManager.TicksGame);
        }
    }
}

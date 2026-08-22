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

namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// Partial of <see cref="ArchiveUiDataProvider"/> 鈥?see main file for the class doc.
    /// </summary>
    public sealed partial class ArchiveUiDataProvider : IArchiveUiDataProvider
    {

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
                    // 职业档案 · 工作经历（简历式分段）：由 CareerData.Events + Workplace 派生。
                    snap.WorkExperiences = BuildWorkExperiences(pawn);
                    // 职业档案 · 总览（身份/资格/预检/下一职称）与职称链（5 档）。
                    snap.CareerOverview = BuildCareerOverview(pawn);
                    snap.TitleTiers = BuildTitleTiers(pawn);
                    snap.Milestones = BuildMilestones(events);
                    snap.Health = BuildHealth(service, detailObjectId);
                    snap.Relations = BuildRelations(service, pawn);
                    // v4.15 condense tab core KPI: aggregate the six digest cells here
                    // in the Read Model only — the ITab renders, never computes.
                    BuildDetailCoreKpis(snap, service, detailObjectId, pawn);
                    // v1.1.4 勋章体系：勋章墙视图（阈值类判定 → MedalView 列表，含未授予灰态）。
                    snap.Medals = BuildMedals(pawn);
                    // 职业事实计数：统一聚合 9 类事件（UI 只消费，禁止绘制路径直查 Domain）。
                    snap.FactCounts = BuildCareerFactCounts(pawn);
                }
                else if (snap.DetailObject is ThingObject thing)
                {
                    // v4.7: legacy chain (传承) for equipment — ownership-transfer
                    // generations, creator, verdict, holder table.
                    snap.Legacy = BuildLegacy(service, thing, events);
                    // v4.9: equipment legacy extension — 溯源 / 工坊署名链 /
                    // 同袍共用 / 退役仪式. All read-model derived, empty-safe.
                    snap.Origin = BuildOrigin(service, thing, events);
                    snap.MakerChain = BuildMakerChain(service, thing, snap.Origin);
                    snap.CoUse = BuildCoUse(service, thing, events);
                    snap.Decommission = BuildDecommission(service, thing, events);
                }
                else if (snap.DetailObject is LocationObject location)
                {
                    // v4.13 location atlas: identity/ownership/geography/lifecycle/
                    // commerce, all read-model derived (the window only renders).
                    snap.Location = BuildLocation(location);
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
                snap.Medals = new List<MedalView>();
            }
            return snap;
        }

        // —— v4.16 职业档案导航已移除（职业档案界面嵌入个人档案「生涯」tab）——
        // 殖民地级职业档案总览（CareerOverviewRowView / BuildCareerOverview(service, revision)）
        // 于本次修复整体删除：侧边栏「职业档案」入口与 MainView.Career 一并移除，
        // 职业身份/下一职称/资格状态改由个人档案「生涯」tab 消费单人生成 CareerOverviewView。

        // ---- v4.15 condense tab core KPI (Read Model only) ----
        // Aggregates the six digest cells consumed by ITab_Pawn_Chronicle. Every
        // counter is computed once here, never in the draw path.
        private static void BuildDetailCoreKpis(
            DetailSnapshot snap, IArchiveService service, string stableId, PawnObject pawn)
        {
            if (snap == null || pawn == null) return;

            // 工时: reuse the work-intensity evaluator via IWorkIntensityService.
            if (service != null && !string.IsNullOrEmpty(stableId))
            {
                IWorkIntensityService intensityService = service as IWorkIntensityService;
                if (intensityService != null)
                {
                    snap.WorkIntensity = intensityService.GetWorkIntensity(stableId); // null when undefined
                }
                ProductionSummaryView prod = service.GetProductionSummary(stableId);
                snap.ProductionTotal = (prod == null) ? 0 : prod.TotalQuantity;
                snap.ProductionSilverValue = (prod == null) ? 0f : prod.TotalMarketValue;
                // v4.15: 产出宫格真实数据源 —— 直接消费累加器累计（按类目聚合、按产值降序），
                // 不再用 BuildProductionLines 重扫事件流估算。bars 与种类数均据此快照。
                snap.ProductionTypeViews = (prod == null || prod.Types == null)
                    ? (IReadOnlyList<ProductionTypeView>)new List<ProductionTypeView>()
                    : prod.Types;
                snap.ProductionCategories = BuildProductionCategories(snap.ProductionLines);

                // 周产出/净值（滚动 7/30 天）：直接扫描近窗事件流，按 ThingDef.BaseMarketValue 估算银币；
                // 不修改 ProductionAccumulator，避免动持久化字段。窗口为 0 时二者默认 0。
                ComputeProductionWindowValue(snap);

                // v1.1.4 损耗宫格：直接消费 ConsumptionAccumulator 持久化数据（不扫事件流）。
                ConsumptionSummaryView cons = service.GetConsumptionSummary(stableId);
                snap.ConsumptionTotalSilver = (cons == null) ? 0f : cons.TotalSilver;
                snap.WeeklyConsumptionSilver = (cons == null) ? 0f : cons.WeeklySilver;
                snap.DailyConsumptionSilver = (cons == null) ? 0f : cons.DailySilver;
                snap.ConsumptionTypeViews = (cons == null || cons.Types == null)
                    ? (IReadOnlyList<ConsumptionTypeView>)new List<ConsumptionTypeView>()
                    : cons.Types;
            }

            // v1.1.4 劳模住所/工坊检测：消费 PawnObject 持久化快照，解析为展示视图。
            // 展示优先级：全局别名（工坊实例/房间类型）> per-pawn CustomName（兼容）> LabelCap 实时名。
            snap.Workplace = BuildWorkplaceView(service, pawn);
            snap.Residence = BuildResidenceView(service, pawn);

            // 击杀: Death events where this pawn is the killer (CombatRole == kill).
            // Counts the total and groups by victim faction/category for the digest.
            string killerLabel = (pawn.LabelShort ?? string.Empty).Trim();
            int kills = 0;
            Dictionary<string, int> byFaction = new Dictionary<string, int>();
            foreach (ChronicleEvent e in snap.RawEvents)
            {
                if (e == null || e.TypeKey != ChronicleEventType.Death) continue;
                if (!e.Params.TryGetValue(ChronicleEventParams.CombatRole, out string role)
                    || role != ChronicleEventParams.CombatRoleKill) continue;
                if (!e.Params.TryGetValue(ChronicleEventParams.Killer, out string killer)
                    || !string.Equals((killer ?? string.Empty).Trim(), killerLabel, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                kills++;
                // Group key: victim faction label when known, else victim category
                // (humanlike enemies are usually factioned; mechs/animals fall back).
                string group = null;
                if (e.Params.TryGetValue(ChronicleEventParams.VictimFactionLabel, out string vfl)
                    && !string.IsNullOrEmpty(vfl))
                {
                    group = vfl;
                }
                else if (e.Params.TryGetValue(ChronicleEventParams.VictimCategory, out string vcat)
                    && !string.IsNullOrEmpty(vcat))
                {
                    group = VictimCategoryLabel(vcat);
                }
                if (string.IsNullOrEmpty(group)) group = ChronicleEventParams.UnknownKillerLabel.Translate().ToString();
                int prev;
                byFaction.TryGetValue(group, out prev);
                byFaction[group] = prev + 1;
            }
            snap.Kills = kills;
            snap.KillsByFaction = BuildKillsByFaction(byFaction);

            // v6.8 个人战斗维度（击杀宫格）：直接消费 PawnObject 持久化字段（Capture 层累加）。
            // 老存档缺字段默认 0；MeleeKillRatio 无击杀时取 0.5 中性占位（UI 用 hasMeleeData 判断）。
            snap.ParticipatedBattles = pawn.ParticipatedBattles;
            snap.DamageDealtTotal = pawn.DamageDealtTotal;
            int meleeKills = pawn.MeleeKills;
            int rangedKills = pawn.RangedKills;
            int combatTotal = meleeKills + rangedKills;
            snap.MeleeKills = meleeKills;
            snap.RangedKills = rangedKills;
            snap.MeleeKillRatio = combatTotal > 0 ? (float)meleeKills / (float)combatTotal : 0.5f;

            // 战役: colony-level Battle events are not bound to a single pawn, so the
            // digest shows the colony's battle count as this pawn's era context.
            int battles = 0;
            foreach (ChronicleEvent e in snap.RawEvents)
            {
                if (e != null && e.TypeKey == ChronicleEventType.Battle) battles++;
            }
            snap.BattleCount = battles;

            // 战役 KPI 条（歼敌/损失/参战规模/重大战局）：colony 级，作为该人物的时代背景。
            // 复用 Overview 的 Battle 分类聚合，窗口只消费。
            IReadOnlyList<ArchiveObject> battleObjects = (service != null)
                ? service.GetObjectsOfCategory(ArchiveCategoryKeys.Battle)
                : null;
            if (battleObjects != null)
            {
                snap.BattleKpis = BuildBattleKpis(service, battleObjects.ToList());
            }

            // 传承: relations that read as a living descendant (child/offspring).
            int offspring = 0;
            foreach (RelationView r in snap.Relations)
            {
                if (r == null || !r.IsLive) continue;
                // 判定只用稳定键（PawnRelationDef.defName），禁止用已翻译的关系名做逻辑判断
                // （BASE-002 / GOV-009：程序逻辑不得依赖显示文本）。
                if (IsDescendantRelation(r.RelationDefName))
                {
                    offspring++;
                }
            }
            snap.LegacyOffspring = offspring;

            // 神器传承：以人物**当前持有的武器**为锚点展示传承信息（需求：人物档案第六格
            // 显示其武器传承链）。复用武器档案同款 BuildLegacy 聚合——找到该人物为持有者
            // （HolderRecords.StableId == 人物stableId，优先当前持有）的 ThingObject，
            // 对其事件流做传承聚合。窗口只消费 snap.Legacy，不在窗口内重算。
            if (service != null && !string.IsNullOrEmpty(stableId))
            {
                IReadOnlyList<ArchiveObject> things = service.GetObjectsOfCategory(ArchiveCategoryKeys.Thing);
                if (things != null)
                {
                    ThingObject anchorThing = null;
                    foreach (ArchiveObject o in things)
                    {
                        ThingObject t = o as ThingObject;
                        if (t == null || t.HolderRecords == null || t.HolderRecords.Count == 0) continue;
                        bool isCurrentHolder = !string.IsNullOrEmpty(t.CurrentHolderId) && t.CurrentHolderId == stableId;
                        bool wasHolder = false;
                        for (int r = 0; r < t.HolderRecords.Count; r++)
                        {
                            HolderRecord hr = t.HolderRecords[r];
                            if (hr != null && hr.StableId == stableId) { wasHolder = true; break; }
                        }
                        if (isCurrentHolder)
                        {
                            anchorThing = t;
                            break; // 当前持有优先
                        }
                        if (wasHolder && anchorThing == null)
                        {
                            anchorThing = t; // 兜底：曾持有过的第一把武器
                        }
                    }
                    if (anchorThing != null)
                    {
                        IReadOnlyList<ChronicleEvent> thingEvents = service.GetEventsFor(anchorThing.StableId);
                        snap.Legacy = BuildLegacy(service, anchorThing, thingEvents);
                    }
                }
            }
        }

        /// <summary>
        /// v1.1.4 劳模住所/工坊检测：把 PawnObject.Workplace 持久化快照解析为展示视图。
        /// 展示优先级：工坊实例全局别名（BuildingAliases，按 BuildingStableId）>
        /// per-pawn CustomName（旧存档兼容）> ThingDef.LabelCap 实时解析名。
        /// 房间角色同规则：RoomRoleAliases（按 defName）> LabelCap。
        /// </summary>
        private static WorkplaceView BuildWorkplaceView(IArchiveService service, PawnObject pawn)
        {
            WorkplaceView view = new WorkplaceView { IsEmpty = true };
            if (pawn == null || pawn.Workplace == null || pawn.Workplace.IsEmpty)
            {
                return view;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(pawn.Workplace.BuildingDefName);
            view.BuildingDefName = pawn.Workplace.BuildingDefName;
            view.BuildingStableId = pawn.Workplace.BuildingStableId;
            view.CustomName = pawn.Workplace.CustomName;
            string defaultLabel = (def != null) ? def.LabelCap : pawn.Workplace.BuildingDefName;
            // 工坊实例全局别名优先（按 stableId）；否则 per-pawn 兼容槽；否则 LabelCap。
            string globalAlias = (service != null && !string.IsNullOrEmpty(pawn.Workplace.BuildingStableId))
                ? service.GetBuildingAlias(pawn.Workplace.BuildingStableId)
                : null;
            string resolvedLabel = defaultLabel;
            if (!string.IsNullOrWhiteSpace(globalAlias))
            {
                resolvedLabel = globalAlias;
            }
            else if (!string.IsNullOrWhiteSpace(view.CustomName))
            {
                resolvedLabel = view.CustomName;
            }
            view.BuildingLabel = resolvedLabel;
            view.UseCount = pawn.Workplace.UseCount;
            view.LastUsedTick = pawn.Workplace.LastUsedTick;
            view.MapIndex = pawn.Workplace.MapIndex;
            view.Cell = pawn.Workplace.Cell;
            view.IsEmpty = false;
            string roomRoleDefName = pawn.Workplace.RoomRoleDefName;
            if (!string.IsNullOrEmpty(roomRoleDefName))
            {
                RoomRoleDef roomRole = DefDatabase<RoomRoleDef>.GetNamedSilentFail(roomRoleDefName);
                string roleDefault = (roomRole != null) ? roomRole.LabelCap : roomRoleDefName;
                string roleAlias = (service != null) ? service.GetRoomRoleAlias(roomRoleDefName) : null;
                view.RoomRoleLabel = string.IsNullOrWhiteSpace(roleAlias) ? roleDefault : roleAlias;
            }
            return view;
        }

        /// <summary>
        /// v1.1.4 劳模住所/工坊检测：把 PawnObject.Residence 持久化快照解析为展示视图。
        /// 展示优先级：房间类型全局别名（RoomRoleAliases，按 defName）> LabelCap（如「卧室」）。
        /// </summary>
        private static ResidenceView BuildResidenceView(IArchiveService service, PawnObject pawn)
        {
            ResidenceView view = new ResidenceView { IsEmpty = true };
            if (pawn == null || pawn.Residence == null || pawn.Residence.IsEmpty)
            {
                return view;
            }
            view.RoomRoleDefName = pawn.Residence.RoomRoleDefName;
            RoomRoleDef roomRole = DefDatabase<RoomRoleDef>.GetNamedSilentFail(pawn.Residence.RoomRoleDefName);
            string defaultLabel = (roomRole != null) ? roomRole.LabelCap : pawn.Residence.RoomRoleDefName;
            // v1.1.4 展示优先级：房间级整间房昵称 > 房间级类型名替换 > 类型级全局 > RoomRoleDef.LabelCap。
            string roomName = null;
            string typeName = null;
            if (service != null)
            {
                roomName = service.GetRoomName(pawn.StableId, pawn.Residence.RoomRoleDefName);
                typeName = service.GetRoomTypeName(pawn.StableId, pawn.Residence.RoomRoleDefName);
            }
            if (string.IsNullOrWhiteSpace(typeName))
            {
                typeName = (service != null) ? service.GetRoomRoleAlias(pawn.Residence.RoomRoleDefName) : null;
            }
            if (string.IsNullOrWhiteSpace(typeName))
            {
                typeName = defaultLabel;
            }
            view.RoomTypeName = typeName;
            view.RoomRoleLabel = string.IsNullOrWhiteSpace(roomName) ? typeName : roomName;
            view.LastSeenTick = pawn.Residence.LastSeenTick;
            view.MapIndex = pawn.Residence.MapIndex;
            view.Cell = pawn.Residence.Cell;
            view.IsEmpty = false;
            return view;
        }

        /// <summary>
        /// v1.1.4 勋章体系：为殖民者生成勋章墙视图（阈值类，§6.1~6.4）。Read Model 只读派生：
        /// 消费 <see cref="MedalAwardEvaluator"/> 判定结果；Label/Desc/BuffText 在此解析翻译键，
        /// 窗口只消费。§6.9 等级规则——同 SeriesKey（称号）只显示最高已达档位，由 IsHighestTier 标记。
        /// 未授予但可判定的勋章也包含（灰态 + 进度条）。
        /// </summary>
        public static IReadOnlyList<MedalView> BuildMedals(PawnObject pawn)
        {
            List<MedalView> views = new List<MedalView>();
            if (pawn == null)
            {
                return views;
            }
            MedalEvaluationResult result = MedalAwardEvaluator.EvaluatePawn(pawn);
            if (result == null || result.Items == null)
            {
                return views;
            }
            Dictionary<string, MedalTier> highestBySeries = new Dictionary<string, MedalTier>();
            for (int i = 0; i < result.Items.Count; i++)
            {
                MedalEvaluation ev = result.Items[i];
                if (ev == null || ev.Def == null)
                {
                    continue;
                }
                MedalDef def = ev.Def;
                string seriesKey = MedalDef.SeriesKeyOf(def.defName);
                double threshold = (double)def.threshold;
                views.Add(new MedalView
                {
                    DefName = def.defName,
                    Label = MedalTranslationKeys.Label(def.defName).Translate().ToString(),
                    Desc = MedalTranslationKeys.Desc(def.defName).Translate().ToString(),
                    Tier = def.tier,
                    SeriesKey = seriesKey,
                    IsApplicable = ev.IsApplicable,
                    IsGranted = ev.IsGranted,
                    IsMet = ev.IsMet,
                    IsNewAward = ev.IsNewAward,
                    BuffText = ResolveBuffText(def),
                    CurrentValue = ev.CurrentValue,
                    Threshold = threshold,
                    Progress = threshold > 0.0 ? Mathf.Clamp01((float)(ev.CurrentValue / threshold)) : 0f
                });
                // 已达 = 已授予历史 或 当前达标（未授予时仍参与最高档归并）。
                if (ev.IsGranted || ev.IsMet)
                {
                    MedalTier prev;
                    if (!highestBySeries.TryGetValue(seriesKey, out prev) || def.tier > prev)
                    {
                        highestBySeries[seriesKey] = def.tier;
                    }
                }
            }
            for (int i = 0; i < views.Count; i++)
            {
                MedalView view = views[i];
                MedalTier highest;
                view.IsHighestTier = highestBySeries.TryGetValue(view.SeriesKey, out highest) && view.Tier == highest;
            }
            return views;
        }

        /// <summary>解析 MedalDef.buffDefName → 通道 B 展示增益文案（MedalBuffDef.displayBonus 翻译键）。</summary>
        private static string ResolveBuffText(MedalDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.buffDefName))
            {
                return null;
            }
            MedalBuffDef buff = DefDatabase<MedalBuffDef>.GetNamedSilentFail(def.buffDefName);
            if (buff == null || string.IsNullOrEmpty(buff.displayBonus))
            {
                return null;
            }
            return buff.displayBonus.Translate().ToString();
        }

        // ---- v4.4 Pawn Overview derivation (Read Model only) ----

        /// <summary>
        /// 职业档案 · 工作经历（简历式分段）。当前数据现实（P1）：
        /// 仅 <c>ItemProduced</c> 真实写入，且 <c>CareerEvent</c> 不携带工坊信息（DefName
        /// 是产品而非工坊）；工坊维度由 <c>PawnObject.Workplace</c>（当前工坊快照）承载。
        /// 因此工作经历以「当前工坊」为唯一一段，聚合该 Pawn 全部 ItemProduced 成果。
        /// 若未来事件携带工坊 stableId（多工坊历史），再改为按工坊分组循环即可。
        /// </summary>
        private static IReadOnlyList<WorkExperienceView> BuildWorkExperiences(PawnObject pawn)
        {
            List<WorkExperienceView> result = new List<WorkExperienceView>();
            if (pawn == null || pawn.CareerData == null
                || pawn.CareerData.Events == null || pawn.CareerData.Events.Count == 0)
            {
                return result; // 空 → UI 占位，不造假
            }
            if (pawn.Workplace == null || pawn.Workplace.IsEmpty)
            {
                return result; // 无工坊记录 → 不生成段（不编造工坊名）
            }

            // 聚合全部 ItemProduced 事件（当前所有事件均为此类）。
            long minTick = long.MaxValue;
            long maxTick = long.MinValue;
            int produced = 0;
            int fine = 0;
            foreach (CareerEvent ev in pawn.CareerData.Events)
            {
                if (ev == null) continue;
                if (ev.Tick < minTick) minTick = ev.Tick;
                if (ev.Tick > maxTick) maxTick = ev.Tick;
                if (string.Equals(ev.EventType, CareerEventType.ItemProduced, System.StringComparison.Ordinal))
                {
                    produced++;
                    if (IsFineQuality(ev.Quality)) fine++;
                }
            }
            int startYear = GenDate.Year((long)minTick, 0f);
            int endYear = GenDate.Year((long)maxTick, 0f);
            // 日期表达：同年显示单日/年；跨年显示 "YYYY/M/D – YYYY/M/D"（GenDate 格式化避免 tick 范围歧义）。
            string period;
            if (startYear == endYear)
            {
                period = GenDate.DateReadoutStringAt((long)minTick, Vector2.zero)
                    + " – " + GenDate.DateReadoutStringAt((long)maxTick, Vector2.zero);
            }
            else
            {
                period = GenDate.DateReadoutStringAt((long)minTick, Vector2.zero)
                    + " – " + GenDate.DateReadoutStringAt((long)maxTick, Vector2.zero);
            }

            IArchiveService service = PersonalChronicleMod.ArchiveService;
            string workLabel = ResolveWorkplaceLabel(service, pawn, null);
            string roomLabel = ResolveRoomLabel(service, pawn, null);

            List<string> achievements = new List<string>();
            if (produced > 0)
            {
                if (produced > 0)
                {
                    string producedText = "PersonalChronicle.UI.Career.Resume.Achieve.Produced".Translate(produced.ToString());
                    if (fine > 0)
                    {
                        producedText += "PersonalChronicle.UI.Career.Resume.Achieve.Fine".Translate(fine.ToString());
                    }
                    achievements.Add(producedText);
                }
            }
            // 其余 4 种事件类型（建造/研究/著书）为 P1 白名单占位，当前无真实写入；
            // 若未来接入，在此按 EventType 聚合追加 achievements（不造假）。

            result.Add(new WorkExperienceView
            {
                WorkplaceLabel = workLabel,
                RoomRoleLabel = roomLabel,
                PeriodText = period,
                Achievements = achievements,
                ProducedCount = produced,
                FineCount = fine,
                UseCount = pawn.Workplace.UseCount,
                IsEmpty = false
            });
            return result;
        }

        /// <summary>优秀及以上品质（Excellent/Masterwork/Legendary）计入"精细成果"。</summary>
        /// <summary>后代关系判定（稳定键白名单：PawnRelationDef.defName；含直系与隔代）。</summary>
        private static bool IsDescendantRelation(string relationDefName)
        {
            if (string.IsNullOrEmpty(relationDefName)) return false;
            switch (relationDefName)
            {
                case "Child":
                case "Grandchild":
                case "Stepchild":
                    return true;
                default:
                    return false;
            }
        }
        private static bool IsFineQuality(string quality)
        {
            if (string.IsNullOrEmpty(quality)) return false;
            return string.Equals(quality, "Excellent", System.StringComparison.Ordinal)
                || string.Equals(quality, "Masterwork", System.StringComparison.Ordinal)
                || string.Equals(quality, "Legendary", System.StringComparison.Ordinal);
        }

        /// <summary>解析该工坊段显示名（与 BuildWorkplaceView 同优先级：全局别名 > 自定义 > LabelCap）。</summary>
        private static string ResolveWorkplaceLabel(IArchiveService service, PawnObject pawn, List<CareerEvent> events)
        {
            // 优先取 pawn.Workplace（当前工坊，最可能是该段所属）。
            if (pawn.Workplace != null && !pawn.Workplace.IsEmpty)
            {
                string defName = pawn.Workplace.BuildingDefName;
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                string label = (def != null) ? def.LabelCap : defName;
                string alias = (!string.IsNullOrEmpty(pawn.Workplace.BuildingStableId) && service != null)
                    ? service.GetBuildingAlias(pawn.Workplace.BuildingStableId) : null;
                if (!string.IsNullOrWhiteSpace(alias)) return alias;
                if (!string.IsNullOrWhiteSpace(pawn.Workplace.CustomName)) return pawn.Workplace.CustomName;
                return label;
            }
            return "--";
        }

        /// <summary>解析该工坊段房间角色 label（RoomRoleAliases > LabelCap）。</summary>
        private static string ResolveRoomLabel(IArchiveService service, PawnObject pawn, List<CareerEvent> events)
        {
            if (pawn.Workplace != null && !string.IsNullOrEmpty(pawn.Workplace.RoomRoleDefName))
            {
                RoomRoleDef roomRole = DefDatabase<RoomRoleDef>.GetNamedSilentFail(pawn.Workplace.RoomRoleDefName);
                string label = (roomRole != null) ? roomRole.LabelCap : pawn.Workplace.RoomRoleDefName;
                string alias = service != null ? service.GetRoomRoleAlias(pawn.Workplace.RoomRoleDefName) : null;
                return string.IsNullOrWhiteSpace(alias) ? label : alias;
            }
            return null;
        }

        // ---- 职业档案 · 总览（职业身份 / 资格状态 / 预检 / 下一职称） ----

        /// <summary>
        /// 职业档案 · 总览视图（对齐前端职业档案Tab预览.html 总览 4 区块）。
        /// 全部从 CareerData 真实数据派生：无数据 → HasData=false（UI 空态，不造假）。
        /// </summary>
        private static CareerOverviewView BuildCareerOverview(PawnObject pawn)
        {
            CareerOverviewView view = new CareerOverviewView();
            if (pawn == null || pawn.CareerData == null)
            {
                return view;
            }
            CareerData cd = pawn.CareerData;

            // —— 职业身份 ——
            // 当前职称 = 已授最高档（按 TitleDef.order 归并）。
            string roleName = null;
            ProfessionalTitleDef currentTitle = FindHighestGrantedTitle(cd);
            if (currentTitle != null)
            {
                roleName = TitleLabel(currentTitle);
            }
            // 主技能 = Professional 中等级最高的技能。
            string skillText = null;
            int maxLevel = 0;
            string directionLabel = null;
            if (cd.Professional != null && cd.Professional.skills != null)
            {
                for (int i = 0; i < cd.Professional.skills.Count; i++)
                {
                    ProfessionalSkillData sd = cd.Professional.skills[i];
                    if (sd == null) continue;
                    ProfessionalSkillDef sdef = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(sd.skillDefName);
                    string sName = sdef != null ? sdef.LabelCap : sd.skillDefName;
                    if (sd.level > maxLevel)
                    {
                        maxLevel = sd.level;
                        skillText = sName + " Lv" + sd.level;
                    }
                    if (directionLabel == null && sdef != null && !string.IsNullOrEmpty(sdef.direction))
                    {
                        ProfessionalDirectionDef ddef = DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(sdef.direction);
                        if (ddef != null) directionLabel = ddef.LabelCap;
                    }
                }
            }
            // 相关工时：按事件首末 tick 跨度（60000tick ≈ 25h → 1h ≈ 2400 tick）。
            long spanTicks = CareerSpanTicks(cd);
            int hours = (int)(spanTicks / 2400L);
            // 事实计数指标：统一从 RecordCountByType 聚合（UI 只消费，禁止绘制路径直查 Domain）。
            int results = 0;
            int made = 0;
            int built = 0;
            int researched = 0;
            if (cd.RecordCountByType != null)
            {
                cd.RecordCountByType.TryGetValue(CareerEventType.ItemProduced, out results);
                cd.RecordCountByType.TryGetValue(CareerEventType.ItemProduced, out made);
                cd.RecordCountByType.TryGetValue(CareerEventType.ConstructionCompleted, out built);
                cd.RecordCountByType.TryGetValue(CareerEventType.ResearchCompleted, out researched);
            }
            int books = cd.Books != null ? cd.Books.Count : 0;

            view.HasData = roleName != null || skillText != null || spanTicks > 0 || results > 0 || books > 0;
            // v1.1.5：移除 "HasData=false 提前 return"——保留 HasData 作为信号，但字段全部填充默认值，
            // 让 UI 端在空数据时也能渲染完整区块（占位 `--` / `0`）；避免"暂无数据"白色背景与缺字段。
            view.RoleName = roleName;
            view.RoleDesc = directionLabel;
            view.SkillText = skillText;
            view.HoursText = hours > 0 ? hours + " h" : null;
            view.Results = results;
            view.Books = books;
            view.Made = made;
            view.Built = built;
            view.Researched = researched;

            // —— 下一职称 + 资格状态 + 预检 ——
            QualificationDef nextQual = FindNextQualification(cd);
            List<QualificationEvaluator.Eligibility> eligibilities = QualificationEvaluator.Evaluate(pawn);
            if (nextQual != null)
            {
                view.NextTitle = TitleLabel(DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(nextQual.titleDefName));
                QualificationEvaluator.Eligibility el = FindEligibility(eligibilities, nextQual);
                float composite = el != null ? el.CompositeScore : 0f;
                view.Qual = BuildQualRows(cd, nextQual, composite);
                view.PreCheck = BuildPreCheckRows(cd, nextQual, composite);
                view.Progress = ComputeReadyPercent(view.PreCheck);
                view.NextGaps = ComputeGaps(view.Qual, view.PreCheck);
            }
            return view;
        }

        /// <summary>
        /// 职业事实计数（UI-001 / ARC-002：事实聚合归属 Provider）。
        /// 统一从 <c>CareerData.RecordCountByType</c> 取全部 9 类事件计数，
        /// 窗口（Overview/Resume/Honor）只消费本快照，不再直查 Domain。
        /// </summary>
        private static CareerFactCounts BuildCareerFactCounts(PawnObject pawn)
        {
            CareerFactCounts c = new CareerFactCounts();
            if (pawn == null || pawn.CareerData == null || pawn.CareerData.RecordCountByType == null)
            {
                return c;
            }
            Dictionary<string, int> counts = pawn.CareerData.RecordCountByType;
            counts.TryGetValue(CareerEventType.WorkCompleted, out c.WorkCompleted);
            counts.TryGetValue(CareerEventType.ItemProduced, out c.ItemProduced);
            counts.TryGetValue(CareerEventType.ConstructionCompleted, out c.ConstructionCompleted);
            counts.TryGetValue(CareerEventType.ResearchCompleted, out c.ResearchCompleted);
            counts.TryGetValue(CareerEventType.BookProduced, out c.BookProduced);
            counts.TryGetValue(CareerEventType.ExamPassed, out c.ExamPassed);
            counts.TryGetValue(CareerEventType.ThesisDefended, out c.ThesisDefended);
            counts.TryGetValue(CareerEventType.TitleGranted, out c.TitleGranted);
            counts.TryGetValue(CareerEventType.MedalGranted, out c.MedalGranted);
            return c;
        }

        /// <summary>已授最高档职称 Def（按 TitleDef.order 取最大）。</summary>
        private static ProfessionalTitleDef FindHighestGrantedTitle(CareerData cd)
        {
            if (cd.GrantedTitles == null)
            {
                return null;
            }
            ProfessionalTitleDef best = null;
            for (int i = 0; i < cd.GrantedTitles.Count; i++)
            {
                GrantedTitle gt = cd.GrantedTitles[i];
                if (gt == null || string.IsNullOrEmpty(gt.TitleDefName)) continue;
                ProfessionalTitleDef td = DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(gt.TitleDefName);
                if (td == null) continue;
                if (best == null || td.order > best.order) best = td;
            }
            return best;
        }

        /// <summary>
        /// 下一档资格：委托 <see cref="QualificationEvaluator.NextQualification"/>（Domain 纯逻辑，
        /// 职称/资格双键匹配 + 严格高于当前已授最高档），以 DefDatabase 全量资格为候选集。
        /// </summary>
        private static QualificationDef FindNextQualification(CareerData cd)
        {
            if (cd == null) return null;
            return QualificationEvaluator.NextQualification(
                cd, DefDatabase<QualificationDef>.AllDefsListForReading);
        }

        private static string TitleLabel(ProfessionalTitleDef titleDef)
        {
            if (titleDef == null) return "--";
            return ("Professional.Title." + titleDef.defName + ".Label").Translate().ToString();
        }

        private static QualificationEvaluator.Eligibility FindEligibility(
            List<QualificationEvaluator.Eligibility> list, QualificationDef target)
        {
            if (list == null || target == null) return null;
            for (int i = 0; i < list.Count; i++)
            {
                QualificationEvaluator.Eligibility e = list[i];
                if (e != null && e.Def != null && string.Equals(e.Def.defName, target.defName, System.StringComparison.Ordinal))
                {
                    return e;
                }
            }
            return null;
        }

        /// <summary>资格状态 7 条件（对齐 P9 HTML：专业等级/职业资历/综合评分/实践考试/理论考试/论文答辩/评级评审）。
        /// 中间列 note 显示「资格档要求」（翻译档名，不硬编码 defName），Tooltip 显示「人物当前条件 + 结构透视」。
        /// 悬停由 DrawQualCell → TooltipHandler.TipRegion 绑定。
        /// v10 体检：中间列走 Qual.Req.* 翻译键（用户要求全简体中文无硬编码），Tooltip 走 Qual.Tooltip.* 结构化模板（多行渲染）。</summary>
        /// <summary>构建"当前资格状态"7 行快照（条件名/要求/状态/悬停 Tooltip），供总览与资格子页统一消费（v2.0 §14 一致性）。</summary>
        public static IReadOnlyList<CareerQualView> BuildQualRows(CareerData cd, QualificationDef nextQual, float composite)
        {
            List<CareerQualView> rows = new List<CareerQualView>();
            if (nextQual == null) return rows;
            ProfessionalSkillData skillData = null;
            if (cd.Professional != null && !string.IsNullOrEmpty(nextQual.professionalSkillDefName))
            {
                skillData = cd.Professional.GetSkill(nextQual.professionalSkillDefName);
            }
            int level = skillData != null ? skillData.level : 0;
            float xp = skillData != null ? skillData.xp : 0f;
            int practiceCount = skillData != null ? skillData.practiceCount : 0;
            long span = CareerSpanTicks(cd);
            bool practical = HasPassedExam(cd, nextQual.defName, true);
            bool theory = HasPassedExam(cd, nextQual.defName, false);
            bool thesisDefense = HasPassedThesisDefense(cd, nextQual.defName);

            string tierLabel = QualTitleLabel(nextQual);

            // ── ① 专业等级：note=档名 ≥ Lv（翻译）；tooltip=等级 XP 来源结构
            ProfessionalSkillDef skillDef = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(nextQual.professionalSkillDefName);
            string skillSourceList = SkillSourceList(skillDef);
            int xpToNext = XpToNext(xp, skillDef);
            rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Level",
                "PersonalChronicle.UI.Career.Qual.Req.Level".Translate(tierLabel, nextQual.requiredMinLevel).ToString(),
                level >= nextQual.requiredMinLevel,
                "PersonalChronicle.UI.Career.Qual.Tooltip.Level".Translate(tierLabel, nextQual.requiredMinLevel, level, xp.ToString("F0"), practiceCount, skillSourceList, xpToNext).ToString()));
            // ── ② 职业资历：note=档名 ≥ 时长（天/月/年）；tooltip=累计 + 来源分项
            SpanBreakdown breakdown1 = CareerSpanBreakdown(cd);
            rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Time",
                "PersonalChronicle.UI.Career.Qual.Req.Time".Translate(tierLabel, FormatSpanHuman(nextQual.requiredCareerTimeTicks)).ToString(),
                span >= nextQual.requiredCareerTimeTicks,
                "PersonalChronicle.UI.Career.Qual.Tooltip.Time".Translate(tierLabel, FormatSpanHuman(nextQual.requiredCareerTimeTicks), FormatSpanHuman(span), span, breakdown1.CraftingCount, breakdown1.CraftingHours, breakdown1.ConstructionCount, breakdown1.ConstructionHours, breakdown1.ResearchCount, breakdown1.ResearchHours, breakdown1.OtherCount, breakdown1.OtherHours).ToString()));
            // ── ③ 综合评分：note=档名 ≥ minScore；tooltip=5 项分项结构
            ScoreBreakdown sb = ScoreBreakdownFor(cd, nextQual, level, composite);
            rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Score",
                "PersonalChronicle.UI.Career.Qual.Req.Score".Translate(tierLabel, nextQual.minimumScore.ToString("F0")).ToString(),
                composite >= nextQual.minimumScore,
                "PersonalChronicle.UI.Career.Qual.Tooltip.Score".Translate(tierLabel, nextQual.minimumScore.ToString("F0"), composite.ToString("F1"),
                    sb.Level.ToString("F1"), (sb.Level * 0.20f).ToString("F1"),
                    sb.Practical.ToString("F1"), (sb.Practical * 0.25f).ToString("F1"),
                    sb.Theory.ToString("F1"), (sb.Theory * 0.20f).ToString("F1"),
                    sb.Thesis.ToString("F1"), (sb.Thesis * 0.20f).ToString("F1"),
                    sb.Defense.ToString("F1"), (sb.Defense * 0.15f).ToString("F1")).ToString()));
            // ── ④ 实践考试：tooltip=任务详情（件数/品质/上限/时限）+ 当前进度
            PracticalDetail pDetail = PracticalDetailFor(cd, nextQual);
            string practicalState = PracticalStateText(cd, nextQual);
            rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Practical",
                "PersonalChronicle.UI.Career.Qual.Practical.Req".Translate(), practical,
                "PersonalChronicle.UI.Career.Qual.Tooltip.Practical".Translate(tierLabel,
                    pDetail.ReqCount, pDetail.MinQuality, pDetail.MaxProduced, FormatSpanHuman(pDetail.TimeLimitTicks),
                    practicalState, pDetail.ProducedCount, pDetail.QualifiedCount, pDetail.Score.ToString("F0")).ToString()));
            // ── ⑤ 理论考试：tooltip=加权合成 4 项
            TheoryDetail tDetail = TheoryDetailFor(cd, nextQual);
            string theoryState = TheoryStateText(cd, nextQual);
            rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Theory",
                "PersonalChronicle.UI.Career.Qual.Theory.Req".Translate(), theory,
                "PersonalChronicle.UI.Career.Qual.Tooltip.Theory".Translate(tierLabel, theoryState,
                    tDetail.Score.ToString("F0"), tDetail.BookScore.ToString("F0"), tDetail.ResearchScore.ToString("F0"),
                    tDetail.SkillScore.ToString("F0"), tDetail.ActivityScore.ToString("F0")).ToString()));
            // ── ⑥ 论文/答辩：tooltip=论文+委员评分构成
            DefenseDetail dDetail = DefenseDetailFor(cd, nextQual);
            string defenseState = DefenseStateText(cd, nextQual);
            rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Defense",
                "PersonalChronicle.UI.Career.Qual.Defense.Req".Translate(), thesisDefense,
                "PersonalChronicle.UI.Career.Qual.Tooltip.Defense".Translate(tierLabel, defenseState,
                    dDetail.ThesisScore.ToString("F0"), dDetail.DefenseScore.ToString("F0"),
                    dDetail.CommitteeSize, dDetail.FinalScore.ToString("F0")).ToString()));
            // ── ⑦ 评级评审：tooltip=评审期长度+流程
            int reviewDays = nextQual.reviewDays > 0 ? nextQual.reviewDays : 3;
            string reviewState;
            bool reviewMet;
            ReviewStateText(cd, nextQual, out reviewState, out reviewMet);
            rows.Add(QualRow("PersonalChronicle.UI.Career.Qual.Review",
                "PersonalChronicle.UI.Career.Qual.Review.Req".Translate(), reviewMet,
                "PersonalChronicle.UI.Career.Qual.Tooltip.Review".Translate(tierLabel, reviewState, reviewDays).ToString()));
            return rows;
        }

        // ============== 详情结构（Tooltip 用） ==============

        /// <summary>原版技能 defName 列表翻译为玩家可读字符串（Crafting → 工艺）。</summary>
        private static string SkillSourceList(ProfessionalSkillDef def)
        {
            if (def == null || def.sourceSkills == null || def.sourceSkills.Count == 0)
                return "PersonalChronicle.UI.Career.Qual.SkillSource.None".Translate().ToString();
            // 走原版 SkillDef 的 LabelCap（RimWorld 自动本地化翻译）
            List<string> labels = new List<string>();
            for (int i = 0; i < def.sourceSkills.Count; i++)
            {
                string s = def.sourceSkills[i];
                if (string.IsNullOrEmpty(s)) continue;
                SkillDef sd = DefDatabase<SkillDef>.GetNamedSilentFail(s);
                if (sd != null) labels.Add(sd.LabelCap.ToString());
                else labels.Add(s);
            }
            return labels.Count > 0 ? string.Join("、", labels) : "PersonalChronicle.UI.Career.Qual.SkillSource.None".Translate().ToString();
        }

        /// <summary>距离下一档还差多少 XP（按当前 xp 与 maxLevel 兜底推算）。无曲线参数时回退占位。</summary>
        private static int XpToNext(float currentXp, ProfessionalSkillDef def)
        {
            if (def == null || def.xpCap <= 0f || def.maxLevel <= 0) return 0;
            float next = currentXp + 1f;
            if (next >= def.xpCap) return 0;
            // 简单线性近似（无 xpCurve 公开数据，仅做提示）
            float ratio = next / def.xpCap;
            float stepXp = def.xpCap / def.maxLevel;
            return Mathf.CeilToInt((1f - ratio) * stepXp);
        }

        /// <summary>职业时长按 CareerEvent 类型分项（用于 Tooltip）。</summary>
        private static SpanBreakdown CareerSpanBreakdown(CareerData cd)
        {
            SpanBreakdown b = new SpanBreakdown();
            if (cd == null || cd.Events == null) return b;
            for (int i = 0; i < cd.Events.Count; i++)
            {
                CareerEvent e = cd.Events[i];
                if (e == null) continue;
                // 仅 Event 数（不算 tick 累计，缺时长字段；下一步可读 cd.HoursByType 派生）
                switch (e.EventType)
                {
                    case "ItemProduced": b.CraftingCount++; b.CraftingHours += 1; break;
                    case "ConstructionCompleted": b.ConstructionCount++; b.ConstructionHours += 1; break;
                    case "ResearchCompleted": b.ResearchCount++; b.ResearchHours += 1; break;
                    case "BookProduced": b.OtherCount++; b.OtherHours += 1; break;
                    default: b.OtherCount++; b.OtherHours += 1; break;
                }
            }
            // 用 hours 替换 tick（防止溢出）：上述每事件记 1h
            return b;
        }

        private sealed class SpanBreakdown
        {
            public int CraftingCount, ConstructionCount, ResearchCount, OtherCount;
            public int CraftingHours, ConstructionHours, ResearchHours, OtherHours;
        }

        /// <summary>综合评分 5 项分项（按 Eligibility.CompositeScore 简单展开）。</summary>
        private static ScoreBreakdown ScoreBreakdownFor(CareerData cd, QualificationDef def, int level, float composite)
        {
            ScoreBreakdown sb = new ScoreBreakdown();
            // 等级分项（按 maxLevel 归一化 0~100）
            ProfessionalSkillDef sd = DefDatabase<ProfessionalSkillDef>.GetNamedSilentFail(def.professionalSkillDefName);
            int max = sd != null && sd.maxLevel > 0 ? sd.maxLevel : 50;
            sb.Level = Mathf.Clamp(level * 100f / max, 0f, 100f);
            // 考试分项（已通过=100，否则取最近一次 Exam 分数，否则0）
            sb.Practical = ExamLatestScore(cd, def.defName, true);
            sb.Theory = ExamLatestScore(cd, def.defName, false);
            sb.Thesis = ThesisLatestScore(cd, def.defName);
            sb.Defense = DefenseLatestScore(cd, def.defName);
            return sb;
        }

        private static float ExamLatestScore(CareerData cd, string defName, bool practical)
        {
            if (cd == null || cd.Exams == null) return 0f;
            if (practical)
            {
                for (int i = cd.Exams.Practical.Count - 1; i >= 0; i--)
                {
                    if (cd.Exams.Practical[i] != null && cd.Exams.Practical[i].QualificationDefName == defName)
                        return cd.Exams.Practical[i].Score;
                }
            }
            else
            {
                for (int i = cd.Exams.Theory.Count - 1; i >= 0; i--)
                {
                    if (cd.Exams.Theory[i] != null && cd.Exams.Theory[i].QualificationDefName == defName)
                        return cd.Exams.Theory[i].Score;
                }
            }
            return 0f;
        }

        private static float ThesisLatestScore(CareerData cd, string defName)
        {
            if (cd == null || cd.Thesis == null || cd.Thesis.Theses == null) return 0f;
            for (int i = cd.Thesis.Theses.Count - 1; i >= 0; i--)
            {
                if (cd.Thesis.Theses[i] != null && cd.Thesis.Theses[i].QualificationDefName == defName)
                    return cd.Thesis.Theses[i].ComputedScore;
            }
            return 0f;
        }

        private static float DefenseLatestScore(CareerData cd, string defName)
        {
            if (cd == null || cd.Thesis == null || cd.Thesis.Defenses == null) return 0f;
            for (int i = cd.Thesis.Defenses.Count - 1; i >= 0; i--)
            {
                if (cd.Thesis.Defenses[i] != null && cd.Thesis.Defenses[i].QualificationDefName == defName)
                    return cd.Thesis.Defenses[i].FinalScore;
            }
            return 0f;
        }

        private sealed class ScoreBreakdown
        {
            public float Level, Practical, Theory, Thesis, Defense;
        }

        /// <summary>实践考试任务详情（用于 Tooltip）。</summary>
        private static PracticalDetail PracticalDetailFor(CareerData cd, QualificationDef def)
        {
            PracticalDetail d = new PracticalDetail();
            if (!def.requiredExam) return d;
            // 取最近一条考试记录
            PracticalExamRecord latest = null;
            if (cd != null && cd.Exams != null && cd.Exams.Practical != null)
            {
                for (int i = cd.Exams.Practical.Count - 1; i >= 0; i--)
                {
                    if (cd.Exams.Practical[i] != null && cd.Exams.Practical[i].QualificationDefName == def.defName)
                    {
                        latest = cd.Exams.Practical[i];
                        break;
                    }
                }
            }
            // 兜底数据：来自 latest；否则用 Def 字段（如有）或写 -- 
            d.ReqCount = latest != null ? latest.RequiredCount : 0;
            d.MinQuality = latest != null ? latest.MinQuality : "Excellent";
            d.MaxProduced = latest != null && latest.MaxProduced > 0 ? latest.MaxProduced : (latest != null ? latest.RequiredCount * 2 : 0);
            d.TimeLimitTicks = latest != null ? latest.TimeLimitTicks : 0L;
            d.ProducedCount = latest != null ? latest.ProducedCount : 0;
            d.QualifiedCount = 0;
            if (latest != null && latest.ProducedQualities != null && latest.MinQuality != null)
            {
                for (int i = 0; i < latest.ProducedQualities.Count; i++)
                {
                    if (QualityMeets(latest.ProducedQualities[i], latest.MinQuality)) d.QualifiedCount++;
                }
            }
            d.Score = latest != null ? latest.Score : 0f;
            return d;
        }

        private static bool QualityMeets(string actual, string min)
        {
            if (string.IsNullOrEmpty(actual) || string.IsNullOrEmpty(min)) return false;
            // QualityCategory 顺序：Awful < Poor < Normal < Good < Excellent < Masterwork < Legendary
            string[] order = { "Awful", "Poor", "Normal", "Good", "Excellent", "Masterwork", "Legendary" };
            int ai = System.Array.IndexOf(order, actual);
            int mi = System.Array.IndexOf(order, min);
            if (ai < 0 || mi < 0) return false;
            return ai >= mi;
        }

        private sealed class PracticalDetail
        {
            public int ReqCount, MaxProduced, ProducedCount, QualifiedCount;
            public string MinQuality;
            public long TimeLimitTicks;
            public float Score;
        }

        private static TheoryDetail TheoryDetailFor(CareerData cd, QualificationDef def)
        {
            TheoryDetail d = new TheoryDetail();
            if (!def.requiredExam) return d;
            TheoryExamRecord r = null;
            if (cd != null && cd.Exams != null && cd.Exams.Theory != null)
            {
                for (int i = cd.Exams.Theory.Count - 1; i >= 0; i--)
                {
                    if (cd.Exams.Theory[i] != null && cd.Exams.Theory[i].QualificationDefName == def.defName)
                    {
                        r = cd.Exams.Theory[i];
                        break;
                    }
                }
            }
            if (r != null)
            {
                d.BookScore = r.BookScore;
                d.ResearchScore = r.ResearchScore;
                d.SkillScore = r.SkillScore;
                d.ActivityScore = r.ActivityScore;
                d.Score = r.Score;
            }
            return d;
        }

        private sealed class TheoryDetail
        {
            public float BookScore, ResearchScore, SkillScore, ActivityScore, Score;
        }

        private static DefenseDetail DefenseDetailFor(CareerData cd, QualificationDef def)
        {
            DefenseDetail d = new DefenseDetail();
            if (cd == null || cd.Thesis == null) return d;
            ThesisEvidence thesis = null;
            DefenseRecord defense = null;
            if (cd.Thesis.Theses != null)
            {
                for (int i = cd.Thesis.Theses.Count - 1; i >= 0; i--)
                {
                    if (cd.Thesis.Theses[i] != null && cd.Thesis.Theses[i].QualificationDefName == def.defName)
                    {
                        thesis = cd.Thesis.Theses[i];
                        break;
                    }
                }
            }
            if (cd.Thesis.Defenses != null)
            {
                for (int i = cd.Thesis.Defenses.Count - 1; i >= 0; i--)
                {
                    if (cd.Thesis.Defenses[i] != null && cd.Thesis.Defenses[i].QualificationDefName == def.defName)
                    {
                        defense = cd.Thesis.Defenses[i];
                        break;
                    }
                }
            }
            if (thesis != null) d.ThesisScore = thesis.ComputedScore;
            if (defense != null)
            {
                d.DefenseScore = defense.CommitteeScore;
                d.CommitteeSize = defense.CommitteePawnIds != null ? defense.CommitteePawnIds.Count : 0;
                d.FinalScore = defense.FinalScore;
            }
            return d;
        }

        private sealed class DefenseDetail
        {
            public float ThesisScore, DefenseScore, FinalScore;
            public int CommitteeSize;
        }

        /// <summary>实践考试实时状态（对齐 P9 HTML：未报名/进行中/通过/未通过）。</summary>
        private static string PracticalStateText(CareerData cd, QualificationDef def)
        {
            if (!def.requiredExam) return "PersonalChronicle.UI.Career.Qual.State.NotRequired".Translate().ToString();
            if (cd == null || cd.Exams == null || cd.Exams.Practical == null || cd.Exams.Practical.Count == 0)
                return "PersonalChronicle.UI.Career.Qual.State.NotApplied".Translate().ToString();
            // 取本档最新一条
            PracticalExamRecord latest = null;
            for (int i = cd.Exams.Practical.Count - 1; i >= 0; i--)
            {
                if (cd.Exams.Practical[i] != null && cd.Exams.Practical[i].QualificationDefName == def.defName)
                {
                    latest = cd.Exams.Practical[i];
                    break;
                }
            }
            if (latest == null) return "PersonalChronicle.UI.Career.Qual.State.NotApplied".Translate().ToString();
            if (latest.Passed) return "PersonalChronicle.UI.Career.Qual.State.Passed".Translate(latest.Score.ToString("F0")).ToString();
            if (latest.Finished) return "PersonalChronicle.UI.Career.Qual.State.Failed".Translate(latest.Score.ToString("F0")).ToString();
            return "PersonalChronicle.UI.Career.Qual.State.InProgress".Translate().ToString();
        }

        /// <summary>理论考试实时状态（待提交/通过）。</summary>
        private static string TheoryStateText(CareerData cd, QualificationDef def)
        {
            if (!def.requiredExam) return "PersonalChronicle.UI.Career.Qual.State.NotRequired".Translate().ToString();
            if (cd == null || cd.Exams == null || cd.Exams.Theory == null || cd.Exams.Theory.Count == 0)
                return "PersonalChronicle.UI.Career.Qual.State.Pending".Translate().ToString();
            for (int i = cd.Exams.Theory.Count - 1; i >= 0; i--)
            {
                TheoryExamRecord r = cd.Exams.Theory[i];
                if (r != null && r.QualificationDefName == def.defName)
                {
                    return r.Passed
                        ? "PersonalChronicle.UI.Career.Qual.State.Passed".Translate(r.Score.ToString("F0")).ToString()
                        : "PersonalChronicle.UI.Career.Qual.State.Pending".Translate().ToString();
                }
            }
            return "PersonalChronicle.UI.Career.Qual.State.Pending".Translate().ToString();
        }

        /// <summary>论文/答辩实时状态（待进行/答辩待进行/通过）。</summary>
        private static string DefenseStateText(CareerData cd, QualificationDef def)
        {
            if (!def.requiredThesis && !def.requiredDefense) return "PersonalChronicle.UI.Career.Qual.State.NotRequired".Translate().ToString();
            if (cd == null || cd.Thesis == null) return "PersonalChronicle.UI.Career.Qual.State.Pending".Translate().ToString();
            ThesisEvidence thesis = null;
            DefenseRecord defense = null;
            if (cd.Thesis.Theses != null)
            {
                for (int i = cd.Thesis.Theses.Count - 1; i >= 0; i--)
                {
                    if (cd.Thesis.Theses[i] != null && cd.Thesis.Theses[i].QualificationDefName == def.defName)
                    {
                        thesis = cd.Thesis.Theses[i];
                        break;
                    }
                }
            }
            if (cd.Thesis.Defenses != null)
            {
                for (int i = cd.Thesis.Defenses.Count - 1; i >= 0; i--)
                {
                    if (cd.Thesis.Defenses[i] != null && cd.Thesis.Defenses[i].QualificationDefName == def.defName)
                    {
                        defense = cd.Thesis.Defenses[i];
                        break;
                    }
                }
            }
            if (defense != null && defense.Passed && thesis != null && thesis.Completed)
                return "PersonalChronicle.UI.Career.Qual.State.ThesisDefense".Translate(thesis.ComputedScore.ToString("F0"), defense.FinalScore.ToString("F0")).ToString();
            if (thesis != null && thesis.Completed)
                return "PersonalChronicle.UI.Career.Qual.State.DefensePending".Translate().ToString();
            return "PersonalChronicle.UI.Career.Qual.State.Pending".Translate().ToString();
        }

        /// <summary>评级评审实时状态：待结算/评审中（已 X/Y 个工作日）/已答复（授予）。</summary>
        private static void ReviewStateText(CareerData cd, QualificationDef def, out string state, out bool met)
        {
            met = false;
            state = "PersonalChronicle.UI.Career.Qual.State.Pending".Translate().ToString();
            if (cd == null || cd.Qualification == null || def == null) return;
            QualificationProgress prog = cd.Qualification.Get(def.defName);
            if (prog == null) return;
            long startTick = prog.ReviewStartedTick;
            int reviewDays = prog.ReviewDays > 0 ? prog.ReviewDays : def.reviewDays;
            if (reviewDays <= 0) reviewDays = 3;
            if (startTick <= 0L)
            {
                state = "PersonalChronicle.UI.Career.Qual.State.ReviewPending".Translate().ToString();
                return;
            }
            long nowTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            long elapsedDays = (nowTick - startTick) / 60000L;
            if (elapsedDays < 0) elapsedDays = 0;
            if (elapsedDays >= reviewDays)
            {
                state = "PersonalChronicle.UI.Career.Qual.State.ReviewGranted".Translate().ToString();
                met = true;
            }
            else
            {
                state = "PersonalChronicle.UI.Career.Qual.State.ReviewInProgress".Translate(elapsedDays, reviewDays).ToString();
            }
        }

        private static CareerQualView QualRow(string labelKey, string note, bool met, string tooltip = null)
        {
            return new CareerQualView
            {
                Label = labelKey.Translate().ToString(),
                Note = note,
                StateKey = met ? "ok" : "wait",
                StateText = met ? "PersonalChronicle.UI.Career.Qual.Met".Translate().ToString()
                                   : "PersonalChronicle.UI.Career.Qual.Unmet".Translate().ToString(),
                Tooltip = tooltip
            };
        }

        /// <summary>资格档显示名（用于 Tooltip 引用）。走 titleDefName → ProfessionalTitleDef.defName → 翻译键。</summary>
        private static string QualTitleLabel(QualificationDef def)
        {
            if (def == null) return "--";
            if (!string.IsNullOrEmpty(def.titleDefName))
            {
                return ("Professional.Title." + def.titleDefName + ".Label").Translate().ToString();
            }
            return def.LabelCap.ToString();
        }

        /// <summary>tick → 天/月/年自适应显示：≥ 1 年 用「X 年 Y 月 Z 日」；≥ 1 月 用「X 月 Y 日」；否则「X 日」。
        /// 单位约定：1 月 = 30 天，1 年 = 12 月 = 360 天（与原版季节 15 天/季解耦，便于跨 Tile 直观）。</summary>
        private static string FormatSpanHuman(long ticks)
        {
            const long TicksPerDay = 60000L;
            long days = ticks / TicksPerDay;
            if (days >= 360L)
            {
                long years = days / 360L;
                long months = (days % 360L) / 30L;
                long remDays = days % 30L;
                if (months > 0 && remDays > 0) return years + " 年 " + months + " 月 " + remDays + " 日";
                if (months > 0) return years + " 年 " + months + " 月";
                return years + " 年 " + remDays + " 日";
            }
            if (days >= 30L)
            {
                long months = days / 30L;
                long remDays = days % 30L;
                if (remDays > 0) return months + " 月 " + remDays + " 日";
                return months + " 月";
            }
            return days + " 日";
        }

        /// <summary>资格预检 6 条件（对齐前端：核心技能/职业履历/成果记录/实践考试/理论考试/论文答辩）。</summary>
        private static IReadOnlyList<CareerPreCheckView> BuildPreCheckRows(CareerData cd, QualificationDef nextQual, float composite)
        {
            List<CareerPreCheckView> rows = new List<CareerPreCheckView>();
            if (nextQual == null) return rows;
            int level = 0;
            if (cd.Professional != null && !string.IsNullOrEmpty(nextQual.professionalSkillDefName))
            {
                ProfessionalSkillData sd = cd.Professional.GetSkill(nextQual.professionalSkillDefName);
                if (sd != null) level = sd.level;
            }
            long span = CareerSpanTicks(cd);
            bool practical = HasPassedExam(cd, nextQual.defName, true);
            bool theory = HasPassedExam(cd, nextQual.defName, false);
            bool thesisDefense = HasPassedThesisDefense(cd, nextQual.defName);
            bool hasAchievements = cd.Events != null && cd.Events.Count > 0;

            rows.Add(PreCheckRow("PersonalChronicle.UI.Career.Pre.Skill",
                level >= nextQual.requiredMinLevel, true));
            rows.Add(PreCheckRow("PersonalChronicle.UI.Career.Pre.Resume",
                span >= nextQual.requiredCareerTimeTicks, true));
            rows.Add(PreCheckRow("PersonalChronicle.UI.Career.Pre.Achieve",
                hasAchievements, true));
            rows.Add(PreCheckRow("PersonalChronicle.UI.Career.Pre.Practical",
                practical, false));
            rows.Add(PreCheckRow("PersonalChronicle.UI.Career.Pre.Theory",
                theory, false));
            rows.Add(PreCheckRow("PersonalChronicle.UI.Career.Pre.Defense",
                thesisDefense, false));
            return rows;
        }

        private static CareerPreCheckView PreCheckRow(string labelKey, bool done, bool hasProgress)
        {
            string stateKey;
            string stateText;
            if (done)
            {
                stateKey = "done";
                stateText = "PersonalChronicle.UI.Career.Pre.Ready".Translate().ToString();
            }
            else if (hasProgress)
            {
                stateKey = "pending";
                stateText = "PersonalChronicle.UI.Career.Pre.Preparing".Translate().ToString();
            }
            else
            {
                stateKey = "not-started";
                stateText = "PersonalChronicle.UI.Career.Pre.NotStarted".Translate().ToString();
            }
            return new CareerPreCheckView
            {
                Label = labelKey.Translate().ToString(),
                StateKey = stateKey,
                StateText = stateText
            };
        }

        /// <summary>晋升准备度 = done 条件占比。</summary>
        private static int ComputeReadyPercent(IReadOnlyList<CareerPreCheckView> preCheck)
        {
            if (preCheck == null || preCheck.Count == 0) return 0;
            int done = 0;
            for (int i = 0; i < preCheck.Count; i++)
            {
                if (preCheck[i] != null && string.Equals(preCheck[i].StateKey, "done", System.StringComparison.Ordinal)) done++;
            }
            return (int)((float)done / preCheck.Count * 100f);
        }

        /// <summary>缺口 = 资格状态中未满足条件的 label 列表（封顶/无缺口 → 空）。</summary>
        private static IReadOnlyList<string> ComputeGaps(IReadOnlyList<CareerQualView> qual, IReadOnlyList<CareerPreCheckView> preCheck)
        {
            List<string> gaps = new List<string>();
            if (qual != null)
            {
                for (int i = 0; i < qual.Count; i++)
                {
                    CareerQualView q = qual[i];
                    if (q != null && string.Equals(q.StateKey, "wait", System.StringComparison.Ordinal) && !string.IsNullOrEmpty(q.Label))
                    {
                        gaps.Add(q.Label);
                    }
                }
            }
            if (gaps.Count == 0 && preCheck != null)
            {
                for (int i = 0; i < preCheck.Count; i++)
                {
                    CareerPreCheckView p = preCheck[i];
                    if (p != null && !string.Equals(p.StateKey, "done", System.StringComparison.Ordinal) && !string.IsNullOrEmpty(p.Label))
                    {
                        gaps.Add(p.Label);
                    }
                }
            }
            return gaps;
        }

        private static bool HasPassedExam(CareerData cd, string qualificationDefName, bool practical)
        {
            if (cd == null || cd.Exams == null || string.IsNullOrEmpty(qualificationDefName)) return false;
            if (practical && cd.Exams.Practical != null)
            {
                for (int i = 0; i < cd.Exams.Practical.Count; i++)
                {
                    PracticalExamRecord r = cd.Exams.Practical[i];
                    if (r != null && string.Equals(r.QualificationDefName, qualificationDefName, System.StringComparison.Ordinal) && r.Passed)
                    {
                        return true;
                    }
                }
            }
            if (!practical && cd.Exams.Theory != null)
            {
                for (int i = 0; i < cd.Exams.Theory.Count; i++)
                {
                    TheoryExamRecord r = cd.Exams.Theory[i];
                    if (r != null && string.Equals(r.QualificationDefName, qualificationDefName, System.StringComparison.Ordinal) && r.Passed)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool HasPassedThesisDefense(CareerData cd, string qualificationDefName)
        {
            if (cd == null || string.IsNullOrEmpty(qualificationDefName)) return false;
            if (cd.Thesis != null && cd.Thesis.Theses != null)
            {
                for (int i = 0; i < cd.Thesis.Theses.Count; i++)
                {
                    ThesisEvidence t = cd.Thesis.Theses[i];
                    if (t != null && string.Equals(t.QualificationDefName, qualificationDefName, System.StringComparison.Ordinal) && t.Completed)
                    {
                        return true;
                    }
                }
            }
            if (cd.Thesis != null && cd.Thesis.Defenses != null)
            {
                for (int i = 0; i < cd.Thesis.Defenses.Count; i++)
                {
                    DefenseRecord d = cd.Thesis.Defenses[i];
                    if (d != null && string.Equals(d.ThesisId, qualificationDefName, System.StringComparison.Ordinal) && d.Passed)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static long CareerSpanTicks(CareerData cd)
        {
            if (cd == null || cd.Events == null || cd.Events.Count == 0) return 0L;
            long first = long.MaxValue;
            long last = 0L;
            for (int i = 0; i < cd.Events.Count; i++)
            {
                CareerEvent ev = cd.Events[i];
                if (ev == null) continue;
                if (ev.Tick < first) first = ev.Tick;
                if (ev.Tick > last) last = ev.Tick;
            }
            return first == long.MaxValue ? 0L : (last - first);
        }

        /// <summary>
        /// 职业档案 · 职称链（5 档，对齐 Defs/QualificationDefs.xml）。
        /// 状态：已获(granted) / 当前(current) / 下一阶(next) / 未开始(locked)。
        /// </summary>
        private static IReadOnlyList<CareerTitleTierView> BuildTitleTiers(PawnObject pawn)
        {
            List<CareerTitleTierView> tiers = new List<CareerTitleTierView>();
            if (pawn == null || pawn.CareerData == null)
            {
                return tiers;
            }
            CareerData cd = pawn.CareerData;
            List<QualificationDef> quals = DefDatabase<QualificationDef>.AllDefsListForReading
                .Where(q => q != null && !string.IsNullOrEmpty(q.titleDefName))
                .OrderBy(q => q.order)
                .ToList();
            if (quals.Count == 0)
            {
                return tiers;
            }
            // 当前档 index = 已授最高档；下一档 = 第一个未授予。
            int currentIdx = -1;
            for (int i = 0; i < quals.Count; i++)
            {
                if (QualificationEvaluator.HasGrantedTitleKey(cd, quals[i].titleDefName))
                {
                    currentIdx = i;
                }
            }
            int nextIdx = currentIdx + 1;
            for (int i = 0; i < quals.Count; i++)
            {
                QualificationDef q = quals[i];
                string defName = q.titleDefName;
                string label = TitleLabel(DefDatabase<ProfessionalTitleDef>.GetNamedSilentFail(defName));
                string stateKey;
                string stateText;
                if (i < currentIdx)
                {
                    stateKey = "granted";
                    stateText = "PersonalChronicle.UI.Career.Title.State.Granted".Translate().ToString();
                }
                else if (i == currentIdx)
                {
                    stateKey = "current";
                    stateText = "PersonalChronicle.UI.Career.Title.State.Current".Translate().ToString();
                }
                else if (i == nextIdx)
                {
                    stateKey = "next";
                    stateText = "PersonalChronicle.UI.Career.Title.State.Next".Translate().ToString();
                }
                else
                {
                    stateKey = "locked";
                    stateText = "PersonalChronicle.UI.Career.Title.State.Locked".Translate().ToString();
                }
                tiers.Add(new CareerTitleTierView
                {
                    DefName = defName,
                    Label = label,
                    StateKey = stateKey,
                    StateText = stateText,
                    Note = "PersonalChronicle.UI.Career.Title.Requires".Translate(q.requiredMinLevel).ToString()
                });
            }
            return tiers;
        }

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

    }
}

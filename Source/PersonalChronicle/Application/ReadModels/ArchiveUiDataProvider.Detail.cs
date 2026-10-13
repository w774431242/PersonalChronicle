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
    /// Partial of <see cref="ArchiveUiDataProvider"/> —see main file for the class doc.
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

        /// <summary>
        /// 资历的**来源分项**（按 <c>CareerEvent</c> 类型计**完成次数**）。
        ///
        /// <para>⚠ 2026-09-19（CHG-20260919-ECON-UI-009）：本类曾有 4 个 <c>*Hours</c> 字段，
        /// 由 <c>CareerSpanBreakdown</c> 对**每一条事件** <c>+= 1</c> 得到，并在悬停里显示成
        /// 「制造 5 次（5 h）」—— 两个数**永远相等**，是典型的"把件数当小时"假读数。</para>
        ///
        /// <para><b>澄清（审计曾误判为"事件数冒充时长"，实情更细）</b>：上游
        /// <c>ArchiveService.Career.cs</c> 的 <c>RecordWorkCompletedQuantum</c> **确实**按
        /// 「每游戏 1 小时记 1 条 <c>WorkCompleted</c>」量子化 —— 在**那一条**口径下"每事件 = 1h"
        /// 是成立的。但 <c>ItemProduced</c> / <c>ConstructionCompleted</c> / <c>ResearchCompleted</c>
        /// / <c>BookProduced</c> 是**完成瞬间的事实**，与时长无关；把五类混在一起再标成"小时"才是错的。
        /// 故此处**只保留次数**，不再产出任何小时。</para>
        ///
        /// <para><b>分项真实时长</b>需要 <c>WorkTimeAccumulator</c> 的按工种 tick 累计
        /// （该面另有持久化数据），属后续接入口，不在本次范围。</para>
        /// </summary>
        private sealed class SpanBreakdown
        {
            public int CraftingCount, ConstructionCount, ResearchCount, OtherCount;
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

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using PersonalChronicle.Api;
using PersonalChronicle.Application;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Career;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Profession;
using PersonalChronicle.Domain.Qualification;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// v1.1.4 ITab 内置开发者测试工具。按钮绘制在人物检视「档案」Tab 的底部
    /// Footer（与「打开完整档案馆」并排），点击后为**当前选中 pawn** 生成随机
    /// 测试数据，**最大化覆盖**所有宫格/时间轴。
    ///
    /// 生成链路（最大化覆盖所有 Read Model 累加器）：
    ///   * 工时 —— <see cref="IWorkTimeCaptureService.RecordSample"/>（×N 随机职业 + Ticks，
    ///     WorkIntensity.TotalHours + CareerBars 全填）；
    ///   * 击杀维度 —— 直接累加 PawnObject.DamageDealtTotal / MeleeKills / RangedKills
    ///     （OnKillRecorded 重载需要 victim Pawn 真实实例，dev test 用直接累加避免污染游戏世界）；
    ///   * 战役 —— <see cref="IArchiveService.OnBattleStarted"/>（colony 级 BattleObject +
    ///     BattleKpis + 个人参战次数），每场战役后立即写击杀事件 Subject 含 BattleObject 让
    ///     累计歼敌关联（v1.1.4 修复）；
    ///   * 产出 —— <see cref="IArchiveService.OnThingCrafted"/>（真实 ThingMaker 生成），
    ///     ProductionAccumulator 累加；
    ///   * 社交 —— <see cref="IArchiveEventSink.TryRecord"/>（Social），时间轴/关系聚合。
    ///
    /// 不可达（依赖真实游戏事件的累加器，公开 API 未暴露）：
    ///   * 足迹 PawnObject.PlaceHistory（需真实 PlaceVisit 累积，公开 API 未暴露写入路径）；
    ///   * 神器传承 LegacyView（需真实 ThingObject + HolderHistory，公开 API 未暴露写入路径）。
    /// 这两个宫格依赖真实游戏流程，测试按钮无法伪造 —— 显示"--"或空态是正确表现。
    ///
    /// 清理路径（ResetDevTestState）通过反射访问 ChronicleGameComponent 私有字段
    /// objectsByStableId 以同步内部索引，属受控偏离，登记于 EXC-2026-002（E1）。
    /// </summary>
    public static class DevTestButtons
    {
        private const string SourceId = "PersonalChronicle.DevTest";
        private static readonly string ButtonLabel = "PersonalChronicle.UI.Dev.Generate".Translate().ToString();

        // 最大化测试量级（充分填充宫格 + 时间轴 + 滚动溢出）。
        private const int BattlesPerPawn = 12;
        private const int KillsPerBattle = 6;
        private const int CraftsPerPawn = 15;
        private const int WorkSamplesPerPawn = 200;
        private const int SocialPerPawn = 8;

        // 击杀维度直接累加（dev 工具，避免生成 victim Pawn 污染游戏世界）。
        private const int DamageDealtTotal = 5000;
        private const int MeleeKills = 40;
        private const int RangedKills = 30;

        private static readonly string[] TestWorkTypes = new[]
        {
            "Firefighter", "Doctor", "Patient", "PatientBedRest",
            "Cook", "Hunt", "Handle", "Smith", "Tailor", "Art", "Craft",
            "Construction", "Repair", "Hauling", "Warden", "Cleaning",
            "PlantCut", "Sow", "Harvest", "PlantTend", "Research", "Mine"
        };

        /// <summary>绘制 Footer 左侧的测试按钮；仅开发者模式开启时显示，点击后为当前选中 pawn 生成测试数据。</summary>
        public static void DrawButton(Rect rect, Pawn pawn)
        {
            // 仅开发者模式（Prefs.DevMode）开启时显示，避免污染正常游戏 UI。
            if (!Prefs.DevMode)
            {
                return;
            }
            if (!Widgets.ButtonText(rect, ButtonLabel, true, true, true))
            {
                return;
            }
            try
            {
                Generate(pawn);
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Ui, "Dev test generation failed: " + ex);
                Messages.Message("PersonalChronicle.UI.Dev.GenerateFailed".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        /// <summary>为指定 pawn 生成随机测试数据并汇总提示（覆盖式：每次点击重置再生成）。</summary>
        private static void Generate(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }
            IArchiveService service = PersonalChronicleMod.ArchiveService;
            IWorkTimeCaptureService workService = PersonalChronicleMod.WorkTimeCaptureService;
            if (service == null || workService == null)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Ui, "Dev test: service unavailable.");
                return;
            }

            string id = pawn.GetUniqueLoadID();
            string label = pawn.LabelShort;
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            // 覆盖式重置：先清理上次测试残留（BattleObject / 旧事件 / 持久化累加器），
            // 再生成新的随机数据。否则多次点击会不断累加导致数字爆炸。
            ResetDevTestState(id);

            System.Random rng = new System.Random();
            int accepted = 0;
            int rejected = 0;

            // 战役+击杀交错：每场战役后立即写若干击杀事件，Subject 含 BattleObject
            // 让 BattleKpis.Kills 关联累加（v1.1.4 关键修复）。
            for (int i = 0; i < BattlesPerPawn; i++)
            {
                IncidentDef def = PickIncident(i, rng);
                if (def == null)
                {
                    rejected++;
                    continue;
                }
                BattleObject battle = null;
                try
                {
                    int battleCountBefore = service.GetObjectsOfCategory(ArchiveCategoryKeys.Battle).Count;
                    service.OnBattleStarted(def);
                    accepted++;
                    int battleCountAfter = service.GetObjectsOfCategory(ArchiveCategoryKeys.Battle).Count;
                    if (battleCountAfter > battleCountBefore)
                    {
                        IReadOnlyList<ArchiveObject> battles = service.GetObjectsOfCategory(ArchiveCategoryKeys.Battle);
                        for (int b = 0; b < battles.Count; b++)
                        {
                            BattleObject candidate = battles[b] as BattleObject;
                            if (candidate != null && candidate.IncidentDefName == def.defName && candidate.EndTick < 0L)
                            {
                                battle = candidate;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ChronicleLog.Warning(ChronicleLog.Category.Ui, "Dev test battle failed: " + ex.Message);
                    rejected++;
                    continue;
                }

                if (battle != null)
                {
                    for (int k = 0; k < KillsPerBattle; k++)
                    {
                        if (WriteKill(service, id, label, battle.StableId, i * 100 + k, rng) == CaptureResult.Accepted)
                        {
                            accepted++;
                        }
                        else
                        {
                            rejected++;
                        }
                    }
                }
                else
                {
                    for (int k = 0; k < KillsPerBattle; k++)
                    {
                        if (WriteKill(service, id, label, null, i * 100 + k, rng) == CaptureResult.Accepted)
                        {
                            accepted++;
                        }
                        else
                        {
                            rejected++;
                        }
                    }
                }
            }

            // 工时：随机职业 + Ticks（每次随机量级，每次点击看到不同数字）。
            long currentTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            int workSamples = rng.Next(WorkSamplesPerPawn, WorkSamplesPerPawn * 2);
            for (int i = 0; i < workSamples; i++)
            {
                string workType = TestWorkTypes[rng.Next(0, TestWorkTypes.Length)];
                long sampleTicks = rng.Next(500, 3000);
                WorkTimeSample sample = new WorkTimeSample(
                    id, workType, sampleTicks, currentTick, SourceId);
                if (workService.RecordSample(sample))
                {
                    accepted++;
                }
                else
                {
                    rejected++;
                }
            }

            // 产出：真实 ThingMaker + OnThingCrafted（每次随机量级）。
            int crafts = rng.Next(CraftsPerPawn, CraftsPerPawn * 2);
            for (int i = 0; i < crafts; i++)
            {
                try
                {
                    Thing product = ThingMaker.MakeThing(PickThingDef(rng), null);
                    if (product == null)
                    {
                        rejected++;
                        continue;
                    }
                    product.stackCount = rng.Next(1, 4);
                    service.OnThingCrafted(product, pawn);
                    accepted++;
                }
                catch (Exception ex)
                {
                    ChronicleLog.Warning(ChronicleLog.Category.Ui, "Dev test craft failed: " + ex.Message);
                    rejected++;
                }
            }

            // 击杀维度：每次随机量级（每次点击看到不同数字）。
            int damage = rng.Next(1000, 20000);
            int melee = rng.Next(20, 80);
            int ranged = rng.Next(20, 80);
            if (AccumulateKillStats(id, damage, melee, ranged))
            {
                accepted++;
            }

            // 损耗维度（v1.1.4 损耗宫格）：直接累加 ConsumptionAccumulator（食物/药品/成瘾品/其他）。
            if (AccumulateConsumption(id, rng))
            {
                accepted++;
            }

            // 社交：随机关系/动作。
            int socials = rng.Next(SocialPerPawn, SocialPerPawn * 2);
            for (int i = 0; i < socials; i++)
            {
                if (WriteSocial(service, id, label, i, rng) == CaptureResult.Accepted)
                {
                    accepted++;
                }
                else
                {
                    rejected++;
                }
            }

            string summary = "PersonalChronicle.UI.Dev.Regenerated".Translate(label, accepted.ToString(), rejected.ToString()).ToString();
            ChronicleLog.Info(ChronicleLog.Category.Ui, summary);
            Messages.Message(summary, MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>
        /// 覆盖式重置：清理上次测试按钮生成的 BattleObject、SourceId 事件、PawnObject 持久化累加器。
        /// 每次点击前调用，确保数据被覆盖而非累加爆炸。
        /// </summary>
        private static void ResetDevTestState(string pawnId)
        {
            try
            {
                if (Current.Game == null)
                {
                    return;
                }
                ChronicleGameComponent component = Current.Game.GetComponent<ChronicleGameComponent>();
                if (component == null)
                {
                    return;
                }

                // 清理 BattleObject（dev test 创建的）。
                // 直接 Objects.RemoveAt 后 objectsByStableId 会残留（旧查询字典命中），
                // 因此用反射同步移除字典条目，保持内部一致性（chronicle 内部 invariant）。
                if (component.Objects != null)
                {
                    System.Reflection.FieldInfo idMapField = typeof(ChronicleGameComponent)
                        .GetField("objectsByStableId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    System.Collections.IDictionary idMap = idMapField != null
                        ? idMapField.GetValue(component) as System.Collections.IDictionary
                        : null;
                    for (int i = component.Objects.Count - 1; i >= 0; i--)
                    {
                        BattleObject b = component.Objects[i] as BattleObject;
                        if (b != null && b.IncidentDefName != null)
                        {
                            // BattleObject 全部由测试按钮创建（真实战役 Capture 会让 BattleObject
                            // 含 raid Lord 关联等，dev test 不创建 raid），此处全部清理。
                            if (idMap != null && idMap.Contains(b.StableId))
                            {
                                idMap.Remove(b.StableId);
                            }
                            component.Objects.RemoveAt(i);
                        }
                    }
                }

                // 清理 SourceId=DevTest 的事件（Death/Crafted/Social）。
                if (component.Events != null)
                {
                    for (int i = component.Events.Count - 1; i >= 0; i--)
                    {
                        ChronicleEvent ev = component.Events[i];
                        if (ev != null && string.Equals(ev.SourceId, SourceId, StringComparison.Ordinal))
                        {
                            component.Events.RemoveAt(i);
                        }
                    }
                }

                // 重置 PawnObject 持久化累加器。
                PawnObject record = component.GetObject(pawnId) as PawnObject;
                if (record != null)
                {
                    record.DamageDealtTotal = 0f;
                    record.MeleeKills = 0;
                    record.RangedKills = 0;
                    record.ParticipatedBattles = 0;
                    if (record.WorkTime != null && record.WorkTime.TicksByWorkType != null)
                    {
                        record.WorkTime.TicksByWorkType.Clear();
                    }
                    if (record.Production != null)
                    {
                        if (record.Production.QuantityByDef != null) record.Production.QuantityByDef.Clear();
                        if (record.Production.MarketValueByDef != null) record.Production.MarketValueByDef.Clear();
                    }
                    if (record.Consumption != null)
                    {
                        if (record.Consumption.SilverByCategory != null) record.Consumption.SilverByCategory.Clear();
                        if (record.Consumption.SilverByDay != null) record.Consumption.SilverByDay.Clear();
                        record.Consumption.TotalSilver = 0f;
                    }
                    // P1 CAREER-001：覆盖式重置职业事实 ledger（Debug 环境）。
                    if (record.CareerData != null)
                    {
                        record.CareerData.Events.Clear();
                        if (record.CareerData.RecordCountByType != null) record.CareerData.RecordCountByType.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Ui, "Dev test reset failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 直接累加 PawnObject 的击杀维度字段（DamageDealtTotal/MeleeKills/RangedKills），
        /// 绕过 OnKillRecorded（避免生成 victim Pawn 污染游戏世界）。仅用于 ITab dev test 工具。
        /// </summary>
        private static bool AccumulateKillStats(string pawnId, int damage, int melee, int ranged)
        {
            try
            {
                if (Current.Game == null)
                {
                    return false;
                }
                ChronicleGameComponent component = Current.Game.GetComponent<ChronicleGameComponent>();
                if (component == null)
                {
                    return false;
                }
                PawnObject record = component.GetObject(pawnId) as PawnObject;
                if (record == null)
                {
                    return false;
                }
                // 直接赋值（覆盖式：Generate 已先重置这些字段为 0）。
                record.DamageDealtTotal = damage;
                record.MeleeKills = melee;
                record.RangedKills = ranged;
                return true;
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Ui, "Dev test kill stats failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// v1.1.4 损耗宫格：直接累加 PawnObject.ConsumptionAccumulator（覆盖式）。
        /// 绕过 Thing.Ingested 捕获（dev 工具无需真实进食）；类目用真实 ThingCategoryDef
        /// 或回落 "Other"，让损耗宫格 bars 有标签可显示。
        /// </summary>
        private static bool AccumulateConsumption(string pawnId, System.Random rng)
        {
            try
            {
                if (Current.Game == null)
                {
                    return false;
                }
                ChronicleGameComponent component = Current.Game.GetComponent<ChronicleGameComponent>();
                if (component == null)
                {
                    return false;
                }
                PawnObject record = component.GetObject(pawnId) as PawnObject;
                if (record == null)
                {
                    return false;
                }
                if (record.Consumption == null)
                {
                    record.Consumption = new ConsumptionAccumulator();
                }
                // 随机类目（真实 ThingCategoryDef 名，UI 会 LabelCap 显示）。
                string[] cats = new string[] { "Food", "Medicine", "Drugs", "Other" };
                float total = 0f;
                long now = Find.TickManager.TicksGame;
                record.Consumption.SilverByDay.Clear();
                for (int d = 6; d >= 0; d--)
                {
                    float day = rng.Next(0, 200);
                    record.Consumption.SilverByDay[now / 60000L - d] = day;
                    total += day;
                }
                record.Consumption.TotalSilver = total + rng.Next(500, 4000);
                record.Consumption.SilverByCategory.Clear();
                for (int i = 0; i < 3; i++)
                {
                    string cat = cats[rng.Next(cats.Length)];
                    float v;
                    record.Consumption.SilverByCategory.TryGetValue(cat, out v);
                    record.Consumption.SilverByCategory[cat] = v + rng.Next(300, 2000);
                }
                record.Consumption.LastConsumeTick = now;
                return true;
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Ui, "Dev test consumption failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 击杀事件：CombatRole=kill + 随机目标/阵营/类别。
        /// <paramref name="battleStableId"/> 非空时加入 BattleObject Subject，让 BattleKpis 累加。
        /// </summary>
        private static CaptureResult WriteKill(IArchiveService service, string pawnId, string pawnLabel,
            string battleStableId, int idx, System.Random rng)
        {
            ArchiveEntityRef primary = new ArchiveEntityRef(ArchiveCategoryKeys.Pawn, pawnId, pawnLabel);
            List<ArchiveEntityRef> subjects = null;
            if (!string.IsNullOrEmpty(battleStableId))
            {
                subjects = new List<ArchiveEntityRef>
                {
                    new ArchiveEntityRef(ArchiveCategoryKeys.Battle, battleStableId, null)
                };
            }
            ArchiveEventInput input = new ArchiveEventInput
            {
                SourceId = SourceId,
                EventTypeDefName = ChronicleEventType.Death,
                Tick = 0L,
                Primary = primary,
                Subjects = subjects,
                Parameters = new Dictionary<string, string>
                {
                    { ChronicleEventParams.CombatRole, ChronicleEventParams.CombatRoleKill },
                    { ChronicleEventParams.Killer, pawnLabel },
                    { ChronicleEventParams.Victim, "测试目标 " + (idx + 1) },
                    { ChronicleEventParams.VictimFactionLabel, Pick(rng, "海盗", "部落", "机械族", "帝国军") },
                    { ChronicleEventParams.VictimCategory, Pick(rng, ChronicleEventParams.VictimCategoryHumanlike, ChronicleEventParams.VictimCategoryMechanoid, ChronicleEventParams.VictimCategoryAnimal) },
                    { ChronicleEventParams.VictimStableId, "devtest_victim_" + pawnId + "_" + idx },
                },
                DeduplicationKey = SourceId + ":kill:" + pawnId + ":" + idx + ":" + rng.Next(0, 999999),
            };
            return service.RecordEvent(input);
        }

        /// <summary>社交事件：随机关系/动作。</summary>
        private static CaptureResult WriteSocial(IArchiveService service, string pawnId, string pawnLabel, int idx, System.Random rng)
        {
            ArchiveEventInput input = new ArchiveEventInput
            {
                SourceId = SourceId,
                EventTypeDefName = ChronicleEventType.Social,
                Tick = 0L,
                Primary = new ArchiveEntityRef(ArchiveCategoryKeys.Pawn, pawnId, pawnLabel),
                Parameters = new Dictionary<string, string>
                {
                    { ChronicleEventParams.Relation, Pick(rng, "Bond", "Lover", "Family", "Rival") },
                    { ChronicleEventParams.RelationAction, ChronicleEventParams.RelationActionFormed },
                },
                DeduplicationKey = SourceId + ":social:" + pawnId + ":" + idx + ":" + rng.Next(0, 999999),
            };
            return service.RecordEvent(input);
        }

        /// <summary>随机选取测试 IncidentDef（字段已反射核验）。</summary>
        private static IncidentDef PickIncident(int idx, System.Random rng)
        {
            switch (rng.Next(0, 4))
            {
                case 0: return IncidentDefOf.RaidEnemy;
                case 1: return IncidentDefOf.Infestation;
                case 2: return IncidentDefOf.MechCluster;
                default: return idx % 2 == 0 ? IncidentDefOf.RaidFriendly : IncidentDefOf.RaidEnemy;
            }
        }

        /// <summary>随机选取测试 ThingDef（字段已反射核验）。</summary>
        private static ThingDef PickThingDef(System.Random rng)
        {
            switch (rng.Next(0, 4))
            {
                case 0: return RimWorld.ThingDefOf.MeleeWeapon_Knife;
                case 1: return RimWorld.ThingDefOf.Apparel_Parka;
                case 2: return RimWorld.ThingDefOf.Apparel_Tuque;
                default: return RimWorld.ThingDefOf.Cloth;
            }
        }

        private static string Pick(System.Random rng, params string[] options)
        {
            return options[rng.Next(0, options.Length)];
        }

        // ---- P1 CAREER-001 职业事实层调试设施（BE-007 / FE-001）-----------------

        /// <summary>
        /// 取 pawn 的 <see cref="PawnObject"/>（经 ChronicleGameComponent）；无则 null。
        /// 仅 Dev 工具使用。
        /// </summary>
        private static PawnObject GetCareerPawnObject(Pawn pawn)
        {
            if (pawn == null || Current.Game == null)
            {
                return null;
            }
            ChronicleGameComponent component = Current.Game.GetComponent<ChronicleGameComponent>();
            if (component == null)
            {
                return null;
            }
            return component.GetObject(pawn.GetUniqueLoadID()) as PawnObject;
        }

        /// <summary>BE-007 Print：输出职业事实 ledger 摘要到日志 + 屏幕消息。</summary>
        public static void CareerPrint(Pawn pawn)
        {
            PawnObject record = GetCareerPawnObject(pawn);
            if (record == null || record.CareerData == null)
            {
                Messages.Message("PersonalChronicle.UI.Dev.NoCareer".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            List<CareerEvent> events = record.CareerData.Events;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("== Career Events: " + (pawn?.LabelShort ?? "?") + " (Total " + events.Count + ") ==");
            for (int i = 0; i < events.Count; i++)
            {
                CareerEvent e = events[i];
                sb.AppendLine(e.Tick + " | " + e.EventType + " | " + e.DefName
                    + " | " + (e.Quality ?? "-") + " | " + (e.SkillDefName ?? "-"));
            }
            ChronicleLog.Info(ChronicleLog.Category.Ui, sb.ToString());
            Messages.Message("PersonalChronicle.UI.Dev.Printed".Translate(events.Count.ToString()).ToString(), MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>BE-007 Add Test：为 pawn 追加一条 ItemProduced 测试事实（模拟制造）。</summary>
        public static void CareerAddTest(Pawn pawn)
        {
            PawnObject record = GetCareerPawnObject(pawn);
            if (record == null)
            {
                Messages.Message("PersonalChronicle.UI.Dev.PawnNotArchived".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (record.CareerData == null)
            {
                record.CareerData = new CareerData();
            }
            long tick = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            string pawnId = pawn.GetUniqueLoadID();
            CareerEvent ev = new CareerEvent(
                pawnId + ":" + tick + ":devtest",
                pawnId,
                tick,
                CareerEventType.ItemProduced,
                "DevTest_Product",
                "Crafting",
                "Excellent",
                null);
            record.CareerData.Events.Add(ev);
            IncrementCareerCount(record.CareerData, CareerEventType.ItemProduced);
            ChronicleGameComponent component = Current.Game?.GetComponent<ChronicleGameComponent>();
            component?.MarkChanged();
            Messages.Message("PersonalChronicle.UI.Dev.EventAdded".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>BE-007 Clear：清空 pawn 的职业事实 ledger。</summary>
        public static void CareerClear(Pawn pawn)
        {
            PawnObject record = GetCareerPawnObject(pawn);
            if (record?.CareerData != null)
            {
                record.CareerData.Events.Clear();
                if (record.CareerData.RecordCountByType != null) record.CareerData.RecordCountByType.Clear();
                ChronicleGameComponent component = Current.Game?.GetComponent<ChronicleGameComponent>();
                component?.MarkChanged();
            }
            Messages.Message("PersonalChronicle.UI.Dev.Cleared".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>BE-007 Validate：校验 ledger 完整性（去重/类型白名单/空字段），输出到日志。</summary>
        public static void CareerValidate(Pawn pawn)
        {
            PawnObject record = GetCareerPawnObject(pawn);
            if (record == null || record.CareerData == null)
            {
                Messages.Message("PersonalChronicle.UI.Dev.NoCareer".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            List<CareerEvent> events = record.CareerData.Events;
            int dup = 0;
            int badType = 0;
            int nullField = 0;
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < events.Count; i++)
            {
                CareerEvent e = events[i];
                if (e == null || string.IsNullOrEmpty(e.EventId) || string.IsNullOrEmpty(e.EventType)
                    || string.IsNullOrEmpty(e.PawnId) || string.IsNullOrEmpty(e.DefName))
                {
                    nullField++;
                    continue;
                }
                if (!CareerEventType.IsAllowed(e.EventType))
                {
                    badType++;
                }
                if (!seen.Add(e.EventId))
                {
                    dup++;
                }
            }
            string report = "Career Validate: total=" + events.Count + " dup=" + dup
                + " badType=" + badType + " nullField=" + nullField;
            ChronicleLog.Info(ChronicleLog.Category.Ui, report);
            Messages.Message("PC Career: " + report, dup == 0 && badType == 0 && nullField == 0
                ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, false);
        }

        private static void IncrementCareerCount(CareerData data, string eventType)
        {
            if (data.RecordCountByType == null) data.RecordCountByType = new Dictionary<string, int>();
            if (data.RecordCountByType.ContainsKey(eventType)) data.RecordCountByType[eventType]++;
            else data.RecordCountByType[eventType] = 1;
        }

        /// <summary>
        /// 随机化职业全链路测试数据（P1~P8 + 专业适配分析输入），仅 DevMode 工具用。
        /// 覆盖式：先清空再随机生成，便于反复点击看到不同数字；生成后触发 MarkChanged 使 ReadModel 重建。
        /// 覆盖范围（对齐 docs/UI预览/人物档案视窗/职业档案Tab预览.html randomizeCareerData）：
        ///   1) 原版 12 技能等级 + 兴趣（驱动职业规划 12 专业适配分析 chips）
        ///   2) 专业技能等级 + 能力 XP + 实践计数（按原版技能键）
        ///   3) CareerEvent 事实（制造/建造/研究，品质分布含 Masterwork，传奇触发成就）
        ///   4) 职称 5 档随机推导（等级区间 → 档位 → 职称/资格联动，非固定档）
        ///   5) 资格状态/预检/下一职称（考试/论文/答辩按下一档资格生成通过记录）
        ///   6) 书籍证据（1~4 本）、生涯工时跨度（300~1200h）
        ///   7) 勋章墙：成就勋章（Legend/MajorProject 联动）+ 随机 2~6 枚 Pawn 阈值勋章（清空重授）
        ///   8) 工坊快照（履历页/当前就职数据源）
        /// </summary>
        public static void CareerRandomize(Pawn pawn)
        {
            if (!Prefs.DevMode || pawn == null)
            {
                return;
            }
            PawnObject record = GetCareerPawnObject(pawn);
            if (record == null)
            {
                Messages.Message("PersonalChronicle.UI.Dev.PawnNotArchived".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (record.CareerData == null)
            {
                record.CareerData = new CareerData();
            }
            System.Random rng = new System.Random();
            long now = Find.TickManager != null ? Find.TickManager.TicksGame : 0L;
            string pawnId = pawn.GetUniqueLoadID();
            CareerData cd = record.CareerData;

            // 0) 覆盖式重置职业相关全部容器（含勋章墙——HTML 语义：清空再随机）。
            cd.Events.Clear();
            if (cd.RecordCountByType != null) cd.RecordCountByType.Clear();
            cd.Professional = new ProfessionalState();
            cd.Qualification = new QualificationState();
            cd.Exams = new ExamData();
            cd.Books = new List<BookEvidence>();
            cd.Thesis = new ThesisData();
            cd.GrantedTitles = new List<GrantedTitle>();
            record.GrantedMedals = new List<string>();

            // 1) 原版 12 技能等级 + 兴趣（活读 pawn.skills；驱动 ProfessionalFitAnalyzer）。
            RandomizeVanillaSkills(pawn, rng);

            // 2) 专业等级（精密制造）+ 能力 XP + 实践计数（键 = 原版技能 defName）。
            int level = rng.Next(10, 50);
            ProfessionalSkillData skill = new ProfessionalSkillData
            {
                skillDefName = "ProfessionalSkill_PrecisionManufacturing",
                level = level,
                xp = level * 100f,
                mastery = (float)level / 50f * 100f,
                firstAcquiredTick = now - 1800000L,
                lastPracticeTick = now - 1000L,
                practiceCount = rng.Next(20, 200)
            };
            skill.abilityXp = new Dictionary<string, float>
            {
                { "machining", rng.Next(100, 5000) },
                { "precisionControl", rng.Next(100, 5000) },
                { "processKnowledge", rng.Next(100, 5000) },
                { "qualityControl", rng.Next(100, 5000) }
            };
            cd.Professional.skills.Add(skill);
            if (cd.Professional.practiceCountBySkill == null) cd.Professional.practiceCountBySkill = new Dictionary<string, int>();
            // 12 原版技能实践计数（Crafting 保底偏强，对齐 HTML SAMPLE_PRACTICE 语义）。
            for (int s = 0; s < VanillaSkillDefNames.Length; s++)
            {
                string key = VanillaSkillDefNames[s];
                int v = rng.Next(0, 340);
                if (key == "Crafting") v = Mathf.Max(v, 180);
                if (key == "Construction") v = Mathf.Max(v, 120);
                cd.Professional.practiceCountBySkill[key] = v;
            }

            // 3) 生涯跨度（资历 300~1200h → 事件首末跨度，驱动 CareerOverview.HoursText）。
            int hours = rng.Next(300, 1201);
            long spanTicks = (long)hours * 2400L;
            long startTick = now - spanTicks;
            long firstTick = startTick;

            // 4) 随机制造事实（品质 5 档含 Masterwork，对齐 HTML QUAL_W 权重表；传奇触发 P8 成就）。
            int itemCount = rng.Next(8, 30);
            string[] qualities = { "Normal", "Good", "Excellent", "Masterwork", "Legendary" };
            int legendary = 0;
            int major = 0;
            for (int i = 0; i < itemCount; i++)
            {
                string q = qualities[rng.Next(qualities.Length)];
                Dictionary<string, string> meta = null;
                if (q == "Legendary") legendary++;
                if (rng.Next(0, 5) == 0)
                {
                    meta = new Dictionary<string, string> { { "major", "1" } };
                    major++;
                }
                long t = startTick + (long)((i + 1) * spanTicks / (itemCount + 1));
                cd.Events.Add(new CareerEvent(
                    pawnId + ":" + t + ":item:" + i,
                    pawnId, t, CareerEventType.ItemProduced,
                    "DevTest_Product", "Crafting", q, "Make_ComponentIndustrial", 1, meta));
                IncrementCareerCount(cd, CareerEventType.ItemProduced);
            }
            // 建造/研究事实（驱动总览 metric 三格与荣誉贡献结构；均为真实计数）。
            int buildCount = rng.Next(0, 6);
            int researchCount = rng.Next(0, 6);
            for (int i = 0; i < buildCount; i++)
            {
                long t = startTick + (long)((i + 1) * spanTicks / (buildCount + 1));
                cd.Events.Add(new CareerEvent(pawnId + ":" + t + ":build:" + i, pawnId, t,
                    CareerEventType.ConstructionCompleted, "DevTest_Building", "Construction", null, null, 1, null));
                IncrementCareerCount(cd, CareerEventType.ConstructionCompleted);
            }
            for (int i = 0; i < researchCount; i++)
            {
                long t = startTick + (long)((i + 1) * spanTicks / (researchCount + 1));
                cd.Events.Add(new CareerEvent(pawnId + ":" + t + ":research:" + i, pawnId, t,
                    CareerEventType.ResearchCompleted, "DevTest_Research", "Intellectual", null, null, 1, null));
                IncrementCareerCount(cd, CareerEventType.ResearchCompleted);
            }

            // 5) 职称 5 档随机推导（对齐 HTML buildCareerOverview：等级区间 + 传奇门槛）。
            string tierKey = level >= 45 && legendary >= 1 ? "Master"
                : level >= 38 ? "Specialist"
                : level >= 25 ? "Senior"
                : level >= 15 ? "Assistant" : "Junior";
            string titleDefName = "Title_Precision_" + tierKey;
            string qualDefName = "Q_Precision_" + tierKey;
            // 下一档资格（考试/论文/答辩记录按此生成，匹配 ReadModel 判定契约）。
            string nextQualDefName = NextQualificationDefName(cd, qualDefName);
            long titleTick = now - 2000L;
            cd.Events.Add(new CareerEvent(pawnId + ":" + titleTick + ":title", pawnId, titleTick,
                CareerEventType.TitleGranted, titleDefName, "ProfessionalSkill_PrecisionManufacturing", null, null, 1, null));
            cd.GrantedTitles.Add(new GrantedTitle(titleDefName, qualDefName, titleTick));
            IncrementCareerCount(cd, CareerEventType.TitleGranted);

            // 6) 资格进度（当前档全通过；评分随机 70~95，保证 CompositeScore 达标）。
            QualificationProgress qp = cd.Qualification.GetOrAdd(qualDefName);
            qp.Status = QualificationStatus.Granted;
            qp.PracticalPassed = true;
            qp.TheoryPassed = true;
            qp.ThesisPassed = true;
            qp.DefensePassed = true;
            qp.CompositeScore = 70f + (float)rng.Next(0, 26);
            qp.DecidedTick = titleTick;
            if (nextQualDefName != null)
            {
                // 考试/论文/答辩通过记录（针对下一档资格生成；BuildQualRows/PreCheck 按此判定）。
                cd.Events.Add(new CareerEvent(pawnId + ":" + now + ":exam", pawnId, now,
                    CareerEventType.ExamPassed, nextQualDefName, null, null, null, 1, null));
                IncrementCareerCount(cd, CareerEventType.ExamPassed);
                cd.Exams.Practical.Add(new PracticalExamRecord
                {
                    ExamId = pawnId + ":pexam",
                    QualificationDefName = nextQualDefName,
                    TargetRecipeDefNames = new List<string> { "Make_ComponentIndustrial" },
                    RequiredCount = 3,
                    MinQuality = "Excellent",
                    TimeLimitTicks = 100000L,
                    StartedTick = now - 50000L,
                    ProducedCount = 3,
                    ProducedQualities = new List<string> { "Excellent", "Excellent", "Excellent" },
                    Passed = true,
                    Score = 100f
                });
                cd.Exams.Theory.Add(new TheoryExamRecord
                {
                    QualificationDefName = nextQualDefName,
                    RequiredBookTopics = new List<string> { "Precision" },
                    RequiredResearchCount = 2,
                    BookScore = 90f,
                    ResearchScore = 80f,
                    SkillScore = 85f,
                    ActivityScore = 70f,
                    Passed = true,
                    Score = 85f
                });
                cd.Thesis.Theses.Add(new ThesisEvidence
                {
                    ThesisId = nextQualDefName,
                    QualificationDefName = nextQualDefName,
                    SourceBookIds = new List<string> { "book1" },
                    SourceResearchEventIds = new List<string> { "r1" },
                    BaseQuality = 85f,
                    ComputedScore = 88f,
                    Completed = true,
                    CompletedTick = now - 500L
                });
                cd.Thesis.Defenses.Add(new DefenseRecord
                {
                    ThesisId = nextQualDefName,
                    QualificationDefName = nextQualDefName,
                    CommitteePawnIds = new List<string> { "cm1", "cm2" },
                    CommitteeScore = 92f,
                    FinalScore = 90f,
                    Passed = true,
                    HeldTick = now - 200L
                });
            }

            // 7) 书籍证据 1~4 本（理论考试语义：Books.Count 驱动 Ov.Books 与摘要）。
            int bookCount = rng.Next(1, 5);
            for (int b = 0; b < bookCount; b++)
            {
                cd.Books.Add(new BookEvidence
                {
                    BookThingId = "book" + b,
                    AuthorPawnId = pawnId,
                    Topic = "Precision",
                    Quality = b < 2 ? "Good" : "Normal",
                    Field = "Manufacturing",
                    CreatedTick = now - 30000L - b * 1000L,
                    Relevance = 1f
                });
                IncrementCareerCount(cd, CareerEventType.BookProduced);
            }

            // 8) 勋章墙：成就勋章（传奇/重大联动）+ 随机 2~6 枚 Pawn 阈值勋章（清空重授）。
            record.GrantedMedals = new List<string>();
            if (legendary >= 1) record.AddGrantedMedal("Medal_Craft_Legend_Bronze");
            if (legendary >= 5) record.AddGrantedMedal("Medal_Craft_Legend_Silver");
            if (legendary >= 15) record.AddGrantedMedal("Medal_Craft_Legend_Gold");
            if (major >= 3) record.AddGrantedMedal("Medal_Craft_MajorProject_Gold");
            List<string> awarded = new List<string>();
            if (legendary >= 1) awarded.Add("Medal_Craft_Legend_Bronze");
            if (legendary >= 5) awarded.Add("Medal_Craft_Legend_Silver");
            if (legendary >= 15) awarded.Add("Medal_Craft_Legend_Gold");
            if (major >= 3) awarded.Add("Medal_Craft_MajorProject_Gold");
            int thresholdCount = rng.Next(2, 7);
            List<string> pool = new List<string>(PawnThresholdMedalDefNames);
            while (awarded.Count < 12 && thresholdCount > 0 && pool.Count > 0)
            {
                int pick = rng.Next(pool.Count);
                string defName = pool[pick];
                pool.RemoveAt(pick);
                if (awarded.Contains(defName)) continue;
                awarded.Add(defName);
                record.AddGrantedMedal(defName);
                thresholdCount--;
            }
            // 勋章授予事实回写（驱动「最近荣誉事件」时间线；每枚一条）。
            for (int m = 0; m < awarded.Count; m++)
            {
                long mt = now - 1000L - m * 500L;
                cd.Events.Add(new CareerEvent(pawnId + ":" + mt + ":medal:" + m, pawnId, mt,
                    CareerEventType.MedalGranted, awarded[m], null, null, null, 1, null));
                IncrementCareerCount(cd, CareerEventType.MedalGranted);
            }

            // 9) 工坊快照（履历页 resume-block / 工坊汇总 / 当前就职数据源）。
            record.Workplace = new WorkplaceSnapshot
            {
                BuildingDefName = "TableMachining",
                BuildingStableId = "TableMachining:1",
                CustomName = null,
                RoomRoleDefName = "Workshop",
                UseCount = rng.Next(200, 2101),
                LastUsedTick = now - 1000L,
                MapIndex = -1,

            };

            ChronicleGameComponent component = Current.Game?.GetComponent<ChronicleGameComponent>();
            component?.MarkChanged();
            Messages.Message("PersonalChronicle.UI.Dev.Randomized".Translate(tierKey, level.ToString(), legendary.ToString(), major.ToString(), awarded.Count.ToString()),
                MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>
        /// 随机化原版 12 技能等级与兴趣（DevMode 测试用；驱动职业规划适配分析输入）。
        /// 对齐 HTML SAMPLE_SKILLS / SAMPLE_PASSION 语义：Crafting 保底 ≥12，其余 1~20。
        /// </summary>
        private static void RandomizeVanillaSkills(Pawn pawn, System.Random rng)
        {
            if (pawn == null || pawn.skills == null) return;
            for (int i = 0; i < VanillaSkillDefNames.Length; i++)
            {
                SkillDef sd = DefDatabase<SkillDef>.GetNamedSilentFail(VanillaSkillDefNames[i]);
                if (sd == null) continue;
                SkillRecord rec = pawn.skills.GetSkill(sd);
                if (rec == null) continue;
                int v = rng.Next(1, 21);
                if (VanillaSkillDefNames[i] == "Crafting") v = Mathf.Max(v, 12);
                rec.Level = v;
                int roll = rng.Next(0, 10);
                rec.passion = roll < 3 ? Passion.Major : (roll < 6 ? Passion.Minor : Passion.None);
            }
        }

        /// <summary>
        /// 按 order 升序取第一个「其职称未授予」的资格 defName（与 ReadModel FindNextQualification 同序），
        /// 用于把考试/论文/答辩记录挂到正确的下一档资格上。无则返回 null（已封顶）。
        /// </summary>
        private static string NextQualificationDefName(CareerData cd, string grantedQualDefName)
        {
            if (cd == null || cd.GrantedTitles == null || string.IsNullOrEmpty(grantedQualDefName)) return null;
            List<QualificationDef> all = DefDatabase<QualificationDef>.AllDefsListForReading
                .Where(q => q != null && !string.IsNullOrEmpty(q.titleDefName))
                .OrderBy(q => q.order)
                .ToList();
            for (int i = 0; i < all.Count; i++)
            {
                QualificationDef q = all[i];
                if (string.IsNullOrEmpty(q.titleDefName)) continue;
                bool granted = false;
                for (int k = 0; k < cd.GrantedTitles.Count; k++)
                {
                    GrantedTitle gt = cd.GrantedTitles[k];
                    if (gt != null && string.Equals(gt.TitleDefName, q.titleDefName, StringComparison.Ordinal))
                    {
                        granted = true;
                        break;
                    }
                }
                if (!granted) return q.defName;
            }
            return null;
        }

        /// <summary>12 原版技能稳定键（与 ITab_Pawn_Career / ProfessionalFitAnalyzer 输入键一致）。</summary>
        private static readonly string[] VanillaSkillDefNames =
        {
            "Shooting", "Melee", "Construction", "Mining", "Cooking", "Plants",
            "Animals", "Crafting", "Artistic", "Medicine", "Social", "Intellectual"
        };

        /// <summary>Pawn 归属阈值勋章池（21 枚；Legacy 传承系为 Thing 归属、Craft 系为成就类，不在此池）。</summary>
        private static readonly string[] PawnThresholdMedalDefNames =
        {
            "Medal_Labor_Model_Bronze", "Medal_Labor_Model_Silver", "Medal_Labor_Model_Gold",
            "Medal_Labor_Worker_Bronze", "Medal_Labor_Worker_Silver", "Medal_Labor_Worker_Gold",
            "Medal_Labor_TechAce_Bronze", "Medal_Labor_TechAce_Silver", "Medal_Labor_TechAce_Gold",
            "Medal_Combat_Hero_Bronze", "Medal_Combat_Hero_Silver", "Medal_Combat_Hero_Gold",
            "Medal_Combat_FirstClass_Bronze", "Medal_Combat_FirstClass_Silver", "Medal_Combat_FirstClass_Gold",
            "Medal_Combat_Enlistee_Bronze", "Medal_Combat_Enlistee_Silver", "Medal_Combat_Enlistee_Gold",
            "Medal_Support_Quartermaster_Bronze", "Medal_Support_Quartermaster_Silver", "Medal_Support_Quartermaster_Gold"
        };

        /// <summary>
        /// FE-001 Career Debug Inspector：在档案 ITab（DevMode）渲染职业事实 ledger
        /// 文本列表，用于验证后端是否正确记录游戏行为。仅展示，不写任何评价。
        /// </summary>
        public static void DrawCareerInspector(Rect rect, Pawn pawn)
        {
            if (!Prefs.DevMode || pawn == null)
            {
                return;
            }
            PawnObject record = GetCareerPawnObject(pawn);
            if (record == null || record.CareerData == null)
            {
                return;
            }
            List<CareerEvent> events = record.CareerData.Events;
            float lineH = 18f;
            // 标题
            string header = "Career Events (" + events.Count + ")";
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, lineH), header);
            float y = rect.y + lineH;
            int maxRows = Mathf.FloorToInt((rect.height - lineH) / lineH);
            int start = Mathf.Max(0, events.Count - maxRows);
            for (int i = start; i < events.Count && y < rect.y + rect.height - lineH; i++)
            {
                CareerEvent e = events[i];
                string line = e.Tick + "  " + e.EventType + "  " + e.DefName
                    + "  " + (e.Quality ?? "-") + "  " + (e.SkillDefName ?? "-");
                Widgets.Label(new Rect(rect.x, y, rect.width, lineH), line);
                y += lineH;
            }
        }
    }
}
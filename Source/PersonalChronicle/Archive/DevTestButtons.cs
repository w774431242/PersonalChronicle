using System;
using System.Collections.Generic;
using PersonalChronicle.Api;
using PersonalChronicle.Application;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
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
    /// </summary>
    public static class DevTestButtons
    {
        private const string SourceId = "PersonalChronicle.DevTest";
        private const string ButtonLabel = "PC: 生成测试数据";

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
                Messages.Message("PC: 测试数据生成失败，详见日志", MessageTypeDefOf.RejectInput, false);
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

            string summary = "PC: 已为 " + label + " 重新生成测试数据（随机量级）→ 接受 " + accepted + "，拒绝 " + rejected;
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
    }
}
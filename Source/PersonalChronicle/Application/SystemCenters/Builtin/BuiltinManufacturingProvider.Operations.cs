using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.ReadModels;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PersonalChronicle.Application.SystemCenters.Builtin
{
    /// <summary>
    /// 制造中心 ④ 运营（在岗殖民者 / 成品库 / Andon 占位）—— 数据构建部分。
    ///
    /// <para><b>口径（全部原版可读）</b></para>
    /// <list type="bullet">
    /// <item><b>在岗殖民者</b>：<c>map.mapPawns.FreeColonistsSpawned</c>；心情取 <c>Need_Mood.CurLevel</c>
    /// （阈值与预览一致：&gt;70% 好 / &gt;50% 警示 / 否则差）；技能取 <c>pawn.skills.skills</c> 的
    /// <c>Level</c>（热力格 = 每个技能一格）；任务取该人当前 <c>JobDriver_DoBill</c> 绑定的设备，无则「待命」。</item>
    /// <item><b>成品库</b>：地图上带 <c>CompQuality</c> 的可搬运物品，逐件列出（前 N），品质用原版
    /// <c>QualityUtility.GetLabel</c>。预览第三列是「入库时间」，原版没有逐件产出时间戳 ⇒ 改为显示
    /// <b>堆叠数 ×N</b>（不编造时间）。</item>
    /// <item><b>Andon 异常聚合</b>：预览本身就是 <c>panel pending</c> 占位（待 DataBridge）——照实留占位。</item>
    /// </list>
    /// </summary>
    public sealed partial class BuiltinManufacturingProvider : IManufacturingOperationsDataProvider
    {
        /// <summary>成品库逐件清单上限（预览取前 12）。</summary>
        private const int MaxDepotItems = 12;

        /// <summary>成品库扫描物品数上限（防超大殖民地卡顿）。</summary>
        private const int MaxDepotScan = 6000;

        /// <summary>技能热力格上限（原版 12 项技能）。</summary>
        private const int MaxSkillCells = 12;

        public ManufacturingOperationsSnapshot BuildOperations(Map map)
        {
            ManufacturingOperationsSnapshot snapshot = new ManufacturingOperationsSnapshot();
            try
            {
                if (map == null)
                {
                    snapshot.IsEmpty = true;
                    return snapshot;
                }
                AppendWorkers(snapshot, map);
                AppendDepot(snapshot, map);
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Provider, "manufacturing operations failed: " + ex);
                snapshot.IsEmpty = true;
            }
            return snapshot;
        }

        private static void AppendWorkers(ManufacturingOperationsSnapshot snapshot, Map map)
        {
            List<Pawn> colonists = map.mapPawns != null ? map.mapPawns.FreeColonistsSpawned : null;
            if (colonists == null)
            {
                return;
            }
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (pawn == null || pawn.skills == null)
                {
                    continue;
                }

                OpsWorkerView view = new OpsWorkerView
                {
                    Name = pawn.LabelShort
                };

                // 心情（>70 好 / >50 警示 / 否则差 —— 与预览的 moodCol 一致）
                Need_Mood mood = pawn.needs?.mood;
                if (mood != null)
                {
                    view.Mood01 = mood.CurLevel;
                    view.MoodText = Mathf.RoundToInt(mood.CurLevel * 100f).ToString(CultureInfo.InvariantCulture) + "%";
                    view.MoodTone = mood.CurLevel > 0.70f
                        ? SystemCenterTone.Good
                        : (mood.CurLevel > 0.50f ? SystemCenterTone.Warn : SystemCenterTone.Bad);
                }

                // 技能：热力格 + 前三名
                List<SkillRecord> skills = pawn.skills.skills;
                List<OpsSkillCell> sorted = new List<OpsSkillCell>();
                int intel = 0;
                for (int s = 0; s < skills.Count; s++)
                {
                    SkillRecord record = skills[s];
                    if (record?.def == null)
                    {
                        continue;
                    }
                    string label = string.IsNullOrEmpty(record.def.label) ? record.def.defName : record.def.label;
                    if (!record.TotallyDisabled)
                    {
                        sorted.Add(new OpsSkillCell { Label = label, Level = record.Level });
                    }
                    if (record.def == SkillDefOf.Intellectual)
                    {
                        intel = record.Level;
                    }
                }
                sorted.Sort((a, b) => b.Level.CompareTo(a.Level));
                view.IntelText = T1("PersonalChronicle.UI.Ops.Intel", intel);

                int topCount = Mathf.Min(3, sorted.Count);
                for (int t = 0; t < topCount; t++)
                {
                    view.TopSkills.Add(sorted[t].Label + " " + sorted[t].Level.ToString(CultureInfo.InvariantCulture));
                }
                // 热力格按技能表原顺序（预览 SKILLS 顺序稳定），不按等级排
                int cells = Mathf.Min(MaxSkillCells, skills.Count);
                for (int s = 0; s < cells; s++)
                {
                    SkillRecord record = skills[s];
                    if (record?.def == null || record.TotallyDisabled)
                    {
                        continue;
                    }
                    view.Heat.Add(new OpsSkillCell
                    {
                        Label = string.IsNullOrEmpty(record.def.label) ? record.def.defName : record.def.label,
                        Level = record.Level
                    });
                }

                view.TaskText = ResolveTask(pawn);
                view.Tooltip = view.Name + " · " + view.IntelText
                    + (string.IsNullOrEmpty(view.MoodText) ? string.Empty : " · " + view.MoodText)
                    + "\n" + view.TaskText;
                snapshot.Workers.Add(view);
            }
        }

        /// <summary>当前任务：该人正在执行的 DoBill 绑定的设备名；没有则「待命」。</summary>
        private static string ResolveTask(Pawn pawn)
        {
            JobDriver_DoBill driver = pawn.jobs?.curDriver as JobDriver_DoBill;
            Thing bench = driver?.BillGiver as Thing;
            if (bench?.def == null)
            {
                return T("PersonalChronicle.UI.Ops.Task.Idle");
            }
            return TS("PersonalChronicle.UI.Ops.Task.Working", LabelOf(bench.def));
        }

        private static string TS(string key, string arg)
        {
            return key.Translate(arg).ToString();
        }

        private static void AppendDepot(ManufacturingOperationsSnapshot snapshot, Map map)
        {
            List<Thing> things = map.listerThings != null
                ? map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver)
                : null;
            int total = 0;
            if (things != null)
            {
                int limit = Mathf.Min(things.Count, MaxDepotScan);
                for (int i = 0; i < limit; i++)
                {
                    Thing thing = things[i];
                    if (thing == null)
                    {
                        continue;
                    }
                    CompQuality quality = thing.TryGetComp<CompQuality>();
                    if (quality == null)
                    {
                        continue;
                    }
                    total++;
                    if (snapshot.DepotItems.Count >= MaxDepotItems)
                    {
                        continue; // 只截"逐件列表"，件数仍全量统计
                    }
                    snapshot.DepotItems.Add(new OpsDepotItemView
                    {
                        QualityLabel = quality.Quality.GetLabel(),
                        QualityTone = QualityTone(quality.Quality),
                        Name = LabelOf(thing.def),
                        CountText = "×" + thing.stackCount.ToString(CultureInfo.InvariantCulture)
                    });
                }
            }
            snapshot.DepotEmpty = total == 0;
            snapshot.DepotCountText = T1("PersonalChronicle.UI.Ops.Depot.Count", total);
        }

        /// <summary>品质语气（7 档 → 4 色语义，UI 只按语气取色）。</summary>
        private static SystemCenterTone QualityTone(QualityCategory quality)
        {
            if (quality >= QualityCategory.Excellent)
            {
                return SystemCenterTone.Good;
            }
            if (quality >= QualityCategory.Good)
            {
                return SystemCenterTone.Flat;
            }
            return quality <= QualityCategory.Poor ? SystemCenterTone.Bad : SystemCenterTone.Warn;
        }
    }
}

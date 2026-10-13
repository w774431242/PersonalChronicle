using System;
using System.Collections.Generic;
using System.Text;
using PersonalChronicle.Data;
using PersonalChronicle.Domain;
using PersonalChronicle.Domain.Honor;
using PersonalChronicle.Domain.Profession;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Archive
{
    /// <summary>
    /// [DEV] 游戏内「全量数据覆盖自检」。只读扫描当前殖民地全部 humanlike pawn 的
    /// PawnObject 累加器 + 全局 Archive 对象/事件，按维度统计填充率，弹出报告窗口。
    ///
    /// 纯只读：不修改任何存档/累加器，不触发 MarkChanged，零契约改动。
    /// 定位：与 DevTestButtons.Generate（注入测试数据）互补——Generate 灌满，本工具
    /// 测量真实覆盖，组合即可验证所有 UI 宫格是否都能正确显示数据。
    ///
    /// 注：足迹(PlaceHistory) 等依赖真实游戏流程的维度标注「需真实流程」，不计入失败
    /// 项（与 DevTestButtons 注释一致：公开 API 未暴露写入路径，dev 无法伪造）。
    /// </summary>
    public static class DevCoverageProbe
    {
        // ---- 报告数据结构 ----------------------------------------------------

        /// <summary>单维度覆盖结果。</summary>
        public sealed class DimensionResult
        {
            public string Key;
            public string Label;
            public int Filled;
            public int Total;
            public bool UnreachableViaDev;
            public string Note;

            public float Coverage => Total <= 0 ? 0f : (float)Filled / Total;
            public string Status
            {
                get
                {
                    if (Total <= 0)
                    {
                        return "PersonalChronicle.UI.Dev.Cov.None".Translate().ToString();
                    }
                    if (Filled <= 0)
                    {
                        return UnreachableViaDev
                            ? "PersonalChronicle.UI.Dev.Cov.WaitPlay".Translate().ToString()
                            : "PersonalChronicle.UI.Dev.Cov.Empty".Translate().ToString();
                    }
                    if (Filled >= Total)
                    {
                        return "PersonalChronicle.UI.Dev.Cov.Full".Translate().ToString();
                    }
                    return "PersonalChronicle.UI.Dev.Cov.Partial".Translate().ToString();
                }
            }
        }

        /// <summary>整份覆盖报告。</summary>
        public sealed class CoverageReport
        {
            public int HumanlikeTotal;
            public int ArchivedTotal;
            public int BattleObjectCount;
            public int ThingObjectCount;
            public int LocationObjectCount;
            public int ChronicleEventCount;
            public Dictionary<string, int> EventByType = new Dictionary<string, int>();
            public List<DimensionResult> Dimensions = new List<DimensionResult>();
        }

        // ---- ITab footer 入口 ------------------------------------------------

        /// <summary>绘制 ITab footer 按钮；仅开发者模式显示。点击后扫描全殖民地并弹报告。</summary>
        public static void DrawButton(Rect rect)
        {
            if (!Prefs.DevMode)
            {
                return;
            }
            if (!Widgets.ButtonText(rect, "PersonalChronicle.UI.Dev.Coverage".Translate().ToString(), true, true, true))
            {
                return;
            }
            try
            {
                CoverageReport report = ProbeColony();
                Find.WindowStack.Add(new Dialog_DevCoverageReport(report));
                LogProbe(report);
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Ui, "Dev coverage probe failed: " + ex);
                Messages.Message("PersonalChronicle.UI.Dev.CoverageFailed".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        // ---- 扫描核心（只读）------------------------------------------------

        /// <summary>扫描全殖民地，返回覆盖报告。绝不修改任何数据。</summary>
        public static CoverageReport ProbeColony()
        {
            CoverageReport report = new CoverageReport();
            if (Current.Game == null)
            {
                return report;
            }
            ChronicleGameComponent component = Current.Game.GetComponent<ChronicleGameComponent>();
            if (component == null)
            {
                return report;
            }

            // 收集全部 humanlike pawn（跨所有地图，含囚犯/访客）。
            List<Pawn> pawns = new List<Pawn>();
            foreach (Map map in Find.Maps)
            {
                if (map == null || map.mapPawns == null)
                {
                    continue;
                }
                foreach (Pawn p in map.mapPawns.AllPawns)
                {
                    if (p != null && p.RaceProps != null && p.RaceProps.Humanlike)
                    {
                        pawns.Add(p);
                    }
                }
            }
            report.HumanlikeTotal = pawns.Count;

            // 取已归档者（有 PawnObject 的）。
            List<PawnObject> records = new List<PawnObject>();
            foreach (Pawn p in pawns)
            {
                PawnObject rec = component.GetObject(p.GetUniqueLoadID()) as PawnObject;
                if (rec != null)
                {
                    records.Add(rec);
                }
            }
            report.ArchivedTotal = records.Count;

            // 逐维度统计填充数。
            List<DimDef> dims = BuildDimensions();
            foreach (DimDef d in dims)
            {
                int filled = 0;
                foreach (PawnObject rec in records)
                {
                    if (d.Predicate(rec))
                    {
                        filled++;
                    }
                }
                report.Dimensions.Add(new DimensionResult
                {
                    Key = d.Key,
                    Label = d.Label,
                    Filled = filled,
                    Total = records.Count,
                });
            }

            // 全局统计：BattleObject 数 + ChronicleEvent 总量/按类型分布。
            if (component.Objects != null)
            {
                foreach (ArchiveObject o in component.Objects)
                {
                    if (o is BattleObject)
                    {
                        report.BattleObjectCount++;
                    }
                    else if (o is ThingObject)
                    {
                        report.ThingObjectCount++;
                    }
                    else if (o is LocationObject)
                    {
                        report.LocationObjectCount++;
                    }
                }
            }
            report.Dimensions.Add(ColonyDim("thingObj", "物品档案 ThingObject", report.ThingObjectCount > 0));
            report.Dimensions.Add(ColonyDim("locObj", "地点 LocationObject", report.LocationObjectCount > 0));
            bool hasAlias = component.BuildingAliases != null && component.BuildingAliases.Count > 0;
            report.Dimensions.Add(ColonyDim("alias", "工坊别名 Aliases", hasAlias));

            if (component.Events != null)
            {
                report.ChronicleEventCount = component.Events.Count;
                foreach (ChronicleEvent ev in component.Events)
                {
                    if (ev == null)
                    {
                        continue;
                    }
                    string t = ev.TypeKey ?? "Unknown";
                    if (report.EventByType.ContainsKey(t))
                    {
                        report.EventByType[t]++;
                    }
                    else
                    {
                        report.EventByType[t] = 1;
                    }
                }
            }
            return report;
        }

        // ---- 维度定义 -------------------------------------------------------

        private sealed class DimDef
        {
            public string Key;
            public string Label;
            public Func<PawnObject, bool> Predicate;
        }

        private static List<DimDef> BuildDimensions()
        {
            return new List<DimDef>
            {
                new DimDef
                {
                    Key = "work", Label = "工时 WorkTime",
                    Predicate = r => r.WorkTime != null && r.WorkTime.TicksByWorkType != null && r.WorkTime.TicksByWorkType.Count > 0,
                },
                new DimDef
                {
                    Key = "prod", Label = "产出 Production",
                    Predicate = r => r.Production != null && r.Production.QuantityByDef != null && r.Production.QuantityByDef.Count > 0,
                },
                new DimDef
                {
                    Key = "cons", Label = "损耗 Consumption",
                    Predicate = r => r.Consumption != null && r.Consumption.TotalSilver > 0f,
                },
                new DimDef
                {
                    Key = "kill", Label = "击杀 Kills",
                    Predicate = r => (r.MeleeKills + r.RangedKills) > 0 || r.DamageDealtTotal > 0f,
                },
                new DimDef
                {
                    Key = "battle", Label = "战役 Participated",
                    Predicate = r => r.ParticipatedBattles > 0,
                },
                new DimDef
                {
                    Key = "social", Label = "社交关系 Relations",
                    Predicate = r => r.Relations != null && r.Relations.Count > 0,
                },
                new DimDef
                {
                    Key = "place", Label = "足迹 PlaceHistory",
                    Predicate = r => r.PlaceHistory != null && r.PlaceHistory.Count > 0,
                },
                new DimDef
                {
                    Key = "wp", Label = "工坊 Workplace",
                    Predicate = r => r.Workplace != null,
                },
                new DimDef
                {
                    Key = "res", Label = "住所 Residence",
                    Predicate = r => r.Residence != null,
                },
                new DimDef
                {
                    Key = "medal", Label = "勋章 GrantedMedals",
                    Predicate = r => r.GrantedMedals != null && r.GrantedMedals.Count > 0,
                },
                new DimDef
                {
                    Key = "careerEvt", Label = "职业事实 CareerEvents",
                    Predicate = r => r.CareerData != null && r.CareerData.Events != null && r.CareerData.Events.Count > 0,
                },
                new DimDef
                {
                    Key = "prof", Label = "专业等级 Professional",
                    Predicate = r => r.CareerData != null && r.CareerData.Professional != null
                        && r.CareerData.Professional.skills != null && r.CareerData.Professional.skills.Count > 0,
                },
                new DimDef
                {
                    Key = "title", Label = "职称 GrantedTitles",
                    Predicate = r => r.CareerData != null && r.CareerData.GrantedTitles != null && r.CareerData.GrantedTitles.Count > 0,
                },
                new DimDef
                {
                    Key = "qual", Label = "资格 Qualification",
                    Predicate = r => r.CareerData != null && r.CareerData.Qualification != null,
                },
                new DimDef
                {
                    Key = "book", Label = "书籍 Books",
                    Predicate = r => r.CareerData != null && r.CareerData.Books != null && r.CareerData.Books.Count > 0,
                },
                new DimDef
                {
                    Key = "exam", Label = "考试/论文/答辩 Exams",
                    Predicate = r => r.CareerData != null && r.CareerData.Exams != null
                        && (((r.CareerData.Exams.Practical != null && r.CareerData.Exams.Practical.Count > 0)
                            || (r.CareerData.Exams.Theory != null && r.CareerData.Exams.Theory.Count > 0))
                            || (r.CareerData.Thesis != null
                                && ((r.CareerData.Thesis.Theses != null && r.CareerData.Thesis.Theses.Count > 0)
                                    || (r.CareerData.Thesis.Defenses != null && r.CareerData.Thesis.Defenses.Count > 0)))),
                },
                new DimDef
                {
                    Key = "profAll", Label = "专业技能全量 ProfSkillsAll",
                    Predicate = r =>
                    {
                        int need = DefDatabase<ProfessionalSkillDef>.AllDefsListForReading.Count;
                        return r.CareerData != null && r.CareerData.Professional != null
                            && r.CareerData.Professional.skills != null
                            && r.CareerData.Professional.skills.Count >= need
                            && AllSkillsLeveled(r);
                    },
                },
                new DimDef
                {
                    Key = "direction", Label = "主方向 PrimaryDirection",
                    Predicate = r =>
                    {
                        string pd = r.CareerData != null && r.CareerData.Professional != null
                            ? r.CareerData.Professional.primaryDirection : null;
                        return !string.IsNullOrEmpty(pd)
                            && DefDatabase<ProfessionalDirectionDef>.GetNamedSilentFail(pd) != null;
                    },
                },
                new DimDef
                {
                    Key = "honor", Label = "显示称号 DisplayedHonor",
                    Predicate = r => r.CareerData != null && !string.IsNullOrEmpty(r.CareerData.DisplayedHonorKey)
                        && !HonorDisplayKeys.IsNone(r.CareerData.DisplayedHonorKey),
                },
                new DimDef
                {
                    Key = "skillSnap", Label = "技能快照 SkillSnapshot",
                    Predicate = r => r.SkillSnapshot != null && r.SkillSnapshot.Count > 0,
                },
                new DimDef
                {
                    Key = "story", Label = "背景故事 Backstory",
                    Predicate = r => !string.IsNullOrEmpty(r.ChildhoodBackstoryDefName)
                        || !string.IsNullOrEmpty(r.AdulthoodBackstoryDefName),
                },
                new DimDef
                {
                    Key = "wpHist", Label = "工坊历史 WorkplaceHistory",
                    Predicate = r => r.WorkplaceHistory != null && r.WorkplaceHistory.Count > 0,
                },
                new DimDef
                {
                    Key = "consCat", Label = "损耗类目 ConsumptionCats",
                    Predicate = r => r.Consumption != null && r.Consumption.SilverByCategory != null
                        && r.Consumption.SilverByCategory.Count > 0,
                },
            };
        }

        private static bool AllSkillsLeveled(PawnObject r)
        {
            if (r.CareerData == null || r.CareerData.Professional == null
                || r.CareerData.Professional.skills == null)
            {
                return false;
            }
            List<ProfessionalSkillData> skills = r.CareerData.Professional.skills;
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i] == null || skills[i].level < 1)
                {
                    return false;
                }
            }
            return skills.Count > 0;
        }

        // ---- 日志 -----------------------------------------------------------

        private static DimensionResult ColonyDim(string key, string label, bool filled)
        {
            return new DimensionResult
            {
                Key = key,
                Label = label,
                Filled = filled ? 1 : 0,
                Total = 1,
            };
        }

        private static void LogProbe(CoverageReport report)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("== Dev Coverage Probe ==");
            sb.AppendLine("humanlike=" + report.HumanlikeTotal + " archived=" + report.ArchivedTotal
                + " battles=" + report.BattleObjectCount
                + " things=" + report.ThingObjectCount
                + " places=" + report.LocationObjectCount
                + " events=" + report.ChronicleEventCount);
            foreach (DimensionResult d in report.Dimensions)
            {
                sb.AppendLine(string.Format("{0,-20} {1}/{2} ({3:P0}) {4}", d.Label, d.Filled, d.Total, d.Coverage, d.Status));
            }
            if (report.EventByType.Count > 0)
            {
                sb.AppendLine("-- events by type --");
                foreach (KeyValuePair<string, int> kv in report.EventByType)
                {
                    sb.AppendLine(kv.Key + " : " + kv.Value);
                }
            }
            ChronicleLog.Info(ChronicleLog.Category.Ui, sb.ToString());
            Messages.Message(
                "PC 覆盖自检：已归档 " + report.ArchivedTotal + "/" + report.HumanlikeTotal
                    + "，维度 " + report.Dimensions.Count + " 项",
                MessageTypeDefOf.NeutralEvent, false);
        }
    }
}

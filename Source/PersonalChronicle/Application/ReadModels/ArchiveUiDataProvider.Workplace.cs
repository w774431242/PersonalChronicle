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
    /// Partial of <see cref="ArchiveUiDataProvider"/> — 工位/居所 Read Model 构建。
    /// </summary>
    /// <remarks>
    /// · 职责：BuildWorkplaceView（工位）、MakeChoice（工位切换候选项）、BuildResidenceView（居所）。
    /// · 调用方：ArchiveUiDataProvider（详情快照装配链路）。
    /// · 禁止：本文件只做投影；工位归属与切换合法性由 Domain 层裁定，此处不判定。
    /// · 拆分：自 Detail.cs 物理拆分（机械切片，方法体原样）；private 成员跨 partial 文件可见，编译安全。
    /// </remarks>
    public sealed partial class ArchiveUiDataProvider : IArchiveUiDataProvider
    {
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
                // v1.2.0 修复：接入 per-pawn 房间级自定义名（GetRoomName），与 BuildResidenceView
                // 及 BuildWorkplaceChoices 一致；否则 SetRoomName 改的名无处回读。
                // 优先级：GetRoomName(pawnId:role) > GetRoomRoleAlias(role) > LabelCap
                string roomName = (service != null)
                    ? service.GetRoomName(pawn.StableId, roomRoleDefName) : null;
                RoomRoleDef roomRole = DefDatabase<RoomRoleDef>.GetNamedSilentFail(roomRoleDefName);
                string roleDefault = (roomRole != null) ? roomRole.LabelCap : roomRoleDefName;
                string roleAlias = (service != null) ? service.GetRoomRoleAlias(roomRoleDefName) : null;
                string fallback = string.IsNullOrWhiteSpace(roleAlias) ? roleDefault : roleAlias;
                view.RoomRoleLabel = string.IsNullOrWhiteSpace(roomName) ? fallback : roomName;
            }
            return view;
        }

        /// <summary>
        /// v1.1.6 工坊卡弹窗：「全部工作 room」候选列表。合并两类来源：
        ///   1) 历史优先：PawnObject.WorkplaceHistory（该殖民者曾使用过的每台制作台）；
        ///   2) 当前地图补充：pawn 所在地图所有 Building_WorkTable 中不在历史里的（户外/未知房间也包含）。
        /// 展示名按「工坊别名 &gt; LabelCap」解析；房间名按「房间类型别名 &gt; RoomRoleDef.LabelCap」。
        /// 不排序（历史本身按捕获顺序，地图补充按枚举顺序）——窗口只渲染。
        /// </summary>
        internal static IReadOnlyList<WorkplaceChoiceView> BuildWorkplaceChoices(
            IArchiveService service, PawnObject pawn, Pawn livePawn)
        {
            List<WorkplaceChoiceView> result = new List<WorkplaceChoiceView>();
            if (pawn == null) return result;

            string currentStableId = (pawn.Workplace != null) ? pawn.Workplace.BuildingStableId : null;
            string pawnId = pawn.StableId;

            // 1) 历史优先：按 RoomRoleDefName 分组聚合 Top3 设备
            // v1.2.0 改造：历史条目先按房间分组 → 每组内按 UseCount 降序取 Top3 设备。
            System.Collections.Generic.Dictionary<string, List<WorkplaceHistoryEntry>> roomGroups =
                new System.Collections.Generic.Dictionary<string, List<WorkplaceHistoryEntry>>();
            if (pawn.WorkplaceHistory != null)
            {
                for (int i = 0; i < pawn.WorkplaceHistory.Count; i++)
                {
                    WorkplaceHistoryEntry e = pawn.WorkplaceHistory[i];
                    if (e == null || string.IsNullOrEmpty(e.BuildingStableId)) continue;
                    string roomKey = e.RoomRoleDefName ?? "";
                    if (!roomGroups.TryGetValue(roomKey, out var list))
                    {
                        list = new List<WorkplaceHistoryEntry>();
                        roomGroups[roomKey] = list;
                    }
                    list.Add(e);
                }
            }

            foreach (var kvp in roomGroups)
            {
                var entries = kvp.Value;
                // 按使用次数降序
                entries.Sort((a, b) => b.UseCount.CompareTo(a.UseCount));
                int topN = Mathf.Min(3, entries.Count);
                DeviceUseItem[] topDevices = new DeviceUseItem[topN];
                for (int t = 0; t < topN; t++)
                {
                    WorkplaceHistoryEntry e = entries[t];
                    ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(e.BuildingDefName);
                    string label = (def != null) ? def.LabelCap : e.BuildingDefName;
                    topDevices[t] = new DeviceUseItem { Label = label, Count = e.UseCount };
                }
                // 取该房间使用次数最多的设备作为代表行（第一条）
                WorkplaceHistoryEntry rep = entries[0];
                result.Add(MakeChoice(service, pawnId, rep.BuildingStableId, rep.BuildingDefName,
                    rep.RoomRoleDefName, rep.MapIndex, rep.Cell, rep.UseCount,
                    currentStableId, topDevices));
            }

            // 2) 当前地图其他制作台补充（去重历史已有的 stableId + 同房间去重）。
            // 走 listerThings.AllThings 枚举（与 ArchiveService.Battle.cs 同款已验证 API），
            // 过滤 Building_WorkTable，避免依赖 listerBuildings 的内部成员名。
            //
            // 去重两层：
            //   a) BuildingStableId 级：与历史条目不重复（同一台设备不出两条）；
            //   b) RoomRoleDefName 级：同房间的多台设备合并为一条（与历史分组逻辑对齐），
            //      否则用户给房间改名后，同房间 N 台设备都显示同一自定义名 → 视觉上重复。
            if (livePawn != null && livePawn.Map != null && livePawn.Map.listerThings != null)
            {
                List<Thing> all = livePawn.Map.listerThings.AllThings;
                if (all != null)
                {
                    // 先收集所有地图制作台，按 RoomRoleDefName 分组
                    System.Collections.Generic.Dictionary<string, List<Building_WorkTable>> mapRoomGroups =
                        new System.Collections.Generic.Dictionary<string, List<Building_WorkTable>>();
                    for (int i = 0; i < all.Count; i++)
                    {
                        Building_WorkTable bench = all[i] as Building_WorkTable;
                        if (bench == null || bench.def == null) continue;
                        string stableId = bench.def.defName + ":" + bench.thingIDNumber;

                        // 层 a: 与历史已有 stableId 去重
                        bool dup = false;
                        for (int j = 0; j < result.Count; j++)
                        {
                            if (string.Equals(result[j].BuildingStableId, stableId, System.StringComparison.Ordinal))
                            {
                                dup = true;
                                break;
                            }
                        }
                        if (dup) continue;

                        string roomRoleDefName = null;
                        Room room = Verse.RegionAndRoomQuery.GetRoom(bench, Verse.RegionType.Set_All);
                        if (room != null && room.Role != null) roomRoleDefName = room.Role.defName;
                        string roomKey = roomRoleDefName ?? "";

                        if (!mapRoomGroups.TryGetValue(roomKey, out var benchList))
                        {
                            benchList = new List<Building_WorkTable>();
                            mapRoomGroups[roomKey] = benchList;
                        }
                        benchList.Add(bench);
                    }

                    // 每个房间组只出一条（取第一台设备作为代表）
                    foreach (var grp in mapRoomGroups)
                    {
                        Building_WorkTable rep = grp.Value[0];
                        string stableId = rep.def.defName + ":" + rep.thingIDNumber;
                        string roomRoleDefName = grp.Key;
                        if (string.IsNullOrEmpty(roomRoleDefName)) roomRoleDefName = null;
                        result.Add(MakeChoice(service, pawnId, stableId, rep.def.defName, roomRoleDefName,
                            livePawn.Map.Index, rep.Position, 0, currentStableId, null));
                    }
                }
            }
            return result;
        }

        private static WorkplaceChoiceView MakeChoice(IArchiveService service, string pawnId, string stableId,
            string defName, string roomRoleDefName, int mapIndex, IntVec3 cell, int useCount,
            string currentStableId, DeviceUseItem[] topDevices = null)
        {
            WorkplaceChoiceView v = new WorkplaceChoiceView
            {
                BuildingStableId = stableId,
                BuildingDefName = defName,
                RoomRoleDefName = roomRoleDefName,
                MapIndex = mapIndex,
                Cell = cell,
                UseCount = useCount,
                IsCurrent = !string.IsNullOrEmpty(currentStableId)
                    && string.Equals(currentStableId, stableId, System.StringComparison.Ordinal),
                TopDevices = topDevices,
            };
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            string defaultLabel = (def != null) ? def.LabelCap : defName;
            string alias = (!string.IsNullOrEmpty(stableId) && service != null)
                ? service.GetBuildingAlias(stableId) : null;
            v.BuildingLabel = string.IsNullOrWhiteSpace(alias) ? defaultLabel : alias;
            if (!string.IsNullOrEmpty(roomRoleDefName))
            {
                // v1.2.0 修复：房间显示名解析必须接入 per-pawn 房间级自定义名（GetRoomName），
                // 否则 OpenRename 经 SetRoomName 写入的房间名无处回读、改名"看似失效"。
                // 展示优先级（与 BuildResidenceView 一致）：
                //   GetRoomName(pawnId:role) > GetRoomRoleAlias(role) > RoomRoleDef.LabelCap
                string roomName = (service != null)
                    ? service.GetRoomName(pawnId, roomRoleDefName) : null;
                RoomRoleDef roomRole = DefDatabase<RoomRoleDef>.GetNamedSilentFail(roomRoleDefName);
                string roleDefault = (roomRole != null) ? roomRole.LabelCap : roomRoleDefName;
                string roleAlias = (service != null) ? service.GetRoomRoleAlias(roomRoleDefName) : null;
                string fallback = string.IsNullOrWhiteSpace(roleAlias) ? roleDefault : roleAlias;
                v.RoomRoleLabel = string.IsNullOrWhiteSpace(roomName) ? fallback : roomName;
            }
            return v;
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
    }
}
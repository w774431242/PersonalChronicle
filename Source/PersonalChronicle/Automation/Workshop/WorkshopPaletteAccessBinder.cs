using System;
using System.Collections.Generic;
using System.Diagnostics;
using PersonalChronicle.Application;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Application.Workshop;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 「车间托盘」收集实现 + 启动注入（与 <c>WorkshopBonusAccessBinder</c> 同范式）。
    ///
    /// <para>本类把原 Manufacturing/UI 直读的 Automation/Domain 内部实现收敛到 Automation 层：
    /// 2 秒节流扫描、设备读数、车间成员首字、当班岗位、技能评分、生产配方全部在此构建，
    /// 经 <see cref="WorkshopPaletteAccess"/> 下发 UI。UI 不再持有任何 Automation/Domain 引用。</para>
    ///
    /// <para>静态缓存自管生命周期（<see cref="ClearCaches"/>），由 UI 的会话守卫
    /// <c>WorkshopLayoutCanvas.ClearSession</c> 经契约 <c>ClearCache</c> 触发。</para>
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class WorkshopPaletteAccessBinder
    {
        // ---- 2 秒节流扫描缓存（Automation 侧持有；UI 不再持静态态，ARC-011 缓解）----
        // ⚠ 2026-10-02 审计（P2-3）：键由单一 zone.ID 改为 **(map, zone) 复合键** ——
        // ZoneManager 的 ID 按地图各自计数，同会话两张地图出现相同 zone.ID 时，
        // 2 秒节流窗口内设备数 / 成员首字会互相串味。
        private static long paletteScanTicks;
        private static readonly Dictionary<long, int> DeviceCountCache = new Dictionary<long, int>();
        private static readonly Dictionary<long, List<DeviceEntry>> DeviceListCache =
            new Dictionary<long, List<DeviceEntry>>();
        private static readonly Dictionary<long, List<string>> MemberCache =
            new Dictionary<long, List<string>>();

        /// <summary>缓存复合键：把 map 与 zone 两级 ID 合成一个 long（无字符串拼接、无分配）。</summary>
        private static long CacheKey(Map map, Zone_Workshop zone)
        {
            int mapId = map != null ? map.uniqueID : 0;
            int zoneId = zone != null ? zone.ID : 0;
            return ((long)mapId << 32) | (uint)zoneId;
        }
        private const float DeviceScanIntervalSeconds = 2f;
        private const int MaxDutyNames = 3;
        private const int MaxWorkerDots = 4;

        /// <summary>
        /// **单调**时钟（<see cref="Stopwatch.GetTimestamp"/>，单位 tick）。
        ///
        /// <para>⚠ 2026-10-02 审计 P1-6：节流窗口原用 <c>DateTime.UtcNow.Ticks</c> 做差值
        /// ⇒ 系统时钟回拨 / 夏令时切换会让 <c>now - paletteScanTicks &lt; 0</c> 恒成立
        /// ⇒ <c>rescan</c> 永远为 false ⇒ 设备库与画布设备**永久停更**，且没有任何报错。</para>
        /// </summary>
        private static long MonotonicTicks()
        {
            return Stopwatch.GetTimestamp();
        }

        /// <summary>把 <see cref="Stopwatch"/> 的频率换算成「秒 → tick」的乘数（一次性）。</summary>
        private static readonly long TicksPerSecondCache = Stopwatch.Frequency;

        static WorkshopPaletteAccessBinder()
        {
            WorkshopPaletteAccess.SetSource(CollectPaletteImpl, ClearCaches);
        }

        private static void ClearCaches()
        {
            paletteScanTicks = 0L;
            DeviceCountCache.Clear();
            DeviceListCache.Clear();
            MemberCache.Clear();
        }

        private static void CollectPaletteImpl(Map map, List<PaletteEntry> into)
        {
            into.Clear();
            if (map == null)
            {
                ClearCaches();
                return;
            }
            IEnumerable<Zone_Workshop> zones = WorkshopZoneUtility.AllWorkshops(map);
            if (zones == null)
            {
                return;
            }
            long now = MonotonicTicks();
            bool rescan = now - paletteScanTicks >
                (long)(TicksPerSecondCache * DeviceScanIntervalSeconds);
            Dictionary<Thing, Pawn> activeWorkers = null;
            if (rescan)
            {
                paletteScanTicks = now;
                DeviceCountCache.Clear();
                DeviceListCache.Clear();
                MemberCache.Clear();
                activeWorkers = WorkshopDeviceReader.CollectActiveWorkers(map);
            }
            foreach (Zone_Workshop zone in zones)
            {
                if (zone == null)
                {
                    continue;
                }
                string label = string.IsNullOrEmpty(zone.label) ? zone.GetUniqueLoadID() : zone.label;
                long key = CacheKey(map, zone);
                if (rescan)
                {
                    List<Thing> devices = WorkshopStats.ScanDevices(zone);
                    DeviceCountCache[key] = devices.Count;
                    DeviceListCache[key] = BuildDeviceEntries(devices, zone, label, activeWorkers, map);
                    MemberCache[key] = BuildMemberInitials(zone);
                }
                else if (!DeviceListCache.ContainsKey(key))
                {
                    // ⚠ 2026-10-02 审计 P1-6b：窗口内新增的车间会 cache miss，
                    //   原实现留 null ⇒ `Devices == null` ⇒ 该车间在画布被整体跳过、设备库不列，
                    //   玩家看到"车间凭空消失"。改为**就地重扫该车间**（只补一个，不清全表）。
                    List<Thing> devices = WorkshopStats.ScanDevices(zone);
                    DeviceCountCache[key] = devices.Count;
                    DeviceListCache[key] = BuildDeviceEntries(devices, zone, label, null, map);
                    MemberCache[key] = BuildMemberInitials(zone);
                }
                DeviceListCache.TryGetValue(key, out List<DeviceEntry> deviceEntries);
                MemberCache.TryGetValue(key, out List<string> initials);
                DeviceCountCache.TryGetValue(key, out int count);
                into.Add(new PaletteEntry
                {
                    Label = label,
                    ZoneId = zone.ID,
                    DeviceCount = count,
                    Devices = deviceEntries,
                    MemberInitials = initials,
                    Stage = zone.WorkshopType != null ? zone.WorkshopType.processStage : string.Empty
                });
            }
        }

        private static List<DeviceEntry> BuildDeviceEntries(List<Thing> devices, Zone_Workshop zone,
            string zoneLabel, Dictionary<Thing, Pawn> activeWorkers, Map map)
        {
            var list = new List<DeviceEntry>(devices.Count);
            string dutyNames = BuildDutyNames(zone, map);
            // CHG-20261005-DEVRENAME：俯视卡 / 画布卡的设备名与设备页、原版面板**同口径**
            // （车间名 ?? 档案覆盖 ?? 默认名）—— 否则同一台设备在俯视卡上仍显示旧名（同屏两名）。
            // 管理器在循环外取一次（不在逐设备的循环里查组件）。
            MapComponent_WorkshopManager nameManager = map == null
                ? null
                : map.GetComponent<MapComponent_WorkshopManager>();
            for (int i = 0; i < devices.Count; i++)
            {
                Thing thing = devices[i];
                if (thing == null)
                {
                    continue;
                }
                Pawn worker = null;
                if (activeWorkers != null)
                {
                    activeWorkers.TryGetValue(thing, out worker);
                }
                WorkshopDeviceReading reading = WorkshopDeviceReader.Read(thing, worker);
                string label = nameManager != null
                    ? nameManager.EffectiveNameOf(thing)
                    : WorkshopStats.DeviceLabelOf(thing);
                string stateText = Translate(EquipmentStateText.TranslationKeyOf(reading.StateKey));
                string billText = reading.BillCount > 0
                    ? Translate1("PersonalChronicle.UI.Floor.BillCount", reading.BillCount)
                    : Translate("PersonalChronicle.UI.Floor.NoBill");
                string subLine = string.IsNullOrEmpty(reading.PowerText)
                    ? billText
                    : billText + " · " + reading.PowerText;
                string crewText = BuildCrewText(dutyNames, reading);
                string skillText = BuildSkillText(zone, reading, worker);
                string recipeText = BuildRecipeText(reading);
                ProductionDeviceCardView card = ProductionDeviceCards.Build(thing);
                list.Add(new DeviceEntry
                {
                    StableId = thing.def.defName + ":" + thing.thingIDNumber.ToString(),
                    Label = label,
                    Card = card,
                    StateKey = reading.StateKey,
                    StateText = stateText,
                    SubLine = subLine,
                    OperatorText = reading.OperatorText,
                    Progress01 = reading.Progress01,
                    CrewText = crewText,
                    SkillText = skillText,
                    RecipeText = recipeText,
                    ZoneLabel = zoneLabel,
                    Tooltip = label + (card == null ? string.Empty : "\n" + card.Tooltip) + "\n" + stateText
                        + (string.IsNullOrEmpty(reading.PowerText) ? string.Empty : " · " + reading.PowerText)
                        + " · " + billText
                        + (string.IsNullOrEmpty(reading.OperatorText) ? string.Empty : "\n" + reading.OperatorText)
                        + (string.IsNullOrEmpty(crewText) ? string.Empty : "\n" + crewText)
                        + "\n" + skillText
                        + (string.IsNullOrEmpty(recipeText) ? string.Empty : "\n" + recipeText)
                        + (reading.Progress01 >= 0f ? "\n" + Translate("PersonalChronicle.UI.Floor.ProgressNote") : string.Empty)
                });
            }
            return list;
        }

        private static string BuildDutyNames(Zone_Workshop zone, Map map)
        {
            if (zone == null || map == null || map.mapPawns == null)
            {
                return string.Empty;
            }
            MapComponent_WorkshopManager manager = MapComponent_WorkshopManager.Get(map);
            if (manager == null)
            {
                return string.Empty;
            }
            IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
            if (spawned == null)
            {
                return string.Empty;
            }
            var names = new List<string>(MaxDutyNames);
            for (int i = 0; i < spawned.Count && names.Count < MaxDutyNames; i++)
            {
                Pawn pawn = spawned[i];
                if (pawn == null || pawn.Dead)
                {
                    continue;
                }
                if (manager.DutyZoneFor(pawn) == zone)
                {
                    names.Add(pawn.LabelShort);
                }
            }
            return names.Count == 0 ? string.Empty : string.Join("/", names.ToArray());
        }

        private static string BuildCrewText(string dutyNames, WorkshopDeviceReading reading)
        {
            bool hasDuty = !string.IsNullOrEmpty(dutyNames);
            bool hasAssigned = !string.IsNullOrEmpty(reading.AssignedText);
            if (hasAssigned)
            {
                string assigned = Translate("PersonalChronicle.UI.Layout.Card.Assigned") + " " + reading.AssignedText;
                return hasDuty
                    ? assigned + " · " + Translate("PersonalChronicle.UI.Layout.Card.Post") + " " + dutyNames
                    : assigned;
            }
            return hasDuty ? Translate("PersonalChronicle.UI.Layout.Card.Post") + " " + dutyNames : string.Empty;
        }

        private static string BuildSkillText(Zone_Workshop zone, WorkshopDeviceReading reading, Pawn worker)
        {
            SkillDef skill = reading.WorkSkill;
            if (skill == null)
            {
                skill = WorkshopDutyScoring.SkillOf(zone, null);
            }
            if (skill == null)
            {
                return "—";
            }
            int level = WorkshopDutyScoring.ScoreOf(zone, skill, worker);
            string name = skill.LabelCap.ToString();
            return level >= 0 ? name + " " + level.ToString() : name + " —";
        }

        private static string BuildRecipeText(WorkshopDeviceReading reading)
        {
            if (string.IsNullOrEmpty(reading.RecipeLabel))
            {
                return "—";
            }
            return reading.RecipeTarget > 0
                ? reading.RecipeLabel + " ×" + reading.RecipeTarget.ToString()
                : reading.RecipeLabel;
        }

        private static List<string> BuildMemberInitials(Zone_Workshop zone)
        {
            var list = new List<string>(MaxWorkerDots);
            List<Pawn> members = zone.Members;
            if (members == null)
            {
                return list;
            }
            for (int i = 0; i < members.Count && list.Count < MaxWorkerDots; i++)
            {
                Pawn pawn = members[i];
                if (pawn == null)
                {
                    continue;
                }
                string name = pawn.LabelShort;
                if (!string.IsNullOrEmpty(name) && name.Length > 0)
                {
                    list.Add(name.Substring(0, 1));
                }
            }
            return list;
        }

        private static string Translate(string key)
        {
            return key.Translate().ToString();
        }

        private static string Translate1(string key, int arg)
        {
            return key.Translate((NamedArgument)arg).ToString();
        }
    }
}

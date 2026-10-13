using System.Collections.Generic;
using PersonalChronicle.Domain;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间 Read Model 快照。
    ///
    /// 打开管理面板时由 <see cref="For"/> 一次性算好，窗口只负责渲染、不做任何
    /// 计算或 null 判定（遵循本项目 UI 规范：窗口只消费 Read Model 快照）。
    /// </summary>
    public sealed class WorkshopStats
    {
        public string Label { get; private set; }

        /// <summary>车间总格数。</summary>
        public int CellCount { get; private set; }

        /// <summary>可站立的格子数（近似「还能放设备的空地」）。</summary>
        public int FreeCells { get; private set; }

        /// <summary>被设备/墙体等占住、不可站立的格子数（CellCount − FreeCells）。</summary>
        public int OccupiedCells { get; private set; }

        /// <summary>
        /// 区内设备数。判定口径 = <see cref="IsWorkshopDevice"/>（自家/第三方
        /// <see cref="IWorkshopDevice"/> **∪ 原版及第三方工作台 `IBillGiver`**）。
        /// </summary>
        public int DeviceCount { get; private set; }

        /// <summary>
        /// 车间类型是否声明了「期望设备」（<see cref="WorkshopTypeDef.expectedDeviceDefNames"/>）。
        /// false ⇒ <see cref="ExtraDevices"/> 即全部设备，且**不得**显示"缺失"
        /// （调试台/质检台/维修机等尚未落地，属诚实口径而非缺口）。
        /// </summary>
        public bool HasExpectedDevices { get; private set; }

        /// <summary>与车间类型期望设备 defName 相符的台数。</summary>
        public int MatchedDevices { get; private set; }

        /// <summary>区内不属于本工序期望设备的台数（"多余"）。</summary>
        public int ExtraDevices { get; private set; }

        /// <summary>本工序期望但区内缺失的设备显示名（非空列表仅在 <see cref="HasExpectedDevices"/> 为 true 时有意义）。</summary>
        public List<string> MissingDeviceLabels { get; private set; } = new List<string>();

        /// <summary>
        /// 快照构造入口。zone 为 null 时返回 null（调用方负责不开窗）。
        ///
        /// <paramref name="scannedDevices"/>：调用方已扫好的设备清单（单一真源）。
        /// 由调用方传入可避免「For 内部扫一遍、Refresh 再扫一遍」的重复全格遍历
        /// ——车间可能上千格，每次开窗省一整趟。
        /// </summary>
        public static WorkshopStats For(Zone_Workshop zone, List<Thing> scannedDevices)
        {
            if (zone == null)
            {
                return null;
            }

            var stats = new WorkshopStats
            {
                Label = zone.RenamableLabel,
                CellCount = zone.CellCount
            };

            Map map = zone.Map;
            if (map == null)
            {
                return stats;
            }

            int free = 0;
            List<IntVec3> cells = zone.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].Standable(map))
                {
                    free++;
                }
            }

            stats.FreeCells = free;
            stats.OccupiedCells = stats.CellCount - free;
            List<Thing> deviceList = scannedDevices ?? ScanDevices(zone);
            stats.DeviceCount = deviceList.Count;
            ComputeDeviceFit(stats, zone, deviceList);
            return stats;
        }

        /// <summary>
        /// 扫描车间内全部设备。
        ///
        /// 这是设备清单的<b>单一真源</b>：<see cref="For"/> 的设备计数与管理面板的
        /// 设备列表都走这里，避免两处各写一遍遍历导致口径漂移。
        ///
        /// <para><b>识别口径（2026-09-11 二次修正）</b>：<c>IWorkshopDevice</c> **∪**
        /// <b>建筑型 <c>IBillGiver</c></b>（见 <see cref="IsBillGiverBench"/>）。只认前者会让
        /// **原版/第三方工作台全部漏掉**（它们不实现我们的接口）—— 实机表现为
        /// "车间内有 32 台机床，设备却显示 0"；而只查 <c>IBillGiver</c> 又会把**所有 pawn**
        /// 算成设备（<c>Verse.Pawn : IBillGiver</c>）—— 实机表现为设备页列出"人类"。</para>
        ///
        /// <para>多格建筑会在它占用的每个格子各出现一次，故按实例去重。</para>
        /// </summary>
        public static List<Thing> ScanDevices(Zone_Workshop zone)
        {
            var found = new List<Thing>();
            if (zone == null || zone.Map == null)
            {
                return found;
            }

            var seen = new HashSet<Thing>();
            List<IntVec3> cells = zone.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                List<Thing> things = cells[i].GetThingList(zone.Map);
                for (int j = 0; j < things.Count; j++)
                {
                    Thing thing = things[j];
                    if (IsWorkshopDevice(thing) && seen.Add(thing))
                    {
                        found.Add(thing);
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// 是否算作车间设备：自家/第三方<b>显式声明</b>的 <see cref="IWorkshopDevice"/>
        /// （传送带段、农机、未来的加工机），或<b>建筑型工作台</b>（见 <see cref="IsBillGiverBench"/>，
        /// 即 `Building_WorkTable` 系：缝纫台/锻造台/机加工台…）。
        /// </summary>
        public static bool IsWorkshopDevice(Thing thing)
        {
            if (thing == null)
            {
                return false;
            }
            // 判定规则在 Domain 纯函数里（可离线单测，含"pawn 不是设备"的回归用例）。
            return WorkshopDeviceRules.IsWorkshopDevice(
                thing is IWorkshopDevice, thing is Building, thing is IBillGiver);
        }

        /// <summary>
        /// 是否算「机台」（可读原版工单的设备）：<b>必须是建筑</b>且实现 <see cref="IBillGiver"/>。
        ///
        /// <para>⚠️ <b>建筑判定不可省</b>：原版 <c>Verse.Pawn : … IBillGiver</c>（其
        /// <c>BillStack</c> 实际是 <c>health.surgeryBills</c>）⇒ 只查接口会把**所有 pawn 都算成
        /// 设备/机台**：设备页列出"人类"（状态还显示为待命）、配方页会去读 pawn 的"手术工单"。
        /// 这是 2026-09-11 实机截图暴露的缺陷。</para>
        /// </summary>
        public static bool IsBillGiverBench(Thing thing)
        {
            return WorkshopDeviceRules.IsBillGiverBench(thing is Building, thing is IBillGiver);
        }

        /// <summary>
        /// 设备显示名（面板设备页/搜索的唯一取词入口，替代原先的
        /// <c>(IWorkshopDevice)thing</c> 硬转 —— 原版工作台不实现该接口，硬转会抛
        /// <c>InvalidCastException</c>）。缺省回落 <c>def.label</c>。
        /// </summary>
        public static string DeviceLabelOf(Thing thing)
        {
            if (thing == null)
            {
                return "?";
            }
            if (thing is IWorkshopDevice device && !string.IsNullOrEmpty(device.DeviceLabel))
            {
                return device.DeviceLabel;
            }
            return thing.def != null ? thing.def.label : "?";
        }

        /// <summary>
        /// 设备匹配度（匹配 / 多余 / 缺失）。缺失项的 label 由 defName 现查（原版
        /// <c>ThingDef.GetNamedSilentFail</c>；Def 不存在时退化为 defName 原文，不编造文本）。
        /// 车间未指定类型或该类型未声明期望设备时：全部设备计为"多余"，且不产生缺失项。
        /// </summary>
        private static void ComputeDeviceFit(WorkshopStats stats, Zone_Workshop zone, List<Thing> devices)
        {
            List<string> expected = zone.WorkshopType != null
                ? zone.WorkshopType.expectedDeviceDefNames
                : null;
            List<string> actual = null;
            if (devices != null && devices.Count > 0)
            {
                actual = new List<string>(devices.Count);
                for (int i = 0; i < devices.Count; i++)
                {
                    Thing device = devices[i];
                    actual.Add(device != null && device.def != null ? device.def.defName : null);
                }
            }

            // 判定委托 Domain 纯函数（可离线单测），本层只做 defName → label 的本地化映射。
            WorkshopDeviceFit fit = WorkshopDeviceFitEvaluator.Evaluate(expected, actual,
                WorkshopDeviceIdentity.CanonicalName,
                zone.WorkshopType != null ? zone.WorkshopType.acceptedDeviceDefNames : null);
            stats.HasExpectedDevices = fit.HasExpected;
            stats.MatchedDevices = fit.Matched;
            stats.ExtraDevices = fit.Extra;
            for (int i = 0; i < fit.MissingDefNames.Count; i++)
            {
                stats.MissingDeviceLabels.Add(LabelOfThingDef(fit.MissingDefNames[i]));
            }
        }

        /// <summary>defName → 显示 label（Def 缺失时回落 defName，绝不编造）。</summary>
        private static string LabelOfThingDef(string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            return def != null ? def.label : defName;
        }
    }
}

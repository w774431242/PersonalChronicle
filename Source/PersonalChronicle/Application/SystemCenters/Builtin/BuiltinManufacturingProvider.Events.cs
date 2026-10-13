using System;
using System.Collections.Generic;
using System.Globalization;
using PersonalChronicle.Application.ReadModels;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Application.SystemCenters.Builtin
{
    /// <summary>
    /// 制造中心 ⑤ 事件与设备健康（对应预览 §⑤）—— 数据构建部分。
    ///
    /// <para><b>事件流口径</b>：直接用本 mod 的**档案事件流**（<c>queries.GetRecentEvents</c>），
    /// 就是预览 <c>evt.*</c> 日志在游戏里的真实对应物 —— 不自造"制造专属日志"。
    /// 文本/字形/配色留给 UI 用档案馆既有的渲染助手（单一真源）。</para>
    ///
    /// <para><b>设备健康口径（如实）</b>：原版 <c>CompBreakdownable</c> **只存布尔故障位，
    /// 不记故障次数与时间戳**（已反编译核实：仅有 <c>brokenDownInt</c>），因此 <b>MTBF/MTTR 实测值无法给出</b>。
    /// 这里给出：① 当前状态（四态，来自状态派生）；② 原版**自带的设计基准**
    /// <c>BreakdownMTBTicks = 13680000 tick ≈ 228 天</c>；③ 健康条 = 当前状态的代理值，
    /// 并在副标写明"实测未接入"（LAND-009）。</para>
    /// </summary>
    public sealed partial class BuiltinManufacturingProvider : IManufacturingEventsDataProvider
    {
        /// <summary>事件流条数上限（预览日志窗口最多保留 40 条）。</summary>
        private const int MaxEventRows = 40;

        /// <summary>健康面板行数上限。</summary>
        private const int MaxHealthRows = 6;

        // GOV-006 硬编码登记：分类 = 原版常量镜像；来源 = 反编译 RimWorld.CompBreakdownable
        // 的 private const BreakdownMTBTicks = 13680000（1 日 = 60000 tick ⇒ 228 天）；
        // 生命周期 = 原版改基准时同步。仅用于"设计基准"展示，不参与玩法判定。
        private const int BreakdownMtbfDays = 228;

        public ManufacturingEventsSnapshot BuildEvents(Map map)
        {
            ManufacturingEventsSnapshot snapshot = new ManufacturingEventsSnapshot();
            try
            {
                AppendEvents(snapshot);
                if (map != null)
                {
                    AppendHealth(snapshot, map);
                }
            }
            catch (Exception ex)
            {
                ChronicleLog.Error(ChronicleLog.Category.Provider, "manufacturing events failed: " + ex);
                snapshot.IsEmpty = true;
            }
            return snapshot;
        }

        private void AppendEvents(ManufacturingEventsSnapshot snapshot)
        {
            snapshot.EventsSub = T1("PersonalChronicle.UI.Events.Sub", MaxEventRows);
            if (queries == null)
            {
                return;
            }
            IReadOnlyList<ChronicleEvent> events;
            try
            {
                events = queries.GetRecentEvents(MaxEventRows);
            }
            catch (Exception ex)
            {
                ChronicleLog.Warning(ChronicleLog.Category.Provider, "event query failed: " + ex.Message);
                return;
            }
            if (events == null)
            {
                return;
            }
            for (int i = 0; i < events.Count && snapshot.Events.Count < MaxEventRows; i++)
            {
                if (events[i] != null)
                {
                    snapshot.Events.Add(events[i]);
                }
            }
        }

        /// <summary>设备健康：每台可接工单设备一行（当前状态代理 + 原版 MTBF 基准 + 未接入说明）。</summary>
        private static void AppendHealth(ManufacturingEventsSnapshot snapshot, Map map)
        {
            snapshot.HealthSub = T("PersonalChronicle.UI.Events.Health.Sub");
            List<Building> buildings = map.listerBuildings != null
                ? map.listerBuildings.allBuildingsColonist
                : null;
            if (buildings == null)
            {
                return;
            }
            for (int i = 0; i < buildings.Count && snapshot.Health.Count < MaxHealthRows; i++)
            {
                Building building = buildings[i];
                if (!(building is IBillGiver))
                {
                    continue;
                }
                BillStack stack = ((IBillGiver)building).BillStack;
                CompBreakdownable breakdown = building.TryGetComp<CompBreakdownable>();
                CompPowerTrader power = building.TryGetComp<CompPowerTrader>();
                EquipmentState state = EquipmentStateResolver.Resolve(
                    breakdown != null && breakdown.BrokenDown,
                    power == null || power.PowerOn,
                    stack != null && stack.AnyShouldDoNow);

                snapshot.Health.Add(new HealthRowView
                {
                    Name = LabelOf(building.def),
                    StateText = T(EquipmentStateText.TranslationKeyOf(EquipmentStateResolver.KeyOf(state))),
                    Health01 = HealthProxy(state),
                    Tone = state == EquipmentState.Fault
                        ? SystemCenterTone.Bad
                        : (state == EquipmentState.Run ? SystemCenterTone.Good : SystemCenterTone.Warn),
                    Note = T1("PersonalChronicle.UI.Events.Health.Note", BreakdownMtbfDays)
                });
            }
        }

        /// <summary>健康条 = 当前状态的代理值（原版无累计故障数据，故非 MTBF 实测）。</summary>
        private static float HealthProxy(EquipmentState state)
        {
            switch (state)
            {
                case EquipmentState.Run: return 1f;
                case EquipmentState.Idle: return 0.6f;
                case EquipmentState.Off: return 0.25f;
                default: return 0f;
            }
        }

    }
}

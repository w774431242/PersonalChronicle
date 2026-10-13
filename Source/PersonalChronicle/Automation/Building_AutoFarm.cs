using System.Collections.Generic;
using PersonalChronicle.Domain;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 自动化农业设备（S/M/L 三档）。
    ///
    /// 薄壳建筑：电力/燃料/开关/发光全部由原版 comps 承载
    /// （PowerTrader / Refuelable / Flickable / Glower，见 Defs/Automation_AutoFarm.xml），
    /// 本类目前只做两件事：
    /// - 实现 <see cref="IWorkshopDevice"/>：放进车间区即自动纳入车间统计与管理面板
    ///   （识别契约：设备识别走接口不走 defName，零注册）。
    /// - 为后续 <c>CompAutoAgriculture</c>（施肥/播种/收获状态机，v2.1 文档 §6-§9）
    ///   预留 thingClass 归属——功能逐个点亮，先跑通组装链与车间挂接。
    ///
    /// <para><b>[StaticConstructorOnStartup]</b>（2026-10-14 清理）：本类持有
    /// <c>Texture2D terrainIcon</c> 静态字段，必须在主线程加载，否则 Player.log 刷
    /// "Type Building_AutoFarm probably needs a StaticConstructorOnStartup attribute"。</para>
    /// </summary>
    [StaticConstructorOnStartup]
    public class Building_AutoFarm : Building, IWorkshopDevice, IWorkshopDeviceState
    {
        public string DeviceLabel => def.label;

        /// <summary>
        /// 自报状态（2026-09-11 体检 P2）：逻辑原先只存在于管理面板农业页的私有函数里，
        /// 导致设备页对同一台农机恒显示"待命"。现上移到设备自身，设备页/农业页共用一份。
        /// </summary>
        public string StateKeyOrNull()
        {
            CompPowerTrader power = GetComp<CompPowerTrader>();
            if (power != null && !power.PowerOn)
            {
                return EquipmentStateResolver.KeyAgNoPower;
            }
            CompRefuelable fuel = GetComp<CompRefuelable>();
            if (fuel != null && !fuel.HasFuel)
            {
                return EquipmentStateResolver.KeyAgNoFuel;
            }
            CompFlickable flick = GetComp<CompFlickable>();
            if (flick != null && !flick.SwitchIsOn)
            {
                return EquipmentStateResolver.KeyAgOff;
            }
            return EquipmentStateResolver.KeyAgRunning;
        }

        // 性能体检 P0-2：GetGizmos 每帧调用，原本每次 new 两个 Command_Action
        // （持续 GC）。改为实例级缓存，对齐 Zone_Workshop.GetGizmos 的既有范例；
        // 只有标签/图标这类字符串在返回前刷新（赋值极廉）。
        private Command_Action terrainGizmo;
        private Command_Action configureGizmo;

        /// <summary>选中设备时绘制农业覆盖范围（原版 DrawFieldEdges 格子高亮，与成长区同观感）。</summary>
        public override void DrawExtraSelectionOverlays()
        {
            base.DrawExtraSelectionOverlays();
            CompAutoAgriculture comp = GetComp<CompAutoAgriculture>();
            if (comp != null && comp.CoverageCells.Count > 0)
            {
                GenDraw.DrawFieldEdges(comp.CoverageCells);
            }
            // 物品输出端口：高亮定向输出目标格（v0.2 §0.8 D3）。
            CompItemPort port = GetComp<CompItemPort>();
            if (port != null)
            {
                port.DrawTargetOverlay();
            }
        }

        /// <summary>
        /// 互动键（选中设备后出现的按钮）：「配置农场」打开种植区配置弹窗，
        /// 玩家在其中选作物 + 整片/画笔划种植区。其余 gizmo（开关/维护等）
        /// 由 Flickable / Breakdownable 等原版 comp 提供，先 yield 再补本机专属键。
        /// </summary>
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            // 地形互动键：选择改造目标地形（S/M/L 全档，兼容第三方地形 mod）。
            CompAutoAgriculture agri = GetComp<CompAutoAgriculture>();
            if (agri != null && agri.RenovationEnabled)
            {
                if (terrainGizmo == null)
                {
                    terrainGizmo = new Command_Action
                    {
                        defaultDesc = "PersonalChronicle.Automation.Farm.TerrainDesc".Translate().ToString(),
                        icon = TerrainIcon,
                        action = () => Find.WindowStack.Add(new Dialog_ChooseTerrain(agri))
                    };
                }
                // 目标地形可被玩家改选，标签每帧刷新（字符串赋值，非 GC 压力源头）。
                terrainGizmo.defaultLabel = "PersonalChronicle.Automation.Farm.Terrain"
                    .Translate(agri.TargetTerrainLabel).ToString();
                yield return terrainGizmo;
            }

            if (configureGizmo == null)
            {
                configureGizmo = new Command_Action
                {
                    defaultDesc = "PersonalChronicle.Automation.Farm.ConfigureDesc".Translate().ToString(),
                    icon = TexButton.OpenStatsReport,
                    action = () => Find.WindowStack.Add(new Dialog_AutoFarmSetup(this))
                };
            }
            configureGizmo.defaultLabel = "PersonalChronicle.Automation.Farm.Configure".Translate().ToString();
            yield return configureGizmo;
        }

        /// <summary>地形互动键图标（原版刷子），静态缓存避免每帧查找。</summary>
        private static Texture2D terrainIcon;

        private static Texture2D TerrainIcon
        {
            get
            {
                if (terrainIcon == null)
                {
                    terrainIcon = ContentFinder<Texture2D>.Get("UI/Designators/FloorPaint", false) ?? TexButton.OpenStatsReport;
                }
                return terrainIcon;
            }
        }

        // ⭐ 2026-09-08：检视/悬停信息不再写在建筑类上。
        // 第三方 UI mod 的悬停提示读的是 Thing.GetInspectString()，它会自动汇总
        // 各 comp 的 CompInspectStringExtra；原先这条供电引导写在建筑类里，
        // 悬停时（未选中）就被漏掉。现已连同状态行一起下移到
        // CompAutoAgriculture.CompInspectStringExtra，避免两处重复输出。
    }
}

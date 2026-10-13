using System.Collections.Generic;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 「车间托盘」绘制模型（自由编排画布 ① 内容）。
    /// <para>由 <see cref="PersonalChronicle.Application.Workshop.WorkshopPaletteAccess"/> 在 Automation 侧构建，
    /// 经 Application 层快照契约下发给 Manufacturing/UI 消费；绘制路径零翻译、零拼接、零分配（PERF-002）。</para>
    /// </summary>
    internal struct PaletteEntry
    {
        /// <summary>车间标签（zone.label；无则回退唯一 LoadID）。</summary>
        internal string Label;

        /// <summary>车间区 ID（Zone.ID；稳定键）。</summary>
        internal int ZoneId;

        /// <summary>该车间内的真实设备数（WorkshopStats.ScanDevices 同源、同 2 秒节流）。</summary>
        internal int DeviceCount;

        /// <summary>车间内设备（画设备卡用；与 DeviceCount 同源）。</summary>
        internal List<DeviceEntry> Devices;

        /// <summary>车间成员首字（工人圆点；≤ MaxWorkerDots）。</summary>
        internal List<string> MemberInitials;

        /// <summary>工序阶段（WorkshopTypeDef.processStage；无类型车间为空串）。</summary>
        internal string Stage;
    }

    /// <summary>
    /// 一台真实设备的绘制面（自由画布内容）。
    /// <para>文本一律在构建期拼好（每 2 秒一次的节流扫描里），绘制路径零翻译、零拼接、零分配。</para>
    /// </summary>
    internal struct DeviceEntry
    {
        /// <summary>真实设备声明的等级、图像与当前速度；普通设备为 null。</summary>
        internal ProductionDeviceCardView Card;

        /// <summary>稳定 ID（&lt;defName&gt;:&lt;thingIDNumber&gt;）—— 点击时回指 FloorDeviceView 开详情。</summary>
        internal string StableId;

        /// <summary>设备名（WorkshopStats.DeviceLabelOf：IWorkshopDevice.DeviceLabel 优先，回落 def.label）。</summary>
        internal string Label;

        /// <summary>状态键（EquipmentStateResolver：run / idle / fault / off）—— UI 只做键→灯色映射。</summary>
        internal string StateKey;

        /// <summary>状态文案（已本地化；行 2，用灯色画）。</summary>
        internal string StateText;

        /// <summary>行 3 = 工单 · 功率（如 "3 单 · 480W"）。</summary>
        internal string SubLine;

        /// <summary>作业者名（正在做这台活的 pawn；空 = 不画行 4）。</summary>
        internal string OperatorText;

        /// <summary>进度 0..1；&lt; 0 = 不画进度条。</summary>
        internal float Progress01;

        /// <summary>人员（行 5）= 工单指定生产人员 + 当班岗位人员。</summary>
        internal string CrewText;

        /// <summary>技能（设备对应技能 + 使用者等级，如 "建造 12"）。</summary>
        internal string SkillText;

        /// <summary>生产（当前工单配方 + 目标数，如 "烹饪简单食物 ×5"）。</summary>
        internal string RecipeText;

        /// <summary>所属车间名（独立设备时用于右上角角标）。</summary>
        internal string ZoneLabel;

        /// <summary>悬停提示（构建期拼好；绘制路径零分配）。</summary>
        internal string Tooltip;
    }
}

using PersonalChronicle.MasterControl.Population;
using System.Collections.Generic;
using PersonalChronicle.Domain;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 制造中心 ⑤ 事件与设备健康快照（对应预览 §⑤：实时事件流 + 设备健康 / TPM）。
    ///
    /// <para><b>事件流只带原始事件</b>：文本/字形/配色由 UI 层用档案既有的
    /// <c>PersonnelArchivePanel.EventName/EventTypeToGlyph/EventTypeToColor</c> 渲染 ——
    /// 与档案馆时间线共用同一套事件文案，不另造一套（08c 契约：快照不自造表现层文案）。</para>
    /// </summary>
    public sealed class ManufacturingEventsSnapshot : ArchiveSectionSnapshot
    {
        /// <summary>最近事件（新→旧；来源 = 档案事件流 <c>GetRecentEvents</c>）。</summary>
        public List<ChronicleEvent> Events = new List<ChronicleEvent>();

        /// <summary>事件流副标（"事件源 · 最多 N 条"）。</summary>
        public string EventsSub = string.Empty;

        /// <summary>设备健康行（每台可接工单设备一行）。</summary>
        public List<HealthRowView> Health = new List<HealthRowView>();

        /// <summary>健康面板副标（口径说明）。</summary>
        public string HealthSub = string.Empty;
    }

    /// <summary>设备健康一行。</summary>
    public sealed class HealthRowView
    {
        public string Name = string.Empty;

        /// <summary>当前状态文案（运行 / 待命 / 故障 / 停机）。</summary>
        public string StateText = string.Empty;

        /// <summary>健康条占比 0..1 = 当前状态代理值。</summary>
        public float Health01;

        public SystemCenterTone Tone = SystemCenterTone.Flat;

        /// <summary>口径附注（"基准 MTBF 228 天 · 实测未接入"）。</summary>
        public string Note = string.Empty;
    }
}

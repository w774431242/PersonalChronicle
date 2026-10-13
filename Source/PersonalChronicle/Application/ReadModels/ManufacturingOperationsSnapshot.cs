using System.Collections.Generic;

namespace PersonalChronicle.Application.ReadModels
{
    /// <summary>
    /// 制造中心 ④ 运营快照（对应预览 §④：在岗殖民者 / 成品库 / Andon 异常聚合）。
    ///
    /// <para><b>面板数量与预览一致</b>：前两个是真实数据面板，第三个（Andon）在预览里本身就是
    /// <c>class="panel pending"</c> 的占位（"⏳ 待 DataBridge"）—— 这里同样照实画占位，
    /// 不假装有数据（LAND-009）。</para>
    /// </summary>
    public sealed class ManufacturingOperationsSnapshot : ArchiveSectionSnapshot
    {
        /// <summary>在岗殖民者（真实 <c>FreeColonistsSpawned</c>）。</summary>
        public List<OpsWorkerView> Workers = new List<OpsWorkerView>();

        /// <summary>成品库逐件清单（带品质的可搬运物品，前 N 件）。</summary>
        public List<OpsDepotItemView> DepotItems = new List<OpsDepotItemView>();

        /// <summary>成品库件数（"N 件"）。</summary>
        public string DepotCountText = string.Empty;

        /// <summary>成品库为空 → 画「暂无成品」。</summary>
        public bool DepotEmpty;
    }

    /// <summary>一个在岗殖民者。</summary>
    public sealed class OpsWorkerView
    {
        public string Name = string.Empty;

        /// <summary>智力等级文字（"智力 8"）。</summary>
        public string IntelText = string.Empty;

        /// <summary>心情 0..1；&lt; 0 = 无心情需求。</summary>
        public float Mood01 = -1f;

        /// <summary>心情文字（"72%"）与语气（&gt;70 好 / &gt;50 警示 / 否则差，与预览一致）。</summary>
        public string MoodText = string.Empty;
        public SystemCenterTone MoodTone = SystemCenterTone.Flat;

        /// <summary>等级最高的前三项技能（"手工 12"）。</summary>
        public List<string> TopSkills = new List<string>();

        /// <summary>
        /// 技能热力格：一个技能一格，<see cref="OpsSkillCell.Level"/> 决定颜色
        /// （≥12 主强调色 / ≥7 健康色 / 其余暗淡），与预览 HeatmapGrid 一致。
        /// </summary>
        public List<OpsSkillCell> Heat = new List<OpsSkillCell>();

        /// <summary>当前任务：正在操作的设备名；无则「待命」。</summary>
        public string TaskText = string.Empty;

        /// <summary>悬停提示（构建期拼好）。</summary>
        public string Tooltip = string.Empty;
    }

    /// <summary>技能热力一格。</summary>
    public sealed class OpsSkillCell
    {
        public string Label = string.Empty;
        public int Level;
    }

    /// <summary>成品库一件。</summary>
    public sealed class OpsDepotItemView
    {
        /// <summary>品质标签（原版 <c>QualityUtility.GetLabel</c>）。</summary>
        public string QualityLabel = string.Empty;

        public SystemCenterTone QualityTone = SystemCenterTone.Flat;

        public string Name = string.Empty;

        /// <summary>堆叠数（"×12"）。</summary>
        public string CountText = string.Empty;
    }
}

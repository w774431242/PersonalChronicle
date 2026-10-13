using PersonalChronicle.Domain;
using Verse;

namespace PersonalChronicle.Data
{
    /// <summary>
    /// <see cref="ChronicleGameComponent"/> 的<b>车间专属工位降档台账</b>分部
    /// （`CHG-20261003-WORKSTATION` / EXC-2026-015）。
    ///
    /// <para><b>归属（GATE-DATA 判据）</b>：台账是「PC 改过玩家原版工作档位」这一<b>运行时事实的
    /// 历史累积</b>，读档后<b>无法重算</b>（原值已丢失）⇒ 属 Save Data，必须落盘。
    /// 落法为<b>加性可选段</b>：旧档无 <c>workshopWorkDemotion</c> 键 ⇒ 读回 null ⇒ 归一为空台账
    /// ⇒ <b>不需要 bump SchemaVersion</b>、不需要 Migration（与 <c>ExposeItemFlow</c> /
    /// <c>ExposeStockSeries</c> 完全同规）。</para>
    ///
    /// <para><b>为什么不落 MapComponent</b>：台账是<b>殖民地级</b>事实（跨图共享同一批玩家 pawn），
    /// 而 <c>MapComponent_WorkshopManager</c> 是<b>地图级</b>。玩家换图（ caravan / 多基地）时
    /// 换的是 MapComponent，落那里会丢台账 ⇒ 一键解除就再也还不了原档位。</para>
    ///
    /// <para><b>写入口纪律（MATRIX-003）</b>：本分部只提供<b>台账存取</b>；
    /// 「决定写什么档位」与「调 <c>Pawn_WorkSettings.SetPriority</c>」都在
    /// <c>MapComponent_WorkshopManager</c>（Automation 门面，单一写入点）。
    /// UI 只读本分部的统计值，不直接改。</para>
    /// </summary>
    public sealed partial class ChronicleGameComponent
    {
        /// <summary>
        /// 降档台账（落盘）。<b>旧档读回可能为 null</b> —— 消费方必须判 null 或走
        /// <see cref="DemotionLedger"/> 的惰性归一。
        ///
        /// <para>⚠ <b>必须内联初始化</b>（同 <c>StockSeries</c> 的 GATE-SAVE P0-1 修复）：
        /// <c>ExposeData</c> 只在存/读档执行，<b>新档不进入</b>；而写入路径由玩家点击
        /// 「一键全解除」或排班事件触发，可能早于任何一次存/读档 ⇒ 无初始化器必然 NPE。</para>
        /// </summary>
        public WorkshopWorkDemotionLedger WorkDemotionLedger = new WorkshopWorkDemotionLedger();

        /// <summary>由主 <see cref="ExposeData"/> 链尾调用（保持主文件只加一行）。</summary>
        internal void ExposeWorkDemotion()
        {
            Scribe_Deep.Look(ref WorkDemotionLedger, "workshopWorkDemotion");
            if (WorkDemotionLedger == null)
            {
                WorkDemotionLedger = new WorkshopWorkDemotionLedger();
            }
        }

        /// <summary>台账的永不为 null 入口（消费方统一走这里，杜绝判 null 漏写）。</summary>
        public WorkshopWorkDemotionLedger DemotionLedger
        {
            get
            {
                if (WorkDemotionLedger == null)
                {
                    WorkDemotionLedger = new WorkshopWorkDemotionLedger();
                }
                return WorkDemotionLedger;
            }
        }
    }
}
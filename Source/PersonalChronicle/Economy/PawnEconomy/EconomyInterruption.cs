using System.Collections.Generic;
using PersonalChronicle.Automation;
using PersonalChronicle.Domain.PawnEconomy.Learning;
using PersonalChronicle.Domain.PawnEconomy.Planning;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// M9 §5（CHG-20261009-ECON-M9-S5）：「中断和加载恢复协议」的**经济侧消费方**。
    ///
    /// <para>两条入口共用同一套判决（Domain 纯规则 InterruptionProtocolRules.Resolve）：
    /// ① 购买尝试的失败定格 —— 证据来自真实对象与 M11 账本（货是否已到买方 / 结算行阶段）；
    /// ② 车间工作被中断的事实 —— Automation 侧上报，本层取走后定格。</para>
    ///
    /// <para>不变量（§5 第 5 条）：**一次中断至多写一条 M9 失败记录**；已结算 / 让路 / 适配未知
    /// 三类零记录（§4）。本类不动任何钱物：不放款、不退货、不改 M11 账目 —— 各自的处置动作
    /// 仍归唯一负责方（购买链的回滚/标记在 EconomyConsumption 内完成）。</para>
    /// </summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        // ---- 入口①：购买尝试的 §5 定格（调用方 = OnPlanPublished 的失败分支）---------------

        /// <summary>
        /// 购买失败后的**单点记账**：本方法与 ResolveWorkshopInterruption（本文件）是全工程
        /// 仅有的两处 RecordAttemptFailure 写入，且都发生在判决之后 —— 由此
        /// 「一个中断至多一条记录」是结构事实，而不是各调用点的自觉；原因码与阶段均由真实证据给出。
        /// </summary>
        private void ResolvePurchaseAttempt(ShadowPlan plan, PurchaseAttempt attempt, int now)
        {
            var verdict = InterruptionProtocolRules.Resolve(attempt.Delivered, attempt.Consumed, attempt.Settled, attempt.ReasonCode);
            if (!verdict.RecordFailure) return;
            // 指纹只取**有意义的修订**：现金与地图。不含 Tick —— 含 Tick 会让每次重算都像「条件变了」。
            plan.Budget.AvailableMinor.TryGetValue(out long cashAtFailure);
            RecordAttemptFailure(plan.PawnId, "Purchase", "Food", now,
                RetryBackoffRules.Fingerprint(null, null, cashAtFailure, null, plan.MapId));
        }

        // ---- 入口②：车间中断事实的取走与定格（§5 第 1~5 步的最小闭环）----------------------

        private readonly List<MapComponent_WorkshopManager.WorkInterruptionFact> interruptionScratch
            = new List<MapComponent_WorkshopManager.WorkInterruptionFact>();

        /// <summary>
        /// 取走各图车间管理器积压的中断事实（方向 Economy → Automation，符合既有依赖）。
        /// 无事实时为 O(地图数) 的空检查，不扫 Pawn、不建对象。
        /// </summary>
        private void DrainWorkshopInterruptions()
        {
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                var manager = MapComponent_WorkshopManager.Get(maps[i]);
                if (manager == null) continue;
                interruptionScratch.Clear();
                if (manager.DrainEconomicWorkInterruptions(interruptionScratch) == 0) continue;
                for (int n = 0; n < interruptionScratch.Count; n++) ResolveWorkshopInterruption(interruptionScratch[n]);
            }
        }

        /// <summary>
        /// §5 定格：①②（结束租约 / 仅释放本票据占用 / 已交付片段保留）由原版 EndCurrentJob 与车间侧
        /// 完成；③ 的业务阶段证据面 = 该次动作尚未产出新交付单位（已产出的迭代由
        /// ReportCraftIteration 在完成点另行处理）。故定格输入为（未交付、未消费、未结算），
        /// 是否记账完全由**实际原因码**的规定处置决定：
        ///   · ProtectedTimetable（让路）⇒ 零记录；
        ///   · ReservationLost（票据失效）⇒ 一条，并按退避账安排有界复核（§4）。
        /// </summary>
        private void ResolveWorkshopInterruption(MapComponent_WorkshopManager.WorkInterruptionFact fact)
        {
            // 冷却未到 = 同一条件持续期间不重复记账（§4：「不每 Tick 扫相同条件」）。
            if (!MayAttempt(fact.PawnKey, "WorkshopWork", fact.CauseCode, fact.Tick)) return;
            var verdict = InterruptionProtocolRules.Resolve(false, false, false, fact.CauseCode);
            if (!verdict.RecordFailure) return;
            RecordAttemptFailure(fact.PawnKey, "WorkshopWork", fact.CauseCode, fact.Tick,
                RetryBackoffRules.Fingerprint(null, null, 0, null, fact.MapId));
        }
    }
}

using RimWorld;   // FA-01：Pawn.IsQuestLodger() 是 RimWorld 命名空间的扩展方法（同 EconomyRuntime / VanillaPawnFactCapture 的用法）
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// FA-01 修复：新 Pawn 的零余额账户登记（设计 §4「新 Pawn 账户由明确 EnrollPawn 命令建立，初始零余额；
    /// 观察查询仅 TryRead，不创建」）。
    ///
    /// 缺陷链（经理侧审计 2026-10-10 FA-01）：账本初始只有 Treasury，个人账户只在工资/旧权益**转入**时被
    /// Apply 创建；`ReadInput` 找不到个人账户即给出 `AccountUnknown` ⇒ `LifeBudget` 得 `WaitingAccount`
    /// ⇒ `ShadowPlan` 停在 `WaitingBudget` 永不进入 `SeekWork`。结果是"国库有钱、岗位存在，全新殖民者仍
    /// 无法自主挣到第一笔工资"。
    ///
    /// 修复边界（为什么不在读路径上把缺失当零）：把"缺账户"读成零余额会让**账本未知**与**确为零**混同。
    /// 这里改为设一个**明确注册边界** —— 本图在册的自由殖民者，由周期命令登记为零余额账户；命令只在账本
    /// 健康（`audit.Fault == null`）时执行，注册失败可观察（返回计数），绝不静默。
    /// </summary>
    public sealed partial class GameComponent_PawnEconomy
    {
        /// <summary>单轮最多登记数（有界；下一轮继续，不做无界全图循环）。</summary>
        private const int MaximumEnrollmentsPerPass = 32;

        /// <summary>登记本图在册但尚无账户的自由殖民者，返回本轮新增数。**不是**规划/展示路径的副作用：
        /// 只由 250 tick 周期槽调用；<c>ReadInput</c> 等只读路径永不创建账户。</summary>
        internal int EnrollObservedPawns()
        {
            if (Scribe.mode != LoadSaveMode.Inactive || Find.Maps == null) return 0;
            var audit = settlement.ReadAudit();
            if (audit == null || audit.Fault != null) return 0;   // 账本不健康 ⇒ 不登记（未知不当零）
            int added = 0;
            int refused = 0;
            for (int m = 0; m < Find.Maps.Count && added < MaximumEnrollmentsPerPass; m++)
            {
                var pawns = Find.Maps[m].mapPawns?.FreeColonistsSpawned;
                if (pawns == null) continue;
                for (int p = 0; p < pawns.Count && added < MaximumEnrollmentsPerPass; p++)
                {
                    var pawn = pawns[p];
                    if (pawn == null || pawn.Dead || !pawn.IsFreeNonSlaveColonist || pawn.IsQuestLodger()) continue;
                    string key = pawn.GetUniqueLoadID();
                    if (settlement.ReadAccount(key) != null) continue;
                    if (settlement.EnrollAccount(key)) added++;
                    else refused++;
                }
            }
            // 数据门 P1（2026-10-10 复核）：登记被**容量**拒绝必须可观察 —— 原先 false 被静默吞掉，
            // 症状与 FA-01 完全相同（AccountUnknown ⇒ WaitingAccount ⇒ WaitingBudget）却零信号。
            // 本方法处在 250 tick 槽内 ⇒ 用 WarningOnce（每会话一次）避免无界刷日志（PERF-001）。
            if (refused > 0)
                Log.WarningOnce("[PC][Economy] settlement-ledger enrolment refused for " + refused
                    + " colonist(s): the zero-balance account cap is reached; their autonomous income stalls at 'waiting for account'.", 0x5C2E01);
            return added;
        }
    }
}

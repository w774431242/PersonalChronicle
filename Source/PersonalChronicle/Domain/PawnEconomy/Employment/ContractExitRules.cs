namespace PersonalChronicle.Domain.PawnEconomy.Employment
{
    /// <summary>
    /// v0.9 §2 M6 的**合理退出**原因码。顺序即优先级：**自然结束**（到期 / 达计件上限）在前 ——
    /// 它不需要玩家解释、也不代表异常；**供给侧消失**（制度关闭 / 岗位不可用）在后。
    /// <see cref="None"/> 表示合同应继续履约。
    /// </summary>
    internal enum ContractExitCode { None, Expired, JobCapReached, InstitutionClosed, PositionUnavailable }

    /// <summary>
    /// M6 合理退出的**纯规则**（离线可证伪）。退出本身只改合同状态；
    /// 工作类型的释放仍逐一走 <see cref="WorkOwnershipRules.CanRestore"/> ——
    /// 玩家改写过的值一律保留并丢弃我方记录（EMP-06：不逐 tick 抢回，退出时也不覆盖）。
    ///
    /// **幂等**是硬要求：已非 <see cref="ContractState.Active"/> 的合同恒返回 <see cref="ContractExitCode.None"/>，
    /// 否则重复复查会重复释放所有权记录。
    /// </summary>
    internal static class ContractExitRules
    {
        /// <summary>
        /// 判断一份仍在履约的合同是否应当结束。
        /// 到期用 <c>now &gt; ExpiresTick</c>，与 <see cref="WorkContract.CanAcceptEvidence"/> 的判定保持一致
        /// （同一边界两处不同会让"最后一天能不能记工"出现分叉）。
        /// </summary>
        public static ContractExitCode ShouldExit(WorkContract contract, int now, bool institutionOpen, bool positionAvailable)
        {
            if (contract == null || contract.State != ContractState.Active) return ContractExitCode.None;
            if (now > contract.ExpiresTick) return ContractExitCode.Expired;
            if (contract.CompletedJobs >= contract.MaxJobs) return ContractExitCode.JobCapReached;
            if (!institutionOpen) return ContractExitCode.InstitutionClosed;
            if (!positionAvailable) return ContractExitCode.PositionUnavailable;
            return ContractExitCode.None;
        }

        /// <summary>自然结束（到期 / 达上限）不属异常，调用方据此决定是否提示玩家。</summary>
        public static bool IsNaturalEnd(ContractExitCode code)
            => code == ContractExitCode.Expired || code == ContractExitCode.JobCapReached;
    }
}

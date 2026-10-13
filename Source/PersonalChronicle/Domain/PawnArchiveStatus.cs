namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 档案生命周期状态（v4.x 自动归档系统）。
    /// 状态机：Active → (死亡) → Archived → (过期) → PendingPurge → (销毁删除)。
    ///
    /// 价值评估逻辑（Data 层 GameComponentTick）：
    ///   - 死亡时首次评估：HealthValuation 银币价值 ≥ 阈值 → Archived（永不过期，保留）
    ///   - 价值 ＜ 阈值 → Archived + ArchiveExpiryTick = DeathTick + 30天
    ///   - tick 到达 ArchiveExpiryTick → PendingPurge（UI 显示"待销毁"）
    ///   - 下一次 purge pass → 真正从 Objects/Events 删除
    /// </summary>
    public enum PawnArchiveStatus
    {
        /// <summary>在世或刚加入，正常显示。</summary>
        Active = 0,

        /// <summary>已归档。高价值档案永久保留；低价值档案等待过期。</summary>
        Archived = 1,

        /// <summary>已过期待销毁。下一次 purge pass 将真正删除此记录及关联事件。</summary>
        PendingPurge = 2,
    }
}

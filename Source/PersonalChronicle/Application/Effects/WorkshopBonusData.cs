namespace PersonalChronicle.Application.Effects
{
    /// <summary>
    /// 车间加成解析结果（纯数据快照，随缓存复用）。
    /// 缺省 = 中性（因子 1f、偏移 0），即"完全无影响"。
    ///
    /// <para><b>归属（2026-09-21 审计修复 · CHG-SHOP-AUDIT-001）</b>：原定义在
    /// <c>Automation/WorkshopBonusResolver.cs</c>。Effects/Capture 需要消费车间加成，
    /// 若类型留在 Automation 命名空间，则契约（<see cref="WorkshopBonusAccess"/>）不得不
    /// 引用 Automation 类型 ⇒ Application → Automation 违规。故把本纯数据快照上移到
    /// Application 契约层（Automation / Effects / Capture 均可引用），
    /// 实现仍由 Automation 提供（<c>WorkshopBonusAccessBinder</c>）。</para>
    /// </summary>
    public struct WorkshopEffectiveBonus
    {
        public float workTimeFactor;
        public float stuffCostFactor;
        public float yieldFactor;
        public int qualityOffset;
        public float accuracyFactor;
        public float maxHpFactor;
        public float goodRateFactor;
        public int ratingOffset;
        public float throughputFactor;
        public float repairSpeedFactor;
        public float researchSpeedFactor;

        /// <summary>中性加成（无影响）。任何解析失败都返回它，保证零副作用。</summary>
        public static WorkshopEffectiveBonus Neutral
        {
            get
            {
                return new WorkshopEffectiveBonus
                {
                    workTimeFactor = 1f,
                    stuffCostFactor = 1f,
                    yieldFactor = 1f,
                    qualityOffset = 0,
                    accuracyFactor = 1f,
                    maxHpFactor = 1f,
                    goodRateFactor = 1f,
                    ratingOffset = 0,
                    throughputFactor = 1f,
                    repairSpeedFactor = 1f,
                    researchSpeedFactor = 1f
                };
            }
        }
    }

    /// <summary>某件成品上生效的车间加成（只存精度/耐久两个"贴在产品上"的维度）。</summary>
    public struct WorkshopProductBonus
    {
        public float maxHpFactor;
        public float accuracyFactor;
    }
}

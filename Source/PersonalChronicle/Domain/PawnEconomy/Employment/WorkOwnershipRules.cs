using System;

namespace PersonalChronicle.Domain.PawnEconomy.Employment
{
    internal enum OwnershipCode
    { Applied, AlreadyActive, PlayerOwned, AbilityDisabled, NotInitialized, InvalidType, Restored, KeptPlayerValue, NoRecord }

    /// <summary>Pure gates for the economy employment write channel (EXC-2026-028). Enable direction
    /// only: this channel never disables a work type, never overwrites a player-chosen priority and
    /// never claims the medical / firefighting work types.</summary>
    internal static class WorkOwnershipRules
    {
        public const int EnabledPriority = 3;
        /// <summary>本通道的**不可写清单**。
        /// <para><b>CHG-20261012-ECON-LABOR2（2026-10-12 用户裁决「红线纳入合同解锁」）</b>：清单由
        /// 四项（含 <c>Hauling</c>/<c>Cleaning</c>）**收窄为医疗 / 灭火**。根因：合同岗位确实会需要
        /// 搬运 / 清洁（例：搬运合同 `vanilla:Hauling:…:Haul`），把它们排除会让合同在 FA-11 复核
        /// （`EconomyVanillaWork.cs` 要求 <c>GetPriority &gt; 0</c>）处**必然失败**并当场终止
        /// （<c>WorkTypeUnavailable</c>，实机 Player.log 实证）。</para>
        /// <para>仍保留两条硬边界：① 本通道**永不写 0**（禁用方向只属 `EXC-2026-016` /
        /// `EXC-2026-026`，本清单不改变这一点）；② 只认领玩家关闭态（<see cref="CanEnable"/>）。
        /// 与 `EXC-2026-016` 的 <c>MandatoryKeep</c>（四项，且允许在"有在岗专属岗位"时接管写 0）
        /// **不再是同一条红线** —— 跨台账互踩由调用方按"合同避让"处置（用户 2026-10-12 裁决）。</para></summary>
        private static readonly string[] NeverManaged = { "Doctor", "Firefighter" };
        public static bool IsManaged(string workTypeDefName)
            => !string.IsNullOrWhiteSpace(workTypeDefName) && Array.IndexOf(NeverManaged, workTypeDefName) < 0;
        /// <summary>Fail-closed enable gate. Priority zero is the only state this channel may claim.</summary>
        public static OwnershipCode CanEnable(string workTypeDefName, bool initialized, bool abilityDisabled, int currentPriority)
        {
            if (!IsManaged(workTypeDefName)) return OwnershipCode.InvalidType;
            if (!initialized) return OwnershipCode.NotInitialized;
            if (abilityDisabled) return OwnershipCode.AbilityDisabled;
            if (currentPriority != 0) return OwnershipCode.PlayerOwned;
            return OwnershipCode.Applied;
        }
        /// <summary>Restore gate: our own written value may be restored; anything else is a player edit
        /// and is kept (the record is dropped without writing).</summary>
        public static OwnershipCode CanRestore(int currentPriority, int writtenPriority)
            => currentPriority == writtenPriority ? OwnershipCode.Restored : OwnershipCode.KeptPlayerValue;
    }
}

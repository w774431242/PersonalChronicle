using PersonalChronicle.Domain;

namespace PersonalChronicle.Application
{
    /// <summary>
    /// 设备状态键 → Keyed 翻译键的唯一映射（2026-09-21 自 <c>Domain/EquipmentState.cs</c> 迁入）。
    ///
    /// <para><b>为什么在 Application 而不是 Domain（CHG-SHOP-AUDIT-001 · A-6）</b>：键面
    /// （Keyed 条目）归属表现 / 本地化层；Domain 只应持有**语言无关**的状态键
    /// （<see cref="EquipmentStateResolver.KeyRun"/> 等）。原实现把 UI / Automation 命名空间的
    /// 键名字面量写在 Domain 的 switch 里（审计 P3「键面硬编码」）。现收敛到本类：
    /// 字面量集中一处、前缀走命名常量（GOV-006），调用方（UI / Automation / Application）
    /// 一律经此取词 —— Domain 不再出现外部命名空间的键名。</para>
    ///
    /// <para>口径不变：key 缺失/未知时回落"停机"键（与旧实现 default 分支逐位一致）。</para>
    /// </summary>
    public static class EquipmentStateText
    {
        /// <summary>机台状态键前缀（Keyed 面在 `Archive.xml`）。</summary>
        private const string FloorStatePrefix = "PersonalChronicle.UI.Floor.State.";

        /// <summary>农机档位状态键前缀（Keyed 面在 `Automation.xml`）。</summary>
        private const string AutomationTabPrefix = "PersonalChronicle.Automation.Tab.";

        /// <summary>语言无关键 → Keyed 翻译键（供读模型构建期 / UI 取词）。</summary>
        public static string TranslationKeyOf(string stateKey)
        {
            switch (stateKey)
            {
                case EquipmentStateResolver.KeyRun: return FloorStatePrefix + "Run";
                case EquipmentStateResolver.KeyIdle: return FloorStatePrefix + "Idle";
                case EquipmentStateResolver.KeyFault: return FloorStatePrefix + "Fault";
                case SmartBenchState.Waiting: return "PersonalChronicle.T1.Waiting";
                case EquipmentStateResolver.KeyAgRunning: return AutomationTabPrefix + "AgRunning";
                case EquipmentStateResolver.KeyAgNoPower: return AutomationTabPrefix + "AgNoPower";
                case EquipmentStateResolver.KeyAgNoFuel: return AutomationTabPrefix + "AgNoFuel";
                case EquipmentStateResolver.KeyAgOff: return AutomationTabPrefix + "AgOff";
                default: return FloorStatePrefix + "Off";
            }
        }
    }
}

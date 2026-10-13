namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 设备运行状态（制造中心 ② 车间俯视的灯色语义）。
    ///
    /// <para>预览是 5 态（含 starved 待料）；**原版没有"缺料"的可读出口**
    /// （<c>RecipeDef.PotentiallyMissingIngredients</c> 不存在，真判定在
    /// <c>WorkGiver_DoBill.TryFindBestBillIngredients</c> 内部），故落地只做 4 态，
    /// 缺料在 M2 与 WorkGiver 对齐后补上（规范 LAND-009：缺口如实，不画永远不亮的灯）。</para>
    /// </summary>
    public enum EquipmentState
    {
        /// <summary>在产：有可执行工单。</summary>
        Run,

        /// <summary>待命：通电但没有工单。</summary>
        Idle,

        /// <summary>故障：<c>CompBreakdownable.BrokenDown</c>。</summary>
        Fault,

        /// <summary>停机：未通电（无电力 comp 的台视为始终通电）。</summary>
        Off
    }

    /// <summary>
    /// 设备状态派生（纯函数，可离线单测）。判定顺序即优先级：
    /// 故障 &gt; 断电 &gt; 有活/没活 —— 与预览 <c>ST</c> 的语义一致。
    /// </summary>
    public static class EquipmentStateResolver
    {
        /// <summary>语言无关的状态键（GOV-009：逻辑与展示分离，UI 只按键选色/取词）。</summary>
        public const string KeyRun = "run";
        public const string KeyIdle = "idle";
        public const string KeyFault = "fault";
        public const string KeyOff = "off";

        /// <summary>
        /// 派生状态。<paramref name="powerOn"/> 对**没有电力 comp** 的设备应传 <c>true</c>
        /// （手工台没有 CompPowerTrader，不代表它停机）。
        /// </summary>
        public static EquipmentState Resolve(bool brokenDown, bool powerOn, bool hasExecutableBill)
        {
            if (brokenDown)
            {
                return EquipmentState.Fault;
            }
            if (!powerOn)
            {
                return EquipmentState.Off;
            }
            return hasExecutableBill ? EquipmentState.Run : EquipmentState.Idle;
        }

        /// <summary>状态 → 语言无关键。</summary>
        public static string KeyOf(EquipmentState state)
        {
            switch (state)
            {
                case EquipmentState.Run: return KeyRun;
                case EquipmentState.Idle: return KeyIdle;
                case EquipmentState.Fault: return KeyFault;
                default: return KeyOff;
            }
        }

        // ---- 设备自报状态键（IWorkshopDeviceState，2026-09-11）--------------------
        // 农机档位状态：与"机台"共用同一套状态键词汇表，避免设备页与农业页两套口径。

        public const string KeyAgRunning = "AgRunning";
        public const string KeyAgNoPower = "AgNoPower";
        public const string KeyAgNoFuel = "AgNoFuel";
        public const string KeyAgOff = "AgOff";

        // 键面映射（状态键 → Keyed 翻译键）**已上移至** `Application/EquipmentStateText.cs`
        // （2026-09-21 · CHG-SHOP-AUDIT-001 · A-6）：键面属表现/本地化层，
        // Domain 不再持有 UI / Automation 命名空间的键名字面量（GOV-006）。
        // 取词一律走 `EquipmentStateText.TranslationKeyOf(key)`。
    }
}

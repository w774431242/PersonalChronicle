namespace PersonalChronicle.Domain
{
    /// <summary>
    /// 设备识别规则 —— <b>纯函数，可离线单测</b>（2026-09-11 体检后抽取）。
    ///
    /// <para><b>为什么要抽出来</b>：原判据写在 Automation 里直接做类型检查
    /// （<c>thing is IWorkshopDevice || thing is Building &amp;&amp; thing is IBillGiver</c>），
    /// 而离线单测无法构造 <c>Building</c>/<c>Pawn</c> ⇒ 曾经"把 pawn 当设备"的回归
    ///（<c>Verse.Pawn : IBillGiver</c>）没有任何测试能挡住。抽成"布尔输入 → 判定"的纯函数后，
    /// 这个坑就有了回归测试。</para>
    /// </summary>
    public static class WorkshopDeviceRules
    {
        /// <summary>
        /// 是否算「机台」（可读原版工单的设备）：<b>建筑</b>且实现 <c>IBillGiver</c>。
        ///
        /// <para>⚠️ 建筑判定不可省：原版 <c>Pawn</c> 也实现 <c>IBillGiver</c>
        /// （其 <c>BillStack</c> 是 <c>health.surgeryBills</c>）。</para>
        /// </summary>
        public static bool IsBillGiverBench(bool isBuilding, bool isBillGiver)
        {
            return isBuilding && isBillGiver;
        }

        /// <summary>
        /// 是否算车间设备：显式声明了 <c>IWorkshopDevice</c>（自家/第三方，可能是非建筑），
        /// 或者是「机台」（见 <see cref="IsBillGiverBench"/>）。
        /// </summary>
        public static bool IsWorkshopDevice(bool declaresDeviceInterface, bool isBuilding, bool isBillGiver)
        {
            return declaresDeviceInterface || IsBillGiverBench(isBuilding, isBillGiver);
        }
    }
}

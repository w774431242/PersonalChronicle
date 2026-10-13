namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间内「可管理设备」的统一标记。
    ///
    /// 加工设备（<c>Building_AutoProcessor</c>）与传送带段
    /// （<c>Building_ConveyorSegment</c>）实现本接口后，即被车间统计与管理面板
    /// 自动纳入：<b>零注册、零 defName 硬编码、零 Harmony</b>。
    ///
    /// 之所以用接口而不是按 defName 前缀匹配：后者会把设备清单写死在数据层，
    /// 第三方 mod 想扩展设备类型就必须改我们的代码（违反开闭原则）。
    /// </summary>
    public interface IWorkshopDevice
    {
        /// <summary>设备显示名（管理面板的设备清单用）。</summary>
        string DeviceLabel { get; }
    }

    /// <summary>
    /// 设备「自报状态」可选能力接口（2026-09-11 体检 P2 新增）。
    ///
    /// <para><b>为什么单独一个接口</b>：设备状态过去只能由管理面板按原版 comp
    /// （`CompBreakdownable` / `CompPowerTrader` / `IBillGiver.BillStack`）反推，
    /// 对**没有工单、没有电力的自研设备**（传送带、农机）三输入恒为 `false/true/null`
    /// ⇒ 设备页永远显示"待命"，而农业页又另写了一套状态判定（两处口径不一致）。
    /// 有了本接口，设备**自己**报状态，设备页/俯视图/农业页共用一个词表
    /// （键见 <see cref="PersonalChronicle.Domain.EquipmentStateResolver"/>，由
    /// <see cref="PersonalChronicle.Application.EquipmentStateText.TranslationKeyOf"/> 统一取词）。</para>
    ///
    /// <para>刻意**不**往 <see cref="IWorkshopDevice"/> 里加成员：那会破坏已编译的第三方实现
    /// （二进制不兼容）。实现可选、探测式使用，零破坏。</para>
    /// </summary>
    public interface IWorkshopDeviceState
    {
        /// <summary>当前状态键（<c>run</c>/<c>idle</c>/<c>fault</c>/<c>off</c>/<c>Ag*</c>）；返回 null/空 = 交回默认反推。</summary>
        string StateKeyOrNull();
    }
}

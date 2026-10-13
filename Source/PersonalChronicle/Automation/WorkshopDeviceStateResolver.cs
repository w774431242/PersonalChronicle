using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 设备状态键解析 —— <b>唯一实现</b>（2026-09-11 Phase 2 抽取）。
    ///
    /// <para>此前同一段判定被抄在三处：管理面板设备页、倒班表 KPI（稼动率）、交接班点检表。
    /// 现集中于此：① 设备**自报**优先（<see cref="IWorkshopDeviceState"/>，自研设备无工单/电力可反推）；
    /// ② 否则按原版 comp 反推（故障 / 断电 / 有可执行工单）；③ 全程异常隔离。</para>
    ///
    /// <para>返回值为状态键常量（<see cref="PersonalChronicle.Domain.EquipmentStateResolver"/>），
    /// 取词一律走 <see cref="PersonalChronicle.Application.EquipmentStateText.TranslationKeyOf"/>。</para>
    /// </summary>
    public static class WorkshopDeviceStateResolver
    {
        public static string KeyOf(Thing thing)
        {
            if (thing == null)
            {
                return PersonalChronicle.Domain.EquipmentStateResolver.KeyIdle;
            }
            if (thing is IWorkshopDeviceState selfReported)
            {
                string reported = selfReported.StateKeyOrNull();
                if (!string.IsNullOrEmpty(reported))
                {
                    return reported;
                }
            }
            if (!WorkshopStats.IsBillGiverBench(thing))
            {
                // 既不自报也不是机台（例如第三方 IWorkshopDevice）：无状态可判。
                return PersonalChronicle.Domain.EquipmentStateResolver.KeyIdle;
            }
            try
            {
                CompBreakdownable breakdown = thing.TryGetComp<CompBreakdownable>();
                CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
                BillStack bills = (thing as IBillGiver).BillStack;
                return PersonalChronicle.Domain.EquipmentStateResolver.KeyOf(
                    PersonalChronicle.Domain.EquipmentStateResolver.Resolve(
                        breakdown != null && breakdown.BrokenDown,
                        power == null || power.PowerOn,
                        bills != null && bills.AnyShouldDoNow));
            }
            catch (System.Exception)
            {
                // 单个设备的 comp 读取异常不允许打断整页绘制（同读模型快照纪律）。
                return PersonalChronicle.Domain.EquipmentStateResolver.KeyIdle;
            }
        }

        /// <summary>是否处于"在运行"（稼动）：机台运行 / 农机运行中。</summary>
        public static bool IsRunning(string stateKey)
        {
            return stateKey == PersonalChronicle.Domain.EquipmentStateResolver.KeyRun
                || stateKey == PersonalChronicle.Domain.EquipmentStateResolver.KeyAgRunning;
        }
    }
}

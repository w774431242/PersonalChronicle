using System.Collections.Generic;
using PersonalChronicle.Data;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// CHG-20261005-OFFSHIFT · **EXC-2026-026**(E2) —— 离班自动取消班组工作类型。
    /// <para>⚠ 2026-10-05（`CHG-20261005-SETTINGS-REMOVE2`）·**更正引用**：本文件原写作
    /// <c>EXC-2026-025(E2)</c>，但 025 是「车间在编员工忽略『无法从事』」的 Harmony 例外，
    /// 与本写 0 行为无关 ⇒ 属**引用错误**（该写 0 点当时实际没有有效登记，
    /// 而 `EXC-2026-016` / `EXC-2026-023` 都声明「写 0 只剩接管通道」）。现补登记
    /// **EXC-2026-026** 并以此为权威引用。</para>
    ///
    /// <para>规则：当前游戏时间<b>不在</b>某班组任何一个已指派班次的时间段内 ⇒
    /// 该班组<b>成员</b>在这些班次覆盖的工作类型上被<b>取消勾选（档位 0）</b>。
    /// 在班时本方法不做任何事 —— 由既有 <c>SyncVanillaWorkSettings</c> /
    /// <c>EnableWorkTypes</c> 写值勤 3 / 映射类型 1（互补而非重复）。</para>
    ///
    /// <para><b>为何必须登记 EXC（E2）</b>：本方法是全工程<b>第二个</b>
    /// <c>SetPriority(x, 0)</c> 调用点。0 是 EXC-2026-016 接管的哨兵值，
    /// 其单点授权前提是「PC 写 0 只发生在接管」。</para>
    ///
    /// <para><b>不误判的保证（M1 退让）</b>：写 0 之前显式
    /// <c>DemotionLedger.Discard</c> + <c>DiscardTakeover</c>。
    /// 接管闸门是「当前值 == 0 才还原」；若写 0 后台账仍有记录，玩家点「解除接管」
    /// 会把档位还原成 X ⇒ <b>离班期间被重新启用</b>（错的）。
    /// 退让后台账无记录 ⇒ 闸门遍历不到 ⇒ 不可能误还原。</para>
    ///
    /// <para><b>触发</b>：挂在既有 60-tick <c>MapComponentTick</c> 内，
    /// 用游戏小时变更做内层门控 ⇒ 每游戏小时最多跑一次，不是每 tick。</para>
    /// </summary>
    public partial class MapComponent_WorkshopManager
    {
        private int lastOffShiftHour = -1;
        private const string OffShiftDutyDefName = "PC_WorkshopOperation";

        /// <summary>
        /// 班次时间窗判定（支持跨午夜，如 20:00→08:00）。
        ///
        /// <para>2026-10-04 深度审计：原为工程内<b>第 3 份</b>手工区间判定
        /// （另有 <c>Domain.WorkshopTime.InRange</c> 权威实现与 <c>WorkshopShift.InTime</c> 转发），
        /// 三份并存即有口径漂移风险。现转发到 <see cref="Domain.WorkshopTime.InRange"/>
        /// —— 逐条核对语义完全一致：左闭右开、<c>start == end</c> 视为全天、<c>end &lt; start</c> 跨午夜；
        /// InRange 另带 0-23 守卫（更严谨，合法输入下行为逐字相同）。</para>
        /// </summary>
        private static bool HourInShift(int hour, WorkshopShift shift)
        {
            return shift != null
                && Domain.WorkshopTime.InRange(hour, shift.StartHour, shift.EndHour);
        }

        private void ApplyOffShiftWorkTypes()
        {
            if (map == null) { return; }
            // CHG-20261004-SHIFTGATE：与 EnableWorkTypes 的 0/3 互跳已由 ShouldSkipAutoEnableForShift
            // 门控根除 —— 离班者不再被面板刷新提权，本链写 0 后不会被写回，故两者不再争抢同一格位。
            //
            // ⚠ CHG-20261005-SETTINGS-REMOVE2：本项已**默认化恒开**（PM 裁决「保持默认开启、
            //   不写入 mod 设置」）⇒ 这里不再询问 `Settings.EnableOffShiftCancel`，也**不再存在
            //   「关闭」语义**（原门控提供玩家退出权：关 = 离班者保留上班档位）。
            //   取消退出权 ⇒ **扩大**了「写 0」的授权面，已单独登记 **EXC-2026-026**（E2）。
            //   字段本身保留仅因同文件 legacy 绘制块仍引用它（见变更标注残留段）。
            int hour = GenLocalDate.HourOfDay(map);
            if (hour == lastOffShiftHour) { return; }
            lastOffShiftHour = hour;
            RunOffShiftPass(hour);
        }

        internal int RunOffShiftPass(int hour)
        {
            if (map == null) { return 0; }
            if (Current.Game?.GetComponent<ChronicleGameComponent>() == null) { return 0; }
            int changed = 0;
            List<Zone_Workshop> zones = AllWorkshopZones();
            for (int zi = 0; zi < zones.Count; zi++)
            {
                List<WorkshopCrew> crews = zones[zi] != null ? zones[zi].Crews : null;
                if (crews == null) { continue; }
                for (int ci = 0; ci < crews.Count; ci++)
                {
                    WorkshopCrew crew = crews[ci];
                    if (crew == null || crew.AssignedShifts == null
                        || crew.AssignedShifts.Count == 0) { continue; }

                    bool onDuty = false;
                    for (int si = 0; si < crew.AssignedShifts.Count; si++)
                    {
                        if (HourInShift(hour, crew.AssignedShifts[si])) { onDuty = true; break; }
                    }
                    if (onDuty) { continue; }

                    for (int si = 0; si < crew.AssignedShifts.Count; si++)
                    {
                        changed += DisableForCrew(crew, crew.AssignedShifts[si]);
                    }
                }
            }
            return changed;
        }

        private int DisableForCrew(WorkshopCrew crew, WorkshopShift shift)
        {
            if (crew == null || shift == null || crew.Members == null) { return 0; }
            ChronicleGameComponent comp = Current.Game?.GetComponent<ChronicleGameComponent>();
            if (comp == null) { return 0; }

            List<string> names = shift.TargetZone != null && shift.TargetZone.WorkshopType != null
                ? shift.TargetZone.WorkshopType.WorkTypeDefNamesOf(shift.TaskType)
                : null;
            HashSet<string> managed = new HashSet<string>();
            if (names != null)
            {
                for (int i = 0; i < names.Count; i++)
                {
                    if (!string.IsNullOrEmpty(names[i])) { managed.Add(names[i]); }
                }
            }
            managed.Add(OffShiftDutyDefName);

            int changed = 0;
            for (int mi = 0; mi < crew.Members.Count; mi++)
            {
                Pawn pawn = crew.Members[mi];
                if (pawn == null || pawn.Dead || pawn.workSettings == null) { continue; }
                bool touched = false;
                foreach (string defName in managed)
                {
                    WorkTypeDef wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
                    if (wt == null) { continue; }
                    if (pawn.workSettings.GetPriority(wt) <= 0) { continue; }
                    comp.DemotionLedger.Discard(pawn.thingIDNumber, wt.defName);
                    comp.DiscardTakeover(pawn.thingIDNumber, wt.defName);
                    pawn.workSettings.SetPriority(wt, 0);
                    touched = true;
                    changed++;
                }
                if (touched) { pawn.workSettings.Notify_UseWorkPrioritiesChanged(); }
            }
            return changed;
        }
    }
}
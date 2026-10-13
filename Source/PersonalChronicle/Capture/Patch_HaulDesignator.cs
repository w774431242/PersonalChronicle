using System;
using HarmonyLib;
using PersonalChronicle.Economy.PawnEconomy;
using RimWorld;
using Verse;

namespace PersonalChronicle.Capture
{
    /// <summary>
    /// CHG-20261012-ECON-HAULCMD：**放宽原版搬运命令的受理范围**（食品 / 武器 / 原料等 alwaysHaulable 物品）。
    ///
    /// <para><b>原版事实</b>：<c>Designator_Haul.CanDesignateThing</c> 硬判 <c>t.def.designateHaulable</c>
    /// ⇒ 只有石块类（<c>designateHaulable = true</c>，见 <c>Data/Core/Defs/ThingDefs_Misc/Various_Stone.xml:23</c>）
    /// 可被搬运命令标记；食品 / 武器的 <c>alwaysHaulable = true</c> 使它们**永远无法被玩家标记**。
    /// 原版提示语 <c>MessageMustDesignateHaulable</c> 明说有些物品永远可搬、不能被指定。</para>
    ///
    /// <para><b>为什么不直接改 <c>designateHaulable</c></b>：那是原版 Def 字段的全局语义（还牵动
    /// <c>ListerHaulables.ShouldBeHaulable</c> / <c>StoreUtility</c> 等一整套搬运经济），且本工程不得改写
    /// 原版 Def。放宽只应作用在**玩家下达这一处**。</para>
    ///
    /// <para><b>为什么是 Postfix 而非 Prefix</b>：Prefix 需要复刻原版全部判定，任一漂移即静默错判；
    /// Postfix 只在原版**已拒绝**（且本格确实没有原版候选）时才补一次放宽判定，
    /// 原版其余行为逐字不动（原版带 Reason 的 <c>MessageAlreadyInStorage</c> 分支天然保留）。</para>
    ///
    /// <para><b>⚠ R2 返修（2026-10-12 实机反馈「无法搬运非石类」）</b>：R1 把格内兜底挂在
    /// <c>Verse.GridsUtility.GetFirstHaulable</c> 上 —— 实机未生效（Player.log 实证游戏已加载本轮构建
    /// <c>build 2026-10-12 15:16 UTC</c>，而原版提示照旧）。该方法是 6 行的静态小方法，
    /// **极可能被 Mono JIT 内联进 <c>Designator_Haul</c> 的两个调用方**，补丁挂在不被执行的调用点上。
    /// 现改为挂**最外层虚方法** <c>CanDesignateCell</c> / <c>DesignateSingleCell</c>（由基类
    /// <c>Designator.DesignateMultiCell</c> 经 vtable 调用 ⇒ 不受内联影响），并加两条一次性探针日志，
    /// 使「补丁未执行」与「该格确实没有可搬运物」在 <c>Player.log</c> 里可区分。</para>
    ///
    /// Patch 档案：
    ///   Patch Target      : RimWorld.Designator_Haul.CanDesignateCell(IntVec3)
    ///   Patch Type        : Postfix（仅在「原版无候选」分支改写返回值）
    ///   Reason            : 让玩家能对食品 / 武器等物品下达搬运命令；见 CHG-20261012-ECON-HAULCMD
    ///   Alternative       : ① 改原版 ThingDef.designateHaulable —— **否决**（改写原版 Def，且全局改变
    ///                       ListerHaulables / 存储判定）；② 自绘 Designator —— **否决**（Orders 菜单无法注入
    ///                       自有设计器，玩家看不到，且与既有 Haul designation 口径分叉）；
    ///                       ③ 挂 GridsUtility.GetFirstHaulable —— **已实测否决**（R2：内联绕过，见上）
    ///   Compatibility Risk: 低 — 只在原版拒绝后补判，判据只认「已生成 / EverHaulable / Item / 未标记 / 未入库」；
    ///                       不碰任何带原版候选的格子，第三方设计器补丁亦不重叠
    ///   Execution Order   : 无顺序契约（Postfix 只读 thingGrid）
    ///   Failure Behavior  : catch → Log.ErrorOnce(唯一错误码)，返回原版结果（最坏 = 该次下达维持原版拒绝）
    ///   Rollback          : 删除本文件即回滚（已下达的仍是原版 Haul designation，随档保留可正常执行）
    /// </summary>
    [HarmonyPatch(typeof(Designator_Haul), nameof(Designator_Haul.CanDesignateCell), new[] { typeof(IntVec3) })]
    public static class Patch_HaulDesignatorCell
    {
        /// <summary>签名探测（漂移 ⇒ Harmony 静默跳过 ⇒ 启动期显式报出，与既有 Patch 同构）。</summary>
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();

        private static bool ProbeTargetMethod()
        {
            bool found = AccessTools.Method(typeof(Designator_Haul), "CanDesignateCell", new[] { typeof(IntVec3) }) != null;
            if (!found)
                ChronicleLog.Error(ChronicleLog.Category.Capture,
                    "Designator_Haul.CanDesignateCell signature changed; command-based hauling for alwaysHaulable items disabled - update Patch_HaulDesignator");
            return found;
        }

        private static void Postfix(Designator_Haul __instance, IntVec3 c, ref AcceptanceReport __result)
        {
            try
            {
                if (__result.Accepted) return;
                var map = __instance?.Map;
                if (map == null) return;
                // 原版有候选（石块类）⇒ 不介入，原版结果原样返回。
                if (c.GetFirstHaulable(map) != null) return;
                Thing candidate = HaulCommandRules.FindCommandableInCell(c, map);
                // R2 探针已按使命移除（2026-10-12 实机实证后）：它们曾把「补丁没跑」与
                // 「这一格确实没有可搬运物」在 Player.log 里分开，并已确认接管路径跑通
                // （Player.log: commandable candidate taken over (Gun_Revolver at (126, 0, 126))）。
                // 玩家侧不需要常驻告警 ⇒ 改为零噪音；再次诊断时按 git 历史恢复这两行即可。
                if (candidate == null) return;
                // 判据自足（不回调 CanDesignateThing）：避免再次依赖可能被内联的实例方法。
                if (HaulCommandRules.ShouldRelaxVanillaRejection(candidate))
                { __result = AcceptanceReport.WasAccepted; return; }
                if (HaulCommandRules.IsBlockedByValidStorage(candidate))
                    __result = "MessageAlreadyInStorage".Translate();
            }
            catch (Exception error)
            {
                Log.ErrorOnce("[PC][Economy] haul designator cell relax failed: " + error.Message, 0x5A1831);
            }
        }
    }

    /// <summary>
    /// 单点下达的接管。<c>CanDesignateCell</c> 放宽受理后，原版 <c>DesignateSingleCell</c> 仍会用
    /// <c>c.GetFirstHaulable(Map)</c> 取目标 ⇒ 对我们的放宽对象恒为 <c>null</c>，于是
    /// <c>new Designation(null, …)</c> 会在 <c>DesignationManager.AddDesignation</c> 里解引用 null 目标
    /// （<c>DesignationOn(null,…)</c> 即抛）。因此这里必须用**同一判据来源**自己补上这条下达。
    ///
    /// <para><b>为什么用 Prefix 接管</b>：该方法没有可回写的目标参数，只能在原版执行前替换行为；
    /// 接管仅在「原版取不到候选」时发生，原版路径（石块类）逐字不变。</para>
    ///
    /// <para>必须经 <c>DesignationManager.AddDesignation</c> 写入（不得绕过）—— 来源捕获
    /// <c>Patch_DesignationSource</c> 挂在该入口上，绕开它会让这次下达**不被劳务站识别为玩家来源**
    /// （既不计待办也不计酬）。</para>
    ///
    /// Patch 档案：
    ///   Patch Target      : RimWorld.Designator_Haul.DesignateSingleCell(IntVec3)
    ///   Patch Type        : Prefix（仅在原版无候选时接管，返回 false 跳过原版）
    ///   Reason            : 使单点下达与 CanDesignateCell 的放宽口径一致；见 CHG-20261012-ECON-HAULCMD
    ///   Alternative       : ① Postfix 补一次 —— **否决**（原版已先执行 null 目标，异常已发生）；
    ///                       ② 挂 GetFirstHaulable —— **已实测否决**（R2 内联绕过）
    ///   Compatibility Risk: 低 — 有原版候选即完全不介入；写入通道与原版逐字相同
    ///   Execution Order   : 与 Patch_HaulDesignatorCell 的 Postfix 无顺序契约（一个管判定、一个管下达）
    ///   Failure Behavior  : catch → Log.ErrorOnce(唯一错误码)，放行原版（最坏 = 维持原版行为）
    ///   Rollback          : 删除本文件即回滚
    /// </summary>
    [HarmonyPatch(typeof(Designator_Haul), nameof(Designator_Haul.DesignateSingleCell), new[] { typeof(IntVec3) })]
    public static class Patch_HaulDesignatorSingleCell
    {
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();

        private static bool ProbeTargetMethod()
        {
            bool found = AccessTools.Method(typeof(Designator_Haul), "DesignateSingleCell", new[] { typeof(IntVec3) }) != null;
            if (!found)
                ChronicleLog.Error(ChronicleLog.Category.Capture,
                    "Designator_Haul.DesignateSingleCell signature changed; command-based hauling for alwaysHaulable items disabled - update Patch_HaulDesignator");
            return found;
        }

        private static bool Prefix(Designator_Haul __instance, IntVec3 c)
        {
            try
            {
                var map = __instance?.Map;
                if (map == null) return true;
                if (c.GetFirstHaulable(map) != null) return true;   // 原版路径（石块类）
                Thing candidate = HaulCommandRules.FindCommandableInCell(c, map);
                if (candidate == null) return false;                 // 无候选：原版此处会解引用 null 目标，直接跳过
                map.designationManager.AddDesignation(new Designation(candidate, DesignationDefOf.Haul, null));
                return false;                                        // 已接管
            }
            catch (Exception error)
            {
                Log.ErrorOnce("[PC][Economy] haul single-cell takeover failed: " + error.Message, 0x5A1836);
                return true;
            }
        }
    }

    /// <summary>
    /// 受理判定的放宽（对象级入口，供鼠标悬停判定 / 第三方调用方复用）。格内拖选路径不依赖它
    /// （见 <c>Patch_HaulDesignatorCell</c> 的 R2 说明），但保留它才能让"通过对象而非格子"发起的
    /// 下达（含第三方 Mod 直接调用 <c>CanDesignateThing</c>）得到同一结果。
    ///
    /// Patch 档案：
    ///   Patch Target      : RimWorld.Designator_Haul.CanDesignateThing(Thing)
    ///   Patch Type        : Postfix（仅在「裸拒」分支改写返回值）
    ///   Reason            : 同族放宽的对象级入口；见 CHG-20261012-ECON-HAULCMD
    ///   Alternative       : 见 Patch_HaulDesignatorCell
    ///   Compatibility Risk: 低 — 只在原版裸拒后补判
    ///   Execution Order   : 无顺序契约
    ///   Failure Behavior  : catch → Log.ErrorOnce(唯一错误码)，返回原版结果
    ///   Rollback          : 删除本文件即回滚
    /// </summary>
    [HarmonyPatch(typeof(Designator_Haul), nameof(Designator_Haul.CanDesignateThing), new[] { typeof(Thing) })]
    public static class Patch_HaulDesignatorThing
    {
        internal static readonly bool TargetMethodExists = ProbeTargetMethod();

        private static bool ProbeTargetMethod()
        {
            bool found = AccessTools.Method(typeof(Designator_Haul), "CanDesignateThing", new[] { typeof(Thing) }) != null;
            if (!found)
                ChronicleLog.Error(ChronicleLog.Category.Capture,
                    "Designator_Haul.CanDesignateThing signature changed; command-based hauling for alwaysHaulable items disabled - update Patch_HaulDesignator");
            return found;
        }

        private static void Postfix(Thing t, ref AcceptanceReport __result)
        {
            try
            {
                if (!HaulCommandRules.IsBareRejection(__result)) return;   // 只处理原版「裸拒」（Reason 为空串，非 null）
                if (HaulCommandRules.ShouldRelaxVanillaRejection(t)) { __result = AcceptanceReport.WasAccepted; return; }
                if (HaulCommandRules.IsBlockedByValidStorage(t)) __result = "MessageAlreadyInStorage".Translate();
            }
            catch (Exception error)
            {
                Log.ErrorOnce("[PC][Economy] haul designator relax failed: " + error.Message, 0x5A1831);
            }
        }
    }

    /// <summary>启动期一次性强制探测（与 <c>Patch_DesignationSourceStartup</c> 同构）。</summary>
    [StaticConstructorOnStartup]
    public static class Patch_HaulDesignatorStartup
    {
        static Patch_HaulDesignatorStartup()
        {
            _ = Patch_HaulDesignatorCell.TargetMethodExists;
            _ = Patch_HaulDesignatorSingleCell.TargetMethodExists;
            _ = Patch_HaulDesignatorThing.TargetMethodExists;
        }
    }
}

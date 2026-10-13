using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Economy.PawnEconomy
{
    /// <summary>
    /// CHG-20261012-ECON-HAULCMD：**原版搬运命令的放宽判据**（唯一来源）。
    ///
    /// <para><b>为什么需要它</b>：原版 <c>Designator_Haul.CanDesignateThing</c> 硬判
    /// <c>t.def.designateHaulable</c>（<c>_research/vanilla/Assembly-CSharp/RimWorld/Designator_Haul.cs:70-85</c>），
    /// 而 <c>ThingDef.EverHaulable = alwaysHaulable || designateHaulable</c>（<c>Verse/ThingDef.cs:16-22</c>）
    /// —— 食品 / 武器 / 原料等 <c>alwaysHaulable</c> 的物品**本来就永远不能被玩家用搬运命令标记**。
    /// 本判据把「可被下达搬运命令」的目标**形态**与**即时状态**分开表达，供两处挂点复用：
    /// ① <c>Designator_Haul.CanDesignateThing</c> 的 Postfix（放宽原版裸拒）；
    /// ② <c>Verse.GridsUtility.GetFirstHaulable</c> 的 Postfix（格内兜底候选，使
    ///    <c>CanDesignateCell</c> 与 <c>DesignateSingleCell</c> 口径一致 —— 只补其一会让
    ///    <c>DesignateSingleCell</c> 取到 null 并 <c>new Designation(null, ...)</c>，
    ///    在 <c>Notify_Added</c> 里对 null 目标解引用 ⇒ NRE）。</para>
    ///
    /// <para><b>为什么形态与状态要分开</b>：<c>GetFirstHaulable</c> 在原版里是**形态过滤器**
    /// （只认 <c>designateHaulable</c>），真正的受理判定在 <c>CanDesignateThing</c>。保持这个分工，
    /// 「拖动框选（CanDesignateCell → CanDesignateThing）」与「单点下达（DesignateSingleCell）」
    /// 才是同一条候选来源，不产生两套口径。</para>
    ///
    /// <para><b>纯函数 + 适配器双出口</b>：布尔重载可离线断言（无 Map / 无 Def 亦可跑）；
    /// Thing 重载只负责取事实，不含业务分支。</para>
    /// </summary>
    public static class HaulCommandRules
    {
        /// <summary>目标**形态**是否属于可下达搬运命令的候选（对应 <c>GetFirstHaulable</c> 兜底）。</summary>
        public static bool IsCommandableShape(bool spawned, bool everHaulable, bool isItem)
            => spawned && everHaulable && isItem;

        /// <summary>形态适配器（<c>EverHaulable = alwaysHaulable || designateHaulable</c>）。</summary>
        public static bool IsCommandableShape(Thing thing)
        {
            if (thing == null || !thing.Spawned) return false;
            var def = thing.def;
            if (def == null || !def.EverHaulable) return false;
            return def.category == ThingCategory.Item;
        }

        /// <summary>
        /// 原版「裸拒」（<c>Accepted == false</c> 且 <c>Reason == null</c>）**是否应放宽为受理**。
        ///
        /// <para>保留两条原版拒绝：① 已存在 <c>Haul</c> designation；② 已在合格储存区。
        /// ② 必须在放宽判据里**显式重做**：原版对 <c>designateHaulable</c> 目标走得到
        /// <c>IsInValidStorage()</c> 并返回带 Reason 的报告，但 <c>alwaysHaulable</c> 目标在
        /// 第一条 <c>designateHaulable</c> 判定处就被裸拒 —— 不复做就会给「已在库内」的物品挂上
        /// 永远不会被执行的 designation（<c>ListerHaulables.ShouldBeHaulable</c> 会把它过滤掉）
        /// ⇒ 幽灵待办 + 永不消失的标记。</para>
        /// </summary>
        public static bool ShouldRelaxVanillaRejection(bool spawned, bool everHaulable, bool isItem,
            bool alreadyDesignatedHaul, bool inValidStorage)
            => IsCommandableShape(spawned, everHaulable, isItem) && !alreadyDesignatedHaul && !inValidStorage;

        /// <summary>同上（Thing 适配）。</summary>
        public static bool ShouldRelaxVanillaRejection(Thing thing)
            => IsCommandableShape(thing) && !AlreadyDesignatedHaul(thing) && !IsInValidStorage(thing);

        /// <summary>
        /// 该目标是否卡在「已在合格储存区」——供 UI 复用原版 <c>MessageAlreadyInStorage</c> 提示。
        /// **必须与 <c>ShouldRelaxVanillaRejection(Thing)</c> 的第三条同时成立**，否则会出现
        /// 「既不放宽、也不给理由」的静默拒绝。
        /// </summary>
        public static bool IsBlockedByValidStorage(Thing thing)
            => IsCommandableShape(thing) && !AlreadyDesignatedHaul(thing) && IsInValidStorage(thing);

        /// <summary>是否已有 <c>Haul</c> designation（Def 未加载时按「无」处理，双加由原版 AddDesignation 自守）。</summary>
        public static bool AlreadyDesignatedHaul(Thing thing)
        {
            var def = DesignationDefOf.Haul;
            var manager = thing?.Map?.designationManager;
            if (def == null || manager == null) return false;
            return manager.DesignationOn(thing, def) != null;
        }

        /// <summary>是否已在合格储存区（<c>RimWorld.StoreUtility.IsInValidStorage</c>，公开扩展方法）。</summary>
        public static bool IsInValidStorage(Thing thing) => thing != null && thing.IsInValidStorage();

        /// <summary>
        /// 原版「**裸拒**」判定（受理为假且**未给出理由**）—— 放宽分支的唯一入口条件。
        ///
        /// <para><b>⚠ 必须用 <c>string.IsNullOrEmpty</c>，绝不能用 <c>Reason != null</c></b>：
        /// 原版 <c>AcceptanceReport.WasRejected = new AcceptanceReport(""){ acceptedInt = false }</c>
        /// （<c>_research/vanilla/Assembly-CSharp/Verse/AcceptanceReport.cs:43-52</c>）⇒ 设计器里
        /// 直接 <c>return false;</c> 产生的报告，其 <c>Reason</c> 是**空串而非 null**。
        /// 用 <c>!= null</c> 判据会对每一条裸拒都判成「已有理由」⇒ 放宽分支**永不执行**。
        /// 2026-10-12 实机「框选后没有石块那样的高亮」正是此缺陷暴露：高亮走
        /// <c>DesignatorUtility.RenderHighlightOverSelectableThings</c> 的
        /// <c>CanDesignateThing(...).Accepted</c>（<c>Verse/DesignatorUtility.cs:55-73</c>），
        /// 而该处从未被放宽过。</para>
        ///
        /// <para>带具名理由的拒绝（如 <c>MessageAlreadyInStorage</c>）**不是**裸拒 —— 必须原样透出，
        /// 否则玩家会看到「既不放宽也不给理由」的静默拒绝。</para>
        /// </summary>
        public static bool IsBareRejection(AcceptanceReport report)
            => !report.Accepted && string.IsNullOrEmpty(report.Reason);

        /// <summary>
        /// 格内**唯一候选选择器**（放宽口径的形态过滤，与 <c>IsCommandableShape</c> 同源）。
        /// 原版 <c>GridsUtility.GetFirstHaulable</c> 只认 <c>designateHaulable</c>；本选择器补上
        /// <c>alwaysHaulable</c> 的物品，供 <c>CanDesignateCell</c> / <c>DesignateSingleCell</c>
        /// 两处**同一个**口径使用（两处各写一套条件必然漂移）。
        ///
        /// <para><b>R2 教训</b>：R1 把这个口径实现为一个 Harmony 挂点（挂 <c>GetFirstHaulable</c>），
        /// 实机未生效（小方法被 Mono JIT 内联 ⇒ 补丁挂在不被执行的调用点）。现改为**普通方法**，
        /// 由 <c>Designator_Haul</c> 最外层虚方法的补丁主动调用，不再依赖内联行为。</para>
        /// </summary>
        public static Thing FirstCommandableIn(IList<Thing> things)
        {
            if (things == null) return null;
            for (int i = 0; i < things.Count; i++)
                if (IsCommandableShape(things[i])) return things[i];
            return null;
        }

        /// <summary>格内候选（Map 适配器；<c>thingGrid</c> 缺失时按「无」处理，fail-closed 不猜）。</summary>
        public static Thing FindCommandableInCell(IntVec3 c, Map map)
            => FirstCommandableIn(map?.thingGrid?.ThingsListAt(c));
    }
}

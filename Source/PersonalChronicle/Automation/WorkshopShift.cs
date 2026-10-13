using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间「班组班次」：玩家自定义「一段时间内需要几个人在岗」。
    ///
    /// <para><b>与既有两种排班的分工</b></para>
    /// <list type="bullet">
    /// <item><see cref="WorkshopSlot"/>（固定班）= <b>点名制</b>：指定某人某时段上岗，无名额概念；</item>
    /// <item><see cref="RotationRule"/>（轮岗班）= 一组人按间隔轮换 <b>1</b> 个岗位；</item>
    /// <item><b>本类（班组）</b>= <b>名额制</b>：一组候选人在某时段内自动上岗 <b>N</b> 人
    /// （名额由 <c>Domain.WorkshopShiftPlanner</c> 按技能评分分配，玩家不指定具体是谁）。</item>
    /// </list>
    ///
    /// <para><b>优先级</b>：固定班 &gt; 班组 &gt; 轮岗 —— 同一名 pawn 同一时刻只会归一个岗位
    /// （由 <c>MapComponent_WorkshopManager</c> 的统一调度计划裁决，避免"两处都算他该上岗"）。
    /// 同类之间按 <see cref="Priority"/> 大者优先。</para>
    ///
    /// <para>存档：随 <c>MapComponent_WorkshopManager</c> 以 <c>LookMode.Deep</c> 持久化；
    /// Pawn 引用用 <c>LookMode.Reference</c>（与 <see cref="RotationRule.Pool"/> 同规格）。</para>
    /// </summary>
    public sealed class WorkshopShift : IExposable
    {
        public Zone_Workshop TargetZone;

        /// <summary>值班起始小时（0-23，含）。</summary>
        public int StartHour = 6;

        /// <summary>值班结束小时（0-23，不含；小于 StartHour 表示跨午夜，与 <see cref="WorkshopHours.InRange"/> 同口径）。</summary>
        public int EndHour = 18;

        // ---- 班中餐（CHG-20261004-SHIFTMEAL，2026-10-04 玩家需求）-----------------
        //
        // 玩家原话：「建议班次设置表优化，添加用餐时间段设置」「标准三餐，七点、十二点、五点」。
        // 动因：倒班制下白班与夜班的餐点本来就不该是同一个时间，全局三餐对倒班不合适
        // ⇒ 每条班次可配自己的一日三餐。
        //
        // 语义（CHG-20261005-MEALHOUR：由"起止段"改为**单时间点**）：
        //   · 每个时间点是一个**小时**（0-23），与原版管制表的小时格**一一对应**；
        //   · **-1 = 该餐关闭**（不再有"回落全局"——全局三餐设置已在上一批删除）；
        //   · 时间点**不受"班次工作时段"限制**（CHG-20261005-MEALHOUR-FIX，2026-10-05 玩家实机）：
        //     跨午夜班次配"上班前吃饭"（20:00 班次配 19:00）必须生效 ⇒
        //     越界餐点由"用餐优先于睡眠"覆盖（见 ApplyWorkHoursToCrew 的判定顺序）。
        // ⇒ 加性可选字段 + 默认值，**旧档读取即为"未设置"**；旧"起止段"键的迁移见 ExposeData。
        // 判定收口在 WorkshopDutyCoordinator.BuildCrewMealHours（逐班次：配了用配的，
        // 且**与该班次自己的工作时段取交集** ⇒ 白班餐点不会落到夜班表上）。

        /// <summary>
        /// 早餐时间点（0-23；<b>-1 = 该餐关闭</b>）。
        /// <para>CHG-20261005-MEALHOUR（2026-10-05 玩家要求「三餐不要时间段，改为时间选择，
        /// 选择单个时间」）：由"起止段"（Start/End 两字段）改为<b>单点</b> —— 语义更贴"到点吃饭"，
        /// 且与原版管制表的小时格<b>天然一一对应</b>（一个时间点 = 涂一格），
        /// 不再需要"段 → 格"的展开。</para>
        /// </summary>
        public int MealBreakfastHour = -1;

        /// <summary>午餐时间点（0-23；-1 = 该餐关闭）。</summary>
        public int MealLunchHour = -1;

        /// <summary>晚餐时间点（0-23；-1 = 该餐关闭）。</summary>
        public int MealDinnerHour = -1;

        // ---- 旧档迁移中转（CHG-20261005-MEALHOUR）----
        // 旧的"起止段"六键（mealBreakfastStartHour/EndHour 等）只在**读档期**读取：
        // 取起点作为单点（7-8 ⇒ 7 点）；旧终点在单点模型下无意义，直接丢弃。
        // 这三个字段只在 LoadingVars → PostLoadInit 之间存活，**不落盘**。

        private int legacyMealBreakfastStart = -1;
        private int legacyMealLunchStart = -1;
        private int legacyMealDinnerStart = -1;

        /// <summary>本班次是否配置了班中餐（任一时间点 &ge; 0）；false ⇒ 该班次无餐点（不再回落全局）。</summary>
        public bool HasShiftMeal
        {
            get
            {
                return MealBreakfastHour >= 0
                    || MealLunchHour >= 0
                    || MealDinnerHour >= 0;
            }
        }

        /// <summary>
        /// 清空班中餐配置（三餐全部关闭）。
        /// <para>CHG-20261005-MEALHOUR：原注释写"回到跟随全局"—— 全局三餐已删除 ⇒ 清空 = 没有餐点。</para>
        /// </summary>
        public void ClearShiftMeal()
        {
            MealBreakfastHour = -1;
            MealLunchHour = -1;
            MealDinnerHour = -1;
        }

        /// <summary>
        /// 上岗名额；<b>0 = 不限人数</b>（全部合格候选者都上岗）。
        ///
        /// <para>⚠ <b>UI 已撤下（2026-10-02）</b>：用户实测结论「班次只需要岗位、优先级就行。名额、
        /// 门槛等等无作用」⇒ 班次编辑器与班次表不再提供该项，新建班次一律用默认值 <c>0</c>。
        /// 字段<b>保留</b>：旧档照常读取并生效（无 Migration）；若想让旧班次回到「不限」，
        /// 删除该班次重建即可。</para>
        /// </summary>
        public int RequiredCount;

        public WorkshopTaskType TaskType = WorkshopTaskType.Production;

        /// <summary>门槛技能（null = 不设门槛）。⚠ UI 已撤下（2026-10-02），字段保留以兼容旧档。</summary>
        public SkillDef RequiredSkill;

        /// <summary>门槛等级（配合 <see cref="RequiredSkill"/>）。⚠ UI 已撤下（2026-10-02）。</summary>
        public int MinSkillLevel;

        /// <summary>与其他班组冲突时的优先级，大者优先（固定班始终高于班组）。</summary>
        public int Priority;

        /// <summary>
        /// 候选池；为空 = 回落车间人员名单（与 <see cref="RotationRule.Pool"/> 同语义）。
        /// ⚠ UI 已撤下（2026-10-02）：派工改由「班组编制 + 倒班表」决定；字段保留以兼容旧档。
        /// </summary>
        public List<Pawn> Pool = new List<Pawn>();

        /// <summary>
        /// 是否参与<b>班组倒班</b>（企业化排班，2026-09-11）：为 true 时，本班次的候选池由
        /// 「当日被排到 <see cref="ShiftSlotIndex"/> 班次序位」的班组（<see cref="WorkshopCrew"/>）
        /// 决定，`Pool` 不再参与；该班组当天休息 ⇒ 本班次当天停班。
        /// false（默认）= 沿用既有手工排班（Pool/车间名单），老档行为不变。
        /// </summary>
        public bool UseCrewCycle;

        /// <summary>
        /// 班次序位（0 = 第 1 班，如早班）；<c>-1</c> = 未指定。
        /// 仅 <see cref="UseCrewCycle"/> 为 true 时有意义；套用班制时会按预设窗口顺序自动填 0/1/2。
        /// </summary>
        public int ShiftSlotIndex = -1;

        /// <summary>
        /// 持久化唯一 ID（2026-10-02）：供「班组 ⇄ 班次」这类<b>跨对象关联</b>使用。
        ///
        /// <para><b>为什么需要它</b>：<see cref="WorkshopShift"/> 只实现 <c>IExposable</c>，不是
        /// <c>ILoadReferenceable</c>。而班组（<see cref="WorkshopCrew"/>）原先用
        /// <c>Scribe_Collections.Look(ref AssignedShifts, "assignedShifts", LookMode.Reference)</c>
        /// 保存班次引用 —— 该列表一旦非空，保存即抛
        /// <c>InvalidOperationException: Cannot save reference to ....WorkshopShift item if it is not ILoadReferenceable</c>，
        /// <b>导致整次存档失败</b>（用户 2026-10-02 实机报错，表现为"指派班组后无法保存"）。</para>
        ///
        /// <para>现在改为「按 ID 关联 + 加载后解析」：本字段持久化，运行时由
        /// <c>MapComponent_WorkshopManager.ShiftById</c> 还原引用 ——
        /// 完全不依赖原版引用机制，零跨对象引用风险。</para>
        ///
        /// <para>分配时机由管理器统一负责（新增时 <c>AddShift</c>、读档时 <c>EnsureShiftIds</c>），
        /// <b>调用方不必关心</b>。</para>
        /// </summary>
        public int LoadId = -1;

        /// <summary>有效候选池（池空 → 车间人员名单；两者皆空 → null）。</summary>
        public List<Pawn> EffectivePool
        {
            get
            {
                if (Pool != null && Pool.Count > 0)
                {
                    return Pool;
                }
                return TargetZone != null ? TargetZone.Members : null;
            }
        }

        /// <summary>该小时是否落在本班组时段内。</summary>
        public bool InTime(int hour)
        {
            return WorkshopHours.InRange(hour, StartHour, EndHour);
        }

        /// <summary>
        /// 技能门槛判定（无门槛恒真；技能被禁用或等级不足视为不合格）。
        /// 与 <see cref="WorkshopSlot.MeetsSkill"/> 保持同一口径（同一玩家预期，两套排班不应有差异）。
        /// </summary>
        public bool MeetsSkill(Pawn pawn)
        {
            if (RequiredSkill == null)
            {
                return true;
            }
            if (pawn == null || pawn.skills == null)
            {
                return false;
            }
            SkillRecord record = pawn.skills.GetSkill(RequiredSkill);
            if (record == null || record.TotallyDisabled)
            {
                return false;
            }
            return record.Level >= MinSkillLevel;
        }

        public void ExposeData()
        {
            // 唯一 ID 先于其它字段（2026-10-02：班组按它关联本对象，见 LoadId 注释）。
            Scribe_Values.Look(ref LoadId, "loadId", -1);
            Scribe_References.Look(ref TargetZone, "targetZone");
            Scribe_Values.Look(ref StartHour, "startHour", 6);
            Scribe_Values.Look(ref EndHour, "endHour", 18);
            // CHG-20261005-MEALHOUR：班中餐 = **单时间点**（-1 = 该餐关闭）。加性可选字段。
            Scribe_Values.Look(ref MealBreakfastHour, "mealBreakfastHour", -1);
            Scribe_Values.Look(ref MealLunchHour, "mealLunchHour", -1);
            Scribe_Values.Look(ref MealDinnerHour, "mealDinnerHour", -1);
            // ⚠ 旧档兼容（CHG-20261004-SHIFTMEAL 的"起止段"键）：
            //   ① 只在 **LoadingVars** 期调用旧键的 Look ⇒ 新存档**不再写**这些键（自然淘汰），
            //      旧档里的多余键由 Scribe 忽略 ⇒ 不需要 Migration、不 bump schemaVersion；
            //   ② 只读旧"起点"（旧"终点"在单点模型下无意义，不读 = 忽略）；
            //   ③ 迁移在下面 PostLoadInit 里做（那时新字段已读毕，能判断"是否为空"）。
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref legacyMealBreakfastStart, "mealBreakfastStartHour", -1);
                Scribe_Values.Look(ref legacyMealLunchStart, "mealLunchStartHour", -1);
                Scribe_Values.Look(ref legacyMealDinnerStart, "mealDinnerStartHour", -1);
            }
            // 缺省 0（不限名额），与字段默认值一致；仅在本字段缺失时生效 —— 旧档里的值照用。
            Scribe_Values.Look(ref RequiredCount, "requiredCount", 0);
            Scribe_Values.Look(ref TaskType, "taskType", WorkshopTaskType.Production);
            Scribe_Defs.Look(ref RequiredSkill, "requiredSkill");
            Scribe_Values.Look(ref MinSkillLevel, "minSkillLevel", 0);
            Scribe_Values.Look(ref Priority, "priority", 0);
            Scribe_Values.Look(ref UseCrewCycle, "useCrewCycle", false);
            Scribe_Values.Look(ref ShiftSlotIndex, "shiftSlotIndex", -1);
            Scribe_Collections.Look(ref Pool, "pool", LookMode.Reference);
            // CHG-20261005-MEALHOUR：旧档"起止段" → 单点迁移（幂等：新字段已有值则不覆盖）。
            // 口径：取旧**起点**（7-8 ⇒ 7 点）——"到点吃饭"落在起点最贴近原意。
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (MealBreakfastHour < 0 && legacyMealBreakfastStart >= 0)
                {
                    MealBreakfastHour = legacyMealBreakfastStart;
                }
                if (MealLunchHour < 0 && legacyMealLunchStart >= 0)
                {
                    MealLunchHour = legacyMealLunchStart;
                }
                if (MealDinnerHour < 0 && legacyMealDinnerStart >= 0)
                {
                    MealDinnerHour = legacyMealDinnerStart;
                }
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit && Pool == null)
            {
                Pool = new List<Pawn>();
            }
        }
    }
}

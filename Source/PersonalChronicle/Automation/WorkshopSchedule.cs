using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 车间岗位的任务语义。2026-10-02 起除展示外还驱动<b>原版工作面板联动</b>：
    /// 岗位 → 该车间类型声明的原版 WorkTypeDef 列表（见
    /// <see cref="WorkshopTypeDef.WorkTypeDefNamesOf"/>），由管理面板在排班变更时自动勾选。
    /// </summary>
    public enum WorkshopTaskType : byte
    {
        Production,
        Maintenance,
        QualityCheck,
        Standby
    }

    /// <summary>轮岗模式。</summary>
    public enum RotationMode : byte
    {
        /// <summary>轮流：池首移到池尾，依次上岗。</summary>
        RoundRobin,
        /// <summary>技能最高者优先上岗。</summary>
        SkillBased,
        /// <summary>手动优先级：池内顺序即优先级，永远取首位。</summary>
        PriorityBased
    }

    /// <summary>
    /// 班次时段的<b>显示格式</b>（UI 层）。
    ///
    /// <para><b>2026-10-02 分层归位</b>：数据判定（<c>InRange</c> / <c>CrossesMidnight</c>，
    /// 纯整数区间数学）已下沉到 <see cref="PersonalChronicle.Domain.WorkshopTime"/>。
    /// 本类只保留<b>显示格式</b> —— 格式串是 UI 关注点，既不该与判定逻辑混在一起，
    /// 也不该让 Domain 反向引用。</para>
    ///
    /// <para>下方两个判定方法保留为<b>薄转发</b>，以免改动 8 处调用点；
    /// 新代码请直接用 <see cref="PersonalChronicle.Domain.WorkshopTime"/>。</para>
    /// </summary>
    public static class WorkshopHours
    {
        /// <summary>时段显示文本（<c>HH:00 – HH:00</c>）。排班页、班次编辑器、班组行共用同一实现。</summary>
        public static string LabelOf(int start, int end)
        {
            return start.ToString("00", System.Globalization.CultureInfo.InvariantCulture)
                + ":00 – "
                + end.ToString("00", System.Globalization.CultureInfo.InvariantCulture)
                + ":00";
        }

        /// <summary>是否跨午夜（薄转发 → <see cref="PersonalChronicle.Domain.WorkshopTime"/>）。</summary>
        public static bool CrossesMidnight(int start, int end)
        {
            return PersonalChronicle.Domain.WorkshopTime.CrossesMidnight(start, end);
        }

        /// <summary>该小时是否在班次窗口内（薄转发 → <see cref="PersonalChronicle.Domain.WorkshopTime"/>）。</summary>
        public static bool InRange(int hour, int start, int end)
        {
            return PersonalChronicle.Domain.WorkshopTime.InRange(hour, start, end);
        }
    }

    /// <summary>
    /// 单个排班槽位：指派某位殖民者在某车间的某时段上岗。
    ///
    /// 存档纪律（参考文档 §8，已按 1.6 修正）：
    /// - Pawn / Zone_Workshop 用 <see cref="Scribe_References"/>（引用类型）；
    /// - SkillDef 用 <see cref="Scribe_Defs"/>；
    /// - enum 与 int 是<b>值类型</b>，必须 <see cref="Scribe_Values"/>
    ///   （参考文档把 WorkshopTaskType 用 Scribe_Defs 序列化是错的）。
    /// </summary>
    public sealed class WorkshopSlot : IExposable
    {
        public Pawn AssignedPawn;
        public long EconomicContractId;
        public Zone_Workshop TargetZone;

        /// <summary>上岗小时（0-23）。</summary>
        public int StartHour = 6;

        /// <summary>下岗小时（0-23，可小于 StartHour 表示跨午夜班）。</summary>
        public int EndHour = 18;

        public WorkshopTaskType TaskType = WorkshopTaskType.Production;

        /// <summary>门槛技能（null = 不设门槛）。</summary>
        public SkillDef RequiredSkill;

        public int MinSkillLevel;

        /// <summary>同时段多槽冲突时的优先级，大者优先。</summary>
        public int Priority;

        public void ExposeData()
        {
            Scribe_References.Look(ref AssignedPawn, "assignedPawn");
            Scribe_Values.Look(ref EconomicContractId, "economicContractId", 0L);
            Scribe_References.Look(ref TargetZone, "targetZone");
            Scribe_Values.Look(ref StartHour, "startHour", 6);
            Scribe_Values.Look(ref EndHour, "endHour", 18);
            Scribe_Values.Look(ref TaskType, "taskType", WorkshopTaskType.Production);
            Scribe_Defs.Look(ref RequiredSkill, "requiredSkill");
            Scribe_Values.Look(ref MinSkillLevel, "minSkillLevel", 0);
            Scribe_Values.Look(ref Priority, "priority", 0);
        }

        /// <summary>该 pawn 当前是否命中本槽位（时段 + 技能门槛 + 目标车间存活）。</summary>
        public bool Matches(Pawn pawn, int hour)
        {
            // Legacy fee contracts are preserved for reconciliation, not executed without wages.
            // New economic contracts will use their own verified route; ordinary slots keep ID 0.
            if (EconomicContractId != 0) return false;
            if (pawn == null || AssignedPawn != pawn)
            {
                return false;
            }
            // 判活统一走 WorkshopZoneUtility.IsAlive（体检 P2-③）：
            // 旧判据 TargetZone.Map == null 对「已注销但未清格」的车间**恒为假**
            // （Zone.Deregister() 不置空 zoneManager，而 Zone.Map => zoneManager.map），
            // 曾导致死车间在最长一个检查间隔内仍被排班视为有效。
            if (!WorkshopZoneUtility.IsAlive(TargetZone))
            {
                return false;
            }
            if (!WorkshopHours.InRange(hour, StartHour, EndHour))
            {
                return false;
            }
            return MeetsSkill(pawn);
        }

        public bool MeetsSkill(Pawn pawn)
        {
            if (RequiredSkill == null)
            {
                return true;
            }
            // 公共方法须自防 null（体检 P2-⑪）：与 WorkshopShift.MeetsSkill 同口径。
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
    }

    /// <summary>
    /// 轮岗规则：一组殖民者共享同一车间的同一班次，按 <see cref="Mode"/> 轮换上岗。
    /// 只有 <see cref="Current"/> 命中时段时才算「该他上岗」。
    /// </summary>
    public sealed class RotationRule : IExposable
    {
        public Zone_Workshop TargetZone;

        public int StartHour = 6;
        public int EndHour = 18;

        /// <summary>换班间隔（游戏 tick）。</summary>
        public int IntervalTicks = 2500;

        public RotationMode Mode = RotationMode.RoundRobin;

        /// <summary>
        /// 轮换池。PriorityBased 模式下池内顺序即优先级。
        /// 为空时自动回落到 <see cref="TargetZone"/>.Members —— 与车间人员页联动，
        /// 人员增删即时生效，无需重建规则。
        /// </summary>
        public List<Pawn> Pool = new List<Pawn>();

        /// <summary>
        /// 有效轮换池（池空 → 回落车间人员名单；两者皆无 → null）。
        /// <b>公开</b>是必要的：推进门槛（<c>ProcessRotations</c>）与 UI 都必须按同一口径判断池规模，
        /// 旧代码门槛读的是 <see cref="Pool"/> ⇒ 从未填充过的池恒为 0 ⇒ 轮岗永不推进（体检 P1-B1）。
        /// </summary>
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

        /// <summary>有效池规模（null → 0；供 UI 与门槛共用，避免各处重新实现回落判断）。</summary>
        public int EffectivePoolCount
        {
            get
            {
                List<Pawn> pool = EffectivePool;
                return pool != null ? pool.Count : 0;
            }
        }

        public int ActiveIndex;

        /// <summary>下次换班的绝对游戏 tick。</summary>
        public int NextRotateTick;

        /// <summary>当前应上岗的人（池为空或越界时为 null）。</summary>
        public Pawn Current
        {
            get
            {
                List<Pawn> pool = EffectivePool;
                if (pool == null || pool.Count == 0)
                {
                    return null;
                }
                int idx = ActiveIndex;
                if (idx < 0 || idx >= pool.Count)
                {
                    idx = 0;
                }
                return pool[idx];
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref TargetZone, "targetZone");
            Scribe_Values.Look(ref StartHour, "startHour", 6);
            Scribe_Values.Look(ref EndHour, "endHour", 18);
            Scribe_Values.Look(ref IntervalTicks, "intervalTicks", 2500);
            Scribe_Values.Look(ref Mode, "mode", RotationMode.RoundRobin);
            Scribe_Collections.Look(ref Pool, "pool", LookMode.Reference);
            Scribe_Values.Look(ref ActiveIndex, "activeIndex", 0);
            Scribe_Values.Look(ref NextRotateTick, "nextRotateTick", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && Pool == null)
            {
                Pool = new List<Pawn>();
            }
        }

        public bool Matches(Pawn pawn, int hour)
        {
            // 判活统一走 WorkshopZoneUtility.IsAlive（体检 P2-③），理由同 WorkshopSlot.Matches。
            if (pawn == null || !WorkshopZoneUtility.IsAlive(TargetZone))
            {
                return false;
            }
            if (!WorkshopHours.InRange(hour, StartHour, EndHour))
            {
                return false;
            }
            return Current == pawn;
        }

        /// <summary>按模式推进一轮轮换（由 MapComponent 在到点时调用）。</summary>
        public void Advance()
        {
            List<Pawn> pool = EffectivePool;
            if (pool == null || pool.Count == 0)
            {
                ActiveIndex = 0;
                return;
            }
            switch (Mode)
            {
                case RotationMode.RoundRobin:
                    ActiveIndex = (ActiveIndex + 1) % pool.Count;
                    break;
                case RotationMode.SkillBased:
                    ActiveIndex = BestSkillIndex(pool);
                    break;
                default:
                    // PriorityBased：池内顺序即优先级，恒取首位。
                    ActiveIndex = 0;
                    break;
            }
            ClampActiveIndex();
        }

        /// <summary>
        /// 池变化后校正下标（越界/负数 → 0）。purge 与推进共用同一实现，
        /// 避免"夹取基准 ≠ 实际取值来源"的漂移（体检 P2-⑨：池被打空后
        /// <see cref="EffectivePool"/> 回落到车间名单，旧代码却按自定义池长度夹取）。
        /// </summary>
        public void ClampActiveIndex()
        {
            if (ActiveIndex < 0 || ActiveIndex >= EffectivePoolCount)
            {
                ActiveIndex = 0;
            }
        }

        /// <summary>
        /// SkillBased 模式下选出技能最高者。评分口径与班组名额分配**共用**
        /// <see cref="WorkshopDutyScoring"/>（门槛技能 → 车间类型主技能 → Crafting）——
        /// 旧实现恒用 <c>Crafting</c>，忽略车间类型声明的主技能（体检 P2-⑥）。
        /// 同分取池内靠前者（与名额分配的 tie-break 一致，避免岗位来回漂移）。
        /// </summary>
        private int BestSkillIndex(List<Pawn> pool)
        {
            int best = 0;
            int bestLevel = int.MinValue;
            for (int i = 0; i < pool.Count; i++)
            {
                int level = WorkshopDutyScoring.ScoreOf(TargetZone, null, pool[i]);
                if (level > bestLevel)
                {
                    bestLevel = level;
                    best = i;
                }
            }
            return best;
        }
    }
}

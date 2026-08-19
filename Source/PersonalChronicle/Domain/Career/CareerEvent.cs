using System.Collections.Generic;
using PersonalChronicle.Domain.Profession;
using Verse;

namespace PersonalChronicle.Domain.Career
{
    /// <summary>
    /// P1 CAREER-001 职业事实层：单一职业事件类型。
    ///
    /// 设计铁律（来自 PM 任务单 BE-002）：CareerEvent 表示**事实**，绝不承载评价。
    /// 例如 "制造传奇品质动力核心" 是一个事实；而 "技术贡献 +9 / 高级工程师资格 +1 /
    /// 勋章积分 +10" 属于后续评价层（P3/P4/P7），**不在本类**。
    ///
    /// 与现有通用 <see cref="ChronicleEvent"/> 解耦：ChronicleEvent 服务于时间轴/事件流
    /// 渲染；CareerEvent 是职业系统的独立事实 ledger，未来评价规则变更不会改写历史事实。
    ///
    /// 持久化：经 <see cref="PawnObject.CareerData"/> 由 Scribe 保存（append-only，不 bump
    /// Schema Version，符合 v1.1.4 工坊/住所同策略）。
    /// </summary>
    public sealed class CareerEvent : IExposable
    {
        /// <summary>稳定唯一 id（运行时生成，持久化后保持；用于去重/排序锚点）。</summary>
        public string EventId;

        /// <summary>所属 Pawn 的 StableId（= Pawn.GetUniqueLoadID()）；冗余存储便于跨 Pawn 校验。</summary>
        public string PawnId;

        /// <summary>发生 tick（Find.TickManager.TicksGame）。</summary>
        public long Tick;

        /// <summary>事件类型（见 <see cref="CareerEventType"/>）。仅 P1 允许的 5 种。</summary>
        public string EventType;

        /// <summary>相关 Def 稳定键（如 ThingDef.defName / RecipeDef.defName）；语言无关，UI 解析 LabelCap。</summary>
        public string DefName;

        /// <summary>相关技能 Def 稳定键（如 SkillDef.defName）；无则 null/空。</summary>
        public string SkillDefName;

        /// <summary>品质键（QualityCategory 的 name，如 "Legendary"）；无品质则 null/空。</summary>
        public string Quality;

        /// <summary>
        /// D2 决策（P2 追加，append-only）：来源 RecipeDef.defName。旧存档事件为 null
        /// （无配方信息），新事件必填。用于配方相关度匹配（V2.0 §7 XP 计算）。
        /// </summary>
        public string RecipeDefName;

        /// <summary>
        /// D2 决策（P2 追加，append-only）：产出数量（批量产物 >1）。旧存档缺省 1。
        /// </summary>
        public int Quantity = 1;

        /// <summary>自由元数据（键值对，扩展点，不写评价结果）。</summary>
        public Dictionary<string, string> Metadata;

        public CareerEvent()
        {
        }

        /// <summary>构造一个事实事件（不含任何评价字段）。</summary>
        public CareerEvent(string eventId, string pawnId, long tick, string eventType,
            string defName, string skillDefName, string quality, Dictionary<string, string> metadata)
            : this(eventId, pawnId, tick, eventType, defName, skillDefName, quality,
                  recipeDefName: null, quantity: 1, metadata)
        {
        }

        /// <summary>构造一个事实事件（P2 含配方信息）。</summary>
        public CareerEvent(string eventId, string pawnId, long tick, string eventType,
            string defName, string skillDefName, string quality,
            string recipeDefName, int quantity, Dictionary<string, string> metadata)
        {
            EventId = eventId;
            PawnId = pawnId;
            Tick = tick;
            EventType = eventType;
            DefName = defName;
            SkillDefName = skillDefName;
            Quality = quality;
            RecipeDefName = recipeDefName;
            Quantity = quantity;
            Metadata = metadata;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref EventId, "eventId");
            Scribe_Values.Look(ref PawnId, "pawnId");
            Scribe_Values.Look(ref Tick, "tick", 0L);
            Scribe_Values.Look(ref EventType, "eventType");
            Scribe_Values.Look(ref DefName, "defName");
            Scribe_Values.Look(ref SkillDefName, "skillDefName");
            Scribe_Values.Look(ref Quality, "quality");
            Scribe_Values.Look(ref RecipeDefName, "recipeDefName");
            Scribe_Values.Look(ref Quantity, "quantity", 1);
            Scribe_Collections.Look(ref Metadata, "metadata", LookMode.Value, LookMode.Value);
            if (Metadata == null) Metadata = new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// P1 BE-004 职业事件类型系统。第一版**只允许 5 种**，禁止扩展几十种事件——
    /// 目的是验证架构而非堆功能。后续阶段（P2/P3...）按需追加，但须经 PM 验收。
    /// </summary>
    public static class CareerEventType
    {
        /// <summary>完成一次工作单位（基础劳作）。</summary>
        public const string WorkCompleted = "WorkCompleted";

        /// <summary>制造产出一件物品（BE-005 第一个真实原版事件源）。</summary>
        public const string ItemProduced = "ItemProduced";

        /// <summary>完成一座建筑建造。</summary>
        public const string ConstructionCompleted = "ConstructionCompleted";

        /// <summary>完成一项研究。</summary>
        public const string ResearchCompleted = "ResearchCompleted";

        /// <summary>写成一本书籍。</summary>
        public const string BookProduced = "BookProduced";

        /// <summary>P5 实践/理论考试通过（事实记录，不承载评分数值）。</summary>
        public const string ExamPassed = "ExamPassed";

        /// <summary>P6 论文答辩通过（事实记录）。</summary>
        public const string ThesisDefended = "ThesisDefended";

        /// <summary>P7 获得职称（事实记录，DefName=TitleDef.defName）。</summary>
        public const string TitleGranted = "TitleGranted";

        /// <summary>P8 获得勋章（事实记录，DefName=MedalDef.defName）。</summary>
        public const string MedalGranted = "MedalGranted";

        /// <summary>返回当前版本允许的全部事件类型（白名单）。</summary>
        public static readonly IReadOnlyList<string> Allowed = new[]
        {
            WorkCompleted, ItemProduced, ConstructionCompleted, ResearchCompleted, BookProduced,
            ExamPassed, ThesisDefended, TitleGranted, MedalGranted
        };

        /// <summary>判定该类型是否在 P1 白名单内（防止误写未授权事件）。</summary>
        public static bool IsAllowed(string eventType)
        {
            if (string.IsNullOrEmpty(eventType))
            {
                return false;
            }
            for (int i = 0; i < Allowed.Count; i++)
            {
                if (string.Equals(Allowed[i], eventType, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// P1 BE-001 Pawn 级职业数据容器（概念上即 CompCareer）。
    ///
    /// 仅承载事实 ledger（CareerEvent 列表）+ 最小聚合计数（CareerRecord 占位，P1 不写评价）。
    /// 禁止：职称逻辑 / 勋章逻辑 / UI 状态 / 保存 Texture 或 Def 实例 / 大量 Pawn 引用。
    /// </summary>
    public sealed class CareerData : IExposable
    {
        /// <summary>职业事实 ledger（按 Tick 升序追加；P1 仅 ItemProduced 真实写入）。</summary>
        public List<CareerEvent> Events = new List<CareerEvent>();

        /// <summary>
        /// BE-003 CareerRecord 占位：事实 → 记录 的聚合点。P1 仅维护 EventType 计数
        /// （事实聚合，非评价）。后续评价层（P3 贡献 / P4 职称）在此之上派生，不污染事实。
        /// </summary>
        public Dictionary<string, int> RecordCountByType = new Dictionary<string, int>();

        /// <summary>
        /// P2-A Q1 决策：Pawn 职业状态容器（专业技能状态/方向/能力 XP）。append-only
        /// （旧存档为 null，null-safe），经 Scribe_Deep "professional" 持久化，不 bump schema。
        /// 只承载状态派生（由 CareerEvent 事实计算），绝不反向写回 Events。
        /// </summary>
        public ProfessionalState Professional;

        /// <summary>P5 资格状态机容器（D-Q1：挂 CareerData 下，与 Professional 同级）。</summary>
        public PersonalChronicle.Domain.Qualification.QualificationState Qualification;

        /// <summary>P5 考试数据容器（实践/理论）。</summary>
        public PersonalChronicle.Domain.Qualification.ExamData Exams;

        /// <summary>P6 书籍证据列表（理论证据，D-B1）。</summary>
        public List<PersonalChronicle.Domain.Qualification.BookEvidence> Books;

        /// <summary>P6 论文/答辩数据容器。</summary>
        public PersonalChronicle.Domain.Qualification.ThesisData Thesis;

        /// <summary>P7 已授予职称记录（append-only）。</summary>
        public List<PersonalChronicle.Domain.Honor.GrantedTitle> GrantedTitles;

        public CareerData()
        {
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref Events, "events", LookMode.Deep);
            Scribe_Collections.Look(ref RecordCountByType, "recordCountByType", LookMode.Value, LookMode.Value);
            Scribe_Deep.Look(ref Professional, "professional");
            Scribe_Deep.Look(ref Qualification, "qualification");
            Scribe_Deep.Look(ref Exams, "exams");
            Scribe_Collections.Look(ref Books, "books", LookMode.Deep);
            Scribe_Deep.Look(ref Thesis, "thesis");
            Scribe_Collections.Look(ref GrantedTitles, "grantedTitles", LookMode.Deep);
            if (Events == null) Events = new List<CareerEvent>();
            if (RecordCountByType == null) RecordCountByType = new Dictionary<string, int>();
        }
    }
}

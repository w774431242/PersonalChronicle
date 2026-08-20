// 职业档案 Read Model 视图（ITab_Pawn_Career 总览/职称/勋章三页消费）。
// 全部由 CareerData 真实数据派生；空数据 → HasData=false / 空列表，UI 显示空态（不造假）。
using System.Collections.Generic;

namespace PersonalChronicle.Archive.ReadModels
{
    /// <summary>
    /// 职业事实计数快照（UI-001 / ARC-002：事实聚合归属 Provider，窗口只消费）。
    /// 统一从 <c>CareerData.RecordCountByType</c> 聚合全部 9 类事件计数，
    /// 替代各页面在绘制路径里直接 <c>CountEvents(po, ...)</c> 直查 Domain 的违规写法。
    /// 只读、无 Save 语义、由 BuildCareerFactCounts 构建。
    /// </summary>
    public sealed class CareerFactCounts
    {
        public int WorkCompleted;
        public int ItemProduced;
        public int ConstructionCompleted;
        public int ResearchCompleted;
        public int BookProduced;
        public int ExamPassed;
        public int ThesisDefended;
        public int TitleGranted;
        public int MedalGranted;
    }

    /// <summary>
    /// 职业档案 · 总览视图（对齐 docs/UI预览/人物档案视窗/职业档案Tab预览.html 总览 4 区块）：
    /// 职业身份 / 当前资格状态 / 资格预检 / 下一职称。窗口只消费，不重新判定。
    /// </summary>
    public sealed class CareerOverviewView
    {
        /// <summary>是否有任何职业数据（无 CareerData 或全空 → UI 空态）。</summary>
        public bool HasData;

        // —— 职业身份 ——
        /// <summary>当前职称（已授予的最高档 TitleDef label；未评定 → null）。</summary>
        public string RoleName;
        /// <summary>方向/专业描述（如 "精密制造 · 机械工程"）。</summary>
        public string RoleDesc;
        /// <summary>专业技能文本（如 "精密制造 Lv25"）。</summary>
        public string SkillText;
        /// <summary>相关工时文本（如 "640 h"）。</summary>
        public string HoursText;
        /// <summary>重大成果数（ItemProduced 事实计数；与 Made/Built/Researched 口径统一来自 RecordCountByType）。</summary>
        public int Results;
        /// <summary>专业著作数（BookEvidence 计数）。</summary>
        public int Books;
        // —— 事实计数指标（UI-001：统一由 Provider 从 RecordCountByType 聚合，窗口只消费，禁止绘制路径直查 Domain）——
        /// <summary>制造产出件数（CareerEventType.ItemProduced 聚合）。</summary>
        public int Made;
        /// <summary>建造完成数（CareerEventType.ConstructionCompleted 聚合）。</summary>
        public int Built;
        /// <summary>研究完成数（CareerEventType.ResearchCompleted 聚合）。</summary>
        public int Researched;

        // —— 下一职称 ——
        /// <summary>下一职称名（已封顶 → null）。</summary>
        public string NextTitle;
        /// <summary>晋升准备度 0~100。</summary>
        public int Progress;
        /// <summary>当前缺口标签（如 "专业等级"/"理论考试"；无缺口 → 空）。</summary>
        public IReadOnlyList<string> NextGaps = new List<string>();

        // —— 当前资格状态（6 条件，目标 = 下一档资格）——
        public IReadOnlyList<CareerQualView> Qual = new List<CareerQualView>();

        // —— 资格预检（6 条件）——
        public IReadOnlyList<CareerPreCheckView> PreCheck = new List<CareerPreCheckView>();
    }

    /// <summary>资格状态单行（要求/达标判定/状态）。</summary>
    public sealed class CareerQualView
    {
        /// <summary>条件名（如 "专业等级"）。</summary>
        public string Label;
        /// <summary>要求说明（如 "精密制造 ≥ 38"）。</summary>
        public string Note;
        /// <summary>状态键：ok / wait。</summary>
        public string StateKey;
        /// <summary>状态文本（满足/未满足/通过/待进行）。</summary>
        public string StateText;
    }

    /// <summary>资格预检单行。</summary>
    public sealed class CareerPreCheckView
    {
        public string Label;
        /// <summary>状态键：done / pending / not-started。</summary>
        public string StateKey;
        public string StateText;
    }

    /// <summary>职称链单档（5 档，对齐 Defs/QualificationDefs.xml）。</summary>
    public sealed class CareerTitleTierView
    {
        /// <summary>ProfessionalTitleDef.defName。</summary>
        public string DefName;
        /// <summary>职称名（已翻译）。</summary>
        public string Label;
        /// <summary>状态键：granted(已获) / current(当前) / next(下一阶) / locked(未开始)。</summary>
        public string StateKey;
        /// <summary>状态文本。</summary>
        public string StateText;
        /// <summary>门槛说明（如 "等级 ≥ 25 且前置资格"）。</summary>
        public string Note;
    }
}

using System.Collections.Generic;
using Verse;

namespace PersonalChronicle.Automation
{
    /// <summary>
    /// 「车间岗位」Def（`CHG-20261003-POSTS` · Phase 1）。
    ///
    /// <para><b>与 <see cref="WorkshopProcessDef"/> 的分工</b>：工序表回答「一道<b>工序</b>要什么」
    /// （工序 → 岗位 → 技能 → 原版工作类型 → 期望设备，五维）；本 Def 回答「一个人在这个车间里
    /// <b>当什么岗</b>」—— 分类依据取自<b>现实规模化工厂</b>的岗位划分（操作员 / 技术员 /
    /// 质检员 / 物料搬运员 / 工程师 / 医务 / 安保 / 计划员），而不是 RimWorld 的 WorkTypeDef。
    /// 两者用 <see cref="postKey"/> ↔ <see cref="taskTypeKey"/> 衔接。</para>
    ///
    /// <para><b>L2 决策：映射而非 fork（用户 2026-10-03 裁决）</b>：岗位通过
    /// <see cref="CoveredWorkTypeNames"/> <b>引用原版 <c>WorkTypeDef</c></b>，
    /// 行为完全交给原版 <c>WorkGiver</c>。**不为岗位新建自有 WorkTypeDef / WorkGiver** ——
    /// 那等于 fork 整个工作系统（30+ WorkGiver 要重绑），原版一升级就漂移，风险高一到两个数量级。
    /// 本 Def 里的 <c>workTypeDefNames</c> 字段名与工序表刻意区分，避免误以为是同一张表。</para>
    ///
    /// <para><b>纪律（GOV-006 / GOV-009）</b>：本表<b>只</b>在 XML（<c>Defs/WorkshopPosts.xml</c>）
    /// 声明；C# 侧零 defName 字面量、零 <c>switch</c>。解析一律 <c>GetNamedSilentFail</c> +
    /// 每 defName 只告警一次（COMP-011，沿 <c>WorkshopProcessCatalog</c> 先例）。</para>
    /// </summary>
    public sealed class WorkshopPostDef : Def
    {
        /// <summary>
        /// 岗位键（稳定 ID，GOV-009：逻辑只认 defName，不认 label / 翻译）。
        /// <para>与 <see cref="WorkshopProcessDef.taskTypeKey"/> <b>不是</b>同一键空间：那个是
        /// 工序内部的粗粒度任务类型（生产/维护/质检/待命），这个是工厂岗位。
        /// 两者靠 <see cref="dutyTaskTypeKey"/> 衔接。</para>
        /// </summary>
        public string postKey = string.Empty;

        /// <summary>
        /// 该岗位对应的值勤任务类型键：<c>Production</c> / <c>Maintenance</c> / <c>QualityCheck</c> /
        /// <c>Standby</c>，或留空 = <b>非值勤岗</b>（不产生 <c>PC_WorkshopOperation</c> 的 Job）。
        /// </summary>
        public string dutyTaskTypeKey = string.Empty;

        /// <summary>
        /// 该岗位承接的<b>原版</b> <c>WorkTypeDef</c> defName 列表（L2 映射面）。
        /// <para>留空 = 纯 PC 内部岗（只表达组织角色，不承接任何原版工作）。</para>
        /// </summary>
        public List<string> coveredWorkTypeDefNames = new List<string>();

        /// <summary>该岗位门槛技能（原版 <c>SkillDef</c> defName 列表；留空 = 不设门槛）。</summary>
        public List<string> skillDefNames = new List<string>();

        /// <summary>门槛等级（<see cref="minSkillLevel"/> = 0 时不生效）。</summary>
        public int minSkillLevel;

        /// <summary>
        /// 所需身体能力（原版 <c>PawnCapacityDef</c> defName，如 <c>Manipulation</c> / <c>Moving</c>；
        /// 留空 = 不设门槛）。
        /// </summary>
        public List<string> requiredCapacityDefNames = new List<string>();

        /// <summary>
        /// ★ 该岗位<b>是否真的能在车间内独立交付</b>它承接的服务。
        ///
        /// <para><b>这是 Phase 4「能力对等闸门」的唯一判定依据</b>：只有为 <c>true</c> 的岗位，
        /// 将来才允许<b>禁用</b>对应的原版工作（否则会造成"无人搬运 ⇒ 工单永远补不上料 ⇒
        /// 产线停摆"这类死锁）。Phase 1 只声明、不实现任何 Job，因此除已存在的值勤链外
        /// 一律填 <c>false</c>，并在 <see cref="notes"/> 写明"未实现"。</para>
        ///
        /// <para><b>不得为"看起来完整"而虚填 true</b>——虚填会直接导致 Phase 4 禁用原版工作后
        /// 殖民地失去该服务且无人替代。</para>
        /// </summary>
        public bool deliversOwnService;

        /// <summary>
        /// ★ 该员工<b>真的实现了</b>的原版工作类型（L2 映射面的"已交付"子集）。
        ///
        /// <para><b>为什么必须与 <see cref="coveredWorkTypeDefNames"/> 分开</b>
        /// （CHG-20261004-STAFF · 玩家口径「这是一套现代化工业岗位体系」）：</para>
        /// <list type="bullet">
        /// <item><c>coveredWorkTypeDefNames</c> = <b>能力声明</b>：这个岗位<b>将来</b>要承接什么。
        /// 它是<b>规划表</b>，可以写得很全（现代化工厂里每个工种都要有对应岗位）。</item>
        /// <item><c>implementedWorkTypeDefNames</c> = <b>已交付</b>：车间里<b>真的已经有 Job</b>
        /// 在做这些活。接管闸门<b>只认这个字段</b>。</item>
        /// </list>
        ///
        /// <para><b>不拆开会怎样</b>：能力声明一旦被当成"能做到"，接管就会清零玩家的原版工作，
        /// 而车间对应的 Job 尚未实现 ⇒ <b>工人被清零了工作又没人替代，站在车间里什么也不干</b>。
        /// 实机已因此出现"Research 被操作员的全集上界误覆盖 ⇒ 科技停滞"的隐患。</para>
        ///
        /// <para><b>当前状态</b>：车间只实现了"值勤"Job（站岗），<b>没有任何生产 Job</b>
        /// ⇒ 本字段全部留空 ⇒ 接管自动空转 ⇒ 逐个实现 Job 时再逐个点亮，零风险。</para>
        /// </summary>
        public List<string> implementedWorkTypeDefNames = new List<string>();

        /// <summary>
        /// ★ 该岗位**豁免「岗位落地」**（CHG-20261005-POSTLAND，2026-10-05 玩家口径
        /// 「将这些岗位进行落地，**除操作员工暂时不动**，其他都对应原版工作类型进行落地」）。
        ///
        /// <para><b>什么是"落地"</b>：把岗位分配结果落到<b>原版工作优先级</b>上 ——
        /// 担任某岗位的成员，其岗位对应的原版工作类型被<b>勾选并设为优先档</b>；
        /// <b>其它工作一律不动</b>（不清零、不接管 —— 后者会因 PC 没有对应 Job 而造成
        /// "没人建造/研究/种植"的灾难，见 <c>WorkshopPostLandingService</c> 的类注释）。</para>
        ///
        /// <para><b>默认 false = 参与落地</b>（反语义设计）：这样新增岗位<b>默认生效</b>，
        /// 只有明确要排除的岗位才需要写一句；若默认改成"不落地"，漏写一个字段就会让岗位
        /// 静默失效（比"多生效一个"更难排查）。</para>
        ///
        /// <para>纪律同全表：只在 XML（<c>Defs/WorkshopPosts.xml</c>）声明，C# 侧零 defName 字面量。</para>
        /// </summary>
        public bool landingExempt;

        /// <summary>岗位边界说明（实机口径 / 未实现项 / 与原版的关系）。面向玩家可读。</summary>
        public string notes = string.Empty;

        private static readonly List<string> EmptyNames = new List<string>();

        /// <summary>安全取「承接的原版工作类型」列表（永不为 null，防漏写 NPE）。</summary>
        public List<string> CoveredWorkTypeNames()
        {
            return coveredWorkTypeDefNames ?? EmptyNames;
        }

        /// <summary>
        /// 安全取「<b>已实现</b>的原版工作类型」列表（永不为 null）。
        /// <para><b>接管闸门的唯一输入</b>（见 <see cref="implementedWorkTypeDefNames"/>）。</para>
        /// </summary>
        public List<string> ImplementedWorkTypeNames()
        {
            return implementedWorkTypeDefNames ?? EmptyNames;
        }

        /// <summary>安全取门槛技能列表（永不为 null）。</summary>
        public List<string> SkillNames()
        {
            return skillDefNames ?? EmptyNames;
        }

        /// <summary>安全取能力门槛列表（永不为 null）。</summary>
        public List<string> RequiredCapacityNames()
        {
            return requiredCapacityDefNames ?? EmptyNames;
        }

        /// <summary>该岗位是否为值勤岗（能被 <c>PC_WorkshopOperation</c> 下发 Job）。</summary>
        public bool IsDutyPost
        {
            get { return !string.IsNullOrEmpty(dutyTaskTypeKey); }
        }

        /// <summary>该岗位是否承接了至少一个原版工作类型（纯内部岗为 false）。</summary>
        public bool CoversVanillaWork
        {
            get { return CoveredWorkTypeNames().Count > 0; }
        }
    }
}
